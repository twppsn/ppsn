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
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using TecWare.DE.Data;
using TecWare.PPSn.Data;
using TecWare.PPSn.Properties;

namespace TecWare.PPSn.UI
{
	/// <summary>Login and loading dialog.</summary>
	public partial class PpsSplashWindow : Window, IProgress<string>
	{
		#region -- enum StatePanes ----------------------------------------------------

		private enum StatePanes : int
		{
			Status = 0,
			NewEnvironment,
			Login,
			Error
		} // prop Panse

		#endregion

		#region -- class LoginState ---------------------------------------------------

		/// <summary>State of the login data.</summary>
		public sealed class LoginStateData : INotifyPropertyChanged, IDisposable
		{
			/// <summary>Property changed event.</summary>
			public event PropertyChangedEventHandler PropertyChanged;

			private readonly PpsSplashWindow parent;

			private PpsEnvironmentInfo[] environments = null;
			private PpsEnvironmentInfo currentEnvironment = null;
			private PpsClientLogin currentLogin = null;
			private bool passwordHasChanged = false;

			#region -- Ctor/Dtor ------------------------------------------------------

			internal LoginStateData(PpsSplashWindow parent)
			{
				this.parent = parent;
			} // ctor

			/// <summary>Release system resources</summary>
			public void Dispose()
			{
				currentLogin?.Dispose();
			} // proc Dispose

			private void OnPropertyChanged(string propertyName)
				=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

			#endregion

			/// <summary>Get current selected credentials (login data)</summary>
			/// <returns></returns>
			public NetworkCredential GetCredentials()
			{
				if (currentLogin == null)
					return null;

				// update password
				if (HasParentPassword && passwordHasChanged)
					currentLogin.SetPassword(parent.pbPassword.SecurePassword);
				currentLogin.Commit();
				
				return currentLogin.GetCredentials();
			} // func GetCredentials

			/// <summary>Refresh environment list.</summary>
			/// <param name="getCurrentEnvironment"></param>
			/// <returns></returns>
			public async Task RefreshEnvironmentsAsync(Func<PpsEnvironmentInfo[], PpsEnvironmentInfo> getCurrentEnvironment)
			{
				// parse all environment
				environments = await Task.Run(() => PpsEnvironmentInfo.GetLocalEnvironments().ToArray());
				OnPropertyChanged(nameof(Environments));

				// update current environment
				if (environments.Length > 0)
					CurrentEnvironment = getCurrentEnvironment?.Invoke(environments) ?? environments[0];
			} // proc RefreshEnvironments

			/// <summary>Access a environment list</summary>
			public PpsEnvironmentInfo[] Environments => environments;

			/// <summary>Current selected environment</summary>
			public PpsEnvironmentInfo CurrentEnvironment
			{
				get => currentEnvironment;
				set
				{
					if (currentEnvironment == value)
						return;
					currentEnvironment = value;

					if (currentEnvironment?.Uri != null)
					{
						// change login
						currentLogin?.Dispose();
						currentLogin = new PpsClientLogin("ppsn_env:" + currentEnvironment.Uri.ToString(), currentEnvironment.Name, false);

						// currect save options
						if (currentLogin.SaveOptions == PpsClientLoginSaveOptions.None)
							currentLogin.SaveOptions = PpsClientLoginSaveOptions.UserName; // at least write a username

						// set dummy password
						if (currentLogin.PasswordLength > 0)
							parent.pbPassword.Password = new string('\x01', currentLogin.PasswordLength);
						if (!IsPasswordEnabled)
							parent.pbPassword.Password = String.Empty;
						passwordHasChanged = false;
					}

					// mark properties as changed
					OnPropertyChanged(nameof(CurrentEnvironment));
					OnPropertyChanged(nameof(UserName));
					OnPropertyChanged(nameof(SavePassword));

					OnPropertyChanged(nameof(IsUserNameEnabled));
					OnPropertyChanged(nameof(IsPasswordEnabled));
					OnPropertyChanged(nameof(IsPasswordSaveEnabled));
					OnPropertyChanged(nameof(IsValid));
				}
			} // prop CurrentEnvironment

			/// <summary>Return user name</summary>
			public string UserName
			{
				get => currentLogin?.UserName;
				set
				{
					if (currentLogin != null)
					{
						currentLogin.UserName = value;
						if (currentLogin.IsDefaultUserName) // clear password
						{
							parent.pbPassword.Password = String.Empty;
							SavePassword = false;
							OnPropertyChanged(nameof(SavePassword));
						}
						OnPropertyChanged(nameof(UserName));
						OnPropertyChanged(nameof(IsPasswordEnabled));

						Validate(false);
					}
				}
			} // prop UserName

			/// <summary>Validate input</summary>
			/// <param name="passwordHasChanged"></param>
			public void Validate(bool passwordHasChanged)
			{
				if (passwordHasChanged)
				{
					this.passwordHasChanged = passwordHasChanged;
					OnPropertyChanged(nameof(IsPasswordSaveEnabled));
				}
				OnPropertyChanged(nameof(IsValid));
			} // proc Validate

			/// <summary>Is user name editable.</summary>
			public bool IsUserNameEnabled => currentLogin != null;
			/// <summary>Is password enabled</summary>
			public bool IsPasswordEnabled => currentLogin != null && !currentLogin.IsDefaultUserName;
			/// <summary>Is password save enabled</summary>
			public bool IsPasswordSaveEnabled => currentLogin != null && !currentLogin.IsDefaultUserName && HasParentPassword;
			/// <summary>Is current login data valid.</summary>
			public bool IsValid => currentLogin != null && (currentLogin.IsDefaultUserName || HasParentPassword);

			/// <summary>Has the password box a password.</summary>
			public bool HasParentPassword => parent.pbPassword.SecurePassword != null && parent.pbPassword.SecurePassword.Length > 0;

			/// <summary>Option if the password should be saved.</summary>
			public bool SavePassword
			{
				get => currentLogin?.SaveOptions == PpsClientLoginSaveOptions.Password;
				set
				{
					if (currentLogin != null)
						currentLogin.SaveOptions = value
							? PpsClientLoginSaveOptions.Password
							: PpsClientLoginSaveOptions.UserName;
				}							
			} // prop SavePassword
		} // class LoginStateData

		#endregion

		#region -- class EditLoginStateData -------------------------------------------

		/// <summary></summary>
		public sealed class EditLoginStateData : ObservableObject
		{
			private string newEnvironmentName = String.Empty;
			public string NewEnvironmentName
			{
				get => newEnvironmentName;
				set
				{
					// because the name is later used as the directory name, it has to be sanitized
					var cleanName = new StringBuilder();
					// only get the chars once
					var illegalChars = Path.GetInvalidPathChars().Union(Path.GetInvalidFileNameChars());
					foreach (var ch in value)
						if (illegalChars.Contains(ch))
							cleanName.Append('_');
						else
							cleanName.Append(ch);
					Set(ref newEnvironmentName, cleanName.ToString(), nameof(NewEnvironmentName));
				}
			}
			private string newEnvironmentUri = String.Empty;
			public string NewEnvironmentUri { get => newEnvironmentUri; set => Set(ref newEnvironmentUri, value, nameof(NewEnvironmentUri)); }
			public bool NewEnvironmentIsValid
			{
				get
				{
					if (String.IsNullOrWhiteSpace(NewEnvironmentName))
						return false;
					if (!Uri.IsWellFormedUriString(NewEnvironmentUri, UriKind.Absolute))
						return false;

					// fastest check if EnvironmentName already exists or if the directory is otherwise already existing
					if (Directory.Exists(Path.GetFullPath(Path.Combine(PpsEnvironmentInfo.LocalEnvironmentsPath, NewEnvironmentName))))
						return false;

					// check if the Uri is already configured
					if (PpsEnvironmentInfo.GetLocalEnvironments().Any(env => env.Uri.Equals(new Uri(NewEnvironmentUri + (NewEnvironmentUri.EndsWith("/") ? String.Empty : "/"), UriKind.Absolute))))
						return false;

					return true;
				}
			}
		} // class EditLoginStateData

		#endregion

		#region -- class ErrorStateData -----------------------------------------------

		/// <summary></summary>
		public sealed class ErrorStateData : ObservableObject
		{
			private string errorText = null;

			/// <summary>Error Text</summary>
			public string ErrorText { get => errorText; set => Set(ref errorText, value, nameof(ErrorText)); }
		} // class EErrorStateDatarrorState

		#endregion

		private readonly static DependencyPropertyKey loginStatePropertyKey = DependencyProperty.RegisterReadOnly(nameof(LoginState), typeof(LoginStateData), typeof(PpsSplashWindow), new FrameworkPropertyMetadata(null));
		private readonly static DependencyPropertyKey errorStatePropertyKey = DependencyProperty.RegisterReadOnly(nameof(ErrorState), typeof(ErrorStateData), typeof(PpsSplashWindow), new FrameworkPropertyMetadata(null));
		private readonly static DependencyPropertyKey editLoginStatePropertyKey = DependencyProperty.RegisterReadOnly(nameof(EditLoginState), typeof(EditLoginStateData), typeof(PpsSplashWindow), new FrameworkPropertyMetadata(null));

		public readonly static DependencyProperty StatusTextProperty = DependencyProperty.Register(nameof(StatusText), typeof(string), typeof(PpsSplashWindow), new FrameworkPropertyMetadata(null));
		public readonly static DependencyProperty LoginStateProperty = loginStatePropertyKey.DependencyProperty;
		public readonly static DependencyProperty ErrorStateProperty = errorStatePropertyKey.DependencyProperty;
		public readonly static DependencyProperty EditLoginStateProperty = editLoginStatePropertyKey.DependencyProperty;

		public readonly static DependencyProperty ActivePageNumProperty = DependencyProperty.Register("ActivePageNum", typeof(int), typeof(PpsSplashWindow), new FrameworkPropertyMetadata(0));

		private bool dialogResult = false;
		private DispatcherFrame loginFrame = null;
		private bool allowClose = false;

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
							e.CanExecute = (ActivateState == StatePanes.NewEnvironment && EditLoginState.NewEnvironmentIsValid)
								|| (ActivateState == StatePanes.Login && LoginState.IsValid);
							e.Handled = true;
						}
					),
					new CommandBinding(ApplicationCommands.Close, CloseFrame, LoginFrameActive),
					new CommandBinding(EnterKeyCommand,
						(sender, e) =>
						{
							EnterKey(sender, e);
							e.Handled = true;
						}
					),
					new CommandBinding(ReStartCommand,
						(sender, e) =>
						{
							ActivateState = StatePanes.Login;
							e.Handled = true;
						}
					),
					new CommandBinding(ShowErrorDetailsCommand,
						(sender, e) =>
						{
							errorEnvironment.ShowTrace(Owner);
							e.Handled = true;
						},
						(sender, e) =>
						{
							e.CanExecute = errorEnvironment != null;
							e.Handled = true;
						}
					)
				}
			);

			ActivateState = StatePanes.Status;
			SetValue(loginStatePropertyKey, new LoginStateData(this));
			SetValue(errorStatePropertyKey, new ErrorStateData());
			SetValue(editLoginStatePropertyKey, new EditLoginStateData());

			this.DataContext = this;
		} // ctor

		protected override void OnClosing(CancelEventArgs e)
		{
			if (loginFrame != null)
			{
				ApplicationCommands.Close.Execute(null, this);
				e.Cancel = true;
			}
			else
				e.Cancel = !allowClose;
			
			base.OnClosing(e);
		} // proc OnClosing

		protected override void OnClosed(EventArgs e)
		{
			base.OnClosed(e);
			LoginState?.Dispose();
		} // proc Dispose

		public void ForceClose()
		{
			allowClose = true;
			Close();
		} // proc ForceClose

		protected override void OnMouseDown(MouseButtonEventArgs e)
		{
			base.OnMouseDown(e);
			if (!e.Handled && e.ChangedButton == MouseButton.Left)
			{
				e.Handled = true;
				DragMove();
			}
		} // proc OnMouseDown

		#endregion

		#region -- Login ----------------------------------------------------------------

		private void LoginFrameActive(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = loginFrame != null;
			e.Handled = true;
		} // proc LoginFrameActive

		private void CreateNewEnvironment(object sender, ExecutedRoutedEventArgs e)
		{
			ActivateState = StatePanes.NewEnvironment;
			e.Handled = true;
		} // proc CreateNewEnvironment

		private void ExecuteFrame(object sender, ExecutedRoutedEventArgs e)
		{
			if (ActivateState == StatePanes.Login && LoginState.IsValid)
			{
				loginFrame.Continue = false;
				dialogResult = true;
				e.Handled = true;
			}
			else if (ActivateState == StatePanes.NewEnvironment)
			{
				loginFrame.Continue = true;
				e.Handled = true;
				SaveEnvironmentAsync().AwaitTask();
			}
		} // proc ExecuteLoginFrame

		private async Task SaveEnvironmentAsync()
		{
			var newEnv = new PpsEnvironmentInfo(EditLoginState.NewEnvironmentName)
			{
				Uri = new Uri(EditLoginState.NewEnvironmentUri)
			};
			await Task.Run(new Action(newEnv.Save));
			await LoginState.RefreshEnvironmentsAsync(l => l.Contains(newEnv) ? newEnv : null);

			ActivateState = StatePanes.Login;
		} // procSaveEnvironmentAsync

		private void AbortEnvironment()
		{
			ActivateState = StatePanes.Login;
		}

		private void CloseFrame(object sender, ExecutedRoutedEventArgs e)
		{
			switch (ActivateState)
			{
				case StatePanes.Login:
					{
						loginFrame.Continue = false;
						dialogResult = false;
						e.Handled = true;
					}
					break;
				case StatePanes.NewEnvironment:
					{
						loginFrame.Continue = true;
						e.Handled = true;
						AbortEnvironment();
					}
					break;
			}
		} // proc CloseLoginFrame

		private Tuple<PpsEnvironmentInfo, NetworkCredential> ShowLogin(PpsEnvironmentInfo selectEnvironment, NetworkCredential userInfo = null)
		{
			var loginState = LoginState;

			// refresh environments
			loginState.RefreshEnvironmentsAsync(envs =>
				{
					if (selectEnvironment != null && envs.Contains(selectEnvironment))
						return selectEnvironment;
					else
					{
						var name = Settings.Default.LastEnvironmentName;
						return String.IsNullOrEmpty(name)
							? null
							: envs.FirstOrDefault(e => e.Name == name);
					}
				}
			).AwaitTask();
			if (userInfo != null)
				loginState.UserName = PpsEnvironmentInfo.GetUserNameFromCredentials(userInfo);

			// show login page only if there is no error page
			if (ActivateState != StatePanes.Error)
				ActivateState = StatePanes.Login;

			try
			{
				if (loginFrame != null) // we are within a login dialog
					throw new InvalidOperationException();

				// spawn new event loop
				loginFrame = new DispatcherFrame();
				CommandManager.InvalidateRequerySuggested();
				Dispatcher.PushFrame(loginFrame);

				// clear message loop and return result
				loginFrame = null;
				if (dialogResult && loginState.IsValid)
				{
					Settings.Default.LastEnvironmentName = loginState.CurrentEnvironment.Name;
					Settings.Default.LastEnvironmentUri = loginState.CurrentEnvironment.Uri.ToString();
					Settings.Default.Save();

					return new Tuple<PpsEnvironmentInfo, NetworkCredential>(loginState.CurrentEnvironment, loginState.GetCredentials());
				}
				else
					return null;
			}
			finally
			{
				ActivateState = StatePanes.Status;
			}
		} // proc ShowLogin

		public async Task<Tuple<PpsEnvironmentInfo, NetworkCredential>> ShowLoginAsync(PpsEnvironmentInfo selectEnvironment, NetworkCredential userInfo = null)
			=> await Dispatcher.InvokeAsync(() => ShowLogin(selectEnvironment, userInfo));
		
		#endregion

		#region -- Progress -----------------------------------------------------------

		public void SetProgressTextAsync(string text)
			=> Dispatcher.BeginInvoke(new Action<string>(t => StatusText = t), DispatcherPriority.Normal, text);

		void IProgress<string>.Report(string text)
			=> SetProgressTextAsync(text);

		#endregion

		#region -- SetError -----------------------------------------------------------

		private PpsEnvironment errorEnvironment = null;

		private void SetError(object errorInfo)
		{
			if (errorInfo is Exception exceptionInfo) // show exception
				errorInfo = exceptionInfo.Message;
			
			ErrorState.ErrorText = errorInfo is string s ? s: errorInfo?.ToString();
			ActivateState = StatePanes.Error;
		} // proc SetError

		private void SetErrorEnvironment(PpsEnvironment environment)
			=> errorEnvironment = environment;

		public async Task SetErrorAsync(object errorInfo, PpsEnvironment environment)
			=> await Dispatcher.InvokeAsync(
				() =>
				{
					SetError(errorInfo);
					SetErrorEnvironment(environment);
				}
			);

		#endregion

		/// <summary>Status text</summary>
		public string StatusText { get => (string)GetValue(StatusTextProperty); set => SetValue(StatusTextProperty, value); }

		/// <summary>Current login state</summary>
		public LoginStateData LoginState => (LoginStateData)GetValue(LoginStateProperty);
		/// <summary>Current error state</summary>
		public ErrorStateData ErrorState => (ErrorStateData)GetValue(ErrorStateProperty);
		/// <summary>Current edit state</summary>
		public EditLoginStateData EditLoginState => (EditLoginStateData)GetValue(EditLoginStateProperty);

		/// <summary>Current active state.</summary>
		private StatePanes ActivateState { get => (StatePanes)(int)GetValue(ActivePageNumProperty); set => SetValue(ActivePageNumProperty, (int)value); }

		#region -- RoutedUICommand ------------------------------------------------------

		public static RoutedUICommand EnterKeyCommand { get; } = new RoutedUICommand("EnterKey", "EnterKey", typeof(PpsSplashWindow));
		public static RoutedUICommand ReStartCommand { get; } = new RoutedUICommand("ReStart", "ReStart", typeof(PpsSplashWindow));
		public static RoutedUICommand ShowErrorDetailsCommand { get; } = new RoutedUICommand("ShowErrorDetails", "ShowErrorDetails", typeof(PpsSplashWindow));

		private void EnterKey(object sender, ExecutedRoutedEventArgs e)
		{
			if (e.OriginalSource is TextBox || e.OriginalSource is PasswordBox)
			{
				dynamic textBox = e.OriginalSource;
				var bindingExpression = textBox.GetBindingExpression(TextBox.TextProperty);

				if (bindingExpression == null)
					ApplicationCommands.Save.Execute(null, null);
				else if (bindingExpression.ResolvedSourcePropertyName == "UserName")
					if (LoginState.IsValid)
						ApplicationCommands.Save.Execute(null, null);
					else
						textBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
			}
		}

		#endregion

		private void PasswordChanged(object sender, RoutedEventArgs e)
			=> LoginState.Validate(true);

	} // class PpsSplashWindow
}
