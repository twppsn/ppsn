using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace TecWare.PPSn.UI
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PpsWindowHitTest
	{
		public int HitTest { get; set; }
	} // class PpsWindowHitTest

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Contains the generic layout of all windows of the the application.</summary>
	public partial class PpsWindow : Window
	{
		/// <summary>Command for minimizing the window.</summary>
		public readonly static RoutedCommand MinimizeCommand = new RoutedCommand("Minimize", typeof(PpsWindow));
		/// <summary>Command for maximizing the window.</summary>
		public readonly static RoutedCommand MaximizeCommand = new RoutedCommand("Maximize", typeof(PpsWindow));
		/// <summary>Command for closing the window.</summary>
		public readonly static RoutedCommand CloseCommand = new RoutedCommand("Close", typeof(PpsWindow));
		/// <summary>Starts the user login.</summary>
		public readonly static RoutedCommand LoginCommand = new RoutedCommand("Login", typeof(PpsWindow));
		/// <summary>Starts the user logout.</summary>
		public readonly static RoutedCommand LogoutCommand = new RoutedCommand("Logout", typeof(PpsWindow));
		/// <summary>Opens a trace pane.</summary>
		public readonly static RoutedCommand TraceLogCommand = new RoutedCommand("TraceLog", typeof(PpsWindow));

		private PpsEnvironment environment;

		public PpsWindow()
		{
			InitChrome();

			CommandBindings.AddRange(
				new CommandBinding[]
				{
					new CommandBinding(MinimizeCommand, (sender, e) => WindowState = WindowState.Minimized, (sender, e) => e.CanExecute = true),
					new CommandBinding(MaximizeCommand, (sender, e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized),
					new CommandBinding(CloseCommand, (sender, e) => Close())
				});
		} // ctor

		public PpsEnvironment Environment
		{
			get
			{
				if (environment == null)
					environment = PpsEnvironment.GetEnvironment(this);
				return environment;
			}
		} // prop Environment
	} // class PpsWindow

	#region -- class PpsWindowApplicationSettings ---------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
  public class PpsWindowApplicationSettings : ApplicationSettingsBase
	{
		private PpsWindow owner;
		private DispatcherTimer persistTimer;
		private bool isResizeable;

		#region -- Ctor/Dtor --------------------------------------------------------------

		public PpsWindowApplicationSettings(PpsWindow owner, string appSettingsKey = "")
		{
			this.owner = owner;
			this.isResizeable = owner.ResizeMode != ResizeMode.NoResize;
			this.SettingsKey = appSettingsKey;

			if (UpgradeSettings)
			{
				Upgrade();
				this[nameof(UpgradeSettings)] = false;
			}

			this.persistTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(10000), DispatcherPriority.ApplicationIdle, (sender, e) => Save(), owner.Dispatcher);
			this.owner.Closed += (sender, e) =>
				{
					if (persistTimer.IsEnabled) // force save on close
						Save();
				};

			InitWindowPlacement();
		} // ctor

		private void InitWindowPlacementProperty(DependencyProperty windowProperty, string settingsProperty)
		{
			var binding = new Binding();
			binding.Source = this;
			binding.Path = new PropertyPath(settingsProperty);
			binding.Mode = BindingMode.TwoWay;
			binding.UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged;
			BindingOperations.SetBinding(owner, windowProperty, binding);
		} // proc InitWindowPlacementProperty

		protected void InitWindowPlacement()
		{
			InitWindowPlacementProperty(Window.LeftProperty, nameof(Left));
			InitWindowPlacementProperty(Window.TopProperty, nameof(Top));

      if (isResizeable)
			{
				InitWindowPlacementProperty(Window.WidthProperty, nameof(Width));
				InitWindowPlacementProperty(Window.HeightProperty, nameof(Height));
			}

			// must be last, because of the maximized state will only appear on the primary screen
			owner.Dispatcher.BeginInvoke(new Action(() => InitWindowPlacementProperty(Window.WindowStateProperty, nameof(WindowState))), DispatcherPriority.ApplicationIdle);
		} // proc InitWindowPlacement

		#endregion

		#region -- OnSettingsLoaded -------------------------------------------------------

		protected override void OnSettingsLoaded(object sender, SettingsLoadedEventArgs e)
		{
			var updateValue = 0;

			// check if the position is valid
			var left = Left;
			var top = Top;
			var width = Width ;
			var height = Height;

			if (left == Double.NaN || top == Double.NaN) // location is default
				return;

			// correct the size
			if (!isResizeable || width == Double.NaN)
				width = owner.Width;
			if (!isResizeable || height == Double.NaN)
				height = owner.Height;

			// on which monitor is the window placed
			var rc = new RECT { Left = (int)left, Top = (int)top, Right = (int)(left + width), Bottom = (int)(top + height) };
      var hMonitor = NativeMethods.MonitorFromRect(ref rc, MonitorOptions.MONITOR_DEFAULTTONEAREST);

			// get the metrics of the monitor
			var monitorInfo = new MONITORINFO();
			monitorInfo.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
			if (!NativeMethods.GetMonitorInfo(hMonitor, ref monitorInfo))
				throw new Win32Exception();

			// check the size
			if (isResizeable)
			{
				if (rc.Width > monitorInfo.rcWork.Width)
				{
					width = monitorInfo.rcWork.Width;
					updateValue |= 4;
				}
				if (rc.Height > monitorInfo.rcWork.Height)
				{
					height = monitorInfo.rcWork.Height;
					updateValue |= 8;
				}
			}

			// move the window in the monitor
			if (rc.Left < monitorInfo.rcWork.Left)
			{
				left = monitorInfo.rcWork.Left;
				updateValue |= 1;
			}
			if (rc.Top < monitorInfo.rcWork.Top)
			{
				top = monitorInfo.rcWork.Top;
				updateValue |= 2;
			}

			if (left + width > monitorInfo.rcWork.Right)
			{
				left = monitorInfo.rcWork.Right - width;
				updateValue |= 1;
			}
			if (top + height > monitorInfo.rcWork.Bottom)
			{
				top = monitorInfo.rcWork.Height - height;
				updateValue |= 2;
			}

			// update window
			if ((updateValue & 1) != 0)
				Left = left;
			if ((updateValue & 2) != 0)
				Top = top;
			if ((updateValue & 4) != 0)
				Width = width;
			if ((updateValue & 8) != 0)
				Height = height;

			if (WindowState == WindowState.Minimized)
				WindowState = WindowState.Normal;

			base.OnSettingsLoaded(sender, e);
		} // proc OnSettingsLoaded

		#endregion

		#region -- OnSettingChanging, Save ------------------------------------------------

		protected override void OnSettingChanging(object sender, SettingChangingEventArgs e)
		{
			if (!Object.Equals(this[e.SettingName], e.NewValue))
			{
				if (persistTimer != null)
				{
					persistTimer.Stop();
					persistTimer.Start();
				}
			}
			else
				e.Cancel = true;

			base.OnSettingChanging(sender, e);
		} // proc OnSettingChanging

		public override void Save()
		{
			base.Save();
			persistTimer.Stop();
		} // proc Save

		#endregion

		#region -- Window Placement -------------------------------------------------------

		private void SetDouble(double value, [CallerMemberName] string propertyName = null)
		{
			if (owner.WindowState == WindowState.Normal)
				this[propertyName] = value;
		} // prop SetDouble

		private double GetDouble([CallerMemberName] string propertyName = null) => this[propertyName] == null ? Double.NaN : (double)this[propertyName];

		[
		UserScopedSetting,
		DefaultSettingValue("Maximized")
		]
		public WindowState WindowState { get { return (WindowState)this[nameof(WindowState)]; } set { this[nameof(WindowState)] = value; } }
		[
		UserScopedSetting,
    DefaultSettingValue(null)
		]
		public double Left { get { return GetDouble(); } set { SetDouble(value);  } }
		[
		UserScopedSetting,
		DefaultSettingValue(null)
		]
		public double Top { get { return GetDouble(); } set { SetDouble(value); } }
		[
		UserScopedSetting,
		DefaultSettingValue(null)
		]
		public double Width { get { return GetDouble(); } set { SetDouble(value); } }
		[
		UserScopedSetting,
		DefaultSettingValue(null)
		]
		public double Height { get { return GetDouble(); } set { SetDouble(value); } }

		[
		UserScopedSetting,
		DefaultSettingValue("True")
		]
		public bool UpgradeSettings => (bool)this[nameof(UpgradeSettings)];

		#endregion
	} // class PpsWindowApplicationSettings

	#endregion
}
