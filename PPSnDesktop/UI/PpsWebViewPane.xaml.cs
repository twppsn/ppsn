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
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Data;
using Neo.IronLua;
using TecWare.DE.Stuff;
using TecWare.PPSn.Controls;

namespace TecWare.PPSn.UI
{
	internal partial class PpsWebViewPane : PpsWindowPaneControl, IPpsWindowPaneBack
	{
		private readonly IPpsVirtualKeyboard virtualKeyboard;

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

			virtualKeyboard = paneHost.GetService<IPpsVirtualKeyboard>(false);

			if (virtualKeyboard != null)
				Commands.AddButton("-100;100", "keyboard", new PpsCommand(ToggleKeyboard), null, " Zeigt die virtuelle Tastatur an.");
		} // ctor

		private static object GetUriArgument(LuaTable args)
			=> args["uri"];

		protected override PpsWindowPaneCompareResult CompareArguments(LuaTable args)
		{
			var uriArg = GetUriArgument(args);
			return webView.History.Count > 0 && uriArg is Uri uri && webView.History[0].Uri == uri ? PpsWindowPaneCompareResult.Same : PpsWindowPaneCompareResult.Reload;
		} // func CompareArguments

		protected override async Task OnLoadAsync(LuaTable args)
		{
			await base.OnLoadAsync(args);

			webView.Source = GetUriArgument(args);
		} // proc OnLoadAsync

		public void InvokeBackButton()
			=> webView.GoBackAsync().OnException();

		private void ToggleKeyboard(PpsCommandContext obj)
		{
			if (virtualKeyboard.IsVisible)
				virtualKeyboard.Hide();
			else
				virtualKeyboard.Show();
		} // proc ToggleKeyboard

		public bool? CanBackButton => webView.CanGoBack ? (bool?)true : null;

		#region -- TitleConverter -----------------------------------------------------

		private sealed class TitleConverterImplementation : IMultiValueConverter
		{
			public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
			{
				var title = values[0] as string;
				var isNavigating = BooleanBox.GetBool(values[1]);

				if (String.IsNullOrEmpty(title))
					return isNavigating ? "Lade..." : "Leer";
				else
					return isNavigating ? String.Format("Lade {0}", title) : title;
			} // func Convert

			public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
				=> throw new NotSupportedException();
		} // class TitleConverterImplementation

		public static IMultiValueConverter TitleConverter { get; } = new TitleConverterImplementation();

		#endregion
	} // class PpsWebViewPane
}
