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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Networking;
using TecWare.DE.Server;
using TecWare.DE.Server.Http;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;
using TecWare.PPSn.Server.Data;
using TecWare.PPSn.Server.Wpf;

namespace TecWare.PPSn.Server
{
	#region -- class PpsDocument --------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsDocument : DEConfigItem, IWpfClientApplicationFileProvider
	{
		private readonly PpsApplication application;

		private PpsDataSetServerDefinition datasetDefinition = null;
		private ILuaAttachedScript[] currentAttachedScripts = null;

		#region -- Ctor/Dtor --------------------------------------------------------------

		public PpsDocument(IServiceProvider sp, string name)
			: base(sp, name)
		{
			this.application = sp.GetService<PpsApplication>(true);
		} // ctor

		protected override void OnBeginReadConfiguration(IDEConfigLoading config)
		{
			base.OnBeginReadConfiguration(config);

			// dispose scripts connections
			if (currentAttachedScripts != null)
			{
				Array.ForEach(currentAttachedScripts, s => s.Dispose());
				currentAttachedScripts = null;
			}
		} // proc OnBeginReadConfiguration

		protected override void OnEndReadConfiguration(IDEConfigLoading config)
		{
			if (datasetDefinition == null)
				application.RegisterInitializationTask(12000, "Bind documents", BindDataSetDefinitonAsync);

			base.OnEndReadConfiguration(config);
		} // proc OnEndReadConfiguration

		private async Task BindDataSetDefinitonAsync()
		{
			datasetDefinition = application.GetDataSetDefinition(Config.GetAttribute("dataset", String.Empty));
			if (!datasetDefinition.IsInitialized) // initialize dataset functionality
				await datasetDefinition.InitializeAsync();

			// prepare scripts for the the current node
			var luaEngine = this.GetService<IDELuaEngine>(true);

			var list = new List<ILuaAttachedScript>();
			foreach (var scriptId in datasetDefinition.ServerScripts)
				list.Add(luaEngine.AttachScript(scriptId, this, true));
			currentAttachedScripts = list.ToArray();
		} // proc BindDataSetDefinitonAsync

		#endregion

		#region -- Pull -------------------------------------------------------------------

		[LuaMember("Pull")]
		public PpsDataSetServer PullDataSet(long? objectId, Guid? guidId, long? revId)
		{
			var currentUser = application.CreateSysContext(); // DEContext.GetCurrentUser<IPpsPrivateDataContext>();

			// read the raw document
			XDocument documentData;
			string documentType;
			using (var trans = currentUser.CreateTransaction(application.MainDataSource))
			{
				const string baseSql = "select o.Id, o.Guid, o.Typ, o.HeadRevId, o.CurRevId, r.Document from Objk o inner join Objr r";
				var args = new LuaTable();
				if (objectId.HasValue && revId.HasValue)
				{
					args["sql"] = baseSql + " on (o.Id = r.ObjkId) where o.Id = @Id and r.Id = @RevId";
					args[1] = Procs.CreateLuaTable(
						new PropertyValue("Id", objectId.Value),
						new PropertyValue("RevId", revId.Value)
					);
				}
				else if (objectId.HasValue)
				{
					args["sql"] = baseSql + " on (o.HeadRevId = r.Id) where o.Id = @Id";
					args[1] = Procs.CreateLuaTable(new PropertyValue("Id", objectId.Value));
				}
				else if (guidId.HasValue)
				{
					args["sql"] = baseSql + " on (o.HeadRevId = r.Id) where o.Guid = @Guid";
					args[1] = Procs.CreateLuaTable(new PropertyValue("Guid", guidId.Value));
				}
				else
					throw new ArgumentNullException(); // todo:

				var row = trans.ExecuteSingleRow(args);
				if (row != null)
				{
					documentType = row.GetProperty("Typ", String.Empty);
					documentData = row["Document"].ChangeType<XDocument>();
				}
				else
					throw new ArgumentException(); // todo:

				if (documentType != Name)
					throw new ArgumentException(); // todo:

				// create the dataset
				var dataset = (PpsDataSetServer)datasetDefinition.CreateDataSet();
				dataset.Read(documentData.Root);

				// update data fields
				var headTable = dataset.Tables["Head"];
				headTable.First["Id"] = row["Id"];
				headTable.First["Guid"] = row["Guid"];
				headTable.First["Typ"] = Name;
				headTable.First["HeadRevId"] = row["HeadRevId"];
				headTable.First["CurRevId"] = row["CurRevId"];

				// fire triggers
				dataset.OnAfterPull();

				// mark all has orignal
				dataset.Commit();

				return dataset;
			}
		} // func PullDataSet

		[
			DEConfigHttpAction("pull", IsSafeCall = false),
			Description("Reads the revision from the server.")
		]
		private void HttpPullAction(IDEContext ctx, long id, long rev = -1)
		{
			try
			{
				var dataset = PullDataSet(id, null, rev < 0 ? (long?)null : rev);

				// write the content
				using (var tw = ctx.GetOutputTextWriter(MimeTypes.Text.Xml))
				using (var xml = XmlWriter.Create(tw, GetSettings(tw)))
				{
					xml.WriteStartDocument();
					dataset.Write(xml);
					xml.WriteEndDocument();
				}
			}
			catch (Exception e)
			{
				ctx.WriteSafeCall(e);
			}

		} // proc HttpPullAction

		#endregion

		#region -- Push -------------------------------------------------------------------

		private string GetNextNumber(PpsDataTransaction trans, PpsDataSet dataset)
		{
			// is there a lua implementation
			var getNextNumber = this["GetNextNumber"];
			if (Lua.RtInvokeable(getNextNumber))
				return Lua.RtInvoke(getNextNumber, trans, dataset).ChangeType<string>();
			else
			{
				var nrLength = Config.GetAttribute("nrLength", 0);
				if (nrLength <= 0)
					throw new ArgumentException("GetNextNumber is missing."); // todo:

				var format = new String('0', nrLength);

				var args = new LuaTable();
				args["sql"] = "select max(Nr) from dbo.Objk where Typ = @Typ";
				args[1] = Procs.CreateLuaTable(new PropertyValue("Typ", Name));

				var row = trans.ExecuteSingleRow(args);
				var nrNumber = 0L;
				if (row != null && row[0] != null)
					nrNumber = row[0].ChangeType<long>();

				return (++nrNumber).ToString(format);
			}
		} // func GetNextNumber

		[LuaMember("Push")]
		public Tuple<long, long> PushDataSet(PpsDataSetServer dataset)
		{
			var currentUser = DEContext.GetCurrentUser<IPpsPrivateDataContext>();

			// fire triggers
			dataset.OnBeforePush();

			// move all to original row
			dataset.Commit();

			// get the arguments
			var headTable = dataset.Tables["Head", true];
			var firstRow = headTable.First;

			var objectId = (long)firstRow["Id"];
			var guidId = (Guid)firstRow["Guid"];
			var typ = Name;
			var nr = (string)firstRow["Nr"];
			var pulledRevId = firstRow.GetProperty("HeadRevId", -1L);

			// persist the data to the database
			var isNewObject = objectId < 0;
			LuaTable args;
			using (var trans = currentUser.CreateTransaction(application.MainDataSource))
			{
				// check for conflict
				if (isNewObject)
				{
					// check if the guid already exists
					args = new LuaTable();
					args["sql"] = "select o.Id, o.Guid from dbo.Objk o where o.Guid = @Guid";
					args[1] = Procs.CreateLuaTable(new PropertyValue("Guid", guidId));

					var row = trans.ExecuteSingleRow(args);
					if (row != null)
						throw new ArgumentException("todo: Guid exists!");

					// get the new objekt id
					nr = nr ?? GetNextNumber(trans, dataset);

					// create the new object in the database
					args = new LuaTable();
					args["insert"] = "dbo.Objk";
					args[1] = Procs.CreateLuaTable(
						new PropertyValue("Guid", guidId),
						new PropertyValue("Typ", typ),
						new PropertyValue("Nr", nr)
					);
					trans.ExecuteNoneResult(args);

					// update the objectId to the database id
					headTable.First["Id"] = objectId = (long)args[1, "Id"];
					headTable.First["Nr"] = nr;
				}
				else
				{
					// check the head revision and the guid, id, typ combination
					args = new LuaTable();
					args["sql"] = "select o.Guid, o.Typ, o.HeadRevId from dbo.Objk o where o.Id = @Id";
					args[1] = Procs.CreateLuaTable(new PropertyValue("Id", objectId));

					var row = trans.ExecuteSingleRow(args);

					if (row.GetProperty("Guid", Guid.Empty) != guidId)
						throw new ArgumentException(); // todo: passt nett
					if (row.GetProperty("Typ", String.Empty) != typ)
						throw new ArgumentException(); // todo: passt nett

					var headRevId = row.GetProperty("HeadRevId", -1L);
					if (headRevId > pulledRevId)
						throw new ArgumentException("todo: pull first.");
					else if (headRevId < pulledRevId)
						throw new ArgumentException(); // todo: rev error
				}

				// update all local generated id's to server id's
				foreach (var dt in dataset.Tables)
				{
					if (dt.TableDefinition.PrimaryKey == null)
						continue;

					var idxPrimaryKey = dt.TableDefinition.PrimaryKey.Index;
					if (dt.TableDefinition.PrimaryKey.IsIdentity) // auto incr => getnext
					{
						var maxKey = 0L;

						// scan for max key
						foreach (var row in dt)
						{
							var t = row[idxPrimaryKey].ChangeType<long>();
							if (t > maxKey)
								maxKey = t;
						}

						// reverse
						foreach (var row in dt)
						{
							if (row[idxPrimaryKey].ChangeType<long>() < 0)
								row[idxPrimaryKey] = ++maxKey;
						}
					}
					else  // self set => abs(nr)
					{
						// absolute
						foreach (var row in dt)
						{
							var t = row[idxPrimaryKey].ChangeType<long>();
							if (t < 0)
								row[idxPrimaryKey] = Math.Abs(t);
						}
					}
				}

				// commit all to orignal
				dataset.Commit();

				// concert data to string
				var documentRawData = new StringBuilder();
				using (var xml = XmlWriter.Create(documentRawData, Procs.XmlWriterSettings))
				{
					xml.WriteStartDocument();
					dataset.Write(xml);
					xml.WriteEndDocument();
				}

				// create new revision
				args = new LuaTable();
				args["insert"] = "dbo.Objr";
				args[1] = Procs.CreateLuaTable(
					new PropertyValue("ParentId", pulledRevId > 0 ? (object)pulledRevId : null),
					new PropertyValue("ObjkId", objectId),
					new PropertyValue("Tags", PpsObjectTag.FormatTagFields(dataset.GetAutoTags())),
					new PropertyValue("Document", documentRawData.ToString()),
					new PropertyValue("CreateUserId", currentUser.UserId)
				);

				trans.ExecuteNoneResult(args);
				var newRevId = (long)args[1, "Id"];

				// set the new head revision
				args = new LuaTable();
				args["update"] = "dbo.Objk";
				args[1] = Procs.CreateLuaTable(
					new PropertyValue("Id", objectId),
					new PropertyValue("HeadRevId", newRevId)
				);
				trans.ExecuteNoneResult(args);

				// commit sql
				trans.Commit();
				return new Tuple<long, long>(objectId, newRevId);
			}
		} // proc PushDataSet

		[
			DEConfigHttpAction("push", IsSafeCall = false),
			Description("Writes a new revision to the object store.")
		]
		private void HttpPushAction(IDEContext ctx)
		{
			XDocument xDoc;

			try
			{
				// read the data
				using (var xml = XmlReader.Create(ctx.GetInputTextReader(), Procs.XmlReaderSettings))
					xDoc = XDocument.Load(xml);

				// create the the dataset
				var dataset = (PpsDataSetServer)datasetDefinition.CreateDataSet();
				dataset.Read(xDoc.Root);

				// push data to object store
				var newId = PushDataSet(dataset);

				// notify client
				ctx.WriteSafeCall(new XElement("return",
					new XAttribute("id", newId.Item1),
					new XAttribute("revId", newId.Item2)));
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

		[
		DEConfigHttpAction("execute", IsSafeCall = true)
		]
		public void HttpExecuteAction(IDEContext ctx, long id)
		{
			throw new NotImplementedException();
		} // proc HttpExecuteAction

		IEnumerable<PpsApplicationFileItem> IWpfClientApplicationFileProvider.GetApplicationFiles()
		{
			var baseUri = Name + '/';

			// schema
			yield return new PpsApplicationFileItem(baseUri + "schema.xml", -1, datasetDefinition.ConfigurationStamp);

			// related client scripts
			foreach (var c in datasetDefinition.ClientScripts)
			{
				var fi = new FileInfo(c);
				if (fi.Exists)
					yield return new PpsApplicationFileItem(baseUri + fi.Name, fi.Length, fi.LastWriteTimeUtc);
			}
		} // func GetApplicationFiles

		private bool GetDatasetResourceFile(string relativeSubPath, out FileInfo fi)
		{
			foreach (var c in datasetDefinition.ClientScripts)
			{
				if (String.Compare(relativeSubPath, Path.GetFileName(c), StringComparison.OrdinalIgnoreCase) == 0)
				{
					fi = new FileInfo(c);
					return true;
				}
			}
			fi = null;
			return false;
		} // func GetDatasetResourceFile

		protected override bool OnProcessRequest(IDEContext r)
		{
			FileInfo fi;
			if (r.RelativeSubPath == "schema.xml")
			{
				datasetDefinition.WriteToDEContext(r, ConfigPath + "/schema.xml");
				return true;
			}
			else if (GetDatasetResourceFile(r.RelativeSubPath, out fi))
			{
				r.WriteFile(fi.FullName);
				return true;
			}
			return base.OnProcessRequest(r);
		} // proc OnProcessRequest

		[LuaMember(nameof(DataSetDefinition))]
		public PpsDataSetServerDefinition DataSetDefinition => datasetDefinition;
	} // class PpsDocument

	#endregion
}
