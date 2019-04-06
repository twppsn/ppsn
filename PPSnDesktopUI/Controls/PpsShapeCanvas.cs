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
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TecWare.DE.Data;
using TecWare.PPSn.UI;

namespace TecWare.PPSn.Controls
{
	#region -- enum PpsShapeTransformMode ---------------------------------------------

	/// <summary>Intentify shape transform mode.</summary>
	[Flags]
	public enum PpsShapeTransformMode
	{
		/// <summary>No transformation.</summary>
		None = 0,
		/// <summary>Scale north and west.</summary>
		ScaleNW = ScaleN | ScaleW,
		/// <summary>Scale north.</summary>
		ScaleN = 1,
		/// <summary>Scale north and east.</summary>
		ScaleNE = ScaleN | ScaleE,
		/// <summary>Scale east.</summary>
		ScaleE = 2,
		/// <summary>Scale south and east.</summary>
		ScaleSE = ScaleS | ScaleE,
		/// <summary>Scale south.</summary>
		ScaleS = 4,
		/// <summary>Scale south and west.</summary>
		ScaleSW = ScaleS | ScaleW,
		/// <summary>Scale west.</summary>
		ScaleW = 8,

		/// <summary>Rotate.</summary>
		Rotate = 16,
		/// <summary>Translate.</summary>
		Translate = 32
	} // enum PpsShapeTransformMode

	#endregion

	#region -- interface IPpsShapeEditing ---------------------------------------------

	/// <summary>Shape editing parameter.</summary>
	public interface IPpsShapeEditing : INotifyPropertyChanged
	{
		/// <summary>Position in the canvas.</summary>
		Matrix Transform { get; set; }
	} // interface IPpsShapeEditing

	#endregion

	#region -- interface IPpsShapeColor -----------------------------------------------

	/// <summary>This shape supports colors.</summary>
	public interface IPpsShapeColor
	{
		/// <summary>Fill brush</summary>
		Brush Brush { get; set; }
		/// <summary>Pen color</summary>
		Pen Pen { get; set; }
	} // interface IPpsShapeColor

	#endregion

	#region -- interface IPpsShapeFactory ---------------------------------------------

	/// <summary>Shape factory.</summary>
	public interface IPpsShapeFactory
	{
		/// <summary>Create a new shape.</summary>
		/// <returns></returns>
		PpsShape CreateNew();

		/// <summary>Order of the shape in an list.</summary>
		PpsCommandOrder Order { get; }
		/// <summary>Display name for the shape.</summary>
		string Text { get; }
		/// <summary>Image of the shape</summary>
		object Image { get; }
		/// <summary>Create the shape with a specific aspect.</summary>
		double DefaultAspect { get; }
	} // interface IPpsShapeFactory

	#endregion

	#region -- class PpsShape ---------------------------------------------------------

	/// <summary>Base class for all shapes, in the shape canvas.</summary>
	public abstract class PpsShape : ObservableObject, IPpsShapeEditing
	{
		private Matrix transformMatrix;         // position on the canvas

		/// <summary>Render the shape in the context.</summary>
		/// <param name="dc"></param>
		public abstract void Render(DrawingContext dc);

		/// <summary>Test if the point is within the shape.</summary>
		/// <param name="pt"></param>
		/// <returns></returns>
		public virtual bool HitTest(Point pt)
		{
			var m = transformMatrix;
			m.Invert();
			pt = m.Transform(pt);
			return new Rect(0.0, 0.0, 1.0, 1.0).Contains(pt);
		} // proc HitTest

		/// <summary>Shape transform matrix.</summary>
		public Matrix Transform
		{
			get => transformMatrix;
			set => Set(ref transformMatrix, value, nameof(Transform));
		} // prop Transform
	} // class PpsShape

	#endregion

	#region -- class PpsGeometryShapeFactory ------------------------------------------

	/// <summary>Shape that is based on a geometry.</summary>
	public sealed class PpsGeometryShapeFactory : DependencyObject, IPpsShapeFactory
	{
		#region -- class PpsGeometryShape ---------------------------------------------

		private class PpsGeometryShape : PpsShape, IPpsShapeColor
		{
			private readonly Geometry geometry;     // base geometry
			private readonly Matrix geometryScale;  // scale of the geometry, to normal

			private Brush brush = null;
			private Pen pen = null;

			public PpsGeometryShape(Geometry geometry, Matrix geometryTransform)
			{
				this.geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
				geometryScale = geometryTransform;
				if (!geometryScale.IsIdentity)
					geometryScale.Invert();
			} // ctor

			public override void Render(DrawingContext dc)
			{
				if (pen != null || brush != null)
				{
					var drawingGroup = new GeometryGroup { Transform = new MatrixTransform(geometryScale * Transform) };
					drawingGroup.Children.Add(geometry);
					dc.DrawGeometry(brush, pen, drawingGroup);
				}
			} // proc Render

			public Brush Brush
			{
				get => brush;
				set => Set(ref brush, value, nameof(Brush));
			} // prop Brush

			public Pen Pen
			{
				get => pen;
				set => Set(ref pen, value, nameof(Pen));
			} // prop Pen
		} // class PpsGeometryShape

		#endregion

		#region -- Text - property ----------------------------------------------------

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public static readonly DependencyProperty OrderProperty = DependencyProperty.Register(nameof(Order), typeof(PpsCommandOrder), typeof(PpsGeometryShapeFactory), new PropertyMetadata(PpsCommandOrder.Empty));
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		/// <summary>Display name of the shape</summary>
		public PpsCommandOrder Order { get => (PpsCommandOrder)GetValue(OrderProperty); set => SetValue(OrderProperty, value); }

		#endregion

		#region -- Text - property ----------------------------------------------------

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(PpsGeometryShapeFactory), new PropertyMetadata(null));
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		/// <summary>Display name of the shape</summary>
		public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }

		#endregion

		#region -- Geometry - property ------------------------------------------------

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public static readonly DependencyProperty GeometryProperty = DependencyProperty.Register(nameof(Geometry), typeof(Geometry), typeof(PpsGeometryShapeFactory), new PropertyMetadata(null, new PropertyChangedCallback(OnGeometryChanged)));
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		private static void OnGeometryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> UpdateImage(d, (Geometry)e.NewValue, (Matrix)d.GetValue(GeometryScaleProperty));

		/// <summary>Display name of the shape</summary>
		public Geometry Geometry { get => (Geometry)GetValue(GeometryProperty); set => SetValue(GeometryProperty, value); }

		#endregion

		#region -- GeometryScale - property -------------------------------------------

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public static readonly DependencyProperty GeometryScaleProperty = DependencyProperty.Register(nameof(GeometryScale), typeof(Matrix), typeof(PpsGeometryShapeFactory), new PropertyMetadata(Matrix.Identity, new PropertyChangedCallback(OnGeometryScaleChanged)));
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		private static void OnGeometryScaleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> UpdateImage(d, (Geometry)d.GetValue(GeometryProperty), (Matrix)e.NewValue);

		/// <summary>Display name of the shape</summary>
		public Matrix GeometryScale { get => (Matrix)GetValue(GeometryScaleProperty); set => SetValue(GeometryScaleProperty, value); }

		#endregion

		#region -- Image - property ---------------------------------------------------

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		private static readonly DependencyPropertyKey imagePropertyKey = DependencyProperty.RegisterReadOnly(nameof(Image), typeof(object), typeof(PpsGeometryShapeFactory), new PropertyMetadata(null));
		public static readonly DependencyProperty ImageProperty = imagePropertyKey.DependencyProperty;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		private static void UpdateImage(DependencyObject d, Geometry g, Matrix gm)
		{
			if (g == null)
				d.SetValue(imagePropertyKey, null);
			else
			{
				var geometry = new GeometryGroup();
				geometry.Children.Add(g);

				// get geometry scale
				if (!gm.IsIdentity)
					gm.Invert();

				// transform to 24.0, 24.0
				geometry.Transform = new MatrixTransform(gm * new Matrix(24.0, 0.0, 0.0, 24.0, 0.0, 0.0));

				// set drawing
				var drawing = new DrawingImage(new GeometryDrawing(Brushes.White, new Pen(Brushes.Black, 1), geometry));
				drawing.Freeze();
				d.SetValue(imagePropertyKey, drawing);
			}
		} // proc UpdateImage

		/// <summary>Display name of the shape</summary>
		public object Image => (Geometry)GetValue(ImageProperty);

		#endregion

		#region -- DefaultAspect - property -------------------------------------------

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		private static readonly DependencyPropertyKey defaultAspectPropertyKey = DependencyProperty.RegisterReadOnly(nameof(DefaultAspect), typeof(double), typeof(PpsGeometryShapeFactory), new PropertyMetadata(Double.NaN));
		public static readonly DependencyProperty DefaultAspectProperty = defaultAspectPropertyKey.DependencyProperty;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		/// <summary>Default aspect</summary>
		public double DefaultAspect => (double)GetValue(DefaultAspectProperty);

		#endregion

		PpsShape IPpsShapeFactory.CreateNew()
			=> new PpsGeometryShape(Geometry, GeometryScale);
	} // class PpsGeometryShapeFactory

	#endregion

	#region -- class PpsShapeTransformBox ---------------------------------------------

	/// <summary>Shape transform box layout element.</summary>
	public class PpsShapeTransformBox : Control
	{
		#region -- GetCursorFromTransform - helper ------------------------------------

		private static Cursor GetCursorFromAngle(double angle)
		{
			// normalize 
			angle %= 360;
			if (angle < 0.0)
				angle += 360;

			var idx = (int)Math.Round(angle / 45);
			switch (idx)
			{
				case 1:
				case 5:
					return Cursors.SizeNWSE;
				case 2:
				case 6:
					return Cursors.SizeNS;
				case 3:
				case 7:
					return Cursors.SizeNESW;
				case 4:
				default:
					return Cursors.SizeWE;
			}
		} // func GetCursorFromAngle

		private static Cursor GetCursorFromTransform(PpsShapeTransformMode mode, double angle)
		{
			switch (mode)
			{
				case PpsShapeTransformMode.ScaleNW:
					return GetCursorFromAngle(225.0 + angle);
				case PpsShapeTransformMode.ScaleN:
					return GetCursorFromAngle(270.0 + angle);
				case PpsShapeTransformMode.ScaleNE:
					return GetCursorFromAngle(315.0 + angle);
				case PpsShapeTransformMode.ScaleE:
					return GetCursorFromAngle(0.0 + angle);
				case PpsShapeTransformMode.ScaleSE:
					return GetCursorFromAngle(45.0 + angle);
				case PpsShapeTransformMode.ScaleS:
					return GetCursorFromAngle(90.0 + angle);
				case PpsShapeTransformMode.ScaleSW:
					return GetCursorFromAngle(135.0 + angle);
				case PpsShapeTransformMode.ScaleW:
					return GetCursorFromAngle(180.0 + angle);
				case PpsShapeTransformMode.Rotate:
					return Cursors.Cross;
				case PpsShapeTransformMode.Translate:
					return Cursors.SizeAll;
				default:
					return Cursors.Arrow;
			}
		} // func GetCursorFromTransform

		#endregion

		#region -- Mode - property ----------------------------------------------------

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public static readonly DependencyProperty ModeProperty = DependencyProperty.Register(nameof(Mode), typeof(PpsShapeTransformMode), typeof(PpsShapeTransformBox), new FrameworkPropertyMetadata(PpsShapeTransformMode.None, new PropertyChangedCallback(OnModeChanged)));
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		private static void OnModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> UpdateCursor(d, (PpsShapeTransformMode)e.NewValue, 0.0);

		private static void UpdateCursor(DependencyObject d, PpsShapeTransformMode mode, double angle)
		{
			// update cursor
			d.SetValue(CursorProperty, GetCursorFromTransform(mode, angle));
		} // proc UpdateCursor

		/// <summary>Transform mode, that this box will initiate.</summary>
		public PpsShapeTransformMode Mode { get => (PpsShapeTransformMode)GetValue(ModeProperty); set => SetValue(ModeProperty, value); }

		#endregion

		internal static void InvalidateCursor(DependencyObject d, double angle)
		{
			for (var i = 0; i < VisualTreeHelper.GetChildrenCount(d); i++)
			{
				var c = VisualTreeHelper.GetChild(d, i);
				if (c is PpsShapeTransformBox b)
					UpdateCursor(b, b.Mode, angle);
				else
					InvalidateCursor(c, angle);
			}
		} // proc InvalidateCursor

		static PpsShapeTransformBox()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PpsShapeTransformBox), new FrameworkPropertyMetadata(typeof(PpsShapeTransformBox)));
		}
	} // class PpsShapeTransformBox

	#endregion

	#region -- class PpsShapeEditor ---------------------------------------------------

	/// <summary>Editor to manipulte a <see cref="IPpsShapeEditing"/>.</summary>
	public class PpsShapeEditor : Control
	{
		#region -- AttachedShape - property -------------------------------------------

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public static readonly DependencyProperty AttachedShapeProperty = DependencyProperty.Register(nameof(AttachedShape), typeof(IPpsShapeEditing), typeof(PpsShapeEditor), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnAttachedShapeChanged)));
		private static readonly DependencyPropertyKey isShapeAttachedPropetyKey = DependencyProperty.RegisterReadOnly(nameof(IsShapeAttached), typeof(bool), typeof(PpsShapeEditor), new FrameworkPropertyMetadata(BooleanBox.False));
		public static readonly DependencyProperty IsShapeAttachedProperty = isShapeAttachedPropetyKey.DependencyProperty;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		private static void OnAttachedShapeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			d.SetValue(isShapeAttachedPropetyKey, e.NewValue != null);
			((PpsShapeEditor)d).OnAttachedShapeChanged((IPpsShapeEditing)e.NewValue, (IPpsShapeEditing)e.OldValue);
		} // proc OnAttachedShapeChanged

		/// <summary>Attached shape is changed</summary>
		/// <param name="newValue"></param>
		/// <param name="oldValue"></param>
		protected virtual void OnAttachedShapeChanged(IPpsShapeEditing newValue, IPpsShapeEditing oldValue)
		{
			if (oldValue != null)
				oldValue.PropertyChanged -= AttachedShape_PropertyChanged;
			if (newValue != null)
				newValue.PropertyChanged += AttachedShape_PropertyChanged;

			UpdateTransformParameter(this, newValue);
		} // proc OnAttachedShapeChanged

		private void AttachedShape_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(IPpsShapeEditing.Transform))
				UpdateTransformParameter(this, AttachedShape);
		} // event AttachedShape_PropertyChanged

		/// <summary>Currently attached shape.</summary>
		public IPpsShapeEditing AttachedShape { get => (IPpsShapeEditing)GetValue(AttachedShapeProperty); set => SetValue(AttachedShapeProperty, value); }
		/// <summary>Is a shape attached.</summary>
		public bool IsShapeAttached => BooleanBox.GetBool(GetValue(IsShapeAttachedProperty));

		#endregion

		#region -- IsScaling, IsTranslating, IsRotating - property --------------------

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		private static readonly DependencyPropertyKey isScalingPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsScaling), typeof(bool), typeof(PpsShapeEditor), new FrameworkPropertyMetadata(BooleanBox.False));
		private static readonly DependencyPropertyKey scaleSizePropertyKey = DependencyProperty.RegisterReadOnly(nameof(ScaleSize), typeof(Size), typeof(PpsShapeEditor), new FrameworkPropertyMetadata(Size.Empty));
		private static readonly DependencyPropertyKey isTranslatingPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsTranslating), typeof(bool), typeof(PpsShapeEditor), new FrameworkPropertyMetadata(BooleanBox.False));
		private static readonly DependencyPropertyKey translateOffsetPropertyKey = DependencyProperty.RegisterReadOnly(nameof(TranslateOffset), typeof(Point), typeof(PpsShapeEditor), new FrameworkPropertyMetadata(new Point(0.0, 0.0)));
		private static readonly DependencyPropertyKey isRotatingPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsRotating), typeof(bool), typeof(PpsShapeEditor), new FrameworkPropertyMetadata(BooleanBox.False));
		private static readonly DependencyPropertyKey rotateAnglePropertyKey = DependencyProperty.RegisterReadOnly(nameof(RotateAngle), typeof(double), typeof(PpsShapeEditor), new FrameworkPropertyMetadata(0.0));
		private static readonly DependencyPropertyKey isDiscretPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsDiscrete), typeof(bool), typeof(PpsShapeEditor), new FrameworkPropertyMetadata(BooleanBox.False));
		public static readonly DependencyProperty IsScalingProperty = isScalingPropertyKey.DependencyProperty;
		public static readonly DependencyProperty ScaleSizeProperty = scaleSizePropertyKey.DependencyProperty;
		public static readonly DependencyProperty IsTranslatingProperty = isTranslatingPropertyKey.DependencyProperty;
		public static readonly DependencyProperty TranslateOffsetProperty = translateOffsetPropertyKey.DependencyProperty;
		public static readonly DependencyProperty IsRotatingProperty = isRotatingPropertyKey.DependencyProperty;
		public static readonly DependencyProperty RotateAngleProperty = rotateAnglePropertyKey.DependencyProperty;
		public static readonly DependencyProperty IsDiscreteProperty = isDiscretPropertyKey.DependencyProperty;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		private static void UpdateTransformParameter(DependencyObject d, IPpsShapeEditing shape)
		{
			if (shape == null)
			{
				d.ClearValue(rotateAnglePropertyKey);
				d.ClearValue(scaleSizePropertyKey);
				d.ClearValue(translateOffsetPropertyKey);
			}
			else
			{
				var matrix = shape.Transform;

				// get angle
				var angle = PpsShapeCanvas.GetAngleFromMatrix(ref matrix);
				d.SetValue(rotateAnglePropertyKey, angle);

				// get scale
				var lengths = new Vector[]
				{
					new Vector(1.0, 0.0),
					new Vector(0.0, 1.0)
				};
				matrix.Transform(lengths);
				d.SetValue(scaleSizePropertyKey, new Size(lengths[0].Length, lengths[1].Length));

				// offset
				d.SetValue(translateOffsetPropertyKey, matrix.Transform(new Point(0.0, 0.0)));
			}
		} // proc UpdateTransformParameter 

		/// <summary>Is the editor in scaling mode.</summary>
		public bool IsScaling => BooleanBox.GetBool(GetValue(IsScalingProperty));
		/// <summary>Current size of the shape.</summary>
		public Size ScaleSize => (Size)GetValue(ScaleSizeProperty);
		/// <summary>Is the editor in translating mode.</summary>
		public bool IsTranslating => BooleanBox.GetBool(GetValue(IsTranslatingProperty));
		/// <summary>Current offset of the shape.</summary>
		public Point TranslateOffset => (Point)GetValue(TranslateOffsetProperty);
		/// <summary>Is the editor in rotating mode.</summary>
		public bool IsRotating => BooleanBox.GetBool(GetValue(IsRotatingProperty));
		/// <summary>Current angle of the shape.</summary>
		public double RotateAngle => (double)GetValue(RotateAngleProperty);
		/// <summary>Does the editor uses discrete steps.</summary>
		public bool IsDiscrete => BooleanBox.GetBool(GetValue(IsDiscreteProperty));

		#endregion

		#region -- Scale, Translate, Rotate -------------------------------------------

		private Point? mouseDownPoint = null;   // button pressed position
		private Matrix transformationRule;      // marks the operation
		private Matrix mouseDownTransformation; // start transformation on mouse down, all is relative this capture moment

		private static Matrix GetTransformationRuleFromMode(PpsShapeTransformMode mode)
		{
			switch (mode)
			{
				case PpsShapeTransformMode.ScaleNW:
					return new Matrix(-1.0, 0.0, 0.0, -1.0, 1.0, 1.0);
				case PpsShapeTransformMode.ScaleN:
					return new Matrix(0.0, 0.0, 0.0, -1.0, 0.0, 1.0);
				case PpsShapeTransformMode.ScaleNE:
					return new Matrix(1.0, 0.0, 0.0, -1.0, 0.0, 1.0);
				case PpsShapeTransformMode.ScaleE:
					return new Matrix(1.0, 0.0, 0.0, 0.0, 0.0, 0.0);
				case PpsShapeTransformMode.ScaleSE:
					return new Matrix(1.0, 0.0, 0.0, 1.0, 0.0, 0.0);
				case PpsShapeTransformMode.ScaleS:
					return new Matrix(0.0, 0.0, 0.0, 1.0, 0.0, 0.0);
				case PpsShapeTransformMode.ScaleSW:
					return new Matrix(-1.0, 0.0, 0.0, 1.0, 1.0, 0.0);
				case PpsShapeTransformMode.ScaleW:
					return new Matrix(-1.0, 0.0, 0.0, 0.0, 1.0, 0.0);

				case PpsShapeTransformMode.Rotate:
					return new Matrix(0.0, 1.0, 1.0, 0.0, 0.0, 0.0);
				case PpsShapeTransformMode.Translate:
					return new Matrix(0.0, 0.0, 0.0, 0.0, 1.0, 1.0);

				default:
					return new Matrix(0.0, 0.0, 0.0, 0.0, 0.0, 0.0);
			}
		} // func GetTransformationRuleFromMode

		/// <summary></summary>
		/// <param name="e"></param>
		protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
		{
			var attachedShape = AttachedShape;
			if (attachedShape != null && e.Source is PpsShapeTransformBox box)
			{
				if (Mouse.Capture(this, CaptureMode.Element))
				{
					Mouse.OverrideCursor = box.Cursor;
					mouseDownPoint = e.GetPosition(ShapeCanvas);
					mouseDownTransformation = attachedShape.Transform;
					transformationRule = GetTransformationRuleFromMode(box.Mode);

					SetValue(isScalingPropertyKey, BooleanBox.GetObject(IsScalingCore));
					SetValue(isTranslatingPropertyKey, BooleanBox.GetObject(IsTranslatingCore));
					SetValue(isRotatingPropertyKey, BooleanBox.GetObject(IsRotatingCore));
				}
				e.Handled = true;
			}
			base.OnMouseLeftButtonDown(e);
		} // proc OnLeftButtonMouseDown

		/// <summary></summary>
		/// <param name="e"></param>
		protected override void OnMouseMove(MouseEventArgs e)
		{
			if (mouseDownPoint.HasValue && IsCaptured && AttachedShape != null) // process transformation
			{
				SetValue(isDiscretPropertyKey, BooleanBox.GetObject((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control));

				var matrix = mouseDownTransformation;
				var currentPoint = e.GetPosition(ShapeCanvas);
				if (IsRotatingCore) // process rotation
				{
					var centerPoint = mouseDownTransformation.Transform(new Point(0.5, 0.5));
					var angle = Vector.AngleBetween(
						centerPoint - mouseDownPoint.Value,
						centerPoint - currentPoint
					);
					var currentAngle = PpsShapeCanvas.GetAngleFromMatrix(ref matrix);

					if (IsDiscrete) // 15° moves
						angle = Math.Round(angle / 15) * 15 - currentAngle % 15;

					// update transformation angle
					matrix.RotateAt(angle, centerPoint.X, centerPoint.Y);
				}
				else if (IsScalingCore) // process scaling
				{
					// normalize distance vector
					var matrixInverse = matrix;
					matrixInverse.Invert();
					var scaleFactor = matrixInverse.Transform(currentPoint - mouseDownPoint.Value);

					// get basic parameter
					var centerPoint = matrix.Transform(new Point(0.5, 0.5));
					var currentAngle = PpsShapeCanvas.GetAngleFromMatrix(ref matrix);

					// scaling is not compatible with rotating
					if (currentAngle != 0.0)
						matrix.RotateAt(-currentAngle, centerPoint.X, centerPoint.Y);

					if (IsDiscrete)
					{
						//if (transformationRule.M11 != 0.0 && transformationRule.M22 != 0.0) // hold aspect
						//{
						//}
						//else // scale in raster
						//{
						//}
					}

					// todo: lock Min,Max Size?

					// scale
					var scaleCenter = matrix.Transform(new Point(transformationRule.OffsetX, transformationRule.OffsetY));
					matrix.ScaleAt(1.0 + transformationRule.M11 * scaleFactor.X, 1.0 + transformationRule.M22 * scaleFactor.Y, scaleCenter.X, scaleCenter.Y);

					// rotate back
					if (currentAngle != 0.0)
						matrix.RotateAt(currentAngle, centerPoint.X, centerPoint.Y);
				}
				else if (IsTranslating) // process move
				{
					var distanceVector = currentPoint - mouseDownPoint.Value;

					if (IsDiscrete)
					{
						var currentOffset = matrix.Transform(new Point(0.0, 0.0));
						distanceVector.X = Math.Round(distanceVector.X / 10) * 10 - currentOffset.X % 10;
						distanceVector.Y = Math.Round(distanceVector.Y / 10) * 10 - currentOffset.Y % 10;
					}
					matrix.Translate(distanceVector.X, distanceVector.Y);
				}
				AttachedShape.Transform = matrix;

				e.Handled = true;
			}

			base.OnMouseMove(e);
		} // proc OnMouseMove

		/// <summary></summary>
		/// <param name="e"></param>
		protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
		{
			if (mouseDownPoint.HasValue)
			{
				Mouse.OverrideCursor = null;

				SetValue(isScalingPropertyKey, BooleanBox.False);
				SetValue(isTranslatingPropertyKey, BooleanBox.False);
				SetValue(isRotatingPropertyKey, BooleanBox.False);

				mouseDownPoint = null;
				mouseDownTransformation = Matrix.Identity;
				transformationRule = GetTransformationRuleFromMode(PpsShapeTransformMode.None);

				e.Handled = true;
			}
			if (IsCaptured)
				Mouse.Capture(null);

			base.OnMouseLeftButtonUp(e);
		} // proc OnMouseLeftButtonUp

		private bool IsScalingCore => transformationRule.M11 != 0.0 || transformationRule.M22 != 0.0;
		private bool IsTranslatingCore => transformationRule.OffsetX != 0.0 || transformationRule.OffsetY != 0.0;
		private bool IsRotatingCore => transformationRule.M12 != 0.0 || transformationRule.M21 != 0.0;

		private bool IsCaptured => Mouse.Captured == this;

		#endregion

		private PpsShapeCanvas ShapeCanvas => (PpsShapeCanvas)VisualParent;

		static PpsShapeEditor()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PpsShapeEditor), new FrameworkPropertyMetadata(typeof(PpsShapeEditor)));
		}
	} // class PpsShapeEditor

	#endregion

	#region -- class PpsShapeCanvas ---------------------------------------------------

	/// <summary>Canvas for shape painting.</summary>
	public class PpsShapeCanvas : FrameworkElement, IPpsReadOnlyControl
	{
		#region -- CurrentShape - property --------------------------------------------

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public static readonly DependencyProperty CurrentShapeProperty = DependencyProperty.Register(nameof(CurrentShape), typeof(PpsShape), typeof(PpsShapeCanvas), new FrameworkPropertyMetadata(null,
			FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender,
			new PropertyChangedCallback(OnCurrentShapeChanged)
		));
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		private static void OnCurrentShapeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsShapeCanvas)d).OnCurrentShapeChanged((PpsShape)e.NewValue, (PpsShape)e.OldValue);

		private void OnCurrentShapeChanged(PpsShape newValue, PpsShape oldValue)
		{
			if (oldValue == null)
				AddVisualChild(shapeEditor);
			if (newValue == null)
				RemoveVisualChild(shapeEditor);

			shapeEditor.AttachedShape = newValue;
		} // proc OnCurrentShapeChanged

		/// <summary>Currently, active shape.</summary>
		public PpsShape CurrentShape { get => (PpsShape)GetValue(CurrentShapeProperty); set => SetValue(CurrentShapeProperty, value); }

		#endregion

		#region -- NewShapeType - property --------------------------------------------

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public static readonly DependencyProperty NewShapeTypeProperty = DependencyProperty.Register(nameof(NewShapeType), typeof(IPpsShapeFactory), typeof(PpsShapeCanvas), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnNewShapeTypeChanged)));
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		private static void OnNewShapeTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> UpdateCursor(d);

		private static void UpdateCursor(DependencyObject d)
		{
			d.SetValue(CursorProperty,
				d.GetValue(NewShapeTypeProperty) != null && d.GetValue(IsReadOnlyProperty) == BooleanBox.False
					? Cursors.Pen
					: Cursors.Arrow
			);
		} // proc UpdateCursor

		/// <summary>Add new shapes to the canvas.</summary>
		public IPpsShapeFactory NewShapeType { get => (IPpsShapeFactory)GetValue(NewShapeTypeProperty); set => SetValue(NewShapeTypeProperty, value); }

		#endregion

		#region -- IsReadOnly - property ----------------------------------------------

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.Register(nameof(IsReadOnly), typeof(bool), typeof(PpsShapeCanvas), new FrameworkPropertyMetadata(BooleanBox.False, new PropertyChangedCallback(OnIsReadOnlyChanged)));
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		private static void OnIsReadOnlyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> UpdateCursor(d);

		/// <summary>Is edit mode active.</summary>
		public bool IsReadOnly { get => BooleanBox.GetBool(GetValue(IsReadOnlyProperty)); set => SetValue(IsReadOnlyProperty, BooleanBox.GetObject(value)); }

		#endregion

		private readonly PpsShapeEditor shapeEditor;    // editor visual
		private readonly List<PpsShape> shapes = new List<PpsShape>(); // current geometries

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary>Canvas for shape painting.</summary>
		public PpsShapeCanvas()
		{
			shapeEditor = new PpsShapeEditor();
		} // ctor

		#endregion

		#region -- Visual Childs ------------------------------------------------------

		/// <summary>Add shape editor as visual.</summary>
		/// <param name="index"></param>
		/// <returns></returns>
		protected override Visual GetVisualChild(int index)
			=> CurrentShape != null && index == 0 ? shapeEditor : null;

		/// <summary>Number of visuals</summary>
		protected override int VisualChildrenCount
			=> CurrentShape != null ? 1 : 0;

		#endregion

		#region -- Arrange, Measure ---------------------------------------------------

		internal static double GetAngleFromMatrix(ref Matrix matrix)
		{
			var n = new Vector(1, 0);
			var r = Vector.Multiply(n, matrix);
			return Vector.AngleBetween(n, r);
		} // func GetAngleFromMatrix

		internal static double GetAngleFromTransform(Transform transform)
		{
			switch (transform)
			{
				case MatrixTransform matrixTransform:
					var matrix = matrixTransform.Matrix;
					return GetAngleFromMatrix(ref matrix);
				case RotateTransform rotateTransform:
					return rotateTransform.Angle;
				default:
					return 0.0;
			}
		} // func GetAngleFromTransform

		private static void GetShapeLayout(PpsShape shape, Thickness padding, out Rect bounds, out double rotateAngle)
		{
			var matrix = shape.Transform;

			var ptCenter = matrix.Transform(new Point(0.5, 0.5)); // get center for rotation
			rotateAngle = GetAngleFromMatrix(ref matrix); // get the angle

			// rotate matrix to normal
			matrix.RotateAt(-rotateAngle, ptCenter.X, ptCenter.Y);

			// get TopLeft and BottomRight
			var rectanglePoints = new Point[] { new Point(0, 0), new Point(1, 1) };
			matrix.Transform(rectanglePoints);

			// add padding
			rectanglePoints[0].Offset(-padding.Left, -padding.Top);
			rectanglePoints[1].Offset(padding.Right, padding.Bottom);

			// create bounds with padding
			bounds = new Rect(rectanglePoints[0], rectanglePoints[1]);
		} // func GetShapeLayout

		/// <summary>Arrange shape editor.</summary>
		/// <param name="finalSize"></param>
		/// <returns></returns>
		protected override Size ArrangeOverride(Size finalSize)
		{
			var currentShape = CurrentShape;
			if (currentShape != null)
			{
				GetShapeLayout(currentShape, shapeEditor.Padding, out var bounds, out var angle);
				shapeEditor.Arrange(bounds);
				PpsShapeTransformBox.InvalidateCursor(shapeEditor, angle);
				shapeEditor.RenderTransform = new RotateTransform(angle, bounds.Width / 2, bounds.Height / 2);
			}
			return finalSize;
		} // func ArrangeOverride

		/// <summary>Measure shape control</summary>
		/// <param name="availableSize"></param>
		/// <returns></returns>
		protected override Size MeasureOverride(Size availableSize)
		{
			var width = availableSize.Width;
			var height = availableSize.Height;

			var currentShape = CurrentShape;
			if (currentShape != null)
			{
				GetShapeLayout(currentShape, shapeEditor.Padding, out var bounds, out var angle);
				shapeEditor.Measure(bounds.Size);

				width = Math.Max(width, bounds.Right);
				height = Math.Max(height, bounds.Bottom);
			}

			foreach (var shape in shapes)
			{
				var bottomRight = shape.Transform.Transform(new Point(1.0, 1.0));
				width = Math.Max(width, bottomRight.X);
				height = Math.Max(height, bottomRight.Y);
			}

			if (Double.IsInfinity(width))
				width = 0.0;
			if (Double.IsInfinity(height))
				height = 0.0;

			return new Size(width, height);
		} // func MeasureOverride 

		#endregion

		#region -- Shape - Management -------------------------------------------------

		private static Matrix CreateMatrixFromRect(Rect initialArea)
			=> new Matrix(initialArea.Width, 0.0, 0.0, initialArea.Height, initialArea.X, initialArea.Y);

		private PpsShape CreateShapeCore(IPpsShapeFactory shapeFactory, Rect initialArea)
		{
			var shape = shapeFactory.CreateNew();
			if (shape is IPpsShapeColor colorable) // todo: apply last color selection
			{
				colorable.Brush = Brushes.Red; // Brushes.Transparent;
				colorable.Pen = new Pen(Brushes.Red, 3.0);
			}
			shape.Transform = CreateMatrixFromRect(initialArea);
			return shape;
		} // func CreateShapeCore

		private PpsShape AddShapeCore(IPpsShapeFactory shapeFactory, Rect initialArea)
			=> AddShapeCore(CreateShapeCore(shapeFactory, initialArea), true);

		private PpsShape AddShapeCore(PpsShape shape, bool attach)
		{
			if (attach)
				AttachShapeCore(shape);
			shapes.Add(shape);
			InvalidateVisual();

			return shape;
		} // func AddShapeCore

		private void AttachShapeCore(PpsShape shape)
			=> shape.PropertyChanged += Shape_PropertyChanged;

		private void DetachShapeCore(PpsShape shape)
			=> shape.PropertyChanged -= Shape_PropertyChanged;

		private bool MoveShapeCore(PpsShape shape, int moveTo)
		{
			var idx = FindShapeVisualIndex(shape);
			if (idx == -1)
				return false;

			// move shape
			var shapeVisual = shapes[idx];
			shapes.RemoveAt(idx);
			if (idx < moveTo)
				moveTo--;
			shapes.Insert(idx, shapeVisual);

			InvalidateVisual();
			return true;
		} // func MoveShapeCore

		private bool RemoveShapeCore(PpsShape shape)
		{
			var idx = FindShapeVisualIndex(shape);
			if (idx == -1)
				return false;

			// remove shape
			DetachShapeCore(shape);
			shapes.RemoveAt(idx);

			InvalidateMeasure();

			return true;
		} // proc RemoveShapeCore

		private void Shape_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (sender == CurrentShape)
				InvalidateMeasure();
			InvalidateVisual();
		} // event Shape_PropertyChanged

		private PpsShape ShapeFromPoint(Point pt)
		{
			for (var i = shapes.Count - 1; i >= 0; i--)
			{
				var shape = shapes[i];
				if (shape.HitTest(pt))
					return shape;
			}
			return null;
		} // func ShapeFromPoint

		private PpsShape FindShapeVisual(PpsShape shape)
			=> shapes.FirstOrDefault(c => c == shape);

		private int FindShapeVisualIndex(PpsShape shape)
			=> shapes.FindIndex(c => c == shape);

		#endregion

		#region -- NewShape - Management ----------------------------------------------

		private PpsShape newShape = null; // selection-box content
		private Point? startPoint = null;
		private double defaultAspect = Double.NaN;

		private void StartNewShape(Point pt, Vector vector)
		{
			startPoint = pt;
			defaultAspect = NewShapeType.DefaultAspect;
			newShape = CreateShapeCore(NewShapeType, new Rect(pt, vector));
			AttachShapeCore(newShape);

			InvalidateMeasure();
		} // proc StartSelectionBox

		private void MoveNewShape(Vector vector)
		{
			if (newShape == null || !startPoint.HasValue)
				return;

			if (!Double.IsNaN(defaultAspect) && !Double.IsInfinity(defaultAspect))
			{
				// todo: lock aspect
			}

			newShape.Transform = CreateMatrixFromRect(new Rect(startPoint.Value, vector));

			InvalidateMeasure();
		} // proc MoveSelectionBox

		private void FinishNewShape(bool commit)
		{
			if (newShape == null)
				return;

			// update visual tree
			if (commit)
				AddShapeCore(newShape, false);
			else
				DetachShapeCore(newShape);

			// clear shape
			newShape = null;
			startPoint = null;
			defaultAspect = Double.NaN;

			InvalidateMeasure();
		} // proc FinishNewShape

		#endregion

		#region -- Mouse - handling ---------------------------------------------------

		private Point? captureStartPosition = null;

		/// <summary></summary>
		/// <param name="e"></param>
		protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
		{
			var pt = e.GetPosition(this);

			// capture mouse to detect drag operations
			if (NewShapeType != null)
			{
				if (Mouse.Capture(this))
					captureStartPosition = pt;
			}

			// select for editor
			if (!IsReadOnly)
			{
				var shape = ShapeFromPoint(pt);
				if (shape != null)
				{
					CurrentShape = shape;
					e.Handled = true;
				}
			}

			base.OnMouseLeftButtonDown(e);
		} // proc OnMouseLeftButtonDown

		/// <summary></summary>
		/// <param name="e"></param>
		protected override void OnMouseMove(MouseEventArgs e)
		{
			if (captureStartPosition.HasValue)
			{
				var pt = e.GetPosition(this);
				var distance = pt - captureStartPosition.Value;
				if (newShape != null)// new shape mode
				{
					MoveNewShape(distance);
					e.Handled = true;
				}
				else if (distance.Length > 10)
				{
					StartNewShape(captureStartPosition.Value, distance);
					e.Handled = true;
				}
			}

			base.OnMouseMove(e);
		} // proc OnMouseMove

		/// <summary></summary>
		/// <param name="e"></param>
		protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
		{
			if (Mouse.Captured == this)
				Mouse.Capture(null);

			if (newShape != null)
			{
				var distance = e.GetPosition(this) - captureStartPosition.Value;
				MoveNewShape(distance);
				FinishNewShape(true);
				e.Handled = true;
			}
			if (captureStartPosition.HasValue)
			{
				captureStartPosition = null;
				e.Handled = true;
			}

			base.OnMouseRightButtonUp(e);
		} // proc OnMouseLeftButtonUp

		#endregion

		#region -- OnRender -----------------------------------------------------------

		/// <summary>Render control</summary>
		/// <param name="dc"></param>
		protected override void OnRender(DrawingContext dc)
		{
			//dc.PushClip(new RectangleGeometry(new Rect(new Point(0.0, 0.0), RenderSize)));

			dc.DrawRectangle(Brushes.Transparent, null, new Rect(new Point(0.0, 0.0), RenderSize));

			foreach (var shape in shapes)
				shape.Render(dc);

			newShape?.Render(dc);

			//dc.Pop();
		} // proc OnRender

		#endregion
	} // class PpsShapeCanvas

	#endregion
}
