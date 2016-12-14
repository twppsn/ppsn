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
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using Neo.IronLua;

namespace TecWare.PPSn.UI
{
	#region -- enum PpsWindowPaneCompareResult ------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
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

	#region -- interface IPpsWindowPane -------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
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

		/// <summary>Content control</summary>
		/// <remarks>Can not be implemented hidden, because of the binding.</remarks>
		object Control { get; }
		/// <summary>Returns the optional pane control, if the hosted control is not a pane control, this property is <c>null</c>.</summary>
		IPpsPWindowPaneControl PaneControl { get; }

		/// <summary>If the pane contains changes, this flag is <c>true</c>.</summary>
		bool IsDirty { get; }

		/// <summary>has sidebar?</summary>
		bool HasSideBar { get; }

	} // interface IPpsWindowPane

	#endregion

	#region -- interface IPpsPWindowPaneControl -----------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Optional inferface for the control</summary>
	public interface IPpsPWindowPaneControl
	{
		/// <summary>Title of the pane control</summary>
		string Title { get; }

		/// <summary>Commands of the Pane-Controls</summary>
		PpsUICommandCollection Commands { get; }

		/// <summary>Optional progress stack</summary>
		PpsProgressStack ProgressStack { get; }

		/// <summary>has sidebar?</summary>
		bool HasSideBar { get; }
	} // interface IPpsPWindowPaneControl

	#endregion
}
