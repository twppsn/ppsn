namespace TecWare.PPSn.Controls
{
	partial class ExceptionDialog
	{
		/// <summary>
		/// Erforderliche Designervariable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Verwendete Ressourcen bereinigen.
		/// </summary>
		/// <param name="disposing">True, wenn verwaltete Ressourcen gelöscht werden sollen; andernfalls False.</param>
		protected override void Dispose(bool disposing)
		{
			if (disposing && (components != null))
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Vom Windows Form-Designer generierter Code

		/// <summary>
		/// Erforderliche Methode für die Designerunterstützung.
		/// Der Inhalt der Methode darf nicht mit dem Code-Editor geändert werden.
		/// </summary>
		private void InitializeComponent()
		{
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ExceptionDialog));
			this.txtDetail = new System.Windows.Forms.TextBox();
			this.panShort = new System.Windows.Forms.Panel();
			this.pictureBox2 = new System.Windows.Forms.PictureBox();
			this.cmdDetails = new System.Windows.Forms.Button();
			this.cmdQuit = new System.Windows.Forms.Button();
			this.lblMessage = new System.Windows.Forms.Label();
			this.panShort.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.pictureBox2)).BeginInit();
			this.SuspendLayout();
			// 
			// txtDetail
			// 
			this.txtDetail.Cursor = System.Windows.Forms.Cursors.IBeam;
			this.txtDetail.Dock = System.Windows.Forms.DockStyle.Fill;
			this.txtDetail.Font = new System.Drawing.Font("Courier New", 8.25F);
			this.txtDetail.Location = new System.Drawing.Point(0, 87);
			this.txtDetail.Multiline = true;
			this.txtDetail.Name = "txtDetail";
			this.txtDetail.ReadOnly = true;
			this.txtDetail.ScrollBars = System.Windows.Forms.ScrollBars.Both;
			this.txtDetail.Size = new System.Drawing.Size(433, 60);
			this.txtDetail.TabIndex = 1;
			this.txtDetail.Visible = false;
			this.txtDetail.WordWrap = false;
			// 
			// panShort
			// 
			this.panShort.Controls.Add(this.pictureBox2);
			this.panShort.Controls.Add(this.cmdDetails);
			this.panShort.Controls.Add(this.cmdQuit);
			this.panShort.Controls.Add(this.lblMessage);
			this.panShort.Dock = System.Windows.Forms.DockStyle.Top;
			this.panShort.Location = new System.Drawing.Point(0, 0);
			this.panShort.Name = "panShort";
			this.panShort.Size = new System.Drawing.Size(433, 87);
			this.panShort.TabIndex = 0;
			// 
			// pictureBox2
			// 
			this.pictureBox2.Image = ((System.Drawing.Image)(resources.GetObject("pictureBox2.Image")));
			this.pictureBox2.ImeMode = System.Windows.Forms.ImeMode.NoControl;
			this.pictureBox2.Location = new System.Drawing.Point(9, 14);
			this.pictureBox2.Name = "pictureBox2";
			this.pictureBox2.Size = new System.Drawing.Size(32, 32);
			this.pictureBox2.SizeMode = System.Windows.Forms.PictureBoxSizeMode.AutoSize;
			this.pictureBox2.TabIndex = 10;
			this.pictureBox2.TabStop = false;
			// 
			// cmdDetails
			// 
			this.cmdDetails.Location = new System.Drawing.Point(9, 52);
			this.cmdDetails.Name = "cmdDetails";
			this.cmdDetails.Size = new System.Drawing.Size(75, 23);
			this.cmdDetails.TabIndex = 1;
			this.cmdDetails.Text = "Details";
			this.cmdDetails.UseVisualStyleBackColor = true;
			this.cmdDetails.Click += new System.EventHandler(this.cmdDetails_Click);
			// 
			// cmdQuit
			// 
			this.cmdQuit.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.cmdQuit.DialogResult = System.Windows.Forms.DialogResult.Abort;
			this.cmdQuit.Location = new System.Drawing.Point(346, 52);
			this.cmdQuit.Name = "cmdQuit";
			this.cmdQuit.Size = new System.Drawing.Size(75, 23);
			this.cmdQuit.TabIndex = 2;
			this.cmdQuit.Text = "OK";
			this.cmdQuit.UseVisualStyleBackColor = true;
			// 
			// lblMessage
			// 
			this.lblMessage.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.lblMessage.AutoEllipsis = true;
			this.lblMessage.Location = new System.Drawing.Point(47, 14);
			this.lblMessage.Name = "lblMessage";
			this.lblMessage.Size = new System.Drawing.Size(371, 32);
			this.lblMessage.TabIndex = 0;
			// 
			// ExceptionDialog
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(433, 147);
			this.Controls.Add(this.txtDetail);
			this.Controls.Add(this.panShort);
			this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.Name = "ExceptionDialog";
			this.ShowInTaskbar = false;
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "Fehler";
			this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.frmExceptionViewer_FormClosed);
			this.panShort.ResumeLayout(false);
			this.panShort.PerformLayout();
			((System.ComponentModel.ISupportInitialize)(this.pictureBox2)).EndInit();
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.TextBox txtDetail;
		private System.Windows.Forms.Panel panShort;
		private System.Windows.Forms.Button cmdDetails;
		private System.Windows.Forms.Button cmdQuit;
		private System.Windows.Forms.Label lblMessage;
		private System.Windows.Forms.PictureBox pictureBox2;
	}
}