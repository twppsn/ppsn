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
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Effects;
using TecWare.PPSn.Data;

namespace TecWare.PPSn.Controls
{
	#region -- class PpsTemplateTextBox -------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PpsTemplateTextBox : TextBox
	{
		public static readonly DependencyProperty TemplatePatternProperty = DependencyProperty.Register(nameof(TemplatePattern), typeof(string), typeof(PpsTemplateTextBox));
		public static readonly DependencyProperty DestinationFieldProperty = DependencyProperty.Register(nameof(DestinationField), typeof(string), typeof(PpsTemplateTextBox));
		public static readonly DependencyProperty CalculatedStringProperty = DependencyProperty.Register(nameof(CalculatedString), typeof(string), typeof(PpsTemplateTextBox));

		private Dictionary<string, string> patterns;

		public string ConvertTemplateString(object[] values)
		{
			CalculateString(values);
			return CalculatedString;
		} // func ConvertTemplateString

		protected override void OnTextChanged(TextChangedEventArgs e)
		{
			base.OnTextChanged(e);
			if (!IsKeyboardFocused || patterns==null)
				return;

			e.Handled = true;

			var row = DataContext as PpsDataRow;
			var cb = patterns.Count;
			dynamic[] values = new dynamic[cb];
			for (int i = 0; i < cb; i++)
				values[i] = row[patterns.Values.ElementAt(i)];

			CalculateString(values);
		} // event OnTextChanged

		private void CalculateString(object[] values)
		{
			var newString = ReplacePatterns(Text, values);
			UpdateProperties(newString);
		} // proc CalculateString

		private string ReplacePatterns(string template, object[] values)
		{
			if (String.IsNullOrEmpty(template))
				return String.Empty;

			var newString = template;
			var cb = patterns.Count;
			for (int i = 0; i < cb; i++)
			{
				var pattern = patterns.Keys.ElementAt(i);
				if (newString.Contains(pattern))
				{
					var value = (string)values[i] ?? String.Empty;
					newString = newString.Replace(pattern, value);
				}
			}
			return newString;
		} // func ReplacePatterns

		private void InitPatterns(string parm)
		{
			var items = parm.Split(';');
			var cb = items.Length;
			patterns = new Dictionary<string, string>(cb);
			for (int i = 0; i < cb; i++)
			{
				string key;
				string value;
				var item = items[i];
				var splitPos = item.IndexOf(':');
				if (splitPos >= 0)
				{
					key = item.Substring(0, splitPos);
					value = item.Substring(splitPos + 1);
				}
				else
				{
					key = item;
					value = item;
				}
				patterns.Add(String.Format("%{0}%", key), value);
			}
		} // proc InitPatterns

		#region -- properties -----------------------------------------------------------

		private void UpdateProperties(string newString)
		{
			// viewer
			if (String.Compare(newString, CalculatedString, false) != 0)
				CalculatedString = newString;
			// data
			if (String.Compare(newString, DestinationField, false) != 0)
				DestinationField = newString;
		} // proc  UpdateProperties

		public string SourceFields { get { throw new NotSupportedException(); } set { InitPatterns(value); } }
		public string TemplatePattern { get { return (string)GetValue(TemplatePatternProperty); } set { SetValue(TemplatePatternProperty, value); } }
		public string DestinationField { get { return (string)GetValue(DestinationFieldProperty); } set { SetValue(DestinationFieldProperty, value); } }
		public string CalculatedString { get { return (string)GetValue(CalculatedStringProperty); } set { SetValue(CalculatedStringProperty, value); } }

		#endregion

	} // class PpsTemplateTextBox

	#endregion

	#region -- class TemplateTextBoxResultConverter -------------------------------------

	public class TemplateTextBoxResultConverter : IMultiValueConverter
	{
		public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
		{
			// can not use binding for parameter
			var ctl = values[0] as PpsTemplateTextBox;
			// real params
			dynamic[] parms = new dynamic[values.Length - 1];
			Array.Copy(values, 1, parms, 0, values.Length - 1);
			return ctl.ConvertTemplateString(parms);
		}// func Convert

		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	} // TemplateTextBoxResultConverter

	#endregion

}
