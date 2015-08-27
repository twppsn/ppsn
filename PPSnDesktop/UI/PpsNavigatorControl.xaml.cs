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
		public PpsNavigatorControl()
		{
			InitializeComponent();
		} // ctor
	} // class PpsNavigatorControl


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
}
