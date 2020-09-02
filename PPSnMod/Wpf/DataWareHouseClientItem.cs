﻿#region -- copyright --
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
using System.Linq;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Data;
using TecWare.DE.Server;
using TecWare.DE.Stuff;
using TecWare.PPSn.Server.Data;

namespace TecWare.PPSn.Server.Wpf
{
	/// <summary>Excel specific services</summary>
	public class DataWareHouseClientItem : DEConfigItem
	{
		private readonly PpsApplication application;

		/// <summary></summary>
		/// <param name="sp"></param>
		/// <param name="name"></param>
		public DataWareHouseClientItem(IServiceProvider sp, string name)
			: base(sp, name)
		{
			application = sp.GetService<PpsApplication>(true);
		} // ctor

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
		} // PpsExcelReportItem

		private IEnumerable<PpsExcelReportItem> GetExcelReportItems()
		{
			foreach (var v in application.GetViewDefinitions())
			{
				if (v.Attributes.GetProperty("bi.visible", false))
					yield return new PpsExcelReportItem("table", v.Name, v.DisplayName, v.Attributes.GetProperty("description", null));
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
		[DEConfigHttpAction("tableinfo", IsSafeCall = true)]
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

		/// <summary>View to get all excel reports</summary>
		/// <param name="dataSource"></param>
		/// <returns></returns>
		public PpsDataSelector GetReportItemsSelector(PpsSysDataSource dataSource)
			=> new PpsGenericSelector<PpsExcelReportItem>(dataSource.SystemConnection, "bi.reports", application.CurrentViewsVersion, GetExcelReportItems());

		#endregion
	} // class ExcelCientItem
}
