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
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
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
		#region -- enum CurrentEnumeratorState ----------------------------------------

		private enum CurrentEnumeratorState
		{
			None,
			// reading a continues stream of rows
			ReadFirstRow,
			ReadBlock,
			Fetching,
			// base enumerator allows block fetch
			BlockMode,
			// end reached
			Closed
		} // enum CurentEnumeratorState

		#endregion

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

		#region -- class WaitForCachedRowsEnumerator ----------------------------------

		private class WaitForCachedRowsEnumerator : IEnumerator
		{
			private readonly CachedRowEnumerable parent;

			public WaitForCachedRowsEnumerator(CachedRowEnumerable parent)
			{
				this.parent = parent ?? throw new ArgumentNullException(nameof(parent));
			} // ctor

			public void Reset() { }

			public bool MoveNext()
			{
				if (parent.Parent != null)
					throw new InvalidOperationException();

				return false;
			} // func MoveNext

			public object Current => null;
		} // class WaitForCachedRowsEnumerator

		#endregion

		#region -- class CachedRowEnumerable ------------------------------------------

		private class CachedRowEnumerable : IEnumerable
		{
			private PpsDataRowEnumerableCollectionView parent = null;

			public IEnumerator GetEnumerator()
			{
				return parent == null
					? new WaitForCachedRowsEnumerator(this)
					: parent.GetEnumerator();
			} // func GetEnumerator

			public PpsDataRowEnumerableCollectionView Parent { get => parent; set => parent = value; }
		} // class DataRowEnumerableDummy

		#endregion

		private readonly IDataRowEnumerable baseEnumerable;
		private readonly int blockFetchSize = 100;

		private uint enumeratorVersion = 0;
		private IEnumerator<IDataRow> currentEnumerator = null; // current row source
		private CurrentEnumeratorState currentEnumeratorState = CurrentEnumeratorState.None; // state of the read process
		private List<IDataRow> currentFetchedRows = null; // cache for the currently readed rows
		private Exception fetchException = null;

		private readonly SortDescriptionCollection sortDescriptions = new SortDescriptionCollection();
		private PpsDataFilterExpression filterExpression = null;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary>Create a collection-view for the IDataRowEnumerable</summary>
		/// <param name="collection"></param>
		public PpsDataRowEnumerableCollectionView(IDataRowEnumerable collection)
			: base(new CachedRowEnumerable())
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
			currentEnumeratorState = CurrentEnumeratorState.None;
			currentFetchedRows = null;
			unchecked { enumeratorVersion++; }
		} // proc ResetDataRowEnumerator

		private IDataRowEnumerable GetDataRowEnumerable()
		{
			var newEnumerable = baseEnumerable;

			if (filterExpression != null && filterExpression != PpsDataFilterExpression.True)
				newEnumerable = newEnumerable.ApplyFilter(filterExpression);

			if (sortDescriptions.Count > 0)
				newEnumerable = newEnumerable.ApplyOrder(Sort);

			return newEnumerable;
		} // func GetDataRowEnumerable

		private IEnumerator<IDataRow> GetDataRowStreamEnumerator()
			=> GetDataRowEnumerable().GetEnumerator();

		private async Task StartEnumeratorAsync(uint callEnumeratorVersion)
		{
			currentEnumeratorState = CurrentEnumeratorState.ReadFirstRow;
			currentFetchedRows = new List<IDataRow>();

			if (baseEnumerable is IDataRowEnumerableRange rangeEnumerator)
			{
				currentEnumerator = null;
				currentEnumeratorState = CurrentEnumeratorState.BlockMode;
				OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
			}
			else
			{
				var tmp = await Task.Run(() => GetDataRowStreamEnumerator());

				if (enumeratorVersion == callEnumeratorVersion)
				{
					currentEnumerator = tmp;
					currentEnumeratorState = CurrentEnumeratorState.Fetching;
					OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
				}
				else
				{
					tmp.Dispose();
				}
			}
		} // proc StartEnumerator

		private static (List<IDataRow>, bool) FetchEnumeratorCore(IEnumerator<IDataRow> enumerator, int rowsToRead)
		{
			var collectedRows = new List<IDataRow>(rowsToRead);
			while (rowsToRead-- > 0)
			{
				if (enumerator != null && enumerator.MoveNext())
					collectedRows.Add(enumerator.Current);
				else
				{
					enumerator?.Dispose();
					return (collectedRows, true);
				}
			}
			return (collectedRows, false);
		} // func FetchEnumeratorCore

		private static (List<IDataRow>, bool) FetchBlockCore(IDataRowEnumerableRange enumerable, int start, int count)
		{
			var collectedRows = new List<IDataRow>(count);

			using (var e = enumerable.GetEnumerator(start, count))
			{
				while (e.MoveNext())
					collectedRows.Add(e.Current.ToMyData());
			}

			return (collectedRows, collectedRows.Count < count);
		} // func FetchBlockCore

		private async Task FetchBlockAsync(int readToCount, uint callEnumeratorVersion)
		{
			var isBlockMode = currentEnumeratorState == CurrentEnumeratorState.BlockMode;
			currentEnumeratorState = CurrentEnumeratorState.ReadBlock;

			// fetch block in background
			var rowCount = readToCount - currentFetchedRows.Count;
			var (rows, isEof) = isBlockMode
				? await Task.Run(() => FetchBlockCore((IDataRowEnumerableRange)GetDataRowEnumerable(), currentFetchedRows.Count, rowCount))
				: await Task.Run(() => FetchEnumeratorCore(currentEnumerator, rowCount));

			if (enumeratorVersion == callEnumeratorVersion)
			{
				var firstRow = currentFetchedRows.Count > 0 ? currentFetchedRows[0] : null;

				// update view
				if (Filter != null)
				{
					foreach (var r in rows)
					{
						// check if alread added
						if (firstRow == r)
							continue;

						// filter
						if (Filter(r))
							currentFetchedRows.Add(r);
					}
				}
				else if (firstRow != null)
					currentFetchedRows.AddRange(rows.Where(c => c != firstRow));
				else
					currentFetchedRows.AddRange(rows);

				currentEnumeratorState = isEof ? CurrentEnumeratorState.Closed : (isBlockMode ? CurrentEnumeratorState.BlockMode : CurrentEnumeratorState.Fetching);
				if (isEof)
					currentEnumerator = null;

				// notify changes
				if (rows.Count > 0)
				{
					OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
					OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
				}
			}
		} // proc FetchBlock

		private void CheckResult(Task task)
		{
			task.ContinueWith(
				t => CloseEnumerator(t.Exception.InnerException),
				TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously
			);
		} // proc CheckResult

		private void CloseEnumerator(Exception innerException = null)
		{
			try { currentEnumerator.Dispose(); }
			catch { }
			currentEnumerator = null;
			currentEnumeratorState = CurrentEnumeratorState.Closed;
			fetchException = innerException;
		} // proc CloseEnumerator

		private bool EnsureItem(int index)
		{
			// enumeration is finished
			switch (currentEnumeratorState)
			{
				case CurrentEnumeratorState.None: // start
					if (currentEnumerator == null)
						CheckResult(StartEnumeratorAsync(enumeratorVersion));
					break;

				case CurrentEnumeratorState.ReadFirstRow:
				case CurrentEnumeratorState.ReadBlock: // in async read block
					break;
				case CurrentEnumeratorState.Fetching: // fetch current block
				case CurrentEnumeratorState.BlockMode:
					if (index >= currentFetchedRows.Count - 1) // do we need to fetch a next block
						CheckResult(FetchBlockAsync((((index + 1) / blockFetchSize) + 1) * blockFetchSize, enumeratorVersion));
					break;

				case CurrentEnumeratorState.Closed:
					if (fetchException != null)
						throw new AggregateException(fetchException);
					break;

				default:
					throw new InvalidOperationException();
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

			if (IsEmpty)
				MoveCurrentToPosition(-1);

			OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
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
		{
			if (IsEmpty) // ensure first row
				return -1;

			var idx = currentFetchedRows.IndexOf((IDataRow)item);
			if (idx >= 0)
				return idx;  // item is cached

			// item is not cached, yet
			// add as first item, may a problem could be that the item is views twice
			if (CheckCompatibleRow(item))
			{
				currentFetchedRows.Insert(0, (IDataRow)item);
				return 0;
			}
			else
				return -1;
		} // func IndexOf

		private bool CheckCompatibleRow(object item)
		{
			return item is IDataRow; // todo: check enumeration
		} // func CheckCompatibleRow

		/// <summary>Test if this collection has items</summary>
		public override bool IsEmpty
			=> !EnsureItem(0);

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
				if (filterExpression != value)
				{
					filterExpression = value;
					RefreshOrDefer();
				}
			}
		} // prop Filter

		/// <summary>It is allowed to change the order with sort description.</summary>
		public sealed override bool CanSort => true;
		/// <summary>Return sort descriptions</summary>
		public sealed override SortDescriptionCollection SortDescriptions => sortDescriptions;

		/// <summary>Returns it self.</summary>
		public override IEnumerable SourceCollection => this;
		/// <summary>Access the cached rows</summary>
		public IEnumerable CachedRows => (CachedRowEnumerable)base.SourceCollection;

		/// <summary>Number of rows in this view.</summary>
		public sealed override int Count => currentFetchedRows?.Count ?? 0;

		private static SortDescription GetSortDescription(PpsDataOrderExpression s)
			=> new SortDescription(s.Identifier, s.Negate ? ListSortDirection.Descending : ListSortDirection.Ascending);

		private static PpsDataOrderExpression GetOrderExpression(SortDescription item)
			=> new PpsDataOrderExpression(item.Direction != ListSortDirection.Ascending, item.PropertyName);
	} // class PpsDataRowEnumerableCollectionView

	#endregion
} 
