using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;
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
			BindingErrorListener.Listen(m => currentEnvironment?.Traces.AppendText(PpsTraceItemType.Fail,m.Replace("; ",";\n")));
		} // ctor

		#region -- OnStartup, OnExit ------------------------------------------------------

		public async Task<bool> StartApplicationAsync(PpsEnvironmentInfo _environment = null, NetworkCredential _userInfo = null)
		{
			var environment = _environment;
			var userInfo = _userInfo;
			var errorInfo = (object)null;
			PpsEnvironment errorEnvironment = null;

			// we will have no windows
			await Dispatcher.InvokeAsync(() => ShutdownMode = ShutdownMode.OnExplicitShutdown);

			// show a login/splash
			var splashWindow = await Dispatcher.InvokeAsync(() =>
				{
					var w = new PpsSplashWindow()
					{
						Owner = currentEnvironment?.GetWindows().FirstOrDefault(),
						StatusText = PPSn.Properties.Resources.AppStartApplicationAsyncInitApp
					};
					w.Show();
					return w;
				}
			);
			try
			{
				while (true)
				{
					try
					{
						// no arguments are given, show user interface
						if (environment == null || userInfo == null || errorInfo != null)
						{
							if (errorInfo != null)
								await splashWindow.SetErrorAsync(errorInfo, errorEnvironment);
							var t = await splashWindow.ShowLoginAsync(environment, userInfo);
							if (t == null)
								return false;

							environment = t.Item1;
							userInfo = t.Item2;
							errorInfo = null;
						}

						// close the current environment
						if (currentEnvironment != null && !await CloseApplicationAsync())
							return false;

						// create the application environment
						splashWindow.SetProgressTextAsync("Starte Anwendung...");
						var env = await Dispatcher.InvokeAsync(() => new PpsMainEnvironment(environment, userInfo, this));
						errorEnvironment = env;

						// create environment
						switch (await env.InitAsync(splashWindow))
						{
							case PpsEnvironmentModeResult.LoginFailed:
								errorInfo = "Anmeldung fehlgeschlagen.";
								errorEnvironment.Traces.AppendText(PpsTraceItemType.Fail, (string)errorInfo);
								break;
							case PpsEnvironmentModeResult.Shutdown:
								return false;

							case PpsEnvironmentModeResult.ServerConnectFailure:
								errorInfo = "Verbindung zum Server fehlgeschlagen.";
								errorEnvironment.Traces.AppendText(PpsTraceItemType.Fail, (string)errorInfo);
								break;

							case PpsEnvironmentModeResult.NeedsUpdate:
								errorInfo = "Update ist erforderlich.";
								errorEnvironment.Traces.AppendText(PpsTraceItemType.Fail, (string)errorInfo);
								break;

							case PpsEnvironmentModeResult.NeedsSynchronization:
								errorInfo = "Synchronization ist erforderlich.";
								errorEnvironment.Traces.AppendText(PpsTraceItemType.Fail, (string)errorInfo);
								break;

							case PpsEnvironmentModeResult.Online:
							case PpsEnvironmentModeResult.Offline:
								// set new environment
								currentEnvironment = env;

								// create first window
								await currentEnvironment.CreateMainWindowAsync();

								// now, we have windows
								await Dispatcher.InvokeAsync(() => ShutdownMode = ShutdownMode.OnLastWindowClose);

								return true;
							default:
								throw new InvalidOperationException();
						}
					}
					catch (Exception e)
					{
						errorEnvironment.Traces.AppendException(e);
						errorInfo = e;
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
			if (!e.Args.Any())
				Task.Run(() =>
					{
						StartApplicationAsync()
						.ContinueWith(t =>
						{
							if (!t.Result)
								Dispatcher.Invoke(Shutdown);
						}
					);
					}
				);
			else
			{
				ParseArguments(e, out var environment, out var userCred);
				Task.Run(() =>
				{
					StartApplicationAsync(environment, userCred)
						.ContinueWith(t =>
						{
							if (!t.Result)
								Dispatcher.Invoke(Shutdown);
						}
					);
				}
				);
			}
			base.OnStartup(e);
		} // proc OnStartup

		private static void ParseArguments(StartupEventArgs e, out PpsEnvironmentInfo environment, out NetworkCredential userCred)
		{
			var userName = (string)null;
			var userPass = (string)null;

			environment = null;
			userCred = (NetworkCredential)null;
			
			var environmentsInfos = PpsEnvironmentInfo.GetLocalEnvironments().ToArray();
			foreach (var arg in e.Args)
			{
				if (arg.Length > 1 && arg[0] == '-')
				{
					var cmd = arg[1];
					switch (cmd)
					{
						case 'u':
							userName = arg.Substring(2);
							break;
						case 'p':
							userPass = arg.Substring(2);
							break;
						case 'a':
							environment = environmentsInfos.FirstOrDefault(c => String.Compare(c.Name, 0, arg, 2, Math.Max(c.Name.Length, arg.Length), StringComparison.OrdinalIgnoreCase) == 0);

							// load user name
							using (var pcl = new PpsClientLogin("ppsn_env:" + environment.Uri.ToString(), "", false))
								userCred = (NetworkCredential)pcl.GetCredentials();
							break;
					}
				}
			}

			if (userName != null)
				userCred = new NetworkCredential(userName, userPass);
		} // func ParseArguments

		private static void CoreExceptionHandler(Exception ex)
		{
			if (MessageBox.Show(String.Format("Unbehandelte Ausnahme: {0}.\n\nDetails in der Zwischenablage abgelegen?", ex.Message), "Fehler", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
				Clipboard.SetText(ex.GetMessageString());
		} // proc CoreExceptionHandler

		private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
		{
			currentEnvironment.Traces.AppendException(e.Exception);
			CoreExceptionHandler(e.Exception);
			e.Handled = true;
		} // event App_DispatcherUnhandledException

		public class BindingErrorListener : TraceListener
		{
			private Action<string> logAction;
			public static void Listen(Action<string> logAction)
			{
				PresentationTraceSources.DataBindingSource.Listeners
					.Add(new BindingErrorListener() { logAction = logAction });
			}
			public override void Write(string message) { }
			public override void WriteLine(string message)
			{
				logAction(message);
			}
		}

		#endregion

		private void SetEnvironment(PpsMainEnvironment env)
		{
			currentEnvironment = env;
		} // proc SetEnvironment

		public PpsMainEnvironment Environment => currentEnvironment;
	} // class App
}
