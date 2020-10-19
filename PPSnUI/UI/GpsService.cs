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
using System.Text;

namespace TecWare.PPSn.UI
{
	#region -- interface IPpsGpsService -----------------------------------------------

	/// <summary>Get the location of the current device.</summary>
	public interface IPpsGpsService
	{
		/// <summary>Last known location of the device.</summary>
		/// <param name="longitude"></param>
		/// <param name="latitude"></param>
		/// <param name="timestamp"></param>
		/// <returns></returns>
		bool TryGetGeoCoordinate(out double longitude, out double latitude, out long timestamp);
	} // interface IPpsGpsService

	#endregion
}
