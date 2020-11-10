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
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Win32;
using Neo.IronLua;
using TecWare.DE.Data;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;
using static TecWare.PPSn.NativeMethods;

namespace TecWare.PPSn.UI
{
	#region -- class PpsLockService ---------------------------------------------------

	[
	PpsService(typeof(PpsLockService))
	]
	internal class PpsLockService : ObservableObject, IPpsShellService
	{
		private readonly IPpsShell shell;
		private bool isUnlocked = false;
		private bool isLocked;
		
		#region -- Ctor/Dtor ----------------------------------------------------------

		public PpsLockService(IPpsShell shell)
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
		} // event Settings_PropertyChanged

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

		public bool Unlock(string pin)
		{
			isUnlocked = IsDpcPin(pin);
			CheckIsLockedProperty();
			return isUnlocked;
		} // proc Unlock

		public bool Lock()
		{
			isUnlocked = false;
			CheckIsLockedProperty();
			return isUnlocked;
		} // func Lock

		public bool IsDpcPin(string pin)
		{
			return pin == null
				? isUnlocked
				: pin == shell.Settings.DpcPin;
		} // func IsDpcPin

		#endregion

		#region -- SendLock -----------------------------------------------------------

		public Task SendLogAsync()
		{
			return Task.CompletedTask;
		} // proc SendLogAsync

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

		public bool IsLocked => isLocked;

		public IPpsShell Shell => shell;
	} // class PpsLockService

	#endregion
}
