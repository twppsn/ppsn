﻿#region -- copyright --
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
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Neo.IronLua;
using TecWare.DE.Stuff;
using TecWare.PPSn.Controls;
using TecWare.PPSn.UI;

namespace TecWare.PPSn.Bde
{
	/// <summary>Touch first window</summary>
	internal partial class PpsBdeWindow : PpsWindow, IPpsWindowPaneManager, IPpsProgressFactory, IPpsBarcodeReceiver
	{
		public static readonly RoutedCommand BackCommand = new RoutedCommand();

		private static readonly DependencyPropertyKey topPaneHostPropertyKey = DependencyProperty.RegisterReadOnly(nameof(TopPaneHost), typeof(PpsBdePaneHost), typeof(PpsBdeWindow), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty TopPaneHostProperty = topPaneHostPropertyKey.DependencyProperty;

		private readonly PpsBarcodeService barcodeService;
		private readonly IDisposable barcodeReceiverToken;
		private string currentDateTimeFormat;

		private readonly List<PpsBdePaneHost> panes = new List<PpsBdePaneHost>();

		public PpsBdeWindow(IServiceProvider services)
			:base(services)
		{
			InitializeComponent();

			// register base service
			Services.AddService(typeof(IPpsWindowPaneManager), this);
			Services.AddService(typeof(IPpsProgressFactory), this);

			barcodeService = services.GetService<PpsBarcodeService>(true);
			barcodeReceiverToken = barcodeService.RegisterReceiver(this);

			this.AddCommandBinding(Shell, BackCommand, new PpsCommand(BackCommandExecuted, CanBackCommandExecute));
			this.AddCommandBinding(Shell, TraceLogCommand, new PpsAsyncCommand(AppInfoCommandExecutedAsync, CanAppInfoCommandExecute));
			
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
			var host = new PpsBdePaneHost(this, paneType);

			// add pane and activate it
			panes.Add(host);
			SetValue(topPaneHostPropertyKey, panes.LastOrDefault());

			// load content
			await host.LoadPaneAsync(arguments);

			return host.Pane;
		} // func PushNewPaneAsync

		private async Task<bool> PopPaneAsync()
		{
			var topPane = TopPaneHost;
			if (topPane != null && await topPane.UnloadPaneAsync(null))
			{
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
			switch(newPaneMode)
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
			=> panes.FirstOrDefault(p => PpsWindowPaneHelper.EqualPane(p.Pane, paneType, arguments))?.Pane;

		/// <summary>Return complete pane stack</summary>
		public IEnumerable<IPpsWindowPane> Panes => panes.Select(p => p.Pane);
		/// <summary>Ret</summary>
		public PpsBdePaneHost TopPaneHost => (PpsBdePaneHost)GetValue(TopPaneHostProperty);

		#endregion

		public IPpsProgress CreateProgress(bool blockUI = true)
			=> null;

		#region -- Back button --------------------------------------------------------

		private void BackCommandExecuted(PpsCommandContext obj)
		{
			if (TopPaneHost?.Pane is IPpsWindowPaneBack backButton)
				backButton.InvokeBackButton();
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

		bool IPpsBarcodeReceiver.IsActive => IsActive;

		public string CurrentTimeString => currentDateTimeFormat;

		public bool IsLocked => false;
	} // class PpsBdeWindow
}