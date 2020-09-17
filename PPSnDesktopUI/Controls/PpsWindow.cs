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
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Configuration;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using TecWare.DE.Stuff;
using TecWare.PPSn.UI;

namespace TecWare.PPSn.Controls
{
	#region -- class PpsWindowHitTest -------------------------------------------------

	/// <summary>HitTest helper for WinApi</summary>
	public class PpsWindowHitTest
	{
		/// <summary>HitTest return</summary>
		public int HitTest { get; set; }
	} // class PpsWindowHitTest

	#endregion

	#region -- class PpsWindow --------------------------------------------------------

	/// <summary>Contains the generic layout of all windows of the the application.</summary>
	public partial class PpsWindow : Window, IServiceContainer
	{
		/// <summary>Command for minimizing the window.</summary>
		public readonly static RoutedCommand MinimizeCommand = new RoutedCommand("Minimize", typeof(PpsWindow));
		/// <summary>Command for maximizing the window.</summary>
		public readonly static RoutedCommand MaximizeCommand = new RoutedCommand("Maximize", typeof(PpsWindow));
		/// <summary>Command for closing the window.</summary>
		public readonly static RoutedCommand CloseCommand = new RoutedCommand("Close", typeof(PpsWindow));
		/// <summary>Opens a trace pane.</summary>
		public readonly static RoutedCommand TraceLogCommand = new RoutedCommand("TraceLog", typeof(PpsWindow));

		/// <summary>GlowColor when window is active.</summary>
		public static readonly DependencyProperty ActiveGlowColorProperty = DependencyProperty.Register("ActiveGlowColor", typeof(Color), typeof(PpsWindow), new FrameworkPropertyMetadata(Colors.Black, new PropertyChangedCallback(OnGlowColorChanged)));
		/// <summary>GlowColor when window is inactive.</summary>
		public static readonly DependencyProperty InactiveGlowColorProperty = DependencyProperty.Register("InactiveGlowColor", typeof(Color), typeof(PpsWindow), new FrameworkPropertyMetadata(Colors.LightGray, new PropertyChangedCallback(OnGlowColorChanged)));

		private readonly ServiceContainer serviceContainer;
		private readonly IPpsShell shell;

		/// <summary>Window</summary>
		public PpsWindow()
			: this(PpsShell.Current)
		{
		} // ctor
		
		/// <summary>Window</summary>
		public PpsWindow(IServiceProvider service)
		{
			serviceContainer = new ServiceContainer(service ?? throw new ArgumentNullException(nameof(service)));
			shell = serviceContainer.GetService<IPpsShell>(true);
			if (shell != null)
				this.RegisterShell(shell);

			InitChrome();

			CommandBindings.AddRange(
				new CommandBinding[]
				{
					new CommandBinding(MinimizeCommand, (sender, e) => WindowState = WindowState.Minimized, (sender, e) => e.CanExecute = true),
					new CommandBinding(MaximizeCommand, (sender, e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized),
					new CommandBinding(CloseCommand, (sender, e) => Close())
				});

			this.AddCommandDefaultHandler(shell);
		} // ctor

		/// <summary>Caption clicked</summary>
		protected virtual void OnWindowCaptionClicked()
		{
		}

#if DEBUG
		/// <summary></summary>
		/// <param name="e"></param>
		protected override void OnKeyUp(KeyEventArgs e)
		{
			if (e.Key == Key.F9)
				PpsWpfShell.PrintLogicalTreeToConsole(Keyboard.FocusedElement as DependencyObject);
			else if (e.Key == Key.F8)
				PpsWpfShell.PrintVisualTreeToConsole(Keyboard.FocusedElement as DependencyObject);
			else if (e.Key == Key.F7)
				PpsWpfShell.PrintEventTreeToConsole(Keyboard.FocusedElement as DependencyObject);
			base.OnKeyUp(e);
		} // proc OnKeyUp
#endif

		/// <summary></summary>
		/// <returns></returns>
		public Rect GetWorkingArea()
		{
			var presentationSource = PresentationSource.FromVisual(this);
			var rc = new RECT { Left = (int)Left, Top = (int)Top, Right = (int)(Left + ActualWidth), Bottom = (int)(Top + ActualHeight) };
			var hMonitor = NativeMethods.MonitorFromRect(ref rc, MonitorOptions.MONITOR_DEFAULTTONEAREST);

			// get the metrics of the monitor
			var monitorInfo = new MONITORINFO
			{
				cbSize = Marshal.SizeOf(typeof(MONITORINFO))
			};
			if (!NativeMethods.GetMonitorInfo(hMonitor, ref monitorInfo))
				throw new Win32Exception();

			var rcWork = new Rect(monitorInfo.rcWork.Left, monitorInfo.rcWork.Top, monitorInfo.rcWork.Width, monitorInfo.rcWork.Height);
			var transformToDevice = presentationSource.CompositionTarget.TransformFromDevice;
			return new Rect(
				transformToDevice.Transform(rcWork.TopLeft),
				transformToDevice.Transform(rcWork.BottomRight)
			);
		} // func GetWorkingArea

		#region -- IServiceContainer - members ----------------------------------------

		void IServiceContainer.AddService(Type serviceType, object serviceInstance) 
			=> serviceContainer.AddService(serviceType, serviceInstance);

		void IServiceContainer.AddService(Type serviceType, object serviceInstance, bool promote) 
			=> serviceContainer.AddService(serviceType, serviceInstance, promote);

		void IServiceContainer.AddService(Type serviceType, ServiceCreatorCallback callback) 
			=> serviceContainer.AddService(serviceType, callback);

		void IServiceContainer.AddService(Type serviceType, ServiceCreatorCallback callback, bool promote) 
			=> serviceContainer.AddService(serviceType, callback, promote);

		void IServiceContainer.RemoveService(Type serviceType) 
			=> serviceContainer.RemoveService(serviceType);

		void IServiceContainer.RemoveService(Type serviceType, bool promote) 
			=> serviceContainer.RemoveService(serviceType, promote);

		object IServiceProvider.GetService(Type serviceType) 
			=> serviceContainer.GetService(serviceType);

		#endregion

		/// <summary>Return services.</summary>
		public IServiceContainer Services => this;
		/// <summary>Current environment of the window.</summary>
		public IPpsShell Shell => shell; 

		public PpsShellWpf _Shell => null;
	} // class PpsWindow

	#endregion

	#region -- class PpsWindowApplicationSettings -------------------------------------

	/// <summary></summary>
	public class PpsWindowApplicationSettings : ApplicationSettingsBase
	{
		private readonly PpsWindow owner;
		private readonly DispatcherTimer persistTimer;
		private readonly bool isResizeable;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary></summary>
		/// <param name="owner"></param>
		/// <param name="appSettingsKey"></param>
		public PpsWindowApplicationSettings(PpsWindow owner, string appSettingsKey = "")
		{
			this.owner = owner;

			isResizeable = owner.ResizeMode != ResizeMode.NoResize;
			SettingsKey = appSettingsKey;

			if (UpgradeSettings)
			{
				Upgrade();
				this[nameof(UpgradeSettings)] = false;
			}

			persistTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(10000), DispatcherPriority.ApplicationIdle, (sender, e) => Save(), owner.Dispatcher);
			this.owner.Closed += (sender, e) =>
			{
				if (persistTimer.IsEnabled) // force save on close
					Save();
			};

			InitWindowPlacement();
		} // ctor

		private void InitWindowPlacementProperty(DependencyProperty windowProperty, string settingsProperty)
		{
			var binding = new Binding
			{
				Source = this,
				Path = new PropertyPath(settingsProperty),
				Mode = BindingMode.TwoWay,
				UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
			};
			BindingOperations.SetBinding(owner, windowProperty, binding);
		} // proc InitWindowPlacementProperty

		/// <summary>Initialize window placement bindings.</summary>
		protected void InitWindowPlacement()
		{
			InitWindowPlacementProperty(Window.LeftProperty, nameof(Left));
			InitWindowPlacementProperty(Window.TopProperty, nameof(Top));

			if (isResizeable)
			{
				InitWindowPlacementProperty(FrameworkElement.WidthProperty, nameof(Width));
				InitWindowPlacementProperty(FrameworkElement.HeightProperty, nameof(Height));
			}

			// must be last, because of the maximized state will only appear on the primary screen
			owner.Dispatcher.BeginInvoke(new Action(() => InitWindowPlacementProperty(Window.WindowStateProperty, nameof(WindowState))), DispatcherPriority.ApplicationIdle);
		} // proc InitWindowPlacement

		#endregion

		#region -- OnSettingsLoaded ---------------------------------------------------

		/// <summary></summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		protected override void OnSettingsLoaded(object sender, SettingsLoadedEventArgs e)
		{
			var updateValue = 0;

			// check if the position is valid
			var left = Left;
			var top = Top;
			var width = Width;
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
			var monitorInfo = new MONITORINFO
			{
				cbSize = Marshal.SizeOf(typeof(MONITORINFO))
			};
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

		#region -- OnSettingChanging, Save --------------------------------------------

		/// <summary></summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		protected override void OnSettingChanging(object sender, SettingChangingEventArgs e)
		{
			if (!Equals(this[e.SettingName], e.NewValue))
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

		/// <summary></summary>
		public override void Save()
		{
			base.Save();
			persistTimer.Stop();
		} // proc Save

		#endregion

		#region -- Window Placement ---------------------------------------------------

		private void SetDouble(double value, [CallerMemberName] string propertyName = null)
		{
			if (owner.WindowState == WindowState.Normal)
				this[propertyName] = value;
		} // prop SetDouble

		private double GetDouble([CallerMemberName] string propertyName = null) => this[propertyName] == null ? Double.NaN : (double)this[propertyName];

		/// <summary></summary>
		[
		UserScopedSetting,
		DefaultSettingValue("Maximized")
		]
		public WindowState WindowState { get => (WindowState)this[nameof(WindowState)]; set => this[nameof(WindowState)] = value; }
		/// <summary></summary>
		[
		UserScopedSetting,
		DefaultSettingValue(null)
		]
		public double Left { get => GetDouble(); set => SetDouble(value); }
		/// <summary></summary>
		[
		UserScopedSetting,
		DefaultSettingValue(null)
		]
		public double Top { get => GetDouble(); set => SetDouble(value); }
		/// <summary></summary>
		[
		UserScopedSetting,
		DefaultSettingValue(null)
		]
		public double Width { get => GetDouble(); set => SetDouble(value); }
		/// <summary></summary>
		[
		UserScopedSetting,
		DefaultSettingValue(null)
		]
		public double Height { get => GetDouble(); set => SetDouble(value); }

		/// <summary></summary>
		[
		UserScopedSetting,
		DefaultSettingValue("True")
		]
		public bool UpgradeSettings => (bool)this[nameof(UpgradeSettings)];

		#endregion
	} // class PpsWindowApplicationSettings

	#endregion
}
