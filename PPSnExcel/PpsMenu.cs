﻿#region -- copyright --
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
using System.Linq;
using System.Xml.Linq;
using Microsoft.Office.Tools.Ribbon;
using TecWare.PPSn;
using Excel = Microsoft.Office.Interop.Excel;

namespace PPSnExcel
{
	/// <summary></summary>
	public partial class PpsMenu
	{
		private Excel.Application application;

		//private bool isEnvironmentInitialized = false;
		private bool isMenuLoaded = false;

		#region -- Load ---------------------------------------------------------------

		private void PpsMenu_Load(object sender, RibbonUIEventArgs e)
		{
			application = Globals.ThisAddIn.Application;
			cmdExtended.Visible = Globals.ThisAddIn.Application.ShowDevTools;
						
			// connection to the excel application
			application.WorkbookActivate += wb => WorkbookStateChanged(wb, true);
			application.WorkbookDeactivate += wb => WorkbookStateChanged(wb, false);

			application.SheetSelectionChange += (sh, target) => Refresh();

			Globals.ThisAddIn.CurrentEnvironmentChanged += (s, _e) => RefreshUsername();
			
			// init environment
			RefreshEnvironments();
			RefreshUsername();

			isMenuLoaded = true;
		} // event PpsMenu_Load

		#endregion

		public void Refresh()
		{
		} // proc Refresh

		private void RefreshUsername()
		{
			var currentEnvironment = Globals.ThisAddIn.CurrentEnvironment;
			loginMenu.Label = currentEnvironment is null ? "Keine Umgebung" : $"{currentEnvironment.Name} ({currentEnvironment.UserName})";
			loginGalery.Label = currentEnvironment is null ? "Keine Umgebung" : $"{currentEnvironment.Info.Name} ({currentEnvironment.Info.DisplayName})";
			logoutButton.Enabled = !(currentEnvironment is null);
		} // proc RefreshUsername

		private void RefreshEnvironments()
		{
			// remove all instances
			loginGalery.Items.Clear();

			// readd them
			foreach (var cur in PpsEnvironmentInfo.GetLocalEnvironments().OrderBy(c => c.DisplayName))
			{
				var ribbonButton = Factory.CreateRibbonDropDownItem();
				ribbonButton.Label = cur.Name ?? cur.DisplayName;
				ribbonButton.ScreenTip = $"{ cur.Name} ({cur.DisplayName})";
				ribbonButton.SuperTip = String.Format("Version {0}\nUri: {1}", cur.Version, cur.Uri.ToString());
				ribbonButton.Tag = cur;
				var env = Globals.ThisAddIn.GetEnvironmentFromInfo(cur);
				ribbonButton.Image = env != null && env.IsAuthentificated ? Properties.Resources.EnvironmentAuthImage : Properties.Resources.EnvironmentImage;
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
		

		private void cmdReport_Click(object sender, RibbonControlEventArgs e)
		{

			//using (var en = Environment.GetViewData(new PpsShellGetList("wpf.reports")).GetEnumerator())
			//{
			//	while (en.MoveNext())
			//		Debug.Print("REPORT: {0}, {1}, {2}", en.Current["Type"], en.Current["ReportId"], en.Current["DisplayName"]);
			//}

			//using (var en = Environment.GetViewData(new PpsShellGetList("sds.ansp") { Count = 10, AttributeSelector = "*" }).GetEnumerator())
			//{
			//	var columns = (IDataColumns)en;

			//	foreach (var col in columns.Columns)
			//		Debug.Print("Column: {0} => {1} ", col.Name, col.Attributes.GetProperty("displayName", col.Name));

			//	while (en.MoveNext())
			//		Debug.Print("ANSP: {0}, {1}, {2}", en.Current["Name"], en.Current["Tel"], en.Current["Fax"]);
			//}

			//var w = new Wpf.PpsReportSelectWindow();
			//var wh = new WindowInteropHelper(w);
			//wh.Owner = new IntPtr(application.Hwnd);
			//w.ShowDialog();
		}

		private void cmdTable_Click(object sender, RibbonControlEventArgs e)
		{
			//var w = new Wpf.PpsTableImportWindow();
			//var wh = new WindowInteropHelper(w);
			//wh.Owner = new IntPtr(application.Hwnd);
			//if (w.ShowDialog() ?? false)
			//	Globals.ThisAddIn.ImportTable(w.TableName, w.TableSourceId);
		} // event cmdTable_Click

		private void cmdStyles_Click(object sender, RibbonControlEventArgs e)
		{
		}

		private void cmdRefresh_Click(object sender, RibbonControlEventArgs e)
		{
			//var xDoc = XDocument.Load(@"C:\Projects\PPSnOS\twppsn\PPSnWpf\PPSnDesktop\Local\Data\contacts.xml");
			//xDoc.Root.Element("columns").Remove();

			//var map = Globals.ThisAddIn.Application.ActiveWorkbook.XmlMaps.Item[1];
			//map.ImportXml(xDoc.ToString(SaveOptions.None), true);
		}

		private void loginGalery_ItemsLoading(object sender, RibbonControlEventArgs e)
			=> RefreshEnvironments();
		
		private void loginGalery_Click(object sender, RibbonControlEventArgs e)
			=> Globals.ThisAddIn.ActivateEnvironment(loginGalery.SelectedItem?.Tag as PpsEnvironmentInfo);

		private void logoutButton_Click(object sender, RibbonControlEventArgs e)
			=> Globals.ThisAddIn.DeactivateEnvironment();
		
		/// <summary>Was Loaded called.</summary>
		public bool IsMenuLoaded => isMenuLoaded;
	} // class PpsMenu
}
