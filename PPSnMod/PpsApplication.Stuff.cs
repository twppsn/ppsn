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
using System.Linq;
using Neo.IronLua;
using TecWare.DE.Data;
using TecWare.DE.Stuff;

namespace TecWare.PPSn.Server
{
	public partial class PpsApplication
	{
		#region -- class RowsArray ----------------------------------------------------

		private sealed class RowsArray : IReadOnlyList<IDataRow>, IDataColumns
		{
			private readonly IDataColumns columns;
			private readonly IDataRow[] rows;

			public RowsArray(IDataColumns columns, IDataRow[] rows)
			{
				this.columns = columns ?? throw new ArgumentNullException(nameof(columns));
				this.rows = rows ?? throw new ArgumentNullException(nameof(rows));
			} // ctor

			public IEnumerator<IDataRow> GetEnumerator()
				=> ((IEnumerable<IDataRow>)rows).GetEnumerator();

			IEnumerator IEnumerable.GetEnumerator()
				=> rows.GetEnumerator();

			public int Count => rows.Length;
			public IDataRow this[int index] => rows[index];

			public IReadOnlyList<IDataColumn> Columns => columns.Columns;
		} // class RowsArray

		#endregion

		/// <summary>Return row of a request.</summary>
		/// <param name="row"></param>
		/// <param name="columnInfo"></param>
		/// <returns></returns>
		[LuaMember]
		public static IDataRow GetRow(IDataRow row, IDataColumns columnInfo = null)
		{
			var columns = new IDataColumn[row.Columns.Count];
			var values = new object[row.Columns.Count];
			for (var i = 0; i < values.Length; i++)
			{
				values[i] = row[i];
				columns[i] = (columnInfo ?? row).Columns[i];
			}

			return new SimpleDataRow(values, columns);
		} // GetRow

		/// <summary>Copy single rows.</summary>
		/// <param name="rows"></param>
		/// <returns></returns>
		[LuaMember]
		public static IEnumerable<IDataRow> CopyRows(IEnumerable<IDataRow> rows)
			=> rows.Select(row => GetRow(row, rows as IDataColumns));

		/// <summary>Create a copy of the whole result set.</summary>
		/// <param name="rows"></param>
		/// <param name="offset"></param>
		/// <param name="count"></param>
		/// <returns></returns>
		[LuaMember]
		public static IReadOnlyList<IDataRow> GetRows(IEnumerable<IDataRow> rows, int offset = 0, int count = Int32.MaxValue)
		{
			var columns = rows as IDataColumns;

			rows = CopyRows(rows);
			if (offset > 0)
				rows = rows.Skip(offset);
			if (count < Int32.MaxValue)
				rows = rows.Take(count);

			return new RowsArray(columns, rows.ToArray());
		} // func GetRows

		/// <summary>Return first row of a request.</summary>
		/// <param name="rows"></param>
		/// <returns></returns>
		[LuaMember]
		public static IDataRow GetFirstRow(IEnumerable<IDataRow> rows)
			=> CopyRows(rows).FirstOrDefault();

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

			LuaTable GetOrCreateTable(string k)
			{
				if (cur[k] is LuaTable t)
					return t;
				else if (writable)
				{
					t = new LuaTable();
					cur[k] = t;
					return t;
				}
				else
					return null;
			} // func GetOrCreateTable

			var lastDot = offset - 1;
			var endAt = offset + count - 1;
			while (offset <= endAt)
			{
				if (tablePath[offset] == '.')
				{
					if ((cur = GetOrCreateTable(tablePath.Substring(lastDot + 1, offset - lastDot - 1))) == null)
						return null;

					lastDot = offset;
				}

				offset++;
			}

			return GetOrCreateTable(tablePath.Substring(lastDot + 1));
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

		/// <summary>Create a copy of all <see cref="IDataRow"/>s</summary>
		/// <param name="rows"></param>
		/// <returns></returns>
		[LuaMember]
		public static LuaTable GetTableWithRows(IEnumerable<IDataRow> rows)
		{
			var t = new LuaTable();
			foreach (var r in rows)
				t.ArrayList.Add(GetTableCore(r));
			return t;
		} // func GetTableWithRows

		/// <summary>Get a structured property.</summary>
		/// <param name="value"></param>
		/// <param name="propertyPath"></param>
		/// <param name="default"></param>
		/// <returns></returns>
		[LuaMember]
		public static object GetProperty(object value, string propertyPath, object @default = null)
		{
			if (String.IsNullOrEmpty(propertyPath))
				return value;

			if (value is LuaTable t)
				return TryGetTableProperty(t, propertyPath, out var r) ? r : @default;
			else if (value is IDataRow row)
				return TryGetTableProperty(row.ToTable(), propertyPath, out var r) ? r : @default;
			else if (value is IPropertyReadOnlyDictionary props)
				return TryGetTableProperty(props.ToTable(), propertyPath, out var r) ? r : @default;
			else
				throw new ArgumentException($"First argument must be a {nameof(IPropertyReadOnlyDictionary)}, {nameof(IDataRow)} or {nameof(LuaTable)}", nameof(value));
		} // func GetTableProperty

		/// <summary></summary>
		/// <param name="table"></param>
		/// <param name="propertyPath"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public static bool TryGetTableProperty(LuaTable table, string propertyPath, out object value)
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
