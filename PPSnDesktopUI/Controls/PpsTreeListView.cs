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
// CONDITIONS OF ANY KIND, either express or implied. See the NotifyCollectionChangedAction.Add for the
// specific language governing permissions and limitations under the Licence.
//
#endregion
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using TecWare.DE.Stuff;

namespace TecWare.PPSn.Controls
{
	#region -- class PpsTreeListView ----------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PpsTreeListView : TreeView
	{
		private readonly static DependencyProperty AutoSelectAddedNodeProperty = DependencyProperty.Register("AutoSelectAddedNode", typeof(bool), typeof(PpsTreeListView), new PropertyMetadata(true));
		private object itemToSelect;

		/// <summary></summary>
		public PpsTreeListView()
		{
			Loaded += OnLoaded;
		} // ctor

		/// <summary></summary>
		protected override DependencyObject GetContainerForItemOverride()
			=> new PpsTreeListViewItem();

		/// <summary></summary>
		protected override bool IsItemItsOwnContainerOverride(object item)
			=> item is PpsTreeListViewItem;

		private void OnLoaded(object sender, RoutedEventArgs e)
			=> EnsureSelection();

		/// <summary></summary>
		protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
		{
			base.OnItemsChanged(e);
			switch (e.Action)
			{
				//case NotifyCollectionChangedAction.Remove:
				//	AlternationExtensions.SetAlternationIndexRecursively((ItemsControl)this, 0);
				//	break;
				case NotifyCollectionChangedAction.Add:
					if (e.NewItems.Count > 0 && e.NewItems[0] != null && AutoSelectAddedNode)
						SelectNode(e.NewItems[0]);
					break;
				case NotifyCollectionChangedAction.Reset:
					EnsureSelection();
					break;
			}
		} // proc OnItemChanged

		/// <summary></summary>
		public void SelectNode(object item)
		{
			if (ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated)
			{
				DoSelectNode(item);
			}
			else
			{
				itemToSelect = item;
				ItemContainerGenerator.StatusChanged += OnItemContainerGeneratorStatusChanged;
			}
		} // proc SelectNode

		/// <summary>Select new node - default true</summary>
		public bool AutoSelectAddedNode { get => (bool)GetValue(AutoSelectAddedNodeProperty); set => SetValue(AutoSelectAddedNodeProperty, value); }

		private void DoSelectNode(object item)
		{
			if (!(ItemContainerGenerator.ContainerFromItem(item) is PpsTreeListViewItem node))
				return; // todo: noch nicht generiert? throw new ArgumentNullException("SelectNode TreeListViewItem");

			//  reset itemToSelect
			if (item == itemToSelect)
				itemToSelect = null;

			node.IsSelected = true;
			node.BringIntoView();
		} // proc DoSelectNode

		private void OnItemContainerGeneratorStatusChanged(object sender, EventArgs e)
		{
			if (ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated)
			{
				ItemContainerGenerator.StatusChanged -= OnItemContainerGeneratorStatusChanged;
				SelectNode(itemToSelect);
			}
		} // event OnItemContainerGeneratorStatusChanged

		private void EnsureSelection()
		{
			if (Items.Count == 0 || SelectedItem != null || itemToSelect != null)
				return;
			SelectNode(Items[0]);
		} // proc EnsureSelection
	} // class PpsTreeListView

	#endregion

	#region -- class PpsTreeListViewItem ------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PpsTreeListViewItem : TreeViewItem
	{
		private object itemToSelect;

		/// <summary></summary>
		public PpsTreeListViewItem()
		{
		} // ctor

		/// <summary></summary>
		protected override DependencyObject GetContainerForItemOverride()
			=> new PpsTreeListViewItem();

		/// <summary></summary>
		protected override bool IsItemItsOwnContainerOverride(object item)
			=> item is PpsTreeListViewItem;

		/// <summary>Select item, before showing the contextmenu</summary>
		protected override void OnPreviewMouseRightButtonDown(MouseButtonEventArgs e)
		{
			base.OnPreviewMouseRightButtonDown(e);
			IsSelected = true;
		} // proc OnPreviewMouseRightButtonDown

		/// <summary>Overridden to update the Alternation index or select an added item. Passes to base-class</summary>
		/// <param name="e">Arguments</param>
		protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
		{
			base.OnItemsChanged(e);

			switch (e.Action)
			{
				//case NotifyCollectionChangedAction.Remove:
				//	UpdateAlternationIndex();
				//	break;
				case NotifyCollectionChangedAction.Add:
					SelectAddedNode(e.NewItems[0]);
					break;
			}
		} // proc OnItemChanged

		#region -- ItemSelection --------------------------------------------------------

		private void SelectNode(object item)
		{
			if (ItemContainerGenerator.ContainerFromItem(item) is PpsTreeListViewItem node)
			{
				node.IsSelected = true;
				node.BringIntoView();
			}
			else
				throw new ArgumentNullException("SelectNode TreeListViewItem");
		} // proc SelectNode

		private void SelectAddedNode(object item)
		{
			if (ParentTreeView.AutoSelectAddedNode)
			{
				if (ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated)
				{
					SelectNode(item);
				}
				else
				{
					itemToSelect = item;
					ItemContainerGenerator.StatusChanged += OnItemContainerGeneratorStatusChanged;
				}
			}
			// ensure new node is visible, also fire StatusChanged
			ExpandParentNode();
		} // proc SelectAddedNode

		private void OnItemContainerGeneratorStatusChanged(object sender, EventArgs e)
		{
			if (ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated)
			{
				ItemContainerGenerator.StatusChanged -= OnItemContainerGeneratorStatusChanged;
				SelectNode(itemToSelect);
			}
		} // event OnItemContainerGeneratorStatusChanged

		private void ExpandParentNode()
		{
			// error handling?
			var parent = ParentItemControl;
			var idx = parent.ItemContainerGenerator.IndexFromContainer(this);
			var node = parent.ItemContainerGenerator.ContainerFromIndex(idx) as PpsTreeListViewItem;
			if (!node.IsExpanded)
				node.IsExpanded = true;
		} // proc ExpandParentNode

		#endregion

		private ItemsControl ParentItemControl => ItemsControlFromItemContainer(this);

		private PpsTreeListView ParentTreeView
		{
			get
			{
				var parent = ParentItemControl;
				while (parent != null)
				{
					if (parent is PpsTreeListView)
						return (PpsTreeListView)parent;
					parent = ItemsControlFromItemContainer(parent);
				}
				return null;
			}
		} // prop ParentTreeView
	} // class PpsTreeListViewItem

	#endregion

	#region -- class PpsTreeViewLevelToIndentConverter ----------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PpsTreeViewLevelToIndentConverter : IValueConverter
	{
		private const double indentSize = 19.0;

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			var level = 0;
			if (value is DependencyObject)
			{
				var parent = VisualTreeHelper.GetParent(value as DependencyObject);
				while (!(parent is PpsTreeListView) && (parent != null))
				{
					if (parent is PpsTreeListViewItem)
						level++;
					parent = VisualTreeHelper.GetParent(parent);
				}
			}
			return new Thickness(level * indentSize, 0, 0, 0);
		} // func Convert

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) 
			=> throw new NotSupportedException();
	} // class PpsTreeViewLevelToIndentConverter

	#endregion
}
