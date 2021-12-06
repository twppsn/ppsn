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
using System.Threading.Tasks;
using System.Windows.Threading;
using Neo.IronLua;
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

	#region -- class PaneUnloadedEventArgs --------------------------------------------

	/// <summary></summary>
	public class PaneUnloadedEventArgs : EventArgs
	{
		/// <summary></summary>
		/// <param name="pane"></param>
		public PaneUnloadedEventArgs(IPpsWindowPane pane)
			=> Pane = pane ?? throw new ArgumentNullException(nameof(pane));

		/// <summary></summary>
		public IPpsWindowPane Pane { get; }
	} // class PaneUnloadedEventArgs

	#endregion

	#region -- interface IPpsWindowPaneHost -------------------------------------------

	/// <summary>The host of the window pane.</summary>
	public interface IPpsWindowPaneHost : IServiceProvider
	{
		/// <summary>Is raised, when the pane of the host is unloaded.</summary>
		event EventHandler<PaneUnloadedEventArgs> PaneUnloaded;

		/// <summary>Close this pane.</summary>
		Task<bool> ClosePaneAsync();

		/// <summary>Is this pane host within the pane-manager active.</summary>
		bool IsActive { get; }
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

		/// <summary>Pane is activated</summary>
		void Activate();
		/// <summary>Pane is deactivated</summary>
		void Deactivate();

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

		/// <summary>Is back button active. <c>null</c> for default.</summary>
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

		/// <summary>Access the assigned shell.</summary>
		IPpsShell Shell { get; }

		/// <summary>Is this pane manager currenty the active pane manager.</summary>
		bool IsActive { get; }
	} // interface IPpsWindowPaneManager

	#endregion

	#region -- class PpsWindowPaneSettings --------------------------------------------

	/// <summary>Pane specific settings.</summary>
	public sealed class PpsWindowPaneSettings : PpsSettingsInfoBase
	{
		/// <summary></summary>
		/// <param name="settingsService"></param>
		public PpsWindowPaneSettings(IPpsSettingsService settingsService)
			: base(settingsService)
		{
		} // ctor

		/// <summary>Open pane mode</summary>
		public bool NewPaneMode => this.GetProperty("PPSn.Panes.NewPaneMode", true);
	} // class PpsWindowPaneSettings

	#endregion

	#region -- class PpsKnownWindowPanes ----------------------------------------------

	/// <summary>Registrar for pane types.</summary>
	public interface IPpsKnownWindowPanes
	{
		/// <summary></summary>
		/// <param name="paneName"></param>
		/// <param name="paneType"></param>
		/// <param name="mimeType"></param>
		void RegisterPaneType(Type paneType, string paneName, params string[] mimeType);

		/// <summary></summary>
		/// <param name="pane"></param>
		/// <param name="throwException"></param>
		/// <returns></returns>
		Type GetPaneType(string pane, bool throwException = false);
		/// <summary></summary>
		/// <param name="paneName"></param>
		/// <param name="throwException"></param>
		/// <returns></returns>
		Type GetPaneTypeFromName(string paneName, bool throwException = false);
		/// <summary></summary>
		/// <param name="mimeType"></param>
		/// <param name="throwException"></param>
		/// <returns></returns>
		Type GetPaneTypeMimeType(string mimeType, bool throwException = false);
	} // interface IPpsKnownWindowPanes

	#endregion

	#region -- class PpsKnownWindowPanes ----------------------------------------------

	[PpsService(typeof(IPpsKnownWindowPanes))]
	internal class PpsKnownWindowPanes : IPpsKnownWindowPanes, IPpsShellService
	{
		private readonly IPpsShell shell;

		private readonly List<Type> paneTypes = new List<Type>();
		private readonly Dictionary<string, int> namePaneTypes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		private readonly Dictionary<string, int> mimePaneTypes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

		public PpsKnownWindowPanes(IPpsShell shell)
		{
			this.shell = shell ?? throw new ArgumentNullException(nameof(shell));
		} // ctor

		private int RegisterPaneType(Type paneType)
		{
			lock (paneTypes)
			{
				var idx = paneTypes.IndexOf(paneType);
				if (idx == -1)
				{
					idx = paneTypes.Count;
					paneTypes.Add(paneType);
					return idx;
				}
				else
					return idx;
			}
		} // func RegisterPaneType

		private static Type ThrowPaneTypeException(string pane, bool throwException)
		{
			if (throwException)
				throw new ArgumentOutOfRangeException(String.Format("Could not resolve pane '{0}'.", pane), pane, nameof(pane));
			return null;
		} // func ThrowPaneTypeException

		void IPpsKnownWindowPanes.RegisterPaneType(Type paneType, string paneName, params string[] mimeTypes)
		{
			var idx = RegisterPaneType(paneType);

			if (!String.IsNullOrEmpty(paneName))
				namePaneTypes[paneName] = idx;

			foreach (var mimeType in mimeTypes)
			{
				if (!String.IsNullOrEmpty(mimeType))
					mimePaneTypes[mimeType] = idx;
			}
		} // proc RegisterPaneType

		Type IPpsKnownWindowPanes.GetPaneType(string pane, bool throwException)
		{
			if (namePaneTypes.TryGetValue(pane, out var idx))
				return paneTypes[idx];
			else if (mimePaneTypes.TryGetValue(pane, out idx))
				return paneTypes[idx];
			else
				return Type.GetType(pane, false) ?? LuaType.GetType(pane).Type ?? ThrowPaneTypeException(pane, throwException);
		} // func IPpsKnownWindowPanes.GetPaneType

		Type IPpsKnownWindowPanes.GetPaneTypeFromName(string paneName, bool throwException)
			=> namePaneTypes.TryGetValue(paneName, out var idx) ? paneTypes[idx] : ThrowPaneTypeException(paneName, throwException);

		Type IPpsKnownWindowPanes.GetPaneTypeMimeType(string mimeType, bool throwException)
			=> mimePaneTypes.TryGetValue(mimeType, out var idx) ? paneTypes[idx] : ThrowPaneTypeException(mimeType, throwException);

		public IPpsShell Shell => shell;
	} // class PpsKnownWindowPanes

	#endregion
}
