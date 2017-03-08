using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using TecWare.PPSn.Data;
using TecWare.PPSn.Stuff;

// todo: designer find place for ErrorText in ui

namespace TecWare.PPSn.UI
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public partial class PpsSplashWindow : Window, IProgress<string>
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


			public LoginStateData(PpsSplashWindow parent)
			{
				this.parent = parent;
			} // ctor

			private void OnPropertyChanged(string propertyName)
				=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

			public ICredentials GetCredentials()
			{
				if (IsDomainName(defaultUser.UserName))
					return CredentialCache.DefaultNetworkCredentials.GetCredential(currentEnvironment.Uri, "Basic");

				if (parent.pbPassword.Password == "tecware-gmbh.de")
					return defaultUser;
				else
					return new NetworkCredential(defaultUser.UserName, parent.pbPassword.SecurePassword);


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

						if (currentEnvironment?.Uri != null)
							DefaultUser = LoadUserCredentials(currentEnvironment.Uri.ToString());

					}
				}
			} // prop CurrentEnvironment

			private NetworkCredential defaultUser;
			public NetworkCredential DefaultUser
			{
				get
				{
					return defaultUser;
				}
				set
				{
					defaultUser = value;

					if (defaultUser?.SecurePassword?.Length > 0)
						parent.pbPassword.Password = "tecware-gmbh.de";
					else parent.pbPassword.Password = String.Empty;
					OnPropertyChanged(nameof(UserName));
					OnPropertyChanged(nameof(IsPasswordEnabled));
					OnPropertyChanged(nameof(IsValid));
				}
			}

			public string UserName
			{
				get
				{
					if (defaultUser != null)
						return defaultUser.UserName;
					return String.Empty;
				}
				set
				{
					var uriCredential = LoadUserCredentials(currentEnvironment.Uri.ToString());
					if (value != uriCredential?.UserName)
						DefaultUser = new NetworkCredential(value, "");
					else DefaultUser = uriCredential;
					OnPropertyChanged(nameof(IsValid));
				}
			}

			private static NetworkCredential LoadUserCredentials(string uri)
			{
				NetworkCredential userCred;
				using (var pcl = new PpsClientLogin(uri, "", false))
					userCred = (NetworkCredential)pcl.GetCredentials();
				return userCred;
			}

			public bool IsValid => IsDomainName(defaultUser != null ? defaultUser.UserName : String.Empty) || !String.IsNullOrEmpty(defaultUser?.Password);
			public bool IsUserNameEnabled => currentEnvironment != null;
			public bool IsPasswordEnabled => !IsDomainName(defaultUser != null ? defaultUser.UserName : String.Empty);

			private static bool IsDomainName(string userName)
				=> userName.StartsWith(System.Environment.UserDomainName + "\\", StringComparison.OrdinalIgnoreCase);

		} // class LoginStateData

		#endregion


		private readonly static DependencyPropertyKey loginPaneVisiblePropertyKey = DependencyProperty.RegisterReadOnly(nameof(LoginPaneVisible), typeof(Visibility), typeof(PpsSplashWindow), new PropertyMetadata(Visibility.Hidden));
		private readonly static DependencyPropertyKey statusPaneVisiblePropertyKey = DependencyProperty.RegisterReadOnly(nameof(StatusPaneVisible), typeof(Visibility), typeof(PpsSplashWindow), new PropertyMetadata(Visibility.Hidden));//Visible
		private readonly static DependencyPropertyKey environmentWizzardPaneVisiblePropertyKey = DependencyProperty.RegisterReadOnly(nameof(EnvironmentWizzardPaneVisible), typeof(Visibility), typeof(PpsSplashWindow), new PropertyMetadata(Visibility.Visible));
		private readonly static DependencyPropertyKey loginStatePropertyKey = DependencyProperty.RegisterReadOnly(nameof(LoginState), typeof(LoginStateData), typeof(PpsSplashWindow), new PropertyMetadata(null));

		public readonly static DependencyProperty LoginPaneVisibleProperty = loginPaneVisiblePropertyKey.DependencyProperty;
		public readonly static DependencyProperty StatusPaneVisibleProperty = statusPaneVisiblePropertyKey.DependencyProperty;
		public readonly static DependencyProperty EnvironmentWizzardPaneVisibleProperty = environmentWizzardPaneVisiblePropertyKey.DependencyProperty;
		public readonly static DependencyProperty StatusTextProperty = DependencyProperty.Register(nameof(StatusText), typeof(string), typeof(PpsSplashWindow));
		public readonly static DependencyProperty LoginStateProperty = loginStatePropertyKey.DependencyProperty;

		private readonly LoginStateData loginStateUnSafe;
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
					new CommandBinding(ApplicationCommands.Save, ExecuteFrame, LoginFrameActive),
					new CommandBinding(ApplicationCommands.Close, CloseFrame, LoginFrameActive)
			}
			// 
			);

			SetValue(loginPaneVisiblePropertyKey, Visibility.Hidden);
			SetValue(statusPaneVisiblePropertyKey, Visibility.Visible);
			SetValue(environmentWizzardPaneVisiblePropertyKey, Visibility.Hidden);
			SetValue(loginStatePropertyKey, loginStateUnSafe = new LoginStateData(this));

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
			SetValue(loginPaneVisiblePropertyKey, Visibility.Hidden);
			SetValue(statusPaneVisiblePropertyKey, Visibility.Hidden);
			SetValue(environmentWizzardPaneVisiblePropertyKey, Visibility.Visible);
			e.Handled = true;
		} // proc CreateNewEnvironment


		private void ExecuteFrame(object sender, ExecutedRoutedEventArgs e)
		{
			if (LoginPaneVisible == Visibility.Visible && loginStateUnSafe.IsValid)
			{
				loginFrame.Continue = false;
				dialogResult = true;
				e.Handled = true;
			}
			else if (EnvironmentWizzardPaneVisible == Visibility.Visible)
			{
				loginFrame.Continue = true;
				e.Handled = true;
				SaveEnvironment();
			}
		} // proc ExecuteLoginFrame

		private void SaveEnvironment()
		{
			// ToDo: create the environment
			var newEnv = new PpsEnvironmentInfo(tbNewEnvironmentName.Text);
			newEnv.Uri = new Uri(tbNewEnvironmentUri.Text);
			loginStateUnSafe.RefreshEnvironments(newEnv);
			SetValue(loginPaneVisiblePropertyKey, Visibility.Visible);
			SetValue(environmentWizzardPaneVisiblePropertyKey, Visibility.Hidden);
		}

		private void AbortEnvironment()
		{
			SetValue(loginPaneVisiblePropertyKey, Visibility.Visible);
			SetValue(environmentWizzardPaneVisiblePropertyKey, Visibility.Hidden);
		}

		private void CloseFrame(object sender, ExecutedRoutedEventArgs e)
		{
			if (LoginPaneVisible == Visibility.Visible)
			{
				loginFrame.Continue = false;
				dialogResult = false;
				e.Handled = true;
			}
			else if (EnvironmentWizzardPaneVisible == Visibility.Visible)
			{
				loginFrame.Continue = true;
				e.Handled = true;
				AbortEnvironment();
			}
		} // proc CloseLoginFrame

		private Tuple<PpsEnvironmentInfo, ICredentials> ShowLogin()
		{
			SetValue(loginPaneVisiblePropertyKey, Visibility.Visible);
			SetValue(statusPaneVisiblePropertyKey, Visibility.Hidden);
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
					using (var plc = new PpsClientLogin(loginStateUnSafe.CurrentEnvironment.Uri.ToString(), "", false))
					{
						var newCreds = (NetworkCredential)loginStateUnSafe.GetCredentials();
						if (newCreds != null && plc != null
							&& (plc.UserName != newCreds.UserName || !PpsProcs.SecureStringCompare(plc.GetPassword(), newCreds.SecurePassword))
							&& MessageBox.Show("Kennwort speichern?", "PPSn", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
						{
							plc.UserName = newCreds.UserName;
							plc.SetPassword(newCreds.SecurePassword);
							plc.Save = true;
							plc.Commit();
						}
					}

					return new Tuple<PpsEnvironmentInfo, ICredentials>(loginStateUnSafe.CurrentEnvironment, loginStateUnSafe.GetCredentials());
				}
				else
					return null;
			}
			finally
			{
				SetValue(loginPaneVisiblePropertyKey, Visibility.Hidden);
				SetValue(statusPaneVisiblePropertyKey, Visibility.Visible);
			}
		} // proc ShowLogin

		public async Task<Tuple<PpsEnvironmentInfo, ICredentials>> ShowLoginAsync(PpsEnvironmentInfo selectEnvironment)
		{
			loginStateUnSafe.RefreshEnvironments(selectEnvironment);

			return await Dispatcher.InvokeAsync(ShowLogin);
		} // func ShowLoginAsync

		#endregion

		#region -- Progress -------------------------------------------------------------

		public void SetProgressTextAsync(string text)
			=> Dispatcher.BeginInvoke(new Action<string>(t => StatusText = t), DispatcherPriority.Normal, text);

		void IProgress<string>.Report(string text)
			=> SetProgressTextAsync(text);

		#endregion

		#region -- SetError -------------------------------------------------------------

		private void SetError(object errorInfo)
		{
			if (errorInfo is Exception) // show exception
			{
				errorInfo = ((Exception)errorInfo).ToString();
			}

			MessageBox.Show(errorInfo.ToString(), "Fehler", MessageBoxButton.OK, MessageBoxImage.Information);
		} // proc SetError

		public async Task SetErrorAsync(object errorInfo)
		{
			if (Dispatcher.CheckAccess())
				SetError(errorInfo);
			else
				await Dispatcher.InvokeAsync(() => SetError(errorInfo));
		} // proc SetErrorAsync

		#endregion

		public Visibility LoginPaneVisible => (Visibility)GetValue(LoginPaneVisibleProperty);
		public Visibility StatusPaneVisible => (Visibility)GetValue(StatusPaneVisibleProperty);
		public Visibility EnvironmentWizzardPaneVisible => (Visibility)GetValue(EnvironmentWizzardPaneVisibleProperty);
		public string StatusText { get { return (string)GetValue(StatusTextProperty); } set { SetValue(StatusTextProperty, value); } }
		public LoginStateData LoginState => (LoginStateData)GetValue(LoginStateProperty);

		private void Window_Drag(object sender, MouseButtonEventArgs e)
		{
			if (e.ChangedButton == MouseButton.Left)
				DragMove();
		} // event Window_Drag

		/*private void Enviroments_SelectionChanged(object sender, SelectionChangedEventArgs e)
			=> loginStateUnSafe.CurrentEnvironment = (PpsEnvironmentInfo)((ComboBox)sender).SelectedItem;*/

	} // class PpsSplashWindow
}
