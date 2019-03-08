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
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using TecWare.PPSn.Controls;

namespace TecWare.PPSn.UI
{
	/// <summary></summary>
	public partial class PpsMainWindow : PpsWindow
	{
		/// <summary>Move to next pane.</summary>
		public readonly static RoutedCommand NextPaneCommand = new RoutedCommand("NextPane", typeof(PpsMainWindow));
		/// <summary>Move to previous pane.</summary>
		public readonly static RoutedCommand PrevPaneCommand = new RoutedCommand("PrevPane", typeof(PpsMainWindow));
		/// <summary>Go to a secific pane.</summary>
		public readonly static RoutedCommand GoToPaneCommand = new RoutedCommand("GoToPane", typeof(PpsMainWindow));
		/// <summary>Close a pane.</summary>
		public readonly static RoutedCommand ClosePaneCommand = new RoutedCommand("ClosePane", typeof(PpsMainWindow));

		public readonly static DependencyProperty IsPaneVisibleProperty = DependencyProperty.Register(nameof(IsPaneVisible), typeof(bool), typeof(PpsMainWindow), new FrameworkPropertyMetadata(false));
		
		private int windowIndex = -1;                                       // settings key
		private PpsWindowApplicationSettings settings;                      // current settings for the window

		private Task<bool> unloadTask = null;

		#region -- Ctor/Dtor -------------------------------------------------------------

		public PpsMainWindow(int windowIndex)
		{
			this.windowIndex = windowIndex;

			InitializeComponent();

			SetValue(paneHostsPropertyKey, paneHosts);

			paneHosts.CollectionChanged += PaneHosts_CollectionChanged;
			paneStrip.ItemContainerGenerator.StatusChanged += paneStrip_ItemContainerGenerator_StatusChanged;
			
			// initialize settings
			settings = new PpsWindowApplicationSettings(this, "main" + windowIndex.ToString());
		
			#region -- set basic command bindings --
			CommandBindings.Add(
				new CommandBinding(LoginCommand,
					(sender, e) =>
					{
						e.Handled = true;
					},
					(sender, e) => e.CanExecute = true //!Environment.IsAuthentificated
				)
			);
			
			CommandBindings.Add(
				new CommandBinding(TraceLogCommand,
					async (sender, e) =>
					{
						e.Handled = true;
						await OpenPaneAsync(typeof(PpsTracePane), PpsOpenPaneMode.NewPane, null);
					}
				)
			);

			CommandBindings.Add(
				new CommandBinding(NextPaneCommand,
					(sender, e) =>
					{
						e.Handled = true;
						ActivateNextPane(true);
					},
					(sender, e) =>
					{
						e.CanExecute = paneHosts.IndexOf(CurrentPaneHost) < PaneHosts.Count - 1;
					}
				)
			);
			CommandBindings.Add(
				new CommandBinding(PrevPaneCommand,
					(sender, e) =>
					{
						e.Handled = true;
						ActivateNextPane(false);
					},
					(sender, e) =>
					{
						e.CanExecute = paneHosts.IndexOf(CurrentPaneHost) > 0;
					}
				)
			);
			CommandBindings.Add(
				new CommandBinding(GoToPaneCommand,
					(sender, e) =>
					{
						e.Handled = true;
						//PART_PaneListPopUp.IsOpen = false;
						if (e.Parameter is PpsWindowPaneHost paneHost)
							ActivatePaneHost(paneHost);
					},
					(sender, e) => { e.CanExecute = e.Parameter is PpsWindowPaneHost ph; }
				)
			);
			CommandBindings.Add(
				new CommandBinding(ClosePaneCommand,
					(sender, e) =>
					{
						e.Handled = true;
						//PART_PaneListPopUp.IsOpen = false;
						if (e.Parameter is PpsWindowPaneHost paneHost && !paneHost.IsFixed)
							UnloadPaneHostAsync(paneHost, null).AwaitTask();
					},
					(sender, e) => e.CanExecute =  e.Parameter is PpsWindowPaneHost paneHost ? !paneHost.PaneProgress.IsActive && !paneHost.IsFixed : false
				)
			);

			#endregion

			DataContext = this;

			// start navigator pane
			OpenPaneAsync(typeof(PpsNavigatorModel), PpsOpenPaneMode.NewPane).SpawnTask(Environment);
			
			Trace.TraceInformation("MainWindow[{0}] created.", windowIndex);
		} // ctor

		protected override void OnClosing(CancelEventArgs e)
		{
			if (unloadTask != null) // currently unloading
				e.Cancel = !unloadTask.IsCompleted;
			else if (PaneHosts.Count > 0)
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

		protected override void OnPreviewKeyDown(KeyEventArgs e)
		{
			if (e.Key == Key.Tab && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
			{
				var collectionView = CollectionViewSource.GetDefaultView(SelectionOrder);
				collectionView.Refresh();

				if (!windowPanePopup.IsOpen)
				{
					windowPanePopup.IsOpen = true; // open popup
					// move first
					collectionView.MoveCurrentToFirst();
				}

				// move next per key down
				if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
				{
					if (!collectionView.MoveCurrentToPrevious())
						collectionView.MoveCurrentToLast();
				}
				else
				{
					if (!collectionView.MoveCurrentToNext())
						collectionView.MoveCurrentToFirst();
				}
				
				e.Handled = true;
			}
		} // proc OnPreviewKeyDown

		protected override void OnPreviewKeyUp(KeyEventArgs e)
		{
			if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
			{
				if (windowPanePopup.IsOpen)
				{
					var collectionView = CollectionViewSource.GetDefaultView(SelectionOrder);
					if (collectionView.CurrentItem is PpsWindowPaneHost paneHost)
						ActivatePaneHost(paneHost);
					windowPanePopup.IsOpen = false; // close popup
				}
				e.Handled = true;
			}
			base.OnPreviewKeyUp(e);
		} // proc OnPreviewPopup

		/// <summary>Settings of the current window.</summary>
		public PpsWindowApplicationSettings Settings => settings;
		/// <summary>Index of the current window</summary>
		public int WindowIndex => windowIndex;
		/// <summary>Access to the current environment,</summary>
		public PpsEnvironment Environment => (PpsEnvironment)Shell;

		public bool IsPaneVisible
		{
			get => (bool)GetValue(IsPaneVisibleProperty);
			set => SetValue(IsPaneVisibleProperty, value);
		} // prop NavigatorState
		
		static PpsMainWindow()
		{
			EventManager.RegisterClassHandler(typeof(PpsMainWindow), Selector.SelectedEvent, new RoutedEventHandler(OnWindowPaneHostItemSelected));
			EventManager.RegisterClassHandler(typeof(PpsMainWindow), PpsWindowPaneStrip.ItemMoveEvent, new PpsWindowPaneStripItemMoveEventEventHandler(OnPaneStripItemMove));
		} // sctor
	} // class PpsMainWindow
}