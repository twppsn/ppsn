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
using System.Linq;
using System.Windows.Forms;
using TecWare.DE.Data;
using TecWare.DE.Stuff;

namespace TecWare.PPSn.Controls
{
	/// <summary> Base class for PpsFilterTextBoxCell </summary>
	internal partial class PpsFilterControl : UserControl
	{
		#region -- DataColumnrWrapper --------------------------------------------------

		internal class DataColumnrWrapper
		{
			private readonly IDataColumn field;

			public DataColumnrWrapper(IDataColumn field)
			{
				this.field = field;
			}

			public string Name => field.Name;
			public string DisplayName => field.Attributes.GetProperty<string>("DisplayName", field.Name);
		}

		#endregion -- DataColumnrWrapper -----------------------------------------------

		#region -- DataColummns --------------------------------------------------------

		private class DataColummns
		{
			private readonly List<DataColumnrWrapper> columns;

			public DataColummns(IEnumerable<IDataColumn> columns)
			{
				this.columns = new List<DataColumnrWrapper>(columns.Select(c => new DataColumnrWrapper(c)));
			}

			public int Count => columns.Count;
			public IReadOnlyList<DataColumnrWrapper> Columns => columns;
		}

		#endregion -- DataColummns -----------------------------------------------------

		private PpsPopup popup;
		private ListBox definedNamesList;
		private ListBox fieldsList;
		private MonthCalendar dateTimeControl;
		
		private string[] definedNames = null;
		private DataColummns fields = null;
		private IPpsFilterColumn filterColumn;

		public PpsFilterControl()
		{
			InitializeComponent();
		}

		public void DisposeTrue()
			=> CleanUp();

		protected virtual void UpdateControls() { }

		private void UpdateExpression(string newExepression)
		{
			Expression = newExepression;
		}

		private void ShowPopup(ListBox lb)
		{
			lb.SelectionMode = SelectionMode.One;
			lb.BorderStyle = BorderStyle.None;

			lb.Click += HandleListClick;
			lb.DoubleClick += HandleListClick;
			lb.MouseDoubleClick += HandleListClick;
			lb.PreviewKeyDown += HandleListKeyDown;

			popup = new PpsPopup(lb);
			popup.Closed += HandlePopupClosedEvent;
			popup.Padding = new Padding(1);
			lb.MinimumSize = new Size(Width, 100);
			popup.Show(this);
		}

		private void ShowFieldsControl()
		{
			CleanUp();

			fieldsList = new ListBox
			{
				ValueMember = "Name",
				DisplayMember = "DisplayName",
			};

			if (fields != null)
				fieldsList.Items.AddRange(fields.Columns.ToArray());

			ShowPopup(fieldsList);
		}

		private void ShowDefinedNameControl()
		{
			CleanUp();

			definedNamesList = new ListBox();
			if (definedNames != null && definedNames.Length > 0)
				definedNamesList.Items.AddRange(definedNames);

			ShowPopup(definedNamesList);
		} //proc ShowDefinedNameControl

		private void ShowCalenderControl()
		{
			dateTimeControl = new MonthCalendar();
			dateTimeControl.DateSelected += HandleDateTimeSelected;
			dateTimeControl.PreviewKeyDown += HandleDateTimeControlPreviewKeyDown;
			popup = new PpsPopup(dateTimeControl);
			popup.Closed += HandlePopupClosedEvent;
			
			popup.Show(this);
		} // proc ShowCalenderControl

		private void ShowButton(Button btn, bool show)
		{
			if (show)
			{
				if (Controls.IndexOf(btn) == -1)
					Controls.Add(btn);
			}
			else
				Controls.Remove(btn);
			Update();
		} // proc ShowButton

		protected void SetFilterColumn(IPpsFilterColumn column)
		{
			filterColumn = column;
			ShowButton(dateTimeButton, column?.ColumnSourceType == typeof(DateTime));
		} // proc UpdateStyle

		protected void SetFields(IEnumerable<IDataColumn> newFields)
		{
			fields = new DataColummns(newFields);
			ShowButton(fieldButton, fields.Count > 0);
		} // proc SetFields

		protected void SetDefinedNames(string[] newDefinedNames)
		{
			definedNames = newDefinedNames;
			ShowButton(definedNamesButton, newDefinedNames.Length > 0);
		} // proc SetDefinedNames

		/// <summary> Dispose UI Controls</summary>
		private void CleanUp()
		{
			if (popup != null)
			{
				popup.Dispose();
				popup = null;
			}
			if (fieldsList != null)
			{
				fieldsList.Dispose();
				fieldsList = null;
			}

			if (definedNamesList != null)
			{
				definedNamesList.Dispose();
				definedNamesList = null;
			}

			if (dateTimeControl != null)
			{
				dateTimeControl.Dispose();
				dateTimeControl = null;
			}
		}

		#region Handle Controls Events

		#region Handle Popup Events
		private void HandlePopupClosedEvent(object sender, ToolStripDropDownClosedEventArgs e)
		{
			if (e.CloseReason == ToolStripDropDownCloseReason.AppFocusChange || 
				e.CloseReason == ToolStripDropDownCloseReason.AppClicked || 
				e.CloseReason == ToolStripDropDownCloseReason.CloseCalled)
				return;

			if (dateTimeControl != null)
			{
				var date = dateTimeControl.SelectionStart.ToShortDateString();
				if (dateTimeControl.SelectionStart.Date != dateTimeControl.SelectionEnd.Date)
				{
					date = $"#{date}~{dateTimeControl.SelectionEnd.ToShortDateString()}#";
				}
				else
				{
					date = $"#{date}#";
				}
				UpdateExpression(date);
			}
			else if (definedNamesList != null && definedNamesList.SelectedItem is string dn)
			{
				var definedName = $"${dn}";
				UpdateExpression(definedName);
			}
			else if (fieldsList != null && fieldsList.SelectedItem is DataColumnrWrapper fld)
			{
				var definedName = $":{fld.Name}";
				UpdateExpression(definedName);
			}
		}

		#endregion

		#region Handle Buttons Events

		private void HandleButtonClickEvent(object sender, EventArgs e)
		{
			if (sender == fieldButton)
				ShowFieldsControl();
			else if (sender == definedNamesButton)
				ShowDefinedNameControl();
			else if (sender == dateTimeButton)
				ShowCalenderControl();
		}
		
		#endregion

		#region Handel DefinedNames Listbox Events

		private void HandleListKeyDown(object sender, PreviewKeyDownEventArgs e)
		{
			if (e.KeyCode == Keys.Enter)
				popup.Close(ToolStripDropDownCloseReason.Keyboard);
			else if (e.KeyCode == Keys.Escape)
				popup.Close(ToolStripDropDownCloseReason.AppFocusChange);
		}

		private void HandleListClick(object sender, EventArgs e)
		{
			string selectedItem = null;
			if (sender == fieldsList && fieldsList.SelectedItem is DataColumnrWrapper fld)
			{
				selectedItem = $":{fld.Name}";
			}
			else if (sender == definedNamesList && definedNamesList.SelectedItem != null)
			{
				selectedItem = $"${definedNamesList.SelectedItem as string}";
			}

			if (!string.IsNullOrEmpty(selectedItem))
			{
				UpdateExpression(selectedItem);
				popup.Close(ToolStripDropDownCloseReason.ItemClicked);
			} 
			else
				popup.Close(ToolStripDropDownCloseReason.AppClicked);
		} // proc HandleDefinedNamesListClick

		#endregion

		#region Handle DataTimeControl Events

		private void HandleDateTimeSelected(object sender, DateRangeEventArgs e)
			=> popup.Close(ToolStripDropDownCloseReason.ItemClicked);

		private void HandleDateTimeControlPreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
		{
			if (e.KeyCode == Keys.Enter)
				popup.Close(ToolStripDropDownCloseReason.Keyboard);
			else if (e.KeyCode == Keys.Escape)
				popup.Close(ToolStripDropDownCloseReason.AppFocusChange);
		}

		#endregion

		#region -- Handle Expression TextBox Events -----------------------------------

		private void HandleTextBoxKeyDown(object sender, KeyEventArgs e)
		{
			switch(e.KeyCode)
			{
				case Keys.D:
					if (e.Control)
					{
						ShowCalenderControl();
						e.Handled = false;
					}
					return;
				case Keys.N:
					if (e.Control)
					{
						ShowDefinedNameControl();
						e.Handled = false;
					}
					return;

				default:
					e.Handled = false;
					return;
			}
		}

		private void HandleTextBoxKeyPress(object sender, KeyPressEventArgs e)
		{
			switch (e.KeyChar)
			{
				case ':':
					if (tbxExpression.TextLength == 0)
						ShowFieldsControl();
					break;

				case '#':
					if (tbxExpression.TextLength == 0 && filterColumn.ColumnSourceType == typeof(DateTime))
						ShowCalenderControl();
					break;

				case '$':
					if (tbxExpression.TextLength == 0)
						ShowDefinedNameControl();
					break;
			}
		}

		#endregion

		#endregion

		private void HendleGetFocus(object sender, EventArgs e)
			=> UpdateControls();

		public string Expression { get => tbxExpression.Text; set => tbxExpression.Text = value; }
	} // class PpsFilterControl
}
