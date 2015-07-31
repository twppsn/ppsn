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

	public class GrayShader : ShaderEffect
	{
		public GrayShader()
		{
			PixelShader = new System.Windows.Media.Effects.PixelShader() { UriSource = new Uri(@"file://C:\Projects\PPSnOS\twppsn\PPSnWpf\PPSnDesktopUI\Controls\text.fx.ps") };
			UpdateShaderValue(InputProperty);
		}

		public Brush Input
		{
			get { return (Brush)GetValue(InputProperty); }
			set { SetValue(InputProperty, value); }
		}

		public static readonly DependencyProperty InputProperty = ShaderEffect.RegisterPixelShaderSamplerProperty("Input", typeof(GrayShader), 0);
	}
}
