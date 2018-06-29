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
using System.IO;
using System.Net;
using System.Reflection;
using System.Xml.Linq;
using TecWare.DE.Stuff;

namespace TecWare.PPSn
{
	#region -- class PpsEnvironmentInfo -----------------------------------------------

	/// <summary>Class for the local environment information.</summary>
	public sealed class PpsEnvironmentInfo : IEquatable<PpsEnvironmentInfo>
	{
		private const string infoFileId = "info.xml";

		private readonly string name;
		private XDocument content;
		private Uri remoteUri = null;
		private bool isModified = false;

		private readonly DirectoryInfo localPath;
		private readonly FileInfo infoFile;

		/// <summary>Opens a local environment information.</summary>
		/// <param name="name"></param>
		public PpsEnvironmentInfo(string name)
		{
			this.name = name;

			this.localPath = new DirectoryInfo(Path.GetFullPath(Path.Combine(localEnvironmentsPath, name)));
			if (!localPath.Exists)
				localPath.Create();

			this.infoFile = new FileInfo(Path.Combine(localPath.FullName, infoFileId));

			ReadInfoFile();
		} // ctor

		/// <summary>Check if this is same environment</summary>
		/// <param name="obj"></param>
		/// <returns></returns>
		public override bool Equals(object obj)
			=> Equals(obj as PpsEnvironmentInfo);

		/// <summary>Check if this is same environment</summary>
		/// <param name="other"></param>
		/// <returns></returns>
		public bool Equals(PpsEnvironmentInfo other)
		{
			if (ReferenceEquals(this, other))
				return true;
			else if (other is null)
				return false;
			else
				return localPath.FullName.Equals(other.LocalPath.FullName);
		} // func Equals

		/// <summary>Create a hashcode for the environment info.</summary>
		/// <returns></returns>
		public override int GetHashCode()
			=> localPath.FullName.GetHashCode();

		private void ReadInfoFile()
		{
			content =
				infoFile.Exists ?
					XDocument.Load(infoFile.FullName) :
					new XDocument(new XElement("ppsn"));
			isModified = false;
		} // proc

		/// <summary>Reread info file.</summary>
		public void Refresh()
			=> ReadInfoFile();

		/// <summary>Update the local environment info with tags from the server login.</summary>
		/// <param name="xNewInfo"></param>
		public void Update(XElement xNewInfo)
		{
			// copy uri
			Procs.MergeAttributes(content.Root, xNewInfo, ref isModified);
		} // proc UpdateInfoFile

		/// <summary>Save changes to the info file.</summary>
		public void Save()
		{
			content.Save(infoFile.FullName);
			isModified = false;
		} // proc Save
		
		/// <summary>Name of the instance.</summary>
		public string Name => name;
		/// <summary>Displayname of the instance for the user</summary>
		public string DisplayName { get { return content.Root.GetAttribute("displayName", name); } set { content.Root.SetAttributeValue("displayName", value); } }
		/// <summary>Uri of the server site.</summary>
		public Uri Uri
		{
			get
			{
				lock (content)
				{
					if (remoteUri == null)
					{
						var uriString = content.Root.GetAttribute("uri", null);
						if (String.IsNullOrEmpty(uriString))
							throw new ArgumentNullException("@uri", "Attribute is missing.");
						if (!uriString.EndsWith("/"))
							uriString += "/";

						remoteUri = new Uri(uriString, UriKind.Absolute);
					}

					return remoteUri;
				}
			}
			set
			{
				lock (content)
				{
					content.Root.SetAttributeValue("uri", value.ToString());
					remoteUri = null;
					isModified = true;
				}
			}
		} // prop Uri

		/// <summary>Information modified.</summary>
		public bool IsModified => isModified;
		/// <summary>Version of the server</summary>
		public Version Version { get { return new Version(content.Root.GetAttribute("version", "0.0.0.0")); } set { content.Root.SetAttributeValue("version", value.ToString()); } }

		/// <summary>Local store for the user data of the instance.</summary>
		public DirectoryInfo LocalPath => localPath;

		/// <summary>Check application version.</summary>
		public bool IsApplicationLatest => Version  <= AppVersion;

		// -- static --------------------------------------------------------------

		private static string localEnvironmentsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ppsn", "env");
		private static Lazy<Version> appVersion;

		static PpsEnvironmentInfo()
		{
			appVersion = new Lazy<Version>(GetAppVersion);
		} // sctor

		private static Version GetAppVersion()
		{
			var versionString = typeof(PpsEnvironmentInfo).Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
			return String.IsNullOrEmpty(versionString) ?
				new Version() : 
				new Version(versionString);

		} // func GetAppVersion

		/// <summary></summary>
		/// <param name="a"></param>
		/// <param name="b"></param>
		/// <returns></returns>
		public static bool operator ==(PpsEnvironmentInfo a, PpsEnvironmentInfo b)
			=> a is null && b is null 
			|| !(a is null) && a.Equals(b);

		/// <summary></summary>
		/// <param name="a"></param>
		/// <param name="b"></param>
		/// <returns></returns>
		public static bool operator !=(PpsEnvironmentInfo a, PpsEnvironmentInfo b)
			=> a is null && !(b is null)
			|| !(a is null) && !a.Equals(b);

		/// <summary>Create a new environment information.</summary>
		/// <param name="serverName"></param>
		/// <param name="serverUri"></param>
		/// <returns></returns>
		public static PpsEnvironmentInfo CreateEnvironment(string serverName, Uri serverUri)
		{
			var info = new PpsEnvironmentInfo(serverName);
			if (info.Uri == null) // update server uri
				info.Uri = serverUri;
			return info;
		} // func CreateEnvironment

		/// <summary>Enumerates the local environments</summary>
		/// <returns></returns>
		public static IEnumerable<PpsEnvironmentInfo> GetLocalEnvironments()
		{
			var localEnvironmentsDirectory = new DirectoryInfo(localEnvironmentsPath);
			if (localEnvironmentsDirectory.Exists)
			{
				foreach (var cur in localEnvironmentsDirectory.EnumerateDirectories())
				{
					var localEnvironment = (PpsEnvironmentInfo)null;
					try
					{
						localEnvironment = new PpsEnvironmentInfo(cur.Name);
					}
					catch (Exception e)
					{
						Debug.Print(e.ToString());
					}
					if (localEnvironment != null && !String.IsNullOrWhiteSpace(localEnvironment.content.Root.GetAttribute("uri", null)))
						yield return localEnvironment;
				}
			}
		} // func GetLocalEnvironments

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

		/// <summary>Application version.</summary>
		public static Version AppVersion => appVersion.Value;
		/// <summary>Returns the local environment path</summary>
		public static string LocalEnvironmentsPath => localEnvironmentsPath;
	} // class PpsEnvironmentInfo

	#endregion
}
