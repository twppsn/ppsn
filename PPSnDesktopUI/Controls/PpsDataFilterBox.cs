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
using TecWare.PPSn.Data;

namespace TecWare.PPSn.Controls
{
	/// <summary>This Filterbox is used to filter a List</summary>
	public class PpsDataFilterBox : Control
	{
		public static readonly DependencyProperty ItemsSourceProperty = ItemsControl.ItemsSourceProperty.AddOwner(typeof(PpsDataFilterBox), new FrameworkPropertyMetadata(OnItemsSourceChanged));
		public static readonly DependencyProperty FilteredItemsSourceProperty = DependencyProperty.Register(nameof(FilteredItemsSource), typeof(IDataRowEnumerable), typeof(PpsDataFilterBox));

		public static readonly DependencyProperty FilterTextProperty = DependencyProperty.Register(nameof(FilterText), typeof(string), typeof(PpsDataFilterBox), new FrameworkPropertyMetadata(OnFilterTextChanged));
		public static readonly DependencyProperty SelectedValueProperty = DependencyProperty.Register(nameof(SelectedValue), typeof(object), typeof(PpsDataFilterBox));
		public static readonly DependencyProperty SelectedValueTemplateProperty = DependencyProperty.Register(nameof(SelectedValueTemplate), typeof(DataTemplate), typeof(PpsDataFilterBox), new FrameworkPropertyMetadata((DataTemplate)null));
		public static readonly DependencyProperty ItemTemplateProperty = DependencyProperty.Register(nameof(ItemTemplate), typeof(DataTemplate), typeof(PpsDataFilterBox), new FrameworkPropertyMetadata((DataTemplate)null));
		public static readonly DependencyProperty ItemContainerStyleProperty = DependencyProperty.Register(nameof(ItemContainerStyle), typeof(Style), typeof(PpsDataFilterBox), new FrameworkPropertyMetadata((Style)null));
		public static readonly DependencyProperty ListBoxStyleProperty = DependencyProperty.Register(nameof(ListBoxStyle), typeof(Style), typeof(PpsDataFilterBox), new FrameworkPropertyMetadata((Style)null));
		public static readonly DependencyProperty IsNullableProperty = DependencyProperty.Register(nameof(IsNullable), typeof(bool), typeof(PpsDataFilterBox));

		private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsDataFilterBox)d).UpdateFilteredList();
		private static void OnFilterTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsDataFilterBox)d).UpdateFilteredList();
		
		private void UpdateFilteredList()
		{
			if (ItemsSource == null)
				return;
			
			var expr = String.IsNullOrWhiteSpace(FilterText) ? PpsDataFilterExpression.True : PpsDataFilterExpression.Parse(FilterText);
			FilteredItemsSource = (expr == PpsDataFilterExpression.True) ? ItemsSource : ItemsSource.ApplyFilter(expr);
		} // proc UpdateFilteredList

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
	}
}
