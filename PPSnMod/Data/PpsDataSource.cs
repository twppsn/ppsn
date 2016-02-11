using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TecWare.DE.Server;
using TecWare.DES.Data;
using TecWare.PPSn.Server.Data;

namespace TecWare.PPSn.Server.Data
{
	public abstract class PpsDataSource : DEConfigItem
	{

		public PpsDataSource(IServiceProvider sp, string name)
			: base(sp, name)
		{
		} // ctor

		//public virtual bool EnsureConnection(object context)
		//{
		//	return true;
		//}

		//public virtual bool IsConnectionRelatedException(object context, Exception e)
		//{
		//	return false;
		//}

		public abstract IPpsConnectionHandle CreateConnection(IPpsPrivateDataContext privateUserData, bool throwException = true);

		public virtual PpsDataSelector CreateSelector(IPpsConnectionHandle connection, string name, bool throwException = true)
		{
			if (throwException)
				throw new ArgumentOutOfRangeException("name", String.Format("Selector '{0}' not found.", name));
			else
				return null;
		} // func CreateSelector

		public virtual PpsDataTransaction CreateTransaction(IPpsConnectionHandle connection)
		{
			throw new NotImplementedException();
		}// func CreateTransaction
	} // class PpsDataSource
}
