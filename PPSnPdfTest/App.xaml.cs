using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace PPSnPdfTest
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		protected override void OnStartup(StartupEventArgs e)
		{
			Environment.SetEnvironmentVariable("Path", Environment.GetEnvironmentVariable("Path") + ";" + Path.Combine(Path.GetDirectoryName(typeof(App).Assembly.Location), "x86"), EnvironmentVariableTarget.Process);
			base.OnStartup(e);
		}

	}
}
