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
using System.Text;
using System.Threading.Tasks;

namespace TecWare.PPSn.Data
{
	#region -- interface IPpsTableData ------------------------------------------------

	/// <summary>Table data representation</summary>
	public interface IPpsTableData
	{
		/// <summary>Update the table data.</summary>
		/// <param name="views"></param>
		Task UpdateAsync(string views);

		/// <summary>Change the displayname of the table.</summary>
		string DisplayName { get; set; }

		/// <summary>Get all views</summary>
		string Views { get; }

		/// <summary>Is this an empty view.</summary>
		bool IsEmpty { get; }
	} // interface IPpsTableData

	#endregion
}
