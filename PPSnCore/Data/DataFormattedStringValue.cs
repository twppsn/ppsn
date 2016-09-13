using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

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
	public sealed class PpsFormattedStringValue : IPpsDataRowExtendedValue, IPpsDataRowGenericValue, INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler PropertyChanged;

		private readonly string value;
		private string formattedValue;

		public PpsFormattedStringValue()
			: this(null)
		{
		} // ctor

		public PpsFormattedStringValue(string value)
		{
			this.value = value;
			UpdateFormattedValue();
		} // ctor

		public override string ToString()
			=> value;

		private void UpdateFormattedValue()
		{
		} // proc UpdateFormattedValue

		public bool SetGenericValue(bool inital, object value)
		{
			throw new NotImplementedException();
		}

		public bool IsNull
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		public string FormattedValue => formattedValue;

		public XElement CoreData
		{
			get
			{
				throw new NotImplementedException();
			}

			set
			{
				throw new NotImplementedException();
			}
		}
	} // struct PpsFormattedStringValue
}
