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
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TecWare.PPSn;
using Excel = Microsoft.Office.Interop.Excel;

namespace PPSnExcel
{
	public partial class WaitForm : Form, IPpsUIService, IPpsAsyncService, IPpsProgressFactory
	{
		#region -- class ProgressBar --------------------------------------------------

		private sealed class ProgressBar : IPpsProgress
		{
			private readonly WaitForm form;

			private string currentText = null;
			private int currentValue = -1;

			public ProgressBar(WaitForm form)
			{
				this.form = form;
				form.AddToStack(this);
			} // ctor

			public void Dispose()
			{
				form.RemoveFromStack(this);
			} // proc Dispose

			public void Report(string value)
			{
				if (currentText != value)
				{
					currentText = value;
					form.UpdateProgress();
				}
			} // proc Report

			public string Text
			{
				get => currentText;
				set => Report(value);
			} // prop Text

			public int Value
			{
				get => currentValue;
				set
				{
					if (value != currentValue)
					{
						currentValue = value;
						form.UpdateProgress();
					}
				}
			} // prop Value 
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
		private readonly int mainThreadId = 0;

		private readonly SynchronizationContext synchronizationContext;
		private readonly List<ProgressBar> progressStack = new List<ProgressBar>();
		private bool inMessageLoop = false;
		private int awaitingTasks = 0;

		private string currentProgressText = null;
		private int currentProgressValue = -1;

		#region -- Ctor/Dtor ----------------------------------------------------------

		public WaitForm(Excel.Application application)
		{
			this.application = application;

			synchronizationContext = new WaitSynchronizationContext(this);
			mainThreadId = Thread.CurrentThread.ManagedThreadId;

			InitializeComponent();

			CreateHandle();
		} // ctor

		protected override void OnHandleCreated(EventArgs e)
		{
			base.OnHandleCreated(e);
			Debug.Print("[Thread {0}] OnHandleCreated", Thread.CurrentThread.ManagedThreadId);
		} // proc OnHandleCreated

		protected override void OnHandleDestroyed(EventArgs e)
		{
			Debug.Print("[Thread {0}] OnHandleDestroyed", Thread.CurrentThread.ManagedThreadId);
			base.OnHandleDestroyed(e);
		} // proc OnHandleDestroyed

		#endregion

		#region -- ShowWait/CloseWait -------------------------------------------------

		private void ShowWait()
		{
			if (inMessageLoop)
				throw new InvalidOperationException();

			// position
			var rc = Globals.ThisAddIn.ApplicationBounds;
			Left = rc.Left + rc.Width / 10;
			Top = rc.Top + rc.Height / 5;

			// run message loop
			try
			{
				inMessageLoop = true;
				awaitingTasks = 1;
				ShowDialog(Globals.ThisAddIn);

				// recreate handle
				CreateHandle();
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

		#endregion

		#region -- Update Progress ----------------------------------------------------

		private void AddToStack(ProgressBar sender)
		{
			progressStack.Add(sender);
			UpdateProgress();
		} // proc AddToStack

		private void RemoveFromStack(ProgressBar sender)
		{
			progressStack.Remove(sender);
			UpdateProgress();
		} // proc AddToStack

		private void UpdateProgress()
		{
			string newProgressText;
			int newProgressValue;
			lock (progressStack)
			{
				newProgressText = progressStack.FindLast(p => !String.IsNullOrEmpty(p.Text))?.Text ?? "Daten werden verarbeitet...";
				newProgressValue = progressStack.FindLast(p => p.Value >= 0)?.Value ?? -1;
			}

			if (newProgressText != currentProgressText)
				UpdateProgressText(newProgressText);
			if (newProgressValue != currentProgressValue)
				UpdateProgressValue(newProgressValue);
		} // proc UpdateProgress

		private void UpdateProgressText(string newProgressText)
		{
			if (InvokeRequired)
				Invoke(new Action<string>(UpdateProgressText), newProgressText);
			else
			{
				currentProgressText = newProgressText;
				label1.Text = currentProgressText;
			}
		} // proc UpdateProgressText

		private void UpdateProgressValue(int newProgressValue)
		{
			if (InvokeRequired)
				Invoke(new Action<int>(UpdateProgressValue), newProgressValue);
			else
			{
				currentProgressValue = newProgressValue;
				if (currentProgressValue < 0)
				{
					progressBar1.Style = ProgressBarStyle.Marquee;
				}
				else
				{
					progressBar1.Style = ProgressBarStyle.Continuous;
					progressBar1.Value = currentProgressValue;
				}
			}
		} // proc UpdateProgressValue

		#endregion

		#region -- UI-Service - members -----------------------------------------------

		int IPpsUIService.MsgBox(string text, PpsImage image, params string[] buttons)
			=> PpsWinShell.ShowMessage(Globals.ThisAddIn, text, image, buttons);

		private Task RunUICore(Action action)
		{
			if (InvokeRequired)
			{
				var ar = BeginInvoke(action);
				return Task.Factory.FromAsync(ar, EndInvoke);
			}
			else
			{
				action.Invoke();
				return Task.CompletedTask;
			}
		} // proc RunUICore

		private Task<T> RunUICore<T>(Func<T> func)
		{
			if (InvokeRequired)
			{
				var ar = BeginInvoke(func);
				return Task.Factory.FromAsync(ar, EndInvoke).ContinueWith(t => (T)t.Result);
			}
			else
				return Task.FromResult(func.Invoke());
		} // func RunCore

		Task IPpsUIService.RunUI(Action action)
			=> RunUICore(action);

		Task<T> IPpsUIService.RunUI<T>(Func<T> action)
			=> RunUICore<T>(action);

		void IPpsUIService.ShowException(PpsExceptionShowFlags flags, Exception exception, string alternativeMessage)
			=> PpsWinShell.ShowException(Globals.ThisAddIn, flags, exception, alternativeMessage);

		void IPpsUIService.ShowNotification(string message, PpsImage image)
			=> throw new NotImplementedException();

		string[] IPpsUIService.Ok => new string[] { "Ok" };
		string[] IPpsUIService.YesNo => new string[] { "Yes", "No" };
		string[] IPpsUIService.OkCancel => new string[] { "Ok", "Cancel" };

		#endregion

		#region -- Async-Serivce - members --------------------------------------------

		private struct VoidResult { }

		public static void CheckSynchronizationContext()
		{
			if (!(SynchronizationContext.Current is WindowsFormsSynchronizationContext))
				SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
		} // proc CheckSynchronizationContext

		void IPpsAsyncService.Await(IServiceProvider sp, Task task)
		{
			if (task.IsCompleted)
				return;

			if (InvokeRequired)
			{
				if (SynchronizationContext.Current is IPpsProcessMessageLoop loop)
					loop.ProcessMessageLoop(task);
				else
					task.Wait();
			}
			else if (inMessageLoop)
			{
				throw new InvalidOperationException();
				// todo: wait
				//awaitingTasks++;
				//task.GetAwaiter().OnCompleted(() => Invoke(new Action(CloseWait)));
				//Application.DoEvents();
			}
			else if (!task.Wait(200))
			{
				task.GetAwaiter().OnCompleted(() => Invoke(new Action(CloseWait)));
				ShowWait();
			}
		} // proc IPpsAsyncService.Await

		#endregion

		IPpsProgress IPpsProgressFactory.CreateProgress(bool blockUI)
			=> new ProgressBar(this);

		public SynchronizationContext SynchronizationContext => synchronizationContext;
	} // class WaitForm
}
