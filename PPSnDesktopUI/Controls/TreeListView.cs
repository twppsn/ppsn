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
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
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
		private object itemToSelect;

		public PpsTreeListView()
		{
			Loaded += OnLoaded;
		} // ctor

		protected override DependencyObject GetContainerForItemOverride()
			=> new PpsTreeListViewItem();

		protected override bool IsItemItsOwnContainerOverride(object item)
			=> item is PpsTreeListViewItem;

		private void OnLoaded(object sender, RoutedEventArgs e)
			=> EnsureSelection();

		protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
		{
			base.OnItemsChanged(e);
			switch (e.Action)
			{
				case NotifyCollectionChangedAction.Remove:
					var x = ItemContainerGenerator;
					AlternationExtensions.SetAlternationIndexRecursively((ItemsControl)this, 0);
					break;
				case NotifyCollectionChangedAction.Add:
					if (e.NewItems.Count > 0 && e.NewItems[0] != null)
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
		}

		private void DoSelectNode(object item)
		{
			var node = ItemContainerGenerator.ContainerFromItem(item) as PpsTreeListViewItem;
			if (node == null)
				throw new ArgumentNullException("SelectNode TreeListViewItem");

			// focus?
			Dispatcher.BeginInvoke(
				new Action(() =>
				{
					node.IsSelected = true;
					node.BringIntoView();
				}),
					DispatcherPriority.Input
				);
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
			if (Items.Count == 0 || SelectedItem != null)
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

		public PpsTreeListViewItem()
		{
			Loaded += OnLoaded;
		} // ctor

		protected override DependencyObject GetContainerForItemOverride()
			=> new PpsTreeListViewItem();

		protected override bool IsItemItsOwnContainerOverride(object item)
			=> item is PpsTreeListViewItem;

		private void OnLoaded(object sender, RoutedEventArgs e)
			=> UpdateAlternationIndex();

		protected override void OnExpanded(RoutedEventArgs e)
		{
			base.OnExpanded(e);
			UpdateAlternationIndex();
		} // proc OnExpanded

		protected override void OnCollapsed(RoutedEventArgs e)
		{
			base.OnCollapsed(e);
			UpdateAlternationIndex();
		} // proc OnCollapsed

		protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
		{
			base.OnItemsChanged(e);

			switch (e.Action)
			{
				case NotifyCollectionChangedAction.Remove:
					UpdateAlternationIndex();
					break;
				case NotifyCollectionChangedAction.Add:
					SelectAddedNode(e.NewItems[0]);
					break;
			}
		} // proc OnItemChanged

		private void UpdateAlternationIndex()
		{
			// update index
			var parent = ParentTreeView;
			if (parent != null)
				AlternationExtensions.SetAlternationIndexRecursively((ItemsControl)parent, 0);
		} // proc UpdateAlternationIndex

		#region -- ItemSelection --------------------------------------------------------

		private void SelectNode(object item)
		{
			var node = ItemContainerGenerator.ContainerFromItem(item) as PpsTreeListViewItem;
			if (node == null)
				throw new ArgumentNullException("SelectNode TreeListViewItem");

			// focus?
			Dispatcher.BeginInvoke(
				new Action(() =>
					{
						node.IsSelected = true;
						node.BringIntoView();
					}),
					DispatcherPriority.Input
				);
		} // proc SelectNode

		private void SelectAddedNode(object item)
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

		private ItemsControl ParentItemControl => ItemsControl.ItemsControlFromItemContainer(this);

		private PpsTreeListView ParentTreeView
		{
			get
			{
				var parent = ParentItemControl;
				while (parent != null)
				{
					if (parent is PpsTreeListView)
						return (PpsTreeListView)parent;
					parent = ItemsControl.ItemsControlFromItemContainer(parent);
				}
				return null;
			}
		} // prop ParentTreeView
	} // class PpsTreeListViewItem

	#endregion

	#region -- class AlternationExtensions ----------------------------------------------

	internal static class AlternationExtensions
	{
		private static readonly MethodInfo SetAlternationIndexMethodInfo;

		static AlternationExtensions()
		{
			SetAlternationIndexMethodInfo = typeof(ItemsControl).GetMethod("SetAlternationIndex", BindingFlags.Static | BindingFlags.NonPublic);
			if (SetAlternationIndexMethodInfo == null)
				throw new ArgumentNullException("SetAlternationIndexMethodInfo");
		} // sctor

		public static int SetAlternationIndexRecursively(ItemsControl control, int firstAlternationIndex)
		{
			// check for alternating
			var alternationCount = control.AlternationCount;
			if (alternationCount == 0)
				return 0;

			// set the index for the controls
			foreach (var item in control.Items)
			{
				var container = control.ItemContainerGenerator.ContainerFromItem(item) as TreeViewItem;
				if (container != null)
				{
					var nextAlternation = firstAlternationIndex++ % alternationCount;
					SetAlternationIndexMethodInfo.Invoke(null, new object[] { container, nextAlternation });
					if (container.IsExpanded)
						firstAlternationIndex = SetAlternationIndexRecursively(container, firstAlternationIndex);
				}
			}

			return firstAlternationIndex;
		}

	} // class AlternationExtensions

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

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) { throw new System.NotSupportedException(); }
	} // class PpsTreeViewLevelToIndentConverter

	#endregion
}
