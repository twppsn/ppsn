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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Office.Tools.Excel;
using PPSnExcel.Data;
using TecWare.DE.Data;
using TecWare.DE.Stuff;
using TecWare.PPSn;
using TecWare.PPSn.Data;
using Excel = Microsoft.Office.Interop.Excel;

namespace PPSnExcel
{
	#region -- class PpsXmlMap --------------------------------------------------------

	internal sealed class PpsListMapping
	{
		#region -- Xsd ----------------------------------------------------------------

		private static readonly XNamespace namespaceName = XNamespace.Get("http://www.w3.org/2001/XMLSchema");
		private static readonly XName xsdElementName = namespaceName + "element";
		private static readonly XName xsdAnnotationName = namespaceName + "annotation";
		private static readonly XName xsdDocumentationName = namespaceName + "documentation";
		private static readonly XName xsdComplexTypeName = namespaceName + "complexType";
		private static readonly XName xsdSequenceName = namespaceName + "sequence";

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
			{ typeof(double), "double" },
			{ typeof(decimal), "decimal" },
			// todo: Check functionality. Try to find better type match.
			{ typeof(DateTime), "dateTime" },
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

		#endregion

		private static readonly Regex schemaName = new Regex(@"^ppsnXsd(?<n>\d+)$");

		private const string environmentNameTag = "env";
		private const string environmentUriTag = "uri";
		private const string filterTag = "filter";
		private const string ordersTag = "orders";
		private const string viewTag = "view";
		private const string columnsTag = "columns";

		private readonly PpsEnvironment environment;            // attached environment
		private readonly string select;                         // view or views
		private readonly string filterExpr;                     // uses placeholder for cells
		private readonly PpsDataColumnExpression[] columns;     // columns to fetch
		private readonly PpsDataOrderExpression[] orders;       // fetch sorting of the rows

		#region -- Ctor/Dtor ----------------------------------------------------------

		public PpsListMapping(PpsEnvironment environment, string select, PpsDataColumnExpression[] columns, string filterExpr, PpsDataOrderExpression[] orders)
		{
			this.environment = environment ?? throw new ArgumentNullException(nameof(environment));
			this.select = select ?? throw new ArgumentNullException(nameof(select));
			this.filterExpr = filterExpr ?? String.Empty;
			this.columns = columns ?? Array.Empty<PpsDataColumnExpression>();
			this.orders = orders ?? Array.Empty<PpsDataOrderExpression>();
		} // ctor

		#endregion

		#region -- GetViewData --------------------------------------------------------

		private IPropertyReadOnlyDictionary GetVariables(Worksheet worksheet, SynchronizationContext context)
			=> null;

		public IEnumerator<IDataRow> GetViewData(Worksheet current, SynchronizationContext context = null, bool headerOnly = false)
		{
			var request = new PpsShellGetList(select)
			{
				Columns = columns,
				Filter = PpsDataFilterExpression.Parse(filterExpr, GetVariables(current, context)),
				Order = orders
			};

			if (headerOnly)
			{
				request.Start = 0;
				request.Count = 0;
			}

			var e = environment.GetViewData(request).GetEnumerator();
			try
			{
				if (((IDataColumns)e).Columns.Count == 0)
					throw new ExcelException("Ergebnismenge hat keine Spalten.");
				return e;
			}
			catch
			{
				e.Dispose();
				throw;
			}
		} // func GetViewData

		#endregion

		#region -- Excel Xml Map ------------------------------------------------------

		private (XElement, string[]) GenerateXsd(IDataColumns columnInfo, string rootElementName)
		{
			const string namespaceShortcut = "xs";

			if (columnInfo == null)
				throw new ExcelException("Ergebnismenge hat keine Spalteninformation.");
			if (columnInfo.Columns.Count == 0)
				throw new ExcelException("Ergebnismenge hat keine Spalten.");

			var annotation = XlProcs.UpdateProperties(String.Empty,
				new KeyValuePair<string, string>(environmentNameTag, environment.Info.Name),
				new KeyValuePair<string, string>(environmentUriTag, environment.Info.Uri.ToString()),
				new KeyValuePair<string, string>(viewTag, select),
				new KeyValuePair<string, string>(filterTag, filterExpr),
				new KeyValuePair<string, string>(ordersTag, PpsDataOrderExpression.ToString(orders)),
				new KeyValuePair<string, string>(columnsTag, PpsDataColumnExpression.ToString(columns))
			);

			XElement xColumns;
			#region -- base schema --
			var xSchema =
				new XElement(namespaceName + "schema",
					new XAttribute("elementFormDefault", "qualified"),
					new XAttribute(XNamespace.Xmlns + namespaceShortcut, namespaceName),
					new XElement(xsdAnnotationName,
						new XElement(xsdDocumentationName, annotation)
					),
					new XElement(xsdElementName,
						new XAttribute("name", rootElementName),
						new XElement(xsdComplexTypeName,
							new XElement(xsdSequenceName,
								new XElement(xsdElementName,
									new XAttribute("name", "r"),
									new XAttribute("minOccurs", "0"),
									new XAttribute("maxOccurs", "unbounded"),
									new XElement(xsdComplexTypeName,
										xColumns = new XElement(xsdSequenceName)
									)
								)
							)
						)
					)
			);
			#endregion

			var columnNames = new string[columnInfo.Columns.Count];
			for (var i = 0; i < columnNames.Length; i++)
			{
				var col = columnInfo.Columns[i];

				var xColumnDef = new XElement(xsdElementName,
					new XAttribute("name", columnNames[i] = col.Name),
					new XAttribute("type", namespaceShortcut + ":" + typeToXsdType[col.DataType]),
					new XAttribute("minOccurs", col.Attributes.TryGetProperty<bool>("nullable", out var isNullable) && isNullable ? 0 : 1),
					new XAttribute("maxOccurs", 1)
				);

				// set schema
				xColumns.Add(xColumnDef);
			}

			return (xSchema, columnNames);
		} // func GenerateXsd

		private IEnumerable<XElement> XsdSchemaElements(Excel.XmlMap map, string listName)
		{
			if (map == null || map.Schemas.Count != 1)
				throw new ArgumentException($"Schema information for list is invalid [{listName}].");
			if (!TryParseSchema(map.Schemas[1].XML, out var xSchema))
				throw new ArgumentException($"Format exception for schema information [{listName}]");

			// parse column names from schema
			return xSchema.Element(xsdElementName).Element(xsdComplexTypeName).Element(xsdSequenceName)
					.Element(xsdElementName).Element(xsdComplexTypeName).Element(xsdSequenceName)
					.Elements(xsdElementName);
		} // func XsdSchemaElements

		private (bool isChanged, string[] columnNames) IsXsdSchemaChange(Excel.XmlMap map, string listName, IDataColumns columnInfo)
		{
			var columnNames = new string[columnInfo.Columns.Count];

			bool TestXsdType(string xsdType, Type netType)
				=> typeToXsdType.TryGetValue(netType, out var tmp) && tmp == xsdType;

			// test schema
			foreach (var x in XsdSchemaElements(map, listName))
			{
				var name = x.Attribute("name").Value;
				var type = x.Attribute("type").Value;

				var columnIndex = columnInfo.FindColumnIndex(name, false);
				if (columnIndex == -1)
					return (true, null);
				else
				{
					columnNames[columnIndex] = name;
					var columnType = columnInfo.Columns[columnIndex].DataType;
					if (type.Length < 3 || !TestXsdType(type.Substring(3), columnType))
						return (true, null);
				}
			}

			// touched all columns
			for (var i = 0; i < columnNames.Length; i++)
			{
				if (columnNames[i] is null)
					return (true, null);
			}

			return (false, columnNames);
		} // func IsXsdSchemaChange

		private (Excel.XmlMap map, string[] columnNames) CreateNewXmlMap(ListObject list, IDataColumns columnInfo)
		{
			const string rootElementName = "data";

			// create new schema
			var (xSchema, columnNames) = GenerateXsd(columnInfo, rootElementName);

			// add to workbook
			var xlMaps = ThisAddIn.GetWorkbook(ThisAddIn.GetWorksheet(list.InnerObject)).XmlMaps;
			var map = xlMaps.Add(xSchema.ToString(SaveOptions.DisableFormatting), rootElementName);
			map.Name = FindSchemaMapName(xlMaps);

			return (map, columnNames);
		} // func CreateNewXmlMap

		internal static void RemoveXmlMap(Excel.XmlMap xlMapToRemove)
		{
			if (xlMapToRemove is null)
				return;

			try
			{
				xlMapToRemove.Delete();
			}
			catch (COMException e)
			{
				Debug.Print("[RemoveXmlMap] " + e.Message);
			}
		} // proc RemoveXmlMap

		private string FindSchemaMapName(Excel.XmlMaps maps)
		{
			var freeNumber = 1;

			for (var i = 1; i <= maps.Count; i++)
			{
				var m = schemaName.Match(maps[i].Name);
				if (m.Success)
				{
					var t = Int32.Parse(m.Groups["n"].Value);
					if (t >= freeNumber)
						freeNumber = t + 1;
				}
			}

			return "ppsnXsd" + freeNumber.ToString();
		} // func FindSchemaMapName

		public (Excel.XmlMap xlMap, string[] columnNames, bool isSchemaChanged, Excel.XmlMap xlMapToRemove) EnsureXmlMap(ListObject list, IDataColumns columnInfo = null)
		{
			var map = list.XmlMap;

			if (columnInfo == null) // read schema information only
			{
				// parse column names from schema
				var columnNames = XsdSchemaElements(map, list.Name).Select(x => x.Attribute("name").Value).ToArray();
				return (map, columnNames, false, null);
			}
			else if (map == null) // no schema exists, create a new one
			{
				var (mapNew, columnNames) = CreateNewXmlMap(list, columnInfo);
				return (mapNew, columnNames, true, null);
			}
			else // schema exists, check if outdated
			{
				var (isChanged, columnNames) = IsXsdSchemaChange(map, list.Name, columnInfo);
				if (isChanged)
				{
					var (mapNew, columnNamesNew) = CreateNewXmlMap(list, columnInfo);
					return (mapNew, columnNamesNew, true, map);
				}
				else
					return (map, columnNames, false, null);

			}
		} // proc EnsureXmlMap

		#endregion

		public PpsEnvironment Environment => environment;
		public string Select => select;
		public string Filter => filterExpr;
		public PpsDataOrderExpression[] Orders => orders;
		public PpsDataColumnExpression[] Columns => columns;

		#region -- TryParse -----------------------------------------------------------

		private static bool TryParseSchema(string schema, out XElement xSchema)
		{
			try
			{
				xSchema = XElement.Parse(schema);
				return true;
			}
			catch
			{
				xSchema = null;
				return false;
			}
		} // func TryParseSchema

		public static bool TryParse(Func<string, Uri, PpsEnvironment> findEnvironment, ListObject xlList, out PpsListMapping ppsMap)
			=> TryParse(findEnvironment, xlList?.XmlMap, out ppsMap);

		public static bool TryParse(Func<string, Uri, PpsEnvironment> findEnvironment, Excel.XmlMap xlMap, out PpsListMapping ppsMap)
		{
			ppsMap = null;
			if (xlMap == null
				|| xlMap.Schemas.Count != 1)
				return false;

			// schema 
			if (!TryParseSchema(xlMap.Schemas[1].XML, out var xSchema))
				return false;

			// get annotation
			var comment = xSchema.Element(xsdAnnotationName)?.Element(xsdDocumentationName)?.Value;
			if (String.IsNullOrEmpty(comment))
				return false;

			// parse content
			string viewId = null;
			PpsDataColumnExpression[] columns = null;
			string environmentName = null;
			Uri environmentUri = null;
			PpsDataOrderExpression[] orders = null;
			string filterExpr = null;

			foreach (var kv in XlProcs.GetLineProperties(comment))
			{
				switch (kv.Key)
				{
					case environmentNameTag:
						environmentName = kv.Value;
						break;
					case environmentUriTag:
						environmentUri = new Uri(kv.Value, UriKind.Absolute);
						break;
					case filterTag:
						filterExpr = kv.Value;
						break;
					case viewTag:
						viewId = kv.Value;
						break;
					case columnsTag:
						columns = PpsDataColumnExpression.Parse(kv.Value).ToArray();
						break;
					case ordersTag:
						orders = PpsDataOrderExpression.Parse(kv.Value).ToArray();
						break;
				}
			}

			var env = findEnvironment(environmentName, environmentUri);
			if (env == null)
				return false;

			if (String.IsNullOrEmpty(viewId))
				return false;

			ppsMap = new PpsListMapping(env, viewId, columns, filterExpr, orders);
			return true;
		} // func TryParse

		#endregion
	} // class PpsListMapping

	#endregion

	#region -- class PpsListObject ----------------------------------------------------

	internal sealed class PpsListObject
	{
		private readonly ListObject xlList; // attached excel list object
		private readonly PpsListMapping map;

		#region -- Ctor ---------------------------------------------------------------

		private PpsListObject(ListObject xlList, PpsListMapping info)
		{
			this.xlList = xlList;
			this.map = info;
		} // ctor

		#endregion

		#region -- GetViewData---------------------------------------------------------

		private static Task<IEnumerator<IDataRow>> GetViewDataAsync(PpsListMapping info, Worksheet worksheet, bool headerOnly)
		{
			var syncContext = SynchronizationContext.Current;
			return Task.Run(() => info.GetViewData(worksheet, syncContext, headerOnly));
		} // func GetViewDataAsync

		#endregion

		#region -- ImportLayout -------------------------------------------------------

		private void ImportLayoutUpdateColumn(Excel.XmlMap xlMap, Excel.ListColumn listColumn, IDataColumn column, string columnName, bool styleUpdate, ref bool showTotals)
		{
			// set caption
			listColumn.Name = column.Attributes.GetProperty("displayName", columnName);

			// update range
			XlConverter.UpdateRange(listColumn.Range, column.DataType, column.Attributes, styleUpdate);

			// set totals calculation
			if (styleUpdate)
			{
				var totalsCalculation = XlConverter.ConvertToTotalsCalculation(column.Attributes.GetProperty("bi.totals", String.Empty));
				listColumn.TotalsCalculation = totalsCalculation;
				if (totalsCalculation != Excel.XlTotalsCalculation.xlTotalsCalculationNone)
					showTotals = true;
			}

			try
			{
				var columnSelector = "/" + xlMap.RootElementName + "/r/" + columnName;
				if (listColumn.XPath.Value != null)
				{
					if (listColumn.XPath.Value != columnSelector)
					{
						listColumn.XPath.Clear();
						listColumn.XPath.SetValue(xlMap, columnSelector);
					}
				}
				else
					listColumn.XPath.SetValue(xlMap, columnSelector);
			}
			catch (COMException e) when (e.HResult == unchecked((int)0x800A03EC))
			{
				Mapping.Environment.Await(Mapping.Environment.ShowMessageAsync(String.Format("Spaltenzuordnung von '{0}' ist fehlgeschlagen.", columnName)));
			}
		} // proc ImportLayoutUpdateColumn

		private static string GetColumnNameFromXPath(string xPath)
		{
			if (String.IsNullOrEmpty(xPath))
				return null;

			var p = xPath.LastIndexOf('/');
			if (p == -1)
				return null;

			return xPath.Substring(p + 1);
		} // func GetColumnNameFromXPath

		private void ImportLayoutRefresh(IDataColumns columnInfo, ListObject xlList, out bool showTotals)
		{
			var (xlMap, columnNames, isChanged, xlMapToRemove) = Mapping.EnsureXmlMap(xlList, columnInfo);

			// import layout
			showTotals = false;

			for (var i = 0; i < columnInfo.Columns.Count; i++)
			{
				var listColumn = i < List.ListColumns.Count ? xlList.ListColumns[i + 1] : xlList.ListColumns.Add();

				// clear cell format -> XPath values
				listColumn.Range.NumberFormat = "General";

				ImportLayoutUpdateColumn(xlMap, listColumn, columnInfo.Columns[i], columnNames[i], true, ref showTotals);
			}

			// remove mapping
			PpsListMapping.RemoveXmlMap(xlMapToRemove);
		} // func ImportLayoutRefresh

		private void ImportLayoutByColumn(IDataColumns columnInfo, ListObject xlList, Excel.XmlMap xlMap, string[] columnNames)
		{
			var showTotals = false;
			for (var i = 1; i <= xlList.ListColumns.Count; i++)
			{
				var listColumn = xlList.ListColumns[i];

				var columnName = GetColumnNameFromXPath(listColumn.XPath.Value);

				var idx = String.IsNullOrEmpty(columnName) ? -1 : Array.FindIndex(columnNames, c => c == columnName);
				if (idx == -1) // clear xpath
					listColumn.XPath.Clear();
				else // update format
					ImportLayoutUpdateColumn(xlMap, listColumn, columnInfo.Columns[idx], columnNames[idx], false, ref showTotals);
			}
		} // proc ImportLayoutByColumn

		#endregion

		#region -- ImportDataAsync ----------------------------------------------------

		private async Task ImportDataAsync(IEnumerator<IDataRow> e, bool checkXmlMapping)
		{
			using (var tw = new StringWriter(CultureInfo.InvariantCulture))
			using (var x = XmlWriter.Create(tw, new XmlWriterSettings() { Encoding = Encoding.Default, NewLineHandling = NewLineHandling.None }))
			{
				var (xlMap, columnNames, isChanged, xlMapToRemove) = map.EnsureXmlMap(xlList, checkXmlMapping ? (IDataColumns)e : null);
				if (isChanged)
				{
					ImportLayoutByColumn((IDataColumns)e, xlList, xlMap, columnNames);
					PpsListMapping.RemoveXmlMap(xlMapToRemove);
				}

				#region -- Fetch data --

				await Task.Run(() =>
				{
					x.WriteStartElement(xlMap.RootElementName);
					while (e.MoveNext())
					{
						var r = e.Current;
						x.WriteStartElement("r");

						for (var i = 0; i < columnNames.Length; i++)
						{
							if (r[i] != null)
							{
								x.WriteStartElement(columnNames[i]);
								x.WriteValue(r[i]);
								x.WriteEndElement();
							}
						}

						x.WriteEndElement();

					}

					x.WriteEndElement();
				});

				#endregion

				x.Flush();
				tw.Flush();

				// import data
				var xmlData = tw.GetStringBuilder().ToString();
#if DEBUG
				File.WriteAllText(@"D:\temp\xl.xml", xmlData);
				File.WriteAllText(@"D:\temp\xl.xsd", xlMap.Schemas[1].XML);
#endif

				switch (xlMap.ImportXml(xmlData, true))
				{
					case Excel.XlXmlImportResult.xlXmlImportElementsTruncated:
						throw new ExcelException("Zu viele Element, nicht alle Zeilen wurden geladen.");
					case Excel.XlXmlImportResult.xlXmlImportValidationFailed:
						throw new ExcelException("Validierung der Rohdaten fehlgeschlagen.");
				}
			}
		} // func ImportDataAsync

		#endregion

		public void ClearData()
		{
			//xlList.DataBodyRange.Clear();
		} // proc ClearData

		#region -- Refresh ------------------------------------------------------------

		public async Task RefreshAsync(bool refreshLayout)
		{
			var showTotals = false;
			var worksheet = ThisAddIn.GetWorksheet(xlList.InnerObject);
			using (var e = await GetViewDataAsync(map, worksheet, false))
			{
				if (refreshLayout)
				{
					ClearData();
					ImportLayoutRefresh((IDataColumns)e, xlList, out showTotals);
				}

				// import data
				await ImportDataAsync(e, !refreshLayout);

				if (refreshLayout && showTotals)
					xlList.ShowTotals = true;
			}
		} // func Refresh

		#endregion

		public ListObject List => xlList;
		public PpsListMapping Mapping => map;

		#region -- CreateAsync --------------------------------------------------------

		public static async Task<PpsListObject> CreateAsync(Excel.Range topLeftCell, PpsListMapping info)
		{
			// create list object and add header
			using (var e = await GetViewDataAsync(info, ThisAddIn.GetWorksheet(topLeftCell), false))
			{
				// Initialize ListObject
				var xlList = Globals.Factory.GetVstoObject((Excel.ListObject)topLeftCell.Worksheet.ListObjects.Add());
				var ppsList = new PpsListObject(xlList, info);

				// Create style
				ppsList.ImportLayoutRefresh((IDataColumns)e, xlList, out var showTotals);

				// Load data
				await ppsList.ImportDataAsync(e, false);

				// show totals
				if (showTotals)
					xlList.ShowTotals = showTotals;

				return ppsList;
			}
		} // func InsertListObject

		#endregion

		#region -- TryGet -------------------------------------------------------------

		public static bool TryGet(Func<string, Uri, PpsEnvironment> findEnvironment, ListObject xlList, out PpsListObject ppsList)
		{
			if (PpsListMapping.TryParse(findEnvironment, xlList, out var info))
			{
				ppsList = new PpsListObject(xlList, info);
				return true;
			}
			else
			{
				ppsList = null;
				return false;
			}
		} // func TryGet

		#endregion
	} // class PpsListObject

	#endregion
}
