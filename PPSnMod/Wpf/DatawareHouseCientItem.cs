using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TecWare.DE.Server;
using TecWare.DE.Stuff;
using TecWare.PPSn.Server.Data;

namespace TecWare.PPSn.Server.Wpf
{
	/// <summary>Excel specific services</summary>
	public class DatawareHouseCientItem : DEConfigItem
	{
		private readonly PpsApplication application;

		/// <summary></summary>
		/// <param name="sp"></param>
		/// <param name="name"></param>
		public DatawareHouseCientItem(IServiceProvider sp, string name)
			: base(sp, name)
		{
			this.application = sp.GetService<PpsApplication>(true);
		} // ctor

		#region -- PpsExcelReportItem -----------------------------------------------------

		/// <summary></summary>
		public sealed class PpsExcelReportItem
		{
			private readonly string type;
			private readonly string reportId;
			private readonly string displayName;
			private readonly string description;

			internal PpsExcelReportItem(string type, string reportId, string displayName, string description)
			{
				this.type = type;
				this.reportId = reportId;
				this.displayName = displayName;
				this.description = description;
			} // ctor

			/// <summary>Type of the excel report item</summary>
			public string Type => type;
			/// <summary>Id of the report.</summary>
			public string ReportId => reportId;
			/// <summary>Display name for the report.</summary>
			public string DisplayName => displayName;
			/// <summary>Description of the report</summary>
			public string Description => description;
		} // PpsExcelReportItem

		private IEnumerable<PpsExcelReportItem> GetExcelReportItems(IPpsPrivateDataContext privateUserData)
		{
			foreach (var v in application.GetViewDefinitions())
			{
				if (v.Attributes.GetProperty("bi.visible", false))
					yield return new PpsExcelReportItem("table", v.Name, v.DisplayName, v.Attributes.GetProperty("description", (string)null));
			}
		} // func GetExcelReportItems

		/// <summary></summary>
		/// <param name="dataSource"></param>
		/// <param name="privateUserData"></param>
		/// <returns></returns>
		public PpsDataFilterCombo GetReportItemsSelector(PpsSysDataSource dataSource, IPpsPrivateDataContext privateUserData)
			=> new PpsGenericSelector<PpsExcelReportItem>(dataSource, "bi.reports", GetExcelReportItems(privateUserData));

		#endregion
	} // class ExcelCientItem
}
