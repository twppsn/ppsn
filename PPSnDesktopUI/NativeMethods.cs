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
using System.Windows;

namespace TecWare.PPSn
{
	#region -- enum WinMsg ------------------------------------------------------------

	internal enum WinMsg : int
	{
		WM_ACTIVATE = 0x0006,
		WM_WINDOWPOSCHANGING = 0x0046,
		WM_WINDOWPOSCHANGED = 0x0047,
		WM_NCCALCSIZE = 0x0083,
		WM_NCLBUTTONDOWN = 0x00A1,
		WM_NCLBUTTONDBLCLK = 0x00A3,
		WM_NCRBUTTONDOWN = 0x00A4,
		WM_NCRBUTTONDBLCLK = 0x00A6,
		WM_NCMBUTTONDOWN = 0x00A7,
		WM_NCMBUTTONDBLCLK = 0x00A9,
		WM_NCXBUTTONDOWN = 0x00AB,
		WM_NCXBUTTONDBLCLK = 0x00AD,
		WM_NCRBUTTONUP = 0x00A5,
		WM_RBUTTONDOWN = 0x0204,
		WM_RBUTTONUP = 0x0205,
		WM_RBUTTONDBLCLK = 0x0206,
		WM_NCHITTEST = 0x0084,
		WM_INPUT = 0x00FF,
		WM_DEVICECHANGE = 0x0219,
		WM_KEYDOWN = 0x0100,
		WM_KEYUP = 0x0101,
		WM_CHAR = 0x0102,
		WM_DEADCHAR = 0x0103,
		WM_SYSKEYDOWN = 0x0104,
		WM_SYSKEYUP = 0x0105,
		WM_KEYFIRST = 0x0100,
		WM_KEYLAST = 0x0108,
	} // enum WinMsg

	#endregion

	#region -- enum KeyCode -----------------------------------------------------------

	internal enum KeyCode
	{
		None = 0x0,

		LButton = 0x1,
		RButton = 0x2,
		Cancel = 0x3,
		MButton = 0x4,
		XButton1 = 0x5,
		XButton2 = 0x6,
		Back = 0x8,
		Tab = 0x9,
		LineFeed = 0xA,
		Clear = 0xC,
		Return = 0xD,
		Enter = 0xD,
		ShiftKey = 0x10,
		ControlKey = 0x11,
		Menu = 0x12,
		Pause = 0x13,
		Capital = 0x14,
		CapsLock = 0x14,
		KanaMode = 0x15,
		HanguelMode = 0x15,
		HangulMode = 0x15,
		JunjaMode = 0x17,
		FinalMode = 0x18,
		HanjaMode = 0x19,
		KanjiMode = 0x19,
		Escape = 0x1B,
		IMEConvert = 0x1C,
		IMENonconvert = 0x1D,
		IMEAccept = 0x1E,
		IMEAceept = 0x1E,
		IMEModeChange = 0x1F,
		Space = 0x20,
		Prior = 0x21,
		PageUp = 0x21,
		Next = 0x22,
		PageDown = 0x22,
		End = 0x23,
		Home = 0x24,
		Left = 0x25,
		Up = 0x26,
		Right = 0x27,
		Down = 0x28,
		Select = 0x29,
		Print = 0x2A,
		Execute = 0x2B,
		Snapshot = 0x2C,
		PrintScreen = 0x2C,
		Insert = 0x2D,
		Delete = 0x2E,
		Help = 0x2F,

		D0 = 0x30,
		D1 = 0x31,
		D2 = 0x32,
		D3 = 0x33,
		D4 = 0x34,
		D5 = 0x35,
		D6 = 0x36,
		D7 = 0x37,
		D8 = 0x38,
		D9 = 0x39,

		A = 0x41,
		B = 0x42,
		C = 0x43,
		D = 0x44,
		E = 0x45,
		F = 0x46,
		G = 0x47,
		H = 0x48,
		I = 0x49,
		J = 0x4A,
		K = 0x4B,
		L = 0x4C,
		M = 0x4D,
		N = 0x4E,
		O = 0x4F,
		P = 0x50,
		Q = 0x51,
		R = 0x52,
		S = 0x53,
		T = 0x54,
		U = 0x55,
		V = 0x56,
		W = 0x57,
		X = 0x58,
		Y = 0x59,
		Z = 0x5A,

		LWin = 0x5B,
		RWin = 0x5C,
		Apps = 0x5D,
		Sleep = 0x5F,

		NumPad0 = 0x60,
		NumPad1 = 0x61,
		NumPad2 = 0x62,
		NumPad3 = 0x63,
		NumPad4 = 0x64,
		NumPad5 = 0x65,
		NumPad6 = 0x66,
		NumPad7 = 0x67,
		NumPad8 = 0x68,
		NumPad9 = 0x69,

		Multiply = 0x6A,
		Add = 0x6B,
		Separator = 0x6C,
		Subtract = 0x6D,
		Decimal = 0x6E,
		Divide = 0x6F,

		F1 = 0x70,
		F2 = 0x71,
		F3 = 0x72,
		F4 = 0x73,
		F5 = 0x74,
		F6 = 0x75,
		F7 = 0x76,
		F8 = 0x77,
		F9 = 0x78,
		F10 = 0x79,
		F11 = 0x7A,
		F12 = 0x7B,
		F13 = 0x7C,
		F14 = 0x7D,
		F15 = 0x7E,
		F16 = 0x7F,
		F17 = 0x80,
		F18 = 0x81,
		F19 = 0x82,
		F20 = 0x83,
		F21 = 0x84,
		F22 = 0x85,
		F23 = 0x86,
		F24 = 0x87,

		NumLock = 0x90,
		Scroll = 0x91,
		LShiftKey = 0xA0,
		RShiftKey = 0xA1,
		LControlKey = 0xA2,
		RControlKey = 0xA3,
		LMenu = 0xA4,
		RMenu = 0xA5,

		BrowserBack = 0xA6,
		BrowserForward = 0xA7,
		BrowserRefresh = 0xA8,
		BrowserStop = 0xA9,
		BrowserSearch = 0xAA,
		BrowserFavorites = 0xAB,
		BrowserHome = 0xAC,

		VolumeMute = 0xAD,
		VolumeDown = 0xAE,
		VolumeUp = 0xAF,

		MediaNextTrack = 0xB0,
		MediaPreviousTrack = 0xB1,
		MediaStop = 0xB2,
		MediaPlayPause = 0xB3,

		LaunchMail = 0xB4,
		SelectMedia = 0xB5,
		LaunchApplication1 = 0xB6,
		LaunchApplication2 = 0xB7,

		OemSemicolon = 0xBA,
		Oem1 = 0xBA,
		Oemplus = 0xBB,
		Oemcomma = 0xBC,
		OemMinus = 0xBD,
		OemPeriod = 0xBE,
		OemQuestion = 0xBF,
		Oem2 = 0xBF,
		Oemtilde = 0xC0,
		Oem3 = 0xC0,
		OemOpenBrackets = 0xDB,
		Oem4 = 0xDB,
		OemPipe = 0xDC,
		Oem5 = 0xDC,
		OemCloseBrackets = 0xDD,
		Oem6 = 0xDD,
		OemQuotes = 0xDE,
		Oem7 = 0xDE,
		Oem8 = 0xDF,
		OemBackslash = 0xE2,
		Oem102 = 0xE2,

		ProcessKey = 0xE5,
		Packet = 0xE7,
		Attn = 0xF6,
		Crsel = 0xF7,
		Exsel = 0xF8,
		EraseEof = 0xF9,
		Play = 0xFA,
		Zoom = 0xFB,
		NoName = 0xFC,
		Pa1 = 0xFD,
		OemClear = 0xFE,
		Shift = 0x10000,
		Control = 0x20000,
		Alt = 0x40000
	} // enum KeyCode

	#endregion

	#region -- enum HitTestValues -----------------------------------------------------

	internal enum HitTestValues : int
	{
		Error = -2,
		Transparent = -1,
		NoWhere = 0,
		Client = 1,
		Caption = 2,
		SysMenu = 3,
		GrowBox = 4,
		Menu = 5,
		HScroll = 6,
		VScroll = 7,
		MinButton = 8,
		MaxButton = 9,
		Left = 10,
		Right = 11,
		Top = 12,
		TopLeft = 13,
		TopRight = 14,
		Bottom = 15,
		BottomLeft = 16,
		BottomRight = 17,
		Border = 18,
		Object = 19,
		Close = 20,
		Help = 21
	} // enum HitTestValues

	#endregion

	#region -- enum SetWindowPosFlag --------------------------------------------------

	[Flags]
	internal enum SetWindowPosFlag : uint
	{
		NoSize = 0x0001,
		NoMove = 0x0002,
		NoZOrder = 0x0004,
		NoRedraw = 0x0008,
		NoActivate = 0x0010,
		FrameChanged = 0x0020,
		DrawFrame = 0x0020,
		ShowWindow = 0x0040,
		HideWindow = 0x0080,
		NoCopyBits = 0x0100,
		NoOwnerZOrder = 0x0200,
		NoReposition = 0x0200,
		ASyncWindowPos = 0x0200,
	} // enum SetWindowPosFlag

	#endregion

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	internal struct WNDCLASS
	{
		public uint style;
		public Delegate lpfnWndProc;
		public int cbClsExtra;
		public int cbWndExtra;
		public IntPtr hInstance;
		public IntPtr hIcon;
		public IntPtr hCursor;
		public IntPtr hbrBackground;
		[MarshalAs(UnmanagedType.LPWStr)]
		public string lpszMenuName;
		[MarshalAs(UnmanagedType.LPWStr)]
		public string lpszClassName;
	}

	internal enum MonitorOptions : uint
	{
		MONITOR_DEFAULTTONULL = 0x00000000,
		MONITOR_DEFAULTTOPRIMARY = 0x00000001,
		MONITOR_DEFAULTTONEAREST = 0x00000002
	}

	[StructLayout(LayoutKind.Sequential)]
	internal struct MONITORINFO
	{
		public int cbSize;
		public RECT rcMonitor;
		public RECT rcWork;
		public uint dwFlags;
	}

	internal struct POINT
	{
#pragma warning disable 0649
		public int x;
		public int y;
#pragma warning restore 0649

		public POINT(int _x, int _y)
		{
			x = _x;
			y = _y;
		}
	}

	[StructLayout(LayoutKind.Sequential)]
	internal struct SIZE
	{
		public int cx;
		public int cy;
	}

	[Serializable]
	internal struct RECT
	{
		public int Left;
		public int Top;
		public int Right;
		public int Bottom;

		public RECT(int left, int top, int right, int bottom)
		{
			Left = left;
			Top = top;
			Right = right;
			Bottom = bottom;
		}
		public RECT(Rect rect)
		{
			Left = (int)rect.Left;
			Top = (int)rect.Top;
			Right = (int)rect.Right;
			Bottom = (int)rect.Bottom;
		}

		public void Offset(int dx, int dy)
		{
			Left += dx;
			Right += dx;
			Top += dy;
			Bottom += dy;
		}

		public Point Position => new Point(Left, Top);
		public Size Size => new Size(Width, Height);
		public int Height { get => Bottom - Top; set => Bottom = Top + value; }
		public int Width { get => Right - Left; set => Right = Left + value; }

		public Int32Rect ToInt32Rect() 
			=> new Int32Rect(Left, Top, Width, Height);
	} // struct RECT

	[StructLayout(LayoutKind.Sequential)]
	internal class WINDOWPOS
	{
		public IntPtr hwnd;
		public IntPtr hwndInsertAfter;
		public int x;
		public int y;
		public int cx;
		public int cy;
		public SetWindowPosFlag flags;
	}

	[StructLayout(LayoutKind.Sequential)]
	internal class WINDOWPLACEMENT
	{
		public int length = Marshal.SizeOf(typeof(WINDOWPLACEMENT));
		public int flags;
		public int showCmd;
		public POINT ptMinPosition;
		public POINT ptMaxPosition;
		public RECT rcNormalPosition;
	}

	[StructLayout(LayoutKind.Sequential)]
	internal class WINDOWINFO
	{
		public int cbSize = Marshal.SizeOf(typeof(WINDOWINFO));
		public RECT rcWindow;
		public RECT rcClient;
		public int dwStyle;
		public int dwExStyle;
		public uint dwWindowStatus;
		public uint cxWindowBorders;
		public uint cyWindowBorders;
		public ushort atomWindowType;
		public ushort wCreatorVersion;
	}

	#region -- struct NativeMessage ---------------------------------------------------

	[StructLayout(LayoutKind.Sequential)]
	internal struct NativeMessage
	{
		public IntPtr handle;
		public uint msg;
		public IntPtr wParam;
		public IntPtr lParam;
		public uint time;
		public POINT p;
	} // struct NativeMessage

	#endregion

	internal struct BITMAPINFO
	{
		internal int biSize;
		internal int biWidth;
		internal int biHeight;
		internal short biPlanes;
		internal short biBitCount;
		internal int biCompression;
		internal int biSizeImage;
		internal int biXPelsPerMeter;
		internal int biYPelsPerMeter;
		internal int biClrUsed;
		internal int biClrImportant;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 1024)]
		internal byte[] bmiColors;
		internal static BITMAPINFO Default
		{
			get
			{
				return new BITMAPINFO
				{
					biSize = 40,
					biPlanes = 1
				};
			}
		}
	} // struct BITMAPINFO

	internal struct BITMAPINFOHEADER
	{
		internal uint biSize;
		internal int biWidth;
		internal int biHeight;
		internal ushort biPlanes;
		internal ushort biBitCount;
		internal uint biCompression;
		internal uint biSizeImage;
		internal int biXPelsPerMeter;
		internal int biYPelsPerMeter;
		internal uint biClrUsed;
		internal uint biClrImportant;
		internal static BITMAPINFOHEADER Default
		{
			get
			{
				return new BITMAPINFOHEADER
				{
					biSize = 40u,
					biWidth = 0,
					biHeight = 0,
					biPlanes = 1,
					biBitCount = 32,
					biCompression = 0,
					biSizeImage = 0,
					biXPelsPerMeter = 0,
					biYPelsPerMeter = 0,
					biClrUsed = 0,
					biClrImportant = 0
				};
			}
		}
	} // struct BITMAPINFOHEADER

	internal struct BLENDFUNCTION
	{
		public byte BlendOp;
		public byte BlendFlags;
		public byte SourceConstantAlpha;
		public byte AlphaFormat;
	}

	#region -- class NativeMethods ----------------------------------------------------

	[SuppressUnmanagedCodeSecurity]
	internal static partial class NativeMethods
	{
		private const string user32 = "user32.dll";
		private const string kernel32 = "kernel32.dll";
		private const string gdi32 = "gdi32.dll";

		#region -- Win32Constants -----------------------------------------------------

		public const int GWL_WNDPROC = -4;

		public const int WS_POPUP = unchecked((int)0x80000000);
		public const int WS_CLIPSIBLINGS = 0x04000000;
		public const int WS_CLIPCHILDREN = 0x02000000;

		public const int WS_EX_TOOLWINDOW = 0x00000080;
		public const int WS_EX_LAYERED = 0x00080000;

		public const int SW_SHOWMAXIMIZED = 3;
		public const int WA_CLICKACTIVE = 2;
		public const int GW_HWNDPREV = 3;
		public const uint ULW_ALPHA = 2;
		public const uint DIB_RGB_COLORS = 0;
		public const int BI_RGB = 0;

		public const int DBT_DEVNODES_CHANGED = 0x0007;
		public const int DBT_DEVICEARRIVAL = 0x8000;
		public const int DBT_DEVICEREMOVECOMPLETE = 0x8004;

		#endregion

		public delegate IntPtr WndProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

		[DllImport(user32, CharSet = CharSet.Unicode)]
		internal static extern IntPtr DefWindowProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

		[DllImport(user32, CharSet = CharSet.Unicode)]
		public static extern ushort RegisterClass(ref WNDCLASS lpWndClass);

		[DllImport(user32)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool UnregisterClass(IntPtr classAtom, IntPtr hInstance);

		[DllImport(kernel32, CharSet = CharSet.Unicode)]
		public static extern IntPtr GetModuleHandle(string moduleName);

		[DllImport(user32, CharSet = CharSet.Unicode, SetLastError = true)]
		public static extern IntPtr CreateWindowEx(int dwExStyle, IntPtr classAtom, string lpWindowName, int dwStyle, int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

		[DllImport(user32, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool DestroyWindow(IntPtr hwnd);

		public static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
		{
			if (IntPtr.Size == 4)
			{
				return SetWindowLongPtr32(hWnd, nIndex, dwNewLong);
			}
			return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
		}

		[DllImport(user32, CharSet = CharSet.Auto, EntryPoint = "SetWindowLong")]
		private static extern IntPtr SetWindowLongPtr32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

		[DllImport(user32, CharSet = CharSet.Auto, EntryPoint = "SetWindowLongPtr")]
		private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

		public static WINDOWPLACEMENT GetWindowPlacement(IntPtr hwnd)
		{
			var windowplacement = new WINDOWPLACEMENT();
			GetWindowPlacement(hwnd, windowplacement);
			return windowplacement;
		}

		[DllImport(user32, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool GetWindowPlacement(IntPtr hwnd, WINDOWPLACEMENT lpwndpl);

		[DllImport(user32)]
		public static extern IntPtr GetWindow(IntPtr hwnd, int nCmd);
		[DllImport(user32, ExactSpelling = true, CharSet = CharSet.Auto)]
		public static extern IntPtr GetActiveWindow();

		[DllImport(user32, CharSet = CharSet.Auto, ExactSpelling = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, SetWindowPosFlag flags);

		[DllImport(user32)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);
		
		[DllImport(user32, CharSet = CharSet.Unicode, SetLastError = true)]
		public static extern IntPtr SendMessage(IntPtr hWnd, int nMsg, IntPtr wParam, IntPtr lParam);
		[DllImport(user32)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool PeekMessage(out NativeMessage lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

		[DllImport(user32)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool GetWindowInfo(IntPtr hwnd, WINDOWINFO pwi);

		[DllImport(user32, SetLastError = true)]
		public static extern IntPtr MonitorFromRect([In] ref RECT lprc, MonitorOptions dwFlags);
		[DllImport(user32, SetLastError = true)]
		public static extern IntPtr MonitorFromPoint([In] POINT lprc, MonitorOptions dwFlags);
		[DllImport(user32, SetLastError = true)]
		public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

		[DllImport(user32)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool GetCursorPos(ref POINT pt);

		[DllImport(gdi32, SetLastError = true)]
		internal static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFO pbmi, uint iUsage, out IntPtr ppvBits, IntPtr hSection, uint dwOffset);

		[DllImport(user32, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
		internal static extern IntPtr GetDC(IntPtr hWnd);

		[DllImport(gdi32, SetLastError = true)]
		internal static extern IntPtr CreateCompatibleDC(IntPtr hdc);

		[DllImport(user32, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
		internal static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

		[DllImport(gdi32)]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern bool DeleteDC(IntPtr hdc);

		[DllImport(gdi32, ExactSpelling = true, SetLastError = true)]
		internal static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

		[DllImport(gdi32)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool DeleteObject(IntPtr hObject);

		[DllImport(user32)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDest, ref POINT pptDest, ref SIZE psize, IntPtr hdcSrc, ref POINT pptSrc, uint crKey, [In] ref BLENDFUNCTION pblend, uint dwFlags);

		[DllImport("msimg32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool AlphaBlend(IntPtr hdcDest, int xoriginDest, int yoriginDest, int wDest, int hDest, IntPtr hdcSrc, int xoriginSrc, int yoriginSrc, int wSrc, int hSrc, BLENDFUNCTION pfn);

		[DllImport(kernel32)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool GlobalUnlock(IntPtr hMem);
		[DllImport(kernel32)]
		public static extern IntPtr GlobalLock(IntPtr hMem);
		[DllImport(kernel32)]
		public static extern IntPtr GlobalSize(IntPtr hMem);

		[DllImport(kernel32, SetLastError = true, CallingConvention = CallingConvention.Winapi)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool IsWow64Process([In] IntPtr hProcess, [Out] out bool lpSystemInfo);

		[DllImport("winspool.drv", CharSet = CharSet.Auto, SetLastError = true)]
		public static extern int DocumentProperties(IntPtr hwnd, IntPtr hPrinter, string pDeviceName, IntPtr pDevModeOutput, IntPtr pDevModeInput, int mode);

		[DllImport("winspool.drv", CharSet = CharSet.Auto, SetLastError = true)]
		public static extern int AdvancedDocumentProperties(IntPtr hwnd, IntPtr hPrinter, string pDeviceName, IntPtr pDevModeOutput, IntPtr pDevModeInput);
	} // class NativeMethods

	#endregion
}