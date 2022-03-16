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
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Neo.IronLua;
using TecWare.DE.Data;
using TecWare.DE.Stuff;

namespace TecWare.PPSn.Data
{
	#region -- enum PpsDataFilterExpressionType ---------------------------------------

	/// <summary></summary>
	public enum PpsDataFilterExpressionType
	{
		/// <summary></summary>
		None,
		/// <summary>and(xxx) - childs are connected with an AND</summary>
		And,
		/// <summary>or(xxx) - childs are connected with an OR</summary>
		Or,
		/// <summary>nand(a) - invert result</summary>
		NAnd,
		/// <summary>nor(a) - invert result</summary>
		NOr,

		/// <summary>compare expression with his own, user friendly syntax</summary>
		Compare,

		/// <summary>The value contains a native expression.</summary>
		Native,

		/// <summary>Neutral</summary>
		True
	} // enum PpsDataFilterExpressionType

	#endregion

	#region -- enum PpsDataFilterCompareOperator --------------------------------------

	/// <summary>Compare operations for expressions</summary>
	[Flags]
	public enum PpsDataFilterCompareOperator
	{
		/// <summary>Starts with operator</summary>
		StartWith = 1,
		/// <summary>Ends with operator</summary>
		EndWith = 2,
		/// <summary>Contains with operator</summary>
		Contains = 3,
		/// <summary>Not starts with operator</summary>
		NotStartWith = 4,
		/// <summary>Not ends with operator</summary>
		NotEndWith = 8,
		/// <summary>Not contains with operator</summary>
		NotContains = 12,
		/// <summary>Equal with operator</summary>
		Equal = 16,
		/// <summary>Not equal with operator</summary>
		NotEqual = 32,
		/// <summary>Greator with operator</summary>
		Greater = 64,
		/// <summary>Greator or equal with operator</summary>
		GreaterOrEqual = 128,
		/// <summary>Lower with operator</summary>
		Lower = 256,
		/// <summary>Lower or equal with operator</summary>
		LowerOrEqual = 512
	} // enum PpsDataFilterCompareOperator

	#endregion

	#region -- enum PpsSqlLikeStringEscapeFlag ----------------------------------------

	/// <summary></summary>
	[Flags]
	public enum PpsSqlLikeStringEscapeFlag
	{
		/// <summary></summary>
		Leading = 1,
		/// <summary></summary>
		Trailing = 2,
		/// <summary></summary>
		Both = Leading | Trailing
	} // enum PpsSqlLikeStringEscapeFlag

	#endregion

	#region -- class PpsDataFilterExpression ------------------------------------------

	/// <summary></summary>
	[TypeConverter(typeof(PpsDataFilterExpressionConverter))]
	public abstract class PpsDataFilterExpression
	{
		/// <summary></summary>
		/// <param name="method"></param>
		protected PpsDataFilterExpression(PpsDataFilterExpressionType method)
		{
			Type = method;
		} // ctor

		/// <summary>Removes expression parts</summary>
		/// <param name="variables">Allow replace variables</param>
		/// <returns></returns>
		public virtual PpsDataFilterExpression Reduce(IPropertyReadOnlyDictionary variables = null)
			=> this;

		/// <summary></summary>
		/// <param name="sb"></param>
		/// <param name="formatProvider"></param>
		public abstract void ToString(StringBuilder sb, IFormatProvider formatProvider);

		/// <inherited />
		public sealed override string ToString()
			=> ToString(CultureInfo.CurrentUICulture);

		/// <summary></summary>
		/// <param name="formatProvider"></param>
		public string ToString(IFormatProvider formatProvider)
		{
			var sb = new StringBuilder();
			ToString(sb, formatProvider);
			return sb.ToString();
		} // func ToString

		/// <summary></summary>
		public PpsDataFilterExpressionType Type { get; }

		// -- Static --------------------------------------------------------------

		#region -- Parse --------------------------------------------------------------

		private static bool IsLetterOrDigit(char c)
			=> c == '_' || c == '.' || Char.IsLetterOrDigit(c);

		private static bool TestExpressionCharacter(string expression, int offset, char c)
			=> offset < expression.Length && expression[offset] == c;

		private static bool EatExpressionCharacter(string expression, ref int offset, char c)
		{
			if (TestExpressionCharacter(expression, offset, c))
			{
				offset++;
				return true;
			}
			else
				return false;
		} // func EatExpressionCharacter

		private static void SkipWhiteSpaces(string expression, ref int offset)
		{
			while (offset < expression.Length && Char.IsWhiteSpace(expression[offset]))
				offset++;
		} // func SkipWhiteSpaces

		private static void ParseIdentifier(string expression, ref int offset, int expressionLength)
		{
			while (offset < expressionLength && IsLetterOrDigit(expression[offset]))
				offset++;
		} // func ParseIdentifier

		private static string ParseEscapedText(string expression, char quote, ref int offset, int expressionLength)
		{
			var sb = new StringBuilder();
			var escape = false;
			offset++;
			while (offset < expressionLength)
			{
				var c = expression[offset];
				if (escape)
				{
					if (c == quote)
						sb.Append(c);
					else
					{
						offset++;
						return sb.ToString();
					}
				}
				else if (c == quote)
					escape = true;
				else
					sb.Append(c);

				offset++;
			}

			return sb.ToString();
		} // func ParseEscapedText

		private static bool IsIdentifier(string expression, int offset, int count)
		{
			if (!Char.IsLetter(expression[offset]) && expression[offset] != '_')
				return false;

			for (var i = 1; i < count; i++)
			{
				if (!IsLetterOrDigit(expression[i + offset]))
					return false;
			}

			return true;
		} // func IsIdentifier

		private static PpsDataFilterValue ParseSingleValue(string expression, ref int offset, int expressionLength, IFormatProvider formatProvider, PpsDataFilterParseOption options)
		{
			PpsDataFilterValue value;

			if (offset >= expressionLength || Char.IsWhiteSpace(expression[offset]))
			{
				value = PpsDataFilterNullValue.Default;
			}
			else if (expression[offset] == '"' || expression[offset] == '\'')
			{
				var text = ParseEscapedText(expression, expression[offset], ref offset, expressionLength);
				value = String.IsNullOrEmpty(text) ? PpsDataFilterNullValue.Default : new PpsDataFilterTextValue(text);
			}
			else if (expression[offset] == '#')
			{
				offset++;
				var startAt2 = offset;
				while (offset < expressionLength && (!Char.IsWhiteSpace(expression[offset]) && expression[offset] != '#'))
					offset++;

				if (TestExpressionCharacter(expression, offset, '#')) // date filter
				{
					offset++;
					value = PpsDataFilterDateTimeValue.ParseDateTime(expression, startAt2, offset - startAt2 - 1, formatProvider);
				}
				else if (startAt2 < offset) // text key filter
					value = new PpsDataFilterTextKeyValue(expression.Substring(startAt2, offset - startAt2));
				else // null
					value = PpsDataFilterNullValue.Default;
			}
			else if (expression[offset] == '$' && (options & PpsDataFilterParseOption.AllowVariables) != 0)
			{
				offset++;
				var startAt2 = offset;

				ParseIdentifier(expression, ref offset, expressionLength);

				if (offset == startAt2)
					value = PpsDataFilterNullValue.Default;
				else
					value = new PpsDataFilterVariableValue(expression.Substring(startAt2, offset - startAt2));
			}
			else
			{
				var startAt2 = offset;
				SkipWhiteSpaces(expression, ref offset);
				while (offset < expressionLength && !(Char.IsWhiteSpace(expression[offset]) || expression[offset] == ')' || expression[offset] == '\'' || expression[offset] == '"'))
					offset++;

				if (startAt2 < offset) // unknown block of text
				{
					if ((options & PpsDataFilterParseOption.AllowFields) != 0 && expression[startAt2] == ':') // parse a field
						value = new PpsDataFilterFieldValue(expression.Substring(startAt2 + 1, offset - startAt2 - 1));
					else
					{
						if ((options & (PpsDataFilterParseOption.FieldsFirst | PpsDataFilterParseOption.AllowFields)) == (PpsDataFilterParseOption.AllowFields | PpsDataFilterParseOption.FieldsFirst)
							&& IsIdentifier(expression, startAt2, offset - startAt2))
						{
							value = new PpsDataFilterFieldValue(expression.Substring(startAt2, offset - startAt2));
						}
						else
						{
							value = new PpsDataFilterTextValue(expression.Substring(startAt2, offset - startAt2));
						}
					}
				}
				else
					value = PpsDataFilterNullValue.Default;
			}

			return value;
		} // proc ParseSingleValue

		private static PpsDataFilterValue[] ParseArrayValues(string expression, ref int offset, int expressionLength, IFormatProvider formatProvider, PpsDataFilterParseOption options)
		{
			var values = new List<PpsDataFilterValue>();

			SkipWhiteSpaces(expression, ref offset);
			while (!EatExpressionCharacter(expression, ref offset, ')'))
			{
				values.Add(ParseSingleValue(expression, ref offset, expressionLength, formatProvider, options));
				SkipWhiteSpaces(expression, ref offset);
			}

			return values.ToArray();
		} // func ParseArrayValues

		/// <summary>Parse a value.</summary>
		/// <param name="expression"></param>
		/// <param name="offset"></param>
		/// <param name="count"></param>
		/// <param name="formatProvider"></param>
		/// <param name="options"></param>
		/// <returns></returns>
		public static PpsDataFilterValue ParseValue(string expression, int offset, int count, IFormatProvider formatProvider, PpsDataFilterParseOption options)
		{
			// " -> Text
			// ## -> DateTime
			// # -> Text that is classified as key
			// 1 -> int
			// 1.2 -> dec
			// [:]t.Field -> field
			// (1 3 4 5) -> array

			var length = offset + count;
			if (EatExpressionCharacter(expression, ref offset, '('))
				return new PpsDataFilterArrayValue(ParseArrayValues(expression, ref offset, length, formatProvider, options));
			else
				return ParseSingleValue(expression, ref offset, length, formatProvider, options);
		} // func ParseValue

		private static PpsDataFilterCompareOperator ParseCompareOperator(string expression, ref int offset)
		{
			var op = PpsDataFilterCompareOperator.Contains;

			if (offset < expression.Length)
			{
				switch (expression[offset])
				{
					case '<':
						offset++;
						op = EatExpressionCharacter(expression, ref offset, '=')
							? PpsDataFilterCompareOperator.LowerOrEqual
							: PpsDataFilterCompareOperator.Lower;
						break;

					case '>':
						offset++;
						op = EatExpressionCharacter(expression, ref offset, '=')
							? PpsDataFilterCompareOperator.GreaterOrEqual
							: PpsDataFilterCompareOperator.Greater;
						break;

					case ']':
						offset++;
						op = PpsDataFilterCompareOperator.EndWith;
						break;

					case '[':
						offset++;
						op = EatExpressionCharacter(expression, ref offset, ']')
							? PpsDataFilterCompareOperator.Contains
							: PpsDataFilterCompareOperator.StartWith;
						break;

					case '=':
						offset++;
						op = PpsDataFilterCompareOperator.Equal;
						break;

					case '!':
						offset++;
						if (EatExpressionCharacter(expression, ref offset, '='))
						{
							op = PpsDataFilterCompareOperator.NotEqual;
						}
						else if (EatExpressionCharacter(expression, ref offset, ']'))
						{
							op = PpsDataFilterCompareOperator.NotEndWith;
						}
						else if (EatExpressionCharacter(expression, ref offset, '['))
						{
							if (EatExpressionCharacter(expression, ref offset, ']'))
							{
								op = PpsDataFilterCompareOperator.NotContains;
							}
							else
							{
								op = PpsDataFilterCompareOperator.NotStartWith;
							}
						}
						break;
				}
			}

			return op;
		} // func ParseCompareOperator

		private static bool IsStartCompareOperation(string expression, int startAt, int offset, out string identifier)
		{
			if (offset > startAt && TestExpressionCharacter(expression, offset, ':'))
			{
				identifier = expression.Substring(startAt, offset - startAt);
				return true;
			}
			else
			{
				identifier = null;
				return false;
			}
		} // func IsStartCompareOperation

		private static bool IsStartLogicOperation(string expression, int startAt, int offset, out PpsDataFilterExpressionType type)
		{
			var count = offset - startAt;

			if (count <= 0 || !TestExpressionCharacter(expression, offset, '('))
				count = 0;

			switch (count)
			{
				case 2:
					if (String.Compare(expression, startAt, "OR", 0, count, StringComparison.OrdinalIgnoreCase) == 0)
					{
						type = PpsDataFilterExpressionType.Or;
						return true;
					}
					goto default;
				case 3:
					if (String.Compare(expression, startAt, "AND", 0, count, StringComparison.OrdinalIgnoreCase) == 0)
					{
						type = PpsDataFilterExpressionType.And;
						return true;
					}
					else if (String.Compare(expression, startAt, "NOR", 0, count, StringComparison.OrdinalIgnoreCase) == 0)
					{
						type = PpsDataFilterExpressionType.NOr;
						return true;
					}
					goto default;
				case 4:
					if (String.Compare(expression, startAt, "NAND", 0, count, StringComparison.OrdinalIgnoreCase) == 0)
					{
						type = PpsDataFilterExpressionType.NAnd;
						return true;
					}
					goto default;
				default:
					type = PpsDataFilterExpressionType.None;
					return false;
			}
		} // func IsStartLogicOperation

		private static bool IsStartNativeReference(string expression, int startAt, int offset, out string identifier)
		{
			if (offset > startAt && TestExpressionCharacter(expression, offset, ':'))
			{
				identifier = expression.Substring(startAt, offset - startAt);
				return true;
			}
			else
			{
				identifier = null;
				return false;
			}
		} // func IsStartNativeReference

		private static PpsDataFilterExpression ParseExpression(string expression, PpsDataFilterExpressionType inLogic, ref int offset, int expressionLength, IFormatProvider formatProvider, PpsDataFilterParseOption options)
		{
			/*  expr ::=
			 *		[ identifier ] ( ':' [ '<' | '>' | '[' | ']' | '[]' | '<=' | '>=' | '!' | '!=' | '![' | '!]' | '![]') [ '(' ] value [ ')' ]
			 *		[ 'and' | 'or' | 'nand' | 'nor' ] '(' expr { SP ... } [ ')' ]
			 *		':' native ':'
			 *		value
			 *	
			 *	base is always an AND concatenation
			 */
			if (expression == null)
				return PpsDataFilterTrueExpression.Default;

			var returnLogic = inLogic == PpsDataFilterExpressionType.None ? PpsDataFilterExpressionType.And : inLogic;
			var compareExpressions = new List<PpsDataFilterExpression>();
			while (offset < expressionLength)
			{
				SkipWhiteSpaces(expression, ref offset);

				if (TestExpressionCharacter(expression, offset, ')'))
				{
					offset++;
					if (inLogic != PpsDataFilterExpressionType.None)
						break;
				}

				var startAt = offset;

				// check for native reference
				var nativeRef = TestExpressionCharacter(expression, offset, ':');
				if (nativeRef)
					offset++;

				// check for an identifier
				ParseIdentifier(expression, ref offset, expressionLength);
				if (IsStartLogicOperation(expression, startAt, offset, out var newLogic))
				{
					offset++;
					var expr = ParseExpression(expression, newLogic, ref offset, expressionLength, formatProvider, options);

					// optimize: concat same sub expression
					if (expr.Type == returnLogic)
						compareExpressions.AddRange(((PpsDataFilterLogicExpression)expr).Arguments);
					else if (expr != PpsDataFilterTrueExpression.Default)
						compareExpressions.Add(expr);
				}
				else if (!nativeRef && IsStartCompareOperation(expression, startAt, offset, out var identifier)) // compare operation
				{
					offset++; // step over the colon

					// check for operator, nothing means contains
					if (offset < expressionLength && !Char.IsWhiteSpace(expression[offset]))
					{
						var op = ParseCompareOperator(expression, ref offset); // parse the operator
						if (EatExpressionCharacter(expression, ref offset, '(')) // parse array
						{
							var values = ParseArrayValues(expression, ref offset, expressionLength, formatProvider, options);
							switch (op)
							{
								case PpsDataFilterCompareOperator.Contains:
									if (values.Length > 0)
										compareExpressions.Add(new PpsDataFilterCompareExpression(identifier, PpsDataFilterCompareOperator.Contains, new PpsDataFilterArrayValue(values)));
									else
										compareExpressions.Add(False);
									break;
								case PpsDataFilterCompareOperator.NotContains:
									if (values.Length > 0)
										compareExpressions.Add(new PpsDataFilterCompareExpression(identifier, PpsDataFilterCompareOperator.NotContains, new PpsDataFilterArrayValue(values)));
									break;
							}
						}
						else // parse value
						{
							var value = ParseSingleValue(expression, ref offset, expressionLength, formatProvider, options);
							// create expression
							compareExpressions.Add(new PpsDataFilterCompareExpression(identifier, op, value));
						}
					}
					else // is nothing
						compareExpressions.Add(new PpsDataFilterCompareExpression(identifier, PpsDataFilterCompareOperator.Equal, PpsDataFilterNullValue.Default));
				}
				else if (nativeRef && IsStartNativeReference(expression, startAt, offset, out identifier)) // native reference
				{
					offset++;
					compareExpressions.Add(new PpsDataFilterNativeExpression(identifier));
				}
				else
				{
					offset = startAt; // nothing special try compare expression
					var value = ParseSingleValue(expression, ref offset, expressionLength, formatProvider, options);
					if (value != PpsDataFilterNullValue.Default)
						compareExpressions.Add(new PpsDataFilterCompareExpression(null, PpsDataFilterCompareOperator.Contains, value));
				}
			}

			// generate expression
			if (compareExpressions.Count == 0)
			{
				return inLogic == PpsDataFilterExpressionType.NAnd || inLogic == PpsDataFilterExpressionType.NOr
					? False
					: True;
			}
			else if (compareExpressions.Count == 1 && (inLogic != PpsDataFilterExpressionType.NAnd && inLogic != PpsDataFilterExpressionType.NOr))
				return compareExpressions[0];
			else
				return new PpsDataFilterLogicExpression(returnLogic, compareExpressions.ToArray());
		} // func ParseExpression

		/// <summary>Parse a filter expression</summary>
		/// <param name="filterExpression"></param>
		/// <param name="formatProvider"></param>
		/// <param name="options"></param>
		/// <returns></returns>
		public static PpsDataFilterExpression Parse(string filterExpression, IFormatProvider formatProvider = null, PpsDataFilterParseOption options = PpsDataFilterParseOption.AllowFields)
		{
			return String.IsNullOrEmpty(filterExpression)
				? PpsDataFilterTrueExpression.Default
				: Parse(filterExpression, 0, filterExpression.Length, formatProvider, options);
		} // func Parse

		/// <summary>Parse a filter expression</summary>
		/// <param name="filterExpression"></param>
		/// <param name="offset"></param>
		/// <param name="count"></param>
		/// <param name="formatProvider"></param>
		/// <param name="options"></param>
		/// <returns></returns>
		public static PpsDataFilterExpression Parse(string filterExpression, int offset, int count, IFormatProvider formatProvider = null, PpsDataFilterParseOption options = PpsDataFilterParseOption.AllowFields)
		{
			var length = offset + count;
			return ParseExpression(filterExpression, PpsDataFilterExpressionType.None, ref offset, length, formatProvider ?? CultureInfo.CurrentUICulture, options);
		} // func Parse

		/// <summary>Parse a expression.</summary>
		/// <param name="filterExpression"></param>
		/// <param name="formatProvider"></param>
		/// <param name="options"></param>
		/// <param name="throwException"></param>
		/// <returns></returns>
		public static PpsDataFilterExpression Parse(object filterExpression, IFormatProvider formatProvider = null, PpsDataFilterParseOption options = PpsDataFilterParseOption.AllowFields | PpsDataFilterParseOption.ReturnTrue, bool throwException = true)
		{
			switch (filterExpression)
			{
				case null:
					return (options & PpsDataFilterParseOption.ReturnTrue) != 0 ? True : null;
				case string stringExpr:
					return Parse(stringExpr, formatProvider, options);
				case LuaTable table:
					return FromTable(table, formatProvider, options);
				default:
					if (throwException)
						throw new ArgumentException("Could not parse filter expression.");
					else
						return null;
			}
		} // func Parse

		#endregion

		#region -- Combine/Compare ----------------------------------------------------

		private static PpsDataFilterValue GetValueExpressionFromString(string s)
		{
			return s.Length > 1 && s[0] == ':'
				? (PpsDataFilterValue)new PpsDataFilterFieldValue(s.Substring(1))
				: (PpsDataFilterValue)new PpsDataFilterTextValue(s);
		} // func GetValueExpressionFromString

		private static PpsDataFilterValue GetValueExpressionFromLong(long i)
			=> new PpsDataFilterIntegerValue(i);

		private static PpsDataFilterValue GetValueExpressionFromDecimal(decimal d)
			=> new PpsDataFilterDecimalValue(d);

		private static PpsDataFilterValue GetValueExpressionFromDateTime(DateTime dt)
			=> PpsDataFilterDateTimeValue.Create(dt);

		private static PpsDataFilterValue GetValueArrayExpression<T>(T[] values, Func<T, PpsDataFilterValue> creator)
		{
			var r = new PpsDataFilterValue[values.Length];

			for (var i = 0; i < values.Length; i++)
				r[i] = creator(values[i]);

			return new PpsDataFilterArrayValue(r);
		}  // func GetValueArrayExpression

		private static PpsDataFilterValue GetValueExpresion(PpsDataFilterValueType type, object value)
		{
			switch (type)
			{
				case PpsDataFilterValueType.Null:
					return PpsDataFilterNullValue.Default;
				case PpsDataFilterValueType.Field:
					return new PpsDataFilterFieldValue(value.ChangeType<string>());
				case PpsDataFilterValueType.Text:
					return new PpsDataFilterTextValue(value.ChangeType<string>());
				case PpsDataFilterValueType.Number:
					return new PpsDataFilterTextKeyValue(value.ChangeType<string>());
				case PpsDataFilterValueType.Integer:
					return GetValueExpressionFromLong(value.ChangeType<long>());
				case PpsDataFilterValueType.Decimal:
					return GetValueExpressionFromDecimal(value.ChangeType<decimal>());
				case PpsDataFilterValueType.Date:
					return GetValueExpressionFromDateTime(value.ChangeType<DateTime>());
				default:
					throw new ArgumentOutOfRangeException(nameof(type), type, "Out of range.");
			}
		} // func GetValueExpresion

		private static PpsDataFilterValue GetValueExpresion(object value)
		{
			PpsDataFilterValue GetValueExpressionFromInt(int i)
				=> GetValueExpressionFromLong(i);

			switch (value)
			{
				case null:
					return PpsDataFilterNullValue.Default;
				case PpsDataFilterValue raw:
					return raw;
				case string s:
					return GetValueExpressionFromString(s);
				case string[] sn:
					return GetValueArrayExpression(sn, GetValueExpressionFromString);
				case int i:
					return GetValueExpressionFromLong(i);
				case int[] @in:
					return GetValueArrayExpression(@in, GetValueExpressionFromInt);
				case long n:
					return GetValueExpressionFromLong(n);
				case long[] an:
					return GetValueArrayExpression(an, GetValueExpressionFromLong);
				case DateTime dt:
					return GetValueExpressionFromDateTime(dt);
				case object[] on:
					return GetValueArrayExpression(on, GetValueExpresion);
				default:
					return new PpsDataFilterTextValue(value.ChangeType<string>());
			}
		} // func GetValueExpresion

		/// <summary>Compine multiple expresions to one expression.</summary>
		/// <param name="expr"></param>
		/// <returns></returns>
		public static PpsDataFilterExpression Combine(params PpsDataFilterExpression[] expr)
			=> Combine(PpsDataFilterExpressionType.And, expr);

		/// <summary>Compine multiple expresions to one expression.</summary>
		/// <param name="logicMethod"></param>
		/// <param name="expr"></param>
		/// <returns></returns>
		public static PpsDataFilterExpression Combine(PpsDataFilterExpressionType logicMethod, params PpsDataFilterExpression[] expr)
			=> new PpsDataFilterLogicExpression(logicMethod, expr).Reduce();

		/// <summary></summary>
		/// <param name="operand"></param>
		/// <param name="op"></param>
		/// <param name="type"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public static PpsDataFilterExpression Compare(string operand, PpsDataFilterCompareOperator op, PpsDataFilterValueType type, object value)
			=> new PpsDataFilterCompareExpression(operand, op, GetValueExpresion(type, value));

		/// <summary></summary>
		/// <param name="operand"></param>
		/// <param name="op"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public static PpsDataFilterExpression Compare(string operand, PpsDataFilterCompareOperator op, object value)
			=> new PpsDataFilterCompareExpression(operand, op, GetValueExpresion(value));

		/// <summary>Create a in expression</summary>
		/// <param name="operand"></param>
		/// <param name="values"></param>
		/// <returns></returns>
		public static PpsDataFilterExpression CompareIn(string operand, params object[] values)
			=> new PpsDataFilterCompareExpression(operand, PpsDataFilterCompareOperator.Contains, GetValueExpresion(values));

		/// <summary>Create a not in expression</summary>
		/// <param name="operand"></param>
		/// <param name="values"></param>
		/// <returns></returns>
		public static PpsDataFilterExpression CompareNotIn(string operand, params object[] values)
			=> new PpsDataFilterCompareExpression(operand, PpsDataFilterCompareOperator.NotContains, GetValueExpresion(values));

		#endregion

		#region -- FromTable ----------------------------------------------------------

		private static PpsDataFilterExpressionType GetLogicExpression(object obj)
		{
			switch (obj)
			{
				case null:
					return PpsDataFilterExpressionType.And;
				case PpsDataFilterExpressionType t:
					return t;
				case string expr:
					switch (expr)
					{
						case "and":
							return PpsDataFilterExpressionType.And;
						case "or":
							return PpsDataFilterExpressionType.Or;
						case "nand":
							return PpsDataFilterExpressionType.NAnd;
						case "nor":
							return PpsDataFilterExpressionType.NOr;
						default:
							throw new ArgumentOutOfRangeException(nameof(expr), expr, "Unknown logic expression.");
					}
				default:
					throw new ArgumentOutOfRangeException(nameof(obj), obj, "Unknown logic expression.");
			}
		} // func GetLogicExpression

		private static PpsDataFilterExpression GetExpressionFromObject(object expr, IFormatProvider formatProvider, PpsDataFilterParseOption options, bool throwException)
			=> Parse(expr, formatProvider, options, throwException);

		private static PpsDataFilterExpression GetExpressionFromKeyValue(KeyValuePair<string, object> expr)
		{
			if (expr.Value == null)
				return null;

			if (expr.Value is LuaTable t && t.Members.Count == 0 && t.ArrayList.Count > 0) // create in filter
				return CompareIn(expr.Key, t.ArrayList.ToArray());
			else // create compare filter
				return Compare(expr.Key, PpsDataFilterCompareOperator.Equal, expr.Value);
		} // func GetExpressionFromObject

		/// <summary>Create a filter expression from a table.</summary>
		/// <param name="expression"></param>
		/// <param name="formatProvider"></param>
		/// <param name="options"></param>
		/// <param name="throwException"></param>
		/// <returns></returns>
		public static PpsDataFilterExpression FromTable(LuaTable expression, IFormatProvider formatProvider = null, PpsDataFilterParseOption options = PpsDataFilterParseOption.AllowFields | PpsDataFilterParseOption.ReturnTrue, bool throwException = true)
		{
			/* { [0] = "or",  COLUMN = VALUE, COLUMN = { VALUE, ... }, "Expr", {} } */

			formatProvider = formatProvider ?? CultureInfo.CurrentCulture;

			var method = GetLogicExpression(expression.GetArrayValue(0, rawGet: true));

			// enumerate all members
			var expr = new PpsDataFilterLogicExpression(method,
				(
					from kv in expression.Members
					select GetExpressionFromKeyValue(kv)
				).Concat(
					from v in expression.ArrayList
					select GetExpressionFromObject(v, formatProvider, options, throwException)
				).Where(c => c != null).ToArray()
			);

			return expr.Reduce();
		} // func FromTable

		#endregion

		/// <summary>Test if the expression is true or empty.</summary>
		/// <param name="expr"></param>
		/// <returns></returns>
		public static bool IsEmpty(PpsDataFilterExpression expr)
			=> expr == null || expr == True;

		/// <summary>Returns a expression, that is true.</summary>
		public static PpsDataFilterExpression True => PpsDataFilterTrueExpression.Default;
		/// <summary>Returns a expression that is false.</summary>
		public static PpsDataFilterExpression False { get; } = new PpsDataFilterLogicExpression(PpsDataFilterExpressionType.NAnd, True);
	} // class PpsDataFilterExpression

	#endregion

	#region -- class PpsDataFilterExpressionConverter ---------------------------------

	/// <summary>Converts expressions</summary>
	public class PpsDataFilterExpressionConverter : TypeConverter
	{
		/// <summary></summary>
		/// <param name="context"></param>
		/// <param name="sourceType"></param>
		/// <returns></returns>
		public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
		{
			return typeof(PpsDataFilterExpression).IsAssignableFrom(sourceType)
				|| sourceType == typeof(string)
				|| base.CanConvertFrom(context, sourceType);
		}// func CanConvertFrom

		/// <summary></summary>
		/// <param name="context"></param>
		/// <param name="destinationType"></param>
		/// <returns></returns>
		public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
		{
			return destinationType == typeof(string)
				|| base.CanConvertFrom(context, destinationType);
		} // func CanConvertTo

		/// <summary></summary>
		/// <param name="context"></param>
		/// <param name="culture"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
		{
			switch (value)
			{
				case null:
					return PpsDataFilterExpression.True;
				case string s:
					return PpsDataFilterExpression.Parse(s);
				default:
					throw GetConvertFromException(value);
			}
		} // func ConvertFrom

		/// <summary></summary>
		/// <param name="context"></param>
		/// <param name="culture"></param>
		/// <param name="value"></param>
		/// <param name="destinationType"></param>
		/// <returns></returns>
		public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
		{
			if (value == null)
			{
				if (destinationType == typeof(string))
					return String.Empty;
			}
			else if (value is PpsDataFilterExpression expr)
			{
				if (destinationType == typeof(string))
					return expr == PpsDataFilterExpression.True ? String.Empty : expr.ToString();
			}

			throw GetConvertToException(value, destinationType);
		} // funcConvertTo
	} // class PpsDataFilterExpressionConverter

	#endregion

	#region -- class PpsDataFilterNativeExpression ------------------------------------

	/// <summary></summary>
	public sealed class PpsDataFilterNativeExpression : PpsDataFilterExpression
	{
		private readonly string key;

		/// <summary></summary>
		/// <param name="key"></param>
		public PpsDataFilterNativeExpression(string key)
			: base(PpsDataFilterExpressionType.Native)
		{
			this.key = key ?? throw new ArgumentNullException(nameof(key));
		} // ctor

		/// <inherited />
		public override void ToString(StringBuilder sb, IFormatProvider formatProvider)
			=> sb.Append(':').Append(key).Append(':');

		/// <summary></summary>
		public string Key => key;
	} // class PpsDataFilterNativeExpression

	#endregion

	#region -- class PpsDataFilterTrueExpression --------------------------------------

	/// <summary></summary>
	public sealed class PpsDataFilterTrueExpression : PpsDataFilterExpression
	{
		private PpsDataFilterTrueExpression()
			: base(PpsDataFilterExpressionType.True)
		{
		} // ctor

		/// <inherited />
		public override void ToString(StringBuilder sb, IFormatProvider formatProvider) { }

		/// <summary>Returns a expression, that is true.</summary>
		public static PpsDataFilterExpression Default { get; } = new PpsDataFilterTrueExpression();
	} // class PpsDataFilterTrueExpression

	#endregion

	#region -- enum PpsDataFilterValueType --------------------------------------------

	/// <summary></summary>
	public enum PpsDataFilterValueType
	{
		/// <summary></summary>
		Null,
		/// <summary></summary>
		Text,
		/// <summary></summary>
		Date,
		/// <summary>A formatted Integer, p.e. D-01689 or 0.12.4310.234</summary>
		Number,
		/// <summary>A value which is presenting an amount or key.</summary>
		Integer,
		/// <summary>A value which is presenting a decimal value.</summary>
		Decimal,
		/// <summary>An other column.</summary>
		Field,
		/// <summary>A placeholder</summary>
		Variable,
		/// <summary>An array of values.</summary>
		Array
	} // enum PpsDataFilterValueType

	#endregion

	#region -- enum PpsDataFilterValueParseOption -------------------------------------

	/// <summary>Parse options for the value</summary>
	[Flags]
	public enum PpsDataFilterParseOption
	{
		/// <summary>Allow parse field names.</summary>
		AllowFields = 1,
		/// <summary>Allow variables in expression.</summary>
		AllowVariables = 2,
		/// <summary>A unescaped text, will be parsed as an field.</summary>
		FieldsFirst = 4,
		/// <summary>Returns at least a <c>true</c> instead of <c>null</c>.</summary>
		ReturnTrue = 8
	} // enum PpsDataFilterValueParseOption

	#endregion

	#region -- class PpsDataFilterValue -----------------------------------------------

	/// <summary></summary>
	public abstract class PpsDataFilterValue : IEquatable<PpsDataFilterValue>
	{
		/// <summary></summary>
		/// <returns></returns>
		public sealed override int GetHashCode()
			=> Type.GetHashCode() ^ GetValueHashCode();

		/// <summary></summary>
		/// <param name="obj"></param>
		/// <returns></returns>
		public sealed override bool Equals(object obj)
		{
			if (ReferenceEquals(this, obj))
				return true;
			else if (obj is PpsDataFilterValue other)
				return Equals(other);
			else
				return false;
		} // func Equals

		/// <summary></summary>
		/// <param name="other"></param>
		/// <returns></returns>
		public bool Equals(PpsDataFilterValue other)
			=> this.Type == other.Type && EqualsValue(other);

		/// <summary></summary>
		/// <param name="other"></param>
		/// <returns></returns>
		protected abstract bool EqualsValue(PpsDataFilterValue other);

		/// <summary></summary>
		/// <returns></returns>
		protected abstract int GetValueHashCode();

		/// <summary></summary>
		/// <param name="sb"></param>
		/// <param name="formatProvider"></param>
		public abstract void ToString(StringBuilder sb, IFormatProvider formatProvider);

		/// <summary></summary>
		/// <returns></returns>
		public override string ToString()
			=> ToString(CultureInfo.CurrentUICulture);

		/// <summary></summary>
		/// <param name="formatProvider"></param>
		/// <returns></returns>
		public string ToString(IFormatProvider formatProvider)
		{
			var sb = new StringBuilder();
			ToString(sb, formatProvider);
			return sb.ToString();
		} // func ToString

		/// <summary></summary>
		public abstract PpsDataFilterValueType Type { get; }
	} // class PpsDataFilterValue

	#endregion

	#region -- class PpsDataFilterNullValue -------------------------------------------

	/// <summary></summary>
	public sealed class PpsDataFilterNullValue : PpsDataFilterValue
	{
		private PpsDataFilterNullValue()
		{
		} // ctor

		/// <summary></summary>
		/// <param name="other"></param>
		/// <returns></returns>
		protected override bool EqualsValue(PpsDataFilterValue other)
			=> true;

		/// <inherited/>
		protected override int GetValueHashCode()
			=> 23.GetHashCode();

		/// <inherited/>
		public override void ToString(StringBuilder sb, IFormatProvider formatProvider) { }

		/// <summary></summary>
		public override PpsDataFilterValueType Type => PpsDataFilterValueType.Null;

		/// <summary></summary>
		public static PpsDataFilterValue Default { get; } = new PpsDataFilterNullValue();
	} // class PpsDataFilterNullValue

	#endregion

	#region -- class PpsDataFilterFieldValue ------------------------------------------

	/// <summary></summary>
	public sealed class PpsDataFilterFieldValue : PpsDataFilterValue
	{
		private readonly string fieldName;

		/// <summary></summary>
		/// <param name="fieldName"></param>
		public PpsDataFilterFieldValue(string fieldName)
		{
			this.fieldName = fieldName ?? throw new ArgumentNullException(nameof(fieldName));
		} // ctor

		/// <summary></summary>
		/// <param name="other"></param>
		/// <returns></returns>
		protected override bool EqualsValue(PpsDataFilterValue other)
			=> Equals(fieldName, ((PpsDataFilterFieldValue)other).fieldName);

		/// <inherited/>
		protected override int GetValueHashCode()
			=> fieldName.GetHashCode();

		/// <inherited/>
		public override void ToString(StringBuilder sb, IFormatProvider formatProvider)
			=> sb.Append(':').Append(fieldName);

		/// <summary></summary>
		public string FieldName => fieldName;
		/// <summary></summary>
		public override PpsDataFilterValueType Type => PpsDataFilterValueType.Field;
	} // class PpsDataFilterFieldValue

	#endregion

	#region -- class PpsDataFilterVariableValue ---------------------------------------

	/// <summary></summary>
	public sealed class PpsDataFilterVariableValue : PpsDataFilterValue
	{
		private readonly string variableName;

		/// <summary></summary>
		/// <param name="variableName"></param>
		public PpsDataFilterVariableValue(string variableName)
		{
			this.variableName = variableName ?? throw new ArgumentNullException(nameof(variableName));
		} // ctor

		/// <summary></summary>
		/// <param name="other"></param>
		/// <returns></returns>
		protected override bool EqualsValue(PpsDataFilterValue other)
			=> Equals(variableName, ((PpsDataFilterVariableValue)other).variableName);

		/// <inherited/>
		protected override int GetValueHashCode()
			=> variableName.GetHashCode();

		/// <inherited/>
		public override void ToString(StringBuilder sb, IFormatProvider formatProvider)
			=> sb.Append('$').Append(variableName);

		/// <summary>Get the value for an variable</summary>
		/// <param name="variables"></param>
		/// <returns></returns>
		public PpsDataFilterValue GetValue(IPropertyReadOnlyDictionary variables)
		{
			if (variables.TryGetProperty(variableName, out var tmp))
			{
				switch (tmp)
				{
					case null:
						return PpsDataFilterNullValue.Default;

					case PpsDataFilterValue vv:
						return vv;

					case DateTime dt:
						return PpsDataFilterDateTimeValue.Create(dt.Date);

					case long i64:
						return new PpsDataFilterIntegerValue(i64);
					case int i32:
						return new PpsDataFilterIntegerValue(i32);
					case short i16:
						return new PpsDataFilterIntegerValue(i16);
					case sbyte i8:
						return new PpsDataFilterIntegerValue(i8);
					case uint ui32:
						return new PpsDataFilterIntegerValue(ui32);
					case ushort ui16:
						return new PpsDataFilterIntegerValue(ui16);
					case byte ui8:
						return new PpsDataFilterIntegerValue(ui8);

					case string str:
						return new PpsDataFilterTextValue(str);

					case float f32:
						return new PpsDataFilterDecimalValue(Convert.ToDecimal(f32));
					case double f64:
						return new PpsDataFilterDecimalValue(Convert.ToDecimal(f64));
					case decimal dec:
						return new PpsDataFilterDecimalValue(dec);

					default:
						return new PpsDataFilterTextValue(tmp.ChangeType<string>());
				}
			}
			else
				return PpsDataFilterNullValue.Default;
		} // func GetValue

		/// <summary></summary>
		public string VariableName => variableName;
		/// <summary></summary>
		public override PpsDataFilterValueType Type => PpsDataFilterValueType.Variable;
	} // class PpsDataFilterVariableValue

	#endregion

	#region -- class PpsDataFilterTextValue -------------------------------------------

	/// <summary></summary>
	public sealed class PpsDataFilterTextValue : PpsDataFilterValue
	{
		private readonly string text;

		/// <summary></summary>
		/// <param name="text"></param>
		public PpsDataFilterTextValue(string text)
		{
			this.text = text ?? throw new ArgumentNullException(nameof(text));
		} // ctor

		/// <summary></summary>
		/// <param name="other"></param>
		/// <returns></returns>
		protected override bool EqualsValue(PpsDataFilterValue other)
			=> Equals(text, ((PpsDataFilterTextValue)other).text);

		/// <summary></summary>
		/// <returns></returns>
		protected override int GetValueHashCode()
			=> text.GetHashCode();

		/// <inherited />
		public override void ToString(StringBuilder sb, IFormatProvider formatProvider)
		{
			var offset = 0;
			while (offset < text.Length && (!Char.IsWhiteSpace(text[offset]) && text[offset] != '"'))
				offset++;

			if (offset == text.Length)
				sb.Append(text);
			else
			{
				sb.Append('"');
				sb.Append(text, 0, offset);
				for (; offset < text.Length; offset++)
					if (text[offset] == '"')
						sb.Append("\"\"");
					else
						sb.Append(text[offset]);
				sb.Append('"');
			}
		} // proc ToString

		/// <summary>Text content</summary>
		public string Text => text;
		/// <summary></summary>
		public override PpsDataFilterValueType Type => PpsDataFilterValueType.Text;
	} // class PpsDataFilterCompareTextValue

	#endregion

	#region -- class PpsDataFilterIntegerValue ----------------------------------------

	/// <summary></summary>
	public sealed class PpsDataFilterIntegerValue : PpsDataFilterValue
	{
		private readonly long value;

		/// <summary></summary>
		/// <param name="value"></param>
		public PpsDataFilterIntegerValue(long value)
		{
			this.value = value;
		} // ctor

		/// <summary></summary>
		/// <param name="other"></param>
		/// <returns></returns>
		protected override bool EqualsValue(PpsDataFilterValue other)
			=> Equals(value, ((PpsDataFilterIntegerValue)other).value);

		/// <summary></summary>
		/// <returns></returns>
		protected override int GetValueHashCode()
			=> value.GetHashCode();

		/// <inherited />
		public override void ToString(StringBuilder sb, IFormatProvider formatProvider)
			=> sb.Append(value);

		/// <summary></summary>
		public long Value => value;
		/// <summary></summary>
		public override PpsDataFilterValueType Type => PpsDataFilterValueType.Integer;
	} // class PpsDataFilterIntegerValue

	#endregion

	#region -- class PpsDataFilterDecimalValue ----------------------------------------

	/// <summary></summary>
	public sealed class PpsDataFilterDecimalValue : PpsDataFilterValue
	{
		private readonly decimal value;

		/// <summary></summary>
		/// <param name="value"></param>
		public PpsDataFilterDecimalValue(decimal value)
		{
			this.value = value;
		} // ctor

		/// <summary></summary>
		/// <param name="other"></param>
		/// <returns></returns>
		protected override bool EqualsValue(PpsDataFilterValue other)
			=> Equals(value, ((PpsDataFilterDecimalValue)other).value);

		/// <summary></summary>
		/// <returns></returns>
		protected override int GetValueHashCode()
			=> value.GetHashCode();

		/// <inherited />
		public override void ToString(StringBuilder sb, IFormatProvider formatProvider)
			=> sb.Append(value.ToString(formatProvider));

		/// <summary></summary>
		public decimal Value => value;
		/// <summary></summary>
		public override PpsDataFilterValueType Type => PpsDataFilterValueType.Decimal;
	} // class PpsDataFilterDecimalValue

	#endregion

	#region -- class PpsDataFilterDateTimeValue ---------------------------------------

	/// <summary>Date value, that is always time range between the result should be.</summary>
	/// <remarks>
	/// The full format is #dd.MM.yyyyTHH:mm:ss~dd.MM.yyyyTHH:mm:ss#
	/// The values can be shorten, e.g. to #yyyy#
	/// 
	/// The definition is <c>from</c>&gt;= v &lt; <c>to</c>.
	/// </remarks>
	public sealed class PpsDataFilterDateTimeValue : PpsDataFilterValue
	{
		private static readonly char[] dateTimePartsSeparators = new[] { 'T', 't', ' ' };

		private readonly DateTime from;
		private readonly DateTime to;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary></summary>
		/// <param name="from"></param>
		/// <param name="to"></param>
		public PpsDataFilterDateTimeValue(DateTime from, DateTime to)
		{
			this.from = from;
			this.to = to;
		} // ctor

		/// <summary></summary>
		/// <param name="other"></param>
		/// <returns></returns>
		protected override bool EqualsValue(PpsDataFilterValue other)
			=> Equals(from, ((PpsDataFilterDateTimeValue)other).from) && Equals(to, ((PpsDataFilterDateTimeValue)other).to);

		/// <summary></summary>
		/// <returns></returns>
		protected override int GetValueHashCode()
			=> from.GetHashCode() ^ to.GetHashCode();

		#endregion

		#region -- ToString -----------------------------------------------------------

		private static string GetFormatPatternFiltered(DateTimeFormatInfo dtf, string allowedPatterns)
		{
			var shortDatePattern = dtf.ShortDatePattern;
			var dateSeparator = String.IsNullOrEmpty(dtf.DateSeparator) ? '\0' : dtf.DateSeparator[0];

			var myDatePattern = new StringBuilder(shortDatePattern.Length);
			var isEmpty = true;
			for (var i = 0; i < shortDatePattern.Length; i++)
			{
				var c = shortDatePattern[i];

				if (c == dateSeparator)
				{
					if (!isEmpty)
					{
						myDatePattern.Append(c);
						isEmpty = true;
					}
				}
				else if (allowedPatterns.IndexOf(c) >= 0)
				{
					myDatePattern.Append(c);
					isEmpty = false;
				}
			}

			// build pattern
			return myDatePattern.ToString();
		} // proc GetFormatPatternFiltered

		private static string GetTimePattern(char timePattern)
		{
			switch (timePattern)
			{
				case 'T':
					return "HH:mm:ss";
				case 't':
					return "HH:mm";
				default:
					return String.Empty;
			}
		} // func GetTimePattern

		private static string GetFormatPattern(DateTime dateTime, DateTime defaultDateTime, char timePattern, DateTimeFormatInfo dtf)
		{
			if (timePattern == 'T' || timePattern == 't')
			{
				if (dateTime == defaultDateTime)
					return String.Empty;
				else
					return dtf.ShortDatePattern + "T" + GetTimePattern(timePattern);
			}
			else
			{
				if (defaultDateTime.Day == dateTime.Day)
				{
					if (defaultDateTime.Month == dateTime.Month)
					{
						if (defaultDateTime.Year == dateTime.Year)
							return String.Empty;
						else
							return GetFormatPatternFiltered(dtf, "y");
					}
					else
						return GetFormatPatternFiltered(dtf, "yM");
				}
				else
					return dtf.ShortDatePattern;
			}
		} // func GetFormatDateTime

		private static void AppendDateFormat(StringBuilder sb, DateTime dateTime, string pattern, DateTimeFormatInfo dtf)
			=> sb.Append(dateTime.ToString(pattern, dtf));

		private static void AppendDateTimeRange(StringBuilder sb, DateTime from, DateTime to, char timePattern, IFormatProvider formatProvider)
		{
			var dtf = GetDateTimeFormatInfo(formatProvider);

			var fromPattern = GetFormatPattern(from, DateTime.MinValue, timePattern, dtf);
			var toPattern = GetFormatPattern(to, DateTime.MaxValue, timePattern, dtf);

			if (fromPattern.Length < toPattern.Length)
				fromPattern = toPattern;

			AppendDateFormat(sb, from, fromPattern, dtf);
			if (from != to)
			{
				sb.Append("~");
				AppendDateFormat(sb, to, fromPattern, dtf);
			}
		} // proc AppendDateTimeRange

		/// <inherited />
		public override void ToString(StringBuilder sb, IFormatProvider formatProvider)
		{
			if (formatProvider == null)
				throw new ArgumentNullException(nameof(formatProvider));

			sb.Append('#');
			if (!IsValid)
				sb.Append("0");
			else if (IsAll)
				sb.Append("~");
			else if (IsTimePoint)
			{
				var dtf = GetDateTimeFormatInfo(formatProvider);
				AppendDateFormat(sb, From, dtf.ShortDatePattern + "THH:mm:ss,fff", dtf);
			}
			else if (WithTime) // do we have a time component
			{
				var timePattern = from.Second != 0 || to.Second != 0 ? 'T' : 't';
				var toNorm = timePattern == 'T' ? to.AddSeconds(-1) : to.AddMinutes(-1);

				AppendDateTimeRange(sb, from, toNorm, timePattern, formatProvider);
			}
			else // only day based date
			{
				var dtf = GetDateTimeFormatInfo(formatProvider);

				if (from.Day == 1 && from.Month == 1 && to.Day == 1 && to.Month == 1 && from.Year + 1 == to.Year) // diff is a year
					AppendDateFormat(sb, from, GetFormatPatternFiltered(dtf, "y"), dtf);
				else if (from.Day == 1 && to.Day == 1 && IsNextMonth(from.Year, from.Month, to.Year, to.Month)) // diff is a month
					AppendDateFormat(sb, from, GetFormatPatternFiltered(dtf, "yM"), dtf);
				else
					AppendDateTimeRange(sb, from, to.AddDays(-1), 'D', formatProvider);
			}
			sb.Append('#');
		} // func ToString

		#endregion

		/// <summary></summary>
		public DateTime From => from;
		/// <summary></summary>
		public DateTime To => to;

		/// <summary>Is this value a point.</summary>
		public bool IsTimePoint => from == to;
		/// <summary>Is this a valid time range.</summary>
		public bool IsRange => from < to;
		/// <summary>This range has the maximum.</summary>
		public bool IsAll => from == DateTime.MinValue && to == DateTime.MaxValue;
		/// <summary>Also compare time values.</summary>
		public bool WithTime => HasTime(from) || HasTime(to);

		/// <summary>Is this a valid time range.</summary>
		public bool IsValid => from <= to;

		/// <summary></summary>
		public override PpsDataFilterValueType Type => PpsDataFilterValueType.Date;

		// -- Static ----------------------------------------------------------

		#region -- Parse --------------------------------------------------------------

		private static DateTimeFormatInfo GetDateTimeFormatInfo(IFormatProvider formatProvider)
			=> (DateTimeFormatInfo)formatProvider.GetFormat(typeof(DateTimeFormatInfo)) ?? throw new ArgumentNullException(nameof(DateTimeFormatInfo), "Format information is missing.");

		private static bool HasTime(DateTime dt)
			=> dt.Hour != 0 || dt.Minute != 0 || dt.Second != 0 || dt.Millisecond != 0;

		private bool IsNextMonth(int yearFrom, int monthFrom, int yearTo, int monthTo)
		{
			return monthFrom == 12
				? monthTo == 1 && yearTo == yearFrom + 1
				: yearTo == yearFrom && monthTo == monthFrom + 1;
		} // func IsNextMonth

		private static char JumpPattern(char patternSymbol, string datePattern, ref int patterPos)
		{
			while (patterPos < datePattern.Length && patternSymbol == datePattern[patterPos])
				patterPos++;

			if (patterPos < datePattern.Length) // jump over pattern
				return datePattern[patterPos++];
			else
				return '\0';
		} // func JumpPattern

		private static int ReadDigits(string inputDate, IFormatProvider formatProvider, ref int inputPos, char splitSymbol)
		{
			string digits;

			var symbolPos = inputDate.IndexOf(splitSymbol, inputPos);
			if (symbolPos == -1)
			{
				digits = inputDate.Substring(inputPos);
				inputPos = inputDate.Length;

				if (digits.Length == 0) // read after end
					return -1;
			}
			else
			{
				digits = inputDate.Substring(inputPos, symbolPos - inputPos);
				inputPos = symbolPos + 1;

				if (digits.Length == 0) // empty part
					return 0;
			}

			return Int32.TryParse(digits, NumberStyles.None, formatProvider, out var r) ? r : -1;
		} // func ReadDigits

		private static int GetValidMonth(int month)
			=> Math.Min(12, Math.Max(1, month));

		private static int GetValidDay(int year, int month, int day)
		{
			if (day < 1)
				return 1;
			else if (day < 28)
				return day;
			else
				return Math.Min(day, DateTime.DaysInMonth(year, month));
		} // func GetValidDay

		private static int GetValidSecond(int second)
			=> second < 0 ? 0 : second % 60;

		private static int GetValidMinute(int minute)
			=> minute < 0 ? 0 : minute % 60;

		private static int GetValidHour(int hour)
			=> hour < 0 ? 0 : hour % 24;

		private static void GetValidDate(DateTime resultDateTime, ref int year, ref int month, ref int day)
		{
			if (year < 1)
				year = resultDateTime.Year;

			if (month < 1)
				month = resultDateTime.Month;
			else
				month = GetValidMonth(month);

			if (day < 1)
				day = resultDateTime.Day;
			else
				day = GetValidDay(year, month, day);
		} // func GetValidDate

		private static char TryParseDateTime(string inputDate, IFormatProvider formatProvider, DateTimeFormatInfo dtf, ref DateTime resultDateTime)
		{
			// Guess a date combination
			// range patterns:
			//   yyyy
			//   MM.yyyy
			//   dd.MM.yyyy
			//   dd.MM.yyyyTHH:mm
			//   dd.MM.yyyyTHH:mm:ss
			// fill up patterns:
			//   dd.MM.
			//   dd.
			//   null

			if (String.IsNullOrWhiteSpace(inputDate))
				return '0';

			inputDate = inputDate.Trim();

			string datePart = null;
			string timePart = null;

			if (inputDate.IndexOfAny(dateTimePartsSeparators) > -1)
			{
				var parts = inputDate.Split(dateTimePartsSeparators);
				datePart = parts[0];
				timePart = parts[1];
			}
			else
			{
				if (inputDate.IndexOf(dtf.TimeSeparator) >= 0 || (inputDate.Length <= 2))
					timePart = inputDate;
				else
					datePart = inputDate;
			}

			int year = -1, month = -1, day = -1;
			int hour = -1, minute = -1, second = -1;

			var error = false;
			if (!string.IsNullOrEmpty(datePart))
				error = TryParseDatePart(datePart, formatProvider, dtf, ref year, ref month, ref day);

			if (!error && !string.IsNullOrEmpty(timePart))
			{
				error = ParseTimePart(timePart, formatProvider, dtf, ref hour, ref minute, ref second);
			}

			if (!error)
			{
				// only the date part is present
				if (!string.IsNullOrEmpty(datePart) && string.IsNullOrEmpty(timePart))
				{
					if (year < 1 && month < 1 && day < 1) // all components are missing
						return 'D';
					else if (year > 0 && month < 1 && day < 1) // only year is given
					{
						resultDateTime = new DateTime(year, 01, 01);
						return 'Y';
					}
					else if (year > 0 && month > 0 && day < 1) // year and month is given
					{
						resultDateTime = new DateTime(year, GetValidMonth(month), 01);
						return 'M';
					}
					else if (year > 0 && month < 1 && day >= 1 && day <= 12) // year and month is given (day is detected)
					{
						resultDateTime = new DateTime(year, day, 01);
						return 'M';
					}
					else // fill up with now, or return values
					{
						GetValidDate(resultDateTime, ref year, ref month, ref day);

						resultDateTime = new DateTime(year, month, day);
						return 'D';
					}
				}
				else if (hour >= 0 || minute >= 0 || second >= 0)
				{
					GetValidDate(resultDateTime, ref year, ref month, ref day);

					resultDateTime = new DateTime(year, month, day, GetValidHour(hour), GetValidMinute(minute), GetValidSecond(second));
					return second < 0 ? 't' : 'T';
				}
			}
			// error is set, parse the complete input date
			var datePattern = dtf.ShortDatePattern + "T" + dtf.LongTimePattern;
			if (DateTime.TryParseExact(inputDate, datePattern, dtf, DateTimeStyles.None, out var dt)
				|| DateTime.TryParse(inputDate, dtf, DateTimeStyles.None, out dt)) // try parse full date
			{
				resultDateTime = dt;
				return 'F';
			}
			else
				return 'E';
		} // func TryParseDateTime

		private static bool ParseTimePart(string timePart, IFormatProvider formatProvider, DateTimeFormatInfo dtf, ref int hour, ref int minute, ref int second)
		{
			var timePattern = dtf.LongTimePattern;
			var error = false;
			var patternPos = 0;
			var inputPos = 0;

			while (patternPos < timePattern.Length && !error)
			{
				// read complete pattern
				var patternSymbol = timePattern[patternPos];
				var splitSymbol = JumpPattern(patternSymbol, timePattern, ref patternPos); // returns the date part separator

				// read digits until the symbol
				var startAt = inputPos;
				var t = ReadDigits(timePart, formatProvider, ref inputPos, splitSymbol);
				var readedNum = inputPos - startAt;

				// set date part, by number
				if (t >= 0)
				{
					switch (patternSymbol)
					{
						case 'H':
							if (hour == -1)
								hour = t;
							break;
						case 'm':
							if (minute == -1)
								minute = t;
							break;
						case 's':
							if (second == -1)
								second = t;
							break;

						default:
							error = true;
							break;
					}
				}
			}
				return error;
		}

		private static bool TryParseDatePart(string datePart, IFormatProvider formatProvider, DateTimeFormatInfo dtf, ref int year, ref int month, ref int day)
		{
			// using regex
			// ^(?<year>\d{4})(-(?<month>\d\d)(-(?<day>\d\d)(T(?<hour>\d\d):(?<minute>\d\d)(:(?<seconds>\d\d))?)?)?)?$
			// string patterns = @"(?<day>\d{2}).(?<month>\d{2}).(?<year>\d{4})";
			//					@"(?<year>\d{4}).(?<month>\d{2}).(?<day>\d{2})";
			//					@"(?<year>\d{4}).(?<month>\d{2})"
			//					@"(?<month>\d{2}).(?<year>\d{4})"
			// or manualy

			var datePattern = dtf.ShortDatePattern;
			var hasError = false;

			// split string using dtf.DateSeparator
			// check the count of the parts
			// if parts.count = 1 -> assume that string contins the year
			// if parts.count = 2 -> assume that string contins the year and the month.
			//   Year part length is 4 char.
			// if parts.count = 3 -> assume that string contins the year and the month and day.
			//   Year part length is 4 char. Day Month order with respect to there order in the ShortDatePattern.
			var dateSep = !String.IsNullOrEmpty(dtf.DateSeparator) ? dtf.DateSeparator[0] : '.';
			var parts = datePart.Split(dateSep);
			if (datePart.IndexOf(dateSep) < 0)
			{
				return !Int32.TryParse(datePart, NumberStyles.None, formatProvider, out year);
			}

			var yearPartNdx = -1;
			var monthPartNdx = -1;
			var dayPartNdx = -1;
			var monthSucceedsDay = datePattern.IndexOf('M') > datePattern.IndexOf('d');

			switch (parts.Length)
			{
				case 2:
					if (parts[0].Length == 4)
					{
						yearPartNdx = 0;
						monthPartNdx = 1;
					}
					else if (parts[1].Length == 4)
					{
						monthPartNdx = 0;
						yearPartNdx = 1;
					}
					else
					{
						// we can also handle MM.dd or dd.MM here
						hasError = true;
					}
					break;
				case 3:
					if (parts[0].Length == 4)
					{
						yearPartNdx = 0;
						monthPartNdx = monthSucceedsDay ? 1 : 2;
						dayPartNdx   = monthSucceedsDay ? 2 : 1;
					}
					else if (parts[2].Length == 4)
					{
						yearPartNdx = 2;
						monthPartNdx = monthSucceedsDay ? 1 : 0;
						dayPartNdx = monthSucceedsDay ? 0 : 1;
					}
					else
						hasError = true;

					break;
				default:
					hasError = true;
					break;
			}
			if (!hasError &&  yearPartNdx >= 0 && !String.IsNullOrEmpty(parts[yearPartNdx]))
				hasError = !Int32.TryParse(parts[yearPartNdx], NumberStyles.None, formatProvider, out year);
			if (!hasError && monthPartNdx >= 0 && !String.IsNullOrEmpty(parts[monthPartNdx]))
				hasError = !Int32.TryParse(parts[monthPartNdx], NumberStyles.None, formatProvider, out month);
			if (!hasError && dayPartNdx >= 0 && !String.IsNullOrEmpty(parts[dayPartNdx]))
				hasError = !Int32.TryParse(parts[dayPartNdx], NumberStyles.None, formatProvider, out day);

			return hasError;
		} // func ParseDatePart

		internal static PpsDataFilterDateTimeValue ParseDateTime(string expression, int offset, int count, IFormatProvider formatProvider)
		{
			var dtf = GetDateTimeFormatInfo(formatProvider);

			// remove fence
			if (expression[offset] == '#')
			{
				offset++;
				count--;
			}
			var lastCharIndex = offset + count - 1;
			if (lastCharIndex < expression.Length && expression[lastCharIndex] == '#')
				count--;

			// parse date expression
			var inputDate = expression.Substring(offset, count);
			var dateSplit = inputDate.IndexOf('~');
			if (dateSplit >= 0) // split date format
			{
				var from = DateTime.MinValue;
				var to = DateTime.MaxValue;

				var isFromValid = TryParseDateTime(inputDate.Substring(0, dateSplit), formatProvider, dtf, ref from);
				var isToValid = TryParseDateTime(inputDate.Substring(dateSplit + 1), formatProvider, dtf, ref to);

				switch (isToValid) // move over bound
				{
					case 'Y':
						to = to.AddYears(1);
						break;
					case 'M':
						to = to.AddMonths(1);
						break;
					case 'D':
						to = to.AddDays(1);
						break;
					case 't':
						to = to.AddMinutes(1);
						break;
					case 'T':
						to = to.AddSeconds(1);
						break;
				}

				if (isFromValid == 'E' || isToValid == 'E')
					return NoneValid;
				else if (isFromValid != '0' && isToValid != '0')
					return new PpsDataFilterDateTimeValue(from, to);
				else if (isFromValid != '0')
					return new PpsDataFilterDateTimeValue(from, DateTime.MaxValue);
				else if (isToValid != '0')
					return new PpsDataFilterDateTimeValue(DateTime.MinValue, to);
				else
					return new PpsDataFilterDateTimeValue(DateTime.MinValue, DateTime.MaxValue);
			}
			else if (inputDate == "0")
				return NoneValid;
			else
			{
				var dt = DateTime.Now.Date;
				switch (TryParseDateTime(inputDate, formatProvider, dtf, ref dt))
				{
					case 'Y':
						return new PpsDataFilterDateTimeValue(dt, dt.AddYears(1));
					case 'M':
						return new PpsDataFilterDateTimeValue(dt, dt.AddMonths(1));
					case 'D':
						return new PpsDataFilterDateTimeValue(dt, dt.AddDays(1));
					case 't':
						return new PpsDataFilterDateTimeValue(dt, dt.AddMinutes(1));
					case 'T':
						return new PpsDataFilterDateTimeValue(dt, dt.AddSeconds(1));
					case 'F':
						return new PpsDataFilterDateTimeValue(dt, dt);
					case '0':
						return new PpsDataFilterDateTimeValue(dt, dt.AddDays(1));
					default:
						return NoneValid;
				}
			}
		} // func ParseDateTime

		#endregion

		#region -- Create -------------------------------------------------------------

		/// <summary>Create a datetime range.</summary>
		/// <param name="dt"></param>
		/// <returns></returns>
		public static PpsDataFilterDateTimeValue Create(DateTime dt)
		{
			if (dt.Hour == 0)
			{
				if (dt.Minute == 0)
				{
					if (dt.Second == 0)
					{
						if (dt.Millisecond == 0)
							return new PpsDataFilterDateTimeValue(dt, dt.AddDays(1));
						else
							return new PpsDataFilterDateTimeValue(dt, dt.AddSeconds(1));
					}
					else
						return new PpsDataFilterDateTimeValue(dt, dt.AddMinutes(1));
				}
				else
					return new PpsDataFilterDateTimeValue(dt, dt.AddHours(1));
			}
			else
				return new PpsDataFilterDateTimeValue(dt, dt);
		} // func Create

		#endregion

		/// <summary>Stores none valid time range.</summary>
		public static PpsDataFilterDateTimeValue NoneValid { get; } = new PpsDataFilterDateTimeValue(DateTime.MaxValue, DateTime.MinValue);
	} // class PpsDataFilterDateTimeValue

	#endregion

	#region -- class PpsDataFilterTextKeyValue ----------------------------------------

	/// <summary></summary>
	public sealed class PpsDataFilterTextKeyValue : PpsDataFilterValue
	{
		private readonly string text;

		/// <summary></summary>
		/// <param name="text"></param>
		public PpsDataFilterTextKeyValue(string text)
		{
			this.text = text ?? throw new ArgumentNullException(nameof(text));
		} // ctor

		/// <summary></summary>
		/// <param name="other"></param>
		/// <returns></returns>
		protected override bool EqualsValue(PpsDataFilterValue other)
			=> Equals(text, ((PpsDataFilterTextKeyValue)other).text);

		/// <summary></summary>
		/// <returns></returns>
		protected override int GetValueHashCode()
			=> text.GetHashCode();

		/// <inherited />
		public override void ToString(StringBuilder sb, IFormatProvider formatProvider)
		{
			sb.Append('#')
				.Append(text);
		} // proc ToString

		/// <summary></summary>
		public string Text => text;
		/// <summary></summary>
		public override PpsDataFilterValueType Type => PpsDataFilterValueType.Number;
	} // class PpsDataFilterTextKeyValue

	#endregion

	#region -- class PpsDataFilterArrayValue ------------------------------------------

	/// <summary>An array of values.</summary>
	public sealed class PpsDataFilterArrayValue : PpsDataFilterValue
	{
		private readonly PpsDataFilterValue[] values;

		/// <summary>Create a array value</summary>
		/// <param name="values"></param>
		public PpsDataFilterArrayValue(PpsDataFilterValue[] values)
		{
			if (values == null || values.Length == 0)
				throw new ArgumentNullException(nameof(values));

			var newValues = new List<PpsDataFilterValue>(values.Length);
			CopyValues(values, newValues);
			this.values = newValues.ToArray();
		} // ctor

		private static void CopyValues(PpsDataFilterValue[] values, List<PpsDataFilterValue> newValues)
		{
			newValues.Add(values[0]);

			for (var i = 1; i < values.Length; i++)
			{
				if (values[i] is PpsDataFilterArrayValue av)
					CopyValues(av.Values, newValues);
				else
				{
					var itemType = values[i].Type;
					if (!IsArrayCompatibleType(itemType))
						throw new ArgumentOutOfRangeException(nameof(values), itemType, $"{itemType} is not allowed as array-type.");
				}

				newValues.Add(values[i]);
			}
		} // proc CopyValues

		/// <summary></summary>
		/// <param name="other"></param>
		/// <returns></returns>
		protected override bool EqualsValue(PpsDataFilterValue other)
		{
			var otherArr = (PpsDataFilterArrayValue)other;
			if (values.Length == otherArr.values.Length)
			{
				for (var i = 0; i < values.Length; i++)
				{
					if (!Equals(values[i], otherArr.Values))
						return false;
				}
				return true;
			}
			else
				return false;
		} // func EqualsValue

		/// <summary></summary>
		/// <returns></returns>
		protected override int GetValueHashCode()
		{
			var r = 0;
			foreach (var v in values)
				r ^= v.GetHashCode();
			return r;
		} // func GetValueHashCode

		/// <inherited />
		public override void ToString(StringBuilder sb, IFormatProvider formatProvider)
		{
			sb.Append("(");

			values[0].ToString(sb, formatProvider);
			for (var i = 1; i < values.Length; i++)
			{
				sb.Append(' ');
				values[i].ToString(sb, formatProvider);
			}

			sb.Append(")");
		} // func ToString

		/// <summary>Values</summary>
		public PpsDataFilterValue[] Values => values;
		/// <summary>Return the item type</summary>
		public PpsDataFilterValueType ItemType => values[0].Type;
		/// <summary>Return Array</summary>
		public override PpsDataFilterValueType Type => PpsDataFilterValueType.Array;

		internal static bool IsArrayCompatibleType(PpsDataFilterValueType itemType)
		{
			switch (itemType)
			{
				case PpsDataFilterValueType.Field:
				case PpsDataFilterValueType.Integer:
				case PpsDataFilterValueType.Number:
				case PpsDataFilterValueType.Text:
					return true;
				default:
					return false;
			}
		} // func IsArrayCompatibleType
	} // class PpsDataFilterArrayValue

	#endregion

	#region -- class PpsDataFilterCompareExpression -----------------------------------

	/// <summary></summary>
	public sealed class PpsDataFilterCompareExpression : PpsDataFilterExpression
	{
		private readonly string operand;
		private readonly PpsDataFilterCompareOperator op;
		private readonly PpsDataFilterValue value;

		/// <summary></summary>
		/// <param name="operand"></param>
		/// <param name="op"></param>
		/// <param name="value"></param>
		public PpsDataFilterCompareExpression(string operand, PpsDataFilterCompareOperator op, PpsDataFilterValue value)
			: base(PpsDataFilterExpressionType.Compare)
		{
			this.operand = operand;
			this.op = op;
			this.value = value ?? throw new ArgumentNullException(nameof(value));

			if (value.Type == PpsDataFilterValueType.Array)
			{
				if (op != PpsDataFilterCompareOperator.Contains && op != PpsDataFilterCompareOperator.NotContains)
					throw new ArgumentException($"Arrays are only allowed for {PpsDataFilterCompareOperator.Contains} or {PpsDataFilterCompareOperator.NotContains}.");
			}
		} // ctor

		/// <inherited />
		public override PpsDataFilterExpression Reduce(IPropertyReadOnlyDictionary variables)
		{
			// combine single value arrays to equal expression
			if (value is PpsDataFilterArrayValue av && av.Values.Length == 1)
			{
				switch (op)
				{
					case PpsDataFilterCompareOperator.Contains:
						return new PpsDataFilterCompareExpression(operand, PpsDataFilterCompareOperator.Equal, av.Values[0]);
					case PpsDataFilterCompareOperator.NotContains:
						return new PpsDataFilterCompareExpression(operand, PpsDataFilterCompareOperator.NotEqual, av.Values[0]);
					default:
						return new PpsDataFilterCompareExpression(operand, op, av.Values[0]);
				}
			}
			else if (variables != null && value is PpsDataFilterVariableValue var)
				return new PpsDataFilterCompareExpression(operand, op, var.GetValue(variables));
			else
				return this;
		} // func Reduce

		/// <inherited />
		public override void ToString(StringBuilder sb, IFormatProvider formatProvider)
		{
			if (String.IsNullOrEmpty(operand))
			{
				value.ToString(sb, formatProvider);
			}
			else
			{
				sb.Append(operand)
					.Append(':');
				switch (op)
				{
					case PpsDataFilterCompareOperator.Equal:
						sb.Append('=');
						break;
					case PpsDataFilterCompareOperator.NotEqual:
						sb.Append("!=");
						break;
					case PpsDataFilterCompareOperator.Greater:
						sb.Append('>');
						break;
					case PpsDataFilterCompareOperator.GreaterOrEqual:
						sb.Append(">=");
						break;
					case PpsDataFilterCompareOperator.Lower:
						sb.Append('<');
						break;
					case PpsDataFilterCompareOperator.LowerOrEqual:
						sb.Append("<=");
						break;
					case PpsDataFilterCompareOperator.NotContains:
						sb.Append("!");
						break;
					case PpsDataFilterCompareOperator.Contains:
						break;
					case PpsDataFilterCompareOperator.StartWith:
						sb.Append("[");
						break;
					case PpsDataFilterCompareOperator.EndWith:
						sb.Append("]");
						break;
					case PpsDataFilterCompareOperator.NotStartWith:
						sb.Append("![");
						break;
					case PpsDataFilterCompareOperator.NotEndWith:
						sb.Append("!]");
						break;

					default:
						throw new InvalidOperationException();
				}
				value.ToString(sb, formatProvider);
			}
		} // func ToString

		/// <summary></summary>
		public string Operand => operand;
		/// <summary></summary>
		public PpsDataFilterCompareOperator Operator => op;
		/// <summary></summary>
		public PpsDataFilterValue Value => value;
	} // class PpsDataFilterCompareExpression

	#endregion

	#region -- class PpsDataFilterLogicExpression -------------------------------------

	/// <summary></summary>
	public sealed class PpsDataFilterLogicExpression : PpsDataFilterExpression
	{
		#region -- enum CompareExpressionResult ---------------------------------------

		private enum CompareExpressionResult
		{
			Unequal,
			EqualOperand,
			EqualOperation,
			Equal
		} // enum CompareExpressionResult

		#endregion

		#region -- class ReduceHelper -------------------------------------------------

		private sealed class ReduceHelper
		{
			private readonly PpsDataFilterLogicExpression target;
			private readonly IPropertyReadOnlyDictionary variables;
			private readonly List<PpsDataFilterExpression> args = new List<PpsDataFilterExpression>();
			private readonly List<List<PpsDataFilterValue>> combineValues = new List<List<PpsDataFilterValue>>();

			public ReduceHelper(PpsDataFilterLogicExpression target, IPropertyReadOnlyDictionary variables)
			{
				this.target = target;
				this.variables = variables;

				Append(target.Arguments);
			} // ctor

			private CompareExpressionResult CompareExpression(PpsDataFilterCompareExpression cmp, PpsDataFilterExpression other)
			{
				if (other is PpsDataFilterCompareExpression otherCmp
					&& String.Compare(cmp.Operand, otherCmp.Operand, StringComparison.OrdinalIgnoreCase) == 0)
				{
					if (cmp.Operator == otherCmp.Operator)
					{
						return Equals(cmp.Value, otherCmp.Value)
							? CompareExpressionResult.Equal
							: CompareExpressionResult.EqualOperation;
					}
					else
						return CompareExpressionResult.EqualOperand;
				}
				else
					return CompareExpressionResult.Unequal;
			} // func CompareExpression


			private static void AddValueTo(List<PpsDataFilterValue> values, PpsDataFilterValue valueToAdd)
			{
				foreach (var v in values)
				{
					if (valueToAdd.Equals(v))
						return;
				}
				values.Add(valueToAdd);
			} // proc AddValueTo

			private void AddValueTo(int i, PpsDataFilterArrayValue currentArrayContent, PpsDataFilterValue valueToAdd)
			{
				// extent list
				while (i >= combineValues.Count)
					combineValues.Add(null);

				// get values
				var values = combineValues[i];
				if (values == null) // init list
				{
					values = combineValues[i] = new List<PpsDataFilterValue>();
					foreach (var c in currentArrayContent.Values)
						AddValueTo(values, c);
				}

				AddValueTo(values, valueToAdd);
			} // proc AddValueTo

			private bool AppendCore(PpsDataFilterExpression expr)
			{
				if (expr is PpsDataFilterCompareExpression cmp && !String.IsNullOrEmpty(cmp.Operand))
				{
					if (target.Type == PpsDataFilterExpressionType.NOr || target.Type == PpsDataFilterExpressionType.Or)
					{
						// same operands can be combined to an in
						for (var i = 0; i < args.Count; i++)
						{
							if (args[i] is PpsDataFilterCompareExpression curCmp)
							{
								switch (CompareExpression(cmp, curCmp))
								{
									case CompareExpressionResult.Equal:
										return true;
									case CompareExpressionResult.EqualOperand:
										if (curCmp.Operator == PpsDataFilterCompareOperator.Contains
											&& cmp.Operator == PpsDataFilterCompareOperator.Equal
											&& curCmp.Value is PpsDataFilterArrayValue arr && PpsDataFilterArrayValue.IsArrayCompatibleType(cmp.Value.Type)) // add to array
										{
											AddValueTo(i, arr, cmp.Value);
											return true;
										}
										break;
									case CompareExpressionResult.EqualOperation:
										if (cmp.Operator == PpsDataFilterCompareOperator.Equal
											&& curCmp.Value.Type == cmp.Value.Type
											&& PpsDataFilterArrayValue.IsArrayCompatibleType(curCmp.Value.Type)) // create a "contains" with array
										{
											var arr3 = new PpsDataFilterArrayValue(new PpsDataFilterValue[] { curCmp.Value });
											args[i] = new PpsDataFilterCompareExpression(curCmp.Operand, PpsDataFilterCompareOperator.Contains, arr3);
											AddValueTo(i, arr3, cmp.Value);
											return true;
										}
										else if (cmp.Operator == PpsDataFilterCompareOperator.Contains
											&& curCmp.Value is PpsDataFilterArrayValue arr1
											&& cmp.Value is PpsDataFilterArrayValue arr2) // if array combine in array
										{
											foreach (var val in arr2.Values)
												AddValueTo(i, arr1, val);
											return true;
										}
										break;
								}
							}
						}
					}
					else if (target.Type == PpsDataFilterExpressionType.NAnd || target.Type == PpsDataFilterExpressionType.And)
					{
						for (var i = 0; i < args.Count; i++)
						{
							// same expression are reduntant and can be removed
							if (args[i] is PpsDataFilterCompareExpression curCmp)
							{
								switch (CompareExpression(cmp, curCmp))
								{
									case CompareExpressionResult.Equal:
										return true;
									case CompareExpressionResult.EqualOperand:
										if (curCmp.Operator == PpsDataFilterCompareOperator.NotContains
											&& cmp.Operator == PpsDataFilterCompareOperator.NotEqual
											&& curCmp.Value is PpsDataFilterArrayValue arr && PpsDataFilterArrayValue.IsArrayCompatibleType(cmp.Value.Type)) // add to array
										{
											AddValueTo(i, arr, cmp.Value);
											return true;
										}
										break;
									case CompareExpressionResult.EqualOperation:
										// equal operation with differnt values is false
										if (cmp.Operator == PpsDataFilterCompareOperator.Equal)
										{
											args.Clear();
											args.Add(False);
											return false;
										}
										if (cmp.Operator == PpsDataFilterCompareOperator.NotEqual
											&& curCmp.Value.Type == cmp.Value.Type
											&& PpsDataFilterArrayValue.IsArrayCompatibleType(curCmp.Value.Type)) // create a "not contains" with array
										{
											var arr3 = new PpsDataFilterArrayValue(new PpsDataFilterValue[] { curCmp.Value });
											args[i] = new PpsDataFilterCompareExpression(curCmp.Operand, PpsDataFilterCompareOperator.NotContains, arr3);
											AddValueTo(i, arr3, cmp.Value);
											return true;
										}
										else if (cmp.Operator == PpsDataFilterCompareOperator.NotContains
											&& curCmp.Value is PpsDataFilterArrayValue arr1
											&& cmp.Value is PpsDataFilterArrayValue arr2) // if array combine in array
										{
											foreach (var val in arr2.Values)
												AddValueTo(i, arr1, val);
											return true;
										}
										break;
								}
							}
						}
					}
				}
				else
				{
					switch (target.Type)
					{
						case PpsDataFilterExpressionType.And:
							if (expr == False) // false in "and" is always false)
							{
								combineValues.Clear();
								args.Clear();
								args.Add(False);
								return false;
							}
							else if (expr == True)
							{
								if (args.Count > 0) // is useless
									return true;
							}
							break;
						case PpsDataFilterExpressionType.NAnd:
							if (expr == False) // false in "not and" is always true
							{
								combineValues.Clear();
								args.Clear();
								args.Add(True);
								return false;
							}
							else if (expr == True)
							{
								if (args.Count > 0) // is useless
									return true;
							}
							break;
						case PpsDataFilterExpressionType.Or:
							if (expr == True) // true in "or" is always true
							{
								combineValues.Clear();
								args.Clear();
								args.Add(True);
								return false;
							}
							else if (expr == False)
							{
								if (args.Count > 0) // is useless
									return true;
							}
							break;
						case PpsDataFilterExpressionType.NOr:  // true in "or" is always false
							if (expr == True)
							{
								combineValues.Clear();
								args.Clear();
								args.Add(False);
								return false;
							}
							else if (expr == False)
							{
								if (args.Count > 0) // is useless
									return true;
							}
							break;
					}
				}

				if (args.Count == 1 && (args[0] == False || args[0] == True)) // clear useless booleans
					args.Clear();
				args.Add(expr);

				return true;
			} // proc AppendCore

			private void Append(IEnumerable<PpsDataFilterExpression> exprs)
			{
				foreach (var cur in exprs)
				{
					if (cur == null)
						continue;

					var expr = cur.Reduce(variables);
					if (expr.Type == target.Type) // compine expresion of same type
					{
						foreach (var c in ((PpsDataFilterLogicExpression)expr).Arguments)
						{
							if (!AppendCore(c)) // only add, without Reduce, because they reduced before
								return;
						}
					}
					else if (!AppendCore(expr)) // append part
						return;
				}
			} // proc Append

			public PpsDataFilterExpression Build()
			{
				// combine values
				for (var i = 0; i < combineValues.Count; i++)
				{
					if (combineValues[i] == null)
						continue;

					var cmp = (PpsDataFilterCompareExpression)args[i];
					args[i] = new PpsDataFilterCompareExpression(cmp.Operand, cmp.Operator,
							new PpsDataFilterArrayValue(combineValues[i].ToArray())
					);
				}

				// optimize 0 or 1 results
				if (args.Count == 0)
				{
					return target.Type == PpsDataFilterExpressionType.And || target.Type == PpsDataFilterExpressionType.Or
						? True
						: False;
				}
				else if (args.Count == 1)
				{
					if (target.Type == PpsDataFilterExpressionType.And || target.Type == PpsDataFilterExpressionType.Or)
						return args[0];
					else if (target.Type == PpsDataFilterExpressionType.NOr || target.Type == PpsDataFilterExpressionType.NAnd)
					{
						if (args[0] == True)
							return False;
						else if (args[0] == False)
							return True;
						else if (args[0] is PpsDataFilterCompareExpression cmp)
						{
							switch (cmp.Operator)
							{
								case PpsDataFilterCompareOperator.Contains:
									return new PpsDataFilterCompareExpression(cmp.Operand, PpsDataFilterCompareOperator.NotContains, cmp.Value);
								case PpsDataFilterCompareOperator.NotContains:
									return new PpsDataFilterCompareExpression(cmp.Operand, PpsDataFilterCompareOperator.Contains, cmp.Value);
								case PpsDataFilterCompareOperator.Equal:
									return new PpsDataFilterCompareExpression(cmp.Operand, PpsDataFilterCompareOperator.NotEqual, cmp.Value);
								case PpsDataFilterCompareOperator.NotEqual:
									return new PpsDataFilterCompareExpression(cmp.Operand, PpsDataFilterCompareOperator.Equal, cmp.Value);
								case PpsDataFilterCompareOperator.Lower:
									return new PpsDataFilterCompareExpression(cmp.Operand, PpsDataFilterCompareOperator.LowerOrEqual, cmp.Value);
								case PpsDataFilterCompareOperator.LowerOrEqual:
									return new PpsDataFilterCompareExpression(cmp.Operand, PpsDataFilterCompareOperator.Lower, cmp.Value);
								case PpsDataFilterCompareOperator.Greater:
									return new PpsDataFilterCompareExpression(cmp.Operand, PpsDataFilterCompareOperator.GreaterOrEqual, cmp.Value);
								case PpsDataFilterCompareOperator.GreaterOrEqual:
									return new PpsDataFilterCompareExpression(cmp.Operand, PpsDataFilterCompareOperator.Greater, cmp.Value);
								
								case PpsDataFilterCompareOperator.StartWith:
									return new PpsDataFilterCompareExpression(cmp.Operand, PpsDataFilterCompareOperator.NotStartWith, cmp.Value);
								case PpsDataFilterCompareOperator.EndWith:
									return new PpsDataFilterCompareExpression(cmp.Operand, PpsDataFilterCompareOperator.NotEndWith, cmp.Value);
								case PpsDataFilterCompareOperator.NotStartWith:
									return new PpsDataFilterCompareExpression(cmp.Operand, PpsDataFilterCompareOperator.StartWith, cmp.Value);
								case PpsDataFilterCompareOperator.NotEndWith:
									return new PpsDataFilterCompareExpression(cmp.Operand, PpsDataFilterCompareOperator.EndWith, cmp.Value);
							}
						}
					}
				}

				return new PpsDataFilterLogicExpression(target.Type, args.ToArray());
			} // proc Build
		} // class ReduceHelper

		#endregion

		private readonly PpsDataFilterExpression[] arguments;

		/// <summary></summary>
		/// <param name="method"></param>
		/// <param name="arguments"></param>
		public PpsDataFilterLogicExpression(PpsDataFilterExpressionType method, params PpsDataFilterExpression[] arguments)
			: base(method)
		{
			switch (method)
			{
				case PpsDataFilterExpressionType.And:
				case PpsDataFilterExpressionType.Or:
				case PpsDataFilterExpressionType.NAnd:
				case PpsDataFilterExpressionType.NOr:
					break;
				default:
					throw new ArgumentException("method is wrong.");
			}
			if (arguments == null || arguments.Length < 1)
				throw new ArgumentNullException(nameof(arguments));

			this.arguments = arguments;
		} // ctor

		/// <summary></summary>
		public override PpsDataFilterExpression Reduce(IPropertyReadOnlyDictionary variables = null)
			=> new ReduceHelper(this, variables).Build();

		/// <inherited />
		public override void ToString(StringBuilder sb, IFormatProvider formatProvider)
		{
			switch (Type)
			{
				case PpsDataFilterExpressionType.And:
					sb.Append("and");
					break;
				case PpsDataFilterExpressionType.Or:
					sb.Append("or");
					break;
				case PpsDataFilterExpressionType.NAnd:
					sb.Append("nand");
					break;
				case PpsDataFilterExpressionType.NOr:
					sb.Append("nor");
					break;
				default:
					throw new ArgumentException("method is wrong.");
			}
			sb.Append('(');

			arguments[0].ToString(sb, formatProvider);

			for (var i = 1; i < arguments.Length; i++)
			{
				sb.Append(' ');
				arguments[i].ToString(sb, formatProvider);
			}

			sb.Append(')');

		} // func ToString

		/// <summary></summary>
		public PpsDataFilterExpression[] Arguments => arguments;
	} // class PpsDataFilterLogicExpression

	#endregion

	#region -- class PpsDataFilterVisitor<T> ------------------------------------------

	/// <summary></summary>
	public abstract class PpsDataFilterVisitor<T>
		where T : class
	{
		/// <summary></summary>
		/// <returns></returns>
		public abstract T CreateTrueFilter();

		/// <summary></summary>
		/// <param name="expression"></param>
		/// <returns></returns>
		public abstract T CreateNativeFilter(PpsDataFilterNativeExpression expression);

		/// <summary></summary>
		/// <param name="expression"></param>
		/// <returns></returns>
		public abstract T CreateCompareFilter(PpsDataFilterCompareExpression expression);

		/// <summary></summary>
		/// <param name="operand"></param>
		/// <param name="values"></param>
		/// <returns></returns>
		public abstract T CreateCompareIn(string operand, PpsDataFilterArrayValue values);

		/// <summary></summary>
		/// <param name="operand"></param>
		/// <param name="values"></param>
		/// <returns></returns>
		public abstract T CreateCompareNotIn(string operand, PpsDataFilterArrayValue values);

		/// <summary></summary>
		/// <param name="method"></param>
		/// <param name="arguments"></param>
		/// <returns></returns>
		public abstract T CreateLogicFilter(PpsDataFilterExpressionType method, IEnumerable<T> arguments);

		/// <summary></summary>
		/// <param name="expression"></param>
		/// <returns></returns>
		public virtual T CreateFilter(PpsDataFilterExpression expression)
		{
			if (expression is PpsDataFilterNativeExpression nativeExpr)
				return CreateNativeFilter(nativeExpr);
			else if (expression is PpsDataFilterLogicExpression logicExpr)
				return CreateLogicFilter(expression.Type, from c in logicExpr.Arguments select CreateFilter(c));
			else if (expression is PpsDataFilterCompareExpression compareExpr)
			{
				if (compareExpr.Value is PpsDataFilterArrayValue arr)
				{
					switch (compareExpr.Operator)
					{
						case PpsDataFilterCompareOperator.Contains:
							return CreateCompareIn(compareExpr.Operand, arr);
						case PpsDataFilterCompareOperator.NotContains:
							return CreateCompareNotIn(compareExpr.Operand, arr);
						default:
							throw new NotImplementedException();
					}
				}
				else
					return CreateCompareFilter(compareExpr);
			}
			else if (expression is PpsDataFilterTrueExpression)
				return CreateTrueFilter();
			else
				throw new NotImplementedException();
		} // func CreateFilter
	} // class PpsDataFilterVisitor

	#endregion

	#region -- class PpsDataFilterVisitorSql ------------------------------------------

	/// <summary></summary>
	public abstract class PpsDataFilterVisitorSql : PpsDataFilterVisitor<string>
	{
		#region -- CreateTrueFilter----------------------------------------------------

		/// <summary></summary>
		/// <returns></returns>
		public override string CreateTrueFilter()
			=> "1=1";

		/// <summary></summary>
		/// <param name="message"></param>
		/// <returns></returns>
		protected virtual string CreateErrorFilter(string message)
			=> "1=0";

		/// <summary></summary>
		/// <param name="columnToken"></param>
		/// <returns></returns>
		protected virtual string CreateColumnErrorFilter(string columnToken)
			=> CreateErrorFilter(String.Format("Column '{0}' not found.'", columnToken ?? "<null>"));

		#endregion

		#region -- CreateCompareFilter ------------------------------------------------

		/// <summary></summary>
		/// <param name="expression"></param>
		/// <returns></returns>
		public override string CreateCompareFilter(PpsDataFilterCompareExpression expression)
		{
			switch (expression.Value.Type)
			{
				case PpsDataFilterValueType.Field:
					return CreateCompareFilterField(expression.Operand, expression.Operator, ((PpsDataFilterFieldValue)expression.Value).FieldName);
				case PpsDataFilterValueType.Date:
					return CreateCompareFilterDate(expression.Operand, expression.Operator, ((PpsDataFilterDateTimeValue)expression.Value).From, ((PpsDataFilterDateTimeValue)expression.Value).To);

				case PpsDataFilterValueType.Text:
					//if (expression.Operand == null) // fulltext
					//	;
					//goto case PpsDataFilterValueType.Decimal;
				case PpsDataFilterValueType.Number:
				case PpsDataFilterValueType.Integer:
				case PpsDataFilterValueType.Decimal:
					var columnToken = expression.Operand;
					var column = LookupNumberColumn(columnToken);
					if (column == null)
						return CreateColumnErrorFilter(columnToken);

					var (columnName, columnType) = column;
					string parseableValue;
					try
					{
						string value = expression.Value is PpsDataFilterTextValue txtValueFilter
							? txtValueFilter.Text
							: expression.Value.ToString(CultureInfo.InvariantCulture);
						parseableValue = CreateParsableValue(value, columnType);
					}
					catch (FormatException)
					{
						return CreateColumnErrorFilter(columnToken);
					}
					return CreateDefaultCompareValue(columnName, expression.Operator, parseableValue, columnType == typeof(string));

				case PpsDataFilterValueType.Null:
					return CreateCompareFilterNull(expression.Operand, expression.Operator);
				default:
					throw new NotImplementedException();
			}

		} // func CreateCompareFilter

		private string CreateDefaultCompareValue(string columnName, PpsDataFilterCompareOperator op, string value, bool useContains)
		{
			switch (op)
			{
				case PpsDataFilterCompareOperator.StartWith:
				case PpsDataFilterCompareOperator.EndWith:
				case PpsDataFilterCompareOperator.Contains:
					if (useContains)
					{
						var flags = op == PpsDataFilterCompareOperator.Contains
							? PpsSqlLikeStringEscapeFlag.Both
							: (op == PpsDataFilterCompareOperator.StartWith ? PpsSqlLikeStringEscapeFlag.Trailing : PpsSqlLikeStringEscapeFlag.Leading);

						return columnName + " LIKE " + CreateLikeString(value, flags);
					}
					else
						goto case PpsDataFilterCompareOperator.Equal;

				case PpsDataFilterCompareOperator.NotContains:
				case PpsDataFilterCompareOperator.NotStartWith:
				case PpsDataFilterCompareOperator.NotEndWith:
					if (useContains)
					{
						var flags = op == PpsDataFilterCompareOperator.NotContains
							? PpsSqlLikeStringEscapeFlag.Both
							: (op == PpsDataFilterCompareOperator.NotStartWith ? PpsSqlLikeStringEscapeFlag.Trailing : PpsSqlLikeStringEscapeFlag.Leading);

						return "NOT " + columnName + " LIKE " + CreateLikeString(value, flags);
					}
					else
						goto case PpsDataFilterCompareOperator.NotEqual;

				case PpsDataFilterCompareOperator.Equal:
					return columnName + " = " + value;
				case PpsDataFilterCompareOperator.NotEqual:
					return columnName + " <> " + value;
				case PpsDataFilterCompareOperator.Greater:
					return columnName + " > " + value;
				case PpsDataFilterCompareOperator.GreaterOrEqual:
					return columnName + " >= " + value;
				case PpsDataFilterCompareOperator.Lower:
					return columnName + " < " + value;
				case PpsDataFilterCompareOperator.LowerOrEqual:
					return columnName + " <= " + value;

				default:
					throw new NotImplementedException();
			}
		} // func CreateDefaultCompareText

		private string CreateCompareFilterField(string operand, PpsDataFilterCompareOperator op, string fieldName)
			=> CreateDefaultCompareValue(operand, op, fieldName, false);

		private string CreateCompareFilterText(string columnToken, PpsDataFilterCompareOperator op, string text)
		{
			var column = LookupColumn(columnToken);
			if (column == null)
				return CreateColumnErrorFilter(columnToken);
			string parseableValue;
			try
			{
				parseableValue = CreateParsableValue(text, column.Item2);
			}
			catch (FormatException)
			{
				return CreateColumnErrorFilter(columnToken);
			}
			return CreateDefaultCompareValue(column.Item1, op, parseableValue, column.Item2 == typeof(string));
		} // func CreateCompareFilterText

		private string CreateCompareFilterInteger(string columnToken, PpsDataFilterCompareOperator op, long value)
		{
			var column = LookupColumn(columnToken);
			return column == null
				? CreateColumnErrorFilter(columnToken)
				: CreateDefaultCompareValue(column.Item1, op, CreateParsableValue(value.ChangeType<string>(), column.Item2), false);
		} // func CreateCompareFilterText

		private string CreateCompareFilterDecimal(string columnToken, PpsDataFilterCompareOperator op, decimal value)
		{
			var column = LookupColumn(columnToken);
			return column == null
				? CreateColumnErrorFilter(columnToken)
				: CreateDefaultCompareValue(column.Item1, op, CreateParsableValue(value.ChangeType<string>(), column.Item2), false);
		} // func CreateCompareFilterDecimal

		private string CreateCompareFilterNumber(string columnToken, PpsDataFilterCompareOperator op, string text)
		{
			var column = LookupNumberColumn(columnToken);
			if (column == null)
				return CreateColumnErrorFilter(columnToken);
			else if (column.Item2 != typeof(string))
				return CreateErrorFilter(String.Format("Text expected for column: {0}.", columnToken));

			var value = CreateParsableValue(text, typeof(string));
			switch (op)
			{
				case PpsDataFilterCompareOperator.Contains:
					return column.Item1 + " LIKE " + CreateLikeString(value, PpsSqlLikeStringEscapeFlag.Trailing);
				case PpsDataFilterCompareOperator.NotContains:
					return "NOT " + column.Item1 + " LIKE " + CreateLikeString(value, PpsSqlLikeStringEscapeFlag.Trailing);

				default:
					return CreateDefaultCompareValue(column.Item1, op, value, column.Item2 == typeof(string));
			}
		} // func CreateCompareFilterNumber

		private string CreateCompareFilterDate(string columnToken, PpsDataFilterCompareOperator op, DateTime from, DateTime to)
		{
			var column = LookupDateColumn(columnToken);
			if (column == null)
				return CreateColumnErrorFilter(columnToken);
			else if (column.Item2 != typeof(DateTime))
				return CreateErrorFilter(String.Format("Date expected for column: {0}.", columnToken));

			switch (op)
			{
				case PpsDataFilterCompareOperator.Contains:
				case PpsDataFilterCompareOperator.Equal:
					return column.Item1 + " BETWEEN " + CreateDateString(from) + " AND " + CreateDateString(to.AddMilliseconds(-1));
				case PpsDataFilterCompareOperator.NotContains:
				case PpsDataFilterCompareOperator.NotEqual:
					return "NOT " + column.Item1 + " BETWEEN " + CreateDateString(from) + " AND " + CreateDateString(to.AddMilliseconds(-1));

				case PpsDataFilterCompareOperator.Greater:
					return column.Item1 + " > " + CreateDateString(to.AddMilliseconds(-1));
				case PpsDataFilterCompareOperator.GreaterOrEqual:
					return column.Item1 + " >= " + CreateDateString(from);
				case PpsDataFilterCompareOperator.Lower:
					return column.Item1 + " < " + CreateDateString(from);
				case PpsDataFilterCompareOperator.LowerOrEqual:
					return column.Item1 + " <= " + CreateDateString(to.AddMilliseconds(-1));

				default:
					throw new NotImplementedException();
			}
		} // func CreateCompareFilterDate

		/// <summary></summary>
		/// <param name="value"></param>
		/// <returns></returns>
		protected virtual string CreateDateString(DateTime value)
			=> "'" + value.ToString("G") + "'";

		private string CreateCompareFilterNull(string columnToken, PpsDataFilterCompareOperator op)
		{
			Tuple<string, Type> column;
			switch (op)
			{
				case PpsDataFilterCompareOperator.Contains:
				case PpsDataFilterCompareOperator.Equal:
					column = LookupColumn(columnToken);
					return column == null
						? CreateColumnErrorFilter(columnToken)
						: column.Item1 + " is null";
				case PpsDataFilterCompareOperator.NotContains:
				case PpsDataFilterCompareOperator.NotEqual:
					column = LookupColumn(columnToken);
					return column == null
						? CreateColumnErrorFilter(columnToken)
						: column.Item1 + " is not null";
				case PpsDataFilterCompareOperator.Greater:
				case PpsDataFilterCompareOperator.GreaterOrEqual:
				case PpsDataFilterCompareOperator.Lower:
				case PpsDataFilterCompareOperator.LowerOrEqual:
					return CreateColumnErrorFilter("Invalid compare for null filter.");
				default:
					throw new NotImplementedException();
			}
		} // func CreateCompareFilterNull

		/// <summary></summary>
		/// <param name="text"></param>
		/// <param name="dataType"></param>
		/// <returns></returns>
		protected virtual string CreateParsableValue(string text, Type dataType)
		{
			var dataTypeName = dataType.Name;
			switch (dataTypeName)
			{
				case nameof(Boolean):
					return text == "1" || String.Compare(text, Boolean.TrueString, StringComparison.OrdinalIgnoreCase) == 0 ? "1" : "0";
				case nameof(Byte):
					return Byte.Parse(text).ToString(CultureInfo.InvariantCulture);
				case nameof(SByte):
					return SByte.Parse(text).ToString(CultureInfo.InvariantCulture);
				case nameof(Int16):
					return Int16.Parse(text).ToString(CultureInfo.InvariantCulture);
				case nameof(UInt16):
					return UInt16.Parse(text).ToString(CultureInfo.InvariantCulture);
				case nameof(Int32):
					return Int32.Parse(text).ToString(CultureInfo.InvariantCulture);
				case nameof(UInt32):
					return UInt32.Parse(text).ToString(CultureInfo.InvariantCulture);
				case nameof(Int64):
					return Int64.Parse(text).ToString(CultureInfo.InvariantCulture);
				case nameof(UInt64):
					return UInt64.Parse(text).ToString(CultureInfo.InvariantCulture);
				case nameof(Single):
					return Single.Parse(text).ToString(CultureInfo.InvariantCulture);
				case nameof(Double):
					return Double.Parse(text).ToString(CultureInfo.InvariantCulture);
				case nameof(Decimal):
					return Decimal.Parse(text).ToString(CultureInfo.InvariantCulture);
				case nameof(Char):
					return String.IsNullOrEmpty(text) ? "char(0)" : "'" + text[0] + "'";
				case nameof(DateTime):
					return CreateDateString(DateTime.Parse(text));
				case nameof(DateTimeOffset):
					return CreateDateString(DateTimeOffset.Parse(text).LocalDateTime);
				case nameof(String):
					var sb = new StringBuilder(text.Length + 2);
					var pos = 0;
					var startAt = 0;
					sb.Append('\'');
					while (pos < text.Length)
					{
						pos = text.IndexOf('\'', pos);
						if (pos == -1)
							pos = text.Length;
						else
						{
							sb.Append(text, startAt, pos - startAt);
							startAt = ++pos;
							sb.Append("''");
						}
					}
					if (startAt < pos)
						sb.Append(text, startAt, pos - startAt);
					sb.Append('\'');
					return sb.ToString();

				case nameof(Guid):
					return "'" + Guid.Parse(text).ToString("D") + "'";

				default:
					return text;
			}
		} // func CreateParsableValue

		/// <summary></summary>
		/// <param name="value"></param>
		/// <param name="flag"></param>
		/// <returns></returns>
		protected virtual string CreateLikeString(string value, PpsSqlLikeStringEscapeFlag flag)
		{
			value = value.Replace("%", "[%]")
				.Replace("?", "[?]");

			if ((flag & PpsSqlLikeStringEscapeFlag.Both) == PpsSqlLikeStringEscapeFlag.Both)
				value = "'%" + value.Substring(1, value.Length - 2) + "%'";
			else if ((flag & PpsSqlLikeStringEscapeFlag.Leading) == PpsSqlLikeStringEscapeFlag.Leading)
				value = "'%" + value.Substring(1);
			else if ((flag & PpsSqlLikeStringEscapeFlag.Trailing) == PpsSqlLikeStringEscapeFlag.Trailing)
				value = value.Substring(0, value.Length - 1) + "%'";

			return value;
		} // func CreateLikeString

		/// <summary></summary>
		/// <param name="columnToken"></param>
		/// <returns></returns>
		protected virtual Tuple<string, Type> LookupDateColumn(string columnToken)
			=> LookupColumn(columnToken);

		/// <summary></summary>
		/// <param name="columnToken"></param>
		/// <returns></returns>
		protected virtual Tuple<string, Type> LookupNumberColumn(string columnToken)
			=> LookupColumn(columnToken);

		/// <summary></summary>
		/// <param name="columnToken"></param>
		/// <returns></returns>
		protected abstract Tuple<string, Type> LookupColumn(string columnToken);

		/// <summary>Translate a column to is native name</summary>
		/// <param name="columnToken"></param>
		/// <returns></returns>
		public string GetNativeColumnName(string columnToken)
		{
			if (String.IsNullOrEmpty(columnToken))
				throw new ArgumentNullException(nameof(columnToken));

			var r = LookupColumn(columnToken);
			return r?.Item1;
		} // func LookupColumn

		#endregion

		#region -- CreateInFilter, CreateNotInFilter ----------------------------------

		/// <summary></summary>
		/// <param name="sb"></param>
		/// <param name="itemType"></param>
		/// <param name="values"></param>
		protected void CreateCompareInValues(StringBuilder sb, Type itemType, PpsDataFilterArrayValue values)
		{
			var first = true;
			foreach (var cur in values.Values)
			{
				if (first)
					first = false;
				else
					sb.Append(',');

				if (cur is PpsDataFilterFieldValue field)
				{
					var col = LookupColumn(field.FieldName);
					sb.Append(col.Item1);
				}
				else if (cur is PpsDataFilterTextValue text)
					sb.Append(CreateParsableValue(text.Text, itemType));
				else if (cur is PpsDataFilterIntegerValue num)
					sb.Append(CreateParsableValue(num.Value.ChangeType<string>(), itemType));
				else
					throw new NotImplementedException($"{cur.Type} is not supported.");
			}
		} // proc CreateCompareInValues

		/// <summary></summary>
		/// <param name="operand"></param>
		/// <param name="values"></param>
		/// <returns></returns>
		public override string CreateCompareIn(string operand, PpsDataFilterArrayValue values)
		{
			var sb = new StringBuilder();
			var col = LookupNumberColumn(operand);
			sb.Append(col.Item1)
				.Append(" IN (");
			CreateCompareInValues(sb, col.Item2, values);
			sb.Append(")");
			return sb.ToString();
		} // func CreateCompareIn

		/// <summary></summary>
		/// <param name="operand"></param>
		/// <param name="values"></param>
		/// <returns></returns>
		public override string CreateCompareNotIn(string operand, PpsDataFilterArrayValue values)
		{
			var sb = new StringBuilder();
			var col = LookupNumberColumn(operand);
			sb.Append("NOT ")
				.Append(col.Item1)
				.Append(" IN (");
			CreateCompareInValues(sb, col.Item2, values);
			sb.Append(")");
			return sb.ToString();
		} // func CreateCompareNotIn

		#endregion

		#region -- CreateLogicFilter --------------------------------------------------

		/// <summary></summary>
		/// <param name="method"></param>
		/// <param name="arguments"></param>
		/// <returns></returns>
		public override string CreateLogicFilter(PpsDataFilterExpressionType method, IEnumerable<string> arguments)
		{
			var sb = new StringBuilder();

			// initialize
			string op;
			switch (method)
			{
				case PpsDataFilterExpressionType.NAnd:
					sb.Append("NOT (");
					goto case PpsDataFilterExpressionType.And;
				case PpsDataFilterExpressionType.And:
					op = " AND ";
					break;
				case PpsDataFilterExpressionType.NOr:
					sb.Append("NOT (");
					goto case PpsDataFilterExpressionType.Or;
				case PpsDataFilterExpressionType.Or:
					op = " OR ";
					break;
				default:
					throw new InvalidOperationException();
			}

			// add arguments
			var first = true;
			foreach (var a in arguments)
			{
				if (first)
					first = false;
				else
					sb.Append(op);
				sb.Append('(').Append(a).Append(')');
			}

			if (first) // no arguments -> return a neutral expression
				return CreateTrueFilter();

			switch (method)
			{
				case PpsDataFilterExpressionType.NAnd:
				case PpsDataFilterExpressionType.NOr:
					sb.Append(')');
					break;
			}

			return sb.ToString();
		} // func CreateLogicFilter

		#endregion

		#region -- CreateNativeFilter -------------------------------------------------

		/// <summary></summary>
		/// <param name="expression"></param>
		/// <returns></returns>
		public override string CreateNativeFilter(PpsDataFilterNativeExpression expression)
		{
			var nativeExpression = LookupNativeExpression(expression.Key);
			return String.IsNullOrEmpty(nativeExpression)
				? CreateErrorFilter(String.Format("Native Expression not found '{0}'.", expression.Key))
				: "(" + nativeExpression + ")";
		} // func CreateNativeFilter

		/// <summary></summary>
		/// <param name="key"></param>
		/// <returns></returns>
		protected abstract string LookupNativeExpression(string key);

		#endregion
	} // class PpsDataFilterVisitorSql

	#endregion

	#region -- class PpsDataFilterVisitorLambda ---------------------------------------

	/// <summary>Creates a predicate from an filter expression.</summary>
	public abstract class PpsDataFilterVisitorLambda : PpsDataFilterVisitor<Expression>
	{
		#region -- class PpsDataFilterVisitorTyped ------------------------------------

		private sealed class PpsDataFilterVisitorTyped : PpsDataFilterVisitorLambda
		{
			public PpsDataFilterVisitorTyped(ParameterExpression rowParameter)
				: base(rowParameter)
			{
			} // ctor

			protected override Expression GetProperty(string memberName)
			{
				if (String.IsNullOrEmpty(memberName))
				{
					// create a concat of all string columns
					var type = CurrentRowParameter.Type;

					// build String.Join("\t", new string[] { } );
					var properties = from pi in type.GetRuntimeProperties()
									 where pi.PropertyType == typeof(string)
									 select Expression.Property(CurrentRowParameter, pi);
					if (properties.Any())
					{
						return Expression.Call(stringJoinMethodInfo, new Expression[]
							{
								Expression.Constant("\t"),
								Expression.NewArrayInit(typeof(string), properties)
							}
						);
					}
					else
						return Expression.Constant(String.Empty);
				}
				else // select spezific column
				{
					var propertyInfo = CurrentRowParameter.Type.GetRuntimeProperties()
							.Where(c => String.Compare(c.Name, memberName, StringComparison.OrdinalIgnoreCase) == 0)
							.FirstOrDefault()
						?? throw new ArgumentNullException(nameof(memberName), $"Property {memberName} not declared in type {CurrentRowParameter.Type.Name}.");
					return Expression.Property(CurrentRowParameter, propertyInfo);
				}
			} // func GetProperty
		} // class PpsDataFilterVisitorTyped

		#endregion

		private readonly ParameterExpression currentRowParameter;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary></summary>
		/// <param name="rowParameter"></param>
		public PpsDataFilterVisitorLambda(ParameterExpression rowParameter)
		{
			this.currentRowParameter = rowParameter ?? throw new ArgumentNullException(nameof(rowParameter));
		} // ctor

		#endregion

		#region -- CreateXXXXFilter ---------------------------------------------------

		private static Exception CreateCompareException(PpsDataFilterCompareExpression expression)
			=> new ArgumentOutOfRangeException(nameof(expression.Operator), expression.Operator, $"Operator '{expression.Operator}' is not defined for the value type '{expression.Value.Type}'.");

		private static ExpressionType GetBinaryExpressionType(PpsDataFilterCompareExpression expression)
		{
			switch (expression.Operator)
			{
				case PpsDataFilterCompareOperator.Contains:
				case PpsDataFilterCompareOperator.Equal:
					return ExpressionType.Equal;
				case PpsDataFilterCompareOperator.NotContains:
				case PpsDataFilterCompareOperator.NotEqual:
					return ExpressionType.NotEqual;
				case PpsDataFilterCompareOperator.Greater:
					return ExpressionType.GreaterThan;
				case PpsDataFilterCompareOperator.GreaterOrEqual:
					return ExpressionType.GreaterThanOrEqual;
				case PpsDataFilterCompareOperator.Lower:
					return ExpressionType.LessThan;
				case PpsDataFilterCompareOperator.LowerOrEqual:
					return ExpressionType.LessThanOrEqual;
				default:
					throw CreateCompareException(expression);
			}
		} // func GetBinaryExpressionType

		/// <summary>Helper for member convert</summary>
		/// <param name="expr"></param>
		/// <param name="typeTo"></param>
		/// <returns></returns>
		protected static Expression ConvertTo(Expression expr, Type typeTo)
		{
			if (typeTo == expr.Type)
				return expr;
			else if (typeTo.IsAssignableFrom(expr.Type))
				return Expression.Convert(expr, typeTo);
			else
				return Expression.Convert(Expression.Call(procsChangeTypeMethodInfo, Expression.Convert(expr, typeof(object)), Expression.Constant(typeTo)), typeTo);
		} // func ConvertTo

		private static Expression CreateCompareTextFilterStartsWith(Expression left, ConstantExpression right)
		{
			return Expression.Call(
				  Expression.Coalesce(ConvertTo(left, typeof(string)), Expression.Constant(String.Empty)), stringStartsWithMethodInfo,
					  ConvertTo(right, typeof(string)),
					  Expression.Constant(StringComparison.OrdinalIgnoreCase)
			  );
		} // func CreateCompareTextFilterStartsWith

		private static Expression CreateCompareTextFilterContains(ExpressionType expressionType, Expression left, Expression right)
		{
			return Expression.MakeBinary(expressionType,
				Expression.Call(
					Expression.Coalesce(ConvertTo(left, typeof(string)), Expression.Constant(String.Empty)), stringIndexOfMethodInfo,
						ConvertTo(right, typeof(string)),
						Expression.Constant(StringComparison.OrdinalIgnoreCase)
				),
				Expression.Constant(0)
			);
		} // func CreateCompareTextFilterContains

		private static Expression CreateCompareTextFilterCompare(ExpressionType expressionType, Expression left, Expression right)
		{
			return Expression.MakeBinary(expressionType,
				  Expression.Call(stringCompareMethodInfo,
						  ConvertTo(left, typeof(string)),
						  ConvertTo(right, typeof(string)),
						  Expression.Constant(StringComparison.OrdinalIgnoreCase)
					  ),
					  Expression.Constant(0)
				  );
		} // func CreateCompareTextFilterCompare

		private static Expression CreateCompareFilterForTextProperty(PpsDataFilterCompareExpression expression, Expression left, Expression right)
		{
			switch (expression.Operator)
			{
				case PpsDataFilterCompareOperator.Contains:
					return CreateCompareTextFilterContains(ExpressionType.GreaterThanOrEqual, left, right);
				case PpsDataFilterCompareOperator.NotContains:
					return CreateCompareTextFilterContains(ExpressionType.LessThan, left, right);
				default:
					return CreateCompareTextFilterCompare(GetBinaryExpressionType(expression), left, right);
			}
		} // func CreateCompareFilterForTextProperty

		private Expression CreateCompareFilterForProperty(PpsDataFilterCompareExpression expression, Expression left)
		{
			// right site depends of the operator
			switch (expression.Value.Type)
			{
				case PpsDataFilterValueType.Field:
					{
						var right = GetProperty(((PpsDataFilterFieldValue)expression.Value).FieldName);
						if (left.Type == typeof(string))
							return CreateCompareFilterForTextProperty(expression, left, right);
						else
							return Expression.MakeBinary(GetBinaryExpressionType(expression), ConvertTo(left, typeof(long)), right);
					}
				case PpsDataFilterValueType.Text:
					return CreateCompareFilterForTextProperty(expression, left, Expression.Constant(((PpsDataFilterTextValue)expression.Value).Text));
				case PpsDataFilterValueType.Integer:
					{
						var right = Expression.Constant(((PpsDataFilterIntegerValue)expression.Value).Value);
						return Expression.MakeBinary(GetBinaryExpressionType(expression), ConvertTo(left, typeof(long)), right);
					}
				case PpsDataFilterValueType.Decimal:
					{
						var right = Expression.Constant(((PpsDataFilterIntegerValue)expression.Value).Value);
						return Expression.MakeBinary(GetBinaryExpressionType(expression), ConvertTo(left, typeof(decimal)), right);
					}
				case PpsDataFilterValueType.Number:
					{
						var right = Expression.Constant(((PpsDataFilterTextKeyValue)expression.Value).Text);
						switch (expression.Operator)
						{
							case PpsDataFilterCompareOperator.Contains:
								return CreateCompareTextFilterStartsWith(left, right);
							case PpsDataFilterCompareOperator.NotContains:
								return Expression.Not(CreateCompareTextFilterStartsWith(left, right));

							default:
								return CreateCompareTextFilterCompare(GetBinaryExpressionType(expression), left, right);
						}

						throw new NotImplementedException();
					}
				case PpsDataFilterValueType.Null:
					{
						switch (expression.Operator)
						{
							case PpsDataFilterCompareOperator.Contains:
							case PpsDataFilterCompareOperator.Equal:
								return Expression.MakeBinary(ExpressionType.Equal, left, Expression.Default(left.Type));
							case PpsDataFilterCompareOperator.NotContains:
							case PpsDataFilterCompareOperator.NotEqual:
								return Expression.MakeBinary(ExpressionType.NotEqual, left, Expression.Default(left.Type));
							case PpsDataFilterCompareOperator.Greater:
							case PpsDataFilterCompareOperator.GreaterOrEqual:
							case PpsDataFilterCompareOperator.Lower:
							case PpsDataFilterCompareOperator.LowerOrEqual:
								return Expression.Constant(false);
							default:
								throw CreateCompareException(expression);
						}

						throw new NotImplementedException();
					}
				case PpsDataFilterValueType.Date:
					{
						var a = (PpsDataFilterDateTimeValue)expression.Value;
						left = Expression.Convert(left, typeof(DateTime));
						switch (expression.Operator)
						{
							case PpsDataFilterCompareOperator.Contains:
							case PpsDataFilterCompareOperator.Equal:
								return Expression.AndAlso(Expression.MakeBinary(ExpressionType.GreaterThanOrEqual, left, Expression.Constant(a.From)), Expression.MakeBinary(ExpressionType.LessThanOrEqual, left, Expression.Constant(a.To)));
							case PpsDataFilterCompareOperator.NotContains:
							case PpsDataFilterCompareOperator.NotEqual:
								return Expression.AndAlso(Expression.MakeBinary(ExpressionType.LessThan, left, Expression.Constant(a.From)), Expression.MakeBinary(ExpressionType.GreaterThan, left, Expression.Constant(a.To)));
							case PpsDataFilterCompareOperator.Greater:
								return Expression.MakeBinary(ExpressionType.GreaterThan, left, Expression.Constant(a.To));
							case PpsDataFilterCompareOperator.GreaterOrEqual:
								return Expression.MakeBinary(ExpressionType.GreaterThanOrEqual, left, Expression.Constant(a.From));
							case PpsDataFilterCompareOperator.Lower:
								return Expression.MakeBinary(ExpressionType.LessThan, left, Expression.Constant(a.From));
							case PpsDataFilterCompareOperator.LowerOrEqual:
								return Expression.MakeBinary(ExpressionType.LessThanOrEqual, left, Expression.Constant(a.To));
							default:
								throw CreateCompareException(expression);
						}

						throw new NotImplementedException();
					}
				default:
					throw CreateCompareException(expression);
			}
		} // func CreateCompareFilterForProperty

		/// <summary></summary>
		/// <param name="expression"></param>
		/// <param name="expressionValue"></param>
		/// <returns></returns>
		protected virtual Expression CreateCompareFilterFullText(PpsDataFilterCompareExpression expression, string expressionValue)
		{
			var type = CurrentRowParameter.Type;
			var typeCompareInterface = type.GetInterfaces().FirstOrDefault(i => i == typeof(ICompareFulltext));
			if (typeCompareInterface != null)
			{
				var expr = (Expression)Expression.Call(
					Expression.Convert(CurrentRowParameter, typeof(ICompareFulltext)), compareFullTextSearchTextMethodInfo,
						Expression.Constant(expressionValue),
						Expression.Constant(expression.Value.Type == PpsDataFilterValueType.Number)
				);

				if (expression.Operator == PpsDataFilterCompareOperator.NotContains
					|| expression.Operator == PpsDataFilterCompareOperator.NotEqual)
					expr = Expression.Not(expr);

				return expr;
			}
			else
			{
				var left = GetProperty(null);
				if (left == null)
					return Expression.Constant(false);

				return CreateCompareFilterForProperty(expression, left);
			}
		} // func CreateCompareFilterFullText

		/// <summary></summary>
		/// <param name="expression"></param>
		/// <returns></returns>
		protected virtual Expression CreateCompareFilterFullDate(PpsDataFilterCompareExpression expression)
		{
			var type = CurrentRowParameter.Type;
			var typeCompareInterface = type.GetInterfaces().FirstOrDefault(i => i == typeof(ICompareDateTime));
			if (typeCompareInterface != null)
			{
				var expr = (Expression)Expression.Call(
					Expression.Convert(CurrentRowParameter, typeof(ICompareDateTime)), compareDateTimeSearchDateMethodInfo,
						Expression.Constant(((PpsDataFilterDateTimeValue)expression.Value).From),
						Expression.Constant(((PpsDataFilterDateTimeValue)expression.Value).To)
				);

				if (expression.Operator == PpsDataFilterCompareOperator.NotContains
					|| expression.Operator == PpsDataFilterCompareOperator.NotEqual)
					expr = Expression.Not(expr);

				return expr;
			}
			else
			{
				var left = GetProperty(null);
				if (left == null)
					return Expression.Constant(false);

				return CreateCompareFilterForProperty(expression, left);
			}
		} // func CreateCompareFilterFullDate

		/// <summary></summary>
		/// <param name="expression"></param>
		/// <returns></returns>
		public sealed override Expression CreateCompareFilter(PpsDataFilterCompareExpression expression)
		{
			if (String.IsNullOrEmpty(expression.Operand)) // compare over all fields
			{
				switch (expression.Value.Type)
				{
					case PpsDataFilterValueType.Text:
						return CreateCompareFilterFullText(expression, ((PpsDataFilterTextValue)expression.Value).Text);
					case PpsDataFilterValueType.Number:
						return CreateCompareFilterFullText(expression, ((PpsDataFilterTextKeyValue)expression.Value).Text);

					case PpsDataFilterValueType.Date:
						return CreateCompareFilterFullDate(expression);

					default:
						throw CreateCompareException(expression);
				}
			}
			else // compare a explicit column
			{
				// left site
				var left = GetProperty(expression.Operand);
				if (left == null)
					return Expression.Constant(false); // property does not exist

				return CreateCompareFilterForProperty(expression, left);
			}
		} // func CreateCompareFilter

		private Expression CreateArrayFind(NewArrayExpression arrayExpression, Expression propertyExpression)
		{
			var existsMethod = arrayExistsethodInfo.MakeGenericMethod(propertyExpression.Type);
			var predicateType = typeof(Predicate<>).MakeGenericType(propertyExpression.Type);
			var objParameter = Expression.Parameter(propertyExpression.Type, "obj");
			var body = propertyExpression.Type == typeof(string)
				? CreateCompareTextFilterCompare(ExpressionType.Equal, propertyExpression, objParameter)
				: Expression.MakeBinary(ExpressionType.Equal, propertyExpression, objParameter);
			var predicateExpression = Expression.Lambda(predicateType,
				body,
				objParameter
			);
			return Expression.Call(existsMethod, arrayExpression, predicateExpression);
		} // func CreateArrayFind

		/// <summary></summary>
		/// <param name="operand"></param>
		/// <param name="values"></param>
		/// <returns></returns>
		public override Expression CreateCompareIn(string operand, PpsDataFilterArrayValue values)
		{
			var prop = GetProperty(operand);
			if (values.ItemType == PpsDataFilterValueType.Integer)
			{
				var arrExpr = Expression.NewArrayInit(prop.Type,
					values.Values.Select(c => Expression.Constant(Procs.ChangeType(((PpsDataFilterIntegerValue)c).Value, prop.Type), prop.Type))
				);
				return CreateArrayFind(arrExpr, prop);
			}
			else if (values.ItemType == PpsDataFilterValueType.Text)
			{
				var arrExpr = Expression.NewArrayInit(prop.Type,
					values.Values.Select(c => Expression.Constant(Procs.ChangeType(((PpsDataFilterTextValue)c).Text, prop.Type), prop.Type))
				);
				return CreateArrayFind(arrExpr, prop);
			}
			else
			{
				Expression expr = null;
				foreach (var cur in values.Values)
				{
					var c = CreateCompareFilterForProperty(new PpsDataFilterCompareExpression(operand, PpsDataFilterCompareOperator.Equal, cur), prop);
					expr = expr == null ? c : Expression.Or(expr, c);
				}
				return expr;
			}
		} // func CreateCompareIn

		/// <summary></summary>
		/// <param name="operand"></param>
		/// <param name="values"></param>
		/// <returns></returns>
		public sealed override Expression CreateCompareNotIn(string operand, PpsDataFilterArrayValue values)
			=> Expression.Not(CreateCompareIn(operand, values));

		/// <summary></summary>
		/// <param name="method"></param>
		/// <param name="arguments"></param>
		/// <returns></returns>
		public sealed override Expression CreateLogicFilter(PpsDataFilterExpressionType method, IEnumerable<Expression> arguments)
		{
			var expr = (Expression)null;
			bool negResult;
			ExpressionType type;
			switch (method)
			{
				case PpsDataFilterExpressionType.And:
					type = ExpressionType.AndAlso;
					negResult = false;
					break;
				case PpsDataFilterExpressionType.NAnd:
					type = ExpressionType.AndAlso;
					negResult = true;
					break;

				case PpsDataFilterExpressionType.Or:
					type = ExpressionType.OrElse;
					negResult = false;
					break;
				case PpsDataFilterExpressionType.NOr:
					type = ExpressionType.OrElse;
					negResult = true;
					break;

				default:
					throw new ArgumentOutOfRangeException(nameof(method), method, "Invalid operation.");
			}

			foreach (var a in arguments)
			{
				if (expr == null)
					expr = a;
				else
					expr = Expression.MakeBinary(type, expr, a);
			}

			if (negResult)
				expr = Expression.Not(expr);

			return expr;
		} // func CreateLogicFilter

		/// <summary></summary>
		/// <param name="expression"></param>
		/// <returns></returns>
		public sealed override Expression CreateNativeFilter(PpsDataFilterNativeExpression expression)
			=> throw new NotSupportedException();

		/// <summary></summary>
		/// <returns></returns>
		public sealed override Expression CreateTrueFilter()
			=> Expression.Constant(true);

		#endregion

		/// <summary>Get the requested property.</summary>
		/// <param name="memberName">Name of the property or an empty string, for a full text seach</param>
		/// <returns>Should return a expression or a property with a comparable value.</returns>
		protected abstract Expression GetProperty(string memberName);

		/// <summary>Row place holder</summary>
		public ParameterExpression CurrentRowParameter => currentRowParameter;

		private static readonly MethodInfo stringIndexOfMethodInfo;
		private static readonly MethodInfo stringCompareMethodInfo;
		private static readonly MethodInfo stringStartsWithMethodInfo;
		private static readonly MethodInfo stringJoinMethodInfo;
		private static readonly MethodInfo procsChangeTypeMethodInfo;
		private static readonly MethodInfo compareFullTextSearchTextMethodInfo;
		private static readonly MethodInfo compareDateTimeSearchDateMethodInfo;
		private static readonly MethodInfo arrayExistsethodInfo;

		static PpsDataFilterVisitorLambda()
		{
			stringIndexOfMethodInfo = typeof(string).GetMethod(nameof(String.IndexOf), new Type[] { typeof(string), typeof(StringComparison) }) ?? throw new ArgumentNullException("String.IndexOf");
			stringCompareMethodInfo = typeof(string).GetMethod(nameof(String.Compare), new Type[] { typeof(string), typeof(string), typeof(StringComparison) }) ?? throw new ArgumentNullException("String.Compare");
			stringStartsWithMethodInfo = typeof(string).GetMethod(nameof(String.StartsWith), new Type[] { typeof(string), typeof(StringComparison) }) ?? throw new ArgumentNullException("String.StartsWith");
			stringJoinMethodInfo = typeof(string).GetMethod(nameof(String.Join), new Type[] { typeof(string), typeof(string[]) }) ?? throw new ArgumentNullException("String.Join");

			procsChangeTypeMethodInfo = typeof(Procs).GetMethod(nameof(Procs.ChangeType), new Type[] { typeof(object), typeof(Type) }) ?? throw new ArgumentNullException("Procs.ChangeType");

			compareFullTextSearchTextMethodInfo = typeof(ICompareFulltext).GetMethod(nameof(ICompareFulltext.SearchText), new Type[] { typeof(string), typeof(bool) }) ?? throw new ArgumentNullException("ICompareFulltext.SearchText");
			compareDateTimeSearchDateMethodInfo = typeof(ICompareDateTime).GetMethod(nameof(ICompareDateTime.SearchDate), new Type[] { typeof(DateTime), typeof(DateTime) }) ?? throw new ArgumentNullException("ICompareDateTime.SearchDate");

			arrayExistsethodInfo = typeof(Array).GetMethod(nameof(Array.Exists)) ?? throw new ArgumentNullException("Array.Exists");
		} // stor

		/// <summary></summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="expression"></param>
		/// <returns></returns>
		public static Predicate<T> CompileTypedFilter<T>(PpsDataFilterExpression expression)
		{
			var filterVisitor = new PpsDataFilterVisitorTyped(Expression.Parameter(typeof(T)));
			var filterExpr = filterVisitor.CreateFilter(expression);
			return Expression.Lambda<Predicate<T>>(filterExpr, filterVisitor.CurrentRowParameter).Compile();
		} // func CompileTypedFilter
	} // class PpsDataFilterVisitorLambda

	#endregion

	#region -- class PpsDataFilterVisitorDataRow --------------------------------------

	/// <summary></summary>
	public sealed class PpsDataFilterVisitorDataRow : PpsDataFilterVisitorLambda
	{
		private readonly IDataColumns columns;

		/// <summary></summary>
		/// <param name="rowParameter"></param>
		/// <param name="columns"></param>
		public PpsDataFilterVisitorDataRow(ParameterExpression rowParameter, IDataColumns columns = null)
			: base(rowParameter)
		{
			if (rowParameter.Type != typeof(IDataRow))
				throw new ArgumentOutOfRangeException(nameof(rowParameter));

			this.columns = columns;
		} // ctor

		private Expression CreateCompareFilterFullOperator(PpsDataFilterCompareExpression expression, Expression expr)
		{
			if (expression.Operator == PpsDataFilterCompareOperator.NotContains
				|| expression.Operator == PpsDataFilterCompareOperator.NotEqual)
				expr = Expression.Not(expr);
			return expr;
		} // func CreateCompareFilterFullOperator

		/// <summary></summary>
		/// <param name="expression"></param>
		/// <param name="expressionValue"></param>
		/// <returns></returns>
		protected override Expression CreateCompareFilterFullText(PpsDataFilterCompareExpression expression, string expressionValue)
			=> CreateCompareFilterFullOperator(expression,
				Expression.Call(datarowSearchFullTextMethodInfo,
					Expression.Convert(CurrentRowParameter, typeof(IDataRow)),
					Expression.Constant(expressionValue),
					Expression.Constant(expression.Value.Type == PpsDataFilterValueType.Number)
				)
			);

		/// <summary></summary>
		/// <param name="expression"></param>
		/// <returns></returns>
		protected override Expression CreateCompareFilterFullDate(PpsDataFilterCompareExpression expression)
			=> CreateCompareFilterFullOperator(expression,
				Expression.Call(datarowSearchFullDateMethodInfo,
					Expression.Convert(CurrentRowParameter, typeof(IDataRow)),
					Expression.Constant(((PpsDataFilterDateTimeValue)expression.Value).From),
					Expression.Constant(((PpsDataFilterDateTimeValue)expression.Value).To)
				)
			);

		/// <summary></summary>
		/// <param name="memberName"></param>
		/// <returns></returns>
		protected override Expression GetProperty(string memberName)
		{
			if (String.IsNullOrEmpty(memberName))
				throw new ArgumentNullException(nameof(memberName));

			if (columns == null)
			{
				return Expression.MakeIndex(CurrentRowParameter, dataRowIndexNamePropertyInfo,
					new Expression[]
					{
						Expression.Constant(memberName),
						Expression.Constant(false)
					}
				);
			}
			else
			{
				var index = columns.FindColumnIndex(memberName);
				return index >= 0
					? Expression.MakeIndex(CurrentRowParameter, dataRowIndexIntPropertyInfo, new Expression[] { Expression.Constant(index) })
					: null;
			}
		} // func GetProperty

		private static readonly PropertyInfo dataRowIndexIntPropertyInfo;
		private static readonly PropertyInfo dataRowIndexNamePropertyInfo;
		private static readonly MethodInfo datarowSearchFullTextMethodInfo;
		private static readonly MethodInfo datarowSearchFullDateMethodInfo;

		static PpsDataFilterVisitorDataRow()
		{
			dataRowIndexNamePropertyInfo = typeof(IDataRow).GetRuntimeProperties().Where(
				c =>
				{
					if (c.Name == "Item")
					{
						var pi = c.GetIndexParameters();
						return pi.Length == 2 && pi[0].ParameterType == typeof(string) && pi[1].ParameterType == typeof(bool);
					}
					else
						return false;
				}
			).FirstOrDefault() ?? throw new ArgumentException();


			dataRowIndexIntPropertyInfo = typeof(IDataValues).GetRuntimeProperties().Where(
				c =>
				{
					if (c.Name == "Item")
					{
						var pi = c.GetIndexParameters();
						return pi.Length == 1 && pi[0].ParameterType == typeof(int);
					}
					else
						return false;
				}
			).FirstOrDefault() ?? throw new ArgumentException();

			datarowSearchFullTextMethodInfo = typeof(PpsDataFilterVisitorDataRow).GetMethod(nameof(PpsDataFilterVisitorDataRow.RtDataRowSearchFullText)) ?? throw new ArgumentNullException("PpsDataFilterVisitorDataRow.RtDataRowSearchFullText");
			datarowSearchFullDateMethodInfo = typeof(PpsDataFilterVisitorDataRow).GetMethod(nameof(PpsDataFilterVisitorDataRow.RtDataRowSearchFullDate)) ?? throw new ArgumentNullException("PpsDataFilterVisitorDataRow.RtDataRowSearchFullDate");
		} // ctor

		/// <summary>Only for internal use.</summary>
		/// <param name="row"></param>
		/// <param name="text"></param>
		/// <param name="startsWith"></param>
		/// <returns></returns>
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static bool RtDataRowSearchFullText(IDataRow row, string text, bool startsWith)
			=> RtDataRowSearchFullText(row, text, startsWith, 0);

		private static bool RtDataRowSearchFullText(IDataRow row, string text, bool startsWith, int level)
		{
			if (row == null)
				return false;

			for (var i = 0; i < row.Columns.Count; i++)
			{
				if (level < 1 && row is IDataValues2 r && r.TryGetRelatedDataRow(i, out var relatedRow))
				{
					if (RtDataRowSearchFullText(relatedRow, text, startsWith, level + 1))
						return true;
				}
				else
				{
					var v = row[i];
					if (v == null)
						continue;

					var s = v.ChangeType<string>();
					if (startsWith && s.StartsWith(text, StringComparison.CurrentCultureIgnoreCase))
						return true;
					else if (!startsWith && s.IndexOf(text, StringComparison.CurrentCultureIgnoreCase) >= 0)
						return true;
				}
			}

			return false;
		} // func RtDataRowSearchFullText

		/// <summary>Only for internal use.</summary>
		/// <param name="row"></param>
		/// <param name="from"></param>
		/// <param name="to"></param>
		/// <returns></returns>
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static bool RtDataRowSearchFullDate(IDataRow row, DateTime from, DateTime to)
		{
			for (var i = 0; i < row.Columns.Count; i++)
			{
				if (row.Columns[i].DataType != typeof(DateTime)
					|| row[i] == null)
					continue;

				var dt = (DateTime)row[i];
				if (from <= dt && dt <= to)
					return true;
			}

			return false;
		} // func RtDataRowSearchFullDate

		/// <summary>Create a DataRow filter function.</summary>
		/// <param name="expression"></param>
		/// <returns></returns>
		public static Predicate<T> CreateDataRowFilter<T>(PpsDataFilterExpression expression)
			where T : class
		{
			var currentParameter = Expression.Parameter(typeof(T), "#current");
			var rowParameter = Expression.Variable(typeof(IDataRow), "#row");
			var filterExpr = new PpsDataFilterVisitorDataRow(rowParameter).CreateFilter(expression);

			var predicateExpr = Expression.Lambda<Predicate<T>>(
				Expression.Block(typeof(bool),
					new ParameterExpression[] { rowParameter },
					Expression.Assign(rowParameter, Expression.Convert(currentParameter, typeof(IDataRow))),
					filterExpr
				),
				currentParameter
			);

			return predicateExpr.Compile();
		} // func CreateDataRowFilter
	} // class PpsDataFilterVisitorLambda

	#endregion

	#region -- class PpsDataOrderExpression -------------------------------------------

	/// <summary></summary>
	public sealed class PpsDataOrderExpression : IEquatable<PpsDataOrderExpression>
	{
		private readonly bool negate;
		private readonly string identifier;

		/// <summary></summary>
		/// <param name="negate"></param>
		/// <param name="identifier"></param>
		public PpsDataOrderExpression(bool negate, string identifier)
		{
			this.negate = negate;
			this.identifier = identifier;
		} // ctor

		/// <summary></summary>
		/// <returns></returns>
		public override int GetHashCode()
			=> identifier.GetHashCode() ^ negate.GetHashCode();

		/// <summary></summary>
		/// <param name="obj"></param>
		/// <returns></returns>
		public override bool Equals(object obj)
			=> obj is PpsDataOrderExpression o ? Equals(o) : base.Equals(obj);

		/// <summary></summary>
		/// <param name="other"></param>
		/// <returns></returns>
		public bool Equals(PpsDataOrderExpression other)
			=> other.identifier.Equals(identifier) && other.negate == negate;

		/// <summary></summary>
		/// <returns></returns>
		public override string ToString()
			=> (negate ? "-" : "+") + identifier;

		/// <summary></summary>
		public bool Negate => negate;
		/// <summary></summary>
		public string Identifier => identifier;

		// -- Static --------------------------------------------------------------

		/// <summary></summary>
		/// <param name="order"></param>
		/// <returns></returns>
		public static IEnumerable<PpsDataOrderExpression> Parse(string order)
		{
			if (String.IsNullOrEmpty(order))
				yield break;

			var orderTokens = order.Split(',', ' ');
			foreach (var _tok in orderTokens)
			{
				if (String.IsNullOrEmpty(_tok))
					continue;

				var tok = _tok.Trim();
				var neg = false;
				if (tok[0] == '+')
				{
					tok = tok.Substring(1);
				}
				else if (tok[0] == '-')
				{
					neg = true;
					tok = tok.Substring(1);
				}

				// try find predefined order
				yield return new PpsDataOrderExpression(neg, tok);
			}
		} // func Parse

		/// <summary></summary>
		/// <param name="order"></param>
		/// <param name="throwException"></param>
		/// <returns></returns>
		public static IEnumerable<PpsDataOrderExpression> Parse(object order, bool throwException = true)
		{
			switch (order)
			{
				case null:
					return Array.Empty<PpsDataOrderExpression>();
				case string orderString:
					return Parse(orderString);
				default:
					if (throwException)
						throw new ArgumentException(nameof(order));
					else
						return null;
			}
		} // func Parse

		/// <summary></summary>
		/// <param name="expr"></param>
		/// <returns></returns>
		public static IEnumerable<PpsDataOrderExpression> Combine(params IEnumerable<PpsDataOrderExpression>[] expr)
		{
			if (expr == null || expr.Length == 0)
				return Empty;

			var i = 0;
			var r = expr[i];
			while (++i < expr.Length)
				r = Enumerable.Union(r, expr[i] ?? Empty, CompareIdentifier);

			return r;
		} // func Combine

		/// <summary></summary>
		/// <param name="orders"></param>
		/// <returns></returns>
		public static string ToString(IEnumerable<PpsDataOrderExpression> orders)
			=> IsEmpty(orders) ? null : String.Join(",", from o in orders select (o.Negate ? "-" : "+") + o.Identifier);

		/// <summary></summary>
		/// <param name="order"></param>
		/// <returns></returns>
		public static bool IsEmpty(IEnumerable<PpsDataOrderExpression> order)
			=> order == null || (order is PpsDataOrderExpression[] a && a.Length == 0);

		private class CompareIdentifierImpl : IEqualityComparer<PpsDataOrderExpression>
		{
			public bool Equals(PpsDataOrderExpression x, PpsDataOrderExpression y)
				=> String.Compare(x.identifier, y.identifier, StringComparison.OrdinalIgnoreCase) == 0;

			public int GetHashCode(PpsDataOrderExpression obj)
				=> obj.identifier.ToLower().GetHashCode();
		} // class CompareIdentifierImpl

		/// <summary></summary>
		public static IEqualityComparer<PpsDataOrderExpression> CompareIdentifier { get; } = new CompareIdentifierImpl();

		/// <summary></summary>
		public static PpsDataOrderExpression[] Empty { get; } = Array.Empty<PpsDataOrderExpression>();
	} // class PpsDataOrderExpression

	#endregion

	#region -- class PpsDataColumnExpression ------------------------------------------

	/// <summary></summary>
	public sealed class PpsDataColumnExpression
	{
		private readonly PpsDataFilterFieldValue column;
		private readonly string columnAlias;

		/// <summary></summary>
		/// <param name="columnName"></param>
		/// <param name="columnAlias"></param>
		public PpsDataColumnExpression(string columnName, string columnAlias = null)
		{
			this.column = new PpsDataFilterFieldValue(columnName ?? throw new ArgumentNullException(nameof(columnName)));
			if (columnAlias == null)
			{
				ParseQualifiedName(columnName, out var tableAlias, out var name);
				this.columnAlias = tableAlias == null ? null : tableAlias + name;
			}
			else
				this.columnAlias = columnAlias;
		} // ctor

		/// <summary>String from column expression.</summary>
		/// <returns></returns>
		public override string ToString()
			=> String.IsNullOrEmpty(columnAlias)
				? column.FieldName
				: column.FieldName + ":" + columnAlias;

		/// <summary></summary>
		public string Name => column.FieldName;
		/// <summary></summary>
		public string Alias => columnAlias ?? column.FieldName;

		/// <summary></summary>
		public bool HasAlias => !String.IsNullOrEmpty(columnAlias);

		// -- Static ----------------------------------------------------------

		private static PpsDataColumnExpression CreateStringKeyValuePair(object value)
		{
			switch (value)
			{
				case string str:
					var p = str.IndexOfAny(new char[] { '=', ':' });
					return p == -1
						? new PpsDataColumnExpression(str.Trim())
						: new PpsDataColumnExpression(str.Substring(0, p).Trim(), str.Substring(p + 1).Trim());
				default:
					return CreateStringKeyValuePair(value.ToString());
			}
		} // func CreateStringKeyValuePair

		/// <summary>Split name into alias and name</summary>
		/// <param name="columnName"></param>
		/// <param name="table"></param>
		/// <param name="name"></param>
		public static void ParseQualifiedName(string columnName, out string table, out string name)
		{
			var p = columnName.IndexOf('.');
			if (p >= 0)
			{
				table = columnName.Substring(0, p);
				name = columnName.Substring(p + 1);
			}
			else
			{
				table = null;
				name = columnName;
			}
		} // proc ParseQualifiedName

		/// <summary></summary>
		/// <param name="columns"></param>
		/// <returns></returns>
		public static IEnumerable<PpsDataColumnExpression> Parse(string columns)
			=> String.IsNullOrEmpty(columns)
				? Array.Empty<PpsDataColumnExpression>()
				: columns.Split(',').Where(s => !String.IsNullOrEmpty(s)).Select(CreateStringKeyValuePair).ToArray();

		/// <summary></summary>
		/// <param name="columns"></param>
		/// <returns></returns>
		public static IEnumerable<PpsDataColumnExpression> Parse(LuaTable columns)
			=> columns.ArrayList.Select(CreateStringKeyValuePair)
				.Union(columns.Members.Select(kv => new PpsDataColumnExpression(kv.Key, kv.Value.ToString())))
				.ToArray();

		/// <summary></summary>
		/// <param name="columns"></param>
		/// <param name="throwException"></param>
		/// <returns></returns>
		public static IEnumerable<PpsDataColumnExpression> Parse(object columns, bool throwException = true)
		{
			switch (columns)
			{
				case null:
					return Array.Empty<PpsDataColumnExpression>();
				case string columnsString:
					return Parse(columnsString);
				case LuaTable columnsArray:
					return Parse(columnsArray);
				default:
					if (throwException)
						throw new ArgumentException(nameof(columns));
					else
						return null;
			}
		} // func Parse

		/// <summary>String from columns</summary>
		/// <param name="columns"></param>
		/// <returns></returns>
		public static string ToString(IEnumerable<PpsDataColumnExpression> columns)
			=> IsEmpty(columns) ? null : String.Join(",", columns.Select(c => c.ToString()));

		/// <summary></summary>
		/// <param name="columns"></param>
		/// <returns></returns>
		public static bool IsEmpty(IEnumerable<PpsDataColumnExpression> columns)
			=> columns == null || (columns is PpsDataColumnExpression[] a && a.Length == 0);

		/// <summary></summary>
		public static PpsDataColumnExpression[] Empty { get; } = Array.Empty<PpsDataColumnExpression>();
	} // class PpsDataColumnExpression

	#endregion
}
