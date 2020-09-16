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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Animation;
using TecWare.PPSn.Controls;

namespace TecWare.PPSn.UI
{
	/// <summary>Popup-Control to for barcode simulation.</summary>
	public class PpsBarcodePopup : PpsPopup
	{
		/// <summary>Send barcode to an window.</summary>
		public readonly static RoutedCommand SendBarcodeCommand = new RoutedCommand();
		/// <summary>Remove stored barcode.</summary>
		public readonly static RoutedCommand RemoveBarcodeCommand = new RoutedCommand();

		/// <summary></summary>
		public PpsBarcodePopup()
		{
			AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(Button_Click));
		} // ctor

		/// <inheritdoc/>
		protected override void OnInitialized(EventArgs e)
		{
			base.OnInitialized(e);

			var provider = this.GetControlService<IPpsWindowBarcodeProvider>(false);
			if (provider != null)
				provider.AddCommandBinding(this);

			DataContext = provider;
		} // proc OnInitialized

		private void Button_Click(object sender, RoutedEventArgs e)
		{
			if (e.OriginalSource is FrameworkElement o && o.CompareName("sendBarcodeButton") == 0)
				PpsControlCommands.ClosePopupCommand.Execute(null, o);
		} // event Button_Click

		static PpsBarcodePopup()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PpsBarcodePopup), new FrameworkPropertyMetadata(typeof(PpsBarcodePopup)));
		} // ctor
	} // class BarcodePopup
}
