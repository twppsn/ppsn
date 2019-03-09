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
using TecWare.DE.Stuff;
using TecWare.PPSn.Controls;

namespace TecWare.PPSn.UI
{
	/// <summary></summary>
	public partial class PpsMainWindow : PpsWindow
	{
		/// <summary>Move to next pane.</summary>
		public readonly static RoutedCommand NextPaneCommand = new RoutedUICommand("Nächstes", "NextPane", typeof(PpsMainWindow),
				new InputGestureCollection(new InputGesture[] { new KeyGesture(Key.Right, ModifierKeys.Control | ModifierKeys.Alt) })
		);
		/// <summary>Move to previous pane.</summary>
		public readonly static RoutedCommand PrevPaneCommand = new RoutedUICommand("Vorheriges", "PrevPane", typeof(PpsMainWindow),
				new InputGestureCollection(new InputGesture[] { new KeyGesture(Key.Left, ModifierKeys.Control | ModifierKeys.Alt) })
		);
		/// <summary>Go to a secific pane.</summary>
		public readonly static RoutedCommand GoToPaneCommand = new RoutedUICommand("Gehe zu", "GoToPane", typeof(PpsMainWindow));
		/// <summary>Create the pane in a single pane window.</summary>
		public readonly static RoutedCommand UndockPaneCommand = new RoutedUICommand("InFenster", "UndockPane", typeof(PpsMainWindow), 
			new InputGestureCollection(new InputGesture[] { new KeyGesture(Key.F5, ModifierKeys.Control) })
		);
		/// <summary>Close a pane.</summary>
		public readonly static RoutedCommand ClosePaneCommand = new RoutedUICommand("Schließen", "ClosePane", typeof(PpsMainWindow), 
			new InputGestureCollection(new InputGesture[] { new KeyGesture(Key.F4, ModifierKeys.Control) })
		);

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
			this.AddCommandBinding(Environment, TraceLogCommand,
				new PpsAsyncCommand(
					ctx => OpenPaneAsync(typeof(PpsTracePane), PpsOpenPaneMode.NewPane)
				)
			);

			this.AddCommandBinding(Environment, NextPaneCommand,
				new PpsCommand(
					ctx => ActivateNextPane(true, false),
					ctx => paneHosts.IndexOf(CurrentPaneHost) < PaneHosts.Count - 1
				)
			);
			this.AddCommandBinding(Environment, PrevPaneCommand,
				new PpsCommand(
					ctx => ActivateNextPane(false, false),
					ctx => paneHosts.IndexOf(CurrentPaneHost) > 0
				)
			);
			this.AddCommandBinding(Environment, GoToPaneCommand,
				new PpsCommand(
					ctx =>
					{
						if (ctx.Parameter is PpsWindowPaneHost paneHost)
							ActivatePaneHost(paneHost);
					},
					ctx => ctx.Parameter is PpsWindowPaneHost 
				)
			);


			this.AddCommandBinding(Environment, UndockPaneCommand,
				new PpsCommand(
					ctx => UndockWindowPane(GetPaneFromParameter(ctx.Parameter)),
					ctx => !(GetPaneFromParameter(ctx.Parameter)?.IsFixed ?? true)
				)
			);

			this.AddCommandBinding(Environment, ClosePaneCommand,
				new PpsAsyncCommand(
					ctx => UnloadPaneHostAsync(GetPaneFromParameter(ctx.Parameter), null),
					ctx => CanUnloadPane(GetPaneFromParameter(ctx.Parameter))
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