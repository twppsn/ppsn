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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace TecWare.PPSn.Controls
{
	#region -- enum PpsSplitButtonMode ------------------------------------------------

	/// <summary></summary>
	public enum PpsSplitButtonType
	{
		/// <summary>Only a drop down button.</summary>
		Dropdown,
		/// <summary>Drop and command button.</summary>
		SplitButton
	} // enum SplitButtonType

	#endregion

	#region -- class PpsSplitButton ---------------------------------------------------

	/// <summary></summary>
	[TemplatePart(Name = "PART_DropDdown", Type = typeof(ButtonBase))]
	public class PpsSplitButton : Button
	{
		private const string PartSplitButton = "PART_Dropdown";

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public static readonly DependencyProperty ModeProperty = DependencyProperty.Register(nameof(Mode), typeof(PpsSplitButtonType), typeof(PpsSplitButton), new FrameworkPropertyMetadata(PpsSplitButtonType.SplitButton));
		public static readonly DependencyProperty PopupProperty = DependencyProperty.Register(nameof(Popup), typeof(Popup), typeof(PpsSplitButton), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(PpsSplitButton), new FrameworkPropertyMetadata(false, OnIsOpenChanged));
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary></summary>
		public PpsSplitButton()
		{
		} // ctor

		/// <summary></summary>
		public override void OnApplyTemplate()
		{
			base.OnApplyTemplate();

			if (GetTemplateChild(PartSplitButton) is ButtonBase button)
				button.Click += (sender, e) => OnDropdown();
		} // proc OnApplyTemplate

		#endregion

		#region -- OnDropDown ---------------------------------------------------------

		private void OnPopupClosed(object sender, EventArgs e)
		{
			var popup = (Popup)sender;
			popup.Closed -= OnPopupClosed;
			if (popup == Popup)
				IsOpen = false;
		} // proc OnPopupClosed

		private void OnContextMenuClosed(object sender, RoutedEventArgs e)
		{
			var ctx = (ContextMenu)sender;
			ctx.Closed -= OnContextMenuClosed;
			if (ctx == ContextMenu)
				IsOpen = false;
		} // proc OnContextMenuClosed

		private static void OnIsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsSplitButton)d).OnIsOpenChanged((bool)e.NewValue, (bool)e.OldValue);

		/// <summary></summary>
		/// <param name="newValue"></param>
		/// <param name="oldValue"></param>
		protected virtual void OnIsOpenChanged(bool newValue, bool oldValue)
		{
			if (Popup != null)
			{
				if (newValue)
					Popup.Closed += OnPopupClosed;
				Popup.IsOpen = false;
			}
			else if (ContextMenu != null)
			{
				if (newValue)
					ContextMenu.Closed += OnContextMenuClosed;
				ContextMenu.IsOpen = false;
			}
		} // OnIsOpenChanged
				
		/// <summary>Route to DropDown button</summary>
		protected override void OnClick()
		{
			if (Mode == PpsSplitButtonType.Dropdown)
				OnDropdown();
			else
				base.OnClick();
		} // proc OnClick

		/// <summary>Activate Dropdown</summary>
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
		} // proc OnDropdown

		#endregion

		/// <summary></summary>
		public bool IsOpen { get { return (bool)GetValue(IsOpenProperty); } set { SetValue(IsOpenProperty, value); } }
		/// <summary></summary>
		public Popup Popup { get { return (Popup)GetValue(PopupProperty); } set { SetValue(PopupProperty, value); } }
		/// <summary></summary>
		public PpsSplitButtonType Mode { get { return (PpsSplitButtonType)GetValue(ModeProperty); } set { SetValue(ModeProperty, value); } }

		// -- Static --------------------------------------------------------------

		static PpsSplitButton()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PpsSplitButton), new FrameworkPropertyMetadata(typeof(PpsSplitButton)));
		} // ctor
	} // class SplitButton

	#endregion
}
