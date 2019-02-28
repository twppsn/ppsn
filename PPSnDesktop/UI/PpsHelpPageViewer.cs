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
using System.Windows;
using System.Windows.Controls;

namespace TecWare.PPSn.UI
{
	public class PpsHelpPageViewer : Control
	{
		#region -- HelpKey - Property -------------------------------------------------

		public static readonly DependencyProperty HelpKeyProperty = DependencyProperty.Register(nameof(HelpKey), typeof(string), typeof(PpsHelpPageViewer), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnHelpKeyChanged)));

		private static void OnHelpKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsHelpPageViewer)d).OnHelpKeyChanged((string)e.NewValue);

		private void OnHelpKeyChanged(string newValue)
		{
			//helpPage = newValue;
		} // proc OnHelpKeyChanged

		public string HelpKey { get => (string)GetValue(HelpKeyProperty); set => SetValue(HelpKeyProperty, value); }

		#endregion

		static PpsHelpPageViewer()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PpsHelpPageViewer), new FrameworkPropertyMetadata(typeof(PpsHelpPageViewer)));
		}
	} //class PpsHelpPageViewer
}
