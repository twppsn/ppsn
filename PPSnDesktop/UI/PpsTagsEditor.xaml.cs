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
    /// Interaction logic for PpsTagsEditor.xaml
    /// </summary>
    public partial class PpsTagsEditor : UserControl
	{
		public PpsObjectTags Tags { get { return ((PpsObject)DataContext).Tags; } }	//ToDo: Converter

		public PpsTagsEditor()
        {
            InitializeComponent();
        }
    }
}
