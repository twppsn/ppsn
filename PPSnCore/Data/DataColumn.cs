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
using System.ComponentModel;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Xml.Linq;
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
		/// <summary>Sets the initial value for a new row.</summary>
		Initial,
		/// <summary>Value gets changed</summary>
		SetValue
	} // enum PpsDataColumnValueChangingFlag

	#endregion

	#region -- interface IPpsDataRowExtendedValue ---------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Interface that is implement on special values classes.
	/// It represent a nested structur of values (e.g. value formulas, ...).
	/// - Special values are instanciated per Value an will not destroyed
	/// during the whole life time.
	/// - If a special value is changable, it has to take care on its own.
	/// -The changes to the undo/redo stack has to be done by the
	/// implementation.</summary>
	public interface IPpsDataRowExtendedValue : INotifyPropertyChanged
	{
		bool IsNull { get; }

		/// <summary>Gets/Sets the core data of the extended value.</summary>
		XElement CoreData { get; set; }
	} // interface IPpsDataRowExtendedValue

	#endregion

	#region -- interface IPpsDataRowGenericValue ----------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Does this extended value supports a generic setter</summary>
	public interface IPpsDataRowGenericValue : IPpsDataRowExtendedValue
	{
		/// <summary>Generic value</summary>
		/// <param name="inital"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		bool SetGenericValue(bool inital, object value);
	} //	interface IPpsDataRowGenericValue

	#endregion

	#region -- class PpsDataRowExtentedValue --------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public abstract class PpsDataRowExtentedValue : IPpsDataRowExtendedValue, INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler PropertyChanged;

		#region -- class PpsDataRowExtentedValueChanged -----------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private class PpsDataRowExtentedValueChanged : PpsDataChangedEvent
		{
			private readonly PpsDataRowExtentedValue value;
			private readonly string propertyName;

			public PpsDataRowExtentedValueChanged(PpsDataRowExtentedValue value, string propertyName)
			{
				this.value = value;
				this.propertyName = propertyName;
			} // ctor

			public override bool Same(PpsDataChangedEvent ev)
			{
				if (ev == this)
					return true;
				else
				{
					var other = ev as PpsDataRowExtentedValueChanged;
					return other != null ?
						other.value == value && other.propertyName == propertyName :
						false;
				}
			} // func Same

			public override void InvokeEvent()
			{
				value.InvokePropertyChanged(propertyName);
				value.row.Table.DataSet.OnTableColumnExtendedValueChanged(value.row, value.column.Name, value, propertyName);
			} // proc InvokeEvent

			public override PpsDataChangeLevel Level => PpsDataChangeLevel.ExtentedValue;
		} // class PpsDataRowExtentedValueChanged

		#endregion

		private readonly PpsDataRow row;
		private readonly PpsDataColumnDefinition column;

		public PpsDataRowExtentedValue(PpsDataRow row, PpsDataColumnDefinition column)
		{
			this.row = row;
			this.column = column;
		} // ctor

		private void InvokePropertyChanged(string propertyName)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		protected virtual void OnPropertyChanged(string propertyName)
		{
			row.Table.DataSet.ExecuteEvent(new PpsDataRowExtentedValueChanged(this, propertyName));
		} // proc OnPropertyChanged

		public abstract bool IsNull { get; }
		public abstract XElement CoreData { get; set; }

		public PpsDataRow Row => row;
		public PpsDataColumnDefinition Column => column;
	} // class PpsDataRowExtentedValue

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
					return base.BindGetMember(binder);
				else
				{
					var column = (PpsDataColumnDefinition)Value;

					return new DynamicMetaObject(
						column.Meta.GetMetaConstantExpression(binder.Name, false),
						BindingRestrictions.GetInstanceRestriction(Expression, Value)
					);
				}
			} // func BindGetMember

			public override DynamicMetaObject BindInvokeMember(InvokeMemberBinder binder, DynamicMetaObject[] args)
			{
				if (args.Length > 0 || PpsDataHelper.IsStandardMember(LimitType, binder.Name))
					return base.BindInvokeMember(binder, args);
				else
				{
					var column = (PpsDataColumnDefinition)Value;

					return new DynamicMetaObject(
						column.Meta.GetMetaConstantExpression(binder.Name, true),
						BindingRestrictions.GetInstanceRestriction(Expression, Value)
					);
				}
			} // func BindInvokeMember
		} // class PpsDataColumnMetaObject

		#endregion

		private readonly PpsDataTableDefinition table;  // table
		private readonly string columnName;             // Internal name of the column
		private readonly bool isIdentity;
		private readonly Lazy<bool> isExtendedValue;
		private PpsDataTableRelationDefinition parentRelation; // relation to the parent column, the current column has a value from the parent column

		protected PpsDataColumnDefinition(PpsDataTableDefinition table, PpsDataColumnDefinition clone)
		{
			this.table = table;
			this.columnName = clone.columnName;
			this.isIdentity = clone.isIdentity;
			this.isExtendedValue = new Lazy<bool>(() => DataType.GetTypeInfo().ImplementedInterfaces.Contains(typeof(IPpsDataRowExtendedValue)));

			if (clone.IsPrimaryKey)
				table.SetPrimaryKey(this);
		} // ctor

		/// <summary>Create a new column.</summary>
		/// <param name="table">Table of the column</param>
		/// <param name="columnName">Name of the column</param>
		/// <param name="isPrimaryKey">Is this a primary key column.</param>
		/// <param name="isIdentity">Is this a identity</param>
		public PpsDataColumnDefinition(PpsDataTableDefinition table, string columnName, bool isPrimaryKey, bool isIdentity)
		{
			if (String.IsNullOrEmpty(columnName))
				throw new ArgumentNullException();

			this.table = table;
			this.columnName = columnName;
			this.parentRelation = null;

			this.isIdentity = isIdentity;
			this.isExtendedValue = new Lazy<bool>(() => DataType.GetTypeInfo().ImplementedInterfaces.Contains(typeof(IPpsDataRowExtendedValue)));

			if (isPrimaryKey)
				table.SetPrimaryKey(this);
		} // ctor

		internal void SetParentRelation(PpsDataTableRelationDefinition parentRelation)
		{
			if (this.parentRelation != null)
				throw new InvalidOperationException("Only one parent relation per column is allowed.");
			this.parentRelation = parentRelation;
		} // proc SetParentRelation

		public abstract PpsDataColumnDefinition Clone(PpsDataTableDefinition tableOwner);

		public override string ToString()
			=> $"{table.Name}.{columnName}";

		public virtual void EndInit()
		{
		} // proc EndInit

		/// <summary>Gets called if a value is changing.</summary>
		/// <param name="row"></param>
		/// <param name="flag"></param>
		/// <param name="oldValue"></param>
		/// <param name="value"></param>
		internal bool OnColumnValueChanging(PpsDataRow row, PpsDataColumnValueChangingFlag flag, object oldValue, ref object value)
		{
			var initial = false;
			switch (flag)
			{
				case PpsDataColumnValueChangingFlag.SetValue:
					var ret = true;

					if (parentRelation != null) // check value contraint
					{
						if (!ExistsValueInParentTable(row, value))
							throw new ArgumentOutOfRangeException($"Value '{value}' does not exist in '{parentRelation.ParentColumn.Table.Name}.{parentRelation.ParentColumn.Name}'.");
					}
					if (IsExtended)
					{
						// check for a internal interface to set a generic value
						var v = oldValue as IPpsDataRowGenericValue;
						if (v != null)
						{
							ret = v.SetGenericValue(initial, value);
							value = oldValue; // reset old value
						}
						else
							throw new NotSupportedException($"It is not allowed to change this extended column ({Table.Name}.{Name}).");
					}
					if (IsPrimaryKey) // check unique
					{
						if (row.Table.FindRows(this, value).FirstOrDefault() != null)
							throw new ArgumentOutOfRangeException($"Value '{value}' is not unique for column '{Table.Name}.{Name}'.");
					}
					return ret;

				case PpsDataColumnValueChangingFlag.Initial:

					if (isIdentity && value == null) // automatic value
						value = row.Table.DataSet.GetNextId();
					else if (IsExtended) // is extended
						oldValue = CreateExtendedValue(row);

					if (value != null)
					{
						initial = true;
						goto case PpsDataColumnValueChangingFlag.SetValue;
					}
					else
						value = oldValue;

					return true;

				default:
					return true;
			}
		} // func OnColumnValueChanging

		private object CreateExtendedValue(PpsDataRow row)
		{
			var typeInfo = DataType.GetTypeInfo();

			// find the longest ctor
			var ci = (ConstructorInfo )null;
			var currentCtorLength = -1;
			foreach (var cur in typeInfo.DeclaredConstructors)
			{
				var newCtorLength = cur.GetParameters().Length;
				if (currentCtorLength < newCtorLength)
				{
					ci = cur;
					currentCtorLength = newCtorLength;
				}
			}

			// create the arguments array
			var parameterInfo = ci.GetParameters();
			var arguments = new object[parameterInfo.Length];
			
			for (var i = 0; i < arguments.Length; i++)
			{
				var parameterType = parameterInfo[i].ParameterType;
				if (parameterType == typeof(PpsDataRow))
					arguments[i] = row;
				else if (parameterType == typeof(PpsDataColumnDefinition))
					arguments[i] = this;
				else if (parameterInfo[i].HasDefaultValue)
					arguments[i] = parameterInfo[i].DefaultValue;
				else
					throw new ArgumentException("Unknown argument.");
			}

			// initialize the value
			return ci.Invoke(arguments);
		} // func CreateExtendedValue

		internal void OnColumnValueChanged(PpsDataRow row, object oldValue, object value)
		{
			if (isIdentity)
				row.Table.DataSet.UpdateNextId((long)value);

			// update child relations
			foreach (var relation in table.Relations.Where(r => r.ParentColumn == this))
			{
				var table = row.Table.DataSet.Tables[relation.ChildColumn.Table];
				var columnIndex = relation.ChildColumn.Index;

				foreach (var childRow in table.FindRows(relation.ChildColumn, oldValue))
					childRow[columnIndex] = value;
			}
		} // proc OnColumnValueChanged

		private bool ExistsValueInParentTable(PpsDataRow row, object value)
		{
			var parentTable = row.Table.DataSet.Tables[parentRelation.ParentColumn.Table];
			return parentTable.FindRows(parentRelation.ParentColumn, value).FirstOrDefault() != null;
		} // func ExistsValueInParentTable

		protected abstract Type GetDataType();

		DynamicMetaObject IDynamicMetaObjectProvider.GetMetaObject(Expression parameter)
			=> new PpsDataColumnMetaObject(parameter, this);

		/// <summary>Zugehörige Tabelle</summary>
		public PpsDataTableDefinition Table { get { return table; } }

		/// <summary>Name der Spalte</summary>
		public string Name => columnName;
		/// <summary>Datentyp der Spalte</summary>
		public Type DataType => IsRelationColumn ? parentRelation.ParentColumn.DataType : GetDataType();

		/// <summary>Index der Spalte innerhalb der Datentabelle</summary>
		public int Index => table.Columns.IndexOf(this);

		/// <summary>Is this column a primary key.</summary>
		public bool IsPrimaryKey => table.PrimaryKey == this;
		/// <summary></summary>
		public bool IsIdentity => isIdentity;
		/// <summary>Is the value in this column an extended value.</summary>
		public bool IsExtended => isExtendedValue.Value;
		/// <summary>Has this column a parent/child relation.</summary>
		public bool IsRelationColumn => parentRelation != null;
		/// <summary>Parent column for the parent child relation.</summary>
		public PpsDataColumnDefinition ParentColumn => parentRelation.ParentColumn;

		public virtual bool IsInitialized => true;

		/// <summary>Zugriff auf die zugeordneten Meta-Daten der Spalte.</summary>
		public abstract PpsDataColumnMetaCollection Meta { get; }

		IPropertyEnumerableDictionary IDataColumn.Attributes => Meta;
	} // class PpsDataColumnDefinition

	#endregion
}
