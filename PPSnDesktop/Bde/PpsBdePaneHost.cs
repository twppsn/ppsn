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
using System.Threading.Tasks;
using System.Windows;
using TecWare.PPSn.UI;

namespace TecWare.PPSn.Bde
{
	internal sealed class PpsBdePaneHost : PpsPaneHost
	{
		public PpsBdePaneHost()
		{
		} // ctor

		/// <summary>Close top most pane.</summary>
		/// <returns></returns>
		public override Task<bool> ClosePaneAsync()
			=> Window.PopPaneAsync(this);

		/// <summary>Access main window</summary>
		private PpsBdeWindow Window => (PpsBdeWindow)PaneManager;

		static PpsBdePaneHost()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PpsBdePaneHost), new FrameworkPropertyMetadata(typeof(PpsBdePaneHost)));
		}
	} // class PpsBdePaneHost
}
