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
		/// <summary></summary>
		int Index { get; }
	} // interface IPpsUndoStep

	#endregion

	#region -- class PpsUndoManager -----------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Manages the undo/redo elements. The undo operations are collected in transactions.</summary>
	/// ~todo: Reset,Commit,Rollback müssen UndoManager ändern können, sonst ist er invalid
	public sealed class PpsUndoManager : PpsUndoManagerBase, IEnumerable<IPpsUndoStep>, INotifyCollectionChanged
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

		public event EventHandler CanUndoChanged;
		public event EventHandler CanRedoChanged;

		public event NotifyCollectionChangedEventHandler CollectionChanged;

		private List<PpsUndoGroup> items = new List<PpsUndoGroup>();
		private int undoBorder = 0;

		#region -- Ctor/Dtor --------------------------------------------------------------

		public PpsUndoManager()
		{
		} // ctor

		#endregion

		#region -- Clear, Commit ----------------------------------------------------------

		protected override void Commit(string description, IPpsUndoItem[] undoItems)
		{
			// Remove undo commands
			items.RemoveRange(undoBorder, items.Count - undoBorder);

			// add the undo item
			items.Add(new PpsUndoGroup(this, description, undoItems));
			undoBorder++;

			RaiseCanUndo();
			RaiseCanRedo();
			RaiseCollectionReset();
		} // proc Commit

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

		#region -- Undo/Redo --------------------------------------------------------------

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

		#region -- IEnumerator ------------------------------------------------------------

		public IEnumerator<IPpsUndoStep> GetEnumerator()
		{
			foreach (var c in items)
				yield return c;
		} // func GetEnumerator

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
			=> GetEnumerator();

		#endregion

		/// <summary></summary>
		public bool CanUndo => undoBorder > 0;
		/// <summary></summary>
		public bool CanRedo => undoBorder < items.Count;
	} // class PpsUndoManager

	#endregion
}
