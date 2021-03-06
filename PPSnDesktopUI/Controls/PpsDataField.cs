﻿#region -- copyright --
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
using System.Dynamic;
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
	public sealed class PpsDataFieldInfo : IPropertyReadOnlyDictionary
	{
		private readonly IDataColumn dataColumn;

		/// <summary></summary>
		/// <param name="dataColumn"></param>
		/// <param name="bindingPath"></param>
		public PpsDataFieldInfo(IDataColumn dataColumn, string bindingPath)
		{
			this.dataColumn = dataColumn ?? throw new ArgumentNullException(nameof(dataColumn));
			this.BindingPath = bindingPath ?? throw new ArgumentNullException(nameof(bindingPath));
		} // ctor

		/// <summary></summary>
		/// <param name="name"></param>
		/// <param name="dataType"></param>
		/// <param name="bindingPath"></param>
		/// <param name="properties"></param>
		public PpsDataFieldInfo(string name, Type dataType, string bindingPath, IPropertyEnumerableDictionary properties)
		{
			this.dataColumn = new SimpleDataColumn(name, dataType, properties);
			this.BindingPath = bindingPath ?? throw new ArgumentNullException(nameof(bindingPath));
		} // ctor

		/// <summary></summary>
		/// <param name="name"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public bool TryGetProperty(string name, out object value)
			=> dataColumn.Attributes.TryGetProperty(name, out value)
				|| TryGetDefaultProperty(name, out value);

		private bool TryGetDefaultProperty(string name, out object value)
		{
			switch(name)
			{
				case nameof(BindingPath):
					value = BindingPath;
					return true;
				case nameof(Name):
					value = Name;
					return true;
				case nameof(DataType):
					value = DataType;
					return true;
				case nameof(ColumnDefinition):
					value = dataColumn;
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

		/// <summary>BindingPath to use this field.</summary>
		public string BindingPath { get; }

		/// <summary></summary>
		/// <param name="serviceProvider"></param>
		/// <param name="fieldName"></param>
		/// <param name="throwException"></param>
		/// <returns></returns>
		public static PpsDataFieldInfo GetDataFieldInfo(IServiceProvider serviceProvider, string fieldName, bool throwException = true)
		{
			if (serviceProvider == null)
				throw new ArgumentNullException(nameof(serviceProvider));
			if (fieldName == null)
				throw new ArgumentNullException(nameof(fieldName));

			var field = serviceProvider.GetService<IPpsDataFieldResolver>(true)?.ResolveColumn(serviceProvider, fieldName);
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
		/// <returns></returns>
		PpsDataFieldInfo ResolveColumn(IServiceProvider sp, string fieldExpression);
	} // interface IPpsDataFieldResolver

	#endregion

	#region -- interface IPpsDataFieldFactory -----------------------------------------

	/// <summary></summary>
	public interface IPpsDataFieldFactory
	{
		/// <summary></summary>
		/// <param name="properties"></param>
		/// <returns></returns>
		System.Xaml.XamlReader CreateField(IPpsDataFieldReadOnlyProperties properties);
	} // interface IPpsDataFieldFactory

	#endregion

	#region -- class PpsDataSetResolver -----------------------------------------------

	/// <summary>Parser service to resolve a dataset.</summary>
	public class PpsDataSetResolver : PpsParserService, IPpsDataFieldResolver
	{
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

		internal static bool TryResolveByDataSetName(IServiceProvider sp, string datasetName, bool throwException, out PpsDataSetDefinition dataset)
		{
			if (datasetName != null)
			{
				var info = sp.GetService<IPpsDataSetProvider>(throwException);
				dataset = info?.TryGetDataSetDefinition(datasetName, throwException);
				return dataset != null;
			}
			else if (throwException)
				throw new ArgumentNullException(nameof(datasetName));

			dataset = null;
			return false;
		} // func TryResolveByDataSetName

		private static bool TryResolveByFunction(IServiceProvider sp, Func<object> getDataSet, out PpsDataSetDefinition dataset)
		{
			switch (getDataSet?.Invoke())
			{
				case PpsDataSet ds:
					dataset = ds.DataSetDefinition;
					return true;
				case PpsDataSetDefinition ds:
					dataset = ds;
					return true;
				default:
					dataset = null;
					return false;

			}
		} // func TryResolveByFunction

		/// <summary></summary>
		protected override void OnInitialized()
		{
			base.OnInitialized();

			if (dataset == null)
			{
				if (!TryResolveByDataSetName(this, DataSetName, false, out dataset)
					&& !TryResolveByFunction(this, GetDataSet, out dataset))
					throw new ArgumentException("DataSet could not resolved.");
			}
		} // proc OnInitialized

		PpsDataFieldInfo IPpsDataFieldResolver.ResolveColumn(IServiceProvider serviceProvider, string fieldExpression)
		{
			CheckInitialized();

			var p = fieldExpression.IndexOf('.');
			if (p == -1) // we expected Table.Column
				return null;

			var tableName = fieldExpression.Substring(0, p);
			var table = dataset.FindTable(tableName) ?? throw new ArgumentOutOfRangeException(nameof(tableName), tableName, "Could not locate table.");
			var columnExpression = fieldExpression.Substring(p + 1);

			return PpsDataTableResolver.TryResolveColumn(
				table, 
				columnExpression, 
				PpsDataFieldBinding.CombinePath(BindingPath, columnExpression), 
				out var fieldInfo
			)
				? fieldInfo 
				: throw new ArgumentOutOfRangeException(nameof(fieldExpression), fieldExpression, "Could not resolve field expression.");
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
		
		/// <summary></summary>
		public Func<object> GetDataSet { get; set; } = null;
		/// <summary>Name of the dataset.</summary>
		public string DataSetName { get; set; } = null;

		internal static PpsDataFieldInfo GetDataFieldInfo(PpsDataColumnDefinition column, string baseBindingPath)
			=> new PpsDataFieldInfo(column, baseBindingPath);
	} // class PpsDataSetResolver

	#endregion

	#region -- class PpsDataTableResolver ---------------------------------------------

	/// <summary></summary>
	public class PpsDataTableResolver : PpsParserService, IPpsDataFieldResolver
	{
		private string tableName = null;
		private string bindingPath = null;

		private PpsDataTableDefinition table = null;

		/// <summary></summary>
		public PpsDataTableResolver() { }

		/// <summary></summary>
		public PpsDataTableResolver(PpsDataTable table)
		{
			this.tableName = table.TableName;
			this.table = table.TableDefinition;
		} // ctor

		private static PpsDataTableDefinition ResolveDataTable(PpsDataSetDefinition dataset, string tableName)
			=> dataset.FindTable(tableName) ?? throw new ArgumentOutOfRangeException(nameof(tableName), tableName, "Could not resolve table.");

		/// <summary></summary>
		protected override void OnInitialized()
		{
			base.OnInitialized();
			if (table == null)
			{
				if (tableName == null)
					throw new ArgumentNullException(nameof(tableName));
				else
				{
					var p = tableName.IndexOf('.');
					if (p == -1)
					{
						if (GetParentService(typeof(PpsDataSetResolver)) is PpsDataSetResolver datasetResolver)
							table = ResolveDataTable(datasetResolver.DataSetDefinition, tableName);
						else
							throw new ArgumentOutOfRangeException(nameof(tableName), tableName, "Could not locate DataSetResolver.");
					}
					else if (PpsDataSetResolver.TryResolveByDataSetName(this, tableName.Substring(0, p), false, out var dataset))
						table = ResolveDataTable(dataset, tableName.Substring(p + 1));
					else
						throw new ArgumentOutOfRangeException(nameof(tableName), tableName, "Could not resolve table.");
				}
			}
		} // func OnInitialized

		internal static bool TryResolveColumn(PpsDataTableDefinition table, string fieldExpression, string bindingPath, out PpsDataFieldInfo fieldInfo)
		{
			var p = fieldExpression.IndexOf('.');
			if (p == -1)
			{
				var columnIndex = table.FindColumnIndex(fieldExpression, false);
				if (columnIndex == -1)
				{
					fieldInfo = null;
					return false;
				}
				else
				{
					fieldInfo = new PpsDataFieldInfo(table.Columns[columnIndex], bindingPath);
					return true;
				}
			}
			else
			{
				var columnIndex = table.FindColumnIndex(fieldExpression.Substring(0, p), false);
				if (columnIndex == -1)
				{
					fieldInfo = null;
					return false;
				}

				var column = table.Columns[columnIndex];
				if (column.IsRelationColumn) // follow path
				{
					if (TryResolveColumn(column.ParentColumn.Table, fieldExpression.Substring(p + 1), bindingPath, out fieldInfo))
						return true;
					else
					{
						fieldInfo = null;
						return false;
					}
				}
				else // follow object properties of datatype or extended column
					throw new NotImplementedException();
			}
		} // func TryResolveColumn
		
		PpsDataFieldInfo IPpsDataFieldResolver.ResolveColumn(IServiceProvider serviceProvider, string fieldExpression)
		{
			CheckInitialized();

			if (TryResolveColumn(table, fieldExpression, PpsDataFieldBinding.CombinePath(BindingPath, fieldExpression), out var fieldInfo))
				return fieldInfo;
			else if (GetParentService(typeof(IPpsDataFieldResolver)) is IPpsDataFieldResolver resolver)
				return resolver.ResolveColumn(serviceProvider, fieldExpression);
			else
				return null;
		} // func ResolveColumn

		/// <summary></summary>
		/// <param name="serviceType"></param>
		/// <returns></returns>
		public override object GetService(Type serviceType)
			=> serviceType == typeof(IPpsDataFieldResolver) ? this : GetParentService(serviceType);

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
			var fieldInfo = PpsDataFieldInfo.GetDataFieldInfo(context, FieldName, true);
			return CreateWpfBinding(fieldInfo, IsReadOnly).CreateReader(context);
		} // func CreateReader

		/// <summary>Field name.</summary>
		[ConstructorArgument("fieldName")]
		public string FieldName { get; set; }
		/// <summary></summary>
		public bool? IsReadOnly { get; set; } = null;

		/// <summary></summary>
		/// <param name="fieldInfo"></param>
		/// <param name="isReadOnly"></param>
		/// <param name="append"></param>
		/// <returns></returns>
		public static LuaWpfCreator CreateWpfBinding(PpsDataFieldInfo fieldInfo, bool? isReadOnly = null, string append = null)
		{
			dynamic ui = new LuaUI();
			dynamic binding = ui.Binding;

			var bindingPath = append != null
				? CombinePath(fieldInfo.BindingPath, append)
				: fieldInfo.BindingPath;

			binding.Path = bindingPath;
			binding.Mode = isReadOnly.HasValue && isReadOnly.Value ? BindingMode.OneWay : BindingMode.Default;

			if (fieldInfo.DataType == typeof(string))
				binding.TargetNullValue = String.Empty;

			return binding;
		} // func CreateBindingForField

		/// <summary></summary>
		/// <param name="baseBindingPath"></param>
		/// <param name="bindingPath"></param>
		/// <returns></returns>
		public static string CombinePath(string baseBindingPath, string bindingPath)
		{
			if (String.IsNullOrEmpty(baseBindingPath))
				return bindingPath;
			else if (String.IsNullOrEmpty(bindingPath))
				return baseBindingPath;

			return baseBindingPath + "." + bindingPath;
		} // func CombinePath
	} // class PpsDataFieldBinding

	#endregion

	#region -- class PpsDataFieldProperty ---------------------------------------------

	/// <summary></summary>
	public sealed class PpsDataFieldProperty : MarkupExtension, IPpsXamlEmitter
	{
		#region -- class ValueXamlEmiter ----------------------------------------------

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

		#endregion

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
			var fieldInfo = PpsDataFieldInfo.GetDataFieldInfo(context, FieldName, true);
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

	#region -- IPpsDataFieldReadOnlyProperties ----------------------------------------

	/// <summary></summary>
	public interface IPpsDataFieldReadOnlyProperties : IDynamicMetaObjectProvider, IPropertyReadOnlyDictionary, IServiceProvider
	{
		/// <summary></summary>
		/// <param name="dataField"></param>
		/// <returns></returns>
		IPpsDataFieldReadOnlyProperties CreateFieldProperties(PpsDataFieldInfo dataField);

		/// <summary></summary>
		string FieldName { get; }
		/// <summary></summary>
		string UseFieldFactory { get; }
		/// <summary></summary>
		Type DataType { get; }
		
		/// <summary></summary>
		IServiceProvider Context { get; }

		/// <summary></summary>
		/// <returns></returns>
		IEnumerable<LocalValueEntry> LocalProperties();
	} // interface IPpsDataFieldReadOnlyProperties

	#endregion

	#region -- class PpsDataField -----------------------------------------------------

	/// <summary>DataField emitter</summary>
	public sealed class PpsDataField : FrameworkElement, IPpsXamlEmitter, IPpsXamlDynamicProperties
	{
		#region -- class PpsDataFieldReadOnlyProperties -------------------------------

		/// <summary></summary>
		private sealed class PpsDataFieldReadOnlyProperties : LuaTable, IPpsDataFieldReadOnlyProperties
		{
			private readonly IServiceProvider context;
			private readonly PpsDataField dataField;
			private readonly PpsDataFieldInfo dataFieldInfo;

			public PpsDataFieldReadOnlyProperties(IServiceProvider context, PpsDataField dataField, PpsDataFieldInfo dataFieldInfo)
			{
				this.context = context ?? throw new ArgumentNullException(nameof(context));
				this.dataField = dataField ?? throw new ArgumentNullException(nameof(dataField));
				this.dataFieldInfo = dataFieldInfo ?? throw new ArgumentNullException(nameof(dataFieldInfo));
			} // ctor

			[LuaMember]
			public IPpsDataFieldReadOnlyProperties CreateFieldProperties(PpsDataFieldInfo dataFieldInfo)
				=> new PpsDataFieldReadOnlyProperties(context, dataField, dataFieldInfo);

			private bool TryGetLocalProperty(string memberName, out object value)
			{
				var p = memberName.IndexOf('.');
				if (p == -1)
				{
					value = null;
					return false;
				}

				var ownerName = memberName.Substring(0, p);
				var propertyName = memberName.Substring(p + 1);

				foreach (var prop in LocalProperties())
				{
					if (prop.Property.OwnerType.Name == ownerName
						  && prop.Property.Name == propertyName)
					{
						value = prop.Value;
						return true;
					}
				}

				value = null;
				return false;
			} // func TryGetLocalProperty

			public IEnumerable<LocalValueEntry> LocalProperties()
			{
				var e = dataField.GetLocalValueEnumerator();
				while (e.MoveNext())
				{
					if (e.Current.Property != NameScope.NameScopeProperty)
						yield return e.Current;
				}
			} // func LocalProperties

			/// <summary>Do not change properties</summary>
			/// <param name="key"></param>
			/// <param name="value"></param>
			/// <returns></returns>
			protected override bool OnNewIndex(object key, object value)
				=> throw new NotSupportedException();

			protected override object OnIndex(object key)
			{
				if (key is string memberName)
				{
					switch (memberName)
					{
						case nameof(FieldName):
							return dataField.FieldName;
						case nameof(BindingPath):
							return dataFieldInfo.BindingPath;
						case nameof(UseFieldFactory):
							return dataField.UseFieldFactory;
						default:
							if (dataField.properties.TryGetValue(memberName, out var value)
								|| TryGetLocalProperty(memberName, out value)
								|| dataFieldInfo.TryGetProperty(memberName, out value))
								return value;
							else
								return null;
					}
				}
				else if (key is DependencyProperty prop)
				{
					var value = dataField.GetValue(prop);
					if (value == null)
					{
						if (!dataField.properties.TryGetValue(prop.Name, out value))
							value = null;
					}
					return value;
				}
				else if (key is PpsDataFieldInfo dataFieldInfo)
					return CreateFieldProperties(dataFieldInfo);
				else
					return null;
			} // func OnIndex

			public bool TryGetProperty(string name, out object value)
				=> (value = GetMemberValue(name)) != null;

			/// <summary></summary>
			/// <param name="serviceType"></param>
			/// <returns></returns>
			[LuaMember]
			public object GetService(object serviceType)
			{
				switch (serviceType)
				{
					case string typeName:
						if (typeName == "DataFieldInfo")
							return dataFieldInfo;
						return GetService(LuaType.GetType(typeName));
					case LuaType luaType:
						return context.GetService(luaType.Type);
					case Type type:
						return context.GetService(type);
					default:
						return null;
				}
			} // func GetService

			object IServiceProvider.GetService(Type serviceType)
			{
				if (serviceType == typeof(PpsDataFieldInfo))
					return dataFieldInfo;
				else if (serviceType == typeof(IDataColumn)
					|| serviceType == typeof(PpsDataColumnDefinition))
					return dataFieldInfo.ColumnDefinition;
				else
					return context.GetService(serviceType);
			} // func GetService

			public string FieldName => dataField.FieldName;
			public Type DataType => dataFieldInfo.DataType;
			public string UseFieldFactory => dataFieldInfo.TryGetProperty<string>(nameof(UseFieldFactory), out var fieldFactory) ? fieldFactory : dataField.UseFieldFactory;
			public IServiceProvider Context => context;
		} // class PpsDataFieldReadOnlyProperties

		#endregion

		private static readonly Dictionary<string, DependencyProperty> knownProperties = new Dictionary<string, DependencyProperty>(StringComparer.OrdinalIgnoreCase)
		{
			{ "GridLabel", PpsDataFieldPanel.LabelProperty },
			{ "GridLines", PpsDataFieldPanel.GridLinesProperty },
			{ "GridFullWidth", PpsDataFieldPanel.FullWidthProperty },
			{ "GridGroup", PpsDataFieldPanel.GroupNameProperty }
		};

		private readonly Dictionary<string, object> properties = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

		System.Xaml.XamlReader IPpsXamlEmitter.CreateReader(IServiceProvider context)
			=> context.GetService<IPpsDataFieldFactory>(true)
				.CreateField(new PpsDataFieldReadOnlyProperties(context, this, PpsDataFieldInfo.GetDataFieldInfo(context, FieldName, true)));

		private bool TryGetKnownProperty(string name, out object value)
		{
			if (knownProperties.TryGetValue(name, out var property))
			{
				value = GetValue(property);
				return true;
			}
			else
			{
				value = null;
				return false;
			}
		} // prop TryGetKnownProperty

		private bool TrySetKnownProperty(string name, object value)
		{
			if (knownProperties.TryGetValue(name, out var property))
			{
				SetValue(property, value?.ChangeTypeWithConverter(property.PropertyType));
				return true;
			}
			else
				return false;
		} // proc TrySetKnownProperty

		object IPpsXamlDynamicProperties.GetValue(string name)
			=> TryGetKnownProperty(name, out var value) ? value : GetPropertyValue(name);

		void IPpsXamlDynamicProperties.SetValue(string name, object value)
		{
			if (!TrySetKnownProperty(name, value))
				SetPropertyValue(name, value);
		} // func SetValue
		private object GetPropertyValue(string name) 
			=> properties.TryGetValue(name, out var tmp) ? tmp : null;

		private void SetPropertyValue(string name, object value)
		{
			if (value == null)
				properties.Remove(name);
			else
				properties[name] = value;
		} // proc SetPropertyValue
		
		/// <summary>Field name.</summary>
		public string FieldName { get; set; }
		/// <summary>Binding path.</summary>
		public string BindingPath
		{
			get => GetPropertyValue(nameof(BindingPath)) as string;
			set => SetPropertyValue(nameof(BindingPath), value);
		} // prop BindingPath
		  /// <summary>Factory</summary>
		public string UseFieldFactory
		{
			get => GetPropertyValue(nameof(UseFieldFactory)) as string;
			set => SetPropertyValue(nameof(UseFieldFactory), value);
		} // prop UseFieldFactory
	} // class PpsDataField

	#endregion
}
