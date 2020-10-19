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
		public static string GetUserNameFromCredentials(ICredentials userInfo)
		{
			if (userInfo == null)
				return null;
			else if (CredentialCache.DefaultCredentials == userInfo)
				return GetDomainUserName(Environment.UserDomainName, Environment.UserName);
			else if (userInfo is NetworkCredential networkCredential)
				return GetDomainUserName(networkCredential.Domain, networkCredential.UserName);
			else
				throw new ArgumentOutOfRangeException("Invalid userInfo.");
		} // func GetUserNameFromCredentials

		#endregion

		#region -- Logger -------------------------------------------------------------

		#region -- class PpsDummyLogger -----------------------------------------------

		private sealed class PpsDummyLogger : IPpsLogger
		{
			public void Append(PpsLogType type, string message)
				=> Debug.WriteLine("[" + type.ToString() + "] " + message);

			public void Append(PpsLogType type, Exception exception, string alternativeException)
			{
				Append(type,
					String.IsNullOrEmpty(alternativeException)
					? exception.GetInnerException().ToString()
					: alternativeException + Environment.NewLine + exception.GetInnerException().ToString()
				);
			} // proc Append
		} // class PpsDummyLogger

		#endregion

		/// <summary>Get the log-interface.</summary>
		/// <param name="sp"></param>
		/// <returns></returns>
		public static IPpsLogger GetLogger(this IServiceProvider sp)
			=> sp.GetService<IPpsLogger>(false) ?? DebugLogger;

		/// <summary>Logger that prints to the debug output.</summary>
		public static IPpsLogger DebugLogger { get; } = new PpsDummyLogger();

		#endregion

		#region -- Idle ---------------------------------------------------------------

		#region -- class FunctionIdleActionImplementation -----------------------------

		private sealed class FunctionIdleActionImplementation : IPpsIdleAction
		{
			private readonly Func<int, bool> onIdle;

			public FunctionIdleActionImplementation(Func<int, bool> onIdle)
			{
				this.onIdle = onIdle ?? throw new ArgumentNullException(nameof(onIdle));
			} // ctor

			public bool OnIdle(int elapsed)
				=> onIdle(elapsed);
		} // class FunctionIdleActionImplementation

		#endregion

		/// <summary>Add a idle action.</summary>
		/// <param name="idleService"></param>
		/// <param name="onIdle"></param>
		/// <returns></returns>
		public static IPpsIdleAction AddIdleFunction(this IPpsIdleService idleService, Func<int, bool> onIdle)
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
	} // class PpsShell

	#endregion

	#region -- class PpsApplicationRestartNeededException -----------------------------

	/// <summary>Exception to initiate a restart.</summary>
	public sealed class PpsApplicationRestartNeededException : Exception
	{
		/// <summary>Exception to initiate a restart.</summary>
		public PpsApplicationRestartNeededException()
			: base("Application need to restart.")
		{
		} // cto
	} // class PpsApplicationRestartNeededException

	#endregion
}
