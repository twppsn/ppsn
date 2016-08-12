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
	public class PpsGenericMaskWindowPane : PpsGenericWpfWindowPane, IPpsDocumentOwner, IPpsIdleAction
	{
		private readonly IPpsDocuments rdt;
		private readonly PpsMainWindow window;
		private CollectionViewSource undoView;
		private CollectionViewSource redoView;

		private PpsDocument document; // current DataSet which is controlled by the mask
		private string documentType = null;

		private bool forceUpdateSource = false; // set this to true, to update the document on idle

		public PpsGenericMaskWindowPane(PpsEnvironment environment, PpsMainWindow window)
			: base(environment)
		{
			this.window = window;
			this.rdt = (IPpsDocuments)environment.GetService(typeof(IPpsDocuments));

			Environment.AddIdleAction(this);
		} // ctor

		protected override void Dispose(bool disposing)
		{
			if (disposing)
				Environment.RemoveIdleAction(this);

			base.Dispose(disposing);
		} // proc Dispose

		public override PpsWindowPaneCompareResult CompareArguments(LuaTable otherArgumens)
		{
			var r = base.CompareArguments(otherArgumens);
			if (r == PpsWindowPaneCompareResult.Reload)
			{
				var otherDocumentId = new PpsDocumentId(
					otherArgumens.GetOptionalValue("guid", Guid.Empty),
					Convert.ToInt64(otherArgumens.GetMemberValue("revId") ?? -1L)
				);

				return document.DocumentId == otherDocumentId ? PpsWindowPaneCompareResult.Same : PpsWindowPaneCompareResult.Reload;
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
				await rdt.OpenDocumentAsync(new PpsDocumentId((Guid)id, Convert.ToInt64(revId ?? -1)), arguments);

			// register events, owner, and in the openDocuments dictionary
			document.RegisterOwner(this);

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

			// Extent command bar
			var ppsGenericControl = Control as PpsGenericWpfControl;
			if (ppsGenericControl != null)
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
							Style = (Style)App.Current.FindResource("PPSnUndoManagerListBoxStyle")
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
							Style = (Style)App.Current.FindResource("PPSnUndoManagerListBoxStyle")
						}
					}
				};

				listBox.SetBinding(FrameworkElement.DataContextProperty, new Binding("DataContext.UndoManager"));

				ppsGenericControl.Commands.Add(undoCommand);
				ppsGenericControl.Commands.Add(redoCommand);
			}

			OnPropertyChanged("UndoManager");
			OnPropertyChanged("UndoView");
			OnPropertyChanged("RedoView");
			OnPropertyChanged("Data");
		} // porc InitializeData

		protected override void OnControlCreated()
		{
			base.OnControlCreated();

			Mouse.AddPreviewMouseDownHandler(Control, Control_MouseDownHandler);
			Mouse.AddPreviewMouseDownOutsideCapturedElementHandler(Control, Control_MouseDownHandler);
			Keyboard.AddPreviewGotKeyboardFocusHandler(Control, Control_GotKeyboardFocusHandler);
			Keyboard.AddPreviewLostKeyboardFocusHandler(Control, Control_LostKeyboardFocusHandler);
			Keyboard.AddPreviewKeyUpHandler(Control, Control_KeyUpHandler);
		} // proc OnControlCreated

		#region -- Undo/Redo Management ---------------------------------------------------

		/*
		 * TextBox default binding is LostFocus, to support changes in long text, we try to
		 * connact undo operations.
		 */
		private BindingExpression currentBindingExpression = null;

		private static bool IsCharKey(Key k)
		{
			switch (k)
			{
				case Key.Back:
				case Key.Return:
				case Key.Space:
				case Key.D0:
				case Key.D1:
				case Key.D2:
				case Key.D3:
				case Key.D4:
				case Key.D5:
				case Key.D6:
				case Key.D7:
				case Key.D8:
				case Key.D9:
				case Key.A:
				case Key.B:
				case Key.C:
				case Key.D:
				case Key.E:
				case Key.F:
				case Key.G:
				case Key.H:
				case Key.I:
				case Key.J:
				case Key.K:
				case Key.L:
				case Key.M:
				case Key.N:
				case Key.O:
				case Key.P:
				case Key.Q:
				case Key.R:
				case Key.S:
				case Key.T:
				case Key.U:
				case Key.V:
				case Key.W:
				case Key.X:
				case Key.Y:
				case Key.Z:
				case Key.NumPad0:
				case Key.NumPad1:
				case Key.NumPad2:
				case Key.NumPad3:
				case Key.NumPad4:
				case Key.NumPad5:
				case Key.NumPad6:
				case Key.NumPad7:
				case Key.NumPad8:
				case Key.NumPad9:
				case Key.Multiply:
				case Key.Add:
				case Key.Separator:
				case Key.Subtract:
				case Key.Decimal:
				case Key.Divide:
				case Key.Oem1:
				case Key.OemPlus:
				case Key.OemComma:
				case Key.OemMinus:
				case Key.OemPeriod:
				case Key.Oem2:
				case Key.Oem3:
				case Key.Oem4:
				case Key.Oem5:
				case Key.Oem6:
				case Key.Oem7:
				case Key.Oem8:
				case Key.Oem102:
					return true;
				default:
					return false;
			}
		} // func IsCharKey

		private void Control_MouseDownHandler(object sender, MouseButtonEventArgs e)
		{
			if (currentBindingExpression != null && currentBindingExpression.IsDirty)
			{
#if DEBUG && NOTIFY_BINDING_SOURCE_UPDATE
				Debug.Print("TextBox force update on mouse.");
#endif
				forceUpdateSource = true;
			}
		} // event Control_MouseDownHandler

		private void Control_KeyUpHandler(object sender, KeyEventArgs e)
		{
			if (currentBindingExpression != null && currentBindingExpression.IsDirty && !IsCharKey(e.Key))
			{
#if DEBUG && NOTIFY_BINDING_SOURCE_UPDATE
				Debug.Print("TextBox force update on keyboard.");
#endif
				forceUpdateSource = true;
			}
		} // event Control_KeyUpHandler

		private void Control_GotKeyboardFocusHandler(object sender, KeyboardFocusChangedEventArgs e)
		{
			var newTextBox = e.NewFocus as TextBox;
			if (newTextBox != null)
			{
				var b = BindingOperations.GetBinding(newTextBox, TextBox.TextProperty);
				var expr = BindingOperations.GetBindingExpression(newTextBox, TextBox.TextProperty);
				if (b != null && (b.UpdateSourceTrigger == UpdateSourceTrigger.Default || b.UpdateSourceTrigger == UpdateSourceTrigger.LostFocus) && expr.Status != BindingStatus.PathError)
				{
					currentBindingExpression = expr;
#if DEBUG && NOTIFY_BINDING_SOURCE_UPDATE
					Debug.Print("Textbox GotFocus");
#endif
				}
			}
		} // event Control_GotKeyboardFocusHandler

		private void Control_LostKeyboardFocusHandler(object sender, KeyboardFocusChangedEventArgs e)
		{
			if (currentBindingExpression != null && e.OldFocus == currentBindingExpression.Target)
			{
#if DEBUG && NOTIFY_BINDING_SOURCE_UPDATE
				Debug.Print("LostFocus");
#endif
				currentBindingExpression = null;
			}
		} // event Control_LostKeyboardFocusHandler

		private void CheckBindingOnIdle()
		{
			if (currentBindingExpression != null && !forceUpdateSource && currentBindingExpression.IsDirty && !Window.IsActive)
			{
#if DEBUG && NOTIFY_BINDING_SOURCE_UPDATE
				Debug.Print("TextBox force update on idle.");
#endif
				forceUpdateSource = true;
			}
		} // proc CheckBindingOnIdle

		#endregion

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

		[LuaMember(nameof(UpdateSources))]
		public void UpdateSources()
		{
			forceUpdateSource = false;

			foreach (var expr in BindingOperations.GetSourceUpdatingBindings(Control))
				expr.UpdateSource();
		} // proc UpdateSources

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

		public bool OnIdle(int elapsed)
		{
			if (elapsed > 3000)
			{
				CommitEdit();
				return false;
			}
			else if (elapsed > 300)
			{
				if (forceUpdateSource)
					UpdateSources();
				return document.IsDirty;
			}
			else
			{
				CheckBindingOnIdle();
				return document != null && (document.IsDirty || forceUpdateSource);
			}
		} // func OnIdle

		[LuaMember(nameof(UndoManager))]
		public PpsUndoManager UndoManager => document.UndoManager;

		/// <summary>Access to the filtert undo/redo list of the undo manager.</summary>
		public ICollectionView UndoView => undoView.View;
		/// <summary>Access to the filtert undo/redo list of the undo manager.</summary>
		public ICollectionView RedoView => redoView.View;

		[LuaMember(nameof(Data))]
		public PpsDataSet Data => document;
		[LuaMember(nameof(Window))]
		public PpsWindow Window => window;

		LuaTable IPpsDocumentOwner.DocumentEvents => this;
	} // class PpsGenericMaskWindowPane
}
