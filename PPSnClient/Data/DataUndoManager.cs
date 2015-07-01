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
			private string sDescription;
			private IPpsUndoItem[] items;

			public PpsUndoGroup(PpsUndoManager manager, string sDescription, IPpsUndoItem[] items)
			{
				this.manager = manager;
				this.sDescription = sDescription;
				this.items = items;
			} // ctor

			public void Goto()
			{
				int iIndex = manager.items.IndexOf(this);
				if (iIndex <= -1)
					throw new InvalidOperationException("Undo group has no manager.");
				else if (iIndex < manager.iUndoBorder)
					manager.Undo(manager.iUndoBorder - iIndex);
				else
					manager.Redo(iIndex - manager.iUndoBorder + 1);
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
					else if (iIndex < manager.iUndoBorder)
						return PpsUndoStepType.Undo;
					else
						return PpsUndoStepType.Redo;
				}
			} // prop Type

			public string Description { get { return sDescription; } }
		} // class PpsUndoGroup

		#endregion

		#region -- class PpsUndoTransaction -------------------------------------------------

		private abstract class PpsUndoTransaction : IPpsUndoTransaction
		{
			protected string sDescription;
			private bool? lCommited = null;
			private PpsUndoTransaction blockingTransaction = null;

			public PpsUndoTransaction(string sDescription)
			{
				if (String.IsNullOrEmpty(sDescription))
					throw new ArgumentNullException("description");

				this.sDescription = sDescription;
			} // ctor

			public void Dispose()
			{
				if (!lCommited.HasValue)
					Rollback();
			} // proc Dispose

			internal void CloseTransaction(bool lCommit)
			{
				CheckBlockingTransaction();
				if (lCommited.HasValue)
					throw new ObjectDisposedException(String.Format("Undo Operation: {0}", Description));

				lCommited = lCommit;
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

			public string Description { get { return sDescription; } }
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
			private int iRollbackIndex;

			public PpsUndoParentTransaction(PpsUndoTransaction parent, string sDescription)
				: base(sDescription)
			{
				this.parent = parent;
				this.iRollbackIndex = parent.LockTransaction(this);
			} // ctor

			public override void Append(IPpsUndoItem item)
			{
				parent.Append(item);
			} // proc Append

			internal override void RollbackToIndex(int iRollbackIndex) { parent.RollbackToIndex(iRollbackIndex); }
			internal override int GetRollbackIndex() { return parent.GetRollbackIndex(); }
			internal override void UpdateCurrentTransaction(PpsUndoTransaction trans) { parent.UpdateCurrentTransaction(trans); }

			public override void Commit()
			{
				parent.UnlockTransaction(true, iRollbackIndex);
			} // proc Commit

			public override void Rollback()
			{
				parent.UnlockTransaction(false, iRollbackIndex);
			} // proc Rollback
		} // class PpsUndoParentTransaction

		#endregion

		public event EventHandler CanUndoChanged;
		public event EventHandler CanRedoChanged;

		private List<PpsUndoGroup> items = new List<PpsUndoGroup>();
		private int iUndoBorder = 0;

		private PpsUndoTransaction currentUndoTransaction = null;
		private int iAppendSuspended = 0;

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
			iAppendSuspended++;
		} // proc SuspendAppend

		private void ResumeAppend()
		{
			iAppendSuspended--; //~todo: throw logic errors
		} // proc SuspendAppend

		private void AppendUndoGroup(PpsUndoGroup undoGroup)
		{
			items.Insert(iUndoBorder++, undoGroup); //~todo: redo kürzen, sonst ist die bedienung komisch
			RaiseCanUndo();
		} // proc AppendUndoGroup

		#endregion

		#region -- BeginTransaction -------------------------------------------------------

		public IPpsUndoTransaction BeginTransaction(string sDescription)
		{
			currentUndoTransaction =
				currentUndoTransaction == null ?
					new PpsUndoRootTransaction(this, sDescription) :
					currentUndoTransaction = new PpsUndoParentTransaction(currentUndoTransaction, sDescription);

			return currentUndoTransaction;
		} // func BeginTransaction

		#endregion

		#region -- Undo/Redo --------------------------------------------------------------

		public void Undo(int iCount = 1)
		{
			if (!CanUndo)
				return;
			if (iCount != 1) //~todo: für mehrere
				throw new ArgumentOutOfRangeException("count");

			SuspendAppend();
			try
			{
				items[iUndoBorder - 1].Undo();
				iUndoBorder--;
			}
			finally
			{
				ResumeAppend();
			}

			RaiseCanRedo();
			RaiseCanUndo();
		} // proc Undo

		public void Redo(int iCount = 1)
		{
			if (!CanRedo)
				return;
			if (iCount != 1) //~todo: für mehrere
				throw new ArgumentOutOfRangeException("count");

			SuspendAppend();
			try
			{
				items[iUndoBorder].Redo();
				iUndoBorder++;
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
		public bool CanUndo { get { return iUndoBorder > 0; } }
		/// <summary></summary>
		public bool CanRedo { get { return iUndoBorder < items.Count; } }

		/// <summary></summary>
		public bool InTransaction { get { return currentUndoTransaction != null; } }
		/// <summary></summary>
		public bool InUndoRedoOperation { get { return iAppendSuspended > 0; } }
	} // class PpsUndoManager

	#endregion
}
