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
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using Neo.Markdig.Xaml;
using TecWare.PPSn.Data;

namespace TecWare.PPSn.UI
{
	public class PpsHelpPageViewer : Control
	{
		private PpsEnvironment environment;
		private IPpsObjectDataAccess currentHelpPage = null;

		#region -- HelpKey - Property -------------------------------------------------

		public static readonly DependencyProperty HelpKeyProperty = DependencyProperty.Register(nameof(HelpKey), typeof(string), typeof(PpsHelpPageViewer), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnHelpKeyChanged)));

		private static void OnHelpKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsHelpPageViewer)d).OnHelpKeyChanged((string)e.NewValue);

		private void OnHelpKeyChanged(string newValue)
		{
			if (environment != null)
				BeginUpdateHelpPage();
		} // proc OnHelpKeyChanged

		public string HelpKey { get => (string)GetValue(HelpKeyProperty); set => SetValue(HelpKeyProperty, value); }

		#endregion

		#region -- ProgressStack - Property -------------------------------------------

		private static readonly DependencyPropertyKey progressStackPropertyKey = DependencyProperty.RegisterReadOnly(nameof(ProgressStack), typeof(PpsProgressStack), typeof(PpsHelpPageViewer), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty ProgressStackProperty = progressStackPropertyKey.DependencyProperty;

		public PpsProgressStack ProgressStack => (PpsProgressStack)GetValue(ProgressStackProperty);

		#endregion

		#region -- Document - Property ------------------------------------------------

		private static readonly DependencyPropertyKey documentPropertyKey = DependencyProperty.RegisterReadOnly(nameof(Document), typeof(FlowDocument), typeof(PpsHelpPageViewer), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty DocumentProperty = documentPropertyKey.DependencyProperty;

		public PpsProgressStack Document => (PpsProgressStack)GetValue(DocumentProperty);

		#endregion

		public PpsHelpPageViewer()
		{
			SetValue(progressStackPropertyKey, new PpsProgressStack(Dispatcher));
			ClearHelpPage();

			CommandBindings.Add(
				new CommandBinding(ApplicationCommands.Open,
					(sender, e) =>
					{
						EditHelpPage();
						e.Handled = true;
					},
					(sender, e) => e.CanExecute = true
				)
			);
		} // ctor

		public override void OnApplyTemplate()
		{
			base.OnApplyTemplate();

			environment = (PpsEnvironment)PpsShellWpf.GetShell(this);
			if (HelpKey != null)
				BeginUpdateHelpPage();
		} // proc OnApplyTemplate

		private void BeginUpdateHelpPage()
		{
			if (HelpKey == null)
				ClearHelpPage();
			else
				RefreshHelpPageAsync().SpawnTask(environment);
		} // proc BeginUpdateHelpPage

		private void ClearHelpPage()
		{
			SetValue(documentPropertyKey, CreateNoHelpKeyDocument());
			if (currentHelpPage != null)
			{
				currentHelpPage.Dispose();
				currentHelpPage = null;
			}
		} // proc ClearHelpPage

		private void EditHelpPage()
		{
			MessageBox.Show("ToDo");
		} // proc EditHelpPage

		private async Task RefreshHelpPageAsync()
		{
			var helpObj = await environment.GetObjectAsync(PpsDataFilterExpression.Combine(
				PpsDataFilterExpression.Compare("TYP", PpsDataFilterCompareOperator.Equal, "HelpKey"),
				PpsDataFilterExpression.Compare("NR", PpsDataFilterCompareOperator.Equal, HelpKey)
			));

			ClearHelpPage();
			if (helpObj == null) // load empty help page
			{
				SetValue(documentPropertyKey, CreateNoHelpKeyDocument());
			}
			else // parse help content
			{
				using (var bar = ProgressStack.CreateProgress(true))
				{
					bar.Text = "Lade Hilfeseite...";

					// reload
					var blob = await helpObj.GetDataAsync<IPpsBlobObjectData>();
					currentHelpPage = await blob.AccessAsync();
					currentHelpPage.DisableUI = () => ProgressStack.CreateProgress(true);
					currentHelpPage.DataChanged += (sender, e) => RenderPageContentAsync().SpawnTask(environment);

					// render page
					await RenderPageContentAsync();
				}
			}
		} // proc RefreshHelpPageAsync

		private async Task RenderPageContentAsync()
		{
			var blob = (IPpsBlobObjectData)currentHelpPage.ObjectData;

			using (var src = await Task.Run(() => blob.OpenStream(FileAccess.Read)))
			using (var sr = new StreamReader(src))
				SetValue(documentPropertyKey, MarkdownXaml.ToFlowDocument(await sr.ReadToEndAsync()));
		} // proc RenderPageContentAsync

		static PpsHelpPageViewer()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PpsHelpPageViewer), new FrameworkPropertyMetadata(typeof(PpsHelpPageViewer)));
		}

		private static FlowDocument CreateNoHelpKeyDocument()
			=> MarkdownXaml.ToFlowDocument("*Es wurde kein HelpKey gesetzt*");
		private static FlowDocument CreateNoHelpDocument()
			=> MarkdownXaml.ToFlowDocument("*Keine Dokunment hinterlegt*");
	} //class PpsHelpPageViewer
}
