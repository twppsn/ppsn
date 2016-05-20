using System;
using System.Collections.Generic;
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
		private PpsEnvironment environment;

		private void PpsMenu_Load(object sender, RibbonUIEventArgs e)
		{
			application = Globals.ThisAddIn.Application;
			cmdExtended.Visible = Globals.ThisAddIn.Application.ShowDevTools;

			if (environment != null)
				InitMenu();

			Refresh();
		} // event PpsMenu_Load

		private void InitMenu()
		{
			if (application == null)
				return;

			application.WorkbookActivate += wb => WorkbookStateChanged(wb, true);
			application.WorkbookDeactivate += wb => WorkbookStateChanged(wb, false);

			application.SheetSelectionChange += (sh, target) => Refresh();

			environment.UsernameChanged += (sender, e) => RefreshUsername();

			RefreshUsername();
    } // proc InitMenu

		public void Refresh()
		{
		} // proc Refresh

		private void RefreshUsername()
		{
			cmdLogin.Label = environment.UsernameDisplay;
		} // proc RefreshUsername

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
				if (value != null)
					InitMenu();
			}
		} // prop Environment

		private void cmdReport_Click(object sender, RibbonControlEventArgs e)
		{
			var w = new Wpf.PpsReportSelectWindow();
			var wh = new WindowInteropHelper(w);
			wh.Owner = new IntPtr(application.Hwnd);
			w.ShowDialog();
        } // event cmdReport_Click

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
            throw new NotImplementedException();
        } // event cmdStyles_Click

        private void cmdRefresh_Click(object sender, RibbonControlEventArgs e)
		{
			var xDoc = XDocument.Load(@"C:\Projects\PPSnOS\twppsn\PPSnWpf\PPSnDesktop\Local\Data\contacts.xml");
			xDoc.Root.Element("columns").Remove();

			var map = Globals.ThisAddIn.Application.ActiveWorkbook.XmlMaps.Item[1];
			map.ImportXml(xDoc.ToString(SaveOptions.None), true);
        } // event cmdRefresh_Click
    } // class PpsMenu
}
