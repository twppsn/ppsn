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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Scripting.Debugging;
using Microsoft.Win32;
using Neo.IronLua;
using TecWare.DE.Stuff;
using TecWare.PPSn.Controls;
using TecWare.PPSn.Data;
using TecWare.PPSn.Lua;

namespace TecWare.PPSn.UI
{
	#region -- class PpsTracePane -----------------------------------------------------

	/// <summary>Pane to display trace messages.</summary>
	internal sealed partial class PpsTracePane : PpsWindowPaneControl
	{
		public static readonly RoutedCommand ToggleDebugCommand = new RoutedCommand();

		#region -- class PpsTraceTable ------------------------------------------------

		private sealed class PpsTraceTable : LuaTable
		{
			private readonly PpsTracePane pane;
			private readonly IPpsLuaShell luaShell;

			private readonly PpsDpcService dpcService;

			#region -- Ctor/Dtor ------------------------------------------------------

			public PpsTraceTable(PpsTracePane pane)
			{
				this.pane = pane ?? throw new ArgumentNullException(nameof(pane));

				luaShell = pane.Shell.GetService<IPpsLuaShell>(false);
				dpcService = pane.Shell.GetService<PpsDpcService>(false);
			} // ctor

			protected override object OnIndex(object key)
				=> luaShell.Global.GetValue(key) ?? base.OnIndex(key);

			#endregion

			#region -- Run Helper -----------------------------------------------------

			private async Task LuaRunTaskAsync(Func<Task> action, string finishMessage)
			{
				await action();
				if (finishMessage != null)
					await UI.ShowNotificationAsync(finishMessage, PpsImage.Information);
			} // proc LuaRunTaskAsync

			private void LuaRunTask(Func<Task> action, string finishMessage = null)
				=> LuaRunTaskAsync(action, finishMessage).Await();

			private void PinProtected(Action action, string pin, string finishMessage = null)
			{
				if (dpcService.IsDpcPin(pin))
				{
					action();
					if (finishMessage != null)
						UI.ShowNotificationAsync(finishMessage, PpsImage.Information).Await();
				}
				else
					UI.ShowNotificationAsync("PIN falsch.", PpsImage.Error).Await();
			} // proc PinProtected

			#endregion

			#region -- Lock-Service ---------------------------------------------------

			public async Task SendLogAsync()
			{	
				await dpcService.PushLogAsync(
					pane.Shell.GetService<IPpsLogService>(true).GetLogAsText()
				);
				await Task.Delay(3000);
				await UI.ShowNotificationAsync("Log gesendet.", PpsImage.Information);
			} // proc SendLogAsync

			[LuaMember, LuaMember("Lock")]
			public void Unlock(string pin)
				=> UI.ShowNotificationAsync(dpcService.Unlock(pin ?? "\0") ? "Entsperrt" : "Gesperrt", PpsImage.Information).Await();

			[LuaMember]
			public void SendLog()
				=> SendLogAsync().Await();

			#endregion

			#region -- DumpLiveData ---------------------------------------------------

			public Task WriteLiveDataAsync(TextWriter tr)
				=> Task.Run(() => pane.Shell.GetService<PpsLiveData>(true).Dump(tr));

			[LuaMember]
			internal void DumpLiveData(string fileName)
			{
				if (String.IsNullOrEmpty(fileName))
				{
					var dlg = new SaveFileDialog { DefaultExt = ".txt", Filter = "Text-File (*.txt)|*.txt" };
					if (dlg.ShowDialog() != true)
						return;
					fileName = dlg.FileName;
				}

				// check root of path
				if (!Path.IsPathRooted(fileName))
					fileName = Path.Combine(Path.GetTempPath(), fileName);

				using (var tr = new StreamWriter(fileName))
					LuaRunTask(() => WriteLiveDataAsync(tr), $"{fileName} geschrieben.");
			} // proc DumpLiveData

			#endregion

			#region -- Shell Mode -----------------------------------------------------

			[LuaMember]
			public void SetAsShell(string pin)
				=> PinProtected(PpsDpcService.SetShellEntry, pin, "Als Shell registriert.");

			[LuaMember]
			public void RemoveAsShell(string pin)
				=> PinProtected(PpsDpcService.RemoveShellEntry, pin, "Als Shell-Registrierung entfernt.");

			[LuaMember]
			public void Quit(string pin)
				=> PinProtected(Application.Current.Shutdown, pin);

			[LuaMember]
			public bool IsShell 
				=> PpsDpcService.GetIsShellMode();

			#endregion

			#region -- Exec -----------------------------------------------------------

			private void Exec(string command)
			{
				var psi = new ProcessStartInfo
				{
					FileName = command,
					WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
				};
				Process.Start(psi).Dispose();
			} // proc Exec

			private string FindRemoteDebugger()
			{
				var fi = new FileInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft Visual Studio 16.0", "Common7", "IDE", "Remote Debugger", "x64", "msvsmon.exe"));
				return fi.Exists ? fi.FullName : null;
			} // funcFindRemoteDebugger

			[LuaMember]
			public void Exec(string command, string pin)
			{
				if (command == null)
					command = "cmd.exe";
				else if (command == "rdbg")
					command = FindRemoteDebugger();

				if (command != null)
					PinProtected(() => Exec(command), pin, command + " ausgeführt.");
			} // proc Exec

			#endregion

			#region -- Shutdown/Restart -----------------------------------------------

			[LuaMember]
			public void ExecShutdown(string pin)
				=> PinProtected(() => PpsDpcService.ShutdownOperationSystem(true), pin);

			[LuaMember]
			public void ExecRestart(string pin)
				=> PinProtected(() => PpsDpcService.ShutdownOperationSystem(true), pin);

			#endregion

			#region -- Help -----------------------------------------------------------

			[LuaMember]
			public void Help()
			{
				var p = pane.Shell.LogProxy("Help");
				foreach(var d in Members)
				{
					if (d.Value is ILuaMethod)
					{
						// todo: parameter info
						p.Info(d.Key);
					}
				}
			} // proc Help

			#endregion

			public IPpsUIService UI => pane.ui;
			public IPpsLuaShell Shell => luaShell;
		} // class PpsTraceTable

		#endregion

		private readonly IPpsUIService ui;
		private readonly IPpsLogService log;
		private readonly PpsTraceTable traceTable;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary>Trace pane constructor</summary>
		/// <param name="paneHost"></param>
		public PpsTracePane(IPpsWindowPaneHost paneHost)
			: base(paneHost)
		{
			InitializeComponent();

			ui = Shell.GetService<IPpsUIService>(true);
			log = Shell.GetService<IPpsLogService>(false);

			eventPane.AddCommandBinding(
				Shell, ApplicationCommands.Open,
				new PpsAsyncCommand(
					ctx => ExecuteCommandAsync(commandTextBox.Text),
					ctx => traceTable.Shell != null && !String.IsNullOrEmpty(commandTextBox.Text)
				)
			);
			commandTextBox.AddCommandBinding(
				Shell, ApplicationCommands.Delete,
				new PpsCommand(ctx => commandTextBox.Clear(), ctx => !String.IsNullOrEmpty(commandTextBox.Text))
			);
			eventPane.AddCommandBinding(Shell, ApplicationCommands.SaveAs,
				new PpsAsyncCommand(
					ctx => traceTable.SendLogAsync()
				)
			);

			eventPane.AddCommandBinding(Shell, ApplicationCommands.Copy,
				new PpsCommand(
					ctx => CopyToClipboard(ctx.Parameter),
					ctx => CanCopyToClipboard(ctx.Parameter)
				)
			);

			eventPane.AddCommandBinding(Shell, ToggleDebugCommand,
				new PpsCommand(ctx => ToggleDebug())
			);

			// create environment for the command box
			traceTable = new PpsTraceTable(this);

			if (log != null)
				InitLog();
		} // ctor

		protected override Task OnLoadAsync(LuaTable args)
			=> Task.CompletedTask;

		#endregion

		#region -- Lua Execute Command ------------------------------------------------

		private static readonly string[] statementKeywords = new string[] { "return ", "local ", "do ", "function " };

		private static bool IsLuaStatement(string command)
		{
			for (var i = 0; i < statementKeywords.Length; i++)
			{
				if (command.StartsWith(statementKeywords[i]))
					return true;
			}
			return false;
		} // func IsLuaStatement

		private async Task ExecuteCommandAsync(string command)
		{
			var p = Shell.LogProxy("Command");
			commandTextBox.IsEnabled = false;
			try
			{
				// compile command
				if (!IsLuaStatement(command))
					command = "return " + command;
				var chunk = await traceTable.Shell.CompileAsync(command, true);

				// run command
				var r = chunk.Run(traceTable);

				// print result
				for (var i = 0; i < r.Count; i++)
					p.Info($"[{i}]: {r[i]}");
			}
			catch (LuaParseException ex)
			{
				p.Except(String.Format("Could not parse command: {0}", ex.Message));
			}
			catch (Exception ex)
			{
				p.Except(ex);
			}
			finally
			{
				commandTextBox.IsEnabled = true;
				commandTextBox.Focus();
			}
		} // proc ExecuteCommandAsync

		#endregion

		#region -- Log ----------------------------------------------------------------

		private bool showDebug = false;

		private void InitLog()
		{
			var logItems = log.Log;

			// enable synchronization
			BindingOperations.EnableCollectionSynchronization(logItems, log.Log, LogSyncCallback);

			// create collectionView
			var collectionView = CollectionViewSource.GetDefaultView(logItems);
			collectionView.Filter = FilterLogItems;
			collectionView.SortDescriptions.Add(new SortDescription(nameof(PpsShellLogItem.Stamp), ListSortDirection.Descending));
			logList.ItemsSource = collectionView;
		} // proc InitLog

		private void LogSyncCallback(IEnumerable collection, object context, Action accessMethod, bool writeAccess)
			=> Dispatcher.Invoke(accessMethod);

		private bool FilterLogItems(object obj)
			=> obj is PpsShellLogItem item ? showDebug || item.Type != LogMsgType.Debug : false;

		private void ToggleDebug()
		{
			showDebug = !showDebug;
			CollectionViewSource.GetDefaultView(log.Log).Refresh();
		} // proc ToggleDebug

		private bool CanCopyToClipboard(object item)
			=> item is PpsShellLogItem && logList.SelectedItem != null;

		private void CopyToClipboard(object item)
		{
			if (item is PpsShellLogItem logItem)
				Clipboard.SetText(logItem.Text);
		} // proc CopyToClipboard

		#endregion
	} // class PpsTracePane

	#endregion
}
