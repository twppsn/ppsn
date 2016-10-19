using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Data;
using TecWare.DE.Networking;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;

namespace TecWare.PPSn
{
	public partial class PpsEnvironment
	{
		private const string TemporaryTablePrefix = "old_";

		#region -- class PpsWebRequestCreate ----------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private class PpsWebRequestCreate : IWebRequestCreate
		{
			private readonly WeakReference<PpsEnvironment> environmentReference;

			public PpsWebRequestCreate(PpsEnvironment environment)
			{
				this.environmentReference = new WeakReference<PpsEnvironment>(environment);
			} // ctor

			public WebRequest Create(Uri uri)
			{
				PpsEnvironment environment;
				if (environmentReference.TryGetTarget(out environment))
					return environment.CreateProxyRequest(uri);
				else
					throw new ObjectDisposedException("Environment does not exists anymore.");
			}
		} // class PpsWebRequestCreate

		#endregion

		#region -- class KnownDataSetDefinition -------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class KnownDataSetDefinition
		{
			private readonly PpsEnvironment environment;

			private readonly Type datasetDefinitionType;
			private readonly string schema;
			private readonly string sourceUri;

			private PpsDataSetDefinitionDesktop definition = null;

			public KnownDataSetDefinition(PpsEnvironment environment, Type datasetDefinitionType, string schema, string sourceUri)
			{
				this.environment = environment;
				this.datasetDefinitionType = datasetDefinitionType;
				this.schema = schema;
				this.sourceUri = sourceUri;
			} // ctor

			public async Task<PpsDataSetDefinitionDesktop> GetDocumentDefinitionAsync()
			{
				if (definition != null)
					return definition;

				// load the schema
				var xSchema = await environment.Request.GetXmlAsync(sourceUri);
				definition = (PpsDataSetDefinitionDesktop)Activator.CreateInstance(datasetDefinitionType, environment, schema, xSchema);
				definition.EndInit();

				return definition;
			} // func GetDocumentDefinitionAsync

			public Type DataSetDefinitionType => datasetDefinitionType;
			public string Schema => schema;
			public string SourceUri => sourceUri;
		} // class KnownDataSetDefinition

		#endregion

		#region -- class PpsActiveDataSetsImplementation ----------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private class PpsActiveDataSetsImplementation : Dictionary<PpsDataSetId, PpsDataSetDesktop>, IPpsActiveDataSets
		{
			private readonly PpsEnvironment environment;
			private readonly Dictionary<string, KnownDataSetDefinition> datasetDefinitions = new Dictionary<string, KnownDataSetDefinition>(StringComparer.OrdinalIgnoreCase);

			public PpsActiveDataSetsImplementation(PpsEnvironment environment)
			{
				this.environment = environment;
			} // ctor

			public bool RegisterDataSetSchema(string schema, string uri, Type datasetDefinitionType)
			{
				lock (datasetDefinitions)
				{
					KnownDataSetDefinition definition;
					if (!datasetDefinitions.TryGetValue(schema, out definition) || // definition is not registered
						definition.Schema != schema || definition.SourceUri != uri) // or registration has changed
					{
						datasetDefinitions[schema] = new KnownDataSetDefinition(environment, datasetDefinitionType ?? typeof(PpsDataSetDefinitionDesktop), schema, uri);
						return true;
					}
					else
						return false;
				}
			} // proc IPpsActiveDataSets.RegisterDataSetSchema

			public void UnregisterDataSetSchema(string schema)
			{
				lock (datasetDefinitions)
					datasetDefinitions.Remove(schema);
			} // proc UnregisterDataSetSchema

			public string GetDataSetSchemaUri(string schema)
			{
				lock (datasetDefinitions)
				{
					KnownDataSetDefinition definition;
					return datasetDefinitions.TryGetValue(schema, out definition) ? definition.Schema : null;
				}
			} // func GetDataSetSchemaUri

			public Task<PpsDataSetDefinitionDesktop> GetDataSetDefinition(string schema)
			{
				KnownDataSetDefinition definition;
				lock (datasetDefinitions)
				{
					if (!datasetDefinitions.TryGetValue(schema, out definition))
						return Task.FromResult<PpsDataSetDefinitionDesktop>(null);
				}
				return definition.GetDocumentDefinitionAsync();
			} // func GetDataSetDefinition

			public Guid GetGuidFromData(XElement xData, XName rootTable)
			{
				// tag of the root table
				var xn = XNamespace.Get("table");
				if (rootTable == null)
					rootTable = xn + "Head";
				else if (rootTable.Namespace != xn)
					rootTable = xn + rootTable.LocalName;

				var x = xData.Element(rootTable);
				if (x == null)
					throw new ArgumentException($"Root table not found (tag: {rootTable}).");

				var xRow = x.Element("r");
				if (xRow == null)
					throw new ArgumentException($"Root table has no row (tag: {rootTable}).");

				var xGuid = xRow.Element("Guid");
				if (xGuid == null)
					throw new ArgumentException($"Root table has no guid-column (tag: {rootTable}).");

				var guidString = xGuid.Element("o")?.Value;
				if (String.IsNullOrEmpty(guidString))
					throw new ArgumentException($"Guid-column is empty (tag: {rootTable}).");

				return Guid.Parse(guidString);
			} // func GetGuidFromData

			public async Task<PpsDataSetDesktop> CreateEmptyDataSetAsync(string schema, PpsDataSetId id)
			{
				if (id == PpsDataSetId.Empty)
					throw new ArgumentNullException("id", "Empty id is not allowed.");

				var dataset = Find(id);
				if (dataset != null)
					throw new ArgumentOutOfRangeException("id", $"DataSet already opened: {id}");

				// try to find known schema
				var definition = await GetDataSetDefinition(schema);
				if (definition == null)
					throw new ArgumentOutOfRangeException("schema", $"'{schema}' is not registered.");

				// create a empty dataset
				return definition.CreateDataSet(id);
			} // func IPpsActiveDataSets.CreateEmptyOrOpenDataSetAsync

			IEnumerator<PpsDataSetDesktop> IEnumerable<PpsDataSetDesktop>.GetEnumerator()
				=> Values.GetEnumerator();

			IEnumerator IEnumerable.GetEnumerator()
				=> Values.GetEnumerator();

			public PpsDataSetDesktop Find(PpsDataSetId id)
			{
				PpsDataSetDesktop dataset;
				return TryGetValue(id, out dataset) ? dataset : null;
			} // func Find

			public IEnumerable<string> KnownSchemas
			{
				get
				{
					lock (datasetDefinitions)
						return datasetDefinitions.Keys.ToArray();
				}
			} // prop KnownSchemas

			PpsDataSetDesktop IPpsActiveDataSets.this[PpsDataSetId id] => Find(id);
		} // class PpsActiveDataSetsImplementation

		#endregion

		/// <summary></summary>
		public event EventHandler IsOnlineChanged;

		private readonly SQLiteConnection localConnection;   // local datastore
		private readonly Uri baseUri;                   // internal uri for this datastore
		private bool isOnline = false;                  // is there an online connection

		private readonly BaseWebRequest request;
		private readonly PpsActiveDataSetsImplementation activeDataSets;

		#region -- Init -------------------------------------------------------------------

		private SQLiteConnection InitLocalStore()
		{
			SQLiteConnection newLocalStore = null;
			// open local data store
			try
			{
				// open the local database
				var dataPath = Path.Combine(info.LocalPath.FullName, "localStore.db");
				newLocalStore = new SQLiteConnection($"Data Source={dataPath};DateTimeKind=Utc;foreign keys=true");
				newLocalStore.Open();
				VerifyLocalStore(newLocalStore);
			}
			catch
			{
				newLocalStore?.Dispose();
				throw;
			}
			return newLocalStore;
		} // proc InitLocalStore

		private Uri InitProxy()
		{
			// register proxy for the web requests
			var baseUri = new Uri($"http://ppsn{environmentId}.local");
			WebRequest.RegisterPrefix(baseUri.ToString(), new PpsWebRequestCreate(this));
			return baseUri;
		} // func InitProxy

		#endregion

		private async Task RefreshOfflineCacheAsync()
		{
			if (IsOnline && IsAuthentificated)
				await Task.Run(new Action(UpdateOfflineItems));
		} // RefreshOfflineCacheAsync

		#region -- VerifyLocalStore -------------------------------------------------------

		#region -- enum ParsingState ------------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private enum ParsingState
		{
			Default = 0,
			ReadElementDescription = 1,
			ReadElementContent = 2,
		} // enum ParsingState

		#endregion

		#region -- ParseSQLiteCreateTableCommands -----------------------------------------

		private XElement ParseSQLiteCreateTableCommands(Type type, string resourceName)
		{
			var command = new XElement("command",
				new XAttribute("name", type.AssemblyQualifiedName + ", " + resourceName)
			);

			using (var source = type.Assembly.GetManifestResourceStream(type, resourceName))
			using (var reader = new StreamReader(source, Encoding.UTF8))
			{
				var state = ParsingState.Default;
				var lineData = new StringBuilder();
				XElement currentNode = null;

				while (true)
				{
					var line = reader.ReadLine();

					switch (state)
					{
						#region -- ParsingState.Default --
						case ParsingState.Default:
							if (line == null)
								return command;
							else if (line.StartsWith("--<", StringComparison.Ordinal))
							{
								lineData.Append(line.Substring(2));
								state = ParsingState.ReadElementDescription;
							}
							break;
						#endregion
						#region -- ParsingState.ReadElementDescription --
						case ParsingState.ReadElementDescription:
							if (line != null && line.StartsWith("--", StringComparison.Ordinal))
								lineData.Append(line.Substring(2));
							else
							{
								currentNode = XElement.Parse(lineData.ToString());
								command.Add(currentNode);
								lineData = new StringBuilder();
								state = ParsingState.ReadElementContent;
								goto case ParsingState.ReadElementContent;
							}
							break;
						#endregion
						#region -- ParsingState.ReadElementContent --
						case ParsingState.ReadElementContent:
							if (line == null || line.StartsWith("--<", StringComparison.Ordinal))
							{
								currentNode.Add(new XCData(lineData.ToString()));
								lineData = new StringBuilder();
								state = ParsingState.Default;
								goto case ParsingState.Default;
							}
							else if (!line.StartsWith("--", StringComparison.Ordinal))
							{
								var tmp = line.IndexOf("--", StringComparison.Ordinal);
								if (tmp != -1)
									line = line.Substring(0, tmp);
								line = line.TrimEnd();
								lineData.AppendLine(line);
							}
							break;
							#endregion
					} // switch state
				} // while true
			} // using reader source
		} // func ParseSQLiteCreateTableCommands

		#endregion

		#region -- VerifyLocalStore -------------------------------------------------------

		protected IEnumerable<Tuple<Type, string>> GetStoreTablesFromAssembly(Type type, string resourceBase)
		{
			var resourcePath = type.Namespace + "." + resourceBase + ".";

			foreach (var resourceName in type.Assembly.GetManifestResourceNames())
			{
				if (resourceName.StartsWith(resourcePath) && resourceName.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
					yield return new Tuple<Type, string>(type, resourceName.Substring(type.Namespace.Length + 1));
			}
		} // func 

		protected virtual IEnumerable<Tuple<Type, string>> GetStoreTables()
			=> GetStoreTablesFromAssembly(typeof(PpsEnvironment), "Scripts").Where(c => !c.Item2.EndsWith("Meta.sql", StringComparison.OrdinalIgnoreCase));

		private void VerifyLocalStore(SQLiteConnection localStore)
		{
			using (var transaction = localStore.BeginTransaction())
			{
				var existsMetaTable = false; // is the meta table existing

				#region -- validate meta table --

				var metaTableCommands = ParseSQLiteCreateTableCommands(typeof(PpsEnvironment), "Scripts.Meta.sql");

				var metaTableInfo = metaTableCommands.Element("info");
				var metaTableSchema = metaTableInfo.Attribute("schema").Value;
				var metaTableName = metaTableInfo.Attribute("name").Value;

				var metaTableCreate = metaTableCommands.Element("create").Value;

				if (String.Compare(metaTableSchema, "main", StringComparison.OrdinalIgnoreCase) != 0)
					throw new InvalidDataException(String.Format("The schema \"{0}\" for the table \"{1}\" is not supported. Only \"main\" schema is supported!", metaTableSchema, metaTableName));

				using (var command = new SQLiteCommand("SELECT 1 FROM [sqlite_master] WHERE [type] = 'table' AND [tbl_name] = @metaTableName;", localStore, transaction))
				{
					command.Parameters.Add("@metaTableName", DbType.AnsiString).Value = metaTableName;
					using (var reader = command.ExecuteReader())
						existsMetaTable = reader.HasRows;
				}

				if (!existsMetaTable)
				{
					using (var command = new SQLiteCommand(metaTableCreate, localStore, transaction))
						command.ExecuteNonQuery();
				}
				#endregion

				#region -- *.sql --
				foreach (var scriptSource in GetStoreTables())
				{
					var existsTable = false;

					var tableCommands = ParseSQLiteCreateTableCommands(scriptSource.Item1, scriptSource.Item2);

					var tableResourceName = tableCommands.Attribute("name").Value;
					var tableInfo = tableCommands.Element("info");
					var tableSchema = tableInfo.Attribute("schema").Value;
					var tableName = tableInfo.Attribute("name").Value;
					var tableRev = Int64.Parse(tableInfo.Attribute("rev").Value);

					var tableCreate = tableCommands.Element("create").Value;
					var tableConvert = tableCommands.Element("convert").Value;

					if (String.Compare(tableSchema, "main", StringComparison.OrdinalIgnoreCase) != 0)
						throw new InvalidDataException(String.Format("The schema \"{0}\" for the table \"{1}\" is not supported. Only \"main\" schema is supported!", metaTableSchema, metaTableName));

					try
					{
						using (var command = new SQLiteCommand("SELECT 1 FROM [sqlite_master] WHERE [type] = 'table' AND [tbl_name] = @tableName;", localStore, transaction))
						{
							command.Parameters.Add("@tableName", DbType.String).Value = tableName;
							using (var reader = command.ExecuteReader())
								existsTable = reader.HasRows;
						}

						if (!existsMetaTable)
						{
							if (existsTable)
								throw new InvalidDataException(String.Format("The table \"{0}\".\"{1}\" can not be verified, because it was created before the revision table \"{2}\".\"{3}\".", tableSchema, tableName, metaTableSchema, metaTableName));

							using (var command = new SQLiteCommand(tableCreate, localStore, transaction))
								command.ExecuteNonQuery();
						}
						else if (!existsTable)
						{
							using (var command = new SQLiteCommand(tableCreate, localStore, transaction))
								command.ExecuteNonQuery();
						}
						else
						{
							var readRev = -1L;

							using (var command = new SQLiteCommand($"SELECT [Revision] FROM [{metaTableSchema}].[{metaTableName}] WHERE [ResourceName] = @resourceName;", localStore, transaction))
							{
								command.Parameters.Add("@resourceName", DbType.String).Value = tableResourceName;
								using (var reader = command.ExecuteReader())
								{
									var enumerator = reader.GetEnumerator();
									if (!enumerator.MoveNext())
										throw new InvalidDataException(String.Format("There is no entry in the revision table \"{0}\".\"{1}\" for resource \"{2}\".", metaTableSchema, metaTableName, tableResourceName));

									readRev = reader.GetInt64(0);
								}
							} // using command

							if (readRev > tableRev)
								throw new InvalidDataException(String.Format("The table \"{0}\".\"{1}\" can not be verified, because the revision number in the revision table \"{2}\".\"{3}\" is greater than the revision number for resource \"{4}\".", tableSchema, tableName, metaTableSchema, metaTableName, tableResourceName));
							else if (readRev == tableRev)
								continue; // Matching revision. Skip revision table update.
							else if (readRev < tableRev)
							{
								using (var command = new SQLiteCommand($"DROP TABLE IF EXISTS [{tableSchema}].[{TemporaryTablePrefix}{tableName}];", localStore, transaction))
									command.ExecuteNonQuery();

								using (var command = new SQLiteCommand($"ALTER TABLE [{tableSchema}].[{tableName}] RENAME TO [{TemporaryTablePrefix}{tableName}];", localStore, transaction))
									command.ExecuteNonQuery();

								using (var command = new SQLiteCommand(tableCreate, localStore, transaction))
									command.ExecuteNonQuery();

								using (var command = new SQLiteCommand(tableConvert, localStore, transaction))
									command.ExecuteNonQuery();

								using (var command = new SQLiteCommand($"DROP TABLE IF EXISTS [{tableSchema}].[{TemporaryTablePrefix}{tableName}];", localStore, transaction))
									command.ExecuteNonQuery();
							} // if readRev < tableRev
						}

						using (var command = new SQLiteCommand(existsTable ?
							$"UPDATE [{metaTableSchema}].[{metaTableName}] SET [Revision] = @tableRev, [LastModification] = DATETIME('now') WHERE [ResourceName] = @resourceName;" :
							$"INSERT INTO [{metaTableSchema}].[{metaTableName}] ([Revision], [LastModification], [ResourceName]) values (@tableRev, DATETIME('now'), @resourceName);", localStore, transaction))
						{
							command.Parameters.Add("@tableRev", DbType.Int64).Value = tableRev;
							command.Parameters.Add("@resourceName", DbType.String).Value = tableResourceName;
							var affectedRows = command.ExecuteNonQuery();

							string errorText = null;
							if (affectedRows < 0)
								errorText = String.Format("unknown ({0})", affectedRows);
							else if (affectedRows == 0)
								errorText = "no entry";
							else if (affectedRows > 1)
								errorText = "multiple entries";

							if (errorText != null)
								throw new Exception(String.Format("The update in the revision table \"{0}\".\"{1}\" for resource \"{2}\" failed. Reason: {3}", metaTableSchema, metaTableName, tableResourceName, errorText));
						} // using command

					}
					catch (SQLiteException e)
					{
						throw new Exception(String.Format("Verify of localStore failed for object [{0}.{1}].", tableSchema, tableName), e);
					}
				} // foreach script
				#endregion

				transaction.Commit();
			} // using transaction
		} // proc VerifyLocalStore

		#endregion

		#endregion

		#region -- Offline Data -----------------------------------------------------------

		#region -- class OfflineItemResult ------------------------------------------------

		private sealed class OfflineItemResult : Tuple<WebResponse, string, Encoding, DateTime, long>
		{
			public OfflineItemResult(WebResponse response, string contentType, Encoding encoding, DateTime lastModified, long length)
				: base(response, contentType, encoding, lastModified, length)
			{
			} // ctor

			public WebResponse Response => Item1;
			public string ContentType => Item2;
			public Encoding Encoding => Item3;
			public DateTime LastModified => Item4;
			public long ContentLength => Item5;
		} // OfflineItemResult

		#endregion

		private void UpdateOfflineItems()
		{
			using (var items = GetViewData(new PpsShellGetList("wpf.sync")).GetEnumerator())
			{
				// find the columns
				var indexPath = items.FindColumnIndex("Path", true);
				var indexLength = items.FindColumnIndex("Length");
				var indexLastWriteTime = items.FindColumnIndex("LastWriteTime");

				while (items.MoveNext())
				{
					// get the item
					var path = items.GetValue(indexPath, String.Empty);
					var length = items.GetValue(indexLength, -1L);
					var lastWriteTime = items.GetValue(indexLastWriteTime, DateTime.MinValue);

					// update the item
					UpdateOfflineItem(path, true,
						() =>
						{
							var response = Request.GetResponseAsync("/remote/" + path).Result;
							var contentType = response.GetContentType();
							return new OfflineItemResult(
								response,
								contentType.MediaType,
								contentType.CharSet == null ?  Encoding.UTF8 : Encoding.GetEncoding(contentType.CharSet),
								response.GetLastModified(),
								response.ContentLength
							);
						}, length, lastWriteTime
					);
				}
			}
		} // proc UpdateOfflineItems

		private void UpdateOfflineItem(string path, bool onlineMode, Func<OfflineItemResult> getContent, long length = -1, DateTime? lastWriteTime = null)
		{
			if (String.IsNullOrEmpty(path))
				throw new ArgumentException("Parameter \"path\" is null or empty.");

			// create a transaction for the sync
			using (var transaction = localConnection.BeginTransaction())
			{
				// find the current cached item
				long? currentRowId;
				bool updateItem;
				using (var command = new SQLiteCommand("SELECT [Id], [ContentSize], [ContentLastModification] FROM [main].[OfflineCache] WHERE [Path] = @path;", localConnection, transaction))
				{
					command.Parameters.Add("@path", DbType.String).Value = path;
					using (var reader = command.ExecuteReader(CommandBehavior.SingleRow))
					{
						if (reader.Read())
						{
							currentRowId = reader.GetInt64(0);
							var currentLength = reader.GetInt64(1);
							var currentLastWriteTime = reader.GetDateTime(2);

							if (length == -1 || (lastWriteTime ?? DateTime.MinValue) == DateTime.MinValue) // new update
								updateItem = true;
							else
								updateItem = length != currentLength && lastWriteTime != currentLastWriteTime;
						}
						else
						{
							currentRowId = null;
							updateItem = true;
						}
					}
				}

				// do we need to update the item
				if (!updateItem)
					return; // RETURN

				// get the content
				var content = getContent();
				try
				{
					if (String.IsNullOrEmpty(content.ContentType))
						throw new ArgumentException("Parameter \"contentType\" is null or empty.");

					if (content.ContentLength > 5 << 20)
						throw new InvalidOperationException("links are not implemented yet.");

					byte[] contentBytes;
					using (var src = Request.GetStreamAsync(content.Response, null))
						contentBytes = src.ReadInArray(); // simple data into an byte array

					if (content.ContentLength > 0 && content.ContentLength != contentBytes.Length)
						throw new ArgumentOutOfRangeException("content", String.Format("Expected {0:N0} bytes, but received {1:N0} bytes.", content.ContentLength, contentBytes.Length));

					// update data base
					using (var command = new SQLiteCommand(
						currentRowId == null ?
							"INSERT INTO [main].[OfflineCache] ([Path], [OnlineMode], [ContentType], [ContentEncoding], [ContentSize], [ContentLastModification], [Content]) VALUES (@path, @onlineMode, @contentType, @contentEncoding, @contentSize, @lastModified, @content);" :
							"UPDATE [main].[OfflineCache] SET [OnlineMode] = @onlineMode, [ContentType] = @contentType, [ContentEncoding] = @contentEncoding, [ContentSize] = @contentSize, [ContentLastModification] = @lastModified, [Content] = @content WHERE [Id] = @id;",
						localConnection, transaction
					))
					{
						if (currentRowId == null)
							command.Parameters.Add("@path", DbType.String).Value = path;
						else
							command.Parameters.Add("@id", DbType.Int64).Value = currentRowId;

						command.Parameters.Add("@onlineMode", DbType.Boolean).Value = onlineMode;
						command.Parameters.Add("@contentType", DbType.String).Value = content.ContentType;
						command.Parameters.Add("@contentEncoding", DbType.String).Value = content.Encoding.BodyName;
						command.Parameters.Add("@contentSize", DbType.Int32).Value = content.ContentLength;
						command.Parameters.Add("@lastModified", DbType.DateTime).Value = content.LastModified;
						command.Parameters.Add("@content", DbType.Binary).Value = contentBytes;

						var affectedRows = command.ExecuteNonQuery();
						if (affectedRows != 1)
							throw new Exception(String.Format("The insert of item \"{0}\" affected an unexpected number ({1}) of rows.", path, affectedRows));
					}
				}
				finally
				{
					content.Response?.Dispose();
				}

				transaction.Commit();
			} // transaction
		} // proc UpdateOfflineItem

		protected virtual bool TryGetOfflineItem(string path, bool onlineMode, out string contentType, out Stream data)
		{
			contentType = null;
			data = null;

			if (String.IsNullOrEmpty(path))
				return false;
			if (path[0] == '/')
				path = path.Substring(1);
			if (String.IsNullOrEmpty(path))
				return false;

			try
			{
				if (localConnection == null || localConnection.State != ConnectionState.Open)
					return false;
			}
			catch (ObjectDisposedException)
			{
				return false;
			}

			string resultContentType = null;
			Stream resultData = null;
			try
			{
				using (var command = new SQLiteCommand("SELECT [OnlineMode], [ContentType], [ContentEncoding], [Content] FROM [main].[OfflineCache] WHERE [Path] = @path;", localConnection))
				{
					command.Parameters.Add("@path", DbType.String).Value = path;
					using (var reader = command.ExecuteReader(CommandBehavior.SingleRow))
					{
						if (!reader.Read())
							return false;

						var readOnlineMode = reader.GetBoolean(0);
						if (onlineMode && !readOnlineMode) // Verify that the stored item can be used in online mode.
							return false;

						resultContentType = reader.GetString(1);
						if (String.IsNullOrEmpty(resultContentType))
							return false;

						var readContentEncoding = reader.IsDBNull(2) ? 
							new string[0] : 
							reader.GetString(2).Split(';');

						if (readContentEncoding.Length > 0 && !String.IsNullOrEmpty(readContentEncoding[0]))
							resultContentType = resultContentType + ";charset=" + readContentEncoding[0];

						var isCompressedContent = readContentEncoding.Length > 1 && readContentEncoding[1] == "gzip"; // compression is marked on index 1

						var src = reader.GetStream(3); // This method returns a newly created MemoryStream object.
						resultData = isCompressedContent ?
							new GZipStream(src, CompressionMode.Decompress, false) :
							src;
					} // using reader
				} // using command
			} // try
			catch (Exception e)
			{
				Traces.AppendException(e, String.Format("Failed to resolve offline item with path \"{0}\".", path));
				resultData?.Dispose();
				return false;
			} // catch e

			contentType = resultContentType;
			data = resultData;
			return true;
		} // func TryGetOfflineItem

		#endregion

		#region -- Web Request ------------------------------------------------------------

		#region -- class PpsStoreRequest --------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		protected class PpsStoreRequest : WebRequest
		{
			private readonly PpsEnvironment environment; // owner, that retrieves a resource
			private readonly Uri uri; // resource
			private bool aborted = false; // is the request cancelled
			private Func<WebResponse> procGetResponse; // async GetResponse

			private string path;
			private WebHeaderCollection headers;
			private NameValueCollection arguments;

			private string method = HttpMethod.Get.Method;
			private string contentType = null;
			private long contentLength = -1;

			public PpsStoreRequest(PpsEnvironment environment, Uri uri, string path)
			{
				this.environment = environment;
				this.uri = uri;
				this.procGetResponse = GetResponse;
				this.path = path;

				arguments = HttpUtility.ParseQueryString(uri.Query);
			} // ctor

			#region -- GetResponse --------------------------------------------------------------

			/// <summary>Handles the request async</summary>
			/// <param name="callback"></param>
			/// <param name="state"></param>
			/// <returns></returns>
			public override IAsyncResult BeginGetResponse(AsyncCallback callback, object state)
			{
				if (aborted)
					throw new WebException("Canceled", WebExceptionStatus.RequestCanceled);

				return procGetResponse.BeginInvoke(callback, state);
			} // func BeginGetResponse

			/// <summary></summary>
			/// <param name="asyncResult"></param>
			/// <returns></returns>
			public override WebResponse EndGetResponse(IAsyncResult asyncResult)
			{
				return procGetResponse.EndInvoke(asyncResult);
			} // func EndGetResponse

			/// <summary></summary>
			/// <returns></returns>
			public override WebResponse GetResponse()
			{
				var response = new PpsStoreResponse(this);
				environment.GetResponseDataStream(response);
				return response;
			} // func GetResponse

			#endregion

			public override string Method { get { return method; } set { method = value; } }
			public override string ContentType { get { return contentType; } set { contentType = value; } }
			public override long ContentLength { get { return contentLength; } set { contentLength = value; } }

			public PpsEnvironment Environment => environment;

			public override Uri RequestUri => uri;

			public override IWebProxy Proxy { get { return null; } set { } } // avoid NotImplementedExceptions

			/// <summary>Arguments of the request</summary>
			public NameValueCollection Arguments => arguments;
			/// <summary>Relative path for the request.</summary>
			public string Path => path;
			/// <summary>Header</summary>
			public override WebHeaderCollection Headers { get { return headers ?? (headers = new WebHeaderCollection()); } set { headers = value; } }
		} // class PpsStoreRequest

		#endregion

		#region -- class PpsStoreCacheRequest ---------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class PpsStoreCacheRequest : PpsStoreRequest
		{
			private WebRequest onlineRequest = null;

			public PpsStoreCacheRequest(PpsEnvironment environment, Uri uri, string absolutePath)
				: base(environment, uri, absolutePath)
			{
			} // ctor

			private WebRequest CreateOnlineRequest()
			{
				if (onlineRequest == null)
				{
					var r = Environment.GetOnlineRequest(RequestUri, Path);

					// copy properties
					r.Method = this.Method;

					if (ContentType != null)
						r.ContentType = this.ContentType;
					if (r.ContentLength > 0)
						r.ContentLength = this.ContentLength;

					// copy headers
					foreach (string k in Headers.Keys)
						r.Headers[k] = this.Headers[k];

					onlineRequest = r;
				}

				return onlineRequest;
			} // func GetOnlineRequest
			
			public override WebResponse GetResponse()
			{
				if (onlineRequest == null)
				{
					string contentType;
					Stream source;

					// is this a static item
					if (Environment.TryGetOfflineItem(Path, true, out contentType, out source))
					{
						var r = new PpsStoreResponse(this);
						r.SetResponseData(source, contentType);
						return r;
					}
					else if (Environment.IsOnline)
					{
						// todo: dynamic cache, copy of properties and headers
						return CreateOnlineRequest().GetResponse();
					}
					else
						throw new WebException("File not found.", null, WebExceptionStatus.ProtocolError, null);
				}
				else
					return onlineRequest.GetResponse();
			} // func GetResponse

			public override IAsyncResult BeginGetRequestStream(AsyncCallback callback, object state)
				=> CreateOnlineRequest().BeginGetRequestStream(callback, state);

			public override Stream EndGetRequestStream(IAsyncResult asyncResult)
				=> onlineRequest.EndGetRequestStream(asyncResult);

			public override Stream GetRequestStream()
				=> CreateOnlineRequest().GetRequestStream();

			public override WebHeaderCollection Headers
			{
				get
				{
					return onlineRequest == null ? base.Headers : onlineRequest.Headers;
				}
				set
				{
					if (onlineRequest == null)
						base.Headers = value;
					else
						onlineRequest.Headers = value;
				}
			} // prop Headers

			public override string ContentType
			{
				get
				{
					return onlineRequest == null ? base.ContentType : onlineRequest.ContentType;
				}
				set
				{
					if (onlineRequest == null)
						base.ContentType = value;
					else
						onlineRequest.ContentType = value;
				}
			} // prop ContentType

			public override long ContentLength
			{
				get
				{
					return onlineRequest == null ? base.ContentLength : onlineRequest.ContentLength;
				}
				set
				{
					if (onlineRequest == null)
						base.ContentLength = value;
					else
						onlineRequest.ContentLength = value;
				}
			}
		} // class PpsStoreCacheRequest

		#endregion

		#region -- class PpsStoreResponse -------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		protected sealed class PpsStoreResponse : WebResponse
		{
			private PpsStoreRequest request;

			private Stream src;
			private string contentType;
			private long? contentLength = null;

			private WebHeaderCollection headers;

			public PpsStoreResponse(PpsStoreRequest request)
			{
				this.request = request;
				this.headers = new WebHeaderCollection();

				this.src = null;
				this.contentType = null;
			} // ctor

			public override void Close()
			{
				Procs.FreeAndNil(ref src);
				contentLength = null;
				contentType = null;
				base.Close();
			} // proc Close

			public void SetResponseData(Stream src, string contentType)
			{
				Procs.FreeAndNil(ref this.src);

				this.src = src;
				this.contentType = contentType;
			} // proc SetResponseData

			public override Stream GetResponseStream()
				=> src;

			/// <summary></summary>
			public override long ContentLength
			{
				get { return contentLength ?? (src == null ? -1 : src.Length); }
				set { contentLength = value; }
			} // func ContentLength

			/// <summary></summary>
			public override string ContentType
			{
				get { return contentType; }
				set { contentType = value; }
			} // prop ContentType

			/// <summary>Headers will be exists</summary>
			public override bool SupportsHeaders => true;
			/// <summary>Header</summary>
			public override WebHeaderCollection Headers => headers;

			/// <summary>Request uri</summary>
			public override Uri ResponseUri => request.RequestUri;
			/// <summary>Access to the original request.</summary>
			public PpsStoreRequest Request => request;
		} // class PpsStoreResponse

		#endregion

		private WebRequest CreateProxyRequest(Uri uri)
		{
			var useOfflineRequest = !isOnline;
			var useCache = true;
			var absolutePath = uri.AbsolutePath;

			// is the local data prefered
			if (uri.AbsolutePath.StartsWith("/local/"))
			{
				absolutePath = absolutePath.Substring(6);
				useOfflineRequest = true;
			}
			else if (uri.AbsolutePath.StartsWith("/remote/"))
			{
				absolutePath = absolutePath.Substring(7);
				useOfflineRequest = false;
				useCache = false;
			}

			// create the request
			if (useOfflineRequest)
				return GetOfflineRequest(uri, absolutePath);
			else
			{
				return useCache ?
					GetCachedRequest(uri, absolutePath) :
					GetOnlineRequest(uri, absolutePath);
			}
		} // func CreateWebRequest

		private WebRequest GetOfflineRequest(Uri uri, string absolutePath)
			=> new PpsStoreRequest(this, uri, absolutePath);

		private WebRequest GetCachedRequest(Uri uri, string absolutePath)
			=> new PpsStoreCacheRequest(this, uri, absolutePath);

		private WebRequest GetOnlineRequest(Uri uri, string absolutePath)
		{
			var request = WebRequest.Create(info.Uri.ToString() + absolutePath + uri.Query); // todo:
			request.Credentials = userInfo; // override the current credentials
			return request;
		} // func GetOnlineRequest

		protected virtual void GetResponseDataStream(PpsStoreResponse r)
		{
			Stream src;
			string contentType;

			var filePath = r.ResponseUri.GetComponents(UriComponents.PathAndQuery, UriFormat.UriEscaped);
			if (TryGetOfflineItem(filePath, false, out contentType, out src)) // ask the file from the cache
				r.SetResponseData(src, contentType);
			else
				throw new WebException($"File '{filePath}' not found.", null, WebExceptionStatus.ProtocolError, r);
		} // proc GetResponseDataStream

		#endregion

		#region -- GetViewData ------------------------------------------------------------

		/// <summary></summary>
		/// <param name="arguments"></param>
		/// <returns></returns>
		public virtual IEnumerable<IDataRow> GetViewData(PpsShellGetList arguments)
			=> GetRemoteViewData(arguments);

		protected IEnumerable<IDataRow> GetRemoteViewData(PpsShellGetList arguments)
		{
			var sb = new StringBuilder("remote/?action=viewget&v=");
			sb.Append(arguments.ViewId);

			if (arguments.Filter != null && arguments.Filter != PpsDataFilterTrueExpression.True)
				sb.Append("&f=").Append(Uri.EscapeDataString(arguments.Filter.ToString()));
			if (arguments.Order != null && arguments.Order.Length > 0)
				sb.Append("&o=").Append(Uri.EscapeDataString(PpsDataOrderExpression.ToString(arguments.Order)));
			if (arguments.Start != -1)
				sb.Append("&s=").Append(arguments.Start);
			if (arguments.Count != -1)
				sb.Append("&c=").Append(arguments.Count);
			if (!String.IsNullOrEmpty(arguments.AttributeSelector))
				sb.Append("&a=").Append(arguments.AttributeSelector);

			return Request.CreateViewDataReader(sb.ToString());
		} // func GetRemoteViewData

		#endregion

		#region -- ActiveDataSets ---------------------------------------------------------

		internal void OnDataSetActivated(PpsDataSetDesktop dataset)
		{
			if (activeDataSets.Find(dataset.DataSetId) != null)
				throw new ArgumentException($"DataSet already registered (Id: ${dataset.DataSetId})");

			activeDataSets[dataset.DataSetId] = dataset;
		} // proc OnDataSetActivated

		internal void OnDataSetDeactivated(PpsDataSetDesktop dataset)
		{
			activeDataSets.Remove(dataset.DataSetId);
		} // proc OnDataSetDeactivated

		[LuaMember(nameof(ActiveDataSets))]
		public IPpsActiveDataSets ActiveDataSets => activeDataSets;

		#endregion

		protected virtual void OnIsOnlineChanged()
		{
			IsOnlineChanged?.Invoke(this, EventArgs.Empty);
			OnPropertyChanged(nameof(IsOnline));
		} // proc OnIsOnlineChanged

		/// <summary></summary>
		[LuaMember("Request")]
		public BaseWebRequest Request => request;
		/// <summary>Default encodig for strings.</summary>
		public Encoding Encoding => Encoding.Default;
		/// <summary>Internal Uri of the environment.</summary>
		public Uri BaseUri => baseUri;
		/// <summary>Is <c>true</c>, if the application is online.</summary>
		public bool IsOnline
		{
			get { return isOnline; }
			private set
			{
				if (isOnline != value)
					OnIsOnlineChanged();
			}
		} // prop IsOnline

		/// <summary>Connection to the local datastore</summary>
		public SQLiteConnection LocalConnection => localConnection;
	} // class PpsEnvironment
}
