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

		/// <summary>Execute a command.</summary>
		/// <param name="parameter"></param>
		public void ExecuteNoneResult(LuaTable parameter)
		{
			foreach (var c in ExecuteResult(parameter, PpsDataTransactionExecuteBehavior.NoResult))
				c.GetEnumerator()?.Dispose();
		} // proc ExecuteNoneResult

		/// <summary>Execute a command.</summary>
		/// <param name="parameter"></param>
		/// <returns></returns>
		public IDataRow ExecuteSingleRow(LuaTable parameter)
		{
			var first = true;
			IDataRow result = null;
			foreach (var c in ExecuteResult(parameter, PpsDataTransactionExecuteBehavior.SingleRow))
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
		} // proc ExecuteSingleRow

		/// <summary>Execute a command.</summary>
		/// <param name="parameter"></param>
		/// <returns></returns>
		public IEnumerable<IDataRow> ExecuteSingleResult(LuaTable parameter)
		{
			var first = true;
			foreach (var c in ExecuteResult(parameter, PpsDataTransactionExecuteBehavior.SingleResult))
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
		} // proc ExecuteSingleResult

		/// <summary>Execute a command.</summary>
		/// <param name="parameter"></param>
		/// <returns></returns>
		public IEnumerable<IEnumerable<IDataRow>> ExecuteMultipleResult(LuaTable parameter)
			=> ExecuteResult(parameter, PpsDataTransactionExecuteBehavior.MutliResult);

		/// <summary>Execute a command.</summary>
		/// <param name="parameter"></param>
		/// <param name="behavior"></param>
		/// <returns></returns>
		protected virtual IEnumerable<IEnumerable<IDataRow>> ExecuteResult(LuaTable parameter, PpsDataTransactionExecuteBehavior behavior)
			=> throw new NotImplementedException();
		
		/// <summary>DataSource of the transaction.</summary>
		public PpsDataSource DataSource => dataSource;
		/// <summary>Connection assigned to this transaction.</summary>
		public IPpsConnectionHandle Connection => connection;

		/// <summary>Is this transaction commited.</summary>
		public bool? IsCommited => commited;
	} // class PpsDataTransaction

	#endregion
}
