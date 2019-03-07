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
using System.IO;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using Neo.IronLua;
using Neo.Markdig.Xaml;
using TecWare.DE.Data;
using TecWare.PPSn.Data;

namespace TecWare.PPSn.UI
{
	internal sealed class PpsMarkdownDocumentModel : ObservableObject, IDisposable
	{
		private readonly IPpsDataInfo dataInfo;
		private IPpsDataObject dataAccess = null;

		private string text = null;
		private FlowDocument document = null;
		private bool isDocumentDirty = false;
		private bool isDirty = false;

		public PpsMarkdownDocumentModel(IPpsDataInfo dataInfo)
		{
			this.dataInfo = dataInfo;
		} // ctor

		public void Dispose()
		{
			dataAccess?.Dispose();
			dataAccess = null;
		} // proc Dispose

		public async Task LoadAsync(IPpsWindowPane windowPane)
		{
			using (var bar = windowPane.DisableUI(String.Format("Lade Dokument ({0})...", dataInfo.Name)))
			{
				// create access
				dataAccess = await dataInfo.LoadAsync();

				dataAccess.DisableUI = () => windowPane.DisableUI("Pdf-Dokument wird bearbeitet...");
				dataAccess.DataChanged += async (sender, e) => await LoadFromObjectAsync();

				await LoadFromObjectAsync();
			}
		} // func LoadAsync

		private async Task LoadFromObjectAsync()
		{
			if (dataAccess.Data is IPpsDataStream stream)
			{
				var src = stream.OpenStream(FileAccess.Read);
				try
				{
					using (var sr = new StreamReader(src))
						Text = await sr.ReadToEndAsync();
					IsDirty = false;
				}
				catch
				{
					src.Dispose();
					throw;
				}
			}
			else
				throw new ArgumentNullException("data", "Data is not an blob.");
		} // proc LoadFromObjectAsync

		public async Task SaveAsync()
		{
			if (dataAccess.Data is IPpsDataStream stream)
			{
				using (var dst = stream.OpenStream(FileAccess.Write))
				using (var sw = new StreamWriter(dst))
					await sw.WriteAsync(Text);

				await dataAccess.CommitAsync();
				IsDirty = false;
			}
			else
				throw new ArgumentNullException("data", "Data is not an blob.");
		}

		private FlowDocument GetDocument()
		{
			if (isDocumentDirty)
			{
				document = MarkdownXaml.ToFlowDocument(Text);
				isDocumentDirty = false;
			}
			return document;
		} // func GetDocument

		public IPpsDataInfo Data => dataInfo;

		public string Text
		{
			get => text; set
			{
				if (Set(ref text, value, nameof(Text)))
				{
					IsDirty = true;
					isDocumentDirty = true;
					OnPropertyChanged(nameof(Document));
				}
			}
		} // prop Text

		public FlowDocument Document => GetDocument();

		public bool IsDirty { get => isDirty; set => Set(ref isDirty, value, nameof(IsDirty)); }
	} // class PpsMarkdownDocumentModel

	public partial class PpsMarkdownPane : UserControl, IPpsWindowPane
	{
		private readonly IPpsWindowPaneHost paneHost;

		private readonly PpsUICommandCollection commands;

		/// <summary></summary>
		/// <param name="paneHost"></param>
		public PpsMarkdownPane(IPpsWindowPaneHost paneHost)
		{
			this.paneHost = paneHost ?? throw new ArgumentNullException(nameof(paneHost));

			commands = new PpsUICommandCollection
			{
				AddLogicalChildHandler = AddLogicalChild,
				RemoveLogicalChildHandler = RemoveLogicalChild
			};

			commands.AddButton("100;100", "save", ApplicationCommands.Save, "Speichern", "Speichert die Änderungen");

			InitializeComponent();
		} // ctor

		/// <summary></summary>
		public void Dispose()
		{
			if (DataContext is IDisposable d)
			{
				d.Dispose();
				DataContext = null;
			}
		} // proc Dispose

		#region -- IPpsWindowPane -----------------------------------------------------

		private event PropertyChangedEventHandler PropertyChanged;
		event PropertyChangedEventHandler INotifyPropertyChanged.PropertyChanged { add => PropertyChanged += value; remove => PropertyChanged -= value; }

		PpsWindowPaneCompareResult IPpsWindowPane.CompareArguments(LuaTable args)
		{
			return PpsWindowPaneCompareResult.Reload;
		} // func IPpsWindowPane.CompareArguments

		async Task IPpsWindowPane.LoadAsync(LuaTable args)
		{
			DataContext = null;

			var doc = new PpsMarkdownDocumentModel(args.GetMemberValue("Object") as IPpsDataInfo);
			await doc.LoadAsync(this);
			DataContext = doc;

			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IPpsWindowPane.CurrentData)));
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IPpsWindowPane.SubTitle)));
		} // proc IPpsWindowPane.LoadAsync

		Task<bool> IPpsWindowPane.UnloadAsync(bool? commit)
		{
			CurrentDocument?.Dispose();
			return Task.FromResult(true);
		} // func IPpsWindowPane.UnloadAsync

		string IPpsWindowPane.Title => "Markdown Editor";
		string IPpsWindowPane.SubTitle => CurrentDocument?.Data.Name;

		private PpsMarkdownDocumentModel CurrentDocument => (PpsMarkdownDocumentModel)DataContext;

		object IPpsWindowPane.Image => null;

		bool IPpsWindowPane.HasSideBar => false;

		object IPpsWindowPane.Control => this;
		IPpsWindowPaneHost IPpsWindowPane.PaneHost => paneHost;

		PpsUICommandCollection IPpsWindowPane.Commands => commands;

		IPpsDataInfo IPpsWindowPane.CurrentData => CurrentDocument?.Data;

		bool IPpsWindowPane.IsDirty => CurrentDocument?.IsDirty ?? false;
		string IPpsWindowPane.HelpKey => "PpsMarkdownPane";

		#endregion

		private async void CommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			await CurrentDocument?.SaveAsync();
			e.Handled = true;
		} // event CommandBinding_Executed

		private void CommandBinding_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = CurrentDocument != null && CurrentDocument.IsDirty;
		} // event CommandBinding_CanExecute
	} // class PpsMarkdownPane
}
