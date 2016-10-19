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
#define NOTIFY_BINDING_SOURCE_UPDATE
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using System.Xml;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.PPSn.Controls;
using TecWare.PPSn.Data;

namespace TecWare.PPSn.UI
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Inhalt, welcher aus einem dynamisch geladenen Xaml besteht
	/// und einer Dataset</summary>
	public class PpsGenericMaskWindowPane : PpsGenericWpfWindowPane, IPpsActiveDataSetOwner
	{
		private readonly IPpsActiveDocuments rdt;
		private readonly IPpsIdleAction idleActionToken;
		private CollectionViewSource undoView;
		private CollectionViewSource redoView;

		private PpsDocument document; // current DataSet which is controlled by the mask
		private string documentType = null;

		public PpsGenericMaskWindowPane(PpsEnvironment environment, PpsMainWindow window)
			: base(environment, window)
		{
			this.rdt = (IPpsActiveDocuments)environment.GetService(typeof(IPpsActiveDocuments));

			idleActionToken = Environment.AddIdleAction(
				elapsed =>
				{
					if (elapsed > 3000)
					{
						CommitEdit();
						return false;
					}
					else
						return document != null && document.IsDirty;
				}
			);
		} // ctor

		protected override void Dispose(bool disposing)
		{
			if (disposing)
				Environment.RemoveIdleAction(idleActionToken);
			base.Dispose(disposing);
		} // proc Dispose

		public override PpsWindowPaneCompareResult CompareArguments(LuaTable otherArgumens)
		{
			var r = base.CompareArguments(otherArgumens);
			if (r == PpsWindowPaneCompareResult.Reload)
			{
				var otherDocumentId = new PpsDataSetId(
					otherArgumens.GetOptionalValue("guid", Guid.Empty),
					Convert.ToInt64(otherArgumens.GetMemberValue("revId") ?? -1L)
				);

				return document.DataSetId == otherDocumentId ? PpsWindowPaneCompareResult.Same : PpsWindowPaneCompareResult.Reload;
			}
			return r;
		} // func CompareArguments

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
				await rdt.CreateDocumentAsync(documentType, arguments) :
				await rdt.OpenDocumentAsync(new PpsDataSetId((Guid)id, Convert.ToInt64(revId ?? -1)), arguments);

			// register events, owner, and in the openDocuments dictionary
			document.RegisterOwner(this);

			// get the pane to view, if it is not given
			if (!arguments.ContainsKey("pane"))
				arguments.SetMemberValue("pane", await rdt.GetDocumentDefaultPaneAsync(documentType));

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

			// Extent command bar
			if (PaneControl?.Commands != null)
			{
				UndoManagerListBox listBox;

				var undoCommand = new PpsUISplitCommandButton()
				{
					Order = new PpsCommandOrder(200, 130),
					DisplayText = "Rückgängig",
					Description = "Rückgängig",
					Image = "undoImage",
					Command = new PpsCommand(Environment,
						(args) =>
						{
							UpdateSources();
							UndoManager.Undo();
						},
						(args) => UndoManager?.CanUndo ?? false
					),
					Popup = new System.Windows.Controls.Primitives.Popup()
					{
						Child = listBox = new UndoManagerListBox()
						{
							Style = (Style)App.Current.FindResource("UndoManagerListBoxStyle")
						}
					}
				};

				listBox.SetBinding(FrameworkElement.DataContextProperty, new Binding("DataContext.UndoManager"));

				var redoCommand = new PpsUISplitCommandButton()
				{
					Order = new PpsCommandOrder(200, 140),
					DisplayText = "Wiederholen",
					Description = "Wiederholen",
					Image = "redoImage",
					Command = new PpsCommand(Environment,
						(args) =>
						{
							UpdateSources();
							UndoManager.Redo();
						},
						(args) => UndoManager?.CanRedo ?? false
					),						
					Popup = new System.Windows.Controls.Primitives.Popup()
					{
						Child = listBox = new UndoManagerListBox()
						{
							IsRedoList=true,
							Style = (Style)App.Current.FindResource("UndoManagerListBoxStyle")
						}
					}
				};

				listBox.SetBinding(FrameworkElement.DataContextProperty, new Binding("DataContext.UndoManager"));

				PaneControl.Commands.Add(undoCommand);
				PaneControl.Commands.Add(redoCommand);
			}

			OnPropertyChanged("UndoManager");
			OnPropertyChanged("UndoView");
			OnPropertyChanged("RedoView");
			OnPropertyChanged("Data");
		} // porc InitializeData

		public override Task<bool> UnloadAsync(bool? commit = default(bool?))
		{
			if (document.IsDirty)
				CommitEdit();

			document.UnregisterOwner(this);

			return base.UnloadAsync(commit);
		} // func UnloadAsync

		[LuaMember(nameof(CommitToDisk))]
		public void CommitToDisk(string fileName)
		{
			using (var xml = XmlWriter.Create(fileName, new XmlWriterSettings() { Encoding = Encoding.UTF8, NewLineHandling = NewLineHandling.Entitize, NewLineChars = "\n\r", IndentChars = "\t" }))
				document.Write(xml);
		} // proc CommitToDisk

		[LuaMember(nameof(CommitEdit))]
		public void CommitEdit()
		{
			UpdateSources();
			document.CommitWork();
			Debug.Print("Saved Document.");
		} // proc CommitEdit

		[LuaMember(nameof(PushDataAsync))]
		public Task PushDataAsync()
		{
			UpdateSources();
			return document.PushWorkAsync();
		} // proc PushDataAsync

		[LuaMember(nameof(UndoManager))]
		public PpsUndoManager UndoManager => document.UndoManager;

		/// <summary>Access to the filtert undo/redo list of the undo manager.</summary>
		public ICollectionView UndoView => undoView.View;
		/// <summary>Access to the filtert undo/redo list of the undo manager.</summary>
		public ICollectionView RedoView => redoView.View;

		[LuaMember(nameof(Data))]
		public PpsDataSet Data => document;

		LuaTable IPpsActiveDataSetOwner.Events => this;
	} // class PpsGenericMaskWindowPane
}
