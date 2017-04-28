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
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TecWare.DE.Stuff;

namespace TecWare.PPSn.Data
{
	#region -- enum PpsDataFilterExpressionType -----------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public enum PpsDataFilterExpressionType
	{
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
	
	#region -- enum PpsDataFilterCompareOperator ----------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public enum PpsDataFilterCompareOperator
	{
		Contains,
		NotContains,
		Equal,
		NotEqual,
		Greater,
		GreaterOrEqual,
		Lower,
		LowerOrEqual
	} // enum PpsDataFilterCompareOperator

	#endregion

	#region -- enum PpsSqlLikeStringEscapeFlag ------------------------------------------

	[Flags]
	public enum PpsSqlLikeStringEscapeFlag
	{
		Leading = 1,
		Trailing = 2,
		Both = Leading | Trailing
	} // enum PpsSqlLikeStringEscapeFlag

	#endregion

	#region -- class PpsDataFilterExpression --------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public abstract class PpsDataFilterExpression
	{
		private readonly PpsDataFilterExpressionType method;

		public PpsDataFilterExpression(PpsDataFilterExpressionType method)
		{
			this.method = method;
		} // ctor

		public virtual PpsDataFilterExpression Reduce()
			=> this;

		public abstract void ToString(StringBuilder sb);

		public override string ToString()
		{
			var sb = new StringBuilder();
			ToString(sb);
			return sb.ToString();
		} // func ToString

		public PpsDataFilterExpressionType Type => method;

		// -- Static --------------------------------------------------------------

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

		private static PpsDataFilterCompareValue ParseCompareValue(string expression, ref int offset)
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
			else
			{
				var startAt2 = offset;
				while (offset < expression.Length && !(Char.IsWhiteSpace(expression[offset]) || expression[offset] == ')' || expression[offset] == '\'' || expression[offset] == '"'))
					offset++;
				value = startAt2 < offset ? new PpsDataFilterCompareTextValue(expression.Substring(startAt2, offset - startAt2)) : PpsDataFilterCompareNullValue.Default;
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

		private static PpsDataFilterExpression ParseExpression(string expression, PpsDataFilterExpressionType inLogic, ref int offset)
		{
			/*  expr ::=
			 *		[ identifier ] ( ':' [ '<' | '>' | '<=' | '>=' | '!' | '!=' ) value
			 *		[ 'and' | 'or' | 'nand' | 'nor' ] '(' expr { SP ... } [ ')' ]
			 *		':' native ':'
			 *		value
			 *	
			 *	base is always an AND concation
			 */
			if (expression == null)
				return PpsDataFilterTrueExpression.True;

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
				string identifier;
				PpsDataFilterExpressionType newLogic;

				// check for native reference
				var nativeRef = TestExpressionCharacter(expression, offset, ':');
				if (nativeRef)
					offset++;
				
				// check for an identifier
				ParseIdentifier(expression, ref offset);
				if (IsStartLogicOperation(expression, startAt, offset, out newLogic))
				{
					offset++;
					var expr = ParseExpression(expression, newLogic, ref offset);

					// optimize: concat same sub expression
					if (expr.Type == returnLogic)
						compareExpressions.AddRange(((PpsDataFilterLogicExpression)expr).Arguments);
					else if (expr != PpsDataFilterTrueExpression.True)
						compareExpressions.Add(expr);
				}
				else if (!nativeRef && IsStartCompareOperation(expression, startAt, offset, out identifier)) // compare operation
				{
					offset++; // step over the colon

					// check for operator, nothing means contains
					if (offset < expression.Length && !Char.IsWhiteSpace(expression[offset]))
					{
						var op = ParseCompareOperator(expression, ref offset); // parse the operator
						var value = ParseCompareValue(expression, ref offset); // parse the value

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
					var value = ParseCompareValue(expression, ref offset);
					if (value != PpsDataFilterCompareNullValue.Default)
						compareExpressions.Add(new PpsDataFilterCompareExpression(null, PpsDataFilterCompareOperator.Contains, value));
				}
			}

			// generate expression
			if (compareExpressions.Count == 0)
			{
				if (inLogic != PpsDataFilterExpressionType.NAnd && inLogic != PpsDataFilterExpressionType.NOr)
					return new PpsDataFilterLogicExpression(PpsDataFilterExpressionType.NAnd, PpsDataFilterTrueExpression.True);
				else
					return PpsDataFilterTrueExpression.True;
			}
			else if (compareExpressions.Count == 1 && (inLogic != PpsDataFilterExpressionType.NAnd && inLogic != PpsDataFilterExpressionType.NOr))
				return compareExpressions[0];
			else
				return new PpsDataFilterLogicExpression(returnLogic, compareExpressions.ToArray());
		} // func ParseExpression

		public static PpsDataFilterExpression Parse(string filterExpression, int offset = 0)
			=> ParseExpression(filterExpression, PpsDataFilterExpressionType.None, ref offset);

		public static PpsDataFilterExpression Combine(params PpsDataFilterExpression[] expr)
			=> new PpsDataFilterLogicExpression(PpsDataFilterExpressionType.And, expr).Reduce();

		public static PpsDataFilterExpression Compare(string operand, PpsDataFilterCompareOperator op, PpsDataFilterCompareValueType type, object value)
		{
			PpsDataFilterCompareValue GetValueExpresion()
			{
				switch (type)
				{
					case PpsDataFilterCompareValueType.Null:
						return PpsDataFilterCompareNullValue.Default;
					case PpsDataFilterCompareValueType.Text:
						return new PpsDataFilterCompareTextValue(value.ChangeType<string>());
					case PpsDataFilterCompareValueType.Number:
						return new PpsDataFilterCompareNumberValue(value.ChangeType<string>());
					case PpsDataFilterCompareValueType.Integer:
						return new PpsDataFilterCompareIntegerValue(value.ChangeType<long>());
					case  PpsDataFilterCompareValueType.Date:
						var dt = value.ChangeType<DateTime>();
						return new PpsDataFilterCompareDateValue(dt.Date, dt.Date.AddDays(1));
					default:
						throw new ArgumentOutOfRangeException(nameof(type), type, "Out of range.");
				}
			} // func GetValueExpresion

			return new PpsDataFilterCompareExpression(operand, op, GetValueExpresion());
		} // func Compare

		public static PpsDataFilterExpression Compare(string operand, PpsDataFilterCompareOperator op, object value)
		{
			PpsDataFilterCompareValue GetValueExpresion()
			{
				switch (value)
				{
					case null:
						return PpsDataFilterCompareNullValue.Default;
					case string s:
						return new PpsDataFilterCompareTextValue(s);
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
	} // class PpsDataFilterExpression

	#endregion

	#region -- class PpsDataFilterNativeExpression --------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsDataFilterNativeExpression : PpsDataFilterExpression
	{
		private readonly string key;
		
		public PpsDataFilterNativeExpression(string key)
			: base(PpsDataFilterExpressionType.Native)
		{
			this.key = key;
		} // ctor

		public override void ToString(StringBuilder sb)
		{
			sb.Append(':').Append(key).Append(':');
		} // func ToString

		public string Key => key;
	} // class PpsDataFilterNativeExpression

	#endregion

	#region -- class PpsDataFilterTrueExpression ----------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsDataFilterTrueExpression : PpsDataFilterExpression
	{
		private PpsDataFilterTrueExpression()
			: base(PpsDataFilterExpressionType.True)
		{
		} // ctor

		public override void ToString(StringBuilder sb) { }

		public static PpsDataFilterExpression True => new PpsDataFilterTrueExpression();
	} // class PpsDataFilterTrueExpression

	#endregion

	#region -- enum PpsDataFilterCompareValueType ---------------------------------------

	public enum PpsDataFilterCompareValueType
	{
		Null,
		Text,
		Date,
		Number,
		Integer
	} // enum PpsDataFilterCompareValueType

	#endregion

	#region -- class PpsDataFilterCompareValue ------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public abstract class PpsDataFilterCompareValue
	{
		public abstract void ToString(StringBuilder sb);

		public override string ToString()
		{
			var sb = new StringBuilder();
			ToString(sb);
			return sb.ToString();
		} // func ToString

		public abstract PpsDataFilterCompareValueType Type { get; }
	} // class PpsDataFilterCompareValue

	#endregion

	#region -- class PpsDataFilterCompareNullValue --------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsDataFilterCompareNullValue : PpsDataFilterCompareValue
	{
		private PpsDataFilterCompareNullValue()
		{
		} // ctor

		public override void ToString(StringBuilder sb) { }
		public override PpsDataFilterCompareValueType Type => PpsDataFilterCompareValueType.Null;

		public static PpsDataFilterCompareValue Default { get; } = new PpsDataFilterCompareNullValue();
	} // class PpsDataFilterCompareNullValue

	#endregion

	#region -- class PpsDataFilterCompareTextValue --------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsDataFilterCompareTextValue : PpsDataFilterCompareValue
	{
		private readonly string text;

		public PpsDataFilterCompareTextValue(string text)
		{
			this.text = text;
		} // ctor

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

		public string Text => text;
		public override PpsDataFilterCompareValueType Type => PpsDataFilterCompareValueType.Text;
	} // class PpsDataFilterCompareTextValue

	#endregion

	#region -- class PpsDataFilterCompareIntegerValue -----------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsDataFilterCompareIntegerValue : PpsDataFilterCompareValue
	{
		private readonly long value;

		public PpsDataFilterCompareIntegerValue(long value)
		{
			this.value = value;
		} // ctor

		public override void ToString(StringBuilder sb)
			=> sb.Append(value);

		public long Value => value;
		public override PpsDataFilterCompareValueType Type => PpsDataFilterCompareValueType.Integer;
	} // class PpsDataFilterCompareIntegerValue

	#endregion

	#region -- class PpsDataFilterCompareDateValue --------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsDataFilterCompareDateValue : PpsDataFilterCompareValue
	{
		private readonly DateTime from;
		private readonly DateTime to;

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

		public DateTime From => from;
		public DateTime To => to;

		public bool IsValid => from != DateTime.MinValue || to != DateTime.MaxValue;

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
			
			int r;
			if (Int32.TryParse(digits, NumberStyles.None, CultureInfo.CurrentUICulture.NumberFormat, out r))
				return r;
			else
				return -1;
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

		public static PpsDataFilterCompareValue Create(string expression, int offset, int count)
		{
			var inputDate = expression.Substring(offset, count);
			var dateSplit = inputDate.IndexOf('~');
			if (dateSplit >= 0) // split date format
			{
				DateTime from;
				DateTime to;
				if (DateTime.TryParse(inputDate.Substring(0, dateSplit), out from) && DateTime.TryParse(inputDate.Substring(dateSplit + 1), out to))
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
				DateTime dt;
				if (DateTime.TryParse(expression.Substring(offset, count), out dt)) // try parse full date
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

	#region -- class PpsDataFilterCompareNumberValue ------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsDataFilterCompareNumberValue : PpsDataFilterCompareValue
	{
		private readonly string text;

		public PpsDataFilterCompareNumberValue(string text)
		{
			this.text = text;
		} // ctor

		public override void ToString(StringBuilder sb)
		{
			sb.Append('#')
				.Append(text);
		} // proc ToString

		public string Text => text;
		public override PpsDataFilterCompareValueType Type => PpsDataFilterCompareValueType.Number;
	} // class PpsDataFilterCompareNumberValue

	#endregion

	#region -- class PpsDataFilterCompareExpression -------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsDataFilterCompareExpression : PpsDataFilterExpression
	{
		private readonly string operand;
		private readonly PpsDataFilterCompareOperator op;
		private readonly PpsDataFilterCompareValue value; // String, DateTime

		public PpsDataFilterCompareExpression(string operand, PpsDataFilterCompareOperator op, PpsDataFilterCompareValue value)
			:base (PpsDataFilterExpressionType.Compare)
		{
			if (value == null)
				throw new ArgumentNullException("value");

			this.operand = operand;
			this.op = op;
			this.value = value;
		} // ctor

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

		public string Operand => operand;
		public PpsDataFilterCompareOperator Operator => op;
		public PpsDataFilterCompareValue Value => value;
	} // class PpsDataFilterCompareExpression

	#endregion
	
	#region -- class PpsDataFilterLogicExpression ---------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsDataFilterLogicExpression : PpsDataFilterExpression
	{
		private readonly PpsDataFilterExpression[] arguments;

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

			if (args.Count > 1)
				return new PpsDataFilterLogicExpression(Type, args.ToArray());
			else if (args.Count == 1)
				return args[0];
			else
				return PpsDataFilterTrueExpression.True;
		} // proc Reduce

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

		public PpsDataFilterExpression[] Arguments => arguments;
	} // class PpsDataFilterLogicExpression

	#endregion

	#region -- class PpsDataFilterVisitor<T> --------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public abstract class PpsDataFilterVisitor<T>
		where T : class
	{
		public abstract T CreateTrueFilter();

		public abstract T CreateNativeFilter(PpsDataFilterNativeExpression expression);

		public abstract T CreateCompareFilter(PpsDataFilterCompareExpression expression);

		public abstract T CreateLogicFilter(PpsDataFilterExpressionType method, IEnumerable<T> arguments);

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

	#region -- class PpsDataFilterVisitorSql --------------------------------------------

	public abstract class PpsDataFilterVisitorSql : PpsDataFilterVisitor<string>
	{
		#region -- CreateTrueFilter--------------------------------------------------------

		public override string CreateTrueFilter()
			=> "1=1";

		#endregion

		#region -- CreateCompareFilter ----------------------------------------------------

		public override string CreateCompareFilter(PpsDataFilterCompareExpression expression)
		{
			switch (expression.Value.Type)
			{
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

		private string CreateCompareFilterText(string columnToken, PpsDataFilterCompareOperator op, string text)
		{
			var column = LookupColumn(columnToken);
			return CreateDefaultCompareValue(column.Item1, op, CreateParsableValue(text, column.Item2), column.Item2 == typeof(string));
		} // func CreateCompareFilterText

		private string CreateCompareFilterInteger(string columnToken, PpsDataFilterCompareOperator op, long value)
		{
			var column = LookupColumn(columnToken);
			return CreateDefaultCompareValue(column.Item1, op, value.ChangeType<string>(), false);
		} // func CreateCompareFilterText

		private string CreateCompareFilterNumber(string columnToken, PpsDataFilterCompareOperator op, string text)
		{
			var column = LookupNumberColumn(columnToken);
			if (column.Item2 != typeof(string))
				return "1=0"; // invalid filter for this column -> todo: exception

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
			if (column.Item2 != typeof(DateTime))
				return "1=0"; // invalid filter for this column -> todo: exception

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

		protected virtual string CreateDateString(DateTime value)
			=> "'" + value.ToString("G") + "'";

		private string CreateCompareFilterNull(string columnToken, PpsDataFilterCompareOperator op)
		{
			switch (op)
			{
				case PpsDataFilterCompareOperator.Contains:
				case PpsDataFilterCompareOperator.Equal:
					return LookupColumn(columnToken).Item1 + " is null";
				case PpsDataFilterCompareOperator.NotContains:
				case PpsDataFilterCompareOperator.NotEqual:
					return LookupColumn(columnToken).Item1 + " is not null";
				case PpsDataFilterCompareOperator.Greater:
				case PpsDataFilterCompareOperator.GreaterOrEqual:
				case PpsDataFilterCompareOperator.Lower:
				case PpsDataFilterCompareOperator.LowerOrEqual:
					return "1=0";
				default:
					throw new NotImplementedException();
			}
		} // func CreateCompareFilterNull

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

		protected virtual Tuple<string, Type> LookupDateColumn(string columnToken)
			=> LookupColumn(columnToken);

		protected virtual Tuple<string, Type> LookupNumberColumn(string columnToken)
			=> LookupColumn(columnToken);

		protected abstract Tuple<string, Type> LookupColumn(string columnToken);

		#endregion

		#region -- CreateLogicFilter ------------------------------------------------------

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
				return "1=1";

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

		#region -- CreateNativeFilter -----------------------------------------------------

		public override string CreateNativeFilter(PpsDataFilterNativeExpression expression)
		{
			var nativeExpression = LookupNativeExpression(expression.Key);
			if (String.IsNullOrEmpty(nativeExpression))
				return CreateTrueFilter();
			else
				return "(" + nativeExpression + ")";
		} // func CreateNativeFilter

		protected abstract string LookupNativeExpression(string key);

		#endregion
	} // class PpsDataFilterVisitorSql

	#endregion

	#region -- class PpsDataOrderExpression ---------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsDataOrderExpression
	{
		private readonly bool negate;
		private readonly string identifier;

		public PpsDataOrderExpression(bool negate, string identifier)
		{
			this.negate = negate;
			this.identifier = identifier;
		} // ctor

		public bool Negate => negate;
		public string Identifier => identifier;

		// -- Static --------------------------------------------------------------

		public static IEnumerable<PpsDataOrderExpression> Parse(string order)
		{
			var orderTokens = order.Split(',');
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

		public static string ToString(PpsDataOrderExpression[] orders)
			=> String.Join(",", from o in orders select (o.Negate ? "-" : "+") + o.Identifier);
	} // class PpsDataOrderExpression

	#endregion
}
