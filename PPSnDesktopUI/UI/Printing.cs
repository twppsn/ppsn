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
using System.Printing.Interop;
using System.Runtime.InteropServices;
using System.Windows.Controls;

namespace TecWare.PPSn.UI
{
	/// <summary></summary>
	public static class PrintingHelper
	{
		#region -- class PpsProgressPrintController -----------------------------------

		/// <summary>System.Drawing print controller, that notifies to IPpsProgress</summary>
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

		#endregion

		/// <summary>Copy print-dialog setting to a System.Drawing.</summary>
		/// <param name="printDialog"></param>
		/// <param name="printDocument"></param>
		/// <param name="progress"></param>
		public static void SetPrintDocument(this PrintDialog printDialog, PrintDocument printDocument, IPpsProgress progress = null)
		{
			var printerSettings = printDocument.PrinterSettings;

			printDocument.PrintController = progress != null
				? (PrintController)new PpsProgressPrintController(new StandardPrintController(), progress)
				: (PrintController)new StandardPrintController();

			// update device settings
			printerSettings.SetPrintTicket(printDialog.PrintQueue, printDialog.PrintTicket);

			switch (printDialog.PageRangeSelection)
			{
				case PageRangeSelection.AllPages:
					printerSettings.PrintRange = PrintRange.AllPages;
					break;
				case PageRangeSelection.UserPages:
					printerSettings.PrintRange = PrintRange.SomePages;
					printerSettings.FromPage = printDialog.PageRange.PageFrom;
					printerSettings.ToPage = printDialog.PageRange.PageTo;
					break;
				//case PageRangeSelection.CurrentPage:
				//case PageRangeSelection.SelectedPages:
				default:
					throw new NotSupportedException(nameof(PrintDialog.PageRangeSelection));
			}
		} // proc SetPrintDocument

		/// <summary>Copy printer settings from System.Printing to System.Drawing</summary>
		/// <param name="printerSettings"></param>
		/// <param name="printQueue"></param>
		/// <param name="printTicket"></param>
		public static void SetPrintTicket(this PrinterSettings printerSettings, PrintQueue printQueue, PrintTicket printTicket)
		{
			using (var printTicketConverter = new PrintTicketConverter(printQueue.Name, PrintTicketConverter.MaxPrintSchemaVersion))
			{
				printerSettings.PrinterName = printQueue.Name;

				var bDevMode = printTicketConverter.ConvertPrintTicketToDevMode(printTicket, BaseDevModeType.UserDefault, PrintTicketScope.JobScope);
				var pDevMode = Marshal.AllocHGlobal(bDevMode.Length);
				try
				{
					// copy settings
					Marshal.Copy(bDevMode, 0, pDevMode, bDevMode.Length);
					printerSettings.SetHdevmode(pDevMode);
					printerSettings.DefaultPageSettings.SetHdevmode(pDevMode);
				}
				finally
				{
					Marshal.FreeHGlobal(pDevMode);
				}
			}
		} // proc SetPrintTicket

		/// <summary>Copy printer settings from System.Drawing to System.Printing</summary>
		/// <param name="printerSettings"></param>
		/// <param name="printQueue"></param>
		/// <param name="printTicket"></param>
		public static unsafe void SetPrinterSettings(this PrinterSettings printerSettings, out PrintQueue printQueue, out PrintTicket printTicket)
		{
			using (var printTicketConverter = new PrintTicketConverter(printerSettings.PrinterName, PrintTicketConverter.MaxPrintSchemaVersion))
			using (var printServer = new LocalPrintServer())
			{
				printQueue = printServer.GetPrintQueue(printerSettings.PrinterName);

				var hDevMode = printerSettings.GetHdevmode();
				try
				{
					var pDevMode = NativeMethods.GlobalLock(hDevMode);
					var bDevMode = new byte[NativeMethods.GlobalSize(hDevMode).ToInt32()];
					Marshal.Copy(pDevMode, bDevMode, 0, bDevMode.Length);
					NativeMethods.GlobalUnlock(hDevMode);

					printTicket = printTicketConverter.ConvertDevModeToPrintTicket(bDevMode, PrintTicketScope.JobScope);
				}
				finally
				{
					Marshal.FreeHGlobal(hDevMode);
				}
			}
		} // proc SetPrinterSettings

		private delegate bool PrintDialogDelegate(IntPtr hwnd, string deviceName, IntPtr pDevModeIn, IntPtr pDevModeOut);

		private static unsafe PrintTicket ShowPrintPropertiesDialog(IntPtr hwnd, PrintQueue printQueue, PrintDialogDelegate dlg, PrintTicket printTicket)
		{
			using (var printTicketConverter = new PrintTicketConverter(printQueue.Name, PrintTicketConverter.MaxPrintSchemaVersion))
			{
				var bDevModeIn = printTicketConverter.ConvertPrintTicketToDevMode(printTicket, BaseDevModeType.UserDefault, PrintTicketScope.JobScope);
				var bDevModeOut = new byte[bDevModeIn.Length];
				fixed (byte* pDevModeOut = bDevModeOut, pDevModeIn = bDevModeIn)
				{
					if (dlg(hwnd, printQueue.Name, new IntPtr(pDevModeIn), new IntPtr(pDevModeOut)))
						return printTicketConverter.ConvertDevModeToPrintTicket(bDevModeOut, PrintTicketScope.JobScope);
					else
						return null;
				}
			}
		} // func ShowPrintPropertiesDialog

		private static bool ShowDocumentProperties(IntPtr hwnd, string deviceName, IntPtr pDevModeIn, IntPtr pDevModeOut)
		{
			// const uint DM_UPDATE = 1;
			const int DM_COPY = 2;
			const int DM_PROMPT = 4;
			const int DM_MODIFY = 8;
			return NativeMethods.DocumentProperties(hwnd, IntPtr.Zero, deviceName, pDevModeOut, pDevModeIn, DM_COPY | DM_PROMPT | DM_MODIFY) == 1;
		} // proc ShowDocumentProperties

		private static bool ShowAdvancedProperties(IntPtr hwnd, string deviceName, IntPtr pDevModeIn, IntPtr pDevModeOut)
			=> NativeMethods.AdvancedDocumentProperties(hwnd, IntPtr.Zero, deviceName, pDevModeOut, pDevModeIn) == 1;

		/// <summary>Show document properties dialog</summary>
		/// <param name="printQueue"></param>
		/// <param name="hwnd"></param>
		/// <param name="printTicket"></param>
		/// <returns></returns>
		public static PrintTicket ShowDocumentProperties(this PrintQueue printQueue, IntPtr hwnd, PrintTicket printTicket)
			=> ShowPrintPropertiesDialog(hwnd, printQueue, ShowDocumentProperties, printTicket);

		/// <summary>Show document advanved dialog</summary>
		/// <param name="printQueue"></param>
		/// <param name="hwnd"></param>
		/// <param name="printTicket"></param>
		public static void ShowAdvancedProperties(this PrintQueue printQueue, IntPtr hwnd, PrintTicket printTicket)
			=> ShowPrintPropertiesDialog(hwnd, printQueue, ShowAdvancedProperties, printTicket);
	} // class PrintingHelper
}
