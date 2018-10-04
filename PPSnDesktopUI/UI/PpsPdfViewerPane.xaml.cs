﻿#region -- copyright --
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
using System.Collections;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Neo.IronLua;
using TecWare.DE.Stuff;
using TecWare.PPSn.Controls;
using TecWare.PPSn.Data;

namespace TecWare.PPSn.UI
{
	/// <summary>Panel to view pdf-data.</summary>
	public partial class PpsPdfViewerPane : UserControl, IPpsWindowPane
	{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public static readonly DependencyProperty SubTitleProperty = DependencyProperty.Register(nameof(SubTitle), typeof(string), typeof(PpsPdfViewerPane), new FrameworkPropertyMetadata(String.Empty, new PropertyChangedCallback(OnSubTitleChanged)));
		private static readonly DependencyPropertyKey commandsPropertyKey = DependencyProperty.RegisterReadOnly(nameof(Commands), typeof(PpsUICommandCollection), typeof(PpsPdfViewerPane), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty CommandsProperty = commandsPropertyKey.DependencyProperty;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		private readonly IPpsWindowPaneManager paneManager;
		private readonly IPpsWindowPaneHost paneHost;

		private PdfReader loadedDocument = null;
		private IPpsObjectDataAccess objectDataAccess = null;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary>Pane constructor, that gets called by the host.</summary>
		public PpsPdfViewerPane(IPpsWindowPaneManager paneManager, IPpsWindowPaneHost paneHost)
		{
			this.paneManager = paneManager ?? throw new ArgumentNullException(nameof(paneManager));
			this.paneHost = paneHost ?? throw new ArgumentNullException(nameof(paneHost));

			var commands = new PpsUICommandCollection
			{
				AddLogicalChildHandler = AddLogicalChild,
				RemoveLogicalChildHandler = RemoveLogicalChild
			};
			SetValue(commandsPropertyKey, commands);

			InitializeComponent();

			// add commands
			commands.AddButton("100;100", "print", new PpsAsyncCommand(ctx => PrintAsync(ctx), ctx => CanPrint(ctx)), "Drucken", "Druckt die Pdf-Datei.");
		} // ctor

		/// <summary></summary>
		public void Dispose()
		{
		} // proc Dispose

		#endregion

		#region -- Load, Close --------------------------------------------------------

		private async Task OpenPdfAsync(object data)
		{
			switch (data)
			{
				case string fileName: // open a file from disk
					using (var bar = this.DisableUI(String.Format("Lade Pdf-Datei ({0})...", fileName)))
						SetLoadedDocument(await LoadDocumentFromFileNameAsync(fileName)); // parse pdf in background
					break;
				case IPpsObject obj: // open a internal object
					using (var bar = this.DisableUI(String.Format("Lade Pdf-Dokument ({0})...", obj.Nr)))
					{
						// create access
						var dataObject = await obj.GetDataAsync();
						objectDataAccess = await dataObject.AccessAsync();

						objectDataAccess.DisableUI = () => this.DisableUI("Pdf-Dokument wird bearbeitet...");
						objectDataAccess.DataChanged += async (sender, e) => await LoadDocumentFromObjectAsync();

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

		private async Task LoadDocumentFromObjectAsync()
		{
			if (objectDataAccess.ObjectData is IPpsBlobObjectData blobData)
			{
				var src = blobData.OpenStream(FileAccess.Read);
				try
				{
					if (src.CanRead && src.CanSeek) // use file stream
						SetLoadedDocument(await Task.Run(() => PdfReader.Open(src, objectDataAccess.ObjectData.Object.GetFileName())));
					else // cache data in a file stream
					{
						var bytes = await src.ReadInArrayAsync();
						SetLoadedDocument(await Task.Run(() => PdfReader.Open(bytes, name: objectDataAccess.ObjectData.Object.GetFileName())));
					}

					UpdateObject(objectDataAccess.ObjectData.Object);
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

			SubTitle = pdf.Name;
		} // proc SetLoadedDocument

		private void UpdateObject(IPpsObject obj)
		{
			// todo: bade code
			//var wnd = (PpsWindow)Window.GetWindow(this);
			var wnd = (PpsWindow)Application.Current.Windows.OfType<Window>().FirstOrDefault(c => c.IsActive);
			((dynamic)wnd).CharmObject = obj;

			// search for more:
			// ((dynamic)wnd).CharmObject = 
		} // proc UpdateObject

		private bool ClosePdf()
		{
			// clear document
			pdfViewer.Document = null;
			// close pdf
			loadedDocument?.Dispose();
			// close object access
			if (objectDataAccess != null)
			{
				objectDataAccess.Dispose();
				objectDataAccess = null;
			}
			return true;
		} // func ClosePdf

		#endregion

		#region -- Print --------------------------------------------------------------

		private bool inPrint = false;

		private bool CanPrint(PpsCommandContext ctx)
			=> pdfViewer.Document != null && !inPrint;

		private async Task PrintAsync(PpsCommandContext ctx)
		{
			var pdf = pdfViewer.Document;
			using (var doc = pdf.GetPrintDocument())
			{
				var printDialog = new PrintDialog
				{
					MinPage = 1,
					MaxPage = (uint)pdf.PageCount,
					PageRange = new PageRange(1, pdf.PageCount),
					CurrentPageEnabled = true,
					UserPageRangeEnabled = true
				};

				using (var bar = this.DisableUI("Drucken..."))
				{
					if (this.ShowModalDialog(printDialog.ShowDialog))
					{
						inPrint = true;
						try
						{
							if (printDialog.PageRangeSelection == PageRangeSelection.CurrentPage)
							{
								printDialog.PageRange = new PageRange(pdfViewer.CurrentPageNumber + 1);
								printDialog.PageRangeSelection = PageRangeSelection.UserPages;
							}
							
							doc.CopyWpfToGDI(printDialog, bar);
							await Task.Run(new Action(doc.Print));
						}
						finally
						{
							inPrint = false;
						}
					}
				}
			}
		} // proc PrintAsync

		#endregion

		#region -- IPpsWindowPane implementation --------------------------------------

		private event PropertyChangedEventHandler PropertyChanged;
		event PropertyChangedEventHandler INotifyPropertyChanged.PropertyChanged { add => PropertyChanged += value; remove => PropertyChanged -= value; }
		
		PpsWindowPaneCompareResult IPpsWindowPane.CompareArguments(LuaTable args)
		{
			return PpsWindowPaneCompareResult.Reload;
		} // func IPpsWindowPane.CompareArguments

		Task IPpsWindowPane.LoadAsync(LuaTable args)
		{
			ClosePdf();
			return OpenPdfAsync(args.GetMemberValue("Object") ?? args.GetMemberValue("FileName"));
		} // proc IPpsWindowPane.LoadAsync

		Task<bool> IPpsWindowPane.UnloadAsync(bool? commit)
			=> Task.FromResult(ClosePdf());

		string IPpsWindowPane.Title => "PDF-Viewer";

		bool IPpsWindowPane.HasSideBar => false;
		bool IPpsWindowPane.IsDirty => false;

		object IPpsWindowPane.Control => this;
		IPpsWindowPaneManager IPpsWindowPane.PaneManager => paneManager;
		IPpsWindowPaneHost IPpsWindowPane.PaneHost => paneHost;

		#endregion

		/// <summary>Extent logical child collection with commands</summary>
		protected override IEnumerator LogicalChildren
			=> Procs.CombineEnumerator(base.LogicalChildren, Commands?.GetEnumerator());

		private static void OnSubTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsPdfViewerPane)d).OnSubTitleChanged();

		private void OnSubTitleChanged()
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SubTitle)));

		/// <summary>Sub-title of the pdf data.</summary>
		public string SubTitle { get => (string)GetValue(SubTitleProperty); set => SetValue(SubTitleProperty, value); }
		/// <summary>Command bar.</summary>
		public PpsUICommandCollection Commands => (PpsUICommandCollection)GetValue(CommandsProperty);
	} // class PpsPdfViewerPane 
}