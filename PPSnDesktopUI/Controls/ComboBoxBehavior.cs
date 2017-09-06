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
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace TecWare.PPSn.Controls
{
	#region -- class PpsSelectionChangedBehavior --------------------------------------

	/// <summary></summary>
	public static class PpsSelectionChangedBehavior
	{
		public static readonly DependencyProperty CommandProperty = DependencyProperty.RegisterAttached("Command", typeof(ICommand), typeof(PpsSelectionChangedBehavior), new FrameworkPropertyMetadata(PropertyChangedCallback));

		public static void PropertyChangedCallback(DependencyObject depObj, DependencyPropertyChangedEventArgs args)
		{
			if (depObj is Selector selector)
				selector.SelectionChanged += new SelectionChangedEventHandler(SelectionChanged);
		} // proc PropertyChangedCallback

		public static ICommand GetCommand(UIElement element)
			=> (ICommand)element.GetValue(CommandProperty);

		public static void SetCommand(UIElement element, ICommand command)
			=> element.SetValue(CommandProperty, command);
		
		private static void SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (sender is Selector selector && selector.GetValue(CommandProperty) is ICommand command)
				command.Execute(selector.SelectedItem);
		} // proc SelectionChanged
	} // class PpsSelectionChangedBehavior

	#endregion

	#region -- class PpsComboBoxBehavior ----------------------------------------------

	/// <summary></summary>
	public static class PpsComboBoxBehavior
	{
		public static readonly DependencyProperty IsNullableProperty = DependencyProperty.RegisterAttached("IsNullable", typeof(bool), typeof(PpsComboBoxBehavior), new FrameworkPropertyMetadata(false));

		public static object GetIsNullable(ComboBox comboBox)
			=> (bool)comboBox.GetValue(IsNullableProperty);
		
		public static void SetIsNullable(ComboBox comboBox, object value)
			=> comboBox.SetValue(IsNullableProperty, value);
	} // class PpsComboBoxBehavior

	#endregion
}
