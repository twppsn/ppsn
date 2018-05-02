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

namespace TecWare.PPSn.Controls
{
	/// <summary></summary>
	public class PpsButton : Button
	{
		/// <summary>The name of the resource</summary>
		public static readonly DependencyProperty GeometryNameProperty = PpsGeometryImage.GeometryNameProperty.AddOwner(typeof(PpsButton));
		/// <summary>The diameter of the circle</summary>
		public static readonly DependencyProperty GeometrySizeProperty = DependencyProperty.Register(nameof(GeometrySize), typeof(double), typeof(PpsButton), new FrameworkPropertyMetadata(36.0));
		
		/// <summary>The property defines the resource to be loaded.</summary>
		public string GeometryName { get => (string)GetValue(GeometryNameProperty); set => SetValue(GeometryNameProperty, value); }
		/// <summary>The property defines the diameter of the circle</summary>
		public double GeometrySize { get => (double)GetValue(GeometrySizeProperty); set => SetValue(GeometrySizeProperty, value); }
		
		static PpsButton()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PpsButton), new FrameworkPropertyMetadata(typeof(PpsButton)));
		}
	} // class PpsButton
}
