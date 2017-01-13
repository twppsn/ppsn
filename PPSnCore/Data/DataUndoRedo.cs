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
		/// <summary>Is called before the element is persist in the memory.</summary>
		void Freeze();
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

	#region -- class PpsUndoManagerBase -------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Simple undo manager implementation for that holds only one transaction.</summary>
	public class PpsUndoManagerBase : IPpsUndoSink
	{
		#region -- class PpsUndoTransaction -------------------------------------------------

		private abstract class PpsUndoTransaction : IPpsUndoTransaction
		{
			protected readonly string description;
			private bool? commited = null;
			private PpsUndoTransaction blockingTransaction = null;

			public PpsUndoTransaction(string description)
			{
				if (String.IsNullOrEmpty(description))
					throw new ArgumentNullException("description");

				this.description = description;
			} // ctor

			public void Dispose()
			{
				if (!commited.HasValue)
					Rollback();
			} // proc Dispose

			internal void CloseTransaction(bool commit)
			{
				CheckBlockingTransaction();
				if (commited.HasValue)
					throw new ObjectDisposedException(String.Format("Undo Operation: {0}", Description));

				commited = commit;
			} // proc CloseTransaction

			private void CheckBlockingTransaction()
			{
				if (blockingTransaction != null)
					throw new InvalidOperationException(String.Format("Transaction '{0}' is still blocked by '{1}'.", Description, blockingTransaction.Description));
			} // proc CheckBlockingTransaction

			public abstract void Append(IPpsUndoItem item);

			public abstract void Commit();
			public abstract void Rollback();

			internal abstract int GetRollbackIndex();
			internal abstract void RollbackToIndex(int rollbackIndex);
			internal abstract void UpdateCurrentTransaction(PpsUndoTransaction trans);

			internal int LockTransaction(PpsUndoTransaction locked)
			{
				CheckBlockingTransaction();

				blockingTransaction = locked;
				return GetRollbackIndex();
			} // proc LockTransaction

			internal void UnlockTransaction(bool commit, int rollbackIndex)
			{
				if (blockingTransaction == null)
					throw new InvalidOperationException(String.Format("No blocking transaction."));

				// is a rollback needed
				if (!commit)
					RollbackToIndex(rollbackIndex);

				// Close the current transaction
				blockingTransaction.CloseTransaction(commit);
				blockingTransaction = null;
				UpdateCurrentTransaction(this);
			} // proc UnlockTransaction

			public string Description { get { return description; } }
		} // class PpsUndoTransaction

		#endregion

		#region -- class PpsUndoRootTransaction ---------------------------------------------

		private sealed class PpsUndoRootTransaction : PpsUndoTransaction
		{
			private readonly PpsUndoManagerBase manager;
			private readonly List<IPpsUndoItem> items;

			public PpsUndoRootTransaction(PpsUndoManagerBase manager, string description)
				: base(description)
			{
				this.manager = manager;
				this.items = new List<IPpsUndoItem>();
			} // ctor

			public override void Append(IPpsUndoItem item)
				=> items.Add(item);
			
			internal override int GetRollbackIndex()
				=> items.Count;

			internal override void RollbackToIndex(int iRollbackIndex)
			{
				manager.SuspendAppend();
				try
				{
					for (int i = items.Count - 1; i >= iRollbackIndex; i--)
					{
						items[i].Undo();
						items.RemoveAt(i);
					}
				}
				finally
				{
					manager.ResumeAppend();
				}
			} // proc RollbackToIndex

			internal override void UpdateCurrentTransaction(PpsUndoTransaction trans)
			{
				manager.currentUndoTransaction = trans;
			} // proc UpdateCurrentTransaction

			public override void Commit()
			{
				CloseTransaction(true);
				UpdateCurrentTransaction(null);

				items.ForEach(c => c.Freeze());
				manager.Commit(Description, items.ToArray());
			} // proc Commit

			public override void Rollback()
			{
				CloseTransaction(false);
				UpdateCurrentTransaction(null);
				RollbackToIndex(0);
			} // proc Rollback
		} // class PpsUndoRootTransaction

		#endregion

		#region -- class PpsUndoParentTransaction -------------------------------------------

		private sealed class PpsUndoParentTransaction : PpsUndoTransaction
		{
			private readonly PpsUndoTransaction parent;
			private readonly int rollbackIndex;

			public PpsUndoParentTransaction(PpsUndoTransaction parent, string sDescription)
				: base(sDescription)
			{
				this.parent = parent;
				this.rollbackIndex = parent.LockTransaction(this);
			} // ctor

			public override void Append(IPpsUndoItem item)
				=> parent.Append(item);

			internal override void RollbackToIndex(int rollbackIndex)
				=> parent.RollbackToIndex(rollbackIndex);
			internal override int GetRollbackIndex() 
				=> parent.GetRollbackIndex();

			internal override void UpdateCurrentTransaction(PpsUndoTransaction trans) 
				=> parent.UpdateCurrentTransaction(trans);

			public override void Commit()
				=> parent.UnlockTransaction(true, rollbackIndex);

			public override void Rollback()
			 => parent.UnlockTransaction(false, rollbackIndex);
		} // class PpsUndoParentTransaction

		#endregion

		private PpsUndoTransaction currentUndoTransaction = null;
		private int appendSuspended = 0;

		#region -- Append, Clear ----------------------------------------------------------

		public void Append(IPpsUndoItem item)
		{
			if (InUndoRedoOperation)
				return;
			if (currentUndoTransaction == null)
				throw new InvalidOperationException("There is no active transaction.");

			currentUndoTransaction.Append(item); // append new item to current undo transaction
		} // proc Append

		protected void SuspendAppend()
		{
			appendSuspended++;
		} // proc SuspendAppend

		protected void ResumeAppend()
		{
			appendSuspended--; //~todo: throw logic errors
		} // proc SuspendAppend

		protected virtual void Commit(string description, IPpsUndoItem[] undoItems)
		{
		} // proc Commit

		public virtual void Clear()
		{
			if (currentUndoTransaction != null)
				throw new InvalidOperationException("There is an active transaction.");
		} // proc Clear

		void IPpsUndoSink.ResetUndoStack()
			=> Clear();

		#endregion

		#region -- BeginTransaction -------------------------------------------------------

		public IPpsUndoTransaction BeginTransaction(string description)
		{
			currentUndoTransaction =
				currentUndoTransaction == null ?
					new PpsUndoRootTransaction(this, description) :
					currentUndoTransaction = new PpsUndoParentTransaction(currentUndoTransaction, description);

			return currentUndoTransaction;
		} // func BeginTransaction

		#endregion

		/// <summary></summary>
		public bool InTransaction => currentUndoTransaction != null;
		/// <summary></summary>
		public bool InUndoRedoOperation => appendSuspended > 0;
	} // class PpsUndoManagerBase

	#endregion

}