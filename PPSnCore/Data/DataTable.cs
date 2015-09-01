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
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using TecWare.DES.Stuff;

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

		private bool lIsInitialized = false;

		protected PpsDataTableDefinition(PpsDataSetDefinition dataset, string tableName)
		{
			this.name = tableName;
			this.dataset = dataset;
			this.columns = new List<PpsDataColumnDefinition>();
			this.columnCollection = new ReadOnlyCollection<PpsDataColumnDefinition>(columns);
			this.relations = new List<PpsDataTableRelationDefinition>();
			this.relationCollection = new ReadOnlyCollection<PpsDataTableRelationDefinition>(relations);
		} // ctor

		public virtual void EndInit()
		{
			lIsInitialized = true;
		} // proc EndInit

		public virtual PpsDataTable CreateDataTable(PpsDataSet dataset)
		{
			return new PpsDataTable(this, dataset);
		} // func CreateDataTable

		protected void AddColumn(PpsDataColumnDefinition column)
		{
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

		/// <summary></summary>
		public PpsDataSetDefinition DataSet => dataset;
		/// <summary>Bezeichnung der Tabelle</summary>
		public string Name { get { return name; } }
		/// <summary>Wurde die Tabelle entgültig geladen.</summary>
		public bool IsInitialized { get { return lIsInitialized; } }
		/// <summary>Column definition</summary>
		public ReadOnlyCollection<PpsDataColumnDefinition> Columns => columnCollection; 
		/// <summary>Attached relations</summary>
		public ReadOnlyCollection<PpsDataTableRelationDefinition> Relations => relationCollection;

		/// <summary>Zugriff auf die Meta-Daten</summary>
		public abstract PpsDataTableMetaCollection Meta { get; }
	} // class PpsDataTableDefinition

	#endregion

	#region -- event ColumnValueChangedEventHandler -------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class ColumnValueChangedEventArgs : EventArgs
	{
		public ColumnValueChangedEventArgs(PpsDataTable table, PpsDataRow row, int columnIndex, object oldValue, object newValue)
		{
			this.Table = table;
			this.Row = row;
			this.ColumnIndex = columnIndex;
			this.OldValue = oldValue;
			this.NewValue = newValue;
		} // ctor

		public PpsDataTable Table { get; }
		public PpsDataRow Row { get; }
		public int ColumnIndex { get; }
		public object OldValue { get; }
		public object NewValue { get; }
	} // class ColumnValueChangedEventArgs

	/// <summary></summary>
	/// <param name="sender"></param>
	/// <param name="e"></param>
	public delegate void ColumnValueChangedEventHandler(object sender, ColumnValueChangedEventArgs e);

	#endregion

	#region -- class PpsDataTable -------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Zugriff auf die Zeilen der Tabelle</summary>
	public class PpsDataTable : IList, IEnumerable<PpsDataRow>, INotifyCollectionChanged, IDynamicMetaObjectProvider
	{
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
				// todo: (ms) schlechter code
				if (String.Compare(binder.Name, "DataSet", StringComparison.OrdinalIgnoreCase) == 0 ||
					String.Compare(binder.Name, "Name", StringComparison.OrdinalIgnoreCase) == 0 ||
					String.Compare(binder.Name, "Count", StringComparison.OrdinalIgnoreCase) == 0 ||
					String.Compare(binder.Name, "AllRows", StringComparison.OrdinalIgnoreCase) == 0 ||
					String.Compare(binder.Name, "OriginalRows", StringComparison.OrdinalIgnoreCase) == 0 ||
					String.Compare(binder.Name, "First", StringComparison.OrdinalIgnoreCase) == 0)
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

		/// <summary>Gibt Auskunft über die Änderungen in der Liste</summary>
		public event NotifyCollectionChangedEventHandler CollectionChanged;
		/// <summary></summary>
		public event ColumnValueChangedEventHandler ColumnValueChanged;

		private PpsDataTableDefinition tableDefinition;  // Zugehörige Definition dieser Tabelle
		private PpsDataSet dataset;            // Eigentümer dieser Tabelle

		private PpsDataRow emptyRow;
		private List<PpsDataRow> rows = new List<PpsDataRow>();         // Alle Datenzeilen
		private List<PpsDataRow> originalRows = new List<PpsDataRow>(); // Alle initial geladenen Datenzeilen
		private List<PpsDataRow> currentRows = new List<PpsDataRow>();  // Alle aktiven nicht gelöschten Datenzeilen

		private ReadOnlyCollection<PpsDataRow> rowsView;
		private ReadOnlyCollection<PpsDataRow> rowsOriginal;

		#region -- Ctor/Dtor --------------------------------------------------------------

		public PpsDataTable(PpsDataTableDefinition tableDefinition, PpsDataSet dataset)
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

		/// <summary>Gets called if row is added.</summary>
		/// <param name="row">The new row</param>
		protected virtual void OnRowAdded(PpsDataRow row)
		{
			OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, row));
		} // proc OnRowAdded

		protected virtual void OnRowRemoved(PpsDataRow row)
		{
			OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, row));
		} // proc OnRowRemoved

		protected internal virtual void OnColumnValueChanged(PpsDataRow row, int columnIndex, object oldValue, object value)
		{
			dataset.OnTableColumnValueChanged(this, row, columnIndex, oldValue, value);
			ColumnValueChanged?.Invoke(this, new ColumnValueChangedEventArgs(this, row, columnIndex, oldValue, value));
		} // proc OnColumnValueChanged

		/// <summary>Benachrichtigt über die Änderung der Tabelle</summary>
		/// <param name="e"></param>
		protected virtual void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
		{
			CollectionChanged?.Invoke(this, e);
		} // proc OnCollectionChanged

		#endregion

		#region -- Add, Remove, Reset, Commit ---------------------------------------------

		internal PpsDataRow AddInternal(bool lOriginal, PpsDataRow row)
		{
			// Zeile anlegen
			rows.Add(row);

			if (lOriginal)
				originalRows.Add(row);
			if (row.RowState != PpsDataRowState.Deleted)
				currentRows.Add(row);

			OnRowAdded(row);

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
				OnRowAdded(row);
			}
			return true;
		} // proc RestoreInternal

		/// <summary></summary>
		/// <param name="row"></param>
		/// <param name="removeOriginal"><c>true</c>, für ein tatsächliches Entfernen.</param>
		/// <returns>Wurde der Eintrag gelöscht</returns>
		internal bool RemoveInternal(PpsDataRow row, bool removeOriginal)
		{
			bool lReturn = false;

			if (row.Table != this)
				throw new InvalidOperationException();

			// Entferne den Eintrag aus der Current-Liste
			if (currentRows.Remove(row))
			{
				OnRowRemoved(row);
				lReturn = true;
			}

			// Entferne den Eintrag aus der Quell-Liste
			int iOriginalIndex = originalRows.IndexOf(row);
			if (iOriginalIndex == -1) // Neu hinzugefügte Zeile
			{
				rows.Remove(row);
				row.Table = null;
				return lReturn;
			}
			else // Orginal geladene Zeile
			{
				if (removeOriginal)
				{
					row.Table = null;
					rows.Remove(row);
					return originalRows.Remove(row);
				}
				else
					return lReturn;
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

		/// <summary>Erzeugt eine neue Zeile.</summary>
		/// <param name="values">Werte, die in der Zeile enthalten sein sollen.</param>
		/// <returns>Neue Datenzeile</returns>
		public PpsDataRow Add(params object[] values)
		{
			if (values != null && values.Length == 0)
				values = null;

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
			Debug.Assert(x.Name.LocalName == PpsDataSet.xnTable); // muss Tabellenelement sein

			foreach (XElement xRow in x.Elements(PpsDataSet.xnRow)) // Zeilen lesen
				AddInternal(xRow.GetAttribute(PpsDataSet.xnRowAdd, "0") != "1", new PpsDataRow(this, xRow));
		} // proc Read

		public void Write(XmlWriter x)
		{
			x.WriteStartElement(PpsDataSet.xnTable.LocalName);

			// Schreibe die Datenzeilen
			foreach (PpsDataRow r in rows)
			{
				x.WriteStartElement(PpsDataSet.xnRow.LocalName);
				r.Write(x);
				x.WriteEndElement();
			}

			x.WriteEndElement();
		} // proc Write

		#endregion

		/// <summary>Zugriff auf das dazugehörige DataSet</summary>
		public PpsDataSet DataSet { get { return dataset; } }
		/// <summary>Zugriff auf die Klasse</summary>
		public PpsDataTableDefinition TableDefinition { get { return tableDefinition; } }
		/// <summary>Name der Tabelle</summary>
		public string Name { get { return tableDefinition.Name; } }
		/// <summary>Zugriff auf die Spalteninformationen</summary>
		public ReadOnlyCollection<PpsDataColumnDefinition> Columns { get { return tableDefinition.Columns; } }

		/// <summary>Gesamtzahl der Datenzeilen in der Tabelle.</summary>
		public int Count { get { return currentRows.Count; } }

		/// <summary>Gibt die erste Zeile, oder eine Leerzeile zurück.</summary>
		public PpsDataRow First { get { return currentRows.Count == 0 ? emptyRow : currentRows[0]; } }
		/// <summary>Zugriff auf alle Datenzeilen</summary>
		public ReadOnlyCollection<PpsDataRow> AllRows { get { return rowsView; } }
		/// <summary>Zugriff auf die originalen Datenzeilen</summary>
		public ReadOnlyCollection<PpsDataRow> OriginalRows { get { return rowsOriginal; } }

		/// <summary>Zugriff auf eine einzelne Datenzeile</summary>
		/// <param name="index"></param>
		/// <returns></returns>
		public PpsDataRow this[int index]
		{
			get { return currentRows[index]; }
			set { throw new NotSupportedException(); }
		} // prop this

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
	/// <summary></summary>
	public abstract class PpsDataFilter : IList, IEnumerable<PpsDataRow>, INotifyCollectionChanged, IDisposable
	{
		public event NotifyCollectionChangedEventHandler CollectionChanged;

		private PpsDataTable table;
		private List<PpsDataRow> rows;

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
			Dispose();
		} // proc Dispose

		/// <summary></summary>
		/// <param name="disposing"></param>
		protected virtual void Dispose(bool disposing)
		{
			if (disposing)
			{
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
						}
						OnCollectionAdd(row);
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

		private void OnCollectionRemove(PpsDataRow row)
		{
			CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, row));
		} // proc OnCollectionRemove

		private void OnCollectionAdd(PpsDataRow row)
		{
			CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, row));
		} // proc OnCollectionAdd

		/// <summary>Belongs the given row to the filter.</summary>
		/// <param name="row">Row to check</param>
		/// <returns><c>true</c>, it belongs to the filter.</returns>
		protected abstract bool FilterRow(PpsDataRow row);

		#endregion

		#region -- IList members ----------------------------------------------------------

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
	} // class PpsDataView

	#endregion
}
