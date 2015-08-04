using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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
using TecWare.DES.Stuff;
using System.Configuration;

namespace TecWare.PPSn.UI
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public partial class PpsMainWindow : PpsWindow
	{
		private readonly static DependencyPropertyKey CurrentPaneKey = DependencyProperty.RegisterReadOnly("CurrentPane", typeof(IPpsWindowPane), typeof(PpsMainWindow), new PropertyMetadata(null));
		private readonly static DependencyProperty CurrentPaneProperty = CurrentPaneKey.DependencyProperty;

		private int windowIndex = -1;
		private PpsWindowApplicationSettings settings;

		public PpsMainWindow(int windowIndex)
		{
			this.windowIndex = windowIndex;

			InitializeComponent();

			// initialize settings
			settings = new PpsWindowApplicationSettings(this, "main" + windowIndex.ToString());

			// set basic command bindings
			CommandBindings.Add(
				new CommandBinding(PpsWindow.LoginCommand,
					async (sender, e) =>  await StartLoginAsync(),
					(sender, e) => e.CanExecute = !Environment.IsAuthentificated
				)
			);
			
			this.DataContext = this;

			RefreshTitle();
			Trace.TraceInformation("MainWindow[{0}] created.", windowIndex);
		} // ctor

		private async Task StartLoginAsync()
		{
			var  tmp = @"C:\Projects\PPSnOS\twppsn\PPSnWpf\PPSnDesktopPG\Example\TestWpfGeneric.xaml";
			if (!System.IO.File.Exists(tmp))
				tmp = @"C:\Projects\PPSn\PPSnWpf\PPSnDesktopPG\Example\TestWpfGeneric.xaml";
			//await LoadPaneAsync(typeof(Panes.PpsLoginPane), null);
			await LoadPaneAsync(typeof(PpsGenericWpfWindowPane),
				Procs.CreateLuaTable(
					new KeyValuePair<string, object>("template", tmp)
				)
			);
		} // proc StartLogin

		private async Task LoadPaneAsync(Type paneType, LuaTable arguments)
		{
			// unload the current pane
			if (!await UnloadPaneAsync())
				return;

			// set the new pane
			var ci = paneType.GetConstructors().FirstOrDefault();
			if (ci == null)
				throw new ArgumentException("No ctor"); // todo: exception

			var parameterInfo = ci.GetParameters();
			var paneArguments = new object[parameterInfo.Length];
			for (int i = 0; i < paneArguments.Length; i++)
			{
				var pi = parameterInfo[i];
				if (pi.ParameterType.IsAssignableFrom(typeof(PpsMainEnvironment)))
					paneArguments[i] = Environment;
				else
					throw new ArgumentException("Unsupported argument."); // todo: Exception
			}

			// Create pane
			var currentPane = (IPpsWindowPane)Activator.CreateInstance(paneType, paneArguments);
			SetValue(CurrentPaneKey, currentPane);

			// load the pane
			await currentPane.LoadAsync(arguments);
			// TEST SCHMIDT
			RefreshTitle();
		} // proc StartPaneAsync

		protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
		{
			base.OnPropertyChanged(e);
		}

		private async Task<bool> UnloadPaneAsync()
		{
			if (CurrentPane == null)
				return true;
			else
			{
				var currentPane = CurrentPane;
				if (await currentPane.UnloadAsync())
				{
					Procs.FreeAndNil(ref currentPane);
					SetValue(CurrentPaneKey, null);
					return true;
				}
				else
					return false;
			}
		} // proc UnloadPaneAsync
				
		public void RefreshTitle()
		{
			this.Title = CurrentPane == null ? "PPS2000n" : String.Format("PPS2000n - {0}", CurrentPane.Title);
		} // proc RefreshTitle

		/// <summary>Returns the current view of the pane as a wpf control.</summary>
		public IPpsWindowPane CurrentPane { get { return (IPpsWindowPane)GetValue(CurrentPaneProperty); } }
		/// <summary>Settings of the current window.</summary>
		public PpsWindowApplicationSettings Settings { get { return settings; } }

		/// <summary>Index of the current window</summary>
		public int WindowIndex { get { return windowIndex; } }
		/// <summary>Access to the current environment,</summary>
		public new PpsMainEnvironment Environment { get { return (PpsMainEnvironment)base.Environment; } }
	} // class PpsMainWindow
}
