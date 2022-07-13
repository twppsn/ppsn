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
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Neo.IronLua;
using TecWare.DE.Networking;
using TecWare.DE.Stuff;
using TecWare.PPSn.Controls;
using TecWare.PPSn.Data;

namespace TecWare.PPSn.UI
{
	/// <summary>Panel to view pdf-data.</summary>
	public partial class PpsPdfViewerPane : PpsWindowPaneControl
	{
		private PdfReader loadedDocument = null;
		private IPpsDataInfo dataInfo = null;
		private IPpsDataObject dataAccess = null;
		private bool zoomScheduled = false;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary>Pane constructor, that gets called by the host.</summary>
		public PpsPdfViewerPane(IPpsWindowPaneHost paneHost)
			: base(paneHost)
		{
			InitializeComponent();

			if (paneHost.PaneManager.Shell.Settings.GetProperty("PPSn.Pdf.AllowPrint", true))
			{
				// add commands
				Commands.AddButton("100;100", "print", new PpsAsyncCommand(ctx => PrintAsync(ctx), ctx => CanPrint(ctx)), "Drucken", "Druckt die Pdf-Datei.");
			}
		} // ctor

		protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
		{
			base.OnRenderSizeChanged(sizeInfo);
			if (zoomScheduled)
			{
				zoomScheduled = false;
				pdfViewer.GotoPage(0, PdfGotoMode.ZoomXorY);
			}
		} // proc OnRenderSizeChanged

		#endregion

		#region -- Load, Close --------------------------------------------------------

		private async Task OpenPdfAsync(object data)
		{
			switch (data)
			{
				case string fileName: // open a file from disk
					using (var bar = this.CreateProgress(progressText: String.Format("Lade Pdf-Datei ({0})...", fileName)))
					{
						if (fileName.StartsWith("http://") || fileName.StartsWith("https://"))
							SetLoadedDocument(await DownloadDocumentAsync(PaneHost.PaneManager.Shell.Http, new Uri(fileName)));
						else
							SetLoadedDocument(await LoadDocumentFromFileNameAsync(fileName)); // parse pdf in background
					}
					break;
				case byte[] bytes:
					using (var bar = this.CreateProgress(progressText: "Lade Pdf-Datei..."))
						SetLoadedDocument(PdfReader.Open(bytes));
					break;
				case IPpsDataInfo info: // open a internal object
					using (var bar = this.CreateProgress(progressText: String.Format("Lade Pdf-Dokument ({0})...", info.Name)))
					{
						dataInfo = info;

						// create access
						dataAccess = await info.LoadAsync();
						
						dataAccess.DisableUI = () => this.CreateProgress(progressText: "Pdf-Dokument wird bearbeitet...");
						dataAccess.DataChanged += async (sender, e) => await LoadDocumentFromObjectAsync();

						await LoadDocumentFromObjectAsync();
					}
					break;
				case null:
					throw new ArgumentNullException(nameof(data));
				default:
					throw new ArgumentException($"Invalid pdf-data container {data.GetType().Name}.", nameof(data));
			}
		} // proc OpenPdfAsync

		private static Task<PdfReader> LoadDocumentFromFileNameAsync(string fileName)
			=> Task.Run(() => PdfReader.Open(fileName));

		internal static async Task<PdfReader> DownloadDocumentAsync(DEHttpClient http, Uri uri)
		{
			using (var r = await http.GetAsync(uri))
			{
				if (!r.IsSuccessStatusCode)
					throw new HttpResponseException(r);
				if (r.Content == null)
					throw new HttpResponseException(HttpStatusCode.NoContent);
				if (r.Content.Headers.ContentType?.MediaType != MimeTypes.Application.Pdf)
					throw new ArgumentOutOfRangeException("Content-Type", r.Content.Headers.ContentType?.MediaType, "Only pdf supported.");

				return PdfReader.Open(await r.Content.ReadAsByteArrayAsync(), name: r.Content.Headers.ContentDisposition?.FileName ?? "a.pdf");
			}
		} // func DownloadDocumentAsync

		private async Task LoadDocumentFromObjectAsync()
		{
			if (dataAccess.Data is IPpsDataStream stream)
			{
				var src = stream.OpenStream(FileAccess.Read);
				try
				{
					if (src.CanRead && src.CanSeek) // use file stream
						SetLoadedDocument(await Task.Run(() => PdfReader.Open(src, dataInfo.Name)));
					else // cache data in a file stream
					{
						var bytes = await src.ReadInArrayAsync();
						SetLoadedDocument(await Task.Run(() => PdfReader.Open(bytes, name: dataInfo.Name)));
					}

					NotifyWindowPanePropertyChanged(nameof(IPpsWindowPane.CurrentData));
				}
				catch
				{
					src.Dispose();
					throw;
				}
			}
			else
				throw new ArgumentNullException("data", "Data is not an blob.");
		} // func LoadDocumentFromObjectAsync

		private void SetLoadedDocument(PdfReader pdf)
		{
			// set new pdf
			loadedDocument = pdf;
			pdfViewer.Document = pdf;

			if (RenderSize.Width > 0)
				pdfViewer.GotoPage(0, PdfGotoMode.ZoomXorY);
			else
				zoomScheduled = true;

			SubTitle = pdf.Name;

			// check bookmarks
			UpdateBookmarks();
		} // proc SetLoadedDocument

		private bool ClosePdf()
		{
			// clear document
			pdfViewer.Document = null;
			// close pdf
			loadedDocument?.Dispose();
			loadedDocument = null;
			UpdateBookmarks();
			// close object access
			if (dataAccess != null)
			{
				dataAccess.Dispose();
				dataAccess = null;
			}
			dataInfo = null;
			return true;
		} // func ClosePdf

		#endregion

		#region -- Bookmarks ----------------------------------------------------------

		private bool canProcessSelectionEvent = true;

		private void UpdateBookmarks()
		{
			canProcessSelectionEvent = false;
			try
			{
			if (loadedDocument != null && loadedDocument.Bookmarks.Any())
			{
				bookmarkTree.ItemsSource = loadedDocument.Bookmarks;
				bookmarkTree.Visibility = Visibility.Visible;
			}
			else
			{
				bookmarkTree.Visibility = Visibility.Collapsed;
				bookmarkTree.ItemsSource = null;
			}
			}
			finally
			{
				canProcessSelectionEvent = true;
			}
		} // proc UpdateBookmarks

		private void bookmarkTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
		{
			if (canProcessSelectionEvent && e.NewValue is PdfBookmark bookmark)
				pdfViewer.GotoDestination(bookmark.Destination);
		} // event bookmarkTree_SelectedItemChanged

		#endregion

		#region -- Print --------------------------------------------------------------

		private bool inPrint = false;

		private bool CanPrint(PpsCommandContext _)
			=> pdfViewer.Document != null && !inPrint;

		private async Task PrintAsync(PpsCommandContext _)
		{
			inPrint = true;
			try
			{
				using (var doc = pdfViewer.GetPrintDocument())
				{
					var job = doc.ShowDialog(this);
					if (job != null)
						await job.PrintAsync(this);
				}
			}
			finally
			{
				inPrint = false;
			}
		} // proc PrintAsync

		#endregion

		#region -- WindowPane - implementation ----------------------------------------

		/// <summary>
		/// </summary>
		/// <param name="args"></param>
		/// <returns></returns>
		protected override PpsWindowPaneCompareResult CompareArguments(LuaTable args)
			=> dataInfo == (args["Object"] as IPpsDataInfo) ? PpsWindowPaneCompareResult.Same : PpsWindowPaneCompareResult.Reload;

		/// <summary></summary>
		/// <param name="args"></param>
		/// <returns></returns>
		protected override Task OnLoadAsync(LuaTable args)
		{
			ClosePdf();
			return OpenPdfAsync(args.GetMemberValue("Object") ?? args.GetMemberValue("FileName"));
		} // proc OnLoadAsync

		/// <summary></summary>
		/// <param name="commit"></param>
		/// <returns></returns>
		protected override Task<bool> OnUnloadAsync(bool? commit)
			=> Task.FromResult(ClosePdf());

		/// <summary></summary>
		protected override IPpsDataInfo CurrentData 
			=> dataInfo;

		#endregion
	} // class PpsPdfViewerPane 
}
