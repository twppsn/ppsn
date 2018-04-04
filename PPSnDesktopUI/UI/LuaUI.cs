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
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Xaml;
using System.Xaml.Schema;
using Neo.IronLua;
using TecWare.DE.Data;
using TecWare.DE.Stuff;
using TecWare.PPSn.Controls;
using TecWare.PPSn.Data;
using static System.Linq.Expressions.Expression;

namespace TecWare.PPSn.UI
{
	#region -- interface ILuaWpfScope -------------------------------------------------

	/// <summary>Base Scope interface.</summary>
	public interface ILuaWpfScope : IServiceProvider
	{
		/// <summary>Access to the parent scope.</summary>
		ILuaWpfScope ParentScope { get; }
		/// <summary>Access the ui.</summary>
		LuaUI UI { get; }
	} // interface ILuaWpfScope

	#endregion

	#region -- class PpsDataFieldInfo -------------------------------------------------

	/// <summary></summary>
	public class PpsDataFieldInfo
	{
		/// <summary></summary>
		/// <param name="fieldInfo"></param>
		/// <param name="source"></param>
		public PpsDataFieldInfo(IDataColumn fieldInfo, object source = null)
		{
			this.FieldInfo = fieldInfo ?? throw new ArgumentNullException(nameof(fieldInfo));
			this.Source = source;
		} // ctor

		/// <summary></summary>
		public IDataColumn FieldInfo { get; private set; }
		/// <summary></summary>
		public object Source { get; private set; }
	} // struct PpsDataFieldInfo

	#endregion

	#region -- interface IPpsDataFieldResolver ----------------------------------------

	/// <summary>Marks a datatable scope assignment</summary>
	public interface IPpsDataFieldResolver
	{
		/// <summary></summary>
		/// <param name="fieldExpression"></param>
		PpsDataFieldInfo ResolveColumn(string fieldExpression);
	} // interface IPpsDataFieldResolver

	#endregion

	#region -- class PpsDataSetResolver -----------------------------------------------

	internal class PpsDataSetResolver : IPpsDataFieldResolver
	{
		private readonly PpsDataSetDefinition dataset;
		private readonly object source;

		public PpsDataSetResolver(PpsDataSetDefinition dataset, object source)
		{
			this.dataset = dataset ?? throw new ArgumentNullException(nameof(dataset));
			this.source = source;
		}

		public PpsDataFieldInfo ResolveColumn(string fieldExpression)
		{
			return null;
		} // func ResolveColumn
	} // class PpsDataSetResolver

	#endregion

	#region -- class PpsDataTableResolver ---------------------------------------------

	internal class PpsDataTableResolver : IPpsDataFieldResolver
	{
		private readonly PpsDataTableDefinition table;
		private readonly object source;

		public PpsDataTableResolver(PpsDataTableDefinition table, object source)
		{
			this.table = table;
			this.source = source;
		}

		public PpsDataFieldInfo ResolveColumn(string fieldExpression)
		{
			var idx = table.FindColumnIndex(fieldExpression);
			if (idx != -1)
				return new PpsDataFieldInfo(table.Columns[idx], source);
			return null;
		} // func ResolveColumn
	} // class PpsDataTableResolver

	#endregion

	#region -- class LuaWpfCreator ----------------------------------------------------

	/// <summary>Table to create new wpf classes.</summary>
	public class LuaWpfCreator<T> : LuaTable, ILuaWpfScope
		where T : class
	{
		// it could be also implemented directly through the dynamic language runtime.
		// currenlty, there is no strong reason for the LuaTable inheritance.

		#region -- class LuaWpfServiceProvider ----------------------------------------

		private sealed class LuaWpfServiceProvider : IServiceProvider,
			IProvideValueTarget,
			IRootObjectProvider,
			ITypeDescriptorContext,
			IAmbientProvider,
			IXamlSchemaContextProvider,
			IXamlTypeResolver,
			IXamlNamespaceResolver
		{
			private readonly LuaWpfCreator<T> scope;
			private readonly XamlMember member;

			public LuaWpfServiceProvider(LuaWpfCreator<T> scope, XamlMember member)
			{
				this.scope = scope ?? throw new ArgumentNullException(nameof(scope));
				this.member = member;
			} // ctor

			#region -- interface IProvideValueTarget, IRootObjectProvider -------------

			public object TargetObject => scope.Instance;
			public object TargetProperty
				=> member is IProvideValueTarget propertyProvider
					? propertyProvider.TargetProperty
					: member.UnderlyingMember;

			public object RootObject => scope.instance;

			#endregion

			#region -- interface IProvideValueTarget ----------------------------------

			public IContainer Container => null;

			public object Instance => scope.Instance;

			public PropertyDescriptor PropertyDescriptor => null;

			public void OnComponentChanged() { }
			public bool OnComponentChanging() => false;

			#endregion

			#region -- interface IProvideValueTarget ----------------------------------

			public IEnumerable<AmbientPropertyValue> GetAllAmbientValues(IEnumerable<XamlType> ceilingTypes, bool searchLiveStackOnly, IEnumerable<XamlType> types, params XamlMember[] properties)
				=> scope.FindAllAmbientValues(ceilingTypes, searchLiveStackOnly, types, properties);

			public IEnumerable<AmbientPropertyValue> GetAllAmbientValues(IEnumerable<XamlType> ceilingTypes, params XamlMember[] properties)
				=> scope.FindAllAmbientValues(ceilingTypes, false, null, properties);

			public AmbientPropertyValue GetFirstAmbientValue(IEnumerable<XamlType> ceilingTypes, params XamlMember[] properties)
				=> scope.FindAllAmbientValues(ceilingTypes, false, null, properties).FirstOrDefault();

			public IEnumerable<object> GetAllAmbientValues(params XamlType[] types)
				=> from o in scope.FindAllAmbientValues(null, false, types, null) select o.Value;

			public object GetFirstAmbientValue(params XamlType[] types)
				=> scope.FindAllAmbientValues(null, false, types, null).FirstOrDefault()?.Value;

			#endregion

			#region -- interface IXamlSchemaContextProvider, IXamlTypeResolver, IXamlNamespaceResolver --

			public Type Resolve(string qualifiedTypeName)
				=> throw new NotImplementedException();

			public string GetNamespace(string prefix)
				=> throw new NotImplementedException();

			public IEnumerable<NamespaceDeclaration> GetNamespacePrefixes()
				=> throw new NotImplementedException();

			public XamlSchemaContext SchemaContext => scope.type?.SchemaContext ?? scope.ui.SchemaContext;

			#endregion

			public object GetService(Type serviceType)
			{
				if (serviceType == typeof(IProvideValueTarget)
					|| serviceType == typeof(ITypeDescriptorContext)
					|| serviceType == typeof(IAmbientProvider)
					|| serviceType == typeof(IXamlSchemaContextProvider)
					|| serviceType == typeof(IXamlTypeResolver)
					|| serviceType == typeof(IXamlTypeResolver)
					|| serviceType == typeof(IXamlNamespaceResolver))
					return this;
				else
					return scope.GetService(serviceType);
			} // func GetService
		} // class LuaWpfServiceProvider

		#endregion

		private readonly LuaUI ui;
		private readonly ILuaWpfScope parentScope;

		private readonly XamlType type;
		private T instance;

		private bool scopeFinished = false; // marks the scope as created, is set after the first call

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary></summary>
		/// <param name="ui"></param>
		/// <param name="instance"></param>
		public LuaWpfCreator(LuaUI ui, T instance)
			: this(ui, ui.GetXamlType(typeof(T)), instance)
		{
		} // ctor

		/// <summary></summary>
		/// <param name="ui"></param>
		/// <param name="type"></param>
		/// <param name="instance"></param>
		public LuaWpfCreator(LuaUI ui, XamlType type, T instance)
		{
			this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
			this.parentScope = ui.CurrentScope;
			this.type = type;
			this.instance = instance;

			ui.PushScope(this);
		} // ctor

		private void CreateDefaultInstance()
		{
			instance = (T)type.Invoker.CreateInstance(Array.Empty<object>());
		} // proc CreateDefaultInstance

		/// <summary>Gets called, if the scope gets finished.</summary>
		protected virtual void OnFinished() { }

		/// <summary></summary>
		/// <returns></returns>
		public T Finish()
			=> (T)FinishScope();

		private object FinishScope()
		{
			if (scopeFinished)
				throw new InvalidOperationException("scope error (finish twice)"); // todo:

			ui.PopScope(this);
			scopeFinished = true;

			OnFinished();

			return Instance;
		} // proc FinishScope

		#endregion

		#region -- FindAllAmbientValues -----------------------------------------------

		private IEnumerable<AmbientPropertyValue> FindAllAmbientValues(IEnumerable<XamlType> ceilingTypes, bool searchLiveStackOnly, IEnumerable<XamlType> types, params XamlMember[] properties)
		{
			if (instance == null)
				CreateDefaultInstance();

			// find types
			if (types != null)
			{
				foreach (var t in types)
				{
					if (type.CanAssignTo(t))
						yield return new AmbientPropertyValue(null, instance);
				}
			}

			// find properties
			if (properties != null)
			{
				foreach (var p in properties)
				{
					if (type.CanAssignTo(p.DeclaringType))
					{
						if (instance is IQueryAmbient qa && qa.IsAmbientPropertyAvailable(p.Name))
							yield return new AmbientPropertyValue(p, p.Invoker.GetValue(instance));
					}
				}
			}
		} // func FindAllAmbientValues

		#endregion

		#region -- OnCall, OnIndex, OnNewIndex ----------------------------------------

		private XamlMember GetXamlAttachedMember(string typeName, string memberName)
		{
			var attachedType = type.SchemaContext.GetXamlType(new XamlTypeName(type.PreferredXamlNamespace, typeName));
			if (attachedType == null)
				throw new ArgumentNullException(nameof(typeName), $"Could not resolve '{typeName}'.");
			return attachedType.GetAttachableMember(memberName);
		} // func GetXamlAttachedMember

		private XamlMember GetXamlMember(string memberName)
		{
			// check for attached property
			var attachedPos = memberName.IndexOf('.');
			if (attachedPos == -1)
				return type.GetMember(memberName);
			
			return GetXamlAttachedMember(memberName.Substring(0, attachedPos), memberName.Substring(attachedPos + 1));
		} // func GetXamlMember

		private void SetXamlMemberValue(XamlMember xamlMember, object value)
		{
			// check if the values is an extension
			if (value is MarkupExtension m)
				value = m.ProvideValue(new LuaWpfServiceProvider(this, xamlMember));

			// convert the value for events
			if (xamlMember.IsEvent)
			{
				var eventHandlerType = xamlMember.Type.UnderlyingType;
				var eventHandlerInvokeMethodInfo = eventHandlerType.GetMethod("Invoke");
				var eventHandlerInvokeParameters = eventHandlerInvokeMethodInfo.GetParameters();
				var eventHandlerInvokeExpressions = new ParameterExpression[eventHandlerInvokeParameters.Length];

				// map arguments
				for (var i = 0; i < eventHandlerInvokeParameters.Length; i++)
					eventHandlerInvokeExpressions[i] = Parameter(eventHandlerInvokeParameters[i].ParameterType, eventHandlerInvokeParameters[i].Name);

				// Lua.RtInvoke(value, new object[] { arg0, arg1, ... });
				var exprCaller = Lambda(eventHandlerType,
					Call(
						luaRtInvokeMethodInfo,
						Constant(value, typeof(object)),
						NewArrayInit(typeof(object), eventHandlerInvokeExpressions)
					),
					eventHandlerInvokeExpressions
				);

				// compile the wrapper
				value = exprCaller.Compile();
			}
			else if (value != null) // convert the value
			{
				if (!(value is System.Windows.Expression)) // expression we do not convert expression
				{
					var xamlValueType = type.SchemaContext.GetXamlType(value.GetType());
					if (!xamlValueType.CanAssignTo(xamlMember.Type) && xamlMember.TypeConverter != null) // is there a converter
					{
						if (xamlMember.TypeConverter.ConverterType != null) // real converter
						{
							try
							{
								value = xamlMember.TypeConverter.ConverterInstance.ConvertFrom(new LuaWpfServiceProvider(this, xamlMember), CultureInfo.InvariantCulture, value);
							}
							catch (Exception e)
							{
								throw new FormatException($"{type.Name}.{xamlMember.Name} could not set with '{value?.ToString()}'.", e);
							}
						}
						else if (xamlMember.TypeConverter.TargetType != null) // simple converter
							value = Procs.ChangeType(value, xamlMember.TypeConverter.TargetType.UnderlyingType);
					}
				}
			}

			// finally set the value
			xamlMember.Invoker.SetValue(Instance, value);
		} // func SetXamlMemberValue

		/// <summary></summary>
		/// <param name="t"></param>
		public void SetTableMembers(LuaTable t)
		{
			// call set member for all value
			foreach (var kv in t.Values)
				SetValue(kv.Key, kv.Value);
		} // proc SetTableMembers

		private void CreateInstanceWithArguments(object[] args)
		{
			if (instance != null)
				throw new ArgumentException("Instance already constructed.");

			// convert the position arguments
			var argumentTypes = type.GetPositionalParameters(args.Length);
			var arguments = new object[argumentTypes.Count];
			for (var i = 0; i < arguments.Length; i++)
			{
				var value = args[i];
				if (value != null)
				{
					var xamlValueType = type.SchemaContext.GetXamlType(args[i].GetType());
					var xamlTypeTo = argumentTypes[i];
					if (!xamlValueType.CanAssignTo(xamlTypeTo) && xamlTypeTo.TypeConverter != null) // is there a converter
					{
						if (xamlTypeTo.TypeConverter != null) // real converter
							value = xamlTypeTo.TypeConverter.ConverterInstance.ConvertFrom(new LuaWpfServiceProvider(this, null), CultureInfo.InvariantCulture, value);
						else if (xamlTypeTo.TypeConverter.TargetType != null) // simple converter
							value = Procs.ChangeType(value, xamlTypeTo.TypeConverter.TargetType.UnderlyingType);
					}
				}
				arguments[i] = value;
			}

			instance = (T)type.Invoker.CreateInstance(arguments);
		} // proc CreateInstanceWithArguments

		/// <summary>Initialize the instance with one LuaTable or use the contructor.</summary>
		/// <param name="args"></param>
		/// <param name="offset"></param>
		/// <param name="count"></param>
		/// <returns>Instance of the constructed instance.</returns>
		protected virtual void OnCall(object[] args, int offset, int count)
		{
			if (count == 1 && args[offset] is LuaTable t)
				SetTableMembers(t);
			else if (args.Length > 0)
				CreateInstanceWithArguments(args);
		} // func OnCall

		/// <summary>Call closes the scope.</summary>
		/// <param name="args"></param>
		/// <returns></returns>
		protected sealed override LuaResult OnCall(object[] args)
		{
			if (args.Length == 0)
				return new LuaResult(FinishScope());
			else if (args[0] == LuaUI.ReturnSelf) // called marked to not finish anything
			{
				OnCall(args, 1, args.Length - 1);
				return new LuaResult(this);
			}
			else
			{
				OnCall(args, 0, args.Length);
				return new LuaResult(FinishScope());
			}
		} // proc OnCall

		/// <summary>Get the property of the instance.</summary>
		/// <param name="key">Member of the instance</param>
		/// <returns></returns>
		protected override object OnIndex(object key)
		{
			if (key is string memberName)
			{
				var xamlMember = GetXamlMember(memberName);
				if (xamlMember == null)
					return null;

				return xamlMember.Invoker.GetValue(Instance);
			}
			else
				return null;
		} // func OnIndex

		/// <summary>Set a property to the instance.</summary>
		/// <param name="key"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		protected override bool OnNewIndex(object key, object value)
		{
			if (key == null)
				throw new ArgumentNullException(nameof(key));
			else if (key is string memberName)
			{
				var xamlMember = GetXamlMember(memberName);
				if (xamlMember == null)
					throw new ArgumentException($"Could not resolve member '{memberName}'.", nameof(key));

				SetXamlMemberValue(xamlMember, value);
			}
			else if (key is DependencyProperty property)
			{
				return OnNewIndex(property.Name, value);
			}
			else if (key is int index)
			{
				var xamlMember = type.ContentProperty;
				if (xamlMember == null)
					throw new ArgumentNullException(nameof(XamlType.ContentProperty), $"Type '{type.Name}' has no {nameof(XamlType.ContentProperty)}.");

				if (xamlMember.Type.IsCollection)
				{
					var collectionValue = xamlMember.Invoker.GetValue(Instance);

					if (collectionValue == null && !xamlMember.IsReadOnly && index == 1) // init value?
						SetXamlMemberValue(xamlMember, value);
					else
					{
						var collectionType = xamlMember.Type;
						collectionType.Invoker.AddToCollection(collectionValue, value);
					}
				}
				else if (index == 1) // first property
				{
					SetXamlMemberValue(xamlMember, value);
				}
				else
					throw new ArgumentOutOfRangeException(xamlMember.Name, index, $"The Content property '{xamlMember.Name}' does not support multiple values.");
			}
			else
				throw new NotSupportedException($"Key of type '{key.GetType().Name}' is not supported.");

			// leave the table empty
			return true;
		} // func OnNewIndex

		#endregion
		
		//private object Resources { get => null; set { } }

		/// <summary>Return scope servie</summary>
		/// <param name="serviceType"></param>
		/// <returns></returns>
		public virtual object GetService(Type serviceType)
		{
			if (serviceType == typeof(IUriContext))
				return ui;
			else
				return parentScope.GetService(serviceType);
		} // func GetService

		/// <summary>Return the current instance, does not finish any scope.</summary>
		public T Instance
		{
			get
			{
				if (instance == null)
					CreateDefaultInstance();
				return instance;
			}
			protected set
			{
				if (instance != null)
					throw new InvalidOperationException();
				instance = value;
			}
		} // prop Instance

		/// <summary>Get the ui class.</summary>
		public LuaUI UI => ui;
		/// <summary>Get the parent scope.</summary>
		public ILuaWpfScope ParentScope => parentScope;

		private static readonly MethodInfo luaRtInvokeMethodInfo;

		static LuaWpfCreator()
		{
			luaRtInvokeMethodInfo = typeof(Lua).GetMethod(nameof(Lua.RtInvoke), BindingFlags.Static | BindingFlags.InvokeMethod | BindingFlags.Public, null, CallingConventions.Standard, new Type[] { typeof(object), typeof(object[]) }, null)
				?? throw new ArgumentException("RtInvoke not resolved.");
		} // sctor

		/// <summary></summary>
		/// <typeparam name="TINSTANCE"></typeparam>
		/// <param name="ui"></param>
		/// <param name="table"></param>
		/// <returns></returns>
		public static TINSTANCE CreateInstance<TINSTANCE>(LuaUI ui, LuaTable table)
			where TINSTANCE : class
		{
			var t = new LuaWpfCreator<TINSTANCE>(ui, null);
			t.SetTableMembers(table);
			return t.Instance;
		} // func CreateInstance

		internal static void SetInstanceFromArgs(LuaWpfCreator<object> creator, object[] args, int offset, int count)
		{
			if (count == 1)
				creator.Instance = args[offset];
			else if (offset == 0 && args.Length == count)
				creator.Instance = args;
			else
			{
				var r = new object[count];
				Array.Copy(args, offset, r, 0, count);
				creator.Instance = r;
			}
		} // func SetInstanceFromArgs
	} // class LuaWpfCreator

	#endregion

	#region -- class LuaWpfGridCreator ------------------------------------------------

	internal class LuaWpfGridCreator : LuaWpfCreator<Grid>
	{
		/// <summary></summary>
		/// <param name="ui"></param>
		/// <param name="type"></param>
		/// <param name="instance"></param>
		public LuaWpfGridCreator(LuaUI ui, XamlType type, Grid instance = null)
			: base(ui, type, instance)
		{
		}

		/// <summary></summary>
		[LuaMember]
		public object RowDefinitions
		{
			get => Instance.RowDefinitions;
			set
			{
				if (value is LuaTable t)
				{
					foreach (var v in t.ArrayList)
					{
						if (v is LuaTable tr)
							Instance.RowDefinitions.Add(CreateInstance<RowDefinition>(UI, tr));
						else
						{
							var creator = new LuaWpfCreator<RowDefinition>(UI, new RowDefinition());
							creator.SetMemberValue(nameof(RowDefinition.Height), v);
							Instance.RowDefinitions.Add(creator.Finish());
						}
					}
				}
				else
					throw new ArgumentException();
			}
		}

		/// <summary></summary>
		[LuaMember]
		public object ColumnDefinitions
		{
			get => Instance.ColumnDefinitions;
			set
			{
				if (value is LuaTable t)
				{
					foreach (var v in t.ArrayList)
					{
						if (v is LuaTable tr)
							Instance.ColumnDefinitions.Add(CreateInstance<ColumnDefinition>(UI, tr));
						else
						{
							var creator = new LuaWpfCreator<ColumnDefinition>(UI, new ColumnDefinition());
							creator.SetMemberValue(nameof(ColumnDefinition.Width), v);
							Instance.ColumnDefinitions.Add(creator.Finish());
						}
					}
				}
				else
					throw new ArgumentException();
			}
		}
	} // class LuaWpfGridCreator

	#endregion

	#region -- class PpsDataFieldScope ------------------------------------------------

	internal class PpsDataFieldScope<T> : LuaWpfCreator<T>, IPpsDataFieldResolver
		where T : class
	{
		public PpsDataFieldScope(LuaUI ui, XamlType type, T instance)
			: base(ui, type, instance)
		{
		} // ctor

		PpsDataFieldInfo IPpsDataFieldResolver.ResolveColumn(string fieldExpression)
		{
			if (String.IsNullOrEmpty(fieldExpression))
				return null;

			//var p = fieldExpression.IndexOf(':');
			//if(p == -1)
			// starts with name, check name
			return FieldResolver?.ResolveColumn(fieldExpression)
				?? ParentScope?.GetService<IPpsDataFieldResolver>(false)?.ResolveColumn(fieldExpression);
		} // func IPpsDataFieldResolver.ResolveColumn

		public override object GetService(Type serviceType)
		{
			if (serviceType == typeof(IPpsDataFieldResolver))
				return this;
			else
				return base.GetService(serviceType);
		} // func GetService

		public IPpsDataFieldResolver FieldResolver { get; set; }
	} // class PpsDataFieldScope

	internal sealed class PpsDataFieldScopeImplementation : PpsDataFieldScope<object>
	{
		public PpsDataFieldScopeImplementation(LuaUI ui)
			: base(ui, null, null)
		{
		} // ctor

		protected override void OnCall(object[] args, int offset, int count)
			=> SetInstanceFromArgs(this, args, offset, count);
	} // class PpsDataFieldScopeImplementation

	#endregion

	#region -- class LuaUI ------------------------------------------------------------

	/// <summary>Library to create a wpf-controls directly in lua.</summary>
	public class LuaUI : LuaTable, IUriContext, IXamlSchemaContextProvider
	{
		private static XamlSchemaContext schemaContext;
		private readonly string currentNamespaceName;

		private readonly Stack<ILuaWpfScope> currentScopes = new Stack<ILuaWpfScope>(); // currenttly added control

		/// <summary>Create the creator for the default name space.</summary>
		public LuaUI()
			: this("http://schemas.microsoft.com/winfx/2006/xaml/presentation")
		{
		} // ctor

		/// <summary>Create the creator for a different namespace.</summary>
		/// <param name="namespaceName">Namespace</param>
		public LuaUI(string namespaceName)
		{
			this.currentNamespaceName = namespaceName;
		} // ctor

		internal void PushScope(ILuaWpfScope scope)
			=> currentScopes.Push(scope);

		internal void PopScope(ILuaWpfScope scope)
		{
			if (CurrentScope == scope)
				currentScopes.Pop();
			else
				throw new InvalidOperationException("Scope error (unfinised scope)."); // todo:
		} // proc PopScope

		internal ILuaWpfScope CurrentScope => currentScopes.Count == 0 ? null : currentScopes.Peek();

		/// <summary>Switch default namespace.</summary>
		/// <param name="namespaceName"></param>
		/// <returns></returns>
		[LuaMember("Namespace")]
		public LuaUI GetNamespace(string namespaceName)
		{
			return new LuaUI(namespaceName);
		}

		/// <summary></summary>
		/// <param name="type"></param>
		/// <returns></returns>
		public XamlType GetXamlType(Type type)
			=> schemaContext.GetXamlType(type);

		private LuaWpfCreator<T> InitDataFieldScope<T>(PpsDataFieldScope<T> scope, IPpsDataFieldResolver resolver)
			where T : class
		{
			if (resolver != null)
				scope.FieldResolver = resolver;
			return scope;
		} // func InitDataFieldScope

		private IPpsDataFieldResolver CreateFieldResolver(object def, object source)
		{
			switch (def)
			{
				case PpsDataSetDefinition dsd:
					return new PpsDataSetResolver(dsd, null);
				case PpsDataSet ds:
					return new PpsDataSetResolver(ds.DataSetDefinition, ds);
				case PpsDataTableDefinition dtd:
					return new PpsDataTableResolver(dtd, null);
				case PpsDataTable dt:
					return new PpsDataTableResolver(dt.TableDefinition, dt.First);
				default:
					throw new ArgumentException(nameof(def));
			}
		} // func CreateFieldResolver

		/// <summary></summary>
		/// <param name="def"></param>
		/// <param name="source"></param>
		/// <returns></returns>
		[LuaMember]
		public object Scope(object def, object source)
			=> InitDataFieldScope(new PpsDataFieldScopeImplementation(this), CreateFieldResolver(def, source));

		/// <summary></summary>
		/// <param name="scope"></param>
		/// <param name="source"></param>
		/// <returns></returns>
		[LuaMember]
		public LuaWpfCreator<PpsDataFieldPanel> DataFields(object scope, object source)
			=> InitDataFieldScope(new PpsDataFieldScope<PpsDataFieldPanel>(this, GetXamlType(typeof(PpsDataFieldPanel)), null), CreateFieldResolver(scope, source));

		/// <summary></summary>
		/// <param name="fieldName"></param>
		/// <returns></returns>
		[LuaMember]
		public object DataField(string fieldName)
		{
			// search field
			var fieldInfo = CurrentScope?.GetService<IPpsDataFieldResolver>(false)?.ResolveColumn(fieldName)
				?? throw new ArgumentOutOfRangeException(nameof(fieldName));

			var ctrl = new LuaWpfCreator<TextBox>(this, GetXamlType(typeof(TextBox)), new TextBox());
			ctrl[TextBox.TextProperty.Name] = DataBinding(fieldInfo.FieldInfo, fieldInfo.Source).Finish();

			if (fieldInfo.FieldInfo.Attributes.TryGetProperty("displayName", out var displayName))
				ctrl.Instance.SetValue(PpsDataFieldPanel.LabelProperty, displayName + ":"); // todo: design

			return ctrl;
		} // func DataField

		/// <summary></summary>
		/// <param name="fieldName"></param>
		/// <returns></returns>
		[LuaMember]
		public LuaWpfCreator<Binding> DataBinding(string fieldName)
		{
			// search field
			var fieldInfo = CurrentScope?.GetService<IPpsDataFieldResolver>(false)?.ResolveColumn(fieldName)
				?? throw new ArgumentOutOfRangeException(nameof(fieldName));

			return DataBinding(fieldInfo.FieldInfo, fieldInfo.Source);
		} // func DataBinding

		/// <summary></summary>
		/// <param name="fieldInfo"></param>
		/// <param name="source"></param>
		/// <returns></returns>
		[LuaMember]
		public LuaWpfCreator<Binding> DataBinding(IDataColumn fieldInfo, object source)
		{
			var b = new Binding
			{
				Path = new PropertyPath(fieldInfo.Name),
				Source = source
			};
			return new LuaWpfCreator<Binding>(this, GetXamlType(typeof(Binding)), b);

		} // func DataBinding

		/// <summary></summary>
		[LuaMember]
		public LuaWpfCreator<Grid> Grid => new LuaWpfGridCreator(this, GetXamlType(typeof(Grid)), null);

		/// <summary>Create the class creator from the context or a preregistered.</summary>
		/// <param name="key"></param>
		/// <returns></returns>
		protected override object OnIndex(object key)
		{
			// get value a registered value
			var value = GetValue(key, true);

			// create from schema context
			if (value == null
				&& key is string typeName)
			{
				var xamlType = schemaContext.GetXamlType(new XamlTypeName(currentNamespaceName, typeName));
				if (xamlType != null)
					value = new LuaWpfCreator<object>(this, xamlType, null);
			}
			return value;
		} // func OnIndex

		/// <summary></summary>
		public XamlSchemaContext SchemaContext => schemaContext;
		/// <summary>Uri, to load external resources.</summary>
		public Uri BaseUri { get; set; }

		static LuaUI()
		{
			schemaContext = System.Windows.Markup.XamlReader.GetWpfSchemaContext();
		} // ctor

		/// <summary>First argument, to mark multiple calls.</summary>
		public static object ReturnSelf { get; } = new object();
	} // class LuaUI

	#endregion
}
