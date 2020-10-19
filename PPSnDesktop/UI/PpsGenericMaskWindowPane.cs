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
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Xml;
using Neo.IronLua;
using TecWare.DE.Stuff;
using TecWare.PPSn.Controls;
using TecWare.PPSn.Data;

namespace TecWare.PPSn.UI
{
	/// <summary>Inhalt, welcher aus einem dynamisch geladenen Xaml besteht
	/// und einer Dataset</summary>
	public class PpsGenericMaskWindowPane : PpsGenericWpfWindowPane
	{
		private readonly IPpsIdleAction idleActionToken;
		private CollectionViewSource undoView;
		private CollectionViewSource redoView;

		private PpsObject obj; // current object, controlled by this mask
		private PpsObjectDataSet data; // data object
		private IPpsObjectDataAccess dataAccess; // access token to the data object

		#region -- Ctor/Dtor/Load/Unload ----------------------------------------------

		/// <summary></summary>
		/// <param name="paneHost"></param>
		public PpsGenericMaskWindowPane(IPpsWindowPaneHost paneHost)
			: base(paneHost)
		{
			idleActionToken = paneHost.GetService<IPpsIdleService>(true).AddIdleFunction(
				elapsed =>
				{
					if (elapsed > 3000)
					{
						if (data != null && data.IsDirty)
							CommitEditAsync().AwaitTask();
						return false;
					}
					else
						return data != null && data.IsDirty;
				}
			);
		} // ctor

		/// <summary></summary>
		/// <param name="disposing"></param>
		protected override void Dispose(bool disposing)
		{
			if (disposing)
				PaneHost.GetService<IPpsIdleService>(true).Remove(idleActionToken);
			base.Dispose(disposing);
		} // proc Dispose

		/// <summary></summary>
		/// <param name="otherArgumens"></param>
		/// <returns></returns>
		public override PpsWindowPaneCompareResult CompareArguments(LuaTable otherArgumens)
		{
			var r = base.CompareArguments(otherArgumens);
			if (r == PpsWindowPaneCompareResult.Reload || r == PpsWindowPaneCompareResult.Incompatible)
			{
				var otherObj = otherArgumens.GetMemberValue("Object");
				return otherObj == obj ? PpsWindowPaneCompareResult.Same : PpsWindowPaneCompareResult.Reload;
			}
			return r;
		} // func CompareArguments

		/// <summary>Return the parser service for the dynamic-xaml.</summary>
		/// <returns></returns>
		protected override PpsParserService[] GetRootParserServices()
			=> new PpsParserService[] { new PpsDataSetResolver(Data) };

		/// <summary></summary>
		/// <param name="arguments"></param>
		/// <returns></returns>
		protected override async Task LoadInternAsync(LuaTable arguments)
		{
			async Task CreateNewObjectAsync()
			{
				// load schema
				var documentType = (string)arguments.GetMemberValue("createNew");
				if (documentType == null)
					throw new ArgumentException("No 'object' or 'createNew' set.");

				// get the object info for the type
				var objectInfo = Shell.ObjectInfos[documentType];
				if (objectInfo.CreateServerSiteOnly || String.IsNullOrEmpty(objectInfo.DocumentUri))
					throw new ArgumentNullException("object", "Parameter 'object' is missing.");

				// create the new object entry
				obj = await Shell.CreateNewObjectAsync(objectInfo);
			} // proc CreateNewObject

			// get the object reference for the document
			obj = (PpsObject)arguments.GetMemberValue("Object");

			// new document or load one
			using (var transaction = await Shell.MasterData.CreateTransactionAsync(PpsMasterDataTransactionLevel.Write))
			{
				if (obj == null) // no object given
					await CreateNewObjectAsync();

				data = await obj.GetDataAsync<PpsObjectDataSet>();

				// register events, owner, and in the openDocuments dictionary
				dataAccess = await data.AccessAsync(arguments);
				dataAccess.DisableUI = new Func<IDisposable>(() => DisableUI("Verarbeite Daten...", -1));
				dataAccess.DataChanged += (sender, e) => OnDataChanged();

				transaction.Commit();
			}

			// get the pane to view, if it is not given
			if (!arguments.ContainsKey("pane"))
			{
				// try to get the uri from the pane list
				var info = Shell.GetObjectInfo(obj.Typ);
				var paneUri = info["defaultPane"] as string;
				if (!String.IsNullOrEmpty(paneUri))
					arguments.SetMemberValue("Pane", paneUri);
				else
				{
					// read the schema meta data
					paneUri = data.DataSetDefinition.Meta.GetProperty<string>(PpsDataSetMetaData.DefaultPaneUri, null);
					if (!String.IsNullOrEmpty(paneUri))
						arguments.SetMemberValue("Pane", paneUri);
				}
			}

			// Load mask
			await base.LoadInternAsync(arguments);

			InitializeData();
		} // proc LoadInternAsync

		private void InitializeData()
		{
			// create the views on the undo manager
			undoView = new CollectionViewSource();
			using (undoView.DeferRefresh())
			{
				undoView.Source = data.UndoManager;
				undoView.SortDescriptions.Add(new SortDescription(nameof(IPpsUndoStep.Index), ListSortDirection.Descending));
				undoView.Filter += (sender, e) => e.Accepted = ((IPpsUndoStep)e.Item).Type == PpsUndoStepType.Undo;
			}

			redoView = new CollectionViewSource();
			using (redoView.DeferRefresh())
			{
				redoView.Source = data.UndoManager;
				redoView.SortDescriptions.Add(new SortDescription(nameof(IPpsUndoStep.Index), ListSortDirection.Ascending));
				redoView.Filter += (sender, e) => e.Accepted = ((IPpsUndoStep)e.Item).Type == PpsUndoStepType.Redo;
			}

			//// Extent command bar
			//if (PaneControl?.Commands != null)
			//{
			//	UndoManagerListBox listBox;

			//	var undoCommand = new PpsUISplitCommandButton()
			//	{
			//		Order = new PpsCommandOrder(200, 130),
			//		DisplayText = "Rückgängig",
			//		Description = "Rückgängig",
			//		Image = "undo",
			//		Command = new PpsCommand(
			//			(args) =>
			//			{
			//				UpdateSources();
			//				UndoManager.Undo();
			//			},
			//			(args) => UndoManager?.CanUndo ?? false
			//		),
			//		Popup = new System.Windows.Controls.Primitives.Popup()
			//		{
			//			Child = listBox = new UndoManagerListBox()
			//			{
			//				Style = (Style)App.Current.FindResource("UndoManagerListBoxStyle")
			//			}
			//		}
			//	};

			//	listBox.SetBinding(FrameworkElement.DataContextProperty, new Binding("DataContext.UndoView"));

			//	var redoCommand = new PpsUISplitCommandButton()
			//	{
			//		Order = new PpsCommandOrder(200, 140),
			//		DisplayText = "Wiederholen",
			//		Description = "Wiederholen",
			//		Image = "redo",
			//		Command = new PpsCommand(
			//			(args) =>
			//			{
			//				UpdateSources();
			//				UndoManager.Redo();
			//			},
			//			(args) => UndoManager?.CanRedo ?? false
			//		),
			//		Popup = new System.Windows.Controls.Primitives.Popup()
			//		{
			//			Child = listBox = new UndoManagerListBox()
			//			{
			//				IsRedoList = true,
			//				Style = (Style)App.Current.FindResource("UndoManagerListBoxStyle")
			//			}
			//		}
			//	};

			//	listBox.SetBinding(FrameworkElement.DataContextProperty, new Binding("DataContext.RedoView"));

			//	PaneControl.Commands.Add(undoCommand);
			//	PaneControl.Commands.Add(redoCommand);
			//}

			HelpKey = obj.Typ + ".Edit";

			OnPropertyChanged(nameof(UndoManager));
			OnPropertyChanged(nameof(UndoView));
			OnPropertyChanged(nameof(RedoView));
			OnPropertyChanged(nameof(Data));
			OnPropertyChanged(nameof(CurrentData));
			OnPropertyChanged(nameof(HelpKey));
			OnDataChanged();
		} // proc InitializeData

		/// <summary></summary>
		/// <param name="commit"></param>
		/// <returns></returns>
		public override async Task<bool> UnloadAsync(bool? commit = default(bool?))
		{
			if (data != null && data.IsDirty)
				await CommitEditAsync();

			dataAccess?.Dispose();

			dataAccess = null;
			data = null;
			obj = null;

			return await base.UnloadAsync(commit);
		} // func UnloadAsync

		#endregion

		/// <summary>Is the data changed.</summary>
		protected virtual void OnDataChanged()
			=> CallMemberDirect("OnDataChanged", Array.Empty<object>(), ignoreNilFunction: true);

		/// <summary>Store the data in a file. For debug proposes.</summary>
		/// <param name="fileName"></param>
		[LuaMember]
		public void CommitToDisk(string fileName)
		{
			using (var xml = XmlWriter.Create(fileName, new XmlWriterSettings() { Encoding = Encoding.UTF8, NewLineHandling = NewLineHandling.Entitize, NewLineChars = "\n\r", IndentChars = "\t" }))
				data.Write(xml);
		} // proc CommitToDisk

		/// <summary>Enforce commit of data to local store.</summary>
		/// <returns></returns>
		[LuaMember]
		public async Task CommitEditAsync()
		{
			UpdateSources();
			await dataAccess.CommitAsync();

			Debug.Print("Saved Document.");
		} // proc CommitEdit

		/// <summary>Push data to server.</summary>
		/// <returns></returns>
		[LuaMember]
		public async Task PushDataAsync()
		{
			UpdateSources();
			try
			{
				await CommitEditAsync();
				await obj.PushAsync();
			}
			catch (Exception ex)
			{
				await Shell.ShowExceptionAsync(PpsExceptionShowFlags.None, ex, "Veröffentlichung ist fehlgeschlagen.");
			}
		} // proc PushDataAsync

		/// <summary>UndoManager of the dataset.</summary>
		[LuaMember]
		public PpsUndoManager UndoManager => data.UndoManager;

		/// <summary></summary>
		protected sealed override IPpsDataInfo CurrentData => obj;

		/// <summary>Access to the filtert undo/redo list of the undo manager.</summary>
		public ICollectionView UndoView => undoView.View;
		/// <summary>Access to the filtert undo/redo list of the undo manager.</summary>
		public ICollectionView RedoView => redoView.View;

		/// <summary>Access the data.</summary>
		[LuaMember]
		public PpsDataSet Data => data;
		/// <summary>Access loaded object.</summary>
		[LuaMember]
		public PpsObject Object => obj;
		/// <summary>Access the environment.</summary>
		[LuaMember("Environment")]
		public new PpsEnvironment Shell => (PpsEnvironment)base.Shell;
	} // class PpsGenericMaskWindowPane
}
