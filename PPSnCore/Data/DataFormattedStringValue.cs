using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using TecWare.DE.Stuff;

namespace TecWare.PPSn.Data
{
	#region -- class PpsStaticCalculated ------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsStaticCalculated : IPpsDataRowExtendedValue, INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler PropertyChanged;

		private readonly PpsDataRow row;
		private readonly PpsDataColumnDefinition column;

		public PpsStaticCalculated(PpsDataColumnDefinition column, PpsDataRow row)
		{
			this.row = row;
			this.column = column;
		} // ctor

		public XElement CoreData
		{
			get { return null; }
			set { }
		} // prop CoreData

		/// <summary>Always <c>true</c>, there is no value to persist.</summary>
		public bool IsNull => true;
	} // class PpsStaticCalculated

	#endregion

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
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
