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
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using TecWare.PPSn.Data;

namespace TecWare.PPSn.Controls
{
	/// <summary>Extended ComboBox which enables searching, with Highlighting</summary>
	public partial class PpsMasterDataSelector : UserControl, INotifyPropertyChanged
	{
		#region -- Constants ----------------------------------------------------------

		private const string defaultTemplate = "<DataTemplate xmlns:local=\"clr-namespace:TecWare.PPSn.Controls;assembly=PPSn.Desktop.UI\">" +
												"	<local:SearchHighlightTextBlock" +
												"		Width=\"{Binding RelativeSource={RelativeSource Mode=FindAncestor, AncestorType=ListBox}, Path=ActualWidth}\"" +
												"		BaseText=\"{Binding <DisplayMemberName/>}\"" +
												"		SearchText=\"{Binding RelativeSource={RelativeSource Mode=FindAncestor, AncestorType=UserControl}, Path=FilterText}\"/>" +
												"</DataTemplate>";
		private const double refreshHoldOff = 400;

		#endregion

		#region -- Helper Classes -----------------------------------------------------

		/// <summary>Interface for handleing the internal representation</summary>
		internal interface IPpsConstant
		{
			/// <summary>The DataRow of this Instance</summary>
			PpsMasterDataRow Row { get; }
			/// <summary>returns the value selected by the DisplayMemberPath</summary>
			string Name { get; }
		}

		#endregion

		#region -- Events -------------------------------------------------------------

		public event PropertyChangedEventHandler PropertyChanged;

		/// <summary>Moves the focus to the SearchBox, puts the actual Name in the Searchbox to not overwrite the Value</summary>
		/// <param name="sender"></param>
		/// <param name="e">not used</param>
		private void SelectedConstantTextbox_GotFocus(object sender, RoutedEventArgs e)
		{
			SearchConstantTextbox.Focus();
			SearchConstantTextbox.Text = ((TextBox)sender).Text;
		}

		/// <summary>KeyUp-Event to handle Keyboard Navifgation in the List & fast-delete the Searchstring</summary>
		/// <param name="sender">SearchTextBox</param>
		/// <param name="e">unused</param>
		private void SearchConstantTextbox_KeyUp(object sender, KeyEventArgs e)
		{
			var sendingTextBox = ((TextBox)sender);

			switch (e.Key)
			{
				case Key.Enter:
					var selected = (ConstantsListbox.SelectedItem ?? (ConstantsListbox.Items.Count > 0 ? ConstantsListbox.Items[0] : null));
					if (selected != null)
					{
						SelectedValue = (PpsMasterDataRow)selected;
						e.Handled = true;
						sendingTextBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
						return;
					}
					break;
				case Key.Down:
					if (ConstantsListbox.SelectedIndex < ConstantsListbox.Items.Count)
					{
						ConstantsListbox.SelectedIndex++;
					}
					else
						ConstantsListbox.SelectedIndex = 0;
					e.Handled = true;
					return;
				case Key.Up:
					if (ConstantsListbox.SelectedIndex > 0)
						ConstantsListbox.SelectedIndex--;
					else
						ConstantsListbox.SelectedIndex = ConstantsListbox.Items.Count - 1;
					e.Handled = true;
					return;
				case Key.Back:
					if (sendingTextBox.CaretIndex == 0)
					{
						sendingTextBox.Text = String.Empty;
						e.Handled = true;
						return;
					}
					break;
			}
		}

		protected virtual void OnConstantsSourceChanged()
		{
			if (searchBreaker == null)
			{
				searchBreaker = new Timer(refreshHoldOff);
				searchBreaker.AutoReset = false;
				searchBreaker.Elapsed += (sender, e) =>
				{
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("FilteredList"));
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("SearchHelp"));
					searchBreaker = null;
				};
				searchBreaker.Start();
			}
			else
			{
				searchBreaker.Interval = refreshHoldOff;
			}
		}

		#endregion

		#region -- Fields -------------------------------------------------------------

		private Timer searchBreaker;

		#endregion

		#region -- Constructors -------------------------------------------------------

		public PpsMasterDataSelector()
		{
			InitializeComponent();
		}

		#endregion

		#region -- Methods ------------------------------------------------------------

		private static ParserContext GetDefaultContext()
		{
			var context = new ParserContext();

			context.XamlTypeMapper = new XamlTypeMapper(new string[0]);

			context.XmlnsDictionary.Add("", "http://schemas.microsoft.com/winfx/2006/xaml/presentation");
			context.XmlnsDictionary.Add("x", "http://schemas.microsoft.com/winfx/2006/xaml");

			return context;
		}

		#endregion

		#region -- Properties ---------------------------------------------------------

		/// <summary>List of Constants to select from</summary>
		public PpsMasterDataTable ConstantsSource { get => (PpsMasterDataTable)GetValue(ConstantsSourceProperty); set { SetValue(ConstantsSourceProperty, value); PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FilteredList))); } }
		public static readonly DependencyProperty ConstantsSourceProperty = DependencyProperty.Register(nameof(ConstantsSource), typeof(PpsMasterDataTable), typeof(PpsMasterDataSelector), new FrameworkPropertyMetadata(new PropertyChangedCallback((sender, e) => ((PpsMasterDataSelector)sender)?.OnConstantsSourceChanged())));
		/// <summary>Actual Constant</summary>
		public PpsMasterDataRow SelectedValue { get => (PpsMasterDataRow)(GetValue(SelectedValueProperty) ?? DependencyProperty.UnsetValue); set { SetValue(SelectedValueProperty, value); PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedValue))); } }
		private static readonly DependencyProperty SelectedValueProperty = DependencyProperty.Register(nameof(SelectedValue), typeof(PpsMasterDataRow), typeof(PpsMasterDataSelector), new FrameworkPropertyMetadata { BindsTwoWayByDefault = true });
		/// <summary>Property of the Constant to show in the UI - may change to a template</summary>
		public string DisplayMemberPath { get => (string)GetValue(DisplayMemberPathProperty); set => SetValue(DisplayMemberPathProperty, value); }
		public static readonly DependencyProperty DisplayMemberPathProperty = DependencyProperty.Register(nameof(DisplayMemberPath), typeof(string), typeof(PpsMasterDataSelector));

		public IDataRowEnumerable FilteredList => ConstantsSource?.ApplyFilter(PpsDataFilterExpression.Parse(FilterText));

		/// <summary>If a string is passed it is parsed as a DataTemplate for the ListItems</summary>
		public string ListTemplateString { get => (string)GetValue(ListTemplateStringProperty); set { SetValue(ListTemplateStringProperty, value); PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ListTemplate))); } }
		public static readonly DependencyProperty ListTemplateStringProperty = DependencyProperty.Register(nameof(ListTemplateString), typeof(string), typeof(PpsMasterDataSelector));
		public DataTemplate ListTemplate => (DataTemplate)XamlReader.Parse(!String.IsNullOrEmpty(ListTemplateString) ? ListTemplateString : defaultTemplate.Replace("<DisplayMemberName/>", DisplayMemberPath), GetDefaultContext());

		/// <summary>Current searchstring</summary>
		public string FilterText { get => (string)GetValue(FilterTextProperty); set => SetValue(FilterTextProperty, value); }
		public static readonly DependencyProperty FilterTextProperty = DependencyProperty.Register(nameof(FilterText), typeof(string), typeof(PpsMasterDataSelector), new FrameworkPropertyMetadata(new PropertyChangedCallback((sender, e) => ((PpsMasterDataSelector)sender)?.OnConstantsSourceChanged())));

		public string SearchHelp
		{
			get
			{
				if (ConstantsSource == null)
					return String.Empty;
				var ret = "Durchsuchbare Felder :" + Environment.NewLine;
				foreach (var column in ConstantsSource?.Columns)
					ret += column.Name + ", ";
				ret = ret.Substring(0, ret.Length - 2) + ".";
				return ret;
			}
		}

		#endregion
	} // class PpsMasterDataSelector

	#region -- SearchHighlightTextBox -------------------------------------------------

	/// <summary>This TextBox enables highlighting parts of the Text - BaseText is the input text, SearchText is the whitespace-separated list of keywords</summary>
	public class SearchHighlightTextBlock : TextBlock
	{
		#region -- Events -------------------------------------------------------------

		private static void OnDataChanged(DependencyObject source,
										  DependencyPropertyChangedEventArgs e)
		{
			var textBlock = (SearchHighlightTextBlock)source;
			if (String.IsNullOrWhiteSpace(textBlock.BaseText))
				return;
			textBlock.Inlines.Clear();

			textBlock.Inlines.AddRange(HighlightSearch(textBlock.BaseText, textBlock.SearchText, (t) => new Bold(new Italic(t))));
		}

		#endregion

		#region -- Constructors -------------------------------------------------------

		public SearchHighlightTextBlock()
			: base()
		{ }

		#endregion

		#region -- Methods ------------------------------------------------------------

		/// <summary>This function Highlights parts of a string.</summary>
		/// <param name="Text">Input text to format</param>
		/// <param name="Searchtext">Whitespace-separated list of keywords</param>
		/// <param name="Highlight">Function to Highlight, p.e. ''(t) => new Bold(new Italic(t))''</param>
		/// <returns>List of Inlines</returns>
		private static IEnumerable<Inline> HighlightSearch(string Text, string Searchtext, Func<Inline, Inline> Highlight)
		{
			var result = new List<Inline>();

			if (String.IsNullOrWhiteSpace(Searchtext))
			{
				// no searchstring - the whole Text is returned unaltered
				result.Add(new Run(Text));
				return result;
			}

			var i = 0;
			var searchtexts = Searchtext.Trim(' ').Split(' ');
			while (i < searchtexts.Count())
			{
				// iterate through all search filters
				var idx = Text.IndexOf(searchtexts[i], StringComparison.CurrentCultureIgnoreCase);
				if (idx >= 0)
				{
					// recurse in the part before and after the found text and concatenate the searchstring bold
					result.AddRange(HighlightSearch(Text.Substring(0, idx), Searchtext, Highlight));
					//ret.Add((Inline)Activator.CreateInstance(Highlight, new Run(Text.Substring(idx, searchtexts[i].Length))));
					result.Add(Highlight(new Run(Text.Substring(idx, searchtexts[i].Length))));
					result.AddRange(HighlightSearch(Text.Substring(idx + searchtexts[i].Length), Searchtext, Highlight));
					return result;
				}
				i++;
			}

			// end of recursion - no search string found in substring
			result.Add(new Run(Text));
			return result;
		} // func HighlightSearch

		#endregion

		#region -- Propertys ----------------------------------------------------------

		/// <summary>Keywords to search for, separated by whitespace</summary>
		public string SearchText { get => (string)GetValue(SearchTextProperty); set => SetValue(SearchTextProperty, value); }
		public static readonly DependencyProperty SearchTextProperty =
			DependencyProperty.Register(nameof(SearchText), typeof(string), typeof(SearchHighlightTextBlock), new FrameworkPropertyMetadata(null, OnDataChanged));

		/// <summary>
		/// Original Unformatted Text
		/// </summary>
		public String BaseText { get => (string)GetValue(BaseTextProperty); set => SetValue(BaseTextProperty, value); }
		public static readonly DependencyProperty BaseTextProperty =
			DependencyProperty.Register(nameof(BaseText), typeof(string), typeof(SearchHighlightTextBlock), new FrameworkPropertyMetadata(null, OnDataChanged));

		#endregion

	} // class SearchHighlightTextBox

	#endregion

	#region -- Converters -------------------------------------------------------------

	/// <summary>This converter just evaluates all parameters or'ed together</summary>
	class OringMultiValueConverter : IMultiValueConverter
	{
		#region -- Interface Functions ------------------------------------------------

		/// <summary>concatenates every value with or</summary>
		/// <param name="values">Bool values</param>
		/// <param name="targetType">Unused</param>
		/// <param name="parameter">Unused</param>
		/// <param name="culture">Unused</param>
		/// <returns>Evaluation of value[0] || values[1] ...</returns>
		public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
		{
			if (values == null)
				return false;

			return (from val in values where (bool)val select val).FirstOrDefault() != null;
		}

		/// <summary>not implemented</summary>
		/// <param name="value">Unused</param>
		/// <param name="targetTypes">Unused</param>
		/// <param name="parameter">Unused</param>
		/// <param name="culture">Unused</param>
		/// <returns></returns>
		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}

		#endregion
	} // class OringMultiValueConverter

	/// <summary>Does the glueing of Constants and internal representation</summary>
	class MasterDataConstantsConverter : IMultiValueConverter
	{
		#region -- Helper Classes -----------------------------------------------------

		/// <summary>Encapsulation for better handling</summary>
		public class PpsConstant : PpsMasterDataSelector.IPpsConstant
		{
			private PpsMasterDataRow row;
			private string visualreturnidentifier;

			public PpsConstant(PpsMasterDataRow Row, string DisplayMemberPath)
			{
				this.row = Row;
				this.visualreturnidentifier = DisplayMemberPath;
			}

			/// <summary>returns the value selected by the DisplayMemberPath</summary>
			public string Name => row[visualreturnidentifier].ToString();
			/// <summary>The DataRow of this Instance</summary>
			public PpsMasterDataRow Row => row;
		} // class PpsConstant

		#endregion

		#region -- Helper Functions ---------------------------------------------------

		private bool SearchFilter(PpsMasterDataRow row, string SearchString)
		{
			if (String.IsNullOrEmpty(SearchString))
				return true;

			var searchparts = SearchString.Trim(' ').Split(' ');
			foreach (var searchitem in searchparts)
			{
				var found = false;
				foreach (var col in row.Columns)
					if (row[col.Name].ToString().Contains(searchitem))
						found = true;
				if (!found)
					return false;
			}

			return true;
		}

		#endregion

		#region -- Interface Functions ------------------------------------------------

		public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
		{
			if (values[0] == null)
				return DependencyProperty.UnsetValue;

			if (values[0].GetType() == typeof(string))
				if (values[1] != null)
				{
					var a = ((PpsMasterDataRow)values[1])[(string)values[0]];
					return a;
				}

			return DependencyProperty.UnsetValue;
		}

		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}

		#endregion
	} // class MasterDataConstantsConverter

	class MasterDataConstantConverter : IValueConverter
	{
		#region -- Interface Functions ------------------------------------------------

		/// <summary>just for Interface implementation</summary>
		/// <param name="value">Input</param>
		/// <param name="targetType">Unused</param>
		/// <param name="parameter">Unused</param>
		/// <param name="culture">Unused</param>
		/// <returns>Input</returns>
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return value;
		}

		/// <summary>Converts the inner Representation to the underlying Constant type</summary>
		/// <param name="value">IPpsConstant</param>
		/// <param name="targetType">Unused</param>
		/// <param name="parameter">Unused</param>
		/// <param name="culture">Unused</param>
		/// <returns>PpsMasterDataRow</returns>
		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return value != null ? ((PpsMasterDataSelector.IPpsConstant)value).Row : DependencyProperty.UnsetValue;
		}

		#endregion
	} // class MasterDataConstantConverter

	#endregion
} // namespace TecWare.PPSn.Controls