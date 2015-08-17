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

		private string currentSearchText = String.Empty;
		private string currentViewCaption = String.Empty;

		public PpsNavigatorModel(PpsMainWindow windowModel)
		{
			this.windowModel = windowModel;

			// Create a view for the actions
			actions = CollectionViewSource.GetDefaultView(Environment.Actions);
			actions.Filter += FilterAction;

			// Create a view for the views
			views = CollectionViewSource.GetDefaultView(Environment.Views);
			views.SortDescriptions.Add(new SortDescription("DisplayName", ListSortDirection.Ascending));
			views.CurrentChanged += (sender, e) => UpdateCurrentView((PpsMainViewDefinition)views.CurrentItem);

			// Update the view
			if (!views.MoveCurrentToFirst())
				UpdateCurrentView((PpsMainViewDefinition)views.CurrentItem);

			currentViewCaption = "DATEN";
		} // ctor

		private bool FilterAction(object item)
		{
			var action = item as PpsMainActionDefinition;
			return action != null;
		} // func ActionFilter
		
		private void UpdateCurrentView(PpsMainViewDefinition currentView)
		{
			if (currentView == null)
			{
				currentFilter = null;
				currentFilters = null;
				currentOrders = null;
			}
			else
			{
				currentFilter = null;
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
					newOrder = null;
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

		private void RefreshData()
		{
		} // proc RefreshData

		protected override object OnIndex(object key)
		{
			return base.OnIndex(key) ?? Environment.GetValue(key); // inherit from the environment
		} // func OnIndex

		public PpsMainEnvironment Environment => windowModel.Environment;

		/// <summary>Points to the current view</summary>
		public PpsMainViewDefinition CurrentView => views.CurrentItem as PpsMainViewDefinition;
		/// <summary>Returns the current filters</summary>
		public IEnumerable<PpsFilterView> CurrentFilters => currentFilters;
		/// <summary>Returns the current orders</summary>
		public IEnumerable<PpsOrderView> CurrentOrders => currentOrders;
		/// <summary></summary>
		public ICollectionView VisibleViews => views;
		/// <summary></summary>
		public ICollectionView VisibleActions => actions;

		public string CurrentSearchText { get { return currentSearchText; } set { currentSearchText = value; } }
		public string CurrentViewCaption { get { return currentViewCaption; } set { currentViewCaption = value; } }

		// Q+D TEST Schmidt
		public List<TestCustomer> TestItems
		{
			get
			{
				return new List<TestCustomer>
				{
					new TestCustomer { Name = "Müller", FirstName = "Walther" },
					new TestCustomer { Name = "Schmidt", FirstName = "Tim" },
					new TestCustomer { Name = "Mustermann", FirstName = "Theo" },
					new TestCustomer { Name = "Meier", FirstName = "Ralf" },
					new TestCustomer { Name = "Knopf", FirstName = "Jim" },
					new TestCustomer { Name = "Hawkins", FirstName = "Sam" },
					new TestCustomer { Name = "Liebers", FirstName = "Anatol" },
					new TestCustomer { Name = "Lehmann", FirstName = "Urs" }
				};
			}
		}
		public class TestCustomer
		{
			public string Name { get; set; }
			public string FirstName { get; set; }
		}

	} // class PpsNavigatorModel

}
