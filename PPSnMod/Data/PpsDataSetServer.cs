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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Server;
using TecWare.DE.Server.Http;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;
using static TecWare.PPSn.Server.PpsStuff;

namespace TecWare.PPSn.Server.Data
{
	#region -- enum PpsDataColumnParentRelationType -------------------------------------

	public enum PpsDataColumnParentRelationType
	{
		None,
		Root,
		Relation
	} // enum PpsDataColumnParentRelationType

	#endregion

	#region -- class PpsDataValueColumnServerDefinition ---------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsDataColumnServerDefinition : PpsDataColumnDefinition
	{
		#region -- class PpsDataColumnMetaCollectionServer --------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class PpsDataColumnMetaCollectionServer : PpsDataColumnMetaCollection
		{
			public PpsDataColumnMetaCollectionServer(XElement xColumnDefinition)
			{
				PpsDataSetServerDefinition.AddMetaFromElement(xColumnDefinition, WellknownMetaTypes, Add);
			} // ctor

			public PpsDataColumnMetaCollectionServer(PpsDataColumnMetaCollectionServer clone)
				: base(clone)
			{
			} // ctor

			public void Update(string key, Type type, object value)
			{
				Add(key, () => type, value);
			} // proc Update
		} // class PpsDataColumnMetaCollectionServer

		#endregion

		private readonly string fieldName;
		private readonly PpsDataColumnMetaCollectionServer metaInfo;
		private readonly PpsDataSetParameterServerDefinition relatedParameter;
		private PpsFieldDescription fieldDescription = null;

		private readonly PpsDataColumnParentRelationType parentType = PpsDataColumnParentRelationType.None;
		private readonly string parentTableName;
		private readonly string parentColumnName;

		private PpsDataColumnServerDefinition(PpsDataTableDefinition tableDefinition, PpsDataColumnServerDefinition clone)
			: base(tableDefinition, clone)
		{
			this.fieldName = clone.fieldName;
			this.metaInfo = new PpsDataColumnMetaCollectionServer(clone.metaInfo);
			this.relatedParameter = clone.relatedParameter;
			this.fieldDescription = clone.fieldDescription;
			this.parentTableName = clone.parentTableName;
			this.parentColumnName = clone.parentColumnName;
		} // ctor

		private PpsDataColumnServerDefinition(PpsDataTableDefinition tableDefinition, string fieldName, string columnName, bool isPrimaryKey, bool createRelationColumn, XElement config)
			: base(tableDefinition, columnName, isPrimaryKey)
		{
			this.fieldName = fieldName;

			// relation
			if (createRelationColumn)
			{
				SetRelationName(config.GetAttribute("relationName", (string)null));
				this.parentType = config.GetAttribute("parentType", PpsDataColumnParentRelationType.None);
				this.parentTableName = config.GetAttribute("parentTable", (string)null);
				this.parentColumnName = config.GetAttribute("parentColumn", (string)null);
			}
			else
			{
				this.parentType = PpsDataColumnParentRelationType.None;
				this.parentTableName = null;
				this.parentColumnName = null;
			}

			// find related parameter
			var parameterName = config.GetAttribute("parameter", String.Empty);
			if (!String.IsNullOrEmpty(parameterName))
			{
				this.relatedParameter = ((PpsDataSetServerDefinition)tableDefinition.DataSet).FindParameter(parameterName);
				if (relatedParameter == null)
					throw new DEConfigurationException(config, $"Parameter '{parameterName}' not found.");
			}
			else
				this.relatedParameter = null;

			this.metaInfo = new PpsDataColumnMetaCollectionServer(config);
		} // ctor

		public static PpsDataColumnServerDefinition Create(PpsDataTableDefinition tableDefinition, bool createRelationColumn, XElement config)
		{
			var columnName = config.GetAttribute("name", (string)null);
			var isPrimary = config.GetAttribute("isPrimary", false);

			if (String.IsNullOrEmpty(columnName))
				throw new DEConfigurationException(config, $"@name is empty.");

			var fieldName = config.GetAttribute("fieldName", (string)null);
			if (String.IsNullOrEmpty(fieldName))
				throw new DEConfigurationException(config, "@fieldName is empty.");

			if (createRelationColumn)
				return new PpsDataColumnServerDefinition(tableDefinition, fieldName, columnName, isPrimary, true, config);
			else
				return new PpsDataColumnServerDefinition(tableDefinition, fieldName, columnName, isPrimary, false, config);
		} // func Create

		public override PpsDataColumnDefinition Clone(PpsDataTableDefinition tableOwner)
			=> new PpsDataColumnServerDefinition(tableOwner, this);

		public override void EndInit()
		{
			// update the relation
			if (parentColumnName != null)
				SetParentColumn(null, parentTableName, parentColumnName);

			// resolve the correct field
			var application = ((PpsDataSetServerDefinition)Table.DataSet).Application;
			fieldDescription = application.GetFieldDescription(FieldName, true);

			// update the meta information
			foreach (var c in fieldDescription)
			{
				if (metaInfo.ContainsKey(c.Name))
					metaInfo.Update(c.Name, c.Type, c.Value);
			}

			base.EndInit();
		} // proc EndInit

		public void WriteSchema(XElement xTable)
		{
			var xColumn = new XElement("column");

			// meta data
			PpsDataSetServerDefinition.WriteSchemaMetaInfo(xColumn, metaInfo);

			// information
			xTable.Add(
				xColumn = new XElement("column",
					new XAttribute("name", Name),
					new XAttribute("datatype", LuaType.GetType(DataType).AliasOrFullName)
				)
			);

			WriteColumnSchema(xColumn);
		} // proc WriteScheam

		public void WriteColumnSchema(XElement xColumn)
		{
			//// Setze die Meta-Daten
			//foreach (var m in metaInfo)
			//{
			//	xColumn.Add(new XElement(m.Key,
			//		new XAttribute("datatype", LuaType.GetType(m.Value.GetType()).AliasOrFullName),
			//		m.Value.ChangeType<string>()));
			//}
		} // proc WriteColumnSchema

		protected override Type GetDataType()
			=> fieldDescription?.DataType ?? typeof(object);

		public override PpsDataColumnMetaCollection Meta => metaInfo;
		
		public PpsDataSetParameterServerDefinition RelatedParameter
			=> relatedParameter;

		private string FieldName => fieldName;

		public PpsFieldDescription FieldDescription => fieldDescription;
		public PpsDataColumnParentRelationType ParentType => parentType;

		public override bool IsInitialized => fieldDescription != null;
	} // class PpsDataColumnServerDefinition

	#endregion

	#region -- class PpsDataTableServerDefinition ---------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PpsDataTableServerDefinition : PpsDataTableDefinition
	{
		private static readonly Dictionary<string, Type> wellknownMetaTypes = new Dictionary<string, Type>();

		#region -- class PpsDataTableMetaCollectionServer ---------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class PpsDataTableMetaCollectionServer : PpsDataTableMetaCollection
		{
			public void Add(XElement xMeta)
			{
				PpsDataSetServerDefinition.AddMetaFromElement(xMeta, WellknownMetaTypes, Add);
			} // proc Add
		} // class PPSnDataTableMetaCollectionServer

		#endregion

		private readonly PpsDataTableMetaCollectionServer metaInfo = new PpsDataTableMetaCollectionServer();

		public PpsDataTableServerDefinition(PpsDataSetServerDefinition dataset, string tableName, XElement xTable)
			: base(dataset, tableName)
		{
			foreach (var c in xTable.Elements())
			{
				if (c.Name == xnColumn)
					AddColumn(PpsDataColumnServerDefinition.Create(this, false, c));
				else if (c.Name == xnRelation)
					AddColumn(PpsDataColumnServerDefinition.Create(this, true, c));
				else if (c.Name == xnMeta)
					metaInfo.Add(c);
				//else
				//	throw new InvalidCo
			}
		} // ctor

		protected override void EndInit()
		{
			// fetch column information
			foreach (PpsDataColumnServerDefinition c in Columns)
				c.EndInit();

			base.EndInit();
		} // proc EndInit

		public void Merge(PpsDataTableDefinition t)
		{
			if (IsInitialized)
				throw new ArgumentException($"table '{Name}' is initialized.");

			// merge meta info
			metaInfo.Merge(t.Meta);

			// merge columns
			foreach (var cur in t.Columns)
				AddColumn(cur.Clone(this));
		} // proc Merge

		public override PpsDataTable CreateDataTable(PpsDataSet dataset)
			=> new PpsDataTableServer(this, dataset);

		public void WriteSchema(XElement xSchema)
		{
			var xTable = new XElement("table");
			xTable.SetAttributeValue("name", Name);

			// write meta data
			PpsDataSetServerDefinition.WriteSchemaMetaInfo(xTable, metaInfo);

			// write the columns
			foreach (PpsDataColumnServerDefinition column in Columns)
				column.WriteSchema(xTable);

			xSchema.Add(xTable);
		} // proc WriteSchema

		public override PpsDataTableMetaCollection Meta => metaInfo;
	} // class PpsDataTableServerDefinition

	#endregion

	#region -- class PpsDataTableServer -------------------------------------------------

	public sealed class PpsDataTableServer : PpsDataTable
	{
		public PpsDataTableServer(PpsDataTableDefinition definition, PpsDataSet dataset)
			: base(definition, dataset)
		{
		} // ctor
	} // class PpsDataTableServer

	#endregion

	#region -- class PpsDataSetParameterServerDefinition --------------------------------

	public class PpsDataSetParameterServerDefinition
	{
		private readonly PpsDataSetServerDefinition dataset;
		private readonly string name;
		private readonly string fieldName;
		private readonly bool isNullable;
		private PpsFieldDescription field = null;

		public PpsDataSetParameterServerDefinition(PpsDataSetServerDefinition dataset, string name, string fieldName, bool isNullable)
		{
			this.dataset = dataset;
			this.name = name;
			this.fieldName = fieldName;
			this.isNullable = isNullable;
		} // ctor

		public void EndInit()
		{
			field = dataset.Application.GetFieldDescription(fieldName);
		} // proc EndInit

		public string Name => name;
		public string VariableName => "@" + name;
		public PpsFieldDescription FieldDescription => field;

		public bool IsInitialized => field != null;

		public bool IsNullable => isNullable;
	} // class PpsDataSetParameterServerDefinition

	#endregion

	#region -- class PpsDataSetServerDefinition -----------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Definiert eine Datenklasse aus der Sicht des Servers heraus.</summary>
	public class PpsDataSetServerDefinition : PpsDataSetDefinition, IServiceProvider
	{
		#region -- class PpsDataSetMetaCollectionServerDefinition -------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class PpsDataSetMetaCollectionServerDefinition : PpsDataSetMetaCollection
		{
			public void Add(XElement xMeta)
			{
				if (xMeta.IsEmpty)
					return;

				AddMetaFromElement(xMeta, WellknownMetaTypes, Add);
			} // proc Add
		} // class PpsDataSetMetaCollectionServerDefinition

		#endregion

		private readonly PpsApplication application;
		private readonly string name;
		private readonly string[] inheritedFrom;
		private string[] scripts;
		private PpsDataSetMetaCollectionServerDefinition metaInfo = new PpsDataSetMetaCollectionServerDefinition();

		private readonly List<PpsDataSetParameterServerDefinition> parameters = new List<PpsDataSetParameterServerDefinition>();

		public PpsDataSetServerDefinition(IServiceProvider sp, string name, XElement config)
		{
			this.application = sp.GetService<PpsApplication>(true);
			this.name = name;

			// get inherited list
			this.inheritedFrom = config.GetStrings("inherited", true);

			// get script list
			this.scripts = config.GetStrings("scripts", true);
			
			// parse data table schema
			foreach (var cur in config.Elements())
			{
				if (cur.Name == xnTable)
				{
					var tableName = cur.GetAttribute("name", String.Empty);
					if (String.IsNullOrEmpty(tableName))
						throw new DEConfigurationException(cur, "table needs a name.");

					// create a table
					var table = FindTable(tableName);
					if (table != null)
						throw new DEConfigurationException(cur, $"table is not unique ('{tableName}')");

					Add(CreateTableDefinition(tableName, cur));
				}
				else if (cur.Name == xnMeta)
				{
					metaInfo.Add(cur);
				}
				else if (cur.Name == xnParameter)
				{
					var parameterName = cur.GetAttribute("name", String.Empty);
					var fieldName = cur.GetAttribute("fieldName", String.Empty);

					if (String.IsNullOrEmpty(parameterName))
						throw new DEConfigurationException(cur, "parameter needs a name.");
					if (String.IsNullOrEmpty(fieldName))
						throw new DEConfigurationException(cur, "parameter needs a name.");

					// check unique
					if (ExistParameterByName(parameterName))
						throw new DEConfigurationException(cur, "parameter is not unique.");

					// add parameter
					parameters.Add(new PpsDataSetParameterServerDefinition(this, parameterName, fieldName, cur.GetAttribute("isNullable", false)));
				}
			} // foreach c
		} // ctor

		protected virtual PpsDataTableServerDefinition CreateTableDefinition(string tableName, XElement config)
			=> new PpsDataTableServerDefinition(this, tableName, config);

		public Task InitializeAsync()
			=> Task.Run(new Action(EndInit));

		public override void EndInit()
		{
			// embed inherited
			var scriptList = new List<string>();
			if (inheritedFrom != null)
			{
				foreach (var c in inheritedFrom)
				{
					// find dataset 
					var datasetDefinition = application.GetDataSetDefinition(c);
					if (datasetDefinition == null)
						throw new ArgumentNullException($"DataSet '{c}' not found.");

					// check compatiblity
					if (!GetType().IsAssignableFrom(datasetDefinition.GetType()))
						throw new ArgumentException("Incompatible datasources"); // todo:

					// combine scripts
					if (datasetDefinition.scripts != null && datasetDefinition.scripts.Length > 0)
						scriptList.AddRange(datasetDefinition.scripts);

					// combine meta information
					metaInfo.Merge(datasetDefinition.Meta);

					// combine parameter
					foreach (var cur in datasetDefinition.parameters)
						if (!ExistParameterByName(cur.Name))
							parameters.Add(cur);

					// combine dataset
					foreach (var t in datasetDefinition.TableDefinitions)
					{
						var mergeTable = FindTable(t.Name);
						if (mergeTable == null)
							Add(t);
						else
							((PpsDataTableServerDefinition)mergeTable).Merge(t);
					}
				}
			}

			// add own scripts
			if (scripts != null && scripts.Length > 0)
				scriptList.AddRange(scripts);
			scripts = scriptList.ToArray();

			// resolve parameters
			foreach (var cur in parameters)
			{
				if (!cur.IsInitialized)
					cur.EndInit();
			}

			// resolve tables
			 base.EndInit();
		} // proc EndInit

		private bool ExistParameterByName(string parameterName)
			=>  FindParameter(parameterName) != null;

		public PpsDataSetParameterServerDefinition FindParameter(string parameterName)
			=> parameters.Find(c => String.Compare(c.Name, parameterName, StringComparison.OrdinalIgnoreCase) == 0);

		public object GetService(Type serviceType)
			=> application.GetService(serviceType);

		public override PpsDataSet CreateDataSet()
			=> new PpsDataSetServer(this);

		public XElement WriteSchema(XElement xSchema)
		{

			//// Wann wurde das Schema geladen
			//// todo

			// write the meta data for the dataset
			WriteSchemaMetaInfo(xSchema, metaInfo);

			// write the tables
			foreach (PpsDataTableServerDefinition t in TableDefinitions)
				t.WriteSchema(xSchema);

			return xSchema;
		} // func WriteSchema

		public string Name => name;
		public override PpsDataSetMetaCollection Meta => metaInfo;
		public IReadOnlyList<PpsDataSetParameterServerDefinition> Parameters => parameters;
		public PpsApplication Application => application;

		// -- Static --------------------------------------------------------------

		public static void AddMetaFromElement(XElement xMeta, IReadOnlyDictionary<string, Type> wellknownTypes, Action<string, Func<Type>, object> add)
		{
			if (xMeta == null)
				return;

			var name = xMeta.GetAttribute("name", String.Empty);
			if (String.IsNullOrEmpty(name))
				throw new DEConfigurationException(xMeta, "@name is empty.");

			add(name, () => LuaType.GetType(xMeta.GetAttribute("datatype", "object")), xMeta.Value);
		} // proc AddMetaFromElement

		public static void WriteSchemaMetaInfo(XElement xParent, PpsMetaCollection metaInfo)
		{
			if (metaInfo.Count > 0)
			{
				var xMeta = new XElement("meta");
				xParent.Add(xMeta);
				foreach (var m in metaInfo)
				{
					xMeta.Add(new XElement(m.Key,
						new XAttribute("datatype", LuaType.GetType(m.Value.GetType()).AliasOrFullName),
						m.Value.ChangeType<string>()
					));
				}
			}
		} // proc WriteSchemaMetaInfo
	} // class PpsDataSetServerDefinition

	#endregion

	#region -- interface IPpsLoadableDataSet --------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public interface IPpsLoadableDataSet
	{
		/// <summary></summary>
		/// <param name="properties"></param>
		void OnBeforeLoad(IPropertyReadOnlyDictionary properties);
		/// <summary></summary>
		/// <param name="properties"></param>
		void OnLoad(IPropertyReadOnlyDictionary properties);
		/// <summary></summary>
		/// <param name="properties"></param>
		void OnAfterLoad(IPropertyReadOnlyDictionary properties);
	} // interface IPpsLoadableDataSet

	#endregion


	#region -- class PpsDataSetServer ---------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Serverseitige Implementierung des DataSets</summary>
	public class PpsDataSetServer : PpsDataSet
	{
		#region -- enum PpsDataTrigger ----------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private enum PpsDataTrigger
		{
			/// <summary>Wird aufgerufen, bevor die Daten aus der Datenbank geladen werden. Es können Argumente und das Sql-Script angepasst werden.</summary>
			OnBeforeLoad,
			/// <summary>Wird aufgerufen, bevor die Daten aus der Datenbank gelesen werden. Daten die vom Sql-Script angefordert werden, müssen abgeholt werden.</summary>
			OnExecuteLoad,
			/// <summary>Wird augerufen, nachdem die daten aus der Datenbank gelesen wurden.</summary>
			OnAfterLoad
		} // enum PpsDataTrigger

		#endregion

		public PpsDataSetServer(PpsDataSetServerDefinition datasetClass)
			: base(datasetClass)
		{
		} // ctor

		private void ExecuteTrigger(PpsDataTrigger trigger, params object[] args)
		{
			//foreach (var s in DataSetDefinition.Scripts)
			//{
			//	object memberValue = s.GetMemberValue(trigger.ToString());
			//	if (memberValue != null)
			//		Lua.RtInvoke(memberValue, args);
			//}
		} // func ExecuteTrigger

		public void Load(IDEContext args)
		{
			//IXdeLockedConnection con = args.Caller.User.GetService<IXdeLockedConnection>(true);
			//using (SqlCommand cmd = con.CreateCommand())
			//{
			//	StringBuilder sbCommand = new StringBuilder();

			//	// Publiziere den Command
			//	args.SetMemberValue("SqlText", sbCommand);
			//	args.SetMemberValue("SqlCommand", cmd);

			//	ExecuteTrigger(PPSnDataTrigger.OnBeforeLoad, this, args);

			//	// Erzeuge das Script zum Laden der Daten
			//	foreach (PPSnDataTable table in Tables)
			//		((PPSnDataTableServerClass)table.Class).PrepareLoad(cmd, sbCommand, args);

			//	// Fetch die Daten, wenn Daten abgefragt wurden
			//	if (sbCommand.Length > 0)
			//	{
			//		cmd.CommandText = sbCommand.ToString();
			//		using (SqlDataReader r = cmd.ExecuteReader())
			//		{
			//			ExecuteTrigger(PPSnDataTrigger.OnExecuteLoad, this, r, args);

			//			foreach (PPSnDataTable table in Tables)
			//				((PPSnDataTableServerClass)table.Class).FinishLoad(r, table);
			//		}
			//	}

			//	ExecuteTrigger(PPSnDataTrigger.OnAfterLoad, this, args);
			//}
		} // proc Load

		public new PpsDataSetServerDefinition DataSetDefinition => (PpsDataSetServerDefinition)base.DataSetDefinition;
	} // class PpsDataSetServer

	#endregion
}
