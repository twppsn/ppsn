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
using System.Collections.ObjectModel;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using TecWare.DE.Stuff;
using static TecWare.PPSn.Data.PpsDataHelper;

namespace TecWare.PPSn.Data
{
	#region -- enum PpsDataSetMetaData --------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Definiert Standardattribute am DataSet.</summary>
	public enum PpsDataSetMetaData
	{
		/// <summary>Titel der Datensammlung.</summary>
		Caption
	} // enum PpsDataSetMetaData

	#endregion

	#region -- enum PpsDataSetAutoTagMode -----------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public enum PpsDataSetAutoTagMode
	{
		First,
		Number,
		Conact
	} // enum PpsDataSetAutoTagMode

	#endregion

	#region -- enum PpsObjectTagClass ---------------------------------------------------

	public enum PpsObjectTagClass : int
	{
		Text = 0,
		Number = 1,
		Date = 2
	} // enum PpsObjectTagClass

	#endregion

	#region -- class PpsObjectTag -------------------------------------------------------

	public sealed class PpsObjectTag
	{
		private readonly string tagName;
		private readonly PpsObjectTagClass cls;
		private readonly object value;

		private PpsObjectTag(XElement tag)
		{
			this.tagName = tag.Name.LocalName;
			this.cls = (PpsObjectTagClass)tag.GetAttribute("c", 0);
			this.value = tag.Value == null ? null : Procs.ChangeType(tag.Value, GetTypeFromClass(cls));
		} // ctor

		public PpsObjectTag(string tagName, PpsObjectTagClass cls, object value)
		{
			this.tagName = tagName;
			this.cls = cls;
			this.value = value;
		} // ctor

		public bool IsValueEqual(object otherValue)
			=> Object.Equals(value, Procs.ChangeType(otherValue, GetTypeFromClass(cls)));

		private XElement GetXTag()
			=> new XElement(tagName,
					new XAttribute("c", (int)cls),
					new XText(value.ChangeType<string>())
				);

		public string Name => tagName;
		public PpsObjectTagClass Class => cls;
		public object Value => value;

		// -- Static ----------------------------------------------------------------------

		public static string FormatTagFields(IEnumerable<PpsObjectTag> tags)
		{
			var xDoc = new XDocument(
				new XElement("tags", from t in tags where t.Value != null select t.GetXTag())
			);
			return xDoc.ToString();
		} // func CreateTagField

		public static IEnumerable<PpsObjectTag> ParseTagFields(string tags)
		{
			var xDoc = XDocument.Parse(tags);
			return from x in xDoc.Root.Elements() select new PpsObjectTag(x);
		} // func ParseTagFields

		public static Type GetTypeFromClass(PpsObjectTagClass cls)
		{
			return typeof(string);
		} // func GetTypeFromClass
	} // class PpsObjectTag

	#endregion

	#region -- class PpsDataSetAutoTagDefinition ----------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PpsDataSetAutoTagDefinition
	{
		private readonly PpsDataSetDefinition datasetDefinition;
		private readonly string tagName;
		private readonly string tableName;
		private readonly string columnName;
		private readonly PpsDataSetAutoTagMode mode;
		private PpsDataColumnDefinition column;

		public PpsDataSetAutoTagDefinition(PpsDataSetDefinition datasetDefinition, string tagName, string tableName, string columnName, PpsDataSetAutoTagMode mode)
		{
			if (datasetDefinition == null)
				throw new ArgumentNullException("datasetDefinition");
			if (String.IsNullOrEmpty( tagName ))
				throw new ArgumentNullException("tagName");
			if (String.IsNullOrEmpty(tableName ))
				throw new ArgumentNullException("tableName");
			if (String.IsNullOrEmpty(columnName))
				throw new ArgumentNullException("columnName");

			this.datasetDefinition = datasetDefinition;
			this.tagName = tagName;
			this.tableName = tableName;
			this.columnName = columnName;
			this.mode = mode;
		} // ctor

		public virtual void EndInit()
		{
			var tableDef = datasetDefinition.FindTable(tableName);
			if (tableDef == null)
				throw new ArgumentException($"Tag '{tagName}' could not initalized. Table '{tableName}' not found.");

			column = tableDef.Columns[columnName];
			if (column == null)
				throw new ArgumentException($"Tag '{tagName}' could not initalized. Column '{tableName}.{columnName}' not found.");
		} // proc EndInit

		public PpsObjectTag GenerateTagValue(PpsDataSet dataset)
		{
			if (column == null)
				throw new ArgumentNullException("column", $"Tag {tagName} not initalized.");

			var table = dataset.Tables[column.Table];
			switch (mode)
			{
				case PpsDataSetAutoTagMode.First:
					return new PpsObjectTag(Name, PpsObjectTagClass.Text, table.Count > 0 ? table[0][column.Index] : null);
				case PpsDataSetAutoTagMode.Conact:
					return new PpsObjectTag(Name, PpsObjectTagClass.Text, table.Count == 0 ? null : String.Join(" ", from c in table select c[column.Index].ToString()));
				case PpsDataSetAutoTagMode.Number:
					goto case PpsDataSetAutoTagMode.First;
				default:
					return null;
			}
		} // func GenerateTagValue

		public PpsDataSetDefinition DataSet => datasetDefinition;

		public string Name => tagName;
		public string TableName => tableName;
		public string ColumnName => columnName;
		public PpsDataSetAutoTagMode Mode => mode;
	} // class PpsDataSetAutoTagDefinition

	#endregion
		
	#region -- class PpsDataSetDefinition -----------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Schema of the collection of data that is arrange in tables.</summary>
	public abstract class PpsDataSetDefinition
	{
		#region -- WellKnownTypes ---------------------------------------------------------

		private static readonly Dictionary<string, Type> wellKnownMetaTypes = new Dictionary<string, Type>()
		{
			{ PpsDataSetMetaData.Caption.ToString(), typeof(string) }
		};

		#endregion

		#region -- class PpsDataSetMetaCollection -----------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		public class PpsDataSetMetaCollection : PpsMetaCollection
		{
			public T GetProperty<T>(PpsDataSetMetaData key, T @default)
				=> PropertyDictionaryExtensions.GetProperty<T>(this, key.ToString(), @default);

			public override IReadOnlyDictionary<string, Type> WellknownMetaTypes => wellKnownMetaTypes;
		} // class PpsDataSetMetaCollection

		#endregion

		private bool isInitialized = false;
		private List<PpsDataTableDefinition> tables;
		private ReadOnlyCollection<PpsDataTableDefinition> tableDefinitions;
		private List<PpsDataSetAutoTagDefinition> tags;
		private ReadOnlyCollection<PpsDataSetAutoTagDefinition> tagDefinitions;

		protected PpsDataSetDefinition()
		{
			this.tables = new List<PpsDataTableDefinition>();
			this.tableDefinitions = new ReadOnlyCollection<PpsDataTableDefinition>(tables);

			this.tags = new List<PpsDataSetAutoTagDefinition>();
			this.tagDefinitions = new ReadOnlyCollection<PpsDataSetAutoTagDefinition>(this.tags);
		} // ctor

		/// <summary>Finish the initialization of the dataset.</summary>
		public virtual void EndInit()
		{
			foreach (var t in TableDefinitions)
				t.EndInit();

			foreach (var t in TagDefinitions)
				t.EndInit();

			isInitialized = true;
		} // proc EndInit

		/// <summary>Durch die Logik, darf die Auflistung der Tabellen nicht geändert werden. Damit die dynamischen Zugriffe nicht gebrochen werden.</summary>
		/// <param name="table"></param>
		protected void Add(PpsDataTableDefinition table)
		{
			if (isInitialized)
				throw new InvalidOperationException($"Can not add table '{table.Name}', because the dataset is initialized.");
			if (table == null)
				throw new ArgumentNullException();
			if (FindTable(table.Name) != null)
				throw new ArgumentOutOfRangeException($"table '{table.Name}' already exists.");

			tables.Add(table);
		} // proc Add

		protected void Add(PpsDataSetAutoTagDefinition tag)
		{
			if (isInitialized)
				throw new InvalidOperationException($"Can not add tag '{tag.Name}', because the dataset is initialized.");
			if (tag == null)
				throw new ArgumentNullException();
			if (FindTag(tag.Name) != null)
				throw new ArgumentOutOfRangeException($"tag '{tag.Name}' already exists.");

			tags.Add(tag);
		} // func Add

		/// <summary>Erzeugt eine Datensammlung aus der Definition</summary>
		/// <returns></returns>
		public virtual PpsDataSet CreateDataSet()
		{
			if (!isInitialized)
				throw new ArgumentException($"{nameof(EndInit)} from the dataset is not called.");

			return new PpsDataSet(this);
		} // func CreateDataSet

		public PpsDataTableDefinition FindTable(string name)
			=> tables.Find(c => String.Compare(c.Name, name, StringComparison.OrdinalIgnoreCase) == 0);

		public PpsDataSetAutoTagDefinition FindTag(string name)
			=> tags.Find(c => String.Compare(c.Name, name, StringComparison.OrdinalIgnoreCase) == 0);

		/// <summary></summary>
		public ReadOnlyCollection<PpsDataSetAutoTagDefinition> TagDefinitions => tagDefinitions;
		/// <summary>Access to the table definitions.</summary>
		public ReadOnlyCollection<PpsDataTableDefinition> TableDefinitions => tableDefinitions;
		/// <summary>Zugriff auf die MetaInformationen</summary>
		public abstract PpsDataSetMetaCollection Meta { get; }
		/// <summary>Is the dataset initialized.</summary>
		public bool IsInitialized => isInitialized;
	} // class PpsDataSetDefinition

	#endregion

	#region -- class PpsDataSet ---------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PpsDataSet : IDynamicMetaObjectProvider
	{
		#region -- class PpsDataSetMetaObject --------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class PpsDataSetMetaObject : DynamicMetaObject
		{
			public PpsDataSetMetaObject(Expression expression, object value)
				: base(expression, BindingRestrictions.Empty, value)
			{
			} // ctor

			private BindingRestrictions GetRestrictions(PpsDataSet dataset)
			{
				return BindingRestrictions.GetExpressionRestriction(
					Expression.AndAlso(
						Expression.TypeIs(Expression, typeof(PpsDataSet)),
						Expression.Equal(
							Expression.Property(Expression.Convert(Expression, typeof(PpsDataSet)), DefinitionPropertyInfo),
							Expression.Constant(dataset.DataSetDefinition)
						)
					)
				);
			} // func GetRestrictions

			public override DynamicMetaObject BindGetMember(GetMemberBinder binder)
			{
				Expression expr;
				PpsDataSet dataset = (PpsDataSet)Value;

				if (PpsDataHelper.IsStandardMember(LimitType, binder.Name))
				{
					return base.BindGetMember(binder);
				}
				else
				{
					// find the table
					var tableIndex = Array.FindIndex(dataset.tables, c => String.Compare(c.TableName, binder.Name) == 0);
					if (tableIndex == -1) // find meta data
						expr = dataset.DataSetDefinition.Meta.GetMetaConstantExpression(binder.Name);
					else
						expr = Expression.ArrayIndex(Expression.Field(Expression.Convert(Expression, typeof(PpsDataSet)), TableFieldInfo), Expression.Constant(tableIndex));

					return new DynamicMetaObject(expr, GetRestrictions(dataset));
				}
			} // func BindGetMember

			public override IEnumerable<string> GetDynamicMemberNames()
			{
				PpsDataSet dataset = (PpsDataSet)Value;

				return
					(from t in dataset.Tables select t.TableName)
					.Concat(from key in dataset.DataSetDefinition.Meta.Keys select key);
			} // func GetDynamicMemberNames
		} // class PpsDataSetMetaObject

		#endregion

		#region -- class TableCollection --------------------------------------------------

		public class TableCollection : ReadOnlyCollection<PpsDataTable>
		{
			internal TableCollection(PpsDataTable[] tables)
				: base(tables)
			{
			} // ctor

			public PpsDataTable this[string tableName, bool throwException = false]
			{
				get
				{
					var table = this.FirstOrDefault(c => String.Compare(c.TableName, tableName, StringComparison.OrdinalIgnoreCase) == 0);
					if (table == null && throwException)
						throw new ArgumentOutOfRangeException("tableName", $"Table '{tableName}' not found.");
					return table;
				}
			} // func this

			public PpsDataTable this[PpsDataTableDefinition tableDefinition] => this.First(c => c.TableDefinition == tableDefinition);
		} // class TableCollection

		#endregion

		private PpsDataSetDefinition datasetDefinition;
		private PpsDataTable[] tables;
		private TableCollection tableCollection;

		private IPpsUndoSink undoSink = null;

		private long lastPrimaryId = -1;
		private object nextPrimaryLock = new object();

		#region -- Ctor/Dtor --------------------------------------------------------------

		public PpsDataSet(PpsDataSetDefinition datasetDefinition)
		{
			this.datasetDefinition = datasetDefinition;
			this.tables = new PpsDataTable[datasetDefinition.TableDefinitions.Count];

			for (int i = 0; i < tables.Length; i++)
				tables[i] = datasetDefinition.TableDefinitions[i].CreateDataTable(this);

			this.tableCollection = new TableCollection(tables);
		} // ctor

		DynamicMetaObject IDynamicMetaObjectProvider.GetMetaObject(Expression parameter)
		{
			return new PpsDataSetMetaObject(parameter, this);
		} // func GetMetaObject

		#endregion

		/// <summary>Registers an undo-manager.</summary>
		/// <param name="undoSink"></param>
		public void RegisterUndoSink(IPpsUndoSink undoSink)
		{
			this.undoSink = undoSink;
		} // proc RegisterUndoSink

		private int FindTableIndex(string tableName)
		{
			return Array.FindIndex(tables, dt => String.Compare(dt.TableName, tableName, StringComparison.OrdinalIgnoreCase) == 0);
		} // func FindTableIndex

		private void ClearInternal()
		{
			// Tabellen
			for (int i = 0; i < tables.Length; i++)
				tables[i].ClearInternal();
		} // proc ClearInternal

		public void Read(XElement x, bool combineData = false)
		{
			if (x.Name != xnData)
				throw new ArgumentException();

			// clear current data, for a fresh load
			if (!combineData)
				ClearInternal();

			// fetch the tables
			foreach (var xTable in x.Elements().Where(c => c.Name.NamespaceName == "table"))
				this.Tables[xTable.Name.LocalName, true].Read(xTable, combineData);
		} // proc Read

		public void Write(XmlWriter x)
		{
			x.WriteStartElement(xnData.LocalName);
			x.WriteAttributeString("xmlns", "t", null, "table");
			foreach (var table in tables)
			{
				x.WriteStartElement(table.TableName, "table");
				table.Write(x);
				x.WriteEndElement();
			}
			x.WriteEndElement();
		} // proc Write

		public void Commit()
		{
			foreach (PpsDataTable t in Tables)
				t.Commit();
		} // proc Commit

		public void Reset()
		{
			foreach (PpsDataTable t in Tables)
				t.Reset();
		} // proc Reset

		/// <summary>Updates to next id.</summary>
		/// <param name="value">Value for a primary column</param>
		public void UpdateNextId(long value)
		{
			lock (nextPrimaryLock)
			{
				if (value < lastPrimaryId)
					lastPrimaryId = value;
			}
		} // proc UpdateNextId

		/// <summary>Returns a next id.</summary>
		/// <returns></returns>
		public long GetNextId()
		{
			lock (nextPrimaryLock)
				return --lastPrimaryId;
		} // func GetNextId

		protected internal virtual void OnTableRowAdded(PpsDataTable table, PpsDataRow row)
		{
		} // proc OnTableRowAdded

		protected internal virtual void OnTableRowDeleted(PpsDataTable table, PpsDataRow row)
		{
		} // proc OnTableRowDeleted

		protected internal virtual void OnTableColumnValueChanged(PpsDataRow row, int iColumnIndex, object oldValue, object value)
		{
		} // proc OnTableColumnValueChanged

		public virtual IEnumerable<PpsObjectTag> GetAutoTags()
		{
			foreach (var tag in DataSetDefinition.TagDefinitions)
			{
				var value = tag.GenerateTagValue(this);
				if (value != null)
					yield return value;
			}
		} // func GetAutoTags

		/// <summary>Zugriff auf die Definition der Datensammlung</summary>
		public PpsDataSetDefinition DataSetDefinition => datasetDefinition;
		/// <summary>Zugriff auf die Tabellendaten.</summary>
		public TableCollection Tables => tableCollection;
		/// <summary></summary>
		public IPpsUndoSink UndoSink => undoSink;

		// -- Static --------------------------------------------------------------

		//private static readonly ConstructorInfo ArgumentOutOfRangeExceptionConstructorInfo;
		private static readonly FieldInfo TableFieldInfo;
		private static readonly PropertyInfo DefinitionPropertyInfo;

		static PpsDataSet()
		{
			var typeInfo = typeof(ArgumentOutOfRangeException).GetTypeInfo();

			//ArgumentOutOfRangeExceptionConstructorInfo =
			//	(
			//		from ci in typeInfo.DeclaredConstructors
			//		let pi = ci.GetParameters()
			//		where pi.Length == 2 && pi[0].ParameterType == typeof(string) && pi[1].ParameterType == typeof(string)
			//		select ci
			//	).FirstOrDefault(); ArgumentOutOfRangeExceptionConstructorInfo == null ||

			typeInfo = typeof(PpsDataSet).GetTypeInfo();
			TableFieldInfo = typeInfo.GetDeclaredField("tables");
			DefinitionPropertyInfo = typeInfo.GetDeclaredProperty("DataSetDefinition");


			if (TableFieldInfo == null || DefinitionPropertyInfo == null)
				throw new ArgumentException("sctor @ PpsDataSet");
		} // sctor
	} // class PpsDataSet

	#endregion
}
