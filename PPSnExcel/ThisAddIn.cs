using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Office.Tools.Excel;
using TecWare.DE.Stuff;
using TecWare.PPSn;
using TecWare.PPSn.Data;
using Excel = Microsoft.Office.Interop.Excel;
using Office = Microsoft.Office.Core;

namespace PPSnExcel
{
	public partial class ThisAddIn
	{
		private PpsEnvironment environment = null;
		private App app = null;

		private void ThisAddIn_Startup(object sender, System.EventArgs e)
		{
			this.app = new App();
		} // ctor

		private void ThisAddIn_Shutdown(object sender, System.EventArgs e)
		{
			app.Shutdown();
			LoginEnvironment(null);
		} // event ThisAddIn_Shutdown

		public void LoginEnvironment(PpsEnvironmentInfo ppsLocalEnvironment)
		{
			if (ppsLocalEnvironment == null) // clear the current environment
			{
				Globals.Ribbons.PpsMenu.Environment = null;
				Procs.FreeAndNil(ref environment);
			}
			else
			{
				environment = new PpsEnvironment(ppsLocalEnvironment, app.Resources);
				try
				{
					// Start the environment
					RunUISynchron(environment.StartOnlineMode(CancellationToken.None));
					Globals.Ribbons.PpsMenu.Environment = environment;

					// Try login
					RunUISynchron(environment.LoginUserAsync());
				}
				catch (Exception ex)
				{
					Globals.Ribbons.PpsMenu.Environment = null;
					environment.ShowException(ExceptionShowFlags.None, ex, "Excel Addin für PPSn konnte nicht geladen werden.");
				}
			}
		} // proc LoginEnvironment

		public T RunUISynchron<T>(Task<T> task)
		{
			return task.Result;
		} // proc RunUISynchron

		public void RunUISynchron(Task task)
		{
			task.Wait();
		} // proc RunUISynchron

		#region -- ImportTable ------------------------------------------------------------

		internal void ImportTable(object tableName, object tableSourceId)
		{
			// get the active sheet and selection
			var workSheet = Globals.Factory.GetVstoObject((Excel._Worksheet)Globals.ThisAddIn.Application.ActiveSheet);
			var workBook = Globals.Factory.GetVstoObject((Excel._Workbook)workSheet.Parent);
			var selection = Globals.ThisAddIn.Application.Selection;

			// create the xml map
			var schema = File.ReadAllText(@"C:\Projects\PPSnOS\twppsn\PPSnWpf\PPSnDesktop\Local\Data\contact.xsd");

			var map = workBook.XmlMaps.Add(schema, "datareader");

			// create the list object
			var list = Globals.Factory.GetVstoObject((Excel.ListObject)workSheet.ListObjects.Add());

			var xDoc = XDocument.Load(@"C:\Projects\PPSnOS\twppsn\PPSnWpf\PPSnDesktop\Local\Data\contacts.xml");
			var f = true;
			foreach (var x in xDoc.Root.Element("columns").Elements())
			{
				var columnName = x.Attribute("name")?.Value;

				var col = f ? list.ListColumns[1] : list.ListColumns.Add();
				f = false;
				col.Name = columnName.ToLower();
				col.XPath.SetValue(map, "/datareader/items/item/" + columnName);

				if (columnName == "OBJKTYP")
				{
					col.Range.HorizontalAlignment = Excel.Constants.xlCenter;
				}
				else
				{
					col.Range.HorizontalAlignment = Excel.Constants.xlLeft;
				}
				if (columnName == "ADRESSE")
					col.Range.EntireColumn.ColumnWidth = 50.0;
			}

			xDoc.Root.Element("columns").Remove();

			map.ImportXml(xDoc.ToString(SaveOptions.None), true);
		} // proc ImportTable

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
	} // class ThisAddIn
}
