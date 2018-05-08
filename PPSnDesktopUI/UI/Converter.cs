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
using Neo.IronLua;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
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
		/// <summary>Convert between Visibility and bool.</summary>
		public static VisibilityConverterParameter VisibilityCollapsedParameter { get; } = new VisibilityConverterParameter() { FalseValue = System.Windows.Visibility.Collapsed };
		/// <summary>Convert between Visibility and bool.</summary>
		public static VisibilityConverterParameter VisibilityOnNullParameter { get; } = new VisibilityConverterParameter() { TrueValue = System.Windows.Visibility.Hidden, FalseValue = System.Windows.Visibility.Visible };
		/// <summary>Convert between Visibility and bool.</summary>
		public static VisibilityConverterParameter VisibilityNotNullParameter { get; } = new VisibilityConverterParameter() { TrueValue = System.Windows.Visibility.Visible, FalseValue = System.Windows.Visibility.Hidden };
		/// <summary>Removes all new lines.</summary>
		public static IValueConverter MultiToSingleLine => MultiToSingleLineConverter.Default;
		/// <summary>Creates a array of objects.</summary>
		public static IMultiValueConverter MultiValueToArray => MultiValueToArrayConverter.Default;
		/// <summary>Converts a type to an string via LuaType.</summary>
		public static IValueConverter LuaTypeString => LuaTypeStringConverter.Default;
		/// <summary>Converts a string to a PathGeometry.</summary>
		public static IValueConverter ImageToPathGeometry => ImageToPathGeometryConverter.Default;
		/// <summary></summary>
		public static IValueConverter TakeListItems => TakeListItemsConverter.Default;
		/// <summary>Multiplies a value with the parameter.</summary>
		public static IValueConverter Multiply => MultiplyConverter.Default;
		/// <summary>Concats Name,Vorname.</summary>
		public static IMultiValueConverter Name => NameConverter.Default;
	} // class PpsConverter

	#endregion

	#region -- class LuaValueConverter ------------------------------------------------

	/// <summary>Generic value converter</summary>
	[ContentProperty("ConvertExpression")]
	public class LuaValueConverter : IValueConverter, IMultiValueConverter
	{
		private delegate object ConvertDelegate(object value, object targetType, object parameter, PpsEnvironment environment, CultureInfo culture);
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
			{
				var localLua = getEnvironment != null ? getEnvironment.Value.Lua : lua;
				dlg = localLua.CreateLambda<ConvertDelegate>("convert.lua", script);
			}

			return dlg.DynamicInvoke(value, targetType, parameter, getEnvironment?.Value, culture);
		} // func ConvertIntern

		object IMultiValueConverter.Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
			=> ConvertIntern(convert, ref convertDelegate, values, targetType, parameter, culture);

		object[] IMultiValueConverter.ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
			=> new LuaResult(ConvertIntern(convertBack, ref convertBackDelegate, value, targetTypes, parameter, culture)).Values;

		object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)
			=> ConvertIntern(convert, ref convertDelegate, value, targetType, parameter, culture);

		object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			try
			{
				return ConvertIntern(convertBack, ref convertBackDelegate, value, targetType, parameter, culture);
			}
			catch (Exception e)
			{
				return new ValidationResult(false, e);
			}
		} // func IValueConverter.Convert

		/// <summary>Convert implementation, the arguments a equal to the IValueConverter-interface.</summary>
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

		/// <summary>ConvertBack implementation, the arguments a equal to the IValueConverter-interface.</summary>
		public string ConvertBackExpression
		{
			get => convertBack;
			set
			{
				if (convertBack != value)
				{
					convertBack = value;
					convertBackDelegate = null;
				}
			}
		} // prop ConvertBackExpression

		/// <summary>Does the converter needs an environment</summary>
		public bool UseEnvironment
		{
			get => getEnvironment != null;
			set => getEnvironment = new Lazy<PpsEnvironment>(PpsEnvironment.GetEnvironment);
		} // prop UseEnvironment
	} // class LuaValueConverter

	#endregion

	#region -- class NumericValueConverter --------------------------------------------

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
							return DependencyProperty.UnsetValue;
					}
				}
			}
			else
				return DependencyProperty.UnsetValue;
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
						return DependencyProperty.UnsetValue;
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
							return DependencyProperty.UnsetValue;
					}
				}
				else
					return DependencyProperty.UnsetValue;
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
					return DependencyProperty.UnsetValue;
			}
		} // func ConvertBack

		public static IValueConverter Default { get; } = new VisibilityConverter();
	} // class VisibilityConverter

	#endregion

	#region -- class MultiToSingleLineConverter  --------------------------------------

	/// <summary>Parameter for multi to single line converter.</summary>
	public sealed class MultiToSingleLineConverterParameter
	{
		/// <summary>Maximal length of the resulting string.</summary>
		public int MaxStringLength { get; set; } = Int32.MaxValue;
		/// <summary>Maximal number of lines to scan.</summary>
		public int MaxLines { get; set; } = Int32.MaxValue;
		/// <summary><c>true</c>: Is the resulting string longer the MaxStringLength, ... is added.</summary>
		public bool EndEllipse { get; set; } = false;

		/// <summary></summary>
		public static MultiToSingleLineConverterParameter Default { get; } = new MultiToSingleLineConverterParameter();
	} // class MultiToSingleLineConverterParameter

	internal sealed class MultiToSingleLineConverter : IValueConverter
	{
		private MultiToSingleLineConverter()
		{
		} // ctor

		private static MultiToSingleLineConverterParameter GetParameter(object parameter)
			=> parameter is MultiToSingleLineConverterParameter p ? p : MultiToSingleLineConverterParameter.Default;

		private static IEnumerable<string> GetLines(string value, MultiToSingleLineConverterParameter p)
		{
			var lineNo = 0;
			var charCount = 0;
			foreach (var (startAt, len) in value.SplitNewLinesTokens())
			{
				var r = value.Substring(startAt, len).Trim();
				if (r.Length == 0)
					continue; // to not count empty lines

				yield return r;

				// char cancel rule
				charCount += r.Length;
				if (charCount > p.MaxStringLength)
					break;

				// line cancel rule
				lineNo++;
				if (lineNo >= p.MaxLines)
					break;
			}
		} // func GetLines

		object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value == null)
				return null;
			else if (value is string s)
			{
				var p = GetParameter(parameter);
				var r = String.Join(" ", GetLines(s, p));
				if (p.EndEllipse && r.Length > p.MaxStringLength && p.MaxStringLength > 3)
					r = r.Substring(0, p.MaxStringLength - 3) + "...";
				return r;
			}
			else
				return DependencyProperty.UnsetValue;
		} // func IValueConverter.Convert

		object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
			=> DependencyProperty.UnsetValue;

		public static MultiToSingleLineConverter Default { get; } = new MultiToSingleLineConverter();
	} // class MultiToSingleLineConverter

	#endregion

	#region -- class MultiValueToArrayConverter ---------------------------------------

	internal sealed class MultiValueToArrayConverter : IMultiValueConverter
	{
		private MultiValueToArrayConverter()
		{
		} // ctor

		object IMultiValueConverter.Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
			=> values?.Clone();

		object[] IMultiValueConverter.ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
			=> value is object[] arr ? (object[])arr.Clone() : new object[] { DependencyProperty.UnsetValue };

		public static MultiValueToArrayConverter Default { get; } = new MultiValueToArrayConverter();
	} // class MultiValueToArrayConverter

	#endregion

	#region -- class LuaTypeStringConverter -------------------------------------------

	internal sealed class LuaTypeStringConverter : IValueConverter
	{
		private LuaTypeStringConverter()
		{
		} // ctor

		object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			switch (value)
			{
				case Type t:
					return LuaType.GetType(t).AliasOrFullName;
				case LuaType lt:
					return lt.AliasOrFullName;
				case null:
					return null;
				default:
					return DependencyProperty.UnsetValue;
			}
		} // func IValueConverter.Convert

		object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			switch (value)
			{
				case null:
					return null;
				case string s:
					return LuaType.GetType(s);
				default:
					return DependencyProperty.UnsetValue;
			}
		} // func IValueConverter.ConvertBack

		public static LuaTypeStringConverter Default { get; } = new LuaTypeStringConverter();
	} // class LuaTypeStringConverter

	#endregion

	#region -- class ImageToPathGeometryConverter -------------------------------------

	internal sealed class ImageToPathGeometryConverter : IValueConverter
	{
		private ImageToPathGeometryConverter()
		{
		} // ctor

		object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			switch (value)
			{
				case null:
					return null;
				case string resName:
					return Application.Current.TryFindResource(resName + "PathGeometry") ?? Application.Current.TryFindResource(resName);
				default:
					return DependencyProperty.UnsetValue;
			}
		} // func Convert

		object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
			=> DependencyProperty.UnsetValue;

		public static ImageToPathGeometryConverter Default { get; } = new ImageToPathGeometryConverter();
	} // class ImageToPathGeometryConverter

	#endregion

	#region -- class TakeListItemsConverter -------------------------------------------

	/// <summary></summary>
	public sealed class TakeListItemsConverterParameter
	{
		/// <summary></summary>
		public int MaxItems { get; set; } = 10;
		/// <summary></summary>
		public bool LastItems { get; set; } = true;

		/// <summary></summary>
		public static TakeListItemsConverterParameter Default { get; } = new TakeListItemsConverterParameter();
	} // class TakeListItemsConverterParameter

	internal sealed class TakeListItemsConverter : IValueConverter
	{
		private TakeListItemsConverter()
		{
		} // ctor

		private static TakeListItemsConverterParameter GetParameter(object parameter)
			=> parameter is TakeListItemsConverterParameter p ? p : TakeListItemsConverterParameter.Default;

		object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			switch (value)
			{
				case null:
					return null;
				case IList l:
					var p = GetParameter(parameter);
					return new TakeList(l, p.MaxItems, p.LastItems);
				default:
					return DependencyProperty.UnsetValue;
			}
		} // funcIValueConverter.Convert

		object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
			=> value is TakeList l ? l.SourceList : DependencyProperty.UnsetValue;

		public static TakeListItemsConverter Default { get; } = new TakeListItemsConverter();
	} // class TakeListItemsConverter

	#endregion

	#region -- class MultiplyConverter ------------------------------------------------

	internal sealed class MultiplyConverter : IValueConverter
	{
		private MultiplyConverter()
		{
		}

		private static Thickness MultiplyThickness(object _value, object _parameter, bool div)
		{
			var value = _value.ChangeTypeWithConverter<Thickness>();
			var parameter = _parameter.ChangeTypeWithConverter<Thickness>();

			return div
				? new Thickness(value.Left / parameter.Left, value.Top / parameter.Top, value.Right / parameter.Right, value.Bottom / parameter.Bottom)
				: new Thickness(value.Left * parameter.Left, value.Top * parameter.Top, value.Right * parameter.Right, value.Bottom * parameter.Bottom);
		} // func MultiplyThickness


		object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			try
			{
				switch (Type.GetTypeCode(targetType))
				{
					case TypeCode.Single:
						return value.ChangeType<float>() * parameter.ChangeType<float>();
					case TypeCode.Double:
						return value.ChangeType<double>() * parameter.ChangeType<double>();
					case TypeCode.Decimal:
						return value.ChangeType<decimal>() * parameter.ChangeType<decimal>();

					case TypeCode.SByte:
						return value.ChangeType<sbyte>() * parameter.ChangeType<sbyte>();
					case TypeCode.Int16:
						return value.ChangeType<short>() * parameter.ChangeType<short>();
					case TypeCode.Int32:
						return value.ChangeType<int>() * parameter.ChangeType<int>();
					case TypeCode.Int64:
						return value.ChangeType<long>() * parameter.ChangeType<long>();

					case TypeCode.Byte:
						return value.ChangeType<byte>() * parameter.ChangeType<byte>();
					case TypeCode.UInt16:
						return value.ChangeType<ushort>() * parameter.ChangeType<ushort>();
					case TypeCode.UInt32:
						return value.ChangeType<uint>() * parameter.ChangeType<uint>();
					case TypeCode.UInt64:
						return value.ChangeType<ulong>() * parameter.ChangeType<ulong>();

					case TypeCode.Object:
						if (targetType == typeof(Thickness))
							return MultiplyThickness(value, parameter,  false);
						else
							goto default;
					default:
						return DependencyProperty.UnsetValue;
				}
			}
			catch (OverflowException)
			{
				return DependencyProperty.UnsetValue;
			}
			catch (InvalidCastException)
			{
				return DependencyProperty.UnsetValue;
			}
		} // func IValueConverter.Convert

		object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			try
			{
				switch (Type.GetTypeCode(targetType))
				{
					case TypeCode.Single:
						return value.ChangeType<float>() / parameter.ChangeType<float>();
					case TypeCode.Double:
						return value.ChangeType<double>() / parameter.ChangeType<double>();
					case TypeCode.Decimal:
						return value.ChangeType<decimal>() / parameter.ChangeType<decimal>();

					case TypeCode.SByte:
						return value.ChangeType<sbyte>() / parameter.ChangeType<sbyte>();
					case TypeCode.Int16:
						return value.ChangeType<short>() / parameter.ChangeType<short>();
					case TypeCode.Int32:
						return value.ChangeType<int>() / parameter.ChangeType<int>();
					case TypeCode.Int64:
						return value.ChangeType<long>() * parameter.ChangeType<long>();

					case TypeCode.Byte:
						return value.ChangeType<byte>() / parameter.ChangeType<byte>();
					case TypeCode.UInt16:
						return value.ChangeType<ushort>() / parameter.ChangeType<ushort>();
					case TypeCode.UInt32:
						return value.ChangeType<uint>() / parameter.ChangeType<uint>();
					case TypeCode.UInt64:
						return value.ChangeType<ulong>() / parameter.ChangeType<ulong>();

					case TypeCode.Object:
						if (targetType == typeof(Thickness))
							return MultiplyThickness(value, parameter, true);
						else
							goto default;

					default:
						return DependencyProperty.UnsetValue;
				}
			}
			catch (DivideByZeroException)
			{
				return DependencyProperty.UnsetValue;
			}
			catch (OverflowException)
			{
				return DependencyProperty.UnsetValue;
			}
			catch (InvalidCastException)
			{
				return DependencyProperty.UnsetValue;
			}
		} // func IValueConverter.Convert

		public static MultiplyConverter Default { get; } = new MultiplyConverter();
	} // class MultiplyConverter

	#endregion

	#region -- class NameConverter ----------------------------------------------------

	internal sealed class NameConverter : IMultiValueConverter
	{
		private NameConverter()
		{
		} // ctor

		object IMultiValueConverter.Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
		{
			if (values != null && values.Length > 0)
			{
				if (values.Length == 1)
					return (string)values[0];
				else
				{
					if (values[0] is null && values[1] is null)
						return "<kein Name>";
					else if (values[0] == null)
						return (string)values[0];
					else if (values[1] == null)
						return (string)values[1];
					else
						return String.Format("{0} {1}", values[0], values[1]);
				}
			}
			else
				return DependencyProperty.UnsetValue;
		} // func IMultiValueConverter.Convert

		object[] IMultiValueConverter.ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
			=> throw new NotSupportedException();

		public static NameConverter Default { get; } = new NameConverter();
	} // class LuaTypeStringConverter

	#endregion
}
