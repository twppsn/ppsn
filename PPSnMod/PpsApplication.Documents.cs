using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TecWare.DE.Server;

namespace TecWare.PPSn.Server
{
	public sealed class PpsDocument : DEConfigItem
	{
		public PpsDocument(IServiceProvider sp, string name)
			: base(sp, name)
		{
		}
	} // class PpsDocument

	public partial class PpsApplication
	{
	} // class PpsApplication
}
