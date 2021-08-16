using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using TecWare.PPSn.Controls;
using TecWare.PPSn.Data;
using TecWare.PPSn.UI;

namespace TecWare.PPSn.Tests
{
	internal class TestItem
	{
		public int Nr { get; set; }
		public string Name { get; set; }
		public string Text { get; set; }
		public double Wert { get; set; }
	} // class TestItem

	[DisplayName("DataListBox")]
	public partial class DataListBoxPanel : UserControl
	{
		public DataListBoxPanel()
		{
			InitializeComponent();

			var items = new TestItem[100];
			var r = new Random();
			for (var i = 0; i < items.Length; i++)
				items[i] = new TestItem { Nr = i, Name = $"Eintrag {i}", Text = $"Todo for a nice text {i}", Wert = r.NextDouble() };

			listBox.ItemsSource = new PpsTypedListCollectionView<TestItem>(items);
			//listBox.ItemCommands.Add(new PpsUICommandButton { Description = "Item3", Command = new PpsCommand(CopyExecuted) });

			CommandBindings.Add(PpsCommandBase.CreateBinding(null, ApplicationCommands.Copy, new PpsCommand(CopyExecuted)));
			CommandBindings.Add(new CommandBinding(ApplicationCommands.Paste, FindExecuted));
			CommandBindings.Add(new CommandBinding(ApplicationCommands.Find, FindExecuted));
		}

		private void FindExecuted(object sender, ExecutedRoutedEventArgs e)
		{

		}

		private void CopyExecuted(PpsCommandContext ctx)
		{
		}

		private void Button_Click(object sender, RoutedEventArgs e)
		{
			var cols = (PpsListColumns)Resources["cols"];
			//cols.Columns[0].Header = "TTTTT";
		}
	}
}
