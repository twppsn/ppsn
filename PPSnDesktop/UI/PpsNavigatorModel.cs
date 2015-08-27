using Neo.IronLua;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using TecWare.DES.Data;
using TecWare.PPSn.Data;

namespace TecWare.PPSn.UI
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PpsNavigatorModel : LuaTable
	{
		#region -- class PpsFilterView ----------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		public class PpsFilterView : ObservableObject, ICommand
		{
			public event EventHandler CanExecuteChanged { add { } remove { } }

			private PpsNavigatorModel model;
			private PpsMainViewFilter filter;
			
			internal PpsFilterView(PpsNavigatorModel model, PpsMainViewFilter filter)
			{
				this.model = model;
				this.filter = filter;
			} // ctor

			public void Execute(object parameter)
			{
				model.UpdateCurrentFilter(IsSelected ? null : this);
				model.RefreshData(); // reload data
			} // proc Execute

			public bool CanExecute(object parameter) => true;

			internal void FireIsSelectedChanged() => OnPropertyChanged(nameof(IsSelected));

			public string DisplayName => filter.DisplayName;
			public string FilterName => filter.Name;
			public string ShortCut => filter.ShortCut;
			public bool IsSelected => model.currentFilter == this;
		} // class PpsNavigatorFilter

		#endregion

		#region -- class PpsOrderView -----------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		public class PpsOrderView : ObservableObject, ICommand
		{
			public event EventHandler CanExecuteChanged { add { } remove { } }

			private PpsNavigatorModel model;
			private PpsMainViewOrder order;

			internal PpsOrderView(PpsNavigatorModel model, PpsMainViewOrder order)
			{
				this.model = model;
				this.order = order;
			} // ctor

			public void Execute(object parameter)
			{
				model.UpdateCurrentOrder(this);
				model.RefreshData();
			}
			public bool CanExecute(object parameter) => true;

			internal void FireIsCheckedChanged() => OnPropertyChanged(nameof(IsChecked));


			public string DisplayName => order.DisplayName;
			public string ColumnName => order.ColumnName;
			public bool? IsChecked => model.currentOrder == this ? (bool?)model.sortAscending : null;
    } // class PpsOrderView

		#endregion

		private PpsMainWindow windowModel;

		private ICollectionView views;
		private ICollectionView actions;

		private PpsFilterView[] currentFilters;
		private PpsOrderView[] currentOrders;
		private PpsFilterView currentFilter;
		private PpsOrderView currentOrder;
		private bool sortAscending = true;

		private PpsDataList items;
		private ICollectionView itemsView;
		
		private string currentSearchText = String.Empty;
		
		public PpsNavigatorModel(PpsMainWindow windowModel)
		{
			this.windowModel = windowModel;

			// Create a view for the actions
			actions = CollectionViewSource.GetDefaultView(Environment.Actions);
			actions.SortDescriptions.Add(new SortDescription("Priority", ListSortDirection.Ascending));
			actions.Filter += FilterAction;

			// Create a view for the views
			views = CollectionViewSource.GetDefaultView(Environment.Views);
			views.SortDescriptions.Add(new SortDescription("DisplayName", ListSortDirection.Ascending));
			views.CurrentChanged += (sender, e) => UpdateCurrentView((PpsMainViewDefinition)views.CurrentItem);

			// init data list
			items = new PpsDataList(Environment);
			itemsView = CollectionViewSource.GetDefaultView(items);
			itemsView.CurrentChanged += (sender, e) => RefreshActions();

			// Update the view
			if (!views.MoveCurrentToFirst())
				UpdateCurrentView((PpsMainViewDefinition)views.CurrentItem);
		} // ctor

		private bool FilterAction(object item)
		{
			var action = item as PpsMainActionDefinition;
			return action != null ? action.CheckCondition(this, false) : false;
		} // func ActionFilter
		
		private void UpdateCurrentView(PpsMainViewDefinition currentView)
		{
			if (currentView == null)
			{
				currentFilter = null;
				currentFilters = null;
				currentOrder = null;
				currentOrders = null;
			}
			else
			{
				currentFilter = null;
				currentOrder = null;
				currentFilters = (from c in currentView.Filters select new PpsFilterView(this, c)).ToArray();
				currentOrders = (from c in currentView.SortOrders select new PpsOrderView(this, c)).ToArray();
			}

			// Notify UI
			OnPropertyChanged(nameof(CurrentView));
			OnPropertyChanged(nameof(CurrentFilters));
			OnPropertyChanged(nameof(CurrentOrders));

			// Update States
			UpdateCurrentFilter(null);
			
			RefreshData();
			RefreshActions();
		} // proc UpdateCurrentView

		private void UpdateCurrentFilter(PpsFilterView newFilter)
		{
			var oldFilter = currentFilter;
			currentFilter = newFilter;

			if (oldFilter != null)
				oldFilter.FireIsSelectedChanged();
			if (newFilter != null)
				newFilter.FireIsSelectedChanged();
		} // proc UpdateCurrentFilter

		private void UpdateCurrentOrder(PpsOrderView newOrder)
		{
			var oldOrder = currentOrder;
			if (oldOrder == newOrder)
			{
				if (sortAscending)
				{
					sortAscending = false;
					oldOrder = null;
				}
				else
					currentOrder = newOrder = null;
			}
			else
			{
				currentOrder = newOrder;
				sortAscending = true;
			}

			if (oldOrder != null)
				oldOrder.FireIsCheckedChanged();
			if (newOrder != null)
				newOrder.FireIsCheckedChanged();
    } // proc UpdateCurrentOrder

		private async void RefreshData()
		{
			// build neu data source
			if (CurrentView == null)
				await items.ClearAsync();
			else
			{
				var dataSource = new PpsShellGetList(CurrentView.Name) { Detailed = false };

				if (currentFilter != null)
					dataSource.PreFilterId = currentFilter.FilterName;
				if (currentOrder != null)
					dataSource.OrderId = currentOrder.ColumnName + (sortAscending ? "+" : "-");
				dataSource.CustomFilter = null;

				await items.Reset(dataSource);
			}
		} // proc RefreshData

		private void RefreshActions()
		{
			actions.Refresh();
		} // proc RefreshActions

		[LuaMember(nameof(IsItemType))]
		public bool IsItemType(object item, string typ)
		{
			var t = item as LuaTable;
			if (t == null)
				return false;
			else
			{
				var itemTyp = t.GetOptionalValue("OBJKTYP", String.Empty);
				return itemTyp == typ;
			}
		} // func IsItemType

		[LuaMember("LoadPane")]
		private async Task LuaLoadGenericPaneAsync(LuaTable arguments)
		{
			await windowModel.LoadPaneAsync(typeof(PpsGenericWpfWindowPane), arguments);
		} // proc LuaLoadGenericPane

		[LuaMember("LoadMask")]
		private async Task LoadGenericMaskAsync(LuaTable arguments)
		{
			await windowModel.LoadPaneAsync(typeof(PpsGenericMaskWindowPane), arguments);
		} // proc LoadGenericMask

		protected override object OnIndex(object key)
		{
			return base.OnIndex(key) ?? Environment.GetValue(key); // inherit from the environment
		} // func OnIndex

		public PpsMainEnvironment Environment => windowModel.Environment;

		[LuaMember(nameof(Window))]
		public PpsMainWindow Window => windowModel;

		[LuaMember(nameof(CurrentItem))]
		public object CurrentItem => itemsView?.CurrentItem;
		/// <summary>Points to the current view</summary>
		[LuaMember(nameof(CurrentView))]
		public PpsMainViewDefinition CurrentView => views?.CurrentItem as PpsMainViewDefinition;
		/// <summary>Returns the current filters</summary>
		public IEnumerable<PpsFilterView> CurrentFilters => currentFilters;
		/// <summary>Returns the current orders</summary>
		public IEnumerable<PpsOrderView> CurrentOrders => currentOrders;
		/// <summary></summary>
		public ICollectionView VisibleViews => views;
		/// <summary></summary>
		public ICollectionView VisibleActions => actions;
		/// <summary>Data Items</summary>
		public ICollectionView Items => itemsView;

		public string CurrentSearchText { get { return currentSearchText; } set { currentSearchText = value; } }
	} // class PpsNavigatorModel
}
