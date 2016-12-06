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
		/// <summary>Gets call if one member of the undo sink marks his data invalid.</summary>
		void ResetUndoStack();

		/// <summary>Opens a new transaction.</summary>
		/// <param name="description">Description of the transaction.</param>
		/// <returns></returns>
		IPpsUndoTransaction BeginTransaction(string description);
		/// <summary>Is a transaction aktive.</summary>
		bool InTransaction { get; }
	} // interface IPpsUndoSink

	#endregion
}
