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
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;
using TecWare.DE.Stuff;
using TecWare.PPSn.Stuff;

namespace TecWare.PPSn
{
	#region -- interface IPpsSettingsService ------------------------------------------

	/// <summary>Settings service contract.</summary>
	public interface IPpsSettingsService : INotifyPropertyChanged
	{
		/// <summary>Refresh settings from server.</summary>
		/// <param name="purge">Clear local options</param>
		Task RefreshAsync(bool purge);

		/// <summary>Returns all settings, or filtered by property names.</summary>
		/// <param name="filter">Property to retrieve</param>
		/// <returns></returns>
		IEnumerable<KeyValuePair<string, string>> Query(params string[] filter);
		/// <summary>Set properties</summary>
		/// <param name="values"></param>
		/// <returns></returns>
		Task<int> UpdateAsync(params KeyValuePair<string, string>[] values);
	} // interface IPpsSettingsService

	#endregion

	#region -- interface IPpsSettingsEdit ---------------------------------------------

	/// <summary>Bulk edit of multiple settings.</summary>
	public interface IPpsSettingsEdit : IDisposable
	{
		/// <summary>Set one setting</summary>
		/// <param name="key">Key</param>
		/// <param name="value">Value</param>
		IPpsSettingsEdit Set(string key, object value);

		/// <summary>Commit changes to settings service.</summary>
		/// <returns></returns>
		Task<int> CommitAsync();
	} // interface IPpsSettingsEdit

	#endregion

	#region -- class PpsSettingsGroup -------------------------------------------------

	/// <summary>Setting group.</summary>
	public sealed class PpsSettingsGroup : IPropertyEnumerableDictionary
	{
		private readonly PpsSettingsInfoBase settingsInfo;
		private readonly string groupName;
		private readonly string[] keys;

		internal PpsSettingsGroup(PpsSettingsInfoBase settingsInfo, string groupName, string[] keys)
		{
			this.settingsInfo = settingsInfo ?? throw new ArgumentNullException(nameof(settingsInfo));
			this.groupName = groupName ?? throw new ArgumentNullException(nameof(groupName));
			this.keys = keys ?? throw new ArgumentNullException(nameof(keys));
		} // ctor

		/// <summary>Return all grouped properties.</summary>
		/// <returns></returns>
		public IEnumerator<PropertyValue> GetEnumerator()
		{
			foreach (var k in keys)
			{
				if (settingsInfo.TryGetProperty(k, out var s))
					yield return new PropertyValue(k, s);
			}
		} // func GetEnumerator

		IEnumerator IEnumerable.GetEnumerator()
			=> GetEnumerator();

		/// <summary>Get a grouped property.</summary>
		/// <param name="index"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public bool TryGetProperty(int index, out string value)
			=> settingsInfo.TryGetProperty(keys[index], out value);

		/// <summary>Get a grouped property.</summary>
		/// <param name="name"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public bool TryGetProperty(string name, out string value)
		{
			foreach (var cur in keys)
			{
				if (cur.Length <= name.Length || cur[cur.Length - name.Length - 1] != '.')
					continue;

				if (cur.EndsWith(name, StringComparison.OrdinalIgnoreCase))
					return settingsInfo.TryGetProperty(cur, out value);
			}

			value = null;
			return false;
		} // func TryGetProperty

		bool IPropertyReadOnlyDictionary.TryGetProperty(string name, out object value)
		{
			if (TryGetProperty(name, out var t))
			{
				value = t;
				return true;
			}
			else
			{
				value = null;
				return false;
			}
		} // func IPropertyReadOnlyDictionary.TryGetProperty

		/// <summary>Name of the group.</summary>
		public string GroupName => groupName;
	} // class PpsSettingsGroup

	#endregion

	#region -- class PpsSettingsInfoBase ----------------------------------------------

	/// <summary>Settings info, that reads the settings service.</summary>
	public abstract class PpsSettingsInfoBase : DynamicObject, IPropertyEnumerableDictionary, INotifyPropertyChanged, IDisposable
	{
		/// <summary>All properties are changed.</summary>
		public static string AllProperties = ".";

		#region -- class SettingsEditor -----------------------------------------------

		private sealed class SettingsEditor : IPpsSettingsEdit
		{
			private readonly IPpsSettingsService settingsService;

			private readonly List<KeyValuePair<string, string>> pairs = new List<KeyValuePair<string, string>>();

			public SettingsEditor(IPpsSettingsService settingsService)
			{
				this.settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
			} // ctor

			public void Dispose()
				=> pairs.Clear();

			public IPpsSettingsEdit Set(string key, object value)
			{
				var idx = pairs.FindIndex(c => key == c.Key);
				if (idx == -1)
					pairs.Add(CreatePair(key, value));
				else
					pairs[idx] = CreatePair(key, value);
				return this;
			} // func Set

			public Task<int> CommitAsync()
				=> settingsService.UpdateAsync(pairs.ToArray());
		} // class SettingsEditor

		#endregion

		private readonly WeakEventList<PropertyChangedEventHandler, PropertyChangedEventArgs> propertyChangedList = new WeakEventList<PropertyChangedEventHandler, PropertyChangedEventArgs>();

		/// <summary>A setting is changed.</summary>
		public event PropertyChangedEventHandler PropertyChanged
		{
			add => propertyChangedList.Add(value);
			remove => propertyChangedList.Remove(value);
		} // event PropertyChanged

		private readonly IPpsSettingsService settingsService;
		private readonly Dictionary<string, string> cachedSettings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary></summary>
		/// <param name="settingsService"></param>
		protected PpsSettingsInfoBase(IPpsSettingsService settingsService)
		{
			this.settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

			settingsService.PropertyChanged += SettingsService_PropertyChanged;
		} // ctor

		/// <inherited/>
		public void Dispose()
		{
			Dispose(true);
		} // proc Dispose

		/// <summary>Dipose the settings proxy.</summary>
		/// <param name="disposing"></param>
		protected virtual void Dispose(bool disposing)
		{
			if (disposing)
				settingsService.PropertyChanged -= SettingsService_PropertyChanged;
		} // proc Dispose

		private void SettingsService_PropertyChanged(object sender, PropertyChangedEventArgs e)
			=> OnPropertyChanged(e.PropertyName);

		private void OnPropertyChanged(string propertyName)
		{
			var dirtyProperties = new List<string>();

			// mark property as dirty
			lock (cachedSettings)
			{
				if (propertyName == AllProperties)
				{
					// update cache in a bulk operation
					var cachedPropertyNames = cachedSettings.Keys.ToArray();
					foreach (var kv in settingsService.Query(cachedPropertyNames))
					{
						if (cachedSettings[kv.Key] != kv.Value)
						{
							dirtyProperties.Add(kv.Key);
							cachedSettings[kv.Key] = kv.Value;
						}
					}
				}
				else // update cache in a single operation
				{
					cachedSettings.Remove(propertyName);
					dirtyProperties.Add(propertyName);
				}
			}

			// invoke property changed
			foreach (var cur in dirtyProperties)
				propertyChangedList.Invoke(this, new PropertyChangedEventArgs(cur));
		} // proc OnPropertyChanged

		#endregion

		#region -- RefreshAsync -------------------------------------------------------

		/// <summary>Refersh settings from service.</summary>
		/// <param name="purge">Remove all locally saved settings.</param>
		/// <returns></returns>
		public Task RefreshAsync(bool purge)
			=> settingsService.RefreshAsync(purge);

		#endregion

		#region -- TryGetProperty, GetEnumerator --------------------------------------

		private static KeyValuePair<string, string> CreatePair(string key, object value)
			=> new KeyValuePair<string, string>(key, value.ChangeType<string>());

		private PropertyValue CreatePropertValue(KeyValuePair<string, string> kv)
			=> new PropertyValue(kv.Key, kv.Value);

		/// <summary>Get/cache multiple properties</summary>
		/// <param name="filter"></param>
		/// <returns></returns>
		public IEnumerable<KeyValuePair<string, string>> GetProperties(params string[] filter)
		{
			foreach (var kv in settingsService.Query(filter))
			{
				cachedSettings[kv.Key] = kv.Value;
				yield return kv;
			}
		} // func GetProperties

		/// <summary>Group keys relative to a baseKey</summary>
		/// <param name="baseKey"></param>
		/// <param name="groupName"></param>
		/// <param name="subKeys"></param>
		/// <returns></returns>
		public PpsSettingsGroup GetGroup(string baseKey, string groupName, params string[] subKeys)
		{
			if (subKeys.Length == 0)
				throw new ArgumentNullException(nameof(subKeys));

			// create full keys
			var fullKeys = new string[subKeys.Length];
			for (var i = 0; i < fullKeys.Length; i++)
				fullKeys[i] = baseKey + "." + subKeys[i];

			return new PpsSettingsGroup(this, groupName, fullKeys);
		} // func GetGroup

		/// <summary>Get grouped keys.</summary>
		/// <param name="baseKey"></param>
		/// <param name="requestAllSubKeys"></param>
		/// <param name="subKeys"></param>
		/// <returns></returns>
		public IEnumerable<PpsSettingsGroup> GetGroups(string baseKey, bool requestAllSubKeys, params string[] subKeys)
		{
			if (subKeys.Length == 0)
				yield break;

			// create query keys
			var queryKeys = new string[requestAllSubKeys ? subKeys.Length : 1];
			for (var i = 0; i < queryKeys.Length; i++)
				queryKeys[i] = baseKey + ".*." + subKeys[i];

			// query all values to fill cache
			var ofsAt = baseKey.Length; // first dot
			foreach (var kv in GetProperties(queryKeys))
			{
				var key = kv.Key;
				if (!key.EndsWith(subKeys[0], StringComparison.OrdinalIgnoreCase))
					continue;

				var endAt = key.Length - subKeys[0].Length - 1; // second dot
				if (ofsAt >= endAt || key[ofsAt] != '.' || key[endAt] != '.')
					continue;

				var groupName = key.Substring(ofsAt + 1, endAt - ofsAt - 1);

				// create other key names from group
				var fullKeys = new string[subKeys.Length];
				for (var i = 0; i < fullKeys.Length; i++)
					fullKeys[i] = baseKey + "." + groupName + "." + subKeys[i];

				yield return new PpsSettingsGroup(this, groupName, fullKeys);
			}
		} // func GetGroups

		/// <summary>Return one setting.</summary>
		/// <param name="name"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public bool TryGetProperty(string name, out object value)
		{
			lock (cachedSettings)
			{
				// read property from cache
				if (cachedSettings.TryGetValue(name, out var v))
				{
					value = v;
					return v != null;
				}
				else
				{
					foreach (var kv in settingsService.Query(name))
					{
						cachedSettings[name] = kv.Value;
						value = kv.Value;
						return true;
					}

					cachedSettings[name] = null;
					value = null;
					return false;
				}
			}
		} // func TryGetProperty

		/// <summary></summary>
		/// <param name="binder"></param>
		/// <param name="result"></param>
		/// <returns></returns>
		[EditorBrowsable(EditorBrowsableState.Never)]
		public override bool TryGetMember(GetMemberBinder binder, out object result)
		{
			TryGetProperty(binder.Name, out result);
			return true;
		} // func TryGetMember

		/// <summary></summary>
		/// <param name="binder"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		[EditorBrowsable(EditorBrowsableState.Never)]
		public override bool TrySetMember(SetMemberBinder binder, object value)
			=> settingsService.UpdateAsync(CreatePair(binder.Name, value)).Await() > 0;

		/// <summary></summary>
		/// <param name="binder"></param>
		/// <param name="indexes"></param>
		/// <param name="result"></param>
		/// <returns></returns>
		[EditorBrowsable(EditorBrowsableState.Never)]
		public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object result)
		{
			if (indexes.Length == 1 && indexes[0] is string propertyName)
			{
				TryGetProperty(propertyName, out result);
				return true;
			}
			else
				return base.TryGetIndex(binder, indexes, out result);
		} // func TryGetIndex

		/// <summary></summary>
		/// <param name="binder"></param>
		/// <param name="indexes"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		[EditorBrowsable(EditorBrowsableState.Never)]
		public override bool TrySetIndex(SetIndexBinder binder, object[] indexes, object value)
		{
			if (indexes.Length == 1 && indexes[0] is string propertyName)
			{
				settingsService.UpdateAsync(CreatePair(propertyName, value)).Await();
				return true;
			}
			else
				return base.TrySetIndex(binder, indexes, value);
		} // func TrySetIndex

		/// <summary>Return all settings.</summary>
		/// <returns></returns>
		public IEnumerator<PropertyValue> GetEnumerator()
			=> settingsService.Query().Select(CreatePropertValue).GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator()
			=> GetEnumerator();

		/// <summary>Start edit settings.</summary>
		/// <returns></returns>
		public IPpsSettingsEdit Edit()
			=> new SettingsEditor(settingsService);

		#endregion

		#region -- class PpsGenericSettingsInfo ---------------------------------------

		private sealed class PpsGenericSettingsInfo : PpsSettingsInfoBase
		{
			public PpsGenericSettingsInfo(IPpsSettingsService settingsService)
				: base(settingsService)
			{
			}
		} // class PpsGenericSettingsInfo

		#endregion

		internal static PpsSettingsInfoBase GetGeneric(IServiceProvider sp)
			=> new PpsGenericSettingsInfo(sp.GetService<IPpsSettingsService>(true));
	} // class PpsSettingsInfoBase

	#endregion

	#region -- class PpsBaseSettingsService -------------------------------------------

	/// <summary>Basic implementation for a settings service.</summary>
	/// <remarks>This class has a cache for requested values.</remarks>
	public abstract class PpsBaseSettingsService : IPpsSettingsService, IDisposable
	{
		#region -- enum SettingMode ---------------------------------------------------

		/// <summary>Setting get-mode</summary>
		protected enum SettingMode
		{
			/// <summary>Setting is for read, can return a value.</summary>
			Read,
			/// <summary>Setting is for write, must return a value.</summary>
			Write,
			/// <summary>Setting will be clear, can return a value.</summary>
			Clear
		} // enum SettingMode

		#endregion

		#region -- interface IUpdateScope ---------------------------------------------

		/// <summary>Implement a update scope for the setting change</summary>
		protected interface IUpdateScope : IDisposable
		{
			/// <summary>Commit all changes.</summary>
			/// <returns></returns>
			Task CommitAsync();
		} // interface IUpdateScope

		#endregion

		#region -- class SettingBase --------------------------------------------------

		/// <summary>A single setting.</summary>
		protected abstract class SettingBase
		{
			private readonly string key;

			/// <summary>Create setting</summary>
			/// <param name="key"></param>
			protected SettingBase(string key)
			{
				this.key = key ?? throw new ArgumentNullException(nameof(key));
			} // ctor

			/// <summary>Remove setting</summary>
			public bool Clear()
				=> ClearCore();

			/// <summary>Remove setting</summary>
			/// <returns></returns>
			protected virtual bool ClearCore()
				=> SetValue(null);

			/// <summary>Get value of the setting</summary>
			protected abstract string GetValueCore();

			/// <summary>Set value of the setting.</summary>
			protected abstract bool SetValueCore(string value);

			/// <summary>Set the value of this property.</summary>
			/// <param name="value"></param>
			/// <returns></returns>
			public bool SetValue(string value)
				=> SetValueCore(value);

			/// <summary>Key of the setting</summary>
			public string Key => key;
			/// <summary>Get the current value of the setting</summary>
			public string Value => GetValueCore();

			/// <summary>Is this setting invisible for the user.</summary>
			public virtual bool IsHidden => false;
		} // class SettingBase

		#endregion

		#region -- class KnownReadOnlySettingValue ------------------------------------

		[DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
		private sealed class KnownReadOnlySettingValue : SettingBase
		{
			private readonly Func<string> getValue;

			public KnownReadOnlySettingValue(string key, Func<string> getValue)
				: base(key)
			{
				this.getValue = getValue ?? throw new ArgumentNullException(nameof(getValue));
			} // ctor


			private string GetDebuggerDisplay()
				=> $"RO Setting: {Key}";

			/// <inherited/>
			protected override bool ClearCore()
				=> false;

			/// <inherited/>
			protected override string GetValueCore()
				=> getValue();

			/// <inherited/>
			protected override bool SetValueCore(string value)
				=> false;
		} // class KnownReadOnlySettingValue

		#endregion

		private readonly WeakEventList<PropertyChangedEventHandler, PropertyChangedEventArgs> propertyChangedList = new WeakEventList<PropertyChangedEventHandler, PropertyChangedEventArgs>();

		/// <summary>Is a property changed.</summary>
		public event PropertyChangedEventHandler PropertyChanged
		{
			add => propertyChangedList.Add(value);
			remove => propertyChangedList.Remove(value);
		} // event PropertyChanged

		private readonly Dictionary<string, SettingBase> knownSettings = new Dictionary<string, SettingBase>(StringComparer.OrdinalIgnoreCase);
		private bool isDisposed = false;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary></summary>
		protected PpsBaseSettingsService()
		{
		} // ctor

		/// <summary></summary>
		~PpsBaseSettingsService()
		{
			Dispose(false);
		} // dtor

		/// <inheritdoc/>
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		} // proc Dispose

		/// <summary></summary>
		/// <param name="disposing"></param>
		protected virtual void Dispose(bool disposing)
		{
			if (isDisposed)
				throw new ObjectDisposedException(nameof(PpsBaseSettingsService));

			isDisposed = true;
		} // proc Dispose

		/// <summary></summary>
		/// <param name="key"></param>
		/// <param name="getValue"></param>
		public void RegisterKnownReadOnlySettingValue(string key, Func<string> getValue)
			=> RegisterKnownSettingValue(new KnownReadOnlySettingValue(key, getValue));

		/// <summary>Register a known setting.</summary>
		/// <param name="setting"></param>
		protected void RegisterKnownSettingValue(SettingBase setting)
			=> knownSettings.Add(setting.Key, setting);

		#endregion

		#region -- GetSettings --------------------------------------------------------

		/// <summary>Get all settings.</summary>
		/// <returns></returns>
		protected abstract IEnumerable<SettingBase> GetSettingsCore();

		/// <summary>Returns all settings</summary>
		/// <returns></returns>
		protected IEnumerable<SettingBase> GetSettings()
		{
			foreach (var setting in knownSettings.Values)
				yield return setting;

			// clear other settings
			foreach (var setting in GetSettingsCore())
			{
				if (!knownSettings.ContainsKey(setting.Key))
					yield return setting;
			}
		} // func GetSettings

		/// <summary>Get a single setting.</summary>
		/// <param name="key">key of the setting.</param>
		/// <param name="forWrite">mode to open the setting.</param>
		/// <returns></returns>
		protected abstract SettingBase GetSettingCore(string key, SettingMode forWrite);

		private SettingBase GetSetting(string key, SettingMode mode)
			=> knownSettings.TryGetValue(key, out var setting) ? setting : GetSettingCore(key, mode);

		private bool TryGetSetting(string key, out SettingBase setting)
		{
			setting = GetSetting(key, SettingMode.Read);
			return setting != null;
		} // TryGetSetting

		#endregion

		#region -- Refresh ------------------------------------------------------------

		/// <summary>Load settings from server.</summary>
		/// <returns></returns>
		protected abstract Task LoadSettingsFromServerAsync();

		/// <summary>Refresh properties from server.</summary>
		/// <param name="purge"></param>
		/// <returns></returns>
		public virtual async Task RefreshAsync(bool purge)
		{
			// clear values
			if (purge)
				await ClearSettingsAsync();


			// refresh properties from server
			await LoadSettingsFromServerAsync();
		} // func RefreshAsync

		#endregion

		#region -- Query --------------------------------------------------------------

		private KeyValuePair<string, string> ToKeyValuePair(SettingBase setting)
			=> new KeyValuePair<string, string>(setting.Key, setting.Value);

		private IEnumerable<SettingBase> QueryFiltered(string[] filter)
		{
			var filterFuncs = new List<Func<string, bool>>();

			foreach (var k in filter)
			{
				if (k.IndexOf('*') >= 0)
					filterFuncs.Add(Procs.GetFilerFunction(k));
				else if (TryGetSetting(k, out var setting))
					yield return setting;
			}

			if (filterFuncs.Count > 0)
			{
				foreach (var kv in GetSettings())
				{
					if (filterFuncs.Any(c => c(kv.Key)))
						yield return kv;
				}
			}
		} // func QueryFiltered

		/// <summary>Get properties.</summary>
		/// <param name="filter"></param>
		/// <returns></returns>
		public virtual IEnumerable<KeyValuePair<string, string>> Query(params string[] filter)
		{
			if (filter == null || filter.Length == 0) // return all
				return GetSettings().Where(c => !c.IsHidden).Select(ToKeyValuePair);
			else
				return QueryFiltered(filter).Select(ToKeyValuePair);
		} // func Query

		#endregion

		#region -- Update -------------------------------------------------------------

		/// <summary>Notify a property is changed.</summary>
		/// <param name="key"></param>
		/// <param name="value"></param>
		protected virtual void OnSettingChanged(string key, string value)
			=> propertyChangedList.Invoke(this, new PropertyChangedEventArgs(key));

		/// <summary>Override to support a update scope.</summary>
		/// <returns></returns>
		protected virtual IUpdateScope BeginUpdate()
			=> null;

		/// <summary>Clear all settings to default.</summary>
		/// <returns></returns>
		protected virtual async Task ClearSettingsAsync()
		{
			using (var scope = BeginUpdate())
			{
				foreach (var setting in GetSettings())
				{
					if (setting.Clear())
						OnSettingChanged(setting.Key, null);
				}

				await scope.CommitAsync();
			}
		} // proc ClearSettings

		/// <summary>Updates settings</summary>
		/// <param name="values"></param>
		/// <returns></returns>
		protected int LoadSettings(IEnumerable<KeyValuePair<string, string>> values)
		{
			var changed = 0;

			foreach (var kv in values)
			{
				if (kv.Value != null) // set setting value
				{
					if (GetSetting(kv.Key, SettingMode.Write).SetValue(kv.Value))
					{
						OnSettingChanged(kv.Key, kv.Value);
						changed++;
					}
				}
				else // value is set to null, check to remove whole tree!
				{
					var s = GetSetting(kv.Key, SettingMode.Clear);
					if (s != null && s.Clear())
					{
						OnSettingChanged(kv.Key, null);
						changed++;
					}
				}
			}

			return changed;
		} // proc LoadSettings

		/// <summary>Updates settings</summary>
		/// <param name="values"></param>
		/// <returns></returns>
		public async Task<int> UpdateAsync(params KeyValuePair<string, string>[] values)
		{
			using (var scope = BeginUpdate())
			{
				var changed = LoadSettings(values);

				if (scope != null)
					await scope.CommitAsync();

				return changed;
			}
		} // func UpdateAsync

		#endregion
	} // class PpsBaseSettingsService

	#endregion
}
