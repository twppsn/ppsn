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
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using TecWare.DE.Data;
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

					joins.Clear();
					foreach (var c in View.Joins)
						joins.Add(new JoinTreeNodeData(this, c));

				}
				else// clear all parents
				{
					this.alias = null;
					joins.Clear();
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
			
				foreach(var j in joins)
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

			public abstract string NodeText { get; }
			public virtual string NodeToolTip => null;

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

		private sealed class ColumnSource
		{
			public ColumnSource(PpsViewDefinition view, IDataColumn column)
			{
				View = view ?? throw new ArgumentNullException(nameof(view));
				Column = column ?? throw new ArgumentNullException(nameof(column));
			} // ctor

			public override string ToString()
				=> Column.Name;

			public PpsViewDefinition View { get; }
			public IDataColumn Column { get; }
		} // class ColumnSource

		#endregion

		private readonly PpsEnvironment env;
		private readonly PpsViewDictionary availableViews; // list of all server tables

		private IPpsTableData currentData = null;	// current selected mapping
		private ViewTreeNodeData selectedView = null;   // current selected root table

		#region -- Ctor/Dtor ----------------------------------------------------------

		public TableInsertForm(PpsEnvironment env)
		{
			this.env = env ?? throw new ArgumentNullException(nameof(env));

			InitializeComponent();

			availableViews = new PpsViewDictionary(env);
		} // ctor

		private async Task RefreshAllAsync(IPpsTableData data)
		{
			// update data model
			await availableViews.RefreshAsync();

			// check current view
			if (data != null)
				selectedView = new PpsJoinParser(availableViews, data.Views).Result;

			// update view
			UpdateTreeView();
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

		private static int BinaryTreeNodeSearch(TreeNodeCollection nodes, int offset, int length, string text)
		{
			var startAt = offset;
			var endAt = offset + length - 1;

			while (startAt <= endAt)
			{
				var middle = startAt + ((endAt - startAt) >> 1);
				var t = String.Compare(nodes[middle].Text, text, StringComparison.OrdinalIgnoreCase);
				if (t == 0)
					return middle;
				else if (t < 0)
					startAt = middle + 1;
				else
					endAt = middle - 1;
			}

			return ~startAt;
		} // proc BinaryTreeNodeSearch

		private static void UpsertTreeNode(TreeNodeCollection nodes, TreeNodeData data)
		{
			var pos = BinaryTreeNodeSearch(nodes, 0, nodes.Count, data.NodeText);
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

		private void UpdateTreeView()
		{
			if (IsTableSelectMode) // table selected
			{
				// remove all to one
				var d = tableTree.Nodes.Count - 1;
				while (tableTree.Nodes.Count > 1)
				{
					if (tableTree.Nodes[d].Tag == selectedView)
						d--;
					tableTree.Nodes.RemoveAt(d);
					d--;
				}
				if (tableTree.Nodes.Count == 0)
					tableTree.Nodes.Add("Node");

				selectedView.Refresh(tableTree.Nodes[0]);
			}
			else // show full filtered list
				UpdateTreeNodes(tableTree.Nodes, availableViews.Select(c => new ViewTreeNodeData(c.Value, null, false)));
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
					selectedView = view;
					UpdateTreeView();
				}
				else
				{
					selectedView = null;
					view.SetActive(false);
					UpdateTreeView();
				}
				RefreshAvailableColumns();
			}
			else if (e.Node.Tag is JoinTreeNodeData join) // child node
			{
				join.SetActive(e.Node.Checked);
				join.Refresh(e.Node);
				RefreshAvailableColumns();
			}
		} // event tableTree_AfterCheck

		#endregion

		#region -- RefreshAvailableColumns --------------------------------------------

		private void RefreshAvailableColumns()
		{
			//availableColumns.BeginUpdate();
			//try
			//{
			//	if (selectedView == null)
			//		availableColumns.Items.Clear();
			//	else
			//	{
			//		foreach (var view in selectedView.GetViews())
			//		{
			//			foreach (var col in view.Columns)
			//			{
			//				availableColumns.Items.Add(new ColumnSource(view, col));
			//			}
			//		}
			//	}
			//}
			//finally
			//{
			//	availableColumns.EndUpdate();
			//}
		} // proc RefreshAvailableColumns

		#endregion

		private bool IsTableSelectMode => selectedView != null;

		private async void cmdInsert_Click(object sender, EventArgs e)
		{
			if (!IsTableSelectMode)
			{
				MessageBox.Show(this, "Kein View gewählt");
				return;
			}
			try
			{

				var sbView = new StringBuilder();
				selectedView.GetJoinExpression(sbView);

				await currentData.UpdateAsync(sbView.ToString());

				DialogResult = DialogResult.OK;
			}
			catch (Exception ex)
			{
				env.ShowException(ex);
			}

			//var columns = new List<PpsDataColumnExpression>();
			//for (var i = 0; i < availableColumns.Items.Count; i++)
			//{
			//	if (availableColumns.GetItemChecked(i))
			//		columns.Add(new PpsDataColumnExpression(((ColumnSource)availableColumns.Items[i]).Column.Name));
			//}
			//if (columns.Count == 0)
			//{
			//	MessageBox.Show(this, "Keine Spalten gewählt");
			//	return;
			//}

			//ReportName = "Custom";
			//ReportSource = new PpsListMapping(env, sbView.ToString(), columns.ToArray(), conditionExpression.Text, null);
		} // event cmdInsert_Click

		public string ReportName { get; private set; } = null;
		//public PpsListMapping ReportSource { get; private set; } = null;
	} // class TableInsertForm
}
