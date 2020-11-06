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
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
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
		private string uriQuery;
		private NameValueCollection query = null;
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
			using (var x = FileSettingsInfo.CreateReaderAsync(infoFile).Await())
			{
				if (x == null)
					return;

				Procs.MoveToContent(x, XmlNodeType.Element);
				PpsShell.GetUriParts(x.GetAttribute<string>("uri", null), UriKind.Absolute, out var uriPath, out uriQuery);

				displayName = x.GetAttribute<string>("displayName", null);
				uri = new Uri(uriPath, UriKind.Absolute);
				version = new Version(x.GetAttribute<string>("version", "1.0.0.0"));
				query = HttpUtility.ParseQueryString(uriQuery, Encoding.UTF8);
			}
		} // proc ReadHeaderInfo

		internal void WriteHeaderInfo()
		{
			if (!infoFile.Exists)
			{
				using (var x = FileSettingsInfo.CreateWriterUnsafe(infoFile))
				{
					x.WriteStartDocument();
					x.WriteStartElement("ppsn");
					x.WriteAttributeString("uri", FullUri);
					if (!String.IsNullOrEmpty(displayName))
						x.WriteAttributeString("displayName", displayName);
					if (version != null)
						x.WriteAttributeString("version", version.ToString());
					x.WriteEndElement();
					x.WriteEndDocument();
				}
			}
		} // proc WriteHeaderInfo

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

		public async Task<IPpsSettingsService> LoadSettingsAsync(IPpsShell shell)
		{
			var settings = FileSettingsInfo.CreateInstanceSettings(shell, this);
			await settings.LoadAsync();
			return settings;
		} // proc LoadSettingsAsync

		public bool TryGetProperty(string name, out object value)
		{
			value = query.Get(name);
			return value != null;
		} // func TryGetProperty

		public string Name => name;
		public string DisplayName { get => displayName ?? name; set => Set(ref displayName, value, nameof(DisplayName)); }
		public Uri Uri
		{
			get => uri;
			set
			{
				PpsShell.GetUriParts(value, out var uriPath, out var uriQuery);
				Set(ref uri, new Uri(uriPath, UriKind.Absolute), nameof(Uri));
				if (Set(ref this.uriQuery, uriQuery, nameof(Query)))
					query = HttpUtility.ParseQueryString(uriQuery, Encoding.UTF8);
			}
		} // prop Uri

		public string FullUri => uri.ToString() + "?" + query;
		public string Query => uriQuery;

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
		#region -- interface IXSettingKey ---------------------------------------------

		private interface IXSettingKey : IComparable<IXSettingKey>
		{
			string Key { get; }
			XSettingValue Setting { get; }
		} // interface IXSettingKey

		#endregion

		#region -- class XSettingKey --------------------------------------------------

		private sealed class XSettingKey : IXSettingKey
		{
			private readonly string key;

			public XSettingKey(string key)
				=> this.key = key ?? throw new ArgumentNullException(nameof(key));

			int IComparable<IXSettingKey>.CompareTo(IXSettingKey other)
				=> key.CompareTo(other.Key);

			public string Key => key;
			public XSettingValue Setting => null;
		} // class XSettingKey

		#endregion

		#region -- class XSettingValue ------------------------------------------------

		[DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
		private class XSettingValue : SettingBase, IXSettingKey
		{
			private bool isTouched = true;
			private string value;

			public XSettingValue(string key, string value)
				: base(key)
			{
				this.value = value;
			} // ctor

			private string GetDebuggerDisplay()
				=> $"Setting: {Key} = {value}";

			public bool ResetTouched()
			{
				var r = isTouched;
				isTouched = false;
				return r;
			} // func ResetTouched

			protected sealed override string GetValueCore()
				=> value;

			protected sealed override bool SetValueCore(string value)
			{
				isTouched = true;

				if (this.value != value)
				{
					this.value = value;
					return true;
				}
				else
					return false;
			} // proc SetValueCore

			int IComparable<IXSettingKey>.CompareTo(IXSettingKey other)
				=> Key.CompareTo(other.Key);

			XSettingValue IXSettingKey.Setting => this;

			public bool IsTouched => isTouched;
		} // class XSettingValue

		#endregion

		#region -- class KnownSettingValue --------------------------------------------

		private class KnownSettingValue : XSettingValue
		{
			private readonly bool isHidden;
			private readonly bool clearAble;

			public KnownSettingValue(string key, string value, bool isHidden = false, bool clearAble = true)
				: base(key, value)
			{
				this.isHidden = isHidden;
				this.clearAble = clearAble;
			} // ctor

			protected override bool ClearCore()
				=> clearAble && base.ClearCore();

			public sealed override bool IsHidden => isHidden;
		} // class KnownSettingValue

		#endregion

		#region -- class InstanceSettingsInfo -----------------------------------------

		private sealed class InstanceSettingsInfo : FileSettingsInfo
		{
			private readonly FileShellInfo info;
			private readonly Lazy<string> deviceId;

			public InstanceSettingsInfo(IPpsShell shell, FileShellInfo info)
				: base(shell, info.SettingsInfo)
			{
				this.info = info ?? throw new ArgumentNullException(nameof(info));

				deviceId = new Lazy<string>(GetDeviceId);

				RegisterKnownReadOnlySettingValue(PpsShellSettings.DpcDeviceIdKey, () => deviceId.Value);

				RegisterKnownSettingValue(new KnownSettingValue(PpsShellSettings.PpsnUriKey, info.FullUri, isHidden: false, clearAble: false));
				RegisterKnownSettingValue(new KnownSettingValue(PpsShellSettings.PpsnNameKey, info.DisplayName, isHidden: false, clearAble: false));
			} // ctor

			private string GetDeviceId()
			{
				if (info.TryGetProperty<string>("id", out var deviceId) && !String.IsNullOrEmpty(deviceId))
				{
					if (deviceId.IndexOf('*') >= 0)
						deviceId = deviceId.Replace("*", Environment.MachineName);

					return deviceId.ToLower();
				}
				else
					return Environment.MachineName.ToLower();
			} // func GetDeviceId

			protected override void OnSettingChanged(string key, string value)
			{
				switch (key)
				{
					case PpsShellSettings.PpsnUriKey:
						info.Uri = new Uri(value, UriKind.Absolute);
						break;
					case PpsShellSettings.PpsnNameKey:
						info.DisplayName = value;
						break;
				}
				base.OnSettingChanged(key, value);
			} // proc OnSettingChanged

			protected override Task LoadSettingsFromServerAsync()
			{
				if (shell == null || shell.Http == null)
					throw new InvalidOperationException();

				return PpsShell.LoadSettingsFromServerAsync(this, shell, deviceId.Value, 0);
			} // proc LoadSettingsFromServerAsync

			protected override IReadOnlyList<Tuple<XName, string>> TranslateProperties => translateInstanceProperties;
		} // class InstanceSettingsInfo

		#endregion

		#region -- class UserSettingsInfo ---------------------------------------------

		private sealed class UserSettingsInfo : FileSettingsInfo
		{
			public UserSettingsInfo(IPpsShell shell, FileInfo fileInfo, string userName)
				: base(shell, fileInfo)
			{
				RegisterKnownSettingValue(new KnownSettingValue(PpsShellUserSettings.UserIdentiyKey, userName, clearAble: false));
			} // ctor

			protected override Task LoadSettingsFromServerAsync()
			{
				if (Shell == null || shell.Http == null)
					throw new InvalidOperationException();

				return PpsShell.LoadUserSettingsFromServerAsync(this, shell.Http);
			} // proc LoadSettingsFromServerAsync

			protected override IReadOnlyList<Tuple<XName, string>> TranslateProperties => translateUserProperties;
		} // class UserFileSettingsInfo

		#endregion

		#region -- class UpdateScope --------------------------------------------------

		private sealed class UpdateScope : IUpdateScope
		{
			private readonly FileSettingsInfo settingsInfo;

			public UpdateScope(FileSettingsInfo settingsInfo)
			{
				this.settingsInfo = settingsInfo ?? throw new ArgumentNullException(nameof(settingsInfo));
			} // ctor

			void IDisposable.Dispose() { }

			Task IUpdateScope.CommitAsync()
				=> settingsInfo.WriteAsync();
		} // class UpdateScope

		#endregion

		private readonly IPpsShell shell;
		private readonly FileInfo fileInfo;
		private DateTime lastReaded = DateTime.MinValue;
		private FileSystemWatcher fileSystemWatcher = null;

		private bool isDirty = false;
		private readonly List<IXSettingKey> settings = new List<IXSettingKey>();

		#region -- Ctor/Dtor ----------------------------------------------------------

		protected FileSettingsInfo(IPpsShell shell, FileInfo fileInfo)
		{
			this.shell = shell ?? throw new ArgumentNullException(nameof(shell));
			this.fileInfo = fileInfo ?? throw new ArgumentNullException(nameof(fileInfo));
		} // ctor

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				fileSystemWatcher.Dispose();
				fileSystemWatcher = null;
			}
			base.Dispose(disposing);
		} // proc Dispose

		#endregion

		#region -- Read/Write primitives ----------------------------------------------

		private static async Task<FileStream> OpenFileSafeAsync(FileInfo fi, bool forRead)
		{
			var sw = Stopwatch.StartNew();
			while (true)
			{
				try
				{
					return new FileStream(fi.FullName,
						forRead ? FileMode.Open : FileMode.OpenOrCreate,
						forRead ? FileAccess.Read : FileAccess.ReadWrite,
						forRead ? FileShare.Read : FileShare.None
					);
				}
				catch (IOException e)
				{
					var code = e.HResult & 0xFFFF;
					if (code == 32 || code == 33)
					{
						if (sw.ElapsedMilliseconds > 10000)
							throw new TimeoutException($"Timeout to lock file: {fi.Name}");
						await Task.Delay(100);
					}
					else
						throw;
				}
			}
		} // func OpenFileSafe

		private static XmlReader CreateReader(FileStream src)
			=> XmlReader.Create(src, Procs.XmlReaderSettings);

		internal static async Task<XmlReader> CreateReaderAsync(FileInfo fi)
			=> fi.Exists ? XmlReader.Create(await OpenFileSafeAsync(fi, true), Procs.XmlReaderSettings) : null;

		internal static XmlWriter CreateWriterUnsafe(FileInfo fi)
		{
			// check directory exists
			if (!fi.Directory.Exists)
				fi.Directory.Create();

			// create unsafe writer
			return XmlWriter.Create(new FileStream(fi.FullName, FileMode.CreateNew), Procs.XmlWriterSettings);
		} // func CreateWriter

		private async Task<(FileStream, XmlWriter)> CreateWriterAsync()
		{
			// check directory exists
			if (!fileInfo.Directory.Exists)
				fileInfo.Directory.Create();
			VerifyFileSystemWatcher();

			// open file locked
			var infoStream = await OpenFileSafeAsync(fileInfo, false);
			try
			{
				// check for changes
				await LoadCoreAsync(infoStream, false);

				infoStream.Position = 0; // reset file pointer
				lastReaded = DateTime.MaxValue; // do not change content during write

				// create xml-write
				return (infoStream, XmlWriter.Create(infoStream, Procs.XmlWriterSettings));
			}
			catch
			{
				infoStream.Dispose();
				throw;
			}
		} // func CreateWriter

		#endregion

		#region -- File Watcher -------------------------------------------------------

		private void VerifyFileSystemWatcher()
		{
			if (fileSystemWatcher == null)
			{
				fileSystemWatcher = new FileSystemWatcher(fileInfo.Directory.FullName, fileInfo.Name) { NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size };
				fileSystemWatcher.Changed += FileSystemWatcher_Changed;
				fileSystemWatcher.EnableRaisingEvents = true;
			}
		} // proc VerifyFileSystemWatcher

		private sealed class RefreshIdleAction : IPpsIdleAction
		{
			private readonly FileSettingsInfo fileSettingsInfo;

			public RefreshIdleAction(FileSettingsInfo fileSettingsInfo)
				=> this.fileSettingsInfo = fileSettingsInfo ?? throw new ArgumentNullException(nameof(fileSettingsInfo));

			private void LoadAsyncFinished(Task t)
			{
				try
				{
					t.Wait();
				}
				catch (Exception e)
				{
					fileSettingsInfo.shell.LogProxy().Except(e);
				}
				finally
				{
					Interlocked.CompareExchange(ref fileSettingsInfo.currentAction, null, this);
				}
			} // proc LoadAsyncFinished

			PpsIdleReturn IPpsIdleAction.OnIdle(int elapsed)
			{
				fileSettingsInfo.LoadAsync(false).ContinueWith(LoadAsyncFinished, TaskContinuationOptions.ExecuteSynchronously);
				return PpsIdleReturn.Remove;
			} // func IPpsIdleAction.OnIdle
		} // class RefreshIdleAction

		private IPpsIdleAction currentAction = null;

		private void FileSystemWatcher_Changed(object sender, FileSystemEventArgs e)
		{
			if (currentAction != null)
				return;

			// schedule reload
			var action = new RefreshIdleAction(this);
			Interlocked.Exchange(ref currentAction, action);
			shell.GetService<IPpsUIService>(true).RunUI(() => shell.GetService<IPpsIdleService>(true).Add(action));
		} // event FileSystemWatcher_Changed

		#endregion

		#region -- GetSettings --------------------------------------------------------

		private bool IsGroupKey(int idx, string key)
		{
			var otherKey = settings[idx].Key;

			if (otherKey.Length == key.Length && String.Compare(otherKey, key, StringComparison.OrdinalIgnoreCase) == 0)
				return true;
			else if (otherKey.Length > key.Length && otherKey[key.Length] == '.' && otherKey.StartsWith(key, StringComparison.OrdinalIgnoreCase))
				return true;
			else
				return false;
		} // func IsGroupKey

		private void GetGroupRange(string key, int idx, out int startAt, out int endAt)
		{
			// find start of group
			startAt = idx;
			while (startAt >= 0 && IsGroupKey(startAt, key))
				startAt--;
			startAt++;

			// find end of group
			endAt = idx;
			while (endAt < settings.Count && IsGroupKey(endAt, key))
				endAt++;
			endAt--;
		} // func GetGroupRange

		protected override SettingBase GetSettingCore(string key, SettingMode forWrite)
		{
			var idx = settings.BinarySearch(new XSettingKey(key));
			if (idx >= 0)
			{
				if (forWrite == SettingMode.Clear)
				{
					GetGroupRange(key, idx, out var startAt, out var endAt);
					settings.RemoveRange(startAt, endAt - startAt + 1);
					return null;
				}
				else
					return settings[idx].Setting;
			}
			else if (forWrite == SettingMode.Write)
			{
				var s = new XSettingValue(key, null);
				settings.Insert(~idx, s);
				return s;
			}
			else
				return null;
		} // func GetSettingsCore

		protected override IEnumerable<SettingBase> GetSettingsCore()
			=> settings.Select(s => s.Setting);

		#endregion

		#region -- Read/Write Settings ------------------------------------------------

		protected override IUpdateScope BeginUpdate()
			=> new UpdateScope(this);

		protected override void OnSettingChanged(string key, string value)
		{
			isDirty = true;
			base.OnSettingChanged(key, value);
		} // proc OnSettingChanged

		private IEnumerable<KeyValuePair<string, string>> LoadSettingsFromFile(FileStream src)
		{
			using (var xml = CreateReader(src))
			{
				if (xml != null)
				{
					foreach (var kv in ParseSettings(xml, a => TranslateAttributeToSettingName(a, TranslateProperties)))
						yield return kv;
				}
			}
		} // func LoadSettingsFromFileAsync

		private async Task LoadCoreAsync(FileStream src, bool force)
		{
			if (!force)
			{
				fileInfo.Refresh();
				if (lastReaded >= fileInfo.LastWriteTime)
					return;
			}

			// load settings from file, do not remove any settings
			LoadSettings(await Task.Run(() => LoadSettingsFromFile(src).ToArray()));
		} // proc LoadCoreAsync

		/// <summary>Read all settings from disk.</summary>
		/// <param name="force"></param>
		/// <returns></returns>
		public async Task LoadAsync(bool force = true)
		{
			if (!fileInfo.Exists)
				return;
			VerifyFileSystemWatcher();

			using (var src = await OpenFileSafeAsync(fileInfo, true))
			{
				// reset state
				fileInfo.Refresh();
				if (!force)
				{
					if (lastReaded >= fileInfo.LastWriteTime)
						return;
				}
				lastReaded = fileInfo.LastWriteTime;

				// reset all setings
				settings.ForEach(c => c.Setting.ResetTouched());

				// load settings from file
				await LoadCoreAsync(src, true);

				// remove untouched settings
				for (var i = settings.Count - 1; i >= 0; i--)
				{
					if (!settings[i].Setting.ResetTouched())
						settings.RemoveAt(i);
				}
			}
		} // proc LoadAsync

		private static XElement EnforceElement(XElement xCur, string localName)
		{
			var x = xCur.Element(localName);
			if (x == null)
				xCur.Add(x = new XElement(localName));
			return x;
		} // func EnforceElement

		private static XNode GetTextValueNode(string value)
		{
			return value.IndexOfAny(new char[] { '<', '>' }) >= 0
				? new XCData(value)
				: new XText(value);
		} // func GetTextValueNode

		private static void SetTextValue(XElement x, string value)
		{
			var n = x.FirstNode;
			XNode c = null;
			while (n != null)
			{
				if (n is XCData || n is XText)
				{
					c = n;
					break;
				}
				n = n.NextNode;
			}

			var s = GetTextValueNode(value);
			if (s == null)
			{
				if (c != null)
					c.Remove();
			}
			else if (c == null)
				x.AddFirst(s);
			else if (s is XText st && c is XText ct)
				st.Value = ct.Value;
			else if (s is XCData sd && c is XCData cd)
				sd.Value = cd.Value;
			else
			{
				c.Remove();
				x.AddFirst(s);
			}
		} // proc SetTextValue

		private async Task WriteAsync()
		{
			if (!isDirty)
				return;

			var xRoot = new XElement("ppsn");

			foreach (var setting in GetSettings().OfType<IXSettingKey>().Select(s => s.Setting))
			{
				var key = setting.Key;
				var attributeName = TranslateSettingToAttributeName(key, TranslateProperties);
				if (attributeName != null)
					xRoot.SetAttributeValue(attributeName, setting.Value);
				else
				{
					var keyParts = key.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);

					var xCur = xRoot;
					for (var i = 0; i < keyParts.Length - 1; i++)
					{
						var n = keyParts[i];
						var localName = Int32.TryParse(n, out var idx) ? "i" + idx.ToString() : n;

						xCur = EnforceElement(xCur, localName);
					}

					SetTextValue(EnforceElement(xCur, keyParts[keyParts.Length - 1]), setting.Value);
				}
			}

			// write file
			lastReaded = await Task.Run(() =>
			{
				var xDoc = new XDocument(xRoot);
				var (src, xml) = CreateWriterAsync().Result;
				using (xml) // lock file for other write, read operations
				{
					// write xml-tags
					xDoc.WriteTo(xml);

					// truncate file
					xml.Flush();
					src.SetLength(src.Position);

					fileInfo.Refresh();
					return fileInfo.LastWriteTime;
				}
			});
			isDirty = false;
		} // proc WriteAsync

		#endregion

		protected abstract IReadOnlyList<Tuple<XName, string>> TranslateProperties { get; }

		public IPpsShell Shell => shell;
		public DateTime LastReaded => lastReaded;

		#region -- ParseSettings ------------------------------------------------------

		private static readonly Tuple<XName, string>[] translateInstanceProperties = new Tuple<XName, string>[]
		{
			new Tuple<XName, string>("uri", PpsShellSettings.PpsnUriKey),
			new Tuple<XName, string>("displayName", PpsShellSettings.PpsnNameKey),
			new Tuple<XName, string>("loginSecurity", PpsShellSettings.PpsnSecurtiyKey),
			new Tuple<XName, string>("version", PpsShellSettings.PpsnVersionKey)
		};

		private static readonly Tuple<XName, string>[] translateUserProperties = new Tuple<XName, string>[]
		{
			new Tuple<XName, string>("userId", PpsShellUserSettings.UserIdKey),
			new Tuple<XName, string>("displayName", PpsShellUserSettings.UserDisplayNameKey),
			new Tuple<XName, string>("FullName",  PpsShellUserSettings.UserIdentiyKey)
		};

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
					if (xml.IsEmptyElement)
					{
						yield return new KeyValuePair<string, string>(GetBasePathName(false), null);

						if (currentPath.Count > 0)
							currentPath.RemoveAt(currentPath.Count - 1); // pop current name as path
					}
					xml.Read();
					if (xml.NodeType == XmlNodeType.EndElement) // empty element
						yield return new KeyValuePair<string, string>(GetBasePathName(false), null);
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
					yield return new KeyValuePair<string, string>(GetBasePathName(false), xml.Value);
					xml.Read();
				}
				else
					xml.Read();
			}
		} // proc ParseSettings

		#endregion

		#region -- Translate Settings -------------------------------------------------

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

		#endregion

		/// <summary>Opens a local environment information.</summary>
		/// <param name="shell"></param>
		/// <param name="info"></param>
		public static FileSettingsInfo CreateInstanceSettings(IPpsShell shell, FileShellInfo info)
			=> new InstanceSettingsInfo(shell, info);

		/// <summary>Opens a local user information.</summary>
		/// <param name="shell"></param>
		/// <param name="userName"></param>
		/// <param name="userSettingInfo"></param>
		public static FileSettingsInfo CreateUserSettings(IPpsShell shell, FileInfo userSettingInfo, string userName)
			=> new UserSettingsInfo(shell, userSettingInfo, userName);
	} // class FileSettingsInfo

	#endregion

	#region -- class FileShellFactory -------------------------------------------------

	[PpsService(typeof(IPpsShellFactory))]
	internal sealed class FileShellFactory : IPpsShellFactory
	{
		private static readonly string localEnvironmentsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ppsn", "env");

		/// <summary>Create a new shell information.</summary>
		/// <param name="instanceName"></param>
		/// <param name="displayName"></param>
		/// <param name="uri"></param>
		/// <returns></returns>
		public IPpsShellInfo CreateNew(string instanceName, string displayName, Uri uri)
		{
			var shellInfo = new FileShellInfo(instanceName)
			{
				DisplayName = displayName,
				Uri = uri
			};
			shellInfo.WriteHeaderInfo();
			return shellInfo;
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
