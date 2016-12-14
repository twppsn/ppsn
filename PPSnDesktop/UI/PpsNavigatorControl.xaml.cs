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
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace TecWare.PPSn.UI
{
	/// <summary>
	/// Interaktionslogik für PpsNavigatorControl.xaml
	/// </summary>
	public partial class PpsNavigatorControl : UserControl
	{
		private PpsNavigatorModel navigatorModel;

		#region -- ctor / init --------------------------------------------------------

		public PpsNavigatorControl()
		{
			InitializeComponent();
		} // ctor

		public void Init(PpsMainWindow windowModel)
		{
			navigatorModel = new PpsNavigatorModel(windowModel);
			DataContext = navigatorModel;
		} // proc Init

		#endregion

		public void OnPreview_MouseDown(object originalSource)
		{
			if (!ElementIsChildOfSearchBox(originalSource))
				CollapseSearchBox();
		}

		public void OnPreview_TextInput(TextCompositionEventArgs e)
		{
			if (!object.Equals(e.OriginalSource, PART_SearchBox))
				e.Handled = ExpandSearchBox(e.Text);
		}

		/// <summary></summary>
		public bool ViewsShowDescriptions => navigatorModel.ViewsShowDescription;

		#region -- SearchBoxHandling --------------------------------------------------

		private void PART_SearchBox_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Enter)
			{
				var expr = BindingOperations.GetBindingExpression(PART_SearchBox, TextBox.TextProperty);
				if (expr != null)
					expr.UpdateSource();
				navigatorModel.ExecuteCurrentSearchText();
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

	#region -- class PpsNavigatorPriorityToDockPosition -------------------------------

	/// <summary>
	/// Converter zum Ermitteln der Dockimg Position aus der Priority Property eines ActionCommands
	/// </summary>
	internal class PpsNavigatorPriorityToDockPosition : IValueConverter
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
	} // class PpsNavigatorPriorityToDockPosition

	#endregion

	#region -- enum PpsNavigatorSearchBoxState ----------------------------------------

	internal enum PpsNavigatorSearchBoxState
	{
		Expanded,
		Collapsed
	} // enum PpsNavigatorSearchBoxState

	#endregion

}
