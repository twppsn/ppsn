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
using System.Windows.Data;
using System.Windows.Markup;
using Neo.IronLua;

namespace TecWare.PPSn.UI
{
	#region -- class LuaValueConverter --------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Generic value converter</summary>
	[ContentProperty("ConvertExpression")]
	public class LuaValueConverter : IValueConverter, IMultiValueConverter
	{
		private delegate object ConvertDelegate(object value, Type targetType, object parameter, PpsEnvironment environment, CultureInfo culture);
		private static Lua lua = new Lua(); // lua engine for the value converters

		private string convert;
		private ConvertDelegate convertDelegate;
		private string convertBack;
		private ConvertDelegate convertBackDelegate;
		private Lazy<PpsEnvironment> getEnvironment = null;

		private object ConvertIntern(string script, ref ConvertDelegate dlg, object value, object targetType, object parameter, CultureInfo culture)
		{
			if (String.IsNullOrEmpty(script))
				throw new NotImplementedException();

			if (dlg == null) // compile function
				dlg = lua.CreateLambda<ConvertDelegate>("convert.lua", script);

			return dlg.DynamicInvoke(value, targetType, parameter, getEnvironment?.Value, culture);
		} // func ConvertIntern

		object IMultiValueConverter.Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
			=> ConvertIntern(convert, ref convertDelegate, values, targetType, parameter, culture);

		object[] IMultiValueConverter.ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
			=> new LuaResult(ConvertIntern(convertBack, ref convertBackDelegate, value, targetTypes, parameter, culture)).Values;

		object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)
			=> ConvertIntern(convert, ref convertDelegate, value, targetType, parameter, culture);

		object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
			=> ConvertIntern(convertBack, ref convertBackDelegate, value, targetType, parameter, culture);
		
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

		public bool UseEnvironment
		{
			get { return getEnvironment != null; }
			set { getEnvironment = new Lazy<PpsEnvironment>(PpsEnvironment.GetEnvironment); }
		} // prop UseEnvironment
	} // class LuaValueConverter

	#endregion
}
