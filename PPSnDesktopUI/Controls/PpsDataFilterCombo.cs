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
using System.Windows.Media;
using TecWare.DE.Data;

namespace TecWare.PPSn.Controls
{
	public partial class PpsDataFilterCombo : PpsDataFilterBase
	{
		/// <summary>DependencyProperty for DropDown state</summary>
		public static readonly DependencyProperty IsDropDownOpenProperty = DependencyProperty.Register(nameof(IsDropDownOpen), typeof(bool), typeof(PpsDataFilterCombo), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, new PropertyChangedCallback(OnIsDropDownOpenChanged)));
		/// <summary>Is PART_Popup open?</summary>
		public bool IsDropDownOpen { get => (bool)GetValue(IsDropDownOpenProperty); set => SetValue(IsDropDownOpenProperty, value); }
		
		private static void OnIsDropDownOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsDataFilterCombo)d).DropDownChanged((bool)e.NewValue);
		

		private void DropDownChanged(bool status)
		{
			this.hasMouseEnteredItemsList = false;

			if (status)
			{
				filteredListBox.Items.CurrentChanged += Items_CurrentChanged;
				this.SetAnchorItem();
				if (VisualChildrenCount > 0)
					Mouse.Capture(this, CaptureMode.SubTree);
			}
			else
			{
				filteredListBox.Items.CurrentChanged -= Items_CurrentChanged;
				// leave clean
				ClearFilter();

				// Release
				if (Mouse.Captured == this)
					Mouse.Capture(null);

				Focus();
			}
		} // delegate OnIsDropDownOpenChanged

		private double CalculateMaxDropDownHeight(double itemHeight)
		{
			// like ComboBox
			var height = Application.Current.Windows[0].ActualHeight / 3;
			// no partially visible items for itemHeight
			height += itemHeight - (height % itemHeight);
			// add header (33) and border (2)
			height += 35;
			return height;
		} // func CalculateMaxDropDownHeight

		private void CloseDropDown(bool commit)
		{
			if (!IsDropDownOpen)
				return;

			if (commit)
				ApplySelectedItem();

			IsDropDownOpen = false;
		} // proc CloseDropDown

		

		


		#region ---- Keyboard interaction -----------------------------------------------
		
		/// <summary>Handles the Navigation by Keyboard</summary>
		/// <param name="e">pressed Keys</param>
		protected override void OnPreviewKeyDown(KeyEventArgs e)
			=> KeyDownHandler(e);

		private void ToggleDropDownStatus(bool commit)
		{
			if (IsDropDownOpen)
				CloseDropDown(commit);
			else
				OpenDropDown();
		} // proc ToggleDropDown

		private void OpenDropDown()
		{
			if (IsDropDownOpen)
				return;
			IsDropDownOpen = true;
		} // proc OpenDropDown

		private void KeyDownHandler(KeyEventArgs e)
		{
			// stop
			if (IsReadOnly)
				return;

			var key = e.Key;
			if (key == Key.System)
				key = e.SystemKey;

			switch (key)
			{
				case Key.Up:
					e.Handled = true;
					if ((e.KeyboardDevice.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
					{
						ToggleDropDownStatus(true);
					}
					else
					{
						if (IsDropDownOpen)
							Navigate(FocusNavigationDirection.Previous);
						else
							ImmediateSelect(FocusNavigationDirection.Previous);
					}
					break;
				case Key.Down:
					e.Handled = true;
					if ((e.KeyboardDevice.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
					{
						ToggleDropDownStatus(true);
					}
					else
					{
						if (IsDropDownOpen)
							Navigate(FocusNavigationDirection.Next);
						else
							ImmediateSelect(FocusNavigationDirection.Next);
					}
					break;
				case Key.Home:
					if (e.KeyboardDevice.Modifiers == ModifierKeys.None)
					{
						e.Handled = true;
						if (IsDropDownOpen)
							Navigate(FocusNavigationDirection.First);
						else
							ImmediateSelect(FocusNavigationDirection.First);
					}
					break;
				case Key.End:
					if (e.KeyboardDevice.Modifiers == ModifierKeys.None)
					{
						e.Handled = true;
						if (IsDropDownOpen)
							Navigate(FocusNavigationDirection.Last);
						else
							ImmediateSelect(FocusNavigationDirection.Last);
					}
					break;
				case Key.F4:
					if ((e.KeyboardDevice.Modifiers & ModifierKeys.Alt) == 0)
					{
						e.Handled = true;
						ToggleDropDownStatus(true);
					}
					break;
				case Key.Enter:
					if (IsDropDownOpen)
					{
						e.Handled = true;
						CloseDropDown(true);
					}
					break;
				case Key.Escape:
					if (IsDropDownOpen)
					{
						e.Handled = true;
						CloseDropDown(false);
					}
					break;
				case Key.Delete:
					if (IsNullable && IsWriteable && IsDropDownOpen)
					{
						e.Handled = true;
						ClearSelection();
					}
					break;
				case Key.PageDown:
					if (IsDropDownOpen)
					{
						e.Handled = true;
						var visibleindexes = from idx in Enumerable.Range(0, filteredListBox.Items.Count) where filteredListBox.ItemContainerGenerator.ContainerFromIndex(idx) != null && ((ListBoxItem)filteredListBox.ItemContainerGenerator.ContainerFromIndex(idx)).IsVisible select idx;
						var newLast = Math.Min(filteredListBox.SelectedIndex + visibleindexes.Count() - 3, filteredListBox.Items.Count - 1);
						filteredListBox.SelectedIndex = newLast;
						filteredListBox.ScrollIntoView(filteredListBox.Items[newLast]);
					}
					break;
				case Key.PageUp:
					if (IsDropDownOpen)
					{
						e.Handled = true;
						var visibleindexes = from idx in Enumerable.Range(0, filteredListBox.Items.Count) where filteredListBox.ItemContainerGenerator.ContainerFromIndex(idx) != null && ((ListBoxItem)filteredListBox.ItemContainerGenerator.ContainerFromIndex(idx)).IsVisible select idx;
						var newLast = Math.Max(filteredListBox.SelectedIndex - visibleindexes.Count() + 3, 0);
						filteredListBox.SelectedIndex = newLast;
						filteredListBox.ScrollIntoView(filteredListBox.Items[newLast]);
					}
					break;
				case Key.Left:
				case Key.Right:
					// disable visual Navigation on the Form
					e.Handled = true;
					break;
			}
		} // proc KeyDownHandler

		public override bool IsFilteredListVisible()
		=> IsDropDownOpen;

		public override void HideFilteredList(bool commit)
		{
			CloseDropDown(commit);
		}

		#endregion

		static PpsDataFilterCombo()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PpsDataFilterCombo), new FrameworkPropertyMetadata(typeof(PpsDataFilterCombo)));
		}
	}
}
