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
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using TecWare.PPSn.Themes;
using TecWare.PPSn.UI;

namespace TecWare.PPSn.Controls
{
	/// <summary></summary>
	public class PpsGeometryImage : Control
	{
		#region -- OnRenderSizeChanged ------------------------------------------------

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

		#endregion

		#region -- Fill - property ----------------------------------------------------

		/// <summary>Background fill color for circled geometries.</summary>
		public static readonly DependencyProperty FillProperty = Shape.FillProperty.AddOwner(typeof(PpsGeometryImage), new FrameworkPropertyMetadata(Brushes.Transparent));

		/// <summary>Background fill color for circled geometries.</summary>
		public Brush Fill { get => (Brush)GetValue(FillProperty); set => SetValue(FillProperty, value); }

		#endregion

		#region -- GeometryCircle - property ------------------------------------------

		private static readonly DependencyPropertyKey geometrySpacingPropertyKey = DependencyProperty.RegisterReadOnly(nameof(GeometrySpacing), typeof(Thickness), typeof(PpsGeometryImage), new FrameworkPropertyMetadata(new Thickness(8.0)));
		/// <summary>The inner distance between circle and image</summary>
		public static readonly DependencyProperty GeometrySpacingProperty = geometrySpacingPropertyKey.DependencyProperty;

		/// <summary>Render a circle around the geometry.</summary>
		public static readonly DependencyProperty GeometryCircledProperty = DependencyProperty.Register(nameof(GeometryCircled), typeof(bool), typeof(PpsGeometryImage), new FrameworkPropertyMetadata(BooleanBox.False, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));

		/// <summary>Render a circle around the geometry.</summary>
		public bool GeometryCircled { get => BooleanBox.GetBool(GetValue(GeometryCircledProperty)); set => SetValue(GeometryCircledProperty, BooleanBox.GetObject(value)); }
		/// <summary>The property defines the inner distance between circle and image</summary>
		public Thickness GeometrySpacing { get => (Thickness)GetValue(GeometrySpacingProperty); private set => SetValue(geometrySpacingPropertyKey, value); }

		#endregion

		#region -- AccentForeground - property ----------------------------------------

		/// <summary>Second color to render the geometry2.</summary>
		public static readonly DependencyProperty AccentForegroundProperty = DependencyProperty.Register(nameof(AccentForeground), typeof(Brush), typeof(PpsGeometryImage), new FrameworkPropertyMetadata(Brushes.Transparent));

		/// <summary>Second color to render the geometry2.</summary>
		public Brush AccentForeground { get => (Brush)GetValue(AccentForegroundProperty); set => SetValue(AccentForegroundProperty, value); }

		#endregion

		#region -- Geometry -----------------------------------------------------------

		private static readonly DependencyPropertyKey foregroundGeometryPropertyKey = DependencyProperty.RegisterReadOnly(nameof(ForegroundGeometry), typeof(Geometry), typeof(PpsGeometryImage), new FrameworkPropertyMetadata(Geometry.Empty, new PropertyChangedCallback(OnForegroundGeometryChanged)));
		private static readonly DependencyPropertyKey accentForegroundGeometryPropertyKey = DependencyProperty.RegisterReadOnly(nameof(AccentForegroundGeometry), typeof(Geometry), typeof(PpsGeometryImage), new FrameworkPropertyMetadata(Geometry.Empty, new PropertyChangedCallback(OnAccentForegroundGeometryChanged)));
		private static readonly DependencyPropertyKey hasGeometryPropertyKey = DependencyProperty.RegisterReadOnly(nameof(HasGeometry), typeof(bool), typeof(PpsGeometryImage), new FrameworkPropertyMetadata(BooleanBox.False));
		private static readonly DependencyPropertyKey hasAccentGeometryPropertyKey = DependencyProperty.RegisterReadOnly(nameof(HasAccentGeometry), typeof(bool), typeof(PpsGeometryImage), new FrameworkPropertyMetadata(BooleanBox.False));

		/// <summary>The property defines the resource to be loaded.</summary>
		public static readonly DependencyProperty GeometryNameProperty = DependencyProperty.Register(nameof(GeometryName), typeof(string), typeof(PpsGeometryImage), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnGeometryNameChanged)));
		/// <summary>Geometry to render.</summary>
		public static readonly DependencyProperty GeometryProperty = DependencyProperty.Register(nameof(Geometry), typeof(Geometry), typeof(PpsGeometryImage), new FrameworkPropertyMetadata(Geometry.Empty, new PropertyChangedCallback(OnGeometryChanged)));
		/// <summary>Foreground part of the geometry.</summary>
		public static readonly DependencyProperty ForegroundGeometryProperty = foregroundGeometryPropertyKey.DependencyProperty;
		/// <summary>Accent foreground color for the second geometry.</summary>
		public static readonly DependencyProperty AccentForegroundGeometryProperty = accentForegroundGeometryPropertyKey.DependencyProperty;
		/// <summary>Has this image a geometry set.</summary>
		public static readonly DependencyProperty HasGeometryProperty = hasGeometryPropertyKey.DependencyProperty;
		/// <summary>Has this image a geometry set.</summary>
		public static readonly DependencyProperty HasAccentGeometryProperty = hasAccentGeometryPropertyKey.DependencyProperty;

		private static void OnGeometryNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var image = (PpsGeometryImage)d;
			var resource = e.NewValue is string resName ? TryFindGeometryByName(resName) : null;

			image.OnGeometryChanged(resource as Geometry, true);
		} // proc OnImageNameChanged

		private static void OnGeometryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsGeometryImage)d).OnGeometryChanged(e.NewValue as Geometry, false);

		private void OnGeometryChanged(Geometry geometry, bool updateGeometryProperty)
		{
			if (geometry == null || geometry.IsEmpty())
			{
				if (updateGeometryProperty)
					Geometry = Geometry.Empty;

				SetValue(foregroundGeometryPropertyKey, Geometry.Empty);
				SetValue(accentForegroundGeometryPropertyKey, Geometry.Empty);
			}
			else if (geometry is CombinedGeometry combinedGeometry && combinedGeometry.GeometryCombineMode == GeometryCombineMode.Union)
			{
				SetValue(foregroundGeometryPropertyKey, combinedGeometry.Geometry1);
				SetValue(accentForegroundGeometryPropertyKey, combinedGeometry.Geometry2);
			}
			else
			{
				SetValue(foregroundGeometryPropertyKey, geometry);
				SetValue(accentForegroundGeometryPropertyKey, Geometry.Empty);
			}
		} // proc OnGeometryChanged

		private static void OnForegroundGeometryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> d.SetValue(hasGeometryPropertyKey, BooleanBox.GetObject(e.NewValue is Geometry geometry && !geometry.IsEmpty()));

		private static void OnAccentForegroundGeometryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> d.SetValue(hasAccentGeometryPropertyKey, BooleanBox.GetObject(e.NewValue is Geometry geometry && !geometry.IsEmpty()));

		/// <summary>The property defines the resource to be loaded.</summary>
		public string GeometryName { get => (string)GetValue(GeometryNameProperty); set => SetValue(GeometryNameProperty, value); }
		/// <summary>The data to draw the image</summary>
		public Geometry Geometry { get => (Geometry)GetValue(GeometryProperty); set => SetValue(GeometryProperty, value); }
		/// <summary>Foreground part of the geometry.</summary>
		public Geometry ForegroundGeometry => (Geometry)GetValue(ForegroundGeometryProperty);
		/// <summary>Accent foreground color for the second geometry.</summary>
		public Geometry AccentForegroundGeometry => (Geometry)GetValue(ForegroundGeometryProperty);

		/// <summary>Has this image a geometry set.</summary>
		public bool HasGeometry => BooleanBox.GetBool(GetValue(HasGeometryProperty));
		/// <summary>Has this image a geometry set.</summary>
		public bool HasAccentGeometry => BooleanBox.GetBool(GetValue(HasAccentGeometryProperty)); 

		#endregion
		
		static PpsGeometryImage()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PpsGeometryImage), new FrameworkPropertyMetadata(typeof(PpsGeometryImage)));
		}

		#region -- GeometryConverter --------------------------------------------------

		private sealed class GeometryConverterImplementation : IValueConverter
		{
			public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
			{
				if (value is Geometry)
					return value;
				else if (value is string name)
					return TryFindGeometryByName(name);
				else
					return value;
			} // func Convert

			public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) 
				=> throw new NotSupportedException();
		}

		private static object TryFindGeometryByName(string geometryName)
		{
			return geometryName.EndsWith("PathGeometry")
				? Application.Current.TryFindResource(geometryName)
				: Application.Current.TryFindResource($"{geometryName}PathGeometry")
					?? Application.Current.TryFindResource(geometryName)
					?? (PpsTheme.TryGetNamedResourceKey(geometryName, out var key) ? Application.Current.TryFindResource(key) : null);
		} // func TryFindGeometryByName

		/// <summary>Convert to enforce a geometry.</summary>
		public static IValueConverter GeometryConverter { get; } = new GeometryConverterImplementation();

		#endregion
	} // class class PpsGeometryImage
}
