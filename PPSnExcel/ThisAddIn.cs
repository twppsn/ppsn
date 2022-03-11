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
using System.Drawing;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Office.Tools.Excel;
using Microsoft.Win32;
using TecWare.DE.Networking;
using TecWare.DE.Stuff;
using TecWare.PPSn;
using TecWare.PPSn.Data;
using Action = System.Action;
using Excel = Microsoft.Office.Interop.Excel;

/*
 * To show load errors:
 * set VSTO_SUPPRESSDISPLAYALERTS=0
 */

namespace PPSnExcel
{
	public partial class ThisAddIn : IWin32Window, IPpsShellApplication
	{
		#region -- enum RefreshContext ------------------------------------------------

		internal enum RefreshContext
		{
			ActiveWorkBook,
			ActiveWorkSheet,
			ActiveListObject,
			ActiveListObjectLayout,
			ActiveWorkBookPivotCaches
		} // enum RefreshContext

		#endregion

		#region -- class ExcelApplicationUpdateException ------------------------------

		private class ExcelApplicationUpdateException : Exception
		{
			private readonly IPpsShell shell;
			private readonly Uri uri;

			public ExcelApplicationUpdateException(IPpsShell shell, Uri uri)
			{
				this.shell = shell ?? throw new ArgumentNullException(nameof(shell));
				this.uri = uri;
			} // ctor

			public IPpsShell Shell => shell;
			public Uri Uri => uri;
		} // class ExcelApplicationUpdateException

		#endregion

		#region -- class ExcelIdleService ---------------------------------------------

		private sealed class ExcelIdleService : PpsIdleServiceBase, IDisposable
		{
			private readonly WaitForm uiService;
			private readonly System.Windows.Forms.Timer idleTimer;

			private int idleTimerStarted = 0;

			public ExcelIdleService(WaitForm uiService)
			{
				this.uiService = uiService ?? throw new ArgumentNullException(nameof(uiService));
				idleTimer = new System.Windows.Forms.Timer { Enabled = false, Interval = 100 };
				idleTimer.Tick += OnIdle;
			} // ctor

			public void Dispose()
				=> idleTimer.Dispose();

			protected override void VerifyAccess()
			{
				if (uiService.InvokeRequired)
					throw new InvalidOperationException();
			}  // proc VerifyAccess

			protected override void StartIdleCore()
			{
				idleTimer.Enabled = false;
				idleTimer.Enabled = true;
				idleTimerStarted = Environment.TickCount;
			} // proc StartIdleCore

			private void OnIdle(object sender, EventArgs e)
			{
				if (unchecked(Environment.TickCount - idleTimerStarted) > 5000)
					RestartIdle();

				DoIdle(out var stopIdle);

				if (stopIdle)
					idleTimer.Enabled = false;
			} // proc OnIdle
		} // class ExcelIdleService

		#endregion

		private readonly List<IPpsShell> activatedShells = new List<IPpsShell>();

		private WaitForm uiService;
		private ExcelIdleService idleService;

		private IPpsnFunctions modul = null;

		private void ThisAddIn_Startup(object sender, EventArgs e)
		{
			// create ui-service
			uiService = new WaitForm(Application);
			idleService = new ExcelIdleService(uiService);
			PpsShell.Global.AddService(typeof(IPpsUIService), uiService);
			PpsShell.Global.AddService(typeof(IPpsAsyncService), uiService);
			PpsShell.Global.AddService(typeof(IPpsProgressFactory), uiService);
			PpsShell.Global.AddService(typeof(IWin32Window), this);
			PpsShell.Global.AddService(typeof(IPpsShellApplication), this);
			PpsShell.Global.AddService(typeof(IPpsIdleService), idleService);
			
			//Globals.Ribbons.PpsMenu.
		} // ctor

		private void ThisAddIn_Shutdown(object sender, EventArgs e)
		{
			idleService.Dispose();
			uiService.Dispose();
		} // event ThisAddIn_Shutdown

		protected override object RequestComAddInAutomationService()
		{
			if (modul == null)
				modul = new PpsnFunctions();
			return modul;
		} // func RequestComAddInAutomationService

		#region -- Shell Application --------------------------------------------------

		private const string applicationId = "PPSnExcel";

		Task IPpsShellApplication.RequestUpdateAsync(IPpsShell shell, Uri uri)
		{
#if DEBUG
			return Task.CompletedTask;
#else
			throw new ExcelApplicationUpdateException(shell, uri);
#endif
		} // proc IPpsShellApplication.RequestUpdateAsync

		Task IPpsShellApplication.RequestRestartAsync(IPpsShell shell)
		{
#if DEBUG
			return Task.CompletedTask;
#else
			throw new ExcelApplicationUpdateException(shell, null);
#endif
		} // proc IPpsShellApplication.RequestRestartAsync

		string IPpsShellApplication.Name => applicationId;

		Version IPpsShellApplication.AssenblyVersion => PpsShell.GetDefaultAssemblyVersion(this);
		Version IPpsShellApplication.InstalledVersion
		{
			get
			{
				using (var reg = Registry.CurrentUser.OpenSubKey(@"Software\TecWare\PPSnExcel\Components", false))
				{
					return reg?.GetValue(null) is string v
						? new Version(v)
						: new Version(1, 0, 0, 0);
				}
			}
		} // prop IPpsShellApplication.InstalledVersion

		#endregion

		#region -- Shell Handling -----------------------------------------------------

		private async Task<bool> UpdateApplicationAsync(IPpsShell shell, Uri sourceUri)
		{
			if (PpsWinShell.ShowMessage(this, String.Format("Eine neue Anwendung steht bereit.\nInstallieren und {0} neustarten?", applicationId), MessageBoxIcon.Question, MessageBoxButtons.YesNo) != DialogResult.Yes)
				return false;

			var msiExe = Path.Combine(Environment.SystemDirectory, "msiexec.exe");
			var msiLog = Path.Combine(Path.GetTempPath(), applicationId + ".msi.txt");
			var psi = new ProcessStartInfo(msiExe, "/i " + sourceUri.ToString() + " /q /l*v \"" + msiLog + "\"");
			using (var bar = uiService.CreateProgress(true))
			using (var ps = Process.Start(psi))
			{
				bar.Text = "Installation wird ausgeführt...";
				await Task.Run(new Action(ps.WaitForExit));
				// ps.ExitCode
			}
			return true;
		} // proc UpdateApplicationAsync

		private static async Task<bool> LoginShellAsync(IWin32Window parent, IPpsShell shell)
		{
			// open trust stroe
			using (var login = new PpsClientLogin(shell.Info.GetCredentialTarget(), shell.Info.Name, false))
			{
				var loginCounter = 0; // first time, do not show login dialog
				while (true)
				{
					// show login dialog
					if (loginCounter > 0)
					{
						if (!login.ShowWindowsLogin(parent.Handle))
							return false;
					}

					// create new client request
					loginCounter++;

					// try login with user
					try
					{
						await shell.LoginAsync(login.GetCredentials(true));
						login.Commit();
						return true;
					}
					catch (HttpResponseException e)
					{
						switch (e.StatusCode)
						{
							case HttpStatusCode.Unauthorized:
								if (loginCounter > 1)
									PpsWinShell.ShowMessage(parent, "Passwort oder Nutzername falsch.");
								break;
							default:
								throw;
						}
					}
				}
			}
		} // proc LoginShellAsync

		private async Task<IPpsShell> CreateShellAsync(IPpsShellInfo info, bool isDefault)
		{
			if (info == null)
				throw new ArgumentNullException(nameof(info));

			IPpsShell newShell = null;
			try
			{
				// initialize a new shell
				newShell = await PpsShell.StartAsync(info, isDefault);

				// authentificate shell
				if (!await LoginShellAsync(this, newShell))
				{
					newShell.Dispose();
					return null;
				}

				return newShell;
			}
			catch (ExcelApplicationUpdateException e)
			{
				if (e.Uri != null)
					UpdateApplicationAsync(e.Shell, e.Uri).Await();

				if (PpsWinShell.ShowMessage(this, String.Format("Die Anwendung muss für eine Aktivierung neugestartet werden.\nNeustart von {0} sofort einleiten?", applicationId), MessageBoxIcon.Question, MessageBoxButtons.YesNo) == DialogResult.Yes)
					Application.Quit();
				return null;
			}
			catch (HttpResponseException e)
			{
				var showDefaultMessage = true;
				if (e.InnerException is WebException we)
				{
					switch (we.Status)
					{
						case WebExceptionStatus.Timeout:
						case WebExceptionStatus.ConnectFailure:
							PpsWinShell.ShowMessage(this, "Server nicht erreichbar.");
							showDefaultMessage = false;
							break;
					}
				}
				if (showDefaultMessage)
					PpsWinShell.ShowMessage(this, String.Format("Verbindung mit fehlgeschlagen.\n{0} ({2} {1})", e.Message, e.StatusCode, (int)e.StatusCode), MessageBoxIcon.Error);
				newShell?.Dispose();
				return null;
			}
		} // func CreateShellAsync

		private IPpsShell CreateShell(IPpsShellInfo info, bool isDefault)
		{
			// connect to shell			
			using (var bar = uiService.CreateProgress(progressText: String.Format("Verbinde mit {0}...", info.Name)))
			{
				var shell = CreateShellAsync(info, isDefault).Await();
				shell.Disposed += ShellDestroyed;
				activatedShells.Add(shell);
				Globals.Ribbons.PpsMenu.RefreshUserName();
				return shell;
			}
		} // proc CreateShell

		private void ShellDestroyed(object sender, EventArgs e)
			=> activatedShells.Remove((IPpsShell)sender);

		public IPpsShell EnforceShell(string name, Uri uri)
		{
			// first lookup the active shells
			IPpsShell shell = null;
			foreach (var cur in activatedShells)
			{
				if (cur.Info.Name == name)
					return cur;
				else if (cur.Info.Uri == uri)
					shell = cur;
			}

			// next lookup inactive shells
			var factory = PpsShell.Global.GetService<IPpsShellFactory>(true);
			IPpsShellInfo shellInfo = null;
			foreach (var cur in factory)
			{
				if (cur.Name == name)
					return CreateShell(cur, false);
				else if (cur.Uri == uri)
					shellInfo = cur;
			}

			// fallback to an uri
			if (shell != null)
				return shell;
			else if (shellInfo != null)
				return CreateShell(shellInfo, false);
			else
				return null;
		} // func EnforceShell

		public IPpsShell GetShellFromInfo(IPpsShellInfo info)
			=> activatedShells.Find(c => c.Info == info);

		public void ActivateEnvironment(IPpsShellInfo info)
		{
			if (info is null)
				return;

			// check if the shell is already active
			var shell = activatedShells.Find(s => s.Info.Equals(info));
			if (shell != null)
				return;

			// activate the shell
			CreateShell(info, true);
		} // proc ActivateEnvironment

		public void DeactivateShell(IPpsShell shell = null)
			=> (shell ?? PpsShell.Current)?.Dispose();

		#endregion

		#region -- ImportTable --------------------------------------------------------

		private static void GetActiveXlObjects(out Worksheet worksheet, out Workbook workbook)
		{
			// get active workbook and worksheet
			worksheet = Globals.Factory.GetVstoObject((Excel._Worksheet)Globals.ThisAddIn.Application.ActiveSheet);
			workbook = Globals.Factory.GetVstoObject((Excel._Workbook)worksheet.Parent);
		} // func GetActiveXlObjects

		/// <summary>Create Table</summary>
		/// <param name="shell"></param>
		/// <param name="range"></param>
		/// <param name="reportId"></param>
		internal void NewTable(IPpsShell shell, Excel.Range range, string reportId)
		{
			// prepare target
			if (range == null)
				range = Globals.ThisAddIn.Application.Selection;
			if (range is null)
				throw new ExcelException("Keine Tabellen-Ziel (Range) definiert.");
			if (range.ListObject != null)
				throw new ExcelException("Tabelle darf nicht innerhalb einer anderen Tabelle eingefügt werden.");

			PpsListObject.New(shell, range, reportId);
		} // func NewTable

		internal Task RefreshTableAsync(RefreshContext context = RefreshContext.ActiveWorkBook, IPpsShell replaceShell = null, bool anonymize = false)
		{
			using (var progress = PpsShell.Global.CreateProgress())
			{
				progress.Report("Aktualisiere Tabellen...");

				// refresh list elements
				switch (context)
				{
					case RefreshContext.ActiveWorkBook:
						return RefreshTableAsync(Globals.ThisAddIn.Application.ActiveWorkbook, replaceShell);
					case RefreshContext.ActiveWorkSheet:
						return RefreshTableAsync(Globals.ThisAddIn.Application.ActiveSheet, replaceShell, Globals.ThisAddIn.Application.ActiveWorkbook);
					case RefreshContext.ActiveWorkBookPivotCaches:
						return RefreshAllPivotCaches(Globals.ThisAddIn.Application.ActiveWorkbook);
					default:
						if (Globals.ThisAddIn.Application.Selection is Excel.Range r && !(r.ListObject is null))
							return RefreshTableAsync(r.ListObject, replaceShell, context == RefreshContext.ActiveListObjectLayout, anonymize);
						return Task.CompletedTask;
				}

			}
		} // func RefreshTableAsync

		private async Task RefreshTableAsync(Excel.Workbook workbook, IPpsShell replaceShell)
		{
			for (var i = 1; i <= workbook.Sheets.Count; i++)
			{
				if (workbook.Sheets[i] is Excel.Worksheet worksheet)
					await RefreshTableAsync(worksheet, replaceShell, null);
			}

			RefreshPivotCaches(workbook);
		} // func RefreshTableAsync

		private async Task RefreshTableAsync(Excel.Worksheet worksheet, IPpsShell replaceShell, Excel.Workbook pivotCachesWorkbook)
		{
			for (var i = 1; i <= worksheet.ListObjects.Count; i++)
				await RefreshTableAsync(worksheet.ListObjects[i], replaceShell, false, false);

			if (pivotCachesWorkbook != null)
				RefreshPivotCaches(pivotCachesWorkbook);
		} // func RefreshTableAsync

		internal async Task RefreshTableAsync(Excel.ListObject _xlList, IPpsShell replaceShell, bool refreshLayout, bool enableAnonymization)
		{
			using (var progress = PpsShell.Global.CreateProgress())
			{
				var xlList = Globals.Factory.GetVstoObject(_xlList);

				progress.Report(String.Format("Aktualisiere {0}...", xlList.Name ?? "Tabelle"));

				var f = replaceShell == null
					? new Func<string, Uri, IPpsShell>(EnforceShell)
					: new Func<string, Uri, IPpsShell>((n, u) => replaceShell);

				if (PpsListObject.TryGet(f, xlList, out var ppsList))
					await ppsList.RefreshAsync(refreshLayout ? PpsXlRefreshList.Style : PpsXlRefreshList.None, PpsMenu.IsSingleLineModeToggle(), null, enableAnonymization);
				else
				{
					switch (xlList.SourceType)
					{
						case Excel.XlListObjectSourceType.xlSrcQuery:
						case Excel.XlListObjectSourceType.xlSrcXml:
							try
							{
								xlList.Refresh();
							}
							catch (COMException)
							{ }
							break;
					}
				}
			}
		} // func RefreshTableAsync

		private async Task RefreshAllPivotCaches(Excel.Workbook workbook)
		{
			if (workbook != null)
				RefreshPivotCaches(workbook);

			await Task.CompletedTask;
		}

		private void RefreshPivotCaches(Excel.Workbook workbook)
		{
			var pivotCaches = workbook.PivotCaches();
			for (var i = 1; i <= pivotCaches.Count; i++)
			{
				try
				{
					pivotCaches[i].Refresh();
				}
				catch (COMException) { }
			}
		} // proc RefreshPivotCaches

		private static readonly DEAction xlsxTemplateAction = DEAction.Create("xlsxtmpl", "bi/", new DEActionParam("id", typeof(string)));

		private static async Task<string> DownloadXlsxReportAsync(DEHttpClient http, string reportId)
		{
			// create temp file
			var fiTemp = new FileInfo(Path.Combine(Path.GetTempPath(), "ppsn", reportId));
			if (!fiTemp.Directory.Exists)
				fiTemp.Directory.Create();
			if (fiTemp.Exists)
				fiTemp.Delete();

			// download file
			using (var src = await http.GetStreamAsync(xlsxTemplateAction.ToQuery(reportId)))
			using (var dst = fiTemp.Create())
				await src.CopyToAsync(dst);

			return fiTemp.FullName;
		} // proc DownloadXlsxReportAsync

		internal void LoadXlsxReport(IPpsShell shell, string reportId, string reportName)
		{
			var http = shell.Http;

			using (var p = PpsShell.Global.CreateProgress())
			{
				p.Text = String.Format("Lade Auswertung {0}...", reportName);
				var documentTemplate = DownloadXlsxReportAsync(http, reportId).Await(shell);

				// open and activate workbook
				p.Text = String.Format("Öffne Auswertung {0}...", reportName);
				var wkb = Globals.ThisAddIn.Application.Workbooks.Add(documentTemplate);
				wkb.Activate();

				// refresh tables
				p.Text = String.Format("Aktualisiere Auswertung {0}...", reportName);
				RefreshTableAsync(RefreshContext.ActiveWorkBook, shell).Await(shell);
			}
		} // LoadXlsxReport

		internal void ShowTableInfo()
		{
			GetActiveXlObjects(out _, out var workbook);

			var sb = new StringBuilder();

			if (Globals.ThisAddIn.Application.Selection is Excel.Range range && range.ListObject != null)
				PpsListMapping.DebugInfo(range.ListObject.XmlMap, sb);
			else // show mappings in workbook
			{
				sb.AppendLine("Mappings of the workbook:");

				for (var i = 1; i <= workbook.XmlMaps.Count; i++)
				{
					var map = workbook.XmlMaps[i];
					sb.AppendLine($"{i}: name={map.Name}, root={map.RootElementName}, schemas={map.Schemas.Count}");
					for (var j = 1; j <= map.Schemas.Count; j++)
					{
						var schema = map.Schemas[j];
						sb.AppendLine($"  {j}: name={schema.Name}, uri={schema.Namespace.Uri}");
					}
				}
			}

			this.ShowMessage(sb.ToString());
		} // func ShowTableInfo

		#endregion

		#region Von VSTO generierter Code

		/// <summary>
		/// Erforderliche Methode für die Designerunterstützung.
		/// Der Inhalt der Methode darf nicht mit dem Code-Editor geändert werden.
		/// </summary>
		private void InternalStartup()
		{
			this.Startup += new System.EventHandler(ThisAddIn_Startup);
			this.Shutdown += new System.EventHandler(ThisAddIn_Shutdown);
		}

		#endregion

		IntPtr IWin32Window.Handle => new IntPtr(Application.Hwnd);

		public Rectangle ApplicationBounds
		{
			get
			{
				if (GetWindowRect(new IntPtr(Application.Hwnd), out var rc))
					return new Rectangle(rc.Left, rc.Top, rc.Right - rc.Left, rc.Bottom - rc.Top);
				else
					return Screen.PrimaryScreen.WorkingArea;
			}
		} // func ApplicationBounds

		// -- Static --------------------------------------------------------------

		public static Worksheet GetWorksheet(Excel.Range range)
			=> Globals.Factory.GetVstoObject(range.Worksheet);

		public static Worksheet GetWorksheet(Excel.ListObject xlList)
			=> Globals.Factory.GetVstoObject((Excel._Worksheet)xlList.Parent);

		public static Workbook GetWorkbook(Worksheet worksheet)
			=> Globals.Factory.GetVstoObject((Excel._Workbook)worksheet.Parent);

		public static Workbook GetWorkbook(Excel._Worksheet worksheet)
			=> Globals.Factory.GetVstoObject((Excel._Workbook)worksheet.Parent);

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

		[StructLayout(LayoutKind.Sequential)]
		struct RECT
		{
			public int Left;
			public int Top;
			public int Right;
			public int Bottom;
		}
	} // class ThisAddIn
}
