namespace TecWare.PPSn.Export
{
	partial class MainWindow
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
			this.joinTextBox = new System.Windows.Forms.TextBox();
			this.cmdEdit = new System.Windows.Forms.Button();
			this.uriLabel = new System.Windows.Forms.Label();
			this.columnsTextBox = new System.Windows.Forms.TextBox();
			this.filterTextBox = new System.Windows.Forms.TextBox();
			this.uriText = new System.Windows.Forms.TextBox();
			this.splitSelectPanel = new System.Windows.Forms.SplitContainer();
			this.panUri = new System.Windows.Forms.Panel();
			this.columnAliasCheck = new System.Windows.Forms.CheckBox();
			this.splitContainer1 = new System.Windows.Forms.SplitContainer();
			((System.ComponentModel.ISupportInitialize)(this.splitSelectPanel)).BeginInit();
			this.splitSelectPanel.Panel1.SuspendLayout();
			this.splitSelectPanel.Panel2.SuspendLayout();
			this.splitSelectPanel.SuspendLayout();
			this.panUri.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
			this.splitContainer1.Panel1.SuspendLayout();
			this.splitContainer1.Panel2.SuspendLayout();
			this.splitContainer1.SuspendLayout();
			this.SuspendLayout();
			// 
			// joinTextBox
			// 
			this.joinTextBox.Dock = System.Windows.Forms.DockStyle.Fill;
			this.joinTextBox.Font = new System.Drawing.Font("Consolas", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.joinTextBox.Location = new System.Drawing.Point(0, 0);
			this.joinTextBox.Multiline = true;
			this.joinTextBox.Name = "joinTextBox";
			this.joinTextBox.Size = new System.Drawing.Size(390, 272);
			this.joinTextBox.TabIndex = 0;
			// 
			// cmdEdit
			// 
			this.cmdEdit.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.cmdEdit.Location = new System.Drawing.Point(586, 14);
			this.cmdEdit.Name = "cmdEdit";
			this.cmdEdit.Size = new System.Drawing.Size(75, 23);
			this.cmdEdit.TabIndex = 1;
			this.cmdEdit.Text = "Edit";
			this.cmdEdit.UseVisualStyleBackColor = true;
			this.cmdEdit.Click += new System.EventHandler(this.button1_Click);
			// 
			// uriLabel
			// 
			this.uriLabel.AutoSize = true;
			this.uriLabel.Location = new System.Drawing.Point(3, 16);
			this.uriLabel.Name = "uriLabel";
			this.uriLabel.Size = new System.Drawing.Size(23, 13);
			this.uriLabel.TabIndex = 2;
			this.uriLabel.Text = "Uri:";
			// 
			// columnsTextBox
			// 
			this.columnsTextBox.Dock = System.Windows.Forms.DockStyle.Fill;
			this.columnsTextBox.Font = new System.Drawing.Font("Consolas", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.columnsTextBox.Location = new System.Drawing.Point(0, 0);
			this.columnsTextBox.Multiline = true;
			this.columnsTextBox.Name = "columnsTextBox";
			this.columnsTextBox.Size = new System.Drawing.Size(267, 372);
			this.columnsTextBox.TabIndex = 0;
			// 
			// filterTextBox
			// 
			this.filterTextBox.Dock = System.Windows.Forms.DockStyle.Fill;
			this.filterTextBox.Font = new System.Drawing.Font("Consolas", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.filterTextBox.Location = new System.Drawing.Point(0, 0);
			this.filterTextBox.Multiline = true;
			this.filterTextBox.Name = "filterTextBox";
			this.filterTextBox.Size = new System.Drawing.Size(390, 96);
			this.filterTextBox.TabIndex = 0;
			// 
			// uriText
			// 
			this.uriText.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.uriText.Font = new System.Drawing.Font("Consolas", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.uriText.Location = new System.Drawing.Point(32, 16);
			this.uriText.Name = "uriText";
			this.uriText.ReadOnly = true;
			this.uriText.Size = new System.Drawing.Size(548, 22);
			this.uriText.TabIndex = 4;
			// 
			// splitSelectPanel
			// 
			this.splitSelectPanel.Dock = System.Windows.Forms.DockStyle.Fill;
			this.splitSelectPanel.Location = new System.Drawing.Point(16, 16);
			this.splitSelectPanel.Name = "splitSelectPanel";
			// 
			// splitSelectPanel.Panel1
			// 
			this.splitSelectPanel.Panel1.Controls.Add(this.splitContainer1);
			// 
			// splitSelectPanel.Panel2
			// 
			this.splitSelectPanel.Panel2.Controls.Add(this.columnsTextBox);
			this.splitSelectPanel.Size = new System.Drawing.Size(661, 372);
			this.splitSelectPanel.SplitterDistance = 390;
			this.splitSelectPanel.TabIndex = 5;
			// 
			// panUri
			// 
			this.panUri.Controls.Add(this.columnAliasCheck);
			this.panUri.Controls.Add(this.uriText);
			this.panUri.Controls.Add(this.cmdEdit);
			this.panUri.Controls.Add(this.uriLabel);
			this.panUri.Dock = System.Windows.Forms.DockStyle.Bottom;
			this.panUri.Location = new System.Drawing.Point(16, 388);
			this.panUri.Name = "panUri";
			this.panUri.Size = new System.Drawing.Size(661, 63);
			this.panUri.TabIndex = 7;
			// 
			// columnAliasCheck
			// 
			this.columnAliasCheck.AutoSize = true;
			this.columnAliasCheck.Checked = true;
			this.columnAliasCheck.CheckState = System.Windows.Forms.CheckState.Checked;
			this.columnAliasCheck.Location = new System.Drawing.Point(32, 44);
			this.columnAliasCheck.Name = "columnAliasCheck";
			this.columnAliasCheck.Size = new System.Drawing.Size(80, 17);
			this.columnAliasCheck.TabIndex = 5;
			this.columnAliasCheck.Text = "Export alias";
			this.columnAliasCheck.UseVisualStyleBackColor = true;
			this.columnAliasCheck.CheckedChanged += new System.EventHandler(this.columnAliasCheck_CheckedChanged);
			// 
			// splitContainer1
			// 
			this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.splitContainer1.Location = new System.Drawing.Point(0, 0);
			this.splitContainer1.Name = "splitContainer1";
			this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
			// 
			// splitContainer1.Panel1
			// 
			this.splitContainer1.Panel1.Controls.Add(this.joinTextBox);
			// 
			// splitContainer1.Panel2
			// 
			this.splitContainer1.Panel2.Controls.Add(this.filterTextBox);
			this.splitContainer1.Size = new System.Drawing.Size(390, 372);
			this.splitContainer1.SplitterDistance = 272;
			this.splitContainer1.TabIndex = 1;
			// 
			// MainWindow
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(693, 467);
			this.Controls.Add(this.splitSelectPanel);
			this.Controls.Add(this.panUri);
			this.Name = "MainWindow";
			this.Padding = new System.Windows.Forms.Padding(16);
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "PPSn View Builder";
			this.splitSelectPanel.Panel1.ResumeLayout(false);
			this.splitSelectPanel.Panel2.ResumeLayout(false);
			this.splitSelectPanel.Panel2.PerformLayout();
			((System.ComponentModel.ISupportInitialize)(this.splitSelectPanel)).EndInit();
			this.splitSelectPanel.ResumeLayout(false);
			this.panUri.ResumeLayout(false);
			this.panUri.PerformLayout();
			this.splitContainer1.Panel1.ResumeLayout(false);
			this.splitContainer1.Panel1.PerformLayout();
			this.splitContainer1.Panel2.ResumeLayout(false);
			this.splitContainer1.Panel2.PerformLayout();
			((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
			this.splitContainer1.ResumeLayout(false);
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.TextBox joinTextBox;
		private System.Windows.Forms.Button cmdEdit;
		private System.Windows.Forms.Label uriLabel;
		private System.Windows.Forms.TextBox columnsTextBox;
		private System.Windows.Forms.TextBox filterTextBox;
		private System.Windows.Forms.TextBox uriText;
		private System.Windows.Forms.SplitContainer splitSelectPanel;
		private System.Windows.Forms.Panel panUri;
		private System.Windows.Forms.CheckBox columnAliasCheck;
		private System.Windows.Forms.SplitContainer splitContainer1;
	}
}

