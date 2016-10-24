using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Neo.IronLua;
using TecWare.DE.Data;
using TecWare.DE.Networking;
using TecWare.DE.Server.Http;
using TecWare.DE.Stuff;
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
		private sealed class PpsObjectsLibrary : LuaTable
		{
			private readonly PpsApplication application;

			public PpsObjectsLibrary(PpsApplication application)
			{
				this.application = application;
			} // ctor

			private static void SetStateChange(LuaTable args)
			{
				if (args.GetOptionalValue<long>("StateChg", 0) <= 0)
					args.SetMemberValue("StateChg", DateTime.Now.ToFileTimeUtc());
			} // proc SetStateChange

			/// <summary>Gets the next number of an object class</summary>
			/// <param name="trans"></param>
			/// <param name="typ"></param>
			/// <returns></returns>
			[LuaMember(nameof(GetNextNumber))]
			public object GetNextNumber(PpsDataTransaction trans, string typ)
			{
				// get the highest number
				var args = Procs.CreateLuaTable(
					new PropertyValue("sql", "select max(Nr) from dbo.Objk where [Typ] = @Typ")
				);
				args[1] = Procs.CreateLuaTable(new PropertyValue("Typ", typ));

				var row = trans.ExecuteSingleRow(args);

				// todo: currently, just add
				var nr = row == null || row[0] == null ? "0" : row[0].ToString();
				int i;
				if (Int32.TryParse(nr, out i))
					return i + 1;
				else
					return null;
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
					new PropertyValue("selectList", Procs.CreateLuaArray("Id", "Guid", "Typ", "Nr", "IsRev", "IsHidden", "IsRemoved", "State", "CurRevId", "HeadRevId"))
				);

				if (args.GetValue("Id") != null && args.GetValue("Guid") != null) // get object by id or guid
					cmd.SetArrayValue(1, args);
				else
					throw new ArgumentException("Id or Guid needed to select an object.");

				var r = trans.ExecuteSingleRow(cmd);
				for (var i = 0; i < r.Columns.Count; i++)
					args[r.Columns[i].Name] = r[i];

				return args;
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

			/// <summary>Appends a new revision to the object.</summary>
			/// <param name="trans"></param>
			/// <param name="args"></param>
			/// <returns></returns>
			[LuaMember(nameof(CreateRevision))]
			public LuaTable CreateRevision(PpsDataTransaction trans, LuaTable args)
			{
				return null;
			} // func CreateRevision

			/// <summary>Removes all existing revision, and sets a new one.</summary>
			/// <param name="trans"></param>
			/// <param name="args"></param>
			/// <returns></returns>
			[LuaMember(nameof(ReplaceObjectRevision))]
			public LuaTable ReplaceObjectRevision(PpsDataTransaction trans, LuaTable args)
			{
				return null;
			} // func ReplaceRevision

			[LuaMember(nameof(GetObjectRevision))]
			public LuaTable GetObjectRevision(PpsDataTransaction trans, LuaTable args)
			{
				return null;
			}// func GetObjectRevision

			[LuaMember(nameof(GetObjectRevisions))]
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

			[LuaMember(nameof(CreateXmlWriter))]
			public static XmlWriter CreateXmlWriter(IDEContext r)
			{
				if (r == null)
					throw new ArgumentNullException("r", "No context given.");
				return XmlWriter.Create(r.GetOutputTextWriter(MimeTypes.Text.Xml, r.Server.Encoding), Procs.XmlWriterSettings);
			} // func CreateXmlWriter
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

		[LuaMember(nameof(Objects))]
		public LuaTable Objects => objectsLibrary;
		[LuaMember(nameof(Http))]
		public LuaTable Http => httpLibrary;
	} // class PpsApplication
}
