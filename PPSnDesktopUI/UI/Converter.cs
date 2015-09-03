using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using Neo.IronLua;

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

}
