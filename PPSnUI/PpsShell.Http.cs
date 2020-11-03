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
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml.Linq;
using TecWare.DE.Data;
using TecWare.PPSn.Core.Data;

namespace TecWare.PPSn
{
	#region -- class PpsShell ---------------------------------------------------------

	public static partial class PpsShell
	{
		#region -- GetViewData, GetXmlData --------------------------------------------

		/// <summary>Get view data from server</summary>
		/// <param name="shell"></param>
		/// <param name="arguments"></param>
		/// <returns></returns>
		public static IEnumerable<IDataRow> GetViewData(this IPpsShell shell, PpsDataQuery arguments)
			=> shell.Http.CreateViewDataReader(arguments.ToQuery());

		/// <summary></summary>
		/// <param name="shell"></param>
		/// <param name="uri"></param>
		/// <returns></returns>
		public static Task<XElement> GetXmlDataAsync(this IPpsShell shell, string uri)
			=> shell.Http.GetXmlAsync(uri);

		#endregion
	} // class PpsShell

	#endregion
}
