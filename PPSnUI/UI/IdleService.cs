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
using System.Text;

namespace TecWare.PPSn.UI
{
	#region -- interface IPpsIdleAction -----------------------------------------------

	/// <summary>Implementation for a idle action.</summary>
	public interface IPpsIdleAction
	{
		/// <summary>Gets called on application idle start.</summary>
		/// <param name="elapsed">ms elapsed since the idle started</param>
		/// <returns><c>false</c>, if there are more idles needed.</returns>
		bool OnIdle(int elapsed);
	} // interface IPpsIdleAction

	#endregion

	#region -- interface IPpsIdleService ----------------------------------------------

	/// <summary>UI idle services</summary>
	public interface IPpsIdleService
	{
		/// <summary>Add a idle action.</summary>
		/// <param name="idleAction"></param>
		/// <returns></returns>
		IPpsIdleAction Add(IPpsIdleAction idleAction);

		/// <summary>Remove a idle action.</summary>
		/// <param name="idleAction"></param>
		void Remove(IPpsIdleAction idleAction);
	} // interface IPpsIdleService

	#endregion

	#region -- class PpsIdleServiceBase -----------------------------------------------

	/// <summary>Basis implementation for an idle service.</summary>
	public abstract class PpsIdleServiceBase : IPpsIdleService
	{
		private int restartTime = 0;
		private readonly List<WeakReference<IPpsIdleAction>> idleActions = new List<WeakReference<IPpsIdleAction>>();

		/// <summary>Check thread access for add,remove operation.</summary>
		protected virtual void VerifyAccess() { }

		private int IndexOfIdleAction(IPpsIdleAction idleAction)
		{
			for (var i = 0; i < idleActions.Count; i++)
			{
				if (idleActions[i].TryGetTarget(out var t) && t == idleAction)
					return i;
			}
			return -1;
		} // func IndexOfIdleAction

		private IPpsIdleAction AddIdleAction(IPpsIdleAction idleAction)
		{
			VerifyAccess();

			if (IndexOfIdleAction(idleAction) == -1)
			{
				idleActions.Add(new WeakReference<IPpsIdleAction>(idleAction));
				if (idleActions.Count == 1)
					RestartIdle();
			}
			return idleAction;
		} // proc AddIdleAction

		IPpsIdleAction IPpsIdleService.Add(IPpsIdleAction idleAction)
			=> AddIdleAction(idleAction);

		private void RemoveIdleAction(IPpsIdleAction idleAction)
		{
			if (idleAction == null)
				return;

			VerifyAccess();

			var i = IndexOfIdleAction(idleAction);
			if (i >= 0)
				idleActions.RemoveAt(i);
		} // proc RemoveIdleAction

		void IPpsIdleService.Remove(IPpsIdleAction idleAction)
			=> RemoveIdleAction(idleAction);

		/// <summary>Start or Restart idle timer.</summary>
		protected abstract void StartIdleCore();

		/// <summary>Restart idle time.</summary>
		/// <returns></returns>
		protected void RestartIdle()
		{
			if (idleActions.Count > 0)
			{
				restartTime = Environment.TickCount;
				StartIdleCore();
			}
		} // proc StartIdle

		/// <summary>Execute idle operations</summary>
		/// <param name="stopIdle"></param>
		protected void DoIdle(out bool stopIdle)
		{
			stopIdle = true;
			var timeSinceRestart = unchecked(Environment.TickCount - restartTime);

			for (var i = idleActions.Count - 1; i >= 0; i--)
			{
				if (idleActions[i].TryGetTarget(out var t))
					stopIdle = stopIdle && !t.OnIdle(timeSinceRestart);
				else
					idleActions.RemoveAt(i);
			}
		} // proc DoIdle
	} // class PpsIdleServiceBase

	#endregion
}
