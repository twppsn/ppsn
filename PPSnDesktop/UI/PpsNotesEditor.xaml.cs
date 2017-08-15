using System;
using System.Collections.Generic;
using System.Globalization;
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
    /// Interaction logic for PpsNotesEditor.xaml
    /// </summary>
    public partial class PpsNotesEditor : UserControl
    {
		public PpsNotesEditor()
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
							(isender, ie) => ie.CanExecute = !String.IsNullOrEmpty(((IPpsTagItem)ie.Parameter).Name) && !String.IsNullOrEmpty(((IPpsTagItem)ie.Parameter).Value)
						)
					);
			CommandBindings.Add(
						new CommandBinding(SaveTagCommand,
							(isender, ie) =>
							{
								((IPpsTagItem)ie.Parameter).Save();
								ie.Handled = true;
							},
							(isender, ie) => ie.CanExecute = ((IPpsTagItem)ie.Parameter).CanSave
						)
					);
		}

		public readonly static DependencyProperty TagsSourceProperty = DependencyProperty.Register(nameof(PNETagsSource), typeof(PpsObject), typeof(PpsNotesEditor));
		public PpsObject PNETagsSource { get => (PpsObject)GetValue(TagsSourceProperty); set { SetValue(TagsSourceProperty, value); } }

		public readonly static RoutedUICommand AppendTagCommand = new RoutedUICommand("AppendTag", "AppendTag", typeof(PpsNotesEditor));
		public readonly static RoutedUICommand SaveTagCommand = new RoutedUICommand("SaveTag", "SaveTag", typeof(PpsNotesEditor));
		public readonly static RoutedUICommand RemoveTagCommand = new RoutedUICommand("RemoveTag", "RemoveTag", typeof(PpsNotesEditor));
	}
}
