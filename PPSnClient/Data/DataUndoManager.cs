using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TecWare.PPSn.Data
{
	#region -- enum PpsUndoStepType -----------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public enum PpsUndoStepType
	{
		/// <summary></summary>
		Undo,
		/// <summary></summary>
		Redo
	} // enum PpsUndoStepType

	#endregion

	#region -- interface IPpsUndoStep ---------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public interface IPpsUndoStep
	{
		/// <summary></summary>
		void Goto();

		/// <summary></summary>
		PpsUndoStepType Type { get; }
		/// <summary></summary>
		string Description { get; }
	} // interface IPpsUndoStep

	#endregion

	#region -- class PpsUndoManager -----------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Manages the undo/redo elements.</summary>
	/// ~todo: Reset,Commit,Rollback müssen UndoManager ändern können, sonst ist er invalid
	public sealed class PpsUndoManager : IPpsUndoSink, IEnumerable<IPpsUndoStep>
	{
		#region -- class PpsUndoGroup -------------------------------------------------------

		private sealed class PpsUndoGroup : IPpsUndoStep
		{
			private PpsUndoManager manager;
			private string description;
			private IPpsUndoItem[] items;

			public PpsUndoGroup(PpsUndoManager manager, string description, IPpsUndoItem[] items)
			{
				this.manager = manager;
				this.description = description;
				this.items = items;
			} // ctor

			public void Goto()
			{
				int index = manager.items.IndexOf(this);
				if (index <= -1)
					throw new InvalidOperationException("Undo group has no manager.");
				else if (index < manager.undoBorder)
					manager.Undo(manager.undoBorder - index);
				else
					manager.Redo(index - manager.undoBorder + 1);
			} // proc Goto

			internal void Undo()
			{
				for (int i = items.Length - 1; i >= 0; i--)
					items[i].Undo();
			} // proc Undo

			internal void Redo()
			{
				for (int i = 0; i < items.Length; i++)
					items[i].Redo();
			} // proc Redo

			public PpsUndoStepType Type
			{
				get
				{
					int iIndex = manager.items.IndexOf(this);
					if (iIndex <= -1)
						throw new InvalidOperationException("Undo group has no manager.");
					else if (iIndex < manager.undoBorder)
						return PpsUndoStepType.Undo;
					else
						return PpsUndoStepType.Redo;
				}
			} // prop Type

			public string Description { get { return description; } }
		} // class PpsUndoGroup

		#endregion

		#region -- class PpsUndoTransaction -------------------------------------------------

		private abstract class PpsUndoTransaction : IPpsUndoTransaction
		{
			protected string description;
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

			internal void CloseTransaction(bool lCommit)
			{
				CheckBlockingTransaction();
				if (commited.HasValue)
					throw new ObjectDisposedException(String.Format("Undo Operation: {0}", Description));

				commited = lCommit;
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
			internal abstract void RollbackToIndex(int iRollbackIndex);
			internal abstract void UpdateCurrentTransaction(PpsUndoTransaction trans);

			internal int LockTransaction(PpsUndoTransaction locked)
			{
				CheckBlockingTransaction();

				blockingTransaction = locked;
				return GetRollbackIndex();
			} // proc LockTransaction

			internal void UnlockTransaction(bool lCommit, int iRollbackIndex)
			{
				if (blockingTransaction == null)
					throw new InvalidOperationException(String.Format("No blocking transaction."));

				// is a rollback needed
				if (!lCommit)
					RollbackToIndex(iRollbackIndex);

				// Close the current transaction
				blockingTransaction.CloseTransaction(lCommit);
				UpdateCurrentTransaction(this);
			} // proc UnlockTransaction

			public string Description { get { return description; } }
		} // class PpsUndoTransaction

		#endregion

		#region -- class PpsUndoRootTransaction ---------------------------------------------

		private sealed class PpsUndoRootTransaction : PpsUndoTransaction
		{
			private PpsUndoManager manager;
			private List<IPpsUndoItem> items;

			public PpsUndoRootTransaction(PpsUndoManager manager, string sDescription)
				: base(sDescription)
			{
				this.manager = manager;
				this.items = new List<IPpsUndoItem>();
			} // ctor

			public override void Append(IPpsUndoItem item)
			{
				items.Add(item);
			} // proc Append

			internal override int GetRollbackIndex()
			{
				return items.Count;
			} // func GetRollbackIndex

			internal override void RollbackToIndex(int iRollbackIndex)
			{
				for (int i = items.Count - 1; i >= iRollbackIndex; i--)
					items[i].Undo();
			} // proc RollbackToIndex

			internal override void UpdateCurrentTransaction(PpsUndoTransaction trans)
			{
				manager.currentUndoTransaction = trans;
			} // proc UpdateCurrentTransaction

			public override void Commit()
			{
				CloseTransaction(true);
				UpdateCurrentTransaction(null);
				manager.AppendUndoGroup(new PpsUndoGroup(manager, Description, items.ToArray()));
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
			private PpsUndoTransaction parent;
			private int rollbackIndex;

			public PpsUndoParentTransaction(PpsUndoTransaction parent, string sDescription)
				: base(sDescription)
			{
				this.parent = parent;
				this.rollbackIndex = parent.LockTransaction(this);
			} // ctor

			public override void Append(IPpsUndoItem item)
			{
				parent.Append(item);
			} // proc Append

			internal override void RollbackToIndex(int rollbackIndex) { parent.RollbackToIndex(rollbackIndex); }
			internal override int GetRollbackIndex() { return parent.GetRollbackIndex(); }
			internal override void UpdateCurrentTransaction(PpsUndoTransaction trans) { parent.UpdateCurrentTransaction(trans); }

			public override void Commit()
			{
				parent.UnlockTransaction(true, rollbackIndex);
			} // proc Commit

			public override void Rollback()
			{
				parent.UnlockTransaction(false, rollbackIndex);
			} // proc Rollback
		} // class PpsUndoParentTransaction

		#endregion

		public event EventHandler CanUndoChanged;
		public event EventHandler CanRedoChanged;

		private List<PpsUndoGroup> items = new List<PpsUndoGroup>();
		private int undoBorder = 0;

		private PpsUndoTransaction currentUndoTransaction = null;
		private int appendSuspended = 0;

		#region -- Ctor/Dtor --------------------------------------------------------------

		public PpsUndoManager()
		{
		} // ctor

		#endregion

		#region -- Append -----------------------------------------------------------------

		public void Append(IPpsUndoItem item)
		{
			if (InUndoRedoOperation)
				return;
			if (currentUndoTransaction == null)
				throw new InvalidOperationException("There is no active transaction.");

			currentUndoTransaction.Append(item); // append new item to current undo transaction
		} // proc Append

		private void SuspendAppend()
		{
			appendSuspended++;
		} // proc SuspendAppend

		private void ResumeAppend()
		{
			appendSuspended--; //~todo: throw logic errors
		} // proc SuspendAppend

		private void AppendUndoGroup(PpsUndoGroup undoGroup)
		{
			// Remove undo commands
			items.RemoveRange(undoBorder, items.Count - undoBorder);

			// add the undo item
			items.Add(undoGroup);
			undoBorder++;

			RaiseCanUndo();
			RaiseCanRedo();
		} // proc AppendUndoGroup

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

		#region -- Undo/Redo --------------------------------------------------------------

		public void Undo(int count = 1)
		{
			if (!CanUndo)
				return;
			if (count != 1) //~todo: für mehrere
				throw new ArgumentOutOfRangeException("count");

			SuspendAppend();
			try
			{
				items[undoBorder - 1].Undo();
				undoBorder--;
			}
			finally
			{
				ResumeAppend();
			}

			RaiseCanRedo();
			RaiseCanUndo();
		} // proc Undo

		public void Redo(int count = 1)
		{
			if (!CanRedo)
				return;
			if (count != 1) //~todo: für mehrere
				throw new ArgumentOutOfRangeException("count");

			SuspendAppend();
			try
			{
				items[undoBorder].Redo();
				undoBorder++;
			}
			finally
			{
				ResumeAppend();
			}

			RaiseCanRedo();
			RaiseCanUndo();
		} // proc Redo

		private void RaiseCanUndo()
		{
			if (CanUndoChanged != null)
				CanUndoChanged(this, EventArgs.Empty);
		} // proc RaiseCanUndo

		private void RaiseCanRedo()
		{
			if (CanRedoChanged != null)
				CanRedoChanged(this, EventArgs.Empty);
		} // proc RaiseCanRedo

		#endregion

		#region -- IEnumerator ------------------------------------------------------------

		public IEnumerator<IPpsUndoStep> GetEnumerator()
		{
			foreach (var c in items)
				yield return c;
		} // func GetEnumerator

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() { return GetEnumerator(); }

		#endregion

		/// <summary></summary>
		public bool CanUndo { get { return undoBorder > 0; } }
		/// <summary></summary>
		public bool CanRedo { get { return undoBorder < items.Count; } }

		/// <summary></summary>
		public bool InTransaction { get { return currentUndoTransaction != null; } }
		/// <summary></summary>
		public bool InUndoRedoOperation { get { return appendSuspended > 0; } }
	} // class PpsUndoManager

	#endregion
}
