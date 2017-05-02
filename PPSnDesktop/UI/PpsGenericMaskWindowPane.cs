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
		private readonly IPpsIdleAction idleActionToken;
		private CollectionViewSource undoView;
		private CollectionViewSource redoView;

		private PpsObject obj; // current object, controlled by this mask
		private PpsObjectDataSet data; // data object
		
		public PpsGenericMaskWindowPane(PpsEnvironment environment, PpsMainWindow window)
			: base(environment, window)
		{
			idleActionToken = Environment.AddIdleAction(
				elapsed =>
				{
					if (elapsed > 3000)
					{
						if (data != null && data.IsDirty)
							CommitEditAsync();
						return false;
					}
					else
						return data != null && data.IsDirty;
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
				var otherObj = otherArgumens.GetMemberValue("object");
				return otherObj == obj ? PpsWindowPaneCompareResult.Same : PpsWindowPaneCompareResult.Reload;
			}
			return r;
		} // func CompareArguments

		public override async Task LoadAsync(LuaTable arguments)
		{
			void CreateNewObject(PpsMasterDataTransaction transaction)
			{

				// load schema
				var documentType = (string)arguments.GetMemberValue("createNew");
				if (documentType == null)
					throw new ArgumentException("No 'object' or 'createNew' set.");

				// get the object info for the type
				var objectInfo = Environment.ObjectInfos[documentType];
				if (objectInfo.CreateServerSiteOnly || String.IsNullOrEmpty(objectInfo.DocumentUri))
					throw new ArgumentNullException("object", "Parameter 'object' is missing.");

				// create the new object entry
				obj = Environment.CreateNewObject(transaction, objectInfo);
			} // proc CreateNewObject
			
			// get the object reference for the document
			obj = (PpsObject)arguments.GetMemberValue("object");

			// new document or load one
			using (var transaction = Environment.MasterData.CreateTransaction())
			{
				if (obj == null) // no object given
					CreateNewObject(transaction);

				data = await obj.GetDataAsync<PpsObjectDataSet>(transaction);

				// register events, owner, and in the openDocuments dictionary
				data.RegisterOwner(this);

				// load data
				if (!data.IsLoaded)
				{
					// call initalization hook
					if (obj.HasData) // existing data
					{
						await data.LoadAsync(transaction);
						await data.OnLoadedAsync(arguments);
					}
					else // new data
					{
						await data.OnNewAsync(arguments);
					}
				}

				transaction.Commit();
			}

			// get the pane to view, if it is not given
			if (!arguments.ContainsKey("pane"))
			{
				// try to get the uri from the pane list
				var info = Environment.GetObjectInfo(obj.Typ);
				var paneUri = info["defaultPane"] as string;
				if (!String.IsNullOrEmpty(paneUri))
					arguments.SetMemberValue("pane", paneUri);
				else
				{
					// read the schema meta data
					paneUri = data.DataSetDefinition.Meta.GetProperty<string>(PpsDataSetMetaData.DefaultPaneUri, null);
					if (!String.IsNullOrEmpty(paneUri))
						arguments.SetMemberValue("pane", paneUri);
				}
			}

			// Load mask
			await base.LoadAsync(arguments);

			await Dispatcher.InvokeAsync(InitializeData);
		} // proc LoadAsync

		private void InitializeData()
		{
			// create the views on the undo manager
			undoView = new CollectionViewSource();
			using (undoView.DeferRefresh())
			{
				undoView.Source = data.UndoManager;
				undoView.SortDescriptions.Add(new SortDescription("Index", ListSortDirection.Descending));
				undoView.Filter += (sender, e) => e.Accepted = ((IPpsUndoStep)e.Item).Type == PpsUndoStepType.Undo;
			}

			redoView = new CollectionViewSource();
			using (redoView.DeferRefresh())
			{
				redoView.Source = data.UndoManager;
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
					Command = new PpsCommand(
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
					Command = new PpsCommand(
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
			if (data != null && data.IsDirty)
				CommitEditAsync().Wait();

			data.UnregisterOwner(this);

			return base.UnloadAsync(commit);
		} // func UnloadAsync

		[LuaMember]
		public void CommitToDisk(string fileName)
		{
			using (var xml = XmlWriter.Create(fileName, new XmlWriterSettings() { Encoding = Encoding.UTF8, NewLineHandling = NewLineHandling.Entitize, NewLineChars = "\n\r", IndentChars = "\t" }))
				data.Write(xml);
		} // proc CommitToDisk

		[LuaMember]
		public PpsLuaTask CommitEditAsync()
		{
			UpdateSources();
			return Environment.RunTask(data.CommitAsync())
				.ContinueUI(new Action(() => Debug.Print("Saved Document.")));
		} // proc CommitEdit

		[LuaMember]
		public PpsLuaTask PushDataAsync()
		{
			UpdateSources();
			return Environment.RunTask(obj.PushAsync())
				.OnException(
				new Action<Exception>(ex => Environment.ShowException(ex, "Veröffentlichung ist fehlgeschlagen."))
			);
		} // proc PushDataAsync

		[LuaMember]
		public PpsUndoManager UndoManager => data.UndoManager;

		/// <summary>Access to the filtert undo/redo list of the undo manager.</summary>
		public ICollectionView UndoView => undoView.View;
		/// <summary>Access to the filtert undo/redo list of the undo manager.</summary>
		public ICollectionView RedoView => redoView.View;

		[LuaMember]
		public PpsDataSet Data => data;
		[LuaMember]
		public PpsObject Object => obj;

		LuaTable IPpsActiveDataSetOwner.Events => this;
	} // class PpsGenericMaskWindowPane
}
