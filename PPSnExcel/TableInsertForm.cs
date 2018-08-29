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
using TecWare.PPSn;
using TecWare.PPSn.Data;

namespace PPSnExcel
{
	internal partial class TableInsertForm : Form
	{
		#region -- class TreeNodeData -------------------------------------------------

		private abstract class TreeNodeData
		{
			private readonly TreeNodeData parent;
			private readonly List<JoinTreeNodeData> joins = new List<JoinTreeNodeData>();
			private bool isActive = false;

			public TreeNodeData(TreeNodeData parent)
			{
				this.parent = parent;
			} // ctor

			public virtual void Refresh(TreeNode tn)
			{
				tn.Text = NodeText;
				tn.ToolTipText = NodeToolTip;
				tn.Tag = this;
				tn.Checked = isActive;

				if (isActive)
				{
					UpdateTreeNodes(tn.Nodes, joins);
					if (!tn.IsExpanded && tn.Nodes.Count > 0)
						tn.Expand();
				}
				else
					tn.Nodes.Clear();
			} // proc Refresh

			private void SetActive(bool isNew)
			{
				if (isNew == isActive)
					return;

				isActive = isNew;
				if (isActive) // build new joins
				{
					joins.Clear();
					foreach (var c in View.Joins)
						joins.Add(new JoinTreeNodeData(this, c));
				
				}
				else// clear all parents
					joins.Clear();
			} // proc SetActive

			public IEnumerable<PpsViewDefinition> GetViews()
			{
				yield return View;
				foreach (var c in ActiveJoins)
				{
					foreach (var v in c.GetViews())
						yield return v;
				}
			} // func GetViews

			public void GetJoinExpression(StringBuilder sb)
			{
				sb.Append(String.Join(",", GetViews().Select(c => c.ViewId)));
			} // func GetJoinExpression

			public abstract string NodeKey { get; }
			public abstract string NodeText { get; }
			public virtual string NodeToolTip => null;

			public TreeNodeData Parent => parent;

			protected IEnumerable<JoinTreeNodeData> ActiveJoins => from j in joins where j.isActive select j;
			protected abstract PpsViewDefinition View { get; }

			public bool IsActive { get => isActive; set => SetActive(value); }
		} // class TreeNodeData

		#endregion

		#region -- class ViewTreeNodeData ---------------------------------------------

		private sealed class ViewTreeNodeData : TreeNodeData
		{
			private readonly PpsViewDefinition view;

			public ViewTreeNodeData(PpsViewDefinition view, bool isActive)
				: base(null)
			{
				this.view = view;

				IsActive = isActive;
			} // ctor

			public override string NodeKey => view.ViewId;
			public override string NodeText => view.DisplayName;

			protected override PpsViewDefinition View => view;
		} // class ViewTreeNodeData

		#endregion

		#region -- class JoinTreeNodeData ---------------------------------------------

		private sealed class JoinTreeNodeData : TreeNodeData
		{
			private readonly string key;
			private readonly PpsViewJoinDefinition join;

			public JoinTreeNodeData(TreeNodeData parent, PpsViewJoinDefinition join)
				: base(parent)
			{
				this.join = join;
				this.key = join.Alias != null ? join.Alias + join.View.ViewId : join.View.ViewId;
			} // ctor

			public override string NodeKey => key;
			public override string NodeText => join.DisplayName;

			protected override PpsViewDefinition View => join.View;
			public PpsViewJoinDefinition JoinDefinition => join;
		} // class JoinTreeNodeData

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

		private ViewTreeNodeData selectedView = null; // current selected root table

		#region -- Ctor/Dtor ----------------------------------------------------------

		public TableInsertForm(PpsEnvironment env)
		{
			this.env = env ?? throw new ArgumentNullException(nameof(env));

			InitializeComponent();

			this.availableViews = new PpsViewDictionary(env);
			env.Spawn(RefreshAllAsync);
		} // ctor

		private async Task RefreshAllAsync()
		{
			await availableViews.RefreshAsync();
			await env.InvokeAsync(UpdateTreeView);
		} // func RefreshAllAsync

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
				nodes.Insert(pos, data.NodeKey, data.NodeText);
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
				UpdateTreeNodes(tableTree.Nodes, availableViews.Select(c => new ViewTreeNodeData(c.Value, false)));
		} // proc UpdateTreeView

		private void tableTree_AfterCheck(object sender, TreeViewEventArgs e)
		{
			if (e.Action == TreeViewAction.Unknown)
				return;

			if (e.Node.Tag is ViewTreeNodeData view) // root node
			{
				if (e.Node.Checked)
				{
					view.IsActive = true;
					selectedView = view;
					UpdateTreeView();
				}
				else
				{
					selectedView = null;
					view.IsActive = false;
					UpdateTreeView();
				}
				RefreshAvailableColumns();
			}
			else if (e.Node.Tag is JoinTreeNodeData join) // child node
			{
				join.IsActive = e.Node.Checked;
				join.Refresh(e.Node);
				RefreshAvailableColumns();
			}
		} // event tableTree_AfterCheck

		#endregion

		#region -- RefreshAvailableColumns --------------------------------------------

		private void RefreshAvailableColumns()
		{
			availableColumns.BeginUpdate();
			try
			{
				if (selectedView == null)
					availableColumns.Items.Clear();
				else
				{
					foreach (var view in selectedView.GetViews())
					{
						foreach (var col in view.Columns)
						{
							availableColumns.Items.Add(new ColumnSource(view, col));
						}
					}
				}
			}
			finally
			{
				availableColumns.EndUpdate();
			}
		} // proc RefreshAvailableColumns

		#endregion

		private void RefreshMode()
		{
		} // proc RefreshMode
		
		private bool IsTableSelectMode => selectedView != null;

		private void cmdInsert_Click(object sender, EventArgs e)
		{
			if (!IsTableSelectMode)
			{
				MessageBox.Show(this, "Kein View gewählt");
				return;
			}

			var sbView = new StringBuilder();
			selectedView.GetJoinExpression(sbView);

			var columns = new List<PpsDataColumnExpression>();
			for (var i = 0; i < availableColumns.Items.Count; i++)
			{
				if (availableColumns.GetItemChecked(i))
					columns.Add(new PpsDataColumnExpression(((ColumnSource)availableColumns.Items[i]).Column.Name));
			}
			if(columns.Count ==0)
			{
				MessageBox.Show(this, "Keine Spalten gewählt");
				return;
			}

			ReportName = "Custom";
			ReportSource = new PpsListMapping(env, sbView.ToString(), columns.ToArray(), conditionExpression.Text, null);

			DialogResult = DialogResult.OK;
		} // event cmdInsert_Click

		public string ReportName { get; private set; } = null;
		public PpsListMapping ReportSource { get; private set; } = null;
	} // class TableInsertForm
}
