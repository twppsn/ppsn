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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TecWare.DE.Stuff;
using Excel = Microsoft.Office.Interop.Excel;

namespace PPSnExcel.Data
{
	/// <summary>.net to excel specific converter</summary>
	public static class XlConverter
	{
		private static readonly Regex numberFormat = new Regex(@"^(?<f>[CDEFNPX])(?<d>\d*)$", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

		private static bool TryMatchRegex(Regex regex, string input, out Match m)
		{
			m = numberFormat.Match(input);
			return m.Success;
		} // func TryMatchRegex

		public static void UpdateRange(Excel.Range range, Type baseCellType, IPropertyReadOnlyDictionary attributes, bool styleUpdate)
		{
			if (range == null)
				throw new ArgumentNullException(nameof(range));
			if (attributes == null)
				throw new ArgumentNullException(nameof(attributes));
			baseCellType = baseCellType ?? typeof(object);

			// set number format
			if (attributes.TryGetProperty("xl.format", out string tmp))
				range.NumberFormat = tmp;
			else if (attributes.TryGetProperty("format", out tmp))
				range.NumberFormat = ConvertNetToExcelFormat(baseCellType, tmp, CultureInfo.CurrentUICulture);

			if (!styleUpdate)
				return;

			// set alignment
			if (attributes.TryGetProperty("halign", out tmp))
				range.HorizontalAlignment = GetAlignment(true, tmp);
			if (attributes.TryGetProperty("valign", out tmp))
				range.HorizontalAlignment = GetAlignment(false, tmp);

			//range.ColumnWidth;
			//range.RowHeight;
			//range.ShrinkToFit;

			//range.WrapText;
			//range.Orientation;

			//range.AddIndent;
			//range.IndentLevel;
		} // func UpdateRange

		private static object GetAlignment(bool horizontal, string alignment)
		{
			switch(alignment)
			{
				case "right":
				case "bottom":
				case "far":
					return horizontal ? Excel.Constants.xlRight : Excel.Constants.xlBottom;
				case "left":
				case "top":
				case "near":
					return horizontal ? Excel.Constants.xlRight : Excel.Constants.xlTop;
				case "center":
					return Excel.Constants.xlCenter;
				case "justify":
					return Excel.Constants.xlJustify;
				case "fill":
					return horizontal ? Excel.Constants.xlFill : Excel.Constants.xlGeneral;
				case "distributed":
					return Excel.Constants.xlDistributed;
				default:
					return Excel.Constants.xlGeneral;
			}
		} // func GetAlignment

		private static string GenerateExcelMask(bool seperator, int comma)
			=> comma <= 0
				? (seperator ? "#,##0" : "0")
				: (seperator ? "#,##0." : "0.") + new String('0', comma);

		private static string GenerateExcelSymbolMask(string sym, bool seperator, int comma, int pattern)
		{
			var m = GenerateExcelMask(seperator, comma);
			switch (pattern)
			{
				case 1:
					return m + sym;
				case 2:
					return sym + " " + m;
				case 3:
					return m + " " + sym;
				default:
					return sym + m;
			}
		} // func 

		private static string GenerateExcelSymbolMask(string sym, bool seperator, int comma, int pattern, string negSign)
		{
			const string b1 = "\\(";
			const string b2 = "\\)";
			const string space = " ";

			var m = GenerateExcelMask(seperator, comma);
			switch (pattern)
			{
				case 1: // -$n
					return negSign + sym + m;
				case 2: //  $-n
					return sym + negSign + m;
				case 3: // $n-
					return sym + m + negSign;
				case 4: // (n$)
					return b1 + m + sym + b2;
				case 5: // -n$
					return negSign + m + sym;
				case 6: // n-$
					return m + negSign + sym;
				case 7: // n$-
					return m + sym + negSign;
				case 8:
					return negSign + m + space + sym;
				case 9: // -$ n
					return negSign + sym + space + m;
				case 10: //  n $-
					return m + space + sym + negSign;
				case 11: //  $ n-
					return sym + space + m + negSign;
				case 12: //  $ -n
					return sym + space + negSign + m;
				case 13: //  n- $
					return m + negSign + space + sym;
				case 14: // ($ n)
					return b1 + sym + space + m + b2;
				case 15: // (n $)
					return b1 + m + space + sym + b2;
				default: // ($n)
					return b1 + sym + m + b2;
			}
		} // func GenerateExcelSymbolMask

		public static object ConvertNetToExcelFormat(Type baseType, string format, CultureInfo cultureInfo)
		{
			if (String.IsNullOrEmpty(format))
				return baseType == typeof(string) ? "Text" : "General";

			if (TryMatchRegex(numberFormat, format, out var m))
			{
				var f = m.Groups["f"].Value;
				var ns = m.Groups["d"].Value;
				var n = String.IsNullOrEmpty(ns) ? -1 : Int32.Parse(ns);

				switch (Char.ToUpper(f[0]))
				{
					case 'C': // currency (number are comma digits)
						{
							if (n == -1)
								n = cultureInfo.NumberFormat.CurrencyDecimalDigits;
							var sym = cultureInfo.NumberFormat.CurrencySymbol;

							return GenerateExcelSymbolMask(sym, true, n, cultureInfo.NumberFormat.CurrencyPositivePattern) + ";" +
								GenerateExcelSymbolMask(sym, true, n, cultureInfo.NumberFormat.CurrencyNegativePattern, cultureInfo.NumberFormat.NegativeSign) + ";";
						}
					case 'P': // percent (number are comma digits)
						{
							if (n == -1)
								n = cultureInfo.NumberFormat.PercentDecimalDigits;
							var sym = cultureInfo.NumberFormat.PercentSymbol;
							
							return GenerateExcelSymbolMask(sym, true, n, cultureInfo.NumberFormat.PercentPositivePattern) + ";" +
								GenerateExcelSymbolMask(sym, true, n, cultureInfo.NumberFormat.PercentNegativePattern, cultureInfo.NumberFormat.NegativeSign) + ";"+
								GenerateExcelSymbolMask(sym, false, n, cultureInfo.NumberFormat.PercentPositivePattern);
						}
					case 'D': // decimal (number are digits)
						return n <= 0
							? "0"
							: new string('0', n);
					case 'F': // fix comma
						if (n == -1)
							n = cultureInfo.NumberFormat.NumberDecimalDigits;

						return GenerateExcelMask(false, n);
					case 'N': // fix comma
						if (n == -1)
							n = cultureInfo.NumberFormat.NumberDecimalDigits;

						return GenerateExcelMask(true, n);

					//case 'E' // exponential (number are comma digits)
					//case 'X' // exponential (number are digits)

					default:
						return "General";
				}
			}
			else
				return "General";
		} // func ConvertNetToExcelFormat

		public static Excel.XlTotalsCalculation ConvertToTotalsCalculation(string value)
		{
			switch(value)
			{
				case "sum":
					return Excel.XlTotalsCalculation.xlTotalsCalculationSum;
				case "avg":
					return Excel.XlTotalsCalculation.xlTotalsCalculationAverage;
				case "count":
					return Excel.XlTotalsCalculation.xlTotalsCalculationCount;
				case "countnums":
					return Excel.XlTotalsCalculation.xlTotalsCalculationCountNums;
				case "min":
					return Excel.XlTotalsCalculation.xlTotalsCalculationMin;
				case "max":
					return Excel.XlTotalsCalculation.xlTotalsCalculationMax;
				case "stddev":
					return Excel.XlTotalsCalculation.xlTotalsCalculationStdDev;
				case "var":
					return Excel.XlTotalsCalculation.xlTotalsCalculationVar;
				case "custom":
					return Excel.XlTotalsCalculation.xlTotalsCalculationCustom;
				default:
					return Excel.XlTotalsCalculation.xlTotalsCalculationNone;
			}
		} // func ConvertToTotalsCalculation
	} // class XlConverter
}
