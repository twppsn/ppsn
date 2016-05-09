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
	#region -- class PpsDataColumnDefinitionServer --------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PpsDataColumnDefinitionServer : PpsDataColumnDefinition
	{
		#region -- class PpsDataColumnMetaCollectionServer --------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class PpsDataColumnMetaCollectionServer : PpsDataColumnMetaCollection
		{
			public PpsDataColumnMetaCollectionServer(XElement xColumnDefinition)
			{
				//PPSnDataSetServerClass.LoadMetaFromConfiguration(xColumnDefinition, WellknownMetaTypes, Add);
			} // ctor

			public void Update(string key, Type type, object value)
			{
				Add(key, () => type, value);
			} // proc Update
		} // class PpsDataColumnMetaCollectionServer

		#endregion

		private readonly string fieldName;
		private readonly PpsDataColumnMetaCollectionServer metaInfo;
		private PpsFieldDescription fieldDescription = null;
		
		public PpsDataColumnDefinitionServer(PpsDataTableDefinition tableDefinition, XElement xColumn)
			: base(tableDefinition, xColumn.GetAttribute("name", String.Empty))
		{
			this.fieldName = xColumn.GetAttribute("fieldName", (string)null);
			if (String.IsNullOrEmpty(fieldName))
				throw new DEConfigurationException(xColumn, "@fieldName is empty.");

			this.metaInfo = new PpsDataColumnMetaCollectionServer(xColumn);
		} // ctor

		public override void EndInit()
		{
			// resolve the correct field
			var application = ((PpsDataSetServerDefinition)Table.DataSet).Application;
			fieldDescription = application.GetFieldDescription(fieldName, true);

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

		protected virtual void WriteColumnSchema(XElement xColumn)
		{
			//// Setze die Meta-Daten
			//foreach (var m in metaInfo)
			//{
			//	xColumn.Add(new XElement(m.Key,
			//		new XAttribute("datatype", LuaType.GetType(m.Value.GetType()).AliasOrFullName),
			//		m.Value.ChangeType<string>()));
			//}
		} // proc WriteColumnSchema

		public override Type DataType => fieldDescription?.DataType ?? typeof(object);
		public override PpsDataColumnMetaCollection Meta => metaInfo;

		public PpsFieldDescription FieldDescription => fieldDescription;

		public override bool IsInitialized => fieldDescription != null;
	} // class PpsDataColumnDefinitionServer

	#endregion

	#region -- class PpsDataSqlColumnServer ---------------------------------------------

	/////////////////////////////////////////////////////////////////////////////////
	///// <summary></summary>
	//public class PpsDataSqlColumnServer : PpsDataColumnServer
	//{
	//	//private string sTableName;
	//	//private string sColumnName;
	//	//private SqlDbType sqlType = (SqlDbType)(-1);
	//	//private int iLength = -1;

	//	public PpsDataSqlColumnServer(PpsDataTableDefinition tableDefinition, XElement xColumn)
	//		: base(tableDefinition, xColumn)
	//	{
	//		//string sFieldDefinition = xColumn.GetAttribute("fieldname", String.Empty);
	//		//if (String.IsNullOrEmpty(sFieldDefinition))
	//		//	throw new DEConfigException(xColumn, "Kein Sql-Mapping angegeben.");

	//		//string[] fieldNameParts = sFieldDefinition.Split('.');
	//		//if (fieldNameParts.Length >= 2 && fieldNameParts.Length <= 3)
	//		//{
	//		//	// Parse den Namen
	//		//	sTableName = fieldNameParts.Length == 2 ? fieldNameParts[0] : fieldNameParts[0] + '.' + fieldNameParts[1];
	//		//	sColumnName = fieldNameParts[fieldNameParts.Length - 1];
	//		//}
	//		//else
	//		//	throw new DEConfigException(xColumn, "Mapping konnte nicht geparst werden.");
	//	} // ctor

	//	//internal void InitSql(SqlDataReader r, int i, DataTable dtSchema)
	//	//{
	//	//	//// Setze den Typ
	//	//	//DataType = r.GetFieldType(i);

	//	//	//// Hole die weiteren Eigenschaften ab
	//	//	//DataRow rowSchema = dtSchema.Rows[i];
	//	//	//sqlType = (SqlDbType)(int)rowSchema["ProviderType"];
	//	//	//if (sqlType == SqlDbType.NVarChar ||
	//	//	//	sqlType == SqlDbType.NChar ||
	//	//	//	sqlType == SqlDbType.Char ||
	//	//	//	sqlType == SqlDbType.VarChar ||
	//	//	//	sqlType == SqlDbType.Binary ||
	//	//	//	sqlType == SqlDbType.VarBinary)
	//	//	//{
	//	//	//	iLength = (int)rowSchema["ColumnSize"];

	//	//	//	// Aktualisiere die Meta Daten
	//	//	//	UpdateMetaData("MaxLength", typeof(int), iLength.ToString());
	//	//	//}
	//	//} // proc InitSql

	//	//public bool IsPrimary { get { return Definition.GetAttribute("primary", false); } }
	//	//public bool IsNullable { get { return Definition.GetAttribute("nullable", true); } }

	//	//public SqlDbType SqlType { get { return sqlType; } }
	//	//public int SqlColumnSize { get { return iLength; } }

	//	//public string SqlTableName { get { return sTableName; } }
	//	//public string SqlColumnName { get { return sColumnName; } }
	//} // class PpsDataSqlColumnServer

	#endregion

	#region -- class PpsDataSqlRelationColumnServer -------------------------------------

	//public class PpsDataSqlRelationColumnServer : PpsDataSqlColumnServer
	//{
	//	//private string sParentTable;
	//	//private string sParentColumn;

	//	public PpsDataSqlRelationColumnServer(PpsDataTableDefinition tableDefinition, XElement xColumn)
	//		: base(tableDefinition, xColumn)
	//	{
	//		//this.sParentTable = xColumn.GetAttribute("parentTable", String.Empty);
	//		//this.sParentColumn = xColumn.GetAttribute("parentColumn", String.Empty);
	//	} // ctor

	//	protected override void WriteColumnSchema(XElement xColumn)
	//	{
	//		base.WriteColumnSchema(xColumn);

	//		//xColumn.SetAttributeValue("parentTable", sParentTable);
	//		//xColumn.SetAttributeValue("parentColumn", sParentColumn);
	//	} // proc WriteWriteColumnSchema

	//	//public string ParentTable { get { return sParentTable; } }
	//	//public string ParentColumn { get { return sParentColumn; } }
	//} // class PpsDataSqlRelationColumnServer

	#endregion

	#region -- class PpsDataTableServerDefinition ---------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsDataTableServerDefinition : PpsDataTableDefinition
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
					AddColumn(new PpsDataColumnDefinitionServer(this, c));
				//else if (c.Name == xnRelation)
				//	AddColumn(new PpsDataSqlRelationColumnServer(this, c));
				else if (c.Name == xnMeta)
					metaInfo.Add(c);
				//else
				//	throw new InvalidCo
			}
		} // ctor

		//public void PrepareInit(SqlCommand cmd, StringBuilder sbCommand)
		//{
		//	sqlSchemas = new List<SqlSchema>();

		//	// Suche das Sql-Shema zusammen
		//	for (int i = 0; i < Columns.Count; i++)
		//	{
		//		var sqlColumn = Columns[i] as PPSnDataSqlColumnServer;
		//		if (sqlColumn != null)
		//		{
		//			// Suche das Schema, oder lege es an
		//			SqlSchema cur = sqlSchemas.Find(c => String.Compare(c.sTableName, sqlColumn.SqlTableName, true) == 0);
		//			if (cur == null)
		//				sqlSchemas.Add(cur = new SqlSchema() { sTableName = sqlColumn.SqlTableName });

		//			// Spalte zuordnen
		//			cur.columns.Add(sqlColumn);

		//			// Primarykey
		//			if (sqlColumn.IsPrimary)
		//				cur.primaryKeys.Add(sqlColumn);
		//		}
		//	}

		//	// Erzeuge den Befehl (top 0)
		//	foreach (SqlSchema c in sqlSchemas)
		//	{
		//		sbCommand.Append("select top 0 ");
		//		c.AppendColumnList(sbCommand);
		//		sbCommand.Append(" from ").Append(c.sTableName);
		//		sbCommand.AppendLine(";");
		//	}
		//} // proc PrepareInit

		//public void FinishInit(SqlDataReader r)
		//{
		//	// Hole die Daten aus dem Resultset
		//	foreach (SqlSchema c in sqlSchemas)
		//	{
		//		if (r.FieldCount != c.columns.Count)
		//			throw new ArgumentException(String.Format("field count missmatch bei {0}.", c.sTableName));

		//		DataTable dtSchema = r.GetSchemaTable();
		//		for (int i = 0; i < r.FieldCount; i++)
		//			c.columns[i].InitSql(r, i, dtSchema);

		//		r.NextResult();
		//	}

		//	EndInit();
		//} // proc FinishInit

		protected override void EndInit()
		{
			//PpsDataSource commonSource = null;

			// fetch column information
			foreach (PpsDataColumnDefinitionServer c in Columns)
			{
				c.EndInit();

				//if (!c.IsInitialized)
				//	throw new ArgumentException("column not initialized."); // todo:

				//var tmp = c.FieldDescription.DataSource;
				//if (tmp != null)
				//{
				//	if (commonSource == null)
				//		commonSource = tmp;
				//	else if (commonSource != tmp)
				//		throw new ArgumentException("mixing is not allowed."); // todo:
				//}
			}

			// generate the load connector
			


			base.EndInit();
		} // proc EndInit

		public override PpsDataTable CreateDataTable(PpsDataSet dataset)
			=> new PpsDataTableServer(this, dataset);

		public void WriteSchema(XElement xSchema)
		{
			var xTable = new XElement("table");
			xTable.SetAttributeValue("name", Name);

			// write meta data
			PpsDataSetServerDefinition.WriteSchemaMetaInfo(xTable, metaInfo);

			// write the columns
			foreach (PpsDataColumnDefinitionServer column in Columns)
				column.WriteSchema(xTable);

			xSchema.Add(xTable);
		} // proc WriteSchema

		//public void PrepareLoad(SqlCommand cmd, StringBuilder sbCommand, LuaTable args)
		//{
		//	foreach (SqlSchema c in sqlSchemas)
		//	{
		//		sbCommand.Append("select ");
		//		c.AppendColumnList(sbCommand);
		//		sbCommand.Append(" from ").Append(c.sTableName);
		//		sbCommand.Append(" where ");
		//		bool lConditionAdded = false;

		//		foreach (PPSnDataSqlColumnServer col in c.columns)
		//		{
		//			object parameterValue = args.GetMemberValue(col.Name);
		//			if (parameterValue != null)
		//			{
		//				string sParameterName = "@" + col.Name;
		//				if (lConditionAdded)
		//					sbCommand.Append(" and ");

		//				sbCommand.Append(col.SqlColumnName);
		//				sbCommand.Append("=");
		//				sbCommand.Append(sParameterName);

		//				if (cmd.Parameters.IndexOf(sParameterName) == -1)
		//					cmd.Parameters.Add(sParameterName, col.SqlType, col.SqlColumnSize).Value = parameterValue ?? DBNull.Value;
		//			}
		//			else
		//			{
		//				PPSnDataSqlRelationColumnServer relColumn = col as PPSnDataSqlRelationColumnServer;
		//				if (relColumn != null)
		//				{
		//					// todo: besser!
		//					string sParameterName = "@" + relColumn.ParentColumn;
		//					if (lConditionAdded)
		//						sbCommand.Append(" and ");

		//					sbCommand.Append(relColumn.SqlColumnName);
		//					sbCommand.Append("=");
		//					sbCommand.Append(sParameterName);

		//					if (cmd.Parameters.IndexOf(sParameterName) == -1)
		//						cmd.Parameters.Add(sParameterName, col.SqlType, col.SqlColumnSize).Value = parameterValue ?? DBNull.Value;
		//				}
		//			}
		//		}

		//		if (lConditionAdded)
		//		{
		//			// todo: zur Sicherheit ne prüfung needs primary key oder so
		//			sbCommand.Append("1=1");
		//		}

		//		sbCommand.AppendLine(";");
		//	}
		//} // proc PrepareLoad

		//public void FinishLoad(SqlDataReader r, PPSnDataTable table)
		//{
		//	// Hole die Daten aus dem Resultset
		//	foreach (SqlSchema c in sqlSchemas)
		//	{
		//		if (r.FieldCount != c.columns.Count)
		//			throw new ArgumentException(String.Format("field count missmatch bei {0}.", c.sTableName));

		//		if (r.HasRows)
		//		{
		//			object[] values = new object[r.FieldCount];
		//			while (r.Read())
		//			{
		//				r.GetValues(values);
		//				for (int i = 0; i < values.Length; i++)
		//					if (values[i] == DBNull.Value)
		//						values[i] = null;
		//				table.Add(values);
		//			}
		//		}
		//		r.NextResult();
		//	}
		//} // proc FinishLoad

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

		public PpsDataSetServerDefinition(IServiceProvider sp, string name, XElement config)
		{
			this.application = sp.GetService<PpsApplication>(true);
			this.name = name;

			// get inherited list
			this.inheritedFrom = config.GetStrings("inherited", true);

			// get script list
			this.scripts = config.GetStrings("scripts", true);
			
			// parse data table schema
			foreach (var c in config.Elements())
			{
				if (c.Name == xnTable)
				{
					var tableName = c.GetAttribute("name", String.Empty);
					if (String.IsNullOrEmpty(tableName))
						throw new DEConfigurationException(c, "table needs a name.");

					// create a table
					var table = FindTable(tableName);
					if (table != null)
						throw new DEConfigurationException(c, $"table is not unique ('{tableName}')");

					Add(new PpsDataTableServerDefinition(this, tableName, c));
				}
				else if (c.Name == xnMeta)
				{
					metaInfo.Add(c);
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
					if (GetType().IsAssignableFrom(datasetDefinition.GetType()))
						throw new ArgumentException("Incompatible datasources"); // todo:

					// combine scripts
					if (datasetDefinition.scripts != null && datasetDefinition.scripts.Length > 0)
						scriptList.AddRange(datasetDefinition.scripts);

					// combine meta information
					metaInfo.Merge(datasetDefinition.Meta);

					// combine dataset
					foreach (var t in datasetDefinition.TableDefinitions)
						Add(t);
				}
			}

			// add own scripts
			if (scripts != null && scripts.Length > 0)
				scriptList.AddRange(scripts);
			scripts = scriptList.ToArray();

			 base.EndInit();
		} // proc EndInit

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
		public PpsApplication Application => application;

		// -- Static --------------------------------------------------------------

		//public static void LoadMetaFromConfiguration(XElement xMetaContainer, IReadOnlyDictionary<string, Type> wellknownTypes, Action<string, Func<Type>, object> add)
		//{
		//	foreach (XElement m in xMetaContainer.Elements(xnMeta))
		//		AddMetaFromElement(m, wellknownTypes, add);
		//} // LoadMetaFromConfiguration

		public static void AddMetaFromElement(XElement xMeta, IReadOnlyDictionary<string, Type> wellknownTypes, Action<string, Func<Type>, object> add)
		{
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

	#region -- class PpsDataSetServer ---------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Serverseitige Implementierung des DataSets</summary>
	public sealed class PpsDataSetServer : PpsDataSet
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
