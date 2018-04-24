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
		/// <param name="name"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public bool TryGetProperty(string name, out object value)
			=> properties.TryGetProperty(name, out value)
				|| dataColumn.Attributes.TryGetProperty(name, out value);

		/// <summary></summary>
		public string Name => dataColumn.Name;
		/// <summary></summary>
		public Type DataType => dataColumn.DataType;

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
		/// <returns></returns>
		System.Xaml.XamlReader CreateField(IServiceProvider context, string fieldName, IPropertyReadOnlyDictionary properties);
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

		PpsDataFieldInfo IPpsDataFieldResolver.ResolveColumn(IServiceProvider serviceProvider, string fieldExpression, IPropertyReadOnlyDictionary properties)
		{
			CheckInitialized();

			// resolve table
			if (table == null && tableName != null)
			{
				datasetResolver = (PpsDataSetResolver)GetParentService(typeof(PpsDataSetResolver)) ?? throw new ArgumentException("DataSet resolve is missing.");
				table = datasetResolver.DataSetDefinition.FindTable(tableName);
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
		public string FieldName { get; set; }
		/// <summary></summary>
		public bool? IsReadOnly { get; set; } = null;

		internal static LuaWpfCreator CreateWpfBinding(PpsDataFieldInfo fieldInfo, bool? isReadOnly = null)
		{
			dynamic ui = new LuaUI();
			dynamic binding = ui.Binding;

			binding.Path = fieldInfo.BindingPath;
			binding.Mode = isReadOnly.HasValue && isReadOnly.Value ? BindingMode.OneWay : BindingMode.Default;

			return binding;
		} // func CreateBindingForField
	} // class PpsDataFieldBinding

	#endregion

	#region -- class PpsDataField -----------------------------------------------------

	/// <summary>DataField emitter</summary>
	public sealed class PpsDataField : UIElement, IPpsXamlEmitter, IPpsXamlDynamicProperties, IPropertyReadOnlyDictionary
	{
		private readonly Dictionary<string, object> properties = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

		System.Xaml.XamlReader IPpsXamlEmitter.CreateReader(IServiceProvider context)
			=> context.GetService<IPpsDataFieldFactory>(true).CreateField(context, FieldName, this);

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
	public sealed class PpsDataFieldFactory : IPpsDataFieldFactory
	{
		private readonly PpsEnvironment environment;

		/// <summary></summary>
		/// <param name="environment"></param>
		public PpsDataFieldFactory(PpsEnvironment environment)
		{
			this.environment = environment;
		} // ctor
				
		/// <summary></summary>
		/// <param name="context"></param>
		/// <param name="fieldName"></param>
		/// <param name="properties"></param>
		/// <returns></returns>
		public System.Xaml.XamlReader CreateField(IServiceProvider context, string fieldName, IPropertyReadOnlyDictionary properties)
		{
			var fieldInfo = PpsDataFieldInfo.GetDataFieldInfo(context, fieldName, properties);

			var ctrl = CreateDefaultField(fieldInfo);

			if (fieldInfo.TryGetProperty<string>("displayName", out var displayName))
				ctrl[PpsDataFieldPanel.LabelProperty] = displayName + ":";

			return ctrl.CreateReader(context);
		} // func CreateField

		private LuaWpfCreator CreateDefaultField(PpsDataFieldInfo fieldInfo)
		{
			// test for datatype
			if (fieldInfo.DataType == typeof(string))
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
			else
				throw new NotImplementedException();
		} // func CreateDefaultField

		private static void SetNumericBinding(dynamic ui, dynamic textBox, dynamic textBinding, bool allowNeg, int floatDigits)
		{
			textBinding.Converter = PpsConverter.NumericValue;
			textBinding.ConverterParameter = new NumericValueConverterParameter() { AllowNeg = allowNeg, FloatDigits = floatDigits };
		} // proc SetNumericBinding

		private static LuaWpfCreator CreateTextField(PpsDataFieldInfo fieldInfo)
		{
			dynamic ui = new LuaUI();
			dynamic txt = ui.PpsTextBox;

			var isReadOnly = fieldInfo.TryGetProperty<bool>("IsReadOnly", out var tmpReadOnly) ? (bool?)tmpReadOnly : null;

			var textBinding = PpsDataFieldBinding.CreateWpfBinding(fieldInfo, isReadOnly);

			var inputType = PpsTextBoxInputType.None;
			switch (Type.GetTypeCode(fieldInfo.DataType))
			{
				case TypeCode.Decimal:
					SetNumericBinding(ui, txt, textBinding, true, 2);
					inputType = PpsTextBoxInputType.DecimalNegative;
					break;
				case TypeCode.Single:
					SetNumericBinding(ui, txt, textBinding, true, 3);
					inputType = PpsTextBoxInputType.DecimalNegative;
					break;
				case TypeCode.Double:
					SetNumericBinding(ui, txt, textBinding, true, 6);
					inputType = PpsTextBoxInputType.DecimalNegative;
					break;

				case TypeCode.SByte:
					SetNumericBinding(ui, txt, textBinding, true, 0);
					inputType = PpsTextBoxInputType.IntegerNegative;
					break;
				case TypeCode.Int16:
					SetNumericBinding(ui, txt, textBinding, true, 0);
					inputType = PpsTextBoxInputType.IntegerNegative;
					break;
				case TypeCode.Int32:
					SetNumericBinding(ui, txt, textBinding, true, 0);
					inputType = PpsTextBoxInputType.IntegerNegative;
					break;
				case TypeCode.Int64:
					SetNumericBinding(ui, txt, textBinding, true, 0);
					inputType = PpsTextBoxInputType.IntegerNegative;
					break;

				case TypeCode.Byte:
					SetNumericBinding(ui, txt, textBinding, false, 0);
					inputType = PpsTextBoxInputType.Integer;
					break;
				case TypeCode.UInt16:
					SetNumericBinding(ui, txt, textBinding, false, 0);
					inputType = PpsTextBoxInputType.Integer;
					break;
				case TypeCode.UInt32:
					SetNumericBinding(ui, txt, textBinding, false, 0);
					inputType = PpsTextBoxInputType.Integer;
					break;
				case TypeCode.UInt64:
					SetNumericBinding(ui, txt, textBinding, false, 0);
					inputType = PpsTextBoxInputType.Integer;
					break;
			}

			if (fieldInfo.TryGetProperty<PpsTextBoxInputType>("InputType", out var tmpInputType))
				inputType = tmpInputType;

			txt.InputType = inputType;
			txt.Text = textBinding;

			if (fieldInfo.TryGetProperty<bool>("IsNullable", out var tmpNullable))
				txt.IsNullable = tmpNullable;

			if (isReadOnly.HasValue)
				txt.IsReadOnly = isReadOnly;

			return txt;
		} // func CreateTextField

		private LuaWpfCreator CreateMasterDataField(PpsDataFieldInfo fieldInfo, string refTableName)
		{
			dynamic ui = new LuaUI();
			dynamic combobox = ui.Pps.PpsDataSelector;
			combobox.ItemsSource = environment.MasterData.GetTable(refTableName, true);
			combobox.SelectedValue = PpsDataFieldBinding.CreateWpfBinding(fieldInfo);
			return combobox;
		} // func CreateMasterDataField

		private static LuaWpfCreator CreateDateTimeField(PpsDataFieldInfo fieldInfo)
			=> throw new NotImplementedException();
	} // class PpsDataFieldFactory

	#endregion
}
