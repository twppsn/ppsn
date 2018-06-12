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

namespace TecWare.PPSn.Controls
{
	#region -- class PpsComboBox ------------------------------------------------------

	/// <summary></summary>
	public class PpsComboBox : ComboBox, IPpsNullableControl
	{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public static readonly DependencyProperty IsNullableProperty = PpsTextBox.IsNullableProperty.AddOwner(typeof(PpsComboBox));
		public static readonly DependencyProperty HintLabelTextProperty = DependencyProperty.Register(nameof(HintLabelText), typeof(string), typeof(PpsComboBox), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnHintLabelTextChanged)));
		private static readonly DependencyPropertyKey hasHintLabelPropertyKey = DependencyProperty.RegisterReadOnly(nameof(HasHintLabel), typeof(bool), typeof(PpsComboBox), new FrameworkPropertyMetadata(BooleanBox.False));
		public static readonly DependencyProperty HasHintLabelProperty = hasHintLabelPropertyKey.DependencyProperty;
		public static readonly DependencyProperty IsClearButtonVisibleProperty = DependencyProperty.Register(nameof(IsClearButtonVisible), typeof(bool), typeof(PpsComboBox), new FrameworkPropertyMetadata(BooleanBox.False));
		public static readonly DependencyProperty GeometryNameProperty = PpsGeometryImage.GeometryNameProperty.AddOwner(typeof(PpsComboBox));
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		/// <summary></summary>
		public void Clear()
		{
			SelectedIndex = -1;
		} // proc ClearValue

		bool IPpsNullableControl.CanClear => IsEnabled && IsNullable && SelectedIndex >= 0;

		private static void OnHintLabelTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var comboBox = (PpsComboBox)d;
			if (e.NewValue is string label && !string.IsNullOrEmpty(label))
				comboBox.HasHintLabel = true;
			else
				comboBox.HasHintLabel = false;
		} // proc OnHintLabelTextChanged

		/// <summary>Is this value nullable.</summary>
		public bool IsNullable { get => BooleanBox.GetBool(GetValue(IsNullableProperty)); set => SetValue(IsNullableProperty, BooleanBox.GetObject(value)); }

		/// <summary>Is the ClearButton visible?</summary>
		public bool IsClearButtonVisible { get => BooleanBox.GetBool(GetValue(IsClearButtonVisibleProperty)); set => SetValue(IsClearButtonVisibleProperty, BooleanBox.GetObject(value)); }

		/// <summary>Optional text to show, when no value is selected</summary>
		public string HintLabelText { get => (string)(GetValue(HintLabelTextProperty)); set => SetValue(HintLabelTextProperty, value); }

		/// <summary>Has the Combobox a HintLabel?</summary>
		public bool HasHintLabel { get => BooleanBox.GetBool(GetValue(HasHintLabelProperty)); private set => SetValue(hasHintLabelPropertyKey, BooleanBox.GetObject(value)); }

		/// <summary>The property defines the resource to be loaded to show the dropdown-symbol.</summary>
		public string GeometryName { get => (string)GetValue(GeometryNameProperty); set => SetValue(GeometryNameProperty, value); }

		static PpsComboBox()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PpsComboBox), new FrameworkPropertyMetadata(typeof(PpsComboBox)));
			PpsControlCommands.RegisterClearCommand(typeof(PpsComboBox));
		}
	} // class PpsComboBox

	#endregion

	#region -- class PpsComboBoxToggleButton ------------------------------------------

	/// <summary></summary>
	public class PpsComboBoxToggleButton : ToggleButton
	{
		/// <summary>The name of the resource</summary>
		public static readonly DependencyProperty GeometryNameProperty = PpsGeometryImage.GeometryNameProperty.AddOwner(typeof(PpsComboBoxToggleButton));

		/// <summary>The property defines the resource to be loaded to show the dropdown-symbol.</summary>
		public string GeometryName { get => (string)GetValue(GeometryNameProperty); set => SetValue(GeometryNameProperty, value); }

		static PpsComboBoxToggleButton()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PpsComboBoxToggleButton), new FrameworkPropertyMetadata(typeof(PpsComboBoxToggleButton)));
		}
	} // class PpsComboBoxToggleButton

	#endregion


}
