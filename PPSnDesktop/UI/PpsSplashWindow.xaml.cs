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
		public sealed class LoginStateData : INotifyPropertyChanged, IDisposable
		{
			public event PropertyChangedEventHandler PropertyChanged;

			private readonly PpsSplashWindow parent;

			private PpsEnvironmentInfo[] environments = null;
			private PpsEnvironmentInfo currentEnvironment = null;
			private PpsClientLogin currentLogin = null;
			private bool passwordHasChanged = false;

			public LoginStateData(PpsSplashWindow parent)
			{
				this.parent = parent;
			} // ctor

			public void Dispose()
			{
				currentLogin?.Dispose();
			} // proc Dispose

			private void OnPropertyChanged(string propertyName)
				=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

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
							passwordHasChanged = false;
						}

						// mark properties as changed
						OnPropertyChanged(nameof(CurrentEnvironment));
						OnPropertyChanged(nameof(UserName));
						OnPropertyChanged(nameof(SavePassword));

						OnPropertyChanged(nameof(IsUserNameEnabled));
						OnPropertyChanged(nameof(IsPasswordEnabled));
						OnPropertyChanged(nameof(IsValid));
					}
				}
			} // prop CurrentEnvironment

			public string UserName
			{
				get => currentLogin?.UserName;
				set
				{
					if (currentLogin != null)
					{
						currentLogin.UserName = value;
						if (currentLogin.IsDefaultUserName) // clear password
							parent.pbPassword.Password = String.Empty;

						OnPropertyChanged(nameof(UserName));
						OnPropertyChanged(nameof(IsPasswordEnabled));
						Validate(false);
					}
				}
			} // prop UserName

			public void Validate(bool passwordHasChanged)
			{
				if (passwordHasChanged)
					this.passwordHasChanged = passwordHasChanged;
				OnPropertyChanged(nameof(IsValid));
			} // proc Validate

			public bool IsUserNameEnabled => currentLogin != null;
			public bool IsPasswordEnabled => currentLogin != null && !currentLogin.IsDefaultUserName;

			public bool HasParentPassword => parent.pbPassword.SecurePassword != null && parent.pbPassword.SecurePassword.Length > 0;

			public bool IsValid => currentLogin != null && (currentLogin.IsDefaultUserName || HasParentPassword);

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

			private string newEnvironmentName = String.Empty;
			public string NewEnvironmentName { get { return newEnvironmentName; } set { this.newEnvironmentName = value; } }
			private string newEnvironmentUri = String.Empty;
			public string NewEnvironmentUri { get { return newEnvironmentUri; } set { this.newEnvironmentUri = value; } }
			public bool NewEnvironmentIsValid => !String.IsNullOrWhiteSpace(NewEnvironmentName) && Uri.IsWellFormedUriString(NewEnvironmentUri, UriKind.Absolute);   //ToDo: check if that Environment already exists!
		} // class LoginStateData

		#endregion

		private readonly static DependencyPropertyKey loginStatePropertyKey = DependencyProperty.RegisterReadOnly(nameof(LoginState), typeof(LoginStateData), typeof(PpsSplashWindow), new PropertyMetadata(null));

		public readonly static DependencyProperty StatusTextProperty = DependencyProperty.Register(nameof(StatusText), typeof(string), typeof(PpsSplashWindow));
		public readonly static DependencyProperty ErrorTextProperty = DependencyProperty.Register(nameof(ErrorText), typeof(string), typeof(PpsSplashWindow));
		public readonly static DependencyProperty ActivePageNumProperty = DependencyProperty.Register(nameof(ActivePageNum), typeof(int), typeof(PpsSplashWindow));
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
							e.CanExecute = (ActivePage == Panes.NewEnvironment && loginStateUnSafe.NewEnvironmentIsValid)
								|| (ActivePage == Panes.Login && loginStateUnSafe.IsValid); e.Handled = true;
						}
					),
					new CommandBinding(ApplicationCommands.Close, CloseFrame, LoginFrameActive),
					new CommandBinding(EnterKeyCommand,
						(sender, e) => EnterKey(sender, e)
					),
					new CommandBinding(PressedKeyCommand,
						(sender, e) => loginStateUnSafe.Validate(false)
					),
					new CommandBinding(ReStartCommand,
						(sender, e) =>
						{
							ActivePage = Panes.Login;
						}
					),
					new CommandBinding(ShowErrorDetailsCommand,
						(sender, e) =>
						{
							errorEnvironment.ShowTrace(this.Owner);
						},
						(sender, e) =>
						{
							e.CanExecute = errorEnvironment != null;
						}
					)
				}
			);

			ActivePage = Panes.Status;
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

		protected override void OnClosed(EventArgs e)
		{
			base.OnClosed(e);
			loginStateUnSafe?.Dispose();
		} // proc Dispose

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
			var newEnv = new PpsEnvironmentInfo(loginStateUnSafe.NewEnvironmentName)
			{
				Uri = new Uri(loginStateUnSafe.NewEnvironmentUri)
			};
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

		private Tuple<PpsEnvironmentInfo, NetworkCredential> ShowLogin()
		{
			if (ActivePage != Panes.Error && ActivePage != Panes.Login)
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
					return new Tuple<PpsEnvironmentInfo, NetworkCredential>(loginStateUnSafe.CurrentEnvironment, loginStateUnSafe.GetCredentials());
				else
					return null;
			}
			finally
			{
				ActivePage = Panes.Status;
			}
		} // proc ShowLogin

		public async Task<Tuple<PpsEnvironmentInfo, NetworkCredential>> ShowLoginAsync(PpsEnvironmentInfo selectEnvironment, NetworkCredential userInfo = null)
		{
			loginStateUnSafe.RefreshEnvironments(selectEnvironment);

			if (userInfo != null)
				Dispatcher.Invoke(() => loginStateUnSafe.UserName = userInfo.UserName);

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
				errorInfo = ((Exception)errorInfo).Message;

			SetValue(ErrorTextProperty, errorInfo);
			ActivePage = Panes.Error;
		} // proc SetError

		private PpsEnvironment errorEnvironment;
		private void SetErrorEnvironment(PpsEnvironment environment)
		{
			errorEnvironment = environment;
		}

		public async Task SetErrorAsync(object errorInfo, PpsEnvironment environment)
		{
			if (Dispatcher.CheckAccess())
			{
				SetError(errorInfo);
				SetErrorEnvironment(environment);
			}
			else
			{
				await Dispatcher.InvokeAsync(() => SetError(errorInfo));
				await Dispatcher.InvokeAsync(() => SetErrorEnvironment(environment));
			}
		} // proc SetErrorAsync

		#endregion

		public string StatusText
		{
			get { return (string)GetValue(StatusTextProperty); }
			set { SetValue(StatusTextProperty, value); }
		} // prop StatusText

		public string ErrorText
		{
			get { return (string)GetValue(ErrorTextProperty); }
			set { SetValue(ErrorTextProperty, value); }
		} // prop ErrorText

		public LoginStateData LoginState => (LoginStateData)GetValue(LoginStateProperty);

		public int ActivePageNum { get => (int)GetValue(ActivePageNumProperty); set => SetValue(ActivePageNumProperty, (int)value); }
		public Panes ActivePage { get => (Panes)ActivePageNum; set => ActivePageNum = (int)value; }

		private void Window_Drag(object sender, MouseButtonEventArgs e)
		{
			if (e.ChangedButton == MouseButton.Left)
				DragMove();
		} // event Window_Drag

		#region -- RoutedUICommand ------------------------------------------------------

		public static RoutedUICommand EnterKeyCommand { get; } = new RoutedUICommand("EnterKey", "EnterKey", typeof(PpsSplashWindow));
		public static RoutedUICommand PressedKeyCommand { get; } = new RoutedUICommand("PressedKey", "PressedKey", typeof(PpsSplashWindow));
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
					if (loginStateUnSafe.IsValid)
						ApplicationCommands.Save.Execute(null, null);
					else
						textBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
			}
		}

		#endregion

		private void PasswordChanged(object sender, RoutedEventArgs e)
			=> loginStateUnSafe.Validate(true);
	} // class PpsSplashWindow
}
