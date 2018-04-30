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
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using Microsoft.Scripting.Utils;
using TecWare.DE.Data;
using TecWare.PPSn.Data;

namespace TecWare.PPSn.Controls
{
	/// <summary>This Filterbox is used to filter a List</summary>
	[TemplatePart(Name = "PART_FilteredItemsListBox", Type =typeof(ListBox))]
	public abstract partial class PpsDataFilterBase : Selector
	{
		#region ---- Dependency Propteries-----------------------------------------------

		/// <summary>DependencyProperty for connecting the Filtered Items</summary>
		public static readonly DependencyProperty FilteredItemsSourceProperty = DependencyProperty.Register(nameof(FilteredItemsSource), typeof(IEnumerable), typeof(PpsDataFilterBase));
		/// <summary>DependencyProperty for conntecting the FilterTex</summary>
		public static readonly DependencyProperty FilterTextProperty = DependencyProperty.Register(nameof(FilterText), typeof(string), typeof(PpsDataFilterBase), new FrameworkPropertyMetadata(OnFilterTextChanged));
		
		/// <summary>DependencyProperty for the Template of the Selected item</summary>
		public static readonly DependencyProperty SelectedValueTemplateProperty = DependencyProperty.Register(nameof(SelectedValueTemplate), typeof(DataTemplate), typeof(PpsDataFilterBase), new FrameworkPropertyMetadata((DataTemplate)null));
		/// <summary>DependencyProperty for the Style of the ListBox</summary>
		public static readonly DependencyProperty ListBoxStyleProperty = DependencyProperty.Register(nameof(ListBoxStyle), typeof(Style), typeof(PpsDataFilterBase), new FrameworkPropertyMetadata((Style)null));
		/// <summary>DependencyProperty for the Nullable state</summary>
		public static readonly DependencyProperty IsNullableProperty = DependencyProperty.Register(nameof(IsNullable), typeof(bool), typeof(PpsDataFilterBase), new PropertyMetadata(true));
		/// <summary>DependencyProperty for the Write-Protection state</summary>
		public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.Register(nameof(IsReadOnly), typeof(bool), typeof(PpsDataFilterBase), new FrameworkPropertyMetadata(false));
		public static readonly DependencyProperty PreSelectedValueProperty = DependencyProperty.Register(nameof(PreSelectedValue), typeof(object), typeof(PpsDataFilterCombo));
		

		#endregion

		#region ---- Statics ------------------------------------------------------------

		/// <summary>Command for clearing the Filter</summary>
		public readonly static RoutedCommand ClearFilterCommand = new RoutedCommand("ClearFilter", typeof(PpsDataFilterBase));
		/// <summary>Command for clearing the Filter</summary>
		public readonly static RoutedCommand ClearValueCommand = new RoutedCommand("ClearValue", typeof(PpsDataFilterBase));

		#endregion

		#region ---- Fields -------------------------------------------------------------

		private const string FilteredItemsListBoxName = "PART_FilteredItemsListBox";
		internal ListBox filteredListBox;
		internal bool hasMouseEnteredItemsList;

		#endregion

		#region ---- Events -------------------------------------------------------------

		public override void OnApplyTemplate()
		{
			base.OnApplyTemplate();

			if (GetTemplateChild(FilteredItemsListBoxName) is ListBox listBox)
				filteredListBox = listBox;
		} // proc OnApplyTemplate

		private void UpdateFilteredList()
		{
			if (ItemsSource == null)
				return;
			
			var expr = String.IsNullOrWhiteSpace(FilterText) ? PpsDataFilterExpression.True : PpsDataFilterExpression.Parse(FilterText);
			FilteredItemsSource = ((expr == PpsDataFilterExpression.True) || !(ItemsSource is IDataRowEnumerable idre)) ? ItemsSource : idre.ApplyFilter(expr);
		} // proc UpdateFilteredList

		/// <summary>loads the List when the Control is used</summary>
		/// <param name="e">unused/sent to base class</param>
		protected override void OnGotFocus(RoutedEventArgs e)
		{
			base.OnGotFocus(e);
		}

		/// <summary>Constructor - initializes the Commands</summary>
		public PpsDataFilterBase()
		{
			AddClearCommand();
		}

		/// <summary>if the ItemsSource changes the Filter is re-applied</summary>
		/// <param name="oldValue"></param>
		/// <param name="newValue"></param>
		protected override void OnItemsSourceChanged(IEnumerable oldValue, IEnumerable newValue)
		{
			base.OnItemsSourceChanged(oldValue, newValue);
			UpdateFilteredList();
		}

		private static void OnFilterTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsDataFilterBase)d).UpdateFilteredList();

		#endregion

		#region ---- Helper Functions ---------------------------------------------------

		internal void ClearSelection()
		{
			CommitValue(null);
		}

		private void CommitValue(IDataRow value)
		{
			if (!object.Equals(value, SelectedValue))
				SelectedValue = value;
		} // proc CommitValue

		/// <summary>Empties the string for filtering</summary>
		public void ClearFilter()
			=> FilterText = null;

		#endregion
		internal void ImmediateSelect(FocusNavigationDirection direction)
		{
			var itemsCount = filteredListBox.Items.Count;
			if (itemsCount == 0)
				return;

			var curIndex = -1;
			if (filteredListBox.SelectedItem != null)
			{
				curIndex = filteredListBox.SelectedIndex;
			}
			else if (SelectedValue != null)
			{
				curIndex = filteredListBox.Items.IndexOf(SelectedValue);
				if (curIndex < 0)
					return;
			}

			var newIndex = CalculateNewPos(curIndex, itemsCount, direction);

			if (newIndex != curIndex)
			{
				filteredListBox.SelectedIndex = newIndex;
				if (filteredListBox.Items.GetItemAt(newIndex) is IDataRow item)
					CommitValue(item);

				filteredListBox.ScrollIntoView(SelectedValue);
			}
		} // proc ImmediateSelect


		internal void Navigate(FocusNavigationDirection direction)
		{
			var itemsCount = filteredListBox.Items.Count;
			if (itemsCount == 0)
				return;

			var curPos = filteredListBox.Items.CurrentPosition;
			var newPos = CalculateNewPos(curPos, itemsCount, direction);

			if (newPos != curPos)
				filteredListBox.Items.MoveCurrentToPosition(newPos);
		} // proc Navigate

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

		internal void ApplySelectedItem()
		{
			if (PreSelectedValue is IDataRow item)
				CommitValue(item);
		} // proc ApplySelectedItem

		private void CommitValue(object value)
		{
			if (!Equals(value, SelectedValue))
				SelectedValue = value;
		} // proc CommitValue

		#region ---- UI interaction -----------------------------------------------------

		private void AddClearCommand()
		{
			CommandBindings.Add(
				new CommandBinding(ClearFilterCommand,
					(sender, e) =>
					{
						ClearFilter();
						e.Handled = true;
					},
					(sender, e) => e.CanExecute = !String.IsNullOrEmpty(FilterText)
				)
			);
			CommandBindings.Add(
				new CommandBinding(ClearValueCommand,
					(sender, e) =>
					{
						SelectedValue = null;
						e.Handled = true;
					},
					(sender, e) => e.CanExecute = IsNullable && SelectedValue != null
				)
			);
		} // proc AddClearCommand

		#endregion

		public abstract bool IsFilteredListVisible();
		public abstract void HideFilteredList(bool commit);

		internal void Items_CurrentChanged(object sender, EventArgs e)
		{
			if (filteredListBox.Items.CurrentItem is IDataRow item)
				filteredListBox.ScrollIntoView(item);
		} // event Items_CurrentChanged
		
		internal void SetAnchorItem()
		{
			if (!IsFilteredListVisible() || filteredListBox.Items==null || filteredListBox.Items.Count <=0)
				return;



			var item = SelectedValue ?? filteredListBox.Items.GetItemAt(0);
			filteredListBox.Items.MoveCurrentTo(item);

			// clear selection?
			if (SelectedValue == null)
				filteredListBox.Items.MoveCurrentToPosition(-1);
		} // proc SetAnchorItem

		#region -- Evaluate MouseEvents -----------------------------------------------
		

		protected override void OnMouseDown(MouseButtonEventArgs e)
		{
			if (!IsKeyboardFocusWithin)
				Focus();

			// always handle
			e.Handled = true;

			if (!IsFilteredListVisible())
				return;

			// Then the click was outside of Popup
			if (Mouse.Captured == this && e.OriginalSource == this)
				HideFilteredList(false);
		} // event OnMouseDown

		private ListBoxItem ItemFromPoint(MouseEventArgs e)
		{
			var point = e.GetPosition(filteredListBox);
			var element = filteredListBox.InputHitTest(point) as UIElement;
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

		protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
		{
			if (!IsFilteredListVisible() || !hasMouseEnteredItemsList)
				return;

			if (ItemFromPoint(e) != null)
			{
				e.Handled = true;
				HideFilteredList(true);
			}
		} // event OnMouseLeftButtonUp
		private Point lastMousePosition = new Point();
		protected override void OnMouseMove(MouseEventArgs e)
		{
			if (!IsFilteredListVisible())
				return;

			e.Handled = true;

			var item = ItemFromPoint(e);
			if (item == null)
				return;

			if (!hasMouseEnteredItemsList)
			{
				if (e.LeftButton == MouseButtonState.Released)
				{
					lastMousePosition = Mouse.GetPosition(filteredListBox);
					hasMouseEnteredItemsList = true;
				}
			}
			else
			{
				if (HasMouseMoved() && !item.IsSelected)
				{
					item.IsSelected = true;
				}
			}
		} // event OnMouseMove

		private bool HasMouseMoved()
		{
			var newPosition = Mouse.GetPosition(filteredListBox);
			if (newPosition != lastMousePosition)
			{
				lastMousePosition = newPosition;
				return true;
			}
			return false;
		} // func HasMouseMoved

		#endregion

		#region ---- Properties ---------------------------------------------------------

		/// <summary>filtered list for binding</summary>
		public IEnumerable FilteredItemsSource { get => (IEnumerable)GetValue(FilteredItemsSourceProperty); private set => SetValue(FilteredItemsSourceProperty, value); }
		/// <summary>Current searchstring</summary>
		public string FilterText { get => (string)GetValue(FilterTextProperty); set => SetValue(FilterTextProperty, value); }
		/// <summary>the template which is used by a control to show the actual value (not used internally - only as a binding-post)</summary>
		public DataTemplate SelectedValueTemplate { get => (DataTemplate)GetValue(SelectedValueTemplateProperty); set => SetValue(SelectedValueTemplateProperty, value); }
		/// <summary>the style which is used by the listbox (not used internally - only as a binding-post)</summary>
		public Style ListBoxStyle { get => (Style)GetValue(ListBoxStyleProperty); set => SetValue(ListBoxStyleProperty, value); }
		/// <summary>if selectedvalue may be null (not used internally - only as a binding-post)</summary>
		public bool IsNullable { get => (bool)GetValue(IsNullableProperty); set => SetValue(IsNullableProperty, value); }
		/// <summary>Can user select content?</summary>
		public bool IsReadOnly { get => (bool)GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }
		/// <summary>Can user select content?</summary>
		public bool IsWriteable { get => !(bool)GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, !value); }
		public object PreSelectedValue { get => GetValue(PreSelectedValueProperty); set => SetValue(PreSelectedValueProperty, value); }

		#endregion
	}
}
