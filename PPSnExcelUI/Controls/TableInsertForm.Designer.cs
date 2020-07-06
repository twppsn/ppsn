namespace TecWare.PPSn.Controls
{
	partial class TableInsertForm
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing)
		{
			if (disposing && (components != null))
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Windows Form Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.components = new System.ComponentModel.Container();
			System.Windows.Forms.ColumnHeader columnHeader1;
			System.Windows.Forms.ColumnHeader columnHeader2;
			System.Windows.Forms.ContextMenuStrip resultColumnsContextMenuStrip;
			System.Windows.Forms.ToolStripSeparator toolStripSeparator3;
			System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
			System.Windows.Forms.ToolStripSeparator toolStripMenuItem1;
			System.Windows.Forms.ColumnHeader columnHeader3;
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(TableInsertForm));
			this.resultColumnAddToCondition = new System.Windows.Forms.ToolStripMenuItem();
			this.resultColumnsSelectAllMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.resultColumnsSelectInverseMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.resultColumnSortNoneMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.resultColumnSortAscMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.resultColumnSortDescMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
			this.resultColumnRemoveMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.cmdRefresh = new System.Windows.Forms.Button();
			this.cmdClose = new System.Windows.Forms.Button();
			this.tableTree = new TecWare.PPSn.Controls.PpsTreeView();
			this.filterGrid = new TecWare.PPSn.Controls.PpsFilterEditor();
			this.currentColumnsListView = new System.Windows.Forms.ListView();
			this.currentContextMenuStrip = new System.Windows.Forms.ContextMenuStrip(this.components);
			this.currentColumnAddToResultMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.currentColumnAddToCondition = new System.Windows.Forms.ToolStripMenuItem();
			this.currentColumnsSelectAllMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.currentColumnsSelectInverseMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.resultColumnsListView = new System.Windows.Forms.ListView();
			this.imageListSort = new System.Windows.Forms.ImageList(this.components);
			columnHeader1 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			columnHeader2 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			resultColumnsContextMenuStrip = new System.Windows.Forms.ContextMenuStrip(this.components);
			toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
			toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
			toolStripMenuItem1 = new System.Windows.Forms.ToolStripSeparator();
			columnHeader3 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			resultColumnsContextMenuStrip.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.filterGrid)).BeginInit();
			this.currentContextMenuStrip.SuspendLayout();
			this.SuspendLayout();
			// 
			// columnHeader1
			// 
			columnHeader1.Text = "Spalte";
			columnHeader1.Width = 220;
			// 
			// columnHeader2
			// 
			columnHeader2.Text = "Spalte";
			columnHeader2.Width = 200;
			// 
			// resultColumnsContextMenuStrip
			// 
			resultColumnsContextMenuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.resultColumnAddToCondition,
            toolStripSeparator3,
            this.resultColumnsSelectAllMenuItem,
            this.resultColumnsSelectInverseMenuItem,
            toolStripSeparator2,
            this.resultColumnSortNoneMenuItem,
            this.resultColumnSortAscMenuItem,
            this.resultColumnSortDescMenuItem,
            this.toolStripSeparator1,
            this.resultColumnRemoveMenuItem});
			resultColumnsContextMenuStrip.Name = "resultColumnsContextMenuStrip";
			resultColumnsContextMenuStrip.Size = new System.Drawing.Size(200, 176);
			resultColumnsContextMenuStrip.Opening += new System.ComponentModel.CancelEventHandler(this.resultColumnsContextMenuStrip_Opening);
			// 
			// resultColumnAddToCondition
			// 
			this.resultColumnAddToCondition.Name = "resultColumnAddToCondition";
			this.resultColumnAddToCondition.Size = new System.Drawing.Size(199, 22);
			this.resultColumnAddToCondition.Text = "Als Bedingung";
			this.resultColumnAddToCondition.Click += new System.EventHandler(this.CommandExec);
			// 
			// toolStripSeparator3
			// 
			toolStripSeparator3.Name = "toolStripSeparator3";
			toolStripSeparator3.Size = new System.Drawing.Size(196, 6);
			// 
			// resultColumnsSelectAllMenuItem
			// 
			this.resultColumnsSelectAllMenuItem.Name = "resultColumnsSelectAllMenuItem";
			this.resultColumnsSelectAllMenuItem.ShortcutKeyDisplayString = "Strg+A";
			this.resultColumnsSelectAllMenuItem.Size = new System.Drawing.Size(199, 22);
			this.resultColumnsSelectAllMenuItem.Text = "Alles markieren";
			this.resultColumnsSelectAllMenuItem.Click += new System.EventHandler(this.CommandExec);
			// 
			// resultColumnsSelectInverseMenuItem
			// 
			this.resultColumnsSelectInverseMenuItem.Name = "resultColumnsSelectInverseMenuItem";
			this.resultColumnsSelectInverseMenuItem.Size = new System.Drawing.Size(199, 22);
			this.resultColumnsSelectInverseMenuItem.Text = "Markierung umkehren";
			this.resultColumnsSelectInverseMenuItem.Click += new System.EventHandler(this.CommandExec);
			// 
			// toolStripSeparator2
			// 
			toolStripSeparator2.Name = "toolStripSeparator2";
			toolStripSeparator2.Size = new System.Drawing.Size(196, 6);
			// 
			// resultColumnSortNoneMenuItem
			// 
			this.resultColumnSortNoneMenuItem.Name = "resultColumnSortNoneMenuItem";
			this.resultColumnSortNoneMenuItem.Size = new System.Drawing.Size(199, 22);
			this.resultColumnSortNoneMenuItem.Text = "Keine Sortierung";
			this.resultColumnSortNoneMenuItem.Click += new System.EventHandler(this.CommandExec);
			// 
			// resultColumnSortAscMenuItem
			// 
			this.resultColumnSortAscMenuItem.Image = global::TecWare.PPSn.Properties.Resources.SortAscendingImage;
			this.resultColumnSortAscMenuItem.Name = "resultColumnSortAscMenuItem";
			this.resultColumnSortAscMenuItem.Size = new System.Drawing.Size(199, 22);
			this.resultColumnSortAscMenuItem.Text = "Aufsteigend sortieren";
			this.resultColumnSortAscMenuItem.Click += new System.EventHandler(this.CommandExec);
			// 
			// resultColumnSortDescMenuItem
			// 
			this.resultColumnSortDescMenuItem.Image = global::TecWare.PPSn.Properties.Resources.SortDescendingImage;
			this.resultColumnSortDescMenuItem.Name = "resultColumnSortDescMenuItem";
			this.resultColumnSortDescMenuItem.Size = new System.Drawing.Size(199, 22);
			this.resultColumnSortDescMenuItem.Text = "Absteigend sortieren";
			this.resultColumnSortDescMenuItem.Click += new System.EventHandler(this.CommandExec);
			// 
			// toolStripSeparator1
			// 
			this.toolStripSeparator1.Name = "toolStripSeparator1";
			this.toolStripSeparator1.Size = new System.Drawing.Size(196, 6);
			// 
			// resultColumnRemoveMenuItem
			// 
			this.resultColumnRemoveMenuItem.Name = "resultColumnRemoveMenuItem";
			this.resultColumnRemoveMenuItem.ShortcutKeyDisplayString = "Entf";
			this.resultColumnRemoveMenuItem.Size = new System.Drawing.Size(199, 22);
			this.resultColumnRemoveMenuItem.Text = "&Entfernen";
			this.resultColumnRemoveMenuItem.Click += new System.EventHandler(this.CommandExec);
			// 
			// toolStripMenuItem1
			// 
			toolStripMenuItem1.Name = "toolStripMenuItem1";
			toolStripMenuItem1.Size = new System.Drawing.Size(196, 6);
			// 
			// columnHeader3
			// 
			columnHeader3.Text = "Quelle";
			columnHeader3.Width = 140;
			// 
			// cmdRefresh
			// 
			this.cmdRefresh.Location = new System.Drawing.Point(707, 540);
			this.cmdRefresh.Name = "cmdRefresh";
			this.cmdRefresh.Size = new System.Drawing.Size(100, 23);
			this.cmdRefresh.TabIndex = 4;
			this.cmdRefresh.Text = "Einfügen";
			this.cmdRefresh.UseVisualStyleBackColor = true;
			this.cmdRefresh.Click += new System.EventHandler(this.cmdInsert_Click);
			// 
			// cmdClose
			// 
			this.cmdClose.DialogResult = System.Windows.Forms.DialogResult.Cancel;
			this.cmdClose.Location = new System.Drawing.Point(813, 540);
			this.cmdClose.Name = "cmdClose";
			this.cmdClose.Size = new System.Drawing.Size(100, 23);
			this.cmdClose.TabIndex = 5;
			this.cmdClose.Text = "Schließen";
			this.cmdClose.UseVisualStyleBackColor = true;
			// 
			// tableTree
			// 
			this.tableTree.CheckBoxes = true;
			this.tableTree.HideSelection = false;
			this.tableTree.Location = new System.Drawing.Point(16, 16);
			this.tableTree.Name = "tableTree";
			this.tableTree.ShowPlusMinus = false;
			this.tableTree.ShowRootLines = false;
			this.tableTree.Size = new System.Drawing.Size(269, 355);
			this.tableTree.TabIndex = 0;
			this.tableTree.AfterCheck += new System.Windows.Forms.TreeViewEventHandler(this.tableTree_AfterCheck);
			this.tableTree.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.tableTree_AfterSelect);
			// 
			// filterGrid
			// 
			this.filterGrid.AllowDrop = true;
			this.filterGrid.AllowUserToAddRows = false;
			this.filterGrid.AllowUserToDeleteRows = false;
			this.filterGrid.AllowUserToOrderColumns = true;
			this.filterGrid.AllowUserToResizeRows = false;
			this.filterGrid.BackgroundColor = System.Drawing.SystemColors.Window;
			this.filterGrid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
			this.filterGrid.EditMode = System.Windows.Forms.DataGridViewEditMode.EditOnEnter;
			this.filterGrid.GridColor = System.Drawing.SystemColors.Window;
			this.filterGrid.Location = new System.Drawing.Point(16, 377);
			this.filterGrid.Name = "filterGrid";
			this.filterGrid.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.CellSelect;
			this.filterGrid.Size = new System.Drawing.Size(897, 157);
			this.filterGrid.TabIndex = 3;
			this.filterGrid.DragDrop += new System.Windows.Forms.DragEventHandler(this.filterGrid_DragDrop);
			this.filterGrid.DragEnter += new System.Windows.Forms.DragEventHandler(this.filterGrid_DragEnter);
			this.filterGrid.DragOver += new System.Windows.Forms.DragEventHandler(this.filterGrid_DragOver);
			// 
			// currentColumnsListView
			// 
			this.currentColumnsListView.AllowDrop = true;
			this.currentColumnsListView.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            columnHeader1});
			this.currentColumnsListView.ContextMenuStrip = this.currentContextMenuStrip;
			this.currentColumnsListView.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.Nonclickable;
			this.currentColumnsListView.HideSelection = false;
			this.currentColumnsListView.Location = new System.Drawing.Point(291, 16);
			this.currentColumnsListView.Name = "currentColumnsListView";
			this.currentColumnsListView.ShowGroups = false;
			this.currentColumnsListView.Size = new System.Drawing.Size(243, 355);
			this.currentColumnsListView.TabIndex = 1;
			this.currentColumnsListView.UseCompatibleStateImageBehavior = false;
			this.currentColumnsListView.View = System.Windows.Forms.View.Details;
			this.currentColumnsListView.DragDrop += new System.Windows.Forms.DragEventHandler(this.currentColumnsListView_DragDrop);
			this.currentColumnsListView.DragEnter += new System.Windows.Forms.DragEventHandler(this.currentColumnsListView_DragEnter);
			this.currentColumnsListView.KeyUp += new System.Windows.Forms.KeyEventHandler(this.currentColumnsListView_KeyUp);
			this.currentColumnsListView.MouseDown += new System.Windows.Forms.MouseEventHandler(this.listView_MouseDown);
			this.currentColumnsListView.MouseMove += new System.Windows.Forms.MouseEventHandler(this.listView_MouseMove);
			// 
			// currentContextMenuStrip
			// 
			this.currentContextMenuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.currentColumnAddToResultMenuItem,
            this.currentColumnAddToCondition,
            toolStripMenuItem1,
            this.currentColumnsSelectAllMenuItem,
            this.currentColumnsSelectInverseMenuItem});
			this.currentContextMenuStrip.Name = "currentContextMenuStrip";
			this.currentContextMenuStrip.Size = new System.Drawing.Size(200, 98);
			this.currentContextMenuStrip.Opening += new System.ComponentModel.CancelEventHandler(this.currentContextMenuStrip_Opening);
			// 
			// currentColumnAddToResultMenuItem
			// 
			this.currentColumnAddToResultMenuItem.Name = "currentColumnAddToResultMenuItem";
			this.currentColumnAddToResultMenuItem.Size = new System.Drawing.Size(199, 22);
			this.currentColumnAddToResultMenuItem.Text = "Hinzufügen";
			this.currentColumnAddToResultMenuItem.Click += new System.EventHandler(this.CommandExec);
			// 
			// currentColumnAddToCondition
			// 
			this.currentColumnAddToCondition.Name = "currentColumnAddToCondition";
			this.currentColumnAddToCondition.Size = new System.Drawing.Size(199, 22);
			this.currentColumnAddToCondition.Text = "Als Bedingung";
			this.currentColumnAddToCondition.Click += new System.EventHandler(this.CommandExec);
			// 
			// currentColumnsSelectAllMenuItem
			// 
			this.currentColumnsSelectAllMenuItem.Name = "currentColumnsSelectAllMenuItem";
			this.currentColumnsSelectAllMenuItem.ShortcutKeyDisplayString = "Strg+A";
			this.currentColumnsSelectAllMenuItem.Size = new System.Drawing.Size(199, 22);
			this.currentColumnsSelectAllMenuItem.Text = "Alles markieren";
			this.currentColumnsSelectAllMenuItem.Click += new System.EventHandler(this.CommandExec);
			// 
			// currentColumnsSelectInverseMenuItem
			// 
			this.currentColumnsSelectInverseMenuItem.Name = "currentColumnsSelectInverseMenuItem";
			this.currentColumnsSelectInverseMenuItem.Size = new System.Drawing.Size(199, 22);
			this.currentColumnsSelectInverseMenuItem.Text = "Markierung umkehren";
			this.currentColumnsSelectInverseMenuItem.Click += new System.EventHandler(this.CommandExec);
			// 
			// resultColumnsListView
			// 
			this.resultColumnsListView.AllowDrop = true;
			this.resultColumnsListView.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            columnHeader2,
            columnHeader3});
			this.resultColumnsListView.ContextMenuStrip = resultColumnsContextMenuStrip;
			this.resultColumnsListView.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.Nonclickable;
			this.resultColumnsListView.HideSelection = false;
			this.resultColumnsListView.LabelEdit = true;
			this.resultColumnsListView.Location = new System.Drawing.Point(540, 16);
			this.resultColumnsListView.Name = "resultColumnsListView";
			this.resultColumnsListView.ShowGroups = false;
			this.resultColumnsListView.ShowItemToolTips = true;
			this.resultColumnsListView.Size = new System.Drawing.Size(373, 355);
			this.resultColumnsListView.SmallImageList = this.imageListSort;
			this.resultColumnsListView.TabIndex = 2;
			this.resultColumnsListView.UseCompatibleStateImageBehavior = false;
			this.resultColumnsListView.View = System.Windows.Forms.View.Details;
			this.resultColumnsListView.AfterLabelEdit += new System.Windows.Forms.LabelEditEventHandler(this.resultColumnsListView_AfterLabelEdit);
			this.resultColumnsListView.DragDrop += new System.Windows.Forms.DragEventHandler(this.resultColumnsListView_DragDrop);
			this.resultColumnsListView.DragEnter += new System.Windows.Forms.DragEventHandler(this.resultColumnsListView_DragEnter);
			this.resultColumnsListView.DragOver += new System.Windows.Forms.DragEventHandler(this.resultColumnsListView_DragOver);
			this.resultColumnsListView.KeyUp += new System.Windows.Forms.KeyEventHandler(this.resultColumnsListView_KeyUp);
			this.resultColumnsListView.MouseDown += new System.Windows.Forms.MouseEventHandler(this.listView_MouseDown);
			this.resultColumnsListView.MouseMove += new System.Windows.Forms.MouseEventHandler(this.listView_MouseMove);
			// 
			// imageListSort
			// 
			this.imageListSort.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("imageListSort.ImageStream")));
			this.imageListSort.TransparentColor = System.Drawing.Color.Transparent;
			this.imageListSort.Images.SetKeyName(0, "sort_ascending.png");
			this.imageListSort.Images.SetKeyName(1, "sort_descending.png");
			// 
			// TableInsertForm
			// 
			this.AcceptButton = this.cmdRefresh;
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(934, 575);
			this.Controls.Add(this.resultColumnsListView);
			this.Controls.Add(this.currentColumnsListView);
			this.Controls.Add(this.filterGrid);
			this.Controls.Add(this.tableTree);
			this.Controls.Add(this.cmdClose);
			this.Controls.Add(this.cmdRefresh);
			this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.Name = "TableInsertForm";
			this.Padding = new System.Windows.Forms.Padding(13);
			this.ShowIcon = false;
			this.ShowInTaskbar = false;
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "Tabelle insert/edit";
			resultColumnsContextMenuStrip.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)(this.filterGrid)).EndInit();
			this.currentContextMenuStrip.ResumeLayout(false);
			this.ResumeLayout(false);

		}

		#endregion
		private System.Windows.Forms.Button cmdRefresh;
		private System.Windows.Forms.Button cmdClose;
		private PpsTreeView tableTree;
		private PpsFilterEditor filterGrid;
		private System.Windows.Forms.ListView currentColumnsListView;
		private System.Windows.Forms.ListView resultColumnsListView;
		private System.Windows.Forms.ContextMenuStrip currentContextMenuStrip;
		private System.Windows.Forms.ToolStripMenuItem currentColumnAddToResultMenuItem;
		private System.Windows.Forms.ToolStripMenuItem resultColumnSortNoneMenuItem;
		private System.Windows.Forms.ToolStripMenuItem resultColumnSortAscMenuItem;
		private System.Windows.Forms.ToolStripMenuItem resultColumnSortDescMenuItem;
		private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
		private System.Windows.Forms.ToolStripMenuItem resultColumnRemoveMenuItem;
		private System.Windows.Forms.ImageList imageListSort;
		private System.Windows.Forms.ToolStripMenuItem resultColumnsSelectAllMenuItem;
		private System.Windows.Forms.ToolStripMenuItem resultColumnsSelectInverseMenuItem;
		private System.Windows.Forms.ToolStripMenuItem currentColumnsSelectInverseMenuItem;
		private System.Windows.Forms.ToolStripMenuItem currentColumnsSelectAllMenuItem;
		private System.Windows.Forms.ToolStripMenuItem resultColumnAddToCondition;
		private System.Windows.Forms.ToolStripMenuItem currentColumnAddToCondition;
	}
}