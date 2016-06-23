using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Neo.IronLua;
using TecWare.DE.Data;

namespace TecWare.PPSn.Server.Data
{
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

		public virtual void ExecuteNoneResult(LuaTable parameter)
		{
			throw new NotImplementedException();
		} // proc ExecuteNoneResult

		public virtual IEnumerable<IDataRow> ExecuteSingleResult(LuaTable parameter)
		{
			throw new NotImplementedException();
		} // proc ExecuteSingleResult

		public virtual IEnumerable<IEnumerable<IDataRow>> ExecuteMultipleResult(LuaTable parameter)
		{
			throw new NotImplementedException();
		} // proc ExecuteMultipleResult

		/// <summary></summary>
		public PpsDataSource DataSource => dataSource;

		/// <summary></summary>
		public bool? IsCommited => commited;
	} // class PpsDataTransaction
}
