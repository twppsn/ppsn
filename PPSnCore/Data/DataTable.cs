using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Stuff;
using static TecWare.PPSn.Data.PpsDataHelper;

namespace TecWare.PPSn.Data
{
	#region -- enum PpsRowVersion -------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Welche Zeilenversionen sollen zurückgegeben werden.</summary>
	public enum PpsRowVersion
	{
		/// <summary>Die Original hinzugefügten Zeilen.</summary>
		Original,
		/// <summary>Alle aktiven, nicht gelöschten Zeilen.</summary>
		Current,
		/// <summary>Behält halle Zeilen, auch die gelöschten</summary>
		All
	} // enum PpsRowVersion

	#endregion

	#region -- enum PpsDataTableMetaData ------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Vordefinierte Meta-Daten an der Tabelle.</summary>
	public enum PpsDataTableMetaData
	{
	} // enum PpsDataTableMetaData

	#endregion

	#region -- class PpsDataTableRelationDefinition -------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsDataTableRelationDefinition
	{
		private readonly string name;
		private readonly PpsDataColumnDefinition parentColumn;
		private readonly PpsDataColumnDefinition childColumn;

		internal PpsDataTableRelationDefinition(string name, PpsDataColumnDefinition parentColumn, PpsDataColumnDefinition childColumn)
		{
			this.name = name;
			this.parentColumn = parentColumn;
			this.childColumn = childColumn;
		} // ctor

		public override string ToString() => $"{parentColumn} -> {name}";

		public string Name => name;
		public PpsDataColumnDefinition ParentColumn => parentColumn;
		public PpsDataColumnDefinition ChildColumn => childColumn;
	} // class PpsDataTableRelationDefinition

	#endregion

	#region -- class PpsDataTableDefinition ---------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public abstract class PpsDataTableDefinition
	{
		#region -- WellKnownTypes ---------------------------------------------------------

		/// <summary>Definiert die bekannten Meta Informationen.</summary>
		private static readonly Dictionary<string, Type> wellknownMetaTypes = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

		#endregion

		#region -- class PpsDataTableMetaCollection ---------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		public class PpsDataTableMetaCollection : PpsMetaCollection
		{
			public T Get<T>(PpsDataTableMetaData key, T @default)
			{
				return Get<T>(key.ToString(), @default);
			} // func Get

			public override IReadOnlyDictionary<string, Type> WellknownMetaTypes { get { return wellknownMetaTypes; } }
		} // class PpsDataTableMetaCollection

		#endregion

		private readonly string name;
		private readonly PpsDataSetDefinition dataset;

		private List<PpsDataColumnDefinition> columns;
		private ReadOnlyCollection<PpsDataColumnDefinition> columnCollection;
		private List<PpsDataTableRelationDefinition> relations;
		private ReadOnlyCollection<PpsDataTableRelationDefinition> relationCollection;

		protected PpsDataTableDefinition(PpsDataSetDefinition dataset, string tableName)
		{
			this.name = tableName;
			this.dataset = dataset;
			this.columns = new List<PpsDataColumnDefinition>();
			this.columnCollection = new ReadOnlyCollection<PpsDataColumnDefinition>(columns);
			this.relations = new List<PpsDataTableRelationDefinition>();
			this.relationCollection = new ReadOnlyCollection<PpsDataTableRelationDefinition>(relations);
		} // ctor

		/// <summary>Ends the initialization.</summary>
		protected internal virtual void EndInit()
		{
		} // proc EndInit

		public virtual PpsDataTable CreateDataTable(PpsDataSet dataset)
		{
			if (!IsInitialized)
				throw new ArgumentException($"{nameof(EndInit)} from table {name} is not called.");

			return new PpsDataTable(this, dataset);
		} // func CreateDataTable

		/// <summary>Add's a new column to the table.</summary>
		/// <param name="column"></param>
		protected void AddColumn(PpsDataColumnDefinition column)
		{
			if (IsInitialized)
				throw new InvalidOperationException($"Can not add column '{column.Name}', because table '{name}' is initialized.");
			if (column == null)
				throw new ArgumentNullException();
			if (column.Table != this)
				throw new ArgumentException();

			int iIndex = FindColumnIndex(column.Name);
			if (iIndex >= 0)
				columns[iIndex] = column;
			else
				columns.Add(column);
		} // proc AddColumn

		/// <summary>Creates a new relation between two columns.</summary>
		/// <param name="relationName">Name of the relation</param>
		/// <param name="parentColumn">Parent column, that must belong to the current table definition.</param>
		/// <param name="childColumn">Child column.</param>
		public void AddRelation(string relationName, PpsDataColumnDefinition parentColumn, PpsDataColumnDefinition childColumn)
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

			relations.Add(new PpsDataTableRelationDefinition(relationName, parentColumn, childColumn));
		} // proc AddRelation

		public PpsDataColumnDefinition FindColumn(string columnName)
		{
			return columns.Find(c => String.Compare(c.Name, columnName, StringComparison.OrdinalIgnoreCase) == 0);
		} // func FindColumn

		public int FindColumnIndex(string columnName)
		{
			return columns.FindIndex(c => String.Compare(c.Name, columnName, StringComparison.OrdinalIgnoreCase) == 0);
		} // func FindColumnIndex

		public int FindColumnIndex(string columnName, bool lThrowException)
		{
			int iIndex = FindColumnIndex(columnName);
			if (iIndex == -1 && lThrowException)
				throw new ArgumentException(String.Format("Spalte '{0}.{1}' nicht gefunden.", Name, columnName));
			return iIndex;
		} // func FindColumnIndex

		public PpsDataTableRelationDefinition FindRelation(string relationName)
		{
			return relations.Find(c => String.Compare(c.Name, relationName, StringComparison.OrdinalIgnoreCase) == 0);
		} // func FindRelation

		/// <summary>Owner of the table.</summary>
		public PpsDataSetDefinition DataSet => dataset;
		/// <summary>Name of the table.</summary>
		public string Name => name;
		/// <summary>Is the table initialized.</summary>
		public bool IsInitialized => dataset.IsInitialized;
		/// <summary>Column definition</summary>
		public ReadOnlyCollection<PpsDataColumnDefinition> Columns => columnCollection; 
		/// <summary>Attached relations</summary>
		public ReadOnlyCollection<PpsDataTableRelationDefinition> Relations => relationCollection;

		/// <summary>Access to the table meta-data.</summary>
		public abstract PpsDataTableMetaCollection Meta { get; }
	} // class PpsDataTableDefinition

	#endregion

	#region -- event ColumnValueChangedEventHandler -------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Event arguments for a changed value.</summary>
	public class ColumnValueChangedEventArgs : EventArgs
	{
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

	#region -- class PpsDataTable -------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Table</summary>
	public class PpsDataTable : IList, IEnumerable<PpsDataRow>, INotifyCollectionChanged, IDynamicMetaObjectProvider
	{
		#region -- class PpsDataTableAddChangeItem ----------------------------------------

		private class PpsDataTableAddChangeItem : IPpsUndoItem
		{
			private PpsDataTable table;
			private PpsDataRow rowAdded;

			public PpsDataTableAddChangeItem(PpsDataTable table, PpsDataRow rowAdded)
			{
				this.table = table;
				this.rowAdded = rowAdded;
			} // ctor

			public void Redo()
			{
				table.AddInternal(false, rowAdded);
			} // proc Redo

			public void Undo()
			{
				table.RemoveInternal(rowAdded, false);
			} // proc Undo
		} // class PpsDataTableAddChangeItem

		#endregion

		#region -- class PpsDataTableRemoveChangeItem -------------------------------------

		private class PpsDataTableRemoveChangeItem : IPpsUndoItem
		{
			private PpsDataTable table;
			private PpsDataRow rowDeleted;

			public PpsDataTableRemoveChangeItem(PpsDataTable table, PpsDataRow rowDeleted)
			{
				this.table = table;
				this.rowDeleted = rowDeleted;
			} // ctor

			public void Redo()
			{
				table.RemoveInternal(rowDeleted, false);
			} // proc Redo

			public void Undo()
			{
				table.AddInternal(false, rowDeleted);
			} // proc Undo
		} // class PpsDataTableAddChangeItem

		#endregion

		#region -- class PpsDataTableMetaObject ------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
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
							Expression.Property(Expression.Convert(Expression, typeof(PpsDataTable)), TableDefinitionPropertyInfo),
							Expression.Constant(table.TableDefinition)
						)
					)
				);
			} // func GetBindingRestrictions

			public override DynamicMetaObject BindGetMember(GetMemberBinder binder)
			{
				if (PpsDataHelper.IsStandardMember(LimitType, binder.Name))
				{
					return base.BindGetMember(binder);
				}
				else
				{
					var table = (PpsDataTable)Value;
					var columnIndex = table.TableDefinition.FindColumnIndex(binder.Name);
					if (columnIndex == -1)
					{
						if (table.TableDefinition.Meta == null)
							return new DynamicMetaObject(Expression.Constant(null, typeof(object)), GetBindingRestrictions(table));
						else
							return new DynamicMetaObject(table.TableDefinition.Meta.GetMetaConstantExpression(binder.Name), GetBindingRestrictions(table));
					}
					else
					{
						return new DynamicMetaObject(
							Expression.MakeIndex(
								Expression.Property(
									Expression.Convert(Expression, typeof(PpsDataTable)),
									ColumnsPropertyInfo
								),
								ReadOnlyCollectionIndexPropertyInfo,
								new Expression[] { Expression.Constant(columnIndex) }
							),
							GetBindingRestrictions(table)
						);
					}
				}
			} // func BindGetMember
		} // class PpsDataTableMetaObject

		#endregion

		/// <summary>Notifies changes of the list.</summary>
		public event NotifyCollectionChangedEventHandler CollectionChanged;
		/// <summary>Notifies changes of single values.</summary>
		public event ColumnValueChangedEventHandler ColumnValueChanged;

		private PpsDataTableDefinition tableDefinition;		// definition of this table
		private PpsDataSet dataset;												// owner of this table

		private PpsDataRow emptyRow;
		private List<PpsDataRow> rows = new List<PpsDataRow>();         // all rows
		private List<PpsDataRow> originalRows = new List<PpsDataRow>(); // all initial loaded rows
		private List<PpsDataRow> currentRows = new List<PpsDataRow>();  // all current active rows

		private ReadOnlyCollection<PpsDataRow> rowsView;
		private ReadOnlyCollection<PpsDataRow> rowsOriginal;

		#region -- Ctor/Dtor --------------------------------------------------------------

		/// <summary>Creates a new table</summary>
		/// <param name="tableDefinition"></param>
		/// <param name="dataset"></param>
		protected internal PpsDataTable(PpsDataTableDefinition tableDefinition, PpsDataSet dataset)
		{
			if (dataset == null)
				throw new ArgumentNullException();

			this.dataset = dataset;
			this.tableDefinition = tableDefinition;

			this.emptyRow = new PpsDataRow(this, PpsDataRowState.Unchanged, new object[tableDefinition.Columns.Count], null);
			this.rowsView = new ReadOnlyCollection<PpsDataRow>(rows);
			this.rowsOriginal = new ReadOnlyCollection<PpsDataRow>(originalRows);
		} // ctor

		DynamicMetaObject IDynamicMetaObjectProvider.GetMetaObject(Expression parameter)
		{
			return new PpsDataTableMetaObject(parameter, this);
		} // func GetMetaObject

		#endregion

		#region -- Collection Changed -----------------------------------------------------

		/// <summary>Notifies if a rows is added.</summary>
		/// <param name="row">The new row</param>
		protected virtual void OnRowAdded(PpsDataRow row)
		{
			OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, row));
		} // proc OnRowAdded

		/// <summary>Notifies if a row is removed.</summary>
		/// <param name="row"></param>
		protected virtual void OnRowRemoved(PpsDataRow row)
		{
			OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, row));
		} // proc OnRowRemoved

		/// <summary>Notifies if value is changed.</summary>
		/// <param name="row"></param>
		/// <param name="columnIndex"></param>
		/// <param name="oldValue"></param>
		/// <param name="value"></param>
		protected internal virtual void OnColumnValueChanged(PpsDataRow row, int columnIndex, object oldValue, object value)
		{
			dataset.OnTableColumnValueChanged(row, columnIndex, oldValue, value);
			ColumnValueChanged?.Invoke(this, new ColumnValueChangedEventArgs(row, columnIndex, oldValue, value));
		} // proc OnColumnValueChanged

		/// <summary>Notifies if the collection of current rows is changed.</summary>
		/// <param name="e"></param>
		protected virtual void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
		{
			CollectionChanged?.Invoke(this, e);
		} // proc OnCollectionChanged

		#endregion

		#region -- Add, Remove, Reset, Commit ---------------------------------------------

		private IPpsUndoSink GetUndoSink()
		{
			var sink = dataset.UndoSink;
			return sink != null && !sink.InUndoRedoOperation ? sink : null;
		} // func GetUndoSink

		internal PpsDataRow AddInternal(bool lOriginal, PpsDataRow row)
		{
			// add the line
			row.Table = this;
			rows.Add(row);

			if (lOriginal)
				originalRows.Add(row);
			if (row.RowState != PpsDataRowState.Deleted)
			{
				currentRows.Add(row);
				GetUndoSink()?.Append(new PpsDataTableAddChangeItem(this, row));

				OnRowAdded(row);
			}

			return row;
		} // proc Add

		/// <summary>Stellt einen gelöschten Eintrag wieder her</summary>
		/// <param name="row"></param>
		internal bool RestoreInternal(PpsDataRow row)
		{
			int iOriginalIndex = originalRows.IndexOf(row);
			int iCurrentIndex = currentRows.IndexOf(row);
			if (iCurrentIndex == -1)
			{
				if (iOriginalIndex == -1 || row.Table == null)
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
					var childTable = dataset.FindTableFromDefinition(r.ChildColumn.Table);
					var childColumnIndex = r.ChildColumn.Index;

          for (var i = childTable.Count - 1; i >= 0; i--)
					{
						if (Object.Equals(childTable[i][childColumnIndex], parentValue))
							childTable.RemoveAt(i);
					}
				}
			}
		} // proc RemoveRelatedRows		

		/// <summary></summary>
		/// <param name="row"></param>
		/// <param name="removeOriginal"><c>true</c>, für ein tatsächliches Entfernen.</param>
		/// <returns>Wurde der Eintrag gelöscht</returns>
		internal bool RemoveInternal(PpsDataRow row, bool removeOriginal)
		{
			bool r = false;

			if (row.Table != this)
				throw new InvalidOperationException();

			// remove the entry from the current list
			if (currentRows.Remove(row))
			{
				RemoveRelatedRows(row); // check related rows

				GetUndoSink()?.Append(new PpsDataTableRemoveChangeItem(this, row));
				OnRowRemoved(row);
				r = true;
			}

			// remove the row also from the original rows
			var originalIndex = originalRows.IndexOf(row);
			if (originalIndex == -1) // it is a new added line
			{
				rows.Remove(row);
				row.Table = null;
				return r;
			}
			else // it is original loaded
			{
				if (removeOriginal)
				{
					row.Table = null;
					rows.Remove(row);
					return originalRows.Remove(row);
				}
				else
					return r;
			}
		} // proc RemoveInternal

		internal void ClearInternal()
		{
			foreach (var r in rows)
				r.Table = null;

			rows.Clear();
			currentRows.Clear();
			originalRows.Clear();
		} // proc ClearInternal

		public object[] GetDataRowValues(LuaTable table)
		{
			var values = new object[Columns.Count];
			for (int i = 0; i < Columns.Count; i++)
				values[i] = table.GetMemberValue(Columns[i].Name);
			return values;
		} // func GetDataRowValues

		public PpsDataRow Add(LuaTable values)
		{
			return Add(GetDataRowValues(values));
		} // func Add

		/// <summary>Erzeugt eine neue Zeile.</summary>
		/// <param name="values">Werte, die in der Zeile enthalten sein sollen.</param>
		/// <returns>Neue Datenzeile</returns>
		public PpsDataRow Add(params object[] values)
		{
			if (values != null && values.Length == 0) // no values 
			{
				values = new object[Columns.Count];
			}
			else if (values.Length != Columns.Count)
			{
				var n = new object[Columns.Count];
				Array.Copy(values, 0, n, 0, Math.Min(values.Length, n.Length));
				values = n;
			}
			
			return AddInternal(false, new PpsDataRow(this, PpsDataRowState.Modified, new object[Columns.Count], values));
		} // proc Add

		/// <summary>Entfernt die Datenzeile</summary>
		/// <param name="row">Datenzeile, die als Entfernt markiert werden soll.</param>
		public bool Remove(PpsDataRow row)
		{
			return row.Remove();
		} // proc Remove

		/// <summary>Entfernt die Datenzeile</summary>
		/// <param name="index"></param>
		public void RemoveAt(int index)
		{
			currentRows[index].Remove();
		} // proc RemoveAt

		/// <summary>Markiert alle Einträge als gelöscht.</summary>
		public void Clear()
		{
			for (int i = currentRows.Count - 1; i >= 0; i--)
				currentRows[i].Remove();
		} // proc Clear

		/// <summary>Setzt die komplette Datentabelle zurück.</summary>
		public void Reset()
		{
			// Setze alle Datenzeilen zurück
			foreach (PpsDataRow row in rows)
				row.Reset();
		} // proc Reset

		/// <summary>Die aktuelle Werte werden in die Default-Wert kopiert.</summary>
		public void Commit()
		{
			// Alle Dateizeilen bearbeiten
			foreach (PpsDataRow row in currentRows)
				row.Commit();
		} // proc Commit

		#endregion

		#region -- Zugriff der Liste ------------------------------------------------------

		/// <summary></summary>
		/// <param name="array"></param>
		/// <param name="arrayIndex"></param>
		public void CopyTo(PpsDataRow[] array, int arrayIndex)
		{
			currentRows.CopyTo(array, arrayIndex);
		} // proc CopyTo

		/// <summary>Gibt den Index der Zeile zurück,</summary>
		/// <param name="row"></param>
		/// <returns></returns>
		public int IndexOf(PpsDataRow row)
		{
			return currentRows.IndexOf(row);
		} // func IndexOf

		/// <summary></summary>
		/// <param name="item"></param>
		/// <returns></returns>
		public bool Contains(PpsDataRow item)
		{
			return currentRows.Contains(item);
		} // func Contains

		public IEnumerator<PpsDataRow> GetEnumerator()
		{
			return currentRows.GetEnumerator();
		} // func GetEnumerator

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return ((System.Collections.IEnumerable)currentRows).GetEnumerator();
		} // func System.Collections.IEnumerable.GetEnumerator

		// not supported
		int IList.Add(object value) { throw new NotSupportedException(); }
		void IList.Insert(int index, object value) { throw new NotSupportedException(); }

		// mapped
		bool IList.Contains(object value) { return Contains((PpsDataRow)value); }
		int IList.IndexOf(object value) { return IndexOf((PpsDataRow)value); }
		void IList.Remove(object value) { Remove((PpsDataRow)value); }
		void ICollection.CopyTo(Array array, int index) { ((IList)currentRows).CopyTo(array, index); }

		bool IList.IsFixedSize { get { return false; } }
		bool IList.IsReadOnly { get { return true; } } // es wurde IList.Add, IList.Insert nicht implementiert
		bool ICollection.IsSynchronized { get { return false; } }
		object ICollection.SyncRoot { get { return null; } }

		object IList.this[int index] { get { return this[index]; } set { throw new NotSupportedException(); } }

		#endregion

		#region -- Read/Write -------------------------------------------------------------

		/// <summary>Fügt die Daten in die Tabelle ein.</summary>
		/// <param name="x"></param>
		public void Read(XElement x)
		{
			Debug.Assert(x.Name.LocalName == xnTable); // muss Tabellenelement sein

			foreach (XElement xRow in x.Elements(xnDataRow)) // Zeilen lesen
				AddInternal(xRow.GetAttribute(xnDataRowAdd, "0") != "1", new PpsDataRow(this, xRow));
		} // proc Read

		public void Write(XmlWriter x)
		{
			x.WriteStartElement(xnTable.LocalName);

			// Schreibe die Datenzeilen
			foreach (PpsDataRow r in rows)
			{
				x.WriteStartElement(xnDataRow.LocalName);
				r.Write(x);
				x.WriteEndElement();
			}

			x.WriteEndElement();
		} // proc Write

		#endregion

		/// <summary>Owner of this table.</summary>
		public PpsDataSet DataSet => dataset;
		/// <summary>Definition of this table.</summary>
		public PpsDataTableDefinition TableDefinition => tableDefinition;
		/// <summary>Name of the table.</summary>
		public string Name => tableDefinition.Name;
		/// <summary>Columns of the table</summary>
		public ReadOnlyCollection<PpsDataColumnDefinition> Columns => tableDefinition.Columns;

		/// <summary>Total number of current rows.</summary>
		public int Count => currentRows.Count;

		/// <summary>Access to the first row.</summary>
		public PpsDataRow First => currentRows.Count == 0 ? emptyRow : currentRows[0];
		/// <summary>Access to all rows in table, also the deleted.</summary>
		public ReadOnlyCollection<PpsDataRow> AllRows =>rowsView;
		/// <summary>Access to all rows, that were loaded.</summary>
		public ReadOnlyCollection<PpsDataRow> OriginalRows => rowsOriginal;

		/// <summary>Access to the current row at the index.</summary>
		/// <param name="index"></param>
		/// <returns></returns>
		public PpsDataRow this[int index]
		{
			get { return currentRows[index]; }
			set { throw new NotSupportedException(); }
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

		// -- Static --------------------------------------------------------------

		private static readonly PropertyInfo ReadOnlyCollectionIndexPropertyInfo;
		private static readonly PropertyInfo ColumnsPropertyInfo;
		internal static readonly PropertyInfo TableDefinitionPropertyInfo;

		static PpsDataTable()
		{
			var typeInfo = typeof(PpsDataTable).GetTypeInfo();
			ColumnsPropertyInfo = typeInfo.GetDeclaredProperty("Columns");
			TableDefinitionPropertyInfo = typeInfo.GetDeclaredProperty("TableDefinition");

			ReadOnlyCollectionIndexPropertyInfo = typeof(ReadOnlyCollection<PpsDataColumnDefinition>).GetTypeInfo().GetDeclaredProperty("Item");

			if (ColumnsPropertyInfo == null || TableDefinitionPropertyInfo == null || ReadOnlyCollectionIndexPropertyInfo == null)
				throw new InvalidOperationException("Reflection fehlgeschlagen (PpsDataTable)");
		} // sctor
	} // class PpsDataTable

	#endregion

	#region -- class PpsDataFilter ------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Base class for a bindable filter of rows in a table.</summary>
	public abstract class PpsDataFilter : IList, IEnumerable<PpsDataRow>, INotifyCollectionChanged, IDisposable
	{
		/// <summary>Notifies about changes in this collection.</summary>
		public event NotifyCollectionChangedEventHandler CollectionChanged;

		private PpsDataTable table;
		private List<PpsDataRow> rows;
		private bool isDisposed = false;

		private NotifyCollectionChangedEventHandler evCollectionListener;
		private ColumnValueChangedEventHandler evColumnListener;

		#region -- Ctor/Dtor --------------------------------------------------------------

		protected PpsDataFilter(PpsDataTable table)
		{
			if (table == null)
				throw new ArgumentNullException();

			this.table = table;
			this.rows = new List<PpsDataRow>();

			evCollectionListener = TableNotifyCollectionChanged;
			evColumnListener = TableColumnValueChanged;
			table.CollectionChanged += evCollectionListener;
			table.ColumnValueChanged += evColumnListener;
		} // ctor

		/// <summary>Unconnect the filter.</summary>
		public void Dispose()
		{
			Dispose(true);
		} // proc Dispose

		/// <summary></summary>
		/// <param name="disposing"></param>
		protected virtual void Dispose(bool disposing)
		{
			if (disposing)
			{
				isDisposed = true;
				table.CollectionChanged -= evCollectionListener;
				table.ColumnValueChanged -= evColumnListener;
			}
		} // proc Dispose

		#endregion

		#region -- Refresh ----------------------------------------------------------------

		/// <summary>Rebuilds the current row-index</summary>
		public void Refresh()
		{
			lock (rows)
			{
				rows.Clear();
				rows.AddRange(from row in table where FilterRow(row) select row);
			}
			OnCollectionReset();
		} // proc Refresh

		private void TableNotifyCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			switch (e.Action)
			{
				case NotifyCollectionChangedAction.Reset:
					Refresh();
					OnCollectionReset();
					break;
				case NotifyCollectionChangedAction.Add:
					{
						var row = (PpsDataRow)e.NewItems[0];
						if (FilterRow(row))
						{
							lock (rows)
								rows.Add(row);

							OnCollectionAdd(row);
						}
					}
					break;
				case NotifyCollectionChangedAction.Remove:
					{
						var row = (PpsDataRow)e.OldItems[0];
						lock (rows)
						{
							if (rows.Remove(row))
								OnCollectionRemove(row);
						}
					}
					break;
			}
		} // proc TableNotifyCollectionChanged

		private void TableColumnValueChanged(object sender, ColumnValueChangedEventArgs e)
		{
			lock (rows)
			{
				if (FilterRow(e.Row))
				{
					if (!rows.Contains(e.Row)) // add row if not in list
					{
						rows.Add(e.Row);
						OnCollectionAdd(e.Row);
					}
				}
				else
				{
					if (rows.Remove(e.Row))
						OnCollectionRemove(e.Row);
				}
			}
		} // proc TableColumnValueChanged

		private void OnCollectionReset()
		{
			CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
		} // proc OnCollectionReset

		private void OnCollectionAdd(PpsDataRow row)
		{
			CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, row));
		} // proc OnCollectionAdd

		private void OnCollectionRemove(PpsDataRow row)
		{
			CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, row));
		} // proc OnCollectionRemove

		/// <summary>Belongs the given row to the filter.</summary>
		/// <param name="row">Row to check</param>
		/// <returns><c>true</c>, it belongs to the filter.</returns>
		protected abstract bool FilterRow(PpsDataRow row);

		#endregion

		#region -- IList members ----------------------------------------------------------

		public PpsDataRow Add(LuaTable values)
		{
			return Add(table.GetDataRowValues(values));
		} // func Add

		public virtual PpsDataRow Add(params object[] values)
		{
			return table.Add(values);
		} // func Add

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

		void IList.Remove(object value) => table.Remove((PpsDataRow)value);

		void IList.RemoveAt(int index)
		{
			PpsDataRow row;
			lock (rows)
				row = rows[index];
			table.Remove(row);
		} // proc IList.RemoveAt

		void ICollection.CopyTo(Array array, int index)
		{
			lock (rows)
			((IList)rows).CopyTo(array, index);
		} // proc ICollection.CopyTo

		// not supported
		int IList.Add(object value) { throw new NotSupportedException(); }
		void IList.Insert(int index, object value) { throw new NotSupportedException(); }
		void IList.Clear() { throw new NotSupportedException(); }

		// mapped
		IEnumerator IEnumerable.GetEnumerator() => rows.GetEnumerator();
		IEnumerator<PpsDataRow> IEnumerable<PpsDataRow>.GetEnumerator() => rows.GetEnumerator();

		bool IList.IsFixedSize => false;
		bool IList.IsReadOnly => false;
		bool ICollection.IsSynchronized => true;
		object ICollection.SyncRoot => rows;

		public int Count
		{
			get
			{
				lock (rows)
					return rows.Count;
			}
		} // prop Count

		public object this[int index]
		{
			get
			{
				lock (rows)
					return rows[index];
			}
			set { throw new NotSupportedException(); }
		} // func this

		#endregion

		/// <summary>Access to the child table.</summary>
		public PpsDataTable Table => table;
		/// <summary></summary>
		public bool IsDisposed => isDisposed;
	} // class PpsDataView

	#endregion
}
