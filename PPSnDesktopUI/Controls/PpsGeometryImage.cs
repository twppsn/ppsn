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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace TecWare.PPSn.Controls
{
	/// <summary></summary>
	public class PpsGeometryImage : Control
	{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public static readonly DependencyProperty GeometryProperty = DependencyProperty.Register(nameof(PpsGeometryImage.Geometry), typeof(Geometry), typeof(PpsGeometryImage), new FrameworkPropertyMetadata(null));
		private static readonly DependencyPropertyKey hasGeometryPropertyKey = DependencyProperty.RegisterReadOnly(nameof(HasGeometry), typeof(bool), typeof(PpsGeometryImage), new FrameworkPropertyMetadata(BooleanBox.False));
		public static readonly DependencyProperty HasGeometryProperty = hasGeometryPropertyKey.DependencyProperty;

		public static readonly DependencyProperty GeometryNameProperty = DependencyProperty.Register(nameof(GeometryName), typeof(string), typeof(PpsGeometryImage), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnGeometryNameChanged)));
		public static readonly DependencyProperty GeometryCircledProperty = DependencyProperty.Register(nameof(GeometryCircled), typeof(bool), typeof(PpsGeometryImage), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));

		public static readonly DependencyProperty FillProperty = Shape.FillProperty.AddOwner(typeof(PpsGeometryImage), new FrameworkPropertyMetadata(Brushes.Transparent));
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		private static readonly DependencyPropertyKey geometrySpacingPropertyKey = DependencyProperty.RegisterReadOnly(nameof(GeometrySpacing), typeof(Thickness), typeof(PpsGeometryImage), new FrameworkPropertyMetadata(new Thickness(8.0)));
		/// <summary>The inner distance between circle and image</summary>
		public static readonly DependencyProperty GeometrySpacingProperty = geometrySpacingPropertyKey.DependencyProperty;

		private static void OnGeometryNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var image = (PpsGeometryImage)d;
			object resource = null;
			if (e.NewValue is string resName)
				resource = Application.Current.TryFindResource($"{resName}PathGeometry") ?? Application.Current.TryFindResource(resName);

			image.Geometry = resource  as Geometry ?? Geometry.Empty;
			image.HasGeometry = resource != null;
		} // proc OnImageNameChanged

		/// <summary></summary>
		/// <param name="sizeInfo"></param>
		protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
		{
			var width = sizeInfo.NewSize.Width;
			var height = sizeInfo.NewSize.Height;
			if (!GeometryCircled || width == Double.NaN || height == Double.NaN || width <= 0.0 || height <= 0.0)
				GeometrySpacing = new Thickness(0);
			else
			{
				var x = 2 + Math.Ceiling((width - (width / Math.Sqrt(2))) / 2);
				var y = 2 + Math.Ceiling((height - (height / Math.Sqrt(2))) / 2);
				GeometrySpacing = new Thickness(x, y, x, y);
			}
			base.OnRenderSizeChanged(sizeInfo);
		} // proc OnRenderSizeChanged

		/// <summary>The property defines the resource to be loaded.</summary>
		public string GeometryName { get => (string)GetValue(GeometryNameProperty); set => SetValue(GeometryNameProperty, value); }
		/// <summary>The data to draw the image</summary>
		public Geometry Geometry { get => (Geometry)GetValue(GeometryProperty); set => SetValue(GeometryProperty, value); }
		/// <summary>Button has an image?</summary>
		public bool HasGeometry { get => BooleanBox.GetBool(GetValue(HasGeometryProperty)); private set => SetValue(hasGeometryPropertyKey, BooleanBox.GetObject(value)); }
		/// <summary></summary>
		public bool GeometryCircled { get=> BooleanBox.GetBool(GetValue(GeometryCircledProperty)); set => SetValue(GeometryCircledProperty, BooleanBox.GetObject(value)); }
		/// <summary>The property defines the inner distance between circle and image</summary>
		public Thickness GeometrySpacing { get => (Thickness)GetValue(GeometrySpacingProperty); private set => SetValue(geometrySpacingPropertyKey, value); }
		/// <summary></summary>
		public Brush Fill { get => (Brush)GetValue(FillProperty); set => SetValue(FillProperty, value); }

		static PpsGeometryImage()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PpsGeometryImage), new FrameworkPropertyMetadata(typeof(PpsGeometryImage)));
		}
	} // class class PpsGeometryImage
}
