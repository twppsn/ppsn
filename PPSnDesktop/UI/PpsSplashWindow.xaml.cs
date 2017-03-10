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
					return new NetworkCredential(defaultUser.UserName, CredentialCache.DefaultNetworkCredentials.SecurePassword);

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
				if (userCred != null && !String.IsNullOrEmpty(userCred.Domain))
					userCred.UserName = userCred.Domain + "\\" + userCred.UserName;
				return userCred;
			}

			public bool IsValid => IsUserNameEnabled && !String.IsNullOrEmpty(UserName) && !IsPasswordEnabled || parent.pbPassword.Password.Length > 0;
			public bool IsUserNameEnabled => currentEnvironment?.Uri != null;
			public bool IsPasswordEnabled => !IsDomainName(defaultUser != null ? defaultUser.UserName : String.Empty) && IsUserNameEnabled;
			public void Validate() => OnPropertyChanged(nameof(IsValid));
			private bool savePassword = false;
			public bool SavePassword
			{ get { return savePassword; } set { savePassword = value; } }
			private int activePage = 0;
			public int ActivePage
			{ get { return activePage; } set { this.activePage = value; } }
			private string newEnvironmentName = String.Empty;
			public string NewEnvironmentName
			{ get { return newEnvironmentName; } set { this.newEnvironmentName = value; } }
			private string newEnvironmentUri = String.Empty;
			public string NewEnvironmentUri
			{ get { return newEnvironmentUri; } set { this.newEnvironmentUri = value; } }
			public bool NewEnvironmentIsValid => !String.IsNullOrWhiteSpace(NewEnvironmentName) && Uri.IsWellFormedUriString(NewEnvironmentUri, UriKind.Absolute);	//ToDo: check if that Environment already exists!
			private static bool IsDomainName(string userName)
				=> userName.StartsWith(System.Environment.UserDomainName + "\\", StringComparison.OrdinalIgnoreCase);

		} // class LoginStateData

		#endregion

		private readonly static DependencyPropertyKey loginStatePropertyKey = DependencyProperty.RegisterReadOnly(nameof(LoginState), typeof(LoginStateData), typeof(PpsSplashWindow), new PropertyMetadata(null));

		public readonly static DependencyProperty StatusTextProperty = DependencyProperty.Register(nameof(StatusText), typeof(string), typeof(PpsSplashWindow));
		public readonly static DependencyProperty ActivePageProperty = DependencyProperty.Register(nameof(ActivePage), typeof(int), typeof(PpsSplashWindow));
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
					new CommandBinding(ApplicationCommands.Save, ExecuteFrame, (sender, e) => { e.CanExecute = ((int)GetValue(ActivePageProperty) == 1 && loginStateUnSafe.NewEnvironmentIsValid) ||
																																			 ((int)GetValue(ActivePageProperty) == 2 && loginStateUnSafe.IsValid); e.Handled = true; }),
					new CommandBinding(ApplicationCommands.Close, CloseFrame, LoginFrameActive),
					new CommandBinding(EnterKeyCommand, (sender, e) =>
					{
						EnterKey(sender, e);
					},
					(sender, e) => { e.CanExecute = true; e.Handled = true; }),
					new CommandBinding(PressedKeyCommand, (sender, e) =>
					{
						PressedKey(sender, e);
					},
					(sender, e) => { e.CanExecute = true; e.Handled = true; })
			}
			// 
			);
			SetValue(ActivePageProperty, 0);
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
			SetValue(ActivePageProperty, 1);
			e.Handled = true;
		} // proc CreateNewEnvironment


		private void ExecuteFrame(object sender, ExecutedRoutedEventArgs e)
		{
			if ((int)GetValue(ActivePageProperty) == 2 && loginStateUnSafe.IsValid)
			{
				loginFrame.Continue = false;
				dialogResult = true;
				e.Handled = true;
			}
			else if((int)GetValue(ActivePageProperty) == 1)
			{
				loginFrame.Continue = true;
				e.Handled = true;
				SaveEnvironment();
			}
		} // proc ExecuteLoginFrame

		private void SaveEnvironment()
		{
			var newEnv = new PpsEnvironmentInfo(loginStateUnSafe.NewEnvironmentName);
			newEnv.Uri = new Uri(loginStateUnSafe.NewEnvironmentUri);
			newEnv.Save();
			loginStateUnSafe.RefreshEnvironments(newEnv);
			SetValue(ActivePageProperty, 2);
		}

		private void AbortEnvironment()
		{
			SetValue(ActivePageProperty, 2);
		}

		private void CloseFrame(object sender, ExecutedRoutedEventArgs e)
		{
			switch ((int)GetValue(ActivePageProperty))
			{
				case 2:
					{
						loginFrame.Continue = false;
						dialogResult = false;
						e.Handled = true;
					}
					break;
				case 1:
					{
						loginFrame.Continue = true;
						e.Handled = true;
						AbortEnvironment();
					}
					break;
			}
		} // proc CloseLoginFrame

		private Tuple<PpsEnvironmentInfo, ICredentials> ShowLogin()
		{
			SetValue(ActivePageProperty, 2);
			try
			{
				if (loginFrame != null)
					throw new InvalidOperationException();

				// spawn new event loop
				loginFrame = new DispatcherFrame();
				CommandManager.InvalidateRequerySuggested();
				Dispatcher.PushFrame(loginFrame);
				loginFrame = null;

				if (dialogResult && loginStateUnSafe.IsValid && loginStateUnSafe.SavePassword)
				{
					using (var plc = new PpsClientLogin(loginStateUnSafe.CurrentEnvironment.Uri.ToString(), "", false))
					{
						var newCreds = (NetworkCredential)loginStateUnSafe.GetCredentials();

						plc.UserName = newCreds.UserName;
						var a = new SecureString();

						if (newCreds.SecurePassword.Length > 0)
							plc.SetPassword(newCreds.SecurePassword);
						plc.Save = true;

						plc.Commit();
					}

					return new Tuple<PpsEnvironmentInfo, ICredentials>(loginStateUnSafe.CurrentEnvironment, loginStateUnSafe.GetCredentials());
				}
				else
					return null;
			}
			finally
			{
				SetValue(ActivePageProperty, 0);
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
		
		public string StatusText { get { return (string)GetValue(StatusTextProperty); } set { SetValue(StatusTextProperty, value); } }
		public LoginStateData LoginState => (LoginStateData)GetValue(LoginStateProperty);
		public int ActivePage => (int)GetValue(ActivePageProperty);

		private void Window_Drag(object sender, MouseButtonEventArgs e)
		{
			if (e.ChangedButton == MouseButton.Left)
				DragMove();
		} // event Window_Drag

		#region -- RoutedUICommand ------------------------------------------------------
		public static RoutedUICommand EnterKeyCommand { get; } = new RoutedUICommand("EnterKey", "EnterKey", typeof(PpsSplashWindow));
		public static RoutedUICommand PressedKeyCommand { get; } = new RoutedUICommand("PressedKey", "PressedKey", typeof(PpsSplashWindow));

		private void EnterKey(object sender, ExecutedRoutedEventArgs e)
		{
			if (e.OriginalSource is TextBox || e.OriginalSource is PasswordBox)
			{
				dynamic textBox = e.OriginalSource;
				var bindingExpression = textBox.GetBindingExpression(TextBox.TextProperty);

				if (bindingExpression == null)
					ApplicationCommands.Save.Execute(null, null);
				else if (bindingExpression.ResolvedSourcePropertyName == "UserName")
					if (loginStateUnSafe.IsValid)
						ApplicationCommands.Save.Execute(null, null);
					else
						textBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
			}
		}

		private void PressedKey(object sender, ExecutedRoutedEventArgs e)
		{
			loginStateUnSafe.Validate();
		}
		#endregion
	} // class PpsSplashWindow
}
