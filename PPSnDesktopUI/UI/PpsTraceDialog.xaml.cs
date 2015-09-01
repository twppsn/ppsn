using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Neo.IronLua;

namespace TecWare.PPSn.UI
{
	///////////////////////////////////////////////////////////////////////////////
/// <summary></summary>
	public partial class PpsTraceDialog : Window
	{
		public PpsTraceDialog()
		{
			InitializeComponent();
		} // ctor

		public async Task LoadAsync(PpsEnvironment environment)
		{
			var pane = new PpsTracePane();
			DataContext = pane;
			await pane.LoadAsync(new LuaTable() {["Environment"] = environment });
		} // proc LoadAsync
	} // class PpsTraceDialog
}
