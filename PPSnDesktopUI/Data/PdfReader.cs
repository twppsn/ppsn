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
using System.Collections;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TecWare.PPSn.UI;
using static TecWare.PPSn.NativeMethods;

namespace TecWare.PPSn.Data
{
	#region -- enum PdfActionType -----------------------------------------------------

	/// <summary>Pdf action type</summary>
	public enum PdfActionType
	{
		/// <summary>Unsupported action</summary>
		Unsupported,
		/// <summary>Goto destination action</summary>
		Goto,
		/// <summary>Goto remote destination action</summary>
		RemoteGoto,
		/// <summary>Open an uri.</summary>
		Uri,
		/// <summary>Launch an application.</summary>
		Launch
	} // enum PdfActionType

	#endregion

	#region -- enum PdfPrintMode ------------------------------------------------------

	/// <summary>Pdf print modes.</summary>
	public enum PdfPrintMode
	{
		/// <summary>EMF output</summary>
		EMF = 0,
		/// <summary>to output text only (for charstream devices)</summary>
		TEXTONLY = 1,
		/// <summary>to output level 2 PostScript into EMF as a series of GDI comments.</summary>
		PostScript2 = 2,
		/// <summary>to output level 3 PostScript into EMF as a series of GDI comments</summary>
		PostScript3 = 3,
		/// <summary>to output level 2 PostScript via ExtEscape() in PASSTHROUGH mode</summary>
		PostScript2Passthrough = 4,
		/// <summary>to output level 3 PostScript via ExtEscape() in PASSTHROUGH mode</summary>
		PostScript3Passthrough = 5
	} // enum PdfPrintMode

	#endregion

	#region -- enum PdfDocPermission --------------------------------------------------

	/// <summary>Dokument permissions (see pdf 1.7 - 7.6.3.2)</summary>
	public enum PdfDocPermission
	{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		AllowPrint = 0x04,
		AllowModify = 0x08,
		AllowCopy = 0x10,
		AllowNotes = 0x20,
		AllowFillForms = 0x100,
		AllowAccessibility = 0x200,
		AllowAssemble = 0x400,
		AllowHighResPrint = 0x800
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
	} // enum PdfDocPermission

	#endregion

	#region -- enum PdfPageRenderFlag -------------------------------------------------

	/// <summary>Pdf page render flags.</summary>
	[Flags]
	public enum PdfPageRenderFlag
	{
		/// <summary>No render options</summary>
		None = 0,
		/// <summary>Set if annotations are to be rendered.</summary>
		Annotations = 0x01,
		/// <summary>Set if using text rendering optimized for LCD display.</summary>
		LCDText = 0x02,
		/// <summary>Don't use the native text output available on some platforms</summary>
		NoNativeText = 0x04,
		/// <summary>Grayscale output.</summary>
		GrayScale = 0x08,
		/// <summary>Set whether to render in a reverse Byte order, this flag is only used when rendering to a bitmap.</summary>
		ReverseByteOrder = 0x10,
		/// <summary>Set if you want to get some debug info.</summary>
		DebugInfo = 0x80,
		/// <summary>Set if you don't want to catch exceptions.</summary>
		NoCatch = 0x100,
		/// <summary>Limit image cache size.</summary>
		LimitedImageCache = 0x200,
		/// <summary>Always use halftone for image stretching.</summary>
		ForceHalfTone = 0x400,
		/// <summary>Render for printing.</summary>
		ForPrinting = 0x800,
		/// <summary>Set to disable anti-aliasing on text.</summary>
		NoSmoothText = 0x1000,
		/// <summary>Set to disable anti-aliasing on images.</summary>
		NoSmoothImage = 0x2000,
		/// <summary>Set to disable anti-aliasing on paths.</summary>
		NoSmoothPath = 0x4000
	} // enum PdfPageRenderFlag

	#endregion

	#region -- class PdfReaderException -----------------------------------------------

	/// <summary>Pdf exception.</summary>
	public sealed class PdfReaderException : Exception
	{
		/// <summary>Create a generic pdf-exception</summary>
		/// <param name="fileName"></param>
		/// <param name="message"></param>
		public PdfReaderException(string message, string fileName)
			: this(1, message, fileName)
		{
		} // ctor

		private PdfReaderException(uint lastError, string message, string fileName)
			: base(message)
		{
			FileName = fileName;
			LastError = lastError;
		} // ctor

		/// <summary>PDF-Error</summary>
		public uint LastError { get; }
		/// <summary>PDF-File name.</summary>
		public string FileName { get; }

		/// <summary>Create an exception for Pdf-GetLastError</summary>
		/// <param name="fileName">Filename</param>
		/// <returns></returns>
		internal static Exception Create(string fileName)
		{
			var lastError = FPDF_GetLastError();
			switch (lastError)
			{
				case 2: // FPDF_ERR_FILE - File not found or could not be opened.
					return new FileNotFoundException("File not found.", fileName);
				case 3: // FPDF_ERR_FORMAT - File not in PDF format or corrupted.
					return new PdfReaderException(3, "File not in PDF format or corrupted.", fileName);
				case 4: // FPDF_ERR_PASSWORD - Password required or incorrect password.
					return new PdfReaderException(4, "Password required or incorrect password.", fileName);
				case 5: // FPDF_ERR_SECURITY - Unsupported security scheme.
					return new PdfReaderException(5, "Password required or incorrect password.", fileName);
				case 6: // FPDF_ERR_PAGE - Page not found or content error.
					return new ArgumentOutOfRangeException(nameof(PdfPage.PageNumber), "Page not found or content error.");
				default: // 1 FPDF_ERR_UNKNOWN - Unknown error.
					return new PdfReaderException(lastError, "Unknown error.", fileName);
			}
		} // func Create
	} // class PdfReaderException

	#endregion

	#region -- class PdfDestination ---------------------------------------------------

	/// <summary>Description of a pdf-destination.</summary>
	public sealed class PdfDestination
	{
		private readonly int pageNumber;
		private readonly double? pageX;
		private readonly double? pageY;
		private readonly double? pageZ;

		internal PdfDestination(PdfReader pdf, IntPtr hDestination)
		{
			lock (FPDF_LibraryLock)
			{
				pageNumber = FPDFDest_GetDestPageIndex(pdf.Handle, hDestination);
				TryGetParameters(hDestination, out pageX, out pageY, out pageZ);
			}
		} // ctor

		/// <summary>Create a generic pdf-destination.</summary>
		/// <param name="pageNumber">page number</param>
		/// <param name="pageX">x-coordinate of the page destination in page units.</param>
		/// <param name="pageY">y-coordinate of the page destination in page units.</param>
		/// <param name="pageZ">z-coordinate of the page destination in page units.</param>
		public PdfDestination(int pageNumber, double? pageX = null, double? pageY = null, double? pageZ = null)
		{
			this.pageNumber = pageNumber;
			this.pageX = pageX;
			this.pageY = pageY;
			this.pageZ = pageZ;
		} // ctor

		private static void TryGetParameters(IntPtr hDestination, out double? x, out double? y, out double? z)
		{
			var hasX = false;
			var hasY = false;
			var hasZ = false;
			var fx = 0.0f;
			var fy = 0.0f;
			var fz = 0.0f;
			if (FPDFDest_GetLocationInPage(hDestination, ref hasX, ref hasY, ref hasZ, ref fx, ref fy, ref fz))
			{
				x = hasX ? (double?)fx : null;
				y = hasY ? (double?)fy : null;
				z = hasZ ? (double?)fz : null;
			}
			else
			{
				x = null;
				y = null;
				z = null;
			}
		} // func TryGetParameters

		/// <summary>Return destination page.</summary>
		public int PageNumber => pageNumber;
		/// <summary>x-coordinate of the page destination in page units.</summary>
		public double? PageX => pageX;
		/// <summary>y-coordinate of the page destination in page units.</summary>
		public double? PageY => pageY;
		/// <summary>z-coordinate of the page destination in page units.</summary>
		public double? PageZ => pageZ;

		internal static PdfDestination Create(PdfReader pdf, IntPtr hDestination)
			=> hDestination == IntPtr.Zero ? null : new PdfDestination(pdf, hDestination);
	} // class PdfDestination

	#endregion

	#region -- class PdfAction --------------------------------------------------------

	/// <summary>Pdf action description.</summary>
	public sealed class PdfAction
	{
		private readonly PdfReader pdf;
		private readonly IntPtr hAction;
		private readonly PdfActionType type;

		internal PdfAction(PdfReader pdf, IntPtr intPtr)
		{
			this.pdf = pdf ?? throw new ArgumentNullException(nameof(pdf));
			this.hAction = intPtr;

			this.type = (PdfActionType)FPDFAction_GetType(hAction);
		} // ctor

		/// <summary>Type of the action.</summary>
		public PdfActionType Type => type;
		/// <summary>Destination of the action.</summary>
		public PdfDestination Destination
		{
			get
			{
				lock (FPDF_LibraryLock)
					return PdfDestination.Create(pdf, FPDFBookmark_GetAction(hAction));
			}
		} // prop Destination

		/// <summary>Destination file path of the action</summary>
		public string FilePath => FPDFAction_GetFilePath(hAction);
		/// <summary>Destination uri of the action</summary>
		public string UriPath => FPDFAction_GetURIPath(pdf.Handle, hAction);

		internal static PdfAction Create(PdfReader pdf, IntPtr hAction)
			=> hAction == IntPtr.Zero ? null : new PdfAction(pdf, hAction);
	} // class PdfAction

	#endregion

	#region -- class PdfBookmark ------------------------------------------------------

	/// <summary>Pdf bookmark description</summary>
	public sealed class PdfBookmark : IEnumerable<PdfBookmark>
	{
		private readonly PdfReader pdf;
		private readonly PdfBookmark parent;
		private readonly IntPtr hBookmark;

		internal PdfBookmark(PdfReader pdf, PdfBookmark parent, IntPtr hBookmark)
		{
			this.pdf = pdf ?? throw new ArgumentNullException(nameof(pdf));
			this.parent = parent;
			this.hBookmark = hBookmark;
		} // ctor

		/// <summary>Enumerate all child bookmarks.</summary>
		/// <returns>Child bookmark enumeration.</returns>
		public IEnumerator<PdfBookmark> GetEnumerator()
			=> pdf.GetBookmarks(this).GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator()
			=> GetEnumerator();

		internal IntPtr Handle => hBookmark;

		/// <summary>Parent Bookmark</summary>
		public PdfBookmark Parent => parent;

		/// <summary>Title of the bookmark</summary>
		public string Title => FPDFBookmark_GetTitle(hBookmark);
		/// <summary>Destination of the bookmark.</summary>
		public PdfDestination Destination
		{
			get
			{
				lock (FPDF_LibraryLock)
					return PdfDestination.Create(pdf, FPDFBookmark_GetDest(pdf.Handle, hBookmark));
			}
		} // prop Destination
		  /// <summary>Action of the bookmark</summary>
		public PdfAction Action
		{
			get
			{
				lock (FPDF_LibraryLock)
					return PdfAction.Create(pdf, FPDFBookmark_GetAction(hBookmark));
			}
		} // prop Action
	} // class PdfBookmark

	#endregion

	#region -- class PdfLink ----------------------------------------------------------

	/// <summary>Pdf link description.</summary>
	public sealed class PdfLink
	{
		private readonly PdfPage page;
		private readonly IntPtr hLink;

		internal PdfLink(PdfPage page, IntPtr hLink)
		{
			this.page = page ?? throw new ArgumentNullException(nameof(page));
			this.hLink = hLink;
		} // ctor

		/// <summary>Destination of the link.</summary>
		public PdfDestination Destination
		{
			get
			{
				lock (FPDF_LibraryLock)
					return PdfDestination.Create(page.Document, FPDFLink_GetDest(page.Document.Handle, hLink));
			}
		} // prop Destination

		/// <summary>Action of the link</summary>
		public PdfAction Action
		{
			get
			{
				lock (FPDF_LibraryLock)
					return PdfAction.Create(page.Document, FPDFLink_GetAction(hLink));
			}
		} // prop Action

		/// <summary>Annotation rect</summary>
		public Rect Bounds
		{
			get
			{
				lock (FPDF_LibraryLock)
				{
					if (FPDFLink_GetAnnotRect(hLink, out var rect) == 0)
						throw PdfReaderException.Create(page.Document.Name);

					return new Rect(rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top);
				}
			}
		} // prop Bounds

		internal static PdfLink Create(PdfPage page, IntPtr hLink)
			=> hLink == IntPtr.Zero ? null : new PdfLink(page, hLink);
	} // class PdfLink

	#endregion

	#region -- class PdfPage ----------------------------------------------------------

	/// <summary>Pdf page description.</summary>
	/// <remarks>It is important to call dispose.</remarks>
	public sealed class PdfPage : IDisposable
	{
		#region -- class PdfPageHandle ------------------------------------------------

		private class PdfPageHandle : SafeHandle
		{
			public PdfPageHandle(IntPtr hPdfPage)
				: base(IntPtr.Zero, true)
			{
				SetHandle(hPdfPage);
			} // ctor

			protected override bool ReleaseHandle()
			{
				lock (FPDF_LibraryLock)
					FPDF_ClosePage(handle);
				return true;
			} // func ReleaseHandle

			public override bool IsInvalid => handle == IntPtr.Zero || handle == new IntPtr(-1);
		} // class PdfPageHandle

		#endregion

		private readonly PdfReader pdf;
		private readonly PdfPageHandle pageHandle;
		private readonly int pageNumber;

		#region -- Ctor/Dtor ----------------------------------------------------------

		internal PdfPage(PdfReader pdf, IntPtr hPage, int pageIndex)
		{
			this.pdf = pdf ?? throw new ArgumentNullException(nameof(pdf));
			this.pageNumber = pageIndex;

			this.pageHandle = new PdfPageHandle(hPage);
			if (pageHandle.IsInvalid)
				throw new ArgumentException();

		} // ctor

		/// <summary>Free unmanaged resources.</summary>
		public void Dispose()
			=> Dispose(true);

		private void Dispose(bool disposing)
		{
			if (disposing)
				pageHandle?.Dispose();
		} // proc Dispose

		#endregion

		#region -- Render -------------------------------------------------------------

		private IntPtr CreatePdfBitmap(int width, int height)
		{
			if (height <= 0)
				throw new ArgumentOutOfRangeException(nameof(height), height, "Value greater zero expected.");
			if (width <= 0)
				throw new ArgumentOutOfRangeException(nameof(width), width, "Value greater zero expected.");

			var firstAlloc = true;
			redo:
			// create target bitmap
			var hBitmap = FPDFBitmap_Create(width, height, 0);
			if (hBitmap == IntPtr.Zero)
			{
				if (firstAlloc) // try free memory, for large pages during scrolling
				{
					firstAlloc = false;
					GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
					goto redo;
				}
				else
					throw new OutOfMemoryException();
			}
			try
			{
				// fill bitmap with white
				FPDFBitmap_FillRect(hBitmap, 0, 0, width, height, 0xFFFFFF);

				return hBitmap;
			}
			catch
			{
				FPDFBitmap_Destroy(hBitmap);
				throw;
			}
		} // func CreatePdfBitmap

		private static ImageSource CreateImageSource(IntPtr hBitmap, int width, int height)
		{
			lock (FPDF_LibraryLock)
			{
				var buf = FPDFBitmap_GetBuffer(hBitmap);
				var stride = FPDFBitmap_GetStride(hBitmap);
				return BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgr32, null, buf, height * stride, stride);
			}
		} // func CreateImageSource

		/// <summary>Renders a Part of a Page</summary>
		/// <param name="pageClipping">Clipping area of the page in pixels.</param>
		/// <param name="scaleX">Scale factor from page units to pixels.</param>
		/// <param name="scaleY">Scale factor from page units to pixels.</param>
		/// <param name="rotation">Rotation of the page (only 0,90,180,270 are usefull)</param>
		/// <param name="flags">Render flags</param>
		/// <returns></returns>
		public ImageSource Render(Rect pageClipping, double scaleX, double scaleY, double rotation = 0, PdfPageRenderFlag flags = PdfPageRenderFlag.None)
		{
			lock (FPDF_LibraryLock)
			{               // create target bitmap
				var width = (int)pageClipping.Width;
				var height = (int)pageClipping.Height;
				var hBitmap = CreatePdfBitmap(width, height);
				try
				{
					var clipping = default(FS_RECTF);
					var matrix = default(FS_MATRIX);

					clipping.left = 0.0f;
					clipping.top = 0.0f;
					clipping.right = (float)pageClipping.Width;
					clipping.bottom = (float)pageClipping.Height;

					var m = new Matrix(1.0, 0.0, 0.0, 1.0, 0.0, 0.0);
					if (rotation != 0)
						m.RotateAt(rotation, Width / 2, Height / 2);
					m.Scale(scaleX, scaleY);
					m.Translate(-(float)pageClipping.X, -(float)pageClipping.Y);

					matrix.a = (float)m.M11;
					matrix.b = (float)m.M12;
					matrix.c = (float)m.M21;
					matrix.d = (float)m.M22;
					matrix.e = (float)m.OffsetX;
					matrix.f = (float)m.OffsetY;

					FPDF_RenderPageBitmapWithMatrix(hBitmap, pageHandle.DangerousGetHandle(), ref matrix, ref clipping, (int)flags);

					return CreateImageSource(hBitmap, width, height);
				}
				finally
				{
					FPDFBitmap_Destroy(hBitmap);
				}
			}
		} // proc Render

		/// <summary>Render the whole page to an bitmap.</summary>
		/// <param name="width">Width of the result bitmap.</param>
		/// <param name="height">Height of the result bitmap</param>
		/// <param name="keepAspect">Should the page keep is aspect.</param>
		/// <param name="flags">Render flags</param>
		/// <returns>Bitmap of the page.</returns>
		public ImageSource Render(int width, int height, bool keepAspect = true, PdfPageRenderFlag flags = PdfPageRenderFlag.None)
		{
			if (keepAspect)
			{
				var aspect = Width / Height;
				if (width > height) // calc height
					height = (int)(width / aspect);
				else
					width = (int)(height * aspect);
			}

			if (width <= 0)
				throw new ArgumentOutOfRangeException(nameof(width), width, "Must be positiv.");
			if (height <= 0)
				throw new ArgumentOutOfRangeException(nameof(height), height, "Must be positiv.");

			// create target bitmap
			lock (FPDF_LibraryLock)
			{
				var hBitmap = CreatePdfBitmap(width, height);
				try
				{
					// render page
					lock (FPDF_LibraryLock)
						FPDF_RenderPageBitmap(hBitmap, pageHandle.DangerousGetHandle(), 0, 0, width, height, 0, (int)flags);

					// copy bitmap
					return CreateImageSource(hBitmap, width, height);
				}
				finally
				{
					FPDFBitmap_Destroy(hBitmap);
				}
			}
		} // proc Render

		/// <summary>Render the page to an device context (e.g. for printing)</summary>
		/// <param name="hdc">GDI+ device context.</param>
		/// <param name="x">x-offset in the device context.</param>
		/// <param name="y">y-offset in the device context.</param>
		/// <param name="w">width in the device context.</param>
		/// <param name="h">height in the device context.</param>
		/// <param name="r">rotation of the page (0,90,180,270 are possible).</param>
		/// <param name="flags">Render flags</param>
		public void Render(IntPtr hdc, int x, int y, int w, int h, int r = 0, PdfPageRenderFlag flags = PdfPageRenderFlag.None)
		{
			lock (FPDF_LibraryLock)
				FPDF_RenderPage(hdc, pageHandle.DangerousGetHandle(), x, y, w, h, (r / 90) % 4, (int)flags);
		} // proc Render

		#endregion

		#region -- Links --------------------------------------------------------------

		/// <summary>Get link from page-coordinate.</summary>
		/// <param name="x">x-coordinate, relative to page.</param>
		/// <param name="y">y-coordinate, relative to page.</param>
		/// <returns>Link at this position</returns>
		public PdfLink HitTest(double x, double y)
		{
			lock (FPDF_LibraryLock)
				return PdfLink.Create(this, FPDFLink_GetLinkAtPoint(pageHandle.DangerousGetHandle(), x, y));
		} // func HitTest

		/// <summary>Enumerate all links of this page.</summary>
		public IEnumerable<PdfLink> Links
		{
			get
			{
				var idx = 0;
				if (LinkEnumerate(ref idx, out var hLink))
				{
					var link = PdfLink.Create(this, hLink);
					if (link != null)
						yield return link;
				}
			}
		} // func Links

		private bool LinkEnumerate(ref int idx, out IntPtr hLink)
		{
			lock (FPDF_LibraryLock)
				return FPDFLink_Enumerate(pageHandle.DangerousGetHandle(), ref idx, out hLink) != 0;
		} // func LinkEnumerate

		#endregion

		/// <summary>Return the document of this page.</summary>
		public PdfReader Document => pdf;

		/// <summary>Number of this page.</summary>
		public int PageNumber => pageNumber;
		/// <summary>Label of this page.</summary>
		public string Label => pdf.GetPageLabel(pageNumber);

		/// <summary>Width of the page</summary>
		public double Width
		{
			get
			{
				lock (FPDF_LibraryLock)
					return FPDF_GetPageWidth(pageHandle.DangerousGetHandle());
			}
		} // prop Width

		/// <summary>Height of the page</summary>
		public double Height
		{
			get
			{
				lock (FPDF_LibraryLock)
					return FPDF_GetPageHeight(pageHandle.DangerousGetHandle());
			}
		} // prop Height
	} // class PdfPage

	#endregion

	#region -- class PdfProperties ----------------------------------------------------

	/// <summary>Property collection of the pdf-file</summary>
	public sealed class PdfProperties : IReadOnlyDictionary<string, string>
	{
		private static readonly string[] metaKeys = new string[] { "Name", "Version", "Title", "Author", "Subject", "Keywords", "Creator", "Producer", "CreationDate", "ModDate", "Trapped" };

		private readonly PdfReader pdf;

		internal PdfProperties(PdfReader pdf)
		{
			this.pdf = pdf ?? throw new ArgumentNullException(nameof(pdf));
		} // ctor

		public bool ContainsKey(string key)
			=> Array.IndexOf(metaKeys, key) >= 0;

		private bool TryGetValue(int metaKeyIndex, out string value)
		{
			switch (metaKeyIndex)
			{
				case -1:
					value = null;
					return false;
				case 0:
					value = pdf.Name;
					return true;
				case 1:
					value = pdf.Version.ToString(2);
					return true;
				default:
					value = FPDF_GetMetaText(pdf.Handle, metaKeys[metaKeyIndex]);
					return !String.IsNullOrEmpty(value);
			}
		} // func TryGetValue

		public bool TryGetValue(string key, out string value)
			=> TryGetValue(Array.FindIndex(metaKeys, c => String.Compare(c, key, StringComparison.OrdinalIgnoreCase) == 0), out value);

		IEnumerator IEnumerable.GetEnumerator()
			=> GetEnumerator();

		public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
		{
			for (var i = 0; i < metaKeys.Length; i++)
			{
				if (!TryGetValue(i, out var tmp))
					tmp = null;
				yield return new KeyValuePair<string, string>(metaKeys[i], tmp);
			}
		} // func GetEnumerator

		public string this[string key]
			=> TryGetValue(key, out var tmp) ? tmp : null;

		public IEnumerable<string> Keys => metaKeys;
		public IEnumerable<string> Values
		{
			get
			{
				foreach (var key in metaKeys)
				{
					if (TryGetValue(key, out var tmp) && tmp != null)
						yield return tmp;
				}
			}
		} // prop Values

		public string Name => TryGetValue(metaKeys[0], out var n) ? n : null;
		public string Version => TryGetValue(metaKeys[1], out var n) ? n : null;
		public string Title => TryGetValue(metaKeys[2], out var n) ? n : null;
		public string Author => TryGetValue(metaKeys[3], out var n) ? n : null;
		public string Subject => TryGetValue(metaKeys[4], out var n) ? n : null;
		public string Keywords => TryGetValue(metaKeys[5], out var n) ? n : null;
		public string Creator => TryGetValue(metaKeys[6], out var n) ? n : null;
		public string Producer => TryGetValue(metaKeys[7], out var n) ? n : null;
		public DateTime? CreationDate => TryGetValue(metaKeys[8], out var n) && DateTime.TryParse(n, out var dt) ? (DateTime?)dt : null;
		public DateTime? ModDate => TryGetValue(metaKeys[9], out var n) && DateTime.TryParse(n,out var dt) ?  (DateTime?)dt : null;
		public string Trapped => TryGetValue(metaKeys[10], out var n) ? n : null;

		public int Count => metaKeys.Length;
	} // class PdfProperties

	#endregion

	#region -- class PpsPdfReaderPrintDocument ----------------------------------------

	public class PpsPdfReaderPrintDocument : PpsDrawingPrintDocument
	{
		private readonly int pageCount;

		public PpsPdfReaderPrintDocument(PdfReader pdf)
			: base((pdf ?? throw new ArgumentNullException(nameof(pdf))).GetDrawingPrintDocument(), PpsPrinting.FindPrintTag(pdf.Properties.Keywords))
		{
			pageCount = pdf.PageCount;
		} // ctor

		public sealed override int? MinPage { get => 1; set { } }
		public sealed override int? MaxPage { get => pageCount; set { } }
	} // class PpsPdfReaderPrintDocument

	#endregion

	#region -- class PdfReader --------------------------------------------------------

	/// <summary>Pdf document reader.</summary>
	public sealed class PdfReader : IDisposable
	{
		#region -- class PdfDocumentHandle --------------------------------------------

		private class PdfDocumentHandle : SafeHandle
		{
			public PdfDocumentHandle(IntPtr hPdfDocument)
				: base(IntPtr.Zero, true)
			{
				SetHandle(hPdfDocument);
			} // ctor

			protected override bool ReleaseHandle()
			{
				lock (FPDF_LibraryLock)
					FPDF_CloseDocument(base.handle);
				return true;
			} // func ReleaseHandle

			public override bool IsInvalid => base.handle == IntPtr.Zero || base.handle == new IntPtr(-1);
		} // class PdfDocumentHandle

		#endregion

		#region -- class PdfMemoryBlock -----------------------------------------------

		private sealed class PdfMemoryBlock : IDisposable
		{
			private readonly int size;
			private readonly IntPtr hMemory;

			private bool disposedValue = false;

			public PdfMemoryBlock(byte[] memory, int offset, int length)
			{
				this.size = length;

				// copy memory block
				hMemory = Marshal.AllocCoTaskMem(size);
				Marshal.Copy(memory, offset, hMemory, length);
			} // ctor

			~PdfMemoryBlock()
			{
				Dispose(false);
			} // dtor

			private void Dispose(bool disposing)
			{
				if (!disposedValue)
				{
					Marshal.FreeCoTaskMem(hMemory);
					disposedValue = true;
				}
			} // proc Dispose

			public void Dispose()
			{
				Dispose(true);
				GC.SuppressFinalize(this);
			} // proc Dispose

			public IntPtr Memory => hMemory;
			public int Size => size;
		} // class PdfMemoryBlock

		#endregion

		#region -- class PdfDocumentAccess --------------------------------------------

		private sealed class PdfDocumentAccess : IDisposable
		{
			private readonly Stream data;

			public PdfDocumentAccess(Stream data)
			{
				this.data = data ?? throw new ArgumentNullException(nameof(data));

				GetBlockDelegateObject = GetBlock;

				if (!data.CanSeek)
					throw new ArgumentException(nameof(Stream.CanSeek));
				if (!data.CanRead)
					throw new ArgumentException(nameof(Stream.CanRead));
			} // ctor

			public void Dispose()
			{
				data?.Dispose();
			} // proc Dispose

			internal uint GetBlock(IntPtr param, uint position, IntPtr pBuf, uint size)
			{
				if (data.Seek(position, SeekOrigin.Begin) != position)
					return 0;

				var len = (int)size;
				var buf = new byte[len];
				if (data.Read(buf, 0, len) != len)
					return 0;

				Marshal.Copy(buf, 0, pBuf, len);

				return size;
			} // func GetBlock

			public long FileLength => data.Length;

			internal GetBlockDelegate GetBlockDelegateObject { get; }
		} // class PdfDocumentAccess

		#endregion

		private readonly PdfDocumentHandle documentHandle;
		private readonly IDisposable dispose;

		private readonly Version pdfVersion;
		private readonly string pdfName;
		private readonly int pageCount = -1;

		#region -- Ctor/Dtor ----------------------------------------------------------

		private PdfReader(PdfDocumentHandle documentHandle, string pdfName, IDisposable dispose)
		{
			this.documentHandle = documentHandle ?? throw new ArgumentNullException(nameof(documentHandle));
			this.pdfName = pdfName ?? "unknown.pdf";
			this.dispose = dispose;

			Properties = new PdfProperties(this);

			// read pdf version
			if (!FPDF_GetFileVersion(documentHandle.DangerousGetHandle(), out var tmp))
				throw PdfReaderException.Create(pdfName);
			pdfVersion = new Version(tmp / 10, tmp % 10);

			// read page count
			pageCount = FPDF_GetPageCount(documentHandle.DangerousGetHandle());
			if (pageCount < 0)
				throw PdfReaderException.Create(pdfName);
		} // ctor

		/// <summary>Free unmanaged resources.</summary>
		public void Dispose()
		{
			documentHandle?.Dispose();
			dispose?.Dispose();
		} // Dispose

		#endregion

		#region -- Page ---------------------------------------------------------------

		private void CheckPageNumber(int pageNumber)
		{
			if (pageNumber < 0 || pageNumber >= pageCount)
				throw new ArgumentOutOfRangeException(nameof(pageNumber), pageNumber, "Page number is invalid.");
		} // proc CheckPageNumber

		/// <summary>Open a page of the pdf-document</summary>
		/// <param name="pageNumber">Page number, zero indexed.</param>
		/// <returns>Page object.</returns>
		public PdfPage OpenPage(int pageNumber)
		{
			CheckPageNumber(pageNumber);

			lock (FPDF_LibraryLock)
				return new PdfPage(this, FPDF_LoadPage(documentHandle.DangerousGetHandle(), pageNumber), pageNumber);
		} // func OpenPage

		/// <summary>Get size of this page.</summary>
		/// <param name="pageNumber">Page number, zero indexed.</param>
		/// <returns>Size of the page in page units.</returns>
		public Size GetPageSize(int pageNumber)
		{
			CheckPageNumber(pageNumber);

			lock (FPDF_LibraryLock)
			{
				if (FPDF_GetPageSizeByIndex(documentHandle.DangerousGetHandle(), pageNumber, out var width, out var height) == 0)
					throw PdfReaderException.Create(pdfName);
				return new Size(width, height);
			}
		} // func GetPageSize

		/// <summary>Get the page label.</summary>
		/// <param name="pageNumber">Page number, zero indexed.</param>
		/// <returns>Page label as string</returns>
		public string GetPageLabel(int pageNumber)
		{
			CheckPageNumber(pageNumber);

			return FPDF_GetPageLabel(documentHandle.DangerousGetHandle(), pageNumber);
		} // func GetPageLabel

		/// <summary>Total number of pages in this document.</summary>
		public int PageCount => pageCount;

		#endregion

		#region -- Print --------------------------------------------------------------

		#region -- class PdfPrintDocument ---------------------------------------------

		private sealed class PdfPrintDocument : PrintDocument
		{
			private readonly PdfReader pdf;
			private int printPage = 0;
			private int lastPage = 0;

			public PdfPrintDocument(PdfReader pdf)
			{
				this.pdf = pdf ?? throw new ArgumentNullException(nameof(pdf));
				DocumentName = Path.GetFileName(pdf.Name);

				PrinterSettings.MinimumPage = 1;
				PrinterSettings.MaximumPage = pdf.PageCount;
			} // ctor

			protected override void OnBeginPrint(PrintEventArgs e)
			{
				base.OnBeginPrint(e);

				switch (PrinterSettings.PrintRange)
				{
					case PrintRange.SomePages:
						printPage = PrinterSettings.FromPage - 1;
						lastPage = PrinterSettings.ToPage - 1;
						break;
					default:
						printPage = 0;
						lastPage = pdf.PageCount - 1;
						break;
				}

				SetPrintMode(PdfPrintMode.EMF);
				//SetPrintTextWithGDI(true);
			} // proc OnBeginPrint

			protected override void OnPrintPage(PrintPageEventArgs e)
			{
				base.OnPrintPage(e);

				if (e.Cancel)
					return;

				var pt = new System.Drawing.Point[] {
					new System.Drawing.Point(e.PageBounds.Left - (int)e.PageSettings.PrintableArea.Left, e.PageBounds.Top - (int)e.PageSettings.PrintableArea.Top),
					new System.Drawing.Point(e.PageBounds.Width - (int)e.PageSettings.PrintableArea.Left, e.PageBounds.Height - (int)e.PageSettings.PrintableArea.Top)
				};
				e.Graphics.TransformPoints(System.Drawing.Drawing2D.CoordinateSpace.Device, System.Drawing.Drawing2D.CoordinateSpace.Page, pt);

				var hdc = e.Graphics.GetHdc();
				try
				{
					using (var page = pdf.OpenPage(printPage))
					{
						//e.PageSettings.PrinterResolution.Kind
						page.Render(hdc, pt[0].X, pt[0].Y, pt[1].X, pt[1].Y, 0, PdfPageRenderFlag.ForPrinting);
					}
				}
				finally
				{
					e.Graphics.ReleaseHdc(hdc);
				}

				e.HasMorePages = ++printPage <= lastPage;
			} // proc OnPrintPage

			protected override void OnEndPrint(PrintEventArgs e)
				=> base.OnEndPrint(e);

			protected override void OnQueryPageSettings(QueryPageSettingsEventArgs e)
				=> base.OnQueryPageSettings(e);
		} // class PdfPrintDocument

		#endregion

		/// <summary>GDI+ printing of the pdf.</summary>
		/// <returns>Return a winforms print document.</returns>
		internal PrintDocument GetDrawingPrintDocument()
			=> new PdfPrintDocument(this);

		/// <summary>Get a pps print document</summary>
		public IPpsPrintDocument GetPrintDocument()
			=> new PpsPdfReaderPrintDocument(this);

		#endregion

		#region -- Bookmarks ----------------------------------------------------------

		internal IEnumerable<PdfBookmark> GetBookmarks(PdfBookmark parentBookmark)
		{
			if (documentHandle.IsClosed
				|| documentHandle.IsInvalid)
				yield break;

			IntPtr hBookmark;
			lock (FPDF_LibraryLock)
				hBookmark = FPDFBookmark_GetFirstChild(documentHandle.DangerousGetHandle(), parentBookmark == null ? IntPtr.Zero : parentBookmark.Handle);
			while (hBookmark != IntPtr.Zero)
			{
				yield return new PdfBookmark(this, parentBookmark, hBookmark);
				lock (FPDF_LibraryLock)
					hBookmark = FPDFBookmark_GetNextSibling(documentHandle.DangerousGetHandle(), hBookmark);
			}
		} // func GetBookmarks

		/// <summary>Get all bookmarks</summary>
		public IEnumerable<PdfBookmark> Bookmarks => GetBookmarks(null);

		#endregion

		#region -- Properties ---------------------------------------------------------

		/// <summary>Get the document permissions.</summary>
		/// <returns></returns>
		public PdfDocPermission GetPermissions()
		{
			lock (FPDF_LibraryLock)
				return (PdfDocPermission)FPDF_GetDocPermissions(documentHandle.DangerousGetHandle());
		} // func GetPermissions

		/// <summary>Return the properties of this pdf-document.</summary>
		public PdfProperties Properties { get; }

		/// <summary>Name of the pdf, in most cases the file name.</summary>
		public string Name => pdfName;
		/// <summary>Pdf version.</summary>
		public Version Version => pdfVersion;

		#endregion

		internal IntPtr Handle => documentHandle.DangerousGetHandle();

		/// <summary>Change the printmode for the rendering on an gdi+ device context.</summary>
		/// <param name="mode">Print mode.</param>
		public static void SetPrintMode(PdfPrintMode mode)
		{
			lock (FPDF_LibraryLock)
				FPDF_SetPrintMode((int)mode);
		} // proc  SetPrintMode

		/// <summary>Change the font rendering on an gdi+ device context.</summary>
		/// <param name="useGdi"><c>true</c>, to render fonts with gdi+.</param>
		public static void SetPrintTextWithGDI(bool useGdi)
		{
			lock (FPDF_LibraryLock)
				FPDF_SetPrintTextWithGDI(useGdi);
		} // proc SetPrintTextWithGDI

		#region -- Open ---------------------------------------------------------------

		/// <summary>Open a pdf-file</summary>
		/// <param name="fileName">Filename</param>
		/// <param name="password">Password of the pdf</param>
		/// <returns>Pdf document reader</returns>
		public static PdfReader Open(string fileName, SecureString password = null)
		{
			if (String.IsNullOrEmpty(fileName))
				throw new ArgumentNullException(nameof(fileName));

			lock (FPDF_LibraryLock)
			{
				var documentHandle = new PdfDocumentHandle(FPDF_LoadDocument(fileName, IntPtr.Zero));
				if (documentHandle.IsInvalid)
					throw PdfReaderException.Create(fileName);

				return new PdfReader(documentHandle, fileName, null);
			}
		} // func Open

		/// <summary>Pdf data as an memory block.</summary>
		/// <param name="data">Pdf bytes.</param>
		/// <param name="offset">Offset in the data stream.</param>
		/// <param name="length">Length of the pdf file.</param>
		/// <param name="password">Password of the pdf</param>
		/// <param name="name">Name of the pdf-data stream.</param>
		/// <returns>Pdf document reader</returns>
		public static PdfReader Open(byte[] data, int offset = 0, int length = -1, string name = null, SecureString password = null)
		{
			if (data == null)
				throw new ArgumentNullException(nameof(data));

			if (length < 0)
				length = data.Length - offset;
			if (length < 0)
				throw new ArgumentOutOfRangeException(nameof(length), length, "Length is lower zero.");

			name = name ?? "bytes.pdf";
			var mem = new PdfMemoryBlock(data, offset, length);

			lock (FPDF_LibraryLock)
			{
				var documentHandle = new PdfDocumentHandle(FPDF_LoadMemDocument(mem.Memory, mem.Size, IntPtr.Zero));
				if (documentHandle.IsInvalid)
					throw PdfReaderException.Create(name);

				return new PdfReader(documentHandle, name, mem);
			}
		} // func Open

		/// <summary>Open the file from an stream.</summary>
		/// <param name="data">Data stream (the must seek- and readable).</param>
		/// <param name="name">Name of the pdf.</param>
		/// <param name="password">Password of the pdf</param>
		/// <returns>Pdf document reader</returns>
		public static PdfReader Open(Stream data, string name = null, SecureString password = null)
		{
			var documentAccess = new PdfDocumentAccess(data);
			var fileAccess = new FPDF_FILEACCESS()
			{
				getBlock = documentAccess.GetBlockDelegateObject,
				m_FileLen = (uint)documentAccess.FileLength,
				param = IntPtr.Zero
			};

			if (String.IsNullOrEmpty(name))
			{
				if (data is FileStream fs)
					name = fs.Name;
				else
					name = "stream.pdf";
			}

			lock (FPDF_LibraryLock)
			{
				var documentHandle = new PdfDocumentHandle(FPDF_LoadCustomDocument(ref fileAccess, IntPtr.Zero));
				if (documentHandle.IsInvalid)
					throw PdfReaderException.Create(name);

				return new PdfReader(documentHandle, name, documentAccess);
			}
		} // func Open

		#endregion
	} // class PdfReader

	#endregion
}
