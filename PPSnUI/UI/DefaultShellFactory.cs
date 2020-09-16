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
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using TecWare.DE.Data;
using TecWare.DE.Stuff;

namespace TecWare.PPSn.UI
{
	#region -- class FileShellInfo ----------------------------------------------------

	internal class FileShellInfo : ObservableObject, IPpsShellInfo
	{
		private const string infoFileId = "info.xml";

		private readonly string name;
		private string displayName;
		private Uri uri;
		private Version version;

		private readonly DirectoryInfo localPath;
		private readonly FileInfo infoFile;

		#region -- Ctor/Dtor ----------------------------------------------------------

		public FileShellInfo(string name)
		{
			this.name = name;

			localPath = new DirectoryInfo(Path.GetFullPath(Path.Combine(FileShellFactory.LocalEnvironmentsPath, name)));
			if (!localPath.Exists)
				localPath.Create();

			infoFile = new FileInfo(Path.Combine(localPath.FullName, infoFileId));

			ReadHeaderInfo();
		} // ctor

		private void ReadHeaderInfo()
		{
			using(var x = FileSettingsInfo.CreateReader(infoFile))
			{
				if (x == null)
					return;

				Procs.MoveToContent(x, XmlNodeType.Element);

				displayName = x.GetAttribute<string>("displayName", null);
				uri = PpsShell.GetUriFromString(x.GetAttribute<string>("uri", null), UriKind.Absolute, true);
				version = new Version(x.GetAttribute<string>("version", "1.0.0.0"));
			}
		} // proc ReadHeaderInfo

		/// <summary>Check if this is same environment</summary>
		/// <param name="obj"></param>
		/// <returns></returns>
		public override bool Equals(object obj)
			=> Equals(obj as FileShellInfo);

		bool IEquatable<IPpsShellInfo>.Equals(IPpsShellInfo other)
			=> Equals(other as FileShellInfo);

		/// <summary>Check if this is same environment</summary>
		/// <param name="other"></param>
		/// <returns></returns>
		public bool Equals(FileShellInfo other)
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

		#endregion

		public async Task<IPpsSettingsService> LoadSettingsAsync()
		{
			var settings = FileSettingsInfo.CreateInstanceSettings(this);
			await settings.LoadAsync();
			return settings;
		} // proc LoadSettingsAsync

		public string Name => name;
		public string DisplayName { get => displayName ?? name; set => Set(ref displayName, value, nameof(DisplayName)); }
		public Uri Uri { get => uri; set => Set(ref uri, value, nameof(Uri)); }
		public Version Version { get => version; set => Set(ref version, value, nameof(Version)); }
		public DirectoryInfo LocalPath => localPath;
		public FileInfo SettingsInfo => infoFile;

		/// <summary></summary>
		/// <param name="a"></param>
		/// <param name="b"></param>
		/// <returns></returns>
		public static bool operator ==(FileShellInfo a, FileShellInfo b)
			=> a is null && b is null || !(a is null) && a.Equals(b);

		/// <summary></summary>
		/// <param name="a"></param>
		/// <param name="b"></param>
		/// <returns></returns>
		public static bool operator !=(FileShellInfo a, FileShellInfo b)
			=> a is null && !(b is null) || !(a is null) && !a.Equals(b);
	} // class FileShellInfo

	#endregion

	#region -- class FileSettingsInfo -------------------------------------------------

	internal abstract class FileSettingsInfo : PpsBaseSettingsService, IPpsShellService
	{
		#region -- class InstanceSettingsInfo -----------------------------------------

		private sealed class InstanceSettingsInfo : FileSettingsInfo
		{
			private readonly FileShellInfo info;

			public InstanceSettingsInfo(FileShellInfo info)
				: base(info.SettingsInfo)
			{
				this.info = info ?? throw new ArgumentNullException(nameof(info));
			} // ctor

			protected override IEnumerable<KeyValuePair<string, string>> GetDefaultSettings()
			{
				yield return new KeyValuePair<string, string>("PPSn.Uri", info.Uri.ToString());
				yield return new KeyValuePair<string, string>("PPSn.Name", info.DisplayName);
			} // func GetDefaultSettings

			protected override void NotifyValueChanged(string attributeName, string value)
			{
				switch (attributeName)
				{
					case "uri":
						info.Uri = PpsShell.GetUriFromString(value, UriKind.Absolute, true);
						break;
					case "displayName":
						info.DisplayName = value;
						break;
					case "version":
						info.Version = new Version(value);
						break;
				}
			} // proc NotifyValueChanged

			protected override Task LoadSettingsFromServerAsync()
			{
				if (Shell == null || Shell.Http == null)
					throw new InvalidOperationException();

				return PpsShell.LoadSettingsFromServerAsync(this, Shell.Http);
			} // proc LoadSettingsFromServerAsync

			protected override IReadOnlyList<Tuple<XName, string>> TranslateProperties => translateInstanceProperties;
		} // class InstanceSettingsInfo

		#endregion

		#region -- class UserSettingsInfo ---------------------------------------------

		private sealed class UserSettingsInfo : FileSettingsInfo
		{
			private readonly string userName;

			public UserSettingsInfo(FileInfo fileInfo, string userName)
				: base(fileInfo)
			{
				this.userName = userName ?? throw new ArgumentNullException(nameof(userName));
			} // ctor

			protected override IEnumerable<KeyValuePair<string, string>> GetDefaultSettings()
			{
				yield return new KeyValuePair<string, string>("PPSn.User.Identity", userName);
			} // func GetDefaultSettings

			protected override Task LoadSettingsFromServerAsync()
			{
				if (Shell == null || Shell.Http == null)
					throw new InvalidOperationException();

				return PpsShell.LoadUserSettingsFromServerAsync(this, Shell.Http);
			} // proc LoadSettingsFromServerAsync

			protected override IReadOnlyList<Tuple<XName, string>> TranslateProperties => translateUserProperties;
		} // class UserFileSettingsInfo

		#endregion

		private readonly FileInfo fileInfo;
		private DateTime lastReaded = DateTime.MinValue;

		#region -- Ctor/Dtor ----------------------------------------------------------

		protected FileSettingsInfo(FileInfo fileInfo)
		{
			// todo: FileSystemWatcher
			this.fileInfo = fileInfo ?? throw new ArgumentNullException(nameof(fileInfo));
		} // ctor

		internal static XmlReader CreateReader(FileInfo fi)
			=> fi.Exists ? XmlReader.Create(fi.FullName, Procs.XmlReaderSettings) : null;

		private XmlReader CreateReader()
			=> CreateReader(fileInfo);

		private XmlWriter CreateWriter()
			=> XmlWriter.Create(fileInfo.FullName, Procs.XmlWriterSettings);

		#endregion

		#region -- Read/Write Settings ------------------------------------------------

		#region -- class SettingsWriter -----------------------------------------------

		private sealed class SettingsWriter : IPpsSettingsWriter
		{
			private readonly FileSettingsInfo settings;
			private readonly XElement xRoot;

			public SettingsWriter(FileSettingsInfo settings)
			{
				this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
				
				xRoot = new XElement("ppsn");
			} // ctor

			private static bool IsElementValue(string value)
				=> value.IndexOfAny(new char[] { '\n', '\r' }) >= 0;

			private static XElement EnforceElement(XElement xCur, string localName)
			{
				var x = xCur.Element(localName);
				if (x == null)
					xCur.Add(x = new XElement(localName));
				return x;
			} // func EnforceElement

			private static void SetTextValue(XElement x, string value)
			{
				var n = x.FirstNode;
				while (n != null)
				{
					if (n is XCData d)
					{
						d.Value = value;
						return;
					}
					n = n.NextNode;
				}
				x.AddFirst(new XCData(value));
			} // proc SetTextValue

			public bool? WritePair(string name, string value, bool isDirty)
			{
				if (String.IsNullOrEmpty(value))
					return null;

				var xCur = xRoot;

				var attributeName = TranslateSettingToAttributeName(name, settings.TranslateProperties);
				if (attributeName != null)
				{
					settings.NotifyValueChanged(attributeName.LocalName, value);
					xRoot.SetAttributeValue(attributeName, value);
				}
				else
				{
					var nameParts = name.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);

					for (var i = 0; i < nameParts.Length - 1; i++)
					{
						var n = nameParts[i];
						var localName = Int32.TryParse(n, out var idx) ? "i" + idx.ToString() : n;

						xCur = EnforceElement(xCur, localName);
					}

					if (IsElementValue(value))
						SetTextValue(EnforceElement(xCur, nameParts[nameParts.Length - 1]), value);
					else
						xCur.SetAttributeValue(nameParts[nameParts.Length - 1], value);
				}
				
				return isDirty;
			} // proc WritePair

			public async Task FlushAsync()
			{
				await Task.Run(() =>
				{
					var xDoc = new XDocument(xRoot);
					using (var xml = settings.CreateWriter())
						xDoc.WriteTo(xml);
				});
				settings.lastReaded = DateTime.Now;
			} // func FlushAsync
		} // class SettingsWriter

		#endregion

		protected virtual void NotifyValueChanged(string attributeName, string value) { }

		/// <summary>Read all settings from disk.</summary>
		/// <returns></returns>
		public Task LoadAsync()
			=> ReadSettingsAsync();
		
		protected override IPpsSettingsWriter CreateSettingsWriter()
			=> new SettingsWriter(this);

		private static IEnumerable<KeyValuePair<string, string>> ParseSettings(XmlReader xml, Func<XName, string> translateProperty)
		{
			if (xml == null)
				throw new ArgumentNullException(nameof(xml));

			var currentPath = new List<string>();

			string GetBasePathName(bool trailingDot)
			{
				var pn = String.Join(".", currentPath);
				if (trailingDot && pn.Length > 0)
					pn += ".";
				return pn;
			} // func GetBasePathName

			var first = true;
			while (!xml.EOF)
			{
				if (xml.NodeType == XmlNodeType.Element)
				{
					// create path name
					string pathName = null;

					if (first)
						first = false;
					else
					{
						var n = xml.LocalName;
						if (n[0] == 'i' && Int32.TryParse(n.Substring(1), out var idx))
							currentPath.Add(idx.ToString());
						else
							currentPath.Add(n);
					}

					// emit attributes
					foreach (var x in xml.EnumerateAttributes())
					{
						if (pathName == null)
							pathName = GetBasePathName(true);

						var settingName = translateProperty(xml.LocalName) ?? pathName + x.Name.LocalName;
						yield return new KeyValuePair<string, string>(settingName, x.Value);
					}

					// next element
					if (xml.IsEmptyElement && currentPath.Count > 0)
						currentPath.RemoveAt(currentPath.Count - 1); // pop current name as path
					xml.Read();
				}
				else if (xml.NodeType == XmlNodeType.EndElement)
				{
					xml.ReadEndElement();

					if (currentPath.Count > 0) // go level down
						currentPath.RemoveAt(currentPath.Count - 1);
					else
						break;
				}
				else if (xml.NodeType == XmlNodeType.CDATA)
				{
					yield return new KeyValuePair<string, string>(GetBasePathName(false), xml.Value);
					xml.Read();
				}
				else if (xml.NodeType == XmlNodeType.Text)
				{
					yield return new KeyValuePair<string, string>(GetBasePathName(false), Encoding.UTF8.GetString(Convert.FromBase64String(xml.Value)));
					xml.Read();
				}
				else
					xml.Read();
			}
		} // proc ParseSettings

		protected virtual IEnumerable<KeyValuePair<string, string>> GetDefaultSettings()
			=> Array.Empty<KeyValuePair<string, string>>();

		protected override IEnumerable<KeyValuePair<string, string>> ParseSettings()
		{
			using (var xml = CreateReader())
			{
				if (xml == null)
				{
					foreach (var kv in GetDefaultSettings())
						yield return kv;
				}
				else
				{
					foreach (var kv in ParseSettings(xml, a => TranslateAttributeToSettingName(a, TranslateProperties)))
						yield return kv;
				}
			}

			lastReaded = DateTime.Now;
		} // func ParseSettings

		protected abstract IReadOnlyList<Tuple<XName, string>> TranslateProperties { get; }

		#endregion

		public IPpsShell Shell { get; set; } = null;

		public DateTime LastReaded => lastReaded;

		private static readonly Tuple<XName, string>[] translateInstanceProperties = new Tuple<XName, string>[]
		{
			new Tuple<XName, string>("uri", "PPSn.Uri"),
			new Tuple<XName, string>("displayName", "PPSn.Name"),
			new Tuple<XName, string>("loginSecurity", "PPSn.Security"),
			new Tuple<XName, string>("version", "PPSn.Version")
		};

		private static readonly Tuple<XName, string>[] translateUserProperties = new Tuple<XName, string>[]
		{
			new Tuple<XName, string>("userId", "PPSn.User.Id"),
			new Tuple<XName, string>("displayName", "PPSn.User.DisplayName"),
			new Tuple<XName, string>("FullName", "PPSn.User.Name")
		};

		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public static IEnumerable<KeyValuePair<string, string>> ParseInstanceSettings(XmlReader xml)
			=> ParseSettings(xml, a => TranslateAttributeToSettingName(a, translateInstanceProperties));

		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public static IEnumerable<KeyValuePair<string, string>> ParseUserSettings(XmlReader xml)
			=> ParseSettings(xml, a => TranslateAttributeToSettingName(a, translateUserProperties));
		
		private static string TranslateAttributeToSettingName(XName attributeName, IEnumerable<Tuple<XName, string>> translateProperties)
			=> (from cur in translateProperties where cur.Item1.LocalName == attributeName.LocalName select cur.Item2).FirstOrDefault();

		private static XName TranslateSettingToAttributeName(string settingName, IEnumerable<Tuple<XName, string>> translateProperties)
			=> (from cur in translateProperties where cur.Item2 == settingName select cur.Item1).FirstOrDefault();

		/// <summary>Opens a local environment information.</summary>
		/// <param name="info"></param>
		public static FileSettingsInfo CreateInstanceSettings(FileShellInfo info)
			=> new InstanceSettingsInfo(info);

		/// <summary>Opens a local user information.</summary>
		/// <param name="userName"></param>
		/// <param name="userSettingInfo"></param>
		public static FileSettingsInfo CreateUserSettings(FileInfo userSettingInfo, string userName)
			=> new UserSettingsInfo(userSettingInfo, userName);
	} // class FileSettingsInfo

	#endregion

	#region -- class FileShellFactory -------------------------------------------------

	[PpsService(typeof(IPpsShellFactory))]
	internal sealed class FileShellFactory : IPpsShellFactory
	{
		private static readonly string localEnvironmentsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ppsn", "env");

		/// <summary>Create a new shell information.</summary>
		/// <param name="instanceName"></param>
		/// <param name="uri"></param>
		/// <returns></returns>
		public IPpsShellInfo CreateNew(string instanceName, string displayName, Uri uri)
		{
			return new FileShellInfo(instanceName)
			{
				DisplayName = displayName,
				Uri = uri
			};
		} // func CreateEnvironment

		public IEnumerator<IPpsShellInfo> GetEnumerator()
		{
			var localEnvironmentsDirectory = new DirectoryInfo(localEnvironmentsPath);
			if (localEnvironmentsDirectory.Exists)
			{
				foreach (var cur in localEnvironmentsDirectory.EnumerateDirectories())
				{
					var localShell = (FileShellInfo)null;
					try
					{
						localShell = new FileShellInfo(cur.Name);
					}
					catch (Exception e)
					{
						Debug.Print(e.ToString());
					}
					if (localShell != null && localShell.Uri != null)
						yield return localShell;
				}
			}
		} // func GetEnumerator
		
		IEnumerator IEnumerable.GetEnumerator()
			=> GetEnumerator();

		/// <summary>Returns the local environment path</summary>
		public static string LocalEnvironmentsPath => localEnvironmentsPath;
	} // class FileShellFactory

	#endregion
}
