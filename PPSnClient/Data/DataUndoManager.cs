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
using System.Collections.Specialized;

namespace TecWare.PPSn.Data
{
	#region -- enum PpsUndoStepType ---------------------------------------------------

	/// <summary>Type of the current step.</summary>
	public enum PpsUndoStepType
	{
		/// <summary>Undo operation</summary>
		Undo,
		/// <summary>Redo operation</summary>
		Redo
	} // enum PpsUndoStepType

	#endregion

	#region -- interface IPpsUndoStep -------------------------------------------------

	/// <summary>Interface fo an undo step.</summary>
	public interface IPpsUndoStep
	{
		/// <summary>Moves to this step.</summary>
		void Goto();

		/// <summary>Type of the step undo/redo.</summary>
		PpsUndoStepType Type { get; }
		/// <summary>Description of the step.</summary>
		string Description { get; }
		/// <summary>Index of the step.</summary>
		int Index { get; }
	} // interface IPpsUndoStep

	#endregion

	#region -- class PpsUndoManager ---------------------------------------------------

	/// <summary>Manages the undo/redo elements. The undo operations are collected in transactions.</summary>
	public sealed class PpsUndoManager : PpsUndoManagerBase, IEnumerable<IPpsUndoStep>, INotifyCollectionChanged
	{
		#region -- class PpsUndoGroup -------------------------------------------------

		private sealed class PpsUndoGroup : IPpsUndoStep
		{
			private readonly PpsUndoManager manager;
			private readonly string description;
			private readonly IPpsUndoItem[] items;

			public PpsUndoGroup(PpsUndoManager manager, string description, IPpsUndoItem[] items)
			{
				this.manager = manager;
				this.description = description;
				this.items = items;
			} // ctor

			public void Goto()
			{
				var index = manager.items.IndexOf(this);
				if (index <= -1)
					throw new InvalidOperationException("Undo group has no manager.");
				else if (index < manager.undoBorder)
					manager.Undo(manager.undoBorder - index);
				else
					manager.Redo(index - manager.undoBorder + 1);
			} // proc Goto

			internal void Undo()
			{
				for (var i = items.Length - 1; i >= 0; i--)
					items[i].Undo();
			} // proc Undo

			internal void Redo()
			{
				for (var i = 0; i < items.Length; i++)
					items[i].Redo();
			} // proc Redo

			public PpsUndoStepType Type
			{
				get
				{
					var index = manager.items.IndexOf(this);
					if (index <= -1)
						throw new InvalidOperationException("Undo group has no manager.");
					else if (index < manager.undoBorder)
						return PpsUndoStepType.Undo;
					else
						return PpsUndoStepType.Redo;
				}
			} // prop Type

			public int Index => manager.items.IndexOf(this);
			public string Description => description;
		} // class PpsUndoGroup

		#endregion

		/// <summary>Notifies if there are undo operations available.</summary>
		public event EventHandler CanUndoChanged;
		/// <summary>Notifies if there are redo operations available.</summary>
		public event EventHandler CanRedoChanged;

		/// <summary>Notifies a change of the operations.</summary>
		public event NotifyCollectionChangedEventHandler CollectionChanged;

		private readonly List<PpsUndoGroup> items = new List<PpsUndoGroup>();
		private int undoBorder = 0;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary>Creates a new undo manager.</summary>
		public PpsUndoManager()
		{
		} // ctor

		#endregion

		#region -- Clear, Commit ------------------------------------------------------

		/// <summary>Commit a transaction to an undo/redo group.</summary>
		/// <param name="description">Description of this group.</param>
		/// <param name="undoItems">Items within this group.</param>
		protected override void Commit(string description, IPpsUndoItem[] undoItems)
		{
			if (undoItems.Length == 0) // no item => no group
				return; 

			// Remove undo commands
			items.RemoveRange(undoBorder, items.Count - undoBorder);

			// add the undo item
			items.Add(new PpsUndoGroup(this, description, undoItems));
			undoBorder++;

			RaiseCanUndo();
			RaiseCanRedo();
			RaiseCollectionReset();
		} // proc Commit

		/// <summary>Reset the undo stack to nothing.</summary>
		public override void Clear()
		{
			base.Clear();

			// clear stack
			items.Clear();
			undoBorder = 0;

			// raise the events
			RaiseCanUndo();
			RaiseCanRedo();
			RaiseCollectionReset();
		} // proc Clear

		#endregion

		#region -- Undo/Redo ----------------------------------------------------------

		/// <summary>Undo the number of groups.</summary>
		/// <param name="count">Number of groups to undo (default 1).</param>
		public void Undo(int count = 1)
		{
			if (!CanUndo)
				return;

			SuspendAppend();
			try
			{
				while (CanUndo && count-- > 0)
				{
					items[undoBorder - 1].Undo();
					undoBorder--;
				}
			}
			finally
			{
				ResumeAppend();
			}

			RaiseCanRedo();
			RaiseCanUndo();
			RaiseCollectionReset();
		} // proc Undo

		/// <summary>Redo the number of groups.</summary>
		/// <param name="count">Number of groups to redo (default 1).</param>
		public void Redo(int count = 1)
		{
			if (!CanRedo)
				return;

			SuspendAppend();
			try
			{
				while (CanRedo && count-- > 0)
				{
					items[undoBorder].Redo();
					undoBorder++;
				}
			}
			finally
			{
				ResumeAppend();
			}

			RaiseCanRedo();
			RaiseCanUndo();
			RaiseCollectionReset();
		} // proc Redo

		private void RaiseCanUndo()
			=> CanUndoChanged?.Invoke(this, EventArgs.Empty);

		private void RaiseCanRedo()
			=> CanRedoChanged?.Invoke(this, EventArgs.Empty);

		private void RaiseCollectionReset()
			=> CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

		#endregion

		#region -- IEnumerator --------------------------------------------------------

		/// <summary>List of all undo/redo operations.</summary>
		/// <returns></returns>
		public IEnumerator<IPpsUndoStep> GetEnumerator()
		{
			foreach (var c in items)
				yield return c;
		} // func GetEnumerator

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
			=> GetEnumerator();

		#endregion

		/// <summary>Is there are a undo operation available.</summary>
		public bool CanUndo => undoBorder > 0;
		/// <summary>Is there are a redo operation available.</summary>
		public bool CanRedo => undoBorder < items.Count;
	} // class PpsUndoManager

	#endregion
}
