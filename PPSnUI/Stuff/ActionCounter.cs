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

namespace TecWare.PPSn.Stuff
{
	#region -- class ActionCounter ----------------------------------------------------

	/// <summary>Action counter is a timer, that is good to detect double/tripple-invoke
	/// actions.</summary>
	public sealed class ActionCounter
	{
		private readonly int delay;
		private int actionCounter = 0;
		private int lastActionTick = 0;

		/// <summary>Delay for the actions.</summary>
		/// <param name="delay"></param>
		public ActionCounter(int delay = 5000)
		{
			this.delay = delay;
		} // ctor

		/// <summary>Is the counter reached.</summary>
		/// <param name="max"></param>
		/// <returns></returns>
		public bool IsCounter(int max = 1)
		{
			if (unchecked(Environment.TickCount - lastActionTick) < delay)
			{
				actionCounter++;
				if (actionCounter >= max)
				{
					actionCounter = 0;
					return true;
				}
				else
				{
					lastActionTick = Environment.TickCount;
					return false;
				}
			}
			else
			{
				actionCounter = 0;
				lastActionTick = Environment.TickCount;
				return false;
			}
		} // func IsCounter

		/// <summary>Set the counter to zero.</summary>
		public void Reset()
			=> actionCounter = 0;

		/// <summary>State of counter.</summary>
		public int Counter => actionCounter;
	} // class ActionCounter

	#endregion
}
