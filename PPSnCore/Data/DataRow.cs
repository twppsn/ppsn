using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using TecWare.DES.Stuff;

namespace TecWare.PPSn.Data
{
	#region -- enum PpsDataRowState -----------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Status der Zeile.</summary>
	public enum PpsDataRowState
	{
		/// <summary>Invalid row state</summary>
		Unknown = -1,
		/// <summary>The row is unchanged</summary>
		Unchanged = 0,
		/// <summaryThe row is modified</summary>
		Modified = 1,
		/// <summary>The row is deleted</summary>
		Deleted = 2
	} // enum PpsDataRowState

	#endregion

	#region -- class PpsDataRow ---------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PpsDataRow : IDynamicMetaObjectProvider, INotifyPropertyChanged
	{
		#region -- class NotSetValue ------------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary>Interne Klasse für Current-Value die anzeigt, ob sich ein Wert zum Original hin geändert hat.</summary>
		private sealed class NotSetValue
		{
			public override string ToString()
			{
				return "NotSet";
			} // func ToString
		} // class NotSetValue

		#endregion

		#region -- class PpsDataRowValueChangedItem ---------------------------------------

		private class PpsDataRowValueChangedItem : IPpsUndoItem
		{
			private PpsDataRow row;
			private int columnIndex;
			private object oldValue;
			private object newValue;

			public PpsDataRowValueChangedItem(PpsDataRow row, int columnIndex, object oldValue, object newValue)
			{
				this.row = row;
				this.columnIndex = columnIndex;
				this.oldValue = oldValue;
				this.newValue = newValue;
			} // ctor

			public override string ToString()
			{
				return String.Format("Undo ColumnChanged: {0}", columnIndex);
			} // func ToString

			private object GetOldValue()
			{
				return oldValue == NotSet ? row.originalValues[columnIndex] : oldValue;
			} // func GetOldValue

			public void Undo()
			{
				row.currentValues[columnIndex] = oldValue;
				row.OnValueChanged(columnIndex, newValue, GetOldValue());
			} // proc Undo

			public void Redo()
			{
				row.currentValues[columnIndex] = newValue;
				row.OnValueChanged(columnIndex, GetOldValue(), newValue);
			} // proc Redo
		} // class PpsDataRowValueChangedItem

		#endregion

		#region -- class PpsDataRowStateChangedItem ---------------------------------------

		private class PpsDataRowStateChangedItem : IPpsUndoItem
		{
			private PpsDataRow row;
			private PpsDataRowState oldValue;
			private PpsDataRowState newValue;

			public PpsDataRowStateChangedItem(PpsDataRow row, PpsDataRowState oldValue, PpsDataRowState newValue)
			{
				this.row = row;
				this.oldValue = oldValue;
				this.newValue = newValue;
			} // ctor

			public override string ToString()
			{
				return String.Format("Undo RowState: {0} -> {1}", oldValue, newValue);
			} // func ToString

			public void Undo()
			{
				row.RowState = oldValue;
			} // proc Undo

			public void Redo()
			{
				row.RowState = newValue;
			} // proc Redo
		} // class PpsDataRowStateChangedItem

		#endregion

		#region -- class PpsDataRowMetaObject ---------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private abstract class PpsDataRowBaseMetaObject : DynamicMetaObject
		{
			public PpsDataRowBaseMetaObject(Expression expression, object value)
				: base(expression, BindingRestrictions.Empty, value)
			{
			} // ctor

			private BindingRestrictions GetRestriction()
			{
				Expression expr;
				Expression exprType;
				if (ItemInfo.DeclaringType == typeof(PpsDataRow))
				{
					expr = Expression.Convert(Expression, typeof(PpsDataRow));
					exprType = Expression.TypeIs(Expression, typeof(PpsDataRow));
				}
				else
				{
					expr = Expression.Field(Expression.Convert(Expression, ItemInfo.DeclaringType), RowFieldInfo);
					exprType = Expression.TypeIs(Expression, typeof(RowValues));
				}

				expr =
					Expression.AndAlso(
						exprType,
						Expression.Equal(
							Expression.Property(Expression.Field(expr, TableFieldInfo), PpsDataTable.TableDefinitionPropertyInfo),
							Expression.Constant(Row.table.TableDefinition, typeof(PpsDataTableDefinition))
						)
					);

				return BindingRestrictions.GetExpressionRestriction(expr);
			} // func GetRestriction

			private Expression GetIndexExpression(int iColumnIndex)
			{
				return Expression.MakeIndex(
					Expression.Convert(Expression, ItemInfo.DeclaringType),
					ItemInfo,
					new Expression[] { Expression.Constant(iColumnIndex) }
				);
			} // func GetIndexExpression

			private static bool IsStandardMember(string sMemberName)
			{
				return String.Compare(sMemberName, RowStatePropertyInfo.Name, StringComparison.OrdinalIgnoreCase) == 0 ||
					String.Compare(sMemberName, CurrentPropertyInfo.Name, StringComparison.OrdinalIgnoreCase) == 0 ||
					String.Compare(sMemberName, OriginalPropertyInfo.Name, StringComparison.OrdinalIgnoreCase) == 0;
			} // func IsStandardMember

			public override DynamicMetaObject BindGetMember(GetMemberBinder binder)
			{
				if (IsStandardMember(binder.Name))
					return base.BindGetMember(binder);

				int iColumnIndex = Row.table.TableDefinition.FindColumnIndex(binder.Name);
				if (iColumnIndex >= 0)
					return new DynamicMetaObject(GetIndexExpression(iColumnIndex), GetRestriction());
				else
					return new DynamicMetaObject(Expression.Constant(null, typeof(object)), GetRestriction());
			} // func BindGetMember

			public override DynamicMetaObject BindSetMember(SetMemberBinder binder, DynamicMetaObject value)
			{
				if (IsStandardMember(binder.Name))
					return base.BindSetMember(binder, value);

				int iColumnIndex = Row.table.TableDefinition.FindColumnIndex(binder.Name);
				if (iColumnIndex >= 0)
				{
					return new DynamicMetaObject(
						Expression.Assign(GetIndexExpression(iColumnIndex), Expression.Convert(value.Expression, typeof(object))),
						GetRestriction().Merge(value.Restrictions)
					);
				}
				else
					return new DynamicMetaObject(Expression.Empty(), GetRestriction());
			} // func BindSetMember

			public override IEnumerable<string> GetDynamicMemberNames()
			{
				foreach (var col in Row.table.Columns)
					yield return col.Name;
			} // func GetDynamicMemberNames

			protected abstract PpsDataRow Row { get; }
			protected abstract PropertyInfo ItemInfo { get; }
		} // class PpsDataRowMetaObject

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class PpsDataRowMetaObject : PpsDataRowBaseMetaObject
		{
			public PpsDataRowMetaObject(Expression expression, object value)
				: base(expression, value)
			{
			} // ctor

			protected override PpsDataRow Row { get { return (PpsDataRow)Value; } }
			protected override PropertyInfo ItemInfo { get { return ItemPropertyInfo; } }
		} // class PpsDataRowMetaObject

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class PpsDataRowValuesMetaObject : PpsDataRowBaseMetaObject
		{
			public PpsDataRowValuesMetaObject(Expression expression, object value)
				: base(expression, value)
			{
			} // ctor

			protected override PpsDataRow Row { get { return ((RowValues)Value).Row; } }
			protected override PropertyInfo ItemInfo { get { return ValuesPropertyInfo; } }
		} // class PpsDataRowValuesMetaObject

		#endregion

		#region -- class RowValues --------------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		public abstract class RowValues : IDynamicMetaObjectProvider
		{
			private PpsDataRow row;

			#region -- Ctor/Dtor ------------------------------------------------------------

			protected RowValues(PpsDataRow row)
			{
				this.row = row;
			} // ctor

			/// <summary></summary>
			/// <param name="parameter"></param>
			/// <returns></returns>
			public DynamicMetaObject GetMetaObject(Expression parameter)
			{
				return new PpsDataRowValuesMetaObject(parameter, this);
			} // func GetMetaObject

			#endregion

			/// <summary>Ermöglicht den Zugriff auf die Spalte.</summary>
			/// <param name="columnIndex">Index der Spalte</param>
			/// <returns>Wert in der Spalte</returns>
			public abstract object this[int iColumnIndex] { get; set; }

			/// <summary>Ermöglicht den Zugriff auf die Spalte.</summary>
			/// <param name="columnName">Name der Spalte</param>
			/// <returns>Wert in der Spalte</returns>
			public object this[string sColumnName]
			{
				get { return this[Row.table.TableDefinition.FindColumnIndex(sColumnName, true)]; }
				set { this[Row.table.TableDefinition.FindColumnIndex(sColumnName, true)] = value; }
			} // prop this

			/// <summary>Zugriff auf die Datenzeile.</summary>
			protected internal PpsDataRow Row { get { return row; } }
		} // class RowValues

		#endregion

		#region -- class OriginalRowValues ------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class OriginalRowValues : RowValues
		{
			public OriginalRowValues(PpsDataRow row)
				: base(row)
			{
			} // ctor

			public override object this[int iColumnIndex]
			{
				get { return Row.originalValues[iColumnIndex]; }
				set { throw new NotSupportedException(); }
			} // prop this
		} // class OriginalRowValues

		#endregion

		#region -- class CurrentRowValues -------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class CurrentRowValues : RowValues, INotifyPropertyChanged
		{
			public CurrentRowValues(PpsDataRow row)
				: base(row)
			{
			} // ctor

			public override object this[int iColumnIndex]
			{
				get
				{
					object currentValue = Row.currentValues[iColumnIndex];
					return currentValue == NotSet ? Row.originalValues[iColumnIndex] : currentValue;
				}
				set
				{
					// Konvertiere als erstes den Wert
					value = Procs.ChangeType(value, Row.table.TableDefinition.Columns[iColumnIndex].DataType);

					// Prüfe die Änderung
					object oldValue = this[iColumnIndex];
					if (!Object.Equals(oldValue, value))
						Row.SetCurrentValue(iColumnIndex, oldValue, value);
				}
			} // prop CurrentRowValues

			public event PropertyChangedEventHandler PropertyChanged
			{
				add { Row.PropertyChanged += value; }
				remove { Row.PropertyChanged -= value; }
			} // prop PropertyChanged
		} // class CurrentRowValues

		#endregion

		internal static readonly object NotSet = new NotSetValue();

		/// <summary>Wird ausgelöst, wenn sich eine Eigenschaft geändert hat</summary>
		public event PropertyChangedEventHandler PropertyChanged;

		private PpsDataTable table;

		private PpsDataRowState rowState;
		private OriginalRowValues orignalValuesProxy;
		private CurrentRowValues currentValuesProxy;
		private object[] originalValues;
		private object[] currentValues;

		#region -- Ctor/Dtor --------------------------------------------------------------

		private PpsDataRow(PpsDataTable table)
		{
			this.rowState = PpsDataRowState.Unchanged;

			this.table = table;
			this.orignalValuesProxy = new OriginalRowValues(this);
			this.currentValuesProxy = new CurrentRowValues(this);

			// Initialisiere die Datenspalten
			this.originalValues = new object[table.Columns.Count];
			this.currentValues = new object[originalValues.Length];
		} // ctor

		/// <summary>Erzeugt eine neue Datenzeile</summary>
		/// <param name="table">Tabelle, welche diese Zeile besitzt.</param>
		/// <param name="rowState"></param>
		/// <param name="originalValues">Originalwerte für die Initialisierung der Zeile.</param>
		/// <param name="currentValues">Aktuelle Werte der Zeile.</param>
		internal PpsDataRow(PpsDataTable table, PpsDataRowState rowState, object[] originalValues, object[] currentValues)
			: this(table)
		{
			this.rowState = rowState;

			int iLength = table.Columns.Count;

			if (originalValues == null || originalValues.Length != iLength)
				throw new ArgumentException("Nicht genug Werte für die Initialisierung.");
			if (currentValues != null && currentValues.Length != iLength)
				throw new ArgumentException("Nicht genug Werte für die Initialisierung.");

			for (int i = 0; i < iLength; i++)
			{
				Type typeTo = table.Columns[i].DataType;
				this.originalValues[i] = originalValues[i] == null ? null : Procs.ChangeType(originalValues[i], typeTo);
				this.currentValues[i] = currentValues == null ? NotSet : Procs.ChangeType(currentValues[i], typeTo);
			}
		} // ctor

		internal PpsDataRow(PpsDataTable table, XElement xRow)
			: this(table)
		{
			int rowState = xRow.GetAttribute(PpsDataSet.xnRowState, 0); // optionales Attribut für Zeilenstatus
			if (!Enum.IsDefined(typeof(PpsDataRowState), rowState))
				throw new ArgumentException(string.Format("Unbekannter Zeilenstatus-Wert '{0}' (=Wert des XML Attributes 's'); erlaubte Werte: siehe Typ 'PpsDataRowState'.", rowState));

			this.rowState = (PpsDataRowState)rowState;

			int i = 0;
			foreach (XElement xValue in xRow.Elements(PpsDataSet.xnRowValue)) // Werte
			{
				if (i >= table.Columns.Count)
					throw new ArgumentException("Mehr Datenwerte als Spaltendefinitionen gefunden");

				XElement xOriginal = xValue.Element(PpsDataSet.xnRowValueOriginal);
				XElement xCurrent = xValue.Element(PpsDataSet.xnRowValueCurrent);

				// Konvertierung wird durch den Konstruktur erledigt
				Type valueType = table.Columns[i].DataType;
				originalValues[i] = xOriginal == null || xOriginal.IsEmpty ? null : Procs.ChangeType(xOriginal.Value, valueType);
				currentValues[i] = xCurrent == null ? NotSet : xCurrent.IsEmpty ? null : Procs.ChangeType(xCurrent.Value, valueType);

				i++;
			}

			if (i != originalValues.Length)
				throw new ArgumentOutOfRangeException("columns");
		} // ctor

		DynamicMetaObject IDynamicMetaObjectProvider.GetMetaObject(Expression parameter)
		{
			return new PpsDataRowMetaObject(parameter, this);
		} // func IDynamicMetaObjectProvider.GetMetaObject

		#endregion

		#region -- Commit, Reset, Remove --------------------------------------------------

		private void CheckForTable()
		{
			if (table == null)
				throw new InvalidOperationException();
		} // proc CheckForTable

		/// <summary>Löscht alle aktuellen Werte und bringt so die Originalwerte zum Vorschein.</summary>
		public void Reset()
		{
			CheckForTable();

			// Setze die Werte zurück
			for (int i = 0; i < originalValues.Length; i++)
			{
				if (currentValues[i] != NotSet)
				{
					object oldValue = currentValues[i];
					currentValues[i] = NotSet;
					OnValueChanged(i, oldValue, originalValues[i]);
				}
			}

			// Die Zeile ist gelöscht, stelle Sie wieder her
			if (rowState == PpsDataRowState.Deleted)
				table.RestoreInternal(this);

			RowState = PpsDataRowState.Unchanged;
		} // proc Reset

		/// <summary>Setzt alle aktuellen Werte in die Originalwerte.</summary>
		public void Commit()
		{
			CheckForTable();

			if (rowState == PpsDataRowState.Deleted)
			{
				table.RemoveInternal(this, true);
			}
			else
			{
				for (int i = 0; i < originalValues.Length; i++)
				{
					if (currentValues[i] != NotSet)
					{
						originalValues[i] = currentValues[i];
						currentValues[i] = NotSet;
					}
				}

				RowState = PpsDataRowState.Unchanged;
			}
		} // proc Commit

		/// <summary>Markiert die Zeile als gelöscht.</summary>
		/// <returns><c>true</c>, wenn die Zeile als gelöscht markiert werden konnte.</returns>
		public bool Remove()
		{
			if (rowState == PpsDataRowState.Deleted || table == null)
				return false;

			bool r = table.RemoveInternal(this, false);
			RowState = PpsDataRowState.Deleted;
			return r;
		} // proc Remove

		#endregion

		#region -- Write ------------------------------------------------------------------

		/// <summary>Schreibt den Inhalt der Datenzeile</summary>
		/// <param name="row"></param>
		internal void Write(XmlWriter x)
		{
			// Status
			x.WriteAttributeString(PpsDataSet.xnRowState.LocalName, ((int)rowState).ToString());
			if (IsAdded)
				x.WriteAttributeString(PpsDataSet.xnRowAdd.LocalName, "1");

			// Werte
			for (int i = 0; i < originalValues.Length; i++)
			{
				x.WriteStartElement(PpsDataSet.xnRowValue.LocalName);

				if (!IsAdded && originalValues[i] != null)
					WriteValue(x, PpsDataSet.xnRowValueOriginal, originalValues[i]);
				if (rowState != PpsDataRowState.Deleted && currentValues[i] != NotSet)
					WriteValue(x, PpsDataSet.xnRowValueCurrent, currentValues[i]);

				x.WriteEndElement();
			}
		} // proc Write

		private void WriteValue(XmlWriter x, XName tag, object value)
		{
			x.WriteStartElement(tag.LocalName);
			if (value != null)
				x.WriteValue(Procs.ChangeType(value, typeof(string)));
			x.WriteEndElement();
		} // proc WriteValue

		#endregion

		#region -- Index Zugriff ----------------------------------------------------------

		private IPpsUndoSink GetUndoSink()
		{
			var sink = table.DataSet.UndoSink;
			return sink != null && !sink.InUndoRedoOperation ? sink : null;
		} // func GetUndoSink

		private void SetCurrentValue(int columnIndex, object oldValue, object value)
		{
			object realCurrentValue = currentValues[columnIndex];

			currentValues[columnIndex] = value; // change the value

			// fill undo stack
			var undo = GetUndoSink();
			if (undo != null)
			{
				// calculate caption and define the transaction
				var column = table.Columns[columnIndex];
				using (var trans = undo.BeginTransaction(String.Format(">{0}< geändert.", column.Meta.Get(PpsDataColumnMetaData.Caption, column.Name))))
				{
					undo.Append(new PpsDataRowValueChangedItem(this, columnIndex, realCurrentValue, value));
					OnValueChanged(columnIndex, oldValue, value); // Notify the value change 
					trans.Commit();
				}
			}
			else
				OnValueChanged(columnIndex, oldValue, value); // Notify the value change 
		} // proc SetCurrentValue

		/// <summary>If the value of the row gets changed, this method is called.</summary>
		/// <param name="columnIndex"></param>
		/// <param name="oldValue"></param>
		/// <param name="value"></param>
		protected virtual void OnValueChanged(int columnIndex, object oldValue, object value)
		{
			if (RowState == PpsDataRowState.Unchanged)
			{
				var undo = GetUndoSink();
				if (undo != null)
					undo.Append(new PpsDataRowStateChangedItem(this, PpsDataRowState.Unchanged, PpsDataRowState.Modified));
				RowState = PpsDataRowState.Modified;
			}

			table.OnColumnValueChanged(this, columnIndex, oldValue, value);
			OnPropertyChanged(table.Columns[columnIndex].Name);
		} // proc OnValueChanged

		protected virtual void OnPropertyChanged(string sPropertyName)
		{
			if (PropertyChanged != null)
				PropertyChanged(this, new PropertyChangedEventArgs(sPropertyName));
		} // proc OnPropertyChanged

		/// <summary>Zugriff auf den aktuellen Wert.</summary>
		/// <param name="columnIndex">Spalte</param>
		/// <returns></returns>
		public object this[int columnIndex]
		{
			get { return currentValuesProxy[columnIndex]; }
			set { currentValuesProxy[columnIndex] = value; }
		} // prop this

		/// <summary>Zugriff auf den aktuellen Wert.</summary>
		/// <param name="columnName">Spalte</param>
		/// <returns></returns>
		public object this[string columnName]
		{
			get { return currentValuesProxy[columnName]; }
			set { currentValuesProxy[columnName] = value; }
		} // prop this

		/// <summary>Zugriff auf die aktuellen Werte</summary>
		public RowValues Current { get { return currentValuesProxy; } }
		/// <summary>Originale Werte, mit der diese Zeile initialisiert wurde</summary>
		public RowValues Original { get { return orignalValuesProxy; } }

		/// <summary>Status der Zeile</summary>
		public PpsDataRowState RowState
		{
			get { return rowState; }
			private set
			{
				if (rowState != value)
				{
					rowState = value;
					OnPropertyChanged("RowState");
				}
			}
		} // prop RowState

		/// <summary>Wurde die Datenzeile neu angefügt.</summary>
		public bool IsAdded { get { return table == null ? false : table.OriginalRows.Contains(this); } }

		#endregion

		/// <summary>Zugehörige Datentabelle</summary>
		public PpsDataTable Table { get { return table; } internal set { table = value; } }

		// -- Static --------------------------------------------------------------

		private static readonly PropertyInfo RowStatePropertyInfo;
		private static readonly PropertyInfo ItemPropertyInfo;
		private static readonly PropertyInfo CurrentPropertyInfo;
		private static readonly PropertyInfo OriginalPropertyInfo;
		private static readonly FieldInfo TableFieldInfo;
		private static readonly MethodInfo ResetMethodInfo;
		private static readonly MethodInfo CommitMethodInfo;

		private static readonly PropertyInfo ValuesPropertyInfo;
		private static readonly FieldInfo RowFieldInfo;

		#region -- sctor ------------------------------------------------------------------

		static PpsDataRow()
		{
			var typeRowInfo = typeof(PpsDataRow).GetTypeInfo();
			RowStatePropertyInfo = typeRowInfo.GetDeclaredProperty("RowState");
			ItemPropertyInfo = FindItemIndex(typeRowInfo);
			CurrentPropertyInfo = typeRowInfo.GetDeclaredProperty("Current");
			OriginalPropertyInfo = typeRowInfo.GetDeclaredProperty("Original");
			TableFieldInfo = typeRowInfo.GetDeclaredField("table");
			ResetMethodInfo = typeRowInfo.GetDeclaredMethod("Reset");
			CommitMethodInfo = typeRowInfo.GetDeclaredMethod("Commit");

			var typeValueInfo = typeof(RowValues).GetTypeInfo();
			ValuesPropertyInfo = FindItemIndex(typeValueInfo);
			RowFieldInfo = typeValueInfo.GetDeclaredField("row");

			if (RowStatePropertyInfo == null ||
					ItemPropertyInfo == null ||
					CurrentPropertyInfo == null ||
					OriginalPropertyInfo == null ||
					TableFieldInfo == null ||
					ResetMethodInfo == null ||
					CommitMethodInfo == null ||
					ValuesPropertyInfo == null ||
					RowFieldInfo == null)
				throw new InvalidOperationException("Reflection fehlgeschlagen (PpsDataRow)");
		} // sctor

		private static PropertyInfo FindItemIndex(TypeInfo typeInfo)
		{
			return (from pi in typeInfo.DeclaredProperties where pi.Name == "Item" && pi.GetIndexParameters()[0].ParameterType == typeof(int) select pi).FirstOrDefault();
		} // func FindItemIndex

		#endregion
	} // class PpsDataRow

	#endregion
}
