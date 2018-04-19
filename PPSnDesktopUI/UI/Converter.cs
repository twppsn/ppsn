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
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Neo.IronLua;
using TecWare.DE.Stuff;

namespace TecWare.PPSn.UI
{
	#region -- class PpsConverter -----------------------------------------------------

	/// <summary></summary>
	public static class PpsConverter
	{
		/// <summary>Wpf-Value Converter.</summary>
		public static IValueConverter NumericValue => NumericValueConverter.Default;
		/// <summary>Convert between Visibility and bool.</summary>
		public static IValueConverter Visibility => VisibilityConverter.Default;
	} // class PpsConverter

	#endregion

	#region -- NumericValueConverter --------------------------------------------------

	/// <summary>Parameter for the FloatValueConverter</summary>
	public sealed class NumericValueConverterParameter
	{
		/// <summary>Allow negative numbers.</summary>
		public bool AllowNeg { get; set; } = true;
		/// <summary>Digits after the comma.</summary>
		public int FloatDigits { get; set; } = 2;

		/// <summary>Default parameter</summary>
		public static NumericValueConverterParameter Default { get; } = new NumericValueConverterParameter();
	} // class FloatValueConverterParameter

	internal sealed class NumericValueConverter : IValueConverter
	{
		private NumericValueConverter()
		{
		} // ctor

		private static NumericValueConverterParameter GetParameter(object parameter)
			=> parameter is NumericValueConverterParameter r ? r : NumericValueConverterParameter.Default;

		private static string GetFormatString(object parameter, bool forInteger)
		{
			var p = GetParameter(parameter);
			return forInteger ? "N0" : "N" + p.FloatDigits.ToString();
		} // func GetFormatString

		object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (targetType == typeof(string))
			{
				if (value == null) // null is null
					return null;
				else
				{
					switch (Type.GetTypeCode(value.GetType()))
					{
						case TypeCode.String:
							return value;
						case TypeCode.Single:
							return ((float)value).ToString(GetFormatString(parameter, false), culture);
						case TypeCode.Double:
							return ((double)value).ToString(GetFormatString(parameter, false), culture);
						case TypeCode.Decimal:
							return ((decimal)value).ToString(GetFormatString(parameter, false), culture);

						case TypeCode.SByte:
							return ((sbyte)value).ToString(GetFormatString(parameter, true), culture);
						case TypeCode.Int16:
							return ((short)value).ToString(GetFormatString(parameter, true), culture);
						case TypeCode.Int32:
							return ((int)value).ToString(GetFormatString(parameter, true), culture);
						case TypeCode.Int64:
							return ((long)value).ToString(GetFormatString(parameter, true), culture);

						case TypeCode.Byte:
							return ((byte)value).ToString(GetFormatString(parameter, true), culture);
						case TypeCode.UInt16:
							return ((ushort)value).ToString(GetFormatString(parameter, true), culture);
						case TypeCode.UInt32:
							return ((uint)value).ToString(GetFormatString(parameter, true), culture);
						case TypeCode.UInt64:
							return ((ulong)value).ToString(GetFormatString(parameter, true), culture);

						default:
							throw new NotSupportedException();
					}
				}
			}
			else
				throw new NotSupportedException();				
		} // func Convert

		object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			var p = GetParameter(parameter);

			if (value == null)
			{
				switch (Type.GetTypeCode(targetType))
				{
					case TypeCode.Single:
						return 0.0f;
					case TypeCode.Double:
						return 0.0;
					case TypeCode.Decimal:
						return 0.0m;

					case TypeCode.SByte:
						return (sbyte)0;
					case TypeCode.Int16:
						return (short)0;
					case TypeCode.Int32:
						return (int)0;
					case TypeCode.Int64:
						return (long)0;

					case TypeCode.Byte:
						return (byte)0;
					case TypeCode.UInt16:
						return (ushort)0;
					case TypeCode.UInt32:
						return (uint)0;
					case TypeCode.UInt64:
						return (ulong)0;

					case TypeCode.String:
						return null;
					default:
						throw new NotSupportedException();
				}
			}
			else
			{
				if (value is string stringValue)
				{
					switch (Type.GetTypeCode(targetType))
					{
						case TypeCode.Single:
							return Single.Parse(stringValue, culture);
						case TypeCode.Double:
							return Double.Parse(stringValue, culture);
						case TypeCode.Decimal:
							return Decimal.Parse(stringValue, culture);

						case TypeCode.SByte:
							return SByte.Parse(stringValue, culture);
						case TypeCode.Int16:
							return Int16.Parse(stringValue, culture);
						case TypeCode.Int32:
							return Int32.Parse(stringValue, culture);
						case TypeCode.Int64:
							return Int64.Parse(stringValue, culture);

						case TypeCode.Byte:
							return SByte.Parse(stringValue, culture);
						case TypeCode.UInt16:
							return Int16.Parse(stringValue, culture);
						case TypeCode.UInt32:
							return Int32.Parse(stringValue, culture);
						case TypeCode.UInt64:
							return Int64.Parse(stringValue, culture);

						case TypeCode.String:
							return null;

						default:
							throw new NotSupportedException();
					}
				}
				else
					throw new NotSupportedException();
			}
		} // func ConvertBack

		public static IValueConverter Default { get; } = new NumericValueConverter();
	} // class FloatValueConverter

	#endregion

	#region -- class VisibilityConverter ----------------------------------------------

	/// <summary>Parameter for the Visibility Convert.</summary>
	public sealed class VisibilityConverterParameter
	{
		/// <summary>Convert value for <c>true</c>.</summary>
		public Visibility TrueValue { get; set; } = Visibility.Visible;
		/// <summary>Convert value for <c>false</c>.</summary>
		public Visibility FalseValue { get; set; } = Visibility.Hidden;

		/// <summary>Singelton for the default Parameter.</summary>
		public static VisibilityConverterParameter Default { get; } = new VisibilityConverterParameter();
	} // class VisibilityConverterParameter

	internal sealed class VisibilityConverter : IValueConverter
    {
		private VisibilityConverter()
		{
		} // ctor

		private static VisibilityConverterParameter GetParameter(object parameter)
			=> parameter is VisibilityConverterParameter p ? p : VisibilityConverterParameter.Default;

		private static bool GetBoolValue(object value)
		{
			switch (value)
			{
				case bool b:
					return b;
				case string s:
					return String.Compare(s, Boolean.TrueString, StringComparison.OrdinalIgnoreCase) == 0;
				default:
					return value != null;
			}
		} // func GetBoolValue

		object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			var p = GetParameter(parameter);
			return GetBoolValue(value) ? p.TrueValue : p.FalseValue;
		} // func Convert

		object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			var p = GetParameter(parameter);
			switch (value)
			{
				case Visibility v:
					return v == p.TrueValue;
				default:
					throw new NotSupportedException();
			}
		} // func ConvertBack
		
		public static IValueConverter Default { get; } = new VisibilityConverter();
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
            => value.ToString().Replace(Environment.NewLine, " ").Replace("\n", " ");

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
        private static IEnumerable<object> GetLast(IList list, int count)
        {
            var end = list.Count - count;
            for (var i = Math.Max(end, 0); i < list.Count; i++)
                yield return (object)list[i];
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

            if (value[0] is IList)
                return GetLast((IList)value[0], 10);

            return ret;
        }

        public object[] ConvertBack(object value, System.Type[] targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new System.NotImplementedException();
        }
    }

	public sealed class PpsTypeStringConverter : IValueConverter
	{
		public object Convert(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
			=> value is Type t ? LuaType.GetType(t).AliasName ?? t.Name : value?.ToString();

		public object ConvertBack(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			throw new System.NotImplementedException();
		}
	}

	#region -- class PpsImageResourceKeyConverter ---------------------------------------

	public sealed class PpsImageResourceKeyConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			if (value is string && !String.IsNullOrEmpty((string)value))
			{
				var resName = String.Concat(value, "PathGeometry");
				return Application.Current.TryFindResource(resName);
			}
			return null;
		} // func Convert

		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			throw new NotSupportedException();
		} // func ConvertBack

	} // class PpsImageResourceKeyConverter

	#endregion

}


