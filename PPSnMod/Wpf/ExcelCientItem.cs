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
	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Excel specific services</summary>
	public class ExcelCientItem : DEConfigItem
	{
		private readonly PpsApplication application;

		public ExcelCientItem(IServiceProvider sp, string name)
			: base(sp, name)
		{
			this.application = sp.GetService<PpsApplication>(true);
		} // ctor

		#region -- PpsExcelReportItem -----------------------------------------------------

		public sealed class PpsExcelReportItem
		{
			private readonly string type;
			private readonly string reportId;
			private readonly string displayName;

			public PpsExcelReportItem(string type, string reportId, string displayName)
			{
				this.type = type;
				this.reportId = reportId;
				this.displayName = displayName;
			} // ctor

			public string Type => type;
			public string ReportId => reportId;
			public string DisplayName => displayName;
		} // PpsExcelReportItem

		private IEnumerable<PpsExcelReportItem> GetExcelReportItems(IPpsPrivateDataContext privateUserData)
		{
			foreach (var v in application.GetViewDefinitions())
			{
				if (v.Attributes.GetProperty("Excel.Report", false))
					yield return new PpsExcelReportItem("table", v.Name, v.DisplayName);
			}
		} // func GetExcelReportItems

		public PpsDataSelector GetExcelReportItemsSelector(PpsSysDataSource dataSource, IPpsPrivateDataContext privateUserData)
			=> new PpsGenericSelector<PpsExcelReportItem>(dataSource, "wpf.reports", GetExcelReportItems(privateUserData));

		#endregion
	} // class ExcelCientItem
}
