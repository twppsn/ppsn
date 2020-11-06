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
using System.Windows.Forms;
using System.Xml.Linq;
using Microsoft.Office.Tools.Ribbon;
using TecWare.DE.Stuff;
using TecWare.PPSn;
using Excel = Microsoft.Office.Interop.Excel;

namespace PPSnExcel
{
	/// <summary></summary>
	public partial class PpsMenu
	{
		private Excel.Application application;

		private bool isMenuLoaded = false;

		#region -- Load ---------------------------------------------------------------

		private void PpsMenu_Load(object sender, RibbonUIEventArgs e)
		{
			application = Globals.ThisAddIn.Application;
			cmdExtended.Visible = Globals.ThisAddIn.Application.ShowDevTools;

			// connection to the excel application
			application.WorkbookActivate += wb => SelectionChanged(wb, true);
			application.WorkbookDeactivate += wb => SelectionChanged(wb, false);
			application.SheetActivate += sh=> SelectionChanged(sh, true);
			application.SheetDeactivate += sh => SelectionChanged(sh, false);
			application.SheetSelectionChange += (sh, target) => SelectionChanged(target, true);

			PpsShell.CurrentChanged += (s, _e) => { RefreshUsername(); Refresh(); };

			// init environment
			RefreshEnvironments();
			RefreshUsername();
			Refresh();

			isMenuLoaded = true;
		} // event PpsMenu_Load

		private void Application_SheetActivate(object Sh) => throw new NotImplementedException();

		#endregion

		#region -- RunActionSafe ------------------------------------------------------

		private void RunActionSafe(Action action)
		{
			WaitForm.CheckSynchronizationContext();
			try
			{
				action();
			}
			catch(ExcelException e)
			{
				Globals.ThisAddIn.ShowMessage(e.Message, MessageBoxIcon.Information, MessageBoxButtons.OK);
			}
			catch (Exception e)
			{
				Globals.ThisAddIn.ShowException(PpsExceptionShowFlags.None, e);
			}
		} // proc RunActionSafe

		#endregion

		public void Refresh()
		{
			var currentShell = PpsShell.Current;
			var hasShell = currentShell != null;
			var hasListObjectInfo = PpsListMapping.TryParseFromSelection();
			cmdReport.Enabled = hasShell;
			cmdTable.Enabled = hasShell || hasListObjectInfo;

			editTableExCommand.Enabled = hasListObjectInfo;
			cmdListObjectInfo.Enabled = hasListObjectInfo;
			removeTableSourceData.Enabled = hasListObjectInfo;

			cmdRefresh.Enabled =
				cmdRefreshLayout.Enabled = Globals.ThisAddIn.Application.Selection is Excel.Range r && !(r.ListObject is null);
		} // proc Refresh

		private void RefreshUsername()
		{
			var currentShell = PpsShell.Current;
			loginMenu.Label = currentShell is null ? "Keine Umgebung" : $"{currentShell.Info.Name} ({"currentShell.UserName"})";
			loginGalery.Label = currentShell is null ? "Keine Umgebung" : $"{currentShell.Info.Name} ({currentShell.Info.DisplayName})";
			logoutButton.Enabled = !(currentShell is null);
		} // proc RefreshUsername

		private void RefreshEnvironments()
		{
			// remove all instances
			loginGalery.Items.Clear();
			
			// readd them
			var shellFactory = PpsShell.Global.GetService<IPpsShellFactory>(true);
			foreach (var cur in shellFactory.OrderBy(c => c.DisplayName))
			{
				var shell = Globals.ThisAddIn.GetShellFromInfo(cur);
				var ribbonButton = Factory.CreateRibbonDropDownItem();
				ribbonButton.Label = cur.Name ?? cur.DisplayName;
				ribbonButton.ScreenTip = $"{ cur.Name} ({cur.DisplayName})";
				ribbonButton.SuperTip =
					shell == null
						? String.Format("Version {0}\nUri: {1}", cur.Version, cur.Uri.ToString())
						: String.Format("Angemeldet: {2}\nVersion {0}\nUri: {1}", cur.Version, cur.Uri.ToString(), PpsShell.GetUserNameFromCredentials(shell.Http?.Credentials));
				ribbonButton.Tag = cur;
				ribbonButton.Image = shell != null && shell.IsAuthentificated ? Properties.Resources.EnvironmentAuthImage : Properties.Resources.EnvironmentImage;
				loginGalery.Items.Add(ribbonButton);
			}
		} // proc RefreshEnvironments

		private static Excel.Range GetTopLeftCell() 
			=> Globals.ThisAddIn.Application.Selection as Excel.Range;
		
		public static bool IsSingleLineModeToggle()
			=> (Control.ModifierKeys & (Keys.Control | Keys.Alt)) == (Keys.Control | Keys.Alt);

		private static void InsertReport()
		{
			var shell = PpsShell.Current;
			if (shell != null)
			{
				using (var frm = new ReportInsertForm(shell))
				{
					if (frm.ShowDialog(Globals.ThisAddIn) == DialogResult.OK)
					{
						if (frm.ReportType == "table")
							Globals.ThisAddIn.NewTable(shell, GetTopLeftCell(), frm.ReportId);
						else if (frm.ReportType == "xlsx")
							Globals.ThisAddIn.LoadXlsxReport(shell, frm.ReportId, frm.ReportName);
						else
							MessageBox.Show("Unbekannter ReportType.");
					}
				}
			}
		} // proc InsertReport

		private static void InsertTable()
			=> InsertTableCore(false);

		private static void InsertTableEx()
			=> InsertTableCore(true);

		private static void RemoveTableData()
		{
			if (!PpsListObject.TryGetFromSelection(out var list))
				return;

			if (MessageBox.Show(String.Format("Remove Xml-Data of {0} ({1})?", list.List.DisplayName, list.List.XmlMap.Name), "Question", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
			{
				var x = new XElement(list.List.XmlMap.RootElementName).ToString();
				list.List.XmlMap.ImportXml(x.ToString(), true);
			}
		} // proc RemoveTableData

		private static void InsertTableCore(bool extendedEdit)
		{
			if (PpsListObject.TryGetFromSelection(out var ppsList)) // edit the current selected table
				ppsList.Edit(extendedEdit);
			else if (!extendedEdit) // create a fresh table
			{
				var shell = PpsShell.Current; // get shell
				if (shell != null)
					PpsListObject.New(shell, GetTopLeftCell());
			}
		} // proc InsertTableCore

		private void SelectionChanged(object sender, bool activate)
		{
			Refresh();
		} // proc SelectionChanged

		private void cmdReport_Click(object sender, RibbonControlEventArgs e)
			=> RunActionSafe(InsertReport);

		private void cmdTable_Click(object sender, RibbonControlEventArgs e)
			=> RunActionSafe(InsertTable);

		private void editTableExCommand_Click(object sender, RibbonControlEventArgs e)
			=> RunActionSafe(InsertTableEx);

		private void removeTableSourceData_Click(object sender, RibbonControlEventArgs e)
			=> RunActionSafe(RemoveTableData);

		private void cmdListObjectInfo_Click(object sender, RibbonControlEventArgs e)
			=> RunActionSafe(Globals.ThisAddIn.ShowTableInfo);

		private void cmdStyles_Click(object sender, RibbonControlEventArgs e)
		{
		}

		private void RunRefreshTableCommand(ThisAddIn.RefreshContext refreshContext)
			=> RunActionSafe(() => Globals.ThisAddIn.RefreshTableAsync(refreshContext).Await());

		private void cmdRefresh_Click(object sender, RibbonControlEventArgs e)
			=> RunRefreshTableCommand(ThisAddIn.RefreshContext.ActiveListObject);

		private void cmdRefreshLayout_Click(object sender, RibbonControlEventArgs e)
			=> RunRefreshTableCommand(ThisAddIn.RefreshContext.ActiveListObjectLayout);

		private void cmdRefreshAll_Click(object sender, RibbonControlEventArgs e)
			=> RunRefreshTableCommand(ThisAddIn.RefreshContext.ActiveWorkBook);

		private void loginGalery_ItemsLoading(object sender, RibbonControlEventArgs e)
			=> RunActionSafe(RefreshEnvironments);

		private void loginGalery_Click(object sender, RibbonControlEventArgs e)
			=> RunActionSafe(() => Globals.ThisAddIn.ActivateEnvironment(loginGalery.SelectedItem?.Tag as IPpsShellInfo));

		private void logoutButton_Click(object sender, RibbonControlEventArgs e)
			=> RunActionSafe(() => Globals.ThisAddIn.DeactivateShell());

		/// <summary>Was Loaded called.</summary>
		public bool IsMenuLoaded => isMenuLoaded;
	} // class PpsMenu
}
