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
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Neo.IronLua;
using TecWare.PPSn.Core.Data;
using TecWare.PPSn.Data;

namespace TecWare.PPSn.Export
{
	public partial class MainWindow : Form//, IPpsFormsApplication
	{
		private readonly PpsTableTextData listInfo = new PpsTableTextData();
		
		private readonly Lua lua = new Lua();
		//private readonly PpsEnvironment environment;
		private readonly SynchronizationContext synchronizationContext;

		public MainWindow()
		{
			InitializeComponent();

			synchronizationContext = SynchronizationContext.Current;

			// init env
			//environment = new PpsEnvironment(lua, this, new PpsEnvironmentInfo("Test") { Uri = new Uri("http://localhost:8080/ppsn/") });
			//			environment.ContinueCatch(InitEnvironmentAsync());

			//			cmdEdit.Enabled = environment.IsAuthentificated;
			//			environment.IsAuthentificatedChanged += (sender, e) => cmdEdit.Enabled = environment.IsAuthentificated;

			joinTextBox.DataBindings.Add(new Binding("Text", listInfo, "Views", true, DataSourceUpdateMode.OnPropertyChanged));
			filterTextBox.DataBindings.Add(new Binding("Text", listInfo, "Filter", true, DataSourceUpdateMode.OnPropertyChanged));
			columnsTextBox.DataBindings.Add(new Binding("Text", listInfo, "Columns", true, DataSourceUpdateMode.OnPropertyChanged));
			listInfo.PropertyChanged += ListInfo_PropertyChanged;

			joinTextBox.Text = "views.Betriebsmittelstamm,(views.Werkzeugstamm,views.WkzLebenslauf)";
			//#if DEBUG
			//			listInfo.Views = "views.Teil t";
			//			listInfo.Filter = "or(t.TEILBEST:<10 t.TEILBEST:>100)";
			//			listInfo.Columns = String.Join(Environment.NewLine, 
			//				"+t.TEILTNR=Artikel_Nr",
			//				"t.TEILNAME1=Artikelbezeichnung",
			//				"t.TEILBEST=Bestand"
			//			);
			//#endif
		} // ctor

		private PpsDataQuery ToQuery(bool includeColumnAlias)
		{
			if (listInfo.IsEmpty)
				return null;
			else
			{
				return new PpsDataQuery(listInfo.Views)
				{
					Filter = PpsDataFilterExpression.Parse(listInfo.Filter),
					Columns = listInfo.GetColumnExpressions(includeColumnAlias).ToArray(),
					Order = listInfo.GetOrderExpression().ToArray()
				};
			}
		} // func ToQuery

		private void ListInfo_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				case nameof(PpsTableTextData.Views):
				case nameof(PpsTableTextData.Filter):
				case nameof(PpsTableTextData.Columns):
					UpdateUri();
					break;
			}
		} // event ListInfo_PropertyChanged

		private async Task InitEnvironmentAsync()
		{
		//	await environment.LoginAsync(this);
			UpdateUri();
		} // proc InitEnvironmentAsync

		private void UpdateUri()
			=> uriText.Text = listInfo.IsEmpty ? String.Empty : CreateUriSafe(ToQuery(columnAliasCheck.Checked).ToQuery());

		private string CreateUriSafe(string query)
			=> string.Empty ; //environment?.Request?.CreateFullUri(Uri.EscapeUriString(query))?.ToString() ?? query;

		#region -- IPpsFormsApplication members ---------------------------------------

		private sealed class AwaitStack
		{
			private volatile bool @continue = false;

			public void DoContinue()
				=> @continue = true;

			public bool Continue => @continue;
		} // class AwaitStack

		private readonly Stack<AwaitStack> awaitStack = new Stack<AwaitStack>();

		//void IPpsFormsApplication.Await(Task task)
		//{
		//	if (task.IsCompleted)
		//		return;

		//	if (InvokeRequired)
		//	{
		//		if (SynchronizationContext.Current is PpSynchronizationContext sync)
		//			sync.ProcessMessageLoop(task);
		//		else
		//			task.Wait();
		//	}
		//	else
		//	{
		//		var a = new AwaitStack();
		//		awaitStack.Push(a);
				
		//		task.GetAwaiter().OnCompleted(() => Invoke(new Action(a.DoContinue)));
		//		while (!a.Continue)
		//		{
		//			Application.DoEvents();
		//			Thread.Sleep(100);
		//		}

		//		awaitStack.Pop();
		//	}
		//} // proc Await

		//IPpsProgress IPpsProgressFactory.CreateProgress(bool blockUI)
		//	=> null;

		//SynchronizationContext IPpsFormsApplication.SynchronizationContext => synchronizationContext;

		public string ApplicationId => "PPSnExport";
		public string Title => "PPSn Exporter";

		#endregion

		private void button1_Click(object sender, EventArgs e)
		{
			//environment.EditTable(listInfo, false);
		}

		private void columnAliasCheck_CheckedChanged(object sender, EventArgs e)
		{
			UpdateUri();
		}
	} // class MainWindow
}
