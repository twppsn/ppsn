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
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using Neo.IronLua;
using TecWare.PPSn.Controls;
using TecWare.PPSn.Data;

namespace TecWare.PPSn.UI
{
	#region -- class PpsNavigatorDataModel --------------------------------------------

	internal sealed class PpsNavigatorDataModel : LuaShellTable
	{
		private const int actionCommandGroup = 450;
		private const int actionItemCommandGroup = 451;

		#region -- class CommandScope -------------------------------------------------

		private sealed class CommandScope
		{
			private CommandScope() { }

			public static object List { get; } = new CommandScope();
			public static object Item { get; } = new CommandScope();
		} // class CommandScope

		#endregion

		#region -- class RunActionCommand ---------------------------------------------

		private class RunActionCommand : ICommand
		{
			event EventHandler ICommand.CanExecuteChanged { add { } remove { } }

			private readonly PpsActionDefinition action;
			private readonly PpsNavigatorDataModel model;

			public RunActionCommand(PpsNavigatorDataModel model, PpsActionDefinition action)
			{
				this.model = model ?? throw new ArgumentNullException(nameof(model));
				this.action = action ?? throw new ArgumentNullException(nameof(action));
			} // ctor

			public void Execute(object parameter)
				=> action.Execute(model);

			bool ICommand.CanExecute(object parameter)
				=> IsTrue(action.CheckCondition(model));

			public PpsActionDefinition Action => action;

			public static bool IsTrue(object result)
				=> result is bool l ? l : true;
		} // class RunActionCommand

		#endregion

		private readonly PpsNavigatorPane pane;

		private readonly PpsUICommandCollection globalCommands;
		private readonly PpsUICommandCollection listCommands;
		private readonly PpsUICommandCollection itemCommands;

		private readonly CollectionViewSource viewSource;

		private CollectionViewSource itemsSource;
		private PpsViewDefinition currentView = null;

		#region -- Ctor/Dtor ----------------------------------------------------------

		public PpsNavigatorDataModel(PpsNavigatorPane pane, PpsUICommandCollection  globalCommands, PpsUICommandCollection listCommands, PpsUICommandCollection itemCommands)
			:base(pane.PaneHost.PaneManager.Shell)
		{
			this.pane = pane ?? throw new ArgumentNullException(nameof(pane));

			this.globalCommands = globalCommands ?? throw new ArgumentNullException(nameof(globalCommands));
			this.listCommands = listCommands ?? throw new ArgumentNullException(nameof(listCommands));
			this.itemCommands = itemCommands ?? throw new ArgumentNullException(nameof(itemCommands));

			// view source
			viewSource = new CollectionViewSource { Source = Environment.Views };
			viewSource.Filter += FilterView;
			viewSource.SortDescriptions.Add(new SortDescription(nameof(PpsViewDefinition.DisplayName), ListSortDirection.Ascending));
			viewSource.View.MoveCurrentTo(null);

			// actions
			Environment.Actions.CollectionChanged += Actions_CollectionChanged;
			RefreshItems();
			RefreshActions();
		} // ctor

		private void FilterView(object sender, FilterEventArgs e)
			=> e.Accepted = e.Item is PpsViewDefinition view ? view.IsVisible : false;
		
		#endregion

		#region -- Actions ------------------------------------------------------------

		private void Actions_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			switch (e.Action)
			{
				case NotifyCollectionChangedAction.Reset:
				case NotifyCollectionChangedAction.Add:
				case NotifyCollectionChangedAction.Remove:
					RefreshActions();
					break;
			}
		} // event Actions_CollectionChanged

		private static bool IsActionGroup(PpsUICommandButton cmd)
			=> cmd.Order.Group == actionCommandGroup || cmd.Order.Group == actionItemCommandGroup;

		private static IEnumerable<PpsUICommandButton> GetActionCommands(IEnumerable<PpsUICommand> commands)
			=> commands.OfType<PpsUICommandButton>().Where(IsActionGroup);

		private void AppendCommand(PpsActionDefinition actionDefinition, PpsUICommandCollection commands, List<PpsUICommandButton> removeCommands, int commandGroup)
		{
			var idx = removeCommands.FindIndex(c => c.Command is RunActionCommand r ? r.Action == actionDefinition : false);
			if (idx == -1)
			{
				var btn = commands.AddButton(
					$"{commandGroup};{actionDefinition.Priority}",
					actionDefinition.DisplayImage,
					new RunActionCommand(this, actionDefinition),
					actionDefinition.DisplayName,
					actionDefinition.Description
				);
			}
			else
			{
				var btn = removeCommands[idx];

				btn.Image = actionDefinition.DisplayImage;
				btn.DisplayText = actionDefinition.DisplayName;
				btn.Description = actionDefinition.Description;

				removeCommands.RemoveAt(idx);
			}
		} // proc AppendCommand

		private static void RemoveCommands(PpsUICommandCollection commands, List<PpsUICommandButton> removeCommands)
		{
			foreach (var c in removeCommands)
				commands.Remove(c);
		} // proc RemoveCommands

		private void RefreshActions()
		{
			using (CollectionViewSource.GetDefaultView(globalCommands).DeferRefresh())
			using (CollectionViewSource.GetDefaultView(listCommands).DeferRefresh())
			using (CollectionViewSource.GetDefaultView(itemCommands).DeferRefresh())
			{
				// collect all currently attached commands
				var removeGlobalCommands = new List<PpsUICommandButton>(GetActionCommands(globalCommands));
				var removeListCommands = new List<PpsUICommandButton>(GetActionCommands(listCommands));
				var removeItemCommands = new List<PpsUICommandButton>(GetActionCommands(itemCommands));

				// enumerate all loaded actions
				foreach (var a in Environment.Actions.OfType<PpsActionDefinition>().Where(c => !c.IsHidden))
				{
					var scope = a.CheckCondition(this); // check the current scope
					if (scope is true) // global command
						AppendCommand(a, globalCommands, removeGlobalCommands, actionCommandGroup);
					else if (scope == CommandScope.List) // list command
						AppendCommand(a, listCommands, removeListCommands, actionCommandGroup);
					else if (scope == CommandScope.Item) // item command
						AppendCommand(a, itemCommands, removeItemCommands, actionItemCommandGroup);
				}

				RemoveCommands(globalCommands, removeGlobalCommands);
				RemoveCommands(listCommands, removeListCommands);
				RemoveCommands(itemCommands, removeItemCommands);
			}
		} // proc RefreshActions

		#endregion

		private void RefreshItems()
		{
			var viewId = CurrentView?.ViewId ?? "local.objects";
			var baseFilterExpr = CurrentView?.ViewFilterExpression ?? PpsDataFilterExpression.True;

			if (itemsSource?.View != null)
				itemsSource.View.CurrentChanged -= CurrentItemChanged;

			itemsSource = new CollectionViewSource {
				Source = Environment.GetViewData(new PpsShellGetList(viewId) { Filter = baseFilterExpr })
			};

			itemsSource.View.CurrentChanged += CurrentItemChanged;

			OnPropertyChanged(nameof(ItemsView));
		} // proc RefreshItems

		private void CurrentItemChanged(object sender, EventArgs e)
		{
			RefreshActions();
			OnPropertyChanged(nameof(CurrentItem));
		} // proc CurrentItemChanged

		[LuaMember]
		public object ListCommand
			=> CommandScope.List;

		[LuaMember]
		public object ItemCommand
			=> CommandScope.Item;

		/// <summary>Access current views.</summary>
		[LuaMember]
		public ICollectionView ViewsView { get => viewSource.View; set { } }
		/// <summary>Access current items.</summary>
		[LuaMember]
		public ICollectionView ItemsView { get => itemsSource?.View; set { } }
		/// <summary>Access environment</summary>
		[LuaMember]
		public PpsEnvironment Environment => (PpsEnvironment)base.Shell;
		/// <summary>Access parent-window</summary>
		[LuaMember]
		public PpsMainWindow Window => (PpsMainWindow)pane.PaneHost.PaneManager;
		/// <summary>Selected item</summary>
		[LuaMember]
		public object CurrentItem
		{
			get => itemsSource?.View?.CurrentItem;
			set { }
		} // func CurrentItem

		/// <summary>Points to the current special view, that is selected.</summary>
		[LuaMember]
		public PpsViewDefinition CurrentView
		{
			get => currentView;
			set
			{
				if (currentView != value)
				{
					currentView = value;
					RefreshActions();
					RefreshItems();
				}
			}
		} // prop CurrentView
	} // class PpsNavigatorDataModel

	#endregion

	#region -- class PpsNavigatorPane -------------------------------------------------

	internal partial class PpsNavigatorPane : PpsWindowPaneControl
	{
		public PpsNavigatorPane(IPpsWindowPaneHost paneHost)
			: base(paneHost)
		{
			InitializeComponent();
		} // ctor

		protected override async Task OnLoadAsync(LuaTable args)
		{
			await base.OnLoadAsync(args);

			DataContext = new PpsNavigatorDataModel(this, Commands, itemList.ListCommands, itemList.ItemCommands);
		} // func OnLoadAsync

		private void SideBarFilterChanged(object sender, PpsSideBarFilterChangedEventArgs e)
			=> Model.CurrentView = e.NewFilter as PpsViewDefinition;
		
		public PpsNavigatorDataModel Model => (PpsNavigatorDataModel)DataContext;
	} // class PpsNavigatorPane

	#endregion
}
