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
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using TecWare.PPSn.UI;

namespace TecWare.PPSn
{
	#region -- interface IPpsAsyncService ---------------------------------------------

	/// <summary>Service for async tasks.</summary>
	public interface IPpsAsyncService
	{
		/// <summary>Await a task.</summary>
		/// <param name="sp"></param>
		/// <param name="t"></param>
		void Await(IServiceProvider sp, Task t);
	} // interface IPpsAsyncService

	#endregion

	#region -- interface IPpsProcessMessageLoop ---------------------------------------

	/// <summary>Implemented by a <see cref="SynchronizationContext"/> for processing the message loop. Is used by <see cref="IPpsAsyncService"/>.</summary>
	public interface IPpsProcessMessageLoop
	{
		/// <summary>Run message loop.</summary>
		/// <param name="cancellationToken"></param>
		void ProcessMessageLoop(CancellationToken cancellationToken);
	} // interface IPpsProcessMessageLoop

	#endregion

	public static partial class PpsShell
	{
		private static readonly Lazy<IPpsAsyncService> asyncHelper = new Lazy<IPpsAsyncService>(GetAsyncHelper);

		#region -- Await --------------------------------------------------------------

		private static IPpsAsyncService GetAsyncHelper()
			=> GetService<IPpsAsyncService>(false);

		private static void AwaitCore(Task t, IServiceProvider sp)
		{
			if (!t.IsCompleted && asyncHelper.Value != null)
				asyncHelper.Value.Await(sp, t);
		} // proc AwaitCore

		/// <summary>Runs the async task in the ui thread (it simulates the async/await pattern for scripts).</summary>
		/// <param name="sp"></param>
		/// <param name="t"></param>
		public static void Await(this Task t, IServiceProvider sp = null)
		{
			AwaitCore(t, sp ?? Current);
			t.Wait();
		} // proc Await

		/// <summary>Runs the async task in the ui thread (it simulates the async/await pattern for scripts).</summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="sp"></param>
		/// <param name="t"></param>
		/// <returns></returns>
		public static T Await<T>(this Task<T> t, IServiceProvider sp = null)
		{
			AwaitCore(t, sp ?? Current);
			return t.Result;
		} // func Await

		/// <summary>Fork a task from the current execution thread. This will not change the <see cref="SynchronizationContext"/>.</summary>
		/// <param name="task"></param>
		/// <param name="serviceProvider"></param>
		public static void Spawn(this Task task, IServiceProvider serviceProvider = null)
			=> task.ContinueWith(t => GetService<IPpsUIService>(serviceProvider, true).ShowExceptionAsync(false, t.Exception).Await(), TaskContinuationOptions.OnlyOnFaulted);

		/// <summary>Spawn the task, but check for exceptions.</summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="task"></param>
		/// <param name="background"></param>
		/// <returns></returns>
		public static Task<T> OnException<T>(this Task<T> task, bool background = false)
		{
			OnException((Task)task, background);
			return task;
		} // func OnException

		/// <summary>Spawn the task, but check for exceptions.</summary>
		/// <param name="task"></param>
		/// <param name="background"></param>
		public static void OnException(this Task task, bool background = false)
		{
			task.ContinueWith(
				t => GetService<IPpsUIService>(true).ShowExceptionAsync(background, t.Exception).Await(),
				TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnFaulted
			);
		} // proc OnException

		/// <summary>Invoke task a return <c>false</c> on exception.</summary>
		/// <param name="task"></param>
		/// <returns></returns>
		[Obsolete("Use DES.Core Success variant.")]
		public static Task<bool> Success(this Task task)
		{
			return task.ContinueWith(
				t =>
				{
					try
					{
						t.Wait();
						return true;
					}
					catch (Exception e)
					{
						Debug.Print(e.ToString());
						return false;
					}
				}
			);
		} // func Success

		/// <summary>Invoke task and raise on timeout.</summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="task"></param>
		/// <param name="timeout"></param>
		/// <returns></returns>
		[Obsolete("Use DES.Core Timeout variant.")]
		public static async Task<T> TaskTimeout<T>(this Task<T> task, int timeout)
		{
			var timeoutTask = Task.Delay(timeout);
			var r = await Task.WhenAny(task, timeoutTask).ConfigureAwait(false);
			if (r == timeoutTask)
				throw new TimeoutException();
			return task.Result;
		} // func WaitTimeout

		#endregion

		#region -- ProcessMessageLoop -------------------------------------------------

		/// <summary>Run the message loop in the current context/thread.</summary>
		/// <param name="context"></param>
		/// <param name="onCompletion"></param>
		public static void ProcessMessageLoop(this IPpsProcessMessageLoop context, INotifyCompletion onCompletion)
		{
			using (var cancellationTokenSource = new CancellationTokenSource())
			{
				onCompletion.OnCompleted(cancellationTokenSource.Cancel);
				context.ProcessMessageLoop(cancellationTokenSource.Token);
			}
		} // proc ProcessMessageLoop

		/// <summary>Run the message loop in the current content/thread.</summary>
		/// <param name="context"></param>
		/// <param name="task"></param>
		public static void ProcessMessageLoop(this IPpsProcessMessageLoop context, Task task)
		{
			ProcessMessageLoop(context, task.GetAwaiter());

			// thread is cancelled, do not wait for finish
			if (!task.IsCompleted)
				throw new OperationCanceledException();
		} // proc ProcessMessageLoop

		#endregion

		#region -- ToAsyncResult ------------------------------------------------------

		/// <summary>Creates the Begin/End-pattern</summary>
		/// <param name="task"></param>
		/// <param name="callback"></param>
		/// <param name="state"></param>
		/// <returns></returns>
		public static IAsyncResult ToAsyncResult<T>(this Task<T> task, AsyncCallback callback, object state)
		{
			if (task.AsyncState == state)
			{
				if (callback == null)
					return task;
				else
					return task.ContinueWith(t => callback(t), TaskContinuationOptions.ExecuteSynchronously);
			}
			else
			{
				var tcs = new TaskCompletionSource<T>(state);

				task.ContinueWith(
					t =>
					{
						if (t.IsFaulted)
							tcs.TrySetException(t.Exception.InnerException);
						else if (t.IsCanceled)
							tcs.TrySetCanceled();
						else
							tcs.TrySetResult(t.Result);

						callback?.Invoke(tcs.Task);
					},
					TaskContinuationOptions.ExecuteSynchronously
				);

				return tcs.Task;
			}
		} // func ToAsyncResult

		/// <summary></summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="ar"></param>
		/// <returns></returns>
		public static T EndAsyncResult<T>(IAsyncResult ar)
		{
			try
			{
				return ((Task<T>)ar).Result;
			}
			catch (AggregateException e)
			{
				throw e.InnerException;
			}
		} // proc EndAsyncResult

		#endregion
	} // class PpsShell
}
