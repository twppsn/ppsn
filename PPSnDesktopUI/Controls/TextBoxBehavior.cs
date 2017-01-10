using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TecWare.PPSn.Controls
{
	public class PPSnNumTextBoxBehavior
	{
		public static PPSnNumTextBoxInputType GetInputType(TextBox textBox)
		{
			return (PPSnNumTextBoxInputType)textBox.GetValue(InputTypeProperty);
		}

		public static void SetInputType(TextBox textBox, PPSnNumTextBoxInputType value)
		{
			textBox.SetValue(InputTypeProperty, value);
		}

		public static readonly DependencyProperty InputTypeProperty =
			DependencyProperty.RegisterAttached(
				"InputType",
				typeof(PPSnNumTextBoxInputType),
				typeof(PPSnNumTextBoxBehavior),
				new FrameworkPropertyMetadata(InputTypeChangedCallback)
				);

		private static void InputTypeChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var textBox = d as TextBox;
			if (textBox == null)
				return;

			var inputType = e.NewValue;
			textBox.PreviewTextInput -= textBox_PreviewTextInput;
			textBox.PreviewKeyDown -= textBox_PreviewKeyDown;
			DataObject.RemovePastingHandler(textBox, textBox_PastingEventHandler);

			if (inputType == null)
			{
				textBox.ClearValue(InputTypeProperty);
			}
			else
			{
				SetInputType(textBox, (PPSnNumTextBoxInputType)inputType);
				textBox.PreviewTextInput += textBox_PreviewTextInput;
				textBox.PreviewKeyDown += textBox_PreviewKeyDown;
				DataObject.AddPastingHandler(textBox, textBox_PastingEventHandler);
			}
		}

		private static void textBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
		{
			var textBox = sender as TextBox;
			var inputType = GetInputType(textBox);
			var proposedText = GetProposedText(textBox, e.Text);

			if(!ValidateString(proposedText, inputType))
			{
				e.Handled = true;
			}
		}

		// pressing space and backspace doesn't raise PreviewTextInput
		private static void textBox_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			var textBox = sender as TextBox;
			var inputType = GetInputType(textBox);

			string proposedText = null;
			if (e.Key == Key.Space)
			{
				proposedText = GetProposedText(textBox, " ");
			}
			else if (e.Key == Key.Back)
			{
				proposedText = GetProposedTextBackspace(textBox);
			}

			if (proposedText == null || proposedText == String.Empty)
			{
				return;
			}


			if (!ValidateString(proposedText, inputType))
			{
				e.Handled = true;
			}
		}

		private static void textBox_PastingEventHandler(object sender, DataObjectPastingEventArgs e)
		{
			if (e.DataObject.GetDataPresent(typeof(string)))
			{
				var textBox = sender as TextBox;
				var pastedText = e.DataObject.GetData(typeof(string)) as string;
				var proposedText = GetProposedText(textBox, pastedText);
				var inputType = GetInputType(textBox);

				if (!ValidateString(proposedText, inputType))
				{
					e.CancelCommand();
				}
			}
			else
			{
				e.CancelCommand();
			}
		}

		private static string GetProposedText(TextBox textBox, string newText)
		{
			var text = textBox.Text;

			if (textBox.SelectionStart != -1)
			{
				text = text.Remove(textBox.SelectionStart, textBox.SelectionLength);
			}

			text = text.Insert(textBox.CaretIndex, newText);

			return text;
		} // func GetProposedText

		private static string GetProposedTextBackspace(TextBox textBox)
		{
			var text = GetTextWithSelectionRemoved(textBox);
			if (textBox.SelectionStart > 0 && textBox.SelectionLength == 0)
			{
				text = text.Remove(textBox.SelectionStart - 1, 1);
			}
			return text;
		}

		private static string GetTextWithSelectionRemoved(TextBox textBox)
		{
			var text = textBox.Text;
			if (textBox.SelectionStart != -1)
			{
				text = text.Remove(textBox.SelectionStart, textBox.SelectionLength);
			}
			return text;
		}

		private static bool ValidateString(string text, PPSnNumTextBoxInputType inputType)
		{
			switch(inputType)
			{
				case PPSnNumTextBoxInputType.Integer:
					return ValidateInteger(text);
				case PPSnNumTextBoxInputType.Decimal:
					return ValidateDecimal(text);
				default:
					return false;
			}
		}

		private static bool ValidateInteger(string text)
		{
			if(text == "-")
				return true;

			var numberStyles = NumberStyles.AllowLeadingSign | NumberStyles.AllowThousands;
			int value;
			if (!int.TryParse(text, numberStyles, CultureInfo.CurrentUICulture, out value))
				return false;

			return true;
		}

		private static bool ValidateDecimal(string text)
		{
			if(text == "-")
				return true;
			var numberStyles = NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign | NumberStyles.AllowThousands;
			decimal value;
			if (!Decimal.TryParse(text, numberStyles, CultureInfo.CurrentUICulture, out value))
				return false;

			return true;
		}

	} // class PPSnNumTextBoxBehavior

	public enum PPSnNumTextBoxInputType
	{
		Integer = 1,
		Decimal = 2
	} // enum PPSnNumTextBoxInputType

}
