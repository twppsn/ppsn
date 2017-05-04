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
		private LuaResult LuaPullDataSet(PpsDataTransaction transaction, long? objectId, Guid? guidId, long? revId)
		{
			var r = PullDataSet(transaction, objectId, guidId, revId);
			return new LuaResult(r.dataset, r.obj);
		} // func PullDataSet

		private (PpsDataSetServer dataset, PpsObjectAccess obj) PullDataSet(PpsDataTransaction transaction, long? objectId, Guid? guidId, long? revId)
		{
			// get object data
			var obj = application.Objects.GetObject(transaction,
				new LuaTable
				{
						{ nameof(PpsObjectAccess.Id), objectId.HasValue ? (object)objectId.Value : null },
						{ nameof(PpsObjectAccess.Guid), guidId.HasValue ? (object)guidId.Value : null },
						{ nameof(PpsObjectAccess.RevId), revId.HasValue ? (object)revId.Value : null },
				}
			);

			// get the head or given revision
			// todo: create rev, if not exists
			var xDocumentData = XDocument.Parse(obj.GetText());

			// create the dataset
			var dataset = (PpsDataSetServer)datasetDefinition.CreateDataSet();
			dataset.Read(xDocumentData.Root);

			// correct id and revision
			CheckHeadObjectId(obj, dataset);

			// fire triggers
			dataset.OnAfterPull();

			// mark all has orignal
			dataset.Commit();

			return (dataset, obj);
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
					var (dataset, obj) = PullDataSet(trans, id, null, rev < 0 ? (long?)null : rev);

					// prepare object data
					var headerBytes = Encoding.Unicode.GetBytes(obj.ToXml().ToString(SaveOptions.DisableFormatting));
					ctx.OutputHeaders["ppsn-header-length"] = headerBytes.Length.ChangeType<string>();

					// write the content
					using (var dst = ctx.GetOutputStream(MimeTypes.Application.OctetStream))
					{
						dst.Write(headerBytes, 0, headerBytes.Length);

						using (var tw = new StreamWriter(dst, Encoding.Unicode))
						using (var xml = XmlWriter.Create(tw, GetSettings(tw)))
						{
							// write dataset
							xml.WriteStartDocument();
							dataset.Write(xml);
							xml.WriteEndDocument();
						}
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

		#region -- Push -------------------------------------------------------------------

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

		[LuaMember("Push")]
		public bool PushDataSet(PpsDataTransaction transaction, PpsObjectAccess obj, PpsDataSetServer dataset)
		{
			// fire triggers
			dataset.OnBeforePush();

			// move all to original row
			dataset.Commit();

			if (obj.IsNew)
			{
				// set the object number for new objects
				var nextNumber = GetNextNumberMethod();
				if (nextNumber == null && obj.Nr == null) // no next number and no number --> error
					throw new ArgumentException($"The field 'Nr' is null or no nextNumber is given.");
				else if (Config.GetAttribute("forceNextNumber", false) || obj.Nr == null) // force the next number or there is no number
					obj["Nr"] = application.Objects.GetNextNumber(transaction, obj.Typ, nextNumber, dataset);
				else  // check the number format
					application.Objects.ValidateNumber(obj.Nr, nextNumber, dataset);
			}
			else
			{
				var headRevId = obj.HeadRevId;
				if (headRevId > obj.RevId)
					return false; // head revision is newer than pulled revision -> return this fact
				else if (headRevId < obj.RevId)
					throw new ArgumentException($"Push failed. Pulled revision is greater than head revision.");
			}

			// update head id
			CheckHeadObjectId(obj, dataset);

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

			// create obj data
			if (obj.IsNew)
				obj.Update(true);

			// create rev data
			obj.UpdateData(
				new Action<Stream>(dst =>
				{
					using (var xml = XmlWriter.Create(dst, Procs.XmlWriterSettings))
					{
						xml.WriteStartDocument();
						dataset.Write(xml);
						xml.WriteEndDocument();
					}
				})
			);

			// update
			obj.Update();

			return true;
		} // proc PushDataSet

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

					// create and load the dataset
					var dataset = (PpsDataSetServer)datasetDefinition.CreateDataSet();
					using (var xml = XmlReader.Create(src, Procs.XmlReaderSettings))
						dataset.Read(XDocument.Load(xml).Root);

					// set IsRev
					if (obj.IsNew)
						obj.IsRev = datasetDefinition.Meta.GetProperty("IsRev", false);

					// push dataset in the database
					if (PushDataSet(transaction, obj, dataset))
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

		private static void CheckHeadObjectId(PpsObjectAccess obj, PpsDataSetServer dataset)
		{
			var headTable = dataset.Tables["Head"];
			if (headTable != null)
			{
				var firstRow = headTable.First;
				if (firstRow != null)
				{
					var columnId = headTable.Columns.FirstOrDefault(c => String.Compare(c.Name, "Id", StringComparison.OrdinalIgnoreCase) == 0);
					var columnRevId = headTable.Columns.FirstOrDefault(c => String.Compare(c.Name, "RevId", StringComparison.OrdinalIgnoreCase) == 0);
					if (columnId != null)
						firstRow[columnId.Index] = obj.Id;
					if (columnRevId != null)
						firstRow[columnRevId.Index] = obj.RevId;
				}
			}
		} // proc CheckHeadObjectId
	} // class PpsDocument

	#endregion
}
