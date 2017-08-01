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
using System.Collections;
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
		Description,
		/// <summary>Is this column nullable.</summary>
		Nullable,
		/// <summary>Default value for the column, if there is no value given (only used on client site database, currently)</summary>
		Default,
		/// <summary>Is the column source for the synchronization or load (default is the column name)</summary>
		SourceColumn
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
		/// <summary>Writes the value in the dataset.</summary>
		/// <param name="x"></param>
		void Write(XElement x);
		/// <summary>Reads the value.</summary>
		/// <param name="x"></param>
		void Read(XElement x);

		bool IsNull { get; }
	} // interface IPpsDataRowExtendedValue

	#endregion

	#region -- interface IPpsDataRowSetGenericValue -------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Does this extended value supports a generic setter</summary>
	public interface IPpsDataRowSetGenericValue : IPpsDataRowExtendedValue
	{
		/// <summary>Generic value</summary>
		/// <param name="inital"><c>true</c>, if the value is set with the initial value.</param>
		/// <param name="value">New value for the property.</param>
		/// <returns><c>true</c>, let fire a notify property changed on the row value</returns>
		bool SetGenericValue(bool inital, object value);

		/// <summary>Commit the value to the original</summary>
		void Commit();
		/// <summary>Reset the value from the original</summary>
		void Reset();
	} //	interface IPpsDataRowSetGenericValue

	#endregion

	#region -- interface IPpsDataRowGetGenericValue -------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Does this extended value supports a generic getter</summary>
	public interface IPpsDataRowGetGenericValue : IPpsDataRowExtendedValue
	{
		/// <summary>Generic value</summary>
		object Value { get; }
	} //	interface IPpsDataRowGetGenericValue

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

		void IPpsDataRowExtendedValue.Write(XElement x)
			=> Write(x);

		void IPpsDataRowExtendedValue.Read(XElement x)
			=> Read(x);

		protected abstract void Write(XElement x);

		protected abstract void Read(XElement x);

		public abstract bool IsNull { get; }

		public PpsDataRow Row => row;
		public PpsDataColumnDefinition Column => column;
	} // class PpsDataRowExtentedValue

	#endregion

	#region -- class PpsDataRowObjectExtendedValue --------------------------------------

	public abstract class PpsDataRowObjectExtendedValue : PpsDataRowExtentedValue, IPpsDataRowGetGenericValue, IPpsDataRowSetGenericValue
	{
		#region -- class PpsUndoDataValue -----------------------------------------------

		private sealed class PpsUndoDataValue : IPpsUndoItem
		{
			private readonly PpsDataRowObjectExtendedValue value;
			private readonly object oldKey;
			private readonly object newKey;

			public PpsUndoDataValue(PpsDataRowObjectExtendedValue value, object oldKey, object newKey)
			{
				this.value = value;
				this.oldKey = oldKey;
				this.newKey = newKey;
			} // ctor

			public void Freeze() { }

			public void Redo()
				=> value.SetGenericValue(newKey, false);

			public void Undo()
				=> value.SetGenericValue(oldKey, false);
		} // class PpsUndoDataValue

		#endregion
		
		private object originalValue = null; // original id
		private object value = null; // id to the master data row
		
		protected PpsDataRowObjectExtendedValue(PpsDataRow row, PpsDataColumnDefinition column)
			: base(row, column)
		{
		} // ctor

		/// <summary>Change the internal value.</summary>
		/// <param name="newValue"></param>
		/// <param name="firePropertyChanged"></param>
		/// <returns></returns>
		protected virtual bool SetGenericValue(object newValue, bool firePropertyChanged)
		{
			if (Object.Equals(InternalValue, newValue))
				return false;
			else
			{
				Row.Table.DataSet.UndoSink?.Append(
					new PpsUndoDataValue(this, value, newValue)
				);
				value = newValue;
				return true;
			}
		} // proc SetGenericValue

		protected virtual void Commit()
		{
			if (PpsDataRow.NotSet != value)
			{
				originalValue = value;
				value = PpsDataRow.NotSet;
			}
		} // proc Commit
		
		protected virtual void Reset()
		{
			var oldValue = value;
			value = PpsDataRow.NotSet;
			Row.Table.DataSet.UndoSink?.Append(
				new PpsUndoDataValue(this, oldValue, PpsDataRow.NotSet)
			);

			OnPropertyChanged(nameof(Value));
		} // proc Reset

		void IPpsDataRowSetGenericValue.Commit()
			=> Commit();

		void IPpsDataRowSetGenericValue.Reset()
			=> Reset();

		/// <summary>Writes the value as a normal value in the document data.</summary>
		/// <param name="x"></param>
		protected override void Write(XElement x)
		{
			// o
			if (originalValue == null)
				x.Add(new XElement("o"));
			else
				x.Add(new XElement("o", originalValue.ChangeType<string>()));
			// v
			if (value != PpsDataRow.NotSet)
			{
				if (value == null)
					x.Add(new XElement("v"));
				else
					x.Add(new XElement("v", value.ChangeType<string>()));
			}
		} // proc Write

		/// <summary>Reads the value as a normal value from the document data.</summary>
		/// <param name="x"></param>
		protected override void Read(XElement x)
		{
			object ReadValueFromElementstring(XElement t)
				=> t == null || String.IsNullOrEmpty(t.Value) ? null : (object)t.Value.ChangeType<long>();

			originalValue = ReadValueFromElementstring(x?.Element("o"));
			var xV = x?.Element("v");
			if (xV == null)
				value = PpsDataRow.NotSet;
			else
				value = ReadValueFromElementstring(xV);
		} // proc Read

		bool IPpsDataRowSetGenericValue.SetGenericValue(bool inital, object value)
			=> SetGenericValue(value, !inital);

		protected object InternalValue => value == PpsDataRow.NotSet ? originalValue : value;

		/// <summary>Equals the internal value <c>null</c>.</summary>
		public override bool IsNull => InternalValue == null;
		/// <summary>Value get implementation.</summary>
		public abstract object Value { get; }
	} // class PpsDataRowObjectExtendedValue

	#endregion

	#region -- class PpsDataColumnDefinition --------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Basisklasse für die Spaltendefinitionen.</summary>
	public abstract class PpsDataColumnDefinition : IDataColumn, IDynamicMetaObjectProvider
	{
		#region -- WellKnownTypes -------------------------------------------------------

		/// <summary>Definiert die bekannten Meta Informationen.</summary>
		private static readonly Dictionary<string, Type> wellknownMetaTypes = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
		{
			{ nameof(PpsDataColumnMetaData.MaxLength), typeof(int) },
			{ nameof(PpsDataColumnMetaData.Caption), typeof(string) },
			{ nameof(PpsDataColumnMetaData.Description), typeof(string) },
			{ nameof(PpsDataColumnMetaData.Nullable), typeof(bool) },
			{ nameof(PpsDataColumnMetaData.Default), typeof(string) },
			{ nameof(PpsDataColumnMetaData.SourceColumn), typeof(string) }
		};

		#endregion

		#region -- class PpsDataColumnStaticMetaCollection ------------------------------

		private sealed class PpsDataColumnStaticMetaCollection : IReadOnlyDictionary<string, object>
		{
			private readonly PpsDataColumnDefinition column;

			public PpsDataColumnStaticMetaCollection(PpsDataColumnDefinition column)
			{
				this.column = column;
			} // ctor

			public bool ContainsKey(string key) 
				=> staticProperties.ContainsKey(key);

			public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
			{
				foreach (var c in staticProperties)
					yield return new KeyValuePair<string, object>(c.Key, c.Value(column));
			} // func GetEnumerator

			IEnumerator IEnumerable.GetEnumerator() 
				=> GetEnumerator();

			public bool TryGetValue(string key, out object value)
			{
				Func<PpsDataColumnDefinition, object> func;
				if (staticProperties.TryGetValue(key, out func))
				{
					value = func(column);
					return true;
				}
				else
				{
					value = null;
					return false;
				}
			} // func TryGetValue

			public object this[string key]
			{
				get
				{
					object v;
					return TryGetValue(key, out v) ? v : null;
				}
			} // func this

			public IEnumerable<string> Keys
				=> staticProperties.Keys;

			public IEnumerable<object> Values
			{
				get
				{
					foreach (var c in staticProperties.Values)
						yield return c(column);
				}
			} // prop Values

			public int Count
				=> staticProperties.Count;

			private static Dictionary<string, Func<PpsDataColumnDefinition, object>> staticProperties = new Dictionary<string, Func<PpsDataColumnDefinition, object>>(StringComparer.OrdinalIgnoreCase)
			{
				["IsIdentity"] = new Func<PpsDataColumnDefinition, object>(c => c.IsIdentity),
				["IsPrimary"] = new Func<PpsDataColumnDefinition, object>(c => c.IsPrimaryKey)
			};
		} // class PpsDataColumnStaticMetaCollection

		#endregion

		#region -- class PpsDataColumnMetaCollection ------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		public class PpsDataColumnMetaCollection : PpsMetaCollection
		{
			private readonly PpsDataColumnStaticMetaCollection staticMeta;

			public PpsDataColumnMetaCollection(PpsDataColumnDefinition column)
			{
				this.staticMeta = new PpsDataColumnStaticMetaCollection(column);
			} // ctor

			protected PpsDataColumnMetaCollection(PpsDataColumnDefinition column, PpsDataColumnMetaCollection clone)
				: base(clone)
			{
				this.staticMeta = new PpsDataColumnStaticMetaCollection(column);
			} // ctor

			public T GetProperty<T>(PpsDataColumnMetaData key, T @default)
				=> PropertyDictionaryExtensions.GetProperty<T>(this, key.ToString(), @default);

			public override IReadOnlyDictionary<string, Type> WellknownMetaTypes => wellknownMetaTypes;
			protected override IReadOnlyDictionary<string, object> StaticKeys => staticMeta;
		} // class PpsDataColumnMetaCollection

		#endregion

		#region -- class PpsDataColumnMetaObject ----------------------------------------

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

		private void CheckParentRelationValue(PpsDataRow row, object value)
		{
			if (!ExistsValueInParentTable(row, value))
				throw new ArgumentOutOfRangeException($"Value '{value}' does not exist in '{parentRelation.ParentColumn.Table.Name}.{parentRelation.ParentColumn.Name}'.");
		} // proc CheckParentRelationValue

		private void CheckPrimaryKeyValue(PpsDataRow row, object value)
		{
			if (row.Table.FindRows(this, value).Where(c => c != row).FirstOrDefault() != null)
				throw new ArgumentOutOfRangeException($"Value '{value}' is not unique for column '{Table.Name}.{Name}'.");
		} // proc CheckPrimaryKeyValue

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

					if (IsRelationColumn) // check value contraint
					{
						row.ClearParentRowCache(this);

						if (value != null)
						{
							var t = row.Table.DataSet.DeferredConstraints;
							if (t == null)
								CheckParentRelationValue(row, value);
							else
								t.Register(new Action<PpsDataRow, object>(CheckParentRelationValue), "Foreign key constraint failed.", row, value);
						}
					}
					if (IsExtended)
					{
						// check for a internal interface to set a generic value
						if (oldValue is IPpsDataRowSetGenericValue v)
						{
							ret = v.SetGenericValue(initial, value);
							value = oldValue; // reset old value
						}
						else if (initial)
							value = oldValue;
						else
							throw new NotSupportedException($"It is not allowed to change this extended column ({Table.Name}.{Name}).");
					}
					if (IsPrimaryKey) // check unique
					{
						var t = row.Table.DataSet.DeferredConstraints;
						if (t == null)
							CheckPrimaryKeyValue(row, value);
						else
							t.Register(new Action<PpsDataRow, object>(CheckPrimaryKeyValue), "Primary key contraint failed.", row, value);
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

				if (oldValue != null && row.Table.DataSet.DeferredConstraints == null)
				{
					foreach (var childRow in table.FindRows(relation.ChildColumn, oldValue))
						childRow[columnIndex] = value;
				}
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
