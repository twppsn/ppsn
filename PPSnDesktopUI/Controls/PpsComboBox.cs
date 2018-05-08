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

namespace TecWare.PPSn.Controls
{
	/// <summary></summary>
	public class PpsComboBox : ComboBox, IPpsNullableControl
	{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public static readonly DependencyProperty IsNullableProperty = PpsTextBox.IsNullableProperty.AddOwner(typeof(PpsComboBox));
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		/// <summary></summary>
		public void Clear()
		{
			SelectedIndex = -1;
		} // proc ClearValue

		bool IPpsNullableControl.CanClear => IsEnabled && IsNullable && SelectedIndex >= 0;

		/// <summary>Is this value nullable.</summary>
		public bool IsNullable { get => BooleanBox.GetBool(GetValue(IsNullableProperty)); set => SetValue(IsNullableProperty, BooleanBox.GetObject(value)); }

		static PpsComboBox()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PpsComboBox), new FrameworkPropertyMetadata(typeof(PpsComboBox)));
			PpsControlCommands.RegisterClearCommand(typeof(PpsComboBox));
		}
	} // class PpsComboBox
}
