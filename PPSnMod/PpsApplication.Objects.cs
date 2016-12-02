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

			private static void SetStateChange(LuaTable args)
			{
				if (args.GetOptionalValue<long>("SyncToken", 0) <= 0)
					args.SetMemberValue("SyncToken", Procs.GetSyncStamp());
			} // proc SetStateChange

			private static void UpdateArgumentsWithRow(LuaTable args, IDataRow r)
			{
				for (var i = 0; i < r.Columns.Count; i++)
					args[r.Columns[i].Name] = r[i];
			} // proc UpdateArgumentsWithRow

			[LuaMember(nameof(ValidateNumber))]
			public void ValidateNumber(string nr, object nextNumber, object data)
			{
				throw new NotImplementedException("todo");
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

			/// <summary>Updates a new entry in the objk</summary>
			/// <param name="args">
			/// Id
			/// Guid
			/// Typ
			/// Nr 
			/// IsRev
			/// IsHidden
			/// IsRemoved
			/// </param>
			/// <returns></returns>
			[LuaMember(nameof(Update))]
			public LuaTable Update(PpsDataTransaction trans, LuaTable args)
			{
				LuaTable cmd;

				SetStateChange(args);

				// prepare stmt
				// check arguments if guid exists -> create a merge
				if (args.GetValue("Guid") != null)
				{
					cmd = Procs.CreateLuaTable(
						new PropertyValue("upsert", "dbo.Objk"),
						new PropertyValue("on", Procs.CreateLuaArray("Guid"))
					);
					cmd.SetArrayValue(1, args);
				}
				// check if id exists -> create a update
				else if (args.GetValue("Id") != null)
				{
					cmd = Procs.CreateLuaTable(new PropertyValue("update", "dbo.Objk"));
					cmd.SetArrayValue(1, args);
				}
				// none of both exists -> create a insert
				else
				{
					cmd = Procs.CreateLuaTable(new PropertyValue("insert", "dbo.Objk"));
					cmd.SetArrayValue(1, args);
				}

				// checks will be done by the database
				trans.ExecuteNoneResult(cmd);
				return args;
			} // func Update

			/// <summary>Returns object data.</summary>
			/// <param name="args"></param>
			/// <returns></returns>
			[LuaMember(nameof(GetObject))]
			public LuaTable GetObject(PpsDataTransaction trans, LuaTable args)
			{
				var cmd = Procs.CreateLuaTable(
					new PropertyValue("select", "dbo.Objk"),
					new PropertyValue("selectList", Procs.CreateLuaArray("Id", "Guid", "Typ", "Nr", "IsRev", "IsHidden", "IsRemoved", "SyncToken", "CurRevId", "HeadRevId"))
				);

				if (args.GetValue("Id") != null || args.GetValue("Guid") != null) // get object by id or guid
					cmd.SetArrayValue(1, args);
				else
					throw new ArgumentException("Id or Guid needed to select an object.");

				var r = trans.ExecuteSingleRow(cmd);
				if (r != null)
				{
					UpdateArgumentsWithRow(args, r);
					return args;
				}
				else
					return null;
			} // func GetObject

			/// <summary>Returns a view on the objects.</summary>
			/// <param name="trans"></param>
			/// <returns></returns>
			[LuaMember(nameof(GetObjects))]
			public PpsDataSelector GetObjects(PpsDataTransaction trans)
			{
				var selector = application.GetViewDefinition("dbo.objects").SelectorToken;
				return selector.CreateSelector(trans.Connection);
			} // func GetObjects

			/// <summary>Sets one ore more state member of an object, to a new value.</summary>
			/// <param name="trans"></param>
			/// <param name="args"></param>
			/// <returns></returns>
			[LuaMember(nameof(UpdateState))]
			public LuaTable UpdateState(PpsDataTransaction trans, LuaTable args)
			{
				return null;
			} // func UpdateState

			private void CheckTagField(LuaTable args, string tagName)
			{
				var v = args[tagName];
				if (v is IEnumerable<PpsObjectTag>)
					args[tagName] = PpsObjectTag.FormatTagFields((IEnumerable<PpsObjectTag>)v);
				else if (v != null)
					throw new ArgumentException("Could not convert tag field.");
			} // func CheckTagField

			/// <summary>Appends a new revision to the object.</summary>
			/// <param name="trans"></param>
			/// <param name="args"></param>
			/// <returns></returns>
			[LuaMember]
			public long CreateRevision(PpsDataTransaction trans, LuaTable args)
			{
				// fill optional values
				if (args.GetMemberValue("CreateUserId") == null)
					args["CreateUserId"] = DEContext.GetCurrentUser<IPpsPrivateDataContext>().UserId;
				if (args.GetMemberValue("CreateDate") == null)
					args["CreateDate"] = DateTime.Now;

				CheckTagField(args, "Tags");

				// execute insert
				var cmd = Procs.CreateLuaTable(
					new PropertyValue("insert", "dbo.Objr")
				);
				cmd[1] = args;

				// convert data
				var rawData = args.GetMemberValue("Document");
				var shouldText = args.GetMemberValue("IsDocumentText");
				var shouldDeflate = args.GetMemberValue("IsDocumentDeflate");

				if (rawData != null)
				{
					if (rawData is string || rawData is StringBuilder)
					{
						#region -- text --
						if (shouldDeflate == null)
							shouldDeflate = true;
						if (shouldText == null)
							shouldText = true;

						args["IsDocumentText"] = shouldText;
						args["IsDocumentDeflate"] = shouldDeflate;

						using (var dstMemory = new MemoryStream())
						{
							Stream dstDeflate = null;
							StreamWriter tr = null;
							try
							{
								if ((bool)shouldDeflate)
									dstDeflate = new GZipStream(dstMemory, CompressionMode.Compress, true);
								tr = new StreamWriter(dstDeflate ?? dstMemory, Encoding.UTF8, 1024, true);

								tr.Write(rawData.ToString());
							}
							finally
							{
								tr?.Dispose();
								dstDeflate?.Close();
							}

							args["Document"] = dstMemory.ToArray();
						}
						#endregion
					}
					else if (rawData is byte[])
					{
						#region -- byte[] --
						if (shouldDeflate == null)
							shouldDeflate = false;

						args["IsDocumentText"] = shouldText ?? false;
						args["IsDocumentDeflate"] = shouldDeflate;

						using (var dstMemory = new MemoryStream())
						{
							var b = (byte[])rawData;
							if ((bool)shouldDeflate)
							{
								using (var dstDeflate = new GZipStream(dstMemory, CompressionMode.Compress))
									dstDeflate.Write(b, 0, b.Length);
							}
							else
								dstMemory.Write(b, 0, b.Length);
							dstMemory.Flush();

							args["Document"] = dstMemory.ToArray();
						}
						#endregion
					}
					else
						throw new ArgumentException("Document data format is unknown (only string or byte[] allowed).");
				}
				else
					throw new ArgumentNullException("Document data is missing.");

				trans.ExecuteNoneResult(cmd);
				return (long)args["Id"];
			} // func CreateRevision

			/// <summary>Removes all existing revision, and sets a new one.</summary>
			/// <param name="trans"></param>
			/// <param name="args"></param>
			/// <returns></returns>
			[LuaMember]
			public LuaTable ReplaceObjectRevision(PpsDataTransaction trans, LuaTable args)
			{
				return null;
			} // func ReplaceRevision

			private static Stream OpenObjectRevisionStream(LuaTable args)
			{
				var rawData = args.GetMemberValue("Document");
				var externalLink = args.GetMemberValue("DocumentLink");
				var internalLink = args.GetMemberValue("DocumentId");
				var isDocumentDeflate = args.GetMemberValue("IsDocumentDeflate");

				Stream src = null;
				if (rawData != null) // raw byte stream detected
				{
					src = new MemoryStream((byte[])rawData, false);
				}
				else
					throw new NotImplementedException();

				// unpack stream
				return isDocumentDeflate is bool && (bool)isDocumentDeflate ? new GZipStream(src, CompressionMode.Decompress) : src;
			} // func OpenObjectRevisionStream

			private static StreamReader OpenObjectRevisionText(LuaTable args)
			{
				// open the stream
				var src = OpenObjectRevisionStream(args);
				return new StreamReader(src, true);
			} // func OpenObjectRevisionText

			private static string GetObjectRevisionText(LuaTable args)
			{
				// open the stream
				using (var sr = OpenObjectRevisionText(args))
					return sr.ReadToEnd();
			} // func OpenObjectRevisionText

			[
				LuaMember,
				LuaArgument("trans"),
				LuaArgument("args"),
				LuaArgument("args|Id"),
				LuaArgument("args|Guid"),
				LuaArgument("args|RevId")
			]
			public LuaTable GetObjectRevision(PpsDataTransaction trans, LuaTable args)
			{
				const string baseSql = "select o.Id, o.Guid, o.Typ, o.HeadRevId, o.CurRevId, r.Document, r.DocumentLink, r.DocumentId, r.IsDocumentText, r.IsDocumentDeflate " +
					"from Objk o inner join Objr r";

				var objectId = args["Id"];
				var guidId = args["Guid"];
				var revId = args["RevId"];

				// create commands
				var cmd = new LuaTable();
				if (objectId != null && revId != null)
					cmd["sql"] = baseSql + " on (o.Id = r.ObjkId) where o.Id = @Id and r.Id = @RevId";
				else if (objectId != null)
					cmd["sql"] = baseSql + " on (o.HeadRevId = r.Id) where o.Id = @Id";
				else if (guidId != null && revId != null)
					args["sql"] = baseSql + " on (o.Id = r.ObjkId) where o.Id = @Guid and r.Id = @RevId";
				else if (guidId != null)
					args["sql"] = baseSql + " on (o.HeadRevId = r.Id) where o.Guid = @Guid";
				else
					throw new ArgumentNullException("Id or Guid is missing.");

				cmd[1] = args;

				var row = trans.ExecuteSingleRow(cmd);
				if (row == null)
					return null;

				UpdateArgumentsWithRow(args, row);

				// patch methods for the stream access
				args["__trans"] = true;
				args.DefineMethod("openText", new Func<LuaTable, StreamReader>(OpenObjectRevisionText));
				args.DefineMethod("openStream", new Func<LuaTable, Stream>(OpenObjectRevisionStream));
				args.DefineMethod("getText", new Func<LuaTable, string>(GetObjectRevisionText));

				return args;
			} // func GetObjectRevision

			[LuaMember]
			public LuaTable GetObjectRevisions(PpsDataTransaction trans, LuaTable args)
			{
				return null;
			}// func GetObjectRevision
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

			[LuaMember]
			public static XmlWriter CreateXmlWriter(IDEContext r)
			{
				CheckContextArgument(r);
				return XmlWriter.Create(r.GetOutputTextWriter(MimeTypes.Text.Xml, r.Server.Encoding), Procs.XmlWriterSettings);
			} // func CreateXmlWriter

			[LuaMember]
			public static XmlReader CreateXmlReader(IDEContext r)
			{
				CheckContextArgument(r);
				return XmlReader.Create(r.GetInputTextReader(), Procs.XmlReaderSettings);
			} // func CreateXmlReader

			[LuaMember]
			public static void WriteXml(IDEContext r, XElement x)
			{
				using (var xml = CreateXmlWriter(r))
					x.WriteTo(xml);
			} // proc WriteXml

			[LuaMember]
			public static XElement GetXml(IDEContext r)
			{
				using (var xml = CreateXmlReader(r))
					return XElement.Load(xml);
			} // proc WriteXml

			[LuaMember]
			public static void WriteTable(IDEContext r, LuaTable t)
				=> WriteXml(r, t.ToXml());

			[LuaMember]
			public static LuaTable GetTable(IDEContext r)
				=> Procs.CreateLuaTable(GetXml(r));
		} // class PpsHttpLibrary

		#endregion

		private readonly PpsObjectsLibrary objectsLibrary;
		private readonly PpsHttpLibrary httpLibrary;

		[LuaMember("error")]
		public void LuaError(string message, params object[] args)
		{
			if (args != null && args.Length > 0)
				message = String.Format(message, args);
			throw new Exception(message);
		} // proc LuaError

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

		[LuaMember(nameof(Objects))]
		public PpsObjectsLibrary Objects => objectsLibrary;
		[LuaMember(nameof(Http))]
		public LuaTable Http => httpLibrary;
	} // class PpsApplication
}
