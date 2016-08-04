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
	#region -- class PpsDocumentId ------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PpsDocumentId : IEquatable<PpsDocumentId>
	{
		private readonly Guid objectGuid;
		private readonly long revisionId;

		public PpsDocumentId(Guid objectGuid, long revisionId)
		{
			this.objectGuid = objectGuid;
			this.revisionId = revisionId;
		} // ctor

		public override string ToString()
			=> $"DocId: {objectGuid:D}@{revisionId:N0}";

		public override int GetHashCode()
			=> objectGuid.GetHashCode() ^ revisionId.GetHashCode();

		public override bool Equals(object obj)
			=> Object.ReferenceEquals(this, obj) ? true : Equals(obj as PpsDocumentId);

		public bool Equals(PpsDocumentId other)
			=> other != null && other.objectGuid == this.objectGuid && other.revisionId == this.revisionId;

		/// <summary>Client site Id, not the server id.</summary>
		public Guid ObjectGuid => objectGuid;
		/// <summary>Pulled revision</summary>
		public long RevisionId => revisionId;
		
		public bool IsEmpty => objectGuid == Guid.Empty && revisionId <= 0;

		public static PpsDocumentId Empty { get; } = new PpsDocumentId(Guid.Empty, 0);
	} // class PpsDocumentId

	#endregion

	#region -- interface IPpsDocumentOwner ----------------------------------------------

	public interface IPpsDocumentOwner
	{
		LuaTable DocumentEvents { get; }
	} // interface IPpsDocumentOwner

	#endregion

	#region -- interface IPpsDocuments --------------------------------------------------

	public interface IPpsDocuments
	{
		Task<PpsDocument> CreateDocumentAsync(IPpsDocumentOwner owner, string type, LuaTable arguments);
		/// <summary></summary>
		/// <param name="owner"></param>
		/// <param name="documentId"></param>
		/// <param name="revisionId"></param>
		/// <param name="arguments"></param>
		/// <returns></returns>
		Task<PpsDocument> OpenDocumentAsync(IPpsDocumentOwner owner, PpsDocumentId documentId, LuaTable arguments);

		string GetDocumentDefaultPane(string type);
	} // interface IPpsDocuments

	#endregion

	#region -- class PpsDocumentDefinition ----------------------------------------------

	public sealed class PpsDocumentDefinition : PpsDataSetDefinitionClient
	{
		private readonly PpsMainEnvironment environment;

		public PpsDocumentDefinition(PpsMainEnvironment environment, string type, XElement xSchema) 
			: base(environment, type, xSchema)
		{
			this.environment = environment;
		} // ctor

		public override PpsDataSet CreateDataSet()
			=> new PpsDocument(this, environment);
	} // class PpsDocumentDefinition


	#endregion

	#region -- class PpsDocument --------------------------------------------------------

	public sealed class PpsDocument : PpsDataSetClient
	{
		private readonly PpsMainEnvironment environment;
		private readonly PpsUndoManager undoManager;

		private PpsDocumentId documentId = PpsDocumentId.Empty;	// document id
		private long localId = -2;        // local Id in the cache (-2 for not setted)
		private long pulledRevisionId;
		private bool isDocumentChanged = false;		// is this document changed to the pulled version
		private bool isReadonly = true;           // is this document a special read-only revision
		private bool isDirty = false;							// is this document changed since the last dump

		private readonly object documentOwnerLock = new object();
		private readonly List<IPpsDocumentOwner> documentOwner = new List<IPpsDocumentOwner>(); // list with document owners

		#region -- Ctor/Dtor --------------------------------------------------------------

		public PpsDocument(PpsDataSetDefinition datasetDefinition, PpsMainEnvironment environment)
			: base(datasetDefinition, environment)
		{
			this.environment = environment;

			this.undoManager = new PpsUndoManager();
			RegisterUndoSink(undoManager);
		} // ctor

		public void SetLocalState(Guid objectId, long localId, long pulledRevisionId, bool isDocumentChanged, bool isReadonly)
		{
			if (localId < 0)
				localId = -1;

			this.documentId = new PpsDocumentId(objectId, isReadonly ? pulledRevisionId : -1);
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

		public void RegisterOwner(IPpsDocumentOwner owner)
		{
			lock (documentOwnerLock)
			{
				if (documentOwner.IndexOf(owner) >= 0)
					throw new InvalidOperationException("Already registered.");

				if (documentOwner.Count == 0)
					environment.OnDocumentOpend(this);

				RegisterEventSink(owner.DocumentEvents);
				documentOwner.Add(owner);
			}
		} // proc RegisterOwner

		public void UnregisterOwner(IPpsDocumentOwner owner)
		{
			lock (documentOwnerLock)
			{
				var index = documentOwner.IndexOf(owner);
				if (index == -1)
					throw new InvalidOperationException("Owner not registered.");

				documentOwner.RemoveAt(index);
				UnregisterEventSink(owner.DocumentEvents);

				if (documentOwner.Count == 0)
					environment.OnDocumentClosed(this);
			}
		} // proc Unregister

		#endregion

		private void SetDirty()
		{
			isDirty = true;
		} // proc SetDirty

		public void ResetDirty()
		{
			isDirty = false;
		} // proc ResetDirty

		protected override void OnTableRowAdded(PpsDataTable table, PpsDataRow row)
		{
			base.OnTableRowAdded(table, row);
			SetDirty();
		} // proc OnTableRowAdded

		protected override void OnTableRowDeleted(PpsDataTable table, PpsDataRow row)
		{
			base.OnTableRowDeleted(table, row);
			SetDirty();
		} // proc OnTableRowDeleted

		protected override void OnTableColumnValueChanged(PpsDataRow row, int iColumnIndex, object oldValue, object value)
		{
			base.OnTableColumnValueChanged(row, iColumnIndex, oldValue, value);
			SetDirty();
		} // proc OnTableColumnValueChanged

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
			var head = Tables["Head", true];
			var documentType = head.First["Typ"];

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
			}

			// pull the document again, and update the local store

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
		public bool IsLoaded => documentId != PpsDocumentId.Empty;
		public bool IsDirty => isDirty;

		public PpsUndoManager UndoManager => undoManager;

		public PpsDocumentId DocumentId => documentId;
	} // class PpsDocument

	#endregion

	#region -- class PpsMainEnvironment -------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public partial class PpsMainEnvironment : IPpsDocuments
	{
		#region -- class KnownDocumentDefinition ------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class KnownDocumentDefinition
		{
			private readonly PpsMainEnvironment environment;

			private readonly string typ;
			private readonly string sourceUri;
			private readonly string paneUri;

			private PpsDocumentDefinition definition = null;

			public KnownDocumentDefinition(PpsMainEnvironment environment, string typ, string sourceUri, string paneUri)
			{
				this.environment = environment;
				this.typ = typ;
				this.sourceUri = sourceUri;
				this.paneUri = paneUri;
			} // ctor

			public async Task<PpsDocumentDefinition> GetDocumentDefinitionAsync()
			{
				if (definition != null)
					return definition;

				// load the schema
				definition = new PpsDocumentDefinition(environment, typ, await environment.Request.GetXmlAsync(sourceUri));
				definition.EndInit();
				return definition;
			} // func GetDocumentDefinitionAsync

			public string Typ => typ;
			public string Schema => sourceUri;
			public string Pane => paneUri;
		} // class KnownDocumentDefinition

		#endregion

		private Dictionary<PpsDocumentId, PpsDocument> openDocuments = new Dictionary<PpsDocumentId, PpsDocument>(); // current active documents
		private Dictionary<string, KnownDocumentDefinition> documentDefinitions = new Dictionary<string, KnownDocumentDefinition>(StringComparer.OrdinalIgnoreCase);

		private void UpdateDocumentDefinitionInfo(XElement x, List<string> updatedDocuments)
		{
			var typ = x.GetAttribute("name", String.Empty);
			var schemaUri = x.GetAttribute("source", String.Empty);
			var paneUri = x.GetAttribute("pane", String.Empty);
			if (String.IsNullOrEmpty(typ))
				return;

			if (String.IsNullOrEmpty(schemaUri))
				throw new ArgumentNullException("@source");

			KnownDocumentDefinition cur;
			if (!documentDefinitions.TryGetValue(typ, out cur) || cur.Schema != schemaUri || cur.Pane != paneUri)
				documentDefinitions[typ] = new KnownDocumentDefinition(this, typ, schemaUri, paneUri);

			updatedDocuments.Add(typ);
		} // proc UpdateDocumentDefinitionInfo

		private void ClearDocumentDefinitionInfo(List<string> updatedDocuments)
		{
			foreach(var k in documentDefinitions.Keys.ToArray())
			{
				if (updatedDocuments.IndexOf(k) == -1)
					documentDefinitions.Remove(k);
			}
		} // proc ClearDocumentDefinitionInfo

		internal void OnDocumentClosed(PpsDocument document)
		{
			openDocuments.Add(document.DocumentId, document);
		}

		internal void OnDocumentOpend(PpsDocument document)
		{
			openDocuments.Remove(document.DocumentId);
		}

		private async Task<PpsDataSetDefinitionClient> GetDocumentDefinitionAsync(string typ)
		{
			KnownDocumentDefinition def;

			// get the known document definition
			if (documentDefinitions.TryGetValue(typ, out def))
				return await def.GetDocumentDefinitionAsync();
			else
				throw new NotImplementedException("todo"); // todo: exception for unknown document
		} // func GetDocumentDefinitionAsync

		public async Task<PpsDocument> CreateDocumentAsync(IPpsDocumentOwner owner, string typ, LuaTable arguments)
		{
			var def = await GetDocumentDefinitionAsync(typ);

			// create the empty dataset
			var newDocument = (PpsDocument)def.CreateDataSet();

			// initialize
			newDocument.SetLocalState(Guid.NewGuid(), -1, -1, false, false);
			await newDocument.OnNewAsync(arguments);

			// register events, owner, and in the openDocuments dictionary
			newDocument.RegisterOwner(owner);
			
			return newDocument;
		} // func CreateDocument
		
		// the return is a uninitialized document
		// SetLocalState, OnLoadAsync is not called, yet
		private async Task<Tuple< PpsDocument,long>> PullDocumentCoreAsync(string documentType, long serverId, long revisionId = -1)
		{
			if (serverId <= 0)
				throw new ArgumentOutOfRangeException("serverId", "Invalid server id.");

			var def = await GetDocumentDefinitionAsync(documentType);
			var xRoot = await Request.GetXmlAsync($"{documentType}/?action=pull&id={serverId}&rev={revisionId}");
			
			var newDocument = (PpsDocument)def.CreateDataSet();
			newDocument.Read(xRoot);

			var pulledRevisionId = newDocument.Tables["Head", true].First.GetProperty("HeadRevId", -1L);
			if (pulledRevisionId < 0)
				throw new ArgumentOutOfRangeException("pulledRevisionId");

			return new Tuple<PPSn.PpsDocument, long>(newDocument, pulledRevisionId);
		} // func PullDocumentCoreAsync

		private async Task<PpsDocument> GetLocalDocumentAsync(Guid guid)
		{
			long localId;
			long serverId;
			string documentType;
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
							documentType = r.GetString(2);
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
					var newDocument = await PullDocumentCoreAsync(documentType, serverId);

					// update document data in the local store
					using (var cmd = LocalConnection.CreateCommand())
					{
						cmd.Transaction = trans;
						cmd.CommandText = "update main.Objects set Document = @Data where Id = @Id;";

						cmd.Parameters.Add("@Id", DbType.Int64).Value = localId;
						cmd.Parameters.Add("@Data", DbType.String).Value = newDocument.Item1;

						cmd.ExecuteNonQuery();
					}

					trans.Commit();

					// init document
					newDocument.Item1.SetLocalState(guid, localId, newDocument.Item2, false, false);

					return newDocument.Item1;
				}
				else // synced, get the current staging
				{
					trans.Rollback();

					var newDocument = (PpsDocument)(await GetDocumentDefinitionAsync(documentType)).CreateDataSet();

					using (var docTrans = newDocument.UndoSink.BeginTransaction("Internal Read"))
					{
						newDocument.Read(XElement.Parse(documentData));
						docTrans.Commit();
					}
					newDocument.UndoManager.Clear();

					newDocument.SetLocalState(guid, localId, pulledRevId, isDocumentChanged, false); // synced and change able
					return newDocument;
				}
			}
		} // func GetLocalDocumentAsync

		private async Task<PpsDocument> GetServerDocumentAsync(PpsDocumentId documentId)
		{
			long serverId;
			string documentType;

			// get database info
			using (var cmd = LocalConnection.CreateCommand())
			{
				cmd.CommandText = "select o.ServerId, o.Typ from main.Objects o where o.Guid = @Guid";
				cmd.Parameters.Add("@Guid", DbType.Guid).Value = documentId.ObjectGuid;

				using (var r = cmd.ExecuteReader(CommandBehavior.SingleRow))
				{
					if (r.Read())
					{
						if (r.IsDBNull(0))
							throw new ArgumentOutOfRangeException("no server object."); // todo:
						else
						{
							serverId = r.GetInt64(0);
							documentType = r.GetString(1);
						}
					}
					else
						throw new InvalidOperationException("unknown local revision"); // todo:
				}
			}
			
			// load from server
			var newDocument = await PullDocumentCoreAsync(documentType, serverId, documentId.RevisionId);
			if (newDocument.Item2 != documentId.RevisionId)
				throw new ArgumentException("rev requested != rev pulled"); // todo:

			// initialize
			newDocument.Item1.SetLocalState(documentId.ObjectGuid, -1, documentId.RevisionId, false, true);
			return newDocument.Item1;
		} // func GetServerDocumentAsync

		public async Task<PpsDocument> OpenDocumentAsync(IPpsDocumentOwner owner, PpsDocumentId documentId, LuaTable arguments)
		{
			PpsDocument document;
			
			// check if the document is already opened
			if (!openDocuments.TryGetValue(documentId, out document))
			{
				// is a request for the head revision
				if (documentId.RevisionId < 0)
					document = await GetLocalDocumentAsync(documentId.ObjectGuid);
				else // get a special revision
					document = await GetServerDocumentAsync(documentId);

				// load document
				if (document == null)
					throw new ArgumentException("todo: open failed"); // todo:

				// load document
				await document.OnLoadedAsync(arguments);
			}

			// register the document -> openDocuments get filled
			document.RegisterOwner(owner);

			return document;
		} // func OpenDocument

		public string GetDocumentDefaultPane(string type)
		{
			KnownDocumentDefinition def;
			if (documentDefinitions.TryGetValue(type, out def))
				return def.Pane;
			else
				return null;
		} // func GetDocumentDefaultPane
	} // class PpsMainEnvironment

	#endregion
}
