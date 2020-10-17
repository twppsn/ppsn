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
using Neo.IronLua;
using TecWare.DE.Data;

namespace TecWare.PPSn.Server
{
	public partial class PpsApplication
	{
		/// <summary>Return first row of a request.</summary>
		/// <param name="rows"></param>
		/// <returns></returns>
		[LuaMember]
		public static IDataRow GetFirstRow(IEnumerable<IDataRow> rows)
			=> rows.Select(c => c.ToMyData()).FirstOrDefault();

		private LuaTable GetTableCore(IDataRow row)
		{
			var t = new LuaTable();
			for (var i = 0; i < row.Columns.Count; i++)
			{
				var v = row[i];
				if (v == null || v is string s && s.Length == 0)
					continue;
				t[row.Columns[i].Name] = v;
			}
			return t;
		} // func GetTableCore

		/// <summary>Copy the data row this data row to a lua-table</summary>
		/// <param name="value"></param>
		/// <returns></returns>
		[LuaMember]
		public LuaTable GetTable(object value)
		{
			if (value is IDataRow row)
				return GetTableCore(row);
			else if (value is IEnumerable<IDataRow> rows)
				return GetTableCore(GetFirstRow(rows));
			else if (value is LuaTable t)
				return t;
			else
				throw new ArgumentException($"First argument must be a {nameof(IDataRow)} or {nameof(IEnumerable<IDataRow>)}", nameof(value));
		} // func GetTable
	} // class PpsApplication
}
