using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TecWare.DE.Server;
using TecWare.DE.Stuff;
using TecWare.PPSn.Server.Data;
using TecWare.PPSn.Server.Sql;

namespace TecWare.PPSn.Server
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public partial class PpsApplication
	{
		private PpsSqlExDataSource mainDataSource;
		
		#region -- Init/Done --------------------------------------------------------------

		private void InitData()
		{
			//mainDataSource = new PpsSqlExDataSource(this, "Data Source=Gurke,6444;Initial Catalog=PPSn01;Integrated Security=True");
		} // proc InitData

		private void BeginReadConfigurationData(IDEConfigLoading config)
		{
			// find mainDataSource
			var mainDataSourceName = config.ConfigNew.GetAttribute("mainDataSource", String.Empty);
			if (String.IsNullOrEmpty(mainDataSourceName))
				throw new DEConfigurationException(config.ConfigNew, "@mainDataSource is empty.");

			var newMainDataSource = this.UnsafeFind(mainDataSourceName);
			if (newMainDataSource == null)
				throw new DEConfigurationException(config.ConfigNew, String.Format("@mainDataSource '{0}' not found.", mainDataSourceName));
			if(!(newMainDataSource is PpsSqlExDataSource))
				throw new DEConfigurationException(config.ConfigNew, String.Format("@mainDataSource '{0}' is a unsupported data source.", mainDataSourceName));

			config.EndReadAction(() => mainDataSource = (PpsSqlExDataSource)newMainDataSource);
		} // proc BeginReadConfigurationData

		private void BeginEndConfigurationData(IDEConfigLoading config)
		{
			// check registered selectors

		} // proc BeginEndConfigurationData

		private void DoneData()
		{
		} // proc DoneData

		#endregion

		public PpsDataSource GetDataSource(string name, bool throwException)
		{
			using (this.EnterReadLock())
				return (PpsDataSource)this.UnsafeChildren.FirstOrDefault(c => c is PpsDataSource && String.Compare(c.Name, name, StringComparison.OrdinalIgnoreCase) == 0);
		} // func GetDataSource
			
		public PpsDataSource MainDataSource => mainDataSource;
	} // class PpsApplication
}
