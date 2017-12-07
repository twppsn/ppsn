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
