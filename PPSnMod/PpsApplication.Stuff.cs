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
using TecWare.DE.Stuff;

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

		private static LuaTable GetTableCore(IDataRow row)
		{
			var t = new LuaTable();
			if (row != null)
			{
				for (var i = 0; i < row.Columns.Count; i++)
				{
					var v = row[i];
					if (v == null || v is string s && s.Length == 0)
						continue;
					t[row.Columns[i].Name] = v;
				}
			}
			return t;
		} // func GetTableCore

		private static LuaTable GetTableFromPathCore(LuaTable table, string tablePath, int offset, int count, bool writable)
		{
			var cur = table;
			
			var lastDot = offset - 1;
			var endAt = offset + count  - 1;
			while (offset <= endAt)
			{
				if (tablePath[offset] == '.')
				{
					var k = tablePath.Substring(lastDot + 1, offset - lastDot + 1);
					if (cur[k] is LuaTable t)
						cur = t;
					else if (writable)
						cur[k] = cur = new LuaTable();
					else
						return null;

					lastDot = offset;
				}

				offset++;
			}

			return cur;
		} // func GetTableFromPathCore

		private static LuaTable GetTableFromPathCore(LuaTable table, string tablePath, bool writable)
			=> String.IsNullOrEmpty(tablePath) ? table : GetTableFromPathCore(table, tablePath, 0, tablePath.Length, writable);

		/// <summary>Copy the data row this data row to a lua-table</summary>
		/// <param name="value"></param>
		/// <param name="tablePath"></param>
		/// <param name="writable"></param>
		/// <returns></returns>
		[LuaMember]
		public static LuaTable GetTable(object value, string tablePath = null, bool writable = false)
		{
			if (value is LuaTable t)
				return GetTableFromPathCore(t, tablePath, writable);
			else if (value is IDataRow row)
				return GetTableFromPathCore(GetTableCore(row), tablePath, writable);
			else if (value is IEnumerable<IDataRow> rows)
				return GetTableFromPathCore(GetTableCore(GetFirstRow(rows)), tablePath, writable);
			else
				throw new ArgumentException($"First argument must be a {nameof(IDataRow)} or {nameof(IEnumerable<IDataRow>)}", nameof(value));
		} // func GetTable

		/// <summary>Get a structured property.</summary>
		/// <param name="table"></param>
		/// <param name="propertyPath"></param>
		/// <returns></returns>
		[LuaMember]
		public static object GetTableProperty(LuaTable table, string propertyPath)
		{
			TryGetTableProperty(table, propertyPath, out var v);
			return v;
		} // func GetTableProperty

		/// <summary></summary>
		/// <param name="table"></param>
		/// <param name="propertyPath"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public static bool TryGetTableProperty(LuaTable table, string propertyPath,out object value)
		{
			if (table == null)
			{
				value = null;
				return false;
			}

			var p = propertyPath.LastIndexOf('.');
			if (p == -1)
			{
				value = table.GetMemberValue(propertyPath);
				return value != null;
			}
			else
			{
				var t = GetTableFromPathCore(table, propertyPath, 0, p, false);
				if (t == null)
				{
					value = null;
					return false;
				}
				else
				{
					value = t.GetMemberValue(propertyPath.Substring(p + 1));
					return value != null;
				}
			}
		} // func GetTableProperty
	} // class PpsApplication
}
