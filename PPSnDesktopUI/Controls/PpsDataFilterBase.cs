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
using System.Windows.Markup;
using System.Windows.Media;
using Microsoft.Scripting.Utils;
using TecWare.DE.Data;
using TecWare.PPSn.Data;

namespace TecWare.PPSn.Controls
{
	/// <summary>This Filterbox is used to filter a List</summary>
	[TemplatePart(Name = "PART_FilteredItemsListBox", Type = typeof(ListBox))]
	public abstract partial class PpsDataFilterBase : Selector
	{
		#region ---- Dependency Propteries-----------------------------------------------

		/// <summary>Input to the Control</summary>
		new public static readonly DependencyProperty ItemsSourceProperty = ItemsControl.ItemsSourceProperty.AddOwner(typeof(PpsDataFilterBase), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnItemsSourceChanged), new CoerceValueCallback(OnItemsSourceCoerceValue)));

		private static readonly DependencyPropertyKey FilteredItemsSourcePropertyKey = DependencyProperty.RegisterReadOnly(nameof(FilteredItemsSource), typeof(IEnumerable<IDataRow>), typeof(PpsDataFilterBase), new FrameworkPropertyMetadata(null));
		/// <summary>Filtered Output</summary>
		public static readonly DependencyProperty FilteredItemsSourceProperty = FilteredItemsSourcePropertyKey.DependencyProperty;

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
		/// <summary>The Value that is highlighted, but not committed</summary>
		public static readonly DependencyProperty PreSelectedValueProperty = DependencyProperty.Register(nameof(PreSelectedValue), typeof(object), typeof(PpsDataFilterBase));

		#endregion

		#region ---- Statics ------------------------------------------------------------

		/// <summary>Command for clearing the Filter</summary>
		public readonly static RoutedCommand ClearFilterCommand = new RoutedCommand("ClearFilter", typeof(PpsDataFilterBase));
		/// <summary>Command for clearing the Filter</summary>
		public readonly static RoutedCommand ClearValueCommand = new RoutedCommand("ClearValue", typeof(PpsDataFilterBase));

		#endregion

		#region ---- Fields -------------------------------------------------------------

		private const string FilteredItemsListBoxName = "PART_FilteredItemsListBox";
		/// <summary>The Listbox showing the filtered Elements</summary>
		protected ListBox filteredListBox;
		/// <summary>if the Mouse is over the Listbox</summary>
		protected bool hasMouseEnteredItemsList;

		#endregion

		#region ---- Events -------------------------------------------------------------

		/// <summary>Overridden to catch the filteredListBox</summary>
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
			SetValue(FilteredItemsSourcePropertyKey,
				expr == PpsDataFilterExpression.True
				? ItemsSource
				: ((IDataRowEnumerable)ItemsSource).ApplyFilter(expr)
			);
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
		/// <param name="d"></param>
		/// <param name="e"></param>
		private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsDataFilterBase)d).UpdateFilteredList();

		private static object OnItemsSourceCoerceValue(DependencyObject d, object baseValue)
		{
			if (baseValue is ICollectionViewFactory f)
				baseValue = f.CreateView();
			return baseValue;
		} // func OnItemsSourceCoerceValue

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

			var curPos = filteredListBox.SelectedIndex;
			var newPos = CalculateNewPos(curPos, itemsCount, direction);

			if (newPos != curPos)
				filteredListBox.SelectedIndex = newPos;

			Console.WriteLine(newPos);
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

		/// <summary>Should be overridden and returns if the Listbox is active (events should be attached)</summary>
		/// <returns></returns>
		public virtual bool IsFilteredListVisible()
			=> true;

		/// <summary>Executed if the ListShould be closed</summary>
		/// <param name="commit"></param>
		public virtual void HideFilteredList(bool commit)
		{
			if (commit)
				ApplySelectedItem();
		}

		internal void Items_CurrentChanged(object sender, EventArgs e)
		{
			//if (filteredListBox.Items.CurrentItem is IDataRow item)
				//filteredListBox.ScrollIntoView(item);
		} // event Items_CurrentChanged

		internal void SetAnchorItem()
		{
			if (!IsFilteredListVisible() || filteredListBox.Items == null || filteredListBox.Items.Count <= 0)
				return;

			var item = SelectedValue ?? filteredListBox.Items.GetItemAt(0);

			filteredListBox.Items.MoveCurrentTo(item);

			// clear selection?
			if (SelectedValue == null)
				filteredListBox.Items.MoveCurrentToPosition(-1);
		} // proc SetAnchorItem

		#region -- Evaluate MouseEvents -----------------------------------------------

		/// <summary>Used to close a ListBox, if available</summary>
		/// <param name="e"></param>
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

		/// <summary>Used to close a ListBox, if available</summary>
		/// <param name="e"></param>
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

		/// <summary>Used to pre-select items under the cursor</summary>
		/// <param name="e"></param>
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
		/// <summary>The Value that is highlighted, but not committed</summary>
		public object PreSelectedValue { get => GetValue(PreSelectedValueProperty); set => SetValue(PreSelectedValueProperty, value); }

		#endregion
	}

	/// <summary>Resembles a ComboBox, which is search-able</summary>
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
					}
					break;
				case Key.Home:
					if (e.KeyboardDevice.Modifiers == ModifierKeys.None)
					{
						e.Handled = true;
						if (IsDropDownOpen)
							Navigate(FocusNavigationDirection.First);
					}
					break;
				case Key.End:
					if (e.KeyboardDevice.Modifiers == ModifierKeys.None)
					{
						e.Handled = true;
						if (IsDropDownOpen)
							Navigate(FocusNavigationDirection.Last);
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

		/// <summary>Returns te state of IsDropDownOpen</summary>
		/// <returns></returns>
		public override bool IsFilteredListVisible()
		=> IsDropDownOpen;

		/// <summary>Closes the DropDown</summary>
		/// <param name="commit">true if the pre-selected value should be committed</param>
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

	/// <summary>This Control shows a List of its Items with an applied Filter</summary>
	public class PpsDataFilterList : PpsDataFilterBase
	{
		static PpsDataFilterList()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PpsDataFilterList), new FrameworkPropertyMetadata(typeof(PpsDataFilterList)));
		}

		/// <summary>Overridden to attach neccessary events</summary>
		public override void OnApplyTemplate()
		{
			base.OnApplyTemplate();

			filteredListBox.Items.CurrentChanged += Items_CurrentChanged;
			SetAnchorItem();
		}

		/// <summary>If an Item is clicked, it is committed</summary>
		/// <param name="e"></param>
		protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
		{
			base.OnMouseLeftButtonUp(e);
			base.ApplySelectedItem();
		}
	}

	/// <summary>This textBlock enables Highlighting of text</summary>
	public class PpsDataFilterItemTextBlock : TextBlock
	{
		/// <summary>The parts of text to highlight, separated by whitespace - may be overlapping</summary>
		public static readonly DependencyProperty SearchTextProperty =
			DependencyProperty.Register(nameof(SearchText), typeof(string), typeof(PpsDataFilterItemTextBlock), new FrameworkPropertyMetadata(null, OnDataChanged));
		/// <summary>Original Unformatted Text </summary>
		public static readonly DependencyProperty BaseTextProperty =
			DependencyProperty.Register(nameof(BaseText), typeof(string), typeof(PpsDataFilterItemTextBlock), new FrameworkPropertyMetadata(null, OnDataChanged));

		/// <summary>Sets IsHitTestVisible to false</summary>
		public PpsDataFilterItemTextBlock()
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
			var textBlock = (PpsDataFilterItemTextBlock)source;
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

		/// <summary>The parts of text to highlight, separated by whitespace - may be overlapping</summary>
		public string SearchText { get => (string)GetValue(SearchTextProperty); set => SetValue(SearchTextProperty, value); }

		/// <summary>Original Unformatted Text </summary>
		public string BaseText { get => (string)GetValue(BaseTextProperty); set => SetValue(BaseTextProperty, value); }
	} // class PpsDataFilterItemTextBlock
}
