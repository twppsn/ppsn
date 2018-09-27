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
using System.Runtime.InteropServices;
using System.Text;

namespace TecWare.PPSn
{
	internal static partial class NativeMethods
	{
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate uint GetBlockDelegate(IntPtr param, uint position, IntPtr pBuf, uint size);

		public struct FPDF_FILEACCESS
		{
			public uint m_FileLen;
			[MarshalAs(UnmanagedType.FunctionPtr)]
			public GetBlockDelegate getBlock;
			public IntPtr param;
		} // struct FPDF_FILEACCESS

		// Matrix for transformation, in the form [a b c d e f], equivalent to:
		// | a  b  0 |
		// | c  d  0 |
		// | e  f  1 |
		//
		// Translation is performed with [1 0 0 1 tx ty].
		// Scaling is performed with [sx 0 0 sy 0 0].
		// See PDF Reference 1.7, 4.2.2 Common Transformations for more.
		public struct FS_MATRIX
		{
			public float a;
			public float b;
			public float c;
			public float d;
			public float e;
			public float f;
		} // struct FS_MATRIX

		// Rectangle area(float) in device or page coordinate system.
		[StructLayout(LayoutKind.Sequential)]
		public struct FS_RECTF
		{
			// The x-coordinate of the left-top corner.
			public float left;
			// The y-coordinate of the left-top corner.
			public float top;
			// The x-coordinate of the right-bottom corner.
			public float right;
			// The y-coordinate of the right-bottom corner.
			public float bottom;
		} // struct FS_RECTF

		[DllImport("pdfium.dll")]
		public static extern void FPDF_InitLibrary();

		[DllImport("pdfium.dll")]
		public static extern uint FPDF_GetLastError();

		[DllImport("pdfium.dll")]
		public static extern IntPtr FPDF_LoadDocument([In] string file_path, [In] IntPtr password);
		[DllImport("pdfium.dll")]
		public static extern IntPtr FPDF_LoadMemDocument([In] IntPtr data_buf, [In] int size, [In] IntPtr password);
		[DllImport("pdfium.dll")]
		public static extern IntPtr FPDF_LoadCustomDocument([In] ref FPDF_FILEACCESS pFileAccess, [In] IntPtr password);

		[DllImport("pdfium.dll")]
		public static extern ulong FPDF_GetDocPermissions(IntPtr document);
		[DllImport("pdfium.dll")]
		public static extern bool FPDF_GetFileVersion(IntPtr document, [Out] out int fileVersion);
		[DllImport("pdfium.dll")]
		public static extern int FPDF_GetPageSizeByIndex(IntPtr document, int page_index, out double width, out double height);
		[DllImport("pdfium.dll")]
		private static extern uint FPDF_GetPageLabel(IntPtr document, int page_index, StringBuilder buffer, uint buflen);

		public static string FPDF_GetPageLabel(IntPtr document, int page_index)
			=> GetPdfString((sb, l) => FPDF_GetPageLabel(document, page_index, sb, l));

		[DllImport("pdfium.dll", CharSet = CharSet.Unicode)]
		private static extern uint FPDF_GetMetaText(IntPtr document, [MarshalAs(UnmanagedType.LPStr)] string tag, StringBuilder buffer, uint buflen);

		public static string FPDF_GetMetaText(IntPtr document, string tag)
			=> GetPdfString((sb, l) => FPDF_GetMetaText(document, tag, sb, l));

		[DllImport("pdfium.dll")]
		public static extern void FPDF_CloseDocument(IntPtr document);
		[DllImport("pdfium.dll")]
		public static extern int FPDF_GetPageCount(IntPtr document);

		[DllImport("pdfium.dll")]
		public static extern IntPtr FPDF_LoadPage(IntPtr document, int page_index);
		[DllImport("pdfium.dll")]
		public static extern void FPDF_ClosePage(IntPtr page);
		[DllImport("pdfium.dll")]
		public static extern double FPDF_GetPageWidth(IntPtr page);
		[DllImport("pdfium.dll")]
		public static extern double FPDF_GetPageHeight(IntPtr page);

		[DllImport("pdfium.dll")]
		public static extern void FPDF_RenderPageBitmap(IntPtr bitmap, IntPtr page, int start_x, int start_y, int size_x, int size_y, int rotate, int flags);
		[DllImport("pdfium.dll")]
		public static extern void FPDF_RenderPageBitmapWithMatrix(IntPtr bitmap, IntPtr page, [In] ref FS_MATRIX matrix, [In] ref FS_RECTF clipping, int flags);
		[DllImport("pdfium.dll")]
		public static extern void FPDF_RenderPage(IntPtr dc, IntPtr page, int start_x, int start_y, int size_x, int size_y, int rotate, int flags);

		[DllImport("pdfium.dll")]
		public static extern IntPtr FPDFBitmap_Create(int width, int height, int alpha);
		[DllImport("pdfium.dll")]
		public static extern IntPtr FPDFBitmap_Destroy(IntPtr bitmap);
		[DllImport("pdfium.dll")]
		public static extern IntPtr FPDFBitmap_GetBuffer(IntPtr bitmap);
		[DllImport("pdfium.dll")]
		public static extern int FPDFBitmap_GetStride(IntPtr bitmap);
		[DllImport("pdfium.dll")]
		public static extern void FPDFBitmap_FillRect(IntPtr bitmap, int left, int top, int width, int height, uint color);

		[DllImport("pdfium.dll")]
		public static extern IntPtr FPDFBookmark_GetFirstChild(IntPtr document, IntPtr bookmark);
		[DllImport("pdfium.dll")]
		public static extern IntPtr FPDFBookmark_GetNextSibling(IntPtr document, IntPtr bookmark);
		[DllImport("pdfium.dll", CharSet = CharSet.Unicode)]
		private static extern uint FPDFBookmark_GetTitle(IntPtr bookmark, StringBuilder buffer, uint buflen);

		public static string FPDFBookmark_GetTitle(IntPtr bookmark)
			=> GetPdfString((sb, l) => FPDFBookmark_GetTitle(bookmark, sb, l));

		[DllImport("pdfium.dll")]
		public static extern IntPtr FPDFBookmark_GetDest(IntPtr document, IntPtr bookmark);
		[DllImport("pdfium.dll")]
		public static extern IntPtr FPDFBookmark_GetAction(IntPtr bookmark);

		[DllImport("pdfium.dll")]
		public static extern int FPDFDest_GetDestPageIndex(IntPtr document, IntPtr dest);
		[DllImport("pdfium.dll")]
		public static extern bool FPDFDest_GetLocationInPage(IntPtr dest, ref bool hasXCoord, ref bool hasYCoord, ref bool hasZoom, ref float x, ref float y, ref float zoom);

		[DllImport("pdfium.dll")]
		public static extern int FPDFAction_GetType(IntPtr action);
		[DllImport("pdfium.dll")]
		public static extern IntPtr FPDFAction_GetDest(IntPtr document, IntPtr action);
		[DllImport("pdfium.dll", CharSet = CharSet.Unicode)]
		private static extern uint FPDFAction_GetFilePath(IntPtr action, StringBuilder buffer, uint buflen);

		public static string FPDFAction_GetFilePath(IntPtr action)
			=> GetPdfString((sb, l) => FPDFAction_GetFilePath(action, sb, l));

		[DllImport("pdfium.dll", CharSet = CharSet.Unicode)]
		private static extern uint FPDFAction_GetURIPath(IntPtr document, IntPtr action, StringBuilder buffer, uint buflen);

		public static string FPDFAction_GetURIPath(IntPtr document, IntPtr action)
			=> GetPdfString((sb, l) => FPDFAction_GetURIPath(document, action, sb, l));

		[DllImport("pdfium.dll")]
		public static extern IntPtr FPDFLink_GetLinkAtPoint(IntPtr page, double x, double y);
		[DllImport("pdfium.dll")]
		public static extern int FPDFLink_GetLinkZOrderAtPoint(IntPtr page, double x, double y);
		[DllImport("pdfium.dll")]
		public static extern IntPtr FPDFLink_GetDest(IntPtr document, IntPtr link);
		[DllImport("pdfium.dll")]
		public static extern IntPtr FPDFLink_GetAction(IntPtr link);
		[DllImport("pdfium.dll")]
		public static extern int FPDFLink_GetAnnotRect(IntPtr link, out FS_RECTF rect);
		[DllImport("pdfium.dll")]
		public static extern int FPDFLink_Enumerate(IntPtr page, ref int startPos, out IntPtr linkAnnot);

		[DllImport("pdfium.dll")]
		public static extern bool FPDF_SetPrintMode(int mode);

		[DllImport("pdfium.dll")]
		public static extern void FPDF_SetPrintTextWithGDI(bool use_gdi);

		private static string GetPdfString(Func<StringBuilder, uint, uint> getStringFunction)
		{
			lock (FPDF_LibraryLock)
			{
				var len = getStringFunction(null, 0);
				if (len == 0)
					return String.Empty;
				else if (len < 0xFFFFF)
				{
					var sb = new StringBuilder((int)len);
					getStringFunction(sb, len);
					return sb.ToString();
				}
				else
					throw new OutOfMemoryException();
			}
		} // func GetPdfString

		public static object FPDF_LibraryLock = new object();

		static NativeMethods()
		{
			FPDF_InitLibrary(); // init lib
		} // sctor
	}
}
