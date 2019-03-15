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
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;
using TecWare.PPSn.UI;

namespace TecWare.PPSn.Controls
{
	#region -- class PpsDataListItem --------------------------------------------------

	/// <summary>Item container for the data list.</summary>
	public class PpsDataListItem : ListBoxItem
	{
		#region -- Commands - Property ------------------------------------------------

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public static readonly DependencyProperty CommandsProperty = DependencyProperty.Register(nameof(Commands), typeof(PpsUICommandCollection), typeof(PpsDataListItem), new FrameworkPropertyMetadata(new PropertyChangedCallback(OnCommandsChanged), new CoerceValueCallback(OnCoerceCommands)));

		private static readonly DependencyPropertyKey hasCommandsPropertyKey = DependencyProperty.RegisterReadOnly(nameof(HasCommands), typeof(bool), typeof(PpsDataListItem), new FrameworkPropertyMetadata(BooleanBox.False));
		public static readonly DependencyProperty HasCommandsProperty = hasCommandsPropertyKey.DependencyProperty;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		private static object OnCoerceCommands(DependencyObject d, object baseValue)
			=> baseValue ?? new PpsUICommandCollection();

		private static void OnCommandsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsDataListItem)d).OnCommandsChanged((PpsUICommandCollection)e.NewValue, (PpsUICommandCollection)e.OldValue);

		/// <summary></summary>
		/// <param name="newValue"></param>
		/// <param name="oldValue"></param>
		protected virtual void OnCommandsChanged(PpsUICommandCollection newValue, PpsUICommandCollection oldValue)
		{
			if (oldValue != null)
				oldValue.CollectionChanged -= Commands_CollectionChanged;
			if (newValue != null)
				newValue.CollectionChanged += Commands_CollectionChanged;

			UpdateCommands();
		} // proc OnCommandsChanged

		private void Commands_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
			=> UpdateCommands();

		private void UpdateCommands()
		{
			var commands = Commands;
			SetValue(hasCommandsPropertyKey, commands != null && commands.Count > 0);

			if (commands == null || commands.Count == 0)
				SetValue(hasCommandsPropertyKey, false);
			else 
			{
				SetValue(hasCommandsPropertyKey, true);
				foreach (var cmd in Commands.OfType<PpsUICommandButton>())
					cmd.CommandParameter = DataContext;
			}
		} // func UpdateHasCommands

		/// <summary>Current commands for the item.</summary>
		public PpsUICommandCollection Commands { get => (PpsUICommandCollection)GetValue(CommandsProperty); set => SetValue(CommandsProperty, value); }
		/// <summary>Are there commands in the collection.</summary>
		public bool HasCommands => BooleanBox.GetBool(GetValue(HasCommandsProperty));

		#endregion

		static PpsDataListItem()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PpsDataListItem), new FrameworkPropertyMetadata(typeof(PpsDataListItem)));
		} // sctor
	} // class PpsDataListItem

	#endregion

	#region -- class PpsDataListControl -----------------------------------------------

	/// <summary></summary>
	public class PpsDataListControl : Selector
	{
		#region -- ListCommands - Property --------------------------------------------

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		private static readonly DependencyPropertyKey listCommandsPropertyKey = DependencyProperty.RegisterReadOnly(nameof(ListCommands), typeof(PpsUICommandCollection), typeof(PpsDataListControl), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty ListCommandsProperty = listCommandsPropertyKey.DependencyProperty;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		/// <summary>Set a commands for the list.</summary>
		public PpsUICommandCollection ListCommands => (PpsUICommandCollection)GetValue(ListCommandsProperty);

		#endregion

		#region -- ItemCommands - Property --------------------------------------------

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		private static readonly DependencyPropertyKey itemCommandsPropertyKey = DependencyProperty.RegisterReadOnly(nameof(ItemCommands), typeof(PpsUICommandCollection), typeof(PpsDataListControl), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty ItemCommandsProperty = itemCommandsPropertyKey.DependencyProperty;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		/// <summary>Set commands for items.</summary>
		public PpsUICommandCollection ItemCommands => (PpsUICommandCollection)GetValue(ItemCommandsProperty);

		#endregion

		#region -- ItemCommandsSelector - Property ------------------------------------

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public static readonly DependencyProperty ItemCommandsSelectorProperty = DependencyProperty.Register(nameof(ItemCommandsSelector), typeof(IPpsCommandsSelector), typeof(PpsDataListControl), new FrameworkPropertyMetadata(null));
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		/// <summary>Set a commands selector for the items.</summary>
		public IPpsCommandsSelector ItemCommandsSelector { get => (IPpsCommandsSelector)GetValue(ItemCommandsSelectorProperty); set => SetValue(ItemCommandsSelectorProperty, value); }

		#endregion

		#region -- HasCommands - Property ---------------------------------------------

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		private static readonly DependencyPropertyKey hasCommandsPropertyKey = DependencyProperty.RegisterReadOnly(nameof(HasCommands), typeof(bool), typeof(PpsDataListControl), new FrameworkPropertyMetadata(BooleanBox.True));
		public static readonly DependencyProperty HasCommandsProperty = hasCommandsPropertyKey.DependencyProperty;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		/// <summary>Do have any commands.</summary>
		public PpsUICommandCollection HasCommands => (PpsUICommandCollection)GetValue(HasCommandsProperty);

		#endregion

		#region -- UserFilterExpression, FilterExpression - Property ------------------

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public static readonly DependencyProperty FilterExpressionProperty = DependencyProperty.Register(nameof(FilterExpression), typeof(PpsDataFilterExpression), typeof(PpsDataListControl), new FrameworkPropertyMetadata(new PropertyChangedCallback(OnFilterExpressionChanged), new CoerceValueCallback(OnCoerceFilterExpression)));
		public static readonly DependencyProperty UserFilterTextProperty = DependencyProperty.Register(nameof(UserTextExpression), typeof(string), typeof(PpsDataListControl), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnUserFilterTextChanged)));

		private static readonly DependencyPropertyKey isFilteredPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsFiltered), typeof(bool), typeof(PpsDataListControl), new FrameworkPropertyMetadata(BooleanBox.False));
		public static readonly DependencyProperty IsFilteredProperty = isFilteredPropertyKey.DependencyProperty;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		private static object OnCoerceFilterExpression(DependencyObject d, object baseValue)
			=> baseValue is PpsDataFilterExpression expr ? expr.Reduce() : PpsDataFilterExpression.True;

		private static void OnFilterExpressionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsDataListControl)d).UpdateFilterExpression();

		private static void OnUserFilterTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsDataListControl)d).UpdateFilterExpression();

		private void UpdateFilterExpression()
		{
			if (filterView != null && AllowFilter)
			{
				var expr = PpsDataFilterExpression.Combine(FilterExpression, PpsDataFilterExpression.Parse(UserTextExpression));
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

		/// <summary>Filter the collection view.</summary>
		public PpsDataFilterExpression FilterExpression { get => (PpsDataFilterExpression)GetValue(FilterExpressionProperty); set => SetValue(FilterExpressionProperty, value); }
		/// <summary>Filter the collection view, this property is used by the template. Type is string because we do not want to change the source e.g. spaces. </summary>
		public string UserTextExpression { get => (string)GetValue(UserFilterTextProperty); set => SetValue(UserFilterTextProperty, value); }
		/// <summary>Is the view filterd, currently.</summary>
		public bool IsFiltered => BooleanBox.GetBool(GetValue(IsFilteredProperty));

		#endregion

		#region -- AllowFilter, IsFilterable - Property -------------------------------

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public static readonly DependencyProperty AllowFilterProperty = DependencyProperty.Register(nameof(AllowFilter), typeof(bool), typeof(PpsDataListControl), new FrameworkPropertyMetadata(BooleanBox.True, new PropertyChangedCallback(OnAllowFilterChanged)));

		private static readonly DependencyPropertyKey isFilterablePropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsFilterable), typeof(bool), typeof(PpsDataListControl), new FrameworkPropertyMetadata(BooleanBox.True));
		public static readonly DependencyProperty IsFilterableProperty = isFilterablePropertyKey.DependencyProperty;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		private void UpdateFilterable()
			=> SetValue(isFilterablePropertyKey, BooleanBox.GetObject(filterView != null && AllowFilter));

		private static void OnAllowFilterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsDataListControl)d).UpdateFilterable();

		/// <summary>Allow filter</summary>
		public bool AllowFilter { get => BooleanBox.GetBool(GetValue(AllowFilterProperty)); set => SetValue(AllowFilterProperty, BooleanBox.GetObject(value)); }

		/// <summary>Is filterable</summary>
		public bool IsFilterable => BooleanBox.GetBool(GetValue(IsFilterableProperty));

		#endregion

		private IPpsDataRowViewFilter filterView = null;
		private IPpsDataRowViewSort sortView = null;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary></summary>
		public PpsDataListControl()
		{
			SetValue(listCommandsPropertyKey, CreateCommands());
			SetValue(itemCommandsPropertyKey, CreateCommands());
		} // ctor

		private PpsUICommandCollection CreateCommands()
		{
			return new PpsUICommandCollection
			{
				AddLogicalChildHandler = AddLogicalChild,
				RemoveLogicalChildHandler = RemoveLogicalChild,
				DefaultCommandTarget = this
			};
		} // func CoerceCommands

		static PpsDataListControl()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PpsDataListControl), new FrameworkPropertyMetadata(typeof(PpsDataListControl)));
		} // sctor

		#endregion

		/// <summary></summary>
		/// <param name="oldValue"></param>
		/// <param name="newValue"></param>
		protected override void OnItemsSourceChanged(IEnumerable oldValue, IEnumerable newValue)
		{
			base.OnItemsSourceChanged(oldValue, newValue);

			// get view
			var view = CollectionViewSource.GetDefaultView(newValue);
			filterView = view as IPpsDataRowViewFilter;
			sortView = view as IPpsDataRowViewSort;

			UpdateFilterExpression();
			UpdateFilterable();
		} // proc OnItemsSourceChanged

		#region -- Item Container -----------------------------------------------------

		/// <summary></summary>
		/// <returns></returns>
		protected override DependencyObject GetContainerForItemOverride()
			=> new PpsDataListItem();

		/// <summary></summary>
		/// <param name="element"></param>
		/// <param name="item"></param>
		protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
		{
			base.PrepareContainerForItemOverride(element, item);
			if (element is PpsDataListItem container)
				container.Commands = ItemCommandsSelector?.SelectCommands(item, element) ?? ItemCommands;
		} // proc PrepareContainerForItemOverride

		/// <summary></summary>
		/// <param name="element"></param>
		/// <param name="item"></param>
		protected override void ClearContainerForItemOverride(DependencyObject element, object item)
			=> base.ClearContainerForItemOverride(element, item);

		/// <summary></summary>
		/// <param name="item"></param>
		/// <returns></returns>
		protected override bool IsItemItsOwnContainerOverride(object item)
			=> item is PpsDataListItem;

		#endregion

		/// <summary></summary>
		protected override IEnumerator LogicalChildren
			=> Procs.CombineEnumerator(base.LogicalChildren, ListCommands.GetEnumerator(), ItemCommands.GetEnumerator());
	} // class PpsDataListControl

	#endregion
}
