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
using System.Globalization;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Stuff;

namespace TecWare.PPSn.Data
{
	#region -- class PpsLuaRowEnvironment ---------------------------------------------

	/// <summary>Special environment, that gives access of a row for formulas.</summary>
	public sealed class PpsLuaRowEnvironment : LuaTable
	{
		private readonly LuaTable env;
		private readonly PpsDataRow row;

		public PpsLuaRowEnvironment(LuaTable env, PpsDataRow row)
		{
			this.env = env;
			this.row = row;
		} // ctor

		protected override object OnIndex(object key)
		{
			if (key is string stringKey)
			{
				var column = row.Table.TableDefinition.Columns[stringKey];
				if (column != null)
				{
					if (column.IsRelationColumn)
						return row.GetParentRow(column);
					else
						return row[column.Index];
				}
				else if (row.Table.TableDefinition.Relations[stringKey] is PpsDataTableRelationDefinition relation)
				{
					return row.GetDefaultRelation(relation);
				}
			}

			return env[key] ?? base.OnIndex(key);
		} // proc OnIndex
	} // class PpsLuaRowEnvironment

	#endregion

	#region -- class PpsStaticCalculated ----------------------------------------------

	/// <summary>The formular to get the result is definied in the column.</summary>
	public sealed class PpsStaticCalculated : PpsDataRowExtentedValue, IPpsDataRowGetGenericValue
	{
		private readonly LuaChunk chunk;
		private object currentValue = null;

		public PpsStaticCalculated(PpsDataRow row, PpsDataColumnDefinition column)
			: base(row, column)
		{
			var code = column.Meta.GetProperty("formula", (string)null);

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
				var r = chunk.Run(new PpsLuaRowEnvironment(Row.Table.DataSet.Properties, Row));
				if (!Object.Equals(currentValue, r[0]))
				{
					var oldValue = currentValue;
					currentValue = r[0];
					OnPropertyChanged(nameof(Value), oldValue, currentValue, true);
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

	#region -- class PpsFormattedStringValue ------------------------------------------

	/// <summary>Multiline text field, that supports a macro syntax.</summary>
	public sealed class PpsFormattedStringValue : PpsDataRowExtentedValue, IPpsDataRowSetGenericValue
	{
		#region -- class ExpressionBlock ----------------------------------------------

		public abstract class ExpressionBlock
		{
			public sealed override string ToString()
				=> GetType().Name;

			public abstract string ToString(PpsLuaRowEnvironment env);
		} // class ExpressionBlock

		#endregion

		#region -- class StringBlock --------------------------------------------------

		private sealed class StringBlock : ExpressionBlock
		{
			private readonly string part;

			public StringBlock(string part)
				=> this.part = part;

			public override string ToString(PpsLuaRowEnvironment env) 
				=> part;
		} // class StringBlock

		#endregion

		#region -- class LuaCodeBlock -------------------------------------------------

		private sealed class LuaCodeBlock : ExpressionBlock
		{
			private readonly LuaChunk chunk;
			private readonly string fmt;

			public LuaCodeBlock(LuaChunk chunk, string fmt)
			{
				this.chunk = chunk;
				this.fmt = fmt;
			} // ctor

			public override string ToString(PpsLuaRowEnvironment env)
			{
				try
				{
					var r = chunk.Run(env)[0];
					if (r == null)
						return String.Empty;
					else if (fmt == null)
						return r.ToString();
					else
					{
						switch (Type.GetTypeCode(r.GetType()))
						{
							case TypeCode.SByte:
							case TypeCode.Byte:
							case TypeCode.Int16:
							case TypeCode.UInt16:
							case TypeCode.Int32:
							case TypeCode.UInt32:
							case TypeCode.Int64:
							case TypeCode.UInt64:
								return r.ChangeType<long>().ToString(fmt);
							case TypeCode.DateTime:
								return ((DateTime)r).ToString(fmt);
							case TypeCode.Decimal:
								return ((decimal)r).ToString(fmt);
							case TypeCode.Double:
								return ((double)r).ToString(fmt);
							case TypeCode.Single:
								return ((float)r).ToString(fmt);

							default:
								if (r is IFormattable f)
									return f.ToString(fmt, CultureInfo.CurrentCulture);
								else
									return r.ToString();
						}
					}
				}
				catch (LuaRuntimeException e)
				{
					return "{RE=" + e.Message + "}";
				}
				catch (Exception e)
				{
					return "{RE=" + e.Message + "}";
				}
			} // proc ToString
		} // class LuaCodeBlock

		#endregion

		private object originalTemplate;
		private object currentTemplate;
		private ExpressionBlock[] parsedValue = null;
		private string formattedValue; // cache for the formatted value

		#region -- Ctor/Dtor ----------------------------------------------------------

		public PpsFormattedStringValue(PpsDataRow row, PpsDataColumnDefinition column) 
			: base(row, column)
		{
		} // ctor

		public override string ToString()
			=> Value;

		#endregion

		#region -- Read/Write ---------------------------------------------------------

		protected override void Read(XElement x)
		{
			originalTemplate= x.Element("o")?.Value;
			currentTemplate = x.Element("c")?.Value ?? PpsDataRow.NotSet;
			formattedValue = x.Element("f")?.Value;

			parsedValue = null; // invalidate the template
		} // proc Read

		protected override void Write(XElement x)
		{
			if (originalTemplate != null)
				x.Add(new XElement("o", originalTemplate));
			if (currentTemplate != PpsDataRow.NotSet)
				x.Add(new XElement("c", currentTemplate));
			if (formattedValue != null)
				x.Add(new XElement("f", formattedValue));
		} // proc Write

		#endregion

		#region -- Get/Set-Value ------------------------------------------------------

		#region -- class PpsTemplateUndoItem ------------------------------------------

		private sealed class PpsTemplateUndoItem : IPpsUndoItem
		{
			private readonly PpsFormattedStringValue value;
			private readonly object oldValue;
			private readonly object newValue;

			public PpsTemplateUndoItem(PpsFormattedStringValue value, object oldValue, object newValue)
			{
				this.value = value;
				this.oldValue = oldValue;
				this.newValue = newValue;
			} // ctor

			public void Freeze() { }

			public void Undo()
				=> value.SetValue(oldValue, true, false);

			public void Redo()
				=> value.SetValue(newValue, true, false);
		} // class PpsTemplateUndoItem

		#endregion

		private void SetValue(object newValue, bool firePropertyChanged, bool addUndo)
		{
			var valueChanged = false;

			var oldValue = currentTemplate;
			if (newValue == PpsDataRow.NotSet && currentTemplate != PpsDataRow.NotSet)
			{
				currentTemplate = PpsDataRow.NotSet;
				parsedValue = null;
				valueChanged = true;
			}
			else if(!Object.Equals(Value, newValue))
			{
				currentTemplate = newValue;
				parsedValue = null;
				valueChanged = true;
			}

			// undo stack
			if (valueChanged && addUndo)
			{
				using (var undo = Row.Table.DataSet.UndoSink?.BeginTransaction("Ändere Wert"))
				{
					Row.Table.DataSet.UndoSink?.Append(
						new PpsTemplateUndoItem(this, oldValue, newValue)
					);
					undo?.Commit();
				}
			}

			// refresh values
			OnPropertyChanged(nameof(Value), oldValue, currentTemplate, true);
			if (firePropertyChanged && valueChanged)
				UpdateFormattedValue();
		} // proc SetValue

		bool IPpsDataRowSetGenericValue.SetGenericValue(bool inital, object value)
		{
			SetValue(value, !inital, !inital);
			return false;
		} // proc IPpsDataRowSetGenericValue.SetGenericValue

		void IPpsDataRowSetGenericValue.Commit()
		{
			// set original
			if (currentTemplate != PpsDataRow.NotSet)
			{
				originalTemplate = currentTemplate;
				currentTemplate = PpsDataRow.NotSet;
			}
		} // proc IPpsDataRowSetGenericValue.Commit

		void IPpsDataRowSetGenericValue.Reset()
		{
			// clear current
			SetValue(PpsDataRow.NotSet, true, true);

			// refresh formatted value
			parsedValue = null;
			UpdateFormattedValue();
		} // proc IPpsDataRowSetGenericValue.Reset

		#endregion

		#region -- ParseTemplate ------------------------------------------------------

		private ExpressionBlock CreateExpressionBlock(string expr, string fmt)
		{
			// all is lua, currently
			try
			{
				return new LuaCodeBlock(Row.Table.DataSet.DataSetDefinition.Lua.CompileChunk(expr, Column.Name + "-fmt.lua", null), fmt);
			}
			catch (LuaParseException e)
			{
				return new StringBlock("{PE=" + e.Message + "}");
			}
		} // func CreateExpressionBlock

		public static ExpressionBlock[] ParseTemplate(string template, Func<string, string, ExpressionBlock> createExpressionBlock)
		{
			var spans = new List<ExpressionBlock>(); // result

			var p = 0;
			var s = 0;
			var startAt = 0;
			var formatAt = -1;

			void EmitTextBlock(int endAt)
			{
				spans.Add(new StringBlock(template.Substring(startAt, endAt - startAt)));

				startAt = endAt + 2;
				formatAt = -1;
			} // func EmitTextBlock

			void EmitExprBlock(int endAt)
			{
				var expr = template.Substring(startAt,( formatAt == -1 ? endAt : formatAt) - startAt);
				var fmt = formatAt == -1 ? null : template.Substring(formatAt + 2, endAt - formatAt - 2);

				spans.Add(createExpressionBlock(expr.Trim(), fmt?.Trim()));

				startAt = endAt + 2;
				formatAt = -1;
			} // func EmitExprBlock

			while (p < template.Length)
			{
				var c = template[p];
				switch (s)
				{
					case 0:
						if (c == '%')
							s = 1;
						break; // eat all chars
					case 1: // check for secound %
						if (c == '%')
						{
							EmitTextBlock(p - 1);
							s = 2; // jump to code
						}
						else
							s = 0;
						break;
					case 2: // collect to format or end
						if (c == '%')
							s = 3;
						else if (c == ':')
							s = 4;
						break; // eat for code block
					case 3: // check for end of code
						if (c == '%')
						{
							EmitExprBlock(p - 1);
							s = 0;
						}
						else
							s = 2;
						break;
					case 4: // start of format
						if (c == ':')
						{
							formatAt = p -1;
							s = 5;
						}
						else
							s = 2;
						break;
					case 5: // collect format
						if (c == '%')
							s = 6;
						break;
					case 6: // end of format
						if (c == '%')
						{
							EmitExprBlock(p - 1);
							s = 0;
						}
						else
							s = 5;
						break;
					default:
						throw new InvalidOperationException();
				}
				
				p++;
			}

			if (spans.Count > 0)
				EmitTextBlock(p);
			
			return spans.ToArray();
		} // func ParseTemplate

		private void ParseTemplate(bool force)
		{
			// '%%' EXPR '::' FORMAT '%%'
			// EXPR ::= IDENTIFIER.IDENTIFIER.IDENTIFIER
			// EXPR ::= luacode?

			if (!force && parsedValue != null)
				return;

			parsedValue = ParseTemplate(Value, CreateExpressionBlock);
			if (parsedValue.Length == 0)
				Row.Table.DataSet.DataChanged -= DataSetDataChanged;
			else
				Row.Table.DataSet.DataChanged += DataSetDataChanged;
		} // proc ParseTemplate

		private void DataSetDataChanged(object sender, EventArgs e)
			=> UpdateFormattedValue();

		#endregion

		private void UpdateFormattedValue()
		{
			ParseTemplate(false);

			var newFormattedValue = String.Empty;
			if (parsedValue.Length == 0) // no formatting
				newFormattedValue = Value;
			else
			{
				var parts = new string[parsedValue.Length];
				for (var i = 0; i < parsedValue.Length; i++)
					parts[i] = parsedValue[i].ToString(new PpsLuaRowEnvironment(Row.Table.DataSet.Properties, Row));
				newFormattedValue = String.Concat(parts);
			}

			if(newFormattedValue != formattedValue)
			{
				var oldValue = formattedValue;
				formattedValue = newFormattedValue;
				OnPropertyChanged(nameof(FormattedValue), oldValue, formattedValue, true);
			}
		} // proc UpdateFormattedValue

		public string FormattedValue
			=> formattedValue;

		public string Value
		{
			get { return currentTemplate != PpsDataRow.NotSet ? (string)currentTemplate : (string)originalTemplate; }
			set { SetValue(value, true, true); }
		} // proc Template

		public override bool IsNull => currentTemplate == PpsDataRow.NotSet ? originalTemplate == null : currentTemplate == null;

		public bool IsValueModified => currentTemplate != PpsDataRow.NotSet;

		/// <summary>Return the formatted value.</summary>
		/// <param name="v"></param>
		public static explicit operator string(PpsFormattedStringValue v)
			=> v.FormattedValue;
	} // struct PpsFormattedStringValue

	#endregion
}
