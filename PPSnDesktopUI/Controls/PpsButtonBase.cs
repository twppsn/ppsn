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

namespace TecWare.PPSn.Controls
{
	#region -- class PpsButtonBase ----------------------------------------------------

	/// <summary></summary>
	public class PpsButtonBase : Button
	{
		/// <summary>The name of the resource</summary>
		public static readonly DependencyProperty ImageNameProperty = DependencyProperty.Register(nameof(ImageName), typeof(string), typeof(PpsButtonBase), new FrameworkPropertyMetadata(string.Empty, new PropertyChangedCallback(OnImageNameChanged)));

		private static readonly DependencyPropertyKey imagePathDataPropertyKey = DependencyProperty.RegisterReadOnly(nameof(ImagePathData), typeof(Geometry), typeof(PpsButtonBase), new FrameworkPropertyMetadata(Geometry.Empty));
		/// <summary>The Geometry to draw the image</summary>
		public static readonly DependencyProperty ImagePathDataProperty = imagePathDataPropertyKey.DependencyProperty;

		private static readonly DependencyPropertyKey hasImagePropertyKey = DependencyProperty.RegisterReadOnly(nameof(HasImage), typeof(bool), typeof(PpsButtonBase), new FrameworkPropertyMetadata(BooleanBox.False));
		/// <summary>Button has an image?</summary>
		public static readonly DependencyProperty HasImageProperty = hasImagePropertyKey.DependencyProperty;

		/// <summary>The diameter of the circle</summary>
		public static readonly DependencyProperty DiameterProperty = DependencyProperty.Register(nameof(Diameter), typeof(double), typeof(PpsButtonBase), new FrameworkPropertyMetadata(36.0, new PropertyChangedCallback(OnDiameterChanged)));

		private static readonly DependencyPropertyKey imageSpacingPropertyKey = DependencyProperty.RegisterReadOnly(nameof(ImageSpacing), typeof(double), typeof(PpsButtonBase), new FrameworkPropertyMetadata(8.0));
		/// <summary>The inner distance between circle and image</summary>
		public static readonly DependencyProperty ImageSpacingProperty = imageSpacingPropertyKey.DependencyProperty;

		private static void OnImageNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var btn = (PpsButtonBase)d;
			object resource = null;
			if (e.NewValue is string resName)
				resource = Application.Current.TryFindResource($"{resName}PathGeometry") ?? Application.Current.TryFindResource(resName);
			btn.ImagePathData = resource != null ? (Geometry)resource : Geometry.Empty;
			btn.HasImage = resource != null;
		} // proc OnImageNameChanged

		private static void OnDiameterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var btn = (PpsButtonBase)d;
			var diameter = (double)e.NewValue;
			btn.ImageSpacing = 2 + Math.Ceiling((diameter - (diameter / Math.Sqrt(2))) / 2);
		} // proc OnDiameterChanged

		/// <summary>The property defines the resource to be loaded.</summary>
		public string ImageName { get => (string)GetValue(ImageNameProperty); set => SetValue(ImageNameProperty, value); }
		/// <summary>The data to draw the image</summary>
		public Geometry ImagePathData { get => (Geometry)GetValue(ImagePathDataProperty); private set => SetValue(imagePathDataPropertyKey, value); }
		/// <summary>Button has an image?</summary>
		public bool HasImage { get => BooleanBox.GetBool(GetValue(HasImageProperty)); private set => SetValue(hasImagePropertyKey, BooleanBox.GetObject(value)); }
		/// <summary>The property defines the diameter of the circle</summary>
		public double Diameter { get => (double)GetValue(DiameterProperty); set => SetValue(DiameterProperty, value); }
		/// <summary>The property defines the inner distance between circle and image</summary>
		public double ImageSpacing { get => (double)GetValue(ImageSpacingProperty); private set => SetValue(imageSpacingPropertyKey, value); }
	} // class PpsButtonBase

	#endregion
}
