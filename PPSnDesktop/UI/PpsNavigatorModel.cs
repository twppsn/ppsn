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

			public CurrentSearchTextExpression(PpsNavigatorModel navigator, bool forceNavigator, string searchText, int searchTextOffset)
			{
				if (forceNavigator && navigator == null)
					throw new ArgumentNullException("navigator");

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

			public virtual bool IsValid => !String.IsNullOrEmpty(ExceptionText);
			public virtual string ExceptionText => null;
			public virtual int ExceptionPosition => -1;

			public PpsNavigatorModel Navigator => navigator;
		} // class CurrentSearchTextExpression

		#endregion

		#region -- class EmptyExpression --------------------------------------------------

		private sealed class EmptyExpression : CurrentSearchTextExpression
		{
			private EmptyExpression()
				: base(null, false, String.Empty, 0)
			{
			} // ctor

			public EmptyExpression(string searchText, int searchTextOffset)
				: base(null, false, searchText, searchTextOffset)
			{
			} // ctor

			public static EmptyExpression Instance { get; } = new EmptyExpression();
		} // class EmptyExpression

		#endregion

		#region -- class SearchExpression -------------------------------------------------

		private sealed class SearchExpression : CurrentSearchTextExpression
		{
			private readonly PpsDataFilterExpression filterExpression;
			private readonly string exceptionText;

			public SearchExpression(PpsNavigatorModel navigator, string searchText, int searchTextOffset)
				: base(navigator, true, searchText, searchTextOffset)
			{
				try
				{
					this.filterExpression = PpsDataFilterExpression.Parse(searchText, searchTextOffset);
				}
				catch (Exception e)
				{
					navigator.Environment.Traces.AppendText(PpsTraceItemType.Debug, $"Parse of search expression failed: {e.Message}");
					exceptionText = e.Message;
				}
			} // ctor

			public PpsDataFilterExpression FilterExpression => filterExpression;
			public override string ExceptionText => exceptionText;
		} // class SearchExpression

		#endregion

		#region -- class MacroExpression --------------------------------------------------

		private sealed class MacroExpression : CurrentSearchTextExpression
		{
			private readonly PpsMainActionDefinition actionDefinition;

			public MacroExpression(PpsNavigatorModel navigator, string searchText, int searchTextOffset)
				: base(navigator, true, searchText, searchTextOffset)
			{
				this.actionDefinition = navigator.Environment.Actions[Data];
			} // ctor

			public override void Execute()
			{
				if (IsExecutable)
				{
					actionDefinition.Execute(Navigator);
					Navigator.ClearCurrentSearchText();
				}
			} // proc Execute

			public override bool IsExecutable
				=> actionDefinition != null;
		} // class MacroExpression

		#endregion

		#region -- class LuaExpression ----------------------------------------------------

		private sealed class LuaExpression : CurrentSearchTextExpression
		{
			private readonly LuaChunk chunk;
			private readonly string exceptionText;
			private readonly int exceptionPosition;

			public LuaExpression(PpsNavigatorModel navigator, string searchText, int searchTextOffset)
				: base(navigator, true, searchText, searchTextOffset)
			{
				if (String.IsNullOrWhiteSpace(Data))
					chunk = null;
				else // compile to check for syntax
				{
					try
					{
						var code = Data;
						if (code.StartsWith("="))
							code = "msgbox(" + code.Substring(1) + ")";

						chunk = navigator.Environment.CreateLuaChunk(code);
					}
					catch (LuaParseException e)
					{
						navigator.Environment.Traces.AppendText(PpsTraceItemType.Debug, String.Format("Syntax: {0} at {1}", e.Message, e.Index + searchTextOffset));

						exceptionText = e.Message;
						exceptionPosition = (int)e.Index + searchTextOffset;
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

			public override string ExceptionText => exceptionText;
			public override int ExceptionPosition => exceptionPosition;
		} // class LuaExpression

		#endregion

		#region -- class PpsFilterView ----------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		public class PpsFilterView : ObservableObject, ICommand
		{
			public event EventHandler CanExecuteChanged { add { } remove { } }

			private readonly PpsNavigatorModel model;
			private readonly PpsMainViewFilter filter;

			internal PpsFilterView(PpsNavigatorModel model, PpsMainViewFilter filter)
			{
				this.model = model;
				this.filter = filter;
			} // ctor

			public void Execute(object parameter)
			{
				model.UpdateCurrentFilter(IsSelected ? null : this);
				Task.Run(model.RefreshDataAsync); // reload data
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

			private readonly PpsNavigatorModel model;
			private readonly PpsMainViewOrder order;

			internal PpsOrderView(PpsNavigatorModel model, PpsMainViewOrder order)
			{
				this.model = model;
				this.order = order;
			} // ctor

			public void Execute(object parameter)
			{
				model.UpdateCurrentOrder(this);
				Task.Run(model.RefreshDataAsync);
			}
			public bool CanExecute(object parameter) => true;

			internal void FireIsCheckedChanged()
				=> OnPropertyChanged(nameof(IsChecked));

			public string Name => order.Name;
			public string DisplayName => order.DisplayName;
			public bool? IsChecked => model.currentOrder == this ? (bool?)model.sortAscending : null;
		} // class PpsOrderView

		#endregion

		#region -- class SearchActionCommand ----------------------------------------------

		private class SearchActionCommand : ICommand
		{
			public event EventHandler CanExecuteChanged;

			private readonly PpsNavigatorModel model;

			internal SearchActionCommand(PpsNavigatorModel model)
			{
				this.model = model;
			} // ctor

			public void Execute(object parameter)
				=> model.ExecuteCurrentSearchText();

			public void DoCanExecuteChanged()
				=> CanExecuteChanged?.Invoke(this, EventArgs.Empty);

			public bool CanExecute(object parameter)
				=> model.currentSearchTextExpression?.IsExecutable ?? false;
		} // class SearchActionCommand

		#endregion

		#region -- class RunActionCommand -------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private class RunActionCommand : ICommand
		{
			event EventHandler ICommand.CanExecuteChanged { add { } remove { } }

			private readonly PpsNavigatorModel model;

			internal RunActionCommand(PpsNavigatorModel model)
			{
				this.model = model;
			} // ctor

			private PpsMainActionDefinition GetActionFromParameter(object parameter)
				=> parameter as PpsMainActionDefinition;

			public void Execute(object parameter)
				=> GetActionFromParameter(parameter)?.Execute(model);

			bool ICommand.CanExecute(object parameter)
				=> GetActionFromParameter(parameter)?.CheckCondition(model) ?? true;

		} // class RunActionCommand

		#endregion

		#region -- class ToggleShowViewDescriptionCommand ---------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private class ToggleShowViewDescriptionCommand : ICommand
		{
			public event EventHandler CanExecuteChanged { add { } remove { } }

			private readonly PpsNavigatorModel model;

			internal ToggleShowViewDescriptionCommand(PpsNavigatorModel model)
			{
				this.model = model;
			} // ctor

			public void Execute(object parameter)
			{
				var currentShow = model.ViewsShowDescription;
				model.ShowViewsDescription(!currentShow);
			} // proc Execute

			public bool CanExecute(object parameter)
				=> true;
		} // class ToggleShowViewDescriptionCommand

		#endregion

		private readonly PpsMainWindow windowModel;

		private CollectionViewSource views;
		private CollectionViewSource actions;

		private PpsFilterView[] currentFilters;
		private PpsOrderView[] currentOrders;
		private PpsFilterView currentFilter;
		private PpsOrderView currentOrder;
		private bool sortAscending = true;

		private PpsDataList items;
		private ICollectionView itemsView;

		private readonly SearchActionCommand searchActionCommand;
		private CurrentSearchTextExpression currentSearchTextExpression = EmptyExpression.Instance;

		private readonly RunActionCommand runActionCommand;
		private readonly ToggleShowViewDescriptionCommand toggleShowViewDescriptionCommand;
		private bool viewsShowDescription = true;

		private readonly EventHandler onlineChanged;

		#region -- Ctor/Dtor --------------------------------------------------------------

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
			views.View.Filter += FilterView;
			views.View.CurrentChanged += (sender, e) => UpdateCurrentView((PpsMainViewDefinition)views.View.CurrentItem);

			// init data list
			items = new PpsDataList(Environment);
			itemsView = CollectionViewSource.GetDefaultView(items);
			itemsView.CurrentChanged += (sender, e) => RefreshActions();

			// Create Command
			toggleShowViewDescriptionCommand = new ToggleShowViewDescriptionCommand(this);
			runActionCommand = new RunActionCommand(this);
			searchActionCommand = new SearchActionCommand(this);

			onlineChanged = Environment_IsOnlineChanged;
			Environment.IsOnlineChanged += onlineChanged;
			Window.Closed += Window_Closed;

			// Update the view, no selection
			if (views.View.CurrentItem != null)
				views.View.MoveCurrentToPosition(-1);
			else
				UpdateCurrentView(null);
		} // ctor

		private bool FilterAction(object item)
		{
			var action = item as PpsMainActionDefinition;
			return action != null &&
				!action.IsHidden &&
				action.CheckCondition(this);
		} // func FilterAction

		private bool FilterView(object item)
			=> (item as PpsMainViewDefinition)?.IsVisible ?? false;

		private void Window_Closed(object sender, EventArgs e)
		{
			Environment.IsOnlineChanged -= onlineChanged;
		} // event Window_Closed

		private void Environment_IsOnlineChanged(object sender, EventArgs e)
		{
			views.View.Refresh();
			UpdateCurrentView(null);
		} // event Environment_IsOnlineChanged

		#endregion

		#region -- Update UI - View, Filter, Order ----------------------------------------

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

			// refresh lists
			RefreshActions();

			Task.Run(RefreshDataAsync)
				.ContinueWith(async r => await Window.Dispatcher.InvokeAsync(RefreshActions));
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

		#endregion

		#region -- Update Extended Search Text --------------------------------------------

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
					newSearchExpression = new EmptyExpression(searchText, 1);
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
			if (cmp != 0)
			{
				var resetData = newSearchExpression is SearchExpression || currentSearchTextExpression is SearchExpression;

				currentSearchTextExpression = newSearchExpression; // set the new expression
				OnPropertyChanged(nameof(CurrentSearchExpression));
				searchActionCommand.DoCanExecuteChanged();

				if (resetData) // new search expression, redo filter
					Task.Run(RefreshDataAsync); // start refresh in background
			}
		} // proc UpdateCurrentExtentedSearch

		[LuaMember(nameof(ClearCurrentSearchText))]
		public void ClearCurrentSearchText()
		{
			currentSearchTextExpression = EmptyExpression.Instance;
			OnPropertyChanged(nameof(CurrentSearchText));
			searchActionCommand.DoCanExecuteChanged();
		} // proc ClearCurrentSearchText

		[LuaMember(nameof(ExecuteCurrentSearchText))]
		public void ExecuteCurrentSearchText()
			=> currentSearchTextExpression.Execute();

		#endregion

		#region -- RefreshDataAsync -------------------------------------------------------

		private PpsShellGetList CreateDataSource()
		{
			PpsShellGetList dataSource;
			PpsDataFilterExpression exprView;
			PpsDataFilterExpression exprFilter;
			PpsDataFilterExpression exprSearch;

			// build new data source
			if (CurrentView == null) // create view from local view
			{
				dataSource = new PpsShellGetList("local.objects");
				exprView = PpsDataFilterTrueExpression.True;
				exprFilter = PpsDataFilterTrueExpression.True;
			}
			else // create a different view, could be remote
			{
				dataSource = new PpsShellGetList(CurrentView.ViewId);
				exprView = CurrentView.ViewFilterExpression;
				exprFilter = currentFilter?.FilterExpression ?? PpsDataFilterTrueExpression.True;

				if (currentOrder != null)
					dataSource.Order = PpsDataOrderExpression.Parse((sortAscending ? "+" : "-") + currentOrder.Name).ToArray();
			}

			// add search expression
			var currentSearchExpression = currentSearchTextExpression as SearchExpression;
			exprSearch = currentSearchExpression != null ?
				currentSearchExpression.FilterExpression :
				PpsDataFilterTrueExpression.True;

			// combine the contitions
			dataSource.Filter = PpsDataFilterExpression.Combine(exprView, exprFilter, exprSearch);
			return dataSource;
		} // func CreateDataSource

		private async Task RefreshDataAsync()
		{
			var dataSource = await Window.Dispatcher.InvokeAsync(CreateDataSource);
			await items.Reset(dataSource);
		} // proc RefreshDataAsync

		#endregion

		#region -- Actions ----------------------------------------------------------------

		private void RefreshActions()
			=> actions.View.Refresh();

		[
			LuaMember(nameof(ExecuteAction)),
			Description("Executes the action by name.")
		]
		public LuaResult ExecuteAction(string actionName)
			=> Environment.Actions[actionName]?.Execute(this);

		#endregion

		private void ShowViewsDescription(bool show)
		{
			if (show == viewsShowDescription)
				return;
			viewsShowDescription = show;
			OnPropertyChanged(nameof(ViewsShowDescription));
		} // proc ShowViewsDescription

		[LuaMember(nameof(IsItemType))]
		public bool IsItemType(object item, string typ)
		{
			var t = item as LuaTable;
			if (t == null)
				return false;
			else
			{
				var itemTyp = t.GetOptionalValue("Typ", String.Empty);
				return itemTyp == typ;
			}
		} // func IsItemType

		protected override object OnIndex(object key)
			=> base.OnIndex(key) ?? Environment.GetValue(key); // inherit from the environment

		/// <summary>The environment, that is the owner of this window.</summary>
		[LuaMember(nameof(Environment))]
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
		[LuaMember(nameof(CurrentSearchText))]
		public string CurrentSearchText { get { return currentSearchTextExpression?.SearchText; } set { UpdateCurrentExtentedSearch(value); } }
		/// <summary>Parsed expression object.</summary>
		[LuaMember(nameof(CurrentSearchExpression))]
		public object CurrentSearchExpression => currentSearchTextExpression;

		/// <summary>Command for the search code execution.</summary>
		public ICommand SearchAction => searchActionCommand;

		/// <summary>Command to run action</summary>
		public ICommand RunAction => runActionCommand;
		/// <summary>Command to toggle description visibility</summary>
		public ICommand ToggleShowViewDescription => toggleShowViewDescriptionCommand;
		/// <summary>Are Descriptions in views visible?</summary>
		public bool ViewsShowDescription => viewsShowDescription;
	} // class PpsNavigatorModel
}