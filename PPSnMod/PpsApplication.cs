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
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Deployment.WindowsInstaller;
using Neo.IronLua;
using TecWare.DE.Data;
using TecWare.DE.Networking;
using TecWare.DE.Server;
using TecWare.DE.Server.Configuration;
using TecWare.DE.Server.Http;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;
using TecWare.PPSn.Reporting;
using TecWare.PPSn.Server.Data;

namespace TecWare.PPSn.Server
{
	#region -- interface IPpsApplicationInitialization --------------------------------

	/// <summary></summary>
	public interface IPpsApplicationInitialization : IDEConfigItem
	{
		/// <summary>Register a one time task, that will be run durring initialization.</summary>
		/// <param name="order"></param>
		/// <param name="status"></param>
		/// <param name="task"></param>
		void RegisterInitializationTask(int order, string status, Func<Task> task);

		/// <summary>Wait until all application information are processed.</summary>
		/// <param name="timeout"></param>
		/// <returns></returns>
		bool? WaitForInitializationProcess(int timeout = -1);

		/// <summary></summary>
		bool IsInitializedSuccessful { get; }
	} // interface IPpsApplicationInitialization

	#endregion

	#region -- delegate PpsClientOptionHookDelegate -----------------------------------

	/// <summary>Option hook definion</summary>
	/// <param name="r"></param>
	/// <param name="keyMatch"></param>
	/// <param name="value"></param>
	/// <returns></returns>
	public delegate IEnumerable<(string key, object value)> PpsClientOptionHookDelegate(IDEWebScope r, Match keyMatch, string value);

	#endregion

	#region -- interface IPpsClientApplicationInfo ------------------------------------

	/// <summary>Application info for a specific file. This should be a immutable object.</summary>
	public interface IPpsClientApplicationInfo
	{
		/// <summary>Unique key for the application info.</summary>
		string Name { get; }
		/// <summary>Version code to find the best version of an file.</summary>
		long VersionCode { get; }
		/// <summary>User version information</summary>
		Version Version { get; }

		/// <summary>User friendly name of the application.</summary>
		string DisplayName { get; }
		/// <summary>Icon of the application.</summary>
		string Icon { get; }
	} // interface IPpsClientApplicationInfo

	#endregion

	#region -- class PpsClientApplicationInfo -----------------------------------------

	/// <summary>Implementation of <see cref="IPpsClientApplicationInfo"/> </summary>
	public sealed class PpsClientApplicationInfo : IPpsClientApplicationInfo
	{
		/// <summary>Create a client application info.</summary>
		/// <param name="name"></param>
		/// <param name="versionCode"></param>
		/// <param name="version"></param>
		/// <param name="displayName"></param>
		/// <param name="icon"></param>
		public PpsClientApplicationInfo(string name, long versionCode = 0, Version version = null, string displayName = null, string icon = null)
		{
			Name = name ?? throw new ArgumentNullException(nameof(name));
			VersionCode = versionCode < 0 && version != null ? version.GetVersionCode() : versionCode;
			Version = version ?? Version0;
			DisplayName = displayName ?? name;
			Icon = icon;
		} // ctor

		/// <inheritdoc />
		public string Name { get; }
		/// <inheritdoc />
		public long VersionCode { get; }
		/// <inheritdoc />
		public Version Version { get; }

		/// <inheritdoc />
		public string DisplayName { get; }
		/// <inheritdoc />
		public string Icon { get; }

		/// <summary>Default version</summary>
		public static Version Version0 { get; } = new Version(0, 0);
	} // class PpsClientApplicationInfo

	#endregion

	#region -- class PpsClientApplicationSource ---------------------------------------

	/// <summary>Known client application file</summary>
	public sealed class PpsClientApplicationSource : IPpsClientApplicationInfo
	{
		private readonly FileInfo fi;
		private readonly Uri relativeUri;
		private readonly string mimeType;
		private DateTime lastWrite;

		internal PpsClientApplicationSource(FileInfo fi, Uri relativeUri, string mimeType = null)
		{
			this.fi = fi ?? throw new ArgumentNullException(nameof(fi));
			this.relativeUri = relativeUri ?? throw new ArgumentNullException(nameof(relativeUri));
			this.mimeType = mimeType;

			lastWrite = fi.LastWriteTime;
		} // ctor

		/// <summary>Check for a new file version</summary>
		/// <returns></returns>
		public bool? Refresh()
		{
			fi.Refresh();
			if (!fi.Exists)
				return null; // deleted
			else if (lastWrite != fi.LastWriteTime)
			{
				lastWrite = fi.LastWriteTime;
				return true;
			}
			else
				return false;
		} // proc Refresh

		/// <summary>File information</summary>
		public FileInfo Info => fi;
		/// <summary>Uri relative to the ppsn-node</summary>
		public Uri Uri => relativeUri;
		/// <summary>Get defined mime type or <c>null</c>.</summary>
		public string MimeType => mimeType;

		string IPpsClientApplicationInfo.Name => relativeUri.ToString();
		long IPpsClientApplicationInfo.VersionCode => 0;
		Version IPpsClientApplicationInfo.Version => PpsClientApplicationInfo.Version0;
		string IPpsClientApplicationInfo.DisplayName => fi.Name;
		string IPpsClientApplicationInfo.Icon => null;
	} // class PpsClientApplicationSource

	#endregion

	#region -- delegate PpsClientApplicationInfoDelegate ------------------------------

	/// <summary>Retrieves a application info from an application source.</summary>
	public delegate IPpsClientApplicationInfo PpsClientApplicationInfoDelegate(PpsClientApplicationSource source);
	
	#endregion

	/// <summary>Base service provider, for all pps-moduls:
	/// - user administration
	/// - data cache, for commonly used data or states
	/// - view services (executes and updates all views, to data)
	/// </summary>
	public partial class PpsApplication : DEConfigLogItem, IPpsApplicationInitialization
	{
		/// <summary>Security token for dpc tasks</summary>
		public const string SecurityDpc = "dpc.sec";
		
		private const RegexOptions clientOptionHookRegexOptions = RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled;
		private const string propertyCategory = "Applications";
		private const string refreshAppsAction = "refreshApp";

		#region -- struct InitializationTask ------------------------------------------

		private struct InitializationTask : IComparable<InitializationTask>
		{
			public int CompareTo(InitializationTask other)
			{
				var r = Order.CompareTo(other.Order);
				return r == 0 ? Status.CompareTo(other.Status) : r;
			} // func CompareTo

			public int Order { get; set; }
			public string Status { get; set; }
			public Func<Task> Task { get; set; }
		} // struct InitializationTask

		#endregion

		#region -- class PpsClientApplicationType -------------------------------------

		private sealed class PpsClientApplicationType
		{
			private readonly string type;
			private readonly PpsClientApplicationInfoDelegate tryGet;

			public PpsClientApplicationType(string type, PpsClientApplicationInfoDelegate tryGet)
			{
				this.type = type ?? throw new ArgumentNullException(nameof(type));
				this.tryGet = tryGet ?? throw new ArgumentNullException(nameof(tryGet));
			} // ctor

			public bool TryGet(PpsClientApplicationSource source, out IPpsClientApplicationInfo info)
			{
				info = tryGet.Invoke(source);
				return info != null;
			} // func TryGet

			[DEListTypeProperty("@type")]
			public string Type => type;
		} // class PpsClientApplicationType

		#endregion

		#region -- class LuaClientApplicationInfoInvoke -------------------------------

		private class LuaClientApplicationInfoInvoke
		{
			private readonly object func;

			public LuaClientApplicationInfoInvoke(object func)
				=> this.func = func ?? throw new ArgumentNullException(nameof(func));

			public IPpsClientApplicationInfo Invoke(PpsClientApplicationSource source)
				=> new LuaResult(Lua.RtInvoke(func, source))[0] as IPpsClientApplicationInfo;
		} // class LuaClientApplicationInfoInvoke

		#endregion

		#region -- class PpsClientApplicationFile -------------------------------------

		/// <summary>Client application information</summary>
		[DEListTypeProperty("appinfo")]
		private sealed class PpsClientApplicationFile : IPpsClientApplicationInfo
		{
			private readonly string type;
			private readonly List<PpsClientApplicationSource> sources = new List<PpsClientApplicationSource>();
			private int activeSourceIndex = -1;

			private IPpsClientApplicationInfo info;
			
			internal PpsClientApplicationFile(string type, PpsClientApplicationSource source, IPpsClientApplicationInfo info)
			{
				this.type = type ?? throw new ArgumentNullException(nameof(type));
				
				sources.Add(source ?? throw new ArgumentNullException(nameof(source)));
				activeSourceIndex = 0;

				this.info = info ?? source;
			} // ctor

			public bool IsSourceRefreshed()
			{
				for (var i = sources.Count - 1; i >= 0; i--)
				{
					var r = sources[i].Refresh();
					if (r.HasValue)
					{
						if (r.Value) // file changed, might be the main file
							return true;
					}
					else // file removed, check importance
					{
						if (i == activeSourceIndex)
							return true;
						else
						{
							sources.RemoveAt(i); // reparse AppInfo
							if (i < activeSourceIndex)
								activeSourceIndex--;
						}
					}
				}
				return false;
			} // func IsAppSourceRefreshed

			public bool Update(PpsClientApplicationSource source, string type, IPpsClientApplicationInfo info)
			{
				if (source == null)
					throw new ArgumentNullException(nameof(source));
				if (type == null)
					throw new ArgumentNullException(nameof(type));
				if (info == null)
					throw new ArgumentNullException(nameof(info));

				if (type != this.type)
					throw new ArgumentOutOfRangeException(nameof(type), type, $"Type '{type}' conflicts with alread setted type '{this.type}' of '{Name}'.");

				if (this.info.VersionCode < info.VersionCode)
				{
					activeSourceIndex = sources.Count;
					sources.Add(source);

					this.info = info;
					return true;
				}
				else
				{
					sources.Add(source);
					return false;
				}
			} // proc Update

			internal bool TryGetActiveSource(out PpsClientApplicationSource source)
			{
				if (HasActiveSource)
				{
					source = sources[activeSourceIndex];
					return true;
				}
				else
				{
					source = null;
					return false;
				}
			} // func TryGetActiveSource

			/// <summary>Type of the file</summary>
			[DEListTypeProperty("@type")]
			public string Type => type;
			/// <summary>Internal name of the client application.</summary>
			[DEListTypeProperty("@name")]
			public string Name => info.Name;
			/// <summary>Version</summary>
			[DEListTypeProperty("@versionCode")]
			public long VersionCode => info.VersionCode;
			/// <summary>Version</summary>
			[DEListTypeProperty("@version")]
			public Version Version => info.Version;

			/// <summary>Displayname for the client application.</summary>
			[DEListTypeProperty("@displayName")]
			public string DisplayName => info.DisplayName;
			/// <summary>Icon of the application</summary>
			[DEListTypeProperty("@icon")]
			public string Icon => info.Icon;

			/// <summary>Download source</summary>
			[DEListTypeProperty("@src")]
			public string Source => TryGetActiveSource(out var s) ? s.Uri.ToString() : null;
			/// <summary>Size of the file in bytes</summary>
			[DEListTypeProperty("@size")]
			public long? Length => TryGetActiveSource(out var s) ? s.Info.Length : (long?)null;
			/// <summary>Lase write time stamp</summary>
			[DEListTypeProperty("@stamp")]
			public DateTime? LastWriteTime => TryGetActiveSource(out var s) ? s.Info.LastWriteTimeUtc : (DateTime?)null;

			/// <summary>Is a source active.</summary>
			internal bool HasActiveSource => activeSourceIndex >= 0 && activeSourceIndex < sources.Count;

			internal IEnumerable<PpsClientApplicationSource> Sources => sources;
		} // class PpsClientApplicationInfo

		#endregion

		#region -- class PpsSeenClient ------------------------------------------------

		/// <summary>Currently known client.</summary>
		public sealed class PpsSeenClient
		{
			private readonly string clientId;

			private DateTime lastUpdate = DateTime.MinValue;
			private string version;
			private double lastLng = Double.NaN;
			private double lastLat = Double.NaN;
			private long lastGpsTimeStamp = 0;
			private string lastWifi = null;
			private string lastAddress = null;

			private bool sendLogFlag = false;
			private bool dumpAppStateFlag = false;
			private int alarmRepeat = 0;

			internal PpsSeenClient(string deviceId, IDEWebRequestScope r)
			{
				this.clientId = deviceId ?? throw new ArgumentNullException(nameof(deviceId));

				Update(r);
			} // ctor

			internal PpsSeenClient(string clientId, XElement x)
			{
				this.clientId = clientId ?? throw new ArgumentNullException(nameof(clientId));

				var lastUpdate = x.GetAttribute("last", 0L);
				this.lastUpdate = lastUpdate > 0 ? DateTime.FromFileTimeUtc(lastUpdate) : DateTime.MinValue;
				
				version = x.GetAttribute("v", null);

				lastLng = x.GetAttribute("lng", Double.NaN);
				lastLat = x.GetAttribute("lat", Double.NaN);
				lastGpsTimeStamp = x.GetAttribute("gpsts", 0L);

				lastWifi = x.GetAttribute("wifi", null);
				lastAddress = x.GetAttribute("addr", null);
			} // ctor

			/// <summary>Create a xml of the data.</summary>
			/// <returns></returns>
			public XElement ToXml()
			{
				return new XElement("client",
					new XAttribute("id", clientId),
					Procs.XAttributeCreate("v", version, null),
					Procs.XAttributeCreate("last", lastUpdate == DateTime.MinValue ? 0L : lastUpdate.ToFileTimeUtc(), 0L),
					Procs.XAttributeCreate("lng", lastLng, Double.NaN),
					Procs.XAttributeCreate("lat", lastLat, Double.NaN),
					Procs.XAttributeCreate("gpsts", lastGpsTimeStamp, 0L),

					Procs.XAttributeCreate("wifi", lastWifi, null),
					Procs.XAttributeCreate("addr", lastAddress, null)
				);
			} // func ToXml

			/// <summary>Update information from request</summary>
			/// <param name="r"></param>
			public void Update(IDEWebRequestScope r)
			{
				version = r.GetProperty("x-ppsn-version", version);
				lastLng = r.GetProperty("x-ppsn-lng", lastLng);
				lastLat = r.GetProperty("x-ppsn-lat", lastLat);
				lastGpsTimeStamp = r.GetProperty("x-ppsn-ltm", lastGpsTimeStamp);
				lastWifi = r.GetProperty("x-ppsn-wifi", lastWifi);
				lastAddress = r.RemoteEndPoint?.Address.ToString();

				lastUpdate = DateTime.Now;
			} // proc Update

			private bool SwitchFlag(ref bool flag)
			{
				if (flag)
				{
					flag = false;
					return true;
				}
				return false;
			} // func SwitchFlag

			/// <summary>Request a log from the client.</summary>
			/// <returns></returns>
			public bool SetSendLogFlag()
				=> sendLogFlag = true;

			/// <summary>Get flag, and reset the state.</summary>
			/// <returns></returns>
			public bool GetSendLogFlag()
				=> SwitchFlag(ref sendLogFlag);

			/// <summary>Request a application dump from the client.</summary>
			/// <returns></returns>
			public bool SetDumpAppStateFlag()
				=> sendLogFlag = true;

			/// <summary>Get flag, and reset the state.</summary>
			/// <returns></returns>
			public bool GetDumpAppStateFlag()
				=> SwitchFlag(ref dumpAppStateFlag);

			/// <summary>Identity the client.</summary>
			/// <param name="repeat"></param>
			public void SetAlarmRepeatFlag(int repeat)
				=> alarmRepeat = repeat;

			/// <summary>Get flag, and reset the state.</summary>
			/// <param name="value"></param>
			/// <returns></returns>
			public bool TryGetAlarmRepeatFlag(out int value)
			{
				if (alarmRepeat != 0)
				{
					value = alarmRepeat;
					alarmRepeat = 0;
					return true;
				}
				else
				{
					value = 0;
					return false;
				}
			} // func TryGetAlarmRepeatFlag

			/// <summary>Id of the device.</summary>
			[DEListTypeProperty("@id")]
			public string ClientId => clientId;
			/// <summary>Current version.</summary>
			[DEListTypeProperty("@version")]
			public string Version => version;

			/// <summary>Last time the information where updated.</summary>
			[DEListTypeProperty("@lastTimeSeen")]
			public DateTime LastTimeSeen => lastUpdate > DateTime.MinValue ? lastUpdate.ToLocalTime() : lastUpdate;
			/// <summary>Gps position of the device.</summary>
			[DEListTypeProperty("@lat")]
			public double Latitude => lastLat;
			/// <summary>Gps position of the device.</summary>
			[DEListTypeProperty("@lng")]
			public double Longtitude => lastLng;
			/// <summary>Last seen gps update.</summary>
			[DEListTypeProperty("@time")]
			public DateTime GpsTimeStamp => new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(lastGpsTimeStamp).ToLocalTime();

			/// <summary>Current wifi of the device.</summary>
			[DEListTypeProperty("@wifi")]
			public string Wifi => lastWifi;
			/// <summary>Last ip-address of the device.</summary>
			[DEListTypeProperty("@addr")]
			public string Address => lastAddress;

			/// <summary>Show pending request flags.</summary>
			[DEListTypeProperty("@pending")]
			public string Pending
			{
				get
				{
					return String.Join(",",
						new string[]
						{
							sendLogFlag ? "LogRequest" : null,
							dumpAppStateFlag ? "AppState" : null,
							alarmRepeat != 0 ? $"R({alarmRepeat})" : null
						}.Where(c => c != null)
					);
				}

			}
		} // class PpsSeenClient

		#endregion

		#region -- struct ClientOptionHook --------------------------------------------

		private struct ClientOptionHook
		{
			public Regex Regex;
			public PpsClientOptionHookDelegate Hook;
		} // struct PropertyHook

		private class LuaClientOptionHook
		{
			private readonly object func;

			public LuaClientOptionHook(object func)
				=> this.func = func ?? throw new ArgumentNullException(nameof(func));

			public IEnumerable<(string key, object value)> InvokeHook(IDEWebScope r, Match keyMatch, string value)
			{
				if (new LuaResult(Lua.RtInvoke(func, r, keyMatch, value))[0] is LuaTable t)
				{
					foreach (var kv in t.Members)
						yield return (kv.Key, kv.Value);
				}
			} // func InvokeHook
		} // class LuaClientOptionHook

		#endregion

		private readonly SimpleConfigItemProperty<string> initializationProgress;

		private Task initializationProcess = null;        // initialization process
		private bool isInitializedSuccessful = false;     // is the system initialized properly

		private readonly List<InitializationTask> initializationTasks = new List<InitializationTask>(); // Action that should be done in the initialization process

		private readonly DEDictionary<string, PpsClientApplicationFile> clientApplicationInfos;
		private readonly DEList<PpsClientApplicationType> clientApplicationTypes;
		private bool resolveExternals = true;

		private readonly SimpleConfigItemProperty<DateTime> lastAppChangeProperty;
		private readonly SimpleConfigItemProperty<DateTime> lastAppScanProperty;

		private readonly DEList<PpsSeenClient> seenClients;
		private readonly Action saveSeenClientsAction;
		private readonly List<ClientOptionHook> clientOptionHooks = new List<ClientOptionHook>();
		private long lastSeenClientsChange = DateTime.Now.ToFileTime();

		private PpsReportEngine reporting = null;
		private readonly PpsServerReportProvider reportProvider;

		private DateTime lastConfigurationTimeStamp = DateTime.MinValue;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary></summary>
		/// <param name="sp"></param>
		/// <param name="name"></param>
		public PpsApplication(IServiceProvider sp, string name)
			: base(sp, name)
		{
			initializationProgress = new SimpleConfigItemProperty<string>(this, "ppsn_init_progress", "Initialization", "Misc", "Show the current state of the initialization of the node.", null, "Pending");

			databaseLibrary = new PpsDatabaseLibrary(this);
			objectsLibrary = new PpsObjectsLibrary(this);
			reportProvider = new PpsServerReportProvider(this);
 
			// register shortcut for text
			LuaType.RegisterTypeAlias("text", typeof(PpsFormattedStringValue));
			LuaType.RegisterTypeAlias("blob", typeof(byte[]));
			LuaType.RegisterTypeAlias("geography", typeof(Microsoft.SqlServer.Types.SqlGeography));

			saveSeenClientsAction = new Action(SaveSeenClients);

			clientOptionHooks.AddRange(new ClientOptionHook[]
			{
				// special case for "secure" options, must be index zero
				new ClientOptionHook
				{
					Regex  = new Regex(@"^Secure\.(?<p>.+)", clientOptionHookRegexOptions),
					Hook = SecureClientOptionHook
				},
				// special case for uri's
				new ClientOptionHook
				{
					Regex = new Regex(@"^.+\.Uri$", clientOptionHookRegexOptions),
					Hook = UriClientOptionHook
				},
				// color hook
				new ClientOptionHook
				{
					Regex = new Regex(@"^PPSn.DefaultTheme$", clientOptionHookRegexOptions),
					Hook = ColorClientOptionHook
				}
			});

			lastAppChangeProperty = RegisterProperty("tw_ppsn_lastchange", "LastChange", propertyCategory, "Last time, application files where modified.", "G", DateTime.MinValue);
			lastAppScanProperty = RegisterProperty("tw_ppsn_lastscan", "LastScan", propertyCategory, "Last time, it was scanned for application files.", "G", DateTime.MinValue);

			PublishItem(seenClients = new DEList<PpsSeenClient>(this, "tw_ppsn_clients", "Last seen clients"));
			PublishItem(clientApplicationTypes = new DEList<PpsClientApplicationType>(this, "tw_ppsn_client_types", "Client types"));
			PublishItem(clientApplicationInfos =  DEDictionary<string, PpsClientApplicationFile>.CreateSortedList(this, "tw_ppsn_client_infos", "Client applications"));

			PublishItem(new DEConfigItemPublicAction(refreshAppsAction) { DisplayName = "Refresh application sources." });

			RegisterApplicationType("msi", TryGetMsiApplicationInfo);

			InitUser();
		} // ctor

		/// <summary>Add resource extension.</summary>
		/// <param name="config"></param>
		protected override void ValidateConfig(XElement config)
		{
			base.ValidateConfig(config);

			var xHttpInfo = config.Elements(DEConfigurationConstants.xnResources).FirstOrDefault(x => x.Attribute("name")?.Value == "httpInfo");
			if (xHttpInfo != null)
				return;

			xHttpInfo = new XElement(DEConfigurationConstants.xnResources,
				new XAttribute("name", "httpInfo"),
				new XAttribute("assembly", typeof(PpsApplication).Assembly.FullName),
				new XAttribute("namespace", "TecWare.PPSn.Server.Resources"),
				new XElement(DEConfigurationConstants.xnSecurityDef, SecuritySys)
			);
#if DEBUG
			xHttpInfo.Add(new XElement(DEConfigurationConstants.xnAlternativeRoot, @"C:\Projects\PPSnOS\ppsn\PPSnMod\Resources"));
#endif
			config.Add(xHttpInfo);
		} // proc ValidateConfig

		/// <summary></summary>
		/// <param name="config"></param>
		protected override void OnBeginReadConfiguration(IDEConfigLoading config)
		{
			base.OnBeginReadConfiguration(config);

			// shutdown the init
			UpdateInitializationState("Shutdown previous init");
			if (!(initializationProcess?.IsCompleted ?? true))
				initializationProcess.Wait();

			UpdateInitializationState("Read configuration");

			// parse the configuration
			BeginReadConfigurationData(config);
			BeginReadConfigurationUser(config);
			BeginReadConfigurationReport(config);
		} // proc OnBeginReadConfiguration

		/// <summary></summary>
		/// <param name="config"></param>
		protected override void OnEndReadConfiguration(IDEConfigLoading config)
		{
			base.OnEndReadConfiguration(config);

			// set the configuration
			BeginEndConfigurationData(config);
			BeginEndConfigurationUser(config);

			lastConfigurationTimeStamp = config.LastWrite;

			databaseLibrary.ClearCache();
			
			// restart main thread
			initializationProcess = Task.Run(new Action(InitializeApplication));

			// start application info
			ReadSeenClients();

			// wait for configuration
			Server.Queue.RegisterCommand(() => StartRefreshApplications(false), 4000);
		} // proc OnEndReadConfiguration

		/// <summary></summary>
		/// <param name="disposing"></param>
		protected override void Dispose(bool disposing)
		{
			try
			{
				UpdateInitializationState("Shuting down");

				DoneUser();

				lastAppChangeProperty.Dispose();
				lastAppScanProperty.Dispose();

				initializationProgress.Dispose();
				clientApplicationInfos.Dispose();
				clientApplicationTypes.Dispose();
				seenClients.Dispose();
			}
			finally
			{
				base.Dispose(disposing);
			}
		} // proc Dispose

		/// <inherited />
		protected override bool IsMemberTableMethod(string key)
			=> key == ExtendLoginMethods || base.IsMemberTableMethod(key);

		#endregion

		#region -- Init ---------------------------------------------------------------

		private void UpdateInitializationState(string state)
		{
			if (String.IsNullOrEmpty(state))
				state = "Initialization";

			initializationProgress.Value = state;
		} // proc UpdateInitializationState

		private void UpdateInitializationState(LogMessageScopeProxy scope, int order, string state)
		{
			UpdateInitializationState(state);

			scope.WriteStopWatch()
				.WriteLine("{0:N0}: {1}", order, state);
		} // proc UpdateInitializationState

		private void InitializeApplication()
		{
			using (var msg = Log.CreateScope(LogMsgType.Information, stopTime: true))
			{
				try
				{
					msg.WriteLine("Initialize system");

					UpdateInitializationState("Initialize databases");

					// get the init tasks
					initializationTasks.Sort();

					var i = 0;
					while (i < initializationTasks.Count)
					{
						// combine same order
						var startAt = i;
						var order = initializationTasks[i].Order;
						while (i < initializationTasks.Count && initializationTasks[i].Order == order)
							i++;

						UpdateInitializationState(
							msg,
							initializationTasks[startAt].Order,
							initializationTasks[startAt].Status
						);

						// execute the action
						var count = i - startAt;
						if (count == 1)
						{
							initializationTasks[startAt].Task().Wait();
						}
						else
						{
							// start all tasks as batch parallel
							var currentTasks = new Task[100];
							var filled = 0;
							for (var j = startAt; j < i; j++)
							{
								var t = initializationTasks[j].Task();
								if (t.IsCompleted)
									t.Wait();
								else
								{
									currentTasks[filled++] = t;
									if (filled == currentTasks.Length)
									{
										Task.WaitAll(currentTasks);
										filled = 0;
									}
								}
							}

							// run last batch
							if (filled > 0)
							{
								while (filled < currentTasks.Length)
									currentTasks[filled++] = Task.CompletedTask;
								Task.WaitAll(currentTasks);
							}
						}
					}

					isInitializedSuccessful = true;
					UpdateInitializationState("Successful");
					Server.LogMsg(EventLogEntryType.Information, "Configuration initialized.");
				}
				catch (Exception e)
				{
					isInitializedSuccessful = false;
					UpdateInitializationState("Failed");
					msg.NewLine()
						.WriteException(e);

					Server.LogMsg(EventLogEntryType.Error, "Configuration not initialized. See log for details.");
				}
			}
		} // proc InitializeApplication

		/// <summary>Wait for initialization of the system, initialization is processed synchron.</summary>
		/// <param name="timeout"></param>
		/// <returns></returns>
		public bool? WaitForInitializationProcess(int timeout = -1)
			=> initializationProcess.Wait(timeout) ? new bool?(isInitializedSuccessful) : null;

		/// <summary>Extent the initialization process.</summary>
		/// <param name="order"></param>
		/// <param name="status"></param>
		/// <param name="task"></param>
		[LuaMember(nameof(RegisterInitializationAction))]
		public void RegisterInitializationAction(int order, string status, Action task)
			=> RegisterInitializationTask(order, status, () => Task.Run(task));

		/// <summary>Extent the initialization process.</summary>
		/// <param name="order"></param>
		/// <param name="status"></param>
		/// <param name="task"></param>
		public void RegisterInitializationTask(int order, string status, Func<Task> task)
		{
			if (status == null)
				status = String.Empty;

			lock (initializationTasks)
			{
				var initTask = new InitializationTask() { Order = order, Status = status, Task = task };

				var index = initializationTasks.BinarySearch(initTask);
				if (index < 0)
					initializationTasks.Insert(~index, initTask);
				else
				{
					while (index < initializationTasks.Count && initializationTasks[index].Order == initTask.Order)
						index++;
					initializationTasks.Insert(index, initTask);
				}
			}
		} // proc RegisterInitializationTask

		/// <summary>Is the ppsn configuration initialized.</summary>
		public bool IsInitializedSuccessful => isInitializedSuccessful;

		#endregion

		#region -- Reporting ----------------------------------------------------------

		#region -- class PpsServerReportProvider --------------------------------------

		/// <summary></summary>
		private sealed class PpsServerReportProvider : PpsDataServerProviderBase
		{
			private readonly PpsApplication application;

			public PpsServerReportProvider(PpsApplication application)
			{
				this.application = application ?? throw new ArgumentNullException(nameof(application));
			} // ctor

			public async override Task<IEnumerable<IDataRow>> GetListAsync(string select, PpsDataColumnExpression[] columns, PpsDataFilterExpression filter, PpsDataOrderExpression[] order)
				=> await application.Database.CreateSelectorAsync(select, columns, filter, order);

			public override async Task<PpsDataSet> GetDataSetAsync(object identitfication, string[] tableFilter)
			{
				PpsObjectAccess GetObjectCore()
				{
					switch (identitfication)
					{
						case int i:
							return application.Objects.GetObject(i);
						case long l:
							return application.Objects.GetObject(l);
						case Guid g:
							return application.Objects.GetObject(g);
						case LuaTable t:
							var o = application.Objects.GetObject(t);
							if (t.TryGetValue<long>("RevId", out var revId))
								o.SetRevision(revId);
							return o;
						default:
							return application.Objects.GetObject(identitfication.ChangeType<long>());
					}
				} // func GetObjectCore

				// initialize object
				var obj = await Task.Run(() => GetObjectCore());
				var item = application.Objects.GetObjectItem(obj.Typ);
				return (PpsDataSet)await Task.Run(() => item.PullData(obj));
			} // func GetDataSetAsync

			public override Task<LuaTable> ExecuteAsync(LuaTable arguments)
			{
				var functionName = arguments.GetOptionalValue("name", (string)null);
				if (functionName == null)
					throw new ArgumentNullException("name", "No function definied.");

				return Task.FromResult(new LuaResult(application.CallMember(functionName, arguments))[0] as LuaTable);
			} // func ExecuteAsync
		} // class PpsServerReportProvider

		#endregion

		#region -- class PpsReportSession ---------------------------------------------

		/// <summary></summary>
		private sealed class PpsReportSession : PpsReportSessionBase
		{
			#region -- class ImageConverter -------------------------------------------

			private sealed class ImageConverter : PpsReportValueEmitter
			{
				private readonly PpsReportSession session;

				public ImageConverter(PpsReportSession session, string columnName, IDataColumns columns)
					: base(columnName, columns)
				{
					this.session = session ?? throw new ArgumentNullException(nameof(session));
				} // ctor

				private async Task WriteAsync(XmlWriter xml, FileInfo fi)
				{
					await xml.WriteStartElementAsync(null, "ref", null);
					await xml.WriteAttributeStringAsync(null, "src", null, fi.FullName);
					await xml.WriteEndElementAsync();
				} // proc WriteAsync

				public override async Task WriteAsync(XmlWriter xml, IDataRow row)
				{
					var objkIdKey = GetValue(row);
					if (objkIdKey == null)
						return;

					var objkId = objkIdKey.ChangeType<long>();

					// check cache
					if (session.exportedImages.TryGetValue(objkId, out var fi) && fi != null)
					{
						await WriteAsync(xml, fi);
						return;
					}

					// mark as exported
					session.exportedImages[objkId] = null;

					// test if the object is an image
					var obj = session.application.Objects.GetObject(objkId);
					if (obj.MimeType != MimeTypes.Image.Jpeg
						&& obj.MimeType != MimeTypes.Image.Png
						&& obj.MimeType != MimeTypes.Application.Pdf)
						return;

					// write object to disc
					// todo: optimize to use direct link
					fi = session.CreateTempFile(objkId.ToString() + MimeTypeMapping.GetExtensionFromMimeType(obj.MimeType), true);

					using (var dst = fi.OpenWrite())
					using (var src = obj.GetDataStream())
						await src.CopyToAsync(dst);

					await WriteAsync(xml, fi);

					session.exportedImages[objkId] = fi;
				} // proc WriteAsnyc
			} // class ImageConverter

			#endregion

			private readonly PpsApplication application;

			private readonly Dictionary<long, FileInfo> exportedImages = new Dictionary<long, FileInfo>();

			public PpsReportSession(PpsApplication application)
			{
				this.application = application ?? throw new ArgumentNullException(nameof(application));
			} // ctor

			public override IPpsReportValueEmitter CreateColumnEmitter(string argument, string columnName, IDataColumns columns)
			{
				if (argument == "image" && columnName != null)
					return new ImageConverter(this, columnName, columns);
				else
					return base.CreateColumnEmitter(argument, columnName, columns);
			} // func CreateColumnEmitter
		} // class PpsReportSession

		#endregion

		private void BeginReadConfigurationReport(IDEConfigLoading config)
		{
			var currentNode = XConfigNode.Create(Server.Configuration, config.ConfigNew).Element(PpsStuff.xnReports);

			var systemPath = currentNode.GetAttribute<string>("system") ?? throw new DEConfigurationException(currentNode.Data, "@system is empty.");
			var basePath = currentNode.GetAttribute<string>("base") ?? throw new DEConfigurationException(currentNode.Data, "@base is empty.");
			var logPath = currentNode.GetAttribute<string>("logs");
			var workPath = currentNode.GetAttribute<string>("work");

			// check for recreate the reporting engine
			if (reporting == null
				|| !ProcsDE.IsPathEqual(reporting.EnginePath, systemPath)
				|| !ProcsDE.IsPathEqual(reporting.BasePath, basePath)
				|| (logPath != null && !ProcsDE.IsPathEqual(reporting.LogPath, logPath))
				|| (workPath != null && !ProcsDE.IsPathEqual(reporting.WorkingPath, workPath)))
			{
				reporting = new PpsReportEngine(systemPath, basePath, reportProvider, CreateReportSession, reportWorkingPath: workPath, reportLogPath: logPath);
			}

			// update values
			reporting.CleanBaseDirectoryAfter = currentNode.GetAttribute<int>("cleanBaseDirectory");
			reporting.ZipLogFiles = currentNode.GetAttribute<bool>("zipLogFiles");
			reporting.StoreSuccessLogs = currentNode.GetAttribute<bool>("storeSuccessLogs");
		} // proc BeginReadConfigurationReport

		private PpsReportSessionBase CreateReportSession()
			=> new PpsReportSession(this);

		/// <summary>Run a report.</summary>
		/// <param name="table"></param>
		/// <returns></returns>
		[LuaMember]
		public string RunReport(LuaTable table)
			=> RunReportAsync(table).AwaitTask();

		/// <summary>Run a report data.</summary>
		/// <param name="table"></param>
		/// <returns></returns>
		[LuaMember]
		public string DebugData(LuaTable table)
			=> RunDataAsync(table).AwaitTask();

		/// <summary>Run a report, with a static name.</summary>
		/// <param name="table"></param>
		[LuaMember]
		public void DebugReport(LuaTable table)
		{
			table["DebugOutput"] = new Action<string>(text => Log.LogMsg(LogMsgType.Debug, text));
			var resultFile = RunReportAsync(table).AwaitTask();

			// move to a unique name
			var moveFileTo = table.GetMemberValue("name") as string;
			var p = moveFileTo.LastIndexOfAny(new char[] { '/', '\\' });
			moveFileTo = Path.Combine(Path.GetDirectoryName(resultFile), moveFileTo.Substring(p + 1)) + ".pdf";
			if (File.Exists(moveFileTo))
				File.Delete(moveFileTo);
			File.Move(resultFile, moveFileTo);
		} // proc DebugReport

		private static SimpleDataRow GetRowContent(LuaTable table)
		{
			var columns = new List<SimpleDataColumn>();
			var values = new List<object>();
			foreach (var c in table.Members)
			{
				columns.Add(new SimpleDataColumn(c.Key, c.Value.GetType()));
				values.Add(c.Value);
			}
			return new SimpleDataRow(values.ToArray(), columns.ToArray());
		} // func GetRowContent

		/// <summary></summary>
		/// <param name="table"></param>
		/// <returns></returns>
		/// <remarks>return DebugEmitter { converter = "markdown", columnName = "test", row = { test = [[ Hallo **Welt**!]] } }</remarks>
		/// <remarks>return DebugEmitter { converter = "image", columnName = "test", row = { test = 57006 } }</remarks>
		[LuaMember]
		public string DebugEmitter(LuaTable table)
		{
			var arguments = table.GetOptionalValue("converter", (string)null);
			var columnName = table.GetOptionalValue("columnName", (string)null);
			// create row
			var row = GetRowContent((table.GetMemberValue("row") as LuaTable) ?? throw new ArgumentNullException("row", "Row information is missing."));

			using (var session = reporting.CreateDebugReportSession())
			using (var sw = new StringWriter())
			using (var xml = XmlWriter.Create(sw, new XmlWriterSettings() { NewLineHandling = NewLineHandling.Entitize, IndentChars = "  ", Async = true }))
			{
				// emit column
				var emitter = session.CreateColumnEmitter(arguments, columnName, row);
				xml.WriteStartElement(emitter.ElementName);
				emitter.WriteAsync(xml, row).AwaitTask();
				xml.WriteEndElement();
				xml.Flush();

				return sw.GetStringBuilder().ToString();
			}
		} // func DebugEmitter

		/// <summary>Run a report.</summary>
		/// <param name="table"></param>
		/// <returns></returns>
		[LuaMember]
		public Task<string> RunReportAsync(LuaTable table)
			=> reporting.RunReportAsync(table);

		/// <summary>Run a report data only.</summary>
		/// <param name="table"></param>
		/// <returns></returns>
		[LuaMember]
		public Task<string> RunDataAsync(LuaTable table)
			=> reporting.RunDataAsync(table);

		[DEConfigHttpAction("report")]
		private void HttpRunReport(IDEWebRequestScope r)
		{
			var reportName = r.GetProperty("name", null);
			if (reportName == null)
				throw new ArgumentNullException("name", "Report name is missing.");
			// enforce user context
			var user = r.GetUser<IPpsPrivateDataContext>();

			try
			{
				// collection arguments
				var properties = new List<KeyValuePair<string, object>>();
				foreach (var p in r.ParameterNames)
				{
					var v = r.GetProperty(p, (object)null);
					if (v != null)
						properties.Add(new KeyValuePair<string, object>(p, v));
				}

				// execute report
				var resultInfo = reporting.RunReportAsync(reportName, properties.ToArray()).AwaitTask();
				r.OutputHeaders["x-ppsn-reportname"] = reportName;
				r.WriteFile(resultInfo, MimeTypes.Application.Pdf);
			}
			catch (Exception e)
			{
				Log.Except(e);
				throw;
			}
		} // proc HttpRunReport

		/// <summary>Access to the report engine.</summary>
		[LuaMember]
		public PpsReportEngine Reports => reporting;

		#endregion

		#region -- msi Application Type -----------------------------------------------

		private static IPpsClientApplicationInfo GetClientMsiApplication(FileInfo fi)
		{
			const string productNameProperty = "ProductName";
			const string productVersionProperty = "ProductVersion";

			var key = Path.GetFileNameWithoutExtension(fi.Name); // id of the client application
			var productName = key;
			var productVersion = new Version(0, 0, 0, 0);

			// remove version add on
			var p = key.LastIndexOf('.');
			if (p > 0 && p < key.Length - 2 && Char.ToUpper(key[p + 1]) == 'V' && Int32.TryParse(key.Substring(p + 2), out _))
				key = key.Substring(0, p);

			// get extented attribtutes
			using (var msi = new Database(fi.FullName, DatabaseOpenMode.ReadOnly))
			{
				using (var view = msi.OpenView("SELECT `Property`, `Value` FROM `Property` " +
					"WHERE `Property` = '" + productNameProperty + "' " +
						"OR `Property` = '" + productVersionProperty + "' ")
				)
				{
					view.Execute();
					foreach (var c in view)
					{
						using (c)
						{
							switch (c.GetString(1))
							{
								case productNameProperty:
									productName = c.GetString(2);
									break;
								case productVersionProperty:
									productVersion = new Version(c.GetString(2));
									break;
							}
						}
					}
				}
			}

			return new PpsClientApplicationInfo(key, -1, productVersion, productName, null);
		} // func GetClientMsiApplication

		private static IPpsClientApplicationInfo TryGetMsiApplicationInfo(PpsClientApplicationSource source)
		{
			var fi = source.Info;

			return String.Compare(fi.Extension, ".msi", StringComparison.OrdinalIgnoreCase) == 0
				? GetClientMsiApplication(fi)
				: null;
		} // func TryGetMsiApplicationInfo

		#endregion

		#region -- Client Application Files -------------------------------------------

		private long GetServerTick()
		{
			var dt = lastAppChangeProperty.Value;
			if (dt == DateTime.MinValue)
				return 1;
			else
				return dt.ToFileTime();
		} // func GetServerTick

		/// <summary>Register a new client application type.</summary>
		/// <param name="type"></param>
		/// <param name="func"></param>
		[LuaMember(nameof(RegisterApplicationType))]
		public void LuaRegisterApplicationType(string type, object func)
			=> RegisterApplicationType(type, new LuaClientApplicationInfoInvoke(func).Invoke);

		/// <summary>Register a new client application type.</summary>
		/// <param name="type"></param>
		/// <param name="tryGet"></param>
		public void RegisterApplicationType(string type, PpsClientApplicationInfoDelegate tryGet)
		{
			using (clientApplicationTypes.EnterWriteLock())
			{
				var isUpdated = false;
				for (var i = 0; i < clientApplicationTypes.Count; i++)
				{
					if (clientApplicationTypes[i].Type == type)
					{
						clientApplicationTypes[i] = new PpsClientApplicationType(type, tryGet);
						isUpdated = true;
					}
				}
				if (!isUpdated)
					clientApplicationTypes.Add(new PpsClientApplicationType(type, tryGet));
			}

			StartRefreshApplications(true);
		} // proc RegisterApplicationType

		private void GetApplicationType(PpsClientApplicationSource source, out string type, out IPpsClientApplicationInfo info)
		{
			using (clientApplicationTypes.EnterReadLock())
			{
				for (var i = clientApplicationTypes.Count - 1; i >= 0; i--)
				{
					var typeDef = clientApplicationTypes[i];
					if (typeDef.TryGet(source, out info))
					{
						type = typeDef.Type;
						return;
					}
				}

				type = "file";
				info = new PpsClientApplicationInfo(source.Uri.ToString());
				return;
			}
		} // func TryGetApplicationType

		private static bool IsPathSeperator(string path, int seperator)
			=> path[seperator] == Path.DirectorySeparatorChar || path[seperator] == Path.AltDirectorySeparatorChar;

		private static Uri CreateRelativePath(string baseUri, string baseDirectory, FileInfo fileInfo, string alternativeName = null)
		{

			var baseDirectoryLength = IsPathSeperator(baseDirectory, baseDirectory.Length - 1) ? baseDirectory.Length - 1: baseDirectory.Length;
			var fullPath = Path.Combine(fileInfo.DirectoryName, alternativeName ?? fileInfo.Name);

			if (!IsPathSeperator(fullPath, baseDirectoryLength))
				throw new ArgumentException("Invalid arguments.");

			return new Uri(baseUri + fullPath.Substring(baseDirectoryLength + 1).Replace(Path.DirectorySeparatorChar, '/'), UriKind.Relative);
		} // func CreateRelativePath

		private void StartRefreshApplications(bool rebuildFileType)
		{
			Task.Run(() =>
			{
				try
				{
					RefreshApplicationInfos(true, rebuildFileType);
				}
				catch (Exception e)
				{
					Log.Except(e);
				}
			});
		} // proc StartRefreshApplications

		private void AddApplicationInfoFromSource(LogMessageScopeProxy log, PpsClientApplicationSource source)
		{
			GetApplicationType(source, out var type, out var info);
			string logLine;
			var key = type + ":" + info.Name;
			if (clientApplicationInfos.TryGetValue(key, out var file))
			{
				if (file.Update(source, type, info))
					logLine = "{0}: updated with version 0x{1:X16} ({2}) / {3}";
				else
					logLine = "{0}: not updated with version 0x{1:X16} ({2}) / {3}";
			}
			else
			{
				clientApplicationInfos.Add(key, file = new PpsClientApplicationFile(type, source, info));
				logLine = "{0}: create with version 0x{1:X16} ({2}) - {3}";
			}
			log.WriteLine(logLine, key, file.VersionCode, file.Version, source.Uri);
		} // proc AddApplicationInfoFromSource

		private static IEnumerable<FileInfo> ParseExternalLinks(FileInfo externalFileInfo)
		{
			using (var tr = externalFileInfo.OpenText())
			{
				string line = null;
				while ((line = tr.ReadLine()) != null)
				{
					if (String.IsNullOrEmpty(line) || line[0] == ';')
						continue;

					if (Uri.TryCreate(line, UriKind.Absolute, out var uri))
					{
						var query = HttpUtility.ParseQueryString(uri.Query);
						var resolve = query.Get("resolve");

						if (!String.IsNullOrEmpty(resolve))
						{
							if (resolve.IndexOfAny(new char[] { '?', '*' }) >= 0)
							{
								var baseDirectory = new DirectoryInfo(Path.GetDirectoryName(resolve));
								if (baseDirectory.Exists)
								{
									foreach (var fi in baseDirectory.EnumerateFiles(Path.GetFileName(resolve)))
										yield return fi;
								}
								else
									yield return new FileInfo(resolve);
							}
							else
								yield return new FileInfo(resolve);
						}
						else if (uri.IsFile)
							yield return new FileInfo(uri.AbsolutePath);
					}
				}
			}
		} // func ParseExternalLinks

		private void RefreshApplicationInfos(bool force, bool rebuildFileType)
		{
			if (force || (DateTime.Now - lastAppScanProperty.Value).TotalMinutes > 5)
			{
				var isChanged = false;

				var toRemove = new List<string>();
				var knownFiles = new List<FileInfo>();

				using (var log = Log.CreateScope(LogMsgType.Information, true, true))
				using (clientApplicationInfos.EnterWriteLock())
				{
					log.WriteLine("Scan for application files...");

					// validate known file infos
					foreach (var file in clientApplicationInfos)
					{
						var name = file.Key;

						if (rebuildFileType && file.Value.Type == "file" || file.Value.IsSourceRefreshed())
						{
							isChanged = true;
							toRemove.Add(name);
							log.WriteLine("{0}: removed, because was changed.", name);
						}
						else
							knownFiles.AddRange(file.Value.Sources.Select(c => c.Info));
					}

					// remove
					foreach (var fileKey in toRemove)
						clientApplicationInfos.Remove(fileKey);

					// find new items
					var fileWorkerArray = CollectChildren<HttpFileWorker>(p => p.VirtualRoot.StartsWith("app/"));
					if (fileWorkerArray.Length == 0)
						log.WriteLine("No application Source defined (HttpFileWorker 'app' is missing).");
					else
					{
						foreach (var fileWorker in fileWorkerArray)
						{
							var relativeUriPath = fileWorker.VirtualRoot;
							var path = fileWorker.DirectoryBase;

							log.WriteLine("Scan: {0} / {1}", fileWorker.Name, path.FullName);

							foreach (var fi in path.EnumerateFiles("*", SearchOption.AllDirectories))
							{
								if (knownFiles.Exists(c => String.Compare(fi.Name, c.Name, StringComparison.OrdinalIgnoreCase) == 0))
									continue;

								// .extern files, is done only for debug.
								if (String.Compare(fi.Extension, ".extern", StringComparison.OrdinalIgnoreCase) == 0)
								{
									if (resolveExternals)
									{
										foreach (var fiLink in ParseExternalLinks(fi))
										{
											if (fiLink.Exists)
											{
												AddApplicationInfoFromSource(log, new PpsClientApplicationSource(
													fiLink,
													CreateRelativePath(relativeUriPath, path.FullName, fi, fiLink.Name),
													mimeType: fileWorker.GetFileContentType(fiLink.Name)
												));
											}
											else
												log.SetType(LogMsgType.Warning).WriteLine("{0}: could not found.", fiLink.FullName);
										}
									}
								}
								else
									AddApplicationInfoFromSource(log, new PpsClientApplicationSource(fi, CreateRelativePath(relativeUriPath, path.FullName, fi)));
							} // foreach files
						} // foreach fileWorker
					}
				}

				// update properties
				if (isChanged)
					lastAppChangeProperty.Value = DateTime.Now;
				lastAppScanProperty.Value = DateTime.Now;
			}
		} // proc RefreshApplicationInfos

		/// <summary>Get the client application information.</summary>
		/// <param name="type"></param>
		/// <param name="name"></param>
		/// <returns></returns>
		[LuaMember]
		public IPpsClientApplicationInfo GetClientApplicationInfo(string type, string name)
			=> clientApplicationInfos.TryGetValue(type + ":" + name, out var file) ? file : null;

		/// <summary>Reload application dictionary</summary>
		[LuaMember(nameof(RefreshApplicationInfos))]
		public void LuaRefreshApplicationInfos()
			=> RefreshApplicationInfos(true, false);

		[DEConfigHttpAction(refreshAppsAction, IsSafeCall = true, SecurityToken = SecuritySys)]
		private void HttpRefreshApplications()
			=> RefreshApplicationInfos(true, false);

		/// <summary>Create a selector for all client files.</summary>
		/// <param name="dataSource"></param>
		/// <returns></returns>
		public PpsDataSelector GetApplicationFilesSelector(PpsSysDataSource dataSource)
			=> new PpsGenericSelector<PpsClientApplicationFile>(dataSource.SystemConnection, "sys.clientFiles", 0, clientApplicationInfos.Select(c => c.Value));

		#endregion

		#region -- Seen Client Client -------------------------------------------------

		private FileInfo GetSeenClientHistoryFileInfo()
			=> new FileInfo(Path.ChangeExtension(LogFileName, ".clients.xml"));

		private void ReadSeenClients()
		{
			using (seenClients.EnterWriteLock())
			{
				try
				{
					var fi = GetSeenClientHistoryFileInfo();
					if (fi.Exists)
					{
						var xDoc = XDocument.Load(fi.FullName);
						foreach (var x in xDoc.Root.Elements("dev"))
						{
							var devId = x.GetAttribute("id", null);
							if (String.IsNullOrEmpty(devId))
								continue;

							var idx = FindSeenClientIndex(devId);
							if (idx == -1)
								seenClients.Add(new PpsSeenClient(devId, x));
						}
					}
				}
				catch (Exception e)
				{
					Log.Except(e);
				}
			}
		} // proc ReadSeenClients

		private void SaveSeenClients()
		{
			using (seenClients.EnterReadLock())
			{
				try
				{
					new XDocument(
						new XElement("clients",
							seenClients.Select(d => d.ToXml())
						)
					).Save(GetSeenClientHistoryFileInfo().FullName);
				}
				catch (Exception e)
				{
					Log.Except(e);
				}
			}
		} // proc SaveSeenClients

		private void EnqueueSaveSeenClients()
		{
			lastSeenClientsChange = DateTime.Now.ToFileTime();

			var queue = Server.Queue;
			if (queue.IsQueueRunning)
			{
				queue.CancelCommand(saveSeenClientsAction);
				queue.RegisterCommand(saveSeenClientsAction, 10000);
			}
			else
				SaveSeenClients();
		} // proc EnqueueSaveSeenClients

		/// <summary>Remove a device from the list.</summary>
		/// <param name="clientId"></param>
		[LuaMember]
		public void RemoveSeenClient(string clientId)
		{
			using (seenClients.EnterWriteLock())
			{
				var idx = FindSeenClientIndex(clientId);
				if (idx >= 0)
				{
					seenClients.RemoveAt(idx);
					EnqueueSaveSeenClients();
				}
			}
		} // proc RemoveSeenClient

		/// <summary>Return last seen clients</summary>
		/// <param name="dataSource"></param>
		/// <returns></returns>
		public PpsDataSelector CreateSeenClientsSelector(PpsSysDataSource dataSource)
			=> new PpsGenericSelector<PpsSeenClient>(dataSource.SystemConnection, "sys.clients", lastSeenClientsChange, seenClients);

		#endregion

		#region -- Client Option Hooks ------------------------------------------------

		private IEnumerable<(string key, object value)> SecureClientOptionHook(IDEWebScope _, Match keyMatch, string value)
		{
			var key = keyMatch.Groups["p"].Value;
			if (!String.IsNullOrEmpty(key))
				yield return (key, value);
		} // func SecureClientOptionHook

		private IEnumerable<(string key, object value)> UriClientOptionHook(IDEWebScope r, Match keyMatch, string value)
		{
			yield return (
				keyMatch.Value,
				Uri.TryCreate(value, UriKind.Relative, out var uri) ? (r == null ? uri : r.GetOrigin(uri)).ToString() : value
			);
		} // func UriClientOptionHook

		private IEnumerable<(string key, object value)> ColorClientOptionHook(IDEWebScope r, Match keyMatch, string value)
			=> GetColors(value).Select(c => new ValueTuple<string, object>(keyMatch.Value + "." + c.Name, ColorTranslator.ToHtml(c.Color)));
				
		private (int idx, Match) FindClientOptionHookForKey(string key, int startAt)
		{
			for (var i = startAt; i >= 0; i--)
			{
				var m = clientOptionHooks[i].Regex.Match(key);
				if (m.Success)
					return (i, m);
			}
			return (-1, null);
		} // func FindClientOptionHookForKey

		/// <summary>Add a client option hook</summary>
		/// <param name="regex"></param>
		/// <param name="func"></param>
		[LuaMember(nameof(RegisterClientOptionHook))]
		public void LuaRegisterClientOptionHook(string regex, object func)
		{
			clientOptionHooks.Add(new ClientOptionHook
			{
				Regex = new Regex(regex, clientOptionHookRegexOptions),
				Hook = new LuaClientOptionHook(func).InvokeHook
			});
		} // proc LuaRegisterClientOptionHook

		/// <summary>Add a client option hook</summary>
		/// <param name="regex"></param>
		/// <param name="hook"></param>
		public void RegisterClientOptionHook(Regex regex, PpsClientOptionHookDelegate hook)
		{
			clientOptionHooks.Add(new ClientOptionHook
			{
				Regex = regex ?? throw new ArgumentNullException(nameof(regex)),
				Hook = hook ?? throw new ArgumentNullException(nameof(hook))
			});
		} // proc RegisterClientOptionHook

		#endregion

		#region -- Client Options -----------------------------------------------------

		private int FindSeenClientIndex(string clientId)
			=> seenClients.FindIndex(c => String.Compare(c.ClientId, clientId, StringComparison.OrdinalIgnoreCase) == 0);

		private PpsSeenClient GetSeenClient(IDEWebRequestScope r, bool throwException)
		{
			var clientId = r.GetProperty("id", null);

			// device id is needed
			if (String.IsNullOrEmpty(clientId))
			{
				if (throwException)
					throw new HttpResponseException(HttpStatusCode.BadRequest, "Parameter missing.", new ArgumentNullException(nameof(clientId)));
				return null;
			}

			var clientIdx = FindSeenClientIndex(clientId);
			if (clientIdx == -1)
			{
				using (seenClients.EnterWriteLock())
				{
					clientIdx = seenClients.Count;
					seenClients.Add(new PpsSeenClient(clientId, r));
				}
			}
			else
				seenClients[clientIdx].Update(r);
			EnqueueSaveSeenClients();

			return seenClients[clientIdx];
		} // func GetLastSeenClient

		private XElement GetClientOptionsByDevId(string clientId)
		{
			var xRet = (XElement)null;
			var matchingLevel = 0;
			foreach (var x in Config.Elements(PpsStuff.xnClientOptions))
			{
				var curId = x.GetAttribute("clientId", null);
				if (curId == null)
				{
				}
				else if (curId.Contains('*'))
				{
					if (curId.Length >= matchingLevel && Procs.IsFilterEqual(clientId, curId))
					{
						matchingLevel = curId.Length;
						xRet = x;
					}
				}
				else if (String.Compare(curId, clientId, StringComparison.OrdinalIgnoreCase) == 0)
				{
					matchingLevel = Int32.MaxValue;
					xRet = x;
					break;
				}
			}

			return xRet;
		} // func GetClientOptionsByDevId

		private XElement GetClientOptionsById(string strictId)
		{
			if (String.IsNullOrEmpty(strictId))
				return null;

			foreach (var x in Config.Elements(PpsStuff.xnClientOptions))
			{
				var curId = x.GetAttribute("id", null);
				if (curId != null && String.Compare(curId, strictId, StringComparison.OrdinalIgnoreCase) == 0)
					return x;
			}
			return null;
		} // func GetClientOptionsById

		private void ParseClientOptionPath(LuaTable options, string clientOptionKey, out LuaTable table, out object localKey)
		{
			object GetCurrentKey(string part)
			{
				if (String.IsNullOrEmpty(part))
					throw new ArgumentException("Invalid client option key.", clientOptionKey);

				return Int32.TryParse(part, out var idx) ? (object)idx : part; // array, member
			} // func GetCurrentKey

			var cur = options;

			var path = clientOptionKey.Split('.');
			if (path.Length == 0)
				throw new ArgumentNullException(clientOptionKey, "Empty key is not allowed.");

			for (var i = 0; i < path.Length - 1; i++)
			{
				var key = GetCurrentKey(path[i]);
				if (!(cur.GetValue(key, rawGet: true) is LuaTable t))
				{
					t = new LuaTable();
					cur[key] = t;
				}
				cur = t;
			}

			localKey = GetCurrentKey(path[path.Length - 1]);
			table = cur;
		} // func ParseClientOptionPath

		private void SetClientOptionValueCore(LuaTable options, string clientOptionKey, object value, bool overwrite)
		{
			ParseClientOptionPath(options, clientOptionKey, out var table, out var localKey);

			// is this property already set
			if (overwrite || table.GetValue(localKey, rawGet: true) == null)
				table.SetValue(localKey, value, rawSet: true);
		} // proc SetClientOptionValueCore

		private void SetClientOptionValueWithHook(IDEWebScope r, LuaTable options, Stack<string> clientOptionKeyStack, string clientOptionKey, object value, bool emitSecureOptions, bool overwrite)
		{
			// check for recursion
			if (clientOptionKeyStack.Contains(clientOptionKey, StringComparer.OrdinalIgnoreCase))
			{
				var sb = new StringBuilder("Client option recursion detected:");
				foreach (var c in clientOptionKeyStack)
					sb.Append(c).Append(" > ");
				sb.Append(clientOptionKeyStack);

				throw new ArgumentException(sb.ToString());
			}

			clientOptionKeyStack.Push(clientOptionKey);
			try
			{
				var offset = clientOptionHooks.Count - 1;
				while (offset >= 0)
				{
					var (hookIndex, hookMatch) = FindClientOptionHookForKey(clientOptionKey, offset);
					if (hookIndex == 0 && !emitSecureOptions) // do not emit secure options
					{
						ParseClientOptionPath(options, clientOptionKey, out var table, out var localKey);
						table.SetValue(localKey, null, rawSet: true); // clear value
					}
					else if (hookIndex >= 0) // other hook is attached
					{
						foreach (var kv in clientOptionHooks[hookIndex].Hook(r, hookMatch, value.ChangeType<string>()))
						{
							if (String.Compare(kv.key, clientOptionKey, StringComparison.OrdinalIgnoreCase) == 0) // same key, just set value
							{
								SetClientOptionValueCore(options, kv.key, kv.value, overwrite);
								value = kv.value; // update value for next hook
							}
							else // set value 
								SetClientOptionValueWithHook(r, options, clientOptionKeyStack, kv.key, kv.value, emitSecureOptions, overwrite);
						}
					}
					else
						SetClientOptionValueCore(options, clientOptionKey, value, overwrite);

					// find next hook
					offset = hookIndex - 1;
				}
			}
			finally
			{
				clientOptionKeyStack.Pop();
			}
		} // proc SetClientOptionValueWithHook

		private void SetClientOptionValue(LuaTable options, string clientOptionKey, object value)
		{
			ParseClientOptionPath(options, clientOptionKey, out var table, out var localKey);
			table.SetValue(localKey, value, rawSet: true);
		} // proc SetClientOptionVlaue

		private LuaTable ParseClientOptions(IDEWebScope r, LuaTable options, bool emitSecureOptions, XElement xOptions)
		{
			if (xOptions == null)
				return options;

			if (options == null)
				throw new ArgumentNullException(nameof(options));

			foreach (var x in xOptions.Elements(PpsStuff.xnClientOptionValue))
			{
				var clientOptionKey = x.GetAttribute("key", null);
				if (clientOptionKey == null)
					continue;

				SetClientOptionValueWithHook(r, options, new Stack<string>(), clientOptionKey, x.Value, emitSecureOptions, false);
			}

			// parse references
			var refs = xOptions.GetAttribute("ref", null);
			if (refs != null)
			{
				var refArray = refs.Split(new char[] { ' ', ';' }, StringSplitOptions.RemoveEmptyEntries);
				for (var i = 0; i < refArray.Length; i++)
					options = ParseClientOptions(r, options, emitSecureOptions, GetClientOptionsById(refArray[i]));
			}
			return options;
		} // proc ParseClientOptions

		private void SetDynamicClientOptions(PpsSeenClient device, LuaTable options)
		{
			if (device.GetDumpAppStateFlag())
				SetClientOptionValue(options, "DPC.Request.AppState", true);
			if (device.GetSendLogFlag())
				SetClientOptionValue(options, "DPC.Request.SendLog", true);
			if (device.TryGetAlarmRepeatFlag(out var value))
				SetClientOptionValue(options, "DPC.Request.Tone", value);
		} // proc SetDynamicClientOptions

		private LuaTable GetClientOptionsCore(string deviceId, PpsSeenClient device, ref long lastTick, bool emitSecureOptions)
		{
			var options = new LuaTable();
			var serverTick = GetServerTick();

			// parse options from config
			if (lastTick < 0 || serverTick > lastTick)
			{
				options = ParseClientOptions(
					DEScope.GetScopeService<IDEWebScope>(false),
					options,
					emitSecureOptions,
					GetClientOptionsByDevId(deviceId)
				);
				lastTick = -1;
			}
			
			// set dynamic options, that may change, only if we have a real device
			if (device != null)
				SetDynamicClientOptions(device, options);

			// ask for more options
			CallTableMethods("DeviceOptions", options, deviceId, lastTick);
			
			lastTick = serverTick;
			return options;
		} // func GetClientOptionsCore

		/// <summary>Get the options for the device.</summary>
		/// <param name="clientId"></param>
		/// <param name="lastTick"></param>
		/// <param name="emitSecureOptions"></param>
		/// <returns></returns>
		[LuaMember]
		public LuaTable GetClientOptions(string clientId, long lastTick = -1, bool emitSecureOptions = false)
			=> GetClientOptionsCore(clientId, null, ref lastTick, emitSecureOptions);

		/// <summary>Get the client options for an web request.</summary>
		/// <param name="r"></param>
		/// <returns></returns>
		public LuaTable GetClientOptions(IDEWebRequestScope r)
		{
			var client = GetSeenClient(r, true);
			var lastTick = r.GetProperty("last", -1L);
			using (r.Use())
			{
				var options = GetClientOptionsCore(client.ClientId, client, ref lastTick, true);

				r.OutputHeaders.Add("x-ppsn-lastrefresh", lastTick.ChangeType<string>());

				return options;
			}
		} // func GetClientOptions

		#endregion

		#region -- Dpc - actions ------------------------------------------------------

		private static void GetReceiveLogParamater(IDEWebRequestScope r, string raw, out string extention, out bool readText)
		{
			if (MediaTypeHeaderValue.TryParse(r.InputContentType, out var mediaType))
			{
				extention = raw == "evtx" ? ".evtx" : MimeTypeMapping.GetExtensionFromMimeType(mediaType.MediaType);
				readText = mediaType.MediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase);
			}
			else
			{
				extention = ".dat";
				readText = false;
			}
		} // proc GetReceiveLogParamater

		private string FilterClientId(string deviceId)
		{
			var invalidChars = Path.GetInvalidFileNameChars();
			var sb = new StringBuilder(deviceId.Length);
			var lastWasPoint = false;
			foreach (var c in deviceId)
			{
				if (Array.IndexOf(invalidChars, c) == -1)
				{
					if (c == '.')
					{
						if (!lastWasPoint)
						{
							lastWasPoint = true;
							sb.Append('.');
						}
					}
					else
					{
						lastWasPoint = false;
						sb.Append(c);
					}
				}
			}
			return sb.ToString();
		} // func FilterClientId

		[DEConfigHttpAction("logmsg", IsSafeCall = true, SecurityToken = SecurityDpc)]
		internal void HttpClientReceiveLogMessage(IDEWebRequestScope r, string typ, string msg)
		{
			LogMsgType GetMsgType()
			{
				if (String.IsNullOrEmpty(typ))
					return LogMsgType.Information;
				switch (Char.ToLower(typ[0]))
				{
					case 'e':
						return LogMsgType.Warning;
					case 'd':
						return LogMsgType.Debug;
					case 'w':
						return LogMsgType.Warning;
					default:
						return LogMsgType.Information;
				}
			} // func HttpReceiveLog

			Log.LogMsg(GetMsgType(), $"[{GetSeenClient(r, true).ClientId}] {msg}");
			r.SetStatus(HttpStatusCode.OK, "Message received.");
		} // proc HttpClientReceiveLogMessage

		[DEConfigHttpAction("logpush", IsSafeCall = true, SecurityToken = SecurityDpc)]
		internal void HttpClientReceiveLogFile(IDEWebRequestScope r, string raw = null)
		{
			var client = GetSeenClient(r, true);

			GetReceiveLogParamater(r, raw, out var extention, out var readText);

			// create target file
			var currentLogDirectory = Path.GetDirectoryName(LogFileName);
			var targetFile = new FileInfo(Path.Combine(currentLogDirectory, $"log-{FilterClientId(client.ClientId)}{extention}"));

			// always overwrite
			if (readText)
			{
				using (var dst = targetFile.OpenWrite())
				{
					using (var sw = new StreamWriter(dst, Encoding.UTF8, 4096, true))
					using (var sr = r.GetInputTextReader())
					{
						string line;
						while ((line = sr.ReadLine()) != null)
							sw.WriteLine(line);
					}
					dst.SetLength(dst.Position);
				}
			}
			else
			{
				using (var dst = targetFile.OpenWrite())
				using (var src = r.GetInputStream())
					src.CopyTo(dst);
			}

			Log.Info("{0}: Log received.", client.ClientId);

			r.SetStatus(HttpStatusCode.OK, "Log received.");
		} // proc HttpClientReceiveLogFile

		#endregion

		#region -- info-html - helper -------------------------------------------------

		private static string GetTypeName(LuaType luaType)
			=> luaType.AliasName ?? luaType.Name;

		/// <summary>Used for info.html to print the column name.</summary>
		/// <param name="column"></param>
		/// <returns></returns>
		[LuaMember]
		public string GetColumnDataTypeName(IDataColumn column)
		{
			bool TryGetMaxLength(out int maxLength)
				=> column.Attributes.TryGetProperty("MaxLength", out maxLength) && maxLength > 0 && maxLength < Int32.MaxValue;

			if (column.DataType == typeof(decimal))
			{
				if (!column.Attributes.TryGetProperty<byte>("Precision", out var precision))
					precision = 0;
				if (!column.Attributes.TryGetProperty<byte>("Scale", out var scale))
					scale = 0;

				return $"decimal({precision},{scale})";
			}
			else if (column.DataType == typeof(byte[]) && TryGetMaxLength(out var maxLength))
				return $"byte({maxLength})";
			else if (column.DataType == typeof(string) && TryGetMaxLength(out maxLength))
				return $"string({maxLength})";
			else
				return GetTypeName(LuaType.GetType(column.DataType));
		} // func GetColumnDataTypeName

		/// <summary>Used for info.html to print the key symbol.</summary>
		/// <param name="column"></param>
		/// <returns></returns>
		[LuaMember]
		public LuaResult IsKeyColumn(IDataColumn column)
		{
			if (column.Attributes.TryGetProperty<bool>("IsIdentity", out var isIdentity) && isIdentity)
				return new LuaResult(true, 0);
			else if (column.Attributes.TryGetProperty<bool>("IsPrimary", out var isPrimary) && isPrimary)
				return new LuaResult(true, 1);
			else
				return LuaResult.Empty;
		} // func IsKeyColumn

		/// <summary>Used for info.html to print all attributes.</summary>
		/// <param name="tw"></param>
		/// <param name="attributes"></param>
		/// <returns></returns>
		[LuaMember]
		public void WriteAttributes(TextWriter tw, IPropertyEnumerableDictionary attributes)
		{
			tw.Write("<table>");
			tw.Write("<thead>");
			tw.Write("<tr>");
			tw.Write("<th>Name</th>");
			tw.Write("<th>Type</th>");
			tw.Write("<th>Wert</th>");
			tw.Write("</tr>");
			tw.Write("</thead>");
			tw.Write("<tbody>");
			foreach (var attr in attributes.OrderBy(a => a.Name, PpsColumnDescriptionHelper.AttributeNameComparer))
			{
				tw.Write("<tr>");
				tw.Write("<td>");
				tw.Write(attr.Name);
				tw.Write("</td>");
				tw.Write("<td><code>");
				tw.Write(GetTypeName(LuaType.GetType(attr.Type)));
				tw.Write("</code></td>");
				tw.Write("<td>");

				if (attr.Type.IsEnum)
				{
					tw.Write("<code>");
					tw.Write(attr.Value.ChangeType<int>());
					tw.Write("</code> ");
					tw.Write(attr.Value.ChangeType<string>());
				}
				else
					tw.Write(attr.Value.ChangeType<string>());

				tw.Write("</td>");
				tw.Write("</tr>");
			}
			tw.Write("</tbody>");
			tw.Write("</table>");
		} // proc WriteColumnAttributes

		#endregion

		/// <summary></summary>
		/// <param name="database"></param>
		public void FireDataChangedEvent(string database)
			=> FireSysEvent("ppsn_database_changed", database);

		private XElement GetMimeTypesInfo()
		{
			var x = new XElement("mimeTypes");

			foreach (var cur in MimeTypeMapping.Mappings)
			{
				x.Add(new XElement("mimeType",
					new XAttribute("id", cur.MimeType),
					Procs.XAttributeCreate("isCompressedContent", cur.IsCompressedContent),
					Procs.XAttributeCreate("extensions", String.Join(";", cur.Extensions))
				));
			}

			return x;
		} // func GetMimeTypesInfo

		private static bool WriteTableOption(XmlWriter xml, string n, object value, bool writeEmptyValue)
		{
			int GetStringType(string v)
			{
				var r = 0;

				if (v == null)
					return r;

				for (var i = 0; i < v.Length; i++)
				{
					var c = v[i];
					if (!XmlConvert.IsXmlChar(c))
						return 3;
					else if (i < 1 && (c == '\n' || c == '\r'))
						r = 1;
					else if (c == '<' || c == '>')
						r = 2;
				}

				return r;
			} // func GetStringType

			if (!writeEmptyValue && value == null)
				return false;

			if (value is LuaTable t)
			{
				if (t.Members.Count > 0 || t.ArrayList.Count > 0)
				{
					xml.WriteStartElement(n);
					WriteTableOptions(xml, t);
					xml.WriteEndElement();
				}
			}
			else
			{
				xml.WriteStartElement(n);
				if (value != null)
				{
					if (value is string str)
					{
						switch (GetStringType(str))
						{
							case 1:
								xml.WriteValue(str);
								break;
							case 2:
								xml.WriteCData(str);
								break;
							case 3:
								xml.WriteValue(Convert.ToBase64String(Encoding.UTF8.GetBytes(str)));
								break;
							default:
								xml.WriteValue(str);
								break;
						}
					}
					else if (value is byte[] data)
						xml.WriteValue(Convert.ToBase64String(data));
					else
						xml.WriteValue(value.ChangeType<string>());
				}
				xml.WriteEndElement();
			}

			return true;
		} // proc WriteTableOption

		private static void WriteTableOptions(XmlWriter xml, LuaTable options)
		{
			// write key/value pairs
			foreach (var kv in options.Members)
				WriteTableOption(xml, kv.Key, kv.Value, true);

			// index value 0-10, will be all checked
			var i = 0;
			while (true)
			{
				if (!WriteTableOption(xml, "i" + i.ToString(), options[i], false) && i > 10)
					break;
				i++;
			}
		} // proc WriteTableOptions

		private void WriteApplicationInfo(IDEWebRequestScope r, string applicationName, bool returnAll)
		{
			using (clientApplicationInfos.EnterReadLock())
			{
				clientApplicationInfos.OnBeforeList();

				// get client options, and set specific options
				// this will also change the result header
				var options = r.GetProperty("id", null) != null ? GetClientOptions(r) : new LuaTable();

				// build result
				using (var xml = XmlWriter.Create(r.GetOutputTextWriter(MimeTypes.Text.Xml, r.Http.DefaultEncoding, -1L), Procs.XmlWriterSettings))
				{
					xml.WriteStartElement("ppsn");
					xml.WriteAttributeString("displayName", DisplayName);
					xml.WriteAttributeString("loginSecurity", "NTLM,Basic");

					// add specific application information
					if (!String.IsNullOrEmpty(applicationName))
					{
						var appInfo = GetClientApplicationInfo("msi", applicationName);
						if (appInfo == null)
							xml.WriteAttributeString("version", "1.0.0.0");
						else
						{
							xml.WriteAttributeString("version", appInfo.Version.ToString());
							if (appInfo is PpsClientApplicationFile file && file.HasActiveSource)
								xml.WriteAttributeString("src", r.GetOrigin(new Uri(file.Source, UriKind.Relative)).ToString());
						}
					}

					// return all application
					if (returnAll)
					{
						xml.WriteStartElement("appinfos");
						var itemWriter = new DEListItemWriter(xml);
						foreach (var cur in clientApplicationInfos.List.OfType<PpsClientApplicationFile>().Where(c => c.Type == "msi"))
							clientApplicationInfos.Descriptor.WriteItem(itemWriter, cur);
						xml.WriteEndElement();
					}

					// add mime information
					GetMimeTypesInfo().WriteTo(xml);

					// add options
					xml.WriteStartElement("options");
					WriteTableOptions(xml, options);
					xml.WriteEndElement();

					xml.WriteEndElement();
				}
			}
		} // func WriteApplicationInfo

		private void WriteUserInfo(IDEWebRequestScope r, LuaTable userOptions)
		{
			using (var xml = XmlWriter.Create(r.GetOutputTextWriter(MimeTypes.Text.Xml, r.Http.DefaultEncoding, -1L), Procs.XmlWriterSettings))
			{
				xml.WriteStartElement("user");

				// header information
				foreach(var key in PrivateUserData.WellKnownUserOptionKeys)
				{
					var v = userOptions.GetMemberValue(key, rawGet: false);
					if (v != null)
						xml.WriteAttributeString(key, v.ChangeType<string>());
				}

				// write additional attribues, ignore single value items, only tables are emitted
				foreach (var kv in userOptions.Members)
				{
					if (kv.Value is LuaTable t)
					{
						xml.WriteStartElement(kv.Key);
						WriteTableOptions(xml, t);
						xml.WriteEndElement();
					}
				}
				xml.WriteEndElement();
			}
		} // proc WriteUserInfo

		private async Task WriteInfoPageAsync(IDEWebRequestScope r)
		{
			r.DemandToken(SecuritySys);
			if (r.TryGetProperty<string>("view", out _))
				await Task.Run(() => r.WriteResource(typeof(PpsApplication), "Resources.view.html", MimeTypes.Text.Html));
			else
				await Task.Run(() => r.WriteResource(typeof(PpsApplication), "Resources.info.html", MimeTypes.Text.Html));
		} // proc WriteInfoPageAsync

		private static async Task<bool> WriteClientApplicationSourceAsync(IDEWebRequestScope r, PpsClientApplicationFile appFile)
		{
			if (appFile != null && appFile.TryGetActiveSource(out var source) && source.Info.Exists)
			{
				var fi = source.Info;
				r.SetInlineFileName(fi.Name);
				r.SetLastModified(fi.LastWriteTimeUtc);

				// ignore cache, easy write through
				using (var src = await Task.Run(() => fi.OpenRead()))
					await r.WriteStreamAsync(src, source.MimeType ?? r.Http.GetContentType(fi.Extension) ?? MimeTypes.Application.OctetStream);
				return true;
			}
			else
				return false;
		} // proc WriteClientApplicationSourceAsync

		private async Task<bool> WriteExternalClientApplicationFileAsync(IDEWebRequestScope r)
		{
			using (clientApplicationInfos.EnterReadLock())
			{
				var appFile = clientApplicationInfos.List.Cast<KeyValuePair<string, PpsClientApplicationFile>>()
					.Select(c => c.Value)
					.Where(c => c.Source != null && c.Source == r.RelativeSubPath).FirstOrDefault();
				return await WriteClientApplicationSourceAsync(r, appFile);
			}
		} // func WriteExternalClientApplicationFileAsync

		private async Task<bool> WriteMsiClientApplicationFileAsync(IDEWebRequestScope r)
		{
			var path = r.RelativeSubPath;
			if (!path.StartsWith("app/") || !path.EndsWith(".msi"))
				return false;

			var packageName = path.Substring(4, path.Length - 8);
			using (clientApplicationInfos.EnterReadLock())
			{
				var package = clientApplicationInfos.List.Cast<KeyValuePair<string, PpsClientApplicationFile>>()
					.Where(c => c.Value.Type == "msi" && c.Key == packageName)
					.Select(c => c.Value)
					.FirstOrDefault();

				return await WriteClientApplicationSourceAsync(r, package);
			}
		} // proc WriteMsiClientApplicationFileAsync

		/// <summary></summary>
		/// <param name="r"></param>
		/// <returns></returns>
		protected override async Task<bool> OnProcessRequestAsync(IDEWebRequestScope r)
		{
			switch (r.RelativeSubPath)
			{
				case "info.xml":
					// additional options
					// id={clientId}
					// last=
					await Task.Run(() => WriteApplicationInfo(r,
						r.GetProperty("app", null),
						r.GetProperty("all", false)
					));
					return true;
				case "login.xml":
					r.DemandToken(SecurityUser);

					var ctx = r.GetUser<IPpsPrivateDataContext>();
					await Task.Run(() => WriteUserInfo(r, GetLoginData(ctx)));
					return true;
				case "geometries.xml":
					await WriteXmlGeometriesAsync(r);
					return true;
				case "geometries.json":
					await WriteJsonGeometriesAsync(r);
					return true;
				case "info.html":
					await WriteInfoPageAsync(r);
					return true;

				default:
					if (r.TryEnterSubPath(this, "geometry/"))
					{
						try
						{
							if (await WriteSingleGeometryAsync(r))
								return true;
						}
						finally
						{
							r.ExitSubPath(this);
						}
					}

					var ret = await base.OnProcessRequestAsync(r);

					if (!ret)
						ret = await WriteMsiClientApplicationFileAsync(r);
					if (!ret && resolveExternals)
						return await WriteExternalClientApplicationFileAsync(r);

					return ret;
			}
		} // proc OnProcessRequest
	} // class PpsApplication
}
