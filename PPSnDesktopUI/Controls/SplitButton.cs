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
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace TecWare.PPSn.Controls
{
	#region -- enum SplitButtonMode -----------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public enum SplitButtonType
	{
		Dropdown,
		SplitButton
	} // enum SplitButtonType

	#endregion

	#region -- class SplitButton --------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class SplitButton : Button
	{
		public static readonly DependencyProperty ModeProperty = DependencyProperty.Register(nameof(Mode), typeof(SplitButtonType), typeof(SplitButton), new FrameworkPropertyMetadata(SplitButtonType.SplitButton));
		public static readonly DependencyProperty PopupProperty = DependencyProperty.Register(nameof(Popup), typeof(Popup), typeof(SplitButton), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(SplitButton), new FrameworkPropertyMetadata(false, IsOpenChangedCallback));

		public const string PartSplitButton = "PART_Dropdown";

		private readonly EventHandler popupClosed;
		private readonly RoutedEventHandler popupClosed2;

		public SplitButton()
		{
			popupClosed = (sender, e) => IsOpen = false;
			popupClosed2 = (sender, e) => IsOpen = false;
		} // ctor

		public override void OnApplyTemplate()
		{
			base.OnApplyTemplate();

			var button = this.Template.FindName(PartSplitButton, this) as ButtonBase;
			if (button != null)
				button.Click += (sender, e) => OnDropdown();
		} // proc OnApplyTemplate

		protected override void OnClick()
		{
			if (Mode == SplitButtonType.Dropdown)
				OnDropdown();
			else
				base.OnClick();
		} // proc OnClick

		protected virtual void OnDropdown()
		{
			if (IsOpen)
				IsOpen = false;
			else if (Popup != null)
			{
				Popup.PlacementTarget = this;
				Popup.Placement = PlacementMode.Bottom;
				Popup.VerticalOffset = 2;
				Popup.StaysOpen = false;
				IsOpen = true;
			}
			else if (ContextMenu != null)
			{
				ContextMenu.Placement = PlacementMode.Bottom;
				ContextMenu.PlacementTarget = this;
				ContextMenu.StaysOpen = false;
				IsOpen = true;
			}
		} // porc OnDropdown

		/// <summary></summary>
		public bool IsOpen { get { return (bool)GetValue(IsOpenProperty); } set { SetValue(IsOpenProperty, value); } }
		/// <summary></summary>
		public Popup Popup { get { return (Popup)GetValue(PopupProperty); } set { SetValue(PopupProperty, value); } }
		/// <summary></summary>
		public SplitButtonType Mode { get { return (SplitButtonType)GetValue(ModeProperty); } set { SetValue(ModeProperty, value); } }

		// -- Static --------------------------------------------------------------

		//static SplitButton()
		//{
		//	DefaultStyleKeyProperty.OverrideMetadata(typeof(SplitButton), new FrameworkPropertyMetadata(typeof(SplitButton)));
		//} // ctor

		private static void IsOpenChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var s = (SplitButton)d;

			if (s.Popup != null)
			{
				s.Popup.Closed += s.popupClosed;
				s.Popup.SetValue(Popup.IsOpenProperty, e.NewValue);
			}
			else if (s.ContextMenu != null)
			{
				s.ContextMenu.Closed += s.popupClosed2;
				s.ContextMenu.SetValue(ContextMenu.IsOpenProperty, e.NewValue);
			}
		} // proc IsOpenChangedCallback
	} // class SplitButton

	#endregion
}
