using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using TecWare.DE.Stuff;
using TecWare.PPSn.Properties;
using TecWare.PPSn.UI;

namespace TecWare.PPSn
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public partial class App : Application
	{
		private PpsMainEnvironment currentEnvironment = null;

		public App()
		{
			this.DispatcherUnhandledException += App_DispatcherUnhandledException;
		} // ctor

		#region -- OnStartup, OnExit ------------------------------------------------------

		public async Task<bool> StartApplicationAsync(PpsEnvironmentInfo _environment = null, ICredentials _userInfo = null, bool parseArguments = false)
		{
         var environment = _environment;
         var userInfo = _userInfo;
			var errorInfo = true;
         // we will have no windows
         await Dispatcher.InvokeAsync(() => ShutdownMode = ShutdownMode.OnExplicitShutdown);

			// show a login/splash
			var splashWindow = await Dispatcher.InvokeAsync(() =>
				{
					var w = new PpsSplashWindow();
					w.Owner = currentEnvironment?.GetWindows().FirstOrDefault();
					w.StatusText = "Initialisiere die Anwendung...";
					w.Show();
					return w;
				}
			);
			try
			{
				if (parseArguments)
				{
					// todo: Parse env, user
					environment = PpsEnvironmentInfo.GetLocalEnvironments().FirstOrDefault(c => c.Name.ToLower() == "test");
				}

				while (true)
				{
					try
					{
						// no arguments are given, show user interface
						if (environment == null || userInfo == null || errorInfo)
						{
							var t = await splashWindow.ShowLoginAsync(environment);
							if (t == null)
								return false;
							environment = t.Item1;
							userInfo = t.Item2;
						}

						// close the current environment
						if (currentEnvironment != null && !await CloseApplicationAsync())
							return false;

						// create the application environment
						splashWindow.SetProgressTextAsync("Starte Anwendung...");
						var env = await Dispatcher.InvokeAsync(() => new PpsMainEnvironment(environment, userInfo, this));

						// create environment
						switch (await env.InitAsync(splashWindow))
						{
							case PpsEnvironmentModeResult.LoginFailed:
								errorInfo = true;
								// todo: show error as simple text
								break;
							case PpsEnvironmentModeResult.Shutdown:
								errorInfo = true;
								// todo: application restart
								return false;

							case PpsEnvironmentModeResult.ServerConnectFailure:
								errorInfo = true;
								// todo: show error as simple text
								break;

							case PpsEnvironmentModeResult.NeedsUpdate:
								errorInfo = true;
								// todo: show error
								break;

							case PpsEnvironmentModeResult.NeedsSynchronization:
								errorInfo = true;
								// todo: show sync failed error
								break;

							case PpsEnvironmentModeResult.Online:
							case PpsEnvironmentModeResult.Offline:
								// set new environment
								currentEnvironment = env;
								environment.WriteLastUser(((dynamic)userInfo).UserName);
								// create first window

								return false; // todo: true
							default:
								throw new InvalidOperationException();
						}
					}
					catch (Exception e)
					{
						errorInfo = true;
						MessageBox.Show(e.ToString());
						// todo: show failed? or Shutdown?
					}
				}
			}
			finally
			{
				// close dialog
				await Dispatcher.InvokeAsync(splashWindow.ForceClose);
			}
		} // proc StartApplicationAsync

		private async Task<bool> CloseApplicationAsync()
		{
			if (!await Dispatcher.Invoke(currentEnvironment.ShutdownAsync))
			{
				currentEnvironment = null;
				return true;
			}
			else
				return false;
		} // func CloseApplicationAsync

		protected override void OnStartup(StartupEventArgs e)
		{
			Task.Run(() =>
				{
					StartApplicationAsync(parseArguments: true)
					.ContinueWith(t =>
					{
						if (!t.Result)
							Dispatcher.Invoke(Shutdown);
					}
				);
				}
			);
			//ShutdownMode = ShutdownMode.OnExplicitShutdown; // set shutdown to explicit



			//private async Task<PpsMainEnvironment> ReloadApplicationAsync()
			//{
			//	// create an new environment in the ui-thread
			//	var env = Dispatcher.Invoke(new Func<PpsMainEnvironment>(() => new PpsMainEnvironment(
			//		PpsEnvironmentInfo.CreateEnvironment(Settings.Default.ServerName, new Uri(Settings.Default.ServerUri, UriKind.Absolute)),
			//		this
			//	)));

			//	// load the offline part from the cache, if it is possible
			//	var relyOnServer = false;
			//	try
			//	{
			//		await env.RefreshAsync();
			//	}
			//	catch (Exception e) // offline part failed
			//	{
			//		relyOnServer = true;
			//		await env.ShowExceptionAsync(ExceptionShowFlags.None, e, "Start der Anwendung fehlgeschlagen. Es wird versucht eine gültige Version vom Server zu laden.");
			//	}

			//	// go online and sync data from the server to client
			//	if (Settings.Default.AutoOnlineMode || relyOnServer)
			//	{
			//		var cancellationSource = new CancellationTokenSource(30000);
			//		bool onlineSuccess;
			//		try
			//		{
			//			onlineSuccess = await env.StartOnlineMode(cancellationSource.Token); // does the online refresh again
			//		}
			//		catch (Exception) 
			//		{
			//			if (!env.IsOnline) // failed to go online -> shutdown
			//				throw;
			//			else
			//				onlineSuccess = true;
			//		}

			//		if (onlineSuccess)
			//			await env.LoginUserAsync(); // does the online refresh (last step)
			//		else if (relyOnServer)
			//			throw new Exception("Invalid client data. Server connection failed.");
			//	}

			//	return env;
			//} // proc ReloadApplicationAsync

			//// Load the environment
			//ReloadApplicationAsync()
			//	.ContinueWith(async t =>
			//		{
			//			try
			//			{
			//				SetEnvironment(t.Result);

			//				if (currentEnvironment != null)
			//				{
			//					await currentEnvironment.CreateMainWindowAsync();
			//					// first window is created -> change shutdown mode
			//					await Dispatcher.InvokeAsync(() => { ShutdownMode = ShutdownMode.OnLastWindowClose; });

			//					foreach (var k in this.Resources.Keys)
			//					{
			//						Debug.Print("({0}) {1}", k.GetType().Name, k.ToString());
			//						var v = this.Resources[k];
			//						if (v is Style)
			//							Debug.Print("  ==> Style: {0}", ((Style)v).TargetType.Name);
			//						else
			//							Debug.Print("  ==> {0}", v.GetType().Name);
			//					}
			//				}
			//				else
			//				{
			//					Dispatcher.Invoke(() => Shutdown(1));// cancel
			//				}
			//			}
			//			catch (Exception ex)
			//			{
			//				Dispatcher.Invoke(() =>
			//					{
			//						if (PpsEnvironment.ShowExceptionDialog(null, ExceptionShowFlags.Shutown, ex, "Anwendung konnte nicht geladen werden."))
			//							CoreExceptionHandler(ex);
			//						Shutdown(1);
			//					}
			//				);
			//			}
			//		});

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

		public PpsMainEnvironment Environment => currentEnvironment;
	} // class App
}
