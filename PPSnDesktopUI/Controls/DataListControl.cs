using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TecWare.PPSn.Data;

namespace TecWare.PPSn.Controls
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Spezial Control to display data list</summary>
	public class DataListControl : ListBox
	{
		private ICollectionView currentDataList = null;
		private NotifyCollectionChangedEventHandler collectionChanged;

		public DataListControl()
		{
			// Create a virtual listbox
			SetValue(HorizontalContentAlignmentProperty, System.Windows.HorizontalAlignment.Stretch);
			SetValue(VirtualizingStackPanel.IsVirtualizingProperty, true);
			SetValue(VirtualizingStackPanel.VirtualizationModeProperty, VirtualizationMode.Recycling);
			SetValue(ScrollViewer.IsDeferredScrollingEnabledProperty, true);

			this.collectionChanged = (sender, e) => ResetList(e);
		} // ctor

		private PpsDataList GetPpsData()
		{
			return this.DataContext as PpsDataList;
		} // func GetPpsData

		protected override void OnItemsSourceChanged(IEnumerable oldValue, IEnumerable newValue)
		{
			base.OnItemsSourceChanged(oldValue, newValue);

			if (currentDataList != null)
				currentDataList.CollectionChanged += collectionChanged;

			currentDataList = newValue as ICollectionView;

			if (currentDataList != null)
			{
				currentDataList.CollectionChanged += collectionChanged;
				ResetList(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
			}
		} // proc OnItemsSourceChanged
		
		private void ResetList(NotifyCollectionChangedEventArgs e)
		{
			if (e.Action == NotifyCollectionChangedAction.Reset)
			{
				var scroll = this.GetVisualChild<ScrollViewer>();
				if (scroll != null)
					scroll.ScrollToTop();
			}
		} // proc ResetList

		//private bool ShowClassContextMenu(dynamic item)
		//{
		//	if (item == null)
		//		return false;

		//	string sTyp = item.OBJKTYP as string;
		//	if (String.IsNullOrEmpty(sTyp))
		//		return false;

		//	dynamic cls = GetPpsData().Classes[sTyp];
		//	ContextMenu menu = new ContextMenu();
		//	foreach (PpsDataAction action in cls.Actions)
		//	{
		//		menu.Items.Add(new MenuItem() { Header = action.Name });
		//	}
		//	menu.PlacementTarget = this;
		//	menu.IsOpen = true;

		//	return true;
		//} // proc ShowClassContextMenu

		protected override void OnKeyUp(KeyEventArgs e)
		{
			//if (e.Key == Key.Space)
			//	e.Handled = ShowClassContextMenu(this.SelectedItem);
			base.OnKeyUp(e);
		} // proc OnKeyUp

		//protected override void OnMouseDoubleClick(MouseButtonEventArgs e)
		//{
		//	if (e.LeftButton == MouseButtonState.Pressed || e.RightButton == MouseButtonState.Pressed)
		//		e.Handled = ShowClassContextMenu(this.SelectedItem);
		//	base.OnMouseDoubleClick(e);
		//} // proc OnMouseDoubleClick

		//protected override void OnContextMenuOpening(ContextMenuEventArgs e)
		//{
		//	e.Handled = ShowClassContextMenu(this.SelectedItem);
		//	base.OnContextMenuOpening(e);
		//} // proc OnContextMenuOpening
	} // class DataListControl
}
