using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using TecWare.PPSn.Server.Data;

namespace TecWare.PPSn.Server
{
	#region -- class PpsSysDataSource -------------------------------------------------

	public sealed class PpsSysDataSource : PpsDataSource
	{
		public PpsSysDataSource(IServiceProvider sp, string name)
			: base(sp, name)
		{
		}

		public override IPpsConnectionHandle CreateConnection(IPpsPrivateDataContext privateUserData, bool throwException = true)
		{
			throw new NotSupportedException();
		}

		public override Task<IPpsSelectorToken> CreateSelectorTokenAsync(string name, XElement sourceDescription)
		{
			throw new NotSupportedException();
		} // proc CreateSelectorTokenAsync

		public override PpsDataTransaction CreateTransaction(IPpsConnectionHandle connection)
		{
			throw new NotSupportedException();
		} // proc CreateTransaction

		public override string Type => "Sys";
	} // class PpsSysDataSource

	#endregion
}
