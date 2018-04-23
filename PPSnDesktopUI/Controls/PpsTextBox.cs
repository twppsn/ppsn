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
		private const bool negativeToggling = false;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public static readonly DependencyProperty InputTypeProperty = DependencyProperty.Register(nameof(InputType), typeof(PpsTextBoxInputType), typeof(PpsTextBox), new FrameworkPropertyMetadata(PpsTextBoxInputType.SingleLine, new PropertyChangedCallback(OnInputTypeChangedCallback)));
		public static readonly DependencyProperty IsNullableProperty = DependencyProperty.Register(nameof(IsNullable), typeof(bool), typeof(PpsTextBox), new FrameworkPropertyMetadata(true));
		public static readonly DependencyProperty ErrorMessageProperty = DependencyProperty.Register(nameof(ErrorMessage), typeof(string), typeof(PpsTextBox));

		/// <summary>Sets the allowed Lines for this Textbox</summary>
		public static readonly DependencyProperty AllowedLineCountProperty = DependencyProperty.Register(nameof(AllowedLineCount), typeof(int), typeof(PpsTextBox), new FrameworkPropertyMetadata(1));

		public static readonly RoutedCommand ClearTextCommand = new RoutedUICommand("ClearText", "ClearText", typeof(PpsTextBox));
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		/// <summary>Is the field nullable.</summary>
		public bool IsNullable { get => BooleanBox.GetBool(GetValue(IsNullableProperty)); set => SetValue(IsNullableProperty, BooleanBox.GetObject(value)); }
		/// <summary>Sets the allowed Lines for this Textbox</summary>
		public int AllowedLineCount { get => (int)GetValue(AllowedLineCountProperty); set => SetValue(AllowedLineCountProperty, value); }
		public string ErrorMessage { get => (string)GetValue(ErrorMessageProperty); set => SetValue(ErrorMessageProperty, value); }

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
			if (InputType == PpsTextBoxInputType.Any || Text.Length < 1)
				return;

			var newText = new StringBuilder();
			var negative = false;
			var firstColonIndex = -1;
			var remainingLines = AllowedLineCount - 1;
			var lastWasNewline = false;
			var lastWasCarriagereturn = false;

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
						if (c == '\n')
						{
							if (lastWasCarriagereturn)
								if (remainingLines > 0)
								{
									newText.Append(c);
									lastWasCarriagereturn = false;
									remainingLines--;
									continue;
								}
								else
								{
									lastWasCarriagereturn = false;
									newText.Remove(newText.Length - 1, 1);
									continue;
								}
							else
							{
								if (remainingLines > 0)
								{
									newText.Append(c);
									lastWasNewline = true;
									remainingLines--;
									continue;
								}
								else
								{
									continue;
								}
							}
						}
						if (c == '\r')
						{
							if (lastWasNewline)
							{
								newText.Append(c);
								lastWasNewline = false;
								continue;
							}
							if (remainingLines < 1)
								continue;
							else
							{
								newText.Append(c);
								lastWasCarriagereturn = true;
								continue;
							}
						}
					}
				}

				lastWasCarriagereturn = false;
				lastWasNewline = false;

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

		/// <summary>Defines the input mask for the textbox.</summary>
		public PpsTextBoxInputType InputType { get => (PpsTextBoxInputType)GetValue(InputTypeProperty); set => SetValue(InputTypeProperty, value); }
	} // class PpsTextBox

	#endregion
}
