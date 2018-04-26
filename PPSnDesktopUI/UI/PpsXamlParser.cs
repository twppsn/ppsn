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
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Markup;
using System.Xaml;
using System.Xaml.Schema;
using System.Xml;
using Neo.IronLua;
using LExpression = System.Linq.Expressions.Expression;

namespace TecWare.PPSn.UI
{
	#region -- interface IPpsXamlCode -------------------------------------------------

	/// <summary>Interface for the parse, that takes care of the code behind.</summary>
	public interface IPpsXamlCode
	{
		/// <summary>Compile code</summary>
		/// <param name="uri"></param>
		/// <param name="code"></param>
		void CompileCode(Uri uri, string code = null);
	} // interface IPpsXamlCode

	#endregion

	#region -- interface IPpsXamlEmitter ----------------------------------------------

	/// <summary>Implement this interface to an FrameworkElement to replace the content.</summary>
	public interface IPpsXamlEmitter
	{
		/// <summary>Creates an emitter for the replacement content.</summary>
		/// <param name="context"></param>
		/// <returns></returns>
		System.Xaml.XamlReader CreateReader(IServiceProvider context);
	} // interface IPpsXamlEmitter

	#endregion

	#region -- interface IPpsXamlDynamicProperties ------------------------------------

	/// <summary>Mark the emitter for the dynamic property support.</summary>
	public interface IPpsXamlDynamicProperties
	{
		/// <summary></summary>
		/// <param name="name"></param>
		/// <param name="value"></param>
		void SetValue(string name, object value);
		/// <summary></summary>
		/// <param name="name"></param>
		/// <returns></returns>
		object GetValue(string name);
	} // interface IPpsXamlDynamicProperties

	#endregion

	#region -- class PpsParserService -------------------------------------------------

	/// <summary></summary>
	public abstract class PpsParserService : IServiceProvider
	{
		private int objectScope = -1;
		private PpsXamlReader serviceSite = null;

		internal void Initialize(PpsXamlReader serviceSite, int objectScope)
		{
			this.serviceSite = serviceSite;
			this.objectScope = objectScope;
			OnInitialized();
		} // proc Initialize

		/// <summary></summary>
		protected virtual void OnInitialized()
		{
		} // proc OnInitialized

		/// <summary></summary>
		protected void CheckInitialized()
		{
			if (serviceSite == null)
				throw new InvalidOperationException();
		} // proc CheckInitialized

		/// <summary></summary>
		/// <param name="serviceType"></param>
		/// <returns></returns>
		public object GetParentService(Type serviceType)
			=> serviceSite?.GetScopeService(objectScope, serviceType);

		/// <summary></summary>
		/// <param name="serviceType"></param>
		/// <returns></returns>
		public abstract object GetService(Type serviceType);

		internal int ObjectScope => objectScope;
		
		/// <summary></summary>
		public bool IsInitialized => serviceSite != null;
	} // class PpsParserService


	#endregion

	#region -- class PpsXamlSchemaContext ---------------------------------------------

	/// <summary>Schema context for the ppsn wpf ui.</summary>
	public class PpsXamlSchemaContext : XamlSchemaContext
	{
		private const string ppsXamlNamespace = "http://tecware-gmbh.de/ppsn/wpf/2015";

		#region -- class PpsXamlDynamicMember -----------------------------------------

		private class PpsXamlDynamicMemberInvoker : XamlMemberInvoker
		{
			private readonly PpsXamlDynamicMember member;

			public PpsXamlDynamicMemberInvoker(PpsXamlDynamicMember member)
			{
				this.member = member ?? throw new ArgumentNullException(nameof(member));
			}

			public override void SetValue(object instance, object value)
				=> ((IPpsXamlDynamicProperties)instance).SetValue(member.Name, value);

			public override object GetValue(object instance)
				=> ((IPpsXamlDynamicProperties)instance).GetValue(member.Name);
		} // class XamlDynamicInvoker 

		#endregion

		#region -- class PpsXamlDynamicMember -----------------------------------------

		private sealed class PpsXamlDynamicMember : XamlMember
		{
			public PpsXamlDynamicMember(PpsXamlDynamicPropertiesType type, string name)
				: base(name, type, false)
			{
			} // ctor

			protected override XamlType LookupType()
				=> DeclaringType.SchemaContext.GetXamlType(typeof(object));

			protected override ICustomAttributeProvider LookupCustomAttributeProvider()
				=> null;

			protected override bool LookupIsAmbient()
				=> false;

			protected override bool LookupIsReadOnly()
				=> false;

			protected override bool LookupIsReadPublic()
				=> true;

			protected override bool LookupIsEvent()
				=> false;

			protected override bool LookupIsWriteOnly()
				=> false;

			protected override bool LookupIsWritePublic()
				=> true;

			protected override bool LookupIsUnknown()
				=> false;

			protected override XamlType LookupTargetType()
				=> DeclaringType;

			protected override XamlMemberInvoker LookupInvoker()
				=> new PpsXamlDynamicMemberInvoker(this);

			protected override MemberInfo LookupUnderlyingMember()
				=> throw new NotSupportedException();

			protected override MethodInfo LookupUnderlyingGetter()
				=> throw new NotSupportedException();

			protected override MethodInfo LookupUnderlyingSetter()
				=> throw new NotSupportedException();

			protected override XamlValueConverter<TypeConverter> LookupTypeConverter()
				=> Type.TypeConverter;

			protected override XamlValueConverter<ValueSerializer> LookupValueSerializer()
				=> Type.ValueSerializer;

			protected override XamlValueConverter<XamlDeferringLoader> LookupDeferringLoader()
				=> null;

			protected override IList<XamlMember> LookupDependsOn()
				=> null;

			protected override IReadOnlyDictionary<char, char> LookupMarkupExtensionBracketCharacters()
				=> throw new NotImplementedException();
		} // class PpsXamlDynamicMember

		#endregion

		#region -- class PpsXamlDynamicPropertiesType ---------------------------------

		private class PpsXamlDynamicPropertiesType : XamlType
		{
			public PpsXamlDynamicPropertiesType(PpsXamlSchemaContext context, Type underlyingType)
				: base(underlyingType, context)
			{
			} // ctor

			protected override bool LookupIsUnknown()
				=> false;

			protected override XamlMember LookupMember(string name, bool skipReadOnlyCheck)
				=> base.LookupMember(name, skipReadOnlyCheck) ?? new PpsXamlDynamicMember(this, name);
		} // class PpsXamlDynamicPropertiesType

		#endregion

		private readonly XamlSchemaContext baseContext;

		private PpsXamlSchemaContext(XamlSchemaContext baseContext)
		{
			this.baseContext = baseContext ?? throw new ArgumentNullException(nameof(baseContext));
		} // ctor

		private static bool IsDynamicPropertiesImplemented(Type type)
			=> typeof(IPpsXamlDynamicProperties).IsAssignableFrom(type);

		/// <summary></summary>
		/// <param name="type"></param>
		/// <returns></returns>
		public override XamlType GetXamlType(Type type)
			=> IsDynamicPropertiesImplemented(type)
				? new PpsXamlDynamicPropertiesType(this, type)
				: baseContext.GetXamlType(type) ?? base.GetXamlType(type);

		/// <summary></summary>
		/// <param name="xamlNamespace"></param>
		/// <param name="name"></param>
		/// <param name="typeArguments"></param>
		/// <returns></returns>
		protected override XamlType GetXamlType(string xamlNamespace, string name, params XamlType[] typeArguments)
		{
			var type = baseContext.GetXamlType(
				new XamlTypeName(xamlNamespace, name,
					typeArguments != null
					? from c in typeArguments select new XamlTypeName(c)
						: null
				)
			);

			if (type == null)
				type = base.GetXamlType(xamlNamespace, name, typeArguments);

			return type != null && IsDynamicPropertiesImplemented(type.UnderlyingType)
				? new PpsXamlDynamicPropertiesType(this, type.UnderlyingType)
				: type;
		} // func GetXamlType

		/// <summary></summary>
		/// <param name="xamlNamespace"></param>
		/// <param name="compatibleNamespace"></param>
		/// <returns></returns>
		public override bool TryGetCompatibleXamlNamespace(string xamlNamespace, out string compatibleNamespace)
			=> baseContext.TryGetCompatibleXamlNamespace(xamlNamespace, out compatibleNamespace)
				 || base.TryGetCompatibleXamlNamespace(xamlNamespace, out compatibleNamespace);

		/// <summary></summary>
		/// <returns></returns>
		public override IEnumerable<string> GetAllXamlNamespaces()
			=> baseContext.GetAllXamlNamespaces().Union(base.GetAllXamlNamespaces());

		/// <summary></summary>
		/// <param name="xamlNamespace"></param>
		/// <returns></returns>
		public override ICollection<XamlType> GetAllXamlTypes(string xamlNamespace)
			=> baseContext.GetAllXamlTypes(xamlNamespace).Concat(base.GetAllXamlTypes(xamlNamespace)).ToArray();

		/// <summary></summary>
		/// <param name="xmlns"></param>
		/// <returns></returns>
		public override string GetPreferredPrefix(string xmlns)
			=> baseContext.GetPreferredPrefix(xmlns) ?? base.GetPreferredPrefix(xmlns);

		/// <summary></summary>
		/// <param name="xamlNamespace"></param>
		/// <param name="name"></param>
		/// <returns></returns>
		public override XamlDirective GetXamlDirective(string xamlNamespace, string name)
		{
			if (xamlNamespace == ppsXamlNamespace && name == ParserService.Name)
				return ParserService;
			return baseContext.GetXamlDirective(xamlNamespace, name) ?? base.GetXamlDirective(xamlNamespace, name);
		} // func GetXamlDirective

		/// <summary></summary>
		/// <param name="assemblyName"></param>
		/// <returns></returns>
		protected override Assembly OnAssemblyResolve(string assemblyName)
			=> String.IsNullOrEmpty(assemblyName)
				? Assembly.GetExecutingAssembly()
				: base.OnAssemblyResolve(assemblyName);

		private static readonly Lazy<XamlDirective> parserServiceProvider;

		static PpsXamlSchemaContext()
		{
			Default = new PpsXamlSchemaContext(System.Windows.Markup.XamlReader.GetWpfSchemaContext());

			parserServiceProvider = new Lazy<XamlDirective>(
				() => new XamlDirective(new string[] { ppsXamlNamespace }, "ParserService", Default.GetXamlType(typeof(PpsParserService)), null, AllowedMemberLocations.MemberElement)
			);
		} // sctor

		/// <summary>Default ppsn wpf schema context.</summary>
		public static XamlSchemaContext Default { get; }
		/// <summary>Directive to support service within the parser.</summary>
		public static XamlDirective ParserService => parserServiceProvider.Value;
	} // class PpsXamlSchemaContext

	#endregion

	#region -- class PpsXamlReaderSettings --------------------------------------------

	/// <summary></summary>
	/// <param name="reader"></param>
	/// <returns><c>true</c>, is the curent node is readed. <c>false</c>, for eof.</returns>
	public delegate bool FilterNodeHandler(PpsXamlReader reader);

	/// <summary>Settings for the ppsn xaml reader.</summary>
	public class PpsXamlReaderSettings : XamlXmlReaderSettings
	{
		/// <summary></summary>
		public PpsXamlReaderSettings()
		{
		} // ctor

		/// <summary></summary>
		public PpsXamlReaderSettings(XamlXmlReaderSettings settings)
			: base(settings)
		{
			if (settings is PpsXamlReaderSettings pps)
			{
				this.Code = pps.Code;
				this.DebugWriter = pps.DebugWriter;
			}
		} // ctor

		internal bool InvokeFilterNode(PpsXamlReader reader)
			=> FilterNode?.Invoke(reader) ?? true;

		/// <summary>Code behind.</summary>
		public IPpsXamlCode Code { get; set; } = null;
		/// <summary>Debug the emitted notes.</summary>
		public TextWriter DebugWriter { get; set; } = null;
		/// <summary></summary>
		public FilterNodeHandler FilterNode { get; set; }
		/// <summary></summary>
		public IServiceProvider ServiceProvider { get; set; }
	} // class PpsXamlReaderSettings

	#endregion

	#region -- class PpsXamlReader ----------------------------------------------------

	/// <summary></summary>
	public sealed class PpsXamlReader : System.Xaml.XamlReader, IXamlLineInfo, IServiceProvider
	{
		#region -- class PpsXamlMemberEmitter -----------------------------------------

		private sealed class PpsXamlMemberEmitter : System.Xaml.XamlReader
		{
			private int state = -1;
			private readonly XamlMember member;
			private readonly object value;

			public PpsXamlMemberEmitter(XamlMember member, object value)
			{
				this.member = member ?? throw new ArgumentNullException(nameof(member));
				this.value = value;
			} // ctor

			public override bool Read()
			{
				if (IsEof)
					return false;

				state++;
				return state <= 2;
			} // func Read

			public override bool IsEof => value == null || state > 2;

			public override XamlNodeType NodeType
			{
				get
				{
					switch (state)
					{
						case 0:
							return XamlNodeType.StartMember;
						case 1:
							return XamlNodeType.Value;
						case 2:
							return XamlNodeType.EndMember;
						default:
							return XamlNodeType.None;
					}
				}
			} // prop NodeType

			public override NamespaceDeclaration Namespace => null;
			public override XamlType Type => null;

			public override object Value => state == 1 ? value : null;
			public override XamlMember Member => member;
			public override XamlSchemaContext SchemaContext => member.DeclaringType.SchemaContext;
		} // class PpsXamlMemberEmitter

		#endregion

		#region -- class PpsXamlNodeEmitter -------------------------------------------

		private sealed class PpsXamlNodeEmitter : System.Xaml.XamlReader
		{
			private int state = -1;
			private readonly XamlMember member;
			private readonly XamlNodeType nodeType;

			public PpsXamlNodeEmitter(XamlMember member, XamlNodeType nodeType)
			{
				this.member = member ?? throw new ArgumentNullException(nameof(member));
				this.nodeType = nodeType;
			} // ctor

			public override bool Read()
			{
				if (IsEof)
					return false;

				state++;
				return state <= 0;
			} // func Read

			public override bool IsEof => state > 0;

			public override XamlNodeType NodeType => nodeType;

			public override NamespaceDeclaration Namespace => null;
			public override XamlType Type => null;

			public override object Value => null;
			public override XamlMember Member => member;
			public override XamlSchemaContext SchemaContext => member.DeclaringType.SchemaContext;
		} // class PpsXamlNodeEmitter

		#endregion

		#region -- class XamlInvokeMemberBinder  --------------------------------------

		private sealed class XamlInvokeMemberBinder : InvokeMemberBinder
		{
			public XamlInvokeMemberBinder(string name, bool ignoreCase, CallInfo callInfo)
				: base(name, ignoreCase, callInfo)
			{
			} // ctor


			public override DynamicMetaObject FallbackInvoke(DynamicMetaObject target, DynamicMetaObject[] args, DynamicMetaObject errorSuggestion)
				=> FallbackInvokeMember(target, args, errorSuggestion);

			public override DynamicMetaObject FallbackInvokeMember(DynamicMetaObject target, DynamicMetaObject[] args, DynamicMetaObject errorSuggestion)
				=> errorSuggestion ??
					new DynamicMetaObject(
						LExpression.Throw(LExpression.New(missingMemberConstructorInfo, 
							LExpression.Constant(target.LimitType.Name),
							LExpression.Constant(Name)
						), ReturnType),
						target.Restrictions
					);

			private static readonly ConstructorInfo missingMemberConstructorInfo;

			static XamlInvokeMemberBinder()
			{
				missingMemberConstructorInfo = typeof(MissingMemberException).GetConstructor(new Type[] { typeof(string), typeof(string) })
					?? throw new ArgumentNullException(nameof(MissingMemberException));
			} // sctor
		} // func CallDynamicBinder

		#endregion

		#region -- class PpsReaderStackItem -------------------------------------------

		private sealed class PpsReaderStackItem
		{
			private readonly PpsReaderStackItem parent;
			private Uri baseUri;
			private readonly IXamlLineInfo lineInfo;
			private readonly System.Xaml.XamlReader reader;

			public PpsReaderStackItem(PpsReaderStackItem parent, System.Xaml.XamlReader reader)
			{
				this.parent = parent;
				this.reader = reader ?? throw new ArgumentNullException(nameof(reader));


				if (reader is IXamlLineInfo lineInfo && lineInfo.HasLineInfo)
				{
					this.baseUri = baseUri ?? new Uri("unknown", UriKind.Relative);
					this.lineInfo = lineInfo;
				}
				else
				{
					this.baseUri = null;
					this.lineInfo = null;
				}
			} // ctor

			public void UpdateUri(Uri baseUri)
			{
				if (lineInfo != null)
					this.baseUri = baseUri;
			} // func UpdateUri

			public System.Xaml.XamlReader Reader => reader;
			public PpsReaderStackItem Parent => parent;

			public Uri BaseUri => baseUri ?? parent?.BaseUri;
			public IXamlLineInfo LineInfo => lineInfo ?? parent?.LineInfo;
		} // class PpsReaderStackItem

		#endregion

		private readonly XamlSchemaContext schemaContext;
		private readonly PpsXamlReaderSettings settings;
		private readonly Stack<PpsReaderStackItem> currentEmitterStack = new Stack<PpsReaderStackItem>();

		private readonly List<PpsParserService> services = new List<PpsParserService>();
		private int currentObjectLevel = 0;

		private int inReadMethod = 0;
		private int currentIndent = 0;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary></summary>
		/// <param name="sourceReader"></param>
		/// <param name="settings"></param>
		public PpsXamlReader(System.Xaml.XamlReader sourceReader, PpsXamlReaderSettings settings)
		{
			this.schemaContext = sourceReader.SchemaContext;
			this.settings = settings ?? new PpsXamlReaderSettings();

			if (sourceReader is PpsXamlReader)
				throw new ArgumentException("It is not allowed to create a PpsXamlReader for an PpsXamlReader", nameof(sourceReader));

			currentEmitterStack.Push(new PpsReaderStackItem(null, sourceReader));
		} // ctor

		/// <summary></summary>
		/// <param name="disposing"></param>
		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
			if (disposing)
			{
				// remove services
				currentObjectLevel = 0;
				PopCurrentServices();

				// remove emitter
				while (currentEmitterStack.Count > 0)
					currentEmitterStack.Pop().Reader.Close();
			}
		} // prop Dispose

		#endregion

		#region -- DebugNode ----------------------------------------------------------

		internal static string GetDebugString(System.Xaml.XamlReader reader, ref int currentIndent)
		{
			const int staticIndent = 2;

			// write indent
			string Indent(int indent)
				=> new string(' ', indent);

			string t;
			switch (reader.NodeType)
			{
				case XamlNodeType.NamespaceDeclaration:
					return Indent(currentIndent) + "Namespace: " + reader.Namespace.Prefix + ":" + reader.Namespace.Namespace;
				case XamlNodeType.StartMember:
					t = Indent(currentIndent) + "StartMember: " + reader.Member.Name;
					currentIndent += staticIndent;
					return t;
				case XamlNodeType.StartObject:
					t = Indent(currentIndent) + "StartObject: " + reader.Type.Name;
					currentIndent += staticIndent;
					return t;
				case XamlNodeType.EndMember:
					currentIndent -= staticIndent;
					return Indent(currentIndent) + "EndMember";
				case XamlNodeType.EndObject:
					currentIndent -= staticIndent;
					return Indent(currentIndent) + "EndObject";
				case XamlNodeType.GetObject:
					t = Indent(currentIndent) + "GetOject";
					currentIndent += staticIndent;
					return t;
				case XamlNodeType.Value:
					return Indent(currentIndent) + "Value: " + (reader.Value == null ? "<null>" : reader.Value.ToString());
				default:
					return Indent(currentIndent) + "None";
			}
		} // func GetDebugString

		private bool DebugNode()
		{
			var debugWriter = settings.DebugWriter;
			if (debugWriter == null)
				return true;

			debugWriter.WriteLine(GetDebugString(this, ref currentIndent));

			return true;
		} // func DebugNode

		#endregion

		#region -- Node Helper --------------------------------------------------------

		private void CheckInReadMethod()
		{
			if (inReadMethod <= 0)
				throw new InvalidOperationException();
		} // proc CheckInReadMethod

		private static Exception XamlParseException(System.Xaml.XamlReader reader, string message)
			=> throw new InvalidOperationException(message);

		private static Exception XamlParseException(System.Xaml.XamlReader reader, XamlNodeType expectedNodeType)
			=> throw new InvalidOperationException($"Expected: {expectedNodeType}, Actual: {reader.NodeType}");

		private static void EnforceNodeType(System.Xaml.XamlReader reader, XamlNodeType nodeType)
		{
			if (reader.NodeType != nodeType)
				throw XamlParseException(reader, nodeType);
		} // func EnforceNodeType

		private static void ReadNodeType(System.Xaml.XamlReader reader, XamlNodeType nodeType)
		{
			EnforceNodeType(reader, nodeType);
			ReadNode(reader);
		} // proc ReadNodeType 

		private static void ReadNode(System.Xaml.XamlReader reader)
		{
			if (!reader.Read())
				throw XamlParseException(reader, "Unexpected eof.");
		} // proc ReadNode

		/// <summary>Reads the current member value.</summary>
		/// <returns></returns>
		public object ReadMemberValue()
		{
			CheckInReadMethod();
			return ReadMemberValue(CurrentReader);
		} // func ReadMemberValue

		internal static object ReadMemberValue(System.Xaml.XamlReader reader)
		{
			ReadNodeType(reader, XamlNodeType.StartMember);

			object value;
			switch (reader.NodeType)
			{
				case XamlNodeType.Value:
					value = reader.Value;
					ReadNode(reader);
					break;
				case XamlNodeType.StartObject:
					value = XamlServices.Load(reader.ReadSubtree()); // calls dispose in Transform
					break;
				default:
					throw new InvalidOperationException(); // todo:
			}

			EnforceNodeType(reader, XamlNodeType.EndMember);
			return value;
		} // ReadMemberValue

		#endregion

		#region -- CompileEventConnector ----------------------------------------------

		private static object CompileEventConnector(Type handlerType, string memberName, object eventTarget)
		{
			var handler = handlerType;
			var sourceMethodInfo = handler.GetMethod("Invoke");
			var sourceParameterInfo = sourceMethodInfo.GetParameters();

			if (eventTarget is IDynamicMetaObjectProvider) // is dynamic member, generate dynamic call
			{
				// build parameter info
				var parameterExpressions = new ParameterExpression[sourceParameterInfo.Length];
				var dynamicParameterExpressions = new LExpression[sourceParameterInfo.Length + 1];
				//var argumentNames = new string[sourceParameterInfo.Length];

				dynamicParameterExpressions[0] = LExpression.Constant(eventTarget);
				for (var i = 0; i < sourceParameterInfo.Length; i++)
				{
					var pi = sourceParameterInfo[i];
					//argumentNames[i] = pi.Name;
					parameterExpressions[i] = LExpression.Parameter(pi.ParameterType, pi.Name);
					dynamicParameterExpressions[i + 1] = parameterExpressions[i];
				}
				
				var callInfo = new CallInfo(sourceParameterInfo.Length); // , argumentNames Lua uses Expression.GetDelegateType to create a call-type, this function does not respect parameter names.

				// bind code to target
				var eventHandler = LExpression.Lambda(handler,
					LExpression.Dynamic(
						new XamlInvokeMemberBinder(memberName, false, callInfo),
						typeof(object),
						dynamicParameterExpressions
					),
					parameterExpressions
				).Compile();

				// update member
				return eventHandler;
			}
			else
			{
				bool FindSignatureSimple(MethodInfo testMethodInfo)
				{
					var testParameterInfo = testMethodInfo.GetParameters();

					if (sourceParameterInfo.Length == testParameterInfo.Length)
					{
						for (var i = 0; i < sourceParameterInfo.Length; i++)
						{
							if (!sourceParameterInfo[i].ParameterType.IsAssignableFrom(testParameterInfo[i].ParameterType))
								return false;
						}
						return true;
					}
					else
						return false;
				} // func FindSignatureSimple

				var mi = eventTarget.GetType().GetRuntimeMethods().FirstOrDefault(FindSignatureSimple);
				if (mi == null)
					throw new MissingMethodException(eventTarget.GetType().Name, memberName);

				var parameterExpressions = new ParameterExpression[sourceParameterInfo.Length];
				var argumentExpressions = new LExpression[sourceParameterInfo.Length];
				for (var i = 0; i < sourceParameterInfo.Length; i++)
				{
					var pi = sourceParameterInfo[i];
					parameterExpressions[i] = LExpression.Parameter(pi.ParameterType, pi.Name);
					argumentExpressions[i] = LExpression.Convert(parameterExpressions[i], pi.ParameterType);
				}

				return LExpression.Lambda(handlerType,
					LExpression.Call(LExpression.Constant(eventTarget, mi.DeclaringType), mi, argumentExpressions),
					parameterExpressions
				).Compile();
			}
		} // proc CompileEventConnector

		#endregion

		#region -- Read ---------------------------------------------------------------

		/// <summary>Push a member with value.</summary>
		/// <param name="member"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public bool PushMember(XamlMember member, object value)
			=> PushEmitter(new PpsXamlMemberEmitter(member, value));

		/// <summary>Push an emitter in the XamlReader stream. Can only be called within the read method.</summary>
		/// <param name="emitter"></param>
		/// <returns></returns>
		public bool PushEmitter(System.Xaml.XamlReader emitter)
		{
			CheckInReadMethod();
			return PushEmitterIntern(emitter);
		} // proc PushEmitter

		/// <summary>Attach a service to the current object level.</summary>
		/// <param name="parserService"></param>
		public void PushServiceProvider(PpsParserService parserService)
		{
			parserService.Initialize(this, currentObjectLevel);
			services.Add(parserService ?? throw new ArgumentNullException(nameof(parserService)));
		} // proc PushServiceProvider

		private bool PushDelegate(XamlMember member, Type delegateType, object value)
		{
			switch (value)
			{
				case string memberName:
					value = CompileEventConnector(delegateType, memberName, settings.Code);
					break;
				case Delegate dlg:
					value = PpsXamlParser.CreateEventFromDelegate(delegateType, dlg);
					break;
				default:
					throw new ArgumentException("Can not assign event member."); // todo:
			}

			return PushMember(member, value);
		} // func PushDelegate

		private bool PushEmitterIntern(System.Xaml.XamlReader emitter)
		{
			currentEmitterStack.Push(new PpsReaderStackItem(CurrentItem, emitter));
			return Read();
		} // func PushEmitterIntern

		private bool PopEmitter()
		{
			var reader = currentEmitterStack.Pop();
			reader.Reader.Close();
			return currentEmitterStack.Count > 0;
		} // proc PopEmitter

		private void PopCurrentServices()
		{
			for(var i=services.Count-1;i>=0;i--)
			{
				if (services[i].ObjectScope >= currentObjectLevel)
				{
					if (services[i] is IDisposable d)
						d.Dispose();
					services.RemoveAt(i);
				}
				else
					break;
			}
		} // prop PopCurrentServices

		private bool ProcessNode(System.Xaml.XamlReader reader, bool result)
		{
			switch (reader.NodeType)
			{
				case XamlNodeType.StartMember:
					if (reader.Member == XamlLanguage.Base && !(reader is PpsXamlMemberEmitter))// baseuri can only be set once
					{
						var value = ReadMemberValue(reader);
						if (value != null)
						{
							var uri = (Uri)SchemaContext.GetXamlType(typeof(Uri)).TypeConverter.ConverterInstance.ConvertFrom(value);

							CurrentItem.UpdateUri(uri);
							if (IsTop) // remit for XamlObjectWriter (only the top value)
							{
								ReadNode(reader); // eat EndMember
								return PushEmitterIntern(new PpsXamlMemberEmitter(XamlLanguage.Base, value));
							}
						}

						return Read();
					}
					else if (Member == XamlLanguage.Code) // search for code tag
					{
						if (settings.Code == null)
							throw XamlParseException(reader, "Code derictive is not allowed in this context.");

						var value = ReadMemberValue(reader);
						if (value is string sourceCode)
							settings.Code.CompileCode(currentEmitterStack.Peek().BaseUri, sourceCode);
						else if (value is Uri sourceFile)
							settings.Code.CompileCode(sourceFile, null);
						else
							throw new ArgumentOutOfRangeException(); // todo:

						return Read();
					}
					else if (Member == PpsXamlSchemaContext.ParserService)
					{
						PushServiceProvider((PpsParserService)ReadMemberValue(this));
						return Read();
					}
					else if (settings.Code != null && Member.IsEvent && !(reader is PpsXamlMemberEmitter))
					{
						var member = Member;
						var eventHandlerType = PpsXamlParser.GetEventHandlerType(member);
						var eventValue = ReadMemberValue(reader);
						ReadNode(reader);

						return PushDelegate(member, eventHandlerType, eventValue);
					}
					else if (settings.Code != null && typeof(ICommand).IsAssignableFrom(Member.Type.UnderlyingType) && !(reader is PpsXamlMemberEmitter || reader is PpsXamlNodeEmitter)) // action should be set direct or dynamic
					{
						var member = Member;
						ReadNodeType(reader, XamlNodeType.StartMember);

						if (reader.NodeType == XamlNodeType.Value) // constant
						{
							var key = reader.Value;
							var value = ((LuaTable)settings.Code)[key];
							ReadNode(reader);
							ReadNodeType(reader, XamlNodeType.EndMember);

							return PushMember(member, value);
						}
						else
							return PushEmitter(new PpsXamlNodeEmitter(member, XamlNodeType.StartMember));
					}
					else if (settings.Code != null && typeof(Delegate).IsAssignableFrom(Member.Type.UnderlyingType) && !(reader is PpsXamlMemberEmitter)) // action should be set direct or dynamic
					{
						var member = Member;
						var delegateType = member.Type.UnderlyingType;
						var delegateValue = ReadMemberValue(reader);
						ReadNode(reader);

						return PushDelegate(member, delegateType, delegateValue);
					}
					goto default;
				case XamlNodeType.StartObject:
					if (!Type.IsUnknown && typeof(IPpsXamlEmitter).IsAssignableFrom(Type.UnderlyingType)) // special object to replace
					{
						// create a new object writer to build the emitter object
						var emitterFactory = (IPpsXamlEmitter)XamlServices.Load(reader.ReadSubtree()); // todo: own subreader for namespaces

						// create emitter
						var newEmitter = emitterFactory.CreateReader(this);
						if (newEmitter != null)
							return PushEmitterIntern(newEmitter);
					}
					currentObjectLevel++;
					goto default;
				case XamlNodeType.GetObject:
					currentObjectLevel++;
					goto default;
				case XamlNodeType.EndObject:
					PopCurrentServices();
					currentObjectLevel--;
					goto default;
				default:
					return settings.InvokeFilterNode(this);
			}
		} // func FilterNode

		/// <summary></summary>
		/// <returns></returns>
		public override bool Read()
		{
			if (IsEof)
				return false;

			inReadMethod++;
			try
			{
				var reader = CurrentReader;
				var result = reader.Read();
				if (result)
					return ProcessNode(reader, result) && (inReadMethod > 1 || DebugNode());
				else if (PopEmitter())
					return ProcessNode(CurrentReader, true) && (inReadMethod > 1 || DebugNode());
				else
					return false;
			}
			finally
			{
				inReadMethod--;
			}
		} // func Read

		#endregion

		/// <summary></summary>
		/// <param name="serviceType"></param>
		/// <returns></returns>
		public object GetService(Type serviceType)
			=> GetScopeService(currentObjectLevel + 1, serviceType);

		internal object GetScopeService(int currentObjectScope, Type serviceType)
		{
			for (var i = services.Count - 1; i >= 0; i--)
			{
				if (services[i].ObjectScope < currentObjectScope)
				{
					var t = services[i].GetService(serviceType);
					if (t != null)
						return t;
				}
			}

			if (serviceType == typeof(IPpsXamlCode))
				return settings.Code;
			else
				return settings.ServiceProvider?.GetService(serviceType);
		} // func GetService

		private bool IsTop => currentEmitterStack.Count == 1;
		private PpsReaderStackItem CurrentItem => currentEmitterStack.Count > 0 ? currentEmitterStack.Peek() : null;
		private System.Xaml.XamlReader CurrentReader => CurrentItem?.Reader ?? throw new ArgumentOutOfRangeException(nameof(CurrentReader));

		/// <summary></summary>
		public override bool IsEof => currentEmitterStack.Count == 0;
		/// <summary></summary>
		public override XamlNodeType NodeType => CurrentReader.NodeType;
		/// <summary></summary>
		public override NamespaceDeclaration Namespace => CurrentReader.Namespace;
		/// <summary></summary>
		public override XamlType Type => CurrentReader.Type;
		/// <summary></summary>
		public override XamlMember Member => CurrentReader.Member;
		/// <summary></summary>
		public override object Value => CurrentReader.Value;

		/// <summary></summary>
		public bool HasLineInfo => CurrentItem?.LineInfo?.HasLineInfo ?? false;
		/// <summary></summary>
		public int LineNumber => CurrentItem?.LineInfo?.LineNumber ?? 0;
		/// <summary></summary>
		public int LinePosition => CurrentItem?.LineInfo?.LinePosition ?? 0;

		/// <summary></summary>
		public override XamlSchemaContext SchemaContext => schemaContext;
		/// <summary></summary>
		public PpsXamlReaderSettings Settings => settings;
	} // class PpsXamlReader

	#endregion

	/// <summary></summary>
	public static class PpsXamlParser
	{
		private static readonly PropertyInfo wpfXamlMemberPropertyInfo;

		static PpsXamlParser()
		{
			var type = System.Type.GetType("System.Windows.Baml2006.WpfXamlMember,PresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35", true);
			//type.GetProperty("DependencyProperty")
			wpfXamlMemberPropertyInfo = type.GetProperty("RoutedEvent", BindingFlags.Public | BindingFlags.Instance | BindingFlags.GetProperty) ?? throw new ArgumentNullException("WpfXamlType.RoutedEvent");
		} // sctor

		internal static Type GetEventHandlerType(XamlMember member)
			=> wpfXamlMemberPropertyInfo.DeclaringType.IsAssignableFrom(member.GetType())
				? ((RoutedEvent)wpfXamlMemberPropertyInfo.GetValue(member)).HandlerType
				: member.Type.UnderlyingType;
		
		internal static object CreateEventFromDelegate(Type delegateType, Delegate dlg)
		{
			if (delegateType.IsAssignableFrom(dlg.GetType()))
				return dlg;
			else
			{
				var sourceMethodInfo = dlg.GetType().GetMethod("Invoke") ?? throw new ArgumentNullException(nameof(dlg), "Delegate-Invoke not defined.");

				// create convert delegate
				var targetMethodInfo = delegateType.GetMethod("Invoke") ?? throw new ArgumentNullException(nameof(delegateType), "Handler-Invoke not defined.");

				var targetParameterInfo = targetMethodInfo.GetParameters();
				var sourceParameterInfo = sourceMethodInfo.GetParameters();
				var targetParameterExpressions = new ParameterExpression[targetParameterInfo.Length];
				var sourceParameterExpressions = new LExpression[sourceParameterInfo.Length];

				var len = Math.Max(targetParameterInfo.Length, sourceParameterInfo.Length);
				for (var i = 0; i < len; i++)
				{
					var currentTargetParameterInfo = i < targetParameterInfo.Length ? targetParameterInfo[i] : null;
					var currentSourceParameterInfo = i < sourceParameterInfo.Length ? sourceParameterInfo[i] : null;
					ref var targetExpression = ref targetParameterExpressions[i];
					if (currentTargetParameterInfo != null && currentSourceParameterInfo != null)
					{
						targetExpression = LExpression.Parameter(currentTargetParameterInfo.ParameterType, currentTargetParameterInfo.Name);
						sourceParameterExpressions[i] = LExpression.Convert(targetExpression, currentSourceParameterInfo.ParameterType);
					}
					else if (currentSourceParameterInfo != null)
						sourceParameterExpressions[i] = LExpression.Default(currentSourceParameterInfo.ParameterType);
					else if (currentTargetParameterInfo != null)
						targetExpression = LExpression.Parameter(currentTargetParameterInfo.ParameterType, currentTargetParameterInfo.Name);
				}

				return LExpression.Lambda(delegateType,
					LExpression.Invoke(LExpression.Constant(dlg),
						sourceParameterExpressions
					),
					targetParameterExpressions
				).Compile();
			}
		} // func CreateEventFromDelegate

		/// <summary>Load xaml file from source.</summary>
		/// <param name="fileName"></param>
		/// <param name="readerSettings"></param>
		/// <param name="writerSettings"></param>
		/// <returns></returns>
		public static async Task<T> LoadAsync<T>(string fileName, PpsXamlReaderSettings readerSettings = null, XamlObjectWriterSettings writerSettings = null)
		{
			using (var xr = new PpsXamlReader(new XamlXmlReader(fileName, PpsXamlSchemaContext.Default, readerSettings), readerSettings))
				return await LoadAsync<T>(xr, writerSettings);
		} // proc LoadAsync

		/// <summary>Load xaml file from source.</summary>
		/// <param name="textReader"></param>
		/// <param name="readerSettings"></param>
		/// <param name="writerSettings"></param>
		/// <returns></returns>
		public static async Task<T> LoadAsync<T>(TextReader textReader, PpsXamlReaderSettings readerSettings = null, XamlObjectWriterSettings writerSettings = null)
		{
			using (var xr = new PpsXamlReader(new XamlXmlReader(textReader, PpsXamlSchemaContext.Default, readerSettings), readerSettings))
				return await LoadAsync<T>(xr, writerSettings);
		} // func LoadAsync

		/// <summary>Load xaml file from source.</summary>
		/// <param name="xmlReader"></param>
		/// <param name="readerSettings"></param>
		/// <param name="writerSettings"></param>
		/// <returns></returns>
		public static async Task<T> LoadAsync<T>(XmlReader xmlReader, PpsXamlReaderSettings readerSettings = null, XamlObjectWriterSettings writerSettings = null)
		{
			using (var xr = new PpsXamlReader(new XamlXmlReader(xmlReader, PpsXamlSchemaContext.Default, readerSettings), readerSettings))
				return await LoadAsync<T>(xr, writerSettings);
		} // func LoadAsync

		/// <summary>Load xaml file from reader.</summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="xamlReader"></param>
		/// <param name="writerSettings"></param>
		/// <returns></returns>
		public static async Task<T> LoadAsync<T>(System.Xaml.XamlReader xamlReader, XamlObjectWriterSettings writerSettings = null)
		{
			try
			{
				var xw = new XamlObjectWriter(PpsXamlSchemaContext.Default, writerSettings ?? new XamlObjectWriterSettings());
				try
				{
					await TransformAsync(xamlReader, xw);
					return (T)xw.Result;
				}
				catch
				{
					xw = null; // Dispose can throw exception?
					throw;
				}
				finally
				{
					xw?.Close();
				}
			}
			finally
			{
				xamlReader?.Close();
			}
		} // func LoadAsync

		/// <summary></summary>
		/// <param name="xamlReader"></param>
		/// <param name="xamlWriter"></param>
		/// <param name="timeSlot"></param>
		/// <returns></returns>
		public static async Task TransformAsync(System.Xaml.XamlReader xamlReader, System.Xaml.XamlWriter xamlWriter, int timeSlot = 300)
		{
			var lineReader = xamlReader as IXamlLineInfo;
			var lineWriter = xamlWriter as IXamlLineInfoConsumer;
			
			var copyLineInfo = lineReader != null && lineReader.HasLineInfo
				&& lineWriter != null && lineWriter.ShouldProvideLineInfo;
			
			var currentIndent = 0;
			var startTick = Environment.TickCount;
			while (xamlReader.Read())
			{
				if (copyLineInfo && lineReader.LineNumber != 0)
					lineWriter.SetLineInfo(lineReader.LineNumber, lineReader.LinePosition);

				// copy nodes
				if (DebugTransform)
					Debug.Print(PpsXamlReader.GetDebugString(xamlReader, ref currentIndent));
						
				xamlWriter.WriteNode(xamlReader);

				if (unchecked((int)(Environment.TickCount - startTick)) > timeSlot)
				{
					await Task.Yield();
					startTick = Environment.TickCount;
				}
			}
		} // func TransformAsync

		/// <summary></summary>
		public static bool DebugTransform { get; set; } = false;
	} // class PpsXamlParser
}
