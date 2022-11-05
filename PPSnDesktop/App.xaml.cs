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
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Threading;
using System.Xml.Linq;
using Microsoft.Win32;
using Neo.IronLua;
using TecWare.DE.Networking;
using TecWare.DE.Stuff;
using TecWare.PPSn.Bde;
using TecWare.PPSn.Controls;
using TecWare.PPSn.Data;
using TecWare.PPSn.Main;
using TecWare.PPSn.Properties;
using TecWare.PPSn.Themes;
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
			public int WaitTime { get; set; } = -1;
			public IPpsShellInfo ShellInfo { get; set; } = null;
			public ICredentials UserInfo { get; set; } = null;
			public bool DoAutoLogin { get; set; } = false;
			public bool AllowSync { get; set; } = true;
			public bool EnforceShellSelection { get; set; } = false;
		} // class AppStartArguments

		#endregion

		#region -- class ExitApplicationException -------------------------------------

		private sealed class ExitApplicationException : Exception
		{
			private readonly bool restart;
			private readonly IPpsShellInfo shellInfo;
			private readonly ICredentials userInfo;

			public ExitApplicationException(string reason, bool restart, IPpsShellInfo shellInfo, ICredentials userInfo)
				: base("Restart application: " + reason)
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

		#region -- class RestartIfSettingChangedNotifier ------------------------------

		[DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
		private sealed class RestartIfSettingChangedNotifier<T> : IPpsSettingRestartCondition
		{
			private readonly string propertyPath;
			private readonly T originalValue;

			public RestartIfSettingChangedNotifier(string propertyPath, T originalValue)
			{
				this.propertyPath = propertyPath ?? throw new ArgumentNullException(nameof(propertyPath));
				this.originalValue = originalValue;
			} // ctor

			public override string ToString()
				=> $"Setting: {propertyPath}: {originalValue}";

			private string GetDebuggerDisplay()
				=> ToString();

			public bool IsChanged(PpsSettingsInfoBase settings)
				=> !Equals(settings.GetProperty<T>(propertyPath, default), originalValue);

			public string Setting => propertyPath;
		} // class RestartIfSettingChangedNotifier

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
				private readonly string installArguments;

				private FileVerifyInfo(string fileId, FileInfo fi, Uri uri, long expectedLength, DateTime expectedLastWriteTime, FileLoadFlag loadFlags, string installArguments)
				{
					this.fileId = fileId;
					this.fi = fi;
					this.uri = uri;
					this.expectedLength = expectedLength;
					this.expectedLastWriteTime = expectedLastWriteTime;
					this.loadFlags = loadFlags;
					this.installArguments = installArguments;
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

				public IReadOnlyList<IPpsSettingRestartCondition> CreateRestartNotifier()
				{
					return new IPpsSettingRestartCondition[]
					{
						new RestartIfSettingChangedNotifier<long>(GetPropertyName(FileId, "Length"), expectedLength),
						new RestartIfSettingChangedNotifier<DateTime>(GetPropertyName(FileId, "LastWriteTime"), expectedLastWriteTime)
					};
				} // func CreateRestartNotifier

				private string GetRuntimeMessage(PpsShellSettings settings)
					=> $"{settings.GetProperty(GetPropertyName(FileId, "Description"), FileId)} / Version {settings.GetProperty(GetPropertyName(FileId, "VersionTest"), settings.GetProperty(GetPropertyName(FileId, "Version"), "Unknown"))}";

				public bool IsRuntimeMissing(PpsShellSettings settings, out string message)
				{
					if (!IsRuntime)
					{
						message = null;
						return false;
					}

					var registryKey = settings.GetProperty(GetPropertyName(fileId, "VersionKey"), null);
					var testValue = settings.GetProperty(GetPropertyName(fileId, "VersionTest"), null);
					if (String.IsNullOrEmpty(registryKey) || String.IsNullOrEmpty(testValue))
					{
						message = GetRuntimeMessage(settings);
						return NeedsUpdate;
					}

					var p = registryKey.LastIndexOf('\\');
					if (p == -1)
						throw new ArgumentException("Invalid VersionKey");

					var keyName = registryKey.Substring(0, p);
					var valueName = registryKey.Substring(p + 1);

					using (var reg = Registry.LocalMachine.OpenSubKey(keyName, false))
					{
						if (reg == null)
						{
							message = GetRuntimeMessage(settings);
							return true;
						}
						var compareValue = reg.GetValue(valueName)?.ToString();
						if (compareValue == null)
						{
							message = GetRuntimeMessage(settings);
							return true;
						}
						else if (Int32.TryParse(compareValue, out var compareInt) && Int32.TryParse(testValue, out var testInt))
						{
							if (compareInt < testInt)
							{
								message = GetRuntimeMessage(settings);
								return true;
							}
						}
						else if (Version.TryParse(compareValue, out var compareVersion) && Version.TryParse(testValue, out var testVersion))
						{
							if (compareVersion < testVersion)
							{
								message = GetRuntimeMessage(settings);
								return true;
							}
						}

						message = null;
						return false;
					}
				} // func IsRuntimeMissing

				public string FileId => fileId;
				public Uri Uri => uri;
				public FileInfo FileInfo => fi;
				public long Length => expectedLength;
				public DateTime LastWriteTime => expectedLastWriteTime;
				public FileLoadFlag Load => loadFlags;

				public bool IsRuntime => !String.IsNullOrEmpty(installArguments);
				public string RuntimeArguments => installArguments;

				public bool NeedsUpdate => !fi.Exists || fi.Length != expectedLength || expectedLastWriteTime > fi.LastWriteTime;
				// bei .config stimmt die länge nie?

				// -- Static --------------------------------------------------

				private static string GetPropertyName(string groupName, string name)
					=> "PPSn.Application.Files." + groupName + "." + name;

				public static FileVerifyInfo Create(string groupName, DirectoryInfo baseDirectory, string path, string source, long expectedLength, string expectedLastWriteTimeText, string loadFlags, string installArguments)
				{
					if (groupName == null)
						throw new ArgumentNullException(nameof(groupName));
					if (baseDirectory == null)
						throw new ArgumentNullException(nameof(baseDirectory));

					if (String.IsNullOrEmpty(source))
						throw new ArgumentNullException(GetPropertyName(groupName, "Uri"));
					if (String.IsNullOrEmpty(path))
						throw new ArgumentNullException(GetPropertyName(groupName, "Path"));
					if (path.Contains(".."))
						throw new ArgumentOutOfRangeException(GetPropertyName(groupName, "Path"), path, "Invalid relative path.");
					if (expectedLength < 0)
						throw new ArgumentNullException(GetPropertyName(groupName, "Length"));
					if (expectedLastWriteTimeText == null)
						throw new ArgumentNullException(GetPropertyName(groupName, "LastWriteTime"));

					if (!Uri.TryCreate(source, UriKind.Absolute, out var uri))
						throw new ArgumentOutOfRangeException(GetPropertyName(groupName, "Uri"), source, "Invalid uri.");

					if (!DateTime.TryParse(expectedLastWriteTimeText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var expectedLastWriteTime))
						throw new ArgumentOutOfRangeException(GetPropertyName(groupName, "LastWriteTime"), expectedLastWriteTimeText, "Could not parse timestamp.");

					var flags = FileLoadFlag.None;
					if (loadFlags != null)
					{
						if (loadFlags.Contains("net"))
							flags |= FileLoadFlag.Net;
						if (loadFlags.Contains("path"))
							flags |= FileLoadFlag.Path;
					}

					return new FileVerifyInfo(groupName, new FileInfo(Path.Combine(baseDirectory.FullName, path)), uri, expectedLength, expectedLastWriteTime, flags, installArguments);
				} // func Create
			} // class FileVerifyInfo

			#endregion

			#region -- class MissingRuntime -------------------------------------------

			private sealed class MissingRuntime
			{
				private readonly string installLine;
				private readonly string message;

				public MissingRuntime(string installLine, string message)
				{
					this.installLine = installLine ?? throw new ArgumentNullException(nameof(installLine));
					this.message = message ?? throw new ArgumentNullException(nameof(message));
				} // ctor

				public override string ToString()
					=> message;

				public string InstallLine => installLine;
			} // class MissingRuntime

			#endregion

			private readonly PpsSplashWindow splashWindow;
			private readonly bool allowSync;

			private readonly List<Uri> themesToLoad = new List<Uri>();
			private readonly List<IPpsSettingRestartCondition> restartNotifier = new List<IPpsSettingRestartCondition>();
			private readonly List<MissingRuntime> missingRuntimes = new List<MissingRuntime>();

			public ShellLoadNotify(PpsSplashWindow splashWindow, bool allowSync)
			{
				this.splashWindow = splashWindow;
				this.allowSync = allowSync;
			} // ctor

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
						progress.Text = String.Format("Kopiere {0} ({1:N0}%)...", fileInfo.Name, percent / 10.0f);

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

			Task IPpsShellLoadNotify.OnBeforeLoadSettingsAsync(IPpsShell shell)
				=> Task.CompletedTask;

			async Task IPpsShellLoadNotify.OnAfterLoadSettingsAsync(IPpsShell shell)
			{
				var log = shell.LogProxy();

				using (var progress = splashWindow.CreateProgress(false))
				{
					progress.Text = "Analysiere die lokalen Dateien...";

					// prepare directory
					var filesDirectoryInfo = shell.EnforceLocalPath("common$");

					// check for extended files
					var extendedFiles = shell.Settings.GetGroups("PPSn.Application.Files", true, "Uri", "Path", "Length", "LastWriteTime", "Load", "InstallArguments")
						.Select(g => FileVerifyInfo.Create(
							g.GroupName,
							filesDirectoryInfo,
							g.GetProperty("Path", null),
							g.GetProperty("Uri", null),
							g.GetProperty("Length", -1L),
							g.GetProperty("LastWriteTime", null),
							g.GetProperty("Load", null),
							g.GetProperty("InstallArguments", null)
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

						// check runtime dependency
						if (cur.IsRuntimeMissing(shell.Settings, out var msg))
							missingRuntimes.Add(new MissingRuntime("\"" + cur.FileInfo.FullName + "\" " + cur.RuntimeArguments, msg));

						// add to restart notifier
						restartNotifier.AddRange(cur.CreateRestartNotifier());
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
						AddToDirectoryList(i++, pathAdditions, c.Value); // add first
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
						var asm = ResolveAssemblyCore(assemblyName);

						// add alias for type resolver
						AddAssemblyAlias(f.FileId, asm);

						// collect services
						PpsShell.Collect(asm);

						// test for resources
						if (PpsWpfShell.TryGetResourceUri(asm, "themes/styles.xaml", out var uri))
							themesToLoad.Add(uri);

						log.Debug($"Assembly {f.FileId} loaded: {asm.Location} resources: {(uri?.ToString() ?? "<null>")}");
					}
				}
				splashWindow.SetProgressText("Lade Anwendungsmodule...");
			} // proc OnAfterLoadSettingsAsync

			Task IPpsShellLoadNotify.OnAfterInitServicesAsync(IPpsShell shell)
			{
				var wpfRes = shell.GetService<IPpsWpfResources>(true);
				// load styles from assemblies
				foreach (var uri in themesToLoad)
					wpfRes.AppendResourceDictionary(uri);

				// register pane types
				var paneRegister = shell.GetService<IPpsKnownWindowPanes>(true);
				paneRegister.RegisterPaneType(typeof(PpsPicturePane), "picture", MimeTypes.Image.Png, MimeTypes.Image.Bmp, MimeTypes.Image.Jpeg, "images/tiff");
				paneRegister.RegisterPaneType(typeof(PpsPdfViewerPane), "pdf", MimeTypes.Application.Pdf);
				paneRegister.RegisterPaneType(typeof(PpsMarkdownPane), "markdown", MimeTypes.Text.Markdown);
				paneRegister.RegisterPaneType(typeof(PpsLuaWindowPane), "xaml");
				paneRegister.RegisterPaneType(typeof(PpsLuaWindowPane), "web");

				// change PpsLiveData
				var liveData = shell.GetService<PpsLiveData>(true);
				liveData.SetDebugLog(shell.GetService<ILogger>(true));
				liveData.DataChanged += (sender, e) => CommandManager.InvalidateRequerySuggested();

				// register restart notifier
				var dpcService = shell.GetService<PpsDpcService>(true);
				dpcService.AddSettingRestartConditions(restartNotifier);

				return Task.CompletedTask;
			} // func OnAfterInitServicesAsync

			public async Task ExecuteMissingRuntimeBatchAsync(bool isAdministrator)
			{
				// generate batch file
				var sb = new StringBuilder();
				sb.AppendLine("@echo off");
#if DEBUG
				sb.AppendLine("pause");
#endif
				foreach (var cur in missingRuntimes)
				{
					sb.Append("echo Install ").Append(cur.ToString()).AppendLine();
					// sb.Append("echo ").Append(cur.InstallLine).AppendLine();
					sb.AppendLine(cur.InstallLine);
				}

				// write batch file
				var fileName = Path.Combine(Path.GetTempPath(), "PPSnDesktopRuntimeInstall.bat");
				File.WriteAllText(fileName, sb.ToString());

				// execute batch file
				var psi = new ProcessStartInfo
				{
					FileName = Path.Combine(Environment.SystemDirectory, "cmd.exe"),
					Arguments = "/c \"" + fileName + "\"",
					UseShellExecute = true,
					Verb = isAdministrator ? String.Empty : "runas"
				};

				using (var p = Process.Start(psi))
					await Task.Run(new Action(p.WaitForExit));
			} // proc ExecuteMissingRuntimeBatchAsync

			public IReadOnlyList<object> MissingRuntimes => missingRuntimes;
		} // class ShellLoadNotify

		#endregion

		private IPpsShell shell = null;
		private Mutex applicationMutex = null;
		private bool logoffUser = true;
		private bool isProcessProtected = false;

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

		private async Task<IPpsShell> StartRuntimeInstallationAsync(PpsSplashWindow splashWindow, IPpsShell shell, ShellLoadNotify notify)
		{
			if (notify.MissingRuntimes != null && notify.MissingRuntimes.Count > 0)
			{
				// check admin is needed
				var isAdmin = PpsWpfShell.IsAdministrator();

				// show information
				if (!await splashWindow.ShowRuntimeInstallAsync(notify.MissingRuntimes, isAdmin))
				{
					shell.Dispose();
					return null;
				}

				// start installation
				splashWindow.SetProgressText("Abhängigkeiten Installieren...");
				await notify.ExecuteMissingRuntimeBatchAsync(isAdmin);
			}

			return shell;
		} // func StartRuntimeInstallationAsync

		private async Task<IPpsShell> StartApplicationShellAsync(PpsSplashWindow splashWindow, IPpsShellInfo shellInfo, bool allowSync, bool enforceShellSelection)
		{
			object errorInfo = null;
			while (true)
			{
				// select a shell by user
				if (shellInfo == null || errorInfo != null)
				{
					if (errorInfo != null)
						await splashWindow.SetErrorAsync(errorInfo, null);

					shellInfo = await splashWindow.ShowShellAsync(shellInfo, enforceShellSelection);
					if (shellInfo == null)
						return null; // cancel pressed, exit application
				}

				// try start shell
				if (errorInfo != null) // previous shell loaded with error -> restart to load
					throw new ExitApplicationException("Restart on error", true, shellInfo, null);

				try
				{
					// create the application shell
					splashWindow.SetProgressText("Starte Anwendung...");
					var notify = new ShellLoadNotify(splashWindow, allowSync);
					return await StartRuntimeInstallationAsync(splashWindow,
						await PpsShell.StartAsync(shellInfo, isDefault: true, notify: notify),
						notify
					);
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
					throw new ExitApplicationException("partly loaded shell", true, newShell.Info, newUserInfo);
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

		private static async Task WaitForTimeAsync(IPpsProgress progress, int timeout)
		{
			progress.Text = String.Format($"Warte ({(timeout / 1000.0):N1}sec)...");
			for (var i = 0; i < timeout; i++)
			{
				await Task.Delay(100);
				i += 100;
				progress.Value = i * 1000 / timeout;
			}
		} // func WaitForProcessAsync

		private static void GetApplicationModulParameter(IPpsShell shell, string applicationModul, out Type paneType, out LuaTable paneArguments)
		{
			if (!String.IsNullOrEmpty(applicationModul))
			{
				// split arguments
				var argumentsStart = applicationModul.IndexOf('{');
				string paneTypeName;
				if (argumentsStart >= 0)
				{
					paneTypeName = applicationModul.Substring(0, argumentsStart);
					paneArguments = LuaTable.FromLson(applicationModul.Substring(argumentsStart));
				}
				else
				{
					paneTypeName = applicationModul;
					paneArguments = new LuaTable();
				}

				paneType = shell.GetService<IPpsKnownWindowPanes>(true).GetPaneType(paneTypeName, true);
			}
			else
			{
				paneType = null;
				paneArguments = null;
			}
		} // proc GetApplicationModulParameter

		private static Mutex CreateOwnMutex(string name)
		{
			var mutex = new Mutex(true, name, out var createdNew);
			if (createdNew)
				return mutex;
			else
			{
				mutex.Dispose();
				return null;
			}
		} // func CreateOwnMutex

		private async Task<bool> CreateApplicationMutex(IPpsProgressFactory factory, string name)
		{
			if (String.IsNullOrEmpty(name))
				return true;

			var c = 3;
			IPpsProgress progress = null;
			do
			{
				var m = CreateOwnMutex(name);
				if (m != null)
				{
					applicationMutex = m;
					return true;
				}
				else
				{
					if (progress == null)
						progress = factory.CreateProgress();
					progress.Text = "Warte auf Anwendung...";
					await Task.Delay(3000);
				}
			} while (--c >= 0);

			if (progress != null)
				progress.Dispose();

			return false;
		} // func CreateApplicationMutex

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

			// wait for timeout
			if (args.WaitTime > 0)
			{
				using (var progress = splashWindow.CreateProgress(false))
					await WaitForTimeAsync(progress, args.WaitTime);
			}

			try
			{
				// first load a shell
				// always restart to change shell, because a load libraries can only load once
				var newShell = await StartApplicationShellAsync(splashWindow, args.ShellInfo, args.AllowSync, args.EnforceShellSelection);
				if (newShell == null)
					return false;

				// init keyboard scanner
				KeyboardScanner.Init();

				// get login handler
				var settings = newShell.GetSettings<PpsWpfShellSettings>();

				// handle mutex
				if (!await CreateApplicationMutex(splashWindow, settings.ApplicationMutex))
					return false;

				// auto login for a user
				var userInfo = args.UserInfo ?? GetLastUserInfo(newShell.Info);
				var autoLogin = args.DoAutoLogin;

				// try find auto login
				var autoUserInfo = settings.GetAutoLogin(userInfo);
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
						GetApplicationModulParameter(newShell, settings.ApplicationModul, out var paneType, out var paneArguments);
						await mw.OpenPaneAsync(paneType, arguments: paneArguments);

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
						newShell.LogProxy().Except(e);
						errorInfo = e;
					}
				}
			}
			catch (ExitApplicationException e)
			{
				if (e.Restart)
					InvokeRestartCore(e.ShellInfo, e.Message, e.UserInfo, true);

				if (isProcessProtected)
					logoffUser = false;

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

		private static void InvokeRestartCore(IPpsShellInfo shellInfo, string reason, ICredentials userInfo, bool noRestart)
		{
			var sb = new StringBuilder();

			// wait info
			sb.Append("-w").Append(Process.GetCurrentProcess().Id);
			if (noRestart)
				sb.Append(" --norestart"); // prevent restart loop

			// shell info
			if (shellInfo != null)
				sb.Append(" -a").Append(shellInfo.Name);

			// user info
			if (userInfo != null)
			{
				if (userInfo == CredentialCache.DefaultCredentials)
					sb.Append(" -u.");
				else
				{
					string GetUserName(string domain, string userName)
						=> String.IsNullOrEmpty(domain) ? userName : domain + "\\" + userName;

					var networkCredentials = userInfo.GetCredential(null, "basic");
					sb.Append(" -u").Append(GetUserName(networkCredentials.Domain, networkCredentials.UserName));

					var pwd = networkCredentials.Password;
					if (!String.IsNullOrEmpty(pwd))
						sb.Append(" -p").Append(pwd);
				}
			}

			// start application
			var fileName = typeof(App).Assembly.Location;
			var arguments = sb.ToString();
#if DEBUG
			MessageBox.Show($"Restart: {reason}\n{fileName}\n{arguments}");
#endif
			Process.Start(fileName, arguments);
		} // proc InvokeRestartCore

		/// <summary>Invoke restart</summary>
		/// <param name="shell"></param>
		/// <param name="reason"></param>
		/// <returns></returns>
		public static async Task InvokeRestartAsync(IPpsShell shell, string reason)
		{
			// write log, before restart
			await shell.GetService<PpsDpcService>(true).WriteLogToTempAsync();

			// restart application
			await Current.Dispatcher.InvokeAsync(
				() =>
				{
					InvokeRestartCore(shell.Info, reason, shell.Http.Credentials, false);

					var app = (App)Current;
					if (app.isProcessProtected)
						app.logoffUser = false;
					app.Shutdown();
				}
			);
		} // proc InvokeRestartAsync

		private async Task<bool> CloseApplicationAsync()
		{
			if (shell == null)
				return true;

			if (await shell.ShutdownAsync())
			{
				if (applicationMutex != null)
				{
					applicationMutex.Dispose();
					applicationMutex = null;
				}
				shell = null;
				return true;
			}
			else
				return false;

		} // func CloseApplicationAsync

		private static string GetNativeLibrariesPath()
			=> Path.Combine(Path.GetDirectoryName(typeof(App).Assembly.Location), IntPtr.Size == 8 ? "x64" : "x86");

		private static string EscapeArg(string arg)
			=> arg.IndexOf(" ") >= 0 ? "\"" + arg + "\"" : arg;

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

			// load default theme
			System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(PpsTheme).TypeHandle); // static ctor is not called in release, enforce call
			PpsTheme.UpdateThemedDictionary(Resources.MergedDictionaries, PpsColorTheme.Default);

			// override language
			FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement), new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag)));

			isProcessProtected = e.Args.Contains("--run");
			if (!isProcessProtected && PpsDpcService.GetIsShellMode()) // protect process in shell mode
			{
				var logFileName = Path.Combine(Path.GetTempPath(), "PPSnDesktop.SafeGuard.txt");

				while (true) // restart loop
				{
					using (var p = Process.Start(typeof(App).Assembly.Location, "--run " + String.Join(" ", e.Args.Select(c => EscapeArg(c)))))
					{
						p.WaitForExit();
						if (p.ExitCode == 2682)
						{
							File.WriteAllText(logFileName, $"Schutzprozess: STOP at {DateTime.Now:G}");
							Shutdown(0);
							return;
						}
						File.WriteAllText(logFileName, $"Schutzprozess: RESTART at {DateTime.Now:G} with {p.ExitCode}");
					}
					Thread.Sleep(10000);
				}
			}
			else
			{
				StartApplicationAsync(ParseArguments(e))
					.ContinueWith(t =>
					{
						if (!t.Result)
							Dispatcher.Invoke(Shutdown);
					}
				);
			}

			base.OnStartup(e);
		} // proc OnStartup

		/// <summary>Close application tasks.</summary>
		/// <param name="e"></param>
		protected override void OnExit(ExitEventArgs e)
		{
			base.OnExit(e);
			CloseApplicationAsync().Await();

			if (isProcessProtected)
			{
				if (PpsDpcService.GetIsShellMode() && logoffUser)
					PpsDpcService.LogoffOperationSystem();
				e.ApplicationExitCode = 2682; // mark real shutdown -> no restart
			}
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
							if (r.ShellInfo == null)
								r.EnforceShellSelection = true;
							break;
						case 'w': // wait process id
							if (Int32.TryParse(arg.Substring(2), out var pid))
								r.WaitProcessId = pid;
							break;
						case 'd': // delay
							if (Int32.TryParse(arg.Substring(2), out var timeout))
								r.WaitTime = timeout;
							break;
						case '-':
							if (arg == "--nosync")
							{
								r.AllowSync = false;
								noRestart = true;
							}
							else if (arg == "--norestart")
							{
								noRestart = true;
							}
							else if (arg == "--shell")
							{
								r.EnforceShellSelection = true;
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
				r.UserInfo = GetLastUserInfo(r.ShellInfo);

			return r;
		} // func ParseArguments

		private static ICredentials GetLastUserInfo(IPpsShellInfo shell)
		{
			using (var pcl = new PpsClientLogin(shell.GetCredentialTarget(), shell.Name, false))
				return pcl.GetCredentials();
		} // func GetLastUserInfo

		private static void CoreExceptionHandler(Exception ex)
		{
			if (MessageBox.Show(String.Format("Unbehandelte Ausnahme: {0}.\n\nDetails in der Zwischenablage ablegen?", ex.Message), "Fehler", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
				Clipboard.SetText(ex.GetMessageString());
		} // proc CoreExceptionHandler

		private void LogException(LogMsgType type, string message)
			=> (shell ?? PpsShell.Global).GetService<ILogger>(false)?.LogMsg(type, message);

		private void LogException(Exception e)
			=> (shell ?? PpsShell.Global).GetService<ILogger>(false)?.LogMsg(LogMsgType.Error, e);

		private Exception currentException = null;

		private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
		{
			var newException = e.Exception;
			if (currentException != null)
			{
				if (currentException.GetType() == newException.GetType()
					&& currentException.Message == newException.Message)
				{
					e.Handled = true;
					return; // same exception, ignore
				}
				else
				{
					LogException(newException); // only log, we have already one exception on the screen
					e.Handled = true;
					return;
				}
			}

			currentException = newException;
			try
			{
				LogException(currentException);
				CoreExceptionHandler(currentException);
				e.Handled = true;
			}
			finally
			{
				currentException = null;
			}
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

			if (index == -1 || index >= directoryList.Count)
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

		private static Assembly ResolveAssemblyCore(AssemblyName assemblyName)
		{
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

		private static Assembly ResolveAssemblyCore(string name)
		{
			// check for assembly alias
			if (assemblyAlias.TryGetValue(name, out var asm))
				return asm;

			return ResolveAssemblyCore(new AssemblyName(name));
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

			var dpc = shell.GetService<PpsDpcService>(false);
			if (dpc == null)
			{
				// run msi
				var msiExe = Path.Combine(Environment.SystemDirectory, "msiexec.exe");
				var msiLogFile = Path.Combine(Path.GetTempPath(), applicationName + ".msi.txt");
				var psi = new ProcessStartInfo(msiExe, "/i " + uri.ToString() + " /qb /l*v \"" + msiLogFile + "\" SHELLNAME=" + shell.Info.Name);

#if DEBUG
				MessageBox.Show($"RunMSI: {psi.FileName} {psi.Arguments}", "Debug");
				return Task.CompletedTask;
#else
				Process.Start(psi);
				throw new ExitApplicationException("MsiExec", false, null, null); // means quit application
#endif
			}
			else
			{
				dpc.ScheduleRestart("Shell application version is newer.");
				return Task.CompletedTask;
			}
		} // proc IPpsShellApplication.RequaestUpdateAsync

		Task IPpsShellApplication.RequestRestartAsync(IPpsShell shell)
		{
			if (noRestart)
				return Task.CompletedTask;

			var dpc = shell.GetService<PpsDpcService>(false);
			if (dpc == null)
				throw new ExitApplicationException("DpcRequest", true, shell.Info, shell.Http.Credentials.GetUserNameFromCredentials() != "dpc" ? shell.Http.Credentials : null);
			else
			{
				dpc.ScheduleRestart("Missmatch of application version.");
				return Task.CompletedTask;
			}
		} // proc IPpsShellApplication.RequestRestartAsync

		string IPpsShellApplication.Name => applicationName;
		Version IPpsShellApplication.AssenblyVersion => PpsShell.GetDefaultAssemblyVersion(this);
		Version IPpsShellApplication.InstalledVersion => GetInstalledVersionDefault();

		#endregion

		/// <summary>Return the current environemnt</summary>
		public IPpsShell Shell => shell;

		#region -- Default Link Processing --------------------------------------------

		private static PpsOpenPaneMode GetOpenPaneModeFromLink(IPpsWindowPaneManager paneManager, PpsWebViewLink link)
			=> !(paneManager is IPpsBdeManager) && link.NewWindow ? PpsOpenPaneMode.NewSingleWindow : PpsOpenPaneMode.Default;

		private static void ProcessPaneManager(IPpsWindowPaneManager paneManager, PpsWebViewLink link)
		{
			// get pane information
			var paneType = paneManager.Shell.GetService<IPpsKnownWindowPanes>(false)?.GetPaneType(link.Location.Host) ?? Type.GetType(link.Location.AbsolutePath);
			if (paneType == null)
				throw new ArgumentOutOfRangeException(nameof(link), String.Format("PaneType unknown: {0}", link.Location));

			// open pane
			paneManager.OpenPaneAsync(paneType, GetOpenPaneModeFromLink(paneManager, link), link.Location.GetArgumentsAsTable()).OnException();
		} // event ProcessPaneManager

		private static void ProcessDefaultWebLink(IPpsWindowPaneManager paneManager, PpsWebViewLink link)
			=> paneManager.OpenPaneAsync(typeof(PpsWebViewPane), GetOpenPaneModeFromLink(paneManager, link), new LuaTable { ["uri"] = link.Location });

		private static void ProcessDefaultLink(IPpsWindowPaneManager paneManager, PpsWebViewLink link)
		{
			try
			{
				if (link.Location.IsAbsoluteUri)
				{
					if (link.Location.Scheme == "panemanager")
						ProcessPaneManager(paneManager, link);
					else if (link.Location.Scheme == "shell")
						PpsDpcService.Execute(link.Location.AbsolutePath.Substring(1));
					else
						ProcessDefaultWebLink(paneManager, link);
				}
				else
					ProcessDefaultWebLink(paneManager, link);
			}
			catch (Exception e)
			{
				paneManager.ShowException(e, String.Format("Could not execute link: {0}", link.Location));
			}
		} // proc ProcessDefaultLink

		private static void LinkCommandExecuted(object sender, ExecutedRoutedEventArgs e)
		{
			if (e.Source is DependencyObject d)
			{
				var paneManager = d.GetControlService<IPpsWindowPaneManager>(false);
				if (paneManager != null)
				{
					switch (e.Parameter)
					{
						case PpsWebViewLink l:
							ProcessDefaultLink(paneManager, l);
							break;
						case Uri uri:
							ProcessDefaultLink(paneManager, new PpsWebViewLink(uri));
							break;
						case string url:
							if (Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out var ruri))
								ProcessDefaultLink(paneManager, new PpsWebViewLink(ruri));
							break;
					}
				}
			}
		} // proc LinkCommandExecuted

		private static void CanLinkCommandExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = e.Parameter is string || e.Parameter is Uri || e.Parameter is PpsWebViewLink;
			e.Handled = true;
		} // event CanLinkCommandExecute

		private static void DefaultNavigationStarted(object sender, PpsWebViewNavigationStartedEventArgs e)
		{
			if (e.Uri is Uri uri
				&& e.OriginalSource is DependencyObject d
				&& uri.IsAbsoluteUri && uri.Scheme == "panemanager")
			{
				ProcessPaneManager(d.GetControlService<IPpsWindowPaneManager>(true), new PpsWebViewLink(uri) { NewWindow = e.NewWindow });
				e.Cancel = true;
			}
		} // event DefaultNavigationStarted

		#endregion

		static App()
		{
			// intercept schemes for pane manager
			EventManager.RegisterClassHandler(typeof(PpsWebView), PpsWebView.NavigationStartedEvent, new PpsWebViewNavigationStartedHandler(DefaultNavigationStarted), false);
			// unprocessed links
			CommandManager.RegisterClassCommandBinding(typeof(Window), new CommandBinding(PpsWebView.LinkCommand, LinkCommandExecuted, CanLinkCommandExecute));
		} // sctor
	} // class App
}
