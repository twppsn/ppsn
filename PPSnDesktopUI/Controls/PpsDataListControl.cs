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
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
		public static readonly DependencyProperty CommandsProperty = DependencyProperty.Register(nameof(Commands), typeof(PpsUICommandCollection), typeof(PpsDataListItem), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnCommandsChanged)));
		private static readonly DependencyPropertyKey hasCommandsPropertyKey = DependencyProperty.RegisterReadOnly(nameof(HasCommands), typeof(bool), typeof(PpsDataListItem), new FrameworkPropertyMetadata(BooleanBox.False));
		public static readonly DependencyProperty HasCommandsProperty = hasCommandsPropertyKey.DependencyProperty;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

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

			UpdateHasCommands();
		} // proc OnCommandsChanged

		private void Commands_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
			=> UpdateHasCommands();

		private void UpdateHasCommands()
		{
			var commands = Commands;
			SetValue(hasCommandsPropertyKey, commands != null && commands.Count > 0);
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
		public static readonly DependencyProperty FilterExpressionProperty = DependencyProperty.Register(nameof(FilterExpression), typeof(PpsDataFilterExpression), typeof(PpsDataListControl), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty UserFilterExpressionProperty = DependencyProperty.Register(nameof(UserFilterExpression), typeof(PpsDataFilterExpression), typeof(PpsDataListControl), new FrameworkPropertyMetadata(null));
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		/// <summary>Filter the collection view.</summary>
		public PpsDataFilterExpression FilterExpression { get => (PpsDataFilterExpression)GetValue(FilterExpressionProperty); set => SetValue(FilterExpressionProperty, value); }
		/// <summary>Filter the collection view, this property is used by the template.</summary>
		public PpsDataFilterExpression UserFilterExpression { get => (PpsDataFilterExpression)GetValue(UserFilterExpressionProperty); set => SetValue(UserFilterExpressionProperty, value); }

		#endregion

		#region -- AllowFilter, IsFilterable - Property -------------------------------

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public static readonly DependencyProperty AllowFilterProperty = DependencyProperty.Register(nameof(AllowFilter), typeof(bool), typeof(PpsDataListControl), new FrameworkPropertyMetadata(true));
		private static readonly DependencyPropertyKey isFilterablePropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsFilterable), typeof(bool), typeof(PpsDataListControl), new FrameworkPropertyMetadata(BooleanBox.True));
		public static readonly DependencyProperty IsFilterableProperty = isFilterablePropertyKey.DependencyProperty;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		/// <summary>Allow filter</summary>
		public bool AllowFilter { get => BooleanBox.GetBool(GetValue(AllowFilterProperty)); set => SetValue(AllowFilterProperty, BooleanBox.GetObject(value)); }

		/// <summary>Is filterable</summary>
		public bool IsFilterable => BooleanBox.GetBool(GetValue(IsFilterableProperty));

		#endregion

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary></summary>
		public PpsDataListControl()
		{
			SetValue(listCommandsPropertyKey,
				new PpsUICommandCollection
				{
					AddLogicalChildHandler = AddLogicalChild,
					RemoveLogicalChildHandler = RemoveLogicalChild,
					DefaultCommandTarget = this
				}
			);
		} // ctor

		static PpsDataListControl()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PpsDataListControl), new FrameworkPropertyMetadata(typeof(PpsDataListControl)));
		} // sctor

		#endregion

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
	} // class PpsDataListControl

	#endregion
}
