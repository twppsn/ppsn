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
			this.application = sp.GetService<PpsApplication>(true);
		} // ctor

		#region -- PpsExcelReportItem -----------------------------------------------------

		/// <summary>Excel Report item.</summary>
		public sealed class PpsExcelReportItem
		{
			internal PpsExcelReportItem(string type, string reportId, string displayName, string description)
			{
				this.Type = type;
				this.ReportId = reportId;
				this.DisplayName = displayName;
				this.Description = description;
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

		private IEnumerable<PpsExcelReportItem> GetExcelReportItems(IPpsPrivateDataContext privateUserData)
		{
			foreach (var v in application.GetViewDefinitions())
			{
				if (v.Attributes.GetProperty("bi.visible", false))
					yield return new PpsExcelReportItem("table", v.Name, v.DisplayName, v.Attributes.GetProperty("description", (string)null));
			}
		} // func GetExcelReportItems

		/// <summary>View to get all excel reports</summary>
		/// <param name="dataSource"></param>
		/// <param name="privateUserData"></param>
		/// <returns></returns>
		public PpsDataSelector GetReportItemsSelector(PpsSysDataSource dataSource, IPpsPrivateDataContext privateUserData)
			=> new PpsGenericSelector<PpsExcelReportItem>(dataSource, "bi.reports", GetExcelReportItems(privateUserData));

		#endregion
	} // class ExcelCientItem
}
