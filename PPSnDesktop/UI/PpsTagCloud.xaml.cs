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
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace TecWare.PPSn.UI
{
	/// <summary>
	/// Interaction logic for PpsTagCloud.xaml
	/// </summary>
	public partial class PpsTagCloud : UserControl
	{
		public PpsTagCloud()
		{
			InitializeComponent();

			CommandBindings.Add(
						new CommandBinding(RemoveTagCommand,
							(isender, ie) =>
							{
								((IPpsTagItem)ie.Parameter).Remove();
								ie.Handled = true;
							},
							(isender, ie) => ie.CanExecute = true
						)
					);
			CommandBindings.Add(
						new CommandBinding(AppendTagCommand,
							(isender, ie) =>
							{
								((IPpsTagItem)ie.Parameter).Append();
								ie.Handled = true;
							},
							(isender, ie) => ie.CanExecute = !String.IsNullOrEmpty(((IPpsTagItem)ie.Parameter).Name)
						)
					);
		}

		public readonly static RoutedUICommand AppendTagCommand = new RoutedUICommand("AppendTag", "AppendTag", typeof(PpsTagCloud));
		public readonly static RoutedUICommand RemoveTagCommand = new RoutedUICommand("RemoveTag", "RemoveTag", typeof(PpsTagCloud));

		public readonly static DependencyProperty TagsSourceProperty = DependencyProperty.Register(nameof(PTCTagsSource), typeof(PpsObject), typeof(PpsTagCloud));
		public PpsObject PTCTagsSource { get => (PpsObject)GetValue(TagsSourceProperty); set { SetValue(TagsSourceProperty, value);  } }
	}
}
