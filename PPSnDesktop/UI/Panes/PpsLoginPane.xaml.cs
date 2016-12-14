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

namespace TecWare.PPSn.UI.Panes
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public partial class PpsLoginPane : UserControl, IPpsWindowPane
	{
		event PropertyChangedEventHandler INotifyPropertyChanged.PropertyChanged { add { } remove { } }

		public PpsLoginPane()
		{
			InitializeComponent();
		} // ctor

		public void Dispose()
		{
		} // proc Dispose

		public Task LoadAsync(LuaTable arguments)
		{
			return Task.Delay(0);
		} // proc LoadAsync
		
		public Task<bool> UnloadAsync(bool? commit = default(bool?))
		{
			return Task.FromResult(true);
		} // func UnloadAsync

		public PpsWindowPaneCompareResult CompareArguments(LuaTable args)
		{
			throw new NotImplementedException();
		}

		public string Title { get { return "Login"; } }
		public object Control { get { return this; } }

		public bool IsDirty
		{
			get
			{
				throw new NotImplementedException();
			}
		}
		IPpsPWindowPaneControl IPpsWindowPane.PaneControl => null;
		public bool HasSideBar => false;
	} // class PpsLoginPane
}
