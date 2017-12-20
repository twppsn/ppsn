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
using System.Threading.Tasks;
using Neo.IronLua;
using TecWare.DE.Data;
using TecWare.DE.Stuff;

namespace TecWare.PPSn.Data
{
	#region -- class PpsDataServerProtocolBase ----------------------------------------

	/// <summary>Byte layer for the data server.</summary>
	public abstract class PpsDataServerProtocolBase : IDisposable
	{
		private bool isDisposed = false; // To detect redundant calls

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary></summary>
		protected PpsDataServerProtocolBase()
		{
		} // ctor

		/// <summary></summary>
		/// <param name="disposing"></param>
		protected virtual void Dispose(bool disposing)
		{
			if (!isDisposed)
			{
				isDisposed = true;
			}
		} // proc Disposed

		/// <summary></summary>
		public void Dispose()
			=> Dispose(true);

		#endregion

		#region -- Push/Pop Packets ---------------------------------------------------

		/// <summary>Parses a packet of the input stream.</summary>
		/// <returns>Parsed data or <c>null</c> for EOF.</returns>
		/// <remarks>Should not called directly.</remarks>
		protected abstract Task<LuaTable> PopPacketCoreAsync();

		internal Task<LuaTable> PopPacketAsync()
			=> PopPacketCoreAsync();

		/// <summary>Implementation of the parsing part. This method is already synchronized.</summary>
		/// <param name="t"></param>
		/// <returns></returns>
		/// <remarks>Should not called directly.</remarks>
		protected abstract Task PushPacketCoreAsync(LuaTable t);

		internal Task PushPacketAsync(LuaTable t)
			=> PushPacketCoreAsync(t);

		#endregion

		/// <summary>Is disposed called.</summary>
		public bool IsDisposed => isDisposed;
	} // class PpsDataServerProtocolBase

	#endregion

	#region -- class PpsDataServerProviderBase ----------------------------------------

	/// <summary>Data layer for the data server.</summary>
	public abstract class PpsDataServerProviderBase : IDisposable
	{
		private bool isDisposed = false; // To detect redundant calls

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary></summary>
		protected PpsDataServerProviderBase()
		{
		} // ctor

		/// <summary></summary>
		/// <param name="disposing"></param>
		protected virtual void Dispose(bool disposing)
		{
			if (!isDisposed)
			{
				isDisposed = true;
			}
		} // proc Disposed

		/// <summary></summary>
		public void Dispose()
			=> Dispose(true);

		#endregion

		/// <summary>Access to a list.</summary>
		/// <param name="select">Command to choose the this.</param>
		/// <param name="columns">Columns the should be returned.</param>
		/// <param name="filter">A condition to select the rows.</param>
		/// <param name="order">Row order</param>
		/// <returns></returns>
		public abstract Task<IEnumerable<IDataRow>> GetListAsync(string select, PpsDataColumnExpression[] columns, PpsDataFilterExpression filter, PpsDataOrderExpression[] order);
		/// <summary>Get's a complete dataset.</summary>
		/// <param name="identitfication"></param>
		/// <param name="tableFilter"></param>
		/// <returns></returns>
		public abstract Task<PpsDataSet> GetDataSetAsync(object identitfication, string[] tableFilter);
		/// <summary>Core execution of an command.</summary>
		/// <param name="arguments"></param>
		/// <returns></returns>
		public abstract Task<LuaTable> ExecuteAsync(LuaTable arguments);

		/// <summary>Is the object disposed.</summary>
		public bool IsDisposed => isDisposed;
	} // class PpsDataServerProviderBase

	#endregion

	#region -- class PpsDataServer ----------------------------------------------------

	/// <summary>Basic implementation of an Lua Object Notation based protocol to exchange data over between processes. It is a simple request/answer protocol.</summary>
	public sealed class PpsDataServer : IDisposable
	{
		#region -- class OpenCursor ---------------------------------------------------

		private sealed class OpenCursor : IDisposable
		{
			#region -- enum State -----------------------------------------------------

			private enum State
			{
				BeforeFirst,
				FirstFetch,
				InFetch,
				Finished
			} // enum State

			#endregion

			private readonly int id;
			private readonly IEnumerator<IDataRow> rows;
			private bool isDisposed = false;
			private State state = State.BeforeFirst;

			#region -- Ctor/Dtor ------------------------------------------------------

			public OpenCursor(int id, IEnumerable<IDataRow> rows)
			{
				this.id = id;
				this.rows = rows.GetEnumerator();
			} // ctor

			public void Dispose()
				=> Dispose(true);

			private void Dispose(bool disposing)
			{
				if (!isDisposed)
				{
					rows?.Dispose();
					isDisposed = true;
				}
			} // proc Dispose

			#endregion

			#region -- Move/Descripe --------------------------------------------------

			public LuaTable GetColumnDescriptions(LuaTable columnList)
			{
				if (rows is IDataColumns columns)
				{
					AddColumnInfo(columns.Columns, columnList);
				}
				else if (state == State.BeforeFirst)
				{
					if (rows.MoveNext())
					{
						AddColumnInfo(rows.Current.Columns, columnList);
						state = State.FirstFetch;
					}
					else
					{
						state = State.Finished;
					}
				}
				else if (state == State.FirstFetch || state == State.InFetch)
					AddColumnInfo(rows.Current.Columns, columnList);

				return columnList;
			} // func GetColumnDescriptions

			public bool MoveNext()
			{
				switch (state)
				{
					case State.FirstFetch:
						state = State.InFetch;
						return true;
					case State.BeforeFirst:
					case State.InFetch:
						if (rows.MoveNext())
							return true;
						else
						{
							state = State.Finished;
							return false;
						}
					case State.Finished:
						return false;
					default:
						throw new InvalidOperationException();
				}
			} // func MoveNext

			public LuaTable GetCurrentRow()
				=> GetRowData(new LuaTable(), rows.Current);

			#endregion

			public int Id => id;
			public bool IsDisposed => isDisposed;
		} // class OpenCursor

		#endregion

		private readonly PpsDataServerProtocolBase protocol;
		private readonly PpsDataServerProviderBase provider;
		private readonly Action<Exception> processException;

		private Dictionary<int, OpenCursor> cursors = new Dictionary<int, OpenCursor>();
		private int lastCursorId = 0;
		private bool isDisposed = false;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary>Create the data server.</summary>
		/// <param name="protocol">Protocoll implementation.</param>
		/// <param name="provider">Data provider implementation.</param>
		/// <param name="processException">Callback for exception handling.</param>
		public PpsDataServer(PpsDataServerProtocolBase protocol, PpsDataServerProviderBase provider, Action<Exception> processException)
		{
			this.protocol = protocol ?? throw new ArgumentNullException(nameof(protocol));
			this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
			this.processException = processException ?? throw new ArgumentNullException(nameof(processException));
		} // ctor

		/// <summary>Free managed resources.</summary>
		/// <param name="disposing"></param>
		private void Dispose(bool disposing)
		{
			if (!isDisposed)
			{
				if (disposing)
				{
					foreach (var c in cursors.Values)
						c.Dispose();
					cursors.Clear();

					protocol?.Dispose();
					provider?.Dispose();
				}
				isDisposed = true;
			}
		} // prod Dispose

		/// <summary>Free managed resources.</summary>
		public void Dispose()
			=> Dispose(true);

		#endregion

		#region -- Helper -------------------------------------------------------------

		private static LuaTable GetColumnData(IDataColumn c, LuaTable t)
		{
			t["Name"] = c.Name;
			t["Type"] = LuaType.GetType(c.DataType).AliasOrFullName;

			foreach (var a in c.Attributes)
				t[a.Name] = a.Value;

			return t;
		} // func GetColumnData

		private static LuaTable GetRowData(LuaTable t, IDataRow r)
		{
			for (var i = 0; i < r.Columns.Count; i++)
			{
				var v = r[i];
				if (v != null)
					t[r.Columns[i].Name] = v;
			}
			return t;
		} // func GetRowData

		private static LuaTable AddColumnInfo(IReadOnlyList<IDataColumn> columns, LuaTable columnList)
		{
			foreach (var c in columns)
				columnList.Add(GetColumnData(c, new LuaTable()));

			return columnList;
		} // proc AddColumnInfo

		#endregion

		#region -- Protocol primitives ------------------------------------------------

		private Task<LuaTable> PopPacketAsync()
			=> protocol.PopPacketAsync();

		private async Task PushPacketAsync(LuaTable t)
		{
			packetEmitted = true;
			await protocol.PushPacketAsync(t);
		} // proc PushPacketAsync

		private Task PushErrorAsync(string message)
			=> PushPacketAsync(new LuaTable() { ["error"] = message });

		#endregion

		#region -- Provider primitives ------------------------------------------------

		private OpenCursor GetCursor(LuaTable table)
		{
			var id = Procs.ChangeType<int>(table.GetMemberValue("id") ?? throw new ArgumentNullException("id"));
			lock (cursors)
			{
				if (cursors.TryGetValue(id, out var cursor))
				{
					if (cursor.IsDisposed)
						cursors.Remove(id);
					else
						return cursor;
				}
			}
			throw new ArgumentOutOfRangeException("id", $"Cursor '{id}' not found.");
		} // func TryGetCursor

		private static string[] GetStringArray(LuaTable table, string memberName)
		{
			switch (table.GetMemberValue(memberName))
			{
				case string stringList:
					return stringList.Split(',').Where(s => !String.IsNullOrEmpty(s)).ToArray();
				case LuaTable tableArray:
					return tableArray.ArrayList.Select(o => o.ToString()).ToArray();
				case null:
					return Array.Empty<string>();
				default:
					throw new ArgumentOutOfRangeException(memberName, $"'{memberName}' should be a string list or a lua array.");
			}
		} // func GetStringArray

		private async Task OpenCursorAsync(LuaTable table)
		{
			// not optional
			var selectExpression = table.GetOptionalValue("name", (string)null) ?? throw new ArgumentNullException("name");

			// parse selector
			var selectorExpression = PpsDataFilterExpression.Parse(table.GetMemberValue("filter"));

			// parse optional columns
			var columns = PpsDataColumnExpression.Parse(table.GetMemberValue("columns")).ToArray();

			// parse optional order
			var orderExpressions = PpsDataOrderExpression.Parse(table.GetMemberValue("order")).ToArray();

			// try to get a list
			int cursorId;
			lock (cursors)
				cursorId = ++lastCursorId;

			var cursor = new OpenCursor(cursorId, await provider.GetListAsync(selectExpression, columns, selectorExpression, orderExpressions));

			lock (cursors)
				cursors.Add(cursorId, cursor);

			// return the cursor id
			await PushPacketAsync(new LuaTable() { ["id"] = cursorId });
		} // proc OpenCursorAsync

		private async Task DescripeCursorAsync(LuaTable table)
		{
			var cursor = GetCursor(table);
			
			await PushPacketAsync(new LuaTable()
			{
				["id"] = cursor.Id,
				["columns"] = cursor.GetColumnDescriptions(new LuaTable())
			});
		} // proc DescripeCursorAsync

		private async Task NextCursorAsync(LuaTable table)
		{
			var cursor = GetCursor(table);

			if (cursor.MoveNext())
			{
				await PushPacketAsync(new LuaTable()
				{
					["id"] = cursor.Id,
					["row"] = cursor.GetCurrentRow()
				});
			}
			else
				await PushPacketAsync(new LuaTable() { ["id"] = cursor.Id });
		} // proc NextCursorAsync

		private async Task NextCursorCountAsync(LuaTable table)
		{
			// get parameter
			var cursor = GetCursor(table);
			var count = Procs.ChangeType<int>(table.GetMemberValue("count") ?? -1);

			// collect rows
			var rows = new LuaTable();
			while (count-- > 0 && cursor.MoveNext())
				rows.Add(cursor.GetCurrentRow());

			// send result
			await PushPacketAsync(new LuaTable()
			{
				["id"] = cursor.Id,
				["row"] = rows.Length == 0 ? null : rows
			});
		} // proc NextCursorCountAsync

		private async Task CloseCursorAsync(LuaTable table)
		{
			var cursor = GetCursor(table);

			lock (cursors)
				cursors.Remove(cursor.Id);
			cursor.Dispose();

			await PushPacketAsync(new LuaTable() { ["id"] = cursor.Id });
		} // proc CloseCursorAsync

		private async Task GetDataSetAsync(LuaTable table)
		{
			var identification = table.GetMemberValue("id") ?? throw new ArgumentNullException("id");
			var tableFilter = GetStringArray(table, "filter");

			var dataset = await provider.GetDataSetAsync(identification, tableFilter);
			var result = new LuaTable();

			foreach (var tab in dataset.Tables)
			{
				if (tableFilter.Length != 0
					&& Array.Exists(tableFilter, c => String.Compare(c, tab.TableName, StringComparison.OrdinalIgnoreCase) != 0))
					continue;

				var resultTable = new LuaTable();

				var columnList = new LuaTable();
				foreach (var col in tab.Columns)
				{
					var resultColumn = GetColumnData(col, new LuaTable());

					if (col.IsRelationColumn)
					{
						resultColumn["parentReleation"] = new LuaTable()
						{
							["table"] = col.ParentColumn.ParentColumn.Table.Name,
							["column"] = col.ParentColumn.ParentColumn.Name
						};
					}

					columnList.Add(resultColumn);
				}

				resultTable["columns"] = columnList;
				
				// convert rows
				foreach (var row in tab)
					resultTable.Add(GetRowData(new LuaTable(), row));

				result[tab.TableName] = resultTable;
			}
			
			await PushPacketAsync(result);
		} // proc ExecuteAsync

		private async Task ExecuteAsync(LuaTable table)
			=> await PushPacketAsync(await provider.ExecuteAsync(table));
	
		#endregion

		#region -- Protocol implementation --------------------------------------------

		private bool packetEmitted = false;

		private async Task ExecuteSafeAsync(Func<LuaTable, Task> task, LuaTable packet)
		{
			packetEmitted = false;
			try
			{
				await task(packet);
			}
			catch (Exception e)
			{
				await PushErrorAsync(e.Message);
				processException.Invoke(e);
			}
			finally
			{
				if (!packetEmitted)
					await PushErrorAsync("No result.");
			}
		} // proc ExecuteSafeAsync

		private async Task RunProtocol()
		{
			LuaTable packet;
			while ((packet = await PopPacketAsync()) != null)
			{
				var cmd = packet.GetMemberValue("cmd");
				if (cmd is string cmdText)
				{
					switch (cmdText)
					{
						case "open": // open a datastream
							await ExecuteSafeAsync(OpenCursorAsync, packet);
							break;
						case "desc":
							await ExecuteSafeAsync(DescripeCursorAsync, packet);
							break;
						case "next": // next data row
							await ExecuteSafeAsync(NextCursorAsync, packet);
							break;
						case "nextc": // next data rows
							await ExecuteSafeAsync(NextCursorCountAsync, packet);
							break;
						case "close": // close data stream
							await ExecuteSafeAsync(CloseCursorAsync, packet);
							break;

						case "data": // dataset
							await ExecuteSafeAsync(GetDataSetAsync, packet);
							break;

						case "exec": // executes a function with one result
							await ExecuteSafeAsync(ExecuteAsync, packet);
							break;

						case "null": // nothing
							await PushPacketAsync(new LuaTable() { ["id"] = -1 });
							break;

						default: // unknown command
							await PushErrorAsync($"Unknown command ('{cmd}').");
							break;
					}
				}
				else
					await PushErrorAsync("'cmd' is missing.");
			}
		} // func RunProtocol

		/// <summary>Process protocol messages.</summary>
		/// <returns></returns>
		public Task ProcessMessagesAsync()
			=> RunProtocol();

		#endregion

		/// <summary>Get the protocol implementation.</summary>
		public PpsDataServerProtocolBase Protocol => protocol;
		/// <summary>Get the data provider implementation.</summary>
		public PpsDataServerProviderBase Provider => provider;
	} // class PpsDataServerBase

	#endregion
}
