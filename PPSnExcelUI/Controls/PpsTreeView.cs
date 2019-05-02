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
	public class PpsTreeView : TreeView
	{
		protected override void WndProc(ref Message m)
		{
			switch (m.Msg)
			{
				case 0x0203: // WM_LBUTTONDBLCLK
					// ignore double click on state aka checkbox
					var info = HitTest(NativeMethods.MakePoints(m.LParam));
					if (info.Location == TreeViewHitTestLocations.StateImage)
					{
						m.Result = IntPtr.Zero;
						return;
					}
					break;
			}
			base.WndProc(ref m);
		} // proc WndProc
	} // class PpsTreeView
}
