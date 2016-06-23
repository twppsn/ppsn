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
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TecWare.DE.Data;
using TecWare.DE.Stuff;
using TecWare.PPSn.Server.Data;

namespace TecWare.PPSn.Server.Sql
{
	#region -- class DynamicDataRow -----------------------------------------------------

	public abstract class DynamicDataRow : IDataRow
	{
		public virtual bool TryGetProperty(string columnName, out object value)
		{
			value = null;

			try
			{
				if (String.IsNullOrEmpty(columnName))
					return false;

				if (Columns == null || Columns.Length < 1)
					return false;

				if (Columns.Length != ColumnCount)
					return false;

				var index = Array.FindIndex(Columns, c => String.Compare(c.Name, columnName, StringComparison.OrdinalIgnoreCase) == 0);
				if (index == -1)
					return false;

				value = this[index];
				return true;
			} // try
			catch
			{
				return false;
			} // catch
		} // func TryGetProperty

		public virtual object this[string columnName]
		{
			get
			{
				var index = Array.FindIndex(Columns, c => String.Compare(c.Name, columnName, StringComparison.OrdinalIgnoreCase) == 0);
				if (index == -1)
					throw new ArgumentException(String.Format("Column with name \"{0}\" not found.", columnName ?? "null"));
				return this[index];
			}
		} // prop this

		public abstract object this[int index] { get; }

		public abstract int ColumnCount { get; }
		public abstract IDataColumn[] Columns { get; }
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
