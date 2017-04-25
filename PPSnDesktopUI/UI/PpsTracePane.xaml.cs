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
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Neo.IronLua;
using TecWare.DE.Stuff;
using Microsoft.Win32;
using System.IO;

namespace TecWare.PPSn.UI
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	internal partial class PpsTracePane : UserControl, IPpsWindowPane
	{
		public event PropertyChangedEventHandler PropertyChanged { add { } remove { } }

		public PpsTracePane()
		{
			InitializeComponent();

			((CollectionViewSource)this.Resources["SortedTraces"]).SortDescriptions.Add(new SortDescription("Stamp", ListSortDirection.Descending));

			CommandBindings.Add(
				new CommandBinding(ApplicationCommands.Copy,
					(sender, e) =>
					{
						if (e.Parameter != null)
							CopyToClipboard(e.Parameter);
						else if (e.OriginalSource is PpsTracePane)
							if (((PpsTracePane)e.OriginalSource).Content is Grid)
								if (((Grid)((PpsTracePane)e.OriginalSource).Content).Children.Count > 0)
									if (((Grid)((PpsTracePane)e.OriginalSource).Content).Children[0] is ListBox)
									{
										var exc = (ListBox)((Grid)((PpsTracePane)e.OriginalSource).Content).Children[0];
										CopyToClipboard(exc.SelectedItem);
									}
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
									if (((Grid)((PpsTracePane)e.Source).Content).Children[0] is ListBox)
									{
										var exc = (ListBox)((Grid)((PpsTracePane)e.Source).Content).Children[0];
										var list = exc.Items;

										var openFileDialog = new SaveFileDialog();
										openFileDialog.Filter = "TraceLog | *.csv;";
										openFileDialog.DefaultExt = ".csv";
										openFileDialog.CheckFileExists = false;
										openFileDialog.CheckPathExists = true;
										openFileDialog.AddExtension = true;
										if (openFileDialog.ShowDialog() == true)
										{
											if (File.Exists(openFileDialog.FileName))
												if (MessageBox.Show("Die Datei existiert bereits. Möchten Sie überschreiben?", "Warnung", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
												{
													e.Handled = true;
													return;
												}

											System.IO.StreamWriter file = new System.IO.StreamWriter(openFileDialog.FileName);
											foreach (var itm in list)
											{
												if (itm is PpsExceptionItem)
													file.WriteLine($"{((dynamic)itm).Type};{((dynamic)itm).Stamp};\"{ExceptionFormatter.FormatPlainText(((PpsExceptionItem)itm).Exception)}\"");
												else
													file.WriteLine($"{((dynamic)itm).Type};{((dynamic)itm).Stamp};\"{((dynamic)itm).Message}\"");


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

		public void Dispose()
		{
		} // proc Dispose

		public Task LoadAsync(LuaTable args)
		{
			var environment = (args["Environment"] as PpsEnvironment) ?? PpsEnvironment.GetEnvironment(this);
			DataContext = environment;

			return Task.CompletedTask;
		} // proc LoadAsync

		public Task<bool> UnloadAsync(bool? commit = default(bool?))
		{
			return Task.FromResult(true);
		} // func UnloadAsync

		private void CopyToClipboard(object item)
		{
			Clipboard.SetText(TraceToString(item)); // ToDo: enable Html/RichText/PlainText
		}

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

		public PpsWindowPaneCompareResult CompareArguments(LuaTable args) => PpsWindowPaneCompareResult.Same;

		public string Title => "Anwendungsereignisse";
		public string SubTitle => "System";
		public object Control => this;
		public IPpsPWindowPaneControl PaneControl => null;
		public bool IsDirty => false;
		public bool HasSideBar => false;

		//public readonly static RoutedCommand CopyTraceCommand = new RoutedCommand("CopyTrace", typeof(PpsTracePane));


		public object Commands => null;
	} // class PpsTracePane

	#region -- class TraceItemTemplateSelector ------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	internal sealed class TraceItemTemplateSelector : DataTemplateSelector
	{
		public override DataTemplate SelectTemplate(object item, DependencyObject container)
		{
			var r = (DataTemplate)null;
			var resources = container as ContentPresenter;
			if (item != null && resources != null)
			{
				if (item is PpsExceptionItem || item is Exception)
					r = ExceptionTemplate;
				else if (item is PpsTraceItem)
					r = TraceItemTemplate;
				else if (item is PpsTextItem)
					r = TextItemTemplate;
			}
			return r ?? NullTemplate;
		} // proc SelectTemplate

		/// <summary>Template for the exception items</summary>
		public DataTemplate NullTemplate { get; set; }
		/// <summary>Template for the exception items</summary>
		public DataTemplate ExceptionTemplate { get; set; }
		/// <summary></summary>
		public DataTemplate TraceItemTemplate { get; set; }
		/// <summary></summary>
		public DataTemplate TextItemTemplate { get; set; }
	} // class TraceItemTemplateSelector

	#endregion

	#region -- class ExceptionToPropertyConverter ---------------------------------------

	public sealed class ExceptionToPropertyConverter : IValueConverter
	{
		#region -- class ExceptionView --------------------------------------------------

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

		#region -- class ExceptionViewArrayFormatter ------------------------------------

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

	public class BoolToVisibilityConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if ((bool)value)
				return Visibility.Visible;
			else return Visibility.Collapsed;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
