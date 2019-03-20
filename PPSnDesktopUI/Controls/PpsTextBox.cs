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
using System.Collections;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace TecWare.PPSn.Controls
{
	#region -- enum PpsTextBoxInputType -----------------------------------------------

	/// <summary>Possible Inputtypes to specify the valid data.</summary>
	public enum PpsTextBoxInputType
	{
		/// <summary>Input is not checked</summary>
		None = 0,
		/// <summary>Multiple Lines of unformatted Text are allowed</summary>
		MultiLine,
		/// <summary>Multiple Lines of unformatted Text are allowed</summary>
		RichMultiLine,
		/// <summary>Input mask for integer values.</summary>
		Integer,
		/// <summary>Input mask for decimal/float values.</summary>
		Decimal,
		/// <summary>Integers, including negative Values</summary>
		IntegerNegative,
		/// <summary>Decimals, includíng negative Values</summary>
		DecimalNegative,

		/// <summary>Input a date.</summary>
		Date,
		/// <summary>Input a time.</summary>
		Time,

		/// <summary>Defined input mask e.g. special text numbers.</summary>
		Formatted
	} // enum PpsTextBoxInputType

	#endregion enum PpsTextBoxInputType

	#region -- class PpsTextBox -------------------------------------------------------

	/// <summary>Extends Textbox for Number input and a clear button.</summary>
	public class PpsTextBox : TextBox, IPpsNullableControl
	{
		/// <summary>Show the drop down.</summary>
		public static readonly RoutedCommand DropDownCommand = new RoutedCommand("DropDown", typeof(PpsTextBox));

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public static readonly DependencyProperty DropDownSourceProperty = DependencyProperty.Register(nameof(DropDownSource), typeof(object), typeof(PpsTextBox), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnDropDownSourceChanged)));
		private static readonly DependencyPropertyKey hasDropDownSourcePropertyKey = DependencyProperty.RegisterReadOnly(nameof(HasDropDownSource), typeof(bool), typeof(PpsTextBox), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty HasDropDownSourceProperty = hasDropDownSourcePropertyKey.DependencyProperty;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		#region -- AdditionalImage - Property -----------------------------------------

		/// <summary>Add an Image</summary>
		public static readonly DependencyProperty AdditionalImageProperty = DependencyProperty.Register(nameof(AdditionalImage), typeof(object), typeof(PpsTextBox), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnAdditionalImageChanged)));

		/// <summary>The property defines an image, showing on right side</summary>
		public object AdditionalImage { get => GetValue(AdditionalImageProperty); set => SetValue(AdditionalImageProperty, value); }

		private static readonly DependencyPropertyKey hasAdditionalImagePropertyKey = DependencyProperty.RegisterReadOnly(nameof(HasAdditionalImage), typeof(bool), typeof(PpsTextBox), new FrameworkPropertyMetadata(BooleanBox.False));

		/// <summary>Has TextBox an additional Image?</summary>
		public static readonly DependencyProperty HasAdditionalImageProperty = hasAdditionalImagePropertyKey.DependencyProperty;

		/// <summary></summary>
		public bool HasAdditionalImage => BooleanBox.GetBool(GetValue(HasAdditionalImageProperty));

		/// <summary></summary>
		protected virtual void OnAdditionalImageChanged(object newValue, object oldValue)
			=> SetValue(hasAdditionalImagePropertyKey, newValue != null);

		private static void OnAdditionalImageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsTextBox)d).OnAdditionalImageChanged(e.NewValue, e.OldValue);

		#endregion

		private IPpsTextBoxInputManager inputManager = null;
		private object textEditor = null;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary></summary>
		public PpsTextBox()
		{
			CommandBindings.Add(new CommandBinding(DropDownCommand,
				(sender, e) => OnDropDown(e),
				(sender, e) => { e.CanExecute = CanExecuteDropDownCommand; e.Handled = true; }
			));

			InputBindings.Add(new KeyBinding(PpsControlCommands.ClearCommand, new KeyGesture(Key.Escape)));

			inputErrorTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
			{
				IsEnabled = false
			};
			inputErrorTimer.Tick += InputTimerCallback;
		} // ctor

		/// <summary></summary>
		/// <param name="e"></param>
		protected override void OnInitialized(EventArgs e)
		{
			base.OnInitialized(e);

			// add "paste" event handler
			DataObject.AddPastingHandler(this, OnPasteTextInput);

			// get text editor
			textEditor = textEditorPropertyInfo.GetValue(this, null)
				?? throw new ArgumentNullException(nameof(textEditor), "TextEditor not located?");
		} // proc OnInitialized

		/// <summary>Hides the ErrorTip if the Textbox is not focused</summary>
		/// <param name="e">unused</param>
		protected override void OnLostFocus(RoutedEventArgs e)
		{
			base.OnLostFocus(e);
			ClearInputError();
		} // proc OnLostFocus

		#endregion

		#region -- IsNullable Property ------------------------------------------------

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public static readonly DependencyProperty IsNullableProperty = DependencyProperty.Register(nameof(IsNullable), typeof(bool), typeof(PpsTextBox), new FrameworkPropertyMetadata(true));
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		void IPpsNullableControl.Clear() => Clear();
		bool IPpsNullableControl.CanClear => IsEnabled && !IsReadOnly && !String.IsNullOrEmpty(Text);

		/// <summary>Is the field nullable.</summary>
		public bool IsNullable { get => BooleanBox.GetBool(GetValue(IsNullableProperty)); set => SetValue(IsNullableProperty, BooleanBox.GetObject(value)); }

		#endregion

		#region -- FormattedValue Property --------------------------------------------

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public static readonly DependencyProperty FormattedValueProperty = DependencyProperty.Register(nameof(FormattedValue), typeof(object), typeof(PpsTextBox), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnFormattedValueChanged)));
		public static readonly DependencyProperty IsFormatableTextProperty = DependencyProperty.Register(nameof(IsFormatableTextProperty), typeof(bool), typeof(PpsTextBox), new FrameworkPropertyMetadata(false));

		private static readonly DependencyPropertyKey hasFormattedValuePropertyKey = DependencyProperty.RegisterReadOnly(nameof(HasFormattedValue), typeof(bool), typeof(PpsTextBox), new FrameworkPropertyMetadata(BooleanBox.False));
		public static readonly DependencyProperty HasFormattedValueProperty = hasFormattedValuePropertyKey.DependencyProperty;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		private static void OnFormattedValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsTextBox)d).OnFormattedValueChanged(e.NewValue, e.OldValue);

		/// <summary></summary>
		/// <param name="newValue"></param>
		/// <param name="oldValue"></param>
		protected virtual void OnFormattedValueChanged(object newValue, object oldValue)
			=> HasFormattedValue = newValue != null;

		/// <summary>Binding Point for formatted value</summary>
		public object FormattedValue { get => GetValue(FormattedValueProperty); set => SetValue(FormattedValueProperty, value); }
		/// <summary>True if FormattedValue is not null</summary>
		public bool HasFormattedValue { get => BooleanBox.GetBool(GetValue(HasFormattedValueProperty)); private set => SetValue(hasFormattedValuePropertyKey, BooleanBox.GetObject(value)); }
		/// <summary>Is the text formatable with bold, italic, ...</summary>
		public bool IsFormatableText { get => BooleanBox.GetBool(GetValue(IsFormatableTextProperty)); set => SetValue(IsFormatableTextProperty, BooleanBox.GetObject(value)); }

		#endregion

		#region -- InputType Properties -----------------------------------------------

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public static readonly DependencyProperty InputTypeProperty = DependencyProperty.Register(nameof(InputType), typeof(PpsTextBoxInputType), typeof(PpsTextBox), new FrameworkPropertyMetadata(PpsTextBoxInputType.None, new PropertyChangedCallback(OnInputTypeChanged)));
		public static readonly DependencyProperty InputMaskProperty = DependencyProperty.Register(nameof(InputMask), typeof(PpsTextBoxInputType), typeof(PpsTextBox), new FrameworkPropertyMetadata(PpsTextBoxInputType.None, new PropertyChangedCallback(OnInputMaskChanged)));
		public static readonly DependencyProperty AllowedLineCountProperty = DependencyProperty.Register(nameof(AllowedLineCount), typeof(int), typeof(PpsTextBox), new FrameworkPropertyMetadata(0, new PropertyChangedCallback(OnAllowedLineCountChanged)));
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		private static void OnInputTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsTextBox)d).OnInputTypeChanged((PpsTextBoxInputType)e.NewValue, (PpsTextBoxInputType)e.OldValue);

		private void SetInputManager(IPpsTextBoxInputManager inputManager)
		{
			this.inputManager = inputManager;
			if (inputManager is IPpsTextBoxDropDownSource dds)
				DropDownSource = dds;
		} // proc SetInputManager

		/// <summary></summary>
		/// <param name="newValue"></param>
		/// <param name="oldValue"></param>
		protected virtual void OnInputTypeChanged(PpsTextBoxInputType newValue, PpsTextBoxInputType oldValue)
		{
			var acceptReturn = false;
			var isFormatable = false;
			var newInputManager = (IPpsTextBoxInputManager)null;

			switch (oldValue)
			{
				case PpsTextBoxInputType.Date:
					DropDownSource = null;
					break;
				case PpsTextBoxInputType.Time:
					DropDownSource = null;
					break;
			}

			switch (newValue)
			{
				case PpsTextBoxInputType.Decimal:
					newInputManager = new PpsRegExInputManager(this, @"^[\d\.]*(\,\d{0,12})?$", "Zahl erwartet.");
					break;
				case PpsTextBoxInputType.DecimalNegative:
					newInputManager = new PpsRegExInputManager(this, @"^\-?[\d\.]*(\,\d{0,12})?$", "Zahl erwartet.");
					break;
				case PpsTextBoxInputType.Integer:
					newInputManager = new PpsRegExInputManager(this, @"^[\d\.]{0,20}$", "Positive Zahl erwartet.");
					break;
				case PpsTextBoxInputType.IntegerNegative:
					newInputManager = new PpsRegExInputManager(this, @"^\-?[\d+\.]{0,20}$", "Zahl erwartet.");
					break;

				case PpsTextBoxInputType.Date:
					newInputManager = new PpsDateInputManager(this);
					break;
				case PpsTextBoxInputType.Time:
					newInputManager = new PpsTimeInputManager(this);
					break;

				case PpsTextBoxInputType.MultiLine:
				case PpsTextBoxInputType.RichMultiLine:
					isFormatable = newValue == PpsTextBoxInputType.RichMultiLine;
					acceptReturn = true;
					if (AllowedLineCount > 0)
						newInputManager = new PpsMultiLineInputManager(this, AllowedLineCount);
					break;

				case PpsTextBoxInputType.Formatted:
					newInputManager = new PpsMaskInputManager(this, String.Empty);
					break;
			}

			// set dependen properties
			AcceptsReturn = acceptReturn;
			IsFormatableText = isFormatable;
			SetInputManager(newInputManager);
		} // proc OnInputTypeChanged

		private static void OnInputMaskChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsTextBox)d).OnInputMaskChanged((string)e.NewValue, (string)e.OldValue);

		/// <summary></summary>
		/// <param name="newValue"></param>
		/// <param name="oldValue"></param>
		protected virtual void OnInputMaskChanged(string newValue, string oldValue)
		{
			InputType = PpsTextBoxInputType.Formatted;
			SetInputManager(new PpsMaskInputManager(this, newValue));
		} // proc OnInputMaskChanged

		private static void OnAllowedLineCountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsTextBox)d).OnAllowedLineCountChanged((int)e.NewValue, (int)e.OldValue);

		/// <summary></summary>
		/// <param name="newValue"></param>
		/// <param name="oldValue"></param>
		protected virtual void OnAllowedLineCountChanged(int newValue, int oldValue)
		{
			InputType = PpsTextBoxInputType.MultiLine;
			if (newValue > 0 && newValue < Int32.MaxValue)
				SetInputManager(new PpsMultiLineInputManager(this, newValue));
		} // proc OnAllowedLineCountChanged

		/// <summary>Defines the predefined input type for the textbox.</summary>
		public PpsTextBoxInputType InputType { get => (PpsTextBoxInputType)GetValue(InputTypeProperty); set => SetValue(InputTypeProperty, value); }
		/// <summary>Defines the input mask for the textbox.</summary>
		public string InputMask { get => (string)GetValue(InputMaskProperty); set => SetValue(InputMaskProperty, value); }
		/// <summary>Sets the allowed Lines for this Textbox</summary>
		public int AllowedLineCount { get => (int)GetValue(AllowedLineCountProperty); set => SetValue(AllowedLineCountProperty, value); }

		#endregion

		#region -- Keyboard events ----------------------------------------------------

		private bool OnPreviewTextChanged(RoutedEventArgs e, string newText)
		{
			if (IsReadOnly)
				return false;
			else if (inputManager != null )
			{
				var r = inputManager.PreviewTextChanged(newText);
				UpdateInputError(r, true);
				e.Handled = !r.IsValid;
				return r.IsValid;
			}
			else
				return true;
		} // func OnPreviewTextChanged

		private void OnPasteTextInput(object sender, DataObjectPastingEventArgs e)
		{
			if (e.DataObject.GetData("Text") is string inputText)
			{
				if (!OnPreviewTextChanged(e, inputText))
					e.CancelCommand();
			}
			else
				e.CancelCommand();
		} // proc OnPasteTextInput

		/// <summary></summary>
		/// <param name="e"></param>
		protected override void OnPreviewTextInput(TextCompositionEventArgs e)
		{
			// filter input, test the new resulting text
			OnPreviewTextChanged(e, e.Text);

			base.OnPreviewTextInput(e);
		} // proc OnPreviewTextInput

		/// <summary></summary>
		/// <param name="e"></param>
		protected override void OnTextChanged(TextChangedEventArgs e)
		{
			base.OnTextChanged(e);

			// update all part information
			if (inputManager != null)
				UpdateInputError(inputManager.TextChanged(), false);
		} // proc OnTextChanged

		/// <summary></summary>
		/// <param name="e"></param>
		protected override void OnPreviewKeyDown(KeyEventArgs e)
		{
			if (!IsReadOnly)
			{
				switch (e.Key)
				{
					case Key.Space: // space is not part of TextInput
						OnPreviewTextChanged(e, " "); // Space
						break;
					case Key.Back:
						OnPreviewTextChanged(e, "\b"); // Backspace
						break;
					case Key.Delete:
						OnPreviewTextChanged(e, "\x7F"); // Delete
						break;
					case Key.F4:
						if (CanExecuteDropDownCommand)
						{
							e.Handled = true;
							DropDownCommand.Execute(null, this);
						}
						break;
					default:
						inputManager?.PreviewKeyDown(e);
						break;
				}
			}

			base.OnPreviewKeyDown(e);
		} // proc OnPreviewKeyDown

		#endregion

		#region -- Input Error Properties ---------------------------------------------

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		private static readonly DependencyPropertyKey inputErrorMessagePropertyKey = DependencyProperty.RegisterReadOnly(nameof(InputErrorMessage), typeof(object), typeof(PpsTextBox), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty InputErrorMessageProperty = inputErrorMessagePropertyKey.DependencyProperty;
		private static readonly DependencyPropertyKey hasInputErrorPropertyKey = DependencyProperty.RegisterReadOnly(nameof(HasInputError), typeof(bool), typeof(PpsTextBox), new FrameworkPropertyMetadata(false));
		public static readonly DependencyProperty HasInputErrorProperty = hasInputErrorPropertyKey.DependencyProperty;

		public static readonly DependencyProperty InputErrorVisibleTimeProperty = DependencyProperty.Register(nameof(InputErrorVisibleTime), typeof(int), typeof(PpsTextBox), new FrameworkPropertyMetadata(5));
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		private readonly DispatcherTimer inputErrorTimer;

		private void ClearInputError()
		{
			inputErrorTimer.Stop();
			InputErrorMessage = null;
			HasInputError = false;
		} // proc ClearError

		private void UpdateInputError(ValidationResult r, bool preview)
			=> UpdateInputError(r.IsValid ? null : r.ErrorContent);

		/// <summary></summary>
		/// <param name="errorMessage"></param>
		public void UpdateInputError(object errorMessage)
		{
			if (!IsKeyboardFocusWithin) // update error only with containing focus
				return;

			if (errorMessage == null)
				ClearInputError();
			else
			{
				// set input error
				InputErrorMessage = errorMessage;
				HasInputError = true;

				// restart timer
				inputErrorTimer.Stop();
				inputErrorTimer.Interval = new TimeSpan(0, 0, InputErrorVisibleTime);
				inputErrorTimer.Start();
			}
		} // proc UpdateInputError

		private void InputTimerCallback(object sender, EventArgs e)
			=> ClearInputError();

		/// <summary>The message presented to the user if the data was invalid</summary>
		public object InputErrorMessage { get => GetValue(InputErrorMessageProperty); private set => SetValue(inputErrorMessagePropertyKey, value); }
		/// <summary>True if there was an invalid entry. Auto-Resets</summary>
		public bool HasInputError { get => BooleanBox.GetBool(GetValue(HasInputErrorProperty)); private set => SetValue(hasInputErrorPropertyKey, BooleanBox.GetObject(value)); }
		/// <summary>The Time in Milliseconds an Information is shown.</summary>
		public int InputErrorVisibleTime { get => (int)GetValue(InputErrorVisibleTimeProperty); set => SetValue(InputErrorVisibleTimeProperty, value); }

		#endregion

		#region -- DropDown source ----------------------------------------------------

		private static void OnDropDownSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsTextBox)d).OnDropDownSourceChanged(e.NewValue, e.OldValue);

		/// <summary></summary>
		/// <param name="newValue"></param>
		/// <param name="oldValue"></param>
		protected virtual void OnDropDownSourceChanged(object newValue, object oldValue)
			=> SetValue(hasDropDownSourcePropertyKey, newValue != null && (newValue is IPpsTextBoxDropDownSource || newValue is IList));

		private void OnDropDown(ExecutedRoutedEventArgs e)
		{
			if (CanExecuteDropDownCommand)
			{
				IPpsTextBoxDropDownSource dropDownSource;
				if (DropDownSource is IPpsTextBoxDropDownSource src)
					dropDownSource = src;
				else if (DropDownSource is IList list)
					dropDownSource = new SimpleTextBoxDropDownSource(this, list);
				else
					return;

				var lists = dropDownSource.GetDropLists();

				var listControl = new PpsMultiCircularListBox
				{
					ListSource = lists
				};

				var selectedItems = dropDownSource.SelectedItems;
				if (selectedItems != null)
					listControl.SelectedItems = selectedItems;
				if (dropDownSource is IPpsTextBoxDropDownSource2 dds2)
					listControl.SelectedItemsChanged += (sender, e2) => dds2.OnSelectedItemsChanged(((PpsMultiCircularListBox)sender).SelectedItems);

				// focus textbox
				Keyboard.Focus(this);

				// create popup
				var popup = new PpsPopup();
				popup.CommandBindings.Add(
					new CommandBinding(ApplicationCommands.Save,
						(sender, e2) =>
						{
							// update text
							dropDownSource.SelectedItems = listControl.SelectedItems;
							// update binding
							GetBindingExpression(TextProperty)?.UpdateSource();
							// close popup
							popup.IsOpen = false;
							e2.Handled = true;
						}
					)
				);
				popup.Child = listControl;

				// show popup
				popup.PlacementTarget = this;
				popup.Placement = PlacementMode.Bottom;
				popup.MinWidth = ActualWidth;
				popup.VerticalOffset = 1;
				popup.StaysOpen = false;
				popup.IsOpen = true;

				e.Handled = true;
			}
		} // proc OnDropDown

		private bool CanExecuteDropDownCommand
			=> HasDropDownSource && IsEnabled && !IsReadOnly;

		/// <summary></summary>
		public bool HasDropDownSource => BooleanBox.GetBool(GetValue(HasDropDownSourceProperty));
		/// <summary></summary>
		public object DropDownSource { get => GetValue(DropDownSourceProperty); set => SetValue(DropDownSourceProperty, value); }

		#endregion
		/// <summary>Get internal overwrite</summary>
		public bool IsOverwriteMode => BooleanBox.GetBool(overwriteModePropertyInfo.GetValue(textEditor, null));

		// -- Static ----------------------------------------------------------

		private static readonly PropertyInfo textEditorPropertyInfo;
		private static readonly PropertyInfo overwriteModePropertyInfo;

		static PpsTextBox()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PpsTextBox), new FrameworkPropertyMetadata(typeof(PpsTextBox)));
			PpsControlCommands.RegisterClearCommand(typeof(PpsTextBox));

			textEditorPropertyInfo = typeof(TextBox).GetProperty("TextEditor", BindingFlags.NonPublic | BindingFlags.Instance)
				?? throw new ArgumentNullException("TextEditor");

			overwriteModePropertyInfo = textEditorPropertyInfo.PropertyType.GetProperty("_OvertypeMode", BindingFlags.NonPublic | BindingFlags.Instance)
				?? throw new ArgumentNullException("_OvertypeMode");
		} // sctor
	} // class PpsTextBox

	#endregion

	#region -- interface IPpsTextBoxInputManager --------------------------------------

	/// <summary>Defines a interface to the input manager</summary>
	public interface IPpsTextBoxInputManager
	{
		/// <summary>Is called before text is inserted.</summary>
		/// <param name="newText"></param>
		/// <returns></returns>
		ValidationResult PreviewTextChanged(string newText);
		/// <summary></summary>
		/// <param name="keyEvent"></param>
		void PreviewKeyDown(KeyEventArgs keyEvent);
		/// <summary>Validate current text.</summary>
		/// <returns></returns>
		ValidationResult TextChanged();
	} // interface IPpsTextBoxInputManager

	#endregion

	#region -- interface IPpsTextBoxDropDownSource ------------------------------------

	/// <summary>Defines a interface for the drop down box of the text box.</summary>
	public interface IPpsTextBoxDropDownSource
	{
		/// <summary>Return tdrop down list.</summary>
		/// <returns></returns>
		object[] GetDropLists();

		/// <summary></summary>
		/// <returns></returns>
		object[] SelectedItems { get; set; }
	} // interface IPpsTextBoxDropDownSource

	/// <summary></summary>
	public interface IPpsTextBoxDropDownSource2 : IPpsTextBoxDropDownSource
	{
		/// <summary></summary>
		/// <param name="selectedItems"></param>
		void OnSelectedItemsChanged(object[] selectedItems);
	} // interface IPpsTextBoxDropDownSource2

	internal sealed class SimpleTextBoxDropDownSource : IPpsTextBoxDropDownSource
	{
		private readonly PpsTextBox textBox;
		private readonly IList baseList;

		public SimpleTextBoxDropDownSource(PpsTextBox textBox, IList baseList)
		{
			this.textBox = textBox;
			this.baseList = baseList;
		} // ctor

		public object[] GetDropLists()
			=> new object[] { baseList };

		public object[] SelectedItems
		{
			get => new object[] { textBox.Text };
			set
			{
				textBox.BeginChange();
				try
				{
					textBox.SelectAll();
					if (value == null)
						textBox.SelectedText = String.Empty;
					else
						textBox.SelectedText = value[0].ToString();
				}
				finally
				{
					textBox.EndChange();
				}
			}
		} // prop SelectedItems
	} // class SimpleTextBoxDropDownSource

	#endregion

	#region -- class PpsTextBoxInputManager -------------------------------------------

	/// <summary>Base class for the input manager</summary>
	public abstract class PpsTextBoxInputManager : IPpsTextBoxInputManager
	{
		private readonly PpsTextBox textBox;

		/// <summary></summary>
		/// <param name="textBox"></param>
		public PpsTextBoxInputManager(PpsTextBox textBox)
			=> this.textBox = textBox;

		#region -- UpdateText, TryInsertText ------------------------------------------

		/// <summary>Get text from text box.</summary>
		/// <returns></returns>
		protected string GetText()
			=> textBox.Text;

		/// <summary>Replace complete text.</summary>
		/// <param name="newText"></param>
		protected void ClearText(string newText)
		{
			textBox.SelectionStart = 0;
			textBox.SelectionLength = textBox.Text.Length;
			textBox.SelectedText = newText;
		} // proc ClearText

		/// <summary>Update a part of the textbox. Simulate normal TextInput, no binding events should</summary>
		/// <param name="newPartText"></param>
		/// <param name="startAt"></param>
		/// <param name="length"></param>
		protected void UpdateText(string newPartText, int startAt, int length)
		{
			var oldSelectionStart = textBox.SelectionStart;
			var oldSelectionLength = textBox.SelectionLength;
			try
			{
				if (newPartText.Length != length) // we need to move selection
				{
					if (oldSelectionStart > startAt + length) // current selection is after the replaced block
					{
						oldSelectionStart += length - newPartText.Length;
					}
					else if (oldSelectionStart > startAt) // current selection is within the replaced block
					{
						var oldSelectionEnd = oldSelectionStart + oldSelectionLength;
						if (oldSelectionStart - startAt > newPartText.Length) // move to the end of the replaced block
							oldSelectionStart = startAt + newPartText.Length;

						// todo: test cases?
						if (oldSelectionEnd > startAt + length) // let selection end behind the block
						{
							oldSelectionEnd = startAt + 2 * length - newPartText.Length;
						}
						else
						{
							oldSelectionEnd = startAt + newPartText.Length;
						}

						oldSelectionLength = oldSelectionEnd - oldSelectionStart;
					}
				}

				// update text
				textBox.SelectionStart = startAt;
				textBox.SelectionLength = length;
				textBox.SelectedText = newPartText;
			}
			finally
			{
				textBox.SelectionStart = oldSelectionStart;
				textBox.SelectionLength = oldSelectionLength;
			}
		} // proc UpdateText

		/// <summary>Create a result text from the input.</summary>
		/// <param name="inputText"></param>
		/// <param name="newText"></param>
		/// <returns></returns>
		protected ValidationResult TryInsertText(string inputText, out string newText)
		{
			var selectionStart = textBox.SelectionStart;
			var selectionLength = textBox.SelectionLength;
			newText = textBox.Text;

			// remove char, on backspace or delete
			if (inputText.Length == 1)
			{
				switch (inputText[0])
				{
					case '\b':// back space
						if (selectionLength > 0)
						{
							newText = newText.Remove(selectionStart, selectionLength);
							return ValidResult;
						}
						else if (selectionStart > 0)
						{
							newText = newText.Remove(selectionStart - 1, 1);
							return ValidResult;
						}
						else
							return InvalidResult;
					case '\x7F':
						if (selectionLength > 0)
						{
							newText = newText.Remove(selectionStart, selectionLength);
							return ValidResult;
						}
						else if (selectionStart + 1 <= newText.Length)
						{
							newText = newText.Remove(selectionStart, 1);
							return ValidResult;
						}
						else
							return InvalidResult;
				}
			}

			// on overwrite mode, replace next char
			if (selectionLength == 0 // only if nothing is selected
				&& selectionStart < newText.Length // and on end
				&& textBox.IsOverwriteMode)
			{
				selectionLength = inputText.Length;
			}

			// remove marked text
			if (selectionLength > 0)
			{
				// ensure length
				if (newText.Length < selectionStart + selectionLength)
					selectionLength = newText.Length - selectionStart;

				if (selectionStart >= 0)
					newText = newText.Remove(selectionStart, selectionLength);
				else
					return new ValidationResult(false, "Could not insert text.");
			}

			// insert the new text
			newText = newText.Insert(selectionStart, inputText);
			return ValidResult;
		} // func TryInsertText

		#endregion

		ValidationResult IPpsTextBoxInputManager.PreviewTextChanged(string newText)
			=> OnPreviewTextChanged(newText);

		/// <summary></summary>
		/// <param name="newText"></param>
		/// <returns></returns>
		protected abstract ValidationResult OnPreviewTextChanged(string newText);

		void IPpsTextBoxInputManager.PreviewKeyDown(KeyEventArgs keyEvent)
			=> OnPreviewKeyDown(keyEvent);

		/// <summary></summary>
		/// <param name="keyEvent"></param>
		protected virtual void OnPreviewKeyDown(KeyEventArgs keyEvent)
			=> GenerateLockKeyErrors();

		/// <summary>Num/Caps-Lock checks</summary>
		protected void GenerateLockKeyErrors()
		{
			if (!Keyboard.IsKeyToggled(Key.NumLock))
				textBox.UpdateInputError("Num-Taste ist nicht aktiviert.");
			else if (Keyboard.IsKeyToggled(Key.CapsLock))
				textBox.UpdateInputError("Feststelltaste ist aktiviert.");
		} // proc GenerateLockKeyErrors

		ValidationResult IPpsTextBoxInputManager.TextChanged()
			=> OnTextChanged();

		/// <summary></summary>
		/// <returns></returns>
		protected abstract ValidationResult OnTextChanged();

		/// <summary>Access textbox.</summary>
		protected PpsTextBox TextBox => textBox;

		/// <summary></summary>
		public static ValidationResult ValidResult { get; } = new ValidationResult(true, null);
		/// <summary></summary>
		public static ValidationResult InvalidResult { get; } = new ValidationResult(false, null);
	} // class PpsTextBoxInputManager

	#endregion

	#region -- class PpsMultiLineInputManager -----------------------------------------

	/// <summary>Regex based input manager.</summary>
	public sealed class PpsMultiLineInputManager : PpsTextBoxInputManager
	{
		/// <summary>Initalize regex input manager.</summary>
		/// <param name="textBox"></param>
		/// <param name="maxLines"></param>
		public PpsMultiLineInputManager(PpsTextBox textBox, int maxLines)
			: base(textBox)
		{
			if (maxLines <= 0)
				throw new ArgumentOutOfRangeException(nameof(maxLines), maxLines, "Only values greate zero allowed.");
		} // ctor

		/// <summary>Handle preview text change event.</summary>
		/// <param name="newText"></param>
		/// <returns></returns>
		protected override ValidationResult OnPreviewTextChanged(string newText)
		{
			// todo: Count Lines
			return ValidResult;
		} // proc OnPreviewTextChanged

		/// <summary>Handle text change event.</summary>
		/// <returns></returns>
		protected override ValidationResult OnTextChanged()
			=> ValidResult;

		/// <summary></summary>
		/// <param name="keyEvent"></param>
		protected override void OnPreviewKeyDown(KeyEventArgs keyEvent) { }
	} // class PpsMultiLineInputManager

	#endregion

	#region -- class PpsRegExInputManager ---------------------------------------------

	/// <summary>Regex based input manager.</summary>
	public sealed class PpsRegExInputManager : PpsTextBoxInputManager
	{
		private readonly Regex regex;
		private readonly object errorMessage;

		/// <summary>Initalize regex input manager.</summary>
		/// <param name="textBox"></param>
		/// <param name="regex"></param>
		/// <param name="errorMessage"></param>
		public PpsRegExInputManager(PpsTextBox textBox, string regex, object errorMessage)
			: base(textBox)
		{
			this.regex = new Regex(regex, RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);
			this.errorMessage = errorMessage;
		} // ctor

		private ValidationResult TryParse(string text, bool preview)
		{
			if (String.IsNullOrEmpty(text))
				return ValidResult;

			var m = regex.Match(text);
			return m.Success
				? ValidResult
				: new ValidationResult(false, errorMessage);
		} // func TryParse

		/// <summary>Handle preview text change event.</summary>
		/// <param name="newText"></param>
		/// <returns></returns>
		protected override ValidationResult OnPreviewTextChanged(string newText)
		{
			var r = TryInsertText(newText, out var text);
			return r.IsValid ? TryParse(text, true) : r;
		} // func OnPreviewTextChanged

		/// <summary>Handle text change event.</summary>
		/// <returns></returns>
		protected override ValidationResult OnTextChanged()
			=> TryParse(GetText(), false);
	} // class PpsRegExInputManager

	#endregion

	#region -- class PpsMaskInputManagerBase ------------------------------------------

	/// <summary>Base implementation for the mask input manager.</summary>
	public abstract class PpsMaskInputManagerBase : PpsTextBoxInputManager
	{
		#region -- class PartBase -----------------------------------------------------

		/// <summary>Mask part definition</summary>
		protected abstract class PartBase
		{
			/// <summary>Part</summary>
			/// <param name="startAt"></param>
			/// <param name="endAt"></param>
			public PartBase(int startAt, int endAt)
			{
				this.StartAt = startAt;
				this.EndAt = endAt;
			} // ctor

			/// <summary></summary>
			/// <param name="offset"></param>
			/// <param name="text"></param>
			/// <returns></returns>
			public abstract bool Reset(int offset, StringBuilder text);

			/// <summary></summary>
			/// <param name="offset"></param>
			/// <param name="c"></param>
			/// <param name="text"></param>
			/// <returns></returns>
			public abstract (bool nextChar, ValidationResult result) Insert(int offset, char c, StringBuilder text);

			/// <summary></summary>
			/// <param name="text"></param>
			/// <returns></returns>
			public string GetTextPart(string text)
				=> EndAt < text.Length
					? text.Substring(StartAt, EndAt - StartAt + 1)
					: null;

			/// <summary>Part starts at.</summary>
			public int StartAt { get; }
			/// <summary>Part ends et.</summary>
			public int EndAt { get; }
		} // class PartBase

		#endregion

		#region -- class StaticPart ---------------------------------------------------

		private sealed class StaticPart : PartBase
		{
			private readonly char staticChar;

			public StaticPart(int startAt, int endAt, char c)
				: base(startAt, endAt)
				=> this.staticChar = c;

			public override bool Reset(int offset, StringBuilder text)
			{
				text[offset] = staticChar;
				return true;
			}

			public override (bool nextChar, ValidationResult result) Insert(int offset, char c, StringBuilder text)
			{
				if (offset < text.Length)
					text[offset] = staticChar;
				else
					text.Append(staticChar);
				return (
					c == this.staticChar, // is the same as static
					ValidResult
				);
			}
		} // class StaticPart

		#endregion

		#region -- class NumberPart ---------------------------------------------------

		private sealed class NumberPart : PartBase
		{
			public NumberPart(int startAt, int endAt)
				: base(startAt, endAt)
			{
			} // ctor

			public override bool Reset(int offset, StringBuilder text)
			{
				text[offset] = '0';
				return true;
			}

			public override (bool nextChar, ValidationResult result) Insert(int offset, char c, StringBuilder text)
			{
				if (!Char.IsDigit(c))
					return (false, new ValidationResult(false, "Zahl erwartet."));
				else
				{
					if (offset < text.Length)
						text[offset] = c;
					else
						text.Append(c);
					return (true, ValidResult);
				}
			} // func Insert
		} // class NumberPart

		#endregion

		#region -- class WordPart -----------------------------------------------------

		private sealed class WordPart : PartBase
		{
			private readonly bool upperCase;
			private readonly bool allowDigits;
			private readonly bool allowAny;

			private readonly object errorMesage;

			public WordPart(int startAt, int endAt, bool upperCase, bool allowDigits, bool allowAny, object errorMessage)
				: base(startAt, endAt)
			{
				this.upperCase = upperCase;
				this.allowDigits = allowDigits;
				this.allowAny = allowAny;

				this.errorMesage = errorMessage;
			} // ctor

			public override bool Reset(int offset, StringBuilder text)
			{
				text[offset] = ' ';
				return true;
			} // func Reset

			public override (bool nextChar, ValidationResult result) Insert(int offset, char c, StringBuilder text)
			{
				if (Char.IsLetter(c))
				{
					if (upperCase)
					{
						c = Char.ToUpper(c);
						text[offset] = c;
					}
					return (true, ValidResult);
				}
				else if (allowDigits && Char.IsDigit(c))
					return (true, ValidResult);
				else if (allowAny && !Char.IsWhiteSpace(c))
					return (true, ValidResult);
				else
					return (false, new ValidationResult(false, errorMesage));
			} // func Insert
		} // class WordPart

		#endregion

		private readonly PartBase[] mask;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary></summary>
		/// <param name="textBox"></param>
		/// <param name="mask"></param>
		protected PpsMaskInputManagerBase(PpsTextBox textBox, params PartBase[] mask)
			: base(textBox)
		{
			this.mask = mask;
		} // ctor

		/// <summary></summary>
		/// <param name="startAt"></param>
		/// <param name="endAt"></param>
		/// <param name="c"></param>
		/// <returns></returns>
		protected static PartBase CreateStatic(int startAt, int endAt, char c)
			=> new StaticPart(startAt, endAt, c);

		/// <summary></summary>
		/// <param name="startAt"></param>
		/// <param name="endAt"></param>
		/// <returns></returns>
		protected static PartBase CreateNumber(int startAt, int endAt)
			=> new NumberPart(startAt, endAt);

		/// <summary></summary>
		/// <param name="startAt"></param>
		/// <param name="endAt"></param>
		/// <param name="upperCase"></param>
		/// <param name="allowDigits"></param>
		/// <param name="allowAny"></param>
		/// <param name="errorMesage"></param>
		/// <returns></returns>
		protected static PartBase CreateWord(int startAt, int endAt, bool upperCase, bool allowDigits, bool allowAny, object errorMesage)
			=> new WordPart(startAt, endAt, upperCase, allowDigits, allowAny, errorMesage);

		#endregion

		#region -- GetPartFromOffset --------------------------------------------------

		private PartBase GetPartFromOffset(int offset)
		{
			foreach (var m in mask)
			{
				if (offset >= m.StartAt && offset <= m.EndAt)
					return m;
			}
			return null;
		} // func GetPartFromOffset

		#endregion

		#region -- OnPreviewTextChanged, OnTextChanged --------------------------------

		/// <summary></summary>
		/// <param name="newText"></param>
		/// <returns></returns>
		protected override ValidationResult OnPreviewTextChanged(string newText)
		{
			int i;

			var selectionLength = TextBox.SelectionLength;
			var selectionStart = TextBox.SelectionStart;
			var text = new StringBuilder(GetText());

			// clear all chars to default
			if (selectionLength > 0)
			{
				var endAt = selectionStart + selectionLength;
				if (endAt == text.Length)
					text.Remove(selectionStart, selectionLength);
				else
				{
					for (i = selectionStart; i < endAt; i++)
					{
						var p = GetPartFromOffset(i);
						if (p == null) // over eof
						{
							text.Remove(i, text.Length - i);
							break;
						}
						else
							p.Reset(i, text);
					}
				}

				selectionLength = 0;

				if (newText.Length == 1 && (newText[0] == '\b' || newText[0] == '\x7F'))
					goto UpdateFullText;
			}
			else if (newText.Length == 1)
			{
				switch (newText[0])
				{
					case '\b':
						if (selectionStart == 0)
							return new ValidationResult(false, null);

						if (selectionStart == text.Length)
						{
							text.Remove(selectionStart - 1, 1);
							selectionStart--;
						}
						else
						{
							selectionStart--;
							GetPartFromOffset(selectionStart)?.Reset(selectionStart, text);
						}
						goto UpdateFullText;
					case '\x7F':
						if (selectionStart == 0 && text.Length == 0)
							goto UpdateNothing;
						else if (selectionStart >= text.Length - 1)
						{
							text.Remove(text.Length - 1, 1);
							selectionStart = text.Length;
						}
						else
							GetPartFromOffset(selectionStart)?.Reset(selectionStart, text);
						goto UpdateFullText;
				}
			}

			// simulate insert of new chars
			i = 0;
			while (i < newText.Length)
			{
				var p = GetPartFromOffset(selectionStart);
				if (p != null)
				{
					var (nextChar, result) = p.Insert(selectionStart, newText[i], text);
					if (!result.IsValid)
						return result;
					selectionStart++;
					if (nextChar)
						i++;
				}
				else
					i++;
			}

			// replace full text
			UpdateFullText:
			TextBox.BeginChange();
			try
			{
				ClearText(text.ToString());
				TextBox.SelectionStart = selectionStart;
				TextBox.SelectionLength = 0;
			}
			finally
			{
				TextBox.EndChange();
			}
			UpdateNothing:
			// always cancel input, write our result
			return new ValidationResult(false, null);
		} // func OnPreviewTextChanged

		/// <summary></summary>
		/// <returns></returns>
		protected override ValidationResult OnTextChanged()
			=> ValidResult;

		#endregion

		/// <summary></summary>
		protected PartBase[] Mask => mask;
	} // class PpsMaskInputManagerBase

	#endregion

	#region -- class PpsMaskInputManager ----------------------------------------------

	/// <summary>Implementation for the mask input manager.</summary>
	public sealed class PpsMaskInputManager : PpsMaskInputManagerBase
	{
		/// <summary></summary>
		/// <param name="textBox"></param>
		/// <param name="mask"></param>
		public PpsMaskInputManager(PpsTextBox textBox, string mask)
			: base(textBox, ParseMask(mask))
		{
		}

		private static PartBase[] ParseMask(string mask)
			=> throw new NotImplementedException();
	} // class PpsMaskInputManager

	#endregion

	#region -- class PpsDateInputManager ----------------------------------------------

	internal sealed class PpsDateInputManager : PpsMaskInputManagerBase, IPpsTextBoxDropDownSource2
	{
		private readonly ObservableCollection<string> dayList;

		public PpsDateInputManager(PpsTextBox textBox)
			: base(textBox,
				  CreateNumber(0, 1),
				  CreateStatic(2, 2, '.'),
				  CreateNumber(3, 4),
				  CreateStatic(5, 5, '.'),
				  CreateNumber(6, 9)
			)
		{
			dayList = new ObservableCollection<string>(dayInput);
		} // ctor

		public void OnSelectedItemsChanged(object[] selectedItems)
		{
			var month = Int32.Parse((string)selectedItems[1]);
			var year = Int32.Parse((string)selectedItems[2]);
			var days =
				year >= 1 && year <= 9999
				&& month >= 1 && month <= 12 ? DateTime.DaysInMonth(year, month) : 31;

			while (days < dayList.Count)
				dayList.RemoveAt(dayList.Count - 1);
			while (days > dayList.Count)
				dayList.Add(dayInput[dayList.Count]);
		} // proc OnSelectedItems

		public object[] GetDropLists()
		{
			OnSelectedItemsChanged(SelectedItems);
			return new object[]
			{
				dayList,
				monthInput,
				yearInput
			};
		} // func GetDropLists

		public object[] SelectedItems
		{
			get
			{
				var d = DateTime.Today.ToString("dd.MM.yyyy");
				var t = GetText();
				if (String.IsNullOrEmpty(t))
					t = d;

				return new string[]
				{
					Mask[0].GetTextPart(t) ?? Mask[0].GetTextPart(d),
					Mask[2].GetTextPart(t) ?? Mask[2].GetTextPart(d),
					Mask[4].GetTextPart(t) ?? Mask[4].GetTextPart(d)
				};
			}
			set
			{
				TextBox.BeginChange();
				try
				{
					if (value != null)
					{
						ClearText(String.Format("{0}.{1}.{2}", value[0], value[1], value[2]));
						TextBox.SelectAll();
					}
					else
						ClearText(String.Empty);
				}
				finally
				{
					TextBox.EndChange();
				}
			}
		} // prop SelectedItems

		private static readonly string[] yearInput;
		private static readonly string[] monthInput;
		private static readonly string[] dayInput;

		static PpsDateInputManager()
		{
			yearInput = new string[41];
			for (var i = 0; i < 41; i++)
				yearInput[i] = (1990 + i).ToString("0000");
			monthInput = new string[12];
			for (var i = 0; i < 12; i++)
				monthInput[i] = (i + 1).ToString("00");
			dayInput = new string[31];
			for (var i = 0; i < 31; i++)
				dayInput[i] = (i + 1).ToString("00");
		}
	} // class PpsDateInputManager

	#endregion

	#region -- class PpsTimeInputManager ----------------------------------------------

	internal sealed class PpsTimeInputManager : PpsMaskInputManagerBase, IPpsTextBoxDropDownSource
	{
		public PpsTimeInputManager(PpsTextBox textBox)
			: base(textBox,
				  CreateNumber(0, 1),
				  CreateStatic(2, 2, ':'),
				  CreateNumber(3, 4)
			)
		{
		} // ctor

		public object[] GetDropLists()
			=> new object[]
			{
				hourArray,
				minutesArray
			};

		public object[] SelectedItems
		{
			get
			{
				var d = DateTime.Now.ToString("HH:mm");
				var t = GetText();
				if (String.IsNullOrEmpty(t))
					t = d;

				return new string[]
				{
					Mask[0].GetTextPart(t) ?? Mask[0].GetTextPart(d),
					Mask[2].GetTextPart(t) ?? Mask[2].GetTextPart(d)
				};
			}
			set
			{
				TextBox.BeginChange();
				try
				{
					if (value != null)
					{
						ClearText(String.Format("{0}:{1}", value[0], value[1]));
						TextBox.SelectAll();
					}
					else
						ClearText(String.Empty);
				}
				finally
				{
					TextBox.EndChange();
				}
			}
		} // prop SelectedItems

		private static readonly string[] hourArray;
		private static readonly string[] minutesArray;

		static PpsTimeInputManager()
		{
			hourArray = new string[24];
			for (var i = 0; i < 24; i++)
				hourArray[i] = i.ToString("00");

			minutesArray = new string[60];
			for (var i = 0; i < 60; i++)
				minutesArray[i] = i.ToString("00");
		} // sctor
	} // class PpsTimeInputManager

	#endregion
}
