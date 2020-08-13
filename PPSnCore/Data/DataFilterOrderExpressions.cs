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
using System.ComponentModel.Design.Serialization;
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

	/// <summary></summary>
	public enum PpsDataFilterCompareOperator
	{
		/// <summary></summary>
		Contains,
		/// <summary></summary>
		NotContains,
		/// <summary></summary>
		Equal,
		/// <summary></summary>
		NotEqual,
		/// <summary></summary>
		Greater,
		/// <summary></summary>
		GreaterOrEqual,
		/// <summary></summary>
		Lower,
		/// <summary></summary>
		LowerOrEqual
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
		public PpsDataFilterExpression(PpsDataFilterExpressionType method)
		{
			Type = method;
		} // ctor

		/// <summary></summary>
		public virtual PpsDataFilterExpression Reduce()
			=> this;

		/// <summary></summary>
		/// <param name="sb"></param>
		public abstract void ToString(StringBuilder sb);

		/// <summary></summary>
		public override string ToString()
		{
			var sb = new StringBuilder();
			ToString(sb);
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

		private static void SkipWhiteSpaces(string filterExpression, ref int offset)
		{
			while (offset < filterExpression.Length && Char.IsWhiteSpace(filterExpression[offset]))
				offset++;
		} // func SkipWhiteSpaces

		private static void ParseIdentifier(string filterExpression, ref int offset)
		{
			while (offset < filterExpression.Length && IsLetterOrDigit(filterExpression[offset]))
				offset++;
		} // func ParseIdentifier

		private static string ParseEscaped(string filterExpression, char quote, ref int offset)
		{
			var sb = new StringBuilder();
			var escape = false;
			offset++;
			while (offset < filterExpression.Length)
			{
				var c = filterExpression[offset];
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
		} // func ParseEscaped

		/// <summary></summary>
		/// <param name="expression"></param>
		/// <param name="allowFields"></param>
		/// <param name="variables"></param>
		/// <returns></returns>
		public static PpsDataFilterCompareValue ParseCompareValue(string expression, bool allowFields = true, IPropertyReadOnlyDictionary variables = null)
		{
			var offset = 0;
			if (EatExpressionCharacter(expression, ref offset, '('))
				return new PpsDataFilterCompareArrayValue(ParseCompareValues(expression, allowFields, variables, ref offset));
			else
				return ParseCompareValue(expression, allowFields, variables, ref offset);
		} // func ParseCompareValue

		private static PpsDataFilterCompareValue ParseCompareValue(string expression, bool allowFields, IPropertyReadOnlyDictionary variables, ref int offset)
		{
			PpsDataFilterCompareValue value;

			if (offset >= expression.Length || Char.IsWhiteSpace(expression[offset]))
			{
				value = PpsDataFilterCompareNullValue.Default;
			}
			else if (expression[offset] == '"' || expression[offset] == '\'')
			{
				var text = ParseEscaped(expression, expression[offset], ref offset);
				value = String.IsNullOrEmpty(text) ? PpsDataFilterCompareNullValue.Default : new PpsDataFilterCompareTextValue(text);
			}
			else if (expression[offset] == '#')
			{
				offset++;
				var startAt2 = offset;
				while (offset < expression.Length && (!Char.IsWhiteSpace(expression[offset]) && expression[offset] != '#'))
					offset++;

				if (TestExpressionCharacter(expression, offset, '#')) // date filter
				{
					offset++;
					value = PpsDataFilterCompareDateValue.Create(expression, startAt2, offset - startAt2 - 1);
				}
				else if (startAt2 < offset) // Number filter
					value = new PpsDataFilterCompareNumberValue(expression.Substring(startAt2, offset - startAt2));
				else // null
					value = PpsDataFilterCompareNullValue.Default;
			}
			else if (variables != null && expression[offset] == '$')
			{
				offset++;
				var startAt2 = offset;
				while (offset < expression.Length && (Char.IsLetterOrDigit(expression[offset]) || expression[offset] == '_'))
					offset++;

				if (variables.TryGetProperty(expression.Substring(startAt2, offset - startAt2), out var tmp))
				{
					if (tmp == null)
						value = PpsDataFilterCompareNullValue.Default;
					else if (tmp is DateTime dt)
						value = new PpsDataFilterCompareDateValue(dt.Date, dt.Date.AddDays(1));
					else if (tmp is long i64)
						value = new PpsDataFilterCompareIntegerValue(i64);
					else if (tmp is int i32)
						value = new PpsDataFilterCompareIntegerValue(i32);
					else if (tmp is short i16)
						value = new PpsDataFilterCompareIntegerValue(i16);
					else if (tmp is sbyte i8)
						value = new PpsDataFilterCompareIntegerValue(i8);
					else if (tmp is uint ui32)
						value = new PpsDataFilterCompareIntegerValue(ui32);
					else if (tmp is ushort ui16)
						value = new PpsDataFilterCompareIntegerValue(ui16);
					else if (tmp is byte ui8)
						value = new PpsDataFilterCompareIntegerValue(ui8);
					else if (tmp is string str)
						value = new PpsDataFilterCompareTextValue(str);
					else
					{
						value = new PpsDataFilterCompareTextValue(tmp.ChangeType<string>());
					}
				}
				else // generate error?
					value = PpsDataFilterCompareNullValue.Default;
			}
			else
			{
				var startAt2 = offset;
				while (offset < expression.Length && !(Char.IsWhiteSpace(expression[offset]) || expression[offset] == ')' || expression[offset] == '\'' || expression[offset] == '"'))
					offset++;

				if (startAt2 < offset)
				{
					var textValue = expression.Substring(startAt2, offset - startAt2);
					var textValueLength = textValue.Length;
					if (allowFields && textValueLength > 1 && textValue[0] == ':')
						value = new PpsDataFilterCompareFieldValue(textValue.Substring(1));
					else
					{
						value = new PpsDataFilterCompareTextValue(textValue);
					}
				}
				else
					value = PpsDataFilterCompareNullValue.Default;
			}

			return value;
		} // func PareCompareValue

		private static PpsDataFilterCompareValue[] ParseCompareValues(string expression, bool allowFields, IPropertyReadOnlyDictionary variables, ref int offset)
		{
			var values = new List<PpsDataFilterCompareValue>();

			SkipWhiteSpaces(expression, ref offset);
			while (!EatExpressionCharacter(expression, ref offset, ')'))
			{
				values.Add(ParseCompareValue(expression, allowFields, variables, ref offset));
				SkipWhiteSpaces(expression, ref offset);
			}

			return values.ToArray();
		} // func ParseCompareValues

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
					case '=':
						offset++;
						op = PpsDataFilterCompareOperator.Equal;
						break;
					case '!':
						offset++;
						op = EatExpressionCharacter(expression, ref offset, '=')
							? PpsDataFilterCompareOperator.NotEqual
							: PpsDataFilterCompareOperator.NotContains;
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

		private static PpsDataFilterExpression ParseExpression(string expression, PpsDataFilterExpressionType inLogic, IPropertyReadOnlyDictionary variables, ref int offset)
		{
			/*  expr ::=
			 *		[ identifier ] ( ':' [ '<' | '>' | '<=' | '>=' | '!' | '!=' ) [ '(' ] value [ ')' ]
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
			while (offset < expression.Length)
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
				ParseIdentifier(expression, ref offset);
				if (IsStartLogicOperation(expression, startAt, offset, out var newLogic))
				{
					offset++;
					var expr = ParseExpression(expression, newLogic, variables, ref offset);

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
					if (offset < expression.Length && !Char.IsWhiteSpace(expression[offset]))
					{
						var op = ParseCompareOperator(expression, ref offset); // parse the operator
						if (EatExpressionCharacter(expression, ref offset, '(')) // parse array
						{
							var values = ParseCompareValues(expression, true, variables, ref offset);
							switch (op)
							{
								case PpsDataFilterCompareOperator.Contains:
									if (values.Length > 0)
										compareExpressions.Add(new PpsDataFilterCompareExpression(identifier, PpsDataFilterCompareOperator.Contains, new PpsDataFilterCompareArrayValue(values)));
									else
										compareExpressions.Add(False);
									break;
								case PpsDataFilterCompareOperator.NotContains:
									if (values.Length > 0)
										compareExpressions.Add(new PpsDataFilterCompareExpression(identifier, PpsDataFilterCompareOperator.NotContains, new PpsDataFilterCompareArrayValue(values)));
									break;
							}
						}
						else // parse value
						{
							var value = ParseCompareValue(expression, true, variables, ref offset);
							// create expression
							compareExpressions.Add(new PpsDataFilterCompareExpression(identifier, op, value));
						}
					}
					else // is nothing
						compareExpressions.Add(new PpsDataFilterCompareExpression(identifier, PpsDataFilterCompareOperator.Equal, PpsDataFilterCompareNullValue.Default));
				}
				else if (nativeRef && IsStartNativeReference(expression, startAt, offset, out identifier)) // native reference
				{
					offset++;
					compareExpressions.Add(new PpsDataFilterNativeExpression(identifier));
				}
				else
				{
					offset = startAt; // nothing special try compare expression
					var value = ParseCompareValue(expression, false, variables, ref offset);
					if (value != PpsDataFilterCompareNullValue.Default)
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

		/// <summary></summary>
		/// <param name="filterExpression"></param>
		/// <param name="variables"></param>
		/// <param name="offset"></param>
		/// <returns></returns>
		public static PpsDataFilterExpression Parse(string filterExpression, IPropertyReadOnlyDictionary variables = null, int offset = 0)
			=> ParseExpression(filterExpression, PpsDataFilterExpressionType.None, variables, ref offset);

		/// <summary></summary>
		/// <param name="filterExpression"></param>
		/// <param name="returnAtLeastTrueExpression"></param>
		/// <param name="throwException"></param>
		/// <returns></returns>
		public static PpsDataFilterExpression Parse(object filterExpression, bool returnAtLeastTrueExpression = true, bool throwException = true)
		{
			switch (filterExpression)
			{
				case null:
					return returnAtLeastTrueExpression ? True : null;
				case string stringExpr:
					return Parse(stringExpr);
				case LuaTable table:
					return FromTable(table);
				default:
					if (throwException)
						throw new ArgumentException("Could not parse filter expression.");
					else
						return null;
			}
		} // func Parse

		#endregion

		#region -- Combine/Compare ----------------------------------------------------

		private static PpsDataFilterCompareValue GetValueExpressionFromString(string s)
		{
			return s.Length > 1 && s[0] == ':'
				? (PpsDataFilterCompareValue)new PpsDataFilterCompareFieldValue(s.Substring(1))
				: (PpsDataFilterCompareValue)new PpsDataFilterCompareTextValue(s);
		} // func GetValueExpressionFromString

		private static PpsDataFilterCompareValue GetValueExpressionFromLong(long i)
			=> new PpsDataFilterCompareIntegerValue(i);

		private static PpsDataFilterCompareValue GetValueExpressionFromDateTime(DateTime dt)
			=> new PpsDataFilterCompareDateValue(dt.Date, dt.Date.AddDays(1));

		private static PpsDataFilterCompareValue GetValueArrayExpression<T>(T[] values, Func<T, PpsDataFilterCompareValue> creator)
		{
			var r = new PpsDataFilterCompareValue[values.Length];

			for (var i = 0; i < values.Length; i++)
				r[i] = creator(values[i]);

			return new PpsDataFilterCompareArrayValue(r);
		}  // func GetValueArrayExpression

		private static PpsDataFilterCompareValue GetValueExpresion(PpsDataFilterCompareValueType type, object value)
		{
			switch (type)
			{
				case PpsDataFilterCompareValueType.Null:
					return PpsDataFilterCompareNullValue.Default;
				case PpsDataFilterCompareValueType.Field:
					return new PpsDataFilterCompareFieldValue(value.ChangeType<string>());
				case PpsDataFilterCompareValueType.Text:
					return new PpsDataFilterCompareTextValue(value.ChangeType<string>());
				case PpsDataFilterCompareValueType.Number:
					return new PpsDataFilterCompareNumberValue(value.ChangeType<string>());
				case PpsDataFilterCompareValueType.Integer:
					return GetValueExpressionFromLong(value.ChangeType<long>());
				case PpsDataFilterCompareValueType.Date:
					return GetValueExpressionFromDateTime(value.ChangeType<DateTime>());
				default:
					throw new ArgumentOutOfRangeException(nameof(type), type, "Out of range.");
			}
		} // func GetValueExpresion

		private static PpsDataFilterCompareValue GetValueExpresion(object value)
		{
			PpsDataFilterCompareValue GetValueExpressionFromInt(int i)
				=> GetValueExpressionFromLong(i);

			switch (value)
			{
				case null:
					return PpsDataFilterCompareNullValue.Default;
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
					return new PpsDataFilterCompareTextValue(value.ChangeType<string>());
			}
		} // func GetValueExpresion

		/// <summary>Compine multiple expresions to one expression.</summary>
		/// <param name="expr"></param>
		/// <returns></returns>
		public static PpsDataFilterExpression Combine(params PpsDataFilterExpression[] expr)
			=> new PpsDataFilterLogicExpression(PpsDataFilterExpressionType.And, expr).Reduce();

		/// <summary></summary>
		/// <param name="operand"></param>
		/// <param name="op"></param>
		/// <param name="type"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public static PpsDataFilterExpression Compare(string operand, PpsDataFilterCompareOperator op, PpsDataFilterCompareValueType type, object value)
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

		private static PpsDataFilterExpression GetExpressionFromObject(object expr)
			=> Parse(expr, false);

		private static PpsDataFilterExpression GetExpressionFromKeyValue(KeyValuePair<string, object> expr)
		{
			if (expr.Value == null)
				return null;

			if (expr.Value is LuaTable t && t.Members.Count == 0 && t.ArrayList.Count > 0) // create in filter
				return CompareIn(expr.Key, GetValueExpresion(t.ArrayList.ToArray()));
			else // create compare filter
				return Compare(expr.Key, PpsDataFilterCompareOperator.Equal, expr.Value);
		} // func GetExpressionFromObject

		/// <summary>Create a filter expression from a table.</summary>
		/// <param name="expression"></param>
		/// <returns></returns>
		public static PpsDataFilterExpression FromTable(LuaTable expression)
		{
			/* { [0] = "or",  COLUMN = VALUE, COLUMN = { VALUE, ... }, "Expr", {} } */

			var method = GetLogicExpression(expression.GetArrayValue(0, rawGet: true));

			// enumerate all members
			var expr = new PpsDataFilterLogicExpression(method,
				(
					from kv in expression.Members
					select GetExpressionFromKeyValue(kv)
				).Concat(
					from v in expression.ArrayList
					select GetExpressionFromObject(v)
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
			this.key = key;
		} // ctor

		/// <summary></summary>
		/// <param name="sb"></param>
		public override void ToString(StringBuilder sb)
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

		/// <summary></summary>
		/// <param name="sb"></param>
		public override void ToString(StringBuilder sb) { }

		/// <summary>Returns a expression, that is true.</summary>
		public static PpsDataFilterExpression Default { get; } = new PpsDataFilterTrueExpression();
	} // class PpsDataFilterTrueExpression

	#endregion

	#region -- enum PpsDataFilterCompareValueType -------------------------------------

	/// <summary></summary>
	public enum PpsDataFilterCompareValueType
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
		/// <summary>An other column.</summary>
		Field,
		/// <summary>An array of values.</summary>
		Array
	} // enum PpsDataFilterCompareValueType

	#endregion

	#region -- class PpsDataFilterCompareValue ----------------------------------------

	/// <summary></summary>
	public abstract class PpsDataFilterCompareValue : IEquatable<PpsDataFilterCompareValue>
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
			else if (obj is PpsDataFilterCompareValue other)
				return Equals(other);
			else
				return false;
		} // func Equals

		/// <summary></summary>
		/// <param name="other"></param>
		/// <returns></returns>
		public bool Equals(PpsDataFilterCompareValue other)
			=> this.Type == other.Type && EqualsValue(other);

		/// <summary></summary>
		/// <param name="other"></param>
		/// <returns></returns>
		protected abstract bool EqualsValue(PpsDataFilterCompareValue other);

		/// <summary></summary>
		/// <returns></returns>
		protected abstract int GetValueHashCode();

		/// <summary></summary>
		/// <param name="sb"></param>
		public abstract void ToString(StringBuilder sb);

		/// <summary></summary>
		public override string ToString()
		{
			var sb = new StringBuilder();
			ToString(sb);
			return sb.ToString();
		} // func ToString

		/// <summary></summary>
		public abstract PpsDataFilterCompareValueType Type { get; }
	} // class PpsDataFilterCompareValue

	#endregion

	#region -- class PpsDataFilterCompareNullValue ------------------------------------

	/// <summary></summary>
	public sealed class PpsDataFilterCompareNullValue : PpsDataFilterCompareValue
	{
		private PpsDataFilterCompareNullValue()
		{
		} // ctor

		/// <summary></summary>
		/// <param name="other"></param>
		/// <returns></returns>
		protected override bool EqualsValue(PpsDataFilterCompareValue other)
			=> true;

		/// <summary></summary>
		/// <returns></returns>
		protected override int GetValueHashCode()
			=> 23.GetHashCode();

		/// <summary></summary>
		/// <param name="sb"></param>
		public override void ToString(StringBuilder sb) { }

		/// <summary></summary>
		public override PpsDataFilterCompareValueType Type => PpsDataFilterCompareValueType.Null;

		/// <summary></summary>
		public static PpsDataFilterCompareValue Default { get; } = new PpsDataFilterCompareNullValue();
	} // class PpsDataFilterCompareNullValue

	#endregion

	#region -- class PpsDataFilterCompareFieldValue -----------------------------------

	/// <summary></summary>
	public sealed class PpsDataFilterCompareFieldValue : PpsDataFilterCompareValue
	{
		private readonly string fieldName;

		/// <summary></summary>
		/// <param name="fieldName"></param>
		public PpsDataFilterCompareFieldValue(string fieldName)
		{
			this.fieldName = fieldName ?? throw new ArgumentNullException(nameof(fieldName));
		} // ctor

		/// <summary></summary>
		/// <param name="other"></param>
		/// <returns></returns>
		protected override bool EqualsValue(PpsDataFilterCompareValue other)
			=> Equals(fieldName, ((PpsDataFilterCompareFieldValue)other).fieldName);

		/// <summary></summary>
		/// <returns></returns>
		protected override int GetValueHashCode()
			=> fieldName.GetHashCode();

		/// <summary></summary>
		/// <param name="sb"></param>
		public override void ToString(StringBuilder sb)
			=> sb.Append(':').Append(fieldName);

		/// <summary></summary>
		public string FieldName => fieldName;
		/// <summary></summary>
		public override PpsDataFilterCompareValueType Type => PpsDataFilterCompareValueType.Field;
	} // class PpsDataFilterCompareFieldValue

	#endregion

	#region -- class PpsDataFilterCompareTextValue ------------------------------------

	/// <summary></summary>
	public sealed class PpsDataFilterCompareTextValue : PpsDataFilterCompareValue
	{
		private readonly string text;

		/// <summary></summary>
		/// <param name="text"></param>
		public PpsDataFilterCompareTextValue(string text)
		{
			this.text = text ?? throw new ArgumentNullException(nameof(text));
		} // ctor

		/// <summary></summary>
		/// <param name="other"></param>
		/// <returns></returns>
		protected override bool EqualsValue(PpsDataFilterCompareValue other)
			=> Equals(text, ((PpsDataFilterCompareTextValue)other).text);

		/// <summary></summary>
		/// <returns></returns>
		protected override int GetValueHashCode()
			=> text.GetHashCode();

		/// <summary></summary>
		/// <param name="sb"></param>
		public override void ToString(StringBuilder sb)
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

		/// <summary></summary>
		public string Text => text;
		/// <summary></summary>
		public override PpsDataFilterCompareValueType Type => PpsDataFilterCompareValueType.Text;
	} // class PpsDataFilterCompareTextValue

	#endregion

	#region -- class PpsDataFilterCompareIntegerValue ---------------------------------

	/// <summary></summary>
	public sealed class PpsDataFilterCompareIntegerValue : PpsDataFilterCompareValue
	{
		private readonly long value;

		/// <summary></summary>
		/// <param name="value"></param>
		public PpsDataFilterCompareIntegerValue(long value)
		{
			this.value = value;
		} // ctor

		/// <summary></summary>
		/// <param name="other"></param>
		/// <returns></returns>
		protected override bool EqualsValue(PpsDataFilterCompareValue other)
			=> Equals(value, ((PpsDataFilterCompareIntegerValue)other).value);

		/// <summary></summary>
		/// <returns></returns>
		protected override int GetValueHashCode()
			=> value.GetHashCode();

		/// <summary></summary>
		/// <param name="sb"></param>
		public override void ToString(StringBuilder sb)
			=> sb.Append(value);

		/// <summary></summary>
		public long Value => value;
		/// <summary></summary>
		public override PpsDataFilterCompareValueType Type => PpsDataFilterCompareValueType.Integer;
	} // class PpsDataFilterCompareIntegerValue

	#endregion

	#region -- class PpsDataFilterCompareDateValue ------------------------------------

	/// <summary></summary>
	public sealed class PpsDataFilterCompareDateValue : PpsDataFilterCompareValue
	{
		private readonly DateTime from;
		private readonly DateTime to;

		/// <summary></summary>
		/// <param name="from"></param>
		/// <param name="to"></param>
		public PpsDataFilterCompareDateValue(DateTime from, DateTime to)
		{
			this.from = from;
			this.to = to;
		} // ctor

		/// <summary></summary>
		/// <param name="other"></param>
		/// <returns></returns>
		protected override bool EqualsValue(PpsDataFilterCompareValue other)
			=> Equals(from, ((PpsDataFilterCompareDateValue)other).from) && Equals(to, ((PpsDataFilterCompareDateValue)other).to);

		/// <summary></summary>
		/// <returns></returns>
		protected override int GetValueHashCode()
			=> from.GetHashCode() ^ to.GetHashCode();

		private void AppendComponents(StringBuilder sb, string notAllowedPatterns)
		{
			var shortDatePattern = CultureInfo.CurrentUICulture.DateTimeFormat.ShortDatePattern;
			var myDatePattern = new StringBuilder(shortDatePattern.Length);

			for (var i = 0; i < shortDatePattern.Length; i++)
			{
				if (notAllowedPatterns.IndexOf(shortDatePattern[i]) == -1)
					myDatePattern.Append(shortDatePattern[i]);
			}

			sb.Append(from.ToString(myDatePattern.ToString(), CultureInfo.CurrentUICulture.DateTimeFormat));
		} // proc AppendComponents

		/// <summary></summary>
		/// <param name="sb"></param>
		public override void ToString(StringBuilder sb)
		{
			sb.Append('#');
			if (from.Day == 1 && from.Month == 1 && to.Day == 1 && to.Month == 1 && to.Year - from.Year == 1) // diff is a year
				AppendComponents(sb, "Md");
			else if (from.Day == 1 && to.Day == 1 && to.Month - from.Month == 1 && to.Year - from.Year == 0) // diff is a month
				AppendComponents(sb, "d");
			else if (to.Day - from.Day == 1 && to.Month - from.Month == 0 && to.Year - from.Year == 0) // diff is a day
				AppendComponents(sb, "");
			else if (IsValid)
			{
				sb.Append(from.ToString("d", CultureInfo.CurrentUICulture.DateTimeFormat))
					.Append("~")
					.Append(to.ToString("d", CultureInfo.CurrentUICulture.DateTimeFormat));
			}

			sb.Append('#');
		}

		/// <summary></summary>
		public DateTime From => from;
		/// <summary></summary>
		public DateTime To => to;

		/// <summary></summary>
		public bool IsValid => from != DateTime.MinValue || to != DateTime.MaxValue;

		/// <summary></summary>
		public override PpsDataFilterCompareValueType Type => PpsDataFilterCompareValueType.Date;

		// -- Static ----------------------------------------------------------------------

		private static char JumpPattern(char patternSymbol, string datePattern, ref int patterPos)
		{
			while (patterPos < datePattern.Length && patternSymbol == datePattern[patterPos])
				patterPos++;

			if (patterPos < datePattern.Length) // jump over pattern
				return datePattern[patterPos++];
			else
				return '\0';
		} // func JumpPattern

		private static int ReadDigits(char splitSymbol, string inputDate, ref int inputPos)
		{
			string digits;

			var symbolPos = inputDate.IndexOf(splitSymbol, inputPos);
			if (symbolPos == -1)
			{
				digits = inputDate.Substring(inputPos);
				inputPos = inputDate.Length;
			}
			else
			{
				digits = inputDate.Substring(inputPos, symbolPos - inputPos);
				inputPos = symbolPos + 1;
			}

			return Int32.TryParse(digits, NumberStyles.None, CultureInfo.CurrentUICulture.NumberFormat, out var r)
				? r
				: -1;
		} // func ReadDigits

		private static PpsDataFilterCompareDateValue CreateValue(DateTime from, char addType)
		{
			switch (addType)
			{
				case 'y':
					return new PpsDataFilterCompareDateValue(from, from.AddYears(1));
				case 'M':
					return new PpsDataFilterCompareDateValue(from, from.AddMonths(1));
				default:
					return new PpsDataFilterCompareDateValue(from, from.AddDays(1));
			}
		} // func CreateValue

		/// <summary></summary>
		/// <param name="expression"></param>
		/// <param name="offset"></param>
		/// <param name="count"></param>
		/// <returns></returns>
		public static PpsDataFilterCompareValue Create(string expression, int offset, int count)
		{
			var inputDate = expression.Substring(offset, count);
			var dateSplit = inputDate.IndexOf('~');
			if (dateSplit >= 0) // split date format
			{
				if (DateTime.TryParse(inputDate.Substring(0, dateSplit), out var from) && DateTime.TryParse(inputDate.Substring(dateSplit + 1), out var to))
					return new PpsDataFilterCompareDateValue(from, to);
			}

			// Guess a date combination
			// range patterns:
			//   yyyy
			//   MM.yyyy
			//   dd.MM.yyyy
			// fill up patterns:
			//   dd.MM.
			//   dd.
			//   null

			var datePattern = CultureInfo.CurrentUICulture.DateTimeFormat.ShortDatePattern;

			var year = -1;
			var month = -1;
			var day = -1;

			var patterPos = 0;
			var inputPos = 0;
			var error = false;
			while (patterPos < datePattern.Length && !error)
			{
				var patternSymbol = datePattern[patterPos];
				var splitSymbol = JumpPattern(patternSymbol, datePattern, ref patterPos);
				var startAt = inputPos;
				var t = ReadDigits(splitSymbol, inputDate, ref inputPos);
				var readedNum = inputPos - startAt;
				if (t > 0)
				{
					if (readedNum == 4) // switch to date
						patternSymbol = 'y';

					switch (patternSymbol)
					{
						case 'y':
							if (year == -1 || readedNum == 4)
								year = t;
							break;
						case 'M':
							if (t > 12)
								goto case 'd';

							if (month == -1)
								month = t;
							break;
						case 'd':
							if (t > 31)
								goto case 'y';

							if (day == -1)
								day = t;
							break;
						default:
							error = true;
							break;
					}
				}
			}
			if (error)
			{
				if (DateTime.TryParse(expression.Substring(offset, count), out var dt)) // try parse full date
					return CreateValue(dt.Date, 'd');
				else // set a invalid date
					return new PpsDataFilterCompareDateValue(DateTime.MinValue, DateTime.MaxValue);
			}
			else
			{
				var dtNow = DateTime.Now;
				if (year == -1 && month == -1 && day == -1) // all components are missing
					return CreateValue(dtNow.Date, 'd');
				else if (year != -1 && month == -1 && day == -1) // only year is given
					return CreateValue(new DateTime(year, 01, 01), 'y');
				else if (year != -1 && month != -1 && day == -1) // year and month is given
					return CreateValue(new DateTime(year, month, 01), 'M');
				else if (year != -1 && month == -1 && day >= 1 && day <= 12) // year and month is given (day is detected)
					return CreateValue(new DateTime(year, day, 01), 'M');
				else // fill up with now
				{
					if (year == -1)
						year = dtNow.Year;
					if (month == -1)
						month = dtNow.Month;
					if (day == -1)
						day = dtNow.Day;

					try
					{
						return CreateValue(new DateTime(year, month, day), 'd');
					}
					catch (ArgumentOutOfRangeException) // invalid date
					{
						return new PpsDataFilterCompareDateValue(DateTime.MinValue, DateTime.MaxValue);
					}
				}
			}
		} // func Create
	} // class PpsDataFilterCompareDateValue

	#endregion

	#region -- class PpsDataFilterCompareNumberValue ----------------------------------

	/// <summary></summary>
	public sealed class PpsDataFilterCompareNumberValue : PpsDataFilterCompareValue
	{
		private readonly string text;

		/// <summary></summary>
		/// <param name="text"></param>
		public PpsDataFilterCompareNumberValue(string text)
		{
			this.text = text;
		} // ctor

		/// <summary></summary>
		/// <param name="other"></param>
		/// <returns></returns>
		protected override bool EqualsValue(PpsDataFilterCompareValue other)
			=> Equals(text, ((PpsDataFilterCompareNumberValue)other).text);

		/// <summary></summary>
		/// <returns></returns>
		protected override int GetValueHashCode()
			=> text.GetHashCode();

		/// <summary></summary>
		/// <param name="sb"></param>
		public override void ToString(StringBuilder sb)
		{
			sb.Append('#')
				.Append(text);
		} // proc ToString

		/// <summary></summary>
		public string Text => text;
		/// <summary></summary>
		public override PpsDataFilterCompareValueType Type => PpsDataFilterCompareValueType.Number;
	} // class PpsDataFilterCompareNumberValue

	#endregion

	#region -- class PpsDataFilterCompareArrayValue -----------------------------------

	/// <summary>An array of values.</summary>
	public sealed class PpsDataFilterCompareArrayValue : PpsDataFilterCompareValue
	{
		private readonly PpsDataFilterCompareValue[] values;

		/// <summary>Create a array value</summary>
		/// <param name="values"></param>
		public PpsDataFilterCompareArrayValue(PpsDataFilterCompareValue[] values)
		{
			if (values == null || values.Length == 0)
				throw new ArgumentNullException(nameof(values));

			var newValues = new List<PpsDataFilterCompareValue>(values.Length);
			CopyValues(values, newValues);
			this.values = newValues.ToArray();
		} // ctor

		private static void CopyValues(PpsDataFilterCompareValue[] values, List<PpsDataFilterCompareValue> newValues)
		{
			newValues.Add(values[0]);

			for (var i = 1; i < values.Length; i++)
			{
				if (values[i] is PpsDataFilterCompareArrayValue av)
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
		protected override bool EqualsValue(PpsDataFilterCompareValue other)
		{
			var otherArr = (PpsDataFilterCompareArrayValue)other;
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

		/// <summary></summary>
		/// <param name="sb"></param>
		public override void ToString(StringBuilder sb)
		{
			sb.Append("(");

			values[0].ToString(sb);
			for (var i = 1; i < values.Length; i++)
			{
				sb.Append(' ');
				values[i].ToString(sb);
			}

			sb.Append(")");
		} // func ToString

		/// <summary>Values</summary>
		public PpsDataFilterCompareValue[] Values => values;
		/// <summary>Return the item type</summary>
		public PpsDataFilterCompareValueType ItemType => values[0].Type;
		/// <summary>Return Array</summary>
		public override PpsDataFilterCompareValueType Type => PpsDataFilterCompareValueType.Array;

		internal static bool IsArrayCompatibleType(PpsDataFilterCompareValueType itemType)
		{
			switch (itemType)
			{
				case PpsDataFilterCompareValueType.Field:
				case PpsDataFilterCompareValueType.Integer:
				case PpsDataFilterCompareValueType.Text:
					return true;
				default:
					return false;
			}
		} // func IsArrayCompatibleType
	} // class PpsDataFilterCompareArrayValue

	#endregion

	#region -- class PpsDataFilterCompareExpression -----------------------------------

	/// <summary></summary>
	public sealed class PpsDataFilterCompareExpression : PpsDataFilterExpression
	{
		private readonly string operand;
		private readonly PpsDataFilterCompareOperator op;
		private readonly PpsDataFilterCompareValue value;

		/// <summary></summary>
		/// <param name="operand"></param>
		/// <param name="op"></param>
		/// <param name="value"></param>
		public PpsDataFilterCompareExpression(string operand, PpsDataFilterCompareOperator op, PpsDataFilterCompareValue value)
			: base(PpsDataFilterExpressionType.Compare)
		{
			this.operand = operand;
			this.op = op;
			this.value = value ?? throw new ArgumentNullException(nameof(value));

			if (value.Type == PpsDataFilterCompareValueType.Array)
			{
				if (op != PpsDataFilterCompareOperator.Contains && op != PpsDataFilterCompareOperator.NotContains)
					throw new ArgumentException($"Arrays are only allowed for {PpsDataFilterCompareOperator.Contains} or {PpsDataFilterCompareOperator.NotContains}.");
			}
		} // ctor

		/// <summary></summary>
		/// <returns></returns>
		public override PpsDataFilterExpression Reduce()
		{
			// combine single value arrays to equal expression
			if (value is PpsDataFilterCompareArrayValue av && av.Values.Length == 1)
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
			else
				return this;
		} // func Reduce

		/// <summary></summary>
		/// <param name="sb"></param>
		public override void ToString(StringBuilder sb)
		{
			if (String.IsNullOrEmpty(operand))
			{
				value.ToString(sb);
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
					default:
						throw new InvalidOperationException();
				}
				value.ToString(sb);
			}
		} // func ToString

		/// <summary></summary>
		public string Operand => operand;
		/// <summary></summary>
		public PpsDataFilterCompareOperator Operator => op;
		/// <summary></summary>
		public PpsDataFilterCompareValue Value => value;
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
			private readonly List<PpsDataFilterExpression> args = new List<PpsDataFilterExpression>();
			private readonly List<List<PpsDataFilterCompareValue>> combineValues = new List<List<PpsDataFilterCompareValue>>();

			public ReduceHelper(PpsDataFilterLogicExpression target)
			{
				this.target = target;

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


			private static void AddValueTo(List<PpsDataFilterCompareValue> values, PpsDataFilterCompareValue valueToAdd)
			{
				foreach (var v in values)
				{
					if (valueToAdd.Equals(v))
						return;
				}
				values.Add(valueToAdd);
			} // proc AddValueTo

			private void AddValueTo(int i, PpsDataFilterCompareArrayValue currentArrayContent, PpsDataFilterCompareValue valueToAdd)
			{
				// extent list
				while (i >= combineValues.Count)
					combineValues.Add(null);

				// get values
				var values = combineValues[i];
				if (values == null) // init list
				{
					values = combineValues[i] = new List<PpsDataFilterCompareValue>();
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
											&& curCmp.Value is PpsDataFilterCompareArrayValue arr && PpsDataFilterCompareArrayValue.IsArrayCompatibleType(cmp.Value.Type)) // add to array
										{
											AddValueTo(i, arr, cmp.Value);
											return true;
										}
										break;
									case CompareExpressionResult.EqualOperation:
										if (cmp.Operator == PpsDataFilterCompareOperator.Equal
											&& curCmp.Value.Type == cmp.Value.Type
											&& PpsDataFilterCompareArrayValue.IsArrayCompatibleType(curCmp.Value.Type)) // create a "contains" with array
										{
											var arr3 = new PpsDataFilterCompareArrayValue(new PpsDataFilterCompareValue[] { curCmp.Value });
											args[i] = new PpsDataFilterCompareExpression(curCmp.Operand, PpsDataFilterCompareOperator.Contains, arr3);
											AddValueTo(i, arr3, cmp.Value);
											return true;
										}
										else if (cmp.Operator == PpsDataFilterCompareOperator.Contains
											&& curCmp.Value is PpsDataFilterCompareArrayValue arr1
											&& cmp.Value is PpsDataFilterCompareArrayValue arr2) // if array combine in array
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
											&& curCmp.Value is PpsDataFilterCompareArrayValue arr && PpsDataFilterCompareArrayValue.IsArrayCompatibleType(cmp.Value.Type)) // add to array
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
											&& PpsDataFilterCompareArrayValue.IsArrayCompatibleType(curCmp.Value.Type)) // create a "not contains" with array
										{
											var arr3 = new PpsDataFilterCompareArrayValue(new PpsDataFilterCompareValue[] { curCmp.Value });
											args[i] = new PpsDataFilterCompareExpression(curCmp.Operand, PpsDataFilterCompareOperator.NotContains, arr3);
											AddValueTo(i, arr3, cmp.Value);
											return true;
										}
										else if (cmp.Operator == PpsDataFilterCompareOperator.NotContains
											&& curCmp.Value is PpsDataFilterCompareArrayValue arr1
											&& cmp.Value is PpsDataFilterCompareArrayValue arr2) // if array combine in array
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

					var expr = cur.Reduce();
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
					if (combineValues[i] != null)
					{
						var cmp = (PpsDataFilterCompareExpression)args[i];
						args[i] = new PpsDataFilterCompareExpression(cmp.Operand, cmp.Operator,
							new PpsDataFilterCompareArrayValue(combineValues[i].ToArray())
						);
					}
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
		public override PpsDataFilterExpression Reduce()
			=> new ReduceHelper(this).Build();

		/// <summary></summary>
		/// <param name="sb"></param>
		public override void ToString(StringBuilder sb)
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

			arguments[0].ToString(sb);

			for (var i = 1; i < arguments.Length; i++)
			{
				sb.Append(' ');
				arguments[i].ToString(sb);
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
		public abstract T CreateCompareIn(string operand, PpsDataFilterCompareArrayValue values);

		/// <summary></summary>
		/// <param name="operand"></param>
		/// <param name="values"></param>
		/// <returns></returns>
		public abstract T CreateCompareNotIn(string operand, PpsDataFilterCompareArrayValue values);

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
				if (compareExpr.Value is PpsDataFilterCompareArrayValue arr)
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
			=> CreateErrorFilter(String.Format("Column '{0}' not found.'", columnToken));

		#endregion

		#region -- CreateCompareFilter ------------------------------------------------

		/// <summary></summary>
		/// <param name="expression"></param>
		/// <returns></returns>
		public override string CreateCompareFilter(PpsDataFilterCompareExpression expression)
		{
			switch (expression.Value.Type)
			{
				case PpsDataFilterCompareValueType.Field:
					return CreateCompareFilterField(expression.Operand, expression.Operator, ((PpsDataFilterCompareFieldValue)expression.Value).FieldName);
				case PpsDataFilterCompareValueType.Text:
					return CreateCompareFilterText(expression.Operand, expression.Operator, ((PpsDataFilterCompareTextValue)expression.Value).Text);
				case PpsDataFilterCompareValueType.Date:
					return CreateCompareFilterDate(expression.Operand, expression.Operator, ((PpsDataFilterCompareDateValue)expression.Value).From, ((PpsDataFilterCompareDateValue)expression.Value).To);
				case PpsDataFilterCompareValueType.Number:
					return CreateCompareFilterNumber(expression.Operand, expression.Operator, ((PpsDataFilterCompareNumberValue)expression.Value).Text);
				case PpsDataFilterCompareValueType.Integer:
					return CreateCompareFilterInteger(expression.Operand, expression.Operator, ((PpsDataFilterCompareIntegerValue)expression.Value).Value);
				case PpsDataFilterCompareValueType.Null:
					return CreateCompareFilterNull(expression.Operand, expression.Operator);
				default:
					throw new NotImplementedException();
			}
		} // func CreateCompareFilter

		private string CreateDefaultCompareValue(string columnName, PpsDataFilterCompareOperator op, string value, bool useContains)
		{
			switch (op)
			{
				case PpsDataFilterCompareOperator.Contains:
					if (useContains)
						return columnName + " LIKE " + CreateLikeString(value, PpsSqlLikeStringEscapeFlag.Both);
					else
						goto case PpsDataFilterCompareOperator.Equal;
				case PpsDataFilterCompareOperator.NotContains:
					if (useContains)
						return "NOT " + columnName + " LIKE " + CreateLikeString(value, PpsSqlLikeStringEscapeFlag.Both);
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
				: CreateDefaultCompareValue(column.Item1, op, value.ChangeType<string>(), false);
		} // func CreateCompareFilterText

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
					return column.Item1 + " BETWEEN " + CreateDateString(from) + " AND " + CreateDateString(to);
				case PpsDataFilterCompareOperator.NotContains:
				case PpsDataFilterCompareOperator.NotEqual:
					return "NOT " + column.Item1 + " BETWEEN " + CreateDateString(from) + " AND " + CreateDateString(to);

				case PpsDataFilterCompareOperator.Greater:
					return column.Item1 + " > " + CreateDateString(to);
				case PpsDataFilterCompareOperator.GreaterOrEqual:
					return column.Item1 + " >= " + CreateDateString(from);
				case PpsDataFilterCompareOperator.Lower:
					return column.Item1 + " < " + CreateDateString(from);
				case PpsDataFilterCompareOperator.LowerOrEqual:
					return column.Item1 + " <= " + CreateDateString(to);

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
		protected void CreateCompareInValues(StringBuilder sb, Type itemType, PpsDataFilterCompareArrayValue values)
		{
			var first = true;
			foreach (var cur in values.Values)
			{
				if (first)
					first = false;
				else
					sb.Append(',');

				if (cur is PpsDataFilterCompareFieldValue field)
				{
					var col = LookupColumn(field.FieldName);
					sb.Append(col.Item1);
				}
				else if (cur is PpsDataFilterCompareTextValue text)
					sb.Append(CreateParsableValue(text.Text, itemType));
				else if (cur is PpsDataFilterCompareIntegerValue num)
					sb.Append(CreateParsableValue(num.Value.ChangeType<string>(), itemType));
				else
					throw new NotImplementedException($"{cur.Type} is not supported.");
			}
		} // proc CreateCompareInValues

		/// <summary></summary>
		/// <param name="operand"></param>
		/// <param name="values"></param>
		/// <returns></returns>
		public override string CreateCompareIn(string operand, PpsDataFilterCompareArrayValue values)
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
		public override string CreateCompareNotIn(string operand, PpsDataFilterCompareArrayValue values)
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

		private static Expression ConvertTo(Expression expr, Type typeTo)
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
				case PpsDataFilterCompareValueType.Field:
					{
						var right = GetProperty(((PpsDataFilterCompareFieldValue)expression.Value).FieldName);
						if (left.Type == typeof(string))
							return CreateCompareFilterForTextProperty(expression, left, right);
						else
							return Expression.MakeBinary(GetBinaryExpressionType(expression), ConvertTo(left, typeof(long)), right);
					}
				case PpsDataFilterCompareValueType.Text:
					return CreateCompareFilterForTextProperty(expression, left, Expression.Constant(((PpsDataFilterCompareTextValue)expression.Value).Text));
				case PpsDataFilterCompareValueType.Integer:
					{
						var right = Expression.Constant(((PpsDataFilterCompareIntegerValue)expression.Value).Value);
						return Expression.MakeBinary(GetBinaryExpressionType(expression), ConvertTo(left, typeof(long)), right);
					}
				case PpsDataFilterCompareValueType.Number:
					{
						var right = Expression.Constant(((PpsDataFilterCompareNumberValue)expression.Value).Text);
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
				case PpsDataFilterCompareValueType.Null:
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
				case PpsDataFilterCompareValueType.Date:
					{
						var a = (PpsDataFilterCompareDateValue)expression.Value;
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
						Expression.Constant(expression.Value.Type == PpsDataFilterCompareValueType.Number)
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
						Expression.Constant(((PpsDataFilterCompareDateValue)expression.Value).From),
						Expression.Constant(((PpsDataFilterCompareDateValue)expression.Value).To)
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
					case PpsDataFilterCompareValueType.Text:
						return CreateCompareFilterFullText(expression, ((PpsDataFilterCompareTextValue)expression.Value).Text);
					case PpsDataFilterCompareValueType.Number:
						return CreateCompareFilterFullText(expression, ((PpsDataFilterCompareNumberValue)expression.Value).Text);

					case PpsDataFilterCompareValueType.Date:
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
		public override Expression CreateCompareIn(string operand, PpsDataFilterCompareArrayValue values)
		{
			var prop = GetProperty(operand);
			if (values.ItemType == PpsDataFilterCompareValueType.Integer)
			{
				var arrExpr = Expression.NewArrayInit(prop.Type,
					values.Values.Select(c => Expression.Constant(Procs.ChangeType(((PpsDataFilterCompareIntegerValue)c).Value, prop.Type), prop.Type))
				);
				return CreateArrayFind(arrExpr, prop);
			}
			else if (values.ItemType == PpsDataFilterCompareValueType.Text)
			{
				var arrExpr = Expression.NewArrayInit(prop.Type,
					values.Values.Select(c => Expression.Constant(Procs.ChangeType(((PpsDataFilterCompareTextValue)c).Text, prop.Type), prop.Type))
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
		public sealed override Expression CreateCompareNotIn(string operand, PpsDataFilterCompareArrayValue values)
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
					Expression.Constant(expression.Value.Type == PpsDataFilterCompareValueType.Number)
				)
			);

		/// <summary></summary>
		/// <param name="expression"></param>
		/// <returns></returns>
		protected override Expression CreateCompareFilterFullDate(PpsDataFilterCompareExpression expression)
			=> CreateCompareFilterFullOperator(expression,
				Expression.Call(datarowSearchFullDateMethodInfo,
					Expression.Convert(CurrentRowParameter, typeof(IDataRow)),
					Expression.Constant(((PpsDataFilterCompareDateValue)expression.Value).From),
					Expression.Constant(((PpsDataFilterCompareDateValue)expression.Value).To)
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
		private readonly string columnName;
		private readonly string columnAlias;

		/// <summary></summary>
		/// <param name="columnName"></param>
		/// <param name="columnAlias"></param>
		public PpsDataColumnExpression(string columnName, string columnAlias = null)
		{
			this.columnName = columnName ?? throw new ArgumentNullException(nameof(columnName));
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
				? columnName
				: columnName + ":" + columnAlias;

		/// <summary></summary>
		public string Name => columnName;
		/// <summary></summary>
		public string Alias => columnAlias ?? columnName;

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
	} // class PpsDataColumnExpression

	#endregion
}
