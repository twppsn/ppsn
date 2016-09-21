using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Interop;
using System.Xml.Linq;
using Microsoft.Office.Tools.Ribbon;
using TecWare.PPSn;
using Excel = Microsoft.Office.Interop.Excel;

namespace PPSnExcel
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public partial class PpsMenu
	{
		private Excel.Application application;

		private PpsEnvironment environment = null;
		private bool isEnvironmentInitialized = false;
		private bool isMenuLoaded = false;

		private EventHandler usernameChanged;

		#region -- Load, Init, Done -------------------------------------------------------

		private void PpsMenu_Load(object sender, RibbonUIEventArgs e)
		{
			application = Globals.ThisAddIn.Application;
			cmdExtended.Visible = Globals.ThisAddIn.Application.ShowDevTools;

			// init events
			usernameChanged = (s1, e1) => App.Current.Dispatcher.BeginInvoke(new Action(RefreshUsername));

			// connection to the excel application
			application.WorkbookActivate += wb => WorkbookStateChanged(wb, true);
			application.WorkbookDeactivate += wb => WorkbookStateChanged(wb, false);

			application.SheetSelectionChange += (sh, target) => Refresh();

			// initialize environment
			if (!isEnvironmentInitialized)
				InitEnvironment();

			RefreshUsername();
			RefreshEnvironments();
			Refresh();

			isMenuLoaded = true;
		} // event PpsMenu_Load

		private void InitEnvironment()
		{
			if (environment == null || !isMenuLoaded)
				return;

			environment.UsernameChanged += usernameChanged;

			RefreshUsername();

			isEnvironmentInitialized = true;
		} // proc InitMenu

		private void DoneEnvironment(PpsEnvironment oldEnvironment)
		{
			environment.UsernameChanged -= usernameChanged;
		} // proc DoneEnvironment

		#endregion

		public void Refresh()
		{
		} // proc Refresh

		private void RefreshUsername()
		{
			loginMenu.Label = environment == null ? "Keine Umgebung" : environment.UsernameDisplay;
			loginGalery.Label = environment?.Info?.DisplayName ?? "Keine Umgebung";
			loginButton.Enabled = environment != null;
			loginButton.Label = environment != null && environment.IsAuthentificated ? "Abmelden" : "Anmelden";
		} // proc RefreshUsername

		private void RefreshEnvironments()
		{
			// remove all instances
			loginGalery.Items.Clear();

			// readd them
			foreach (var cur in PpsEnvironmentInfo.GetLocalEnvironments().OrderBy(c => c.DisplayName))
			{
				var ribbonButton = Factory.CreateRibbonDropDownItem();
				ribbonButton.Label = cur.DisplayName;
				ribbonButton.ScreenTip = String.Format("{0} ({1})", cur.DisplayName, cur.Name);
				ribbonButton.SuperTip = String.Format("Version {0}\nUri: {1}", cur.Version, cur.Uri.ToString());
				ribbonButton.Tag = cur;
				loginGalery.Items.Add(ribbonButton);
			}
		} // proc RefreshEnvironments

		private void WorkbookStateChanged(Excel._Workbook wb, bool activate)
		{
			cmdTable.Enabled = activate;
		} // proc WorkbookStateChanged

		//private void cmdDataImport_Click(object sender, RibbonControlEventArgs e)
		//{
		//	var w = new Wpf.PpsReportSelectWindow();
		//	w.ShowDialog();

		//	//var t = new Thread(() =>
		//	//{
		//	//	var w = new Wpf.PpsExcelDataWindow();
		//	//	w.Show();
		//	//	w.Closed += (s1, e2) => w.Dispatcher.InvokeShutdown();
		//	//	System.Windows.Threading.Dispatcher.Run();
		//	//});

		//	//t.SetApartmentState(ApartmentState.STA);
		//	//t.IsBackground = true;
		//	//t.Start();
		//} // event cmdDataImport_Click

		public PpsEnvironment Environment
		{
			get { return environment; }
			set
			{
				environment = value;
				if (environment != null)
				{
					isEnvironmentInitialized = false;
					if (isMenuLoaded)
						InitEnvironment();
				}
				else if (isMenuLoaded)
					Refresh();
			}
		} // prop Environment

		private void cmdReport_Click(object sender, RibbonControlEventArgs e)
		{

			using (var en = Environment.GetViewData(new PpsShellGetList("wpf.reports")).GetEnumerator())
			{
				while (en.MoveNext())
					Debug.Print("REPORT: {0}, {1}, {2}", en.Current["Type"], en.Current["ReportId"], en.Current["DisplayName"]);
			}

			using (var en = Environment.GetViewData(new PpsShellGetList("sds.ansp") { Count = 10 }).GetEnumerator())
			{
				while (en.MoveNext())
					Debug.Print("ANSP: {0}, {1}, {2}", en.Current["Name"], en.Current["Tel"], en.Current["Fax"]);
			}

			var w = new Wpf.PpsReportSelectWindow();
			var wh = new WindowInteropHelper(w);
			wh.Owner = new IntPtr(application.Hwnd);
			w.ShowDialog();
		}

		private void cmdTable_Click(object sender, RibbonControlEventArgs e)
		{
			var w = new Wpf.PpsTableImportWindow();
			var wh = new WindowInteropHelper(w);
			wh.Owner = new IntPtr(application.Hwnd);
			if (w.ShowDialog() ?? false)
				Globals.ThisAddIn.ImportTable(w.TableName, w.TableSourceId);
		} // event cmdTable_Click

		private void cmdStyles_Click(object sender, RibbonControlEventArgs e)
		{
		}

		private void cmdRefresh_Click(object sender, RibbonControlEventArgs e)
		{
			var xDoc = XDocument.Load(@"C:\Projects\PPSnOS\twppsn\PPSnWpf\PPSnDesktop\Local\Data\contacts.xml");
			xDoc.Root.Element("columns").Remove();

			var map = Globals.ThisAddIn.Application.ActiveWorkbook.XmlMaps.Item[1];
			map.ImportXml(xDoc.ToString(SaveOptions.None), true);
		}

		private void loginGalery_ItemsLoading(object sender, RibbonControlEventArgs e)
		{
			RefreshEnvironments();
		}

		private void loginGalery_Click(object sender, RibbonControlEventArgs e)
		{
			if (loginGalery.SelectedItem == null)
				return;
			Globals.ThisAddIn.LoginEnvironment(loginGalery.SelectedItem.Tag as PpsEnvironmentInfo);
		} // event loginGalery_Click

		private void loginButton_Click(object sender, RibbonControlEventArgs e)
		{
			if (environment == null)
				return;

			if (environment.IsAuthentificated)
				Globals.ThisAddIn.RunUISynchron(environment.LogoutUserAsync());
			else
				Globals.ThisAddIn.RunUISynchron(environment.LoginUserAsync());
		} // event loginButton_Click
	} // class PpsMenu
}
