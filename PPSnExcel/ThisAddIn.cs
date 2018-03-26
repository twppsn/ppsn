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
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Office.Tools.Excel;
using Neo.IronLua;
using TecWare.DE.Data;
using TecWare.PPSn;
using Excel = Microsoft.Office.Interop.Excel;

namespace PPSnExcel
{
	public partial class ThisAddIn : IWin32Window, IPpsShell
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

		private void ThisAddIn_Startup(object sender, EventArgs e)
		{
			// create wait form
			this.waitForm = new WaitForm(Application);

			//Globals.Ribbons.PpsMenu.
		} // ctor

		private void ThisAddIn_Shutdown(object sender, EventArgs e)
		{
		} // event ThisAddIn_Shutdown

		#region -- ShowMessage, ShowException -----------------------------------------

		IEnumerable<IDataRow> IPpsShell.GetViewData(PpsShellGetList arguments)
			=> throw new NotSupportedException();

		public void ShowException(ExceptionShowFlags flags, Exception exception, string alternativeMessage = null)
		{
			if (exception is ExcelException ee)
				MessageBox.Show(this, alternativeMessage ?? exception.Message, "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
			else
				MessageBox.Show(this, alternativeMessage ?? exception.ToString());
		} // proc ShowException

		public void ShowMessage(string message)
			=> MessageBox.Show(this, message, "PPSnExcel", MessageBoxButtons.OK, MessageBoxIcon.Information);

		public Task ShowExceptionAsync(ExceptionShowFlags flags, Exception exception, string alternativeMessage = null)
			=> InvokeAsync(() => ShowException(flags, exception, alternativeMessage));

		public Task ShowMessageAsync(string message)
			=> InvokeAsync(() => ShowMessage(message));

		#endregion

		#region -- Threading helper ---------------------------------------------------

		public void Run(Func<Task> t)
			=> waitForm.Run(t);

		public T Run<T>(Func<Task<T>> t)
			=> waitForm.Run(t).Result;

		public void Await(Task task)
			=> waitForm.Await(task);

		public T Await<T>(Task<T> task)
		{
			Await((Task)task);
			return task.Result;
		} // func Await

		public void Invoke(System.Action action)
		{
			if (waitForm.InvokeRequired)
				waitForm.Invoke(action);
			else
				action();
		} // proc Invoke

		public Task InvokeAsync(System.Action action)
		{
			if (waitForm.InvokeRequired)
			{
				return Task.Factory.FromAsync(
					waitForm.BeginInvoke(action),
					waitForm.EndInvoke
				);
			}
			else
			{
				action();
				return Task.CompletedTask;
			}
		} // proc Invoke

		public T Invoke<T>(Func<T> func)
		{
			if (waitForm.InvokeRequired)
				return (T)waitForm.Invoke(func);
			else
				return func();
		} // proc Invoke

		public Task<T> InvokeAsync<T>(Func<T> func)
		{
			if (waitForm.InvokeRequired)
			{
				return Task.Factory.FromAsync(
					waitForm.BeginInvoke(func),
					ar => (T)waitForm.EndInvoke(ar)
				);
			}
			else
				return Task.FromResult(func());
		} // proc Invoke

		public void BeginInvoke(System.Action action)
		{
			if (waitForm.InvokeRequired)
				waitForm.BeginInvoke(action);
			else
				action();
		} // proc BeginInvoke

		public IProgressBar CreateAwaitProgress()
			=> waitForm.CreateProgress();

		#endregion

		#region -- Environment Handling -----------------------------------------------

		private PpsEnvironment FindOrCreateEnvironment(PpsEnvironmentInfo info)
		{
			var env = GetEnvironmentFromInfo(info);
			if (env == null)
			{
				env = new PpsEnvironment(this, info);
				openEnvironments.Add(env);
			}
			return env;
		} // func FindOrCreateEnvironment

		public PpsEnvironment AuthentificateEnvironment(PpsEnvironment environment)
		{
			if (environment != null
				&& (environment.IsAuthentificated // is authentificated
				|| Await(environment.LoginAsync(this)))) // try to authentificate the environment
				return environment;
			else
				return null;
		} // func AuthentificateEnvironment

		public PpsEnvironment FindEnvironment(string name, Uri uri)
			=> Invoke(() =>
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
			});

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
		/// <param name="map"></param>
		internal async Task ImportTableAsync(Excel.Range range, PpsListMapping map, string reportName)
		{
			GetActiveXlObjects(out var worksheet, out var workbook);

			// prepare target
			if (range == null)
				range = Globals.ThisAddIn.Application.Selection;
			if (range is null)
				throw new ExcelException("Keine Tabellen-Ziel (Range) definiert.");
			if (range.ListObject != null)
				throw new ExcelException("Tabelle darf nicht innerhalb einer anderen Tabelle eingefügt werden.");

			using (var progress = CreateAwaitProgress())
			{
				progress.Report(String.Format("Importiere '{0}'...", reportName));

				await PpsListObject.CreateAsync(range, map);
			}
		} // func ImportTableAsync

		internal Task RefreshTableAsync(RefreshContext context = RefreshContext.ActiveWorkBook)
		{
			using (var progress = CreateAwaitProgress())
			{
				progress.Report("Aktualisiere Tabellen...");

				switch (context)
				{
					case RefreshContext.ActiveWorkBook:
						return RefreshTableAsync(Globals.ThisAddIn.Application.ActiveWorkbook);
					case RefreshContext.ActiveWorkSheet:
						return RefreshTableAsync(Globals.ThisAddIn.Application.ActiveSheet);
					default:
						if (Globals.ThisAddIn.Application.Selection is Excel.Range r && !(r.ListObject is null))
							return RefreshTableAsync(r.ListObject, context == RefreshContext.ActiveListObjectLayout);
						return Task.CompletedTask;
				}
			}
		} // func RefreshTableAsync

		private async Task RefreshTableAsync(Excel.Workbook workbook)
		{
			for (var i = 1; i <= workbook.Sheets.Count; i++)
			{
				if (workbook.Sheets[i] is Excel.Worksheet worksheet)
					await RefreshTableAsync(worksheet);
			}
		} // func RefreshTableAsync

		private async Task RefreshTableAsync(Excel.Worksheet worksheet)
		{
			for (var i = 1; i <= worksheet.ListObjects.Count; i++)
				await RefreshTableAsync(worksheet.ListObjects[i], false);
		} // func RefreshTableAsync

		private async Task RefreshTableAsync(Excel.ListObject _xlList, bool refreshColumnLayout)
		{
			using (var progress = CreateAwaitProgress())
			{
				var xlList = Globals.Factory.GetVstoObject(_xlList);

				progress.Report(String.Format("Aktualisiere {0}...", xlList.Name ?? "Tabelle"));

				if (PpsListObject.TryGet(FindEnvironment, xlList, out var ppsList))
					await ppsList.RefreshAsync(refreshColumnLayout);
				else
				{
					//if (refreshColumnLayout)
					//	;

					xlList.Refresh();
				}
			}
		} // func RefreshTableAsync

		internal void ShowTableInfo()
		{
			GetActiveXlObjects(out var worksheet, out var workbook);

			if (Globals.ThisAddIn.Application.Selection is Excel.Range range && range.ListObject != null)
			{
				ShowMessage($"{range.ListObject.XmlMap.Schemas[1].XML} --- {range.ListObject.SourceType}");
			}

			var sb = new StringBuilder();

			for (var i = 1; i <= workbook.XmlMaps.Count; i++)
			{
				var map = workbook.XmlMaps[i];
				sb.AppendLine($"{i}: name={map.Name}, root={map.RootElementName}, schemas={map.Schemas.Count}");
				for (var j = 1; j <= map.Schemas.Count; j++)
				{
					var schema = map.Schemas[j];
					sb.AppendLine($"  {j}: name={schema.Name}, uri={schema.Namespace.Uri.ToString()}");
				}
			}

			ShowMessage(sb.ToString());
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

		/// <summary>Current active, authentificated environment.</summary>
		public PpsEnvironment CurrentEnvironment => currentEnvironment;
		/// <summary>Lua environment.</summary>
		public Lua Lua => lua;
		/// <summary>Base encoding.</summary>
		public Encoding Encoding => Encoding.Default;
		/// <summary></summary>
		public SynchronizationContext Context => waitForm.SynchronizationContext;

		IntPtr IWin32Window.Handle => new IntPtr(Application.Hwnd);

		LuaTable IPpsShell.LuaLibrary => throw new NotSupportedException();
		Uri IPpsShell.BaseUri => throw new NotSupportedException();

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
