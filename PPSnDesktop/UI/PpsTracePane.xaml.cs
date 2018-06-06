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
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;
using Neo.IronLua;
using TecWare.DE.Stuff;

namespace TecWare.PPSn.UI
{
	#region -- class PpsTracePane -----------------------------------------------------

	/// <summary>Pane to display trace messages.</summary>
	internal sealed partial class PpsTracePane : UserControl, IPpsWindowPane
	{
		// ignore any property changed
		event PropertyChangedEventHandler INotifyPropertyChanged.PropertyChanged { add { } remove { } }

		/// <summary>Command to execute Lua-Code on the current Environment</summary>
		public readonly static RoutedUICommand ExecuteCommandCommand =
			new RoutedUICommand("ExecuteCommand", "ExecuteCommand", typeof(PpsTracePane));


		private readonly IPpsWindowPaneManager paneManager;
		private readonly IPpsWindowPaneHost paneHost;
		private readonly PpsUICommandCollection commands;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary>Trace pane constructor</summary>
		/// <param name="paneManager"></param>
		/// <param name="paneHost"></param>
		public PpsTracePane(IPpsWindowPaneManager paneManager, IPpsWindowPaneHost paneHost)
		{
			this.paneManager = paneManager ?? throw new ArgumentNullException(nameof(paneManager));
			this.paneHost = paneHost ?? throw new ArgumentNullException(nameof(paneHost));

			InitializeComponent();

			Resources[PpsEnvironment.WindowPaneService] = this;

			this.commands = new PpsUICommandCollection
			{
				AddLogicalChildHandler = AddLogicalChild,
				RemoveLogicalChildHandler = RemoveLogicalChild
			};

			//commands.AddButton("100:100", "CopySelected", CopySelectedTraceItemsCommand, "InZwischenable", "Kopiert alle markierten Einträge in die Zwischenablage.");

			CommandBindings.Add(
				new CommandBinding(ExecuteCommandCommand,
					(sender, e) =>
					{
						try
						{
							var ret = Environment.DoChunk((string)e.Parameter, "DebugCommand", null);
							if (String.IsNullOrEmpty(ret.ToString()))
								Environment.Traces.AppendText(PpsTraceItemType.Debug, $"Command \"{(string)e.Parameter}\" executed without result.");
							else
								Environment.Traces.AppendText(PpsTraceItemType.Debug, $"Command \"{(string)e.Parameter}\" returned: \"{ret.ToString()}\".");
						}
						catch (Exception ex)
						{
							Environment.Traces.AppendException(ex, $"Command \"{(string)e.Parameter}\" threw an Exception.");
						}
					},
					(sender, e)=>e.CanExecute=!String.IsNullOrWhiteSpace((string)e.Parameter)
				)
			);
			CommandBindings.Add(
				new CommandBinding(ApplicationCommands.Copy,
					(sender, e) =>
					{
						if (e.Parameter != null)
							CopyToClipboard(e.Parameter);
						else if (e.OriginalSource is PpsTracePane pt
								&& pt.Content is Grid grid
								&& grid.Children.Count > 0
								&& grid.Children[0] is ListBox exc)
							CopyToClipboard(exc.SelectedItem);
						e.Handled = true;
					},
					(sender, e) => e.CanExecute = true
				)
			);
			CommandBindings.Add(
				new CommandBinding(ApplicationCommands.SaveAs,
					(sender, e) =>
					{

						if (e.Source is PpsTracePane)
							if (((PpsTracePane)e.Source).Content is Grid)
								if (((Grid)((PpsTracePane)e.Source).Content).Children.Count > 0)
									if (((Grid)((PpsTracePane)e.Source).Content).Children[0] is ListBox exc)
									{
										var list = exc.Items;

										var openFileDialog = new SaveFileDialog
										{
											Filter = "TraceLog | *.csv;",
											DefaultExt = ".csv",
											CheckFileExists = false,
											CheckPathExists = true,
											AddExtension = true
										};
										if (openFileDialog.ShowDialog() == true)
										{
											if (File.Exists(openFileDialog.FileName))
												if (MessageBox.Show("Die Datei existiert bereits. Möchten Sie überschreiben?", "Warnung", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
												{
													e.Handled = true;
													return;
												}

											var file = new StreamWriter(openFileDialog.FileName);
											foreach (var itm in list)
											{
												if (itm is PpsExceptionItem pei)
													file.WriteLine($"{pei.Type};{pei.Stamp};\"{ExceptionFormatter.FormatPlainText(pei.Exception)}\"");
												else
													file.WriteLine($"{((dynamic)itm).Type};{((dynamic)itm).Stamp};\"{((dynamic)itm).Message}\"");
											}
											foreach (var statistic in Environment.Statistics)
											{
												var en = statistic.History.GetEnumerator();
												var historic = 0;
												en.Reset();
												while (en.MoveNext())
												{
													file.WriteLine($"{statistic.Name};{DateTime.Now.AddSeconds(historic)};{en.Current}");
													historic--;
												}
											}
											file.Close();
										}
									}
						e.Handled = true;
					},
					(sender, e) => e.CanExecute = true
				)
			);
		} // ctor

		void IDisposable.Dispose()
		{
		} // proc Dispose

		#endregion

		#region -- IPpsWindowPane members ---------------------------------------------

		PpsWindowPaneCompareResult IPpsWindowPane.CompareArguments(LuaTable args)
			=> PpsWindowPaneCompareResult.Same;

		Task IPpsWindowPane.LoadAsync(LuaTable args)
		{
			DataContext = Environment; // set environment to DataContext
			return Task.CompletedTask;
		} // proc LoadAsync

		Task<bool> IPpsWindowPane.UnloadAsync(bool? commit)
		{
			return Task.FromResult(true);
		} // func UnloadAsync

		string IPpsWindowPane.Title => "System";
		string IPpsWindowPane.SubTitle => "Anwendungsereignisse";

		object IPpsWindowPane.Control => this;
		IPpsWindowPaneManager IPpsWindowPane.PaneManager => paneManager;
		IPpsWindowPaneHost IPpsWindowPane.PaneHost => paneHost;
		PpsUICommandCollection IPpsWindowPane.Commands => commands;

		bool IPpsWindowPane.HasSideBar => false;
		bool IPpsWindowPane.IsDirty => false;

		#endregion

		private void CopyToClipboard(object item)
			=> Clipboard.SetText(TraceToString(item)); // ToDo: enable Html/RichText/PlainText

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
				case null:
					return "<null>";
				default:
					return String.Empty;
			}
		} // func TraceToString

		protected override IEnumerator LogicalChildren
			=> Procs.CombineEnumerator(base.LogicalChildren, commands?.GetEnumerator());

		/// <summary>Access the environment</summary>
		public PpsEnvironment Environment => paneManager.Environment;
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
