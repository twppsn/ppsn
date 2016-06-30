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
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

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
				while (offset < expression.Length && !Char.IsWhiteSpace(expression[offset]))
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

		private static bool IsStartCompareOperation(string expression, int startAt, int offset, Func<string, string> lookupToken, out string identifier)
		{
			if (offset > startAt && TestExpressionCharacter(expression, offset, ':'))
			{
				identifier = expression.Substring(startAt, offset - startAt - 1);
				if (lookupToken != null)
					identifier = lookupToken(identifier);
				return !String.IsNullOrEmpty(identifier);
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

		private static bool IsStartNativeReference(string expression, int startAt, int offset, Func<string, string> lookupNative, out string identifier, out string nativeExpression)
		{
			if (offset > startAt && TestExpressionCharacter(expression, offset, ':'))
			{
				identifier = expression.Substring(startAt, offset - startAt - 1);
				nativeExpression = lookupNative(identifier);
				return true;
			}
			else
			{
				identifier = null;
				nativeExpression = null;
				return false;
			}
		} // func IsStartNativeReference

		private static PpsDataFilterExpression ParseExpression(string expression, PpsDataFilterExpressionType inLogic, ref int offset, Func<string, string> lookupNative,  Func<string, string> lookupToken)
		{
			/*  expr ::=
			 *		identifier ( ':' [ '<' | '>' | '<=' | '>=' | '!' | '!=' ) value
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
				string nativeExpression;
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
					var expr = ParseExpression(expression, newLogic, ref offset, lookupNative, lookupToken);

					// optimize: concat same sub expression
					if (expr.Type == returnLogic)
						compareExpressions.AddRange(((PpsDataFilterLogicExpression)expr).Arguments);
					else if (expr != PpsDataFilterTrueExpression.True)
						compareExpressions.Add(expr);
				}
				else if (!nativeRef && IsStartCompareOperation(expression, startAt, offset, lookupToken, out identifier)) // compare operation
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
						compareExpressions.Add(new PpsDataFilterCompareExpression(identifier, PpsDataFilterCompareOperator.Equal, null));
				}
				else if (nativeRef && IsStartNativeReference(expression, startAt, offset, lookupNative, out identifier, out nativeExpression)) // native reference
				{
					offset++;
					compareExpressions.Add(new PpsDataFilterNativeExpression(identifier, nativeExpression));
				}
				else
				{
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

		public static PpsDataFilterExpression Parse(string filterExpression, int offset, Func<string, string> lookupNative, Func<string, string> lookupAttribute)
			=> ParseExpression(filterExpression, PpsDataFilterExpressionType.None, ref offset, lookupNative, lookupAttribute);
	} // class PpsDataFilterExpression

	#endregion

	#region -- class PpsDataFilterNativeExpression --------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsDataFilterNativeExpression : PpsDataFilterExpression
	{
		private readonly string key;
		private readonly string expression;

		public PpsDataFilterNativeExpression(string key, string expression)
			: base(PpsDataFilterExpressionType.Native)
		{
			this.key = key;
			this.expression = expression;
		} // ctor

		public override void ToString(StringBuilder sb)
		{
			sb.Append(key);
		} // func ToString

		public string Key => key;
		public string Expression => expression;
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
	} // class PpsDataFilterCompareTextValue

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

		public override void ToString(StringBuilder sb)
		{
			throw new NotImplementedException();
		}

		public DateTime From => from;
		public DateTime To => to;

		// -- Static ----------------------------------------------------------------------

		internal static PpsDataFilterCompareValue Create(string expression, int startAt2, int v)
		{
			throw new NotImplementedException();
		}
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
				}
				value.ToString(sb);
			}
		} // func ToString

		public string Operand => operand;
		public PpsDataFilterCompareOperator Operator => op;
		public object Value => value;
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
				sb.Append(',');
				arguments[0].ToString(sb);
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
	} // class PpsDataFilterExpressionVisitor

	#endregion

	#region -- class PpsDataOrderExpression ---------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsDataOrderExpression
	{
		private readonly bool negate;
		private readonly bool isNative;
		private readonly string expression;

		public PpsDataOrderExpression(bool negate, bool isNative, string expression)
		{
			this.negate = negate;
			this.isNative = isNative;
			this.expression = expression;
		} // ctor

		public bool Negate => negate;
		public bool IsNative => isNative;
		public string Expression => expression;

		// -- Static --------------------------------------------------------------

		public static IEnumerable<PpsDataOrderExpression> Parse(string order, Func<string, string> findNativeOrder)
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
				var nativeOrder = findNativeOrder(tok);
				yield return new PpsDataOrderExpression(neg, nativeOrder != null, nativeOrder ?? tok);
			}
		} // func Parse
	} // class PpsDataOrderExpression

	#endregion
}
