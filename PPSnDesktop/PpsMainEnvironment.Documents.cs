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
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Networking;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;

namespace TecWare.PPSn
{
	#region -- interface IPpsActiveDocuments --------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Extension for the dataset model.</summary>
	public interface IPpsActiveDocuments
	{
		/// <summary>Create and initialize a new document.</summary>
		/// <param name="schema">Schema of the document.</param>
		/// <param name="arguments">Arguments</param>
		/// <returns>Initialized document.</returns>
		Task<PpsDocument> CreateDocumentAsync(string schema, LuaTable arguments);
		/// <summary>Open an existing document (or already opened document).</summary>
		/// <param name="documentId">Id of the document.</param>
		/// <param name="arguments">Arguments</param>
		/// <returns>Initialized document</returns>
		Task<PpsDocument> OpenDocumentAsync(PpsDataSetId documentId, LuaTable arguments);

		/// <summary>Returns the default pane for a schema.</summary>
		/// <param name="schema">Name of the document.</param>
		/// <returns>Uri of the pane.</returns>
		Task<string> GetDocumentDefaultPaneAsync(string schema);
	} // interface IPpsDocuments

	#endregion

	#region -- class PpsObjectInfo ------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsObjectInfo
	{
		private readonly long localId;
		private readonly long serverId;
		private readonly Guid guid;
		private readonly string typ;
		private readonly string nr;
		private readonly bool isRev;
		private readonly long remoteRevId;
		private readonly long pulledRevId;
		private readonly bool isDocumentChanged;
		private readonly bool hasData;

		public PpsObjectInfo(long localId, long serverId, Guid guid, string typ, string nr, bool isRev, long remoteRevId, long pulledRevId, bool isDocumentChanged, bool hasData)
		{
			this.localId = localId;
			this.serverId = serverId;
			this.guid = guid;
			this.typ = typ;
			this.nr = nr;
			this.isRev = isRev;
			this.remoteRevId = remoteRevId;
			this.pulledRevId = pulledRevId;
			this.isDocumentChanged = isDocumentChanged;
			this.hasData = hasData;
		} // ctor

		public long LocalId => localId;
		public long ServerId => serverId;
		public Guid Guid => guid;
		public string Typ => typ;
		public string Nr => nr;
		public bool IsRev => isRev;
		public long RemoteRevId => remoteRevId;
		public long PulledRevId => pulledRevId;
		public bool IsDocumentChanged => isDocumentChanged;
		public bool HasData => hasData;
	} // class PpsObjectInfo

	#endregion

	#region -- interface IPpsLocalStoreTransaction --------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public interface IPpsLocalStoreTransaction : IDbTransaction
	{
		/// <summary>Get information about the object guid.</summary>
		/// <param name="guid"></param>
		/// <returns></returns>
		PpsObjectInfo GetInfo(Guid guid);

		/// <summary>Create a new object.</summary>
		/// <param name="serverId"></param>
		/// <param name="guid"></param>
		/// <param name="typ"></param>
		/// <param name="nr"></param>
		/// <param name="isRev"></param>
		/// <param name="remoteRevId"></param>
		/// <returns></returns>
		long Create(long serverId, Guid guid, string typ, string nr, bool isRev, long remoteRevId);

		/// <summary></summary>
		/// <param name="localId"></param>
		/// <param name="serverId"></param>
		/// <param name="pulledRevId"></param>
		/// <param name="nr"></param>
		void Update(long localId, long serverId, long pulledRevId, string nr);
		/// <summary>Write document data in the local store.</summary>
		/// <param name="trans"></param>
		/// <param name="localId"></param>
		/// <param name="serverId"></param>
		/// <param name="pulledRevId"></param>
		/// <param name="nr"></param>
		/// <param name="data"></param>
		void UpdateData(long localId, Action<Stream> data, long serverId = -1, long pulledRevId = -1, string nr = null, bool isDocumentChanged = true);

		/// <summary>Read data from the object.</summary>
		/// <param name="localId"></param>
		/// <returns></returns>
		Stream GetData(long localId);

		/// <summary></summary>
		/// <param name="localId"></param>
		void Delete(long localId);

		/// <summary>Refresh the meta data/tags of a local store object</summary>
		/// <param name="localId"></param>
		/// <param name="tags"></param>
		void UpdateTags(long localId, IEnumerable<PpsObjectTag> tags);
		/// <summary>Refresh the meta data/tags of a local store object</summary>
		/// <param name="localId"></param>
		/// <param name="tags"></param>
		void UpdateTags(long localId, LuaTable tags);

		/// <summary>Read a DataSet from the local store.</summary>
		/// <param name="dataset">DataSet</param>
		/// <param name="guid"></param>
		/// <returns></returns>
		bool ReadDataSet(long localId, PpsDataSet dataset);
		/// <summary>Refresh a dataset in the local store.</summary>
		/// <param name="dataset"></param>
		void UpdateDataSet(long localId, PpsDataSet dataset);

		/// <summary>Access to the core transaction.</summary>
		IDbTransaction Transaction { get; }
	} // interface IPpsLocalStoreTransaction

	#endregion

	#region -- class PpsDocumentDefinition ----------------------------------------------

	public sealed class PpsDocumentDefinition : PpsDataSetDefinitionDesktop
	{
		private readonly PpsMainEnvironment environment;

		public PpsDocumentDefinition(PpsMainEnvironment environment, string type, XElement xSchema)
			: base(environment, type, xSchema)
		{
			this.environment = environment;
		} // ctor

		public override PpsDataSet CreateDataSet()
		{
			throw new NotSupportedException();
		} // func CreateDataSet

		public override PpsDataSetDesktop CreateDataSet(PpsDataSetId id)
			=> new PpsDocument(this, (PpsMainEnvironment)Shell, id);
	} // class PpsDocumentDefinition
	
	#endregion

	#region -- class PpsDocument --------------------------------------------------------

	public sealed class PpsDocument : PpsDataSetDesktop
	{
		private readonly PpsMainEnvironment environment;
		private readonly PpsUndoManager undoManager;

		private long localId = -2;        // local Id in the cache (-2 for not setted)
		private long pulledRevisionId;
		private bool isDocumentChanged = false;   // is this document changed to the pulled version
		private bool isReadonly = true;           // is this document a special read-only revision

		#region -- Ctor/Dtor --------------------------------------------------------------

		public PpsDocument(PpsDataSetDefinitionDesktop datasetDefinition, PpsMainEnvironment environment, PpsDataSetId datasetId)
			: base(datasetDefinition, environment, datasetId)
		{
			this.environment = environment;

			this.undoManager = new PpsUndoManager();
			RegisterUndoSink(undoManager);
		} // ctor

		public void SetLocalState(long localId, long pulledRevisionId, bool isDocumentChanged, bool isReadonly)
		{
			if (localId < 0)
				localId = -1;

			this.localId = localId;
			this.pulledRevisionId = pulledRevisionId;
			this.isDocumentChanged = isDocumentChanged;
			this.isReadonly = isReadonly;

			ResetDirty();
		} // proc SetLocalState

		private void CheckLocalState()
		{
			if (localId < -1)
				throw new InvalidOperationException("Document is not initialized.");
		} // proc CheckLocalState

		#endregion
		
		public override Task OnNewAsync(LuaTable arguments)
		{
			CheckLocalState();
			return base.OnNewAsync(arguments);
		} // func OnNewAsync

		public override Task OnLoadedAsync(LuaTable arguments)
		{
			CheckLocalState();
			return base.OnLoadedAsync(arguments);
		} // proc OnLoadedAsync

		public async Task PushWorkAsync()
		{
			CommitWork();

			var head = Tables["Head", true];
			var documentType = head.First["Typ"];

			long newServerId;
			long newRevId;

			// send the document to the server
			using (var xmlAnswer = environment.Request.GetXmlStreamAsync(
				await environment.Request.PutTextResponseAsync(documentType + "/?action=push", MimeTypes.Text.Xml,
					(tw) =>
					{
						using (var xmlPush = XmlWriter.Create(tw, Procs.XmlWriterSettings))
							Write(xmlPush);
					}
				)))
			{
				var xResult = XDocument.Load(xmlAnswer);

				newServerId = xResult.Root.GetAttribute("id", -1L);
				newRevId = xResult.Root.GetAttribute("revId", -1L);
				if (newServerId < 0 || newRevId < 0)
					throw new ArgumentOutOfRangeException("id", "Pull action failed.");
			}

			// pull the document again, and update the local store
			var xRoot = await environment.Request.GetXmlAsync($"{documentType}/?action=pull&id={newServerId}&rev={newRevId}");

			// recreate dataset
			undoManager.Clear();
			Read(xRoot);
			SetLocalState(localId, newRevId, false, false);

			// update local store and
			using (var trans = environment.BeginLocalStoreTransaction())
			{
				trans.Update(localId, newServerId, newRevId, Tables["Head", true].First["Nr"].ToString());
				trans.UpdateDataSet(localId, this);
				trans.Commit();
			}
		} // proc PushWork

		public void CommitWork()
		{
			var head = Tables["Head", true];
			var data = new StringBuilder();

			// convert data to a string
			using (var dst = new StringWriter(data))
			using (var xml = XmlWriter.Create(dst, Procs.XmlWriterSettings))
				Write(xml);

			// update local store
			using (var trans = environment.BeginLocalStoreTransaction())
			{
				// update object data
				if (localId <= 0)
					localId = trans.Create(-1, (Guid)head.First["Guid"], (string)head.First["Typ"], head.First["Nr"]?.ToString(), true, -1);

				// update the document data
				trans.UpdateDataSet(localId, this);

				// update meta tags
				trans.UpdateTags(localId, GetAutoTags());

				trans.Commit();
				ResetDirty();
			}
		} // proc CommitWork

		public bool IsReadOnly => isReadonly;
		public bool IsLoaded => localId < 0;

		public PpsUndoManager UndoManager => undoManager;
	} // class PpsDocument

	#endregion

	#region -- class PpsMainEnvironment -------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public partial class PpsMainEnvironment : IPpsActiveDocuments
	{
		#region -- class PpsLocalStoreTransaction -----------------------------------------

		private sealed class PpsLocalStoreTransaction : IPpsLocalStoreTransaction
		{
			private readonly PpsMainEnvironment environment;
			private readonly SQLiteTransaction transaction;
			private bool isDisposed = false;

			#region -- Ctor/Dtor/Commit -----------------------------------------------------

			public PpsLocalStoreTransaction(PpsMainEnvironment environment)
			{
				this.environment = environment;
				this.transaction = LocalConnection.BeginTransaction();
			} // ctor

			public void Dispose()
				=> Dispose(true);

			private void Dispose(bool disposing)
			{
				if (!isDisposed)
				{
					if (disposing)
						transaction.Dispose();

					isDisposed = true;
				}
			} // proc Dispose

			public void Commit()
			{
				if (!isDisposed)
					transaction.Commit();
				else
					throw new InvalidOperationException();
			} // proc Commit

			public void Rollback()
			{
				if (!isDisposed)
					transaction.Rollback();
				else
					throw new InvalidOperationException();
			} // proc Rollback

			#endregion

			private static bool DbNullOnNeg(long value)
				=> value < 0;

			public PpsObjectInfo GetInfo(Guid guid)
			{
				using (var cmd = LocalConnection.CreateCommand())
				{
					cmd.CommandText = "SELECT o.Id, o.ServerId, o.Typ, o.Nr, o.IsRev, o.RemoteRevId, o.PulledRevId, o.DocumentIsChanged, length(o.Document) FROM main.Objects o WHERE o.Guid = @Guid";
					cmd.Transaction = transaction;
					cmd.Parameters.Add("@Guid", DbType.Guid).Value = guid;

					using (var r = cmd.ExecuteReader(CommandBehavior.SingleRow))
					{
						if (r.Read())
						{
							return new PpsObjectInfo(
								r.GetInt64(0),
								r.IsDBNull(1) ? -1 : r.GetInt64(1),
								guid,
								r.GetString(2),
								r.GetString(3),
								r.GetBoolean(4),
								r.IsDBNull(5) ? -1 : r.GetInt64(5),
								r.IsDBNull(6) ? -1 : r.GetInt64(6),
								r.IsDBNull(7) ? false : r.GetBoolean(7),
								!r.IsDBNull(8)
							);
						}
						else
							return null;
					}
				}
			} // func GetInfo

			public long Create(long serverId, Guid guid, string typ, string nr, bool isRev, long remoteRevId)
			{
				using (var cmd = LocalConnection.CreateCommand())
				{
					cmd.CommandText = "INSERT INTO main.Objects (ServerId, Guid, Typ, Nr, IsRev, RemoteRevId) VALUES (@ServerId, @Guid, @Typ, @Nr, @IsRev, @RemoteRevId)";
					cmd.Transaction = transaction;

					cmd.Parameters.Add("@ServerId", DbType.Int64).Value = serverId.DbNullIf(DbNullOnNeg);
					cmd.Parameters.Add("@Guid", DbType.Guid).Value = guid;
					cmd.Parameters.Add("@Typ", DbType.String).Value = typ.DbNullIfString();
					cmd.Parameters.Add("@Nr", DbType.String).Value = nr.DbNullIfString();
					cmd.Parameters.Add("@IsRev", DbType.Boolean).Value = isRev;
					cmd.Parameters.Add("@RemoteRevId", DbType.Int64).Value = remoteRevId.DbNullIf(DbNullOnNeg);

					cmd.ExecuteNonQuery();

					return LocalConnection.LastInsertRowId;
				}
			} // func Create

			public void Update(long localId, long serverId, long pulledRevId, string nr)
			{
				using (var cmd = LocalConnection.CreateCommand())
				{
					cmd.CommandText = "UPDATE main.Objects SET ServerId = @ServerId, PulledRevId = @PulledRevId, Nr = @Nr WHERE Id = @Id";
					cmd.Transaction = transaction;

					cmd.Parameters.Add("@Id", DbType.Int64).Value = localId;
					cmd.Parameters.Add("@ServerId", DbType.Int64).Value = serverId.DbNullIf(DbNullOnNeg);
					cmd.Parameters.Add("@PulledRevId", DbType.Int64).Value = pulledRevId.DbNullIf(DbNullOnNeg);
					cmd.Parameters.Add("@Nr", DbType.String).Value = nr.DbNullIfString();

					cmd.ExecuteNonQuery();
				}
			} // proc Update

			public void UpdateData(long localId, Action<Stream> data, long serverId, long pulledRevId, string nr, bool isDocumentChanged)
			{
				byte[] bData = null;

				// read the data into a memory stream
				if (data != null)
				{
					using (var dst = new MemoryStream())
					{
						data(dst);
						dst.Position = 0;
						bData = dst.ToArray();
					}
				}

				// store the value
				using (var cmd = LocalConnection.CreateCommand())
				{
					cmd.CommandText = "UPDATE main.Objects SET ServerId = IFNULL(@ServerId, ServerId), PulledRevId = IFNULL(@PulledRevId, PulledRevId), Nr = IFNULL(@Nr, Nr), Document = @Document, DocumentIsChanged = @DocumentIsChanged WHERE Id = @Id";
					cmd.Transaction = transaction;

					cmd.Parameters.Add("@Id", DbType.Int64).Value = localId;
					cmd.Parameters.Add("@ServerId", DbType.Int64).Value = serverId.DbNullIf(DbNullOnNeg);
					cmd.Parameters.Add("@PulledRevId", DbType.Int64).Value = bData== null ? DBNull.Value : pulledRevId.DbNullIf(DbNullOnNeg);
					cmd.Parameters.Add("@Nr", DbType.String).Value = nr.DbNullIfString();
					cmd.Parameters.Add("@Document", DbType.Binary).Value = bData == null ? (object)DBNull.Value : bData;
					cmd.Parameters.Add("@DocumentIsChanged", DbType.Boolean).Value = bData == null ? false : isDocumentChanged;

					cmd.ExecuteNonQuery();
				}
			} // proc UpdateData

			public Stream GetData(long localId)
			{
				using (var cmd = LocalConnection.CreateCommand())
				{
					cmd.CommandText = "SELECT Document, length(Document) FROM main.Objects WHERE Id = @Id";
					cmd.Transaction = transaction;

					cmd.Parameters.Add("@Id", DbType.Int64).Value = localId;

					using (var r = cmd.ExecuteReader(CommandBehavior.SingleRow))
					{
						if (r.Read() && !r.IsDBNull(0))
						{
							var data = new byte[r.GetInt64(1)];

							r.GetBytes(0, 0, data, 0, data.Length);

							return new MemoryStream(data, false);
						}
						else
							return null;
					}
				}
			} // func GetData

			public void UpdateTags(long localId, IEnumerable<PpsObjectTag> tags)
			{
				using (var tagUpdater = new TagDatabaseCommands(LocalConnection))
				{
					tagUpdater.ObjectId = localId;
					tagUpdater.Transaction = transaction;

					IList<PpsObjectTag> t;
					if (tags is PpsObjectTag[])
						t = (PpsObjectTag[])tags;
					else if (tags is IList<PpsObjectTag>)
						t = (IList<PpsObjectTag>)tags;
					else
						t = tags.ToArray();

					tagUpdater.UpdateTags(t);
				}
			} // proc UpdateTags

			public void UpdateTags(long localId, LuaTable tags)
			{
				var tagList = new List<PpsObjectTag>();
				foreach (var c in tags.Members)
				{
					if (c.Key[0] == '_')
						continue;

					var v = c.Value;
					var t = PpsObjectTagClass.Text;
					var tString = tags.GetOptionalValue<string>("_" + c.Key, null);
					if (tString != null)
					{
						if (String.Compare(tString, "Number", StringComparison.OrdinalIgnoreCase) == 0)
							t = PpsObjectTagClass.Number;
						else if (String.Compare(tString, "Date", StringComparison.OrdinalIgnoreCase) == 0)
							t = PpsObjectTagClass.Date;
					}
					else if (v is DateTime)
						t = PpsObjectTagClass.Date;

					tagList.Add(new PpsObjectTag(c.Key, t, c.Value));
				}

				UpdateTags(localId, tagList);
			} // proc UpdateTags

			public void Delete(long localId)
			{
				using (var cmd = LocalConnection.CreateCommand())
				{
					cmd.CommandText = "DELETE FROM main.Objects WHERE Id = @Id;";
					cmd.Transaction = transaction;

					cmd.Parameters.Add("@Id", DbType.Int64).Value = localId;

					cmd.ExecuteNonQuery();
				}
			} // func Delete

			public bool ReadDataSet(long localId, PpsDataSet dataset)
			{
				var src = GetData(localId);
				if (src == null)
					return false;

				using (var xml = XmlReader.Create(src, Procs.XmlReaderSettings))
					dataset.Read(XDocument.Load(xml).Root);

				return true;
			} // func ReadDataSet

			public void UpdateDataSet(long localId, PpsDataSet dataset)
			{
				// update data
				UpdateData(localId,
					dst =>
					{
						var settings = Procs.XmlWriterSettings;
						settings.CloseOutput = false;
						using (var xml = XmlWriter.Create(dst, settings))
							dataset.Write(xml);
					}, -1, -1, null, true
				);

				// update tags
				UpdateTags(localId, dataset.GetAutoTags());
			} // func UpdateDataSet

			public IDbConnection Connection => LocalConnection;
			public IsolationLevel IsolationLevel => transaction.IsolationLevel;
			public IDbTransaction Transaction => transaction;

			private SQLiteConnection LocalConnection => environment.LocalConnection;
		} // class PpsLocalStoreTransaction

		#endregion

		private readonly Dictionary<string, string> defaultPanes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); // default panes

		private void UpdateDocumentDefinitionInfo(XElement x, List<string> updatedDocuments)
		{
			var schema = x.GetAttribute("name", String.Empty);
			var sourceUri = x.GetAttribute("source", String.Empty);
			var paneUri = x.GetAttribute("pane", String.Empty);
			if (String.IsNullOrEmpty(schema))
				return;
			if (String.IsNullOrEmpty(sourceUri))
				throw new ArgumentNullException("@source");

			// update dataset definitions
			ActiveDataSets.RegisterDataSetSchema(schema, sourceUri, typeof(PpsDocumentDefinition));

			// update pane hint
			if (!String.IsNullOrEmpty(paneUri))
			{
				lock (defaultPanes)
					defaultPanes[schema] = paneUri;
			}

			// mark document as readed
			updatedDocuments.Add(schema);
		} // proc UpdateDocumentDefinitionInfo

		private void ClearDocumentDefinitionInfo(List<string> updatedDocuments)
		{
			foreach (var k in ActiveDataSets.KnownSchemas) // is already an array
			{
				if (updatedDocuments.IndexOf(k) == -1)
					ActiveDataSets.UnregisterDataSetSchema(k);
			}
		} // proc ClearDocumentDefinitionInfo

		// the return is a uninitialized document
		// SetLocalState, OnLoadAsync is not called, yet
		private async Task<Tuple<PpsDocument, long, string>> PullDocumentCoreAsync(string documentSchema, Guid guid, long serverId, long revisionId = -1)
		{
			if (serverId <= 0)
				throw new ArgumentOutOfRangeException("serverId", "Invalid server id.");

			var xRoot = await Request.GetXmlAsync($"{documentSchema}/?action=pull&id={serverId}&rev={revisionId}");

			var newDocument = await CreateDocumentInternalAsync(documentSchema, new PpsDataSetId(guid, revisionId));
			newDocument.Read(xRoot);

			var headRow = newDocument.Tables["Head", true].First;
			var pulledRevisionId = headRow.GetProperty("HeadRevId", -1L);
			if (pulledRevisionId < 0)
				throw new ArgumentOutOfRangeException("pulledRevisionId");

			return new Tuple<PpsDocument, long, string>(newDocument, pulledRevisionId, headRow["Nr"]?.ToString());
		} // func PullDocumentCoreAsync

		private async Task<PpsDocument> GetLocalDocumentAsync(Guid guid)
		{
			using (var trans = BeginLocalStoreTransaction())
			{
				// get database info
				var objectInfo = trans.GetInfo(guid);
				if (objectInfo == null)
					throw new ArgumentException($"Object {0:B} not found.");

				if (objectInfo.PulledRevId < 0) // not synced yet, pull the head revision
				{
					var newDocument = await PullDocumentCoreAsync(objectInfo.Typ, guid, objectInfo.ServerId);

					// update document data in the local store
					trans.Update(objectInfo.LocalId, objectInfo.ServerId, newDocument.Item2, newDocument.Item3);
					trans.UpdateDataSet(objectInfo.LocalId, newDocument.Item1);

					trans.Commit();

					// init document
					newDocument.Item1.SetLocalState(objectInfo.LocalId, newDocument.Item2, false, false);

					return newDocument.Item1;
				}
				else // synced, get the current staging
				{
					var newDocument = await CreateDocumentInternalAsync(objectInfo.Typ, new PpsDataSetId(guid, -1));

					using (var docTrans = newDocument.UndoSink.BeginTransaction("Internal Read"))
					{
						trans.ReadDataSet(objectInfo.LocalId, newDocument);
						docTrans.Commit();
					}
					newDocument.UndoManager.Clear();
					trans.Rollback();

					newDocument.SetLocalState(objectInfo.LocalId, objectInfo.PulledRevId, objectInfo.IsDocumentChanged, false); // synced and change able
					return newDocument;
				}
			}
		} // func GetLocalDocumentAsync
		
		private async Task<PpsDocument> GetServerDocumentAsync(PpsDataSetId documentId)
		{
			// get database info
			using (var trans = BeginLocalStoreTransaction())
			{
				var objectInfo = trans.GetInfo(documentId.Guid);
				if(objectInfo == null || objectInfo.ServerId <0)
					throw new ArgumentException($"No server object for document {documentId.Guid:N}."); // todo:
				
				// load from server
				var newDocument = await PullDocumentCoreAsync(objectInfo.Typ, documentId.Guid, objectInfo.ServerId, documentId.Index);
				if (newDocument.Item2 != documentId.Index)
					throw new ArgumentException("rev requested != rev pulled"); // todo:


				// initialize
				newDocument.Item1.SetLocalState(-1, documentId.Index, false, true);

				trans.Rollback();

				return newDocument.Item1;
			}
		} // func GetServerDocumentAsync

		private PpsDocument FindActiveDocumentById(PpsDataSetId documentId)
		{
			var dataset = ActiveDataSets[documentId];
			if (dataset is PpsDocument)
				return (PpsDocument)dataset;
			else if (dataset != null)
				throw new ArgumentException($"{documentId} is not a document.");
			else
				return null;
		} // func FindActivateDocumentById

		private async Task<PpsDocument> CreateDocumentInternalAsync(string schema, PpsDataSetId documentId)
		{
			var def = await ActiveDataSets.GetDataSetDefinition(schema) as PpsDocumentDefinition;
			if (def == null)
				throw new ArgumentNullException("schema", $"{schema} could not initialized definition.");

			// create the empty dataset
			return (PpsDocument)def.CreateDataSet(documentId);
		} // func CreateDocumentInternalAsync

		public async Task<PpsDocument> CreateDocumentAsync(string schema, LuaTable arguments)
		{
			var documentId = new PpsDataSetId(Guid.NewGuid(), -1);

			PpsDocument newDocument = await CreateDocumentInternalAsync(schema, documentId);

			// initialize
			newDocument.SetLocalState(-1, -1, false, false);
			await newDocument.OnNewAsync(arguments);
			newDocument.UndoManager.Clear();

			return newDocument;
		} // func CreateDocumentAsync

		public async Task<PpsDocument> OpenDocumentAsync(PpsDataSetId documentId, LuaTable arguments)
		{
			PpsDocument document;

			// check if the document is already opened
			document = FindActiveDocumentById(documentId);
			if (document == null)
			{
				// is a request for the head revision
				if (documentId.Index < 0)
					document = await GetLocalDocumentAsync(documentId.Guid);
				else // get a special revision
					document = await GetServerDocumentAsync(documentId);

				// load document
				if (document == null)
					throw new ArgumentException($"Failed to load {documentId}.");

				// load document
				await document.OnLoadedAsync(arguments);

				document.UndoManager.Clear();
			}

			return document;
		} // func OpenDocumentAsync

		private string GetDefaultPaneFromDictionary(string schema)
		{
			string paneUri;
			lock (defaultPanes)
				return defaultPanes.TryGetValue(schema, out paneUri) ? paneUri : null;
		} // func GetDocumentDefaultPaneAsync

		public Task<string> GetDocumentDefaultPaneAsync(string schema)
		{
			// try to get the uri from the pane list
			var paneUri = GetDefaultPaneFromDictionary(schema);
			if (!String.IsNullOrEmpty(paneUri))
				return Task.FromResult<string>(paneUri);

			// read the schema meta data
			return ActiveDataSets.GetDataSetDefinition(schema)
				.ContinueWith(t => t.Result == null ? null : t.Result.Meta.GetProperty<string>(PpsDataSetMetaData.DefaultPaneUri, null));
		} // func GetDocumentDefaultPaneAsync

		[LuaMember(nameof(BeginLocalStoreTransaction))]
		public IPpsLocalStoreTransaction BeginLocalStoreTransaction()
			=> new PpsLocalStoreTransaction(this);
	} // class PpsMainEnvironment

	#endregion
}
