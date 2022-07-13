using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using TecWare.PPSn.Data;
using TecWare.PPSn.UI;

namespace PPSnPdfTest
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();

			pdfViewer.Document = PdfReader.Open(@"M:\Daten\Projekte\PPSn\Dokumente\Techn\XRechnung\211-XRechnung-2021-07-29.pdf");
			
			//for (var i = 0; i < pdfViewer.Document.PageCount; i++)
			//{
			//	using (var p = pdfViewer.Document.OpenPage(i))
			//	{
			//		Debug.Print("PAGE[{0}]: {1}", i, p.Label);
			//		foreach (var c in p.Links)
			//			Debug.Print("- LINK: {0}, {1}", c.Destination?.PageNumber, c.Action.Type);
			//	}
			//}

			//foreach(var b in pdfViewer.Document.Bookmarks)
			//{
			//	Debug.Print("{0} => {1}", b.Title, b.Destination, b.Action);
			//}
			//pdfViewer.Document = PdfReader.Open(@"D:\Temp\test.pdf");
		}

		private async void Print_Click(object sender, RoutedEventArgs e)
		{
			var p = pdfViewer.Document.GetPrintDocument();
			var j = p.ShowDialog(this);
			await j.PrintAsync(this);
		}

		private void PpsTreeListView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
		{
			pdfViewer.GotoDestination(e.NewValue is PdfBookmark a ? a?.Destination : null);
		}
	}
}
