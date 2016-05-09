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

		/// <summary>Returns the field description for the name in the resultset</summary>
		/// <param name="nativeColumnName"></param>
		/// <returns></returns>
		public abstract IPpsColumnDescription GetFieldDescription(string nativeColumnName);

		/// <summary>by default we do not know the number of items</summary>
		public virtual int Count => -1;

		/// <summary></summary>
		public PpsDataSource DataSource => source;
	} // class PpsDataView
}
