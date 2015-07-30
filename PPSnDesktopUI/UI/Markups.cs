using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Markup;
using System.Xaml;

namespace TecWare.PPSn.UI
{
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

		public override object ProvideValue(IServiceProvider serviceProvider)
		{
			var target = (IProvideValueTarget)serviceProvider.GetService(typeof(IProvideValueTarget));
			var root = (IRootObjectProvider)serviceProvider.GetService(typeof(IRootObjectProvider));

			// check if the root is can call lua methods
			var eventSink = root.RootObject as ILuaEventSink;
			if (eventSink == null)
				throw new ArgumentNullException("LuaEvent did not find ILuaEventSink.");

			var eventInfo = target.TargetProperty as EventInfo;
			if (eventInfo == null)
				throw new ArgumentException("LuaEvent can only used with events.");

			var methodInfo = eventInfo.EventHandlerType.GetMethod("Invoke");
			var parameterInfo = methodInfo.GetParameters();
			var parameterExpressions = new ParameterExpression[parameterInfo.Length];

			for (int i = 0; i < parameterInfo.Length; i++)
				parameterExpressions[i] = Expression.Parameter(parameterInfo[i].ParameterType);

			var callEventMethodInfo = typeof(ILuaEventSink).GetRuntimeMethod("CallMethod", new Type[] { typeof(string), typeof(object[]) });
			var eventBody = Expression.Lambda(eventInfo.EventHandlerType,
				Expression.Call(
					Expression.Constant(eventSink),
					callEventMethodInfo,
					Expression.Constant(methodName), Expression.NewArrayInit(typeof(object), parameterExpressions)
				),
				parameterExpressions);
			
			return eventBody.Compile();
		} // func ProvideValue

		[ConstructorArgument("methodName")]
		public string MethodName { get { return methodName; } set { methodName = value; } }
	} // class LuaEventExtension
}
