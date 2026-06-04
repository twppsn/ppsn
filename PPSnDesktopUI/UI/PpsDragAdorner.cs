using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;

namespace TecWare.PPSn.UI
{
	public class PpsDragAdorner : Adorner
	{
		private readonly UIElement dragElement;
		private readonly VisualBrush brush;
		private Point offset;

		/// <param name="adornedElement">The element the adorner is bound to.</param>
		/// <param name="dragElement">The element we want to drag & drop.</param>
		/// <param name="opacity">The opacity of the dragElement.</param>
		public PpsDragAdorner(UIElement adornedElement, UIElement dragElement, float opacity) : base(adornedElement)
		{
			this.dragElement = dragElement;

			brush = new VisualBrush(adornedElement)
			{
				Opacity = opacity

			};

			// Das Vorschaubild ist für Mausereignisse unsichtbar 
			dragElement.IsHitTestVisible = false;
		} // ctor

		#region -- Drawing Overrides --------------------------------------------------
		protected override Size MeasureOverride(Size constraint)
		{
			dragElement.Measure(constraint);
			return dragElement.DesiredSize;
		} // proc MeasureOverride

		protected override Size ArrangeOverride(Size finalSize) 
		{
			// Laut Dr. Google sollte die Positionierung hier und nicht im OnRender stattfinden 
			dragElement.Arrange(new Rect(offset, dragElement.DesiredSize));
			return finalSize;
		} // proc ArrangeOverride

		protected override void OnRender(DrawingContext drawingContext)
		{
			var size = dragElement.RenderSize;
			var rect = new Rect(offset.X, offset.Y, size.Width, size.Height);
			drawingContext.DrawRectangle(brush, null, rect); // Ist das so korrekt? 
		} // proc OnRender

		#endregion

		/// <summary>Set new Offset (position) and invalidate visual.</summary>
		public void SetOffset(Point newOffset)
		{
			offset = newOffset;
			InvalidateVisual();
		} // proc SetOffset

		protected override int VisualChildrenCount => 1;

		protected override Visual GetVisualChild(int index) => dragElement;
	} // class PpsDragAdorner
}
