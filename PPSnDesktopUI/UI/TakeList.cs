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
using System.Collections;
using System.Collections.Specialized;

namespace TecWare.PPSn.UI
{
	internal class TakeList : IList, INotifyCollectionChanged
	{
		#region -- class TopEnumerator ------------------------------------------------

		private sealed class TakeEnumerator : IEnumerator
		{
			private readonly TakeList list;
			private int currentItem = -1;

			public TakeEnumerator(TakeList list)
			{
				this.list = list;
			}

			public object Current => list[currentItem];

			public bool MoveNext()
				=> ++currentItem < list.Count;

			public void Reset()
				=> currentItem = -1;
		} // class TakeEnumerator

		#endregion

		public event NotifyCollectionChangedEventHandler CollectionChanged;

		private readonly IList sourceList;
		private readonly bool lastItems;
		private readonly int maxItems;

		public TakeList(IList sourceList, int maxItems, bool lastItems)
		{
			this.sourceList = sourceList;
			this.maxItems = maxItems;
			this.lastItems = lastItems;
		} // ctor

		private void SourceListChanged( object sender, NotifyCollectionChangedEventArgs e)
		{
			switch(e.Action)
			{
				case NotifyCollectionChangedAction.Add:
				case NotifyCollectionChangedAction.Move:
				case NotifyCollectionChangedAction.Remove:
				case NotifyCollectionChangedAction.Replace:
				case NotifyCollectionChangedAction.Reset:
					CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
					break;
			}
		} // proc SourceListChanged

		public void CopyTo(Array array, int index)
			=> sourceList.CopyTo(array, index);

		private int GetIndex(int index)
		{
			var r = lastItems ? sourceList.Count - index - 1 : index;
			if (r < 0)
				return -1;
			return r;
		} // func GetIndex

		public bool Contains(object value)
			=> IndexOf(value) >= 0;

		public int IndexOf(object value)
		{
			var i = 0;
			foreach (var cur in this)
			{
				if (cur == value)
					return i;
				i++;
			}
			return -1;
		} // func IndexOf

		public IEnumerator GetEnumerator()
			=> new TakeEnumerator(this);

		public int Add(object value) => throw new NotSupportedException();
		public void Clear() => throw new NotSupportedException();
		public void Insert(int index, object value) => throw new NotSupportedException();
		public void Remove(object value) => throw new NotSupportedException();
		public void RemoveAt(int index) => throw new NotSupportedException();

		public bool IsReadOnly => true;
		public bool IsFixedSize => false;
		public object SyncRoot => sourceList.SyncRoot;
		public bool IsSynchronized => sourceList.IsSynchronized;

		public int Count => Math.Min(sourceList.Count, maxItems);
		
		public object this[int index] { get => sourceList[GetIndex(index)]; set => sourceList[GetIndex(index)] = value; }

		public IList SourceList => sourceList;
	} // class TakeList
}
