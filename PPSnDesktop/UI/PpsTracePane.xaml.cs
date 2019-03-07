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
using TecWare.PPSn.Data;

namespace TecWare.PPSn.UI
{
	#region -- class PpsTracePane -----------------------------------------------------

	/// <summary>Pane to display trace messages.</summary>
	internal sealed partial class PpsTracePane : UserControl, IPpsWindowPane
	{
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
				=> base.OnIndex(key) ?? Context?.GetValue(key) ?? paneHost.PaneManager.Shell.GetValue(key);

			public LuaTable Context { get; set; } = null;

			[LuaMember]
			public IPpsWindowPaneManager Window => paneHost.PaneManager;
			[LuaMember]
			public PpsEnvironment Environment => (PpsEnvironment)paneHost.PaneManager.Shell;
		} // class PpsTraceEnvironment

		#endregion

		// ignore any property changed, because all properties are static
		event PropertyChangedEventHandler INotifyPropertyChanged.PropertyChanged { add { } remove { } }
			
		private readonly IPpsWindowPaneHost paneHost;
		private readonly PpsTraceEnvironment traceEnvironment;
		private readonly PpsUICommandCollection commands;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary>Trace pane constructor</summary>
		/// <param name="paneHost"></param>
		public PpsTracePane(IPpsWindowPaneHost paneHost)
		{
			this.paneHost = paneHost ?? throw new ArgumentNullException(nameof(paneHost));
			traceEnvironment = new PpsTraceEnvironment(paneHost);

			InitializeComponent();

			Resources[PpsWindowPaneHelper.WindowPaneService] = this;

			commands = new PpsUICommandCollection
			{
				AddLogicalChildHandler = AddLogicalChild,
				RemoveLogicalChildHandler = RemoveLogicalChild
			};

			commands.AddButton("100;100", "save", ApplicationCommands.SaveAs, "Speichern", "Speichere alle Log in eine Datei.");
			commands.AddButton("100;200", "copy", ApplicationCommands.Copy, "Kopieren", "Kopiere markierte Einträge in die Zwischenablage.");

			CommandBindings.Add(new CommandBinding(ApplicationCommands.Open,
				(sender, e) => { ExecuteCommandAsync(ConsoleCommandTextBox.Text).SpawnTask(Environment); e.Handled = true; },
				(sender, e) => e.CanExecute = !String.IsNullOrEmpty(ConsoleCommandTextBox.Text)
			));
			CommandBindings.Add(new CommandBinding(ApplicationCommands.SaveAs,
				(sender, e) => { SaveTrace(); e.Handled = true; }
			));
			CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy,
				(sender, e) => { CopyToClipboard(e.Parameter); e.Handled = true; },
				(sender, e) => e.CanExecute = e.Parameter != null || logList.SelectedItem != null
			));
		} // ctor

		void IDisposable.Dispose()
		{
		} // proc Dispose

		#endregion

		#region -- IPpsWindowPane members ---------------------------------------------

		PpsWindowPaneCompareResult IPpsWindowPane.CompareArguments(LuaTable args)
			=> PpsWindowPaneCompareResult.Same; // only one trace, per window

		Task IPpsWindowPane.LoadAsync(LuaTable args)
		{
			DataContext = Environment; // set environment to DataContext
			return Task.CompletedTask;
		} // proc LoadAsync

		Task<bool> IPpsWindowPane.UnloadAsync(bool? commit)
			=> Task.FromResult(true);

		string IPpsWindowPane.Title => "System";
		string IPpsWindowPane.SubTitle => "Anwendungsereignisse";
		object IPpsWindowPane.Image => null;

		object IPpsWindowPane.Control => this;
		IPpsWindowPaneHost IPpsWindowPane.PaneHost => paneHost;
		PpsUICommandCollection IPpsWindowPane.Commands => commands;

		bool IPpsWindowPane.HasSideBar => false;
		bool IPpsWindowPane.IsDirty => false;
		IPpsDataInfo IPpsWindowPane.CurrentData => null;
		string IPpsWindowPane.HelpKey => "PpsnTracePane";

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

		private void CopyToClipboard(object item)
		{
			var clipText = TraceToString(item ?? logList.SelectedItem);
			if (clipText != null)
				Clipboard.SetText(clipText);
		} // proc CopyToClipboard

		#endregion
		
		protected override IEnumerator LogicalChildren
			=> Procs.CombineEnumerator(base.LogicalChildren, commands?.GetEnumerator());

		/// <summary>Access the environment</summary>
		public PpsEnvironment Environment => (PpsEnvironment)paneHost.PaneManager.Shell;
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
