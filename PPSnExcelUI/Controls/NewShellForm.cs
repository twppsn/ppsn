using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TecWare.PPSn.Controls
{
	internal partial class NewShellForm : Form
	{
		public NewShellForm()
		{
			InitializeComponent();

			RefreshControls();
		} // ctor

		private void RefreshControls()
		{
			okButton.Enabled = ShellName.Length > 0 && ShellUri.Length > 0;
		} // proc RefreshControls

		private void nameText_TextChanged(object sender, EventArgs e)
			=> RefreshControls();

		private void uriText_TextChanged(object sender, EventArgs e)
			=> RefreshControls();

		private static string GetStringFromTextBox(TextBox textBox)
			=> textBox.Text != null ? textBox.Text.Trim() : String.Empty;

		public string ShellName => GetStringFromTextBox(nameText);
		public string ShellUri => GetStringFromTextBox(uriText);

		public static IPpsShellInfo CreateNew(IWin32Window owner)
		{
			using (var frm = new NewShellForm())
			{
				var factory = PpsShell.GetService<IPpsShellFactory>(true);

				while (true)
				{
					try
					{
						if (frm.ShowDialog() == DialogResult.Cancel)
							return null;

						var displayName = frm.ShellName;

						var newShell = factory.CreateNew(PpsShell.GetCleanShellName(displayName), displayName, new Uri(frm.ShellUri, UriKind.Absolute));
						if (newShell != null)
							return newShell;
					}
					catch (Exception ex)
					{
						PpsWinShell.ShowException(owner, PpsExceptionShowFlags.None, ex);
					}
				}
			}
		} // proc CreateNew
	} // NewShellForm
}
