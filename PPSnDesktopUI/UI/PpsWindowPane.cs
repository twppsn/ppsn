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
	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Wird von allen Elementen implementiert, die als Inhalt
	/// angezeigt werden sollen.</summary>
	public interface IPpsWindowPane : INotifyPropertyChanged, IDisposable
	{
		/// <summary>Lädt den Inhalt des Panes</summary>
		/// <param name="args"></param>
		/// <returns></returns>
		Task LoadAsync(LuaTable args);
		/// <summary>Wird aufgerufen, wenn der Inhalt geschlossen werden soll.</summary>
		/// <returns></returns>
		Task<bool> UnloadAsync();

		/// <summary>Title of the content.</summary>
		string Title { get; }
		/// <summary>Content control</summary>
		object Control { get; }
	} // interface IPpsWindowPane

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public interface IPpsWindowPane2 : IPpsWindowPane
	{
		/// <summary>Attached commands</summary>
		IEnumerable<System.Windows.UIElement> Commands { get; }
	} // interface IPpsWindowPane2
}
