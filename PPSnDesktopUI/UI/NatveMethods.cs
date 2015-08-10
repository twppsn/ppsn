	using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace TecWare.PPSn.UI
{
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
		WM_NCHITTEST = 0x0084
	}

	internal enum HitTestValues : int
	{
		HTERROR = -2,
		HTTRANSPARENT = -1,
		HTNOWHERE = 0,
		HTCLIENT = 1,
		HTCAPTION = 2,
		HTSYSMENU = 3,
		HTGROWBOX = 4,
		HTMENU = 5,
		HTHSCROLL = 6,
		HTVSCROLL = 7,
		HTMINBUTTON = 8,
		HTMAXBUTTON = 9,
		HTLEFT = 10,
		HTRIGHT = 11,
		HTTOP = 12,
		HTTOPLEFT = 13,
		HTTOPRIGHT = 14,
		HTBOTTOM = 15,
		HTBOTTOMLEFT = 16,
		HTBOTTOMRIGHT = 17,
		HTBORDER = 18,
		HTOBJECT = 19,
		HTCLOSE = 20,
		HTHELP = 21
	} // enum HitTestValues

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
			this.Left = left;
			this.Top = top;
			this.Right = right;
			this.Bottom = bottom;
		}
		public RECT(Rect rect)
		{
			this.Left = (int)rect.Left;
			this.Top = (int)rect.Top;
			this.Right = (int)rect.Right;
			this.Bottom = (int)rect.Bottom;
		}

		public void Offset(int dx, int dy)
		{
			this.Left += dx;
			this.Right += dx;
			this.Top += dy;
			this.Bottom += dy;
		}

		public Point Position { get { return new Point((double)this.Left, (double)this.Top); } }
		public Size Size { get { return new Size((double)this.Width, (double)this.Height); } }
		public int Height { get { return this.Bottom - this.Top; } set { this.Bottom = this.Top + value; } }
		public int Width { get { return this.Right - this.Left; } set { this.Right = this.Left + value; } }

		public Int32Rect ToInt32Rect()
		{
			return new Int32Rect(this.Left, this.Top, this.Width, this.Height);
		}
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
		public uint flags;
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

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	internal static class NativeMethods
	{
		#region -- Win32Constants -----------------------------------------------------------

		public const int GWL_WNDPROC = -4;

		public const int WS_POPUP = unchecked((int)0x80000000);
		public const int WS_CLIPSIBLINGS = 0x04000000;
		public const int WS_CLIPCHILDREN = 0x02000000;

		public const int WS_EX_TOOLWINDOW = 0x00000080;
		public const int WS_EX_LAYERED = 0x00080000;

		public const uint SWP_NOSIZE = 0x0001;
		public const uint SWP_NOMOVE = 0x0002;
		public const uint SWP_NOZORDER = 0x0004;
		public const uint SWP_NOACTIVATE = 0x0010;
		public const uint SWP_SHOWWINDOW = 0x0040;
		public const int SWP_HIDEWINDOW = 0x0080;
		public const uint SWP_NOOWNERZORDER = 0x0200;

		public const int SW_SHOWMAXIMIZED = 3;
		public const int WA_CLICKACTIVE = 2;
		public const int GW_HWNDPREV = 3;
		public const uint ULW_ALPHA = 2;
		public const uint DIB_RGB_COLORS = 0;
		public const int BI_RGB = 0;

		#endregion

		public delegate IntPtr WndProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

		[DllImport("user32.dll", CharSet = CharSet.Unicode)]
		internal static extern IntPtr DefWindowProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

		[DllImport("user32.dll", CharSet = CharSet.Unicode)]
		public static extern ushort RegisterClass(ref WNDCLASS lpWndClass);

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool UnregisterClass(IntPtr classAtom, IntPtr hInstance);

		[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
		public static extern IntPtr GetModuleHandle(string moduleName);

		[DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
		public static extern IntPtr CreateWindowEx(int dwExStyle, IntPtr classAtom, string lpWindowName, int dwStyle, int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

		[DllImport("user32.dll", SetLastError = true)]
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

		[DllImport("user32.dll", CharSet = CharSet.Auto, EntryPoint = "SetWindowLong")]
		private static extern IntPtr SetWindowLongPtr32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

		[DllImport("user32.dll", CharSet = CharSet.Auto, EntryPoint = "SetWindowLongPtr")]
		private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

		public static WINDOWPLACEMENT GetWindowPlacement(IntPtr hwnd)
		{
			WINDOWPLACEMENT windowplacement = new WINDOWPLACEMENT();
			GetWindowPlacement(hwnd, windowplacement);
			return windowplacement;
		}

		[DllImport("user32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool GetWindowPlacement(IntPtr hwnd, WINDOWPLACEMENT lpwndpl);

		[DllImport("user32.dll")]
		public static extern IntPtr GetWindow(IntPtr hwnd, int nCmd);

		[DllImport("User32", CharSet = CharSet.Auto, ExactSpelling = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

		[DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
		public static extern IntPtr SendMessage(IntPtr hWnd, int nMsg, IntPtr wParam, IntPtr lParam);

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool GetWindowInfo(IntPtr hwnd, WINDOWINFO pwi);

		[DllImport("user32.dll", SetLastError = true)]
		public static extern IntPtr MonitorFromRect([In] ref RECT lprc, MonitorOptions dwFlags);
		[DllImport("user32.dll", SetLastError = true)]
		public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

		[DllImport("gdi32.dll", SetLastError = true)]
		internal static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFO pbmi, uint iUsage, out IntPtr ppvBits, IntPtr hSection, uint dwOffset);

		[DllImport("user32.dll", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
		internal static extern IntPtr GetDC(IntPtr hWnd);

		[DllImport("gdi32.dll", SetLastError = true)]
		internal static extern IntPtr CreateCompatibleDC(IntPtr hdc);

		[DllImport("User32.dll", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
		internal static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

		[DllImport("gdi32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern bool DeleteDC(IntPtr hdc);

		[DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
		internal static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

		[DllImport("gdi32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool DeleteObject(IntPtr hObject);

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDest, ref POINT pptDest, ref SIZE psize, IntPtr hdcSrc, ref POINT pptSrc, uint crKey, [In] ref BLENDFUNCTION pblend, uint dwFlags);

		[DllImport("msimg32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern bool AlphaBlend(IntPtr hdcDest, int xoriginDest, int yoriginDest, int wDest, int hDest, IntPtr hdcSrc, int xoriginSrc, int yoriginSrc, int wSrc, int hSrc, BLENDFUNCTION pfn);
	} // class NativeMethods
}