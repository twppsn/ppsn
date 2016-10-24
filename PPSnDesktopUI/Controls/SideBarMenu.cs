using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TecWare.PPSn.Controls
{
	#region -- class SideBarMenu ------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class SideBarMenu : ListBox
	{
		/// <summary></summary>
		public SideBarMenu()
		{
			AddHandler(Button.ClickEvent, new RoutedEventHandler(SideBarMenuItemButtonClick));
		} // ctor

		void SideBarMenuItemButtonClick(object sender, RoutedEventArgs e)
		{
			var button = e.OriginalSource as Button;
			// ups
			if (button == null)
				return;

			e.Handled = true;

			var item = button.DataContext as SideBarMenuRootItem;
			// action only for RootItems
			if (item == null)
				return;
			SetItemState(item);
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

	} // class SideBarMenu

	#endregion

	#region -- class SideBarMenuItem --------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class SideBarMenuItem : FrameworkContentElement
	{
		private static readonly DependencyProperty DisplayTextProperty = DependencyProperty.Register("DisplayText", typeof(string), typeof(SideBarMenuItem));
		private static readonly DependencyProperty CommandProperty = DependencyProperty.Register("Command", typeof(ICommand), typeof(SideBarMenuItem), new UIPropertyMetadata(null));
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
		public bool IsChildItem { get { return (bool)GetValue(IsChildItemProperty); } set { SetValue(IsChildItemProperty, value); } }
	} // class SideBarMenuItem

	#endregion

	#region -- class SideBarMenuRootItem ----------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
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
					var item = e.NewItems[0] as SideBarMenuItem;
					if (item == null)
						throw new ArgumentNullException("SideBarMenu, wrong item.");
					AddLogicalChild(item);
					item.IsChildItem = true;
					break;
				case NotifyCollectionChangedAction.Remove:
					RemoveLogicalChild(e.OldItems[0]);
					break;
				default:
					throw new InvalidOperationException();
			}
		} // proc Items_CollectionChanged

		private void SetChildItemsVisibility(bool show)
		{
			foreach (var item in items)
				item.IsVisible = show;
		} // proc SetChildItemsVisibility

		/// <summary></summary>
		public bool IsExpanded
		{
			get { return(bool)GetValue(IsExpandedProperty); }
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
		public bool HasChildItems => items.Count > 0;
		/// <summary>List of ChildMenuItems.</summary>
		public SideBarMenuItemCollection Items => items;
	} // class SideBarMenuRootItem

	#endregion

	#region -- class SideBarMenuItemCollection ----------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
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
