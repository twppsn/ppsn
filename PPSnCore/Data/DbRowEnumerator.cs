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
using System.Collections;
using System.Collections.Generic;
using TecWare.DE.Data;

namespace TecWare.PPSn.Data
{
	#region -- interface IDataRowEnumerable -------------------------------------------

	/// <summary>Interface to provider access to an enumerable, that is filtered 
	/// and/or sorted.</summary>
	public interface IDataRowEnumerable : IEnumerable<IDataRow>
	{
		/// <summary>Apply an order and return a filtered enumerator.</summary>
		/// <param name="order"></param>
		/// <param name="lookupNative"></param>
		/// <returns></returns>
		IDataRowEnumerable ApplyOrder(IEnumerable<PpsDataOrderExpression> order, Func<string, string> lookupNative = null);
		/// <summary>Apply a filter and return a filtered enumerator.</summary>
		/// <param name="filter"></param>
		/// <param name="lookupNative"></param>
		/// <returns></returns>
		IDataRowEnumerable ApplyFilter(PpsDataFilterExpression filter, Func<string, string> lookupNative = null);
		/// <summary>Select columns and return a filtered enumerator.</summary>
		/// <param name="columns"></param>
		/// <returns></returns>
		IDataRowEnumerable ApplyColumns(IEnumerable<PpsDataColumnExpression> columns);
	} // interface IDataRowEnumerable

	#endregion
}
