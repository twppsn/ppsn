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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;
using TecWare.PPSn.Main;
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
				=> app.LogException(PpsLogType.Warning, message.Replace(";", ";\n"));
		} // class BindingErrorListener

		#endregion

		private IPpsShell shell = null;

		/// <summary>Start application</summary>
		public App()
		{
			DispatcherUnhandledException += App_DispatcherUnhandledException;

			PresentationTraceSources.DataBindingSource.Listeners.Add(new BindingErrorListener(this));
		} // ctor

		#region -- OnStartup, OnExit ------------------------------------------------------

		internal async Task<bool> StartApplicationAsync(IPpsShellInfo _shellInfo = null, ICredentials _userInfo = null)
		{
			var shellInfo = _shellInfo;
			var userInfo = _userInfo;

			object errorInfo = null;
			IPpsShell newShell = null; 
			IPpsShell errorShell = null;

			// we will have no windows
			ShutdownMode = ShutdownMode.OnExplicitShutdown;

			// show a login/splash
			var splashWindow =  new PpsSplashWindow()
			{
				Owner = Current.Windows.OfType<PpsMainWindow>().FirstOrDefault(),
				StatusText = PPSn.Properties.Resources.AppStartApplicationAsyncInitApp
			};
			splashWindow.Show();

			// todo: config
			PpsShell.Collect(Assembly.Load("PPSn.Pps2000.VO, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null"));

			try
			{
				while (true)
				{
					try
					{
						// no arguments are given, show user interface to select a shell
						if (newShell == null)
						{
							if (shellInfo == null || errorInfo != null)
							{
								if (errorInfo != null)
									await splashWindow.SetErrorAsync(errorInfo, errorShell);

								var newShellInfo = await splashWindow.ShowShellAsync(shellInfo);
								if (newShellInfo == null)
									return false;
								shellInfo = newShellInfo;
								errorInfo = null;
							}

							// close the current shell
							if (shell != null && !await CloseApplicationAsync())
								return false;

							// create the application shell
							splashWindow.SetProgressText("Starte Anwendung...");
							newShell = await PpsShell.StartAsync(shellInfo, true);
							errorShell = newShell;
						}

						// login user
						if (userInfo == null && errorInfo == null) // try find auto login
						{
							// check shell settings for user information
							// todo: get auto login
						}

						if (userInfo == null || errorInfo != null) // show login page
						{
							if (errorInfo != null)
								await splashWindow.SetErrorAsync(errorInfo, errorShell);

							try
							{
								var (newLoginShellInfo, newUserInfo) = await splashWindow.ShowLoginAsync(newShell, userInfo);
								if (newUserInfo == null)
									return false;
								else if (newLoginShellInfo != null && !newLoginShellInfo.Equals(newShell.Info))
								{
									await newShell.ShutdownAsync();

									errorShell = null;
									newShell = null;
								}

								userInfo = newUserInfo;
							}
							catch
							{
								newShell = null;
								throw;
							}
						}

						if (newShell != null)
						{
							// login user info
							splashWindow.SetProgressText("Anmelden des Nutzers...");
							await newShell.LoginAsync(userInfo);

							// start window
							var mws = newShell.GetService<IPpsMainWindowService>(true);
							await mws.OpenPaneAsync(Type.GetType("TecWare.PPSn.Pps2000.VO.VoWindowPane,PPSn.Pps2000.VO, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null"));
							//await mws.OpenPaneAsync(typeof(PpsTracePane));

							//var bde = new PpsBdeWindow(newShell);
							//bde.Show();
							//var paneType = Type.GetType("TecWare.PPSn.Pps2000.Bde.Maschine.MaPanel,PPSn.Pps2000.Bde, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null", true);
							//bde.OpenPaneAsync(paneType, arguments: new LuaTable()).Spawn(bde);

							// set active shell
							errorShell = null;
							shell = newShell;

							// now, we have windows
							ShutdownMode = ShutdownMode.OnLastWindowClose;

							return true;
						}
					}
					catch (Exception e)
					{
						newShell?.GetService<IPpsLogger>(false)?.Append(PpsLogType.Exception, e);
						errorInfo = e;
					}
				}
			}
			finally
			{
				if (errorShell != null)
					errorShell.ShutdownAsync().Await();

				// close dialog
				splashWindow.ForceClose();
			}
		} // proc StartApplicationAsync

		private async Task<bool> CloseApplicationAsync()
		{
			if (shell == null)
				return true;

			if (await shell.ShutdownAsync())
			{
				shell = null;
				return true;
			}
			else
				return false;
			
		} // func CloseApplicationAsync

		private static string GetNativeLibrariesPath()
			=> Path.Combine(Path.GetDirectoryName(typeof(App).Assembly.Location), IntPtr.Size == 8 ? "x64" : "x86");

		/// <summary>Start application</summary>
		/// <param name="e"></param>
		protected override void OnStartup(StartupEventArgs e)
		{
			// upgrade settings
			if (Settings.Default.UpgradeSettings)
			{
				Settings.Default.Upgrade();
				Settings.Default.UpgradeSettings = false;
			}

			// change environment for specific dll's
			Environment.SetEnvironmentVariable("PATH",
				Environment.GetEnvironmentVariable("PATH") + ";" + GetNativeLibrariesPath(),
				EnvironmentVariableTarget.Process
			);

			// Init services
			PpsShell.Collect(typeof(StuffUI).Assembly);
			PpsShell.Collect(typeof(App).Assembly);

			ParseArguments(e, out var shellInfo, out var userCred);

			FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement), new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag)));

			// init keyboard scanner
			KeyboardScanner.Init();

			StartApplicationAsync(shellInfo, userCred)
				.ContinueWith(t =>
				{
					if (!t.Result)
						Dispatcher.Invoke(Shutdown);
				}
			);

			base.OnStartup(e);
		} // proc OnStartup

		/// <summary>Close application tasks.</summary>
		/// <param name="e"></param>
		protected override void OnExit(ExitEventArgs e)
		{
			base.OnExit(e);
			CloseApplicationAsync().Await();
		} // proc OnExit

		private static void ParseArguments(StartupEventArgs e, out IPpsShellInfo shellInfo, out ICredentials userCred)
		{
			var userName = (string)null;
			var userPass = (string)null;

			shellInfo = null;
			userCred = null;

			if (e.Args.Length == 0)
				return;

			var localShells = new Lazy<IPpsShellInfo[]>(PpsShell.GetShellInfo().ToArray);

			// first parse arguments for environment or user information
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
							var shellName = arg.Substring(2);
							shellInfo = localShells.Value.FirstOrDefault(c => String.Compare(c.Name, shellName, StringComparison.OrdinalIgnoreCase) == 0);
							break;
						case 'l': // enforce load of an remote assembly
							var path = arg.Substring(2);
							if (path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
								|| path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
								Task.Run(() => PpsEnvironment.LoadAssemblyFromLocalAsync(arg.Substring(2))).Wait();
							else
							{ }
							break;
					}
				}
			}

			// load user name from environment
			if (shellInfo != null)
			{
				using (var pcl = new PpsClientLogin("ppsn_env:" + shellInfo.Uri.ToString(), shellInfo.Name, false))
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

		private void LogException(PpsLogType type, string message)
			=> (shell ?? PpsShell.Global).GetService<IPpsLogger>(false)?.Append(type, message);

		private void LogException(Exception e)
			=> (shell ?? PpsShell.Global).GetService<IPpsLogger>(false)?.Append(PpsLogType.Exception, e);

		private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
		{
			LogException(e.Exception);
			CoreExceptionHandler(e.Exception);
			e.Handled = true;
		} // event App_DispatcherUnhandledException

		#endregion

		/// <summary>Return the current environemnt</summary>
		public IPpsShell Shell => shell;
	} // class App
}
