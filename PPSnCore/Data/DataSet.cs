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
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Neo.IronLua;
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
		Caption,
		/// <summary>Default pane uri</summary>
		DefaultPaneUri
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
		Deleted = -1,
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
		private readonly long syncToken;

		public PpsObjectTag(string tagName, PpsObjectTagClass cls, object value, long syncToken = -1)
		{
			this.tagName = tagName;
			this.cls = cls;
			this.value = value;
			this.syncToken = syncToken < 0 ? Procs.GetSyncStamp() : syncToken;
		} // ctor

		public bool IsValueEqual(object otherValue)
			=> Object.Equals(value, Procs.ChangeType(otherValue, GetTypeFromClass(cls)));

		public string Name => tagName;
		public PpsObjectTagClass Class => cls;
		public object Value => value;
		public long SyncToken => syncToken;

		// -- Static ----------------------------------------------------------------------

		private static Regex regAttributeLine = new Regex(@"(?<n>\w+)(\:(?<c>\d*)(\:(?<u>\d*)(\:(?<s>\d*))?)?)?\=(?<v>.*)", RegexOptions.Singleline);

		private static PpsObjectTag CreateKeyValue(string attributeLine)
		{
			//+Key:0:0:0=

			var m = regAttributeLine.Match(attributeLine);
			if (!m.Success)
				throw new FormatException();

			var classHint = (PpsObjectTagClass)(String.IsNullOrEmpty(m.Groups["c"].Value) ? 0 : Int32.Parse(m.Groups["c"].Value));
			object value;
			if (classHint == PpsObjectTagClass.Deleted)
				value = null;
			else
			{
				var dataType = GetTypeFromClass(classHint);
				value = Procs.UnescapeSpecialChars(m.Groups["v"].Value);
				if (value != null)
					value = Procs.ChangeType(value, dataType);
			}

			return new PpsObjectTag(m.Groups["n"].Value, classHint, value, String.IsNullOrEmpty(m.Groups["s"].Value) ? 0 : Int64.Parse(m.Groups["s"].Value));
		} // func CreateKeyValue

		[Obsolete("Not implemented yet.")]
		public static string FormatTagFields(IEnumerable<PpsObjectTag> tags)
		{
			throw new NotImplementedException();
		} // func CreateTagField

		public static IEnumerable<PpsObjectTag> ParseTagFields(string tags)
		{
			if (String.IsNullOrEmpty(tags))
				return Enumerable.Empty<PpsObjectTag>();

			return tags
				.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
				.Select(c => CreateKeyValue(c));
		} // func ParseTagFields

		public static Type GetTypeFromClass(PpsObjectTagClass classHint)
		{
			if (classHint == PpsObjectTagClass.Date)
				return typeof(DateTime);
			// todo:
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

	#region -- enum PpsDataChangeLevel --------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public enum PpsDataChangeLevel
	{
		ExtentedValue = 1,
		PropertyValue = 2,
		RowAdded = 3,
		RowRemoved = 3,
		RowModified = 4,
		TableModifed = 5,
		DataSetModified = 6
	} // enum PpsDataChangeLevel

	#endregion

	#region -- class PpsDataChangedEvent ------------------------------------------------

	public abstract class PpsDataChangedEvent
	{
		public abstract void InvokeEvent();

		public abstract bool Same(PpsDataChangedEvent ev);

		public abstract PpsDataChangeLevel Level { get; }
	} // class PpsDataChangedEvent

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

	#region -- interface IPpsDeferedConstraintCheck -------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public interface IPpsDeferredConstraintCheck
	{
		void Register(Delegate check, string failText, params object[] arguments);
	} // interface IPpsDeferedConstraintCheck

	#endregion

	#region -- class PpsDataSet ---------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PpsDataSet : IDynamicMetaObjectProvider
	{
		#region -- class PpsDataSetMetaObject ---------------------------------------------

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

			private DynamicMetaObject BindTableOrMeta(string name, bool generateException)
			{
				Expression expr;
				PpsDataSet dataset = (PpsDataSet)Value;

				// find the table
				var tableIndex = Array.FindIndex(dataset.tables, c => String.Compare(c.TableName, name, StringComparison.OrdinalIgnoreCase) == 0);
				if (tableIndex == -1) // find meta data
					expr = dataset.DataSetDefinition.Meta.GetMetaConstantExpression(name, generateException);
				else
					expr = Expression.ArrayIndex(Expression.Field(Expression.Convert(Expression, typeof(PpsDataSet)), TableFieldInfo), Expression.Constant(tableIndex));

				return new DynamicMetaObject(expr, GetRestrictions(dataset));
			} // func BindTableOrMeta

			public override DynamicMetaObject BindGetMember(GetMemberBinder binder)
			{
				if (PpsDataHelper.IsStandardMember(LimitType, binder.Name))
					return base.BindGetMember(binder);
				else
					return BindTableOrMeta(binder.Name, false);
			} // func BindGetMember

			public override DynamicMetaObject BindInvokeMember(InvokeMemberBinder binder, DynamicMetaObject[] args)
			{
				if (args.Length > 0 || PpsDataHelper.IsStandardMember(LimitType, binder.Name))
					return base.BindInvokeMember(binder, args);
				else
					return BindTableOrMeta(binder.Name, true);
			} // func BindInvokeMember

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

		#region -- class DynamicRuntimeTable ----------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private class DynamicRuntimeTable : LuaTable
		{
			private readonly PpsDataSet dataset;

			public DynamicRuntimeTable(PpsDataSet dataset)
			{
				this.dataset = dataset;
			} // ctor

			protected override void OnPropertyChanged(string propertyName)
			{
				base.OnPropertyChanged(propertyName);
				dataset.ExecuteDataChanged();
			} // proc OnPropertyChanged

			protected override object OnIndex(object key)
				=> base.OnIndex(key) ?? dataset.GetEnvironmentValue(key);
		} // class DynamicRuntimeTable

		#endregion

		#region -- class PpsDeferedConstraintCheck ----------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class PpsDeferedConstraintCheck : IPpsDeferredConstraintCheck
		{
			private List<Tuple<Delegate, string, object[]>> checks = new List<Tuple<Delegate, string, object[]>>();

			public void ExecuteAll()
			{
				for (var i = 0; i < checks.Count; i++)
				{
					var c = checks[i];
					try
					{
						c.Item1.DynamicInvoke(c.Item3);
					}
					catch (TargetInvocationException e)
					{
						throw new TargetInvocationException(
							String.Format(c.Item2, c.Item3), e.InnerException
						);
					}
				}
			} // proc ExecuteAll

			public void Register(Delegate check, string failText, params object[] arguments)
			{
				checks.Add(new Tuple<Delegate, string, object[]>(check, failText, arguments));
			} // proc Register
		} // class PpsDeferedConstraintCheck

		#endregion

		private PpsDataSetDefinition datasetDefinition;
		private PpsDataTable[] tables;
		private TableCollection tableCollection;

		private IPpsUndoSink undoSink = null;
		private PpsDeferedConstraintCheck deferredConstraintChecks = null;

		private long lastPrimaryId = -1;
		private object nextPrimaryLock = new object();

		private LuaTable properties; // local properties and states, that are not persisted
		private readonly List<LuaTable> eventSinks;

		#region -- Ctor/Dtor --------------------------------------------------------------

		public PpsDataSet(PpsDataSetDefinition datasetDefinition)
		{
			this.datasetDefinition = datasetDefinition;
			this.tables = new PpsDataTable[datasetDefinition.TableDefinitions.Count];

			for (int i = 0; i < tables.Length; i++)
				tables[i] = datasetDefinition.TableDefinitions[i].CreateDataTable(this);

			this.tableCollection = new TableCollection(tables);
			this.properties = new DynamicRuntimeTable(this);
			this.eventSinks = new List<LuaTable>();
		} // ctor

		DynamicMetaObject IDynamicMetaObjectProvider.GetMetaObject(Expression parameter)
		{
			return new PpsDataSetMetaObject(parameter, this);
		} // func GetMetaObject

		#endregion

		#region --  Environment, EventSink ------------------------------------------------

		protected virtual object GetEnvironmentValue(object key)
			=> null;
		
		/// <summary>Registers a new event sink.</summary>
		/// <param name="eventSink"></param>
		public void RegisterEventSink(LuaTable eventSink)
		{
			eventSinks.Add(eventSink);
		} // proc RegisterEventSink

		/// <summary>Unregisters an event sink.</summary>
		/// <param name="eventSink"></param>
		public void UnregisterEventSink(LuaTable eventSink)
		{
			eventSinks.Remove(eventSink);
		} // proc UnregisterEventSink

		/// <summary>Get all event sinks.</summary>
		/// <returns></returns>
		private LuaTable[] GetEventSinks()
			=> eventSinks.ToArray();

		private Task AsyncLua(LuaResult r)
			=> r[0] as Task ?? Task.FromResult<int>(0);

		public LuaResult InvokeLuaFunction(LuaTable t, string methodName, params object[] args)
		{
			var handler = t.GetMemberValue(methodName, rawGet: true);
			if (Lua.RtInvokeable(handler))
				return new LuaResult(Lua.RtInvoke(handler, args));
			return LuaResult.Empty;
		} // func InvokeClientFunction

		public void InvokeEventHandler(string methodName, params object[] args)
		{
			// call the local function
			InvokeLuaFunction(properties, methodName, args);

			// call connected events
			foreach (var s in eventSinks)
				InvokeLuaFunction(s, methodName, args);
		} // proc InvokeEventHandler

		public async Task InvokeEventHandlerAsync(string methodName, params object[] args)
		{
			// call the local function
			await AsyncLua(InvokeLuaFunction(properties, methodName, args));

			// call connected events
			foreach (var s in GetEventSinks())
				await AsyncLua(InvokeLuaFunction(s, methodName, args));
		} // proc InvokeEventHandler

		#endregion

		/// <summary>Registers an undo-manager.</summary>
		/// <param name="undoSink"></param>
		public void RegisterUndoSink(IPpsUndoSink undoSink)
		{
			this.undoSink = undoSink;
		} // proc RegisterUndoSink

		private int FindTableIndex(string tableName)
			=> Array.FindIndex(tables, dt => String.Compare(dt.TableName, tableName, StringComparison.OrdinalIgnoreCase) == 0);

		private void ClearInternal()
		{
			// Tabellen
			for (var i = 0; i < tables.Length; i++)
				tables[i].ClearInternal();
		} // proc ClearInternal

		/// <summary>This functions starts a section without any constraint and foreign key check.</summary>
		/// <param name="disableUndoStack">Do not add to use undo stack. The undo-stack will be cut.</param>
		/// <returns></returns>
		public IDisposable BeginData(bool disableUndoStack = true)
		{
			if (deferredConstraintChecks != null)
				throw new InvalidOperationException();

			// detach undo sink
			var tmp = disableUndoStack && undoSink != null ? undoSink : null;
			if (tmp != null)
			{
				undoSink.ResetUndoStack();
				undoSink = null;
			}

			// start deferred contraints
			deferredConstraintChecks = new PpsDeferedConstraintCheck();

			return new DisposableScope(
				() =>
				{
					// executes constaint checks
					deferredConstraintChecks.ExecuteAll();
					deferredConstraintChecks = null;

					// attach undo sink
					if (tmp != null)
						undoSink = tmp;
				});
		} // func BeginData

		/// <summary>Reads the structur into the dataset.</summary>
		/// <param name="x">data of the dataset</param>
		/// <param name="combineData"><c>true</c>, to combine the data. <c>false</c>, clear the data first.</param>
		public void Read(XElement x, bool combineData = false)
		{
			if (x.Name != xnData)
				throw new ArgumentException();

			using (BeginData(true))
			{
				// clear current data, for a fresh load
				if (!combineData)
					ClearInternal();

				// fetch the tables
				foreach (var xTable in x.Elements().Where(c => c.Name.NamespaceName == "table"))
					this.Tables[xTable.Name.LocalName, true].Read(xTable, combineData);
			}
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

		public string GetAsString()
		{
			var sb = new StringBuilder();
			using (var tw = new StringWriter(sb))
			using (var xml = XmlWriter.Create(tw, Procs.XmlWriterSettings))
				Write(xml);
			return sb.ToString();
		} // func GetAsString

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

		#region -- ExecuteEvent -----------------------------------------------------------

		private bool inChanged = false;
		private List<PpsDataChangedEvent> changedEvents = new List<PpsDataChangedEvent>();
		/*
		 * Sortierte Liste der Ereignisse. Idee:
		 * Zuerst werden die Properties ausgelöst, wenn diese sich beruhigt haben started die nächste ebene.
		 */

		internal void ExecuteEvent(PpsDataChangedEvent ev)
		{
			if (inChanged)
			{
				var i = 0;
				var length = changedEvents.Count;
				PpsDataChangedEvent cur;
				while (i < length && (cur = changedEvents[i]).Level <= ev.Level)
				{
					if (cur.Same(ev))
						return; // same event, no add needed
					i++;
				}

				changedEvents.Insert(i, ev);
			}
			else
			{
				inChanged = true;
				try
				{
					// clear list to zero
					changedEvents.Clear();

					// invoke the event and start the loop
					ev.InvokeEvent();

					// invoke related events
					var sw = Stopwatch.StartNew();
					while (changedEvents.Count > 0)
					{
						var  cur = changedEvents[0];
						changedEvents.RemoveAt(0);

						cur.InvokeEvent();

						if (sw.ElapsedMilliseconds > 1000)
						{
							Debug.WriteLine("PPSDataSet: StopEvent work due timeout.");
							break;
						}
					}
				}
				finally
				{
					inChanged = false;
				}
			}
		} // proc ExecuteEvent

		#endregion

		#region -- class PpsDataSetChangedEvent -------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class PpsDataSetChangedEvent : PpsDataChangedEvent
		{
			private readonly PpsDataSet dataset;

			public PpsDataSetChangedEvent(PpsDataSet dataset)
			{
				this.dataset = dataset;
			} // ctor

			public override void InvokeEvent()
				=> dataset.OnDataChanged();


			public override bool Same(PpsDataChangedEvent ev)
				=> true;

			public override PpsDataChangeLevel Level => PpsDataChangeLevel.DataSetModified;
		} // class PpsDataSetChangedEvent

		#endregion

		#region -- Events -----------------------------------------------------------------

		internal void ExecuteDataChanged()
			=> ExecuteEvent(new PpsDataSetChangedEvent(this));

		protected virtual void OnDataChanged()
			=> InvokeEventHandler("OnDataChanged", this);

		protected internal virtual void OnTableChanged(PpsDataTable table)
			=> InvokeEventHandler("OnTableChanged", table);
		
		protected internal virtual void OnTableRowAdded(PpsDataTable table, PpsDataRow row)
			=> InvokeEventHandler("OnTableRowAdded", table, row);

		protected internal virtual void OnTableRowDeleted(PpsDataTable table, PpsDataRow row)
			=> InvokeEventHandler("OnTableRowDeleted", table, row);

		protected internal virtual void OnTableRowChanged(PpsDataTable table, PpsDataRow row)
			=> InvokeEventHandler("OnTableRowChanged", table, row);

		protected internal virtual void OnTableColumnValueChanged(PpsDataRow row, string propertyName, object oldValue, object value)
			=> InvokeEventHandler("OnTableColumnValueChanged", row, propertyName, oldValue, value);

		protected internal virtual void OnTableColumnExtendedValueChanged(PpsDataRow row, string columnName, object value, string propertyName)
			=> InvokeEventHandler("OnTableColumnExtendedValueChanged", row, columnName, value, propertyName);

		#endregion

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
		/// <summary>Local properties and functions for the dataset.</summary>
		public LuaTable Properties => properties;

		/// <summary></summary>
		public IPpsDeferredConstraintCheck DeferredConstraints => deferredConstraintChecks;

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
