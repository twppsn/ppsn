using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TecWare.PPSn.UI
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Wird von allen Elementen implementiert, die als Inhalt
	/// angezeigt werden sollen.</summary>
	public interface IPpsWindowPane : INotifyPropertyChanged, IDisposable
	{
		/// <summary>Lädt den Inhalt des Panes</summary>
		/// <returns></returns>
		Task LoadAsync();
		/// <summary>Wird aufgerufen, wenn der Inhalt geschlossen werden soll.</summary>
		/// <returns></returns>
		Task<bool> UnloadAsync();

		/// <summary>Titel des Inhaltes</summary>
		string Title { get; }
		/// <summary>Wpf oder Win32Window, welches Angezeigt werden soll.</summary>
		object Control { get; }
	} // interface IPpsWindowPane
}
