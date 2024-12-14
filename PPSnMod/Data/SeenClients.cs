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
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using TecWare.DE.Server;
using TecWare.DE.Server.Http;
using TecWare.DE.Stuff;

namespace TecWare.PPSn.Server.Data
{
	#region -- class PpsSeenClient ----------------------------------------------------

	/// <summary>Currently known client.</summary>
	public sealed class PpsSeenClient
	{
		private readonly string clientId;

		private DateTime lastUpdate = DateTime.MinValue;
		private string version;
		private double lastLng = Double.NaN;
		private double lastLat = Double.NaN;
		private long lastGpsTimeStamp = 0;
		private string lastWifi = null;
		private string lastAddress = null;
		private Tuple<string, string>[] lastModulInfo = Array.Empty<Tuple<string, string>>();

		private bool sendLogFlag = false;
		private bool dumpAppStateFlag = false;
		private int alarmRepeat = 0;

		#region -- Ctor/Dtor ----------------------------------------------------------

		internal PpsSeenClient(string deviceId, IDEWebRequestScope r)
		{
			this.clientId = deviceId ?? throw new ArgumentNullException(nameof(deviceId));

			Update(r, out _);
		} // ctor

		internal PpsSeenClient(string clientId, XElement x)
		{
			this.clientId = clientId ?? throw new ArgumentNullException(nameof(clientId));

			var lastUpdate = x.GetAttribute("last", 0L);
			this.lastUpdate = lastUpdate > 0 ? DateTime.FromFileTimeUtc(lastUpdate) : DateTime.MinValue;

			version = x.GetAttribute("v", null);

			lastLng = x.GetAttribute("lng", Double.NaN);
			lastLat = x.GetAttribute("lat", Double.NaN);
			lastGpsTimeStamp = x.GetAttribute("gpsts", 0L);

			lastWifi = x.GetAttribute("wifi", null);
			lastAddress = x.GetAttribute("addr", null);

			lastModulInfo = x.Elements("app").Select(CreateModulInfoTuple).ToArray();
		} // ctor

		private static Tuple<string, string> CreateModulInfoTuple(XElement x)
			=> x.TryGetAttribute<string>("n", out var name) ? new Tuple<string, string>(name, x.Value) : null;

		#endregion

		#region -- ToXml/Update -------------------------------------------------------

		/// <summary>Create a xml of the data.</summary>
		/// <returns></returns>
		public XElement ToXml()
		{
			return new XElement("client",
				new XAttribute("id", clientId),
				Procs.XAttributeCreate("v", version, null),
				Procs.XAttributeCreate("last", lastUpdate == DateTime.MinValue ? 0L : lastUpdate.ToFileTimeUtc(), 0L),
				Procs.XAttributeCreate("lng", lastLng, Double.NaN),
				Procs.XAttributeCreate("lat", lastLat, Double.NaN),
				Procs.XAttributeCreate("gpsts", lastGpsTimeStamp, 0L),

				Procs.XAttributeCreate("wifi", lastWifi, null),
				Procs.XAttributeCreate("addr", lastAddress, null),
				from c in lastModulInfo select new XElement("app", new XAttribute("n", c.Item1), c.Item2)
			);
		} // func ToXml

		/// <summary>Update information from request</summary>
		/// <param name="r"></param>
		/// <param name="columnsChanged"></param>
		public void Update(IDEWebRequestScope r, out bool columnsChanged)
		{
			version = r.GetProperty("x-ppsn-version", version);
			lastLng = r.GetProperty("x-ppsn-lng", lastLng);
			lastLat = r.GetProperty("x-ppsn-lat", lastLat);
			lastGpsTimeStamp = r.GetProperty("x-ppsn-ltm", lastGpsTimeStamp);
			lastWifi = r.GetProperty("x-ppsn-wifi", lastWifi);
			lastAddress = r.RemoteEndPoint?.Address.ToString();

			var newModulInfo = Procs.SplitPropertyList(r.GetProperty("x-ppsn-versions", null)).Select(c => new Tuple<string, string>(c.Key, c.Value)).ToArray();
			if (newModulInfo.Length == lastModulInfo.Length)
			{
				columnsChanged = false;
				for (var i = 0; i < newModulInfo.Length; i++)
				{
					if (String.Compare(newModulInfo[i].Item1, lastModulInfo[i].Item1, StringComparison.OrdinalIgnoreCase) != 0
						|| String.Compare(newModulInfo[i].Item2, lastModulInfo[i].Item2, StringComparison.OrdinalIgnoreCase) != 0)
					{
						columnsChanged = true;
						break;
					}
				}
			}
			else
				columnsChanged = true;

			if (columnsChanged)
				lastModulInfo = newModulInfo;

			lastUpdate = DateTime.Now;
		} // proc Update

		#endregion

		#region -- Flags --------------------------------------------------------------

		private bool SwitchFlag(ref bool flag)
		{
			if (flag)
			{
				flag = false;
				return true;
			}
			return false;
		} // func SwitchFlag

		/// <summary>Request a log from the client.</summary>
		/// <returns></returns>
		public bool SetSendLogFlag()
			=> sendLogFlag = true;

		/// <summary>Get flag, and reset the state.</summary>
		/// <returns></returns>
		public bool GetSendLogFlag()
			=> SwitchFlag(ref sendLogFlag);

		/// <summary>Request a application dump from the client.</summary>
		/// <returns></returns>
		public bool SetDumpAppStateFlag()
			=> sendLogFlag = true;

		/// <summary>Get flag, and reset the state.</summary>
		/// <returns></returns>
		public bool GetDumpAppStateFlag()
			=> SwitchFlag(ref dumpAppStateFlag);

		/// <summary>Identity the client.</summary>
		/// <param name="repeat"></param>
		public void SetAlarmRepeatFlag(int repeat)
			=> alarmRepeat = repeat;

		/// <summary>Get flag, and reset the state.</summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public bool TryGetAlarmRepeatFlag(out int value)
		{
			if (alarmRepeat != 0)
			{
				value = alarmRepeat;
				alarmRepeat = 0;
				return true;
			}
			else
			{
				value = 0;
				return false;
			}
		} // func TryGetAlarmRepeatFlag

		#endregion

		/// <summary>Value for the modul info</summary>
		/// <param name="name"></param>
		/// <returns></returns>
		public string GetModulInfoValue(string name)
			=> lastModulInfo.FirstOrDefault(c => String.Compare(c.Item1, name, StringComparison.OrdinalIgnoreCase) == 0)?.Item2;

		/// <summary>Id of the device.</summary>
		public string ClientId => clientId;
		/// <summary>Current version.</summary>
		public string Version => version;

		/// <summary>Last time the information where updated.</summary>
		public DateTime LastTimeSeen => lastUpdate > DateTime.MinValue ? lastUpdate.ToLocalTime() : lastUpdate;
		/// <summary>Gps position of the device.</summary>
		public double Latitude => lastLat;
		/// <summary>Gps position of the device.</summary>
		public double Longtitude => lastLng;
		/// <summary>Last seen gps update.</summary>
		public DateTime GpsTimeStamp => new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(lastGpsTimeStamp).ToLocalTime();

		/// <summary>Current wifi of the device.</summary>
		public string Wifi => lastWifi;
		/// <summary>Last ip-address of the device.</summary>
		public string Address => lastAddress;

		/// <summary>Show pending request flags.</summary>
		public string Pending
		{
			get
			{
				return String.Join(",",
					new string[]
					{
							sendLogFlag ? "LogRequest" : null,
							dumpAppStateFlag ? "AppState" : null,
							alarmRepeat != 0 ? $"R({alarmRepeat})" : null
					}.Where(c => c != null)
				);
			}
		} // prop Pending

		/// <summary>Modul version information</summary>
		public IReadOnlyList<Tuple<string, string>> ModulVersionInfo => lastModulInfo;
	} // class PpsSeenClient

	#endregion

	#region -- class PpsSeenClientList ------------------------------------------------

	internal sealed class PpsSeenClientList : IDEListController, IDEListDescriptor
	{
		#region -- class ExtraColumnInfo ----------------------------------------------

		private sealed class ExtraColumnInfo : IComparable<ExtraColumnInfo>
		{
			private readonly string key;
			private readonly string attribute;
			private int refCount;

			public ExtraColumnInfo(string name)
			{
				key = name ?? throw new ArgumentNullException(nameof(name));
				attribute = GetCleanName(name);
			} // ctor

			public int CompareTo(ExtraColumnInfo other)
			{
				var r = refCount.CompareTo(other.refCount);
				if (r == 0)
					r = key.CompareTo(other.key);
				return r;
			} // func CompareTo

			private static string GetCleanName(string name)
			{
				var j = 0;
				var c = new char[20];
				for (var i = 0; i < name.Length; i++)
				{
					if (j > 20)
						break;

					if (j == 0)
					{
						if (Char.IsLetter(name[i]))
						{
							c[j] = name[i];
							j++;
						}
					}
					else
					{
						if (Char.IsLetterOrDigit(name[i]))
						{
							c[j] = name[i];
							j++;
						}
					}
				}

				return j == 0 ? null : new String(c, 0, j);
			} // func GetCleanName

			public void IncRef()
				=> refCount++;

			public string Key => key;
			public string Attribute => attribute;

			public bool IsValid => attribute != null;
		} // class ExtraColumnInfo

		#endregion

		private const string typeName = nameof(PpsSeenClient);

		private readonly DEConfigLogItem configItem;
		private readonly Action saveAction;
		private readonly List<PpsSeenClient> clients;
		private readonly ReaderWriterLockSlim listLock;

		private long lastChange = DateTime.Now.ToFileTime();
		private ExtraColumnInfo[] extraColumns = null;
		private bool refreshExtraColumns;

		#region -- Ctor/Dtor ----------------------------------------------------------

		public PpsSeenClientList(DEConfigLogItem configItem)
		{
			this.configItem = configItem ?? throw new ArgumentNullException(nameof(configItem));

			saveAction = new Action(Save);

			clients = new List<PpsSeenClient>();
			listLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
		} // ctor

		~PpsSeenClientList()
			=> Dispose(false);

		public void Dispose()
		{
			try
			{
				Dispose(true);
			}
			finally
			{
				GC.SuppressFinalize(this);
			}
		} // proc Dispose

		/// <summary></summary>
		/// <param name="disposing"></param>
		private void Dispose(bool disposing)
		{
			if (disposing)
			{
				// Liste wieder austragen
				configItem.UnregisterList(this);

				// Sperren zerstören
				listLock.Dispose();
			}
		} // proc Dispose

		#endregion

		#region -- EnterReadLock, EnterWriteLock --------------------------------------

		/// <summary>Enter read access to this list.</summary>
		/// <returns></returns>
		public IDisposable EnterReadLock()
		{
			if (listLock == null)
				return null;

			if (listLock.IsWriteLockHeld)
				return null;
			else
			{
				listLock.EnterReadLock();
				return new DisposableScope(listLock.ExitReadLock);
			}
		} // func EnterReadLock

		/// <summary>Enter write access to this list.</summary>
		/// <returns></returns>
		public IDisposable EnterWriteLock()
		{
			if (listLock == null)
				return null;

			listLock.EnterWriteLock();
			return new DisposableScope(listLock.ExitWriteLock);
		} // func EnterWriteLock

		#endregion

		#region -- IDEListDescriptor - members ----------------------------------------

		private ExtraColumnInfo[] GetExtraColumns()
		{
			var tmp = extraColumns;
			if (refreshExtraColumns || tmp is null)
			{
				var list = new List<ExtraColumnInfo>(16);

				for (var i = 0; i < clients.Count; i++)
				{
					var info = clients[i].ModulVersionInfo;
					for (var j = 0; j < info.Count; j++)
					{
						var name = info[j].Item1;
						var col = list.FirstOrDefault(c => String.Compare(c.Key, name, StringComparison.OrdinalIgnoreCase) == 0);
						if (col != null)
							col.IncRef();
						else
							list.Add(new ExtraColumnInfo(name));
					}
				}

				list.Sort();

				refreshExtraColumns = false;
				tmp = extraColumns = list.Where(c => c.IsValid).ToArray();
			}
			return tmp;
		} // proc RefreshExtraColumns

		void IDEListDescriptor.WriteType(DEListTypeWriter xml)
		{
			xml.WriteStartType(typeName);
			xml.WriteProperty("@id", typeof(string));
			xml.WriteProperty("@version", typeof(string));

			var columns = GetExtraColumns();
			for (var i = 0; i < columns.Length; i++)
				xml.WriteProperty(columns[i].Attribute, typeof(string));

			xml.WriteProperty("@lastTimeSeen", typeof(DateTime));
			xml.WriteProperty("@lat", typeof(double));
			xml.WriteProperty("@lng", typeof(double));
			xml.WriteProperty("@time", typeof(DateTime));
			xml.WriteProperty("@wifi", typeof(string));
			xml.WriteProperty("@addr", typeof(string));
			xml.WriteProperty("@pending", typeof(string));
			xml.WriteEndType();
		} // proc WriteType

		void IDEListDescriptor.WriteItem(DEListItemWriter xml, object item)
		{
			var cur = (PpsSeenClient)item;

			xml.WriteStartProperty(typeName);
			xml.WriteProperty("@id", cur.ClientId);
			xml.WriteProperty("@version", cur.Version);

			var columns = extraColumns;
			if (columns != null)
			{
				for (var i = 0; i < columns.Length; i++)
				{
					var value = cur.GetModulInfoValue(columns[i].Key);
					if (value != null)
						xml.WriteProperty(columns[i].Attribute, value);
				}
			}

			xml.WriteProperty("@lastTimeSeen", cur.LastTimeSeen);
			xml.WriteProperty("@lat", cur.Latitude);
			xml.WriteProperty("@lng", cur.Longtitude);
			xml.WriteProperty("@time", cur.GpsTimeStamp);
			xml.WriteProperty("@wifi", cur.Wifi);
			xml.WriteProperty("@addr", cur.Address);
			xml.WriteProperty("@pending", cur.Pending);
			xml.WriteEndProperty();
		} // proc WriteItem

		#endregion

		#region -- Load/Save ------------------------------------------------------

		private FileInfo GetHistoryFileInfo()
			=> new FileInfo(Path.ChangeExtension(configItem.LogFileName, ".clients.xml"));

		public void Load()
		{
			using (EnterWriteLock())
			{
				try
				{
					var fi = GetHistoryFileInfo();
					if (fi.Exists)
					{
						var xDoc = XDocument.Load(fi.FullName);
						foreach (var x in xDoc.Root.Elements("client"))
						{
							var clientId = x.GetAttribute("id", null);
							if (String.IsNullOrEmpty(clientId))
								continue;

							var idx = FindIndex(clientId);
							if (idx == -1)
								clients.Add(new PpsSeenClient(clientId, x));
						}
					}
				}
				catch (Exception e)
				{
					configItem.Log.Except(e);
				}
			}
		} // proc Load

		private void Save()
		{
			using (EnterReadLock())
			{
				try
				{
					new XDocument(
						new XElement("clients",
							clients.Select(d => d.ToXml())
						)
					).Save(GetHistoryFileInfo().FullName);
				}
				catch (Exception e)
				{
					configItem.Log.Except(e);
				}
			}
		} // proc Save

		private void EnqueueSave()
		{
			lastChange = DateTime.Now.ToFileTime();

			var queue = configItem.Server.Queue;
			if (queue.IsQueueRunning)
			{
				queue.CancelCommand(saveAction);
				queue.RegisterCommand(saveAction, 10000);
			}
			else
				Save();
		} // proc EnqueueSaveSeenClients
		#endregion

		private int FindIndex(string clientId)
			=> clients.FindIndex(c => String.Compare(c.ClientId, clientId, StringComparison.OrdinalIgnoreCase) == 0);

		public PpsSeenClient Update(IDEWebRequestScope r)
		{
			var clientId = r.GetProperty("id", null);

			// device id is needed
			if (String.IsNullOrEmpty(clientId))
				return null;

			var idx = -1;
			using (EnterWriteLock())
				idx = FindIndex(clientId);
			
			if (idx == -1)
			{
				using (EnterWriteLock())
				{
					idx = clients.Count;
					clients.Add(new PpsSeenClient(clientId, r));
				}
			}
			else
			{
				clients[idx].Update(r, out var columnsChanged);
				if (columnsChanged)
					refreshExtraColumns = true;
			}
			EnqueueSave();

			return clients[idx];
		} // proc Update
		
		public void Remove(string clientId)
		{
			using (EnterWriteLock())
			{
				var idx = FindIndex(clientId);
				if (idx >= 0)
				{
					clients.RemoveAt(idx);
					extraColumns = null; // redo extra columns
					EnqueueSave();
				}
			}
		} // proc Remove

		public PpsSeenClient Find(string clientId)
		{
			using (EnterReadLock())
			{
				var idx = FindIndex(clientId);
				return idx == -1 ? null : clients[idx];
			}
		} // func Find

		void IDEListController.OnBeforeList() { }

		public string Id => "tw_ppsn_clients";
		public string DisplayName => "Last Seen Clients";
		public string SecurityToken => DEConfigItem.SecuritySys;

		public IReadOnlyList<PpsSeenClient> UnsafeClients => clients;
		public long LastChange => lastChange;

		IDEListDescriptor IDEListController.Descriptor => this;
		IEnumerable IDEListController.List => clients;
	} // class PpsSeenClientList

	#endregion

}
