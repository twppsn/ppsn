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

namespace TecWare.PPSn
{
	#region -- class PpsEnvironmentException ------------------------------------------

	/// <summary>Environment exception</summary>
	public class PpsEnvironmentException : Exception
	{
		/// <summary></summary>
		/// <param name="message"></param>
		/// <param name="innerException"></param>
		public PpsEnvironmentException(string message, Exception innerException)
			: base(message, innerException)
		{
		} // ctor
	} // class PpsEnvironmentException

	#endregion

	#region -- class PpsEnvironmentOnlineFailedException ------------------------------

	/// <summary>Exception if the system could not go online.</summary>
	public class PpsEnvironmentOnlineFailedException : PpsEnvironmentException
	{
		/// <summary>Exception if the system could not go online.</summary>
		public PpsEnvironmentOnlineFailedException()
			: base("System konnte nicht online geschaltet werden.", null)
		{
		}
	} // class PpsEnvironmentException

	#endregion
}