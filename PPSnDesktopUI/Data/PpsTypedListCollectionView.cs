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
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace TecWare.PPSn.Data
{
	/// <summary>Support filter for generic lists</summary>
	public class PpsTypedListCollectionView<T> : ListCollectionView, IPpsDataRowViewFilter, IPpsDataRowViewSort
	{
		private PpsDataFilterExpression filterExpression = null;

		/// <summary></summary>
		/// <param name="list"></param>
		public PpsTypedListCollectionView(IList list)
			: base(list)
		{
		} // ctor

		/// <summary>Apply a sort expression to the collection view.</summary>
		public IEnumerable<PpsDataOrderExpression> Sort
		{
			get => from s in SortDescriptions select new PpsDataOrderExpression(s.Direction != ListSortDirection.Ascending, s.PropertyName);
			set
			{
				SortDescriptions.Clear();
				if (value != null)
				{
					foreach (var s in value)
						SortDescriptions.Add(new SortDescription(s.Identifier, s.Negate ? ListSortDirection.Descending : ListSortDirection.Ascending));
				}

				RefreshOrDefer();
			}
		} // prop Sort

		private bool allowSetFilter = false;

		/// <summary>Filter expression</summary>
		public override Predicate<object> Filter
		{
			get => base.Filter;
			set
			{
				if (!allowSetFilter)
					throw new NotSupportedException();
				base.Filter = value;
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
					allowSetFilter = true;
					try
					{
						var filterFunc = PpsDataFilterVisitorLambda.CompileTypedFilter<T>(filterExpression);
						base.Filter = new Predicate<object>(o => filterFunc((T)o));
					}
					finally
					{
						allowSetFilter = false;
					}
				}
			}
		} // prop FilterExpression
	} // class PpsTypedListCollectionView
}
