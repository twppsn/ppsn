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
using TecWare.DE.Data;
using TecWare.DE.Stuff;

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
	public abstract class PpsDataColumnDefinition : IDataColumn, IDynamicMetaObjectProvider
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
			public PpsDataColumnMetaCollection()
			{
			} // ctor

			protected PpsDataColumnMetaCollection(PpsDataColumnMetaCollection clone)
				: base(clone)
			{
			} // ctor

			public T GetProperty<T>(PpsDataColumnMetaData key, T @default)
				=> PropertyDictionaryExtensions.GetProperty<T>(this, key.ToString(), @default);

			public override IReadOnlyDictionary<string, Type> WellknownMetaTypes => wellknownMetaTypes;
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

		private readonly string columnName;		// Internal name of the column
		private readonly bool isPrimaryKey;   // is this a primary column

		private string relationName;
		private PpsDataColumnDefinition parentColumn = null;   // parent column of the parent child relation

		protected PpsDataColumnDefinition(PpsDataTableDefinition table, PpsDataColumnDefinition clone)
		{
			this.table = table;
			this.columnName = clone.columnName;
			this.isPrimaryKey = clone.isPrimaryKey;

			if (clone.parentColumn != null)
			{
				this.relationName = clone.relationName;

				var parentTable = table.DataSet.FindTable(clone.parentColumn.Table.Name);
				this.parentColumn = parentTable.FindColumn(clone.parentColumn.Name);

				// register the relation
				parentColumn.Table.AddRelation(relationName, parentColumn, this);
			}
		} // ctor

		/// <summary>Erzeugt eine neue Spaltendefinition.</summary>
		/// <param name="table">Zugehörige Tabelle</param>
		/// <param name="columnName">Name der Spalte</param>
		public PpsDataColumnDefinition(PpsDataTableDefinition table, string columnName, bool isPrimaryKey)
		{
			if (String.IsNullOrEmpty(columnName))
				throw new ArgumentNullException();

			this.table = table;
			this.columnName = columnName;
			this.isPrimaryKey = isPrimaryKey;

			this.relationName = null;
			this.parentColumn = null;
		} // ctor

		protected void SetRelationName(string relationName)
		{
			if (IsInitialized)
				throw new InvalidOperationException("Can not change a relation after initialization.");

			this.relationName = relationName ?? table.Name;
		} // proc SetRelationName

		protected void SetParentColumn(string relationName, string parentTableName, string parentColumnName)
		{
			SetRelationName(relationName ?? this.relationName);

			// register the relation
			var parentTable = table.DataSet.FindTable(parentTableName);
			if (parentTable == null)
				throw new ArgumentOutOfRangeException("parentTableName", $"'{parentTableName}' not found.");

			var parentColumn = parentTable.FindColumn(parentColumnName);
			if (parentColumn == null)
				throw new ArgumentOutOfRangeException("parentColumnName", $"'{parentTableName}.{parentColumnName}' not found.");

			parentColumn.Table.AddRelation(this.relationName, parentColumn, this);

			this.parentColumn = parentColumn;
		} //  proc SetParentColumn

		public abstract PpsDataColumnDefinition Clone(PpsDataTableDefinition tableOwner);
		
		public override string ToString()
			=> $"{table.Name}.{columnName}";

		public virtual void EndInit()
		{
		} // proc EndInit

		/// <summary>Returns the initial value for a column.</summary>
		/// <returns></returns>
		public virtual object GetInitialValue(PpsDataTable table)
			=> null;

		/// <summary>Gets called if a value is changing.</summary>
		/// <param name="row"></param>
		/// <param name="flag"></param>
		/// <param name="oldValue"></param>
		/// <param name="value"></param>
		protected internal virtual bool OnColumnValueChanging(PpsDataRow row, PpsDataColumnValueChangingFlag flag, object oldValue, ref object value)
		{
			switch (flag)
			{
				case PpsDataColumnValueChangingFlag.SetValue:
					if (IsRelationColumn)
					{
						if (!ExistsValueInParentTable(row, value))
							throw new ArgumentOutOfRangeException($"Value '{value}' does not exist in '{parentColumn.Table.Name}.{parentColumn.Name}'.");
					}
					else if (IsPrimaryKey)
						throw new NotSupportedException($"{Name} is a readonly column.");
					return true;

				case PpsDataColumnValueChangingFlag.Notify:
					if (IsPrimaryKey)
						row.Table.DataSet.UpdateNextId((long)value);
					return true;

				case PpsDataColumnValueChangingFlag.Initial:
					if (value != null)
						goto case PpsDataColumnValueChangingFlag.SetValue;

					if (IsPrimaryKey)
						value = row.Table.DataSet.GetNextId();

					return true;

				default:
					return true;
			}
		} // func OnColumnValueChanging

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

		DynamicMetaObject IDynamicMetaObjectProvider.GetMetaObject(Expression parameter)
			=> new PpsDataColumnMetaObject(parameter, this);

		protected virtual Type GetDataType()
		{
			throw new NotImplementedException();
		} // func GetDataType
		
		/// <summary>Zugehörige Tabelle</summary>
		public PpsDataTableDefinition Table { get { return table; } }

		/// <summary>Name der Spalte</summary>
		public string Name => columnName;
		/// <summary>Datentyp der Spalte</summary>
		public Type DataType
		{
			get
			{
				if (IsRelationColumn)
					return parentColumn.DataType;
				else
					return GetDataType();
			}
		} // prop DataType
		
		/// <summary>Index der Spalte innerhalb der Datentabelle</summary>
		public int Index => table.Columns.IndexOf(this);

		/// <summary></summary>
		public bool IsPrimaryKey => isPrimaryKey;
		/// <summary></summary>
		public bool IsRelationColumn => parentColumn != null;
		/// <summary></summary>
		public PpsDataColumnDefinition ParentColumn => parentColumn;

		public virtual bool IsInitialized => true;

		/// <summary>Zugriff auf die zugeordneten Meta-Daten der Spalte.</summary>
		public abstract PpsDataColumnMetaCollection Meta { get; }

		IDataColumnAttributes IDataColumn.Attributes => Meta;
	} // class PpsDataColumnDefinition

	#endregion
}
