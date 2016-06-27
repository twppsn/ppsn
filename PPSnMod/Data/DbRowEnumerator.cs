﻿#region -- copyright --
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
using TecWare.DE.Stuff;
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
			FetchRows,
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
			private readonly IPropertyEnumerableDictionary attributes;

			#region -- Ctor/Dtor --------------------------------------------------------------

			public DbDataColumn(string name, Type dataType, IPropertyEnumerableDictionary attributes)
			{
				this.name = name;
				this.dataType = dataType;
				this.attributes = attributes;
			} // ctor

			#endregion

			#region -- IDataColumn ------------------------------------------------------------

			public string Name => name;
			public Type DataType => dataType;
			public IPropertyEnumerableDictionary Attributes => attributes;

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

		private bool isDisposed;
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

				if (state == ReadingState.Unread && !MoveNext(true))
					return null;

				if (state == ReadingState.Complete)
					return null;
				
				var tmp = new DbDataColumn[reader.FieldCount];
				for (var i = 0; i < reader.FieldCount; i++)
					tmp[i] = new DbDataColumn(reader.GetName(i), reader.GetFieldType(i), PropertyDictionary.EmptyReadOnly);
				return tmp;
			});
		} // ctor

		public void Dispose()
			=> Dispose(true);

		private void Dispose(bool disposing)
		{
			if (isDisposed)
				return;

			if (disposing)
			{
				command?.Dispose();
				reader?.Dispose();
			}

			isDisposed = true;
		} // proc Dispose

		#endregion

		private void CheckDisposed()
		{
			if (isDisposed)
				throw new ObjectDisposedException(typeof(DbRowEnumerator).FullName);
		} // proc CheckDisposed

		#region -- IEnumerator<T> ---------------------------------------------------------
		
		private bool MoveNext(bool headerOnly)
		{
			CheckDisposed();

			switch (state)
			{
				case ReadingState.Unread:
					if (reader == null)
						reader = command.ExecuteReader(CommandBehavior.SingleResult);

					state = ReadingState.FetchRows;
					if (headerOnly)
						return true;
					else
						goto case ReadingState.FetchRows;

				case ReadingState.FetchRows:
					if (!reader.Read())
						goto case ReadingState.Complete;

					var values = new object[reader.FieldCount];
					for (var i = 0; i < reader.FieldCount; i++)
						values[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);
					currentRow = new DbDataRow(this, values);

					return true;

				case ReadingState.Complete:
					state = ReadingState.Complete;
					currentRow = null;
					return false;
				default:
					throw new InvalidOperationException("The state of the object is invalid.");
			} // switch state
		} // func MoveNext

		public bool MoveNext()
			=> MoveNext(false);

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
				if (state != ReadingState.FetchRows)
					throw new InvalidOperationException("The state of the object forbids the retrieval of this property.");
				return currentRow;
			}
		} // prop Current

		object IEnumerator.Current => Current;

		#endregion

		#region -- IDataColumns -----------------------------------------------------------

		public IDataColumn[] Columns
		{
			get
			{
				CheckDisposed();
				return columns.Value;
			}
		} // prop Columns

		public int ColumnCount => Columns?.Length ?? 0;

		#endregion
	} // class DbRowEnumerator
}