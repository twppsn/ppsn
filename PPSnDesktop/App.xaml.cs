#region -- copyright --
//
// Licensed under the EUPL, Version 1.1 or - as soon they will be approved by the
// European Commission - subsequent versions of the EUPL(the "Licence"); You may
// not use this work except in compliance with the Licence.
//
// You may obtain a copy of the Licence at:
// http://ec.europa.eu/idabc/eupl
//
// Unless required by applicable law or agreed to in writing, software distributed
// under the Licence is distributed on an "AS IS" basis, WITHOUT WARRANTIES OR
// CONDITIONS OF ANY KIND, either express or implied. See the Licence for the
// specific language governing permissions and limitations under the Licence.
//
#endregion
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;
using TecWare.PPSn.Properties;
using TecWare.PPSn.UI;

namespace TecWare.PPSn
{
	/// <summary></summary>
	public partial class App : Application
	{
		#region -- class BindingErrorListener -----------------------------------------

		private sealed class BindingErrorListener : TraceListener
		{
			private readonly App app;

			public BindingErrorListener(App app)
				=> this.app = app ?? throw new ArgumentNullException(nameof(app));

			public override void Write(string message) { }

			public override void WriteLine(string message)
				=> app.Environment?.Log.Append(PpsLogType.Warning, message.Replace(";", ";\n"));
		} // class BindingErrorListener

		#endregion

		private PpsEnvironment currentEnvironment = null;

		public App()
		{
			DispatcherUnhandledException += App_DispatcherUnhandledException;

			PresentationTraceSources.DataBindingSource.Listeners.Add(new BindingErrorListener(this));
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

							try
							{
								var t = await splashWindow.ShowLoginAsync(environment, userInfo);
								if (t == null)
									return false;

								environment = t.Item1;
								userInfo = t.Item2;
								errorInfo = null;
							}
							finally
							{
								errorEnvironment?.Dispose();
							}
						}

						// close the current environment
						if (currentEnvironment != null && !await CloseApplicationAsync())
							return false;

						// create the application environment
						splashWindow.SetProgressTextAsync("Starte Anwendung...");
						var env = await Dispatcher.InvokeAsync(() => new PpsEnvironment(environment, userInfo, this));
						errorEnvironment = env;

						// create environment
						switch (await env.InitAsync(splashWindow))
						{
							case PpsEnvironmentModeResult.LoginFailed:
								errorInfo = "Anmeldung fehlgeschlagen.";
								errorEnvironment.Log.Append(PpsLogType.Fail, (string)errorInfo);
								break;
							case PpsEnvironmentModeResult.Shutdown:
								return false;

							case PpsEnvironmentModeResult.ServerConnectFailure:
								errorInfo = "Verbindung zum Server fehlgeschlagen.";
								errorEnvironment.Log.Append(PpsLogType.Fail, (string)errorInfo);
								break;

							case PpsEnvironmentModeResult.NeedsUpdate:
								errorInfo = "Update ist erforderlich.";
								errorEnvironment.Log.Append(PpsLogType.Fail, (string)errorInfo);
								break;

							case PpsEnvironmentModeResult.NeedsSynchronization:
								errorInfo = "Synchronization ist erforderlich.";
								errorEnvironment.Log.Append(PpsLogType.Fail, (string)errorInfo);
								break;

							case PpsEnvironmentModeResult.Online:
							case PpsEnvironmentModeResult.Offline:
								// set new environment
								SetEnvironment(env);

								// create first window
								await currentEnvironment.CreateMainWindowAsync();

								// now, we have windows
								ShutdownMode = ShutdownMode.OnLastWindowClose;

								return true;
							default:
								throw new InvalidOperationException();
						}
					}
					catch (Exception e)
					{
						errorEnvironment.Log.Append(PpsLogType.Exception, e);
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
			if (currentEnvironment == null)
				return true;

			if (!await Dispatcher.Invoke(currentEnvironment.ShutdownAsync))
			{
				SetEnvironment(null);
				return true;
			}
			else
				return false;
		} // func CloseApplicationAsync

		private static string GetNativeLibrariesPath()
			=> Path.Combine(Path.GetDirectoryName(typeof(App).Assembly.Location), IntPtr.Size == 8 ? "x64" : "x86");

		protected override void OnStartup(StartupEventArgs e)
		{
			// upgrade settings
			if (Settings.Default.UpgradeSettings)
			{
				Settings.Default.Upgrade();
				Settings.Default.UpgradeSettings = false;
			}

			// change environment for specific dll's
			System.Environment.SetEnvironmentVariable("PATH",
				System.Environment.GetEnvironmentVariable("PATH") + ";" + GetNativeLibrariesPath(),
				EnvironmentVariableTarget.Process
			);

			ParseArguments(e, out var environment, out var userCred);

			StartApplicationAsync(environment, userCred)
				.ContinueWith(t =>
				{
					if (!t.Result)
						Dispatcher.Invoke(Shutdown);
				}
			);

			base.OnStartup(e);
		} // proc OnStartup

		protected override void OnExit(ExitEventArgs e)
		{
			base.OnExit(e);
			CloseApplicationAsync().AwaitTask();
		} // proc OnExit

		private static void ParseArguments(StartupEventArgs e, out PpsEnvironmentInfo environment, out NetworkCredential userCred)
		{
			var userName = (string)null;
			var userPass = (string)null;

			environment = null;
			userCred = null;

			if (e.Args.Length == 0)
				return;

			var environmentsInfos = new Lazy<PpsEnvironmentInfo[]>(PpsEnvironmentInfo.GetLocalEnvironments().ToArray);

			// first parse arguments for environment or user information
			foreach (var arg in e.Args)
			{
				if (arg.Length > 1 && arg[0] == '-')
				{
					var cmd = arg[1];
					switch (cmd)
					{
						case 'u':
							userName = arg.Substring(2).Trim('\"', '\'');
							break;
						case 'p':
							userPass = arg.Substring(2).Trim('\"', '\'');
							break;
						case 'a':
							var environmentname = arg.Substring(2).Trim('\"', '\'');
							environment = environmentsInfos.Value.FirstOrDefault(c => String.Compare(c.Name, environmentname, StringComparison.OrdinalIgnoreCase) == 0);
							break;
					}
				}
			}

			// load user name from environment
			if (environment != null)
			{
				using (var pcl = new PpsClientLogin("ppsn_env:" + environment.Uri.ToString(), environment.Name, false))
					userCred = pcl.GetCredentials();
			}

			// set user credentials
			if (userName != null && userCred == null)
				userCred = new NetworkCredential(userName, userPass);
		} // func ParseArguments

		private static void CoreExceptionHandler(Exception ex)
		{
			if (MessageBox.Show(String.Format("Unbehandelte Ausnahme: {0}.\n\nDetails in der Zwischenablage ablegen?", ex.Message), "Fehler", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
				Clipboard.SetText(ex.GetMessageString());
		} // proc CoreExceptionHandler

		private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
		{
			currentEnvironment?.Log.Append(PpsLogType.Exception, e.Exception);
			CoreExceptionHandler(e.Exception);
			e.Handled = true;
		} // event App_DispatcherUnhandledException

		#endregion

		private void SetEnvironment(PpsEnvironment env)
		{
			currentEnvironment = env;
			PpsShell.SetShell(env);
		} // proc SetEnvironment

		public PpsEnvironment Environment => currentEnvironment;
	} // class App
}
