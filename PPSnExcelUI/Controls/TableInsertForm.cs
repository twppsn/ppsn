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
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

		#region -- class ColumnSource -------------------------------------------------

		private sealed class ColumnSource : IPpsTableColumn
		{
			public ColumnSource(TreeNodeData columnSource, IDataColumn column)
			{
				Source = columnSource ?? throw new ArgumentNullException(nameof(columnSource));
				Column = column ?? throw new ArgumentNullException(nameof(column));

				DisplayName = column.Attributes.GetProperty<string>("DisplayName", column.Name);
			} // ctor

			public override string ToString()
				=> DisplayName;

			public bool IsEqualColumn(ColumnSource other)
				=> Column == other.Column && Source == other.Source;

			public TreeNodeData Source { get; }
			public IDataColumn Column { get; }

			public string DisplayName { get; }
			public string SourcePath => Source.Path;

			string IPpsTableColumn.Expression => Source.GetColumnName(Column);
			bool? IPpsTableColumn.Ascending
			{
				get
				{
					switch (ColumnSort)
					{
						case SortOrder.Ascending:
							return true;
						case SortOrder.Descending:
							return false;
						default:
							return null;
					}
				}
			} // prop Ascending

			public SortOrder ColumnSort { get; set; } = SortOrder.None;
		} // class ColumnSource

		#endregion

		#region -- class ColumCondition -----------------------------------------------

		private sealed class ColumnCondition : IPpsFilterColumn
		{
			private readonly ColumnSource columnSource;
			private PpsDataFilterCompareOperator op;
			private string compareValue;
			private IPpsFilterGroup groupdIndex = null;

			public ColumnCondition(ColumnSource columnSource, PpsDataFilterCompareOperator op = PpsDataFilterCompareOperator.Contains, PpsDataFilterCompareValue value = null)
			{
				this.columnSource = columnSource ?? throw new ArgumentNullException(nameof(columnSource));

				this.op = op;
				this.compareValue = value is PpsDataFilterCompareTextValue textValue ? textValue.Text : null;
			} // ctor

			private void SetExpression(string expr)
			{
				if (expr == null)
				{
					op = PpsDataFilterCompareOperator.Contains;
					compareValue = null;
				}
				else
				{
					expr = expr.Trim();
					var (newOp, valueOfs) = ParsePrefix(expr);
					op = newOp;
					compareValue = expr.Substring(valueOfs).Trim();
				}
			} // proc SetExpression

			private static (PpsDataFilterCompareOperator, int) ParsePrefix(string expr)
			{
				if (expr.Length >= 1)
				{
					switch (expr[0])
					{
						case '!':
							if (expr.Length >= 2 && expr[1] == '=')
								return (PpsDataFilterCompareOperator.NotEqual, 2);
							else
								return (PpsDataFilterCompareOperator.NotContains, 1);
						case '=':
							return (PpsDataFilterCompareOperator.Equal, 1);
						case '>':
							if (expr.Length >= 2 && expr[1] == '=')
								return (PpsDataFilterCompareOperator.GreaterOrEqual, 2);
							else
								return (PpsDataFilterCompareOperator.Greater, 1);
						case '<':
							if (expr.Length >= 2 && expr[1] == '=')
								return (PpsDataFilterCompareOperator.LowerOrEqual, 2);
							else
								return (PpsDataFilterCompareOperator.Lower, 1);
						default:
							return (PpsDataFilterCompareOperator.Contains, 0);
					}
				}
				else
					return (PpsDataFilterCompareOperator.Contains, 0);
			} // func ParsePrefix

			private static string GetPrefix(PpsDataFilterCompareOperator op)
			{
				switch (op)
				{
					case PpsDataFilterCompareOperator.NotContains:
						return "!";
					case PpsDataFilterCompareOperator.NotEqual:
						return "!=";
					case PpsDataFilterCompareOperator.Equal:
						return "=";
					case PpsDataFilterCompareOperator.Greater:
						return ">";
					case PpsDataFilterCompareOperator.GreaterOrEqual:
						return ">=";
					case PpsDataFilterCompareOperator.Lower:
						return "<";
					case PpsDataFilterCompareOperator.LowerOrEqual:
						return "<=";
					case PpsDataFilterCompareOperator.Contains:
					default:
						return String.Empty;
				}
			} // func GetPrefix

			private string GetExpression()
			{
				return GetPrefix(op) + compareValue;
			} // func GetExpression

			internal string FormatFilterExpression()
			{
				return IsEmpty
					? null
					: PpsDataFilterExpression.Compare(Source.Source.GetColumnName(Source.Column), op, compareValue).ToString();
			} // func FormatFilterExpression

			internal ColumnSource Source => columnSource;

			public string ColumnName => columnSource.DisplayName;
			public string ColumnSource => columnSource.SourcePath;

			public string Expression { get => GetExpression(); set => SetExpression(value); }
			public IPpsFilterGroup Group { get => groupdIndex; set => groupdIndex = value; }

			public bool IsEmpty => compareValue == null;
		} // class ColumnCondition

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

			public bool IsVisibleColumn(ColumnSource column)
			{
				if (resultView == column.Source)
					return true;

				foreach (var c in activeTreeNodes)
					if (c == column.Source)
						return true;

				return false;
			} // func IsVisibleColumn

			public bool IsVisibleColumn2(IPpsFilterColumn filterColumn)
				=> IsVisibleColumn(((ColumnCondition)filterColumn).Source);

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

			public ColumnSource FindColumnSource(string expression)
			{
				PpsDataColumnExpression.ParseQualifiedName(expression, out var exprAlias, out var exprColumn);

				var resultColumnSource = (TreeNodeData)null;
				var resultColumn = (IDataColumn)null;

				if (TryFindColumn(resultView, exprAlias, exprColumn, ref resultColumnSource, ref resultColumn))
					return new ColumnSource(resultColumnSource, resultColumn);

				foreach (var c in activeTreeNodes)
					if (TryFindColumn(c, exprAlias, exprColumn, ref resultColumnSource, ref resultColumn))
						return new ColumnSource(resultColumnSource, resultColumn);

				return resultColumnSource == null || resultColumn == null ? null : new ColumnSource(resultColumnSource, resultColumn);
			} // func FindColumnSource
		} // class VisibleColumnHelper

		#endregion

		private const string dataObjectFormat = "PpsnColumnSource[]";

		private readonly PpsEnvironment env;
		private readonly PpsViewDictionary availableViews; // list of all server tables
		private readonly List<ViewTreeNodeData> createdTreeNodeViews = new List<ViewTreeNodeData>();

		private IPpsTableData currentData = null;   // current selected mapping
		private ViewTreeNodeData resultView = null;   // current selected root table
		private List<ColumnSource> resultColumns = new List<ColumnSource>(); // result column set
		private List<IPpsFilterColumn> resultFilter = new List<IPpsFilterColumn>(); // filter expression

		#region -- Ctor/Dtor ----------------------------------------------------------

		public TableInsertForm(PpsEnvironment env)
		{
			this.env = env ?? throw new ArgumentNullException(nameof(env));

			InitializeComponent();

			filterGrid.RequireRefreshFilter += (sender, e) => RefreshResultColumns();
			availableViews = new PpsViewDictionary(env);
		} // ctor

		private void AddCompareExpression(VisibleColumnHelper visibleResultView, PpsDataFilterCompareExpression compareExpr)
		{
			if (compareExpr.Operand == null)
				return;

			var columnSource = visibleResultView.FindColumnSource(compareExpr.Operand); // todo: reuse columns?
			if (columnSource != null)
				resultFilter.Add(new ColumnCondition(columnSource, compareExpr.Operator, compareExpr.Value));
		} // proc AddCompareExpression

		private void LoadFilterExpression(VisibleColumnHelper visibleResultView, PpsDataFilterExpression expr)
		{
			if (expr == PpsDataFilterExpression.True)
				resultFilter.Clear();
			else if(expr is PpsDataFilterCompareExpression compareExpr)
			{
				AddCompareExpression(visibleResultView, compareExpr);
			}
			else if(expr is PpsDataFilterLogicExpression logicExpr)
			{
				foreach (var c in logicExpr.Arguments.OfType<PpsDataFilterCompareExpression>())
					AddCompareExpression(visibleResultView, c);
			}
		} // proc LoadFilterExpression

		private async Task RefreshAllAsync(IPpsTableData data)
		{
			// update data model
			await availableViews.RefreshAsync();

			// check current view
			if (data != null && !data.IsEmpty)
			{
				// joins
				SetResultView(new PpsJoinParser(availableViews, data.Views).Result);

				// update columns
				var visibleResultView = new VisibleColumnHelper(resultView);
				if (data.Columns != null)
				{
					foreach (var col in data.Columns)
					{
						var columnSource = visibleResultView.FindColumnSource(col.Expression);
						if (columnSource != null)
						{
							resultColumns.Add(columnSource);
							columnSource.ColumnSort = col.Ascending.HasValue ? (col.Ascending.Value ? SortOrder.Ascending : SortOrder.Descending) : SortOrder.None;
						}
					}
				}

				// update filter
				var expr = PpsDataFilterExpression.Parse(data.Filter);
				LoadFilterExpression(visibleResultView, expr);
			}

			// update view
			UpdateTreeView();
			RefreshResultColumns();
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

		#endregion

		#region -- Tree View Management -----------------------------------------------

		private static int BinarySearch(Func<int, string> g, int offset, int length, string text)
		{
			var startAt = offset;
			var endAt = offset + length - 1;

			while (startAt <= endAt)
			{
				var middle = startAt + ((endAt - startAt) >> 1);
				var t = String.Compare(g(middle), text, StringComparison.OrdinalIgnoreCase);
				if (t == 0)
					return middle;
				else if (t < 0)
					startAt = middle + 1;
				else
					endAt = middle - 1;
			}

			return ~startAt;
		} // proc BinaryNodeSearch

		private static void UpsertTreeNode(TreeNodeCollection nodes, TreeNodeData data)
		{
			var pos = BinarySearch(i => nodes[i].Text, 0, nodes.Count, data.NodeText);
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
				}
				else
				{
					SetResultView(null);
					view.SetActive(false);
					UpdateTreeView();
				}
				RefreshResultColumns();
				if (e.Node.IsSelected)
					RefreshAvailableColumns(view);
			}
			else if (e.Node.Tag is JoinTreeNodeData join) // child node
			{
				join.SetActive(e.Node.Checked);
				join.Refresh(e.Node);
				RefreshResultColumns();
				if (e.Node.IsSelected)
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
						if (currentColumnsListView.Items[i].Tag is ColumnSource columnSource && columnSource.Source != nodeData)
							currentColumnsListView.Items.RemoveAt(i);
					}

					// add new columns
					foreach (var col in nodeData.View.Columns)
					{
						var newCol = new ColumnSource(nodeData, col);
						var idx = BinarySearch(
							i => currentColumnsListView.Items[i].Text,
							0, currentColumnsListView.Items.Count,
							newCol.DisplayName
						);

						ListViewItem lvi;
						if (idx < 0)
							currentColumnsListView.Items.Insert(~idx, lvi = new ListViewItem() { Tag = newCol });
						else
							lvi = currentColumnsListView.Items[idx];

						lvi.Text = newCol.DisplayName;
					}
				}
			}
			finally
			{
				currentColumnsListView.EndUpdate();
			}
		} // proc RefreshAvailableColumns

		private void MoveColumnsToResult()
		{
			MoveColumnsToResult(from lvi in currentColumnsListView.SelectedItems.Cast<ListViewItem>() select (ColumnSource)lvi.Tag);
			currentColumnsListView.SelectedItems.Clear();
		} // proc MoveColumnsToResult

		private void MoveColumnsToResult(IEnumerable<ColumnSource> columnSource, int insertAt = -1)
		{
			if (insertAt < 0)
				insertAt = resultColumns.Count;

			// clear selection of target
			resultColumnsListView.SelectedItems.Clear();

			// add new items in target
			var toSelect = new List<ColumnSource>();
			foreach (var col in columnSource)
			{
				var existsIndex = resultColumns.FindIndex(col.IsEqualColumn);
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

		private void RemoveColumnsFromResult()
		{
			foreach (var lvi in resultColumnsListView.SelectedItems.Cast<ListViewItem>())
			{
				var column = (ColumnSource)lvi.Tag;
				var selectedIndex = resultColumns.FindIndex(column.IsEqualColumn);
				if (selectedIndex >= 0)
					resultColumns.RemoveAt(selectedIndex);
			}

			RefreshResultColumns();
		} // proc RemoveColumnsFromResult

		private void SetColumnResultSortOrder(SortOrder sort)
		{
			foreach (var lvi in resultColumnsListView.SelectedItems.Cast<ListViewItem>())
			{
				var column = (ColumnSource)lvi.Tag;
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

		private static void UpdateResultColumnListViewItem(ColumnSource currentColumn, ListViewItem currentLvi)
		{
			var columnSourcePath = currentColumn.SourcePath;
			var columnDisplayName = currentColumn.DisplayName;

			currentLvi.Text = columnDisplayName;
			currentLvi.ToolTipText = columnDisplayName + "\n" + columnSourcePath;

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

		private ListViewItem FindResultColumnListViewItem(ColumnSource column, int offset)
		{
			for (var i = offset; i < resultColumnsListView.Items.Count; i++)
			{
				if (resultColumnsListView.Items[i].Tag is ColumnSource other && column.IsEqualColumn(other))
					return resultColumnsListView.Items[i];
			}
			return null;
		} // func FindResultColumnListViewItem

		private void RefreshResultColumns(ColumnSource[] toSelect = null)
		{
			if (IsTableSelectMode)
			{
				// get all treenodes
				var visibleColumnHelper = new VisibleColumnHelper(resultView);

				// update filter
				filterGrid.SetFilter(resultFilter);
				filterGrid.RefreshFilter(visibleColumnHelper.IsVisibleColumn2);

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
						if (currentLvi == null) // not in lsit -> insert new 
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
							currentLvi.Selected = Array.Exists(toSelect, currentColumn.IsEqualColumn);

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
				filterGrid.SetFilter(null);
			}
		} // proc RefreshAvailableColumns

		#endregion

		#region -- ListView UI --------------------------------------------------------

		private Rectangle dragStartRectangle = Rectangle.Empty;

		private static IEnumerable<ColumnSource> CheckResultColumnsDragSource(DragEventArgs e)
		{
			var columns = e.Data.GetData(dataObjectFormat) as ColumnSource[];
			if (columns != null && columns.Length > 0)
				e.Effect = DragDropEffects.Move & e.AllowedEffect;
			return columns;
		} // func CheckResultColumnsDragSource

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
			currentColumnAddToResultMenuItem.Enabled = hasSelectedItems;
		} // event currentContextMenuStrip_Opening

		private void resultColumnsContextMenuStrip_Opening(object sender, CancelEventArgs e)
		{
			var hasItems = resultColumnsListView.Items.Count > 0;
			var hasSelectedItems = resultColumnsListView.SelectedItems.Count > 0;

			resultColumnsSelectAllMenuItem.Enabled = hasItems;
			resultColumnsSelectInverseMenuItem.Enabled = hasItems;
			resultColumnRemoveMenuItem.Enabled = hasSelectedItems;
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
			if (sender is ListView listView && (e.Button & MouseButtons.Left) != 0 && !dragStartRectangle.Contains(e.X, e.Y))
			{
				dragStartRectangle = Rectangle.Empty;

				var dragData = new DataObject(dataObjectFormat,
					(
						from lvi in listView.SelectedItems.Cast<ListViewItem>()
						where lvi.Tag is ColumnSource
						select (ColumnSource)lvi.Tag
					).ToArray()
				);

				if (DoDragDrop(dragData, DragDropEffects.Move) == DragDropEffects.Move)
					listView.SelectedItems.Clear();
			}
		} // event listView_MouseMove

		private void resultColumnsListView_DragEnter(object sender, DragEventArgs e)
			=> CheckResultColumnsDragSource(e);

		private void resultColumnsListView_DragOver(object sender, DragEventArgs e)
			=> CheckResultColumnsDragSource(e);

		private void resultColumnsListView_DragDrop(object sender, DragEventArgs e)
		{
			var columns = CheckResultColumnsDragSource(e);

			var hoverItem = GetListViewHoverItem(resultColumnsListView, e, out var insertAfter);
			if (hoverItem == null)
				MoveColumnsToResult(columns, insertAfter ? -1 : 0); // append at end
			else if (hoverItem.Tag is ColumnSource columnSource)
			{
				// get index to insert
				var insertAt = resultColumns.FindIndex(columnSource.IsEqualColumn);
				if (insertAfter)
					insertAt++;

				// insert items
				MoveColumnsToResult(columns, insertAt);
			}
		} // event resultColumnsListView_DragDrop

		private void filterGrid_DragEnter(object sender, DragEventArgs e)
			=> CheckResultColumnsDragSource(e);

		private void filterGrid_DragOver(object sender, DragEventArgs e)
			=> CheckResultColumnsDragSource(e);

		private void filterGrid_DragDrop(object sender, DragEventArgs e)
		{
			var columns = CheckResultColumnsDragSource(e);
			if (columns != null)
				filterGrid.InsertFilter(columns.Select(c => new ColumnCondition(c)), new Point(e.X, e.Y));
		} // event filterGrid_DragDrop

		#endregion

		private void CommandExec(object sender, EventArgs e)
		{
			if (sender == currentColumnAddToResultMenuItem)
				MoveColumnsToResult();
			else if (sender == resultColumnRemoveMenuItem)
				RemoveColumnsFromResult();
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
				MessageBox.Show(this, "Kein View gewählt");
				return;
			}
			try
			{
				// create join expression
				var sbView = new StringBuilder();
				resultView.GetJoinExpression(sbView);

				// build columns, filter
				var visibleColumnHelper = new VisibleColumnHelper(resultView);

				await currentData.UpdateAsync(
					sbView.ToString(),
					String.Join(" ", from f in resultFilter.Cast<ColumnCondition>() where !f.IsEmpty && visibleColumnHelper.IsVisibleColumn2(f) select f.FormatFilterExpression()),
					from col in resultColumns where visibleColumnHelper.IsVisibleColumn(col) select col
				);

				DialogResult = DialogResult.OK;
			}
			catch (Exception ex)
			{
				env.ShowException(ex);
			}
		} // event cmdInsert_Click
	} // class TableInsertForm
}
