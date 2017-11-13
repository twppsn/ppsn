using System;
using System.Collections.Generic;
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
	/// <summary>Extended ComboBox which enables searching, with Highlighting</summary>
	public class PpsDataSelector : Control
	{
		public static readonly DependencyProperty SelectedValueProperty = DependencyProperty.Register(nameof(SelectedValue), typeof(IDataRow), typeof(PpsDataSelector), new FrameworkPropertyMetadata((IDataRow)null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
		public static readonly DependencyProperty ItemsSourceProperty = ItemsControl.ItemsSourceProperty.AddOwner(typeof(PpsDataSelector), new FrameworkPropertyMetadata(OnItemsSourceChanged));

		private static readonly DependencyPropertyKey FilteredItemsSourcePropertyKey = DependencyProperty.RegisterReadOnly(nameof(FilteredItemsSource), typeof(IEnumerable<IDataRow>), typeof(PpsDataSelector), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty FilteredItemsSourceProperty = FilteredItemsSourcePropertyKey.DependencyProperty;

		public static readonly DependencyProperty FilterTextProperty = DependencyProperty.Register(nameof(FilterText), typeof(string), typeof(PpsDataSelector), new FrameworkPropertyMetadata(OnFilterTextChanged));
		public static readonly DependencyProperty ItemTemplateProperty = DependencyProperty.Register(nameof(ItemTemplate), typeof(DataTemplate), typeof(PpsDataSelector), new FrameworkPropertyMetadata((DataTemplate)null));
		public static readonly DependencyProperty SelectedValueTemplateProperty = DependencyProperty.Register(nameof(SelectedValueTemplate), typeof(DataTemplate), typeof(PpsDataSelector), new FrameworkPropertyMetadata((DataTemplate)null));
		public static readonly DependencyProperty IsDropDownOpenProperty = DependencyProperty.Register(nameof(IsDropDownOpen), typeof(bool), typeof(PpsDataSelector), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, new PropertyChangedCallback(OnIsDropDownOpenChanged)));
		public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.Register(nameof(IsReadOnly), typeof(bool), typeof(PpsDataSelector), new FrameworkPropertyMetadata(false));
		public static readonly DependencyProperty IsNullableProperty = DependencyProperty.Register(nameof(IsNullable), typeof(bool), typeof(PpsDataSelector), new FrameworkPropertyMetadata(true));

		public readonly static RoutedCommand ClearSelectionCommand = new RoutedCommand("ClearSelection", typeof(PpsDataSelector));

		private const string PopupTemplateName = "PART_DropDownPopup";
		private const string SearchBoxTemplateName = "PART_SearchTextBox";
		private const string ListBoxTemplateName = "PART_ItemsListBox";

		private TextBox searchTextBox;
		private ListBox itemsListBox;

		private bool hasMouseEnteredItemsList;
		private Point lastMousePosition = new Point();

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

		#region -- SelectedValue ------------------------------------------------------

		private void CommitValue(IDataRow value)
		{
			if (!object.Equals(value, SelectedValue))
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

		public void ClearFilter()
		{
			ClearFilterText();
			ClearSearchTextBox();
		} // proc Clearfilter

		public void ClearFilterText()
			=> FilterText = null;

		/// <summary>SearchBox.Text binding is OneWayToSource</summary>
		public void ClearSearchTextBox()
			=> searchTextBox.Clear();

		private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsDataSelector)d).UpdateFilteredList();

		private static void OnFilterTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsDataSelector)d).UpdateFilteredList();

		private void UpdateFilteredList()
		{
			if (ItemsSource == null)
				return;

			var expr = String.IsNullOrWhiteSpace(FilterText) ? PpsDataFilterExpression.True : PpsDataFilterExpression.Parse(FilterText);
			SetValue(FilteredItemsSourcePropertyKey,
				expr == PpsDataFilterExpression.True
				? ItemsSource
				: ItemsSource.ApplyFilter(expr)
			);
		} // proc UpdateFilteredList

		#endregion

		#region -- DropDownPopup ------------------------------------------------------

		// Handle IsDropDownOpenProperty when triggered from StaysOpen=False
		private void OnPopupClosed(object source, EventArgs e)
			=> CloseDropDown(false);

		private static void OnIsDropDownOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var selector = (PpsDataSelector)d;
			var isopen = (bool)e.NewValue;

			selector.hasMouseEnteredItemsList = false;

			if (isopen)
			{
				selector.Items.CurrentChanged += selector.Items_CurrentChanged;
				selector.SetAnchorItem();
				Mouse.Capture(selector, CaptureMode.SubTree);
			}
			else
			{
				selector.Items.CurrentChanged -= selector.Items_CurrentChanged;
				// leave clean
				selector.ClearFilter();

				// Otherwise focus is in the disposed hWnd
				if (selector.IsKeyboardFocusWithin)
					selector.Focus();

				// Release
				if (Mouse.Captured == selector)
					Mouse.Capture(null);
			}
		} // delegate OnIsDropDownOpenChanged

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
			var items = Items.Count;
			if (items == 0)
				return;

			var curIndex = -1;
			if (SelectedValue != null)
			{
				curIndex = Items.IndexOf(SelectedValue);
				if (curIndex < 0)
					return;
			}

			var newIndex = CalculateNewPos(curIndex, items, direction);

			if (newIndex != curIndex)
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

		private ItemCollection Items => itemsListBox.Items;

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
					if(IsClearSelectionAllowed)
					{
						e.Handled = true;
						ClearSelection();
					}
					break;
			}
		} // proc KeyDownHandler

		#endregion

		/// <summary>List of Data to select from</summary>	
		public IDataRowEnumerable ItemsSource { get => (IDataRowEnumerable)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

		/// <summary>Access to the filtered list.</summary>
		public IEnumerable<IDataRow> FilteredItemsSource => (IEnumerable<IDataRow>)GetValue(FilteredItemsSourceProperty);

		/// <summary>Datatemplate for FilteredItems</summary>
		public DataTemplate ItemTemplate { get => (DataTemplate)GetValue(ItemTemplateProperty); set => SetValue(ItemTemplateProperty, value); }

		/// <summary>Actual Value</summary>
		public IDataRow SelectedValue { get => (IDataRow)(GetValue(SelectedValueProperty)); set => SetValue(SelectedValueProperty, value); }

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

		private static List<string> GetOperators(PpsDataFilterExpression filter)
		{

			if (filter is PpsDataFilterCompareExpression compare)
				return new List<string>() { compare.Value.ToString() };

			var ret = new List<string>();
			if (filter is PpsDataFilterLogicExpression logic)
				foreach (var sub in logic.Arguments)
					ret.AddRange(GetOperators(sub));

			return ret.Distinct().ToList();
		} // func GetOperators

		private static void OnDataChanged(DependencyObject source, DependencyPropertyChangedEventArgs e)
		{
			var textBlock = (PpsDataSelectorItemTextBlock)source;
			if (String.IsNullOrWhiteSpace(textBlock.BaseText))
				return;

			textBlock.Inlines.Clear();
			textBlock.Inlines.AddRange(HighlightSearch(textBlock.BaseText, GetOperators(PpsDataFilterExpression.Parse(textBlock.SearchText)), (t) => new Bold(new Italic(t))));
		} // event OnDataChanged

		/// <summary>This function Highlights parts of a string.</summary>
		/// <param name="Text">Input text to format</param>
		/// <param name="Searchtext">Whitespace-separated list of keywords</param>
		/// <param name="Highlight">Function to Highlight, p.e. ''(t) => new Bold(new Italic(t))''</param>
		/// <returns>List of Inlines</returns>
		private static IEnumerable<Inline> HighlightSearch(string Text, List<string> Searchtext, Func<Inline, Inline> Highlight)
		{
			var result = new List<Inline>();

			if (Searchtext.Count == 0)
			{
				// no searchstring - the whole Text is returned unaltered
				result.Add(new Run(Text));
				return result;
			}

			//var searchtexts = Searchtext.Trim(' ').Split(' ');
			foreach (var st in Searchtext)
				if (!String.IsNullOrEmpty(st))
				{
					// iterate through all search filters
					var idx = Text.IndexOf(st, StringComparison.CurrentCultureIgnoreCase);
					if (idx >= 0)
					{
						// recurse in the part before and after the found text and concatenate the searchstring bold
						result.AddRange(HighlightSearch(Text.Substring(0, idx), Searchtext, Highlight));
						result.Add(Highlight(new Run(Text.Substring(idx, st.Length))));
						result.AddRange(HighlightSearch(Text.Substring(idx + st.Length), Searchtext, Highlight));
						return result;
					}
				}

			// end of recursion - no search string found in substring
			result.Add(new Run(Text));
			return result;
		} // func HighlightSearch

		/// <summary>Keywords to search for, separated by whitespace</summary>
		public string SearchText { get => (string)GetValue(SearchTextProperty); set => SetValue(SearchTextProperty, value); }

		/// <summary>Original Unformatted Text </summary>
		public String BaseText { get => (string)GetValue(BaseTextProperty); set => SetValue(BaseTextProperty, value); }

	} // class SearchHighlightTextBox

	#endregion
}