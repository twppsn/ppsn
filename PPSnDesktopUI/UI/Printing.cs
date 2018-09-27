#region -- copyright --
//
// Licensed under the EUPL, Version 1.1 or - as soon they will be approved by the
// European Commission - subsequent versions of the EUPL(the "Licence"); You may
// not use this work except in compliance with the Licence.
//
// You may obtain a copy of the Licence at:
// http://ec.europa.eu/idabc/eupl
//
// Unless required by applicable law or agreed to in writing, software distributed
// under the Licence is distributed on an "AS IS" basis, WITHOUT WARRANTIES OR
// CONDITIONS OF ANY KIND, either express or implied. See the Licence for the
// specific language governing permissions and limitations under the Licence.
//
#endregion
using System;
using System.Drawing;
using System.Drawing.Printing;
using System.Printing;
using System.Windows.Controls;

namespace TecWare.PPSn.UI
{
	/// <summary></summary>
	public static class PrintingHelper
	{
		private sealed class PpsProgressPrintController : PrintController
		{
			private readonly PrintController controller;
			private readonly IPpsProgress progress;

			private string jobName;
			private int pageNumber;
			private int fromPage;
			private int pageCount;

			public PpsProgressPrintController(PrintController controller, IPpsProgress progress)
			{
				this.controller = controller;
				this.progress = progress;
			} // ctor

			public override void OnStartPrint(PrintDocument document, PrintEventArgs e)
			{
				base.OnStartPrint(document, e);

				fromPage = document.PrinterSettings.FromPage;
				pageCount = document.PrinterSettings.ToPage - document.PrinterSettings.FromPage;
				jobName = document.DocumentName;
				pageNumber = 0;

				UpdateProgress();

				controller.OnStartPrint(document, e);
			} // proc OnStartPrint

			public override Graphics OnStartPage(PrintDocument document, PrintPageEventArgs e)
			{
				base.OnStartPage(document, e);
				UpdateProgress();
				return controller.OnStartPage(document, e);
			} // func OnStartPage

			public override void OnEndPage(PrintDocument document, PrintPageEventArgs e)
			{
				controller.OnEndPage(document, e);
				pageNumber++;
				base.OnEndPage(document, e);
			} // proc OnEndPage

			public override void OnEndPrint(PrintDocument document, PrintEventArgs e)
			{
				controller.OnEndPrint(document, e);
				base.OnEndPrint(document, e);
			} // proc OnEndPrint

			private void UpdateProgress()
			{
				progress.Text = String.Format("Drucke '{0}' Seite {1} (letzte {2})", jobName, pageNumber + fromPage, pageCount + fromPage);
				progress.Value = pageNumber * 1000 / (pageCount + 1);
			} // proc UpdateProgress
		} // class PpsProgressPrintController

		private static Duplex ConvertDuplex(Duplexing duplexing)
		{
			switch (duplexing)
			{
				case Duplexing.OneSided:
					return Duplex.Simplex;
				case Duplexing.TwoSidedLongEdge:
					return Duplex.Horizontal;
				case Duplexing.TwoSidedShortEdge:
					return Duplex.Vertical;
				default:
					return Duplex.Simplex;
			}
		} // func ConvertDuply

		private static PrintRange ConvertPrintRange(PageRangeSelection pageRangeSelection)
		{
			switch(pageRangeSelection)
			{
				case PageRangeSelection.CurrentPage:
					return PrintRange.CurrentPage;
				case PageRangeSelection.SelectedPages:
					return PrintRange.Selection;
				case PageRangeSelection.UserPages:
					return PrintRange.SomePages;
				default:
					return PrintRange.AllPages;
			}
		} // func ConvertPrintRange

		/// <summary>Copy Wpf-PrintDialog settings to PrintDocument settings</summary>
		/// <param name="doc"></param>
		/// <param name="printDialog"></param>
		/// <param name="progress"></param>
		public static void CopyWpfToGDI(this PrintDocument doc, PrintDialog printDialog, IPpsProgress progress = null)
		{
			doc.PrintController = progress != null
				? (PrintController)new PpsProgressPrintController(new StandardPrintController(), progress)
				: (PrintController)new StandardPrintController();

			// translate printer setting to gdi+
			var wpfPrintTicket = printDialog.PrintTicket;
			var gdiPrinterSettings = doc.PrinterSettings;

			// basic settings
			gdiPrinterSettings.PrinterName = printDialog.PrintQueue.Name;
			gdiPrinterSettings.Collate = (wpfPrintTicket.Collation ?? System.Printing.Collation.Uncollated) == System.Printing.Collation.Collated;
			gdiPrinterSettings.Copies = (short)(wpfPrintTicket.CopyCount ?? 1);
			gdiPrinterSettings.Duplex = ConvertDuplex(wpfPrintTicket.Duplexing ?? System.Printing.Duplexing.Unknown);
			gdiPrinterSettings.PrintRange = ConvertPrintRange(printDialog.PageRangeSelection);
			gdiPrinterSettings.ToPage = printDialog.PageRange.PageTo;
			gdiPrinterSettings.FromPage = printDialog.PageRange.PageFrom;

			doc.DefaultPageSettings.Color = (wpfPrintTicket.OutputColor ?? OutputColor.Color) == OutputColor.Color;
			// todo:
		} // proc CopyWpfToGDI
	} // class PrintingHelper
}
