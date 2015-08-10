using Neo.IronLua;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace TecWare.PPSn.UI
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PpsNavigatorModel : LuaTable
	{
		private PpsMainWindow windowModel;

		private ICollectionView views;
		private ICollectionView actions;

		private PpsMainViewDefinition currentView;
		private PpsMainViewFilter currentFilter;
		private PpsMainViewSort currentSortOrder;

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

			// test
			currentView = (PpsMainViewDefinition)views.CurrentItem;
			currentFilter = currentView.Filters.Skip(1).First();
			currentSortOrder = currentView.SortOrders.Skip(2).First();
			currentViewCaption = "GEHE ZU MODUL";

			OnPropertyChanged(nameof(VisibleActions));
			OnPropertyChanged(nameof(VisibleViews));
			OnPropertyChanged(nameof(CurrentView));
			OnPropertyChanged(nameof(CurrentFilters));
			OnPropertyChanged(nameof(CurrentFilter));
			OnPropertyChanged(nameof(CurrentSortOrders));
			OnPropertyChanged(nameof(CurrentSortOrder));
			OnPropertyChanged(nameof(CurrentViewCaption));
		} // ctor

		private bool FilterAction(object item)
		{
			var action = item as PpsMainActionDefinition;
			return action != null;
		} // func ActionFilter

		protected override object OnIndex(object key)
		{
			return base.OnIndex(key) ?? Environment.GetValue(key); // inherit from the environment
		} // func OnIndex

		public PpsMainEnvironment Environment => windowModel.Environment;

		/// <summary></summary>
		public PpsMainViewDefinition CurrentView => currentView;
		/// <summary></summary>
		public PpsMainViewFilter CurrentFilter => currentFilter;
		/// <summary></summary>
		public PpsMainViewSort CurrentSortOrder => currentSortOrder;
		/// <summary></summary>
		public IEnumerable<PpsMainViewFilter> CurrentFilters => currentView?.Filters;
		/// <summary></summary>
		public IEnumerable<PpsMainViewSort> CurrentSortOrders => currentView?.SortOrders;
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
