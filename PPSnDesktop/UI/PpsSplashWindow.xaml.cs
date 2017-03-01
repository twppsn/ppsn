using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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
using TecWare.PPSn.Data;

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
			private string actualUserName = null;
			private IEnumerable<string> recentUsers = null;

			public LoginStateData(PpsSplashWindow parent)
			{
				this.parent = parent;
			} // ctor

			private void OnPropertyChanged(string propertyName)
				=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

			public ICredentials GetCredentials()
			{
				if (IsDomainName(actualUserName) && CurrentEnvironment != null && CurrentEnvironment.Uri != null)
				{
					var test = new PpsClientLogin(CurrentEnvironment.Uri.ToString(), "", false);
					var creds = test.GetCredentials();
					var match = creds?.GetCredential(CurrentEnvironment.Uri, CurrentEnvironment.AuthType);
					return match ?? CredentialCache.DefaultCredentials.GetCredential(currentEnvironment.Uri, "");
				}
					//return CredentialCache.DefaultCredentials.GetCredential(currentEnvironment.Uri, "");
				else
				{
					var test = new NetworkCredential(ActualUserName, Password);
					return new NetworkCredential(ActualUserName, Password);
				}
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

						if (String.IsNullOrEmpty(currentEnvironment.LastUser))
							ActualUserName = Environment.UserDomainName + "\\" + Environment.UserName;
						else
							ActualUserName = currentEnvironment.LastUser;

						RecentUsers = currentEnvironment.RecentUsers;
					}
				}
			} // prop CurrentEnvironment

			public string ActualUserName
			{
				get { return actualUserName; }
				set
				{
					if (value != null && value.Length == 0)
						value = null;

					if (value != actualUserName)
					{
						actualUserName = value;

						OnPropertyChanged(nameof(ActualUserName));
						OnPropertyChanged(nameof(IsValid));
						OnPropertyChanged(nameof(IsPasswordEnabled));
						
					}
				}
			} // prop UserName
			
			public string Password;

			public string ActualUserPassword()
			{
				if (IsPasswordEnabled && CurrentEnvironment != null && CurrentEnvironment.Uri != null)
				{
					var test = new PpsClientLogin(CurrentEnvironment.Uri.AbsoluteUri, "", false);
					var creds = test.GetCredentials();
					var match = creds?.GetCredential(CurrentEnvironment.Uri, CurrentEnvironment.AuthType);
					if (match?.UserName == ActualUserName)
						return match.Password ?? String.Empty;
					return String.Empty;
				}
				else
					return String.Empty;
			}

			public bool IsValid => !String.IsNullOrEmpty(actualUserName);
			public bool IsUserNameEnabled => currentEnvironment != null;
			public bool IsPasswordEnabled => actualUserName != null && !IsDomainName(actualUserName) && !IsDefaultUser(actualUserName);

			public IEnumerable<string> RecentUsers
			{
				get
				{
					return recentUsers;
				}
				set
				{
					recentUsers = value;
					OnPropertyChanged(nameof(RecentUsers));
				}
			}

			private static bool IsDomainName(string userName)
				=> userName.StartsWith(System.Environment.UserDomainName + "\\", StringComparison.OrdinalIgnoreCase);

			private static bool IsDefaultUser(string userName)
				=> false;
		} // class LoginStateData

		#endregion

		private readonly static DependencyPropertyKey LoginPaneVisiblePropertyKey = DependencyProperty.RegisterReadOnly(nameof(LoginPaneVisible), typeof(Visibility), typeof(PpsSplashWindow), new PropertyMetadata(Visibility.Hidden));
		private readonly static DependencyPropertyKey StatusPaneVisiblePropertyKey = DependencyProperty.RegisterReadOnly(nameof(StatusPaneVisible), typeof(Visibility), typeof(PpsSplashWindow), new PropertyMetadata(Visibility.Hidden));//Visible
      private readonly static DependencyPropertyKey EnvironmentWizzardPaneVisiblePropertyKey = DependencyProperty.RegisterReadOnly(nameof(EnvironmentWizzardPaneVisible), typeof(Visibility), typeof(PpsSplashWindow), new PropertyMetadata(Visibility.Visible));
      private readonly static DependencyPropertyKey LoginStatePropertyKey = DependencyProperty.RegisterReadOnly(nameof(LoginState), typeof(LoginStateData), typeof(PpsSplashWindow), new PropertyMetadata(null));

		public readonly static DependencyProperty LoginPaneVisibleProperty = LoginPaneVisiblePropertyKey.DependencyProperty;
		public readonly static DependencyProperty StatusPaneVisibleProperty = StatusPaneVisiblePropertyKey.DependencyProperty;
      public readonly static DependencyProperty EnvironmentWizzardPaneVisibleProperty = EnvironmentWizzardPaneVisiblePropertyKey.DependencyProperty;
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
					new CommandBinding(ApplicationCommands.Save, ExecuteFrame, LoginFrameActive),
					new CommandBinding(ApplicationCommands.Close, CloseFrame, LoginFrameActive)
            }
            // 
			);

			SetValue(LoginPaneVisiblePropertyKey, Visibility.Hidden);
			SetValue(StatusPaneVisiblePropertyKey, Visibility.Visible);
         SetValue(EnvironmentWizzardPaneVisiblePropertyKey, Visibility.Hidden);
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
         SetValue(LoginPaneVisiblePropertyKey, Visibility.Hidden);
         SetValue(StatusPaneVisiblePropertyKey, Visibility.Hidden);
         SetValue(EnvironmentWizzardPaneVisiblePropertyKey, Visibility.Visible);
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
         SetValue(LoginPaneVisiblePropertyKey, Visibility.Visible);
         SetValue(EnvironmentWizzardPaneVisiblePropertyKey, Visibility.Hidden);
      }

      private void AbortEnvironment()
      {
         SetValue(LoginPaneVisiblePropertyKey, Visibility.Visible);
         SetValue(EnvironmentWizzardPaneVisiblePropertyKey, Visibility.Hidden);
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

		void IProgress<string>.Report(string text)
			=> SetProgressTextAsync(text);

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
		}

		private void cbEnviroments_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			loginStateUnSafe.CurrentEnvironment = (PpsEnvironmentInfo)((ComboBox)sender).SelectedItem;
		}

		private void ComboBox_TextInput(object sender, TextCompositionEventArgs e)
		{
			pbPassword.Password = loginStateUnSafe.ActualUserPassword();
		}

		private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			ComboBox_TextInput(sender, null);
		}

		private void ComboBox_KeyUp(object sender, KeyEventArgs e)
		{
			ComboBox_TextInput(sender, null);
		}
	} // class PpsSplashWindow
}
