using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace TecWare.PPSn.UI
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public partial class PpsWindow
	{
		#region -- class GlowWindow ---------------------------------------------------

		private sealed class GlowWindow
		{
			[Flags]
			private enum PropertyType
			{
				None = 0,
				Location = 1,
				Size = 2,
				Visibility = 4
			} // enum PropertyType

			private const int dimension = 9;
			private static ushort sharedClassAtom = 0;
			private static int sharedClassCount = 0;
			private readonly PpsWindow ownerWindow;
			private readonly Dock orientation;
			private IntPtr handle;
			private Delegate wndProc;
			private PropertyType changedPropertys;
			private int left;
			private int top;
			private int width;
			private int height;
			private bool isVisible;

			#region -- ctor / dtor ----------------------------------------------------

			public GlowWindow(PpsWindow ownerWindow, Dock orientation)
			{
				this.ownerWindow = ownerWindow;
				this.orientation = orientation;
				CreateNativeWindow();
			} // ctor

			public void DestroyNativeResources()
			{
				DestroyNativeWindow();
			} // dtor

			private void CreateNativeWindow()
			{
				CreateWindowClass();
				CreateWindowHandle();
			}

			private void DestroyNativeWindow()
			{
				DestroyWindowHandle();
				DestroyWindowClass();
			}

			#endregion

			#region -- NativeWindowClassAtom ------------------------------------------

			private void CreateWindowClass()
			{
				sharedClassCount++;
				if (sharedClassAtom != 0)
					return;

				WNDCLASS wndclass = default(WNDCLASS);
				wndclass.cbClsExtra = 0;
				wndclass.cbWndExtra = 0;
				wndclass.hbrBackground = IntPtr.Add(IntPtr.Zero, 17); //  IntPtr.Zero;
				wndclass.hCursor = IntPtr.Zero;
				wndclass.hIcon = IntPtr.Zero;
				wndclass.lpfnWndProc = new NativeMethods.WndProc(NativeMethods.DefWindowProc);
				wndclass.lpszClassName = "PpsWindowGlowWnd";
				wndclass.lpszMenuName = null;
				wndclass.style = 0u;
				sharedClassAtom = NativeMethods.RegisterClass(ref wndclass);
			} // proc CreateWindowClass

			private void DestroyWindowClass()
			{
				sharedClassCount--;
				if (sharedClassCount > 0 || sharedClassAtom == 0)
					return;

				IntPtr moduleHandle = NativeMethods.GetModuleHandle(null);
				NativeMethods.UnregisterClass(new IntPtr((int)sharedClassAtom), moduleHandle);
				sharedClassAtom = 0;
			} // proc DestroyWindowClass

			#endregion

			#region -- NativeWindow ---------------------------------------------------

			private void CreateWindowHandle()
			{
				int dwExStyle = NativeMethods.WS_EX_TOOLWINDOW;// | NativeMethods.WS_EX_LAYERED;
				int dwStyle = NativeMethods.WS_POPUP | NativeMethods.WS_CLIPCHILDREN | NativeMethods.WS_CLIPSIBLINGS;
				handle = NativeMethods.CreateWindowEx(dwExStyle, new IntPtr((int)sharedClassAtom), string.Empty, dwStyle, 0, 0, 0, 0, new WindowInteropHelper(ownerWindow).Owner, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
				wndProc = new NativeMethods.WndProc(WndProc);
				NativeMethods.SetWindowLongPtr(handle, NativeMethods.GWL_WNDPROC, Marshal.GetFunctionPointerForDelegate(wndProc));
			}

			private void DestroyWindowHandle()
			{
				if (handle != IntPtr.Zero)
				{
					bool b = NativeMethods.DestroyWindow(handle);
					handle = IntPtr.Zero;
				}
			}

			private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam)
			{
				switch ((WinMsg)msg)
				{
					case WinMsg.WM_ACTIVATE:
						return IntPtr.Zero;
					case WinMsg.WM_WINDOWPOSCHANGING:
                        WINDOWPOS windowpos = (WINDOWPOS)Marshal.PtrToStructure(lParam, typeof(WINDOWPOS));
						windowpos.flags |= NativeMethods.SWP_NOACTIVATE;
						Marshal.StructureToPtr(windowpos, lParam, true);
						break;
					case WinMsg.WM_NCHITTEST:
						return new IntPtr(this.WmNcHitTest(lParam));
					case WinMsg.WM_NCLBUTTONDOWN:
					case WinMsg.WM_NCLBUTTONDBLCLK:
					case WinMsg.WM_NCRBUTTONDOWN:
					case WinMsg.WM_NCRBUTTONDBLCLK:
					case WinMsg.WM_NCMBUTTONDOWN:
					case WinMsg.WM_NCMBUTTONDBLCLK:
					case WinMsg.WM_NCXBUTTONDOWN:
					case WinMsg.WM_NCXBUTTONDBLCLK:
						{
							IntPtr ownerWindowHandle = new WindowInteropHelper(ownerWindow).Handle;
							NativeMethods.SendMessage(ownerWindowHandle, (int)WinMsg.WM_ACTIVATE, new IntPtr(NativeMethods.WA_CLICKACTIVE), IntPtr.Zero);
							NativeMethods.SendMessage(ownerWindowHandle, msg, wParam, IntPtr.Zero);
							return IntPtr.Zero;
						}
				}
				return NativeMethods.DefWindowProc(hwnd, msg, wParam, lParam);
			}

			private int WmNcHitTest(IntPtr lParam)
			{
				int xLParam = NativeMethods.GetXLParam(lParam.ToInt32());
				int yLParam = NativeMethods.GetYLParam(lParam.ToInt32());
				RECT rect;
				NativeMethods.GetWindowRect(handle, out rect);
				int offset = 2 * dimension;
				switch (orientation)
				{
					case Dock.Left:
						if (yLParam - offset < rect.Top)
							return (int)HitTestValues.HTTOPLEFT;
						if (yLParam + offset > rect.Bottom)
							return (int)HitTestValues.HTBOTTOMLEFT;
						return (int)HitTestValues.HTLEFT;
					case Dock.Top:
						if (xLParam - offset < rect.Left)
							return (int)HitTestValues.HTTOPLEFT;
						if (xLParam + offset > rect.Right)
							return (int)HitTestValues.HTTOPRIGHT;
						return (int)HitTestValues.HTTOP;
					case Dock.Right:
						if (yLParam - offset < rect.Top)
							return (int)HitTestValues.HTTOPRIGHT;
						if (yLParam + offset > rect.Bottom)
							return (int)HitTestValues.HTBOTTOMRIGHT;
						return (int)HitTestValues.HTRIGHT;
					default:
						if (xLParam - offset < rect.Left)
							return (int)HitTestValues.HTBOTTOMLEFT;
						if (xLParam + offset > rect.Right)
							return (int)HitTestValues.HTBOTTOMRIGHT;
						return (int)HitTestValues.HTBOTTOM;
				}
			}

			#endregion

			#region -- Updating -------------------------------------------------------

			public void CommitChanges()
			{
				UpdateWindowPos();
				changedPropertys = PropertyType.None;
			}

			public void UpdateBounds()
			{
				IntPtr ownerWindowHandle = new WindowInteropHelper(ownerWindow).Handle;

				RECT rect;
				NativeMethods.GetWindowRect(ownerWindowHandle, out rect);
				if (IsVisible)
				{
					switch (orientation)
					{
						case Dock.Left:
							Left = rect.Left - dimension;
							Top = rect.Top - dimension;
							Width = dimension;
							Height = rect.Height + 2 * dimension;
							return;
						case Dock.Top:
							Left = rect.Left - dimension;
							Top = rect.Top - dimension;
							Width = rect.Width + 2 * dimension;
							Height = dimension;
							return;
						case Dock.Right:
							Left = rect.Right;
							Top = rect.Top - dimension;
							Width = dimension;
							Height = rect.Height + 2 * dimension;
							return;
						default:
							Left = rect.Left - dimension;
							Top = rect.Bottom;
							Width = rect.Width + 2 * dimension;
							Height = dimension;
							break;
					}
				}
			}

			private void UpdateWindowPos()
			{
				if (changedPropertys.HasFlag(PropertyType.Location) || changedPropertys.HasFlag(PropertyType.Size) || changedPropertys.HasFlag(PropertyType.Visibility))
				{
					uint flags = NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_NOOWNERZORDER;
					if (changedPropertys.HasFlag(PropertyType.Visibility))
					{
						if (IsVisible)
							flags |= NativeMethods.SWP_SHOWWINDOW;
						else
							flags |= (NativeMethods.SWP_HIDEWINDOW | NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE);
					}
					if (!changedPropertys.HasFlag(PropertyType.Location))
					{
						flags |= 2;
					}
					if (!changedPropertys.HasFlag(PropertyType.Size))
					{
						flags |= 1;
					}
					NativeMethods.SetWindowPos(handle, IntPtr.Zero, Left, Top, Width, Height, flags);
				}
			}

			#endregion

			#region -- Properties -----------------------------------------------------

			public IntPtr Handle { get { return handle; } }

			public bool IsVisible { get { return isVisible; } set { UpdateProperty(ref isVisible, value, PropertyType.Visibility); } }

			private int Left { get { return left; } set { UpdateProperty(ref left, value, PropertyType.Location); } }

			private int Top { get { return top; } set { UpdateProperty(ref top, value, PropertyType.Location); } }

			private int Width { get { return width; } set { UpdateProperty(ref width, value, PropertyType.Size); } }

			private int Height { get { return height; } set { UpdateProperty(ref height, value, PropertyType.Size); } }

			private void UpdateProperty<T>(ref T field, T value, PropertyType propertytype) where T : struct
			{
				if (!Object.Equals(field, value))
				{
					field = value;
					changedPropertys |= propertytype;
					if (ownerWindow.glowPendingUpdates == 0)
						CommitChanges();
				}
			}

			#endregion
		} // class GlowWindow

		#endregion

		#region -- class GlowWindowChanging -------------------------------------------

		private class GlowWindowChanging : IDisposable
		{
			private readonly PpsWindow ownerWindow;
			private bool disposed = false;

			public GlowWindowChanging(PpsWindow owner)
			{
				ownerWindow = owner;
				ownerWindow.glowPendingUpdates++;
			}

			public void Dispose()
			{
				Dispose(true);
				GC.SuppressFinalize(this);
			}

			protected virtual void Dispose(bool disposing)
			{
				// Check to see if Dispose has already been called.
				if (!this.disposed)
				{
					if (disposing)
					{
						ownerWindow.glowPendingUpdates--;
						if (ownerWindow.glowPendingUpdates == 0)
						{
							ownerWindow.CommitGlowChanges();
						}
					}
					disposed = true;
				}
			}
		} // glass GlowWindowChanging

		#endregion

		private HwndSource hwndSource;
		private HwndSourceHook wndProc;
		private readonly GlowWindow[] glowWindows = new GlowWindow[4];
		private bool updatingZOrder = false;
		private bool isGlowVisible = false;
		private int glowPendingUpdates = 0;

		private void InitChrome()
		{
			wndProc = WndProc;
			CreateGlowWindows();
		} // ctor

		protected override void OnSourceInitialized(EventArgs e)
		{
			hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
			hwndSource.AddHook(wndProc);
			base.OnSourceInitialized(e);
		} // proc OnSourceInitialized

		protected override void OnClosed(EventArgs e)
		{
			hwndSource.RemoveHook(wndProc);
			DestroyGlowWindows();
			base.OnClosed(e);
		} // proc OnClosed

		protected override void OnStateChanged(EventArgs e)
		{
			UpdateGlowWindowVisibility();
			base.OnStateChanged(e);
		}

		protected override void OnLocationChanged(EventArgs e)
		{
			UpdateGlowWindowBounds();
			base.OnLocationChanged(e);
		}

		protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
		{
			UpdateGlowWindowBounds();
			base.OnRenderSizeChanged(sizeInfo);
		}

		#region -- GlowWindowManagement -----------------------------------------------

		private void CreateGlowWindows()
		{
			for (int i = 0; i < 4; i++)
				glowWindows[i] = new GlowWindow(this, (Dock)i);
		} // proc CreateGlowWindows

		private void DestroyGlowWindows()
		{
			for (int i = 0; i < 4; i++)
				glowWindows[i].DestroyNativeResources();
		} // proc DestroyGlowWindows

		private void UpdateGlowWindowBounds()
		{
			using (var gwc = new GlowWindowChanging(this))
			{
				UpdateGlowWindowVisibility();
				for (int i = 0; i < 4; i++)
					glowWindows[i].UpdateBounds();
			}
		}

		private void UpdateGlowWindowVisibility()
		{
			bool showGlow = Visibility == Visibility.Visible && WindowState == System.Windows.WindowState.Normal;
			if (showGlow != isGlowVisible)
			{
				isGlowVisible = showGlow;
				for (int i = 0; i < 4; i++)
					glowWindows[i].IsVisible = isGlowVisible;
			}
		}

		private void UpdateGlowWindowZOrder()
		{
			if (updatingZOrder)
				return;
			try
			{
				updatingZOrder = true;
				WindowInteropHelper windowInteropHelper = new WindowInteropHelper(this);
				IntPtr handle = windowInteropHelper.Handle;
				foreach (var cur in glowWindows)
				{
					IntPtr prevHandle = NativeMethods.GetWindow(cur.Handle, NativeMethods.GW_HWNDPREV);
					if (prevHandle != handle)
						NativeMethods.SetWindowPos(cur.Handle, handle, 0, 0, 0, 0, NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOACTIVATE);
					handle = cur.Handle;
				}
			}
			finally
			{
				updatingZOrder = false;
			}
		} // proc UpdateZOrder


		private void CommitGlowChanges()
		{
			for (int i = 0; i < 4; i++)
				glowWindows[i].CommitChanges();
		}

		#endregion

		#region -- Hooking ------------------------------------------------------------

		private IntPtr WndProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
		{
			switch ((WinMsg)msg)
			{
				case WinMsg.WM_WINDOWPOSCHANGED:
					return WmWindowPosChanged(hWnd, lParam);
				case WinMsg.WM_NCCALCSIZE:
					return WmNcCalcSize(hWnd, msg, wParam, lParam, ref handled);
				case WinMsg.WM_NCHITTEST:
					return WmNcHitTest(hWnd, msg, wParam, lParam, ref handled);
			}
			return IntPtr.Zero;
		} // func WndProc

		private IntPtr WmNcCalcSize(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
		{
			WINDOWPLACEMENT wp = NativeMethods.GetWindowPlacement(hwnd);
			if (wp.showCmd == NativeMethods.SW_SHOWMAXIMIZED)
			{
				RECT rcWindow = (RECT)Marshal.PtrToStructure(lParam, typeof(RECT));
				NativeMethods.DefWindowProc(hwnd, (int)WinMsg.WM_NCCALCSIZE, wParam, lParam);
				RECT rcClient = (RECT)Marshal.PtrToStructure(lParam, typeof(RECT));
				WINDOWINFO wi = new WINDOWINFO();
				NativeMethods.GetWindowInfo(hwnd, wi);
				rcClient.Top = rcWindow.Top + (int)wi.cyWindowBorders;
				Marshal.StructureToPtr(rcClient, lParam, true);
			}
			handled = true;
			return IntPtr.Zero;
		} // func WmNcCalcSize

		private IntPtr WmWindowPosChanged(IntPtr hWnd, IntPtr lParam)
		{
			UpdateGlowWindowZOrder();
			return IntPtr.Zero;
		} // func WmWindowPosChanged

		private IntPtr WmNcHitTest(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
		{
			Point point = this.PointFromScreen(new Point(lParam.ToInt32() & 0xFFFF, lParam.ToInt32() >> 16));
			DependencyObject visualHit = null;
			VisualTreeHelper.HitTest(
				this,
				delegate(DependencyObject target)
				{
					var f = target as FrameworkElement;
					if (f != null && (!f.IsVisible || !f.IsEnabled))
						return HitTestFilterBehavior.ContinueSkipSelfAndChildren;
					return HitTestFilterBehavior.Continue;
				},
				delegate(HitTestResult target)
				{
					visualHit = target.VisualHit;
					return HitTestResultBehavior.Stop;
				},
				new PointHitTestParameters(point));

			int num = (int)HitTestValues.HTCLIENT;
			while (visualHit != null)
			{
				var f = visualHit as FrameworkElement;
				PpsWindowHitTest t;
				if (f != null && f.IsVisible && ((t = f.Tag as PpsWindowHitTest) != null) && t.HitTest != 0)
				{
					num = t.HitTest;
					break;
				}
				visualHit = VisualTreeHelper.GetParent(visualHit) ?? LogicalTreeHelper.GetParent(visualHit);
			}
			handled = true;
			return new IntPtr(num);
		} // func WmNcHitTest

		#endregion
	} // class PpsWindow
}
