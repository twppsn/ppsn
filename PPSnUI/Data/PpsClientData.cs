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
using System.Threading.Tasks;

namespace TecWare.PPSn.Data
{
	#region -- class PpsClientData ----------------------------------------------------

	/// <summary>A set of table based data, that is loaded from the server.</summary>
	public class PpsClientData : PpsDataSet
	{
		#region -- class PpsClientDataDefinition --------------------------------------

		private sealed class PpsClientDataDefinition
		{
		} // class PpsClientDataDefinition

		#endregion

		/// <summary>Creates a data representation for the client.</summary>
		/// <param name="datasetDefinition"></param>
		private PpsClientData(PpsDataSetDefinition datasetDefinition) 
			: base(datasetDefinition)
		{
		} // ctor

		// protected abstract T CreateExtendedRowValue() <-- z.b. Link in LiveData?
		// protected abstract T CreateTypedDataRow();
		
		// private static Type tableType = null; //  ICollectionViewFactory, PpsDataTableDesktop - Problem <- sollte privat sein
		
		/// <summary></summary>
		/// <param name="clientDataSetType"></param>
		/// <param name="serverName"></param>
		/// <returns></returns>
		public static Task<PpsClientData> CreateAsync(Type clientDataSetType, string serverName)
		{
			return Task.FromResult<PpsClientData>(null);
		} // func CreateAsync

		/// <summary>Create empty dataset.</summary>
		/// <typeparam name="T"></typeparam>
		/// <returns></returns>
		public static Task<T> CreateAsync<T>()
			where T : PpsClientData
		{
			return Task.FromResult<T>(null);
		} // func CreateAsync
	} // class PpsClientData

	#endregion
}
