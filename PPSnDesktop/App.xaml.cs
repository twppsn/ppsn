using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using TecWare.DE.Stuff;
using TecWare.PPSn.Properties;

namespace TecWare.PPSn
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public partial class App : Application
	{
		private PpsMainEnvironment currentEnvironment;

		public App()
		{
			this.DispatcherUnhandledException += App_DispatcherUnhandledException;
		} // ctor

		#region -- OnStartup, OnExit ------------------------------------------------------

		protected override void OnStartup(StartupEventArgs e)
		{
			ShutdownMode = ShutdownMode.OnExplicitShutdown; // set shutdown to explicit

			// Load the environment
			ReloadApplicationAsync()
				.ContinueWith(async t =>
					{
						try
						{
							SetEnvironment(t.Result);

							if (currentEnvironment != null)
							{
								await currentEnvironment.CreateMainWindowAsync();
								// first window is created -> change shutdown mode
								await Dispatcher.InvokeAsync(() => { ShutdownMode = ShutdownMode.OnLastWindowClose; });

								foreach (var k in this.Resources.Keys)
								{
									Debug.Print("({0}) {1}", k.GetType().Name, k.ToString());
									var v = this.Resources[k];
									if (v is Style)
										Debug.Print("  ==> Style: {0}", ((Style)v).TargetType.Name);
									else
										Debug.Print("  ==> {0}", v.GetType().Name);
								}
							}
							else
							{
								Dispatcher.Invoke(() => Shutdown(1));// cancel
							}
						}
						catch (Exception ex)
						{
							Dispatcher.Invoke(() =>
								{
									if (PpsEnvironment.ShowExceptionDialog(null, ExceptionShowFlags.Shutown, ex, "Anwendung konnte nicht geladen werden."))
										CoreExceptionHandler(ex);
									Shutdown(1);
								}
							);
						}
					});

			base.OnStartup(e);
		} // proc OnStartup

		private static void CoreExceptionHandler(Exception ex)
		{
			if (MessageBox.Show(String.Format("Unbehandelte Ausnahme: {0}.\n\nDetails in der Zwischenablage abgelegen?", ex.Message), "Fehler", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
				Clipboard.SetText(ex.GetMessageString());
		} // proc CoreExceptionHandler

		private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
		{
			CoreExceptionHandler(e.Exception);
			e.Handled = true;
		} // event App_DispatcherUnhandledException
		
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
			var relyOnServer = false;
			try
			{
				await env.RefreshAsync();
			}
			catch (Exception e) // offline part failed
			{
				relyOnServer = true;
				await env.ShowExceptionAsync(ExceptionShowFlags.None, e, "Start der Anwendung fehlgeschlagen. Es wird versucht eine gültige Version vom Server zu laden.");
			}

			// go online and sync data from the server to client
			if (Settings.Default.AutoOnlineMode || relyOnServer)
			{
				var cancellationSource = new CancellationTokenSource(30000);
				bool onlineSuccess;
				try
				{
					onlineSuccess = await env.StartOnlineMode(cancellationSource.Token); // does the online refresh again
				}
				catch (Exception) 
				{
					if (!env.IsOnline) // failed to go online -> shutdown
						throw;
					else
						onlineSuccess = true;
				}

				if (onlineSuccess)
					await env.LoginUserAsync(); // does the online refresh (last step)
				else if (relyOnServer)
					throw new Exception("Invalid client data. Server connection failed.");
			}

			return env;
		} // proc ReloadApplicationAsync

		public PpsMainEnvironment Environment => currentEnvironment;
	} // class App
}
