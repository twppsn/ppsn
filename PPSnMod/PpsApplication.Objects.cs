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

		public void Reset()
			=> isDirty = id < 0;

		/// <summary>Marks the tag as removed</summary>
		public void Remove()
		{
			isRemoved = true;
			isDirty = true;
		} // proc Remove

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

		private void SetDocumentArguments(LuaTable args, Action<Stream> copyData, long contentLength, bool deflateStream)
		{
			args["IsDocumentDeflate"] = deflateStream;

			using (var dstMemory = new MemoryStream())
			{
				if (deflateStream)
				{
					using (var dstDeflate = new GZipStream(dstMemory, CompressionMode.Compress))
						copyData(dstDeflate);
				}
				else
					copyData(dstMemory);
				dstMemory.Flush();

				args["Document"] = dstMemory.ToArray();
			}
		} // proc SetDocumentArguments

		[LuaMember]
		public void UpdateData(object content, long contentLength = -1, bool changeHead = true, bool forceReplace = false)
		{
			if (objectId < 0)
				throw new ArgumentOutOfRangeException("Id", "Object Id is invalid.");

			var args = new LuaTable
			{
				{ "ObjkId", objectId },

				{ "CreateUserId", DEScope.GetScopeService<IPpsPrivateDataContext>().UserId },
				{ "CreateDate",  DateTime.Now }
			};

			var insertNew = isRev.Value || (!forceReplace && revId > 0);
			if (insertNew && revId > 0)
				args["ParentId"] = revId;

			// convert data
			var shouldText = MimeType != null && MimeType.StartsWith("text/", StringComparison.OrdinalIgnoreCase);
			var shouldDeflate = shouldText;

			if (content != null)
			{
				switch (content)
				{
					case string s:
						shouldText = true;
						SetDocumentArguments(args, dst =>
							{
								using (var sr = new StreamWriter(dst, Encoding.Unicode, 4096, true))
									sr.Write(s);
							},
							s.Length, shouldDeflate
						);
						break;
					case StringBuilder sb:
						shouldText = true;
						SetDocumentArguments(args, dst =>
							{
								using (var sr = new StreamWriter(dst, Encoding.Unicode, 4096, true))
									sr.Write(sb.ToString());
							},
							sb.Length, shouldDeflate
						);
						break;
					case byte[] data:
						SetDocumentArguments(args, dst => dst.Write(data, 0, data.Length), data.Length, shouldDeflate);
						break;

					case Action<Stream> copyStream:
						SetDocumentArguments(args, copyStream, contentLength, shouldDeflate);
						break;
					default:
						throw new ArgumentException("Document data format is unknown (only string or byte[] allowed).");
				}
			}
			else
				throw new ArgumentNullException("Document data is missing.");

			args["IsDocumentText"] = shouldText;

			LuaTable cmd;
			if (insertNew)
			{
				cmd = new LuaTable
				{
					{  "insert", "dbo.ObjR" },
					args
				};
			}
			else// upsert current rev
			{
				args["Id"] = revId;

				cmd = new LuaTable
				{
					{  "upsert", "dbo.ObjR" },
					args
				};
			}

			application.Database.Main.ExecuteNoneResult(cmd);
			var newRevId = (long)args["Id"];

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

		[LuaMember]
		public Stream GetDataStream()
		{
			CheckRevision();

			var cmd = new LuaTable
			{
				{ "select", "dbo.ObjR" },
				{ "selectList", new LuaTable { "Id", "IsDocumentText", "IsDocumentDeflate", "Document", "DocumentId", "DocumentLink" } },
				revId > -1 ? new LuaTable
				{
					{ "Id", revId }
				} : new LuaTable
				{
					{ "ObjkId", objectId }
				}
			};

			var row = application.Database.Main.ExecuteSingleRow(cmd);
			if (row == null)
				throw new ArgumentException($"Could not read revision '{revId}'.");

			var isDeflated = row.GetProperty("IsDocumentDeflate", false);
			var rawData = (byte[])row["Document"];

			var src = (Stream)new MemoryStream(rawData, false);
			if (isDeflated)
				src = new GZipStream(src, CompressionMode.Decompress, false);

			return src;
		} // func GetDataStream

		[LuaMember]
		public byte[] GetBytes()
		{
			using (var src = GetDataStream())
				return src.ReadInArray();
		} // func GetBytes

		[LuaMember]
		public string GetText()
		{
			// todo: in DESCore\Networking\Requests.cs:BaseWebRequest.CheckMimeType 
			using (var tr = Procs.OpenStreamReader(GetDataStream(), DataEncoding))
				return tr.ReadToEnd();
		} // func GetText

		#endregion

		#region -- ToXml --------------------------------------------------------------

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

		[LuaMember]
		public PpsObjectTagAccess FindTag(int tagClass, string key, long userId = 0)
		{
			GetTags(ref tags);
			return tags.Find(
				t => t.TagClass == tagClass && String.Compare(t.Key, key, StringComparison.OrdinalIgnoreCase) == 0 && t.UserId == userId
			);
		} // func FindTag

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

		[LuaMember]
		public PpsObjectTagAccess AddTag(LuaTable args)
			=> AddTag(
				tagClass: args.GetOptionalValue("Class", 0),
				key: args["Key"] as string,
				value: args["Value"]?.ToString(),
				userId: args.GetOptionalValue("UserId", 0),
				createDate: args.GetOptionalValue("CreateDate", DateTime.Now).ToUniversalTime()
			);

		public PpsObjectTagAccess AddTag(XElement x)
			=> AddTag(
				tagClass: x.GetAttribute("tagClass", -1),
				key: x.GetAttribute("key", String.Empty),
				value: x.GetAttribute("value", String.Empty),
				userId: x.GetAttribute("userId", 0L),
				createDate: x.GetAttribute("createDate", DateTime.Now),
				isRevision: true
			);

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
		public string Nr => this.TryGetValue<string>(nameof(Nr), out var t) ? t : null;
		public long CurRevId => this.TryGetValue<long>(nameof(CurRevId), out var t) ? t : -1;
		public long HeadRevId => this.TryGetValue<long>(nameof(HeadRevId), out var t) ? t : -1;

		public Encoding DataEncoding => Encoding.Unicode;

		[LuaMember]
		public long RevId
		{
			get
			{
				CheckRevision();
				return revId;
			}
		}

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

		[LuaMember]
		public bool IsNew => objectId < 0;

		[LuaMember]
		public long ContentLength => throw new NotImplementedException();

		[LuaMember]
		public IEnumerable<PpsObjectLinkAccess> LinksTo { get => GetLinks(true, ref linksTo); set { } }
		[LuaMember]
		public IEnumerable<PpsObjectLinkAccess> LinksFrom { get => GetLinks(false, ref linksFrom); set { } }
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
		bool PushData(PpsObjectAccess obj, object data);

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

	#region -- class PpsObjectItem ------------------------------------------------------

	/// <summary>Base class for all objects, that can be processed from the server.</summary>
	public abstract class PpsObjectItem<T> : PpsPackage, IPpsObjectItem
		where T : class
	{
		private readonly PpsApplication application;

		#region -- Ctor/Dtor ------------------------------------------------------------

		/// <summary></summary>
		/// <param name="sp"></param>
		/// <param name="name"></param>
		public PpsObjectItem(IServiceProvider sp, string name)
			: base(sp, name)
		{
			this.application = sp.GetService<PpsApplication>(true);
		} // ctor

		#endregion

		#region -- GetObject ------------------------------------------------------------

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
			=> VerfiyObjectType(application.Objects.GetObject(id));

		/// <summary>Get object of the correct class.</summary>
		/// <param name="guid"></param>
		/// <returns></returns>
		[LuaMember]
		public PpsObjectAccess GetObject(Guid guid)
			=> VerfiyObjectType(application.Objects.GetObject(guid));

		/// <summary>Serialize data to an stream of bytes.</summary>
		protected abstract void WriteDataToStream(T data, Stream dst);

		/// <summary>Get the data from an stream.</summary>
		/// <param name="src"></param>
		/// <returns></returns>
		protected abstract T GetDataFromStream(Stream src);

		#endregion

		#region -- Pull -----------------------------------------------------------------

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
				var trans = application.Database.GetDatabase();

				// get the object and set the correct revision
				var obj = GetObject(id);
				if (rev >= 0)
					obj.SetRevision(rev);

				// prepare object data
				var headerBytes = Encoding.Unicode.GetBytes(obj.ToXml().ToString(SaveOptions.DisableFormatting));
				ctx.OutputHeaders["ppsn-header-length"] = headerBytes.Length.ChangeType<string>();

				// get content
				var data = PullData(obj);

				// write all data to the application
				using (var dst = ctx.GetOutputStream(MimeTypes.Application.OctetStream))
				{
					// write header bytes
					dst.Write(headerBytes, 0, headerBytes.Length);

					// write content
					WriteDataToStream(data, dst);
				}

				// commit
				trans.Commit();
			}
			catch (Exception e)
			{
				ctx.WriteSafeCall(e);
			}
		} // proc HttpPullAction

		#endregion

		#region -- Push -----------------------------------------------------------------

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
				obj["Nr"] = application.Objects.GetNextNumber(obj.Typ, nextNumber, data);
			else  // check the number format
				application.Objects.ValidateNumber(obj.Nr, nextNumber, data);
		} // func SetNextNumber

		/// <summary>Create a new object of this class.</summary>
		/// <param name="obj"></param>
		/// <param name="data"></param>
		protected void InsertNewObject(PpsObjectAccess obj, T data)
		{
			obj.IsRev = IsDataRevision(data);
			SetNextNumber(application.Database.GetDatabaseAsync().AwaitTask(), obj, data);

			// insert the new object, without rev
			obj.Update(PpsObjectUpdateFlag.None);
		} // proc InsertNewObject
		
		bool IPpsObjectItem.PushData(PpsObjectAccess obj, object data)
			=> PushData(obj, (T)data, false);

		/// <summary>Persist the data in the server</summary>
		/// <param name="obj">Object information.</param>
		/// <param name="data">Data to push</param>
		/// <param name="release">Has this data a release request.</param>
		/// <returns></returns>
		protected virtual bool PushData(PpsObjectAccess obj, T data, bool release)
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
				obj.UpdateData(new Action<Stream>(dst => WriteDataToStream(data, dst)));

			// write object layout
			obj.Update(PpsObjectUpdateFlag.All);

			return true;
		} // func PushData

		/// <summary>Push an object.</summary>
		/// <param name="obj"></param>
		/// <param name="data"></param>
		/// <param name="release"></param>
		/// <returns></returns>
		[LuaMember("Push")]
		protected virtual bool LuaPush(PpsObjectAccess obj, object data, bool release)
			=> PushData(obj, (T)data, release);

		[
		DEConfigHttpAction("push", IsSafeCall = false),
		Description("Writes a new revision to the object store.")
		]
		private void HttpPushAction(IDEWebRequestScope ctx)
		{
			var currentUser = ctx.GetUser<IPpsPrivateDataContext>();

			try
			{
				// read header length
				var headerLength = ctx.GetProperty("ppsn-header-length", -1L);
				if (headerLength > 10 << 20 || headerLength < 10) // ignore greater than 10mb or smaller 10bytes (<object/>)
					throw new ArgumentOutOfRangeException("header-length");

				var pulledId = ctx.GetProperty("ppsn-pulled-revId", -1L);
				var releaseRequest = ctx.GetProperty("ppsn-release", false);
				
				var src = ctx.GetInputStream();

				// parse the object body
				XElement xObject;
				using (var headerStream = new WindowStream(src, 0, headerLength, false, true))
				using (var xmlHeader = XmlReader.Create(headerStream, Procs.XmlReaderSettings))
					xObject = XElement.Load(xmlHeader);

				// read the data
				using (var transaction = currentUser.CreateTransactionAsync(application.MainDataSource).AwaitTask())
				{
					// first the get the object data
					var obj = application.Objects.ObjectFromXml(transaction, xObject, pulledId);
					VerfiyObjectType(obj);

					// create and load the dataset
					var data = GetDataFromStream(src);

					// push data in the database
					if (PushData(obj, data, releaseRequest))
					{
						// write the object definition to client
						using (var tw = ctx.GetOutputTextWriter(MimeTypes.Text.Xml))
						using (var xml = XmlWriter.Create(tw, GetSettings(tw)))
							obj.ToXml(true).WriteTo(xml);
					}
					else
					{
						ctx.WriteSafeCall(
							new XElement("push",
								new XAttribute("headRevId", obj.HeadRevId),
								new XAttribute("pullRequest", Boolean.TrueString)
							)
						);
					}

					transaction.Commit();
				}
			}
			catch (HttpResponseException)
			{
				throw;
			}
			catch (Exception e)
			{
				Log.Except("Push failed.", e);
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
		public PpsApplication Application => application;
	} // class PpsObjectItem

	#endregion

	///////////////////////////////////////////////////////////////////////////////
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
			/// <param name="r"></param>
			/// <returns></returns>
			[LuaMember]
			public static XmlWriter CreateXmlWriter()
			{
				var r = DEScope.GetScopeService<IDEWebRequestScope>(true);
				CheckContextArgument(r);
				return XmlWriter.Create(r.GetOutputTextWriter(MimeTypes.Text.Xml, r.Http.DefaultEncoding), Procs.XmlWriterSettings);
			} // func CreateXmlWriter

			/// <summary>Creates a XmlReader for the input stream.</summary>
			/// <param name="r"></param>
			/// <returns></returns>
			[LuaMember]
			public static XmlReader CreateXmlReader()
			{
				var r = DEScope.GetScopeService<IDEWebRequestScope>(true);
				CheckContextArgument(r);
				return XmlReader.Create(r.GetInputTextReader(), Procs.XmlReaderSettings);
			} // func CreateXmlReader

			/// <summary>Writes the XElement in the output stream.</summary>
			/// <param name="r"></param>
			/// <param name="x"></param>
			[LuaMember]
			public static void WriteXml(XElement x)
			{
				using (var xml = CreateXmlWriter())
					x.WriteTo(xml);
			} // proc WriteXml

			/// <summary>Gets the input stream as an XElement.</summary>
			/// <param name="r"></param>
			/// <returns></returns>
			[LuaMember]
			public static XElement GetXml()
			{
				using (var xml = CreateXmlReader())
					return XElement.Load(xml);
			} // proc WriteXml

			/// <summary>Write the table in the output stream.</summary>
			/// <param name="r"></param>
			/// <param name="t"></param>
			[LuaMember]
			public static void WriteTable(LuaTable t)
				=> WriteXml(t.ToXml());

			/// <summary>Gets the input stream as an lua-table.</summary>
			/// <param name="r"></param>
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
