using System;
using System.ComponentModel;
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

	#region -- struct RAWHID ----------------------------------------------------------

	[StructLayout(LayoutKind.Sequential)]
	internal struct RAWHID
	{
		public uint dwSizeHid;
		public uint dwCount;
	} // struct RAWHID

	#endregion

	#region -- struct RID_DEVICE_INFO -------------------------------------------------

	[StructLayout(LayoutKind.Explicit)]
	internal struct RID_DEVICE_INFO
	{
		[FieldOffset(0)]
		public uint cbSize;
		[FieldOffset(4)]
		public uint dwType;
		//[FieldOffset(8)]
		//public RID_DEVICE_INFO_MOUSE mouse;
		[FieldOffset(8)]
		public RID_DEVICE_INFO_KEYBOARD keyboard;
		[FieldOffset(8)]
		public RID_DEVICE_INFO_HID hid;
	} // struct RID_DEVICE_INFO

	#endregion

	#region -- struct RID_DEVICE_INFO_MOUSE -------------------------------------------

	//[StructLayout(LayoutKind.Sequential)]
	//internal struct RID_DEVICE_INFO_MOUSE
	//{
	//	public int dwId;
	//	public int dwNumberOfButtons;
	//	public int dwSampleRate;
	//	public bool fHasHorizontalWheel;
	//} // struct RID_DEVICE_INFO_MOUSE

	#endregion

	#region -- struct RID_DEVICE_INFO_KEYBOARD ----------------------------------------

	[StructLayout(LayoutKind.Sequential)]
	internal struct RID_DEVICE_INFO_KEYBOARD
	{
		public int dwType;
		public int dwSubType;
		public int dwKeyboardMode;
		public int dwNumberOfFunctionKeys;
		public int dwNumberOfIndicators;
		public int dwNumberOfKeysTotal;
	} // struct RID_DEVICE_INFO_KEYBOARD

	#endregion

	#region -- struct RID_DEVICE_INFO_HID ---------------------------------------------

	[StructLayout(LayoutKind.Sequential)]
	internal struct RID_DEVICE_INFO_HID
	{
		public int dwVendorId;
		public int dwProductId;
		public int dwVersionNumber;
		public ushort usUsagePage;
		public ushort usUsage;
	} // struct RID_DEVICE_INFO_HID

	#endregion

	#region -- struct MOUSEINPUT ------------------------------------------------------

	[StructLayout(LayoutKind.Sequential)]
	internal struct MOUSEINPUT
	{
		public int dx;
		public int dy;
		public uint mouseData;
		public uint dwFlags;
		public uint time;
		public IntPtr dwExtraInfo;
	} // struct MOUSEINPUT

	#endregion

	#region -- struct KEYBDINPUT ------------------------------------------------------

	[StructLayout(LayoutKind.Sequential)]
	internal struct KEYBDINPUT
	{
		public ushort wVk;
		public ushort wScan;
		public uint dwFlags;
		public uint time;
		public IntPtr dwExtraInfo;
	} // struct KEYBDINPUT

	#endregion

	#region -- struct HARDWAREINPUT ---------------------------------------------------

	[StructLayout(LayoutKind.Sequential)]
	internal struct HARDWAREINPUT
	{
		public int uMsg;
		public short wParamL;
		public short wParamH;
	} // struct HARDWAREINPUT

	#endregion

	#region -- class NativeMethods ----------------------------------------------------

	internal static partial class NativeMethods
	{
		public const int INPUT_MOUSE = 0;
		public const int INPUT_KEYBOARD = 1;
		public const int INPUT_HARDWARE = 2;

		public const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
		public const uint KEYEVENTF_KEYUP = 0x0002;
		public const uint KEYEVENTF_UNICODE = 0x0004;
		public const uint KEYEVENTF_SCANCODE = 0x0008;

		public const uint XBUTTON1 = 0x0001;
		public const uint XBUTTON2 = 0x0002;

		public const uint MOUSEEVENTF_MOVE = 0x0001;
		public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
		public const uint MOUSEEVENTF_LEFTUP = 0x0004;
		public const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
		public const uint MOUSEEVENTF_RIGHTUP = 0x0010;
		public const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
		public const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
		public const uint MOUSEEVENTF_XDOWN = 0x0080;
		public const uint MOUSEEVENTF_XUP = 0x0100;
		public const uint MOUSEEVENTF_WHEEL = 0x0800;
		public const uint MOUSEEVENTF_HWHEEL = 0x1000;
		public const uint MOUSEEVENTF_VIRTUALDESK = 0x4000;
		public const uint MOUSEEVENTF_ABSOLUTE = 0x8000;

		public const uint KLF_NOTELLSHELL = 0x00000080;

		public const uint RIDI_DEVICENAME = 0x20000007;
		public const uint RIDI_DEVICEINFO = 0x2000000B;
		public const uint RID_INPUT = 0x10000003;
		public const uint RIM_TYPEKEYBOARD = 1;
		public const uint RIM_TYPEHID = 2;

		[DllImport(user32, SetLastError = true)]
		public extern static bool RegisterRawInputDevices(RAWINPUTDEVICE[] pRawInputDevice, int iNumDevices, int cbSize);
		[DllImport(user32, SetLastError = true)]
		public extern static uint GetRawInputData(IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, int cbSizeHeader);
		[DllImport(user32)]
		public static extern int GetRawInputDeviceList(IntPtr pRawInputDeviceList, ref uint dwNumDevices, uint dwSize);
		[DllImport(user32)]
		public static extern int GetRawInputDeviceInfo(IntPtr hDevice, uint dwCommand, IntPtr pData, ref uint dataSize);
		[return: MarshalAs(UnmanagedType.Bool)]
		[DllImport(user32, SetLastError = true)]
		public extern static bool DefRawInputProc(IntPtr paRawInput, uint dwInput, int cbSizeHeader);

		[DllImport(user32, SetLastError = true, CharSet = CharSet.Unicode)]
		public static extern int ToUnicodeEx(uint uCode, uint uScanCode, byte[] lpKeyState, [Out, MarshalAs(UnmanagedType.LPWStr, SizeConst = 64)] StringBuilder sbBuf, int iBuf, uint uFlags, IntPtr hkl);
		[DllImport(user32, SetLastError = true)]
		public extern static uint MapVirtualKeyEx(uint dwCode, uint dwMapType, IntPtr hkl);

		[DllImport(user32)]
		public static extern IntPtr GetMessageExtraInfo();

		#region -- SendInput ----------------------------------------------------------

		#region -- struct INPUT -------------------------------------------------------

		[StructLayout(LayoutKind.Explicit)]
		private struct INPUT_64
		{
			[FieldOffset(0)]
			public int type;

			[FieldOffset(8)]
			public MOUSEINPUT mi;
			[FieldOffset(8)]
			public KEYBDINPUT ki;
			[FieldOffset(8)]
			public HARDWAREINPUT hi;
		} // struct INPUT_64

		[StructLayout(LayoutKind.Explicit)]
		private struct INPUT_32
		{
			[FieldOffset(0)]
			public int type;

			[FieldOffset(4)]
			public MOUSEINPUT mi;
			[FieldOffset(4)]
			public KEYBDINPUT ki;
			[FieldOffset(4)]
			public HARDWAREINPUT hi;
		} // struct INPUT_32

		#endregion

		[DllImport(user32, CharSet = CharSet.Unicode, SetLastError = true)]
		private static extern uint SendInput(uint nInputs, IntPtr pInputs, int cbSize);

		private static int GetInputType(object input)
		{
			if (input is MOUSEINPUT)
				return INPUT_MOUSE;
			else if (input is KEYBDINPUT)
				return INPUT_KEYBOARD;
			else if (input is HARDWAREINPUT)
				return INPUT_HARDWARE;
			else
				throw new ArgumentException(nameof(input));
		} // func GetInputType

		public static void SendInput(object[] inputs, int count)
		{
			var is32Bit = IntPtr.Size == 4;
			var structSize = is32Bit ? Marshal.SizeOf<INPUT_32>() : Marshal.SizeOf<INPUT_64>();

			var hData = Marshal.AllocHGlobal(structSize * count);
			try
			{
				var hCur = hData;

				for (var i = 0; i < count; i++)
				{
					Marshal.WriteInt32(hCur, GetInputType(inputs[i]));
					Marshal.StructureToPtr(inputs[i], hCur + IntPtr.Size, false);

					hCur += structSize;
				}

				if (SendInput((uint)count, hData, structSize) == 0)
					throw new Win32Exception();
			}
			finally
			{
				Marshal.FreeHGlobal(hData);
			}
		} // proc SendInput

		#endregion

		[DllImport(user32, CharSet = CharSet.Unicode, SetLastError = true)]
		public static extern ushort VkKeyScan(char ch);

		[DllImport(user32, SetLastError = true)]
		public static extern ushort GetKeyState(int nVirtKey);

		[DllImport("shlwapi.dll", BestFitMapping = false, CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = false, ThrowOnUnmappableChar = true)]
		public static extern int SHLoadIndirectString(string pszSource, StringBuilder pszOutBuf, int cchOutBuf, IntPtr ppvReserved);

		[DllImport(user32)]
		public static extern IntPtr GetKeyboardLayout(uint idThread);
		[DllImport(user32, CharSet = CharSet.Auto)]
		public static extern IntPtr LoadKeyboardLayout(string pwszKLID, uint Flags);
		[DllImport(user32)]
		public static extern bool UnloadKeyboardLayout(IntPtr hkl);
	} // class UnsafeNativeMethods

	#endregion
}
