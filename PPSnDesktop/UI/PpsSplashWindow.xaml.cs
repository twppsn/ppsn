using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
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
using System.Windows.Threading;

namespace TecWare.PPSn.UI
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public partial class PpsSplashWindow : Window
	{
		#region -- class LoginState -------------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		public sealed class LoginStateData : INotifyPropertyChanged
		{
			public event PropertyChangedEventHandler PropertyChanged;

			private readonly PpsSplashWindow parent;

			private PpsEnvironmentInfo[] environments = null;
			private PpsEnvironmentInfo currentEnvironment = null;
			private string userName = null;
			
			public LoginStateData(PpsSplashWindow parent)
			{
				this.parent = parent;
			} // ctor

			private void OnPropertyChanged(string propertyName)
				=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

			public ICredentials GetCredentials()
			{
				return CredentialCache.DefaultCredentials;
			} // func GetCredentials

			public void RefreshEnvironments(PpsEnvironmentInfo selectEnvironment)
			{
				var tmp = PpsEnvironmentInfo.GetLocalEnvironments().ToArray();
				parent.Dispatcher.Invoke(() =>
					{
						environments = tmp;
						OnPropertyChanged(nameof(Environments));

						if (environments.Length == 1)
							CurrentEnvironment = environments[0];
						else if (environments.Contains(selectEnvironment))
							CurrentEnvironment = selectEnvironment;
					}
				);
			} // proc RefreshEnvironments

			public PpsEnvironmentInfo[] Environments => environments;

			public PpsEnvironmentInfo CurrentEnvironment
			{
				get { return currentEnvironment; }
				set
				{
					if (currentEnvironment != value)
					{
						currentEnvironment = value;
						OnPropertyChanged(nameof(CurrentEnvironment));
						OnPropertyChanged(nameof(IsUserNameEnabled));

						UserName = Environment.UserDomainName + "\\" + Environment.UserName;
					}
				}
			} // prop CurrentEnvironment

			public string UserName
			{
				get { return userName; }
				set
				{
					if (value != null && value.Length == 0)
						value = null;

					if (value != userName)
					{
						userName = value;

						OnPropertyChanged(nameof(UserName));
						OnPropertyChanged(nameof(IsValid));
						OnPropertyChanged(nameof(IsPasswordEnabled));
					}
				}
			} // prop UserName

			public string Password;

			public bool IsValid => !String.IsNullOrEmpty(userName);
			public bool IsUserNameEnabled => currentEnvironment != null;
			public bool IsPasswordEnabled => userName != null && !IsDomainName(userName) && !IsDefaultUser(userName);

			private static bool IsDomainName(string userName)
				=> userName.StartsWith(System.Environment.UserDomainName + "\\", StringComparison.OrdinalIgnoreCase);

			private static bool IsDefaultUser(string userName)
				=> false;
		} // class LoginStateData

		#endregion

		private readonly static DependencyPropertyKey LoginPaneVisiblePropertyKey = DependencyProperty.RegisterReadOnly(nameof(LoginPaneVisible), typeof(Visibility), typeof(PpsSplashWindow), new PropertyMetadata(Visibility.Hidden));
		private readonly static DependencyPropertyKey StatusPaneVisiblePropertyKey = DependencyProperty.RegisterReadOnly(nameof(StatusPaneVisible), typeof(Visibility), typeof(PpsSplashWindow), new PropertyMetadata(Visibility.Visible));
		private readonly static DependencyPropertyKey LoginStatePropertyKey = DependencyProperty.RegisterReadOnly(nameof(LoginState), typeof(LoginStateData), typeof(PpsSplashWindow), new PropertyMetadata(null));

		public readonly static DependencyProperty LoginPaneVisibleProperty = LoginPaneVisiblePropertyKey.DependencyProperty;
		public readonly static DependencyProperty StatusPaneVisibleProperty = StatusPaneVisiblePropertyKey.DependencyProperty;
		public readonly static DependencyProperty StatusTextProperty = DependencyProperty.Register(nameof(StatusText), typeof(string), typeof(PpsSplashWindow));
		public readonly static DependencyProperty LoginStateProperty = LoginStatePropertyKey.DependencyProperty;

		private LoginStateData loginStateUnSafe;
		private bool dialogResult = false;
		private DispatcherFrame loginFrame = null;
		private bool allowClose = false;

		#region -- Ctor/Dtor --------------------------------------------------------------

		public PpsSplashWindow()
		{
			InitializeComponent();

			CommandBindings.AddRange(
				new CommandBinding[]
				{
					new CommandBinding(ApplicationCommands.New, CreateNewEnvironment, LoginFrameActive),
					new CommandBinding(ApplicationCommands.Save, ExecuteLoginFrame, LoginFrameActive),
					new CommandBinding(ApplicationCommands.Close, CloseLoginFrame, LoginFrameActive)
				}
			);
			
			SetValue(LoginPaneVisiblePropertyKey, Visibility.Hidden);
			SetValue(StatusPaneVisiblePropertyKey, Visibility.Visible);
			SetValue(LoginStatePropertyKey, loginStateUnSafe = new LoginStateData(this));

			this.DataContext = this;
		} // ctor

		protected override void OnClosing(CancelEventArgs e)
		{
			if (loginFrame != null)
			{
				ApplicationCommands.Close.Execute(null, Keyboard.FocusedElement);
				e.Cancel = true;
			}
			else
				e.Cancel = !allowClose;
			base.OnClosing(e);
		} // proc OnClosing

		public void ForceClose()
		{
			allowClose = true;
			Close();
		} // proc ForceClose

		#endregion

		#region -- Login ------------------------------------------------------------------

		private void LoginFrameActive(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = loginFrame != null;
			e.Handled = true;
		} // proc LoginFrameActive

		private void CreateNewEnvironment(object sender, ExecutedRoutedEventArgs e)
		{
			MessageBox.Show("Wizasrd fürs verbinden!");
			e.Handled = true;
		} // proc CreateNewEnvironment

		private void ExecuteLoginFrame(object sender, ExecutedRoutedEventArgs e)
		{
			loginFrame.Continue = false;
			dialogResult = true;
			e.Handled = true;
		} // proc ExecuteLoginFrame

		private void CloseLoginFrame(object sender, ExecutedRoutedEventArgs e)
		{
			loginFrame.Continue = false;
			dialogResult = false;
			e.Handled = true;
		} // proc CloseLoginFrame

		private Tuple<PpsEnvironmentInfo, ICredentials> ShowLogin()
		{
			SetValue(LoginPaneVisiblePropertyKey, Visibility.Visible);
			SetValue(StatusPaneVisiblePropertyKey, Visibility.Hidden);
			try
			{
				if (loginFrame != null)
					throw new InvalidOperationException();

				// spawn new event loop
				loginFrame = new DispatcherFrame();
				CommandManager.InvalidateRequerySuggested();
				Dispatcher.PushFrame(loginFrame);
				loginFrame = null;

				if (dialogResult && loginStateUnSafe.IsValid)
				{
					return new Tuple<PpsEnvironmentInfo, ICredentials>(loginStateUnSafe.CurrentEnvironment, loginStateUnSafe.GetCredentials());
				}
				else
					return null;
			}
			finally
			{
				SetValue(LoginPaneVisiblePropertyKey, Visibility.Hidden);
				SetValue(StatusPaneVisiblePropertyKey, Visibility.Visible);
			}
		} // proc ShowLogin

		public async Task<Tuple<PpsEnvironmentInfo, ICredentials>> ShowLoginAsync(PpsEnvironmentInfo selectEnvironment)
		{
			loginStateUnSafe.RefreshEnvironments(selectEnvironment);

			return await Dispatcher.InvokeAsync(ShowLogin);
		} // func ShowLoginAsync

		#endregion

		#region -- Progres ----------------------------------------------------------------

		public void SetProgressTextAsync(string text)
			=> Dispatcher.BeginInvoke(new Action<string>(t => StatusText = t), DispatcherPriority.Normal, text);

		#endregion

		public Visibility LoginPaneVisible => (Visibility)GetValue(LoginPaneVisibleProperty);
		public Visibility StatusPaneVisible => (Visibility)GetValue(StatusPaneVisibleProperty);
		public string StatusText { get { return (string)GetValue(StatusTextProperty); } set { SetValue(StatusTextProperty, value); } }
		public LoginStateData LoginState => (LoginStateData)GetValue(LoginStateProperty);
	} // class PpsSplashWindow
}
