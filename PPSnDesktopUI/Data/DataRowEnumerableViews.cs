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
using System.Windows.Data;
using TecWare.DE.Data;

namespace TecWare.PPSn.Data
{
	#region -- interface IPpsDataRowViewFilter ----------------------------------------

	/// <summary>Contract to make a CollectionView filterable.</summary>
	public interface IPpsDataRowViewFilter
	{
		/// <summary>Filter expression.</summary>
		PpsDataFilterExpression FilterExpression { get; set; }
	} // class IPpsDataRowViewFilter

	#endregion

	#region -- interface IPpsDataRowViewSort ------------------------------------------

	/// <summary>Contract to make a CollectionView filterable.</summary>
	public interface IPpsDataRowViewSort
	{
		/// <summary>Sort expression.</summary>
		IEnumerable<PpsDataOrderExpression> Sort { get; set; }
	} // class IPpsDataRowViewSort

	#endregion

	#region -- class PpsDataRowEnumerableCollectionView -------------------------------

	/// <summary>CollectionView for IDataRowEnumerable's</summary>
	public class PpsDataRowEnumerableCollectionView : CollectionView, IPpsDataRowViewFilter, IPpsDataRowViewSort
	{
		#region -- class CachedRowEnumerator ------------------------------------------

		private sealed class CachedRowEnumerator : IEnumerator
		{
			private readonly PpsDataRowEnumerableCollectionView parent;
			private readonly List<IDataRow> cachedRows;
			private int currentIndex = -1;

			public CachedRowEnumerator(PpsDataRowEnumerableCollectionView parent, List<IDataRow> cachedRows)
			{
				this.parent = parent ?? throw new ArgumentNullException(nameof(parent));
				this.cachedRows = cachedRows ?? throw new ArgumentNullException(nameof(cachedRows));
			} // ctor
						
			public void Reset()
				=> currentIndex = -1;

			public bool MoveNext()
			{
				if (cachedRows != parent.currentFetchedRows)
					throw new InvalidOperationException();

				return parent.EnsureItem(++currentIndex);
			} // func MoveNext

			public object Current => currentIndex >= 0 && currentIndex < cachedRows.Count ? cachedRows[currentIndex] : null;
		} // class CachedRowEnumerator

		#endregion

		private readonly IDataRowEnumerable baseEnumerable;
		private readonly int blockFetchSize = 100;

		private IEnumerator<IDataRow> currentEnumerator = null; // current row source
		private bool currentEnumeratorDisposed = false; // is the end is reached
		private List<IDataRow> currentFetchedRows = null; // cache for the currently readed rows

		private readonly SortDescriptionCollection sortDescriptions = new SortDescriptionCollection();
		private PpsDataFilterExpression filterExpression = null;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary>Create a collection-view for the IDataRowEnumerable</summary>
		/// <param name="collection"></param>
		public PpsDataRowEnumerableCollectionView(IDataRowEnumerable collection) 
			: base(collection)
		{
			baseEnumerable = collection ?? throw new ArgumentNullException(nameof(collection));

			// todo: hook baseEnumerable.CollectionChanged

			((INotifyCollectionChanged)sortDescriptions).CollectionChanged += (object sender, NotifyCollectionChangedEventArgs e) => RefreshOrDefer();
		} // ctor

		/// <summary>Unhook source events</summary>
		public override void DetachFromSourceCollection()
			=> base.DetachFromSourceCollection();

		#endregion

		#region -- Core DataRow Enumerator --------------------------------------------

		private void ResetDataRowEnumerator()
		{
			currentEnumerator = null;
			currentEnumeratorDisposed = false;
			currentFetchedRows = null;
		} // proc ResetDataRowEnumerator

		private IEnumerator<IDataRow> GetDataRowEnumerator()
		{
			var newEnumerable = baseEnumerable;

			if (filterExpression != null && filterExpression != PpsDataFilterExpression.True)
				newEnumerable = newEnumerable.ApplyFilter(filterExpression);

			if(sortDescriptions.Count > 0)
				newEnumerable = newEnumerable.ApplyOrder(Sort);

			return newEnumerable.GetEnumerator();
		} // func GetDataRowEnumerator

		private bool EnsureItem(int index)
		{
			// enumeration is finished
			if (currentEnumeratorDisposed)
				return index < (currentFetchedRows?.Count ?? 0);

			// start enumeration
			if (currentEnumerator == null)
			{
				currentEnumerator = GetDataRowEnumerator();
				currentFetchedRows = new List<IDataRow>();
			}

			if (index >= currentFetchedRows.Count) // do we need to fetch a next block
			{
				var changed = false;
				var readToCount = ((index / blockFetchSize) + 1) * blockFetchSize;
				while (currentFetchedRows.Count < readToCount)
				{
					if (currentEnumerator.MoveNext())
					{
						currentFetchedRows.Add(currentEnumerator.Current);
						changed = true;
					}
					else
					{
						currentEnumerator.Dispose();
						currentEnumerator = null;
						currentEnumeratorDisposed = true;
						break;
					}
				}

				// notify changes
				if (changed)
				{
					OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
					OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
				}
			}

			return index < currentFetchedRows.Count;
		} // func EnsureItem

		#endregion

		#region -- CollectionView -----------------------------------------------------

		/*
		 * This part override's all methods to avoid a call on EnumerableWrapper.
		 */

		/// <summary>Refresh data</summary>
		protected override void RefreshOverride()
		{
			ResetDataRowEnumerator();
			base.RefreshOverride();
		} // proc RefreshOverride

		/// <summary>Return enumerator to fetch current rows.</summary>
		/// <returns></returns>
		protected override IEnumerator GetEnumerator()
		{
			EnsureItem(-1);
			return new CachedRowEnumerator(this, currentFetchedRows);
		} // func GetEnumerator

		/// <summary>Return the cached items.</summary>
		/// <param name="index"></param>
		/// <returns></returns>
		public override object GetItemAt(int index)
			=> EnsureItem(index) ? currentFetchedRows[index] : null;

		/// <summary>Index odf the item</summary>
		/// <param name="item"></param>
		/// <returns></returns>
		public override int IndexOf(object item) 
			=> currentFetchedRows?.IndexOf((IDataRow)item) ?? -1;

		/// <summary>Test if this collection has items</summary>
		public override bool IsEmpty
			=> EnsureItem(0);

		#endregion

		/// <summary>Change sort order of the data.</summary>
		public IEnumerable<PpsDataOrderExpression> Sort
		{
			get => sortDescriptions.Select(GetOrderExpression);
			set
			{
				using (DeferRefresh())
				{
					sortDescriptions.Clear();
					if (value != null)
					{
						foreach (var s in value)
							sortDescriptions.Add(GetSortDescription(s));
					}
				}
			}
		} // prop Sort

		/// <summary>Change filter of the data.</summary>
		public PpsDataFilterExpression FilterExpression
		{
			get => filterExpression;
			set
			{
				filterExpression = value;
				RefreshOrDefer();
			}
		} // prop Filter

		/// <summary>It is not allowed to set the filter property.</summary>
		public sealed override bool CanFilter => false;
		/// <summary>Ignore filter property</summary>
		public sealed override Predicate<object> Filter { get => null; set => throw new NotSupportedException(); }

		/// <summary>It is allowed to change the order with sort description.</summary>
		public sealed override bool CanSort => true;
		/// <summary>Return sort descriptions</summary>
		public sealed override SortDescriptionCollection SortDescriptions => sortDescriptions;

		/// <summary>Returns it self.</summary>
		public override IEnumerable SourceCollection => this;
		/// <summary>Number of rows in this view.</summary>
		public sealed override int Count => currentFetchedRows?.Count ?? 0;

		private static SortDescription GetSortDescription(PpsDataOrderExpression s)
			=> new SortDescription(s.Identifier, s.Negate ? ListSortDirection.Descending : ListSortDirection.Ascending);

		private static PpsDataOrderExpression GetOrderExpression(SortDescription item)
			=> new PpsDataOrderExpression(item.Direction != ListSortDirection.Ascending, item.PropertyName);
	} // class PpsDataRowEnumerableCollectionView

	#endregion
} 
