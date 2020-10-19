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
using System.Threading;
using System.Threading.Tasks;
using TecWare.DE.Stuff;
using TecWare.PPSn.UI;

namespace TecWare.PPSn
{
	#region -- interface IPpsAsyncService ---------------------------------------------

	/// <summary>Service for async tasks.</summary>
	public interface IPpsAsyncService
	{
		/// <summary>Await a task.</summary>
		/// <param name="t"></param>
		void Await(Task t);
	} // interface IPpsAsyncService

	#endregion

	#region -- interface IPpsProcessMessageLoop ------------------------------------------

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
		#region -- Await --------------------------------------------------------------

		private static IPpsAsyncService GetAsyncHelper()
			=> GetService<IPpsAsyncService>(false);

		private static void AwaitCore(Task t)
		{
			if (asyncHelper.Value == null)
				t.Wait();
			else
				asyncHelper.Value.Await(t);
		} // proc AwaitCore

		/// <summary>Await for a task.</summary>
		/// <param name="t"></param>
		public static void Await(this Task t)
			=> AwaitCore(t);

		/// <summary>Await for a task.</summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="t"></param>
		/// <returns></returns>
		public static T Await<T>(this Task<T> t)
		{
			AwaitCore(t);
			return t.Result;
		} // func Await

		/// <summary>Fork a task from the current execution thread. This will not change the <see cref="SynchronizationContext"/>.</summary>
		/// <param name="task"></param>
		/// <param name="serviceProvider"></param>
		public static void Spawn(this Task task, IServiceProvider serviceProvider = null)
			=> task.ContinueWith(t => GetService<IPpsUIService>(serviceProvider, true).ShowException(t.Exception), TaskContinuationOptions.OnlyOnFaulted);

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
				t => GetService<IPpsUIService>(true).ShowExceptionAsync(background, t.Exception),
				TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnFaulted
			);
		} // proc OnException

		/// <summary>Spawn the task, an supress exceptions silent.</summary>
		/// <param name="task"></param>
		public static void Silent(this Task task)
		{
			task.ContinueWith(
				t => Debug.Print(t.Exception.ToString()),
				TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnFaulted
			);
		} // proc Silent

		/// <summary>Invoke task a return <c>false</c> on exception.</summary>
		/// <param name="task"></param>
		/// <returns></returns>
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
		public static async Task<T> TaskTimeout<T>(this Task<T> task, int timeout)
		{
			var timeoutTask = Task.Delay(timeout);
			var r = await Task.WhenAny(task, timeoutTask).ConfigureAwait(false);
			if (r == timeoutTask)
				throw new TimeoutException();
			return task.Result;
		} // func WaitTimeout

		#endregion
	} // class PpsShell
}
