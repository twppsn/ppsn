﻿#region -- copyright --
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
using Neo.IronLua;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using System.Xaml;
using TecWare.DE.Stuff;

namespace TecWare.PPSn.UI
{
	#region -- class LuaConvertExtension ----------------------------------------------

	///// <summary>Create a value LuaValueConverter with a ConvertExpression.</summary>
	//public class LuaConvertExtension : MarkupExtension
	//{
	//	/// <summary></summary>
	//	/// <param name="code"></param>
	//	public LuaConvertExtension(string code)
	//	{
	//		this.Code = code;
	//	} // ctor

	//	/// <summary></summary>
	//	/// <param name="serviceProvider"></param>
	//	/// <returns></returns>
	//	public override object ProvideValue(IServiceProvider serviceProvider)
	//	{
	//		var target = (IProvideValueTarget)serviceProvider.GetService(typeof(IProvideValueTarget));
	//		var property = target.TargetProperty as PropertyInfo;
	//		if (property == null)
	//			throw new ArgumentException("This markup is only allowed on properties.");

	//		if (!property.PropertyType.IsAssignableFrom(typeof(IValueConverter)))
	//			throw new ArgumentException("The property must except IValueConverter's.");

	//		return new LuaValueConverter() { ConvertExpression = Code };
	//	} // func ProvideValue

	//	/// <summary>ConvertExpression</summary>
	//	[ConstructorArgument("code")]
	//	public string Code { get; set; }
	//} // class LuaConvertExtension

	#endregion

	#region -- class Constant ---------------------------------------------------------

	/// <summary></summary>
	public sealed class Constant : MarkupExtension
	{
		/// <summary></summary>
		public Constant()
		{
		} // ctor

		/// <summary></summary>
		/// <param name="returnType"></param>
		/// <param name="value"></param>
		public Constant(string returnType, object value)
		{
			TypeName = returnType;
			Value = value;
		} // ctor

		/// <summary></summary>
		/// <param name="serviceProvider"></param>
		/// <returns></returns>
		public override object ProvideValue(IServiceProvider serviceProvider)
		{
			var type = Type;
			if (type == null)
			{
				if (TypeName == null)
				{
					var target = serviceProvider.GetService<IProvideValueTarget>(true);
					if (target.TargetProperty is PropertyInfo pi)
						type = pi.PropertyType;
				}
				else
					type = LuaType.GetType(TypeName);
			}
			return type == null ? Value : PpsWpfShell.ChangeTypeWithConverter(Value, type);
		} // func ProvideValue

		/// <summary></summary>
		public Type Type { get; set; } = null;
		/// <summary></summary>
		[ConstructorArgument("returnType")]
		public string TypeName { get; set; }
		/// <summary></summary>
		[ConstructorArgument("value")]
		public object Value { get; set; }
	} // class Constant

	#endregion

	#region -- class PpsTypedResourceKey ----------------------------------------------

	/// <summary>Resource key to mark shapes</summary>
	public abstract class PpsTypedResourceKey : ResourceKey
	{
		private readonly string name;

		/// <summary></summary>
		/// <param name="name"></param>
		protected PpsTypedResourceKey(string name)
		{
			this.name = name ?? throw new ArgumentNullException(nameof(name));
		} // ctor

		/// <summary></summary>
		/// <returns></returns>
		public override string ToString()
			=> GetType().Name + "@" + name;

		/// <summary></summary>
		/// <returns></returns>
		public sealed override int GetHashCode()
			=> GetType().GetHashCode() ^ name.GetHashCode();

		/// <summary>Compare resource keys.</summary>
		/// <param name="obj"></param>
		/// <returns></returns>
		public sealed override bool Equals(object obj)
			=> obj is PpsTypedResourceKey r ? r.name == name && r.GetType() == GetType() : base.Equals(obj);

		/// <summary>Resource name.</summary>
		public string Name => Name;
		/// <summary>Assembly is <c>null</c>.</summary>
		public sealed override Assembly Assembly => null;
	} // class PpsTypedResourceKey

	#endregion

	#region -- class PpsCollectTypedResources -----------------------------------------

	/// <summary></summary>
	public sealed class PpsCollectTypedResources : MarkupExtension
	{
		/// <summary></summary>
		/// <param name="resourceKeyType"></param>
		public PpsCollectTypedResources(Type resourceKeyType)
		{
			ResourceKeyType = resourceKeyType ?? throw new ArgumentNullException(nameof(resourceKeyType));
		} // ctor

		/// <summary></summary>
		/// <typeparam name="TKEY"></typeparam>
		/// <typeparam name="T"></typeparam>
		/// <param name="shell"></param>
		/// <returns></returns>
		[EditorBrowsable(EditorBrowsableState.Never)]
		public object GetResult<TKEY, T>(IPpsShell shell)
			where TKEY : ResourceKey
		{
			var result = shell.FindResourceByKey<TKEY, T>();

			// order result
			if (Order || Comparer != null)
			{
				if (Comparer != null)
				{
					if (Comparer is IComparer<T> comparer)
						result = result.OrderBy(c => c, comparer);
					else
						throw new ArgumentOutOfRangeException(nameof(Comparer), $"Comparer does not implement '{typeof(IComparer<T>).GetType().Name}'.");
				}
				else if (typeof(IComparable<T>).IsAssignableFrom(typeof(T)))
				{
					result = result.OrderBy(c => c);
				}
				else
					throw new ArgumentOutOfRangeException(nameof(Comparer), $"Resource does not implement '{typeof(IComparable<T>).GetType().Name}'.");
			}

			return CreateArray ? result.ToArray() : result;
		} // func GetResult

		/// <summary></summary>
		/// <param name="serviceProvider"></param>
		/// <returns></returns>
		public override object ProvideValue(IServiceProvider serviceProvider)
		{
			// find shell
			var rootObject = serviceProvider.GetService<IRootObjectProvider>(true);
			var shell = ((DependencyObject)rootObject.RootObject).GetControlService<IPpsShell>(true);

			// make select method
			var m = findResourceKeyMethodInfo.MakeGenericMethod(ResourceKeyType, ResourceType ?? typeof(object));
			return m.Invoke(this, new object[] { shell });
		} // func ProvideValue

		/// <summary>Resource key, which is selected.</summary>
		public Type ResourceKeyType { get; }

		/// <summary>Type of the returned resource</summary>
		public Type ResourceType { get; set; } = typeof(object);

		/// <summary>Return a ordered collection.</summary>
		public bool Order { get; set; } = false;
		/// <summary>Compare resources</summary>
		public IComparer Comparer { get; set; } = null;
		/// <summary>Return an array.</summary>
		public bool CreateArray { get; set; } = false;

		private static readonly MethodInfo findResourceKeyMethodInfo;

		static PpsCollectTypedResources()
		{
			findResourceKeyMethodInfo = typeof(PpsCollectTypedResources).GetMethod(nameof(GetResult)) ?? throw new ArgumentNullException(nameof(PpsCollectTypedResources.GetResult));
		}
	} // class PpsCollectTypedResources

	#endregion

	#region -- Color Markup Extensions ------------------------------------------------

	#region -- class ColorMarkupExtension ---------------------------------------------

	/// <summary></summary>
	public abstract class ColorMarkupExtension : MarkupExtension
	{
		/// <summary></summary>
		/// <param name="serviceProvider"></param>
		/// <returns></returns>
		protected abstract Color ProvideColor(IServiceProvider serviceProvider);

		/// <summary></summary>
		/// <param name="serviceProvider"></param>
		/// <returns></returns>
		public sealed override object ProvideValue(IServiceProvider serviceProvider)
		{
			var target = serviceProvider.GetService<IProvideValueTarget>(true);
			var color = ProvideColor(serviceProvider);

			return target.TargetProperty is System.Windows.DependencyProperty propertyInfo && propertyInfo.PropertyType == typeof(Brush)
				? (object)new SolidColorBrush(color)
				: color;
		} // func ProvideValue 
	} // class ColorMarkupExtension

	#endregion

	#region -- class AlphaBlendColor --------------------------------------------------

	/// <summary>Mixes two colors to one color.</summary>
	public sealed class AlphaBlendColor : ColorMarkupExtension
	{
		/// <summary></summary>
		/// <param name="serviceProvider"></param>
		/// <returns></returns>
		protected override Color ProvideColor(IServiceProvider serviceProvider)
		{
			var destinationPart = 1.0f - SourcePart;

			if (SourcePart < 0.0f)
				return Color.FromScRgb(Alpha, Source.ScR, Source.ScG, Source.ScB);
			else if (SourcePart > 1.0f)
				return Color.FromScRgb(Alpha, Destination.ScR, Destination.ScG, Destination.ScB);

			// the scale 0.0 - 1.0 does not map linearly to 0 - 255
			return Color.FromScRgb(
				Alpha,
				Source.ScR * SourcePart + Destination.ScR * destinationPart,
				Source.ScG * SourcePart + Destination.ScG * destinationPart,
				Source.ScB * SourcePart + Destination.ScB * destinationPart
			);
		} // func ProvideColor

		/// <summary>Source color</summary>
		public Color Source { get; set; } = Colors.Black;
		/// <summary>Destination color.</summary>
		public Color Destination { get; set; } = Colors.White;
		/// <summary>Distance to pick.</summary>
		public float SourcePart { get; set; } = 0.5f;
		/// <summary>Alpha value of the result.</summary>
		public float Alpha { get; set; } = 1.0f;
	} // class AlphaBlendColor

	#endregion

	#region -- class TransparencyResultColor ------------------------------------------

	/// <summary>Mixes two colors to one color.</summary>
	public sealed class TransparencyResultColor : ColorMarkupExtension
	{
		/// <summary></summary>
		/// <param name="serviceProvider"></param>
		/// <returns></returns>
		protected override Color ProvideColor(IServiceProvider serviceProvider)
		{
			var backgroundPart = 1.0f - Transparency;

			if (Transparency < 0.0f)
				return Color.FromScRgb(1f, BackColor.ScR, BackColor.ScG, BackColor.ScB);
			else if (Transparency > 1.0f)
				return Color.FromScRgb(1f, TransparentColor.ScR, TransparentColor.ScG, TransparentColor.ScB);

			return Color.FromArgb(
				255,
				(byte)(BackColor.R * backgroundPart + TransparentColor.R * Transparency),
				(byte)(BackColor.G * backgroundPart + TransparentColor.G * Transparency),
				(byte)(BackColor.B * backgroundPart + TransparentColor.B * Transparency)
			);
		} // func ProvideColor

		/// <summary>Background color</summary>
		public Color BackColor { get; set; } = Colors.Black;
		/// <summary>Transparent color</summary>
		public Color TransparentColor { get; set; } = Colors.White;
		/// <summary>Transparency value</summary>
		public float Transparency { get; set; } = 1.0f;
	} // class TransparencyResultColor

	#endregion

	#region -- class WeightColor ------------------------------------------------------

	/// <summary>Makes the color dark.</summary>
	public sealed class WeightColor : ColorMarkupExtension
	{
		/// <summary></summary>
		public WeightColor()
		{
		} // ctor

		/// <summary></summary>
		/// <param name="source"></param>
		public WeightColor(Color source)
		{
			this.Source = source;
		} // ctor

		/// <summary></summary>
		/// <param name="serviceProvider"></param>
		/// <returns></returns>
		protected override Color ProvideColor(IServiceProvider serviceProvider)
		{
			var a = Source.ScA;
			var r = Source.ScR;
			var g = Source.ScG;
			var b = Source.ScB;

			if (Factor < 0.0f)
				r = g = b = 0.0f;
			else if (Factor > 1.0f)
				r = g = b = 1.0f;
			else
			{
				for (var i = 0; i < Times; i++)
				{
					r += Factor * (1 - r);
					g += Factor * (1 - g);
					b += Factor * (1 - b);
				}
			}
			return Color.FromScRgb(a, r, g, b);
		} // func ProvideColor

		/// <summary>Source color.</summary>
		[ConstructorArgument("source")]
		public Color Source { get; set; } = Colors.Gray;

		/// <summary>Weight factor.</summary>
		public float Factor { get; set; } = 0.3f;
		/// <summary>Number of weight calculations.</summary>
		public int Times { get; set; } = 1;
	} // class WeightColor

	#endregion

	#region -- class GrayColor --------------------------------------------------------

	/// <summary>Makes the color dark.</summary>
	public sealed class GrayColor : ColorMarkupExtension
	{
		/// <summary></summary>
		/// <param name="source"></param>
		public GrayColor(Color source)
		{
			this.Source = source;
		} // ctor

		/// <summary></summary>
		/// <param name="serviceProvider"></param>
		/// <returns></returns>
		protected override Color ProvideColor(IServiceProvider serviceProvider)
		{
			var a = Source.ScA;
			var c = Source.ScR * 0.2126f + Source.ScG * 0.7152f + Source.ScB * 0.0722f;

			return Color.FromScRgb(a, c, c, c);
		} // func ProvideColor

		/// <summary>Source color</summary>
		[ConstructorArgument("source")]
		public Color Source { get; set; }
	} // class GrayColor

	#endregion

	#endregion
}
