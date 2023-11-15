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
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Threading;
using TecWare.DE.Data;
using TecWare.DE.Stuff;
using TecWare.PPSn.Core.Data;
using TecWare.PPSn.Data;
using TecWare.PPSn.UI;

namespace TecWare.PPSn.Controls
{
	#region -- class PpsComboBoxItem --------------------------------------------------

	/// <summary>Item container for the data list.</summary>
	public class PpsComboBoxItem : ComboBoxItem
	{
		/// <summary></summary>
		/// <param name="e"></param>
		protected override void OnMouseEnter(MouseEventArgs e)
		{
			if (ParentComboBox != null && ParentComboBox.IsFilterable)
			{
				var focusAble = Focusable;
				try
				{
					// OnMouseEnter will set focus by default, mark the container as not focusable
					Focusable = false;
					base.OnMouseEnter(e);
				}
				finally
				{
					Focusable = focusAble;
				}
			}
			else
				base.OnMouseEnter(e);
		} // proc OnMouseEnter

		internal PpsComboBox ParentComboBox => ItemsControl.ItemsControlFromItemContainer(this) as PpsComboBox;

		static PpsComboBoxItem()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PpsComboBoxItem), new FrameworkPropertyMetadata(typeof(PpsComboBoxItem)));
		} // sctor
	} // class PpsComboBoxItem

	#endregion

	#region -- class PpsComboBox ------------------------------------------------------

	/// <summary></summary>
	[
		TemplatePart(Name = FilterBoxTemplateName, Type = typeof(PpsTextBox)),
		TemplatePart(Name = ContentPresenterTemplateName, Type = typeof(ContentPresenter))
	]
	public class PpsComboBox : ComboBox, IPpsNullableControl
	{
		/// <summary>Template name for the filter box</summary>
		public const string FilterBoxTemplateName = "PART_FilterBox";
		/// <summary>Template name for the contentpresenter</summary>
		public const string ContentPresenterTemplateName = "PART_ContentPresenter";

		#region -- UserFilterText - Property ------------------------------------------

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public static readonly DependencyProperty FilterExpressionProperty = DependencyProperty.Register(nameof(FilterExpression), typeof(PpsDataFilterExpression), typeof(PpsComboBox), new FrameworkPropertyMetadata(new PropertyChangedCallback(OnFilterExpressionChanged), new CoerceValueCallback(OnCoerceFilterExpression)));
		public static readonly DependencyProperty UserFilterTextProperty = DependencyProperty.Register(nameof(UserFilterText), typeof(string), typeof(PpsComboBox), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnUserFilterTextChanged)));

		private static readonly DependencyPropertyKey isFilteredPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsFiltered), typeof(bool), typeof(PpsComboBox), new FrameworkPropertyMetadata(BooleanBox.False));
		public static readonly DependencyProperty IsFilteredProperty = isFilteredPropertyKey.DependencyProperty;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		private static object OnCoerceFilterExpression(DependencyObject d, object baseValue)
			=> baseValue is PpsDataFilterExpression expr ? expr.Reduce() : PpsDataFilterExpression.True;

		private static void OnFilterExpressionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsComboBox)d).UpdateFilterExpression();

		private static void OnUserFilterTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsComboBox)d).UpdateFilterExpression();

		/// <summary>Convert filter-text to expresion</summary>
		/// <returns></returns>
		protected virtual PpsDataFilterExpression GetUserFilterExpression()
			=> PpsDataFilterExpression.Parse(UserFilterText);

		/// <summary>Refresh filter</summary>
		protected void UpdateFilterExpression()
		{
			if (filterView != null && AllowFilter)
			{
				var expr = PpsDataFilterExpression.Combine(FilterExpression, GetUserFilterExpression());
				filterView.FilterExpression = expr;
				SetValue(isFilteredPropertyKey, expr == PpsDataFilterExpression.True);
			}
			else
			{
				SetValue(isFilteredPropertyKey, false);
				if (filterView != null)
					filterView.FilterExpression = PpsDataFilterExpression.True;
			}
		} // proc UpdateFilterExpression

		/// <summary>Static filter for the collection view.</summary>
		public PpsDataFilterExpression FilterExpression { get => (PpsDataFilterExpression)GetValue(FilterExpressionProperty); set => SetValue(FilterExpressionProperty, value); }
		/// <summary>Filter the collection view, this property is used by the template. Type is string because we do not want to change the source e.g. spaces. </summary>
		public string UserFilterText { get => (string)GetValue(UserFilterTextProperty); set => SetValue(UserFilterTextProperty, value); }
		/// <summary>Is the view filterd, currently.</summary>
		public bool IsFiltered => BooleanBox.GetBool(GetValue(IsFilteredProperty));

		#endregion

		#region -- AllowFilter, IsFilterable - Property -------------------------------

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public static readonly DependencyProperty AllowFilterProperty = PpsDataListBox.AllowFilterProperty.AddOwner(typeof(PpsComboBox), new FrameworkPropertyMetadata(BooleanBox.True, new PropertyChangedCallback(OnAllowFilterChanged)));

		private static readonly DependencyPropertyKey isFilterablePropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsFilterable), typeof(bool), typeof(PpsComboBox), new FrameworkPropertyMetadata(BooleanBox.True));
		public static readonly DependencyProperty IsFilterableProperty = isFilterablePropertyKey.DependencyProperty;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		private void UpdateFilterable()
			=> SetValue(isFilterablePropertyKey, BooleanBox.GetObject(filterBox != null && filterView != null && AllowFilter));

		private static void OnAllowFilterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsComboBox)d).UpdateFilterable();

		/// <summary>Allow filter</summary>
		public bool AllowFilter { get => BooleanBox.GetBool(GetValue(AllowFilterProperty)); set => SetValue(AllowFilterProperty, BooleanBox.GetObject(value)); }

		/// <summary>Is filterable</summary>
		public bool IsFilterable => BooleanBox.GetBool(GetValue(IsFilterableProperty));

		#endregion

		#region -- IsNullable - property ----------------------------------------------

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public static readonly DependencyProperty IsNullableProperty = PpsTextBox.IsNullableProperty.AddOwner(typeof(PpsComboBox));
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		/// <summary>Is this value nullable.</summary>
		public bool IsNullable { get => BooleanBox.GetBool(GetValue(IsNullableProperty)); set => SetValue(IsNullableProperty, BooleanBox.GetObject(value)); }

		#endregion

		#region -- HintLabel - property -----------------------------------------------

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public static readonly DependencyProperty HintLabelTextProperty = DependencyProperty.Register(nameof(HintLabelText), typeof(string), typeof(PpsComboBox), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnHintLabelTextChanged)));
		private static readonly DependencyPropertyKey hasHintLabelPropertyKey = DependencyProperty.RegisterReadOnly(nameof(HasHintLabel), typeof(bool), typeof(PpsComboBox), new FrameworkPropertyMetadata(BooleanBox.False));
		public static readonly DependencyProperty HasHintLabelProperty = hasHintLabelPropertyKey.DependencyProperty;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		private static void OnHintLabelTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var comboBox = (PpsComboBox)d;
			if (e.NewValue is string label && !String.IsNullOrEmpty(label))
				comboBox.HasHintLabel = true;
			else
				comboBox.HasHintLabel = false;
		} // proc OnHintLabelTextChanged

		/// <summary>Optional text to show, when no value is selected</summary>
		public string HintLabelText { get => (string)(GetValue(HintLabelTextProperty)); set => SetValue(HintLabelTextProperty, value); }
		/// <summary>Has the Combobox a HintLabel?</summary>
		public bool HasHintLabel { get => BooleanBox.GetBool(GetValue(HasHintLabelProperty)); private set => SetValue(hasHintLabelPropertyKey, BooleanBox.GetObject(value)); }

		#endregion

		#region -- IsClearButtonVisible - property ------------------------------------

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public static readonly DependencyProperty IsClearButtonVisibleProperty = DependencyProperty.Register(nameof(IsClearButtonVisible), typeof(bool), typeof(PpsComboBox), new FrameworkPropertyMetadata(BooleanBox.False));
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		/// <summary>Is the ClearButton visible?</summary>
		public bool IsClearButtonVisible { get => BooleanBox.GetBool(GetValue(IsClearButtonVisibleProperty)); set => SetValue(IsClearButtonVisibleProperty, BooleanBox.GetObject(value)); }

		#endregion

		#region -- GeometryName - property --------------------------------------------

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public static readonly DependencyProperty GeometryNameProperty = PpsGeometryImage.GeometryNameProperty.AddOwner(typeof(PpsComboBox));
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		/// <summary>The property defines the resource to be loaded to show the dropdown-symbol.</summary>
		public string GeometryName { get => (string)GetValue(GeometryNameProperty); set => SetValue(GeometryNameProperty, value); }

		#endregion

		private IPpsDataRowViewFilter filterView = null;
		private PpsTextBox filterBox;

		private SelectedBaseValue selectedValueHelper;

		public PpsComboBox()
		{
			selectedValueHelper = new SelectedDefaultValue(this);
		} // ctor

		bool IPpsNullableControl.CanClear => IsEnabled && IsNullable && SelectedIndex >= 0;

		/// <summary>Clear index</summary>
		public void Clear()
		{
			SelectedIndex = -1;
		} // proc ClearValue

		#region -- ItemsSource --------------------------------------------------------

		private static object ItemsSourceCoerceValue(DependencyObject d, object baseValue)
		{
			var cbb = (PpsComboBox)d;

			// update selected value helper
			if (baseValue is PpsDataQueryView query && query.PrimaryKey != null)
			{
				cbb.UpdateSelectedValueHelper(new SelectedQueryValue(cbb, query));
				cbb.SelectedValuePath = query.PrimaryKey;
			}
			else if (baseValue is IPpsLiveTableViewBase liveQuery && liveQuery.Type.IsSinglePrimaryKey)
			{
				cbb.UpdateSelectedValueHelper(new SelectedLiveValue(cbb, liveQuery.Type));
				cbb.SelectedValuePath = liveQuery.Type.PrimaryKey.PropertyName;
			}
			else
			{
				cbb.UpdateSelectedValueHelper(null);
				cbb.SelectedValuePath = null;
			}

			// create a compatible view
			var view = CollectionViewSource.GetDefaultView(baseValue);
			if (view is PpsDataRowEnumerableCollectionView)
				return baseValue;
			else if (view is IPpsDataRowViewFilter) // filterable view
			{
				if (baseValue is ICollectionViewFactory factory)
					return factory.CreateView();
				else if (view is ICollectionView currentView)
					return new CollectionViewSource { Source = currentView.SourceCollection }.View;
				else
					return baseValue;
			}
			else
				return baseValue;
		} // func ItemsSourceCoerceValue

		/// <summary></summary>
		/// <param name="oldValue"></param>
		/// <param name="newValue"></param>
		protected override void OnItemsSourceChanged(IEnumerable oldValue, IEnumerable newValue)
		{
			base.OnItemsSourceChanged(oldValue, newValue);

			// get view
			var view = CollectionViewSource.GetDefaultView(newValue);
			filterView = view as IPpsDataRowViewFilter;

			UpdateFilterExpression();
			UpdateFilterable();
		} // proc OnItemsSourceChanged

		/// <summary></summary>
		/// <param name="e"></param>
		protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
		{
			if (!IsFilterable)
				base.OnItemsChanged(e); // do not change any selection
			else if (IsDropDownOpen) // enforce focus on first
			{
				var itemContainerGenerator = ItemContainerGenerator;
				if (itemContainerGenerator != null)
					itemContainerGenerator.StatusChanged += ItemContainerGenerator_StatusChanged;

			}
			else if (e.Action == NotifyCollectionChangedAction.Reset && SelectedValue != null && SelectedIndex == -1) // set selection
				base.OnItemsChanged(e);
		} // proc OnItemsChanged

		private void ItemContainerGenerator_StatusChanged(object sender, EventArgs e)
		{
			var itemContainerGenerator = ItemContainerGenerator;
			if (itemContainerGenerator.Status == GeneratorStatus.ContainersGenerated)
			{
				itemContainerGenerator.StatusChanged -= ItemContainerGenerator_StatusChanged;
				var container = itemContainerGenerator.ContainerFromItem(SelectedItem);
				if (container is PpsComboBoxItem ci)
					HighlightItem(ci);
				else
				{
					if (itemContainerGenerator.Items.Count > 0)
						HighlightItem((itemContainerGenerator.ContainerFromIndex(0) as PpsComboBoxItem));
				}
			}
		} // event ItemContainerGenerator_StatusChanged

		private void HighlightItem(PpsComboBoxItem ci)
		{
			if (ci != null && !ci.IsHighlighted)
			{
				// HighlightedInfo = ItemInfoFromContainer(item);
				highlightedInfoPropertyInfo.SetValue(this, itemInfoFromContainerMethodInfo.Invoke(this, new object[] { ci }));
			}
		} // proc HighlightItem

		#endregion

		#region -- SelectedValue - property -------------------------------------------

		#region -- class SelectedBaseValue --------------------------------------------

		private abstract class SelectedBaseValue
		{
			private readonly PpsComboBox comboBox;

			protected SelectedBaseValue(PpsComboBox comboBox)
			{
				this.comboBox = comboBox ?? throw new ArgumentNullException(nameof(comboBox));
			} // ctor

			public virtual object Coerce(object value)
				=> value;

			public virtual void Set(DependencyPropertyChangedEventArgs e)
			{
			}

			public bool IsSelectionPending;
			public abstract object Value { get; }

			public PpsComboBox ComboBox => comboBox;
		} // class SelectedBaseValue

		#endregion

		#region -- class SelectedPendingValue -----------------------------------------

		private sealed class SelectedPendingValue : SelectedBaseValue
		{
			private object pendingValue;

			public SelectedPendingValue(PpsComboBox comboBox, object pendingValue)
				: base(comboBox)
			{
				this.pendingValue = pendingValue ?? throw new ArgumentNullException(nameof(pendingValue));
			} // ctor

			public override void Set(DependencyPropertyChangedEventArgs e)
				=> pendingValue = e.NewValue;

			public override object Value => pendingValue;
		} // class SelectedPendingValue

		#endregion

		#region -- class SelectedDefaultValue -----------------------------------------

		private sealed class SelectedDefaultValue : SelectedBaseValue
		{
			public SelectedDefaultValue(PpsComboBox comboBox)
				: base(comboBox)
			{
			} // ctor

			public override object Coerce(object value)
				=> SelectedValueProperty.GetMetadata(typeof(ComboBox)).CoerceValueCallback(ComboBox, value);

			public override void Set(DependencyPropertyChangedEventArgs e)
				=> SelectedValueProperty.GetMetadata(typeof(ComboBox)).PropertyChangedCallback(ComboBox, e);

			public override object Value => ComboBox.SelectedValue;
		} // class SelectedDefaultValue

		#endregion

		#region -- class SelectedLiveValue --------------------------------------------

		private sealed class SelectedLiveValue : SelectedBaseValue
		{
			private readonly PpsLiveDataRowType type;
			private IPpsLiveRowView row;

			public SelectedLiveValue(PpsComboBox comboBox, PpsLiveDataRowType type)
				: base(comboBox)
			{
				this.type = type ?? throw new ArgumentNullException(nameof(type));
			} // ctor

			private void Row_PropertyChanged(object sender, PropertyChangedEventArgs e)
			{
				if (e.PropertyName == nameof(IPpsLiveRowView.Row))
					ComboBox.UpdateSelectedValueItem(row.Row);
			} // event Row_PropertyChanged

			public override object Coerce(object value)
			{
				if (value == null)
					return null;
				return Procs.ChangeType(value, type.PrimaryKey.DataType);
			} // func Coerce

			public override void Set(DependencyPropertyChangedEventArgs e)
			{
				var data = ComboBox.GetControlService<PpsLiveData>(true);
				if (e.NewValue == null)
					ComboBox.UpdateSelectedValueItem(null);
				else
				{
					object keyValue = e.NewValue;
					if (e.NewValue.GetType() == type.RowType)
						keyValue = type.RowType.GetProperty(type.PrimaryKey.PropertyName).GetValue(keyValue);

					if (row == null)
					{
						row = type.CreateRow(data, keyValue);
						if (row.Row != null)
							ComboBox.UpdateSelectedValueItem(row.Row);

						row.PropertyChanged += Row_PropertyChanged;
					}
					else
						row.SetKey(keyValue);
				}
			} // proc Set

			public override object Value => row?.Key.FirstOrDefault();
		} // class SelectedLiveValue

		#endregion

		#region -- class SelectedQueryValue -------------------------------------------

		private sealed class SelectedQueryValue : SelectedBaseValue
		{
			private readonly string viewName;
			private readonly string primaryKey;
			private readonly PpsDataFilterExpression filter;
			private readonly PpsDataColumnExpression[] columns;

			private object currentValue;

			public SelectedQueryValue(PpsComboBox comboBox, PpsDataQueryView query)
				: base(comboBox)
			{
				if (query == null)
					throw new ArgumentNullException(nameof(query));

				// copy usefull attributes
				viewName = query.ViewName;
				primaryKey = query.PrimaryKey ?? throw new ArgumentNullException(nameof(PpsDataQueryView.PrimaryKey));
				filter = query.FilterCore;
				columns = query.ColumnsCore;
			} // ctor

			private async Task GetRowAsync(IPpsShell shell, PpsDataQuery query)
			{
				var row = await Task.Run(() => shell.GetViewData(query).FirstOrDefault());
				ComboBox.UpdateSelectedValueItem(row);
			} // func GeRowAsync

			public override object Coerce(object value)
				=> base.Coerce(value);

			public override void Set(DependencyPropertyChangedEventArgs e)
			{
				currentValue = e.NewValue;

				var qry = new PpsDataQuery(viewName)
				{
					Filter = PpsDataFilterExpression.Combine(filter, PpsDataFilterExpression.Compare(primaryKey, PpsDataFilterCompareOperator.Equal, currentValue)),
					Columns = columns,
					Count = 1
				};

				GetRowAsync(ComboBox.GetShell(), qry).Spawn();
			} // proc Set

			public override object Value => currentValue;
		} // class SelectedQueryValue

		#endregion


		private static readonly DependencyPropertyKey SelectedValueItemPropertyKey = DependencyProperty.RegisterReadOnly(nameof(SelectedValueItem), typeof(object), typeof(PpsComboBox), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty SelectedValueItemProperty = SelectedValueItemPropertyKey.DependencyProperty;

		private void UpdateSelectedValueHelper(SelectedBaseValue newSelectedValue)
		{
			var oldSelectedValueHelper = selectedValueHelper;
			selectedValueHelper = newSelectedValue ?? new SelectedDefaultValue(this);
			if (oldSelectedValueHelper != null)
				selectedValueHelper.Set(new DependencyPropertyChangedEventArgs(SelectedValueProperty, null, oldSelectedValueHelper.Value));
		} // proc UpdateSelectedValueHelper

		protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
		{
			if (e.Property == SelectedItemProperty && selectedValueHelper is SelectedDefaultValue)
				UpdateSelectedValueItem(e.NewValue);
				
			base.OnPropertyChanged(e);
		} // proc OnPropertyChanged

		private void UpdateSelectedValueItem(object value)
			=> SetValue(SelectedValueItemPropertyKey, value);

		public object SelectedValueItem => GetValue(SelectedValueItemProperty);

		private static object OnSelectedValueCoerceValue(DependencyObject d, object baseValue)
		{
			var cbb = (PpsComboBox)d;
			return cbb.selectedValueHelper.Coerce(baseValue);
		} //  func OnSelectedValueCoerceValue

		private static void OnSelectedValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var cbb = (PpsComboBox)d;
			if (cbb.ItemsSource is null && cbb.selectedValueHelper is SelectedDefaultValue)
				cbb.UpdateSelectedValueHelper(new SelectedPendingValue(cbb, e.NewValue));
			else
				cbb.selectedValueHelper.Set(e);
		} // proc OnSelectedValueChanged

		#endregion

		/// <summary></summary>
		public override void OnApplyTemplate()
		{
			base.OnApplyTemplate();

			filterBox = (PpsTextBox)GetTemplateChild(FilterBoxTemplateName);
			if (filterBox != null)
				filterBox.PreviewKeyDown += FilterBox_PreviewKeyDown;

			UpdateFilterable();
		} // proc OnApplyTemplate

		private void FilterBox_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (filterBox.IsKeyboardFocusWithin)
			{
				switch (e.Key)
				{
					case Key.Up:
					case Key.Down:
						OnKeyDown(e); // will change focus
						filterBox.Focus(); // enforce focus
						break;
					case Key.Return:
					case Key.Escape:
						OnKeyDown(e);
						break;
				}
			}
		} // event FilterBox_PreviewKeyDown

		/// <summary></summary>
		/// <param name="e"></param>
		protected override void OnDropDownOpened(EventArgs e)
		{
			base.OnDropDownOpened(e);
			if (IsFilterable)
			{
				Focus();
				Dispatcher.BeginInvoke(DispatcherPriority.Send, new Func<bool>(filterBox.Focus));
			}
		} // proc OnDropDownOpened

		/// <summary>Clear filter on close</summary>
		/// <param name="e"></param>
		protected override void OnDropDownClosed(EventArgs e)
		{
			UserFilterText = String.Empty; // clear filter
			base.OnDropDownClosed(e);
		} // proc OnDropDownClosed

		/// <summary></summary>
		/// <param name="e"></param>
		protected override void OnIsKeyboardFocusWithinChanged(DependencyPropertyChangedEventArgs e)
		{
			base.OnIsKeyboardFocusWithinChanged(e);
			if (IsDropDownOpen && IsFilterable)
				filterBox.Focus();
		} // proc OnIsKeyboardFocusWithinChanged

		#region -- Item Container -----------------------------------------------------

		/// <summary></summary>
		/// <returns></returns>
		protected override DependencyObject GetContainerForItemOverride()
			=> new PpsComboBoxItem();

		/// <summary></summary>
		/// <param name="item"></param>
		/// <returns></returns>
		protected override bool IsItemItsOwnContainerOverride(object item)
			=> item is PpsComboBoxItem;

		/// <summary></summary>
		/// <param name="element"></param>
		/// <param name="item"></param>
		protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
		{
			base.PrepareContainerForItemOverride(element, item);

			if (element is PpsComboBoxItem container)
			{
				if (container.ContentTemplate == null
					&& container.ContentTemplateSelector == null
					&& container.ContentStringFormat == null)
				{
					container.ContentTemplateSelector = null; // PpsWpfShell.DataTemplateSelector;
				}
			}
		} // proc PrepareContainerForItemOverride

		#endregion

		private static readonly PropertyInfo highlightedInfoPropertyInfo;
		private static readonly MethodInfo itemInfoFromContainerMethodInfo;

		static PpsComboBox()
		{
			highlightedInfoPropertyInfo = typeof(ComboBox).GetProperty("HighlightedInfo", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly) ?? throw new ArgumentNullException("ComboBox.HighlightedInfo");
			itemInfoFromContainerMethodInfo = typeof(ItemsControl).GetMethod("ItemInfoFromContainer", BindingFlags.NonPublic | BindingFlags.Instance) ?? throw new ArgumentNullException("ItemsControl.ItemInfoFromContainer");

			DefaultStyleKeyProperty.OverrideMetadata(typeof(PpsComboBox), new FrameworkPropertyMetadata(typeof(PpsComboBox)));

			SelectedValueProperty.OverrideMetadata(typeof(PpsComboBox), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnSelectedValueChanged), new CoerceValueCallback(OnSelectedValueCoerceValue)));


			// erzeugt neue collectionviews, da durch den filter ein manipulation eintritt
			ItemsSourceProperty.OverrideMetadata(typeof(PpsComboBox), new FrameworkPropertyMetadata(null, null, new CoerceValueCallback(ItemsSourceCoerceValue)));
			// muss ausgeschalten werden, da sonst bei nicht default views aktiv
			IsSynchronizedWithCurrentItemProperty.OverrideMetadata(typeof(PpsComboBox), new FrameworkPropertyMetadata(false));

			PpsControlCommands.RegisterClearCommand(typeof(PpsComboBox));
		}
	} // class PpsComboBox

	#endregion

	#region -- class PpsComboBoxToggleButton ------------------------------------------

	/// <summary></summary>
	public class PpsComboBoxToggleButton : ToggleButton
	{
		/// <summary>The name of the resource</summary>
		public static readonly DependencyProperty GeometryNameProperty = PpsGeometryImage.GeometryNameProperty.AddOwner(typeof(PpsComboBoxToggleButton));

		/// <summary>The property defines the resource to be loaded to show the dropdown-symbol.</summary>
		public string GeometryName { get => (string)GetValue(GeometryNameProperty); set => SetValue(GeometryNameProperty, value); }

		static PpsComboBoxToggleButton()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PpsComboBoxToggleButton), new FrameworkPropertyMetadata(typeof(PpsComboBoxToggleButton)));
		}
	} // class PpsComboBoxToggleButton

	#endregion

	#region -- class PpsComboBoxTemplate ----------------------------------------------

	public sealed class PpsComboBoxTemplateMarkup : MarkupExtension
	{
		public override object ProvideValue(IServiceProvider serviceProvider)
		{
			return new PpsComboBoxTemplateSelector
			{
				SelectedTemplate = SelectedTemplate,
				SelectedTemplateSelector = SelectedTemplateSelector,
				DropDownItemTemplate = DropDownItemTemplate,
				DropDownItemTemplateSelector = DropDownItemTemplateSelector
			};
		} // func ProvideValue

		public DataTemplate SelectedTemplate { get; set; } = null;
		public DataTemplateSelector SelectedTemplateSelector { get; set; } = null;
		public DataTemplate DropDownItemTemplate { get; set; } = null;
		public DataTemplateSelector DropDownItemTemplateSelector { get; set; } = null;
	} // class PpsComboBoxTemplateMarkup

	public class PpsComboBoxTemplateSelector : DataTemplateSelector
	{
		public override DataTemplate SelectTemplate(object item, DependencyObject container)
		{
			var isDropItem = container.GetVisualParent<ComboBoxItem>() != null;
			return isDropItem
				? (DropDownItemTemplate ?? DropDownItemTemplateSelector?.SelectTemplate(item, container))
				: (SelectedTemplate ?? SelectedTemplateSelector?.SelectTemplate(item, container));
		} // func SelectTemplate

		public DataTemplate SelectedTemplate { get; set; } = null;
		public DataTemplateSelector SelectedTemplateSelector { get; set; } = null;
		public DataTemplate DropDownItemTemplate { get; set; } = null;
		public DataTemplateSelector DropDownItemTemplateSelector { get; set; } = null;
	} // class PpsComboBoxTemplateSelector


	#endregion

}
