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
using Neo.IronLua;
using TecWare.DE.Data;
using TecWare.DE.Stuff;

namespace TecWare.PPSn.Data
{
	#region -- class PpsFilterableListCollectionView ----------------------------------

	/// <summary>Basic filter and sort implementation.</summary>
	public abstract class PpsFilterableListCollectionView : ListCollectionView, IPpsDataRowViewFilter
	{
		private PpsDataFilterExpression filterExpression = null;
		private Predicate<object> filterCustomFunction = null;
		private Predicate<object> filterExpressionFunction = null;

		/// <summary></summary>
		/// <param name="list"></param>
		protected PpsFilterableListCollectionView(IList list)
			: base(list)
		{
		} // ctor

		/// <summary>Apply a sort expression to the collection view, the interface is not set, because RefreshOverride is not implemented.</summary>
		public IEnumerable<PpsDataOrderExpression> Sort
		{
			get => from s in SortDescriptions select new PpsDataOrderExpression(s.Direction != ListSortDirection.Ascending, s.PropertyName);
			set
			{
				if (!CanSort)
					throw new NotSupportedException();

				SortDescriptions.Clear();
				if (value != null)
				{
					foreach (var s in value)
						SortDescriptions.Add(new SortDescription(s.Identifier, s.Negate ? ListSortDirection.Descending : ListSortDirection.Ascending));
				}

				RefreshOrDefer();
			}
		} // prop Sort

		private bool setBaseFilter = false;

		/// <summary></summary>
		/// <param name="filterExpression"></param>
		/// <returns></returns>
		protected abstract Predicate<object> CreateFilterPredicate(PpsDataFilterExpression filterExpression);

		private bool CombinedFilterFunction(object item)
			=> filterCustomFunction(item) && filterExpressionFunction(item);

		private void UpdateBaseFilter()
		{
			setBaseFilter = true;
			try
			{
				if (filterCustomFunction != null && filterExpressionFunction != null)
					base.Filter = CombinedFilterFunction;
				else if (filterCustomFunction != null)
					base.Filter = filterCustomFunction;
				else
					base.Filter = filterExpressionFunction;
			}
			finally
			{
				setBaseFilter = false;
			}
		} // proc UpdateBaseFilter

		/// <summary>Can filter is always false.</summary>
		public sealed override bool CanFilter => true;

		/// <summary>Filter expression</summary>
		public sealed override Predicate<object> Filter
		{
			get => base.Filter;
			set
			{
				if (setBaseFilter)
					base.Filter = value;
				else
				{
					filterCustomFunction = value;
					UpdateBaseFilter();
				}
			}
		} // prop Filter

		/// <summary>Apply a filter to the collection view.</summary>
		public PpsDataFilterExpression FilterExpression
		{
			get => filterExpression ?? PpsDataFilterExpression.True;
			set
			{
				if (filterExpression != value)
				{
					filterExpression = value;
					filterExpressionFunction = CreateFilterPredicate(filterExpression);
					UpdateBaseFilter();
				}
			}
		} // prop FilterExpression
	} // class PpsFilterableListCollectionView

	#endregion

	#region -- class PpsTypedListCollectionView ---------------------------------------

	/// <summary>Support filter for generic lists</summary>
	public class PpsTypedListCollectionView<T> : PpsFilterableListCollectionView, IPpsDataRowViewFilter
	{
		/// <summary></summary>
		/// <param name="list"></param>
		public PpsTypedListCollectionView(IList list)
			: base(list)
		{
		} // ctor

		/// <summary></summary>
		/// <param name="filterExpression"></param>
		/// <returns></returns>
		protected override Predicate<object> CreateFilterPredicate(PpsDataFilterExpression filterExpression)
		{
			var filterFunc = PpsDataFilterVisitorLambda.CompileTypedFilter<T>(filterExpression);
			return new Predicate<object>(o => filterFunc((T)o));
		} // func CreateFilterPredicate
	} // class PpsTypedListCollectionView

	#endregion

	#region -- class PpsDataCollectionView --------------------------------------------

	/// <summary>Special collection view for PpsDataTable, that supports IDataRowEnumerable</summary>
	public class PpsDataCollectionView : PpsFilterableListCollectionView, IPpsDataRowViewFilter, IPpsDataRowViewSort
	{
		#region -- class DataRowEnumerator --------------------------------------------

		private sealed class DataRowEnumerator : IEnumerator<IDataRow>
		{
			private readonly IEnumerator enumerator;

			public DataRowEnumerator(IEnumerator enumerator)
			{
				this.enumerator = enumerator;
			} // ctor

			public void Dispose()
			{
				if (enumerator is IDisposable d)
					d.Dispose();
			} // proc Dispose

			public void Reset()
				=> enumerator.Reset();

			public bool MoveNext()
				=> enumerator.MoveNext();

			object IEnumerator.Current => enumerator.Current;
			public IDataRow Current => enumerator.Current as IDataRow;
		} // class DataRowEnumerator

		#endregion

		private readonly IDisposable detachView;

		/// <summary>Collection view for PpsDataTable's.</summary>
		/// <param name="dataTable"></param>
		public PpsDataCollectionView(IPpsDataView dataTable)
			: base(dataTable)
		{
			detachView = dataTable as IDisposable;
		} // ctor

		/// <summary>Detach view from CollectionView.</summary>
		public override void DetachFromSourceCollection()
		{
			base.DetachFromSourceCollection();
			detachView?.Dispose();
		} // proc DetachFromSourceCollection

		/// <summary>Implement a add method, that supports a LuaTable as argument.</summary>
		/// <param name="values">Values for the new row.</param>
		/// <returns>Added row.</returns>
		public PpsDataRow Add(LuaTable values)
		{
			var row = DataView.NewRow(DataView.Table.GetDataRowValues(values), null);
			AddNewItem(row);
			return row;
		} // func Add

		/// <summary>Implement a add method, that supports a LuaTable as argument.</summary>
		/// <param name="values">Values for the new row.</param>
		/// <returns>Added row.</returns>
		public PpsDataRow Add(IPropertyReadOnlyDictionary values)
		{
			var row = DataView.NewRow(DataView.Table.GetDataRowValues(values), null);
			AddNewItem(row);
			return row;
		} // func Add

		/// <summary>Auto Commit rows.</summary>
		/// <param name="args"></param>
		protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs args)
		{
			base.OnCollectionChanged(args);
			// we do not want the new/edit
			if (IsAddingNew)
				CommitNew();
			else if (IsEditingItem)
				CommitEdit();
		} // proc OnCollectionChanged

		/// <summary></summary>
		/// <param name="filterExpression"></param>
		/// <returns></returns>
		protected sealed override Predicate<object> CreateFilterPredicate(PpsDataFilterExpression filterExpression)
			=> PpsDataFilterVisitorDataRow.CreateDataRowFilter<object>(filterExpression);

		/// <summary>Parent row, of the current filter.</summary>
		public PpsDataRow Parent => (InternalList as PpsDataRelatedFilter)?.Parent;
		/// <summary>Get the DataView, that is filtered.</summary>
		public IPpsDataView DataView => (IPpsDataView)base.SourceCollection;
	} // class PpsDataCollectionView

	#endregion
}
