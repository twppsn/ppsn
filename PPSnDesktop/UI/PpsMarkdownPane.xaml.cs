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
using System.IO;
using System.Threading.Tasks;
using System.Windows.Documents;
using Neo.IronLua;
using Neo.Markdig.Xaml;
using TecWare.DE.Data;
using TecWare.PPSn.Controls;
using TecWare.PPSn.Data;

namespace TecWare.PPSn.UI
{
	#region -- class PpsMarkdownDocumentModel -----------------------------------------

	/// <summary></summary>
	public sealed class PpsMarkdownDocumentModel : ObservableObject, IDisposable
	{
		private readonly IPpsDataInfo dataInfo;
		private IPpsDataObject dataAccess = null;

		private string text = null;
		private FlowDocument document = null;
		private bool isDocumentDirty = false;
		private bool isDirty = false;

		/// <summary></summary>
		/// <param name="dataInfo"></param>
		public PpsMarkdownDocumentModel(IPpsDataInfo dataInfo)
		{
			this.dataInfo = dataInfo;
		} // ctor

		/// <summary></summary>
		public void Dispose()
		{
			dataAccess?.Dispose();
			dataAccess = null;
		} // proc Dispose

		/// <summary></summary>
		/// <param name="windowPane"></param>
		/// <returns></returns>
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

		/// <summary></summary>
		/// <returns></returns>
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

		/// <summary>Current data information</summary>
		public IPpsDataInfo Data => dataInfo;

		/// <summary>Current text content.</summary>
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

		/// <summary>Current flow document</summary>
		public FlowDocument Document => GetDocument();

		/// <summary>Is the current document changed</summary>
		public bool IsDirty { get => isDirty; set => Set(ref isDirty, value, nameof(IsDirty)); }
	} // class PpsMarkdownDocumentModel

	#endregion

	#region -- class PpsMarkdownPane --------------------------------------------------

	// todo: sollte in Desktop.UI sein, aber LoadComponent prüft das Assembly, anderes Model notwendig?
	public partial class PpsMarkdownPane : PpsWindowPaneControl
	{
		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary></summary>
		/// <param name="paneHost"></param>
		public PpsMarkdownPane(IPpsWindowPaneHost paneHost)
			: base(paneHost)
		{
			InitializeComponent();

			Commands.AddButton("100;120", "save",
				CommitCommand,
				"Speichern", "Speichert die Änderungen"
			);

			this.AddCommandBinding(Shell, CommitCommand,
				new PpsAsyncCommand(
					async ctx => await CurrentDocument.SaveAsync(),
					ctx => CurrentDocument != null && CurrentDocument.IsDirty
				)
			);
		} // ctor

		/// <summary></summary>
		/// <param name="disposing"></param>
		protected override void Dispose(bool disposing)
		{
			if (DataContext is IDisposable d)
			{
				d.Dispose();
				DataContext = null;
			}
		} // proc Dispose

		#endregion

		#region -- WindowPane ---------------------------------------------------------

		/// <summary></summary>
		/// <param name="args"></param>
		/// <returns></returns>
		protected override PpsWindowPaneCompareResult CompareArguments(LuaTable args)
			=> PpsWindowPaneCompareResult.Reload;

		/// <summary></summary>
		/// <param name="args"></param>
		/// <returns></returns>
		protected override async Task OnLoadAsync(LuaTable args)
		{
			DataContext = null;

			var doc = new PpsMarkdownDocumentModel(args.GetMemberValue("Object") as IPpsDataInfo);
			await doc.LoadAsync(this);
			DataContext = doc;

			NotifyWindowPanePropertyChanged(nameof(IPpsWindowPane.CurrentData));
		} // proc OnLoadAsync

		/// <summary></summary>
		/// <param name="commit"></param>
		/// <returns></returns>
		protected override Task<bool> OnUnloadAsync(bool? commit)
		{
			CurrentDocument?.Dispose();
			return Task.FromResult(true);
		} // proc OnUnloadAsync

		private PpsMarkdownDocumentModel CurrentDocument => (PpsMarkdownDocumentModel)DataContext;

		/// <summary></summary>
		protected override IPpsDataInfo CurrentData
			=> CurrentDocument?.Data;

		#endregion
	} // class PpsMarkdownPane

	#endregion
}
