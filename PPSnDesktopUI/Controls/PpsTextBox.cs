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
using System.Windows.Input;

namespace TecWare.PPSn.Controls
{
	#region -- enum PpsTextBoxInputType -----------------------------------------------

	/// <summary></summary>
	public enum PpsTextBoxInputType
	{
		/// <summary>Input is not checked</summary>
		Any = 0,
		/// <summary>Single Line of unformatted Text is allowed</summary>
		SingleLine = 1,
		/// <summary>Multiple Lines of unformatted Text are allowed</summary>
		Multiline = 2,
		/// <summary>Input mask for integer values.</summary>
		Integer = 3,
		/// <summary>Input mask for decimal/float values.</summary>
		Decimal = 4,
		/// <summary>Integers, including negative Values</summary>
		IntegerNegative = 5,
		/// <summary>Decimals, includíng negative Values</summary>
		DecimalNegative = 6,
		/// <summary>any Text consisting of Numbers</summary>
		Number = 7

		///// <summary>Defined input mask e.g. special text numbers.</summary>
		//Formatted = 3
	} // enum PpsTextBoxInputType

	#endregion

	#region -- class PpsTextBox -------------------------------------------------------

	/// <summary>Extends Textbox for Number input and a clear button.</summary>
	public class PpsTextBox : TextBox
	{
		/// <summary></summary>
		public static readonly DependencyProperty InputTypeProperty = DependencyProperty.Register(nameof(InputType), typeof(PpsTextBoxInputType), typeof(PpsTextBox), new FrameworkPropertyMetadata(PpsTextBoxInputType.SingleLine, new PropertyChangedCallback(OnInputTypeChangedCallback)));
		/// <summary></summary>
		public static readonly DependencyProperty IsMandatoryProperty = DependencyProperty.Register(nameof(IsMandatory), typeof(bool), typeof(PpsTextBox), new FrameworkPropertyMetadata(false));
		/// <summary></summary>
		public static readonly DependencyProperty IsNullableProperty = DependencyProperty.Register(nameof(IsNullable), typeof(bool), typeof(PpsTextBox), new FrameworkPropertyMetadata(true));

		/// <summary></summary>
		public static readonly RoutedCommand ClearTextCommand = new RoutedUICommand("ClearText", "ClearText", typeof(PpsTextBox));

		/// <summary>Mandatory field.</summary>
		public bool IsMandatory { get => BooleanBox.GetBool(GetValue(IsMandatoryProperty)); set => SetValue(IsMandatoryProperty, BooleanBox.GetObject(value)); }
		/// <summary>Is the field nullable.</summary>
		public bool IsNullable { get => BooleanBox.GetBool(GetValue(IsNullableProperty)); set => SetValue(IsNullableProperty, BooleanBox.GetObject(value)); }

		private static void OnInputTypeChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsTextBox)d).OnInputTypeChanged((PpsTextBoxInputType)e.NewValue, (PpsTextBoxInputType)e.OldValue);

		/// <summary></summary>
		/// <param name="newValue"></param>
		/// <param name="oldValue"></param>
		protected virtual void OnInputTypeChanged(PpsTextBoxInputType newValue, PpsTextBoxInputType oldValue)
		{
			if (newValue != PpsTextBoxInputType.Text && !TryValidateInput(Text))
				ClearValue(TextProperty);

			if (oldValue == PpsTextBoxInputType.Text && newValue != PpsTextBoxInputType.Text)
				DataObject.AddPastingHandler(this, OnClipboardPasting);
			else
				DataObject.RemovePastingHandler(this, OnClipboardPasting);
		} // proc OnInputTypeChanged

		/// <summary>Initialize per Input a key filter.</summary>
		/// <param name="e"></param>
		protected override void OnPreviewTextInput(TextCompositionEventArgs e)
		{
			base.OnPreviewTextInput(e);

			if (InputType != PpsTextBoxInputType.Text)
			{
				//e.TextComposition
			}
		} // func OnPreviewTextInput

		/// <summary></summary>
		/// <param name="e"></param>
		protected override void OnPreviewKeyDown(KeyEventArgs e)
		{
			base.OnPreviewKeyDown(e);
		} // func OnPreviewKeyDown

		private void OnClipboardPasting(object sender, DataObjectPastingEventArgs e)
		{
		} // proc OnClipboardPasting

		private bool TryValidateInput(string text)
		{
			switch (InputType)
			{
				case PpsTextBoxInputType.Decimal:
				case PpsTextBoxInputType.Integer:
				case PpsTextBoxInputType.Any:
					break;
			}
			return true;
		} // func TryValidateInput

		//private static bool ValidateInteger(string text)
		//{
		//	if (text == "-")
		//		return true;

		//	var numberStyles = NumberStyles.AllowLeadingSign | NumberStyles.AllowThousands;
		//	int value;
		//	if (!int.TryParse(text, numberStyles, CultureInfo.CurrentUICulture, out value))
		//		return false;

		//	return true;
		//}

		//private static bool ValidateDecimal(string text)
		//{
		//	if (text == "-")
		//		return true;
		//	var numberStyles = NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign | NumberStyles.AllowThousands;
		//	decimal value;
		//	if (!Decimal.TryParse(text, numberStyles, CultureInfo.CurrentUICulture, out value))
		//		return false;

		//	return true;
		//}


		//private static void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
		//{
		//	var textBox = sender as TextBox;
		//	var inputType = GetInputType(textBox);
		//	var proposedText = GetProposedText(textBox, e.Text);

		//	if (!ValidateString(proposedText, inputType))
		//	{
		//		e.Handled = true;
		//	}
		//}

		//// pressing space and backspace doesn't raise PreviewTextInput
		//private static void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
		//{
		//	var textBox = sender as TextBox;
		//	var inputType = GetInputType(textBox);

		//	string proposedText = null;
		//	if (e.Key == Key.Space)
		//	{
		//		proposedText = GetProposedText(textBox, " ");
		//	}
		//	else if (e.Key == Key.Back)
		//	{
		//		proposedText = GetProposedTextBackspace(textBox);
		//	}

		//	if (proposedText == null || proposedText == String.Empty)
		//	{
		//		return;
		//	}


		//	if (!ValidateString(proposedText, inputType))
		//	{
		//		e.Handled = true;
		//	}
		//}

		//private static void TextBox_PastingEventHandler(object sender, DataObjectPastingEventArgs e)
		//{
		//	if (e.DataObject.GetDataPresent(typeof(string)))
		//	{
		//		var textBox = sender as TextBox;
		//		var pastedText = e.DataObject.GetData(typeof(string)) as string;
		//		var proposedText = GetProposedText(textBox, pastedText);
		//		var inputType = GetInputType(textBox);

		//		if (!ValidateString(proposedText, inputType))
		//		{
		//			e.CancelCommand();
		//		}
		//	}
		//	else
		//	{
		//		e.CancelCommand();
		//	}
		//}

		//private static string GetProposedText(TextBox textBox, string newText)
		//{
		//	var text = textBox.Text;

		//	if (textBox.SelectionStart != -1)
		//	{
		//		text = text.Remove(textBox.SelectionStart, textBox.SelectionLength);
		//	}

		//	text = text.Insert(textBox.CaretIndex, newText);

		//	return text;
		//} // func GetProposedText

		//private static string GetProposedTextBackspace(TextBox textBox)
		//{
		//	var text = GetTextWithSelectionRemoved(textBox);
		//	if (textBox.SelectionStart > 0 && textBox.SelectionLength == 0)
		//	{
		//		text = text.Remove(textBox.SelectionStart - 1, 1);
		//	}
		//	return text;
		//}

		//private static string GetTextWithSelectionRemoved(TextBox textBox)
		//{
		//	var text = textBox.Text;
		//	if (textBox.SelectionStart != -1)
		//	{
		//		text = text.Remove(textBox.SelectionStart, textBox.SelectionLength);
		//	}
		//	return text;
		//}

		/// <summary>Defines the input mask for the textbox.</summary>
		public PpsTextBoxInputType InputType { get => (PpsTextBoxInputType)GetValue(InputTypeProperty); set => SetValue(InputTypeProperty, value); }
	} // class PpsTextBox

	#endregion
}
