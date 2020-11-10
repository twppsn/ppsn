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
using System.Runtime.InteropServices;
using System.Security;

namespace TecWare.PPSn
{
	#region -- struct LUID ------------------------------------------------------------

	internal struct LUID
	{
		public int LowPart;
		public int HighPart;
	} // struct LUID

	#endregion

	#region -- struct LUID ------------------------------------------------------------
	
	internal struct LUID_AND_ATTRIBUTES
	{
		public LUID pLuid;
		public int Attributes;
	} // struct LUID_AND_ATTRIBUTES

	#endregion

	#region -- struct LUID ------------------------------------------------------------

	internal struct TOKEN_PRIVILEGES
	{
		public int PrivilegeCount;
		public LUID_AND_ATTRIBUTES Privileges;
	} // struct TOKEN_PRIVILEGES

	#endregion

	#region -- class NativeMethods ----------------------------------------------------

	[SuppressUnmanagedCodeSecurity]
	internal static class NativeMethods
	{
		private const string user32 = "user32.dll";
		private const string advapi32 = "advapi32.dll";

		public const string SE_SHUTDOWN_NAME = "SeShutdownPrivilege";
		public const short SE_PRIVILEGE_ENABLED = 2;
		public const short TOKEN_ADJUST_PRIVILEGES = 32;
		public const short TOKEN_QUERY = 8;

		public const ushort EWX_LOGOFF = 0;
		public const ushort EWX_POWEROFF = 0x00000008;
		public const ushort EWX_REBOOT = 0x00000002;
		public const ushort EWX_RESTARTAPPS = 0x00000040;
		public const ushort EWX_SHUTDOWN = 0x00000001;
		public const ushort EWX_FORCE = 0x00000004;

		[DllImport(user32, SetLastError = true)]
		public static extern int ExitWindowsEx(uint uFlags, uint dwReason);

		[DllImport(advapi32, SetLastError = true)]
		public static extern int OpenProcessToken(IntPtr ProcessHandle, int DesiredAccess, out IntPtr TokenHandle);
		[DllImport(advapi32, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool AdjustTokenPrivileges(IntPtr TokenHandle, [MarshalAs(UnmanagedType.Bool)] bool DisableAllPrivileges, ref TOKEN_PRIVILEGES NewState, UInt32 BufferLength, IntPtr PreviousState, IntPtr ReturnLength);
		[DllImport(advapi32, CharSet = CharSet.Unicode, SetLastError = true)]
		public static extern int LookupPrivilegeValue(string lpSystemName, string lpName, out LUID lpLuid);
	} // class NativeMethods

	#endregion
}
