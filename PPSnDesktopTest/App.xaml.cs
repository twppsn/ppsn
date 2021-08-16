using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using TecWare.PPSn.Themes;
using TecWare.PPSn.UI;

namespace TecWare.PPSn
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		protected override void OnStartup(StartupEventArgs e)
		{
			PpsTheme.UpdateThemedDictionary(Resources.MergedDictionaries, PpsColorTheme.Default);

			base.OnStartup(e);
		}
	}
}
