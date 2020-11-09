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
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Threading;
using Microsoft.Win32;
using TecWare.DE.Networking;
using TecWare.DE.Stuff;
using TecWare.PPSn.Bde;
using TecWare.PPSn.Data;
using TecWare.PPSn.Main;
using TecWare.PPSn.Properties;
using TecWare.PPSn.UI;

namespace TecWare.PPSn
{
	/// <summary></summary>
	public partial class App : Application, IPpsShellApplication
	{
		#region -- class BindingErrorListener -----------------------------------------

		private sealed class BindingErrorListener : TraceListener
		{
			private readonly App app;

			public BindingErrorListener(App app)
				=> this.app = app ?? throw new ArgumentNullException(nameof(app));

			public override void Write(string message) { }

			public override void WriteLine(string message)
				=> app.LogException(LogMsgType.Warning, message.Replace(";", ";\n"));
		} // class BindingErrorListener

		#endregion

		#region -- class AppStartArguments --------------------------------------------

		private sealed class AppStartArguments
		{
			public int WaitProcessId { get; set; } = -1;
			public IPpsShellInfo ShellInfo { get; set; } = null;
			public ICredentials UserInfo { get; set; } = null;
			public bool DoAutoLogin { get; set; } = false;
			public bool AllowSync { get; set; } = true;
		} // class AppStartArguments

		#endregion

		#region -- class ExitApplicationException -------------------------------------

		private sealed class ExitApplicationException : Exception
		{
			private readonly bool restart;
			private readonly IPpsShellInfo shellInfo;
			private readonly ICredentials userInfo;

			public ExitApplicationException(bool restart, IPpsShellInfo shellInfo, ICredentials userInfo)
				: base("Restart application")
			{
				this.restart = restart;
				this.shellInfo = shellInfo;
				this.userInfo = userInfo;
			} // ctor

			public bool Restart => restart;
			public IPpsShellInfo ShellInfo => shellInfo;
			public ICredentials UserInfo => userInfo;
		} // class ExitApplicationException

		#endregion

		#region -- class ShellLoadNotify ----------------------------------------------

		private sealed class ShellLoadNotify : IPpsShellLoadNotify
		{
			#region -- enum FileLoadFlag ----------------------------------------------

			[Flags]
			private enum FileLoadFlag
			{
				None = 0,
				Net = 1,
				Path = 2
			} // enum FileLoadFlag

			#endregion

			#region -- class FileVerifyInfo -------------------------------------------

			private sealed class FileVerifyInfo
			{
				private readonly string fileId;
				private readonly Uri uri;
				private readonly FileInfo fi;
				private readonly long expectedLength;
				private readonly DateTime expectedLastWriteTime;
				private readonly FileLoadFlag loadFlags;

				private FileVerifyInfo(string fileId, FileInfo fi, Uri uri, long expectedLength, DateTime expectedLastWriteTime, FileLoadFlag loadFlags)
				{
					this.fileId = fileId;
					this.fi = fi;
					this.uri = uri;
					this.expectedLength = expectedLength;
					this.expectedLastWriteTime = expectedLastWriteTime;
					this.loadFlags = loadFlags;
				} // ctor

				public Process IsApplicationBlocked()
				{
					if (String.Compare(fi.Extension, ".exe", StringComparison.OrdinalIgnoreCase) != 0)
						return null;

					foreach (var p in Process.GetProcessesByName(fi.Name).Where(c => c.StartInfo != null))
					{
						if (String.Compare(p.StartInfo.FileName, fi.FullName, StringComparison.OrdinalIgnoreCase) == 0)
							return p;
					}

					return null;
				} // func IsApplicationBlocked

				public string FileId => fileId;
				public Uri Uri => uri;
				public FileInfo FileInfo => fi;
				public long Length => expectedLength;
				public DateTime LastWriteTime => expectedLastWriteTime;
				public FileLoadFlag Load => loadFlags;

				public bool NeedsUpdate => !fi.Exists || fi.Length != expectedLength || expectedLastWriteTime > fi.LastWriteTime;

				// -- Static --------------------------------------------------

				public static FileVerifyInfo Create(string groupName, DirectoryInfo baseDirectory, string path, string source, long expectedLength, string expectedLastWriteTimeText, string loadFlags)
				{
					if (groupName == null)
						throw new ArgumentNullException(nameof(groupName));
					if (baseDirectory == null)
						throw new ArgumentNullException(nameof(baseDirectory));

					string GetPropertyName(string name)
						=> "PPSn.Application.Files." + groupName + "." + name;

					if (String.IsNullOrEmpty(source))
						throw new ArgumentNullException(GetPropertyName("Uri"));
					if (String.IsNullOrEmpty(path))
						throw new ArgumentNullException(GetPropertyName("Path"));
					if (path.Contains(".."))
						throw new ArgumentOutOfRangeException(GetPropertyName("Path"), path, "Invalid relative path.");
					if (expectedLength < 0)
						throw new ArgumentNullException(GetPropertyName("Length"));
					if (expectedLastWriteTimeText == null)
						throw new ArgumentNullException(GetPropertyName("LastWriteTime"));

					if (!Uri.TryCreate(source, UriKind.Absolute, out var uri))
						throw new ArgumentOutOfRangeException(GetPropertyName("Uri"), source, "Invalid uri.");

					if (!DateTime.TryParse(expectedLastWriteTimeText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var expectedLastWriteTime))
						throw new ArgumentOutOfRangeException(GetPropertyName("LastWriteTime"), expectedLastWriteTimeText, "Could not parse timestamp.");

					var flags = FileLoadFlag.None;
					if (loadFlags != null)
					{
						if (loadFlags.Contains("net"))
							flags |= FileLoadFlag.Net;
						if (loadFlags.Contains("path"))
							flags |= FileLoadFlag.Path;
					}

					return new FileVerifyInfo(groupName, new FileInfo(Path.Combine(baseDirectory.FullName, path)), uri, expectedLength, expectedLastWriteTime, flags);
				} // func Create
			} // class FileVerifyInfo

			#endregion

			private readonly PpsSplashWindow splashWindow;
			private readonly bool allowSync;

			public ShellLoadNotify(PpsSplashWindow splashWindow, bool allowSync)
			{
				this.splashWindow = splashWindow;
				this.allowSync = allowSync;
			} // ctor

			Task IPpsShellLoadNotify.OnAfterInitServicesAsync(IPpsShell shell)
			{
				// register pane types
				var paneRegister = shell.GetService<IPpsKnownWindowPanes>(true);
				paneRegister.RegisterPaneType(typeof(PpsPicturePane), "picture", MimeTypes.Image.Png, MimeTypes.Image.Bmp, MimeTypes.Image.Jpeg);
				paneRegister.RegisterPaneType(typeof(PpsPdfViewerPane), "pdf", MimeTypes.Application.Pdf);
				paneRegister.RegisterPaneType(typeof(PpsMarkdownPane), "markdown", MimeTypes.Text.Markdown);

				// change PpsLiveData
				var liveData = shell.GetService<PpsLiveData>(true);
				liveData.SetDebugLog(shell.GetService<ILogger>(true));
				liveData.DataChanged += (sender, e) => CommandManager.InvalidateRequerySuggested();

				return Task.CompletedTask;
			} // func OnAfterInitServicesAsync

			private static async Task<Stream> OpenFileSafe(IPpsProgress progress, FileInfo fileInfo)
			{
				// create directory if not exists
				if (!fileInfo.Directory.Exists)
					fileInfo.Directory.Create();

				if (!fileInfo.Exists)
					return fileInfo.Create();
				else
				{
					var count = 0;
					while (true)
					{
						try
						{
							return fileInfo.Create();
						}
						catch (IOException)
						{
							progress.Text = String.Format("Datei '{0}' ist blockiert (warte auf Freigabe, {1})...", fileInfo.Name, ++count);
						}
						await Task.Delay(1000);
					}
				}
			} // func OpenFileSafe

			private static async Task DownloadFileAsync(IPpsProgress progress, DEHttpClient http, Uri uri, FileInfo fileInfo, long expectedLength, DateTime lastWriteTimeUtc, long progressMin, long progressCount)
			{
				using (var dst = await OpenFileSafe(progress, fileInfo))
				using (var src = await http.GetStreamAsync(uri))
				{
					var totalReaded = 0L;

					while (true)
					{
						// update progress
						var percent = (int)(progressMin + Math.Min(totalReaded, expectedLength) * progressCount / expectedLength);
						progress.Value = percent;
						progress.Text = String.Format("Kopiere {0} ({1:N1}%)...", fileInfo.Name, percent / 10.0f);

						// copy data
						var buf = new byte[0x4000];
						var readed = await src.ReadAsync(buf, 0, buf.Length);
						if (readed <= 0)
							break;

						totalReaded += readed;
						await dst.WriteAsync(buf, 0, readed);
					}

					// update length of file
					dst.SetLength(dst.Position);
				}

				fileInfo.LastWriteTimeUtc = lastWriteTimeUtc;
				fileInfo.Refresh();
			} // proc DownloadFileAsync

			async Task IPpsShellLoadNotify.OnAfterLoadSettingsAsync(IPpsShell shell)
			{
				var log = shell.LogProxy();

				using (var progress = splashWindow.CreateProgress(false))
				{
					progress.Text = "Analysiere die lokalen Dateien...";

					// prepare directory
					var filesDirectoryInfo = shell.EnforceLocalPath("common$");

					// check for extended files
					var extendedFiles = shell.Settings.GetGroups("PPSn.Application.Files", true, "Uri", "Path", "Length", "LastWriteTime", "Load")
						.Select(g => FileVerifyInfo.Create(
							g.GroupName,
							filesDirectoryInfo,
							g.GetProperty("Path", null),
							g.GetProperty("Uri", null),
							g.GetProperty("Length", -1L),
							g.GetProperty("LastWriteTime", null),
							g.GetProperty("Load", null)
						))
						.ToArray();

					// analyze file information
					var pathAdditions = new List<string>();
					var totalBytesToUpdate = 0L;
					foreach (var cur in extendedFiles)
					{
						// check directory for path variable
						if ((cur.Load & FileLoadFlag.Path) != 0)
						{
							log.Debug($"Path {cur.FileId}: {cur.FileInfo.DirectoryName}");
							AddToDirectoryList(-1, pathAdditions, cur.FileInfo.DirectoryName);
						}

						// check outdated file
						if (cur.NeedsUpdate)
						{
							log.Debug($"Schedule {cur.FileInfo} for update: {cur.Uri}");
							totalBytesToUpdate += cur.Length;
						}
					}

					// we need to sync
					if (allowSync && totalBytesToUpdate > 0)
					{
						// check for running processes
						foreach (var cur in extendedFiles)
						{
							while (true)
							{
								var process = cur.IsApplicationBlocked();
								if (process == null)
									break;
								await WaitForProcessAsync(progress, process);
							}
						}

						// update new files
						var totalCopiedFileLength = 0L;
						foreach (var cur in extendedFiles.Where(c => c.NeedsUpdate))
						{
							// copy file
							await DownloadFileAsync(progress, shell.Http, cur.Uri, cur.FileInfo, cur.Length, cur.LastWriteTime,
								totalCopiedFileLength * 1000 / totalBytesToUpdate,
								cur.Length * 1000 / totalBytesToUpdate
							);

							totalCopiedFileLength += cur.Length;
						}
					}

					// locate alternative paths
					var i = 0;
					foreach (var c in shell.Settings.GetProperties("PPSn.Application.Debug.Path.*"))
					{
						log.Debug($"Path {c.Key}: {c.Value}");
						AddToDirectoryList(i++, pathAdditions, c.Value);
					}

					// set environment to extended files
					Environment.SetEnvironmentVariable("PATH",
						Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Process) + ";" + String.Join(";", pathAdditions),
						EnvironmentVariableTarget.Process
					);

					// load additional assemblies from extended files
					foreach (var f in extendedFiles.Where(c => (c.Load & FileLoadFlag.Net) != 0))
					{
						// load assembly
						var assemblyName = AssemblyName.GetAssemblyName(f.FileInfo.FullName);
						AddAssemblyResolveDirectory(f.FileInfo.DirectoryName);

						progress.Text = String.Format("Lade Anwendungsmodul {0}...", assemblyName.Name);
						var asm = Assembly.Load(assemblyName);
						log.Debug($"Assembly {f.FileId} loaded: {asm.Location}");

						// add alias for type resolver
						AddAssemblyAlias(f.FileId, asm);

						// collect services
						PpsShell.Collect(asm);
					}
				}
				splashWindow.SetProgressText("Lade Anwendungsmodule...");
			} // proc OnAfterLoadSettingsAsync

			Task IPpsShellLoadNotify.OnBeforeLoadSettingsAsync(IPpsShell shell)
				=> Task.CompletedTask;
		} // class ShellLoadNotify

		#endregion

		private IPpsShell shell = null;

		/// <summary>Start application</summary>
		public App()
		{
			DispatcherUnhandledException += App_DispatcherUnhandledException;

			AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
			AppDomain.CurrentDomain.TypeResolve += CurrentDomain_TypeResolve;

#if DEBUG
			AddAssemblyResolveDirectory(Environment.CurrentDirectory);
#endif
			AddAssemblyResolveDirectory(Path.GetDirectoryName(typeof(App).Assembly.Location));

			PresentationTraceSources.DataBindingSource.Listeners.Add(new BindingErrorListener(this));
		} // ctor

		#region -- OnStartup, OnExit --------------------------------------------------

		private static IPpsWindowPaneManager GetMainWindowPaneManager(IPpsShell shell, string applicationMode)
		{
			if (applicationMode == null || String.Compare(applicationMode, "main", StringComparison.OrdinalIgnoreCase) == 0) // start window service
				return shell.GetService<IPpsMainWindowService>(true);
			else if (String.Compare(applicationMode, "bde", StringComparison.OrdinalIgnoreCase) == 0)
			{
				var bde = new PpsBdeWindow(shell);
				bde.Show();
				return bde;
			}
			else
				throw new ArgumentOutOfRangeException(nameof(applicationMode), applicationMode, "Invalid application mode.");
		} // func GetMainWindowManager

		private async Task<IPpsShell> StartApplicationShellAsync(PpsSplashWindow splashWindow, IPpsShellInfo shellInfo, bool allowSync)
		{
			object errorInfo = null;
			while (true)
			{
				// select a shell by user
				if (shellInfo == null || errorInfo != null)
				{
					if (errorInfo != null)
						await splashWindow.SetErrorAsync(errorInfo, null);

					shellInfo = await splashWindow.ShowShellAsync(shellInfo);
					if (shellInfo == null)
						return null; // cancel pressed, exit application
				}

				// try start shell
				if (errorInfo != null) // previous shell loaded with error -> restart to load
					throw new ExitApplicationException(true, shellInfo, null);

				try
				{
					// create the application shell
					splashWindow.SetProgressText("Starte Anwendung...");
					return await PpsShell.StartAsync(shellInfo, isDefault: true, notify: new ShellLoadNotify(splashWindow, allowSync));
				}
				catch (Exception e)
				{
					errorInfo = e;
				}
			}
		} // func StartApplicationShellAsync

		private async Task<ICredentials> StartApplicationLoginAsync(PpsSplashWindow splashWindow, bool autoLogin, ICredentials userInfo, IPpsShell newShell, object errorInfo)
		{
			if (!autoLogin || userInfo == null || errorInfo != null) // show login page
			{
				if (errorInfo != null)
					await splashWindow.SetErrorAsync(errorInfo, newShell);

				var (newLoginShellInfo, newUserInfo) = await splashWindow.ShowLoginAsync(newShell, userInfo);
				if (newUserInfo == null)
					return null;
				else if (newLoginShellInfo != null && !newLoginShellInfo.Equals(newShell.Info))
				{
					await newShell.ShutdownAsync();  // shutdown partly loaded shell
					throw new ExitApplicationException(true, newShell.Info, newUserInfo);
				}

				return newUserInfo;
			}
			else
				return userInfo;
		} // func StartApplicationLoginAsync

		private bool TryGetProcessById(int processId, out Process process)
		{
			try
			{
				if (processId > 0)
				{
					process = Process.GetProcessById(processId);
					return process != null;
				}
				else
				{
					process = null;
					return false;
				}
			}
			catch
			{
				process = null;
				return false;
			}
		} // func GetProcessById

		private static async Task WaitForProcessAsync(IPpsProgress progress, Process process)
		{
			progress.Text = String.Format("Warte auf Beenden von '{0}' ({1}, {2})...", process.MainWindowTitle, process.ProcessName, process.Id);
			await Task.Run(new Action(process.WaitForExit));
		} // func WaitForProcessAsync

		private async Task<bool> StartApplicationAsync(AppStartArguments args)
		{
			// we will have no windows
			ShutdownMode = ShutdownMode.OnExplicitShutdown;

			// show a login/splash
			var splashWindow = new PpsSplashWindow()
			{
				Owner = Current.Windows.OfType<PpsMainWindow>().FirstOrDefault(),
				StatusText = PPSn.Properties.Resources.AppStartApplicationAsyncInitApp
			};
			splashWindow.Show();

			// wait for process id
			if (TryGetProcessById(args.WaitProcessId, out var process))
			{
				using (var progress = splashWindow.CreateProgress(false))
					await WaitForProcessAsync(progress, process);
			}

			try
			{
				// first load a shell
				// always restart to change shell, because a load libraries can only load once
				var newShell = await StartApplicationShellAsync(splashWindow, args.ShellInfo, args.AllowSync);
				if (newShell == null)
					return false;

				// init keyboard scanner
				KeyboardScanner.Init();

				// get login handler
				var settings = newShell.GetSettings<PpsWpfShellSettings>();

				// auto login for a user
				var userInfo = args.UserInfo;
				var autoLogin = args.DoAutoLogin;

				// try find auto login
				var autoUserInfo = settings.GetAutoLogin();
				if (autoUserInfo != null)
				{
					userInfo = autoUserInfo;
					autoLogin = true;
				}

				object errorInfo = null;
				while (true)
				{
					try
					{
						// request login information
						userInfo = await StartApplicationLoginAsync(splashWindow, autoLogin, userInfo, newShell, errorInfo);
						if (userInfo == null)
							return false;

						autoLogin = false;
						await newShell.LoginAsync(userInfo);

						// start up main window manager
						var mw = GetMainWindowPaneManager(newShell, settings.ApplicationMode);

						// open first window
						var paneType = String.IsNullOrEmpty(settings.ApplicationModul) ? null : Type.GetType(settings.ApplicationModul, true);
						await mw.OpenPaneAsync(paneType);

						// set active shell
						shell = newShell;

						// now, we have windows
						ShutdownMode = ShutdownMode.OnLastWindowClose;
						return true;
					}
					catch (ExitApplicationException)
					{
						throw;
					}
					catch (Exception e)
					{
						errorInfo = e;
					}
				}
			}
			catch (ExitApplicationException e)
			{
				if (e.Restart)
					InvokeRestartCore(e.ShellInfo, e.UserInfo);
				return false;
			}
			catch (Exception e)
			{
				PpsShell.GetService<IPpsUIService>(true).ShowException(e);
				return false;
			}
			finally
			{
				splashWindow.ForceClose();
			}
		} // proc StartApplicationAsync

		private void InvokeRestartCore(IPpsShellInfo shellInfo, ICredentials userInfo)
		{
			var sb = new StringBuilder();

			// wait info
			sb.Append("-w").Append(Process.GetCurrentProcess().Id).Append(' ');

			// shell info
			if (shellInfo != null)
				sb.Append("-a").Append(shellInfo.Name);

			// user info
			if (userInfo != null)
			{
				if (userInfo == CredentialCache.DefaultCredentials)
					sb.Append("-u.");
				else
				{
					string GetUserName(string domain, string userName)
						=> String.IsNullOrEmpty(domain) ? userName : domain + "\\" + userName;

					var networkCredentials = userInfo.GetCredential(null, "basic");
					sb.Append("-u").Append(GetUserName(networkCredentials.Domain, networkCredentials.UserName)).Append(' ');

					var pwd = networkCredentials.Password;
					if (!String.IsNullOrEmpty(pwd))
						sb.Append("-p").Append(pwd).Append(' ');
				}
			}

			// start application
			var fileName = typeof(App).Assembly.Location;
			var arguments = sb.ToString();
#if DEBUG
			MessageBox.Show($"Restart:\n{fileName}\n{arguments}");
#endif
			Process.Start(fileName, arguments);
		} // proc InvokeRestartCore

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
			PpsShell.Global.AddService(typeof(IPpsShellApplication), this);
			PpsShell.Collect(typeof(StuffUI).Assembly);
			PpsShell.Collect(typeof(App).Assembly);

			FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement), new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag)));

			StartApplicationAsync(ParseArguments(e))
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

		private static AppStartArguments ParseArguments(StartupEventArgs e)
		{
			var userName = (string)null;
			var userPass = (string)null;

			var r = new AppStartArguments();

			if (e.Args.Length == 0)
				return r;

			var localShells = new Lazy<IPpsShellInfo[]>(PpsShell.GetShellInfo().ToArray);

			// first parse arguments for environment or user information
			foreach (var arg in e.Args)
			{
				if (arg.Length > 1 && arg[0] == '-')
				{
					var cmd = arg[1];
					switch (cmd)
					{
						case 'u': // username
							userName = arg.Substring(2);
							break;
						case 'p': // password
							userPass = arg.Substring(2);
							break;
						case 'a': // shell id
							var shellName = arg.Substring(2);
							r.ShellInfo = localShells.Value.FirstOrDefault(c => String.Compare(c.Name, shellName, StringComparison.OrdinalIgnoreCase) == 0);
							break;
						case 'w': // wait process id
							if (Int32.TryParse(arg.Substring(2), out var pid))
								r.WaitProcessId = pid;
							break;
						case '-':
							if (arg == "--nosync")
							{
								r.AllowSync = false;
								noRestart = true;
							}
							else if(arg == "--norestart")
							{
								noRestart = true;
							}
							break;
					}
				}
			}

			// set user credentials
			if (userName != null)
			{
				r.UserInfo = IsDefaultUser(userName) ? CredentialCache.DefaultCredentials : UserCredential.Create(userName, userPass);
				r.DoAutoLogin = true;
			}
			else if (r.ShellInfo != null) // load user name from environment
			{
				using (var pcl = new PpsClientLogin("ppsn_env:" + r.ShellInfo.Uri.ToString(), r.ShellInfo.Name, false))
					r.UserInfo = pcl.GetCredentials();
			}

			return r;
		} // func ParseArguments

		private static void CoreExceptionHandler(Exception ex)
		{
			if (MessageBox.Show(String.Format("Unbehandelte Ausnahme: {0}.\n\nDetails in der Zwischenablage ablegen?", ex.Message), "Fehler", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
				Clipboard.SetText(ex.GetMessageString());
		} // proc CoreExceptionHandler

		private void LogException(LogMsgType type, string message)
			=> (shell ?? PpsShell.Global).GetService<ILogger>(false)?.LogMsg(type, message);

		private void LogException(Exception e)
			=> (shell ?? PpsShell.Global).GetService<ILogger>(false)?.LogMsg(LogMsgType.Error, e);

		private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
		{
			LogException(e.Exception);
			CoreExceptionHandler(e.Exception);
			e.Handled = true;
		} // event App_DispatcherUnhandledException

		private static bool IsDefaultUser(string userName)
			=> userName == "." || String.Compare(userName, Environment.UserDomainName + "\\" + Environment.UserName, StringComparison.OrdinalIgnoreCase) == 0;

		#endregion

		#region -- Assembly Loader ----------------------------------------------------

		private readonly static Dictionary<string, Assembly> assemblyAlias = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);
		private readonly static List<string> assemblyResolvePaths = new List<string>();

		private static void AddToDirectoryList(int index, List<string> directoryList, string directoryName)
		{
			var idx = directoryList.FindIndex(c => String.Compare(c, directoryName, StringComparison.OrdinalIgnoreCase) == 0);
			if (idx != -1)
				directoryList.RemoveAt(idx);

			if (index == -1)
				directoryList.Add(directoryName);
			else
				directoryList.Insert(index, directoryName);
		} // proc AddToDirectoryList

		private static void AddAssemblyAlias(string alias, Assembly assembly)
			=> assemblyAlias[alias] = assembly;

		private static void AddAssemblyResolveDirectory(string directoryName)
		{
			lock (assemblyResolvePaths)
				AddToDirectoryList(-1, assemblyResolvePaths, directoryName);
		} // proc AddAssemblyResolveDirectory

		private static FileInfo LocateAssemblyFile(string directoryName, string name, string extension)
		{
			var fi = new FileInfo(Path.Combine(directoryName, name + extension));
			return fi.Exists ? fi : null;
		} // func LocateAssemblyFile

		private static IEnumerable<FileInfo> LocateAssemblyFiles(string name)
		{
			foreach (var directoryName in assemblyResolvePaths)
			{
				var fi = LocateAssemblyFile(directoryName, name, ".dll") ?? LocateAssemblyFile(directoryName, name, ".exe");
				if (fi != null)
					yield return fi;
			}
		} // func LocateAssemblyFiles

		private static FileInfo LocateAssemblyFile(string name)
		{
			FileInfo choosen = null;

			foreach (var fi in LocateAssemblyFiles(name))
			{
				if (choosen == null || choosen.LastWriteTimeUtc < fi.LastWriteTimeUtc)
					choosen = fi;
			}

			return choosen;
		} // func LocateAssemblyFile

		private static Assembly ResolveAssemblyCore(string name)
		{
			// check for assembly alias
			if (assemblyAlias.TryGetValue(name, out var asm))
				return asm;

			var assemblyName = new AssemblyName(name);

			// check if the file is loaded
			foreach (var cur in AppDomain.CurrentDomain.GetAssemblies())
			{
				if (!cur.IsDynamic)
				{
					var curName = cur.GetName();
					if (curName.Name == assemblyName.Name
						&& curName.Version == assemblyName.Version)
						return cur;
				}
			}

			// load assembly
			var fi = LocateAssemblyFile(assemblyName.Name);
			return fi == null ? null : Assembly.LoadFile(fi.FullName);
		} // func ResolveAssemblyCore

		private static Assembly CurrentDomain_TypeResolve(object sender, ResolveEventArgs args)
		{
			var pos = args.Name.IndexOf(',');
			if (pos == -1)
				return null;

			var assemblyName = args.Name.Substring(pos + 1);
			return ResolveAssemblyCore(assemblyName);
		} // event CurrentDomain_TypeResolve

		private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
			=> ResolveAssemblyCore(args.Name);

		#endregion

		#region -- IPpsShellApplication - members -------------------------------------

		private const string applicationName = "PPSnDesktop";
		private static bool noRestart = false;

		private static Version GetInstalledVersionDefault()
		{
			using (var reg = Registry.CurrentUser.OpenSubKey(@"Software\TecWare\" + applicationName + @"\Components", false))
			{
				return reg?.GetValue(null) is string v
					? new Version(v)
					: new Version(1, 0, 0, 0);
			}
		} // func GetInstalledVersionDefault

		Task IPpsShellApplication.RequestUpdateAsync(IPpsShell shell, Uri uri)
		{
			if (noRestart)
				return Task.CompletedTask;

			// run msi
			var msiExe = Path.Combine(Environment.SystemDirectory, "msiexec.exe");
			var msiLogFile = Path.Combine(Path.GetTempPath(), applicationName + ".msi.txt");
			var psi = new ProcessStartInfo(msiExe, "/i " + uri.ToString() + " /qb /l*v \"" + msiLogFile + "\" SHELLNAME=" + shell.Info.Name);

#if DEBUG
			MessageBox.Show($"RunMSI: {psi.FileName} {psi.Arguments}", "Debug");
			return Task.CompletedTask;
#else
			Process.Start(psi);
			throw new ExitApplicationException(false, null, null); // means quit application
#endif
		} // proc IPpsShellApplication.RequaestUpdateAsync

		Task IPpsShellApplication.RequestRestartAsync(IPpsShell shell)
		{
			if (noRestart)
				return Task.CompletedTask;

			throw new ExitApplicationException(true, shell.Info, shell.Http.Credentials);
		} // proc IPpsShellApplication.RequestRestartAsync

		string IPpsShellApplication.Name => applicationName;
		Version IPpsShellApplication.AssenblyVersion => PpsShell.GetDefaultAssemblyVersion(this);
		Version IPpsShellApplication.InstalledVersion => GetInstalledVersionDefault();

		#endregion

		/// <summary>Return the current environemnt</summary>
		public IPpsShell Shell => shell;
	} // class App
}
