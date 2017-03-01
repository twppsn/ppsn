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
using System.Text.RegularExpressions;
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
		private class PpsActiveDataSetsImplementation : List<PpsDataSetDesktop>, IPpsActiveDataSets
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

			public Task<PpsDataSetDefinitionDesktop> GetDataSetDefinitionAsync(string schema)
			{
				KnownDataSetDefinition definition;
				lock (datasetDefinitions)
				{
					if (!datasetDefinitions.TryGetValue(schema, out definition))
						return Task.FromResult<PpsDataSetDefinitionDesktop>(null);
				}
				return definition.GetDocumentDefinitionAsync();
			} // func GetDataSetDefinition

			public IEnumerable<T> GetKnownDataSets<T>(string schema = null)
				where T : PpsDataSetDesktop
			{
				foreach (var c in this)
				{
					var ds = c as T;
					if (ds != null && (schema == null || ((PpsDataSetDefinitionDesktop)ds.DataSetDefinition).SchemaType == schema))
						yield return ds;
				}
			} // func GetKnownDataSets

			public IEnumerable<string> KnownSchemas
			{
				get
				{
					lock (datasetDefinitions)
						return datasetDefinitions.Keys.ToArray();
				}
			} // prop KnownSchemas
		} // class PpsActiveDataSetsImplementation

		#endregion

		private SQLiteConnection localConnection;   // local datastore
		private readonly Uri baseUri;               // internal uri for this datastore
		
		private long lastSynchronizationId = -1;    // sync token
		private DateTime lastSynchronizationStamp = DateTime.MinValue;	// last synchronization stamp
		private DateTime lastSynchronizationSchema = DateTime.MinValue; // last synchronization of the schema

		private readonly BaseWebRequest request;

		#region -- Init -------------------------------------------------------------------

		private Task<string> GetLocalStorePassword()
		{
			var passwordFile = Path.Combine(LocalPath.FullName, "localStore.dat");
			if (File.Exists(passwordFile))
				return Task.FromResult("geheim"); // todo: rk read and verify
			else
				return Task.FromResult("geheim"); // todo: rk generate and store
		} // func GetLocalStorePassword

		/// <summary></summary>
		/// <returns><c>true</c>, if a valid database is present.</returns>
		private async Task<bool> InitLocalStoreAsync(IProgress<string> progress)
		{
			var isUseable = false;
			// open a new local store
			SQLiteConnection newLocalStore = null;
			try
			{
				// open the local database
				progress.Report("Lokale Datenbank öffnen...");
				var dataPath = Path.Combine(LocalPath.FullName, "localStore.db");
				newLocalStore = new SQLiteConnection($"Data Source={dataPath};DateTimeKind=Utc;Password=Pps{GetLocalStorePassword()}"); // foreign keys=true
				await newLocalStore.OpenAsync();

				// check synchronisation table
				progress.Report("Lokale Datenbank verifizieren...");
				if (TestLocalTableColumns(newLocalStore, "SyncTokens",
					new SimpleDataColumn("Schema", typeof(long)),
					new SimpleDataColumn("SyncStamp", typeof(long)),
					new SimpleDataColumn("SyncToken", typeof(long))
					))
				{
					// read sync tokens
					using (var commd = new SQLiteCommand("SELECT Schema, SyncStamp, SyncToken FROM main.SyncTokens ", newLocalStore))
					{
						using (var r = commd.ExecuteReader(CommandBehavior.SingleRow))
						{
							if (r.Read() && !r.IsDBNull(0) && !r.IsDBNull(1) && !r.IsDBNull(2))
							{
								lastSynchronizationSchema = DateTime.FromFileTimeUtc(r.GetInt64(0));
								lastSynchronizationStamp = DateTime.FromFileTimeUtc(r.GetInt64(1));
								lastSynchronizationId = r.GetInt64(2);
								isUseable = true;
							}
						}
					}
				}

				// reset values
				if (!isUseable)
				{
					lastSynchronizationSchema = DateTime.MinValue;
					lastSynchronizationStamp = DateTime.MinValue;
					lastSynchronizationId = -1;
				}
			}
			catch
			{
				newLocalStore?.Dispose();
				throw;
			}

			// close current connection
			localConnection?.Dispose();

			// set new connection
			localConnection = newLocalStore;

			return isUseable;
		} // proc InitLocalStore

		private Uri InitProxy()
		{
			// register proxy for the web requests
			var baseUri = new Uri($"http://ppsn{environmentId}.local");
			WebRequest.RegisterPrefix(baseUri.ToString(), new PpsWebRequestCreate(this));
			return baseUri;
		} // func InitProxy

		#endregion

		#region -- Local store primitives -----------------------------------------------

		private static Type ConvertSqLiteDataType(string dataType)
		{
			if (String.IsNullOrEmpty(dataType))
				return typeof(string);
			else
				switch (Char.ToUpper(dataType[0]))
				{
					case 'I':
						return String.Compare(dataType, "INTEGER", StringComparison.OrdinalIgnoreCase) == 0
							? typeof(long)
							: typeof(string);
					case 'R':
						return String.Compare(dataType, "REAL", StringComparison.OrdinalIgnoreCase) == 0
							? typeof(double)
							: typeof(string);
					case 'B':
						return String.Compare(dataType, "BLOB", StringComparison.OrdinalIgnoreCase) == 0
							? typeof(byte[])
							: typeof(string);
					default: // TEXT, NUMERIC (numeric is date, datetime, decimal, boolean, ...)
						return typeof(string);
				}
		} // func ConvertSqLiteDataType

		private static bool CheckLocalTableExists(SQLiteConnection connection, string tableName)
		{
			using (var command = new SQLiteCommand("SELECT [tbl_name] FROM [sqlite_master] WHERE [type] = 'table' AND [tbl_name] = @tableName;", connection))
			{
				command.Parameters.Add("@tableName", DbType.String, tableName.Length + 1).Value = tableName;
				using (var r = command.ExecuteReader(CommandBehavior.SingleRow))
					return r.Read();
			}
		} // func CheckLocalTableExistsAsync

		private static IEnumerable<IDataColumn> GetLocalTableColumns(SQLiteConnection connection, string tableName)
		{
			using (var command = new SQLiteCommand($"PRAGMA table_info({tableName});", connection))
			{
				using (var r = command.ExecuteReader(CommandBehavior.SingleResult))
				{
					while (r.Read())
					{
						yield return new SimpleDataColumn(
							r.GetString(1),
							r.IsDBNull(2) ? typeof(string) : ConvertSqLiteDataType(r.GetString(2)),
							new PropertyDictionary(
								new PropertyValue("IsNull", r.IsDBNull(3) || r.GetBoolean(3)),
								new PropertyValue("Default", r.GetValue(4)?.ToString()),
								new PropertyValue("IsPrimary", !r.IsDBNull(5) && r.GetBoolean(5))
							)
						);
					}
				}
			}
		} // func GetLocalTableColumns

		private static bool TestLocalTableColumns(SQLiteConnection connection, string tableName, params SimpleDataColumn[] columns)
		{
			using (var e = GetLocalTableColumns(connection, tableName).GetEnumerator())
			{
				for (var i = 0; i < columns.Length; i++)
				{
					if (!e.MoveNext())
						return false;

					var textColumn = e.Current;
					var expectedColumn = columns[i];
					if (String.Compare(textColumn.Name, expectedColumn.Name, StringComparison.OrdinalIgnoreCase) != 0)
						return false;
					else if (textColumn.DataType != expectedColumn.DataType)
						return false;
				}
			}
			return true;
		} // func TestLocalTableColumns

		#endregion

		//private async Task RefreshOfflineCacheAsync()
		//{
		//	if (IsOnline && IsAuthentificated)
		//		await Task.Run(new Action(UpdateOfflineItems));
		//} // RefreshOfflineCacheAsync

		private Task<bool> SynchronizationAsync(IProgress<string> progress)
		{
			progress.Report("Synchronization...");

			// todo: check for new schema PpsMasterData

			// todo: fetch data

			lastSynchronizationId = 0;
			return Task.FromResult(true);
		} // func SynchronizationAsync

		/// <summary>Tests, if the synchronization needs to be in foreground (last sync it to far away e.g. 1 day)</summary>
		/// <returns></returns>
		private Task<bool> CheckSynchronizationStateAsync()
		{
			// check if schema is change
			// todo: HEAD schema

			// is the system "synchrone enough"?
			return Task.FromResult((DateTime.UtcNow - lastSynchronizationStamp) > TimeSpan.FromDays(1));
		} // proc CheckSynchronizationStateAsync

		public bool IsSynchronizationStarted => lastSynchronizationId >= 0;

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
					//if (path.Contains("masterdata"))
					//	path = path;
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
								contentType.CharSet == null ? Encoding.UTF8 : Encoding.GetEncoding(contentType.CharSet),
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
					using (var src = Request.GetStream(content.Response, null))
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

		private bool MoveReader(SQLiteDataReader r, string path, NameValueCollection arguments)
		{
			while (r.Read())
			{
				var testPath = r.GetString(0);

				// get query is only allowed for absolute queries, so we scan for ?
				var pos = testPath.IndexOf('?');
				if (pos == -1 && arguments.Count == 0) // path is exact
				{
					if (String.Compare(path, testPath, StringComparison.OrdinalIgnoreCase) == 0)
						return true;
				}
				else if (arguments.Count > 0)
				{
					var testArguments = HttpUtility.ParseQueryString(testPath.Substring(pos + 1));
					var failed = false;
					foreach (var c in arguments.AllKeys)
					{
						var testValue = testArguments[c];
						if (testValue == null || String.Compare(testValue, arguments[c], StringComparison.OrdinalIgnoreCase) != 0)
						{
							failed = true;
							break;
						}
					}
					if (!failed)
						return true; // all arguments are fit
				}
			}
			return false;
		} // func MoveReader

		protected virtual bool TryGetOfflineItem(string path, NameValueCollection arguments, bool onlineMode, out string contentType, out Stream data)
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
				using (var command = new SQLiteCommand("SELECT [Path], [OnlineMode], [ContentType], [ContentEncoding], [Content] FROM [main].[OfflineCache] WHERE substr([Path], 1, length(@path)) = @path;", localConnection))
				{
					command.Parameters.Add("@path", DbType.String).Value = path;
					using (var reader = command.ExecuteReader(CommandBehavior.SingleRow))
					{
						if (!MoveReader(reader, path, arguments))
							return false;

						var readOnlineMode = reader.GetBoolean(1);
						if (onlineMode && !readOnlineMode) // Verify that the stored item can be used in online mode.
							return false;

						resultContentType = reader.GetString(2);
						if (String.IsNullOrEmpty(resultContentType))
							return false;

						var readContentEncoding = reader.IsDBNull(3) ?
							new string[0] :
							reader.GetString(3).Split(';');

						if (readContentEncoding.Length > 0 && !String.IsNullOrEmpty(readContentEncoding[0]))
							resultContentType = resultContentType + ";charset=" + readContentEncoding[0];

						var isCompressedContent = readContentEncoding.Length > 1 && readContentEncoding[1] == "gzip"; // compression is marked on index 1

						var src = reader.GetStream(4); // This method returns a newly created MemoryStream object.
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
					if (Environment.TryGetOfflineItem(Path, Arguments, true, out contentType, out source))
					{
						var r = new PpsStoreResponse(this);
						r.SetResponseData(source, contentType);
						return r;
					}
					else if (Environment.CurrentMode == PpsEnvironmentMode.Online)
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
			var useOfflineRequest = CurrentMode == PpsEnvironmentMode.Offline;
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

			if (TryGetOfflineItem(r.Request.Path, r.Request.Arguments, false, out contentType, out src)) // ask the file from the cache
				r.SetResponseData(src, contentType);
			else
				throw new WebException($"File '{r.Request.RequestUri.GetComponents(UriComponents.PathAndQuery, UriFormat.Unescaped)}' not found.", null, WebExceptionStatus.ProtocolError, r);
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
			if (arguments.ViewId.StartsWith("local.", StringComparison.OrdinalIgnoreCase)) // it references the local db
			{
				if (arguments.ViewId == "local.objects")
					return CreateObjectFilter(arguments);
				else
					throw new ArgumentOutOfRangeException("todo"); // todo: exception
			}
			else
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
			}
		} // func GetRemoteViewData

		#endregion

		private async Task RefreshMasterDataSchemeAsync()
		{
			if (CurrentMode == PpsEnvironmentMode.Online)
			{
				var commands = new List<string>();
				var masterDataDataSet = await ActiveDataSets.GetDataSetDefinitionAsync("masterdata");
				if (masterDataDataSet == null)
					throw new Exception("Failed to load masterdata.xml.");

				var master = new PpsMasterData(masterDataDataSet, localConnection);

				master.RefreshMasterDataScheme();
			}
		}

		public async Task<bool> ForceOnlineAsync(bool throwException = true)
		{
			if (CurrentMode != PpsEnvironmentMode.Online)
			{
				switch (await WaitForEnvironmentMode(PpsEnvironmentMode.Online))
				{
					case PpsEnvironmentModeResult.Online:
						return true;
				}
			}
			throw new NotImplementedException("Todo: Force online mode.");
		} // func ForceOnlineMode

		/// <summary></summary>
		[LuaMember]
		public BaseWebRequest Request => request;
		/// <summary>Default encodig for strings.</summary>
		public Encoding Encoding => Encoding.Default;
		/// <summary>Internal Uri of the environment.</summary>
		public Uri BaseUri => baseUri;

		/// <summary>Connection to the local datastore</summary>
		public SQLiteConnection LocalConnection => localConnection;
	} // class PpsEnvironment
}
