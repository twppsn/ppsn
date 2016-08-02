using Neo.IronLua;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
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
using TecWare.DE.Stuff;
using System.Reflection;

namespace TecWare.PPSn.UI
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public partial class PpsMainWindow : PpsWindow
	{
		/// <summary>Toggles between DataPane and Navigator.</summary>
		public readonly static RoutedCommand NavigatorToggleCommand = new RoutedCommand("NavigatorToggle", typeof(PpsMainWindow));

		public readonly static RoutedCommand NextPaneCommand = new RoutedCommand("NextPane", typeof(PpsMainWindow));
		public readonly static RoutedCommand PrevPaneCommand = new RoutedCommand("PrevPane", typeof(PpsMainWindow));

		private readonly static DependencyProperty NavigatorVisibilityProperty = DependencyProperty.Register("NavigatorVisibility", typeof(Visibility), typeof(PpsMainWindow), new UIPropertyMetadata(Visibility.Visible));
		private readonly static DependencyProperty PaneVisibilityProperty = DependencyProperty.Register("PaneVisibility", typeof(Visibility), typeof(PpsMainWindow), new UIPropertyMetadata(Visibility.Collapsed));
		private readonly static DependencyProperty CharmbarActualWidthProperty = DependencyProperty.Register("CharmbarActualWidth", typeof(double), typeof(PpsMainWindow));

		/// <summary>Readonly property for the current pane.</summary>
		private readonly static DependencyPropertyKey CurrentPaneKey = DependencyProperty.RegisterReadOnly("CurrentPane", typeof(IPpsWindowPane), typeof(PpsMainWindow), new PropertyMetadata(null));
		private readonly static DependencyProperty CurrentPaneProperty = CurrentPaneKey.DependencyProperty;

		private int windowIndex = -1;										// settings key
		private PpsWindowApplicationSettings settings;	                    // current settings for the window

		#region -- Ctor/Dtor --------------------------------------------------------------

		public PpsMainWindow(int windowIndex)
		{
			this.windowIndex = windowIndex;

			InitializeComponent();
			
			// initialize settings
			settings = new PpsWindowApplicationSettings(this, "main" + windowIndex.ToString());
			PART_Navigator.Init(this);

			#region -- set basic command bindings --
			CommandBindings.Add(
				new CommandBinding(PpsWindow.LoginCommand,
					async (sender, e) =>
					{
						e.Handled = true;
						await StartLoginAsync();
					},
					(sender, e) => e.CanExecute = !Environment.IsAuthentificated
				)
			);

			CommandBindings.Add(
				new CommandBinding(NavigatorToggleCommand,
					(sender, e) =>
					{
						IsNavigatorVisible = !IsNavigatorVisible;
						e.Handled = true;
					},
					(sender, e) => e.CanExecute = true
				)
			);

			CommandBindings.Add(
				new CommandBinding(PpsWindow.TraceLogCommand,
					async	(sender, e) =>
					{
						e.Handled = true;
						await LoadPaneAsync(Environment.TracePane, PpsOpenPaneMode.NewSingleWindow, null);
					}
				)
			);

			CommandBindings.Add(
				new CommandBinding(PpsMainWindow.NextPaneCommand, 
					(sender, e) =>
					{
						e.Handled = true;
						ActivateNextPane(true);
					}, 
					(sender, e) =>
					{
						e.CanExecute = Panes.IndexOf(CurrentPane) < Panes.Count - 1;
					}
				)
			);
			CommandBindings.Add(
				new CommandBinding(PpsMainWindow.PrevPaneCommand,
					(sender, e) =>
					{
						e.Handled = true;
						ActivateNextPane(false);
					},
					(sender, e) =>
					{
						e.CanExecute = Panes.IndexOf(CurrentPane) > 0;
					}
				)
			);

			#endregion

			this.DataContext = this;

			RefreshTitle();
			Trace.TraceInformation("MainWindow[{0}] created.", windowIndex);

			var descriptor = DependencyPropertyDescriptor.FromProperty(PpsCharmbarControl.ActualWidthProperty, typeof(PpsCharmbarControl));
			descriptor.AddValueChanged(PART_Charmbar, OnCharmbarActualWidthChanged);
		} // ctor

		#endregion

		public void RefreshTitle()
		{
			this.Title = CurrentPane == null ? "PPS2000n" : String.Format("PPS2000n - {0}", CurrentPane.Title);
		} // proc RefreshTitle

		#region -- Navigator.SearchBox ------------------------------------------------

		protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
		{
			base.OnPreviewMouseDown(e);
			if (!IsNavigatorVisible)
				return;
			PART_Navigator.OnPreview_MouseDown(e.OriginalSource);
		} // event OnPreviewMouseDown

		protected override void OnPreviewTextInput(TextCompositionEventArgs e)
		{
			base.OnPreviewTextInput(e);
			if (!IsNavigatorVisible)
				return;
			PART_Navigator.OnPreview_TextInput(e);
		} // event OnPreviewTextInput

		protected override void OnWindowCaptionClicked()
		{
			if (!IsNavigatorVisible)
				return;
			PART_Navigator.OnPreview_MouseDown(null);
		} //

		#endregion

		#region -- charmbar -----------------------------------------------------------------

		private void OnCharmbarActualWidthChanged(object sender, EventArgs e)
		{
			SetActualWidth(PART_Charmbar.ActualWidth);
		}

		private void SetActualWidth(double value)
		{
			SetValue(CharmbarActualWidthProperty, value);
		}

		#endregion

		/// <summary>Settings of the current window.</summary>
		public PpsWindowApplicationSettings Settings => settings;
		/// <summary>Index of the current window</summary>
		public int WindowIndex => windowIndex; 
		/// <summary>Access to the current environment,</summary>
		public new PpsMainEnvironment Environment => (PpsMainEnvironment)base.Environment;
		/// <summary>Access to current charmbar width</summary>
		public double CharmbarActualWidth { get { return (double)GetValue(CharmbarActualWidthProperty); } }

		public bool IsNavigatorVisible
		{
			get
			{
				return (Visibility)GetValue(NavigatorVisibilityProperty) == Visibility.Visible;
			}
			set
			{
				if (IsNavigatorVisible != value)
				{
					if (value)
					{
						SetValue(NavigatorVisibilityProperty, Visibility.Visible);
						SetValue(PaneVisibilityProperty, Visibility.Collapsed);
					}
					else
					{
						SetValue(NavigatorVisibilityProperty, Visibility.Collapsed);
						SetValue(PaneVisibilityProperty, Visibility.Visible);
					}
				}
			}
		} // prop NavigatorState

	} // class PpsMainWindow

}
