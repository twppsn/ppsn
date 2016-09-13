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
			using (var trans = environment.LocalConnection.BeginTransaction())
			{
				environment.UpdateLocalDocumentState(trans, localId, this, newServerId, newRevId, Tables["Head", true].First["Nr"].ToString());
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
			using (var trans = environment.LocalConnection.BeginTransaction())
			{
				// update object data
				using (var cmd = environment.LocalConnection.CreateCommand())
				{
					cmd.CommandText = localId <= 0 ?
						"insert into main.[Objects] (Guid, Typ, Document, DocumentIsChanged) values (@Guid, @Typ, @Document, 1);" :
						"update main.[Objects] set Document = @Document, DocumentIsChanged = 1 where Id = @Id";

					if (localId > 0) // for update
					{
						cmd.Parameters.Add("@Id", DbType.Int64).Value = localId;
					}
					else // for insert
					{
						cmd.Parameters.Add("@Guid", DbType.Guid).Value = head.First["Guid"];
						cmd.Parameters.Add("@Typ", DbType.String).Value = head.First["Typ"];
					}

					// update the document data
					cmd.Parameters.Add("@Document", DbType.String).Value = data.ToString();

					// exec
					cmd.ExecuteNonQuery();

					// update localId
					if (localId <= 0)
						localId = environment.LocalConnection.LastInsertRowId;
				}

				// update meta tags
				using (var updateTags = new TagDatabaseCommands(environment.LocalConnection))
				{
					updateTags.Transaction = trans;
					updateTags.ObjectId = localId;
					updateTags.UpdateTags(GetAutoTags().ToArray());
				}

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

			return new Tuple<PpsDocument, long, string>(newDocument, pulledRevisionId, headRow["Nr"].ToString());
		} // func PullDocumentCoreAsync

		private async Task<PpsDocument> GetLocalDocumentAsync(Guid guid)
		{
			long localId;
			long serverId;
			string documentSchema;
			string documentData;
			bool isDocumentChanged;
			long pulledRevId;

			using (var trans = LocalConnection.BeginTransaction())
			{
				// get database info
				using (var cmd = LocalConnection.CreateCommand())
				{
					cmd.CommandText = "select o.Id, o.ServerId, o.Typ, o.Document, o.DocumentIsChanged, o.PulledRevId from main.Objects o where o.Guid = @Guid";
					cmd.Transaction = trans;
					cmd.Parameters.Add("@Guid", DbType.Guid).Value = guid;

					using (var r = cmd.ExecuteReader(CommandBehavior.SingleRow))
					{
						if (r.Read())
						{
							localId = r.GetInt64(0);
							serverId = r.IsDBNull(1) ? -1 : r.GetInt64(1);
							documentSchema = r.GetString(2);
							documentData = r.IsDBNull(3) ? null : r.GetString(3);
							isDocumentChanged = r.IsDBNull(4) ? false : r.GetBoolean(4);
							pulledRevId = r.IsDBNull(5) ? -1 : r.GetInt32(5);
						}
						else
						{
							throw new InvalidOperationException("unknown local revision"); // todo:
						}
					}
				}

				if (documentData == null) // not synced yet, pull the head revision
				{
					var newDocument = await PullDocumentCoreAsync(documentSchema, guid, serverId);

					// update document data in the local store
					UpdateLocalDocumentState(trans, localId, newDocument.Item1, serverId, newDocument.Item2, newDocument.Item3);

					trans.Commit();

					// init document
					newDocument.Item1.SetLocalState(localId, newDocument.Item2, false, false);

					return newDocument.Item1;
				}
				else // synced, get the current staging
				{
					trans.Rollback();
					var newDocument = await CreateDocumentInternalAsync(documentSchema, new PpsDataSetId(guid, -1));

					using (var docTrans = newDocument.UndoSink.BeginTransaction("Internal Read"))
					{
						newDocument.Read(XElement.Parse(documentData));
						docTrans.Commit();
					}
					newDocument.UndoManager.Clear();

					newDocument.SetLocalState(localId, pulledRevId, isDocumentChanged, false); // synced and change able
					return newDocument;
				}
			}
		} // func GetLocalDocumentAsync

		internal void UpdateLocalDocumentState(SQLiteTransaction trans, long localId, PpsDocument document, long serverId, long pulledRevId, string nr)
		{
			using (var cmd = LocalConnection.CreateCommand())
			{
				cmd.Transaction = trans;
				cmd.CommandText = "update main.Objects set ServerId = @ServerId, Document = @Data, PulledRevId = @RevId, Nr = @Nr where Id = @Id;";

				cmd.Parameters.Add("@Id", DbType.Int64).Value = localId;
				cmd.Parameters.Add("@ServerId", DbType.Int64).Value = serverId;
				cmd.Parameters.Add("@Nr", DbType.String).Value = nr;
				cmd.Parameters.Add("@Data", DbType.String).Value = document.GetAsString();
				cmd.Parameters.Add("@RevId", DbType.Int64).Value = pulledRevId;

				cmd.ExecuteNonQuery();

				using (var tags = new TagDatabaseCommands(LocalConnection))
				{
					tags.ObjectId = localId;
					tags.Transaction = trans;

					tags.UpdateTags(document.GetAutoTags().ToArray());
				}
			}
		} // proc UpdateLocalState

		private async Task<PpsDocument> GetServerDocumentAsync(PpsDataSetId documentId)
		{
			long serverId;
			string documentSchema;

			// get database info
			using (var cmd = LocalConnection.CreateCommand())
			{
				cmd.CommandText = "select o.ServerId, o.Typ from main.Objects o where o.Guid = @Guid";
				cmd.Parameters.Add("@Guid", DbType.Guid).Value = documentId.Guid;

				using (var r = cmd.ExecuteReader(CommandBehavior.SingleRow))
				{
					if (r.Read())
					{
						if (r.IsDBNull(0))
							throw new ArgumentOutOfRangeException("no server object."); // todo:
						else
						{
							serverId = r.GetInt64(0);
							documentSchema = r.GetString(1);
						}
					}
					else
						throw new InvalidOperationException("unknown local revision"); // todo:
				}
			}

			// load from server
			var newDocument = await PullDocumentCoreAsync(documentSchema, documentId.Guid, serverId, documentId.Index);
			if (newDocument.Item2 != documentId.Index)
				throw new ArgumentException("rev requested != rev pulled"); // todo:

			// initialize
			newDocument.Item1.SetLocalState(-1, documentId.Index, false, true);
			return newDocument.Item1;
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
	} // class PpsMainEnvironment

	#endregion
}
