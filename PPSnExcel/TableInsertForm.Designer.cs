namespace PPSnExcel
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
			this.tableSearchText = new System.Windows.Forms.TextBox();
			this.label3 = new System.Windows.Forms.Label();
			this.conditionExpression = new System.Windows.Forms.TextBox();
			this.cmdInsert = new System.Windows.Forms.Button();
			this.cmdClose = new System.Windows.Forms.Button();
			this.tableTree = new System.Windows.Forms.TreeView();
			this.availableColumns = new System.Windows.Forms.CheckedListBox();
			this.SuspendLayout();
			// 
			// tableSearchText
			// 
			this.tableSearchText.Location = new System.Drawing.Point(121, 477);
			this.tableSearchText.Name = "tableSearchText";
			this.tableSearchText.Size = new System.Drawing.Size(159, 20);
			this.tableSearchText.TabIndex = 1;
			// 
			// label3
			// 
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(304, 393);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(73, 13);
			this.label3.TabIndex = 8;
			this.label3.Text = "Bedingungen:";
			// 
			// conditionExpression
			// 
			this.conditionExpression.Location = new System.Drawing.Point(307, 409);
			this.conditionExpression.Multiline = true;
			this.conditionExpression.Name = "conditionExpression";
			this.conditionExpression.Size = new System.Drawing.Size(379, 62);
			this.conditionExpression.TabIndex = 6;
			// 
			// cmdInsert
			// 
			this.cmdInsert.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.cmdInsert.Location = new System.Drawing.Point(489, 504);
			this.cmdInsert.Name = "cmdInsert";
			this.cmdInsert.Size = new System.Drawing.Size(100, 23);
			this.cmdInsert.TabIndex = 9;
			this.cmdInsert.Text = "Einfügen";
			this.cmdInsert.UseVisualStyleBackColor = true;
			this.cmdInsert.Click += new System.EventHandler(this.cmdInsert_Click);
			// 
			// cmdClose
			// 
			this.cmdClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.cmdClose.DialogResult = System.Windows.Forms.DialogResult.Cancel;
			this.cmdClose.Location = new System.Drawing.Point(595, 504);
			this.cmdClose.Name = "cmdClose";
			this.cmdClose.Size = new System.Drawing.Size(100, 23);
			this.cmdClose.TabIndex = 10;
			this.cmdClose.Text = "Schließen";
			this.cmdClose.UseVisualStyleBackColor = true;
			// 
			// tableTree
			// 
			this.tableTree.CheckBoxes = true;
			this.tableTree.Location = new System.Drawing.Point(27, 25);
			this.tableTree.Name = "tableTree";
			this.tableTree.ShowPlusMinus = false;
			this.tableTree.ShowRootLines = false;
			this.tableTree.Size = new System.Drawing.Size(253, 446);
			this.tableTree.TabIndex = 11;
			this.tableTree.AfterCheck += new System.Windows.Forms.TreeViewEventHandler(this.tableTree_AfterCheck);
			// 
			// availableColumns
			// 
			this.availableColumns.FormattingEnabled = true;
			this.availableColumns.Location = new System.Drawing.Point(307, 25);
			this.availableColumns.Name = "availableColumns";
			this.availableColumns.Size = new System.Drawing.Size(379, 364);
			this.availableColumns.TabIndex = 12;
			// 
			// TableInsertForm
			// 
			this.AcceptButton = this.cmdInsert;
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(711, 543);
			this.Controls.Add(this.availableColumns);
			this.Controls.Add(this.tableTree);
			this.Controls.Add(this.cmdClose);
			this.Controls.Add(this.cmdInsert);
			this.Controls.Add(this.label3);
			this.Controls.Add(this.conditionExpression);
			this.Controls.Add(this.tableSearchText);
			this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.Name = "TableInsertForm";
			this.Padding = new System.Windows.Forms.Padding(13);
			this.ShowIcon = false;
			this.ShowInTaskbar = false;
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "Tabelle insert/edit";
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion
		private System.Windows.Forms.TextBox tableSearchText;
		private System.Windows.Forms.Label label3;
		private System.Windows.Forms.TextBox conditionExpression;
		private System.Windows.Forms.Button cmdInsert;
		private System.Windows.Forms.Button cmdClose;
		private System.Windows.Forms.TreeView tableTree;
		private System.Windows.Forms.CheckedListBox availableColumns;
	}
}