
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
			if (disposing)
				DisposeTrue();

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
			this.tbxExpression = new System.Windows.Forms.TextBox();
			this.dateTimeButton = new System.Windows.Forms.Button();
			this.definedNamesButton = new System.Windows.Forms.Button();
			this.fieldButton = new System.Windows.Forms.Button();
			this.panel1 = new System.Windows.Forms.Panel();
			this.panel1.SuspendLayout();
			this.SuspendLayout();
			// 
			// tbxExpression
			// 
			this.tbxExpression.BorderStyle = System.Windows.Forms.BorderStyle.None;
			this.tbxExpression.Dock = System.Windows.Forms.DockStyle.Fill;
			this.tbxExpression.Location = new System.Drawing.Point(3, 5);
			this.tbxExpression.Name = "tbxExpression";
			this.tbxExpression.Size = new System.Drawing.Size(121, 13);
			this.tbxExpression.TabIndex = 0;
			this.tbxExpression.WordWrap = false;
			this.tbxExpression.KeyDown += new System.Windows.Forms.KeyEventHandler(this.HandleTextBoxKeyDown);
			this.tbxExpression.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.HandleTextBoxKeyPress);
			// 
			// dateTimeButton
			// 
			this.dateTimeButton.Dock = System.Windows.Forms.DockStyle.Right;
			this.dateTimeButton.Location = new System.Drawing.Point(127, 0);
			this.dateTimeButton.Margin = new System.Windows.Forms.Padding(1);
			this.dateTimeButton.Name = "dateTimeButton";
			this.dateTimeButton.Size = new System.Drawing.Size(24, 24);
			this.dateTimeButton.TabIndex = 1;
			this.dateTimeButton.Text = "#";
			this.dateTimeButton.UseVisualStyleBackColor = true;
			this.dateTimeButton.Click += new System.EventHandler(this.HandleButtonClickEvent);
			// 
			// definedNamesButton
			// 
			this.definedNamesButton.Dock = System.Windows.Forms.DockStyle.Right;
			this.definedNamesButton.Location = new System.Drawing.Point(151, 0);
			this.definedNamesButton.Margin = new System.Windows.Forms.Padding(0, 1, 0, 1);
			this.definedNamesButton.Name = "definedNamesButton";
			this.definedNamesButton.Size = new System.Drawing.Size(24, 24);
			this.definedNamesButton.TabIndex = 2;
			this.definedNamesButton.Text = "$";
			this.definedNamesButton.UseVisualStyleBackColor = true;
			this.definedNamesButton.Click += new System.EventHandler(this.HandleButtonClickEvent);
			// 
			// fieldButton
			// 
			this.fieldButton.Dock = System.Windows.Forms.DockStyle.Right;
			this.fieldButton.Location = new System.Drawing.Point(175, 0);
			this.fieldButton.Margin = new System.Windows.Forms.Padding(1);
			this.fieldButton.Name = "fieldButton";
			this.fieldButton.Size = new System.Drawing.Size(24, 24);
			this.fieldButton.TabIndex = 3;
			this.fieldButton.Text = ":";
			this.fieldButton.UseVisualStyleBackColor = true;
			this.fieldButton.Click += new System.EventHandler(this.HandleButtonClickEvent);
			// 
			// panel1
			// 
			this.panel1.Controls.Add(this.tbxExpression);
			this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.panel1.Location = new System.Drawing.Point(0, 0);
			this.panel1.Name = "panel1";
			this.panel1.Padding = new System.Windows.Forms.Padding(3, 5, 3, 0);
			this.panel1.Size = new System.Drawing.Size(127, 24);
			this.panel1.TabIndex = 4;
			// 
			// PpsFilterControl
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
			this.Controls.Add(this.panel1);
			this.Controls.Add(this.dateTimeButton);
			this.Controls.Add(this.definedNamesButton);
			this.Controls.Add(this.fieldButton);
			this.Margin = new System.Windows.Forms.Padding(0);
			this.Name = "PpsFilterControl";
			this.Size = new System.Drawing.Size(199, 24);
			this.Enter += new System.EventHandler(this.HendleGetFocus);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.TextBox tbxExpression;
		private System.Windows.Forms.Button definedNamesButton;
		private System.Windows.Forms.Button dateTimeButton;
		private System.Windows.Forms.Button fieldButton;
		private System.Windows.Forms.Panel panel1;
	}
}
