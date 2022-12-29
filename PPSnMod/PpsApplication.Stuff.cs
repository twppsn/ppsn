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
using System.CodeDom;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Neo.IronLua;
using TecWare.DE.Data;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;
using TecWare.PPSn.Server.Data;

namespace TecWare.PPSn.Server
{
	public partial class PpsApplication
	{
		#region -- class RowCopy ------------------------------------------------------

		private sealed class RowCopy : DynamicDataRow
		{
			private readonly object[] values;
			private readonly IDataColumns columns;

			public RowCopy(object[] values, IDataColumns columns)
			{
				this.values = values ?? throw new ArgumentNullException(nameof(values));
				this.columns = columns ?? throw new ArgumentNullException(nameof(columns));
			} // ctor

			public override object this[int index] => values[index];
			public override IReadOnlyList<IDataColumn> Columns => columns.Columns;

			public override bool IsDataOwner => true;
		} // class RowCopy

		#endregion

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

		#region -- class RowsEnumerable -----------------------------------------------

		private sealed class RowsEnumerable : IEnumerable<IDataRow>, IDataColumns
		{
			private readonly IDataColumns columns;
			private readonly IEnumerable<IDataRow> rows;

			public RowsEnumerable(IDataColumns columns, IEnumerable<IDataRow> rows)
			{
				this.columns = columns ?? throw new ArgumentNullException(nameof(columns));
				this.rows = rows ?? throw new ArgumentNullException(nameof(rows));
			} // ctor

			public IEnumerator<IDataRow> GetEnumerator()
				=> rows.GetEnumerator();

			IEnumerator IEnumerable.GetEnumerator()
				=> rows.GetEnumerator();

			public IReadOnlyList<IDataColumn> Columns => columns.Columns;
		} // class RowsEnumerable

		#endregion

		#region -- class RowColumn ----------------------------------------------------

		private sealed class RowColumn : IPpsColumnDescription
		{
			private readonly string columnName;
			private readonly IPpsColumnDescription columnDescription;

			public RowColumn(string columnName, IPpsColumnDescription columnDescription)
			{
				this.columnName = columnName ?? throw new ArgumentNullException(nameof(columnName));
				this.columnDescription = columnDescription ?? throw new ArgumentNullException(nameof(columnDescription));
			} // ctor

			public string Name => columnName;

			public Type DataType => columnDescription.DataType ?? typeof(string);

			public IPropertyEnumerableDictionary Attributes => columnDescription.Attributes;

			public T GetColumnDescription<T>()
				where T : IPpsColumnDescription
				=> columnDescription.GetColumnDescription<T>();
		} // class RowColumn

		#endregion

		#region -- class RowColumns ---------------------------------------------------

		private sealed class RowColumns : IPpsColumnDescriptions
		{
			private readonly IPpsColumnDescription[] columns;

			public RowColumns(IPpsColumnDescription[] columns)
			{
				this.columns = columns ?? throw new ArgumentNullException(nameof(columns));
			} // ctor

			public IReadOnlyList<IDataColumn> Columns => columns;

			public IPpsColumnDescription GetFieldDescription(int columnIndex)
				=> columns[columnIndex];

			public IPpsColumnDescription GetFieldDescription(string columnName)
				=> columns.FirstOrDefault(c => String.Compare(c.Name, columnName, StringComparison.OrdinalIgnoreCase) == 0);
		} // class RowColumns

		#endregion

		#region -- GetRow -------------------------------------------------------------

		private static IDataRow GetRowWithColumnInfo(IDataRow row, IDataColumns columnInfo)
		{
			var values = new object[row.Columns.Count];
			for (var i = 0; i < values.Length; i++)
				values[i] = row[i];

			return new RowCopy(values, columnInfo);
		} // GetRowWithColumnInfo

		private static IDataRow GetRowCreateColumnInfo(IDataRow row)
		{
			var columns = new IDataColumn[row.Columns.Count];
			var values = new object[row.Columns.Count];
			for (var i = 0; i < values.Length; i++)
			{
				values[i] = row[i];
					columns[i] = row.Columns[i];
			}

			return new RowCopy(values, new SimpleDataColumns(columns));
		} // GetRowCreateColumnInfo

		/// <summary>Return row of a request.</summary>
		/// <param name="row"></param>
		/// <param name="columnInfo"></param>
		/// <returns></returns>
		[LuaMember]
		public static IDataRow GetRow(IDataRow row, IDataColumns columnInfo = null)
			=> columnInfo == null ? GetRowCreateColumnInfo(row) : GetRowWithColumnInfo(row, columnInfo);

		/// <summary>Create a copy of the object as an row.</summary>
		/// <param name="values"></param>
		/// <param name="columnInfo"></param>
		/// <returns></returns>
		[LuaMember]
		public static IDataRow GetRowFromGeneric(IDataColumns columnInfo, object values)
		{
			switch (values)
			{
				case IDataRow row:
					return GetRow(row, columnInfo);
				case LuaTable table:
					return GetRowFromTable(columnInfo, table);
				case IPropertyReadOnlyDictionary properties:
					return GetRowFromProperties(columnInfo, properties);
				default:
					object tmp = null;
					return GetRowFromObject(columnInfo, values, ref tmp);
			}
		} // func GetRowFromGeneric

		/// <summary>Create a copy of the table as an row.</summary>
		/// <param name="table"></param>
		/// <param name="columnInfo"></param>
		/// <returns></returns>
		[LuaMember]
		public static IDataRow GetRowFromTable(IDataColumns columnInfo, LuaTable table)
		{
			var values = new object[columnInfo.Columns.Count];
			for (var i = 0; i < values.Length; i++)
				values[i] = table.GetMemberValue(columnInfo.Columns[i].Name);
			return new RowCopy(values, columnInfo);
		} // func GetRowFromTable

		/// <summary>Create a copy of the properties as an row.</summary>
		/// <param name="properties"></param>
		/// <param name="columnInfo"></param>
		/// <returns></returns>
		[LuaMember]
		public static IDataRow GetRowFromProperties(IDataColumns columnInfo, IPropertyReadOnlyDictionary properties)
		{
			var values = new object[columnInfo.Columns.Count];
			for (var i = 0; i < values.Length; i++)
				values[i] = properties.TryGetProperty(columnInfo.Columns[i].Name, out var v) ? v : null;
			return new RowCopy(values, columnInfo);
		} // func GetRowFromTable

		/// <summary>Create a copy of the object as an row.</summary>
		/// <param name="obj"></param>
		/// <param name="columnInfo"></param>
		/// <param name="infoCache"></param>
		/// <returns></returns>
		[LuaMember]
		public static IDataRow GetRowFromObject(IDataColumns columnInfo, object obj, ref object infoCache)
		{
			if (obj is null)
				throw new ArgumentNullException(nameof(obj));

			// build member info
			if (!(infoCache is MemberInfo[] memberInfo))
			{
				var typeInfo = obj.GetType();
				memberInfo = new MemberInfo[columnInfo.Columns.Count];
				for (var i = 0; i < memberInfo.Length; i++)
					memberInfo[i] = typeInfo.GetMember(columnInfo.Columns[i].Name).FirstOrDefault();
			}

			// copy values
			var values = new object[memberInfo.Length];
			for (var i = 0; i < values.Length; i++)
			{
				switch (memberInfo[i])
				{
					case FieldInfo fieldInfo:
						values[i] = fieldInfo.GetValue(obj);
						break;
					case PropertyInfo propertyInfo:
						values[i] = propertyInfo.GetValue(obj);
						break;
					case MethodInfo methodInfo:
						values[i] = methodInfo.Invoke(obj, Array.Empty<object>());
						break;
					default:
						values[i] = null;
						break;
				}
			}

			return new RowCopy(values, columnInfo);
		} // func GetRowFromObject

		#endregion

		#region -- GetRows, CopyRows --------------------------------------------------

		private static IEnumerable<IDataRow> CopyRowsCore(IEnumerable<IDataRow> rows, IDataColumns columnInfo)
		{
			var isFirst = true;
			foreach (var row in rows)
			{
				if (isFirst)
				{
					isFirst = false;

					if (columnInfo == null) // copy columns of the first row
					{
						var firstRow = GetRowCreateColumnInfo(row);
						columnInfo = firstRow;
						yield return firstRow;
					}
					else // validate columnInfo
					{
						if (row.Columns.Count != columnInfo.Columns.Count)
							throw new ArgumentOutOfRangeException($"ColumnInfo does not the same number of columns (columnInfo={columnInfo.Columns.Count},row={row.Columns.Count}");
						yield return GetRowWithColumnInfo(row, columnInfo);
					}
				}
				else
					yield return GetRowWithColumnInfo(row, columnInfo);
			}
		} // func CopyRowsCore

		/// <summary>Copy single rows.</summary>
		/// <param name="rows"></param>
		/// <param name="columnInfo"></param>
		/// <returns></returns>
		[LuaMember]
		public static IEnumerable<IDataRow> CopyRows(IEnumerable<IDataRow> rows, IDataColumns columnInfo = null)
			=> columnInfo == null ? CopyRowsCore(rows, columnInfo) : new RowsEnumerable(columnInfo, CopyRowsCore(rows, columnInfo));

		/// <summary>Create a copy of the whole result set.</summary>
		/// <param name="rows"></param>
		/// <param name="offset"></param>
		/// <param name="count"></param>
		/// <param name="columnInfo"></param>
		/// <returns></returns>
		[LuaMember]
		public static IReadOnlyList<IDataRow> GetRows(IEnumerable<IDataRow> rows, int offset = 0, int count = Int32.MaxValue, IDataColumns columnInfo = null)
		{
			rows = CopyRows(rows, columnInfo);

			if (offset > 0)
				rows = rows.Skip(offset);
			if (count < Int32.MaxValue)
				rows = rows.Take(count);

			return new RowsArray(columnInfo ?? rows as IDataColumns, rows.ToArray());
		} // func GetRows

		/// <summary>Create a copy of the whole result set.</summary>
		/// <param name="rows"></param>
		/// <param name="offset"></param>
		/// <param name="count"></param>
		/// <param name="columnInfo"></param>
		/// <returns></returns>
		[LuaMember]
		public IReadOnlyList<IDataRow> GetRowsWithFields(IEnumerable<IDataRow> rows, int offset = 0, int count = Int32.MaxValue, object columnInfo = null)
			=> GetRows(rows, offset, count, CreateColumnDescriptions(columnInfo));

		#endregion

		#region -- CreateColumnDescriptions -------------------------------------------

		private IPpsColumnDescription CreateColumnDescription(string fieldDescription, string name)
		{
			if (String.IsNullOrEmpty(name))
			{
				var p = fieldDescription.LastIndexOf('.');
				name = p >= 0 ? fieldDescription.Substring(p) : fieldDescription; ;
			}
			return new RowColumn(name, GetFieldDescription(fieldDescription));
		} // func CreateColumnDescription

		/// <summary>Create a set of columns.</summary>
		/// <param name="fieldDescriptions">Alias is allowed with the equal seperator.</param>
		/// <returns></returns>
		[LuaMember]
		public IPpsColumnDescriptions CreateColumnDescriptionsFromFields(params string[] fieldDescriptions)
		{
			var columnInfo = new List<IPpsColumnDescription>();
			foreach (var cur in fieldDescriptions)
			{
				var p = cur.IndexOf('=');
				if (p >= 0)
					columnInfo.Add(CreateColumnDescription(cur.Substring(p + 1), cur.Substring(0, p)));
				else
					columnInfo.Add(CreateColumnDescription(cur, null));
			}
			return new RowColumns(columnInfo.ToArray());
		} // func CreateColumnDescriptions

		/// <summary>Create a set of columns.</summary>
		/// <param name="descriptions">Index by index.</param>
		/// <returns></returns>
		[LuaMember]
		public IPpsColumnDescriptions CreateColumnDescriptionsFromTable(LuaTable descriptions)
		{
			var columnInfo = new List<IPpsColumnDescription>();
			foreach(var kv in descriptions.Members)
			{
				if (kv.Value is string fieldDescription)
					columnInfo.Add(CreateColumnDescription(fieldDescription, kv.Key));
				else
					throw new ArgumentException($"Invalid column definition for '{kv.Key}'");
			}
			return new RowColumns(columnInfo.ToArray());
		} // func CreateColumnDescriptionsFromTable

		/// <summary>Create a set of columns.</summary>
		/// <param name="descriptions">Alias is allowed with the equal seperator.</param>
		/// <returns></returns>
		[LuaMember]
		public IPpsColumnDescriptions CreateColumnDescriptions(object descriptions)
		{
			if (descriptions is IPpsColumnDescriptions columns)
				return columns;
			else if (descriptions is string[] fieldList)
				return CreateColumnDescriptionsFromFields(fieldList);
			else if (descriptions is LuaTable fieldTable)
				return CreateColumnDescriptionsFromTable(fieldTable);
			else
				throw new ArgumentException("Unsupported type.", nameof(descriptions));
		} // func CreateColumnDescriptions

		private static IDataRow CreateRowsCopyFromObject(IDataColumns columnInfo, object o, IDictionary<Type, object> infoCaches)
		{
			switch (o)
			{
				case LuaTable t:
					return GetRowFromTable(columnInfo, t);
				case IPropertyReadOnlyDictionary p:
					return GetRowFromProperties(columnInfo, p);
				case null:
					return new RowCopy(new object[columnInfo.Columns.Count], columnInfo);
				default:
					var typeInfo = o.GetType();
					if (infoCaches.TryGetValue(typeInfo, out var ic))
						return GetRowFromObject(columnInfo, o, ref ic);
					else
					{
						ic = null;
						var r = GetRowFromObject(columnInfo, o, ref ic);
						infoCaches[typeInfo] = ic;
						return r;
					}
			}
		} // func CreateRowsCopyFromObject

		#endregion

		#region -- CreateRows ---------------------------------------------------------

		private static IEnumerable<IDataRow> CreateRowsGeneric(IDataColumns columnInfo, IEnumerable e)
		{
			var infoCaches = new Dictionary<Type, object>();
			foreach (var c in e)
				yield return CreateRowsCopyFromObject(columnInfo, c, infoCaches);
		} // func CreateRowsGeneric

		private static IEnumerable<IDataRow> CreateRowsFromFunction(IDataColumns columnInfo, object func)
		{
			var infoCaches = new Dictionary<Type, object>();
			while (true)
			{
				var r = new LuaResult(Lua.RtInvoke(func));
				var o = r[0];
				if (o == null)
					break;
				yield return CreateRowsCopyFromObject(columnInfo, o, infoCaches);
			}
		} // func CreateRowsFromFunction

		/// <summary></summary>
		/// <param name="columnInfo"></param>
		/// <param name="rowInfo"></param>
		/// <param name="enforceCopy"></param>
		/// <returns></returns>
		/// <exception cref="ArgumentException"></exception>
		[LuaMember]
		public IEnumerable<IDataRow> CreateRows(object columnInfo, object rowInfo, bool enforceCopy)
		{
			var columns = CreateColumnDescriptions(columnInfo);

			if (rowInfo == null)
				return new RowsArray(columns, Array.Empty<IDataRow>());
			else
			{
				IEnumerable<IDataRow> rowEnum;
				if (rowInfo is LuaTable tableArray)
					rowEnum = tableArray.ArrayList.Select(c => GetRowFromGeneric(columns, c));
				else if (rowInfo is IEnumerable<LuaTable> tableEnum)
					rowEnum = tableEnum.Select(t => GetRowFromTable(columns, t));
				else if (rowInfo is IEnumerable<IPropertyReadOnlyDictionary> propertyEnum)
					rowEnum = propertyEnum.Select(p => GetRowFromProperties(columns, p));
				else if (rowInfo is IEnumerable genericEnum)
					rowEnum = CreateRowsGeneric(columns, genericEnum);
				else if (Lua.RtInvokeable(rowInfo))
					rowEnum = CreateRowsFromFunction(columns, rowInfo);
				else
					throw new ArgumentException("Unknown typ for rowInfo.", nameof(rowInfo));

				// create result
				if (enforceCopy)
					return new RowsArray(columns, rowEnum.ToArray());
				else
					return new RowsEnumerable(columns, rowEnum);
			}
		} // func CreateRows

		#endregion

		#region -- GetFirstRow --------------------------------------------------------

		/// <summary>Return first row of a request.</summary>
		/// <param name="rows"></param>
		/// <returns></returns>
		[LuaMember]
		public static IDataRow GetFirstRow(IEnumerable<IDataRow> rows)
			=> CopyRows(rows).FirstOrDefault();

		#endregion

		#region -- GetTable -----------------------------------------------------------

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

		#endregion

		#region -- GetProperty --------------------------------------------------------

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

		#endregion
	} // class PpsApplication
}
