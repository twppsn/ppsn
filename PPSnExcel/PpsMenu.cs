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
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
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
			cmdExtended.Visible = false; // Globals.ThisAddIn.Application.ShowDevTools;
						
			// connection to the excel application
			application.WorkbookActivate += wb => WorkbookStateChanged(wb, true);
			application.WorkbookDeactivate += wb => WorkbookStateChanged(wb, false);

			application.SheetSelectionChange += (sh, target) => Refresh();

			Globals.ThisAddIn.CurrentEnvironmentChanged += (s, _e) => { RefreshUsername(); Refresh(); };
			
			// init environment
			RefreshEnvironments();
			RefreshUsername();
			Refresh();

			isMenuLoaded = true;
		} // event PpsMenu_Load

		#endregion

		#region -- RunActionSafe ------------------------------------------------------

		private void RunActionSafe(Action action)
		{
			try
			{
				action();
			}
			catch (Exception e)
			{
				Globals.ThisAddIn.ShowException(ExceptionShowFlags.None, e);
			}
		} // proc RunActionSafe

		#endregion

		public void Refresh()
		{
			var currentEnvironment = Globals.ThisAddIn.CurrentEnvironment;
			cmdReport.Enabled = 
				cmdTable.Enabled = currentEnvironment != null;
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
				var env = Globals.ThisAddIn.GetEnvironmentFromInfo(cur);
				var ribbonButton = Factory.CreateRibbonDropDownItem();
				ribbonButton.Label = cur.Name ?? cur.DisplayName;
				ribbonButton.ScreenTip = $"{ cur.Name} ({cur.DisplayName})";
				ribbonButton.SuperTip =
					env == null 
						? String.Format("Version {0}\nUri: {1}", cur.Version, cur.Uri.ToString())
						: String.Format("Angemeldet: {2}\nVersion {0}\nUri: {1}", cur.Version, cur.Uri.ToString(), env.UserName);
				ribbonButton.Tag = cur;
				ribbonButton.Image = env != null && env.IsAuthentificated ? Properties.Resources.EnvironmentAuthImage : Properties.Resources.EnvironmentImage;
				loginGalery.Items.Add(ribbonButton);
			}
		} // proc RefreshEnvironments

		private static void InsertReport()
		{
			var env = Globals.ThisAddIn.CurrentEnvironment;
			if (env != null)
			{
				using (var frm = new ReportInsertForm(env))
				{
					if (frm.ShowDialog(Globals.ThisAddIn) == DialogResult.OK)
					{
						if (frm.ReportSource is PpsShellGetList args)
							Globals.ThisAddIn.ImportTable(env, frm.ReportName, args);
						else
							MessageBox.Show("todo");
					}
				}
			}
		} // proc InsertReport

		private static void InsertTable()
		{
			var env = Globals.ThisAddIn.CurrentEnvironment;
			if (env != null)
			{
				using (var frm = new TableInsertForm(env))
				{
					if (frm.ShowDialog(Globals.ThisAddIn) == DialogResult.OK)
						Globals.ThisAddIn.ImportTable(env, frm.ReportName, frm.ReportSource);
				}
			}
		} // proc InsertTable

		private void WorkbookStateChanged(Excel._Workbook wb, bool activate)
		{
			Refresh();
		} // proc WorkbookStateChanged

		private void cmdReport_Click(object sender, RibbonControlEventArgs e)
			=> RunActionSafe(InsertReport);

		private void cmdTable_Click(object sender, RibbonControlEventArgs e)
			=> RunActionSafe(InsertTable);

		private void cmdStyles_Click(object sender, RibbonControlEventArgs e)
		{
		}

		private void cmdRefresh_Click(object sender, RibbonControlEventArgs e)
			=> RunActionSafe(Globals.ThisAddIn.ShowTableInfo);

		private void loginGalery_ItemsLoading(object sender, RibbonControlEventArgs e)
			=> RunActionSafe(RefreshEnvironments);

		private void loginGalery_Click(object sender, RibbonControlEventArgs e)
			=> RunActionSafe(() => Globals.ThisAddIn.ActivateEnvironment(loginGalery.SelectedItem?.Tag as PpsEnvironmentInfo));

		private void logoutButton_Click(object sender, RibbonControlEventArgs e)
			=> RunActionSafe(() => Globals.ThisAddIn.DeactivateEnvironment());
		
		/// <summary>Was Loaded called.</summary>
		public bool IsMenuLoaded => isMenuLoaded;
	} // class PpsMenu
}
