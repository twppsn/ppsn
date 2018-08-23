﻿#region -- copyright --
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
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using TecWare.DE.Data;
using TecWare.DE.Server;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;

namespace TecWare.PPSn.Server.Data
{
	#region -- class PpsDataSelector --------------------------------------------------

	/// <summary>Minimal function set for a selector.</summary>
	public abstract class PpsDataSelector : IDERangeEnumerable<IDataRow>, IDataRowEnumerable, IDataColumns
	{
		#region -- class AliasColumn --------------------------------------------------

		/// <summary></summary>
		public abstract class AliasColumn : IPpsColumnDescription
		{
			private readonly IPpsColumnDescription columnDescription;

			/// <summary></summary>
			/// <param name="nativeColumn"></param>
			/// <param name="aliasName"></param>
			public AliasColumn(IPpsColumnDescription nativeColumn, string aliasName)
			{
				Alias = aliasName ?? throw new ArgumentNullException(nameof(aliasName));

				this.columnDescription = nativeColumn ?? throw new ArgumentNullException(nameof(nativeColumn));
			} // ctor

			T IPpsColumnDescription.GetColumnDescription<T>()
				=> (columnDescription is IPpsColumnDescription f) ? f.GetColumnDescription<T>() : default(T);

			/// <summary></summary>
			public string Alias { get; }

			/// <summary></summary>
			public Type DataType => columnDescription.DataType;

			internal IPpsColumnDescription NativeColumnInfo => columnDescription;
			string IDataColumn.Name => Alias;
			
			IPropertyEnumerableDictionary IDataColumn.Attributes => columnDescription.Attributes;
		} // class AliasColumn

		#endregion

		#region -- class IndexAliasColumn ---------------------------------------------

		/// <summary></summary>
		public sealed class IndexAliasColumn : AliasColumn
		{
			/// <summary></summary>
			/// <param name="rowIndex"></param>
			/// <param name="nativeColumn"></param>
			/// <param name="aliasName"></param>
			public IndexAliasColumn(int rowIndex, IPpsColumnDescription nativeColumn, string aliasName)
				: base(nativeColumn, aliasName)
			{
				NativeIndex = rowIndex;
			} // ctor

			/// <summary></summary>
			public int NativeIndex { get; }
		} // class IndexAliasColumn

		#endregion

		private readonly IPpsConnectionHandle connection;
		private readonly AliasColumn[] columns;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary></summary>
		/// <param name="connection"></param>
		/// <param name="columns"></param>
		public PpsDataSelector(IPpsConnectionHandle connection, AliasColumn[] columns = null)
		{
			this.connection = connection ?? throw new ArgumentNullException(nameof(connection));
			this.columns = columns ?? GetAllColumns(); 
		} // ctor

		#endregion

		#region -- GetEnumerator ------------------------------------------------------

		IEnumerator IEnumerable.GetEnumerator()
			=> GetEnumeratorCore(0, Int32.MaxValue);

		IEnumerator<IDataRow> IEnumerable<IDataRow>.GetEnumerator()
			=> GetEnumeratorCore(0, Int32.MaxValue);

		/// <summary>Overwrite to execute selector</summary>
		/// <returns></returns>
		public IEnumerator<IDataRow> GetEnumerator()
			=> GetEnumeratorCore(0, Int32.MaxValue);

		/// <summary>Returns a enumerator for the range.</summary>
		/// <param name="start">Start of the enumerator</param>
		/// <param name="count">Number of elements that should be returned,</param>
		/// <returns></returns>
		public IEnumerator<IDataRow> GetEnumerator(int start, int count)
			=> GetEnumeratorCore(start, count);

		/// <summary></summary>
		/// <param name="start"></param>
		/// <param name="count"></param>
		/// <returns></returns>
		protected abstract IEnumerator<IDataRow> GetEnumeratorCore(int start, int count);

		#endregion

		#region -- ApplyOrder ---------------------------------------------------------

		/// <summary></summary>
		/// <param name="columnName"></param>
		/// <returns></returns>
		public virtual bool IsOrderDesc(string columnName)
			=> false;

		IDataRowEnumerable IDataRowEnumerable.ApplyOrder(IEnumerable<PpsDataOrderExpression> expressions, Func<string, string> lookupNative)
			=> ApplyOrder(expressions, lookupNative);

		/// <summary>Apply a filter expression to the selector..</summary>
		/// <param name="expression">Expression as an string.</param>
		/// <param name="lookupNative">Native lookup.</param>
		/// <returns>Return a new selector with the attached order.</returns>
		public PpsDataSelector ApplyOrder(string expression, Func<string, string> lookupNative = null)
			=> ApplyOrder(PpsDataOrderExpression.Parse(expression), lookupNative);

		/// <summary>Apply a filter expression to the selector..</summary>
		/// <param name="expressions">Order expressions.</param>
		/// <param name="lookupNative">Native lookup.</param>
		/// <returns>Return a new selector with the attached order.</returns>
		public virtual PpsDataSelector ApplyOrder(IEnumerable<PpsDataOrderExpression> expressions, Func<string, string> lookupNative = null)
			=> this;

		#endregion

		#region -- ApplyFilter --------------------------------------------------------

		IDataRowEnumerable IDataRowEnumerable.ApplyFilter(PpsDataFilterExpression expression, Func<string, string> lookupNative)
			=> ApplyFilter(expression, lookupNative);

		/// <summary>Apply a filter to the selector.</summary>
		/// <param name="expression">Filter expression as an string.</param>
		/// <param name="lookupNative">Native filter lookup.</param>
		/// <returns>Return a new selector with the attached filter.</returns>
		public PpsDataSelector ApplyFilter(string expression, Func<string, string> lookupNative)
			=> ApplyFilter(PpsDataFilterExpression.Parse(expression), lookupNative);

		/// <summary>Apply a filter to the selector.</summary>
		/// <param name="expression">Filter expression.</param>
		/// <param name="lookupNative">Native filter lookup.</param>
		/// <returns>Return a new selector with the attached filter.</returns>
		public virtual PpsDataSelector ApplyFilter(PpsDataFilterExpression expression, Func<string, string> lookupNative = null)
			=> this;

		#endregion

		#region -- ApplyColumns -------------------------------------------------------

		IDataRowEnumerable IDataRowEnumerable.ApplyColumns(IEnumerable<PpsDataColumnExpression> columns)
			=> ApplyColumns(columns);

		/// <summary>Apply column information to the selector.</summary>
		/// <param name="columns">Columns to return.</param>
		/// <returns>Return a new selector with the column set.</returns>
		public PpsDataSelector ApplyColumns(object columns)
			=> ApplyColumns(PpsDataColumnExpression.Parse(columns));

		/// <summary>Apply column information to the selector.</summary>
		/// <param name="columns">Columns to return.</param>
		/// <returns>Return a new selector with the column set.</returns>
		public PpsDataSelector ApplyColumns(IEnumerable<PpsDataColumnExpression> columns)
		{
			var columnList = new List<AliasColumn>();
			foreach (var col in columns)
			{
				// first lookup current alias
				var aliasColumn = this.columns.FirstOrDefault(c => String.Compare(c.Alias, col.Name, StringComparison.OrdinalIgnoreCase) == 0);
				var columnAlias = aliasColumn != null
						? CreateColumnAliasFromExisting(col, aliasColumn)
						: CreateColumnAliasFromNative(col);
				if (columnAlias == null)
					throw new ArgumentException($"Column could not assign ({col.Name}).");

				columnList.Add(columnAlias);
			}

			return ApplyColumnsCore(columnList.ToArray());
		} // func ApplyColumns

		/// <summary></summary>
		/// <param name="col"></param>
		/// <param name="aliasColumn"></param>
		/// <returns></returns>
		protected abstract AliasColumn CreateColumnAliasFromExisting(PpsDataColumnExpression col, AliasColumn aliasColumn);
			
		/// <summary></summary>
		/// <param name="col"></param>
		/// <returns></returns>
		protected abstract AliasColumn CreateColumnAliasFromNative(PpsDataColumnExpression col);

		/// <summary></summary>
		/// <param name="columns"></param>
		/// <returns></returns>
		protected abstract PpsDataSelector ApplyColumnsCore(AliasColumn[] columns);

		#endregion

		#region -- ApplyJoin ----------------------------------------------------------

		#region -- class CompareFields ------------------------------------------------

		private sealed class CompareFields
		{
			private readonly Tuple<int, int>[] compareColumns;

			public CompareFields(Tuple<int, int>[] compareColumns)
			{
				this.compareColumns = compareColumns ?? throw new ArgumentNullException(nameof(compareColumns));
			} // ctor

			public bool Compare(IDataRow left, IDataRow right)
			{
				for (var i = 0; i < compareColumns.Length; i++)
				{
					if (!Equals(left[compareColumns[i].Item1], right[compareColumns[i].Item2]))
						return false;
				}
				return true;
			} // func Compare
		} // class CompareFields

		#endregion
		
		/// <summary></summary>
		/// <param name="selectorName"></param>
		/// <param name="joinType"></param>
		/// <param name="statement"></param>
		/// <returns></returns>
		public PpsDataSelector ApplyJoin(string selectorName, string joinType, string statement)
			=> ApplyJoin(DataSource.CreateSelector(connection, selectorName), PpsDataJoinStatement.ParseJoinType(joinType, true), PpsDataJoinStatement.Parse(statement).ToArray());

		/// <summary>Create a join to an other dataselector.</summary>
		/// <param name="selector"></param>
		/// <param name="joinType"></param>
		/// <param name="statements"></param>
		/// <returns></returns>
		public virtual PpsDataSelector ApplyJoin(PpsDataSelector selector, PpsDataJoinType joinType, PpsDataJoinStatement[] statements)
		{
			var left = this.ApplyOrder(GetOrderExpressionFromStatements(statements, this, true));
			var right = selector.ApplyOrder(GetOrderExpressionFromStatements(statements, selector, false));

			return new PpsJoinDataSelector(connection, left, joinType, right, CreateEqualsFunctionFromStatements(left, right, statements), null);
		} // func ApplyJoin

		private static IEnumerable<PpsDataOrderExpression> GetOrderExpressionFromStatements(PpsDataJoinStatement[] statements, PpsDataSelector selector, bool useLeft)
		{
			foreach (var cur in statements)
			{
				yield return useLeft
				? new PpsDataOrderExpression(selector.IsOrderDesc(cur.Left), cur.Left)
				: new PpsDataOrderExpression(selector.IsOrderDesc(cur.Right), cur.Right);
			}
		} // func GetOrderExpressionFromStatements

		private static Func<IDataRow, IDataRow, bool> CreateEqualsFunctionFromStatements(PpsDataSelector leftSelector, PpsDataSelector rightSelector, PpsDataJoinStatement[] statements)
		{
			var compareFields = new List<Tuple<int, int>>();
			foreach (var cur in statements)
			{
				var leftColumnIndex = leftSelector.FindColumnIndex(cur.Left, true);
				var rightColumnIndex = rightSelector.FindColumnIndex(cur.Right, true);
				compareFields.Add(new Tuple<int, int>(leftColumnIndex, rightColumnIndex));
			}
			return new CompareFields(compareFields.ToArray()).Compare;
		} // func CreateEqualsFunctionFromStatements

		#endregion

		#region -- GetFieldDescription ------------------------------------------------

		/// <summary></summary>
		/// <returns></returns>
		protected abstract AliasColumn[] GetAllColumns();

		/// <summary></summary>
		/// <param name="columnIndex"></param>
		/// <returns></returns>
		public IPpsColumnDescription GetFieldDescription(int columnIndex)
			=> columns != null && columnIndex >= 0 && columnIndex < columns.Length ? columns[columnIndex] : null;

		/// <summary>Returns the field description for the name in the resultset</summary>
		/// <param name="columnName"></param>
		/// <returns></returns>
		public IPpsColumnDescription GetFieldDescription(string columnName)
			=> columns.FirstOrDefault(c => String.Compare(c.Alias, columnName, StringComparison.OrdinalIgnoreCase) == 0);

		#endregion

		/// <summary>by default we do not know the number of items</summary>
		public virtual int Count => -1;

		/// <summary>DataSource of the selector</summary>
		public PpsDataSource DataSource => connection.DataSource;
		/// <summary>Connection for the selector.</summary>
		public IPpsConnectionHandle Connection => connection;
		/// <summary>Expected columns of the selector</summary>
		public IReadOnlyList<IDataColumn> Columns => columns;
		/// <summary>Internal column list.</summary>
		protected AliasColumn[] AliasColumns => columns;
	} // class PpsDataSelector

	#endregion

	#region -- class PpsJoinDataSelector ----------------------------------------------

	/// <summary>Joins to selector to one.</summary>
	public sealed class PpsJoinDataSelector : PpsDataSelector
	{
		#region -- class JoinEnumerator -----------------------------------------------

		private sealed class JoinEnumerator : IEnumerator<IDataRow>, IDataRow, IDataColumns
		{
			private readonly PpsJoinDataSelector selector;
			private readonly IEnumerator<IDataRow> left;
			private readonly IEnumerator<IDataRow> right;
			private readonly bool innerJoin;
			private readonly Func<IDataRow, IDataRow, bool> equalRows;

			private IDataRow currentLeft = null;
			private IDataRow currentRight = null;
		
			public JoinEnumerator(PpsJoinDataSelector selector, IEnumerator<IDataRow> left, IEnumerator<IDataRow> right, bool innerJoin, Func<IDataRow, IDataRow, bool> equalRows)
			{
				this.selector = selector ?? throw new ArgumentNullException(nameof(selector));
				this.left = left ?? throw new ArgumentNullException(nameof(left));
				this.right = right ?? throw new ArgumentNullException(nameof(right));
				this.innerJoin = innerJoin;
				this.equalRows = equalRows ?? throw new ArgumentNullException(nameof(equalRows));
			} // ctor

			public void Dispose()
			{
				left.Dispose();
				right.Dispose();
			} // proc Dispose

			public void Reset()
			{
				left.Reset();
				right.Reset();
				currentLeft = null;
				currentRight = null;
			} // proc Reset

			public bool MoveNext()
			{
				if (currentLeft != null)
				{
					if (right.MoveNext())
					{
						currentRight = right.Current;
						if (equalRows(currentLeft, currentRight))
							return true;
					}
					else
						currentRight = null;
				}

				// first move left
				ReDoForInner:
				if (!left.MoveNext())
				{
					currentLeft = null;
					return false;
				}
				else // look right
				{
					if (currentRight != null && equalRows(currentLeft, currentRight))
						return true;
					else
					{
						RoDoForRight:
						if (right.MoveNext())
						{
							currentRight = right.Current;
							if (!equalRows(currentLeft, currentLeft))
								goto RoDoForRight;
							else
								return true;
						}
						else
						{
							currentRight = null;
							if (innerJoin)
								goto ReDoForInner;
							return true;
						}
					}
				}
			} // func MoveNext

			private object GetNativeValue(int index)
			{
				if (currentLeft == null)
					return null;
				else
				{
					var leftCount = currentLeft.Columns.Count;
					if (index < leftCount)
						return currentLeft[index];
					else if (currentRight == null)
						return null;
					else
					{
						index -= leftCount;
						if (index < currentRight.Columns.Count)
							return currentRight[index];
						else
							return null;
					}
				}
			} // func GetNativeValue

			bool IPropertyReadOnlyDictionary.TryGetProperty(string name, out object value)
				=> TryGetProperty(name, out value);

			private bool TryGetProperty(string name, out object value)
			{
				var idx = selector.FindColumnIndex(name);
				if (idx >= 0)
				{
					var col = (IndexAliasColumn)selector.Columns[idx];
					value = GetNativeValue(col.NativeIndex);
					return true;
				}
				else
				{
					value = null;
					return false;
				}
			} // func TryGetProperty

			object IDataValues.this[int index]
				=> GetNativeValue(((IndexAliasColumn)selector.Columns[index]).NativeIndex);
			
			object IDataRow.this[string columnName, bool throwException]
				=> TryGetProperty(columnName, out var tmp) ? tmp : (!throwException ? (object)null : throw new ArgumentOutOfRangeException(nameof(columnName), columnName, "Column not found."));

			bool IDataRow.IsDataOwner => false;

			IReadOnlyList<IDataColumn> IDataColumns.Columns => selector.Columns;

			object IEnumerator.Current => this;
			public IDataRow Current => this;
		} // class JoinEnumerator

		#endregion

		private readonly PpsDataSelector leftSelector;
		private readonly PpsDataSelector rightSelector;
		private readonly PpsDataJoinType joinType;
		private readonly Func<IDataRow, IDataRow, bool> equalRows;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary></summary>
		/// <param name="connection"></param>
		/// <param name="leftSelector"></param>
		/// <param name="joinType"></param>
		/// <param name="rightSelector"></param>
		/// <param name="equalRows"></param>
		/// <param name="columns"></param>
		public PpsJoinDataSelector(IPpsConnectionHandle connection, PpsDataSelector leftSelector, PpsDataJoinType joinType, PpsDataSelector rightSelector, Func<IDataRow, IDataRow, bool> equalRows, AliasColumn[] columns)
			: base(connection, columns)
		{
			if (joinType == PpsDataJoinType.None)
				joinType = PpsDataJoinType.Inner;
			else if (joinType == PpsDataJoinType.Right)
			{
				joinType = PpsDataJoinType.Left;
				var tmp = leftSelector;
				leftSelector = rightSelector;
				rightSelector = tmp;
			}

			this.leftSelector = leftSelector ?? throw new ArgumentNullException(nameof(leftSelector));
			this.joinType = joinType;
			this.rightSelector = rightSelector ?? throw new ArgumentNullException(nameof(rightSelector));
			this.equalRows = equalRows;
		} // ctor

		#endregion

		#region -- Columns ------------------------------------------------------------

		private (int, IPpsColumnDescription) GetNativeColumnInfo(string name)
		{
			var leftAliasIndex = leftSelector.FindColumnIndex(name);
			if (leftAliasIndex >= 0)
				return (leftAliasIndex, ((AliasColumn)leftSelector.Columns[leftAliasIndex]).NativeColumnInfo);
			else
			{
				var ofs = leftSelector.Columns.Count;
				var rightAliasIndex = rightSelector.FindColumnIndex(name);
				if (rightAliasIndex >= 0)
					return (rightAliasIndex + ofs, ((AliasColumn)rightSelector.Columns[rightAliasIndex]).NativeColumnInfo);
				else
					return (-1, null);
			}
		} // func GetNativeFieldDescription

		/// <summary></summary>
		/// <returns></returns>
		protected override AliasColumn[] GetAllColumns()
		{
			var leftColumnCount = leftSelector.Columns.Count;
			var rightColumnCount = rightSelector.Columns.Count;
			var aliasColumns = new AliasColumn[leftColumnCount + rightColumnCount];
			for (var i = 0; i < leftColumnCount; i++)
			{
				var col = (AliasColumn)leftSelector.Columns[i];
				aliasColumns[i] = new IndexAliasColumn(i, col.NativeColumnInfo, col.Alias);
			}

			for (var i = 0; i < rightColumnCount; i++)
			{
				var col = (AliasColumn)rightSelector.Columns[i];
				var j = i + leftColumnCount;
				aliasColumns[j] = new IndexAliasColumn(j, col.NativeColumnInfo, col.Alias);
			}
			return aliasColumns;
		} // func GetAllColumns

		/// <summary></summary>
		/// <param name="columns"></param>
		/// <returns></returns>
		protected override PpsDataSelector ApplyColumnsCore(AliasColumn[] columns)
			=> new PpsJoinDataSelector(Connection, leftSelector, joinType, rightSelector, equalRows, columns);

		/// <summary></summary>
		/// <param name="col"></param>
		/// <param name="aliasColumn"></param>
		/// <returns></returns>
		protected override AliasColumn CreateColumnAliasFromExisting(PpsDataColumnExpression col, AliasColumn aliasColumn)
		{
			if (aliasColumn is IndexAliasColumn indexAliasColumn)
			{
				if (col.HasAlias) // rename
					return new IndexAliasColumn(indexAliasColumn.NativeIndex, indexAliasColumn.NativeColumnInfo, col.Alias);
				else // select
					return indexAliasColumn;
			}
			else
				throw new InvalidOperationException(); // wrong type
		} // func CreateColumnAliasFromExisting

		/// <summary></summary>
		/// <param name="col"></param>
		/// <returns></returns>
		protected override AliasColumn CreateColumnAliasFromNative(PpsDataColumnExpression col)
		{
			var (nativeIndex, columnInfo) = GetNativeColumnInfo(col.Name);
			if (columnInfo == null)
				return null;

			return new IndexAliasColumn(nativeIndex, columnInfo, col.HasAlias ? col.Alias : columnInfo.Name);
		} // func CreateColumnAliasFromNative

		#endregion

		/// <summary></summary>
		/// <param name="start"></param>
		/// <param name="count"></param>
		/// <returns></returns>
		protected override IEnumerator<IDataRow> GetEnumeratorCore(int start, int count)
			=> Procs.GetRangeEnumerator(new JoinEnumerator(this, leftSelector.GetEnumerator(), rightSelector.GetEnumerator(), joinType == PpsDataJoinType.Inner, equalRows), start, count);
	} // class PpsJoinDataSelector

	#endregion

	#region -- class PpsGenericSelector<T> --------------------------------------------

	/// <summary>Creates a selection for a typed list.</summary>
	/// <typeparam name="T"></typeparam>
	public sealed class PpsGenericSelector<T> : PpsDataSelector
	{
		private readonly string viewId;
		private readonly IEnumerable<T> enumerable;
		private readonly PpsApplication application;

		private readonly Lazy<IDataColumn[]> nativeColumns = new Lazy<IDataColumn[]>(GenericDataRowEnumerator<T>.GetColumnInfo);

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary></summary>
		/// <param name="connection"></param>
		/// <param name="viewId"></param>
		/// <param name="enumerable"></param>
		/// <param name="columns"></param>
		public PpsGenericSelector(IPpsConnectionHandle connection, string viewId, IEnumerable<T> enumerable, AliasColumn[] columns = null) 
			: base(connection, columns)
		{
			this.viewId = viewId;
			this.enumerable = enumerable;
			this.application = connection.DataSource.GetService<PpsApplication>(true);
		} // ctor

		#endregion

		#region -- Columns ------------------------------------------------------------

		/// <summary></summary>
		/// <returns></returns>
		protected override AliasColumn[] GetAllColumns()
		{
			var application = DataSource.GetService<PpsApplication>(true);
			var nativeColumns = this.nativeColumns.Value;
			var aliasColumns = new AliasColumn[nativeColumns.Length];
			for (var i = 0; i < aliasColumns.Length; i++)
			{
				var col = nativeColumns[i];
				aliasColumns[i] = new IndexAliasColumn(i, col.ToColumnDescription(application.GetFieldDescription(viewId + "." + col.Name, false)), col.Name);
			}
			return aliasColumns;
		} // func GetAllColumns

		/// <summary></summary>
		/// <param name="name"></param>
		/// <returns></returns>
		private (int columnIndex, IPpsColumnDescription field) GetNativeColumnInfo(string name)
		{
			var nc = nativeColumns.Value;
			for (var i = 0; i < nc.Length; i++)
			{
				var col = nc[i];
				if (String.Compare(col.Name, name, StringComparison.OrdinalIgnoreCase) == 0)
				{
					var fieldDescription = application.GetFieldDescription(viewId + "." + col.Name, false);
					return (i, col.ToColumnDescription(fieldDescription));
				}
			}
			return (-1, null);
		} // func GetNativeColumnInfo

		/// <summary></summary>
		/// <param name="col"></param>
		/// <param name="aliasColumn"></param>
		/// <returns></returns>
		protected override AliasColumn CreateColumnAliasFromExisting(PpsDataColumnExpression col, AliasColumn aliasColumn)
		{
			if (aliasColumn is IndexAliasColumn indexAliasColumn)
			{
				if (col.HasAlias) // rename
					return new IndexAliasColumn(indexAliasColumn.NativeIndex, indexAliasColumn.NativeColumnInfo, col.Alias);
				else // select
					return indexAliasColumn;
			}
			else
				throw new InvalidOperationException(); // wrong type
		} // func CreateColumnAliasFromExisting

		/// <summary></summary>
		/// <param name="col"></param>
		/// <returns></returns>
		protected override AliasColumn CreateColumnAliasFromNative(PpsDataColumnExpression col)
		{
			var (nativeIndex, columnInfo) = GetNativeColumnInfo(col.Name);
			if (columnInfo == null)
				return null;

			return new IndexAliasColumn(nativeIndex, columnInfo, col.HasAlias ? col.Alias : columnInfo.Name);
		} // func CreateColumnAliasFromNative

		/// <summary></summary>
		/// <param name="columns"></param>
		/// <returns></returns>
		protected override PpsDataSelector ApplyColumnsCore(AliasColumn[] columns)
			=> new PpsGenericSelector<T>(Connection, viewId, enumerable, columns);

		#endregion

		/// <summary></summary>
		/// <param name="start"></param>
		/// <param name="count"></param>
		/// <returns></returns>
		protected override IEnumerator<IDataRow> GetEnumeratorCore(int start, int count)
			=> Procs.GetRangeEnumerator(new GenericDataRowEnumerator<T>(enumerable.GetEnumerator()), start, count);

		/// <summary></summary>
		/// <param name="expression"></param>
		/// <param name="lookupNative"></param>
		/// <returns></returns>
		public override PpsDataSelector ApplyFilter(PpsDataFilterExpression expression, Func<string, string> lookupNative = null)
		{
			var predicate = PpsDataFilterVisitorLambda.CompileTypedFilter<T>(expression);
			return new PpsGenericSelector<T>(Connection, viewId, enumerable.Where(new Func<T, bool>(predicate)));
		} // func ApplyFilter

		/// <summary></summary>
		/// <param name="expressions"></param>
		/// <param name="lookupNative"></param>
		/// <returns></returns>
		public override PpsDataSelector ApplyOrder(IEnumerable<PpsDataOrderExpression> expressions, Func<string, string> lookupNative = null)
			=> throw new NotSupportedException(); // base.ApplyOrder(expressions, lookupNative);

		/// <summary></summary>
		public override int Count
			=> enumerable is IList l ? l.Count : base.Count;
	} // class PpsGenericSelector

	#endregion
}
