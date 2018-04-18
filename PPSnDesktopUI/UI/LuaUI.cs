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
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Xaml;
using System.Xaml.Schema;
using Neo.IronLua;
using TecWare.DE.Data;
using TecWare.PPSn.Controls;
using TecWare.PPSn.Data;
using LExpression = System.Linq.Expressions.Expression;

namespace TecWare.PPSn.UI
{
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
		/// <summary>Bindpath to use this field.</summary>
		public string Path { get; private set; }
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
	public class LuaWpfCreator : IDynamicMetaObjectProvider
	{
		#region -- class LuaWpfCreaterMetaObject --------------------------------------

		private class LuaWpfCreaterMetaObject : DynamicMetaObject
		{
			public LuaWpfCreaterMetaObject(LExpression expression, object value)
				: base(expression, BindingRestrictions.GetTypeRestriction(expression, value.GetType()), value)
			{
			}

			private bool IsPublicMember(string propertyName, bool ignoreCase)
			{
				var bindingFlags = BindingFlags.Public | BindingFlags.Instance;
				if (ignoreCase)
					bindingFlags |= BindingFlags.IgnoreCase;

				var memberInfo = LimitType.GetMember(propertyName, bindingFlags).FirstOrDefault();
				if (memberInfo == null)
					return false;

				var browsable = memberInfo.GetCustomAttribute<BrowsableAttribute>();
				return browsable != null && browsable.Browsable;
			} // func GetPublicProperty

			#region -- Get/Set/Delete -------------------------------------------------

			private PropertyInfo GetItemPropertyInfo(Type keyType)
			{
				if (keyType == typeof(string))
					return indexStringPropertyInfo;
				else if (typeof(DependencyProperty).IsAssignableFrom(keyType))
					return indexDependencyPropertyPropertyInfo;
				else if (keyType == typeof(sbyte)
					|| keyType == typeof(byte)
					|| keyType == typeof(short)
					|| keyType == typeof(ushort)
					|| keyType == typeof(int)
					|| keyType == typeof(uint)
					|| keyType == typeof(long)
					|| keyType == typeof(ulong))
				{
					return indexIntPropertyInfo;
				}
				else
					return null;
			} // func GetItemPropertyInfo

			private DynamicMetaObject BindGetProperty(DynamicMetaObject index, Type returnType)
			{
				var indexPropertyInfo = GetItemPropertyInfo(index.LimitType);
				if (indexPropertyInfo == null)
					return null;

				return new DynamicMetaObject(
					LExpression.Convert(
						LExpression.Property(Expression, indexPropertyInfo, LExpression.Convert(index.Expression, indexPropertyInfo.PropertyType)),
						returnType
					),
					Restrictions.Merge(index.Restrictions)
				);
			} // func BindGetProperty

			private DynamicMetaObject BindSetProperty(DynamicMetaObject index, DynamicMetaObject value, Type returnType)
			{
				var indexPropertyInfo = GetItemPropertyInfo(index.LimitType);
				if (indexPropertyInfo == null)
					return null;

				return new DynamicMetaObject(
					LExpression.Convert(
						LExpression.Assign(
							LExpression.Property(Expression, indexPropertyInfo, LExpression.Convert(index.Expression, indexPropertyInfo.PropertyType)),
							value == null ? (LExpression)LExpression.Constant(null) : LExpression.Convert(value.Expression, typeof(object))
						),
						returnType
					),
					Restrictions
				);
			} // func BindSetProperty

			private DynamicMetaObject GetKeyExceptionExpression(DynamicMetaObject keyInfo, Type returnType)
			{
				return new DynamicMetaObject(
					LExpression.Throw(
						LExpression.New(notSupportedExceptionConstructorInfo,
							LExpression.Call(stringFormatMethodInfo2,
								LExpression.Constant("Key '{0}' of type '{1}' is not supported."),
								LExpression.Convert(keyInfo.Expression, typeof(object)),
								LExpression.Constant(keyInfo.LimitType, typeof(object))
							)
						),
						returnType
					),
					Restrictions.Merge(keyInfo.Restrictions)
				);
			} // func GetKeyExceptionExpression 

			private bool TryGetMemberIndex(string binderName, bool binderIgnoreCase, out DynamicMetaObject index)
			{
				if (IsPublicMember(binderName, binderIgnoreCase))
				{
					index = null;
					return false;
				}
				else
				{
					index = new DynamicMetaObject(LExpression.Constant(binderName), BindingRestrictions.Empty, binderName);
					return true;
				}
			} // func TryGetMemberIndex

			public override DynamicMetaObject BindGetMember(GetMemberBinder binder)
				=> TryGetMemberIndex(binder.Name, binder.IgnoreCase, out var index)
					? BindGetProperty(index, binder.ReturnType)
					: binder.FallbackGetMember(this);

			public override DynamicMetaObject BindSetMember(SetMemberBinder binder, DynamicMetaObject value)
				=> TryGetMemberIndex(binder.Name, binder.IgnoreCase, out var index)
					? BindSetProperty(index, value, binder.ReturnType)
					: binder.FallbackSetMember(this, value);

			public override DynamicMetaObject BindDeleteMember(DeleteMemberBinder binder)
				=> TryGetMemberIndex(binder.Name, binder.IgnoreCase, out var index)
					? BindSetProperty(index, null, binder.ReturnType)
					: binder.FallbackDeleteMember(this);

			public override DynamicMetaObject BindGetIndex(GetIndexBinder binder, DynamicMetaObject[] indexes)
				=> indexes.Length == 1
					? BindGetProperty(indexes[0], binder.ReturnType) ?? GetKeyExceptionExpression(indexes[0], binder.ReturnType)
					: binder.FallbackGetIndex(this, indexes);

			public override DynamicMetaObject BindSetIndex(SetIndexBinder binder, DynamicMetaObject[] indexes, DynamicMetaObject value)
				=> indexes.Length == 1
					? BindSetProperty(indexes[0], value, binder.ReturnType) ?? GetKeyExceptionExpression(indexes[0], binder.ReturnType)
					: binder.FallbackSetIndex(this, indexes, value);


			public override DynamicMetaObject BindDeleteIndex(DeleteIndexBinder binder, DynamicMetaObject[] indexes)
				=> indexes.Length == 1
					? BindSetProperty(indexes[0], null, binder.ReturnType) ?? GetKeyExceptionExpression(indexes[0], binder.ReturnType)
					: binder.FallbackDeleteIndex(this, indexes);

			#endregion

			private static BindingRestrictions GetSimpleRestriction(DynamicMetaObject mo)
				=> mo.HasValue && mo.Value == null
					? BindingRestrictions.GetInstanceRestriction(mo.Expression, null)
					: BindingRestrictions.GetTypeRestriction(mo.Expression, mo.LimitType);

			public override DynamicMetaObject BindInvoke(InvokeBinder binder, DynamicMetaObject[] args)
			{
				var restrictions = Restrictions.Merge(BindingRestrictions.Combine(args)).Merge(GetSimpleRestriction(args[0]));

				if (args.Length == 1 && typeof(LuaTable).IsAssignableFrom(args[0].LimitType)) // SetTableMembers
				{
					return new DynamicMetaObject(
						LExpression.Convert(
							LExpression.Call(LExpression.Convert(Expression, typeof(LuaWpfCreator)), setTableMembersMethodInfo,
								LExpression.Convert(args[0].Expression, typeof(LuaTable))
							),
							binder.ReturnType
						),
						restrictions
					);
				}
				else
				{
					var argumentExpressions = new LExpression[args.Length];
					for (var i = 0; i < argumentExpressions.Length; i++)
					{
						argumentExpressions[i] = LExpression.Convert(args[i].Expression, typeof(object));
						restrictions = restrictions.Merge(args[i].Restrictions);
					}

					return new DynamicMetaObject(
						LExpression.Convert(
							LExpression.Call(LExpression.Convert(Expression, typeof(LuaWpfCreator)), setPositionalParametersMethodInfo,
								LExpression.NewArrayInit(typeof(object),
									argumentExpressions
								)
							),
							binder.ReturnType
						),
						restrictions
					);
				}
			} // func BindInvoke

			public override DynamicMetaObject BindInvokeMember(InvokeMemberBinder binder, DynamicMetaObject[] args)
				=> base.BindInvokeMember(binder, args);

			public override DynamicMetaObject BindConvert(ConvertBinder binder)
				=> base.BindConvert(binder);

			public override IEnumerable<string> GetDynamicMemberNames()
				=> base.GetDynamicMemberNames();
		} // class LuaWpfCreaterMetaObject

		#endregion

		#region -- class LuaXamlReaderScope -------------------------------------------

		private sealed class LuaXamlReaderState : IDisposable
		{
			private readonly XamlMember parentMember;
			private readonly LuaWpfCreator creator;
			private readonly IEnumerator positionalParameterEnumerator;
			private readonly IEnumerator<KeyValuePair<XamlMember, object>> memberEnumerator;
			private readonly IEnumerator collectionItemsEnumerator;
			public int state;
			
			public LuaXamlReaderState(LuaWpfCreator creator, XamlMember parentMember, int initState)
			{
				this.creator = creator;
				this.parentMember = parentMember;
				this.state = initState;

				positionalParameterEnumerator = creator.positionalParameter?.GetEnumerator();
				memberEnumerator = creator.members.GetEnumerator();
				collectionItemsEnumerator = creator.values.GetEnumerator();
			} // ctor

			public void Dispose()
			{
				if(positionalParameterEnumerator is IDisposable d1)
					d1.Dispose();
				if (collectionItemsEnumerator is IDisposable d2)
					d2.Dispose();

				memberEnumerator?.Dispose();
			} // proc Dispose

			public bool NextPositionalParameter()
				=> positionalParameterEnumerator?.MoveNext() ?? false;

			public object CurrentPositionalParameter => positionalParameterEnumerator.Current;

			public bool NextMember()
				=> memberEnumerator.MoveNext();

			public XamlMember CurrentMember
				=> memberEnumerator.Current.Key;

			public object GetMemberValue()
			{
				var xamlMember = memberEnumerator.Current.Key;
				var xamlValue = memberEnumerator.Current.Value;

				return xamlValue;
				//return xamlValue == null || xamlValue is LuaWpfCreator
				//	? xamlValue
				//	: (
				//	xamlValue is string s
				//		? s
				//		: xamlMember.Type.TypeConverter.ConverterInstance.ConvertToInvariantString(xamlValue)
				//	);
			} // func GetMemberValue

			public bool NextCollectionItem()
				=> collectionItemsEnumerator.MoveNext();

			public object CurrentCollectionItem => collectionItemsEnumerator.Current;

			public LuaWpfCreator Creator => creator;
			public XamlMember ParentMember => parentMember;
		} // class LuaXamlReaderScope

		#endregion

		#region -- class LuaXamlUsedNamespaceState ------------------------------------

		private sealed class LuaXamlUsedNamespaceState : IDisposable
		{
			private readonly LuaWpfCreator creator;
			private readonly IEnumerator<KeyValuePair<XamlMember, object>> memberEnumerator;
			private readonly IEnumerator<object> valuesEnumerator;
			private IEnumerator<XamlType> allowedContentTypes = null;
			private int state = 0;

			public LuaXamlUsedNamespaceState(LuaWpfCreator creator)
			{
				this.creator = creator;
				this.memberEnumerator = creator.members.GetEnumerator();
				this.valuesEnumerator = creator.values.GetEnumerator();
			} // ctor

			public void Dispose()
			{
				memberEnumerator.Dispose();
				valuesEnumerator.Dispose();
				allowedContentTypes?.Dispose();
			} // proc Dispose

			public bool NextType(out XamlType type, out LuaWpfCreator value)
			{
				switch (state)
				{
					case 0:
						state = 1;
						type = creator.type;
						value = null;
						return true;
					case 1:
						if (creator.type.ContentProperty != null)
						{
							type = creator.type.ContentProperty.Type;
							value = null;
							if (creator.type.AllowedContentTypes != null)
							{
								state = 2;
								allowedContentTypes = creator.type.AllowedContentTypes.GetEnumerator();
							}
							else
								state = 3;
							return true;
						}
						else
						{
							state = 3;
							goto case 3;
						}
					case 2:
						if (allowedContentTypes.MoveNext())
						{
							type = allowedContentTypes.Current;
							value = null;
							return true;
						}
						else
						{
							allowedContentTypes.Dispose();
							allowedContentTypes = null;
							state = 3;
							goto case 3;
						}

					case 3:
						if (memberEnumerator.MoveNext())
						{
							type = memberEnumerator.Current.Key.Type;
							value = memberEnumerator.Current.Value as LuaWpfCreator;
							return true;
						}
						else
						{
							state = 4;
							goto case 4;
						}
					case 4:
						if (valuesEnumerator.MoveNext())
						{
							type = null;
							value = valuesEnumerator.Current as LuaWpfCreator;
							if (value == null)
								goto case 4;
							return true;
						}
						else
						{
							state = 5;
							goto case 5;
						}
					case 5:
						type = null;
						value = null;
						return false;
					default:
						throw new InvalidOperationException();
				}
			} // func NextType
		} // class LuaXamlUsedNamespaceState

		#endregion

		#region -- class LuaXamlReader ------------------------------------------------

		private class LuaXamlReader : System.Xaml.XamlReader
		{
			private readonly Stack<LuaXamlReaderState> states = new Stack<LuaXamlReaderState>();
			private readonly IEnumerator<NamespaceDeclaration> collectNamespaces;

			private XamlNodeType nodeType = XamlNodeType.None;
			private NamespaceDeclaration namespaceDeclaration = null;
			private XamlType xamlType = null;
			private XamlMember xamlMember = null;
			private object xamlValue = null;

			public LuaXamlReader(LuaWpfCreator creator)
			{
				this.collectNamespaces = CollectNamespaces().GetEnumerator();

				states.Push(new LuaXamlReaderState(creator ?? throw new ArgumentNullException(nameof(creator)), null, -1));
			} // ctor

			protected override void Dispose(bool disposing)
			{
				base.Dispose(disposing);

				while (states.Count > 0)
					states.Pop().Dispose();
			} // proc Dispose

			private IEnumerable<string> GetUsedNamespaces()
			{
				var currentNameSpaceStates = new Stack<LuaXamlUsedNamespaceState>();
				var currentNameSpaceState = new LuaXamlUsedNamespaceState(CurrentState.Creator);
				while (true)
				{
					if (currentNameSpaceState.NextType(out var type, out var childCreator))
					{
						if (type != null)
						{
							var cur = type.GetXamlNamespaces().FirstOrDefault();
							if (!String.IsNullOrEmpty(cur))
								yield return cur;
						}

						if (childCreator != null)
						{
							currentNameSpaceStates.Push(currentNameSpaceState);
							currentNameSpaceState = new LuaXamlUsedNamespaceState(childCreator);
						}
					}
					else
					{
						currentNameSpaceState.Dispose();
						if (currentNameSpaceStates.Count > 0)
							currentNameSpaceState = currentNameSpaceStates.Pop();
						else
							break;
					}
				}
			} // func GetUsedNamespaces

			private IEnumerable<NamespaceDeclaration> CollectNamespaces()
			{
				var emittedNamespaces = new List<string>();
				var emittedPrefixes = new List<string>();

				foreach (var _ns in GetUsedNamespaces())
				{
					var ns = PpsXamlSchemaContext.Default.TryGetCompatibleXamlNamespace(_ns, out var comp)
						? comp
						: _ns;

					var nsIdx = emittedNamespaces.BinarySearch(ns, StringComparer.OrdinalIgnoreCase);
					if (nsIdx < 0)
					{
						string preferedPrefix;
						if (ns == StuffUI.PresentationNamespace.NamespaceName)
							preferedPrefix = "";
						else
						{
							preferedPrefix = PpsXamlSchemaContext.Default.GetPreferredPrefix(ns);
							if (String.IsNullOrEmpty(preferedPrefix))
								preferedPrefix = "local";
						}

						var prefix = preferedPrefix;
						var prefixIdx = emittedPrefixes.BinarySearch(prefix, StringComparer.Ordinal);
						var prefixCounter = 1;
						while (prefixIdx >= 0)
						{
							prefix = preferedPrefix + (prefixCounter++).ToString();
							prefixIdx = emittedPrefixes.BinarySearch(prefix, StringComparer.Ordinal);
						}

						emittedPrefixes.Insert(~prefixIdx, prefix);
						emittedNamespaces.Insert(~nsIdx, ns);
						yield return new NamespaceDeclaration(ns, prefix);
					}
				}
			} // func CollectNamespaces

			private bool PushState(int newState, LuaXamlReaderState currentState, LuaWpfCreator creator, XamlMember parentMember)
			{
				if (creator != null)
					states.Push(new LuaXamlReaderState(creator, parentMember, 0));

				currentState.state = newState;
				return Read();
			} // proc PushState

			private bool PopState()
			{
				states.Pop().Dispose();
				return Read();
			} // proc PopState

			private bool SetValueState(int loopState, int finishState, LuaXamlReaderState currentState, XamlMember parentMember, Func<bool> moveNext, object value)
			{
				var n = moveNext() ? loopState : finishState;
				return value is LuaWpfCreator subCreator
					? PushState(n, currentState, subCreator, parentMember)
					: SetState(n, XamlNodeType.Value, xamlValue: value);
			} // func SetState

			private bool SetState(int newState, XamlNodeType nodeType, NamespaceDeclaration namespaceDeclaration = null, XamlType xamlType = null, XamlMember xamlMember = null, object xamlValue = null)
			{
				CurrentState.state = newState;

				switch (nodeType)
				{
					case XamlNodeType.StartMember:
						if (xamlMember == null)
							throw new ArgumentNullException(nameof(xamlMember));
						break;
					case XamlNodeType.StartObject:
						if (xamlType == null)
							throw new ArgumentNullException(nameof(xamlType));
						break;
					case XamlNodeType.NamespaceDeclaration:
						if (namespaceDeclaration == null)
							throw new ArgumentNullException(nameof(namespaceDeclaration));
						break;
				}

				//Debug.Print($"SETSTATE: {newState} -> {nodeType}");

				this.nodeType = nodeType;
				this.namespaceDeclaration = namespaceDeclaration;
				this.xamlType = xamlType;
				this.xamlMember = xamlMember;
				this.xamlValue = xamlValue;

				return nodeType != XamlNodeType.None;
			} // func SetState

			public override bool Read()
			{
				var currentState = CurrentState;
				if (currentState == null)
					return false;

				switch (currentState.state)
				{
					case -1: // init namespaces
						if (collectNamespaces.MoveNext())
							return SetState(-1, XamlNodeType.NamespaceDeclaration, namespaceDeclaration: collectNamespaces.Current);
						else
							goto case 0;
					case 0: // init object state
						return currentState.ParentMember != null && currentState.ParentMember.IsReadOnly
							? SetState(2, XamlNodeType.GetObject)
							: SetState(2, XamlNodeType.StartObject, xamlType: currentState.Creator.type);
					case 1: // finish state
						return states.Count == 0
							? SetState(1, XamlNodeType.None)
							: PopState();
					
					case 2: // emit member objects
						if (currentState.NextPositionalParameter())
							return SetState(10, XamlNodeType.StartMember, xamlMember: XamlLanguage.PositionalParameters);
						else if(currentState.NextMember())
							return SetState(20, XamlNodeType.StartMember, xamlMember: currentState.CurrentMember);
						else if (currentState.NextCollectionItem())
							return SetState(30, XamlNodeType.StartMember, xamlMember: XamlLanguage.Items);
						else
							return SetState(1, XamlNodeType.EndObject); // close this object

					case 10:
						return SetValueState(10, 19, currentState, XamlLanguage.PositionalParameters, currentState.NextPositionalParameter, currentState.CurrentPositionalParameter);
					case 19:
						return SetState(2, XamlNodeType.EndMember);
						
					case 20:
						return SetValueState(21, 29, currentState, currentState.CurrentMember, currentState.NextMember, currentState.GetMemberValue());
					case 21:
						return SetState(22, XamlNodeType.EndMember);
					case 22:
						return SetState(20, XamlNodeType.StartMember, xamlMember: currentState.CurrentMember);
					case 29:
						return SetState(2, XamlNodeType.EndMember);

					case 30:
						return SetValueState(30, 39, currentState, XamlLanguage.Items, currentState.NextCollectionItem, currentState.CurrentCollectionItem);
					case 39:
						return SetState(2, XamlNodeType.EndMember);

					default:
						throw new ArgumentOutOfRangeException("state");
				}
			} // func Read

			private LuaXamlReaderState CurrentState => states.Count > 0 ? states.Peek() : null;

			public override bool IsEof => CurrentState == null;

			public override XamlNodeType NodeType => nodeType;
			public override NamespaceDeclaration Namespace => namespaceDeclaration;
			public override XamlType Type => xamlType;
			public override XamlMember Member => xamlMember;
			public override object Value => xamlValue;

			public override XamlSchemaContext SchemaContext => PpsXamlSchemaContext.Default;
		} // class LuaXamlReader

		#endregion

		private readonly LuaUI ui;
		private readonly XamlType type;

		private readonly List<object> values = new List<object>();
		private readonly Dictionary<XamlMember, object> members = new Dictionary<XamlMember, object>();
		private object[] positionalParameter = null;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary></summary>
		/// <param name="ui"></param>
		/// <param name="type"></param>
		public LuaWpfCreator(LuaUI ui, XamlType type)
		{
			this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
			this.type = type ?? throw new ArgumentNullException(nameof(type));
		} // ctor

		DynamicMetaObject IDynamicMetaObjectProvider.GetMetaObject(LExpression parameter)
			=> new LuaWpfCreaterMetaObject(parameter, this);

		/// <summary>Create a reader</summary>
		/// <param name="context"></param>
		/// <returns></returns>
		public System.Xaml.XamlReader CreateReader(IServiceProvider context)
			=> new LuaXamlReader(this);

		/// <summary></summary>
		/// <param name="context"></param>
		/// <returns></returns>
		public Task<T> GetInstanceAsync<T>(IServiceProvider context)
			=> PpsXamlParser.LoadAsync<T>(CreateReader(context));

		/// <summary></summary>
		/// <returns></returns>
		public override string ToString()
			=> "WpfCreator: " + type.Name;

		#endregion

		#region -- Setter/Getter ------------------------------------------------------

		private XamlMember GetXamlAttachedMember(string typeName, string memberName)
		{
			var attachedType = type.SchemaContext.GetXamlType(new XamlTypeName(type.PreferredXamlNamespace, typeName));
			if (attachedType == null)
				throw new ArgumentNullException(nameof(typeName), $"Could not resolve '{typeName}'.");
			return attachedType.GetAttachableMember(memberName);
		} // func GetXamlAttachedMember

		/// <summary></summary>
		/// <param name="propertyName"></param>
		/// <returns></returns>
		protected XamlMember GetXamlMember(string propertyName)
		{
			// check for attached property
			var attachedPos = propertyName.IndexOf('.');
			return (
				attachedPos == -1
					? type.GetMember(propertyName)
					: GetXamlAttachedMember(propertyName.Substring(0, attachedPos), propertyName.Substring(attachedPos + 1))
			) ?? throw new ArgumentException($"Could not resolve member '{propertyName}'.", nameof(propertyName));
		} // func GetXamlMember

		private XamlMember GetXamlMember(DependencyProperty property)
		{
			// todo: AttachedProperties?
			return GetXamlMember(property.Name);
		} // func GetXamlMember

		private void SetXamlProperty(XamlMember member, object value)
		{
			if (member == null)
				throw new ArgumentNullException(nameof(member));

			if (value == null || value == DependencyProperty.UnsetValue)
				members.Remove(member);
			else
			{
				// checkk access
				if (member.IsReadOnly)
					throw new NotSupportedException($"'{member.Name}' is readonly.");

				if (member.IsEvent)
				{
					if (value is string)
						members[member] = value; // works if the stream is wrapped to an PpsXamlReader
					else if(value is Delegate dlg)
						members[member] = PpsXamlParser.CreateEventFromDelegate(member, dlg);
					else
						throw new ArgumentException($"Can not set event '{member.Name}' to '{value}'. Only string is allowed.");
				}
				else if (value is LuaWpfCreator subCreator)
				{
					// todo: check return type, and test for binding
					members[member] = subCreator;
				}
				else
				{
					// convert value
					var valueType = UI.GetXamlType(value.GetType());
					var valueTypeConverter = valueType.TypeConverter?.ConverterInstance;
					var memberTypeConverter = member.Type.TypeConverter?.ConverterInstance;
					if (member.Type.UnderlyingType.IsAssignableFrom(valueType.UnderlyingType))
						value = Convert.ChangeType(value, member.Type.UnderlyingType);
					else if (valueTypeConverter?.CanConvertTo(member.Type.UnderlyingType) ?? false)
						value = valueTypeConverter.ConvertTo(value, member.Type.UnderlyingType);
					else if (memberTypeConverter?.CanConvertFrom(valueType.UnderlyingType) ?? false)
						value = memberTypeConverter.ConvertFrom(value);
					else
						throw new ArgumentException($"Can not set '{member.Name}' to '{value}'. Type is not assignable.");

					members[member] = value;
				}
			}
		} // proc SetXamlProperty

		/// <summary></summary>
		/// <param name="key"></param>
		/// <param name="value"></param>
		public void SetProperty(object key, object value)
		{
			switch (key)
			{
				case XamlMember member:
					SetXamlProperty(member, value);
					break;
				case string propertyName:
					var pi = GetType().GetProperty(propertyName); // todo: cache to speed up
					if (pi != null)
						pi.SetValue(this, value);
					else
						SetXamlProperty(GetXamlMember(propertyName), value);
					break;
				case DependencyProperty property:
					SetXamlProperty(GetXamlMember(property), value);
					break;

				case sbyte idxI8:
					SetXamlIndex(idxI8, value);
					break;
				case byte idxU8:
					SetXamlIndex(idxU8, value);
					break;
				case short idxI16:
					SetXamlIndex(idxI16, value);
					break;
				case ushort idxU16:
					SetXamlIndex(idxU16, value);
					break;
				case int idxI32:
					SetXamlIndex(idxI32, value);
					break;
				case uint idxU32:
					SetXamlIndex((int)idxU32, value);
					break;
				case long idxI64:
					SetXamlIndex((int)idxI64, value);
					break;
				case ulong idxU64:
					SetXamlIndex((int)idxU64, value);
					break;

				case null:
					throw new ArgumentNullException(nameof(key));
				default:
					throw new NotSupportedException($"Key of type '{key.GetType().Name}' is not supported.");
			}
		} // proc SetProperty

		/// <summary></summary>
		/// <param name="key"></param>
		/// <returns></returns>
		public object GetProperty(object key)
		{
			switch (key)
			{
				case XamlMember member:
					return GetXamlProperty(member);
				case string propertyName:
					// todo: Public Properties!!!
					return GetXamlProperty(GetXamlMember(propertyName));
				case DependencyProperty property:
					return GetXamlProperty(GetXamlMember(property));

				case sbyte idxI8:
					return GetXamlIndex(idxI8);
				case byte idxU8:
					return GetXamlIndex(idxU8);
				case short idxI16:
					return GetXamlIndex(idxI16);
				case ushort idxU16:
					return GetXamlIndex(idxU16);
				case int idxI32:
					return GetXamlIndex(idxI32);
				case uint idxU32:
					return GetXamlIndex((int)idxU32);
				case long idxI64:
					return GetXamlIndex((int)idxI64);
				case ulong idxU64:
					return GetXamlIndex((int)idxU64);

				case null:
					throw new ArgumentNullException(nameof(key));
				default:
					throw new NotSupportedException($"Key of type '{key.GetType().Name}' is not supported.");
			}
		} // proc SetProperty
		private object GetXamlProperty(XamlMember member)
		{
			if (member == null)
				return null;
			if (members.TryGetValue(member, out var value))
				return value;
			else if (member.IsReadOnly)
			{
				value = CreateFactory(UI, member.Type);
				members.Add(member, value);
				return value;
			}
			else
				return null;
		} // func GetXamlProperty

		private static void CheckIndex(int idx)
		{
			if (idx < 1)
				throw new ArgumentOutOfRangeException(nameof(idx), "Index is lower than 1.");
		} // func CheckIndex

		/// <summary></summary>
		/// <param name="value"></param>
		public void AddValue(object value)
		{
			var idx = values.Count - 1;
			while (idx >= 0 && values[idx] == null)
				idx--;

			SetXamlIndex(idx + 2, value);
		} // proc AddValue

		private int EnsureIndex(int idx)
		{
			while (idx >= values.Count)
				values.Add(null);
			return idx;
		} // proc EnsureIndex

		private void SetXamlIndex(int idx, object value)
		{
			CheckIndex(idx);

			//type.AllowedContentTypes

			if (type.IsCollection)
				values[EnsureIndex(idx - 1)] = value;
			else
			{
				var xamlMember = type.ContentProperty;
				if (xamlMember == null)
					throw new ArgumentNullException(nameof(XamlType.ContentProperty), $"Type '{type.Name}' has no {nameof(XamlType.ContentProperty)}.");

				if (xamlMember.IsReadOnly)
				{
					var subProperty = GetXamlProperty(xamlMember);
					if (subProperty is LuaWpfCreator subCreator)
						subCreator.SetXamlIndex(idx, value);
					else
						xamlMember.Type.Invoker.AddToCollection(subProperty, value);
				}
				else if (idx == 1)
				{
					SetXamlProperty(xamlMember, value);
				}
				else
					throw new ArgumentOutOfRangeException(type.Name, idx, $"The type '{type.Name}' does not support multiple values.");
			}
		} // proc SetXamlIndex

		private object GetXamlIndex(int idx)
		{
			CheckIndex(idx);
			idx--;
			return idx >= 0 && idx < values.Count ? values[idx] : null;
		} // proc GetXamlIndex

		/// <summary></summary>
		/// <param name="t"></param>
		/// <returns></returns>
		public LuaWpfCreator SetTableMembers(LuaTable t)
		{
			if (t == null)
				return this;

			// call set member for all value
			foreach (var kv in t.Values)
				SetProperty(kv.Key, kv.Value);

			return this;
		} // proc SetTableMembers

		/// <summary></summary>
		/// <param name="arguments"></param>
		/// <returns></returns>
		public LuaWpfCreator SetPositionalParameters(object[] arguments)
		{
			if (positionalParameter == null)
				positionalParameter = arguments;
			else
				throw new InvalidOperationException("Positional parameters already set.");
			return this;
		} // proc SetPositionalParameters

		#endregion

		/// <summary></summary>
		/// <param name="member"></param>
		/// <returns></returns>
		public object this[XamlMember member] { get => GetXamlProperty(member); set => SetXamlProperty(member, value); }
		/// <summary></summary>
		/// <param name="propertyName"></param>
		/// <returns></returns>
		public object this[string propertyName] { get => GetXamlProperty(GetXamlMember(propertyName)); set => SetXamlProperty(GetXamlMember(propertyName), value); }
		/// <summary></summary>
		/// <param name="property"></param>
		/// <returns></returns>
		public object this[DependencyProperty property] { get => GetXamlProperty(GetXamlMember(property)); set => SetXamlProperty(GetXamlMember(property), value); }

		/// <summary></summary>
		/// <param name="idx"></param>
		/// <returns></returns>
		public object this[int idx] { get => GetXamlIndex(idx); set => SetXamlIndex(idx, value); }

		/// <summary>Name of the object</summary>
		public virtual string Name => type.Name;

		/// <summary>Get the ui class.</summary>
		public LuaUI UI => ui;

		/// <summary>Create a wpf creator for a object-type.</summary>
		/// <param name="ui">UI reference</param>
		/// <param name="type">Type of the object.</param>
		/// <param name="table">Members to initialize</param>
		/// <returns></returns>
		public static LuaWpfCreator CreateFactory(LuaUI ui, Type type, LuaTable table = null)
			=> new LuaWpfCreator(ui, ui.GetXamlType(type)).SetTableMembers(table);

		/// <summary>Create a wpf creator for a object-type.</summary>
		/// <param name="ui">UI reference</param>
		/// <param name="xamlType">Type of the object.</param>
		/// <param name="table">Members to initialize</param>
		/// <returns></returns>
		public static LuaWpfCreator CreateFactory(LuaUI ui, XamlType xamlType, LuaTable table = null)
			=> new LuaWpfCreator(ui, xamlType).SetTableMembers(table);

		private static readonly PropertyInfo indexStringPropertyInfo;
		private static readonly PropertyInfo indexDependencyPropertyPropertyInfo;
		private static readonly PropertyInfo indexIntPropertyInfo;
		private static readonly MethodInfo setTableMembersMethodInfo;
		private static readonly MethodInfo setPositionalParametersMethodInfo;

		private static readonly ConstructorInfo notSupportedExceptionConstructorInfo;
		private static readonly MethodInfo stringFormatMethodInfo2;

		static LuaWpfCreator()
		{
			var type = typeof(LuaWpfCreator);
			indexStringPropertyInfo = type.GetProperty("Item", BindingFlags.Public | BindingFlags.Instance | BindingFlags.GetProperty, null, typeof(object), new Type[] { typeof(string) }, null) ?? throw new ArgumentNullException();
			indexDependencyPropertyPropertyInfo = type.GetProperty("Item", BindingFlags.Public | BindingFlags.Instance | BindingFlags.GetProperty, null, typeof(object), new Type[] { typeof(DependencyProperty) }, null) ?? throw new ArgumentNullException();
			indexIntPropertyInfo = type.GetProperty("Item", BindingFlags.Public | BindingFlags.Instance | BindingFlags.GetProperty, null, typeof(object), new Type[] { typeof(int) }, null) ?? throw new ArgumentNullException();
			setTableMembersMethodInfo = type.GetMethod(nameof(SetTableMembers), BindingFlags.Public | BindingFlags.Instance | BindingFlags.InvokeMethod, null, new Type[] { typeof(LuaTable) }, null) ?? throw new ArgumentNullException();
			setPositionalParametersMethodInfo = type.GetMethod(nameof(SetPositionalParameters), BindingFlags.Public | BindingFlags.Instance | BindingFlags.InvokeMethod, null, new Type[] { typeof(object[]) }, null) ?? throw new ArgumentNullException();

			notSupportedExceptionConstructorInfo = typeof(NotSupportedException).GetConstructor(new Type[] { typeof(string) }) ?? throw new ArgumentNullException();
			stringFormatMethodInfo2 = typeof(string).GetMethod(nameof(String.Format), BindingFlags.Static | BindingFlags.Public | BindingFlags.InvokeMethod, null, new Type[] { typeof(string), typeof(object), typeof(object) }, null) ?? throw new ArgumentNullException();
		} // sctor
	} // class LuaWpfCreator

	#endregion

	#region -- class LuaWpfGridCreator ------------------------------------------------

	internal class LuaWpfGridCreator : LuaWpfCreator
	{
		private readonly Lazy<XamlMember> rowDefinitionsMember;
		private readonly Lazy<XamlMember> columnDefinitionsMember;

		/// <summary></summary>
		/// <param name="ui"></param>
		public LuaWpfGridCreator(LuaUI ui)
			: base(ui, ui.GetXamlType(typeof(Grid)))
		{
			this.rowDefinitionsMember = new Lazy<XamlMember>(() => GetXamlMember(nameof(Grid.RowDefinitions)));
			this.columnDefinitionsMember = new Lazy<XamlMember>(() => GetXamlMember(nameof(Grid.ColumnDefinitions)));
		} // ctor

		public object RowDefinitions
		{
			get => this[rowDefinitionsMember.Value];
			set
			{
				if (value is LuaTable t)
				{
					var rows = (LuaWpfCreator)this.RowDefinitions;
					foreach (var v in t.ArrayList)
					{
						if (v is LuaTable tr)
							rows.AddValue(CreateFactory(UI, typeof(RowDefinition), tr));
						else
						{
							var row = CreateFactory(UI, typeof(RowDefinition));
							row[RowDefinition.HeightProperty] = v;
							rows.AddValue(row);
						}
					}
				}
				else
					throw new ArgumentException();
			}
		} // prop RowDefinitions

		public object ColumnDefinitions
		{
			get => this[columnDefinitionsMember.Value];
			set
			{
				if (value is LuaTable t)
				{
					var rows = (LuaWpfCreator)this.ColumnDefinitions;
					foreach (var v in t.ArrayList)
					{
						if (v is LuaTable tr)
							rows.AddValue(CreateFactory(UI, typeof(ColumnDefinition), tr));
						else
						{
							var row = CreateFactory(UI, typeof(ColumnDefinition));
							row[ColumnDefinition.WidthProperty] = v;
							rows.AddValue(row);
						}
					}
				}
				else
					throw new ArgumentException();
			}
		} // prop RowDefinitions
	} // class LuaWpfGridCreator

	#endregion

	#region -- class LuaUI ------------------------------------------------------------

	/// <summary>Library to create a wpf-controls directly in lua.</summary>
	public class LuaUI : LuaTable, IUriContext
	{
		private readonly string currentNamespaceName;

		/// <summary>Create the creator for the default name space.</summary>
		public LuaUI()
			: this(StuffUI.PresentationNamespace.NamespaceName)
		{
		} // ctor

		/// <summary>Create the creator for a different namespace.</summary>
		/// <param name="namespaceName">Namespace</param>
		public LuaUI(string namespaceName)
		{
			this.currentNamespaceName = namespaceName;
		} // ctor

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
				var xamlType = GetXamlType(new XamlTypeName(currentNamespaceName, typeName));
				if (xamlType != null)
					value = new LuaWpfCreator(this, xamlType);
			}
			return value;
		} // func OnIndex

		/// <summary></summary>
		/// <param name="type"></param>
		/// <returns></returns>
		public XamlType GetXamlType(Type type)
			=> PpsXamlSchemaContext.Default.GetXamlType(type);

		/// <summary></summary>
		/// <param name="typeName"></param>
		/// <returns></returns>
		public XamlType GetXamlType(XamlTypeName typeName)
			=> PpsXamlSchemaContext.Default.GetXamlType(typeName);

		/// <summary>Switch default namespace.</summary>
		/// <param name="namespaceName"></param>
		/// <returns></returns>
		[LuaMember("Namespace")]
		public LuaUI GetNamespace(string namespaceName)
			=> new LuaUI(namespaceName);

		#region -- SideBar ------------------------------------------------------------

		/// <summary></summary>
		[LuaMember]
		public LuaWpfCreator SideBar
			=> LuaWpfCreator.CreateFactory(this, typeof(PpsSideBarControl));

		/// <summary></summary>
		[LuaMember]
		public LuaWpfCreator SideBarGroup
			=> LuaWpfCreator.CreateFactory(this, typeof(PpsSideBarGroup));

		/// <summary></summary>
		[LuaMember]
		public LuaWpfCreator SideBarPanel
			=> LuaWpfCreator.CreateFactory(this, typeof(PpsSideBarPanel));

		/// <summary></summary>
		[LuaMember]
		public LuaWpfCreator SideBarPanelFilter
			=> LuaWpfCreator.CreateFactory(this, typeof(PpsSideBarPanelFilter));

		/// <summary></summary>
		[LuaMember]
		public LuaWpfCreator StackSection
			=> LuaWpfCreator.CreateFactory(this, typeof(PpsStackSectionControl));

		/// <summary></summary>
		[LuaMember]
		public LuaWpfCreator StackSectionItem
			=> LuaWpfCreator.CreateFactory(this, typeof(PpsStackSectionItem));

		#endregion

		/// <summary></summary>
		[LuaMember]
		public LuaWpfCreator Grid => new LuaWpfGridCreator(this);

		/// <summary></summary>
		[LuaMember]
		public LuaUI Pps => GetNamespace("http://tecare-gmbh.de/ppsn/wpf/2015");

		/// <summary>Uri, to load external resources.</summary>
		public Uri BaseUri { get; set; }

		//private IPpsDataFieldResolver CreateFieldResolver(object def, object source)
		//{
		//	if (def == null && source == null)
		//		return null;

		//	switch (def)
		//	{
		//		case PpsDataSetDefinition dsd:
		//			return new PpsDataSetResolver(dsd, null);
		//		case PpsDataSet ds:
		//			return new PpsDataSetResolver(ds.DataSetDefinition, ds);
		//		case PpsDataTableDefinition dtd:
		//			return new PpsDataTableResolver(dtd, null);
		//		case PpsDataTable dt:
		//			return new PpsDataTableResolver(dt.TableDefinition, dt.First);
		//		default:
		//			throw new ArgumentException(nameof(def));
		//	}
		//} // func CreateFieldResolver

		///// <summary></summary>
		///// <param name="def"></param>
		///// <param name="source"></param>
		///// <returns></returns>
		//[LuaMember]
		//public object Scope(object def, object source)
		//	=> InitDataFieldScope(new PpsDataFieldScopeImplementation(this), CreateFieldResolver(def, source));

		///// <summary></summary>
		///// <param name="scope"></param>
		///// <param name="source"></param>
		///// <returns></returns>
		//[LuaMember]
		//public LuaWpfCreator<PpsDataFieldPanel> DataFields(object scope, object source)
		//	=> InitDataFieldScope(new PpsDataFieldScope<PpsDataFieldPanel>(this, GetXamlType(typeof(PpsDataFieldPanel)), null), CreateFieldResolver(scope, source));

		///// <summary></summary>
		///// <param name="fieldName"></param>
		///// <returns></returns>
		//[LuaMember]
		//public object DataField(string fieldName)
		//{
		//	// search field
		//	var fieldInfo = CurrentScope?.GetService<IPpsDataFieldResolver>(false)?.ResolveColumn(fieldName)
		//		?? throw new ArgumentOutOfRangeException(nameof(fieldName), fieldName, $"Could not locate field: {fieldName}.");

		//	var ctrl = new LuaWpfCreator<TextBox>(this, GetXamlType(typeof(TextBox)), new TextBox());
		//	ctrl[TextBox.TextProperty.Name] = DataBinding(fieldInfo.FieldInfo, fieldInfo.Source).Finish();

		//	if (fieldInfo.FieldInfo.Attributes.TryGetProperty("displayName", out var displayName))
		//		ctrl.Instance.SetValue(PpsDataFieldPanel.LabelProperty, displayName + ":"); // todo: design

		//	return ctrl;
		//} // func DataField

		///// <summary></summary>
		///// <param name="fieldName"></param>
		///// <returns></returns>
		//[LuaMember]
		//public LuaWpfCreator<Binding> DataBinding(string fieldName)
		//{
		//	// search field
		//	var fieldInfo = CurrentScope?.GetService<IPpsDataFieldResolver>(false)?.ResolveColumn(fieldName)
		//		?? throw new ArgumentOutOfRangeException(nameof(fieldName));

		//	return DataBinding(fieldInfo.FieldInfo, fieldInfo.Source);
		//} // func DataBinding

		///// <summary></summary>
		///// <param name="fieldInfo"></param>
		///// <param name="source"></param>
		///// <returns></returns>
		//[LuaMember]
		//public LuaWpfCreator<Binding> DataBinding(IDataColumn fieldInfo, object source)
		//{
		//	var b = new Binding
		//	{
		//		Path = new PropertyPath(fieldInfo.Name),
		//		Source = source
		//	};
		//	return new LuaWpfCreator<Binding>(this, GetXamlType(typeof(Binding)), b);

		//} // func DataBinding
	} // class LuaUI

	#endregion
}
