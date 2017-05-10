﻿#region -- copyright --
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
	public sealed class PpsDocumentItem : PpsObjectItem<PpsDataSetServer>, IWpfClientApplicationFileProvider
	{

		private PpsDataSetServerDefinition datasetDefinition = null;
		private ILuaAttachedScript[] currentAttachedScripts = null;

		#region -- Ctor/Dtor --------------------------------------------------------------

		public PpsDocumentItem(IServiceProvider sp, string name)
			: base(sp, name)
		{
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
				Application.RegisterInitializationTask(12000, "Bind documents", BindDataSetDefinitonAsync);

			base.OnEndReadConfiguration(config);
		} // proc OnEndReadConfiguration

		private async Task BindDataSetDefinitonAsync()
		{
			datasetDefinition = Application.GetDataSetDefinition(Config.GetAttribute("dataset", String.Empty));
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
		
		#region -- Push/Pull --------------------------------------------------------------

		protected override void WriteDataToStream(PpsDataSetServer data, Stream dst)
		{
			using (var xml = XmlWriter.Create(dst, Procs.XmlWriterSettings))
			{
				xml.WriteStartDocument();
				data.Write(xml);
				xml.WriteEndDocument();
			}
		} // proc WriteDataToStream

		protected override PpsDataSetServer GetDataFromStream(Stream src)
		{
			var data = (PpsDataSetServer)datasetDefinition.CreateDataSet();
			using (var xml = XmlReader.Create(src, Procs.XmlReaderSettings))
				data.Read(XDocument.Load(xml).Root);
			return data;
		} // func GetDataFromStream

		protected override PpsDataSetServer PullData(PpsDataTransaction trans, PpsObjectAccess obj)
		{
			// get the head or given revision
			// todo: create rev, if not exists
			var xDocumentData = XDocument.Parse(obj.GetText());

			// create the dataset
			var data = (PpsDataSetServer)datasetDefinition.CreateDataSet();
			data.Read(xDocumentData.Root);

			// correct id and revision
			CheckHeadObjectId(obj, data);

			// fire triggers
			data.OnAfterPull();

			// mark all has orignal
			data.Commit();

			return data;
		} // func PullData

		protected override bool PushData(PpsDataTransaction transaction, PpsObjectAccess obj, PpsDataSetServer data)
		{
			// fire triggers
			data.OnBeforePush();

			// move all to original row
			data.Commit();

			if (obj.IsNew)
				InsertNewObject(transaction, obj, data);
			else // check rev, to in base implementation?
			{
				var headRevId = obj.HeadRevId;
				if (headRevId > obj.RevId)
					return false; // head revision is newer than pulled revision -> return this fact
				else if (headRevId < obj.RevId)
					throw new ArgumentException($"Push failed. Pulled revision is greater than head revision.");
			}

			// update head id
			CheckHeadObjectId(obj, data);

			// update all local generated id's to server id's
			foreach (var dt in data.Tables)
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
			data.Commit();

			obj.UpdateData(new Action<Stream>(dst => WriteDataToStream(data, dst)));
			obj.Update();

			return true;
		} // func PushData

		#endregion

		[
		DEConfigHttpAction("execute", IsSafeCall = true)
		]
		public void HttpExecuteAction(IDEContext ctx, long id)
		{
			throw new NotImplementedException();
		} // proc HttpExecuteAction

		#region -- Application Files ----------------------------------------------------

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
			if (r.RelativeSubPath == "schema.xml")
			{
				datasetDefinition.WriteToDEContext(r, ConfigPath + "/schema.xml");
				return true;
			}
			else if (GetDatasetResourceFile(r.RelativeSubPath, out var fi))
			{
				r.WriteFile(fi.FullName);
				return true;
			}
			return base.OnProcessRequest(r);
		} // proc OnProcessRequest

	#endregion

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
