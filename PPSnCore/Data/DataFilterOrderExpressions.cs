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

		/// <summary>#</summary>
		Number,
		/// <summary>"" or base64</summary>
		Fulltext,

		/// <summary>The value contains a native expression.</summary>
		Native
	} // enum PpsDataFilterExpressionType

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

		public PpsDataFilterExpressionType Type => method;

		// -- Static --------------------------------------------------------------

		private static void SkipWhiteSpaces(string filterExpression, ref int offset)
		{
			while (offset < filterExpression.Length && Char.IsWhiteSpace(filterExpression[offset]))
				offset++;
		} // func SkipWhiteSpaces

		private static void ParseIdentifier(string filterExpression, ref int offset)
		{
			while (offset < filterExpression.Length && Char.IsLetterOrDigit(filterExpression[offset]))
				offset++;
		} // func ParseIdentifier

		private static void ParseToUnescaped(string filterExpression, char eof, ref int offset)
		{
			while (offset < filterExpression.Length && filterExpression[offset] != eof)
				offset++;
		} // func ParseToUnescaped

		private static string ParseConstant(string filterExpression, ref int offset)
		{
			var sb = new StringBuilder();
			var escape = false;
			offset++;
			while (offset < filterExpression.Length)
			{
				var c = filterExpression[offset];
				if (escape)
				{
					if (c == '"')
						sb.Append(c);
					else
					{
						offset++;
						return sb.ToString();
					}
				}
				else if (c == '"')
					escape = true;
				else
					sb.Append(c);

				offset++;
			}

			return sb.ToString();
		} // func ParseConstant

		private static string DecodeBase64(string filterExpression, int startAt, int count)
		{
			var b = Convert.FromBase64String(filterExpression.Substring(startAt, count));
			return Encoding.UTF8.GetString(b, 0, b.Length);
		} // func DecodeBase64

		private static PpsDataFilterExpressionType GetFilterType(string filterExpression, int startAt, int count)
		{
			foreach (var c in typeof(PpsDataFilterExpressionType).GetTypeInfo().DeclaredFields)
			{
				if (c.IsStatic && c.IsPublic && String.Compare(filterExpression, startAt, c.Name, 0, count, StringComparison.OrdinalIgnoreCase) == 0)
					return (PpsDataFilterExpressionType)c.GetValue(null);
			}
			return PpsDataFilterExpressionType.None;
		} // func GetFilterType

		private static PpsDataFilterExpression Parse(string filterExpression, ref int offset, Func<string, string> lookupToken)
		{
			// use a simple syntax
			//   expr = identifier | command '(' expr ',' ... ')' | '\'base64'\' | '"' chars '"'

			SkipWhiteSpaces(filterExpression, ref offset);
			if (offset >= filterExpression.Length)
				return null;

			if (filterExpression[offset] == '"') // constant
			{
				return new PpsDataFilterConstantExpression(PpsDataFilterExpressionType.Fulltext, ParseConstant(filterExpression, ref offset));
			}
			else if (filterExpression[offset] == '\'') // base64 constant
			{
				offset++;
				var startAt = offset;
				ParseToUnescaped(filterExpression, '\'', ref offset);
				var endAt = offset;
				offset++;

				return new PpsDataFilterConstantExpression(PpsDataFilterExpressionType.Native, DecodeBase64(filterExpression, startAt, endAt - startAt));
			}
			else if (Char.IsLetter(filterExpression[offset])) // identifier
			{
				var startAt = offset;
				ParseIdentifier(filterExpression, ref offset);
				var endAt = offset;


				SkipWhiteSpaces(filterExpression, ref offset);

				if (offset < filterExpression.Length && filterExpression[offset] == '(') // command
				{
					var method = GetFilterType(filterExpression, startAt, endAt - startAt);
					if (method == PpsDataFilterExpressionType.None)
						throw new Exception("parse??"); // todo: parse at error

					offset++;
					var exprList = new List<PpsDataFilterExpression>();
					while (true)
					{
						exprList.Add(Parse(filterExpression, ref offset, lookupToken));
						SkipWhiteSpaces(filterExpression, ref offset);
						if (offset >= filterExpression.Length)
							break;
						else if (filterExpression[offset] == ')')
						{
							offset++;
							break;
						}
						else if (filterExpression[offset] == ',')
							offset++;
						else
							throw new Exception("parse??"); // todo: parse at error
					}

					return new PpsDataFilterMultiExpression(method, exprList.ToArray());
				}
				else // native expression by keyword
				{
					var tok = filterExpression.Substring(startAt, endAt - startAt);
					var nativeExpression = lookupToken(tok);
					if (nativeExpression == null)
						return new PpsDataFilterConstantExpression(PpsDataFilterExpressionType.Fulltext, tok);
					else
						return new PpsDataFilterConstantExpression(PpsDataFilterExpressionType.Native, nativeExpression);
				}
			}
			else
				throw new Exception("parse??"); // todo: parse at error
		} // func Parse

		public static PpsDataFilterExpression Parse(string filterExpression, int offset, Func<string, string> lookupToken)
			=> Parse(filterExpression, ref offset, lookupToken);
	} // class PpsDataFilterExpression

	#endregion

	#region -- class PpsDataFilterConstantExpression ------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsDataFilterConstantExpression : PpsDataFilterExpression
	{
		private readonly string expression;

		public PpsDataFilterConstantExpression(PpsDataFilterExpressionType method, string expression)
			: base(PpsDataFilterExpressionType.Native)
		{
			switch (method)
			{
				case PpsDataFilterExpressionType.Native:
				case PpsDataFilterExpressionType.Number:
				case PpsDataFilterExpressionType.Fulltext:
					break;
				default:
					throw new ArgumentException("method is wrong.");
			}
			this.expression = expression;
		} // ctor

		public string Expression => expression;
	} // class PpsDataFilterConstantExpression

	#endregion

	#region -- class PpsDataFilterMultiExpression ---------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsDataFilterMultiExpression : PpsDataFilterExpression
	{
		private readonly PpsDataFilterExpression[] arguments;

		public PpsDataFilterMultiExpression(PpsDataFilterExpressionType method, PpsDataFilterExpression[] arguments)
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

			this.arguments = arguments;
		} // ctor

		public PpsDataFilterExpression[] Arguments => arguments;
	} // class PpsDataFilterMultiExpression

	#endregion

	#region -- class PpsDataFilterVisitor<T> --------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public abstract class PpsDataFilterVisitor<T>
		where T : class
	{
		public abstract T CreateFilter(PpsDataFilterExpressionType method, string expression);

		public abstract T CreateFilter(PpsDataFilterExpressionType method, IEnumerable<T> arguments);

		public virtual T CreateFilter(PpsDataFilterExpression expression)
		{
			if (expression is PpsDataFilterConstantExpression)
				return CreateFilter(expression.Type, ((PpsDataFilterConstantExpression)expression).Expression);
			else if (expression is PpsDataFilterMultiExpression)
				return CreateFilter(expression.Type, from c in ((PpsDataFilterMultiExpression)expression).Arguments select CreateFilter(c));
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

		public static IEnumerable<PpsDataOrderExpression> Parse(string order, int v, Func<string, string> findNativeOrder)
		{
			var orderTokens = order.Split(',');
			foreach (var _tok in orderTokens)
			{
				if (String.IsNullOrEmpty(_tok))
					continue;

				var tok = _tok;
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
