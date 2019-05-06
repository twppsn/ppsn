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
	#region -- class PpsXlColumnInfo --------------------------------------------------

	internal sealed class PpsXlColumnInfo
	{
		private readonly string selectColumnName;
		private readonly string resultColumnName;
		private readonly string xmlType;
		private readonly bool isNullable;

		public PpsXlColumnInfo(string selectColumnName, string resultColumnName, string xmlType, bool isNullable)
		{
			this.selectColumnName = selectColumnName;
			this.resultColumnName = resultColumnName;
			this.xmlType = xmlType;
			this.isNullable = isNullable;
		} // ctor

		public PpsDataColumnExpression ToColumnExpression()
			=> selectColumnName == null ? null : new PpsDataColumnExpression(selectColumnName);

		internal XElement GetXsd(XName elementName)
		{
			return new XElement(elementName,
				Procs.XAttributeCreate("id", selectColumnName),
				new XAttribute("name", resultColumnName),
				new XAttribute("type", xmlType),
				new XAttribute("minOccurs", isNullable ? 0 : 1),
				new XAttribute("maxOccurs", 1)
			);
		} // func GetXsd

		public string SelectColumnName => selectColumnName;
		public string ResultColumnName => resultColumnName;
		public string XmlType => xmlType;
		public bool IsNullable => isNullable;
	} // class PpsXlColumnInfo

	#endregion

	#region -- class PpsListMapping ---------------------------------------------------

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
		private const string viewTag = "view";

		private readonly PpsEnvironment environment;            // attached environment
		private readonly string select;                         // view or views
		private readonly string filterExpr;                     // uses placeholder for cells
		private PpsXlColumnInfo[] columns;                      // columns information

		#region -- Ctor/Dtor ----------------------------------------------------------

		public PpsListMapping(PpsEnvironment environment, string select, PpsXlColumnInfo[] columns, string filterExpr)
		{
			this.environment = environment ?? throw new ArgumentNullException(nameof(environment));
			this.select = select ?? throw new ArgumentNullException(nameof(select));
			this.filterExpr = filterExpr ?? String.Empty;
			this.columns = columns ?? Array.Empty<PpsXlColumnInfo>();
		} // ctor

		#endregion

		#region -- GetViewData --------------------------------------------------------

		private IPropertyReadOnlyDictionary GetVariables(Worksheet worksheet, SynchronizationContext context)
			=> null;

		internal IEnumerator<IDataRow> GetViewData(PpsDataOrderExpression[] order, Worksheet current, SynchronizationContext context = null, bool headerOnly = false)
		{
			var request = new PpsShellGetList(select)
			{
				Columns = columns.Select(c => c.ToColumnExpression()).Where(c => c != null).ToArray(),
				Filter = PpsDataFilterExpression.Parse(filterExpr, GetVariables(current, context)),
				Order = order
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

		private XElement GenerateXsd(IDataColumns columnInfo, string rootElementName)
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
				new KeyValuePair<string, string>(filterTag, filterExpr)
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

			var currentColumns = columns;
			var newColumns = new PpsXlColumnInfo[columnInfo.Columns.Count];
			for (var i = 0; i < newColumns.Length; i++)
			{
				var col = columnInfo.Columns[i]; // result
				var xsdType = namespaceShortcut + ":" + typeToXsdType[col.DataType];
				var isNullable = col.Attributes.TryGetProperty<bool>("nullable", out var tmp) && tmp;

				// try find source column
				newColumns[i] = new PpsXlColumnInfo(
					i < currentColumns.Length ? currentColumns[i].SelectColumnName : null,
					col.Name,
					xsdType,
					isNullable
				);

				// set schema
				xColumns.Add(newColumns[i].GetXsd(xsdElementName));
			}

			columns = newColumns;
			return xSchema;
		} // func GenerateXsd

		private static IEnumerable<XElement> XsdSchemaElements(XElement xSchema)
		{
			return xSchema.Element(xsdElementName) // data
				.Element(xsdComplexTypeName).Element(xsdSequenceName).Element(xsdElementName) // r
				.Element(xsdComplexTypeName).Element(xsdSequenceName).Elements(xsdElementName); // column
		} // func XsdSchemaElements

		private static IEnumerable<XElement> XsdSchemaElements(Excel.XmlMap map, string listName)
		{
			if (map == null || map.Schemas.Count != 1)
				throw new ArgumentException($"Schema information for list is invalid [{listName}].");
			if (!TryParseSchema(map.Schemas[1].XML, out var xSchema))
				throw new ArgumentException($"Format exception for schema information [{listName}]");

			// parse column names from schema
			return XsdSchemaElements(xSchema);
		} // func XsdSchemaElements

		private bool IsXsdSchemaChange(Excel.XmlMap map, string listName, IDataColumns columnInfo)
		{
			var newColumns = new PpsXlColumnInfo[columnInfo.Columns.Count];

			bool TestXsdType(string xsdType, Type netType)
				=> typeToXsdType.TryGetValue(netType, out var tmp) && tmp == xsdType;

			// test schema
			foreach (var x in XsdSchemaElements(map, listName))
			{
				var name = x.Attribute("name").Value;
				var type = x.Attribute("type").Value;
				var minOccures = x.GetAttribute("minOccurs", 1);

				var columnIndex = columnInfo.FindColumnIndex(name, false);
				if (columnIndex == -1)
					return true;
				else
				{
					var columnType = columnInfo.Columns[columnIndex].DataType;
					if (type.Length < 3 || !TestXsdType(type.Substring(3), columnType))
						return true;
					else
						newColumns[columnIndex] = new PpsXlColumnInfo(columnIndex < columns.Length ? columns[columnIndex].SelectColumnName : null, name, type, minOccures == 0);
				}
			}

			// touched all columns
			for (var i = 0; i < newColumns.Length; i++)
			{
				if (newColumns[i] is null)
					return true;
			}

			columns = newColumns;
			return false;
		} // func IsXsdSchemaChange

		private Excel.XmlMap CreateNewXmlMap(ListObject list, IDataColumns columnInfo)
		{
			const string rootElementName = "data";

			// create new schema
			var xSchema = GenerateXsd(columnInfo, rootElementName);

			// add to workbook
			var xlMaps = ThisAddIn.GetWorkbook(ThisAddIn.GetWorksheet(list.InnerObject)).XmlMaps;
			var map = xlMaps.Add(xSchema.ToString(SaveOptions.DisableFormatting), rootElementName);
			map.Name = FindSchemaMapName(xlMaps);

			return map;
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

		public (Excel.XmlMap xlMap, bool isSchemaChanged, Excel.XmlMap xlMapToRemove) EnsureXmlMap(ListObject list, IDataColumns columnInfo = null)
		{
			var map = list.XmlMap;

			if (columnInfo == null) // read schema information only
				return (map, false, null);
			else if (map == null) // no schema exists, create a new one
			{
				var mapNew = CreateNewXmlMap(list, columnInfo);
				return (mapNew, true, null);
			}
			else // schema exists, check if outdated
			{
				if (IsXsdSchemaChange(map, list.Name, columnInfo))
				{
					var mapNew = CreateNewXmlMap(list, columnInfo);
					return (mapNew, true, map);
				}
				else
					return (map, false, null);
			}
		} // proc EnsureXmlMap

		#endregion

		public PpsXlColumnInfo FindColumnFromXPath(string xPath)
		{
			if (xPath != null && columns != null)
			{
				foreach (var col in columns)
				{
					if (col.ResultColumnName != null && PpsListObject.IsXPathEqualColumnName(xPath, col.ResultColumnName))
						return col;
				}
			}
			return null;
		} // func FindColumnForOrderKey

		public PpsEnvironment Environment => environment;
		public string Select => select;
		public string Filter => filterExpr;
		public PpsXlColumnInfo[] Columns => columns;

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
			if (TryParse(xlMap, out var environmentName, out var environmentUri, out var viewId, out var filterExpr, out var columns))
			{
				// find environment
				var env = findEnvironment(environmentName, environmentUri);
				if (env == null)
				{
					ppsMap = null;
					return false;
				}

				// create mapping
				ppsMap = new PpsListMapping(env, viewId, columns, filterExpr);
				return true;
			}
			else
			{
				ppsMap = null;
				return false;
			}
		} // func TryParse

		public static bool TryParse(Excel.XmlMap xlMap, out string environmentName, out Uri environmentUri, out string viewId, out string filterExpr, out PpsXlColumnInfo[] columns)
		{
			environmentName = null;
			environmentUri = null;
			viewId = null;
			filterExpr = null;
			columns = null;

			if (xlMap == null || xlMap.Schemas.Count != 1)
				return false;

			// schema 
			if (!TryParseSchema(xlMap.Schemas[1].XML, out var xSchema))
				return false;

			// get annotation
			var comment = xSchema.Element(xsdAnnotationName)?.Element(xsdDocumentationName)?.Value;
			if (String.IsNullOrEmpty(comment))
				return false;

			// parse content
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
				}
			}

			// parse column information
			columns = (from xCol in XsdSchemaElements(xSchema)
					   let id = xCol?.Attribute("id")?.Value
					   let name = xCol?.Attribute("name")?.Value
					   let type = xCol?.Attribute("type")?.Value
					   let minOccurs = xCol.GetAttribute("minOccurs", 1)
					   where name != null
					   select new PpsXlColumnInfo(id, name, type, minOccurs == 0)
			).ToArray();

			return environmentUri != null && !String.IsNullOrEmpty(viewId);
		} // func TryParse

		public static bool TryParseFromSelection(out PpsListMapping ppsMap)
		{
			if (Globals.ThisAddIn.Application.Selection is Excel.Range range && !(range.ListObject is null))
			{
				var xlList = Globals.Factory.GetVstoObject(range.ListObject);
				return TryParse(Globals.ThisAddIn.FindEnvironment, xlList.XmlMap, out ppsMap);
			}
			else
			{
				ppsMap = null;
				return false;
			}
		} // func TryParseFromSelection

		public static bool TryParseFromSelection()
		{
			if (Globals.ThisAddIn.Application.Selection is Excel.Range range && !(range.ListObject is null))
			{
				var xlList = Globals.Factory.GetVstoObject(range.ListObject);
				return xlList.XmlMap.Schemas.Count >= 1;
			}
			else
				return false;
		} // func TryParseFromSelection

		#endregion
	} // class PpsListMapping

	#endregion

	#region -- class PpsListObject ----------------------------------------------------

	internal sealed class PpsListObject : IPpsTableData
	{
		private readonly ListObject xlList; // attached excel list object
		private PpsListMapping map;

		#region -- Ctor ---------------------------------------------------------------

		private PpsListObject(ListObject xlList, PpsListMapping map)
		{
			this.xlList = xlList ?? throw new ArgumentNullException(nameof(xlList));
			this.map = map ?? throw new ArgumentNullException(nameof(map));
		} // ctor

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

		private void ImportLayoutRefresh(IDataColumns columnInfo, ListObject xlList, out bool showTotals)
		{
			var (xlMap, isChanged, xlMapToRemove) = Mapping.EnsureXmlMap(xlList, columnInfo);

			// import layout
			showTotals = false;
			// remove mapping
			PpsListMapping.RemoveXmlMap(xlMapToRemove);

			for (var i = 0; i < columnInfo.Columns.Count; i++)
			{
				var listColumn = i < List.ListColumns.Count ? xlList.ListColumns[i + 1] : xlList.ListColumns.Add();

				// clear cell format -> XPath values
				listColumn.Range.NumberFormat = "General";

				ImportLayoutUpdateColumn(xlMap, listColumn, columnInfo.Columns[i], map.Columns[i].ResultColumnName, true, ref showTotals);
			}
		} // func ImportLayoutRefresh

		private void ImportLayoutByColumn(IDataColumns columnInfo, ListObject xlList, Excel.XmlMap xlMap)
		{
			var showTotals = false;
			for (var i = 1; i <= xlList.ListColumns.Count; i++)
			{
				var listColumn = xlList.ListColumns[i];

				var col = map.FindColumnFromXPath(listColumn.XPath.Value);
				if (col == null) // clear xpath
					listColumn.XPath.Clear();
				else // update format
				{
					var idx = columnInfo.FindColumnIndex(col.ResultColumnName);
					ImportLayoutUpdateColumn(xlMap, listColumn, columnInfo.Columns[idx], col.ResultColumnName, false, ref showTotals);
				}
			}
		} // proc ImportLayoutByColumn

		#endregion

		#region -- ImportDataAsync ----------------------------------------------------

		private async Task<bool> ImportDataBlockAsync(IEnumerator<IDataRow> e, Excel.XmlMap xlMap, int blockIndex, int blockSize)
		{
			var moveNext = false;

			using (var tw = new StringWriter(CultureInfo.InvariantCulture))
			using (var x = XmlWriter.Create(tw, new XmlWriterSettings() { Encoding = Encoding.Default, NewLineHandling = NewLineHandling.None }))
			{
				#region -- Fetch data --

				await Task.Run(() =>
				{
					x.WriteStartElement(xlMap.RootElementName);
					while ((moveNext = e.MoveNext()) && blockSize-- > 0)
					{
						var r = e.Current;
						x.WriteStartElement("r");

						for (var i = 0; i < map.Columns.Length; i++)
						{
							if (r[i] != null)
							{
								x.WriteStartElement(map.Columns[i].ResultColumnName);
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
				switch (xlMap.ImportXml(xmlData, blockIndex == 0))
				{
					case Excel.XlXmlImportResult.xlXmlImportElementsTruncated:
						throw new ExcelException("Zu viele Element, nicht alle Zeilen wurden geladen.");
					case Excel.XlXmlImportResult.xlXmlImportValidationFailed:
						throw new ExcelException("Validierung der Rohdaten fehlgeschlagen.");
				}

				return moveNext;
			}
		} // func ImportDataBlockAsync

		private async Task ImportDataAsync(IEnumerator<IDataRow> e, bool checkXmlMapping, bool singleLineMode)
		{
			var (xlMap, isChanged, xlMapToRemove) = map.EnsureXmlMap(xlList, checkXmlMapping ? (IDataColumns)e : null);
			if (isChanged)
			{
				ImportLayoutByColumn((IDataColumns)e, xlList, xlMap);
				PpsListMapping.RemoveXmlMap(xlMapToRemove);
			}

			var errorCheckingOptions = xlList.Application.ErrorCheckingOptions;
			var oldNumberAsTextValue = errorCheckingOptions.NumberAsText;
			var oldTextDateValue = errorCheckingOptions.TextDate;
			errorCheckingOptions.NumberAsText = false;
			errorCheckingOptions.TextDate = false;
			try
			{
				var blockSize = singleLineMode ? 1 : (64 << 10);
				var blockIndex = 0;
				while (await ImportDataBlockAsync(e, xlMap, blockIndex, blockSize))
					blockIndex++;
			}
			finally
			{
				errorCheckingOptions.NumberAsText = oldNumberAsTextValue;
				errorCheckingOptions.TextDate = oldTextDateValue;
			}

			//// disable alerts
			//	var dataRange = xlList.DataBodyRange;
			//for (var y = 1; y <= dataRange.Rows.Count; y++)
			//{
			//	for (var x = 1; x <= dataRange.Columns.Count; x++)
			//	{
			//		var range = (Excel.Range)dataRange.Cells[y, x];
			//		range.Errors[Excel.XlErrorChecks.xlNumberAsText].Ignore = true;
			//		range.Errors[Excel.XlErrorChecks.xlTextDate].Ignore = true;
			//	}
			//}
		} // func ImportDataAsync

		#endregion

		public void ClearData()
		{
			//xlList.DataBodyRange.Clear();
		} // proc ClearData

		#region -- Refresh ------------------------------------------------------------

		internal static bool IsXPathEqualColumnName(string xPath, string columnName)
		{
			if (String.IsNullOrEmpty(xPath) || String.IsNullOrEmpty(columnName))
				return false;

			var ofs = xPath.Length - columnName.Length;
			return ofs >= 1 && xPath[ofs - 1] == '/' && String.Compare(xPath, ofs, columnName, 0, columnName.Length, StringComparison.OrdinalIgnoreCase) == 0;
		} // func IsXPathEqualColumnName

		private bool? IsColumnSortedAscending(PpsXlColumnInfo columnInfo)
		{
			for (var i = 1; i <= xlList.Sort.SortFields.Count; i++)
			{
				var f = xlList.Sort.SortFields[i];
				var column = f.Key.Column;
				if (column >= 1 && column <= xlList.ListColumns.Count)
				{
					var path = xlList.ListColumns[column].XPath;
					if (path != null && IsXPathEqualColumnName(path.Value, columnInfo.ResultColumnName))
						return f.Order == Excel.XlSortOrder.xlAscending;
				}
			}
			return null;
		} // func IsColumnSortedAscending

		private Excel.ListColumn FindListColumnByName(string columnName)
		{
			for (var i = 1; i <= xlList.ListColumns.Count; i++)
			{
				var col = xlList.ListColumns[i];
				var path = col.XPath;
				if (path != null && IsXPathEqualColumnName(path.Value, columnName))
					return col;
			}
			return null;
		} // func FindListColumnByName

		private (PpsDataOrderExpression[], bool) CompareOrder(PpsDataOrderExpression[] order)
		{
			var isChanged = false;
			var newOrder = new List<PpsDataOrderExpression>();
			for (var i = 1; i <= xlList.Sort.SortFields.Count; i++)
			{
				var f = xlList.Sort.SortFields[i];
				var path = f.Key.XPath;
				if (path != null)
				{
					var col = map.FindColumnFromXPath(path.Value);
					if (col != null)
					{
						var s = new PpsDataOrderExpression(f.Order == Excel.XlSortOrder.xlDescending, col.SelectColumnName ?? col.ResultColumnName);
						if (order == null) // no known order
						{
							newOrder.Add(s);
							isChanged = true;
						}
						else // find current order
						{
							var oldOrder = order.FirstOrDefault(c => c.Equals(s));
							if (oldOrder == null)
							{
								newOrder.Add(s);
								isChanged = true;
							}
							else
								newOrder.Add(oldOrder);
						}
					}
				}
			}

			if (order != null)
			{
				foreach (var o in order)
				{
					if (!newOrder.Contains(o))
					{
						newOrder.Add(o);
						isChanged = true;
					}
				}
			}

			return (newOrder.ToArray(), isChanged);
		} // func CompareOrder

		private void UpdateSortOrder(PpsDataOrderExpression[] order)
		{
			var xlSort = xlList.Sort;

			// clear order
			while (xlSort.SortFields.Count > 0)
				xlSort.SortFields[1].Delete();

			// create new order
			if (order != null)
			{
				foreach (var o in order)
				{
					var columnInfo = map.Columns.FirstOrDefault(c => c.SelectColumnName == o.Identifier);
					if (columnInfo != null)
					{
						var col = FindListColumnByName(columnInfo.ResultColumnName);
						if (col != null)
							xlSort.SortFields.Add(col.DataBodyRange, Order: o.Negate ? Excel.XlSortOrder.xlDescending : Excel.XlSortOrder.xlAscending);
					}
				}
			}
		} // proc UpdateSortOrder

		public async Task RefreshAsync(bool refreshLayout, bool singleLineMode, PpsDataOrderExpression[] order)
		{
			var showTotals = false;
			var worksheet = ThisAddIn.GetWorksheet(xlList.InnerObject);
			var syncContext = SynchronizationContext.Current;

			var (newOrder, isOrderChanged) = CompareOrder(order);

			using (var e = await Task.Run(() => map.GetViewData(order ?? newOrder, worksheet, syncContext, false)))
			{
				if (refreshLayout)
				{
					ClearData();
					ImportLayoutRefresh((IDataColumns)e, xlList, out showTotals);
				}

				// update sort order
				if (order != null && isOrderChanged)
					UpdateSortOrder(newOrder);

				// import data
				await ImportDataAsync(e, !refreshLayout, singleLineMode);

				if (refreshLayout && showTotals)
					xlList.ShowTotals = true;
			}
			//List.Sort.SortFields.Item[0].Order
		} // func Refresh


		#endregion

		#region -- Editor -------------------------------------------------------------

		private sealed class PpsTableColumnInfo : IPpsTableColumn
		{
			private readonly PpsListObject parent;
			private readonly PpsXlColumnInfo columnInfo;

			public PpsTableColumnInfo(PpsListObject parent, PpsXlColumnInfo columnInfo)
			{
				this.parent = parent ?? throw new ArgumentNullException(nameof(parent));
				this.columnInfo = columnInfo ?? throw new ArgumentNullException(nameof(columnInfo));
			} // ctor

			public string Expression => columnInfo.SelectColumnName ?? columnInfo.ResultColumnName;
			public bool? Ascending => parent.IsColumnSortedAscending(columnInfo);
		} // class PpsTableColumnInfo

		public void Edit()
			=> map.Environment.EditTable(this);

		Task IPpsTableData.UpdateAsync(string views, string filter, IEnumerable<IPpsTableColumn> columns)
		{
			// create new mapping
			map = new PpsListMapping(map.Environment, views, columns?.Select(c => new PpsXlColumnInfo(c.Expression, null, null, true)).ToArray(), filter);
			return RefreshAsync(true, PpsMenu.IsSingleLineModeToggle(), columns.ToOrder().ToArray());
		} // proc UpdateAsync

		string IPpsTableData.DisplayName { get => xlList.DisplayName; set => xlList.DisplayName = value; }
		string IPpsTableData.Views => map.Select;
		string IPpsTableData.Filter => map.Filter;
		IEnumerable<IPpsTableColumn> IPpsTableData.Columns => map.Columns.Select(c => new PpsTableColumnInfo(this, c));
		bool IPpsTableData.IsEmpty => false;

		#endregion

		public ListObject List => xlList;
		public PpsListMapping Mapping => map;

		#region -- New ----------------------------------------------------------------

		private sealed class NewModel : IPpsTableData
		{
			private readonly PpsEnvironment environment;
			private readonly Excel.Range topLeftCell;

			public NewModel(PpsEnvironment environment, Excel.Range topLeftCell)
			{
				this.environment = environment ?? throw new ArgumentNullException(nameof(environment));
				this.topLeftCell = topLeftCell ?? throw new ArgumentNullException(nameof(topLeftCell));
			} // ctor

			public async Task UpdateAsync(string views, string filter, IEnumerable<IPpsTableColumn> columns)
			{
				// create a new mapping
				var newMapping = new PpsListMapping(environment,
					views,
					columns?.Select(c => new PpsXlColumnInfo(c.Expression, null, null, true)).ToArray(),
					filter
				);

				// Initialize ListObject
				var xlList = Globals.Factory.GetVstoObject((Excel.ListObject)topLeftCell.Worksheet.ListObjects.Add());
				var ppsList = new PpsListObject(xlList, newMapping);

				// Create table content
				await ppsList.RefreshAsync(true, PpsMenu.IsSingleLineModeToggle(), columns.ToOrder().ToArray());
			} // proc UpdateAsync

			public string DisplayName { get; set; } = null;

			public string Views => null;
			public string Filter => null;
			public IEnumerable<IPpsTableColumn> Columns => null;
			public bool IsEmpty => true;
		} // class NewModel 

		public static Task NewAsync(PpsEnvironment environment, Excel.Range topLeftCell, string views, string filter = null)
			=> new NewModel(environment, topLeftCell).UpdateAsync(views, filter, null);

		public static void New(PpsEnvironment env, Excel.Range topLeftCell)
			=> env.EditTable(new NewModel(env, topLeftCell));

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

		public static bool TryGetFromSelection(out PpsListObject ppsList)
		{
			if (Globals.ThisAddIn.Application.Selection is Excel.Range range && !(range.ListObject is null))
			{
				var xlList = Globals.Factory.GetVstoObject(range.ListObject);
				return TryGet(Globals.ThisAddIn.FindEnvironment, xlList, out ppsList);
			}
			else
			{
				ppsList = null;
				return false;
			}
		} // func TryGetFromSelection

		#endregion
	} // class PpsListObject

	#endregion
}
