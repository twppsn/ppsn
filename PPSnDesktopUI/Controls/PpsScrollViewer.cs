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
using System.Windows.Input;
using System.Windows.Media;
using TecWare.PPSn.UI;

namespace TecWare.PPSn.Controls
{
	#region -- class PpsScrollViewer --------------------------------------------------

	/// <summary>ScrollViewer that supports touch gestures for panning and zoom.</summary>
	public class PpsScrollViewer : ScrollViewer
	{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public static readonly DependencyProperty ScaleFactorProperty = DependencyProperty.Register(nameof(ScaleFactor), typeof(double), typeof(PpsScrollViewer), new FrameworkPropertyMetadata(1.0));
		public static readonly DependencyProperty MinScaleFactorProperty = DependencyProperty.Register(nameof(MinScaleFactor), typeof(double), typeof(PpsScrollViewer), new FrameworkPropertyMetadata(0.1));
		public static readonly DependencyProperty MaxScaleFactorProperty = DependencyProperty.Register(nameof(MaxScaleFactor), typeof(double), typeof(PpsScrollViewer), new FrameworkPropertyMetadata(20.0));
		public static readonly DependencyProperty IsZoomAllowedProperty = DependencyProperty.Register(nameof(IsZoomAllowed), typeof(bool), typeof(PpsScrollViewer), new FrameworkPropertyMetadata(BooleanBox.False));
		public static readonly DependencyProperty IsPanningAllowedProperty = DependencyProperty.Register(nameof(IsPanningAllowed), typeof(bool), typeof(PpsScrollViewer), new FrameworkPropertyMetadata(BooleanBox.False));
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		#region -- Transform primitives -----------------------------------------------

		private Matrix BeginContentTransform()
			=> new Matrix(ScaleFactor, 0.0, 0.0, ScaleFactor, -HorizontalOffset, -VerticalOffset);

		private void UpdateContentTransform(Matrix matrix, Point origin)
		{
			// first scale image
			var newScaleFactor = matrix.M11;
			if (newScaleFactor < MinScaleFactor)
			{
				var diff = MinScaleFactor - newScaleFactor;
				ScaleRelative(ref matrix, diff, origin);
			}
			else if (newScaleFactor > MaxScaleFactor)
			{
				var diff = MaxScaleFactor - newScaleFactor;
				ScaleRelative(ref matrix, diff, origin);
			}

			// update scale factor
			ScaleFactor = matrix.M11;

			// update position
			ScrollToHorizontalOffset(-matrix.OffsetX);
			ScrollToVerticalOffset(-matrix.OffsetY);
		} // proc UpdateContentTransform

		private static void ScaleRelative(ref Matrix matrix, double relScale, Point origin)
		{
			var delta = (matrix.M11 + relScale) / matrix.M11;
			if (delta != 0.0)
				matrix.ScaleAt(delta, delta, origin.X, origin.Y);
		} // proc ScaleReltive

		#endregion

		#region -- Mouse Transform ----------------------------------------------------

		#region -- class MouseTransformInfo -------------------------------------------

		private sealed class MouseTransformInfo
		{
			public MouseTransformInfo(Point origin, Matrix matrix)
			{
				Matrix = matrix;
				Origin = origin;
				IsStarted = false;
			} // ctor

			public Matrix Translate(Point currentPoint)
			{
				var rel = currentPoint - Origin;
				Matrix.Translate(rel.X, rel.Y);
				Origin = currentPoint;
				return Matrix;
			} // proc Translate

			public Matrix Matrix;
			public Point Origin { get; private set; }
			public bool IsStarted;
		} // class MouseTransformInfo

		#endregion

		private MouseTransformInfo mouseTransformInfo = null;

		/// <summary>Mouse wheel functions. Ctrl-Wheel for zoom and normal wheel for scrolling.</summary>
		/// <param name="e"></param>
		protected override void OnMouseWheel(MouseWheelEventArgs e)
		{
			if (!e.Handled)
			{
				if (IsZoomAllowed && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
				{
					var origin = e.GetPosition(this);
					var m = BeginContentTransform();
					ScaleRelative(ref m, e.Delta / 1200.0, origin);
					UpdateContentTransform(m, origin);
					e.Handled = true;
				}
				else if (ScrollInfo != null && IsPanningAllowed)
				{
					if (e.Delta < 0)
						ScrollInfo.MouseWheelDown();
					else
						ScrollInfo.MouseWheelUp();
				}
			}
			base.OnMouseWheel(e);
		} // proc OnMouseWheel

		/// <summary>Panning emulation for mouse.</summary>
		/// <param name="e"></param>
		protected override void OnMouseDown(MouseButtonEventArgs e)
		{
			if (Keyboard.Modifiers == ModifierKeys.None && IsPanningAllowed)
			{
				if (Mouse.Capture(this, CaptureMode.Element))
				{
					mouseTransformInfo = new MouseTransformInfo(e.GetPosition(this), BeginContentTransform());
					Mouse.OverrideCursor = Cursors.Hand;
					e.Handled = true;
				}
			}
			base.OnMouseDown(e);
		} // proc OnMouseDown

		/// <summary>Panning emulation for mouse.</summary>
		/// <param name="e"></param>
		protected override void OnMouseMove(MouseEventArgs e)
		{
			if (mouseTransformInfo != null)
			{
				// check minimal movement
				if (!mouseTransformInfo.IsStarted)
				{
					var rel = mouseTransformInfo.Origin - e.GetPosition(this);
					if (rel.X < -5.0 || rel.X > 5.0 || rel.Y < -5.0 || rel.Y > 5.0)
						mouseTransformInfo.IsStarted = true;
				}

				// simulate translation
				if (mouseTransformInfo.IsStarted)
				{
					var origin = e.GetPosition(this);
					UpdateContentTransform(mouseTransformInfo.Translate(origin), origin);
				}

				e.Handled = true;
			}
			base.OnMouseMove(e);
		} // proc OnMouseMove

		/// <summary>Panning emulation for mouse.</summary>
		/// <param name="e"></param>
		protected override void OnMouseUp(MouseButtonEventArgs e)
		{
			if (Mouse.Captured == this)
			{
				mouseTransformInfo = null;
				Mouse.Capture(null);
				Mouse.OverrideCursor = null;
				e.Handled = true;
			}

			base.OnMouseUp(e);
		} // proc OnMouseUp

		#endregion

		#region -- Touch Transform ----------------------------------------------------

		private Matrix? touchTransformMatrix = null;

		/// <summary>Overwrite panning and zoom implementation fo the scroll viewer.</summary>
		/// <param name="e"></param>
		protected override void OnManipulationStarting(ManipulationStartingEventArgs e)
		{
			var panningMode = PanningMode;

			if (panningMode == PanningMode.None)
				return;

			if (e.OriginalSource != this)
			{
				e.ManipulationContainer = this;

				// set manipulation mopde
				e.Mode = ManipulationModes.None;
				switch (panningMode)
				{
					case PanningMode.Both:
					case PanningMode.HorizontalFirst:
					case PanningMode.VerticalFirst:
						e.Mode |= ManipulationModes.Translate;
						break;
					case PanningMode.HorizontalOnly:
						e.Mode |= ManipulationModes.TranslateX;
						break;
					case PanningMode.VerticalOnly:
						e.Mode |= ManipulationModes.TranslateY;
						break;
				}
				if (IsZoomAllowed)
					e.Mode |= ManipulationModes.Scale;

				// begin touch transform matrix
				touchTransformMatrix = BeginContentTransform();
			}
			else
			{
				e.Cancel();
				touchTransformMatrix = null;
			}

			e.Handled = true;
		} // proc OnManipulationStarting

		/// <summary>Overwrite panning and zoom implementation fo the scroll viewer.</summary>
		/// <param name="e"></param>
		protected override void OnManipulationStarted(ManipulationStartedEventArgs e) { }

		/// <summary>Overwrite panning and zoom implementation fo the scroll viewer.</summary>
		/// <param name="e"></param>
		protected override void OnManipulationDelta(ManipulationDeltaEventArgs e)
		{
			if (!touchTransformMatrix.HasValue)
				return;

			touchTransformMatrix.Value.ScaleAt(e.DeltaManipulation.Scale.X, e.DeltaManipulation.Scale.Y, e.ManipulationOrigin.X, e.ManipulationOrigin.Y);
			touchTransformMatrix.Value.Translate(e.DeltaManipulation.Translation.X, e.DeltaManipulation.Translation.Y);

			UpdateContentTransform(touchTransformMatrix.Value, e.ManipulationOrigin);

			if (e.IsInertial)
				e.Complete();

			e.Handled = true;
		} // proc OnManipulationDelta

		/// <summary>Overwrite panning and zoom implementation fo the scroll viewer.</summary>
		/// <param name="e"></param>
		protected override void OnManipulationInertiaStarting(ManipulationInertiaStartingEventArgs e)
		{
			if (!touchTransformMatrix.HasValue)
				return;

			e.ExpansionBehavior.DesiredDeceleration = 100;
			e.TranslationBehavior.DesiredDeceleration = 100;
			e.Handled = true;
		} // proc OnManipulationInertiaStarting

		/// <summary>Overwrite panning and zoom implementation fo the scroll viewer.</summary>
		/// <param name="e"></param>
		protected override void OnManipulationCompleted(ManipulationCompletedEventArgs e)
		{
			if (!touchTransformMatrix.HasValue)
				return;

			touchTransformMatrix = Matrix.Identity;
			e.Handled = true;
		} // proc OnManipulationCompleted

		#endregion

		/// <summary>Current scale factor for the content.</summary>
		public double ScaleFactor { get => (double)GetValue(ScaleFactorProperty); set => SetValue(ScaleFactorProperty, value); }
		/// <summary>Minimal scale factor for the content.</summary>
		public double MinScaleFactor { get => (double)GetValue(MinScaleFactorProperty); set => SetValue(MinScaleFactorProperty, value); }
		/// <summary>Maximal scale factor for the content.</summary>
		public double MaxScaleFactor { get => (double)GetValue(MaxScaleFactorProperty); set => SetValue(MaxScaleFactorProperty, value); }
		/// <summary>Is zoom allowed.</summary>
		public bool IsZoomAllowed { get => BooleanBox.GetBool(GetValue(IsZoomAllowedProperty)); set => SetValue(IsZoomAllowedProperty, BooleanBox.GetObject(value)); }
		/// <summary>Is panning allowed.</summary>
		public bool IsPanningAllowed { get => BooleanBox.GetBool(GetValue(IsPanningAllowedProperty)); set => SetValue(IsPanningAllowedProperty, BooleanBox.GetObject(value)); }

		static PpsScrollViewer()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PpsScrollViewer), new FrameworkPropertyMetadata(typeof(PpsScrollViewer)));
		} // sctor

	} // class PpsScrollViewer

	#endregion
}
