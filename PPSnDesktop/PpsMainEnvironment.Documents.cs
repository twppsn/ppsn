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
				var xResult = environment.Request.CheckForExceptionResult(XDocument.Load(xmlAnswer).Root);
				
				newServerId = xResult.GetAttribute("id", -1L);
				newRevId = xResult.GetAttribute("revId", -1L);
				var pullRequest = xResult.GetAttribute("pullRequest", false);
				if (pullRequest)
					throw new ArgumentException("todo: Pull before push");
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
				var objectInfo = GetObject(guid, trans);
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
				var objectInfo = GetObject(documentId.Guid, trans);
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
	} // class PpsMainEnvironment

	#endregion
}
