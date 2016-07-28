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
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.PPSn.Data;

namespace TecWare.PPSn.UI
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Inhalt, welcher aus einem dynamisch geladenen Xaml besteht
	/// und einer Dataset</summary>
	public class PpsGenericMaskWindowPane : PpsGenericWpfWindowPane, IPpsDocumentOwner
	{
		private readonly IPpsDocuments rdt;
		private CollectionViewSource undoView;
		private CollectionViewSource redoView;

		private PpsDocument document; // current DataSet which is controlled by the mask
		private string documentType = null;

		public PpsGenericMaskWindowPane(PpsEnvironment environment)
			: base(environment)
		{
			this.rdt = (IPpsDocuments)environment.GetService(typeof(IPpsDocuments));
		} // ctor

		public override async Task LoadAsync(LuaTable arguments)
		{
			// get the document path
			this.documentType = arguments.GetOptionalValue("document", String.Empty);
			if (String.IsNullOrEmpty(documentType))
				throw new ArgumentNullException("document", "Parameter is missing.");

			await Task.Yield(); // spawn new thread

			// new document or load one
			var id = arguments.GetMemberValue("guid");
			var revId = arguments.GetMemberValue("revId");
			this.document = id == null ?
				await rdt.CreateDocumentAsync(this, documentType, arguments) :
				await rdt.OpenDocumentAsync(this, new PpsDocumentId((Guid)id, Convert.ToInt64(revId ?? -1)), arguments);
						
			// get the pane to view, if it is not given
			if (!arguments.ContainsKey("pane"))
				arguments.SetMemberValue("pane", rdt.GetDocumentDefaultPane(documentType));

			// Lade die Maske
			await base.LoadAsync(arguments);

			await Dispatcher.InvokeAsync(InitializeData);
		} // proc LoadAsync

		private void InitializeData()
		{
			// create the views on the undo manager
			undoView = new CollectionViewSource();
			using (undoView.DeferRefresh())
			{
				undoView.Source = document.UndoManager;
				undoView.SortDescriptions.Add(new SortDescription("Index", ListSortDirection.Descending));
				undoView.Filter += (sender, e) => e.Accepted = ((IPpsUndoStep)e.Item).Type == PpsUndoStepType.Undo;
			}

			redoView = new CollectionViewSource();
			using (redoView.DeferRefresh())
			{
				redoView.Source = document.UndoManager;
				redoView.Filter += (sender, e) => e.Accepted = ((IPpsUndoStep)e.Item).Type == PpsUndoStepType.Redo;
			}

			OnPropertyChanged("UndoManager");
			OnPropertyChanged("UndoView");
			OnPropertyChanged("RedoView");
			OnPropertyChanged("Data");
		} // porc InitializeData

		[LuaMember(nameof(CommitEdit))]
		public void CommitEdit()
		{
			foreach (var expr in BindingOperations.GetSourceUpdatingBindings(Control))
				expr.UpdateSource();

			document.CommitWork();
		} // proc CommitEdit

		[LuaMember(nameof(UndoManager))]
		public PpsUndoManager UndoManager => document.UndoManager;

		/// <summary>Access to the filtert undo/redo list of the undo manager.</summary>
		public ICollectionView UndoView => undoView.View;
		/// <summary>Access to the filtert undo/redo list of the undo manager.</summary>
		public ICollectionView RedoView => redoView.View;

		[LuaMember(nameof(Data))]
		public PpsDataSet Data => document;

		LuaTable IPpsDocumentOwner.DocumentEvents => this;
	} // class PpsGenericMaskWindowPane
}
