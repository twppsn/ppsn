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

namespace TecWare.PPSn.Stuff
{
	/// <summary></summary>
	/// <typeparam name="TEVENT"></typeparam>
	/// /// <typeparam name="TEVENTARGS"></typeparam>
	public sealed class WeakEventList<TEVENT, TEVENTARGS>
		where TEVENT : Delegate
		where TEVENTARGS : EventArgs
	{
		private readonly List<WeakReference<TEVENT>> handler = new List<WeakReference<TEVENT>>();

		/// <summary></summary>
		public WeakEventList()
		{
		} // ctor

		private bool Enumerate<T>(Func<int, TEVENT, T, bool> action, T arg)
		{
			var ret = false;
			for (var i = handler.Count - 1; i >= 0; i--)
			{
				if (handler[i] != null && handler[i].TryGetTarget(out var ev))
				{
					if (!ret)
						ret = action(i, ev, arg);
				}
				else
				{
					if (!ret)
						ret = action(i, null, arg);
					handler[i] = null;
				}
			}
			return ret;
		} // proc Enumerate

		/// <summary></summary>
		/// <param name="handler"></param>
		public void Add(TEVENT handler)
		{
			if (!Enumerate(
				(idx, ev, arg) =>
				{
					if (ev == null)
					{
						this.handler[idx] = new WeakReference<TEVENT>(arg);
						return true;
					}
					else
						return false;
				},
				handler
			))
				this.handler.Add(new WeakReference<TEVENT>(handler));
		} // proc Add

		/// <summary></summary>
		/// <param name="handler"></param>
		public void Remove(TEVENT handler)
		{
			Enumerate(
				(idx, ev, arg) =>
				{
					if (ev != null && ev == arg)
					{
						this.handler[idx] = null;
						return true;
					}
					else
						return false;
				},
				handler
			);
		} // proc Remove

		/// <summary></summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		public void Invoke(object sender, TEVENTARGS e)
		{
			Enumerate(
				(idx, ev, a) =>
				{
					if (ev != null)
						ev.DynamicInvoke(sender, a);
					return false;
				},
				e
			);
		} // proc Invoke
	} // class WeakEventList
}
