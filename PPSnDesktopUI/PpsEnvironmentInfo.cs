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
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using TecWare.DE.Stuff;

namespace TecWare.PPSn
{
	#region -- class PpsEnvironmentInfo -------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsEnvironmentInfo : IEquatable<PpsEnvironmentInfo>
	{
		private const string UserNameId = "userName";
		private const string LoginTimeId = "loginTime";
		private const string LoginElementId = "recentUser";
		private const string LoginId = "login";
		private const string AuthTypeId = "authType";
		private const string InfoFileId = "info.xml";

		private readonly string name;
		private XDocument content;

		private readonly DirectoryInfo localPath;
		private readonly FileInfo infoFile;

		public PpsEnvironmentInfo(string name)
		{
			this.name = name;

			this.localPath = new DirectoryInfo(Path.GetFullPath(Path.Combine(localEnvironmentsPath, name)));
			if (!localPath.Exists)
				localPath.Create();

			this.infoFile = new FileInfo(Path.Combine(localPath.FullName, InfoFileId));

			ReadInfoFile();
		} // ctor

		public override bool Equals(object obj)
			=> Equals(obj as PpsEnvironmentInfo);

		public bool Equals(PpsEnvironmentInfo other)
		{
			if (Object.ReferenceEquals(this, other))
				return true;
			else if (Object.ReferenceEquals(other, null))
				return false;
			else
				return localPath.FullName.Equals(other.LocalPath.FullName);
		} // func Equals

		public override int GetHashCode()
			=> localPath.FullName.GetHashCode();

		private void ReadInfoFile()
		{
			content =
				infoFile.Exists ?
					XDocument.Load(infoFile.FullName) :
					new XDocument(new XElement("ppsn"));
		} // proc

		private IEnumerable<XElement> RecentUsersInternal => content.Root.Element(LoginId)?.Elements(LoginElementId) ?? Array.Empty<XElement>();

		public string AuthType => content.Root.Element(LoginId)?.GetAttribute(AuthTypeId, String.Empty) ?? String.Empty;

		public string LastUser
		{
			get
			{
				return (from x in RecentUsersInternal
						  let userName = x.GetAttribute(UserNameId, String.Empty)
						  let lastLogin = x.GetAttribute(LoginTimeId, DateTime.MinValue)
						  where !String.IsNullOrEmpty(userName)
						  orderby lastLogin
						  select userName
						).FirstOrDefault() ?? String.Empty;
			}
		}

		public void WriteLastUser(string userName)
		{
			if (String.IsNullOrEmpty(userName))
				return;

			// remove current reference
			RecentUsersInternal
				.FirstOrDefault(x => String.Compare(x.GetAttribute(UserNameId, String.Empty), userName, StringComparison.OrdinalIgnoreCase) == 0)
				?.Remove();

			var xLogin = content.Root.Element(LoginId);
			if (xLogin == null)
				content.Root.Add(xLogin = new XElement(LoginId));

			xLogin.AddFirst(new XElement(LoginElementId,
				new XAttribute(UserNameId, userName),
				new XAttribute(LoginTimeId, DateTime.UtcNow.ChangeType<string>())
			));

			// save to persistent setting
			content.Save(infoFile.FullName);
		}

		public void Update(XElement xNewInfo)
		{
			// copy uri
			var isChanged = false;
			Procs.MergeAttributes(content.Root, xNewInfo, ref isChanged);
		} // proc UpdateInfoFile

		public void Save()
		{
		} // proc Save

		public IEnumerable<string> RecentUsers
			=> from x in RecentUsersInternal
				let userName = x.GetAttribute(UserNameId, String.Empty)
				where !String.IsNullOrEmpty(userName)
				select userName;

		public string Name => name;

		public string DisplayName { get { return content.Root.GetAttribute("displayName", name); } set { content.Root.SetAttributeValue("displayName", value); } }

		public Uri Uri
		{
			get
			{
				var uri = content.Root.GetAttribute("uri", null);
				return uri == null ? null : new Uri(uri);
			}
			set { content.Root.SetAttributeValue("uri", value.ToString());
			}
		} // prop Uri

		public Version Version { get { return new Version(content.Root.GetAttribute("version", "0.0.0.0")); } set { content.Root.SetAttributeValue("version", value.ToString()); } }

		public DirectoryInfo LocalPath => localPath;

		public bool IsApplicationLatest { get; internal set; }

		// -- static --------------------------------------------------------------

		private static string localEnvironmentsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ppsn", "env");

		public static bool operator ==(PpsEnvironmentInfo a, PpsEnvironmentInfo b)
			=> !Object.ReferenceEquals(a, null) && a.Equals(b);

		public static bool operator !=(PpsEnvironmentInfo a, PpsEnvironmentInfo b)
			=> Object.ReferenceEquals(a, null) || !a.Equals(b);

		public static PpsEnvironmentInfo CreateEnvironment(string serverName, Uri serverUri)
		{
			var info = new PpsEnvironmentInfo(serverName);
			if (info.Uri == null) // update server uri
				info.Uri = serverUri;
			return info;
		} // func CreateEnvironment

		public static IEnumerable<PpsEnvironmentInfo> GetLocalEnvironments()
		{
			var localEnvironmentsDirectory = new DirectoryInfo(localEnvironmentsPath);
			if (localEnvironmentsDirectory.Exists)
			{
				foreach (var cur in localEnvironmentsDirectory.EnumerateDirectories())
				{
					PpsEnvironmentInfo localEnvironment = null;
					try
					{
						localEnvironment = new PpsEnvironmentInfo(cur.Name);
					}
					catch (Exception e)
					{
						Debug.Print(e.ToString());
					}
					if (localEnvironment != null)
						yield return localEnvironment;
				}
			}
		} // func GetLocalEnvironments

		private static string GetDomainUserName(string domain, string userName)
				=> String.IsNullOrEmpty(domain) ? userName : domain + "\\" + userName;

		public static string GetUserNameFromCredentials(ICredentials userInfo)
		{
			if (userInfo == null)
				return null;
			else if (CredentialCache.DefaultCredentials == userInfo)
				return GetDomainUserName(Environment.UserDomainName, Environment.UserName);
			else
			{
				var networkCredential = userInfo as NetworkCredential;
				if (networkCredential != null)
					return GetDomainUserName(networkCredential.Domain, networkCredential.UserName);
				else
					throw new ArgumentOutOfRangeException("Invalid userInfo.");
			} // func GetUserNameFromCredentials
		} // func GetUserNameFromCredentials
	} // class PpsEnvironmentInfo

	#endregion
}
