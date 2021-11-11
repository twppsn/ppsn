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
using System.Reflection;
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
		#region -- class WeakEventSlot ------------------------------------------------

		private sealed class WeakEventSlot
		{
			private WeakReference targetReference;
			private MethodInfo method;

			public WeakEventSlot(TEVENT ev)
			{
				Set(ev);
			} // ctor

			public bool IsEqual(TEVENT ev)
				=> IsAlive && ReferenceEquals(ev.Target, targetReference.Target) && ev.Method == method;

			public bool Set(TEVENT ev)
			{
				if (ev == null)
				{
					targetReference = null;
					method = null;
				}
				else
				{
					targetReference = ev.Target != null ? new WeakReference(ev.Target) : null;
					method = ev.Method;
				}
				return true;
			} // proc Set

			public bool TryGet(out Delegate ev)
			{
				if (IsAlive)
				{
					if (targetReference == null)
						ev = Delegate.CreateDelegate(typeof(TEVENT), method);
					else
						ev = Delegate.CreateDelegate(typeof(TEVENT), targetReference.Target, method);
					return true;
				}
				else
				{
					ev = null;
					return false;
				}
			} // func TryGet

			public bool IsAlive => targetReference == null || targetReference.IsAlive;
		} // class WeakEventSlot

		#endregion

		private readonly List<WeakEventSlot> slots = new List<WeakEventSlot>();

		/// <summary></summary>
		public WeakEventList()
		{
		} // ctor

		private bool Enumerate(Func<WeakEventSlot, bool> action)
		{
			var ret = false;
			for (var i = slots.Count - 1; i >= 0; i--)
			{
				if (!ret)
					ret = action(slots[i]);
			}
			return ret;
		} // proc Enumerate

		/// <summary></summary>
		/// <param name="handler"></param>
		public void Add(TEVENT handler)
		{
			if (!Enumerate(
				slot =>
				{
					if (!slot.IsAlive)
						return slot.Set(handler);
					else
						return false;
				}
			))
			{
				slots.Add(new WeakEventSlot(handler));
			}
		} // proc Add

		/// <summary></summary>
		/// <param name="handler"></param>
		public void Remove(TEVENT handler)
			=> Enumerate(slot => slot.IsEqual(handler) && slot.Set(null));

		/// <summary></summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		public void Invoke(object sender, TEVENTARGS e)
		{
			Enumerate(
				slot =>
				{
					if(slot.TryGet(out var ev))
						ev.DynamicInvoke(sender, e);
					return false;
				}
			);
		} // proc Invoke
	} // class WeakEventList
}
