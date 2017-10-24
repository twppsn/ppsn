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
	#region -- enum PpsDataJoinType -----------------------------------------------------

	/// <summary>Join type</summary>
	public enum PpsDataJoinType
	{
		None,
		Inner,
		Left,
		Right
	} // enum JoinType

	#endregion

	#region -- class PpsJoinException ---------------------------------------------------

	public class PpsJoinException : FormatException
	{
		private readonly int position;

		public PpsJoinException(int position, string message, Exception innerException = null)
			: base(message, innerException)
		{
			this.position = position;
		} // ctor

		public int Position => position;
	} // class PpsJoinException

	#endregion

	#region -- class PpsJoinExpression --------------------------------------------------

	public abstract class PpsDataJoinExpression<TTABLE>
		where TTABLE : IDataColumns
	{
		public abstract class PpsExpressionPart
		{
		} // class PpsExpressionPart

		public sealed class PpsTableExpression : PpsExpressionPart
		{
			private readonly TTABLE table;
			private readonly string alias;

			public PpsTableExpression(TTABLE table, string alias)
			{
				this.table = table;
				this.alias = alias;
			} // ctor

			public TTABLE Table => table;
			public string Alias => alias;
		} // class PpsTableExpression

		public sealed class PpsJoinExpression : PpsExpressionPart
		{
			private readonly PpsExpressionPart left;
			private readonly PpsDataJoinType type;
			private readonly PpsExpressionPart right;

			private readonly string onStatement;

			public PpsJoinExpression(PpsExpressionPart left, PpsDataJoinType type, PpsExpressionPart right, string onStatement)
			{
				this.left = left;
				this.type = type;
				this.right = right;
				this.onStatement = onStatement ?? throw new ArgumentNullException(nameof(onStatement));
			} // ctor

			public PpsExpressionPart Left => left;
			public PpsExpressionPart Right => right;
			public string Statement => onStatement;
			public PpsDataJoinType Type => type;
		} // class PpsJoinExpression

		public abstract class PpsJoinVisitor<TRESULT>
		{
			public abstract TRESULT CreateTableStatement(TTABLE table, string alias);
			public abstract TRESULT CreateJoinStatement(TRESULT leftExpression, PpsDataJoinType type, TRESULT rightExpression, string on);

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

			public TRESULT Visit(PpsDataJoinExpression<TTABLE> expr)
				=> Visit(expr.root);
		} // class PpsJoinVisitor

		#region -- class SimpleParser -----------------------------------------

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

		protected abstract string CreateOnStatement(PpsTableExpression left, PpsDataJoinType joinOp, PpsTableExpression right);

		protected abstract TTABLE ResolveTable(string tableName);

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
