using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Office.Tools.Excel;
using TecWare.DE.Data;
using TecWare.DE.Networking;
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
				environment = new PpsExcelEnvironment(ppsLocalEnvironment, this);
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
			// QD
			while (!task.IsCompleted)
			{
				System.Windows.Forms.Application.DoEvents();
				Thread.Sleep(100);
			}
			return task.Result;
		} // proc RunUISynchron

		public void RunUISynchron(Task task)
		{
			// QD
			while (!task.IsCompleted)
			{
				System.Windows.Forms.Application.DoEvents();
				Thread.Sleep(100);
			}
			task.Wait();
		} // proc RunUISynchron

		#region -- ImportTable ------------------------------------------------------------

		internal void ImportTable(object tableName, object tableSourceId)
		{
			// todo: use the parameters
			// todo: feedback for the user
			// todo: exception handling

			if (environment == null)
				return;

			if (!environment.IsAuthentificated)
				return;

			var columns = new List<KeyValuePair<string, Type>>();
			var rows = new List<XElement>();
			#region -- get columns and rows --
			using (var e = environment.Request.CreateViewDataReader("remote/?action=viewget&v=dbo.objects").GetEnumerator())
			{
				if (e.MoveNext())
				{
					for (var i = 0; i < e.Current.Columns.Count; i++)
						columns.Add(new KeyValuePair<string, Type>(e.Current.Columns[i].Name, e.Current.Columns[i].DataType));

					do
					{
						var row = new XElement("r");
						for (var i = 0; i < e.Current.Columns.Count; i++)
						{
							if (e.Current[i] != null)
							{
								row.Add(new XElement(e.Current.Columns[i].Name,
									Procs.ChangeType<string>(e.Current[i])
								));
							}
						}
						rows.Add(row);
					} while (e.MoveNext());
				} // if e.MoveNext
			} // using e
			#endregion

			if (columns.Count < 1)
				return;

			var schema = string.Empty;
			#region -- create schema for XML mapping --
			var xsdNamespace = XNamespace.Get("http://www.w3.org/2001/XMLSchema");
			var xsdNamespaceShortcut = "xs";
			schema = new XElement(xsdNamespace + "schema",
				new XAttribute("elementFormDefault", "qualified"),
				new XAttribute(XNamespace.Xmlns + xsdNamespaceShortcut, xsdNamespace),
				new XElement(xsdNamespace + "element",
					new XAttribute("name", "view"),
					new XElement(xsdNamespace + "complexType",
						new XElement(xsdNamespace + "sequence",
							new XElement(xsdNamespace + "element",
								new XAttribute("name", "rows"),
								new XElement(xsdNamespace + "complexType",
									new XElement(xsdNamespace + "sequence",
										new XElement(xsdNamespace + "element",
											new XAttribute("name", "r"),
											new XAttribute("minOccurs", "0"),
											new XAttribute("maxOccurs", "unbounded"),
											new XElement(xsdNamespace + "complexType",
												new XElement(xsdNamespace + "sequence",
													from c in columns select new XElement(xsdNamespace + "element",
														new XAttribute("name", c.Key),
														new XAttribute("type", String.Format("{0}:{1}", xsdNamespaceShortcut, typeToXsdType[c.Value]))
													)
												)
											)
										)
									)
								)
							)
						)
					)
				)
			).ToString();
			#endregion

			var data = new XElement("view",
				new XElement("rows",
					rows
				)
			).ToString();

			// get active workbook and worksheet
			var worksheet = Globals.Factory.GetVstoObject((Excel._Worksheet)Globals.ThisAddIn.Application.ActiveSheet);
			var workbook = Globals.Factory.GetVstoObject((Excel._Workbook)worksheet.Parent);

			// add created schema for XML mapping
			// todo: check for schema existence (duplicates!)
			var map = workbook.XmlMaps.Add(schema, "view");

			// create list object and add header
			// todo: exception occurs if the selected cell is already part of a table
			var list = Globals.Factory.GetVstoObject((Excel.ListObject)worksheet.ListObjects.Add());
			var flag = true;
			foreach (var column in columns)
			{
				var columnName = column.Key;
				var listColumn = flag ? list.ListColumns[1] : list.ListColumns.Add();
				flag = false;
				listColumn.Name = columnName;
				listColumn.XPath.SetValue(map, "/view/rows/r/" + columnName);
			}

			// import data
			var result = map.ImportXml(data, true);
			// todo: check result
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

		public App App => app;

		// -- Static --------------------------------------------------------------

		private static readonly Dictionary<Type, string> typeToXsdType = new Dictionary<Type, string>
		{
			{ typeof(byte), "unsignedByte" },
			{ typeof(sbyte), "byte" },
			{ typeof(ushort), "unsignedShort" },
			{ typeof(short), "short" },
			{ typeof(uint), "unsignedInt" },
			{ typeof(int), "int" },
			{ typeof(ulong), "unsignedLong" },
			{ typeof(long), "long" },
			// todo: Check functionality.
			{ typeof(float), "float" },
			// todo: Check functionality.
			{ typeof(double), "double" },
			// todo: Check functionality.
			{ typeof(decimal), "decimal" },
			// todo: Check functionality. Try to find better type match.
			{ typeof(DateTime), "string" },
			// todo: Check functionality. Try to find better type match.
			{ typeof(char), "string" },
			{ typeof(string), "string" },
			{ typeof(bool), "boolean" },
			// todo: Check functionality. Try to find better type match.
			{ typeof(Guid), "string" },
			// todo: Check functionality. Try to find better type match.
			{ typeof(XDocument), "string" },
			// todo: Check functionality. Try to find better type match.
			{ typeof(byte[]), "string" }
		};
	} // class ThisAddIn
}
