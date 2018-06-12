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
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using TecWare.DE.Stuff;
using TecWare.DE.Data;
using static TecWare.PPSn.Data.PpsDataHelper;

namespace TecWare.PPSn.Data
{
	#region -- enum PpsDataRowState ---------------------------------------------------

	/// <summary>Status der Zeile.</summary>
	public enum PpsDataRowState
	{
		/// <summary>Invalid row state</summary>
		Unknown = -1,
		/// <summary>The row is unchanged</summary>
		Unchanged = 0,
		/// <summary>The row is modified</summary>
		Modified = 1,
		/// <summary>The row is deleted</summary>
		Deleted = 2
	} // enum PpsDataRowState

	#endregion

	#region -- interface ICompareFulltext ---------------------------------------------

	/// <summary>Interface to compare with an fulltext expression.</summary>
	public interface ICompareFulltext
	{
		/// <summary>Find the text within the all attributes.</summary>
		/// <param name="text">Text to search.</param>
		/// <param name="startsWith">Text with Startswith</param>
		/// <returns></returns>
		bool SearchText(string text, bool startsWith);
	} // interface ICompareFulltext

	#endregion

	#region -- interface ICompareDateTime ---------------------------------------------

	/// <summary>Interface to compare with an date expression.</summary>
	public interface ICompareDateTime
	{
		/// <summary>Find the text within the all attributes.</summary>
		/// <param name="minDate"></param>
		/// <param name="maxDate"></param>
		/// <returns></returns>
		bool SearchDate(DateTime minDate, DateTime maxDate);
	} // interface ICompareDateTime

	#endregion

	#region -- class PpsDataRelatedFilter ---------------------------------------------

	/// <summary>View implementation for relations.</summary>
	public class PpsDataRelatedFilter : PpsDataFilter
	{
		private readonly PpsDataTableRelationDefinition relation;
		private readonly PpsDataRow parentRow;
		private readonly int parentColumnIndex;
		private readonly int childColumnIndex;

		/// <summary></summary>
		/// <param name="parentRow"></param>
		/// <param name="relation"></param>
		public PpsDataRelatedFilter(PpsDataRow parentRow, PpsDataTableRelationDefinition relation)
			 : base(parentRow.Table.DataSet.Tables[relation.ChildColumn.Table])
		{
			this.relation = relation;
			this.parentRow = parentRow;
			this.parentColumnIndex = relation.ParentColumn.Index;
			this.childColumnIndex = relation.ChildColumn.Index;

			if (parentColumnIndex < 0 || parentColumnIndex >= parentRow.Table.Columns.Count)
				throw new ArgumentOutOfRangeException("parentColumnIndex");
			if (childColumnIndex < 0 || childColumnIndex >= Table.Columns.Count)
				throw new ArgumentOutOfRangeException("childColumnIndex");

			Refresh();
		} // ctor

		/// <summary></summary>
		/// <param name="values"></param>
		/// <returns></returns>
		protected override object[] InitializeValues(object[] values)
		{
			values = base.InitializeValues(values);

			values[childColumnIndex] = parentRow[parentColumnIndex];

			return values;
		} // proc InitializeValues

		/// <summary></summary>
		/// <param name="row"></param>
		/// <returns></returns>
		protected sealed override bool FilterRow(PpsDataRow row)
			=> Object.Equals(parentRow[parentColumnIndex], row[childColumnIndex]);

		/// <summary>Parent row for the filter</summary>
		public PpsDataRow Parent => parentRow;
		/// <summary>Relation that builds the filter</summary>
		public PpsDataTableRelationDefinition Relation => relation;
	} // class PpsDataRelatedFilter

	#endregion

	#region -- class PpsDataRow -------------------------------------------------------

	/// <summary>DataRow implementation for DataTables.</summary>
	public class PpsDataRow : IDynamicMetaObjectProvider, IDataRow, INotifyPropertyChanged, ICustomTypeDescriptor
	{
		#region -- class NotSetValue --------------------------------------------------

		/// <summary>Interne Klasse für Current-Value die anzeigt, ob sich ein Wert zum Original hin geändert hat.</summary>
		private sealed class NotSetValue
		{
			public override string ToString()
			{
				return "NotSet";
			} // func ToString

			public override int GetHashCode() 
				=> typeof(NotSetValue).GetHashCode();

			public override bool Equals(object obj)
				=> obj is NotSetValue;
		} // class NotSetValue

		#endregion

		#region -- class PpsDataRowValueChangedItem -----------------------------------

		private class PpsDataRowValueChangedItem : IPpsUndoItem
		{
			private readonly PpsDataRow row;
			private readonly int columnIndex;
			private readonly object oldValue;
			private readonly object newValue;

			public PpsDataRowValueChangedItem(PpsDataRow row, int columnIndex, object oldValue, object newValue)
			{
				this.row = row;
				this.columnIndex = columnIndex;
				this.oldValue = oldValue;
				this.newValue = newValue;
			} // ctor

			public override string ToString()
				=> $"Undo ColumnChanged: {columnIndex}";

			private object GetOldValue()
				=> oldValue == NotSet ? row.originalValues[columnIndex] : oldValue;

			public void Freeze() { }

			public void Undo()
			{
				row.currentValues[columnIndex] = oldValue;
				row.OnValueChanged(columnIndex, newValue, GetOldValue());
			} // proc Undo

			public void Redo()
			{
				row.currentValues[columnIndex] = newValue;
				row.OnValueChanged(columnIndex, GetOldValue(), newValue);
			} // proc Redo
		} // class PpsDataRowValueChangedItem

		#endregion

		#region -- class PpsDataRowStateChangedItem -----------------------------------

		private class PpsDataRowStateChangedItem : IPpsUndoItem
		{
			private readonly PpsDataRow row;
			private readonly PpsDataRowState oldValue;
			private readonly PpsDataRowState newValue;

			public PpsDataRowStateChangedItem(PpsDataRow row, PpsDataRowState oldValue, PpsDataRowState newValue)
			{
				this.row = row;
				this.oldValue = oldValue;
				this.newValue = newValue;
			} // ctor

			public override string ToString()
				=> $"Undo RowState: {oldValue} -> {newValue}";

			public void Freeze() { }

			public void Undo()
			{
				row.RowState = oldValue;
			} // proc Undo

			public void Redo()
			{
				row.RowState = newValue;
			} // proc Redo
		} // class PpsDataRowStateChangedItem

		#endregion

		#region -- class PpsDataRowMetaObject -----------------------------------------

		/// <summary></summary>
		private abstract class PpsDataRowBaseMetaObject : DynamicMetaObject
		{
			public PpsDataRowBaseMetaObject(Expression expression, object value)
				: base(expression, BindingRestrictions.Empty, value)
			{
			} // ctor

			private BindingRestrictions GetRestriction()
			{
				Expression expr;
				Expression exprType;
				if (ItemInfo.DeclaringType == typeof(PpsDataRow))
				{
					expr = Expression.Convert(Expression, typeof(PpsDataRow));
					exprType = Expression.TypeIs(Expression, typeof(PpsDataRow));
				}
				else
				{
					expr = Expression.Field(Expression.Convert(Expression, ItemInfo.DeclaringType), RowFieldInfo);
					exprType = Expression.TypeIs(Expression, typeof(RowValues));
				}

				expr =
					Expression.AndAlso(
						exprType,
						Expression.Equal(
							Expression.Property(Expression.Field(expr, TableFieldInfo), PpsDataTable.tableDefinitionPropertyInfo),
							Expression.Constant(Row.table.TableDefinition, typeof(PpsDataTableDefinition))
						)
					);

				return BindingRestrictions.GetExpressionRestriction(expr);
			} // func GetRestriction

			private Expression GetIndexExpression(int columnIndex)
			{
				return Expression.MakeIndex(
					Expression.Convert(Expression, ItemInfo.DeclaringType),
					ItemInfo,
					new Expression[] { Expression.Constant(columnIndex) }
				);
			} // func GetIndexExpression

			private DynamicMetaObject BindDataColumn(string name, Expression defaultExpression)
			{
				// find the column and bind a index expression
				var columnIndex = Row.table.TableDefinition.FindColumnIndex(name);
				if (columnIndex >= 0)
				{
					var columnInfo = Row.Table.Columns[columnIndex];
					if (columnInfo.IsRelationColumn)
						return new DynamicMetaObject(
							Expression.Call(Expression.Convert(Expression, typeof(PpsDataRow)), GetParentRowMethodInfo, Expression.Constant(columnInfo)), 
							GetRestriction()
						);
					else
						return new DynamicMetaObject(GetIndexExpression(columnIndex), GetRestriction());
				}
				else // find a relation
				{
					PpsDataTableRelationDefinition relation;
					if (ItemInfo.DeclaringType == typeof(PpsDataRow) && (relation = Row.table.TableDefinition.Relations[name]) != null)  // find a relation
					{
						return new DynamicMetaObject(
							Expression.Call(Expression.Convert(Expression, typeof(PpsDataRow)), GetDefaultRelationMethodInfo, Expression.Constant(relation)),
							GetRestriction()
						);
					}
					else // return default expression
						return new DynamicMetaObject(defaultExpression, GetRestriction());
				}
			} // func BindDataColumn

			public override DynamicMetaObject BindGetMember(GetMemberBinder binder)
			{
				if (PpsDataHelper.IsStandardMember(LimitType, binder.Name))
					return base.BindGetMember(binder);

				return BindDataColumn(binder.Name, Expression.Constant(null, typeof(object)));
			} // func BindGetMember

			public override DynamicMetaObject BindSetMember(SetMemberBinder binder, DynamicMetaObject value)
			{
				if (PpsDataHelper.IsStandardMember(LimitType, binder.Name))
					return base.BindSetMember(binder, value);

				var columnIndex = Row.table.TableDefinition.FindColumnIndex(binder.Name);
				if (columnIndex >= 0)
				{
					return new DynamicMetaObject(
						Expression.Assign(GetIndexExpression(columnIndex), Expression.Convert(value.Expression, typeof(object))),
						GetRestriction().Merge(value.Restrictions)
					);
				}
				else
					return new DynamicMetaObject(
						Expression.Throw(
							Expression.New(Procs.ArgumentOutOfRangeConstructorInfo2, 
								new Expression[]
								{
									Expression.Constant(binder.Name),
									Expression.Constant(String.Format("Could not resolve column {0} in table {1}.", binder.Name, Row.Table.TableName))
								}
							)
						), GetRestriction());
			} // func BindSetMember

			public override DynamicMetaObject BindInvokeMember(InvokeMemberBinder binder, DynamicMetaObject[] args)
			{
				if (args.Length > 0 || PpsDataHelper.IsStandardMember(LimitType, binder.Name))
					return base.BindInvokeMember(binder, args);
				else
				{
					return BindDataColumn(binder.Name, Expression.Throw(Expression.New(Procs.ArgumentOutOfRangeConstructorInfo2,
						new Expression[]
						{
							Expression.Constant(binder.Name),
							Expression.Constant(String.Format("Could not resolve column {0} in table {1}.", binder.Name, Row.Table.TableName))
						}
					), typeof(object)));
				}
			} // func BindInvokeMember

			public override IEnumerable<string> GetDynamicMemberNames()
			{
				foreach (var col in Row.table.Columns)
					yield return col.Name;
			} // func GetDynamicMemberNames

			protected abstract PpsDataRow Row { get; }
			protected abstract PropertyInfo ItemInfo { get; }
		} // class PpsDataRowMetaObject

		/// <summary></summary>
		private sealed class PpsDataRowMetaObject : PpsDataRowBaseMetaObject
		{
			public PpsDataRowMetaObject(Expression expression, object value)
				: base(expression, value)
			{
			} // ctor

			protected override PpsDataRow Row { get { return (PpsDataRow)Value; } }
			protected override PropertyInfo ItemInfo { get { return ItemPropertyInfo; } }
		} // class PpsDataRowMetaObject

		/// <summary></summary>
		private sealed class PpsDataRowValuesMetaObject : PpsDataRowBaseMetaObject
		{
			public PpsDataRowValuesMetaObject(Expression expression, object value)
				: base(expression, value)
			{
			} // ctor

			protected override PpsDataRow Row { get { return ((RowValues)Value).Row; } }
			protected override PropertyInfo ItemInfo { get { return ValuesPropertyInfo; } }
		} // class PpsDataRowValuesMetaObject

		#endregion

		#region -- class RowValues ----------------------------------------------------

		/// <summary></summary>
		public abstract class RowValues : IDynamicMetaObjectProvider
		{
			private readonly PpsDataRow row;

			#region -- Ctor/Dtor ------------------------------------------------------

			/// <summary></summary>
			/// <param name="row"></param>
			protected RowValues(PpsDataRow row)
			{
				this.row = row;
			} // ctor

			/// <summary></summary>
			/// <param name="parameter"></param>
			/// <returns></returns>
			public DynamicMetaObject GetMetaObject(Expression parameter)
				=> new PpsDataRowValuesMetaObject(parameter, this);

			#endregion

			/// <summary>Ermöglicht den Zugriff auf die Spalte.</summary>
			/// <param name="columnIndex">Index der Spalte</param>
			/// <returns>Wert in der Spalte</returns>
			public abstract object this[int columnIndex] { get; set; }

			/// <summary>Ermöglicht den Zugriff auf die Spalte.</summary>
			/// <param name="columnName">Name der Spalte</param>
			/// <param name="throwException"></param>
			/// <returns>Wert in der Spalte</returns>
			public object this[string columnName, bool throwException = true]
			{
				get
				{
					var idx = Row.table.TableDefinition.FindColumnIndex(columnName, throwException);
					return idx >= 0 ? this[idx] : null;
				}
				set
				{
					var idx = Row.table.TableDefinition.FindColumnIndex(columnName, throwException);
					if (idx >= 0)
						this[idx] = value;
				}
			} // prop this

			/// <summary>Zugriff auf die Datenzeile.</summary>
			protected internal PpsDataRow Row => row;
		} // class RowValues

		#endregion

		#region -- class OriginalRowValues --------------------------------------------

		/// <summary></summary>
		private sealed class OriginalRowValues : RowValues
		{
			public OriginalRowValues(PpsDataRow row)
				: base(row)
			{
			} // ctor

			public override object this[int columnIndex]
			{
				get => Row.GetRowValueCore(columnIndex, true, false);
				set { throw new NotSupportedException(); }
			} // prop this
		} // class OriginalRowValues

		#endregion

		#region -- class CurrentRowValues ---------------------------------------------

		private sealed class CurrentRowValues : RowValues, INotifyPropertyChanged
		{
			public CurrentRowValues(PpsDataRow row)
				: base(row)
			{
			} // ctor

			public override object this[int columnIndex]
			{
				get => Row.GetRowValueCore(columnIndex, false, false);
				set
				{
					var columnInfo = Row.Table.Columns[columnIndex];
					if (columnInfo.IsExtended)
					{
						// set the value through the interface
						Row.SetCurrentValue(columnIndex, Row.originalValues[columnIndex], value);
					}
					else
					{
						// Convert the value to the expected type
						value = GetConvertedValue(columnInfo, value);

						// Is the value changed
						var oldValue = this[columnIndex];
						if (!Object.Equals(oldValue, value))
							Row.SetCurrentValue(columnIndex, oldValue, value);
					}
				}
			} // prop CurrentRowValues

			public event PropertyChangedEventHandler PropertyChanged
			{
				add { Row.PropertyChanged += value; }
				remove { Row.PropertyChanged -= value; }
			} // prop PropertyChanged
		} // class CurrentRowValues

		#endregion

		/// <summary>Placeholder for not setted values.</summary>
		public static readonly object NotSet = new NotSetValue();

		/// <summary>Wird ausgelöst, wenn sich eine Eigenschaft geändert hat</summary>
		public event PropertyChangedEventHandler PropertyChanged;

		private readonly PpsDataTable table;
		private readonly Lazy<IDataColumn[]> columnsArray;

		private PpsDataRowState rowState;
		private readonly OriginalRowValues orignalValuesProxy;
		private readonly CurrentRowValues currentValuesProxy;
		private object[] originalValues;
		private object[] currentValues;

		private object relationFilterLock = new object();
		private List<PpsDataRelatedFilter> relationFilter = null;
		private Dictionary<PpsDataColumnDefinition, PpsDataRow> parentRows = new Dictionary<PpsDataColumnDefinition, PpsDataRow>();

		#region -- Ctor/Dtor ----------------------------------------------------------

		private PpsDataRow(PpsDataTable table)
		{
			this.rowState = PpsDataRowState.Unchanged;

			this.table = table;
			this.columnsArray = new Lazy<IDataColumn[]>(() => this.table.TableDefinition.Columns.ToArray());
			this.orignalValuesProxy = new OriginalRowValues(this);
			this.currentValuesProxy = new CurrentRowValues(this);

			// Create the empty arrays for the column values
			this.originalValues = new object[table.Columns.Count];
			this.currentValues = new object[originalValues.Length];
		} // ctor

		/// <summary>Creates a new empty row.</summary>
		/// <param name="table">Table that owns the row.</param>
		/// <param name="rowState">Initial state of the row.</param>
		/// <param name="originalValues">Defined original/default values for the row.</param>
		/// <param name="currentValues">Initial values for the row.</param>
		internal PpsDataRow(PpsDataTable table, PpsDataRowState rowState, object[] originalValues, object[] currentValues)
			: this(table)
		{
			this.rowState = rowState;

			var length = table.Columns.Count;

			if (originalValues == null || originalValues.Length != length)
				throw new ArgumentOutOfRangeException("originalValues", "Not enough values for initialization.");

			if (currentValues == null)
				currentValues = new object[length];
			else if (currentValues.Length != length)
				throw new ArgumentOutOfRangeException("currentValues", "Not enough values for initialization.");

			for (var i = 0; i < length; i++)
			{
				var columnInfo = table.Columns[i];

				// set the originalValue
				var newOriginalValue = GetConvertedValue(columnInfo, originalValues[i]);
				table.Columns[i].OnColumnValueChanging(this, PpsDataColumnValueChangingFlag.Initial, null, ref newOriginalValue);

				// get the new value
				var newCurrentValue = currentValues[i] == null ? NotSet : (GetConvertedValue(columnInfo, currentValues[i]) ?? NotSet);
				if (newCurrentValue != NotSet)
					table.Columns[i].OnColumnValueChanging(this, PpsDataColumnValueChangingFlag.SetValue, newOriginalValue, ref newCurrentValue);

				// set the values
				this.originalValues[i] = newOriginalValue;
				this.currentValues[i] = newCurrentValue;
			}
		} // ctor

		internal PpsDataRow(PpsDataTable table, XElement xRow)
			: this(table)
		{
			// init 

			// update row state
			this.rowState = ReadRowState(xRow);

			UpdateRowValues(xRow);
		} // ctor

		/// <summary>Compare the data rows.</summary>
		/// <param name="obj"></param>
		/// <returns></returns>
		public override bool Equals(object obj)
			=> obj is IEquatable<PpsDataRow> obj2
				? obj2.Equals(this)
				: base.Equals(obj);

		/// <summary>Get hash code for this data row.</summary>
		/// <returns></returns>
		public override int GetHashCode() 
			=> base.GetHashCode();
		
		private static PpsDataRowState ReadRowState(XElement xRow)
		{
			var rowState = xRow.GetAttribute(xnDataRowState, 0); // optional state of the row

			if (!Enum.IsDefined(typeof(PpsDataRowState), rowState))
				throw new ArgumentException($"Unexpected value '{rowState}' for <{xnDataRow.LocalName} @{xnDataRowState}>.");

			return (PpsDataRowState)rowState;
		} // func ReadRowState

		internal void UpdateRow(XElement xRow)
		{
			// update row state
			var t = ReadRowState(xRow);
			if (RowState == PpsDataRowState.Deleted)
				this.RowState = t;
			else if (t != PpsDataRowState.Deleted && t != PpsDataRowState.Unknown)
				this.RowState = t;

			// update values
			UpdateRowValues(xRow);
		} // func UpdateRow

		private void UpdateRowValues(XElement xRow)
		{
			foreach (var column in Table.TableDefinition.Columns)
			{
				var xValue = xRow.Element(column.Name);
				var index = column.Index;

				if (column.IsExtended)
				{
					// create the extended value
					object v = null;
					column.OnColumnValueChanging(this, PpsDataColumnValueChangingFlag.Initial, null, ref v);
					var extendedValue = (IPpsDataRowExtendedValue)v;

					// set the values
					originalValues[index] = v;
					currentValues[index] = NotSet;

					// read the extend values
					extendedValue.Read(xValue);
				}
				else
				{
					// load the values
					var valueType = column.DataType;

					var xOriginal = xValue?.Element(xnDataRowValueOriginal);
					var xCurrent = xValue?.Element(xnDataRowValueCurrent);

					originalValues[index] = xOriginal == null || xOriginal.IsEmpty ? null : GetConvertedValue(column, xOriginal.Value);
					currentValues[index] = xCurrent == null ? NotSet : xCurrent.IsEmpty ? null : GetConvertedValue(column, xCurrent.Value);
				}

				// notify
				var newValue = this[index];
				Table.Columns[index].OnColumnValueChanged(this, null, newValue);
			}

			ClearRowCache();
		} // func UpdateRowValues

		DynamicMetaObject IDynamicMetaObjectProvider.GetMetaObject(Expression parameter)
			=> new PpsDataRowMetaObject(parameter, this);

		/// <summary></summary>
		/// <returns></returns>
		public override string ToString()
			=> $"{table.TableName}[pk: {this[table.TableDefinition.PrimaryKey.Index]?.ToString() ?? "<null>"}]";

		#endregion

		#region -- Commit, Reset, Remove ----------------------------------------------

		private void CheckForTable()
		{
			if (table == null)
				throw new InvalidOperationException();
		} // proc CheckForTable

		/// <summary>Removes all current values and restores the original loaded values. Supports Undo.</summary>
		public void Reset()
		{
			CheckForTable();

			// Reset all values
			var undo = GetUndoSink();
			using (var trans = undo?.BeginTransaction("Reset row"))
			{
				for (var i = 0; i < originalValues.Length; i++)
				{
					if(table.Columns[i].IsExtended)
					{
						if (originalValues[i] is IPpsDataRowSetGenericValue e)
						{
							e.Reset();
							OnValueChanged(i, originalValues[i], originalValues[i]);
						}
					}
					else if (currentValues[i] != NotSet)
					{
						var oldValue = currentValues[i];
						currentValues[i] = NotSet;
						undo?.Append(new PpsDataRowValueChangedItem(this, i, oldValue, NotSet));
						OnValueChanged(i, oldValue, originalValues[i]);
					}
				}

				// this row was delete restore it
				if (rowState == PpsDataRowState.Deleted)
					table.RestoreInternal(this);

				undo?.Append(new PpsDataRowStateChangedItem(this, rowState, PpsDataRowState.Unchanged));
				RowState = PpsDataRowState.Unchanged;

				trans?.Commit();
			}
		} // proc Reset

		/// <summary>Commits the current values to the orignal values. Breaks Undo, you must clear the Undostack.</summary>
		public void Commit()
		{
			CheckForTable();

			if (rowState == PpsDataRowState.Deleted)
			{
				table.RemoveInternal(this, true);
			}
			else
			{
				for (var i = 0; i < originalValues.Length; i++)
				{
					if(table.Columns[i].IsExtended)
					{
						if (originalValues[i] is IPpsDataRowSetGenericValue e)
							e.Commit();
					}
					else if (currentValues[i] != NotSet)
					{
						originalValues[i] = currentValues[i];
						currentValues[i] = NotSet;

					}
				}
				
				if (IsAdded)
					table.CommitRow(this);

				RowState = PpsDataRowState.Unchanged;
			}
		} // proc Commit

		/// <summary>Marks the current row as deleted. Supports Undo</summary>
		/// <returns><c>true</c>, wenn die Zeile als gelöscht markiert werden konnte.</returns>
		public bool Remove()
		{
			if (rowState == PpsDataRowState.Deleted || table == null)
				return false;

			var undo = GetUndoSink();
			using (var trans = undo?.BeginTransaction("Delete row."))
			{
				var r = table.RemoveInternal(this, false);
				
				undo?.Append(new PpsDataRowStateChangedItem(this, rowState, PpsDataRowState.Deleted));
				RowState = PpsDataRowState.Deleted;

				trans?.Commit();
				return r;
			}
		} // proc Remove

		#endregion

		#region -- Write --------------------------------------------------------------

		/// <summary>Schreibt den Inhalt der Datenzeile</summary>
		/// <param name="x"></param>
		internal void Write(XmlWriter x)
		{
			// Status
			x.WriteAttributeString(xnDataRowState.LocalName, ((int)rowState).ToString());
			if (IsAdded)
				x.WriteAttributeString(xnDataRowAdd.LocalName, "1");

			// Werte
			for (var i = 0; i < originalValues.Length; i++)
			{
				var columnInfo = Table.TableDefinition.Columns[i];
				x.WriteStartElement(columnInfo.Name);

				if (columnInfo.IsExtended)
				{
					var extendedValue = (IPpsDataRowExtendedValue)originalValues[i];
					if (!extendedValue.IsNull)
					{
						var xWriteTo = new XElement("t");
						extendedValue.Write(xWriteTo);
						foreach (var c in xWriteTo.Elements())
							c.WriteTo(x);
					}
				}
				else
				{
					if (originalValues[i] != null)
						WriteValue(x, xnDataRowValueOriginal, originalValues[i]);
					if (rowState != PpsDataRowState.Deleted && currentValues[i] != NotSet)
						WriteValue(x, xnDataRowValueCurrent, currentValues[i]);
				}
				x.WriteEndElement();
			}
		} // proc Write

		private void WriteValue(XmlWriter x, XName tag, object value)
		{
			x.WriteStartElement(tag.LocalName);
			if (value != null)
				x.WriteValue(Procs.ChangeType(value, typeof(string)));
			x.WriteEndElement();
		} // proc WriteValue

		#endregion

		#region -- Index Zugriff ------------------------------------------------------

		/// <summary>Get current undo sink, for this row.</summary>
		/// <returns></returns>
		public IPpsUndoSink GetUndoSink()
		{
			var sink = table.DataSet.UndoSink;
			return sink != null && !sink.InUndoRedoOperation ? sink : null;
		} // func GetUndoSink

		private static object GetGenericValue(object v)
			=> v is IPpsDataRowGetGenericValue t ? t.Value : v;

		/// <summary></summary>
		/// <param name="columnIndex"></param>
		/// <param name="originalValue"></param>
		/// <param name="rawValue"></param>
		/// <returns></returns>
		public object GetRowValueCore(int columnIndex, bool originalValue = false, bool rawValue = false)
		{
			if (originalValue)
			{
				var value = originalValues[columnIndex];
				if (!rawValue)
					value = GetGenericValue(value);
				return value;
			}
			else
			{
				var value = currentValues[columnIndex];
				value = value == NotSet ? originalValues[columnIndex] : value;
				if (!rawValue)
					value = GetGenericValue(value);
				return value;
			}
		} // func GetRowValueCore

		private void SetCurrentValue(int columnIndex, object oldValue, object value)
		{
			var column = table.Columns[columnIndex];

			// calculate caption and define the transaction
			var undo = GetUndoSink();
			using (var trans = undo?.BeginTransaction(String.Format(">{0}< geändert.", column.Meta.GetProperty(PpsDataColumnMetaData.Caption, column.Name))))
			{
				if (column.OnColumnValueChanging(this, PpsDataColumnValueChangingFlag.SetValue, oldValue, ref value)) // notify the value set, e.g. to child tables, or unique indizes
				{
					if (!column.IsExtended)
					{
						// change the value
						var realCurrentValue = currentValues[columnIndex];
						currentValues[columnIndex] = value;

						// update the undo stack
						column.OnColumnValueChanged(this, oldValue, value);
						undo?.Append(new PpsDataRowValueChangedItem(this, columnIndex, realCurrentValue, value));
					}

					// notify the value change
					OnValueChanged(columnIndex, oldValue, value); // Notify the value change 
					trans?.Commit();
				}
				else
					trans?.Rollback();
			}
		} // proc SetCurrentValue

		/// <summary>If the value of the row gets changed, this method is called.</summary>
		/// <param name="columnIndex"></param>
		/// <param name="oldValue"></param>
		/// <param name="value"></param>
		public virtual void OnValueChanged(int columnIndex, object oldValue, object value)
		{
			if (RowState == PpsDataRowState.Unchanged)
			{
				GetUndoSink()?.Append(new PpsDataRowStateChangedItem(this, PpsDataRowState.Unchanged, PpsDataRowState.Modified));
				RowState = PpsDataRowState.Modified;
			}

			// notify the change
			OnPropertyChanged(columnIndex, table.Columns[columnIndex].Name, oldValue, value);
		} // proc OnValueChanged

		#region -- OnPropertyChanged ------------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private class PpsDataRowPropertyChangedEvent : PpsDataChangedEvent
		{
			private readonly PpsDataRow row;
			private readonly string propertyName;
			private readonly object oldValue;
			private readonly object newValue;
			
			public PpsDataRowPropertyChangedEvent(PpsDataRow row, string propertyName, object oldValue, object newValue)
			{
				this.row = row;
				this.propertyName = propertyName;
				this.oldValue = oldValue;
				this.newValue = newValue;
			} // ctor

			public override void InvokeEvent()
			{
				row.table.DataSet.OnTableColumnValueChanged(row, propertyName, oldValue, newValue);
				row.InvokePropertyChanged(propertyName);
			} // proc InvokeEvent

			public override bool Equals(PpsDataChangedEvent ev)
			{
				if (ev == this)
					return true;
				else
				{
					var other = ev as PpsDataRowPropertyChangedEvent;
					return other != null ? 
						other.row == row && other.propertyName == propertyName && Object.Equals(other.newValue, newValue) :
						false;
				}
			} // func Same

			public override PpsDataChangeLevel Level => PpsDataChangeLevel.PropertyValue;
		} // class PpsDataRowPropertyChangedEvent

		/// <summary></summary>
		/// <param name="columnIndex"></param>
		/// <param name="propertyName"></param>
		/// <param name="oldValue"></param>
		/// <param name="newValue"></param>
		protected virtual void OnPropertyChanged(int columnIndex, string propertyName, object oldValue, object newValue)
		{
			table.DataSet.ExecuteEvent(new PpsDataRowPropertyChangedEvent(this, propertyName, oldValue, newValue));
			// mark row as modified
			table.OnRowModified(this, columnIndex, oldValue, newValue);
		} // proc OnPropertyChanged

		private void InvokePropertyChanged(string propertyName)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		#endregion

		/// <summary></summary>
		/// <param name="name"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public bool TryGetProperty(string name, out object value)
		{
			var idx = table.TableDefinition.FindColumnIndex(name);
			if (idx == -1)
			{
				value = null;
				return false;
			}
			else
			{
				value = currentValuesProxy[idx];
				return true;
			}
		} // func TryGetProperty

		/// <summary></summary>
		/// <param name="index"></param>
		/// <returns></returns>
		public bool IsValueModified(int index)
		{
			if (Table.Columns[index].IsExtended)
				return originalValues[index] is IPpsDataRowSetGenericValue g ? g.IsValueModified : false;
			else
				return currentValues[index] != NotSet;
		} // func IsValueModified

		/// <summary>Zugriff auf den aktuellen Wert.</summary>
		/// <param name="columnIndex">Spalte</param>
		/// <returns></returns>
		public object this[int columnIndex]
		{
			get { return currentValuesProxy[columnIndex]; }
			set { currentValuesProxy[columnIndex] = value; }
		} // prop this

		/// <summary>Zugriff auf den aktuellen Wert.</summary>
		/// <param name="columnName">Spalte</param>
		/// <param name="throwException"></param>
		/// <returns></returns>
		public object this[string columnName, bool throwException = true]
		{
			get { return currentValuesProxy[columnName, throwException]; }
			set { currentValuesProxy[columnName, throwException] = value; }
		} // prop this

		/// <summary>Zugriff auf die aktuellen Werte</summary>
		public RowValues Current => currentValuesProxy;
		/// <summary>Originale Werte, mit der diese Zeile initialisiert wurde</summary>
		public RowValues Original => orignalValuesProxy;

		/// <summary>Status der Zeile</summary>
		public PpsDataRowState RowState
		{
			get { return rowState; }
			private set
			{
				if (rowState != value)
				{
					var oldState = rowState;
					rowState = value;
					OnPropertyChanged(-1, "RowState", oldState, rowState);
				}
			}
		} // prop RowState

		/// <summary>Wurde die Datenzeile neu angefügt.</summary>
		public bool IsAdded => !table.OriginalRows.Contains(this);
		/// <summary>Is this row in the current row set.</summary>
		public bool IsCurrent => table.Contains(this);

		bool IDataRow.IsDataOwner => true;

		#endregion

		#region -- CreateRelation -----------------------------------------------------

		/// <summary>Creates a new view on data over an relation (parent/child relation)</summary>
		/// <param name="relation"></param>
		/// <returns></returns>
		public PpsDataFilter CreateRelation(PpsDataTableRelationDefinition relation)
			=> table.CreateRelationFilter(this, relation);

		/// <summary></summary>
		/// <param name="relation"></param>
		/// <returns></returns>
		public PpsDataFilter GetDefaultRelation(PpsDataTableRelationDefinition relation)
		{
			lock (relationFilterLock)
			{
				if (relationFilter == null)
					relationFilter = new List<PpsDataRelatedFilter>();

				// find a existing relation
				var i = relationFilter.FindIndex(c => c.Relation == relation);
				if (i >= 0 && relationFilter[i].IsDisposed)
				{
					relationFilter.RemoveAt(i);
					i = -1;
				}

				// return the relation
				if (i == -1)
				{
					var r = (PpsDataRelatedFilter)CreateRelation(relation);
					relationFilter.Add(r);
					return r;
				}
				else
					return relationFilter[i];
			}
		} // func GetDefaultRelation

		private void ClearRowCache()
		{
			lock (relationFilterLock)
			{
				parentRows.Clear();
				relationFilter?.Clear();
			}
		} // proc ClearRowCache

		internal void ClearParentRowCache(PpsDataColumnDefinition column)
		{
			lock (relationFilterLock)
				parentRows.Remove(column);
		} // func ClearParentRowCache

		/// <summary></summary>
		/// <param name="column"></param>
		/// <returns></returns>
		public PpsDataRow GetParentRow(PpsDataColumnDefinition column)
		{
			var val = this[column.Index];
			if (val != null)
			{
				if (!parentRows.TryGetValue(column, out var row))
				{
					var parentTable = Table.DataSet.Tables[column.ParentColumn.Table];
					if (column.ParentColumn.IsPrimaryKey)
						row = parentTable.FindKey(val);
					else
						row = parentTable.FindRows(column.ParentColumn, val).FirstOrDefault();

					parentRows[column] = row;
				}
				return row;
			}
			else
				return null;
		} // func GetParentRow

		#endregion

		#region -- ICustomTypeDescriptor members --------------------------------------

		AttributeCollection ICustomTypeDescriptor.GetAttributes()
			=> AttributeCollection.Empty;

		string ICustomTypeDescriptor.GetClassName()
			=> nameof(PpsDataRow);

		string ICustomTypeDescriptor.GetComponentName()
			=> nameof(PpsDataRow);

		TypeConverter ICustomTypeDescriptor.GetConverter()
			=> null;

		EventDescriptor ICustomTypeDescriptor.GetDefaultEvent()
			=> null;

		PropertyDescriptor ICustomTypeDescriptor.GetDefaultProperty()
			=> Table.TableDefinition.PropertyDescriptors.Find(Table.TableDefinition.PrimaryKey.Name, true);

		object ICustomTypeDescriptor.GetEditor(Type editorBaseType)
			=> null;

		EventDescriptorCollection ICustomTypeDescriptor.GetEvents()
			=> EventDescriptorCollection.Empty;

		EventDescriptorCollection ICustomTypeDescriptor.GetEvents(Attribute[] attributes)
			=> EventDescriptorCollection.Empty;
		
		PropertyDescriptorCollection ICustomTypeDescriptor.GetProperties()
			=> Table.TableDefinition.PropertyDescriptors;

		PropertyDescriptorCollection ICustomTypeDescriptor.GetProperties(Attribute[] attributes)
			=> Table.TableDefinition.PropertyDescriptors;

		object ICustomTypeDescriptor.GetPropertyOwner(PropertyDescriptor pd)
			=> this;
		
		#endregion

		/// <summary>Zugehörige Datentabelle</summary>
		public PpsDataTable Table => table;

		// todo: Implement missing functionality. Change PpsDataTable > PpsDataTableDefinition > PpsDataTableMetaCollection > PpsMetaCollection implementation. ?Expectation: "IDataColumn[] IDataColumns.Columns = table.Columns;"?
		IReadOnlyList<IDataColumn> IDataColumns.Columns => columnsArray.Value;

		// -- Static --------------------------------------------------------------

		private static readonly PropertyInfo RowStatePropertyInfo;
		private static readonly PropertyInfo ItemPropertyInfo;
		private static readonly PropertyInfo CurrentPropertyInfo;
		private static readonly PropertyInfo OriginalPropertyInfo;
		private static readonly FieldInfo TableFieldInfo;
		private static readonly MethodInfo ResetMethodInfo;
		private static readonly MethodInfo CommitMethodInfo;
		private static readonly MethodInfo GetDefaultRelationMethodInfo;
		private static readonly MethodInfo GetParentRowMethodInfo;

		private static readonly PropertyInfo ValuesPropertyInfo;
		private static readonly FieldInfo RowFieldInfo;

		#region -- sctor --------------------------------------------------------------

		static PpsDataRow()
		{
			var typeRow = typeof(PpsDataRow);
			RowStatePropertyInfo = Procs.GetProperty(typeRow, nameof(RowState));
			ItemPropertyInfo = Procs.GetProperty(typeRow, "Item", typeof(int));
			CurrentPropertyInfo = Procs.GetProperty(typeRow, nameof(Current));
			OriginalPropertyInfo = Procs.GetProperty(typeRow, nameof(Original));
			TableFieldInfo = typeRow.GetTypeInfo().GetDeclaredField(nameof(table));
			ResetMethodInfo = Procs.GetMethod(typeRow, nameof(Reset));
			CommitMethodInfo = Procs.GetMethod(typeRow, nameof(Commit));
			GetDefaultRelationMethodInfo = Procs.GetMethod(typeRow, nameof(GetDefaultRelation), typeof(PpsDataTableRelationDefinition));
			GetParentRowMethodInfo = Procs.GetMethod(typeRow, nameof(GetParentRow), typeof(PpsDataColumnDefinition));

			var typeValue = typeof(RowValues);
			ValuesPropertyInfo = Procs.GetProperty(typeValue, "Item", typeof(int));
			RowFieldInfo = typeValue.GetTypeInfo().GetDeclaredField("row");

			if (TableFieldInfo == null ||
					RowFieldInfo == null)
				throw new InvalidOperationException("Reflection failed (PpsDataRow)");
		} // sctor

		#endregion
	} // class PpsDataRow

	#endregion
}
