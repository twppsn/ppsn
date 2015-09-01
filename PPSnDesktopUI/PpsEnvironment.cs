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
using TecWare.PPSn.Data;
using TecWare.DES.Networking;
using System.Xml.Linq;
using TecWare.PPSn.Controls;
using System.IO;
using System.Net.Mime;

namespace TecWare.PPSn
{
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
		Offline = 1,
		Online = 2
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
	public class PpsEnvironment : LuaGlobalPortable, IPpsShell, ICredentials, IDisposable
	{
		public const string EnvironmentService = "PpsEnvironmentService";

		#region -- class PpsWebRequestCreate ----------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private class PpsWebRequestCreate : IWebRequestCreate
		{
			private readonly WeakReference< PpsEnvironment> environment;

			public PpsWebRequestCreate(PpsEnvironment environment)
			{
				this.environment = new WeakReference<PpsEnvironment>(environment);
			} // ctor

			public WebRequest Create(Uri uri)
			{
				PpsEnvironment env;
				if (environment.TryGetTarget(out env))
					return env.CreateWebRequest(uri);
				else
					throw new ObjectDisposedException("Environment does not exists anymore.");
			}
		} // class PpsWebRequestCreate

		#endregion

		#region -- class WebIndex ---------------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		public class WebIndex
		{
			private readonly PpsEnvironment environment;

			internal WebIndex(PpsEnvironment environment)
			{
				this.environment = environment;
			} // ctor

			public BaseWebReqeust this[PpsEnvironmentDefinitionSource source]
			{
				get
				{
					if (source == PpsEnvironmentDefinitionSource.Offline)
						return environment.localRequest;
					else
						return environment.remoteRequest;
				}
			} // prop this
		} // class WebIndex

		#endregion
		
		/// <summary></summary>
		public event EventHandler IsOnlineChanged;
		/// <summary></summary>
		public event EventHandler UsernameChanged;

		private Uri remoteUri;                // remote source
		private Uri baseUri;									// internal uri for the environment
		private NetworkCredential userInfo;   // currently credentials of the user

		private BaseWebReqeust request;
		private BaseWebReqeust localRequest;
		private PpsLocalDataStore localStore;
		private BaseWebReqeust remoteRequest;

		private LuaCompileOptions luaOptions = LuaDeskop.StackTraceCompileOptions;

		private Dispatcher currentDispatcher; // Synchronisation
		private InputManager inputManager;
		private SynchronizationContext synchronizationContext;
		private ResourceDictionary mainResources;

		private PpsTraceLog logData = new PpsTraceLog();
		private PpsDataListTemplateSelector dataListTemplateSelector;
		private PpsEnvironmentCollection<PpsDataListItemDefinition> datalistItems;

		private DispatcherTimer idleTimer;
		private List<WeakReference<IPpsIdleAction>> idleActions = new List<WeakReference<IPpsIdleAction>>();
		private PreProcessInputEventHandler preProcessInputEventHandler;

		#region -- Ctor/Dtor --------------------------------------------------------------

		public PpsEnvironment(Uri remoteUri, ResourceDictionary mainResources)
			: base(new Lua())
		{
			this.remoteUri = remoteUri;
			this.mainResources = mainResources;
			this.currentDispatcher = Dispatcher.CurrentDispatcher;
			this.inputManager = InputManager.Current;
			this.synchronizationContext = new DispatcherSynchronizationContext(currentDispatcher);
			this.Web = new WebIndex(this);
			this.dataListTemplateSelector = new PpsDataListTemplateSelector(this);
			this.datalistItems = new PpsEnvironmentCollection<PpsDataListItemDefinition>(this);

			// Start idle implementation
			idleTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.ApplicationIdle, (sender, e) => OnIdle(), currentDispatcher);
			inputManager.PreProcessInput += preProcessInputEventHandler = (sender, e) => RestartIdleTimer();

			// Register internal uri
			baseUri = new Uri("http://environment1");
			localStore = CreateLocalDataStore();
			WebRequest.RegisterPrefix(baseUri.ToString(), new PpsWebRequestCreate(this));

			request = new BaseWebReqeust(baseUri, Encoding);
			localRequest = new BaseWebReqeust(new Uri(baseUri, "local/"), Encoding);
			remoteRequest = null;

			// Register Service
			mainResources[EnvironmentService] = this;
		} // ctor

		public void Dispose()
		{
			Dispose(true);
		} // proc Dispose

		protected virtual PpsLocalDataStore CreateLocalDataStore() => new PpsLocalDataStore(this);

		protected virtual void Dispose(bool disposing)
		{
			if (disposing)
			{
				mainResources.Remove(EnvironmentService);

				inputManager.PreProcessInput -= preProcessInputEventHandler;

				Lua.Dispose();
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
		public async virtual Task RefreshAsync()
		{
			await Task.Yield();

			string p = @"C:\Projects\PPSnOS\twppsn\PPSnWpf\PPSnDesktop\Local\Templates.xml";
			if (!File.Exists(p))
				p = @"..\..\..\PPSnDesktop\Local\Templates.xml";

			var xTemplates = XDocument.Load(p);
			foreach (var xTemplate in xTemplates.Root.Elements("template"))
			{
				var key = xTemplate.GetAttribute("key", String.Empty);
				if (String.IsNullOrEmpty(key))
					continue;

				var typeDef = datalistItems[key];
				if (typeDef == null)
				{
					typeDef = new PpsDataListItemDefinition(this, PpsEnvironmentDefinitionSource.Offline, key);
					datalistItems.AppendItem(typeDef);
				}

				typeDef.AppendTemplate(xTemplate);
			}
		} // proc RefreshAsync

		protected virtual void OnIsOnlineChanged()
		{
			var tmp = IsOnlineChanged;
			if (tmp != null)
				tmp(this, EventArgs.Empty);
		} // proc OnIsOnlineChanged

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

		#region -- Data Request -----------------------------------------------------------

		private WebRequest CreateWebRequest(Uri uri)
		{
			if (uri.AbsolutePath.StartsWith("/local")) // local request
				return localStore.GetRequest(uri, uri.AbsolutePath.Substring(6));
			else // todo: wenn nicht /local /remote, dann wähle erste remote dann local
				throw new NotImplementedException();
		} // func CreateWebRequest

		public IEnumerable<IDataRecord> GetListData(PpsShellGetList arguments)
		{
			return localStore.GetListData(arguments);
		} // func GetListData
		
		#endregion

		#region -- UI - Helper ------------------------------------------------------------

		void IPpsShell.BeginInvoke(Action action) => Dispatcher.BeginInvoke(action, DispatcherPriority.ApplicationIdle); // must be idle, that method is invoked after the current changes
		async Task IPpsShell.InvokeAsync(Action action) => await Dispatcher.InvokeAsync(action);
		async Task<T> IPpsShell.InvokeAsync<T>(Func<T> func) => await Dispatcher.InvokeAsync<T>(func);

		public void ShowException(ExceptionShowFlags flags, Exception exception, object alternativeMessage = null)
		{
			System.Windows.MessageBox.Show("todo: " + (alternativeMessage ?? exception.Message));
		} // proc ShowException

		public async Task ShowExceptionAsync(ExceptionShowFlags flags, Exception exception, object alternativeMessage = null)
		{
			await Dispatcher.InvokeAsync(() => ShowException(flags, exception, alternativeMessage));
		} // proc ShowException

		#endregion

		#region -- Lua Compiler -----------------------------------------------------------

		public async Task<LuaChunk> CompileAsync(XElement xSource, bool throwException, params KeyValuePair<string, Type>[] arguments)
		{
			try
			{
				var code = xSource.Value;
				var fileName = "dummy.lua"; // todo: get position
				return Lua.CompileChunk(code, fileName, luaOptions, arguments);
			}
			catch (LuaParseException e)
			{
				if (throwException)
					throw;
				else
				{
					await ShowExceptionAsync(ExceptionShowFlags.Background, e, "Compile failed.");
					return null;
				}
			}
		} // func CompileAsync

		/// <summary>Load an compile the file from a remote source.</summary>
		/// <param name="source">Source</param>
		/// <param name="throwException">Throw an exception on fail</param>
		/// <param name="arguments"></param>
		/// <returns></returns>
		public async Task<LuaChunk> CompileAsync(Uri source, bool throwException, params KeyValuePair<string, Type>[] arguments)
		{
			try
			{
				using (var r = await request.GetResponseAsync(source.ToString()))
				{
					var contentDisposion = r.GetContentDisposition(true);
					using (var sr = request.GetTextReaderAsync(r, MimeTypes.Lua))
						return Lua.CompileChunk(sr, contentDisposion.FileName, luaOptions, arguments);
				}
			}
			catch (LuaParseException e)
			{
				if (throwException)
					throw;
				else
				{
					await ShowExceptionAsync(ExceptionShowFlags.Background, e, $"Compile for {source} failed.");
					return null;
				}
			}
		} // func CompileAsync

		/// <summary></summary>
		/// <param name="chunk"></param>
		/// <param name="env"></param>
		/// <param name="throwException"></param>
		/// <param name="arguments"></param>
		/// <returns></returns>
		public LuaResult RunScript(LuaChunk chunk, LuaTable env, bool throwException, params object[] arguments)
		{
			try
			{
				return chunk.Run(env, arguments);
			}
			catch (LuaException e)
			{
				if (throwException)
					throw;
				else
				{
					Dispatcher.Invoke(() => ShowException(ExceptionShowFlags.Background, e));
					return LuaResult.Empty;
				}
			}
		} // func RunScript

		#endregion

		#region -- LuaHelper --------------------------------------------------------------

		/// <summary>Show a simple message box.</summary>
		/// <param name="text"></param>
		/// <param name="caption"></param>
		/// <param name="button"></param>
		/// <param name="image"></param>
		/// <param name="defaultResult"></param>
		/// <returns></returns>
		[LuaMember("msgbox")]
		private MessageBoxResult LuaMsgBox(string text, string caption, MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.Information, MessageBoxResult defaultResult = MessageBoxResult.OK)
		{
			return MessageBox.Show(text, caption ?? "Information", button, image, defaultResult);
		} // proc LuaMsgBox

		[LuaMember("trace")]
		private void LuaTrace(PpsTraceItemType type, params object[] args)
		{
			if (args == null || args.Length == 0)
				return;

			if (args[0] is string)
				Traces.AppendText(type, String.Format((string)args[0], args.Skip(1).ToArray()));
			else
				Traces.AppendText(type, String.Join(", ", args));
		} // proc LuaTrace
		
		/// <summary>Send a simple notification to the internal log</summary>
		/// <param name="args"></param>
		[LuaMember("print")]
		private void LuaPrint(params object[] args)
		{
			LuaTrace(PpsTraceItemType.Information, args);
		} // proc LuaPrint

		#endregion

		#region -- Resources --------------------------------------------------------------

		public T FindResource<T>(object resourceKey)
			where T : class
		{
			return mainResources[resourceKey] as T;
		} // func FindResource 

		#endregion

		/// <summary>Internal Uri of the environment.</summary>
		public Uri BaseUri => baseUri;
		/// <summary></summary>
		public WebIndex Web { get; }
		/// <summary></summary>
		public BaseWebReqeust BaseRequest => request;
		/// <summary>Default encodig for strings.</summary>
		public Encoding Encoding => Encoding.Default;

		/// <summary>Has the application login data.</summary>
		public bool IsAuthentificated { get { return userInfo != null; } }
		/// <summary>Is <c>true</c>, if the application is online.</summary>
		public bool IsOnline => remoteRequest != null;
		/// <summary>Current user the is logged in.</summary>
		public string Username { get { return userInfo == null ? String.Empty : userInfo.UserName; } }
		/// <summary>Display name for the user.</summary>
		public string UsernameDisplay { get { return "No User"; } }

		/// <summary></summary>
		public PpsEnvironmentCollection<PpsDataListItemDefinition> DataListItemTypes => datalistItems;
		/// <summary></summary>
		public PpsDataListTemplateSelector DataListTemplateSelector => dataListTemplateSelector;

		/// <summary>Dispatcher of the ui-thread.</summary>
		public Dispatcher Dispatcher { get { return currentDispatcher; } }
		/// <summary>Synchronisation</summary>
		SynchronizationContext IPpsShell.Context { get { return synchronizationContext; } }

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

	#region -- class LuaEnvironmentTable ------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Connects the current table with the Environment</summary>
	public class LuaEnvironmentTable : LuaTable
	{
		private PpsEnvironment environment;

		public LuaEnvironmentTable(PpsEnvironment environment)
		{
			this.environment = environment;
		} // ctor

		protected override object OnIndex(object key)
		{
			return base.OnIndex(key) ?? environment.GetValue(key);
		} // func OnIndex

		/// <summary>Access to the current environemnt.</summary>
		[LuaMember("Environment")]
		public PpsEnvironment Environment { get { return environment; } }
	} // class LuaEnvironmentTable

	#endregion
}
