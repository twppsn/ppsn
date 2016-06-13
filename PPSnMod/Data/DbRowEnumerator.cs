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
using System.Data.Common;
using TecWare.DE.Data;
using TecWare.PPSn.Server.Sql;

namespace TecWare.PPSn.Server.Data
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class DbRowEnumerator : IEnumerator<IDataRow>, IDataColumns
	{
		#region -- enum ReadingState ------------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private enum ReadingState
		{
			Unread,
			Partly,
			Complete,
		} // enum ReadingState

		#endregion

		#region -- class DbDataColumn -----------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class DbDataColumn : IDataColumn
		{
			private readonly string name;
			private readonly Type dataType;
			private readonly IDataColumnAttributes attributes;

			#region -- Ctor/Dtor --------------------------------------------------------------

			public DbDataColumn(string name, Type dataType, IDataColumnAttributes attributes)
			{
				this.name = name;
				this.dataType = dataType;
				this.attributes = attributes;
			} // ctor

			#endregion

			#region -- IDataColumn ------------------------------------------------------------

			public string Name => name;
			public Type DataType => dataType;
			public IDataColumnAttributes Attributes => attributes;

			#endregion
		} // class DbDataColumn

		#endregion

		#region -- class DbDataRow --------------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class DbDataRow : DynamicDataRow
		{
			private readonly DbRowEnumerator enumerator;
			private readonly object[] values;

			#region -- Ctor/Dtor --------------------------------------------------------------

			public DbDataRow(DbRowEnumerator enumerator, object[] values)
			{
				this.enumerator = enumerator;
				this.values = values;
			} // ctor

			#endregion

			#region -- override ---------------------------------------------------------------

			public override object this[int index] => values[index];

			public override IDataColumn[] Columns => enumerator.Columns;
			public override int ColumnCount => enumerator.ColumnCount;

			#endregion
		} // class DbDataRow

		#endregion

		private bool disposed;
		private readonly DbCommand command;
		private DbDataReader reader;
		private ReadingState state;
		private IDataRow currentRow;
		private Lazy<IDataColumn[]> columns;

		#region -- Ctor/Dtor --------------------------------------------------------------

		public DbRowEnumerator(DbCommand command)
		{
			this.command = command;

			columns = new Lazy<IDataColumn[]>(() =>
			{
				CheckDisposed();

				if (state == ReadingState.Unread && !MoveNext())
					return null;

				if (state != ReadingState.Partly)
					return null;

				var tmp = new DbDataColumn[reader.FieldCount];
				for (var i = 0; i < reader.FieldCount; i++)
					tmp[i] = new DbDataColumn(reader.GetName(i), reader.GetFieldType(i), null);
				return tmp;
			});
		} // ctor

		public void Dispose()
		{
			Dispose(true);
		} // proc Dispose

		private void Dispose(bool disposing)
		{
			if (disposed)
				return;

			if (disposing)
			{
				command?.Dispose();
				reader?.Dispose();
			}

			disposed = true;
		} // proc Dispose

		#endregion

		private void CheckDisposed()
		{
			if (disposed)
				throw new ObjectDisposedException(typeof(DbRowEnumerator).FullName);
		} // proc CheckDisposed

		#region -- IEnumerator ------------------------------------------------------------

		public bool MoveNext()
		{
			CheckDisposed();

			switch (state)
			{
				case ReadingState.Unread:
					if (reader == null)
						reader = command.ExecuteReader(CommandBehavior.SingleResult);

					if (!reader.Read())
						goto case ReadingState.Complete;

					goto case ReadingState.Partly;
				case ReadingState.Partly:
					if (state == ReadingState.Partly)
					{
						if (!reader.Read())
							goto case ReadingState.Complete;
					}
					else
						state = ReadingState.Partly;

					var values = new object[reader.FieldCount];
					for (var i = 0; i < reader.FieldCount; i++)
						values[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);
					currentRow = new DbDataRow(this, values);
					return true;
				case ReadingState.Complete:
					state = ReadingState.Complete;
					// todo: Dispose command and reader?
					currentRow = null;
					return false;
				default:
					throw new InvalidOperationException("The state of the object is invalid.");
			} // switch state
		} // func MoveNext

		void IEnumerator.Reset()
		{
			CheckDisposed();
			if (state != ReadingState.Unread)
				throw new InvalidOperationException("The state of the object forbids the calling of this method.");
		} // proc Reset

		public IDataRow Current
		{
			get
			{
				CheckDisposed();
				if (state != ReadingState.Partly)
					throw new InvalidOperationException("The state of the object forbids the retrieval of this property.");
				return currentRow;
			}
		} // prop Current

		object IEnumerator.Current => Current;

		#endregion

		#region -- IDataColumns -----------------------------------------------------------

		public IDataColumn[] Columns => columns.Value;
		public int ColumnCount => Columns.Length;

		#endregion
	} // class DbRowEnumerator
}