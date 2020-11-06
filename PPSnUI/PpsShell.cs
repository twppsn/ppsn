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
using System.ComponentModel;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Networking;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;
using TecWare.PPSn.Networking;
using TecWare.PPSn.UI;

namespace TecWare.PPSn
{
	#region -- class PpsServiceAttribute ----------------------------------------------

	/// <summary>Mark a class as service</summary>
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
	public sealed class PpsServiceAttribute : Attribute
	{
		private readonly Type serviceType;

		/// <summary></summary>
		/// <param name="serviceType"></param>
		public PpsServiceAttribute(Type serviceType)
		{
			this.serviceType = serviceType ?? throw new ArgumentNullException(nameof(serviceType));
		} // ctor

		/// <summary>Type of the default service.</summary>
		public Type Type => serviceType;
	} // class PpsServiceAttribute

	#endregion

	#region -- class PpsLazyServiceAttribute ------------------------------------------

	/// <summary>Mark a class as service</summary>
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
	public sealed class PpsLazyServiceAttribute : Attribute
	{
		/// <summary></summary>
		public PpsLazyServiceAttribute()
		{
		} // ctor
	} // class PpsLazyServiceAttribute

	#endregion

	#region -- interface IPpsShellApplication -----------------------------------------

	/// <summary>This interface is implemented by the main assembly.</summary>
	public interface IPpsShellApplication
	{
		/// <summary>Schedule restart for the application, because a new version of the package is detected.</summary>
		/// <param name="shell"></param>
		/// <param name="uri"></param>
		Task RequestUpdateAsync(IPpsShell shell, Uri uri);
		/// <summary>Schedule restart for the application, because a new version is detected.</summary>
		/// <param name="shell"></param>
		Task RequestRestartAsync(IPpsShell shell);

		/// <summary>Internal application name of the host.</summary>
		string Name { get; }
		/// <summary>Version of the running application.</summary>
		Version AssenblyVersion { get; }
		/// <summary>Version if the installed package.</summary>
		Version InstalledVersion { get; }
	} // interface IPpsShellApplication

	#endregion

	#region -- interface IPpsShellService ---------------------------------------------

	/// <summary>Service that is hosted with a shell environment.</summary>
	public interface IPpsShellService
	{
		/// <summary>Shell of the shell service.</summary>
		IPpsShell Shell { get; }
	} // interface IPpsShellService

	#endregion

	#region -- interface IPpsShellServiceInit -----------------------------------------

	/// <summary>Shell service supports initialization.</summary>
	public interface IPpsShellServiceInit
	{
		/// <summary>Init shell service</summary>
		/// <returns></returns>
		Task InitAsync();
		/// <summary>Init user</summary>
		/// <returns></returns>
		Task InitUserAsync();
		/// <summary>Done user</summary>
		/// <returns></returns>
		Task DoneUserAsync();
		/// <summary>Destruct shell service</summary>
		/// <returns></returns>
		Task DoneAsync();
	} // interface IPpsShellServiceInit

	#endregion

	#region -- interface IPpsShellServiceSite -----------------------------------------

	/// <summary>Service that is hosted with a shell environment.</summary>
	public interface IPpsShellServiceSite : IPpsShellService
	{
		/// <summary>Set shell from the site</summary>
		/// <param name="shell"></param>
		void SetShell(IPpsShell shell);
	} // interface IPpsShellService

	#endregion

	#region -- interface IPpsShell ----------------------------------------------------

	/// <summary>Active shell for services.</summary>
	public interface IPpsShell : IServiceContainer, IPpsCommunicationService, IServiceProvider, INotifyPropertyChanged, IDisposable
	{
		/// <summary>Create a new http request for the uri.</summary>
		/// <param name="uri"></param>
		/// <returns></returns>
		DEHttpClient CreateHttp(Uri uri = null);

		/// <summary>User login to shell</summary>
		/// <param name="userInfo"></param>
		/// <returns></returns>
		Task LoginAsync(ICredentials userInfo);

		/// <summary>Invoke a shutdown of this shell.</summary>
		/// <returns></returns>
		Task<bool> ShutdownAsync();

		/// <summary>Startup parameter for the device.</summary>
		string DeviceId { get; }
		/// <summary>Return the shell info for this shell.</summary>
		IPpsShellInfo Info { get; }
		/// <summary>Settings of this shell.</summary>
		PpsShellSettings Settings { get; }
		/// <summary>Settings of this shell.</summary>
		PpsShellUserSettings UserSettings { get; }

		/// <summary>Local store for the instance data.</summary>
		DirectoryInfo LocalPath { get; }
		/// <summary>Local store for the user data of the instance.</summary>
		DirectoryInfo LocalUserPath { get; }

		/// <summary>Is the shell fully loaded.</summary>
		bool IsInitialized { get; }
	} //  interface IPpsShell

	#endregion

	#region -- interface IPpsShellInfo ------------------------------------------------

	/// <summary>Basic shell attributes.</summary>
	public interface IPpsShellInfo : IEquatable<IPpsShellInfo>, IPropertyReadOnlyDictionary, INotifyPropertyChanged
	{
		/// <summary>Create the settings for this shell.</summary>
		/// <returns></returns>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		Task<IPpsSettingsService> LoadSettingsAsync(IPpsShell shell);

		/// <summary>Name of the instance.</summary>
		string Name { get; }
		/// <summary>Displayname of the instance for the user</summary>
		string DisplayName { get; }
		/// <summary>Uri</summary>
		Uri Uri { get; }

		/// <summary>Local store for the instance data.</summary>
		DirectoryInfo LocalPath { get; }
	} // interface IPpsShellInfo

	#endregion

	#region -- interface IPpsShellFactory ---------------------------------------------

	/// <summary>Service to enumerate all registered shells.</summary>
	public interface IPpsShellFactory : IEnumerable<IPpsShellInfo>
	{
		/// <summary>Register a new shell.</summary>
		/// <param name="instanceName">Name for shell</param>
		/// <param name="displayName"></param>
		/// <param name="uri">Uri to connect.</param>
		/// <returns>Shell info.</returns>
		IPpsShellInfo CreateNew(string instanceName, string displayName, Uri uri);
	} // interface IPpsShellFactory

	#endregion

	#region -- interface IPpsShellLoadNotify ------------------------------------------

	/// <summary>Interface that notifies states during shell initialization.</summary>
	public interface IPpsShellLoadNotify
	{
		/// <summary>First notify during load, after the cache instance information are loaded.</summary>
		/// <param name="shell"></param>
		/// <returns></returns>
		Task OnBeforeLoadSettingsAsync(IPpsShell shell);
		
		/// <summary>Notify basic settings are loaded from server</summary>
		/// <param name="shell"></param>
		/// <returns></returns>
		Task OnAfterLoadSettingsAsync(IPpsShell shell);

		/// <summary>Shell is finally initialized.</summary>
		/// <param name="shell"></param>
		/// <returns></returns>
		Task OnAfterInitServicesAsync(IPpsShell shell);
	} // interface IPpsShellLoadNotify

	#endregion

	#region -- class PpsShellSettings -------------------------------------------------

	/// <summary>Basic shell settings</summary>
	public sealed class PpsShellSettings : PpsSettingsInfoBase
	{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public const string DpcUriKey = "DPC.Uri";
		public const string DpcUserKey = "DPC.User";
		public const string DpcPasswordKey = "DPC.Password";
		public const string DpcPinKey = "DPC.Pin";
		public const string DpcDebugModeKey = "DPC.Debug";

		public const string DpcDeviceIdKey = "DPC.DeviceId";

		public const string PpsnUriKey = "PPSn.Uri";
		public const string PpsnNameKey = "PPSn.Name";
		public const string PpsnSecurtiyKey = "PPSn.Security";
		public const string PpsnVersionKey = "PPSn.Version";

		public const string ClockFormatKey = "PPSn.ClockFormat"; // todo: bde
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		/// <summary>Basic shell settings</summary>
		/// <param name="settingsService"></param>
		public PpsShellSettings(IPpsSettingsService settingsService) 
			: base(settingsService)
		{
		} // ctor

		/// <summary>Return the dpc user and password.</summary>
		/// <returns></returns>
		public ICredentials GetDpcCredentials()
		{
			var userName = this.GetProperty(DpcUserKey, null);
			var password = this.GetProperty(DpcPasswordKey, null);

			if (String.IsNullOrEmpty(userName))
				return null;

			return UserCredential.Create(userName, password);
		} // func GetDpcCredentials

		/// <summary>Uri</summary>
		public Uri DpcUri => PpsShell.GetUriFromString(this.GetProperty(DpcUriKey, null), UriKind.Absolute);
		/// <summary>Pin to unprotect the application.</summary>
		public string DpcPin => this.GetProperty(DpcPinKey, "2682");
		/// <summary>Is the application in debug mode.</summary>
		public bool IsDebugMode => this.GetProperty(DpcDebugModeKey, false);
		/// <summary>Id of the device.</summary>
		public string DpcDeviceId => this.GetProperty(DpcDeviceIdKey, "(unknown)");

		/// <summary>Target uri of the shell.</summary>
		public string Uri => this.GetProperty<string>(PpsnUriKey, null);
		/// <summary>Name of the server.</summary>
		public string Name => this.GetProperty<string>(PpsnNameKey, null);
		/// <summary>Supported authentification models</summary>
		public string Security => this.GetProperty<string>(PpsnSecurtiyKey, null);
		/// <summary>Server version of the application.</summary>
		public string Version => this.GetProperty<string>(PpsnVersionKey, null);

		/// <summary></summary>
		public string ClockFormat => this.GetProperty(ClockFormatKey, "HH:mm\ndd.MM.yyyy");
	} // class PpsShellSettings

	#endregion

	#region -- class PpsShellUserSettings ---------------------------------------------

	/// <summary>Basic user settings.</summary>
	public sealed class PpsShellUserSettings : PpsSettingsInfoBase
	{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public const string UserIdKey = "PPSn.User.Id";
		public const string UserDisplayNameKey = "PPSn.User.Name";
		public const string UserIdentiyKey = "PPSn.User.Identity";
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		/// <summary>Basic user settings.</summary>
		/// <param name="settingsService"></param>
		public PpsShellUserSettings(IPpsSettingsService settingsService)
			: base(settingsService)
		{
		} // ctor

		/// <summary>Name of the server.</summary>
		public long UserId => this.GetProperty(UserIdKey, 0L);
		/// <summary>Supported authentification models</summary>
		public string UserName => this.GetProperty<string>(UserDisplayNameKey, null);
		/// <summary>Server version of the application.</summary>
		public string UserIdentity => this.GetProperty<string>(UserIdentiyKey, null);
	} // class PpsShellSettings

	#endregion

	#region -- class PpsShell ---------------------------------------------------------

	/// <summary>Global environment to host multiple active environments.</summary>
	public static partial class PpsShell
	{
		#region -- class TypeComparer -------------------------------------------------

		private sealed class TypeComparer : IEqualityComparer<Type>
		{
			public bool Equals(Type x, Type y)
				=> x.IsEquivalentTo(y);

			public int GetHashCode(Type obj)
				=> obj.FullName.GetHashCode();

			public static IEqualityComparer<Type> Default { get; } = new TypeComparer();
		} // class TypeComparer

		#endregion

		#region -- class PpsMessageHandler --------------------------------------------

		private sealed class PpsMessageHandler : HttpClientHandler
		{
			private readonly IPpsShell shell;

			public PpsMessageHandler(IPpsShell shell)
			{
				this.shell = shell ?? throw new ArgumentNullException(nameof(shell));
			} // ctor

			protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
			{
				// todo: proxy?
				return base.SendAsync(request, cancellationToken);
			} // func SendAsync
		} // class PpsMessageHandler

		#endregion

		#region -- class PpsBackgroundWorker ------------------------------------------

		private sealed class PpsBackgroundWorker : IDisposable
		{
			#region -- class PpsBackgroundContext -------------------------------------

			private sealed class PpsBackgroundContext : SynchronizationContext, IPpsProcessMessageLoop
			{
				private readonly PpsBackgroundWorker worker;

				/// <summary></summary>
				/// <param name="thread"></param>
				public PpsBackgroundContext(PpsBackgroundWorker thread)
					=> this.worker = thread ?? throw new ArgumentNullException(nameof(thread));

				/// <summary></summary>
				/// <returns></returns>
				public override SynchronizationContext CreateCopy()
					=> new PpsBackgroundContext(worker);

				public override void Post(SendOrPostCallback d, object state)
					=> worker.Post(d, state);

				public override void Send(SendOrPostCallback d, object state)
					=> worker.Send(d, state);
				
				void IPpsProcessMessageLoop.ProcessMessageLoop(CancellationToken cancellationToken) 
					=> worker.ProcessMessageLoop(cancellationToken);
			} // class DEThreadContext

			#endregion

			#region -- struct CurrentTaskItem -----------------------------------------

			private struct CurrentTaskItem
			{
				public SendOrPostCallback Callback;
				public object State;
				public ManualResetEventSlim Wait;
			} // struct CurrentTaskItem

			#endregion

			private readonly PpsShellImplementation shell;

			private readonly Thread thread;
			private readonly Queue<CurrentTaskItem> currentTasks = new Queue<CurrentTaskItem>();
			private volatile bool threadDoStop = false;
			private readonly ManualResetEventSlim communicationThreadRun;
			private readonly ManualResetEventSlim tasksFilled;

			private DateTime nextBackgroundCycle = DateTime.MinValue;

			#region -- Ctor/Dtor ----------------------------------------------------------

			public PpsBackgroundWorker(PpsShellImplementation shell)
			{
				this.shell = shell ?? throw new ArgumentNullException(nameof(shell));

				tasksFilled = new ManualResetEventSlim(false);

				communicationThreadRun = new ManualResetEventSlim(false);
				thread = new Thread(ExecuteCommunication)
				{
					Name = nameof(PpsBackgroundWorker) + " " + shell.Info.Name,
					IsBackground = true
				};
				thread.Start();
			} // ctor

			public void Dispose()
			{
				threadDoStop = true;
				communicationThreadRun.Set();
				thread.Join(5000);
				tasksFilled.Dispose();
			} // prop Dispose

			#endregion

			#region -- Message Loop ---------------------------------------------------

			private void ResetMessageLoopUnsafe()
			{
				if (!threadDoStop)
					tasksFilled.Reset();
			} // proc ResetMessageLoopUnsafe

			private void PulseMessageLoop()
			{
				lock (tasksFilled)
					tasksFilled.Set();
			} // proc PulseMessageLoop

			private bool TryDequeueTask(out SendOrPostCallback d, out object state, out ManualResetEventSlim wait)
			{
				lock (currentTasks)
				{
					if (currentTasks.Count == 0)
					{
						ResetMessageLoopUnsafe();
						d = null;
						state = null;
						wait = null;
						return false;
					}
					else
					{
						var currentTask = currentTasks.Dequeue();
						d = currentTask.Callback;
						state = currentTask.State;
						wait = currentTask.Wait;
						return true;
					}
				}
			} // proc TryDequeueTask

			private void EnqueueTask(SendOrPostCallback d, object state, ManualResetEventSlim waitHandle)
			{
				lock (currentTasks)
				{
					currentTasks.Enqueue(new CurrentTaskItem() { Callback = d, State = state, Wait = waitHandle });
					PulseMessageLoop();
				}
			} // proc EnqueueTask

			internal void Post(SendOrPostCallback d, object state)
				=> EnqueueTask(d, state, null);

			internal void Send(SendOrPostCallback d, object state)
			{
				if (thread == Thread.CurrentThread)
					throw new InvalidOperationException($"Send can not be called from the same thread (Deadlock).");

				using (var waitHandle = new ManualResetEventSlim(false))
				{
					EnqueueTask(d, state, waitHandle);
					waitHandle.Wait();
				}
			} // proc Send

			private void ProcessMessageLoopUnsafe(CancellationToken cancellationToken)
			{
				// if cancel, then run the loop, we avoid an TaskCanceledException her
				cancellationToken.Register(PulseMessageLoop);

				// process messages until cancel
				while (!threadDoStop)
				{
					while (TryDequeueTask(out var d, out var state, out var wait))
					{
						try
						{
							d(state);
						}
						finally
						{
							if (wait != null)
								wait.Set();
						}
					}

					// wait for event
					if (cancellationToken.IsCancellationRequested)
						break;
					tasksFilled.Wait();
				}
			} // proc ProcessMessageLoopUnsafe

			public void ProcessMessageLoop(CancellationToken cancellationToken)
			{
				if (thread != Thread.CurrentThread)
					throw new InvalidOperationException($"Process of the queued task is only allowed in the same thread.(queue threadId {thread.ManagedThreadId}, caller thread id: {Thread.CurrentThread.ManagedThreadId})");

				ProcessMessageLoopUnsafe(cancellationToken);
			} // proc ProcessMessageLoop

			#endregion

			#region -- Background Communication -------------------------------------------

			private void ExecuteCommunication()
			{
				// todo: allow spin cycle

				SynchronizationContext.SetSynchronizationContext(new PpsBackgroundContext(this));

				// stop for every
				while (!threadDoStop)
				{
					// run synchronization
					if (nextBackgroundCycle <= DateTime.Now)
					{
						nextBackgroundCycle = DateTime.Now.AddSeconds(15); // ask every 15 seconds

						// start batch of background tasks
						var t = shell.RunBackgroundTasksAsync();

						// process queue until all tasks are done
						using (var cancellationTokenSource = new CancellationTokenSource())
						{
							t.GetAwaiter().OnCompleted(cancellationTokenSource.Cancel);
							ProcessMessageLoop(cancellationTokenSource.Token);
						}
					}

					// calculate next wake up time
					var sleepFor = (int)(nextBackgroundCycle - DateTime.Now).TotalMilliseconds;
					if (sleepFor <= 0)
						Thread.Sleep(100);
					else if (communicationThreadRun.Wait(sleepFor))
						communicationThreadRun.Reset();
				}
			} // proc ExecuteCommunication

			#endregion
		} // class PpsBackgroundWorker 

		#endregion

		#region -- class PpsShellImplementation ---------------------------------------

		private sealed class PpsShellImplementation : IServiceContainer, IPpsShell, IPpsSettingsService, ILogger, IDisposable
		{
			public event PropertyChangedEventHandler PropertyChanged;

			private readonly IServiceProvider parentProvider;
			private readonly IPpsShellInfo info;
			private readonly Dictionary<Type, object> services = new Dictionary<Type, object>(TypeComparer.Default);
			private IPpsSettingsService settingsService = null;
			private IPpsSettingsService userSettingsService = null;
			private PpsShellSettings instanceSettingsInfo = null;
			private PpsShellUserSettings userSettingsInfo = null;
			private DirectoryInfo localUserPath = null;

			private readonly PpsBackgroundWorker backgroundWorker = null;
			private DEHttpClient http = null;
			private bool isOnline = false;

			#region -- Ctor/Dtor ------------------------------------------------------

			public PpsShellImplementation(IServiceProvider parentProvider, IPpsShellInfo info)
			{
				this.info = info ?? throw new ArgumentNullException(nameof(info));
				this.parentProvider = parentProvider ?? throw new ArgumentNullException(nameof(parentProvider));

				AddService(typeof(IPpsShell), this);
				AddService(typeof(IPpsCommunicationService), this);
				AddService(typeof(ILogger), this);

				backgroundWorker = new PpsBackgroundWorker(this);
			} // ctor

			public void Dispose()
			{
				if (currentShell == this)
					SetCurrent(null);

				// stop background worker
				backgroundWorker.Dispose();

				// dispose all services
				foreach (var cur in services.OfType<IDisposable>())
					cur.Dispose();

				// remove all services from memory
				services.Clear();
			} // proc Dispose

			internal object CreateShellService(Type serviceInstanceType)
			{
				var ciNone = serviceInstanceType.GetConstructor(Array.Empty<Type>());
				var ciShell = serviceInstanceType.GetConstructor(new Type[] { typeof(IPpsShell) });
				if (ciShell == null && ciNone == null)
					throw new ArgumentException($"Invalid constructor for type {serviceInstanceType.Name}. Expected: ctor(), ctor({nameof(IPpsShell)})");

				return ciShell?.Invoke(new object[] { this }) ?? ciNone.Invoke(Array.Empty<object>());
			} // func CreateShellService

			public async Task LoadAsync(IPpsShellLoadNotify notify)
			{
				// first create settings
				settingsService = await info.LoadSettingsAsync(this);
				AddService(typeof(IPpsSettingsService), this);
				if (notify != null)
					await notify.OnBeforeLoadSettingsAsync(this);

				// start communication
				instanceSettingsInfo = new PpsShellSettings(settingsService);
				info.PropertyChanged += Info_PropertyChanged;

				try
				{
					// create a none user context for the initialization

					using (var dpcHttp = CreateHttpCore(info.Uri, Settings.GetDpcCredentials()))
					{
						http = dpcHttp;

						// load settings from server
						await LoadSettingsFromServerAsync(settingsService, this, instanceSettingsInfo.DpcDeviceId, 0);


						// notify settings loaded
						OnPropertyChanged(nameof(Settings));
						if (notify != null)
							await notify.OnAfterLoadSettingsAsync(this);

						// init all shell services
						foreach (var sv in shellServices)
						{
							if (sv.GetCustomAttribute<PpsLazyServiceAttribute>() != null)
								AddServices(this, sv, new LazyShellServiceCreator(this, sv).CreateService);
							else
								AddServices(this, sv, CreateShellService(sv));
						}

						// load shell services
						foreach (var init in services.Values.OfType<IPpsShellServiceInit>())
							await init.InitAsync();

						if (notify != null)
							await notify.OnAfterInitServicesAsync(this);
					}
				}
				finally
				{
					http = null;
				}

				// notify 
				IsInitialized = true;
				OnPropertyChanged(nameof(IsInitialized));
			} // proc LoadAsync

			public async Task<bool> ShutdownAsync()
			{
				// logout user
				await LogoutAsync();

				// notify shell services
				foreach (var init in services.Values.OfType<IPpsShellServiceInit>())
					await init.DoneAsync();

				Dispose();
				return true;
			} // func ShutdownAsync

			private void Info_PropertyChanged(object sender, PropertyChangedEventArgs e)
			{
				if (e.PropertyName == nameof(IPpsShellInfo.Uri))
					UpdateHttp();
			} // event Info_PropertyChanged

			private void OnPropertyChanged(string propertyName)
				=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

			private static string GetDefaultDeviceKey()
				=> Environment.MachineName;

			#endregion

			#region -- IServiceContainer - members ------------------------------------

			private bool TryGetServiceContainer(out IServiceContainer container)
			{
				if (parentProvider is IServiceContainer c)
				{
					container = c;
					return true;
				}
				else
				{
					container = parentProvider.GetService<IServiceContainer>(false);
					return container != null;
				}
			} // TryGetServiceContainer

			private void AddService(Type serviceType, object service, bool promote = false)
			{
				if (promote && TryGetServiceContainer(out var container))
					container.AddService(serviceType, service, promote);
				else
				{
					if (serviceType == null)
						throw new ArgumentNullException(nameof(serviceType));
					if (service == null)
						throw new ArgumentNullException(nameof(service));
					if (!(service is ServiceCreatorCallback || serviceType.IsAssignableFrom(service.GetType())))
						throw new ArgumentException($"Invalid service instance of type {service.GetType().Name} is not compatible with service {serviceType.Name}.");

					if (service is IPpsShellServiceSite s)
						s.SetShell(this);

					services.Add(serviceType, service);
				}
			} // proc AddService

			private void AddServiceCallback(Type serviceType, ServiceCreatorCallback callback, bool promote)
			{
				if (promote && TryGetServiceContainer(out var container))
					container.AddService(serviceType, callback, promote);
				else
					AddService(serviceType, (object)callback, false);
			} // proc AddService

			private void RemoveService(Type serviceType, bool promote)
			{
				if (promote && TryGetServiceContainer(out var container))
					container.RemoveService(serviceType, promote);

				if (serviceType == null)
					throw new ArgumentNullException(nameof(serviceType));

				services.Remove(serviceType);
			} // proc RemoveService

			void IServiceContainer.AddService(Type serviceType, ServiceCreatorCallback callback)
				=> AddServiceCallback(serviceType, callback, false);

			void IServiceContainer.AddService(Type serviceType, ServiceCreatorCallback callback, bool promote)
				=> AddServiceCallback(serviceType, callback, promote);

			void IServiceContainer.AddService(Type serviceType, object serviceInstance)
				=> AddService(serviceType, serviceInstance, false);

			void IServiceContainer.AddService(Type serviceType, object serviceInstance, bool promote)
				=> AddService(serviceType, serviceInstance, promote);

			void IServiceContainer.RemoveService(Type serviceType)
				=> RemoveService(serviceType, false);

			void IServiceContainer.RemoveService(Type serviceType, bool promote)
				=> RemoveService(serviceType, promote);

			object IServiceProvider.GetService(Type serviceType)
			{
				if (services.TryGetValue(serviceType, out var r))
				{
					if (r is ServiceCreatorCallback createNew)
					{
						r = createNew(this, serviceType);
						if (r != null && serviceType.IsAssignableFrom(r.GetType()))
						{
							services[serviceType] = r;
							return r;
						}
						else
							return null;
					}
					else
						return r;
				}
				else
					return parentProvider.GetService(serviceType);
			} // func IServiceProvider.GetService

			#endregion

			#region -- Http -----------------------------------------------------------

			private DEHttpClient CreateHttpCore(Uri uri, ICredentials credentials)
				=> DEHttpClient.Create(uri, credentials, httpHandler: new PpsMessageHandler(this));

			public DEHttpClient CreateHttp(Uri uri = null)
			{
				if (http == null)
					throw new InvalidOperationException();
				else if (uri == null)
					return CreateHttpCore(http.BaseAddress, http.Credentials);
				else if (uri.IsAbsoluteUri)
				{
					var relativeUri = http.BaseAddress.MakeRelativeUri(uri);
					return CreateHttpCore(new Uri(http.BaseAddress, relativeUri), http.Credentials);
				}
				else // relative to base
					return CreateHttpCore(new Uri(http.BaseAddress, uri), http.Credentials);
			} // func CreateHttp

			private void UpdateHttp()
			{
				if (http == null)
				{
					// todo:
					// Settings.GetPPsnUri();
					// InitAsync
					// LoginAsync
				}
			} // proc UpdateHttp

			public async Task LoginAsync(ICredentials credentials)
			{
				var newHttp = CreateHttpCore(info.Uri, credentials);
				try
				{
					var userName = GetUserNameFromCredentials(credentials);

					// create local directory from user name
					localUserPath = new DirectoryInfo(Path.Combine(info.LocalPath.FullName, userName));
					if (!localUserPath.Exists)
						localUserPath.Create();

					// load user settings
					var userSettings = FileSettingsInfo.CreateUserSettings(this, new FileInfo(Path.Combine(LocalUserPath.FullName, "info.xml")), userName);
					await userSettings.LoadAsync();

					// login user and parse user specific settings
					userSettingsService = userSettings;
					userSettingsInfo = new PpsShellUserSettings(userSettingsService);
					await LoadUserSettingsFromServerAsync(userSettingsService, newHttp);
					
					// update http
					http = newHttp;
					OnPropertyChanged(nameof(Http));

					// notify login to services
					foreach (var init in services.Values.OfType<IPpsShellServiceInit>())
						await init.InitUserAsync();
				}
				catch
				{
					newHttp.Dispose();
					throw;
				}
			} // proc LoginAsync
		
			private async Task LogoutAsync()
			{
				foreach (var init in services.Values.OfType<IPpsShellServiceInit>())
					await init.DoneUserAsync();
			} // proc LogoutAsync

			internal Task RunBackgroundTasksAsync()
			{
				// must not return any exception
				return Task.CompletedTask;
			} // proc RunBackgroundTasksAsync

			#endregion

			#region -- Settings -------------------------------------------------------

			async Task IPpsSettingsService.RefreshAsync(bool purge)
			{
				await settingsService.RefreshAsync(purge);
				if (userSettingsService != null)
					await userSettingsService.RefreshAsync(purge);
			} // func RefreshAsync

			IEnumerable<KeyValuePair<string, string>> IPpsSettingsService.Query(params string[] filter)
			{
				var returnedNames = new List<string>();

				// first return user settings
				if (userSettingsService != null)
				{
					foreach (var kv in userSettingsService.Query(filter))
					{
						yield return kv;

						var idx = returnedNames.BinarySearch(kv.Key, StringComparer.OrdinalIgnoreCase);
						if (idx < 0)
							returnedNames.Insert(~idx, kv.Key);
					}
				}

				// fill up with instance settings
				foreach (var kv in settingsService.Query(filter))
				{
					if (returnedNames.BinarySearch(kv.Key, StringComparer.OrdinalIgnoreCase) < 0)
						yield return kv;
				}
			} // func IPpsSettingsService.Query

			Task<int> IPpsSettingsService.UpdateAsync(params KeyValuePair<string, string>[] values)
				=> (userSettingsService ?? settingsService).UpdateAsync(values);

			#endregion

			#region -- Logger ---------------------------------------------------------

			void ILogger.LogMsg(LogMsgType type, string message)
				=> DebugLogger.LogMsg(type, message);

			#endregion

			public string DeviceId => GetDefaultDeviceKey();
			public IPpsShellInfo Info => info;
			
			public DEHttpClient Http => http;
			public IPpsSettingsService SettingsService => settingsService;
			public PpsShellSettings Settings => instanceSettingsInfo;
			public PpsShellUserSettings UserSettings => userSettingsInfo;

			public DirectoryInfo LocalPath => info.LocalPath;
			public DirectoryInfo LocalUserPath => localUserPath;

			public bool IsInitialized { get; private set; }
			public bool IsAuthentificated => isOnline && http.Credentials != null;
			public bool IsOnline => isOnline;
		} // class PpsShellImplementation

		#endregion

		#region -- class ProxyImplementation ------------------------------------------

		private abstract class ProxyImplementation
		{
			private IPpsShell oldShell;

			protected ProxyImplementation()
			{
				oldShell = currentShell;
				CurrentChanged += PpsSettingsProxy_CurrentChanged;
			} // ctor

			private void PpsSettingsProxy_CurrentChanged(object sender, EventArgs e)
			{
				if (oldShell != null)
					oldShell.PropertyChanged -= CurrentShell_PropertyChanged;

				OnCurrentChanged(currentShell, oldShell);

				if (currentShell != null)
					currentShell.PropertyChanged += CurrentShell_PropertyChanged;

				oldShell = currentShell;
			} // event PpsSettingsProxy_CurrentChanged

			private void CurrentShell_PropertyChanged(object sender, PropertyChangedEventArgs e)
				=> OnCurrentPropertyChanged(e);

			protected virtual void OnCurrentChanged(IPpsShell current, IPpsShell old) { }
			protected virtual void OnCurrentPropertyChanged(PropertyChangedEventArgs e) { }
		} // class ProxyImplementation

		#endregion

		#region -- class PpsSettingsProxy ---------------------------------------------

		private sealed class PpsSettingsProxy : ProxyImplementation, IPpsSettingsService
		{
			public event PropertyChangedEventHandler PropertyChanged;

			private IPpsSettingsService currentSettingsService = null;

			public PpsSettingsProxy()
			{
			} // ctor

			private void ConnectSettingsService(IPpsSettingsService settingsService)
			{
				if(currentSettingsService != null)
					currentSettingsService.PropertyChanged -= CurrentSettingsService_PropertyChanged;

				currentSettingsService = settingsService;

				if(currentSettingsService != null)
					currentSettingsService.PropertyChanged += CurrentSettingsService_PropertyChanged;
			} // proc ConnectSettingsService

			private void ConnectSettingsService(IPpsShell current)
				=> ConnectSettingsService(current is PpsShellImplementation s ? s.SettingsService : null);

			protected override void OnCurrentChanged(IPpsShell current, IPpsShell old)
			{
				base.OnCurrentChanged(current, old);
				ConnectSettingsService(current);
			} // proc OnCurrentChanged

			protected override void OnCurrentPropertyChanged(PropertyChangedEventArgs e)
			{
				if (e.PropertyName == nameof(PpsShellImplementation.Settings)) // Settings service might also changed
					ConnectSettingsService(currentShell?.SettingsService);
				base.OnCurrentPropertyChanged(e);
			} // proc OnCurrentPropertyChanged

			private void CurrentSettingsService_PropertyChanged(object sender, PropertyChangedEventArgs e)
				=> PropertyChanged.Invoke(this, e);

			IEnumerable<KeyValuePair<string, string>> IPpsSettingsService.Query(params string[] filter)
				=> currentSettingsService?.Query(filter) ?? Array.Empty<KeyValuePair<string, string>>();

			Task<int> IPpsSettingsService.UpdateAsync(params KeyValuePair<string, string>[] values)
				=> currentSettingsService?.UpdateAsync(values) ?? Task.FromResult(0);

			Task IPpsSettingsService.RefreshAsync(bool purge)
				=> currentSettingsService?.RefreshAsync(purge) ?? Task.CompletedTask;
		} // class PpsSettingsProxy

		#endregion

		#region -- class PpsCommunicationProxy ----------------------------------------

		private sealed class PpsCommunicationProxy : ProxyImplementation, IPpsCommunicationService
		{
			public event PropertyChangedEventHandler PropertyChanged;

			public PpsCommunicationProxy()
			{
			} // ctor

			protected override void OnCurrentChanged(IPpsShell current, IPpsShell old)
			{
				base.OnCurrentChanged(current, old);

				OnPropertyChanged(new PropertyChangedEventArgs(nameof(Http)));
				OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsAuthentificated)));
				OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsOnline)));
			} // proc OnCurrentChanged

			protected override void OnCurrentPropertyChanged(PropertyChangedEventArgs e)
			{
				switch (e.PropertyName)
				{
					case nameof(Http):
					case nameof(IsAuthentificated):
					case nameof(IsOnline):
						OnPropertyChanged(e);
						break;
					default:
						base.OnCurrentPropertyChanged(e);
						break;
				}
			} // proc OnCurrentPropertyChanged

			private void OnPropertyChanged(PropertyChangedEventArgs e)
				=> PropertyChanged?.Invoke(this, e);

			public DEHttpClient Http => currentShell?.Http;

			public bool IsAuthentificated => currentShell?.IsAuthentificated ?? false;
			public bool IsOnline => currentShell?.IsOnline ?? false;
		} // class PpsCommunicationProxy

		#endregion

		#region -- class LazyServiceCreator -------------------------------------------

		private sealed class LazyServiceCreator
		{
			private readonly Type instanceType;
			private object instance = null;

			public LazyServiceCreator(Type instanceType)
			{
				this.instanceType = instanceType ?? throw new ArgumentNullException(nameof(instanceType));
			} // ctor

			public object CreateService(IServiceContainer serviceContainer, Type serviceType)
			{
				if (instance == null)
					instance = Activator.CreateInstance(instanceType);
				return instance;
			} // func CreateService
		} // class LazyServiceCreator

		#endregion

		#region -- class LazyServiceCreator -------------------------------------------

		private sealed class LazyShellServiceCreator
		{
			private readonly PpsShellImplementation shell;
			private readonly Type instanceType;
			private object instance = null;

			public LazyShellServiceCreator(PpsShellImplementation shell, Type instanceType)
			{
				this.shell = shell ?? throw new ArgumentNullException(nameof(shell));
				this.instanceType = instanceType ?? throw new ArgumentNullException(nameof(instanceType));
			} // ctor

			public object CreateService(IServiceContainer serviceContainer, Type serviceType)
			{
				if (instance == null)
					instance = shell.CreateShellService(instanceType);
				return instance;
			} // func CreateService
		} // class LazyServiceCreator

		#endregion

		/// <summary>Is the current shell changed.</summary>
		public static event EventHandler CurrentChanged;

		private static readonly ServiceContainer global = new ServiceContainer();
		private static readonly Lazy<Version> appVersion = new Lazy<Version>(GetAppVersion);
		private static readonly List<Type> shellServices = new List<Type>();
		private static PpsShellImplementation currentShell = null;

		#region -- Ctor ---------------------------------------------------------------

		static PpsShell()
		{
			global.AddService(typeof(IPpsSettingsService), CreateProxyService);
			global.AddService(typeof(IPpsCommunicationService), CreateProxyService);
			global.AddService(typeof(ILogger), DebugLogger);

			Collect(typeof(PpsShell).Assembly);

			LuaType.RegisterTypeAlias("text", typeof(PpsFormattedStringValue));
			LuaType.RegisterTypeAlias("blob", typeof(byte[]));
		} // sctor

		private static object CreateProxyService(IServiceContainer container, Type serviceType)
		{
			if (serviceType == typeof(IPpsCommunicationService))
				return new PpsCommunicationProxy();
			else if (serviceType == typeof(IPpsSettingsService))
				return new PpsSettingsProxy();
			else
				return null;
		} // func CreateProxyService

		#endregion

		#region -- Collect, AddServices -----------------------------------------------

		/// <summary>Add a defined services (<see cref="PpsServiceAttribute"/>) to the service container.</summary>
		/// <param name="serviceContainer"></param>
		/// <param name="serviceInstance"></param>
		public static void AddServices(this IServiceContainer serviceContainer, object serviceInstance)
			=> AddServices(serviceContainer, serviceInstance.GetType(), serviceInstance);

		/// <summary>Add a defined services (<see cref="PpsServiceAttribute"/>) to the service container.</summary>
		/// <param name="serviceContainer"></param>
		/// <param name="serviceInstanceType"></param>
		/// <param name="serviceInstance"></param>
		public static void AddServices(this IServiceContainer serviceContainer, Type serviceInstanceType, object serviceInstance)
		{
			foreach (var attr in serviceInstanceType.GetCustomAttributes<PpsServiceAttribute>())
				serviceContainer.AddService(attr.Type, serviceInstance);
		} // proc AddServices

		/// <summary>Add a defined services (<see cref="PpsServiceAttribute"/>) to the service container.</summary>
		/// <param name="serviceContainer"></param>
		/// <param name="serviceInstanceType"></param>
		/// <param name="callback"></param>
		public static void AddServices(this IServiceContainer serviceContainer, Type serviceInstanceType, ServiceCreatorCallback callback)
		{
			foreach (var attr in serviceInstanceType.GetCustomAttributes<PpsServiceAttribute>())
				serviceContainer.AddService(attr.Type, callback);
		} // proc AddServices

		/// <summary>Create all services of an assembly.</summary>
		/// <param name="assembly">Assembly to look for services.</param>
		public static void Collect(Assembly assembly)
		{
			foreach (var t in assembly.GetTypes())
			{
				if (t.GetCustomAttributes<PpsServiceAttribute>().FirstOrDefault() == null)
					continue;

				if (typeof(IPpsShellService).IsAssignableFrom(t)) // is this service a shell service
					shellServices.Add(t);
				else if (t.GetCustomAttribute<PpsLazyServiceAttribute>() != null)// add a normal service
					AddServices(global, t, new LazyServiceCreator(t).CreateService);
				else
					AddServices(global, t, Activator.CreateInstance(t));
			}
		} // proc Collect

		#endregion

		#region -- Start --------------------------------------------------------------

		private static void SetCurrent(PpsShellImplementation value)
		{
			if (currentShell != value)
			{
				currentShell = value;
				CurrentChanged?.Invoke(null, EventArgs.Empty);
			}
		} // proc SetCurrent

		/// <summary>Start a new shell.</summary>
		/// <param name="shellInfo"></param>
		/// <param name="isDefault"></param>
		/// <param name="notify"></param>
		/// <returns></returns>
		public static async Task<IPpsShell> StartAsync(IPpsShellInfo shellInfo, bool isDefault = false, IPpsShellLoadNotify notify = null)
		{
			var n = new PpsShellImplementation(global, shellInfo);
			try
			{
				await n.LoadAsync(notify);
				if (isDefault || currentShell == null)
					SetCurrent(n);
				return n;
			}
			catch
			{
				n?.Dispose();
				throw;
			}
		} // proc StartAsync

		/// <summary>Get all registered shell.</summary>
		/// <returns></returns>
		public static IEnumerable<IPpsShellInfo> GetShellInfo()
			=> (IEnumerable<IPpsShellInfo>)GetService<IPpsShellFactory>(false) ?? Array.Empty<IPpsShellInfo>();

		private static IEnumerable<PropertyValue> GetLoadSettingsArguments(IPpsShellApplication application, string clientId, int lastRefreshTick)
		{
			if (application != null)
				yield return new PropertyValue("app", application.Name);
			yield return new PropertyValue("id", clientId);
			yield return new PropertyValue("last", lastRefreshTick);
		} // func GetLoadSettingsArguments

		/// <summary></summary>
		/// <param name="settingsService"></param>
		/// <param name="shell"></param>
		/// <param name="clientId"></param>
		/// <param name="lastRefreshTick"></param>
		/// <returns></returns>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public static async Task<int> LoadSettingsFromServerAsync(IPpsSettingsService settingsService, IPpsShell shell, string clientId, int lastRefreshTick)
		{
			var http = shell.Http;
			var application = Global.GetService<IPpsShellApplication>(false);

			// refresh properties from server
			var sb = new StringBuilder("info.xml");
			HttpStuff.MakeUriArguments(sb, false, GetLoadSettingsArguments(application, clientId, lastRefreshTick));
			var request = new HttpRequestMessage(HttpMethod.Get, http.CreateFullUri(sb.ToString()));
			request.Headers.Accept.TryParseAdd(MimeTypes.Text.Xml);

			// add location info
			request.Headers.Add("x-ppsn-version", AppVersion.ToString());
			var gps = GetService<IPpsGpsService>(false);
			if (gps != null && gps.TryGetGeoCoordinate(out var lng, out var lat, out var ltm))
			{
				request.Headers.Add("x-ppsn-lng", lng.ChangeType<string>());
				request.Headers.Add("x-ppsn-lat", lat.ChangeType<string>());
				request.Headers.Add("x-ppsn-ltm", ltm.ChangeType<string>());
			}
			request.Headers.Add("x-ppsn-wifi", Environment.MachineName + "/" + Environment.UserName);

			// send request
			using (var r = await http.SendAsync(request))
			{
				if (!r.IsSuccessStatusCode)
					throw new HttpResponseException(r);

				if (r.Headers.TryGetValue("x-ppsn-lastrefresh", out var lastRefreshTickValue) && Int32.TryParse(lastRefreshTickValue, out var tmp))
					lastRefreshTick = tmp;

				var xInfo = await r.GetXmlAsync(MimeTypes.Text.Xml, "ppsn");

				// check version of shell application
				if (application != null && Version.TryParse(xInfo.GetAttribute("version", "1.0.0.0"), out var serverVersion))
				{
					var installedVersion = application.InstalledVersion;
					var assemblyVersion = application.AssenblyVersion;

					if (installedVersion < serverVersion) // new version is provided
						await application.RequestUpdateAsync(shell, new Uri(xInfo.GetAttribute("src", null), UriKind.Absolute));
					else if (assemblyVersion < installedVersion) // new version is installed, but not active
						await application.RequestRestartAsync(shell);
				}
				
				// update mime type mappings
				var xMimeTypes = xInfo.Element("mimeTypes");
				if (xMimeTypes != null)
				{
					UpdateMimeTypesFromInfo(xMimeTypes);
					xMimeTypes.Remove();
				}

				// write server settings to file
				var xOptions = xInfo.Element("options");
				if (xOptions != null)
				{
					using (var xml = xOptions.CreateReader())
						await settingsService.UpdateAsync(FileSettingsInfo.ParseInstanceSettings(xml).ToArray());
				}
			}

			return lastRefreshTick;
		} // proc LoadSettingsFromServerAsync

		/// <summary></summary>
		/// <param name="settingsService"></param>
		/// <param name="http"></param>
		/// <returns></returns>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public static async Task LoadUserSettingsFromServerAsync(IPpsSettingsService settingsService, DEHttpClient http)
		{
			// try login for the user
			var xUser = await http.GetXmlAsync("login.xml", rootName: "user");

			// write server settings to file
			using (var xml = xUser.CreateReader())
				await settingsService.UpdateAsync(FileSettingsInfo.ParseUserSettings(xml).ToArray());
		} // proc LoadSettingsFromServerAsync

		/// <summary>Refresh mime type information</summary>
		/// <param name="xMimeTypes"></param>
		public static void UpdateMimeTypesFromInfo(XElement xMimeTypes)
		{
			var x = xMimeTypes.Elements("mimeType");
			if (x != null)
			{
				foreach (var c in x)
				{
					var mimeType = c.GetAttribute("id", null);
					if (mimeType != null)
					{
						var isCompressedContent = c.GetAttribute("isCompressedContent", false);
						var extensions = c.GetAttribute("extensions", null)?.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
						MimeTypeMapping.Update(mimeType, isCompressedContent, false, extensions);
					}
				}
			}
		} // proc UpdateMimeTypesFromInfo

		#endregion

		#region -- GetService ---------------------------------------------------------

		/// <summary>Return a service.</summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="throwException"></param>
		/// <returns></returns>
		public static T GetService<T>(bool throwException = false)
			where T : class
			=> global.GetService<T>(throwException);

		/// <summary>Return a service.</summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="serviceType"></param>
		/// <param name="throwException"></param>
		/// <returns></returns>
		public static T GetService<T>(Type serviceType, bool throwException = false)
			where T : class
			=> global.GetService<T>(serviceType, throwException);

		/// <summary>Return a service.</summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="sp"></param>
		/// <param name="throwException"></param>
		/// <returns></returns>
		public static T GetService<T>(IServiceProvider sp, bool throwException = false)
			where T : class
			=> (sp ?? Current ?? Global).GetService<T>(throwException);

		/// <summary>Return a service.</summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="sp"></param>
		/// <param name="serviceType"></param>
		/// <param name="throwException"></param>
		/// <returns></returns>
		public static T GetService<T>(IServiceProvider sp, Type serviceType, bool throwException = false)
			where T : class
			=> (sp ?? Current ?? Global).GetService<T>(serviceType, throwException);

		#endregion

		#region -- Settings -----------------------------------------------------------

		/// <summary></summary>
		/// <param name="sp"></param>
		/// <returns></returns>
		public static PpsSettingsInfoBase GetSettings(this IServiceProvider sp)
			=> PpsSettingsInfoBase.GetGeneric(sp);

		/// <summary></summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="sp"></param>
		/// <returns></returns>
		public static T GetSettings<T>(this IServiceProvider sp)
			where T : PpsSettingsInfoBase
			=> (T)Activator.CreateInstance(typeof(T), sp.GetService<IPpsSettingsService>(true));

		#endregion

		#region -- Application --------------------------------------------------------

		/// <summary>Get the assembly version from the type.</summary>
		/// <param name="application"></param>
		/// <returns></returns>
		public static Version GetDefaultAssemblyVersion(this IPpsShellApplication application)
		{
			var fileVersion = application.GetType().Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>();
			return fileVersion == null ? new Version(1, 0, 0, 0) : new Version(fileVersion.Version);
		} // func GetDefaultAssemblyVersion

		#endregion

		/// <summary>Current shell</summary>
		public static IPpsShell Current => currentShell;

		/// <summary>Access global service provider.</summary>
		public static IServiceContainer Global => global;
	} // class PpsShell

	#endregion
}
