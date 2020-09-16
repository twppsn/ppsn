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
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using System.Xml;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Networking;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;

namespace TecWare.PPSn.UI
{
	#region -- enum PpsWindowPaneCompareResult ----------------------------------------

	/// <summary>Result for the compare of two panes</summary>
	public enum PpsWindowPaneCompareResult
	{
		/// <summary>Panes are not compatible or reusable.</summary>
		Incompatible,
		/// <summary>Reload of data is necessary.</summary>
		Reload,
		/// <summary>The same pane, with the same data.</summary>
		Same
	} // enum PpsWindowPaneCompareResult

	#endregion

	#region -- interface IPpsWindowPaneHost -------------------------------------------

	/// <summary>The host of the window pane.</summary>
	public interface IPpsWindowPaneHost
	{
		/// <summary>Close this pane.</summary>
		Task<bool> ClosePaneAsync();

		/// <summary>Dispatcher of the host.</summary>
		Dispatcher Dispatcher { get; }
		/// <summary>Progress bar of the host.</summary>
		IPpsProgressFactory Progress { get; }
		/// <summary>PaneManager</summary>
		IPpsWindowPaneManager PaneManager { get; }
	} // interface IPpsWindowPaneHost

	#endregion

	#region -- interface IPpsWindowPane -----------------------------------------------

	/// <summary>Implements the basic function of a pane for the window client area.</summary>
	/// <remarks>The implementer can have a constructor with argumnets. E.g. to retrieve a
	///  - <seealso cref="IPpsWindowPaneManager"/>
	///  - <seealso cref="IPpsWindowPaneHost"/>.</remarks>
	public interface IPpsWindowPane : INotifyPropertyChanged, IDisposable
	{
		/// <summary>Loads the content of a pane</summary>
		/// <param name="args">Arguments for the pane.</param>
		/// <returns></returns>
		Task LoadAsync(LuaTable args);
		/// <summary>Unloads the content of a pane.</summary>
		/// <param name="commit"><c>null</c>, ask the user for a action.</param>
		/// <returns>False, if the content can not be unloaded.</returns>
		Task<bool> UnloadAsync(bool? commit = null);

		/// <summary>Compare the pane with the given pane arguments.</summary>
		/// <param name="args">Arguments for the pane.</param>
		/// <returns></returns>
		PpsWindowPaneCompareResult CompareArguments(LuaTable args);

		/// <summary>Title of the content.</summary>
		/// <remarks>Can not be implemented hidden, because of the binding.</remarks>
		string Title { get; }
		/// <summary>Sub title of the content.</summary>
		/// <remarks>Can not be implemented hidden, because of the binding.</remarks>
		string SubTitle { get; }
		/// <summary>Image of the window pane.</summary>
		object Image { get; }

		/// <summary>Mark the sidebar mode for the control.</summary>
		bool HasSideBar { get; }

		/// <summary>Control for the pane data.</summary>
		/// <remarks>Can not be implemented hidden, because of the binding.</remarks>
		object Control { get; }

		/// <summary>Access the pane host.</summary>
		IPpsWindowPaneHost PaneHost { get; }
		/// <summary>Commands attached to the pane.</summary>
		PpsUICommandCollection Commands { get; }

		/// <summary>Points to the current selected object.</summary>
		IPpsDataInfo CurrentData { get; }

		/// <summary>If the pane contains changes, this flag is <c>true</c>.</summary>
		bool IsDirty { get; }
		
		/// <summary>Help key for the current pane.</summary>
		string HelpKey { get; }
	} // interface IPpsWindowPane

	#endregion

	#region -- interface IPpsWindowPaneBack -------------------------------------------

	/// <summary>Pane supports a back-button</summary>
	public interface IPpsWindowPaneBack
	{
		/// <summary>Invoke the back button.</summary>
		void InvokeBackButton();

		/// <summary>Is back button active.</summary>
		bool? CanBackButton { get; }
	} // interface IPpsWindowPaneBack

	#endregion

	#region -- enum PpsOpenPaneMode ---------------------------------------------------

	/// <summary>Prefered mode to open the pane.</summary>
	public enum PpsOpenPaneMode
	{
		/// <summary>Default</summary>
		Default,
		/// <summary>Replace the current pane.</summary>
		ReplacePane,
		/// <summary>Create a new pane in the current window (in single window this option will open a new single window).</summary>
		NewPane,
		/// <summary>Create a new main window with this pane.</summary>
		NewMainWindow,
		/// <summary>Create a new single window with this pane.</summary>
		NewSingleWindow,
		/// <summary>Create a new single window with this pane.</summary>
		NewSingleDialog
	} // enum PpsOpenPaneMode

	#endregion

	#region -- interface IPpsWindowPaneManager ----------------------------------------

	/// <summary>Interface to open new panes.</summary>
	public interface IPpsWindowPaneManager : IServiceProvider
	{
		/// <summary>Activate the pane.</summary>
		/// <param name="pane"></param>
		/// <returns></returns>
		bool ActivatePane(IPpsWindowPane pane);
		/// <summary>Load a new pane</summary>
		/// <param name="paneType">Pane type.</param>
		/// <param name="newPaneMode">Pane create mode.</param>
		/// <param name="arguments">Arguments for the pane.</param>
		/// <returns></returns>
		Task<IPpsWindowPane> OpenPaneAsync(Type paneType, PpsOpenPaneMode newPaneMode = PpsOpenPaneMode.Default, LuaTable arguments = null);
		/// <summary>Find a pane, that is already open.</summary>
		/// <param name="paneType"></param>
		/// <param name="arguments"></param>
		/// <returns></returns>
		IPpsWindowPane FindOpenPane(Type paneType, LuaTable arguments = null);

		/// <summary>Pane enumeration of this pane manager and child pane manager.</summary>
		IEnumerable<IPpsWindowPane> Panes { get; }

		/// <summary>Access the environment.</summary>
		PpsShellWpf _Shell { get; }
		/// <summary>Access the assigned shell.</summary>
		IPpsShell Shell { get; }

		/// <summary>Is this pane manager currenty the active pane manager.</summary>
		bool IsActive { get; }
	} // interface IPpsWindowPaneManager

	#endregion

	#region -- class PpsWindowPaneHelper ----------------------------------------------

	/// <summary>Extensions for the Pane, or PaneControl</summary>
	public static class PpsWindowPaneHelper
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

		private static Type GetPaneType(IPpsWindowPaneManager paneManager, dynamic arguments)
		{
			var paneTypeValue = (object)arguments?.PaneType;

			switch (paneTypeValue)
			{
				case Type type:
					return type;
				case LuaType luaType:
					return luaType.Type;
				case string typeString:
					return paneManager._Shell.GetPaneTypeFromString(typeString);
				case null:
					throw new ArgumentNullException("paneType");
				default:
					throw new ArgumentException("Could not parse pane type.", "paneType");
			}
		} // func GetPaneType

		/// <summary>Get default open pane mode</summary>
		/// <param name="paneManager">Pane manager to use.</param>
		/// <param name="arguments"></param>
		/// <returns></returns>
		public static PpsOpenPaneMode GetDefaultPaneMode(this IPpsWindowPaneManager paneManager, dynamic arguments)
		{
			if (arguments != null && arguments.Mode != null)
				return Procs.ChangeType<PpsOpenPaneMode>(arguments.Mode);

			return paneManager._Shell.GetOptionalValue("NewPaneMode", true) ? PpsOpenPaneMode.NewPane : PpsOpenPaneMode.ReplacePane;
		} // func GetDefaultPaneMode

		/// <summary>Loads a generic wpf window pane <see cref="PpsGenericWpfWindowPane"/>.</summary>
		/// <param name="paneManager">Pane manager to use.</param>
		/// <param name="arguments">Argument for the pane load function. This is pane specific.</param>
		/// <returns>A full inialized pane.</returns>
		/// <remarks>This function uses <c>AwaitTask</c>
		/// 
		/// - <c>arguments.mode</c>: is the <see cref="PpsOpenPaneMode"/> (optional)
		/// </remarks>
		public static IPpsWindowPane LoadGenericPane(this IPpsWindowPaneManager paneManager, LuaTable arguments = null)
			=> paneManager.OpenPaneAsync(typeof(PpsGenericWpfWindowPane), PpsOpenPaneMode.Default, arguments).AwaitTask();

		/// <summary>Loads a pane of specific type with the givven arguments.</summary>
		/// <param name="paneManager">Pane manager to use.</param>
		/// <param name="arguments">Argument for the pane load function. This is pane specific.</param>
		/// <returns>A full inialized pane.</returns>
		/// <remarks>This function uses <c>AwaitTask</c>
		/// 
		/// - <c>arguments.mode</c>: is the <see cref="PpsOpenPaneMode"/> (optional)
		/// - <c>arguments.paneType</c>: is the type as string or type, of the pane. (required)
		///   Well known Pane types are:
		///   - mask
		///   - generic
		///   - picture
		///   - pdf
		/// </remarks>
		public static IPpsWindowPane LoadPane(this IPpsWindowPaneManager paneManager, LuaTable arguments = null)
		{
			var paneType = GetPaneType(paneManager, arguments);
			if (paneType == null)
				throw new ArgumentException("Pane type is missing.");

			return paneManager.OpenPaneAsync(paneType, GetDefaultPaneMode(paneManager, arguments), arguments).AwaitTask();
		} // func OpenPane

		/// <summary></summary>
		/// <param name="request"></param>
		/// <param name="arguments"></param>
		/// <param name="paneUri"></param>
		/// <returns></returns>
		public static async Task<object> LoadPaneDataAsync(this IPpsRequest request, LuaTable arguments, Uri paneUri)
		{
			try
			{
				using (var r = await request.Request.GetResponseAsync(paneUri.ToString(), String.Join(";", MimeTypes.Application.Xaml, MimeTypes.Text.Lua, MimeTypes.Text.Plain)))
				{
					// read the file name
					arguments["_filename"] = r.GetContentDisposition().FileName.Trim('"');

					// check content
					var contentType = r.Content.Headers.ContentType;
					if (contentType.MediaType == MimeTypes.Application.Xaml) // load a xaml file
					{
						XDocument xamlContent;

						// parse the xaml as xml document
						using (var sr = await r.GetTextReaderAsync(MimeTypes.Application.Xaml))
						{
							using (var xml = XmlReader.Create(sr, Procs.XmlReaderSettings, paneUri.ToString()))
								xamlContent = await Task.Run(() => XDocument.Load(xml, LoadOptions.SetBaseUri | LoadOptions.SetLineInfo));
						}

						return xamlContent;
					}
					else if (contentType.MediaType == MimeTypes.Text.Lua
						|| contentType.MediaType == MimeTypes.Text.Plain) // load a code file
					{
						// load an compile the chunk
						using (var sr = await r.GetTextReaderAsync(null))
							return await request.Shell.CompileAsync(sr, paneUri.ToString(), true, new KeyValuePair<string, Type>("self", typeof(LuaTable)));
					}
					else
						throw new ArgumentException($"Expected: xaml/lua; received: {contentType.MediaType}");
				}
			}
			catch (Exception e)
			{
				throw new ArgumentException("Can not load pane definition.\n" + paneUri.ToString(), e);
			}
		} // func LoadPaneDataAsync

		#endregion

		/// <summary>Are the pane equal to the pane arguments.</summary>
		/// <param name="pane">Pane to compare with.</param>
		/// <param name="paneType">Type of the other pane.</param>
		/// <param name="arguments">Arguments of the other pane.</param>
		/// <returns><c>true</c>, if the pane arguments are the same.</returns>
		public static bool EqualPane(this IPpsWindowPane pane, Type paneType, LuaTable arguments)
			=> pane != null && paneType == pane.GetType() && pane.CompareArguments(arguments ?? new LuaTable()) == PpsWindowPaneCompareResult.Same;

		/// <summary></summary>
		/// <param name="pane"></param>
		/// <param name="text"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public static IPpsProgress DisableUI(this IPpsWindowPane pane, string text = null, int value = -1)
			=> DisableUI(pane.PaneHost, text, value);

		/// <summary></summary>
		/// <param name="pane"></param>
		/// <param name="text"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public static IPpsProgress DisableUI(this IPpsWindowPaneHost pane, string text = null, int value = -1)
		{
			var progress = pane.Progress.CreateProgress() ?? _PpsShell.EmptyProgress;
			if (text != null)
				progress.Text = text;
			progress.Value = value;
			return progress;
		} // func DisableUI

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

		private static Window GetWindowFromOwner(DependencyObject owner)
		{
			return owner == null
				? Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
				: Window.GetWindow(owner);
		} // func GetWindowFromOwner

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
				if (Application.Current.MainWindow != ownerWindow)
					throw new InvalidOperationException();
				Application.Current.MainWindow = oldWindow;
			}
		} // proc ShowModalDialog
	} // class PpsWindowPaneHelper

	#endregion
}
