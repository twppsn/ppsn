using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace TecWare.PPSn.UI
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PpsWindowHitTest
	{
		public int HitTest { get; set; }
	} // class PpsWindowHitTest

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Contains the generic layout of all windows of the the application.</summary>
	public partial class PpsWindow : Window
	{
		/// <summary>Command for minimizing the window.</summary>
		public readonly static RoutedCommand MinimizeCommand = new RoutedCommand("Minimize", typeof(PpsWindow));
		/// <summary>Command for maximizing the window.</summary>
		public readonly static RoutedCommand MaximizeCommand = new RoutedCommand("Maximize", typeof(PpsWindow));
		/// <summary>Command for closing the window.</summary>
		public readonly static RoutedCommand CloseCommand = new RoutedCommand("Close", typeof(PpsWindow));

		public PpsWindow()
		{
			InitChrome();

			CommandBindings.AddRange(
				new CommandBinding[]
				{
					new CommandBinding(MinimizeCommand, (sender, e) => WindowState = WindowState.Minimized, (sender, e) => e.CanExecute = true),
					new CommandBinding(MaximizeCommand, (sender, e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized),
					new CommandBinding(CloseCommand, (sender, e) => Close())
				});
		} // ctor
	} // class PpsWindow
}
