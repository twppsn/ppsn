using Neo.IronLua;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using TecWare.DE.Data;
using TecWare.PPSn.Data;
using System.Diagnostics;

namespace TecWare.PPSn.UI
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PpsNavigatorModel : LuaTable
	{
		#region -- class CurrentSearchTextExpression --------------------------------------

		private abstract class CurrentSearchTextExpression
		{
			private readonly PpsNavigatorModel navigator;
			private readonly int searchTextOffset;
			private readonly string originalSearchText;

			public CurrentSearchTextExpression(PpsNavigatorModel navigator, string searchText, int searchTextOffset)
			{
				this.navigator = navigator;
				this.searchTextOffset = searchTextOffset;
				this.originalSearchText = searchText;
			} // ctor

			/// <summary></summary>
			/// <param name="currentSearchTextExpression"></param>
			/// <returns></returns>
			public virtual int CompareTo(CurrentSearchTextExpression other)
			{
				if (Object.ReferenceEquals(this, other))
					return 0;
				else
				{
					if (this.GetType() == other.GetType())
					{
						if (SearchText == other.SearchText)
							return 0;
						else
							return 1; // change text only
					}
					else
						return -1; //change + refresh
				}
			} // func CompareTo

			public virtual void Execute()
			{
			} // proc Execute

			public virtual bool IsExecutable => false;
			public string Data => originalSearchText.Substring(searchTextOffset);
			public string SearchText => originalSearchText;

			public PpsNavigatorModel Navigator => navigator;
		} // class CurrentSearchTextExpression

		#endregion

		#region -- class EmptyExpression --------------------------------------------------

		private sealed class EmptyExpression : CurrentSearchTextExpression
		{
			public EmptyExpression(PpsNavigatorModel navigator, string searchText, int searchTextOffset)
				:base(navigator, searchText, searchTextOffset)
			{
			} // ctor

			public static EmptyExpression Instance { get; } = new EmptyExpression(null, String.Empty, 0);
		} // class EmptyExpression

		#endregion

		#region -- class SearchExpression -------------------------------------------------

		private sealed class SearchExpression : CurrentSearchTextExpression
		{
			public SearchExpression(PpsNavigatorModel navigator, string searchText, int searchTextOffset)
				:base(navigator, searchText, searchTextOffset)
			{
			} // ctor
		} // class SearchExpression

		#endregion

		#region -- class MacroExpression --------------------------------------------------

		private sealed class MacroExpression : CurrentSearchTextExpression
		{
			public MacroExpression(PpsNavigatorModel navigator, string searchText, int searchTextOffset)
				:base(navigator, searchText, searchTextOffset)
			{
			} // ctor
		} // class MacroExpression

		#endregion

		#region -- class LuaExpression ----------------------------------------------------

		private sealed class LuaExpression : CurrentSearchTextExpression
		{
			private readonly LuaChunk chunk;

			public LuaExpression(PpsNavigatorModel navigator, string searchText, int searchTextOffset)
				: base(navigator, searchText, searchTextOffset)
			{
				if (navigator == null)
					throw new ArgumentNullException("navigator");

				if (String.IsNullOrWhiteSpace(Data))
					chunk = null;
				else // compile to check for syntax
				{
					try
					{
						chunk = navigator.Environment.CreateLuaChunk(Data);
					}
					catch (LuaParseException e)
					{
						navigator.Environment.Traces.AppendText(PpsTraceItemType.Debug, String.Format("Syntax: {0} at {1}", e.Message, e.Index));
						chunk = null;
					}
				}
			} // ctor

			public override void Execute()
			{
				if (chunk == null)
					return;

				try
				{
					var r = chunk.Run(Navigator);
					// write the return in the log
					if (r.Count > 0)
						Navigator.Environment.Traces.AppendText(PpsTraceItemType.Information, String.Format("Executed: {0}", r.ToString()));
					Navigator.ClearCurrentSearchText();
				}
				catch (Exception e)
				{
					Navigator.Environment.ShowException(ExceptionShowFlags.None, e, "Execution failed.");
				}
			} // proc Execute

			public override bool IsExecutable => chunk != null;
		} // class LuaExpression

		#endregion

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
				model.RefreshDataAsync(); // reload data
			} // proc Execute

			public bool CanExecute(object parameter) => true;

			internal void FireIsSelectedChanged() => OnPropertyChanged(nameof(IsSelected));

			public string DisplayName => filter.DisplayName;
			public string FilterName => filter.Name;
			public PpsDataFilterExpression FilterExpression => filter.FilterExpression;
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
				model.RefreshDataAsync();
			}
			public bool CanExecute(object parameter) => true;

			internal void FireIsCheckedChanged() => OnPropertyChanged(nameof(IsChecked));


			public string Name => order.Name;
			public string DisplayName => order.DisplayName;
			public bool? IsChecked => model.currentOrder == this ? (bool?)model.sortAscending : null;
    } // class PpsOrderView

		#endregion

		private PpsMainWindow windowModel;

		private CollectionViewSource views;
		private CollectionViewSource actions;

		private PpsFilterView[] currentFilters;
		private PpsOrderView[] currentOrders;
		private PpsFilterView currentFilter;
		private PpsOrderView currentOrder;
		private bool sortAscending = true;

		private PpsDataList items;
		private ICollectionView itemsView;
		
		private CurrentSearchTextExpression currentSearchTextExpression = EmptyExpression.Instance;		
		
		public PpsNavigatorModel(PpsMainWindow windowModel)
		{
			this.windowModel = windowModel;

			// Create a view for the actions
			actions = new CollectionViewSource();
			actions.Source = Environment.Actions;
			actions.SortDescriptions.Add(new SortDescription("Priority", ListSortDirection.Ascending));
			actions.View.Filter += FilterAction;

			// Create a view for the views
			views = new CollectionViewSource();
			views.Source = Environment.Views;
			views.SortDescriptions.Add(new SortDescription("DisplayName", ListSortDirection.Ascending));
			views.View.CurrentChanged += (sender, e) => UpdateCurrentView((PpsMainViewDefinition)views.View.CurrentItem);

			// init data list
			items = new PpsDataList(Environment);
			itemsView = CollectionViewSource.GetDefaultView(items);
			itemsView.CurrentChanged += (sender, e) => RefreshActions();

			// Update the view
			if (!views.View.MoveCurrentToFirst())
				UpdateCurrentView((PpsMainViewDefinition)views.View.CurrentItem);
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

			RefreshDataAsync();
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

		private void UpdateCurrentExtentedSearch(string searchText)
		{
			// parses the current search text
			// "." -> ui action/macro
			//  ".:" -> results in a lua commant

			CurrentSearchTextExpression newSearchExpression;
			if (String.IsNullOrEmpty(searchText))
				newSearchExpression = EmptyExpression.Instance;
			else if (searchText[0] == '.') // special expression
			{
				if (searchText.Length == 1)
					newSearchExpression = new EmptyExpression(this, searchText, 1);
				else if (searchText[1] == ':') // lua
					newSearchExpression = new LuaExpression(this, searchText, 2);
				else if (Char.IsLetter(searchText[1])) // macro
					newSearchExpression = new MacroExpression(this, searchText, 1);
				else
					newSearchExpression = new SearchExpression(this, searchText, 0);
			}
			else
				newSearchExpression = new SearchExpression(this, searchText, 0);

			var cmp = newSearchExpression.CompareTo(currentSearchTextExpression);
			if (cmp < 0)
				currentSearchTextExpression = newSearchExpression;
			else if (cmp > 0)
			{
				currentSearchTextExpression = newSearchExpression;
				RefreshDataAsync();
			}
		} // proc ReUpdateCurrentExtentedSearch

		public void ClearCurrentSearchText()
		{
			currentSearchTextExpression = EmptyExpression.Instance;
			OnPropertyChanged(nameof(CurrentSearchText));
		} // proc ClearCurrentSearchText

		public void ExecuteCurrentSearchText()
		{
			currentSearchTextExpression.Execute();
		} // proc ExecuteCurrentSearchText

		private async void RefreshDataAsync()
		{
			PpsShellGetList dataSource;
			PpsDataFilterExpression exprView;
			PpsDataFilterExpression exprFilter;
			PpsDataFilterExpression exprSearch;


			// build neu data source
			if (CurrentView == null) // create view from local view
			{
				dataSource = new PpsShellGetList("local.objects");
				exprView = PpsDataFilterTrueExpression.True;
				exprFilter = PpsDataFilterTrueExpression.True;
			}
			else // create a different view
			{
				dataSource = new PpsShellGetList(CurrentView.ViewId);
				exprView = CurrentView.ViewFilterExpression;
				exprFilter = currentFilter?.FilterExpression ?? PpsDataFilterTrueExpression.True;

				if (currentOrder != null)
					dataSource.Order = PpsDataOrderExpression.Parse((sortAscending ? "+" : "-") + currentOrder.Name).ToArray();
			}

			// todo: add search expression
			//var currentSearchExpression = currentSearchTextExpression as SearchExpression;
			exprSearch = PpsDataFilterTrueExpression.True;

			dataSource.Filter = PpsDataFilterExpression.Combine(exprView, exprFilter, exprSearch);

			await items.Reset(dataSource);
		} // proc RefreshDataAsync

		private void RefreshActions()
		{
			actions.View.Refresh();
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
			=> base.OnIndex(key) ?? Environment.GetValue(key); // inherit from the environment

		/// <summary>The environment, that is the owenr of this window.</summary>
		public PpsMainEnvironment Environment => windowModel.Environment;

		[LuaMember(nameof(Window))]
		public PpsMainWindow Window => windowModel;

		/// <summary>Selected item</summary>
		[LuaMember(nameof(CurrentItem))]
		public object CurrentItem => itemsView?.CurrentItem;
		/// <summary>Points to the current special view, that is selected.</summary>
		[LuaMember(nameof(CurrentView))]
		public PpsMainViewDefinition CurrentView => views?.View.CurrentItem as PpsMainViewDefinition;
		/// <summary>Returns the current filters</summary>
		public IEnumerable<PpsFilterView> CurrentFilters => currentFilters;
		/// <summary>Returns the current orders</summary>
		public IEnumerable<PpsOrderView> CurrentOrders => currentOrders;
		/// <summary></summary>
		public ICollectionView VisibleViews => views.View;
		/// <summary></summary>
		public ICollectionView VisibleActions => actions.View;
		/// <summary>Data Items</summary>
		public ICollectionView Items => itemsView;

		/// <summary>Current SearchText</summary>
		public string CurrentSearchText { get { return currentSearchTextExpression?.SearchText; } set { UpdateCurrentExtentedSearch(value); } }
		/// <summary>Is the current search content executable.</summary>
		public bool CanExecuteSearchText => currentSearchTextExpression.IsExecutable;
	} // class PpsNavigatorModel
}
