using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TecWare.DE.Server;
using TecWare.PPSn.Server.Sql;

namespace TecWare.PPSn.Server
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Base service provider, for all pps-moduls:
	/// - user administration
	/// - data cache, for commonly used data or states
	/// - view services (executes and updates all views, to data)
	/// </summary>
	public partial class PpsApplication : DEConfigLogItem
	{
		#region -- Ctor/Dtor --------------------------------------------------------------

		public PpsApplication(IServiceProvider sp, string name)
			: base(sp, name)
		{
			InitData();
			InitUser();
		} // ctor

		protected override void OnBeginReadConfiguration(IDEConfigLoading config)
		{
			base.OnBeginReadConfiguration(config);

			BeginReadConfigurationData(config);
			BeginReadConfigurationUser(config);

		} // proc OnBeginReadConfiguration

		protected override void OnEndReadConfiguration(IDEConfigLoading config)
		{
			base.OnEndReadConfiguration(config);

			BeginEndConfigurationData(config);
			BeginEndConfigurationUser(config);
		} // proc OnEndReadConfiguration

		protected override void Dispose(bool disposing)
		{
			try
			{
				DoneUser();
				DoneData();
			}
			finally
			{
				base.Dispose(disposing);
			}
		} // proc Dispose

		#endregion

	} // class PpsApplication

}
