using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Stuff;

namespace TecWare.PPSn.Data
{
	#region -- class PpsStaticCalculated ------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>The formular to get the result is definied in the column.</summary>
	public sealed class PpsStaticCalculated : PpsDataRowExtentedValue
	{
		private object currentValue = null;
		// private readonly LuaChunk chunk;

		public PpsStaticCalculated(PpsDataRow row, PpsDataColumnDefinition column)
			: base(row, column)
		{

			// var code = column.Meta.GetProperty("formula", (String)null);

			// row.Table.DataSet.Properties.

			// row.Table.DataSet.OnTableChanged
		} // ctor

		private void UpdateValue()
		{
		} // proc UpdateValue

		/// <summary>Do persist the calculated value.</summary>
		public override XElement CoreData
		{
			get
			{
				return new XElement("c", currentValue.ChangeType<string>());
			}
			set
			{
				var t = value?.Element("c")?.Value;
				currentValue = t != null ? t : Procs.ChangeType(t, Column.DataType);
			}
		} // prop CoreData

		public override bool IsNull => currentValue == null;
	} // class PpsStaticCalculated

	#endregion

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Multi line text field, that supports a macro syntax.</summary>
	public sealed class PpsFormattedStringValue : PpsDataRowExtentedValue, IPpsDataRowGenericValue
	{
		private string value;
		private string formattedValue;

		private PropertyChangedEventHandler rowChangeHandler;
		private bool changeHandlerAssigned = false;

		public PpsFormattedStringValue(PpsDataRow row, PpsDataColumnDefinition column, string value = null)
			: base(row, column)
		{
			this.value = value;
			this.rowChangeHandler = (sender, e) => UpdateFormattedValue(); // holzhammer

			UpdateFormattedValue();
		} // ctor

		public override string ToString()
			=> value;
			
		private void UpdateFormattedValue()
		{
			if (value == null)
			{
				if (changeHandlerAssigned)
				{
					Row.PropertyChanged -= rowChangeHandler;
					changeHandlerAssigned = false;
				}
				formattedValue = value;
			}
			else
			{
				if (!changeHandlerAssigned)
				{
					Row.PropertyChanged += rowChangeHandler;
					changeHandlerAssigned = true;

				}
				var r = new Regex("\\%\\%(\\w+)\\%\\%"); // todo:
				formattedValue = r.Replace(value, m => Row.GetProperty(m.Groups[1].Value, String.Empty));
			}
		} // proc UpdateFormattedValue

		public bool SetGenericValue(bool inital, object value)
		{
			Value = value?.ToString();
			return true;
		} // proc SetGenericValue

		public override XElement CoreData
		{
			get
			{
				return new XElement("fv",
					new XElement("v", value),
					new XElement("f", formattedValue)
				);
			}
			set
			{
				Value = value?.Element("fv")?.Element("v")?.Value;
			}
		} // prop CoreData
		
		/// <summary>Set/Get the unformatted value.</summary>
		public string Value
		{
			get { return value; }
			set
			{
				if (this.value != value)
				{
					// todo: Undo-Operation missing
					this.value = value;
					OnPropertyChanged(nameof(Value));
					UpdateFormattedValue();
				}
			}
		} // prop Value

		/// <summary>Is the core value null.</summary>
		public override bool IsNull => String.IsNullOrEmpty(value);

		/// <summary>Returns the formatted value.</summary>
		public string FormattedValue => formattedValue;
	} // struct PpsFormattedStringValue
}
