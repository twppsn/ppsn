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
using System.IO;
using System.Linq;
using System.Net;
using System.Xml.Linq;
using ICSharpCode.SharpZipLib.Zip;
using Neo.IronLua;
using TecWare.DE.Data;
using TecWare.DE.Networking;
using TecWare.DE.Server;
using TecWare.DE.Server.Http;
using TecWare.DE.Stuff;
using TecWare.PPSn.Server.Data;

namespace TecWare.PPSn.Server.Wpf
{
	#region -- PpsExcelReportItem -----------------------------------------------------

	/// <summary>Excel Report item.</summary>
	public sealed class PpsExcelReportItem
	{
		internal PpsExcelReportItem(string type, string reportId, string displayName, string description)
		{
			Type = type;
			ReportId = reportId;
			DisplayName = displayName;
			Description = description;
		} // ctor

		/// <summary>Type of the excel report item</summary>
		public string Type { get; }
		/// <summary>Id of the report.</summary>
		public string ReportId { get; }
		/// <summary>Display name for the report.</summary>
		public string DisplayName { get; }
		/// <summary>Description of the report</summary>
		public string Description { get; }
	} // class PpsExcelReportItem

	#endregion

	#region -- class DataWareHouseClientItem ------------------------------------------

	/// <summary>Excel specific services</summary>
	public class DataWareHouseClientItem : DEConfigItem
	{
		#region -- class XlsxCacheItem ------------------------------------------------

		private sealed class XlsxCacheItem
		{
			private static readonly XNamespace documentComment = XNamespace.Get("http://purl.org/dc/elements/1.1/");
			private static readonly XName xDocumentCommentTitle = documentComment + "title";
			private static readonly XName xDocumentCommentDescription = documentComment + "description";

			private readonly DateTime lastWriteTime;
			private readonly string displayName;
			private readonly string description;

			public XlsxCacheItem(DateTime lastWriteTime)
			{
				this.lastWriteTime = lastWriteTime;

				displayName = null;
				description = null;
			} // ctor

			private XlsxCacheItem(DateTime lastWriteTime, string displayName, string description)
			{
				this.lastWriteTime = lastWriteTime;
				this.displayName = displayName;
				this.description = description;
			} // ctor

			public DateTime Stamp => lastWriteTime;
			public string DisplayName => displayName;
			public string Description => description;
			public bool IsValid => displayName != null;

			public static XlsxCacheItem ParseXlsxInfo(FileInfo fi)
			{
				string displayName = null;
				string description = null;

				using (var src = fi.OpenRead())
				using (var zip = new ZipInputStream(src))
				{
					while (true)
					{
						var item = zip.GetNextEntry();
						if (item == null)
							break;
						else if (item.IsFile && String.Compare(item.Name, @"docProps/core.xml", StringComparison.OrdinalIgnoreCase) == 0)
						{
							using (var s = new WindowStream(zip, 0, item.Size, false, true))
							{
								var xDoc = XDocument.Load(s);
								displayName = xDoc.Root.Element(xDocumentCommentTitle)?.Value;
								description = xDoc.Root.Element(xDocumentCommentDescription)?.Value;
							}
							break;
						}
					}
				}

				return new XlsxCacheItem(fi.LastWriteTime, displayName ?? fi.Name, description);
			} // func ParseXlsxInfo
		} // class XlsxCacheItem

		#endregion

		private readonly PpsApplication application;
		private DirectoryInfo xlsxTemplateDirectory = null;

		private readonly Dictionary<string, XlsxCacheItem> cachedXlsxReport = new Dictionary<string, XlsxCacheItem>();

		/// <summary></summary>
		/// <param name="sp"></param>
		/// <param name="name"></param>
		public DataWareHouseClientItem(IServiceProvider sp, string name)
			: base(sp, name)
		{
			application = sp.GetService<PpsApplication>(true);
		} // ctor

		/// <inheritdoc />
		protected override void OnEndReadConfiguration(IDEConfigLoading config)
		{
			base.OnEndReadConfiguration(config);

			xlsxTemplateDirectory = ConfigNode.GetAttribute<DirectoryInfo>("xlsxTemplates");
		} // proc OnEndReadConfiguration

		private bool TryParseInfo(FileInfo fi, out string displayName, out string description)
		{
			if (!cachedXlsxReport.TryGetValue(fi.Name, out var item) || item.Stamp < fi.LastWriteTime)
			{
				try
				{
					item = XlsxCacheItem.ParseXlsxInfo(fi);
				}
				catch (Exception e)
				{
					Log.Warn(e);
					item = new XlsxCacheItem(fi.LastWriteTime);
				}
				cachedXlsxReport[fi.Name] = item;
			}

			displayName = item.DisplayName;
			description = item.Description;
			return item.IsValid;
		} // proc TryParseInfo

		private IEnumerable<PpsExcelReportItem> GetExcelReportItems()
		{
			// return all visible views
			foreach (var v in application.GetViewDefinitions())
			{
				if (v.Attributes.GetProperty("bi.visible", false))
					yield return new PpsExcelReportItem("table", v.Name, v.DisplayName, v.Attributes.GetProperty("description", null));
			}

			// return all templates
			if (xlsxTemplateDirectory != null && xlsxTemplateDirectory.Exists)
			{
				foreach(var fi in xlsxTemplateDirectory.EnumerateFiles("*.*", SearchOption.TopDirectoryOnly))
				{
					if (String.Compare(fi.Extension, ".xlsx", StringComparison.OrdinalIgnoreCase) == 0
						|| String.Compare(fi.Extension, ".xlsm", StringComparison.OrdinalIgnoreCase) == 0)
					{
						if (TryParseInfo(fi, out var displayName, out var description))
							yield return new PpsExcelReportItem("xlsx", fi.Name, displayName, description);
					}
				}
			}
		} // func GetExcelReportItems

		private static XElement CreateParameterInfo(XName n, PpsViewParameterDefinition def)
		{
			return new XElement(n,
				new XAttribute("name", def.Name),
				Procs.XAttributeCreate("displayName", def.DisplayName, null)
			);
		} // func CreateParameterInfo

		private static XElement CreateFilterInfo(PpsViewParameterDefinition def)
			=> CreateParameterInfo("filter", def);

		private static XElement CreateOrderInfo(PpsViewParameterDefinition def)
			=> CreateParameterInfo("order", def);

		private static XElement CreateJoinInfo(PpsViewJoinDefinition def)
		{
			return new XElement("join",
				new XAttribute("view", def.ViewName),
				Procs.XAttributeCreate("alias", def.AliasName, null),
				Procs.XAttributeCreate("type", def.Type, PPSn.Data.PpsDataJoinType.Inner),
				Procs.XAttributeCreate("displayName", def.DisplayName, null)
			);
		} // func CreateJoinInfo

		private static XElement CreateColumnInfo(IDataColumn column, string attributeSelector)
		{
			var xColumn = new XElement("field",
				new XAttribute("name", column.Name),
				new XAttribute("type", LuaType.GetType(column.DataType).AliasOrFullName)
			);

			// filter attributes
			var desc = column is IPpsColumnDescription columnDescription ? columnDescription.GetColumnDescription<PpsFieldDescription>() : null;
			if (desc == null)
			{
				if (column.Attributes.TryGetProperty<string>("displayName", out var displayName))
					xColumn.Add(new PropertyValue("displayName", displayName).ToXml());
			}
			else
				xColumn.Add(from p in desc.GetAttributes(attributeSelector) where p.Value != null select p.ToXml());

			return xColumn;
		} // func CreateColumnInfo

		/// <summary>Return information for a view.</summary>
		/// <param name="v">view list to that will returned</param>
		/// <param name="a">Attribute selector</param>
		/// <returns></returns>
		[DEConfigHttpAction("tableinfo", IsSafeCall = true, SecurityToken = SecurityUser)]
		public XElement HttpViewInfoAction(string v, string a)
		{
			var xReturn = new XElement("return");

			foreach (var view in v.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
			{
				var viewInfo = application.GetViewDefinition(view, true);
				var xInfo = new XElement("table",
					new XAttribute("name", viewInfo.Name),
					new XAttribute("displayName", viewInfo.DisplayName)
				);

				// append properties
				if (viewInfo.Attributes is IPropertyEnumerableDictionary properties)
					xInfo.Add(from p in properties where p.Value != null select p.ToXml());

				// append filter
				xInfo.Add(viewInfo.Filter.Where(o => o.IsVisible).Select(CreateFilterInfo));

				// append order
				xInfo.Add(viewInfo.Order.Where(o => o.IsVisible).Select(CreateOrderInfo));

				// append joins
				xInfo.Add(viewInfo.Joins.Select(CreateJoinInfo));

				// apppend columns
				xInfo.Add(viewInfo.SelectorToken.Columns.Select(c => CreateColumnInfo(viewInfo.SelectorToken.GetFieldDescription(c.Name) ?? c, a)));

				xReturn.Add(xInfo);
			}

			return xReturn;
		} // func HttpViewInfoAction

		/// <summary>Returns xlsx-templates</summary>
		/// <param name="r"></param>
		/// <param name="id"></param>
		[DEConfigHttpAction("xlsxtmpl", IsSafeCall = false, SecurityToken = SecurityUser)]
		public void HttpXlsxTemplateAction(IDEWebRequestScope r, string id)
		{
			if (id.IndexOfAny(new char[] { '/', '\\' }) >= 0)
				throw new HttpResponseException(HttpStatusCode.BadRequest, "Invalid id.");
			if (xlsxTemplateDirectory == null || !xlsxTemplateDirectory.Exists)
				throw new HttpResponseException(HttpStatusCode.NotFound, "No templates.");

			var fi = new FileInfo(Path.Combine(xlsxTemplateDirectory.FullName, id));
			if (!fi.Exists)
				throw new HttpResponseException(HttpStatusCode.NotFound, $"{id} not found.");

			r.WriteFile(fi, MimeTypes.Application.OctetStream);
		} // proc HttpXlsxTemplateAction

		/// <summary>View to get all excel reports</summary>
		/// <param name="dataSource"></param>
		/// <returns></returns>
		public PpsDataSelector GetReportItemsSelector(PpsSysDataSource dataSource)
			=> new PpsGenericSelector<PpsExcelReportItem>(dataSource.SystemConnection, "bi.reports", application.CurrentViewsVersion, GetExcelReportItems());
	} // class DataWareHouseClientItem

	#endregion
}
