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
using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using TecWare.PPSn.UI;

namespace TecWare.PPSn.Themes
{
	#region -- class PpsTheme ---------------------------------------------------------

	public static class PpsTheme
	{
		#region -- class PpsThemeKey --------------------------------------------------

		private sealed class PpsThemeKey : ResourceKey
		{
			private readonly string name;

			public PpsThemeKey(string name)
			{
				this.name = name ?? throw new ArgumentNullException(nameof(name));
				namedKeys[name] = this;
			} // ctor

			public override string ToString()
				=> "PPSn:" + name;

			public override bool Equals(object obj)
				=> obj is PpsThemeKey k && k.name == name;

			public override int GetHashCode() 
				=> name.GetHashCode() ^ typeof(PpsTheme).GetHashCode();

			public string Name => name;
			public override Assembly Assembly => typeof(PpsTheme).Assembly;
		} // class PpsThemeKey

		#endregion

		#region -- enum PpsColorType --------------------------------------------------

		private enum PpsColorType
		{
			Color,
			Brush
		} // enum PpsColorType

		#endregion

		#region -- class PpsColorTypeKey ----------------------------------------------

		private sealed class PpsColorTypeKey
		{
			private readonly PpsColorType type;
			private readonly PpsColor color;

			public PpsColorTypeKey(PpsColorType type, PpsColor color)
			{
				this.type = type;
				this.color = color ?? throw new ArgumentNullException(nameof(color));
			} // ctor

			public override string ToString()
				=> "PPSn" + color.Name + type.ToString();

			public override int GetHashCode()
				=> type.GetHashCode() ^ color.GetHashCode();

			public override bool Equals(object obj)
				=> obj is PpsColorTypeKey k && k.type == type && k.color.Equals(color);

			public PpsColorType Type => type;
			public PpsColor Color => color;
		} // class PpsColorType

		#endregion

		#region -- class PpsColorThemeDictionary --------------------------------------

		private sealed class PpsColorThemeDictionary : ResourceDictionary
		{
			private readonly PpsColorTheme theme;

			public PpsColorThemeDictionary(PpsColorTheme theme)
			{
				this.theme = theme ?? throw new ArgumentNullException(nameof(theme));

				foreach (var c in theme)
				{
					Add(c.BrushKey, c);
					Add(c.BrushKey.ToString(), c.BrushKey);
					Add(c.ColorKey, c);
					Add(c.ColorKey.ToString(), c.ColorKey);
				}
			} // ctor

			protected override void OnGettingValue(object key, ref object value, out bool canCache)
			{
				if (key is string && value is PpsColorTypeKey valueColorType)
				{
					value = this[valueColorType];
					canCache = true;
				}
				else if (key is PpsColorTypeKey colorType && value is PpsColor colorKey)
				{
					switch (colorType.Type)
					{
						case PpsColorType.Brush:
							value = new SolidColorBrush(colorKey.GetColor(theme));
							break;
						default:
							value = colorKey.GetColor(theme);
							break;
					}

					// freeze value
					if (value is Freezable f && f.CanFreeze)
						f.Freeze();
					canCache = true;
				}
				else
					base.OnGettingValue(key, ref value, out canCache);
			} // func OnGettingValue
		} // class PpsColorThemeDictionary

		#endregion

		private static readonly Dictionary<string, PpsThemeKey> namedKeys = new Dictionary<string, PpsThemeKey>();

		internal static object CreateColorKey(PpsColor color)
			=> new PpsColorTypeKey(PpsColorType.Color, color);

		internal static object CreateBrushKey(PpsColor color)
			=> new PpsColorTypeKey(PpsColorType.Brush, color);

		/// <summary>Update theme.</summary>
		/// <param name="theme"></param>
		public static void UpdateThemedDictionary(Collection<ResourceDictionary> resources, PpsColorTheme theme)
		{
			for (var i = 0; i < resources.Count; i++)
			{
				if (resources[i] is PpsColorThemeDictionary)
				{
					resources[i] = new PpsColorThemeDictionary(theme);
					return;
				}
			}
			resources.Add(new PpsColorThemeDictionary(theme));
		} // proc UpdateThemedDictionary

		/// <summary>Resolves the resource-key from a name.</summary>
		/// <param name="name"></param>
		/// <param name="resourceKey"></param>
		/// <returns></returns>
		public static bool TryGetNamedResourceKey(string name, out object resourceKey)
		{
			if (namedKeys.TryGetValue(name, out var key))
			{
				resourceKey = key;
				return true;
			}
			else
			{
				resourceKey = null;
				return false;
			}
		} // func TryGetNamedResourceKey

		#region -- Colors -------------------------------------------------------------

		/// <summary>Contrast color for light.</summary>
		public static readonly PpsColor White = new PpsColor(nameof(White), theme => Color.FromArgb(255, 255, 255, 255));
		/// <summary>Contrast color for dark.</summary>
		public static readonly PpsColor Black = new PpsColor(nameof(Black), theme => Color.FromArgb(255, 0, 0, 0));

		/// <summary>Application background color.</summary>
		public static readonly PpsColor Desktop = new PpsColor(nameof(Desktop), theme => Color.FromArgb(255, 255, 250, 240));
		/// <summary>Application foreground color.</summary>
		public static readonly PpsColor Accent = new PpsColor(nameof(Accent), theme => Color.FromArgb(255, 0, 0, 0));
		/// <summary>Application highlight color.</summary>
		public static readonly PpsColor Marker = new PpsColor(nameof(Marker), theme => Color.FromArgb(255, 90, 124, 54));

		/// <summary>Window title</summary>
		public static readonly PpsColor WindowActive = new PpsColor(nameof(WindowActive), Marker.GetColor);
		/// <summary>Window title bar background on active.</summary>
		public static readonly PpsColor WindowActiveGlow = new PpsColor(nameof(WindowActiveGlow), WindowActive.GetColor);
		/// <summary>Window title bar background on inactive.</summary>
		public static readonly PpsColor WindowInActiveGlow = new PpsColor(nameof(WindowInActiveGlow), theme => Color.FromArgb(255, 170, 170, 170));
		/// <summary>Window status bar.</summary>
		public static readonly PpsColor WindowHeader = new PpsColor(nameof(WindowHeader), theme => theme.GetTransparencyColor(WindowBackground, Black, 0.17f));
		/// <summary>Window foreground.</summary>
		public static readonly PpsColor WindowForeground = new PpsColor(nameof(WindowForeground), Accent.GetColor);
		/// <summary>Window  background.</summary>
		public static readonly PpsColor WindowBackground = new PpsColor(nameof(WindowBackground), Desktop.GetColor);
		/// <summary>Window status bar.</summary>
		public static readonly PpsColor WindowFooter = new PpsColor(nameof(WindowFooter), WindowActiveGlow.GetColor);

		/// <summary>todo: using</summary>
		public static readonly PpsColor Seperator = new PpsColor(nameof(Seperator), theme => theme.GetTransparencyColor(WindowBackground, WindowForeground, 0.1f));

		/// <summary>todo: using</summary>
		public static readonly PpsColor ControlBackground = new PpsColor(nameof(ControlBackground), White.GetColor);
		/// <summary>todo: using</summary>
		public static readonly PpsColor ControlFocusedBorder = new PpsColor(nameof(ControlFocusedBorder), Marker.GetColor);
		/// <summary>todo: using</summary>
		public static readonly PpsColor ControlNormalBorder = new PpsColor(nameof(ControlNormalBorder), theme => theme.GetTransparencyColor(Desktop, Accent, 0.25f));

		/// <summary>Background for standard button</summary>
		public static readonly PpsColor ButtonBackground = new PpsColor(nameof(ButtonBackground), theme => theme.GetTransparencyColor(WindowBackground, Accent, 0.11f));

		/// <summary>todo: using</summary>
		public static readonly PpsColor SelectionBar = new PpsColor(nameof(SelectionBar), theme => theme.GetTransparencyColor(ControlBackground, Marker, 0.35f));
		/// <summary>todo: using</summary>
		public static readonly PpsColor MouseOver = new PpsColor(nameof(MouseOver), theme => theme.GetTransparencyColor(ControlBackground, Marker, 0.35f));

		/// <summary>todo: using</summary>
		public static readonly PpsColor PopupBackground = new PpsColor(nameof(PopupBackground), theme => theme.GetAlphaBlendColor(Desktop, Black, sourcePart: 0.9f));
		/// <summary>todo: using</summary>
		public static readonly PpsColor PopupBorder = new PpsColor(nameof(PopupBorder), theme => theme.GetAlphaBlendColor(Desktop, Black, sourcePart: 0.5f));

		/// <summary>todo: using</summary>
		public static readonly PpsColor SideBarBackground = new PpsColor(nameof(SideBarBackground), theme => theme.GetTransparencyColor(Desktop, Black, 0.03f));

		#endregion

		#region -- Images -------------------------------------------------------------

		/// <summary>Close geometry</summary>
		public static readonly ResourceKey WindowClosePathGeometry = new PpsThemeKey("windowClose");
		/// <summary>Connecting geometry</summary>
		public static readonly ResourceKey ServerConnectingPathGeometry = new PpsThemeKey("serverConnecting");
		/// <summary>UnConnecting geometry</summary>
		public static readonly ResourceKey ServerUnConnectedPathGeometry = new PpsThemeKey("serverUnConnected");
		/// <summary>Connected geometry</summary>
		public static readonly ResourceKey ServerConnectedPathGeometry = new PpsThemeKey("serverConnected");

		/// <summary>ScannerActive geometry</summary>
		public static readonly ResourceKey ScannerActivePathGeometry = new PpsThemeKey("scannerActive");
		/// <summary>ScannerInActive geometry</summary>
		public static readonly ResourceKey ScannerInActivePathGeometry = new PpsThemeKey("scannerInActive");

		#endregion
	} // class PpsTheme

	#endregion
}
