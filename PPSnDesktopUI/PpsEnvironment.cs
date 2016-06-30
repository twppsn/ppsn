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
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Threading;
using System.Xml;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Data;
using TecWare.DE.Networking;
using TecWare.DE.Stuff;
using TecWare.PPSn.Controls;
using TecWare.PPSn.Data;
using TecWare.PPSn.UI;

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

	#region -- enum PpsClientAuthentificationType ---------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public enum PpsClientAuthentificationType
	{
		/// <summary>Unkown type.</summary>
		Unknown = 0,
		/// <summary>Normal unsecure web authentification.</summary>
		Basic,
		/// <summary>Windows/Kerberos authentification.</summary>
		Ntlm
	} // enum PpsClientAuthentificationType

	#endregion

	#region -- class PpsEnvironmentDefinition -------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Base class of sort and accessable environment items.</summary>
	public abstract class PpsEnvironmentDefinition
	{
		private readonly PpsEnvironment environment;
		private readonly string name;           // internal name of the item

		protected PpsEnvironmentDefinition(PpsEnvironment environment, string name)
		{
			if (String.IsNullOrEmpty(name))
				throw new ArgumentNullException("name");
			this.environment = environment;
			this.name = name;
		} // ctor
		
		/// <summary>Access to the owning environment.</summary>
		public PpsEnvironment Environment => environment;
		/// <summary>Name of the property.</summary>
		public string Name => name;
	} // class PpsEnvironmentDefinition

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

		public void Clear()
		{
			lock (items)
			{
					items.Clear();
					keys.Clear();
					currentVersion++;
			}
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
			=> CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

		private void OnAddCollection(object added)
			=> CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, added));

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
	public partial class PpsEnvironment : LuaGlobalPortable, IPpsShell, IDisposable
	{
		public const string EnvironmentService = "PpsEnvironmentService";

		#region -- class PpsWebRequestCreate ----------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private class PpsWebRequestCreate : IWebRequestCreate
		{
			private readonly WeakReference<PpsEnvironment> environment;

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

		/// <summary></summary>
		public event EventHandler IsOnlineChanged;
		/// <summary></summary>
		public event EventHandler UsernameChanged;

		private readonly int environmentId;            // unique id of the environment
		private readonly PpsEnvironmentInfo info;     // source information of the environment
		private readonly Uri baseUri;                 // internal uri for the environment

		private ICredentials userInfo;  // currently credentials of the user
		private string userName;				// display name of the user

		private readonly BaseWebRequest request;
		private readonly PpsLocalDataStore localStore;
		private bool isOnline = false;

		private LuaCompileOptions luaOptions = LuaDeskop.StackTraceCompileOptions;

		private Dispatcher currentDispatcher; // Synchronisation
		private InputManager inputManager;
		private SynchronizationContext synchronizationContext;
		private ResourceDictionary mainResources;

		private PpsTraceLog logData = new PpsTraceLog();
		private PpsDataListTemplateSelector dataListTemplateSelector;
		private PpsEnvironmentCollection<PpsDataListItemDefinition> templateDefinitions;

		private DispatcherTimer idleTimer;
		private List<WeakReference<IPpsIdleAction>> idleActions = new List<WeakReference<IPpsIdleAction>>();
		private PreProcessInputEventHandler preProcessInputEventHandler;

		#region -- Ctor/Dtor --------------------------------------------------------------

		public PpsEnvironment(PpsEnvironmentInfo info, ResourceDictionary mainResources)
			: base(new Lua())
		{
			if (info == null)
				throw new ArgumentNullException("info");
			if (mainResources == null)
				throw new ArgumentNullException("mainResources");

			this.info = info;
			this.mainResources = mainResources;
			this.currentDispatcher = Dispatcher.CurrentDispatcher;
			this.inputManager = InputManager.Current;
			this.synchronizationContext = new DispatcherSynchronizationContext(currentDispatcher);
			this.dataListTemplateSelector = new PpsDataListTemplateSelector(this);
			this.templateDefinitions = new PpsEnvironmentCollection<PpsDataListItemDefinition>(this);

			// Enable Trace Access
			BindingOperations.EnableCollectionSynchronization(logData, logData.SyncRoot,
				(collection, context, accessMethod, writeAccess) => currentDispatcher.Invoke(accessMethod)
			);

			// Start idle implementation
			idleTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(10), DispatcherPriority.ApplicationIdle, (sender, e) => OnIdle(), currentDispatcher);
			inputManager.PreProcessInput += preProcessInputEventHandler = (sender, e) => RestartIdleTimer();

			// Register internal uri
			lock (environmentCounterLock)
				this.environmentId = environmentCounter++;
			baseUri = new Uri($"http://environment{environmentId}.local");
			localStore = CreateLocalDataStore();
			WebRequest.RegisterPrefix(baseUri.ToString(), new PpsWebRequestCreate(this));

			request = new BaseWebRequest(baseUri, Encoding);

			// Register Service
			mainResources[EnvironmentService] = this;
		} // ctor

		public void Dispose()
		{
			Dispose(true);
		} // proc Dispose

		protected virtual PpsLocalDataStore CreateLocalDataStore()
			=> new PpsLocalDataStore(this);

		protected virtual void Dispose(bool disposing)
		{
			if (disposing)
			{
				mainResources.Remove(EnvironmentService);
				
				inputManager.PreProcessInput -= preProcessInputEventHandler;

				// close handles
				localStore.Dispose();
				Lua.Dispose();
			}
		} // proc Dispose

		#endregion

		#region -- Login/Logout -----------------------------------------------------------

		protected virtual bool ShowLoginDialog(PpsClientLogin clientLogin)
			=> false;

		/// <summary>Gets called to request the user information.</summary>
		/// <param name="type">Authentification type</param>
		/// <param name="realm">Realm of the server.</param>
		/// <param name="count">Counts the login requests.</param>
		/// <returns>User information or <c>null</c> for cancel.</returns>
		protected virtual ICredentials GetCredentials(PpsClientAuthentificationType type, string realm, int count)
		{
			if (type == PpsClientAuthentificationType.Ntlm && count == 0)
				return CredentialCache.DefaultCredentials;
			else
			{
				using (PpsClientLogin loginCache = new PpsClientLogin("twppsn:" + info.Uri.AbsoluteUri, realm, count > 1))
				{
					if (ShowLoginDialog(loginCache))
					{
						loginCache.Commit();
						return loginCache.GetCredentials();
					}
					else
						return null;
				}
			}
		} // func GetCredentials

		public async Task LoginUserAsync()
		{
			const string integratedSecurity = "Integrated Security";
			var count = 0;
			var realm = integratedSecurity;
			var type = PpsClientAuthentificationType.Ntlm;
			try
			{
				XElement xLogin = null;

				while (xLogin == null)
				{
					// get the user information
					userInfo = GetCredentials(type, realm, count);
					if (userInfo == null)
					{
						ResetLogin();
						return;
					}

					try
					{
						// try to login with this user
						xLogin = await request.GetXmlAsync("remote/login.xml", MimeTypes.Text.Xml, "user");
					}
					catch (WebException e)
					{
						if (e.Response == null)
							throw;

						// get the response
						using (var r = (HttpWebResponse)e.Response)
						{
							var code = r.StatusCode;

							if (code == HttpStatusCode.Unauthorized)
							{
								// Lese die Authentifizierung aus
								var authenticate = r.Headers["WWW-Authenticate"];

								if (authenticate.StartsWith("Basic realm=", StringComparison.OrdinalIgnoreCase)) // basic network authentification
								{
									type = PpsClientAuthentificationType.Basic;
									realm = authenticate.Substring(12);
									if (!String.IsNullOrEmpty(realm) && realm[0] == '"')
										realm = realm.Substring(1, realm.Length - 2);
								}
								else if (authenticate.IndexOf("NTLM", StringComparison.OrdinalIgnoreCase) >= 0) // Windows authentification
								{
									type = PpsClientAuthentificationType.Ntlm;
									realm = integratedSecurity;
								}
								else
								{
									type = PpsClientAuthentificationType.Unknown;
									realm = "Unknown";
								}
							}
							else
								throw;
						}
					}
				} // while xLogin

				userName = xLogin.GetAttribute("displayName", userInfo.ToString());

				OnUsernameChanged();
				await RefreshAsync();
			}
			catch
			{
				ResetLogin();
				throw;
			}
		} // proc LoginUser

		public async Task LogoutUserAsync()
		{
			await Task.Yield();
			ResetLogin();
		} // proc LogoutUser

		private void ResetLogin()
		{
			userName = null;
			userInfo = null;
			OnUsernameChanged();
		} // proc ResetLogin

		protected virtual void OnUsernameChanged()
			=> UsernameChanged?.Invoke(this, EventArgs.Empty);

		/// <summary>Queues a request, to check if the server is available. After this the environment and the cache will be updated.</summary>
		/// <param name="timeout">wait timeout for the server</param>
		/// <returns></returns>
		public async Task<bool> StartOnlineMode(CancellationToken token)
		{
			XElement xInfo;
			try
			{
				xInfo = await request.GetXmlAsync("remote/info.xml", MimeTypes.Text.Xml, "ppsn");
			}
			catch (WebException ex)
			{
				if (ex.Status == WebExceptionStatus.ConnectFailure) // remote host does not respond
					return false;
				else
					throw;
			}

			// update the current info data
			info.Update(xInfo);

			if (!isOnline)
			{
				isOnline = true;
				OnIsOnlineChanged();
			}

			// refresh data
			await RefreshAsync();

			return true;
		} // func StartOnlineMode

		#endregion

		private async Task<Tuple< XDocument,DateTime>> GetXmlDocumentAsync(string path, bool isXaml, bool isOptional)
		{
			try
			{
				using (var r = await Request.GetResponseAsync(path))
				using (var xml = Request.GetXmlStreamAsync(r, isXaml ? MimeTypes.Application.Xaml : MimeTypes.Text.Xml))
				{
					var dt = DateTime.MinValue;
					return new Tuple<XDocument, DateTime>(XDocument.Load(xml), dt);
				}
			}
			catch (WebException e)
			{
				if (e.Status == WebExceptionStatus.ProtocolError)
					return null;
				throw;
			}
		} // func GetXmlDocumentAsync

		/// <summary>Loads basic data for the environment.</summary>
		/// <returns></returns>
		public async virtual Task RefreshAsync()
		{
			await Task.Yield();

			await RefreshOfflineCacheAsync();
			await RefreshDefaultResourcesAsync();
			await RefreshTemplatesAsync();
		} // proc RefreshAsync

		private async Task RefreshOfflineCacheAsync()
		{
			if (IsOnline && IsAuthentificated)
				await Task.Run(new Action(localStore.UpdateOfflineItems));
		} // RefreshOfflineCacheAsync

		private async Task RefreshDefaultResourcesAsync()
		{
			// update the resources, load a server site resource dictionary
			var t = await GetXmlDocumentAsync("wpf/default.xaml", true, true);
			if (t == null)
				return;

			var basicTemplates = t.Item1;
			var lastTimeStamp = t.Item2;

			// theme/resources
			var xTheme = basicTemplates.Root;

			// build context
			var parserContext = new ParserContext();
			parserContext.AddNamespaces(xTheme);

			UpdateResources(xTheme, parserContext);
		} // proc RefreshDefaultResourcesAsync

		private async Task RefreshTemplatesAsync()
		{
			var t = await GetXmlDocumentAsync("wpf/templates.xaml", true, true);
			if (t == null)
				return;

			var xTemplates = t.Item1.Root;

			// build context
			var parserContext = new ParserContext();
			parserContext.AddNamespaces(xTemplates);

			// check for a global resource dictionary, and update the main resources
			UpdateResources(xTemplates, parserContext);

			// load the templates
			foreach (var x in xTemplates.Elements("template"))
			{
				var key = x.GetAttribute("key", String.Empty);
				if (String.IsNullOrEmpty(key))
					continue;

				var templateDefinition = templateDefinitions[key];
				if (templateDefinition == null)
				{
					templateDefinition = new PpsDataListItemDefinition(this, key);
					templateDefinitions.AppendItem(templateDefinition);
				}
				templateDefinition.AppendTemplate(x);
			}

			// remove unused templates
			// todo:
		} // proc RefreshTemplatesAsync

		private void UpdateResources(XElement xRoot, ParserContext parserContext)
		{
			var xResources = xRoot.Element(StuffUI.PresentationNamespace + "resources");
			if (xResources != null)
			{
				foreach (var cur in xResources.Elements())
				{
					var key = cur.GetAttribute(StuffUI.xnKey, String.Empty);
					if (key != null)
						Dispatcher.Invoke(() => UpdateResource(key, cur.ToString(), parserContext));
				}
			}
		} // proc UpdateResources

		protected virtual void OnIsOnlineChanged()
			=> IsOnlineChanged?.Invoke(this, EventArgs.Empty);

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

		#region -- Data Request -----------------------------------------------------------

		internal WebRequest CreateWebRequestNative(Uri uri, string absolutePath)
		{
			var request = WebRequest.Create(info.Uri.ToString() + absolutePath + uri.Query); // todo:
			request.Credentials = userInfo; // override the current credentials
			return request;
		} // func CreateWebRequestNative

		private WebRequest CreateWebRequest(Uri uri)
		{
			var useOfflineRequest = !isOnline;
			var useCache = true;
			var absolutePath = uri.AbsolutePath;

			// is the local data prefered
			if (uri.AbsolutePath.StartsWith("/local/"))
			{
				absolutePath = absolutePath.Substring(6);
				useOfflineRequest = true;
			}
			else if (uri.AbsolutePath.StartsWith("/remote/"))
			{
				absolutePath = absolutePath.Substring(7);
				useOfflineRequest = false;
				useCache = false;
			}

			// create the request
			if (useOfflineRequest )
				return localStore.GetRequest(uri, absolutePath);
			else
			{
				return useCache ?
					localStore.GetCachedRequest(uri, absolutePath) :
					CreateWebRequestNative(uri, absolutePath);
			}
		} // func CreateWebRequest
		public IEnumerable<IDataRow> GetViewData(PpsShellGetList arguments)
			=> localStore.GetViewData(arguments);

		#endregion

		#region -- UI - Helper ------------------------------------------------------------

		void IPpsShell.BeginInvoke(Action action)
			=> Dispatcher.BeginInvoke(action, DispatcherPriority.ApplicationIdle); // must be idle, that method is invoked after the current changes

		async Task IPpsShell.InvokeAsync(Action action)
			=> await Dispatcher.InvokeAsync(action);

		async Task<T> IPpsShell.InvokeAsync<T>(Func<T> func)
			=> await Dispatcher.InvokeAsync<T>(func);

		public void ShowException(ExceptionShowFlags flags, Exception exception, string alternativeMessage = null)
		{
			// always add the exception to the list
			Traces.AppendException(exception, alternativeMessage);

			// show the exception if it is not marked as background
			if ((flags & ExceptionShowFlags.Background) != ExceptionShowFlags.Background)
			{
				var dialogOwner = Application.Current.Windows.OfType<Window>().FirstOrDefault(c => c.IsActive);

				if (ShowExceptionDialog(dialogOwner, flags, exception, alternativeMessage)) // should follow a detailed dialog
					ShowTrace(dialogOwner);

				if ((flags & ExceptionShowFlags.Shutown) != 0) // close application
					Application.Current.Shutdown(1);
			}
		} // proc ShowException

		public static bool ShowExceptionDialog(Window dialogOwner, ExceptionShowFlags flags, Exception exception, string alternativeMessage)
		{
			var shutDown = (flags & ExceptionShowFlags.Shutown) != 0;

			var dialog = new PpsExceptionDialog();
			dialog.MessageType = shutDown ? PpsTraceItemType.Fail : PpsTraceItemType.Exception;
			dialog.MessageText = alternativeMessage ?? exception.Message;
			dialog.SkipVisible = !shutDown;

			dialog.Owner = dialogOwner;
			return dialog.ShowDialog() ?? false; // show the dialog
		} // func ShowExceptionDialog

		public async Task ShowExceptionAsync(ExceptionShowFlags flags, Exception exception, string alternativeMessage = null)
			=> await Dispatcher.InvokeAsync(() => ShowException(flags, exception, alternativeMessage));

		/// <summary></summary>
		/// <param name="owner"></param>
		public void ShowTrace(Window owner)
		{
			var dialog = new PpsTraceDialog();
			var t = dialog.LoadAsync(this);
			if (t.IsCompleted)
				t.Wait();
			dialog.Owner = owner;
			dialog.ShowDialog();
		} // proc ShowTrace

		/// <summary>Returns the pane declaration for the trace pane.</summary>
		public Type TracePane
			=> typeof(PpsTracePane);

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
					using (var sr = request.GetTextReaderAsync(r, MimeTypes.Text.Lua))
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
					ShowException(ExceptionShowFlags.None, e);
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
			=> mainResources[resourceKey] as T;

		private void UpdateResource(string keyString, string xamlSource, ParserContext parserContext)
		{
			var resource = XamlReader.Parse(xamlSource, parserContext); // todo: Exception handling
			mainResources[keyString] = resource;
		} // func UpdateResource

		#endregion

		/// <summary>Internal Uri of the environment.</summary>
		public Uri BaseUri => baseUri;
		/// <summary></summary>
		public BaseWebRequest Request => request;
		/// <summary>Default encodig for strings.</summary>
		public Encoding Encoding => Encoding.Default;

		/// <summary>Local description of the environment.</summary>
		public PpsEnvironmentInfo Info => info;

		/// <summary>Has the application login data.</summary>
		public bool IsAuthentificated => userInfo != null;
		/// <summary>Is <c>true</c>, if the application is online.</summary>
		public bool IsOnline => isOnline;
		/// <summary>Current user the is logged in.</summary>
		public string Username => userName ?? String.Empty;
		/// <summary>Display name for the user.</summary>
		public string UsernameDisplay => IsAuthentificated ? userName : "Nicht angemeldet";

		/// <summary></summary>
		public PpsEnvironmentCollection<PpsDataListItemDefinition> DataListItemTypes => templateDefinitions;
		/// <summary></summary>
		public PpsDataListTemplateSelector DataListTemplateSelector => dataListTemplateSelector;

		/// <summary>Dispatcher of the ui-thread.</summary>
		public Dispatcher Dispatcher => currentDispatcher;
		/// <summary>Synchronisation</summary>
		SynchronizationContext IPpsShell.Context => synchronizationContext;

		/// <summary>Access to the current collected informations.</summary>
		public PpsTraceLog Traces => logData;

		protected PpsLocalDataStore LocalStore => localStore;

		// -- Static --------------------------------------------------------------

		private static object environmentCounterLock = new object();
		private static int environmentCounter = 1;

		public static PpsEnvironment GetEnvironment(FrameworkElement ui)
			=> (PpsEnvironment)ui.FindResource(EnvironmentService);

		public static PpsEnvironment GetEnvironment()
			=> (PpsEnvironment)Application.Current.FindResource(EnvironmentService);
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
