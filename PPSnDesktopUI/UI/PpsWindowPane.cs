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
using System.Reflection;
using System.Threading.Tasks;
using Neo.IronLua;

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

	#region -- interface IPpsWindowPane -----------------------------------------------

	/// <summary>Implements the basic function of a pane for the window client 
	/// area.</summary>
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

		/// <summary>Control for the pane data.</summary>
		/// <remarks>Can not be implemented hidden, because of the binding.</remarks>
		object Control { get; }

		/// <summary>Access the pane manager.</summary>
		IPpsWindowPaneManager PaneManager { get; }
		/// <summary>Commands attached to the pane.</summary>
		PpsUICommandCollection Commands { get; }

		/// <summary>If the pane contains changes, this flag is <c>true</c>.</summary>
		bool IsDirty { get; }
	} // interface IPpsWindowPane

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

	#region -- enum PpsWellknownType --------------------------------------------------

	/// <summary></summary>
	public enum PpsWellknownType
	{
		/// <summary></summary>
		Generic,
		/// <summary></summary>
		Mask
	} // enum PpsWellknownType

	#endregion

	#region -- interface IPpsWindowPaneManager ----------------------------------------

	/// <summary>Interface to open new panes.</summary>
	public interface IPpsWindowPaneManager
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

		/// <summary></summary>
		/// <param name="wellknownType"></param>
		/// <returns></returns>
		Type GetPaneType(PpsWellknownType wellknownType);
		
		/// <summary>Pane enumeration.</summary>
		IEnumerable<IPpsWindowPane> Panes { get; }
		/// <summary>Access the environment.</summary>
		PpsEnvironment Environment { get; }
		/// <summary>Is this pane manager currenty the active pane manager.</summary>
		bool IsActive { get; }
	} // interface IPpsWindowPaneManager

	#endregion

	#region -- class PpsWindowPaneHelper ----------------------------------------------

	/// <summary>Extensions for the Pane, or PaneControl</summary>
	public static class PpsWindowPaneHelper
	{
		/// <summary>Initializes a empty pane, it can only be used by a pane manager.</summary>
		/// <param name="paneManager"></param>
		/// <param name="paneType"></param>
		/// <returns></returns>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public static IPpsWindowPane CreateEmptyPane(this IPpsWindowPaneManager paneManager, Type paneType)
		{
			var ti = paneType.GetTypeInfo();
			var ctorBest = (ConstructorInfo)null;
			var ctoreBestParamLength = -1;

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
				if (tiParam.IsAssignableFrom(typeof(PpsEnvironment)))
					paneArguments[i] = paneManager.Environment;
				else if (tiParam.IsAssignableFrom(typeof(IPpsWindowPaneManager)))
					paneArguments[i] = paneManager;
				else if (pi.HasDefaultValue)
					paneArguments[i] = pi.DefaultValue;
				else
					throw new ArgumentException($"Unsupported argument '{pi.Name}' for type '{ti.Name}'.");
			}

			// activate the pane
			return (IPpsWindowPane)Activator.CreateInstance(paneType, paneArguments);
		} // func CreateEmptyPane

		/// <summary></summary>
		/// <param name="pane"></param>
		/// <param name="paneType"></param>
		/// <param name="arguments"></param>
		/// <returns></returns>
		public static bool EqualPane(this IPpsWindowPane pane, Type paneType, LuaTable arguments)
			=> paneType == pane.GetType() && pane.CompareArguments(arguments ?? new LuaTable()) == PpsWindowPaneCompareResult.Same;


		/// <summary>Disable current ui.</summary>
		/// <param name="pane"></param>
		/// <returns></returns>
		public static IPpsProgress DisableUI(this IPpsWindowPane pane)
			=> null; // DisableUI(pane?.PaneControl);
	} // class PpsWindowPaneHelper

	#endregion
}
