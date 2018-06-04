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
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace TecWare.PPSn.Controls
{
	#region -- class SideBarMenu ------------------------------------------------------

	/// <summary></summary>
	[Obsolete]
	public class SideBarMenu : ListBox, INotifyPropertyChanged
	{
		//public static readonly DependencyProperty PaneTitleText;

		/// <summary></summary>
		public event PropertyChangedEventHandler PropertyChanged;
		private string paneTitleText = String.Empty;
		private string headlineText = String.Empty;

		/// <summary></summary>
		public SideBarMenu()
		{
			AddHandler(Button.ClickEvent, new RoutedEventHandler(SideBarMenuItemButtonClick));
		} // ctor

		private void SideBarMenuItemButtonClick(object sender, RoutedEventArgs e)
		{
			var button = e.OriginalSource as Button;
			if (button == null)
				return;
			var item = button.DataContext as SideBarMenuItem;
			if (item == null)
				return;

			e.Handled = true;

			PaneTitleText = GetPaneTitleTextFromItem(item).ToUpper();

			// action only for RootItems
			if (item.IsChildItem)
				return;

			SetItemState((SideBarMenuRootItem)item);
		} // proc SideBarMenuItemButtonClick

		private void SetItemState(SideBarMenuRootItem item)
		{
			var currIsExpanded = item.IsExpanded;

			CollapseItems();

			if (!currIsExpanded)
				item.IsExpanded = true;
		} // proc SetItemState

		private void CollapseItems()
		{
			foreach (SideBarMenuRootItem item in Items)
				item.IsExpanded = false;
		} // proc CollapseItems

		private string GetPaneTitleTextFromItem(SideBarMenuItem item)
		{
			if (!item.IsChildItem)
				return item.DisplayText;
			foreach (SideBarMenuRootItem rootItem in Items)
			{
				if (rootItem.IsChildOf(item))
					return String.Format("{0} / {1}", rootItem.DisplayText, item.DisplayText);
			}
			return string.Empty;
		} // func GetPaneTitleTextFromItem

		protected void OnPropertyChanged(string propertyName)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		} // proc OnPropertyChanged

		/// <summary></summary>
		public string PaneTitleText
		{
			get { return paneTitleText; }
			private set
			{
				if (String.Compare(value, paneTitleText, false) != 0)
				{
					paneTitleText = value;
					OnPropertyChanged(nameof(PaneTitleText));
				}
			}
		} // prop PaneTitleText

		/// <summary></summary>
		public string HeadLineText
		{
			get { return headlineText; }
			set
			{
				headlineText = value;
				OnPropertyChanged(nameof(HeadLineText));
			}
		} // prop HeadLineText
	} // class SideBarMenu

	#endregion

	#region -- class SideBarMenuItem --------------------------------------------------

	/// <summary></summary>
	[Obsolete]
	public class SideBarMenuItem : FrameworkContentElement, ICommandSource
	{
		private static readonly DependencyProperty DisplayTextProperty = DependencyProperty.Register("DisplayText", typeof(string), typeof(SideBarMenuItem));
		/// <summary>Dependcencyproperty the Command</summary>
		public static readonly DependencyProperty CommandProperty = ButtonBase.CommandProperty.AddOwner(typeof(SideBarMenuItem));
		/// <summary>Dependcencyproperty the Command Parameters</summary>
		public static readonly DependencyProperty CommandParameterProperty = ButtonBase.CommandParameterProperty.AddOwner(typeof(SideBarMenuItem));
		/// <summary>Dependcencyproperty the Command Target</summary>
		public static readonly DependencyProperty CommandTargetProperty = ButtonBase.CommandTargetProperty.AddOwner(typeof(SideBarMenuItem));
		private static readonly DependencyProperty IsVisibleProperty = DependencyProperty.Register("IsVisible", typeof(bool), typeof(SideBarMenuItem), new PropertyMetadata(false));
		private static readonly DependencyProperty IsChildItemProperty = DependencyProperty.Register("IsChildItem", typeof(bool), typeof(SideBarMenuItem), new PropertyMetadata(false));

		/// <summary>only used for child items</summary>
		public bool IsVisible
		{
			get { return (bool)GetValue(IsVisibleProperty); }
			set
			{
				if (IsVisible != value)
					SetValue(IsVisibleProperty, value);
			}
		} // prop IsVisible

		/// <summary></summary>
		public string DisplayText { get { return (string)GetValue(DisplayTextProperty); } set { SetValue(DisplayTextProperty, value); } }

		/// <summary></summary>
		public ICommand Command { get { return (ICommand)GetValue(CommandProperty); } set { SetValue(CommandProperty, value); } }

		/// <summary></summary>
		public object CommandParameter { get { return GetValue(CommandParameterProperty); } set { SetValue(CommandParameterProperty, value); } }

		/// <summary>nur für RoutedCommands</summary>
		public IInputElement CommandTarget { get { return (IInputElement)GetValue(CommandTargetProperty); } set { SetValue(CommandTargetProperty, value); } }

		/// <summary></summary>
		public bool IsChildItem { get { return (bool)GetValue(IsChildItemProperty); } set { SetValue(IsChildItemProperty, value); } }
	} // class SideBarMenuItem

	#endregion

	#region -- class SideBarMenuRootItem ----------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	[Obsolete]
	public class SideBarMenuRootItem : SideBarMenuItem
	{
		private static readonly DependencyProperty IsExpandedProperty = DependencyProperty.Register("IsExpanded", typeof(bool), typeof(SideBarMenuItem), new PropertyMetadata(false));
		private SideBarMenuItemCollection items;

		/// <summary></summary>
		public SideBarMenuRootItem() : base()
		{
			items = new SideBarMenuItemCollection();
			items.CollectionChanged += Items_CollectionChanged;
		} // ctor

		private void Items_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			switch (e.Action)
			{
				case NotifyCollectionChangedAction.Add:
					{
						if (e.NewItems[0] is SideBarMenuItem item)
						{
							AddLogicalChild(item);
							item.IsChildItem = true;
						}
						else
							throw new ArgumentNullException("SideBarMenu, wrong item.");
					}
					break;
				case NotifyCollectionChangedAction.Remove:
					{
						if (e.OldItems[0] is SideBarMenuItem item && IsChildOf(item))
							RemoveLogicalChild(item);
					}
					break;
				default:
					throw new InvalidOperationException();
			}
		} // proc Items_CollectionChanged

		protected override IEnumerator LogicalChildren
			=> items.OfType<SideBarMenuItem>().GetEnumerator();

		private void SetChildItemsVisibility(bool show)
		{
			foreach (var item in items)
				item.IsVisible = show;
		} // proc SetChildItemsVisibility

		/// <summary></summary>
		public bool IsExpanded
		{
			get { return (bool)GetValue(IsExpandedProperty); }
			set
			{
				if (value != IsExpanded && HasChildItems)
				{
					SetChildItemsVisibility(value);
					SetValue(IsExpandedProperty, value);
				}
			}
		} // prop IsExpanded

		/// <summary></summary>
		public bool IsChildOf(SideBarMenuItem item) => items.Contains(item);

		/// <summary></summary>
		public bool HasChildItems => items.Count > 0;

		/// <summary>List of ChildMenuItems.</summary>
		public SideBarMenuItemCollection Items => items;
	} // class SideBarMenuRootItem

	#endregion

	#region -- class SideBarMenuItemCollection ----------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	[Obsolete]
	public class SideBarMenuItemCollection : Collection<SideBarMenuItem>, INotifyCollectionChanged
	{
		/// <summary>INotifyCollectionChanged member</summary>
		public event NotifyCollectionChangedEventHandler CollectionChanged;

		protected override void InsertItem(int index, SideBarMenuItem item)
		{
			base.InsertItem(index, item);
			CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, this[index], index));
		} // proc InsertItem

		protected override void RemoveItem(int index)
		{
			CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, this[index], index));
			base.RemoveItem(index);
		} // proc RemoveItem
	} // class SideBarMenuItemCollection

	#endregion
}
