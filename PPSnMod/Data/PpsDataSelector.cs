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
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TecWare.DE.Server;
using TecWare.DE.Stuff;
using TecWare.DE.Data;
using System.Reflection;
using TecWare.PPSn.Data;

namespace TecWare.PPSn.Server.Data
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public abstract class PpsDataSelector : IDERangeEnumerable<IDataRow>, IEnumerable<IDataRow>
	{
		private readonly PpsDataSource source;

		public PpsDataSelector(PpsDataSource source)
		{
			this.source = source;
		} // ctor

		public virtual IEnumerator GetEnumerator()
			=> GetEnumerator(0, Int32.MaxValue);

		IEnumerator<IDataRow> IEnumerable<IDataRow>.GetEnumerator()
			=> GetEnumerator(0, Int32.MaxValue);

		/// <summary>Returns a enumerator for the range.</summary>
		/// <param name="start">Start of the enumerator</param>
		/// <param name="count">Number of elements that should be returned,</param>
		/// <returns></returns>
		public abstract IEnumerator<IDataRow> GetEnumerator(int start, int count);

		public virtual PpsDataSelector ApplyOrder(IEnumerable<PpsDataOrderExpression> expressions, Func<string, string> lookupNative = null)
			=> this;

		public virtual PpsDataSelector ApplyFilter(PpsDataFilterExpression expression, Func<string, string> lookupNative = null)
			=> this;

		/// <summary>Returns the field description for the name in the resultset</summary>
		/// <param name="nativeColumnName"></param>
		/// <returns></returns>
		public abstract IPpsColumnDescription GetFieldDescription(string nativeColumnName);

		/// <summary>by default we do not know the number of items</summary>
		public virtual int Count => -1;

		/// <summary></summary>
		public PpsDataSource DataSource => source;
	} // class PpsDataSelector
}
