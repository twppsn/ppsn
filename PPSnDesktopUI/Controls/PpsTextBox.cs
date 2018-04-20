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
using System.Globalization;
using System.Linq;
using System.Text;
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
		private const bool negativeToggling = true;

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
			this.AcceptsReturn = IsMultilineInput(newValue);
			this.AcceptsTab = newValue == PpsTextBoxInputType.Any;

			/*if (newValue != PpsTextBoxInputType.Text && !TryValidateInput(Text))
				ClearValue(TextProperty);

			if (oldValue == PpsTextBoxInputType.Text && newValue != PpsTextBoxInputType.Text)
				DataObject.AddPastingHandler(this, OnClipboardPasting);
			else
				DataObject.RemovePastingHandler(this, OnClipboardPasting);*/ // ToDo
		} // proc OnInputTypeChanged

		/// <summary>If text is entered my Keyboard do not process illegal chars (TextChanged is not called)</summary>
		/// <param name="e"></param>
		protected override void OnPreviewTextInput(TextCompositionEventArgs e)
		{
			if (!IsTextualInput(InputType))
			{
				if (!IsNegativeAllowed(InputType))
					if (e.Text.Except(LegalDecimalChars(false)).Any())
						e.Handled = true;
				if (!IsDecimalAllowed(InputType))
					if (e.Text.Except(LegalIntegerChars(true)).Any())
						e.Handled = true;
			}

			base.OnPreviewTextInput(e);
		} // func OnPreviewTextInput

		private void NeatlyReplaceText(string newText)
		{
			var curPos = this.CaretIndex;
			var curLen = Text.Length;
			Text = newText;
			curPos -= curLen - Text.Length;
			this.CaretIndex = curPos;
		}

		private void NeatlyCleanText()
		{
			// int type is Any nothing is to be checked
			if (InputType == PpsTextBoxInputType.Any || Text.Length < 1)
				return;

			var newText = new StringBuilder();
			var negative = false;
			var firstColonIndex = -1;

			// while checking the input, the Text is only parsed once
			foreach (var c in Text)
			{
				if (IsTextualInput(InputType))
				{
					if (c >= ' ')
					{
						newText.Append(c);
						continue;
					}
					if (IsMultilineInput(InputType))
					{
						if (c == '\n' || c == '\r')
						{
							newText.Append(c);
							continue;
						}
					}
				}

				if (LegalIntegers.Contains(c) || c == CultureInfo.CurrentCulture.NumberFormat.NumberGroupSeparator[0])
				{
					if (InputType != PpsTextBoxInputType.Number)
						if (newText.Length == 0)
							if (c == '0')
								continue;
					newText.Append(c);
					continue;
				}

				if (IsNegativeAllowed(InputType))
				{
					if (c == CultureInfo.CurrentCulture.NumberFormat.NegativeSign[0])
					{
						negative = negativeToggling ? !negative : true;
						continue;
					}
				}

				if (IsDecimalAllowed(InputType))
				{
					if (c == CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator[0])
					{
						if (firstColonIndex < 0)
						{
							newText.Append(c);
							firstColonIndex = newText.Length - 1;
							continue;
						}
						else
						{
							newText.Remove(firstColonIndex, 1);
							newText.Append(c);
							firstColonIndex = newText.Length - 1;
							continue;
						}
					}
				}
			} // foreach(var c in Text)

			if (IsTextualInput(InputType))
			{
				if (Text.Length == newText.Length)
				{
					// in case of text input if there is no change in length, there was no change in content
					return;
				}

				NeatlyReplaceText(newText.ToString());
				return;
			}

			if (negative)
				newText.Insert(0, CultureInfo.CurrentCulture.NumberFormat.NegativeSign);

			if (firstColonIndex == 0)
				newText.Insert(0, '0');

			var newTextString = newText.ToString();

			if (Text != newTextString)
				NeatlyReplaceText(newTextString);
		}

		private static bool IsMultilineInput(PpsTextBoxInputType inputType) => inputType == PpsTextBoxInputType.Any || inputType == PpsTextBoxInputType.Multiline;

		private static bool IsTextualInput(PpsTextBoxInputType inputType) => inputType == PpsTextBoxInputType.Any ||
			inputType == PpsTextBoxInputType.SingleLine ||
			inputType == PpsTextBoxInputType.Multiline;

		private static bool IsDecimalAllowed(PpsTextBoxInputType inputType) => inputType == PpsTextBoxInputType.Decimal || inputType == PpsTextBoxInputType.DecimalNegative;

		private static bool IsNegativeAllowed(PpsTextBoxInputType inputType) => inputType == PpsTextBoxInputType.IntegerNegative || inputType == PpsTextBoxInputType.DecimalNegative;

		private bool retriggerHold = false;

		/// <summary>Cleans the Text if it was pasted and in cases of contextual plausibility (p.e. only one minus sign)</summary>
		/// <param name="e"></param>
		protected override void OnTextChanged(TextChangedEventArgs e)
		{
			if (retriggerHold || InputType == PpsTextBoxInputType.Any)
				return;

			retriggerHold = true;

			NeatlyCleanText();

			retriggerHold = false;
		}

		/// <summary></summary>
		/// <param name="e"></param>
		protected override void OnPreviewKeyDown(KeyEventArgs e)
		{

			if (InputType == PpsTextBoxInputType.Integer)
			{
				//var c = GetCharFromKey(e.Key);
			}

		} // func OnPreviewKeyDown

		/// <summary>Legal chars which can be in a Number</summary>
		public const string LegalIntegers = "0123456789";

		/// <summary>Legal chars which can be in a Integer</summary>
		/// <param name="includeNegative">is the minus sign legal</param>
		/// <returns></returns>
		public static string LegalIntegerChars(bool includeNegative = false)
			=> LegalIntegers +
			   CultureInfo.CurrentCulture.NumberFormat.NumberGroupSeparator +
			   (includeNegative ? CultureInfo.CurrentCulture.NumberFormat.NegativeSign : String.Empty);

		/// <summary>Legal chars which can be in a Decimal</summary>
		/// <param name="includeNegative">is the minus sign legal</param>
		/// <returns></returns>
		public static string LegalDecimalChars(bool includeNegative = false)
			=> LegalIntegerChars(includeNegative) +
			   CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;

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
