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
using TecWare.DE.Data;

namespace TecWare.PPSn.Data
{
	#region -- enum PpsDataJoinType ---------------------------------------------------

	/// <summary>Join type</summary>
	public enum PpsDataJoinType
	{
		/// <summary>Not specified.</summary>
		None,
		/// <summary>Inner join</summary>
		Inner,
		/// <summary>Left outer join</summary>
		Left,
		/// <summary>Right outer join</summary>
		Right
	} // enum JoinType

	#endregion

	#region -- class SimpleStatementParser --------------------------------------------

	internal class SimpleStatementParser
	{
		protected readonly string expression;
		protected int pos;

		protected SimpleStatementParser(string expression)
		{
			this.pos = 0;
			this.expression = expression ?? throw new ArgumentNullException(nameof(expression));

			ParseWhiteSpace();
		} // ctor

		protected Exception CreateException(string message)
			=> throw new PpsSimpleParseException(pos, message);

		protected void ParseIdentifier()
		{
			while (pos < expression.Length && Char.IsLetterOrDigit(expression[pos]))
				pos++;
		} // proc ParseIdentifier

		protected void ParseWhiteSpace()
		{
			while (pos < expression.Length && Char.IsWhiteSpace(expression[pos]))
				pos++;
		} // proc ParseWhiteSpace

		protected string ParseDotName()
		{
			var startAt = pos;
			ParseIdentifier();
			while (Cur == '.')
			{
				pos++;
				ParseIdentifier();
			}

			return expression.Substring(startAt, pos - startAt);
		} // fzunc ParseTableName

		protected string ParseAlias()
		{
			ParseWhiteSpace();
			if (Char.IsLetter(Cur))
			{
				var startAt = pos;
				ParseIdentifier();
				return expression.Substring(startAt, pos - startAt);
			}
			else
				return null;
		} // func ParseAlias

		protected IEnumerable<PpsDataJoinStatement> ParseOnStatement()
		{
			while (true)
			{
				// left site
				var left = ParseDotName();
				ParseWhiteSpace();

				// parse equal
				if (Cur != '=')
					throw CreateException("Expected '='.");
				else
					pos++;

				// right site
				ParseWhiteSpace();
				var right = ParseDotName();
				yield return new PpsDataJoinStatement(left, right);

				// connector , or AND
				ParseWhiteSpace();
				if (Cur == ',')
				{
					pos++;
					ParseWhiteSpace();
					continue;
				}

				var and = ParseAlias();
				if (and == null)
					break;

				if (String.Compare(and, "AND", StringComparison.OrdinalIgnoreCase) != 0)
					throw CreateException("Expected 'AND' or ','");
				ParseWhiteSpace();
			}
		} // func ParseOnStatement

		protected char Cur => pos >= expression.Length ? '\0' : expression[pos];
	} // class SimpleStatementParser

	#endregion

	#region -- class PpsDataJoinStatement ---------------------------------------------

	/// <summary>Compare statement of two columns.</summary>
	public sealed class PpsDataJoinStatement
	{
		/// <summary></summary>
		/// <param name="left"></param>
		/// <param name="right"></param>
		public PpsDataJoinStatement(string left, string right)
		{
			Left = left ?? throw new ArgumentNullException(nameof(left));
			Right = right ?? throw new ArgumentNullException(nameof(right));
		} // ctor

		/// <summary>Left expression column.</summary>
		public string Left { get; }
		/// <summary>Right expression column.</summary>
		public string Right { get; }

		#region -- ParseJoinType ------------------------------------------------------

		/// <summary>Create join type from string.</summary>
		/// <param name="expr"></param>
		/// <param name="throwException"></param>
		/// <returns></returns>
		public static PpsDataJoinType ParseJoinType(string expr, bool throwException)
		{
			if (String.IsNullOrWhiteSpace(expr))
				return PpsDataJoinType.None;

			var t = expr.Trim();
			if (t.Length == 1)
				return ParseJoinType(t[0], throwException);
			else if (throwException)
				throw CreateJoinTypeException(expr);
			else
				return PpsDataJoinType.None;
		} // func ParseJoinType

		/// <summary>Create join type from char.</summary>
		/// <param name="c"></param>
		/// <param name="throwException"></param>
		/// <returns></returns>
		public static PpsDataJoinType ParseJoinType(char c, bool throwException)
		{
			switch (c)
			{
				case '\0':
					return PpsDataJoinType.None;
				case ',':
				case '=':
					return PpsDataJoinType.Inner;
				case '>':
					return PpsDataJoinType.Left;
				case '<':
					return PpsDataJoinType.Right;
				default:
					if (throwException)
						throw CreateJoinTypeException(c.ToString());
					return PpsDataJoinType.None;
			}
		} // func ParseJoinType

		/// <summary>Convert join type to string.</summary>
		/// <param name="type"></param>
		/// <returns></returns>
		public static string ConvertJoinType(PpsDataJoinType type)
		{
			switch(type)
			{
				case PpsDataJoinType.Left:
					return ">";
				case PpsDataJoinType.Right:
					return "<";
				case PpsDataJoinType.Inner:
					return "=";
				default:
					return ",";
			}
		} // func ConvertJoinType

		private static Exception CreateJoinTypeException(string expr)
			=> throw new ArgumentOutOfRangeException(nameof(expr), expr, "Invalid join expression.");
		
		#endregion

		#region -- Parse --------------------------------------------------------------

		private sealed class SimpleOnParser : SimpleStatementParser
		{
			public SimpleOnParser(string expression)
				: base(expression)
			{
			} // ctor

			public IEnumerable<PpsDataJoinStatement> Parse()
			{
				foreach (var cur in ParseOnStatement())
					yield return cur;
				if (Cur != '\0')
					throw CreateException("End of statement expected."); // todo: better message?
			} // func Parse
		} // class SimpleOnParser

		/// <summary></summary>
		/// <param name="statement"></param>
		/// <returns></returns>
		public static IEnumerable<PpsDataJoinStatement> Parse(string statement)
			=> new SimpleOnParser(statement).Parse();

		#endregion
	} // class PpsDataJoinStatement

	#endregion

	#region -- class PpsSimpleParseException ------------------------------------------

	/// <summary>Join syntax exception.</summary>
	public class PpsSimpleParseException : FormatException
	{
		/// <summary>Join syntax exception.</summary>
		/// <param name="position">Position of the syntax error.</param>
		/// <param name="message"></param>
		/// <param name="innerException"></param>
		public PpsSimpleParseException(int position, string message, Exception innerException = null)
			: base(message, innerException)
		{
			Position = position;
		} // ctor

		/// <summary>Position of the syntax error.</summary>
		public int Position { get; }
	} // class PpsSimpleParseException

	#endregion

	#region -- class PpsJoinExpression ------------------------------------------------

	/// <summary>Join expression</summary>
	/// <typeparam name="TTABLE">Table information type.</typeparam>
	public abstract class PpsDataJoinExpression<TTABLE>
	{
		#region -- class PpsExpressionPart --------------------------------------------

		/// <summary>Base part of the expression.</summary>
		public abstract class PpsExpressionPart
		{
			/// <summary></summary>
			/// <returns></returns>
			public abstract IEnumerable<PpsTableExpression> GetTables();
			/// <summary></summary>
			public abstract bool IsValid { get; }
		} // class PpsExpressionPart

		#endregion

		#region -- class PpsTableExpression -------------------------------------------

		/// <summary>Table part of the expression</summary>
		public sealed class PpsTableExpression : PpsExpressionPart
		{
			private readonly TTABLE table;
			private readonly string alias;

			/// <summary></summary>
			/// <param name="table"></param>
			/// <param name="alias"></param>
			public PpsTableExpression(TTABLE table, string alias)
			{
				this.table = table;
				this.alias = alias;
			} // ctor

			/// <summary></summary>
			/// <returns></returns>
			public override string ToString()
				=> "#Table#" + table.ToString() + (String.IsNullOrEmpty(alias) ? String.Empty : "#" + alias);

			/// <summary></summary>
			/// <returns></returns>
			public override IEnumerable<PpsTableExpression> GetTables()
			{
				yield return this;
			} // func GetTables

			/// <summary>Referenced table.</summary>
			public TTABLE Table => table;
			/// <summary>Alias of this table.</summary>
			public string Alias => alias;
			/// <summary>Check the table.</summary>
			public override bool IsValid => table != null;
		} // class PpsTableExpression

		#endregion

		#region -- class PpsJoinExpression --------------------------------------------

		/// <summary>Join part of the expression.</summary>
		public sealed class PpsJoinExpression : PpsExpressionPart
		{
			private readonly PpsExpressionPart left;
			private readonly PpsDataJoinType type;
			private readonly PpsExpressionPart right;

			private readonly PpsDataJoinStatement[] onStatement;

			/// <summary></summary>
			/// <param name="left"></param>
			/// <param name="type"></param>
			/// <param name="right"></param>
			/// <param name="onStatement"></param>
			public PpsJoinExpression(PpsExpressionPart left, PpsDataJoinType type, PpsExpressionPart right, PpsDataJoinStatement[] onStatement)
			{
				this.left = left ?? throw new ArgumentNullException(nameof(left));
				this.type = type;
				this.right = right ?? throw new ArgumentNullException(nameof(right));
				this.onStatement = onStatement ?? throw new ArgumentNullException(nameof(onStatement));
			} // ctor

			/// <summary></summary>
			/// <returns></returns>
			public override string ToString()
				=> left.ToString() + " " + type.ToString() + " " + right.ToString();

			/// <summary></summary>
			/// <returns></returns>
			public override IEnumerable<PpsTableExpression> GetTables()
			{
				if (left is PpsTableExpression leftTable)
					yield return  leftTable;
				else 
				{
					foreach (var c in left.GetTables())
						yield return c;
				}
				if (right is PpsTableExpression rightTable)
					yield return rightTable;
				else
				{
					foreach (var c in right.GetTables())
						yield return c;
				}
			} // func GetTables

			/// <summary>Left site of the join.</summary>
			public PpsExpressionPart Left => left;
			/// <summary>Right site of the join.</summary>
			public PpsExpressionPart Right => right;
			/// <summary>Join statement.</summary>
			public PpsDataJoinStatement[] Statement => onStatement;
			/// <summary>Type</summary>
			public PpsDataJoinType Type => type;

			/// <summary></summary>
			public override bool IsValid => left != null && left.IsValid && right != null && right.IsValid;
		} // class PpsJoinExpression

		#endregion

		#region -- class PpsJoinVisitor<TRESULT> --------------------------------------

		/// <summary>Visitor pattern for join expressions.</summary>
		/// <typeparam name="TRESULT"></typeparam>
		public abstract class PpsJoinVisitor<TRESULT>
		{
			/// <summary>Visit table statement</summary>
			/// <param name="table"></param>
			/// <param name="alias"></param>
			/// <returns></returns>
			public abstract TRESULT CreateTableStatement(TTABLE table, string alias);
			/// <summary>Visit join statement</summary>
			/// <param name="leftExpression"></param>
			/// <param name="type"></param>
			/// <param name="rightExpression"></param>
			/// <param name="on"></param>
			/// <returns></returns>
			public abstract TRESULT CreateJoinStatement(TRESULT leftExpression, PpsDataJoinType type, TRESULT rightExpression, PpsDataJoinStatement[] on);

			/// <summary>Visit expression part</summary>
			/// <param name="expr"></param>
			/// <returns></returns>
			public TRESULT Visit(PpsExpressionPart expr)
			{
				switch(expr)
				{
					case PpsTableExpression tableExpr:
						return CreateTableStatement(tableExpr.Table, tableExpr.Alias);
					case PpsJoinExpression joinExpr:
						return CreateJoinStatement(Visit(joinExpr.Left), joinExpr.Type, Visit(joinExpr.Right), joinExpr.Statement);
					default:
						throw new InvalidOperationException();
				}
			} // func Visit

			/// <summary>Visit expression part</summary>
			/// <param name="expr"></param>
			/// <returns></returns>
			public TRESULT Visit(PpsDataJoinExpression<TTABLE> expr)
				=> Visit(expr.root);
		} // class PpsJoinVisitor

		#endregion

		#region -- class SimpleParser -------------------------------------------------

		private sealed class SimpleParser : SimpleStatementParser
		{
			private PpsDataJoinExpression<TTABLE> owner;

			public SimpleParser(PpsDataJoinExpression<TTABLE> owner, string expression)
				: base(expression)
			{
				this.owner = owner;
			} // ctor

			private PpsExpressionPart ParseTable()
			{
				if (Cur == '(')
				{
					pos++;
					var r = ParseExpr();
					if (Cur != ')')
						throw CreateException("')' expected.");
					pos++;
					return r;
				}
				else
				{
					var tableName = ParseDotName();
					if (String.IsNullOrEmpty(tableName))
						throw new ArgumentException("Identifier expected.");

					var tableAlias = ParseAlias();
					return owner.CreateTable(tableName, tableAlias);
				}
			} // func ParseTable

			private PpsDataJoinType ParseJoinOperator()
			{
				ParseWhiteSpace();
				var r = PpsDataJoinStatement.ParseJoinType(Cur, false);
				if (r != PpsDataJoinType.None)
					pos++;
				return r;
			} // func ParseJoinOperator

			private PpsExpressionPart ParseExpr()
			{
				ParseWhiteSpace();
				var left = ParseTable();

				var joinOp = PpsDataJoinType.None;
				while ((joinOp = ParseJoinOperator()) != PpsDataJoinType.None)
				{
					var right = ParseTable();
					PpsDataJoinStatement[] onStatement = null;
					if (Cur == '[')
					{
						pos++; // eat start
						onStatement = ParseOnStatement().ToArray();

						if (Cur != ']')
							throw CreateException("']' expected.");
						pos++;
					}

					left = new PpsJoinExpression(left, joinOp, right, onStatement ?? owner.CreateOnStatement(left, joinOp, right));
				}

				return left;
			} // func ParseExpr

			public PpsExpressionPart Parse()
			{
				var r = ParseExpr();
				if (Cur != '\0')
					throw CreateException("Unexpected end of string.");
				return r;
			} // func Parse
		} // class SimpleParser

		#endregion

		private PpsExpressionPart root;

		/// <summary></summary>
		protected PpsDataJoinExpression() { }

		/// <summary></summary>
		/// <param name="part"></param>
		protected PpsDataJoinExpression(PpsExpressionPart part)
			=> root = part ?? throw new ArgumentNullException(nameof(part));

		/// <summary></summary>
		/// <param name="table"></param>
		/// <param name="alias"></param>
		public PpsDataJoinExpression(TTABLE table, string alias)
			: this(new PpsTableExpression(table, alias))
		{
		} // ctor

		/// <summary></summary>
		/// <param name="tableName"></param>
		/// <param name="tableAlias"></param>
		/// <returns></returns>
		private PpsExpressionPart CreateTable(string tableName, string tableAlias)
			=> new PpsTableExpression(ResolveTable(tableName), tableAlias);

		private PpsDataJoinStatement[] CreateOnStatement(PpsExpressionPart left, PpsDataJoinType joinOp, PpsExpressionPart right)
		{
			foreach (var l in left.GetTables())
			{
				foreach (var r in right.GetTables())
				{
					var on = CreateOnStatement(l, joinOp, r);
					if (on != null)
						return on;
				}
			}
			return null;
		} // func CreateOnStatement

		/// <summary>Create on statement</summary>
		/// <param name="left"></param>
		/// <param name="joinOp"></param>
		/// <param name="right"></param>
		/// <returns></returns>
		protected abstract PpsDataJoinStatement[] CreateOnStatement(PpsTableExpression left, PpsDataJoinType joinOp, PpsTableExpression right);

		/// <summary>Resolve table by name.</summary>
		/// <param name="tableName"></param>
		/// <returns></returns>
		protected abstract TTABLE ResolveTable(string tableName);

		/// <summary></summary>
		/// <param name="table"></param>
		/// <param name="aliasName"></param>
		/// <param name="joinType"></param>
		/// <param name="statement"></param>
		protected PpsJoinExpression AppendCore(TTABLE table, string aliasName, PpsDataJoinType joinType, PpsDataJoinStatement[] statement)
			=> new PpsJoinExpression(root, joinType, new PpsTableExpression(table, aliasName), statement);

		/// <summary></summary>
		/// <param name="expr"></param>
		/// <param name="aliasName"></param>
		/// <param name="joinType"></param>
		/// <param name="statement"></param>
		protected PpsJoinExpression AppendCore(PpsDataJoinExpression<TTABLE> expr, string aliasName, PpsDataJoinType joinType, PpsDataJoinStatement[] statement)
			=> new PpsJoinExpression(root, joinType, expr.root, statement);

		/// <summary>Parse expression.</summary>
		/// <param name="expression"></param>
		protected void Parse(string expression)
		{
			// t1 alias
			// t1 op t2 [ ]
			// ( )
			root = new SimpleParser(this, expression).Parse();
		} // func Parse
		
		/// <summary>Enumerates all tables.</summary>
		/// <returns></returns>
		public IEnumerable<PpsTableExpression> GetTables()
		{
			var items = new Stack<PpsExpressionPart>();
			items.Push(root);
			while (items.Count > 0)
			{
				var cur = items.Pop();

				switch (cur)
				{
					case PpsTableExpression t:
						yield return t;
						break;
					case PpsJoinExpression j:
						items.Push(j.Right);
						items.Push(j.Left);
						break;
				}
			}
		} // func GetTables

		/// <summary>Parsed expression part</summary>
		public PpsExpressionPart Root => root;
		/// <summary>Is this expression valid.</summary>
		public bool IsValid => root?.IsValid ?? false;
	} // class PpsJoinExpression

	#endregion
}
