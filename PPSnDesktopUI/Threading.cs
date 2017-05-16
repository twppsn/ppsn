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
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace TecWare.PPSn
{
	#region -- class PpsSynchronizationContext ------------------------------------------

	public abstract class PpsSynchronizationContext : SynchronizationContext
	{
		#region -- struct CurrentTask -----------------------------------------------

		private struct CurrentTask
		{
			public SendOrPostCallback Delegate { get; set; }
			public object State { get; set; }
			public ManualResetEventSlim WaitHandle { get; set; }
		} // struct CurrentTask

		#endregion

		private readonly ManualResetEventSlim tasksFilled = new ManualResetEventSlim(false);
		private readonly Queue<CurrentTask> tasks = new Queue<CurrentTask>();
		private volatile bool doContinue = true;

		public PpsSynchronizationContext()
		{
		} // ctor

		public override SynchronizationContext CreateCopy()
			=> this;

		protected void Stop()
		{
			lock (tasksFilled)
			{
				doContinue = false;
				PulseTaskQueue();
			}
		} // proc Stop

		private void VerifyThreadAccess()
		{
			if (QueueThread != Thread.CurrentThread)
				throw new InvalidOperationException($"Process of the queued task is only allowed in the same thread.(queue threadid {QueueThread.ManagedThreadId}, caller thread id: {Thread.CurrentThread.ManagedThreadId})");
		} // proc VerifyThreadAccess

		private bool TryDequeueTask(CancellationToken cancellationToken, out SendOrPostCallback d, out object state, out ManualResetEventSlim waitHandle)
		{
			lock (tasksFilled)
			{
				try
				{
					if (tasks.Count > 0)
					{
						var t = tasks.Dequeue();
						d = t.Delegate;
						state = t.State;
						waitHandle = t.WaitHandle;
						return true;
					}
					else
					{
						d = null;
						state = null;
						waitHandle = null;
						return false;
					}
				}
				finally
				{
					if (tasks.Count == 0 && Continue && !cancellationToken.IsCancellationRequested)
						tasksFilled.Reset();
				}
			}
		} // proc DequeueTask

		private void EnqueueTask(SendOrPostCallback d, object state, ManualResetEventSlim waitHandle)
		{
			lock (tasksFilled)
			{
				tasks.Enqueue(new CurrentTask() { Delegate = d, State = state, WaitHandle = waitHandle });
				if (tasks.Count > 0)
					tasksFilled.Set();
			}
		} // proc EnqueueTask

		protected void PulseTaskQueue()
		{
			lock (tasksFilled)
				tasksFilled.Set();
		} // proc PulseTaskQueue

		protected void ProcessMessageLoopUnsafe(CancellationToken cancellationToken)
		{
			// if cancel, then run the loop, we avoid an TaskCanceledException her
			cancellationToken.Register(PulseTaskQueue);

			// process messages until cancel
			while (!cancellationToken.IsCancellationRequested && Continue)
			{
				// process queue
				while (TryDequeueTask(cancellationToken, out var d, out var state, out var wait))
				{
					d(state);
					if (wait != null)
						wait.Set();
				}

				// wait for event
				tasksFilled.Wait();
			}
		} // proc ProcessMessageLoop

		public void ProcessMessageLoop(INotifyCompletion onCompletion)
		{
			using (var cancellationTokenSource = new CancellationTokenSource())
			{
				onCompletion.OnCompleted(cancellationTokenSource.Cancel);
				ProcessMessageLoop(cancellationTokenSource.Token);
			}
		} // proc ProcessMessageLoop

		public void ProcessMessageLoop(CancellationToken cancellationToken)
		{
			VerifyThreadAccess();
			ProcessMessageLoopUnsafe(cancellationToken);
		} // proc ProcessMessageLoop

		public sealed override void Post(SendOrPostCallback d, object state)
			=> EnqueueTask(d, state, null);

		public sealed override void Send(SendOrPostCallback d, object state)
		{
			using (var waitHandle = new ManualResetEventSlim(false))
			{
				EnqueueTask(d, state, waitHandle);
				waitHandle.Wait();
			}
		} // proc Send

		protected abstract Thread QueueThread { get; }
		protected bool Continue => doContinue;
	} // class PpsSynchronizationContext

	#endregion

	#region -- class PpsSingleThreadSynchronizationContext ------------------------------

	/// <summary>For background task, we want one execution thread, that we do not
	/// switch between thread, and destroy the assigned context to an thread</summary>
	public sealed class PpsSingleThreadSynchronizationContext : PpsSynchronizationContext
	{
		private struct NoneResult { }

		private readonly Thread thread;
		
		private readonly TaskCompletionSource<NoneResult> taskCompletion = new TaskCompletionSource<NoneResult>();

		public PpsSingleThreadSynchronizationContext(string name, CancellationToken cancellationToken, Func<Task> mainProc)
		{
			cancellationToken.Register(Finish);

			this.thread = new Thread(ExecuteMessageLoop)
			{
				Name = name,
				IsBackground = true,
				Priority = ThreadPriority.BelowNormal
			};

			// single thread apartment
			thread.SetApartmentState(ApartmentState.STA);

			Post(
				state => mainProc().GetAwaiter().OnCompleted(Finish), null
			);

			thread.Start();
		} // ctor

		public void Finish()
			=> Stop();

		private void ExecuteMessageLoop()
		{
			var oldContext = Current;
			SetSynchronizationContext(this);
			try
			{
				ProcessMessageLoopUnsafe(CancellationToken.None);
				taskCompletion.TrySetResult(new NoneResult());
			}
			catch (Exception e)
			{
				taskCompletion.TrySetException(e);
			}
			finally
			{
				SetSynchronizationContext(oldContext);
			}
		} // proc ExecuteMessageLoop

		public Task Task => taskCompletion.Task;

		protected override Thread QueueThread => thread;
	} // class PpsSingleThreadSynchronizationContext

	#endregion

	public static class StuffThreading
	{
		private static void AwaitTaskInternal(INotifyCompletion awaiter)
		{
			if (SynchronizationContext.Current is DispatcherSynchronizationContext)
			{
				var frame = new DispatcherFrame();

				// get the awaiter
				awaiter.OnCompleted(() => frame.Continue = false);

				// block ui for the task
				using (PpsEnvironment.GetEnvironment().BlockAllUI(frame))
					Dispatcher.PushFrame(frame);
			}
			else if (SynchronizationContext.Current is PpsSynchronizationContext ctx)
				ctx.ProcessMessageLoop(awaiter);
		} // func RunTaskSyncInternal

		public static SynchronizationContext VerifySynchronizationContext()
		{
			var ctx = SynchronizationContext.Current;
			if (ctx is DispatcherSynchronizationContext || ctx is PpsSynchronizationContext)
				return ctx;
			else
				throw new InvalidOperationException($"The synchronization context must be in the single-threaded.");
		} // func VerifySynchronizationContext

		/// <summary>Runs the async task in the ui thread (it simulates the async/await pattern for scripts).</summary>
		/// <param name="task"></param>
		/// <remarks></remarks>
		public static void AwaitTask(this Task task)
		{
			if (!task.IsCompleted)
				AwaitTaskInternal(task.GetAwaiter());
			task.Wait();
		} // proc AwaitTask

		/// <summary>Runs the async task in the ui thread (it simulates the async/await pattern for scripts).</summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="task"></param>
		/// <returns></returns>
		public static T AwaitTask<T>(this Task<T> task)
		{
			if (!task.IsCompleted)
				AwaitTaskInternal(task.GetAwaiter());
			return task.Result;
		} // proc AwaitTask	
	} // class StuffThreading
}
