using Neo.IronLua;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using TecWare.DES.Stuff;

namespace TecWare.PPSn.UI
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public partial class PpsMainWindow : PpsWindow
	{
		/// <summary>Toggles between DataPane and Navigator.</summary>
		public readonly static RoutedCommand NavigatorToggleCommand = new RoutedCommand("NavigatorToggle", typeof(PpsMainWindow));
		/// <summary></summary>
		public readonly static RoutedCommand NavigatorViewsToggleDescriptionCommand = new RoutedCommand("NavigatorViewsToggleDescription", typeof(PpsMainWindow));
		/// <summary></summary>
		public readonly static RoutedCommand RunActionCommand = new RoutedCommand("RunAction", typeof(PpsNavigatorControl));

		private readonly static DependencyProperty NavigatorVisibilityProperty = DependencyProperty.Register("NavigatorVisibility", typeof(Visibility), typeof(PpsMainWindow), new UIPropertyMetadata(Visibility.Visible));
		private readonly static DependencyProperty NavigatorViewsDescriptionVisibilityProperty = DependencyProperty.Register("NavigatorViewsDescriptionVisibility", typeof(Visibility), typeof(PpsMainWindow), new UIPropertyMetadata(Visibility.Visible));
		private readonly static DependencyProperty PaneVisibilityProperty = DependencyProperty.Register("PaneVisibility", typeof(Visibility), typeof(PpsMainWindow), new UIPropertyMetadata(Visibility.Collapsed));
		private readonly static DependencyPropertyKey CurrentPaneKey = DependencyProperty.RegisterReadOnly("CurrentPane", typeof(IPpsWindowPane), typeof(PpsMainWindow), new PropertyMetadata(null));
		private readonly static DependencyProperty CurrentPaneProperty = CurrentPaneKey.DependencyProperty;

		private int windowIndex = -1;
		private PpsWindowApplicationSettings settings;
		private PpsNavigatorModel navigator;

		public PpsMainWindow(int windowIndex)
		{
			this.windowIndex = windowIndex;

			InitializeComponent();

			// initialize settings
			navigator = new PpsNavigatorModel(this);
			settings = new PpsWindowApplicationSettings(this, "main" + windowIndex.ToString());

			// set basic command bindings
			CommandBindings.Add(
				new CommandBinding(PpsWindow.LoginCommand,
					async (sender, e) =>
					{
						e.Handled = true;
						await StartLoginAsync();
					},
					(sender, e) => e.CanExecute = !Environment.IsAuthentificated
				)
			);

			CommandBindings.Add(
				new CommandBinding(NavigatorToggleCommand,
					(sender, e) =>
					{
						IsNavigatorVisible = !IsNavigatorVisible;
						e.Handled = true;
					},
					(sender, e) => e.CanExecute = true
				)
			);

			CommandBindings.Add(
				new CommandBinding(NavigatorViewsToggleDescriptionCommand,
					(sender, e) =>
					{
						IsViewsDescriptionVisible = !IsViewsDescriptionVisible;
						e.Handled = true;
					},
					(sender, e) => e.CanExecute = true
				)
			);


			CommandBindings.Add(
				new CommandBinding(RunActionCommand,
					(sender, e) =>
					{
						((PpsMainActionDefinition)((Button)e.OriginalSource).DataContext).Execute(navigator);
						e.Handled = true;
					}
				)
			);

			this.DataContext = this;

			RefreshTitle();
			Trace.TraceInformation("MainWindow[{0}] created.", windowIndex);
		} // ctor

		private async Task StartLoginAsync()
		{
			var  tmp =  @"http://environment1/local/masks/TestWpfGeneric.xaml";
			//await LoadPaneAsync(typeof(Panes.PpsLoginPane), null);
			await LoadPaneAsync(typeof(PpsGenericWpfWindowPane),
				Procs.CreateLuaTable(
					new KeyValuePair<string, object>("template", tmp)
				)
			);
		} // proc StartLogin

		public async Task LoadPaneAsync(Type paneType, LuaTable arguments)
		{
			// unload the current pane
			if (!await UnloadPaneAsync())
				return;

			// set the new pane
			var ci = paneType.GetConstructors().FirstOrDefault();
			if (ci == null)
				throw new ArgumentException("No ctor"); // todo: exception

			var parameterInfo = ci.GetParameters();
			var paneArguments = new object[parameterInfo.Length];
			for (int i = 0; i < paneArguments.Length; i++)
			{
				var pi = parameterInfo[i];
				if (pi.ParameterType.IsAssignableFrom(typeof(PpsMainEnvironment)))
					paneArguments[i] = Environment;
				else
					throw new ArgumentException("Unsupported argument."); // todo: Exception
			}

			// Create pane
			var currentPane = (IPpsWindowPane)Activator.CreateInstance(paneType, paneArguments);
			SetValue(CurrentPaneKey, currentPane);

			// load the pane
			await currentPane.LoadAsync(arguments);

			// Hide Navigator
			await Dispatcher.InvokeAsync(() =>
			{
				IsNavigatorVisible = false;
				RefreshTitle();
			});
		} // proc StartPaneAsync

		protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
		{
			base.OnPreviewMouseDown(e);
			if (!object.Equals(e.Source, PART_SearchBox))
				CollapseSearchBox();
		}

		protected override void OnPreviewTextInput(TextCompositionEventArgs e)
		{
			base.OnPreviewTextInput(e);
			if (!object.Equals(e.Source, PART_SearchBox))
				e.Handled = ExpandSearchBox(e.Text);
		}

		protected override void OnWindowCaptionClicked()
		{
			CollapseSearchBox();
		}

		#region -- SearchBoxHandling --------------------------------------------------

		private bool ExpandSearchBox(string input)
		{
			if (PART_SearchBox.Visibility != Visibility.Visible || (PpsnSearchBoxState)PART_SearchBox.Tag == PpsnSearchBoxState.Expanded)
				return false;
			if (!char.IsLetterOrDigit(input, 0))
				return false;
			PART_SearchBox.Text += input;
			PART_SearchBox.Select(PART_SearchBox.Text.Length, 0);
			FocusManager.SetFocusedElement(PART_MainWindowGrid, PART_SearchBox);
			Keyboard.Focus(PART_SearchBox);
			return true;
		}

		private void CollapseSearchBox()
		{
			if (PART_SearchBox.Visibility != Visibility.Visible || (PpsnSearchBoxState)PART_SearchBox.Tag == PpsnSearchBoxState.Collapsed)
				return;
			FocusManager.SetFocusedElement(PART_MainWindowGrid, PART_Caption);
			Keyboard.Focus(PART_Caption);
		}

		#endregion

		// TEST Schmidt open ContextMenu CurrentUser with MouseButtonLeft
		private void PART_User_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			e.Handled = true;
			var mouseDownEvent =
				new MouseButtonEventArgs(Mouse.PrimaryDevice, System.Environment.TickCount, MouseButton.Right)
				{
					RoutedEvent = Mouse.MouseUpEvent,
					Source = PART_User,
				};
			InputManager.Current.ProcessInput(mouseDownEvent);
		} // event PART_User_MouseLeftButtonUp

		private async Task<bool> UnloadPaneAsync()
		{
			if (CurrentPane == null)
				return true;
			else
			{
				var currentPane = CurrentPane;
				if (await currentPane.UnloadAsync())
				{
					Procs.FreeAndNil(ref currentPane);
					SetValue(CurrentPaneKey, null);
					return true;
				}
				else
					return false;
			}
		} // proc UnloadPaneAsync
				
		public void RefreshTitle()
		{
			this.Title = CurrentPane == null ? "PPS2000n" : String.Format("PPS2000n - {0}", CurrentPane.Title);
		} // proc RefreshTitle

		/// <summary>Returns the current view of the pane as a wpf control.</summary>
		public IPpsWindowPane CurrentPane => (IPpsWindowPane)GetValue(CurrentPaneProperty);
		/// <summary></summary>
		public PpsNavigatorModel Navigator => navigator;
		/// <summary>Settings of the current window.</summary>
		public PpsWindowApplicationSettings Settings => settings;
		/// <summary>Index of the current window</summary>
		public int WindowIndex => windowIndex; 
		/// <summary>Access to the current environment,</summary>
		public new PpsMainEnvironment Environment => (PpsMainEnvironment)base.Environment;

		public bool IsNavigatorVisible
		{
			get
			{
				return (Visibility)GetValue(NavigatorVisibilityProperty) == Visibility.Visible;
			}
			set
			{
				if (IsNavigatorVisible != value)
				{
					if (value)
					{
						SetValue(NavigatorVisibilityProperty, Visibility.Visible);
						SetValue(PaneVisibilityProperty, Visibility.Collapsed);
					}
					else
					{
						SetValue(NavigatorVisibilityProperty, Visibility.Collapsed);
						SetValue(PaneVisibilityProperty, Visibility.Visible);
					}
				}
			}
		} // prop NavigatorState

		private bool IsViewsDescriptionVisible
		{
			get
			{
				return (Visibility)GetValue(NavigatorViewsDescriptionVisibilityProperty) == Visibility.Visible;
			}
			set
			{
				if (IsViewsDescriptionVisible != value)
				{
					if (value)
					{
						SetValue(NavigatorViewsDescriptionVisibilityProperty, Visibility.Visible);
					}
					else
					{
						SetValue(NavigatorViewsDescriptionVisibilityProperty, Visibility.Collapsed);
					}
				}
			}
		} // prop IsViewsDescriptionVisible

	} // class PpsMainWindow

	#region -- enum PpsnSearchBoxState ------------------------------------------------

	internal enum PpsnSearchBoxState
	{
		Expanded,
		Collapsed
	}

	#endregion
}
