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
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using TecWare.DE.Data;
using TecWare.PPSn.Core.Data;

namespace TecWare.PPSn.Data
{
	#region -- class PpsDataQueryView -------------------------------------------------

	public class PpsDataQueryView : DependencyObject, IPpsDataRowViewFilter, IEnumerable<IDataRow>, INotifyCollectionChanged //, ICollectionViewFactory
	{
		//#region -- class PpsDataQueryEnumerator ---------------------------------------

		//private sealed class PpsDataQueryEnumerator : IEnumerator<IDataRow>
		//{

		//} // class PpsDataQueryEnumerator

		//#endregion

		public event NotifyCollectionChangedEventHandler CollectionChanged;

		private PpsDataQuery query = null;

		private void OnCollectionChanged()
			=> CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

		public IEnumerator<IDataRow> GetEnumerator()
		{
			var shell = this.GetControlService<IPpsShell>(false);
			if (shell == null)
				return Array.Empty<IDataRow>().OfType<IDataRow>().GetEnumerator();

			return Task.Run(() => shell.GetViewData(query).ToArray()).Await().OfType<IDataRow>().GetEnumerator();
		} // func GetEnumerator

		IEnumerator IEnumerable.GetEnumerator()
			=> GetEnumerator();

		#region -- ViewName - property ------------------------------------------------

		public static readonly DependencyProperty ViewNameProperty = DependencyProperty.Register(nameof(ViewName), typeof(string), typeof(PpsDataQueryView), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnViewNameChanged)));

		private static void OnViewNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsDataQueryView)d).OnViewNameChanged((string)e.OldValue, (string)e.NewValue);

		private void OnViewNameChanged(string oldValue, string newValue)
		{
			query = new PpsDataQuery(newValue);
			OnCollectionChanged(); 
			// todo:
			// collection changed -> empty
			// request to server
			// collection changed -> has rows
		} // proc OnViewNameChanged

		public string ViewName { get => (string)GetValue(ViewNameProperty); set => SetValue(ViewNameProperty, value); }

		#endregion

		#region -- FilterExpression - property ----------------------------------------

		PpsDataFilterExpression IPpsDataRowViewFilter.FilterExpression
		{
			get => query != null ? query.Filter : PpsDataFilterExpression.False;
			set
			{
				if (query == null)
					return;
				query.Filter = value;
				OnCollectionChanged();
			}
		} // prop FilterExpression

		#endregion
	} // class PpsDataQueryView

	#endregion
}
