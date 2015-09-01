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

		/// <summary>If the pane contains changes, this flag is <c>true</c>.</summary>
		bool IsDirty { get; }
	} // interface IPpsWindowPane

	#endregion

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public interface IPpsWindowPane2 : IPpsWindowPane
	{
		/// <summary>Attached commands</summary>
		IEnumerable<System.Windows.UIElement> Commands { get; }
	} // interface IPpsWindowPane2
}
