#region -- copyright --
//
// Licensed to the Apache Software Foundation (ASF) under one
// or more contributor license agreements.  See the NOTICE file
// distributed with this work for additional information
// regarding copyright ownership.  The ASF licenses this file
// to you under the Apache License, Version 2.0 (the
// "License"); you may not use this file except in compliance
// with the License.  You may obtain a copy of the License at
// 
//   http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing,
// software distributed under the License is distributed on an
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
// KIND, either express or implied.  See the License for the
// specific language governing permissions and limitations
// under the License.
//
#endregion
using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace TecWare.PPSn.Interop
{
	#region -- class WindowHandle -----------------------------------------------------

	/// <summary>Safe window handle</summary>
	public sealed class WindowHandle : SafeHandle
	{
		/// <summary>Create a safe window handle</summary>
		/// <param name="hWnd">Handle</param>
		/// <param name="ownsHandle">Is this the owner of the handle.</param>
		public WindowHandle(IntPtr hWnd, bool ownsHandle = true)
			: base(IntPtr.Zero, ownsHandle)
		{
			SetHandle(hWnd);
		} // ctor

		/// <summary>Check if the current handle is valid.</summary>
		public override bool IsInvalid
			=> handle == IntPtr.Zero;

		/// <summary>Destroy window on release.</summary>
		/// <returns></returns>
		protected override bool ReleaseHandle()
			=> NativeMethods.DestroyWindow(handle);
	} // class WindowHandle

	#endregion

	#region -- class Win32Window ------------------------------------------------------

	/// <summary>Win32 basic window implementation</summary>
	public abstract class Win32Window : IWin32Window, IDisposable
	{
		#region -- class WindowClass --------------------------------------------------

		/// <summary>Create a window class for the win32 window</summary>
		protected sealed class WindowClass : IDisposable
		{
			private readonly NativeMethods.WndProc defWindowProc;
			private readonly IntPtr hWndClass;

			/// <summary>Create a window class for the win32 window</summary>
			/// <param name="className">Name of the window class.</param>
			public WindowClass(string className)
			{
				defWindowProc = new NativeMethods.WndProc(NativeMethods.DefWindowProc);

				var wndclass = default(WNDCLASS);
				wndclass.cbClsExtra = 0;
				wndclass.cbWndExtra = 0;
				wndclass.hbrBackground = IntPtr.Zero;
				wndclass.hCursor = IntPtr.Zero;
				wndclass.hIcon = IntPtr.Zero;
				wndclass.lpfnWndProc = defWindowProc;
				wndclass.lpszClassName = className;
				wndclass.lpszMenuName = null;
				wndclass.style = 0u;
				hWndClass = new IntPtr(NativeMethods.RegisterClass(ref wndclass));
			} // ctor

			/// <summary>Destructor</summary>
			~WindowClass()
			{
				Dispose(false);
			} // dtor

			/// <summary>Unregister window class.</summary>
			public void Dispose()
			{
				GC.SuppressFinalize(this);
				Dispose(true);
			} // proc Dispose

			private void Dispose(bool disposing)
				=> NativeMethods.UnregisterClass(hWndClass, NativeMethods.GetModuleHandle(null));

			/// <summary>Window class atom handle.</summary>
			public IntPtr WndClass => hWndClass;
		} // class WindowClass

		#endregion

		private readonly WindowClass windowClass;
		private readonly NativeMethods.WndProc wndProc;
		private WindowHandle handle = null;
		private bool isDisposed = false;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary>Win32 basic window implementation</summary>
		protected Win32Window()
		{
			wndProc = new NativeMethods.WndProc(WndProc);
			windowClass = CreateWindowClass();
		} // ctor

		/// <summary>Create the window handle.</summary>
		/// <param name="caption"></param>
		/// <param name="style"></param>
		/// <param name="exStyle"></param>
		/// <param name="parentWindow"></param>
		/// <returns></returns>
		protected void CreateWindow(string caption = null, int style = 0, int exStyle = 0, IntPtr parentWindow = default(IntPtr))
		{
			if (handle != null)
				throw new InvalidOperationException();

			// create window
			handle = new WindowHandle(NativeMethods.CreateWindowEx(
				exStyle,
				windowClass.WndClass,
				caption ?? String.Empty,
				style,
				0, 0, 0, 0,
				parentWindow,
				IntPtr.Zero,
				IntPtr.Zero,
				IntPtr.Zero
			));

			// subclass
			NativeMethods.SetWindowLongPtr(
				handle.DangerousGetHandle(),
				NativeMethods.GWL_WNDPROC,
				Marshal.GetFunctionPointerForDelegate(wndProc)
			);
		} // func CreateWindow

		/// <summary>Gets called to create a window class for the window.</summary>
		/// <returns></returns>
		protected abstract WindowClass CreateWindowClass();

		/// <summary>Gets called if a window gets destroyed.</summary>
		/// <param name="windowClass"></param>
		protected abstract void DestroyWindowClass(WindowClass windowClass);

		/// <summary></summary>
		~Win32Window()
		{
			Dispose(false);
		} // dtor

		/// <summary>Destroy the window.</summary>
		public void Dispose()
		{
			GC.SuppressFinalize(this);
			Dispose(true);
		} // proc Dispose

		/// <summary>Override to destroy resources.</summary>
		/// <param name="disposing"></param>
		protected virtual void Dispose(bool disposing)
		{
			if (IsDisposed)
				return;

			// destroy window
			handle.Dispose();
			DestroyWindowClass(windowClass);

			isDisposed = true;
		} // proc Dispose

		#endregion

		/// <summary>Window Proc</summary>
		/// <param name="hwnd"></param>
		/// <param name="msg"></param>
		/// <param name="wParam"></param>
		/// <param name="lParam"></param>
		/// <returns></returns>
		protected virtual IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam)
			=> NativeMethods.DefWindowProc(hwnd, msg, wParam, lParam);

		/// <summary>Is the window destroyed.</summary>
		public bool IsDisposed => isDisposed;
		/// <summary>Handle of the window.</summary>
		public IntPtr Handle => handle.DangerousGetHandle();
	} // class Win32Window

	#endregion
}