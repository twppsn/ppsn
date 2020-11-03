namespace TecWare.PPSn.Controls
{
	partial class TableInsertFormEx
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
			System.Windows.Forms.Button cancelButton;
			this.refreshButton = new System.Windows.Forms.Button();
			this.viewsText = new System.Windows.Forms.TextBox();
			this.filterText = new System.Windows.Forms.TextBox();
			this.columnsText = new System.Windows.Forms.TextBox();
			this.displayNameText = new System.Windows.Forms.TextBox();
			cancelButton = new System.Windows.Forms.Button();
			this.SuspendLayout();
			// 
			// cancelButton
			// 
			cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
			cancelButton.Location = new System.Drawing.Point(1032, 536);
			cancelButton.Name = "cancelButton";
			cancelButton.Size = new System.Drawing.Size(100, 23);
			cancelButton.TabIndex = 5;
			cancelButton.Text = "Abbrechen";
			cancelButton.UseVisualStyleBackColor = true;
			// 
			// refreshButton
			// 
			this.refreshButton.Location = new System.Drawing.Point(926, 536);
			this.refreshButton.Name = "refreshButton";
			this.refreshButton.Size = new System.Drawing.Size(100, 23);
			this.refreshButton.TabIndex = 4;
			this.refreshButton.UseVisualStyleBackColor = true;
			this.refreshButton.Click += new System.EventHandler(this.refreshButton_Click);
			// 
			// viewsText
			// 
			this.viewsText.AcceptsReturn = true;
			this.viewsText.AcceptsTab = true;
			this.viewsText.Font = new System.Drawing.Font("Consolas", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.viewsText.Location = new System.Drawing.Point(16, 48);
			this.viewsText.Multiline = true;
			this.viewsText.Name = "viewsText";
			this.viewsText.Size = new System.Drawing.Size(716, 222);
			this.viewsText.TabIndex = 1;
			this.viewsText.WordWrap = false;
			// 
			// filterText
			// 
			this.filterText.AcceptsReturn = true;
			this.filterText.AcceptsTab = true;
			this.filterText.Font = new System.Drawing.Font("Consolas", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.filterText.Location = new System.Drawing.Point(16, 276);
			this.filterText.Multiline = true;
			this.filterText.Name = "filterText";
			this.filterText.Size = new System.Drawing.Size(716, 254);
			this.filterText.TabIndex = 2;
			this.filterText.WordWrap = false;
			// 
			// columnsText
			// 
			this.columnsText.AcceptsReturn = true;
			this.columnsText.AcceptsTab = true;
			this.columnsText.Font = new System.Drawing.Font("Consolas", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.columnsText.Location = new System.Drawing.Point(738, 16);
			this.columnsText.Multiline = true;
			this.columnsText.Name = "columnsText";
			this.columnsText.Size = new System.Drawing.Size(394, 514);
			this.columnsText.TabIndex = 3;
			this.columnsText.WordWrap = false;
			// 
			// displayNameText
			// 
			this.displayNameText.Font = new System.Drawing.Font("Consolas", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.displayNameText.Location = new System.Drawing.Point(16, 16);
			this.displayNameText.Name = "displayNameText";
			this.displayNameText.Size = new System.Drawing.Size(716, 26);
			this.displayNameText.TabIndex = 0;
			// 
			// TableInsertFormEx
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.CancelButton = cancelButton;
			this.ClientSize = new System.Drawing.Size(1148, 575);
			this.Controls.Add(this.displayNameText);
			this.Controls.Add(this.columnsText);
			this.Controls.Add(this.filterText);
			this.Controls.Add(this.viewsText);
			this.Controls.Add(cancelButton);
			this.Controls.Add(this.refreshButton);
			this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.Name = "TableInsertFormEx";
			this.Padding = new System.Windows.Forms.Padding(13);
			this.ShowIcon = false;
			this.ShowInTaskbar = false;
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.Button refreshButton;
		private System.Windows.Forms.TextBox viewsText;
		private System.Windows.Forms.TextBox filterText;
		private System.Windows.Forms.TextBox columnsText;
		private System.Windows.Forms.TextBox displayNameText;
	}
}