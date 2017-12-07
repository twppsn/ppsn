using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace PPSnExcel.Wpf
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public partial class PpsTableImportWindow
	{
		public PpsTableImportWindow()
		{
			InitializeComponent();

			CommandBindings.Add(new CommandBinding(ApplicationCommands.Open,
				(sender, e) =>
				{
					DialogResult = true;
					Close();
				},
				(sender, e) =>
				{
					e.CanExecute = true;
				}
			));
			CommandBindings.Add(new CommandBinding(ApplicationCommands.Close, (sender, e) => Close()));
		} // ctor

		public string TableName { get; internal set; }
		public string TableSourceId { get; internal set; }
	} // class PpsTableImportWindow
}
