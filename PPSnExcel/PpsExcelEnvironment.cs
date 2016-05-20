using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using TecWare.PPSn;
using TecWare.PPSn.Data;

namespace PPSnExcel
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsExcelEnvironment :PpsEnvironment
	{
		private readonly ThisAddIn addIn;

		public PpsExcelEnvironment(PpsEnvironmentInfo info, ThisAddIn addIn)
			: base(info, addIn.App.Resources)
		{
			this.addIn = addIn;
		} // ctor

		protected override bool ShowLoginDialog(PpsClientLogin clientLogin)
			=> addIn.App.Dispatcher.Invoke(() => clientLogin.ShowWindowsLogin(new IntPtr(addIn.Application.Application.Hwnd)));
	} // class PpsExcelEnvironment
}
