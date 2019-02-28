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
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace TecWare.PPSn
{
	#region -- class StuffThreading ---------------------------------------------------

	/// <summary>Thread helper.</summary>
	public static class StuffThreading
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void AwaitTaskInternal(Task task)
		{
			if (task.IsCompleted)
				return;

			if (SynchronizationContext.Current is DispatcherSynchronizationContext)
			{
				var frame = new DispatcherFrame();

				// get the awaiter
				task.GetAwaiter().OnCompleted(() => frame.Continue = false);

				// block ui for the task
				Thread.Sleep(1); // force context change
				if (frame.Continue)
				{
					using (PpsShell.Current?.CreateProgress(true))
						Dispatcher.PushFrame(frame);
				}

				// thread is cancelled, do not wait for finish
				if (!task.IsCompleted)
					throw new OperationCanceledException();
			}
			else if (SynchronizationContext.Current is PpsSynchronizationContext ctx)
				ctx.ProcessMessageLoop(task);
		} // func RunTaskSyncInternal

		/// <summary>Check if the current synchronization context has a message loop.</summary>
		/// <returns></returns>
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
			AwaitTaskInternal(task);
			task.Wait();
		} // proc AwaitTask

		/// <summary>Runs the async task in the ui thread (it simulates the async/await pattern for scripts).</summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="task"></param>
		/// <returns></returns>
		public static T AwaitTask<T>(this Task<T> task)
		{
			AwaitTaskInternal(task);
			return task.Result;
		} // proc AwaitTask	
	} // class StuffThreading

	#endregion
}
