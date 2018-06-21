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
			this.availTablesList = new System.Windows.Forms.ListBox();
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.availableColumns = new System.Windows.Forms.ListBox();
			this.selectedColumns = new System.Windows.Forms.ListBox();
			this.button1 = new System.Windows.Forms.Button();
			this.button2 = new System.Windows.Forms.Button();
			this.selectedTables = new System.Windows.Forms.ListBox();
			this.textBox2 = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.label2 = new System.Windows.Forms.Label();
			this.label3 = new System.Windows.Forms.Label();
			this.conditionExpression = new System.Windows.Forms.TextBox();
			this.cmdInsert = new System.Windows.Forms.Button();
			this.cmdClose = new System.Windows.Forms.Button();
			this.SuspendLayout();
			// 
			// availTablesList
			// 
			this.availTablesList.FormattingEnabled = true;
			this.availTablesList.Location = new System.Drawing.Point(27, 25);
			this.availTablesList.Name = "availTablesList";
			this.availTablesList.Size = new System.Drawing.Size(253, 446);
			this.availTablesList.TabIndex = 0;
			// 
			// textBox1
			// 
			this.textBox1.Location = new System.Drawing.Point(121, 477);
			this.textBox1.Name = "textBox1";
			this.textBox1.Size = new System.Drawing.Size(159, 20);
			this.textBox1.TabIndex = 1;
			// 
			// availableColumns
			// 
			this.availableColumns.FormattingEnabled = true;
			this.availableColumns.Location = new System.Drawing.Point(331, 198);
			this.availableColumns.Name = "availableColumns";
			this.availableColumns.Size = new System.Drawing.Size(187, 199);
			this.availableColumns.TabIndex = 2;
			// 
			// selectedColumns
			// 
			this.selectedColumns.FormattingEnabled = true;
			this.selectedColumns.Location = new System.Drawing.Point(586, 181);
			this.selectedColumns.Name = "selectedColumns";
			this.selectedColumns.Size = new System.Drawing.Size(203, 199);
			this.selectedColumns.TabIndex = 3;
			// 
			// button1
			// 
			this.button1.Location = new System.Drawing.Point(539, 225);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(41, 23);
			this.button1.TabIndex = 4;
			this.button1.Text = "<";
			this.button1.UseVisualStyleBackColor = true;
			// 
			// button2
			// 
			this.button2.Location = new System.Drawing.Point(539, 254);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(41, 23);
			this.button2.TabIndex = 4;
			this.button2.Text = ">";
			this.button2.UseVisualStyleBackColor = true;
			// 
			// selectedTables
			// 
			this.selectedTables.FormattingEnabled = true;
			this.selectedTables.Location = new System.Drawing.Point(322, 40);
			this.selectedTables.Name = "selectedTables";
			this.selectedTables.Size = new System.Drawing.Size(458, 56);
			this.selectedTables.TabIndex = 5;
			// 
			// textBox2
			// 
			this.textBox2.Location = new System.Drawing.Point(384, 103);
			this.textBox2.Name = "textBox2";
			this.textBox2.Size = new System.Drawing.Size(396, 20);
			this.textBox2.TabIndex = 6;
			// 
			// label1
			// 
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(335, 23);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(34, 13);
			this.label1.TabIndex = 7;
			this.label1.Text = "Joins:";
			// 
			// label2
			// 
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(344, 178);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(50, 13);
			this.label2.TabIndex = 7;
			this.label2.Text = "Columns:";
			// 
			// label3
			// 
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(319, 425);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(73, 13);
			this.label3.TabIndex = 8;
			this.label3.Text = "Bedingungen:";
			// 
			// conditionExpression
			// 
			this.conditionExpression.Location = new System.Drawing.Point(385, 409);
			this.conditionExpression.Multiline = true;
			this.conditionExpression.Name = "conditionExpression";
			this.conditionExpression.Size = new System.Drawing.Size(379, 62);
			this.conditionExpression.TabIndex = 6;
			// 
			// cmdInsert
			// 
			this.cmdInsert.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.cmdInsert.DialogResult = System.Windows.Forms.DialogResult.Cancel;
			this.cmdInsert.Location = new System.Drawing.Point(604, 515);
			this.cmdInsert.Name = "cmdInsert";
			this.cmdInsert.Size = new System.Drawing.Size(100, 23);
			this.cmdInsert.TabIndex = 9;
			this.cmdInsert.Text = "Einfügen";
			this.cmdInsert.UseVisualStyleBackColor = true;
			// 
			// cmdClose
			// 
			this.cmdClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.cmdClose.DialogResult = System.Windows.Forms.DialogResult.Cancel;
			this.cmdClose.Location = new System.Drawing.Point(710, 515);
			this.cmdClose.Name = "cmdClose";
			this.cmdClose.Size = new System.Drawing.Size(100, 23);
			this.cmdClose.TabIndex = 10;
			this.cmdClose.Text = "Schließen";
			this.cmdClose.UseVisualStyleBackColor = true;
			// 
			// TableInsertForm
			// 
			this.AcceptButton = this.cmdInsert;
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(826, 554);
			this.Controls.Add(this.cmdClose);
			this.Controls.Add(this.cmdInsert);
			this.Controls.Add(this.label3);
			this.Controls.Add(this.label2);
			this.Controls.Add(this.label1);
			this.Controls.Add(this.conditionExpression);
			this.Controls.Add(this.textBox2);
			this.Controls.Add(this.selectedTables);
			this.Controls.Add(this.button2);
			this.Controls.Add(this.button1);
			this.Controls.Add(this.selectedColumns);
			this.Controls.Add(this.availableColumns);
			this.Controls.Add(this.textBox1);
			this.Controls.Add(this.availTablesList);
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

		private System.Windows.Forms.ListBox availTablesList;
		private System.Windows.Forms.TextBox textBox1;
		private System.Windows.Forms.ListBox availableColumns;
		private System.Windows.Forms.ListBox selectedColumns;
		private System.Windows.Forms.Button button1;
		private System.Windows.Forms.Button button2;
		private System.Windows.Forms.ListBox selectedTables;
		private System.Windows.Forms.TextBox textBox2;
		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.Label label2;
		private System.Windows.Forms.Label label3;
		private System.Windows.Forms.TextBox conditionExpression;
		private System.Windows.Forms.Button cmdInsert;
		private System.Windows.Forms.Button cmdClose;
	}
}