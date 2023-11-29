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
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace TecWare.PPSn.Controls
{
	public class PpsMenuControl : MenuBase
	{
		protected override void OnKeyDown(KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
			{
				var popup = this.GetLogicalParent<Popup>();
				if (popup != null)
					popup.IsOpen = false;
				e.Handled = true;
			}
			base.OnKeyDown(e);
		} // func OnKeyDown

		static PpsMenuControl()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PpsMenuControl), new FrameworkPropertyMetadata(typeof(PpsMenuControl)));
		}
	} // class PpsMenuControl

	public class PpsMenuItemContainerSelector : ItemContainerTemplateSelector
	{
		public override DataTemplate SelectTemplate(object item, ItemsControl parentItemsControl)
		{
			if (item is null)
				return NullItemTemplate;
			else
				return MenuItemTemplate;
		} // func SelectTemplate

		public DataTemplate MenuItemTemplate { get; set; }
		public DataTemplate NullItemTemplate { get; set; }
	} // class PpsMenuItemContainerSelector
}
