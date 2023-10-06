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
using TecWare.PPSn.UI;

namespace TecWare.PPSn.Main
{
	/// <summary></summary>
	internal partial class PpsMainWindow : PpsWindow, IPpsMainWindow, IPpsBarcodeReceiver
	{
		/// <summary>Move to next pane.</summary>
		public static readonly RoutedCommand NextPaneCommand = new RoutedUICommand("Nächstes", "NextPane", typeof(PpsMainWindow),
			new InputGestureCollection(new InputGesture[] { new KeyGesture(Key.Right, ModifierKeys.Control | ModifierKeys.Alt) })
		);
		/// <summary>Move to previous pane.</summary>
		public static readonly RoutedCommand PrevPaneCommand = new RoutedUICommand("Vorheriges", "PrevPane", typeof(PpsMainWindow),
			new InputGestureCollection(new InputGesture[] { new KeyGesture(Key.Left, ModifierKeys.Control | ModifierKeys.Alt) })
		);
		/// <summary>Go to a secific pane.</summary>
		public static readonly RoutedCommand GoToPaneCommand = new RoutedUICommand("Gehe zu", "GoToPane", typeof(PpsMainWindow));
		/// <summary>Create the pane in a single pane window.</summary>
		public static readonly RoutedCommand UndockPaneCommand = new RoutedUICommand("InFenster", "UndockPane", typeof(PpsMainWindow), 
			new InputGestureCollection(new InputGesture[] { new KeyGesture(Key.F5, ModifierKeys.Control) })
		);
		/// <summary>Close a pane.</summary>
		public static readonly RoutedCommand ClosePaneCommand = new RoutedUICommand("Schließen", "ClosePane", typeof(PpsMainWindow), 
			new InputGestureCollection(new InputGesture[] { new KeyGesture(Key.F4, ModifierKeys.Control) })
		);

		/// <summary>Is the a pane visible</summary>
		public static readonly DependencyProperty IsPaneVisibleProperty = DependencyProperty.Register(nameof(IsPaneVisible), typeof(bool), typeof(PpsMainWindow), new FrameworkPropertyMetadata(false));
		
		private readonly int windowIndex = -1;                                       // settings key
		private readonly PpsWindowApplicationSettings settings;                      // current settings for the window

		private readonly PpsDpcService dpcService;
		private readonly IDisposable barcodeReceiverToken;
		private Task<bool> unloadTask = null;

		#region -- Ctor/Dtor -------------------------------------------------------------

		/// <summary></summary>
		/// <param name="shell"></param>
		/// <param name="windowIndex"></param>
		public PpsMainWindow(IPpsShell shell, int windowIndex)
			: base(shell)
		{
			this.windowIndex = windowIndex;
			this.mainWindowService = shell.GetService<IPpsMainWindowService>(true);

			InitializeComponent();

			Services.AddService(typeof(IPpsMainWindow), this);
			Services.AddService(typeof(IPpsWindowPaneManager), this);

			SetValue(paneHostsPropertyKey, paneHosts);

			paneHosts.CollectionChanged += PaneHosts_CollectionChanged;
			paneStrip.ItemContainerGenerator.StatusChanged += paneStrip_ItemContainerGenerator_StatusChanged;
			
			// initialize settings
			settings = new PpsWindowApplicationSettings(this, "main" + windowIndex.ToString());
			
			// init locking
			dpcService = shell.GetService<PpsDpcService>(false);
			if (dpcService != null)
			{
				dpcService.PropertyChanged += Shell_PropertyChanged;
				SetValue(isLockedPropertyKey, dpcService.IsLocked);
			}
			else
				SetValue(isLockedPropertyKey, false);
			Shell.PropertyChanged += Shell_PropertyChanged;

			// register barcode
			var barcodeService = shell.GetService<PpsBarcodeService>(true);
			barcodeReceiverToken = barcodeService.RegisterReceiver(this);

			#region -- set basic command bindings --
			this.AddCommandBinding(Shell, TraceLogCommand,
				new PpsAsyncCommand(
					ctx => OpenPaneAsync(typeof(PpsTracePane), PpsOpenPaneMode.NewPane)
				)
			);

			this.AddCommandBinding(Shell, NextPaneCommand,
				new PpsCommand(
					ctx => ActivateNextPane(true, false),
					ctx => paneHosts.IndexOf(CurrentPaneHost) < PaneHosts.Count - 1
				)
			);
			this.AddCommandBinding(Shell, PrevPaneCommand,
				new PpsCommand(
					ctx => ActivateNextPane(false, false),
					ctx => paneHosts.IndexOf(CurrentPaneHost) > 0
				)
			);
			this.AddCommandBinding(Shell, GoToPaneCommand,
				new PpsCommand(
					ctx =>
					{
						if (ctx.Parameter is PpsWindowPaneHost paneHost)
							ActivatePaneHost(paneHost);
					},
					ctx => ctx.Parameter is PpsWindowPaneHost 
				)
			);


			this.AddCommandBinding(Shell, UndockPaneCommand,
				new PpsCommand(
					ctx => UndockWindowPane(GetPaneFromParameter(ctx.Parameter)),
					ctx => !(GetPaneFromParameter(ctx.Parameter)?.IsFixed ?? true)
				)
			);

			this.AddCommandBinding(Shell, ClosePaneCommand,
				new PpsAsyncCommand(
					ctx => UnloadPaneHostAsync(GetPaneFromParameter(ctx.Parameter), null),
					ctx => CanUnloadPane(GetPaneFromParameter(ctx.Parameter))
				)
			);

			#endregion

			DataContext = this;

			Trace.TraceInformation("MainWindow[{0}] created.", windowIndex);
		} // ctor

		/// <summary></summary>
		/// <param name="e"></param>
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

		protected override void OnClosed(EventArgs e)
		{
			barcodeReceiverToken?.Dispose();
			base.OnClosed(e);
		} // proc OnClosed

		private void FinishClosing(Task<bool> r)
		{
			if (r.Result)
				Dispatcher.Invoke(Close);
		} // proc FinishClosing

		private void Shell_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (dpcService != null && e.PropertyName == nameof(PpsDpcService.IsLocked))
				SetValue(isLockedPropertyKey, dpcService.IsLocked);
			else if (e.PropertyName == nameof(PpsDpcService.IsRestartNeeded)) // background
			{
				//App.InvokeRestartAsync(Shell).AwaitUI(this);
			}
		} // event Shell_PropertyChanged

		#endregion

		#region -- IPpsBarcodeReceiver - members --------------------------------------------

		async Task<bool> IPpsBarcodeReceiver.OnBarcodeAsync(PpsBarcodeInfo code)
		{
			if (CurrentPaneHost.Pane is IPpsBarcodeReceiver receiver && receiver.IsActive)
			{
				try
				{
					return await receiver.OnBarcodeAsync(code);
				}
				catch (Exception e)
				{
					await Shell.ShowExceptionAsync(false, e, "Barcode nicht verarbeitet.");
					return true;
				}
			}
			else
				return false;
		} // proc IPpsBarcodeReceiver.OnBarcodeAsync

		#endregion

		private double CalculatePaneHostListMaxWidth(int items)
		{
			// ScreenWidth
			var availableWidth = GetWorkingArea().Width;
			// reduce Margin
			availableWidth -= 256;
			// fixe Breite für ein Item
			var itemWidth = 212d;
			// max items horizontal
			var maxColumns = Math.Floor(availableWidth / itemWidth);
			// make it more compact, up to 4 lines
			var columns = 0;
			if (items > 4)
			{
				if (items <= 12)
					columns = items / 2 + (items % 2 > 0 ? 1 : 0);
				else if (items <= 18)
					columns = items / 3 + (items % 3 > 0 ? 1 : 0);
				else
					columns = items / 4 + (items % 4 > 0 ? 1 : 0);

				if (columns < maxColumns)
					maxColumns = columns;
			}
			return maxColumns * itemWidth + 2;  // AddChild 2Pxl for WrapPanel margin
		} // func CalculatePaneHostListColumns

		protected override void OnPreviewKeyDown(KeyEventArgs e)
		{
			if (e.Key == Key.Tab && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
			{
				var collectionView = CollectionViewSource.GetDefaultView(SelectionOrder);
				collectionView.Refresh();

				if (!windowPanePopup.IsOpen)
				{
					windowPanePopupListBox.MaxWidth = CalculatePaneHostListMaxWidth(SelectionOrder.Count);
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

		#region -- IsLocked - property ------------------------------------------------

		private static readonly DependencyPropertyKey isLockedPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsLocked), typeof(bool), typeof(PpsMainWindow), new FrameworkPropertyMetadata(BooleanBox.False));
		public static readonly DependencyProperty IsLockedProperty = isLockedPropertyKey.DependencyProperty;

		public bool IsLocked => BooleanBox.GetBool(GetValue(IsLockedProperty));

		#endregion

		/// <summary>Settings of the current window.</summary>
		public PpsWindowApplicationSettings Settings => settings;
		/// <summary>Index of the current window</summary>
		public int WindowIndex => windowIndex;

		/// <summary>Current user</summary>
		public string CurrentUserName => Shell.GetUserName();

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