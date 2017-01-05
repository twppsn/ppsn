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
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TecWare.PPSn.UI
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public partial class PpsWindow
	{
		#region -- enum GlowDirection -------------------------------------------------

		private enum GlowDirection
		{
			West,
			North,
			East,
			South
		} // enum GlowDirection

		#endregion

		#region -- class GlowDrawingHelper --------------------------------------------

		private class GlowDrawingHelper : IDisposable
		{
			private readonly IntPtr windowHandle;
			private readonly GlowBitmap windowBitmap;
			private readonly IntPtr hdcScreen;
			private readonly IntPtr hdcWindow;
			private readonly IntPtr hdcBackground;

			private BLENDFUNCTION blendFunc;
			private int left;
			private int top;
			private int width;
			private int height;
			private bool disposed = false;

			#region -- ctor / dtor ----------------------------------------------------

			public GlowDrawingHelper(IntPtr handle)
			{
				windowHandle = handle;
				GetBounds(handle);

				hdcScreen = NativeMethods.GetDC(IntPtr.Zero);
				hdcWindow = NativeMethods.CreateCompatibleDC(hdcScreen);
				hdcBackground = NativeMethods.CreateCompatibleDC(hdcScreen);
				blendFunc.BlendOp = 0;
				blendFunc.BlendFlags = 0;
				blendFunc.SourceConstantAlpha = 255;
				blendFunc.AlphaFormat = 1;
				windowBitmap = new GlowBitmap(hdcScreen, width, height);
				NativeMethods.SelectObject(hdcWindow, windowBitmap.Handle);
			} // ctor

			public void Dispose()
			{
				Dispose(true);
				GC.SuppressFinalize(this);
			}

			protected virtual void Dispose(bool disposing)
			{
				if (disposed)
					return;

				// Free managed objects here
				if (disposing)
				{
					windowBitmap.Dispose();
				}
				// Free unmanaged objects here. 
				NativeMethods.ReleaseDC(IntPtr.Zero, hdcScreen);
				NativeMethods.DeleteDC(hdcWindow);
				NativeMethods.DeleteDC(hdcBackground);
				disposed = true;
			}

			~GlowDrawingHelper()
			{
				Dispose(false);
			}

			#endregion

			private void GetBounds(IntPtr handle)
			{
				RECT rect;
				NativeMethods.GetWindowRect(handle, out rect);
				left = rect.Left;
				top = rect.Top;
				width = rect.Width;
				height = rect.Height;
			} // proc GetBounds

			#region -- drawing stuff --------------------------------------------------

			public void DrawBitmap(GlowBitmap[] bitmaps, Orientation orientation, int offset)
			{
				if (orientation == Orientation.Horizontal)
					BlendHorizontal(bitmaps, offset);
				else
					BlendVertical(bitmaps, offset);

				Render();
			}

			private void BlendVertical(GlowBitmap[] bitmaps, int offset)
			{
				GlowBitmap bmpTop = bitmaps[0];
				GlowBitmap bmpMiddle = bitmaps[1];
				GlowBitmap bmpBottom = bitmaps[2];

				int yMiddle = bmpTop.Height;
				int yBottom = height - bmpBottom.Height;
				int hMiddle = yBottom - yMiddle;

				Blend(bmpTop, 0, 0, bmpTop.Width, bmpTop.Height);
				if (hMiddle > 0)
					Blend(bmpMiddle, 0, yMiddle, bmpMiddle.Width, hMiddle);
				Blend(bmpBottom, 0, yBottom, bmpBottom.Width, bmpBottom.Height);
			}

			private void BlendHorizontal(GlowBitmap[] bitmaps, int offset)
			{
				GlowBitmap bmpLeft = bitmaps[0];
				GlowBitmap bmpMiddle = bitmaps[1];
				GlowBitmap bmpRight = bitmaps[2];

				int xLeft = offset;
				int xMiddle = xLeft + bmpLeft.Width;
				int xRight = width - offset - bmpRight.Width;
				int wMiddle = xRight - xMiddle;
				Blend(bmpLeft, xLeft, 0, bmpLeft.Width, bmpLeft.Height);
				if (wMiddle > 0)
					Blend(bmpMiddle, xMiddle, 0, wMiddle, bmpMiddle.Height);
				Blend(bmpRight, xRight, 0, bmpRight.Width, bmpRight.Height);
			}

			private void Blend(GlowBitmap bmp, int xOriginDest, int yOriginDest, int widthDest, int heightDest)
			{
				NativeMethods.SelectObject(hdcBackground, bmp.Handle);
				NativeMethods.AlphaBlend(hdcWindow, xOriginDest, yOriginDest, widthDest, heightDest, hdcBackground, 0, 0, bmp.Width, bmp.Height, blendFunc);
			}
  
			private void Render()
			{
				var pptDst = new POINT { x = left, y = top };
				var psize = new SIZE { cx = width, cy = height };
				var pptSrc = new POINT { x = 0, y = 0 };
				NativeMethods.UpdateLayeredWindow(windowHandle, hdcScreen, ref pptDst, ref psize, hdcWindow, ref pptSrc, 0u, ref blendFunc, NativeMethods.ULW_ALPHA);
			}

			#endregion
		} // class GlowDrawingHelper

		#endregion

		#region -- class GlowBitmap ---------------------------------------------------

		private class GlowBitmap : IDisposable
		{
			#region -- enum BitmapPart ------------------------------------------------

			private enum BitmapPart
			{
				WestTop,
				WestMiddle,
				WestBottom,
				NorthLeft,
				NorthMiddle,
				NorthRight,
				EastTop,
				EastMiddle,
				EastBottom,
				SouthLeft,
				SouthMiddle,
				SouthRight
			}

			#endregion

			#region -- class CachedBitmapInfo -----------------------------------------

			private class CachedBitmapInfo
			{
				public readonly int Width;
				public readonly int Height;
				public readonly byte[] DIBits;
				public CachedBitmapInfo(byte[] diBits, int width, int height)
				{
					Width = width;
					Height = height;
					DIBits = diBits;
				}
			}  // class CachedBitmapInfo

			#endregion

			private static readonly CachedBitmapInfo[] alphaMasks = new CachedBitmapInfo[12];
			private readonly IntPtr hBmp;
			private readonly IntPtr pbits;
			private readonly BITMAPINFO bitmapInfo;
			private bool disposed = false;

			#region -- ctor / dtor-----------------------------------------------------

			public GlowBitmap(IntPtr hdc, int width, int height)
			{
				bitmapInfo.biSize = Marshal.SizeOf(typeof(BITMAPINFOHEADER));
				bitmapInfo.biPlanes = 1;
				bitmapInfo.biBitCount = 32;
				bitmapInfo.biCompression = NativeMethods.BI_RGB;
				bitmapInfo.biXPelsPerMeter = 0;
				bitmapInfo.biYPelsPerMeter = 0;
				bitmapInfo.biWidth = width;
				// negatve: top-down DIB 
				bitmapInfo.biHeight = -height;
				hBmp = NativeMethods.CreateDIBSection(hdc, ref bitmapInfo, NativeMethods.DIB_RGB_COLORS, out pbits, IntPtr.Zero, 0u);
			} // ctor

			public static GlowBitmap FromImage(int imagePos, Color color)
			{
				CreateAlphaMask();
				CachedBitmapInfo alphaMask = alphaMasks[imagePos];
				IntPtr hdc = NativeMethods.GetDC(IntPtr.Zero);
				var glowBitmap = new GlowBitmap(hdc, alphaMask.Width, alphaMask.Height);
				for (int i = 0; i < alphaMask.DIBits.Length; i += 4)
				{
					byte alpha = alphaMask.DIBits[i + 3];
					byte red = (byte)((double)(color.R * alpha) / 255.0);
					byte green = (byte)((double)(color.G * alpha) / 255.0);
					byte blue = (byte)((double)(color.B * alpha) / 255.0);
					Marshal.WriteByte(glowBitmap.DIBits, i, blue);
					Marshal.WriteByte(glowBitmap.DIBits, i + 1, green);
					Marshal.WriteByte(glowBitmap.DIBits, i + 2, red);
					Marshal.WriteByte(glowBitmap.DIBits, i + 3, alpha);
				}
				NativeMethods.ReleaseDC(IntPtr.Zero, hdc);
				return glowBitmap;
			} // ctor static

			public void Dispose()
			{
				Dispose(true);
				GC.SuppressFinalize(this);
			}

			protected virtual void Dispose(bool disposing)
			{
				if (disposed)
					return;
				// no managed objects

				// Free unmanaged objects here. 
				NativeMethods.DeleteObject(hBmp);
				disposed = true;
			}

			~GlowBitmap()
			{
				Dispose(false);
			}

			#endregion

			private static void CreateAlphaMask()
			{
				// run once
				if (alphaMasks[0] != null)
					return;

				string assembly = typeof(GlowBitmap).Assembly.GetName().Name;
				for (int i = 0; i < 12; i++)
				{
					string path = String.Format("Images/{0}.png", (BitmapPart)i);
					var uri = new Uri(String.Format("pack://application:,,,/{0};component/{1}", assembly, path), UriKind.Absolute);
					var bitmapImage = new BitmapImage(uri);
					byte[] array = new byte[4 * bitmapImage.PixelWidth * bitmapImage.PixelHeight];
					int stride = 4 * bitmapImage.PixelWidth;
					bitmapImage.CopyPixels(array, stride, 0);
					alphaMasks[i] = new CachedBitmapInfo(array, bitmapImage.PixelWidth, bitmapImage.PixelHeight);
				}
			}
			public IntPtr Handle { get { return hBmp; } }
			public IntPtr DIBits { get { return pbits; } }
			public int Width { get { return bitmapInfo.biWidth; } }
			public int Height { get { return -bitmapInfo.biHeight; } }
		} // class GlowBitmap

		#endregion

		#region -- class GlowWindow ---------------------------------------------------

		private sealed class GlowWindow : IDisposable
		{
			#region -- enum PropertyType ----------------------------------------------

			[Flags]
			private enum PropertyType
			{
				None = 0,
				Location = 1,
				Size = 2,
				Visibility = 4,
				ActiveColor = 8,
				InactiveColor = 16,
				Draw = 32
			} // enum PropertyType

			#endregion

			private const int glowDimension = 10;
			private static ushort sharedClassAtom = 0;
			private static NativeMethods.WndProc sharedWndProc;
			private static int sharedClassCount = 0;

			private readonly PpsWindow ownerWindow;
			private readonly GlowDirection direction;
			private readonly GlowBitmap[] activeBitmaps = new GlowBitmap[3];
			private readonly GlowBitmap[] inactiveBitmaps = new GlowBitmap[3];
			private IntPtr handle;
			private Delegate wndProc;
			private PropertyType changedPropertys;
			private int left;
			private int top;
			private int width;
			private int height;
			private bool isVisible;
			private bool isActive;
			private bool disposed;
            private Color activeColor = Colors.Black;
			private Color inactiveColor = Colors.LightGray;

			#region -- ctor / dtor ----------------------------------------------------

			public GlowWindow(PpsWindow ownerWindow, GlowDirection direction)
			{
				this.ownerWindow = ownerWindow;
				this.direction = direction;
				CreateNativeWindow();
			} // ctor

			private void CreateNativeWindow()
			{
				CreateWindowClass();
				CreateWindowHandle();
				CreateBitMaps();
			}

			public void Dispose()
			{
				Dispose(true);
				GC.SuppressFinalize(this);
			}

			private void Dispose(bool disposing)
			{
				if (disposed)
					return;

				// Free managed objects here
				if (disposing)
				{
					DestroyBitmaps();
                }
				// Free unmanaged objects here. 
				DestroyWindowHandle();
				DestroyWindowClass();
				disposed = true;
			}

			~GlowWindow()
			{
				Dispose(false);
			}

			#endregion

			#region -- NativeWindowClassAtom ------------------------------------------

			private void CreateWindowClass()
			{
				sharedClassCount++;
				if (sharedClassAtom != 0)
					return;

				sharedWndProc = new NativeMethods.WndProc(NativeMethods.DefWindowProc);

				WNDCLASS wndclass = default(WNDCLASS);
				wndclass.cbClsExtra = 0;
				wndclass.cbWndExtra = 0;
				wndclass.hbrBackground = IntPtr.Zero;
				wndclass.hCursor = IntPtr.Zero;
				wndclass.hIcon = IntPtr.Zero;
				wndclass.lpfnWndProc = sharedWndProc;
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
				int dwExStyle = NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_LAYERED;
				int dwStyle = NativeMethods.WS_POPUP | NativeMethods.WS_CLIPCHILDREN | NativeMethods.WS_CLIPSIBLINGS;
				handle = NativeMethods.CreateWindowEx(dwExStyle, new IntPtr((int)sharedClassAtom), string.Empty, dwStyle, 0, 0, 0, 0, new WindowInteropHelper(ownerWindow).Owner, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
				wndProc = new NativeMethods.WndProc(WndProc);
				// subclass
				NativeMethods.SetWindowLongPtr(handle, NativeMethods.GWL_WNDPROC, Marshal.GetFunctionPointerForDelegate(wndProc));
			} // proc CreateWindowHandle

			private void DestroyWindowHandle()
			{
				if (handle == IntPtr.Zero)
					return;

				NativeMethods.DestroyWindow(handle);
				handle = IntPtr.Zero;
			} // proc DestroyWindowHandle

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
						return new IntPtr(WmNcHitTest(lParam));
					case WinMsg.WM_NCLBUTTONDOWN:
					case WinMsg.WM_NCLBUTTONDBLCLK:
					case WinMsg.WM_NCRBUTTONDOWN:
					case WinMsg.WM_NCRBUTTONDBLCLK:
					case WinMsg.WM_NCMBUTTONDOWN:
					case WinMsg.WM_NCMBUTTONDBLCLK:
					case WinMsg.WM_NCXBUTTONDOWN:
					case WinMsg.WM_NCXBUTTONDBLCLK:
						{
							var ownerWindowHandle = new WindowInteropHelper(ownerWindow).Handle;
							NativeMethods.SendMessage(ownerWindowHandle, (int)WinMsg.WM_ACTIVATE, new IntPtr(NativeMethods.WA_CLICKACTIVE), IntPtr.Zero);
							NativeMethods.SendMessage(ownerWindowHandle, msg, wParam, IntPtr.Zero);
							return IntPtr.Zero;
						}
				}
				return NativeMethods.DefWindowProc(hwnd, msg, wParam, lParam);
			} // func WndProc

			private int WmNcHitTest(IntPtr lParam)
			{
				RECT rect;
				NativeMethods.GetWindowRect(handle, out rect);
				var value = lParam.ToInt32();
				var xLParam = (int)((short)(value & 0xFFFF));
				var yLParam = (int)((short)(value >> 16));
				int offset = 2 * glowDimension;
				switch (direction)
				{
					case GlowDirection.West:
						if (yLParam - offset < rect.Top)
							return (int)HitTestValues.HTTOPLEFT;
						if (yLParam + offset > rect.Bottom)
							return (int)HitTestValues.HTBOTTOMLEFT;
						return (int)HitTestValues.HTLEFT;
					case GlowDirection.North:
						if (xLParam - offset < rect.Left)
							return (int)HitTestValues.HTTOPLEFT;
						if (xLParam + offset > rect.Right)
							return (int)HitTestValues.HTTOPRIGHT;
						return (int)HitTestValues.HTTOP;
					case GlowDirection.East:
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
			} // func WmNcHitTest

			#endregion

			#region -- Bitmaps --------------------------------------------------------

			private void CreateBitMaps()
			{
				int offset = 3 * (int)direction;
				for (int i = 0; i < 3; i++)
				{
					activeBitmaps[i] = GlowBitmap.FromImage(i + offset, activeColor);
					inactiveBitmaps[i] = GlowBitmap.FromImage(i + offset, inactiveColor);
				}
			}

			private void DestroyBitmaps()
			{
				for (int i = 0; i < 3; i++)
				{
					activeBitmaps[i].Dispose();
					activeBitmaps[i] = null;
					inactiveBitmaps[i].Dispose();
					inactiveBitmaps[i] = null;
				}
			}

			private void ChangeBitmaps(GlowBitmap[] bitmaps, Color newColor)
			{
				int offset = 3 * (int)direction;
				for (int i = 0; i < 3; i++)
				{
					bitmaps[i].Dispose();
					bitmaps[i] = null;
					bitmaps[i] = GlowBitmap.FromImage(i + offset, newColor);
				}
			}

			#endregion

			#region -- Updating -------------------------------------------------------

			public void UpdateBounds()
			{
				var ownerWindowHandle = new WindowInteropHelper(ownerWindow).Handle;

				RECT rect;
				NativeMethods.GetWindowRect(ownerWindowHandle, out rect);
				if (IsVisible)
				{
					// Ecken müssen sich Überschneiden, sonst ist beim Resize mit der Mouse der Hintergrund sichtbar
					switch (direction)
					{
						case GlowDirection.West:
							Left = rect.Left - glowDimension;
							Top = rect.Top - glowDimension;
							Width = glowDimension;
							Height = rect.Height + 2 * glowDimension;
							return;
						case GlowDirection.North:
							Left = rect.Left - glowDimension;
							Top = rect.Top - glowDimension;
							Width = rect.Width + 2 * glowDimension;
							Height = glowDimension;
							return;
						case GlowDirection.East:
							Left = rect.Right;
							Top = rect.Top - glowDimension;
							Width = glowDimension;
							Height = rect.Height + 2 * glowDimension;
							return;
						default:
							Left = rect.Left - glowDimension;
							Top = rect.Bottom;
							Width = rect.Width + 2 * glowDimension;
							Height = glowDimension;
							break;
					}
				}
			}

			public void UpdateColors(Color activeColor, Color inactiveColor)
			{
				ActiveColor = activeColor;
				InactiveColor = inactiveColor;
			}

			public void CommitChanges()
			{
				UpdateColors();
				UpdateWindowPos();
				UpdateCanvas();
				changedPropertys = PropertyType.None;
			}

			private void UpdateColors()
			{
				if (changedPropertys.HasFlag(PropertyType.ActiveColor))
				{
					ChangeBitmaps(activeBitmaps, activeColor);
				}
				if (changedPropertys.HasFlag(PropertyType.InactiveColor))
				{
					ChangeBitmaps(inactiveBitmaps, inactiveColor);
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
						flags |= NativeMethods.SWP_NOMOVE;
					}
					if (!changedPropertys.HasFlag(PropertyType.Size))
					{
						flags |= NativeMethods.SWP_NOSIZE;
					}
					NativeMethods.SetWindowPos(handle, IntPtr.Zero, Left, Top, Width, Height, flags);
				}
			} // proc UpdateWindowPos

			private void UpdateCanvas()
			{
				if (!isVisible || !changedPropertys.HasFlag(PropertyType.Draw))
					return;

				using (var draw = new GlowDrawingHelper(handle))
				{
					Orientation orientation;
					switch (direction)
					{
						case GlowDirection.West:
						case GlowDirection.East:
							orientation = Orientation.Vertical;
							break;
						default:
							orientation = Orientation.Horizontal;
							break;
					}
					GlowBitmap[] bitmaps = IsActive ? activeBitmaps : inactiveBitmaps;
					draw.DrawBitmap(bitmaps, orientation, glowDimension);
				}
			} // proc UpdateWindowCanvas

			#endregion

			#region -- Properties -----------------------------------------------------

			public IntPtr Handle { get { return handle; } }

			public bool IsVisible { get { return isVisible; } set { UpdateProperty(ref isVisible, value, PropertyType.Visibility | PropertyType.Draw); } }

			public bool IsActive { get { return isActive; } set { UpdateProperty(ref isActive, value, PropertyType.Draw); } }

			private Color ActiveColor { get { return activeColor; } set { UpdateProperty(ref activeColor, value, PropertyType.ActiveColor | PropertyType.Draw); } }

			private Color InactiveColor { get { return inactiveColor; } set { UpdateProperty(ref inactiveColor, value, PropertyType.InactiveColor | PropertyType.Draw); } }

			private int Left { get { return left; } set { UpdateProperty(ref left, value, PropertyType.Location); } }

			private int Top { get { return top; } set { UpdateProperty(ref top, value, PropertyType.Location); } }

			private int Width { get { return width; } set { UpdateProperty(ref width, value, PropertyType.Size | PropertyType.Draw); } }

			private int Height { get { return height; } set { UpdateProperty(ref height, value, PropertyType.Size | PropertyType.Draw); } }

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
			}

			protected virtual void Dispose(bool disposing)
			{
				if (disposed)
					return;

				if (disposing)
				{
					ownerWindow.glowPendingUpdates--;
					if (ownerWindow.glowPendingUpdates == 0)
						ownerWindow.CommitGlowChanges();
				}
				disposed = true;
			}
		}

		#endregion

		private HwndSource hwndSource;
		private HwndSourceHook wndProc;
		private readonly GlowWindow[] glowWindows = new GlowWindow[4];
		private bool updatingZOrder = false;
		private bool isGlowVisible = false;
		private int glowPendingUpdates = 0;
		//private bool launchSysMenuOnRButttonUp = false;  Handling SysMenu

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
		}

		protected override void OnClosed(EventArgs e)
		{
			hwndSource.RemoveHook(wndProc);
			DestroyGlowWindows();
			base.OnClosed(e);
		}

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

		protected override void OnActivated(EventArgs e)
		{
			UpdateGlowWindowActiveState();
			base.OnActivated(e);
		}

		protected override void OnDeactivated(EventArgs e)
		{
			UpdateGlowWindowActiveState();
			base.OnDeactivated(e);
		}

		private static void OnGlowColorChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
		{
			((PpsWindow)obj).UpdateGlowWindowColors();
		}

		#region -- GlowWindowManagement -----------------------------------------------

		private void CreateGlowWindows()
		{
			for (int i = 0; i < 4; i++)
				glowWindows[i] = new GlowWindow(this, (GlowDirection)i);
		} // proc CreateGlowWindows

		private void DestroyGlowWindows()
		{
			for (int i = 0; i < 4; i++)
				glowWindows[i].Dispose();
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

		private void UpdateGlowWindowColors()
		{
			using (var gwc = new GlowWindowChanging(this))
			{
				for (int i = 0; i < 4; i++)
					glowWindows[i].UpdateColors(ActiveGlowColor, InactiveGlowColor);
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

		private void UpdateGlowWindowActiveState()
		{
			using (var gwc = new GlowWindowChanging(this))
			{
				for (int i = 0; i < 4; i++)
					glowWindows[i].IsActive = base.IsActive;
			}
		}

		private void UpdateGlowWindowZOrder()
		{
			if (updatingZOrder)
				return;
			try
			{
				updatingZOrder = true;
				var windowInteropHelper = new WindowInteropHelper(this);
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
		} // proc UpdateGlowWindowZOrder

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
				case WinMsg.WM_NCLBUTTONDOWN:
				case WinMsg.WM_NCMBUTTONDOWN:
				case WinMsg.WM_NCRBUTTONDOWN:
				case WinMsg.WM_NCXBUTTONDOWN:
					OnWindowCaptionClicked();
					break;
				// future work: SystemMenu
				//case WinMsg.WM_RBUTTONDOWN:
				//case WinMsg.WM_RBUTTONUP:
				//case WinMsg.WM_NCXBUTTONDBLCLK:
				//	launchSysMenuOnRButttonUp = false;
				//	break;
				//case WinMsg.WM_NCRBUTTONDOWN:
				//	OnWindowCaptionClicked();
				//	launchSysMenuOnRButttonUp = wParam == (IntPtr)HitTestValues.HTCAPTION;
				//	break;
				//case WinMsg.WM_NCRBUTTONUP:
				//	if (launchSysMenuOnRButttonUp && wParam == (IntPtr)HitTestValues.HTCAPTION)
				//	{
				//		System.Diagnostics.Debug.Print("------------------------------>  CONTEXTMENU");
				//	}
				//	launchSysMenuOnRButttonUp = false;
				//	break;
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
				var windowinfo = new WINDOWINFO();
				NativeMethods.GetWindowInfo(hwnd, windowinfo);
				rcClient.Top = rcWindow.Top + (int)windowinfo.cyWindowBorders;
				Marshal.StructureToPtr(rcClient, lParam, true);
			}
			handled = true;
			return IntPtr.Zero;
		} // func WmNcCalcSize

		private IntPtr WmWindowPosChanged(IntPtr hWnd, IntPtr lParam)
		{
			UpdateGlowWindowZOrder();
			return IntPtr.Zero;
		}

		private IntPtr WmNcHitTest(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
		{
			var value = lParam.ToInt32();
			var x = (int)((short)(value & 0xFFFF));
			var y = (int)((short)(value >> 16));
			Point point = PointFromScreen(new Point(x, y));

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

		public Color ActiveGlowColor { get { return (Color)base.GetValue(ActiveGlowColorProperty); } set { base.SetValue(ActiveGlowColorProperty, value); } }
		public Color InactiveGlowColor { get { return (Color)base.GetValue(InactiveGlowColorProperty); } set { base.SetValue(InactiveGlowColorProperty, value); } }
	} // class PpsWindow
}