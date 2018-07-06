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
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Data;
using TecWare.DE.Stuff;
using static TecWare.PPSn.Data.PpsDataHelper;

namespace TecWare.PPSn.Data
{
	#region -- enum PpsRowVersion -----------------------------------------------------

	/// <summary>Defines the row versions.</summary>
	public enum PpsRowVersion
	{
		/// <summary>The original loaded rows.</summary>
		Original,
		/// <summary>All current active rows..</summary>
		Current,
		/// <summary>All rows, current and deleted rows.</summary>
		All
	} // enum PpsRowVersion

	#endregion

	#region -- enum PpsDataTableMetaData ----------------------------------------------

	/// <summary>Predefined meta-data for a data table.</summary>
	public enum PpsDataTableMetaData
	{
	} // enum PpsDataTableMetaData

	#endregion

	#region -- enum PpsRelationType ---------------------------------------------------

	/// <summary>Type of a relation.</summary>
	public enum PpsRelationType
	{
		/// <summary>Default behaviour, Cascade.</summary>
		None,
		/// <summary>Throws an exception</summary>
		Restricted,
		/// <summary>Deletes all related rows.</summary>
		Cascade,
		/// <summary>Sets the foreign key to zero.</summary>
		SetNull
	} // enum PpsRelationType

	#endregion

	#region -- enum PpsTablePrimaryKeyType --------------------------------------------

	/// <summary>Classification for the primary key.</summary>
	public enum PpsTablePrimaryKeyType
	{
		/// <summary>Database generated key (greater than zero).</summary>
		Database,
		/// <summary>Server generated key (lower than zero).</summary>
		Server,
		/// <summary>Local generated key (lower than zero).</summary>
		Local
	} // enum PpsTablePrimaryKeyType

	#endregion

	#region -- class PpsDataTableRelationDefinition -----------------------------------

	/// <summary>Definition of a relation between two tables (only 1 to n is possible).</summary>
	public sealed class PpsDataTableRelationDefinition
	{
		private readonly string name;
		private readonly PpsRelationType type;
		private readonly PpsDataColumnDefinition parentColumn;
		private readonly PpsDataColumnDefinition childColumn;

		internal PpsDataTableRelationDefinition(string name, PpsRelationType type, PpsDataColumnDefinition parentColumn, PpsDataColumnDefinition childColumn)
		{
			this.name = name;
			this.type = type;
			this.parentColumn = parentColumn;
			this.childColumn = childColumn;
		} // ctor

		/// <summary>Debug representation of the relation.</summary>
		/// <returns></returns>
		public override string ToString()
			=> $"{parentColumn} -> {name}";

		/// <summary>Name of the relation.</summary>
		public string Name => name;
		/// <summary>Relation type.</summary>
		public PpsRelationType Type => type;
		/// <summary>Parent column of the relation.</summary>
		public PpsDataColumnDefinition ParentColumn => parentColumn;
		/// <summary>Child column of the relation.</summary>
		public PpsDataColumnDefinition ChildColumn => childColumn;
	} // class PpsDataTableRelationDefinition

	#endregion

	#region -- class PpsDataTableDefinition -------------------------------------------

	/// <summary>Definition of a data table.</summary>
	public abstract class PpsDataTableDefinition : IDataColumns
	{
		#region -- WellKnownTypes -----------------------------------------------------

		/// <summary>Definiert die bekannten Meta Informationen.</summary>
		private static readonly Dictionary<string, Type> wellknownMetaTypes = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

		#endregion

		#region -- class PpsDataTableMetaCollection -----------------------------------

		/// <summary></summary>
		public class PpsDataTableMetaCollection : PpsMetaCollection
		{
			/// <summary></summary>
			public PpsDataTableMetaCollection()
			{
			} // ctor

			/// <summary></summary>
			/// <param name="clone"></param>
			public PpsDataTableMetaCollection(PpsDataTableMetaCollection clone)
				: base(clone)
			{
			} // ctor

			/// <summary></summary>
			/// <typeparam name="T"></typeparam>
			/// <param name="key"></param>
			/// <param name="default"></param>
			/// <returns></returns>
			public T GetProperty<T>(PpsDataTableMetaData key, T @default)
				=> PropertyDictionaryExtensions.GetProperty<T>(this, key.ToString(), @default);

			/// <summary></summary>
			public override IReadOnlyDictionary<string, Type> WellknownMetaTypes => wellknownMetaTypes;

			/// <summary></summary>
			public static PpsDataTableMetaCollection Empty { get; } = new PpsDataTableMetaCollection();
		} // class PpsDataTableMetaCollection

		#endregion

		#region -- class PpsDataTableColumnCollection ---------------------------------

		/// <summary></summary>
		public sealed class PpsDataTableColumnCollection : ReadOnlyCollection<PpsDataColumnDefinition>
		{
			/// <summary></summary>
			/// <param name="columns"></param>
			public PpsDataTableColumnCollection(IList<PpsDataColumnDefinition> columns)
				: base(columns)
			{
			} // ctor

			/// <summary></summary>
			/// <param name="columnName"></param>
			/// <param name="throwException"></param>
			/// <returns></returns>
			public PpsDataColumnDefinition this[string columnName, bool throwException = false]
			{
				get
				{
					var column = this.FirstOrDefault(c => String.Compare(c.Name, columnName, StringComparison.OrdinalIgnoreCase) == 0);
					if (column == null && throwException)
						throw new ArgumentOutOfRangeException("columnName", $"Columns '{columnName}' not found.");
					return column;
				}
			} // prop this
		} // class PpsDataTableColumnCollection

		#endregion

		#region -- class PpsDataTabbleRelationCollection ------------------------------

		/// <summary></summary>
		public sealed class PpsDataTabbleRelationCollection : ReadOnlyCollection<PpsDataTableRelationDefinition>
		{
			/// <summary></summary>
			/// <param name="relations"></param>
			public PpsDataTabbleRelationCollection(IList<PpsDataTableRelationDefinition> relations)
				: base(relations)
			{
			} // ctor

			/// <summary></summary>
			/// <param name="relationName"></param>
			/// <param name="throwException"></param>
			/// <returns></returns>
			public PpsDataTableRelationDefinition this[string relationName, bool throwException = false]
			{
				get
				{
					var relation = this.FirstOrDefault(c => String.Compare(c.Name, relationName, StringComparison.OrdinalIgnoreCase) == 0);
					if (relation == null && throwException)
						throw new ArgumentOutOfRangeException("relationName", $"Columns '{relationName}' not found.");
					return relation;
				}
			} // prop this
		} // class PpsDataTabbleRelationCollection

		#endregion

		#region -- class PpsColumnPropertyDescriptor ----------------------------------

		private sealed class PpsColumnPropertyDescriptor : PropertyDescriptor
		{
			private readonly PpsDataColumnDefinition column;
			private readonly PropertyChangedEventHandler propertyChangedHandler;

			public PpsColumnPropertyDescriptor(PpsDataColumnDefinition column)
				: base(column.Name, Array.Empty<Attribute>())
			{
				this.column = column;

				propertyChangedHandler = (sender, e) =>
				{
					if (String.Compare(column.Name, e.PropertyName, StringComparison.OrdinalIgnoreCase) == 0)
						OnValueChanged(sender, EventArgs.Empty);
				};
			} // ctor

			public override void AddValueChanged(object component, EventHandler handler)
			{
				if (!(component is PpsDataRow))
					return;

				var row = (PpsDataRow)component;
				if (GetValueChangedHandler(component) == null)
					row.PropertyChanged += propertyChangedHandler;

				base.AddValueChanged(component, handler);
			} // proc AddValueChanged

			public override void RemoveValueChanged(object component, EventHandler handler)
			{
				if (!(component is PpsDataRow))
					return;

				base.RemoveValueChanged(component, handler);

				var row = (PpsDataRow)component;
				if (GetValueChangedHandler(component) == null)
					row.PropertyChanged -= propertyChangedHandler;
			} // proc RemoveValueChanged

			private static Type GetNullableType(Type dataType, bool isNullable)
			{
				if (isNullable && dataType.IsValueType && dataType != typeof(void))
					return typeof(Nullable<>).MakeGenericType(dataType);
				return dataType;
			} // func GetNullableType

			public override Type ComponentType
				=> typeof(PpsDataRow);

			public override bool IsReadOnly
				=> column.IsExtended ? !HasColumnSetter : false;

			public override Type PropertyType
			{
				get
				{
					if (column.IsRelationColumn)
						return typeof(PpsDataRow);
					else if (column.IsExtended)
					{
						if (HasColumnSetter)
							return typeof(object);
						else
							return column.DataType;
					}
					else
						return GetNullableType(column.DataType, IsNullable);
				}
			} // prop PropertyType

			public override bool CanResetValue(object component)
			{
				var row = (PpsDataRow)component;
				return row.Original[column.Index] != null;
			} // func CanResetValue

			public override void ResetValue(object component)
			{
				var row = (PpsDataRow)component;
				row[column.Index] = row.Original[column.Index]; // todo: reset for extended
			} // proc ResetValue

			public override object GetValue(object component)
			{
				var row = (PpsDataRow)component;
				if (column.IsRelationColumn)
					return row.GetParentRow(column);
				else
					return row[column.Index];
			} // func GetValue

			public override void SetValue(object component, object value)
			{
				var row = (PpsDataRow)component;
				row[column.Index] = value;
			} // proc SetValue

			public override bool ShouldSerializeValue(object component)
			{
				var row = (PpsDataRow)component;
				return row.IsValueModified(column.Index);
			} // func ShouldSerializeValue

			private bool HasColumnSetter
				=> typeof(IPpsDataRowSetGenericValue).IsAssignableFrom(column.DataType);

			public bool IsNullable => column.Meta.GetProperty(PpsDataColumnMetaData.Nullable, false);
		} // class PpsColumnPropertyDescriptor

		#endregion

		private readonly PpsDataSetDefinition dataset;
		private readonly string name;

		private List<PpsDataColumnDefinition> columns;
		private PpsDataTableColumnCollection columnCollection;
		private List<PpsDataTableRelationDefinition> relations;
		private PpsDataTabbleRelationCollection relationCollection;

		private PpsDataColumnDefinition primaryKeyColumn;

		private readonly Lazy<PropertyDescriptorCollection> properties;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary></summary>
		/// <param name="dataset"></param>
		/// <param name="tableName"></param>
		protected PpsDataTableDefinition(PpsDataSetDefinition dataset, string tableName)
		{
			this.name = tableName;
			this.dataset = dataset;
			this.columns = new List<PpsDataColumnDefinition>();
			this.columnCollection = new PpsDataTableColumnCollection(columns);
			this.relations = new List<PpsDataTableRelationDefinition>();
			this.relationCollection = new PpsDataTabbleRelationCollection(relations);

			this.properties = new Lazy<PropertyDescriptorCollection>(
				() => new PropertyDescriptorCollection(
					(
						from c in Columns
						select new PpsColumnPropertyDescriptor(c)
					).ToArray()
				)
			);
		} // ctor

		/// <summary></summary>
		/// <param name="dataset"></param>
		/// <param name="clone"></param>
		protected PpsDataTableDefinition(PpsDataSetDefinition dataset, PpsDataTableDefinition clone)
			: this(dataset, clone.Name)
		{
			// clone columns
			foreach (var column in clone.Columns)
				columns.Add(column.Clone(this));

			// clone relations
			foreach (var relation in clone.Relations)
				AddRelation(relation.Name, relation.Type, Columns[relation.ParentColumn.Name, true], Columns[relation.ChildColumn.Name, true]);
		} // ctor

		/// <summary>Clone the table definition.</summary>
		/// <param name="dataset"></param>
		/// <returns></returns>
		public abstract PpsDataTableDefinition Clone(PpsDataSetDefinition dataset);

		/// <summary>Ends the initialization.</summary>
		protected internal virtual void EndInit()
		{
			if (primaryKeyColumn == null)
				throw new ArgumentException($"No primary column is defined (at table '{Name}').");
		} // proc EndInit

		internal void SetPrimaryKey(PpsDataColumnDefinition column)
		{
			if (column.Table != this)
				throw new ArgumentException("Invalid table.");

			if (primaryKeyColumn == null)
				primaryKeyColumn = column;
			else
				throw new ArgumentException($"Only one primary column is allowed (at table '{Name}').");
		} // proc SetPrimaryKey

		#endregion

		/// <summary>Create a instance of this table.</summary>
		/// <param name="dataset">Dataset for the table.</param>
		/// <returns>Data table instance</returns>
		public virtual PpsDataTable CreateDataTable(PpsDataSet dataset)
		{
			if (!IsInitialized)
				throw new ArgumentException($"{nameof(EndInit)} from table {name} is not called.");

			return new PpsDataTable(this, dataset);
		} // func CreateDataTable

		/// <summary>Add's a new column to the table.</summary>
		/// <param name="column">Column definition.</param>
		protected void AddColumn(PpsDataColumnDefinition column)
		{
			if (IsInitialized)
				throw new InvalidOperationException($"Can not add column '{column.Name}', because table '{name}' is initialized.");
			if (column == null)
				throw new ArgumentNullException();
			if (column.Table != this)
				throw new ArgumentException();

			var index = FindColumnIndex(column.Name);
			if (index >= 0)
				columns[index] = column;
			else
				columns.Add(column);
		} // proc AddColumn

		/// <summary>Creates a new relation between two columns.</summary>
		/// <param name="relationName">Name of the relation</param>
		/// <param name="relationType">Type of the relation.</param>
		/// <param name="parentColumn">Parent column, that must belong to the current table definition.</param>
		/// <param name="childColumn">Child column.</param>
		public void AddRelation(string relationName, PpsRelationType relationType, PpsDataColumnDefinition parentColumn, PpsDataColumnDefinition childColumn)
		{
			if (IsInitialized)
				throw new InvalidOperationException($"Can not add relation '{relationName}', because table '{name}' is initialized.");
			if (String.IsNullOrEmpty(relationName))
				throw new ArgumentNullException("relationName");
			if (parentColumn == null)
				throw new ArgumentNullException("parentColumn");
			if (childColumn == null)
				throw new ArgumentNullException("childColumn");

			if (parentColumn.Table != this)
				throw new ArgumentException("parentColumn must belong to the current table.");

			var relation = new PpsDataTableRelationDefinition(relationName, relationType, parentColumn, childColumn);
			relations.Add(relation);

			childColumn.SetParentRelation(relation);
		} // proc AddRelation

		/// <summary>Find the column index.</summary>
		/// <param name="columnName">Name of the column.</param>
		/// <param name="throwException"><c>true</c>, and the function raises an exception, if the column does not exist.</param>
		/// <returns>Column index or -1</returns>
		public int FindColumnIndex(string columnName, bool throwException = false)
		{
			var index = columns.FindIndex(c => String.Compare(c.Name, columnName, StringComparison.OrdinalIgnoreCase) == 0);
			if (index == -1 && throwException)
				throw new ArgumentOutOfRangeException("columnName", $"Column'{Name}.{columnName}' not found.");
			return index;
		} // func FindColumnIndex

		/// <summary>Owner of the table.</summary>
		public PpsDataSetDefinition DataSet => dataset;
		/// <summary>Name of the table.</summary>
		public string Name => name;
		/// <summary>User name for table.</summary>
		public string DisplayName => Meta.TryGetProperty<string>("displayName", out var tmp) ? tmp : name;
		/// <summary>Is the table initialized.</summary>
		public bool IsInitialized => dataset.IsInitialized;
		/// <summary>Column definition</summary>
		public PpsDataTableColumnCollection Columns => columnCollection;

		IReadOnlyList<IDataColumn> IDataColumns.Columns => columnCollection;

		/// <summary>Attached relations</summary>
		public PpsDataTabbleRelationCollection Relations => relationCollection;
		/// <summary>The column that identifies every row.</summary>
		public PpsDataColumnDefinition PrimaryKey => primaryKeyColumn;

		internal PropertyDescriptorCollection PropertyDescriptors => properties.Value;

		/// <summary>Access to the table meta-data.</summary>
		public abstract PpsDataTableMetaCollection Meta { get; }
	} // class PpsDataTableDefinition

	#endregion

	#region -- event ColumnValueChangedEventHandler -----------------------------------

	/// <summary>Event arguments for a changed value.</summary>
	public class ColumnValueChangedEventArgs : EventArgs
	{
		/// <summary></summary>
		/// <param name="row"></param>
		/// <param name="columnIndex"></param>
		/// <param name="oldValue"></param>
		/// <param name="newValue"></param>
		public ColumnValueChangedEventArgs(PpsDataRow row, int columnIndex, object oldValue, object newValue)
		{
			this.Row = row;
			this.ColumnIndex = columnIndex;
			this.OldValue = oldValue;
			this.NewValue = newValue;
		} // ctor

		/// <summary>Row</summary>
		public PpsDataRow Row { get; }
		/// <summary>Index of the column</summary>
		public int ColumnIndex { get; }
		/// <summary>Old value</summary>
		public object OldValue { get; }
		/// <summary>New value</summary>
		public object NewValue { get; }
	} // class ColumnValueChangedEventArgs

	/// <summary>Defines the delegate for the change value event.</summary>
	/// <param name="sender"></param>
	/// <param name="e"></param>
	public delegate void ColumnValueChangedEventHandler(object sender, ColumnValueChangedEventArgs e);

	#endregion

	#region -- interface IPpsDataView -------------------------------------------------

	/// <summary>Basic definition of a data view, that is based on data rows.</summary>
	public interface IPpsDataView : IList, IDataColumns, IEnumerable<PpsDataRow>, INotifyCollectionChanged
	{
		/// <summary>Creates a new unattached data row, that could be add.</summary>
		/// <param name="originalValues">Original values for the row.</param>
		/// <param name="currentValues">Current values for the row.</param>
		/// <returns>Unattached data row.</returns>
		PpsDataRow NewRow(object[] originalValues, object[] currentValues);
		/// <summary>Remove a datarow from the view.</summary>
		/// <param name="row">Data row, that should be removed.</param>
		/// <returns><c>true</c>, if remnoved.</returns>
		bool Remove(PpsDataRow row);

		/// <summary>The table the view is based on.</summary>
		PpsDataTable Table { get; }
	} // interface IPpsDataView

	#endregion

	#region -- class PpsDataTable -----------------------------------------------------

	/// <summary>Table for rows.</summary>
	public class PpsDataTable : IPpsDataView, IDynamicMetaObjectProvider, INotifyPropertyChanged
	{
		#region -- class PpsDataTableAddChangeItem ------------------------------------

		private class PpsDataTableAddChangeItem : IPpsUndoItem
		{
			private readonly PpsDataTable table;
			private readonly PpsDataRow rowAdded;

			public PpsDataTableAddChangeItem(PpsDataTable table, PpsDataRow rowAdded)
			{
				this.table = table;
				this.rowAdded = rowAdded;
			} // ctor

			public void Freeze() { }

			public void Redo()
				=> table.AddInternal(false, rowAdded);

			public void Undo()
				=> table.RemoveInternal(rowAdded, false);
		} // class PpsDataTableAddChangeItem

		#endregion

		#region -- class PpsDataTableRemoveChangeItem ---------------------------------

		private class PpsDataTableRemoveChangeItem : IPpsUndoItem
		{
			private readonly PpsDataTable table;
			private readonly PpsDataRow rowDeleted;

			public PpsDataTableRemoveChangeItem(PpsDataTable table, PpsDataRow rowDeleted)
			{
				this.table = table;
				this.rowDeleted = rowDeleted;
			} // ctor

			public void Freeze() { }

			public void Redo()
				=> table.RemoveInternal(rowDeleted, false);

			public void Undo()
				=> table.AddInternal(false, rowDeleted);
		} // class PpsDataTableAddChangeItem

		#endregion

		#region -- class PpsDataTableMetaObject ---------------------------------------

		private class PpsDataTableMetaObject : DynamicMetaObject
		{
			public PpsDataTableMetaObject(Expression expr, object value)
				: base(expr, BindingRestrictions.Empty, value)
			{
			} // ctor

			private BindingRestrictions GetBindingRestrictions(PpsDataTable table)
			{
				return BindingRestrictions.GetExpressionRestriction(
					Expression.AndAlso(
						Expression.TypeIs(Expression, typeof(PpsDataTable)),
						Expression.Equal(
							Expression.Property(Expression.Convert(Expression, typeof(PpsDataTable)), tableDefinitionPropertyInfo),
							Expression.Constant(table.TableDefinition)
						)
					)
				);
			} // func GetBindingRestrictions

			private DynamicMetaObject BindColumnOrMeta(string name, bool generateException)
			{
				var table = (PpsDataTable)Value;
				var columnIndex = table.TableDefinition.FindColumnIndex(name);
				if (columnIndex == -1)
				{
					return table.TableDefinition.Meta == null
						? new DynamicMetaObject(Expression.Constant(null, typeof(object)), GetBindingRestrictions(table))
						: new DynamicMetaObject(table.TableDefinition.Meta.GetMetaConstantExpression(name, generateException), GetBindingRestrictions(table));
				}
				else
				{
					return new DynamicMetaObject(
						Expression.MakeIndex(
							Expression.Property(
								Expression.Convert(Expression, typeof(PpsDataTable)),
								columnsPropertyInfo
							),
							readOnlyCollectionIndexPropertyInfo,
							new Expression[] { Expression.Constant(columnIndex) }
						),
						GetBindingRestrictions(table)
					);
				}
			} // func DynamicMetaObject

			public override DynamicMetaObject BindGetMember(GetMemberBinder binder)
				=> IsStandardMember(LimitType, binder.Name)
					? base.BindGetMember(binder)
					: BindColumnOrMeta(binder.Name, false);

			public override DynamicMetaObject BindInvokeMember(InvokeMemberBinder binder, DynamicMetaObject[] args)
				=> args.Length > 0 || IsStandardMember(LimitType, binder.Name)
					? base.BindInvokeMember(binder, args)
					: BindColumnOrMeta(binder.Name, true);
		} // class PpsDataTableMetaObject

		#endregion

		/// <summary>Notifies changes of the list.</summary>
		public event NotifyCollectionChangedEventHandler CollectionChanged;
		/// <summary>Notifies changes of single values.</summary>
		public event ColumnValueChangedEventHandler ColumnValueChanged;
		/// <summary>Notifies the change of a table property.</summary>
		public event PropertyChangedEventHandler PropertyChanged;

		private PpsDataTableDefinition tableDefinition;   // definition of this table
		private PpsDataSet dataset;                       // owner of this table

		private PpsDataRow emptyRow;
		private List<PpsDataRow> rows = new List<PpsDataRow>();         // all rows
		private List<PpsDataRow> originalRows = new List<PpsDataRow>(); // all initial loaded rows
		private List<PpsDataRow> currentRows = new List<PpsDataRow>();  // all current active rows

		private ReadOnlyCollection<PpsDataRow> rowsView;
		private ReadOnlyCollection<PpsDataRow> rowsOriginal;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary>Creates a new table</summary>
		/// <param name="tableDefinition"></param>
		/// <param name="dataset"></param>
		protected internal PpsDataTable(PpsDataTableDefinition tableDefinition, PpsDataSet dataset)
		{
			this.dataset = dataset ?? throw new ArgumentNullException();
			this.tableDefinition = tableDefinition;

			this.emptyRow = new PpsDataRow(this, PpsDataRowState.Unchanged, new object[tableDefinition.Columns.Count], null);
			this.rowsView = new ReadOnlyCollection<PpsDataRow>(rows);
			this.rowsOriginal = new ReadOnlyCollection<PpsDataRow>(originalRows);
		} // ctor

		DynamicMetaObject IDynamicMetaObjectProvider.GetMetaObject(Expression parameter)
			=> new PpsDataTableMetaObject(parameter, this);

		/// <summary>Create a relation view.</summary>
		/// <param name="row">Base row for the relation.</param>
		/// <param name="relation">Relation.</param>
		/// <returns></returns>
		public virtual PpsDataFilter CreateRelationFilter(PpsDataRow row, PpsDataTableRelationDefinition relation)
			=> new PpsDataRelatedFilter(row, relation);

		#endregion

		#region -- Collection Changed -------------------------------------------------

		#region -- class PpsDataRowChangedEvent ---------------------------------------

		private abstract class PpsDataRowChangedEvent : PpsDataChangedEvent
		{
			public PpsDataRowChangedEvent(PpsDataTable table, PpsDataRow row)
			{
				this.Table = table ?? throw new ArgumentNullException(nameof(table));
				this.Row = row;
			} // ctor

			public PpsDataTable Table { get; }
			public PpsDataRow Row { get; }
		} // class PpsDataRowChangedEvent

		#endregion

		#region -- class PpsDataRowAddedChangedEvent ----------------------------------

		private class PpsDataRowAddedChangedEvent : PpsDataRowChangedEvent
		{
			public PpsDataRowAddedChangedEvent(PpsDataTable table, PpsDataRow row)
				: base(table, row)
			{
			} // ctor

			public override string ToString()
				=> $"Add: {Table.TableName} -> {Row}";

			public override void InvokeEvent()
			{
				Table.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, Row, Table.currentRows.IndexOf(Row)));
				Table.dataset.OnTableRowAdded(Table, Row);
			} // proc InvokeEvent
			
			public override bool Equals(PpsDataChangedEvent ev)
			{
				if (ev == this)
					return true;
				else if (ev is PpsDataTableResetChangedEvent re && re.Table == Table)
					return true;
				else
					return ev is PpsDataRowAddedChangedEvent other ? other.Row == Row : false;
			} // func Equals

			public override PpsDataChangeLevel Level => PpsDataChangeLevel.RowAdded;
		} // class PpsDataRowAddedChangedEvent

		#endregion

		#region -- class PpsDataRowRemovedChangedEvent --------------------------------

		private sealed class PpsDataRowRemovedChangedEvent : PpsDataRowChangedEvent
		{
			private readonly int oldIndex;

			public PpsDataRowRemovedChangedEvent(PpsDataTable table, PpsDataRow row, int oldIndex)
				: base(table, row)
			{
				this.oldIndex = oldIndex;
			} // ctor

			public override string ToString()
				=> $"Remove: {Table.TableName} -> {Row}";

			public override void InvokeEvent()
			{
				Table.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, Row, oldIndex));
				Table.dataset.OnTableRowDeleted(Table, Row);
			} // proc InvokeEvent

			public override bool Equals(PpsDataChangedEvent ev)
			{
				if (ev == this)
					return true;
				else if (ev is PpsDataTableResetChangedEvent re && re.Table == Table)
					return true;
				else
					return ev is PpsDataRowRemovedChangedEvent other ? other.Row == Row : false;
			} // func Equals

			public override PpsDataChangeLevel Level => PpsDataChangeLevel.RowRemoved;
		} // class PpsDataRowRemovedChangedEvent

		#endregion

		#region -- class PpsDataTableResetChangedEvent --------------------------------

		private sealed class PpsDataTableResetChangedEvent : PpsDataRowChangedEvent
		{	
			public PpsDataTableResetChangedEvent(PpsDataTable table)
				: base(table, null)
			{
			} // ctor

			public override string ToString()
				=> $"Reset: {Table.TableName}";

			public override void InvokeEvent()
			{
				Table.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
				Table.dataset.OnTableChanged(Table);
			} // proc InvokeEvent

			public override bool Equals(PpsDataChangedEvent ev)
				=> ev == this
					|| (ev is PpsDataRowChangedEvent other ? other.Table == Table : false);

			public override PpsDataChangeLevel Level => PpsDataChangeLevel.TableReset;
		} // class PpsDataTableResetChangedEvent

		#endregion

		#region -- class PpsDataRowModifiedChangedEvent -------------------------------

		private sealed class PpsDataRowModifiedChangedEvent : PpsDataRowChangedEvent
		{
			private readonly int columnIndex;
			private readonly object oldValue;
			private readonly object newValue;

			public PpsDataRowModifiedChangedEvent(PpsDataTable table, PpsDataRow row, int columnIndex, object oldValue, object newValue)
				: base(table, row)
			{
				this.columnIndex = columnIndex;
				this.oldValue = oldValue;
				this.newValue = newValue;
			} // ctor

			public override void InvokeEvent()
			{
				Table.dataset.OnTableRowChanged(Table, Row);
				Table.InvokeColumnValueChanged(new ColumnValueChangedEventArgs(Row, columnIndex, oldValue, newValue));
			} // proc InvokeEvent

			public override bool Equals(PpsDataChangedEvent ev)
			{
				if (ev == this)
					return true;
				else
				{
					var other = ev as PpsDataRowModifiedChangedEvent;
					return other != null ?
						other.Row == Row && other.columnIndex == columnIndex && Object.Equals(other.newValue, newValue) :
						false;
				}
			} // func Same

			public override PpsDataChangeLevel Level => PpsDataChangeLevel.RowModified;
		} // class PpsDataRowModifiedChangedEvent

		#endregion

		#region -- class PpsDataTableChangedEvent -------------------------------------

		private sealed class PpsDataTableChangedEvent : PpsDataChangedEvent
		{
			private readonly PpsDataTable table;

			public PpsDataTableChangedEvent(PpsDataTable table)
			{
				this.table = table;
			} // ctor

			public override void InvokeEvent()
				=> table.dataset.OnTableChanged(table);

			public override bool Equals(PpsDataChangedEvent ev)
			{
				if (ev == this)
					return true;
				else
				{
					var other = ev as PpsDataTableChangedEvent;
					return other != null ?
						other.table == table :
						false;
				}
			} // func Same

			public override PpsDataChangeLevel Level => PpsDataChangeLevel.TableModifed;
		} // class PpsDataTableChangedEvent

		#endregion

		#region -- class PpsDataTablePropertyChangedEvent -----------------------------

		private sealed class PpsDataTablePropertyChangedEvent : PpsDataChangedEvent
		{
			private readonly PpsDataTable table;
			private readonly string propertyName;

			public PpsDataTablePropertyChangedEvent(PpsDataTable table, string propertyName)
			{
				this.table = table;
				this.propertyName = propertyName;
			} // ctor

			public override void InvokeEvent()
				=> table.InvokePropertyChanged(propertyName);

			public override bool Equals(PpsDataChangedEvent ev)
			{
				if (ev == this)
					return true;
				else
				{
					var other = ev as PpsDataTablePropertyChangedEvent;
					return other != null ?
						other.table == table && other.propertyName == propertyName :
						false;
				}
			} // func Same

			public override PpsDataChangeLevel Level => PpsDataChangeLevel.TableModifed;
		} // class PpsDataTablePropertyChangedEvent

		#endregion

		/// <summary>Notifies if a rows is added.</summary>
		/// <param name="row">The new row</param>
		protected virtual void OnRowAdded(PpsDataRow row)
		{
			if (rows.Count == 1)
				OnPropertyChanged(nameof(First));

			for (var i = 0; i < TableDefinition.Columns.Count; i++)
			{
				if (row.GetRowValueCore(i, rawValue: true) is IPpsDataRowExtendedEvents t)
					t.OnRowAdded();
			}

			dataset.ExecuteEvent(new PpsDataRowAddedChangedEvent(this, row));
			OnTableChanged();
		} // proc OnRowAdded

		/// <summary>Notifies if a row is removed.</summary>
		/// <param name="row"></param>
		/// <param name="oldIndex"></param>
		protected virtual void OnRowRemoved(PpsDataRow row, int oldIndex)
		{
			if (oldIndex == 0)
				OnPropertyChanged(nameof(First));

			for (var i = 0; i < TableDefinition.Columns.Count; i++)
			{
				if (row.GetRowValueCore(i, rawValue: true) is IPpsDataRowExtendedEvents t)
					t.OnRowRemoved();
			}

			dataset.ExecuteEvent(new PpsDataRowRemovedChangedEvent(this, row, -1)); // fire with -1 to support batches (the internal check of ListCollectionView is on the current state *arg*)
			OnTableChanged();
		} // proc OnRowModified

		/// <summary>Notifies if a row is mnodified.</summary>
		/// <param name="row"></param>
		/// <param name="columnIndex"></param>
		/// <param name="oldValue"></param>
		/// <param name="newValue"></param>
		protected internal virtual void OnRowModified(PpsDataRow row, int columnIndex, object oldValue, object newValue)
		{
			dataset.ExecuteEvent(new PpsDataRowModifiedChangedEvent(this, row, columnIndex, oldValue, newValue));
			OnTableChanged();
		} // proc OnDataChanged

		private void InvokeColumnValueChanged(ColumnValueChangedEventArgs e)
			=> ColumnValueChanged?.Invoke(this, e);

		/// <summary>Gets called if something was changed in the table data.</summary>
		protected virtual void OnTableChanged()
		{
			dataset.ExecuteEvent(new PpsDataTableChangedEvent(this));
			dataset.ExecuteDataChanged();
		} // proc OnTableChanged

		/// <summary>Property of the table was changed.</summary>
		/// <param name="propertyName"></param>
		protected virtual void OnPropertyChanged(string propertyName)
			=> dataset.ExecuteEvent(new PpsDataTablePropertyChangedEvent(this, propertyName));

		private void InvokePropertyChanged(string propertyName)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		/// <summary>Notifies if the collection of current rows is changed.</summary>
		/// <param name="e"></param>
		protected virtual void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
			=> CollectionChanged?.Invoke(this, e);

		#endregion

		#region -- Add, Remove, Reset, Commit -----------------------------------------

		private IPpsUndoSink GetUndoSink()
		{
			var sink = dataset.UndoSink;
			return sink != null && !sink.InUndoRedoOperation ? sink : null;
		} // func GetUndoSink

		internal PpsDataRow AddInternal(bool isOriginal, PpsDataRow row)
		{
			if (row.Table != this)
				throw new InvalidOperationException();

			var undo = GetUndoSink();
			using (var trans = undo?.BeginTransaction("Add row"))
			{
				// add the line
				rows.Add(row);

				// add this as an original row
				if (isOriginal)
					originalRows.Add(row);

				// update current rows, if not deleted
				if (row.RowState != PpsDataRowState.Deleted)
				{
					currentRows.Add(row);
					undo?.Append(new PpsDataTableAddChangeItem(this, row));

					OnRowAdded(row);
				}

				trans?.Commit();
			}

			return row;
		} // proc Add

		/// <summary>Stellt einen gelöschten Eintrag wieder her</summary>
		/// <param name="row"></param>
		internal bool RestoreInternal(PpsDataRow row)
		{
			var originalIndex = originalRows.IndexOf(row);
			var currentIndex = currentRows.IndexOf(row);
			if (currentIndex == -1)
			{
				if (originalIndex == -1 || row.Table == null)
					return false;

				currentRows.Add(row);

				GetUndoSink()?.Append(new PpsDataTableAddChangeItem(this, row));
				OnRowAdded(row);
			}
			return true;
		} // proc RestoreInternal

		private void RemoveRelatedRows(PpsDataRow row)
		{
			foreach (var r in tableDefinition.Relations)
			{
				if (r.ParentColumn.Table == tableDefinition)
				{
					var parentValue = row[r.ParentColumn.Index];
					var childTable = dataset.Tables[r.ChildColumn.Table];
					var childColumnIndex = r.ChildColumn.Index;

					for (var i = childTable.Count - 1; i >= 0; i--)
					{
						if (Object.Equals(childTable[i][childColumnIndex], parentValue))
						{
							switch (r.Type)
							{
								case PpsRelationType.Restricted:
									throw new PpsDataTableForeignKeyRestrictionException(row, childTable[i]);
								case PpsRelationType.SetNull:
									childTable[i][childColumnIndex] = null;
									break;
								default:
									childTable.RemoveAt(i);
									break;
							}
						}
					} // for
				}
			} // foreach
		} // proc RemoveRelatedRows		

		/// <summary></summary>
		/// <param name="row"></param>
		/// <param name="removeOriginal"><c>true</c>, für ein tatsächliches Entfernen.</param>
		/// <returns>Wurde der Eintrag gelöscht</returns>
		internal bool RemoveInternal(PpsDataRow row, bool removeOriginal)
		{
			var r = false;

			if (row.Table != this)
				throw new InvalidOperationException();

			var undo = GetUndoSink();
			using (var trans = undo?.BeginTransaction("Remove row"))
			{
				// remove the entry from the current list
				var oldIndex = currentRows.IndexOf(row);
				if (oldIndex != -1)
				{
					RemoveRelatedRows(row); // check related rows

					// index might be changed
					oldIndex = currentRows.IndexOf(row);
					currentRows.Remove(row);

					// undo manager
					undo?.Append(new PpsDataTableRemoveChangeItem(this, row));

					// event
					OnRowRemoved(row, oldIndex);
					r = true;
				}

				// remove the row also from the original rows
				var originalIndex = originalRows.IndexOf(row);
				if (originalIndex == -1) // it is a new added line
					rows.Remove(row);
				else // it is original loaded
				{
					if (removeOriginal)
					{
						rows.Remove(row);
						r = originalRows.Remove(row);
					}
				}

				trans?.Commit();
				return r;
			}
		} // proc RemoveInternal

		internal void ClearInternal()
		{
			// fire reset
			dataset.ExecuteEvent(new PpsDataTableResetChangedEvent(this));

			// remove rows
			for (var i = 0; i < currentRows.Count; i++)
				OnRowRemoved(currentRows[i], i);

			rows.Clear();
			currentRows.Clear();
			originalRows.Clear();
		} // proc ClearInternal

		/// <summary>Create a datarow-value-array from a property dictionary.</summary>
		/// <param name="properties">Member for the datarow.</param>
		/// <returns>Value array.</returns>
		public object[] GetDataRowValues(IPropertyReadOnlyDictionary properties)
		{
			var values = new object[Columns.Count];
			for (var i = 0; i < Columns.Count; i++)
				values[i] = properties.TryGetProperty(Columns[i].Name, out var v) ? v : null;
			return values;
		} // func GetDataRowValues

		/// <summary>Create a datarow-value-array from a lua table.</summary>
		/// <param name="table">Table member for the row.</param>
		/// <returns>Value array.</returns>
		public object[] GetDataRowValues(LuaTable table)
		{
			var values = new object[Columns.Count];
			for (var i = 0; i < Columns.Count; i++)
				values[i] = table.GetMemberValue(Columns[i].Name);
			return values;
		} // func GetDataRowValues

		/// <summary>Create a datarow-value-array from a value array.</summary>
		/// <param name="values">Array of values or <c>null</c>.</param>
		/// <returns>Value array.</returns>
		public object[] CreateDataRowValuesArray(object[] values = null)
		{
			if (values == null || values.Length == 0) // no values 
			{
				values = new object[Columns.Count];
			}
			else if (values.Length != Columns.Count)
			{
				var n = new object[Columns.Count];
				Array.Copy(values, 0, n, 0, Math.Min(values.Length, n.Length));
				values = n;
			}
			return values;
		} // func CheckValueArray

		private PpsDataRow NewRow(object[] originalValues, object[] currentValues)
			=> new PpsDataRow(this, PpsDataRowState.Modified, CreateDataRowValuesArray(originalValues), CreateDataRowValuesArray(currentValues));

		/// <summary>Add a row from a table.</summary>
		/// <param name="values">Members that will be assigned to the datarow columns.</param>
		/// <returns>Added data row.</returns>
		public PpsDataRow Add(LuaTable values)
			=> AddInternal(false, NewRow(GetDataRowValues(values), null));

		/// <summary>Add a row from a property dictionary.</summary>
		/// <param name="properties">Properties, that will be assigned to the datarow columns.</param>
		/// <returns>Added data row.</returns>
		public PpsDataRow Add(IPropertyReadOnlyDictionary properties)
			=> AddInternal(false, NewRow(GetDataRowValues(properties), null));

		/// <summary>Add a row from a value array.</summary>
		/// <param name="values">Value array, that will be assigned by index.</param>
		/// <returns>Added data row.</returns>
		public PpsDataRow Add(params object[] values)
		{
			if (values.Length == 1) // some languages may choose the wrong overload.
			{
				if (values[0] is LuaTable)
					return Add((LuaTable)values[0]);
				else if (values[1] is IPropertyReadOnlyDictionary)
					return Add((IPropertyReadOnlyDictionary)values[0]);
			}
			return AddInternal(false, NewRow(values, null));
		} // func Add

		private void AddRangeDataRows(IEnumerable<IDataRow> newRows)
		{
			var columnMapping = (int[])null;

			foreach (var r in newRows)
			{
				if (columnMapping == null) // create mapping, we assume all rows have an equal column set
				{
					columnMapping = new int[Columns.Count];
					for (var i = 0; i < columnMapping.Length; i++)
						columnMapping[i] = r.FindColumnIndex(Columns[i].Name, false);
				}

				// create values array
				var values = new object[columnMapping.Length];
				for (var i = 0; i < columnMapping.Length; i++)
				{
					var idx = columnMapping[i];
					values[i] = idx == -1 ? null : r[idx];
				}

				// add row
				AddInternal(false, NewRow(null, values));
			}
		} // proc AddRangeDataRows

		/// <summary>Add multiple rows from property dictionaries.</summary>
		/// <param name="newRows">Row collection.</param>
		public void AddRange(IEnumerable<IPropertyReadOnlyDictionary> newRows)
		{

			if (newRows is IEnumerable<IDataRow> newDataRows)
				AddRangeDataRows(newDataRows);
			else
			{
				foreach (var r in newRows)
					Add(r);
			}
		} // proc AddRange

		PpsDataRow IPpsDataView.NewRow(object[] originalValues, object[] currentValues)
			=> NewRow(originalValues, currentValues);

		/// <summary>Remove a datarow from the table.</summary>
		/// <param name="row">Data row, that should be removed.</param>
		/// <returns><c>true</c>, if removed.</returns>
		public bool Remove(PpsDataRow row)
			=> row.Remove();

		/// <summary>Remove a datarow from the table.</summary>
		/// <param name="index">Index of the data, that should be removed.</param>
		public void RemoveAt(int index)
			=> currentRows[index].Remove();

		/// <summary>Removes all rows from the table.</summary>
		public void Clear()
		{
			for (var i = currentRows.Count - 1; i >= 0; i--)
				currentRows[i].Remove();
		} // proc Clear

		/// <summary>Reset the state to the original values.</summary>
		public void Reset()
		{
			// Setze alle Datenzeilen zurück
			foreach (var row in rows)
				row.Reset();
		} // proc Reset

		internal void CommitRow(PpsDataRow row)
		{
			if (row.IsAdded)
				originalRows.Add(row);
		} // proc CommitRow

		/// <summary>Copy the current values into the original values and remove all deleted rows.</summary>
		public void Commit()
		{
			// copy values of the current rows
			foreach (var row in currentRows)
				row.Commit();

			// remove all deleted rows
			for (var i = rows.Count - 1; i >= 0; i--)
			{
				if (rows[i].RowState == PpsDataRowState.Deleted)
				{
					originalRows.Remove(rows[i]);
					rows.RemoveAt(i);
				}
			}
		} // proc Commit

		#endregion

		#region -- Find ---------------------------------------------------------------

		private IEnumerable<PpsDataRow> GetRows(bool allRows)
			=> allRows ? (IEnumerable<PpsDataRow>)AllRows : this;

		/// <summary>Find a data row by primary key.</summary>
		/// <param name="keyValue">Primarykey value.</param>
		/// <param name="allRows"><c>true</c> to search also the original and deleted rows.</param>
		/// <returns>The row or <c>null</c>.</returns>
		public PpsDataRow FindKey(object keyValue, bool allRows = false)
		{
			var primaryKey = TableDefinition.PrimaryKey;
			if (primaryKey == null)
				throw new ArgumentException($"No primary key defined (at table {TableName}).");

			return FindRows(primaryKey, keyValue, allRows).FirstOrDefault();
		} // func FindKey

		/// <summary>Find data rows by column.</summary>
		/// <param name="column">Column to search in.</param>
		/// <param name="value">Value of the column.</param>
		/// <param name="allRows"><c>true</c> to search also the original and deleted rows.</param>
		/// <returns>Matching rows.</returns>
		public IEnumerable<PpsDataRow> FindRows(PpsDataColumnDefinition column, object value, bool allRows = false)
		{
			var columnIndex = column.Index; // index of the column
			value = GetConvertedValue(column, value); // change type of the value

			return from r in GetRows(allRows)
				   where Object.Equals(r[columnIndex], value)
				   select r;
		} // func FindRows

		#endregion

		#region -- Zugriff der Liste --------------------------------------------------

		/// <summary>Copy rows.</summary>
		/// <param name="array">Target array.</param>
		/// <param name="arrayIndex">Start within the target array.</param>
		public void CopyTo(PpsDataRow[] array, int arrayIndex)
			=> currentRows.CopyTo(array, arrayIndex);

		/// <summary>Find the index of the row in the current rows..</summary>
		/// <param name="row">Row to find.</param>
		/// <returns>Index or -1.</returns>
		public int IndexOf(PpsDataRow row)
			=> currentRows.IndexOf(row);

		/// <summary>Exists the row in the current rows.</summary>
		/// <param name="row">Row to check.</param>
		/// <returns><c>true</c>, if the row is in the current row.</returns>
		public bool Contains(PpsDataRow row)
			=> currentRows.Contains(row);

		/// <summary>Enumerate all currrent rows.</summary>
		/// <returns></returns>
		public IEnumerator<PpsDataRow> GetEnumerator()
			=> currentRows.GetEnumerator();

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
			=> ((System.Collections.IEnumerable)currentRows).GetEnumerator();

		int IList.Add(object value)
		{
			if (value is PpsDataRow row)
			{
				if (row.Table != this)
					throw new ArgumentException("Row does not belong to the current table.");

				return IndexOf(AddInternal(false, row));
			}
			else if (value is LuaTable table)
				return IndexOf(Add(table));
			else
				throw new NotSupportedException();
		} // func IListAdd
		
		// not supported
		void IList.Insert(int index, object value) 
			=> throw new NotSupportedException();

		// mapped
		bool IList.Contains(object value) 
			=> Contains((PpsDataRow)value);
		int IList.IndexOf(object value) 
			=> IndexOf((PpsDataRow)value);
		void IList.Remove(object value) 
			=> Remove((PpsDataRow)value);
		void ICollection.CopyTo(Array array, int index)
			=> ((IList)currentRows).CopyTo(array, index);

		bool IList.IsFixedSize => false; 
		bool IList.IsReadOnly => true; // es wurde IList.Add, IList.Insert nicht implementiert
		bool ICollection.IsSynchronized => false;
		object ICollection.SyncRoot => null;

		object IList.this[int index] { get => this[index]; set => throw new NotSupportedException(); }

		#endregion

		#region -- Read/Write ---------------------------------------------------------

		/// <summary>Reads data of an table.</summary>
		/// <param name="x"></param>
		/// <param name="combineData"></param>
		internal void Read(XElement x, bool combineData)
		{
			if (!combineData)
				ClearInternal();

			// read all rows
			foreach (var xRow in x.Elements(xnDataRow))
			{
				var row = (PpsDataRow)null;

				if (combineData)
				{
					// get current row key
					var xPrimaryKey = xRow.Element(TableDefinition.PrimaryKey.Name)
						?? throw new ArgumentException("Primary key is missing.");

					var key = xPrimaryKey.Element(xnDataRowValueCurrent)?.Value ?? xPrimaryKey.Element(xnDataRowValueOriginal)?.Value
						?? throw new ArgumentException("Primary key is null.");

					// find the row
					row = FindKey(Procs.ChangeType(key, TableDefinition.PrimaryKey.DataType), true);
				}

				// add the row
				if (row == null)
					AddInternal(xRow.GetAttribute(xnDataRowAdd, "0") != "1", new PpsDataRow(this, xRow));
				else
					row.UpdateRow(xRow);
			}
		} // proc Read

		/// <summary>Serialize data as xml.</summary>
		/// <param name="x">Destination.</param>
		public void Write(XmlWriter x)
		{
			foreach (var r in rows)
			{
				x.WriteStartElement(xnDataRow.LocalName);
				r.Write(x);
				x.WriteEndElement();
			}
		} // proc Write

		#endregion

		/// <summary>Owner of this table.</summary>
		public PpsDataSet DataSet => dataset;
		/// <summary>Definition of this table.</summary>
		public PpsDataTableDefinition TableDefinition => tableDefinition;
		/// <summary>Name of the table.</summary>
		public string TableName => tableDefinition.Name;
		/// <summary>Columns of the table</summary>
		public ReadOnlyCollection<PpsDataColumnDefinition> Columns => tableDefinition.Columns;
		IReadOnlyList<IDataColumn> IDataColumns.Columns => tableDefinition.Columns;

		/// <summary>Total number of current rows.</summary>
		public int Count => currentRows.Count;

		/// <summary>Access to the first row.</summary>
		public PpsDataRow First => currentRows.Count == 0 ? emptyRow : currentRows[0];
		/// <summary>Access to all rows in table, also the deleted.</summary>
		public ReadOnlyCollection<PpsDataRow> AllRows => rowsView;
		/// <summary>Access to all rows, that were loaded.</summary>
		public ReadOnlyCollection<PpsDataRow> OriginalRows => rowsOriginal;
		
		/// <summary>Access to the current row at the index.</summary>
		/// <param name="index"></param>
		/// <returns></returns>
		public PpsDataRow this[int index]
		{
			get => currentRows[index];
			set => throw new NotSupportedException();
		} // prop this

		/// <summary>Access to the row at the index.</summary>
		/// <param name="index">Index of the row.</param>
		/// <param name="version">Version of the row list.</param>
		/// <returns></returns>
		public PpsDataRow this[int index, PpsRowVersion version]
		{
			get
			{
				switch (version)
				{
					case PpsRowVersion.All:
						return rows[index];
					case PpsRowVersion.Original:
						return originalRows[index];
					default:
						return currentRows[index];
				}
			}
		} // prop this

		PpsDataTable IPpsDataView.Table => this;

		// -- Static --------------------------------------------------------------

		private static readonly PropertyInfo readOnlyCollectionIndexPropertyInfo;
		private static readonly PropertyInfo columnsPropertyInfo;
		internal static readonly PropertyInfo tableDefinitionPropertyInfo;

		private const ulong localKeyBit = (ulong)2 << 62;
		private const ulong serverKeyBit = (ulong)3 << 62;
		
		static PpsDataTable()
		{
			var typeInfo = typeof(PpsDataTable).GetTypeInfo();
			columnsPropertyInfo = typeInfo.GetDeclaredProperty("Columns");
			tableDefinitionPropertyInfo = typeInfo.GetDeclaredProperty("TableDefinition");

			readOnlyCollectionIndexPropertyInfo = typeof(ReadOnlyCollection<PpsDataColumnDefinition>).GetTypeInfo().GetDeclaredProperty("Item");

			if (columnsPropertyInfo == null || tableDefinitionPropertyInfo == null || readOnlyCollectionIndexPropertyInfo == null)
				throw new InvalidOperationException("Reflection fehlgeschlagen (PpsDataTable)");
		} // sctor

		#region -- GetKey, MakeKey ----------------------------------------------------

		/// <summary>Split a key value into his components.</summary>
		/// <param name="key">Encoded key.</param>
		/// <param name="type">Key type.</param>
		/// <param name="value">Key value.</param>
		public static void GetKey(long key, out PpsTablePrimaryKeyType type, out long value)
		{
			if (key >= 0)
			{
				type = PpsTablePrimaryKeyType.Database;
				value = key;
			}
			else
			{
				var k = unchecked((ulong)key);
				if (k >> 62 == 2)
				{
					type = PpsTablePrimaryKeyType.Local;
					value = (long)(~serverKeyBit & (ulong)key); // serverKeyBit is correct, because we use only the bitmask
				}
				else if (k >> 62 == 3)
				{
					type = PpsTablePrimaryKeyType.Server;
					value = (long)(~serverKeyBit & (ulong)key);
				}
				else
					throw new ArgumentException("Could not detect key type.", nameof(value));
			}
		} // func GetKey

		/// <summary>Encode a key field.</summary>
		/// <param name="type">Key type.</param>
		/// <param name="value">Key value.</param>
		/// <returns>Encoded key.</returns>
		public static long MakeKey(PpsTablePrimaryKeyType type, long value)
		{
			if ((serverKeyBit & (ulong)value) != 0)
				throw new ArgumentOutOfRangeException(nameof(value));

			switch(type)
			{
				case PpsTablePrimaryKeyType.Database:
					return value;
				case PpsTablePrimaryKeyType.Local:
					return unchecked((long)(localKeyBit | (ulong)value));
				case PpsTablePrimaryKeyType.Server:
					return unchecked((long)(serverKeyBit | (ulong)value));
				default:
					throw new ArgumentOutOfRangeException(nameof(type));
			}
		} // func MakeKey

		#endregion
	} // class PpsDataTable

	#endregion

	#region -- class PpsDataFilter ----------------------------------------------------

	/// <summary>Base class for a bindable filter of rows in a table.</summary>
	public abstract class PpsDataFilter : IPpsDataView, IDisposable
	{
		/// <summary>Notifies about changes in this collection.</summary>
		public event NotifyCollectionChangedEventHandler CollectionChanged;

		private List<PpsDataRow> rows;
		private NotifyCollectionChangedEventHandler evCollectionListener;
		private ColumnValueChangedEventHandler evColumnListener;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary>Datafilter for a data table-</summary>
		/// <param name="table">DataTable to filter</param>
		protected PpsDataFilter(PpsDataTable table)
		{
			this.Table = table ?? throw new ArgumentNullException();
			this.rows = new List<PpsDataRow>();

			evCollectionListener = TableNotifyCollectionChanged;
			evColumnListener = TableColumnValueChanged;
			table.CollectionChanged += evCollectionListener;
			table.ColumnValueChanged += evColumnListener;
		} // ctor

		/// <summary>Unconnect the filter.</summary>
		public void Dispose()
			=> Dispose(true);

		/// <summary></summary>
		/// <param name="disposing"></param>
		protected virtual void Dispose(bool disposing)
		{
			if (disposing)
			{
				IsDisposed = true;
				Table.CollectionChanged -= evCollectionListener;
				Table.ColumnValueChanged -= evColumnListener;
			}
		} // proc Dispose

		#endregion

		#region -- Refresh ------------------------------------------------------------

		/// <summary>Rebuilds the current row-index</summary>
		public void Refresh()
		{
			lock (rows)
			{
				rows.Clear();
				rows.AddRange(from row in Table where FilterRow(row) select row);
			}
			OnCollectionReset();
		} // proc Refresh

		private void TableNotifyCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			switch (e.Action)
			{
				case NotifyCollectionChangedAction.Reset:
					Refresh();
					break;
				case NotifyCollectionChangedAction.Add:
					{
						var row = (PpsDataRow)e.NewItems[0];
						if (FilterRow(row))
						{
							lock (rows)
							{
								if (!rows.Contains(row))
								{
									rows.Add(row);
									OnCollectionAdd(row);
								}
							}
						}
					}
					break;
				case NotifyCollectionChangedAction.Remove:
					{
						var row = (PpsDataRow)e.OldItems[0];
						lock (rows)
						{
							var oldIndex = rows.IndexOf(row);
							if (oldIndex != -1)
							{
								rows.RemoveAt(oldIndex);
								OnCollectionRemove(row, oldIndex);
							}
						}
					}
					break;
			}
		} // proc TableNotifyCollectionChanged

		private void TableColumnValueChanged(object sender, ColumnValueChangedEventArgs e)
		{
			lock (rows)
			{
				if (e.Row.RowState != PpsDataRowState.Deleted && FilterRow(e.Row))
				{
					if (!rows.Contains(e.Row)) // add row if not in list
					{
						rows.Add(e.Row);
						OnCollectionAdd(e.Row);
					}
				}
				else
				{
					var oldIndex = rows.IndexOf(e.Row);
					if (oldIndex != -1)
					{
						rows.RemoveAt(oldIndex);
						OnCollectionRemove(e.Row, oldIndex);
					}
				}
			}
		} // proc TableColumnValueChanged

		private void OnCollectionReset()
			=> CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

		private void OnCollectionAdd(PpsDataRow row)
			=> CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, row, rows.IndexOf(row)));

		private void OnCollectionRemove(PpsDataRow row, int oldIndex)
			=> CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, row, oldIndex));
		
		/// <summary>Belongs the given row to the filter.</summary>
		/// <param name="row">Row to check</param>
		/// <returns><c>true</c>, it belongs to the filter.</returns>
		protected abstract bool FilterRow(PpsDataRow row);

		#endregion

		#region -- IList members ------------------------------------------------------

		/// <summary>Add a row from a table to the view.</summary>
		/// <param name="values">Members that will be assigned to the datarow columns.</param>
		/// <returns>Added data row.</returns>
		public PpsDataRow Add(LuaTable values)
			=> Add(Table.GetDataRowValues(values));

		/// <summary>Add a row from a value array to the view.</summary>
		/// <param name="values">Value array, that will be assigned by index.</param>
		/// <returns>Added data row.</returns>
		public PpsDataRow Add(params object[] values)
			=> Table.Add(InitializeValues(values));

		PpsDataRow IPpsDataView.NewRow(object[] originalValues, object[] currentValues)
			=> ((IPpsDataView)Table).NewRow(InitializeValues(originalValues), currentValues);

		/// <summary>Create a datarow-value-array from a value array.</summary>
		/// <param name="values">Array of values or <c>null</c>.</param>
		/// <returns>Value array.</returns>
		protected virtual object[] InitializeValues(object[] values)
			=> Table.CreateDataRowValuesArray(values);

		/// <summary>Remove a datarow from the view.</summary>
		/// <param name="row">Data row, that should be removed.</param>
		/// <returns><c>true</c>, if remnoved.</returns>
		public bool Remove(PpsDataRow row)
			=> Table.Remove(row);

		int IList.Add(object value)
		{
			if (value is PpsDataRow row)
				return rows.IndexOf(Table.AddInternal(false, row));
			else if (value is LuaTable t)
			{
				lock (rows)
					return rows.IndexOf(Add(t));
			}
			else
				throw new NotSupportedException();
		} // prop IList.Add


		bool IList.Contains(object value)
		{
			lock (rows)
				return rows.Contains((PpsDataRow)value);
		} // func IList.Contains

		int IList.IndexOf(object value)
		{
			lock (rows)
				return rows.IndexOf((PpsDataRow)value);
		} // func IList.IndexOf

		void IList.Remove(object value)
			=> Table.Remove((PpsDataRow)value);

		void IList.RemoveAt(int index)
		{
			PpsDataRow row;
			lock (rows)
				row = rows[index];
			Table.Remove(row);
		} // proc IList.RemoveAt

		void ICollection.CopyTo(Array array, int index)
		{
			lock (rows)
				((IList)rows).CopyTo(array, index);
		} // proc ICollection.CopyTo

		// not supported
		void IList.Insert(int index, object value) { throw new NotSupportedException(); }
		void IList.Clear() { throw new NotSupportedException(); }

		// mapped
		IEnumerator IEnumerable.GetEnumerator() => rows.GetEnumerator();
		IEnumerator<PpsDataRow> IEnumerable<PpsDataRow>.GetEnumerator() => rows.GetEnumerator();

		bool IList.IsFixedSize => false;
		bool IList.IsReadOnly => false;
		bool ICollection.IsSynchronized => true;
		object ICollection.SyncRoot => rows;

		/// <summary>Number of rows in this view.</summary>
		public int Count
		{
			get
			{
				lock (rows)
					return rows.Count;
			}
		} // prop Count

		/// <summary>Access the rows by index.</summary>
		/// <param name="index">Index of the row.</param>
		/// <returns></returns>
		public object this[int index]
		{
			get
			{
				lock (rows)
					return rows[index];
			}
			set => throw new NotSupportedException();
		} // func this

		#endregion

		/// <summary>Access to the child table.</summary>
		public PpsDataTable Table { get; }

		/// <summary>Columns</summary>
		public IReadOnlyList<IDataColumn> Columns => Table.Columns;
		/// <summary>Is the filter disposed.</summary>
		public bool IsDisposed { get; private set; } = false;
	} // class PpsDataFilter

	#endregion
}
