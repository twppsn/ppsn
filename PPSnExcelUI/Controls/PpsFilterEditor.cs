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
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using TecWare.PPSn.Data;

namespace TecWare.PPSn.Controls
{
	#region -- interface IPpsFilterExpression -----------------------------------------

	internal interface IPpsFilterExpression : IReadOnlyList<IPpsFilterColumn>
	{
		event EventHandler FilterChanged;

		IPpsFilterGroup CreateFilterGroup(IPpsFilterGroup parentGroup, PpsDataFilterExpressionType type);

		void Insert(int insertAt, IPpsFilterColumn filterColumn);
		bool Remove(int index);

		int IndexOf(IPpsFilterColumn filterColumn);

		IPpsFilterGroup Group { get; }
	} // interface IPpsFilterExpression

	#endregion

	#region -- interface IPpsFilterGroup ----------------------------------------------

	internal interface IPpsFilterGroup
	{
		PpsDataFilterExpressionType Type { get; set; }
		IPpsFilterGroup Group { get; }
		int Level { get; }
	} // interface IPpsFilterGroup

	#endregion

	#region -- interface IPpsFilterColumn ---------------------------------------------

	internal interface IPpsFilterColumn : IPpsFilterColumnFactory
	{
		bool TrySetValue(string value);

		string ColumnName { get; }
		string ColumnSource { get; }

		PpsDataFilterCompareOperator Operator { get; set; }
		string Value { get; }

		IPpsFilterGroup Group { get; set; }

		bool IsEmpty { get; }
		/// <summary>Is this column visible, does the table exist in the result.</summary>
		bool IsVisible { get; }
	} // interface IPpsFilterColumn

	#endregion

	#region -- interface IPpsFilterColumnFactory --------------------------------------

	internal interface IPpsFilterColumnFactory
	{
		IPpsFilterColumn CreateFilterColumn(IPpsFilterExpression filterExpression, IPpsFilterGroup group);
	} // interface IPpsFilterColumnFactory

	#endregion

	#region -- class PpsFilterEditor --------------------------------------------------

	internal class PpsFilterEditor : DataGridView
	{
		private const int groupColumnWidth = 24;

		#region -- interface IFilterToolTip -------------------------------------------

		private interface IFilterToolTip
		{
			string GetToolTip(int rowIndex);
		} // interface IFilterToolTip

		#endregion

		#region -- class FilterGroupCell ----------------------------------------------

		private sealed class FilterGroupCell : DataGridViewCell
		{
			private const int halfWidth = groupColumnWidth / 2;

			private void PaintGroup(Graphics g, Rectangle cellBounds, DataGridViewCellStyle cellStyle, IPpsFilterGroup prevGroup, IPpsFilterGroup group, IPpsFilterGroup nextGroup, out Rectangle nextBounds)
			{
				var parentGroup = group.Group;
				Rectangle bounds;
				if (parentGroup == null) // root will not rendered
				{
					nextBounds = Rectangle.Empty;
					return;
				}
				else if (parentGroup.Group != null)
				{
					var parentPrevGroup = prevGroup != null && prevGroup.Level == group.Level ? prevGroup.Group : prevGroup;
					var parentNextGroup = nextGroup != null && nextGroup.Level == group.Level ? nextGroup.Group : nextGroup;
					PaintGroup(g, cellBounds, cellStyle, parentPrevGroup, parentGroup, parentNextGroup, out bounds);
				}
				else
					bounds = new Rectangle(cellBounds.X, cellBounds.Y, groupColumnWidth, cellBounds.Height);

				// render group
				var isStart = prevGroup == null || prevGroup != group;
				var isEnd = nextGroup == null || group != nextGroup;
				var isHighlighed = ((PpsFilterEditor)DataGridView).highlightedGroup == group;

				var left = bounds.Left + (bounds.Width / 2 - halfWidth / 2);
				var top = bounds.Top;
				var bottom = bounds.Bottom;
				var br = isHighlighed ? SystemBrushes.Highlight : GetCachedBrush(cellStyle.ForeColor);

				if (isStart)
				{
					top += 3;
					g.FillRectangle(br, left, top, halfWidth / 2, 2);

					var textLeft = left + 1;
					var textTop = top + 1;

					if (isStart && isEnd)
						g.DrawString("!", cellStyle.Font, br, textLeft, textTop);
					else
					{
						switch (group.Type)
						{
							case PpsDataFilterExpressionType.And:
								g.DrawString("&", cellStyle.Font, br, textLeft, textTop);
								break;
							case PpsDataFilterExpressionType.Or:
								g.DrawString("+", cellStyle.Font, br, textLeft, textTop);
								break;
							case PpsDataFilterExpressionType.NOr:
								using (var fnt = new Font(cellStyle.Font, FontStyle.Underline))
									g.DrawString("+", fnt, br, textLeft, textTop);
								break;
							case PpsDataFilterExpressionType.NAnd:
								using (var fnt = new Font(cellStyle.Font, FontStyle.Underline))
									g.DrawString("&", fnt, br, textLeft, textTop);
								break;
						}

						// "±"
					}
				}

				if (isEnd)
				{
					bottom -= 3;
					g.FillRectangle(br, left, bottom - 1, halfWidth / 2, 2);
				}

				g.FillRectangle(br, left, top, 2, bottom - top + 1);

				// move next
				nextBounds = bounds;
				nextBounds.X += groupColumnWidth;
			} // proc PaintGroup

			protected override void Paint(Graphics g, Rectangle clipBounds, Rectangle cellBounds, int rowIndex, DataGridViewElementStates cellState, object value, object formattedValue, string errorText, DataGridViewCellStyle cellStyle, DataGridViewAdvancedBorderStyle advancedBorderStyle, DataGridViewPaintParts paintParts)
			{
				PaintBorder(g, clipBounds, cellBounds, cellStyle, advancedBorderStyle);
				g.FillRectangle(GetCachedBrush(cellStyle.BackColor), cellBounds);

				var parentGroup = GetFilterColumn(DataGridView, rowIndex - 1)?.Group;
				var group = GetFilterColumn(DataGridView, rowIndex)?.Group;
				var nextGroup = GetFilterColumn(DataGridView, rowIndex + 1)?.Group;

				// change to same group level
				while (parentGroup != null && parentGroup.Level > group.Level)
					parentGroup = parentGroup.Group;
				while (nextGroup != null && nextGroup.Level > group.Level)
					nextGroup = nextGroup.Group;

				// render groups
				PaintGroup(g,
					cellBounds,
					cellStyle,
					parentGroup,
					group,
					nextGroup,
					out var _
				);
			} // proc Paint

			public IPpsFilterGroup GetHoverGroup(int rowIndex, int x)
			{
				var column = GetFilterColumn(DataGridView, rowIndex);
				if (column != null)
				{
					var g = column.Group;
					var level = (x / groupColumnWidth) + 1;

					while (level < g.Level)
						g = g.Group;

					return g.Level == level ? g : null;
				}
				else
					return null;
			} // func GetHoverGroup

			protected override void OnMouseClick(DataGridViewCellMouseEventArgs e)
			{
				if (e.Button == MouseButtons.Left)
					((PpsFilterEditor)DataGridView).SetGroupChecked(GetHoverGroup(e.RowIndex, e.X));
				else if (e.Button == MouseButtons.Right)
					((PpsFilterEditor)DataGridView).ShowContextMenu(e.ColumnIndex, e.RowIndex, e.X, e.Y, GetHoverGroup(e.RowIndex, e.X));

				base.OnMouseClick(e);
			} // proc OnMouseClick

			public override Type FormattedValueType => typeof(string);
			public override Type ValueType { get => typeof(string); set { } }
		} // class FilterGroupCell

		#endregion

		#region -- class FilterGroupColumn --------------------------------------------

		private sealed class FilterGroupColumn : DataGridViewColumn
		{
			public FilterGroupColumn()
				: base(new FilterGroupCell())
			{
				ReadOnly = true;
			} // ctor
		} // class FilterGroupColumn

		#endregion

		#region -- class FilterGroupSelectCell ----------------------------------------

		private sealed class FilterGroupSelectCell : DataGridViewCell
		{
			private bool isPressed = false;

			private Rectangle GetCheckBoxRect(Rectangle cellBounds)
			{
				return new Rectangle(
					cellBounds.Left + cellBounds.Width / 2 - 7,
					cellBounds.Top + cellBounds.Height / 2 - 7,
					13,
					13
				);
			}// func GetCheckBoxRect

			private bool IsChecked(int rowIndex)
				=> DataGridView is PpsFilterEditor filterEditor && filterEditor.IsRowChecked(rowIndex);

			protected override void Paint(Graphics g, Rectangle clipBounds, Rectangle cellBounds, int rowIndex, DataGridViewElementStates cellState, object value, object formattedValue, string errorText, DataGridViewCellStyle cellStyle, DataGridViewAdvancedBorderStyle advancedBorderStyle, DataGridViewPaintParts paintParts)
			{
				PaintBorder(g, clipBounds, cellBounds, cellStyle, advancedBorderStyle);
				g.FillRectangle(GetBackgroundBrush(cellState, cellStyle), cellBounds);

				var rc = GetCheckBoxRect(cellBounds);
				var state = IsChecked(rowIndex)
					? (isPressed ? CheckBoxState.CheckedPressed : CheckBoxState.CheckedNormal)
					: (isPressed ? CheckBoxState.UncheckedPressed : CheckBoxState.UncheckedNormal);

				CheckBoxRenderer.DrawCheckBox(g, new Point(rc.Left, rc.Top), state);
			} // proc Paint

			private bool IsCheckBoxHit(int columnIndex, int rowIndex, int x, int y)
			{
				var cellBounds = DataGridView.GetCellDisplayRectangle(columnIndex, rowIndex, false);
				var rc = GetCheckBoxRect(cellBounds);
				rc.Offset(-cellBounds.Left, -cellBounds.Top);
				return rc.Contains(x, y);
			} // func IsCheckBoxHit

			protected override void OnMouseDown(DataGridViewCellMouseEventArgs e)
			{
				if (e.Button == MouseButtons.Left)
				{
					if (IsCheckBoxHit(e.ColumnIndex, e.RowIndex, e.X, e.Y))
					{
						isPressed = true;
						DataGridView.InvalidateCell(this);
					}
				}

				base.OnMouseDown(e);
			} // proc OnMouseDown

			protected override void OnMouseUp(DataGridViewCellMouseEventArgs e)
			{
				if (isPressed)
				{
					if (DataGridView is PpsFilterEditor filterEditor && IsCheckBoxHit(e.ColumnIndex, e.RowIndex, e.X, e.Y))
						filterEditor.SetRowChecked(e.RowIndex, (ModifierKeys & Keys.Control) != 0);

					isPressed = false;
					DataGridView.InvalidateCell(this);
				}

				base.OnMouseUp(e);
			} // proc OnMouseUp

			public override Type FormattedValueType => typeof(string);
			public override Type ValueType { get => typeof(string); set { } }
		} // class FilterGroupSelectCell

		#endregion

		#region -- class FilterGroupSelectColumn --------------------------------------

		private sealed class FilterGroupSelectColumn : DataGridViewColumn
		{
			public FilterGroupSelectColumn()
				: base(new FilterGroupSelectCell())
			{
				Width = 21;
				ReadOnly = true;
			} // ctor
		} // class FilterGroupSelectColumn

		#endregion

		#region -- class FilterFieldCell ----------------------------------------------

		private sealed class FilterFieldCell : DataGridViewCell, IFilterToolTip
		{
			protected override object GetValue(int rowIndex)
				=> GetFilterColumn(DataGridView, rowIndex)?.ColumnName;

			protected override object GetFormattedValue(object value, int rowIndex, ref DataGridViewCellStyle cellStyle, TypeConverter valueTypeConverter, TypeConverter formattedValueTypeConverter, DataGridViewDataErrorContexts context)
				=> value?.ToString();

			public string GetToolTip(int rowIndex)
			{
				var col = GetFilterColumn(DataGridView, rowIndex);
				if (col == null)
					return null;

				return col.ColumnName + "\n" + col.ColumnSource;
			} // func GetToolTip

			protected override void Paint(Graphics g, Rectangle clipBounds, Rectangle cellBounds, int rowIndex, DataGridViewElementStates cellState, object value, object formattedValue, string errorText, DataGridViewCellStyle cellStyle, DataGridViewAdvancedBorderStyle advancedBorderStyle, DataGridViewPaintParts paintParts)
			{
				// paint border
				if ((paintParts & DataGridViewPaintParts.Border) != 0)
					PaintBorder(g, clipBounds, cellBounds, cellStyle, advancedBorderStyle);

				var borderRect = BorderWidths(advancedBorderStyle);
				var cellRect = cellBounds;
				cellRect.Offset(borderRect.X, borderRect.Y);
				cellRect.Width -= borderRect.Right;
				cellRect.Height -= borderRect.Bottom;

				// paint background
				g.FillRectangle(GetBackgroundBrush(cellState, cellStyle), cellRect);

				// draw text
				var isVisible = GetFilterColumn(DataGridView, rowIndex)?.IsVisible ?? true;
				cellRect.Inflate(-3, 0);
				using (var fmt = new StringFormat(StringFormatFlags.NoWrap) { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter })
				{
					if (isVisible)
						g.DrawString((string)formattedValue, cellStyle.Font, GetForegroundBrush(cellState, cellStyle), cellRect, fmt);
					else
					{
						using (var fnt = new Font(cellStyle.Font, FontStyle.Strikeout))
							g.DrawString((string)formattedValue, fnt, GetForegroundBrush(cellState, cellStyle), cellRect, fmt);
					}
				}
			} // proc Paint

			public override Type FormattedValueType => typeof(string);
			public override Type ValueType { get => typeof(string); set { } }
		} // class FilterFieldCell

		#endregion

		#region -- class FilterFieldColumn --------------------------------------------

		private sealed class FilterFieldColumn : DataGridViewColumn
		{
			public FilterFieldColumn()
				: base(new FilterFieldCell())
			{
				ReadOnly = true;
				Width = 140;
			} // ctor
		} // class FilterFieldColumn

		#endregion

		#region -- class FilterOperatorCell -------------------------------------------

		private sealed class FilterOperatorCell : DataGridViewComboBoxCell
		{
			protected override object GetValue(int rowIndex)
			{
				var col = GetFilterColumn(DataGridView, rowIndex);
				return col?.Operator ?? PpsDataFilterCompareOperator.Contains;
			} // func GetValue

			protected override bool SetValue(int rowIndex, object value)
			{
				var col = GetFilterColumn(DataGridView, rowIndex);
				if (col == null)
					return false;

				col.Operator = (PpsDataFilterCompareOperator)(value ?? PpsDataFilterCompareOperator.Contains);
				return true;
			} // func SetValue

			public override Type FormattedValueType => typeof(string);
			public override Type ValueType { get => typeof(PpsDataFilterCompareOperator); set { } }
		} // class FilterOperatorCell

		#endregion

		#region -- class FilterOperatorColumn -----------------------------------------

		private sealed class FilterOperatorColumn : DataGridViewComboBoxColumn
		{
			public FilterOperatorColumn()
			{
				CellTemplate = new FilterOperatorCell();

				DisplayMember = "Value";
				ValueMember = "Key";

				Width = 48;

				for (var i = 0; i < operatorItems.Length; i++)
					Items.Add(operatorItems[i]);

				ReadOnly = false;
			} // ctor
		} // class FilterOperatorColumn

		#endregion

		#region -- class FilterExpressionCell -----------------------------------------

		private sealed class FilterExpressionCell : DataGridViewTextBoxCell
		{
			protected override object GetValue(int rowIndex)
			{
				var col = GetFilterColumn(DataGridView, rowIndex);
				return col?.Value;
			} // func GetValue

			protected override bool SetValue(int rowIndex, object value)
			{
				var col = GetFilterColumn(DataGridView, rowIndex);
				return col?.TrySetValue((string)value) ?? false;
			} // func SetValue

			public override Type FormattedValueType => typeof(string);
			public override Type ValueType { get => typeof(string); set { } }
		} // class FilterExpressionCell

		#endregion

		#region -- class FilterExpressionColumn ---------------------------------------

		private sealed class FilterExpressionColumn : DataGridViewColumn
		{
			public FilterExpressionColumn()
				: base(new FilterExpressionCell())
			{
				AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
				ReadOnly = false;
			} // ctor
		} // class FilterExpressionColumn

		#endregion

		private static readonly Dictionary<int, Brush> brushes = new Dictionary<int, Brush>();
		private static readonly object useSelection = new object();

		private IPpsFilterExpression resultFilter = null;
		private int startRowSelection = -1;
		private int endRowSelection = -1;
		private IPpsFilterGroup highlightedGroup = null;

		private Rectangle dragStartRectangle = Rectangle.Empty;
		private object dragSourceInfo = null;

		private readonly ContextMenuStrip contextMenu;
		private readonly ToolStripMenuItem logicGroupAddMenuItem;
		private readonly ToolStripMenuItem logicGroupRemoveMenuItem;
		private readonly ToolStripMenuItem logicAndMenuItem;
		private readonly ToolStripMenuItem logicOrMenuItem;
		private readonly ToolStripMenuItem logicNotAndMenuItem; 
		private readonly ToolStripMenuItem logicNotOrMenuItem;

		#region -- Ctor/Dtor ----------------------------------------------------------

		public PpsFilterEditor()
		{
			SetStyle(ControlStyles.OptimizedDoubleBuffer, true);

			AllowUserToResizeColumns = false;

			SelectionMode = DataGridViewSelectionMode.CellSelect;
			ColumnHeadersVisible = false;
			RowHeadersVisible = false;
			MultiSelect = false;

			ReadOnly = false;
			VirtualMode = true;
			EditMode = DataGridViewEditMode.EditOnEnter;

			contextMenu = new ContextMenuStrip();
			contextMenu.Items.Add(logicGroupAddMenuItem = new ToolStripMenuItem("Logische Gruppe erstellen", null, contextMenu_LogicOperation) { ShortcutKeyDisplayString = "F9" });
			contextMenu.Items.Add(logicGroupRemoveMenuItem = new ToolStripMenuItem("Logische Gruppe entfernen", null, contextMenu_LogicOperation) { ShortcutKeyDisplayString = "F8" });
			contextMenu.Items.Add(new ToolStripSeparator());
			contextMenu.Items.Add(logicAndMenuItem = new ToolStripMenuItem("Logisches Und", null, contextMenu_LogicOperation));
			contextMenu.Items.Add(logicOrMenuItem = new ToolStripMenuItem("Logisches Oder", null, contextMenu_LogicOperation));
			contextMenu.Items.Add(logicNotAndMenuItem = new ToolStripMenuItem("Logisches Nicht Und", null, contextMenu_LogicOperation));
			contextMenu.Items.Add(logicNotOrMenuItem = new ToolStripMenuItem("Logisches Nicht Oder", null, contextMenu_LogicOperation));
			contextMenu.Items.Add(new ToolStripSeparator());
			contextMenu.Items.Add(new ToolStripMenuItem("Filter &Entfernen", null, contextMenu_RemoveItem) { ShortcutKeyDisplayString = "Entf" });
		} // ctor

		public void AddColumns()
		{
			Columns.Add(new FilterGroupColumn());
			Columns.Add(new FilterGroupSelectColumn());
			Columns.Add(new FilterFieldColumn());
			Columns.Add(new FilterOperatorColumn());
			Columns.Add(new FilterExpressionColumn());
		} // proc AddColumns

		protected override void Dispose(bool disposing)
		{
			if (disposing)
				contextMenu.Dispose();
			base.Dispose(disposing);
		} // proc Dispose

		#endregion

		#region -- SetFilter, RefreshFilter -------------------------------------------

		protected override void OnCellToolTipTextNeeded(DataGridViewCellToolTipTextNeededEventArgs e)
		{
			if (e.ColumnIndex >= 0 && e.ColumnIndex < ColumnCount
				&& e.RowIndex >= 0 && e.RowIndex < RowCount)
			{
				if (Rows[e.RowIndex].Cells[e.ColumnIndex] is IFilterToolTip filterToolTip)
					e.ToolTipText = filterToolTip.GetToolTip(e.RowIndex);
				else
					e.ToolTipText = null;
			}
			else
				e.ToolTipText = null;
		} // func OnCellToolTipTextNeeded

		public void SetFilter(IPpsFilterExpression newResultFilter)
		{
			if (resultFilter != null)
				resultFilter.FilterChanged -= ResultFilterChanged;

			resultFilter = newResultFilter;

			if (resultFilter != null)
				resultFilter.FilterChanged += ResultFilterChanged;

			RefreshFilterView();
		} // proc SetFilter

		private void ResultFilterChanged(object sender, EventArgs e)
			=> RefreshFilterView();

		private void RefreshFilterView()
		{
			if (resultFilter == null || resultFilter.Count == 0)
			{
				RowCount = 0;
				Columns[0].Width = groupColumnWidth;
			}
			else
			{
				RowCount = resultFilter.Count;

				// update first column
				var maxLevel = 1;
				for (var i = 0; i < resultFilter.Count; i++)
					maxLevel = Math.Max(resultFilter[i].Group.Level, maxLevel);
				Columns[0].Width = maxLevel * groupColumnWidth;
			}

			Invalidate();
		} // proc RefreshFilterView

		#endregion

		#region -- Filter Group Manager -----------------------------------------------

		private bool IsRowChecked(int rowIndex)
			=> rowIndex >= startRowSelection && rowIndex <= endRowSelection;

		private static bool GontainsGroup(IPpsFilterGroup g, IPpsFilterGroup t)
		{
			while (g != null)
			{
				if (g == t)
					return true;
				g = g.Group;
			}
			return false;
		} // func GontainsGroup

		private void SetGroupChecked(IPpsFilterGroup group)
		{
			if (group == null)
				return;

			var s = 0;
			var isChanged = false;
			for (var i = 0; i < resultFilter.Count; i++)
			{
				var containsGroup = GontainsGroup(resultFilter[i].Group, group);
				if (s == 0)// before group
				{
					if (containsGroup)
					{
						s = 1;
						if (startRowSelection != i)
						{
							startRowSelection = i;
							isChanged = true;
						}
					}
				}
				else if (s == 1) // in group
				{
					if (!containsGroup)
					{
						s = 2;
						if (endRowSelection != i - 1)
						{
							endRowSelection = i - 1;
							isChanged = true;
						}
					}
				}
				else // after group
					break;
			}

			if (s == 1)
			{
				var i = resultFilter.Count - 1;
				if (i != endRowSelection)
				{
					endRowSelection = i;
					isChanged = true;
				}
			}

			if (isChanged)
				InvalidateColumn(1);
			else
			{
				startRowSelection = -1;
				endRowSelection = -1;
				InvalidateColumn(1);
			}
		} // func SetGroupChecked

		private void SetRowChecked(int rowIndex, bool collapse)
		{
			var isChanged = false;

			if (collapse || startRowSelection < 0)
			{
				startRowSelection =
					endRowSelection = rowIndex;
				isChanged = true;
			}
			else if (rowIndex < startRowSelection)
			{
				if (startRowSelection != rowIndex)
				{
					startRowSelection = rowIndex;
					isChanged = true;
				}
			}
			else if (rowIndex > endRowSelection)
			{
				if (endRowSelection != rowIndex)
				{
					endRowSelection = rowIndex;
					isChanged = true;
				}
			}
			else if (rowIndex > startRowSelection && rowIndex < endRowSelection)
			{
				if (rowIndex - startRowSelection < endRowSelection - rowIndex)
					startRowSelection = rowIndex + 1;
				else
					endRowSelection = rowIndex - 1;
				isChanged = true;
			}
			else if (endRowSelection - startRowSelection >= 1) // toggle edges
			{
				if (rowIndex == startRowSelection)
					startRowSelection++;
				else
					endRowSelection--;
				isChanged = true;
			}
			else
			{
				startRowSelection = endRowSelection = -1;
				isChanged = true;
			}

			if (isChanged)
				InvalidateColumn(1);
		} // proc SetRowChecked

		private bool TryGetRowGroupSelection(out int startRow, out int endRow)
		{
			if (startRowSelection >= 0 && startRowSelection < resultFilter.Count)
			{
				startRow = startRowSelection;
				endRow = endRowSelection >= resultFilter.Count ? resultFilter.Count - 1 : endRowSelection;
				return true;
			}

			startRow = -1;
			endRow = -1;
			return false;
		} // func TryGetRowGroupSelection

		private IEnumerable<IPpsFilterColumn> GetRowGroupSelection()
		{
			if(TryGetRowGroupSelection(out var startRow, out var endRow))
			{
				for (var i = startRow; i <= endRow; i++)
					yield return resultFilter[i];
			}
		} // func GetRowGroupSelection

		private PpsDataFilterExpressionType SpinRowGroup(PpsDataFilterExpressionType type)
		{
			// just change the type of the group
			switch (type)
			{
				case PpsDataFilterExpressionType.And:
					return PpsDataFilterExpressionType.NAnd;
				case PpsDataFilterExpressionType.NAnd:
					return PpsDataFilterExpressionType.Or;
				case PpsDataFilterExpressionType.Or:
					return PpsDataFilterExpressionType.NOr;
				case PpsDataFilterExpressionType.NOr:
					return PpsDataFilterExpressionType.And;
				default:
					return PpsDataFilterExpressionType.And;
			}
		} // func SpinRowGroup

		private (bool isAllSameGroup, IPpsFilterGroup topGroup) IsSameRowGroup(int startRow, int endRow)
		{
			var g = resultFilter[startRow].Group;
			var t = g;
			var isEqual = true;

			for (var i = startRow + 1; i <= endRow; i++)
			{
				var c = resultFilter[i].Group;
				if (g != c)
				{
					isEqual = false;
					if (t.Level < c.Level)
						t = c;
				}
			}

			return (isEqual, t);
		} // func IsSameRowGroup

		private bool IsStartGroup(int startRow)
		{
			if (startRow == 0)
				return true;

			var p = resultFilter[startRow - 1].Group;
			var g = resultFilter[startRow].Group;

			return p != g && p.Level <= g.Level;
		} // func IsStartGroup

		private bool IsEndGroup(int endRow)
		{
			if (endRow == resultFilter.Count - 1)
				return true;

			var g = resultFilter[endRow].Group;
			var n = resultFilter[endRow + 1].Group;

			return g != n && n.Level <= g.Level;
		} // func IsEndGroup

		private void CreateRowGroup()
		{
			if (!TryGetRowGroupSelection(out var startRow, out var endRow))
				return;

			var (isAllSameGroup, topGroup) = IsSameRowGroup(startRow, endRow);

			IPpsFilterGroup updateGroup = null;

			if (isAllSameGroup && IsStartGroup(startRow) && IsEndGroup(endRow)) // it is a complete group selected
			{
				// just change the type of the group
				var g = resultFilter[startRow].Group;
				var nextType = SpinRowGroup(g.Type);
				if (g.Group != null && g.Group.Type == nextType)
					nextType = SpinRowGroup(nextType);
				g.Type = nextType;
			}
			else if (isAllSameGroup) // it is a part of a group selected
			{
				var nextType = PpsDataFilterExpressionType.And;

				if (startRow == endRow)
					nextType = PpsDataFilterExpressionType.NAnd;
				else
				{
					switch (resultFilter[startRow].Group.Type)
					{
						case PpsDataFilterExpressionType.And:
							nextType = PpsDataFilterExpressionType.Or;
							break;
						case PpsDataFilterExpressionType.Or:
							nextType = PpsDataFilterExpressionType.And;
							break;
						case PpsDataFilterExpressionType.NOr:
							nextType = PpsDataFilterExpressionType.NAnd;
							break;
						case PpsDataFilterExpressionType.NAnd:
							nextType = PpsDataFilterExpressionType.NOr;
							break;
					}
				}

				// create a new group for this area
				updateGroup = resultFilter.CreateFilterGroup(resultFilter[startRow].Group, nextType);
			}
			else
			{
				// this is a mixed group, unify to the top group
				updateGroup = topGroup;
			}

			UpdateRowGroup(startRow, endRow, updateGroup);
			InvalidateColumn(0);
		} // proc CreateRowGroup

		private void RemoveRowGroup()
		{
			if (!TryGetRowGroupSelection(out var startRow, out var endRow))
				return;

			var (isAllSameGroup, topGroup) = IsSameRowGroup(startRow, endRow);

			if (isAllSameGroup) // it is part/whole group selected
			{
				var toRemoveGroup = resultFilter[startRow].Group;

				// update group with the parent group
				if (toRemoveGroup.Group != null)
					UpdateRowGroup(startRow, endRow, toRemoveGroup.Group);

				// if there is a tail, recreate it. because there should never exist splitted groups
				var i = endRow + 1;
				var newGroup = resultFilter.CreateFilterGroup(toRemoveGroup.Group, toRemoveGroup.Type);
				while (i < resultFilter.Count)
				{
					if (toRemoveGroup == resultFilter[i].Group)
						resultFilter[i].Group = newGroup;
					else
						break;
					i++;
				}

				InvalidateRow(0);
			}
			else
			{
				// find top most level
				var maxLevel = topGroup.Level;

				// remove top most groups
				if (maxLevel >= 1)
				{
					for (var i = startRow; i < endRow; i++)
					{
						if (resultFilter[i].Group.Level == maxLevel)
							resultFilter[i].Group = resultFilter[i].Group.Group;
					}
				}

				InvalidateColumn(0);
			}
			InvalidateColumn(0);
		} // proc RemoveRowGroup

		private void UpdateRowGroup(int startRow, int endRow, IPpsFilterGroup updateGroup)
		{
			if (updateGroup != null)
			{
				for (var i = startRow; i <= endRow; i++)
					resultFilter[i].Group = updateGroup;

				InvalidateColumn(0);
			}
		} // proc UpdateRowGroup

		#endregion

		#region -- Insert -------------------------------------------------------------

		private int GetInsertPosition(int rowIndex, bool after, out IPpsFilterGroup group)
		{
			if (rowIndex < 0)
			{
				rowIndex = -1;
				group = resultFilter.Group;
				return rowIndex;
			}
			else if (rowIndex >= resultFilter.Count)
			{
				rowIndex = resultFilter.Count - 1;
				group = rowIndex >= 0 ? resultFilter[rowIndex].Group : resultFilter.Group;
				return rowIndex + 1;
			}
			else
			{
				group = resultFilter[rowIndex].Group;
				return after ? rowIndex + 1 : rowIndex;
			}
		} // func GetInsertPosition

		private int Insert(IReadOnlyList<IPpsFilterColumnFactory> filterFactories, int insertAt, IPpsFilterGroup group, bool moveItems)
		{
			foreach (var c in filterFactories)
			{
				if (moveItems && c is IPpsFilterColumn column)
				{
					var existsIndex = resultFilter.IndexOf(column);
					if (existsIndex >= 0)
					{
						if (resultFilter.Remove(existsIndex) && insertAt >= resultFilter.Count)
							insertAt--;
					}
				}
				resultFilter.Insert(insertAt++, c.CreateFilterColumn(resultFilter, group));
			}
			return insertAt;
		} // func Insert

		public void Insert(IReadOnlyList<IPpsFilterColumnFactory> columnFactories)
		{
			var atSelectionEnd = endRowSelection >= 0;
			var i = Insert(columnFactories, GetInsertPosition(atSelectionEnd ? endRowSelection : resultFilter.Count, true, out var group), group, false);
			if (atSelectionEnd)
			{
				endRowSelection = i - 1;
				SelectRow(i - 1);
				InvalidateColumn(1);
			}
		} // proc Insert

		private void SelectRow(int rowIndex)
		{
			CurrentCell = this[2, rowIndex];
		} // proc SelectRow

		#endregion

		private void ShowContextMenu(int columnIndex, int rowIndex, int x, int y, IPpsFilterGroup selectedGroup)
		{
			var rc = GetCellDisplayRectangle(columnIndex, rowIndex, false);

			// activate or deactivate menu items
			logicGroupAddMenuItem.Enabled = startRowSelection >= 0;
			logicGroupRemoveMenuItem.Enabled = startRowSelection >= 0;

			logicAndMenuItem.Enabled = selectedGroup != null && selectedGroup.Type != PpsDataFilterExpressionType.And && selectedGroup.Group.Type != PpsDataFilterExpressionType.And;
			logicOrMenuItem.Enabled = selectedGroup != null && selectedGroup.Type != PpsDataFilterExpressionType.Or && selectedGroup.Group.Type != PpsDataFilterExpressionType.Or;
			logicNotAndMenuItem.Enabled = selectedGroup != null && selectedGroup.Type != PpsDataFilterExpressionType.NAnd && selectedGroup.Group.Type != PpsDataFilterExpressionType.NAnd;
			logicNotOrMenuItem.Enabled = selectedGroup != null && selectedGroup.Type != PpsDataFilterExpressionType.NOr && selectedGroup.Group.Type != PpsDataFilterExpressionType.NOr;

			// open menu
			highlightedGroup = selectedGroup;
			if (highlightedGroup != null)
				InvalidateColumn(0);

			contextMenu.Tag = new object[] { rowIndex, selectedGroup };
			contextMenu.Show(this, rc.Left + x, rc.Top + y);
			contextMenu.Closed += ContextMenu_Closed;
		} // proc ShowContextMenu

		private void contextMenu_LogicOperation(object sender, EventArgs e)
		{
			if (sender == logicGroupAddMenuItem)
				CreateRowGroup();
			else if (sender == logicGroupRemoveMenuItem)
				RemoveRowGroup();
			else if (contextMenu.Tag is object[] args)
			{
				if (args[1] is IPpsFilterGroup group)
				{
					if (sender == logicAndMenuItem)
						group.Type = PpsDataFilterExpressionType.And;
					else if (sender == logicOrMenuItem)
						group.Type = PpsDataFilterExpressionType.Or;
					else if (sender == logicNotAndMenuItem)
						group.Type = PpsDataFilterExpressionType.NAnd;
					else if (sender == logicNotOrMenuItem)
						group.Type = PpsDataFilterExpressionType.NOr;
					InvalidateColumn(0);
				}
			}
		} // event contextMenu_LogicOperation

		private void contextMenu_RemoveItem(object sender, EventArgs e)
		{
			if (contextMenu.Tag is object[] args && args[0] is int rowIndex && rowIndex >= 0)
				resultFilter.Remove(rowIndex);
		} // event contextMenu_RemoveItem

		private void ContextMenu_Closed(object sender, ToolStripDropDownClosedEventArgs e)
		{
			highlightedGroup = null;
			InvalidateColumn(0);
		} // event ContextMenu_Closed

		protected override void OnKeyUp(KeyEventArgs e)
		{
			if (e.KeyCode == Keys.F8)
			{
				RemoveRowGroup();
				e.Handled = true;
			}
			else if (e.KeyCode == Keys.F9)
			{
				CreateRowGroup();
				e.Handled = true;
			}
			else if (e.KeyCode == Keys.Space)
			{
				if (CurrentCell != null)
					SetRowChecked(CurrentCell.RowIndex, (ModifierKeys & Keys.Control) != 0);
				e.Handled = true;
			}
			else if(e.KeyCode == Keys.Delete)
			{
				if (CurrentRow != null)
					resultFilter.Remove(CurrentRow.Index);
				e.Handled = true;
			}
			else
				base.OnKeyUp(e);
		} // proc OnKeyUp

		protected override void OnCellMouseDown(DataGridViewCellMouseEventArgs e)
		{
			if (e.RowIndex >= 0 && (e.Button & MouseButtons.Left) != 0)
			{
				if (e.ColumnIndex == 0) // gruppe verschieben!!!
				{
					dragSourceInfo = ((FilterGroupCell)this[e.ColumnIndex, e.RowIndex]).GetHoverGroup(e.RowIndex, e.X);
				}
				else if (e.ColumnIndex == 1) // ggf. markierung verschieben
				{
					if (startRowSelection >= 0)
						dragSourceInfo = useSelection;
					else
						dragSourceInfo = null;
				}
				if (e.ColumnIndex > 1) // einzelne Zeile verschieben
				{
					dragSourceInfo = e.RowIndex;
				}

				if (dragSourceInfo != null)
				{
					var dragSize = SystemInformation.DragSize;
					dragStartRectangle = new Rectangle(new Point(e.X - (dragSize.Width / 2), e.Y - (dragSize.Height / 2)), dragSize);
				}
				else
					dragStartRectangle = Rectangle.Empty;
			}
			else if (e.ColumnIndex > 0 && e.RowIndex >= 0 && (e.Button & MouseButtons.Right) != 0)
				ShowContextMenu(e.ColumnIndex, e.RowIndex, e.X, e.Y, null);

			base.OnCellMouseDown(e);
		} // proc OnCellMouseDown

		protected override void OnMouseMove(MouseEventArgs e)
		{
			if ((e.Button & MouseButtons.Left) != 0 && !dragStartRectangle.IsEmpty && !dragStartRectangle.Contains(e.Location))
			{
				dragStartRectangle = Rectangle.Empty;

				if (dragSourceInfo is int rowIndex)
				{
					var dragData = new DataObject("filterRow[]", new IPpsFilterColumn[] { resultFilter[rowIndex] });
					DoDragDrop(dragData, DragDropEffects.Move | DragDropEffects.Copy);
				}
				else if (dragSourceInfo == useSelection)
				{
					var dragData = new DataObject("filterRow[]", GetRowGroupSelection().ToArray());
					DoDragDrop(dragData, DragDropEffects.Move | DragDropEffects.Copy);
				}
				else if (dragSourceInfo is IPpsFilterGroup filterGroup)
				{
					var dragData = new DataObject("filterGroup", dragSourceInfo);
					highlightedGroup = filterGroup;
					InvalidateColumn(0);
					try
					{
						DoDragDrop(dragData, DragDropEffects.Move | DragDropEffects.Copy);
					}
					finally
					{
						highlightedGroup = null;
						InvalidateColumn(0);
					}
				}
			}
			else
				base.OnMouseMove(e);
		} // proc OnMouseMove

		private int TryGetHoverGroup(DragEventArgs e, out IPpsFilterGroup group)
		{
			// get current cell
			var pt = PointToClient(new Point(e.X, e.Y));
			var ht = HitTest(pt.X, pt.Y);

			// find group
			return GetInsertPosition(ht.RowIndex >= 0 ? ht.RowIndex : resultFilter.Count, false, out group);
		} // func TryGetHoverGroup

		protected override void OnDragEnter(DragEventArgs e)
		{
			if (TableInsertForm.TryGetFilterFactoriesInDragSource(e, out _))
				e.Effect = e.AllowedEffect & DragDropEffects.Copy;
			else if (e.Data.GetData("filterRow[]") is IPpsFilterColumn[]
				|| e.Data.GetData("filterGroup") is IPpsFilterGroup)
			{
				e.Effect = e.AllowedEffect & (((e.KeyState & 8) != 0) ? DragDropEffects.Copy : DragDropEffects.Move);
			}
		} // proc OnDragEnter

		protected override void OnDragOver(DragEventArgs e)
			=> OnDragEnter(e);

		protected override void OnDragDrop(DragEventArgs e)
		{
			if (TableInsertForm.TryGetFilterFactoriesInDragSource(e, out var filterFactories))
				Insert(filterFactories, TryGetHoverGroup(e, out var group), group, true);
			else
			{
				var insertAt = TryGetHoverGroup(e, out var group);

				if (e.Data.GetData("filterRow[]") is IPpsFilterColumn[] columns)
				{
					// prüfe die position;
					if (columns.Length == 1 && resultFilter.IndexOf(columns[0]) == insertAt
						|| resultFilter.IndexOf(columns[0]) >= insertAt && resultFilter.IndexOf(columns[columns.Length - 1]) <= insertAt)
						return;

					SelectRow(Insert(columns, insertAt, group, (e.Effect & DragDropEffects.Copy) == 0) - 1);
				}
				else if (e.Data.GetData("filterGroup") is IPpsFilterGroup filterGroup)
				{
					// todo:
					//if (insertAt == -1)
					//	;
				}
			}
		} // proc OnDragDrop

		// -- Static ----------------------------------------------------------

		private static readonly KeyValuePair<PpsDataFilterCompareOperator, string>[] operatorItems;

		static PpsFilterEditor()
		{
			operatorItems = new KeyValuePair<PpsDataFilterCompareOperator, string>[] {
				new KeyValuePair<PpsDataFilterCompareOperator, string>(PpsDataFilterCompareOperator.Contains, ""),
				new KeyValuePair<PpsDataFilterCompareOperator, string>(PpsDataFilterCompareOperator.NotContains, "!"),
				new KeyValuePair<PpsDataFilterCompareOperator, string>(PpsDataFilterCompareOperator.Equal, "="),
				new KeyValuePair<PpsDataFilterCompareOperator, string>(PpsDataFilterCompareOperator.NotEqual, "!="),
				new KeyValuePair<PpsDataFilterCompareOperator, string>(PpsDataFilterCompareOperator.Greater, ">"),
				new KeyValuePair<PpsDataFilterCompareOperator, string>(PpsDataFilterCompareOperator.GreaterOrEqual, ">="),
				new KeyValuePair<PpsDataFilterCompareOperator, string>(PpsDataFilterCompareOperator.Lower, "<"),
				new KeyValuePair<PpsDataFilterCompareOperator, string>(PpsDataFilterCompareOperator.LowerOrEqual, "<=")
			};
		} // sctor

		private static IPpsFilterColumn GetFilterColumn(DataGridView dataGridView, int rowIndex)
		{
			var self = (PpsFilterEditor)dataGridView;
			if (self == null)
				return null;

			return rowIndex >= 0 && rowIndex < self.resultFilter.Count ? self.resultFilter[rowIndex] : null;
		} // func GetFilterColumn

		private static Brush GetCachedBrush(Color color)
		{
			if (brushes.TryGetValue(color.ToArgb(), out var br))
				return br;
			else
			{
				br = new SolidBrush(color);
				brushes.Add(color.ToArgb(), br);
				return br;
			}
		} // func GetCachedBrush

		private static Brush GetBackgroundBrush(DataGridViewElementStates cellState, DataGridViewCellStyle cellStyle)
			=> GetCachedBrush((cellState & DataGridViewElementStates.Selected) != 0 ? cellStyle.SelectionBackColor : cellStyle.BackColor);

		private static Brush GetForegroundBrush(DataGridViewElementStates cellState, DataGridViewCellStyle cellStyle)
			=> GetCachedBrush((cellState & DataGridViewElementStates.Selected) != 0 ? cellStyle.SelectionForeColor : cellStyle.ForeColor);
	} // class PpsFilterEditor

	#endregion
}
