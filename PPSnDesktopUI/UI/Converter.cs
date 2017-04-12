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
using System.Windows.Data;
using Neo.IronLua;
using TecWare.DE.Stuff;
using System.Collections;

namespace TecWare.PPSn.UI
{
	#region -- class VisibilityConverter ------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class VisibilityConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if ((bool)Lua.RtConvertValue(value, typeof(bool)))
				return TrueValue;
			else
				return FalseValue;
		} // func Convert

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return (Visibility)value == TrueValue;
		} // func ConvertBack

		public Visibility TrueValue { get; set; } = Visibility.Visible;
		public Visibility FalseValue { get; set; } = Visibility.Hidden;
	} // class VisibilityConverter

	#endregion

	#region -- class PpsStringConverter -------------------------------------------------

	public sealed class PpsStringConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
			=> value == null ? String.Empty : String.Format((string)parameter ?? Text, RemoveNewLines(value));

		object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		} // func ConvertBack

		private string RemoveNewLines(object value)
			=> value.ToString().Replace(Environment.NewLine, " ");

		public string Text { get; set; } = "{0}";
	} // class PpsStringConverter

	#endregion

	#region -- class PpsMultiLineStringConverter ----------------------------------------

	public sealed class PpsMultiLineStringConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
			=> value == null ? String.Empty : RemoveNewLines(value);

		object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		} // func ConvertBack

		private string RemoveNewLines(object value)
			=> value.ToString().Replace(Environment.NewLine, " ");

	} // class PpsMultiLineStringConverter

	#endregion

	#region -- class PpsSingleLineConverter ---------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsSingleLineConverter : IValueConverter
	{
		/// <summary></summary>
		/// <param name="value"></param>
		/// <param name="targetType"></param>
		/// <param name="parameter"></param>
		/// <param name="culture"></param>
		/// <returns></returns>
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value == null)
				return null;

			var ellipse = false;
			var txt = value.ToString().TrimStart('\n', ' ', '\r');
			var p = txt.IndexOf('\n');

			if (p >= 0)
			{
				txt = txt.Substring(0, p).TrimEnd();
				ellipse = true;
			}

			if (parameter != null)
			{
				var maxLen = Procs.ChangeType<int>(parameter);
				if (maxLen > 1 && txt.Length > maxLen)
				{
					txt = txt.Substring(0, maxLen);
					ellipse = true;
				}
			}

			if (ellipse)
				txt += "...";

			return txt;
		} // func Convert

		object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		} // func ConvertBack
	} // class PpsSingleLineConverter

	#endregion

	public sealed class PpsCommandParameterPassthroughConverter : IMultiValueConverter
	{
		public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
		{
			return values.Clone();
		} // func Convert

		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}

	}

	public sealed class ManyToTopTenConverter : IMultiValueConverter
	{
		private static IEnumerable<PpsTraceItemBase> GetLast(IList list, int count)
		{
			var end = list.Count - count;
			for (var i = Math.Max(end,0); i < list.Count; i++)
				yield return (PpsTraceItemBase)list[i];
		}

		//public class III : IEnumerable<PpsTraceItemBase>
		//{
		//	private readonly PpsTraceLog trace;


		//}

		public object Convert(object[] value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			var ret = new System.Collections.ObjectModel.ObservableCollection<PpsTraceItemBase>();

			if (value == null)
				return ret;

			return GetLast((IList)value[0], 10); // (from PpsTraceItemBase item in (value[0] as PpsTraceLog) select item).Take(10).Reverse();			
		}

		public object[] ConvertBack(object value, System.Type[] targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			throw new System.NotImplementedException();
		}
	}
}


