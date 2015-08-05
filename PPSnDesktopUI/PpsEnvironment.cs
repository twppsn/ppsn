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
using System.Collections;
using System.Collections.Specialized;

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

	#region -- interface IPpsIdleAction -------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Implementation for a idle action.</summary>
	public interface IPpsIdleAction
	{
		/// <summary>Gets called on application idle start.</summary>
		void OnIdle();
	} // interface IPpsIdleAction

	#endregion

	#region -- enum PpsEnvironmentDefinitionSource --------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public enum PpsEnvironmentDefinitionSource
	{
		Offline = 0,
		Online
	} // enum PpsEnvironmentDefinitionSource

	#endregion

	#region -- class PpsEnvironmentDefinition -------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Base class of sort and accessable environment items.</summary>
	public abstract class PpsEnvironmentDefinition
	{
		private PpsEnvironment environment;

		private readonly string name;           // internal name of the item
		private readonly PpsEnvironmentDefinitionSource source; // is this item offline available

		protected PpsEnvironmentDefinition(PpsEnvironment environment, PpsEnvironmentDefinitionSource source, string name)
		{
			this.environment = environment;
			this.source = source;
			this.name = name;
		} // ctor
		
		/// <summary>Access to the owning environment.</summary>
		public PpsEnvironment Environment => environment;
		/// <summary>Name of the property.</summary>
		public string Name => name;
		/// <summary>Is the item offline available.</summary>
		public PpsEnvironmentDefinitionSource Source => source;
	} // class PpsEnvironmentDefinition

	#endregion

	#region -- enum PpsEnvironmentClearFlags --------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	[Flags]
	public enum PpsEnvironmentClearFlags
	{
		None = 0,
		Offline = 1,
		Online = 2,
		All = 3
	} // enum PpsEnvironmentClearFlags

	#endregion

	#region -- class PpsEnvironmentCollection -------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsEnvironmentCollection<T> : ICollection, IDictionary<string, T>, INotifyCollectionChanged
		where T : PpsEnvironmentDefinition
	{
		#region -- class ValueCollection --------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private class ValueCollection : ICollection<T>
		{
			private PpsEnvironmentCollection<T> owner;

			public ValueCollection(PpsEnvironmentCollection<T> owner)
			{
				this.owner = owner;
			} // ctor

			public void Add(T item)
			{ throw new NotSupportedException(); }
			public void Clear()
			{ throw new NotSupportedException(); }
			public bool Remove(T item)
			{ throw new NotSupportedException(); }

			public void CopyTo(T[] array, int arrayIndex)
			{
				lock (owner.items)
				{
					foreach (var i in owner.keys.Values)
						array[arrayIndex++] = owner.items[i];
				}
			} // prop CopyTo
			public IEnumerator<T> GetEnumerator()
			{
				lock (owner.items)
				{
					foreach (var i in owner.keys.Values)
						yield return owner.items[i];
				}
			} // func GetEnumerator

			IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
			public bool Contains(T item) => owner.items.Contains(item);

			public bool IsReadOnly => true;

			public int Count => owner.Count;
		} // class ValueCollection

		#endregion

		public event NotifyCollectionChangedEventHandler CollectionChanged;

		private PpsEnvironment environment;
		private int currentVersion = 0;
		private List<T> items = new List<T>(); // list with all items
		private Dictionary<string, int> keys = new Dictionary<string, int>(); // list with all active items

		public PpsEnvironmentCollection(PpsEnvironment environment)
		{
			this.environment = environment;
		} // ctor

		public void AppendItem(T item)
		{
			var indexAdded = -1;

			if (item.Environment != environment)
				throw new ArgumentException("Invalid environment.");

			lock (items)
			{
				indexAdded = items.Count;
				items.Add(item);

				// update keys
				keys[item.Name] = indexAdded;
			}

			if (indexAdded != -1)
				OnAddCollection(item);
		} // proc AppendItem

		public void Clear(PpsEnvironmentClearFlags flags = PpsEnvironmentClearFlags.All)
		{
			var resetCollection = false;
			lock (items)
			{
				if ((flags & PpsEnvironmentClearFlags.All) == PpsEnvironmentClearFlags.All)
				{
					items.Clear();
					keys.Clear();
					currentVersion++;
					resetCollection = true;
				}
				else
				{
					var itemsRemoved = false;

					for (int i = items.Count - 1; i >= 0; i--)
					{
						if (((flags & PpsEnvironmentClearFlags.Online) != 0 && items[i].Source == PpsEnvironmentDefinitionSource.Online) ||
							((flags & PpsEnvironmentClearFlags.Offline) != 0 && items[i].Source == PpsEnvironmentDefinitionSource.Online))
						{
							// update keys
							var current = items[i];
							if (keys[current.Name] == i)
							{
								var replaceIndex = -1;
								if (current.Source == PpsEnvironmentDefinitionSource.Online)
									replaceIndex = items.FindIndex(c => current.Name == c.Name && c.Source == PpsEnvironmentDefinitionSource.Offline);
								if (replaceIndex == -1)
									keys.Remove(current.Name);
								else
									keys[current.Name] = replaceIndex;
							}

							items.RemoveAt(i);
							itemsRemoved |= true;
						}
					}

					if (itemsRemoved)
					{
						currentVersion++;
						resetCollection = true;
					}
				}
			}
			if (resetCollection)
				OnResetCollection();
		} // proc Clear

		private int FindIndexByKey(string name)
		{
			lock (items)
			{
				var index = 0;
				return keys.TryGetValue(name, out index) ? index : -1;
			}
		} // func FindIndexByKey

		private void OnResetCollection()
		{
			if (CollectionChanged != null)
				CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
		} // proc OnResetCollection

		private void OnAddCollection(object added)
		{
			if (CollectionChanged != null)
				CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, added));
		} // proc OnAddCollection

		#region -- IDictionary, IList, ... ------------------------------------------------

		bool IDictionary<string, T>.TryGetValue(string key, out T value)
		{
			lock (items)
			{
				var index = FindIndexByKey(key);
				if (index >= 0)
				{
					value = items[index];
					return true;
				}
				else
				{
					value = null;
					return false;
				}
			}
		} // func IDictionary<string, T>.TryGetValue

		void ICollection<KeyValuePair<string, T>>.CopyTo(KeyValuePair<string, T>[] array, int arrayIndex)
		{
			// normally the locking is done by the consumer
			foreach (var i in keys.Values)
				array[arrayIndex++] = new KeyValuePair<string, T>(items[i].Name, items[i]);
		} // proc ICollection<KeyValuePair<string, T>>.CopyTo

		IEnumerator<KeyValuePair<string, T>> IEnumerable<KeyValuePair<string, T>>.GetEnumerator()
		{
			lock (items)
			{
				foreach (T c in this)
					yield return new KeyValuePair<string, T>(c.Name, c);
			}
		} // func IEnumerable<KeyValuePair<string, T>>.GetEnumerator

		void ICollection.CopyTo(Array array, int index)
		{
			lock (items)
			{
				foreach (var i in keys.Values)
					array.SetValue(items[i], index++);
			}
		} // proc ICollection.CopyTo

		bool IDictionary<string, T>.ContainsKey(string key)
		{
			lock (items)
				return keys.ContainsKey(key);
		} // func IDictionary<string, T>.ContainsKey

		public IEnumerator GetEnumerator()
		{
			lock (items)
			{
				foreach (var i in keys.Values)
					yield return items[i];
			}
		} // func IEnumerable.GetEnumerator

		void IDictionary<string, T>.Add(string key, T value) { throw new NotSupportedException(); }
		bool IDictionary<string, T>.Remove(string key) { throw new NotSupportedException(); }
		T IDictionary<string, T>.this[string key] { get { return this[key]; } set { throw new NotSupportedException(); } }
		ICollection<T> IDictionary<string, T>.Values { get { return new ValueCollection(this); } }
		ICollection<string> IDictionary<string, T>.Keys { get { return keys.Keys; } }

		void ICollection<KeyValuePair<string, T>>.Add(KeyValuePair<string, T> item) { throw new NotSupportedException(); }
		bool ICollection<KeyValuePair<string, T>>.Contains(KeyValuePair<string, T> item) { throw new NotSupportedException(); }
		bool ICollection<KeyValuePair<string, T>>.Remove(KeyValuePair<string, T> item) { throw new NotSupportedException(); }
		void ICollection<KeyValuePair<string, T>>.Clear() { Clear(PpsEnvironmentClearFlags.All); }

		bool ICollection.IsSynchronized => true;
		object ICollection.SyncRoot => items;
		bool ICollection<KeyValuePair<string, T>>.IsReadOnly => true;

		#endregion

		public T this[string name]
		{
			get
			{
				lock (items)
				{
					var index = 0;
					return keys.TryGetValue(name, out index) ? items[index] : null;
				}
			}
		} // prop this

		/// <summary>Number of items</summary>
		public int Count => keys.Count;
		/// <summary></summary>
		public PpsEnvironment Environment => environment;
	} // class PpsEnvironmentCollection

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

		private Uri remoteUri;                // remote source
		private bool isOffline = true;        // is the application online
		private NetworkCredential userInfo;   // currently credentials of the user

		private Lua lua;
		private Dispatcher currentDispatcher; // Synchronisation
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
