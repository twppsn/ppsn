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
using System.ComponentModel;
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
using System.Windows.Navigation;
using System.Windows.Shapes;
using Neo.IronLua;

namespace TecWare.PPSn.UI
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	internal partial class PpsTracePane : UserControl, IPpsWindowPane
	{
		public event PropertyChangedEventHandler PropertyChanged { add { } remove { } }

		public PpsTracePane()
		{
			InitializeComponent();
		} // ctor

		public void Dispose()
		{
		} // proc Dispose

		public Task LoadAsync(LuaTable args)
		{
			var environment = (args["Environment"] as PpsEnvironment) ?? PpsEnvironment.GetEnvironment(this);
			DataContext = environment;

			return Task.CompletedTask;
		} // proc LoadAsync

		public Task<bool> UnloadAsync(bool? commit = default(bool?))
		{
			return Task.FromResult(true);
		} // func UnloadAsync

		public PpsWindowPaneCompareResult CompareArguments(LuaTable args) => PpsWindowPaneCompareResult.Same;

		public string Title => "Anwendungsereignisse";
		public string SubTitle => "System";
		public object Control => this;
		public IPpsPWindowPaneControl PaneControl => null;
		public bool IsDirty => false;
		public bool HasSideBar => false;
	} // class PpsTracePane

	#region -- class TraceItemTemplateSelector ------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	internal sealed class TraceItemTemplateSelector : DataTemplateSelector
	{
		public override DataTemplate SelectTemplate(object item, DependencyObject container)
		{
			var r = (DataTemplate)null;
			var resources = container as ContentPresenter;
			if (item != null && resources != null)
			{
				if (item is PpsExceptionItem)
					r = ExceptionTemplate;
				else if (item is PpsTraceItem)
					r = TraceItemTemplate;
				else if (item is PpsTextItem)
					r = TextItemTemplate;
			}
			return r ?? NullTemplate;
		} // proc SelectTemplate

		/// <summary>Template for the exception items</summary>
		public DataTemplate NullTemplate { get; set; }
		/// <summary>Template for the exception items</summary>
		public DataTemplate ExceptionTemplate { get; set; }
		/// <summary></summary>
		public DataTemplate TraceItemTemplate { get; set; }
		/// <summary></summary>
		public DataTemplate TextItemTemplate { get; set; }
	} // class TraceItemTemplateSelector

	#endregion
}
