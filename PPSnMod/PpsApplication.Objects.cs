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
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Data;
using TecWare.DE.Networking;
using TecWare.DE.Server;
using TecWare.DE.Server.Http;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;
using TecWare.PPSn.Server.Data;

namespace TecWare.PPSn.Server
{
	#region -- class PpsObjectTagAccess -------------------------------------------------

	/// <summary>Tag access class</summary>
	public sealed class PpsObjectTagAccess
	{
		private readonly PpsObjectAccess obj;
		private long id;
		private readonly bool isRev;
		private readonly string key;
		private readonly int tagClass;
		private string value;
		private readonly long userId;
		private readonly DateTime createDate;

		private bool isRemoved = false;
		private bool isDirty = false;

		internal PpsObjectTagAccess(PpsObjectAccess obj, long id, bool isRev, int tagClass, string key, string value, long userId, DateTime createDate)
		{
			this.obj = obj;
			this.id = id;
			this.isRev = isRev;
			this.tagClass = tagClass;
			this.key = key ?? throw new ArgumentNullException(nameof(key));
			this.value = value;
			this.userId = userId;
			this.createDate = createDate;

			Reset();
		} // ctor

		internal PpsObjectTagAccess(PpsObjectAccess obj, IDataRow row)
			: this(obj, row.GetProperty("Id", -1L), row.GetProperty("ObjRId", -1L) > 0, row.GetProperty("Class", 0), row.GetProperty("Key", String.Empty), row.GetProperty("Value", String.Empty), row.GetProperty("UserId", -1L), row.GetProperty("CreateDate", DateTime.UtcNow))
		{
		} // ctor

		/// <summary>Reset the dirty flag.</summary>
		public void Reset()
			=> isDirty = id < 0;

		/// <summary>Marks the tag as removed</summary>
		public void Remove()
		{
			isRemoved = true;
			isDirty = true;
		} // proc Remove

		/// <summary>Generate Xml representation of the tag.</summary>
		/// <param name="elementName"></param>
		/// <returns></returns>
		public XElement ToXml(string elementName)
			=> new XElement(elementName,
				new XAttribute("tagClass", tagClass),
				new XAttribute("key", key),
				new XAttribute("value", value),
				new XAttribute("userId", userId)
			);

		/// <summary>Current Id of the tag</summary>
		public long Id { get { return id; } internal set { id = value; } }

		/// <summary>Object Id</summary>
		public long ObjectId => obj.Id;
		/// <summary>Tag class</summary>
		public int TagClass => tagClass;
		/// <summary>Name of the tag</summary>
		public string Key => key;

		/// <summary>Value of the tag.</summary>
		public string Value
		{
			get => value;
			set
			{
				if (this.value != value)
				{
					this.value = value;
					isDirty = true;
				}
				isRemoved = false;
			}
		} // prop Value

		/// <summary>Owner of the tag.</summary>
		public long UserId => userId;

		/// <summary>Is the tag new.</summary>
		public bool IsNew => id < 0;
		/// <summary>Is this attribute attached to a revision.</summary>
		public bool IsRev => isRev;

		/// <summary>Is the tag dirty</summary>
		public bool IsDirty => isDirty;
		/// <summary>Is the tag removed.</summary>
		public bool IsRemoved => isRemoved;
	} // class PpsObjectTagAccess

	#endregion

	#region -- class PpsObjectLinkAccess ------------------------------------------------

	public sealed class PpsObjectLinkAccess
	{
		private readonly PpsObjectAccess obj;
		private long id;
		private readonly long linkId;
		private int refCount;

		private bool isRemoved = false;
		private bool isDirty = false;

		internal PpsObjectLinkAccess(PpsObjectAccess obj, long id, long linkToId, int refCount)
		{
			this.obj = obj;
			this.id = id;
			this.linkId = linkToId;
			this.refCount = refCount;

			Reset();
		} // ctor

		internal PpsObjectLinkAccess(PpsObjectAccess obj, IDataRow row)
			: this(obj, row.GetProperty("Id", -1L), row.GetProperty("LinkObjKId", -1L), row.GetProperty("RefCount", 0))
		{
		} // ctor

		public void Reset()
			=> isDirty = id < 0;

		public void Remove()
		{
			isDirty = true;
			isRemoved = true;
		} // proc Remove

		public XElement ToXml(string elementName)
			=> new XElement(elementName,
				new XAttribute("linkId", linkId),
				new XAttribute("refCount", refCount)
			);

		/// <summary>Id of the lin</summary>
		public long Id { get => id; internal set => id = value; }
		/// <summary>Object, the current object is linked.</summary>
		public long LinkId => linkId;

		/// <summary>Reference counter for this link.</summary>
		public int RefCount
		{
			get => refCount;
			set
			{
				if (refCount != value)
				{
					refCount = value;
					isDirty = true;
				}
				isRemoved = false;
			}
		} // prop RefCount

		/// <summary>Is this link new</summary>
		public bool IsNew => id < 0;

		/// <summary>Is the link dirty</summary>
		public bool IsDirty => isDirty;
		/// <summary>Is the link removed.</summary>
		public bool IsRemoved => isRemoved;
	} // class PpsObjectLinkAccess

	#endregion

	#region -- enum PpsObjectUpdateFlag -------------------------------------------------

	[Flags]
	public enum PpsObjectUpdateFlag
	{
		/// <summary>Updates only the object record.</summary>
		None = 0,
		/// <summary>Updates the tag list.</summary>
		Tags = 1,
		/// <summary>Updates the link list.</summary>
		Links = 2,

		/// <summary>Update links and tags.</summary>
		All = Tags | Links
	} // enum PpsObjectUpdateFlag

	#endregion

	#region -- class PpsObjectAccess ----------------------------------------------------

	/// <summary>This class defines a interface to access pps-object model. Some properties are late bound. So, wait for closing the transaction.</summary>
	public sealed class PpsObjectAccess : LuaTable
	{
		private readonly PpsApplication application;
		private readonly IPropertyReadOnlyDictionary defaultData; // default data for new objects
		private long objectId;  // object id

		private bool? isRev;    // is this a rev, object (null for not loaded)
		private long revId;     // current visible rev id

		private List<PpsObjectLinkAccess> linksTo = null;   // cache list for the links
		private List<PpsObjectLinkAccess> linksFrom = null; // cache list for the links (from, they are not changable)
		private List<PpsObjectTagAccess> tags = null;       // cache list for the tags

		#region -- Ctor/Dtor ----------------------------------------------------------

		internal PpsObjectAccess(PpsApplication application, IPropertyReadOnlyDictionary defaultData)
		{
			this.application = application;
			this.defaultData = defaultData;

			this.objectId = defaultData.GetProperty(nameof(Id), -1L);
			this.revId = defaultData.GetProperty(nameof(RevId), defaultData.GetProperty(nameof(HeadRevId), -1L));
			this.isRev = defaultData.TryGetProperty<bool>(nameof(IsRev), out var t) ? (bool?)t : null;
		} // ctor

		/// <summary></summary>
		/// <param name="key"></param>
		/// <returns></returns>
		protected override object OnIndex(object key)
			=> key is string k && defaultData.TryGetProperty(k, out var t) ? t : base.OnIndex(key);

		#endregion

		#region -- Revision management ------------------------------------------------
		
		private void Reset(PpsObjectUpdateFlag refresh = PpsObjectUpdateFlag.All)
		{
			// reload links
			if ((refresh & PpsObjectUpdateFlag.Links) == PpsObjectUpdateFlag.Links)
			{
				linksTo = null;
				linksFrom = null;
			}
			// reload tags
			if ((refresh & PpsObjectUpdateFlag.Tags) == PpsObjectUpdateFlag.Tags)
				tags = null;
		} // proc Reset

		private void CheckRevision()
		{
			if (isRev.HasValue
				|| IsNew)
				return;

			// get head rev.
			var cmd = new LuaTable
			{
				{ "select", "dbo.ObjK" },
				{ "selectList", new LuaTable { nameof(IsRev), nameof(HeadRevId), } },
				new LuaTable
				{
					{ "Id", objectId }
				}
			};

			var r = application.Database.Main.ExecuteSingleRow(cmd);

			isRev = (bool)r[nameof(IsRev), true];
			revId = (long)(r[nameof(HeadRevId), true] ?? -1);
		} // proc CheckRevision

		/// <summary>Change current revision.</summary>
		/// <param name="newRevId"></param>
		/// <param name="refresh"></param>
		[LuaMember]
		public void SetRevision(long newRevId, PpsObjectUpdateFlag refresh = PpsObjectUpdateFlag.All)
		{
			CheckRevision();
			if (revId == newRevId)
				return;

			revId = newRevId;
			Reset(refresh);
		} // proc SetRevision

		#endregion

		#region -- Update Object ------------------------------------------------------

		private LuaTable GetObjectArguments(bool forInsert)
		{
			var args = new LuaTable
			{
				{ nameof(Nr), Nr }
			};

			if (forInsert)
			{
				var guid = Guid;
				if (guid == Guid.Empty)
					guid = Guid.NewGuid();
				args[nameof(Guid)] = guid;
				args[nameof(Typ)] = Typ;
				args[nameof(IsRev)] = IsRev;
			}
			else
				args[nameof(Id)] = objectId;

			if (MimeType != null)
				args[nameof(MimeType)] = MimeType;

			if (CurRevId > 0)
				args[nameof(CurRevId)] = CurRevId;
			if (HeadRevId > 0)
				args[nameof(HeadRevId)] = HeadRevId;

			return args;
		} // func GetObjectArguments

		/// <summary>Persist object structure in the database.</summary>
		/// <param name="flags"></param>
		[LuaMember]
		public void Update(PpsObjectUpdateFlag flags = PpsObjectUpdateFlag.All)
		{
			LuaTable cmd;
			var trans = application.Database.Main;

			// prepare stmt
			if (objectId > 0) // exists an id for the object
			{
				cmd = new LuaTable
				{
					{ "update", "dbo.ObjK" },
					GetObjectArguments(false)
				};
			}
			else if (Guid == Guid.Empty) // does not exist
			{
				cmd = new LuaTable
				{
					{ "insert", "dbo.ObjK" },
					GetObjectArguments(true)
				};
			}
			else // upsert over guid or id
			{
				var args = GetObjectArguments(IsNew);

				cmd = new LuaTable
				{
					{ "upsert", "dbo.ObjK"},
					args
				};

				if (IsNew)
				{
					args[nameof(Guid)] = Guid;
					cmd.Add("on", new LuaTable { nameof(Guid) });
				}
			}

			trans.ExecuteNoneResult(cmd);

			// update values
			if (IsNew)
			{
				var args = (LuaTable)cmd[1];
				objectId = (long)args[nameof(Id)];
				this[nameof(Guid)] = args[nameof(Guid)];
			}

			// process tags
			if ((flags & PpsObjectUpdateFlag.Tags) == PpsObjectUpdateFlag.Tags)
			{
				CheckRevision(); // referesh current rev.

				// tags changed?
				if (tags != null)
				{
					#region -- prepare structures --
					var deleteCmd = new LuaTable
					{
						{ "delete", "dbo.ObjT" },
						new LuaTable { { "Id", null } }
					};
					var upsertCmd = new LuaTable
					{
						{ "upsert", "dbo.ObjT" },
						{ "columnList", new LuaTable { "ObjKId", "ObjRId", "Key", "Class", "Value", "UserId", "CreateDate" } },
						{ "on", new LuaTable { "ObjKId", "ObjRId", "Key", "UserId" } }
					};
					#endregion

					foreach (var t in tags.Where(c => c.IsDirty))
					{
						if (t.IsRemoved)
						{
							if (t.Id > 0)
							{
								#region -- tag is going to be removed --

								((LuaTable)deleteCmd[1])["Id"] = t.Id;
								trans.ExecuteNoneResult(deleteCmd);

								#endregion
							}
						}
						else
						{
							#region -- tag update --

							upsertCmd[1] = new LuaTable
							{
								{ "ObjKId", objectId },
								{ "ObjRId", t.IsRev ? (object)revId : null },
								{ "Class", t.TagClass },
								{ "Key", t.Key },
								{ "Value", t.Value },
								{ "UserId", t.UserId },
								{ "CreateDate", DateTime.Now }
							};

							trans.ExecuteNoneResult(upsertCmd);

							#endregion
						}
					}
				}

				Reset(PpsObjectUpdateFlag.Tags);
			}

			// process links
			if ((flags & PpsObjectUpdateFlag.Links) == PpsObjectUpdateFlag.Links && linksTo != null)
			{
				CheckRevision();

				#region -- update links --
				cmd = new LuaTable
				{
					{ "delete", "dbo.ObjL" },
					new LuaTable { {"Id", null } }
				};

				// upsert, insert links
				foreach (var l in linksTo.Where(c => c.IsDirty))
				{
					if (l.IsRemoved)
					{
						if (l.Id > 0)
						{
							((LuaTable)cmd[1])["Id"] = l.Id;
							trans.ExecuteNoneResult(cmd);
						}
					}
					else if (l.IsNew)
					{
						cmd = new LuaTable
						{
							{ "upsert", "dbo.ObjL" },
							{ "columnList", new LuaTable() { "ParentObjKId", "ParentObjRId", "LinkObjKId", "RefCount" } },
							new LuaTable
							{
								{ "ParentObjKId", objectId },
								{ "ParentObjRId", revId < 0 ? null : (object)revId },
								{ "LinkObjKId", l.LinkId },
								{ "RefCount", l.RefCount }
							},
							{ "on", new LuaTable { "ParentObjKId", "ParentObjRId", "LinkObjKId" } }
						};

						trans.ExecuteNoneResult(cmd);

						l.Id = ((LuaTable)cmd[1]).GetOptionalValue("Id", -1L);
					}
					else
					{
						cmd = new LuaTable
						{
							{ "upsert", "dbo.ObjL" },
							{ "columnList", new LuaTable() { "ParentObjKId", "ParentObjRId", "LinkObjKId", "RefCount" } },
							new LuaTable
							{
								{ "Id", l.Id },
								{ "ParentObjKId", objectId },
								{ "ParentObjRId", revId < 0 ? null : (object)revId },
								{ "LinkObjKId", l.LinkId },
								{ "RefCount", l.RefCount }
							}
						};

						trans.ExecuteNoneResult(cmd);
					}
				}
				#endregion

				Reset(PpsObjectUpdateFlag.Links);
			}
		} // proc Update

		#endregion

		#region -- Update Revision Data -----------------------------------------------

		private static Stream ConvertContentToStream(object content, ref bool shouldText, ref bool shouldDeflate)
		{
			switch(content)
			{
				case string s:
					shouldText = true;
					shouldDeflate = shouldDeflate || s.Length > 1024;
					return ConvertContentToStream(Encoding.Unicode.GetBytes(s), ref shouldText, ref shouldDeflate);
				case StringBuilder sb:
					return ConvertContentToStream(sb.ToString(), ref shouldText, ref shouldDeflate);
				case byte[] data:
					return new MemoryStream(data, false);
				case Stream copyStream:
					if (shouldDeflate && copyStream is GZipStream)
						shouldDeflate = false;
					return copyStream;

				case null:
					throw new ArgumentNullException("Document data is missing.");
				default:
					throw new ArgumentException("Document data format is unknown (only string, stream or byte[] allowed).");
			}
		} // func ConvertContentToStream

		/// <summary>Persist content data in the database.</summary>
		/// <param name="content">Content access (string, StringBuilder,byte[],Action[Stream] is allowed).</param>
		/// <param name="changeHead">Change head revision field.</param>
		/// <param name="forceReplace">Force a replace of an revision.</param>
		[LuaMember]
		public void UpdateData(object content, bool changeHead = true, bool forceReplace = false)
		{
			if (objectId < 0)
				throw new ArgumentOutOfRangeException("Id", "Object Id is invalid.");

			var args = new LuaTable
			{
				{ "ObjkId", objectId },
				
				{ "CreateUserId", DEScope.GetScopeService<IPpsPrivateDataContext>().UserId },
				{ "CreateDate",  DateTime.Now }
			};

			// set revId's
			// - if this object new, always create a new rev
			// - exists this object, and there is a rev, create a new and link it via parentId
			// - forceReplace, replaces the current rev
			var insertNew = isRev.Value || (!forceReplace && revId > 0);
			if (insertNew)
			{
				if (revId > 0)
					args["ParentRevId"] = revId;
			}
			else if (revId > 0)
				args["RevId"] = revId;

			// convert data
			var shouldText = MimeType != null && MimeType.StartsWith("text/", StringComparison.OrdinalIgnoreCase);
			var shouldDeflate = shouldText;

			args["Content"] = ConvertContentToStream(content, ref shouldText, ref shouldDeflate);

			args["IsDocumentText"] = shouldText;
			args["Deflate"] = shouldDeflate;

			var cmd = new LuaTable
			{
				{ "execute", "sys.UpdateRevisionData" },
				args
			};

			application.Database.Main.ExecuteNoneResult(cmd);
			var newRevId = (long)args["RevId"];
								
			// update
			// head should be set, if this revId is new or revId has changed
			if ((insertNew || revId != newRevId) && changeHead)
			{
				this["HeadRevId"] = newRevId;
				SetRevision(newRevId, PpsObjectUpdateFlag.None);
			}

			revId = newRevId;
		} // proc UpdateData

		#endregion

		#region -- Get Revision Data --------------------------------------------------

		private IDataRow GetDataRow()
		{
			CheckRevision();

			var cmd = new LuaTable
			{
				{ "execute", "sys.GetRevisionData" },
				revId > -1
					? new LuaTable { { "RevId", revId } }
					: new LuaTable { { "ObjkId", objectId } }
			};

			var row = application.Database.Main.ExecuteSingleRow(cmd);
			if (row == null)
				throw new ArgumentException($"Could not read revision '{revId}'.");

			return row;
		} // func GetDataRow

		/// <summary>Get data stream.</summary>
		/// <returns></returns>
		[LuaMember]
		public Stream GetDataStream()
		{
			var row = GetDataRow();

			return (Stream)row["Document"];
		} // func GetDataStream

		/// <summary>Return the data as an byte array.</summary>
		/// <returns></returns>
		[LuaMember]
		public byte[] GetBytes()
		{
			using (var src = GetDataStream())
				return src.ReadInArray();
		} // func GetBytes

		/// <summary>Return the data as an string.</summary>
		/// <returns></returns>
		[LuaMember]
		public string GetText()
		{
			var row = GetDataRow();
			if (row.GetProperty("IsDocumentText", false))
			{
				using (var src = (Stream)row["Document"])
				using (var tr = Procs.OpenStreamReader(src, DataEncoding))
					return tr.ReadToEnd();
			}
			else
				throw new ArgumentException("No text content.");
		} // func GetText

		#endregion

		#region -- ToXml --------------------------------------------------------------

		/// <summary>Convert the object structure to a xml.</summary>
		/// <param name="onlyObjectData"></param>
		/// <returns></returns>
		[LuaMember]
		public XElement ToXml(bool onlyObjectData = false)
		{
			var x = new XElement(
				"object",
				Procs.XAttributeCreate(nameof(Id), objectId, -1),
				Procs.XAttributeCreate(nameof(RevId), RevId, -1),
				Procs.XAttributeCreate(nameof(HeadRevId), HeadRevId, -1),
				Procs.XAttributeCreate(nameof(CurRevId), CurRevId, -1),
				Procs.XAttributeCreate(nameof(Typ), Typ, null),
				Procs.XAttributeCreate(nameof(MimeType), MimeType, null),
				Procs.XAttributeCreate(nameof(Nr), Nr),
				Procs.XAttributeCreate(nameof(IsRev), IsRev, false)
			);

			if (!onlyObjectData)
			{
				// append links
				foreach (var cur in LinksTo)
					x.Add(cur.ToXml("linksTo"));

				// append tags
				foreach (var cur in Tags)
				{
					if (cur.IsRev)
						x.Add(cur.ToXml("tag"));
				}
			}

			return x;
		} // func ToXml

		#endregion

		#region -- AddTag -------------------------------------------------------------

		private List<PpsObjectTagAccess> GetTags(ref List<PpsObjectTagAccess> tags)
		{
			if (tags != null)
				return tags;
			else if (IsNew)
			{
				tags = new List<PpsObjectTagAccess>(); // create empty tag list
			}
			else // load tag list
			{
				CheckRevision();

				var whereClause = $"[ObjKId] = {objectId} AND ";

				if (revId < 0)
					whereClause += "[ObjRId] IS NULL";
				else
					whereClause += $"([ObjRId] = {revId} OR [ObjRId] IS NULL)";
				
				var cmd = new LuaTable
				{
					{ "select", "dbo.ObjT" },
					{ "selectList",
						new LuaTable
						{
							"Id",
							"ObjRId",
							"Class",
							"Key",
							"Value",
							"UserId",
							"CreateDate"
						}
					},
					{ "where", whereClause }
				};

				try
				{
					tags = new List<PpsObjectTagAccess>();
					foreach (var c in application.Database.Main.ExecuteSingleResult(cmd))
						tags.Add(new PpsObjectTagAccess(this, c));
				}
				catch
				{
					tags = null;
					throw;
				}
			}
			return tags;
		} // func GetTags

		/// <summary>Find a Tag by class, key and user.</summary>
		/// <param name="tagClass"></param>
		/// <param name="key"></param>
		/// <param name="userId"></param>
		/// <returns></returns>
		[LuaMember]
		public PpsObjectTagAccess FindTag(int tagClass, string key, long userId = 0)
		{
			GetTags(ref tags);
			return tags.Find(
				t => t.TagClass == tagClass && String.Compare(t.Key, key, StringComparison.OrdinalIgnoreCase) == 0 && t.UserId == userId
			);
		} // func FindTag

		/// <summary>Add a single tag to the tag list.</summary>
		/// <param name="tagClass"></param>
		/// <param name="key"></param>
		/// <param name="value"></param>
		/// <param name="userId"></param>
		/// <param name="createDate"></param>
		/// <param name="isRevision"></param>
		/// <returns></returns>
		[LuaMember]
		private PpsObjectTagAccess AddTag(int tagClass, string key, string value, long userId = 0, DateTime? createDate = null, bool isRevision = false)
		{
			if (isRevision)
				CheckRevision();

			var tag = FindTag(tagClass, key, userId);

			if (tag == null)
				tags.Add(tag = new PpsObjectTagAccess(this, -1, isRevision, tagClass, key, value, userId, createDate ?? DateTime.Now));
			else
				tag.Value = value;
	
			return tag;
		} // proc AddTag

		/// <summary>Update tag from a table properties (for user and system tags).</summary>
		/// <param name="args"></param>
		/// <returns></returns>
		[LuaMember]
		public PpsObjectTagAccess AddTag(LuaTable args)
			=> AddTag(
				tagClass: args.GetOptionalValue("Class", 0),
				key: args["Key"] as string,
				value: args["Value"]?.ToString(),
				userId: args.GetOptionalValue("UserId", 0),
				createDate: args.GetOptionalValue("CreateDate", DateTime.Now).ToUniversalTime()
			);

		/// <summary>Add tag from a xml-definition (create revision tags)</summary>
		/// <param name="x"></param>
		/// <returns></returns>
		public PpsObjectTagAccess AddTag(XElement x)
			=> AddTag(
				tagClass: x.GetAttribute("tagClass", -1),
				key: x.GetAttribute("key", String.Empty),
				value: x.GetAttribute("value", String.Empty),
				userId: x.GetAttribute("userId", 0L),
				createDate: x.GetAttribute("createDate", DateTime.Now),
				isRevision: true
			);

		/// <summary>Synchronize all revision tags.</summary>
		/// <param name="revisionTags"></param>
		[LuaMember]
		public void UpdateRevisionTags(IEnumerable<PpsObjectTag> revisionTags)
		{
			var currentRevTags = new List<PpsObjectTagAccess>(Tags.Where(t => t.IsRev));

			foreach (var n in revisionTags)
			{
				// update tag
				var o = currentRevTags.Find(c => c.Key == n.Name);
				if (o == null
					|| o.TagClass != (int)n.Class)
				{
					o?.Remove();
					AddTag((int)n.Class, n.Name, n.Value.ChangeType<string>(), 0, DateTime.Now, true);
				}
				else
				{
					o.Value = n.Value.ChangeType<string>();
				}

				// remove from work list
				if (o != null)
					currentRevTags.Remove(o);
			}

			// remove none touched tags
			currentRevTags.ForEach(c => c.Remove());
		} // UpdateRevisionTags

		#endregion

		#region -- AddLinkTo ----------------------------------------------------------

		private List<PpsObjectLinkAccess> GetLinks(bool linksToThis, ref List<PpsObjectLinkAccess> links)
		{
			if (links != null)
				return links;
			else if (IsNew || revId < 0)
			{
				links = new List<PpsObjectLinkAccess>();
			}
			else
			{
				CheckRevision();

				var cmd = new LuaTable
				{
					{ "select", "dbo.ObjL" },
					{ "selectList",
						new LuaTable
						{
							"Id",
							{ linksToThis ? "LinkObjKId" : "ParentObjKId", "ObjKId" },
							{ linksToThis ? "LinkObjRId" : "ParentObjRId", "ObjRId" },
							"RefCount"
						}
					},

					new LuaTable
					{
						{ "ParentObjKId", objectId },
						{ "ParentObjRId", revId }
					}
				};

				try
				{
					links = new List<PpsObjectLinkAccess>();
					foreach (var c in application.Database.Main.ExecuteSingleResult(cmd))
						links.Add(new PpsObjectLinkAccess(this, c));
				}
				catch
				{
					links = null;
					throw;
				}
			}
			return links;
		} // func GetLinks

		private PpsObjectLinkAccess FindLink(bool linksToThis, ref List<PpsObjectLinkAccess> links, long linkId)
		{
			GetLinks(linksToThis, ref links);
			return links.Find(
				l => l.LinkId == linkId
			);
		} // func FindLink

		[LuaMember]
		public PpsObjectLinkAccess FindLinkTo(long linkId)
			=> GetLinks(true, ref linksTo).FirstOrDefault(l => l.LinkId == linkId);

		[LuaMember]
		public PpsObjectLinkAccess FindLinkFrom(long linkId)
			=> GetLinks(true, ref linksFrom).FirstOrDefault(l => l.LinkId == linkId);

		[LuaMember]
		public PpsObjectLinkAccess AddLinkTo(long linkId, int refCount = -1)
		{
			CheckRevision();
			if (IsRev && revId < 0)
				throw new ArgumentOutOfRangeException(nameof(RevId), "No revision is set.");

			var link = FindLink(true, ref linksTo, linkId);
			if (link == null)
				linksTo.Add(link = new PpsObjectLinkAccess(this, -1, linkId, refCount >= 0 ? refCount : 0));
			else if (refCount >= 0)
				link.RefCount = refCount;

			return link;
		} // proc AddLink

		public PpsObjectLinkAccess AddLinkTo(XElement x)
			=> AddLinkTo(x.GetAttribute("linkId", -1L), x.GetAttribute("refCount", -1));

		#endregion

		/// <summary>Id of the object.</summary>
		[LuaMember]
		public long Id => objectId;

		/// <summary>Return the guid of the current object.</summary>
		public Guid Guid => this.TryGetValue<Guid>(nameof(Guid), out var t) ? t : Guid.Empty;
		/// <summary>Object class</summary>
		public string Typ => this.TryGetValue<string>(nameof(Typ), out var t) ? t : null;
		/// <summary>Content mime type.</summary>
		public string MimeType => this.TryGetValue<string>(nameof(MimeType), out var t) ? t : null;
		/// <summary>Human readable number.</summary>
		public string Nr => this.TryGetValue<string>(nameof(Nr), out var t) ? t : null;
		/// <summary>Currently active revision in the RDB.</summary>
		public long CurRevId => this.TryGetValue<long>(nameof(CurRevId), out var t) ? t : -1;
		/// <summary>Head revision.</summary>
		public long HeadRevId => this.TryGetValue<long>(nameof(HeadRevId), out var t) ? t : -1;

		/// <summary>Encoding of the Text-DataStream</summary>
		public Encoding DataEncoding => Encoding.Unicode;

		/// <summary>Active revision of this object.</summary>
		[LuaMember]
		public long RevId
		{
			get
			{
				CheckRevision();
				return revId;
			}
		}

		/// <summary>Does the object track revisions.</summary>
		[LuaMember]
		public bool IsRev
		{
			get
			{
				CheckRevision();
				return isRev ?? false;
			}
			set
			{
				isRev = value;
			}
		} // prop IsRev

		/// <summary>Is this a new object.</summary>
		[LuaMember]
		public bool IsNew => objectId < 0;

		/// <summary>Length of the Object-Content.</summary>
		[LuaMember]
		public long ContentLength => throw new NotImplementedException();

		/// <summary>The object has links to.</summary>
		[LuaMember]
		public IEnumerable<PpsObjectLinkAccess> LinksTo { get => GetLinks(true, ref linksTo); set { } }
		/// <summary>The object is linked from.</summary>
		[LuaMember]
		public IEnumerable<PpsObjectLinkAccess> LinksFrom { get => GetLinks(false, ref linksFrom); set { } }
		/// <summary>Attachted tags to this objects.</summary>
		[LuaMember]
		public IEnumerable<PpsObjectTagAccess> Tags { get => GetTags(ref tags); set { } }
	} // class PpsObjectAccess

	#endregion

	#region -- interface IPpsObjectItem -------------------------------------------------

	/// <summary>Description of an object item.</summary>
	public interface IPpsObjectItem
	{
		/// <summary></summary>
		/// <param name="obj"></param>
		/// <returns></returns>
		object PullData(PpsObjectAccess obj);
		/// <summary></summary>
		/// <param name="obj"></param>
		/// <param name="data"></param>
		/// <returns></returns>
		PpsPushDataResult PushData(PpsObjectAccess obj, object data);

		/// <summary>The name or object typ of the object.</summary>
		string ObjectType { get; }
		/// <summary>Optional description for the object.</summary>
		string ObjectSource { get; }
		/// <summary>Optional default pane for the object.</summary>
		string DefaultPane { get; }
		/// <summary>Returns the default revision behaviour for the object</summary>
		bool IsRevDefault { get; }
	} // interface IPpsObjectItem

	#endregion

	#region -- enum PpsPushDataResult -------------------------------------------------

	/// <summary>Return value for a push-operation.</summary>
	public enum PpsPushDataResult
	{
		/// <summary>No result</summary>
		None = 0,
		/// <summary>Data was pushed, but changed. Pull required.</summary>
		PushedAndChanged = 0,
		/// <summary>Data was pushed, and not changed. No pull required.</summary>
		PushedAndUnchanged = 1,
		/// <summary>Data was not pushed. Because there was a push operation between pull.</summary>
		PulledRevIsToOld = -1
	} // class PpsPushDataResult

	#endregion

	#region -- class PpsObjectItem ----------------------------------------------------

	/// <summary>Base class for all objects, that can be processed from the server.</summary>
	public abstract class PpsObjectItem<T> : PpsPackage, IPpsObjectItem
		where T : class
	{
		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary></summary>
		/// <param name="sp"></param>
		/// <param name="name"></param>
		public PpsObjectItem(IServiceProvider sp, string name)
			: base(sp, name)
		{
			Application = sp.GetService<PpsApplication>(true);
		} // ctor

		#endregion

		#region -- GetObject ----------------------------------------------------------

		private PpsObjectAccess VerfiyObjectType(PpsObjectAccess obj)
		{
			if (String.Compare(obj.Typ, ObjectType, StringComparison.OrdinalIgnoreCase) != 0)
				throw new ArgumentOutOfRangeException(nameof(obj), obj.Typ, $"Object is '{obj.Typ}'. Expected: '{ObjectType}'");
			return obj;
		} // func VerfiyObjectType

		/// <summary>Get object of the correct class.</summary>
		/// <param name="id"></param>
		/// <returns></returns>
		[LuaMember]
		public PpsObjectAccess GetObject(long id)
			=> VerfiyObjectType(Application.Objects.GetObject(id));

		/// <summary>Get object of the correct class.</summary>
		/// <param name="guid"></param>
		/// <returns></returns>
		[LuaMember]
		public PpsObjectAccess GetObject(Guid guid)
			=> VerfiyObjectType(Application.Objects.GetObject(guid));

		/// <summary>Serialize data to an stream of bytes.</summary>
		protected abstract Stream GetStreamFromData(T data);

		/// <summary>Get the data from an stream.</summary>
		/// <param name="src"></param>
		/// <returns></returns>
		protected abstract T GetDataFromStream(Stream src);

		#endregion

		#region -- Pull ---------------------------------------------------------------

		/// <summary>Pull object content from the database</summary>
		/// <param name="obj"></param>
		/// <returns></returns>
		protected virtual T PullData(PpsObjectAccess obj)
		{
			using (var src = obj.GetDataStream())
				return GetDataFromStream(src);
		} // func PullData

		object IPpsObjectItem.PullData(PpsObjectAccess obj)
			=> PullData(obj);

		[LuaMember("Pull")]
		private LuaResult LuaPull(long? objectId, Guid? guidId, long? revId)
		{
			// initialize object access
			var obj = objectId.HasValue
				? GetObject(objectId.Value)
				: guidId.HasValue
					? GetObject(guidId.Value)
					: throw new ArgumentNullException("objectId|guidId");

			if (revId.HasValue)
				obj.SetRevision(revId.Value);

			var data = PullData(obj);

			return new LuaResult(data, obj);
		} // func PullDataSet

		[
		DEConfigHttpAction("pull", IsSafeCall = false, SecurityToken = "user"),
		Description("Reads the revision from the server.")
		]
		private void HttpPullAction(long id, long rev = -1)
		{
			var ctx = DEScope.GetScopeService<IDEWebRequestScope>(true);
			try
			{
				var trans = Application.Database.GetDatabase();

				// get the object and set the correct revision
				var obj = GetObject(id);
				if (rev >= 0)
					obj.SetRevision(rev);

				// prepare object data
				var xObject = SetStatusAttributes(obj.ToXml(), true);
				var headerBytes = Encoding.Unicode.GetBytes(xObject.ToString(SaveOptions.DisableFormatting));
				ctx.OutputHeaders["ppsn-header-length"] = headerBytes.Length.ChangeType<string>();
				ctx.OutputHeaders["ppsn-pulled-revId"] = obj.RevId.ChangeType<string>();
				ctx.OutputHeaders["ppsn-content-type"] = obj.MimeType;

				// get content
				var data = PullData(obj);

				using (var srcStream = GetStreamFromData(data))
				{
					var transferDeflated = MimeTypeMapping.TryGetMapping(obj.MimeType, out var mapping)
						? !mapping.IsCompressedContent
						: false;

					if (srcStream.CanSeek)
						ctx.OutputHeaders["ppsn-content-length"] = srcStream.Length.ToString();
					if (transferDeflated)
						ctx.OutputHeaders["ppsn-content-transfer"] = "gzip";

					// write all data to the application
					using (var dst = ctx.GetOutputStream(MimeTypes.Application.OctetStream))
					{
						// write header bytes
						dst.Write(headerBytes, 0, headerBytes.Length);

						// write content
						if (transferDeflated)
						{
							if (srcStream is GZipStream srcZip && srcZip.CanRead)
								srcZip.BaseStream.CopyTo(dst);
							else
							{
								dst.Flush();
								using (var dstZip = new GZipStream(dst, CompressionMode.Compress, true))
									srcStream.CopyTo(dstZip);
							}
						}
						else
							srcStream.CopyTo(dst);
					}
				}

				// commit
				trans.Commit();
			}
			catch (Exception e)
			{
				ctx.WriteSafeCall(e);
				Log.Except(e);
			}
		} // proc HttpPullAction

		#endregion

		#region -- Push ---------------------------------------------------------------

		/// <summary>Does this object class manage revisions</summary>
		/// <param name="data"></param>
		/// <returns></returns>
		protected virtual bool IsDataRevision(T data)
			=> false;

		private object GetNextNumberMethod()
		{
			// test for next number
			var nextNumber = this["NextNumber"];
			if (nextNumber != null)
				return nextNumber;

			// test for length
			var nextNumberString = Config.GetAttribute("nextNumber", null);
			if (!String.IsNullOrEmpty(nextNumberString))
				return nextNumberString;

			return null;
		} // func GetNextNumberMethod

		/// <summary>Create a human readable number for the object.</summary>
		/// <param name="transaction"></param>
		/// <param name="obj"></param>
		/// <param name="data"></param>
		protected virtual void SetNextNumber(PpsDataTransaction transaction, PpsObjectAccess obj, T data)
		{
			// set the object number for new objects
			var nextNumber = GetNextNumberMethod();
			if (nextNumber == null && obj.Nr == null) // no next number and no number --> error
				throw new ArgumentException($"The field 'Nr' is null or no nextNumber is given.");
			else if (Config.GetAttribute("forceNextNumber", false) || obj.Nr == null) // force the next number or there is no number
				obj["Nr"] = Application.Objects.GetNextNumber(obj.Typ, nextNumber, data);
			else  // check the number format
				Application.Objects.ValidateNumber(obj.Nr, nextNumber, data);
		} // func SetNextNumber

		/// <summary>Create a new object of this class.</summary>
		/// <param name="obj"></param>
		/// <param name="data"></param>
		protected void InsertNewObject(PpsObjectAccess obj, T data)
		{
			obj.IsRev = IsDataRevision(data);
			SetNextNumber(Application.Database.GetDatabaseAsync().AwaitTask(), obj, data);

			// insert the new object, without rev
			obj.Update(PpsObjectUpdateFlag.None);
		} // proc InsertNewObject

		PpsPushDataResult IPpsObjectItem.PushData(PpsObjectAccess obj, object data)
			=> PushData(obj, (T)data, false);

		/// <summary>Persist the data in the server</summary>
		/// <param name="obj">Object information.</param>
		/// <param name="data">Data to push</param>
		/// <param name="release">Has this data a release request.</param>
		/// <returns></returns>
		protected virtual PpsPushDataResult PushData(PpsObjectAccess obj, T data, bool release)
		{
			if (obj == null)
				throw new ArgumentNullException(nameof(obj));
			if (data == null)
				throw new ArgumentNullException(nameof(data));

			VerfiyObjectType(obj);

			// set IsRev
			if (obj.IsNew)
				InsertNewObject(obj, data);

			// write revision
			if (data != null)
			{
				using (var src = GetStreamFromData(data))
					obj.UpdateData(src);
			}

			// write object layout
			obj.Update(PpsObjectUpdateFlag.All);

			return PpsPushDataResult.PushedAndUnchanged;
		} // func PushData

		/// <summary>Push an object.</summary>
		/// <param name="obj"></param>
		/// <param name="data"></param>
		/// <param name="release"></param>
		/// <returns></returns>
		[LuaMember("Push")]
		protected virtual PpsPushDataResult LuaPush(PpsObjectAccess obj, object data, bool release)
			=> PushData(obj, (T)data, release);

		private bool ParseContentTransfer(string value)
		{
			if (String.IsNullOrEmpty(value))
				return false;
			else if (value == "gzip")
				return true;
			else
				throw new ArgumentException("Invalid ppsn-content-transfer");
		} // func ParseContentTransfer

		[
		DEConfigHttpAction("push", IsSafeCall = false),
		Description("Writes a new revision to the object store.")
		]
		private void HttpPushAction(IDEWebRequestScope ctx)
		{
			long objectId = -1;

			var currentUser = ctx.GetUser<IPpsPrivateDataContext>();
			try
			{
				// read header length
				var headerLength = ctx.GetProperty("ppsn-header-length", -1L);
				if (headerLength > 10 << 20 || headerLength < 10) // ignore greater than 10mb or smaller 10bytes (<object/>)
					throw new ArgumentOutOfRangeException("header-length");

				var pulledId = ctx.GetProperty("ppsn-pulled-revId", -1L);
				var releaseRequest = ctx.GetProperty("ppsn-release", false);
				var isContentDeflated = ParseContentTransfer(ctx.GetProperty("ppsn-content-transfer", null));

				var src = ctx.GetInputStream();

				// parse the object body
				XElement xObject;
				using (var headerStream = new WindowStream(src, 0, headerLength, false, true))
				using (var xmlHeader = XmlReader.Create(headerStream, Procs.XmlReaderSettings))
					xObject = XElement.Load(xmlHeader);

				// read the data
				using (var transaction = currentUser.CreateTransactionAsync(Application.MainDataSource).AwaitTask())
				{
					// first the get the object data
					var obj = Application.Objects.ObjectFromXml(transaction, xObject, pulledId);
					VerfiyObjectType(obj);

					objectId = obj.Id;

					// create and load the dataset
					if (isContentDeflated)
						src = new GZipStream(src, CompressionMode.Decompress, true);
					var data = GetDataFromStream(src);

					// push data in the database
					var r = PushData(obj, data, releaseRequest);
					switch (r)
					{
						case PpsPushDataResult.PulledRevIsToOld:
							ctx.WriteSafeCall(
								new XElement("push",
									new XAttribute("headRevId", obj.HeadRevId),
									new XAttribute("pullRequest", Boolean.TrueString)
								)
							);
							break;
						case PpsPushDataResult.PushedAndChanged:
						case PpsPushDataResult.PushedAndUnchanged:
							// write the object definition to client
							using (var tw = ctx.GetOutputTextWriter(MimeTypes.Text.Xml))
							using (var xml = XmlWriter.Create(tw, GetSettings(tw)))
							{
								var xObjInfo = obj.ToXml(true);
								if (r == PpsPushDataResult.PushedAndUnchanged)
									xObjInfo.SetAttributeValue("newRevId", obj.RevId);
								xObjInfo.WriteTo(xml);
							}
							break;
						default:
							throw new ArgumentOutOfRangeException();
					}
					transaction.Commit();
					Log.Info("Push new object: ObjkId={0},Result={1}", obj.Id, r);
				}
			}
			catch (HttpResponseException)
			{
				throw;
			}
			catch (Exception e)
			{
				Log.Except($"Push failed for object ({(objectId <= 0 ? "unknown" : objectId.ToString())}).", e);
				ctx.WriteSafeCall(e);
			}
		} // proc HttpPushAction

		#endregion

		/// <summary>Object class or type</summary>
		[LuaMember]
		public virtual string ObjectType => Name;
		/// <summary>Object source</summary>
		public virtual string ObjectSource => null;
		/// <summary>Default pane for the ui.</summary>
		public virtual string DefaultPane => null;

		/// <summary>Does the object type manages revisions by default</summary>
		public bool IsRevDefault => IsDataRevision(null);

		/// <summary>Get the application.</summary>
		public PpsApplication Application { get; }
	} // class PpsObjectItem

	#endregion

	/// <summary>Function to store and load object related data.</summary>
	public partial class PpsApplication
	{
		#region -- class PpsObjectsLibrary ----------------------------------------------

		/// <summary></summary>
		public sealed class PpsObjectsLibrary : LuaTable
		{
			private static readonly Regex simpleNextNumberFormat = new Regex(@"(?<prefix>[A-Z-_]*)?(\{(?<var>\d+|\w+)\})*", RegexOptions.Singleline | RegexOptions.IgnoreCase);

			private readonly PpsApplication application;

			/// <summary></summary>
			/// <param name="application"></param>
			public PpsObjectsLibrary(PpsApplication application)
			{
				this.application = application;
			} // ctor

			/// <summary>todo</summary>
			/// <param name="nr"></param>
			/// <param name="nextNumber"></param>
			/// <param name="data"></param>
			[LuaMember(nameof(ValidateNumber))]
			public void ValidateNumber(string nr, object nextNumber, object data)
			{
				//throw new NotImplementedException("todo");
			} // proc ValidateNumber

			/// <summary>Gets the next number of an object class</summary>
			/// <param name="typ"></param>
			/// <param name="nextNumber"></param>
			/// <param name="data"></param>
			/// <returns></returns>
			[LuaMember(nameof(GetNextNumber))]
			public string GetNextNumber(string typ, object nextNumber, object data)
			{
				string nr;
				if (nextNumber == null || nextNumber is int) // fill with zeros
				{
					// get the highest number
					var args = new LuaTable()
					{
						["sql"] = "select max(Nr) from dbo.Objk where [Typ] = @Typ",
						[1] = new LuaTable() { ["Typ"] = typ }
					};
					var row = application.Database.Main.ExecuteSingleRow(args);

					// get the next number
					if (row == null || row[0] == null)
						nr = "1"; // first number
					else if (Int64.TryParse(row[0].ToString(), out var i))
						nr = (i + 1L).ToString(CultureInfo.InvariantCulture);
					else
						throw new ArgumentException($"GetNextNumber failed, could not parse '{row[0]}' to number.");

					// pad zeros
					if (nextNumber != null)
						nr = nr.PadLeft((int)nextNumber, '0');
				}
				else if (nextNumber is string) // format mask
				{
					// PREFIX{NR}
					// is also allowed to add {YY} {MM}

					var selectValue = new StringBuilder();
					var formatValue = new StringBuilder();
					var numberOffset = 0;
					var numberLength = -1;
					foreach (var seg in ParseSegments(nextNumber))
					{
						if (seg is string str)
						{
							selectValue.Append(str);
							formatValue.Append(str);
							if (numberLength < 0)
								numberOffset += str.Length;
						}
						else if (seg is int counter)
						{
							selectValue.Append("%");
							formatValue.Append("{0:")
								.Append(new string('0', counter))
								.Append('}');
							numberLength = counter;
						}
						else
							throw new ArgumentException();
					}

					// get the highest number
					var args = new LuaTable()
					{
						["sql"] = "select max(Nr) from dbo.Objk where [Typ] = @Typ and [Nr] LIKE @NRFMT",
						[1] = new LuaTable() { ["Typ"] = typ, ["NRFMT"] = selectValue.ToString() }
					};
					var row = application.Database.Main.ExecuteSingleRow(args);

					var n = 1L;
					if (row != null && row[0] != null)
					{
						var value = row[0].ToString();
						if (value.Length >= numberOffset + numberLength
							&& Int64.TryParse(value.Substring(numberOffset, numberLength), out var i))
							n = i + 1;
						else
							throw new ArgumentException($"GetNextNumber failed, could not parse '{row[0]}' to number.");
					}

					nr = String.Format(formatValue.ToString(), n);
				}
				else if (Lua.RtInvokeable(nextNumber)) // use a function
				{
					nr = new LuaResult(Lua.RtInvoke(nextNumber, data)).ToString();
				}
				else
					throw new ArgumentException($"Unknown number format '{nextNumber}'.", "nextNumber");

				return nr;
			} // func GetNextNumber

			private static IEnumerable<object> ParseSegments(object nextNumber)
			{
				var segments = new List<(int sort, object value)>();
				var m = simpleNextNumberFormat.Match((string)nextNumber);
				if (!m.Success)
					throw new ArgumentException($"GetNextNumber failed, could not parse number format '{nextNumber}'.");

				var hasCounter = false;

				// add prefix
				var prefixCapture = m.Groups["prefix"];
				if (prefixCapture.Length > 0)
					segments.Add((0, prefixCapture.Value));

				// add segments
				foreach (var _c in m.Groups["var"].Captures)
				{
					var c = (Capture)_c;
					if (Int32.TryParse(c.Value, out var counterLength))
					{
						hasCounter = true;
						segments.Add((c.Index, counterLength));
					}
					else
					{
						var segmentData = "";
						switch (c.Value.ToUpper())
						{
							case "YY":
								segmentData = (DateTime.Now.Year % 100).ToString("00");
								break;
							case "YYYY":
								segmentData = DateTime.Now.Year.ToString("0000");
								break;
							case "MM":
								segmentData = DateTime.Now.Month.ToString("00");
								break;
							case "DD":
								segmentData = DateTime.Now.Day.ToString("00");
								break;
							default:
								throw new ArgumentException($"GetNextNumber failed, placeholder '{c.Value}' is unknown.");
						}

						var insertAt = 0;
						while (insertAt < segments.Count && segments[insertAt].sort < c.Index)
							insertAt++;

						segments.Insert(insertAt, (c.Index, segmentData));
					}
				}
				if (!hasCounter)
					throw new ArgumentException($"GetNextNumber failed, counter is missing.");
				
				return from seg in segments select seg.value;
			} // func ParseSegments

			/// <summary>Create a complete new object.</summary>
			/// <param name="args"></param>
			/// <returns></returns>
			[LuaMember]
			public PpsObjectAccess CreateNewObject(LuaTable args)
			{
				if (args.GetMemberValue(nameof(PpsObjectAccess.Guid)) == null)
					args[nameof(PpsObjectAccess.Guid)] = Guid.NewGuid();
				if (args.GetMemberValue(nameof(PpsObjectAccess.Typ)) == null)
					throw new ArgumentNullException(nameof(PpsObjectAccess.Typ), "Typ is missing.");

				return new PpsObjectAccess(application, new LuaTableProperties(args));
			} // func CreateNewObject

			/// <summary>Opens a object for an update operation.</summary>
			/// <param name="trans"></param>
			/// <param name="x"></param>
			/// <returns></returns>
			[LuaMember]
			public PpsObjectAccess ObjectFromXml(PpsDataTransaction trans, XElement x, long setInitRevision)
			{
				var objectId = x.GetAttribute(nameof(PpsObjectAccess.Id), -1L);
				var objectGuid = x.GetAttribute(nameof(PpsObjectAccess.Guid), Guid.Empty);

				// try to find the object in the database
				var obj = (PpsObjectAccess)null;
				if (objectId > 0)
					obj = GetObject(new LuaTable { { nameof(PpsObjectAccess.Id), objectId } });
				else if (objectGuid != Guid.Empty)
					obj = GetObject(new LuaTable { { nameof(PpsObjectAccess.Guid), objectGuid } });

				// create a new object
				if (obj == null)
				{
					obj = new PpsObjectAccess(application, PropertyDictionary.EmptyReadOnly);
					if (objectId > 0)
						throw new ArgumentOutOfRangeException(nameof(objectId), objectId, "Could not found object.");
					if (objectGuid != Guid.Empty)
						obj[nameof(PpsObjectAccess.Guid)] = objectGuid;
				}

				// update the values
				// Do not use CurRev and HeadRev from xml
				if (x.TryGetAttribute<string>(nameof(PpsObjectAccess.Nr), out var nr))
					obj[nameof(PpsObjectAccess.Nr)] = nr;
				if (x.TryGetAttribute<string>(nameof(PpsObjectAccess.Typ), out var typ))
					obj[nameof(PpsObjectAccess.Typ)] = typ;
				if (x.TryGetAttribute<string>(nameof(PpsObjectAccess.MimeType), out var mimeType))
					obj[nameof(PpsObjectAccess.MimeType)] = mimeType;

				// initialize revision
				if (obj.HeadRevId != -1 && obj.IsRev)
				{
					if (setInitRevision == -1)
						throw new ArgumentException("Pulled revId is missing.");
					else
						obj.SetRevision(setInitRevision, PpsObjectUpdateFlag.Tags | PpsObjectUpdateFlag.Links);
				}
				
				// ToDo: maybe provide an Interface and merge these two functions
				// update tags
				void UpdateTags(string tagName, IEnumerable<PpsObjectTagAccess> currentTags, Func<XElement, PpsObjectTagAccess> addTag)
				{
					var removeList = new List<PpsObjectTagAccess>(currentTags.Where(c => c.IsRev));
					foreach (var c in x.Elements(tagName))
					{
						var idx = removeList.IndexOf(addTag(c));
						if (idx != -1)
							removeList.RemoveAt(idx);
					}
					removeList.ForEach(c => c.Remove());
				}

				// update the links
				void UpdateLinks(string tagName, IEnumerable<PpsObjectLinkAccess> currentLinks, Func<XElement, PpsObjectLinkAccess> addLink)
				{
					var removeList = new List<PpsObjectLinkAccess>(currentLinks);
					foreach (var c in x.Elements(tagName))
					{
						var idx = removeList.IndexOf(addLink(c));
						if (idx != -1)
							removeList.RemoveAt(idx);
					}
					removeList.ForEach(c => c.Remove());
				} // proc UpdateLinks

				UpdateLinks("linksTo", obj.LinksTo, obj.AddLinkTo);
				UpdateTags("tag", obj.Tags, obj.AddTag);

				return obj;
			} // func ObjectFromXml

			[LuaMember]
			public PpsDataSelector GetObjectSelector()
			{
				var selector = application.GetViewDefinition("dbo.objects").SelectorToken;
				return selector.CreateSelector(application.Database.Main.Connection);
			} // func GetObjectSelector

			private PpsObjectAccess GetObject(Func<PpsDataSelector, PpsDataSelector> applyFilter)
			{
				// create selector
				var selector = applyFilter(GetObjectSelector());

				// return only the first object
				var r = selector.Select(c => new SimpleDataRow(c)).FirstOrDefault();
				return r == null ? null : new PpsObjectAccess(application, r);
			} // func GetObject

			/// <summary>Get the object from an id.</summary>
			/// <param name="id"></param>
			/// <returns></returns>
			[LuaMember]
			public PpsObjectAccess GetObject(long id)
				=> GetObject(s => s.ApplyFilter(PpsDataFilterExpression.Compare("Id", PpsDataFilterCompareOperator.Equal, id)));

			/// <summary>Get the object from an guid.</summary>
			/// <param name="guid"></param>
			/// <returns></returns>
			[LuaMember]
			public PpsObjectAccess GetObject(Guid guid)
				=> GetObject(s => s.ApplyFilter(PpsDataFilterExpression.Compare("Guid", PpsDataFilterCompareOperator.Equal, guid)));

			/// <summary>Returns object data.</summary>
			/// <param name="args"></param>
			/// <returns></returns>
			[LuaMember]
			public PpsObjectAccess GetObject(LuaTable args)
				=> GetObject(s =>
				{
					// append filter
					if (args.TryGetValue<long>(nameof(PpsObjectAccess.Id), out var objectId))
						return s.ApplyFilter(PpsDataFilterExpression.Compare("Id", PpsDataFilterCompareOperator.Equal, objectId));
					else if (args.TryGetValue<Guid>(nameof(PpsObjectAccess.Guid), out var guidId))
						return s.ApplyFilter(PpsDataFilterExpression.Compare("Guid", PpsDataFilterCompareOperator.Equal, guidId));
					else
						throw new ArgumentNullException(nameof(PpsObjectAccess.Id), "Id or Guid needed to select an object.");
				});

			/// <summary>Returns a view on the objects.</summary>
			/// <returns></returns>
			[LuaMember(nameof(GetObjects))]
			public IEnumerable<PpsObjectAccess> GetObjects()
			{
				foreach (var r in GetObjectSelector())
					yield return new PpsObjectAccess(application, r);
			} // func GetObjects

			#region -- Object Item --------------------------------------------------------

			[LuaMember]
			public IPpsObjectItem GetObjectItem(string objectType)
			{
				IPpsObjectItem item = null;
				if (application.FirstChildren<IPpsObjectItem>(
					c => c.ObjectType == objectType, c => item = c, true
				))
					return item;
				return null;
			} // func GetObjectItem

			#endregion
		} // class PpsObjectsLibrary

		#endregion

		#region -- class PpsDatabaseLibrary ---------------------------------------------

		public sealed class PpsDatabaseLibrary : LuaTable
		{
			private readonly PpsApplication application;

			public PpsDatabaseLibrary(PpsApplication application)
			{
				this.application = application;
			} // ctor

			public Task<PpsDataTransaction> GetDatabaseAsync(string name = null)
			{
				// find existing source
				var dataSource = name == null ? application.MainDataSource : application.GetDataSource(name, true);
				return GetDatabaseAsync(dataSource);
			} // func GetDatabaseAsync

			public async Task<PpsDataTransaction> GetDatabaseAsync(PpsDataSource dataSource)
			{
				var scope = DEScope.GetScopeService<IDECommonScope>(true);
				if (scope.TryGetGlobal<PpsDataTransaction>(this, dataSource, out var trans))
					return trans;

				// get datasource
				var user = DEScope.GetScopeService<IPpsPrivateDataContext>(true);

				// create and register transaction
				trans = await user.CreateTransactionAsync(dataSource, true);

				scope.RegisterCommitAction(new Action(trans.Commit));
				scope.RegisterRollbackAction(new Action(trans.Rollback));
				scope.RegisterDispose(trans);

				scope.SetGlobal(this, dataSource, trans);

				return trans;
			} // func GetDatabaseAsync

			public PpsDataTransaction GetActiveTransaction(PpsDataSource dataSource)
			{
				var scope = DEScope.GetScopeService<IDECommonScope>(false);
				return scope != null && scope.TryGetGlobal<PpsDataTransaction>(this, dataSource, out var trans)
					? trans
					: null;
			} // func GetActiveTransaction

			[LuaMember]
			public PpsDataTransaction GetDatabase(string name = null)
				=> GetDatabaseAsync(name).AwaitTask();

			public Task<PpsDataSelector> CreateSelectorAsync(string name, string columns, string filter, string order)
				=> DEScope.GetScopeService<IPpsPrivateDataContext>(true)
					.CreateSelectorAsync(name, columns, filter, order, true);

			public Task<PpsDataSelector> CreateSelectorAsync(string name, PpsDataColumnExpression[] columns, PpsDataFilterExpression filter, PpsDataOrderExpression[] order)
				=> DEScope.GetScopeService<IPpsPrivateDataContext>(true)
					.CreateSelectorAsync(name, columns, filter, order, true);

			public Task<PpsDataSelector> CreateSelectorAsync(LuaTable table)
				=> DEScope.GetScopeService<IPpsPrivateDataContext>(true)
					.CreateSelectorAsync(table, true);

			[LuaMember]
			public PpsDataSelector CreateSelector(string name, string columns, string filter, string order)
				=> CreateSelectorAsync(name, columns, filter, order).AwaitTask();

			[LuaMember]
			public PpsDataSelector CreateSelector(LuaTable table)
				=> CreateSelectorAsync(table).AwaitTask();

			[LuaMember]
			public PpsDataTransaction Main => GetDatabase();
		} // class PpsDatabaseLibrary

		#endregion

		#region -- class PpsHttpLibrary -------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class PpsHttpLibrary : LuaTable
		{
			private readonly PpsApplication application;

			public PpsHttpLibrary(PpsApplication application)
			{
				this.application = application;
			} // ctor

			private static IDEWebRequestScope CheckContextArgument(IDEWebRequestScope r)
			{
				if (r == null)
					throw new ArgumentNullException("r", "No context given.");
				return r;
			} // func CheckContextArgument

			/// <summary>Creates a XmlWriter for the output stream</summary>
			/// <returns></returns>
			[LuaMember]
			public static XmlWriter CreateXmlWriter()
			{
				var r = DEScope.GetScopeService<IDEWebRequestScope>(true);
				CheckContextArgument(r);
				return XmlWriter.Create(r.GetOutputTextWriter(MimeTypes.Text.Xml, r.Http.DefaultEncoding), Procs.XmlWriterSettings);
			} // func CreateXmlWriter

			/// <summary>Creates a XmlReader for the input stream.</summary>
			/// <returns></returns>
			[LuaMember]
			public static XmlReader CreateXmlReader()
			{
				var r = DEScope.GetScopeService<IDEWebRequestScope>(true);
				CheckContextArgument(r);
				return XmlReader.Create(r.GetInputTextReader(), Procs.XmlReaderSettings);
			} // func CreateXmlReader

			/// <summary>Writes the XElement in the output stream.</summary>
			/// <param name="x"></param>
			[LuaMember]
			public static void WriteXml(XElement x)
			{
				using (var xml = CreateXmlWriter())
					x.WriteTo(xml);
			} // proc WriteXml

			/// <summary>Gets the input stream as an XElement.</summary>
			/// <returns></returns>
			[LuaMember]
			public static XElement GetXml()
			{
				using (var xml = CreateXmlReader())
					return XElement.Load(xml);
			} // proc WriteXml

			/// <summary>Write the table in the output stream.</summary>
			/// <param name="t"></param>
			[LuaMember]
			public static void WriteTable(LuaTable t)
				=> WriteXml(t.ToXml());

			/// <summary>Gets the input stream as an lua-table.</summary>
			/// <returns></returns>
			[LuaMember]
			public static LuaTable GetTable()
				=> Procs.CreateLuaTable(GetXml());
		} // class PpsHttpLibrary

		#endregion

		private readonly PpsObjectsLibrary objectsLibrary;
		private readonly PpsDatabaseLibrary databaseLibrary;
		private readonly PpsHttpLibrary httpLibrary;
		
		/// <summary>Library for access the object store.</summary>
		[LuaMember("Db")]
		public PpsDatabaseLibrary Database => databaseLibrary;

		/// <summary>Library for access the object store.</summary>
		[LuaMember]
		public PpsObjectsLibrary Objects => objectsLibrary;
		/// <summary>Library for easy creation of http-results.</summary>
		[LuaMember]
		public LuaTable Http => httpLibrary;
	} // class PpsApplication
}
