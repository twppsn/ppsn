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
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using TecWare.DE.Data;
using TecWare.PPSn.Data;

namespace TecWare.PPSn.Controls
{
	#region -- class PpsDataSelector --------------------------------------------------

	/// <summary>Extended ComboBox which enables searching, with Highlighting</summary>
	public class PpsDataSelector : Selector
	{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

		public static readonly DependencyProperty SelectedValueProperty = DependencyProperty.Register(nameof(SelectedValue), typeof(IDataRow), typeof(PpsDataSelector), new FrameworkPropertyMetadata((IDataRow)null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
		public static readonly DependencyProperty SelectedValuePathProperty = DependencyProperty.Register(nameof(SelectedValuePath), typeof(string), typeof(PpsDataSelector));
		public static readonly DependencyProperty ItemsSourceProperty = ItemsControl.ItemsSourceProperty.AddOwner(typeof(PpsDataSelector), new FrameworkPropertyMetadata(null, new PropertyChangedCallback( OnItemsSourceChanged), new CoerceValueCallback(OnItemsSourceCoerceValue)));

		private static readonly DependencyPropertyKey FilteredItemsSourcePropertyKey = DependencyProperty.RegisterReadOnly(nameof(FilteredItemsSource), typeof(IEnumerable<IDataRow>), typeof(PpsDataSelector), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty FilteredItemsSourceProperty = FilteredItemsSourcePropertyKey.DependencyProperty;


		public static readonly DependencyProperty FilterTextProperty = DependencyProperty.Register(nameof(FilterText), typeof(string), typeof(PpsDataSelector), new FrameworkPropertyMetadata(OnFilterTextChanged));

		//public static readonly DependencyProperty ItemTemplateProperty = DependencyProperty.Register(nameof(ItemTemplate), typeof(DataTemplate), typeof(PpsDataSelector), new FrameworkPropertyMetadata((DataTemplate)null));
		public static readonly DependencyProperty SelectedValueTemplateProperty = DependencyProperty.Register(nameof(SelectedValueTemplate), typeof(DataTemplate), typeof(PpsDataSelector), new FrameworkPropertyMetadata((DataTemplate)null));

		public static readonly DependencyProperty IsDropDownOpenProperty = ComboBox.IsDropDownOpenProperty.AddOwner(typeof(PpsDataSelector), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, new PropertyChangedCallback(OnIsDropDownOpenChanged)));

		public static readonly DependencyProperty IsReadOnlyProperty = TextBoxBase.IsReadOnlyProperty.AddOwner(typeof(PpsDataSelector));
		public static readonly DependencyProperty IsNullableProperty = PpsTextBox.IsNullableProperty.AddOwner(typeof(PpsDataSelector));

		public static readonly RoutedCommand ClearSelectionCommand = new RoutedUICommand("Clear Selection", "ClearSelection", typeof(PpsDataSelector));
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		/// <summary>Template name for the popup</summary>
		public const string PopupTemplateName = "PART_DropDownPopup";
		/// <summary>Template name for the search text box.</summary>
		public const string SearchBoxTemplateName = "PART_SearchTextBox";
		/// <summary>Template anem for the listbox.</summary>
		public const string ListBoxTemplateName = "PART_ItemsListBox";

		private TextBox searchTextBox = null;
		private ListBox itemsListBox = null;

		private bool hasMouseEnteredItemsList = false;
		private Point lastMousePosition = new Point();

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary></summary>
		public PpsDataSelector()
		{
			AddClearCommand();
		} // ctor

		public override void OnApplyTemplate()
		{
			base.OnApplyTemplate();

			searchTextBox = GetTemplateChild(SearchBoxTemplateName) as TextBox ?? throw new ArgumentNullException(SearchBoxTemplateName);
			itemsListBox = GetTemplateChild(ListBoxTemplateName) as ListBox ?? throw new ArgumentNullException(ListBoxTemplateName);
			var popup = GetTemplateChild(PopupTemplateName) as Popup ?? throw new ArgumentNullException(PopupTemplateName);

			popup.Closed += OnPopupClosed;
			popup.MaxHeight = CalculateMaxDropDownHeight();
		} // proc OnApplyTemplate

		#endregion

		#region -- SelectedValue ------------------------------------------------------

		private void CommitValue(object value)
		{
			if (!Equals(value, SelectedValue))
				SelectedValue = value;
		} // proc CommitValue

		private void ClearSelection()
			=> CommitValue(null);

		private void AddClearCommand()
		{
			CommandBindings.Add(
				new CommandBinding(ClearSelectionCommand,
					(sender, e) =>
					{
						ClearSelection();
						e.Handled = true;
					},
					(sender, e) => e.CanExecute = IsClearSelectionAllowed
				)
			);
		} // proc AddClearCommand

		private bool IsClearSelectionAllowed => IsNullable && !IsReadOnly && !IsDropDownOpen && SelectedValue != null && IsKeyboardFocusWithin;

		#endregion

		#region -- Filter -------------------------------------------------------------

		/// <summary>Clear filter-text and filter.</summary>
		public void ClearFilter()
		{
			ClearFilterText();
			ClearSearchTextBox();
		} // proc Clearfilter

		/// <summary>Clear current filter.</summary>
		public void ClearFilterText()
			=> FilterText = null;

		/// <summary>Clear the selection textbox content.</summary>
		public void ClearSearchTextBox()
			=> searchTextBox.Clear();


		private static object OnItemsSourceCoerceValue(DependencyObject d, object baseValue)
		{
			if (baseValue is ICollectionViewFactory f)
				baseValue = f.CreateView();
			return baseValue;
		} // func OnItemsSourceCoerceValue

		private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsDataSelector)d).UpdateFilteredList();

		private static void OnFilterTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsDataSelector)d).OnFilterTextChanged((string)e.NewValue, (string)e.OldValue);

		/// <summary></summary>
		/// <param name="newValue"></param>
		/// <param name="oldValue"></param>
		protected virtual void OnFilterTextChanged(string newValue, string oldValue)
			=> UpdateFilteredList();

		private void UpdateFilteredList()
		{
			if (ItemsSource == null)
				return;

			var expr = String.IsNullOrWhiteSpace(FilterText) ? PpsDataFilterExpression.True : PpsDataFilterExpression.Parse(FilterText);
			SetValue(filteredItemsSourcePropertyKey,
				expr == PpsDataFilterExpression.True
				? ItemsSource
				: ((IDataRowEnumerable)ItemsSource).ApplyFilter(expr)
			);
		} // proc UpdateFilteredList

		#endregion

		#region -- DropDownPopup ------------------------------------------------------

		// Handle IsDropDownOpenProperty when triggered from StaysOpen=False
		private void OnPopupClosed(object source, EventArgs e)
			=> CloseDropDown(false);

		private static void OnIsDropDownOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsDataSelector)d).OnIsDropDownOpenChanged(BooleanBox.GetBool(e.NewValue), BooleanBox.GetBool(e.OldValue));

		private void OnIsDropDownOpenChanged(bool newValue, bool oldValue)
		{
			hasMouseEnteredItemsList = false;

			if (newValue)
			{
				Items.CurrentChanged += Items_CurrentChanged;
				SetAnchorItem();
				Mouse.Capture(this, CaptureMode.SubTree);
			}
			else
			{
				Items.CurrentChanged -= Items_CurrentChanged;
				// leave clean
				ClearFilter();

				// Otherwise focus is in the disposed hWnd
				if (IsKeyboardFocusWithin)
					Focus();

				// Release
				if (Mouse.Captured == this)
					Mouse.Capture(null);
			}
		} // proc OnIsDropDownOpenChanged

		private void ToggleDropDownStatus(bool commit)
		{
			if (IsDropDownOpen)
				CloseDropDown(commit);
			else
				OpenDropDown();
		} // proc ToggleDropDown

		private void CloseDropDown(bool commit)
		{
			if (!IsDropDownOpen)
				return;

			if (commit)
				ApplySelectedItem();

			IsDropDownOpen = false;
		} // proc CloseDropDown

		private void OpenDropDown()
		{
			if (IsDropDownOpen)
				return;
			IsDropDownOpen = true;
		} // proc OpenDropDown

		private double CalculateMaxDropDownHeight()
		{
			// like ComboBox
			var height = SystemParameters.PrimaryScreenHeight / 3;
			// no partially visible items for default itemheight (29)
			height += 29 - (height % 29);
			// add header (33) and border (2)
			height += 35;
			return height;
		} // func CalculateMaxDropDownHeight

		#endregion

		#region -- ItemsList ----------------------------------------------------------

		private void SetAnchorItem()
		{
			if (Items.Count == 0)
				return;

			var item = SelectedValue ?? Items.GetItemAt(0);
			Items.MoveCurrentTo(item);

			// clear selection?
			if (SelectedValue == null)
				Items.MoveCurrentToPosition(-1);
		} // proc SetAnchorItem

		private void ApplySelectedItem()
		{
			if (Items.CurrentItem is IDataRow item)
				CommitValue(item);
		} // proc ApplySelectedItem

		private void Items_CurrentChanged(object sender, EventArgs e)
		{
			if (Items.CurrentItem is IDataRow item)
				itemsListBox.ScrollIntoView(item);
		} // event Items_CurrentChanged

		private void Navigate(FocusNavigationDirection direction)
		{
			var items = Items.Count;
			if (items == 0)
				return;

			var curPos = Items.CurrentPosition;
			var newPos = CalculateNewPos(curPos, items, direction);

			if (newPos != curPos)
				Items.MoveCurrentToPosition(newPos);
		} // proc Navigate

		private void ImmediateSelect(FocusNavigationDirection direction)
		{
			var itemsCount = Items.Count;
			if (itemsCount == 0)
				return;

			var currentIndex = -1;
			if (SelectedValue != null)
			{
				currentIndex = Items.IndexOf(SelectedValue);
				if (currentIndex < 0)
					return;
			}

			var newIndex = CalculateNewPos(currentIndex, itemsCount, direction);

			if (newIndex != currentIndex)
			{
				if (Items.GetItemAt(newIndex) is IDataRow item)
					CommitValue(item);
			}
		} // proc ImmediateSelect

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

		private ListBoxItem ItemFromPoint(MouseEventArgs e)
		{
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

		//private ItemCollection Items => itemsListBox.Items;

		#endregion

		#region -- Evaluate MouseEvents -----------------------------------------------

		protected override void OnMouseDown(MouseButtonEventArgs e)
		{
			if (!IsKeyboardFocusWithin)
				Focus();

			// always handle
			e.Handled = true;

			if (!IsDropDownOpen)
				return;

			// Then the click was outside of Popup
			if (Mouse.Captured == this && e.OriginalSource == this)
				CloseDropDown(false);
		} // event OnMouseDown

		protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
		{
			if (!IsDropDownOpen || !hasMouseEnteredItemsList)
				return;

			if (ItemFromPoint(e) != null)
			{
				e.Handled = true;
				CloseDropDown(true);
			}
		} // event OnMouseLeftButtonUp

		protected override void OnMouseMove(MouseEventArgs e)
		{
			if (!IsDropDownOpen)
				return;

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
				if (HasMouseMoved() && !item.IsSelected)
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

		// cannot use OnKeyDown, (searchTextBox handled navigation keys)
		protected override void OnPreviewKeyDown(KeyEventArgs e)
			=> KeyDownHandler(e);

		private void KeyDownHandler(KeyEventArgs e)
		{
			// stop
			if (IsReadOnly)
				return;

			Key key = e.Key;
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
					if (IsClearSelectionAllowed)
					{
						e.Handled = true;
						ClearSelection();
					}
					break;
			}
		} // proc KeyDownHandler

		#endregion

		/// <summary>List of Data to select from</summary>	
		//public IDataRowEnumerable ItemsSource { get => (IDataRowEnumerable)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

		/// <summary>Access to the filtered list.</summary>
		public IEnumerable<IDataRow> FilteredItemsSource => (IEnumerable<IDataRow>)GetValue(FilteredItemsSourceProperty);

		/// <summary>Datatemplate for FilteredItems</summary>
		//public DataTemplate ItemTemplate { get => (DataTemplate)GetValue(ItemTemplateProperty); set => SetValue(ItemTemplateProperty, value); }

		/// <summary>Actual Value</summary>
		/*public object SelectedValue { get {
				if (String.IsNullOrEmpty(SelectedValuePath))
					return (IDataRow)(GetValue(SelectedValueProperty));
				return (IDataColumn)((IDataRow)(GetValue(SelectedValueProperty)))[SelectedValuePath];
			} set => SetValue(SelectedValueProperty, value); }
*/
		//public string SelectedValuePath { get => (string)GetValue(SelectedValuePathProperty); set => SetValue(SelectedValuePathProperty, value); }

		/// <summary>Datatemplate for selected value</summary>
		public DataTemplate SelectedValueTemplate { get => (DataTemplate)GetValue(SelectedValueTemplateProperty); set => SetValue(SelectedValueTemplateProperty, value); }

		/// <summary>Current searchstring</summary>
		public string FilterText { get => (string)GetValue(FilterTextProperty); set => SetValue(FilterTextProperty, value); }

		/// <summary>Is PART_Popup open?</summary>
		public bool IsDropDownOpen { get => (bool)GetValue(IsDropDownOpenProperty); set => SetValue(IsDropDownOpenProperty, value); }

		/// <summary>Can user select content?</summary>
		public bool IsReadOnly { get => (bool)GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

		/// <summary>Can user clear content?</summary>
		public bool IsNullable { get => (bool)GetValue(IsNullableProperty); set => SetValue(IsNullableProperty, value); }

	} // class PpsDataSelector

	#endregion

	#region -- class PpsDataSelectorItemTextBlock -------------------------------------

	/// <summary>This TextBox enables highlighting parts of the Text - BaseText is the input text, SearchText is the whitespace-separated list of keywords</summary>
	public class PpsDataSelectorItemTextBlock : TextBlock
	{
		public static readonly DependencyProperty SearchTextProperty =
			DependencyProperty.Register(nameof(SearchText), typeof(string), typeof(PpsDataSelectorItemTextBlock), new FrameworkPropertyMetadata(null, OnDataChanged));
		public static readonly DependencyProperty BaseTextProperty =
			DependencyProperty.Register(nameof(BaseText), typeof(string), typeof(PpsDataSelectorItemTextBlock), new FrameworkPropertyMetadata(null, OnDataChanged));

		public PpsDataSelectorItemTextBlock()
			: base()
		{
			// when true, we cannot use VisualTree, to get the ListBoxItem under Mouse
			IsHitTestVisible = false;
		} // ctor

		private static List<string> GetPositiveOperators(PpsDataFilterExpression filter)
		{
			// if operator is < > ! != it shouldn't be highlighted ( shouldn't be shown anyhow )
			if (filter is PpsDataFilterCompareExpression compare && (compare.Operator == PpsDataFilterCompareOperator.Contains ||
																	 compare.Operator == PpsDataFilterCompareOperator.Equal))
				return new List<string>() { compare.Value.ToString() };

			var ret = new List<string>();
			if (filter is PpsDataFilterLogicExpression logic)
				foreach (var sub in logic.Arguments)
					ret.AddRange(GetPositiveOperators(sub));

			return ret.Distinct().ToList();
		} // func GetOperators

		private static void OnDataChanged(DependencyObject source, DependencyPropertyChangedEventArgs e)
		{
			var textBlock = (PpsDataSelectorItemTextBlock)source;
			if (String.IsNullOrWhiteSpace(textBlock.BaseText))
				return;

			textBlock.Inlines.Clear();
			textBlock.Inlines.AddRange(HighlightSearch(textBlock.BaseText, GetPositiveOperators(PpsDataFilterExpression.Parse(textBlock.SearchText)), (t) => new Bold(new Italic(new Run(t)))));
		} // event OnDataChanged

		/// <summary>This function Highlights parts of a string.</summary>
		/// <param name="Text">Input text to format</param>
		/// <param name="Searchtext">Whitespace-separated list of keywords</param>
		/// <param name="Highlight">Function to Highlight, p.e. ''(t) => new Bold(new Italic(t))''</param>
		/// <returns>List of Inlines</returns>
		private static IEnumerable<Inline> HighlightSearch(string Text, List<string> Searchtext, Func<string, Inline> Highlight)
		{
			var result = new List<Inline>();

			// mark is the array of characters to highlight
			var mark = new bool[Text.Length];
			mark.Initialize();  // play save

			// finf every searchitem
			foreach (var high in Searchtext.Where((s) => s.Length > 1))
			{
				var start = Text.IndexOf(high, StringComparison.OrdinalIgnoreCase);
				// find every occurence
				while (start >= 0)
				{
					for (var j = 0; j < high.Length; j++)
						mark[start + j] = true;
					start = Text.IndexOf(high, start + 1, StringComparison.OrdinalIgnoreCase);
				}
			}

			if (!mark.Contains(true))
			{
				// nothing is highlighted - should only happen if nothing is searched for, thus showing the whole list
				result.Add(new Run(Text));
				return result;
			}

			var i = 0;
			// marks if the first character is highlighted or not
			var highlighting = mark[0];
			// create new output inlines
			while (i < mark.Length)
			{
				var stop = mark.ToList().IndexOf(!highlighting, i);
				if (stop < 0)
					stop = mark.Length;
				if (highlighting)
					result.Add(Highlight(Text.Substring(i, stop - i)));
				else
					result.Add(new Run(Text.Substring(i, stop - i)));
				highlighting = !highlighting;
				i = stop;
			}
			return result;
		} // func HighlightSearch

		/// <summary>Keywords to search for, separated by whitespace</summary>
		public string SearchText { get => (string)GetValue(SearchTextProperty); set => SetValue(SearchTextProperty, value); }

		/// <summary>Original Unformatted Text </summary>
		public string BaseText { get => (string)GetValue(BaseTextProperty); set => SetValue(BaseTextProperty, value); }
	} // class PpsDataSelectorItemTextBlock

	#endregion
}
