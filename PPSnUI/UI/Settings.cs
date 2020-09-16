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
using System.Dynamic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using TecWare.DE.Networking;
using TecWare.DE.Stuff;

namespace TecWare.PPSn.UI
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

		/// <summary>Return one property value.</summary>
		/// <param name="name"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		bool TryGetProperty(string name, out string value);
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

		/// <summary>A setting is changed.</summary>
		public event PropertyChangedEventHandler PropertyChanged;

		private readonly IPpsSettingsService settingsService;
		private readonly Dictionary<string, string> cachedSettings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		#region -- Ctor/Dtor ----------------------------------------------------------

		protected PpsSettingsInfoBase(IPpsSettingsService settingsService)
		{
			this.settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

			settingsService.PropertyChanged += SettingsService_PropertyChanged;
		} // ctor

		/// <summary></summary>
		public void Dispose()
		{
			Dispose(true);
		} // proc Dispose

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
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(cur));
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
				else if (settingsService.TryGetProperty(name, out v))
				{
					cachedSettings[name] = v;
					value = v;
					return true;
				}
				else
				{
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
	} // class PpsSettingsInfoBase

	#endregion

	#region -- class PpsSettingsInfo --------------------------------------------------

	/// <summary>Settings info, that reads the settings service.</summary>
	[Obsolete("use PpsSettingsInfoBase")]
	public sealed class PpsSettingsInfo : DynamicObject, IPropertyEnumerableDictionary, INotifyPropertyChanged, IDisposable
	{
		/// <summary>All properties are changed.</summary>
		public static string AllProperties = ".";

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public static string DpcUriKey { get; } = "DPC.Uri";
		public static string DpcUserKey { get; } = "DPC.User";
		public static string DpcPasswordKey { get; } = "DPC.Password";
		public static string DpcPinKey { get; } = "DPC.Pin";
		public static string DpcDebugModeKey { get; } = "DPC.Debug";

		public static string DpcDeviceIdKey { get; } = "DPC.DeviceId.Local";
		public static string DpcSoftwareVersionKey { get; } = "DPC.SoftwareVersion.Local";
		public static string DpcConnectionStateKey { get; } = "DPC.ConnectionStatus.Local";
		public static string DpcNetworkStateKey { get; } = "DPC.NetworkStatus.Local";
		public static string DpcLogStateKey { get; } = "DPC.LogState.Local";

		public const string ClockFormatKey = "PPSn.ClockFormat";
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		/// <summary>A setting is changed.</summary>
		public event PropertyChangedEventHandler PropertyChanged;

		#region -- class KnownSetting -------------------------------------------------

		private sealed class KnownSetting
		{
			public KnownSetting(string key, string displayName)
			{
				Key = key ?? throw new ArgumentNullException(nameof(key));
				DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
			}

			public string Key { get; }
			public string DisplayName { get; }
		} // class KnownSetting

		#endregion

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

		private readonly IPpsSettingsService settingsService;
		private readonly Dictionary<string, string> cachedSettings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		#region -- Ctor/Dtor ----------------------------------------------------------

		private PpsSettingsInfo(IPpsSettingsService settingsService)
		{
			this.settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

			settingsService.PropertyChanged += SettingsService_PropertyChanged;
		} // ctor

		/// <summary></summary>
		public void Dispose()
		{
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
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(cur));
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
				else if (settingsService.TryGetProperty(name, out v))
				{
					cachedSettings[name] = v;
					value = v;
					return true;
				}
				else
				{
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

		#region -- Known Properties ---------------------------------------------------

		/// <summary>Return the dpc user and password.</summary>
		/// <returns></returns>
		public ICredentials GetDpcCredentials()
		{
			var userName = this.GetProperty(DpcUserKey, null);
			var password = this.GetProperty(DpcPasswordKey, null);

			if (String.IsNullOrEmpty(userName))
				return null;

			return UserCredential.Create(userName, password);
		} // func GetDpcCredentials

		public string GetClockFormat()
			=> this.GetProperty(ClockFormatKey, "HH:mm\ndd.MM.yyyy");

		/// <summary>Uri</summary>
		public Uri DpcUri => PpsShell.GetUriFromString(this.GetProperty(DpcUriKey, null), UriKind.Absolute);
		/// <summary>Pin to unprotect the application.</summary>
		public string DpcPin => this.GetProperty(DpcPinKey, "2682");
		/// <summary>Id of the device.</summary>
		public string DpcDeviceId => this.GetProperty(DpcDeviceIdKey, "(unknown)");
		/// <summary>Is the application in debug mode.</summary>
		public bool IsDebugMode => this.GetProperty(DpcDebugModeKey, false);

		#endregion

		// -- Static ----------------------------------------------------------

		private static readonly Lazy<PpsSettingsInfo> defaultLazy = new Lazy<PpsSettingsInfo>(() => new PpsSettingsInfo(PpsShell.GetService<IPpsSettingsService>(true)));
		private static readonly List<KnownSetting> knownSettings = new List<KnownSetting>();

		/// <summary>Register a known setting, that will be showed without entering the pin.</summary>
		/// <param name="key"></param>
		/// <param name="displayName"></param>
		public static void RegisterKnownSetting(string key, string displayName)
			=> knownSettings.Add(new KnownSetting(key, displayName));

		/// <summary></summary>
		/// <param name="key"></param>
		/// <param name="displayName"></param>
		/// <returns></returns>
		public static bool TryGetKnownSetting(string key, out string displayName)
		{
			var s = knownSettings.FirstOrDefault(c => String.Compare(c.Key, key, StringComparison.OrdinalIgnoreCase) == 0);
			if (s != null)
			{
				displayName = s.DisplayName;
				return true;
			}
			else
			{
				displayName = key;
				return false;
			}
		} // func TryGetKnownSetting

		/// <summary></summary>
		/// <param name="service"></param>
		/// <returns></returns>
		public static PpsSettingsInfo Create(IPpsSettingsService service)
			=> new PpsSettingsInfo(service);

		/// <summary>Settingsinfo</summary>
		public static PpsSettingsInfo Default => defaultLazy.Value;
	} // class PpsSettingsInfo

	#endregion

	#region -- class PpsBaseSettingsService -------------------------------------------

	/// <summary>Basic implementation for a settings service.</summary>
	public abstract class PpsBaseSettingsService : IPpsSettingsService, IDisposable
	{
		#region -- class SettingBase --------------------------------------------------

		private abstract class SettingBase
		{
			private readonly PpsBaseSettingsService service;
			private readonly string key;

			public SettingBase(PpsBaseSettingsService service, string key)
			{
				this.service = service ?? throw new ArgumentNullException(nameof(service));
				this.key = key ?? throw new ArgumentNullException(nameof(key));
			} // ctor

			public void FireValueChanged()
				=> OnValueChanged();

			protected virtual void OnValueChanged()
				=> service.FireSettingChanged(key);

			public void Clear()
				=> ClearCore();

			protected virtual void ClearCore()
				=> SetValue(null);

			protected virtual string GetValueCore()
				=> null;

			protected virtual bool SetValueCore(string value)
				=> false;

			private void SetValue(string value)
			{
				if (SetValueCore(value))
					OnValueChanged();
			} // proc SetValue

			public string Key => key;
			public string Value { get => GetValueCore(); set => SetValue(value); }

			public virtual bool IsHidden => false;

			protected PpsBaseSettingsService Service => service;
		} // class SettingBase

		#endregion

		#region -- class SettingValue -------------------------------------------------

		private class SettingValue : SettingBase
		{
			public SettingValue(PpsBaseSettingsService service, string key)
				: base(service, key)
			{
			} // ctor

			protected override string GetValueCore()
				=> Service.GetStoredValue(Key);

			protected override bool SetValueCore(string value)
				=> Service.SetStoredValue(Key, value);
		} // class SettingValue

		#endregion

		#region -- class KnownSettingValue --------------------------------------------

		private sealed class KnownSettingValue : SettingValue
		{
			private readonly bool isHidden;
			private readonly bool clearAble;
			private readonly string defaultValue;

			private readonly Action<string> hook;

			public KnownSettingValue(PpsBaseSettingsService service, string key, bool isHidden = false, string defaultValue = null, bool clearAble = true, Action<string> hook = null)
				: base(service, key)
			{
				this.isHidden = isHidden;
				this.clearAble = clearAble;
				this.defaultValue = defaultValue;

				this.hook = hook;
			} // ctor

			protected override void OnValueChanged()
			{
				base.OnValueChanged();
				hook?.Invoke(Value);
			} // proc OnPropertyChanged

			protected override void ClearCore()
			{
				if (clearAble)
					Value = defaultValue;
			} // proc ClearCore

			protected override string GetValueCore()
				=> base.GetValueCore() ?? defaultValue;

			protected override bool SetValueCore(string value)
			{
				return base.SetValueCore(
					(defaultValue == null && value == null) || defaultValue == value
						? null
						: (value ?? String.Empty)
				);
			} // func SetValueCore

			public sealed override bool IsHidden => isHidden;
		} // class KnownSettingValue

		#endregion

		#region -- class KnownReadOnlySettingValue ------------------------------------

		private sealed class KnownReadOnlySettingValue : SettingBase
		{
			private readonly Func<string> getValue;

			public KnownReadOnlySettingValue(PpsBaseSettingsService service, string key, Func<string> getValue)
				: base(service, key)
			{
				this.getValue = getValue ?? throw new ArgumentNullException(nameof(getValue));
			} // ctor

			protected override void ClearCore() { }

			protected override string GetValueCore()
				=> getValue();
		} // class KnownReadOnlySettingValue

		#endregion

		#region -- enum SettingsCacheState --------------------------------------------

		[Flags]
		public enum SettingsCacheState
		{
			None = 0,
			Readed = 1,
			Changed = 2
		} // enum SettingsCacheState

		#endregion

		#region -- class SettingsCacheValue -------------------------------------------

		private sealed class SettingsCacheValue
		{
			private readonly string name;
			private string value = null;
			private bool isDirty = false;

			public SettingsCacheValue(string name)
				=> this.name = name ?? throw new ArgumentNullException(nameof(name));

			public SettingsCacheState ReadCachedSetting(string newValue)
			{
				isDirty = false;
				if (newValue != value)
				{
					value = newValue;
					return SettingsCacheState.Changed | SettingsCacheState.Readed;
				}
				else
					return SettingsCacheState.Readed;
			} // func ReadValueCore

			public string Name => name;

			public string Value
			{
				get => value; 
				set
				{
					if (this.value != value)
					{
						this.value = value;
						isDirty = true;
					}
				}
			} // prop Value

			public bool IsDirty => isDirty;
		} // class SettingsCacheValue

		#endregion

		public event PropertyChangedEventHandler PropertyChanged;

		private readonly Dictionary<string, SettingBase> knownSettings = new Dictionary<string, SettingBase>(StringComparer.OrdinalIgnoreCase);
		private readonly Dictionary<string, SettingsCacheValue> values = new Dictionary<string, SettingsCacheValue>(StringComparer.OrdinalIgnoreCase);
		private bool isDisposed = false;

		#region -- Ctor/Dtor ----------------------------------------------------------

		protected PpsBaseSettingsService()
		{
		} // ctor

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

		public void RegisterKnownSettingValue(string key, bool isHidden = false, string defaultValue = null, bool clearAble = true, Action<string> hook = null)
			=> RegisterKnownSettingValue(new KnownSettingValue(this, key, isHidden, defaultValue, clearAble, hook));

		public void RegisterKnownReadOnlySettingValue(string key, Func<string> getValue)
			=> RegisterKnownSettingValue(new KnownReadOnlySettingValue(this, key, getValue));

		private void RegisterKnownSettingValue(SettingBase setting)
			=> knownSettings.Add(setting.Key, setting);

		#endregion

		#region -- Read/Write ---------------------------------------------------------

		#region -- interface IPpsSettingsWriter ---------------------------------------

		protected interface IPpsSettingsWriter
		{
			bool? WritePair(string name, string value, bool isDirty);

			Task FlushAsync();
		} // interface IPpsSettingsWriter

		#endregion

		protected abstract Task LoadSettingsFromServerAsync();

		protected abstract IEnumerable<KeyValuePair<string, string>> ParseSettings();

		protected abstract IPpsSettingsWriter CreateSettingsWriter();

		protected async Task ReadSettingsAsync()
		{
			var removeNames = new List<string>(values.Keys);

			// read values from registry
			var e = await Task.Run(() => ParseSettings().GetEnumerator());
			try
			{
				while (await Task.Run(() => e.MoveNext()))
				{
					var kv = e.Current;

					if (!values.TryGetValue(kv.Key, out var cur))
					{
						cur = new SettingsCacheValue(kv.Key);
						values[kv.Key] = cur;
					}
					var f = cur.ReadCachedSetting(kv.Value);
					if (f != SettingsCacheState.None)
						removeNames.Remove(cur.Name);
					if ((f & SettingsCacheState.Changed) != 0)
						GetSetting(cur.Name).FireValueChanged();
				}
			}
			finally
			{
				await Task.Run(() => e.Dispose());
			}

			// remove clear
			foreach (var c in removeNames)
			{
				values.Remove(c);
				FireSettingChanged(c);
			}
		} // proc ReadSettingsAsync

		protected async Task<int> WriteSettingsAsync()
		{
			var removeNames = new List<string>();
			var i = 0;

			// write keys
			var w = CreateSettingsWriter();
			try
			{
				foreach (var cur in values.Values)
				{
					var l = w.WritePair(cur.Name, cur.Value, cur.IsDirty);
					if (!l.HasValue)
						removeNames.Add(cur.Name);
					else if (l.Value)
						i++;
				}

				if (i > 0)
				{
					await w.FlushAsync();

					// remove clear
					foreach (var c in removeNames)
						values.Remove(c);
				}
			}
			finally
			{
				if (w is IDisposable d)
					d.Dispose();
			}

			return i;
		} // func WriteSettings

		#endregion

		#region -- GetStoredValue, SetStoredValue -------------------------------------

		private void FireSettingChanged(string key)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(key));

		private SettingBase GetSetting(string key)
			=> knownSettings.TryGetValue(key, out var s) ? s : new SettingValue(this, key);

		private string GetStoredValue(string key)
			=> values.TryGetValue(key, out var cur) ? cur.Value : null;

		private bool SetStoredValue(string key, string value)
		{
			if (!values.TryGetValue(key, out var cur))
			{
				cur = new SettingsCacheValue(key);
				values[cur.Name] = cur;
			}

			cur.Value = value;
			return cur.IsDirty;
		} // func SetStoredValue

		#endregion

		#region -- Refresh, Query, Update ---------------------------------------------

		public async Task RefreshAsync(bool purge)
		{
			// clear values
			if (purge)
			{
				foreach (var k in values.Keys.ToArray())
				{
					if (knownSettings.TryGetValue(k, out var s))
						s.Clear();
					else
						values.Remove(k);
				}

				// clear settings on disk
				await WriteSettingsAsync();
			}

			// refresh properties from server
			await LoadSettingsFromServerAsync();
		} // func RefreshAsync

		public IEnumerable<KeyValuePair<string, string>> Query(params string[] filter)
		{
			if (filter == null || filter.Length == 0) // return all
			{
				// return known settings
				foreach (var cur in knownSettings.Values)
				{
					if (!cur.IsHidden)
						yield return new KeyValuePair<string, string>(cur.Key, cur.Value);
				}

				// return other settings
				foreach (var cur in values.Values)
				{
					if (!knownSettings.ContainsKey(cur.Name))
						yield return new KeyValuePair<string, string>(cur.Name, cur.Value);
				}
			}
			else
			{
				foreach (var k in filter)
				{
					if (TryGetProperty(k, out var v))
						yield return new KeyValuePair<string, string>(k, v);
				}
			}
		} // func Query

		public Task<int> UpdateAsync(params KeyValuePair<string, string>[] values)
		{
			foreach (var kv in values)
				GetSetting(kv.Key).Value = kv.Value;

			return WriteSettingsAsync();
		} // func UpdateAsync

		public bool TryGetProperty(string name, out string value)
		{
			if (knownSettings.TryGetValue(name, out var s))
			{
				value = s.Value;
				return true;
			}
			else if (values.TryGetValue(name, out var c))
			{
				value = c.Value;
				return true;
			}
			else
			{
				value = null;
				return false;
			}
		} // TryGetProperty

		#endregion
	} // class PpsBaseSettingsService

	#endregion
}
