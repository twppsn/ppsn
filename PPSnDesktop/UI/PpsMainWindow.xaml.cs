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
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using TecWare.PPSn.Controls;

namespace TecWare.PPSn.UI
{
	/// <summary></summary>
	public partial class PpsMainWindow : PpsWindow
	{
		/// <summary>Toggles between DataPane and Navigator.</summary>
		public readonly static RoutedCommand NavigatorToggleCommand = new RoutedCommand("NavigatorToggle", typeof(PpsMainWindow));
		public readonly static RoutedCommand NextPaneCommand = new RoutedCommand("NextPane", typeof(PpsMainWindow));
		public readonly static RoutedCommand PrevPaneCommand = new RoutedCommand("PrevPane", typeof(PpsMainWindow));
		public readonly static RoutedCommand GoToPaneCommand = new RoutedCommand("GoToPane", typeof(PpsMainWindow));
		public readonly static RoutedCommand ClosePaneCommand = new RoutedCommand("ClosePane", typeof(PpsMainWindow));

		public readonly static DependencyProperty CharmObjectProperty = DependencyProperty.Register(nameof(CharmObject), typeof(object), typeof(PpsMainWindow), new FrameworkPropertyMetadata(null));

		public readonly static DependencyProperty IsNavigatorVisibleProperty = DependencyProperty.Register(nameof(IsNavigatorVisible), typeof(bool), typeof(PpsMainWindow), new FrameworkPropertyMetadata(true, NavigatorVisibleChanged));
		public readonly static DependencyProperty IsPaneVisibleProperty = DependencyProperty.Register(nameof(IsPaneVisible), typeof(bool), typeof(PpsMainWindow), new FrameworkPropertyMetadata(false));
		private readonly static DependencyPropertyKey IsSideBarVisiblePropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsSideBarVisible), typeof(bool), typeof(PpsMainWindow), new FrameworkPropertyMetadata(true));
		public readonly static DependencyProperty IsSideBarVisibleProperty = IsSideBarVisiblePropertyKey.DependencyProperty;

		/// <summary>Readonly property for the current pane.</summary>
		private readonly static DependencyPropertyKey CurrentPaneKey = DependencyProperty.RegisterReadOnly("CurrentPane", typeof(IPpsWindowPane), typeof(PpsMainWindow), new FrameworkPropertyMetadata(null, CurrentPaneChanged));
		public readonly static DependencyProperty CurrentPaneProperty = CurrentPaneKey.DependencyProperty;

		private int windowIndex = -1;                                       // settings key
		private PpsWindowApplicationSettings settings;                      // current settings for the window

		#region -- Ctor/Dtor -------------------------------------------------------------

		public PpsMainWindow(int windowIndex)
		{
			this.windowIndex = windowIndex;

			InitializeComponent();

			// initialize settings
			settings = new PpsWindowApplicationSettings(this, "main" + windowIndex.ToString());
			navigator.Init(this);

			// set sidebar background logic
			navigator.NavigatorModel.PropertyChanged += OnCurrentPanePropertyChanged;
			

			#region -- set basic command bindings --
			CommandBindings.Add(
				new CommandBinding(PpsWindow.LoginCommand,
					(sender, e) =>
					{
						e.Handled = true;
					},
					(sender, e) => e.CanExecute = true //!Environment.IsAuthentificated
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
				new CommandBinding(PpsWindow.TraceLogCommand,
					async (sender, e) =>
					{
						e.Handled = true;
						await OpenPaneAsync(Environment.TracePane, PpsOpenPaneMode.NewPane, null);
					}
				)
			);

			CommandBindings.Add(
				new CommandBinding(PpsMainWindow.NextPaneCommand,
					(sender, e) =>
					{
						e.Handled = true;
						ActivateNextPane(true);
					},
					(sender, e) =>
					{
						e.CanExecute = panes.IndexOf(CurrentPane) < Panes.Count - 1;
					}
				)
			);
			CommandBindings.Add(
				new CommandBinding(PpsMainWindow.PrevPaneCommand,
					(sender, e) =>
					{
						e.Handled = true;
						ActivateNextPane(false);
					},
					(sender, e) =>
					{
						e.CanExecute = panes.IndexOf(CurrentPane) > 0;
					}
				)
			);
			CommandBindings.Add(
				new CommandBinding(PpsMainWindow.GoToPaneCommand,
					(sender, e) =>
					{
						e.Handled = true;
						if (e.Parameter is IPpsWindowPane pane)
							ActivatePane(pane);
					},
					(sender, e) =>
					{
						e.CanExecute = true;
					}
				)
			);
			CommandBindings.Add(
				new CommandBinding(PpsMainWindow.ClosePaneCommand,
					(sender, e) =>
					{
						e.Handled = true;
						if (e.Parameter is IPpsWindowPane pane)
							Remove(pane);
					},
					(sender, e) =>
					{
						e.CanExecute = CurrentPane != null; //&& (CurrentPane.PaneControl == null || !CurrentPane.PaneControl.ProgressStack.IsEnabled);
					}
				)
			);

			#endregion

			this.DataContext = this;
			
			Trace.TraceInformation("MainWindow[{0}] created.", windowIndex);
		} // ctor

		private Task<bool> unloadTask = null;

		protected override void OnClosing(CancelEventArgs e)
		{
			if (unloadTask != null) // currently unloading
				e.Cancel = !unloadTask.IsCompleted;
			else if (Panes.Count > 0)
			{
				e.Cancel = true; // cancel operation an start shutdown

				unloadTask = UnloadPanesAsync();
				unloadTask.ContinueWith(FinishClosing);
			}
		} // func OnClosing

		private void FinishClosing(Task<bool> r)
		{
			if (r.Result)
				Dispatcher.Invoke(Close);
		} // proc FinishClosing

		#endregion

		#region -- Navigator.SearchBox & Charmbar-----------------------------------------

		protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
		{
			base.OnPreviewMouseDown(e);

			ResetCharmbar(e.Source);

			if (!IsNavigatorVisible)
				return;
			navigator.OnPreview_MouseDown(e.OriginalSource);
		} // event OnPreviewMouseDown

		protected override void OnPreviewTextInput(TextCompositionEventArgs e)
		{
			base.OnPreviewTextInput(e);
			if (!IsNavigatorVisible)
				return;
			navigator.OnPreview_TextInput(e);
		} // event OnPreviewTextInput

		protected override void OnWindowCaptionClicked()
		{
			ResetCharmbar(null);

			if (!IsNavigatorVisible)
				return;
			navigator.OnPreview_MouseDown(null);
		} // proc OnWindowCaptionClicked

		private void ResetCharmbar(object mouseDownSource)
		{
			if (PART_Charmbar.CurrentContentType == PPSnCharmbarContentType.Default)
				return;
			else if (Object.Equals(mouseDownSource, PART_Charmbar))
				return;

			PART_Charmbar.CurrentContentType = PPSnCharmbarContentType.Default;
		} // proc ResetCharmbar


		#endregion

		private void RefreshSideIsVisibleProperty()
		{
			var show = (IsNavigatorVisible && navigator.NavigatorModel.ViewsShowDescription) 
				|| (!IsNavigatorVisible && CurrentPane != null && false); // && CurrentPane.HasSideBar
			SetValue(IsSideBarVisiblePropertyKey, show);
		} // proc RefreshSideIsVisibleProperty

		private void OnCurrentPanePropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (//e.PropertyName == nameof(IPpsWindowPane.HasSideBar)
			 e.PropertyName == nameof(PpsNavigatorModel.ViewsShowDescription))
				RefreshSideIsVisibleProperty();
		} // proc OnCurrentPanePropertyChanged

		private void OnCurrentPaneChanged(IPpsWindowPane oldValue, IPpsWindowPane newValue)
		{
			if (oldValue != null)
				oldValue.PropertyChanged -= OnCurrentPanePropertyChanged;
			if (newValue != null)
				newValue.PropertyChanged += OnCurrentPanePropertyChanged;

			RefreshSideIsVisibleProperty();
		} // proc OnCurrentPaneChanged

		private static void CurrentPaneChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsMainWindow)d).OnCurrentPaneChanged((IPpsWindowPane)e.OldValue, (IPpsWindowPane)e.NewValue);

		private void OnNavigatorVisibleChanged(bool oldValue, bool newValue)
		{
			RefreshSideIsVisibleProperty();
			SetValue(IsPaneVisibleProperty, !newValue);
		} // proc OnNavigatorVisibleChanged

		private static void NavigatorVisibleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsMainWindow)d).OnNavigatorVisibleChanged((bool)e.OldValue, (bool)e.NewValue);

		/// <summary>Settings of the current window.</summary>
		public PpsWindowApplicationSettings Settings => settings;
		/// <summary>Index of the current window</summary>
		public int WindowIndex => windowIndex;
		/// <summary>Access to the current environment,</summary>
		public new PpsMainEnvironment Environment => (PpsMainEnvironment)base.Environment;
		/// <summary>Access to the navigator model</summary>
		public PpsNavigatorModel Navigator => (PpsNavigatorModel)navigator.DataContext;
		/// <summary>Is the navigator visible.</summary>
		public bool IsNavigatorVisible
		{
			get=>(bool)GetValue(IsNavigatorVisibleProperty);
			set => SetValue(IsNavigatorVisibleProperty, value);
		} // prop NavigatorState

		public bool IsSideBarVisible => (bool)GetValue(IsSideBarVisibleProperty);

		public bool IsPaneVisible
		{
			get => (bool)GetValue(IsPaneVisibleProperty);
			set => SetValue(IsPaneVisibleProperty, value);
		} // prop NavigatorState

		public object CharmObject { get => GetValue(CharmObjectProperty); set => SetValue(CharmObjectProperty, value); }
	} // class PpsMainWindow
}