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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Scripting.Utils;
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
		public static readonly RoutedCommand ToggleDebugCommand = PpsRoutedCommand.Create(typeof(PpsTracePane), "ToggleDebug");
		public static readonly RoutedCommand ExecuteBarcodeCommand = PpsRoutedCommand.Create(typeof(PpsTracePane), "ExecBarcode");

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

			#region -- Open, Exec -----------------------------------------------------

			[LuaMember]
			public void Open(string link)
				=> PpsWebView.LinkCommand.Execute(new PpsWebViewLink(luaShell.Shell.Http.CreateFullUri(link)), pane);

			[LuaMember]
			public void TakePicture(string path = null)
			{
				var captureService = pane.GetControlService<IPpsCaptureService>(true);
				LuaRunTask(() => captureService.CaptureAsync(pane, PpsCaptureDevice.Camera, path == null ? null : new PpsCapturePathTarget(path, "test")), "Geschlossen.");
			} // proc TakePicture

			[LuaMember]
			public void Exec(string command, string pin)
			{
				if (command == null)
					command = "cmd";
				PinProtected(() => PpsDpcService.Execute(command, null), pin, command + " ausgeführt.");
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
					WriteRegistryValue(sw, "DefaultDomain", String.Empty);
					WriteRegistryValue(sw, "DefaultUserName", String.IsNullOrEmpty(domainName) ? userName : domainName + "\\" + userName);
					WriteRegistryValue(sw, "DefaultPassword", password);
				}

				PpsDpcService.Execute(Path.Combine(Environment.SystemDirectory, "regedit.exe"), fi.FullName, true);
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

			#region -- Clear ----------------------------------------------------------

			[LuaMember("Clear")]
			public void ClearHistory()
				=> pane.Dispatcher.BeginInvoke(new Action(pane.ClearHistory));

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
			private readonly AppAssemblyInfo[] assemblyInfo;

			public AppInfoModel(IPpsShell shell)
			{
				shellApplication = shell.GetService<IPpsShellApplication>(false);

				assemblyInfo = (
					from c in
						from cur in shell.Settings.GetGroups("PPSn.Application.Files", true, "Path", "Version", "Load")
						where cur.GetProperty("Load", null) == "net"
						select CreateAssemblyInfo(cur)
					where c != null
					orderby c.Name
					select c
				).ToArray();
			} // ctor

			private static AppAssemblyInfo CreateAssemblyInfo(PpsSettingsGroup setting)
			{
				var name = Path.GetFileNameWithoutExtension(setting.GetProperty("Path", null));
				var asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(c => c.GetName().Name == name);
				return asm != null && Version.TryParse(setting.GetProperty("Version", "0.0.0.0"), out var serverVersion)
					? AppAssemblyInfo.Create(asm, serverVersion)
					: null;
			} // func CreateAssemblyInfo

			public string AppName => shellApplication?.Name;
			public Version AssemblyVersion => shellApplication?.AssenblyVersion;
			public PpsShellApplicationVersion InstalledVersion => shellApplication?.InstalledVersion ?? PpsShellApplicationVersion.Default;

			public IReadOnlyList<AppAssemblyInfo> AssemblyInfo => assemblyInfo;
		} // class AppInfoModel

		#endregion

		private readonly IPpsUIService ui;
		private readonly IPpsLogService log;
		private readonly IPpsWpfResources wpfResources;
		private readonly PpsBarcodeService barcodeService;
		private readonly PpsDpcService dpcService;
		private readonly PpsTraceTable traceTable;

		private readonly ObservableCollection<string> recentCommands = new ObservableCollection<string>();

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
			commandTextBox.AddCommandBinding(
				Shell, PpsControlCommands.SelectCommand,
				new PpsCommand(ShowSelectCommandPopup, CanShowSelectCommandPopup)
			);
			commandTextList.AddCommandBinding(
				Shell, PpsControlCommands.SelectCommand,
				new PpsCommand(SelectCommandExecute, CanSelectCommand)
			);
			commandTextList.ItemsSource = recentCommands;

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

			barcodePanel.AddCommandBinding(Shell, ExecuteBarcodeCommand, new PpsAsyncCommand(ExecuteBarcodeAsync));

			// create environment for the command box
			traceTable = new PpsTraceTable(this);

			if (dpcService != null)
				dpcService.PropertyChanged += DpcService_PropertyChanged;

			appInfoPane.DataContext = new AppInfoModel(Shell);

			if (log != null)
				InitLog();
		} // ctor

		private bool CanShowSelectCommandPopup(PpsCommandContext ctx)
			=> !dpcService.IsLocked && (!(ctx.Parameter is null) || recentCommands.Count > 0);

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

			LoadCommandHistory();

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

		private void AppendCommandHistory(string command)
		{
			// append command or move
			var currentPos = recentCommands.IndexOf(command);
			if (currentPos >= 0)
				recentCommands.Move(currentPos, 0);
			else
				recentCommands.Insert(0, command);

			// remove commands
			while (recentCommands.Count > 40)
				recentCommands.RemoveAt(recentCommands.Count - 1);

			// Save changes
			SaveCommandHistory();
		} // proc AppendCommandHistory

		private void ClearHistory()
		{
			recentCommands.Clear();
			SaveCommandHistory();
		} // proc ClearHistory

		private void LoadCommandHistory()
		{
			var value = Shell.UserSettings.GetProperty("PPSn.Trace.Commands.Local", null);
			if (value != null)
				recentCommands.AddRange(value.SplitNewLines().Where(c => !String.IsNullOrEmpty(c)));
		} // proc LoadCommandHistory

		private void SaveCommandHistory()
		{
			var value = String.Join("\n", recentCommands.Select(c => c.Replace('\n', ' ')));
			using (var settings = Shell.UserSettings.Edit())
			{
				settings.Set("PPSn.Trace.Commands.Local", value);
				settings.CommitAsync().Spawn();
			}
		} // proc SaveCommandHistory

		private void SelectCommandExecute(PpsCommandContext context)
		{
			if (context.DataContext is string command)
			{
				commandTextBox.Text = command;
				commandTextBox.Select(command.Length, 0);
				commandTextPopup.IsOpen = false;
				commandTextBox.Focus();
			}
		} // proc SelectCommandExecute

		private void ShowSelectCommandPopup(PpsCommandContext context)
		{
			if (context.Parameter is null)
				commandTextPopup.IsOpen = true;
		} // proc ShowSelectCommandPopup

		private bool CanSelectCommand(PpsCommandContext context)
			=> commandTextBox.IsEnabled;

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
				var useHistory = !dpcService.IsLocked;

				// compile command
				var luaCommand = IsLuaStatement(command) ? command : "return " + command;
				var chunk = await traceTable.Shell.CompileAsync(luaCommand, true);

				// run command
				var r = chunk.Run(traceTable);

				// add command to history
				if (useHistory)
					AppendCommandHistory(command);

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

			// load theme images
			GetAllResources(new ResourceDictionary { Source = new Uri("pack://application:,,,/PPSn.Desktop.UI;component/themes/images.xaml", UriKind.Absolute) }, resourceList);

			// load application resources
			GetAllResources(wpfResources.Resources, resourceList);
			
			// source resources
			resourceList.Sort();

			// create resource view
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

		async Task<bool> IPpsBarcodeReceiver.OnBarcodeAsync(PpsBarcodeInfo code)
		{
			while (cachedBarcodes.Count > 20)
				cachedBarcodes.RemoveAt(0);

			var isLocked = dpcService.IsLocked;
			if (dpcService.UnlockWithCode(code.RawCode))
			{
				if (isLocked)
					await ShowUnlockNotificationAsync(true);
				else
				{
					dpcService.Lock();
					await ShowUnlockNotificationAsync(false);
				}
			}
			else
				cachedBarcodes.Add(new PpsTraceBarcodeItem(code));

			return true;
		} // event IPpsBarcodeReceiver.OnBarcodeAsync

		private Task ExecuteBarcodeAsync(PpsCommandContext arg)
		{
			if (arg.DataContext is PpsTraceBarcodeItem item)
			{
				return barcodeService.OnDefaultBarcodeAsync(item.Info).ContinueWith(
					t =>
					{
						if (!t.Result)
							ui.ShowNotificationAsync("Keine Verarbeitung erfolgt...", PpsImage.Information);
					}, TaskContinuationOptions.ExecuteSynchronously
				 );
			}
			return Task.CompletedTask;
		} // proc ExecuteBarcodeAsync

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
		private readonly PpsBarcodeInfo code;

		public PpsTraceBarcodeItem(PpsBarcodeInfo code)
		{
			this.code = code ?? throw new ArgumentNullException(nameof(code));
		} // ctor

		public string Provider => "(" + code.Provider.Type + ") " + code.Provider.Description;
		public string CodeName => code.Code.CodeName;
		public string Code => code.Code.ToString();
		public string RawCode => code.RawCode;
		public string Format => code.Format;

		public PpsBarcodeInfo Info => code;
	} // class PpsTraceBarcodeItem

	#endregion

	#region -- class AppAssemblyInfo --------------------------------------------------

	internal sealed class AppAssemblyInfo
	{
		public AppAssemblyInfo(string name, Version fileVersion, Version serverVersion, string informationalVersion, string company)
		{
			Name = name;
			FileVersion = fileVersion;
			ServerVersion = serverVersion;
			InformationalVersion = informationalVersion;
			Company = company;
		} // ctor

		public string Name { get; }
		public Version FileVersion { get; }
		public Version ServerVersion { get; }
		public string InformationalVersion { get; }
		public string Company { get; }
		public bool IsOutdated => ServerVersion > FileVersion;

		public static AppAssemblyInfo Create(Assembly asm, Version serverVersion)
		{
			var name = asm.GetName();

			var fileVersionInfo = asm.GetCustomAttribute<AssemblyFileVersionAttribute>();
			var infoVersion = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
			var companyInfo = asm.GetCustomAttribute<AssemblyCopyrightAttribute>();

			if (!Version.TryParse(fileVersionInfo.Version, out var fileVersion))
				fileVersion = name.Version;

			return new AppAssemblyInfo(name.Name, fileVersion, serverVersion, infoVersion?.InformationalVersion, companyInfo?.Copyright);
		} // class AppAssemblyInfo
	} // class AppAssemblyInfo

	#endregion
}
