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
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using System.Xaml;
using Neo.IronLua;
using TecWare.DE.Data;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;
using TecWare.PPSn.UI;

namespace TecWare.PPSn.Controls
{
	#region -- class PpsDataFieldInfo -------------------------------------------------

	/// <summary>Result of the resolver.</summary>
	public class PpsDataFieldInfo : IPropertyReadOnlyDictionary
	{
		private readonly IServiceProvider sp;
		private readonly IPropertyReadOnlyDictionary properties;
		private readonly IDataColumn dataColumn;
		
		/// <summary></summary>
		/// <param name="sp"></param>
		/// <param name="dataColumn"></param>
		/// <param name="properties"></param>
		/// <param name="bindingPath"></param>
		public PpsDataFieldInfo(IServiceProvider sp, IDataColumn dataColumn, IPropertyReadOnlyDictionary properties, string bindingPath)
		{
			this.sp = sp;
			this.dataColumn = dataColumn ?? throw new ArgumentNullException(nameof(dataColumn));
			this.properties = properties ?? PropertyDictionary.EmptyReadOnly;
			this.BindingPath = bindingPath ?? throw new ArgumentNullException(nameof(bindingPath));
		} // ctor

		/// <summary></summary>
		/// <param name="sp"></param>
		/// <param name="name"></param>
		/// <param name="dataType"></param>
		/// <param name="bindingPath"></param>
		/// <param name="properties"></param>
		public PpsDataFieldInfo(IServiceProvider sp, string name, Type dataType, string bindingPath, IPropertyReadOnlyDictionary properties)
		{
			this.sp = sp;
			this.dataColumn = new SimpleDataColumn(name, dataType);
			this.properties = properties ?? PropertyDictionary.EmptyReadOnly;
			this.BindingPath = bindingPath ?? throw new ArgumentNullException(nameof(bindingPath));
		} // ctor

		/// <summary></summary>
		/// <param name="name"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public bool TryGetProperty(string name, out object value)
			=> properties.TryGetProperty(name, out value)
				|| dataColumn.Attributes.TryGetProperty(name, out value)
				|| TryGetDefaultProperty(name, out value);

		private bool TryGetDefaultProperty(string name, out object value)
		{
			switch(name)
			{
				case nameof(BindingPath):
					value = BindingPath;
					return true;
				default:
					value = null;
					return false;
			}
		} // func TryGetDefaultProperty

		/// <summary></summary>
		public string Name => dataColumn.Name;
		/// <summary></summary>
		public Type DataType => dataColumn.DataType;
		/// <summary></summary>
		public PpsDataColumnDefinition ColumnDefinition => dataColumn as PpsDataColumnDefinition;

		/// <summary></summary>
		public IServiceProvider Context => sp;

		/// <summary>Bindpath to use this field.</summary>
		public string BindingPath { get; private set; }

		/// <summary></summary>
		/// <param name="serviceProvider"></param>
		/// <param name="fieldName"></param>
		/// <param name="properties"></param>
		/// <param name="throwException"></param>
		/// <returns></returns>
		public static PpsDataFieldInfo GetDataFieldInfo(IServiceProvider serviceProvider, string fieldName, IPropertyReadOnlyDictionary properties, bool throwException = true)
		{
			var field = serviceProvider.GetService<IPpsDataFieldResolver>(true)?.ResolveColumn(serviceProvider, fieldName, properties ?? PropertyDictionary.EmptyReadOnly);
			if (throwException && field == null )
				throw new ArgumentNullException(nameof(field), $"Could not resolve field '{fieldName}'.");
			return field;
		} // func GetDataFieldInfo
	} // struct PpsDataFieldInfo

	#endregion

	#region -- interface IPpsDataFieldResolver ----------------------------------------

	/// <summary>Marks a datatable scope assignment</summary>
	public interface IPpsDataFieldResolver
	{
		/// <summary></summary>
		/// <param name="sp"></param>
		/// <param name="fieldExpression"></param>
		/// <param name="properties"></param>
		/// <returns></returns>
		PpsDataFieldInfo ResolveColumn(IServiceProvider sp, string fieldExpression, IPropertyReadOnlyDictionary properties);
	} // interface IPpsDataFieldResolver

	#endregion

	#region -- interface IPpsDataFieldFactory -----------------------------------------

	/// <summary></summary>
	public interface IPpsDataFieldFactory
	{
		/// <summary></summary>
		/// <param name="context"></param>
		/// <param name="fieldName"></param>
		/// <param name="properties"></param>
		/// <param name="localProperties"></param>
		/// <returns></returns>
		System.Xaml.XamlReader CreateField(IServiceProvider context, string fieldName, IPropertyReadOnlyDictionary properties, LocalValueEnumerator localProperties);
	} // interface IPpsDataFieldFactory

	#endregion

	#region -- class PpsDataSetResolver -----------------------------------------------

	/// <summary>Parser service to resolve a dataset.</summary>
	public class PpsDataSetResolver : PpsParserService, IPpsDataFieldResolver
	{
		/// <summary></summary>
		public Func<object> GetDataSet { get; set; }

		private PpsDataSetDefinition dataset = null;

		/// <summary></summary>
		public PpsDataSetResolver()
		{
		}

		/// <summary></summary>
		/// <param name="dataset"></param>
		public PpsDataSetResolver(PpsDataSet dataset)
			: this(dataset?.DataSetDefinition)
		{
		} // ctor

		/// <summary></summary>
		/// <param name="dataset"></param>
		public PpsDataSetResolver(PpsDataSetDefinition dataset)
		{
			this.dataset = dataset ?? throw new ArgumentNullException(nameof(dataset));
		} // ctor

		/// <summary></summary>
		protected override void OnInitialized()
		{
			base.OnInitialized();

			if (dataset == null)
			{
				switch (GetDataSet?.Invoke())
				{
					case PpsDataSet ds:
						dataset = ds.DataSetDefinition;
						break;
					case PpsDataSetDefinition ds:
						dataset = ds;
						break;
					default:
						throw new ArgumentException("No dataset found.");

				}
			}
		} // proc OnInitialized

		PpsDataFieldInfo IPpsDataFieldResolver.ResolveColumn(IServiceProvider serviceProvider, string fieldExpression, IPropertyReadOnlyDictionary properties)
			=> ReolveDataFieldInfo(serviceProvider, fieldExpression, properties);

		internal PpsDataFieldInfo ReolveDataFieldInfo(IServiceProvider serviceProvider, string fieldExpression, IPropertyReadOnlyDictionary properties)
		{
			return null;
		} // func ReolveDataFieldInfo

		/// <summary></summary>
		/// <param name="serviceType"></param>
		/// <returns></returns>
		public override object GetService(Type serviceType)
		{
			if (serviceType == typeof(PpsDataSetDefinition))
				return dataset;
			else if (serviceType == typeof(IPpsDataFieldResolver)
				|| serviceType == typeof(PpsDataSetResolver))
				return this;
			else
				return null;
		} // func GetService

		/// <summary>Base Binding-Path</summary>
		public string BindingPath { get; set; } = String.Empty;
		/// <summary></summary>
		public PpsDataSetDefinition DataSetDefinition => dataset;

		internal static PpsDataFieldInfo GetDataFieldInfo(IServiceProvider serviceProvider, PpsDataColumnDefinition column, IPropertyReadOnlyDictionary properties, string baseBindingPath)
		{
			if (!properties.TryGetProperty<string>(nameof(PpsDataField.BindingPath), out var localBindingPath))
				localBindingPath = column.Name;

			return new PpsDataFieldInfo(serviceProvider, column, properties, baseBindingPath ?? localBindingPath);
		} // func GetDataFieldInfo
	} // class PpsDataSetResolver

	#endregion

	#region -- class PpsDataTableResolver ---------------------------------------------

	/// <summary></summary>
	public class PpsDataTableResolver : PpsParserService, IPpsDataFieldResolver
	{
		private string tableName = null;
		private string bindingPath = null;

		private PpsDataSetResolver datasetResolver = null;
		private PpsDataTableDefinition table = null;

		/// <summary></summary>
		public PpsDataTableResolver() { }

		/// <summary></summary>
		public PpsDataTableResolver(PpsDataTable table)
		{
			this.tableName = table.TableName;
			this.table = table.TableDefinition;
		} // ctor

		PpsDataFieldInfo IPpsDataFieldResolver.ResolveColumn(IServiceProvider serviceProvider, string fieldExpression, IPropertyReadOnlyDictionary properties)
		{
			CheckInitialized();

			// resolve table
			if (table == null && tableName != null)
			{
				var pos = tableName.IndexOf('.');
				if (pos == -1)
				{
					datasetResolver = (PpsDataSetResolver)GetParentService(typeof(PpsDataSetResolver)) ?? throw new ArgumentException("DataSet resolve is missing.");
					table = datasetResolver.DataSetDefinition.FindTable(tableName);
				}
				else
				{
					var info = serviceProvider.GetService<PpsEnvironment>(true).ObjectInfos[tableName.Substring(0, pos), true];
					table = info.GetDocumentDefinitionAsync().AwaitTask().FindTable(tableName.Substring(pos + 1));
				}
			}

			var idx = table.FindColumnIndex(fieldExpression);
			return idx != -1
				? PpsDataSetResolver.GetDataFieldInfo(serviceProvider, table.Columns[idx], properties, bindingPath)
				: datasetResolver.ReolveDataFieldInfo(serviceProvider, fieldExpression, properties);
		} // func ResolveColumn

		/// <summary></summary>
		/// <param name="serviceType"></param>
		/// <returns></returns>
		public override object GetService(Type serviceType)
			=> serviceType == typeof(IPpsDataFieldResolver) ? this : null;

		/// <summary></summary>
		public string Name { get => tableName; set { tableName = value; table = null; } }
		/// <summary></summary>
		public string BindingPath { get => bindingPath; set => bindingPath = value; }
	} // class PpsDataTableResolver

	#endregion

	#region -- class PpsDataFieldBinding ----------------------------------------------

	/// <summary></summary>
	public sealed class PpsDataFieldBinding : MarkupExtension, IPpsXamlEmitter
	{
		/// <summary></summary>
		public PpsDataFieldBinding()
		{
		}

		/// <summary></summary>
		/// <param name="fieldName"></param>
		public PpsDataFieldBinding(string fieldName)
		{
			this.FieldName = fieldName;
		} // ctor

		/// <summary></summary>
		/// <param name="serviceProvider"></param>
		/// <returns></returns>
		public override object ProvideValue(IServiceProvider serviceProvider) 
			=> this;

		System.Xaml.XamlReader IPpsXamlEmitter.CreateReader(IServiceProvider context)
		{
			var fieldInfo = PpsDataFieldInfo.GetDataFieldInfo(context, FieldName, null, true);
			return CreateWpfBinding(fieldInfo, IsReadOnly).CreateReader(context);
		} // func CreateReader

		/// <summary>Field name.</summary>
		[ConstructorArgument("fieldName")]
		public string FieldName { get; set; }
		/// <summary></summary>
		public bool? IsReadOnly { get; set; } = null;

		internal static LuaWpfCreator CreateWpfBinding(PpsDataFieldInfo fieldInfo, bool? isReadOnly = null, string append = null)
		{
			dynamic ui = new LuaUI();
			dynamic binding = ui.Binding;

			var bindingPath = append != null
				? CombinePath(fieldInfo.BindingPath, append)
				: fieldInfo.BindingPath;

			binding.Path = bindingPath;
			binding.Mode = isReadOnly.HasValue && isReadOnly.Value ? BindingMode.OneWay : BindingMode.Default;

			return binding;
		} // func CreateBindingForField

		internal static string CombinePath(string baseBindingPath, string bindingPath)
		{
			return baseBindingPath + "." + bindingPath;
		}
	} // class PpsDataFieldBinding

	#endregion

	#region -- class PpsDataFieldProperty ---------------------------------------------

	/// <summary></summary>
	public sealed class PpsDataFieldProperty : MarkupExtension, IPpsXamlEmitter
	{
		private sealed class ValueXamlEmiter : System.Xaml.XamlReader
		{
			private int state = -1;
			private object value;
						
			public ValueXamlEmiter(object value)
				=> this.value = value;

			public override bool Read()
			{
				return ++state <= 0;
			} // func Read

			public override bool IsEof => state > 0;

			public override XamlNodeType NodeType => state == 0 ? XamlNodeType.Value : XamlNodeType.None;

			public override NamespaceDeclaration Namespace => null;
			public override XamlType Type => null;
			public override XamlMember Member => null;
			public override object Value => state == 0 ? value : null;
			public override XamlSchemaContext SchemaContext => PpsXamlSchemaContext.Default;
		} // class ValueXamlEmiter

		/// <summary></summary>
		public PpsDataFieldProperty()
		{
		}

		/// <summary></summary>
		/// <param name="fieldName"></param>
		/// <param name="propertyName"></param>
		public PpsDataFieldProperty(string fieldName, string propertyName)
		{
			this.FieldName = fieldName;
			this.PropertyName = propertyName;
		} // ctor

		/// <summary></summary>
		/// <param name="serviceProvider"></param>
		/// <returns></returns>
		public override object ProvideValue(IServiceProvider serviceProvider)
			=> this;

		System.Xaml.XamlReader IPpsXamlEmitter.CreateReader(IServiceProvider context)
		{
			var fieldInfo = PpsDataFieldInfo.GetDataFieldInfo(context, FieldName, null, true);

			if (PropertyName != null && fieldInfo.TryGetProperty(PropertyName, out var value))
			{
				if (!String.IsNullOrEmpty(StringFormat))
					value = String.Format(StringFormat, value);

				return new ValueXamlEmiter(value);
			}
			else
				return null;
		} // func CreateReader

		/// <summary>Field name.</summary>
		[ConstructorArgument("fieldName")]
		public string FieldName { get; set; }
		/// <summary></summary>
		[ConstructorArgument("propertyName")]
		public string PropertyName { get; set; }
		/// <summary></summary>
		public string StringFormat { get; set; }
	} // class PpsDataFieldBinding

	#endregion

	#region -- class PpsDataField -----------------------------------------------------

	/// <summary>DataField emitter</summary>
	public sealed class PpsDataField : UIElement, IPpsXamlEmitter, IPpsXamlDynamicProperties, IPropertyReadOnlyDictionary
	{
		private readonly Dictionary<string, object> properties = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

		System.Xaml.XamlReader IPpsXamlEmitter.CreateReader(IServiceProvider context)
			=> context.GetService<IPpsDataFieldFactory>(true).CreateField(context, FieldName, this, GetLocalValueEnumerator());

		object IPpsXamlDynamicProperties.GetValue(string name)
			=> properties.TryGetValue(name, out var tmp) ? tmp : null;

		void IPpsXamlDynamicProperties.SetValue(string name, object value)
			=> properties[name] = value;

		/// <summary></summary>
		/// <param name="name"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public bool TryGetProperty(string name, out object value)
			=> properties.TryGetValue(name, out value);

		/// <summary>Field name.</summary>
		public string FieldName { get; set; }
		/// <summary>Binding path.</summary>
		public string BindingPath
		{
			get => TryGetProperty(nameof(BindingPath), out var t) ? (string)t : null;
			set
			{
				if (value == null)
					properties.Remove(nameof(BindingPath));
				else
					properties[nameof(BindingPath)] = value;
			}
		}
	} // class PpsDataField

	#endregion

	#region -- class PpsDataFieldFactory ----------------------------------------------

	/// <summary></summary>
	public sealed class PpsDataFieldFactory : LuaTable, IPpsDataFieldFactory
	{
		private readonly PpsEnvironment environment;

		/// <summary></summary>
		/// <param name="environment"></param>
		public PpsDataFieldFactory(PpsEnvironment environment)
		{
			this.environment = environment;
		} // ctor

		private LuaWpfCreator UpdateDefaultProperties(IPropertyReadOnlyDictionary properties, LuaWpfCreator ctrl, LocalValueEnumerator localProperties)
		{
			// update local properties
			while (localProperties.MoveNext())
			{
				var property = localProperties.Current.Property;
				var value = localProperties.Current.Value;

				if (NameScope.NameScopeProperty != property)
					ctrl[property] = value;
			}

			// update known properties
			if (properties.TryGetProperty<string>("displayName", out var displayName))
				ctrl[PpsDataFieldPanel.LabelProperty] = displayName + ":";
			if (properties.TryGetProperty<int>("GridLines", out var gridLines))
				ctrl[PpsDataFieldPanel.GridLinesProperty] = gridLines;
			if (properties.TryGetProperty<bool>("GridFullWidth", out var gridFullWidth))
				ctrl[PpsDataFieldPanel.FullWidthProperty] = gridFullWidth;
			if (properties.TryGetProperty<object>("GridGroup", out var gridGroup))
				ctrl[PpsDataFieldPanel.GroupNameProperty] = gridGroup;

			if (properties.TryGetProperty("Margin", out var margin))
				ctrl["Margin"] = margin;

			return ctrl;
		} // proc UpdateDefaultProperties

		/// <summary></summary>
		/// <param name="context"></param>
		/// <param name="fieldName"></param>
		/// <param name="properties"></param>
		/// <param name="localProperties"></param>
		/// <returns></returns>
		public System.Xaml.XamlReader CreateField(IServiceProvider context, string fieldName, IPropertyReadOnlyDictionary properties, LocalValueEnumerator localProperties)
		{
			if (TryResolveFieldCreator(context, fieldName, properties, out var ctrl))
				UpdateDefaultProperties(properties, ctrl, localProperties);
			else
			{
				var fieldInfo = GetFieldInfo(context, fieldName, properties);

				if (!fieldInfo.TryGetProperty<string>("useFieldFactory", out var fieldFactory)
					|| !TryResolveFieldCreator(context, fieldFactory, fieldInfo, out ctrl))
					ctrl = CreateDefaultField(fieldInfo);

				UpdateDefaultProperties(fieldInfo, ctrl, localProperties);
			}

			return ctrl.CreateReader(context);
		} // func CreateField

		private bool TryResolveFieldCreator(IServiceProvider context, string fieldName, IPropertyReadOnlyDictionary properties, out LuaWpfCreator creator)
		{
			// resolve complex fieldinformation within the Environment
			// the member must be registered within the table.
			var result = CallMemberDirect(fieldName, new object[] { context, new LuaPropertiesTable(properties), properties as PpsDataFieldInfo }, rawGet: true, throwExceptions: true, ignoreNilFunction: true)[0];
			if (result == null)
			{
				creator = null;
				return false;
			}
			else if (result is LuaWpfCreator r)
			{
				creator = r;
				return true;
			}
			else
				throw new ArgumentNullException(fieldName, "Return type must be a control creator.");
		} // func TryResolveFieldCreator

		[LuaMember]
		private double GetWidth(double w)
			=> w * 10;

		[LuaMember]
		private double GetHeight(double h)
			=> h * 23;

		[LuaMember]
		private object GetCode(IServiceProvider context)
			=> context?.GetService<IPpsXamlCode>(false);

		[LuaMember]
		private PpsDataFieldInfo GetFieldInfo(IServiceProvider context, string fieldName, object properties)
			=> PpsDataFieldInfo.GetDataFieldInfo(context, fieldName, Procs.ToProperties(properties));

		[LuaMember]
		private PpsDataFieldInfo CreateFieldInfo(IServiceProvider context, string fieldName, Type dataType, string bindingPath, object properties)
			=> new PpsDataFieldInfo(context, fieldName, dataType, bindingPath, Procs.ToProperties(properties));

		[LuaMember]
		private LuaWpfCreator CreateFieldBinding(PpsDataFieldInfo fieldInfo, bool? isReadOnly = null, string append = null)
			=> PpsDataFieldBinding.CreateWpfBinding(fieldInfo, isReadOnly, append);

		[LuaMember]
		private LuaWpfCreator CreateDefaultField(PpsDataFieldInfo fieldInfo)
		{
			// test for creator
			if (fieldInfo.DataType == typeof(string)
				|| fieldInfo.DataType == typeof(int)
				|| fieldInfo.DataType == typeof(float)
				|| fieldInfo.DataType == typeof(double)
				|| fieldInfo.DataType == typeof(decimal))
				return CreateTextField(fieldInfo);
			else if (fieldInfo.DataType == typeof(DateTime))
				return CreateDateTimeField(fieldInfo);
			else if (fieldInfo.DataType == typeof(PpsMasterDataExtendedValue))
			{
				// test for master table
				if (fieldInfo.TryGetProperty<string>("refTable", out var refTable))
					return CreateMasterDataField(fieldInfo, refTable);
				else
					throw new ArgumentNullException("refTable", "refTable is null.");
			}
			else if (fieldInfo.DataType == typeof(PpsFormattedStringValue))
				return CreateTextField(fieldInfo, true);
			else if (fieldInfo.ColumnDefinition?.IsRelationColumn ?? false)
				return CreateRelationField(fieldInfo, fieldInfo.ColumnDefinition);
			else 
				throw new NotImplementedException();
		} // func CreateDefaultField

		private static void SetNumericBinding(dynamic ui, dynamic textBox, dynamic textBinding, bool allowNeg, int floatDigits)
		{
			textBinding.Converter = PpsConverter.NumericValue;
			textBinding.ConverterParameter = new NumericValueConverterParameter() { AllowNeg = allowNeg, FloatDigits = floatDigits };
		} // proc SetNumericBinding
		
		[LuaMember]
		private LuaWpfCreator CreateTextField(PpsDataFieldInfo fieldInfo, bool formattedText = false)
		{
			var ui = new LuaUI();
			dynamic txt = LuaWpfCreator.CreateFactory(ui, typeof(PpsTextBox));

			var isReadOnly = fieldInfo.TryGetProperty<bool>("IsReadOnly", out var tmpReadOnly) ? (bool?)tmpReadOnly : null;

			var textBinding = PpsDataFieldBinding.CreateWpfBinding(fieldInfo, append: formattedText ? "Value" : null, isReadOnly: isReadOnly);

			var inputType = formattedText ? PpsTextBoxInputType.MultiLine : PpsTextBoxInputType.None;
			switch (Type.GetTypeCode(fieldInfo.DataType))
			{
				case TypeCode.Decimal:
					PpsDataFieldFactory.SetNumericBinding(ui, txt, textBinding, true, 2);
					inputType = PpsTextBoxInputType.DecimalNegative;
					break;
				case TypeCode.Single:
					PpsDataFieldFactory.SetNumericBinding(ui, txt, textBinding, true, 3);
					inputType = PpsTextBoxInputType.DecimalNegative;
					break;
				case TypeCode.Double:
					PpsDataFieldFactory.SetNumericBinding(ui, txt, textBinding, true, 6);
					inputType = PpsTextBoxInputType.DecimalNegative;
					break;

				case TypeCode.SByte:
					PpsDataFieldFactory.SetNumericBinding(ui, txt, textBinding, true, 0);
					inputType = PpsTextBoxInputType.IntegerNegative;
					break;
				case TypeCode.Int16:
					PpsDataFieldFactory.SetNumericBinding(ui, txt, textBinding, true, 0);
					inputType = PpsTextBoxInputType.IntegerNegative;
					break;
				case TypeCode.Int32:
					PpsDataFieldFactory.SetNumericBinding(ui, txt, textBinding, true, 0);
					inputType = PpsTextBoxInputType.IntegerNegative;
					break;
				case TypeCode.Int64:
					PpsDataFieldFactory.SetNumericBinding(ui, txt, textBinding, true, 0);
					inputType = PpsTextBoxInputType.IntegerNegative;
					break;

				case TypeCode.Byte:
					PpsDataFieldFactory.SetNumericBinding(ui, txt, textBinding, false, 0);
					inputType = PpsTextBoxInputType.Integer;
					break;
				case TypeCode.UInt16:
					SetNumericBinding(ui, txt, textBinding, false, 0);
					inputType = PpsTextBoxInputType.Integer;
					break;
				case TypeCode.UInt32:
					PpsDataFieldFactory.SetNumericBinding(ui, txt, textBinding, false, 0);
					inputType = PpsTextBoxInputType.Integer;
					break;
				case TypeCode.UInt64:
					PpsDataFieldFactory.SetNumericBinding(ui, txt, textBinding, false, 0);
					inputType = PpsTextBoxInputType.Integer;
					break;
			}

			if (fieldInfo.TryGetProperty<PpsTextBoxInputType>("InputType", out var tmpInputType))
				inputType = tmpInputType;

			txt.InputType = inputType;
			txt.Text = textBinding;

			if (fieldInfo.TryGetProperty<double>("Height", out var height))
			{
				txt.Height = GetHeight(height);
				txt.VerticalAlignment = VerticalAlignment.Top;
			}
			else
				txt.VerticalAlignment = VerticalAlignment.Top;

			if (fieldInfo.TryGetProperty<double>("Width", out var width))
			{
				txt.Width = GetWidth(width);
				txt.HorizontalAlignment = HorizontalAlignment.Left;
			}
			else
				txt.HorizontalAlignment = HorizontalAlignment.Stretch;

			if (fieldInfo.TryGetProperty<int>("MaxLength", out var maxInputLength))
				txt.MaxLength = maxInputLength;

			if (fieldInfo.TryGetProperty<bool>("IsNullable", out var tmpNullable))
				txt.IsNullable = tmpNullable;

			if (isReadOnly.HasValue)
				txt.IsReadOnly = isReadOnly;

			if (formattedText)
			{
				txt.FormattedValue = PpsDataFieldBinding.CreateWpfBinding(fieldInfo, append: "FormattedValue", isReadOnly: true);
			}

			return txt;
		} // func CreateTextField

		private LuaWpfCreator CreateSelector(PpsDataFieldInfo fieldInfo)
		{
			dynamic combobox = LuaWpfCreator.CreateFactory(new LuaUI(), typeof(PpsDataSelector));

			if (fieldInfo.TryGetProperty<bool>("IsNullable", out var tmpNullable))
				combobox.IsNullable = tmpNullable;

			return combobox;
		} // func CreateSelector

		[LuaMember]
		private LuaWpfCreator CreateMasterDataField(PpsDataFieldInfo fieldInfo, string refTableName)
		{
			dynamic combobox = CreateSelector(fieldInfo);

			combobox.ItemsSource = environment.MasterData.GetTable(refTableName, true);
			combobox.SelectedValue = PpsDataFieldBinding.CreateWpfBinding(fieldInfo);

			return combobox;
		} // func CreateMasterDataField

		[LuaMember]
		private LuaWpfCreator CreateRelationField(PpsDataFieldInfo fieldInfo, PpsDataColumnDefinition columnDefinition)
		{
			dynamic combobox = CreateSelector(fieldInfo);

			// bind items source
			var baseBindingPath = fieldInfo.Context.GetService<PpsDataSetResolver>(true).BindingPath;
			var codeBase = fieldInfo.Context.GetService<IPpsXamlCode>(true);

			dynamic itemsSourceBinding = LuaWpfCreator.CreateFactory(new LuaUI(), typeof(Binding));
			itemsSourceBinding.Path = PpsDataFieldBinding.CombinePath(baseBindingPath, columnDefinition.ParentColumn.Table.Name);
			itemsSourceBinding.Source = codeBase;
			combobox.ItemsSource = itemsSourceBinding;

			// bind value
			combobox.SelectedValue = PpsDataFieldBinding.CreateWpfBinding(fieldInfo);

			return combobox;
		} // func CreateRelationField

		[LuaMember]
		private static LuaWpfCreator CreateDateTimeField(PpsDataFieldInfo fieldInfo)
		{
			dynamic ui = new LuaUI();
			dynamic date = LuaWpfCreator.CreateFactory(ui, typeof(System.Windows.Controls.DatePicker));

			date.SelectedDate = PpsDataFieldBinding.CreateWpfBinding(fieldInfo);

			return date;
		} // func CreateDateTimeField

		[LuaMember]
		private static LuaWpfCreator CreateComboField(PpsDataFieldInfo fieldInfo)
		{
			dynamic ui = new LuaUI();
			dynamic combo = LuaWpfCreator.CreateFactory(ui, typeof(System.Windows.Controls.ComboBox));

			combo.SelectedValue = PpsDataFieldBinding.CreateWpfBinding(fieldInfo);

			return combo;
		} // func CreateComboField

		/// <summary></summary>
		[LuaMember]
		public PpsEnvironment Environment => environment;
	} // class PpsDataFieldFactory

	#endregion
}
