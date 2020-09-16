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
using System.Net;
using System.Reflection;

namespace TecWare.PPSn
{
	public static partial class PpsShell
	{
		#region -- GetUriFromString ---------------------------------------------------

		/// <summary>Create a uri from an string.</summary>
		/// <param name="uri"></param>
		/// <param name="kind"></param>
		/// <param name="addTrailingSlash"></param>
		/// <returns><c>null</c>, if the uri was invalid.</returns>
		public static Uri GetUriFromString(string uri, UriKind kind, bool addTrailingSlash = false)
		{
			if (String.IsNullOrEmpty(uri))
				return null;

			if (addTrailingSlash && uri[uri.Length - 1] != '/')
				uri += "/";

			return Uri.TryCreate(uri, kind, out var result) ? result : null;
		} // func GetUriFromString

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
}
