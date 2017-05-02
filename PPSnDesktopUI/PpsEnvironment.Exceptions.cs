using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TecWare.PPSn
{
	#region -- class PpsEnvironmentException --------------------------------------------

	public class PpsEnvironmentException : Exception
	{
		public PpsEnvironmentException(string message, Exception innerException)
			: base(message, innerException)
		{
		} // ctor
	} // class PpsEnvironmentException

	#endregion

	#region -- class PpsEnvironmentOnlineFailedException -----------------------------------

	public class PpsEnvironmentOnlineFailedException : PpsEnvironmentException
	{
		public PpsEnvironmentOnlineFailedException()
			: base("System konnte nicht online geschaltet werden.", null)
		{
		}
	} // class PpsEnvironmentException

	#endregion
}
