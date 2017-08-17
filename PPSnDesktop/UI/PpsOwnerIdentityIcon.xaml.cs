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
	/// Interaction logic for PpsOwnerIdentityIcon.xaml
	/// </summary>
	public partial class PpsOwnerIdentityIcon : UserControl
	{
		public PpsOwnerIdentityIcon()
		{
			InitializeComponent();
		}

		public string UserString
		{
			get
			{
				if (Owner == 0)
						return "🖳";
				if (Owner == PpsEnvironment.GetEnvironment().UserId)
						return "👨";
				return "👪";
			}
		}

		private bool IsLocalUser(long id)
			=> id == PpsEnvironment.GetEnvironment().UserId;

		public readonly static DependencyProperty OwnerProperty = DependencyProperty.Register(nameof(Owner), typeof(long), typeof(PpsOwnerIdentityIcon));
		public long Owner { get => (long)GetValue(OwnerProperty); set { SetValue(OwnerProperty, value); } }
	}
}
