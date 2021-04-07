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
using Neo.IronLua;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;

namespace TecWare.PPSn.UI
{
	#region -- class PpsConverter -----------------------------------------------------

	/// <summary></summary>
	public static class PpsConverter
	{
		/// <summary>Dummy converter for debug proposes.</summary>
		public static IValueConverter Dummy => DummyConverter.Default;
		/// <summary>Wpf-Value Converter.</summary>
		public static IValueConverter NumericValue => NumericValueConverter.Default;
		/// <summary>Wpf-Value Converter.</summary>
		public static IValueConverter DateValue => DateValueConverter.Default;
		/// <summary>Convert between Visibility and bool.</summary>
		public static IValueConverter Visibility => VisibilityConverter.Default;
		/// <summary>Convert decimal values to an visibility.</summary>
		public static IValueConverter VisibilityMark => VisibilityMarkConverter.Default;
		/// <summary>Convert between Visibility and bool.</summary>
		public static VisibilityConverterParameter VisibilityCollapsedParameter { get; } = new VisibilityConverterParameter() { FalseValue = System.Windows.Visibility.Collapsed };
		/// <summary>Convert between Visibility and bool.</summary>
		public static VisibilityConverterParameter VisibilityNotCollapsedParameter { get; } = new VisibilityConverterParameter() { TrueValue = System.Windows.Visibility.Collapsed, FalseValue = System.Windows.Visibility.Visible };
		/// <summary>Convert between Visibility and bool.</summary>
		public static VisibilityConverterParameter VisibilityOnNullParameter { get; } = new VisibilityConverterParameter() { HasValue = true, TrueValue = System.Windows.Visibility.Hidden, FalseValue = System.Windows.Visibility.Visible };
		/// <summary>Convert between Visibility and bool.</summary>
		public static VisibilityConverterParameter VisibilityNotNullParameter { get; } = new VisibilityConverterParameter() { HasValue = true, TrueValue = System.Windows.Visibility.Visible, FalseValue = System.Windows.Visibility.Hidden };
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
		/// <summary>Compare values.</summary>
		public static IMultiValueConverter Equality => EqualConverter.Default;
		/// <summary>Concats Name,Vorname.</summary>
		public static IMultiValueConverter Name => NameConverter.Default;
		/// <summary>Replaces a color with the givven resource.</summary>
		public static IValueConverter DefaultColor => DefaultColorConverter.Default;
		/// <summary>Replaces a brush with the givven resource.</summary>
		public static IValueConverter DefaultBrush => DefaultBrushConverter.Default;
	} // class PpsConverter

	#endregion

	#region -- class DummyConverter ---------------------------------------------------

	internal sealed class DummyConverter : IValueConverter
	{
		private DummyConverter()
		{
		} // ctor

		private static object NoConvert(object value, Type targetType)
		{
			if (value is null)
				return null;
			else if (value.GetType() == targetType)
				return value;
			else
				return DependencyProperty.UnsetValue;
		} // func NoConvert

		object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)
			=> NoConvert(value, targetType);

		object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
			=> NoConvert(value, targetType);

		public static DummyConverter Default { get; } = new DummyConverter();
	} // class DummyConverter

	#endregion

	#region -- class LuaValueConverter ------------------------------------------------

	///// <summary>Generic value converter</summary>
	//[ContentProperty("ConvertExpression")]
	//public class LuaValueConverter : IValueConverter, IMultiValueConverter
	//{
	//	private delegate object ConvertDelegate(object value, object targetType, object parameter, _PpsShell shell, CultureInfo culture);
	//	private static readonly Lua lua = new Lua(); // lua engine for the value converters

	//	private string convert;
	//	private ConvertDelegate convertDelegate;
	//	private string convertBack;
	//	private ConvertDelegate convertBackDelegate;
	//	private Lazy<_PpsShell> getShell = null;

	//	private object ConvertIntern(string script, ref ConvertDelegate dlg, object value, object targetType, object parameter, CultureInfo culture)
	//	{
	//		if (String.IsNullOrEmpty(script))
	//			throw new NotImplementedException();

	//		if (dlg == null) // compile function
	//		{
	//			var localLua = getShell != null ? getShell.Value.Lua : lua;
	//			dlg = localLua.CreateLambda<ConvertDelegate>("convert.lua", script);
	//		}

	//		return dlg.DynamicInvoke(value, targetType, parameter, getShell?.Value, culture);
	//	} // func ConvertIntern

	//	object IMultiValueConverter.Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
	//		=> ConvertIntern(convert, ref convertDelegate, values, targetType, parameter, culture);

	//	object[] IMultiValueConverter.ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
	//		=> new LuaResult(ConvertIntern(convertBack, ref convertBackDelegate, value, targetTypes, parameter, culture)).Values;

	//	object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)
	//		=> ConvertIntern(convert, ref convertDelegate, value, targetType, parameter, culture);

	//	object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	//	{
	//		try
	//		{
	//			return ConvertIntern(convertBack, ref convertBackDelegate, value, targetType, parameter, culture);
	//		}
	//		catch (Exception e)
	//		{
	//			return new ValidationResult(false, e);
	//		}
	//	} // func IValueConverter.Convert

	//	/// <summary>Convert implementation, the arguments a equal to the IValueConverter-interface.</summary>
	//	public string ConvertExpression
	//	{
	//		get { return convert; }
	//		set
	//		{
	//			if (convert != value)
	//			{
	//				convert = value;
	//				convertDelegate = null;
	//			}
	//		}
	//	} // prop ConvertExpression

	//	/// <summary>ConvertBack implementation, the arguments a equal to the IValueConverter-interface.</summary>
	//	public string ConvertBackExpression
	//	{
	//		get => convertBack;
	//		set
	//		{
	//			if (convertBack != value)
	//			{
	//				convertBack = value;
	//				convertBackDelegate = null;
	//			}
	//		}
	//	} // prop ConvertBackExpression

	//	/// <summary>Does the converter needs an environment</summary>
	//	public bool UseEnvironment
	//	{
	//		get => getShell != null;
	//		set => getShell = new Lazy<_PpsShell>(_PpsShell.GetShell);
	//	} // prop UseEnvironment
	//} // class LuaValueConverter

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
	} // class NumericValueConverterParameter

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

			// get type info
			TypeCode targetTypeCode;
			bool isNullable;
			if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
			{
				isNullable = true;
				targetTypeCode = Type.GetTypeCode(targetType.GetGenericArguments()[0]);
			}
			else
			{
				isNullable = false;
				targetTypeCode = Type.GetTypeCode(targetType);
			}

			if (value == null)
				return GetNullValue(targetType, targetTypeCode, isNullable);
			else
			{
				if (value is string stringValue)
				{
					if (stringValue.Length == 0)
						return GetNullValue(targetType, targetTypeCode, isNullable);
					else
					{
						try
						{
							switch (targetTypeCode)
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
						catch(OverflowException e)
						{
							return new ValidationResult(false, e);
						}
						catch (FormatException e)
						{
							return new ValidationResult(false, e);
						}
					}
				}
				else
					return DependencyProperty.UnsetValue;
			}
		} // func ConvertBack

		private static object GetNullValue(Type targetType, TypeCode targetTypeCode, bool isNullable)
		{
			if (isNullable)
				return Activator.CreateInstance(targetType);
			else
			{
				switch (targetTypeCode)
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
		}

		public static IValueConverter Default { get; } = new NumericValueConverter();
	} // class NumericValueConverter

	#endregion

	#region -- class VisibilityConverter ----------------------------------------------

	/// <summary>Parameter for the Visibility Convert.</summary>
	public sealed class VisibilityConverterParameter
	{
		/// <summary>Check only is there value unequal <c>null</c>.</summary>
		public bool HasValue { get; set; } = false;
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

		private static bool GetBoolValue(object value, bool hasValue)
		{
			switch (value)
			{
				case bool b:
					return b;
				case string s when !hasValue:
					return String.Compare(s, Boolean.TrueString, StringComparison.OrdinalIgnoreCase) == 0;
				default:
					return value != null;
			}
		} // func GetBoolValue

		object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			var p = GetParameter(parameter);
			return GetBoolValue(value, p.HasValue) ? p.TrueValue : p.FalseValue;
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

	#region -- class VisibilityMarkConverter ------------------------------------------

	/// <summary>Parameter for the Visibility Convert.</summary>
	public sealed class VisibilityMarkConverterParameter
	{
		/// <summary>Watermark for visibility.</summary>
		public double WaterMark { get; set; } = 0.5;
		/// <summary>Convert value for lower the mark.</summary>
		public Visibility LowerValue { get; set; } = Visibility.Visible;
		/// <summary>Convert value for greater the mark.</summary>
		public Visibility GreaterValue { get; set; } = Visibility.Hidden;

		/// <summary>Singelton for the default Parameter.</summary>
		public static VisibilityMarkConverterParameter Default { get; } = new VisibilityMarkConverterParameter();
	} // class VisibilityMarkConverterParameter

	internal sealed class VisibilityMarkConverter : IValueConverter
	{
		private VisibilityMarkConverter()
		{
		} // ctor

		private static VisibilityMarkConverterParameter GetParameter(object parameter)
			=> parameter is VisibilityMarkConverterParameter p ? p : VisibilityMarkConverterParameter.Default;

		private static double GetDoubleValue(object value)
		{
			switch (value)
			{
				case double d:
					return d;
				case float f:
					return f;

				default:
					throw new FormatException();
			}
		} // func GetBoolValue

		object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			var p = GetParameter(parameter);
			return GetDoubleValue(value) < p.WaterMark ? p.LowerValue : p.GreaterValue;
		} // func Convert

		object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
			=> throw new NotSupportedException();

		public static IValueConverter Default { get; } = new VisibilityMarkConverter();
	} // class VisibilityMarkConverter

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

	#region -- class MultiplyConverter ------------------------------------------------

	internal sealed class EqualConverter : IMultiValueConverter
	{
		private EqualConverter()
		{
		}

		object IMultiValueConverter.Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
		{
			try
			{
				if (targetType != typeof(bool) || values.Length == 0)
					return DependencyProperty.UnsetValue;

				var f = values[0];
				for (var i = 1; i < values.Length; i++)
				{
					if (Equals(f, values[i]))
						return false;
				}

				return true;
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

		object[] IMultiValueConverter.ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
			=> throw new NotSupportedException();

		public static EqualConverter Default { get; } = new EqualConverter();
	} // class MultiplyConverter

	#endregion

	#region -- class NameConverter ----------------------------------------------------

	internal sealed class NameConverter : IMultiValueConverter
	{
		private NameConverter()
		{
		} // ctor

		string GetString(object v)
			=> v is string s ? s : null;

		object IMultiValueConverter.Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
		{
			if (values != null && values.Length > 0)
			{
				if (values.Length == 1)
					return GetString(values[0]);
				else
				{
					var name = GetString(values[0]);
					var vorname = GetString(values[1]);

					if (String.IsNullOrEmpty(name) && String.IsNullOrEmpty(vorname))
						return "<kein Name>";
					else if (String.IsNullOrEmpty(name))
						return vorname;
					else if (String.IsNullOrEmpty(vorname))
						return name;
					else
						return String.Format("{0}, {1}", name, vorname);
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

	#region -- class DateValueConverter -----------------------------------------------

	/// <summary>Parameter for the DateValueConverter</summary>
	public sealed class DateValueConverterParameter
	{
		/// <summary>Default parameter</summary>
		public static DateValueConverterParameter Default { get; } = new DateValueConverterParameter();
	} // class DateValueConverterParameter

	internal sealed class DateValueConverter : IValueConverter
	{
		private DateValueConverter()
		{
		} // ctor

		private static DateValueConverterParameter GetParameter(object parameter)
			=> parameter is DateValueConverterParameter r ? r : DateValueConverterParameter.Default;

		object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (targetType == typeof(string))
			{
				if (value == null) // null is null
					return null;
				else if (value is DateTime dt)
					return dt.ToString("dd.MM.yyyy"); // enforce german format, because the textbox does only support this format
				else if (value is DateTimeOffset dto)
					return dto.ToString("dd.MM.yyyy");
				else
					return DependencyProperty.UnsetValue;
			}
			else
				return DependencyProperty.UnsetValue;
		} // func Convert

		object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			var isNullable = targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>);
			if (isNullable)
				targetType = targetType.GetGenericArguments()[0];

			try
			{
				var str = (string)value;
				if (targetType == typeof(DateTime))
				{
					if (String.IsNullOrEmpty(str))
					{
						if (isNullable)
							return new DateTime?();
						else
							return new ValidationResult(false, "Null is not allowed");
					}
					else
						return DateTime.Parse(str);
				}
				else if (targetType == typeof(DateTimeOffset))
				{
					if (String.IsNullOrEmpty(str))
					{
						if (isNullable)
							return new DateTimeOffset?();
						else
							return new ValidationResult(false, "Null is not allowed");
					}
					else
						return DateTime.Parse(str);
				}
				else
					return DependencyProperty.UnsetValue;
			}
			catch (FormatException e)
			{
				return new ValidationResult(false, e);
			}
		} // func ConvertBack
		
		public static IValueConverter Default { get; } = new DateValueConverter();
	} // class DateValueConverter

	#endregion

	#region -- class PreviewImageConverter --------------------------------------------

	internal sealed class PreviewImageConverter : IValueConverter
	{
		object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is IPpsDataInfo data && targetType == typeof(ImageSource))
			{
				return null;
			}
			else
				return DependencyProperty.UnsetValue;
		} // func IValueConverter.Convert

		object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
			=> throw new NotSupportedException();

		public static PreviewImageConverter Default { get; } = new PreviewImageConverter();
	} // class PreviewImageConverter

	#endregion

	#region -- class DefaultColorConverter --------------------------------------------

	internal class DefaultColorConverter : IValueConverter
	{
		object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is Color color && color == Colors.Transparent)
				value = Application.Current.TryFindResource(parameter) ?? color;

			return value;
		} // func IValueConverter.Convert

		object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
			=> throw new NotSupportedException();

		public static IValueConverter Default { get; } = new DefaultBrushConverter();
	} //class DefaultColorConverter

	#endregion

	#region -- class DefaultBrushConverter --------------------------------------------

	internal class DefaultBrushConverter : IValueConverter
	{
		object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if(value is SolidColorBrush brush && brush.Color == Colors.Transparent)
				value = Application.Current.TryFindResource(parameter) ?? brush;

			return value;
		} // func IValueConverter.Convert

		object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
			=> throw new NotSupportedException();

		public static IValueConverter Default { get; } = new DefaultBrushConverter();
	} //class DefaultBrushConverter

	#endregion
}
