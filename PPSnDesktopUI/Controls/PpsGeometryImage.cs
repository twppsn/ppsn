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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace TecWare.PPSn.Controls
{
	/// <summary></summary>
	public class PpsGeometryImage : Control
	{
		/// <summary></summary>
		public static readonly DependencyProperty FillProperty = Shape.FillProperty.AddOwner(typeof(PpsGeometryImage), new FrameworkPropertyMetadata(Brushes.Transparent));

		/// <summary>The name of the resource</summary>
		public static readonly DependencyProperty GeometryNameProperty = DependencyProperty.Register(nameof(GeometryName), typeof(string), typeof(PpsGeometryImage), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnGeometryNameChanged)));

		/// <summary>The Geometry to draw the image</summary>
		public static readonly DependencyProperty GeometryProperty = DependencyProperty.Register(nameof(PpsGeometryImage.Geometry), typeof(Geometry), typeof(PpsGeometryImage), new FrameworkPropertyMetadata(null));
		/// <summary></summary>
		public static readonly DependencyProperty GeometryCircledProperty = DependencyProperty.Register(nameof(GeometryCircled), typeof(bool), typeof(PpsGeometryImage), new FrameworkPropertyMetadata(true));

		private static readonly DependencyPropertyKey hasImagePropertyKey = DependencyProperty.RegisterReadOnly(nameof(HasGeometry), typeof(bool), typeof(PpsGeometryImage), new FrameworkPropertyMetadata(BooleanBox.False));
		/// <summary>Button has an image?</summary>
		public static readonly DependencyProperty HasGeometryProperty = hasImagePropertyKey.DependencyProperty;

		/// <summary>The diameter of the circle</summary>
		public static readonly DependencyProperty DiameterProperty = DependencyProperty.Register(nameof(Diameter), typeof(double), typeof(PpsGeometryImage), new FrameworkPropertyMetadata(36.0, new PropertyChangedCallback(OnDiameterChanged)));

		private static readonly DependencyPropertyKey geometrySpacingPropertyKey = DependencyProperty.RegisterReadOnly(nameof(GeometrySpacing), typeof(Thickness), typeof(PpsGeometryImage), new FrameworkPropertyMetadata(new Thickness(8.0)));
		/// <summary>The inner distance between circle and image</summary>
		public static readonly DependencyProperty GeometrySpacingProperty = geometrySpacingPropertyKey.DependencyProperty;

		private static void OnGeometryNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var image = (PpsGeometryImage)d;
			object resource = null;
			if (e.NewValue is string resName)
				resource = Application.Current.TryFindResource($"{resName}PathGeometry") ?? Application.Current.TryFindResource(resName);

			image.Geometry = resource != null ? (Geometry)resource : Geometry.Empty;
			image.HasGeometry = resource != null;
		} // proc OnImageNameChanged

		private static void OnDiameterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var image = (PpsGeometryImage)d;
			var diameter = (double)e.NewValue;
			image.GeometrySpacing = new Thickness(2 + Math.Ceiling((diameter - (diameter / Math.Sqrt(2))) / 2));
		} // proc OnDiameterChanged

		/// <summary>The property defines the resource to be loaded.</summary>
		public string GeometryName { get => (string)GetValue(GeometryNameProperty); set => SetValue(GeometryNameProperty, value); }
		/// <summary>The data to draw the image</summary>
		public Geometry Geometry { get => (Geometry)GetValue(GeometryProperty); set => SetValue(GeometryProperty, value); }
		/// <summary>Button has an image?</summary>
		public bool HasGeometry { get => BooleanBox.GetBool(GetValue(HasGeometryProperty)); private set => SetValue(hasImagePropertyKey, BooleanBox.GetObject(value)); }
		/// <summary></summary>
		public bool GeometryCircled { get=> BooleanBox.GetBool(GetValue(GeometryCircledProperty)); set => SetValue(GeometryCircledProperty, BooleanBox.GetObject(value)); }
		/// <summary>The property defines the diameter of the circle</summary>
		public double Diameter { get => (double)GetValue(DiameterProperty); set => SetValue(DiameterProperty, value); }
		/// <summary>The property defines the inner distance between circle and image</summary>
		public Thickness GeometrySpacing { get => (Thickness)GetValue(GeometrySpacingProperty); private set => SetValue(geometrySpacingPropertyKey, value); }
		/// <summary></summary>
		public Brush Fill { get => (Brush)GetValue(FillProperty); set => SetValue(FillProperty, value); }

		static PpsGeometryImage()
		{
			OpacityProperty.OverrideMetadata(typeof(PpsGeometryImage), new FrameworkPropertyMetadata(0.65));
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PpsGeometryImage), new FrameworkPropertyMetadata(typeof(PpsGeometryImage)));
		}
	} // class class PpsGeometryImage
}
