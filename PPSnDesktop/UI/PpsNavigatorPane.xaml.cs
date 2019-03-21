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
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Neo.IronLua;
using TecWare.PPSn.Controls;
using TecWare.PPSn.Data;

namespace TecWare.PPSn.UI
{
	#region -- enum PpsNavigationFilterMode -------------------------------------------

	internal enum PpsNavigationFilterMode
	{
		None,
		Filter,
		Macro,
		Lua
	} // enum FilterMode

	#endregion

	#region -- class PpsNavigationExecuteEventArgs ------------------------------------

	internal class PpsNavigationExecuteEventArgs : RoutedEventArgs
	{
		public PpsNavigationExecuteEventArgs(RoutedEvent routedEvent, string command)
			: base(routedEvent)
		{
			this.Command = command;
		} // ctor

		public string Command { get; }
		public bool IsFailed { get; set; } = false;
	} // class PpsNavigationExecuteEventArgs

	internal delegate void PpsNavigationExecuteEventHandler(object sender, PpsNavigationExecuteEventArgs e);

	#endregion

	#region -- class PpsNavigationListBox ---------------------------------------------

	internal class PpsNavigationListBox : PpsDataListBox
	{
		public static readonly RoutedEvent ExecuteMacroEvent = EventManager.RegisterRoutedEvent("Macro", RoutingStrategy.Bubble, typeof(PpsNavigationExecuteEventHandler), typeof(PpsNavigationListBox));
		public static readonly RoutedEvent ExecuteLuaEvent = EventManager.RegisterRoutedEvent("Lua", RoutingStrategy.Bubble, typeof(PpsNavigationExecuteEventHandler), typeof(PpsNavigationListBox));

		#region -- FilterMode - property ----------------------------------------------

		private static readonly DependencyPropertyKey filterModePropertyKey = DependencyProperty.RegisterReadOnly(nameof(FilterMode), typeof(PpsNavigationFilterMode), typeof(PpsNavigationListBox), new FrameworkPropertyMetadata(PpsNavigationFilterMode.None));
		public static readonly DependencyProperty FilterModeProperty = filterModePropertyKey.DependencyProperty;

		public PpsNavigationFilterMode FilterMode => (PpsNavigationFilterMode)GetValue(FilterModeProperty);

		#endregion

		public PpsNavigationListBox()
		{
			CommandBindings.Add(new CommandBinding(PpsControlCommands.ExecuteCommand,
				(sender, e) =>
				{
					switch (FilterMode)
					{
						case PpsNavigationFilterMode.Macro:
							if (TryExecute(ExecuteMacroEvent, GetMacroText(UserFilterText)))
								ClearValue(UserFilterTextProperty);
							break;
						case PpsNavigationFilterMode.Lua:
							if (TryExecute(ExecuteLuaEvent, GetLuaText(UserFilterText)))
								ClearValue(UserFilterTextProperty);
							break;
					}
					e.Handled = true;
				},
				(sender, e) =>
				{
					e.CanExecute = FilterMode == PpsNavigationFilterMode.Lua || FilterMode == PpsNavigationFilterMode.Macro;
					e.Handled = true;
				}
			));
		} // ctor

		private string GetLuaText(string userFilterText)
			=> userFilterText.Substring(2);

		private string GetMacroText(string userFilterText)
			=> userFilterText.Substring(1).Trim();

		private bool TryExecute(RoutedEvent ev, string command)
		{
			var e = new PpsNavigationExecuteEventArgs(ev, command);
			RaiseEvent(e);
			return e.Handled && !e.IsFailed;
		} // func TryExecute

		public override void OnApplyTemplate()
		{
			base.OnApplyTemplate();

			Filterbox.InputBindings.Add(new KeyBinding(PpsControlCommands.ExecuteCommand, new KeyGesture(Key.Enter)));
		} // proc OnApplyTemplate

		protected override void OnUserFilterTextChanged(string newValue, string oldValue)
		{
			if (!String.IsNullOrEmpty(newValue) && newValue.Length > 2)
			{
				if (newValue[0] == '.') // macro
				{
					if (newValue[1] == ':') // lua
						SetValue(filterModePropertyKey, PpsNavigationFilterMode.Lua);
					else
						SetValue(filterModePropertyKey, PpsNavigationFilterMode.Macro);
				}
				else
					SetValue(filterModePropertyKey, PpsNavigationFilterMode.Filter);
			}
			else
				SetValue(filterModePropertyKey, PpsNavigationFilterMode.None);

			base.OnUserFilterTextChanged(newValue, oldValue);
		} // proc OnUserFilterTextChanged

		protected override PpsDataFilterExpression GetUserFilterExpression()
			=> FilterMode == PpsNavigationFilterMode.Filter ? base.GetUserFilterExpression() : PpsDataFilterExpression.True;
	} // class PpsNavigationListBox

	#endregion

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

		public PpsNavigatorDataModel(PpsNavigatorPane pane, PpsUICommandCollection globalCommands, PpsUICommandCollection listCommands, PpsUICommandCollection itemCommands)
			: base(pane.PaneHost.PaneManager.Shell)
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

			itemsSource = new CollectionViewSource
			{
				// Source = Environment.MasterData.GetTable("Objects") 
				Source = Environment.GetViewData(new PpsShellGetList(viewId) { Filter = baseFilterExpr })
			};

			itemsSource.View.CurrentChanged += CurrentItemChanged;

			OnPropertyChanged(nameof(ItemsView));
		} // proc RefreshItems

		[
		LuaMember(nameof(ExecuteAction)),
		Description("Executes the action by name.")
		]
		public LuaResult ExecuteAction(string actionName)
		{
			var (isExecuted, result) = ExecuteMarcoAsync(actionName).AwaitTask();
			return new LuaResult(isExecuted, new LuaResult(result));
		} // func ExecuteAction

		public Task<(bool, object)> ExecuteMarcoAsync(string actionName)
		{
			var actionDefinition = Environment.Actions[actionName, false];
			if (actionDefinition == null)
				return Task.FromResult((false, (object)String.Format("Befehl {0} nicht gefunden.", actionName)));

			if (!RunActionCommand.IsTrue(actionDefinition.CheckCondition(this)))
				return Task.FromResult((false, (object)String.Format("Befehl {0} ist nicht aktiv.", actionDefinition.DisplayName)));

			return Task.FromResult((true, (object)actionDefinition.Execute(this)));
		} // func ExecuteMarcoAsync

		public async Task<LuaResult> ExecuteCodeAsync(string code)
		{
			// replace equal with message box
			if (code.StartsWith("="))
				code = "return " + code.Substring(1);

			// compile code
			var chunk = await Environment.CompileAsync(code, "searchbox.lua", true);
			return chunk.Run(this);
		} // proc ExecuteCode

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
		  /// <summary>Current SearchText</summary>
		[LuaMember(nameof(CurrentSearchText))]
		public string CurrentSearchText
		{
			get => pane.itemList?.UserFilterText;
			set
			{
				if (pane.itemList != null)
					pane.itemList.UserFilterText = value;
			}
		} // func CurrentSearchText

	} // class PpsNavigatorDataModel

	#endregion

	#region -- class PpsNavigatorPane -------------------------------------------------

	internal partial class PpsNavigatorPane : PpsWindowPaneControl
	{
		public PpsNavigatorPane(IPpsWindowPaneHost paneHost)
			: base(paneHost)
		{
			InitializeComponent();

			AddHandler(PpsNavigationListBox.ExecuteMacroEvent, new PpsNavigationExecuteEventHandler(OnExecuteEvent), false);
			AddHandler(PpsNavigationListBox.ExecuteLuaEvent, new PpsNavigationExecuteEventHandler(OnExecuteEvent), false);
		} // ctor

		private void ShowResult(LuaResult result)
		{
			if (result.Count > 0)
			{
				var msg = String.Format("Ergebnis:\n{0}", result);
				Model.Environment.ShowMessage(msg);
			}
		} // proc ShowResult

		private void OnExecuteEvent(object sender, PpsNavigationExecuteEventArgs e)
		{
			try
			{
				if (e.RoutedEvent == PpsNavigationListBox.ExecuteMacroEvent)
				{
					var (executed, res) = Model.ExecuteMarcoAsync(e.Command).AwaitTask();
					if (executed)
						ShowResult(new LuaResult(res));
					else
					{
						Model.Environment.ShowMessage(res is string msg ? msg : String.Format("Befehl '{0}' nicht ausgeführt.", e.Command));
						e.IsFailed = true;
					}
				}
				else if (e.RoutedEvent == PpsNavigationListBox.ExecuteLuaEvent)
					ShowResult(Model.ExecuteCodeAsync(e.Command).AwaitTask());
			}
			catch (LuaParseException ex)
			{
				e.IsFailed = true;
				Model.Environment.ShowMessage(String.Format("Syntax: {0} at {1}", ex.Message, ex.Index + 2));
			}
			catch (Exception ex)
			{
				e.IsFailed = true;
				Model.Environment.ShowException(ExceptionShowFlags.None, ex, "Execution failed.\n" + ex.GetBaseException().Message);
			}
		} // proc OnExecuteEvent

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
