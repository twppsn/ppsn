using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml.Linq;
using Microsoft.Office.Tools.Excel;
using TecWare.PPSn;
using TecWare.PPSn.UI;
using Excel = Microsoft.Office.Interop.Excel;

namespace PPSnExcel.Wpf
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public partial class PpsReportSelectWindow : Window
	{
		private PpsEnvironment environment;

		public PpsReportSelectWindow()
		{
			InitializeComponent();

			this.environment = PpsEnvironment.GetEnvironment(this);

			//CommandBindings.Add(new CommandBinding(ApplicationCommands.Open, (sender, e) => OpenReport()));
			CommandBindings.Add(new CommandBinding(ApplicationCommands.Close, (sender, e) => Close()));
		} // ctor
	} // class PpsReportSelectWindow
}
