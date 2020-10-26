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
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Office.Tools.Excel;
using Neo.IronLua;
using TecWare.DE.Networking;
using TecWare.PPSn;
using TecWare.PPSn.Data;
using Excel = Microsoft.Office.Interop.Excel;

namespace PPSnExcel
{
	public partial class ThisAddIn : IPpsFormsApplication
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

		/// <summary>Current environment has changed.</summary>
		public event EventHandler CurrentEnvironmentChanged;

		private readonly List<PpsEnvironment> openEnvironments = new List<PpsEnvironment>();
		private readonly Lua lua = new Lua();
		private PpsEnvironment currentEnvironment = null;

		private WaitForm waitForm;
		private int mainThreadId = 0;

		private IPpsnFunctions modul = null;

		private void ThisAddIn_Startup(object sender, EventArgs e)
		{
			// create wait form
			waitForm = new WaitForm(Application);
			mainThreadId = Thread.CurrentThread.ManagedThreadId;
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

		#region -- Environment Handling -----------------------------------------------

		private PpsEnvironment FindOrCreateEnvironment(PpsEnvironmentInfo info)
		{
			var env = GetEnvironmentFromInfo(info);
			if (env == null)
			{
				env = new PpsEnvironment(lua, this, info);
				openEnvironments.Add(env);
			}
			return env;
		} // func FindOrCreateEnvironment

		public PpsEnvironment AuthentificateEnvironment(PpsEnvironment environment)
		{
			if (environment == null)
				return null;

			if (environment.IsAuthentificated) // is authentificated
				return environment;
			else
			{
				switch (environment.Await(environment.LoginAsync(this)))// try to authentificate the environment
				{
					case PpsLoginResult.Sucess:
						return environment;
					case PpsLoginResult.Restart:
						Application.Quit();
						return null;
					default:
						return null;
				}
			}
		} // func AuthentificateEnvironment

		public PpsEnvironment FindEnvironment(string name, Uri uri)
		{
			return (PpsEnvironment)waitForm.Invoke(new Func<PpsEnvironment>(() =>
		   {
			   PpsEnvironment envByName = null;
			   PpsEnvironmentInfo infoByName = null;
			   PpsEnvironmentInfo infoByUri = null;

			   foreach (var oe in openEnvironments)
			   {
				   if (oe.Info.Name == name)
				   {
					   envByName = oe;
					   break;
				   }
			   }

			   if (envByName != null)
				   return AuthentificateEnvironment(envByName);

			   foreach (var info in PpsEnvironmentInfo.GetLocalEnvironments())
			   {
				   if (info.Name == name)
				   {
					   infoByName = info;
					   break;
				   }
				   else if (info.Uri == uri)
				   {
					   infoByUri = info;
				   }
			   }

			   return AuthentificateEnvironment(FindOrCreateEnvironment(infoByName ?? infoByUri)) ?? CurrentEnvironment;
		   }));
		} // func FindEnvironment

		public void ActivateEnvironment(PpsEnvironmentInfo info)
		{
			if (info is null)
				return;

			var env = FindOrCreateEnvironment(info);
			if (AuthentificateEnvironment(env) != null)
				SetCurrentEnvironment(env);
		} // proc ActivateEnvironment

		public void DeactivateEnvironment(PpsEnvironment environment = null)
		{
			environment = environment ?? currentEnvironment;
			if (environment != null)
			{
				environment.ClearCredentials();

				if (currentEnvironment == environment)
					SetCurrentEnvironment(null);
			}
		} // proc DeactivateCurrentEnvironment

		private void SetCurrentEnvironment(PpsEnvironment env)
		{
			currentEnvironment = env;
			CurrentEnvironmentChanged?.Invoke(this, EventArgs.Empty);
		} // proc SetCurrentEnvironment

		public PpsEnvironment GetEnvironmentFromInfo(PpsEnvironmentInfo info)
			=> openEnvironments.Find(c => c.Info == info);

		#endregion

		#region -- ImportTable --------------------------------------------------------

		private static void GetActiveXlObjects(out Worksheet worksheet, out Workbook workbook)
		{
			// get active workbook and worksheet
			worksheet = Globals.Factory.GetVstoObject((Excel._Worksheet)Globals.ThisAddIn.Application.ActiveSheet);
			workbook = Globals.Factory.GetVstoObject((Excel._Workbook)worksheet.Parent);
		} // func GetActiveXlObjects

		/// <summary>Create Table</summary>
		/// <param name="environment"></param>
		/// <param name="range"></param>
		/// <param name="reportId"></param>
		internal void NewTable(PpsEnvironment environment, Excel.Range range, string reportId)
		{
			// prepare target
			if (range == null)
				range = Globals.ThisAddIn.Application.Selection;
			if (range is null)
				throw new ExcelException("Keine Tabellen-Ziel (Range) definiert.");
			if (range.ListObject != null)
				throw new ExcelException("Tabelle darf nicht innerhalb einer anderen Tabelle eingefügt werden.");
			
			PpsListObject.New(environment, range, reportId);
		} // func NewTable

		internal Task RefreshTableAsync(RefreshContext context = RefreshContext.ActiveWorkBook, PpsEnvironment replaceEnvironment = null)
		{
			using (var progress = CreateProgress())
			{
				progress.Report("Aktualisiere Tabellen...");

				// refresh list elements
				switch (context)
				{
					case RefreshContext.ActiveWorkBook:
						return RefreshTableAsync(Globals.ThisAddIn.Application.ActiveWorkbook, replaceEnvironment);
					case RefreshContext.ActiveWorkSheet:
						return RefreshTableAsync(Globals.ThisAddIn.Application.ActiveSheet, replaceEnvironment, Globals.ThisAddIn.Application.ActiveWorkbook);
					default:
						if (Globals.ThisAddIn.Application.Selection is Excel.Range r && !(r.ListObject is null))
							return RefreshTableAsync(r.ListObject, replaceEnvironment, context == RefreshContext.ActiveListObjectLayout);
						return Task.CompletedTask;
				}

			}
		} // func RefreshTableAsync

		private async Task RefreshTableAsync(Excel.Workbook workbook, PpsEnvironment replaceEnvironment)
		{
			for (var i = 1; i <= workbook.Sheets.Count; i++)
			{
				if (workbook.Sheets[i] is Excel.Worksheet worksheet)
					await RefreshTableAsync(worksheet, replaceEnvironment, null);
			}

			RefreshPivotCaches(workbook);
		} // func RefreshTableAsync

		private async Task RefreshTableAsync(Excel.Worksheet worksheet, PpsEnvironment replaceEnvironment, Excel.Workbook pivotCachesWorkbook)
		{
			for (var i = 1; i <= worksheet.ListObjects.Count; i++)
				await RefreshTableAsync(worksheet.ListObjects[i], replaceEnvironment, false);

			if (pivotCachesWorkbook != null)
				RefreshPivotCaches(pivotCachesWorkbook);
		} // func RefreshTableAsync

		internal async Task RefreshTableAsync(Excel.ListObject _xlList, PpsEnvironment replaceEnvironment, bool refreshLayout)
		{
			using (var progress = CreateProgress())
			{
				var xlList = Globals.Factory.GetVstoObject(_xlList);

				progress.Report(String.Format("Aktualisiere {0}...", xlList.Name ?? "Tabelle"));

				var f = replaceEnvironment == null
					? new Func<string, Uri, PpsEnvironment>(FindEnvironment)
					: new Func<string, Uri, PpsEnvironment>((n, u) => replaceEnvironment);

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

		internal void LoadXlsxReport(PpsEnvironment environment, string reportId, string reportName)
		{
			var http = environment.Request;

			using (var p = CreateProgress())
			{
				p.Text = String.Format("Lade Auswertung {0}...", reportName);
				var documentTemplate = Run(() => DownloadXlsxReportAsync(http, reportId));

				// open and activate workbook
				p.Text = String.Format("Öffne Auswertung {0}...", reportName);
				var wkb = Globals.ThisAddIn.Application.Workbooks.Add(documentTemplate);
				wkb.Activate();

				// refresh tables
				p.Text = String.Format("Aktualisiere Auswertung {0}...", reportName);
				Run(() => RefreshTableAsync(RefreshContext.ActiveWorkBook, environment));
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

		#region -- IPpsFormsApplication -----------------------------------------------

		public void Await(Task t)
			=> waitForm.Await(t);

		public IAsyncResult BeginInvoke(Delegate method, object[] args)
			=> waitForm.BeginInvoke(method, args);

		public object EndInvoke(IAsyncResult result)
			=> waitForm.EndInvoke(result);

		public object Invoke(Delegate method, object[] args)
			=> waitForm.Invoke(method, args);

		public IPpsProgress CreateProgress(bool blockUI = true)
			=> waitForm.CreateProgress();

		public SynchronizationContext SynchronizationContext
			=> waitForm.SynchronizationContext;

		public static void CheckSynchronizationContext()
		{
			if (!(SynchronizationContext.Current is WindowsFormsSynchronizationContext))
				SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
		} // proc CheckSynchronizationContext

		private T RunCore<T>(Func<T> func)
			where T : Task
		{
			CheckSynchronizationContext();

			var t = func();
			Await(t);
			t.Wait();
			return t;
		} // func RunCore

		public Task Run(Func<Task> func)
			=> RunCore(func);

		public T Run<T>(Func<Task<T>> t)
			=> RunCore(t).Result;

		public bool InvokeRequired => waitForm.InvokeRequired;

		#endregion

		string IPpsFormsApplication.ApplicationId => "PPSnExcel";
		string IPpsFormsApplication.Title => "Excel";

		/// <summary>Current active, authentificated environment.</summary>
		public PpsEnvironment CurrentEnvironment => currentEnvironment;
		/// <summary>Lua environment.</summary>
		public Lua Lua => lua;

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
