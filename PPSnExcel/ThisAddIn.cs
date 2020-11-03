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
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Office.Tools.Excel;
using Microsoft.Win32;
using Neo.IronLua;
using TecWare.DE.Networking;
using TecWare.DE.Stuff;
using TecWare.PPSn;
using Excel = Microsoft.Office.Interop.Excel;

namespace PPSnExcel
{
	public partial class ThisAddIn : IWin32Window
	{
		#region -- enum RefreshContext ------------------------------------------------

		internal enum RefreshContext
		{
			ActiveWorkBook,
			ActiveWorkSheet,
			ActiveListObject,
			ActiveListObjectLayout
		} // enum RefreshContext

		#endregion

		private readonly List<IPpsShell> activatedShells = new List<IPpsShell>();

		private WaitForm uiService;

		private IPpsnFunctions modul = null;

		private void ThisAddIn_Startup(object sender, EventArgs e)
		{
			// create ui-service
			uiService = new WaitForm(Application);
			PpsShell.Global.AddService(typeof(IPpsUIService), uiService);
			PpsShell.Global.AddService(typeof(IPpsAsyncService), uiService);
			PpsShell.Global.AddService(typeof(IPpsProgressFactory), uiService);
			PpsShell.Global.AddService(typeof(IWin32Window), this);

			//Globals.Ribbons.PpsMenu.
		} // ctor

		private void ThisAddIn_Shutdown(object sender, EventArgs e)
		{
		} // event ThisAddIn_Shutdown

		protected override object RequestComAddInAutomationService()
		{
			if (modul == null)
				modul = new PpsnFunctions();
			return modul;
		} // func RequestComAddInAutomationService

		#region -- Shell Handling -----------------------------------------------------

		//		#region -- enum PpsLoginResult ------------------------------------------------

		//		public enum PpsLoginResult
		//		{
		//			Canceled,
		//			Sucess,
		//			Restart
		//		} // enum PpsLoginResult

		//		#endregion

		//		#region -- Login --------------------------------------------------------------

		//		private static Version GetExcelAssemblyVersion()
		//		{
		//			var fileVersion = typeof(ThisAddIn).Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>();
		//			return fileVersion == null ? new Version(1, 0, 0, 0) : new Version(fileVersion.Version);
		//		} // func GetExcelAssemblyVersion

		//		private static Version GetExcelAddinVersion()
		//		{
		//			using (var reg = Registry.CurrentUser.OpenSubKey(@"Software\TecWare\PPSnExcel\Components", false))
		//			{
		//				return reg?.GetValue(null) is string v
		//					? new Version(v)
		//					: new Version(1, 0, 0, 0);
		//			}
		//		} // func GetExcelAddinVersion

		//		private async Task<PpsLoginResult> LoginAsync(IPpsShell shell, IPpsShellInfo info)
		//		{


		//			shell.LoginAsync();


		////			var newRequest = DEHttpClient.Create(info.Uri, null, Encoding.UTF8);
		////			try
		////			{
		////				// get app-info
		////				var xInfo = await newRequest.GetXmlAsync("info.xml?app=PPSnExcel");

		////				// check version
		////				var clientVersion = GetExcelAddinVersion();
		////				var assemblyVesion = GetExcelAssemblyVersion();
		////				var serverVersion = new Version(xInfo.GetAttribute("version", "1.0.0.0"));

		////#if DEBUG
		////				assemblyVesion = serverVersion = clientVersion;
		////#endif

		////				if (clientVersion < serverVersion) // new version is provided
		////				{
		////					return await UpdateApplicationAsync(newRequest, xInfo.GetAttribute("src", null))
		////						? PpsLoginResult.Restart
		////						: PpsLoginResult.Canceled;
		////				}
		////				else if (assemblyVesion < clientVersion) // new version is installed, but not active
		////				{
		////					return AskForRestart()
		////						? PpsLoginResult.Restart
		////						: PpsLoginResult.Canceled;
		////				}

		////				// update mime type mappings
		////				PpsShellExtensions.UpdateMimeTypesFromInfo(xInfo);

		////				// open trust stroe
		////				using (var login = new PpsClientLogin("ppsn_env:" + info.Uri.ToString(), info.Name, false))
		////				{
		////					var loginCounter = 0; // first time, do not show login dialog
		////					while (true)
		////					{
		////						// show login dialog
		////						if (loginCounter > 0)
		////						{
		////							if (!await InvokeAsync(() => login.ShowWindowsLogin(parent.Handle)))
		////								return PpsLoginResult.Canceled;
		////						}

		////						// create new client request
		////						loginCounter++;
		////						newRequest?.Dispose();
		////						newRequest = DEHttpClient.Create(info.Uri, login.GetCredentials(true), Encoding);

		////						// try login with user
		////						try
		////						{
		////							// execute login
		////							var xLogin = await newRequest.GetXmlAsync("login.xml");
		////							request = newRequest;
		////							IsAuthentificatedChanged?.Invoke(this, EventArgs.Empty);
		////							fullName = xLogin.GetAttribute("FullName", (string)null);

		////							login.Commit();
		////							return PpsLoginResult.Sucess;
		////						}
		////						catch (HttpResponseException e)
		////						{
		////							switch (e.StatusCode)
		////							{
		////								case HttpStatusCode.Unauthorized:
		////									if (loginCounter > 1)
		////										ShowMessage("Passwort oder Nutzername falsch.");
		////									break;
		////								default:
		////									formsApplication.ShowMessage(String.Format("Verbindung mit fehlgeschlagen.\n{0} ({2} {1})", e.Message, e.StatusCode, (int)e.StatusCode), MessageBoxIcon.Error);
		////									return PpsLoginResult.Canceled;
		////							}
		////						}
		////					}
		////				}
		////			}
		////			catch (HttpRequestException e)
		////			{
		////				newRequest?.Dispose();
		////				if (e.InnerException is WebException we)
		////				{
		////					switch (we.Status)
		////					{
		////						case WebExceptionStatus.Timeout:
		////						case WebExceptionStatus.ConnectFailure:
		////							ShowMessage("Server nicht erreichbar.");
		////							return PpsLoginResult.Canceled;
		////					}
		////				}
		////				else
		////					ShowException(ExceptionShowFlags.None, e, "Verbindung fehlgeschlagen.");
		////				return PpsLoginResult.Canceled;
		////			}
		////			catch
		////			{
		////				newRequest?.Dispose();
		////				throw;
		////			}
		//		} // func LoginAsync

		//	#endregion

		//private IPpsShell FindOrCreateShell(IPpsShellInfo info)
		//{
		//	if (info == null)
		//		throw new ArgumentNullException(nameof(info));

		//	var env = GetShellFromInfo(info);
		//	if (env == null)
		//	{
		//		env = PpsShell.StartAsync(info).Await();
		//		activatedShells.Add(env);
		//	}
		//	return env;
		//} // func FindOrCreateShell

		//public IPpsShell AuthentificateShell(IPpsShell shell)
		//{
		//	if (shell == null)
		//		return null;

		//	if (shell.IsAuthentificated) // is authentificated
		//		return shell;
		//	else
		//	{
		//		switch (LoginUser(shell))// try to authentificate the environment
		//		{
		//			case PpsLoginResult.Sucess:
		//				return shell;
		//			case PpsLoginResult.Restart:
		//				Application.Quit();
		//				return null;
		//			default:
		//				return null;
		//		}
		//	}
		//} // func AuthentificateShell

		//private IPpsShell FindShellIntern(string name, Uri uri)
		//{
		//	IPpsShell shellByName = null;
		//	IPpsShellInfo infoByName = null;
		//	IPpsShellInfo infoByUri = null;

		//	foreach (var oe in activatedShells)
		//	{
		//		if (oe.Info.Name == name)
		//		{
		//			shellByName = oe;
		//			break;
		//		}
		//	}

		//	if (shellByName != null)
		//		return AuthentificateShell(shellByName);

		//	var shellFactory = PpsShell.Global.GetService<IPpsShellFactory>(true);
		//	foreach (var info in shellFactory)
		//	{
		//		if (info.Name == name)
		//		{
		//			infoByName = info;
		//			break;
		//		}
		//		else if (info.Uri == uri)
		//		{
		//			infoByUri = info;
		//		}
		//	}

		//	// check for unloaded environment
		//	var shellInfo = infoByName ?? infoByUri;
		//	return AuthentificateShell(shellInfo != null ? FindOrCreateShell(shellInfo) :  CurrentShell);
		//} // func FindShellIntern

		//		private bool AskForRestart()
		//			=> formsApplication.ShowMessage(String.Format("Die Anwendung muss für eine Aktivierung neugestartet werden.\nNeustart von {0} sofort einleiten?", formsApplication.Title), MessageBoxIcon.Question, MessageBoxButtons.YesNo) == DialogResult.Yes;

		//		private async Task<bool> UpdateApplicationAsync(DEHttpClient client, string src)
		//		{
		//			if (formsApplication.ShowMessage(String.Format("Eine neue Anwendung steht bereit.\nInstallieren und {0} neustarten?", formsApplication.Title), MessageBoxIcon.Question, MessageBoxButtons.YesNo) != DialogResult.Yes)
		//				return false;

		//			var sourceUri = client.CreateFullUri(src);
		//			var msiExe = Path.Combine(Environment.SystemDirectory, "msiexec.exe");
		//			var psi = new ProcessStartInfo(msiExe, "/i " + sourceUri.ToString() + " /q");
		//			using (var bar = CreateProgress(true))
		//			using (var ps = Process.Start(psi))
		//			{
		//				bar.Text = "Installation wird ausgeführt...";
		//				await Task.Run(new Action(ps.WaitForExit));
		//				// ps.ExitCode
		//			}

		//			return true;
		//		} // proc UpdateApplicationAsync

		//		#endregion

		//		/// <summary>Name of the environment.</summary>
		//		public string Name => info.Name;
		//		/// <summary>Authentificated user.</summary>
		//		public string UserName => Credentials is null ? null : fullName ?? PpsEnvironmentInfo.GetUserNameFromCredentials(request.Credentials);

		private IPpsShell FindOrCreateShell(IPpsShellInfo info, bool isDefault)
		{
			// todo: applicationId needs to be changable
			//       xml needs FileSystemWatcher for changes and strong locking for read and write
			//       exchange for notifications?

			// PpsShell.StartAsync(info, isDefault)
			throw new NotImplementedException();
		} // proc FindOrCreateShell

		public IPpsShell FindShell(string name, Uri uri)
		{
			throw new NotImplementedException();

			//(IPpsShell)waitForm.Invoke(new Func<IPpsShell>(() => FindShellIntern(name, uri)));
		} // func FindShell

		public IPpsShell GetShellFromInfo(IPpsShellInfo info)
			=> activatedShells.Find(c => c.Info == info);

		public void ActivateEnvironment(IPpsShellInfo info)
		{
			if (info is null)
				return;

			FindOrCreateShell(info, true);
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

		internal Task RefreshTableAsync(RefreshContext context = RefreshContext.ActiveWorkBook, IPpsShell replaceShell = null)
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
					default:
						if (Globals.ThisAddIn.Application.Selection is Excel.Range r && !(r.ListObject is null))
							return RefreshTableAsync(r.ListObject, replaceShell, context == RefreshContext.ActiveListObjectLayout);
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
				await RefreshTableAsync(worksheet.ListObjects[i], replaceShell, false);

			if (pivotCachesWorkbook != null)
				RefreshPivotCaches(pivotCachesWorkbook);
		} // func RefreshTableAsync

		internal async Task RefreshTableAsync(Excel.ListObject _xlList, IPpsShell replaceShell, bool refreshLayout)
		{
			using (var progress = PpsShell.Global.CreateProgress())
			{
				var xlList = Globals.Factory.GetVstoObject(_xlList);

				progress.Report(String.Format("Aktualisiere {0}...", xlList.Name ?? "Tabelle"));

				var f = replaceShell == null
					? new Func<string, Uri, IPpsShell>(FindShell)
					: new Func<string, Uri, IPpsShell>((n, u) => replaceShell);

				if (PpsListObject.TryGet(f, xlList, out var ppsList))
					await ppsList.RefreshAsync(refreshLayout ? PpsXlRefreshList.Style : PpsXlRefreshList.None, PpsMenu.IsSingleLineModeToggle(), null);
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
