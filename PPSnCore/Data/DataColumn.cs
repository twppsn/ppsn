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
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;

namespace TecWare.PPSn.Data
{
	#region -- enum PpsDataColumnMetaData -----------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Vordefinierte Meta-Daten an der Spalte.</summary>
	public enum PpsDataColumnMetaData
	{
		/// <summary>Beschreibt die maximale Länge einer Zeichenfolge.</summary>
		MaxLength,
		/// <summary>Kurztext der Spalte</summary>
		Caption,
		/// <summary>Beschreibungstext der Spalte</summary>
		Description
	} // enum PpsDataColumnMetaData

	#endregion

	#region -- enum PpsDataColumnValueChangingFlag --------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public enum PpsDataColumnValueChangingFlag
	{
		/// <summary>Notifies about a loaded value. The value is not changeabled</summary>
		Notify,
		/// <summary>Sets the initial value for a new row.</summary>
		Initial,
		/// <summary>Value gets changed</summary>
		SetValue
	} // enum PpsDataColumnValueChangingFlag

	#endregion

	#region -- class PpsDataColumnDefinition --------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Basisklasse für die Spaltendefinitionen.</summary>
	public abstract class PpsDataColumnDefinition : IDynamicMetaObjectProvider
	{
		#region -- WellKnownTypes ---------------------------------------------------------

		/// <summary>Definiert die bekannten Meta Informationen.</summary>
		private static readonly Dictionary<string, Type> wellknownMetaTypes = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
		{
			{ PpsDataColumnMetaData.MaxLength.ToString(), typeof(int) },
			{ PpsDataColumnMetaData.Caption.ToString(), typeof(string) },
			{ PpsDataColumnMetaData.Description.ToString(), typeof(string) }
		};

		#endregion

		#region -- class PpsDataColumnMetaCollection --------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		public class PpsDataColumnMetaCollection : PpsMetaCollection
		{
			public T Get<T>(PpsDataColumnMetaData key, T @default)
			{
				return Get<T>(key.ToString(), @default);
			} // func Get

			public override IReadOnlyDictionary<string, Type> WellknownMetaTypes { get { return wellknownMetaTypes; } }
		} // class PpsDataColumnMetaCollection

		#endregion

		#region -- class PpsDataColumnMetaObject ------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class PpsDataColumnMetaObject : DynamicMetaObject
		{
			public PpsDataColumnMetaObject(Expression expr, object value)
				: base(expr, BindingRestrictions.Empty, value)
			{
			} // ctor

			public override DynamicMetaObject BindGetMember(GetMemberBinder binder)
			{
				if (PpsDataHelper.IsStandardMember(LimitType, binder.Name))
				{
					return base.BindGetMember(binder);
				}
				else
				{
					var column = (PpsDataColumnDefinition)Value;

					return new DynamicMetaObject(
						column.Meta.GetMetaConstantExpression(binder.Name),
						BindingRestrictions.GetInstanceRestriction(Expression, Value)
					);
				}
			} // func BindGetMemger
		} // class PpsDataColumnMetaObject

		#endregion

		private PpsDataTableDefinition table;

		private readonly string columnName;	// Internal name of the column

		/// <summary>Erzeugt eine neue Spaltendefinition.</summary>
		/// <param name="table">Zugehörige Tabelle</param>
		/// <param name="columnName">Name der Spalte</param>
		public PpsDataColumnDefinition(PpsDataTableDefinition table, string columnName)
		{
			this.table = table;
			this.columnName = columnName;
		} // ctor
		
		public override string ToString() => $"{table.Name}.{columnName}";

		public virtual void EndInit()
		{
		} // proc EndInit

		/// <summary>Returns the initial value for a column.</summary>
		/// <returns></returns>
		public virtual object GetInitialValue(PpsDataTable table) => null;

		/// <summary>Gets called if a value is changing.</summary>
		/// <param name="row"></param>
		/// <param name="flag"></param>
		/// <param name="oldValue"></param>
		/// <param name="value"></param>
		protected internal virtual bool OnColumnValueChanging(PpsDataRow row, PpsDataColumnValueChangingFlag flag, object oldValue, ref object value) => true;

		DynamicMetaObject IDynamicMetaObjectProvider.GetMetaObject(Expression parameter)
		{
			return new PpsDataColumnMetaObject(parameter, this);
		} // func IDynamicMetaObjectProvider.GetMetaObject

		/// <summary>Zugehörige Tabelle</summary>
		public PpsDataTableDefinition Table { get { return table; } }

		/// <summary>Name der Spalte</summary>
		public string Name { get { return columnName; } }
		/// <summary>Datentyp der Spalte</summary>
		public abstract Type DataType { get; }
		/// <summary>Index der Spalte innerhalb der Datentabelle</summary>
		public int Index { get { return table.Columns.IndexOf(this); } }

		public virtual bool IsInitialized => true;

		/// <summary>Zugriff auf die zugeordneten Meta-Daten der Spalte.</summary>
		public abstract PpsDataColumnMetaCollection Meta { get; }
	} // class PpsDataColumnDefinition

	#endregion

	#region -- class PpsDataValueColumnDefinition ---------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public abstract class PpsDataValueColumnDefinition : PpsDataColumnDefinition
	{
		private readonly Type dataType;              // Datatype of the column

		public PpsDataValueColumnDefinition(PpsDataTableDefinition table, string columnName, Type dataType)
			: base(table, columnName)
		{
			this.dataType = dataType;
		} // ctor

		/// <summary>Datatype of the current column</summary>
		public override Type DataType => dataType;
	} // class PpsDataValueColumnDefinition

	#endregion

	#region -- class PpsDataPrimaryColumnDefinition -------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public abstract class PpsDataPrimaryColumnDefinition : PpsDataColumnDefinition
	{
		public PpsDataPrimaryColumnDefinition(PpsDataTableDefinition table, string columnName)
			: base(table, columnName)
		{
		} // ctor

		protected internal override bool OnColumnValueChanging(PpsDataRow row, PpsDataColumnValueChangingFlag flag, object oldValue, ref object value)
		{
			switch (flag)
			{
				case PpsDataColumnValueChangingFlag.SetValue:
					throw new NotSupportedException($"{Name} is a readonly column.");

				case PpsDataColumnValueChangingFlag.Notify:
					row.Table.DataSet.UpdateNextId((long)value);
					return true;

				case PpsDataColumnValueChangingFlag.Initial:
					if (value != null)
						goto case PpsDataColumnValueChangingFlag.SetValue;

					value = row.Table.DataSet.GetNextId();
					return true;
			}
      return base.OnColumnValueChanging(row, flag, oldValue, ref value);
		} // func OnColumnValueChanging

		/// <summary>PrimaryKey is always a long</summary>
		public override Type DataType => typeof(long);
	} // class PpsDataPrimaryColumnDefinition

	#endregion

	#region -- class PpsDataRelationColumnDefinition ------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public abstract class PpsDataRelationColumnDefinition : PpsDataColumnDefinition
	{
		private readonly PpsDataColumnDefinition parentColumn;   // parent column of the parent child relation

		public PpsDataRelationColumnDefinition(PpsDataTableDefinition table, string columnName, string relationName, PpsDataColumnDefinition parentColumn)
			: base(table, columnName)
		{
			this.parentColumn = parentColumn;

			// register the relation
			parentColumn.Table.AddRelation(relationName ?? table.Name, parentColumn, this);
		} // ctor

		private bool ExistsValueInParentTable(PpsDataRow row, object value)
		{
			var parentTable = row.Table.DataSet.FindTableFromDefinition(parentColumn.Table);
			var parentColumnIndex = parentColumn.Index;

			for (int i = 0; i < parentTable.Count; i++)
			{
				if (Object.Equals(parentTable[i][parentColumnIndex], value))
					return true;
			}

			return false;
		} // func ExistsValueInParentTable

		protected internal override bool OnColumnValueChanging(PpsDataRow row, PpsDataColumnValueChangingFlag flag, object oldValue, ref object value)
		{
			switch (flag)
			{
				case PpsDataColumnValueChangingFlag.SetValue: // check if the values exists
					if (!ExistsValueInParentTable(row, value))
						throw new ArgumentOutOfRangeException($"Value '{value}' does not exist in '{parentColumn.Table.Name}.{parentColumn.Name}'.");
          return true;
			}

			return base.OnColumnValueChanging(row, flag, oldValue, ref value);
		} // func OnColumnValueChanging

		/// <summary>Column that is the parent</summary>
		public PpsDataColumnDefinition ParentColumn => parentColumn;
		/// <summary>Datatype of the parent</summary>
		public override Type DataType => parentColumn.DataType;
	} // class PpsDataRelationColumnDefinition

#endregion
}
