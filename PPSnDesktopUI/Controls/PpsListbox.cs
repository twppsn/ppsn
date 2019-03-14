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
using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;


namespace TecWare.PPSn.Controls
{
	#region -- enum PpsListBoxShowSelectionType ---------------------------------------

	/// <summary></summary>
	public enum PpsListBoxShowSelectionType
	{
		/// <summary>Always hilite selected items.</summary>
		Always,
		/// <summary>Hilite selected item when focused.</summary>
		WhenFocused,
		/// <summary>Never show selection.</summary>
		Never
	} // enum PpsListBoxShowSelectionType

	#endregion

	#region -- class PpsListBox -------------------------------------------------------

	/// <summary></summary>
	public class PpsListBox : ListBox
	{
		#region -- ShowSelectionMode - Property ---------------------------------------

		/// <summary>The type of showing selected items</summary>
		public static readonly DependencyProperty ShowSelectionModeProperty = DependencyProperty.Register(nameof(ShowSelectionMode), typeof(PpsListBoxShowSelectionType), typeof(PpsButton), new FrameworkPropertyMetadata(PpsListBoxShowSelectionType.Always));

		/// <summary>The property defines the type of hiliting selected items</summary>
		public PpsListBoxShowSelectionType ShowSelectionMode { get => (PpsListBoxShowSelectionType)GetValue(ShowSelectionModeProperty); set => SetValue(ShowSelectionModeProperty, value); }

		#endregion

		static PpsListBox()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PpsListBox), new FrameworkPropertyMetadata(typeof(PpsListBox)));
		} // sctor
	} // class PpsListBox

	#endregion
}
