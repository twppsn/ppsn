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
using System.Linq.Expressions;
using System.Reflection;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using System.Xaml;
using Neo.IronLua;
using TecWare.DE.Stuff;

namespace TecWare.PPSn.UI
{
	#region -- class LuaEventExtension --------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public interface ILuaEventSink
	{
		void CallMethod(string methodName, object[] args);
	} // interface ILuaEventSink

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class LuaEventExtension : MarkupExtension
	{
		private string methodName;

		public LuaEventExtension()
		{
			this.methodName = null;
		} // ctor

		public LuaEventExtension(string methodName)
		{
			this.methodName = methodName;
		} // ctor

		private T FindImplementation<T>(IRootObjectProvider root)
			where T : class
		{
			// check the object itself
			var r = root.RootObject as T;
			if (r != null)
				return r;

			// check the context
			var ctrl = root.RootObject as System.Windows.FrameworkElement;
			return ctrl?.DataContext as T;
		} // func FindImplementation

		private LambdaExpression GenerateCallMethod(EventInfo eventInfo, object eventSink, Type eventSinkType, MethodInfo callMethodInfo)
		{
			// prepare event signature
			var methodInfo = eventInfo.EventHandlerType.GetMethod("Invoke");
			var parameterInfo = methodInfo.GetParameters();
			var parameterExpressions = new ParameterExpression[parameterInfo.Length];

			for (var i = 0; i < parameterInfo.Length; i++)
				parameterExpressions[i] = Expression.Parameter(parameterInfo[i].ParameterType);

			var eventBody = Expression.Lambda(eventInfo.EventHandlerType,
				Expression.Call(
					Expression.Constant(eventSink, eventSinkType),
					callMethodInfo,
					Expression.Constant(methodName), Expression.NewArrayInit(typeof(object), parameterExpressions)
				),
				parameterExpressions
			);

			return eventBody;
		} // func GenerateCallMethod

		public override object ProvideValue(IServiceProvider serviceProvider)
		{
			var target = (IProvideValueTarget)serviceProvider.GetService(typeof(IProvideValueTarget));
			var root = (IRootObjectProvider)serviceProvider.GetService(typeof(IRootObjectProvider));

			// check that we have to provide a value for an event
			var eventInfo = target.TargetProperty as EventInfo;
			if (eventInfo == null)
				throw new ArgumentException("LuaEvent can only used with events.");

			// check for a ILuaEventSink implementation
			var eventSink = FindImplementation<ILuaEventSink>(root);
			if (eventSink != null) // bind event sink
			{
				return GenerateCallMethod(eventInfo, eventSink, typeof(ILuaEventSink), LuaEventSinkCallMethodMethodInfo).Compile();
			}
			else // check for a Table
			{
				var table = FindImplementation<LuaTable>(root);
				if (table != null) // bind table method
				{
					return GenerateCallMethod(eventInfo, table, typeof(LuaTable), LuaTableCallMemberMethodInfo).Compile();
				}
				else
					throw new ArgumentNullException("LuaEvent did not find ILuaEventSink or LuaTable.");
			}
		} // func ProvideValue

		[ConstructorArgument("methodName")]
		public string MethodName { get { return methodName; } set { methodName = value; } }

		private static MethodInfo LuaEventSinkCallMethodMethodInfo { get; }
		private static MethodInfo LuaTableCallMemberMethodInfo { get; }

		static LuaEventExtension()
		{
			LuaEventSinkCallMethodMethodInfo = Procs.GetMethod(typeof(ILuaEventSink), nameof(ILuaEventSink.CallMethod), typeof(string), typeof(object[]));
			LuaTableCallMemberMethodInfo = Procs.GetMethod(typeof(LuaTable), nameof(LuaTable.CallMember), typeof(string), typeof(object[]));
		} // sctor
	} // class LuaEventExtension

	#endregion

	#region -- class LuaConvertExtension ------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class LuaConvertExtension : MarkupExtension
	{
		private string code;

		public LuaConvertExtension(string code)
		{
			this.code = code;
		} // ctor

		public override object ProvideValue(IServiceProvider serviceProvider)
		{
			var target = (IProvideValueTarget)serviceProvider.GetService(typeof(IProvideValueTarget));
			var property = target.TargetProperty as PropertyInfo;
			if (property == null)
				throw new ArgumentException("This markup is only allowed on properties.");

			if (!property.PropertyType.IsAssignableFrom(typeof(IValueConverter)))
				throw new ArgumentException("The property must except IValueConverter's.");

			return new LuaValueConverter() { ConvertExpression = code };
		} // func ProvideValue

		[ConstructorArgument("code")]
		public string Code { get { return code; } set { code = value; } }
	} // class LuaConvertExtension

	#endregion

	#region -- class AlphaBlendColor ----------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Mixes two colors to one color.</summary>
	public class AlphaBlendColor : MarkupExtension
	{
		public AlphaBlendColor()
		{
		} // ctor

		public override object ProvideValue(IServiceProvider serviceProvider)
		{
			var destinationPart = 1.0f - SourcePart;

			if (SourcePart < 0.0f)
				return ProvideColorValue(serviceProvider, Color.FromScRgb(Alpha, Source.ScR, Source.ScG, Source.ScB));
			else if (SourcePart > 1.0f)
				return ProvideColorValue(serviceProvider, Color.FromScRgb(Alpha, Destination.ScR, Destination.ScG, Destination.ScB));
			// the scale 0.0 - 1.0 does not map linearly to 0 - 255
			var color = Color.FromScRgb(
				Alpha,
				Source.ScR * SourcePart + Destination.ScR * destinationPart,
				Source.ScG * SourcePart + Destination.ScG * destinationPart,
				Source.ScB * SourcePart + Destination.ScB * destinationPart
			);
			return ProvideColorValue(serviceProvider, color);
		} // func ProvideValue

		public Color Source { get; set; } = Colors.Black;
		public Color Destination { get; set; } = Colors.White;
		public float SourcePart { get; set; } = 0.5f;
		public float Alpha { get; set; } = 1.0f;

		internal static object ProvideColorValue(IServiceProvider sp, Color color)
		{
			var target = (IProvideValueTarget)sp.GetService(typeof(IProvideValueTarget));
			var propertyInfo = target.TargetProperty as System.Windows.DependencyProperty;

			if (propertyInfo != null && propertyInfo.PropertyType == typeof(Brush))
				return new SolidColorBrush(color);
			else
				return color;
		} // func ProvideColorValue
	} // class AlphaBlendColor

	#endregion

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Mixes two colors to one color.</summary>
	public class TransparencyResultColor : MarkupExtension
	{
		public TransparencyResultColor()
		{
		} // ctor

		public override object ProvideValue(IServiceProvider serviceProvider)
		{
			var backgroundPart = 1.0f - Transpareny;

			if (Transpareny < 0.0f)
				return AlphaBlendColor.ProvideColorValue(serviceProvider, Color.FromScRgb(1f, BackColor.ScR, BackColor.ScG, BackColor.ScB));
			else if (Transpareny > 1.0f)
				return AlphaBlendColor.ProvideColorValue(serviceProvider, Color.FromScRgb(1f, TransparentColor.ScR, TransparentColor.ScG, TransparentColor.ScB));
			var color = Color.FromArgb(
				255,
				(byte)(BackColor.R * backgroundPart + TransparentColor.R * Transpareny),
				(byte)(BackColor.G * backgroundPart + TransparentColor.G * Transpareny),
				(byte)(BackColor.B * backgroundPart + TransparentColor.B * Transpareny)
			);
			return AlphaBlendColor.ProvideColorValue(serviceProvider, color);
		} // func ProvideValue

		public Color BackColor { get; set; } = Colors.Black;
		public Color TransparentColor { get; set; } = Colors.White;
		public float Transpareny { get; set; } = 1.0f;

	} // class TransparencyResultColor

	#region -- class WeightColor --------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Makes the color dark.</summary>
	public class WeightColor : MarkupExtension
	{
		public WeightColor()
		{
		} // ctor

		public WeightColor(Color source)
		{
			this.Source = source;
		} // ctor

		public override object ProvideValue(IServiceProvider serviceProvider)
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
				for (int i = 0; i < Times; i++)
				{
					r += Factor * (1 - r);
					g += Factor * (1 - g);
					b += Factor * (1 - b);
				}
			}
			return AlphaBlendColor.ProvideColorValue(serviceProvider, Color.FromScRgb(a, r, g, b));
		} // func ProvideValue

		[ConstructorArgument("source")]
		public Color Source { get; set; } = Colors.Gray;

		public float Factor { get; set; } = 0.3f;
		public int Times { get; set; } = 1;
	} // class WeightColor

	#endregion

	#region -- class GrayColor ----------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Makes the color dark.</summary>
	public class GrayColor : MarkupExtension
	{
		public GrayColor()
		{
		} // ctor

		public GrayColor(Color source)
		{
			this.Source = source;
		} // ctor

		public override object ProvideValue(IServiceProvider serviceProvider)
		{
			var a = Source.ScA;
			var c = Source.ScR * 0.2126f + Source.ScG * 0.7152f + Source.ScB * 0.0722f;

			return AlphaBlendColor.ProvideColorValue(serviceProvider, Color.FromScRgb(a, c, c, c));
		} // func ProvideValue

		[ConstructorArgument("source")]
		public Color Source { get; set; }
	} // class GrayColor

	#endregion

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Binding ImageSourceName.</summary>
	[MarkupExtensionReturnType(typeof(ImageSource))]
	public class PpsImageStaticResourceBinding : System.Windows.StaticResourceExtension
	{
		public Binding binding { get; set; }
		private static readonly System.Windows.DependencyProperty dummyProperty;

		public PpsImageStaticResourceBinding(Binding binding)
		{
			this.binding = binding;
			this.binding.Mode = BindingMode.OneWay;
		}

		static PpsImageStaticResourceBinding()
		{
			dummyProperty = System.Windows.DependencyProperty.RegisterAttached("Dummy", typeof(Object), typeof(System.Windows.DependencyObject), new System.Windows.UIPropertyMetadata(null));
		}

		public override object ProvideValue(IServiceProvider serviceProvider)
		{
			var target = (IProvideValueTarget)serviceProvider.GetService(typeof(IProvideValueTarget));
			var targetObject = (System.Windows.FrameworkElement)target.TargetObject;

			// simuliere Binding
			binding.Source = targetObject.DataContext;
			var dummyDO = new System.Windows.DependencyObject();

			BindingOperations.SetBinding(dummyDO, dummyProperty, binding);
			// todo: checken ob die die source das object hat
			try
			{
				ResourceKey = dummyDO.GetValue(dummyProperty);
				return base.ProvideValue(serviceProvider);
			}
			catch
			{
				return null;
			}
		}
	} // class PpsImageStaticResourceBinding

}
