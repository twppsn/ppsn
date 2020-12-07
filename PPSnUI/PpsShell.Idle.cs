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

namespace TecWare.PPSn
{
	#region -- enum PpsIdleReturn -----------------------------------------------------

	/// <summary>Return for OnIdle</summary>
	public enum PpsIdleReturn
	{
		/// <summary>Wait for next idle.</summary>
		Idle,
		/// <summary>No more idles needed, before next idle restart.</summary>
		StopIdle,
		/// <summary>Remove this idle action, from idle list.</summary>
		Remove
	} // enum PpsIdleReturn

	#endregion

	#region -- interface IPpsIdleAction -----------------------------------------------

	/// <summary>Implementation for a idle action.</summary>
	public interface IPpsIdleAction
	{
		/// <summary>Gets called on application idle start.</summary>
		/// <param name="elapsed">ms elapsed since the idle started</param>
		/// <returns><see cref="PpsIdleReturn"/></returns>
		// <returns><c>false</c>, if there are more idles needed.</returns>
		PpsIdleReturn OnIdle(int elapsed);
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
		#region -- class IdleActionItem -----------------------------------------------

		private sealed class IdleActionItem
		{
			private readonly WeakReference<IPpsIdleAction> action;
			private bool isStopped = false;

			public IdleActionItem(IPpsIdleAction action)
				=> this.action = new WeakReference<IPpsIdleAction>(action);
			
			public bool IsIdleAction(IPpsIdleAction idleAction)
				=> action.TryGetTarget(out var t) && t == idleAction;

			public void Reset()
				=> isStopped = false;

			public PpsIdleReturn OnIdle(int elapsed)
			{
				if (action.TryGetTarget(out var t))
				{
					if (isStopped)
						return PpsIdleReturn.StopIdle;
					else
					{
						var r = t.OnIdle(elapsed);
						if (r == PpsIdleReturn.StopIdle)
							isStopped = true;
						return r;
					}
				}
				else
					return PpsIdleReturn.Remove;
			} // func OnIdle

			public bool IsStopped => isStopped;
		} // struct IdleActionItem

		#endregion

		private int restartTime = 0;
		private readonly List<IdleActionItem> idleActions = new List<IdleActionItem>();

		/// <summary>Check thread access for add,remove operation.</summary>
		protected virtual void VerifyAccess() { }

		private int IndexOfIdleAction(IPpsIdleAction idleAction)
		{
			for (var i = 0; i < idleActions.Count; i++)
			{
				if (idleActions[i].IsIdleAction(idleAction))
					return i;
			}
			return -1;
		} // func IndexOfIdleAction

		private IPpsIdleAction AddIdleAction(IPpsIdleAction idleAction)
		{
			VerifyAccess();

			if (IndexOfIdleAction(idleAction) == -1)
			{
				idleActions.Add(new IdleActionItem(idleAction));
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
				for (var i = 0; i < idleActions.Count; i++)
					idleActions[i].Reset();

				StartIdleCore();
			}
		} // proc StartIdle

		/// <summary>Execute idle operations</summary>
		/// <param name="stopIdle"></param>
		protected void DoIdle(out bool stopIdle)
		{
			stopIdle = false;
			var timeSinceRestart = unchecked(Environment.TickCount - restartTime);

			for (var i = idleActions.Count - 1; i >= 0; i--)
			{
				switch (idleActions[i].OnIdle(timeSinceRestart))
				{
					case PpsIdleReturn.StopIdle:
						stopIdle = true;
						break;
					case PpsIdleReturn.Remove:
						idleActions.RemoveAt(i);
						break;
				}
			}
		} // proc DoIdle
	} // class PpsIdleServiceBase

	#endregion
}
