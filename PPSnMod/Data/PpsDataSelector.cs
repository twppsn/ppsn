using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TecWare.DE.Server;
using TecWare.DE.Stuff;
using TecWare.DES.Data;

namespace TecWare.PPSn.Server.Data
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public abstract class PpsDataSelector : IDERangeEnumerable2<IDataRow>, IEnumerable<IDataRow>
	{
		private readonly PpsDataSource source;

		public PpsDataSelector(PpsDataSource source)
		{
			this.source = source;
		} // ctor

		public virtual int Count => -1;

		public virtual IEnumerator GetEnumerator()
			=> GetEnumerator(0, Int32.MaxValue);

		IEnumerator<IDataRow> IEnumerable<IDataRow>.GetEnumerator()
			=> GetEnumerator(0, Int32.MaxValue, PropertyDictionary.EmptyReadOnly);

		public virtual IEnumerator<IDataRow> GetEnumerator(int start, int count)
			=> GetEnumerator(start, count, PropertyDictionary.EmptyReadOnly);

		public abstract IEnumerator<IDataRow> GetEnumerator(int start, int count, IPropertyReadOnlyDictionary selector);

		public PpsDataSource DataSource => source;
	} // class PpsDataView
}
