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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Networking;
using TecWare.DE.Server;
using TecWare.DE.Server.Http;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;
using static TecWare.PPSn.Server.PpsStuff;

namespace TecWare.PPSn.Server.Data
{
	#region -- enum PpsDataColumnParentRelationType -----------------------------------

	/// <summary>Server site relation types.</summary>
	public enum PpsDataColumnParentRelationType
	{
		/// <summary>Not defined.</summary>
		None,
		/// <summary>Direct direction to root.</summary>
		Root,
		/// <summary>Delete and update cascade.</summary>
		Cascade,
		/// <summary>Delete and update restrict.</summary>
		Restricted,
		/// <summary>Set null</summary>
		SetNull
	} // enum PpsDataColumnParentRelationType

	#endregion

	#region -- class PpsDataValueColumnServerDefinition -------------------------------

	/// <summary>Column description of the dataset for the server implementation.</summary>
	public sealed class PpsDataColumnServerDefinition : PpsDataColumnDefinition, IPpsColumnDescription
	{
		#region -- class PpsDataColumnMetaCollectionServer ----------------------------

		private sealed class PpsDataColumnMetaCollectionServer : PpsDataColumnMetaCollection
		{
			public PpsDataColumnMetaCollectionServer(PpsDataColumnDefinition column, XElement xColumnDefinition)
				: base(column)
			{
				foreach (var x in xColumnDefinition.Elements(xnMeta))
					PpsDataSetServerDefinition.AddMetaFromElement(x, WellknownMetaTypes, Add);
			} // ctor

			public PpsDataColumnMetaCollectionServer(PpsDataColumnDefinition column, PpsDataColumnMetaCollectionServer clone)
				: base(column, clone)
			{
			} // ctor

			public void Update(string key, Type type, object value)
				=> Add(key, () => type, value);
		} // class PpsDataColumnMetaCollectionServer

		#endregion

		private readonly string fieldName;
		private readonly PpsDataColumnMetaCollectionServer metaInfo;

		private PpsFieldDescription fieldDescription = null;

		private readonly PpsDataColumnParentRelationType parentType = PpsDataColumnParentRelationType.None;
		private readonly string parentRelationName;
		private readonly string parentTableName;
		private readonly string parentColumnName;

		private PpsDataColumnServerDefinition(PpsDataTableDefinition tableDefinition, PpsDataColumnServerDefinition clone)
			: base(tableDefinition, clone)
		{
			this.fieldName = clone.fieldName;
			this.metaInfo = new PpsDataColumnMetaCollectionServer(this, clone.metaInfo);
			this.fieldDescription = clone.fieldDescription;

			this.parentType = clone.parentType;
			this.parentRelationName = clone.parentRelationName;
			this.parentTableName = clone.parentTableName;
			this.parentColumnName = clone.parentColumnName;
		} // ctor

		private PpsDataColumnServerDefinition(PpsDataTableDefinition tableDefinition, string fieldName, string columnName, bool isPrimaryKey, bool isIdentity, bool createRelationColumn, XElement config)
			: base(tableDefinition, columnName, isPrimaryKey, isIdentity)
		{
			this.fieldName = fieldName;

			// relation
			if (createRelationColumn)
			{
				this.parentRelationName = config.GetAttribute("relationName", (string)null);
				this.parentType = config.GetAttribute("parentType", PpsDataColumnParentRelationType.None);
				this.parentTableName = config.GetAttribute("parentTable", (string)null);
				this.parentColumnName = config.GetAttribute("parentColumn", (string)null);
			}
			else
			{
				this.parentRelationName = null;
				this.parentType = PpsDataColumnParentRelationType.None;
				this.parentTableName = null;
				this.parentColumnName = null;
			}

			this.metaInfo = new PpsDataColumnMetaCollectionServer(this, config);
		} // ctor

		/// <summary></summary>
		/// <param name="tableDefinition"></param>
		/// <param name="createRelationColumn"></param>
		/// <param name="config"></param>
		/// <returns></returns>
		public static PpsDataColumnServerDefinition Create(PpsDataTableDefinition tableDefinition, bool createRelationColumn, XElement config)
		{
			var columnName = config.GetAttribute("name", (string)null);
			var isPrimary = config.GetAttribute("isPrimary", false);
			var isIdentity = config.GetAttribute("isIdentity", isPrimary);

			if (String.IsNullOrEmpty(columnName))
				throw new DEConfigurationException(config, $"@name is empty.");

			var fieldName = config.GetAttribute("fieldName", (string)null);
			if (String.IsNullOrEmpty(fieldName))
				throw new DEConfigurationException(config, "@fieldName is empty.");

			return new PpsDataColumnServerDefinition(tableDefinition, fieldName, columnName, isPrimary, isIdentity, createRelationColumn, config);
		} // func Create

		/// <summary></summary>
		/// <param name="tableOwner"></param>
		/// <returns></returns>
		public override PpsDataColumnDefinition Clone(PpsDataTableDefinition tableOwner)
			=> new PpsDataColumnServerDefinition(tableOwner, this);

		/// <summary></summary>
		public override void EndInit()
		{
			// update the relation
			if (parentRelationName != null)
			{
				var parentTable = Table.DataSet.FindTable(parentTableName);
				if (parentTable == null)
					throw new ArgumentException($"Table {parentTableName} for relation {parentRelationName} not found.");
				parentTable.AddRelation(parentRelationName, GetClientRelationFromServerRelation(parentType), parentTable.Columns[parentColumnName, true], this);
			}

			// resolve the correct field
			var application = ((PpsDataSetServerDefinition)Table.DataSet).Application;
			fieldDescription = application.GetFieldDescription(fieldName, true);

			// update the meta information
			foreach (var c in fieldDescription.Attributes)
			{
				if (!metaInfo.ContainsKey(c.Name))
					metaInfo.Update(c.Name, c.Type, c.Value);
			}

			base.EndInit();
		} // proc EndInit

		private static PpsRelationType GetClientRelationFromServerRelation(PpsDataColumnParentRelationType parentType)
		{
			switch (parentType)
			{
				case PpsDataColumnParentRelationType.None:
					return PpsRelationType.None;

				case PpsDataColumnParentRelationType.Restricted:
					return PpsRelationType.Restricted;
				case PpsDataColumnParentRelationType.SetNull:
					return PpsRelationType.SetNull;

				default:
					return PpsRelationType.Cascade;
			}
		} // func GetClientRelationFromServerRelation

		/// <summary></summary>
		/// <param name="xTable"></param>
		public void WriteSchema(XElement xTable)
		{
			var clientDataType = Meta.GetProperty<string>("clientDataType", null);

			var xColumn = new XElement("column",
				new XAttribute("name", Name),
				new XAttribute("dataType", clientDataType ?? LuaType.GetType(DataType).AliasOrFullName)
			);

			if (IsPrimaryKey)
				xColumn.Add(new XAttribute("isPrimary", IsPrimaryKey));
			if (IsIdentity)
				xColumn.Add(new XAttribute("isIdentity", IsIdentity));

			if (IsRelationColumn)
			{
				xColumn.Add(new XAttribute("parentRelationName", parentRelationName));
				xColumn.Add(new XAttribute("parentRelationType", GetClientRelationFromServerRelation(parentType)));
				xColumn.Add(new XAttribute("parentTable", ParentColumn.Table.Name));
				xColumn.Add(new XAttribute("parentColumn", ParentColumn.Name));
			}
			xTable.Add(xColumn);

			// meta data
			PpsDataSetServerDefinition.WriteSchemaMetaInfo(xColumn, metaInfo);
		} // proc WriteSchema

		/// <summary></summary>
		/// <typeparam name="T"></typeparam>
		/// <returns></returns>
		public T GetColumnDescription<T>() where T : IPpsColumnDescription
			=> this.GetColumnDescriptionParentImplementation<T>(fieldDescription);

		/// <summary></summary>
		/// <returns></returns>
		protected override Type GetDataType()
			=> fieldDescription?.DataType ?? typeof(object);

		/// <summary></summary>
		public override PpsDataColumnMetaCollection Meta => metaInfo;

		/// <summary>Field, that descripse format and behaviour of the column.</summary>
		public PpsFieldDescription FieldDescription => fieldDescription;

		/// <summary>Relation to the parent column</summary>
		public PpsDataColumnParentRelationType ParentType => parentType;

		/// <summary>Is the column initialized.</summary>
		public override bool IsInitialized => fieldDescription != null;
	} // class PpsDataColumnServerDefinition

	#endregion

	#region -- class PpsDataTableServerDefinition -------------------------------------

	/// <summary></summary>
	public class PpsDataTableServerDefinition : PpsDataTableDefinition
	{
		private static readonly Dictionary<string, Type> wellknownMetaTypes = new Dictionary<string, Type>();

		#region -- class PpsDataTableMetaCollectionServer -----------------------------

		private sealed class PpsDataTableMetaCollectionServer : PpsDataTableMetaCollection
		{
			public PpsDataTableMetaCollectionServer()
			{
			} // ctor

			public PpsDataTableMetaCollectionServer(PpsDataTableMetaCollectionServer clone)
				: base(clone)
			{
			} // ctor

			public void Add(XElement xMeta)
				=> PpsDataSetServerDefinition.AddMetaFromElement(xMeta, WellknownMetaTypes, Add);
		} // class PPSnDataTableMetaCollectionServer

		#endregion

		private readonly PpsDataTableMetaCollectionServer metaInfo = new PpsDataTableMetaCollectionServer();

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary></summary>
		/// <param name="dataset"></param>
		/// <param name="clone"></param>
		protected PpsDataTableServerDefinition(PpsDataSetServerDefinition dataset, PpsDataTableServerDefinition clone)
			: base(dataset, clone)
		{
			this.metaInfo = new PpsDataTableMetaCollectionServer(clone.metaInfo);
		} // ctor

		/// <summary></summary>
		/// <param name="dataset"></param>
		/// <param name="tableName"></param>
		/// <param name="xTable"></param>
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

		/// <summary></summary>
		/// <param name="dataset"></param>
		/// <returns></returns>
		public override PpsDataTableDefinition Clone(PpsDataSetDefinition dataset)
			=> new PpsDataTableServerDefinition((PpsDataSetServerDefinition)dataset, this);

		/// <summary></summary>
		protected override void EndInit()
		{
			// fetch column information
			foreach (PpsDataColumnServerDefinition c in Columns)
				c.EndInit();

			base.EndInit();
		} // proc EndInit

		/// <summary></summary>
		/// <param name="t"></param>
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

		#endregion
		
		/// <summary></summary>
		/// <param name="xSchema"></param>
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

		/// <summary></summary>
		public override PpsDataTableMetaCollection Meta => metaInfo;
	} // class PpsDataTableServerDefinition

	#endregion
	
	#region -- class PpsDataSetServerDefinition ---------------------------------------

	/// <summary>Dataset implementation for the server.</summary>
	public sealed class PpsDataSetServerDefinition : PpsDataSetDefinition, IServiceProvider
	{
		#region -- class PpsDataSetMetaCollectionServerDefinition ---------------------

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

		private readonly PpsDataSource dataSource; // the data source the server definition is bound to
		private readonly PpsApplication application;
		private readonly Lua lua;
		private readonly string name;
		private readonly DateTime configurationStamp;
		private readonly string[] inheritedFrom;

		private string[] clientScripts;
		private string[] serverScripts;

		private PpsDataSetMetaCollectionServerDefinition metaInfo = new PpsDataSetMetaCollectionServerDefinition();

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary></summary>
		/// <param name="dataSource"></param>
		/// <param name="name"></param>
		/// <param name="config"></param>
		/// <param name="configurationStamp"></param>
		public PpsDataSetServerDefinition(PpsDataSource dataSource, string name, XElement config, DateTime configurationStamp)
		{
			this.dataSource = dataSource;
			this.application = dataSource.GetService<PpsApplication>(true);
			this.lua = dataSource.GetService<IDELuaEngine>(true).Lua; 
			this.name = name;
			this.configurationStamp = configurationStamp;

			// get inherited list
			this.inheritedFrom = config.GetStrings("inherited", true);

			// get script list
			this.serverScripts = config.GetStrings("serverScripts", true);
			this.clientScripts = config.GetPaths("clientScripts", true);

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
				else if (cur.Name == xnAutoTag)
				{
					Add(new PpsDataSetAutoTagDefinition(this,
						cur.GetAttribute("name", String.Empty),
						cur.GetAttribute("tableName", String.Empty),
						cur.GetAttribute("columnName", String.Empty),
						cur.GetAttribute<PpsDataSetAutoTagMode>("mode", PpsDataSetAutoTagMode.First))
					);
				}
			} // foreach c
		} // ctor

		private PpsDataTableServerDefinition CreateTableDefinition(string tableName, XElement config)
		{
			var alternativeDataSource = config.GetAttribute("dataSource", String.Empty);
			if (String.IsNullOrEmpty(alternativeDataSource))
				return dataSource.CreateTableDefinition(this, tableName, config);
			else
			{
				return application.GetDataSource(alternativeDataSource, true)
				   .CreateTableDefinition(this, tableName, config);
			}
		} // func CreateTableDefinition

		/// <summary></summary>
		/// <returns></returns>
		public Task InitializeAsync()
			=> Task.Run(new Action(EndInit));

		/// <summary></summary>
		public override void EndInit()
		{
			// embed inherited
			var collectedServerScripts = new List<string>();
			var collectedClientScripts = new List<string>();

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

					// combine scripts, server
					if (datasetDefinition.serverScripts != null && datasetDefinition.serverScripts.Length > 0)
						collectedServerScripts.AddRange(datasetDefinition.serverScripts);
					// combine scripts, server
					if (datasetDefinition.clientScripts != null && datasetDefinition.clientScripts.Length > 0)
						collectedClientScripts.AddRange(datasetDefinition.clientScripts);
					
					// combine meta information
					metaInfo.Merge(datasetDefinition.Meta);

					// combine tags
					foreach (var cur in datasetDefinition.TagDefinitions)
						if (FindTag(cur.Name) != null)
							Add(cur);

					// combine dataset
					foreach (var t in datasetDefinition.TableDefinitions)
					{
						var mergeTable = FindTable(t.Name);
						if (mergeTable == null)
							Add(t.Clone(this));
						else
							((PpsDataTableServerDefinition)mergeTable).Merge(t);
					}
				}
			}

			// add own scripts
			if (serverScripts != null && serverScripts.Length > 0)
				collectedServerScripts.AddRange(serverScripts);
			serverScripts = collectedServerScripts.ToArray();

			// add own scripts
			if (clientScripts != null && clientScripts.Length > 0)
				collectedClientScripts.AddRange(clientScripts);
			clientScripts = collectedClientScripts.ToArray();

			// resolve tables
			 base.EndInit();
		} // proc EndInit

		#endregion

		/// <summary></summary>
		/// <param name="serviceType"></param>
		/// <returns></returns>
		public object GetService(Type serviceType)
			=> application.GetService(serviceType);

		/// <summary></summary>
		/// <returns></returns>
		public override PpsDataSet CreateDataSet()
			=> new PpsDataSetServer(this);
		
		private static XElement CreateTagSchema(PpsDataSetAutoTagDefinition def)
		{
			return new XElement("tag",
				new XAttribute("name", def.Name),
				new XAttribute("tableName", def.TableName),
				new XAttribute("columnName", def.ColumnName),
				new XAttribute("mode", def.Mode)
			);
		} // func CreateTagSchema

		/// <summary></summary>
		/// <param name="xSchema"></param>
		public void WriteSchema(XElement xSchema)
		{
			// write the meta data for the dataset
			WriteSchemaMetaInfo(xSchema, metaInfo);

			// script list
			xSchema.Add(
				from s in clientScripts
				select new XElement("script",
					new XAttribute("uri", Path.GetFileName(s))
				)
			);

			// write tagging
			xSchema.Add(
				from t in TagDefinitions
				select CreateTagSchema(t)
			);

			// write the tables
			foreach (PpsDataTableServerDefinition t in TableDefinitions)
				t.WriteSchema(xSchema);
		} // func WriteSchema

		/// <summary></summary>
		/// <param name="r"></param>
		/// <param name="cacheId"></param>
		public void WriteToDEContext(IDEWebRequestScope r, string cacheId)
		{
			r.SetLastModified(ConfigurationStamp);

			r.WriteContent(() =>
				{
					var xSchema = new XElement("schema");
					WriteSchema(xSchema);

					var dst = new MemoryStream();
					var xmlSettings = Procs.XmlWriterSettings;
					xmlSettings.CloseOutput = false;
					xmlSettings.Encoding = Encoding.UTF8;
					using (var xml = XmlWriter.Create(dst, xmlSettings))
						xSchema.WriteTo(xml);

					dst.Position = 0;
					return dst;
				}, cacheId, MimeTypes.Text.Xml + ";charset=utf-8"
			);
		} // proc WriteToDEContext
		
		/// <summary>Name of the dataset</summary>
		public string Name => name;
		/// <summary></summary>
		public override PpsDataSetMetaCollection Meta => metaInfo;
		/// <summary>Main datasource of the dataset.</summary>
		public PpsDataSource DataSource => dataSource;
		/// <summary></summary>
		public PpsApplication Application => application;
		/// <summary></summary>
		public override Lua Lua => lua;

		/// <summary>Key type to use for auto increment columns.</summary>
		public override PpsTablePrimaryKeyType KeyType => PpsTablePrimaryKeyType.Server;

		/// <summary></summary>
		public DateTime ConfigurationStamp => configurationStamp;
		/// <summary></summary>
		public string[] ClientScripts => clientScripts;
		/// <summary></summary>
		public string[] ServerScripts => serverScripts;

		// -- Static ----------------------------------------------------------

		/// <summary></summary>
		/// <param name="xMeta"></param>
		/// <param name="wellknownTypes"></param>
		/// <param name="add"></param>
		public static void AddMetaFromElement(XElement xMeta, IReadOnlyDictionary<string, Type> wellknownTypes, Action<string, Func<Type>, object> add)
		{
			if (xMeta == null)
				return;
			
			var name = xMeta.GetAttribute("name", String.Empty);
			if (String.IsNullOrEmpty(name))
				throw new DEConfigurationException(xMeta, "@name is empty.");

			try
			{
				add(name, () => LuaType.GetType(xMeta.GetAttribute("dataType", "object")), xMeta.Value);
			}
			catch (ArgumentNullException e)
			{
				throw new DEConfigurationException(xMeta, $"Datatype '{xMeta.GetAttribute("dataType", "object")}' unknown.", e);
			}
		} // proc AddMetaFromElement

		/// <summary></summary>
		/// <param name="xParent"></param>
		/// <param name="metaInfo"></param>
		public static void WriteSchemaMetaInfo(XElement xParent, PpsMetaCollection metaInfo)
		{
			if (metaInfo.Count > 0)
			{
				var xMeta = new XElement("meta");
				xParent.Add(xMeta);
				foreach (var m in metaInfo)
				{
					if (m.Name == "dataType" || m.Value == null)
						continue;

					xMeta.Add(new XElement(m.Name,
						new XAttribute("dataType", LuaType.GetType(m.Value.GetType()).AliasOrFullName),
						m.Value.ChangeType<string>()
					));
				}
			}
		} // proc WriteSchemaMetaInfo
	} // class PpsDataSetServerDefinition

	#endregion

	#region -- class PpsDataSetServer -------------------------------------------------

	/// <summary>Server site implementation of the dataset.</summary>
	public class PpsDataSetServer : PpsDataSet
	{
		/// <summary></summary>
		/// <param name="datasetClass"></param>
		public PpsDataSetServer(PpsDataSetServerDefinition datasetClass)
			: base(datasetClass)
		{
		} // ctor

		/// <summary></summary>
		public new PpsDataSetServerDefinition DataSetDefinition => (PpsDataSetServerDefinition)base.DataSetDefinition;
	} // class PpsDataSetServer

	#endregion
}
