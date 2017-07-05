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
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Data;
using TecWare.DE.Networking;
using TecWare.DE.Server;
using TecWare.DE.Server.Http;
using TecWare.DE.Stuff;
using TecWare.PPSn.Server.Data;
using TecWare.PPSn.Server.Sql;
using static TecWare.PPSn.Server.PpsStuff;

namespace TecWare.PPSn.Server
{
	#region -- class PpsFieldDescription ------------------------------------------------

	/// <summary>Description of a field.</summary>
	public sealed class PpsFieldDescription : IPpsColumnDescription
	{
		private const string displayNameAttributeString = "displayName";
		private const string maxLengthAttributeString = "maxLength";
		private const string dataTypeAttributeString = "dataType";
		private const string inheritedAttributeString = "inherited";

		#region -- class PpsFieldAttributesEnumerator -----------------------------------

		private sealed class PpsFieldAttributesEnumerator : IEnumerator<PropertyValue>
		{
			private readonly List<string> emittedProperties = new List<string>();
			private readonly PpsFieldDescription field;
			private int state = 0;

			private PropertyValue currentProperty;

			private readonly Stack<IPpsColumnDescription> nativeColumnDescriptors = new Stack<IPpsColumnDescription>();
			private PpsFieldDescription currentField = null;
			private IEnumerator<XElement> currentFieldEnumerator = null;
			private IEnumerator<PropertyValue> currentAttributesEnumerator = null;

			public PpsFieldAttributesEnumerator(PpsFieldDescription field)
			{
				this.field = field;
				Reset();
			} // ctor

			public void Dispose()
				=> Reset();

			private bool PropertyEmitted(string propertyName)
			{
				if (String.IsNullOrEmpty(propertyName))
					return true;

				var idx = emittedProperties.BinarySearch(propertyName, StringComparer.OrdinalIgnoreCase);
				if (idx >= 0)
					return true;
				emittedProperties.Insert(~idx, propertyName);
				return false;
			} // func PropertyEmit

			private bool EmitPropertyValue(PropertyValue v, int nextState)
			{
				if (PropertyEmitted(v.Name))
					return false;

				this.state = nextState;
				this.currentProperty = v;
				return true;
			} // func EmitPropertyValue

			public bool MoveNext()
			{
				PropertyValue r;
				switch (state)
				{
					case 0:
						if (currentField == null)
						{
							state = 5;
							goto case 5;
						}
						else
						{
							if (currentField.nativeColumnDescription != null)
								nativeColumnDescriptors.Push(currentField.nativeColumnDescription);

							currentFieldEnumerator = currentField.xDefinition.Elements(xnFieldAttribute).GetEnumerator();
							goto case 1;
						}
					case 1:
						if (currentField.TryGetAttributeBasedProperty(displayNameAttributeString, typeof(string), out r) && EmitPropertyValue(r, 2))
							return true;
						else
							goto case 2;
					case 2:
						if (currentField.TryGetAttributeBasedProperty(dataTypeAttributeString, typeof(string), out r) && EmitPropertyValue(r, 3))
							return true;
						else
							goto case 3;
					case 3:
						if (currentField.TryGetAttributeBasedProperty(maxLengthAttributeString, typeof(string), out r) && EmitPropertyValue(r, 4))
							return true;
						else
							goto case 4;
					case 4:
						while (currentFieldEnumerator.MoveNext())
						{
							var name = currentFieldEnumerator.Current.GetAttribute<string>("name", null);
							if (!PropertyEmitted(name))
							{
								currentProperty = GetPropertyFromElement(currentFieldEnumerator.Current);
								return true;
							}
						}
						currentFieldEnumerator.Dispose();
						currentFieldEnumerator = null;
						currentField = currentField.inheritedDefinition;
						goto case 0;

					case 5:
						if (nativeColumnDescriptors.Count == 0)
						{
							state = 7;
							goto default;
						}
						else
						{
							var nativeColumnDescription = nativeColumnDescriptors.Pop();
							currentAttributesEnumerator = nativeColumnDescription.Attributes.GetEnumerator();
							state = 6;
							goto case 6;
						}
					case 6:
						while (currentAttributesEnumerator.MoveNext())
						{
							if (!PropertyEmitted(currentAttributesEnumerator.Current.Name))
							{
								currentProperty = currentAttributesEnumerator.Current;
								return true;
							}
						}
						currentAttributesEnumerator.Dispose();
						currentAttributesEnumerator = null;
						goto case 5;

					default:
						return false;
				}
			} // func MoveNext

			public void Reset()
			{
				state = 0;
				currentProperty = null;
				currentField = field;
				currentFieldEnumerator?.Dispose();
				currentFieldEnumerator = null;
				currentAttributesEnumerator?.Dispose();
				currentAttributesEnumerator = null;
			} // prop Reset
	
			public PropertyValue Current 
				=> currentProperty;

			object IEnumerator.Current => currentProperty;
		} // class PpsFieldAttributesEnumerator

		#endregion

		#region -- class PpsFieldAttributes ---------------------------------------------

		private sealed class PpsFieldAttributes : IPropertyEnumerableDictionary
		{
			private readonly PpsFieldDescription field;

			public PpsFieldAttributes(PpsFieldDescription field)
			{
				this.field = field;
			} // ctor
			
			public bool TryGetProperty(string name, out object value)
			{
				var p = field.GetProperty(name);
				if (p != null)
				{
					value = p.Value;
					return true;
				}
				else
				{
					value = null;
					return false;
				}
			} // func TryGetProperty
			
			public IEnumerator<PropertyValue> GetEnumerator()
				=> new PpsFieldAttributesEnumerator(field);

			IEnumerator IEnumerable.GetEnumerator()
				=> GetEnumerator();
		} // class PpsFieldAttributes

		#endregion

		private readonly PpsDataSource source;	// attached datasource
		private readonly string name; // name of the field
		private readonly string inheritedFieldName; // name for the base field
		private readonly IPropertyEnumerableDictionary attributes; // attributes
		private readonly XElement xDefinition; // definition in configuration

		private IPpsColumnDescription nativeColumnDescription = null; // assign field description of the datasource
		private PpsFieldDescription inheritedDefinition = null; // inherited field description

		private readonly Lazy<string> displayName;
		private readonly Lazy<int> maxLength;
		private readonly Lazy<Type> dataType;

		private bool isInitialzed = false;

		#region -- Ctor/Dtor --------------------------------------------------------------

		public PpsFieldDescription(PpsDataSource source, string name, XElement xDefinition)
		{
			this.source = source;
			this.name = name;
			this.attributes = new PpsFieldAttributes(this);
			this.xDefinition = xDefinition;

			this.inheritedFieldName = xDefinition.GetAttribute(inheritedAttributeString, (string)null);
						
			displayName = new Lazy<string>(() => this.Attributes.GetProperty(displayNameAttributeString, null));
			maxLength = new Lazy<int>(() => this.Attributes.GetProperty(maxLengthAttributeString, Int32.MaxValue));

			var xDataType = xDefinition.Attribute(dataTypeAttributeString);

			dataType = xDataType != null ? 
				new Lazy<Type>(() => LuaType.GetType(xDataType.Value, lateAllowed: false)):
				null;
		} // ctor

		private void CheckInitialized()
		{
			if (!isInitialzed)
				throw new InvalidOperationException($"Call of {nameof(EndInit)} is missing.");
		} // proc CheckInitialized

		/// <summary>Fills the network</summary>
		public void EndInit()
		{
			// find inheritied column
			if (!String.IsNullOrEmpty(inheritedFieldName))
				inheritedDefinition = source.Application.GetFieldDescription(inheritedFieldName, true);
			
			// Resolve a native column description for the field.
			this.nativeColumnDescription = source.GetColumnDescription(name); // no exception

			isInitialzed = true;
		} // proc EndInit

		#endregion

		#region -- GetProperty ------------------------------------------------------------

		private static PropertyValue GetPropertyFromElement(XElement xAttribute)
		{
			if (xAttribute?.Value == null)
				return null;

			try
			{
				var name = xAttribute.GetAttribute<string>("name", null);
				if (String.IsNullOrEmpty(name))
					throw new ArgumentNullException("name");

				object value;
				var dataType = LuaType.GetType(xAttribute.GetAttribute("dataType", typeof(string))).Type;
				if (dataType == typeof(Type))
					value = (object)LuaType.GetType(xAttribute.Value, lateAllowed: false).Type;
				else
					value = Procs.ChangeType(xAttribute.Value, dataType);

				return new PropertyValue(name, dataType, value);
			}
			catch
			{
				return null;
			}
		} // func GetProperty

		private bool TryGetAttributeBasedProperty(string attributeName, Type propertyType, out PropertyValue r)
		{
			var xAttr = xDefinition.Attribute(attributeName);
			if (xAttr != null)
			{
				r = new PropertyValue(attributeName, propertyType, xAttr.Value);
				return true;
			}
			else
			{
				r = null;
				return false;
			}
		} // func TryGetAttributeBasedProperty

		private PropertyValue GetProperty(string propertyName)
		{
			bool TryGetAttributeBasedPropertyLocal(string attributeName, Type propertyType, out PropertyValue r)
			{
				if (String.Compare(attributeName, propertyName, StringComparison.OrdinalIgnoreCase) == 0
					&& TryGetAttributeBasedProperty(attributeName, propertyType, out r))
					return true;
				r = null;
				return false;
			} // func TryGetAttributeBasedProperty

			CheckInitialized();

			// find a attribute property
			if (TryGetAttributeBasedPropertyLocal(displayNameAttributeString, typeof(string), out var ret))
				return ret;
			if (TryGetAttributeBasedPropertyLocal(dataTypeAttributeString, typeof(Type), out ret))
				return ret;
			if (TryGetAttributeBasedPropertyLocal(maxLengthAttributeString, typeof(Type), out ret))
				return ret;

			// search for a attribute field, with the specific name
			ret = GetPropertyFromElement(
				xDefinition.Elements(xnFieldAttribute)
					.FirstOrDefault(c => String.Compare(c.GetAttribute<string>("name", null), propertyName, StringComparison.OrdinalIgnoreCase) == 0)
			);
			if (ret != null)
				return ret;

			// ask native, inherited
			ret = inheritedDefinition?.GetProperty(propertyName);
			if (ret != null)
				return ret;

			object v = null;
			if (nativeColumnDescription?.Attributes.TryGetProperty(propertyName, out v) ?? false)
				return new PropertyValue(propertyName, v);

			return null;
		} // func GetProperty

		#endregion

		#region -- IEnumerable ------------------------------------------------------------

		private IEnumerable<PropertyValue> GetAttributesConverted(string attributeSelector)
		{
			foreach (var c in Attributes)
			{
				if (c.Name.StartsWith(attributeSelector)) // todo: convert standard attribute
					yield return c;
			}
		} // proc GetAttributesConverted

		public IEnumerable<PropertyValue> GetAttributes(string attributeSelector)
		{
			if (attributeSelector == "*")
				return Attributes;
			else
				return GetAttributesConverted(attributeSelector);
		} // func GetAttributes

		#endregion

		/// <summary>Gets the specific column description.</summary>
		/// <typeparam name="T">Specific type for the description.</typeparam>
		/// <returns></returns>
		public T GetColumnDescription<T>()
			where T : IPpsColumnDescription
		{
			if (typeof(T).IsAssignableFrom(GetType()))
				return (T)(IPpsColumnDescription)this;
			else if (nativeColumnDescription != null && typeof(T).IsAssignableFrom(nativeColumnDescription.GetType()))
				return (T)nativeColumnDescription;
			else
				return inheritedDefinition == null ? default(T) : inheritedDefinition.GetColumnDescription<T>();
		} // func GetColumnDescription

		public PpsDataSource DataSource => source;
		public string Name => name;
		public string DisplayName => displayName.Value;

		public int MaxLength => Attributes.GetPropertyLate("MaxLength", () => maxLength.Value);
		public Type DataType => dataType?.Value ?? (nativeColumnDescription?.DataType ?? typeof(string));

		public IPropertyEnumerableDictionary Attributes => attributes;
	} // class PpsFieldDescription

	#endregion

	#region -- class PpsViewParameterDefinition -----------------------------------------

	public sealed class PpsViewParameterDefinition
	{
		private readonly string name;
		private readonly string displayName;
		private readonly string parameter;

		public PpsViewParameterDefinition(string name, string displayName, string parameter)
		{
			if (String.IsNullOrEmpty(name))
				throw new ArgumentNullException("name");
			
			this.name = name;
			this.displayName = displayName;
			this.parameter = parameter;
		} // ctor

		public PpsViewParameterDefinition(XElement x)
		{
			this.name = x.GetAttribute<string>("name", null);
			if (String.IsNullOrEmpty(name))
				throw new DEConfigurationException(x, "@name is missing.");

			this.displayName = x.GetAttribute<string>("displayName", null);
			this.parameter = x.Value;
		} // ctor

		internal void WriteElement(XmlWriter xml, string tagName)
		{
			xml.WriteStartElement(tagName);
			xml.WriteAttributeString("name", Name);
			xml.WriteAttributeString("displayName", DisplayName);
			xml.WriteEndElement();
		} // proc WriteElement

		public string Name => name;
		public string DisplayName => displayName ?? name;
		public string Parameter => parameter;

		public bool IsVisible => displayName != null;

		public static PpsViewParameterDefinition[] EmptyArray { get; } = new PpsViewParameterDefinition[0];
	} // class PpsViewParameterDefinition

	#endregion

	#region -- class PpsViewDefinition --------------------------------------------------

	public sealed class PpsViewDescription
	{
		private readonly IPpsSelectorToken selectorToken;
		private readonly string displayName;

		private readonly PpsViewParameterDefinition[] filter;
		private readonly PpsViewParameterDefinition[] order;

		private readonly IPropertyReadOnlyDictionary attributes;

		public PpsViewDescription(IPpsSelectorToken selectorToken, string displayName, PpsViewParameterDefinition[] filter, PpsViewParameterDefinition[] order, IPropertyReadOnlyDictionary attributes)
		{
			this.selectorToken = selectorToken;
			this.displayName = displayName;

			this.filter = filter ?? PpsViewParameterDefinition.EmptyArray;
			this.order = order ?? PpsViewParameterDefinition.EmptyArray;

			this.attributes = attributes ?? new PropertyDictionary();
		} // ctor

		public string LookupOrder(string orderName)
			=> order.FirstOrDefault(c => String.Compare(orderName, c.Name, StringComparison.OrdinalIgnoreCase) == 0)?.Parameter;

		public string LookupFilter(string filterName)
			=> filter.FirstOrDefault(c => String.Compare(filterName, c.Name, StringComparison.OrdinalIgnoreCase) == 0)?.Parameter;

		public string Name => selectorToken.Name;
		public string DisplayName => displayName ?? selectorToken.Name;
		public string SecurityToken => null;

		public PpsViewParameterDefinition[] Filter => filter;
		public PpsViewParameterDefinition[] Order => order;

		public IPropertyReadOnlyDictionary Attributes => attributes;

		public IPpsSelectorToken SelectorToken => selectorToken;

		public bool IsVisible => displayName != null;
	} // class PpsViewDefinition

	#endregion

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public partial class PpsApplication
	{
		#region -- class PpsViewDefinitionInit --------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class PpsViewDescriptionInit
		{
			private readonly PpsDataSource source;
			private readonly string name;
			private readonly XElement xDefinition;
			
			public PpsViewDescriptionInit(PpsDataSource source, string name, XElement xDefinition)
			{
				this.source = source;
				this.name = name;
				this.xDefinition = xDefinition;
			} // ctor

			public async Task<PpsViewDescription> InitializeAsync()
			{
				// create the selector source
				var sourceDescription = xDefinition.Element(xnSource);
				if (sourceDescription == null)
					throw new DEConfigurationException(xDefinition, "source definition is missing.");

				var selectorToken = await source.CreateSelectorTokenAsync(name, sourceDescription);
				
				var view = new PpsViewDescription(
					selectorToken,
					xDefinition.GetAttribute("displayName", (string)null),
					xDefinition.Elements(xnFilter).Select(x => new PpsViewParameterDefinition(x)).ToArray(),
					xDefinition.Elements(xnOrder).Select(x => new PpsViewParameterDefinition(x)).ToArray(),
					xDefinition.Elements(xnAttribute).ToPropertyDictionary()
				);

				return view;
			} // proc InitializeAsync
		} // class PpsViewDefinitionInit

		#endregion
		
		#region -- class DependencyElement ------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class DependencyElement
		{
			private readonly PpsDataSource source;
			private readonly string name;
			private readonly string[] inherited;
			private readonly XElement element;

			public DependencyElement(PpsDataSource source, XElement element)
			{
				this.source = source;

				this.name = element.GetAttribute("name", String.Empty);
				if (String.IsNullOrEmpty(this.name))
					throw new DEConfigurationException(element, "@name is empty.");

				var inheritedStrings = element.GetAttribute("inherited", String.Empty);
				if (String.IsNullOrEmpty(inheritedStrings))
					this.inherited = null;
				else
				{
					var tmp = inheritedStrings.Split(new char[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
					inherited = tmp.Length == 0 ? null : tmp;
				}

				this.element = element;
			} // ctor

			public override string ToString()
				=> $"{GetType().Name}: {name}";

			public PpsDataSource Source => source;
			public string Name => name;
			public string[] Inherited => inherited;
			public XElement Element => element;

			private bool IsRegistered { get; set; } = false;

			private static void RegisterDependencyItem(Stack<string> stack, List<DependencyElement> list, DependencyElement dependencyElement, Action<PpsDataSource, string, XElement> registerMethod)
			{
				if (dependencyElement.inherited != null)
				{
					foreach (var cur in dependencyElement.inherited)
					{
						// find the current dependency - lax
						var index = list.FindIndex(c => String.Compare(c.Name, cur, StringComparison.OrdinalIgnoreCase) == 0);
						if (index >= 0 && !list[index].IsRegistered)
						{
							// check recursion
							if (stack.FirstOrDefault(c => String.Compare(c, cur, StringComparison.OrdinalIgnoreCase) == 0) != null)
								throw new ArgumentOutOfRangeException("todo: rek");

							// register the dependency
							stack.Push(cur);
							RegisterDependencyItem(stack, list, list[index], registerMethod);
							list[index].IsRegistered = true;
						}
					}
				}

				registerMethod(dependencyElement.Source, dependencyElement.Name, dependencyElement.Element);
			} // proc RegisterDependencyItem

			public static void RegisterList(List<DependencyElement> list, Action<PpsDataSource, string, XElement> registerMethod)
			{
				var stack = new Stack<string>();

				// resolve dependencies
				for (var i = 0; i < list.Count; i++)
				{
					if (list[i] != null)
					{
						stack.Clear();
						RegisterDependencyItem(stack, list, list[i], registerMethod);
						list[i].IsRegistered = true;
					}
				}
			} // proc RegisterList
		} // class DependencyElement

		#endregion

		private PpsSqlExDataSource mainDataSource;

		private Dictionary<string, PpsFieldDescription> fieldDescription = new Dictionary<string, PpsFieldDescription>(StringComparer.OrdinalIgnoreCase);
		private Dictionary<string, PpsViewDescription> viewController = new Dictionary<string, PpsViewDescription>(StringComparer.OrdinalIgnoreCase);
		private Dictionary<string, PpsDataSetServerDefinition> datasetDefinitions = new Dictionary<string, PpsDataSetServerDefinition>(StringComparer.OrdinalIgnoreCase);

		#region -- Init/Done --------------------------------------------------------------

		private void InitData()
		{
			//mainDataSource = new PpsSqlExDataSource(this, "Data Source=Gurke,6444;Initial Catalog=PPSn01;Integrated Security=True");
		} // proc InitData

		private void BeginReadConfigurationData(IDEConfigLoading config)
		{
			// find mainDataSource
			var mainDataSourceName = config.ConfigNew.GetAttribute("mainDataSource", String.Empty);
			if (String.IsNullOrEmpty(mainDataSourceName))
				throw new DEConfigurationException(config.ConfigNew, "@mainDataSource is empty.");

			var newMainDataSource = this.UnsafeFind(mainDataSourceName);
			if (newMainDataSource == null)
				throw new DEConfigurationException(config.ConfigNew, String.Format("@mainDataSource '{0}' not found.", mainDataSourceName));
			if(!(newMainDataSource is PpsSqlExDataSource))
				throw new DEConfigurationException(config.ConfigNew, String.Format("@mainDataSource '{0}' is a unsupported data source.", mainDataSourceName));

			config.EndReadAction(() => mainDataSource = (PpsSqlExDataSource)newMainDataSource);
		} // proc BeginReadConfigurationData

		private void BeginEndConfigurationData(IDEConfigLoading config)
		{
			var fieldDeclarationList = new List<DependencyElement>();
			var viewDeclarationList = new List<DependencyElement>();
			var datasetDeclarationList = new List<DependencyElement>();
		
			// register views, columns, ...
			// we add or overide elements, but there is no deletion -> reboot
			foreach (var xRegister in config.ConfigNew.Elements(xnRegister))
			{
				// evaluate the source, that is connected to the objects
				var sourceName = xRegister.GetAttribute("source", String.Empty);
				var source = String.IsNullOrEmpty(sourceName) ? null : GetDataSource(sourceName, true);

				foreach (var xNode in xRegister.Elements())
				{
					if (xNode.Name == xnField)
						fieldDeclarationList.Add(new DependencyElement(source, xNode));
					else if (xNode.Name == xnView)
						viewDeclarationList.Add(new DependencyElement(source, xNode));
					else if (xNode.Name == xnDataSet)
						datasetDeclarationList.Add(new DependencyElement(source, xNode));
					else
						throw new NotImplementedException();
				}
			}

			DependencyElement.RegisterList(fieldDeclarationList, RegisterField); // register all fields
			DependencyElement.RegisterList(viewDeclarationList, RegisterView); // register all views
			DependencyElement.RegisterList(datasetDeclarationList, RegisterDataSet); // register all datasets
		} // proc BeginEndConfigurationData

		private void DoneData()
		{
		} // proc DoneData

		#endregion

		#region -- Register ---------------------------------------------------------------

		private void RegisterField(PpsDataSource source, string name, XElement x)
		{
			var fieldInfo = new PpsFieldDescription(source, name, x); // create generic field definition
			fieldDescription[name] = fieldInfo;
			if (source != null) // create a provider specific field
			{
				RegisterInitializationTask(10001, "Resolve columns", () =>
				{
					fieldInfo.EndInit(); // do throw exception here
					return Task.CompletedTask;
				});
			}
		} // proc RegisterField

		public void RegisterView(IPpsSelectorToken selectorToken, string displayName = null)
		{
			RegisterView(new PpsViewDescription(selectorToken, displayName, null, null, null));
		} // func RegisterView

		private void RegisterView(PpsDataSource source, string name, XElement x)
		{
			var cur = new PpsViewDescriptionInit(source, name, x);
			RegisterInitializationTask(10002, "Build views", async () => RegisterView(await cur.InitializeAsync()));
		} // func RegisterView

		private void RegisterView(PpsViewDescription view)
		{
			lock (viewController)
				viewController[view.Name] = view;
		} // func RegisterView
		
		private void RegisterDataSet(PpsDataSource source, string name, XElement x)
		{
			var datasetDefinition = (source ?? mainDataSource).CreateDataSetDefinition(name, x, DateTime.Now);
			lock (datasetDefinitions)
				datasetDefinitions.Add(datasetDefinition.Name, datasetDefinition);
		} // void RegisterDataSet

		#endregion

		[LuaMember]
		public PpsDataSource GetDataSource(string name, bool throwException)
		{
			using (this.EnterReadLock())
				return (PpsDataSource)this.UnsafeChildren.FirstOrDefault(c => c is PpsDataSource && String.Compare(c.Name, name, StringComparison.OrdinalIgnoreCase) == 0);
		} // func GetDataSource

		[LuaMember]
		public PpsFieldDescription GetFieldDescription(string name, bool throwException = true)
		{
			if (fieldDescription.TryGetValue(name, out var fieldInfo))
				return fieldInfo;
			else if (throwException)
				throw new ArgumentOutOfRangeException("fieldName", String.Format("Field is not defined ({0}).", name));
			else
				return null;
		} // func GetFieldDescription

		[LuaMember]
		public PpsViewDescription GetViewDefinition(string name, bool throwException = true)
		{
			PpsViewDescription viewInfo;
			lock (viewController)
			{
				if (viewController.TryGetValue(name, out viewInfo))
					return viewInfo;
				else if (throwException)
					throw new ArgumentOutOfRangeException(); // todo:
				else
					return null;
			}
		} // func GetViewDefinition

		public IEnumerable<PpsViewDescription> GetViewDefinitions()
		{
			lock (viewController)
			{
				foreach (var c in viewController.Values)
					yield return c;
			}
		} // func GetViewDefinitions

		public PpsDataSelector GetViewDefinitionSelector(PpsSysDataSource dataSource, IPpsPrivateDataContext privateUserData)
			=> new PpsGenericSelector<PpsViewDescription>(dataSource, "sys.views", GetViewDefinitions());

		[LuaMember]
		public PpsDataSetServerDefinition GetDataSetDefinition(string name, bool throwException = true)
		{
			lock (datasetDefinitions)
			{
				PpsDataSetServerDefinition datasetDefinition;
				if (datasetDefinitions.TryGetValue(name, out datasetDefinition))
					return datasetDefinition;
				else if (throwException)
					throw new ArgumentOutOfRangeException($"Dataset definition '{name}' not found.");
				else
					return null;
			}
		} // func GetDataSetDefinition

		private void WriteDataRow(XmlWriter xml, IDataValues row, string[] columnNames)
		{
			xml.WriteStartElement("r");
			for (var i = 0; i < columnNames.Length; i++)
			{
				if (columnNames[i] != null)
				{
					var v = row[i];
					if (v != null)
						xml.WriteElementString(columnNames[i], v.ChangeType<string>());
				}
			}
			xml.WriteEndElement();
		} // proc WriteDataRow

		[DEConfigHttpAction("viewget", IsSafeCall = false)]
		private void HttpViewGetAction(IDEWebRequestScope r)
		{
			// v=views,...&f={filterList}&o={orderList}&s={startAt]&c={count}&a={attributeSelector}
			// ???views => view,view2(c1+c2),view3(c3+c4)
			// sort: +FIELD,-FIELD,:DEF
			// ???filter: :DEF-and-not-or-xor-contains-

			var startAt = r.GetProperty("s", 0);
			var count = r.GetProperty("c", Int32.MaxValue);
			
			var ctx = r.GetUser<IPpsPrivateDataContext>();
			
			var selector = ctx.CreateSelectorAsync(
				r.GetProperty<string>("v", null),
				r.GetProperty<string>("f", null),
				r.GetProperty<string>("o", null),
				true
			).AwaitTask();

			var attributeSelector = r.GetProperty("a", String.Empty);

			r.OutputHeaders["x-ppsn-source"] = selector.DataSource.Name;
			r.OutputHeaders["x-ppsn-native"] = selector.DataSource.Type;

			// emit the selector
			using (var tw = r.GetOutputTextWriter(MimeTypes.Text.Xml))
			using (var xml = XmlWriter.Create(tw, GetSettings(tw)))
			{
				xml.WriteStartDocument();
				xml.WriteStartElement("view");

				// execute the complete statemet
				using (var enumerator = selector.GetEnumerator(startAt, count))
				{
					bool emitCurrentRow = false;
					
					// extract the columns, optional before the fetch operation
					var columnDefinition = enumerator as IDataColumns;
					if (columnDefinition == null)
					{
						if (enumerator.MoveNext())
						{
							emitCurrentRow = true;
							columnDefinition = enumerator.Current;
						}
						else
							count = 0; // no rows
					}

					// emit column description
					string[] columnNames = null;
					if (columnDefinition != null)
					{
						columnNames = new string[columnDefinition.Columns.Count];

						xml.WriteStartElement("fields");
						for (var i = 0; i < columnNames.Length; i++)
						{
							var nativeColumnName = columnDefinition.Columns[i].Name;
							var fieldDefinition = selector.GetFieldDescription(nativeColumnName); // get the field description for the native column

							if (fieldDefinition == null)
							{
								columnNames[i] = null;
								continue;
							}
							else
							{
								columnNames[i] = nativeColumnName;

								var fieldDescription = fieldDefinition.GetColumnDescription<PpsFieldDescription>(); // get the global description of the field

								xml.WriteStartElement(nativeColumnName);

								if (fieldDescription == null)
								{
									xml.WriteAttributeString("type", LuaType.GetType(fieldDefinition.DataType).AliasOrFullName);
									xml.WriteAttributeString("field", fieldDefinition.Name);
								}
								else
								{
									xml.WriteAttributeString("type", LuaType.GetType(fieldDefinition.DataType).AliasOrFullName);

									foreach (var c in fieldDescription.GetAttributes(attributeSelector))
									{
										xml.WriteStartElement("attribute");
										
										xml.WriteAttributeString("name", c.Name);
										xml.WriteAttributeString("dataType", LuaType.GetType(c.Type).AliasOrFullName);
										if (c.Value != null)
											xml.WriteValue(Procs.ChangeType<string>(c.Value));

										xml.WriteEndElement();
									}
								}
								xml.WriteEndElement();
							}
						}
						xml.WriteEndElement();
					}

					// emit first row
					xml.WriteStartElement("rows");
					if (emitCurrentRow)
					{
						WriteDataRow(xml, enumerator.Current, columnNames);
						count--;
					}

					// emit all rows
					while (count > 0)
					{
						if (!enumerator.MoveNext())
							break;

						WriteDataRow(xml, enumerator.Current, columnNames);

						count--;
					}

					xml.WriteEndElement();
				}

				xml.WriteEndElement();
				xml.WriteEndDocument();
			}
		} // func HttpViewGetAction

		public PpsDataSource MainDataSource => mainDataSource;
	} // class PpsApplication
}
