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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Neo.IronLua;
using TecWare.PPSn.Controls;

namespace TecWare.PPSn.UI
{
	internal partial class PpsWebViewPane : PpsWindowPaneControl, IPpsWindowPaneBack
	{
		public PpsWebViewPane(IPpsWindowPaneHost paneHost)
			: base(paneHost)
		{
			InitializeComponent();

			if (paneHost.PaneManager is Bde.IPpsBdeManager)
			{
				goBackButton.IsVisible = false;
				goForwardButton.IsVisible = false;
			}

			var shell = paneHost.PaneManager.Shell;
			webView.AddCommandBinding(shell, CommandBindings);
		} // ctor

		protected override async Task OnLoadAsync(LuaTable args)
		{
			await base.OnLoadAsync(args);

			webView.Source = args["uri"];
		} // proc OnLoadAsync

		public void InvokeBackButton()
			=> webView.GoBackAsync().OnException();

		public bool? CanBackButton => webView.CanGoBack ? (bool?)true : null;
	} // class PpsWebViewPane
}
