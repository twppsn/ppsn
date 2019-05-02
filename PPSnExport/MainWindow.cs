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

			//textBox1.Text = "views.Betriebsmittelstamm,(views.Werkzeugstamm,views.WkzLebenslauf)";
			textBox1.Text = "views.Betriebsmittelstamm,views.Werkzeugstamm,views.WkzLebenslauf";
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


		Task IPpsTableData.UpdateAsync(string views)
		{
			textBox1.Text = views;
			return Task.CompletedTask;
		} // func UpdateAsync

		string IPpsTableData.DisplayName { get => label1.Text; set => label1.Text=value; }

		string IPpsTableData.Views => textBox1.Text;

		bool IPpsTableData.IsEmpty => String.IsNullOrEmpty(textBox1.Text);

		private void button1_Click(object sender, EventArgs e)
		{
			environment.EditTable(this);
		}
	} // class MainWindow
}
