using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace TecWare.PPSn.Controls
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PPSnSelectionChangedBehavior
	{
		public static readonly DependencyProperty CommandProperty = DependencyProperty.RegisterAttached("Command",
			typeof(ICommand),
			typeof(PPSnSelectionChangedBehavior),
			new PropertyMetadata(PropertyChangedCallback));

		public static void PropertyChangedCallback(DependencyObject depObj, DependencyPropertyChangedEventArgs args)
		{
			var selector = depObj as Selector;
			if (selector == null)
				return;

			selector.SelectionChanged += new SelectionChangedEventHandler(SelectionChanged);
		}

		public static ICommand GetCommand(UIElement element)
		{
			return (ICommand)element.GetValue(CommandProperty);
		}

		public static void SetCommand(UIElement element, ICommand command)
		{
			element.SetValue(CommandProperty, command);
		}

		private static void SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			var selector = sender as Selector;
			if (selector == null)
				return;

			var command = selector.GetValue(CommandProperty) as ICommand;
			if (command == null)
				return;

			command.Execute(selector.SelectedItem);
		}
	} // class PPSnSelectionChangedBehavior

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PPSnComboBoxBehavior
	{
		public static object GetIsNullable(ComboBox comboBox)
		{
			return (bool)comboBox.GetValue(IsNullableProperty);
		}

		public static void SetIsNullable(ComboBox comboBox, object value)
		{
			comboBox.SetValue(IsNullableProperty, value);
		}

		public static readonly DependencyProperty IsNullableProperty =
			DependencyProperty.RegisterAttached(
				"IsNullable",
				typeof(bool),
				typeof(PPSnComboBoxBehavior),
				new PropertyMetadata(false)
				);
	} // class PPSnComboBoxBehavior

}
