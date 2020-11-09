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
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using TecWare.DE.Networking;
using TecWare.DE.Stuff;
using TecWare.PPSn.UI;

namespace TecWare.PPSn
{
	#region -- class PpsShell ---------------------------------------------------------

	public static partial class PpsShell
	{
		#region -- GetUriFromString, GetNormalizedUri ---------------------------------

		/// <summary>Create a uri from an string.</summary>
		/// <param name="uri"></param>
		/// <param name="kind"></param>
		/// <returns><c>null</c>, if the uri was invalid.</returns>
		public static Uri GetUriFromString(string uri, UriKind kind)
		{
			if (String.IsNullOrEmpty(uri))
				return null;

			return Uri.TryCreate(uri, kind, out var result) ? result : null;
		} // func GetUriFromString

		/// <summary>Format a normalized connection uri.</summary>
		/// <param name="uri"></param>
		/// <param name="uriPath"></param>
		/// <param name="uriQuery"></param>
		/// <returns></returns>
		public static void GetUriParts(Uri uri, out string uriPath, out string uriQuery)
		{
			// /info.xml?id=<id> is allowd but cut off
			uriPath = uri.GetComponents(UriComponents.Scheme | UriComponents.HostAndPort | UriComponents.Path, UriFormat.UriEscaped);
			uriQuery = uri.GetComponents(UriComponents.Query, UriFormat.UriEscaped);

			// normalize
			if (uriPath.EndsWith("/info.xml")) // remove info.xml
				uriPath = uriPath.Substring(0, uriPath.Length - 8);
			if (uriPath[uriPath.Length - 1] != '/') // add path seperator
				uriPath += '/';
		} // func GetUriParts

		/// <summary>Format a normalized connection uri.</summary>
		/// <param name="uri"></param>
		/// <param name="uriKind"></param>
		/// <param name="uriPath"></param>
		/// <param name="uriQuery"></param>
		/// <returns></returns>
		public static void GetUriParts(string uri, UriKind uriKind, out string uriPath, out string uriQuery)
			=> GetUriParts(GetUriFromString(uri, uriKind), out uriPath, out uriQuery);

		#endregion

		#region -- Paths --------------------------------------------------------------

		private static DirectoryInfo EncforcePath(DirectoryInfo di)
		{
			if (!di.Exists)
				di.Create();
			return di;
		} // func EncforcePath

		/// <summary></summary>
		/// <param name="shell"></param>
		/// <param name="path"></param>
		/// <returns></returns>
		public static DirectoryInfo EnforceLocalPath(this IPpsShell shell, string path)
			=> EncforcePath(new DirectoryInfo(Path.Combine(shell.LocalPath.FullName, path)));

		/// <summary></summary>
		/// <param name="shell"></param>
		/// <param name="path"></param>
		/// <returns></returns>
		public static DirectoryInfo EnforceLocalUserPath(this IPpsShell shell, string path)
			=> EncforcePath(new DirectoryInfo(Path.Combine(shell.LocalUserPath.FullName, path)));

		#endregion

		#region -- GetUserNameFromCredentials -----------------------------------------

		private static string GetDomainUserName(string domain, string userName)
			=> String.IsNullOrEmpty(domain) ? userName : domain + "\\" + userName;

		/// <summary>Get the user name from the credentials.</summary>
		/// <param name="userInfo"></param>
		/// <returns></returns>
		public static string GetUserNameFromCredentials(this ICredentials userInfo)
		{
			if (userInfo == null)
				return null;
			else if (CredentialCache.DefaultCredentials == userInfo)
				return GetDomainUserName(Environment.UserDomainName, Environment.UserName);
			else if (userInfo is NetworkCredential networkCredential)
				return GetDomainUserName(networkCredential.Domain, networkCredential.UserName);
			else if (userInfo is UserCredential userCredential)
				return GetDomainUserName(userCredential.Domain, userCredential.UserName);
			else
				throw new ArgumentOutOfRangeException("Invalid userInfo.");
		} // func GetUserNameFromCredentials

		#endregion

		#region -- Logger -------------------------------------------------------------

		#region -- class PpsDummyLogger -----------------------------------------------

		private sealed class PpsDummyLogger : ILogger
		{
			public void LogMsg(LogMsgType type, string message)
				=> Debug.WriteLine("[" + type.ToString() + "] " + message);
		} // class PpsDummyLogger

		#endregion

		/// <summary>Exception to log.</summary>
		/// <param name="log"></param>
		/// <param name="exception"></param>
		/// <param name="alternativeMessage"></param>
		public static void Except(this ILogger log, Exception exception, string alternativeMessage)
			=> log.LogMsg(LogMsgType.Error, alternativeMessage + Environment.NewLine + exception.GetMessageString());

		/// <summary>Logger that prints to the debug output.</summary>
		public static ILogger DebugLogger { get; } = new PpsDummyLogger();

		#endregion

		#region -- Idle ---------------------------------------------------------------

		#region -- class FunctionIdleActionImplementation -----------------------------

		private sealed class FunctionIdleActionImplementation : IPpsIdleAction
		{
			private readonly Func<int, PpsIdleReturn> onIdle;

			public FunctionIdleActionImplementation(Func<int, PpsIdleReturn> onIdle)
			{
				this.onIdle = onIdle ?? throw new ArgumentNullException(nameof(onIdle));
			} // ctor

			PpsIdleReturn IPpsIdleAction.OnIdle(int elapsed)
				=> onIdle(elapsed);
		} // class FunctionIdleActionImplementation

		#endregion

		/// <summary>Add a idle function to the idle-service</summary>
		/// <param name="idleService"></param>
		/// <param name="onIdle"></param>
		/// <returns></returns>
		[Obsolete("Use AddIdleFunction with new return.")]
		public static IPpsIdleAction AddIdleFunction(this IPpsIdleService idleService, Func<int, bool> onIdle)
			=> idleService.Add(new FunctionIdleActionImplementation(e => onIdle(e) ? PpsIdleReturn.Idle : PpsIdleReturn.StopIdle));

		/// <summary>Add a idle function to the idle-service</summary>
		/// <param name="idleService"></param>
		/// <param name="onIdle"></param>
		/// <returns></returns>
		public static IPpsIdleAction AddIdleFunction(this IPpsIdleService idleService, Func<int, PpsIdleReturn> onIdle)
			=> idleService.Add(new FunctionIdleActionImplementation(onIdle));

		#endregion

		#region -- GetAppVersion ------------------------------------------------------

		private static Version GetAppVersion()
		{
			var versionString = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
			return String.IsNullOrEmpty(versionString) ?
				new Version() :
				new Version(versionString);
		} // func GetAppVersion

		/// <summary>Application version.</summary>
		public static Version AppVersion => appVersion.Value;

		#endregion

		/// <summary>Is this process a 64bit process</summary>
		public static bool Is64BitProcess => IntPtr.Size == 8;
	} // class PpsShell

	#endregion
}
