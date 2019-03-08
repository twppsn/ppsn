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
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using TecWare.PPSn.Data;

namespace TecWare.PPSn.Controls
{
	#region -- enum PdfGotoMode -------------------------------------------------------

	/// <summary>Zoom mode for the pdf.</summary>
	public enum PdfGotoMode
	{
		/// <summary>Do not change the zoom.</summary>
		None,
		/// <summary>Zoom page in X</summary>
		ZoomX,
		/// <summary>Zoom page in Y</summary>
		ZoomY,
		/// <summary>Zoom page to be full visible.</summary>
		ZoomXorY
	} // class PdfGotoMode

	#endregion

	#region -- class PpsPdfViewer -----------------------------------------------------

	/// <summary>Pdf viewer primitive to show all pages of an pdf..</summary>
	public class PpsPdfViewer : FrameworkElement, IScrollInfo
	{
		#region -- struct PageLayout --------------------------------------------------

		private struct PageLayout
		{
			public Rect Area;
			public int Rotation;
		} // struct PageLayout

		#endregion

		#region -- class PageCache ----------------------------------------------------

		private sealed class PageCache
		{
			private readonly PpsPdfViewer viewer;
			private readonly int pageNumber;

			// rendered result
			private readonly object renderLock = new object();
			private Rect imagePart;
			private Matrix imageTransform;
			private ImageSource image = null;

			private CancellationTokenSource currentRenderCancellation = null;
			private Rect lastImagePart;
			private Matrix lastImageTransform;
			private bool isDead = false;

			public PageCache(PpsPdfViewer viewer, int pageNumber)
			{
				this.viewer = viewer ?? throw new ArgumentNullException(nameof(viewer));
				this.pageNumber = pageNumber;
			} // ctor

			public void Clean()
			{
				lock (renderLock)
				{
					StopRenderUnsafe();

					isDead = true;
					image = null;
				}
			} // proc Clean

			private void EnqueueRender(Rect part, Matrix transform)
			{
				lock (renderLock)
				{
					if (RenderTargetEqual(ref part, ref transform, ref lastImagePart, ref lastImageTransform))
						return;

					//Debug.Print("Enqueue: {0}", pageNumber);
					StopRenderUnsafe();

					lastImagePart = part;
					lastImageTransform = transform;

					currentRenderCancellation = new CancellationTokenSource();
					var token = currentRenderCancellation.Token;
					var currentRenderTask = Task.Run(() => RenderCore(part, transform, token));
					currentRenderTask.ContinueWith(InvalidViewer, TaskContinuationOptions.OnlyOnRanToCompletion);
				}
			} // proc EnqueueRender

			private void InvalidViewer(Task t)
			{
				if (!isDead)
					viewer.Dispatcher.BeginInvoke(new Action(()=>viewer.InvalidateVisual()));
			} // proc InvalidViewer

			private void StopRenderUnsafe()
			{
				currentRenderCancellation?.Cancel();
				currentRenderCancellation = null;
			} // proc StopRenderUnsafe

			private static readonly object renderGlobalLock = new object(); // only one page at a time

			private async Task RenderCore(Rect part, Matrix transform, CancellationToken cancellationToken)
			{
				// wait for changes
				await Task.Delay(20);

				ImageSource newImage;

				lock (renderGlobalLock)
				{
					// cancellation requested
					if (cancellationToken.IsCancellationRequested)
						return;

					// start render
					//Debug.Print("Render: {0}", pageNumber);
					// render the image to device pixels, not screen pixels
					var deviceScaleX = viewer.dpiScaleFactor.X;
					var deviceScaleY = viewer.dpiScaleFactor.Y;
					var drawRect = new Rect(part.Left + transform.OffsetX, part.Top + transform.OffsetY, part.Width, part.Height); // move rect to one page coordinates
					drawRect.Scale(deviceScaleX, deviceScaleY);
					using (var page = viewer.DocumentUnsafe.OpenPage(pageNumber))
						newImage = page.Render(drawRect, transform.M11 * deviceScaleX, transform.M22 * deviceScaleY, 0);

					newImage.Freeze();
				}

				if (cancellationToken.IsCancellationRequested)
				{
					//Debug.Print("Render-Rollback: {0}", pageNumber);
					return;
				}

				//Debug.Print("Render-Commit: {0}", pageNumber);
				lock (renderLock)
				{
					image = newImage;
					imageTransform = transform;
					imagePart = part;
				}
			} // func RenderCore

			public void RenderFast(DrawingContext dc, Rect pagePosition, Rect pagePart, Matrix pageMatrix)
			{
				//Debug.Print("Paint: {0}", pageNumber);
				if (image != null)
				{
					if (RenderTargetEqual(ref imagePart, ref imageTransform, ref pagePart, ref pageMatrix))
						dc.DrawImage(image, pagePosition);
					else
					{
						// white background
						dc.DrawRectangle(Brushes.White, null, pagePosition);

						// use current page
						dc.PushClip(new RectangleGeometry(pagePosition)); // clip area
						var deltaX = pageMatrix.M11 / imageTransform.M11;
						var deltaY = pageMatrix.M22 / imageTransform.M22;
						var rc = new Rect(
							pagePosition.X,
							pagePosition.Y,
							imagePart.Width * deltaX,
							imagePart.Height * deltaY
						); // calc new image position

						// move canvas
						dc.PushTransform(new TranslateTransform(imagePart.X * deltaX - pagePart.X, imagePart.Y * deltaY - pagePart.Y));
						// draw
						dc.DrawImage(image, rc);
						dc.Pop(); // transform
						dc.Pop(); // clip

						// start render
						EnqueueRender(pagePart, pageMatrix);
					}
				}
				else
				{
					// white page
					dc.DrawRectangle(Brushes.White, null, pagePosition);
					// start render
					EnqueueRender(pagePart, pageMatrix);
				}
			} // proc RenderFast

			private static bool AreClose(double a, double b)
			{
				if (a == b)
					return true;
				var c = Math.Abs(a) - Math.Abs(b);
				if (c > -0.01 && c < 0.01)
					return true;
				return false;
			} // func AreClose

			private static bool RenderTargetEqual(ref Rect part1, ref Matrix transform1, ref Rect part2, ref Matrix transform2)
				=> AreClose(part1.Left, part2.Left)
					&& AreClose(part1.Top, part2.Top)
					&& AreClose(part1.Width, part2.Width)
					&& AreClose(part1.Height, part2.Height)
					&& AreClose(transform1.M11, transform2.M11)
					&& AreClose(transform1.OffsetX, transform2.OffsetX)
					&& AreClose(transform1.OffsetY, transform2.OffsetY);

			public int PageNumber => pageNumber;
		} // class PageCache

		#endregion

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public static readonly DependencyProperty DocumentProperty = DependencyProperty.Register(nameof(Document), typeof(PdfReader), typeof(PpsPdfViewer), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnPdfSourceChanged)));
		public static readonly DependencyProperty BackgroundProperty = Control.BackgroundProperty.AddOwner(typeof(PpsPdfViewer), new FrameworkPropertyMetadata(SystemColors.ControlBrush, new PropertyChangedCallback(OnBackgroundChanged)));

		public static readonly DependencyProperty CurrentPageNumberProperty = DependencyProperty.Register(nameof(CurrentPageNumber), typeof(int), typeof(PpsPdfViewer), new FrameworkPropertyMetadata(-1, new PropertyChangedCallback(OnCurrentPageNumberChanged), new CoerceValueCallback(OnCurrentPageNumberCoerce)));
		private static readonly DependencyPropertyKey pageCountPropertyKey = DependencyProperty.RegisterReadOnly(nameof(PageCount), typeof(int), typeof(PpsPdfViewer), new PropertyMetadata(-1));
		public static readonly DependencyProperty PageCountProperty = pageCountPropertyKey.DependencyProperty;

		public static readonly DependencyProperty ZoomProperty = DependencyProperty.Register(nameof(Zoom), typeof(double), typeof(PpsPdfViewer), new FrameworkPropertyMetadata(1.0, new PropertyChangedCallback(OnZoomChanged), new CoerceValueCallback(OnZoomCoerce)));

		private static readonly DependencyPropertyKey visiblePageAreaPropertyKey = DependencyProperty.RegisterReadOnly(nameof(VisiblePageArea), typeof(Rect), typeof(PpsPdfViewer), new FrameworkPropertyMetadata(Rect.Empty));
		public static readonly DependencyProperty VisiblePageAreaProperty = visiblePageAreaPropertyKey.DependencyProperty;
		private static readonly DependencyPropertyKey currentPageAreaPropertyKey = DependencyProperty.RegisterReadOnly(nameof(CurrentPageArea), typeof(Rect), typeof(PpsPdfViewer), new FrameworkPropertyMetadata(Rect.Empty));
		public static readonly DependencyProperty CurrentPageAreaProperty = currentPageAreaPropertyKey.DependencyProperty;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		private PdfReader pdf = null;

		private Size virtualSize = default(Size); // in 1/72inch, page units
		private Point viewOffset = default(Point); // in 1/72inch, page units
		private Size viewSize = default(Size); // total view size in page units
		private readonly double gapInInch; // gap in inch, between the pages
		private PageLayout[] pageSizes = null; // page layout in the virtual area, page units
		private Point scaleFactor; // scale factor to screen to wpf screen coordinates (not true pixel)
		private Point dpiScaleFactor; // scale factor to pixels

		private ScrollViewer scrollViewer = null; // attached scroll viewer
		private bool canVerticallyScroll = true;
		private bool canHorizontallyScroll = true;

		private readonly List<PageCache> currentPages = new List<PageCache>();

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary>Initialize pdf viewer primitive</summary>
		public PpsPdfViewer()
		{
			gapInInch = MillimeterToUnit(1.5);
			scaleFactor = GetScaleFactor(1.0);

			var dpi = VisualTreeHelper.GetDpi(this);
			dpiScaleFactor = new Point(dpi.DpiScaleX, dpi.DpiScaleY);
		} // ctor

		static PpsPdfViewer()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PpsPdfViewer), new FrameworkPropertyMetadata(typeof(PpsPdfViewer)));
		}

		#endregion

		#region -- Unit Calculation ---------------------------------------------------

		// unit is defined as 1/72 inch
		private const double mmPerInch = 25.4; // mm
		private const double unitsPerInch = 72; // 1/inch

		/// <summary>Calculate page units to mm.</summary>
		/// <param name="unit">Page units.</param>
		/// <returns>Millimeters</returns>
		public static double UnitToMillimeter(double unit)
			=> unit * mmPerInch / unitsPerInch;

		/// <summary>Calculate page units to inch.</summary>
		/// <param name="unit">Page units.</param>
		/// <returns>Inchs</returns>
		public static double UnitToInch(double unit)
			=> unit / unitsPerInch;

		/// <summary>Calculate page mm to page units.</summary>
		/// <param name="mm">Millimeter.</param>
		/// <returns>Page units.</returns>
		public static double MillimeterToUnit(double mm)
			=> mm * unitsPerInch / mmPerInch;

		/// <summary>Calculate inch to page units.</summary>
		/// <param name="inch">Inchs</param>
		/// <returns>Page units.</returns>
		public static double InchToUnit(double inch)
			=> inch * unitsPerInch;

		/// <summary>Transform page units to wpf screen units.</summary>
		/// <param name="pageUnits">Page units.</param>
		/// <returns>Wpf screen units.</returns>
		public double ToScreenUnitsX(double pageUnits)
			=> pageUnits * scaleFactor.X;

		/// <summary>Transform page units to wpf screen units.</summary>
		/// <param name="pageUnits">Page units.</param>
		/// <returns>Wpf screen units.</returns>
		public double ToScreenUnitsY(double pageUnits)
			=> pageUnits * scaleFactor.Y;

		/// <summary>Transform page units to wpf screen units.</summary>
		/// <param name="pagePoint">Page units</param>
		/// <returns>Wpf screen units.</returns>
		public Point ToScreenUnits(Point pagePoint)
			=> new Point(pagePoint.X * scaleFactor.X, pagePoint.Y * scaleFactor.Y);

		/// <summary>Transform page units to wpf screen units.</summary>
		/// <param name="pageSize">Page units</param>
		/// <returns>Wpf screen units.</returns>
		public Size ToScreenUnits(Size pageSize)
			=> new Size(pageSize.Width * scaleFactor.X, pageSize.Height * scaleFactor.Y);

		/// <summary>Transform page units to wpf screen units.</summary>
		/// <param name="pageRect">Page units</param>
		/// <returns>Wpf screen units.</returns>
		public Rect ToScreenUnits(Rect pageRect)
			=> new Rect(pageRect.X * scaleFactor.X, pageRect.Y * scaleFactor.Y, pageRect.Width * scaleFactor.X, pageRect.Height * scaleFactor.Y);

		/// <summary>Transform wpf screen units to page units.</summary>
		/// <param name="screenUnits">Wpf screen units.</param>
		/// <returns>Page units</returns>
		public double ToVirtualUnitsX(double screenUnits)
			=> screenUnits / scaleFactor.X;

		/// <summary>Transform wpf screen units to page units.</summary>
		/// <param name="screenUnits">Wpf screen units.</param>
		/// <returns>Page units</returns>
		public double ToVirtualUnitsY(double screenUnits)
			=> screenUnits / scaleFactor.Y;

		/// <summary>Transform wpf screen units to page units.</summary>
		/// <param name="screenPoint">Wpf screen units.</param>
		/// <returns>Page units</returns>
		public Point ToVirtualUnits(Point screenPoint)
			=> new Point(screenPoint.X / scaleFactor.X, screenPoint.Y / scaleFactor.Y);

		/// <summary>Transform wpf screen units to page units.</summary>
		/// <param name="screenSize">Wpf screen units.</param>
		/// <returns>Page units</returns>
		public Size ToVirtualUnits(Size screenSize)
			=> new Size(screenSize.Width / scaleFactor.X, screenSize.Height / scaleFactor.Y);

		/// <summary>Transform wpf screen units to page units.</summary>
		/// <param name="screenRect">Wpf screen units.</param>
		/// <returns>Page units</returns>
		public Rect ToVirtualUnits(Rect screenRect)
			=> new Rect(screenRect.X / scaleFactor.X, screenRect.Y / scaleFactor.Y, screenRect.Width / scaleFactor.X, screenRect.Height / scaleFactor.Y);

		#endregion

		#region -- IScrollInfo members  -----------------------------------------------

		// we use virtual units, same scroll distance in different scales for the user
		private double ScrollStepX => ToVirtualUnitsX(MillimeterToUnit(10.0));
		private double ScrollStepY => ToVirtualUnitsY(MillimeterToUnit(10.0));

		void IScrollInfo.LineUp()
			=> ScrollToUnit(viewOffset.X, viewOffset.Y - ScrollStepY);

		void IScrollInfo.LineDown()
			=> ScrollToUnit(viewOffset.X, viewOffset.Y + ScrollStepY);

		void IScrollInfo.LineLeft()
			=> ScrollToUnit(viewOffset.X - ScrollStepX, viewOffset.Y);

		void IScrollInfo.LineRight()
			=> ScrollToUnit(viewOffset.X + ScrollStepX, viewOffset.Y);

		void IScrollInfo.PageUp()
			=> ScrollToUnit(viewOffset.X, viewOffset.Y - viewSize.Height);

		void IScrollInfo.PageDown()
			=> ScrollToUnit(viewOffset.X, viewOffset.Y + viewSize.Height);

		void IScrollInfo.PageLeft() { }
		void IScrollInfo.PageRight() { }

		void IScrollInfo.MouseWheelUp()
			=> ScrollToUnit(viewOffset.X, viewOffset.Y - ScrollStepY * 3);

		void IScrollInfo.MouseWheelDown()
			=> ScrollToUnit(viewOffset.X, viewOffset.Y + ScrollStepY * 3);

		void IScrollInfo.MouseWheelLeft()
			=> ScrollToUnit(viewOffset.X - ScrollStepX * 3, viewOffset.Y);

		void IScrollInfo.MouseWheelRight()
			=> ScrollToUnit(viewOffset.X + ScrollStepX * 3, viewOffset.Y);

		void IScrollInfo.SetHorizontalOffset(double offset)
			=> ScrollToUnit(ToVirtualUnitsX(offset), viewOffset.Y);

		void IScrollInfo.SetVerticalOffset(double offset)
			=> ScrollToUnit(viewOffset.X, ToVirtualUnitsY(offset));

		Rect IScrollInfo.MakeVisible(Visual visual, Rect rectangle)
		{
			return rectangle;
		}

		bool IScrollInfo.CanVerticallyScroll { get => canVerticallyScroll; set => canVerticallyScroll = value; }
		bool IScrollInfo.CanHorizontallyScroll { get => canHorizontallyScroll; set => canHorizontallyScroll = value; }

		double IScrollInfo.ExtentWidth => ToScreenUnitsX(virtualSize.Width);
		double IScrollInfo.ExtentHeight => ToScreenUnitsY(virtualSize.Height);

		double IScrollInfo.ViewportWidth => RenderSize.Width;
		double IScrollInfo.ViewportHeight => RenderSize.Height;

		double IScrollInfo.HorizontalOffset => ScreenOffsetX;
		double IScrollInfo.VerticalOffset => ScreenOffsetY;

		ScrollViewer IScrollInfo.ScrollOwner { get => scrollViewer; set => scrollViewer = value; }

		#endregion

		#region -- Page Layout --------------------------------------------------------

		private static void OnPdfSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsPdfViewer)d).OnPdfSourceChanged((PdfReader)e.NewValue, (PdfReader)e.OldValue);

		private void OnPdfSourceChanged(PdfReader newValue, PdfReader oldValue)
		{
			pdf = newValue;

			// clear page cache
			currentPages.ForEach(c => c.Clean());
			currentPages.Clear();

			// analyse document
			if (pdf == null)
			{
				pageSizes = null;
				viewOffset = default(Point);
				viewSize = default(Size);
				virtualSize = default(Size);

				CurrentPageNumber = -1;
				PageCount = 0;

				scrollViewer?.InvalidateScrollInfo();
			}
			else
				MeasureDocumentSize();
		} // proc OnPdfSourceChanged

		/// <summary>Calculates the virtual layout of the control.</summary>
		private void MeasureDocumentSize()
		{
			var w = 0.0;
			var h = 0.0;

			// create new layout
			pageSizes = new PageLayout[pdf.PageCount];
			for (var i = 0; i < pdf.PageCount; i++)
			{
				if (i > 0)
					h += gapInInch; // space between

				var sz = pdf.GetPageSize(i);
				ref var pageLayout = ref pageSizes[i];
				pageLayout.Area = new Rect(0.0, h, sz.Width, sz.Height);
				pageLayout.Rotation = 0;

				w = Math.Max(w, sz.Width);
				h += sz.Height;
			}

			for (var i = 0; i < pageSizes.Length; i++)
				pageSizes[i].Area.X = (w - pageSizes[i].Area.Width) / 2;

			// reset scroll information
			virtualSize = new Size(w, h);
			viewOffset = new Point(0, 0);

			CurrentPageNumber = pageSizes.Length > 0 ? 0 : -1;
			PageCount = pdf.PageCount;

			scrollViewer?.InvalidateScrollInfo();
		} // proc MeasureDocumentSize

		/// <summary>Invalid render cache.</summary>
		/// <param name="sizeInfo"></param>
		protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
		{
			base.OnRenderSizeChanged(sizeInfo);
			InvalidateView(sizeInfo.NewSize);
		} // proc OnRenderSizeChanged

		private void GetVisiblePageArea(int pageNumber, out Rect pageArea, out Rect visibleArea)
		{
			if (!IsValidPageNumber(pageNumber))
				goto EmptyResult;

			ref var pageLayout = ref pageSizes[pageNumber];
			pageArea = pageLayout.Area;
			visibleArea = Rect.Intersect(new Rect(viewOffset, viewSize), pageArea);
			if (visibleArea.IsEmpty)
				goto EmptyResult;

			visibleArea.Offset(-pageLayout.Area.Left, -pageLayout.Area.Top);
			pageArea.Offset(-pageLayout.Area.Left, -pageLayout.Area.Top);

			return;

			EmptyResult:
			pageArea = Rect.Empty;
			visibleArea = Rect.Empty;
		} // func GetVisiblePageArea

		private void InvalidVisiblePageAreaRect()
		{
			GetVisiblePageArea(CurrentPageNumber, out var pageArea, out var pageVisible);

			SetValue(currentPageAreaPropertyKey, pageArea);
			SetValue(visiblePageAreaPropertyKey, pageVisible);
		} // proc InvalidVisiblePageAreaRect

		/// <summary>Scroll to a page.</summary>
		/// <param name="pageNumber">Number of the page.</param>
		/// <returns><c>true</c>, if the page could be made visible.</returns>
		public bool ScrollToPage(int pageNumber)
		{
			if (!IsValidPageNumber(pageNumber))
				return false;

			var x = pageSizes[pageNumber].Area.Top;
			var y = pageSizes[pageNumber].Area.Height < viewSize.Height // page needs to center
				? pageSizes[pageNumber].Area.Top - (viewSize.Height - pageSizes[pageNumber].Area.Height) / 2
				: pageSizes[pageNumber].Area.Left;

			return ScrollToUnit(x, y);
		} // proc ScrollToPage

		/// <summary>Scroll to page unit.</summary>
		/// <param name="x">x position in the virtual page area.</param>
		/// <param name="y">y position in the virtual page area.</param>
		/// <returns><c>true</c>, if the position was changed.</returns>
		public bool ScrollToUnit(double x, double y)
		{
			// test upper and lower
			x = Math.Max(0.0, Math.Min(x, virtualSize.Width - viewSize.Width));
			y = Math.Max(0.0, Math.Min(y, virtualSize.Height - viewSize.Height));

			viewOffset = new Point(x, y);

			// update current page
			CurrentPageNumber = PageFromPosition(y + viewSize.Height / 2);

			// redraw
			scrollViewer?.InvalidateScrollInfo();
			InvalidVisiblePageAreaRect();
			InvalidateVisual();
			return true;
		} // func ScrollToPosition

		private int IsWithInPage(int pageNumber, double y)
		{
			ValidatePageNumber(pageNumber);

			ref var rc = ref pageSizes[pageNumber].Area;
			if (y < rc.Y)
				return -1;
			else if (y > rc.Y + rc.Height + gapInInch)
				return 1;
			else
				return 0;
		} // func IsWithInPage

		private int PageFromPosition(double y)
		{
			if (!IsValidDocument)
				return -1;

			// move page index near
			var idx = (int)(y * pageSizes.Length / virtualSize.Height);
			if (idx >= pageSizes.Length)
				idx = pageSizes.Length - 1;
			else if (idx < 0)
				idx = 0;

			// find correct page
			while (idx > 0 && IsWithInPage(idx, y) < 0)
				idx--;
			while (idx < pageSizes.Length && IsWithInPage(idx, y) > 0)
				idx++;

			return idx;
		} // func PageFromPosition

		private void ValidatePageNumber(int pageNumber)
		{
			if (!IsValidPageNumber(pageNumber))
				throw new ArgumentOutOfRangeException(nameof(pageNumber), pageNumber, "Invalid page number");
		} // proc ValidatePageNumber

		private bool IsValidPageNumber(int pageNumber)
			=> pageSizes != null && pageNumber >= 0 && pageNumber < pageSizes.Length;

		private static object OnCurrentPageNumberCoerce(DependencyObject d, object baseValue)
			=> ((PpsPdfViewer)d).OnCurrentPageNumberCoerce((int)baseValue);

		private int OnCurrentPageNumberCoerce(int baseValue)
		{
			if (IsValidDocument)
			{
				if (baseValue < 0)
					return 0;
				else if (baseValue >= pageSizes.Length)
					return pageSizes.Length - 1;
				else
					return baseValue;
			}
			else
				return -1;
		} // func OnCurrentPageNumberCoerce

		private static void OnCurrentPageNumberChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsPdfViewer)d).OnCurrentPageNumberChanged((int)e.NewValue, (int)e.OldValue);

		private void OnCurrentPageNumberChanged(int newValue, int oldValue)
		{
			if (newValue < 0)
				return;

			if (IsWithInPage(newValue, viewOffset.Y + viewSize.Height / 2) != 0)
				ScrollToPage(newValue);
		} // proc OnCurrentPageNumberChanged

		/// <summary>Set the page rotation state</summary>
		/// <param name="pageNumber"></param>
		/// <param name="rotation"></param>
		public void RotatePage(int pageNumber, int rotation)
		{
		} // proc RotatePage

		/// <summary>Return the current rotation state of the page.</summary>
		/// <param name="pageNumber"></param>
		/// <returns></returns>
		public int GetPageRotation(int pageNumber)
		{
			return 0;
		} // func GetPageRotation

		/// <summary>Current available pages.</summary>
		public int PageCount { get => (int)GetValue(PageCountProperty); private set => SetValue(pageCountPropertyKey, value); }
		/// <summary>Current visible page.</summary>
		public int CurrentPageNumber { get => (int)GetValue(CurrentPageNumberProperty); set => SetValue(CurrentPageNumberProperty, value); }

		#endregion

		#region -- Render Page --------------------------------------------------------

		private PageCache GetPageCacheEntry(int pageNumber, int cacheIndex)
		{
			// remove unused cached pages
			while (cacheIndex < currentPages.Count && currentPages[cacheIndex].PageNumber < pageNumber)
			{
				currentPages[cacheIndex].Clean();
				currentPages.RemoveAt(cacheIndex);
			}

			// insert page
			if (cacheIndex >= currentPages.Count || currentPages[cacheIndex].PageNumber > pageNumber)
				currentPages.Insert(cacheIndex, new PageCache(this, pageNumber));
		
			// return page cache entry
			return currentPages[cacheIndex];
		} // func GetPageCacheEntry

		private static void OnBackgroundChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsPdfViewer)d).InvalidateVisual();

		/// <summary></summary>
		/// <param name="dc"></param>
		protected override void OnRender(DrawingContext dc)
		{
			dc.DrawRectangle(Background, null, new Rect(new Point(0, 0), RenderSize));

			if (!IsValidDocument)
				return;

			// validate offset
			var xOfs = ToScreenUnitsX(Math.Max(0, Math.Min(viewOffset.X, virtualSize.Width - viewSize.Width)));
			var yOfs = ToScreenUnitsX(Math.Max(0, Math.Min(viewOffset.Y, virtualSize.Height - viewSize.Height)));

			// calc view rect
			var viewRect = new Rect(xOfs, yOfs, ToScreenUnitsX(viewSize.Width), ToScreenUnitsY(viewSize.Height));
			if (viewRect.Width < 1 || viewRect.Height < 1)
				return;

			// get first page
			var idx = PageFromPosition(viewOffset.Y);
			var cacheIndex = 0;
			var firstPage = true;
			while (idx < pageSizes.Length)
			{
				// calculate render page position
				var pageLayout = ToScreenUnits(pageSizes[idx].Area);

				// calculate draw rect
				var drawRect = Rect.Intersect(viewRect, pageLayout);
				if (drawRect.Width < 1 || drawRect.Height < 1)
				{
					if (firstPage)
					{
						idx++;
						continue;
					}
					break;
				}
				firstPage = false;

				// get page cache
				var pageCache = GetPageCacheEntry(idx, cacheIndex);

				// position on screen
				var pagePosition = new Rect(
					drawRect.X - xOfs,
					drawRect.Y - yOfs,
					drawRect.Width,
					drawRect.Height
				);

				if (pagePosition.Width < RenderSize.Width) // center page
					pagePosition.X = (RenderSize.Width - pagePosition.Width) / 2;

				pageCache.RenderFast(dc, pagePosition, drawRect, new Matrix(scaleFactor.X, 0.0, 0.0, scaleFactor.Y, -pageLayout.X, -pageLayout.Y));

				idx++;
				cacheIndex++;
			}

			// remove unused cache pages
			while (cacheIndex < currentPages.Count)
			{
				currentPages[cacheIndex].Clean();
				currentPages.RemoveAt(cacheIndex);
			}
		} // func OnRender

		private void InvalidateView(Size newRenderSize)
		{
			viewSize = ToVirtualUnits(newRenderSize);
			scrollViewer?.InvalidateScrollInfo();
			InvalidVisiblePageAreaRect();
			InvalidateVisual();
		} // proc InvalidateView

		#endregion

		#region -- Zoom ---------------------------------------------------------------

		/// <summary>Render params.</summary>
		/// <param name="oldDpi"></param>
		/// <param name="newDpi"></param>
		protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
		{
			base.OnDpiChanged(oldDpi, newDpi);
			scaleFactor = GetScaleFactor(Zoom, newDpi);
			dpiScaleFactor = new Point(newDpi.DpiScaleX, newDpi.DpiScaleY);
		} // proc OnDpiChanged 

		private Point GetScaleFactor(double zoom, DpiScale? newDpi = null)
		{
			var dpi = newDpi ?? VisualTreeHelper.GetDpi(this);
			return new Point(zoom * UnitToInch(dpi.PixelsPerInchX), zoom * UnitToInch(dpi.PixelsPerInchY));
		} // func GetScaleFactor

		private static object OnZoomCoerce(DependencyObject d, object baseValue)
			=> (double)baseValue < 0.01 ? 0.01 : baseValue;

		private static void OnZoomChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsPdfViewer)d).OnZoomChanged((double)e.NewValue, (double)e.OldValue);

		private void OnZoomChanged(double newValue, double oldValue)
		{
			scaleFactor = GetScaleFactor(newValue);

			// refresh view
			InvalidateView(RenderSize);
		} // proc OnZoomChanged

		/// <summary>Make page visible</summary>
		/// <param name="pageNumber">Page that will be visible</param>
		/// <param name="zoom">Zoom mode to this page.</param>
		/// <returns><c>true</c>, if operation was successful.</returns>
		public bool GotoPage(int pageNumber, PdfGotoMode zoom = PdfGotoMode.None)
		{
			if (!IsValidPageNumber(pageNumber))
				return false;

			if (zoom != PdfGotoMode.None)
			{
				var dpi = VisualTreeHelper.GetDpi(this);

				ref var pageLayout = ref pageSizes[pageNumber];
				var aspectX = RenderSize.Width / pageLayout.Area.Width / UnitToInch(dpi.PixelsPerInchX);
				var aspectY = RenderSize.Height / pageLayout.Area.Height / UnitToInch(dpi.PixelsPerInchY);

				if (zoom == PdfGotoMode.ZoomXorY)
				{
					if (aspectX * RenderSize.Height > RenderSize.Width)
						zoom = PdfGotoMode.ZoomY;
					else
						zoom = PdfGotoMode.ZoomX;
				}

				Zoom = zoom == PdfGotoMode.ZoomY ? Math.Min(aspectX, aspectY) : Math.Max(aspectX, aspectY);
				return ScrollToUnit(pageLayout.Area.X, pageLayout.Area.Y);
			}
			else
				return ScrollToPage(pageNumber);
		} // proc GotoPage

		/// <summary>Goto a specific pdf destination.</summary>
		/// <param name="destination">Pdf destination description</param>
		/// <returns><c>true</c>, if operation was successful.</returns>
		public bool GotoDestination(PdfDestination destination)
		{
			if (!IsValidPageNumber(destination.PageNumber))
				return false;

			ref var pageLayout = ref pageSizes[destination.PageNumber];
			var gotoOffset = pageLayout.Area.TopLeft;
			gotoOffset.Offset(destination.PageX ?? 0, pageLayout.Area.Height - destination.PageY ?? 0);
			return ScrollToUnit(gotoOffset.X, gotoOffset.Y);
		} // func GotoDestination

		/// <summary>Scroll position.</summary>
		public double VirtualOffsetX => viewOffset.X;
		/// <summary>Scroll position.</summary>
		public double VirtualOffsetY => viewOffset.Y;
		/// <summary>Scroll position.</summary>
		public double ScreenOffsetX => ToScreenUnitsX(viewOffset.X);
		/// <summary>Scroll position.</summary>
		public double ScreenOffsetY => ToScreenUnitsY(viewOffset.Y);

		/// <summary>Page zoom.</summary>
		public double Zoom
		{
			get => (double)GetValue(ZoomProperty);
			set => SetValue(ZoomProperty, value);
		} // prop Zoom

		/// <summary>Visible page area in page units.</summary>
		public Rect CurrentPageArea => (Rect)GetValue(CurrentPageAreaProperty);

		/// <summary>Visible page area in page units.</summary>
		public Rect VisiblePageArea => (Rect)GetValue(VisiblePageAreaProperty);

		#endregion

		/// <summary>Pdf control background.</summary>
		public Brush Background { get => (Brush)GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }
		/// <summary>Is a valid document setted.</summary>
		public bool IsValidDocument => pageSizes != null && pageSizes.Length > 0;
		/// <summary>Set a pdf source to render.</summary>
		public PdfReader Document { get => (PdfReader)GetValue(DocumentProperty); set => SetValue(DocumentProperty, value); }

		internal PdfReader DocumentUnsafe => pdf;
	} // class PpsPdfViewer

	#endregion

	#region -- class PpsPdfPageViewer -------------------------------------------------

	/// <summary>Primitve to render only one pdf page.</summary>
	public class PpsPdfPageViewer : FrameworkElement
	{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		private static readonly DependencyPropertyKey hasPagePropertyKey = DependencyProperty.RegisterReadOnly(nameof(HasPage), typeof(bool), typeof(PpsPdfPageViewer), new FrameworkPropertyMetadata(false));
		public static readonly DependencyProperty HasPageProperty = hasPagePropertyKey.DependencyProperty;
		public static readonly DependencyProperty DocumentProperty = DependencyProperty.Register(nameof(Document), typeof(PdfReader), typeof(PpsPdfPageViewer), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnDocumentChanged)));
		public static readonly DependencyProperty PageNumberProperty = DependencyProperty.Register(nameof(PageNumber), typeof(int), typeof(PpsPdfPageViewer), new FrameworkPropertyMetadata(-1, new PropertyChangedCallback(OnPageNumberChanged)));
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		#region -- class BackgroundRenderer -------------------------------------------

		private sealed class BackgroundRenderer
		{
			private readonly PpsPdfPageViewer viewer;

			// rendered result
			private int pageNumber = -1;
			private int width = -1;
			private int height = -1;
			private ImageSource image = null;

			private CancellationTokenSource currentRenderCancellation = null;
			private int lastPageNumber = -1;
			private int lastWidth = -1;
			private int lastHeight = -1;

			private bool isDead = false;

			private readonly object renderLock = new object();

			public BackgroundRenderer(PpsPdfPageViewer viewer)
			{
				this.viewer = viewer ?? throw new ArgumentNullException(nameof(viewer));
			} // ctor

			public void Clean()
			{
				lock (renderLock)
				{
					StopRenderUnsafe();

					isDead = true;
					image = null;
				}
			} // proc Clean

			private void EnqueueRender(int drawPageNumber, int drawWidth, int drawHeight)
			{
				lock (renderLock)
				{
					if (drawPageNumber == lastPageNumber && drawWidth == lastWidth && drawHeight == lastHeight)
						return;

					//Debug.Print("Enqueue: {0}", pageNumber);
					StopRenderUnsafe();

					lastPageNumber = drawPageNumber;
					lastWidth = drawWidth;
					lastHeight = drawHeight;

					currentRenderCancellation = new CancellationTokenSource();
					var token = currentRenderCancellation.Token;
					var currentRenderTask = Task.Run(() => RenderCore(drawPageNumber, drawWidth, drawHeight, token));
					currentRenderTask.ContinueWith(InvalidViewer, TaskContinuationOptions.OnlyOnRanToCompletion);
				}
			} // proc EnqueueRender

			private void InvalidViewer(Task t)
			{
				if (!isDead)
					viewer.Dispatcher.BeginInvoke(new Action(() => viewer.InvalidateVisual()));
			} // proc InvalidViewer

			private void StopRenderUnsafe()
			{
				currentRenderCancellation?.Cancel();
				currentRenderCancellation = null;
			} // proc StopRenderUnsafe

			private async Task RenderCore(int drawPageNumber, int drawWidth, int drawHeight, CancellationToken cancellationToken)
			{
				// wait for changes
				await Task.Delay(20);

				ImageSource newImage;

				// cancellation requested
				if (cancellationToken.IsCancellationRequested)
					return;

				// start render
				using (var page = viewer.DocumentUnsafe.OpenPage(drawPageNumber))
					newImage = page.Render((int)(drawWidth * viewer.dpiScaleFactor.X), (int)(drawHeight * viewer.dpiScaleFactor.Y), false);

				newImage.Freeze();

				if (cancellationToken.IsCancellationRequested)
					return;

				lock (renderLock)
				{
					image = newImage;
					pageNumber = drawPageNumber;
					width = drawWidth;
					height = drawHeight;
				}
			} // func RenderCore

			public void RenderFast(DrawingContext dc, int drawPageNumber, Rect rc)
			{
				//Debug.Print("Paint: {0}", pageNumber);
				var rectWidth = (int)rc.Width;
				var rectHeight = (int)rc.Height;

				if (drawPageNumber == pageNumber && image != null)
				{
					dc.DrawImage(image, rc);
					if (rectWidth != width || rectHeight != height)
						EnqueueRender(drawPageNumber, rectWidth, rectHeight); // start render, size has changed
				}
				else
				{
					// white page
					dc.DrawRectangle(Brushes.White, null, rc);

					// start render, page has changed
					EnqueueRender(drawPageNumber, rectWidth, rectHeight);
				}
			} // proc RenderFast
		} // class BackgroundRenderer

		#endregion

		private double currentPageAspect = 1.0;
		private Point dpiScaleFactor; // scale factor to pixels
		private readonly BackgroundRenderer backgroundRenderer;

		private PdfReader pdf;

		/// <summary>Pdf page viewer primitive.</summary>
		public PpsPdfPageViewer()
		{
			backgroundRenderer = new BackgroundRenderer(this);

			var dpi = VisualTreeHelper.GetDpi(this);
			dpiScaleFactor = new Point(dpi.DpiScaleX, dpi.DpiScaleY);
		} // ctor

		private bool GetPageInfo(out PdfReader pdf, out int pageNumber)
		{
			pdf = Document;
			pageNumber = PageNumber;

			return pdf != null && pageNumber >= 0 && pageNumber < pdf.PageCount;
		} // func GetPageInfo

		/// <summary>Enforce aspect of the pdf-page.</summary>
		/// <param name="availableSize"></param>
		/// <returns></returns>
		protected override Size MeasureOverride(Size availableSize)
		{
			var newHeight = availableSize.Width * currentPageAspect;
			if (newHeight > availableSize.Height)
			{
				var newWidth = availableSize.Height / currentPageAspect;
				return new Size(newWidth, availableSize.Height);
			}
			else
				return new Size(availableSize.Width, newHeight);
		} // func MeasureOverride

		private void InvalidPage()
		{
			if (GetPageInfo(out var pdf, out var pageNumber))
			{
				var pageSize = pdf.GetPageSize(pageNumber);
				currentPageAspect = pageSize.Height / pageSize.Width;
			}
			else
			{
				currentPageAspect = 1.0;
				HasPage = false;
			}

			// redo element
			InvalidateMeasure();
			InvalidateVisual();
		} // proc InvalidPage

		/// <summary></summary>
		/// <param name="oldDpi"></param>
		/// <param name="newDpi"></param>
		protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
		{
			dpiScaleFactor = new Point(newDpi.DpiScaleX, newDpi.DpiScaleY);
			base.OnDpiChanged(oldDpi, newDpi);
		} // proc OnDpiChanged

		/// <summary></summary>
		/// <param name="dc"></param>
		protected override void OnRender(DrawingContext dc)
		{
			if (GetPageInfo(out var pdf, out var pageNumber))
			{
				var width = (int)RenderSize.Width;
				var height = (int)RenderSize.Height;
				if (width > 0 && height > 0)
					backgroundRenderer.RenderFast(dc, pageNumber, new Rect(new Point(0.0, 0.0), RenderSize));
			}
		} // proc OnRender

		private static void OnDocumentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsPdfPageViewer)d).OnDocumentChanged((PdfReader)e.NewValue);

		private void OnDocumentChanged(PdfReader newValue)
		{
			pdf = newValue;
			InvalidPage();
		} // proc OnDocumentChanged

		private static void OnPageNumberChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsPdfPageViewer)d).InvalidPage();

		/// <summary>Pdf document.</summary>
		public PdfReader Document { get => (PdfReader)GetValue(DocumentProperty); set => SetValue(DocumentProperty, value); }

		internal PdfReader DocumentUnsafe => pdf;

		/// <summary>Page number.</summary>
		public int PageNumber { get => (int)GetValue(PageNumberProperty); set => SetValue(PageNumberProperty, value); }

		/// <summary>Has this control a page in view.</summary>
		public bool HasPage { get => (bool)GetValue(HasPageProperty); private set => SetValue(hasPagePropertyKey, value); }
	} // class PpsPdfPageViewer

	#endregion
}
