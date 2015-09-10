using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace TecWare.PPSn.UI
{
	/// <summary>
	/// Interaktionslogik für PpsCharmbarControl.xaml
	/// </summary>
	public partial class PpsCharmbarControl : UserControl
	{
		private static readonly DependencyProperty BoardVisibilityProperty = DependencyProperty.Register("BoardVisibility", typeof(Visibility), typeof(PpsCharmbarControl), new UIPropertyMetadata(Visibility.Collapsed));
		private ToggleButton curCheckedButton = null;

		public PpsCharmbarControl()
		{
			InitializeComponent();
		} // ctor

		private void ToggleButton_Click(object sender, RoutedEventArgs e)
		{
			var button = sender as ToggleButton;
            if (button == null)
				return;
			else if (button.IsChecked == true)
				ShowBoard(button);
			else
			{
				HideBoard();
			}
		}
		private void ShowBoard(ToggleButton button)
		{
			if (curCheckedButton != null)
				curCheckedButton.IsChecked = false;
			curCheckedButton = button;
			PART_ContentInfo.Text = String.Format("{0} ...", button.Content);
			IsBoardVisible = true;
		}
		private void HideBoard()
		{
			curCheckedButton = null;
			IsBoardVisible = false;
		}

		private bool IsBoardVisible
		{
			get
			{
				return (Visibility)GetValue(BoardVisibilityProperty) == Visibility.Visible;
			}
			set
			{
				if (IsBoardVisible != value)
				{
					if (value)
					{
						SetValue(BoardVisibilityProperty, Visibility.Visible);
					}
					else
					{
						SetValue(BoardVisibilityProperty, Visibility.Collapsed);
					}
				}
			}
		}

	} // class PpsCharmbarControl

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PpsCharmbarWidthToPaddingConverter : IValueConverter
	{
		private const double indentSize = 19.0;

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return new Thickness(0, 0, System.Convert.ToDouble(value) + 8, 0);  // 8pxl margin to pane
		}
		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new System.NotImplementedException();
		}
	} // converter PpsCharmbarWidthToPaddingConverter

}
