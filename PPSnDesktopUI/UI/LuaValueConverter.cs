using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Markup;
using Neo.IronLua;

namespace TecWare.PPSn.UI
{
	#region -- class LuaValueConverter --------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Generic value converter</summary>
	[ContentProperty("ConvertExpression")]
	public class LuaValueConverter : IValueConverter
	{
		private delegate object ConvertDelegate(object value, Type targetType, object parameter, CultureInfo culture);
		private static Lua lua = new Lua(); // lua engine for the value converters

		private string convert;
		private ConvertDelegate convertDelegate;
		private string convertBack;
		private ConvertDelegate convertBackDelegate;

		private object ConvertIntern(string script, ref ConvertDelegate dlg, object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (String.IsNullOrEmpty(script))
				throw new NotImplementedException();

			if (dlg == null) // compile function
				dlg = lua.CreateLambda<ConvertDelegate>("convert.lua", script);

			return dlg.DynamicInvoke(value, targetType, parameter, culture);
		} // func ConvertIntern

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return ConvertIntern(convert, ref convertDelegate, value, targetType, parameter, culture);
		} // func Convert

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return ConvertIntern(convertBack, ref convertBackDelegate, value, targetType, parameter, culture);
		} // func ConvertBack

		public string ConvertExpression
		{
			get { return convert; }
			set
			{
				if (convert != value)
				{
					convert = value;
					convertDelegate = null;
				}
			}
		} // prop ConvertExpression

		public string ConvertBackExpression
		{
			get { return convertBack; }
			set
			{
				if (convertBack != value)
				{
					convertBack = value;
					convertBackDelegate = null;
				}
			}
		} // prop ConvertBackExpression
	} // class LuaValueConverter

	#endregion
}
