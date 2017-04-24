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
	public sealed class PpsStaticCalculated : PpsDataRowExtentedValue, IPpsDataRowGetGenericValue
	{
		#region -- class SimpleRowTable ---------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class SimpleRowTable : LuaTable
		{
			private readonly LuaTable env;
			private readonly PpsDataRow row;

			public SimpleRowTable(PpsDataRow row)
			{
				this.env = row.Table.DataSet.Properties;
				this.row = row;
			} // ctor

			protected override object OnIndex(object key)
			{
				if (key is string)
				{
					var column = row.Table.TableDefinition.Columns[(string)key];
					if (column != null)
					{
						if (column.IsRelationColumn)
							return row.GetParentRow(column);
						else
							return row[column.Index];
					}
				}

				return env[key] ?? base.OnIndex(key);
			} // proc OnIndex
		} // class SimpleRowTable

		#endregion

		private readonly LuaChunk chunk;
		private object currentValue = null;

		public PpsStaticCalculated(PpsDataRow row, PpsDataColumnDefinition column)
			: base(row, column)
		{
			var code = column.Meta.GetProperty("formula", (String)null);

			// compile the code
			if (!String.IsNullOrEmpty(code))
			{
				// get lua engine
				var lua = column.Table.DataSet.Lua;
				chunk = lua.CompileChunk(code, column.Name + "-formula.lua", null);

				// register table changed, for the update
				row.Table.DataSet.DataChanged += (sender, e) => UpdateValue();
			}
		} // ctor

		protected override void Write(XElement x)
		{
			x.Add(new XElement("c", currentValue.ChangeType<string>()));
		} // proc Write

		protected override void Read(XElement x)
		{
			var t = x?.Element("c")?.Value;
			currentValue = t ?? Procs.ChangeType(t, Column.DataType);
		} // proc Read

		private void UpdateValue()
		{
			if (chunk == null)
				return;

			try
			{
				var r = chunk.Run(new SimpleRowTable(Row));
				if (!Object.Equals(currentValue, r[0]))
				{
					currentValue = r[0];
					OnPropertyChanged(nameof(Value));
				}
			}
			catch (Exception e)
			{
				System.Diagnostics.Debug.Print(e.ToString()); // todo:
			}
		} // proc UpdateValue
		
		public override bool IsNull => currentValue == null;
		/// <summary></summary>
		public object Value => currentValue;
	} // class PpsStaticCalculated

	#endregion

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Multi line text field, that supports a macro syntax.</summary>
	public sealed class PpsFormattedStringValue : PpsDataRowExtentedValue, IPpsDataRowSetGenericValue
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

		protected override void Write(XElement x)
		{
			x.Add(
				new XElement("v", value),
				new XElement("f", formattedValue)
			);
		} // proc Write

		protected override void Read(XElement x)
		{
			Value = x?.Element("v")?.Value;
		} // proc Read

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
