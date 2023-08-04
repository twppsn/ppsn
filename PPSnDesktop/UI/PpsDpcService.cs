﻿#region -- copyright --
//
// Licensed under the EUPL, Version 1.1 or - as soon they will be approved by the
// European Commission - subsequent versions of the EUPL(the "Licence"); You may
// not use this work except in compliance with the Licence.
//
// You may obtain a copy of the Licence at:
// http://ec.europa.eu/idabc/eupl
//
// Unless required by applicable law or agreed to in writing, software distributed
// under the Licence is distributed on an "AS IS" basis, WITHOUT WARRANTIES OR
// CONDITIONS OF ANY KIND, either express or implied. See the Licence for the
// specific language governing permissions and limitations under the Licence.
//
#endregion
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using TecWare.DE.Networking;
using TecWare.DE.Stuff;
using TecWare.PPSn.Stuff;
using static TecWare.PPSn.NativeMethods;

namespace TecWare.PPSn.UI
{
	#region -- interface IPpsSettingRestartCondition ----------------------------------

	internal interface IPpsSettingRestartCondition
	{
		bool IsChanged(PpsSettingsInfoBase settings);

		string Setting { get; }
	} // interface IPpsSettingRestartCondition

	#endregion

	#region -- class PpsDpcService ----------------------------------------------------

	[
	PpsService(typeof(PpsDpcService))
	]
	internal class PpsDpcService : IPpsShellService, INotifyPropertyChanged
	{
		private readonly WeakEventList<PropertyChangedEventHandler, PropertyChangedEventArgs> propertyChangedList = new WeakEventList<PropertyChangedEventHandler, PropertyChangedEventArgs>();

		private readonly IPpsShell shell;
		private bool isUnlocked = false;
		private bool isLocked;
		private bool isRestartNeeded = false;

		private readonly Dictionary<string, IPpsSettingRestartCondition> conditions = new Dictionary<string, IPpsSettingRestartCondition>();

		#region -- Ctor/Dtor ----------------------------------------------------------

		public PpsDpcService(IPpsShell shell)
		{
			this.shell = shell ?? throw new ArgumentNullException(nameof(shell));
			shell.Settings.PropertyChanged += Settings_PropertyChanged;
#if DEBUG
			isUnlocked = true;
#endif
			CheckIsLockedProperty();
		} // ctor

		private void Settings_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == PpsShellSettings.DpcDebugModeKey)
				CheckIsLockedProperty();
			else if (conditions.TryGetValue(e.PropertyName, out var condition) && condition.IsChanged(shell.Settings))
				ScheduleRestart(condition.ToString());
		} // event Settings_PropertyChanged

		#endregion

		#region -- Property Changed ---------------------------------------------------

		public event PropertyChangedEventHandler PropertyChanged
		{
			add => propertyChangedList.Add(value);
			remove => propertyChangedList.Remove(value);
		} // event PropertyChanged

		private bool Set<T>(ref T field, T value, string propertyName)
		{
			if (!Equals(field, value))
			{
				field = value;
				propertyChangedList.Invoke(this, new PropertyChangedEventArgs(propertyName));
				return true;
			}
			else
				return false;
		} // func Set

		#endregion

		#region -- Dpc - http ---------------------------------------------------------

		private DEHttpClient CreateDpcHttpClient()
			=> DEHttpClient.Create(shell.Settings.DpcUri, shell.Settings.GetDpcCredentials());

		public async Task PushMessageAsync(LogMsgType type, string message)
		{
			try
			{
				string GetLogType()
				{
					switch (type)
					{
						case LogMsgType.Warning:
							return "w";
						case LogMsgType.Error:
							return "e";
						default:
							return "i";
					}
				} // func GetLogType

				using (var http = CreateDpcHttpClient())
				using (var r = await http.GetResponseAsync($"./?action=logmsg&id={shell.DeviceId}&typ={GetLogType()}&msg={Uri.EscapeDataString(message)}"))
				{
				}
			}
			catch (Exception e)
			{
				Debug.Print(e.ToString());
			}
		} // func PushMessageAsync

		public async Task WriteLogToTempAsync()
		{
			var logService = shell.GetService<IPpsLogService>(false);
			if (logService != null)
			{
				var text = logService.GetLogAsText();
				await Task.Run(() => File.WriteAllText(Path.Combine(Path.GetTempPath(), "PPSnDesktop.txt"), text));
			}
		} // proc WriteLogToTempAsync

		public async Task PushLogAsync(string log)
		{
			using (var http = CreateDpcHttpClient())
			using (var content = new StringContent(log, Encoding.UTF8, MimeTypes.Text.Plain))
				await http.GetResponseAsync($"./?action=logpush&id={shell.DeviceId}", putContent: content);
		} // func PushLogAsync

		#endregion

		#region -- Lock, Unlock -------------------------------------------------------

		private bool CheckIsLockedProperty()
		{
			if (isUnlocked)
				return Set(ref isLocked, false, nameof(IsLocked));
			else if (shell.Settings.IsDebugMode)
				return Set(ref isLocked, false, nameof(IsLocked));
			else
				return Set(ref isLocked, true, nameof(IsLocked));
		} // proc CheckIsLockedProperty

		public bool UnlockWithPin(string pin)
		{
			isUnlocked = IsDpcPin(pin);
			CheckIsLockedProperty();
			return isUnlocked;
		} // proc UnlockWithPin

		public bool UnlockWithCode(string code)
		{
			if (IsDpcUnlockCode(code))
			{
				isUnlocked = !isUnlocked;
				CheckIsLockedProperty();
				return true;
			}
			else
				return false;
		} // proc UnlockWithCode

		public bool Lock()
		{
			isUnlocked = false;
			CheckIsLockedProperty();
			return isUnlocked;
		} // func Lock

		public bool IsDpcUnlockCode(string code)
		{
			if (code == null)
				return false;

			var unlockCode = shell.Settings.DpcUnlockCode;
			if (unlockCode == null)
				return false;

			return unlockCode == code; 
		} // proc IsDpcUnlockCode

		public bool IsDpcPin(string pin)
			=> pin == null ? isUnlocked : pin == shell.Settings.DpcPin;

		#endregion

		#region -- Shell Mode ---------------------------------------------------------

		private const string shellRegPath = @"Software\Microsoft\Windows NT\CurrentVersion\Winlogon";

		private static string GetShellEntry()
		{
			using (var k = Registry.CurrentUser.OpenSubKey(shellRegPath, false))
			{
				if (k == null)
					return String.Empty;
				else
					return k.GetValue("Shell") is string r ? r : String.Empty;
			}
		} // func GetShellEntry

		/// <summary>Set shell entry.</summary>
		public static void SetShellEntry()
		{
			using (var k = Registry.CurrentUser.OpenSubKey(shellRegPath, true))
			{
				k.SetValue("Shell", typeof(App).Assembly.Location);
				k.SetValue("AutoRestartShell", 1, RegistryValueKind.DWord);
			}
		} // fucn SetShellEntry

		/// <summary>Remove application as shell.</summary>
		public static void RemoveShellEntry()
		{
			using (var k = Registry.CurrentUser.OpenSubKey(shellRegPath, true))
			{
				if (k != null)
				{
					k.DeleteValue("Shell");
					k.DeleteValue("AutoRestartShell");
				}
			}
		} // proc RemoveShellEntry

		public static bool GetIsShellMode()
			=> String.Compare(GetShellEntry(), typeof(App).Assembly.Location, true) == 0;

		public bool IsShellMode => GetIsShellMode();

		#endregion

		#region -- Execute ------------------------------------------------------------

		private static void ExecuteCore(string command, string arguments, bool runasAdministrator = false)
		{
			var psi = new ProcessStartInfo
			{
				FileName = command,
				Arguments = arguments,
				WindowStyle = ProcessWindowStyle.Maximized,
				WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
			};

			if (runasAdministrator && !PpsWpfShell.IsAdministrator())
				psi.Verb = "runas";

			Process.Start(psi)?.Dispose();
		} // proc ExecuteCore

		private static string FindRemoteDebugger()
		{
			var fi = new FileInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft Visual Studio 16.0", "Common7", "IDE", "Remote Debugger", "x64", "msvsmon.exe"));
			return fi.Exists ? fi.FullName : null;
		} // funcFindRemoteDebugger

		public static void Execute(string command, string args = null, bool runasAdministrator = false)
		{
			if (command == null)
				throw new ArgumentNullException(nameof(command));
			else if (command == "cmd")
				command = "cmd.exe";
			else if (command == "calc")
				command = "calc.exe";
			else if (command == "rdbg")
				command = FindRemoteDebugger();
			else if (command == "settings")
				command = "ms-settings:";

			ExecuteCore(command, args, runasAdministrator);
		} // proc Execute

		#endregion

		#region -- Shutdown/Restart ---------------------------------------------------

		private static void SetPrivileges()
		{
			TOKEN_PRIVILEGES p;

			if (OpenProcessToken(Process.GetCurrentProcess().Handle, TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out var hProcess) == 0)
				throw new Win32Exception();

			p.PrivilegeCount = 1;
			p.Privileges.Attributes = SE_PRIVILEGE_ENABLED;
			if (LookupPrivilegeValue(String.Empty, SE_SHUTDOWN_NAME, out p.Privileges.pLuid) == 0)
				throw new Win32Exception();

			AdjustTokenPrivileges(hProcess, false, ref p, 0, IntPtr.Zero, IntPtr.Zero);
		} // proc SetPrivileges

		public static void ShutdownOperationSystem(bool restart)
		{
			SetPrivileges();
			ExitWindowsEx((uint)(restart ? EWX_REBOOT : EWX_SHUTDOWN) | EWX_FORCE, 0);
		} // proc ShutdownOperationSystem

		public static void LogoffOperationSystem()
			=> ExitWindowsEx(EWX_LOGOFF, 0);

		#endregion

		#region -- Restart Information ------------------------------------------------

		public void AddSettingRestartConditions(IEnumerable<IPpsSettingRestartCondition> restartConditions)
		{
			foreach (var c in restartConditions)
				conditions[c.Setting] = c;
		} // proc AddSettingRestartConditions

		public void ScheduleRestart(string reason)
		{
			shell.LogProxy("Shutdown").LogMsg(LogMsgType.Information, reason);

			Set(ref isRestartNeeded, true, nameof(IsRestartNeeded));
		} // proc ScheduleRestart

		#endregion

		public bool IsLocked => isLocked;
		public bool IsRestartNeeded => isRestartNeeded;

		public IPpsShell Shell => shell;
	} // class PpsDpcService

	#endregion
}
