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
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace TecWare.PPSn
{
	/// <summary>Special environment for data warehouse applications</summary>
	public sealed class PpsEnvironment
	{
		private readonly PpsEnvironmentInfo info;
		private ICredentials credentialInfo;

		public PpsEnvironment(PpsEnvironmentInfo info)
		{
			this.info = info;
		} // ctor

		public void ClearCredentials()
			=> credentialInfo = null;

		/// <summary>Name of the environment.</summary>
		public string Name => info.Name;
		/// <summary>Authentificated user.</summary>
		public string UserName => credentialInfo is null ? null : PpsEnvironmentInfo.GetUserNameFromCredentials(credentialInfo);

		/// <summary>Access the environment information block.</summary>
		public PpsEnvironmentInfo Info => info;
		/// <summary>Login information</summary>
		public ICredentials Credentials => credentialInfo;
		/// <summary>Has this environment credentials.</summary>
		public bool IsAuthentificated => !(credentialInfo is null);
	} // class PpsEnvironment
}
