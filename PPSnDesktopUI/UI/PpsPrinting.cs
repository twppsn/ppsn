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
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Printing;
using System.Printing.Interop;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Xps;
using System.Xml.Linq;
using TecWare.DE.Networking;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;

namespace TecWare.PPSn.UI
{
	#region -- interface IPpsPrintRange -----------------------------------------------

	/// <summary>Selected Print-Range for a document.</summary>
	public interface IPpsPrintRange
	{
		/// <summary>Start at page</summary>
		int FromPage { get; }
		/// <summary>Stop at page</summary>
		int ToPage { get; }
	} // interface IPpsPrintRange

	#endregion

	#region -- class PpsPrintRange ----------------------------------------------------

	public sealed class PpsPrintRange : IPpsPrintRange
	{
		private readonly int fromPage;
		private readonly int toPage;

		public PpsPrintRange(int page)
			: this(page, page)
		{
		} // ctor

		public PpsPrintRange(int fromPage, int toPage)
		{
			this.fromPage = fromPage;
			this.toPage = toPage;
		} // ctor

		public int FromPage => fromPage;
		public int ToPage => toPage;
	} // class PpsPrintRange

	#endregion

	#region -- interface IPpsPrintDocument --------------------------------------------

	public interface IPpsPrintDocument : IDisposable
	{
		/// <summary>Returns the document combined with the range to print.</summary>
		/// <param name="settings"></param>
		/// <param name="printRange">Optional print range</param>
		/// <returns>Document to print. <c>DocumentPaginator</c> for xps print path OR <c>PrintDocument</c> for GDI print path.</returns>
		IPpsPrintJob GetPrintJob(IPpsPrintSettings settings, IPpsPrintRange printRange = null);

		/// <summary>First page number</summary>
		int? MinPage { get; }
		/// <summary>Last page number</summary>
		int? MaxPage { get; }
		/// <summary>Current selected page</summary>
		int? CurrentPage { get; }

		/// <summary>Optional print-key for this document.</summary>
		string PrintKey { get; }
	} // interface IPpsPrintDocument

	#endregion

	#region -- interface IPpsPrintSettings --------------------------------------------

	/// <summary>Persistable print settings</summary>
	public interface IPpsPrintSettings
	{
		/// <summary>Write settings</summary>
		/// <returns></returns>
		Task CommitAsync();

		/// <summary>Key for the settings.</summary>
		string Key { get; }

		/// <summary>Used print queue</summary>
		PrintQueue Queue { get; set; }
		/// <summary>Print ticket to use</summary>
		PrintTicket Ticket { get; set; }

		/// <summary>Is this the default ticket.</summary>
		bool IsDefault { get; }
		/// <summary>Is the ticet modified.</summary>
		bool IsModified { get; }
	} // interface IPpsPrintSettings

	#endregion

	#region -- class PpsPersistPrintSettings ------------------------------------------

	public abstract class PpsPrintSettings : IPpsPrintSettings
	{
		private PrintQueue queue;
		private PrintTicket ticket;
		private bool isDefault;
		private bool isModified = false;

		private PpsPrintSettings CheckInitialized()
		{
			if (queue == null
				|| ticket == null)
				throw new InvalidOperationException("Print settings not initialized.");
			return this;
		} // func CheckInitialized

		protected void SetDefault(bool isDefault)
			=> this.isDefault = isDefault;

		protected void SetChanged()
			=> isModified = true;

		protected void ResetChanged()
			=> isModified = false;

		private void Ticket_PropertyChanged(object sender, PropertyChangedEventArgs e)
			=> SetChanged();

		protected bool SetQueueCore(PrintQueue newQueue)
		{
			if (queue == newQueue)
				return false;

			queue = newQueue;
			return true;
		} // proc SetQueueCore

		protected async Task<bool> SetQueueByNameAsync(string queueName)
		{
			var printQueue = await PpsPrinting.GetPrintQueueAsync(queueName);
			SetQueueCore(printQueue ?? new LocalPrintServer().DefaultPrintQueue);
			return printQueue != null;
		} // func SetQueueByName

		protected bool SetTicketCore(PrintTicket newTicket)
		{
			if (newTicket == ticket)
				return false;

			var oldTicket = ticket;
			ticket = newTicket;

			if (oldTicket != null)
				oldTicket.PropertyChanged -= Ticket_PropertyChanged;
			if (newTicket != null)
				newTicket.PropertyChanged += Ticket_PropertyChanged;

			return true;
		} // proc SetTicket

		protected void InitDefault(PrintQueue printQueue = null, PrintTicket printTicket = null)
		{
			SetQueueCore(printQueue ?? LocalPrintServer.GetDefaultPrintQueue());
			SetTicketCore(printTicket ?? queue.DefaultPrintTicket);
			SetDefault(true);
		} // proc InitDefault

		public abstract Task CommitAsync();

		public abstract string Key { get; }

		public PrintQueue Queue
		{
			get => CheckInitialized().queue;
			set
			{
				CheckInitialized();

				if (SetQueueCore(value ?? LocalPrintServer.GetDefaultPrintQueue()))
				{
					if (SetTicketCore(queue.MergeAndValidatePrintTicket(ticket, null, PrintTicketScope.JobScope).ValidatedPrintTicket))
						SetChanged();
					else
						SetChanged();
				}
			}
		} // proc Queue

		public PrintTicket Ticket
		{
			get => CheckInitialized().ticket;
			set
			{
				CheckInitialized();

				if (ticket != value
					&& SetTicketCore(queue.MergeAndValidatePrintTicket(ticket, value, PrintTicketScope.JobScope).ValidatedPrintTicket))
					SetChanged();
			}
		} // prop Ticket

		public bool IsDefault => !isModified && isDefault;
		public bool IsModified => isModified;
	} // class PpsPersistPrintSettings

	#endregion

	#region -- class PpsPersistPrintSettings ------------------------------------------

	public abstract class PpsPersistPrintSettings : PpsPrintSettings
	{
		private readonly string printKey;

		protected PpsPersistPrintSettings(string printKey)
		{
			this.printKey = printKey;
		} // ctor

		private Task<XDocument> LoadAsync(Stream source)
			=> Task.Run(() => XDocument.Load(source));

		private async Task<XDocument> LoadCompressedAsync(Stream source)
		{
			using (var src = new GZipStream(source, CompressionMode.Decompress))
				return await LoadAsync(src);
		} // proc LoadCompressedAsync

		private Task SaveAsync(Stream destination, XDocument xDoc)
			=> Task.Run(() => xDoc.Save(destination));

		private async Task SaveCompressedAsync(Stream destination, XDocument xDoc)
		{
			using (var dst = new GZipStream(destination, CompressionLevel.Optimal))
				await SaveAsync(dst, xDoc);
		} // proc SaveCompressedAsync

		protected async Task LoadFromStreamAsync(Stream source, bool decompress)
		{
			var xDoc = await (decompress ? LoadCompressedAsync(source) : LoadAsync(source));
			var queueName = xDoc.Root.GetAttribute("queueName", null);

			var findQueue = await SetQueueByNameAsync(queueName);

			using (var m = new MemoryStream())
			{
				xDoc.Save(m);
				m.Position = 0;

				var newTicket = new PrintTicket(m);
				if (findQueue)
					SetTicketCore(newTicket);
				else
					Ticket = newTicket;
			}

			ResetChanged();
			SetDefault(false);
		} // proc LoadFromStreamAsync

		protected Task SaveToStreamAsync(Stream destination, bool compress)
		{
			var xDoc = XDocument.Load(Ticket.GetXmlStream());
			xDoc.Root.SetAttributeValue("queueName", Queue.FullName);

			ResetChanged();
			return compress ? SaveCompressedAsync(destination, xDoc) : SaveAsync(destination, xDoc);
		} // proc SaveToStreamAsync

		public sealed override string Key => printKey;
	} // class PpsPersistPrintSettings

	#endregion

	#region -- class PpsFilePrintSettings ---------------------------------------------

	/// <summary>File based printer settings.</summary>
	public sealed class PpsFilePrintSettings : PpsPersistPrintSettings
	{
		private readonly string fileName;

		private PpsFilePrintSettings(string fileName, string printKey)
			: base(printKey)
		{
			this.fileName = fileName;
		} // ctor

		private bool IsCompressed()
			=> Char.ToLowerInvariant(fileName[fileName.Length - 1]) == 'z';

		private static Stream OpenStream(string fileName, bool forWrite)
		{
			if (forWrite)
				return new FileStream(fileName, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
			else
				return new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
		} // func OpenStream

		private static async Task<Stream> OpenStreamAsync(string fileName, bool forWrite)
		{
			// check directory
			var fi = new FileInfo(fileName);
			if (forWrite)
			{
				if (!fi.Directory.Exists)
					fi.Directory.Create();
			}
			else
			{
				if (!fi.Exists)
					return null;
			}

			var retry = 3;
			while (true)
			{
				try
				{
					retry--;
					return OpenStream(fi.FullName, forWrite);
				}
				catch (IOException)
				{
					if (retry < 0)
						throw;
				}
				await Task.Delay(500);
			}
		} // func OpenStreamAsync

		private async Task<IPpsPrintSettings> LoadAsync()
		{
			using (var src = await OpenStreamAsync(fileName, false))
			{
				if (src != null)
					await LoadFromStreamAsync(src, IsCompressed());
				else
					InitDefault();
			}
			return this;
		} // proc LoadAsync

		public override async Task CommitAsync()
		{
			if (!IsModified)
				return;

			using (var dst = await OpenStreamAsync(fileName, true))
				await SaveToStreamAsync(dst, IsCompressed());
		} // proc CommitAsync

		public static Task<IPpsPrintSettings> LoadAsync(string fileName, string printKey = null)
		{
			if (String.IsNullOrEmpty(fileName))
				throw new ArgumentNullException(nameof(fileName));

			return new PpsFilePrintSettings(fileName, printKey ?? GetKeyFromFileName(fileName)).LoadAsync();
		} // func LoadAsync

		private static string GetExtension(bool compressed)
			=> compressed ? ".prnz" : ".prn";

		/// <summary>Create a filename for the print key.</summary>
		/// <param name="printKey"></param>
		/// <param name="compressed"></param>
		/// <returns></returns>
		public static string GetNameFromKey(string printKey, bool compressed = true)
		{
			if (String.IsNullOrEmpty(printKey)) // default
				return String.Empty + GetExtension(compressed);
			else
				return printKey + GetExtension(compressed);
		} // func GetNameFromKey

		/// <summary>Get the print key from an filename</summary>
		/// <param name="fileName"></param>
		/// <returns></returns>
		public static string GetKeyFromFileName(string fileName)
		{
			var name = Path.GetFileName(fileName);
			var p = name.LastIndexOf('.');
			if (p == -1)
				throw new FormatException("Invalid format.");

			return p > 0 ? name.Substring(0, p) : null; // default
		} // func GetKeyFromFileName
	} // class PpsFilePrintSettings

	#endregion

	#region -- interface IPpsPrintJob -------------------------------------------------

	/// <summary>Print job, ready to print.</summary>
	public interface IPpsPrintJob
	{
		/// <summary>Print job on queue.</summary>
		/// <param name="progress"></param>
		/// <returns></returns>
		Task PrintAsync(IPpsProgress progress = null);

		string Name { get; }
	} // interface IPpsPrintJob

	#endregion

	#region -- class PpsPrintService --------------------------------------------------

	/// <summary>Shell service to manage printer settings.</summary>
	[PpsLazyService, PpsService(typeof(PpsPrintService))]
	public class PpsPrintService : IPpsShellService
	{
		private readonly IPpsShell shell;

		public PpsPrintService(IPpsShell shell)
		{
			this.shell = shell ?? throw new ArgumentNullException(nameof(shell));
		} // ctor

		private string GetPrinterSettingsFileName(string printKey)
			=> Path.Combine(shell.LocalUserPath.FullName, "PrintSettings", PpsFilePrintSettings.GetNameFromKey(printKey));

		/// <summary>Get printer settings.</summary>
		/// <param name="printKey"></param>
		/// <returns></returns>
		public Task<IPpsPrintSettings> GetPrintSettingsAsync(string printKey)
			=> PpsFilePrintSettings.LoadAsync(GetPrinterSettingsFileName(printKey));

		/// <summary></summary>
		public IPpsShell Shell => shell;
	} // interface IPpsPrintService

	#endregion

	#region -- class PpsDrawingPrintDocument ------------------------------------------

	public class PpsDrawingPrintDocument : IPpsPrintDocument
	{
		#region -- class PpsDrawingPrintController ------------------------------------

		/// <summary>System.Drawing print controller, that notifies to IPpsProgress</summary>
		private sealed class PpsDrawingPrintController : PrintController
		{
			private readonly PrintController controller;
			private readonly IPpsProgress progress;

			private string jobName;
			private int pageNumber;
			private int fromPage;
			private int pageCount;

			public PpsDrawingPrintController(PrintController controller, IPpsProgress progress)
			{
				this.controller = controller ?? throw new ArgumentNullException(nameof(controller));
				this.progress = progress ?? throw new ArgumentNullException(nameof(progress));
			} // ctor

			public override void OnStartPrint(PrintDocument document, PrintEventArgs e)
			{
				base.OnStartPrint(document, e);

				switch (document.PrinterSettings.PrintRange)
				{
					case PrintRange.AllPages:
						fromPage = 0;
						pageCount = document.PrinterSettings.MaximumPage - document.PrinterSettings.MinimumPage + 1;
						break;

					default:
					case PrintRange.SomePages:
						fromPage = document.PrinterSettings.FromPage;
						pageCount = document.PrinterSettings.ToPage - document.PrinterSettings.FromPage + 1;
						break;
				}

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
				progress.Text = String.Format("Drucke '{0}' Seite {1} (letzte {2})", jobName, pageNumber + fromPage + 1, fromPage + pageCount);
				progress.Value = pageNumber * 1000 / (pageCount + 1);
			} // proc UpdateProgress
		} // class PpsDrawingPrintController

		#endregion

		#region -- class PpsDrawingPrintJob -------------------------------------------

		private sealed class PpsDrawingPrintJob : IPpsPrintJob
		{
			private readonly PrintDocument printDocument;

			public PpsDrawingPrintJob(PpsDrawingPrintDocument document, IPpsPrintSettings settings, IPpsPrintRange range)
			{
				if (document == null)
					throw new ArgumentNullException(nameof(document));
				if (settings == null)
					throw new ArgumentNullException(nameof(settings));

				printDocument = document.document;
				var printerSettings = printDocument.PrinterSettings;

				// update device settings
				printerSettings.SetFromSystemPrinting(settings.Queue, settings.Ticket);

				// update range
				if (range == null)
					printerSettings.PrintRange = PrintRange.AllPages;
				else
				{
					printerSettings.PrintRange = PrintRange.SomePages;
					printerSettings.FromPage = range.FromPage;
					printerSettings.ToPage = range.ToPage;

				}
			} // ctor

			public Task PrintAsync(IPpsProgress progress = null)
			{
				printDocument.PrintController = progress != null
					? (PrintController)new PpsDrawingPrintController(new StandardPrintController(), progress)
					: new StandardPrintController();

				return Task.Run(new Action(printDocument.Print));
			} // proc PrintAsync

			public string Name => printDocument.DocumentName;
		} // class PpsDrawingPrintJob

		#endregion

		private readonly PrintDocument document;
		private readonly string printKey;

		public PpsDrawingPrintDocument(PrintDocument document, string printKey)
		{
			this.document = document ?? throw new ArgumentNullException(nameof(document));
			this.printKey = printKey;
		} // ctor

		/// <inherited/>
		public void Dispose()
		{
			GC.SuppressFinalize(this);
			Dispose(true);
		} // proc Dispose

		protected virtual void Dispose(bool disposing)
		{
			if (disposing)
				document.Dispose();
		} // proc Dispose

		/// <inherited/>
		public IPpsPrintJob GetPrintJob(IPpsPrintSettings settings, IPpsPrintRange printRange = null)
			=> new PpsDrawingPrintJob(this, settings ?? PpsPrinting.GetDefaultPrintSettings(), printRange);

		/// <inherited/>
		public virtual int? MinPage
		{
			get => document.PrinterSettings.MinimumPage;
			set => document.PrinterSettings.MinimumPage = value ?? 0;
		} // prop MinPage

		/// <inherited/>
		public virtual int? MaxPage
		{
			get => document.PrinterSettings.MaximumPage;
			set => document.PrinterSettings.MaximumPage = value ?? 9999;
		} // prop MaxPage

		/// <inherited/>
		public virtual int? CurrentPage => null;

		/// <inherited/>
		public virtual string PrintKey => printKey;
	} // class PpsDrawingPrintDocument

	#endregion

	#region -- class PpsWpfPrintDocument ----------------------------------------------

	public abstract class PpsWpfPrintDocument : IPpsPrintDocument
	{
		#region -- class WpfPrintJob --------------------------------------------------

		private sealed class WpfPrintJob : IPpsPrintJob
		{
			private readonly PpsWpfPrintDocument document;
			private readonly PrintQueue printQueue;
			private readonly PrintTicket printTicket;
			private readonly IPpsPrintRange printRange;

			public WpfPrintJob(PpsWpfPrintDocument document, PrintQueue printQueue, PrintTicket printTicket, IPpsPrintRange printRange)
			{
				this.document = document ?? throw new ArgumentNullException(nameof(document));
				this.printQueue = printQueue ?? throw new ArgumentNullException(nameof(printQueue));
				this.printTicket = printTicket ?? throw new ArgumentNullException(nameof(printTicket));
				this.printRange = printRange;
			} // ctor

			public Task PrintAsync(IPpsProgress progress = null)
			{
				var taskSource = new TaskCompletionSource<bool>();
				var xps = PrintQueue.CreateXpsDocumentWriter(printQueue);

				xps.WritingCancelled += (sender, e) => taskSource.SetCanceled();
				xps.WritingCompleted += (sender, e) => taskSource.SetResult(true);
				printQueue.CurrentJobSettings.Description = document.Name;

				document.WriteDocument(xps, printTicket, printRange);
				return taskSource.Task;
			} // proc PrintAsync

			public string Name => document.Name;
		} // class WpfPrintJob

		#endregion

		protected readonly string name;
		private readonly string printKey;

		protected PpsWpfPrintDocument(string printKey, string name)
		{
			this.printKey = printKey;
			this.name = name ?? throw new ArgumentNullException(nameof(name));
		} // ctor

		public void Dispose()
		{
			GC.SuppressFinalize(this);
			Dispose(true);
		} // proc Dispose

		protected virtual void Dispose(bool disposing) { }

		protected abstract void WriteDocument(XpsDocumentWriter xps, PrintTicket printTicket, IPpsPrintRange printRange);

		public IPpsPrintJob GetPrintJob(IPpsPrintSettings settings, IPpsPrintRange printRange = null)
			=> new WpfPrintJob(this, settings.Queue, settings.Ticket, printRange);

		protected string Name => name;

		public abstract int? MinPage { get; }
		public abstract int? MaxPage { get; }
		public abstract int? CurrentPage { get; }

		public string PrintKey => printKey;
	} // class PpsWpfPrintDocument


	#endregion

	#region -- class PpsPrinting ------------------------------------------------------

	/// <summary>Helper to print documents.</summary>
	public static class PpsPrinting
	{
		#region -- Convert Printer Settings -------------------------------------------

		/// <summary>Copy printer settings from System.Printing to System.Drawing</summary>
		/// <param name="printerSettings"></param>
		/// <param name="printQueue"></param>
		/// <param name="printTicket"></param>
		public static void SetFromSystemPrinting(this PrinterSettings printerSettings, PrintQueue printQueue, PrintTicket printTicket)
		{
			using (var printTicketConverter = new PrintTicketConverter(printQueue.Name, PrintTicketConverter.MaxPrintSchemaVersion))
			{
				printerSettings.PrinterName = printQueue.FullName;

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
		} // proc SetFromSystemPrint

		private static PrintServer GetPrintServer(string printServerPath, bool throwException = false)
		{
			try
			{
				return new PrintServer(printServerPath);
			}
			catch (PrintServerException)
			{
				if (throwException)
					throw;
				return null;
			}
		} // func GetPrintServer

		/// <summary>Get the print server, without an exception.</summary>
		/// <param name="printServerPath">Path to the print server.</param>
		/// <param name="throwException"><c>true</c>, if the server is not found. A exception will be thrown.</param>
		/// <returns><see cref="PrintServer"/> or <c>null</c></returns>
		public static Task<PrintServer> GetPrintServerAsync(string printServerPath, bool throwException = false)
			=> Task.Run(() => GetPrintServer(printServerPath, throwException));

		/// <summary>Get a print queue for the printer name.</summary>
		/// <param name="printServer">Print server to search.</param>
		/// <param name="queueName">Local printer name</param>
		/// <param name="throwException"><c>true</c>, if the queue is not found. A exception will be thrown.</param>
		/// <returns><see cref="PrintQueue"/> or <c>null</c></returns>
		public static PrintQueue GetPrintQueue(PrintServer printServer, string queueName, bool throwException = false)
		{
			var queue = printServer?.GetPrintQueues().FirstOrDefault(c => String.Compare(c.Name, queueName, StringComparison.OrdinalIgnoreCase) == 0);
			if (queue == null && throwException)
				throw new FileNotFoundException(String.Format($"Printer {0} on {1} not found.", queueName, printServer?.Name));
			return queue;
		} // func GetPrintQueue

		/// <summary>Get a print queue from a full printer name.</summary>
		/// <param name="queueName">Full printer name, or a local printer name.</param>
		/// <param name="throwException"><c>true</c>, if the queue is not found. A exception will be thrown.</param>
		/// <returns><see cref="PrintQueue"/> or <c>null</c></returns>
		public static async Task<PrintQueue> GetPrintQueueAsync(string queueName, bool throwException = false)
		{
			if (queueName.Length > 1 && queueName[0] == '\\' && queueName[1] == '\\') // network printer
			{
				var endAt = queueName.IndexOf('\\', 2);
				if (endAt == -1)
					return GetPrintQueue(new LocalPrintServer(), queueName, throwException);
				else
				{
					var printServer = await GetPrintServerAsync(queueName.Substring(0, endAt), throwException);
					return GetPrintQueue(printServer, queueName.Substring(endAt + 1), throwException);
				}
			}
			else
				return GetPrintQueue(new LocalPrintServer(), queueName, throwException);
		} // func GetPrintQueueAsync

		/// <summary>Copy printer settings from System.Drawing to System.Printing</summary>
		/// <param name="printerSettings"></param>
		/// <param name="printQueue"></param>
		/// <param name="printTicket"></param>
		public static unsafe void SetFromSystemDrawing(this PrinterSettings printerSettings, out PrintQueue printQueue, out PrintTicket printTicket)
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
		} // proc SetFromSystemDrawing

		#endregion

		#region -- CreatePrintDocument ------------------------------------------------

		#region -- class SinglePageDocument -------------------------------------------

		private sealed class SinglePageDocument : PpsWpfPrintDocument
		{
			private readonly object page;

			public SinglePageDocument(object page, string printKey, string name)
				: base(printKey, name)
			{
				this.page = page ?? throw new ArgumentNullException(nameof(page));
			} // ctor

			protected override void WriteDocument(XpsDocumentWriter xps, PrintTicket printTicket, IPpsPrintRange printRange)
			{
				if (page is Visual visual)
					xps.WriteAsync(visual, printTicket);
				else if (page is FixedPage fixedPage)
					xps.WriteAsync(fixedPage, printTicket);
				else
					throw new ArgumentOutOfRangeException(nameof(page), page.GetType(), "Type is not supported.");
			} // proc WriteDocument

			public override int? MinPage => null;
			public override int? MaxPage => null;
			public override int? CurrentPage => null;
		} // class SinglePageDocument

		#endregion

		#region -- class PaginatorDocument --------------------------------------------

		private sealed class PaginatorDocument : PpsWpfPrintDocument
		{
			private readonly DocumentPaginator paginator;

			public PaginatorDocument(DocumentPaginator paginator, string printKey, string name)
				:base(printKey, name)
			{
				this.paginator = paginator ?? throw new ArgumentNullException(nameof(paginator));
			} // ctor

			protected override void WriteDocument(XpsDocumentWriter xps, PrintTicket printTicket, IPpsPrintRange printRange)
			{
				if (printRange != null)
				{
					// todo: paginator to respect range
				}
				xps.WriteAsync(paginator, printTicket);
			} // proc WriteDocument

			public override int? MinPage => paginator.IsPageCountValid ?  1 : (int?)null;
			public override int? MaxPage => paginator.IsPageCountValid ? paginator.PageCount : (int?)null;
			public override int? CurrentPage => null;
		} // class PaginatorDocument

		#endregion

		#region -- class PpsWebDocument -----------------------------------------------

		private sealed class PpsWebDocument : IPpsPrintDocument
		{
			private PdfReader pdf = null;
			private IPpsPrintDocument document = null;

			public async Task<IPpsPrintDocument> LoadAsync(DEHttpClient http, Uri uri)
			{
				pdf = await PpsPdfViewerPane.DownloadDocumentAsync(http, uri);
				document = pdf.GetPrintDocument();
				return this;
			} // func LoadAsync

			public void Dispose()
			{
				document?.Dispose();
				pdf?.Dispose();
			} // proc Dispose

			public IPpsPrintJob GetPrintJob(IPpsPrintSettings settings, IPpsPrintRange printRange = null)
				=> document.GetPrintJob(settings, printRange);

			public int? MinPage => document.MinPage;
			public int? MaxPage => document.MaxPage;
			public int? CurrentPage => null;

			public string PrintKey => document.PrintKey;
		} // class PpsWebDocument

		#endregion

		/// <summary>Create a System.Drawing.PrintDocument path.</summary>
		/// <param name="printDocument"></param>
		/// <returns></returns>
		public static IPpsPrintDocument CreatePrintDocument(this PrintDocument printDocument, string printKey = null)
			=> new PpsDrawingPrintDocument(printDocument, printKey);

		public static IPpsPrintDocument CreatePrintDocument(this Visual visual, string printKey = null, string name = null)
			=> new SinglePageDocument(visual, printKey, name ?? visual.GetName() ?? "visual");

		public static IPpsPrintDocument CreatePrintDocument(this FixedPage fixedPage, string printKey = null, string name = null)
			=> new SinglePageDocument(fixedPage, printKey, name ?? fixedPage.Name ?? "page");

		public static IPpsPrintDocument CreatePrintDocument(this FixedDocument fixedDocument, string printKey = null, string name = null)
			=> new PaginatorDocument(fixedDocument.DocumentPaginator, printKey, name ?? fixedDocument.Name ?? "document");

		public static IPpsPrintDocument CreatePrintDocument(this FixedDocumentSequence fixedDocumentSequence, string printKey = null, string name = null)
			=> new PaginatorDocument(fixedDocumentSequence.DocumentPaginator, printKey, name ?? fixedDocumentSequence.Name ?? "document");

		public static IPpsPrintDocument CreatePrintDocument(this DocumentPaginator documentPaginator, string printKey = null, string name = null)
			=> new PaginatorDocument(documentPaginator, printKey, name ?? "pages");

		public static Task<IPpsPrintDocument> CreatePrintDocumentAsync(this DEHttpClient http, Uri uri)
			=> new PpsWebDocument().LoadAsync(http, uri);

		#endregion

		#region -- Print Job ----------------------------------------------------------

		/// <summary>Print</summary>
		/// <param name="job"></param>
		/// <param name="dp"></param>
		/// <returns></returns>
		public static async Task PrintAsync(this IPpsPrintJob job, DependencyObject dp)
		{
			using (var progress = dp.CreateProgress(progressText: String.Format("Drucke {0}...", job.Name)))
				await job.PrintAsync(progress);
		} // func PrintAsync

		/// <summary>Create print job</summary>
		/// <param name="document"></param>
		/// <param name="printService"></param>
		/// <param name="printRange"></param>
		/// <returns></returns>
		public static async Task<IPpsPrintJob> GetPrintJobAsync(this IPpsPrintDocument document, PpsPrintService printService, IPpsPrintRange printRange = null)
		{
			if (document == null)
				throw new ArgumentNullException(nameof(document));
			if (printService == null)
				throw new ArgumentNullException(nameof(printService));

			var settings = await printService.GetPrintSettingsAsync(document.PrintKey);
			return document.GetPrintJob(settings, printRange);
		} // func GetPrintJob

		/// <summary>Create print job</summary>
		/// <param name="document"></param>
		/// <param name="dp"></param>
		/// <param name="printRange"></param>
		/// <returns></returns>
		public static Task<IPpsPrintJob> GetPrintJobAsync(this IPpsPrintDocument document, DependencyObject dp, IPpsPrintRange printRange = null)
		{
			if (document == null)
				throw new ArgumentNullException(nameof(document));
			if (dp == null)
				throw new ArgumentNullException(nameof(dp));

			var printService = dp.GetControlService<PpsPrintService>(false);
			return printService == null
				? Task.FromResult(document.GetPrintJob(GetDefaultPrintSettings(), printRange))
				: GetPrintJobAsync(document, printService, printRange);
		} // func GetPrintJob

		#endregion

		#region -- PrintDialog --------------------------------------------------------

		/// <summary>Show print dialog for this document.</summary>
		/// <param name="document">Document for this dialog.</param>
		/// <param name="dp">Service provider.</param>
		/// <param name="settings">Settings that will be updated by the dialog. If there no settings, a temporary settings set will be generated.</param>
		/// <returns></returns>
		public static IPpsPrintJob ShowDialog(this IPpsPrintDocument document, DependencyObject dp, IPpsPrintSettings settings = null)
		{
			if (document == null)
				throw new ArgumentNullException(nameof(document));
			if (dp == null)
				throw new ArgumentNullException(nameof(dp));

			var printDialog = new PrintDialog
			{
				MinPage = (uint)(document.MinPage ?? 1),
				MaxPage = (uint)(document.MaxPage ?? 9999),
				PageRange = document.ToPageRange(),
				CurrentPageEnabled = document.CurrentPage.HasValue,
				UserPageRangeEnabled = document.HasRange(),
			};

			if (settings != null)
			{
				printDialog.PrintQueue = settings.Queue;
				printDialog.PrintTicket = settings.Ticket;
			}

			// show dialog
			if (!dp.ShowModalDialog(printDialog.ShowDialog))
				return null;

			if (settings != null)
			{
				settings.Queue = printDialog.PrintQueue;
				settings.Ticket = printDialog.PrintTicket;
			}

			// return job-info
			return document.GetPrintJob(
				settings ?? GetPrintSettings(printDialog.PrintQueue, printDialog.PrintTicket), 
				printDialog.ToPrintRange(document)
			);
		} // proc ShowDialog

		/// <summary>Show print dialog an manage settings.</summary>
		/// <param name="document"></param>
		/// <param name="dp"></param>
		/// <param name="printService"></param>
		/// <returns></returns>
		public static async Task<IPpsPrintJob> ShowDialogAsync(this IPpsPrintDocument document, DependencyObject dp, PpsPrintService printService)
		{
			if (document == null)
				throw new ArgumentNullException(nameof(document));
			if (dp == null)
				throw new ArgumentNullException(nameof(dp));
			if (printService == null)
				throw new ArgumentNullException(nameof(printService));

			// get settings
			var settings = await printService.GetPrintSettingsAsync(document.PrintKey);

			var job = ShowDialog(document, dp, settings);
			if (job == null)
				return null;

			if (settings != null)
				await settings.CommitAsync();

			return job;
		} // func ShowDialogAsync

		/// <summary>Show print dialog and manage settings, if possible.</summary>
		/// <param name="document"></param>
		/// <param name="dp"></param>
		/// <returns></returns>
		public static Task<IPpsPrintJob> ShowDialogAsync(this IPpsPrintDocument document, DependencyObject dp)
		{
			if (document == null)
				throw new ArgumentNullException(nameof(document));
			if (dp == null)
				throw new ArgumentNullException(nameof(dp));

			var printService = dp.GetControlService<PpsPrintService>(false);
			return printService == null
				? Task.FromResult(ShowDialog(document, dp))
				: ShowDialogAsync(document, dp, printService);
		} // func ShowDialogAsync

		/// <summary>Print document with dialog.</summary>
		/// <param name="document">Document to print.</param>
		/// <param name="dp">Service provider</param>
		/// <param name="enforceShowDialog"><c>true</c>, always show the print dialog.</param>
		/// <param name="settings">Optional settings, that will be updated or changed.</param>
		/// <returns></returns>
		public static async Task<bool> PrintDialogAsync(this IPpsPrintDocument document, DependencyObject dp, bool enforceShowDialog = true, IPpsPrintSettings settings = null)
		{
			if (document == null)
				throw new ArgumentNullException(nameof(document));
			if (dp == null)
				throw new ArgumentNullException(nameof(dp));

			// load settings
			var printService = dp.GetControlService<PpsPrintService>(false);
			if (printService != null && settings == null)
				settings = await printService.GetPrintSettingsAsync(document.PrintKey);

			// show dialog
			var job = enforceShowDialog || settings == null || settings.IsDefault || ToggleProperties
				? ShowDialog(document, dp, settings)
				: document.GetPrintJob(settings);
			if (job == null)
				return false;

			using (var progress = dp.CreateProgress(progressText: $"Drucke {job.Name}..."))
			{
				// save settings
				if (settings != null)
					await settings.CommitAsync();

				// print job
				await job.PrintAsync(progress);
			}
			return true;
		} // func PrintDialogAsync

		#endregion

		#region -- Print Settings -----------------------------------------------------

		#region -- class PpsDefaultPrintSettings --------------------------------------

		private sealed class PpsDefaultPrintSettings : PpsPrintSettings
		{
			public PpsDefaultPrintSettings(PrintQueue queue, PrintTicket ticket)
			{
				InitDefault(queue, ticket);
			} // ctor

			public override Task CommitAsync()
				=> Task.CompletedTask;

			public override string Key => null;
		} // class PpsDefaultPrintSettings

		#endregion

		/// <summary>Create non persist print settings</summary>
		/// <param name="queue"></param>
		/// <param name="ticket"></param>
		/// <returns></returns>
		public static IPpsPrintSettings GetPrintSettings(PrintQueue queue, PrintTicket ticket = null)
			=> new PpsDefaultPrintSettings(queue ?? throw new ArgumentNullException(nameof(queue)), ticket);

		/// <summary>Create non persist print settings from default printer</summary>
		/// <returns></returns>
		public static IPpsPrintSettings GetDefaultPrintSettings()
			=> GetPrintSettings(null);

		#endregion

		#region -- Print Range --------------------------------------------------------

		/// <summary></summary>
		/// <param name="pageRange"></param>
		/// <returns></returns>
		public static IPpsPrintRange ToPrintRange(this PageRange pageRange)
			=> new PpsPrintRange(pageRange.PageFrom, pageRange.PageTo);

		/// <summary></summary>
		/// <param name="printDialog"></param>
		/// <param name="document"></param>
		/// <returns></returns>
		public static IPpsPrintRange ToPrintRange(this PrintDialog printDialog, IPpsPrintDocument document)
		{
			switch (printDialog.PageRangeSelection)
			{
				case PageRangeSelection.AllPages:
					return null;
				case PageRangeSelection.CurrentPage:
					return new PpsPrintRange(document.CurrentPage.Value);
				case PageRangeSelection.UserPages:
					return printDialog.PageRange.ToPrintRange();
				default:
					throw new ArgumentOutOfRangeException(nameof(PrintDialog.PageRangeSelection), printDialog.PageRangeSelection, "Page range selection is out.");
			}
		} // func GetPrintRange

		/// <summary></summary>
		/// <param name="document"></param>
		/// <returns></returns>
		public static bool HasRange(this IPpsPrintDocument document)
			=> document.MinPage.HasValue && document.MaxPage.HasValue;

		/// <summary></summary>
		/// <param name="document"></param>
		/// <returns></returns>
		public static PageRange ToPageRange(this IPpsPrintDocument document)
			=> HasRange(document) ? new PageRange(document.MinPage.Value, document.MaxPage.Value) : new PageRange(1);

		#endregion

		#region -- Advanced Dialog ----------------------------------------------------

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
		public static PrintTicket ShowAdvancedProperties(this PrintQueue printQueue, IntPtr hwnd, PrintTicket printTicket)
			=> ShowPrintPropertiesDialog(hwnd, printQueue, ShowAdvancedProperties, printTicket);

		/// <summary>Show document options</summary>
		/// <param name="hwnd"></param>
		public static void ShowDocumentProperties(this IPpsPrintSettings settings, IntPtr hwnd)
		{

		} // proc ShowDocumentProperties

		/// <summary>Show advanced options.</summary>
		/// <param name="hwnd"></param>
		public static void ShowAdvancedProperties(this IPpsPrintSettings settings, IntPtr hwnd)
		{

		} // proc ShowAdvancedProperties

		#endregion

		#region -- Print Tag ----------------------------------------------------------

		/// <summary>Find a print tag</summary>
		/// <param name="keywords"></param>
		/// <returns>Returns a print key</returns>
		public static string FindPrintTag(string keywords)
		{
			if (String.IsNullOrEmpty(keywords))
				return null;

			return keywords.Split(new char[] { ',', ' ' })
				.Where(IsPrintTag)
				.Select(GetPrintTag)
				.FirstOrDefault();
		} // func FindPrintKey

		private static bool IsPrintTag(string tag)
			=> tag.EndsWith(".prn", StringComparison.OrdinalIgnoreCase);

		private static string GetPrintTag(string tag)
		{
			var p = tag.LastIndexOf('.');
			return p <= 0 ? null : tag.Substring(0, p);
		} // func GetPrintTag

		#endregion

		/// <summary>Toggle to dialog</summary>
		public static bool ToggleProperties => (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) == (ModifierKeys.Control | ModifierKeys.Alt);
	} // class PpsPrinting

	#endregion
}
