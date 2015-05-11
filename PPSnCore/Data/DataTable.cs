using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
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

		private readonly string sName;
		private List<PpsDataColumnDefinition> columns;
		private ReadOnlyCollection<PpsDataColumnDefinition> columnCollection;

		private bool lIsInitialized = false;

		protected PpsDataTableDefinition(string sTableName)
		{
			this.sName = sTableName;
			this.columns = new List<PpsDataColumnDefinition>();
			this.columnCollection = new ReadOnlyCollection<PpsDataColumnDefinition>(columns);
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

		public PpsDataColumnDefinition FindColumn(string sColumnName)
		{
			return columns.Find(c => String.Compare(c.Name, sColumnName, StringComparison.OrdinalIgnoreCase) == 0);
		} // func FindColumn

		public int FindColumnIndex(string sColumnName)
		{
			return columns.FindIndex(c => String.Compare(c.Name, sColumnName, StringComparison.OrdinalIgnoreCase) == 0);
		} // func FindColumnIndex

		public int FindColumnIndex(string sColumnName, bool lThrowException)
		{
			int iIndex = FindColumnIndex(sColumnName);
			if (iIndex == -1 && lThrowException)
				throw new ArgumentException(String.Format("Spalte '{0}.{1}' nicht gefunden.", Name, sColumnName));
			return iIndex;
		} // func FindColumnIndex

		/// <summary>Bezeichnung der Tabelle</summary>
		public string Name { get { return sName; } }
		/// <summary>Wurde die Tabelle entgültig geladen.</summary>
		public bool IsInitialized { get { return lIsInitialized; } }
		/// <summary>Spaltendefinitionen</summary>
		public ReadOnlyCollection<PpsDataColumnDefinition> Columns { get { return columnCollection; } }
		/// <summary>Zugriff auf die Meta-Daten</summary>
		public abstract PpsDataTableMetaCollection Meta { get; }
	} // class PpsDataTableDefinition

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
							Expression.Property(Expression.Convert(Expression, typeof(PpsDataTable)), ClassPropertyInfo),
							Expression.Constant(table.Class)
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
					PpsDataTable table = (PpsDataTable)Value;
					int iColumnIndex = table.Class.FindColumnIndex(binder.Name);
					if (iColumnIndex == -1)
						return new DynamicMetaObject(table.Class.Meta.GetMetaConstantExpression(binder.Name), GetBindingRestrictions(table));
					else
						return new DynamicMetaObject(
							Expression.MakeIndex(
								Expression.Property(
									Expression.Convert(Expression, typeof(PpsDataTable)),
									ColumnsPropertyInfo
								),
								ReadOnlyCollectionIndexPropertyInfo,
								new Expression[] { Expression.Constant(iColumnIndex) }
							),
							GetBindingRestrictions(table)
						);
				}
			} // func BindGetMember
		} // class PpsDataTableMetaObject

		#endregion

		/// <summary>Gibt Auskunft über die Änderungen in der Liste</summary>
		public event NotifyCollectionChangedEventHandler CollectionChanged;

		private PpsDataTableDefinition tableClass;  // Zugehörige Klasse der Tabelle
		private PpsDataSet dataset;									// Zugeordnetes DataSet

		private PpsDataRow emptyRow;
		private List<PpsDataRow> rows = new List<PpsDataRow>();					// Alle Datenzeilen
		private List<PpsDataRow> originalRows = new List<PpsDataRow>();	// Alle initial geladenen Datenzeilen
		private List<PpsDataRow> currentRows = new List<PpsDataRow>();	// Alle aktiven nicht gelöschten Datenzeilen

		private ReadOnlyCollection<PpsDataRow> rowsView;
		private ReadOnlyCollection<PpsDataRow> rowsOriginal;

		#region -- Ctor/Dtor --------------------------------------------------------------

		public PpsDataTable(PpsDataTableDefinition tableClass, PpsDataSet dataset)
		{
			if (dataset == null)
				throw new ArgumentNullException();

			this.dataset = dataset;
			this.tableClass = tableClass;

			this.emptyRow = new PpsDataRow(this, PpsDataRowState.Unchanged, new object[tableClass.Columns.Count], null);
			this.rowsView = new ReadOnlyCollection<PpsDataRow>(rows);
			this.rowsOriginal = new ReadOnlyCollection<PpsDataRow>(originalRows);
		} // ctor

		DynamicMetaObject IDynamicMetaObjectProvider.GetMetaObject(Expression parameter)
		{
			return new PpsDataTableMetaObject(parameter, this);
		} // func GetMetaObject

		#endregion

		#region -- Collection Changed -----------------------------------------------------

		protected void OnRowAdded(PpsDataRow row)
		{
			OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, row));
		} // proc OnRowAdded

		protected void OnRowRemoved(PpsDataRow row)
		{
			OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, row));
		} // proc OnRowRemoved

		/// <summary>Benachrichtigt über die Änderung der Tabelle</summary>
		/// <param name="e"></param>
		protected virtual void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
		{
			if (CollectionChanged != null)
				CollectionChanged(this, e);
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
		/// <param name="lRemoveOriginal"><c>true</c>, für ein tatsächliches Entfernen.</param>
		/// <returns>Wurde der Eintrag gelöscht</returns>
		internal bool RemoveInternal(PpsDataRow row, bool lRemoveOriginal)
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
				if (lRemoveOriginal)
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

			return AddInternal(false, new PpsDataRow(this, PpsDataRowState.Changed, new object[Columns.Count], values));
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
		bool IList.IsReadOnly		{			get { return true; }		} // es wurde IList.Add, IList.Insert nicht implementiert
		bool ICollection.IsSynchronized { get { return false; } }
		object ICollection.SyncRoot { get { return null; } }

		object IList.this[int index]		{			get			{				return this[index];			}			set			{				throw new NotSupportedException();			}		}

		#endregion

		#region -- Read/Write -------------------------------------------------------------

		/// <summary>Fügt die Daten in die Tabelle ein.</summary>
		/// <param name="x"></param>
		public void Read(XElement x)
		{
			if (x.Name.LocalName == "t") // Tabellendefinition
			{
				foreach (XElement xRow in x.Elements("r")) // Zeilen lesen
					AddInternal(xRow.GetAttribute("a", "0") != "1", new PpsDataRow(this, xRow));
			}
			else
				throw new NotSupportedException();
		} // proc Read

		public void Write(XmlWriter x)
		{
			x.WriteStartElement("t");

			// Schreibe die Datenzeilen
			foreach (PpsDataRow r in rows)
			{
				x.WriteStartElement("r");
				r.Write(x);
				x.WriteEndElement();
			}

			x.WriteEndElement();
		} // proc Write

		#endregion

		/// <summary>Zugriff auf das dazugehörige DataSet</summary>
		public PpsDataSet DataSet { get { return dataset; } }
		/// <summary>Zugriff auf die Klasse</summary>
		public PpsDataTableDefinition Class { get { return tableClass; } }
		/// <summary>Name der Tabelle</summary>
		public string Name { get { return tableClass.Name; } }
		/// <summary>Zugriff auf die Spalteninformationen</summary>
		public ReadOnlyCollection<PpsDataColumnDefinition> Columns { get { return tableClass.Columns; } }

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
		internal static readonly PropertyInfo ClassPropertyInfo;

		static PpsDataTable()
		{
			TypeInfo typeInfo = typeof(PpsDataTable).GetTypeInfo();
			ColumnsPropertyInfo = typeInfo.GetDeclaredProperty("Columns");
			ClassPropertyInfo = typeInfo.GetDeclaredProperty("Class");

			ReadOnlyCollectionIndexPropertyInfo = typeof(ReadOnlyCollection<PpsDataColumnDefinition>).GetTypeInfo().GetDeclaredProperty("Item");

			if (ColumnsPropertyInfo == null || ClassPropertyInfo == null || ReadOnlyCollectionIndexPropertyInfo == null)
				throw new InvalidOperationException("Reflection fehlgeschlagen (PpsDataTable)");
		} // sctor
	} // class PpsDataTable

	#endregion
}
