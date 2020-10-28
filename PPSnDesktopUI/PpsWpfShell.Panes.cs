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
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using Neo.IronLua;
using TecWare.DE.Stuff;
using TecWare.PPSn.UI;

namespace TecWare.PPSn
{
	public static partial class PpsWpfShell
	{
		#region -- Open Pane helper ---------------------------------------------------

		/// <summary>Initializes a empty pane, it can only be used by a pane manager.</summary>
		/// <param name="paneManager">Pane manager to use.</param>
		/// <param name="paneHost">Pane host, that will be owner of this pane.</param>
		/// <param name="paneType">Type the fill be initialized.</param>
		/// <returns></returns>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public static IPpsWindowPane CreateEmptyPane(this IPpsWindowPaneManager paneManager, IPpsWindowPaneHost paneHost, Type paneType)
		{
			var ti = paneType.GetTypeInfo();
			var ctorBest = (ConstructorInfo)null;
			var ctoreBestParamLength = -1;

			if (!typeof(IPpsWindowPane).IsAssignableFrom(paneType))
				throw new ArgumentException($"{paneType.Name} does not implement {nameof(IPpsWindowPane)}.");

			// search for the longest constructor
			foreach (var ci in ti.GetConstructors())
			{
				var pi = ci.GetParameters();
				if (ctoreBestParamLength < pi.Length)
				{
					ctorBest = ci;
					ctoreBestParamLength = pi.Length;
				}
			}
			if (ctorBest == null)
				throw new ArgumentException($"'{ti.Name}' has no constructor.");

			// create the argument set
			var parameterInfo = ctorBest.GetParameters();
			var paneArguments = new object[parameterInfo.Length];

			for (var i = 0; i < paneArguments.Length; i++)
			{
				var pi = parameterInfo[i];
				var tiParam = pi.ParameterType.GetTypeInfo();
				if (tiParam.IsAssignableFrom(typeof(IServiceProvider)))
					paneArguments[i] = paneManager;
				else if (tiParam.IsAssignableFrom(typeof(IPpsWindowPaneManager)))
					paneArguments[i] = paneManager;
				else if (tiParam.IsAssignableFrom(typeof(IPpsWindowPaneHost)))
					paneArguments[i] = paneHost;
				else if (pi.HasDefaultValue)
					paneArguments[i] = pi.DefaultValue;
				else
					throw new ArgumentException($"Unsupported argument '{pi.Name}' for type '{ti.Name}'.");
			}

			// activate the pane
			return (IPpsWindowPane)Activator.CreateInstance(paneType, paneArguments);
		} // func CreateEmptyPane

		/// <summary>Get default open pane mode</summary>
		/// <param name="paneManager">Pane manager to use.</param>
		/// <param name="arguments"></param>
		/// <returns></returns>
		public static PpsOpenPaneMode GetDefaultPaneMode(this IPpsWindowPaneManager paneManager, dynamic arguments)
		{
			if (arguments != null && arguments.Mode != null)
				return Procs.ChangeType<PpsOpenPaneMode>(arguments.Mode);

			return paneManager.Shell.GetSettings<PpsWindowPaneSettings>().NewPaneMode ? PpsOpenPaneMode.NewPane : PpsOpenPaneMode.ReplacePane;
		} // func GetDefaultPaneMode

		#endregion

		#region -- EqualPane ----------------------------------------------------------

		/// <summary>Are the pane equal to the pane arguments.</summary>
		/// <param name="pane">Pane to compare with.</param>
		/// <param name="paneType">Type of the other pane.</param>
		/// <param name="arguments">Arguments of the other pane.</param>
		/// <returns><c>true</c>, if the pane arguments are the same.</returns>
		public static bool EqualPane(this IPpsWindowPane pane, Type paneType, LuaTable arguments)
			=> pane != null && paneType == pane.GetType() && pane.CompareArguments(arguments ?? new LuaTable()) == PpsWindowPaneCompareResult.Same;

		#endregion

		#region -- SetFullScreen ------------------------------------------------------

		/// <summary></summary>
		/// <param name="window"></param>
		/// <param name="relativeTo"></param>
		public static void SetFullscreen(this Window window, DependencyObject relativeTo)
		{
			var parentWindow = relativeTo == null ? Application.Current.MainWindow : Window.GetWindow(relativeTo);
			IntPtr hMonitor;
			if (parentWindow != null)
			{
				var ps = PresentationSource.FromVisual(parentWindow);
				var windowPoints = new Point[]
				{
					new Point(parentWindow.Left, parentWindow.Top),
					new Point(parentWindow.Left + parentWindow.Width, parentWindow.Top + parentWindow.Height)
				};

				ps.CompositionTarget.TransformToDevice.Transform(windowPoints);

				var rc = new RECT(
					(int)windowPoints[0].X,
					(int)windowPoints[0].Y,
					(int)windowPoints[1].X,
					(int)windowPoints[1].Y
				);
				hMonitor = NativeMethods.MonitorFromRect(ref rc, MonitorOptions.MONITOR_DEFAULTTONEAREST);
			}
			else
			{
				var pt = default(POINT);
				NativeMethods.GetCursorPos(ref pt);
				hMonitor = NativeMethods.MonitorFromPoint(pt, MonitorOptions.MONITOR_DEFAULTTONEAREST);
			}

			var monitorInfo = new MONITORINFO
			{
				cbSize = Marshal.SizeOf(typeof(MONITORINFO))
			};
			if (NativeMethods.GetMonitorInfo(hMonitor, ref monitorInfo) && parentWindow != null)
			{
				var ps = PresentationSource.FromVisual(parentWindow);
				var windowPoints = new Point[]
				{
					new Point(monitorInfo.rcMonitor.Left, monitorInfo.rcMonitor.Top),
					new Point(monitorInfo.rcMonitor.Right, monitorInfo.rcMonitor.Bottom)
				};

				ps.CompositionTarget.TransformFromDevice.Transform(windowPoints);

				window.Left = windowPoints[0].X;
				window.Top = windowPoints[0].Y;
				window.Width = windowPoints[1].X - windowPoints[0].X;
				window.Height = windowPoints[1].Y - windowPoints[0].Y;
			}
			else
				window.WindowState = WindowState.Maximized;
		} // proc SetFullscreen

		#endregion

		#region -- SetFullScreen ------------------------------------------------------

		/// <summary>Get the window from an DependencyObject</summary>
		/// <param name="paneHost"></param>
		/// <returns></returns>
		public static Window GetWindowFromOwner(this IPpsWindowPaneHost paneHost)
			=> GetWindowFromOwner(paneHost as DependencyObject);

		/// <summary>Get the window from an DependencyObject</summary>
		/// <param name="dependencyObject"></param>
		/// <returns></returns>
		public static Window GetWindowFromOwner(this DependencyObject dependencyObject)
		{
			return dependencyObject == null
				? Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
				: Window.GetWindow(dependencyObject);
		} // func GetWindowFromOwner

		#endregion

		#region -- ShowModalDialog ----------------------------------------------------


		/// <summary>Helper to show a system dialog static.</summary>
		/// <param name="owner"></param>
		/// <param name="window"></param>
		/// <returns></returns>
		public static bool ShowModalDialog(this DependencyObject owner, Window window)
		{
			var ownerWindow = GetWindowFromOwner(owner);
			window.Owner = ownerWindow;
			return ShowModalDialog(ownerWindow, window.ShowDialog);
		} // proc ShowModalDialog

		/// <summary>Helper to show a system dialog static.</summary>
		/// <param name="owner"></param>
		/// <param name="showDialog"></param>
		/// <returns></returns>
		public static bool ShowModalDialog(this DependencyObject owner, Func<bool?> showDialog)
			=> ShowModalDialog(GetWindowFromOwner(owner), showDialog);

		private static bool ShowModalDialog(Window ownerWindow, Func<bool?> showDialog)
		{
			var oldWindow = Application.Current.MainWindow;
			Application.Current.MainWindow = ownerWindow;
			try
			{
				return showDialog() ?? false;
			}
			finally
			{
#pragma warning disable CA2219 // Do not raise exceptions in finally clauses
				if (Application.Current.MainWindow != ownerWindow)
					throw new InvalidOperationException();
#pragma warning restore CA2219 // Do not raise exceptions in finally clauses
				Application.Current.MainWindow = oldWindow;
			}
		} // proc ShowModalDialog

		#endregion

		#region -- ShowModalPane ------------------------------------------------------

		/// <summary></summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="paneManager"></param>
		/// <param name="arguments"></param>
		/// <returns></returns>
		public static async Task<T> ShowModalPaneAsync<T>(this IPpsWindowPaneManager paneManager, LuaTable arguments = null)
			where T : class, IPpsWindowPane
		{
			// open new pane
			var pane = (T)await paneManager.OpenPaneAsync(typeof(T), PpsOpenPaneMode.NewPane, arguments);

			// wait for unload
			var paneClosedTask = new TaskCompletionSource<T>();
			pane.PaneHost.PaneUnloaded += (sender, e) => paneClosedTask.SetResult(pane);
			return await paneClosedTask.Task;
		} // func ShowModalPaneAsync

		#endregion
	} // class PpsWpfShell
}
