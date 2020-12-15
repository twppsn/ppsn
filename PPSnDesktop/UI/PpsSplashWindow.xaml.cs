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
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using TecWare.DE.Data;
using TecWare.DE.Stuff;
using TecWare.PPSn.Controls;
using TecWare.PPSn.Data;
using TecWare.PPSn.Main;
using TecWare.PPSn.Properties;

namespace TecWare.PPSn.UI
{
	/// <summary>Login and loading dialog.</summary>
	internal partial class PpsSplashWindow : PpsWindow, IPpsProgressFactory
	{
		#region -- enum StatePanes ----------------------------------------------------

		private enum StatePanes : int
		{
			Status = 0,
			ShellList = 1,
			NewShell = 2,
			Login = 3,
			Runtime = 4
		} // prop Panse

		#endregion

		#region -- interface IReturnState ---------------------------------------------

		private interface IReturnState 
		{
			bool Finish(object parameter);
			bool CanFinish(object parameter);

			StatePanes State { get; }
		} // interface IReturnState

		#endregion

		#region -- class ShellInfoData ------------------------------------------------

		public sealed class ShellInfo
		{
			private readonly IPpsShellInfo shellInfo;

			public ShellInfo(IPpsShellInfo shellInfo)
				=> this.shellInfo = shellInfo ?? throw new ArgumentNullException(nameof(shellInfo));

			public override bool Equals(object obj)
				=> obj is ShellInfo o && o.shellInfo.Equals(shellInfo);

			public override int GetHashCode()
				=> shellInfo.GetHashCode();

			public IPpsShellInfo Info => shellInfo;

			public string Name => shellInfo.Name;
			public string DisplayName => shellInfo.DisplayName;
			public string Uri => shellInfo.Uri.ToString();
		} // class ShellInfo

		public sealed class ShellInfoData : ObservableObject, IEnumerable<ShellInfo>, INotifyCollectionChanged, IReturnState, IDisposable
		{
			public event NotifyCollectionChangedEventHandler CollectionChanged;

			private readonly TaskCompletionSource<IPpsShellInfo> returnShellInfo = null;

			private ShellInfo[] shellInfos = Array.Empty<ShellInfo>();

			public ShellInfoData()
			{
				returnShellInfo = new TaskCompletionSource<IPpsShellInfo>();

				Refresh();
			} // ctor

			public void Dispose()
			{
				returnShellInfo.TrySetResult(null);
			} // proc Dispose

			public void Select(IPpsShellInfo shellInfo)
			{
				var shellView = CollectionViewSource.GetDefaultView(this);
				shellView.MoveCurrentTo(new ShellInfo(shellInfo));
			} // proc Select

			public void SelectLast()
			{
				var lastShellName = Settings.Default.LastEnvironmentName;
				var lastUri = Settings.Default.LastEnvironmentUri;
				if (lastShellName != null && lastUri != null)
				{
					var shellView = CollectionViewSource.GetDefaultView(this);

					foreach (var s in shellView.Cast<ShellInfo>())
					{
						if (String.Compare(s.Name, lastShellName, StringComparison.OrdinalIgnoreCase) == 0
							&& String.Compare(s.Uri.ToString(), lastUri, StringComparison.OrdinalIgnoreCase) == 0)
						{
							shellView.MoveCurrentTo(s);
							return;
						}
					}
				}
			} // proc SelectLast

			public void Refresh()
			{
				shellInfos = (from s in PpsShell.GetShellInfo() select new ShellInfo(s)).ToArray();
				CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
			} // proc Refresh

			public IEnumerator<ShellInfo> GetEnumerator()
				=> shellInfos.OfType<ShellInfo>().GetEnumerator();

			System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
				=> GetEnumerator();

			private ShellInfo GetCurrentShellInfo(object parameter)
				=> parameter as ShellInfo ?? CollectionViewSource.GetDefaultView(this)?.CurrentItem as ShellInfo;

			bool IReturnState.Finish(object parameter)
			{
				var shellInfo = GetCurrentShellInfo(parameter);
				if (shellInfo != null)
				{
					Settings.Default.LastEnvironmentName = shellInfo.Name;
					Settings.Default.LastEnvironmentUri = shellInfo.Uri.ToString();
					Settings.Default.Save();

					returnShellInfo.SetResult(shellInfo.Info);
				}
				return true;
			} // func IReturnState.Finish

			bool IReturnState.CanFinish(object parameter)
				=> GetCurrentShellInfo(parameter) != null;

			StatePanes IReturnState.State => StatePanes.ShellList;

			public bool IsOnlyOne()
			{
				if (shellInfos.Length == 1)
				{
					returnShellInfo.SetResult(shellInfos[0].Info);
					return true;
				}
				else
					return false;
			} // func IsOnlyOne

			public Task<IPpsShellInfo> Result => returnShellInfo.Task;
		} // ShellInfoData

		#endregion

		#region -- class EditShellData ------------------------------------------------

		public sealed class EditShellData : ObservableObject, IReturnState
		{
			private readonly PpsSplashWindow splashWindow;
			private string shellName = String.Empty;
			private string shellUri = String.Empty;

			public EditShellData(PpsSplashWindow splashWindow)
				=> this.splashWindow = splashWindow ?? throw new ArgumentNullException(nameof(splashWindow));

			private string CleanName(string name)
			{
				var sb = new StringBuilder();
				foreach(var c in name)
				{
					if (Array.IndexOf(Path.GetInvalidPathChars(), c) >= 0)
						continue;
					if (Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0)
						continue;

					if (Char.IsWhiteSpace(c))
						sb.Append('_');
					else
						sb.Append(c);
				}
				return sb.ToString();
			} // func CleanName

			private bool TryGetParameter(IPpsShellFactory shellFactory, out string instanceName, out string displayName, out Uri uri)
			{
				instanceName = shellName;
				displayName = shellName;
				uri = null;

				// check content -> should not raise
				if (String.IsNullOrWhiteSpace(shellName))
					return false;

				// check name exists
				if (shellFactory.Any(s => String.Compare(s.Name, shellName, StringComparison.OrdinalIgnoreCase) == 0))
				{
					MessageBox.Show("Mandant existiert schon.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
					return false;
				}

				// check uri
				if (String.IsNullOrWhiteSpace(shellUri))
					return false;

				if (!Uri.IsWellFormedUriString(shellUri, UriKind.Absolute))
				{
					MessageBox.Show("Uri ist ungültig.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
					return false;
				}

				uri = new Uri(shellUri, UriKind.Absolute);
				return true;
			} // func TryGetParameter

			bool IReturnState.Finish(object parameter)
			{
				var shellFactory = PpsShell.Global.GetService<IPpsShellFactory>(true);
				if (TryGetParameter(shellFactory, out var instanceName, out var displayName, out var uri))
				{
					var shellInfo = shellFactory.CreateNew(instanceName, displayName, uri);
					
					if (splashWindow.ShellState != null)
					{
						splashWindow.ShellState.Refresh();
						splashWindow.ShellState.Select(shellInfo);
					}
					if (splashWindow.LoginState != null)
						splashWindow.LoginState.UpdateShellInfo(shellInfo);

					return true;
				}
				else
					return false;
			} // func IReturnState.Finish

			bool IReturnState.CanFinish(object parameter)
				=> !String.IsNullOrEmpty(shellName) && !String.IsNullOrEmpty(shellUri);

			StatePanes IReturnState.State => StatePanes.NewShell;

			public string NewName
			{
				get => shellName;
				set => Set(ref shellName, CleanName(value), nameof(NewName));
			} // prop NewName

			public string NewUri
			{
				get => shellUri;
				set => Set(ref shellUri, value, nameof(NewUri));
			} // prop NewUri
		} // class EditShellData

		#endregion

		#region -- class LoginState ---------------------------------------------------

		/// <summary>State of the login data.</summary>
		public sealed class LoginStateData : INotifyPropertyChanged, IReturnState, IDisposable
		{
			public event PropertyChangedEventHandler PropertyChanged;

			private readonly PpsSplashWindow splashWindow;
			private readonly TaskCompletionSource<Tuple<IPpsShellInfo, ICredentials>> result;

			private PpsClientLogin login = null;
			private IPpsShellInfo shellInfo;

			private bool passwordHasChanged = false;

			public LoginStateData(PpsSplashWindow splashWindow, IPpsShellInfo shellInfo, ICredentials credentials)
			{
				this.splashWindow = splashWindow ?? throw new ArgumentNullException(nameof(splashWindow));
				result = new TaskCompletionSource<Tuple<IPpsShellInfo, ICredentials>>();

				UpdateShellInfo(shellInfo ?? throw new ArgumentNullException(nameof(shellInfo)));

				// get user name from parameter
				if (credentials != null)
					UserName = PpsShell.GetUserNameFromCredentials(credentials);
			} // ctor

			public void Dispose()
			{
				result.TrySetResult(new Tuple<IPpsShellInfo, ICredentials>(null, null));
				login?.Dispose();
			} // proc Dispose

			private void OnPropertyChanged(string propertyName)
				=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

			public void UpdateShellInfo(IPpsShellInfo shellInfo)
			{
				this.shellInfo = shellInfo;

				// close old login
				login?.Dispose();

				// create new login
				login = new PpsClientLogin("ppsn_env:" + shellInfo.Uri.ToString(), shellInfo.Name, false);

				// currect save options
				if (login.SaveOptions == PpsClientLoginSaveOptions.None)
					login.SaveOptions = PpsClientLoginSaveOptions.UserName; // at least write a username

				// set default user name
				if (String.IsNullOrEmpty(login.UserName))
					login.UserName = PpsShell.GetUserNameFromCredentials(CredentialCache.DefaultCredentials);

				// set dummy password
				if (login.PasswordLength > 0)
					splashWindow.passwordTextBox.Password = new string('\x01', login.PasswordLength);
				if (!IsPasswordEnabled)
					splashWindow.passwordTextBox.Password = String.Empty;
				passwordHasChanged = false;

				OnPropertyChanged(nameof(ShellName));
				OnPropertyChanged(nameof(UserName));
				OnPropertyChanged(nameof(IsPasswordEnabled));
				OnPropertyChanged(nameof(IsPasswordSaveEnabled));
				OnPropertyChanged(nameof(SavePassword));
			} // proc UpdateShellInfo

			bool IReturnState.Finish(object parameter)
			{
				// update password
				if (HasParentPassword && passwordHasChanged)
					login.SetPassword(splashWindow.passwordTextBox.SecurePassword);
				login.Commit();

				result.SetResult(new Tuple<IPpsShellInfo, ICredentials>(shellInfo, login.GetCredentials()));

				return true;
			} // func IReturnState.Finish

			bool IReturnState.CanFinish(object parameter)
				=> IsValid;
			
			public void Validate(bool passwordHasChanged)
			{
				if (passwordHasChanged)
				{
					this.passwordHasChanged = passwordHasChanged;
					OnPropertyChanged(nameof(IsPasswordSaveEnabled));
				}
				CommandManager.InvalidateRequerySuggested();
			} // proc Validate

			StatePanes IReturnState.State => StatePanes.Login;

			public string ShellName => shellInfo.DisplayName ?? shellInfo.Name;

			public string UserName
			{
				get => login.UserName;
				set
				{
					if (login != null)
					{
						login.UserName = value;
						if (login.IsDefaultUserName) // clear password
						{
							splashWindow.passwordTextBox.Password = String.Empty;
							SavePassword = false;
							OnPropertyChanged(nameof(SavePassword));
						}
						OnPropertyChanged(nameof(UserName));
						OnPropertyChanged(nameof(IsPasswordEnabled));

						Validate(false);
					}
				}
			} // prop UserName

			public bool IsPasswordEnabled => !login.IsDefaultUserName;
			public bool IsPasswordSaveEnabled => !login.IsDefaultUserName && HasParentPassword;

			public bool SavePassword
			{
				get => login.SaveOptions == PpsClientLoginSaveOptions.Password;
				set
				{
					login.SaveOptions = value
						? PpsClientLoginSaveOptions.Password
						: PpsClientLoginSaveOptions.UserName;
				}
			} // prop SavePassword

			internal bool IsValid => login != null && (login.IsDefaultUserName || HasParentPassword);
			private bool HasParentPassword => splashWindow.passwordTextBox.SecurePassword != null && splashWindow.passwordTextBox.SecurePassword.Length > 0;

			public Task<Tuple<IPpsShellInfo, ICredentials>> Result => result.Task;
		} // class LoginStateData

		#endregion

		#region -- class ErrorStateData -----------------------------------------------

		/// <summary></summary>
		public sealed class ErrorStateData : ObservableObject
		{
			private readonly string errorText = null;
			private readonly Exception exceptionInfo;
			private readonly IPpsShell errorShell;

			public ErrorStateData(object errorInfo, IPpsShell errorShell)
			{
				if (errorInfo is Exception exceptionInfo) // show exception
				{
					this.exceptionInfo = exceptionInfo;
					errorInfo = exceptionInfo.GetInnerException().Message;
				}
				else
					this.exceptionInfo = null;

				errorText = errorInfo.ToString();
				this.errorShell = errorShell;
			} // ctor

			public IPpsShell Shell=> errorShell; 
			public bool HasShell => errorShell != null;

			public string Text => errorText;
			public Exception ExceptionInfo => exceptionInfo;
		} // class EErrorStateDatarrorState

		#endregion

		#region -- class RuntimeInstallState ------------------------------------------

		private sealed class RuntimeInstallState : IReturnState, IDisposable
		{
			private readonly TaskCompletionSource<bool> returnState = null;

			public RuntimeInstallState()
			{
				returnState = new TaskCompletionSource<bool>();
			} // ctor

			public void Dispose()
			{
				returnState.TrySetResult(false);
			} // proc Dispose

			bool IReturnState.Finish(object parameter)
			{
				returnState.SetResult(true);
				return true;
			} // func IReturnState.Finish

			bool IReturnState.CanFinish(object parameter)
				=> true;

			StatePanes IReturnState.State => StatePanes.Runtime;

			public Task<bool> Result => returnState.Task;
		} // class RuntimeInstallState

		#endregion

		public static readonly RoutedUICommand ShowErrorDetailsCommand = new RoutedUICommand("ShowErrorDetails", "ShowErrorDetails", typeof(PpsSplashWindow));

		private readonly Stack<IReturnState> dialogStates = new Stack<IReturnState>();
		private readonly PpsProgressStack progressStack;
		private bool allowClose = false;

		#region -- Ctor/Dtor ----------------------------------------------------------

		public PpsSplashWindow()
		{
			InitializeComponent();

			progressStack = PpsWpfShell.CreateProgressStack(Dispatcher);
			progressStack.PropertyChanged += ProgressStack_PropertyChanged;

			CommandBindings.AddRange(
				new CommandBinding[]
				{
					new CommandBinding(ApplicationCommands.New, CreateNewShell, CanCreateNewShell),
					new CommandBinding(ApplicationCommands.Open, FinishState, CanFinishState),
					new CommandBinding(ApplicationCommands.Close, CancelState, CanCancelState),
					new CommandBinding(EnterKeyCommand,
						(sender, e) =>
						{
							EnterKey(sender, e);
							e.Handled = true;
						}
					),
					new CommandBinding(ShowErrorDetailsCommand,
						async (sender, e) =>
						{
							e.Handled = true;
							await ShowTracePaneAsync();
						},
						(sender, e) =>
						{
							e.CanExecute = ErrorState?.Shell != null || ErrorState?.ExceptionInfo != null;
							e.Handled = true;
						}
					)
				}
			);

			ActiveState = StatePanes.Status;

			DataContext = this;
		} // ctor

		private async Task ShowTracePaneAsync()
		{
			if (ErrorState != null)
			{
				if (ErrorState.Shell != null)
				{
					var shell = ErrorState.Shell;
					try
					{
						await shell.GetService<IPpsMainWindowService>(true).OpenPaneAsync(typeof(PpsTracePane), PpsOpenPaneMode.NewSingleWindow);
					}
					catch (Exception ex)
					{
						PpsShell.GetService<IPpsUIService>(true).ShowException(ex, "Kann Fehler nicht anzeigen.");
					}
				}
				else if (ErrorState.ExceptionInfo != null)
				{
					MessageBox.Show(ErrorState.ExceptionInfo.GetMessageString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				}
			}
		} // func ShowTracePaneAsync

		private void PushState(IReturnState state)
		{
			dialogStates.Push(state);
			ActiveState = state.State;
			CommandManager.InvalidateRequerySuggested();
		} // proc PushState

		private void PopState()
		{
			if (dialogStates.Pop() is IDisposable d)
				d.Dispose();

			ActiveState = dialogStates.Count > 0 ? dialogStates.Peek().State : StatePanes.Status;
			CommandManager.InvalidateRequerySuggested();
		} // prop PopState

		private void FinishState(object sender, ExecutedRoutedEventArgs e)
		{
			if (dialogStates.Count > 0)
			{
				var top = dialogStates.Peek();
				if (top.CanFinish(e.Parameter) && top.Finish(e.Parameter))
					PopState();
			}
			e.Handled = true;
		} // proc ExecuteResult

		private void CanFinishState(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = dialogStates.Count > 0 && dialogStates.Peek().CanFinish(e.Parameter);
			e.Handled = true;
		} // proc ExecuteResult

		private void CancelState(object sender, ExecutedRoutedEventArgs e)
		{
			if (dialogStates.Count > 0)
				PopState();
		} // proc CancelState

		private void CanCancelState(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = dialogStates.Count > 0;
			e.Handled = true;
		} // proc CanCancelState

		protected override void OnClosing(CancelEventArgs e)
		{
			if (dialogStates.Count > 0)
			{
				ApplicationCommands.Close.Execute(null, this);
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

		protected override void OnMouseDown(MouseButtonEventArgs e)
		{
			base.OnMouseDown(e);
			if (!e.Handled && e.ChangedButton == MouseButton.Left)
			{
				var pt = e.GetPosition(this);
				var ht = VisualTreeHelper.HitTest(this, pt);
				if (ht != null && ht.VisualHit != null)
				{
					var b = ht.VisualHit as Border ?? PpsWpfShell.GetVisualParent<Border>(ht.VisualHit);
					if (b.CompareName("leftBar") == 0)
					{
						e.Handled = true;
						DragMove();
					}
				}
			}
		} // proc OnMouseDown

		#endregion

		#region -- StatusText, StatusValue - property ---------------------------------

		public static readonly DependencyProperty StatusTextProperty = DependencyProperty.Register(nameof(StatusText), typeof(string), typeof(PpsSplashWindow), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnStatusTextChanged)));
		public static readonly DependencyProperty StatusValueProperty = DependencyProperty.Register(nameof(StatusValue), typeof(int), typeof(PpsSplashWindow), new FrameworkPropertyMetadata(-1, new PropertyChangedCallback(OnStatusValueChanged)));

		private string statusText = null;
		private int statusValue = -1;

		private static void OnStatusTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsSplashWindow)d).statusText = (string)e.NewValue;

		private static void OnStatusValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsSplashWindow)d).statusValue = (int)e.NewValue;

		private void ProgressStack_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(PpsProgressStack.CurrentProgress))
				StatusValue = progressStack.CurrentProgress;
			else if (e.PropertyName == nameof(PpsProgressStack.CurrentText))
				StatusText = progressStack.CurrentText;
		} // event ProgressStack_PropertyChanged

		/// <summary>Set or get text</summary>
		public string StatusText { get => (string)GetValue(StatusTextProperty); set => SetValue(StatusTextProperty, value); }
		/// <summary>Set or get progress</summary>
		public int StatusValue { get => (int)GetValue(StatusValueProperty); set => SetValue(StatusValueProperty, value); }

		#endregion

		#region -- ShellState - property ----------------------------------------------

		private static readonly DependencyPropertyKey shellStatePropertyKey = DependencyProperty.RegisterReadOnly(nameof(ShellState), typeof(ShellInfoData), typeof(PpsSplashWindow), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty ShellStateProperty = shellStatePropertyKey.DependencyProperty;

		public Task<IPpsShellInfo> ShowShellAsync(IPpsShellInfo shellInfo, bool enforceShellSelection)
		{
			// init shell data
			var shell = new ShellInfoData();
			if (!enforceShellSelection && shell.IsOnlyOne())
				return shell.Result;

			SetValue(shellStatePropertyKey, shell);
			if (shellInfo != null)
				shell.Select(shellInfo);
			else
				shell.SelectLast();

			PushState(shell);

			return shell.Result;
		} // proc ShowShellAsync

		public ShellInfoData ShellState => (ShellInfoData)GetValue(ShellStateProperty);

		#endregion

		#region -- EditShellState - property ------------------------------------------

		private readonly static DependencyPropertyKey editShellStatePropertyKey = DependencyProperty.RegisterReadOnly(nameof(EditShellState), typeof(EditShellData), typeof(PpsSplashWindow), new FrameworkPropertyMetadata(null));
		public readonly static DependencyProperty EditShellStateProperty = editShellStatePropertyKey.DependencyProperty;

		private void CreateNewShell(object sender, ExecutedRoutedEventArgs e)
		{
			var edit = new EditShellData(this);
			SetValue(editShellStatePropertyKey, edit);
			PushState(edit);
			
			e.Handled = true;
		} // proc CreateNewShell

		private void CanCreateNewShell(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = ActiveState == StatePanes.ShellList || ActiveState == StatePanes.Login;
			e.Handled = true;
		} // proc CanCreateNewShell

		public EditShellData EditShellState => (EditShellData)GetValue(EditShellStateProperty);

		#endregion

		#region -- LoginState - property ----------------------------------------------

		public static readonly RoutedUICommand EnterKeyCommand = new RoutedUICommand("EnterKey", "EnterKey", typeof(PpsSplashWindow));

		private readonly static DependencyPropertyKey loginStatePropertyKey = DependencyProperty.RegisterReadOnly(nameof(LoginState), typeof(LoginStateData), typeof(PpsSplashWindow), new FrameworkPropertyMetadata(null));
		public readonly static DependencyProperty LoginStateProperty = loginStatePropertyKey.DependencyProperty;

		public Task<Tuple<IPpsShellInfo, ICredentials>> ShowLoginAsync(IPpsShell shell, ICredentials userInfo = null)
		{
			var login = new LoginStateData(this, shell.Info, userInfo);
			SetValue(loginStatePropertyKey, login);
			PasswordChanged(null, null);

			PushState(login);
			return login.Result;
		} // func ShowLoginAsync

		private void PasswordChanged(object sender, RoutedEventArgs e)
			=> LoginState?.Validate(true);

		private void EnterKey(object sender, ExecutedRoutedEventArgs e)
		{
			if (e.OriginalSource is TextBox || e.OriginalSource is PasswordBox)
			{
				dynamic textBox = e.OriginalSource;
				var bindingExpression = textBox.GetBindingExpression(TextBox.TextProperty);

				if (bindingExpression == null)
					ApplicationCommands.Open.Execute(null, null);
				else if (bindingExpression.ResolvedSourcePropertyName == "UserName")
					if (LoginState.IsValid)
						ApplicationCommands.Open.Execute(null, null);
					else
						textBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
			}
		} // proc EnterKey

		public LoginStateData LoginState => (LoginStateData)GetValue(LoginStateProperty);

		#endregion

		#region -- Progress -----------------------------------------------------------

		public void SetProgressText(string text)
			=> Dispatcher.BeginInvoke(new Action<string>(t => StatusText = t), DispatcherPriority.Normal, text);

		public IPpsProgress CreateProgress(bool blockUI)
			=> progressStack.CreateProgress(blockUI);

		#endregion

		#region -- ErrorState - property ----------------------------------------------

		private readonly static DependencyPropertyKey errorStatePropertyKey = DependencyProperty.RegisterReadOnly(nameof(ErrorState), typeof(ErrorStateData), typeof(PpsSplashWindow), new FrameworkPropertyMetadata(null));
		public readonly static DependencyProperty ErrorStateProperty = errorStatePropertyKey.DependencyProperty;

		private readonly static DependencyPropertyKey hasErrorStatePropertyKey = DependencyProperty.RegisterReadOnly(nameof(HasErrorState), typeof(bool), typeof(PpsSplashWindow), new FrameworkPropertyMetadata(BooleanBox.False));
		public readonly static DependencyProperty HasErrorStateProperty = hasErrorStatePropertyKey.DependencyProperty;

		public async Task SetErrorAsync(object errorInfo, IPpsShell shell)
		{
			await Dispatcher.InvokeAsync(
				() =>
				{
					if (errorInfo is null)
					{
						SetValue(errorStatePropertyKey, null);
						SetValue(hasErrorStatePropertyKey, false);
					}
					else
					{
						SetValue(errorStatePropertyKey, new ErrorStateData(errorInfo, shell));
						SetValue(hasErrorStatePropertyKey, true);
					}
				}
			);
		} // func SetErrorAsync

		public ErrorStateData ErrorState => (ErrorStateData)GetValue(ErrorStateProperty);
		public bool HasErrorState => BooleanBox.GetBool(GetValue(HasErrorStateProperty));

		#endregion

		#region -- Runtime - property -------------------------------------------------

		public Task<bool> ShowRuntimeInstallAsync(IEnumerable<object> runtimeList, bool isAdmin)
		{
			var runtime = new RuntimeInstallState();

			runtimeInstallList.ItemsSource = runtimeList;
			runtimeAdminInfo.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;

			PushState(runtime);
			return runtime.Result;
		} // proc ShowRuntimeInstallAsync

		#endregion

		#region -- ActivateState - property -------------------------------------------

		public readonly static DependencyProperty ActivePageNumProperty = DependencyProperty.Register("ActivePageNum", typeof(int), typeof(PpsSplashWindow), new FrameworkPropertyMetadata(0));

		/// <summary>Current active state.</summary>
		private StatePanes ActiveState { get => (StatePanes)(int)GetValue(ActivePageNumProperty); set => SetValue(ActivePageNumProperty, (int)value); }

		#endregion
	} // class PpsSplashWindow
}
