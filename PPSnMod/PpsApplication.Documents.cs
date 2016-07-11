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
using TecWare.DE.Networking;
using TecWare.DE.Server;
using TecWare.DE.Server.Http;
using TecWare.DE.Stuff;
using TecWare.PPSn.Server.Data;

namespace TecWare.PPSn.Server
{
	#region -- class PpsDocument --------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsDocument : DEConfigItem
	{
		private readonly PpsApplication application;
		private PpsDataSetServerDefinition datasetDefinition = null;

		#region -- Ctor/Dtor --------------------------------------------------------------

		public PpsDocument(IServiceProvider sp, string name)
			: base(sp, name)
		{
			this.application = sp.GetService<PpsApplication>(true);
		} // ctor

		protected override void OnEndReadConfiguration(IDEConfigLoading config)
		{
			if (datasetDefinition == null)
				application.RegisterInitializationTask(12000, "Bind documents", BindDataSetDefinitonAsync);

			base.OnEndReadConfiguration(config);
		} // proc OnEndReadConfiguration

		private async Task BindDataSetDefinitonAsync()
		{
			datasetDefinition = application.GetDataSetDefinition(Config.GetAttribute("dataset", String.Empty));
			if (!datasetDefinition.IsInitialized)
			{
				// initialize dataset functionality
				await datasetDefinition.InitializeAsync();

				// prepare current node for the dataset
				// run scripts

			}
		} // proc BindDataSetDefinitonAsync

		#endregion

		[
			DEConfigHttpAction("pull", IsSafeCall = true),
			Description("Reads the revision from the server.")
		]
		public void HttpLoadAction(IDEContext ctx, long id, long rev = -1)
		{
			// lädt revision aus object tabelle
			throw new NotImplementedException();


			//// prepare load
			//var dataset = datasetDefinition.CreateDataSet();

			//// create the arguments and call the lua load function
			//var args = new object[] { dataset, new LuaPropertiesTable(ctx) };

			//var loadableDataset = dataset as IPpsLoadableDataSet;

			//loadableDataset?.OnBeforeLoad(ctx);
			//CallTableMethods("BeforeLoad", args);
			//loadableDataset?.OnLoad(ctx);
			//CallTableMethods("Load", args);
			//loadableDataset?.OnAfterLoad(ctx);
			//CallTableMethods("AfterLoad", args);

			//// send a clean document
			//dataset.Commit();

			//using (var tw = ctx.GetOutputTextWriter(MimeTypes.Text.Xml))
			//using (var xml = XmlWriter.Create(tw, GetSettings(tw)))
			//{
			//	xml.WriteStartDocument();
			//	dataset.Write(xml);
			//	xml.WriteEndDocument();
			//}
		} // proc HttpLoadAction

		[
		DEConfigHttpAction("push", IsSafeCall = true)
		]
		public void HttpSaveAction(IDEContext ctx, long id)
		{
			throw new NotImplementedException();
		} // proc HttpSaveAction

		[
		DEConfigHttpAction("execute", IsSafeCall = true)
		]
		public void HttpExecuteAction(IDEContext ctx, long id)
		{
			throw new NotImplementedException();
		} // proc HttpExecuteAction

		public IEnumerable<PpsApplicationFileItem> GetClientFiles()
		{
			// schema
			yield return new PpsApplicationFileItem("schema.xml", -1, datasetDefinition.ConfigurationStamp);

			// related client scripts
			foreach (var c in datasetDefinition.ClientScripts)
			{
				var fi = new FileInfo(c);
				if (fi.Exists)
					yield return new PpsApplicationFileItem(fi.Name, fi.Length, fi.LastWriteTime);
			}
		} // func GetClientFiles

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
				r.SetLastModified(datasetDefinition.ConfigurationStamp);

				r.WriteContent(() =>
					{
						var xSchema = new XElement("schema");
						datasetDefinition.WriteSchema(xSchema);

						var dst = new MemoryStream();
						var xmlSettings = Procs.XmlWriterSettings;
						xmlSettings.CloseOutput = false;
						using (var xml = XmlWriter.Create(dst, xmlSettings))
							xSchema.WriteTo(xml);

						dst.Position = 0;
						return dst;
					}, ConfigPath + "/schema.xml", MimeTypes.Text.Xml
				);
				return true;
			}
			else if (GetDatasetResourceFile(r.RelativeSubPath, out fi))
			{
				r.WriteFile(fi.FullName);
				return true;
			}
			return base.OnProcessRequest(r);
		} // proc OnProcessRequest
	} // class PpsDocument

	#endregion
}
