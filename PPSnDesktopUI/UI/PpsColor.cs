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
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using TecWare.PPSn.Themes;

namespace TecWare.PPSn.UI
{
	#region -- class PpsColorExtension ------------------------------------------------

	/// <summary>Color extension.</summary>
	public sealed class PpsColorExtension : MarkupExtension
	{
		private readonly PpsColor color;
		private readonly float opacity = 1.0f;

		/// <summary>Create the color binding by name.</summary>
		/// <param name="colorName">Name of the color</param>
		public PpsColorExtension(object color)
		{
			if (color is string colorName)
				this.color = PpsColor.Get(colorName);
			else if (color is PpsColor tmp)
				this.color = tmp;
			else
				throw new ArgumentException(nameof(color));
		} // ctor

		private Exception GetProvideException()
			=> new XamlParseException($"Could not create resource expression for color: {color.Name}");

		private object GetColorResourceExpression(IServiceProvider serviceProvider)
			=> new DynamicResourceExtension(color.ColorKey).ProvideValue(serviceProvider);

		private object GetBrushResourceExpression(IServiceProvider serviceProvider)
			=> new DynamicResourceExtension(color.BrushKey).ProvideValue(serviceProvider);

		private object ProvideValue(IServiceProvider serviceProvider, Type propertyType)
		{
			if (propertyType.IsAssignableFrom(typeof(SolidColorBrush)))
			{
				if (opacity <= 0f)
					return Brushes.Transparent;
				else
				{
					if (opacity < 1.0)
					{
						var br = new SolidColorBrush();
						br.SetValue(SolidColorBrush.ColorProperty, GetColorResourceExpression(serviceProvider));
						br.SetValue(Brush.OpacityProperty, opacity);
						return br;
					}
					else
						return GetBrushResourceExpression(serviceProvider);
				}
			}
			else if (propertyType.IsAssignableFrom(typeof(Color)))
				return GetColorResourceExpression(serviceProvider);
			else
				return null;
		} // func ProvideValue

		/// <inheritdoc/>
		public override object ProvideValue(IServiceProvider serviceProvider)
		{
			if (serviceProvider.GetService(typeof(IProvideValueTarget)) is IProvideValueTarget target)
			{
				if (target.TargetProperty is DependencyProperty dp)
					return ProvideValue(serviceProvider, dp.PropertyType) ?? throw GetProvideException();
				else if (target.TargetProperty is PropertyInfo pi)
					return ProvideValue(serviceProvider, pi.PropertyType) ?? throw GetProvideException();
			}

			throw GetProvideException();
		} // func ProvideValue

		/// <summary>Color key.</summary>
		public PpsColor Color => color;
		/// <summary>Transparent value for brushes.</summary>
		public float Opacity => opacity;
	} // class PpsColorExtension

	#endregion

	#region -- class PpsColor ---------------------------------------------------------

	/// <summary>Pps color definition</summary>
	public sealed class PpsColor : IEquatable<PpsColor>
	{
		private readonly string name;
		private readonly Func<PpsColorTheme, Color> callbackValue;

		private readonly object colorKey;
		private readonly object brushKey;

		/// <summary>Define a color.</summary>
		/// <param name="name"></param>
		/// <param name="callbackValue"></param>
		public PpsColor(string name, Func<PpsColorTheme, Color> callbackValue)
		{
			this.name = name ?? throw new ArgumentNullException(nameof(name));
			this.callbackValue = callbackValue ?? throw new ArgumentNullException(nameof(callbackValue));

			RegisterColor(this);

			colorKey = PpsTheme.CreateColorKey(this);
			brushKey = PpsTheme.CreateBrushKey(this);
		} // ctor

		/// <inheritdoc/>
		public override string ToString()
			=> name;

		/// <inheritdoc/>
		public override int GetHashCode()
			=> name.GetHashCode();

		/// <inheritdoc/>
		public override bool Equals(object obj)
			=> obj is PpsColor color && Equals(color);

		/// <summary>Compare the color key.</summary>
		/// <param name="other"></param>
		/// <returns></returns>
		public bool Equals(PpsColor other)
			=> other.name == name;

		/// <summary>Get the themed color.</summary>
		/// <param name="theme"></param>
		/// <returns></returns>
		public Color GetColor(PpsColorTheme theme)
			=> theme.GetThemedColor(this);

		/// <summary>Get the color.</summary>
		/// <param name="theme"></param>
		/// <returns></returns>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public Color GetCallbackColor(PpsColorTheme theme)
			=> callbackValue(theme);

		/// <summary>Name of the color</summary>
		public string Name => name;

		/// <summary>Resource key for the <see cref="Color" /></summary>
		public object ColorKey => colorKey;
		/// <summary>Resource key for the <see cref="SolidColorBrush"/></summary>
		public object BrushKey => brushKey;

		/// <summary>Empty color</summary>
		public static Color Empty => Color.FromArgb(0, 0, 0, 0);

		// -- Static ----------------------------------------------------------

		private static readonly Dictionary<string, PpsColor> colors = new Dictionary<string, PpsColor>();

		private static void RegisterColor(PpsColor color)
			=> colors.Add(color.Name, color);

		/// <summary>Get color by name</summary>
		/// <param name="name"></param>
		/// <param name="color"></param>
		/// <returns></returns>
		public static bool TryGet(string name, out PpsColor color)
			=> colors.TryGetValue(name, out color);

		/// <summary>Get color by name</summary>
		/// <param name="name"></param>
		/// <returns></returns>
		public static PpsColor Get(string name)
			=> TryGet(name, out var color) ? color : throw new ArgumentOutOfRangeException(nameof(name), name, $"Unknown color name: {name}");

		/// <summary>Defined colors</summary>
		public static IEnumerable<PpsColor> Defined
			=> colors.Values;
	} // class PpsColor

	#endregion

	#region -- class PpsColorTheme ----------------------------------------------------

	/// <summary>Color theme for the application</summary>
	public class PpsColorTheme : IEnumerable<PpsColor>
	{
		private readonly string name;
		private readonly Dictionary<string, Color> themedColor = new Dictionary<string, Color>();

		/// <summary></summary>
		public PpsColorTheme(string name, IEnumerable<KeyValuePair<string, Color>> colors)
		{
			this.name = name ?? throw new ArgumentNullException(nameof(name));

			if (colors != null)
			{
				foreach (var c in colors)
					themedColor.Add(c.Key, c.Value);
			}
		} // ctor

		/// <summary>Returns the themed color.</summary>
		/// <param name="color"></param>
		/// <returns></returns>
		public Color GetThemedColor(PpsColor color)
			=> themedColor.TryGetValue(color.Name, out var cl) ? cl : color.GetCallbackColor(this);

		/// <summary>Mixes two colors to one color.</summary>
		/// <param name="backColor"></param>
		/// <param name="transparentColor"></param>
		/// <param name="opacity"></param>
		/// <returns></returns>
		public Color GetTransparencyColor(PpsColor backColor, PpsColor transparentColor, float opacity)
			=> TransparencyResultColor.GetColor(backColor.GetColor(this), transparentColor.GetColor(this), opacity);

		/// <summary>Mixes two colors to one color.</summary>
		/// <param name="source">Source color.</param>
		/// <param name="destination">Destination color.</param>
		/// <param name="sourcePart">Distance to pick.</param>
		/// <param name="alpha">Alpha value of the result.</param>
		/// <returns></returns>
		internal Color GetAlphaBlendColor(PpsColor source, PpsColor destination, float sourcePart = 0.5f, float alpha = 1.0f)
			=> AlphaBlendColor.GetColor(source.GetColor(this), destination.GetColor(this), sourcePart, alpha);

		/// <summary>Enumerates all color keys.</summary>
		/// <returns></returns>
		public IEnumerator<PpsColor> GetEnumerator()
			=> PpsColor.Defined.GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator()
			=> GetEnumerator();

		/// <summary>Name of the theme</summary>
		public string Name => name;

		/// <summary>Default color scheme.</summary>
		public static PpsColorTheme Default { get; } = new PpsColorTheme(nameof(Default), null);
	} // class PpsColorTheme

	#endregion

	#region -- class PpsColorThemeKey -------------------------------------------------

	/// <summary>Marks theme resources.</summary>
	public sealed class PpsColorThemeKey : PpsTypedResourceKey
	{
		public PpsColorThemeKey(string name) 
			: base(name)
		{
		} // ctor
	} // class PpsColorThemeKey

	#endregion

	#region -- class PpsColorThemeFactory ---------------------------------------------

	/// <summary>Theme factory.</summary>
	public class PpsColorThemeFactory : Dictionary<string, Color>
	{
		/// <summary>Name of the resource</summary>
		public string DisplayName { get; set; } = null;
	} // class PpsColorThemeFactory

	#endregion
}
