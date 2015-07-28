using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using TecWare.PPSn.Properties;

namespace TecWare.PPSn
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public partial class App : Application
	{
		private PpsMainEnvironment currentEnvironment;

		#region -- OnStartup, OnExit ------------------------------------------------------

		protected override void OnStartup(StartupEventArgs e)
		{
			// Load the environment
			ReloadApplicationAsync()
				.ContinueWith(t =>
					{
						SetEnvironment(t.Result);

						if (currentEnvironment != null)
							currentEnvironment.CreateMainWindow();
					});

			base.OnStartup(e);
		} // proc OnStartup

		protected override void OnExit(ExitEventArgs e)
		{
			base.OnExit(e);
		} // proc OnExit

		#endregion

		private void SetEnvironment(PpsMainEnvironment env)
		{
			currentEnvironment = env;
		} // proc SetEnvironment

		private async Task<PpsMainEnvironment> ReloadApplicationAsync()
		{
			// create an new environment in the ui-thread
			var env = Dispatcher.Invoke(new Func<PpsMainEnvironment>(() => new PpsMainEnvironment(new Uri(Settings.Default.ServerUri, UriKind.Absolute), Resources)));

			// go online and sync data from the server to client
			if (Settings.Default.AutoOnlineMode)
				await env.StartOnlineMode(30000);

			// load the offline part
			await env.RefreshAsync();

			return env;
		} // proc ReloadApplicationAsync

		public PpsMainEnvironment Environment { get { return currentEnvironment; } }
	} // class App
}
