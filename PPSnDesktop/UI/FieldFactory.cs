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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Neo.IronLua;
using TecWare.DE.Stuff;
using TecWare.PPSn.Controls;
using TecWare.PPSn.Data;

namespace TecWare.PPSn.UI
{

	#region -- class PpsDataFieldFactory ----------------------------------------------

	/// <summary></summary>
	public sealed class PpsDataFieldFactory : LuaTable, IPpsDataFieldFactory
	{
		/// <summary></summary>
		/// <param name="env"></param>
		public PpsDataFieldFactory(PpsEnvironment env)
		{
			Environment = env;
		} // ctor

		/// <summary></summary>
		/// <param name="properties"></param>
		/// <returns></returns>
		public System.Xaml.XamlReader CreateField(IPpsDataFieldReadOnlyProperties properties)
		{
			// create controls
			if (!TryResolveFieldCreator(properties, out var controls))
			{
				var ctrl = CreateDefaultField(properties);

				// update display name
				if (ctrl[PpsDataFieldPanel.LabelProperty] == null
					&& properties.TryGetProperty<string>("displayName", out var displayName)
					&& !String.IsNullOrEmpty(displayName))
					ctrl[PpsDataFieldPanel.LabelProperty] = displayName + ":";

				controls = new LuaWpfCreator[] { ctrl };
			}

			// update local properties
			if (controls != null && controls.Length > 0)
			{
				foreach (var p in properties.LocalProperties())
				{
					foreach (var c in controls)
						c[p.Property] = p.Value;
				}
			}

			// emit code
			if (controls == null || controls.Length == 0)
				return null;
			else if (controls.Length == 1)
				return controls[0].CreateReader(properties.Context);
			else
				return LuaWpfCreator.CreateCollectionReader(from c in controls select c.CreateReader(properties.Context));
		} // func CreateField

		private bool TryResolveFieldCreator(IPpsDataFieldReadOnlyProperties properties, out LuaWpfCreator[] creator)
		{
			if (String.IsNullOrEmpty(properties.UseFieldFactory))
			{
				creator = null;
				return false;
			}

			// resolve complex fieldinformation within the Environment
			// the member must be registered within the table.
			var result = new LuaResult(CallMemberDirect(properties.UseFieldFactory, new object[] { properties }, rawGet: true, throwExceptions: true, ignoreNilFunction: true));
			if (result.Count == 0)
			{
				creator = null;
				return false;
			}
			else if (result.Count == 1 && result[0] is LuaWpfCreator r)
			{
				creator = new LuaWpfCreator[] { r };
				return true;
			}
			else if (result.Count == 1 && result[0] is LuaWpfCreator[] ra)
			{
				creator = ra;
				return true;
			}
			else if (result.Count > 1)
			{
				var wpfControls = new LuaWpfCreator[result.Count];
				for (var i = 0; i < wpfControls.Length; i++)
				{
					if (result[i] is LuaWpfCreator t)
						wpfControls[i] = t;
					else
						throw new ArgumentNullException(properties.UseFieldFactory, "Return type must be a control creator.");
				}
				creator = wpfControls;
				return true;
			}
			else
				throw new ArgumentNullException(properties.UseFieldFactory, "Return type must be a control creator.");
		} // func TryResolveFieldCreator

		[LuaMember]
		private double GetWidth(double w)
			=> w * 10;

		[LuaMember]
		private double GetHeight(double h)
			=> h * 23;

		[LuaMember]
		private DependencyProperty GridLabel => PpsDataFieldPanel.LabelProperty;

		[LuaMember]
		private DependencyProperty GridLines => PpsDataFieldPanel.GridLinesProperty;

		[LuaMember]
		private object GetCode(IServiceProvider context)
			=> context?.GetService<IPpsXamlCode>(false);

		[LuaMember]
		private PpsDataFieldInfo GetFieldInfo(IServiceProvider context, string fieldName, bool throwException = true)
			=> PpsDataFieldInfo.GetDataFieldInfo(context, fieldName, throwException);

		[LuaMember]
		private PpsDataFieldInfo CreateFieldInfo(string fieldName, Type dataType, string bindingPath, object properties)
			=> new PpsDataFieldInfo(fieldName, dataType, bindingPath, Procs.ToProperties(properties));

		[LuaMember]
		private LuaWpfCreator CreateFieldBinding(IPpsDataFieldReadOnlyProperties properties, bool? isReadOnly = null, string append = null)
			=> PpsDataFieldBinding.CreateWpfBinding(properties.GetService<PpsDataFieldInfo>(true), isReadOnly, append);

		[LuaMember]
		private LuaWpfCreator CreateDefaultField(IPpsDataFieldReadOnlyProperties properties)
		{
			// test for creator
			if (properties.DataType == typeof(string)
				|| properties.DataType == typeof(int)
				|| properties.DataType == typeof(float)
				|| properties.DataType == typeof(double)
				|| properties.DataType == typeof(decimal)
				|| properties.DataType == typeof(DateTime))
				return CreateTextField(properties);
			else if (properties.DataType == typeof(bool))
				return CreateCheckField(properties);
			else if (properties.DataType == typeof(PpsMasterDataExtendedValue))
			{
				// test for master table
				if (properties.TryGetProperty<string>("refTable", out var refTable))
					return CreateMasterDataField(properties, refTable);
				else
					throw new ArgumentNullException("refTable", "refTable is null.");
			}
			else if (properties.DataType == typeof(PpsFormattedStringValue))
				return CreateTextField(properties, true);
			else
			{
				var columnDefinition = properties.GetService<PpsDataColumnDefinition>(false);
				if (columnDefinition?.IsRelationColumn ?? false)
					return CreateRelationField(properties, columnDefinition);
				else
					throw new NotImplementedException();
			}
		} // func CreateDefaultField

		private static void SetNumericBinding(dynamic ui, dynamic textBox, dynamic textBinding, bool allowNeg, int floatDigits)
		{
			textBinding.Converter = PpsConverter.NumericValue;
			textBinding.ConverterParameter = new NumericValueConverterParameter() { AllowNeg = allowNeg, FloatDigits = floatDigits };
		} // proc SetNumericBinding

		private static void SetDateBinding(dynamic ui, dynamic textBox, dynamic textBinding, bool allowNeg, int floatDigits)
		{
			textBinding.Converter = PpsConverter.DateValue;
			textBinding.ConverterParameter = DateValueConverterParameter.Default;
		} // proc SetNumericBinding

		private void SetTextFieldProperties(dynamic ctrl, IPpsDataFieldReadOnlyProperties properties)
		{
			if (properties.TryGetProperty<double>("TextHeight", out var height))
			{
				ctrl.Height = GetHeight(height);
				ctrl.VerticalAlignment = VerticalAlignment.Top;
			}
			else
				ctrl.VerticalAlignment = VerticalAlignment.Top;

			if (properties.TryGetProperty<double>("TextWidth", out var width))
			{
				ctrl.Width = GetWidth(width);
				ctrl.HorizontalAlignment = HorizontalAlignment.Left;
			}
			else
				ctrl.HorizontalAlignment = HorizontalAlignment.Stretch;
		} // proc SetTextFieldProperties

		[LuaMember]
		private LuaWpfCreator CreateTextField(IPpsDataFieldReadOnlyProperties properties, bool formattedText = false)
		{
			var ui = new LuaUI();
			dynamic txt = LuaWpfCreator.CreateFactory(ui, typeof(PpsTextBox));

			var isReadOnly = properties.TryGetProperty<bool>("IsReadOnly", out var tmpReadOnly) ? (bool?)tmpReadOnly : null;

			var textBinding = PpsDataFieldBinding.CreateWpfBinding(properties.GetService<PpsDataFieldInfo>(true), append: formattedText ? "Value" : null, isReadOnly: isReadOnly);

			var inputType = formattedText ? PpsTextBoxInputType.MultiLine : PpsTextBoxInputType.None;
			var setMaxLength = true;
			switch (Type.GetTypeCode(properties.DataType))
			{
				case TypeCode.Decimal:
					PpsDataFieldFactory.SetNumericBinding(ui, txt, textBinding, true, 2);
					inputType = PpsTextBoxInputType.DecimalNegative;
					setMaxLength = false;
					break;
				case TypeCode.Single:
					PpsDataFieldFactory.SetNumericBinding(ui, txt, textBinding, true, 3);
					inputType = PpsTextBoxInputType.DecimalNegative;
					setMaxLength = false;
					break;
				case TypeCode.Double:
					PpsDataFieldFactory.SetNumericBinding(ui, txt, textBinding, true, 6);
					inputType = PpsTextBoxInputType.DecimalNegative;
					setMaxLength = false;
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

				case TypeCode.DateTime:
					if (inputType != PpsTextBoxInputType.Date
						&& inputType != PpsTextBoxInputType.Time)
						inputType = PpsTextBoxInputType.Date;

					if (inputType == PpsTextBoxInputType.Date) // we can set a binding converter
						PpsDataFieldFactory.SetDateBinding(ui, txt, textBinding, false, 0);
					break;
			}

			if (properties.TryGetProperty<PpsTextBoxInputType>("InputType", out var tmpInputType))
				inputType = tmpInputType;

			txt.InputType = inputType;
			txt.Text = textBinding;

			SetTextFieldProperties((object)txt, properties);

			if (setMaxLength && properties.TryGetProperty<int>("MaxLength", out var maxInputLength))
				txt.MaxLength = maxInputLength;

			if (properties.TryGetProperty<bool>("Nullable", out var tmpNullable))
				txt.IsNullable = tmpNullable;

			if (isReadOnly.HasValue)
				txt.IsReadOnly = isReadOnly;

			if (formattedText)
				txt.FormattedValue = PpsDataFieldBinding.CreateWpfBinding(properties.GetService<PpsDataFieldInfo>(true), append: "FormattedValue", isReadOnly: true);

			return txt;
		} // func CreateTextField

		private LuaWpfCreator CreateSelector(IPpsDataFieldReadOnlyProperties properties)
		{
			dynamic combobox = LuaWpfCreator.CreateFactory(new LuaUI(), typeof(PpsComboBox));

			if (properties.TryGetProperty<bool>("Nullable", out var tmpNullable))
				combobox.IsNullable = tmpNullable;

			SetTextFieldProperties((object)combobox, properties);

			return combobox;
		} // func CreateSelector

		[LuaMember]
		private LuaWpfCreator CreateMasterDataField(IPpsDataFieldReadOnlyProperties properties, string refTableName)
		{
			dynamic combobox = CreateSelector(properties);

			var table = Environment.MasterData.GetTable(refTableName, true);

			combobox.ItemsSource = table;
			combobox.SelectedValue = PpsDataFieldBinding.CreateWpfBinding(properties.GetService<PpsDataFieldInfo>());

			if (properties.TryGetProperty("TemplateResourceKey", out var templateResource)
				|| table.Definition.Meta.TryGetProperty("Wpf.TemplateResourceKey", out templateResource))
			{
				combobox.ItemTemplate = Environment.FindResource<DataTemplate>(templateResource);

				//if (properties.TryGetProperty("SelectedValueTemplateResourceKey", out var selectedValueResourceKey)
				//	|| table.Definition.Meta.TryGetProperty("Wpf.SelectedValueTemplateResourceKey", out selectedValueResourceKey))
				//	combobox.SelectedValueTemplate = Environment.FindResource<DataTemplate>(selectedValueResourceKey);
				//else
				//	combobox.SelectedValueTemplate = combobox.ItemTemplate;
			}

			return combobox;
		} // func CreateMasterDataField

		[LuaMember]
		private LuaWpfCreator CreateRelationField(IPpsDataFieldReadOnlyProperties properties, PpsDataColumnDefinition columnDefinition)
		{
			dynamic combobox = CreateSelector(properties);

			// bind items source
			if (!properties.TryGetProperty<string>("TableBindingPath", out var baseBindingPath))
				baseBindingPath = properties.GetService<PpsDataSetResolver>(true).BindingPath;

			var codeBase = properties.GetService<IPpsXamlCode>(true);

			dynamic itemsSourceBinding = LuaWpfCreator.CreateFactory(new LuaUI(), typeof(Binding));
			itemsSourceBinding.Path = PpsDataFieldBinding.CombinePath(baseBindingPath, columnDefinition.ParentColumn.Table.Name);
			itemsSourceBinding.Source = codeBase;
			combobox.ItemsSource = itemsSourceBinding;

			// bind value
			combobox.SelectedValue = PpsDataFieldBinding.CreateWpfBinding(properties.GetService<PpsDataFieldInfo>(true));

			return combobox;
		} // func CreateRelationField

		[LuaMember]
		private static LuaWpfCreator CreateComboField(IPpsDataFieldReadOnlyProperties properties)
		{
			dynamic ui = new LuaUI();
			//dynamic combo = LuaWpfCreator.CreateFactory(ui, typeof(ComboBox));
			dynamic combo = LuaWpfCreator.CreateFactory(ui, typeof(PpsComboBox));

			combo.SelectedValue = PpsDataFieldBinding.CreateWpfBinding(properties.GetService<PpsDataFieldInfo>(true));

			return combo;
		} // func CreateComboField

		[LuaMember]
		private LuaWpfCreator CreateCheckField(IPpsDataFieldReadOnlyProperties properties)
		{
			dynamic ui = new LuaUI();
			dynamic check = LuaWpfCreator.CreateFactory(ui, typeof(CheckBox));

			//check.Content = properties.displayName;
			check.IsThreeState = false;
			check.IsChecked = PpsDataFieldBinding.CreateWpfBinding(properties.GetService<PpsDataFieldInfo>(true));

			return check;
		} // func CreateCheckField

		/// <summary></summary>
		[LuaMember]
		public PpsEnvironment Environment { get; }
	} // class PpsDataFieldFactory

	#endregion
}
