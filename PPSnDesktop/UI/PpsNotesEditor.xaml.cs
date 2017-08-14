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
		public string Notice
		{
			get
			{
				var tag = (from ttag in ((PpsObject)DataContext).Tags where ttag.Name == "Notiz" select ttag).FirstOrDefault();
				return tag != null ? (string)tag.Value : String.Empty;
			}
			set
			{
				((PpsObject)DataContext).Tags.UpdateTag("Notiz", Data.PpsObjectTagClass.Text, value);
				((PpsObject)DataContext).UpdateLocalAsync().AwaitTask();
			}
		}

		public PpsNotesEditor()
        {
            InitializeComponent();
        }
    }
}
