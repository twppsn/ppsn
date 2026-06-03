using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;

namespace TecWare.PPSn.UI
{
	public class PpsDragAdorner : Adorner
	{
		private readonly UIElement adornerElement;
		private AdornerLayer adornerLayer;
		private Point offset;

		public PpsDragAdorner(UIElement adornedElement, UIElement adornerElement, Point offset) : base(adornedElement)
		{
			this.adornerElement = adornerElement;
			this.offset = offset;

			adornerLayer = AdornerLayer.GetAdornerLayer(adornedElement);

			// Das Vorschaubild ist für Mausereignisse unsichtbar 
			adornerElement.IsHitTestVisible = false;
		} // ctor

		#region -- Drawing Overrides --------------------------------------------------
		protected override Size MeasureOverride(Size constraint)
		{
			adornerElement.Measure(constraint);
			return adornerElement.DesiredSize;
		} // proc MeasureOverride

		protected override Size ArrangeOverride(Size finalSize) 
		{
			// Laut Dr. Google sollte die Positionierung hier und nicht im OnRender stattfinden 
			adornerElement.Arrange(new Rect(offset, adornerElement.DesiredSize));
			return finalSize;
		} // proc ArrangeOverride

		protected override void OnRender(DrawingContext drawingContext)
		{
			// Sollten wir hier einen RenderBrush definieren oder dies dem Adorner-Nutzer überlassen? 
		} // proc OnRender

		#endregion

		protected override int VisualChildrenCount => 1;

		protected override Visual GetVisualChild(int index) => adornerElement;
	} // class PpsDragAdorner
}
