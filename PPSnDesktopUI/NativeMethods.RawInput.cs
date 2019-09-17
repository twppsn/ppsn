using System;
using System.Runtime.InteropServices;
using System.Text;

namespace TecWare.PPSn
{
	#region -- struct RAWINPUTDEVICELIST ----------------------------------------------

	[StructLayout(LayoutKind.Sequential)]
	internal struct RAWINPUTDEVICELIST
	{
		public IntPtr hDevice;
		public uint dwType;
	} // struct RAWINPUTDEVICELIST

	#endregion

	#region -- struct RAWINPUTDEVICE --------------------------------------------------

	[StructLayout(LayoutKind.Sequential)]
	internal struct RAWINPUTDEVICE
	{
		[MarshalAs(UnmanagedType.U2)]
		public ushort wUsagePage;
		[MarshalAs(UnmanagedType.U2)]
		public ushort wUsage;
		[MarshalAs(UnmanagedType.U4)]
		public uint dwFlags;
		public IntPtr hwndTarget;
	} // struct RAWINPUTDEVICE

	#endregion

	#region -- struct RAWINPUTHEADER --------------------------------------------------

	[StructLayout(LayoutKind.Sequential)]
	internal struct RAWINPUTHEADER
	{
		[MarshalAs(UnmanagedType.U4)]
		public int dwType;
		[MarshalAs(UnmanagedType.U4)]
		public int dwSize;
		public IntPtr hDevice;
		public IntPtr wParam;
	} // struct RAWINPUTHEADER

	#endregion

	#region -- struct RAWINPUT --------------------------------------------------------

	[StructLayout(LayoutKind.Explicit)]
	internal struct RAWINPUT
	{
		[FieldOffset(0)]
		public RAWINPUTHEADER header;
		//[FieldOffset(16 oder 24)]
		//public RAWMOUSE mouse;
		//[FieldOffset(16 oder 24)]
		//public RAWKEYBOARD keyboard;
		//[FieldOffset(16 oder 24)]
		//public RAWHID hid;
	} // struct RAWINPUT

	#endregion

	#region -- struct RAWKEYBOARD -----------------------------------------------------

	[StructLayout(LayoutKind.Sequential)]
	internal struct RAWKEYBOARD
	{
		[MarshalAs(UnmanagedType.U2)]
		public ushort MakeCode;
		[MarshalAs(UnmanagedType.U2)]
		public ushort Flags;
		[MarshalAs(UnmanagedType.U2)]
		public ushort Reserved;
		[MarshalAs(UnmanagedType.U2)]
		public ushort VKey;
		[MarshalAs(UnmanagedType.U4)]
		public uint Message;
		[MarshalAs(UnmanagedType.U4)]
		public int ExtraInformation;
	} // struct RAWKEYBOARD

	#endregion

	#region -- class NativeMethods ----------------------------------------------------

	internal static partial class NativeMethods
	{
		public const uint KLF_NOTELLSHELL = 0x00000080;

		public const uint RIDI_DEVICENAME = 0x20000007;
		public const uint RID_INPUT = 0x10000003;
		public const uint RIM_TYPEKEYBOARD = 1;

		[DllImport(user32, SetLastError = true)]
		public extern static bool RegisterRawInputDevices(RAWINPUTDEVICE[] pRawInputDevice, int iNumDevices, int cbSize);
		[DllImport(user32, SetLastError = true)]
		public extern static uint GetRawInputData(IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, int cbSizeHeader);
		[DllImport(user32)]
		public static extern int GetRawInputDeviceList(IntPtr pRawInputDeviceList, ref uint dwNumDevices, uint dwSize);
		[DllImport(user32)]
		public static extern uint GetRawInputDeviceInfo(IntPtr hDevice, uint dwCommand, IntPtr pData, ref uint dataSize);
		[return: MarshalAs(UnmanagedType.Bool)]
		[DllImport(user32, SetLastError = true)]
		public extern static bool DefRawInputProc(IntPtr paRawInput, uint dwInput, int cbSizeHeader);

		[DllImport(user32, SetLastError = true, CharSet = CharSet.Unicode)]
		public static extern int ToUnicodeEx(uint uCode, uint uScanCode, byte[] lpKeyState, [Out, MarshalAs(UnmanagedType.LPWStr, SizeConst = 64)] StringBuilder sbBuf, int iBuf, uint uFlags, IntPtr hkl);
		[DllImport(user32, SetLastError = true)]
		public extern static uint MapVirtualKeyEx(uint dwCode, uint dwMapType, IntPtr hkl);

		[DllImport("shlwapi.dll", BestFitMapping = false, CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = false, ThrowOnUnmappableChar = true)]
		public static extern int SHLoadIndirectString(string pszSource, StringBuilder pszOutBuf, int cchOutBuf, IntPtr ppvReserved);

		[DllImport(user32)]
		public static extern IntPtr GetKeyboardLayout(uint idThread);
		[DllImport(user32)]
		public static extern IntPtr LoadKeyboardLayout(string pwszKLID, uint Flags);
		[DllImport(user32)]
		public static extern bool UnloadKeyboardLayout(IntPtr hkl);
	} // class UnsafeNativeMethods

	#endregion
}
