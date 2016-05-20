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
	/// <summary></summary>
	public sealed class PpsEnvironmentInfo
	{
		private readonly string name;
		private XDocument content;

		private readonly DirectoryInfo localPath;
		private readonly FileInfo infoFile;

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

		public void UpdateInfoFile()
		{
			content.Save(infoFile.FullName);
		} // proc UpdateInfoFile

		public string Name => name;

		public string DisplayName { get { return content.Root.GetAttribute("displayName", name); } set { content.Root.SetAttributeValue("displayName", value); } }
		public Uri Uri
		{
			get
			{
				var uri = content.Root.GetAttribute("uri", null);
				return uri == null ? null : new Uri(uri);
			}
			set { content.Root.SetAttributeValue("uri", value.ToString()); }
		} // prop Uri

		public Version Version { get { return new Version(content.Root.GetAttribute("version", "0.0.0.0")); } set { content.Root.SetAttributeValue("version", value.ToString()); } }

		public DirectoryInfo LocalPath => localPath;

		// -- static --------------------------------------------------------------

		private static string localEnvironmentsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ppsn", "env");

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
	} // class PpsEnvironmentInfo

	#endregion
}
