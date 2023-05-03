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
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Win32;
using Neo.IronLua;
using TecWare.DE.Networking;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;
using TecWare.PPSn.Networking;
using TecWare.PPSn.Stuff;
using TecWare.PPSn.UI;

namespace TecWare.PPSn
{
	#region -- class PpsStaticServiceAttribute ----------------------------------------

	/// <summary>Enforce static ctor.</summary>
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
	public sealed class PpsStaticServiceAttribute : Attribute
	{
	} // class PpsStaticServiceAttribute 

	#endregion

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

	#region -- struct PpsShellApplicationVersion --------------------------------------

	/// <summary>Version information for Shell-Application</summary>
	public readonly struct PpsShellApplicationVersion : IComparable<Version>, IComparable<PpsShellApplicationVersion>
	{
		#region -- enum VersionType ---------------------------------------------------

		private enum VersionType
		{
			Unknown,
			User,
			Machine
		} // enum VersionType

		#endregion

		private static readonly Version defaultVersion = new Version(1, 0, 0, 0);
		private readonly Version version;
		private readonly VersionType type;

		private PpsShellApplicationVersion(Version version, VersionType type)
		{
			this.version = version ?? defaultVersion;
			this.type = type;
		} // ctor

		/// <summary></summary>
		/// <param name="version"></param>
		/// <param name="isMachine"></param>
		public PpsShellApplicationVersion(Version version, bool isMachine)
			: this(version, isMachine ? VersionType.Machine : VersionType.User)
		{ }

		/// <summary></summary>
		/// <returns></returns>
		public override string ToString()
			=> type == VersionType.Unknown ? "not installed" : Version.ToString() + " " + (type == VersionType.Machine ? "(machine)" : "(user)");

		/// <summary></summary>
		/// <param name="obj"></param>
		/// <returns></returns>
		public override bool Equals(object obj)
			=> obj is PpsShellApplicationVersion v && CompareTo(v) == 0;

		/// <summary></summary>
		/// <returns></returns>
		public override int GetHashCode()
			=> Version.GetHashCode();

		/// <summary>Compare version part</summary>
		/// <param name="other"></param>
		/// <returns></returns>
		public int CompareTo(PpsShellApplicationVersion other)
			=> Version.CompareTo(other.Version);

		int IComparable<Version>.CompareTo(Version other)
			=> Version.CompareTo(other);

		/// <summary></summary>
		public bool PerUser => type == VersionType.User;
		/// <summary></summary>
		public bool PerMachine => type == VersionType.Machine;
		/// <summary></summary>
		public bool IsDefault => type == VersionType.Unknown;
		/// <summary></summary>
		public Version Version => version ?? defaultVersion;

		#region -- Compare ------------------------------------------------------------

		/// <summary></summary>
		/// <param name="left"></param>
		/// <param name="right"></param>
		/// <returns></returns>
		public static bool operator ==(PpsShellApplicationVersion left, PpsShellApplicationVersion right) => left.CompareTo(right) == 0;
		/// <summary></summary>
		/// <param name="left"></param>
		/// <param name="right"></param>
		/// <returns></returns>
		public static bool operator !=(PpsShellApplicationVersion left, PpsShellApplicationVersion right) => left.CompareTo(right) != 0;
		/// <summary></summary>
		/// <param name="left"></param>
		/// <param name="right"></param>
		/// <returns></returns>
		public static bool operator <(PpsShellApplicationVersion left, PpsShellApplicationVersion right) => left.CompareTo(right) < 0;
		/// <summary></summary>
		/// <param name="left"></param>
		/// <param name="right"></param>
		/// <returns></returns>
		public static bool operator <=(PpsShellApplicationVersion left, PpsShellApplicationVersion right) => left.CompareTo(right) <= 0;
		/// <summary></summary>
		/// <param name="left"></param>
		/// <param name="right"></param>
		/// <returns></returns>
		public static bool operator >(PpsShellApplicationVersion left, PpsShellApplicationVersion right) => left.CompareTo(right) > 0;
		/// <summary></summary>
		/// <param name="left"></param>
		/// <param name="right"></param>
		/// <returns></returns>
		public static bool operator >=(PpsShellApplicationVersion left, PpsShellApplicationVersion right) => left.CompareTo(right) >= 0;

		/// <summary></summary>
		/// <param name="left"></param>
		/// <param name="right"></param>
		/// <returns></returns>
		public static bool operator ==(PpsShellApplicationVersion left, Version right) => ((IComparable<Version>)left).CompareTo(right) == 0;
		/// <summary></summary>
		/// <param name="left"></param>
		/// <param name="right"></param>
		/// <returns></returns>
		public static bool operator !=(PpsShellApplicationVersion left, Version right) => ((IComparable<Version>)left).CompareTo(right) != 0;
		/// <summary></summary>
		/// <param name="left"></param>
		/// <param name="right"></param>
		/// <returns></returns>
		public static bool operator <(PpsShellApplicationVersion left, Version right) => ((IComparable<Version>)left).CompareTo(right) < 0;
		/// <summary></summary>
		/// <param name="left"></param>
		/// <param name="right"></param>
		/// <returns></returns>
		public static bool operator <=(PpsShellApplicationVersion left, Version right) => ((IComparable<Version>)left).CompareTo(right) <= 0;
		/// <summary></summary>
		/// <param name="left"></param>
		/// <param name="right"></param>
		/// <returns></returns>
		public static bool operator >(PpsShellApplicationVersion left, Version right) => ((IComparable<Version>)left).CompareTo(right) > 0;
		/// <summary></summary>
		/// <param name="left"></param>
		/// <param name="right"></param>
		/// <returns></returns>
		public static bool operator >=(PpsShellApplicationVersion left, Version right) => ((IComparable<Version>)left).CompareTo(right) >= 0;

		#endregion

#if NET48
		/// <summary></summary>
		/// <param name="registryKey"></param>
		/// <param name="applicationId"></param>
		/// <param name="wow"></param>
		/// <param name="version"></param>
		/// <returns></returns>
		public static bool TryGetInstalledVersion(RegistryKey registryKey, string applicationId, bool wow, out Version version)
		{
			using (var reg = registryKey.OpenSubKey(@"Software\" + (wow ? @"WOW6432Node\" : String.Empty) + @"TecWare\" + applicationId + @"\Components", false))
			{
				if (reg?.GetValue(null) is string versionString)
				{
					version = new Version(versionString);
					return true;
				}
				else
				{
					version = defaultVersion;
					return false;
				}
			}
		} // func TryGetInstalledVersion

		/// <summary></summary>
		/// <returns></returns>
		public static PpsShellApplicationVersion GetInstalledVersion(string applicationId)
		{
			if (TryGetInstalledVersion(Registry.CurrentUser, applicationId, false, out var version))
				return new PpsShellApplicationVersion(version, false);
			else if (TryGetInstalledVersion(Registry.LocalMachine, applicationId, false, out version))
				return new PpsShellApplicationVersion(version, true);
			else if (TryGetInstalledVersion(Registry.LocalMachine, applicationId, true, out version))
				return new PpsShellApplicationVersion(version, true);
			else
				return Default;
		} // func GetInstalledVersion
#endif

		/// <summary></summary>
		public static PpsShellApplicationVersion Default { get; } = new PpsShellApplicationVersion(defaultVersion, VersionType.Unknown);
	} // struct PpsShellApplicationVersion

#endregion

	#region -- interface IPpsShellApplication -----------------------------------------

	/// <summary>This interface is implemented by the main assembly.</summary>
	public interface IPpsShellApplication
	{
		/// <summary>Schedule restart for the application, because a new version of the package is detected.</summary>
		/// <param name="shell"></param>
		/// <param name="uri"></param>
		/// <param name="useRunAs">Needs administrator</param>
		Task RequestUpdateAsync(IPpsShell shell, Uri uri, bool useRunAs);
		/// <summary>Schedule restart for the application, because a new version is detected.</summary>
		/// <param name="shell"></param>
		Task RequestRestartAsync(IPpsShell shell);

		/// <summary>Internal application name of the host.</summary>
		string Name { get; }
		/// <summary>Version of the running application.</summary>
		Version AssenblyVersion { get; }
		/// <summary>Version if the installed package.</summary>
		PpsShellApplicationVersion InstalledVersion { get; }
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

	#region -- interface IPpsShellBackgroundTask --------------------------------------

	/// <summary></summary>
	public interface IPpsShellBackgroundTask : IDisposable
	{
		/// <summary>Pulse this background task.</summary>
		void Pulse();
		/// <summary>Execute task in background, and wait for finish.</summary>
		/// <returns></returns>
		Task RunAsync();
		/// <summary>Wait for the execution of the background task.</summary>
		/// <returns></returns>
		Task WaitAsync();
	} // interface IPpsShellBackgroundTask

	#endregion

	#region -- interface IPpsShell ----------------------------------------------------

	/// <summary>Active shell for services.</summary>
	public interface IPpsShell : IServiceContainer, IPpsCommunicationService, IServiceProvider, INotifyPropertyChanged, IDisposable
	{
		/// <summary>Gets called on the dispose of a shell.</summary>
		event EventHandler Disposed;

		/// <summary>Create a new http request for the uri.</summary>
		/// <param name="uri"></param>
		/// <returns></returns>
		DEHttpClient CreateHttp(Uri uri = null);

		/// <summary>Returns the real uri.</summary>
		/// <param name="http"></param>
		/// <returns></returns>
		Uri GetRemoteUri(DEHttpClient http);
		/// <summary>Translate the uri to an remote uri.</summary>
		/// <param name="uri"></param>
		/// <param name="remoteUri"></param>
		/// <returns></returns>
		bool TryGetRemoteUri(Uri uri, out Uri remoteUri);

		/// <summary>User login to shell</summary>
		/// <param name="userInfo"></param>
		/// <returns></returns>
		Task LoginAsync(ICredentials userInfo);

		/// <summary>Register a new background task.</summary>
		/// <param name="taskAction"></param>
		/// <param name="waitTime"></param>
		IPpsShellBackgroundTask RegisterTask(Func<Task> taskAction, TimeSpan waitTime);

		/// <summary>Invoke a shutdown of this shell.</summary>
		/// <returns></returns>
		Task<bool> ShutdownAsync();

		/// <summary>Instance Id of this shell.</summary>
		int Id { get; }
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
		/// <summary>Local store for the user data of the instance (e.g. the place for the info.xml).</summary>
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
		/// <summary>Update the last used stamp.</summary>
		void UpdateLastUsed();

		/// <summary>Name of the instance.</summary>
		string Name { get; }
		/// <summary>Displayname of the instance for the user</summary>
		string DisplayName { get; }
		/// <summary>Uri</summary>
		Uri Uri { get; }
		/// <summary>Last Server-Version</summary>
		Version Version { get; }
		/// <summary>Last time this shell was used.</summary>
		DateTime LastUsed { get; }

		/// <summary>Local store for the instance data.</summary>
		DirectoryInfo LocalPath { get; }
		/// <summary>Is this a per machine store.</summary>
		bool PerMachine { get; }
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
		/// <param name="perMachine">Enforce a perUser or perMachine shell. <c>null</c> for default.</param>
		/// <returns>Shell info.</returns>
		IPpsShellInfo CreateNew(string instanceName, string displayName, Uri uri, bool? perMachine = null);
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
		public const string DpcUnlockCodeKey = "DPC.UnlockCode";
		public const string DpcDebugModeKey = "DPC.Debug";

		public const string DpcDeviceIdKey = "DPC.DeviceId";

		public const string PpsnUriKey = "PPSn.Uri";
		public const string PpsnNameKey = "PPSn.Name";
		public const string PpsnSecurtiyKey = "PPSn.Security";
		public const string PpsnVersionKey = "PPSn.Version";

		public const string PpsnTouchKeyboard = "PPSn.Application.TouchKeyboard";
		public const string PpsnClockFormatKey = "PPSn.Application.ClockFormat";
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
		/// <summary>Unlockbarcode for trace pane.</summary>
		public string DpcUnlockCode => this.GetProperty<string>(DpcUnlockCodeKey, null);
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
		/// <summary>Use touch keyboard</summary>
		public bool? UseTouchKeyboard
		{
			get
			{
				var d = this.GetProperty<string>(PpsnTouchKeyboard, "default");
				switch (d.ToLower())
				{
					case "t":
					case "true":
						return true;
					case "f":
					case "false":
						return false;
					default:
						return null;
				}
			}
		} // prop UseTouchKeyboard

		/// <summary></summary>
		public string ClockFormat => this.GetProperty(PpsnClockFormatKey, "HH:mm\ndd.MM.yyyy");
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

	#region -- class PpsShellLogItem --------------------------------------------------

	/// <summary>Simple log-item store.</summary>
	public sealed class PpsShellLogItem
	{
		internal PpsShellLogItem(LogMsgType type, string message)
		{
			Type = type;
			Text = message;
		} // ctor

		/// <summary>Generate a log line</summary>
		/// <returns></returns>
		public override string ToString()
			=> $"{Stamp:yyyy-MM-dd HH:mm:ss:fff}\t{(int)Type}\t{Text.EscapeSpecialChars()}";

		/// <summary></summary>
		public DateTime Stamp { get; } = DateTime.Now;
		/// <summary>Type of the message.</summary>
		public LogMsgType Type { get; }
		/// <summary>Text</summary>
		public string Text { get; }
	} // class PpsShellLogItem

	#endregion

	#region -- interface IPpsLogService -----------------------------------------------

	/// <summary></summary>
	public interface IPpsLogService
	{
		/// <summary></summary>
		IReadOnlyCollection<PpsShellLogItem> Log { get; }
	} // interface IPpsLogService

	#endregion

	#region -- class PpsShell ---------------------------------------------------------

	/// <summary>Global environment to host multiple active environments.</summary>
	[PpsStaticService]
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
			private static readonly Regex regexLocalUri = new Regex(@"^http://ppsn(?<id>\d+).local(?<path>.*)", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

			private readonly IPpsShell shell;
			private readonly Uri remoteUri;

			public PpsMessageHandler(IPpsShell shell, Uri remoteUri)
			{
				this.shell = shell ?? throw new ArgumentNullException(nameof(shell));
				this.remoteUri = remoteUri ?? throw new ArgumentNullException(nameof(remoteUri));
				if (!remoteUri.IsAbsoluteUri)
					throw new ArgumentException("Only absolute uri's are allowed.", nameof(remoteUri));

				AllowAutoRedirect = false;
			} // ctor

			protected override void Dispose(bool disposing)
			{
				if (shell is PpsShellImplementation shellImpl)
					shellImpl.UnregisterHandler(this);
				base.Dispose(disposing);
			} // proc Dispose

			public bool TryGetRemoteUri(Uri uri, out Uri remoteUri)
			{
				remoteUri = null;

				if (!uri.IsAbsoluteUri)
					return false;

				var m = regexLocalUri.Match(uri.ToString());
				if (!m.Success)
					return false;

				var uriId = Int32.Parse(m.Groups["id"].Value);
				var absolutePath = m.Groups["path"].Value;

				if (shell.Id != uriId)
					throw new ArgumentException("Invalid proxy.");

				if (absolutePath.StartsWith("/")) // if the uri starts with "/", remove it, because the info.remoteUri is our root
					absolutePath = absolutePath.Substring(1);
				var relativeUri = new Uri(absolutePath, UriKind.Relative);

				remoteUri = new Uri(this.remoteUri, relativeUri);
				return true;
			} // func TryGetRemoteUri

			protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
			{
				// todo: download cache for files?

				if (shell is PpsShellImplementation shellImpl)
					shellImpl.HttpRequestStarted();

				// exchange proxy uri to real one
				if (TryGetRemoteUri(request.RequestUri, out var uri))
					request.RequestUri = uri;

				return base.SendAsync(request, cancellationToken)
					.ContinueWith(SendFinish, TaskContinuationOptions.ExecuteSynchronously);
			} // func SendAsync

			private HttpResponseMessage SendFinish(Task<HttpResponseMessage> task)
			{
				try
				{
					var result = task.Result;

					if (shell is PpsShellImplementation shellImpl)
						shellImpl.HttpRequestFinished(null);

					return result;
				}
				catch (AggregateException e)
				{
					if (e.InnerException is HttpRequestException ex)
					{
						if (shell is PpsShellImplementation shellImpl)
							shellImpl.HttpRequestFinished(ex);
					}
					throw;
				}
			} // proc SendFinish

			public Uri RemoteUri => remoteUri;
		} // class PpsMessageHandler

		#endregion

		#region -- class PpsBackgroundWorker ------------------------------------------

		private sealed class PpsBackgroundWorker : PpsSynchronizationQueue, IDisposable
		{
			private struct VoidResult { }

			#region -- class BackgroundTask -------------------------------------------

			private sealed class BackgroundTask : IPpsShellBackgroundTask
			{
				private readonly PpsBackgroundWorker worker;
				private readonly Func<Task> taskAction;
				private readonly TimeSpan waitTime;

				private readonly object nextRunLock = new object();
				private DateTime nextRun = DateTime.MinValue;
				private bool restartPulse = false;

				private readonly List<TaskCompletionSource<VoidResult>> waitForCycle = new List<TaskCompletionSource<VoidResult>>();
				private readonly List<TaskCompletionSource<VoidResult>> waitForNextCycle = new List<TaskCompletionSource<VoidResult>>();

				public BackgroundTask(PpsBackgroundWorker worker, Func<Task> taskAction, TimeSpan waitTime)
				{
					this.worker = worker ?? throw new ArgumentNullException(nameof(worker));
					this.taskAction = taskAction ?? throw new ArgumentNullException(nameof(taskAction));
					this.waitTime = waitTime;
					if (waitTime.TotalMilliseconds < 0)
						throw new ArgumentOutOfRangeException(nameof(waitTime));
				} // ctor

				public void Dispose()
					=> worker.UnregisterTask(this);

				private async Task RunTaskCoreAsync()
				{
					Exception exception = null;
					try
					{
						await taskAction();
					}
					catch (Exception e)
					{
						exception = e;
						worker.shell.LogMsg(LogMsgType.Error, e);
					}

					lock (nextRunLock)
					{
						if (restartPulse)
						{
							restartPulse = false;
							nextRun = DateTime.MinValue;
						}
						else
							nextRun = DateTime.Now + waitTime;

						// finish wait tasks
						while (waitForCycle.Count > 0)
						{
							var i = waitForCycle.Count - 1;
							var tcs = waitForCycle[i];
							waitForCycle.RemoveAt(i);

							// notify wait event
							if (exception == null)
								tcs.TrySetResult(new VoidResult());
							else if (exception is TaskCanceledException)
								tcs.TrySetCanceled();
							else
								tcs.TrySetException(exception);
						}

						// copy wait tasks
						waitForCycle.AddRange(waitForNextCycle);
						waitForNextCycle.Clear();
					}
				} // proc RunTaskFinish

				internal (Task, DateTime) RunTaskAsync()
				{
					lock (nextRunLock)
					{
						// is time reached
						if (nextRun >= DateTime.Now)
							return (null, nextRun);

						// mark start
						nextRun = DateTime.MaxValue;
					}

					return (RunTaskCoreAsync(), DateTime.MaxValue);
				} // proc RunTaskAsync

				public void Pulse()
				{
					lock (nextRunLock)
					{
						if (nextRun == DateTime.MaxValue)
							restartPulse = true;
						else
							nextRun = DateTime.MinValue;
					}
					worker.PulseTaskQueue();
				} // proc Pulse

				public Task RunAsync()
				{
					var tcs = new TaskCompletionSource<VoidResult>();

					lock (nextRunLock)
					{
						if (nextRun == DateTime.MaxValue)
						{
							restartPulse = true;
							waitForNextCycle.Add(tcs);
						}
						else
						{
							nextRun = DateTime.MinValue;
							waitForCycle.Add(tcs);
						}
					}

					worker.PulseTaskQueue();

					return tcs.Task;
				} // proc RunAsync

				public Task WaitAsync()
				{
					var tcs = new TaskCompletionSource<VoidResult>();
					lock (nextRunLock)
						waitForCycle.Add(tcs);
					return tcs.Task;
				} // proc WaitBackgroundCycleAsync
			} // class BackgroundTask

			#endregion

			private readonly PpsShellImplementation shell;
			private readonly Thread thread;

			private readonly List<BackgroundTask> backgroundTasks = new List<BackgroundTask>();

			#region -- Ctor/Dtor ------------------------------------------------------

			public PpsBackgroundWorker(PpsShellImplementation shell)
			{
				this.shell = shell ?? throw new ArgumentNullException(nameof(shell));

				thread = new Thread(ExecuteCommunication)
				{
					Name = nameof(PpsBackgroundWorker) + " " + shell.Info.Name,
					IsBackground = true
				};
				thread.Start();
			} // ctor

			public void Dispose()
			{
				Stop();
				thread.Join(5000);
			} // prop Dispose

			#endregion

			#region -- Background -----------------------------------------------------

			protected override void OnWait(ManualResetEventSlim wait)
			{
				if (TryGetRunTasks(out var tasks, out var nextCycleScheduled)) // start batch of background tasks
				{
					// run task within this context
					var ts = new CancellationTokenSource();
					var t = Task.WhenAll(tasks); // start all tasks

					// on completed finish task loop
					t.GetAwaiter().OnCompleted(ts.Cancel);
					ProcessTaskLoopUnsafe(ts.Token, false);

					// is loop faulted, log exception
					if (t.IsFaulted)
						shell.ShowExceptionAsync(true, t.Exception);
				}
				else
				{
					// calculate next wake up time
					var sleepFor = (int)(nextCycleScheduled - DateTime.Now).TotalMilliseconds;
					if (sleepFor <= 0)
						Thread.Sleep(100); // avoid endless loops
					else
						wait.Wait(sleepFor);
				}
			} // proc OnWait

			private void ExecuteCommunication()
				=> Use(() => ProcessTaskLoopUnsafe(CancellationToken.None, true));

			#endregion

			#region -- Tasks ----------------------------------------------------------

			public IPpsShellBackgroundTask RegisterTask(Func<Task> taskAction, TimeSpan waitTime)
			{
				lock (backgroundTasks)
				{
					var t = new BackgroundTask(this, taskAction, waitTime);
					backgroundTasks.Add(t);
					return t;
				}
			} // func RegisterTask

			private void UnregisterTask(BackgroundTask task)
			{
				lock (backgroundTasks)
					backgroundTasks.Remove(task);
			} // proc UnregisterTask

			private bool TryGetRunTasks(out Task[] tasks, out DateTime minNextRun)
			{
				var waitTasks = new List<Task>();
				minNextRun = DateTime.MaxValue;

				lock (backgroundTasks)
				{
					foreach (var t in backgroundTasks)
					{
						var (task, nextRun) = t.RunTaskAsync();
						if (task != null)
							waitTasks.Add(task);
						else if (nextRun < minNextRun)
							minNextRun = nextRun;
					}
				}

				tasks = waitTasks.ToArray();
				return tasks.Length > 0;
			} // proc RunTasksAsync

			#endregion

			protected override Thread Thread => thread;
		} // class PpsBackgroundWorker 

		#endregion

		#region -- class PpsShellImplementation ---------------------------------------

		private sealed class PpsShellImplementation : IServiceContainer, IPpsShell, IPpsSettingsService, ILogger, IPpsLogService, IDisposable
		{
			private readonly WeakEventList<PropertyChangedEventHandler, PropertyChangedEventArgs> propertyChangedList = new WeakEventList<PropertyChangedEventHandler, PropertyChangedEventArgs>();
			private readonly WeakEventList<EventHandler, EventArgs> disposedList = new WeakEventList<EventHandler, EventArgs>();

			public event PropertyChangedEventHandler PropertyChanged
			{
				add => propertyChangedList.Add(value);
				remove => propertyChangedList.Remove(value);
			} // event PropertyChanged

			public event EventHandler Disposed
			{
				add => disposedList.Add(value);
				remove => disposedList.Remove(value);
			} // event Disposed

			private readonly IServiceProvider parentProvider;
			private readonly IPpsShellInfo info;
			private readonly int shellId;
			private readonly Dictionary<Type, object> services = new Dictionary<Type, object>(TypeComparer.Default);
			private IPpsSettingsService settingsService = null;
			private IPpsSettingsService userSettingsService = null;
			private PpsShellSettings instanceSettingsInfo = null;
			private PpsShellUserSettings userSettingsInfo = null;
			private DirectoryInfo localUserPath = null;

			private readonly PpsBackgroundWorker backgroundWorker = null;
			private IPpsShellBackgroundTask serverSettingsTask = null;
			private long lastSettingsVersion = 0;
			private DEHttpClient http = null;
			private readonly Dictionary<DEHttpClient, PpsMessageHandler> knownMessageHandler = new Dictionary<DEHttpClient, PpsMessageHandler>();
			private PpsCommunicationState connectionState = PpsCommunicationState.Disconnected;

			private readonly ObservableCollection<PpsShellLogItem> logItems = new ObservableCollection<PpsShellLogItem>();

			#region -- Ctor/Dtor ------------------------------------------------------

			public PpsShellImplementation(IServiceProvider parentProvider, IPpsShellInfo info)
			{
				this.info = info ?? throw new ArgumentNullException(nameof(info));
				shellId = GetNextShellId();
				this.parentProvider = parentProvider ?? throw new ArgumentNullException(nameof(parentProvider));

				info.UpdateLastUsed();

				AddService(typeof(IServiceProvider), this);
				AddService(typeof(IPpsShell), this);
				AddService(typeof(IPpsCommunicationService), this);
				AddService(typeof(ILogger), this);
				AddService(typeof(IPpsLogService), this);

				backgroundWorker = new PpsBackgroundWorker(this);
			} // ctor

			public void Dispose()
			{
				if (currentShell == this)
					SetCurrent(null);

				disposedList.Invoke(this, EventArgs.Empty);

				// stop background worker
				serverSettingsTask?.Dispose();
				backgroundWorker.Dispose();

				// dispose http
				http?.Dispose();

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
					using (var directHttp = CreateHttpCore(null, info.Uri, Settings.GetDpcCredentials()))
					{
						http = directHttp;

						// load settings from server
						lastSettingsVersion = await LoadSettingsFromServerAsync(settingsService, this, instanceSettingsInfo.DpcDeviceId, lastSettingsVersion);
					}

					using (var dpcHttp = CreateHttpCore(CreateProxyUri(shellId), info.Uri, Settings.GetDpcCredentials()))
					{
						http = dpcHttp;

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
						foreach (var init in EnumerateShellServiceInit())
							await init.InitAsync();

						if (notify != null)
							await notify.OnAfterInitServicesAsync(this);

						// start background task for settings
						serverSettingsTask = backgroundWorker.RegisterTask(RefreshSettingsFromServerTaskAsync, TimeSpan.FromSeconds(60));
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
				foreach (var init in EnumerateShellServiceInit())
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
				=> propertyChangedList.Invoke(this, new PropertyChangedEventArgs(propertyName));

			IPpsShellBackgroundTask IPpsShell.RegisterTask(Func<Task> taskAction, TimeSpan waitTime)
				=> backgroundWorker.RegisterTask(taskAction, waitTime);

			private IEnumerable<IPpsShellServiceInit> EnumerateShellServiceInit()
				=> services.Values.OfType<IPpsShellServiceInit>().Distinct();

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

			private void RegisterHandler(DEHttpClient http, PpsMessageHandler handler)
			{
				knownMessageHandler[http] = handler;
			} // proc RegisterHandler

			internal void UnregisterHandler(PpsMessageHandler handler)
			{
				var kv = knownMessageHandler.FirstOrDefault(c => c.Value == handler);
				if (kv.Key != null)
					knownMessageHandler.Remove(kv.Key);
			} // proc UnregisterHandler

			/// <summary>Createa a http-client that always uses a proxy uri. It is possible to proxy request for other clients.</summary>
			/// <param name="uri"></param>
			/// <param name="remoteUri"></param>
			/// <param name="credentials"></param>
			/// <returns></returns>
			private DEHttpClient CreateHttpCore(Uri uri, Uri remoteUri, ICredentials credentials)
			{
				var handler = new PpsMessageHandler(this, remoteUri);
				var http = DEHttpClient.Create(uri ?? remoteUri, credentials, httpHandler: handler);
				if (uri != null)
					RegisterHandler(http, handler);
				return http;
			} // proc CreateHttpCore

			public DEHttpClient CreateHttp(Uri uri = null)
			{
				if (http == null)
					throw new InvalidOperationException();
				else if (uri == null)
					return CreateHttpCore(http.BaseAddress, GetRemoteUri(http), http.Credentials);
				else if (uri.IsAbsoluteUri)
				{
					var relativeUri = http.BaseAddress.MakeRelativeUri(uri);
					return CreateHttpCore(new Uri(http.BaseAddress, relativeUri), GetRemoteUri(http), http.Credentials);
				}
				else // relative to base
					return CreateHttpCore(new Uri(http.BaseAddress, uri), GetRemoteUri(http), http.Credentials);
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
				var newHttp = CreateHttpCore(CreateProxyUri(shellId), info.Uri, credentials);
				try
				{
					var userName = GetUserNameFromCredentials(credentials);

					// create local directory from user name
					localUserPath = new DirectoryInfo(Path.Combine(info.LocalPath.FullName, userName));
					if (!localUserPath.Exists)
					{
						localUserPath.Create();
						if (info.PerMachine)
							FileShellFactory.RestrictControlToUser(localUserPath, userName);
					}

					// load user settings
					var userSettings = FileSettingsInfo.CreateUserSettings(this, new FileInfo(Path.Combine(LocalUserPath.FullName, "info.xml")), userName);
					await userSettings.LoadAsync();

					// login user and parse user specific settings
					userSettingsService = userSettings;
					userSettingsInfo = new PpsShellUserSettings(userSettingsService);
					await LoadUserSettingsFromServerAsync(userSettingsService, newHttp);

					// update http
					http?.Dispose();
					http = newHttp;
					OnPropertyChanged(nameof(Http));

					// notify login to services
					foreach (var init in EnumerateShellServiceInit())
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
				foreach (var init in EnumerateShellServiceInit())
					await init.DoneUserAsync();
			} // proc LogoutAsync

			private async Task RefreshSettingsFromServerTaskAsync()
			{
				// settings from server and check connection state
				if (http != null)
					lastSettingsVersion = await LoadSettingsFromServerAsync(settingsService, this, instanceSettingsInfo.DpcDeviceId, lastSettingsVersion);
			} // proc RunBackgroundTasksAsync

			internal void HttpRequestStarted()
			{
				if (connectionState == PpsCommunicationState.Disconnected)
					SetConnectionState(PpsCommunicationState.Connecting);
			} // proc HttpRequestStarted

			internal void HttpRequestFinished(HttpRequestException e)
			{
				if (e == null)
					SetConnectionState(PpsCommunicationState.Connected);
				else
					SetConnectionState(PpsCommunicationState.Disconnected);
			} // proc HttpRequestFinished

			private void SetConnectionState(PpsCommunicationState state)
			{
				if (state != connectionState)
				{
					connectionState = state;
					this.GetService<IPpsUIService>(true).RunUI(() => OnPropertyChanged(nameof(ConnectionState)));
				}
			} // proc SetConnectionState

			private PpsMessageHandler GetMessageHandler(DEHttpClient http)
				=> knownMessageHandler[http] ?? throw new ArgumentOutOfRangeException(nameof(http));

			public Uri GetRemoteUri(DEHttpClient http)
				=> GetMessageHandler(http).RemoteUri;

			public bool TryGetRemoteUri(Uri uri, out Uri remoteUri)
				=> GetMessageHandler(http).TryGetRemoteUri(uri, out remoteUri);

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

			private void LogMsgAppend(LogMsgType type, string message)
			{
				lock (logItems)
				{
					logItems.Add(new PpsShellLogItem(type, message));

					while (logItems.Count > 1000) // 0xFFFF seems to much for UI
						logItems.RemoveAt(0);
				}
			} // proc LogMsgAppend

			void ILogger.LogMsg(LogMsgType type, string message)
			{
				DebugLogger.LogMsg(type, message);
				LogMsgAppend(type, message);
			} // proc ILogger.LogMsg

			IReadOnlyCollection<PpsShellLogItem> IPpsLogService.Log => logItems;

			#endregion

			public int Id => shellId;
			public string DeviceId => instanceSettingsInfo.DpcDeviceId;
			public IPpsShellInfo Info => info;

			public DEHttpClient Http => http;
			public IPpsSettingsService SettingsService => settingsService;
			public PpsShellSettings Settings => instanceSettingsInfo;
			public PpsShellUserSettings UserSettings => userSettingsInfo;

			public DirectoryInfo LocalPath => info.LocalPath;
			public DirectoryInfo LocalUserPath => localUserPath;

			public bool IsInitialized { get; private set; }
			public bool IsAuthentificated => connectionState == PpsCommunicationState.Connected && http.Credentials != null;
			public PpsCommunicationState ConnectionState => connectionState;

			// -- Static ----------------------------------------------------------

			private static int shellCounter = 1;

			private static int GetNextShellId()
				=> shellCounter++;
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
			private readonly WeakEventList<PropertyChangedEventHandler, PropertyChangedEventArgs> propertyChangedList = new WeakEventList<PropertyChangedEventHandler, PropertyChangedEventArgs>();

			public event PropertyChangedEventHandler PropertyChanged
			{
				add => propertyChangedList.Add(value);
				remove => propertyChangedList.Add(value);
			} // event PropertyChanged

			private IPpsSettingsService currentSettingsService = null;

			public PpsSettingsProxy()
			{
			} // ctor

			private void ConnectSettingsService(IPpsSettingsService settingsService)
			{
				if (currentSettingsService != null)
					currentSettingsService.PropertyChanged -= CurrentSettingsService_PropertyChanged;

				currentSettingsService = settingsService;

				if (currentSettingsService != null)
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
				=> propertyChangedList.Invoke(this, e);

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
			private readonly WeakEventList<PropertyChangedEventHandler, PropertyChangedEventArgs> propertyChangedList = new WeakEventList<PropertyChangedEventHandler, PropertyChangedEventArgs>();

			public event PropertyChangedEventHandler PropertyChanged
			{
				add => propertyChangedList.Add(value);
				remove => propertyChangedList.Remove(value);
			} // event PropertyChanged

			public PpsCommunicationProxy()
			{
			} // ctor

			protected override void OnCurrentChanged(IPpsShell current, IPpsShell old)
			{
				base.OnCurrentChanged(current, old);

				OnPropertyChanged(new PropertyChangedEventArgs(nameof(Http)));
				OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsAuthentificated)));
				OnPropertyChanged(new PropertyChangedEventArgs(nameof(ConnectionState)));
			} // proc OnCurrentChanged

			protected override void OnCurrentPropertyChanged(PropertyChangedEventArgs e)
			{
				switch (e.PropertyName)
				{
					case nameof(Http):
					case nameof(IsAuthentificated):
					case nameof(ConnectionState):
						OnPropertyChanged(e);
						break;
					default:
						base.OnCurrentPropertyChanged(e);
						break;
				}
			} // proc OnCurrentPropertyChanged

			private void OnPropertyChanged(PropertyChangedEventArgs e)
				=> propertyChangedList.Invoke(this, e);

			public DEHttpClient Http => currentShell?.Http;

			public bool IsAuthentificated => currentShell?.IsAuthentificated ?? false;
			public PpsCommunicationState ConnectionState => currentShell?.ConnectionState ?? PpsCommunicationState.Disconnected;
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

		private static readonly WeakEventList<EventHandler, EventArgs> currentChangedList = new WeakEventList<EventHandler, EventArgs>();

		/// <summary>Is the current shell changed.</summary>
		public static event EventHandler CurrentChanged
		{
			add => currentChangedList.Add(value);
			remove => currentChangedList.Remove(value);
		} // event CurrentChanged
		
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

		/// <summary>Create the proxy uri for a shell</summary>
		/// <param name="shellId"></param>
		/// <returns></returns>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public static Uri CreateProxyUri(int shellId)
			=> new Uri($"http://ppsn{shellId}.local");

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
				if (t.GetCustomAttribute<PpsStaticServiceAttribute>() != null) // enforce static ctor
					RuntimeHelpers.RunClassConstructor(t.TypeHandle);
				
				if (t.GetCustomAttributes<PpsServiceAttribute>().FirstOrDefault() != null)
				{
					if (typeof(IPpsShellService).IsAssignableFrom(t)) // is this service a shell service
						shellServices.Add(t);
					else if (t.GetCustomAttribute<PpsLazyServiceAttribute>() != null)// add a normal service
						AddServices(global, t, new LazyServiceCreator(t).CreateService);
					else
						AddServices(global, t, Activator.CreateInstance(t));
				}
			}
		} // proc Collect

		#endregion

		#region -- Start --------------------------------------------------------------

		private static void SetCurrent(PpsShellImplementation value)
		{
			if (currentShell != value)
			{
				currentShell = value;
				currentChangedList.Invoke(null, EventArgs.Empty);
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

		private static IEnumerable<PropertyValue> GetLoadSettingsArguments(IPpsShellApplication application, string clientId, long lastRefreshTick)
		{
			if (application != null)
			{
				var appName = application.Name;
				if (application.InstalledVersion.PerMachine)
					appName += ".Machine";
				yield return new PropertyValue("app", appName);
			}
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
		public static async Task<long> LoadSettingsFromServerAsync(IPpsSettingsService settingsService, IPpsShell shell, string clientId, long lastRefreshTick)
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

					if (installedVersion.Version < serverVersion) // new version is provided
						await application.RequestUpdateAsync(shell, new Uri(xInfo.GetAttribute("src", null), UriKind.Absolute), installedVersion.PerMachine);
					else if (assemblyVersion < installedVersion.Version) // new version is installed, but not active
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

		/// <summary>Get unique identifier for the credential manager.</summary>
		/// <param name="shellInfo"></param>
		/// <returns></returns>
		public static string GetCredentialTarget(this IPpsShellInfo shellInfo)
			=> "ppsn_env:" + shellInfo.Name;

		#endregion

		#region -- Log ----------------------------------------------------------------

		/// <summary>Create a text file from the current log.</summary>
		/// <param name="logService"></param>
		/// <returns></returns>
		public static string GetLogAsText(this IPpsLogService logService)
			=> String.Join(Environment.NewLine, logService.Log.Select(c => c.ToString()));

		#endregion

		#region -- GetCleanShellName --------------------------------------------------

		/// <summary>Replace all none direction chars.</summary>
		/// <param name="name"></param>
		/// <returns></returns>
		public static string GetCleanShellName(string name)
		{
			var sb = new StringBuilder();
			foreach (var c in name)
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
		} // func GetCleanShellName

		#endregion

		/// <summary>Current shell</summary>
		public static IPpsShell Current => currentShell;

		/// <summary>Access global service provider.</summary>
		public static IServiceContainer Global => global;
	} // class PpsShell

	#endregion
}
