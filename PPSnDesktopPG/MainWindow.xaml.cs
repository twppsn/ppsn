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
using TecWare.DES.Stuff;
using TecWare.PPSn.UI;

namespace TecWare.PPSn.Playground
{
	/// <summary>
	/// Interaktionslogik für MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		private IPpsWindowPane pane;

		public MainWindow()
		{
			InitializeComponent();

			Task.Run(new Func<Task>(() => LoadMask(@"..\..\Example\MasterData.Contacts.xaml")))
				.ContinueWith(t =>
					{
						if (t.Exception != null)
							Dispatcher.Invoke(new Action<Exception>(RaiseException), t.Exception);
						else
						{
							Dispatcher.Invoke(RefreshView);
						}
					});
		} // ctor

		private async Task LoadMask(string sXamlFile)
		{
			pane = new PpsGenericMaskWindowPane(null);
			await pane.LoadAsync(Procs.CreateLuaTable(new KeyValuePair<string, object>("template", sXamlFile)));
		} // func LoadMask

		private void RefreshView()
		{
			this.Title = "PPSn Playground  - [" + pane.Title + "]";
			contentPresenter.Content = pane.Control;
		} // proc RefreshView

		private void RaiseException(Exception e)
		{
			if (e is AggregateException)
			{
				foreach (var inner in ((AggregateException)e).InnerExceptions)
					RaiseException(inner);
			}
			else
				MessageBox.Show(e.Message);
		} // proc RaiseException

		private void Button_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}
	}
}
