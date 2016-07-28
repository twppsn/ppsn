using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Neo.IronLua;
using TecWare.DE.Data;
using TecWare.DE.Stuff;

namespace TecWare.PPSn.Server.Data
{
	public enum PpsDataTransactionExecuteBehavior
	{
		NoResult,
		SingleRow,
		SingleResult,
		MutliResult
	} // enum PpsDataTransactionExecuteBehavior

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PpsDataTransaction : IDisposable
	{
		private readonly PpsDataSource dataSource;

		private bool? commited = null;

		#region -- Ctor/Dtor --------------------------------------------------------------

		public PpsDataTransaction(PpsDataSource dataSource)
		{
			this.dataSource = dataSource;
		} // ctor

		public void Dispose()
		{
			Dispose(true);
		} // proc Dispose

		protected virtual void Dispose(bool disposing)
		{
			if (disposing)
			{
				if (commited == null)
					Rollback();
			}
		} // proc Dispose

		#endregion

		public virtual void Commit()
		{
			commited = true;
		} // proc Commit

		public virtual void Rollback()
		{
			commited = false;
		} // proc Rollback

		public void ExecuteNoneResult(LuaTable parameter)
		{
			foreach (var c in ExecuteResult(parameter, PpsDataTransactionExecuteBehavior.NoResult))
				c.GetEnumerator()?.Dispose();
		} // proc ExecuteNoneResult

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

		public IEnumerable<IEnumerable<IDataRow>> ExecuteMultipleResult(LuaTable parameter)
			=> ExecuteResult(parameter, PpsDataTransactionExecuteBehavior.MutliResult);

		protected virtual IEnumerable<IEnumerable<IDataRow>> ExecuteResult(LuaTable parameter, PpsDataTransactionExecuteBehavior behavior)
		{
			throw new NotImplementedException();
		} // func ExecuteResult


		/// <summary></summary>
		public PpsDataSource DataSource => dataSource;

		/// <summary></summary>
		public bool? IsCommited => commited;
	} // class PpsDataTransaction
}
