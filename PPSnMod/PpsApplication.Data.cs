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
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Networking;
using TecWare.DE.Server;
using TecWare.DE.Server.Http;
using TecWare.DE.Stuff;
using TecWare.DE.Data;
using TecWare.PPSn.Server.Data;
using TecWare.PPSn.Server.Sql;
using static TecWare.PPSn.Server.PpsStuff;
using TecWare.PPSn.Data;

namespace TecWare.PPSn.Server
{
	#region -- class PpsFieldDescription ------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsFieldDescription : IPpsColumnDescription
	{
		private const string DisplayNameAttributeString = "displayName";
		private const string MaxLengthAttributeString = "maxLength";
		private const string DataTypeAttributeString = "dataType";

		#region -- class PpsFieldAttributes -----------------------------------------------

		private sealed class PpsFieldAttributes : PpsColumnDescriptionAttributes<PpsFieldDescription>
		{
			public PpsFieldAttributes(PpsFieldDescription owner)
				: base(owner)
			{
			} // ctor

			public override bool TryGetProperty(string name, out object value)
			{
				var p = Owner.GetProperty(name);
				if (p != null)
				{
					value = p.Value;
					return true;
				}
				return base.TryGetProperty(name, out value);
			} // func TryGetProperty

			public override IEnumerator<PropertyValue> GetEnumerator()
			{
				var displayNameEmitted = false;
				var dataTypeEmitted = false;

				foreach (var cur in Owner.xDefinition.Elements(xnFieldAttribute))
				{
					var p = Owner.GetProperty(cur);

					if (p == null)
						continue;

					if (String.Compare(p.Name, DisplayNameAttributeString, StringComparison.OrdinalIgnoreCase) == 0)
						displayNameEmitted = true;
					else if (String.Compare(p.Name, DataTypeAttributeString, StringComparison.OrdinalIgnoreCase) == 0)
						dataTypeEmitted = true;

					yield return p;
				}

				if (!displayNameEmitted)
					yield return new PropertyValue(DisplayNameAttributeString, typeof(string), Owner.DisplayName);

				if (!dataTypeEmitted)
					yield return new PropertyValue(DataTypeAttributeString, typeof(Type), Owner.DataType);

				using (var e = base.GetEnumerator())
				{
					while (e.MoveNext())
						yield return e.Current;
				}
			}
		} // class PpsFieldAttributes

		#endregion

		private readonly PpsDataSource source;
		private readonly string name;
		private readonly IPropertyEnumerableDictionary attributes;
		private readonly XElement xDefinition;
		private IPpsColumnDescription parentColumnDescription = null;

		private readonly Lazy<string> displayName;
		private readonly Lazy<int> maxLength;
		private readonly Lazy<Type> dataType;

		#region -- Ctor/Dtor --------------------------------------------------------------

		public PpsFieldDescription(PpsDataSource source, string name, XElement xDefinition)
		{
			this.source = source;
			this.name = name;
			this.attributes = new PpsFieldAttributes(this);
			this.xDefinition = xDefinition;

			displayName = new Lazy<string>(() => this.Attributes.GetProperty(DisplayNameAttributeString, name));
			maxLength = new Lazy<int>(() => this.Attributes.GetProperty(MaxLengthAttributeString, Int32.MaxValue));

			var xDataType = xDefinition.Attribute(DataTypeAttributeString);

			dataType = xDataType != null ? 
				new Lazy<Type>(() => LuaType.GetType(xDataType.Value, lateAllowed: false)):
				null;
		} // ctor

		internal void SetColumnDescription(IPpsColumnDescription columnDescription)
			=> this.parentColumnDescription = columnDescription;

		#endregion

		#region -- GetProperty ------------------------------------------------------------

		private PropertyValue GetProperty(XElement attribute)
		{
			if (attribute?.Value == null)
				return null;

			try
			{
				var name = attribute.GetAttribute<string>("name", null);
				if (String.IsNullOrEmpty(name))
					throw new ArgumentNullException("name");

				object value;
				var dataType = LuaType.GetType(attribute.GetAttribute("dataType", typeof(string))).Type;
				if (dataType == typeof(Type))
					value = (object)LuaType.GetType(attribute.Value, lateAllowed: false).Type;
				else
					value = Procs.ChangeType(attribute.Value, dataType);

				return new PropertyValue(name, dataType, value);
			}
			catch
			{
				return null;
			}
		} // func GetProperty

		private PropertyValue GetProperty(string propertyName)
		{
			var ret = GetProperty(xDefinition.Elements(xnFieldAttribute).FirstOrDefault(c => String.Compare(c.GetAttribute<string>("name", null), propertyName, StringComparison.OrdinalIgnoreCase) == 0));

			if (ret == null)
			{
				if (String.Compare(propertyName, DisplayNameAttributeString, StringComparison.OrdinalIgnoreCase) == 0)
					return new PropertyValue(DisplayNameAttributeString, typeof(string), xDefinition.GetAttribute<string>(DisplayNameAttributeString, this.name));
				else if (String.Compare(propertyName, DataTypeAttributeString, StringComparison.OrdinalIgnoreCase) == 0)
					return new PropertyValue(DataTypeAttributeString, typeof(Type), DataType);
			}

			return ret;
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

		public PpsDataSource DataSource => source;
		public string Name => name;
		public string DisplayName => displayName.Value;

		public int MaxLength => Attributes.GetPropertyLate("MaxLength", () => maxLength.Value);
		public Type DataType => dataType?.Value ?? (parentColumnDescription?.DataType ?? typeof(string));

		public IPropertyEnumerableDictionary Attributes => attributes;
		public IPpsColumnDescription Parent => parentColumnDescription;
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

	#region -- class PpsConstantDefintion -----------------------------------------------

	public abstract class PpsConstantDefintion
	{
		private readonly PpsDataSource dataSource;
		private readonly string name;

		public PpsConstantDefintion(PpsDataSource dataSource, string name)
		{
			this.dataSource = dataSource;
			this.name = name;
		} // ctor

		public abstract IEnumerable<IDataRow> GetValues(long lastSync);

		public PpsDataSource DataSource => dataSource;
		public string Name => name;

		public abstract IEnumerable<IDataColumn> Columns { get; }
	} // class PpsConstantDefintion

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

		#region -- class PpsConstantDefintionInit -----------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class PpsConstantDefintionInit
		{
			private readonly PpsDataSource source;
			private readonly string name;
			private readonly XElement xDefinition;

			public PpsConstantDefintionInit(PpsDataSource source, string name, XElement xDefinition)
			{
				this.source = source;
				this.name = name;
				this.xDefinition = xDefinition;
			} // ctor

			public async Task<PpsConstantDefintion> InitializeAsync()
				=> await source.CreateConstantDefinitionAsync(name, xDefinition);
		} // class PpsConstantDefintionInit

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
		private Dictionary<string, PpsConstantDefintion> constantDefinitions = new Dictionary<string, PpsConstantDefintion>(StringComparer.OrdinalIgnoreCase);
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
			var constantDeclarationList = new List<DependencyElement>();
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
					else if (xNode.Name == xnConstant)
						constantDeclarationList.Add(new DependencyElement(source, xNode));
					else if (xNode.Name == xnDataSet)
						datasetDeclarationList.Add(new DependencyElement(source, xNode));
					else
						throw new NotImplementedException();
				}
			}

			DependencyElement.RegisterList(fieldDeclarationList, RegisterField); // register all fields
			DependencyElement.RegisterList(viewDeclarationList, RegisterView); // register all views
			DependencyElement.RegisterList(constantDeclarationList, RegisterConstant); // register all constants
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
					fieldInfo.SetColumnDescription(source.GetColumnDescription(name));
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

		private void RegisterConstant(PpsDataSource source, string name, XElement x)
		{
			var cur = new PpsConstantDefintionInit(source, name, x);
			RegisterInitializationTask(10003, "Build constants", async () => RegisterConstant(await cur.InitializeAsync()));
		} // func RegisterConstant

		private void RegisterConstant(PpsConstantDefintion constant)
		{
			lock (constantDefinitions)
				constantDefinitions[constant.Name] = constant;
		} // proc RegisterConstant

		private void RegisterDataSet(PpsDataSource source, string name, XElement x)
		{
			var datasetDefinition = source == null ? new PpsDataSetServerDefinition(this, name, x, DateTime.Now) : source.CreateDocumentDescription(this, name, x, DateTime.Now);
			lock (datasetDefinitions)
				datasetDefinitions.Add(datasetDefinition.Name, datasetDefinition);
		} // void RegisterDataSet

		#endregion

		public PpsDataSource GetDataSource(string name, bool throwException)
		{
			using (this.EnterReadLock())
				return (PpsDataSource)this.UnsafeChildren.FirstOrDefault(c => c is PpsDataSource && String.Compare(c.Name, name, StringComparison.OrdinalIgnoreCase) == 0);
		} // func GetDataSource

		public PpsFieldDescription GetFieldDescription(string name, bool throwException = true)
		{
			PpsFieldDescription fieldInfo;
			if (fieldDescription.TryGetValue(name, out fieldInfo))
				return fieldInfo;
			else if (throwException)
				throw new ArgumentOutOfRangeException("fieldName", String.Format("Field is not defined ({0}).", name));
			else
				return null;
		} // func GetFieldDescription

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

		[LuaMember(nameof(GetDataSetDefinition))]
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

		#region -- GetConstantSelector ----------------------------------------------------

		#region -- class ConstantEnumerator -----------------------------------------------

		private sealed class ConstantEnumerator : IEnumerator<IDataRow>, IDataColumns
		{
			private readonly ConstantSelector selector;

			private int currentConstant = -1;
			private IEnumerator<IDataRow> currentEnumerator = null;

			public ConstantEnumerator(ConstantSelector selector)
			{
				this.selector = selector;
			} // ctor

			public void Dispose()
			{
				currentEnumerator?.Dispose();
			} // proc Dispose

			public void Reset()
			{
				currentConstant = -1;
				currentEnumerator?.Dispose();
				currentEnumerator = null;
			} // proc Reset

			public bool MoveNext()
			{
				if (currentConstant == -2)
					return false;
				else if (currentConstant == -1 || !currentEnumerator.MoveNext()) // get the next enumerator
				{
					currentConstant++;

					currentEnumerator?.Dispose();
					currentEnumerator = selector.GetNextConstant(currentConstant);
					if (currentEnumerator == null)
					{
						currentConstant = -2;
						return false;
					}
					else
						return MoveNext();
				}
				return true;
			} // func MoveNext

			public IDataRow Current => currentEnumerator?.Current;
			object IEnumerator.Current => currentEnumerator?.Current;

			public IReadOnlyList<IDataColumn> Columns => SqlConstantDefinition.DefaultDataColumns;
		} // class ConstantEnumerator 

		#endregion

		#region -- class ConstantSelector -------------------------------------------------

		private sealed class ConstantSelector : PpsDataSelector
		{
			private readonly PpsApplication application;
			private readonly PpsConstantDefintion[] constants;
			private readonly long syncFilter = 0;

			public ConstantSelector(PpsDataSource source, PpsApplication application) 
				: base(source)
			{
				this.application = application;

				lock (application.constantDefinitions)
					constants = application.constantDefinitions.Values.ToArray();
			} // ctor

			public ConstantSelector(PpsDataSource source, PpsApplication application, PpsConstantDefintion[] constants, long syncFilter)
				: base(source)
			{
				this.application = application;
				this.constants = constants;
				this.syncFilter = syncFilter;
			} // ctor

			internal IEnumerator<IDataRow> GetNextConstant(int index)
			{
				if (index >= 0 && index < constants.Length)
					return constants[index].GetValues(syncFilter).GetEnumerator();
				else
					return null;
			} // func GetNextConstant

			public override PpsDataSelector ApplyFilter(PpsDataFilterExpression expression, Func<string, string> lookupNative = null)
			{
				if (expression.Type == PpsDataFilterExpressionType.Compare)
				{
					var expr = expression as PpsDataFilterCompareExpression;
					if (expr.Operator == PpsDataFilterCompareOperator.Greater && expr.Operand == "sync" && expr.Value.Type == PpsDataFilterCompareValueType.Text)
						return new ConstantSelector(DataSource, application, constants, Int64.Parse(((PpsDataFilterCompareTextValue)expr.Value).Text));
				}

				throw new InvalidOperationException("Invalid filter");
			} // func ApplyFilter

			public override IEnumerator<IDataRow> GetEnumerator(int start, int count)
				=> new ConstantEnumerator(this);

			public override IPpsColumnDescription GetFieldDescription(string nativeColumnName)
			{
				var index = Array.FindIndex(SqlConstantDefinition.DefaultDataColumns, c => String.Compare(c.Name, nativeColumnName, StringComparison.OrdinalIgnoreCase) == 0);
				if (index == -1)
					throw new ArgumentException($"Invalid column name '{nativeColumnName}'");

				return SqlConstantDefinition.DefaultDataColumns[index].ToColumnDescription(application.GetFieldDescription("sys.constants." + nativeColumnName, false));
			} // func GetFiedDescription
		} // class ConstantSelector

		#endregion

		public PpsDataSelector GetConstantSelector(PpsSysDataSource dataSource, IPpsPrivateDataContext privateUserData)
			=> new ConstantSelector(dataSource, this);

		private XDocument GetConstantGlobalSchema()
		{
			lock (constantDefinitions)
			{
				return new XDocument(
					new XElement("constants",
						from constant in constantDefinitions.Values
						select new XElement("constant",
							new XAttribute("name", constant.Name),
							from col in constant.Columns
							select new XElement("column",
								new XAttribute("name", col.Name),
								new XAttribute("dataType", LuaType.GetType(col.DataType).AliasName)
							)
						)
					)
				);
			}
		} // func GetConstantGlobalSchema

		#endregion

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
		private void HttpViewGetAction(IDEContext r)
		{
			// v=views,...&f={filterList}&o={orderList}&s={startAt]&c={count}&a={attributeSelector}
			// ???views => view,view2(c1+c2),view3(c3+c4)
			// sort: +FIELD,-FIELD,:DEF
			// ???filter: :DEF-and-not-or-xor-contains-

			var startAt = r.GetProperty("s", 0);
			var count = r.GetProperty("c", Int32.MaxValue);
			
			var ctx = r.GetUser<IPpsPrivateDataContext>();
			
			var selector = ctx.CreateSelector(
				r.GetProperty<string>("v", null),
				r.GetProperty<string>("f", null),
				r.GetProperty<string>("o", null),
				true
			);

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

								var fieldDescription = String.IsNullOrEmpty(attributeSelector) ? null : this.GetFieldDescription(fieldDefinition.Name, false); // get the global description of the field

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
