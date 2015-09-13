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
using System.Reflection;

namespace TecWare.PPSn.UI
{
	#region -- enum PpsNewWindowMode ----------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public enum PpsNewWindowMode
	{
		Default,
		UseThisWindow,
		NewMainWindow,
		NewSingleWindow
	} // enum PpsNewWindowMode

	#endregion

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public partial class PpsMainWindow : PpsWindow
	{
		/// <summary>Toggles between DataPane and Navigator.</summary>
		public readonly static RoutedCommand NavigatorToggleCommand = new RoutedCommand("NavigatorToggle", typeof(PpsMainWindow));
		/// <summary>Toggles the description texts in the navigator panel.</summary>
		public readonly static RoutedCommand NavigatorViewsToggleDescriptionCommand = new RoutedCommand("NavigatorViewsToggleDescription", typeof(PpsMainWindow));
		/// <summary>Command to run a dynamic command in the navigator.</summary>
		public readonly static RoutedCommand RunActionCommand = new RoutedCommand("RunAction", typeof(PpsNavigatorControl));

		private readonly static DependencyProperty NavigatorVisibilityProperty = DependencyProperty.Register("NavigatorVisibility", typeof(Visibility), typeof(PpsMainWindow), new UIPropertyMetadata(Visibility.Visible));
		private readonly static DependencyProperty NavigatorViewsDescriptionVisibilityProperty = DependencyProperty.Register("NavigatorViewsDescriptionVisibility", typeof(Visibility), typeof(PpsMainWindow), new UIPropertyMetadata(Visibility.Visible));
		private readonly static DependencyProperty PaneVisibilityProperty = DependencyProperty.Register("PaneVisibility", typeof(Visibility), typeof(PpsMainWindow), new UIPropertyMetadata(Visibility.Collapsed));
		private readonly static DependencyProperty CharmbarVisibilityProperty = DependencyProperty.Register("CharmbarVisibility", typeof(Visibility), typeof(PpsMainWindow), new UIPropertyMetadata(Visibility.Collapsed));
		private readonly static DependencyProperty CharmbarActualWidthProperty = DependencyProperty.Register("CharmbarActualWidth", typeof(double), typeof(PpsMainWindow));

		/// <summary>Readonly property for the current pane.</summary>
		private readonly static DependencyPropertyKey CurrentPaneKey = DependencyProperty.RegisterReadOnly("CurrentPane", typeof(IPpsWindowPane), typeof(PpsMainWindow), new PropertyMetadata(null));
		private readonly static DependencyProperty CurrentPaneProperty = CurrentPaneKey.DependencyProperty;

		private int windowIndex = -1;										// settings key
		private PpsWindowApplicationSettings settings;	// current settings for the window
		private PpsNavigatorModel navigator;						// base model for the navigator content

		#region -- Ctor/Dtor --------------------------------------------------------------

		public PpsMainWindow(int windowIndex)
		{
			this.windowIndex = windowIndex;

			InitializeComponent();

			// initialize settings
			navigator = new PpsNavigatorModel(this);
			settings = new PpsWindowApplicationSettings(this, "main" + windowIndex.ToString());

			#region -- set basic command bindings --
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
				new CommandBinding(PpsWindow.TraceLogCommand,
					async	(sender, e) =>
					{
						e.Handled = true;
						await LoadPaneAsync(Environment.TracePane, PpsNewWindowMode.NewSingleWindow, null);
					}
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

			#endregion

			this.DataContext = this;

			RefreshTitle();
			Trace.TraceInformation("MainWindow[{0}] created.", windowIndex);

			var descriptor = DependencyPropertyDescriptor.FromProperty(PpsCharmbarControl.ActualWidthProperty, typeof(PpsCharmbarControl));
			descriptor.AddValueChanged(PART_Charmbar, OnCharmbarActualWidthChanged);
		} // ctor

		#endregion



		#region -- LoadPaneAsync ----------------------------------------------------------

		private async Task StartLoginAsync()
		{
			await LoadPaneAsync(typeof(Panes.PpsLoginPane), null);
		} // proc StartLogin

		private IPpsWindowPane CreateEmptyPane(Type paneType)
		{
			var ti = paneType.GetTypeInfo();
			var ctorBest = (ConstructorInfo)null;
			var ctoreBestParamLength = -1;

			// search for the longest constructor
			foreach (var ci in ti.GetConstructors())
			{
				var pi = ci.GetParameters();
				if (ctoreBestParamLength < pi.Length)
				{
					ctorBest = ci;
					ctoreBestParamLength = pi.Length;
				}
			}
			if (ctorBest == null)
				throw new ArgumentException($"'{ti.Name}' has no constructor.");

			// create the argument set
			var parameterInfo = ctorBest.GetParameters();
			var paneArguments = new object[parameterInfo.Length];

			for (int i = 0; i < paneArguments.Length; i++)
			{
				var pi = parameterInfo[i];
				var tiParam = pi.ParameterType.GetTypeInfo();
				if (tiParam.IsAssignableFrom(typeof(PpsMainEnvironment)))
					paneArguments[i] = Environment;
				else if (pi.HasDefaultValue)
					paneArguments[i] = pi.DefaultValue;
				else
					throw new ArgumentException($"Unsupported argument '{pi.Name}' for type '{ti.Name}'.");
			}

			// activate the pane
			return (IPpsWindowPane)Activator.CreateInstance(paneType, paneArguments);
		} // func CreateEmptyPane

		/// <summary>Loads a new current pane.</summary>
		/// <param name="paneType">Type of the pane to load.</param>
		/// <param name="arguments">Argument set for the pane</param>
		/// <returns></returns>
		public Task LoadPaneAsync(Type paneType, LuaTable arguments)
		{
			return LoadPaneAsync(paneType, PpsNewWindowMode.Default, arguments);
		} // func LoadPaneAsync

		/// <summary>Loads a new current pane.</summary>
		/// <param name="paneType">Type of the pane to load.</param>
		/// <param name="newWindowMode"></param>
		/// <param name="arguments">Argument set for the pane</param>
		/// <returns></returns>
		public async Task LoadPaneAsync(Type paneType, PpsNewWindowMode newWindowMode, LuaTable arguments)
		{
			if (newWindowMode == PpsNewWindowMode.Default)
				newWindowMode = (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) ? PpsNewWindowMode.NewMainWindow : PpsNewWindowMode.UseThisWindow;

			switch (newWindowMode)
			{
				case PpsNewWindowMode.NewMainWindow:
				case PpsNewWindowMode.NewSingleWindow:
					{
						var newWindow = Environment.GetWindows().FirstOrDefault(c => ComparePane(c.CurrentPane, paneType, arguments) == PpsWindowPaneCompareResult.Same);
						if (newWindow == null)
						{
							newWindow = await Environment.CreateMainWindowAsync();
							await newWindow.LoadPaneInternAsync(paneType, arguments);
						}
						newWindow.Activate();
					}
					break;
				default:
					await LoadPaneInternAsync(paneType, arguments);
					break;
			}
		} // proc StartPaneAsync

		private async Task LoadPaneInternAsync(Type paneType, LuaTable arguments)
		{
			var currentPane = CurrentPane;
			if (arguments == null)
				arguments = new LuaTable();

			try
			{
				// check, if we have to initialize a new pane
				PpsWindowPaneCompareResult loadMode = ComparePane(currentPane, paneType, arguments);

				// do the load mode
				switch (loadMode)
				{
					case PpsWindowPaneCompareResult.Incompatible:

						// unload the current pane
						if (!await UnloadPaneAsync())
							return;

						// Build the new pane
						currentPane = CreateEmptyPane(paneType);
						SetValue(CurrentPaneKey, currentPane);

						goto case PpsWindowPaneCompareResult.Reload;

					case PpsWindowPaneCompareResult.Reload:

						// load or reload data
						await currentPane.LoadAsync(arguments);
						goto case PpsWindowPaneCompareResult.Same;

					case PpsWindowPaneCompareResult.Same:

						// Hide Navigator
						await Dispatcher.InvokeAsync(() =>
						{
							IsNavigatorVisible = false;
							RefreshTitle();
						});
						break;
				}
			}
			catch (Exception e)
			{
				await Environment.ShowExceptionAsync(ExceptionShowFlags.None, e, "Die Ansicht konnte nicht geladen werden.");
			}
		} // proc LoadPaneInternAsync

		private static PpsWindowPaneCompareResult ComparePane(IPpsWindowPane currentPane, Type paneType, LuaTable arguments)
		{
			var loadMode = PpsWindowPaneCompareResult.Incompatible;
			if (currentPane != null)
			{
				if (currentPane.GetType() == paneType)
					loadMode = currentPane.CompareArguments(arguments);
			}
			return loadMode;
		} // func ComparePane

		/// <summary>Unloads the current pane, to a empty pane.</summary>
		/// <returns></returns>
		public async Task<bool> UnloadPaneAsync()
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

		#endregion

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

		#region -- charmbar -----------------------------------------------------------------

		private void OnCharmbarActualWidthChanged(object sender, EventArgs e)
		{
			SetActualWidth(PART_Charmbar.ActualWidth);
		}

		private void SetActualWidth(double value)
		{
			SetValue(CharmbarActualWidthProperty, value);
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
		/// <summary>Access to current charmbar width</summary>
		public double CharmbarActualWidth { get { return (double)GetValue(CharmbarActualWidthProperty); } }

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
						SetValue(CharmbarVisibilityProperty, Visibility.Collapsed);
					}
					else
					{
						SetValue(NavigatorVisibilityProperty, Visibility.Collapsed);
						SetValue(PaneVisibilityProperty, Visibility.Visible);
						SetValue(CharmbarVisibilityProperty, Visibility.Visible);
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
