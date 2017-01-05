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

namespace TecWare.PPSn.UI
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public partial class PpsMainWindow : PpsWindow
	{
		/// <summary>Toggles between DataPane and Navigator.</summary>
		public readonly static RoutedCommand NavigatorToggleCommand = new RoutedCommand("NavigatorToggle", typeof(PpsMainWindow));
		public readonly static RoutedCommand NextPaneCommand = new RoutedCommand("NextPane", typeof(PpsMainWindow));
		public readonly static RoutedCommand PrevPaneCommand = new RoutedCommand("PrevPane", typeof(PpsMainWindow));
		public readonly static RoutedCommand GoToPaneCommand = new RoutedCommand("GoToPane", typeof(PpsMainWindow));
		public readonly static RoutedCommand ClosePaneCommand = new RoutedCommand("ClosePane", typeof(PpsMainWindow));

		private readonly static DependencyProperty IsNavigatorVisibleProperty = DependencyProperty.Register("IsNavigatorVisible", typeof(bool), typeof(PpsMainWindow), new PropertyMetadata(true));
		private readonly static DependencyProperty IsPaneVisibleProperty = DependencyProperty.Register("IsPaneVisible", typeof(bool), typeof(PpsMainWindow), new PropertyMetadata(false));
		private readonly static DependencyProperty CharmbarActualWidthProperty = DependencyProperty.Register("CharmbarActualWidth", typeof(double), typeof(PpsMainWindow));
		private readonly static DependencyProperty IsSideBarVisibleProperty = DependencyProperty.Register("IsSideBarVisible", typeof(bool), typeof(PpsMainWindow), new PropertyMetadata(true));

		/// <summary>Readonly property for the current pane.</summary>
		private readonly static DependencyPropertyKey CurrentPaneKey = DependencyProperty.RegisterReadOnly("CurrentPane", typeof(IPpsWindowPane), typeof(PpsMainWindow), new PropertyMetadata(null));
		private readonly static DependencyProperty CurrentPaneProperty = CurrentPaneKey.DependencyProperty;

		private int windowIndex = -1;                                       // settings key
		private PpsWindowApplicationSettings settings;                      // current settings for the window

		#region -- Ctor/Dtor ----------------------------------------------------------------

		public PpsMainWindow(int windowIndex)
		{
			this.windowIndex = windowIndex;

			InitializeComponent();

			// initialize settings
			settings = new PpsWindowApplicationSettings(this, "main" + windowIndex.ToString());
			navigator.Init(this);

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

			//CommandBindings.Add(
			//	new CommandBinding(PpsWindow.TraceLogCommand,
			//		async (sender, e) =>
			//		{
			//			e.Handled = true;
			//			await LoadPaneAsync(Environment.TracePane, PpsOpenPaneMode.NewSingleWindow, null);
			//		}
			//	)
			//);

			CommandBindings.Add(
				new CommandBinding(PpsWindow.TraceLogCommand,
					async (sender, e) =>
					{
						e.Handled = true;
						await LoadPaneAsync(Environment.TracePane, PpsOpenPaneMode.NewPane, null);
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
						e.CanExecute = Panes.IndexOf(CurrentPane) < Panes.Count - 1;
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
						e.CanExecute = Panes.IndexOf(CurrentPane) > 0;
					}
				)
			);
			CommandBindings.Add(
				new CommandBinding(PpsMainWindow.GoToPaneCommand,
					(sender, e) =>
					{
						e.Handled = true;
						var pane = e.Parameter as IPpsWindowPane;
						if (pane != null)
							Activate(pane);
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
						var pane = e.Parameter as IPpsWindowPane;
						if (pane != null)
							Remove(pane);
					},
					(sender, e) =>
					{
						e.CanExecute = CurrentPane != null && (CurrentPane.PaneControl == null || !CurrentPane.PaneControl.ProgressStack.IsEnabled);
					}
				)
			);

			#endregion

			this.DataContext = this;

			Trace.TraceInformation("MainWindow[{0}] created.", windowIndex);

			var descriptor = DependencyPropertyDescriptor.FromProperty(PpsCharmbarControl.ActualWidthProperty, typeof(PpsCharmbarControl));
			descriptor.AddValueChanged(PART_Charmbar, OnCharmbarActualWidthChanged);
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

		#region -- Navigator.SearchBox ------------------------------------------------------

		protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
		{
			base.OnPreviewMouseDown(e);
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
			if (!IsNavigatorVisible)
				return;
			navigator.OnPreview_MouseDown(null);
		} // proc OnWindowCaptionClicked

		#endregion

		#region -- charmbar ---------------------------------------------------------------

		private void OnCharmbarActualWidthChanged(object sender, EventArgs e)
		{
			SetActualWidth(PART_Charmbar.ActualWidth);
		}

		private void SetActualWidth(double value)
		{
			SetValue(CharmbarActualWidthProperty, value);
		}

		#endregion

		/// <summary>Settings of the current window.</summary>
		public PpsWindowApplicationSettings Settings => settings;
		/// <summary>Index of the current window</summary>
		public int WindowIndex => windowIndex;
		/// <summary>Access to the current environment,</summary>
		public new PpsMainEnvironment Environment => (PpsMainEnvironment)base.Environment;
		/// <summary>Access to current charmbar width</summary>
		public double CharmbarActualWidth => (double)GetValue(CharmbarActualWidthProperty);
		/// <summary>Access to the navigator model</summary>
		public PpsNavigatorModel Navigator => (PpsNavigatorModel)navigator.DataContext;
		/// <summary>Is the navigator visible.</summary>
		public bool IsNavigatorVisible
		{
			get
			{
				return (bool)GetValue(IsNavigatorVisibleProperty);
			}
			set
			{
				if (IsNavigatorVisible != value)
				{
					if (value)
					{
						SetValue(IsNavigatorVisibleProperty, true);
						SetValue(IsPaneVisibleProperty, false);
					}
					else
					{
						SetValue(IsNavigatorVisibleProperty, false);
						SetValue(IsPaneVisibleProperty, true);
					}
				}
				ShowSideBarBackground();
			}
		} // prop NavigatorState

		/// <summary>Show SideBarBackground</summary>
		public void ShowSideBarBackground()
		{
			var show = (IsNavigatorVisible && navigator.ViewsShowDescriptions) || (!IsNavigatorVisible && ShowPaneSideBar);
			if (show != (bool)GetValue(IsSideBarVisibleProperty))
				SetValue(IsSideBarVisibleProperty, show);
		} // proc ShowSideBarBackground
	} // class PpsMainWindow
}
