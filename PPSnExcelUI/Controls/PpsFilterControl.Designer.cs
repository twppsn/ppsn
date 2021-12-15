
namespace TecWare.PPSn.Controls
{
	partial class PpsFilterControl
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

		#region Component Designer generated code

		/// <summary> 
		/// Required method for Designer support - do not modify 
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.tblLayout = new System.Windows.Forms.TableLayoutPanel();
			this.tbxExpression = new System.Windows.Forms.TextBox();
			this.btnDatetimePicker = new System.Windows.Forms.Button();
			this.btnDefinedNames = new System.Windows.Forms.Button();
			this.btnField = new System.Windows.Forms.Button();
			this.tblLayout.SuspendLayout();
			this.SuspendLayout();
			// 
			// tblLayout
			// 
			this.tblLayout.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
			this.tblLayout.BackColor = System.Drawing.Color.Transparent;
			this.tblLayout.ColumnCount = 4;
			this.tblLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
			this.tblLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
			this.tblLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
			this.tblLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
			this.tblLayout.Controls.Add(this.tbxExpression, 0, 0);
			this.tblLayout.Controls.Add(this.btnDatetimePicker, 1, 0);
			this.tblLayout.Controls.Add(this.btnDefinedNames, 2, 0);
			this.tblLayout.Controls.Add(this.btnField, 3, 0);
			this.tblLayout.Dock = System.Windows.Forms.DockStyle.Fill;
			this.tblLayout.Location = new System.Drawing.Point(0, 0);
			this.tblLayout.Name = "tblLayout";
			this.tblLayout.RowCount = 1;
			this.tblLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
			this.tblLayout.Size = new System.Drawing.Size(199, 24);
			this.tblLayout.TabIndex = 0;
			this.tblLayout.Enter += new System.EventHandler(this.HendleGetFocus);
			// 
			// tbxExpression
			// 
			this.tbxExpression.BorderStyle = System.Windows.Forms.BorderStyle.None;
			this.tbxExpression.Dock = System.Windows.Forms.DockStyle.Fill;
			this.tbxExpression.Location = new System.Drawing.Point(3, 4);
			this.tbxExpression.Margin = new System.Windows.Forms.Padding(3, 4, 1, 1);
			this.tbxExpression.Name = "tbxExpression";
			this.tbxExpression.Size = new System.Drawing.Size(135, 13);
			this.tbxExpression.TabIndex = 0;
			this.tbxExpression.WordWrap = false;
			this.tbxExpression.KeyDown += new System.Windows.Forms.KeyEventHandler(this.HandleTextBoxKeyDown);
			this.tbxExpression.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.HandleTextBoxKeyPress);
			// 
			// btnDatetimePicker
			// 
			this.btnDatetimePicker.Location = new System.Drawing.Point(139, 1);
			this.btnDatetimePicker.Margin = new System.Windows.Forms.Padding(0, 1, 0, 1);
			this.btnDatetimePicker.Name = "btnDatetimePicker";
			this.btnDatetimePicker.Size = new System.Drawing.Size(20, 22);
			this.btnDatetimePicker.TabIndex = 1;
			this.btnDatetimePicker.Text = "#";
			this.btnDatetimePicker.UseVisualStyleBackColor = true;
			this.btnDatetimePicker.Click += new System.EventHandler(this.HandleButtonClickEvent);
			// 
			// btnDefinedNames
			// 
			this.btnDefinedNames.Location = new System.Drawing.Point(159, 1);
			this.btnDefinedNames.Margin = new System.Windows.Forms.Padding(0, 1, 0, 1);
			this.btnDefinedNames.Name = "btnDefinedNames";
			this.btnDefinedNames.Size = new System.Drawing.Size(20, 22);
			this.btnDefinedNames.TabIndex = 2;
			this.btnDefinedNames.Text = "$";
			this.btnDefinedNames.UseVisualStyleBackColor = true;
			this.btnDefinedNames.Click += new System.EventHandler(this.HandleButtonClickEvent);
			// 
			// btnField
			// 
			this.btnField.Location = new System.Drawing.Point(179, 1);
			this.btnField.Margin = new System.Windows.Forms.Padding(0, 1, 0, 1);
			this.btnField.Name = "btnField";
			this.btnField.Size = new System.Drawing.Size(20, 22);
			this.btnField.TabIndex = 3;
			this.btnField.Text = ":";
			this.btnField.UseVisualStyleBackColor = true;
			this.btnField.Click += new System.EventHandler(this.HandleButtonClickEvent);
			// 
			// PpsFilterControl
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
			this.Controls.Add(this.tblLayout);
			this.Margin = new System.Windows.Forms.Padding(0);
			this.Name = "PpsFilterControl";
			this.Size = new System.Drawing.Size(199, 24);
			this.tblLayout.ResumeLayout(false);
			this.tblLayout.PerformLayout();
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.TableLayoutPanel tblLayout;
		private System.Windows.Forms.TextBox tbxExpression;
		private System.Windows.Forms.Button btnDefinedNames;
		private System.Windows.Forms.Button btnDatetimePicker;
		private System.Windows.Forms.Button btnField;
	}
}
