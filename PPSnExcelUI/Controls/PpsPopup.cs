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
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace TecWare.PPSn.Controls
{
	#region -- class PpsPopup ---------------------------------------------------------

	/// <summary>Represents a pop-up window.</summary>
	internal partial class PpsPopup : ToolStripDropDown
	{
		private readonly ToolStripControlHost host;

		#region -- Ctor/Dtor ----------------------------------------------------------

		public PpsPopup(Control content)
		{
			if (content == null)
				throw new ArgumentNullException(nameof(content));

			InitializeComponent();


			Padding = Padding.Empty;
			Margin = Padding.Empty;

			host = new ToolStripControlHost(content)
			{
				Padding = Padding.Empty,
				Margin = Padding.Empty
			};

			Items.Add(host);
		} // ctor

		private void DisposeTrue()
			=> host.Dispose();

		protected override CreateParams CreateParams
		{
			get
			{
				var cp = base.CreateParams;
				cp.ExStyle |= 0x08000000; // NativeMethods.WS_EX_NOACTIVATE;
				return cp;
			}
		} // prop CreateParams

		#endregion

		protected override bool ProcessDialogKey(Keys keyData)
		{
			var ret = base.ProcessDialogKey(keyData);
			if (!ret && (keyData == Keys.Tab || keyData == (Keys.Tab | Keys.Shift)))
			{
				var doBackward = (keyData & Keys.Shift) == Keys.Shift;
				host.Control.SelectNextControl(null, !doBackward, true, true, true);
			}
			return ret;
		} // func ProcessDialogKey

		public void Show(Control control, Rectangle area)
		{
			if (control == null)
				throw new ArgumentNullException(nameof(control));

			var location = control.PointToScreen(new Point(area.Left, area.Top + area.Height));
			var screen = Screen.FromControl(control).WorkingArea;
			if (location.X + Size.Width > (screen.Left + screen.Width))
				location.X = screen.Left + screen.Width - Size.Width;
			if (location.Y + Size.Height > (screen.Top + screen.Height))
				location.Y -= Size.Height + area.Height;

			location = control.PointToClient(location);
			Show(control, location, ToolStripDropDownDirection.BelowRight);
		} // proc Show

		public void Show(Control control)
			=> Show(control, control.ClientRectangle);

		protected override void WndProc(ref Message m)
		{
			switch (m.Msg)
			{
				case 0x001C: // WM_ACTIVATE
				case 0x0086: // WM_NCACTIVATE
					if (host != null && host.Control != null)
						host.Control.Focus();
					break;
			}
			base.WndProc(ref m);
		} // proc WndProc
	} // class PpsPopup

	#endregion
}
