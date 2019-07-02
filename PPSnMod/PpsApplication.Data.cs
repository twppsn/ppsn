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
using System.Globalization;
using System.IO;
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
using TecWare.PPSn.Data;
using TecWare.PPSn.Server.Data;
using TecWare.PPSn.Server.Sql;
using static TecWare.PPSn.Server.PpsStuff;

namespace TecWare.PPSn.Server
{
	#region -- class PpsFieldDescription ----------------------------------------------

	/// <summary>Description of a field. Extent a column, field or result value with 
	/// custom attributes.</summary>
	public sealed class PpsFieldDescription : IPpsColumnDescription
	{
		private const string displayNameAttributeString = "displayName";
		private const string maxLengthAttributeString = "maxLength";
		private const string dataTypeAttributeString = "dataType";
		private const string inheritedAttributeString = "inherited";

		#region -- class PpsFieldAttributesEnumerator ---------------------------------

		private sealed class PpsFieldAttributesEnumerator : IEnumerator<PropertyValue>
		{
			private readonly List<string> emittedProperties = new List<string>();
			private readonly PpsFieldDescription field;
			private int state = 0;

			private readonly Stack<IPpsColumnDescription> nativeColumnDescriptors = new Stack<IPpsColumnDescription>();
			private PpsFieldDescription currentField = null;
			private Queue<PpsFieldDescription> backFields = new Queue<PpsFieldDescription>();
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
				this.Current = v;
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
								Current = GetPropertyFromElement(currentFieldEnumerator.Current);
								return true;
							}
						}
						currentFieldEnumerator.Dispose();
						currentFieldEnumerator = null;

						foreach (var fd in currentField.inheritedDefinitions)
						{
							if (!backFields.Contains(fd))
								backFields.Enqueue(fd);
						}

						currentField = backFields.Count == 0 ? null : backFields.Dequeue();
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
								Current = currentAttributesEnumerator.Current;
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
				Current = null;
				currentField = field;
				currentFieldEnumerator?.Dispose();
				currentFieldEnumerator = null;
				currentAttributesEnumerator?.Dispose();
				currentAttributesEnumerator = null;
			} // prop Reset

			public PropertyValue Current { get; private set; }

			object IEnumerator.Current => Current;
		} // class PpsFieldAttributesEnumerator

		#endregion

		#region -- class PpsFieldAttributes -------------------------------------------

		private sealed class PpsFieldAttributes : IPropertyEnumerableDictionary
		{
			private readonly PpsFieldDescription field;

			public PpsFieldAttributes(PpsFieldDescription field)
			{
				this.field = field;
			} // ctor
			
			public bool TryGetProperty(string name, out object value)
			{
				var p = field.GetProperty(name, new List<PpsFieldDescription>());
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

		private readonly string[] inheritedFieldNames; // name for the base field
		private readonly XElement xDefinition; // definition in configuration

		private IPpsColumnDescription nativeColumnDescription = null; // assign field description of the datasource
		private PpsFieldDescription[] inheritedDefinitions = null; // inherited field description

		private readonly Lazy<string> displayName;
		private readonly Lazy<int> maxLength;
		private readonly Lazy<Type> dataType;

		private bool isInitialzed = false;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary></summary>
		/// <param name="source"></param>
		/// <param name="name"></param>
		/// <param name="xDefinition"></param>
		public PpsFieldDescription(PpsDataSource source, string name, XElement xDefinition)
		{
			this.DataSource = source;
			this.Name = name;
			this.Attributes = new PpsFieldAttributes(this);
			this.xDefinition = xDefinition;

			inheritedFieldNames = Procs.GetStrings(xDefinition.Attribute(inheritedAttributeString)?.Value, false);
						
			displayName = new Lazy<string>(() => Attributes.GetProperty(displayNameAttributeString, null));
			maxLength = new Lazy<int>(() => Attributes.GetProperty(maxLengthAttributeString, Int32.MaxValue));

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
			// find inheritied field descriptions
			inheritedDefinitions = inheritedFieldNames.Select(fn => DataSource.Application.GetFieldDescription(fn, true)).ToArray();
			
			// Resolve a native column description for the field.
			nativeColumnDescription = DataSource.GetColumnDescription(Name); // no exception

			isInitialzed = true;
		} // proc EndInit

		#endregion

		#region -- GetProperty --------------------------------------------------------

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
					value = LuaType.GetType(xAttribute.Value, lateAllowed: false).Type;
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

		private PropertyValue GetPropertyFromElement(string propertyName)
		{
			return GetPropertyFromElement(
				xDefinition.Elements(xnFieldAttribute)
					.FirstOrDefault(c => String.Compare(c.GetAttribute<string>("name", null), propertyName, StringComparison.OrdinalIgnoreCase) == 0)
			);
		} // func GetPropertyFromElement

		private PropertyValue GetInheritedProperty(string propertyName, ICollection<PpsFieldDescription> fetchedFields)
			=> inheritedDefinitions?.Select(d => d.GetProperty(propertyName, fetchedFields)).FirstOrDefault();

		private PropertyValue GetProperty(string propertyName, ICollection<PpsFieldDescription> fetchedFields)
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

			// recursion detection
			if (fetchedFields.Contains(this))
				return null;
			fetchedFields.Add(this);

			// find a attribute property
			if (TryGetAttributeBasedPropertyLocal(displayNameAttributeString, typeof(string), out var ret))
				return ret;
			if (TryGetAttributeBasedPropertyLocal(dataTypeAttributeString, typeof(Type), out ret))
				return ret;
			if (TryGetAttributeBasedPropertyLocal(maxLengthAttributeString, typeof(int), out ret))
				return ret;

			// search for a attribute field, with the specific name
			ret = GetPropertyFromElement(propertyName);
			if (ret != null)
				return ret;

			// ask native, inherited
			ret = GetInheritedProperty(propertyName, fetchedFields);
			if (ret != null)
				return ret;

			object v = null;
			if (nativeColumnDescription?.Attributes.TryGetProperty(propertyName, out v) ?? false)
				return new PropertyValue(propertyName, v);

			return null;
		} // func GetProperty

		#endregion

		#region -- IEnumerable --------------------------------------------------------

		/// <summary>Get attributes of this field.</summary>
		/// <param name="attributeSelector">String selector for attribute names.</param>
		/// <returns></returns>
		public IEnumerable<PropertyValue> GetAttributes(string attributeSelector)
		{
			return attributeSelector == null || attributeSelector.Length == 0 || attributeSelector == "*"
				? Attributes
				: Attributes.Where(c => c.Name.StartsWith(attributeSelector)); // todo: convert standard attribute
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
			else if (inheritedDefinitions != null)
				return inheritedDefinitions.Select(d => d.GetColumnDescription<T>()).FirstOrDefault();
			else
				return default(T);
		} // func GetColumnDescription

		/// <summary>Defined in.</summary>
		public PpsDataSource DataSource { get; }

		/// <summary>Name of the field.</summary>
		public string Name { get; }

		/// <summary>Displayname of the field.</summary>
		public string DisplayName => displayName.Value;

		/// <summary>Max length of the field.</summary>
		public int MaxLength => Attributes.GetPropertyLate("MaxLength", () => maxLength.Value);
		/// <summary>DataType of the field.</summary>
		public Type DataType => dataType?.Value ?? (nativeColumnDescription?.DataType ?? typeof(string));

		/// <summary>Field attributes.</summary>
		public IPropertyEnumerableDictionary Attributes { get; }
	} // class PpsFieldDescription

	#endregion

	#region -- class PpsViewParameterDefinition ---------------------------------------

	/// <summary>Defines a parameter for a view, order or filter expression.</summary>
	public sealed class PpsViewParameterDefinition
	{
		private readonly string name;
		private readonly string displayName;
		private readonly string parameter;

		/// <summary></summary>
		/// <param name="name"></param>
		/// <param name="displayName"></param>
		/// <param name="parameter"></param>
		public PpsViewParameterDefinition(string name, string displayName, string parameter)
		{
			if (String.IsNullOrEmpty(name))
				throw new ArgumentNullException("name");
			
			this.name = name;
			this.displayName = displayName;
			this.parameter = parameter;
		} // ctor

		/// <summary></summary>
		/// <param name="x"></param>
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

		/// <summary>Internal name of the parameter</summary>
		public string Name => name;
		/// <summary>Display name of the parameter for the user.</summary>
		public string DisplayName => displayName ?? name;
		/// <summary>Parameter content.</summary>
		public string Parameter => parameter;

		/// <summary>Is this parameter visible for the user.</summary>
		public bool IsVisible => displayName != null;

		/// <summary>Empty parameter array.</summary>
		public static PpsViewParameterDefinition[] EmptyArray { get; } = Array.Empty<PpsViewParameterDefinition>();
	} // class PpsViewParameterDefinition

	#endregion

	#region -- class PpsViewJoinDefinition --------------------------------------------

	/// <summary>Define join</summary>
	public sealed class PpsViewJoinDefinition
	{
		/// <summary></summary>
		/// <param name="viewName"></param>
		/// <param name="aliasName"></param>
		/// <param name="type"></param>
		/// <param name="statement"></param>
		public PpsViewJoinDefinition(string viewName, string aliasName, PpsDataJoinType type, PpsDataJoinStatement[] statement)
		{
			ViewName = viewName;
			AliasName = aliasName;
			Statement = statement;
			Type = type;
		} // ctor

		/// <summary>View to join to</summary>
		public string ViewName { get; }
		/// <summary>Alias to intendify joins to the same view</summary>
		public string AliasName { get;  }
		/// <summary>Join type</summary>
		public PpsDataJoinType Type { get; }
		/// <summary>On statement for the view.</summary>
		public PpsDataJoinStatement[] Statement { get;  }

		/// <summary>Empty parameter array.</summary>
		public static PpsViewJoinDefinition[] EmptyArray { get; } = Array.Empty<PpsViewJoinDefinition>();
	} // class PpsViewJoinDefinition
	
	#endregion

	#region -- class PpsViewDefinition ------------------------------------------------

	/// <summary>View description to manage request to the system.</summary>
	public sealed class PpsViewDescription
	{
		private readonly IPpsSelectorToken selectorToken;
		private readonly string displayName;

		private readonly PpsViewJoinDefinition[] joins;
		private readonly PpsViewParameterDefinition[] filter;
		private readonly PpsViewParameterDefinition[] order;

		private readonly IPropertyReadOnlyDictionary attributes;

		/// <summary></summary>
		/// <param name="selectorToken"></param>
		/// <param name="displayName"></param>
		/// <param name="joins"></param>
		/// <param name="filters"></param>
		/// <param name="orders"></param>
		/// <param name="attributes"></param>
		public PpsViewDescription(IPpsSelectorToken selectorToken, string displayName, PpsViewJoinDefinition[] joins, PpsViewParameterDefinition[] filters, PpsViewParameterDefinition[] orders, IPropertyReadOnlyDictionary attributes)
		{
			this.selectorToken = selectorToken;
			this.displayName = displayName;

			this.joins = joins ?? PpsViewJoinDefinition.EmptyArray;
			this.filter = filters ?? PpsViewParameterDefinition.EmptyArray;
			this.order = orders ?? PpsViewParameterDefinition.EmptyArray;

			this.attributes = attributes ?? new PropertyDictionary();
		} // ctor

		/// <summary>Lookup a order parameter to retrieve a native order expression.</summary>
		/// <param name="orderName"></param>
		/// <returns></returns>
		public string LookupOrder(string orderName)
			=> order.FirstOrDefault(c => String.Compare(orderName, c.Name, StringComparison.OrdinalIgnoreCase) == 0)?.Parameter;

		/// <summary>Lookup a filter parameter to retrieve a native filter expression.</summary>
		/// <param name="filterName"></param>
		/// <returns></returns>
		public string LookupFilter(string filterName)
			=> filter.FirstOrDefault(c => String.Compare(filterName, c.Name, StringComparison.OrdinalIgnoreCase) == 0)?.Parameter;

		/// <summary>Lookup a join by view name.</summary>
		/// <param name="viewName"></param>
		/// <returns></returns>
		public PpsViewJoinDefinition LookupJoin(string viewName)
			=> joins.FirstOrDefault(c => String.Compare(viewName, c.ViewName, StringComparison.OrdinalIgnoreCase) == 0);

		/// <summary>Name of the view.</summary>
		public string Name => selectorToken.Name;
		/// <summary>Name of the view for the user.</summary>
		public string DisplayName => displayName ?? selectorToken.Name;
		/// <summary>Access token to the view.</summary>
		public string SecurityToken => null;

		/// <summary>Predefined join expressions.</summary>
		public PpsViewJoinDefinition[] Joins => joins;
		/// <summary>Predefined filter expressions.</summary>
		public PpsViewParameterDefinition[] Filter => filter;
		/// <summary>Predefined order expressions,</summary>
		public PpsViewParameterDefinition[] Order => order;

		/// <summary>Extented attributes of the view.</summary>
		public IPropertyReadOnlyDictionary Attributes => attributes;

		/// <summary>Selector token to execute the view.</summary>
		public IPpsSelectorToken SelectorToken => selectorToken;

		/// <summary>Is this view visible for the user.</summary>
		public bool IsVisible => displayName != null;
	} // class PpsViewDefinition

	#endregion

	/// <summary></summary>
	public partial class PpsApplication
	{
		#region -- class PpsViewDefinitionInit ----------------------------------------

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
					xDefinition.Elements(xnJoin).Select(CreateJoinDefinition).ToArray(),
					xDefinition.Elements(xnFilter).Select(x => new PpsViewParameterDefinition(x)).ToArray(),
					xDefinition.Elements(xnOrder).Select(x => new PpsViewParameterDefinition(x)).ToArray(),
					xDefinition.Elements(xnAttribute).ToPropertyDictionary(
						new KeyValuePair<string, Type>("description", typeof(string)),
						new KeyValuePair<string, Type>("bi.visible", typeof(bool))
					)
				);

				return view;
			} // proc InitializeAsync

			private static PpsViewJoinDefinition CreateJoinDefinition(XElement xJoin)
			{
				var viewName = xJoin.GetAttribute("view", null);
				if (String.IsNullOrEmpty(viewName))
					throw new DEConfigurationException(xJoin, "@view is missing.");
				var statement = ParseOnStatement(xJoin);
				var aliasName = xJoin.GetAttribute("alias", null); // optional
				var type= xJoin.GetAttribute("type", PpsDataJoinType.Inner);

				return new PpsViewJoinDefinition(viewName, aliasName, type, statement);
			} // func CreateJoinDefinition

			private static PpsDataJoinStatement[] ParseOnStatement(XElement xJoin)
			{
				PpsDataJoinStatement[] statement;
				try
				{
					statement = PpsDataJoinStatement.Parse(xJoin.GetAttribute("on", null)).ToArray();
					if (statement == null && statement.Length == 0)
						throw new ArgumentNullException();
				}
				catch (Exception e)
				{
					throw new DEConfigurationException(xJoin, "@on could not parsed.", e);
				}

				return statement;
			} // func ParseOnStatement
		} // class PpsViewDefinitionInit

		#endregion

		#region -- class DependencyElement --------------------------------------------

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

		#region -- class ViewWriter ---------------------------------------------------

		private abstract class ViewWriter : IDisposable
		{
			private bool[] rowAllowed = null;

			public void Dispose()
				=> Dispose(true);

			protected virtual void Dispose(bool disposing)
			{
			} // proc Dispose

			protected bool IsColumnAllowedFast(int columnIndex)
				=> rowAllowed[columnIndex];

			protected bool IsColumnAllowed(int columnIndex)
				=> rowAllowed == null || columnIndex < 0 || columnIndex >= rowAllowed.Length ? false : rowAllowed[columnIndex];

			public virtual void Begin(IPpsPrivateDataContext ctx, PpsDataSelector selector, IDataColumns columns, string attributeSelector)
			{
				// create column export rights
				rowAllowed = new bool[columns.Columns.Count];
				for (var i = 0; i < rowAllowed.Length; i++)
				{
					var col = columns.Columns[i];
					var fieldDescription = selector.GetFieldDescription(col.Name);

					rowAllowed[i] = fieldDescription == null
						|| !fieldDescription.Attributes.TryGetProperty<string>("securityToken", out var securityToken)
						|| ctx.TryDemandToken(securityToken);
				}

				// write output
				BeginCore(selector, columns, attributeSelector);
			} // proc Begin

			protected virtual void BeginCore(PpsDataSelector selector, IDataColumns columns, string attributeSelector) { }

			public void WriteRow(IDataRow current)
				=> WriteRowCore(current);

			protected abstract void WriteRowCore(IDataRow current);

			public void End()
				=> EndCore();

			protected virtual void EndCore() { }
		} // class ViewWriter

		#endregion

		#region -- class ViewXmlWriter ------------------------------------------------

		private sealed class ViewXmlWriter : ViewWriter
		{
			private readonly TextWriter tw;
			private readonly XmlWriter xml;

			private bool firstRow = true;
			private string[] columnNames = null;
			private Type[] columnTypes = null;

			public ViewXmlWriter(IDEWebRequestScope r)
			{
				tw = r.GetOutputTextWriter(MimeTypes.Text.Xml);
				xml = XmlWriter.Create(tw, GetSettings(tw));
			} // ctor

			protected override void Dispose(bool disposing)
			{
				try
				{
					xml.Dispose();
					tw.Dispose();
				}
				finally
				{
					base.Dispose(disposing);
				}
			} // proc Dispose

			private static void WriteAttributeForViewGet(XmlWriter xml, PropertyValue attr)
			{
				xml.WriteStartElement("attribute");

				xml.WriteAttributeString("name", attr.Name);
				xml.WriteAttributeString("dataType", LuaType.GetType(attr.Type).AliasOrFullName);

				var value = attr.Value;
				if (value != null)
					xml.WriteValue(Procs.RemoveInvalidXmlChars(Procs.ChangeType<string>(value)));

				xml.WriteEndElement();
			} // proc WriteAttributeForViewGet

			private void WriteColumns(PpsDataSelector selector, IDataColumns columns, string attributeSelector)
			{
				// emit column description
				columnNames = new string[columns.Columns.Count];
				columnTypes = new Type[columnNames.Length];

				xml.WriteStartElement("fields");
				for (var i = 0; i < columnNames.Length; i++)
				{
					var nativeColumnName = columns.Columns[i].Name;
					var fieldDefinition = selector.GetFieldDescription(nativeColumnName); // get the field description for the native column

					if (fieldDefinition == null)
					{
						columnNames[i] = null;
						columnTypes[i] = null;
						continue;
					}
					else
					{
						columnNames[i] = nativeColumnName;

						var fieldDescription = fieldDefinition.GetColumnDescription<PpsFieldDescription>(); // get the global description of the field
						var fieldType = fieldDefinition.DataType;
						var isNullable = false;

						xml.WriteStartElement(nativeColumnName);

						if (fieldDescription == null)
						{
							xml.WriteAttributeString("type", LuaType.GetType(fieldType).AliasOrFullName);
							xml.WriteAttributeString("field", fieldDefinition.Name);

							WriteAttributeForViewGet(xml, new PropertyValue("Nullable", typeof(bool), true));
							isNullable = true;
						}
						else
						{
							xml.WriteAttributeString("type", LuaType.GetType(fieldType).AliasOrFullName);

							foreach (var c in fieldDescription.GetAttributes(attributeSelector))
							{
								if (String.Compare(c.Name, "Nullable", true) == 0)
								{
									isNullable = fieldType == typeof(string) // always nullable
										|| c.Value.ChangeType<bool>();
									WriteAttributeForViewGet(xml, new PropertyValue(c.Name, typeof(bool), isNullable));
								}
								else
									WriteAttributeForViewGet(xml, c);
							}
						}
						xml.WriteEndElement();

						columnTypes[i] = isNullable && fieldType.IsValueType
							? typeof(Nullable<>).MakeGenericType(fieldType)
							: fieldType;
					}
				}
				xml.WriteEndElement();
			} // proc WriteColumns

			protected override void BeginCore(PpsDataSelector selector, IDataColumns columns, string attributeSelector)
			{
				xml.WriteStartDocument();
				xml.WriteStartElement("view");

				if (columns != null)
					WriteColumns(selector, columns, attributeSelector);
				else
					columnNames = null;
			} // proc Begin

			private void WriteRowValue(string columnName, object v)
			{
				if (v != null)
					xml.WriteElementString(columnName, Procs.RemoveInvalidXmlChars(v.ChangeType<string>()));
			} // proc WriteRowValue

			protected override void WriteRowCore(IDataRow row)
			{
				if (firstRow)
				{
					firstRow = false;
					xml.WriteStartElement("rows");
				}

				xml.WriteStartElement("r");
				if (columnNames == null)
				{
					for (var i = 0; i < row.Columns.Count; i++)
					{
						if (IsColumnAllowedFast(i))
							WriteRowValue(row.Columns[i].Name, row[i]);
						else if (columnTypes[i].IsValueType)
							WriteRowValue(columnNames[i], Activator.CreateInstance(columnTypes[i]));
					}
				}
				else
				{
					for (var i = 0; i < columnNames.Length; i++)
					{
						if (columnNames[i] != null)
						{
							if (IsColumnAllowedFast(i))
								WriteRowValue(columnNames[i], row[i]);
							else if (columnTypes[i].IsValueType)
								WriteRowValue(columnNames[i], Activator.CreateInstance(columnTypes[i]));
						}
					}
				}
				xml.WriteEndElement();
			} // proc WriteRow

			protected override void EndCore()
			{
				if (firstRow) // write empty element
				{
					xml.WriteStartElement("rows");
					xml.WriteEndElement();
				}
				else
					xml.WriteEndElement();

				xml.WriteEndElement();
				xml.WriteEndDocument();
			} // proc End
		} // class ViewXmlWriter

		#endregion

		#region -- class ViewCsvWriter ------------------------------------------------

		private sealed class ViewCsvWriter : ViewWriter
		{
			private readonly TextCsvWriter writer;
			private bool[] isText = null;
			private string[] rowBuffer = null;

			public ViewCsvWriter(IDEWebRequestScope r)
			{
				writer = new TextCsvWriter(
					r.GetOutputTextWriter(MimeTypes.Text.Plain),
					new TextCsvSettings()
				);
			} // ctor

			protected override void Dispose(bool disposing)
			{
				try
				{
					writer.Dispose();
				}
				finally
				{
					base.Dispose(disposing);
				}
			} // proc Dispose

			protected override void BeginCore(PpsDataSelector selector, IDataColumns columns, string attributeSelector)
			{
				rowBuffer = new string[columns.Columns.Count];
				isText = new bool[rowBuffer.Length];
				var isHeaderText = new bool[rowBuffer.Length];
				for (var i = 0; i < columns.Columns.Count; i++)
				{
					rowBuffer[i] = columns.Columns[i].Name;
					isText[i] = columns.Columns[i].DataType == typeof(string);
					isHeaderText[i] = true;
				}

				writer.WriteRow(rowBuffer, isHeaderText);
			} // proc Begin

			protected override void WriteRowCore(IDataRow row)
			{
				for (var i = 0; i < rowBuffer.Length; i++)
					rowBuffer[i] = IsColumnAllowedFast(i) ? Convert.ToString(row[i], CultureInfo.InvariantCulture) : String.Empty;

				writer.WriteRow(rowBuffer, isText);
			} // proc WriteRow

			protected override void EndCore() { }
		} // class ViewXmlWriter

		#endregion

		private PpsSqlExDataSource mainDataSource;
		private long viewsVersion;
		private readonly Dictionary<string, PpsFieldDescription> fieldDescription = new Dictionary<string, PpsFieldDescription>(StringComparer.OrdinalIgnoreCase);
		private readonly Dictionary<string, PpsViewDescription> viewController = new Dictionary<string, PpsViewDescription>(StringComparer.OrdinalIgnoreCase);
		private readonly Dictionary<string, PpsDataSetServerDefinition> datasetDefinitions = new Dictionary<string, PpsDataSetServerDefinition>(StringComparer.OrdinalIgnoreCase);

		#region -- Init/Done ----------------------------------------------------------
		
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

			viewsVersion = DateTime.Now.ToFileTimeUtc();
		} // proc BeginEndConfigurationData

		#endregion

		#region -- Register -----------------------------------------------------------

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

		/// <summary>Register a view to the view list.</summary>
		/// <param name="selectorToken"></param>
		/// <param name="displayName"></param>
		public void RegisterView(IPpsSelectorToken selectorToken, string displayName = null)
			=> RegisterView(new PpsViewDescription(selectorToken, displayName, null, null, null, null));

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
			var datasetDefinition = (source ?? mainDataSource).CreateDataSetDefinition(name, x, Server.Configuration.ConfigurationStamp);
			lock (datasetDefinitions)
				datasetDefinitions.Add(datasetDefinition.Name, datasetDefinition);
		} // void RegisterDataSet

		#endregion
		
		/// <summary>Find a data source by name.</summary>
		/// <param name="name"></param>
		/// <param name="throwException"></param>
		/// <returns></returns>
		[LuaMember]
		public PpsDataSource GetDataSource(string name, bool throwException = true)
		{
			using (EnterReadLock())
				return (PpsDataSource)UnsafeChildren.FirstOrDefault(c => c is PpsDataSource && String.Compare(c.Name, name, StringComparison.OrdinalIgnoreCase) == 0)
					?? (throwException ? throw new ArgumentOutOfRangeException(nameof(name), name, $"Data source is not defined ('{name}').") : (PpsDataSource)null);
		} // func GetDataSource

		/// <summary>Find a field description.</summary>
		/// <param name="name"></param>
		/// <param name="throwException"></param>
		/// <returns></returns>
		[LuaMember]
		public PpsFieldDescription GetFieldDescription(string name, bool throwException = true)
		{
			if (fieldDescription.TryGetValue(name, out var fieldInfo))
				return fieldInfo;
			else if (throwException)
				throw new ArgumentOutOfRangeException(nameof(name), name, $"Field is not defined ({name}).");
			else
				return null;
		} // func GetFieldDescription

		/// <summary>Get a specific view definition.</summary>
		/// <param name="name"></param>
		/// <param name="throwException"></param>
		/// <returns></returns>
		[LuaMember]
		public PpsViewDescription GetViewDefinition(string name, bool throwException = true)
		{
			lock (viewController)
			{
				if (viewController.TryGetValue(name, out var viewInfo))
					return viewInfo;
				else if (throwException)
					throw new ArgumentOutOfRangeException(nameof(name), $"View definition is not defined ('{name}').");
				else
					return null;
			}
		} // func GetViewDefinition

		/// <summary>Enumerate all registered view definitions.</summary>
		/// <returns></returns>
		public IEnumerable<PpsViewDescription> GetViewDefinitions()
		{
			lock (viewController)
			{
				foreach (var c in viewController.Values)
					yield return c;
			}
		} // func GetViewDefinitions

		/// <summary>Return a selector for all views.</summary>
		/// <param name="dataSource"></param>
		/// <returns></returns>
		public PpsDataSelector GetViewDefinitionSelector(PpsSysDataSource dataSource)
			=> new PpsGenericSelector<PpsViewDescription>(dataSource.SystemConnection, "sys.views", viewsVersion, GetViewDefinitions());

		/// <summary></summary>
		/// <param name="name"></param>
		/// <param name="throwException"></param>
		/// <returns></returns>
		[LuaMember]
		public PpsDataSetServerDefinition GetDataSetDefinition(string name, bool throwException = true)
		{
			lock (datasetDefinitions)
			{
				if (datasetDefinitions.TryGetValue(name, out var datasetDefinition))
					return datasetDefinition;
				else if (throwException)
					throw new ArgumentOutOfRangeException($"Dataset definition '{name}' not found.");
				else
					return null;
			}
		} // func GetDataSetDefinition

		private static ViewWriter ViewGetCreateWriter(IDEWebRequestScope r)
		{
			if (r.AcceptType(MimeTypes.Text.Plain))
				return new ViewCsvWriter(r);
			else
				return new ViewXmlWriter(r);
		} // func ViewGetCreateWriter

		[DEConfigHttpAction("viewget", IsSafeCall = false)]
		private void HttpViewGetAction(IDEWebRequestScope r)
		{
			// v=views,...&f={filterList}&o={orderList}&s={startAt]&c={count}&a={attributeSelector}
			var startAt = r.GetProperty("s", 0);
			var count = r.GetProperty("c", Int32.MaxValue);

			var ctx = r.GetUser<IPpsPrivateDataContext>();

			var selector = ctx.CreateSelectorAsync(
				r.GetProperty<string>("v", null),
				r.GetProperty<string>("r", null),
				r.GetProperty<string>("f", null),
				r.GetProperty<string>("o", null),
				true
			).AwaitTask();

			var attributeSelector = r.GetProperty("a", String.Empty);

			// emit the selector
			using (var viewWriter = ViewGetCreateWriter(r))
			{
				// execute the complete statemet
				using (var enumerator = selector.GetEnumerator(startAt, count))
				{
					r.OutputHeaders["x-ppsn-source"] = selector.DataSource.Name;
					r.OutputHeaders["x-ppsn-native"] = selector.DataSource.Type;

					var emitCurrentRow = false;

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

					// write header
					viewWriter.Begin(ctx, selector, columnDefinition, attributeSelector);

					// emit first row
					if (emitCurrentRow)
					{
						viewWriter.WriteRow(enumerator.Current);
						count--;
					}

					// emit all rows
					while (count > 0)
					{
						if (!enumerator.MoveNext())
							break;

						viewWriter.WriteRow(enumerator.Current);
						count--;
					}

					// write end
					viewWriter.End();
				}
			}
		} // func HttpViewGetAction

		/// <summary>Main data source, MS Sql Server</summary>
		public PpsDataSource MainDataSource => mainDataSource;
		/// <summary></summary>
		public long CurrentViewsVersion => viewsVersion;
	} // class PpsApplication
}
