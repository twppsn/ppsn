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
using TecWare.PPSn.Data;

namespace TecWare.PPSn.Export
{
	public partial class MainWindow : Form, IPpsFormsApplication, IPpsTableData
	{
		private readonly Lua lua = new Lua();
		private readonly PpsEnvironment environment;
		private readonly SynchronizationContext synchronizationContext;

		public MainWindow()
		{
			InitializeComponent();

			synchronizationContext = SynchronizationContext.Current;

			// init env
			environment = new PpsEnvironment(lua, this, new PpsEnvironmentInfo("Test") { Uri = new Uri("http://localhost:8080/ppsn/") });
			environment.ContinueCatch(environment.LoginAsync(this));

			//joinTextBox.Text = "views.Betriebsmittelstamm,(views.Werkzeugstamm,views.WkzLebenslauf)";
			joinTextBox.Text = "views.Betriebsmittelstamm t=views.Werkzeugstamm t1=views.WkzLebenslauf t2";
			filterTextBox.Text = "t.BMKKID:test";
			columnsTextBox.Text = String.Join(Environment.NewLine, "+t.BMKKIDENT", "t.BMKKBEZ", "t.BMKKCRDAT", "t.BMKKCRUSER", "t.BMKKFBERIDENT", "t.FBERNAME", "t.BMKKUPDAT", "t.BMKKUPUSER");
		} // ctor

		public string ApplicationId => "PPSnExport";
		public string Title => "PPSn Exporter";

		private sealed class AwaitStack
		{
			private volatile bool @continue = false;

			public void DoContinue()
				=> @continue = true;

			public bool Continue => @continue;
		} // class AwaitStack

		private readonly Stack<AwaitStack> awaitStack = new Stack<AwaitStack>();

		void IPpsFormsApplication.Await(Task task)
		{
			if (task.IsCompleted)
				return;

			if (InvokeRequired)
			{
				if (SynchronizationContext.Current is PpsSynchronizationContext sync)
					sync.ProcessMessageLoop(task);
				else
					task.Wait();
			}
			else
			{
				var a = new AwaitStack();
				awaitStack.Push(a);
				
				task.GetAwaiter().OnCompleted(() => Invoke(new Action(a.DoContinue)));
				while (!a.Continue)
				{
					Application.DoEvents();
					Thread.Sleep(100);
				}

				awaitStack.Pop();
			}
		} // proc Await

		IPpsProgress IPpsProgressFactory.CreateProgress(bool blockUI)
			=> null;

		SynchronizationContext IPpsFormsApplication.SynchronizationContext => synchronizationContext;

		private sealed class ParseColumnInfo : IPpsTableColumn
		{
			public ParseColumnInfo(string expr)
			{
				var ofs = 0;
				if (expr[0] == '+')
				{
					Ascending = true;
					ofs = 1;
				}
				else if (expr[0] == '-')
				{
					Ascending = false;
					ofs = 1;
				}

				Expression = expr.Substring(ofs);
			} // ctor

			public string Expression { get; }
			public bool? Ascending { get; }
		} // class ParseColumnInfo

		private static string FormatColumnInfo(IPpsTableColumn col)
		{
			var prefix = String.Empty;
			if (col.Ascending.HasValue)
				prefix = col.Ascending.Value ? "+" : "-";

			return prefix + col.Expression;
		} // func FormatColumnInfo

		Task IPpsTableData.UpdateAsync(string views, string filter, IEnumerable<IPpsTableColumn> columns)
		{
			joinTextBox.Text = views;
			filterTextBox.Text = filter;
			columnsTextBox.Text= String.Join(Environment.NewLine, from col in columns select FormatColumnInfo(col)); 

			return Task.CompletedTask;
		} // func UpdateAsync

		string IPpsTableData.DisplayName { get => label1.Text; set => label1.Text=value; }

		string IPpsTableData.Views => joinTextBox.Text;
		string IPpsTableData.Filter => filterTextBox.Text;
		IEnumerable<IPpsTableColumn> IPpsTableData.Columns => columnsTextBox.Text.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries).Select(s => new ParseColumnInfo(s));

		bool IPpsTableData.IsEmpty => String.IsNullOrEmpty(joinTextBox.Text);

		private void button1_Click(object sender, EventArgs e)
		{
			environment.EditTable(this);
		}
	} // class MainWindow
}
