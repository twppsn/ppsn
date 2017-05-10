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
		private long objectId;

		private bool? isRev;
		private long revId;

		private List<PpsObjectLinkAccess> linksTo = null;
		private List<PpsObjectLinkAccess> linksFrom = null;

		internal PpsObjectAccess(PpsDataTransaction transaction, IPropertyReadOnlyDictionary defaultData)
		{
			this.transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
			this.defaultData = defaultData;

			this.objectId = defaultData.GetProperty(nameof(Id), -1L);
			this.revId = defaultData.GetProperty(nameof(RevId), defaultData.GetProperty(nameof(HeadRevId), -1L));
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

		[LuaMember]
		public void Update(bool updateObjectOnly = true)
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

			transaction.ExecuteNoneResult(cmd);

			// update values
			if (IsNew)
			{
				var args = (LuaTable)cmd[1];
				objectId = (long)args[nameof(Id)];
				this[nameof(Guid)] = args[nameof(Guid)];
			}

			if (!updateObjectOnly)
			{

				Reset();
			}
		} // proc Update

		[LuaMember]
		public void SetRevision(long newRevId, bool refreshLinks = true)
		{
			CheckRevision();
			if (revId == newRevId)
				return;

			revId = newRevId;
			if (refreshLinks)
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

			transaction.ExecuteNoneResult(cmd);
			var newRevId = (long)args["Id"];

			// update
			if (insertNew && changeHead)
			{
				this["HeadRevId"] = newRevId;
				SetRevision(newRevId, false);
			}
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

			var row = transaction.ExecuteSingleRow(cmd);
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
					x.Add(cur.ToXml("linkTo"));
				foreach (var cur in LinksFrom)
					x.Add(cur.ToXml("linkFrom"));
			}

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
			else if (IsNew)
			{
				links = new List<PpsObjectLinkAccess>();
			}
			else
			{
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

				new LuaTable
				{
					{ "ParentObjKId", objectId },
					{ "ParentObjRId", revId }
				}
			};

				links = new List<PpsObjectLinkAccess>();
				foreach (var c in transaction.ExecuteSingleResult(cmd))
					links.Add(new PpsObjectLinkAccess(c));
			}
			return links;
		} // func GetLinks

		[LuaMember]
		public long Id => objectId;

		public Guid Guid => this.TryGetValue<Guid>(nameof(Guid), out var t) ? t : Guid.Empty;
		public string Typ => this.TryGetValue<string>(nameof(Typ), out var t) ? t : null;
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
				return isRev.Value;
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

		public IEnumerable<PpsObjectLinkAccess> LinksTo => GetLinks(true, ref linksTo);
		public IEnumerable<PpsObjectLinkAccess> LinksFrom => GetLinks(true, ref linksFrom);
	} // class PpsObjectAccess

	#endregion

	#region -- interface IPpsObjectItem ---------------------------------------------------

	/// <summary>Description of an object item.</summary>
	public interface IPpsObjectItem
	{
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

	#region -- class PpsObjectItem --------------------------------------------------------

	/// <summary>Base class for all objects, that can be processed from the server.</summary>
	public abstract class PpsObjectItem<T> : DEConfigItem, IPpsObjectItem
		where T : class
	{
		private readonly PpsApplication application;

		#region -- Ctor/Dtor ------------------------------------------------------------

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
				throw new ArgumentOutOfRangeException(nameof(obj), obj.Typ, "Object is '{obj.Typ}'. Expected: '{ObjectType}'");
			return obj;
		} // func VerfiyObjectType

		public PpsObjectAccess GetObject(PpsDataTransaction trans, long id)
			=> VerfiyObjectType(application.Objects.GetObject(trans, id));

		public PpsObjectAccess GetObject(PpsDataTransaction trans, Guid guid)
			=> VerfiyObjectType(application.Objects.GetObject(trans, guid));

		/// <summary>Serialize data to an stream of bytes.</summary>
		protected abstract void WriteDataToStream(T data, Stream dst);

		protected abstract T GetDataFromStream(Stream src);

		#endregion

		#region -- Pull -----------------------------------------------------------------

		protected virtual T PullData(PpsDataTransaction trans, PpsObjectAccess obj)
		{
			using (var src = obj.GetDataStream())
				return GetDataFromStream(src);
		} // func PullData

		[LuaMember("Pull")]
		private LuaResult LuaPull(PpsDataTransaction transaction, long? objectId, Guid? guidId, long? revId)
		{
			// initialize object access
			var obj = objectId.HasValue
				? GetObject(transaction, objectId.Value)
				: guidId.HasValue
					? GetObject(transaction, guidId.Value)
					: throw new ArgumentNullException("objectId|guidId");

			if (revId.HasValue)
				obj.SetRevision(revId.Value);

			var data = PullData(transaction, obj);

			return new LuaResult(data, obj);
		} // func PullDataSet

		[
		DEConfigHttpAction("pull", IsSafeCall = false),
		Description("Reads the revision from the server.")
		]
		private void HttpPullAction(IDEContext ctx, long id, long rev = -1)
		{
			var currentUser = DEContext.GetCurrentUser<IPpsPrivateDataContext>();

			try
			{
				using (var trans = currentUser.CreateTransaction(application.MainDataSource))
				{
					// get the object and set the correct revision
					var obj = GetObject(trans, id);
					if (rev >= 0)
						obj.SetRevision(rev);

					// prepare object data
					var headerBytes = Encoding.Unicode.GetBytes(obj.ToXml().ToString(SaveOptions.DisableFormatting));
					ctx.OutputHeaders["ppsn-header-length"] = headerBytes.Length.ChangeType<string>();

					// get content
					var data = PullData(trans, obj);

					// write all data to the application
					using (var dst = ctx.GetOutputStream(MimeTypes.Application.OctetStream))
					{
						// write header bytes
						dst.Write(headerBytes, 0, headerBytes.Length);

						// write content
						WriteDataToStream(data, dst);
					}

					trans.Commit();
				}
			}
			catch (Exception e)
			{
				ctx.WriteSafeCall(e);
			}
		} // proc HttpPullAction

		#endregion

		#region -- Push -----------------------------------------------------------------

		protected virtual bool IsDataRevision(T data)
			=> false;

		private object GetNextNumberMethod()
		{
			// test for next number
			var nextNumber = this["NextNumber"];
			if (nextNumber != null)
				return nextNumber;

			// test for length
			var nrLength = Config.GetAttribute("nrLength", 0);
			if (nrLength > 0)
				return nrLength;

			return null;
		} // func GetNextNumberMethod

		protected virtual void SetNextNumber(PpsDataTransaction transaction, PpsObjectAccess obj, T data)
		{
			// set the object number for new objects
			var nextNumber = GetNextNumberMethod();
			if (nextNumber == null && obj.Nr == null) // no next number and no number --> error
				throw new ArgumentException($"The field 'Nr' is null or no nextNumber is given.");
			else if (Config.GetAttribute("forceNextNumber", false) || obj.Nr == null) // force the next number or there is no number
				obj["Nr"] = application.Objects.GetNextNumber(transaction, obj.Typ, nextNumber, data);
			else  // check the number format
				application.Objects.ValidateNumber(obj.Nr, nextNumber, data);
		} // func SetNextNumber

		protected void InsertNewObject(PpsDataTransaction transaction, PpsObjectAccess obj, T data)
		{
			obj.IsRev = IsDataRevision(data);
			SetNextNumber(transaction, obj, data);

			// insert the new object
			obj.Update(true);
		} // proc InsertNewObject

		protected virtual bool PushData(PpsDataTransaction transaction, PpsObjectAccess obj, T data)
		{
			// set IsRev
			if (obj.IsNew)
				InsertNewObject(transaction, obj, data);

			// update database
			obj.UpdateData(new Action<Stream>(dst => WriteDataToStream(data, dst)));
			obj.Update();

			return true;
		} // func PushData

		[LuaMember("Push")]
		protected virtual bool LuaPush(PpsDataTransaction transaction, PpsObjectAccess obj, object data)
			=> PushData(transaction, obj, (T)data);

		[
		DEConfigHttpAction("push", IsSafeCall = false),
		Description("Writes a new revision to the object store.")
		]
		private void HttpPushAction(IDEContext ctx)
		{
			var currentUser = DEContext.GetCurrentUser<IPpsPrivateDataContext>();

			try
			{
				// read header length
				var headerLength = ctx.GetProperty("ppsn-header-length", -1L);
				if (headerLength > 10 << 20 || headerLength < 10) // ignore greater than 10mb or smaller 10bytes (<object/>)
					throw new ArgumentOutOfRangeException("header-length");

				var src = ctx.GetInputStream();

				// parse the object body
				XElement xObject;
				using (var headerStream = new WindowStream(src, 0, headerLength, false, true))
				using (var xmlHeader = XmlReader.Create(headerStream, Procs.XmlReaderSettings))
					xObject = XElement.Load(xmlHeader);

				// read the data
				using (var transaction = currentUser.CreateTransaction(application.MainDataSource))
				{
					// first the get the object data
					var obj = application.Objects.ObjectFromXml(transaction, xObject);
					VerfiyObjectType(obj);

					// create and load the dataset
					var data = GetDataFromStream(src);

					// push data in the database
					if (PushData(transaction, obj, data))
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

		public virtual string ObjectType => Name;
		public virtual string ObjectSource => null;
		public virtual string DefaultPane => null;

		public bool IsRevDefault => IsDataRevision(null);

		public PpsApplication Application => application;
	} // class PpsObjectItem

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
					obj = GetObject(trans, new LuaTable { { nameof(PpsObjectAccess.Guid), objectGuid } });

				// create a new object
				if (obj == null)
				{
					obj = new PpsObjectAccess(trans, PropertyDictionary.EmptyReadOnly);
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

			private PpsObjectAccess GetObject(PpsDataTransaction trans, Func<PpsDataSelector, PpsDataSelector> applyFilter)
			{
				// create selector
				var selector = applyFilter(GetObjectSelector(trans));

				// return only the first object
				var r = selector.Select(c => new SimpleDataRow(c)).FirstOrDefault();
				return r == null ? null : new PpsObjectAccess(trans, r);
			} // func GetObject

			[LuaMember]
			public PpsObjectAccess GetObject(PpsDataTransaction trans, long id)
				=> GetObject(trans, s => s.ApplyFilter(PpsDataFilterExpression.Compare("Id", PpsDataFilterCompareOperator.Equal, id)));

			[LuaMember]
			public PpsObjectAccess GetObject(PpsDataTransaction trans, Guid guid)
				=> GetObject(trans, s => s.ApplyFilter(PpsDataFilterExpression.Compare("Guid", PpsDataFilterCompareOperator.Equal, guid)));

			/// <summary>Returns object data.</summary>
			/// <param name="args"></param>
			/// <returns></returns>
			[LuaMember(nameof(GetObject))]
			public PpsObjectAccess GetObject(PpsDataTransaction trans, LuaTable args)
				=> GetObject(trans, s =>
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

		/// <summary>Library for access the object store.</summary>
		[LuaMember(nameof(Objects))]
		public PpsObjectsLibrary Objects => objectsLibrary;
		/// <summary>Library for easy creation of http-results.</summary>
		[LuaMember(nameof(Http))]
		public LuaTable Http => httpLibrary;
	} // class PpsApplication
}
