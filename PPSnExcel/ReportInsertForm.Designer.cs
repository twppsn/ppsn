namespace PPSnExcel
{
	partial class ReportInsertForm
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
			this.txtFilter = new System.Windows.Forms.TextBox();
			this.cmdOk = new System.Windows.Forms.Button();
			this.cmdClose = new System.Windows.Forms.Button();
			this.dv = new System.Windows.Forms.DataGridView();
			this.colText = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.bsReports = new System.Windows.Forms.BindingSource(this.components);
			((System.ComponentModel.ISupportInitialize)(this.dv)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.bsReports)).BeginInit();
			this.SuspendLayout();
			// 
			// txtFilter
			// 
			this.txtFilter.Location = new System.Drawing.Point(23, 578);
			this.txtFilter.Margin = new System.Windows.Forms.Padding(0);
			this.txtFilter.Name = "txtFilter";
			this.txtFilter.Size = new System.Drawing.Size(221, 20);
			this.txtFilter.TabIndex = 1;
			this.txtFilter.TextChanged += new System.EventHandler(this.txtFilter_TextChanged);
			// 
			// cmdOk
			// 
			this.cmdOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.cmdOk.Location = new System.Drawing.Point(604, 576);
			this.cmdOk.Margin = new System.Windows.Forms.Padding(6, 0, 0, 0);
			this.cmdOk.Name = "cmdOk";
			this.cmdOk.Size = new System.Drawing.Size(100, 23);
			this.cmdOk.TabIndex = 2;
			this.cmdOk.Text = "Einfügen";
			this.cmdOk.UseVisualStyleBackColor = true;
			// 
			// cmdClose
			// 
			this.cmdClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.cmdClose.DialogResult = System.Windows.Forms.DialogResult.Cancel;
			this.cmdClose.Location = new System.Drawing.Point(710, 576);
			this.cmdClose.Margin = new System.Windows.Forms.Padding(6, 0, 0, 0);
			this.cmdClose.Name = "cmdClose";
			this.cmdClose.Size = new System.Drawing.Size(100, 23);
			this.cmdClose.TabIndex = 3;
			this.cmdClose.Text = "Schließen";
			this.cmdClose.UseVisualStyleBackColor = true;
			// 
			// dv
			// 
			this.dv.AllowUserToAddRows = false;
			this.dv.AllowUserToDeleteRows = false;
			this.dv.AllowUserToResizeColumns = false;
			this.dv.AllowUserToResizeRows = false;
			this.dv.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.dv.AutoGenerateColumns = false;
			this.dv.BackgroundColor = System.Drawing.SystemColors.Window;
			this.dv.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
			this.dv.ColumnHeadersVisible = false;
			this.dv.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colText});
			this.dv.DataSource = this.bsReports;
			this.dv.Location = new System.Drawing.Point(23, 19);
			this.dv.Margin = new System.Windows.Forms.Padding(0, 0, 0, 12);
			this.dv.MultiSelect = false;
			this.dv.Name = "dv";
			this.dv.ReadOnly = true;
			this.dv.RowHeadersVisible = false;
			this.dv.RowTemplate.Height = 38;
			this.dv.RowTemplate.ReadOnly = true;
			this.dv.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
			this.dv.ShowCellErrors = false;
			this.dv.ShowCellToolTips = false;
			this.dv.ShowEditingIcon = false;
			this.dv.ShowRowErrors = false;
			this.dv.Size = new System.Drawing.Size(787, 545);
			this.dv.TabIndex = 0;
			this.dv.CellDoubleClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dv_CellDoubleClick);
			this.dv.CellPainting += new System.Windows.Forms.DataGridViewCellPaintingEventHandler(this.dv_CellPainting);
			this.dv.SelectionChanged += new System.EventHandler(this.dv_SelectionChanged);
			// 
			// colText
			// 
			this.colText.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
			this.colText.HeaderText = "Text";
			this.colText.Name = "colText";
			this.colText.ReadOnly = true;
			// 
			// bsReports
			// 
			this.bsReports.AllowNew = false;
			// 
			// ReportInsertForm
			// 
			this.AcceptButton = this.cmdOk;
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.CancelButton = this.cmdClose;
			this.ClientSize = new System.Drawing.Size(826, 615);
			this.Controls.Add(this.dv);
			this.Controls.Add(this.cmdClose);
			this.Controls.Add(this.cmdOk);
			this.Controls.Add(this.txtFilter);
			this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.Name = "ReportInsertForm";
			this.Padding = new System.Windows.Forms.Padding(16);
			this.ShowIcon = false;
			this.ShowInTaskbar = false;
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "Report einfügen";
			((System.ComponentModel.ISupportInitialize)(this.dv)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.bsReports)).EndInit();
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.TextBox txtFilter;
		private System.Windows.Forms.Button cmdOk;
		private System.Windows.Forms.Button cmdClose;
		private System.Windows.Forms.BindingSource bsReports;
		private System.Windows.Forms.DataGridView dv;
		private System.Windows.Forms.DataGridViewTextBoxColumn colText;
	}
}