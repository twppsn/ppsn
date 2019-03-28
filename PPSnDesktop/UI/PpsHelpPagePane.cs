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
using System.Threading.Tasks;

namespace TecWare.PPSn.UI
{
	internal class PpsHelpPagePane : PpsMarkdownPane
	{
		public PpsHelpPagePane(IPpsWindowPaneHost paneHost) 
			: base(paneHost)
		{
			Commands.AddButton("100;100", "save",
				PublishCommand,
				"Veröffentlichen", "Änderungen für alle freigeben"
			);

			this.AddCommandBinding(Shell, PublishCommand,
				new PpsAsyncCommand(
					ctx => PublishHelpPageAsync(),
					ctx => IsLocalChanged
				)
			);

		} // ctor

		private Task PublishHelpPageAsync()
			=> CurrentObject?.PushAsync();

		private bool IsLocalChanged => CurrentObject?.IsDocumentChanged ?? false;
		private PpsObject CurrentObject => (PpsObject)CurrentData;
	} // class PpsHelpPagePane
}
