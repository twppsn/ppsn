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
using System.Globalization;
using System.Linq;
using System.Windows;
using TecWare.DE.Data;
using TecWare.PPSn.Core.Data;
using TecWare.PPSn.Data;

namespace TecWare.PPSn.UI
{
	#region -- class PpsDataQueryView -------------------------------------------------

	public class PpsDataQueryView : DependencyObject, IDataRowEnumerable, INotifyCollectionChanged, ICollectionViewFactory
	{
		#region -- class PpsDataQueryBuilder ------------------------------------------

		private sealed class PpsDataQueryBuilder : IDataRowEnumerable
		{
			private readonly PpsDataQueryView view;
			private readonly PpsDataQuery query;

			public PpsDataQueryBuilder(PpsDataQueryView view, PpsDataQuery query, PpsDataFilterExpression filter = null, IEnumerable<PpsDataColumnExpression> columns = null, IEnumerable<PpsDataOrderExpression> order = null)
			{
				this.view = view ?? throw new ArgumentNullException(nameof(view));
				this.query = new PpsDataQuery(query, filter, columns, order);
			} // ctor

			public IEnumerator<IDataRow> GetEnumerator()
				=> view.ExecuteQueryCore(query).GetEnumerator();
			
			IEnumerator IEnumerable.GetEnumerator()
				=> GetEnumerator();

			public IDataRowEnumerable ApplyColumns(IEnumerable<PpsDataColumnExpression> columns)
				=> new PpsDataQueryBuilder(view, query, columns: columns);

			public IDataRowEnumerable ApplyFilter(PpsDataFilterExpression filter, Func<string, string> lookupNative = null)
			{
				if (lookupNative != null)
					throw new ArgumentException();
				return new PpsDataQueryBuilder(view, query, filter: filter);
			} // func ApplyFilter

			public IDataRowEnumerable ApplyOrder(IEnumerable<PpsDataOrderExpression> order, Func<string, string> lookupNative = null)
			{
				if (lookupNative != null)
					throw new ArgumentException();
				return new PpsDataQueryBuilder(view, query, order: order);
			} // func ApplyOrder
		} // class PpsDataQueryBuilder

		#endregion

		public event NotifyCollectionChangedEventHandler CollectionChanged;

		private void OnCollectionChanged()
			=> CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

		ICollectionView ICollectionViewFactory.CreateView()
			=> new PpsDataRowEnumerableCollectionView(this);

		#region -- IDataRowEnumerable - members ---------------------------------------

		private PpsDataQueryBuilder CreateQueryBuilder()
		{
			if (String.IsNullOrEmpty(ViewName))
				return new PpsDataQueryBuilder(this, PpsDataQuery.Empty);

			var query = new PpsDataQuery(ViewName)
			{
				Filter = PpsDataFilterExpression.Parse(Filter, formatProvider: CultureInfo.InvariantCulture),
				Columns = PpsDataColumnExpression.Parse(Columns).ToArray(),
				Order = PpsDataOrderExpression.Parse(Order).ToArray()
			};

			return new PpsDataQueryBuilder(this, query);
		} // func CreateQuery

		private IEnumerable<IDataRow> ExecuteQueryCore(PpsDataQuery query)
		{
			var shell = this.GetControlService<IPpsShell>(false);
			if (shell == null || query.ViewId == null)
				return Array.Empty<IDataRow>().OfType<IDataRow>();

			return shell.GetViewData(query);
		} // func ExecuteQueryCore

		private IPpsShell GetShell()
			=> this.GetControlService<IPpsShell>(false);

		IEnumerator<IDataRow> IEnumerable<IDataRow>.GetEnumerator()
			=> CreateQueryBuilder().GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator()
			=> CreateQueryBuilder().GetEnumerator();

		IDataRowEnumerable IDataRowEnumerable.ApplyOrder(IEnumerable<PpsDataOrderExpression> order, Func<string, string> lookupNative)
			=> CreateQueryBuilder().ApplyOrder(order, lookupNative);

		IDataRowEnumerable IDataRowEnumerable.ApplyFilter(PpsDataFilterExpression filter, Func<string, string> lookupNative)
			=> CreateQueryBuilder().ApplyFilter(filter, lookupNative);

		IDataRowEnumerable IDataRowEnumerable.ApplyColumns(IEnumerable<PpsDataColumnExpression> columns)
			=> CreateQueryBuilder().ApplyColumns(columns);

		#endregion

		#region -- ViewName - property ------------------------------------------------

		public static readonly DependencyProperty ViewNameProperty = DependencyProperty.Register(nameof(ViewName), typeof(string), typeof(PpsDataQueryView), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnViewNameChanged)));

		private static void OnViewNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsDataQueryView)d).OnCollectionChanged();

		public string ViewName { get => (string)GetValue(ViewNameProperty); set => SetValue(ViewNameProperty, value); }

		#endregion

		#region -- Columns - property -------------------------------------------------

		public static readonly DependencyProperty ColumnsProperty = DependencyProperty.Register(nameof(Columns), typeof(object), typeof(PpsDataQueryView), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnColumnsChanged)));

		private static void OnColumnsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsDataQueryView)d).OnCollectionChanged();


		public object Columns { get => GetValue(ColumnsProperty); set => SetValue(ColumnsProperty, value); }

		#endregion

		#region -- FilterExpression - property ----------------------------------------

		public static readonly DependencyProperty FilterProperty = DependencyProperty.Register(nameof(Filter), typeof(object), typeof(PpsDataQueryView), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnFilterChanged)));

		private static void OnFilterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsDataQueryView)d).OnCollectionChanged();

		public object Filter { get => GetValue(FilterProperty); set => SetValue(FilterProperty, value); }

		#endregion

		#region -- Order - property ---------------------------------------------------

		public static readonly DependencyProperty OrderProperty = DependencyProperty.Register(nameof(Order), typeof(object), typeof(PpsDataQueryView), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnOrderChanged)));

		private static void OnOrderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsDataQueryView)d).OnCollectionChanged();

		public object Order { get => GetValue(OrderProperty); set => SetValue(OrderProperty, value); }

		#endregion
	} // class PpsDataQueryView

	#endregion
}
