using System;
using System.Collections.Generic;
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
using TecWare.DE.Server.Http;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;
using TecWare.PPSn.Server.Data;

namespace TecWare.PPSn.Server
{
	#region -- class PpsObjectLinkAccess ------------------------------------------------

	public sealed class PpsObjectLinkAccess
	{
		private readonly long id;
		private readonly long objectId;
		private readonly long revId;

		private bool isRemoved = false;

		internal PpsObjectLinkAccess(long id, long objectId, long revId)
		{
			this.id = id;
			this.objectId = objectId;
			this.revId = revId;
		}

		internal PpsObjectLinkAccess(IDataRow row)
		{
		} // ctor

		public void Remove()
		{
		} // proc Remove

		public XElement ToXml(string elementName) 
			=> null;

		public long Id => id;
		public long ObjectId => objectId;
		public long RevId => revId;

		public bool IsNew => id < 0;
		public bool IsRemoved => isRemoved;
	} // class PpsObjectLinkAccess

	#endregion

	#region -- class PpsObjectAccess ----------------------------------------------------

	/// <summary>This class defines a interface to access pps-object model. Some properties are late bound. So, wait for closing the transaction.</summary>
	public sealed class PpsObjectAccess : LuaTable
	{
		private readonly PpsDataTransaction transaction;
		private readonly IPropertyReadOnlyDictionary defaultData;
		private readonly long objectId;

		private bool? isRev;
		private long revId;

		private List<PpsObjectLinkAccess> linksTo = null;
		private List<PpsObjectLinkAccess> linksFrom = null;

		internal PpsObjectAccess(PpsDataTransaction transaction, IPropertyReadOnlyDictionary defaultData)
		{
			this.transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
			this.defaultData = defaultData;

			this.objectId = defaultData.GetProperty(nameof(Id), -1L);
			this.revId = defaultData.GetProperty(nameof(RevId), defaultData.GetProperty(nameof(HeadRevId) ,- 1L));
			this.isRev = defaultData.TryGetProperty<bool>(nameof(IsRev), out var t) ? (bool?)t : null;
		} // ctor
		
		protected override object OnIndex(object key)
			=> key is string k && defaultData.TryGetProperty(k, out var t) ? t : base.OnIndex(key);

		private void Reset()
		{
			// reload links
			linksTo = null;
			linksFrom = null;
		} // proc Reset

		private void CheckRevision()
		{
			if (isRev.HasValue)
				return;

			// get head rev.
			var cmd = new LuaTable
			{
				{ "select", "dbo.ObjK" },
				{ "selectList", new LuaTable { nameof(IsRev), nameof(HeadRevId), } },
				new LuaTable { "Id", objectId }
			};

			var r = transaction.ExecuteSingleRow(cmd);
			isRev = (bool)r[nameof(IsRev), true];
			revId = (long)(r[nameof(HeadRevId), true] ?? -1);
		} // proc CheckRevision

		private LuaTable GetObjectArguments(bool forInsert)
		{
			var args = new LuaTable
			{
				{ nameof(MimeType), MimeType },
				{ nameof(Nr), Nr }
			};

			if (forInsert)
			{
				var guid = Guid;
				if (guid == Guid.Empty)
					guid = Guid.NewGuid();
				args[nameof(Guid)] = guid;
				args[nameof(Typ)] = Typ;
			}
			else
				args[nameof(Id)] = objectId;

			if (CurRevId > 0)
				args[nameof(CurRevId)] = CurRevId;
			if (HeadRevId > 0)
				args[nameof(HeadRevId)] = HeadRevId;

			return args;
		} // func GetObjectArguments

		[LuaMember]
		public void Update()
		{
			LuaTable cmd;
			// prepare stmt
			if (objectId > 0) // exists an id for the object
			{
				cmd = new LuaTable
				{
					{"update", "dbo.ObjK" },
					GetObjectArguments(false)
				};
			}
			else if (Guid == Guid.Empty) // does not exists
			{
				cmd = new LuaTable
				{
					{"insert", "dbo.ObjK" },
					GetObjectArguments(true)
				};
			}
			else // upsert over guid
			{
				cmd = new LuaTable
				{
					{ "upsert", "dbo.Objk"},
					{ "on" ,new LuaTable { nameof(Guid) } },
					GetObjectArguments(false)
				};
			}
			
			transaction.ExecuteNoneResult(cmd);

			Reset();
		} // proc Update

		[LuaMember]
		public void SetRevision(long newRevId)
		{
			CheckRevision();
			if (revId == newRevId)
				return;

			revId = newRevId;
			Reset();
		} // proc SetRevision

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
			CheckRevision();

			if (objectId < 0)
				throw new ArgumentOutOfRangeException("Id", "Object Id is invalid.");

			var args = new LuaTable
			{
				{ "ObjkId", objectId },

				{ "CreateUserId", DEContext.GetCurrentUser<IPpsPrivateDataContext>().UserId },
				{ "CreateDate",  DateTime.Now }
			};

			var insertNew = isRev.Value && changeHead && !forceReplace && revId > 0;
			if (insertNew)
				args["ParentId"] = revId;

			// convert data
			var shouldText = MimeType != null && MimeType.StartsWith("text/", StringComparison.OrdinalIgnoreCase);
			var shouldDeflate = shouldText;

			if(content != null)
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

			transaction.ExecuteNoneResult(cmd);
			var newRevId = (long)args["Id"];

			// update
			if (insertNew && changeHead)
			{
				transaction.ExecuteNoneResult(
					new LuaTable
					{
						{ "update", "dbo.ObjK" },
						new LuaTable
						{
							{ "Id", objectId },
							{ "HeadRevId", newRevId }
						}
					}
				);
			}

			SetRevision(newRevId);
		} // proc UpdateData

		[LuaMember]
		public Stream GetDataStream()
		{
			CheckRevision();

			var cmd = new LuaTable
			{
				{ "select", "dbo.ObjR" },
				{ "selectList", new LuaTable { "Id", "IsDocumentText","IsDocumentDeflate","Document","DocumentId","DocumentLink" } },
				new LuaTable { { "Id", revId } }
			};

			var row =	transaction.ExecuteSingleRow(cmd);
			if (row == null)
				throw new ArgumentException($"Could not read revision '{revId}'.");

			var isDeflated = row.GetProperty("IsDocumentDeflate", false);
			var rawData = (byte[])row["Document"];

			var src = (Stream) new MemoryStream(rawData, false);
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
			return Encoding.Unicode.GetString(GetBytes());
		} // func GetText

		[LuaMember]
		public XElement ToXml()
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

			// append links
			foreach (var cur in LinksTo)
				x.Add(cur.ToXml("linkTo"));
			foreach (var cur in LinksFrom)
				x.Add(cur.ToXml("linkFrom"));
			
			return x;
		} // func ToXml

		public PpsObjectLinkAccess AddLinkTo(XElement x)
		{
			return null;
		}

		public PpsObjectLinkAccess AddLinkFrom(XElement x)
		{
			return null;
		}

		[LuaMember]
		public void AddLink(long objectId, long revId)
		{
			var existingLink = GetLinks(true, ref linksTo).FirstOrDefault(c => c.ObjectId == objectId && c.RevId == revId);
			if (existingLink == null)
				linksTo.Add(new PpsObjectLinkAccess(-1, objectId, revId));
		} // proc AddLink

		private List<PpsObjectLinkAccess> GetLinks(bool linksTo, ref List<PpsObjectLinkAccess> links)
		{
			if (links != null)
				return links;

			var cmd = new LuaTable
			{
				{ "select", "dbo.ObjL" },
				{ "selectList",
					new LuaTable
					{
						"Id",
						{ linksTo ? "LinkObjKId" : "ParentObjKId", "ObjKId" },
						{ linksTo ? "LinkObjRId" : "ParentObjRId", "ObjRId" },
						"OnDelete"
					}
				},
				{ "args",
					new LuaTable
					{
						{ "ParentObjKId", objectId },
						{ "ParentObjRId", revId }
					}
				}
			};

			links = new List<PpsObjectLinkAccess>();
			foreach (var c in transaction.ExecuteSingleResult(cmd))
				links.Add(new PpsObjectLinkAccess(c));

			return links;
		} // func GetLinks

		[LuaMember]
		public long Id => objectId;

		public Guid Guid => this.TryGetValue<Guid>(nameof(Guid), out var t, rawGet: true) ? t : Guid.Empty;
		public string Typ => this.TryGetValue<string>(nameof(Typ), out var t, rawGet: true) ? t : null;
		public string MimeType => this.TryGetValue<string>(nameof(MimeType), out var t, rawGet: true) ? t : null;
		public string Nr => this.TryGetValue<string>(nameof(Nr), out var t, rawGet: true) ? t : null;
		public long CurRevId => this.TryGetValue<long>(nameof(CurRevId), out var t, rawGet: true) ? t : -1;
		public long HeadRevId => this.TryGetValue<long>(nameof(HeadRevId), out var t, rawGet: true) ? t : -1;

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
				return isRev.Value;
			}
		} // prop IsRev

		[LuaMember]
		public bool IsNew => objectId < 0;

		[LuaMember]
		public long ContentLength => throw new NotImplementedException();

		public IEnumerable<PpsObjectLinkAccess> LinksTo => GetLinks(true, ref linksTo);
		public IEnumerable<PpsObjectLinkAccess> LinksFrom => GetLinks(true, ref linksFrom);
	} // class PpsObjectAccess

	#endregion

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Function to store and load object related data.</summary>
	public partial class PpsApplication
	{
		#region -- class PpsObjectsLibrary ------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		public sealed class PpsObjectsLibrary : LuaTable
		{
			private readonly PpsApplication application;

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
			/// <param name="trans"></param>
			/// <param name="typ"></param>
			/// <returns></returns>
			[LuaMember(nameof(GetNextNumber))]
			public string GetNextNumber(PpsDataTransaction trans, string typ, object nextNumber, object data)
			{
				// get the highest number
				var args = Procs.CreateLuaTable(
					new PropertyValue("sql", "select max(Nr) from dbo.Objk where [Typ] = @Typ")
				);
				args[1] = Procs.CreateLuaTable(new PropertyValue("Typ", typ));

				var row = trans.ExecuteSingleRow(args);

				string nr;
				if (nextNumber == null || nextNumber is int) // fill with zeros
				{
					long i;
					if (row == null || row[0] == null)
						nr = "1"; // first number
					else if (Int64.TryParse(row[0].ToString(), out i))
						nr = (i + 1L).ToString(CultureInfo.InvariantCulture);
					else
						throw new ArgumentException($"GetNextNumber failed, could not parse '{row[0]}' to number.");

					if (nextNumber != null)
						nr = nr.PadLeft((int)nextNumber, '0');
				}
				else if (nextNumber is string) // format mask
				{
					// V<YY><NR>
					throw new NotImplementedException();
				}
				else if (Lua.RtInvokeable(nextNumber)) // use a function
				{
					nr = new LuaResult(Lua.RtInvoke(nextNumber, trans, row == null ? null : row[0], data)).ToString();
				}
				else
					throw new ArgumentException($"Unknown number format '{nextNumber}'.", "nextNumber");

				return nr;
			} // func GetNextNumber
			
			/// <summary>Create a complete new object.</summary>
			/// <param name="trans"></param>
			/// <param name="args"></param>
			/// <returns></returns>
			[LuaMember]
			public PpsObjectAccess CreateNewObject(PpsDataTransaction trans, LuaTable args)
				=> new PpsObjectAccess(trans, new LuaTableProperties(args));

			/// <summary>Opens a object for an update operation.</summary>
			/// <param name="trans"></param>
			/// <param name="x"></param>
			/// <returns></returns>
			[LuaMember]
			public PpsObjectAccess ObjectFromXml(PpsDataTransaction trans, XElement x)
			{
				var objectId = x.GetAttribute(nameof(PpsObjectAccess.Id), -1L);
				var objectGuid = x.GetAttribute(nameof(PpsObjectAccess.Guid), Guid.Empty);

				// try to find the object in the database
				var obj = (PpsObjectAccess)null;
				if (objectId > 0)
					obj = GetObject(trans, new LuaTable { { nameof(PpsObjectAccess.Id), objectId } });
				else if (objectGuid != Guid.Empty)
					obj = GetObject(trans, new LuaTable { { nameof(PpsObjectAccess.Guid), objectId } });

				// create a new object
				if (obj == null)
					obj = new PpsObjectAccess(trans, PropertyDictionary.EmptyReadOnly);

				// update the values
				// Do not use CurRev and HeadRev from xml
				if (x.TryGetAttribute<string>(nameof(PpsObjectAccess.Nr), out var nr))
					obj[nameof(PpsObjectAccess.Nr)] = nr;
				if (x.TryGetAttribute<string>(nameof(PpsObjectAccess.MimeType), out var mimeType))
					obj[nameof(PpsObjectAccess.MimeType)] = mimeType;

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

				UpdateLinks(nameof(PpsObjectAccess.LinksTo), obj.LinksTo, obj.AddLinkTo);
				UpdateLinks(nameof(PpsObjectAccess.LinksFrom), obj.LinksFrom, obj.AddLinkTo);

				return obj;
			} // func ObjectFromXml

			[LuaMember]
			public PpsDataSelector GetObjectSelector(PpsDataTransaction trans)
			{
				var selector = application.GetViewDefinition("dbo.objects").SelectorToken;
				return selector.CreateSelector(trans.Connection);
			} // func GetObjectSelector

			/// <summary>Returns object data.</summary>
			/// <param name="args"></param>
			/// <returns></returns>
			[LuaMember(nameof(GetObject))]
			public PpsObjectAccess GetObject(PpsDataTransaction trans, LuaTable args)
			{
				var selector = GetObjectSelector(trans);

				// append filter
				if (args.TryGetValue<long>(nameof(PpsObjectAccess.Id), out var objectId))
					selector = selector.ApplyFilter(PpsDataFilterExpression.Compare("Id", PpsDataFilterCompareOperator.Equal, objectId));
				else if (args.TryGetValue<Guid>(nameof(PpsObjectAccess.Guid), out var guidId))
					selector = selector.ApplyFilter(PpsDataFilterExpression.Compare("Guid", PpsDataFilterCompareOperator.Equal, guidId));
				else
					throw new ArgumentNullException(nameof(PpsObjectAccess.Id), "Id or Guid needed to select an object.");

				// return only the first object
				var r = selector.Select(c => new SimpleDataRow(c)).FirstOrDefault();
				return r == null ? null : new PpsObjectAccess(trans, r);
			} // func GetObject

			/// <summary>Returns a view on the objects.</summary>
			/// <param name="trans"></param>
			/// <returns></returns>
			[LuaMember(nameof(GetObjects))]
			public IEnumerable<PpsObjectAccess> GetObjects(PpsDataTransaction trans)
			{
				foreach (var r in GetObjectSelector(trans))
					yield return new PpsObjectAccess(trans, r);
			} // func GetObjects
		} // class PpsObjectsLibrary

		#endregion

		#region -- class PpsHttpLibrary ---------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class PpsHttpLibrary : LuaTable
		{
			private readonly PpsApplication application;

			public PpsHttpLibrary(PpsApplication application)
			{
				this.application = application;
			} // ctor

			private static IDEContext CheckContextArgument(IDEContext r)
			{
				if (r == null)
					throw new ArgumentNullException("r", "No context given.");
				return r;
			} // func CheckContextArgument

			/// <summary>Creates a XmlWriter for the output stream</summary>
			/// <param name="r"></param>
			/// <returns></returns>
			[LuaMember]
			public static XmlWriter CreateXmlWriter(IDEContext r)
			{
				CheckContextArgument(r);
				return XmlWriter.Create(r.GetOutputTextWriter(MimeTypes.Text.Xml, r.Server.Encoding), Procs.XmlWriterSettings);
			} // func CreateXmlWriter

			/// <summary>Creates a XmlReader for the input stream.</summary>
			/// <param name="r"></param>
			/// <returns></returns>
			[LuaMember]
			public static XmlReader CreateXmlReader(IDEContext r)
			{
				CheckContextArgument(r);
				return XmlReader.Create(r.GetInputTextReader(), Procs.XmlReaderSettings);
			} // func CreateXmlReader

			/// <summary>Writes the XElement in the output stream.</summary>
			/// <param name="r"></param>
			/// <param name="x"></param>
			[LuaMember]
			public static void WriteXml(IDEContext r, XElement x)
			{
				using (var xml = CreateXmlWriter(r))
					x.WriteTo(xml);
			} // proc WriteXml

			/// <summary>Gets the input stream as an XElement.</summary>
			/// <param name="r"></param>
			/// <returns></returns>
			[LuaMember]
			public static XElement GetXml(IDEContext r)
			{
				using (var xml = CreateXmlReader(r))
					return XElement.Load(xml);
			} // proc WriteXml

			/// <summary>Write the table in the output stream.</summary>
			/// <param name="r"></param>
			/// <param name="t"></param>
			[LuaMember]
			public static void WriteTable(IDEContext r, LuaTable t)
				=> WriteXml(r, t.ToXml());

			/// <summary>Gets the input stream as an lua-table.</summary>
			/// <param name="r"></param>
			/// <returns></returns>
			[LuaMember]
			public static LuaTable GetTable(IDEContext r)
				=> Procs.CreateLuaTable(GetXml(r));
		} // class PpsHttpLibrary

		#endregion

		private readonly PpsObjectsLibrary objectsLibrary;
		private readonly PpsHttpLibrary httpLibrary;

		/// <summary>Funktion to create a synchronisation time stamp.</summary>
		/// <returns>Timestamp formatted as an Int64.</returns>
		[LuaMember]
		public long GetSyncStamp()
			=> Procs.GetSyncStamp();

		private object HttpPushFile()
		{
			return null;
		}

		private object HttpPullFile()
		{
			return null;
		}

		private object HttpLoadFile()
		{
			return null;
		}

		private object HttpStoreFile()
		{
			return null;
		}

		/// <summary>Library for access the object store.</summary>
		[LuaMember(nameof(Objects))]
		public PpsObjectsLibrary Objects => objectsLibrary;
		/// <summary>Library for easy creation of http-results.</summary>
		[LuaMember(nameof(Http))]
		public LuaTable Http => httpLibrary;
	} // class PpsApplication
}
