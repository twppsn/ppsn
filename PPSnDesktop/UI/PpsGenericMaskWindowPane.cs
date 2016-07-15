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
	public class PpsGenericMaskWindowPane : PpsGenericWpfWindowPane
	{
		private readonly IPpsDocuments rdt;
		//private PpsUndoManager undoManager = new PpsUndoManager();
		//private CollectionViewSource undoView;
		//private CollectionViewSource redoView;

		private PpsDataSetClient dataSet; // current DataSet which is controlled by the mask
		private string documentType = null;

		public PpsGenericMaskWindowPane(PpsEnvironment environment)
			: base(environment)
		{
			this.rdt = (IPpsDocuments)environment.GetService(typeof(IPpsDocuments));

			//// create the views on the undo manager
			//undoView = new CollectionViewSource();
			//using (undoView.DeferRefresh())
			//{
			//	undoView.Source = undoManager;
			//	undoView.SortDescriptions.Add(new SortDescription("Index", ListSortDirection.Descending));
			//	undoView.Filter += (sender, e) => e.Accepted = ((IPpsUndoStep)e.Item).Type == PpsUndoStepType.Undo;
			//}

			//redoView = new CollectionViewSource();
			//using (redoView.DeferRefresh())
			//{
			//	redoView.Source = undoManager;
			//	redoView.Filter += (sender, e) => e.Accepted = ((IPpsUndoStep)e.Item).Type == PpsUndoStepType.Redo;
			//}
		} // ctor

		public override async Task LoadAsync(LuaTable arguments)
		{
			// get the document path
			this.documentType = arguments.GetOptionalValue("document", String.Empty);
			if (String.IsNullOrEmpty(documentType))
				throw new ArgumentNullException("document", "Parameter is missing.");

			await Task.Yield(); // spawn new thread

			// new document or load one
			var id = arguments.GetMemberValue("id");
			var revId = arguments.GetMemberValue("revId");
			this.dataSet = id == null ?
				await rdt.CreateDocumentAsync(documentType, arguments) :
				await rdt.OpenDocumentAsync(new PpsDocumentId(Convert.ToInt64(id), Convert.ToInt64(revId ?? -1)), arguments);
						
			// get the pane to view, if it is not given
			if (!arguments.ContainsKey("pane"))
				arguments.SetMemberValue("pane", rdt.GetDocumentDefaultPane(documentType));

			// Lade die Maske
			await base.LoadAsync(arguments);

			await Dispatcher.InvokeAsync(() => OnPropertyChanged("Data"));
		} // proc LoadAsync

		[LuaMember(nameof(CommitEdit))]
		public void CommitEdit()
		{
			//foreach (var expr in BindingOperations.GetSourceUpdatingBindings(Control))
			//	expr.UpdateSource();
		} // proc CommitEdit

		//[LuaMember(nameof(UndoManager))]
		//public PpsUndoManager UndoManager => undoManager;

		///// <summary>Access to the filtert undo/redo list of the undo manager.</summary>
		//public ICollectionView UndoView => undoView.View;
		///// <summary>Access to the filtert undo/redo list of the undo manager.</summary>
		//public ICollectionView RedoView => redoView.View;

		[LuaMember(nameof(Data))]
		public PpsDataSet Data => dataSet;
	} // class PpsGenericMaskWindowPane

	////Q&D
	//public class PpsContentTemplateSelector : DataTemplateSelector
	//{
	//	public override DataTemplate SelectTemplate(object item, DependencyObject container)
	//	{
	//		var row = item as PpsDataRow;
	//		if (row == null)
	//			return null;
			
	//		var control = container as ContentPresenter;
	//		if (control == null)
	//			return null;

	//		var r = (DataTemplate)control.FindResource(row.Table.Name);
	//		return r;
	//	}
	//}
}
