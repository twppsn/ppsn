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
			this.Type = method;
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
			=> c == '_' || Char.IsLetterOrDigit(c);

		private static bool TestExpressionCharacter(string expression, int offset, char c)
			=> offset < expression.Length && expression[offset] == c;

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
		} // func ParseConstant

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
						value = new PpsDataFilterCompareTextValue(textValue);
				}
				else
					value = PpsDataFilterCompareNullValue.Default;
			}

			return value;
		} // func PareCompareValue

		private static PpsDataFilterCompareOperator ParseCompareOperator(string expression, ref int offset)
		{
			var op = PpsDataFilterCompareOperator.Contains;

			if (offset < expression.Length)
			{
				if (expression[offset] == '<')
				{
					offset++;
					if (TestExpressionCharacter(expression, offset, '='))
					{
						offset++;
						op = PpsDataFilterCompareOperator.LowerOrEqual;
					}
					else
						op = PpsDataFilterCompareOperator.Lower;
				}
				else if (expression[offset] == '>')
				{
					offset++;
					if (TestExpressionCharacter(expression, offset, '='))
					{
						offset++;
						op = PpsDataFilterCompareOperator.GreaterOrEqual;
					}
					else
						op = PpsDataFilterCompareOperator.Greater;
				}
				else if (expression[offset] == '=')
				{
					offset++;
					op = PpsDataFilterCompareOperator.Equal;
				}
				else if (expression[offset] == '!')
				{
					offset++;
					if (TestExpressionCharacter(expression, offset, '='))
					{
						offset++;
						op = PpsDataFilterCompareOperator.NotEqual;
					}
					else
						op = PpsDataFilterCompareOperator.NotContains;
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
			 *		[ identifier ] ( ':' [ '<' | '>' | '<=' | '>=' | '!' | '!=' ) value
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
						var value = ParseCompareValue(expression, true, variables, ref offset); // parse the value

						// create expression
						compareExpressions.Add(new PpsDataFilterCompareExpression(identifier, op, value));
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
				if (inLogic == PpsDataFilterExpressionType.NAnd || inLogic == PpsDataFilterExpressionType.NOr)
					return new PpsDataFilterLogicExpression(PpsDataFilterExpressionType.NAnd, PpsDataFilterTrueExpression.Default);
				else
					return PpsDataFilterTrueExpression.Default;
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
					return PpsDataFilterExpression.FromTable(table);
				default:
					if (throwException)
						throw new ArgumentException("Could not parse filter expression.");
					else
						return null;
			}
		} // func Parse

		#endregion

		#region -- Combine/Compare ----------------------------------------------------

		/// <summary></summary>
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
		{
			PpsDataFilterCompareValue GetValueExpresion()
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
						return new PpsDataFilterCompareIntegerValue(value.ChangeType<long>());
					case PpsDataFilterCompareValueType.Date:
						var dt = value.ChangeType<DateTime>();
						return new PpsDataFilterCompareDateValue(dt.Date, dt.Date.AddDays(1));
					default:
						throw new ArgumentOutOfRangeException(nameof(type), type, "Out of range.");
				}
			} // func GetValueExpresion

			return new PpsDataFilterCompareExpression(operand, op, GetValueExpresion());
		} // func Compare

		/// <summary></summary>
		/// <param name="operand"></param>
		/// <param name="op"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public static PpsDataFilterExpression Compare(string operand, PpsDataFilterCompareOperator op, object value)
		{
			PpsDataFilterCompareValue GetValueExpresion()
			{
				switch (value)
				{
					case null:
						return PpsDataFilterCompareNullValue.Default;
					case string s:
						return s.Length > 1 && s[0] == ':'
							? (PpsDataFilterCompareValue)new PpsDataFilterCompareFieldValue(s.Substring(1))
							: (PpsDataFilterCompareValue)new PpsDataFilterCompareTextValue(s);
					case int i:
						return new PpsDataFilterCompareIntegerValue((long)i);
					case long n:
						return new PpsDataFilterCompareIntegerValue((long)n);
					case DateTime dt:
						return new PpsDataFilterCompareDateValue(dt.Date, dt.Date.AddDays(1));
					default:
						return new PpsDataFilterCompareTextValue(value.ChangeType<string>());
				}
			} // func GetValueExpresion

			return new PpsDataFilterCompareExpression(operand, op, GetValueExpresion());
		} // func Compare

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

			return Compare(expr.Key, PpsDataFilterCompareOperator.Equal, expr.Value);
		} // func GetExpressionFromObject

		/// <summary>Create a filter expression from a table.</summary>
		/// <param name="expression"></param>
		/// <returns></returns>
		public static PpsDataFilterExpression FromTable(LuaTable expression)
		{
			/* { [0] = "or",  COLUMN = VALUE, "Expr", {} } */

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
	} // class PpsDataFilterExpression

	#endregion

	#region -- class PpsDataFilterExpressionConverter ---------------------------------

	/// <summary>Converts expressions</summary>
	public class PpsDataFilterExpressionConverter : TypeConverter
	{
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
		{
			sb.Append(':').Append(key).Append(':');
		} // func ToString

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
		/// <summary>A value which is presenting an amount.</summary>
		Integer,
		/// <summary>An other column.</summary>
		Field
	} // enum PpsDataFilterCompareValueType

	#endregion

	#region -- class PpsDataFilterCompareValue ----------------------------------------

	/// <summary></summary>
	public abstract class PpsDataFilterCompareValue
	{
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

	#region -- class PpsDataFilterCompareExpression -----------------------------------

	/// <summary></summary>
	public sealed class PpsDataFilterCompareExpression : PpsDataFilterExpression
	{
		private readonly string operand;
		private readonly PpsDataFilterCompareOperator op;
		private readonly PpsDataFilterCompareValue value; // String, DateTime

		/// <summary></summary>
		/// <param name="operand"></param>
		/// <param name="op"></param>
		/// <param name="value"></param>
		public PpsDataFilterCompareExpression(string operand, PpsDataFilterCompareOperator op, PpsDataFilterCompareValue value)
			: base(PpsDataFilterExpressionType.Compare)
		{
			this.operand = operand;
			this.op = op;
			this.value = value ?? throw new ArgumentNullException("value");
		} // ctor

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
						sb.Append('!');
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
				throw new ArgumentNullException("arguments");

			this.arguments = arguments;
		} // ctor

		/// <summary></summary>
		public override PpsDataFilterExpression Reduce()
		{
			var args = new List<PpsDataFilterExpression>(arguments.Length);
			foreach (var arg in arguments)
			{
				var c = arg.Reduce();

				if (c.Type == this.Type)
					args.AddRange(((PpsDataFilterLogicExpression)c).Arguments);
				else if (c.Type != PpsDataFilterExpressionType.True)
					args.Add(c);
			}

			if (args.Count == 0)
				return PpsDataFilterTrueExpression.Default;
			else if (args.Count == 1 && (Type == PpsDataFilterExpressionType.And || Type == PpsDataFilterExpressionType.Or))
				return args[0];
			else
				return new PpsDataFilterLogicExpression(Type, args.ToArray());
		} // proc Reduce

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
		/// <param name="method"></param>
		/// <param name="arguments"></param>
		/// <returns></returns>
		public abstract T CreateLogicFilter(PpsDataFilterExpressionType method, IEnumerable<T> arguments);

		/// <summary></summary>
		/// <param name="expression"></param>
		/// <returns></returns>
		public virtual T CreateFilter(PpsDataFilterExpression expression)
		{
			if (expression is PpsDataFilterNativeExpression)
				return CreateNativeFilter((PpsDataFilterNativeExpression)expression);
			else if (expression is PpsDataFilterLogicExpression)
				return CreateLogicFilter(expression.Type, from c in ((PpsDataFilterLogicExpression)expression).Arguments select CreateFilter(c));
			else if (expression is PpsDataFilterCompareExpression)
				return CreateCompareFilter((PpsDataFilterCompareExpression)expression);
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
					return CreateCompareFilterText(expression.Operand, expression.Operator, ((PpsDataFilterCompareIntegerValue)expression.Value).Value.ChangeType<string>());
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
			var parseableValue = String.Empty;
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
					throw new NotImplementedException();
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

		private string CreateLikeString(string value, PpsSqlLikeStringEscapeFlag flag)
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
					var propertyInfo = CurrentRowParameter.Type.GetRuntimeProperty(memberName)
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
				return Expression.Convert(Expression.Call(procsChangeTypeMethodInfo, expr, Expression.Constant(typeTo)), typeTo);
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
			=> Expression.MakeBinary(expressionType,
				Expression.Call(
					Expression.Coalesce(ConvertTo(left, typeof(string)), Expression.Constant(String.Empty)), stringIndexOfMethodInfo,
						ConvertTo(right, typeof(string)),
						Expression.Constant(StringComparison.OrdinalIgnoreCase)
				),
				Expression.Constant(0)
			);

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
						{
							return CreateCompareFilterFullDate(expression);
						}

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

		static PpsDataFilterVisitorLambda()
		{
			stringIndexOfMethodInfo = typeof(string).GetMethod(nameof(String.IndexOf), new Type[] { typeof(string), typeof(StringComparison) }) ?? throw new ArgumentNullException("String.IndexOf");
			stringCompareMethodInfo = typeof(string).GetMethod(nameof(String.Compare), new Type[] { typeof(string), typeof(string), typeof(StringComparison) }) ?? throw new ArgumentNullException("String.Compare");
			stringStartsWithMethodInfo = typeof(string).GetMethod(nameof(String.StartsWith), new Type[] { typeof(string), typeof(StringComparison) }) ?? throw new ArgumentNullException("String.StartsWith");
			stringJoinMethodInfo = typeof(string).GetMethod(nameof(String.Join), new Type[] { typeof(string), typeof(string[]) }) ?? throw new ArgumentNullException("String.Join");

			procsChangeTypeMethodInfo = typeof(Procs).GetMethod(nameof(Procs.ChangeType), new Type[] { typeof(object), typeof(Type) }) ?? throw new ArgumentNullException("Procs.ChangeType");

			compareFullTextSearchTextMethodInfo = typeof(ICompareFulltext).GetMethod(nameof(ICompareFulltext.SearchText), new Type[] { typeof(string), typeof(bool) }) ?? throw new ArgumentNullException("ICompareFulltext.SearchText");
			compareDateTimeSearchDateMethodInfo = typeof(ICompareDateTime).GetMethod(nameof(ICompareDateTime.SearchDate), new Type[] { typeof(DateTime), typeof(DateTime) }) ?? throw new ArgumentNullException("ICompareDateTime.SearchDate");
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
				if (level < 3 && row is IDataValues2 r && r.TryGetRelatedDataRow(i, out var relatedRow))
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
					if (startsWith && s.StartsWith(text, StringComparison.OrdinalIgnoreCase))
						return true;
					else if (!startsWith && s.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
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
	} // class PpsDataFilterVisitorDataRow

	#endregion

	#region -- class PpsDataOrderExpression -------------------------------------------

	/// <summary></summary>
	public sealed class PpsDataOrderExpression
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
			=> obj is PpsDataOrderExpression o ? o.identifier.Equals(identifier) && o.negate == negate : base.Equals(obj);

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
