using System;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Security;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using TecWare.PPSn.Data;

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
					return CredentialCache.DefaultNetworkCredentials;

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

						if (environments.Contains(selectEnvironment))
							CurrentEnvironment = selectEnvironment;
						else if (environments.Length > 0)
							CurrentEnvironment = environments[0];
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
						{
							var cred = LoadUserCredentials(currentEnvironment.Uri.ToString());
							DefaultUser = cred;
							if (cred != null)
							{
								SavePassword = true;
								OnPropertyChanged(nameof(SavePassword));
							}
						}

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

					if (defaultUser?.SecurePassword?.Length > 0 && !IsDomainName(defaultUser?.UserName))
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
			public bool SavePassword { get { return savePassword; } set { savePassword = value; } }
			private string newEnvironmentName = String.Empty;
			public string NewEnvironmentName { get { return newEnvironmentName; } set { this.newEnvironmentName = value; } }
			private string newEnvironmentUri = String.Empty;
			public string NewEnvironmentUri { get { return newEnvironmentUri; } set { this.newEnvironmentUri = value; } }
			public bool NewEnvironmentIsValid => !String.IsNullOrWhiteSpace(NewEnvironmentName) && Uri.IsWellFormedUriString(NewEnvironmentUri, UriKind.Absolute);   //ToDo: check if that Environment already exists!
			private static bool IsDomainName(string userName) => String.Compare(userName, System.Environment.UserDomainName + "\\" + System.Environment.UserName, true) == 0;
		} // class LoginStateData

		#endregion

		private readonly static DependencyPropertyKey loginStatePropertyKey = DependencyProperty.RegisterReadOnly(nameof(LoginState), typeof(LoginStateData), typeof(PpsSplashWindow), new PropertyMetadata(null));

		public readonly static DependencyProperty InErrorProperty = DependencyProperty.Register(nameof(InError), typeof(bool), typeof(PpsSplashWindow));
		public readonly static DependencyProperty StatusTextProperty = DependencyProperty.Register(nameof(StatusText), typeof(string), typeof(PpsSplashWindow));
		public readonly static DependencyProperty ErrorTextProperty = DependencyProperty.Register(nameof(ErrorText), typeof(string), typeof(PpsSplashWindow));
		public readonly static DependencyProperty ActivePageProperty = DependencyProperty.Register(nameof(ActivePage), typeof(int), typeof(PpsSplashWindow));
		public readonly static DependencyProperty LoginStateProperty = loginStatePropertyKey.DependencyProperty;

		private readonly LoginStateData loginStateUnSafe;
		private bool dialogResult = false;
		private DispatcherFrame loginFrame = null;
		private bool allowClose = false;

		public enum Panes : int { Status, NewEnvironment, Login, Error }

		#region -- Ctor/Dtor ------------------------------------------------------------

		public PpsSplashWindow()
		{
			InitializeComponent();

			CommandBindings.AddRange(
				new CommandBinding[]
				{
					new CommandBinding(ApplicationCommands.New, CreateNewEnvironment, LoginFrameActive),
					new CommandBinding(ApplicationCommands.Save, ExecuteFrame, 
						(sender, e) => 
						{
							e.CanExecute = ((int)GetValue(ActivePageProperty) == 1 && loginStateUnSafe.NewEnvironmentIsValid)
								|| ((int)GetValue(ActivePageProperty) == 2 && loginStateUnSafe.IsValid); e.Handled = true;
						}
					),
					new CommandBinding(ApplicationCommands.Close, CloseFrame, LoginFrameActive),
					new CommandBinding(EnterKeyCommand, 
						(sender, e) => EnterKey(sender, e)
					),
					new CommandBinding(PressedKeyCommand, 
						(sender, e) =>loginStateUnSafe.Validate()
					),
					new CommandBinding(ReStartCommand, 
						(sender, e) =>
						{
							SetValue(InErrorProperty,false);
							ActivePage = Panes.Login;
						}
					)
				}
			);

			ActivePage = 0; ;
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

		#region -- Login ----------------------------------------------------------------

		private void LoginFrameActive(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = loginFrame != null;
			e.Handled = true;
		} // proc LoginFrameActive

		private void CreateNewEnvironment(object sender, ExecutedRoutedEventArgs e)
		{
			ActivePage = Panes.NewEnvironment;
			e.Handled = true;
		} // proc CreateNewEnvironment


		private void ExecuteFrame(object sender, ExecutedRoutedEventArgs e)
		{
			if (ActivePage == Panes.Login && loginStateUnSafe.IsValid)
			{
				loginFrame.Continue = false;
				dialogResult = true;
				e.Handled = true;
			}
			else if (ActivePage == Panes.NewEnvironment)
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
			ActivePage = Panes.Login;
		}

		private void AbortEnvironment()
		{
			ActivePage = Panes.Login;
		}

		private void CloseFrame(object sender, ExecutedRoutedEventArgs e)
		{
			switch (ActivePage)
			{
				case Panes.Login:
					{
						loginFrame.Continue = false;
						dialogResult = false;
						e.Handled = true;
					}
					break;
				case Panes.NewEnvironment:
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
			if (!(bool)GetValue(InErrorProperty))
				ActivePage = Panes.Login;
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
					if (loginStateUnSafe.SavePassword)
						using (var plc = new PpsClientLogin(loginStateUnSafe.CurrentEnvironment.Uri.ToString(), "", false))
						{
							var newCreds = (NetworkCredential)loginStateUnSafe.GetCredentials();

							plc.UserName = newCreds.UserName;

							//if the app is here and the username is empty a System.Net.SystemNetworkCredential was passed where the username can't be read
							if (String.IsNullOrWhiteSpace(plc.UserName))
							{
								plc.UserName = Environment.UserDomainName + "\\" + Environment.UserName;
								var emptyPass = new SecureString();
								emptyPass.AppendChar(' ');
								plc.SetPassword(emptyPass);
							}

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
				ActivePage = Panes.Status;
			}
		} // proc ShowLogin

		public async Task<Tuple<PpsEnvironmentInfo, ICredentials>> ShowLoginAsync(PpsEnvironmentInfo selectEnvironment, ICredentials userInfo = null)
		{
			loginStateUnSafe.RefreshEnvironments(selectEnvironment);

			if (userInfo != null)
				Dispatcher.Invoke(()=> loginStateUnSafe.UserName = ((NetworkCredential)userInfo).UserName);

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
				errorInfo = ((Exception)errorInfo).Message;
			}
			SetValue(ErrorTextProperty, errorInfo);
			SetValue(InErrorProperty, true);
			ActivePage = Panes.Error;
		} // proc SetError

		public async Task SetErrorAsync(object errorInfo)
		{
			if (Dispatcher.CheckAccess())
				SetError(errorInfo);
			else
				await Dispatcher.InvokeAsync(() => SetError(errorInfo));
		} // proc SetErrorAsync

		#endregion

		public string StatusText
		{
			get { return (string)GetValue(StatusTextProperty); }
			set
			{
				SetValue(StatusTextProperty, value);
				ActivePage = Panes.Status;
			}
		}
		public string ErrorText
		{
			get { return (string)GetValue(ErrorTextProperty); }
			set
			{
				SetValue(ErrorTextProperty, value);
				ActivePage = Panes.Error;
			}
		}

		public bool InError { get { return (bool)GetValue(InErrorProperty); } set { SetValue(InErrorProperty, value); } }
		public LoginStateData LoginState => (LoginStateData)GetValue(LoginStateProperty);
		public Panes ActivePage { get { return (Panes)GetValue(ActivePageProperty); } set { SetValue(ActivePageProperty, (int)value); } }

		private void Window_Drag(object sender, MouseButtonEventArgs e)
		{
			if (e.ChangedButton == MouseButton.Left)
				DragMove();
		} // event Window_Drag

		#region -- RoutedUICommand ------------------------------------------------------

		public static RoutedUICommand EnterKeyCommand { get; } = new RoutedUICommand("EnterKey", "EnterKey", typeof(PpsSplashWindow));
		public static RoutedUICommand PressedKeyCommand { get; } = new RoutedUICommand("PressedKey", "PressedKey", typeof(PpsSplashWindow));
		public static RoutedUICommand ReStartCommand { get; } = new RoutedUICommand("ReStart", "ReStart", typeof(PpsSplashWindow));

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

		#endregion

		private void PasswordChanged(object sender, RoutedEventArgs e)
			=> loginStateUnSafe.Validate();
	} // class PpsSplashWindow
}
