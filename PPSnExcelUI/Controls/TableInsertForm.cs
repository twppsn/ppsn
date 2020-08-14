﻿#region -- copyright --
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
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.WebSockets;
using System.Windows.Forms;
using TecWare.DE.Data;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;

namespace TecWare.PPSn.Controls
{
	internal partial class TableInsertForm : Form
	{
		#region -- class TreeNodeData -------------------------------------------------

		private abstract class TreeNodeData
		{
			private readonly TreeNodeData parent; // parent node
			private readonly List<JoinTreeNodeData> joins = new List<JoinTreeNodeData>(); // list of all sub joins
			private string alias = null; // encodes the alias name

			protected TreeNodeData(TreeNodeData parent)
			{
				this.parent = parent;
			} // ctor

			/// <summary>Update the tree node in the tree view.</summary>
			/// <param name="tn"></param>
			public virtual void Refresh(TreeNode tn)
			{
				tn.Text = NodeText;
				tn.ToolTipText = NodeToolTip;
				tn.Tag = this;
				tn.Checked = IsActive;

				if (IsActive)
				{
					UpdateTreeNodes(tn.Nodes, joins);
					if (!tn.IsExpanded && tn.Nodes.Count > 0)
						tn.Expand();
				}
				else
					tn.Nodes.Clear();
			} // proc Refresh

			protected abstract string GetDefaultAlias(string alias);

			/// <summary>Activate this node</summary>
			/// <param name="isNewActive"></param>
			public void SetActive(bool isNewActive, string alias = null)
			{
				if (isNewActive == IsActive)
					return;

				if (isNewActive) // build new joins
				{
					this.alias = GetDefaultAlias(alias);

					if (joins.Count == 0)
					{
						foreach (var c in View.Joins)
							joins.Add(new JoinTreeNodeData(this, c));
					}

				}
				else// clear all parents
				{
					this.alias = null;
					foreach (var c in joins)
						c.SetActive(false);
				}
			} // proc SetActive

			public JoinTreeNodeData ActivateJoin(PpsViewDefinition table, string alias, PpsDataJoinType joinType)
			{
				if (!IsActive)
					throw new ArgumentException("Current join is not active, parent should be activated first.");

				// find explicit join
				var joinToActivate = (JoinTreeNodeData)null;
				foreach (var j in joins)
				{
					if (j.JoinDefinition.View == table)
					{
						joinToActivate = j;
						if (j.JoinDefinition.Type == joinType && j.JoinDefinition.IsCompatibleAlias(alias))
							break;
					}
				}

				// activate join
				if (joinToActivate == null)
					return null;

				joinToActivate.SetActive(true, alias);
				return joinToActivate;
			} // func ActivateJoin

			public void GetJoinExpression(StringBuilder sb)
			{
				sb.Append(View.ViewId);
				if (!String.IsNullOrEmpty(alias))
					sb.Append(' ').Append(alias);

				foreach (var j in joins)
				{
					if (!j.IsActive)
						continue;

					sb.Append(PpsDataJoinStatement.ConvertJoinType(j.JoinDefinition.Type));

					var bracket = j.HasActiveJoins;
					if (bracket)
						sb.Append('(');

					j.GetJoinExpression(sb);

					if (bracket)
						sb.Append(')');
				}
			} // func GetJoinExpression

			/// <summary>Enumerate children</summary>
			/// <param name="recursive"></param>
			/// <returns></returns>
			public IEnumerable<JoinTreeNodeData> ActiveChildren()
			{
				var stack = new Stack<IEnumerator<JoinTreeNodeData>>();
				var currentJoins = ActiveJoins.GetEnumerator();
				while (true)
				{
					if (currentJoins.MoveNext())
					{
						var c = currentJoins.Current;
						stack.Push(currentJoins);
						currentJoins = c.ActiveJoins.GetEnumerator();
						yield return c;
					}
					else
					{
						currentJoins.Dispose();
						if (stack.Count > 0)
							currentJoins = stack.Pop();
						else
							yield break;
					}
				}
			} // func ActiveChildren

			public bool ContainsColumn(IDataColumn column)
				=> View.Columns.Contains(column);

			public string GetColumnName(IDataColumn column)
			{
				return String.IsNullOrEmpty(alias)
					? column.Name
					: alias + "." + column.Name;
			} // func GetColumnName

			private StringBuilder GetPath(StringBuilder sb)
			{
				if (parent == null)
					sb.Append(NodeText);
				else
				{
					sb.Append(NodeText)
						.Append("/");
					parent.GetPath(sb);
				}
				return sb;
			} // func GetPath

			public abstract string NodeText { get; }
			public virtual string NodeToolTip => null;

			public string Path => GetPath(new StringBuilder()).ToString();

			public IEnumerable<JoinTreeNodeData> ActiveJoins => from j in joins where j.IsActive select j;
			public bool HasActiveJoins => ActiveJoins.Any();

			/// <summary>Parent node</summary>
			public TreeNodeData Parent => parent;

			/// <summary>Find root table.</summary>
			public ViewTreeNodeData Root
			{
				get
				{
					var cur = this;
					while (cur.Parent != null)
						cur = cur.Parent;
					return cur as ViewTreeNodeData;
				}
			} // prop Root

			/// <summary>Attached view definition.</summary>
			public abstract PpsViewDefinition View { get; }

			/// <summary>Is this join active</summary>
			public bool IsActive => alias != null;
			/// <summary>Alias</summary>
			public string Alias => alias;
		} // class TreeNodeData

		#endregion

		#region -- class ViewTreeNodeData ---------------------------------------------

		private sealed class ViewTreeNodeData : TreeNodeData
		{
			private readonly PpsViewDefinition view;

			public ViewTreeNodeData(PpsViewDefinition view, string alias, bool isActive)
				: base(null)
			{
				this.view = view ?? throw new ArgumentNullException(nameof(view));

				SetActive(isActive, alias);
			} // ctor

			private static string PlainAlias(string alias, out int currentIndex)
			{
				if (String.IsNullOrEmpty(alias))
				{
					currentIndex = 0;
					return "t";
				}

				for (var i = 0; i < alias.Length; i++)
				{
					if (!Char.IsLetter(alias[i]))
					{
						if (i == 0)
						{
							currentIndex = 0;
							return "t";
						}
						else if (Int32.TryParse(alias.Substring(i), out currentIndex))
							return alias.Substring(0, i);
						else
						{
							currentIndex = 0;
							return alias.Substring(0, i);
						}
					}
				}

				currentIndex = 0;
				return alias;
			} // func PlainAlias

			private static void AddAliasIndex(TreeNodeData cur, string aliasPrefix, List<int> usedIndex)
			{
				var curPrefix = PlainAlias(cur.Alias, out var curIndex);
				if (String.Compare(curPrefix, aliasPrefix, StringComparison.OrdinalIgnoreCase) == 0)
					usedIndex.Add(curIndex);
			} // proc AddAliasIndex

			internal string GetUniqueAlias(string alias, TreeNodeData ignoreNode)
			{
				var usedIndex = new List<int>();

				// enforce alias
				var aliasPrefix = PlainAlias(alias, out var currentIndex);

				// collect all idx
				if (this != ignoreNode)
					AddAliasIndex(this, aliasPrefix, usedIndex);

				foreach (var cur in ActiveChildren())
				{
					if (cur == ignoreNode)
						continue;

					AddAliasIndex(cur, aliasPrefix, usedIndex);
				}

				if (currentIndex < 0)
					currentIndex = 0;

				while (usedIndex.Contains(currentIndex))
					currentIndex++;

				return currentIndex == 0 ? aliasPrefix : aliasPrefix + currentIndex.ToString();
			} // func GetUniqueAlias

			protected override string GetDefaultAlias(string alias)
				=> GetUniqueAlias(alias, this);

			/// <summary>DisplayText for the user</summary>
			public override string NodeText => view.DisplayName;
			/// <summary>Right view.</summary>
			public override PpsViewDefinition View => view;
		} // class ViewTreeNodeData

		#endregion

		#region -- class JoinTreeNodeData ---------------------------------------------

		private sealed class JoinTreeNodeData : TreeNodeData
		{
			private readonly PpsViewJoinDefinition join;

			public JoinTreeNodeData(TreeNodeData parent, PpsViewJoinDefinition join)
				: base(parent ?? throw new ArgumentNullException(nameof(parent)))
			{
				this.join = join ?? throw new ArgumentNullException(nameof(join));
			} // ctor

			protected override string GetDefaultAlias(string alias)
				=> Root.GetUniqueAlias(join.HasAlias ? join.Alias : alias, this);

			public override string NodeText => join.DisplayName;

			public override PpsViewDefinition View => join.View;
			public PpsViewJoinDefinition JoinDefinition => join;
		} // class JoinTreeNodeData

		#endregion

		#region -- class PpsJoinParser ------------------------------------------------

		private sealed class PpsJoinParser : PpsDataJoinExpression<PpsViewDefinition>
		{
			private readonly PpsViewDictionary views;

			public PpsJoinParser(PpsViewDictionary views, string expression)
			{
				this.views = views ?? throw new ArgumentNullException(nameof(views));

				Parse(expression);
			} // ctor

			protected override PpsDataJoinStatement[] CreateOnStatement(PpsTableExpression left, PpsDataJoinType joinOp, PpsTableExpression right)
				=> DefaultOnStatement; // is a to broad search, in this program we use a more strict algo.

			protected override PpsViewDefinition ResolveTable(string tableName)
				=> views[tableName];

			private PpsTableExpression FindLeftSiteTree(Stack<PpsJoinExpression> joins, PpsExpressionPart part)
			{
				while (part is PpsJoinExpression joinExpression)
				{
					joins.Push(joinExpression);
					part = joinExpression.Left;
				}

				return (PpsTableExpression)part;
			} // void FindLeftSiteTree

			private TreeNodeData ActivateJoins(TreeNodeData currentNode, PpsExpressionPart part, PpsDataJoinType joinType = PpsDataJoinType.Inner)
			{
				// find left outer table, this is our join-root
				var joinStack = new Stack<PpsJoinExpression>();
				var leftTable = FindLeftSiteTree(joinStack, part);

				// create or activate branch
				if (currentNode == null)
					currentNode = new ViewTreeNodeData(leftTable.Table, leftTable.Alias, true);
				else
				{
					currentNode = currentNode.ActivateJoin(leftTable.Table, leftTable.Alias, joinType);
					if (currentNode == null)
					{
						// todo: collect errors?
						return null; // skip whole branch
					}
				}

				// activate joins reverse
				while (joinStack.Count > 0)
				{
					var j = joinStack.Pop();

					if (j.Right is PpsTableExpression table) // simple activate on the same level
						currentNode.ActivateJoin(table.Table, table.Alias, j.Type);
					else
						ActivateJoins(currentNode, j.Right, j.Type); // sub branch, that will be activated seperate
				}

				return currentNode;
			} // proc ActivateJoins

			public ViewTreeNodeData Result => (ViewTreeNodeData)ActivateJoins(null, Root);

			private static PpsDataJoinStatement[] DefaultOnStatement { get; } = Array.Empty<PpsDataJoinStatement>();
		} // class PpsJoinParser

		#endregion

		#region -- class ColumnData ---------------------------------------------------

		private abstract class ColumnData : IPpsTableColumn, IEquatable<ColumnData>
		{
			private string displayName;
			private SortOrder sortOrder = SortOrder.None;

			public override string ToString()
				=> DisplayName;

			protected abstract string GetColumnExpression();

			public abstract bool Equals(ColumnData other);

			string IPpsTableColumn.Expression => GetColumnExpression();
			string IPpsTableColumn.Name => displayName;

			bool? IPpsTableColumn.Ascending
			{
				get
				{
					switch (sortOrder)
					{
						case SortOrder.Ascending:
							return true;
						case SortOrder.Descending:
							return false;
						default:
							return null;
					}
				}
			} // prop IPpsTableColumn.Ascending

			public abstract PpsTableColumnType Type { get; }
			public abstract string SourceName { get; }
			public abstract string SourcePath { get; }

			public string DisplayName { get => displayName; set => displayName = value; }
			public SortOrder ColumnSort { get => sortOrder; set => sortOrder = value; }
		} // class ColumnData

		#endregion

		#region -- class UserColumnData -----------------------------------------------

		private sealed class UserColumnData : ColumnData
		{
			private readonly PpsTableColumnType type;
			private readonly string name;
			private readonly string expression;

			public UserColumnData(PpsTableColumnType type, string name, string expression)
			{
				this.type = type;
				this.name = name;
				this.expression = expression;
			} // ctor

			public override bool Equals(ColumnData other)
				=> other is UserColumnData uc && IsEqualColumn(uc);

			public bool IsEqualColumn(UserColumnData other)
				=> type == other.type && expression == other.expression;

			protected override string GetColumnExpression()
				=> expression;

			public override PpsTableColumnType Type => type;
			public override string SourceName => name;
			public override string SourcePath => expression;
		} // class UserColumnData

		#endregion

		#region -- class SourceColumnData ---------------------------------------------

		private sealed class SourceColumnData : ColumnData, IPpsFilterColumnFactory
		{
			private readonly TreeNodeData columnSource;
			private readonly IDataColumn column;
			private readonly string sourceDisplayName;

			public SourceColumnData(TreeNodeData columnSource, IDataColumn column)
			{
				this.columnSource = columnSource ?? throw new ArgumentNullException(nameof(columnSource));
				this.column = column ?? throw new ArgumentNullException(nameof(column));

				sourceDisplayName = column.Attributes.GetProperty<string>("DisplayName", column.Name);
				DisplayName = sourceDisplayName;
			} // ctor

			public override bool Equals(ColumnData other)
				=> other is SourceColumnData sc && IsEqualColumn(sc);

			public bool IsEqualColumn(SourceColumnData other)
				=> column == other.column && columnSource == other.columnSource;

			protected override string GetColumnExpression() 
				=> Source.GetColumnName(column);

			IPpsFilterColumn IPpsFilterColumnFactory.CreateFilterColumn(IPpsFilterExpression filterExpression, IPpsFilterGroup group)
				=> new FilterColumn((FilterExpression)filterExpression, (FilterGroup)group, this, PpsDataFilterCompareOperator.Contains, PpsDataFilterCompareNullValue.Default);

			public override PpsTableColumnType Type => PpsTableColumnType.Data;
			public TreeNodeData Source => columnSource;
			public IDataColumn Column => column;
			public override string SourceName => sourceDisplayName;
			public override string SourcePath => columnSource.Path;
		} // class SourceColumnData

		#endregion

		#region -- class FilterColumn -------------------------------------------------

		private sealed class FilterColumn : IPpsFilterColumn
		{
			private readonly FilterExpression filterExpression;
			private readonly SourceColumnData columnSource;
			private FilterGroup group;
			private PpsDataFilterCompareOperator op;
			private PpsDataFilterCompareValue value;
			private bool isVisible = true;

			internal FilterColumn(FilterExpression filterExpression, FilterGroup group, SourceColumnData columnSource, PpsDataFilterCompareOperator op, PpsDataFilterCompareValue value)
			{
				this.filterExpression = filterExpression ?? throw new ArgumentNullException(nameof(filterExpression));
				this.group = group ?? throw new ArgumentNullException(nameof(group));
				this.columnSource = columnSource;
				this.op = op;
				this.value = value;
			} // ctor

			private string GetFormattedValue()
			{
				if (value == null)
					return null;
				else
				{
					var sb = new StringBuilder();
					value.ToString(sb);
					return sb.ToString();
				}
			} // func GetFormattedValue

			bool IPpsFilterColumn.TrySetValue(string text)
			{
				if (String.IsNullOrEmpty(text))
					value = PpsDataFilterCompareNullValue.Default;
				else
					value = PpsDataFilterExpression.ParseCompareValue(text);
				return true;

				//Source.Column.DataType
				//PpsDataFilterCompareValueType.
			} // func TrySetValue

			public PpsDataFilterExpression ToExpression()
				=> new PpsDataFilterCompareExpression(columnSource.Source.GetColumnName(columnSource.Column), op, value);

			IPpsFilterColumn IPpsFilterColumnFactory.CreateFilterColumn(IPpsFilterExpression filterExpression, IPpsFilterGroup group)
				=> new FilterColumn((FilterExpression)filterExpression, (FilterGroup)group, columnSource, op, value);

			internal SourceColumnData Source => columnSource;

			public string ColumnName => columnSource.SourceName;
			public string ColumnSource => columnSource.SourcePath;

			public FilterGroup Group
			{
				get => group;
				set
				{
					if (group != value)
					{
						group = value;
						filterExpression.OnFilterChanged();
					}
				}
			} // prop Group

			public bool IsEmpty => false;

			public bool IsVisible
			{
				get => isVisible;
				set => isVisible = value;
			} // prop IsVisible

			IPpsFilterGroup IPpsFilterColumn.Group { get => group; set => Group = (FilterGroup)value; }
			PpsDataFilterCompareOperator IPpsFilterColumn.Operator { get => op; set => op = value; }
			string IPpsFilterColumn.Value => GetFormattedValue();
		} // class FilterColumn

		#endregion

		#region -- class FilterGroup --------------------------------------------------

		private sealed class FilterGroup : IPpsFilterGroup
		{
			private readonly int groupLevel;
			private readonly FilterGroup group;
			private PpsDataFilterExpressionType type;

			public FilterGroup(FilterGroup group, PpsDataFilterExpressionType type)
			{
				this.group = group;
				this.groupLevel = group == null ? 0 : group.groupLevel + 1;
				this.type = type;
			} // ctor

			public override string ToString()
				=> type.ToString();

			public FilterGroup GetGroup(int level)
			{
				var b = groupLevel - level;
				if (b < 0)
					throw new ArgumentOutOfRangeException(nameof(level));

				var c = this;
				while (b > 0)
				{
					c = c.group;
					b--;
				}
				return c;
			} // func GetGroup

			public PpsDataFilterExpressionType Type { get => type; set => type = value; }
			public FilterGroup Group => group;
			public int Level => groupLevel;

			IPpsFilterGroup IPpsFilterGroup.Group => group;
		} // class FilterGroup

		#endregion

		#region -- class FilterExpression ---------------------------------------------

		private sealed class FilterExpression : IPpsFilterExpression
		{
			public event EventHandler FilterChanged;

			private readonly List<FilterColumn> filterColumns = new List<FilterColumn>();
			private readonly FilterGroup root = new FilterGroup(null, PpsDataFilterExpressionType.And);

			internal void OnFilterChanged()
				=> FilterChanged?.Invoke(this, EventArgs.Empty);

			#region -- Load/Compile ---------------------------------------------------

			private void LoadCore(FilterGroup currentGroup, VisibleColumnHelper visibleResultView, PpsDataFilterExpression expr)
			{
				switch (expr.Type)
				{
					case PpsDataFilterExpressionType.And:
					case PpsDataFilterExpressionType.NAnd:
					case PpsDataFilterExpressionType.Or:
					case PpsDataFilterExpressionType.NOr:
						var logicExpr = (PpsDataFilterLogicExpression)expr;
						var subGroup = currentGroup.Type == expr.Type ? root : new FilterGroup(currentGroup, logicExpr.Type);
						foreach (var cur in logicExpr.Arguments)
							LoadCore(subGroup, visibleResultView, cur);
						break;

					case PpsDataFilterExpressionType.Compare:
						var compareExpr = (PpsDataFilterCompareExpression)expr;
						var columnSource = visibleResultView.FindColumnSource(compareExpr.Operand);
						if (columnSource != null)
							filterColumns.Add(new FilterColumn(this, currentGroup, columnSource, compareExpr.Operator, compareExpr.Value));
						break;

					case PpsDataFilterExpressionType.Native: // ignore!
					case PpsDataFilterExpressionType.True: // ignore!
						break;
				}
			} // proc LoadCore

			public void Load(VisibleColumnHelper visibleResultView, PpsDataFilterExpression expr)
			{
				filterColumns.Clear();

				LoadCore(root, visibleResultView, expr);

				OnFilterChanged();
			} // proc Load

			public void Refresh(VisibleColumnHelper visibleColumnHelper)
			{
				// check filter columns, if they are still active
				for (var i = 0; i < filterColumns.Count; i++)
					filterColumns[i].IsVisible = visibleColumnHelper?.IsVisibleColumn(filterColumns[i].Source) ?? false;

				OnFilterChanged();
			} // proc OnFilterChanged

			private PpsDataFilterExpression CompileCore(VisibleColumnHelper visibleResultView, FilterGroup currentGroup, ref int offset)
			{
				var parts = new List<PpsDataFilterExpression>();

				while (offset < filterColumns.Count)
				{
					var group = filterColumns[offset].Group;
					if (group == currentGroup) // same group level
					{
						parts.Add(filterColumns[offset].ToExpression());
						offset++;
					}
					else if (group.Level > currentGroup.Level)  // sub group, compile sub group
					{
						parts.Add(CompileCore(visibleResultView, group.GetGroup(currentGroup.Level + 1), ref offset));
					}
					else  // new group same level, or lower group
					{
						break;
					}
				}
				return new PpsDataFilterLogicExpression(currentGroup.Type, parts.ToArray());
			} // func CompileCore

			public PpsDataFilterExpression Compile(VisibleColumnHelper visibleResultView)
			{
				var parts = new List<PpsDataFilterExpression>();
				var offset = 0;

				while (offset < filterColumns.Count)
					parts.Add(CompileCore(visibleResultView, root, ref offset));

				return parts.Count > 0
					? new PpsDataFilterLogicExpression(PpsDataFilterExpressionType.And, parts.ToArray()).Reduce()
					: PpsDataFilterExpression.True;
			} // func Compile

			#endregion

			public void Insert(int insertAt, IPpsFilterColumn column)
			{
				filterColumns.Insert(insertAt, (FilterColumn)column);
				OnFilterChanged();
			} // proc Insert

			public bool Remove(int index)
			{
				filterColumns.RemoveAt(index);
				OnFilterChanged();
				return true;
			} // func Remove

			public int IndexOf(IPpsFilterColumn column)
				=> filterColumns.IndexOf((FilterColumn)column);

			IPpsFilterGroup IPpsFilterExpression.CreateFilterGroup(IPpsFilterGroup parentGroup, PpsDataFilterExpressionType type)
				=> new FilterGroup((FilterGroup)parentGroup, type);

			public IEnumerator<IPpsFilterColumn> GetEnumerator()
				=> filterColumns.GetEnumerator();

			IEnumerator IEnumerable.GetEnumerator() 
				=> filterColumns.GetEnumerator();

			public int Count => filterColumns.Count;
			public IPpsFilterColumn this[int index] => filterColumns[index];

			public IPpsFilterGroup Group => root;
		} // class FilterExpression

		#endregion

		#region -- class VisibleColumnHelper ------------------------------------------

		private sealed class VisibleColumnHelper
		{
			private readonly ViewTreeNodeData resultView;
			private readonly JoinTreeNodeData[] activeTreeNodes;

			public VisibleColumnHelper(ViewTreeNodeData resultView)
			{
				this.resultView = resultView ?? throw new ArgumentNullException(nameof(resultView));
				activeTreeNodes = resultView.ActiveChildren().ToArray();
			} // ctor

			public bool IsVisibleColumn(ColumnData column)
			{
				if (column is SourceColumnData sourceColumn)
				{
					if (resultView == sourceColumn.Source)
						return true;

					foreach (var c in activeTreeNodes)
					{
						if (c == sourceColumn.Source)
							return true;
					}
					return false;
				}
				else
					return true; // other column types always visible
			} // func IsVisibleColumn

			public bool IsVisibleColumn2(IPpsFilterColumn filterColumn)
				=> IsVisibleColumn(((FilterColumn)filterColumn).Source);

			private bool TryFindColumn(TreeNodeData view, string exprAlias, string exprColumn, ref TreeNodeData resultColumnSource, ref IDataColumn resultColumn)
			{
				var r = false;
				if (!String.IsNullOrEmpty(exprAlias))
					r = String.Compare(view.Alias, exprAlias, StringComparison.OrdinalIgnoreCase) == 0;

				var currentResultColumn = view.View.Columns.FirstOrDefault(c => String.Compare(c.Name, exprColumn, StringComparison.OrdinalIgnoreCase) == 0);
				if (currentResultColumn != null)
				{
					resultColumnSource = view;
					resultColumn = currentResultColumn;
				}

				return r && currentResultColumn != null;
			} // func TryFindColumn

			public SourceColumnData FindColumnSource(string expression)
			{
				PpsDataColumnExpression.ParseQualifiedName(expression, out var exprAlias, out var exprColumn);

				var resultColumnSource = (TreeNodeData)null;
				var resultColumn = (IDataColumn)null;

				if (TryFindColumn(resultView, exprAlias, exprColumn, ref resultColumnSource, ref resultColumn))
					return new SourceColumnData(resultColumnSource, resultColumn);

				foreach (var c in activeTreeNodes)
				{
					if (TryFindColumn(c, exprAlias, exprColumn, ref resultColumnSource, ref resultColumn))
						return new SourceColumnData(resultColumnSource, resultColumn);
				}

				return resultColumnSource == null || resultColumn == null ? null : new SourceColumnData(resultColumnSource, resultColumn);
			} // func FindColumnSource
		} // class VisibleColumnHelper

		#endregion

		private const string dataObjectFormat = "PpsnColumnSource[]";
		private const string dataSourceFormat = "PpsnColumnSource";

		private readonly PpsEnvironment env;
		private readonly PpsViewDictionary availableViews; // list of all server tables
		private readonly List<ViewTreeNodeData> createdTreeNodeViews = new List<ViewTreeNodeData>();

		private IPpsTableData currentData = null;   // current selected mapping
		private ViewTreeNodeData resultView = null;   // current selected root table
		private readonly List<ColumnData> resultColumns = new List<ColumnData>(); // result column set
		private readonly FilterExpression resultFilter = new FilterExpression(); // filter expression

		private bool showInternalName = false;
		private int currentColumnsSortOrder = 1;

		#region -- Ctor/Dtor ----------------------------------------------------------

		public TableInsertForm(PpsEnvironment env)
		{
			this.env = env ?? throw new ArgumentNullException(nameof(env));

			InitializeComponent();

			filterGrid.AddColumns(); // the designer will generate code, if this is done before initialize component
			filterGrid.SetFilter(resultFilter);
			availableViews = new PpsViewDictionary(env);

			UpdateLayout();
		} // ctor

		private async Task RefreshAllAsync(IPpsTableData data)
		{
			// update data model
			await availableViews.RefreshAsync();

			// check current view
			var hasData = data != null && !data.IsEmpty;
			if (hasData)
			{
				// joins
				SetResultView(new PpsJoinParser(availableViews, data.Views).Result);

				// update columns
				var visibleResultView = new VisibleColumnHelper(resultView);
				if (data.Columns != null)
				{
					foreach (var col in data.Columns)
					{
						ColumnData columnData;
						if (col.Type == PpsTableColumnType.Data)
							columnData = visibleResultView.FindColumnSource(col.Expression);
						else
							columnData = new UserColumnData(col.Type, col.Name, col.Expression);

						if (columnData != null)
						{
							columnData.ColumnSort = col.Ascending.HasValue ? (col.Ascending.Value ? SortOrder.Ascending : SortOrder.Descending) : SortOrder.None;
							columnData.DisplayName = col.Name;

							resultColumns.Add(columnData);
						}
					}
				}

				// update filter
				var expr = PpsDataFilterExpression.Parse(data.Filter);
				resultFilter.Load(visibleResultView, expr);
			}

			// update view
			UpdateTreeView();
			RefreshResultColumns();
			UpdateLayout();

			// update selection
			if (hasData && tableTree.SelectedNode == null && tableTree.Nodes.Count > 0)
				tableTree.SelectedNode = tableTree.Nodes[0];
		} // func RefreshAllAsync

		public void LoadData(IPpsTableData tableData)
		{
			Text = tableData.IsEmpty
				? "Neue Tabelle einfügen"
				: String.Format("{0} bearbeiten", tableData.DisplayName);
			cmdRefresh.Text = tableData.IsEmpty
				? "Einfügen"
				: "Aktualisieren";

			// update view
			currentData = tableData;
			env.ContinueCatch(RefreshAllAsync(currentData));
		} // proc LoadData

		private void UpdateLayout()
		{
			var defaultCap = resultColumnsListView.Left - tableTree.Right;
			var clientArea = new Rectangle(tableTree.Left, tableTree.Top, ClientSize.Width - tableTree.Left, ClientSize.Height - cmdClose.Height - tableTree.Top * 2 - defaultCap);

			// position of tabletree, do not touch top/left and width
			if (IsTableSelectMode)
			{
				var h = tableTree.Top * 8;
				tableTree.SetBounds(clientArea.Left, clientArea.Top, tableTree.Width, h);
				var t = h + defaultCap;
				currentColumnsListView.SetBounds(clientArea.Left, clientArea.Top + t, tableTree.Width, clientArea.Height - t);

				var scrollSize = SystemInformation.VerticalScrollBarWidth + SystemInformation.FrameBorderSize.Width;
				if (showInternalName)
				{
					var techWidth = tableTree.Top * 6;
					columnListHeader.Width = currentColumnsListView.Width - techWidth - scrollSize;
					columnListTechHeader.Width = techWidth;
				}
				else
				{
					columnListHeader.Width = currentColumnsListView.Width - scrollSize;
					columnListTechHeader.Width = 0;
				}
				currentColumnsListView.Visible = true;
			}
			else
			{
				tableTree.SetBounds(clientArea.Left, clientArea.Top, tableTree.Width, clientArea.Height);
				currentColumnsListView.Visible = false;
			}

			// set result, do not touch left and width
			resultColumnsListView.SetBounds(resultColumnsListView.Left, clientArea.Top, resultColumnsListView.Width, clientArea.Height);
		} // proc UpdateLayout

		#endregion

		#region -- Tree View Management -----------------------------------------------

		private static int BinarySearch(Func<int, string> g, int offset, int length, string text, bool ascending)
		{
			var startAt = offset;
			var endAt = offset + length - 1;

			while (startAt <= endAt)
			{
				var middle = startAt + ((endAt - startAt) >> 1);
				var t = String.Compare(g(middle), text, StringComparison.OrdinalIgnoreCase);
				if (t == 0)
					return middle;
				else if (ascending && t < 0 || !ascending && t > 0)
					startAt = middle + 1;
				else
					endAt = middle - 1;
			}

			return ~startAt;
		} // proc BinaryNodeSearch

		private static void UpsertTreeNode(TreeNodeCollection nodes, TreeNodeData data)
		{
			var pos = BinarySearch(i => nodes[i].Text, 0, nodes.Count, data.NodeText, true);
			if (pos < 0)
			{
				pos = ~pos;
				nodes.Insert(pos, data.NodeText);
			}

			data.Refresh(nodes[pos]);
		} // proc UpsertTreeNode

		private static void UpdateTreeNodes(TreeNodeCollection nodes, IEnumerable<TreeNodeData> children)
		{
			foreach (var tn in nodes)
				((TreeNode)tn).Tag = null;

			// update current elements
			foreach (var c in children)
				UpsertTreeNode(nodes, c);

			// remove outdated elements
			for (var i = nodes.Count - 1; i >= 0; i--)
			{
				var tn = nodes[i];
				if (tn.Tag == null)
					tn.Remove();
			}
		} // proc UpdateTreeNodes

		private int GetTreeNodeViewCacheIndex(PpsViewDefinition view)
			=> createdTreeNodeViews.FindIndex(c => c.View == view);

		private void SetResultView(ViewTreeNodeData newView)
		{
			resultView = newView;
			if (newView != null && !createdTreeNodeViews.Contains(newView))
				createdTreeNodeViews.Add(newView);
		} // proc SetResultView

		private ViewTreeNodeData GetTreeNodeView(PpsViewDefinition view)
		{
			var cacheIndex = GetTreeNodeViewCacheIndex(view);
			if (cacheIndex >= 0)
				return createdTreeNodeViews[cacheIndex];
			else
				return new ViewTreeNodeData(view, null, false);
		} // func GetTreeNodeView

		private void UpdateTreeView()
		{
			if (IsTableSelectMode) // table selected
			{
				// remove all to one
				var d = tableTree.Nodes.Count - 1;
				while (tableTree.Nodes.Count > 1)
				{
					if (tableTree.Nodes[d].Tag == resultView)
						d--;
					tableTree.Nodes.RemoveAt(d);
					d--;
				}
				if (tableTree.Nodes.Count == 0)
					tableTree.Nodes.Add("Node");

				resultView.Refresh(tableTree.Nodes[0]);
			}
			else // show full filtered list
				UpdateTreeNodes(tableTree.Nodes, availableViews.Select(c => GetTreeNodeView(c.Value)));
		} // proc UpdateTreeView

		private void tableTree_AfterCheck(object sender, TreeViewEventArgs e)
		{
			if (e.Action == TreeViewAction.Unknown)
				return;

			if (e.Node.Tag is ViewTreeNodeData view) // root node
			{
				if (e.Node.Checked)
				{
					view.SetActive(true);
					SetResultView(view);
					UpdateTreeView();
					UpdateLayout();
				}
				else
				{
					SetResultView(null);
					view.SetActive(false);
					UpdateTreeView();
					UpdateLayout();
				}
				RefreshResultColumns();
				tableTree.SelectedNode = e.Node;
				RefreshAvailableColumns(view);
			}
			else if (e.Node.Tag is JoinTreeNodeData join) // child node
			{
				join.SetActive(e.Node.Checked);
				join.Refresh(e.Node);
				RefreshResultColumns();
				tableTree.SelectedNode = e.Node;
				RefreshAvailableColumns(join);
			}
		} // event tableTree_AfterCheck

		private bool IsTableSelectMode => resultView != null;

		#endregion

		#region -- ColumnSelection ----------------------------------------------------

		private void tableTree_AfterSelect(object sender, TreeViewEventArgs e)
		{
			if (e.Node.Tag is TreeNodeData nodeData)
				RefreshAvailableColumns(nodeData);
		} // event tableTree_AfterSelect

		private void RefreshAvailableColumns()
			=> RefreshAvailableColumns(tableTree.SelectedNode?.Tag is TreeNodeData nodeData ? nodeData : null);

		private void RefreshAvailableColumns(TreeNodeData nodeData)
		{
			currentColumnsListView.BeginUpdate();
			try
			{
				if (nodeData == null || !nodeData.IsActive || !IsTableSelectMode)
				{
					currentColumnsListView.Items.Clear();
				}
				else
				{
					// remove columns
					for (var i = currentColumnsListView.Items.Count - 1; i >= 0; i--)
					{
						if (currentColumnsListView.Items[i].Tag is SourceColumnData columnSource && columnSource.Source != nodeData)
							currentColumnsListView.Items.RemoveAt(i);
					}

					// add new columns
					var sortField = currentColumnsSortOrder == 2 || currentColumnsSortOrder == -2 ? 1 : 0;
					foreach (var col in nodeData.View.Columns)
					{
						var newCol = new SourceColumnData(nodeData, col);
						var idx = BinarySearch(
							i => currentColumnsListView.Items[i].SubItems[sortField].Text,
							0, currentColumnsListView.Items.Count,
							sortField == 1 ? newCol.Column.Name : newCol.DisplayName,
							currentColumnsSortOrder > 0
						);

						ListViewItem lvi;
						if (idx < 0)
						{
							lvi = new ListViewItem() { Tag = newCol };
							lvi.SubItems.Add("");
							currentColumnsListView.Items.Insert(~idx, lvi);
						}
						else
							lvi = currentColumnsListView.Items[idx];

						lvi.Text = newCol.DisplayName;
						lvi.SubItems[1].Text = newCol.Column.Name;
					}
				}
			}
			finally
			{
				currentColumnsListView.EndUpdate();
			}
		} // proc RefreshAvailableColumns

		private IEnumerable<ColumnData> GetSelectedColumns(ListView listView)
			=> from lvi in listView.SelectedItems.Cast<ListViewItem>() select (ColumnData)lvi.Tag;

		private void MoveColumnsToResult(bool selectNext)
		{
			var selectedColumns = GetSelectedColumns(currentColumnsListView).ToArray();
			if (selectedColumns.Length == 0)
				return;

			MoveColumnsToResult(selectedColumns);
			var lastSelectedIndex = selectNext && currentColumnsListView.SelectedItems.Count > 0
				? currentColumnsListView.SelectedItems[currentColumnsListView.SelectedItems.Count - 1].Index
				: -1;

			lastSelectedIndex++;
			currentColumnsListView.SelectedItems.Clear();
			if (lastSelectedIndex >= 0 && lastSelectedIndex < currentColumnsListView.Items.Count)
			{
				var lvi = currentColumnsListView.Items[lastSelectedIndex];
				lvi.Selected = true;
				currentColumnsListView.FocusedItem = lvi;
			}
		} // proc MoveColumnsToResult

		private void MoveColumnsToResult(IEnumerable<ColumnData> columnSource, int insertAt = -1)
		{
			if (insertAt < 0)
				insertAt = resultColumns.Count;

			// clear selection of target
			resultColumnsListView.SelectedItems.Clear();

			// add new items in target
			var toSelect = new List<ColumnData>();
			foreach (var col in columnSource)
			{
				var existsIndex = resultColumns.FindIndex(col.Equals);
				if (existsIndex >= 0)
				{
					resultColumns.RemoveAt(existsIndex);
					if (existsIndex < insertAt)
						insertAt--;
				}

				resultColumns.Insert(insertAt++, col);
				toSelect.Add(col);
			}

			RefreshResultColumns(toSelect.ToArray());
		} // proc MoveColumnsToResult

		private void RemoveColumnsFromResult(bool selectColumn)
			=> RemoveColumnsFromResult(resultColumnsListView.SelectedItems.Cast<ListViewItem>().Select(lvi => (ColumnData)lvi.Tag), selectColumn);

		private void RemoveColumnsFromResult(IEnumerable<ColumnData> columnSource, bool selectColumn)
		{
			var maxIndex = -1;

			foreach (var column in columnSource)
			{
				var selectedIndex = resultColumns.FindIndex(column.Equals);
				if (selectedIndex >= 0)
					resultColumns.RemoveAt(selectedIndex);

				maxIndex = Math.Max(selectedIndex, maxIndex);
				if (maxIndex <= selectedIndex)
					maxIndex--;
			}

			var toSelect = (ColumnData[])null;
			if (selectColumn)
			{
				maxIndex++;
				if (maxIndex < resultColumns.Count)
					toSelect = new ColumnData[] { resultColumns[maxIndex] };
				else if (resultColumns.Count > 0)
					toSelect = new ColumnData[] { resultColumns[resultColumns.Count - 1] };
			}

			RefreshResultColumns(toSelect);
		} // proc RemoveColumnsFromResult

		private void SetColumnResultSortOrder(SortOrder sort)
		{
			foreach (var lvi in resultColumnsListView.SelectedItems.Cast<ListViewItem>())
			{
				var column = (ColumnData)lvi.Tag;
				column.ColumnSort = sort;
			}

			RefreshResultColumns();
		} // proc SetColumnResultSortOrder

		private void SelectAll(ListView list)
		{
			list.BeginUpdate();
			try
			{
				for (var i = 0; i < list.Items.Count; i++)
					list.Items[i].Selected = true;
			}
			finally
			{
				list.EndUpdate();
			}
		} // proc SelectAll

		private void SelectInverse(ListView list)
		{
			list.BeginUpdate();
			try
			{
				for (var i = 0; i < list.Items.Count; i++)
					list.Items[i].Selected = !list.Items[i].Selected;
			}
			finally
			{
				list.EndUpdate();
			}
		} // proc SelectInverse

		private static void UpdateResultColumnListViewItem(ColumnData currentColumn, ListViewItem currentLvi)
		{
			var columnSourcePath = currentColumn.SourcePath;

			currentLvi.Text = currentColumn.DisplayName;
			currentLvi.ToolTipText = currentColumn.SourceName + "\n" + columnSourcePath;

			if (currentLvi.SubItems.Count == 1)
				currentLvi.SubItems.Add(String.Empty);
			currentLvi.SubItems[1].Text = columnSourcePath;

			switch (currentColumn.ColumnSort)
			{
				case SortOrder.Ascending:
					currentLvi.ImageIndex = 0;
					break;
				case SortOrder.Descending:
					currentLvi.ImageIndex = 1;
					break;
				default:
					currentLvi.ImageIndex = -1;
					break;
			}
		} // proc UpdateResultColumnListViewItem

		private ListViewItem FindResultColumnListViewItem(ColumnData column, int offset)
		{
			for (var i = offset; i < resultColumnsListView.Items.Count; i++)
			{
				if (resultColumnsListView.Items[i].Tag is ColumnData other && column.Equals(other))
					return resultColumnsListView.Items[i];
			}
			return null;
		} // func FindResultColumnListViewItem

		private void RefreshResultColumns(ColumnData[] toSelect = null)
		{
			if (IsTableSelectMode)
			{
				// get all treenodes
				var visibleColumnHelper = new VisibleColumnHelper(resultView);

				// update filter
				resultFilter.Refresh(visibleColumnHelper);

				// update view
				resultColumnsListView.BeginUpdate();
				try
				{
					// update items
					var sourceIndex = 0;
					var targetIndex = 0;

					while (sourceIndex < resultColumns.Count)
					{
						// get source
						var currentColumn = resultColumns[sourceIndex++];
						if (!visibleColumnHelper.IsVisibleColumn(currentColumn))
							continue;

						// get target
						var currentLvi = FindResultColumnListViewItem(currentColumn, targetIndex);
						if (currentLvi == null) // not in list -> insert new 
						{
							currentLvi = new ListViewItem { Tag = currentColumn };
							UpdateResultColumnListViewItem(currentColumn, currentLvi);
							resultColumnsListView.Items.Insert(targetIndex, currentLvi);
						}
						else if (currentLvi.Index == targetIndex) // same position -> update
						{
							UpdateResultColumnListViewItem(currentColumn, currentLvi);
						}
						else // other position -> move
						{
							currentLvi.Remove();
							UpdateResultColumnListViewItem(currentColumn, currentLvi);
							resultColumnsListView.Items.Insert(targetIndex, currentLvi);
						}

						// update selection
						if (toSelect != null)
							currentLvi.Selected = Array.Exists(toSelect, currentColumn.Equals);

						targetIndex++;
					}

					// remove unused items
					while (resultColumnsListView.Items.Count > targetIndex)
						resultColumnsListView.Items.RemoveAt(resultColumnsListView.Items.Count - 1);

				}
				finally
				{
					resultColumnsListView.EndUpdate();
				}
			}
			else
			{
				resultColumnsListView.Items.Clear();
				resultFilter.Refresh(null);
			}
		} // proc RefreshAvailableColumns

		#endregion

		#region -- ListView UI --------------------------------------------------------

		private Rectangle dragStartRectangle = Rectangle.Empty;

		private static bool TryGetDataColumnsInDragSource(DragEventArgs e, out IReadOnlyList<ColumnData> columns)
		{
			columns = e.Data.GetData(dataObjectFormat) as ColumnData[];
			return columns != null && columns.Count > 0;
		} // func TryGetDataColumnsInDragSource

		private static bool IsDataColumnsSourceInDragSource(DragEventArgs e, string expectedSource)
			=> Equals(e.Data.GetData(dataSourceFormat), expectedSource);

		public static bool TryGetFilterFactoriesInDragSource(DragEventArgs e, out IReadOnlyList<IPpsFilterColumnFactory> filterFactories)
		{
			filterFactories = (e.Data.GetData(dataObjectFormat) as ColumnData[])?.OfType<IPpsFilterColumnFactory>().ToArray();
			return filterFactories != null && filterFactories.Count > 0;
		} // func TryGetFilterFactoriesInDragSource

		private ListViewItem GetListViewHoverItem(ListView listView, DragEventArgs e, out bool insertAfter)
		{
			if (listView.Items.Count == 0)
			{
				insertAfter = false;
				return null;
			}
			else
			{
				var pt = listView.PointToClient(new Point(e.X, e.Y));
				var firstItemBounds = listView.GetItemRect(0, ItemBoundsPortion.Entire);
				var hoverItem = listView.GetItemAt(firstItemBounds.Left, pt.Y);
				if (hoverItem == null)
				{
					insertAfter = pt.Y > firstItemBounds.Top;
					return null;
				}
				else
				{
					var hoverItemBounds = hoverItem.GetBounds(ItemBoundsPortion.Entire);
					var middle = hoverItemBounds.Top + hoverItemBounds.Height / 2;
					insertAfter = middle < pt.Y;
					return hoverItem;
				}
			}
		} // func GetListViewHoverItem

		private void currentContextMenuStrip_Opening(object sender, CancelEventArgs e)
		{
			var hasItems = currentColumnsListView.Items.Count > 0;
			var hasSelectedItems = currentColumnsListView.SelectedItems.Count > 0;

			currentColumnsSelectAllMenuItem.Enabled = hasItems;
			currentColumnsSelectInverseMenuItem.Enabled = hasItems;
			currentColumnAddToCondition.Enabled = hasSelectedItems;
			currentColumnAddToResultMenuItem.Enabled = hasSelectedItems;
		} // event currentContextMenuStrip_Opening

		private void resultColumnsContextMenuStrip_Opening(object sender, CancelEventArgs e)
		{
			var hasItems = resultColumnsListView.Items.Count > 0;
			var hasSelectedItems = resultColumnsListView.SelectedItems.Count > 0;

			resultColumnsSelectAllMenuItem.Enabled = hasItems;
			resultColumnsSelectInverseMenuItem.Enabled = hasItems;
			resultColumnAddToCondition.Enabled = hasSelectedItems;
			resultColumnRemoveMenuItem.Enabled = hasSelectedItems;
			resultColumnRenameMenuItem.Enabled = resultColumnsListView.FocusedItem != null;
			resultColumnSortAscMenuItem.Enabled = hasSelectedItems;
			resultColumnSortDescMenuItem.Enabled = hasSelectedItems;
			resultColumnSortNoneMenuItem.Enabled = hasSelectedItems;
		} // event resultColumnsContextMenuStrip_Opening

		private void listView_MouseDown(object sender, MouseEventArgs e)
		{
			if (sender is ListView listView && (e.Button & MouseButtons.Left) != 0)
			{
				var dragSize = SystemInformation.DragSize;
				if (listView.SelectedItems.Count > 0)
					dragStartRectangle = new Rectangle(new Point(e.X - (dragSize.Width / 2), e.Y - (dragSize.Height / 2)), dragSize);
			}
		} // event listView_MouseDown

		private void listView_MouseMove(object sender, MouseEventArgs e)
		{
			if (sender is ListView listView && (e.Button & MouseButtons.Left) != 0 && !dragStartRectangle.IsEmpty && !dragStartRectangle.Contains(e.X, e.Y))
			{
				dragStartRectangle = Rectangle.Empty;

				// setze die spalten
				var dragData = new DataObject(dataObjectFormat,
					(
						from lvi in listView.SelectedItems.Cast<ListViewItem>()
						where lvi.Tag is ColumnData
						select (ColumnData)lvi.Tag
					).ToArray()
				);

				// setze die quelle der columns
				if (sender == resultColumnsListView)
					dragData.SetData(dataSourceFormat, "result");
				else if (sender == currentColumnsListView)
					dragData.SetData(dataSourceFormat, "current");

				// drag&drop starten
				if (DoDragDrop(dragData, DragDropEffects.All) != DragDropEffects.None)
					listView.SelectedItems.Clear(); // bei Erfolg Markierung entfernen
			}
		} // event listView_MouseMove

		private void currentColumnsListView_DragEnter(object sender, DragEventArgs e)
		{
			if (TryGetDataColumnsInDragSource(e, out _) && IsDataColumnsSourceInDragSource(e, "result"))
				e.Effect = e.AllowedEffect & DragDropEffects.Move;
		} // event currentColumnsListView_DragEnter

		private void currentColumnsListView_DragDrop(object sender, DragEventArgs e)
		{
			if (TryGetDataColumnsInDragSource(e, out var columns) && IsDataColumnsSourceInDragSource(e, "result"))
				RemoveColumnsFromResult(columns, false);
		} // event currentColumnsListView_DragDrop

		private void resultColumnsListView_DragEnter(object sender, DragEventArgs e)
		{
			if (TryGetDataColumnsInDragSource(e, out _))
				e.Effect = e.AllowedEffect & DragDropEffects.Move;
		} // event resultColumnsListView_DragEnter

		private void resultColumnsListView_DragDrop(object sender, DragEventArgs e)
		{
			if (TryGetDataColumnsInDragSource(e, out var columns))
			{
				// hover item ermitteln
				var hoverItem = GetListViewHoverItem(resultColumnsListView, e, out var insertAfter);
				
				if (hoverItem == null) // append at end
					MoveColumnsToResult(columns, insertAfter ? -1 : 0);
				else if (hoverItem.Tag is ColumnData columnSource)
				{
					// get index to insert
					var insertAt = resultColumns.FindIndex(columnSource.Equals);
					if (insertAfter)
						insertAt++;

					// insert items
					MoveColumnsToResult(columns, insertAt);
				}
			}
		} // event resultColumnsListView_DragDrop

		private void currentColumnsListView_KeyUp(object sender, KeyEventArgs e)
		{
			switch (e.KeyData)
			{
				case Keys.Control | Keys.A:
					SelectAll(currentColumnsListView);
					e.Handled = true;
					break;
				case Keys.Insert:
					MoveColumnsToResult(true);
					break;
				case Keys.F4:
					showInternalName = !showInternalName;
					UpdateLayout();
					break;
			}
		} // event currentColumnsListView_KeyUp

		private void currentColumnsListView_ColumnClick(object sender, ColumnClickEventArgs e)
		{
			var sortColumnIndex = e.Column + 1;
			if (currentColumnsSortOrder == sortColumnIndex)
				currentColumnsSortOrder = -sortColumnIndex;
			else
				currentColumnsSortOrder = sortColumnIndex;
			RefreshAvailableColumns();
		} // event currentColumnsListView_ColumnClick

		private void resultColumnsListView_KeyUp(object sender, KeyEventArgs e)
		{
			switch (e.KeyData)
			{
				case Keys.Control | Keys.A:
					SelectAll(resultColumnsListView);
					e.Handled = true;
					break;
				case Keys.Delete:
					RemoveColumnsFromResult(true);
					break;
				case Keys.F2:
					resultColumnsListView.FocusedItem?.BeginEdit();
					break;
			}
		} // event resultColumnsListView_KeyUp

		#endregion

		private void CommandExec(object sender, EventArgs e)
		{
			if (sender == currentColumnAddToResultMenuItem)
				MoveColumnsToResult(false);
			else if (sender == currentColumnAddToCondition)
				filterGrid.Insert(GetSelectedColumns(currentColumnsListView).OfType<IPpsFilterColumnFactory>().ToArray());
			else if (sender == resultColumnRemoveMenuItem)
				RemoveColumnsFromResult(false);
			else if (sender == resultColumnRenameMenuItem)
				resultColumnsListView.FocusedItem?.BeginEdit();
			else if (sender == resultColumnAddToCondition)
				filterGrid.Insert(GetSelectedColumns(resultColumnsListView).OfType<IPpsFilterColumnFactory>().ToArray());
			// sort
			else if (sender == resultColumnSortAscMenuItem)
				SetColumnResultSortOrder(SortOrder.Ascending);
			else if (sender == resultColumnSortDescMenuItem)
				SetColumnResultSortOrder(SortOrder.Descending);
			else if (sender == resultColumnSortNoneMenuItem)
				SetColumnResultSortOrder(SortOrder.None);
			// selection
			else if (sender == currentColumnsSelectAllMenuItem)
				SelectAll(currentColumnsListView);
			else if (sender == currentColumnsSelectInverseMenuItem)
				SelectInverse(currentColumnsListView);
			else if (sender == resultColumnsSelectAllMenuItem)
				SelectAll(resultColumnsListView);
			else if (sender == resultColumnsSelectInverseMenuItem)
				SelectInverse(resultColumnsListView);
		} // event CommandExec

		private async void cmdInsert_Click(object sender, EventArgs e)
		{
			if (!IsTableSelectMode)
			{
				MessageBox.Show(this, "Kein View gewählt.");
				return;
			}
			if (resultColumns.Count == 0)
			{
				MessageBox.Show(this, "Keine Spalte gewählt.");
				return;
			}

			Enabled = false;
			try
			{
				// create join expression
				var sbView = new StringBuilder();
				resultView.GetJoinExpression(sbView);

				// build columns, filter
				var visibleColumnHelper = new VisibleColumnHelper(resultView);

				await currentData.UpdateAsync(
					sbView.ToString(),
					resultFilter.Compile(visibleColumnHelper).ToString(),
					from col in resultColumns where visibleColumnHelper.IsVisibleColumn(col) select col
				);

				DialogResult = DialogResult.OK;
			}
			catch (Exception ex)
			{
				env.ShowException(ex);
			}
			finally
			{
				Enabled = true;
			}
		} // event cmdInsert_Click

		private void resultColumnsListView_AfterLabelEdit(object sender, LabelEditEventArgs e)
		{
			if (e.Item >= 0 && resultColumnsListView.Items[e.Item].Tag is ColumnData col)
			{
				col.DisplayName = e.Label;
				e.CancelEdit = false;
			}
			else
				e.CancelEdit = true;
		} // event resultColumnsListView_AfterLabelEdit
	} // class TableInsertForm
}
