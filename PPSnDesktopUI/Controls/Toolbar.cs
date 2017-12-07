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
using System.Windows.Media.Effects;

namespace TecWare.PPSn.Controls
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PpsToolbarDataTemplateSelector : DataTemplateSelector
	{
		public override DataTemplate SelectTemplate(object item, DependencyObject container)
		{
			var element = container as FrameworkElement;
			if (item == null)
				return (DataTemplate)element.FindResource("Seperator");
			else
			{
				var template = element.FindResource(item.GetType());
				if (template is DataTemplate)
					return (DataTemplate)template;
				return (DataTemplate)element.FindResource("ToolbarTemplateError");
			}
		} // func SelectTemplate
	} // class PpsToolbarDataTemplateSelector

	//public class GrayShader : ShaderEffect
	//{
	//	public GrayShader()
	//	{
	//		PixelShader = new System.Windows.Media.Effects.PixelShader() { UriSource = new Uri(@"file://C:\Projects\PPSnOS\twppsn\PPSnWpf\PPSnDesktopUI\Controls\text.fx.ps") };
	//		UpdateShaderValue(InputProperty);
	//	}

	//	public Brush Input
	//	{
	//		get { return (Brush)GetValue(InputProperty); }
	//		set { SetValue(InputProperty, value); }
	//	}

	//	public static readonly DependencyProperty InputProperty = ShaderEffect.RegisterPixelShaderSamplerProperty("Input", typeof(GrayShader), 0);
	//}
}
