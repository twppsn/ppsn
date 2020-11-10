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
using System.Windows.Data;
using System.Windows.Input;
using Neo.IronLua;
using TecWare.DE.Stuff;
using TecWare.PPSn.Controls;
using TecWare.PPSn.UI;

namespace TecWare.PPSn.Bde
{
	/// <summary>Touch first window</summary>
	internal partial class PpsBdeWindow : PpsWindow, IPpsWindowPaneManager, IPpsBarcodeReceiver
	{
		public static readonly RoutedCommand BackCommand = new RoutedCommand();

		private static readonly DependencyPropertyKey topPaneHostPropertyKey = DependencyProperty.RegisterReadOnly(nameof(TopPaneHost), typeof(PpsBdePaneHost), typeof(PpsBdeWindow), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty TopPaneHostProperty = topPaneHostPropertyKey.DependencyProperty;

		private readonly PpsLockService lockService;
		private readonly PpsWindowApplicationSettings settings;

		private readonly PpsBarcodeService barcodeService;
		private readonly IDisposable barcodeReceiverToken;
		private string currentDateTimeFormat;

		private readonly List<PpsBdePaneHost> panes = new List<PpsBdePaneHost>();

		public PpsBdeWindow(IServiceProvider services)
			: base(services)
		{
			InitializeComponent();

			// register base service
			Services.AddService(typeof(IPpsWindowPaneManager), this);

			barcodeService = services.GetService<PpsBarcodeService>(true);
			barcodeReceiverToken = barcodeService.RegisterReceiver(this);

			this.AddCommandBinding(Shell, BackCommand, new PpsCommand(BackCommandExecuted, CanBackCommandExecute));
			this.AddCommandBinding(Shell, TraceLogCommand, new PpsAsyncCommand(AppInfoCommandExecutedAsync, CanAppInfoCommandExecute));

			// init locking
			lockService = services.GetService<PpsLockService>(true);
			lockService.PropertyChanged += LockService_PropertyChanged;
			SetValue(isLockedPropertyKey, lockService.IsLocked);

			if (lockService.IsShellMode)
			{
				WindowStyle = WindowStyle.None;
				WindowState = WindowState.Maximized;
			}
			// init window settings
			if (WindowStyle != WindowStyle.None)
				settings = new PpsWindowApplicationSettings(this, "bde");
			
			UpdateDateTimeFormat();
		} // ctor

		protected override void OnClosed(EventArgs e)
		{
			barcodeReceiverToken?.Dispose();
			base.OnClosed(e);
		} // proc OnClosed

		private void UpdateDateTimeFormat()
			=> currentDateTimeFormat = Shell.Settings.ClockFormat;

		private Task AppInfoCommandExecutedAsync(PpsCommandContext arg)
			=> OpenPaneAsync(typeof(PpsTracePane), PpsOpenPaneMode.Default);

		private bool CanAppInfoCommandExecute(PpsCommandContext arg)
			=> FindOpenPane(typeof(PpsTracePane)) == null;

		async Task IPpsBarcodeReceiver.OnBarcodeAsync(IPpsBarcodeProvider provider, string text, string format)
		{
			if (TopPaneHost.Pane is IPpsBarcodeReceiver receiver && receiver.IsActive)
			{
				try
				{
					await receiver.OnBarcodeAsync(provider, text, format);
				}
				catch (Exception e)
				{
					await Shell.ShowExceptionAsync(false, e, "Barcode nicht verarbeitet.");
				}
			}
		} // proc IPpsBarcodeReceiver.OnBarcodeAsync

		#region -- Pane Manager -------------------------------------------------------

		private Exception GetPaneStackException()
			=> new NotSupportedException("Bde uses a pane stack, it is not allowed to changed the steck.");

		private async Task<IPpsWindowPane> PushPaneAsync(Type paneType, LuaTable arguments)
		{
			// create the pane 
			var host = new PpsBdePaneHost();

			// add host and activate it
			panes.Add(host);
			AddLogicalChild(host);
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
			if (TopPaneHost != paneHost)
				throw GetPaneStackException();

			return await PopPaneAsync();
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
				case PpsOpenPaneMode.NewPane:
					return PushPaneAsync(paneType, arguments);

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

		private bool CanBackCommandExecute(PpsCommandContext arg)
		{
			return TopPaneHost?.Pane is IPpsWindowPaneBack backButton && backButton.CanBackButton.HasValue
				? backButton.CanBackButton.Value
				: panes.Count > 1;
		} // func CanBackCommandExecute

		#endregion

		#region -- IsLocked - property ------------------------------------------------

		private static readonly DependencyPropertyKey isLockedPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsLocked), typeof(bool), typeof(PpsBdeWindow), new FrameworkPropertyMetadata(BooleanBox.False));
		public static readonly DependencyProperty IsLockedProperty = isLockedPropertyKey.DependencyProperty;

		private void LockService_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(PpsLockService.IsLocked))
				SetValue(isLockedPropertyKey, lockService.IsLocked);
		} // event LockService_PropertyChanged

		public bool IsLocked => BooleanBox.GetBool(GetValue(IsLockedProperty));

		#endregion

		protected override IEnumerator LogicalChildren
			=> Procs.CombineEnumerator(base.LogicalChildren, panes.GetEnumerator());

		bool IPpsBarcodeReceiver.IsActive => IsActive;

		public string CurrentTimeString => currentDateTimeFormat;


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
}
