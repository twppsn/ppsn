using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TecWare.DE.Stuff;
using TecWare.DES.Data;
using TecWare.PPSn.Server.Data;

namespace TecWare.PPSn.Server.Sql
{
	#region -- class DynamicDataRow -----------------------------------------------------

	public abstract class DynamicDataRow : IDataRow
	{
		public virtual bool TryGetProperty(string name, out object value)
		{
			var idx = Array.FindIndex(ColumnNames, c => String.Compare(c, name, StringComparison.OrdinalIgnoreCase) == 0);
			if (idx >= 0 && idx < ColumnCount)
			{
				value = this[idx];
				return true;
			}
			else
			{
				value = null;
				return false;
			}
		} // func TryGetProperty

		public virtual object this[string columnName]
		{
			get
			{
				object tmp;
				return TryGetProperty(columnName, out tmp) ? tmp : null;
			}
		} // func this

		public abstract object this[int index] { get; }

		public abstract int ColumnCount { get; }
		public abstract string[] ColumnNames { get; }
		public abstract Type[] ColumnTypes { get; }
	} // class DynamicDataRow

	#endregion

	#region -- class PpsSqlDataSource ---------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public abstract class PpsSqlDataSource : PpsDataSource
	{
		//private DbConnection connection = null;

		public PpsSqlDataSource(IServiceProvider sp, string name)
			: base(sp, name)
		{
		} // ctor
	} // class PpsSqlDataSource

	#endregion
}
