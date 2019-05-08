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
using System.Drawing;
using System.Windows.Forms;

namespace TecWare.PPSn.Controls
{
	internal interface IPpsFilterGroup
	{
	} // interface IPpsFilterGroup

	#region -- interface IPpsFilterColumn ---------------------------------------------

	internal interface IPpsFilterColumn
	{
		string ColumnName { get; }
		string ColumnSource { get; }

		string Expression { get; set; }
		IPpsFilterGroup Group { get; set; }

		bool IsEmpty { get; }
	} // interface IPpsFilterColumn

	#endregion

	#region -- class PpsFilterEditor --------------------------------------------------

	internal class PpsFilterEditor : DataGridView
	{
		public event EventHandler RequireRefreshFilter;

		private List<IPpsFilterColumn> resultFilter = null;

		#region -- class DataGridViewFilterCell ---------------------------------------

		private sealed class DataGridViewFilterCell : DataGridViewTextBoxCell
		{
			protected override object GetValue(int rowIndex)
			{
				if (OwningColumn is DataGridViewFilterColumn filterColumn)
					return filterColumn.FilterColumn.Expression;
				else
					return null;
			} // func GetValue

			protected override bool SetValue(int rowIndex, object value)
			{
				if (OwningColumn is DataGridViewFilterColumn filterColumn)
				{
					filterColumn.FilterColumn.Expression = (string)value;
					return true;
				}
				else
					return false;
			} // func SetValue

			public override Type FormattedValueType => typeof(string);
			public override Type ValueType { get => typeof(string); set { } }
		} // class DataGridViewFilterCell

		#endregion

		#region -- class DataGridViewFilterColumn -------------------------------------

		private sealed class DataGridViewFilterColumn : DataGridViewColumn
		{
			private readonly IPpsFilterColumn filterColumn;

			public DataGridViewFilterColumn(IPpsFilterColumn filterColumn)
				: base(new DataGridViewFilterCell())
			{
				this.filterColumn = filterColumn ?? throw new ArgumentNullException(nameof(filterColumn));

				// set text
				HeaderText = filterColumn.ColumnName;
				HeaderCell.ToolTipText = filterColumn.ColumnName + "\n" + filterColumn.ColumnSource;
				HeaderCell.Style.WrapMode = DataGridViewTriState.False;

				ReadOnly = false;
			} // ctor

			public IPpsFilterColumn FilterColumn => filterColumn;
		} // class DataGridViewFilterColumn

		#endregion

		public PpsFilterEditor()
		{
			ReadOnly = false;
			EditMode = DataGridViewEditMode.EditOnEnter;
		} // ctor

		public void SetFilter(List<IPpsFilterColumn> newResultFilter)
			=> resultFilter = newResultFilter;

		private DataGridViewFilterColumn FindFilterColumnView(IPpsFilterColumn filterColumn, int offset)
		{
			for(var i = offset;i<Columns.Count;i++)
			{
				if (Columns[i] is DataGridViewFilterColumn filterColumnView && filterColumn == filterColumnView.FilterColumn)
					return filterColumnView;
			}
			return null;
		} // func FindFilterColumnView

		public void RefreshFilter(Predicate<IPpsFilterColumn> isVisible)
		{
			if (resultFilter == null)
			{
				Columns.Clear();
				Rows.Clear();
			}
			else
			{
				var filterIndex = 0;
				var columnIndex = 0;

				while (filterIndex < resultFilter.Count)
				{
					// get source
					var currentFilter = resultFilter[filterIndex++];
					// get target
					var currentColumn = FindFilterColumnView(currentFilter, columnIndex);
					if (currentColumn == null) // not in lsit -> insert new 
					{
						currentColumn = new DataGridViewFilterColumn(currentFilter);
						Columns.Insert(columnIndex, currentColumn);
					}
					else if (currentColumn.Index == columnIndex) // same position -> update
					{
					}
					else // other position -> move
					{
						Columns.Remove(currentColumn);
						Columns.Insert(columnIndex, currentColumn);
					}

					currentColumn.Visible = isVisible(currentFilter);

					columnIndex++;
				}

				// remove unused items
				while (Columns.Count > columnIndex)
					Columns.RemoveAt(Columns.Count - 1);

				// update rows
				if (columnIndex > 0 && Rows.Count == 0)
					Rows.Add();
			}
		} // proc RefreshFilter

		public void InsertFilter(IEnumerable<IPpsFilterColumn> newFilter, Point? insertPoint = null)
		{
			//var pt = PointToClient(insertPoint);

			foreach (var c in newFilter)
				resultFilter.Add(c);

			FireRefreshFilter();
		} // func InsertFilter

		private void FireRefreshFilter()
			=> RequireRefreshFilter?.Invoke(this, EventArgs.Empty);
	} // class PpsFilterEditor

	#endregion
}
