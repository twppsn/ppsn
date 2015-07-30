using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Neo.IronLua;
using TecWare.DES.Stuff;

namespace TecWare.PPSn
{
	#region -- enum ExceptionShowFlags --------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Wie soll die Nachricht angezeigt werden.</summary>
	[Flags]
	public enum ExceptionShowFlags
	{
		/// <summary>Keine näheren Angaben</summary>
		None = 0,
		/// <summary>Beenden soll angezeigt werden.</summary>
		ExitButton = 1,
		/// <summary>Shutdown der Anwendung soll gestartet werden.</summary>
		Shutown = 2,
		/// <summary>Schwere Meldung, Anwendung muss geschlossen werden</summary>
		Fatal = ExitButton | Shutown,
		/// <summary>Ohne Dialog anzeigen, nur sammeln.</summary>
		Background = 4
	} // enum ExceptionShowFlags

	#endregion

	#region -- interface IPpsIdleAction -----------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Implementation for a idle action.</summary>
	public interface IPpsIdleAction
	{
		/// <summary>Gets called on application idle start.</summary>
		void OnIdle();
	} // interface IPpsIdleAction

	#endregion

	#region -- class PpsIdleCommand ---------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PpsIdleCommand : ICommand, IPpsIdleAction
	{
		private Action<object> command;
		private Func<object, bool> canExecute;

		public PpsIdleCommand(Action<object> command, Func<object, bool> canExecute)
		{
			this.command = command;
			this.canExecute = canExecute;
		} // ctor

		#region -- ICommand Member ---------------------------------------------------------

		public event EventHandler CanExecuteChanged;

		public virtual bool CanExecute(object parameter)
		{
			return canExecute == null || canExecute(parameter);
		} // func CanExecute

		public virtual void Execute(object parameter)
		{
			command(parameter);
		} // proc Execute

		#endregion

		public void Refresh()
		{
			if (CanExecuteChanged != null)
				CanExecuteChanged(this, EventArgs.Empty);
		} // proc Refresh

		void IPpsIdleAction.OnIdle() { Refresh(); }
	} // class PpsIdleCommand


	#endregion

	#region -- class PpsEnvironment -----------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Base class for application data. Holds information about view
	/// classes, exception, connection, synchronisation and the script 
	/// engine.</summary>
	public class PpsEnvironment : LuaTable, IPpsShell, ICredentials, IDisposable
	{
		public const string EnvironmentService = "PpsEnvironmentService";

		/// <summary></summary>
		public event EventHandler IsOfflineChanged;
		/// <summary></summary>
		public event EventHandler UsernameChanged;

		private Uri remoteUri;								// remote source
		private bool isOffline = true;				// is the application online
		private NetworkCredential userInfo;		// currently credentials of the user

		private Lua lua;
		private Dispatcher currentDispatcher;	// Synchronisation
		private InputManager inputManager;
		private SynchronizationContext synchronizationContext;
		private ResourceDictionary mainResources;

		private PpsTraceLog logData = new PpsTraceLog();

		private DispatcherTimer idleTimer;
		private List<WeakReference<IPpsIdleAction>> idleActions = new List<WeakReference<IPpsIdleAction>>();
		private PreProcessInputEventHandler preProcessInputEventHandler;
		
		#region -- Ctor/Dtor --------------------------------------------------------------

		public PpsEnvironment(Uri remoteUri, ResourceDictionary mainResources)
		{
			this.lua = new Lua();
			this.remoteUri = remoteUri;
			this.mainResources = mainResources;
			this.currentDispatcher = Dispatcher.CurrentDispatcher;
			this.inputManager = InputManager.Current;
			this.synchronizationContext = new DispatcherSynchronizationContext(currentDispatcher);

			// Start idle implementation
			idleTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.ApplicationIdle, (sender, e) => OnIdle(), currentDispatcher);
			inputManager.PreProcessInput += preProcessInputEventHandler = (sender, e) => RestartIdleTimer();
			
			// Register Service
			mainResources[EnvironmentService] = this;
		} // ctor

		~PpsEnvironment()
		{
			Dispose(false);
		} // dtor

		public void Dispose()
		{
			GC.SuppressFinalize(this);
			Dispose(true);
		} // proc Dispose

		protected virtual void Dispose(bool disposing)
		{
			if (disposing)
			{
				mainResources.Remove(EnvironmentService);

				inputManager.PreProcessInput -= preProcessInputEventHandler;

				Procs.FreeAndNil(ref lua);
			}
		} // proc Dispose

		#endregion

		public void LoginUser()
		{
			userInfo = CredentialCache.DefaultNetworkCredentials;
		} // proc LoginUser

		public void LogoutUser()
		{
		} // proc LogoutUser
		
		protected virtual void OnUsernameChanged()
		{
			var tmp = UsernameChanged;
			if (tmp != null)
				tmp(this, EventArgs.Empty);
		} // proc OnUsernameChanged

		/// <summary>Queues a request, to check if the server is available. After this the environment and the cache will be updated.</summary>
		/// <param name="timeout">wait timeout for the server</param>
		/// <returns></returns>
		public Task StartOnlineMode(int timeout)
		{
			return Task.Delay(1);
		} // func StartOnlineMode

		/// <summary>Loads basic data for the environment.</summary>
		/// <returns></returns>
		public Task RefreshAsync()
		{
			return Task.Delay(1000);
		} // proc RefreshAsync

		protected virtual void OnIsOfflineChanged()
		{
			var tmp = IsOfflineChanged;
			if (tmp != null)
				tmp(this, EventArgs.Empty);
		} // proc OnIsOfflineChanged

		#region -- Idle service -----------------------------------------------------------

		private int IndexOfIdleAction(IPpsIdleAction idleAction)
		{
			for (int i = 0; i < idleActions.Count; i++)
			{
				IPpsIdleAction t;
				if (idleActions[i].TryGetTarget(out t) && t == idleAction)
					return i;
			}
			return -1;
		} // func IndexOfIdleAction

		public void AddIdleAction(IPpsIdleAction idleAction)
		{
			if (IndexOfIdleAction(idleAction) == -1)
				idleActions.Add(new WeakReference<IPpsIdleAction>(idleAction));
		} // proc AddIdleAction

		public void RemoveIdleAction(IPpsIdleAction idleAction)
		{
			var i = IndexOfIdleAction(idleAction);
			if (i >= 0)
				idleActions.RemoveAt(i);
		} // proc RemoveIdleAction

		private void OnIdle()
		{
			for (var i = idleActions.Count - 1; i >= 0; i--)
			{
				IPpsIdleAction t;
				if (idleActions[i].TryGetTarget(out t))
					t.OnIdle();
				else
					idleActions.RemoveAt(i);
			}

			// stop the timer, this function should only called once
			idleTimer.Stop();
		} // proc OnIdle

		private void RestartIdleTimer()
		{
			if (idleActions.Count > 0)
			{
				idleTimer.Stop();
				idleTimer.Start();
			}
		} // proc RestartIdleTimer

		#endregion

		#region -- ICredentials members ---------------------------------------------------

		NetworkCredential ICredentials.GetCredential(Uri uri, string authType)
		{
			return userInfo;
		} // func ICredentials.GetCredential

		#endregion

		public void ShowException(ExceptionShowFlags flags, Exception exception, object alternativeMessage = null)
		{
			System.Windows.MessageBox.Show("todo: " + (alternativeMessage ?? exception.Message));
		} // proc ShowException

		/// <summary>Has the application login data.</summary>
		public bool IsAuthentificated { get { return userInfo != null; } }
		/// <summary>Is <c>true</c>, if the application is online.</summary>
		public bool IsOffline { get { return isOffline; } }
		/// <summary>Current user the is logged in.</summary>
		public string Username { get { return userInfo == null ? String.Empty : userInfo.UserName; } }
		/// <summary>Display name for the user.</summary>
		public string UsernameDisplay { get { return "No User"; } }

		/// <summary>Dispatcher of the ui-thread.</summary>
		public Dispatcher Dispatcher { get { return currentDispatcher; } }
		/// <summary>Synchronisation</summary>
		SynchronizationContext IPpsShell.Context { get { return synchronizationContext; } }
		/// <summary>Access to the current lua engine.</summary>
		public Lua Lua { get { return lua; } }

		/// <summary>Access to the current collected informations.</summary>
		public PpsTraceLog Traces { get { return logData; } }

		// -- Static --------------------------------------------------------------

		public static PpsEnvironment GetEnvironment(FrameworkElement ui)
		{
			return (PpsEnvironment)ui.FindResource(EnvironmentService);
		} // func GetEnvironment

		public static PpsEnvironment GetEnvironment()
		{
			return (PpsEnvironment)Application.Current.FindResource(EnvironmentService);
		} // func GetEnvironment
	} // class PpsEnvironment

	#endregion
}
