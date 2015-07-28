using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Neo.IronLua;

namespace TecWare.PPSn.UI
{
	#region -- interface IPpsWindowCommand ----------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>The interface is a contract of possible items. The members are
	/// retrieved dynamically</summary>
	public interface IPpsWindowCommand : INotifyPropertyChanged
	{
		/// <summary>Internal Name</summary>
		string Name { get; }
		/// <summary>Group to which the command belongs</summary>
		string Group { get; }
		/// <summary>Sort order key.</summary>
		int Order { get; }

		/// <summary>Is the button visible.</summary>
		bool IsVisible { get; }
	} // interface IPpsWindowCommand

	#endregion

	#region -- interface IPpsWindowCommandButton -----------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public interface IPpsWindowCommandButton : IPpsWindowCommand
	{
		/// <summary>Displayname.</summary>
		string DisplayName { get; }
		/// <summary>Description of then command. Shown as tooltip.</summary>
		object Description { get; }
		/// <summary>Image for the command</summary>
		object ImageSource { get; }

		/// <summary>Command implementation</summary>
		ICommand Command { get; }
	} // interface IPpsWindowCommandButton

	#endregion

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

		/// <summary>Titel des Inhaltes</summary>
		string Title { get; }
		/// <summary>Wpf oder Win32Window, welches Angezeigt werden soll.</summary>
		object Control { get; }

		/// <summary>Attached commands</summary>
		IEnumerable<IPpsWindowCommand> Commands { get; }
	} // interface IPpsWindowPane
}
