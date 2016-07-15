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
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;

namespace TecWare.PPSn
{
	#region -- class PpsDocumentId ------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PpsDocumentId : IEquatable<PpsDocumentId>
	{
		private readonly long objectId;
		private readonly long parentRevisionId;

		public PpsDocumentId(long objectId, long parentRevisionId)
		{
			this.objectId = objectId;
			this.parentRevisionId = parentRevisionId;
		} // ctor

		public override string ToString()
			=> $"DocId: {objectId:N0}@{parentRevisionId:N0}";

		public override int GetHashCode()
			=> objectId.GetHashCode() ^ parentRevisionId.GetHashCode();

		public override bool Equals(object obj)
			=> Object.ReferenceEquals(this, obj) ? true : Equals(obj as PpsDocumentId);

		public bool Equals(PpsDocumentId other)
			=> other != null && other.objectId == this.objectId && other.parentRevisionId == this.parentRevisionId;

		/// <summary>Client site Id, not the server id.</summary>
		public long ObjectId => objectId;
		/// <summary>Pulled revision</summary>
		public long ParentRevisionId => parentRevisionId;

		public bool IsEmpty => objectId <= 0 && parentRevisionId <= 0;

		public static PpsDocumentId Empty { get; } = new PpsDocumentId(0, 0);
	} // class PpsDocumentId

	#endregion

	#region -- interface IPpsDocuments --------------------------------------------------

	public interface IPpsDocuments
	{
		Task<PpsDocument> CreateDocumentAsync(string type, LuaTable arguments);
		Task<PpsDocument> OpenDocumentAsync(PpsDocumentId documentId, LuaTable arguments);
		Task<PpsDocument> OpenDocumentAsync(long localId, LuaTable arguments);

		string GetDocumentDefaultPane(string type);
	} // interface IPpsDocuments

	#endregion

	#region -- class PpsDocumentDefinition ----------------------------------------------

	public sealed class PpsDocumentDefinition : PpsDataSetDefinitionClient
	{
		private readonly PpsMainEnvironment environment;
		//private readonly PpsUndoManager undoManager; -> null bis lösung

		public PpsDocumentDefinition(PpsMainEnvironment environment, string type, XElement xSchema) 
			: base(environment, type, xSchema)
		{
			this.environment = environment;
		} // ctor

		public override PpsDataSetClient CreateDataSet(LuaTable arguments)
			=> new PpsDocument(this, environment, arguments); 
	} // class PpsDocumentDefinition


	#endregion

	#region -- class PpsDocument --------------------------------------------------------

	public sealed class PpsDocument : PpsDataSetClient
	{
		private readonly PpsMainEnvironment environment;
		//private PpsUndoManager undoManager;
		private long localId = -1;

		public PpsDocument(PpsDataSetDefinition datasetDefinition, PpsMainEnvironment environment, LuaTable arguments)
			: base(datasetDefinition, environment, arguments)
		{
			this.environment = environment;
		} // ctor
		
		public void CommitWork()
		{
			var head = Tables["Head", true];
			var data = new StringBuilder();

			// convert data to a string
			using (var dst = new StringWriter(data))
			using (var xml = XmlWriter.Create(dst, new XmlWriterSettings() { }))
				Write(xml);

			// update local store
			using (var trans = environment.LocalConnection.BeginTransaction())
			{
				// update object data
				using (var cmd = environment.LocalConnection.CreateCommand())
				{
					cmd.CommandText = localId <= 0 ?
						"insert into main.[Objects] (Guid, Typ, Document, DocumentIsChanged) values (@Guid, @Typ, @Document, 1);" :
						"update main.[Objects] set Document = @Document, DocumentIsChanged = 1where Id = @Id";

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
				// todo:

				trans.Commit();
			}
		} // proc Commit
	} // class PpsDocument

	#endregion

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

		private async Task<PpsDataSetDefinitionClient> GetDocumentDefinitionAsync(string typ)
		{
			KnownDocumentDefinition def;

			// get the known document definition
			if (documentDefinitions.TryGetValue(typ, out def))
				return await def.GetDocumentDefinitionAsync();
			else
				throw new NotImplementedException("todo"); // todo: exception for unknown document
		} // func GetDocumentDefinitionAsync

		public async Task<PpsDocument> CreateDocumentAsync(string typ, LuaTable arguments)
		{
			var def = await GetDocumentDefinitionAsync(typ);

			// create the empty dataset
			var newDocument = (PpsDocument)def.CreateDataSet(arguments);
			
			// initialize
			await newDocument.OnNewAsync();
			
			return newDocument;
		} // func CreateDocument

		public Task<PpsDocument> OpenDocumentAsync(long objectId, long revId = -1, LuaTable arguments = null)
			=> OpenDocumentAsync(new PpsDocumentId(objectId, revId), arguments);

		private async Task<PpsDocument> LoadDocumentAsync(LuaTable arguments, string type, string data)
		{
			var def = await GetDocumentDefinitionAsync(type);

			// load content
			var newDocument = (PpsDocument)def.CreateDataSet(arguments);
			newDocument.Read(XElement.Parse(data));

			// notify
			await newDocument.OnLoadedAsync();

			return newDocument;
		} // func LoadDocumentAsync

		public async Task<PpsDocument> OpenDocumentAsync(long localId, LuaTable arguments)
		{
			// todo: open only once?

			using (var cmd = LocalConnection.CreateCommand())
			{
				cmd.CommandText = "select [Typ], [Document] from main.[Objects] where Id = @Id";
				cmd.Parameters.Add("@Id", DbType.Int64).Value = localId;

				using (var r = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow))
				{
					if (r.Read())
						return await LoadDocumentAsync(arguments, r.GetString(0), r.GetString(1));
					else
						throw new ArgumentException("localId failed."); // todo:
				}
			}
		} // func OpenDocumentAsync

		public Task<PpsDocument> OpenDocumentAsync(PpsDocumentId documentId, LuaTable arguments)
		{
			throw new NotImplementedException();
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
}
