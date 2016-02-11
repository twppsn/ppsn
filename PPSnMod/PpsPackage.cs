using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TecWare.DE.Server;

namespace TecWare.PPSn.Server
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PpsPackage : DEConfigLogItem
	{
		public PpsPackage(IServiceProvider sp, string name)
			: base(sp, name)
		{
		} // ctor
	} // class PpsPackage
}
