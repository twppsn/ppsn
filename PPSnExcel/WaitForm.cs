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
using Excel = Microsoft.Office.Interop.Excel;

namespace PPSnExcel
{
	public partial class WaitForm : Form
	{
		#region -- class ProgressBar --------------------------------------------------

		private sealed class ProgressBar : IProgress<string>
		{
			private readonly WaitForm form;

			public ProgressBar(WaitForm form)
			{
				this.form = form;
			} // ctor

			public void Report(string value)
			{
			} // proc Report
		} // proc ProgressBar

		#endregion

		#region -- class WaitSynchronizationContext -----------------------------------

		private sealed class WaitSynchronizationContext : SynchronizationContext
		{
			private readonly WaitForm form;

			public WaitSynchronizationContext(WaitForm form)
			{
				this.form = form;
			} // ctor

			public override SynchronizationContext CreateCopy()
				=> new WaitSynchronizationContext(form);

			public override void Post(SendOrPostCallback d, object state)
				=> form.BeginInvoke(d, state);

			public override void Send(SendOrPostCallback d, object state)
				=> form.Invoke(d, state);
		} // class WaitSynchronizationContext

		#endregion

		private readonly Excel.Application application;
		private readonly SynchronizationContext synchronizationContext;
		private bool inMessageLoop = false;
		private int awaitingTasks = 0;

		public WaitForm(Excel.Application application)
		{
			this.application = application;
			this.synchronizationContext = new WaitSynchronizationContext(this);

			InitializeComponent();

			CreateHandle();
		} // ctor

		private void ShowWait()
		{
			if (inMessageLoop)
				throw new InvalidOperationException();

			// position
			Left = 100;
			Top = 100;

			// run message loop
			try
			{
				inMessageLoop = true;
				awaitingTasks = 1;
				ShowDialog(Globals.ThisAddIn);
			}
			finally
			{
				inMessageLoop = false;
			}
		} // proc ShowWait

		private void CloseWait()
		{
			awaitingTasks--;
			if (awaitingTasks <= 0)
				Close();
		} // proc CloseWait

		public void Await(Task task)
		{
			if (task.IsCompleted)
				return;

			if (InvokeRequired)
				task.Wait();
			else if (inMessageLoop)
			{
				awaitingTasks++;
				task.GetAwaiter().OnCompleted(() => Invoke(new Action(CloseWait)));
			}
			else if (!task.Wait(200))
			{
				task.GetAwaiter().OnCompleted(() => Invoke(new Action(CloseWait)));
				ShowWait();
			}
		} // proc Await

		public IProgress<string> CreateProgress()
			=> new ProgressBar(this);

		public SynchronizationContext SynchronizationContext => synchronizationContext;
	} // class WaitForm
}
