using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TecWare.PPSn.Data
{
	#region -- interface IPpsUndoItem ---------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Basic interface for all undo/redo elements.</summary>
	public interface IPpsUndoItem
	{
		/// <summary>Undo the element.</summary>
		void Undo();
		/// <summary>Redo the element.</summary>
		void Redo();
	} // interface IPpsUndoItem

	#endregion

	#region -- interface IPpsUndoTransaction --------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Records undo operations.</summary>
	public interface IPpsUndoTransaction : IDisposable
	{
		/// <summary>Persist the operation in the undo-stack</summary>
		void Commit();
		/// <summary>Calls undo of all collected operations.</summary>
		void Rollback();
	} // interface IPpsUndoTransaction

	#endregion

	#region -- interface IPpsUndoSink ---------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Implemented by the undo/redo manager to catch all the operations</summary>
	public interface IPpsUndoSink
	{
		/// <summary>Appends a simple undo-item to the current transaction.</summary>
		/// <param name="item"></param>
		void Append(IPpsUndoItem item);
		/// <summary>Undo/redo operation aktive.</summary>
		bool InUndoRedoOperation { get; }

		/// <summary>Opens a new transaction.</summary>
		/// <param name="sDescription">Description of the transaction.</param>
		/// <returns></returns>
		IPpsUndoTransaction BeginTransaction(string sDescription);
		/// <summary>Is a transaction aktive.</summary>
		bool InTransaction { get; }
	} // interface IPpsUndoSink

	#endregion
}
