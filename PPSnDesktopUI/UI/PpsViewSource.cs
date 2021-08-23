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
using TecWare.PPSn.Controls;
using TecWare.PPSn.Core.Data;
using TecWare.PPSn.Data;

namespace TecWare.PPSn.UI
{
	public class PpsViewSource : IDataRowEnumerable, INotifyCollectionChanged, INotifyPropertyChanged
	{
		/// <summary>If the query or the columns are changed</summary>
		public event PropertyChangedEventHandler PropertyChanged;
		/// <summary>Only Reset for a new query is raised.</summary>
		public event NotifyCollectionChangedEventHandler CollectionChanged;

		private string viewId = null;
		private PpsDataFilterExpression filterExpression = PpsDataFilterExpression.True;
		private PpsDataOrderExpression[] orderExpression = PpsDataOrderExpression.Empty;
		private PpsDataColumnExpression[] columnsExpression = null;

		/// <summary>Return data rows.</summary>
		/// <returns></returns>
		public IEnumerator<IDataRow> GetEnumerator()
		{
			if (String.IsNullOrEmpty(viewId))
			{
				return Array.Empty<IDataRow>()
					.OfType<IDataRow>()
					.GetEnumerator();
			}
			else
			{
				return PpsShell.Current.GetViewData(
					new PpsDataQuery(ViewId)
					{
						Filter = filterExpression,
						Order = orderExpression,
						Columns = columnsExpression
					}
				).GetEnumerator();
			}
		} // func GetEnumerator

		IEnumerator IEnumerable.GetEnumerator()
			=> GetEnumerator();

		IDataRowEnumerable IDataRowEnumerable.ApplyFilter(PpsDataFilterExpression expression, Func<string, string> lookupNative)
		{
			filterExpression = PpsDataFilterExpression.Combine(expression).Reduce();
			return this;
		} // func IDataRowEnumerable.ApplyFilter

		IDataRowEnumerable IDataRowEnumerable.ApplyOrder(IEnumerable<PpsDataOrderExpression> expressions, Func<string, string> lookupNative)
		{
			orderExpression = expressions.ToArray();
			return this;
		} // func IDataRowEnumerable.ApplyOrder

		IDataRowEnumerable IDataRowEnumerable.ApplyColumns(IEnumerable<PpsDataColumnExpression> columns)
		{
			columnsExpression = columns.ToArray();
			return this;
		} // func IDataRowEnumerable.ApplyColumns

		public string ViewId { get; set; }

		public PpsDataFilterExpression Filter { get; set; }
		/// <summary>Columns of the view.</summary>
		public PpsListColumns Columns { get; set; }
	} // class PpsViewSource
}
