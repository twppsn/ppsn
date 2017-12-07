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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace TecWare.PPSn.UI
{
	#region -- class PpsNavigatorControl ----------------------------------------------

	public partial class PpsNavigatorControl : UserControl
	{
		private static readonly DependencyPropertyKey NavigatorModelPropertyKey = DependencyProperty.RegisterReadOnly(nameof(NavigatorModel), typeof(PpsNavigatorModel), typeof(PpsNavigatorControl), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty NavigatorModelProperty = NavigatorModelPropertyKey.DependencyProperty;

		#region -- ctor / init --------------------------------------------------------

		public PpsNavigatorControl()
		{
			InitializeComponent();
		} // ctor

		public void Init(PpsMainWindow windowModel)
		{
			SetValue(NavigatorModelPropertyKey, new PpsNavigatorModel(windowModel));
			DataContext = NavigatorModel;
		} // proc Init

		#endregion

		public void OnPreview_MouseDown(object originalSource)
		{
			if (!ElementIsChildOfSearchBox(originalSource))
				CollapseSearchBox();
		}

		public void OnPreview_TextInput(TextCompositionEventArgs e)
		{
			if (!Object.Equals(e.OriginalSource, PART_SearchBox))
				e.Handled = ExpandSearchBox(e.Text);
		}

		public PpsNavigatorModel NavigatorModel => (PpsNavigatorModel)GetValue(NavigatorModelProperty);

		#region -- SearchBoxHandling --------------------------------------------------

		private void PART_SearchBox_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Enter)
			{
				var expr = BindingOperations.GetBindingExpression(PART_SearchBox, TextBox.TextProperty);
				if (expr != null)
					expr.UpdateSource();
				NavigatorModel.ExecuteCurrentSearchText();
			}
		}

		private bool ExpandSearchBox(string input)
		{
			if (String.IsNullOrEmpty(input) || (PpsNavigatorSearchBoxState)PART_SearchBox.Tag == PpsNavigatorSearchBoxState.Expanded)
				return false;
			if (!IsValidSearchText(input))
				return false;
			PART_SearchBox.Text += input;
			PART_SearchBox.Select(PART_SearchBox.Text.Length, 0);
			FocusManager.SetFocusedElement(PART_NavigatorGrid, PART_SearchBox);
			Keyboard.Focus(PART_SearchBox);
			return true;
		}

		private void CollapseSearchBox()
		{
			if (PART_SearchBox.Visibility != Visibility.Visible || (PpsNavigatorSearchBoxState)PART_SearchBox.Tag == PpsNavigatorSearchBoxState.Collapsed)
				return;
			FocusManager.SetFocusedElement(PART_NavigatorGrid, PART_Views);
			Keyboard.Focus(PART_Views);
		}

		private bool IsValidSearchText(string input)
		{
			var c = input[0];
			return !Char.IsControl(c);
		}

		private bool ElementIsChildOfSearchBox(object element)
		{
			var o = element as DependencyObject;
			if (o == null)
				return false;
			return FindChild(PART_SearchBox, o);
		} // func ElementIsChildOfSearchBox

		private bool FindChild(DependencyObject parent, DependencyObject o)
		{
			int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
			for (int i = 0; i < childrenCount; i++)
			{
				var child = VisualTreeHelper.GetChild(parent, i);
				if (child.Equals(o))
					return true;
				if (FindChild(child, o))
					return true;
			}
			return false;
		} // func FindChild

		#endregion
	} // class PpsNavigatorControl

	#endregion

	#region -- class PPSnNavigatorPriorityToDockPosition ------------------------------

	/// <summary>
	/// Converter zum Ermitteln der Dockimg Position aus der Priority Property eines ActionCommands
	/// </summary>
	internal class PPSnNavigatorPriorityToDockPosition : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			if (value is int && (int)value < 0)
			{
				return Dock.Right;
			}
			return Dock.Left;
		}

		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			return DependencyProperty.UnsetValue;
		}
	} // class PPSnNavigatorPriorityToDockPosition

	#endregion

	#region -- enum PpsNavigatorSearchBoxState ----------------------------------------

	internal enum PpsNavigatorSearchBoxState
	{
		Expanded,
		Collapsed
	} // enum PpsNavigatorSearchBoxState

	#endregion
}
