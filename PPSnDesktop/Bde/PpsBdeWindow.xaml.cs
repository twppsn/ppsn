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
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;
using Neo.IronLua;
using TecWare.DE.Stuff;
using TecWare.PPSn.Controls;
using TecWare.PPSn.Networking;
using TecWare.PPSn.UI;

namespace TecWare.PPSn.Bde
{
	/// <summary>Touch first window</summary>
	internal partial class PpsBdeWindow : PpsWindow, IPpsWindowPaneManager, IPpsBdeManager, IPpsBarcodeReceiver, IPpsIdleAction
	{
		public static readonly RoutedCommand BackCommand = new RoutedCommand();

		private static readonly DependencyPropertyKey topPaneHostPropertyKey = DependencyProperty.RegisterReadOnly(nameof(TopPaneHost), typeof(PpsBdePaneHost), typeof(PpsBdeWindow), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(TopPaneHostChanged)));

		public static readonly DependencyProperty TopPaneHostProperty = topPaneHostPropertyKey.DependencyProperty;

		private readonly PpsDpcService dpcService;
		private readonly PpsWindowApplicationSettings settings;

		private readonly IDisposable barcodeReceiverToken;
		private string currentDateTimeFormat = null;

		private readonly List<PpsBdePaneHost> panes = new List<PpsBdePaneHost>();

		public PpsBdeWindow(IServiceProvider services)
			: base(services)
		{
			InitializeComponent();

			// register base service
			Services.AddService(typeof(IPpsWindowPaneManager), this);
			Services.AddService(typeof(IPpsBdeManager), this);

			var barcodeService = services.GetService<PpsBarcodeService>(true);
			barcodeReceiverToken = barcodeService.RegisterReceiver(this);

			this.AddCommandBinding(Shell, BackCommand, new PpsCommand(BackCommandExecuted, CanBackCommandExecute));
			this.AddCommandBinding(Shell, TraceLogCommand, new PpsAsyncCommand(AppInfoCommandExecutedAsync, CanAppInfoCommandExecute));

			// init locking
			dpcService = services.GetService<PpsDpcService>(true);
			dpcService.PropertyChanged += Shell_PropertyChanged;
			SetValue(isLockedPropertyKey, dpcService.IsLocked);
			Shell.PropertyChanged += Shell_PropertyChanged;

			if (dpcService.IsShellMode)
			{
				ResizeMode = ResizeMode.NoResize;
				WindowStyle = WindowStyle.None;
				WindowState = WindowState.Maximized;
				captionLabel.Tag = null;

				SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;
			}
			// init window settings
			if (WindowStyle != WindowStyle.None)
				settings = new PpsWindowApplicationSettings(this, "bde");

			Shell.GetService<IPpsIdleService>(true).Add(this);

			UpdateDateTimeFormat();

			if (Shell.Settings.UseTouchKeyboard ?? PpsDpcService.GetIsShellMode())
			{
				virtualKeyboard.Attach(this);
				Services.AddService(typeof(IPpsVirtualKeyboard), virtualKeyboard);
			}
		} // ctor

		private void SystemEvents_DisplaySettingsChanged(object sender, EventArgs e)
		{
			Shell.LogProxy().Info("DisplaySettingsChanged. Resize Window.");
			WindowState = WindowState.Normal;
			WindowState = WindowState.Maximized;
		} // event SystemEvents_DisplaySettingsChanged

		protected override void OnClosed(EventArgs e)
		{
			barcodeReceiverToken?.Dispose();
			base.OnClosed(e);
		} // proc OnClosed

		private Task AppInfoCommandExecutedAsync(PpsCommandContext arg)
			=> OpenPaneAsync(typeof(PpsTracePane), PpsOpenPaneMode.Default);

		private bool CanAppInfoCommandExecute(PpsCommandContext arg)
			=> FindOpenPane(typeof(PpsTracePane)) == null;

		private void Shell_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(PpsDpcService.IsLocked))
				SetValue(isLockedPropertyKey, dpcService.IsLocked);
			else if (e.PropertyName == nameof(PpsDpcService.IsRestartNeeded)) // background thread
				App.InvokeRestartAsync(Shell, "DpcRequest").AwaitUI(this);
			else if (e.PropertyName == nameof(IPpsCommunicationService.ConnectionState))
			{
				if (Shell.ConnectionState == PpsCommunicationState.Connected)
				{
					paneContent.IsEnabled = true;
					disconnectProgress.Visibility = Visibility.Hidden;
				}
				else
				{
					var allowEdit = Shell.Settings.GetProperty("PPSn.Bde.AllowEditOnDisconnect", false);
					if (!allowEdit)
					{
						paneContent.IsEnabled = false;
						disconnectProgress.Visibility = Visibility.Visible;
					}
				}
			}
		} // event Shell_PropertyChanged

		async Task<bool> IPpsBarcodeReceiver.OnBarcodeAsync(PpsBarcodeInfo code)
		{
			if (TopPaneHost.Pane is IPpsBarcodeReceiver receiver && receiver.IsActive)
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

		PpsIdleReturn IPpsIdleAction.OnIdle(int elapsed)
			=> UpdateCurrentTimeString() ? PpsIdleReturn.Idle : PpsIdleReturn.StopIdle;

		#region -- Pane Manager -------------------------------------------------------

		private Exception GetPaneStackException()
			=> new NotSupportedException("Bde uses a pane stack, it is not allowed to changed the stack.");

		private async Task<IPpsWindowPane> PushPaneAsync(Type paneType, LuaTable arguments, bool enforce)
		{
			if (!enforce)
			{
				var currentPane = TopPaneHost?.Pane;
				if (currentPane != null && currentPane.GetType() == paneType)
				{
					switch (currentPane.CompareArguments(arguments))
					{
						case PpsWindowPaneCompareResult.Same:
							return currentPane;
						case PpsWindowPaneCompareResult.Reload:
							await currentPane.LoadAsync(arguments);
							return currentPane;
					}
				}
			}

			// create the pane 
			var host = new PpsBdePaneHost();

			// add host and activate it
			panes.Add(host);
			AddLogicalChild(host);
			virtualKeyboard.Hide();
			SetValue(topPaneHostPropertyKey, panes.LastOrDefault());

			// load content
			await host.LoadAsync(this, paneType, arguments);

			return host.Pane;
		} // func PushNewPaneAsync

		private async Task<bool> PopPaneAsync()
		{
			var topPane = TopPaneHost;
			if (topPane != null && await topPane.UnloadAsync(null))
			{
				RemoveLogicalChild(topPane);
				panes.Remove(topPane);
				SetValue(topPaneHostPropertyKey, panes.LastOrDefault());
				virtualKeyboard.Hide();
#if DEBUG
				GC.Collect();
				GC.WaitForPendingFinalizers();
				GC.Collect();
#endif
				return true;
			}
			else
				return false;
		} // func PopPaneAsync

		/// <summary>Close the pane host.</summary>
		/// <param name="paneHost"></param>
		/// <returns></returns>
		public async Task<bool> PopPaneAsync(PpsBdePaneHost paneHost)
		{
			// find the index in stack
			var idx = panes.IndexOf(paneHost);
			if (idx == -1)
				throw GetPaneStackException();

			// pop complete stack to this pane
			while (idx < panes.Count)
			{
				if (!await PopPaneAsync())
					return false;
			}

			return true;
		} // func PopPaneAsync

		bool IPpsWindowPaneManager.ActivatePane(IPpsWindowPane pane)
			=> throw GetPaneStackException();

		/// <summary>Push a new pane to the stack</summary>
		/// <param name="paneType"></param>
		/// <param name="newPaneMode"></param>
		/// <param name="arguments"></param>
		/// <returns></returns>
		public Task<IPpsWindowPane> OpenPaneAsync(Type paneType, PpsOpenPaneMode newPaneMode = PpsOpenPaneMode.Default, LuaTable arguments = null)
		{
			switch (newPaneMode)
			{
				case PpsOpenPaneMode.Default:
					return PushPaneAsync(paneType, arguments, false);
				case PpsOpenPaneMode.NewPane:
					return PushPaneAsync(paneType, arguments, true);

				case PpsOpenPaneMode.ReplacePane:
				case PpsOpenPaneMode.NewSingleDialog:
				case PpsOpenPaneMode.NewSingleWindow:
				case PpsOpenPaneMode.NewMainWindow:
				default:
					throw GetPaneStackException();
			}
		} // func OpenPaneAsync

		/// <summary>Search for an existing pane.</summary>
		/// <param name="paneType"></param>
		/// <param name="arguments"></param>
		/// <returns></returns>
		public IPpsWindowPane FindOpenPane(Type paneType, LuaTable arguments = null)
			=> panes.FirstOrDefault(p => PpsWpfShell.EqualPane(p.Pane, paneType, arguments))?.Pane;

		private static void TopPaneHostChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			if (e.OldValue is PpsBdePaneHost oldPane)
				oldPane.OnDeactivated();
			if (e.NewValue is PpsBdePaneHost newPane)
				newPane.OnActivated();
		} // proc TopPaneHostChanged

		/// <summary>Return complete pane stack</summary>
		public IEnumerable<IPpsWindowPane> Panes => panes.Select(p => p.Pane);
		/// <summary>Ret</summary>
		public PpsBdePaneHost TopPaneHost => (PpsBdePaneHost)GetValue(TopPaneHostProperty);

		#endregion

		#region -- Back button --------------------------------------------------------

		private void BackCommandExecuted(PpsCommandContext obj)
		{
			if (TopPaneHost?.Pane is IPpsWindowPaneBack backButton && backButton.CanBackButton.HasValue)
			{
				if (backButton.CanBackButton.Value)
					backButton.InvokeBackButton();
			}
			else
				PopPaneAsync().Spawn(this);
		} // proc BackCommandExecuted

		private bool TryCanBackButton(IPpsWindowPaneBack backButton, out bool backButtonState)
		{
			var c = backButton.CanBackButton;
			if (c.HasValue)
			{
				backButtonState = c.Value;
				return true;
			}
			else
			{
				backButtonState = false;
				return false;
			}
		} // func TryCanBackButton

		private bool CanBackCommandExecute(PpsCommandContext arg)
			=> TopPaneHost?.Pane is IPpsWindowPaneBack backButton && TryCanBackButton(backButton, out var backButtonState) ? backButtonState : panes.Count > 1;

		#endregion

		#region -- IsLocked - property ------------------------------------------------

		private static readonly DependencyPropertyKey isLockedPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsLocked), typeof(bool), typeof(PpsBdeWindow), new FrameworkPropertyMetadata(BooleanBox.False));
		public static readonly DependencyProperty IsLockedProperty = isLockedPropertyKey.DependencyProperty;

		public bool IsLocked => BooleanBox.GetBool(GetValue(IsLockedProperty));

		#endregion

		#region -- CurrentTimeString - property ---------------------------------------

		private static readonly DependencyPropertyKey currentTimeStringPropertyKey = DependencyProperty.RegisterReadOnly(nameof(CurrentTimeString), typeof(string), typeof(PpsBdeWindow), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty CurrentTimeStringProperty = currentTimeStringPropertyKey.DependencyProperty;
		private static readonly DependencyPropertyKey currentTimeLineHeightPropertyKey = DependencyProperty.RegisterReadOnly(nameof(CurrentTimeLineHeight), typeof(double), typeof(PpsBdeWindow), new FrameworkPropertyMetadata(16.0));
		public static readonly DependencyProperty CurrentTimeLineHeightProperty = currentTimeLineHeightPropertyKey.DependencyProperty;
		private static readonly DependencyPropertyKey currentTimeFontSizePropertyKey = DependencyProperty.RegisterReadOnly(nameof(CurrentTimeFontSize), typeof(double), typeof(PpsBdeWindow), new FrameworkPropertyMetadata(14.0));
		public static readonly DependencyProperty CurrentTimeFontSizeProperty = currentTimeFontSizePropertyKey.DependencyProperty;
		
		private void UpdateDateTimeFormat()
		{
			currentDateTimeFormat = Shell.Settings.ClockFormat;

			// passe die Metricen entsprechend der Zeilen an
			var newLines = currentDateTimeFormat.Count(c => c == '\n') + 1;
			var lineHeight = 32.0 / newLines;
			SetValue(currentTimeLineHeightPropertyKey, lineHeight);
			SetValue(currentTimeFontSizePropertyKey, lineHeight - 2.0);
			
			UpdateCurrentTimeString();
		} // proc UpdateDateTimeFormat

		private bool UpdateCurrentTimeString()
		{
			var hasTimeFormat = !String.IsNullOrEmpty(currentDateTimeFormat);
			SetValue(currentTimeStringPropertyKey,
				 hasTimeFormat ? DateTime.Now.ToString(currentDateTimeFormat) : null
			);
			return hasTimeFormat;
		} // proc UpdateCurrentTimeString

		public string CurrentTimeString => (string)GetValue(CurrentTimeStringProperty);
		public double CurrentTimeLineHeight => (double)GetValue(CurrentTimeLineHeightProperty);
		public double CurrentTimeFontSize => (double)GetValue(CurrentTimeFontSizeProperty);
		#endregion

		protected override IEnumerator LogicalChildren
			=> Procs.CombineEnumerator(base.LogicalChildren, panes.GetEnumerator());

		bool IPpsBarcodeReceiver.IsActive => IsActive;

		#region -- WindowStyle - Converter --------------------------------------------

		private sealed class WindowStyleConverterImplementation : IValueConverter
		{
			public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
			{
				if (value is WindowStyle windowStyle)
				{
					switch (windowStyle)
					{
						case WindowStyle.None:
							return Visibility.Collapsed;
						default:
							return Visibility.Visible;
					}
				}
				else
					return Visibility.Visible;
			} // func Convert

			public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
				=> throw new NotSupportedException();
		} // class WindowStyleConverterImplementation

		public static IValueConverter WindowStyleConverter { get; } = new WindowStyleConverterImplementation();

		#endregion
	} // class PpsBdeWindow

	#region -- interface IPpsBdeManager -----------------------------------------------

	/// <summary>Implemented by BdeWindow</summary>
	public interface IPpsBdeManager : IPpsWindowPaneManager
	{
	} // interface IPpsBdeManager

	#endregion
}
