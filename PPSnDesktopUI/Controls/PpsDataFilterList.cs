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
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TecWare.PPSn.Controls
{
	/// <summary>This Control shows a List of its Items with an applied Filter</summary>
	
	public class PpsDataFilterList : PpsDataFilterBase
	{
		static PpsDataFilterList()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PpsDataFilterList), new FrameworkPropertyMetadata(typeof(PpsDataFilterList)));
		}

		public override void OnApplyTemplate()
		{
			base.OnApplyTemplate();

			filteredListBox.Items.CurrentChanged += Items_CurrentChanged;
			SetAnchorItem();
		}


		~PpsDataFilterList()
			{
			filteredListBox.Items.CurrentChanged -= Items_CurrentChanged;
			// leave clean
			ClearFilter();
		}

		public override void HideFilteredList(bool commit)
		{
			
		}

		protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
		{
			base.OnMouseLeftButtonUp(e);
			base.ApplySelectedItem();
		}

		public override bool IsFilteredListVisible()
		=> true;
		/*
protected override void OnPreviewKeyDown(KeyEventArgs e)
{
	if (e.Handled)
		return;

	switch (e.Key)
	{
		case Key.Prior:
		case Key.Up:
			if (filteredListBox.SelectedIndex < 0)
			{
				filteredListBox.SelectedIndex = filteredListBox.Items.Count - 1;
				filteredListBox.ScrollIntoView(filteredListBox.SelectedItem);
				e.Handled = true;
			}
			else if (filteredListBox.SelectedIndex > 0)
			{
				filteredListBox.SelectedIndex--;
				filteredListBox.ScrollIntoView(filteredListBox.SelectedItem);
				e.Handled = true;
			}
			break;
		case Key.Next:
		case Key.Down:
			if (filteredListBox.SelectedIndex < 0)
			{
				filteredListBox.SelectedIndex = 0;
				filteredListBox.ScrollIntoView(filteredListBox.SelectedItem);
				e.Handled = true;
			}
			else if (filteredListBox.SelectedIndex < (filteredListBox.Items.Count - 1))
			{
				filteredListBox.SelectedIndex++;
				filteredListBox.ScrollIntoView(filteredListBox.SelectedItem);
				e.Handled = true;
			}
			break;
		case Key.End:
			filteredListBox.SelectedIndex = filteredListBox.Items.Count - 1;
			filteredListBox.ScrollIntoView(filteredListBox.SelectedItem);
			e.Handled = true;
			break;
		case Key.Home:
			filteredListBox.SelectedIndex = 0;
			filteredListBox.ScrollIntoView(filteredListBox.SelectedItem);
			e.Handled = true;
			break;				
	}

	base.OnPreviewKeyDown(e);
}
*/
	}

}
