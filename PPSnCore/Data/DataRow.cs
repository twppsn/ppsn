using System;
using System.Collections.Generic;
using System.ComponentModel;
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
		/// <summary>In der Datenzeile wurde nichts geändert</summary>
		Unchanged = 0,
		/// <summary>Datenzeile wurde geändert</summary>
		Changed = 1,
		/// <summary>Datenzeile wurde gelöscht</summary>
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
							Expression.Property(Expression.Field(expr, TableFieldInfo), PpsDataTable.ClassPropertyInfo),
							Expression.Constant(Row.table.Class, typeof(PpsDataTableDefinition))
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

				int iColumnIndex = Row.table.Class.FindColumnIndex(binder.Name);
				if (iColumnIndex >= 0)
					return new DynamicMetaObject(GetIndexExpression(iColumnIndex), GetRestriction());
				else
					return new DynamicMetaObject(Expression.Constant(null, typeof(object)), GetRestriction());
			} // func BindGetMember

			public override DynamicMetaObject BindSetMember(SetMemberBinder binder, DynamicMetaObject value)
			{
				if (IsStandardMember(binder.Name))
					return base.BindSetMember(binder, value);

				int iColumnIndex = Row.table.Class.FindColumnIndex(binder.Name);
				if (iColumnIndex >= 0){
					return new DynamicMetaObject(
						Expression.Assign(GetIndexExpression(iColumnIndex), Expression.Convert(value.Expression, typeof(object))),
						GetRestriction().Merge(value.Restrictions)
					);}
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
			/// <param name="iColumnIndex">Index der Spalte</param>
			/// <returns>Wert in der Spalte</returns>
			public abstract object this[int iColumnIndex] { get; set; }

			/// <summary>Ermöglicht den Zugriff auf die Spalte.</summary>
			/// <param name="sColumnName">Name der Spalte</param>
			/// <returns>Wert in der Spalte</returns>
			public object this[string sColumnName]
			{
				get { return this[Row.table.Class.FindColumnIndex(sColumnName, true)]; }
				set { this[Row.table.Class.FindColumnIndex(sColumnName, true)] = value; }
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
					value = Procs.ChangeType(value, Row.table.Class.Columns[iColumnIndex].DataType);

					// Prüfe die Änderung
					if (!Object.Equals(this[iColumnIndex], value))
					{
						Row.currentValues[iColumnIndex] = value;
						Row.OnPropertyChanged(iColumnIndex);
					}
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

		private PpsDataRowState state;
		private OriginalRowValues orignalValuesProxy;
		private CurrentRowValues currentValuesProxy;
		private object[] originalValues;
		private object[] currentValues;

		#region -- Ctor/Dtor --------------------------------------------------------------

		private PpsDataRow(PpsDataTable table, PpsDataRowState state)
		{
			this.table = table;
			this.state = state;
			this.orignalValuesProxy = new OriginalRowValues(this);
			this.currentValuesProxy = new CurrentRowValues(this);

			// Initialisiere die Datenspalten
			this.originalValues = new object[table.Columns.Count];
			this.currentValues = new object[originalValues.Length];
		} // ctor

		/// <summary>Erzeugt eine neue Datenspalte</summary>
		/// <param name="table">Tabelle, zu der diese Zeile zugeordnet wird.</param>
		/// <param name="state"></param>
		/// <param name="originalValues">Originalwerte für die Initialisierung der Zeile.</param>
		/// <param name="currentValues">Aktuelle Werte der Zeile.</param>
		internal PpsDataRow(PpsDataTable table, PpsDataRowState state, object[] originalValues, object[] currentValues)
			: this(table, state)
		{
			int iLength = table.Columns.Count;

			if (originalValues == null || originalValues.Length != iLength)
				throw new ArgumentException("Nicht genug werde für die Initialisierung.");
			if (currentValues != null && currentValues.Length != iLength)
				throw new ArgumentException("Nicht genug werde für die Initialisierung.");

			for (int i = 0; i < iLength; i++)
			{
				Type typeTo = table.Columns[i].DataType;
				this.originalValues[i] = originalValues[i] == null ? null : Procs.ChangeType(originalValues[i], typeTo);
				this.currentValues[i] = currentValues == null ? NotSet : Procs.ChangeType(currentValues[i], typeTo);
			}
		} // ctor

		internal PpsDataRow(PpsDataTable table, XElement xRow)
			: this(table, PpsDataRowState.Unchanged)
		{
			this.table = table;
			this.state = (PpsDataRowState)xRow.GetAttribute("s", 0);

			int i = 0;
			foreach (XElement xValue in xRow.Elements("v")) // Werte
			{
				XElement xOriginal = xValue.Element("o");
				XElement xCurrent = xValue.Element("c");

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
					currentValues[i] = NotSet;
					OnPropertyChanged(i);
				}
			}

			// Die Zeile ist gelöscht, stelle Sie wieder her
			if (state == PpsDataRowState.Deleted)
				table.RestoreInternal(this);

			RowState = PpsDataRowState.Unchanged;
		} // proc Reset

		/// <summary>Setzt alle aktuellen Werte in die Originalwerte.</summary>
		public void Commit()
		{
			CheckForTable();

			if (state == PpsDataRowState.Deleted)
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
			if (state == PpsDataRowState.Deleted || table == null)
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
			x.WriteAttributeString("s", ((int)state).ToString());
			if (IsAdded)
				x.WriteAttributeString("a", "1");

			// Werte
			for (int i = 0; i < originalValues.Length; i++)
			{
				x.WriteStartElement("v");

				if (!IsAdded && originalValues[i] != null)
					WriteValue(x, "o", originalValues[i]);
				if (state != PpsDataRowState.Deleted && currentValues[i] != NotSet)
					WriteValue(x, "c", currentValues[i]);

				x.WriteEndElement();
			}
		} // proc Write

		private void WriteValue(XmlWriter x, string sTag, object value)
		{
			x.WriteStartElement(sTag);
			if (value != null)
				x.WriteValue(Procs.ChangeType(value, typeof(string)));
			x.WriteEndElement();
		} // proc WriteValue

		#endregion

		#region -- Index Zugriff ----------------------------------------------------------

		/// <summary>Löst das Event aus, welches über die Änderung eines Wertes informiert.</summary>
		/// <param name="iColumnIndex">Spalte</param>
		protected void OnPropertyChanged(int iColumnIndex)
		{
			if (RowState == PpsDataRowState.Unchanged)
				RowState = PpsDataRowState.Changed;

			OnPropertyChanged(table.Columns[iColumnIndex].Name);
		} // proc OnPropertyChanged

		protected virtual void OnPropertyChanged(string sPropertyName)
		{
			if (PropertyChanged != null)
				PropertyChanged(this, new PropertyChangedEventArgs(sPropertyName));
		} // proc OnPropertyChanged

		/// <summary>Zugriff auf den aktuellen Wert.</summary>
		/// <param name="iColumnIndex">Spalte</param>
		/// <returns></returns>
		public object this[int iColumnIndex]
		{
			get { return currentValuesProxy[iColumnIndex]; }
			set { currentValuesProxy[iColumnIndex] = value; }
		} // prop this

		/// <summary>Zugriff auf den aktuellen Wert.</summary>
		/// <param name="sColumnName">Spalte</param>
		/// <returns></returns>
		public object this[string sColumnName]
		{
			get { return currentValuesProxy[sColumnName]; }
			set { currentValuesProxy[sColumnName] = value; }
		} // prop this

		/// <summary>Zugriff auf die aktuellen Werte</summary>
		public RowValues Current { get { return currentValuesProxy; } }
		/// <summary>Originale Werte, mit der diese Zeile initialisiert wurde</summary>
		public RowValues Original { get { return orignalValuesProxy; } }

		/// <summary>Status der Zeile</summary>
		public PpsDataRowState RowState
		{
			get { return state; }
			private set
			{
				if (state != value)
				{
					state = value;
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
