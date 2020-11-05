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
	/// <summary>Readonly flag for the mask and control</summary>
	public interface IPpsReadOnlyControl
	{
		/// <summary></summary>
		bool IsReadOnly { get; set; }
	} // interface IPpsReadOnlyControl

	#region -- class PpsReadOnlyPaneBehavior ------------------------------------------

	/// <summary></summary>
	public static class PpsReadOnlyPaneBehavior
	{
		//From MSDN:
		//Note, however, that designating a property as inheritable does have some performance considerations.
		//In cases where that property does not have an established local value, or a value obtained through styles, templates, or data binding,
		//an inheritable property provides its assigned property values to all child elements in the logical tree
		public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.RegisterAttached("IsReadOnly", typeof(bool), typeof(PpsReadOnlyPaneBehavior), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits, ReadOnlyPropertyChanged));

		// Ingesamt schlechte Idee, da der eigentliche Kontrolstatus nicht beachtet wird?

		public static bool GetIsReadOnly(DependencyObject d)
			=> (bool)d.GetValue(IsReadOnlyProperty);

		public static void SetIsReadOnly(DependencyObject d, bool value)
			=> d.SetValue(IsReadOnlyProperty, value);
		
		private static void ReadOnlyPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			if (d is IPpsReadOnlyControl ro)
				ro.IsReadOnly = BooleanBox.GetBool(e.NewValue);
			else if (d is TextBox tb)
				tb.IsReadOnly = BooleanBox.GetBool(e.NewValue);
			else if (d is PpsDataFilterCombo ds)
				ds.IsReadOnly = BooleanBox.GetBool(e.NewValue);
			else if (d is PpsComboBox cb)
				cb.IsReadOnly = BooleanBox.GetBool(e.NewValue);
			else if (d is CheckBox c)
				c.IsEnabled = !BooleanBox.GetBool(e.NewValue);
		} // proc ReadOnlyPropertyChanged
	} // class PpsReadOnlyPaneBehavior

	#endregion
}
