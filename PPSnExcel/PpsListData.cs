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
using TecWare.PPSn.Core.Data;
using TecWare.PPSn.Data;
using Excel = Microsoft.Office.Interop.Excel;

namespace PPSnExcel
{
	#region -- enum PpsXlRefreshList --------------------------------------------------

	[Flags]
	internal enum PpsXlRefreshList
	{
		None,
		Style = 1,
		Columns = 2
	} // enum PpsXlRefreshList

	#endregion

	#region -- interface IPpsViewResult -----------------------------------------------

	internal interface IPpsViewResult : IDataColumns, IDisposable
	{
		bool PrepareMapping(Excel.ListObject list);

		Task<(bool, string)> GetNextBlockAsync(int blockSize);
		IDataColumn FindColumnFromExpression(string expression);

		Excel.XmlMap XlMapping { get; }
	} // interface IPpsViewResult

	#endregion

	#region -- class PpsListMapping ---------------------------------------------------

	/// <summary>Manage the xml-data for excel</summary>
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
			{ typeof(DateTimeOffset), "dateTime" },
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

		#region -- class PpsListColumnInfo --------------------------------------------

		private sealed class PpsListColumnInfo
		{
			private readonly string selectColumnName;
			private readonly string resultColumnName;
			private readonly string xmlType;
			private readonly bool isNullable;

			public PpsListColumnInfo(string selectColumnName, string resultColumnName, string xmlType, bool isNullable)
			{
				this.selectColumnName = selectColumnName ?? throw new ArgumentNullException(nameof(selectColumnName));
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

			/// <summary>Name for the select, if this value is empty. This result column is always <c>default</c> or <c>null</c>.</summary>
			public string SelectColumnExpression => selectColumnName;
			/// <summary>Column name in the schema</summary>
			public string ResultColumnName => resultColumnName;

			/// <summary>Xml-Type</summary>
			public string XmlType => xmlType;
			/// <summary>Is this column nullable</summary>
			public bool IsNullable => isNullable;
		} // class PpsListColumnInfo

		#endregion

		#region -- class PpsViewResult ------------------------------------------------

		private sealed class PpsViewResult : IPpsViewResult
		{
			private readonly PpsListMapping map;
			private readonly IEnumerator<IDataRow> enumerator;
			private readonly IReadOnlyList<IDataColumn> resultColumns; // columns return by the request

			private Excel.XmlMap xlMap = null;
			private int[] resultToXml = null;

			#region -- Ctor/Dtor ------------------------------------------------------

			internal PpsViewResult(PpsListMapping map, IEnumerable<IDataRow> enumerable)
			{
				this.map = map ?? throw new ArgumentNullException(nameof(map));

				if (enumerable == null)
					throw new ArgumentNullException(nameof(enumerable));

				enumerator = enumerable.GetEnumerator();
				try
				{
					resultColumns = ((IDataColumns)enumerator).Columns;

					if (resultColumns.Count == 0)
						throw new ExcelException("Ergebnismenge hat keine Spalten.");
				}
				catch
				{
					Dispose();
					throw;
				}
			} // ctor

			public void Dispose()
				=> enumerator.Dispose();

			#endregion

			#region -- Prepare Xsd Mapping --------------------------------------------

			public bool PrepareMapping(Excel.ListObject list)
			{
				// ensure XmlMap
				var xlMap = list.XmlMap;
				Excel.XmlMap newXlMap;
				Excel.XmlMap removeXlMap = null;
				bool isChanged;
				
				if (xlMap == null) // no schema exists, create a new one
				{
					newXlMap = map.CreateNewXmlMap(((Excel.Worksheet)list.Parent).Parent, this);
					isChanged = true;
				}
				else // schema exists, check if outdated
				{
					(isChanged, resultToXml) = map.IsXsdSchemaChange(xlMap, this);
					if (isChanged)
					{
						newXlMap = map.CreateNewXmlMap(((Excel.Worksheet)list.Parent).Parent, this);
						removeXlMap = xlMap;
						isChanged = true;
					}
					else
					{
						newXlMap = xlMap;
						isChanged = false;
					}
				}

				if (removeXlMap != null) // remove mapping
					RemoveXmlMap(removeXlMap);

				this.xlMap = newXlMap;

				return isChanged;
			} // func PrepareMapping

			#endregion

			#region -- GetNextBlockAsync ----------------------------------------------

			public async Task<(bool, string)> GetNextBlockAsync(int blockSize)
			{
				if (xlMap == null)
					throw new InvalidOperationException("Prepare not called.");

				var moveNext = false;

				using (var tw = new StringWriter(CultureInfo.InvariantCulture))
				using (var x = XmlWriter.Create(tw, new XmlWriterSettings() { Encoding = Encoding.Default, NewLineHandling = NewLineHandling.None }))
				{
					#region -- Fetch data --

					await Task.Run(() =>
					{
						x.WriteStartElement(xlMap.RootElementName);
						while ((moveNext = enumerator.MoveNext()) && blockSize-- > 0)
						{
							var r = enumerator.Current;
							x.WriteStartElement("r");

							var count = map.columns.Length;
							for (var i = 0; i < count; i++)
							{
								var v = r[resultToXml?[i] ?? i];
								if (v != null)
								{
									x.WriteStartElement(map.columns[i].ResultColumnName);
									x.WriteValue(v);
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
					return (moveNext, tw.GetStringBuilder().ToString());
				}
			} // func GetNextBlockAsync

			#endregion

			public IDataColumn FindColumnFromExpression(string expression)
			{
				var i = Array.FindIndex(map.columns, c => c.SelectColumnExpression == expression);
				return i >= 0 ? resultColumns[i] : null;
			} // func FindColumnFromExpression

			public IReadOnlyList<IDataColumn> Columns => resultColumns;
			public Excel.XmlMap XlMapping => xlMap;
		} // class PpsViewResult

		#endregion

		private static readonly Regex schemaName = new Regex(@"^ppsnXsd(?<n>\d+)$");

		private const string environmentNameTag = "env";
		private const string environmentUriTag = "uri";
		private const string filterTag = "filter";
		private const string viewTag = "view";

		private readonly IPpsShell shell;    // attached environment
		private readonly string viewId;                 // view or views
		private readonly string filterExpr;             // uses placeholder for cells
		private PpsListColumnInfo[] columns;			// columns information

		#region -- Ctor/Dtor ----------------------------------------------------------

		private PpsListMapping(IPpsShell shell, string select, PpsListColumnInfo[] columns, string filterExpr)
		{
			this.shell = shell ?? throw new ArgumentNullException(nameof(shell));
			this.viewId = select ?? throw new ArgumentNullException(nameof(select));
			this.filterExpr = filterExpr ?? String.Empty;
			this.columns = columns ?? Array.Empty<PpsListColumnInfo>();
		} // ctor

		#endregion

		#region -- GetViewData --------------------------------------------------------

		#region -- class WorksheetPropertyDictionary ----------------------------------

		private sealed class WorksheetPropertyDictionary : IPropertyReadOnlyDictionary
		{
			private static readonly Regex cellExpr = new Regex(@"((?<t>\w+)_)?(?<c>[A-Za-z]{1,3}\d{1,9})", RegexOptions.Singleline | RegexOptions.Compiled);
			private static readonly Regex cellExprRC = new Regex(@"((?<t>\w+)_)?R(?<r>\d{1,9})C(?<c>\d{1,9})", RegexOptions.Singleline | RegexOptions.Compiled);

			private readonly Worksheet worksheet;

			public WorksheetPropertyDictionary(Worksheet worksheet)
			{
				this.worksheet = worksheet ?? throw new ArgumentNullException(nameof(worksheet));
			} // ctor

			private bool TryGetName(Excel.Workbook workbook, string name, out object value)
			{
				for (var i = 1; i <= workbook.Names.Count; i++)
				{
					var n = workbook.Names.Item(i);
					if (String.Compare(n.Name, name, StringComparison.OrdinalIgnoreCase) == 0)
					{
						try
						{
							value = n.RefersToRange.Value;
							return true;
						}
						catch (COMException)
						{
							value = null;
							return false;
						}
					}
				}

				value = null;
				return false;
			} // func TryGetName

			private Worksheet GetWorksheet(Excel.Workbook workbook, string sheetName)
			{
				if (String.IsNullOrEmpty(sheetName))
					return worksheet;

				for (var i = 1; i <= workbook.Sheets.Count; i++)
				{
					if (workbook.Sheets[i] is Excel.Worksheet wks && String.Compare(wks.Name, sheetName, StringComparison.OrdinalIgnoreCase) == 0)
						return Globals.Factory.GetVstoObject(wks);
				}

				throw new ArgumentException(String.Format("Could not find worksheet: {0}", sheetName));
			} // func GetWorksheet

			private bool TryGetCellA1(Worksheet worksheet, string cell, out object value)
			{
				try
				{
					value = (object)worksheet.Range[cell].Value;
					return true;
				}
				catch (COMException)
				{
					value = null;
					return false;
				}
			} // func TryGetCell

			private bool TryGetCellRC(Worksheet worksheet, int row, int col, out object value)
			{
				try
				{
					value = (object)worksheet.Cells[row, col].Value;
					return true;
				}
				catch (COMException)
				{
					value = null;
					return false;
				}
			} // func TryGetCell

			private bool TryGetCell(Excel.Workbook workbook, string name, out object value)
			{
				var m = cellExpr.Match(name); // test for A1
				if (m.Success)
					return TryGetCellA1(GetWorksheet(workbook, m.Groups["t"].Value), m.Groups["c"].Value, out value);
				m = cellExprRC.Match(name);
				if (m.Success)
					return TryGetCellRC(GetWorksheet(workbook, m.Groups["t"].Value), Int32.Parse(m.Groups["r"].Value), Int32.Parse(m.Groups["c"].Value), out value);

				value = null;
				return false;
			} // func TryGetCell

			public bool TryGetProperty(string name, out object value)
			{
				// lookup name manager
				var workbook = (Excel.Workbook)worksheet.InnerObject.Parent;

				// search for the name
				if (TryGetName(workbook, name, out value))
					return true;

				// search cell value
				if (TryGetCell(workbook, name, out value))
					return true;

				value = null;
				return false;
			} // func TryGetProperty
		} // class WorksheetPropertyDictionary

		#endregion

		private IPropertyReadOnlyDictionary GetVariables(Worksheet worksheet, SynchronizationContext context)
			=> new WorksheetPropertyDictionary(worksheet);

		internal IPpsViewResult GetViewData(PpsDataOrderExpression[] order, Worksheet current, SynchronizationContext context = null)
		{
			var request = new PpsDataQuery(viewId)
			{
				Columns = columns.Select(c => c.ToColumnExpression()).Where(c => c != null).ToArray(),
				Filter = PpsDataFilterExpression.Parse(filterExpr, CultureInfo.InvariantCulture, PpsDataFilterParseOption.AllowFields | PpsDataFilterParseOption.AllowVariables).Reduce(GetVariables(current, context)),
				Order = order,
				AttributeSelector = "*,V.*,Xl.*"
			};

			return new PpsViewResult(this, shell.GetViewData(request));
		} // func GetViewData

		#endregion

		#region -- Excel Xml Map ------------------------------------------------------

		/// <summary>This function creates a new XmlMap for a server column set</summary>
		/// <param name="columnInfo"></param>
		/// <param name="rootElementName"></param>
		/// <returns></returns>
		private XElement GenerateXsd(IDataColumns columnInfo, string rootElementName)
		{
			const string namespaceShortcut = "xs";

			if (columnInfo == null)
				throw new ExcelException("Ergebnismenge hat keine Spalteninformation.");
			if (columnInfo.Columns.Count == 0)
				throw new ExcelException("Ergebnismenge hat keine Spalten.");

			var annotation = PpsWinShell.UpdateProperties(String.Empty,
				new KeyValuePair<string, string>(environmentNameTag, shell.Info.Name),
				new KeyValuePair<string, string>(environmentUriTag, shell.Info.Uri.ToString()),
				new KeyValuePair<string, string>(viewTag, viewId),
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
			var newColumns = new PpsListColumnInfo[columnInfo.Columns.Count];
			for (var i = 0; i < newColumns.Length; i++)
			{
				var col = columnInfo.Columns[i]; // result
				var xsdType = namespaceShortcut + ":" + typeToXsdType[col.DataType];
				var isNullable = col.GetIsNullable();

				// try find source column
				newColumns[i] = new PpsListColumnInfo(
					i < currentColumns.Length ? currentColumns[i].SelectColumnExpression : null,
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

		private (bool isChanged, int[] resultToXml) IsXsdSchemaChange(Excel.XmlMap map, IDataColumns columnInfo)
		{
			var newColumns = new PpsListColumnInfo[columnInfo.Columns.Count];
			var mapping = new int[columnInfo.Columns.Count];

			bool TestXsdType(string xsdType, Type netType)
				=> typeToXsdType.TryGetValue(netType, out var tmp) && tmp == xsdType;

			// test base params
			if (!TryParse(map, out _, out var shelltUri, out var currentViewId, out var currentFilterExpr, out var currentColumns))
				return (false, null);

			if (Uri.Compare(shelltUri, shell.Http.BaseAddress, UriComponents.Scheme | UriComponents.Host | UriComponents.Port | UriComponents.Path, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) != 0)
				return (true, null);

			if (viewId != currentViewId)
				return (true, null);

			if (String.IsNullOrEmpty(currentFilterExpr))
				currentFilterExpr = currentFilterExpr ?? String.Empty;
			if (filterExpr != currentFilterExpr)
				return (true, null);

			// test schema
			for (var i = 0; i < currentColumns.Length; i++)
			{
				var col = currentColumns[i];
				var columnIndex = columnInfo.FindColumnIndex(col.ResultColumnName, false);
				if (columnIndex == -1)
					return (true, null);
				else
				{
					var column = columnInfo.Columns[columnIndex];
					var columnType = column.DataType;
					var isNewNullable = column.GetIsNullable();

					if (col.XmlType.Length < 3 || !TestXsdType(col.XmlType.Substring(3), columnType))
						return (true, null);
					else if (isNewNullable != col.IsNullable)
						return (true, null);
					else
					{
						mapping[i] = columnIndex;
						newColumns[i] = new PpsListColumnInfo(
							columnIndex < columns.Length ? columns[columnIndex].SelectColumnExpression : null,
							col.ResultColumnName,
							col.XmlType,
							isNewNullable
						);
					}
				}
			}

			// touched all columns
			for (var i = 0; i < newColumns.Length; i++)
			{
				if (newColumns[i] is null)
					return (true, null);
			}

			columns = newColumns;
			return (false, mapping);
		} // func IsXsdSchemaChange

		internal Excel.XmlMap CreateNewXmlMap(Excel.Workbook workbook, IDataColumns columnInfo)
		{
			const string rootElementName = "data";

			// create new schema
			var xSchema = GenerateXsd(columnInfo, rootElementName);

			// add to workbook
			var xlMaps = workbook.XmlMaps;
			var map = xlMaps.Add(xSchema.ToString(SaveOptions.DisableFormatting), rootElementName);
			map.Name = FindSchemaMapName(xlMaps);

			return map;
		} // func CreateNewXmlMap

		private static void RemoveXmlMap(Excel.XmlMap xlMapToRemove)
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

		#endregion

		public string GetColumnExpressionFromXPath(string xPath)
		{
			if (xPath != null && columns != null)
			{
				for (var i = 0; i < columns.Length; i++)
				{
					var col = columns[i];
					if (col.ResultColumnName != null && PpsListObject.IsXPathEqualColumnName(xPath, col.ResultColumnName))
						return col.SelectColumnExpression;
				}
			}
			return null;
		} // func GetColumnExpressionFromXPath

		private PpsListColumnInfo GetColumnFromExpression(string expression)
		{
			if (columns != null)
			{
				for (var i = 0; i < columns.Length; i++)
				{
					var col = columns[i];
					if (String.Compare(col.SelectColumnExpression, expression, StringComparison.OrdinalIgnoreCase) == 0)
						return col;
				}
			}
			return null;
		} // func GetColumnFromExpression

		public IPpsShell Shell => shell;
		public string Select => viewId;
		public string Filter => filterExpr;

		public int ColumnCount => columns.Length;

		#region -- TryParse -----------------------------------------------------------

		public static PpsListMapping Create(IPpsShell shell, string views, string filter, IEnumerable<IPpsTableColumn> columns, PpsListMapping currentMapping)
		{
			return new PpsListMapping(shell, views,
				columns == null
					? Array.Empty<PpsListColumnInfo>()
					: (
						from col in columns
						where col.Type == PpsTableColumnType.Data
						let currentColumn = currentMapping?.GetColumnFromExpression(col.Expression)
						select new PpsListColumnInfo(col.Expression, currentColumn?.ResultColumnName, currentColumn?.XmlType, currentColumn?.IsNullable ?? true)
					).ToArray(),
				filter
			);
		} // func Create

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

		public static bool TryParse(Func<string, Uri, IPpsShell> findShell, ListObject xlList, out PpsListMapping ppsMap)
			=> TryParse(findShell, xlList?.XmlMap, out ppsMap);

		public static bool TryParse(Func<string, Uri, IPpsShell> findShell, Excel.XmlMap xlMap, out PpsListMapping ppsMap)
		{
			if (TryParse(xlMap, out var shellName, out var shellUri, out var viewId, out var filterExpr, out var columns))
			{
				// find environment
				var env = findShell(shellName, shellUri);
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

		public static bool TryParseComment(XElement xSchema, out string shellName, out Uri shellUri, out string viewId, out string filterExpr)
		{
			shellName = null;
			shellUri = null;
			viewId = null;
			filterExpr = null;

			// get annotation
			var comment = xSchema.Element(xsdAnnotationName)?.Element(xsdDocumentationName)?.Value;
			if (String.IsNullOrEmpty(comment))
				return false;

			// parse content
			foreach (var kv in PpsWinShell.GetLineProperties(comment))
			{
				switch (kv.Key)
				{
					case environmentNameTag:
						shellName = kv.Value;
						break;
					case environmentUriTag:
						shellUri = new Uri(kv.Value, UriKind.Absolute);
						break;
					case filterTag:
						filterExpr = kv.Value;
						break;
					case viewTag:
						viewId = kv.Value;
						break;
				}
			}
			return true;
		} // func TryParseComment

		public static void DebugInfo(Excel.XmlMap xlMap, StringBuilder sb)
		{
			if (TryParse(xlMap, out var environmentName, out var environmentUri, out var viewId, out var filterExpr, out var columns))
			{
				sb.AppendLine("Mapping found:");
				sb.AppendFormat("\tEnvironment: {0} ({1})", environmentName, environmentUri).AppendLine();
				sb.Append("\tView: ").Append(viewId).AppendLine();
				sb.Append("\tFilter: ").Append(filterExpr).AppendLine();
				sb.AppendLine();
				if (columns == null || columns.Length == 0)
					sb.Append("Column information not found.");
				else
				{
					var i = 0;
					foreach (var col in columns)
					{
						sb.AppendFormat("{0}: {1} | {2} [{3}]", ++i, col.SelectColumnExpression, col.ResultColumnName, col.XmlType);
						if (col.IsNullable)
							sb.Append(" OPT");
						sb.AppendLine();
					}
				}
			}
			else
			{
				sb.AppendLine("Could not parse Mapping.");
				sb.AppendLine();
				if (xlMap == null || xlMap.Schemas.Count == 0)
					sb.AppendLine("Xml-Schema is missing.");
				else
					sb.Append(xlMap.Schemas[1].XML);
			}
		} // func DebugInfo

		private static bool TryParse(Excel.XmlMap xlMap, out string environmentName, out Uri environmentUri, out string viewId, out string filterExpr, out PpsListColumnInfo[] columns)
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

			// parse header
			if (!TryParseComment(xSchema, out environmentName, out environmentUri, out viewId, out filterExpr))
				return false;

			// parse column information
			columns = (from xCol in XsdSchemaElements(xSchema)
					   let id = xCol?.Attribute("id")?.Value
					   let name = xCol?.Attribute("name")?.Value
					   let type = xCol?.Attribute("type")?.Value
					   let minOccurs = xCol.GetAttribute("minOccurs", 1)
					   where name != null
					   select new PpsListColumnInfo(id, name, type, minOccurs == 0)
			).ToArray();

			return environmentUri != null && !String.IsNullOrEmpty(viewId);
		} // func TryParse

		public static bool TryParseFromSelection(out PpsListMapping ppsMap)
		{
			if (Globals.ThisAddIn.Application.Selection is Excel.Range range && !(range.ListObject is null))
			{
				var xlList = Globals.Factory.GetVstoObject(range.ListObject);
				return TryParse(Globals.ThisAddIn.EnforceShell, xlList.XmlMap, out ppsMap);
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
		#region -- class PpsListColumnInfo --------------------------------------------

		private sealed class PpsListColumnInfo : IPpsTableColumn, IEquatable<IPpsTableColumn>
		{
			private readonly PpsListObject parent;
			private readonly Excel.ListColumn column;

			private readonly Lazy<bool?> ascendingValue;

			public PpsListColumnInfo(PpsListObject parent, Excel.ListColumn column)
			{
				this.parent = parent ?? throw new ArgumentNullException(nameof(parent));
				this.column = column ?? throw new ArgumentNullException(nameof(column));

				// set name and sort information
				ascendingValue = new Lazy<bool?>(() => this.parent.IsListColumnSortedAscending(this.column));
				
				// read field expression
				var xPath = column.XPath?.Value;
				if (!String.IsNullOrEmpty(xPath)) // check data field
				{
					Expression = parent.map.GetColumnExpressionFromXPath(xPath);
					Type = PpsTableColumnType.Data;
				}
				else if (column.DataBodyRange != null && column.DataBodyRange.HasFormula)
				{
					Type = PpsTableColumnType.Formula;
					Expression = column.Name + "=" + column.DataBodyRange.Cells[1, 1].Formula;
				}
				else
				{
					Type = PpsTableColumnType.User;
					Expression = column.Name;
				}
			} // ctor

			public bool Equals(IPpsTableColumn column)
				=> Equals(this, column);

			public static bool Equals(IPpsTableColumn a, IPpsTableColumn b)
				=> a.Type == b.Type && a.Expression == b.Expression;

			public string Name => column.Name;
			
			public PpsTableColumnType Type { get; }
			public string Expression { get; }
			
			public bool? Ascending => ascendingValue.Value;

			public Excel.ListColumn Column => column;

			public static PpsListColumnInfo Find(IEnumerable<PpsListColumnInfo> columnInfos, IPpsTableColumn column)
				=> columnInfos.FirstOrDefault(c => Equals(c, column));
		} // class PpsListColumnInfo

		#endregion

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

		private static bool ListColumnNameExists(string displayName, ListObject xlList, int ofs, int last)
		{
			for (var i = ofs; i <= last; i++)
			{
				if (String.Compare(xlList.ListColumns[i].Name, displayName, StringComparison.OrdinalIgnoreCase) == 0)
					return true;

			}
			return false;
		} // func ListColumnNameExists

		private static string GetUniqueListColumnName(string displayName, ListObject xlList, int ofs, int last)
		{
			if (ListColumnNameExists(displayName, xlList, ofs, last))
			{
				var c = 2;
				while (true)
				{
					var newDisplyName = displayName + "_" + c.ToString();
					if (ListColumnNameExists(newDisplyName, xlList, ofs, last))
						c++;
					else
						return newDisplyName;
				}
			}
			else
				return displayName;
		} // func GetUniqueListColumnName

		private static Excel.ListColumn MoveListColumn(Excel.ListColumn sourceColumn, int insertAt)
		{
			// add new column
			var list = (Excel.ListObject)sourceColumn.Parent;

			// move content to new column
			var targetColumn = list.ListColumns.Add(insertAt);
			sourceColumn.Range.Cut(targetColumn.Range);

			// remove source column
			sourceColumn.Delete();

			return targetColumn;
		} // proc MoveListColumn

		private static void RefreshColumnStyleLayout(Excel.ListColumn targetColumn, IDataColumn dataColumn, ref bool showTotals)
		{
			// update range
			XlConverter.UpdateRange(targetColumn.Range, dataColumn.DataType, dataColumn.Attributes);

			// set totals calculation
			var totalsCalculation = XlConverter.ConvertToTotalsCalculation(dataColumn.Attributes.GetProperty("bi.totals", String.Empty));
			targetColumn.TotalsCalculation = totalsCalculation;
			if (totalsCalculation != Excel.XlTotalsCalculation.xlTotalsCalculationNone)
				showTotals = true;
		} // proc RefreshColumnStyleLayout

		private bool RefreshColumnDataLayout(IPpsViewResult result, string columnExpression, Excel.ListColumn targetColumn, bool styleUpdate, ref bool showTotals)
		{
			var dataColumn = result.FindColumnFromExpression(columnExpression);
			if (dataColumn == null)
			{
				if (targetColumn.XPath != null)
					targetColumn.XPath.Clear(); // remove current binding
				return false;
			}

			var isXPathChanged = false;
			try
			{
				var columnSelector = "/" + result.XlMapping.RootElementName + "/r/" + dataColumn.Name;

				// clear used
				for (var i = targetColumn.Index + 1; i <= xlList.ListColumns.Count; i++)
				{
					if (xlList.ListColumns[i].XPath.Value == columnSelector)
						xlList.ListColumns[i].XPath.Clear();
				}

				// update currrent value
				if (targetColumn.XPath.Value != null)
				{
					if (targetColumn.XPath.Value != columnSelector)
					{
						targetColumn.XPath.Clear();
						targetColumn.XPath.SetValue(result.XlMapping, columnSelector);
						isXPathChanged = true;
					}
				}
				else
				{
					if (targetColumn.Range != null)
						targetColumn.Range.NumberFormat = "General";
					targetColumn.XPath.SetValue(result.XlMapping, columnSelector);
					isXPathChanged = true;
				}
			}
			catch (COMException e) when (e.HResult == unchecked((int)0x800A03EC))
			{
				return false;
			}

			if (styleUpdate || isXPathChanged)
				RefreshColumnStyleLayout(targetColumn, dataColumn, ref showTotals);

			return true;
		} // proc RefreshColumnDataLayout

		// rewrite layout
		private void RefreshLayout(IPpsViewResult result, IPpsTableColumn[] sourceColumns, PpsListColumnInfo[] currentColumns, PpsXlRefreshList refreshLayout, Excel.Sort xlSort, out bool showTotals)
		{
			var bindError = new StringBuilder("Spalten Zuordnung ist fehlgeschlagen:");
			var xPathError = 0;
			var xPathSet = 0;
			var styleUpdate = (refreshLayout & PpsXlRefreshList.Style) != 0;

			// import layout
			showTotals = false;

			// update all columns as requested in the columns array
			for (var i = 0; i < sourceColumns.Length; i++)
			{
				var sourceColumn = sourceColumns[i]; // column information to set
				var targetColumn = PpsListColumnInfo.Find(currentColumns, sourceColumn)?.Column; // map to current column

				if (targetColumn == null) // there is no column, insert column at this position
					targetColumn = xlList.ListColumns.Add(i + 1);
				else if (i + 1 != targetColumn.Index) // move column column
					targetColumn = MoveListColumn(targetColumn, i + 1);

				// update column title
				var title = sourceColumn.Name;
				for (var j = targetColumn.Index + 1; j <= xlList.ListColumns.Count; j++)
				{
					if (xlList.ListColumns[j].Name == title)
						xlList.ListColumns[j].Name = "n" + j.ToString();
				}

				targetColumn.Name = GetUniqueListColumnName(title, xlList, 1, i - 1);

				// check column content
				if (sourceColumn.Type == PpsTableColumnType.Data)
				{
					if (RefreshColumnDataLayout(result, sourceColumn.Expression, targetColumn, styleUpdate, ref showTotals))
						xPathSet++;
					else
					{
						bindError.AppendLine().Append($"- {sourceColumn.Expression} => {targetColumn.Name}");
						xPathError++;
					}
				}

				// update sort field
				if (xlSort != null && sourceColumn.Ascending.HasValue)
					xlSort.SortFields.Add(targetColumn.Range, Order: sourceColumn.Ascending.Value ? Excel.XlSortOrder.xlAscending : Excel.XlSortOrder.xlDescending);
			}

			// check for columns to delete
			var removeColumns = (refreshLayout & PpsXlRefreshList.Columns) != 0;
			var lastColumnIndex = sourceColumns.Length + 1;
			while (lastColumnIndex <= xlList.ListColumns.Count)
			{
				if (removeColumns) // remove whole column
					xlList.ListColumns[lastColumnIndex].Delete();
				else // remove only data connections
				{
					var col = xlList.ListColumns[lastColumnIndex];
					if (col.XPath != null)
						col.XPath.Clear();
					lastColumnIndex++;
				}
			}

			if (xPathError > 0)
				throw new ExcelException(bindError.ToString());
			else if (xPathSet == 0)
				throw new ExcelException("Es wurde keine Zuordnung getroffen.");
		} // func RefreshLayout

		private static void RefreshLayoutOnly(IPpsViewResult result, PpsListColumnInfo[] currentColumns, ref bool showTotals)
		{
			for (var i = 0; i < currentColumns.Length; i++)
			{
				var currentColumn = currentColumns[i];
				var dataColumn = result.FindColumnFromExpression(currentColumn.Expression);
				if (dataColumn != null)
					RefreshColumnStyleLayout(currentColumn.Column, dataColumn, ref showTotals);
			}
		} // proc RefreshLayoutOnly

		#endregion

		#region -- ClearData ----------------------------------------------------------

		public void ClearData()
		{
			xlList.DataBodyRange.Clear();
		} // proc ClearData

		#endregion

		#region -- ColumnInfo ---------------------------------------------------------

		private IEnumerable<PpsListColumnInfo> GetListColumnInfo()
		{
			for (var i = 1; i <= xlList.ListColumns.Count; i++)
				yield return new PpsListColumnInfo(this, xlList.ListColumns[i]);
		} // func GetListColumnInfo

		#endregion

		#region -- Refresh ------------------------------------------------------------

		internal static bool IsXPathEqualColumnName(string xPath, string columnName)
		{
			if (String.IsNullOrEmpty(xPath) || String.IsNullOrEmpty(columnName))
				return false;

			var ofs = xPath.Length - columnName.Length;
			return ofs >= 1 && xPath[ofs - 1] == '/' && String.Compare(xPath, ofs, columnName, 0, columnName.Length, StringComparison.OrdinalIgnoreCase) == 0;
		} // func IsXPathEqualColumnName

		private bool? IsListColumnSortedAscending(Excel.ListColumn column)
		{
			var sortFields = xlList.Sort.SortFields;

			for (var i = 1; i <= sortFields.Count; i++)
			{
				var sortField = sortFields[i];
				var columnIndex = sortField.Key.Column;
				if (columnIndex == column.Index)
					return sortField.Order == Excel.XlSortOrder.xlAscending;
			}

			return null;
		} // func IsListColumnSortedAscending

		private (bool isOrderChanged, PpsDataOrderExpression[] order) CompareOrder(PpsListColumnInfo[] currentColumns, IPpsTableColumn[] newColumns)
		{
			if (newColumns != null) // new column set provided
			{
				var isChanged = false;
				var j = 0;
				var dataOrderExpr = new List<PpsDataOrderExpression>();
				for (var i = 0; i < newColumns.Length; i++)
				{
					var col = newColumns[i];
					if (col.Ascending != null)
					{
						if (!isChanged)
						{
							// find next current sort column
							while (j < currentColumns.Length && currentColumns[j].Ascending == null)
								j++;

							// check if the columns are equal
							if (j >= currentColumns.Length ||
								!PpsListColumnInfo.Equals(col, currentColumns[j]) || col.Ascending != currentColumns[j].Ascending)
								isChanged = true;
						}

						if (col.Type == PpsTableColumnType.Data) // data column order
							dataOrderExpr.Add(new PpsDataOrderExpression(!col.Ascending.Value, col.Expression));
					}
				}

				return (isChanged, dataOrderExpr.ToArray());
			}
			else // get arguments from the current columns
			{
				return (
					false, 
					currentColumns
						.Where(c => c.Type == PpsTableColumnType.Data && c.Ascending.HasValue)
						.Select(c => new PpsDataOrderExpression(!c.Ascending.Value, c.Expression))
						.ToArray()
				);
			}
		} // proc CompareOrder

		public async Task RefreshAsync(PpsXlRefreshList refreshLayout, bool singleLineMode, IPpsTableColumn[] columns)
		{
			var showTotals = false;
			var worksheet = ThisAddIn.GetWorksheet(xlList.InnerObject);
			var syncContext = SynchronizationContext.Current;

			// get current column layout
			// before the xml mapping is changed, because it will clear all mappings
			var currentColumns = GetListColumnInfo().ToArray();

			var xlSort = xlList.Sort;
			var (isOrderChanged, order) = CompareOrder(currentColumns, columns);

			// remove filters if column set is changed
			if (columns != null && xlList.AutoFilter != null)
				xlList.AutoFilter.ShowAllData();

			using (var result = await Task.Run(() => map.GetViewData(order, worksheet, syncContext)))
			{
				// create a default layout from the current layout
				// this must be created add this place, because the change of the mapping will destroy all xpath-relations
				var isColumnSetChanged = columns != null;
				if (columns == null)
					columns = GetListColumnInfo().ToArray();

				// prepare data mapping
				var isChanged = result.PrepareMapping(xlList.InnerObject);
				if (isChanged || isColumnSetChanged)
				{
					// clear current order
					while (xlSort.SortFields.Count > 0)
						xlSort.SortFields[1].Delete();
					
					RefreshLayout(result, columns, currentColumns, refreshLayout, xlSort, out showTotals);
				}
				else
				{
					if ((refreshLayout & PpsXlRefreshList.Style) != PpsXlRefreshList.None)
						RefreshLayoutOnly(result, currentColumns, ref showTotals);
				}

				// import data
				var errorCheckingOptions = xlList.Application.ErrorCheckingOptions;
				var oldNumberAsTextValue = errorCheckingOptions.NumberAsText;
				var oldTextDateValue = errorCheckingOptions.TextDate;
				errorCheckingOptions.NumberAsText = false;
				errorCheckingOptions.TextDate = false;
				try
				{
					var blockSize = singleLineMode ? 1 : (64 << 10);
					var blockIndex = 0;
					var moveNext = true;
					while (moveNext)
					{
						string xmlData;
						(moveNext, xmlData) = await result.GetNextBlockAsync(blockSize);
						switch (result.XlMapping.ImportXml(xmlData, blockIndex == 0))
						{
							case Excel.XlXmlImportResult.xlXmlImportElementsTruncated:
								throw new ExcelException("Zu viele Element, nicht alle Zeilen wurden geladen.");
							case Excel.XlXmlImportResult.xlXmlImportValidationFailed:
								throw new ExcelException("Validierung der Rohdaten fehlgeschlagen.");
						}
						blockIndex++;
					}
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

				// refresh summary
				if ((refreshLayout & PpsXlRefreshList.Style) != 0 && showTotals)
					xlList.ShowTotals = true;
			}

			if (isOrderChanged)
				xlSort.Apply();
		} // func RefreshAsync

		#endregion

		#region -- Editor -------------------------------------------------------------

		public void Edit(bool extended)
			=> map.Shell.EditTable(this, extended);

		Task IPpsTableData.UpdateAsync(string views, string filter, IEnumerable<IPpsTableColumn> columns)
		{
			var columnArray = columns.ToArray();

			// create new mapping
			map = PpsListMapping.Create(map.Shell, views, filter, columnArray, map);
			using (var progress = PpsShell.Global.CreateProgress())
				return RefreshAsync(PpsXlRefreshList.Columns, PpsMenu.IsSingleLineModeToggle(), columnArray);
		} // proc UpdateAsync

		string IPpsTableData.DisplayName { get => xlList.DisplayName; set => xlList.DisplayName = value; }
		string IPpsTableData.Views => map.Select;
		string IPpsTableData.Filter => map.Filter;
		IEnumerable<IPpsTableColumn> IPpsTableData.Columns => GetListColumnInfo();

		bool IPpsTableData.IsEmpty => false;

		#endregion

		public ListObject List => xlList;
		public PpsListMapping Mapping => map;

		#region -- New ----------------------------------------------------------------

		private sealed class NewModel : IPpsTableData
		{
			private readonly IPpsShell shell;
			private readonly Excel.Range topLeftCell;

			public NewModel(IPpsShell shell, Excel.Range topLeftCell)
			{
				this.shell = shell ?? throw new ArgumentNullException(nameof(shell));
				this.topLeftCell = topLeftCell ?? throw new ArgumentNullException(nameof(topLeftCell));
			} // ctor

			public async Task UpdateAsync(string views, string filter, IEnumerable<IPpsTableColumn> columns)
			{
				var columnArray = columns.ToArray();

				// create a new mapping
				var newMapping = PpsListMapping.Create(shell, views, filter, columnArray, null);

				// Initialize ListObject
				var xlList = Globals.Factory.GetVstoObject(topLeftCell.ListObject ?? topLeftCell.Worksheet.ListObjects.Add());
				var ppsList = new PpsListObject(xlList, newMapping);

				// Create table content
				await ppsList.RefreshAsync(PpsXlRefreshList.Style | PpsXlRefreshList.Columns, PpsMenu.IsSingleLineModeToggle(), columnArray);
			} // proc UpdateAsync

			public string DisplayName { get; set; } = null;

			public string Views { get; set; } = null;
			public string Filter => null;
			public IEnumerable<IPpsTableColumn> Columns => null;
			public bool IsEmpty => true;
		} // class NewModel 

		public static void New(IPpsShell shell, Excel.Range topLeftCell, string views)
			=> shell.EditTable(new NewModel(shell, topLeftCell) { Views = views }, false);

		public static void New(IPpsShell shell, Excel.Range topLeftCell)
			=> shell.EditTable(new NewModel(shell, topLeftCell), false);

		#endregion

		#region -- TryGet -------------------------------------------------------------

		public static bool TryGet(Func<string, Uri, IPpsShell> findEnvironment, ListObject xlList, out PpsListObject ppsList)
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

		public static bool TryGetFromListObject(Excel.ListObject xlListObject, out PpsListObject ppsList)
		{
			if (xlListObject is null)
			{
				ppsList = null;
				return false;
			}
			else
			{
				var xlList = Globals.Factory.GetVstoObject(xlListObject);
				return TryGet(Globals.ThisAddIn.EnforceShell, xlList, out ppsList);
			}
		} // func TryGetFromListObject

		public static bool TryGetFromSelection(out PpsListObject ppsList)
		{
			if (Globals.ThisAddIn.Application.Selection is Excel.Range range  && TryGetFromListObject(range.ListObject, out ppsList))
				return true;
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
