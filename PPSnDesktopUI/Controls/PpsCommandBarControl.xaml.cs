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
using TecWare.PPSn.UI;

namespace TecWare.PPSn.Controls
{
	/// <summary>Designs for CommandButton</summary>
	public enum CommandBarMode
	{
		/// <summary>this is for Round Buttons</summary>
		Round,
		/// <summary>this is for Rectangular Buttons</summary>
		Rectangle
	}

	/// <summary>
	/// Interaction logic for PpsCommandBarControl.xaml
	/// </summary>
	public partial class PpsCommandBarControl : UserControl
	{
		/// <summary>PpsUICommandCollection of the available Commands</summary>
		public static DependencyProperty CommandsProperty = DependencyProperty.Register(nameof(Commands), typeof(PpsUICommandCollection), typeof(PpsCommandBarControl), new PropertyMetadata(new PpsUICommandCollection()));
		/// <summary>PpsUICommandCollection of the available Commands</summary>
		public PpsUICommandCollection Commands { get => (PpsUICommandCollection)GetValue(CommandsProperty); set => SetValue(CommandsProperty, value); }

		/// <summary>Sets the Style for the UI</summary>
		public static DependencyProperty ModeProperty = DependencyProperty.Register(nameof(Mode), typeof(CommandBarMode), typeof(PpsCommandBarControl), new PropertyMetadata(CommandBarMode.Round));
		/// <summary>Sets the Style for the UI</summary>
		public CommandBarMode Mode { get => (CommandBarMode)GetValue(ModeProperty); set => SetValue(ModeProperty, value); }
		
		/// <summary>standard constructor</summary>
		public PpsCommandBarControl()
		{
			InitializeComponent();
		}
	}

	/// <summary>This DataTemplateSelector selects the Template according to the type of CommandButton</summary>
	public class PpsCommandBarDataTemplateSelector : DataTemplateSelector
	{
		/// <summary>Selects the Template according to the type of CommandButton</summary>
		/// <param name="item">Ancestor of PpsUICommandButton</param>
		/// <param name="container"></param>
		/// <returns>the Template for the Button</returns>
		public override DataTemplate SelectTemplate(object item, DependencyObject container)
		{
			var element = container as FrameworkElement;
			if (item == null)
				return (DataTemplate)element.FindResource("Separator");
			else
			{
				var template = element.FindResource(item.GetType());
				if (template is DataTemplate)
					return (DataTemplate)template;
				return null; // ToDo: fallback
			}
		} // func SelectTemplate
	} // class PpsToolbarDataTemplateSelector
}
