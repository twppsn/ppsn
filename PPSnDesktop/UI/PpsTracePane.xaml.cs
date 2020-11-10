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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
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
		//public static readonly RoutedCommand AssignDebugTarget = new RoutedCommand("Assign", typeof(PpsTracePane));
		//public static readonly RoutedCommand ClearDebugTarget = new RoutedCommand("Clear", typeof(PpsTracePane));

		#region -- class PpsTraceTable ------------------------------------------------

		private sealed class PpsTraceTable : LuaTable
		{
			private readonly PpsTracePane pane;
			private readonly IPpsLuaShell luaShell;

			private readonly PpsLockService lockService;

			#region -- Ctor/Dtor ------------------------------------------------------

			public PpsTraceTable(PpsTracePane pane)
			{
				this.pane = pane ?? throw new ArgumentNullException(nameof(pane));

				luaShell = pane.Shell.GetService<IPpsLuaShell>(false);
				lockService = pane.Shell.GetService<PpsLockService>(false);
			} // ctor

			protected override object OnIndex(object key)
				=> luaShell.Global.GetValue(key) ?? base.OnIndex(key);

			#endregion

			#region -- Run Helper -----------------------------------------------------

			private async Task LuaRunTaskAsync(Func<Task> action, string finishMessage)
			{
				await action();
				await Task.Delay(3000);
				if (finishMessage != null)
					await UI.ShowNotificationAsync(finishMessage, PpsImage.Information);
			} // proc LuaRunTaskAsync

			private void LuaRunTask(Func<Task> action, string finishMessage = null)
				=> LuaRunTaskAsync(action, finishMessage).Await();

			private void PinProtected(Action action, string pin, string finishMessage = null)
			{
				if (lockService.IsDpcPin(pin))
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

			[LuaMember, LuaMember("Lock")]
			public void Unlock(string pin)
				=> UI.ShowNotificationAsync(lockService.Unlock(pin ?? "\0") ? "Entsperrt" : "Gesperrt", PpsImage.Information).Await();

			[LuaMember]
			public void SendLog()
				=> LuaRunTask(() => lockService.SendLogAsync(), "Log gesendet.");

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
					LuaRunTask(() => WriteLiveDataAsync(tr), "{fileName} geschrieben.");
			} // proc DumpLiveData

			#endregion

			#region -- Shell Mode ---------------------------------------------------------

			[LuaMember]
			public void SetAsShell(string pin)
				=> PinProtected(PpsLockService.SetShellEntry, pin, "Als Shell registriert.");

			[LuaMember]
			public void RemoveAsShell(string pin)
				=> PinProtected(PpsLockService.RemoveShellEntry, pin, "Als Shell-Registrierung entfernt.");

			[LuaMember]
			public void Quit(string pin)
				=> PinProtected(Application.Current.Shutdown, pin);

			[LuaMember]
			public bool IsShell 
				=> PpsLockService.GetIsShellMode();

			#endregion

			#region -- Exec -----------------------------------------------------------

			private void Exec(string command)
			{
				Process.Start(command).Dispose();
			} // proc Exec

			[LuaMember]
			public void Exec(string command, string pin)
			{
				if (command == null)
					command = "cmd.exe";

				PinProtected(() => Exec(command), pin, command + " ausgeführt.");
			} // proc Exec

			#endregion

			#region -- Shutdown/Restart -----------------------------------------------

			[LuaMember]
			public void ExecShutdown(string pin)
				=> PinProtected(() => PpsLockService.ShutdownOperationSystem(true), pin);

			[LuaMember]
			public void ExecRestart(string pin)
				=> PinProtected(() => PpsLockService.ShutdownOperationSystem(true), pin);

			#endregion

			public IPpsUIService UI => pane.ui;
			public IPpsLuaShell Shell => luaShell;
		} // class PpsTraceTable

		#endregion

		private readonly IPpsUIService ui;
		private readonly PpsTraceTable traceTable;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary>Trace pane constructor</summary>
		/// <param name="paneHost"></param>
		public PpsTracePane(IPpsWindowPaneHost paneHost)
			: base(paneHost)
		{
			InitializeComponent();

			ui = Shell.GetService<IPpsUIService>(true);

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

			// create environment for the command box
			traceTable = new PpsTraceTable(this);
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
					p.Debug($"[{i}]: {r[i]}");
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
			}
		} // proc ExecuteCommandAsync

		#endregion










		#region -- Ctor/Dtor ----------------------------------------------------------

		///// <summary>Trace pane constructor</summary>
		///// <param name="paneHost"></param>
		//public PpsTracePane(IPpsWindowPaneHost paneHost)
		//	: base(paneHost)
		//{
		//	this.AddCommandBinding(Shell, ApplicationCommands.SaveAs,
		//		new PpsCommand(
		//			 ctx => SaveTrace()
		//		)
		//	);

		//	this.AddCommandBinding(Shell, ApplicationCommands.Copy,
		//		new PpsCommand(
		//			 ctx => CopyToClipboard(ctx.Parameter),
		//			 ctx => CanCopyToClipboard(ctx.Parameter)
		//		)
		//	);


		//	//this.AddCommandBinding(Shell, AssignDebugTarget,
		//	//	new PpsAsyncCommand(
		//	//		ctx => UpdateDebugTargetAsync((PpsMasterDataRow)ctx.Parameter, false),
		//	//		ctx => ctx.Parameter is PpsMasterDataRow
		//	//	)
		//	//);


		//	//this.AddCommandBinding(Shell, ClearDebugTarget,
		//	//	new PpsAsyncCommand(
		//	//		ctx => UpdateDebugTargetAsync((PpsMasterDataRow)ctx.Parameter, true),
		//	//		ctx => ctx.Parameter is PpsMasterDataRow row && row.GetProperty("DebugPath", null) != null
		//	//	)
		//	//);

		//	//var collectionView = CollectionViewSource.GetDefaultView(Environment?.Log);
		//	//collectionView.SortDescriptions.Add(new SortDescription(nameof(PpsTraceItemBase.Stamp), ListSortDirection.Descending));
		//} // ctor

		#endregion

		#region -- Copy, SaveAs -------------------------------------------------------

		private void SaveTrace()
		{
			var openFileDialog = new SaveFileDialog
			{
				Filter = "TraceLog | *.csv;",
				DefaultExt = ".csv",
				CheckFileExists = false,
				CheckPathExists = true,
				AddExtension = true
			};

			if (openFileDialog.ShowDialog() != true)
				return;

			if (File.Exists(openFileDialog.FileName)
				&& MessageBox.Show("Die Datei existiert bereits. Möchten Sie überschreiben?", "Warnung", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
				return;

			using (var sw = new StreamWriter(openFileDialog.FileName))
			{
				// write log content
				//if (Environment.Log is PpsTraceLog log)
				//{
				//	foreach (var c in log.OfType<PpsTraceItemBase>())
				//	{
				//		sw.Write(c.Type);
				//		sw.Write(';');
				//		sw.Write(c.Stamp.ToString("G"));
				//		sw.Write(';');

				//		if (c is PpsExceptionItem exi)
				//		{
				//			sw.Write('"');
				//			sw.Write(ExceptionFormatter.FormatPlainText(exi.Exception).Replace("\"", "\"\""));
				//			sw.Write('"');
				//		}
				//		else
				//			sw.Write(c.Message.Replace("\"", "\"\""));
				//		sw.WriteLine();
				//	}
				//}
				//else
					sw.WriteLine("Log is not a trace log?");

				//// write statistics
				//foreach (var statistic in Environment.Statistics)
				//{
				//	if (!statistic.HasData)
				//		continue;
				//	var en = statistic.History.GetEnumerator();
				//	var historic = 0;
				//	en.Reset();
				//	while (en.MoveNext())
				//	{
				//		sw.WriteLine($"{statistic.Name};{DateTime.Now.AddSeconds(historic)};{en.Current}");
				//		historic--;
				//	}
				//}
			}
		} // proc SaveTrace

		#endregion

		#region -- Copy To Clipboard --------------------------------------------------

		private string TraceToString(object item)
		{
			switch (item)
			{
				case PpsTraceItem pti:
					return $"{pti.Type} - {pti.Stamp} - ID:{pti.Id} - Source: {pti.Source} - Message: {pti.Message}";
				case PpsTextItem pti:
					return $"{pti.Type} - {pti.Stamp} - {pti.Message}";
				case string s:
					return s;
				case PpsExceptionItem exc:
					var ret = new StringBuilder();

					ret.Append($"{exc.Type} - {exc.Stamp} - ");

					ExceptionFormatter.FormatPlainText(ret, exc.Exception);

					return ret.ToString();
				default:
					return null;
			}
		} // func TraceToString

		private bool CanCopyToClipboard(object item)
			=> false; // item != null || logList.SelectedItem != null;

		private void CopyToClipboard(object item)
		{
			//var clipText = TraceToString(item ?? logList.SelectedItem);
			//if (clipText != null)
			//	Clipboard.SetText(clipText);
		} // proc CopyToClipboard

		#endregion

		#region -- UpdateDebugTarget --------------------------------------------------

		private string GetDebugTargetFileName(string path, string currentDebugTarget)
		{
			var queryIndex = path.IndexOf('?');
			var name = queryIndex >= 0 ? Path.GetFileName(path.Substring(0, queryIndex)) : Path.GetFileName(path);

			var openDialog = new OpenFileDialog
			{
				Title = "Assign Debug Target to " + name,
				Filter = name + "|" + name,
				FileName = currentDebugTarget,
				CheckFileExists = true
			};

			if (openDialog.ShowDialog() != true)
				return null;
			return openDialog.FileName;
		} // func GetDebugTargetFileName

		//private async Task UpdateDebugTargetAsync(PpsMasterDataRow row, bool clear)
		//{
		//	var path = row.GetProperty("Path", String.Empty);
		//	if (String.IsNullOrEmpty(path))
		//		return;

		//	var newFileName = clear ? null : GetDebugTargetFileName(path, row.GetProperty("DebugPath", null));
		//	//try
		//	//{
		//	//	await Environment.MasterData.UpdateDebugPathAsync(row.RowId, path, newFileName);
		//	//}
		//	//catch (Exception e)
		//	//{
		//	//	Environment.ShowException(e);
		//	//}
		//} // proc UpdateDebugTargetAsync

		#endregion
	} // class PpsTracePane

	#endregion

	#region -- class PpsTraceItemTemplateSelector -------------------------------------

	internal sealed class PpsTraceItemTemplateSelector : DataTemplateSelector
	{
		public override DataTemplate SelectTemplate(object item, DependencyObject container)
		{
			DataTemplate GetTemplate()
			{
				switch (item)
				{
					case PpsExceptionItem ei:
					case Exception e:
						return ExceptionTemplate;
					case PpsTraceItem ti:
						return TraceItemTemplate;
					case PpsTextItem tt:
						return TextItemTemplate;
					default:
						return null;
				}
			} // func GetTemplate
			return GetTemplate() ?? NullTemplate;
		} // proc SelectTemplate

		/// <summary>Template for the exception items</summary>
		public DataTemplate NullTemplate { get; set; }
		/// <summary>Template for the exception items</summary>
		public DataTemplate ExceptionTemplate { get; set; }
		/// <summary></summary>
		public DataTemplate TraceItemTemplate { get; set; }
		/// <summary></summary>
		public DataTemplate TextItemTemplate { get; set; }
	} // class PpsTraceItemTemplateSelector

	#endregion

	#region -- class ExceptionToPropertyConverter -------------------------------------

	internal sealed class ExceptionToPropertyConverter : IValueConverter
	{
		#region -- class ExceptionView ------------------------------------------------

		public sealed class ExceptionView : IEnumerable<PropertyValue>
		{
			private readonly string title;
			private readonly string type;
			private readonly string text;
			private readonly PropertyValue[] properties;

			public ExceptionView(string title, string type, string text, PropertyValue[] properties)
			{
				this.title = title;
				this.type = type ?? throw new ArgumentNullException(nameof(type));
				this.text = text;
				this.properties = properties ?? throw new ArgumentNullException(nameof(properties));
			} // ctor

			public IEnumerator<PropertyValue> GetEnumerator()
				=> ((IEnumerable<PropertyValue>)properties).GetEnumerator();

			IEnumerator IEnumerable.GetEnumerator()
				=> GetEnumerator();

			public string Title => title;
			public string Type => type;
			public string Text => text;
		} // class ExceptionView

		#endregion

		#region -- class ExceptionViewArrayFormatter ----------------------------------

		private sealed class ExceptionViewArrayFormatter : ExceptionFormatter
		{
			private List<ExceptionView> exceptions = new List<ExceptionView>();

			private string currentTitle;
			private Exception currentException;
			private List<PropertyValue> currentProperties = new List<PropertyValue>();

			protected override void AppendProperty(string name, Type type, Func<object> value)
			{
				var val = value.Invoke();
				if (val != null && !String.IsNullOrEmpty(val.ToString()))
					currentProperties.Add(new PropertyValue(name, type, value()));
			}

			protected override void AppendSection(bool isFirst, string sectionName, Exception ex)
			{
				if (isFirst)
				{
					exceptions.Clear();
					currentProperties.Clear();

					currentTitle = null;
					currentException = ex;
				}
				else
				{
					CompileCurrentException();
					currentProperties.Clear();

					currentTitle = sectionName;
					currentException = ex;
				}
			} // proc AppendSection

			private void CompileCurrentException()
			{
				if (currentException == null)
					throw new InvalidOperationException();

				exceptions.Add(new ExceptionView(currentTitle, currentException.GetType().Name, currentException.Message, currentProperties.ToArray()));
			} // proc CompileCurrentException

			protected override object Compile()
			{
				CompileCurrentException();
				return exceptions.ToArray();
			} // func Compile
		} // class ExceptionViewArrayFormatter

		#endregion

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value == null)
				return null;

			return value is Exception e
				? ExceptionFormatter.Format<ExceptionViewArrayFormatter>(e)
				: throw new ArgumentException(nameof(value));
		} // func Convert

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
			=> throw new NotSupportedException();
	} // class ExceptionToPropertyConverter

	#endregion
}
