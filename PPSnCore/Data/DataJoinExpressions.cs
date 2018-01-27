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

	#region -- class PpsJoinException -------------------------------------------------

	/// <summary>Join syntax exception.</summary>
	public class PpsJoinException : FormatException
	{
		private readonly int position;

		/// <summary>Join syntax exception.</summary>
		/// <param name="position">Position of the syntax error.</param>
		/// <param name="message"></param>
		/// <param name="innerException"></param>
		public PpsJoinException(int position, string message, Exception innerException = null)
			: base(message, innerException)
		{
			this.position = position;
		} // ctor

		/// <summary>Position of the syntax error.</summary>
		public int Position => position;
	} // class PpsJoinException

	#endregion

	#region -- class PpsJoinExpression ------------------------------------------------

	/// <summary>Join expression</summary>
	/// <typeparam name="TTABLE">Table information type.</typeparam>
	public abstract class PpsDataJoinExpression<TTABLE>
		where TTABLE : IDataColumns
	{
		#region -- class PpsExpressionPart --------------------------------------------

		/// <summary>Base part of the expression.</summary>
		public abstract class PpsExpressionPart
		{
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

			/// <summary>Referenced table.</summary>
			public TTABLE Table => table;
			/// <summary>Alias of this table.</summary>
			public string Alias => alias;
		} // class PpsTableExpression

		#endregion

		#region -- class PpsJoinExpression --------------------------------------------

		/// <summary>Join part of the expression.</summary>
		public sealed class PpsJoinExpression : PpsExpressionPart
		{
			private readonly PpsExpressionPart left;
			private readonly PpsDataJoinType type;
			private readonly PpsExpressionPart right;

			private readonly string onStatement;

			/// <summary></summary>
			/// <param name="left"></param>
			/// <param name="type"></param>
			/// <param name="right"></param>
			/// <param name="onStatement"></param>
			public PpsJoinExpression(PpsExpressionPart left, PpsDataJoinType type, PpsExpressionPart right, string onStatement)
			{
				this.left = left ?? throw new ArgumentNullException(nameof(left));
				this.type = type;
				this.right = right ?? throw new ArgumentNullException(nameof(right));
				this.onStatement = onStatement ?? throw new ArgumentNullException(nameof(onStatement));
			} // ctor

			/// <summary>Left site of the join.</summary>
			public PpsExpressionPart Left => left;
			/// <summary>Right site of the join.</summary>
			public PpsExpressionPart Right => right;
			/// <summary>Join statement.</summary>
			public string Statement => onStatement;
			/// <summary>Type</summary>
			public PpsDataJoinType Type => type;
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
			public abstract TRESULT CreateJoinStatement(TRESULT leftExpression, PpsDataJoinType type, TRESULT rightExpression, string on);

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

		private sealed class SimpleParser
		{
			private PpsDataJoinExpression<TTABLE> owner;
			private int pos;
			private readonly string expression;

			public SimpleParser(PpsDataJoinExpression<TTABLE> owner, string expression)
			{
				this.pos = 0;
				this.owner = owner;
				this.expression = expression ?? throw new ArgumentNullException(nameof(expression));

				ParseWhiteSpace();
			} // ctor

			private Exception CreateException(string message)
				=> throw new PpsJoinException(pos, message);

			private void ParseIdentifier()
			{
				while (pos < expression.Length && Char.IsLetterOrDigit(expression[pos]))
					pos++;
			} // proc ParseIdentifier

			private void ParseWhiteSpace()
			{
				while (pos < expression.Length && Char.IsWhiteSpace(expression[pos]))
					pos++;
			} // proc ParseWhiteSpace

			private string ParseTableName()
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

			private string ParseAlias()
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

			private PpsExpressionPart ParseTable()
			{
				var tableName = ParseTableName();
				var tableAlias = ParseAlias();
				return owner.CreateTable(tableName, tableAlias);
			} // func ParseTable

			private string ParseOnStatement()
			{
				ParseWhiteSpace();
				if (Cur == '[')
				{
					var startAt = ++pos;
					while (Cur != ']' && Cur != '\0')
						pos++;

					if (Cur != ']')
						throw CreateException("']' expected.");
					var endAt = pos;

					pos++;
					return expression.Substring(startAt, endAt - startAt);
				}
				else
					return null;
			} // func ParseOnStatement

			private PpsDataJoinType ParseJoinOperator()
			{
				ParseWhiteSpace();
				switch(Cur)
				{
					case ',':
					case '=':
						pos++;
						return PpsDataJoinType.Inner;
					case '>':
						pos++;
						return PpsDataJoinType.Left;
					case '<':
						pos++;
						return PpsDataJoinType.Right;
					default:
						return PpsDataJoinType.None;
				}
			} // func ParseJoinOperator

			private PpsExpressionPart ParseExpr()
			{
				ParseWhiteSpace();
				if (Cur == '(')
				{
					pos++;
					var r = ParseExpr();
					if (Cur != ')')
						throw CreateException("')' expected.");
					pos++;
					return r;
				}
				else if (Char.IsLetter(Cur))
				{
					var left = ParseTable();

					var joinOp = PpsDataJoinType.None;
					while ((joinOp = ParseJoinOperator()) != PpsDataJoinType.None)
					{
						var right = ParseExpr();
						var on = ParseOnStatement();

						left = new PpsJoinExpression(left, joinOp, right, on ?? owner.CreateOnStatement(left, joinOp, right));
					}

					return left;
				}
				else
					throw CreateException("Identifier expected.");
			} // func ParseExpr

			public PpsExpressionPart Parse()
			{
				var r = ParseExpr();
				if (Cur != '\0')
					throw CreateException("Unexpected end of string.");
				return r;
			} // func Parse

			private char Cur => pos >= expression.Length ? '\0' : expression[pos];
		} // class SimpleParser

		#endregion

		private PpsExpressionPart root;

		/// <summary></summary>
		public PpsDataJoinExpression()
		{
		} // ctor

		private string CreateOnStatement(PpsExpressionPart left, PpsDataJoinType joinOp, PpsExpressionPart right)
		{
			if (left is PpsTableExpression leftTableExpr
				&& right is PpsTableExpression rightTableExpr)
				return CreateOnStatement(leftTableExpr, joinOp, rightTableExpr);
			else
				return null;
		} // func CreateOnStatement

		private PpsExpressionPart CreateTable(string tableName, string tableAlias)
			=> new PpsTableExpression(ResolveTable(tableName), tableAlias);

		/// <summary>Create on statement</summary>
		/// <param name="left"></param>
		/// <param name="joinOp"></param>
		/// <param name="right"></param>
		/// <returns></returns>
		protected abstract string CreateOnStatement(PpsTableExpression left, PpsDataJoinType joinOp, PpsTableExpression right);

		/// <summary>Resolve table by name.</summary>
		/// <param name="tableName"></param>
		/// <returns></returns>
		protected abstract TTABLE ResolveTable(string tableName);

		/// <summary>Parse expression.</summary>
		/// <param name="expression"></param>
		public void Parse(string expression)
		{
			// t1 alias
			// t1 op t2 [ ]
			// ( )

			var p = new SimpleParser(this, expression);
			root = p.Parse();
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
	} // class PpsJoinExpression

	#endregion
}
