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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TecWare.PPSn.Controls
{
	/// <summary>Primitive to create a visible mask. Can be used to show the scroll viewer visible area.</summary>
	public class PpsVisibleBox : FrameworkElement
	{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public static readonly DependencyProperty BaseRectangleProperty = DependencyProperty.Register(nameof(BaseRectangle), typeof(Rect), typeof(PpsVisibleBox), new FrameworkPropertyMetadata(Rect.Empty, new PropertyChangedCallback(OnBaseRectangleChanged)));
		public static readonly DependencyProperty VisibleRectangleProperty = DependencyProperty.Register(nameof(VisibleRectangle), typeof(Rect), typeof(PpsVisibleBox), new FrameworkPropertyMetadata(Rect.Empty, new PropertyChangedCallback(OnVisibleRectangleChanged)));
		public static readonly DependencyProperty ForegroundProperty = Control.ForegroundProperty.AddOwner(typeof(PpsVisibleBox), new FrameworkPropertyMetadata(Brushes.Black, new PropertyChangedCallback(OnForegroundChanged)));

		private static readonly DependencyPropertyKey visibleAreaPropertyKey = DependencyProperty.RegisterReadOnly(nameof(VisibleArea), typeof(double), typeof(PpsVisibleBox), new FrameworkPropertyMetadata(1.0));
		public static readonly DependencyProperty VisibleAreaProperty = visibleAreaPropertyKey.DependencyProperty;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		private static void OnBaseRectangleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsVisibleBox)d).OnRectangleRecalc();

		private static void OnVisibleRectangleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsVisibleBox)d).OnRectangleRecalc();

		private static void OnForegroundChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsVisibleBox)d).InvalidateVisual();

		private void OnRectangleRecalc()
		{
			var rcBase = BaseRectangle;
			var rcVisible =  Rect.Intersect(BaseRectangle, VisibleRectangle);

			VisibleArea = (rcVisible.Width * rcVisible.Height) / (rcBase.Width * rcBase.Height);

			InvalidateVisual();
		} // proc OnRectangleRecalc

		/// <summary></summary>
		/// <param name="dc"></param>
		protected override void OnRender(DrawingContext dc)
		{
			var rc = BaseRectangle;

			var scaleX = RenderSize.Width /  rc.Width;
			var scaleY = RenderSize.Height / rc.Height;

			var geometry = new CombinedGeometry(GeometryCombineMode.Exclude,
				new RectangleGeometry(BaseRectangle),
				new RectangleGeometry(VisibleRectangle),
				new MatrixTransform(scaleX, 0.0, 0.0, scaleY, -rc.X, -rc.Y)
			);

			dc.DrawGeometry(Foreground, null, geometry);
		} // proc OnRender

		/// <summary>Brush for the invisible area.</summary>
		public Brush Foreground { get => (Brush)GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
		/// <summary>Base rectangle to scale the element rectangle</summary>
		public Rect BaseRectangle { get => (Rect)GetValue(BaseRectangleProperty); set => SetValue(BaseRectangleProperty, value); }
		/// <summary>Visible rect in the current base rectangle.</summary>
		public Rect VisibleRectangle { get => (Rect)GetValue(VisibleRectangleProperty); set => SetValue(VisibleRectangleProperty, value); }
		/// <summary>Relative visible area.</summary>
		public double VisibleArea { get => (double)GetValue(VisibleAreaProperty); private set => SetValue(visibleAreaPropertyKey, value); }
	} // class PpsVisibleBox
}
