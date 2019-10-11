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
using System.ComponentModel;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Networking;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;
using TecWare.PPSn.UI;
using LExpression = System.Linq.Expressions.Expression;

namespace TecWare.PPSn
{
	#region -- class IPpsEnvironmentDefinition ----------------------------------------

	/// <summary>Defines basic set of properties for an environment definition.</summary>
	public interface IPpsEnvironmentDefinition
	{
		/// <summary>Active environment</summary>
		PpsEnvironment Environment { get; }
		/// <summary>Name of the entry.</summary>
		string Name { get; }
	} // interface IPpsEnvironmentDefinition

	#endregion

	#region -- class PpsEnvironmentDefinition -----------------------------------------

	/// <summary>Base class of sort and accessable environment items.</summary>
	public abstract class PpsEnvironmentDefinition : IPpsEnvironmentDefinition
	{
		private readonly PpsEnvironment environment;
		private readonly string name;           // internal name of the item

		/// <summary></summary>
		/// <param name="environment"></param>
		/// <param name="name"></param>
		protected PpsEnvironmentDefinition(PpsEnvironment environment, string name)
		{
			if (String.IsNullOrEmpty(name))
				throw new ArgumentNullException(nameof(name));
			this.environment = environment;
			this.name = name;
		} // ctor

		/// <summary>Access to the owning environment.</summary>
		public PpsEnvironment Environment => environment;
		/// <summary>Name of the property.</summary>
		public string Name => name;
	} // class PpsEnvironmentDefinition

	#endregion

	#region -- interface IPpsEnvironmentCollectionInternal ----------------------------

	/// <summary>Interface for management dynamic property access.</summary>
	internal interface IPpsEnvironmentCollectionInternal
	{
		int FindItemIndex(string name);

		object this[int index] { get; }

		int CurrentVersion { get; }
		int Count { get; }
	} // interface IPpsEnvironmentCollectionInternal

	#endregion

	#region -- class PpsEnvironmentCollection -----------------------------------------

	/// <summary></summary>
	public sealed class PpsEnvironmentCollection<T> : IPpsEnvironmentCollectionInternal, ICollection, IDictionary<string, T>, INotifyCollectionChanged, IDynamicMetaObjectProvider
		where T : class, IPpsEnvironmentDefinition
	{
		#region -- class ValueCollection ----------------------------------------------

		private class ValueCollection : ICollection<T>
		{
			private PpsEnvironmentCollection<T> owner;

			public ValueCollection(PpsEnvironmentCollection<T> owner)
			{
				this.owner = owner;
			} // ctor

			public void Add(T item)
				=> throw new NotSupportedException();
			public void Clear()
				=> throw new NotSupportedException();
			public bool Remove(T item)
				=> throw new NotSupportedException();

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

		#region -- class PpsEnvironmentMetaObjectProvider -----------------------------

		private sealed class PpsEnvironmentMetaObjectProvider : DynamicMetaObject
		{
			public PpsEnvironmentMetaObjectProvider(LExpression expression, PpsEnvironmentCollection<T> value)
				: base(expression, BindingRestrictions.Empty, value)
			{
			} // ctor

			private BindingRestrictions GetRestriction(IPpsEnvironmentCollectionInternal value, bool countRestriction)
			{
				var expr = LExpression.AndAlso(
					LExpression.TypeIs(Expression, typeof(IPpsEnvironmentCollectionInternal)),
					LExpression.AndAlso(
						LExpression.Equal(Expression, LExpression.Constant(Value)),
						LExpression.Equal(
							LExpression.Property(
								LExpression.Convert(Expression, typeof(IPpsEnvironmentCollectionInternal)),
								countRestriction ? CountPropertyInfo : CurrentVersionPropertyInfo
							),
							LExpression.Constant(countRestriction ? value.Count : value.CurrentVersion)
						)
					)
				);
				return BindingRestrictions.GetExpressionRestriction(expr);
			} // func GetRestriction

			private DynamicMetaObject BindGetItem(string name, bool throwException)
			{
				var value = (IPpsEnvironmentCollectionInternal)Value;

				var index = value.FindItemIndex(name);
				if (index >= 0)
				{
					return new DynamicMetaObject(
						LExpression.MakeIndex(
							LExpression.Convert(Expression, typeof(IPpsEnvironmentCollectionInternal)),
							ItemsPropertyInfo,
							new LExpression[] { LExpression.Constant(index) }
						), GetRestriction(value, false)
					);
				}
				else
				{
					LExpression expr;
					if (throwException)
					{
						expr = LExpression.Throw(
							LExpression.New(Procs.ArgumentOutOfRangeConstructorInfo2,
								new LExpression[]
								{
									LExpression.Constant(name),
									LExpression.Constant("Could not get environment item.")
								}
							), typeof(object)
						);
					}
					else
						expr = LExpression.Constant(null, typeof(object));

					return new DynamicMetaObject(expr, GetRestriction(value, true));
				}
			} // func BindGetItem

			public override DynamicMetaObject BindGetMember(GetMemberBinder binder)
				=> BindGetItem(binder.Name, false);

			public override DynamicMetaObject BindInvokeMember(InvokeMemberBinder binder, DynamicMetaObject[] args)
				=> BindGetItem(binder.Name, true);

			// -- Static ------------------------------------------------------------

			private static readonly PropertyInfo CountPropertyInfo;
			private static readonly PropertyInfo CurrentVersionPropertyInfo;
			private static readonly PropertyInfo ItemsPropertyInfo;

			static PpsEnvironmentMetaObjectProvider()
			{
				var t = typeof(IPpsEnvironmentCollectionInternal);
				CountPropertyInfo = Procs.GetProperty(t, nameof(IPpsEnvironmentCollectionInternal.Count));
				CurrentVersionPropertyInfo = Procs.GetProperty(t, nameof(IPpsEnvironmentCollectionInternal.CurrentVersion));
				ItemsPropertyInfo = Procs.GetProperty(t, "Item", typeof(int));
			} // sctor
		} // class PpsEnvironmentMetaObjectProvider

		#endregion

		/// <summary>Collection content has changed.</summary>
		public event NotifyCollectionChangedEventHandler CollectionChanged;

		private readonly PpsEnvironment environment;
		private int currentVersion = 0;
		private List<T> items = new List<T>(); // list with all items
		private Dictionary<string, int> keys = new Dictionary<string, int>(); // list with all active items

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary></summary>
		/// <param name="environment"></param>
		public PpsEnvironmentCollection(PpsEnvironment environment)
		{
			this.environment = environment;
		} // ctor

		DynamicMetaObject IDynamicMetaObjectProvider.GetMetaObject(LExpression parameter)
			=> new PpsEnvironmentMetaObjectProvider(parameter, this);

		#endregion

		/// <summary>Append a new item to the collection.</summary>
		/// <param name="item"></param>
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

		/// <summary>Clear all items in the collection.</summary>
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

		#region -- IDictionary, IList, ... --------------------------------------------

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

		/// <summary></summary>
		/// <returns></returns>
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
		ICollection<T> IDictionary<string, T>.Values => new ValueCollection(this);
		ICollection<string> IDictionary<string, T>.Keys => keys.Keys;

		void ICollection<KeyValuePair<string, T>>.Add(KeyValuePair<string, T> item) { throw new NotSupportedException(); }
		bool ICollection<KeyValuePair<string, T>>.Contains(KeyValuePair<string, T> item) { throw new NotSupportedException(); }
		bool ICollection<KeyValuePair<string, T>>.Remove(KeyValuePair<string, T> item) { throw new NotSupportedException(); }

		bool ICollection.IsSynchronized => true;
		object ICollection.SyncRoot => items;
		bool ICollection<KeyValuePair<string, T>>.IsReadOnly => true;

		#endregion

		int IPpsEnvironmentCollectionInternal.FindItemIndex(string name)
			=> keys.TryGetValue(name, out var index) ? index : -1;

		/// <summary>Get a item by name.</summary>
		/// <param name="name"></param>
		/// <param name="throwExpression"></param>
		/// <returns></returns>
		public T this[string name, bool throwExpression = false]
		{
			get
			{
				lock (items)
				{
					if (keys.TryGetValue(name, out var index))
						return items[index];
					else if (throwExpression)
						throw new ArgumentOutOfRangeException(name);
					else
						return null;
				}
			}
		} // prop this

		/// <summary>Number of items</summary>
		public int Count => keys.Count;
		/// <summary>Access the environment</summary>
		public PpsEnvironment Environment => environment;

		int IPpsEnvironmentCollectionInternal.CurrentVersion => currentVersion;
		object IPpsEnvironmentCollectionInternal.this[int index] => items[index];
	} // class PpsEnvironmentCollection

	#endregion

	#region -- enum PpsEnvironmentState -----------------------------------------------

	/// <summary>Current state of the environment.</summary>
	public enum PpsEnvironmentState
	{
		/// <summary>Nothing</summary>
		None,
		/// <summary>System environment will be destroyed or is destroyed.</summary>
		Shutdown,
		/// <summary>System is offline.</summary>
		Offline,
		/// <summary>System is offline and tries to connect to the server.</summary>
		OfflineConnect,
		/// <summary>System is online.</summary>
		Online
	} // enum PpsEnvironmentState

	#endregion

	#region -- enum PpsEnvironmentMode ------------------------------------------------

	/// <summary>Current mode of the environment.</summary>
	public enum PpsEnvironmentMode
	{
		/// <summary>No state.</summary>
		None,
		/// <summary>System offline.</summary>
		Offline,
		/// <summary>System online.</summary>
		Online,
		/// <summary>System closes.</summary>
		Shutdown
	} // enum PpsEnvironmentMode

	#endregion

	#region -- enum PpsEnvironmentModeResult ------------------------------------------

	/// <summary></summary>
	public enum PpsEnvironmentModeResult
	{
		/// <summary>System offline.</summary>
		Offline,
		/// <summary>System online.</summary>
		Online,
		/// <summary>System closes.</summary>
		Shutdown,

		/// <summary>Application data is outdated.</summary>
		NeedsUpdate,
		/// <summary>Start synchronization.</summary>
		NeedsSynchronization,
		/// <summary>State change login failed.</summary>
		LoginFailed,
		/// <summary>Server is not available.</summary>
		ServerConnectFailure
	} // enum PpsEnvironmentModeResult

	#endregion

	#region -- class PpsEnvironment ---------------------------------------------------

	/// <summary>Base class for the ppsn-Desktop rich client. Holds information about view
	/// classes, exception, connection, synchronisation and the script 
	/// engine.</summary>
	public partial class PpsEnvironment : PpsShellWpf, IPpsWindowPaneManager, IPpsDataSetProvider, IServiceProvider
	{
		private readonly App app;
		private readonly int environmentId;             // unique id of the environment
		private readonly PpsEnvironmentInfo info;       // source information of the environment
		private readonly ICredentials userInfo;    // currently credentials of the user

		private long userId = -1;
		private readonly DirectoryInfo localDirectory = null;   // local directory for the user data

		private readonly CancellationTokenSource environmentDisposing;
		
		private readonly PpsTraceLog traceLog;

		private readonly PpsEnvironmentCollection<PpsActionDefinition> actions;
		private readonly PpsEnvironmentCollection<PpsViewDefinition> views;
		private readonly PpsEnvironmentCollection<PpsDataTemplateDefinition> templateDefinitions;

		private readonly PpsProgressStack backgroundProgress;
		private readonly PpsProgressStack forgroundProgress;

		#region -- Ctor/Dtor ----------------------------------------------------------

		internal PpsEnvironment(PpsEnvironmentInfo info, ICredentials userInfo, App app)
			: base(new Lua(), app.Resources)
		{
			this.app = app;
			this.info = info ?? throw new ArgumentNullException("info");
			this.userInfo = userInfo ?? throw new ArgumentNullException("userInfo");

			environmentDisposing = new CancellationTokenSource();

			// Register internal uri
			lock (environmentCounterLock)
				environmentId = environmentCounter++;

			// create user name
			var userName = PpsEnvironmentInfo.GetUserNameFromCredentials(userInfo);
			SetMemberValue("UserName", userName);

			actions = new PpsEnvironmentCollection<PpsActionDefinition>(this);
			views = new PpsEnvironmentCollection<PpsViewDefinition>(this);
			templateDefinitions = new PpsEnvironmentCollection<PpsDataTemplateDefinition>(this);

			CompileOptions = new LuaCompileOptions()
			{
				DebugEngine = new LuaEnvironmentTraceLineDebugger()
			};

			// create local directory from user name
			localDirectory = new DirectoryInfo(Path.Combine(info.LocalPath.FullName, userName));
			if (!localDirectory.Exists)
				localDirectory.Create();

			// create web request proxy
			webProxy = new PpsWebProxy(this);

			// register proxy for the web requests
			var baseUri = new Uri($"http://ppsn{environmentId}.local");
			WebRequest.RegisterPrefix(baseUri.ToString(), new PpsWebRequestCreate(this));
			Request = CreateHttpCore(baseUri);

			// create object information cache
			activeObjectData = new PpsActiveObjectDataImplementation(this);
			objectInfo = new PpsEnvironmentCollection<PpsObjectInfo>(this);

			// create ui stuff
			this.statusOfProxy = new ProxyStatus(this.webProxy, Dispatcher);

			// enable trace access
			traceLog = new PpsTraceLog(this);
			BindingOperations.EnableCollectionSynchronization(traceLog, traceLog.SyncRoot,
				(collection, context, accessMethod, writeAccess) => Dispatcher.Invoke(accessMethod)
			);


			// Register new Data Schemes from, the server
			RegisterObjectInfoSchema(PpsMasterData.MasterDataSchema,
				new LuaTable()
				{
					[nameof(PpsObjectInfo.DocumentUri)] = "remote/wpf/masterdata.xml",
					[nameof(PpsObjectInfo.DocumentDefinitionType)] = typeof(PpsDataSetDefinitionDesktop)
				}
			);

			InitBackgroundNotifier(environmentDisposing.Token, out backgroundNotifier, out backgroundNotifierModeTransmission);

			RegisterService("DataFieldFactory", fieldFactory = new PpsDataFieldFactory(this));

			InitializeStatistics();

			backgroundProgress = new PpsProgressStack(app.Dispatcher);
			forgroundProgress = new PpsProgressStack(app.Dispatcher);
		} // ctor

		/// <summary>Initialize environmnet.</summary>
		/// <param name="progress"></param>
		/// <param name="bootOffline"></param>
		/// <returns></returns>
		public async Task<PpsEnvironmentModeResult> InitAsync(IProgress<string> progress, bool bootOffline = false)
		{
			// initialize the local database
			var isLocalDbReady = await InitLocalStoreAsync(progress);
			if (!isLocalDbReady && bootOffline)
			{
				if (await MsgBoxAsync("Es steht keine Offline-Version bereit.\nOnline Synchronisation jetzt starten?", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
					return await InitAsync(progress, false);
				else
					return PpsEnvironmentModeResult.NeedsUpdate;
			}

			// check for online mode
			redoConnect:
			progress.Report("Verbinden...");
			var r = await WaitForEnvironmentMode(bootOffline ? PpsEnvironmentMode.Offline : PpsEnvironmentMode.Online);
			switch (r)
			{
				case PpsEnvironmentModeResult.Online:
					await OnSystemOnlineAsync(); // mark as online
					break;
				case PpsEnvironmentModeResult.Offline:
					await OnSystemOfflineAsync();
					break;

				case PpsEnvironmentModeResult.NeedsUpdate:
					if (await MsgBoxAsync("Es steht eine neue Version zur Verfügung.\nUpdate durchführen?", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
					{
						if (await UpdateAsync(progress))
							goto redoConnect;
						else
							return PpsEnvironmentModeResult.NeedsUpdate;
					}
					else
					{
						await WaitForEnvironmentMode(PpsEnvironmentMode.Offline);
						bootOffline = true;
						goto redoConnect;
					}

				case PpsEnvironmentModeResult.NeedsSynchronization:
					if (await RunAsync(() => masterData.SynchronizationAsync(true, progress)))
						goto redoConnect;
					else
						return PpsEnvironmentModeResult.NeedsSynchronization;

				case PpsEnvironmentModeResult.ServerConnectFailure:
					if (!bootOffline)
						return await InitAsync(progress, true);
					break;
			}
			return r;
		} // func InitAsync

		// true => update successful
		// false => needs restart
		private Task<bool> UpdateAsync(IProgress<string> progress)
		{
			progress.Report("Lade Installationsdateien...");

			// start setup

			return Task.FromResult(true);
		} // proc UpdateAsync

		/// <summary></summary>
		/// <param name="disposing"></param>
		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				if (environmentDisposing.IsCancellationRequested)
					throw new ObjectDisposedException(nameof(PpsEnvironment));

				SetNewMode(PpsEnvironmentMode.Shutdown);

				// close tasks
				webProxy.Dispose();
				environmentDisposing.Cancel();

				// close handles
				Lua.Dispose();
				// dispose local store
				masterData?.Dispose();
			}

			base.Dispose(disposing);
		} // proc Dispose

		internal Task<bool> ShutdownAsync()
		{
			Dispose();
			return Task.FromResult(true);
		} // func ShutdownAsync

		#endregion

		/// <summary>Create progessbar for background tasks.</summary>
		/// <param name="blockUI"></param>
		/// <returns></returns>
		protected override IPpsProgress CreateProgressCore(bool blockUI)
			=> BackgroundProgressState.CreateProgress(false); 

		#region -- Online/Offline -----------------------------------------------------

		/// <summary>Is called when the system changes the state to online.</summary>
		/// <returns></returns>
		private async Task OnSystemOnlineAsync()
		{
			Trace.WriteLine("[Environment] System goes online.");

			masterData.CheckOfflineCache(); // start download

			await RefreshDefaultResourcesAsync(); // styles
			await RefreshNavigatorAsync(); // environment
			await RefreshTemplatesAsync(); // datatemplates
		} // proc OnSystemOnlineAsync

		/// <summary>Is called when the system changes the state to offline.</summary>
		/// <returns></returns>
		private async Task OnSystemOfflineAsync()
		{
			Trace.WriteLine("[Environment] System goes offline.");

			await RefreshDefaultResourcesAsync();
			await RefreshNavigatorAsync();
			await RefreshTemplatesAsync();
		} // proc OnSystemOfflineAsync

		#endregion

		#region -- RefreshNavigatorAsync ----------------------------------------------

		private static readonly XName xnEnvironment = "environment";
		private static readonly XName xnCode = "code";
		private static readonly XName xnNavigator = "navigator";

		private async Task RefreshNavigatorAsync()
		{
			// clear the views
			views.Clear();
			actions.Clear();
		
			try
			{
				// get the views from storage
				var xEnvironment = await Request.GetXmlAsync("wpf/environment.xml", rootName: xnEnvironment);

				// read navigator content
				var xNavigator = xEnvironment.Element(xnNavigator);
				if (xNavigator != null)
				{

					// append the new views
					foreach (var cur in xNavigator.Elements(PpsViewDefinition.xnView))
						views.AppendItem(new PpsViewDefinition(this, cur));

					// append the new actions
					var priority = 0;
					foreach (var cur in xNavigator.Elements(PpsActionDefinition.xnAction))
						actions.AppendItem(new PpsActionDefinition(this, cur, ref priority));

					// update document info
					lock (GetObjectInfoSyncObject())
					{
						var removeList = GetRemoveListObjectInfo();
						foreach (var cur in xNavigator.Elements(XName.Get("document")))
							UpdateObjectInfo(cur, removeList);
						ClearObjectInfo(removeList);
					}
				}

				// run environment extensions
				var code = xEnvironment.Element(xnCode)?.Value;
				if (!String.IsNullOrEmpty(code))
				{
					try
					{
						var chunk = await CompileAsync(code, "environment.lua", true, new KeyValuePair<string, Type>("self", typeof(LuaTable)));
						await Dispatcher.InvokeAsync(() => RunScript(chunk, this, true, this));
					}
					catch (Exception e)
					{
						AppendException(e);
					}
				}
			}
			catch (WebException ex)
			{
				if (ex.Status == WebExceptionStatus.ProtocolError) // e.g. file not found
					await ShowExceptionAsync(ExceptionShowFlags.Background, ex);
				else
					throw;
			}
		} // proc RefreshNavigatorAsync

		#endregion

		#region -- Window Manager -----------------------------------------------------

		public async Task<PpsMainWindow> CreateMainWindowAsync()
		{
			return await Dispatcher.InvokeAsync(() =>
			{
				// find a free window index
				var freeIndex = 0;
				while (GetWindow(freeIndex) != null)
					freeIndex++;

				var window = new PpsMainWindow(freeIndex);
				window.Show();
				return window;
			}
			);
		} // proc CreateMainWindow

		public PpsMainWindow GetWindow(int index)
		{
			foreach (var c in Application.Current.Windows)
			{
				if (c is PpsMainWindow w && w.WindowIndex == index)
					return w;
			}
			return null;
		} // func GetWindow

		public IEnumerable<PpsMainWindow> GetWindows()
		{
			foreach (var c in Application.Current.Windows)
			{
				if (c is PpsMainWindow w)
					yield return w;
			}
		} // func GetWindows

		#endregion

		#region -- Pane Manager -------------------------------------------------------

		/// <summary></summary>
		/// <param name="pane"></param>
		/// <returns></returns>
		public bool ActivatePane(IPpsWindowPane pane)
			=> pane?.PaneHost.PaneManager.ActivatePane(pane) ?? false;

		/// <summary>Loads a new pane in a new window.</summary>
		/// <param name="paneType"></param>
		/// <param name="newPaneMode">Pane mode to use. Default is <c>NewMainWindow</c>. <c>NewPane</c> and <c>ReplacePane</c> will throw an exception.</param>
		/// <param name="arguments">Argument set for the pane.</param>
		/// <returns>Task that returns a full initialized pane.</returns>
		public async Task<IPpsWindowPane> OpenPaneAsync(Type paneType, PpsOpenPaneMode newPaneMode, LuaTable arguments)
		{
			// find pane
			var pane = FindOpenPane(paneType, arguments);
			if (pane == null)
			{
				// open pane
				switch (newPaneMode)
				{
					case PpsOpenPaneMode.Default:
					case PpsOpenPaneMode.NewMainWindow:
						{
							var window = await CreateMainWindowAsync();
							return await window.OpenPaneAsync(paneType, PpsOpenPaneMode.NewPane, arguments);
						}
					case PpsOpenPaneMode.NewSingleWindow:
						{
							var window = new PpsSingleWindow(this, false);
							window.Show();
							return await window.OpenPaneAsync(paneType, PpsOpenPaneMode.NewPane, arguments);
						}
					case PpsOpenPaneMode.NewSingleDialog:
						{
							var window = new PpsSingleWindow(this, false);
							if (arguments?.GetMemberValue("DialogOwner") is Window dialogOwner)
								window.Owner = dialogOwner;
							var dlgPane = await window.OpenPaneAsync(paneType, PpsOpenPaneMode.Default, arguments);
							var r = window.ShowDialog();
							if (arguments != null && r.HasValue)
								arguments["DialogResult"] = r;
							return dlgPane;
						}
					default:
						throw new ArgumentOutOfRangeException(nameof(newPaneMode), newPaneMode, $"Only {nameof(PpsOpenPaneMode.Default)}, {nameof(PpsOpenPaneMode.NewMainWindow)} and {nameof(PpsOpenPaneMode.NewSingleWindow)} is allowed.");
				}
			}
			else
			{
				ActivatePane(pane);
				return pane;
			}
		} // func OpenPaneAsync

		/// <summary></summary>
		/// <param name="paneType"></param>
		/// <param name="arguments"></param>
		/// <returns></returns>
		public IPpsWindowPane FindOpenPane(Type paneType, LuaTable arguments)
		{
			foreach (var p in Panes)
			{
				if (p.EqualPane(paneType, arguments))
					return p;
			}
			return null;
		} // func FindOpenPane

		/// <summary>Enumerate panes of all windows</summary>
		public IEnumerable<IPpsWindowPane> Panes
		{
			get
			{
				foreach (var w in GetWindows())
				{
					if (w is IPpsWindowPaneManager m)
					{
						foreach (var p in m.Panes)
							yield return p;
					}
				}
			}
		} // prop Panes

		/// <summary>Return a pane manager</summary>
		/// <returns></returns>
		public IPpsWindowPaneManager GetDefaultPaneManager()
		{
			// get the current window
			var w = GetWindows().FirstOrDefault(c => c.IsActive);
			// or the environment
			return (IPpsWindowPaneManager)w ?? this;
		} // func GetDefaultPaneManager

		PpsShellWpf IPpsWindowPaneManager.Shell => this;
		bool IPpsWindowPaneManager.IsActive => false;

		#endregion

		#region -- Services -----------------------------------------------------------

		/// <summary></summary>
		/// <param name="serviceType"></param>
		/// <returns></returns>
		public override object GetService(Type serviceType)
		{
			if (serviceType == typeof(IPpsDataSetProvider))
				return this;
			else
				return base.GetService(serviceType);
		} // func GetService

		#endregion

		#region -- Background Notifier ------------------------------------------------

		#region -- class ModeTransission ----------------------------------------------

		/// <summary></summary>
		private sealed class ModeTransission
		{
			private bool isCompleted = false;
			private readonly PpsEnvironmentMode desiredMode;
			private readonly TaskCompletionSource<PpsEnvironmentModeResult> waitForSuccess;

			public ModeTransission(PpsEnvironmentMode desiredMode)
			{
				if (desiredMode != PpsEnvironmentMode.Online &&
						desiredMode != PpsEnvironmentMode.Offline &&
						desiredMode != PpsEnvironmentMode.Shutdown)
					throw new ArgumentException($"{desiredMode} is not a valid mode transission.");

				Trace.WriteLine($"[WaitForEnvironmentMode] Wait for {desiredMode}");
				this.desiredMode = desiredMode;
				this.waitForSuccess = new TaskCompletionSource<PPSn.PpsEnvironmentModeResult>();
			} // ctor

			public void SetResult(PpsEnvironmentModeResult result)
			{
				if (isCompleted)
					throw new InvalidOperationException();

				Trace.WriteLine($"[WaitForEnvironmentMode] Return {result}");
				waitForSuccess.SetResult(result);
				isCompleted = true;
			} // proc SetResult

			public void SetException(Exception e)
			{
				if (!isCompleted)
				{
					Trace.WriteLine($"[WaitForEnvironmentMode] Exception");
					waitForSuccess.SetException(e);
					isCompleted = true;
				}
			} // proc SetException

			public void Cancel()
			{
				if (!isCompleted)
				{
					Trace.WriteLine($"[WaitForEnvironmentMode] Canceled");
					waitForSuccess.TrySetCanceled();
					isCompleted = true;
				}
			} // proc Cancel

			public PpsEnvironmentMode DesiredMode => desiredMode;
			public Task<PpsEnvironmentModeResult> Task => waitForSuccess.Task;
		} // class StateTransission

		#endregion

		private readonly object modeTransmissionLock = new object();
		private ModeTransission modeTransmission = null;

		private readonly PpsSynchronizationContext backgroundNotifier;
		private readonly ManualResetEventAsync backgroundNotifierModeTransmission;

		private void InitBackgroundNotifier(CancellationToken cancellationToken, out PpsSynchronizationContext backgroundNotifier, out ManualResetEventAsync backgroundNotifierModeTransmission)
		{
			backgroundNotifierModeTransmission = new ManualResetEventAsync(false);
			backgroundNotifier = new PpsSingleThreadSynchronizationContext($"Environment Notify {environmentId}", cancellationToken, () => ExecuteNotifierLoopAsync(cancellationToken));
		} // proc InitBackgroundNotifier

		private void SetNewMode(PpsEnvironmentMode newMode)
			=> WaitForEnvironmentMode(newMode).AwaitTask(); // stops here if pending operations are not finished

		private Task<PpsEnvironmentModeResult> WaitForEnvironmentMode(PpsEnvironmentMode desiredMode)
		{
			// is this a new mode
			if (desiredMode == CurrentMode
				&& CurrentState != PpsEnvironmentState.None
				&& CurrentState != PpsEnvironmentState.OfflineConnect)
			{
				switch (desiredMode)
				{
					case PpsEnvironmentMode.Offline:
						return Task.FromResult(PpsEnvironmentModeResult.Offline);
					case PpsEnvironmentMode.Online:
						return Task.FromResult(PpsEnvironmentModeResult.Online);
					case PpsEnvironmentMode.Shutdown:
						return Task.FromResult(PpsEnvironmentModeResult.Shutdown);
					default:
						throw new InvalidOperationException();
				}
			}

			// start the mode switching
			lock (modeTransmissionLock)
			{
				var transmission = new ModeTransission(desiredMode);
				modeTransmission = transmission;
				backgroundNotifierModeTransmission.Set();
				return transmission.Task;
			}
		} // func WaitForEnvironmentState

		private bool TryGetModeTransmission(ref ModeTransission result)
		{
			lock (modeTransmissionLock)
			{
				if (modeTransmission == null)
					return false;
				else
				{
					if (result != null)
						result.Cancel();

					result = modeTransmission;
					backgroundNotifierModeTransmission.Reset();
					modeTransmission = null;
					return result != null;
				}
			}
		} // func TryGetModeTransmission

		private async Task ExecuteNotifierLoopAsync(CancellationToken cancellationToken)
		{
			async Task RunSyncAsync()
			{
				if (!masterData.IsInSynchronization)
				{
					using (var log = CreateProgress(false))
						await masterData.SynchronizationAsync(false, log);
				}
			}

			var state = PpsEnvironmentState.None;
			ModeTransission currentTransmission = null;
			while (!cancellationToken.IsCancellationRequested)
			{
				try
				{
					// new mode requested
					if (TryGetModeTransmission(ref currentTransmission))
					{
						switch (currentTransmission.DesiredMode)
						{
							case PpsEnvironmentMode.Offline:
								state = PpsEnvironmentState.Offline;
								break;
							case PpsEnvironmentMode.Online:
								state = PpsEnvironmentState.OfflineConnect;
								break;
							case PpsEnvironmentMode.Shutdown:
								state = PpsEnvironmentState.Shutdown;
								break;
						}
					}

					// process current state
					var changedTo = UpdatePulicState(state);
					switch (state)
					{
						case PpsEnvironmentState.None: // nothing to do wait for a state
						case PpsEnvironmentState.Offline:
							if (!SetTransmissionResult(ref currentTransmission, PpsEnvironmentModeResult.Offline))
							{
								if (changedTo)
									await Dispatcher.InvokeAsync(() => OnSystemOfflineAsync().AwaitTask());
							}

							if (!await backgroundNotifierModeTransmission.WaitAsync(30000) && IsNetworkPresent)
								state = PpsEnvironmentState.OfflineConnect;
							break;

						case PpsEnvironmentState.OfflineConnect:

							// load application info
							var xInfo = await Request.GetXmlAsync("remote/info.xml", rootName: "ppsn");
							info.Update(xInfo);
							if (info.IsModified)
								info.Save();

							PpsShellExtensions.UpdateMimeTypesFromInfo(xInfo); // update mime type mappings

							// new version
							if (!info.IsApplicationLatest)
							{
								// application needs a update
								state = PpsEnvironmentState.None;
								if (!SetTransmissionResult(ref currentTransmission, PpsEnvironmentModeResult.NeedsUpdate))
									state = PpsEnvironmentState.Offline; // todo: User message to update client
							}
							else
							{
								// try login for the user
								var xUser = await Request.GetXmlAsync("remote/login.xml", rootName: "user");

								// sync will write the header
								var newUserId = xUser.GetAttribute("userId", -1);

								foreach (var xAttr in xUser.Attributes())
								{
									var name = xAttr.Name.LocalName;
									if (name != "userId")
									{
										if (name.EndsWith("Id", StringComparison.OrdinalIgnoreCase))
											SetMemberValue(name, xAttr.Value.ChangeType<long>());
										else
											SetMemberValue(name, xAttr.Value);
									}
								}
								OnPropertyChanged(nameof(UsernameDisplay));

								if (newUserId == -1)
									throw new ArgumentOutOfRangeException("@userid", userId, "UID is missing.");

								if (userId != newUserId)
								{
									userId = newUserId;

									await Dispatcher.BeginInvoke(
										new Action(() => OnPropertyChanged(nameof(UserId)))
									);
								}
								// after connect, refresh properties
								masterData.SetUpdateUserInfo();

								// start synchronization
								if (!masterData.IsSynchronizationStarted || masterData.CheckSynchronizationStateAsync().AwaitTask())
								{
									if (!SetTransmissionResult(ref currentTransmission, PpsEnvironmentModeResult.NeedsSynchronization))
									{
										await RunSyncAsync();
										await Dispatcher.InvokeAsync(() => OnSystemOnlineAsync().AwaitTask());
									}
								}
								else // mark the system online
								{
									if (!SetTransmissionResult(ref currentTransmission, PpsEnvironmentModeResult.Online))
										await Dispatcher.InvokeAsync(() => OnSystemOnlineAsync().AwaitTask());
								}
							}
							state = PpsEnvironmentState.Online;
							break;

						case PpsEnvironmentState.Online:
							// fetch next state on ws-info
							if (!await backgroundNotifierModeTransmission.WaitAsync(3000))
								await RunSyncAsync();
							break;

						case PpsEnvironmentState.Shutdown:
							SetTransmissionResult(ref currentTransmission, PpsEnvironmentModeResult.Shutdown);
							return; // cancel all connections

						default:
							throw new InvalidOperationException("Unknown state.");
					}
				}
				catch (Exception e)
				{
					var ex = Procs.GetInnerException(e);
					if (currentTransmission != null)
					{
						var webEx = ex as WebException;

						if ((webEx?.Response is HttpWebResponse httpResponse) && httpResponse.StatusCode == HttpStatusCode.Forbidden)
						{
							SetTransmissionResult(ref currentTransmission, PpsEnvironmentModeResult.LoginFailed);
						}
						else
						{
							switch (webEx?.Status ?? WebExceptionStatus.UnknownError)
							{
								case WebExceptionStatus.NameResolutionFailure:
								case WebExceptionStatus.Timeout:
								case WebExceptionStatus.ConnectFailure:
									SetTransmissionResult(ref currentTransmission, PpsEnvironmentModeResult.ServerConnectFailure);
									break;
								case WebExceptionStatus.ProtocolError:
									Log.Append(PpsLogType.Exception, webEx);
									SetTransmissionResult(ref currentTransmission, PpsEnvironmentModeResult.LoginFailed);
									break;
								default:
									if (currentTransmission != null)
									{
										currentTransmission.SetException(ex);
										currentTransmission = null;
									}
									break;
							}
						}
						state = PpsEnvironmentState.None;
					}
					else
					{
						if (ex is WebException webEx && webEx.Status == WebExceptionStatus.ConnectFailure)
							state = PpsEnvironmentState.Offline;
						else
							Log.Append(PpsLogType.Warning, ex);
						await Task.Delay(500);
					}
				}
			}
		} // proc ExecuteNotifierLoopAsync

		private bool SetTransmissionResult(ref ModeTransission currentTransmission, PpsEnvironmentModeResult result)
		{
			if (currentTransmission != null)
			{
				currentTransmission.SetResult(result);
				currentTransmission = null;
				return true;
			}
			else
				return false;
		} // proc SetTransmissionResult

		private bool UpdatePulicState(PpsEnvironmentState state)
		{
			if (CurrentState != state)
			{
				CurrentState = state;

				var isModeChanged = false;
				switch (state)
				{
					case PpsEnvironmentState.Offline:
					case PpsEnvironmentState.OfflineConnect:
						if (CurrentMode != PpsEnvironmentMode.Offline)
						{
							CurrentMode = PpsEnvironmentMode.Offline;
							isModeChanged = true;
						}
						break;
					case PpsEnvironmentState.Online:
						if (CurrentMode != PpsEnvironmentMode.Online)
						{
							CurrentMode = PpsEnvironmentMode.Online;
							isModeChanged = true;
						}
						break;
					case PpsEnvironmentState.Shutdown:
						CurrentMode = PpsEnvironmentMode.Shutdown;
						break;
				}

				Dispatcher.BeginInvoke(new Action(
					() =>
					{
						OnPropertyChanged(nameof(CurrentMode));
						OnPropertyChanged(nameof(CurrentState));
					})
				);

				return isModeChanged;
			}
			else
				return false;
		} // proc UpdatePulicState

		#endregion

		#region -- Statistics ---------------------------------------------------------

		/// <summary>Class for performant keeping of statistical values</summary>
		public class CircularBuffer : IEnumerable<long>, INotifyCollectionChanged
		{
			private readonly Queue<long> queue;
			private readonly int maxCount;
			/// <summary>Keeps the UI up to date.</summary>
			public event NotifyCollectionChangedEventHandler CollectionChanged;

			private bool hasData = false;

			/// <summary>Public Constructor</summary>
			/// <param name="elementCount">Amount of datapoints to keep</param>
			public CircularBuffer(int elementCount)
			{
				maxCount = elementCount;
				queue = new Queue<long>(elementCount);
				for (var i = 0; i < elementCount; i++) queue.Enqueue((long)0);
			}

			/// <summary>Adds a new Datapoint (remove the oldest)</summary>
			/// <param name="item"></param>
			public void Add(long item)
			{
				if (!hasData)
					hasData = true;

				if (queue.Count == maxCount)
					queue.Dequeue();

				queue.Enqueue(item);
				CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
			}

			/// <summary>Returns the Enumerator</summary>
			/// <returns>Enum of the Buffer</returns>
			public IEnumerator<long> GetEnumerator()
			{
				return queue.GetEnumerator();
			}

			IEnumerator IEnumerable.GetEnumerator()
			{
				return GetEnumerator();
			}

			/// <summary>if false, the Queue is empty</summary>
			public bool HasData => hasData;
		}

		/// <summary>Class representing one Statistic Element</summary>
		public class StatisticElement : INotifyPropertyChanged
		{
			private readonly string name;
			private readonly Func<long> request;
			private CircularBuffer history;
			private long max = 0;
			private readonly Func<long, string> formatValue;
			private readonly int historyCount;

			/// <summary>For keeping the UI up to Date</summary>
			public event PropertyChangedEventHandler PropertyChanged;

			/// <summary>Constructor</summary>
			/// <param name="name">Name of the Statistic</param>
			/// <param name="request">Function to request one value (long)</param>
			/// <param name="formatValue">Function to Format the Value</param>
			/// <param name="historyCount">Amount of Datapoinst to keep (default 50)</param>
			public StatisticElement(string name, Func<long> request, Func<long, string> formatValue = null, int historyCount = 50)
			{
				this.name = name;
				this.request = request;
				this.history = new CircularBuffer(historyCount);
				this.formatValue = formatValue;
				this.historyCount = historyCount;
			}

			/// <summary>Captures the actual Value</summary>
			public void Request()
			{
				var current = request();
				history.Add(current);
				if (current > max)
				{
					max = current;
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Factor)));
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MaxValue)));
				}
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentValue)));
			}

			/// <summary>The Label for the Statistic</summary>
			public string Name => name;
			/// <summary>Keeps the older values</summary>
			public CircularBuffer History => history;
			/// <summary>Returns the scale - should be calculated in the VM</summary>
			public double Factor => (double)70 / max;
			/// <summary>Returns the maximum Value, formatted as given</summary>
			public string MaxValue => formatValue != null ? formatValue(max) : max.ToString();
			/// <summary>Returns the last Value, formatted as given</summary>
			public string CurrentValue => formatValue != null ? formatValue(history.Last()) : history.Last().ToString();
			/// <summary>False, if the Statistic was never activated</summary>
			public bool HasData => history.HasData;

			public int HistoryCount => historyCount;
		}

		private DispatcherTimer statisticsTimer;
		private List<StatisticElement> statistics;

		private void InitializeStatistics()
		{
			statistics = new List<StatisticElement>
			{
				new StatisticElement("Ram Usage", () => System.Diagnostics.Process.GetCurrentProcess().WorkingSet64, (val) => $"{val / (1024 * 1024)} MB"),
				new StatisticElement("Handles", () => System.Diagnostics.Process.GetCurrentProcess().HandleCount),
				new StatisticElement("Threads", () => Process.GetCurrentProcess().Threads.Count),
				new StatisticElement("Objects", () =>
				{
					var objectCount = (long)0;
					for (var i = 0; i <= GC.MaxGeneration; i++)
						objectCount += GC.CollectionCount(i);
					return objectCount;
				}),
				new StatisticElement("ObjectStore Alive", () =>
				{
					var alive = 0;
					foreach(var obj in objectStore)
						if (obj.TryGetTarget(out var tmp))
							alive++;
					return alive;
				}),
				new StatisticElement("ObjectStore Dead", () =>
				{
					var alive = 0;
					foreach(var obj in objectStore)
						if (!obj.TryGetTarget(out var tmp))
							alive++;
					return alive;
				}),
				new StatisticElement("Cached Tables Alive", () =>
				{
					var alive = 0;
					var field = typeof (PpsMasterData).GetField("cachedTables", BindingFlags.NonPublic |BindingFlags.GetField | BindingFlags.Instance);
					var cachedTables = (Dictionary<PpsDataTableDefinition, WeakReference<PpsMasterDataTable>>)field.GetValue(MasterData);
					foreach(var obj in cachedTables.Values)
						if (obj.TryGetTarget(out var tmp))
							alive++;
					return alive;
				}),
				new StatisticElement("Cached Tables Dead", () =>
				{
					var alive = 0;
					var field = typeof (PpsMasterData).GetField("cachedTables", BindingFlags.NonPublic |BindingFlags.GetField | BindingFlags.Instance);
					var cachedTables = (Dictionary<PpsDataTableDefinition, WeakReference<PpsMasterDataTable>>)field.GetValue(MasterData);
					foreach(var obj in cachedTables.Values)
						if (!obj.TryGetTarget(out var tmp))
							alive++;
					return alive;
				})
			};
			statisticsTimer = new DispatcherTimer(DispatcherPriority.Background)
			{
				Interval = new TimeSpan(0, 0, 1)
			};
			statisticsTimer.Tick += (s, e) => { if (this["collectStatistics"] is bool collect && collect) foreach (var stat in statistics) stat.Request(); };
			statisticsTimer.Start();
		}

		/// <summary>Returns the available Statistics</summary>
		public List<StatisticElement> Statistics => statistics;

		#endregion Statistics

		/// <summary>Internal Id of the environment.</summary>
		[LuaMember]
		public int EnvironmentId => environmentId;

		/// <summary>User Id, not the contact id.</summary>
		[LuaMember]
		public long UserId => userId;
		/// <summary>Displayname for UI.</summary>
		[LuaMember]
		public string UsernameDisplay =>
			GetMemberValue("FullName", rawGet: true) as string
			?? GetMemberValue("displayName", rawGet: true) as string
			?? $"User:{UserId}";

		/// <summary>Path of the local data for the user.</summary>
		[LuaMember]
		public DirectoryInfo LocalPath => localDirectory;

		/// <summary>List of actions defined for an context.</summary>
		[LuaMember]
		public PpsEnvironmentCollection<PpsActionDefinition> Actions => actions;
		/// <summary>Navigator views.</summary>
		[LuaMember]
		public PpsEnvironmentCollection<PpsViewDefinition> Views => views;
		/// <summary>Registered global templates</summary>
		[LuaMember]
		public PpsEnvironmentCollection<PpsDataTemplateDefinition> Templates => templateDefinitions;

		/// <summary>The current mode of the environment.</summary>
		public PpsEnvironmentMode CurrentMode { get; private set; } = PpsEnvironmentMode.None;

		/// <summary>The current state of the environment.</summary>
		public PpsEnvironmentState CurrentState { get; private set; } = PpsEnvironmentState.None;

		/// <summary>Current state of the environment</summary>
		[LuaMember]
		public bool IsOnline => CurrentState == PpsEnvironmentState.Online;

		/// <summary>Access trace log</summary>
		[LuaMember]
		public override IPpsLogger Log => traceLog;
		
		/// <summary>Test if Network is present.</summary>
		[LuaMember]
		public bool IsNetworkPresent
			=> System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable();

		[LuaMember(nameof(BackgroundProgressState))]
		public PpsProgressStack BackgroundProgressState => backgroundProgress;
		[LuaMember(nameof(ForegroundProgressState))]
		public PpsProgressStack ForegroundProgressState => forgroundProgress;

		// -- Static ----------------------------------------------------------

		#region -- class DynamicReferencedAssembly ------------------------------------

		private class DynamicReferencedAssembly : IEquatable<AssemblyName>
		{
			public DynamicReferencedAssembly(Uri uri, AssemblyName name)
			{
				Uri = uri ?? throw new ArgumentNullException(nameof(uri));
				Name = name ?? throw new ArgumentNullException(nameof(uri));
			} // ctor

			public bool Equals(AssemblyName other)
				=> CompareAssemblyName(Name, other);

			public AssemblyName Name { get; }
			public Uri Uri { get; }

			public Assembly Loaded { get; set; } = null;
			public DateTime LastWriteTime { get; set; } = DateTime.MinValue;
		} // class DynamicReferencedAssembly

		#endregion

		private static object environmentCounterLock = new object();
		private static int environmentCounter = 1;

		private static readonly Regex internalUri = new Regex(@"^ppsn\d+.local$", RegexOptions.Compiled);
		private static List<DynamicReferencedAssembly> dynamicReferencedAssemblies = new List<DynamicReferencedAssembly>(); // list with referenced assemblies for the resolver

		static PpsEnvironment()
		{
			// install resolver for referenced assemblies
			AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
		} // ctor 

		private static bool CompareAssemblyName(AssemblyName a, AssemblyName b)
			=> String.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase) == 0
				&& a.Version == b.Version;

		private static DynamicReferencedAssembly FindReferencedAssembly(AssemblyName name)
		{
			lock (dynamicReferencedAssemblies)
			{
				return (
					from c in dynamicReferencedAssemblies
					where c.Equals(name)
					select c
				).FirstOrDefault();
			}
		} // func FindReferencedAssembly

		private static DynamicReferencedAssembly FindReferencedAssemblyByUri(Uri uri)
		{
			lock (dynamicReferencedAssemblies)
			{
				return (
					from c in dynamicReferencedAssemblies
					where WebRequestHelper.EqualUri(uri, c.Uri)
					select c
				).FirstOrDefault();
			}
		} // func FindReferencedAssemblySource

		private static Assembly FindLoadedAssembly(AssemblyName name)
		{
			return (
				from c in AppDomain.CurrentDomain.GetAssemblies()
				where CompareAssemblyName(name, c.GetName())
				select c
			).FirstOrDefault();
		} // func FindLoadedAssembly

		private static void UpdateDynamicReferencedAssembly(Uri uri, AssemblyName referencedName, Assembly loadedAssembly, DateTime loadeLastWriteTime)
		{
			var item = FindReferencedAssembly(referencedName);
			if (item == null)
			{
				item = new DynamicReferencedAssembly(uri, referencedName);
				dynamicReferencedAssemblies.Add(item);
			}

			if (loadedAssembly != null)
			{
				item.Loaded = loadedAssembly;
				item.LastWriteTime = loadeLastWriteTime;
			}
		} // porc UpdateDynamicReferencedAssembly

		private static Assembly RegisterReferencedAssemblySource(Uri baseUri, Assembly assembly, DateTime modifiedDate)
		{
			// Registers all referenced assemblies to an currently loaded assembly.
			// This is needed the load the references lazy.
			lock (dynamicReferencedAssemblies)
			{
				// add self
				UpdateDynamicReferencedAssembly(baseUri, assembly.GetName(), assembly, modifiedDate);

				// add references
				foreach (var referencedName in assembly.GetReferencedAssemblies())
					UpdateDynamicReferencedAssembly(new Uri(baseUri, new Uri(referencedName.Name + ".dll", UriKind.Relative)), referencedName, null, DateTime.MinValue);
			}

			return assembly;
		} // func RegisterReferencedAssemblySource

		private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
		{
			var name = new AssemblyName(args.Name);

			var asm = FindLoadedAssembly(name);
			if (asm == null)
			{
				var ra = FindReferencedAssembly(name);
				if (ra != null)
				{
					if (ra.Loaded != null)
						asm = ra.Loaded;
					else
						asm = LoadAssemblyFromUriAsync(ra.Uri).AwaitTask();
				}
			}

			return asm;
		} // func CurrentDomain_AssemblyResolve

		private static async Task<Assembly> LoadAssemblyFromUriAsync(Uri uri)
		{
			// check for environment uri, we need an absolute and local uri
			if (!(uri.IsAbsoluteUri && (uri.IsFile || internalUri.IsMatch(uri.Host))))
				throw new ArgumentOutOfRangeException(nameof(uri), uri, "Invalid uri to load an assembly.");

			// check if loaded
			var ra = FindReferencedAssemblyByUri(uri);
			if (ra?.Loaded != null)
				return ra.Loaded;

			// get the binary
			if (uri.IsFile) // load from file
			{
				var fiDll = new FileInfo(uri.AbsolutePath);
				var fiPdb = new FileInfo(Path.ChangeExtension(uri.AbsolutePath, ".pdb"));
				if (!fiDll.Exists)
					return null;

				using (var srcDll = fiDll.OpenRead())
				using (var srcPdb = fiPdb?.OpenRead())
				{
					var asm = Assembly.Load(
						await srcDll.ReadInArrayAsync(),
						srcPdb == null ? null : await srcPdb.ReadInArrayAsync()
					);

					return RegisterReferencedAssemblySource(uri, asm, fiDll.LastWriteTime);
				}
			}
			else // load from web source
			{
				var env = GetEnvironment();
				if (env == null)
					throw new InvalidOperationException("No environment found to load an remote assembly.");

				// create assembly cache directory
				var assemblyCache = new DirectoryInfo(Path.Combine(env.LocalPath.FullName, "assemblies"));
				if (!assemblyCache.Exists)
					assemblyCache.Create();

				// fetch from server
				using (var r = await env.Request.GetResponseAsync(env.Request.MakeRelative(uri)))
				{
					var fileInfo = r.GetContentDisposition();
					var modificationDate = fileInfo.ModificationDate?.DateTime ?? DateTime.MinValue;

					var targetFile = new FileInfo(Path.Combine(assemblyCache.FullName, fileInfo.FileName.Trim('"')));

					// download file in cache
					if (!targetFile.Exists || modificationDate == DateTime.MinValue || targetFile.LastWriteTime < modificationDate)
					{
						using (var src = await r.Content.ReadAsStreamAsync())
						using (var dst = targetFile.OpenWrite())
							await src.CopyToAsync(dst);

						if (modificationDate > DateTime.MinValue)
							targetFile.LastAccessTime = modificationDate;
					}

					// get assembly name
					var name = AssemblyName.GetAssemblyName(targetFile.FullName);
					return RegisterReferencedAssemblySource(uri, FindLoadedAssembly(name) ?? Assembly.LoadFile(targetFile.FullName), modificationDate);
				}
			}
		} // func LoadAssemblyFromUriAsync

		/// <summary>Pre load an assembly.</summary>
		/// <param name="fileName"></param>
		/// <returns></returns>
		public static Task LoadAssemblyFromLocalAsync(string fileName)
			=> LoadAssemblyFromUriAsync(new Uri("file://" + fileName.Replace('\\', '/')));

		/// <summary>Get the Environment, that is attached to the current application.</summary>
		/// <returns></returns>
		public static PpsEnvironment GetEnvironment(DependencyObject dependencyObject = null)
			=> GetShell<PpsEnvironment>(dependencyObject);
	} // class PpsEnvironment

	#endregion
}
