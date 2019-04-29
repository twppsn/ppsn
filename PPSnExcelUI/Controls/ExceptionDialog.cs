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
using System.Windows.Forms;

namespace TecWare.PPSn.Controls
{
	internal partial class ExceptionDialog : Form
	{
		private Exception exceptionToShow = null;

		/// <summary></summary>
		public ExceptionDialog()
		{
			InitializeComponent();

			SetClientSizeCore(ClientSize.Width, panShort.Height);
		} // ctor

		public void SetData(string message, Exception e, bool showInTaskbar)
		{
			ShowInTaskbar = showInTaskbar;

			AcceptButton = cmdQuit;
			CancelButton = cmdQuit;

			exceptionToShow = e;
			lblMessage.Text = message ?? e.Message;
		} // proc DataToControls

		public void ShowDetail()
		{
			cmdDetails.Visible = false;

			FormBorderStyle = FormBorderStyle.Sizable;

			SuspendLayout();

			SetClientSizeCore(ClientSize.Width, panShort.Height + 300);
			ShowIcon = false;
			MaximizeBox = true;

			txtDetail.Visible = true;
			txtDetail.Text = exceptionToShow.ToString();
			txtDetail.Select(0, 0);
			ResumeLayout();
		} // proc ShowDetail

		#region -- Ereignisse ---------------------------------------------------------

		private void frmExceptionViewer_FormClosed(object sender, FormClosedEventArgs e)
		{
			if (DialogResult == DialogResult.Cancel && CancelButton != null)
				DialogResult = CancelButton.DialogResult;
		} // event frmExceptionViewer_FormClosed

		private void cmdDetails_Click(object sender, EventArgs e)
			=> ShowDetail();
		
		#endregion
	} // class frmExceptionViewer
}