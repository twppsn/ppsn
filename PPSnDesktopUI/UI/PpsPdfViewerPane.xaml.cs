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
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Neo.IronLua;
using TecWare.DE.Stuff;

namespace TecWare.PPSn.UI
{
	/// <summary>Panel to view pdf-data.</summary>
	public partial class PpsPdfViewerPane : UserControl, IPpsWindowPane
	{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public static readonly DependencyProperty SubTitleProperty = DependencyProperty.Register(nameof(SubTitle), typeof(string), typeof(PpsPdfViewerPane), new FrameworkPropertyMetadata(String.Empty, new PropertyChangedCallback(OnSubTitleChanged)));
		private static readonly DependencyPropertyKey commandsPropertyKey = DependencyProperty.RegisterReadOnly(nameof(Commands), typeof(string), typeof(PpsUICommandCollection), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty CommandsProperty = commandsPropertyKey.DependencyProperty;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		private readonly IPpsWindowPaneManager paneManager;
		private readonly IPpsWindowPaneHost paneHost;

		/// <summary>Pane constructor, that gets called by the host.</summary>
		public PpsPdfViewerPane(IPpsWindowPaneManager paneManager, IPpsWindowPaneHost paneHost)
		{
			this.paneManager = paneManager ?? throw new ArgumentNullException(nameof(paneManager));
			this.paneHost = paneHost ?? throw new ArgumentNullException(nameof(paneHost));

			var commands = new PpsUICommandCollection
			{
				AddLogicalChildHandler = AddLogicalChild,
				RemoveLogicalChildHandler = RemoveLogicalChild
			};
			SetValue(commandsPropertyKey, commands);

			InitializeComponent();
		} // ctor

		/// <summary></summary>
		public void Dispose()
		{
		} // proc Dispose

		#region -- Load, Close --------------------------------------------------------

		private Task OpenPdfAsync(string fileName)
		{
			return Task.CompletedTask;
		} // proc OpenPdfAsync

		private bool ClosePdf()
		{
			return true;
		} // func ClosePdf

		#endregion

		#region -- IPpsWindowPane implementation --------------------------------------

		private event PropertyChangedEventHandler PropertyChanged;
		event PropertyChangedEventHandler INotifyPropertyChanged.PropertyChanged { add => PropertyChanged += value; remove => PropertyChanged -= value; }
		
		PpsWindowPaneCompareResult IPpsWindowPane.CompareArguments(LuaTable args)
		{
			return PpsWindowPaneCompareResult.Reload;
		} // func IPpsWindowPane.CompareArguments

		Task IPpsWindowPane.LoadAsync(LuaTable args)
		{
			var fileName = args.GetOptionalValue("fileName", String.Empty);

			return OpenPdfAsync(fileName);
		} // func LoadAsync

		Task<bool> IPpsWindowPane.UnloadAsync(bool? commit)
			=> Task.FromResult(ClosePdf());

		string IPpsWindowPane.Title => "PDF-Viewer";

		bool IPpsWindowPane.HasSideBar => false;
		bool IPpsWindowPane.IsDirty => false;

		object IPpsWindowPane.Control => this;
		IPpsWindowPaneManager IPpsWindowPane.PaneManager => paneManager;
		IPpsWindowPaneHost IPpsWindowPane.PaneHost => paneHost;

		#endregion

		/// <summary>Extent logical child collection with commands</summary>
		protected override IEnumerator LogicalChildren
			=> Procs.CombineEnumerator(base.LogicalChildren, Commands?.GetEnumerator());

		private static void OnSubTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsPdfViewerPane)d).OnSubTitleChanged();

		private void OnSubTitleChanged()
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SubTitle)));

		/// <summary>Sub-title of the pdf data.</summary>
		public string SubTitle { get => (string)GetValue(SubTitleProperty); set => SetValue(SubTitleProperty, value); }
		/// <summary>Command bar.</summary>
		public PpsUICommandCollection Commands => (PpsUICommandCollection)GetValue(CommandsProperty);
	} // class PpsPdfViewerPane 
}
