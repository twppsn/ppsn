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
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using TecWare.DE.Stuff;

namespace TecWare.PPSn
{
	#region -- class PpsEnvironmentInfo -------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Class for the local environment information.</summary>
	public sealed class PpsEnvironmentInfo
	{
		private readonly string name;
		private XDocument content;

		private readonly DirectoryInfo localPath;
		private readonly FileInfo infoFile;

		/// <summary>Opens a local environment information.</summary>
		/// <param name="name"></param>
		public PpsEnvironmentInfo(string name)
		{
			this.name = name;

			this.localPath = new DirectoryInfo(Path.Combine(localEnvironmentsPath, name));
			if (!localPath.Exists)
				localPath.Create();

			this.infoFile = new FileInfo(Path.Combine(localPath.FullName, "info.xml"));

			ReadInfoFile();
		} // ctor

		private void ReadInfoFile()
		{
			if (infoFile.Exists)
				content = XDocument.Load(infoFile.FullName);
			else
				content = new XDocument(new XElement("ppsn"));
		} // proc

		/// <summary>Update the local environment info.</summary>
		/// <param name="xNewInfo"></param>
		public void Update(XElement xNewInfo)
		{
			// copy uri
			xNewInfo.SetAttributeValue("uri", Uri);

			if (!Procs.CompareNode(content.Root, xNewInfo))
			{
				content = new XDocument(xNewInfo);
				content.Save(infoFile.FullName);
			}
		} // proc UpdateInfoFile

		/// <summary>Name of the instance.</summary>
		public string Name => name;
		/// <summary>Displayname of the instance for the user</summary>
		public string DisplayName { get { return content.Root.GetAttribute("displayName", name); } set { content.Root.SetAttributeValue("displayName", value); } }
		/// <summary>Uri of the server site.</summary>
		public Uri Uri
		{
			get
			{
				var uri = content.Root.GetAttribute("uri", null);
				return uri == null ? null : new Uri(uri);
			}
			set { content.Root.SetAttributeValue("uri", value.ToString()); }
		} // prop Uri

		/// <summary>Version of the server</summary>
		public Version Version { get { return new Version(content.Root.GetAttribute("version", "0.0.0.0")); } set { content.Root.SetAttributeValue("version", value.ToString()); } }

		/// <summary>Local store for the user data of the instance.</summary>
		public DirectoryInfo LocalPath => localPath;

		// -- static --------------------------------------------------------------

		private static string localEnvironmentsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ppsn", "env");

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
					if (localEnvironment != null)
						yield return localEnvironment;
				}
			}
		} // func GetLocalEnvironments
	} // class PpsEnvironmentInfo

	#endregion
}
