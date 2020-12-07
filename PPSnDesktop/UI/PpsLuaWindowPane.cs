#region -- copyright -
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
using Neo.IronLua;
using TecWare.DE.Networking;
using TecWare.DE.Stuff;
using TecWare.PPSn.Controls;

namespace TecWare.PPSn.UI
{
	internal sealed class PpsLuaWindowPane : PpsWindowPaneControl
	{
		#region -- class PpsLuaWindowPaneCode -----------------------------------------

		private sealed class PpsLuaWindowPaneCode : PpsLuaCodeBehind
		{
			private readonly PpsLuaWindowPane pane;

			public PpsLuaWindowPaneCode(PpsLuaWindowPane pane, Uri baseUri)
				: base(pane.Shell.GetService<IPpsLuaShell>(true), baseUri)
			{
				this.pane = pane ?? throw new ArgumentNullException(nameof(pane));
			} // ctor

			[LuaMember]
			public PpsWindowPaneControl Pane => pane;
		} // class PpsLuaWindowPaneCode

		#endregion

		#region -- Ctor/Dtor ----------------------------------------------------------

		private PpsLuaWindowPaneCode code = null;

		public PpsLuaWindowPane(IPpsWindowPaneHost paneHost)
			: base(paneHost)
		{
		} // ctor

		protected override async Task OnLoadAsync(LuaTable args)
		{
			await base.OnLoadAsync(args);

			var path = args.GetMemberValue("uri")?.ToString();
			if (String.IsNullOrEmpty(path))
				throw new ArgumentNullException("uri");

			// build base uri
			var baseUri = Shell.Http.CreateFullUri(path);

			// create code
			code = new PpsLuaWindowPaneCode(this, baseUri);

			// load layout
			using (var response = await Shell.Http.GetAsync(baseUri))
			using (var tr = await response.GetTextReaderAsync(MimeTypes.Application.Xaml))
			{
				await PpsXamlParser.LoadAsync<PpsWindowPaneControl>(tr,
					new PpsXamlReaderSettings() { BaseUri = baseUri, Code = code, ServiceProvider = Shell },
					new System.Xaml.XamlObjectWriterSettings() { RootObjectInstance = this }
				);
			}

			// init load code
			((IPpsLuaCodeBehind)code).OnControlCreated(this, args);
		} // proc OnLoadAsync

		protected override Task<bool> OnUnloadAsync(bool? commit)
		{
			return base.OnUnloadAsync(commit);
		} // proc OnUnloadAsync

		#endregion
	} // class PpsLuaWindowPaneControl
}
