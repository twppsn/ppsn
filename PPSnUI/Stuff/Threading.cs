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
using System.Threading;
using System.Threading.Tasks;

namespace TecWare.PPSn.Stuff
{
	#region -- class PpsSynchronizationContext ----------------------------------------

	/// <summary>Synchronization context, that implements message queue behaviour 
	/// in a thread. Tasks </summary>
	public abstract class PpsSynchronizationQueue
	{
		#region -- class PpsSynchronizationContext ------------------------------------

		private sealed class PpsSynchronizationContext : SynchronizationContext, IPpsProcessMessageLoop
		{
			private readonly PpsSynchronizationQueue queue;

			public PpsSynchronizationContext(PpsSynchronizationQueue queue)
			{
				this.queue = queue ?? throw new ArgumentNullException(nameof(queue));
			} // ctor

			public override SynchronizationContext CreateCopy()
				=> queue.CreateSynchronizationContext();

			public override void Post(SendOrPostCallback d, object state)
				=> queue.EnqueueTask(d, state, null);

			public override void Send(SendOrPostCallback d, object state)
			{
				using (var waitHandle = new ManualResetEventSlim(false))
				{
					queue.EnqueueTask(d, state, waitHandle);
					waitHandle.Wait();
				}
			} // proc Send

			void IPpsProcessMessageLoop.ProcessMessageLoop(CancellationToken cancellationToken)
				=> queue.ProcessMessageLoop(cancellationToken);
		} // class PpsSynchronizationContext

		#endregion

		#region -- struct CurrentTask -------------------------------------------------

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

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary></summary>
		protected PpsSynchronizationQueue()
		{
		} // ctor

		/// <summary>Create a synchronization context, for this queue.</summary>
		/// <returns></returns>
		protected SynchronizationContext CreateSynchronizationContext()
			=> new PpsSynchronizationContext(this);

		/// <summary></summary>
		/// <param name="execute"></param>
		protected void Use(Action execute)
		{
			var oldContext = SynchronizationContext.Current;
			SynchronizationContext.SetSynchronizationContext(CreateSynchronizationContext());
			try
			{
				execute();
			}
			finally
			{
				SynchronizationContext.SetSynchronizationContext(oldContext);
			}
		} // proc Use

		/// <summary>Stop the message loop, of the current context/thread.</summary>
		protected void Stop()
		{
			lock (tasksFilled)
			{
				doContinue = false;
				PulseTaskQueue();
			}
		} // proc Stop

		#endregion

		#region -- Message Loop -------------------------------------------------------

		private void VerifyThreadAccess()
		{
			if (Thread != Thread.CurrentThread)
				throw new InvalidOperationException($"Process of the queued task is only allowed in the same thread.(queue threadid {Thread.ManagedThreadId}, caller thread id: {Thread.CurrentThread.ManagedThreadId})");
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

		/// <summary>Enqueue a new task.</summary>
		/// <param name="d"></param>
		/// <param name="state"></param>
		/// <param name="waitHandle"></param>
		protected void EnqueueTask(SendOrPostCallback d, object state, ManualResetEventSlim waitHandle)
		{
			lock (tasksFilled)
			{
				tasks.Enqueue(new CurrentTask() { Delegate = d, State = state, WaitHandle = waitHandle });
				if (tasks.Count > 0)
					tasksFilled.Set();
			}
		} // proc EnqueueTask

		/// <summary>Pulse message loop in the current context.</summary>
		protected void PulseTaskQueue()
		{
			lock (tasksFilled)
				tasksFilled.Set();
		} // proc PulseTaskQueue

		/// <summary>Wait for new tasks to process.</summary>
		/// <param name="wait"></param>
		protected virtual void OnWait(ManualResetEventSlim wait)
			=> wait.Wait();

		/// <summary>Run the message loop in the current context/thread.</summary>
		/// <param name="cancellationToken">Cancellation condition.</param>
		/// <param name="callOnWait"></param>
		protected void ProcessTaskLoopUnsafe(CancellationToken cancellationToken, bool callOnWait)
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
				if (callOnWait)
					OnWait(tasksFilled);
				else
					tasksFilled.Wait();
			}
		} // proc ProcessTaskLoopUnsafe

		/// <summary>Run the message loop in the current context/thread.</summary>
		/// <param name="cancellationToken">Cancellation condition.</param>
		public void ProcessMessageLoop(CancellationToken cancellationToken)
		{
			VerifyThreadAccess();
			ProcessTaskLoopUnsafe(cancellationToken, true);
		} // proc ProcessMessageLoop

		#endregion

		/// <summary>Access the thread.</summary>
		protected abstract Thread Thread { get; }
		/// <summary>Is the message loop active.</summary>
		protected bool Continue => doContinue;
	} // class PpsSynchronizationQueue

	#endregion
	
	#region -- class PpsSynchronizationThread -----------------------------------------

	/// <summary>For background task, we want one execution thread, that we do not
	/// switch between thread, and destroy the assigned context to an thread.</summary>
	public sealed class PpsSynchronizationThread : PpsSynchronizationQueue
	{
		private struct NoneResult { }

		private readonly Thread thread;

		private readonly TaskCompletionSource<NoneResult> taskCompletion = new TaskCompletionSource<NoneResult>();

		/// <summary></summary>
		/// <param name="name"></param>
		/// <param name="cancellationToken"></param>
		/// <param name="mainProc"></param>
		public PpsSynchronizationThread(string name, Func<Task> mainProc, CancellationToken cancellationToken)
		{
			cancellationToken.Register(Finish);

			thread = new Thread(ExecuteMessageLoop)
			{
				Name = name,
				IsBackground = false,
				Priority = ThreadPriority.BelowNormal
			};

			// single thread apartment
			thread.SetApartmentState(ApartmentState.STA);

			EnqueueTask(state => mainProc().GetAwaiter().OnCompleted(Finish), null, null);

			thread.Start();
		} // ctor

		/// <summary>Stop message loop.</summary>
		public void Finish()
			=> Stop();

		private void ExecuteMessageLoopCore()
		{
			try
			{
				ProcessTaskLoopUnsafe(CancellationToken.None, true);
				taskCompletion.TrySetResult(new NoneResult());
			}
			catch (Exception e)
			{
				taskCompletion.TrySetException(e);
			}
		} // proc ExecuteMessageLoopCore

		private void ExecuteMessageLoop()
			=> Use(ExecuteMessageLoopCore);

		/// <summary>Task for this execution thread.</summary>
		public Task Task => taskCompletion.Task;
		/// <summary>Assigned thread.</summary>
		protected override Thread Thread => thread;
	} // class PpsSynchronizationThread

	#endregion

	#region -- class PpsThreadSafeMonitor ---------------------------------------------

	/// <summary>Build a monitor, that raises an exception, if the exit gets called in the wrong thread.</summary>
	public sealed class PpsThreadSafeMonitor : IDisposable
	{
		private readonly object threadLock;
		private readonly int threadId;

		private bool isDisposed = false;

		/// <summary>Enter lock</summary>
		/// <param name="threadLock"></param>
		public PpsThreadSafeMonitor(object threadLock)
		{
			this.threadLock = threadLock;
			this.threadId = Thread.CurrentThread.ManagedThreadId;

			Monitor.Enter(threadLock);
		} // ctor

		/// <summary></summary>
		~PpsThreadSafeMonitor()
		{
			Dispose(false);
		} // dtor

		/// <summary>Exit lock</summary>
		public void Dispose()
		{
			GC.SuppressFinalize(this);
			Dispose(true);
		} // proc Dispose

		private void Dispose(bool disposing)
		{
			if (disposing)
			{
				if (isDisposed)
					throw new ObjectDisposedException(nameof(PpsThreadSafeMonitor));
				if (threadId != Thread.CurrentThread.ManagedThreadId)
					throw new ArgumentException();

				Monitor.Exit(threadLock);
				isDisposed = true;
			}
			else if (!isDisposed)
				throw new ArgumentException();
		} // proc Dispose
	} // class PpsThreadSafeMonitor

	#endregion
}
