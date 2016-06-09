using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using TecWare.DE.Stuff;
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
				.ContinueWith(async t =>
					{
						try
						{
							SetEnvironment(t.Result);

							if (currentEnvironment != null)
								await currentEnvironment.CreateMainWindowAsync();
							else
							{
								Dispatcher.Invoke(() =>
									{
										MessageBox.Show("Environment konnte nicht initialisiert werden.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
										Shutdown(1);
									}
								);
							}
						}
						catch (Exception ex)
						{
							Dispatcher.Invoke(() =>
								{
									if (PpsEnvironment.ShowExceptionDialog(null, ExceptionShowFlags.Shutown, ex, "Environment wurde nicht initializiert."))
										MessageBox.Show(ex.GetMessageString(), "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
									Shutdown(1);
								}
							);
						}
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
			var env = Dispatcher.Invoke(new Func<PpsMainEnvironment>(() => new PpsMainEnvironment(
				PpsEnvironmentInfo.CreateEnvironment(Settings.Default.ServerName, new Uri(Settings.Default.ServerUri, UriKind.Absolute)),
				this
			)));

			// load the offline part from the cache, if it is possible
			await env.RefreshAsync();

			// go online and sync data from the server to client
			if (Settings.Default.AutoOnlineMode)
			{
				var cancellationSource = new CancellationTokenSource(30000);
				if (await env.StartOnlineMode(cancellationSource.Token)) // does the online refresh
					await env.LoginUserAsync();
			}

			return env;
		} // proc ReloadApplicationAsync

		public PpsMainEnvironment Environment => currentEnvironment;
	} // class App
}
