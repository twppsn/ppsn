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
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using TecWare.DE.Data;

namespace TecWare.PPSn.Data
{
	#region -- class PpsLiveDataRowView -----------------------------------------------

	/// <summary>View container for <see cref="IDataRow"/>.</summary>
	public class PpsDataRowView : ObservableObject
	{
		private readonly IDataRow row;

		/// <summary></summary>
		/// <param name="row"></param>
		public PpsDataRowView(IDataRow row)
		{
			this.row = row ?? throw new ArgumentNullException(nameof(row));

			if (row is INotifyPropertyChanged pc)
				pc.PropertyChanged += Row_PropertyChanged;
		} // ctor

		/// <summary></summary>
		/// <param name="propertyName"></param>
		protected virtual void OnRowPropertyChanged(string propertyName) { }

		private void Row_PropertyChanged(object sender, PropertyChangedEventArgs e)
			=> OnRowPropertyChanged(e.PropertyName);

		/// <summary>Orignal row.</summary>
		public IDataRow Row => row;
	} // class PpsLiveDataRowView

	#endregion

	#region -- class PpsLiveTableViewGenerator ----------------------------------------

	/// <summary></summary>
	/// <typeparam name="T"></typeparam>
	public class PpsLiveTableViewGenerator<T> : IList, IReadOnlyList<T>, INotifyPropertyChanged, INotifyCollectionChanged
		where T : PpsDataRowView
	{
		/// <summary></summary>
		public event PropertyChangedEventHandler PropertyChanged;
		/// <summary></summary>
		public event NotifyCollectionChangedEventHandler CollectionChanged;

		private readonly IList innerList;
		private readonly Dictionary<IDataRow, T> generatedRows = new Dictionary<IDataRow, T>();

		/// <summary></summary>
		/// <param name="innerList"></param>
		public PpsLiveTableViewGenerator(IList innerList)
		{
			this.innerList = innerList ?? throw new ArgumentNullException(nameof(innerList));

			if (innerList is INotifyPropertyChanged pc)
				pc.PropertyChanged += Row_PropertyChanged;
			if (innerList is INotifyCollectionChanged cc)
				cc.CollectionChanged += Row_CollectionChanged;
		} // ctor

		private void Row_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(Count))
				PropertyChanged?.Invoke(this, e);
		} // event Row_PropertyChanged

		private void Row_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			switch (e.Action)
			{
				case NotifyCollectionChangedAction.Add:
				case NotifyCollectionChangedAction.Remove:
				case NotifyCollectionChangedAction.Reset:
				case NotifyCollectionChangedAction.Replace:
				case NotifyCollectionChangedAction.Move:
					CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
					break;
			}
		} // event Row_CollectionChanged

		/// <summary>Create a row view for a IDataRow.</summary>
		/// <param name="row"></param>
		/// <returns></returns>
		protected virtual T CreateView(IDataRow row)
			=> (T)Activator.CreateInstance(typeof(T), row);

		private T GetViewFromRow(IDataRow row)
		{
			if (generatedRows.TryGetValue(row, out var r))
				return r;

			// generate item
			r = CreateView(row);
			generatedRows[row] = r;
			return r;
		} // func GetViewFromRow

		int IList.Add(object value)
			=> throw new NotSupportedException();

		void IList.Insert(int index, object value)
			=> throw new NotSupportedException();

		void IList.Remove(object value)
			=> throw new NotSupportedException();

		void IList.RemoveAt(int index)
			=> throw new NotSupportedException();

		void IList.Clear()
			=> throw new NotSupportedException();

		void ICollection.CopyTo(Array array, int index)
		{
			for (var i = 0; i < array.Length; i++)
				array.SetValue(GetViewFromRow((IDataRow)innerList[index + i]), i);
		} // proc ICollection.CopyTo

		bool IList.Contains(object value)
			=> IndexOf((T)value) >= 0;

		int IList.IndexOf(object value)
			=> IndexOf((T)value);

		/// <summary></summary>
		/// <returns></returns>
		public IEnumerator<T> GetEnumerator()
			=> innerList.Cast<IDataRow>().Select(c => GetViewFromRow(c)).GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator()
			=> GetEnumerator();

		/// <summary></summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public int IndexOf(T value)
		{
			for (var i = 0; i < innerList.Count; i++)
			{
				if (generatedRows.TryGetValue((IDataRow)innerList[i], out var r) && r == value)
					return i;
			}
			return -1;
		} // proc IndexOf

		/// <summary></summary>
		/// <param name="index"></param>
		/// <returns></returns>
		public T this[int index] => GetViewFromRow((IDataRow)innerList[index]);
		/// <summary>Count of elements</summary>
		public int Count => innerList.Count;

		bool IList.IsReadOnly => true;
		bool IList.IsFixedSize => false;

		object ICollection.SyncRoot => null;
		bool ICollection.IsSynchronized => false;

		object IList.this[int index] { get => this[index]; set => throw new NotSupportedException(); }
	} // class PpsLiveTableViewGenerator

	#endregion
}
