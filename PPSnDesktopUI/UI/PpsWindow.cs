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
	/// <summary>Contains the generic layout of all windows of the the application.</summary>
	public partial class PpsWindow : Window
	{
		/// <summary>Command for minimizing the window.</summary>
		public readonly static RoutedCommand MinimizeCommand = new RoutedCommand("Minimize", typeof(PpsWindow));
		/// <summary>Command for maximizing the window.</summary>
		public readonly static RoutedCommand MaximizeCommand = new RoutedCommand("Maximize", typeof(PpsWindow));
		/// <summary>Command for closing the window.</summary>
		public readonly static RoutedCommand CloseCommand = new RoutedCommand("Close", typeof(PpsWindow));
		/// <summary>Starts the use login.</summary>
		public readonly static RoutedCommand LoginCommand = new RoutedCommand("Login", typeof(PpsWindow));

		private PpsEnvironment environment;

		public PpsEnvironment Environment
		{
			get
			{
				if (environment == null)
					environment = PpsEnvironment.GetEnvironment(this);
				return environment;
			}
		} // prop Environment
	} // class PpsWindow
}
