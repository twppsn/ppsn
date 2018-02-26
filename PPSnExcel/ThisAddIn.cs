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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Data;
using TecWare.PPSn;
using TecWare.PPSn.Data;
using Excel = Microsoft.Office.Interop.Excel;

namespace PPSnExcel
{
	public partial class ThisAddIn : IWin32Window, IPpsShell
	{
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

		public void Await(Task task)
			=> waitForm.Await(task);

		public T Await<T>(Task<T> task)
		{
			Await((Task)task);
			return task.Result;
		} // func Await

		public void Invoke(Action action)
		{
			if (waitForm.InvokeRequired)
				waitForm.Invoke(action);
			else
				action();
		} // proc Invoke

		public Task InvokeAsync(Action action)
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

		public void BeginInvoke(Action action)
		{
			if (waitForm.InvokeRequired)
				waitForm.BeginInvoke(action);
			else
				action();
		} // proc BeginInvoke

		public IProgress<string> GetAwaitProgress()
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

		public void ActivateEnvironment(PpsEnvironmentInfo info)
		{
			if (info is null)
				return;

			var env = FindOrCreateEnvironment(info);
			if (env.IsAuthentificated // is authentificated
				|| Await(env.LoginAsync(this))) // try to authentificate the environment
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

		private static readonly XNamespace namespaceName = XNamespace.Get("http://www.w3.org/2001/XMLSchema");

		private static (string schemaData, IDataColumns columnInfo, string xmlData) GetImportTable(PpsEnvironment env, string rootElementName, PpsShellGetList tableSource)
		{
			var xmlData = String.Empty;
			var schemaData = String.Empty;
			var columnInfo = new List<SimpleDataColumn>();

			using (var tw = new StringWriter(CultureInfo.InvariantCulture))
			using (var x = XmlWriter.Create(tw, new XmlWriterSettings() { Encoding = Encoding.Default, NewLineHandling = NewLineHandling.None }))
			using (var e = env.GetViewData(tableSource).GetEnumerator())
			{
				var columns = e as IDataColumns;

				const string namespaceShortcut = "xs";

				XElement xColumns;
				#region -- base schema --
				var xSchema =
					new XElement(namespaceName + "schema",
						new XAttribute("elementFormDefault", "qualified"),
						new XAttribute(XNamespace.Xmlns + namespaceShortcut, namespaceName),
						new XElement(namespaceName + "annotation",
							new XElement(namespaceName + "documentation", "todo")
						),
						new XElement(namespaceName + "element",
							new XAttribute("name", rootElementName),
							new XElement(namespaceName + "complexType",
								new XElement(namespaceName + "sequence",
									new XElement(namespaceName + "element",
										new XAttribute("name", "r"),
										new XAttribute("minOccurs", "0"),
										new XAttribute("maxOccurs", "unbounded"),
										new XElement(namespaceName + "complexType",
											xColumns = new XElement(namespaceName + "sequence")
										)
									)
								)
							)
						)
				);
				#endregion

				#region -- read column information --

				for (var i = 0; i < columns.Columns.Count; i++)
				{
					var dataColumn = columns.Columns[i];

					// add column
					columnInfo.Add(new SimpleDataColumn(dataColumn));

					// set schema
					xColumns.Add(new XElement(namespaceName + "element",
						new XAttribute("name", dataColumn.Name),
						new XAttribute("type", namespaceShortcut + ":" + typeToXsdType[dataColumn.DataType])
					));
				}
				#endregion

				if (columnInfo.Count == 0)
					throw new ExcelException("Ergebnismenge hat keine Spalten.");

				schemaData = xSchema.ToString(SaveOptions.DisableFormatting);

				#region -- Fetch data --
				x.WriteStartElement(rootElementName);

				while (e.MoveNext())
				{
					var r = e.Current;
					x.WriteStartElement("r");

					for (var i = 0; i < columnInfo.Count; i++)
					{
						x.WriteStartElement(columnInfo[i].Name);
						x.WriteValue(r[i]);
						x.WriteEndElement();
					}

					x.WriteEndElement();

				}

				x.WriteEndElement();

				#endregion

				x.Flush();
				tw.Flush();

				xmlData = tw.GetStringBuilder().ToString();
			}

			return (schemaData, new SimpleDataColumns(columnInfo.ToArray()), schemaData);
		} // func GetImportTable

		internal void ImportTable(PpsEnvironment env, string tableName, PpsShellGetList tableSource)
		{
			// get active workbook and worksheet
			var worksheet = Globals.Factory.GetVstoObject((Excel._Worksheet)Globals.ThisAddIn.Application.ActiveSheet);
			var workbook = Globals.Factory.GetVstoObject((Excel._Workbook)worksheet.Parent);

			//var progress = GetAwaitProgress();
			var rootElementName = "data";

			var (schemaData, columnInfo, xmlData) = Await(Task.Run(() => GetImportTable(env, rootElementName, tableSource)));

			// add created schema for XML mapping
			// todo: check for schema existence (duplicates!)
			var map = workbook.XmlMaps.Add(schemaData, rootElementName);
			map.Name = rootElementName;

			// create list object and add header
			if (Globals.ThisAddIn.Application.Selection is Excel.Range range && range.ListObject != null)
				throw new ExcelException("Tabelle darf nicht innerhalb einer anderen Tabelle eingefügt werden.");

			// todo: exception occurs if the selected cell is already part of a table
			var list = Globals.Factory.GetVstoObject((Excel.ListObject)worksheet.ListObjects.Add());
			var flag = true;
			foreach (var column in columnInfo.Columns)
			{
				var columnName = column.Name;
				var listColumn = flag ? list.ListColumns[1] : list.ListColumns.Add();
				flag = false;
				listColumn.Name = columnName;
				listColumn.XPath.SetValue(map, "/" + rootElementName + "/r/" + columnName);
			}

			// import data
			switch (map.ImportXml(xmlData, true))
			{
				case Excel.XlXmlImportResult.xlXmlImportElementsTruncated:
					throw new ExcelException("Zu viele Element, nicht alle Zeilen wurden geladen.");
				case Excel.XlXmlImportResult.xlXmlImportValidationFailed:
					throw new ExcelException("Validierung der Rohdaten fehlgeschlagen.");
			}

			//// todo: use the parameters
			//// todo: feedback for the user
			//// todo: exception handling
		} // proc ImportTable

		internal void ShowTableInfo()
		{
			var worksheet = Globals.Factory.GetVstoObject((Excel._Worksheet)Globals.ThisAddIn.Application.ActiveSheet);
			var workbook = Globals.Factory.GetVstoObject((Excel._Workbook)worksheet.Parent);

			//worksheet.ListObjects
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
