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
using System.IO;
using System.Linq;
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

namespace TecWare.PPSn.UI
{
	#region -- class PpsTracePane -----------------------------------------------------

	/// <summary>Pane to display trace messages.</summary>
	internal sealed partial class PpsTracePane : PpsWindowPaneControl
	{
		public static readonly RoutedCommand AssignDebugTarget = new RoutedCommand("Assign", typeof(PpsTracePane));
		public static readonly RoutedCommand ClearDebugTarget = new RoutedCommand("Clear", typeof(PpsTracePane));

		#region -- class PpsTraceEnvironment ------------------------------------------

		private sealed class PpsTraceEnvironment : LuaTable
		{
			private readonly IPpsWindowPaneHost paneHost;
			
			public PpsTraceEnvironment(IPpsWindowPaneHost paneHost)
			{
				this.paneHost = paneHost ?? throw new ArgumentNullException(nameof(paneHost));
			} // ctor

			#region -- remove --

			[LuaMember]
			public void LoadPdf(string guid)
			{
				var obj = Environment.GetObject(new Guid(guid ?? "F83EA1D1-0248-4880-8FB9-6121960B3FF5"));
				obj.OpenPaneAsync(paneHost.PaneManager).AwaitTask();
			}

			[LuaMember]
			public void StartStat()
			{
				Environment["collectStatistics"] = true;
			} // proc StartState

			#endregion
			
			protected override object OnIndex(object key)
				=> base.OnIndex(key) ?? Context?.GetValue(key) ?? paneHost.PaneManager._Shell.GetValue(key);

			public LuaTable Context { get; set; } = null;

			[LuaMember]
			public IPpsWindowPaneManager Window => paneHost.PaneManager;
			[LuaMember]
			public PpsEnvironment Environment => (PpsEnvironment)paneHost.PaneManager._Shell;
		} // class PpsTraceEnvironment

		#endregion

		private readonly PpsTraceEnvironment traceEnvironment;
		
		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary>Trace pane constructor</summary>
		/// <param name="paneHost"></param>
		public PpsTracePane(IPpsWindowPaneHost paneHost)
			: base(paneHost)
		{
			traceEnvironment = new PpsTraceEnvironment(paneHost);

			InitializeComponent();

			this.AddCommandBinding(Shell, ApplicationCommands.SaveAs,
				new PpsCommand(
					 ctx => SaveTrace()
				)
			);

			this.AddCommandBinding(Shell, ApplicationCommands.Copy,
				new PpsCommand(
					 ctx => CopyToClipboard(ctx.Parameter),
					 ctx => CanCopyToClipboard(ctx.Parameter)
				)
			);

			this.AddCommandBinding(
				Shell, ApplicationCommands.Open,
				new PpsAsyncCommand(
					ctx => ExecuteCommandAsync(ConsoleCommandTextBox.Text),
					ctx => !String.IsNullOrEmpty(ConsoleCommandTextBox.Text)
				)
			);

			this.AddCommandBinding(Shell, AssignDebugTarget,
				new PpsAsyncCommand(
					ctx => UpdateDebugTargetAsync((PpsMasterDataRow)ctx.Parameter, false),
					ctx => ctx.Parameter is PpsMasterDataRow
				)
			);


			this.AddCommandBinding(Shell, ClearDebugTarget,
				new PpsAsyncCommand(
					ctx => UpdateDebugTargetAsync((PpsMasterDataRow)ctx.Parameter, true),
					ctx => ctx.Parameter is PpsMasterDataRow row && row.GetProperty("DebugPath", null) != null
				)
			);

			//var collectionView = CollectionViewSource.GetDefaultView(Environment?.Log);
			//collectionView.SortDescriptions.Add(new SortDescription(nameof(PpsTraceItemBase.Stamp), ListSortDirection.Descending));
		} // ctor

		protected override Task OnLoadAsync(LuaTable args)
		{
			DataContext = Environment; // set environment to DataContext
			return Task.CompletedTask;
		} // proc OnLoadAsync

		#endregion

		#region -- Execute Command ----------------------------------------------------

		private async Task ExecuteCommandAsync(string command)
		{
			try
			{
				var chunk = await Environment.CompileAsync(command, "command.lua", true);

				var r = chunk.Run(traceEnvironment);

				// print result
				var i = 0;
				foreach (var c in r)
					Environment.Log.Append(PpsLogType.Debug, $"${i++}: {c}");
			}
			catch (LuaParseException ex)
			{
				Environment.Log.Append(PpsLogType.Fail, String.Format("Could not parse command: {0}", ex.Message));
			}
			catch (Exception ex)
			{
				Environment.Log.Append(PpsLogType.Fail, ex, String.Format("Command \"{0}\" threw an Exception: {1}", command, ex.Message));
			}
		} // proc ExecuteCommandAsync

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
				if (Environment.Log is PpsTraceLog log)
				{
					foreach (var c in log.OfType<PpsTraceItemBase>())
					{
						sw.Write(c.Type);
						sw.Write(';');
						sw.Write(c.Stamp.ToString("G"));
						sw.Write(';');

						if (c is PpsExceptionItem exi)
						{
							sw.Write('"');
							sw.Write(ExceptionFormatter.FormatPlainText(exi.Exception).Replace("\"", "\"\""));
							sw.Write('"');
						}
						else
							sw.Write(c.Message.Replace("\"", "\"\""));
						sw.WriteLine();
					}
				}
				else
					sw.WriteLine("Log is not a trace log?");

				// write statistics
				foreach (var statistic in Environment.Statistics)
				{
					if (!statistic.HasData)
						continue;
					var en = statistic.History.GetEnumerator();
					var historic = 0;
					en.Reset();
					while (en.MoveNext())
					{
						sw.WriteLine($"{statistic.Name};{DateTime.Now.AddSeconds(historic)};{en.Current}");
						historic--;
					}
				}
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
			=> item != null || logList.SelectedItem != null;

		private void CopyToClipboard(object item)
		{
			var clipText = TraceToString(item ?? logList.SelectedItem);
			if (clipText != null)
				Clipboard.SetText(clipText);
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

		private async Task UpdateDebugTargetAsync(PpsMasterDataRow row, bool clear)
		{
			var path = row.GetProperty("Path", String.Empty);
			if (String.IsNullOrEmpty(path))
				return;

			var newFileName = clear ? null : GetDebugTargetFileName(path, row.GetProperty("DebugPath", null));
			try
			{
				await Environment.MasterData.UpdateDebugPathAsync(row.RowId, path, newFileName);
			}
			catch (Exception e)
			{
				Environment.ShowException(e);
			}
		} // proc UpdateDebugTargetAsync

		#endregion

		protected override IEnumerator LogicalChildren
			=> Procs.CombineEnumerator(base.LogicalChildren, Commands?.GetEnumerator());

		/// <summary>Access the environment</summary>
		public PpsEnvironment Environment => (PpsEnvironment)PaneHost.PaneManager._Shell;
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
