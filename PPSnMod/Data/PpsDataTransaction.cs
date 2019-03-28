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

namespace TecWare.PPSn.Server.Data
{
	#region -- enum PpsDataTransactionExecuteBehavior ---------------------------------

	/// <summary>Descripes the resultset of an data function.</summary>
	public enum PpsDataTransactionExecuteBehavior
	{
		/// <summary>No result expected.</summary>
		NoResult,
		/// <summary>A single row is expected.</summary>
		SingleRow,
		/// <summary>A single result is expected.</summary>
		SingleResult,
		/// <summary>Multiple results are expected.</summary>
		MutliResult
	} // enum PpsDataTransactionExecuteBehavior

	#endregion

	#region -- class PpsDataCommand ---------------------------------------------------

	/// <summary>Transaction command representation.</summary>
	public abstract class PpsDataCommand : IDisposable
	{
		private readonly PpsDataTransaction transaction;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary></summary>
		/// <param name="transaction"></param>
		protected PpsDataCommand(PpsDataTransaction transaction)
		{
			this.transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
		} // ctor

		/// <summary></summary>
		public void Dispose()
			=> Dispose(true);
		
		/// <summary></summary>
		/// <param name="disposing"></param>
		protected virtual void Dispose(bool disposing)
		{
		} // proc Dispose

		#endregion
		
		#region -- Execute Result -----------------------------------------------------

		/// <summary>Overwrite to execute command.</summary>
		/// <param name="args">Arguments for the command</param>
		/// <param name="behavior">Result behaviour.</param>
		/// <returns></returns>
		protected abstract IEnumerable<IEnumerable<IDataRow>> ExecuteResultCore(object args, PpsDataTransactionExecuteBehavior behavior);

		private IEnumerable<IEnumerable<IDataRow>> ExecuteResult(object args, PpsDataTransactionExecuteBehavior behavior)
		{
			transaction.ResetTransaction();
			return ExecuteResultCore(args, behavior);
		} // func ExecuteResult

		/// <summary>Execute the command with no result.</summary>
		/// <param name="args"></param>
		public void ExecuteNoneResult(object args)
		{
			foreach (var c in ExecuteResult(args, PpsDataTransactionExecuteBehavior.NoResult))
				c.GetEnumerator()?.Dispose();
		} // func ExecuteNoneResult

		/// <summary>Execute the command and return one row.</summary>
		/// <param name="args"></param>
		/// <returns></returns>
		public IDataRow ExecuteSingleRow(object args)
		{
			var first = true;
			IDataRow result = null;
			foreach (var c in ExecuteResult(args, PpsDataTransactionExecuteBehavior.SingleRow))
			{
				if (first)
				{
					using (var r = c.GetEnumerator())
					{
						if (r.MoveNext())
							result = new SimpleDataRow(r.Current);
					}
					first = false;
				}
				else
					c.GetEnumerator()?.Dispose();
			}
			return result;
		} // func ExecuteSingleRow

		/// <summary>Execute command and return one result set.</summary>
		/// <param name="args"></param>
		/// <returns></returns>
		public IEnumerable<IDataRow> ExecuteSingleResult(object args)
		{
			var first = true;
			foreach (var c in ExecuteResult(args, PpsDataTransactionExecuteBehavior.SingleResult))
			{
				if (first)
				{
					foreach (var r in c)
						yield return r;

					first = false;
				}
				else
					c.GetEnumerator()?.Dispose();
			}
		} // func ExecuteSingleResult

		/// <summary>Execute command and return all results.</summary>
		/// <param name="args"></param>
		/// <returns></returns>
		public IEnumerable<IEnumerable<IDataRow>> ExecuteMultipleResult(object args)
			=> ExecuteResult(args, PpsDataTransactionExecuteBehavior.MutliResult);

		#endregion

		/// <summary></summary>
		public PpsDataTransaction Transaction => transaction;
	} // class PpsDataCommand

	#endregion

	#region -- class PpsInvokeDataCommand ---------------------------------------------

	/// <summary></summary>
	/// <param name="args"></param>
	/// <param name="behavior"></param>
	public delegate IEnumerable<IEnumerable<IDataRow>> PpsInvokeDataCommandDelegate(object args, PpsDataTransactionExecuteBehavior behavior);

	/// <summary></summary>
	public sealed class PpsInvokeDataCommand : PpsDataCommand
	{
		private readonly PpsInvokeDataCommandDelegate invoke;

		/// <summary></summary>
		/// <param name="invoke"></param>
		public PpsInvokeDataCommand(PpsInvokeDataCommandDelegate invoke)
			: base((PpsDataTransaction)invoke.Target)
		{
			this.invoke = invoke ?? throw new ArgumentNullException(nameof(invoke));
		} // ctor

		/// <summary></summary>
		/// <param name="args"></param>
		/// <param name="behavior"></param>
		/// <returns></returns>
		protected override IEnumerable<IEnumerable<IDataRow>> ExecuteResultCore(object args, PpsDataTransactionExecuteBehavior behavior)
			=> invoke.Invoke(args, behavior);
	} // class PpsInvokeDataCommand

	#endregion

	#region -- class PpsDataTransaction -----------------------------------------------

	/// <summary>Base class for transaction based access to a data source.</summary>
	public abstract class PpsDataTransaction : IDisposable
	{
		private readonly PpsDataSource dataSource;
		private readonly IPpsConnectionHandle connection;

		private bool? commited = null;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary></summary>
		/// <param name="dataSource"></param>
		/// <param name="connection"></param>
		public PpsDataTransaction(PpsDataSource dataSource, IPpsConnectionHandle connection)
		{
			this.dataSource = dataSource;
			this.connection = connection;
		} // ctor

		/// <summary>Close transaction.</summary>
		public void Dispose()
			=> Dispose(true);

		/// <summary>Close transaction and rollback, if no commit is called.</summary>
		/// <param name="disposing"></param>
		protected virtual void Dispose(bool disposing)
		{
			if (disposing)
			{
				if (commited == null)
					Rollback();
			}
		} // proc Dispose

		#endregion

		#region -- Commit, Rollback ---------------------------------------------------

		/// <summary>Start a new transaction.</summary>
		protected internal virtual void ResetTransaction()
			=> commited = null;

		/// <summary>Commit the transaction.</summary>
		public virtual void Commit()
		{
			commited = true;
		} // proc Commit

		/// <summary>Rollback the transaction.</summary>
		public virtual void Rollback()
		{
			commited = false;
		} // proc Rollback

		#endregion

		#region -- Prepare/Execute ----------------------------------------------------

		/// <summary></summary>
		/// <param name="parameter"></param>
		/// <param name="firstArgs"></param>
		/// <returns></returns>
		public PpsDataCommand Prepare(LuaTable parameter, LuaTable firstArgs)
		{
			if (parameter.GetMemberValue("rows") != null)
				throw new ArgumentNullException("rows", "Prepare does not support 'rows'.");

			return PrepareCore(parameter, firstArgs);
		} // func Prepare

		/// <summary>Prepare a command.</summary>
		/// <param name="parameter"></param>
		/// <param name="firstArgs"></param>
		/// <returns></returns>
		protected virtual PpsDataCommand PrepareCore(LuaTable parameter, LuaTable firstArgs)
			=> throw new ArgumentOutOfRangeException(nameof(parameter), "Parameter not supported.");

		private (IEnumerator<object>, PpsDataCommand) PrepareWithData(LuaTable parameter)
		{
			if (parameter.GetMemberValue("rows") is IEnumerable<IDataRow> rows) // from datatable or other row source
			{
				var rowEnumerator = rows.GetEnumerator();
				if (rows is IDataColumns columns)
				{
					rowEnumerator.MoveNext();
				}
				else
				{
					if (!rowEnumerator.MoveNext()) // no arguments defined
					{
						rowEnumerator.Dispose();
						return (null, null); // silent return nothing
					}

					columns = rowEnumerator.Current as IDataColumns;
				}

				// create columns list parameter
				parameter["columnList"] = columns ?? throw new ArgumentException("IDataColumns not implemented.", nameof(parameter));

				return (rowEnumerator, PrepareCore(parameter, null));
			}
			else
			{
				var rowEnumerator = parameter.ArrayList.OfType<LuaTable>().GetEnumerator();
				if (!rowEnumerator.MoveNext())
				{
					rowEnumerator.Dispose();
					return (null, PrepareCore(parameter, null));
				}

				return (rowEnumerator, PrepareCore(parameter, rowEnumerator.Current));
			}
		} // func PrepareWithData

		/// <summary>Execute a command.</summary>
		/// <param name="parameter"></param>
		public void ExecuteNoneResult(LuaTable parameter)
		{
			var (data, cmd) = PrepareWithData(parameter);
			try
			{
				do
				{
					cmd.ExecuteNoneResult(data?.Current);
				} while (data != null && data.MoveNext());
			}
			finally
			{
				data?.Dispose();
				cmd?.Dispose();
			}
		} // proc ExecuteNoneResult

		/// <summary>Execute a command.</summary>
		/// <param name="parameter"></param>
		/// <returns></returns>
		public IDataRow ExecuteSingleRow(LuaTable parameter)
		{
			var (data, cmd) = PrepareWithData(parameter);
			try
			{
				return cmd.ExecuteSingleRow(data?.Current);
			}
			finally
			{
				data?.Dispose();
				cmd?.Dispose();
			}
		} // proc ExecuteSingleRow

		/// <summary>Execute a command.</summary>
		/// <param name="parameter"></param>
		/// <returns></returns>
		public IEnumerable<IDataRow> ExecuteSingleResult(LuaTable parameter)
		{
			var (data, cmd) = PrepareWithData(parameter);
			try
			{
				do
				{
					foreach (var cur in cmd.ExecuteSingleResult(data?.Current))
						yield return cur;
				} while (data != null && data.MoveNext());
			}
			finally
			{
				data?.Dispose();
				cmd?.Dispose();
			}
		} // proc ExecuteSingleResult

		/// <summary>Execute a command.</summary>
		/// <param name="parameter"></param>
		/// <returns></returns>
		public IEnumerable<IEnumerable<IDataRow>> ExecuteMultipleResult(LuaTable parameter)
		{
			var (data, cmd) = PrepareWithData(parameter);
			try
			{
				do
				{
					foreach (var i in cmd.ExecuteMultipleResult(data?.Current))
						yield return i;
				} while (data != null && data.MoveNext());
			}
			finally
			{
				data?.Dispose();
				cmd?.Dispose();
			}
		} // func ExecuteMultipleResult

		#endregion

		/// <summary>Create a selector for a view or table.</summary>
		/// <param name="selectorName"></param>
		/// <returns></returns>
		public PpsDataSelector CreateSelector(string selectorName)
			=> DataSource.CreateSelector(connection, selectorName);

		/// <summary>DataSource of the transaction.</summary>
		public PpsDataSource DataSource => dataSource;
		/// <summary>Connection assigned to this transaction.</summary>
		public IPpsConnectionHandle Connection => connection;

		/// <summary>Is this transaction commited.</summary>
		public bool? IsCommited => commited;
	} // class PpsDataTransaction

	#endregion
}
