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
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Scripting.Utils;
using TecWare.DE.Data;
using TecWare.PPSn.Data;
using System.Linq;

namespace TecWare.PPSn.Controls
{
	/// <summary>This Filterbox is used to filter a List</summary>
	public class PpsDataFilterBox : Control
	{
		/// <summary>DependencyProperty for connecting the ItemsSource</summary>
		public static readonly DependencyProperty ItemsSourceProperty = ItemsControl.ItemsSourceProperty.AddOwner(typeof(PpsDataFilterBox), new FrameworkPropertyMetadata(OnItemsSourceChanged));
		/// <summary>DependencyProperty for connecting the Filtered Items</summary>
		public static readonly DependencyProperty FilteredItemsSourceProperty = DependencyProperty.Register(nameof(FilteredItemsSource), typeof(IDataRowEnumerable), typeof(PpsDataFilterBox));
		/// <summary>DependencyProperty for conntecting the FilterTex</summary>
		public static readonly DependencyProperty FilterTextProperty = DependencyProperty.Register(nameof(FilterText), typeof(string), typeof(PpsDataFilterBox), new FrameworkPropertyMetadata(OnFilterTextChanged));
		/// <summary>DependencyProperty for the Selected(committed) Value</summary>
		public static readonly DependencyProperty SelectedValueProperty = DependencyProperty.Register(nameof(SelectedValue), typeof(object), typeof(PpsDataFilterBox));
		/// <summary>DependencyProperty for the Template of the Selected item</summary>
		public static readonly DependencyProperty SelectedValueTemplateProperty = DependencyProperty.Register(nameof(SelectedValueTemplate), typeof(DataTemplate), typeof(PpsDataFilterBox), new FrameworkPropertyMetadata((DataTemplate)null));
		/// <summary>DependencyProperty for the Template for the Items in the ListBox</summary>
		public static readonly DependencyProperty ItemTemplateProperty = DependencyProperty.Register(nameof(ItemTemplate), typeof(DataTemplate), typeof(PpsDataFilterBox), new FrameworkPropertyMetadata((DataTemplate)null));
		/// <summary>DependencyProperty for the ItemCOntainerStyle of the ListBox</summary>
		public static readonly DependencyProperty ItemContainerStyleProperty = DependencyProperty.Register(nameof(ItemContainerStyle), typeof(Style), typeof(PpsDataFilterBox), new FrameworkPropertyMetadata((Style)null));
		/// <summary>DependencyProperty for the Style of the ListBox</summary>
		public static readonly DependencyProperty ListBoxStyleProperty = DependencyProperty.Register(nameof(ListBoxStyle), typeof(Style), typeof(PpsDataFilterBox), new FrameworkPropertyMetadata((Style)null));
		/// <summary>DependencyProperty for the Nullable state</summary>
		public static readonly DependencyProperty IsNullableProperty = DependencyProperty.Register(nameof(IsNullable), typeof(bool), typeof(PpsDataFilterBox));
		/// <summary>DependencyProperty for DropDown state</summary>
		public static readonly DependencyProperty IsDropDownOpenProperty = DependencyProperty.Register(nameof(IsDropDownOpen), typeof(bool), typeof(PpsDataFilterBox), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, new PropertyChangedCallback(OnIsDropDownOpenChanged)));
		/// <summary>DependencyProperty for the Write-Protection state</summary>
		public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.Register(nameof(IsReadOnly), typeof(bool), typeof(PpsDataFilterBox), new FrameworkPropertyMetadata(false));

		/// <summary>Command for clearing the Value</summary>
		public readonly static RoutedCommand ClearSelectionCommand = new RoutedCommand("ClearSelection", typeof(PpsDataFilterBox));

		private const string SearchBoxTemplateName = "PART_SearchBox";
		private const string ListBoxTemplateName = "PART_ItemsListBox";
		private const string PopupTemplateName = "PART_DropDownPopup";

		private ListBox itemsListBox;
		private bool hasMouseEnteredItemsList;
		private Point lastMousePosition = new Point();


		private void UpdateFilteredList()
		{
			if (ItemsSource == null)
				return;

			var expr = String.IsNullOrWhiteSpace(FilterText) ? PpsDataFilterExpression.True : PpsDataFilterExpression.Parse(FilterText);
			FilteredItemsSource = (expr == PpsDataFilterExpression.True) ? ItemsSource : ItemsSource.ApplyFilter(expr);
		} // proc UpdateFilteredList

		/// <summary>loads the List when the Control is used</summary>
		/// <param name="e">unused/sent to base class</param>
		protected override void OnGotFocus(RoutedEventArgs e)
		{
			ReferenceListBox();
			base.OnGotFocus(e);
		}

		private bool ReferenceListBox()
		{
			if (itemsListBox != null)
				return true;

			if (GetTemplateChild("PART_DropDownPopup") is Popup popup)
			{
				popup.ApplyTemplate();

				var childDataFilterBox = popup.Child.GetVisualChild<PpsDataFilterBox>();
				childDataFilterBox.ApplyTemplate();
				itemsListBox = (ListBox)childDataFilterBox.GetTemplateChild(ListBoxTemplateName);
				if (itemsListBox?.Items.Count > 0)
					itemsListBox.ItemContainerGenerator.StatusChanged += (sender, e) =>
					{
						var container = (ListBoxItem)(from IDataRow itm in itemsListBox.Items where itemsListBox.ItemContainerGenerator.ContainerFromItem(itm) != null select itemsListBox.ItemContainerGenerator.ContainerFromItem(itm)).FirstOrDefault();
						if (container != null && container.ActualHeight > 0)
							popup.MaxHeight = CalculateMaxDropDownHeight(((ListBoxItem)container).ActualHeight);
					};
				if (popup.IsOpen)
					this.Focus();

				((FrameworkElement)popup.Child).SizeChanged += (sender, e) =>
				{
					popup.HorizontalOffset = (((FrameworkElement)popup.PlacementTarget).ActualWidth) - itemsListBox.ActualWidth;
				};
			}
			else
			{
				this.ApplyTemplate();
				itemsListBox = (ListBox)this.GetTemplateChild(ListBoxTemplateName);
			}

			if (itemsListBox != null && itemsListBox.Items.Count <= 0)
				itemsListBox = null;

			return (itemsListBox != null);
		}

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

		/// <summary>Constructor - initializes the Commands</summary>
		public PpsDataFilterBox()
		{
			AddClearCommand();
		}

		private void DropDownChanged(bool status)
		{
			if (!ReferenceListBox())
				return;

			this.hasMouseEnteredItemsList = false;

			if (status)
			{
				itemsListBox.Items.CurrentChanged += Items_CurrentChanged;
				this.SetAnchorItem();
				if (VisualChildrenCount > 0)
					Mouse.Capture(this, CaptureMode.SubTree);
			}
			else
			{
				itemsListBox.Items.CurrentChanged -= Items_CurrentChanged;
				// leave clean
				ClearFilter();

				// Release
				if (Mouse.Captured == this)
					Mouse.Capture(null);

				Focus();
			}
		} // delegate OnIsDropDownOpenChanged

		#region -- Evaluate MouseEvents -----------------------------------------------

		/// <summary>Handles the Mouseclicks - mainly for closing the Popup</summary>
		/// <param name="e"></param>
		protected override void OnMouseDown(MouseButtonEventArgs e)
		{
			if (!IsDropDownOpen)
			{
				base.OnMouseDown(e);
				return;
			}

			if (!IsKeyboardFocusWithin)
				Focus();

			// always handle
			e.Handled = true;

			// Then the click was outside of Popup
			if (Mouse.Captured == this && e.OriginalSource == this)
				CloseDropDown(false);
		} // event OnMouseDown

		/// <summary>Handles the Mouseclicks - mainly for selecting in the ItemList</summary>
		/// <param name="e"></param>
		protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
		{
			if (!hasMouseEnteredItemsList)
				return;

			var hoveredItem = ItemFromPoint(e);

			if (hoveredItem != null && IsWriteable)
			{
				// this case is for Searchable List, where the hovered item is not selected but the clicked
				if (!hoveredItem.IsSelected)
					hoveredItem.IsSelected = true;
				e.Handled = true;
				CloseDropDown(true);
			}
		} // event OnMouseLeftButtonUp

		private void SetFocus()
		{
			var actualfocus = Keyboard.FocusedElement;

			if (actualfocus != null)
				// the mouse is over the window, but the window is not focused
				if (!this.IsAncestorOf((FrameworkElement)actualfocus))
					// the searchbox is not already focused
					if (((Visual)actualfocus).IsAncestorOf(this))
						// the actual focus is more general - do not catch the focus if a sibling is focused
						((TextBox)((PpsDataFilterBox)((Grid)this.GetVisualChild(0)).GetVisualChild<PpsDataFilterBox>()).GetTemplateChild("PART_SearchBox")).Focus();
		}

		/// <summary>Handles the Movement of the Mouse - used for UI-Feedback of the ''would-be'' selected Item</summary>
		/// <param name="e"></param>
		protected override void OnMouseMove(MouseEventArgs e)
		{
			e.Handled = true;

			var item = ItemFromPoint(e);
			if (item == null)
				return;

			if (!hasMouseEnteredItemsList)
			{
				if (e.LeftButton == MouseButtonState.Released)
				{
					lastMousePosition = Mouse.GetPosition(itemsListBox);
					hasMouseEnteredItemsList = true;
				}
			}
			else
			{
				SetFocus();

				if (IsDropDownOpen && HasMouseMoved() && !item.IsSelected)
				{
					item.IsSelected = true;
				}
			}
		} // event OnMouseMove

		private bool HasMouseMoved()
		{
			var newPosition = Mouse.GetPosition(itemsListBox);
			if (newPosition != lastMousePosition)
			{
				lastMousePosition = newPosition;
				return true;
			}
			return false;
		} // func HasMouseMoved

		#endregion

		#region -- Evaluate KeyboardEvents --------------------------------------------

		/// <summary>Handles the Navigation by Keyboard</summary>
		/// <param name="e">pressed Keys</param>
		protected override void OnPreviewKeyDown(KeyEventArgs e)
			=> KeyDownHandler(e);

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
				case Key.Left:
				case Key.Right:
					// disable visual Navigation on the Form
					e.Handled = true;
					break;
			}
		} // proc KeyDownHandler

		#endregion

		private void AddClearCommand()
		{
			CommandBindings.Add(
				new CommandBinding(ClearSelectionCommand,
					(sender, e) =>
					{
						ClearSelection();
						e.Handled = true;
					},
					(sender, e) => e.CanExecute = IsNullable && IsWriteable && (SelectedValue != null)
				)
			);
		} // proc AddClearCommand

		private void ClearSelection()
		{
			CommitValue(null);
		}

		private void Navigate(FocusNavigationDirection direction)
		{
			if (!ReferenceListBox())
				return;

			var itemsCount = itemsListBox.Items.Count;
			if (itemsCount == 0)
				return;

			var curPos = itemsListBox.Items.CurrentPosition;
			var newPos = CalculateNewPos(curPos, itemsCount, direction);

			if (newPos != curPos)
				itemsListBox.Items.MoveCurrentToPosition(newPos);
		} // proc Navigate

		private void ImmediateSelect(FocusNavigationDirection direction)
		{
			if (!ReferenceListBox())
				return;

			var itemsCount = itemsListBox.Items.Count;
			if (itemsCount == 0)
				return;

			var curIndex = -1;
			if (SelectedValue != null)
			{
				curIndex = itemsListBox.Items.IndexOf(SelectedValue);
				if (curIndex < 0)
					return;
			}

			var newIndex = CalculateNewPos(curIndex, itemsCount, direction);

			if (newIndex != curIndex)
			{
				if (itemsListBox.Items.GetItemAt(newIndex) is IDataRow item)
					CommitValue(item);

				itemsListBox.ScrollIntoView(SelectedValue);
			}
		} // proc ImmediateSelect

		private void CommitValue(IDataRow value)
		{
			if (!object.Equals(value, SelectedValue))
				SelectedValue = value;
		} // proc CommitValue

		private int CalculateNewPos(int currentPos, int items, FocusNavigationDirection direction)
		{
			var newPos = currentPos;
			switch (direction)
			{
				case FocusNavigationDirection.First:
					newPos = 0;
					break;
				case FocusNavigationDirection.Last:
					newPos = items - 1;
					break;
				case FocusNavigationDirection.Next:
					newPos++;
					if (newPos >= items)
						newPos = items - 1;
					break;
				case FocusNavigationDirection.Previous:
					newPos--;
					if (newPos < 0)
						newPos = 0;
					break;
			}
			return newPos;
		} // func CalculateNewPos

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

		private ListBoxItem ItemFromPoint(MouseEventArgs e)
		{
			if (!ReferenceListBox())
				return null;

			var point = e.GetPosition(itemsListBox);
			var element = itemsListBox.InputHitTest(point) as UIElement;
			while (element != null)
			{
				if (element is ListBoxItem)
				{
					return (ListBoxItem)element;
				}
				element = VisualTreeHelper.GetParent(element) as UIElement;
			}
			return null;
		} // func ItemFromPoint

		private void Items_CurrentChanged(object sender, EventArgs e)
		{
			if (!ReferenceListBox())
				return;

			if (itemsListBox.Items.CurrentItem is IDataRow item)
				itemsListBox.ScrollIntoView(item);
		} // event Items_CurrentChanged

		private void SetAnchorItem()
		{
			if (!ReferenceListBox() || itemsListBox.Items.Count <= 0)
				return;

			var item = SelectedValue ?? itemsListBox.Items.GetItemAt(0);
			itemsListBox.Items.MoveCurrentTo(item);

			// clear selection?
			if (SelectedValue == null)
				itemsListBox.Items.MoveCurrentToPosition(-1);
		} // proc SetAnchorItem

		/// <summary>Empties the string for filtering</summary>
		public void ClearFilter()
			=> FilterText = null;

		private void CloseDropDown(bool commit)
		{
			if (!IsDropDownOpen)
				return;

			if (commit)
				CommitValue((IDataRow)itemsListBox.SelectedValue);

			IsDropDownOpen = false;

			ClearFilter();
		} // proc CloseDropDown

		/// <summary>incoming list with all items</summary>
		public IDataRowEnumerable ItemsSource { get => (IDataRowEnumerable)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
		/// <summary>filtered list for binding</summary>
		public IDataRowEnumerable FilteredItemsSource { get => (IDataRowEnumerable)GetValue(FilteredItemsSourceProperty); set => SetValue(FilteredItemsSourceProperty, value); }
		/// <summary>Current searchstring</summary>
		public string FilterText { get => (string)GetValue(FilterTextProperty); set => SetValue(FilterTextProperty, value); }
		/// <summary>Value which is Selected</summary>
		public object SelectedValue { get => GetValue(SelectedValueProperty); set => SetValue(SelectedValueProperty, value); }
		/// <summary>the template which is used by a control to show the actual value (not used internally - only as a binding-post)</summary>
		public DataTemplate SelectedValueTemplate { get => (DataTemplate)GetValue(SelectedValueTemplateProperty); set => SetValue(SelectedValueTemplateProperty, value); }
		/// <summary>the template which is used by a control to show the items in the list (not used internally - only as a binding-post)</summary>
		public DataTemplate ItemTemplate { get => (DataTemplate)GetValue(ItemTemplateProperty); set => SetValue(ItemTemplateProperty, value); }
		/// <summary>the ContainerStyle which is used by a listbox to show the items (not used internally - only as a binding-post)</summary>
		public Style ItemContainerStyle { get => (Style)GetValue(ItemContainerStyleProperty); set => SetValue(ItemContainerStyleProperty, value); }
		/// <summary>the style which is used by the listbox (not used internally - only as a binding-post)</summary>
		public Style ListBoxStyle { get => (Style)GetValue(ListBoxStyleProperty); set => SetValue(ListBoxStyleProperty, value); }
		/// <summary>if selectedvalue may be null (not used internally - only as a binding-post)</summary>
		public bool IsNullable { get => (bool)GetValue(IsNullableProperty); set => SetValue(IsNullableProperty, value); }
		/// <summary>Is PART_Popup open?</summary>
		public bool IsDropDownOpen { get => (bool)GetValue(IsDropDownOpenProperty); set => SetValue(IsDropDownOpenProperty, value); }
		/// <summary>Can user select content?</summary>
		public bool IsReadOnly { get => (bool)GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }
		/// <summary>Can user select content?</summary>
		public bool IsWriteable { get => !(bool)GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, !value); }

		private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsDataFilterBox)d).UpdateFilteredList();
		private static void OnFilterTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsDataFilterBox)d).UpdateFilteredList();
		private static void OnIsDropDownOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsDataFilterBox)d).DropDownChanged((bool)e.NewValue);
	}
}
