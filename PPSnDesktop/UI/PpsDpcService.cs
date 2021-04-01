#region -- copyright --
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
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using TecWare.DE.Data;
using TecWare.DE.Networking;
using TecWare.DE.Stuff;
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
	internal class PpsDpcService : ObservableObject, IPpsShellService
	{
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
			isUnlocked = IsDpcUnlockCode(code);
			CheckIsLockedProperty();
			return isUnlocked;
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
				return isUnlocked;

			var unlockCode = shell.Settings.DpcUnlockCode;
			if (unlockCode == null)
				return isUnlocked;

			return unlockCode == code; 
		} // proc IsDpcUnlockCode

		public bool IsDpcPin(string pin)
		{
			return pin == null
				? isUnlocked
				: pin == shell.Settings.DpcPin;
		} // func IsDpcPin

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
