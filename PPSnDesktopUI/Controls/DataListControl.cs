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
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Neo.IronLua;
using TecWare.PPSn.Data;

namespace TecWare.PPSn.Controls
{
	#region -- class PpsDataListTemplateSelector ----------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PpsDataListTemplateSelector : DataTemplateSelector
	{
		private PpsEnvironment environment;

		public PpsDataListTemplateSelector(PpsEnvironment environment)
		{
			this.environment = environment;
		} // ctor

		public override DataTemplate SelectTemplate(object item, DependencyObject container)
		{
			var table = item as LuaTable;
			var key = table != null ? table.GetMemberValue("Typ") : ((dynamic)item).Typ;
			if (key == null)
				return null;

			var typeDef = environment.DataListItemTypes[key];
			return typeDef?.FindTemplate(item);
		} // proc SelectTemplate
	} // class PpsDataListTemplateSelector

	#endregion

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Spezial Control to display data list</summary>
	public class DataListControl : ListBox
	{
		public DataListControl()
		{
			// Create a virtual listbox
			SetValue(HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch);
			SetValue(VirtualizingStackPanel.IsVirtualizingProperty, true);
			SetValue(VirtualizingStackPanel.VirtualizationModeProperty, VirtualizationMode.Recycling);
			SetValue(ScrollViewer.IsDeferredScrollingEnabledProperty, true);
		} // ctor

		//private bool ShowClassContextMenu(dynamic item)
		//{
		//	if (item == null)
		//		return false;

		//	string sTyp = item.OBJKTYP as string;
		//	if (String.IsNullOrEmpty(sTyp))
		//		return false;

		//	dynamic cls = GetPpsData().Classes[sTyp];
		//	ContextMenu menu = new ContextMenu();
		//	foreach (PpsDataAction action in cls.Actions)
		//	{
		//		menu.Items.Add(new MenuItem() { Header = action.Name });
		//	}
		//	menu.PlacementTarget = this;
		//	menu.IsOpen = true;

		//	return true;
		//} // proc ShowClassContextMenu

		protected override void OnKeyUp(KeyEventArgs e)
		{
			//if (e.Key == Key.Space)
			//	e.Handled = ShowClassContextMenu(this.SelectedItem);
			base.OnKeyUp(e);
		} // proc OnKeyUp

		protected override void OnItemsSourceChanged(IEnumerable oldValue, IEnumerable newValue)
		{
			if (this.ItemTemplateSelector == null)
				this.ItemTemplateSelector = PpsEnvironment.GetEnvironment(this).DataListTemplateSelector;

			base.OnItemsSourceChanged(oldValue, newValue);
		} // proc OnItemsSourceChanged

		//protected override void OnMouseDoubleClick(MouseButtonEventArgs e)
		//{
		//	if (e.LeftButton == MouseButtonState.Pressed || e.RightButton == MouseButtonState.Pressed)
		//		e.Handled = ShowClassContextMenu(this.SelectedItem);
		//	base.OnMouseDoubleClick(e);
		//} // proc OnMouseDoubleClick

		//protected override void OnContextMenuOpening(ContextMenuEventArgs e)
		//{
		//	e.Handled = ShowClassContextMenu(this.SelectedItem);
		//	base.OnContextMenuOpening(e);
		//} // proc OnContextMenuOpening
	} // class DataListControl
}
