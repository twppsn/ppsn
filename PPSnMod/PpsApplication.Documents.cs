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
		DEConfigHttpAction("pull", IsSafeCall = true)
		]
		public void HttpLoadAction(IDEContext ctx)
		{
			// prepare load
			var dataset = datasetDefinition.CreateDataSet();

			// create the arguments and call the lua load function
			var args = new object[] { dataset, new LuaPropertiesTable(ctx) };

			var loadableDataset = dataset as IPpsLoadableDataSet;

			loadableDataset?.OnBeforeLoad(ctx);
			CallTableMethods("BeforeLoad", args);
			loadableDataset?.OnLoad(ctx);
			CallTableMethods("Load", args);
			loadableDataset?.OnAfterLoad(ctx);
			CallTableMethods("AfterLoad", args);

			// send a clean document
			dataset.Commit();

			using (var tw = ctx.GetOutputTextWriter(MimeTypes.Text.Xml))
			using (var xml = XmlWriter.Create(tw, GetSettings(tw)))
			{
				xml.WriteStartDocument();
				dataset.Write(xml);
				xml.WriteEndDocument();
			}
		} // proc HttpLoadAction

		[
		DEConfigHttpAction("push", IsSafeCall = true)
		]
		public void HttpSaveAction()
		{
		} // proc HttpSaveAction

		[
		DEConfigHttpAction("execute", IsSafeCall = true)
		]
		public void HttpExecuteAction()
		{
		} // proc HttpExecuteAction

		protected override bool OnProcessRequest(IDEContext r)
		{
			if (r.RelativeSubPath == "schema.xml")
			{
				r.WriteObject(datasetDefinition.WriteSchema(new XElement("schema")), MimeTypes.Text.Xml);
				return true;
			}
			return base.OnProcessRequest(r);
		} // proc OnProcessRequest
	} // class PpsDocument

	#endregion
}
