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
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;
using Neo.IronLua;
using TecWare.DE.Stuff;
using TecWare.PPSn.Controls;
using TecWare.PPSn.Data;

namespace TecWare.PPSn.UI
{
	#region -- class PpsTracePane -----------------------------------------------------

	/// <summary>Pane to display trace messages.</summary>
	internal sealed partial class PpsTracePane : PpsWindowPaneControl, IPpsBarcodeReceiver
	{
		public static readonly RoutedCommand ToggleDebugCommand = new RoutedCommand();

		#region -- class PpsTraceTable ------------------------------------------------

		private sealed class PpsTraceTable : LuaTable
		{
			private readonly PpsTracePane pane;
			private readonly IPpsLuaShell luaShell;

			#region -- Ctor/Dtor ------------------------------------------------------

			public PpsTraceTable(PpsTracePane pane)
			{
				this.pane = pane ?? throw new ArgumentNullException(nameof(pane));

				luaShell = pane.Shell.GetService<IPpsLuaShell>(false);
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
				if (Dpc.IsDpcPin(pin))
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
				await Dpc.PushLogAsync(
					pane.Shell.GetService<IPpsLogService>(true).GetLogAsText()
				);
				await Task.Delay(3000);
				await UI.ShowNotificationAsync("Log gesendet.", PpsImage.Information);
			} // proc SendLogAsync

			[LuaMember, LuaMember("Lock")]
			public void Unlock(string pin)
				=> pane.ShowUnlockNotificationAsync(Dpc.UnlockWithPin(pin ?? "\0")) .Await();

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

			private void ExecCore(string command, string arguments)
			{
				var psi = new ProcessStartInfo
				{
					FileName = command,
					Arguments = arguments,
					WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
				};
				Process.Start(psi)?.Dispose();
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
				else if (command == "settings")
					command = "ms-settings:";

				if (command != null)
					PinProtected(() => ExecCore(command, null), pin, command + " ausgeführt.");
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

			#region -- Autologon ------------------------------------------------------

			private void WriteRegistryValue(TextWriter tw, string key, string value)
			{
				tw.Write('"');
				tw.Write(key);
					tw.Write('"');
					tw.Write('=');

				if (value == null)
					tw.Write('-');
				else
				{
					tw.Write('"');
					tw.Write(value.Replace("\"", "\"\""));
					tw.Write('"');
				}

				tw.WriteLine();
			} // proc WriteRegistryValue

			private void ConfigAutoLogon(string password)
			{
				var fi = new FileInfo(Path.Combine(Path.GetTempPath(), "AutoLogon.5fffca5e885048428ea4b38694768f95.reg"));

				// https://docs.microsoft.com/en-us/troubleshoot/windows-server/user-profiles-and-logon/turn-on-automatic-logon
				// todo: https://docs.microsoft.com/en-us/windows/win32/secauthn/protecting-the-automatic-logon-password

				string domainName = null;
				string userName = null;

				if (password != null)
				{
					userName = Environment.UserName;
					domainName = Environment.UserDomainName;
					if (String.IsNullOrEmpty(domainName))
						domainName = null;
				}

				using (var sw = new StreamWriter(fi.FullName))
				{
					sw.WriteLine("Windows Registry Editor Version 5.00");
					sw.WriteLine();
					sw.WriteLine(@"[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon]");
					WriteRegistryValue(sw, "AutoAdminLogon", password != null ? "1" : "0");
					WriteRegistryValue(sw, "DefaultDomain", domainName);
					WriteRegistryValue(sw, "DefaultUserName", userName);
					WriteRegistryValue(sw, "DefaultPassword", password);
				}

				ExecCore(Path.Combine(Environment.SystemDirectory, "regedit.exe"), fi.FullName);
			} // proc ConfigAutoLogon

			[LuaMember]
			public void AutoLogon(string password, string pin)
				=> PinProtected(() => ConfigAutoLogon(password), pin, password != null ? "AutoLogon konfiguriert." : "AutoLogon deaktiviert.");

			#endregion

			#region -- Theme ----------------------------------------------------------

			[LuaMember]
			public void SetTheme(string name)
			{
				var theme = pane.wpfResources.GetThemes().FirstOrDefault(c => String.Compare(c.Name, name, StringComparison.OrdinalIgnoreCase) == 0);
				if (theme == null)
					throw new ArgumentOutOfRangeException(nameof(name), name, "Theme not found.");
				theme.Apply();
			} // func SetTheme

			#endregion

			#region -- Help -----------------------------------------------------------

			[LuaMember]
			public void Help()
			{
				var p = pane.Shell.LogProxy("Help");
				foreach (var d in Members)
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
			public PpsDpcService Dpc => pane.dpcService;
		} // class PpsTraceTable

		#endregion

		#region -- class SettingsCollection -------------------------------------------

		private sealed class SettingsCollection : IEnumerable, IEnumerable<PpsTraceSettingInfoItem>, ICollectionViewFactory
		{
			private readonly IPpsSettingsService settingsService;

			public SettingsCollection(IPpsSettingsService settingsService)
			{
				this.settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
			}

			public IEnumerator<PpsTraceSettingInfoItem> GetEnumerator()
				=> settingsService.Query().Select(c => new PpsTraceSettingInfoItem(c.Key, c.Value)).GetEnumerator();

			IEnumerator IEnumerable.GetEnumerator()
				=> GetEnumerator();

			ICollectionView ICollectionViewFactory.CreateView()
				=> new PpsTypedListCollectionView<PpsTraceSettingInfoItem>(this.ToList());
		} // class SettingsCollection

		#endregion

		#region -- class AppInfoModel -------------------------------------------------

		private sealed class AppInfoModel
		{
			private readonly IPpsShellApplication shellApplication;

			public AppInfoModel(IPpsShell shell)
			{
				shellApplication = shell.GetService<IPpsShellApplication>(false);
			} // ctor

			public string AppName => shellApplication?.Name;
			public Version AssemblyVersion => shellApplication?.AssenblyVersion;
			public Version InstalledVersion => shellApplication?.InstalledVersion;
		} // class AppInfoModel

		#endregion

		private readonly IPpsUIService ui;
		private readonly IPpsLogService log;
		private readonly IPpsWpfResources wpfResources;
		private readonly PpsBarcodeService barcodeService;
		private readonly PpsDpcService dpcService;
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
			wpfResources = Shell.GetService<IPpsWpfResources>(true);
			barcodeService = Shell.GetService<PpsBarcodeService>(false);
			dpcService = Shell.GetService<PpsDpcService>(false);

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

			if (dpcService != null)
				dpcService.PropertyChanged += DpcService_PropertyChanged;

			appInfoPane.DataContext = new AppInfoModel(Shell);

			if (log != null)
				InitLog();
		} // ctor

		protected override Task OnLoadAsync(LuaTable args)
		{
			// connect settings
			settingsList.ItemsSource = new SettingsCollection(Shell.GetService<IPpsSettingsService>(true));

			// connect resource
			InitResources();

			// connect barcodes
			if (barcodeService != null)
				InitBarcodes();

			UpdateLockedState();

			return Task.CompletedTask;
		} // func OnLoadAsync

		private void DpcService_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(PpsDpcService.IsLocked))
				UpdateLockedState();
		} // event DpcService_PropertyChanged

		private void UpdateLockedState()
		{
			var isLocked = dpcService.IsLocked;

			resourcePanel.IsEnabled = !isLocked;
			settingsPanel.IsEnabled = !isLocked;
		} // proc UpdateLockedState

		private Task ShowUnlockNotificationAsync(bool isUnlocked)
			=> ui.ShowNotificationAsync(isUnlocked ? "Entsperrt" : "Gesperrt", PpsImage.Information);

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

		#region -- Resources ----------------------------------------------------------

		private void InitResources()
		{
			var resourceList = new List<PpsTraceResourceInfoItem>();
			GetAllResources(wpfResources.Resources, resourceList);
			resourceList.Sort();
			var resourceView = new PpsTypedListCollectionView<PpsTraceResourceInfoItem>(resourceList);
			resourceView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(PpsTraceResourceInfoItem.SourceName), null, StringComparison.OrdinalIgnoreCase));

			resourceTree.ItemsSource = resourceView;
		} // proc InitResources

		private void GetAllResources(ResourceDictionary resources, List<PpsTraceResourceInfoItem> resourceList)
		{
			foreach (var k in resources.Keys)
			{
				if (k.GetType().Name == "PpsColorTypeKey")
					continue;

				resourceList.Add(new PpsTraceResourceInfoItem(resources, k));
			}

			foreach (var m in resources.MergedDictionaries)
				GetAllResources(m, resourceList);
		} // func GetAllResources

		#endregion

		#region -- Barcodes -----------------------------------------------------------

		private readonly ObservableCollection<PpsTraceBarcodeItem> cachedBarcodes = new ObservableCollection<PpsTraceBarcodeItem>();

		private void InitBarcodes()
		{
			barcodeProvider.ItemsSource = barcodeService;
			barcodeList.ItemsSource = cachedBarcodes;
		} // proc InitBarcodes

		Task IPpsBarcodeReceiver.OnBarcodeAsync(IPpsBarcodeProvider provider, string code, string format)
		{
			while (cachedBarcodes.Count > 20)
				cachedBarcodes.RemoveAt(0);

			var isLocked = dpcService.IsLocked;
			if (dpcService.UnlockWithCode(code))
			{
				if (isLocked)
					return ShowUnlockNotificationAsync(true);
				else
				{
					dpcService.Lock();
					return ShowUnlockNotificationAsync(false);
				}
			}
			else
				cachedBarcodes.Add(new PpsTraceBarcodeItem(provider, code, format));
			return Task.CompletedTask;
		} // event IPpsBarcodeReceiver.OnBarcodeAsync

		/// <summary>Is the barcode receiver active.</summary>
		bool IPpsBarcodeReceiver.IsActive => true;

		#endregion
	} // class PpsTracePane

	#endregion

	#region -- class PpsTraceSettingInfoItem ------------------------------------------

	internal sealed class PpsTraceSettingInfoItem
	{
		private readonly string key;
		private readonly string value;

		public PpsTraceSettingInfoItem(string key, string value)
		{
			this.key = key ?? throw new ArgumentNullException(nameof(key));

			if (key.EndsWith(".Password", StringComparison.OrdinalIgnoreCase))
				this.value = "****";
			else
				this.value = value;
		} // ctor

		public string Key => key;
		public string Value => value;
	} // class PpsTraceSettingInfoItem

	#endregion

	#region -- class PpsTraceResourceInfoItem -----------------------------------------

	internal sealed class PpsTraceResourceInfoItem : IComparable<PpsTraceResourceInfoItem>
	{
		private readonly ResourceDictionary origin;
		private readonly object key;

		public PpsTraceResourceInfoItem(ResourceDictionary origin, object key)
		{
			this.origin = origin ?? throw new ArgumentNullException(nameof(origin));
			this.key = key ?? throw new ArgumentNullException(nameof(key));
		} // ctor

		public int CompareTo(PpsTraceResourceInfoItem other)
		{
			return origin == other.origin
				? String.Compare(KeyName, other.KeyName, StringComparison.OrdinalIgnoreCase)
				: String.Compare(SourceName, other.SourceName, StringComparison.OrdinalIgnoreCase);
		} // func CompareTo

		private static string GetTypeName(Type type) 
			=> type == null ? "<null>" : LuaType.GetType(type).AliasName ?? type.Name;

		public string SourceName => origin.Source == null ? origin.GetType().Name : origin.Source.ToString();

		public string KeyName => key.ToString();
		public string KeyType => GetTypeName(key.GetType());

		public object Value => origin[key];
		public string ValueType => GetTypeName(Value?.GetType());
	} // class PpsTraceResourceInfoItem

	#endregion

	#region -- class PpsTraceBarcodeItem ----------------------------------------------

	internal class PpsTraceBarcodeItem
	{
		private readonly IPpsBarcodeProvider provider;
		private readonly string code;
		private readonly string format;

		public PpsTraceBarcodeItem(IPpsBarcodeProvider provider, string code, string format)
		{
			this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
			this.code = code ?? throw new ArgumentNullException(nameof(code));
			this.format = format;
		} // ctor

		public string Provider => "(" + provider.Type + ") " + provider.Description;
		public string RawCode => code;
		public string Format => format;
	} // class PpsTraceBarcodeItem

	#endregion
}
