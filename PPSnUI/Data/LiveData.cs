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
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Data;
using TecWare.DE.Networking;
using TecWare.DE.Stuff;
using TecWare.PPSn.Core.Data;
using TecWare.PPSn.Networking;
using TecWare.PPSn.UI;

namespace TecWare.PPSn.Data
{
	#region -- class PpsLiveTableAttribute --------------------------------------------

	/// <summary>Source view for the columns</summary>
	[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
	public sealed class PpsLiveTableAttribute : Attribute
	{
		/// <summary>Source view for the columns</summary>
		/// <param name="viewName">Name of the view.</param>
		public PpsLiveTableAttribute(string viewName)
			=> ViewName = viewName ?? throw new ArgumentNullException(nameof(viewName));

		/// <summary>Name of the server view.</summary>
		public string ViewName { get; }
	} // class PpsLiveTableAttribute

	#endregion

	#region -- class PpsLiveColumnAttribute -------------------------------------------

	/// <summary>Column binding definition for a property.</summary>
	[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
	public sealed class PpsLiveColumnAttribute : Attribute
	{
		/// <summary>Name of the field, default is the property-name.</summary>
		public string Field { get; set; } = null;
		/// <summary>Is this field a primary column (every PpsLiveDataRow needs a primary key)</summary>
		public bool IsPrimary { get; set; } = false;
	} // class PpsLiveColumnAttribute

	#endregion

	#region -- class PpsLiveParentRelationAttribute -----------------------------------

	/// <summary>Marks a property as an child to parent relation. The return value of the property must be a <see cref="PpsLiveDataRow"/> descendent.</summary>
	[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
	public sealed class PpsLiveParentRelationAttribute : Attribute
	{
		/// <summary>Marks a property as an child to parent relation.</summary>
		/// <param name="childField">Field in the current table, that is compared with the primary key in the parent table.</param>
		public PpsLiveParentRelationAttribute(string childField)
			=> ChildFields = new string[] { childField ?? throw new ArgumentNullException(nameof(childField)) };

		/// <summary>Marks a property as an child to parent relation.</summary>
		/// <param name="childFields">Fields in the current table, that is compared with the primary key in the parent table.</param>
		public PpsLiveParentRelationAttribute(params string[] childFields)
			=> ChildFields = PpsLiveChildRelationAttribute.TestFieldArray(childFields);

		/// <summary>Child fields of the relation.</summary>
		public string[] ChildFields { get; }
	} // class PpsLiveParentRelationAttribute

	#endregion

	#region -- class PpsLiveChildRelationAttribute ------------------------------------

	/// <summary>Marks the attribute a parent/child relation, the current primary is used to the select the child fields. The return
	/// value of the property must be a enumerator if <see cref="PpsLiveDataRow"/>.</summary>
	[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
	public sealed class PpsLiveChildRelationAttribute : Attribute
	{
		/// <summary>Marks the attribute a child relation, the current primary is used to the select the child fields.</summary>
		/// <param name="childField">Field in the other table, that is compared with current primary key.</param>
		public PpsLiveChildRelationAttribute(string childField)
			=> ChildFields = new string[] { childField ?? throw new ArgumentNullException(nameof(childField)) };

		/// <summary>Marks the attribute a child relation, the current primary is used to the select the child fields.</summary>
		/// <param name="childFields">Fields in the other table, that is compared with current primary key.</param>
		public PpsLiveChildRelationAttribute(params string[] childFields)
			=> ChildFields = TestFieldArray(childFields);

		internal static string[] TestFieldArray(string[] childFields)
		{
			if (childFields == null || childFields.Length == 0)
				throw new ArgumentNullException(nameof(childFields));

			for (var i = 0; i < childFields.Length; i++)
			{
				if (String.IsNullOrEmpty(childFields[i]))
					throw new ArgumentNullException(nameof(childFields));
			}

			return childFields;
		} // func TestFieldArray

		/// <summary>Child fields of the relation.</summary>
		public string[] ChildFields { get; }
	} // class PpsLiveChildRelationAttribute 

	#endregion

	#region -- interface IPpsLiveRowView ----------------------------------------------

	/// <summary>Base row view interface.</summary>
	public interface IPpsLiveRowViewBase : IDataColumns, IDataRow, INotifyPropertyChanged
	{
		/// <summary>Enforce a refresh of the view.</summary>
		/// <returns></returns>
		Task RefreshAsync();

		/// <summary>Assigend data set.</summary>
		PpsLiveData Data { get; }
	} // interface IPpsLiveRowViewBase

	/// <summary>Generic row view interface.</summary>
	public interface IPpsLiveRowView : IPpsLiveRowViewBase
	{
		/// <summary>Change the current key.</summary>
		/// <param name="key"></param>
		void SetKey(params object[] key);

		/// <summary>Current row or <c>null</c> if there is no row with this key.</summary>
		PpsLiveDataRow Row { get; }
		/// <summary>Return the key</summary>
		IReadOnlyList<object> Key { get; }
	} // interface IPpsLiveRowView

	/// <summary></summary>
	/// <typeparam name="T"></typeparam>
	/// <typeparam name="TKEY"></typeparam>
	public interface IPpsLiveRowView<T, TKEY> : IPpsLiveRowViewBase
		where T : PpsLiveDataRow
	{
		/// <summary>Current row or <c>null</c> if there is no row with this key.</summary>
		T Row { get; }

		/// <summary>Return the key</summary>
		TKEY Key { get; set; }
	} // interface IPpsLiveRowView

	/// <summary></summary>
	/// <typeparam name="T"></typeparam>
	public interface IPpsLiveRowView<T> : IPpsLiveRowViewBase
		where T : PpsLiveDataRow
	{
		/// <summary>Current row or <c>null</c> if there is no row with this key.</summary>
		T Row { get; }
	} // interface IPpsLiveRowView

	#endregion

	#region -- interface IPpsLiveTableView --------------------------------------------

	/// <summary>Untyped interface for live data table.</summary>
	public interface IPpsLiveTableViewBase : IDataColumns, IEnumerable, IList, INotifyCollectionChanged
	{
		/// <summary>Refresh data.</summary>
		/// <returns></returns>
		Task RefreshAsync();

		/// <summary>Assigend data set.</summary>
		PpsLiveData Data { get; }
	} // interface IPpsLiveTableView

	/// <summary>Untyped interface for live data table.</summary>
	public interface IPpsLiveTableView : IPpsLiveTableViewBase
	{
		/// <summary>Create a related view, that is based on the current view.</summary>
		/// <param name="order">New order expression.</param>
		/// <returns>New view.</returns>
		IPpsLiveTableView CreateView(params PpsDataOrderExpression[] order);
		/// <summary>Create a related view, that is based on the current view.</summary>
		/// <param name="filter">Additional filter expression.</param>
		/// <param name="order">New order expression.</param>
		/// <returns>New view.</returns>
		IPpsLiveTableView CreateView(PpsDataFilterExpression filter, params PpsDataOrderExpression[] order);

		/// <summary>Find rows by index.</summary>
		/// <param name="values">Indexed values.</param>
		/// <returns></returns>
		IEnumerable<PpsLiveDataRow> FindRows(params object[] values);

		/// <summary>Enumerated typed rows.</summary>
		IEnumerable<PpsLiveDataRow> Rows { get; }
	} // interface IPpsLiveTableView

	/// <summary>Typed interface for live data table.</summary>
	/// <typeparam name="T"></typeparam>
	public interface IPpsLiveTableView<T> : IPpsLiveTableViewBase
		where T : PpsLiveDataRow
	{
		/// <summary>Create a related view, that is based on the current view.</summary>
		/// <param name="order">New order expression.</param>
		/// <returns>New view.</returns>
		IPpsLiveTableView<T> CreateView(params PpsDataOrderExpression[] order);
		/// <summary>Create a related view, that is based on the current view.</summary>
		/// <param name="filter">Additional filter expression.</param>
		/// <param name="order">New order expression.</param>
		/// <returns>New view.</returns>
		IPpsLiveTableView<T> CreateView(PpsDataFilterExpression filter, params PpsDataOrderExpression[] order);

		/// <summary>Find rows by index.</summary>
		/// <param name="values">Indexed values (the sort key).</param>
		/// <returns></returns>
		IEnumerable<T> FindRows(params object[] values);

		/// <summary>Enumerated typed rows.</summary>
		IEnumerable<T> Rows { get; }

		/// <summary>Index</summary>
		/// <param name="index"></param>
		/// <returns></returns>
		new T this[int index] { get; }
	} // class IPpsLiveTableView

	/// <summary>Typed interface for live data table.</summary>
	/// <typeparam name="T"></typeparam>
	public interface IPpsLiveTableFilterView<T> : IPpsLiveTableView<T>
		where T : PpsLiveDataRow
	{
		/// <summary>Set a new filter for the live view.</summary>
		/// <param name="filterExpression"></param>
		void SetFilter(PpsDataFilterExpression filterExpression);

		/// <summary>Current filter expression.</summary>
		PpsDataFilterExpression Filter { get; }
	} // interface IPpsLiveTableFilterView

	#endregion

	#region -- interface IPpsLiveDataRowSource ----------------------------------------

	/// <summary>View on row data, mapped to PpsLiveDataRow layout.</summary>
	internal interface IPpsLiveDataRowSource
	{
		bool IsModified(int index);

		object this[int index] { get; }
		object Key { get; }
	} // interface IPpsLiveDataRowSource

	#endregion

	#region -- interface IPpsLiveDataViewEvents ---------------------------------------

	/// <summary>View change events, already mapped to the correct PpsLiveDataRow layout.</summary>
	internal interface IPpsLiveDataViewEvents
	{
		/// <summary>New row received or changed.</summary>
		/// <param name="source"></param>
		void OnRowChanged(IPpsLiveDataRowSource source);
		/// <summary>Row removed from base table.</summary>
		/// <param name="source"></param>
		void OnRowRemoved(IPpsLiveDataRowSource source);

		/// <summary>Column layout for the data.</summary>
		PpsLiveDataRowType Type { get; }
		/// <summary>Return the row filter for the change events.</summary>
		PpsDataFilterExpression Filter { get; }
	} // interface IPpsLiveDataViewEvents

	#endregion

	#region -- interface IPpsLiveDataTableInfo ----------------------------------------

	/// <summary>Access to the live data table.</summary>
	public interface IPpsLiveDataTableInfo
	{
		/// <summary>Schedule a refresh for the whole table and all connected views.</summary>
		void EnqueueForRefresh();
		/// <summary>Do a refresh for the whole table and all connected views.</summary>
		/// <returns></returns>
		Task RefreshAsync();

		/// <summary>Live data owner</summary>
		PpsLiveData Data { get; }
		/// <summary>Name of the table.</summary>
		string Name { get; }
	} // interface IPpsLiveDataTableInfo

	#endregion

	#region -- interface IPpsLiveDataTable --------------------------------------------

	internal interface IPpsLiveDataTable : IPpsLiveDataTableInfo
	{
		void Register(IPpsLiveDataViewEvents events);
	} // interface IPpsLiveDataTable

	#endregion

	#region -- class PpsLiveDataColumn ------------------------------------------------

	/// <summary>Live data column information.</summary>
	public sealed class PpsLiveDataColumn : SimpleDataColumn
	{
		internal PpsLiveDataColumn(string name, Type dataType, string propertyName, bool isPrimaryKey)
			: base(name, dataType, null)
		{
			PropertyName = propertyName;
			IsPrimaryKey = isPrimaryKey;
		} // ctor

		/// <summary>Is this column mapped to an property.</summary>
		public bool HasProperty => PropertyName != null;
		/// <summary>Mapped properties</summary>
		public string PropertyName { get; }

		/// <summary>Is this column a primary key.</summary>
		public bool IsPrimaryKey { get; }
	} // class PpsLiveDataColumn

	#endregion

	#region -- class PpsLiveDataRelation ----------------------------------------------

	/// <summary>Relation between to tables.</summary>
	public sealed class PpsLiveDataRelation : IEquatable<PpsLiveDataRelation>
	{
		private readonly PpsLiveDataRowType parentType;
		private readonly PpsLiveDataRowType childType;
		private readonly int[] parentColumnIndices;
		private readonly int[] childColumnIndices;

		private readonly int hashCode;

		#region -- Ctor/Dtor ----------------------------------------------------------

		private PpsLiveDataRelation(int hashCode, PpsLiveDataRowType parentType, int[] parentColumnIndices, PpsLiveDataRowType childType, int[] childColumnIndices)
		{
			this.hashCode = hashCode;
			this.parentType = parentType;
			this.parentColumnIndices = parentColumnIndices;
			this.childType = childType;
			this.childColumnIndices = childColumnIndices;
		} // ctor

		/// <inheritdoc/>
		public override bool Equals(object obj)
			=> ReferenceEquals(this, obj) || (obj is PpsLiveDataRelation o ? Equals(o) : false);

		/// <inheritdoc/>
		public bool Equals(PpsLiveDataRelation other)
			=> hashCode == other.hashCode && Equals(other.parentType, other.parentColumnIndices, other.childType, other.childColumnIndices);

		private bool Equals(PpsLiveDataRowType otherParentType, int[] otherParentColumnIndices, PpsLiveDataRowType otherChildType, int[] otherChildColumnIndices)
		{
			return parentType == otherParentType
				&& childType == otherChildType
				&& CompareArray(parentColumnIndices, otherParentColumnIndices)
				&& CompareArray(childColumnIndices, otherChildColumnIndices);
		} // func Equals

		/// <inheritdoc/>
		public override int GetHashCode()
			=> hashCode;

		private static bool CompareArray(int[] a, int[] b)
		{
			var l = a.Length;
			var l2 = b.Length;
			if (l != l2)
				return false;

			for (var i = 0; i < l; i++)
			{
				if (a[i] != b[i])
					return false;
			}

			return true;
		} // func CompareArray

		private static int GetArrayHashCode(int[] a)
		{
			var r = 0;
			var l = a.Length;
			for (var i = 0; i < l; i++)
				r ^= a[i].GetHashCode();
			return r;
		} // func GetArrayHashCode

		#endregion

		#region -- Relations ----------------------------------------------------------

		private void CheckChildRowType(PpsLiveDataRow childRow)
		{
			if (childType != childRow.Type)
				throw new ArgumentOutOfRangeException(nameof(childRow), childRow.Type.Name, $"Relation is not a parent -> child relation for the child row {childRow.Type} (Expected type is: {childType.Name}).");
		} // proc CheckChildRowType

		private void CheckParentRowType(PpsLiveDataRow parentRow)
		{
			if (parentType != parentRow.Type)
				throw new ArgumentOutOfRangeException(nameof(parentRow), parentRow.Type.Name, $"Relation is not a parent -> child relation for the parent row {parentRow.Type} (Expected type is: {parentType.Name}).");
		} // proc CheckParentRowType

		/// <summary>Create a relation.</summary>
		/// <param name="childRow"></param>
		/// <returns></returns>
		public IPpsLiveRowView CreateParentRelation(PpsLiveDataRow childRow)
		{
			CheckChildRowType(childRow);

			return parentType.CreateParentRow(childRow, childColumnIndices);
		} // func CreateParentRelation

		/// <summary>Create a typed relation.</summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="childRow"></param>
		/// <returns></returns>
		public IPpsLiveRowView<T> CreateParentRelation<T>(PpsLiveDataRow childRow)
			where T : PpsLiveDataRow
		{
			CheckChildRowType(childRow);

			var newRowType = PpsLiveDataRowType.Get<T>();
			if (parentType != newRowType)
				throw new ArgumentOutOfRangeException(nameof(T), $"Return type '{newRowType.Name}' is not '{parentType.Name}'.");

			return parentType.CreateParentRow<T>(childRow, childColumnIndices);
		} // func CreateParentRelation

		/// <summary></summary>
		/// <param name="parentRow"></param>
		/// <returns></returns>
		public IPpsLiveTableView CreateChildRelation(PpsLiveDataRow parentRow)
		{
			CheckParentRowType(parentRow);

			return childType.CreateChildView(parentRow, childColumnIndices);
		} // func CreateChildRelation

		/// <summary></summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="parentRow"></param>
		/// <returns></returns>
		public IPpsLiveTableView<T> CreateChildRelation<T>(PpsLiveDataRow parentRow)
			where T : PpsLiveDataRow
		{
			CheckParentRowType(parentRow);

			var newRowType = PpsLiveDataRowType.Get<T>();
			if (childType != newRowType)
				throw new ArgumentOutOfRangeException(nameof(T), $"Return type '{newRowType.Name}' is not '{childType.Name}'.");

			return childType.CreateChildView<T>(parentRow, childColumnIndices);
		} // func CreateChildRelation

		#endregion

		/// <summary>Type description for the parent view.</summary>
		public PpsLiveDataRowType ParentType => parentType;
		/// <summary>Type description for the child view.</summary>
		public PpsLiveDataRowType ChildType => childType;

		#region -- Static - Relation List ---------------------------------------------

		private static readonly List<PpsLiveDataRelation> relations = new List<PpsLiveDataRelation>();

		private static int GetRelationHashCode(PpsLiveDataRowType parentType, int[] parentColumnIndices, PpsLiveDataRowType childType, int[] childColumnIndices)
		{
			if (parentType == null)
				throw new ArgumentNullException(nameof(parentType));
			if (parentColumnIndices == null || parentColumnIndices.Length == 0)
				throw new ArgumentNullException(nameof(parentColumnIndices));
			if (childType == null)
				throw new ArgumentNullException(nameof(childType));
			if (childColumnIndices == null || childColumnIndices.Length == 0)
				throw new ArgumentNullException(nameof(childColumnIndices));
			if (childColumnIndices.Length != parentColumnIndices.Length)
				throw new ArgumentOutOfRangeException(nameof(parentColumnIndices), "Column length is not equal.");

			return parentType.GetHashCode()
				^ childType.GetHashCode()
				^ GetArrayHashCode(parentColumnIndices)
				^ GetArrayHashCode(childColumnIndices);
		} // func GetRelationHashCode

		private static int Find(int otherHashCode, PpsLiveDataRowType otherParentType, int[] otherParentColumnIndices, PpsLiveDataRowType otherChildType, int[] otherChildColumnIndices)
		{
			var min = 0;
			var max = relations.Count - 1;
			while (min <= max)
			{
				var m = min + (max - min >> 1);
				var r = relations[m];
				var cmp = r.hashCode - otherHashCode;
				if (cmp == 0)
				{
					// compare current
					if (r.Equals(otherParentType, otherParentColumnIndices, otherChildType, otherChildColumnIndices))
						return m;

					// search before
					var i = m - 1;
					while (i > 0 && relations[i].hashCode == otherHashCode)
					{
						if (r.Equals(otherParentType, otherParentColumnIndices, otherChildType, otherChildColumnIndices))
							return i;
						i--;
					}

					// search after
					i = m + 1;
					while (i < relations.Count && relations[i].hashCode == otherHashCode)
					{
						if (r.Equals(otherParentType, otherParentColumnIndices, otherChildType, otherChildColumnIndices))
							return i;
						i++;
					}

					return ~i;
				}
				if (cmp < 0)
					min = m + 1;
				else
					max = m - 1;
			}
			return ~min;
		} // func Find

		/// <summary>Create a relation.</summary>
		/// <param name="parentType"></param>
		/// <param name="parentColumnIndices"></param>
		/// <param name="childType"></param>
		/// <param name="childColumnIndices"></param>
		/// <returns></returns>
		public static PpsLiveDataRelation Get(PpsLiveDataRowType parentType, int[] parentColumnIndices, PpsLiveDataRowType childType, int[] childColumnIndices)
		{
			var hashCode = GetRelationHashCode(parentType, parentColumnIndices, childType, childColumnIndices);
			lock (relations)
			{
				var index = Find(hashCode, parentType, parentColumnIndices, childType, childColumnIndices);
				if (index >= 0)
					return relations[index];
				else
				{
					var r = new PpsLiveDataRelation(hashCode, parentType, parentColumnIndices, childType, childColumnIndices);
					relations.Insert(~index, r);
					return r;
				}
			}
		} // func Get

		/// <summary>Return all known relations.</summary>
		public static IReadOnlyList<PpsLiveDataRelation> Relations => relations;

		#endregion
	} // class PpsLiveDataRelation

	#endregion

	#region -- class PpsLiveDataRowType -----------------------------------------------

	/// <summary>DataRow description.</summary>
	[DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
	public sealed class PpsLiveDataRowType : IDataColumns
	{
		private readonly Type rowType;
		private readonly Lazy<Type> keyType;
		private readonly Lazy<Type> rowViewType;
		private readonly Lazy<Type> liveKeyType;
		private readonly Lazy<Type> tableViewType;

		private readonly PpsLiveTableAttribute tableAttribute;
		private readonly PpsLiveDataColumn[] columns;

		private readonly Dictionary<string, PpsLiveDataRelation> relations = new Dictionary<string, PpsLiveDataRelation>();

		#region -- Ctor/Dtor ----------------------------------------------------------

		private PpsLiveDataRowType(Type rowType)
		{
			this.rowType = rowType ?? throw new ArgumentNullException(nameof(rowType));

			// check type restrictions
			if (rowType == typeof(PpsLiveDataRow) || rowType.BaseType != typeof(PpsLiveDataRow))
				throw new ArgumentException($"{rowType.Name} type must be derived from {nameof(PpsLiveDataRow)} (direct).", nameof(rowType));

			// view information
			tableAttribute = rowType.GetCustomAttribute<PpsLiveTableAttribute>();

			// collect columns
			var hasPrimaryKeyDefinition = false;

			var newColumns = new List<PpsLiveDataColumn>();
			var parentRelationProperties = new List<Tuple<PropertyInfo, PpsLiveParentRelationAttribute>>();
			var childRelationProperties = new List<Tuple<PropertyInfo, PpsLiveChildRelationAttribute>>();
			foreach (var pi in rowType.GetRuntimeProperties().Where(c => c.GetMethod != null && !c.GetMethod.IsStatic && c.GetMethod.IsPublic))
			{
				// check for column
				var columnAttribute = pi.GetCustomAttribute<PpsLiveColumnAttribute>();
				if (columnAttribute != null)
				{
					if (columnAttribute.IsPrimary)
						hasPrimaryKeyDefinition = true;

					newColumns.Add(new PpsLiveDataColumn(columnAttribute.Field, pi.PropertyType, pi.Name, columnAttribute.IsPrimary));
				}

				// check for relation
				var parentRelationAttribute = pi.GetCustomAttribute<PpsLiveParentRelationAttribute>();
				if (parentRelationAttribute != null)
				{
					if (columnAttribute != null)
						throw new ArgumentException($"Column attribute is already set for {rowType.Name}.{pi.Name}.");

					parentRelationProperties.Add(new Tuple<PropertyInfo, PpsLiveParentRelationAttribute>(pi, parentRelationAttribute));
				}

				var childRelationAttribute = pi.GetCustomAttribute<PpsLiveChildRelationAttribute>();
				if (childRelationAttribute != null)
				{
					if (columnAttribute != null)
						throw new ArgumentException($"Column attribute is already set for {rowType.Name}.{pi.Name}.");
					if (parentRelationAttribute != null)
						throw new ArgumentException($"Parent relation is already set for {rowType.Name}.{pi.Name}.");

					childRelationProperties.Add(new Tuple<PropertyInfo, PpsLiveChildRelationAttribute>(pi, childRelationAttribute));
				}
			}
			columns = newColumns.ToArray();
			keyType = new Lazy<Type>(CreateKeyType);
			rowViewType = new Lazy<Type>(CreateRowViewType);
			tableViewType = new Lazy<Type>(CreateTableViewType);
			liveKeyType = new Lazy<Type>(CreateLiveKeyType);

			types.Add(rowType, this);

			// check primary key
			if (!hasPrimaryKeyDefinition)
				throw new ArgumentException($"No primary key defined for {rowType.Name}.");

			// resolve relations
			foreach (var cur in parentRelationProperties)
			{
				var pi = cur.Item1;
				var parentRelationAttribute = cur.Item2;

				// get data type
				var parentType = Get(pi.GetMethod.ReturnType);
				var parentColumns = parentType.GetPrimaryKeyFields();
				var childColumns = this.FindColumnIndices(true, parentRelationAttribute.ChildFields);

				relations.Add(pi.Name, PpsLiveDataRelation.Get(parentType, parentColumns, this, childColumns));
			}
			foreach (var cur in childRelationProperties)
			{
				var pi = cur.Item1;
				var childRelationAttribute = cur.Item2;

				var parentColumns = GetPrimaryKeyFields();
				var childType = Get(GetRowType(pi.GetMethod.ReturnType));
				var childColumns = childType.FindColumnIndices(true, childRelationAttribute.ChildFields);

				relations.Add(pi.Name, PpsLiveDataRelation.Get(this, parentColumns, childType, childColumns));
			}
		} // ctor

		private string GetDebuggerDisplay()
			=> $"LiveDataRowType: {rowType.Name}";

		private int[] GetPrimaryKeyFields()
		{
			var r = new List<int>();
			for (var i = 0; i < columns.Length; i++)
			{
				if (columns[i].IsPrimaryKey)
					r.Add(i);
			}
			return r.ToArray();
		} // func GetPrimaryKeyFields

		private Type CreateKeyType()
		{
			// create primary key
			var primaryFields = GetPrimaryKeyFields();
			if (primaryFields.Length == 1)
				return columns[primaryFields[0]].DataType;
			else if (primaryFields.Length > 1 && primaryFields.Length <= 8) // contruct tuple type
			{
				var types = new Type[primaryFields.Length];
				for (var i = 0; i < types.Length; i++)
					types[i] = columns[primaryFields[i]].DataType;
				return valueTuples[types.Length - 1].MakeGenericType(types);
			}
			else
				throw new ArgumentOutOfRangeException("primaryKey");
		} // func CreateKeyType

		private Type CreateRowViewType()
		{
			var rowViewType = typeof(PpsLiveDataRowViewImpl<,>);
			return rowViewType.MakeGenericType(rowType, keyType.Value);
		} // func CreateRowViewType

		private Type CreateTableViewType()
		{
			var tableViewType = typeof(PpsLiveTableViewImpl<>);
			return tableViewType.MakeGenericType(rowType);
		} // func CreateTableViewType

		private Type CreateLiveKeyType()
		{
			var typeLiveKey = typeof(TypedLiveKey<>);
			return typeLiveKey.MakeGenericType(rowType, keyType.Value);
		} // func CreateRowViewType

		private void CheckRowType(Type rowType)
		{
			if (rowType != this.rowType)
				throw new ArgumentException($"Row type {rowType.Name} are not compatible with {this.rowType.Name}.");
		} // func CheckRowType

		private Type GetRowType(Type returnType)
		{
			if (returnType.GetGenericTypeDefinition() != typeof(IPpsLiveTableView<>))
				throw new ArgumentException($"The return type of a child relation must be IPpsLiveTableView<>.");

			return returnType.GetGenericArguments()[0];
		} // func GetRowType

		internal IPpsLiveDataTable GetDataTable(PpsLiveData data)
		{
			var table = tableAttribute != null
				? data.GetTable(tableAttribute.ViewName)
				: null;
			if (table == null)
				throw new ArgumentNullException(nameof(table), $"{nameof(PpsLiveTableAttribute)} is missing on {rowType.Name}.");
			return table;
		} // func GetDataTable

		#endregion

		#region -- CreateRow ----------------------------------------------------------

		#region -- class PpsLiveDataRowViewBase ---------------------------------------

		private abstract class PpsLiveDataRowViewBase<T> : DynamicDataRow, IPpsLiveRowViewBase, IPpsLiveDataViewEvents, INotifyPropertyChanged
			where T : PpsLiveDataRow
		{
			public event PropertyChangedEventHandler PropertyChanged;

			private readonly PpsLiveData data;
			private readonly PpsLiveDataRowType type;
			private T row = null;
			private PpsDataFilterExpression filter = PpsDataFilterExpression.False;

			#region -- Ctor/Dtor ------------------------------------------------------

			protected PpsLiveDataRowViewBase(PpsLiveDataRowType type, PpsLiveData data)
			{
				this.type = type ?? throw new ArgumentNullException(nameof(type));
				this.data = data ?? throw new ArgumentNullException(nameof(data));
				type.CheckRowType(typeof(T));

				type.GetDataTable(data).Register(this);
			} // ctor

			private void OnPropertyChanged(string propertyName)
				=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

			private void Row_ColumnChanged(object sender, PpsLiveDataColumnChangedEventArgs e)
				=> OnPropertyChanged(Columns[e.Index].Name);

			#endregion

			#region -- row changed ----------------------------------------------------

			public Task RefreshAsync()
				=> Data.RefreshViewAsync(this);

			void IPpsLiveDataViewEvents.OnRowChanged(IPpsLiveDataRowSource source)
			{
				if (!Equals(source.Key, LiveKey))
					return;

				var rowChanged = false;
				if (row == null)
				{
					row = (T)type.CreateEmptyRow(data);
					row.ColumnChanged += Row_ColumnChanged;
					rowChanged = true;
				}

				row.Set(source);

				if (rowChanged)
					OnRowChanged();
			} // proc IPpsLiveDataViewEvents.OnRowChanged

			void IPpsLiveDataViewEvents.OnRowRemoved(IPpsLiveDataRowSource source)
			{
				if (Equals(source.Key, LiveKey))
					ClearRow();
			} // proc IPpsLiveDataViewEvents.OnRowRemoved

			private void ClearRow()
			{
				if (row != null)
				{
					row.ColumnChanged -= Row_ColumnChanged;
					row = null;
					OnRowChanged();
				}
			} // proc ClearRow

			protected void FireLiveKeyChanged()
			{
				// remove row, key is different
				ClearRow();
				// refresh filter
				UpdateFilter();
				// refresh row asynchron
				data.EnqueueRefreshView(this);
			} // proc FireLiveKeyChanged

			private void OnRowChanged()
			{
				OnPropertyChanged(nameof(Row));
				foreach (var col in type.Columns)
					OnPropertyChanged(col.Name);
			} // proc OnRowChanged

			private void UpdateFilter()
			{
				var key = LiveKey;
				if (key == null)
					filter = PpsDataFilterExpression.False;
				else
				{
					var keyValues = (IReadOnlyList<object>)key;
					var primaryKey = type.GetPrimaryKeyFields();
					var filterParts = new PpsDataFilterExpression[primaryKey.Length];
					for (var i = 0; i < primaryKey.Length; i++)
						filterParts[i] = PpsDataFilterExpression.Compare(type.columns[primaryKey[i]].Name, PpsDataFilterCompareOperator.Equal, keyValues[0]);

					filter = PpsDataFilterExpression.Combine(filterParts).Reduce();
				}
			} // proc UpdateFilter

			PpsLiveDataRowType IPpsLiveDataViewEvents.Type => type;
			PpsDataFilterExpression IPpsLiveDataViewEvents.Filter => filter;

			#endregion

			public override IReadOnlyList<IDataColumn> Columns => type.Columns;
			public override bool IsDataOwner => true;
			public override object this[int index] => row?[index];

			/// <summary>Return row type</summary>
			public PpsLiveDataRowType RowType => type;
			/// <summary>Return the row.</summary>
			public T Row => row;
			/// <summary>Live key of the row</summary>
			protected abstract LiveKeyBase LiveKey { get; }

			/// <inheritdoc/>
			public PpsLiveData Data => data;
		} // class PpsLiveDataRowViewBase

		#endregion

		#region -- class PpsLiveDataRowViewImpl ---------------------------------------

		private sealed class PpsLiveDataRowViewImpl<T, TKEY> : PpsLiveDataRowViewBase<T>, IPpsLiveRowView<T, TKEY>, IPpsLiveRowView
			where T : PpsLiveDataRow
		{
			private LiveKeyBase key = null;

			#region -- Ctor/Dtor ------------------------------------------------------

			public PpsLiveDataRowViewImpl(PpsLiveDataRowType type, PpsLiveData data)
				: base(type, data)
			{
			} // ctor

			public PpsLiveDataRowViewImpl(PpsLiveDataRowType type, PpsLiveData data, TKEY key)
				: this(type, data)
			{
				SetValueKey(type.CreateKey(key));
			} // ctor

			void IPpsLiveRowView.SetKey(params object[] key)
				=> SetValueKey(RowType.CreateKey(key));

			private void SetValueKey(LiveKeyBase newKey)
			{
				if (!Equals(newKey, key))
				{
					key = newKey;
					FireLiveKeyChanged();
				}
			} // proc SetValueKey

			#endregion

			protected override LiveKeyBase LiveKey => key;

			/// <summary>Primary key of the row.</summary>
			public TKEY Key { get => RowType.ToKeyValue<TKEY>(key); set => SetValueKey(RowType.CreateKey(value)); }
			/// <summary>Return the row generic</summary>
			PpsLiveDataRow IPpsLiveRowView.Row => Row;
			/// <summary>Return the key generic</summary>
			IReadOnlyList<object> IPpsLiveRowView.Key => key;
		} // class PpsLiveDataRowViewImpl

		#endregion

		#region -- class PpsLiveParentRelationRowViewImpl -----------------------------

		private sealed class PpsLiveParentRelationRowViewImpl<T> : PpsLiveDataRowViewBase<T>, IPpsLiveRowView<T>, IPpsLiveRowView
			where T : PpsLiveDataRow
		{
			private readonly int[] childFieldIndices;
			private readonly PpsLiveDataRow childRow;
			private LiveKeyBase liveKey = null;

			public PpsLiveParentRelationRowViewImpl(PpsLiveDataRowType type, PpsLiveDataRow childRow, int[] childFieldIndices)
				: base(type, childRow.Data)
			{
				this.childFieldIndices = childFieldIndices;
				this.childRow = childRow;

				childRow.ColumnChanged += ChildRow_ColumnChanged;

				UpdateKey();
			} // ctor

			private void ChildRow_ColumnChanged(object sender, PpsLiveDataColumnChangedEventArgs e)
			{
				var updateKey = false;
				for (var i = 0; i < childFieldIndices.Length; i++)
				{
					if (childFieldIndices[i] == e.Index)
						updateKey = true;
				}
				if (updateKey)
					UpdateKey();
			} // event ChildRow_ColumnChanged

			private void UpdateKey()
			{
				var values = new object[childFieldIndices.Length];

				for (var i = 0; i < values.Length; i++)
					values[i] = childRow[childFieldIndices[i]];
				liveKey = LiveKeyBase.CreateValueKeyDirect(values);

				FireLiveKeyChanged();
			} // proc UpdateKey

			void IPpsLiveRowView.SetKey(params object[] key)
				=> throw new NotSupportedException();

			PpsLiveDataRow IPpsLiveRowView.Row => Row;
			IReadOnlyList<object> IPpsLiveRowView.Key => liveKey;

			protected override LiveKeyBase LiveKey => liveKey;
		} // class ParentRelationRowView

		#endregion

		#region -- class TypedLiveKey -------------------------------------------------

		private sealed class TypedLiveKey<TKEY> : LiveKeyBase
		{
			private readonly TKEY value;

			public TypedLiveKey(TKEY value)
				=> this.value = value;

			protected override bool IsSameReference(LiveKeyBase other)
				=> other is TypedLiveKey<TKEY> k && ReferenceEquals(value, k.value);

			protected override object this[int index] => index == 0 ? value : throw new ArgumentOutOfRangeException(nameof(index), index, "Invalid key index.");
			protected override int Count => 1;
		} // class TypeLiveKey

		#endregion

		private static readonly Type[] valueTuples = new Type[]
		{
			null, // typeof(ValueTuple< >), is not allowed
			typeof(ValueTuple<,>),
			typeof(ValueTuple<,,>),
			typeof(ValueTuple<,,,>),
			typeof(ValueTuple<,,,,>),
			typeof(ValueTuple<,,,,,>),
			typeof(ValueTuple<,,,,,,>),
			typeof(ValueTuple<,,,,,,,>)
		}; // valueTuples

		private static readonly string[] valueTuplePropertyNames = new string[]
		{
			nameof(ValueTuple<int,int,int,int,int,int,int,int>.Item1),
			nameof(ValueTuple<int,int,int,int,int,int,int,int>.Item2),
			nameof(ValueTuple<int,int,int,int,int,int,int,int>.Item3),
			nameof(ValueTuple<int,int,int,int,int,int,int,int>.Item4),
			nameof(ValueTuple<int,int,int,int,int,int,int,int>.Item5),
			nameof(ValueTuple<int,int,int,int,int,int,int,int>.Item6),
			nameof(ValueTuple<int,int,int,int,int,int,int,int>.Item7),
			nameof(ValueTuple<int,int,int,int,int,int,int,int>.Rest),
		}; // valueTuplePropertyNames

		private static int GetValueTypeCount(Type keyType)
		{
			if (keyType.IsGenericTypeDefinition)
			{
				var gt = keyType.GetGenericTypeDefinition();
				for (var i = 0; i < valueTuples.Length; i++)
					if (gt == valueTuples[i])
						return i;
			}
			return -1;
		} // func GetValueTypeCount

		private object GetValueTupleValue<TKEY>(int i, TKEY key)
			=> typeof(TKEY).GetProperty(valueTuplePropertyNames[i]).GetValue(key);

		private LiveKeyBase CreateKey(object[] key)
		{
			var kt = keyType.Value;
			var c = GetValueTypeCount(kt);
			if (c == -1) // single value
			{
				if (key.Length != 1)
					throw new ArgumentOutOfRangeException(nameof(key));

				return (LiveKeyBase)Activator.CreateInstance(liveKeyType.Value, Procs.ChangeType(key[0], kt));
			}
			else // ValueTuple -> create a correct value key
			{
				var primaryKeys = GetPrimaryKeyFields();
				if (primaryKeys.Length != key.Length)
					throw new ArgumentOutOfRangeException(nameof(key), "Key length is different.");

				var values = new object[primaryKeys.Length];
				for (var i = 0; i < primaryKeys.Length; i++)
					values[i] = Convert.ChangeType(key[i], columns[primaryKeys[i]].DataType);

				return LiveKeyBase.CreateValueKeyDirect(values);
			}
		} // func CreateKey

		private LiveKeyBase CreateKey<TKEY>(TKEY key)
		{
			var kt = keyType.Value;
			var c = GetValueTypeCount(kt);
			if (c == -1) // single value key
			{
				if (typeof(TKEY) == kt)
					return new TypedLiveKey<TKEY>(key);
				else
					return CreateKey(new object[] { key });
			}
			else // ValueTuple -> create a value key
			{
				var c2 = GetValueTypeCount(typeof(TKEY));
				if (c != c2)
					throw new ArgumentOutOfRangeException(nameof(key), "Key length is different.");

				var values = new object[c];
				if (kt == typeof(TKEY)) // key is compatible -> create array
				{
					for (var i = 0; i < c; i++)
						values[i] = GetValueTupleValue(i, key);
				}
				else // key is not compatible -> convert array
				{
					var primaryKeys = GetPrimaryKeyFields();
					for (var i = 0; i < c; i++)
						values[i] = Convert.ChangeType(GetValueTupleValue(i, key), columns[primaryKeys[i]].DataType);
				}
				return LiveKeyBase.CreateValueKeyDirect(values);
			}
		} // func CreateKey

		private TKEY ToKeyValue<TKEY>(LiveKeyBase liveKey)
		{
			var kt = keyType.Value;
			var c = GetValueTypeCount(kt);
			var keyValues = (IReadOnlyList<object>)liveKey;
			if (c == -1)
			{
				if (keyValues.Count != 1)
					throw new ArgumentOutOfRangeException(nameof(liveKey), "Key is incompatible.");
				return (TKEY)keyValues[0];
			}
			else
			{
				if (keyValues.Count != c)
					throw new ArgumentOutOfRangeException(nameof(liveKey), "Key is incompatible.");

				return (TKEY)Activator.CreateInstance(kt, liveKey.ToArray());
			}
		} // proc ToKeyValue

		internal PpsLiveDataRow CreateEmptyRow(PpsLiveData data)
			=> (PpsLiveDataRow)Activator.CreateInstance(rowType, data);

		/// <summary>Create a live data row for a primary key.</summary>
		/// <param name="data">LiveData service.</param>
		/// <param name="primaryKey">Primary key to select the row.</param>
		/// <returns></returns>
		public IPpsLiveRowView CreateRow(PpsLiveData data, object primaryKey)
			=> CreateRow(data, new object[] { primaryKey });

		/// <summary>Create a live data row for a primary key.</summary>
		/// <param name="data">LiveData service.</param>
		/// <param name="primaryKey">Primary key to select the row.</param>
		/// <returns></returns>
		public IPpsLiveRowView CreateRow(PpsLiveData data, params object[] primaryKey)
		{
			var row = (IPpsLiveRowView)Activator.CreateInstance(rowViewType.Value, this, data);
			row.SetKey(primaryKey);
			return row;
		} // func CreateRow

		/// <summary>Create a live data row for a primary key.</summary>
		/// <param name="data">LiveData service.</param>
		/// <param name="primaryKey">Primary key to select the row.</param>
		/// <returns></returns>
		/// <typeparam name="T"></typeparam>
		/// <typeparam name="TKEY"></typeparam>
		public IPpsLiveRowView<T, TKEY> CreateRow<T, TKEY>(PpsLiveData data, TKEY primaryKey)
			where T : PpsLiveDataRow
			=> new PpsLiveDataRowViewImpl<T, TKEY>(this, data, primaryKey);

		internal IPpsLiveRowView CreateParentRow(PpsLiveDataRow childRow, int[] childFieldIndices)
		{
			var parentRelationType = typeof(PpsLiveParentRelationRowViewImpl<>).MakeGenericType(rowType);
			return (IPpsLiveRowView)Activator.CreateInstance(parentRelationType, this, childRow, childFieldIndices);
		} // func CreateParentRow

		internal IPpsLiveRowView<T> CreateParentRow<T>(PpsLiveDataRow childRow, int[] childFieldIndices)
			where T : PpsLiveDataRow
		{
			if (typeof(T) != rowType)
				throw new ArgumentOutOfRangeException();

			return new PpsLiveParentRelationRowViewImpl<T>(this, childRow, childFieldIndices);
		} // func CreateParentRow

		internal IPpsLiveRowView<T> CreateParentRelation<T>(string propertyName, PpsLiveDataRow childRow)
			where T : PpsLiveDataRow
			=> GetRelation(propertyName).CreateParentRelation<T>(childRow);

		#endregion

		#region -- CreateView ---------------------------------------------------------

		#region -- class PpsLiveFilterNormalizeVisitor --------------------------------

		private sealed class PpsLiveFilterNormalizeVisitor : PpsDataFilterVisitor<PpsDataFilterExpression>
		{
			private readonly IDataColumns columns;

			public PpsLiveFilterNormalizeVisitor(IDataColumns columns)
			{
				this.columns = columns ?? throw new ArgumentNullException(nameof(columns));
			} // ctor

			private static bool IsColumnByMember(string memberName, IDataColumn column)
			{
				if (column is PpsLiveDataColumn liveColumn && liveColumn.HasProperty && String.Compare(memberName, liveColumn.PropertyName, StringComparison.Ordinal) == 0)
					return true;

				return String.Compare(column.Name, memberName, StringComparison.OrdinalIgnoreCase) == 0;
			} // func IsColumnByMember

			private string TranslateOperand(string operand)
			{
				for (var i = 0; i < columns.Columns.Count; i++)
				{
					var col = columns.Columns[i];
					if (IsColumnByMember(operand, col))
						return col.Name;
				}
				throw new ArgumentOutOfRangeException($"Member or column '{operand}' not found");
			} // func TranslateOperand

			public override PpsDataFilterExpression CreateTrueFilter()
				=> PpsDataFilterExpression.True;

			public override PpsDataFilterExpression CreateNativeFilter(PpsDataFilterNativeExpression expression)
				=> expression;

			public override PpsDataFilterExpression CreateCompareFilter(PpsDataFilterCompareExpression expression)
				=> new PpsDataFilterCompareExpression(TranslateOperand(expression.Operand), expression.Operator, expression.Value);
			
			public override PpsDataFilterExpression CreateCompareIn(string operand, PpsDataFilterArrayValue values)
				=> new PpsDataFilterCompareExpression(TranslateOperand(operand), PpsDataFilterCompareOperator.Contains, values);

			public override PpsDataFilterExpression CreateCompareNotIn(string operand, PpsDataFilterArrayValue values)
				=> new PpsDataFilterCompareExpression(TranslateOperand(operand), PpsDataFilterCompareOperator.NotContains, values);

			public override PpsDataFilterExpression CreateLogicFilter(PpsDataFilterExpressionType method, IEnumerable<PpsDataFilterExpression> arguments)
				=> new PpsDataFilterLogicExpression(method, arguments.ToArray());
		} // class PpsLiveFilterNormalizeVisitor

		#endregion

		#region -- class PpsLiveFilterPredicateVisitor --------------------------------

		private sealed class PpsLiveFilterPredicateVisitor : PpsDataFilterVisitorLambda
		{
			private readonly IDataColumns columns;

			public PpsLiveFilterPredicateVisitor(IDataColumns columns)
				: base(Expression.Parameter(typeof(IPpsLiveDataRowSource), "row"))
			{
				this.columns = columns ?? throw new ArgumentNullException(nameof(columns));
			} // ctor

			private (int, IDataColumn) GetColumnIndex(string memberName)
			{
				for (var i = 0; i < columns.Columns.Count; i++)
				{
					var col = columns.Columns[i];
					if (String.Compare(col.Name, memberName, StringComparison.OrdinalIgnoreCase) == 0)
						return (i, columns.Columns[i]);
				}
				throw new ArgumentOutOfRangeException($"Member or column '{memberName}' not found");
			} // func GetColumnIndex

			protected override Expression GetProperty(string memberName)
			{
				var (index, column) = GetColumnIndex(memberName);
				return ConvertTo(
					Expression.MakeIndex(CurrentRowParameter,
						PpsLiveData.liveDataRowSourceIndexPropertyInfo,
						new Expression[] {
							Expression.Constant(index, typeof(int))
						}
					),
					column.DataType
				);
			} // func GetProperty
		} // class PpsLiveFilterPredicateVisitor

		#endregion

		#region -- class PpsLiveTableViewBase -----------------------------------------

		private abstract class PpsLiveTableViewBase<T> : IPpsLiveTableViewBase, IPpsLiveDataViewEvents, IList, INotifyCollectionChanged
			where T : PpsLiveDataRow
		{
			#region -- class RowKey -------------------------------------------------------

			private sealed class RowKey : LiveKeyBase, IComparable<LiveKeyBase>
			{
				private readonly PpsLiveTableViewBase<T> table;
				private readonly T row;

				public RowKey(PpsLiveTableViewBase<T> table, T row)
				{
					this.table = table ?? throw new ArgumentNullException(nameof(table));
					this.row = row ?? throw new ArgumentNullException(nameof(row));
				} // ctor

				protected override bool IsSameReference(LiveKeyBase other)
					=> other is RowKey rk && ReferenceEquals(row, rk.row);

				public int CompareTo(LiveKeyBase other)
					=> CompareTo(this, other, table.orderModifier);

				public T Row => row;

				protected override int Count => table.orderKeyColumns.Length;
				protected override object this[int index] => row[table.orderKeyColumns[index]];
			} // class RowKey

			#endregion

			public event NotifyCollectionChangedEventHandler CollectionChanged;
			public event EventHandler FilterChanged;

			private readonly PpsLiveData data;
			private readonly PpsLiveDataRowType type;

			private readonly Dictionary<object, int> rowKeys = new Dictionary<object, int>();
			private readonly List<LiveKeyBase> rows = new List<LiveKeyBase>();

			private readonly int[] orderKeyColumns;
			private readonly int[] orderModifier;

			private PpsDataFilterExpression currentFilterExpression = null;
			private Predicate<IPpsLiveDataRowSource> currentFilterPredicate = null;

			#region -- Ctor/Dtor ------------------------------------------------------

			protected PpsLiveTableViewBase(PpsLiveDataRowType type, PpsLiveData data, int[] order)
			{
				this.type = type ?? throw new ArgumentNullException(nameof(type));
				this.data = data ?? throw new ArgumentNullException(nameof(data));

				type.CheckRowType(typeof(T));

				// enforce order key
				if (order == null || order.Length == 0)
					order = type.GetPrimaryKeyFields();

				orderKeyColumns = new int[order.Length];
				orderModifier = new int[order.Length];
				for (var i = 0; i < orderKeyColumns.Length; i++)
				{
					if (order[i] < 0)
					{
						orderKeyColumns[i] = ~order[i];
						orderModifier[i] = -1;
					}
					else
					{
						orderKeyColumns[i] = order[i];
						orderModifier[i] = 1;
					}
				}

				type.GetDataTable(data).Register(this);
			} // ctor

			#endregion

			#region -- Row - management -----------------------------------------------

			private RowKey CreateRow()
				=> new RowKey(this, (T)type.CreateEmptyRow(data));

			private int OnRowAddedCore(RowKey rowKey)
			{
				// find index to add
				var idx = FindRowIndex(rowKey, true);
				if (idx < 0)
					idx = ~idx;
				else
					idx++;

				// update list and index
				rows.Insert(idx, rowKey);
				UpdateIndex(idx);

				return idx;
			} // proc OnRowAddedCore

			private RowKey OnRowRemovedCore(object key, int idx)
			{
				rowKeys.Remove(key);
				var r = (RowKey)rows[idx];
				rows.RemoveAt(idx);
				UpdateIndex(idx);
				return r;
			} // proc OnRowRemovedCore

			private void UpdateIndex(int idx)
			{
				for (var i = idx; i < rows.Count; i++)
					rowKeys[((RowKey)rows[i]).Row.Key] = i;
			} // proc UpdateIndex

			private void OnRowRemovedCoreWithEvent(IPpsLiveDataRowSource source, int idx)
			{
				var r = OnRowRemovedCore(source.Key, idx);
				CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, r.Row, idx));
			} // proc OnRowRemovedCoreWithEvent

			private T GetRow(int i)
				=> ((RowKey)rows[i]).Row;

			private int FindRowIndex(RowKey rowKey)
			{
				bool CompareRows(int i)
					=> ReferenceEquals(GetRow(i), rowKey.Row);

				var idx = rows.BinarySearch(rowKey, LiveKeyBase.Comparer);

				if (idx >= 0)
				{
					if (CompareRows(idx))
						return idx;

					// search index to first row
					var i = idx - 1;
					while (i >= 0 && rows[i].Equals(rowKey))
					{
						if (CompareRows(i))
							return i;
						i--;
					}

					// search to index to end
					i = idx + 1;
					while (i < rows.Count && rows[i].Equals(rowKey))
					{
						if (CompareRows(i))
							return i;
						i++;
					}
				}

				return -1;
			} // func FindRowIndex

			private int FindRowIndex(LiveKeyBase key, bool moveToEnd)
			{
				var idx = rows.BinarySearch(key, LiveKeyBase.Comparer);

				if (idx >= 0)
				{
					if (moveToEnd)  // adjust index to end
					{
						idx++;
						while (idx < rows.Count && rows[idx].Equals(key))
							idx++;
						idx--;
					}
					else // adjust index to first row
					{
						idx--;
						while (idx >= 0 && rows[idx].Equals(key))
							idx--;
						idx++;
					}
				}

				return idx;
			} // func FindRowIndex

			void IPpsLiveDataViewEvents.OnRowChanged(IPpsLiveDataRowSource source)
			{
				bool IsOrderKeyModified()
				{
					for (var i = 0; i < orderKeyColumns.Length; i++)
					{
						if (source.IsModified(orderKeyColumns[i]))
							return true;
					}
					return false;
				} // func IsOrderKeyModified

				if (rowKeys.TryGetValue(source.Key, out var idx))
				{
					if (OnFilterRow(source))
					{
						if (IsOrderKeyModified())
						{
							var r = OnRowRemovedCore(source.Key, idx); // remove row
							r.Row.Set(source); // update values
							var newIdx = OnRowAddedCore(r); // reinsert row

							// invoke event
							CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Move, r.Row, newIdx, idx));
						}
						else
							GetRow(idx).Set(source);
					}
					else
						OnRowRemovedCoreWithEvent(source, idx);
				}
				else if (OnFilterRow(source))
				{
					var r = CreateRow();
					r.Row.Set(source);
					idx = OnRowAddedCore(r);
					CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, r.Row, idx));
				}
			} // proc IPpsLiveDataViewEvents.OnRowChanged

			void IPpsLiveDataViewEvents.OnRowRemoved(IPpsLiveDataRowSource source)
			{
				if (rowKeys.TryGetValue(source.Key, out var idx))
					OnRowRemovedCoreWithEvent(source, idx);
			} // proc IPpsLiveDataViewEvents.OnRowRemoved

			/// <summary>Override to the define the filter.</summary>
			protected abstract PpsDataFilterExpression GetFilter();

			protected void OnFilterChanged()
			{
				// get current filter and normalize to server columns
				currentFilterExpression = type.NormalizeFilterExpression(GetFilter());

				// rebuild filter predicate
				currentFilterPredicate = type.CreateLiveSourceRowFilter<T>(currentFilterExpression);

				// refilter current rows
				for (var i = 0; i < Count; i++)
				{
					var row = GetRow(i);
					if (!OnFilterRow(row.Source))
						OnRowRemovedCoreWithEvent(row.Source, i);
				}

				// schedule a reread of missing rows
				data.EnqueueRefreshView(this);

				FilterChanged?.Invoke(this, EventArgs.Empty);
			} // func OnFilterChanged

			/// <summary>Override to define the row filter.</summary>
			/// <param name="source"></param>
			/// <returns></returns>
			private bool OnFilterRow(IPpsLiveDataRowSource source)
				=> currentFilterPredicate?.Invoke(source) ?? false;

			/// <summary>Refresh the current view.</summary>
			/// <returns></returns>
			public Task RefreshAsync()
				=> data.RefreshViewAsync(this);

			IReadOnlyList<IDataColumn> IDataColumns.Columns => Columns;

			/// <summary>Row type for this view.</summary>
			public PpsLiveDataRowType Type => type;
			/// <summary>Columns of the view.</summary>
			public IReadOnlyList<PpsLiveDataColumn> Columns => type.Columns;
			/// <summary>Current attached filter.</summary>
			public PpsDataFilterExpression Filter => currentFilterExpression ?? PpsDataFilterExpression.False;

			#endregion

			#region -- IList, IEnumerator - members -----------------------------------

			/// <summary>Is the row member of this view.</summary>
			/// <param name="row"></param>
			/// <returns></returns>
			public bool Contains(T row)
				=> row != null && FindRowIndex(new RowKey(this, row)) >= 0;

			/// <summary>Is the row member of this view.</summary>
			/// <param name="row"></param>
			/// <returns></returns>
			public int IndexOf(T row)
				=> row == null ? -1 : FindRowIndex(new RowKey(this, row));

			public T FindRow(params object[] keyValues)
			{
				var idx = FindRowIndex(LiveKeyBase.CreateValueKey(keyValues), false);
				return idx < 0 ? default : GetRow(idx);
			} // func FindRow

			public IEnumerable<T> FindRows(params object[] keyValues)
			{
				var key = LiveKeyBase.CreateValueKey(keyValues);
				var idx = FindRowIndex(key, false);
				if (idx >= 0)
				{
					do
					{
						yield return GetRow(idx++);
					} while (idx < rows.Count && rows[idx].Equals(key));
				}
			} // func FindRows

			int IList.Add(object value)
				=> throw new NotSupportedException();

			void IList.Clear()
				=> throw new NotSupportedException();

			void IList.Insert(int index, object value)
				=> throw new NotSupportedException();

			void IList.Remove(object value)
				=> throw new NotSupportedException();

			void IList.RemoveAt(int index)
				=> throw new NotSupportedException();

			bool IList.Contains(object value)
				=> Contains((T)value);

			int IList.IndexOf(object value)
				=> IndexOf((T)value);

			void ICollection.CopyTo(Array array, int index)
				=> throw new NotImplementedException();

			public IEnumerator<T> GetEnumerator()
				=> Rows.GetEnumerator();

			IEnumerator IEnumerable.GetEnumerator()
				=> GetEnumerator();

			object IList.this[int index] { get => GetRow(index); set => throw new NotSupportedException(); }

			bool IList.IsFixedSize => false;
			bool IList.IsReadOnly => true;
			bool ICollection.IsSynchronized => false;
			object ICollection.SyncRoot => null;

			public IEnumerable<T> Rows => rows.Cast<RowKey>().Select(c => c.Row);

			/// <summary>Access row by index.</summary>
			/// <param name="index"></param>
			/// <returns></returns>
			public T this[int index] => GetRow(index);
			/// <summary>Number of rows</summary>
			public int Count => rows.Count;

			#endregion

			public PpsLiveData Data => data;
		} // class PpsLiveTableViewBase

		#endregion

		#region -- class PpsLiveTableViewImpl -----------------------------------------

		private sealed class PpsLiveTableViewImpl<T> : PpsLiveTableViewBase<T>, IPpsLiveTableFilterView<T>, IPpsLiveTableView
			where T : PpsLiveDataRow
		{
			private readonly PpsLiveTableViewBase<T> parent;
			private PpsDataFilterExpression filterExpression;

			public PpsLiveTableViewImpl(PpsLiveDataRowType type, PpsLiveData data, PpsDataFilterExpression filterExpression, int[] order)
				: base(type, data, order)
			{
				SetFilter(filterExpression ?? PpsDataFilterExpression.True);
			} // ctor

			public PpsLiveTableViewImpl(PpsLiveTableViewBase<T> parent, PpsDataFilterExpression filterExpression, int[] order)
				: base(parent.Type, parent.Data, order)
			{
				this.parent = parent ?? throw new ArgumentNullException(nameof(parent));
				SetFilter(filterExpression ?? PpsDataFilterExpression.True);
			} // ctor

			public void SetFilter(PpsDataFilterExpression filterExpression)
			{
				this.filterExpression = filterExpression ?? PpsDataFilterExpression.False;
				OnFilterChanged();
			} // proc SetFilter			

			protected override PpsDataFilterExpression GetFilter()
			{
				return parent != null
					? PpsDataFilterExpression.Combine(parent.Filter, filterExpression)
					: filterExpression;
			} // func GetFilter

			private PpsLiveTableViewImpl<T> CreateViewCore(PpsDataFilterExpression filterExpression, PpsDataOrderExpression[] orderExpression)
				=> new PpsLiveTableViewImpl<T>(this, filterExpression, Type.CreateOrderArray(orderExpression));

			public IPpsLiveTableView<T> CreateView(params PpsDataOrderExpression[] order)
				=> CreateViewCore(PpsDataFilterExpression.True, order);

			IPpsLiveTableView IPpsLiveTableView.CreateView(params PpsDataOrderExpression[] order)
				=> CreateViewCore(PpsDataFilterExpression.True, order);

			public IPpsLiveTableView<T> CreateView(PpsDataFilterExpression filter, params PpsDataOrderExpression[] order)
				=> CreateViewCore(filter, order);

			IPpsLiveTableView IPpsLiveTableView.CreateView(PpsDataFilterExpression filter, params PpsDataOrderExpression[] order)
				=> CreateViewCore(filter, order);

			IEnumerable<PpsLiveDataRow> IPpsLiveTableView.FindRows(params object[] values)
				=> FindRows(values);

			IEnumerable<PpsLiveDataRow> IPpsLiveTableView.Rows => Rows;
		} // class PpsLiveTableViewImpl

		#endregion

		#region -- class PpsLiveTableParentViewImpl -----------------------------------

		private sealed class PpsLiveTableParentViewImpl<T> : PpsLiveTableViewBase<T>, IPpsLiveTableView<T>, IPpsLiveTableView
			where T : PpsLiveDataRow
		{
			private readonly PpsLiveDataRow parentRow;

			private readonly int[] parentFieldIndices;
			private readonly int[] childFieldIndices;

			public PpsLiveTableParentViewImpl(PpsLiveDataRowType type, PpsLiveDataRow parentRow, int[] parentFieldIndices, int[] childFieldIndices, int[] order)
				: base(type, parentRow.Data, order)
			{
				this.parentRow = parentRow ?? throw new ArgumentNullException(nameof(parentRow));

				if (parentFieldIndices.Length != childFieldIndices.Length)
					throw new ArgumentException("Field missmatch, different field number.");

				this.parentFieldIndices = parentFieldIndices;
				this.childFieldIndices = childFieldIndices;

				// attach to row
				parentRow.ColumnChanged += ParentRow_ColumnChanged;

				// activate filter
				OnFilterChanged();
			} // ctor

			private void ParentRow_ColumnChanged(object sender, PpsLiveDataColumnChangedEventArgs e)
			{
				var updateKey = false;
				for (var i = 0; i < parentFieldIndices.Length; i++)
				{
					if (childFieldIndices[i] == e.Index)
						updateKey = true;
				}
				if (updateKey)
					OnFilterChanged();
			} // event ParentRow_ColumnChanged

			protected override PpsDataFilterExpression GetFilter()
			{
				var filter = new List<PpsDataFilterExpression>();
				for (var i = 0; i < parentFieldIndices.Length; i++)
					filter.Add(PpsDataFilterExpression.Compare(Type.columns[childFieldIndices[i]].Name, PpsDataFilterCompareOperator.Equal, parentRow[parentFieldIndices[i]]));

				return PpsDataFilterExpression.Combine(filter.ToArray());
			} // func GetFilter

			private PpsLiveTableViewImpl<T> CreateViewCore(PpsDataFilterExpression filterExpression, PpsDataOrderExpression[] orderExpression)
				=> new PpsLiveTableViewImpl<T>(this, filterExpression, Type.CreateOrderArray(orderExpression));

			public IPpsLiveTableView<T> CreateView(params PpsDataOrderExpression[] order)
				=> CreateViewCore(PpsDataFilterExpression.True, order);

			IPpsLiveTableView IPpsLiveTableView.CreateView(params PpsDataOrderExpression[] order)
				=> CreateViewCore(PpsDataFilterExpression.True, order);

			public IPpsLiveTableView<T> CreateView(PpsDataFilterExpression filter, params PpsDataOrderExpression[] order)
				=> CreateViewCore(filter, order);

			IPpsLiveTableView IPpsLiveTableView.CreateView(PpsDataFilterExpression filter, params PpsDataOrderExpression[] order)
				=> CreateViewCore(filter, order);

			IEnumerable<PpsLiveDataRow> IPpsLiveTableView.FindRows(params object[] values)
				=> FindRows(values);

			IEnumerable<PpsLiveDataRow> IPpsLiveTableView.Rows => Rows;
		} // class PpsLiveTableParentViewImpl

		#endregion

		private PpsDataFilterExpression NormalizeFilterExpression(PpsDataFilterExpression expression)
			=> new PpsLiveFilterNormalizeVisitor(this).CreateFilter(expression.Reduce());

		private Predicate<IPpsLiveDataRowSource> CreateLiveSourceRowFilter<T>(PpsDataFilterExpression filterExpression)
			where T : PpsLiveDataRow
		{
			var filterCompiler = new PpsLiveFilterPredicateVisitor(this);
			var expr = Expression.Lambda<Predicate<IPpsLiveDataRowSource>>(
				filterCompiler.CreateFilter(filterExpression),
				new ParameterExpression[1]
				{
					filterCompiler.CurrentRowParameter
				}
			);
			return expr.Compile();
		} // func CreateLiveSourceRowFilter

		private int[] CreateOrderArray(PpsDataOrderExpression[] orderExpressions)
		{
			return orderExpressions.Select(o =>
			{
				var idx = Array.FindIndex(columns, c => String.Compare(c.Name, o.Identifier, StringComparison.OrdinalIgnoreCase) == 0
					|| c.HasProperty && String.Compare(c.PropertyName, o.Identifier, StringComparison.Ordinal) == 0);
				if (idx < 0)
					throw new ArgumentOutOfRangeException($"Order column '{o.Identifier}' not found.");
				return o.Negate ? ~idx : idx;
			}).ToArray();
		} // func CreateOrderArray

		/// <summary>Creates view with example rows. The rows on this view are not refreshed.</summary>
		/// <param name="data"></param>
		/// <param name="rowSource"></param>
		/// <param name="orderExpressions"></param>
		/// <returns></returns>
		public IPpsLiveTableView CreateView(PpsLiveData data, IEnumerable<IDataRow> rowSource, params PpsDataOrderExpression[] orderExpressions)
			=> throw new NotImplementedException();

		/// <summary>Create view.</summary>
		/// <param name="data"></param>
		/// <param name="filterExpression"></param>
		/// <param name="orderExpressions"></param>
		/// <returns></returns>
		public IPpsLiveTableView CreateView(PpsLiveData data, PpsDataFilterExpression filterExpression, params PpsDataOrderExpression[] orderExpressions)
			=> (IPpsLiveTableView)Activator.CreateInstance(tableViewType.Value, this, data, filterExpression, CreateOrderArray(orderExpressions));

		/// <summary>Creates view with example rows. The rows on this view are not refreshed.</summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="data"></param>
		/// <param name="rows"></param>
		/// <param name="orderExpressions"></param>
		/// <returns></returns>
		public IPpsLiveTableFilterView<T> CreateView<T>(PpsLiveData data, IEnumerable<IDataRow> rows, params PpsDataOrderExpression[] orderExpressions)
			where T : PpsLiveDataRow
			=> throw new NotImplementedException();

		/// <summary>Create view.</summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="data"></param>
		/// <param name="filterExpression"></param>
		/// <param name="orderExpressions"></param>
		/// <returns></returns>
		public IPpsLiveTableFilterView<T> CreateView<T>(PpsLiveData data, PpsDataFilterExpression filterExpression, params PpsDataOrderExpression[] orderExpressions)
			where T : PpsLiveDataRow
			=> new PpsLiveTableViewImpl<T>(this, data, filterExpression, CreateOrderArray(orderExpressions));

		internal IPpsLiveTableView CreateChildView(PpsLiveDataRow parentRow, int[] childFieldIndices)
		{
			var childRelationType = typeof(PpsLiveTableParentViewImpl<>).MakeGenericType(rowType);
			return (IPpsLiveTableView)Activator.CreateInstance(childRelationType, this, parentRow, parentRow.Type.GetPrimaryKeyFields(), childFieldIndices, GetPrimaryKeyFields());
		} // func CreateParentRow

		internal IPpsLiveTableView<T> CreateChildView<T>(PpsLiveDataRow parentRow, int[] childFieldIndices)
			where T : PpsLiveDataRow
			=> new PpsLiveTableParentViewImpl<T>(this, parentRow, parentRow.Type.GetPrimaryKeyFields(), childFieldIndices, GetPrimaryKeyFields());

		internal IPpsLiveTableView<T> CreateChildRelation<T>(string propertyName, PpsLiveDataRow parentRow)
			where T : PpsLiveDataRow
			=> GetRelation(propertyName).CreateChildRelation<T>(parentRow);

		#endregion

		private PpsLiveDataRelation GetRelation(string propertyName)
		{
			return relations.TryGetValue(propertyName, out var relation)
				? relation
				: throw new ArgumentOutOfRangeException(nameof(propertyName), propertyName, $"Relation with for {propertyName} does not exist in {Name}/{tableAttribute.ViewName}.");
		} // func GetRelation

		/// <summary>Name of the data row.</summary>
		public string Name => rowType.Name;

		IReadOnlyList<IDataColumn> IDataColumns.Columns => columns;
		/// <summary>Live columns of this row type.</summary>
		public IReadOnlyList<PpsLiveDataColumn> Columns => columns;

		// -- Static ----------------------------------------------------------

		private static readonly Dictionary<Type, PpsLiveDataRowType> types = new Dictionary<Type, PpsLiveDataRowType>();

		/// <summary>Create a row type, generic.</summary>
		/// <typeparam name="T"></typeparam>
		/// <returns></returns>
		public static PpsLiveDataRowType Get<T>()
			where T : PpsLiveDataRow
			=> Get(typeof(T));

		/// <summary>Create a row type.</summary>
		/// <param name="dataRowType">PpsLiveDataRow type.</param>
		/// <returns></returns>
		public static PpsLiveDataRowType Get(Type dataRowType)
		{
			lock (types)
			{
				if (types.TryGetValue(dataRowType, out var r))
					return r;

				return new PpsLiveDataRowType(dataRowType);
			}
		} // func Get
	} // class PpsLiveDataRowType

	#endregion

	#region -- class PpsLiveDataColumnChangedEventArgs --------------------------------
	
	/// <summary>Change event for rows</summary>
	public sealed class PpsLiveDataColumnChangedEventArgs : EventArgs
	{
		/// <summary></summary>
		/// <param name="index"></param>
		public PpsLiveDataColumnChangedEventArgs(int index)
		{
			Index = index;
		} // ctor

		/// <summary>Index of the column</summary>
		public int Index { get; }
	} // class PpsLiveDataColumnChangedEventArgs

	#endregion

	#region -- class PpsLiveDataRow ---------------------------------------------------

	/// <summary>Basic implementation for the live data rows.</summary>
	public abstract class PpsLiveDataRow : IDataRow, INotifyPropertyChanged
	{
		/// <summary>Notify property changes</summary>
		public event PropertyChangedEventHandler PropertyChanged;
		/// <summary></summary>
		public event EventHandler<PpsLiveDataColumnChangedEventArgs> ColumnChanged;

		private readonly PpsLiveDataRowType type;
		private readonly PpsLiveData data;
		private IPpsLiveDataRowSource source = null;

		private readonly Dictionary<string, IPpsLiveRowViewBase> activeParentRelations = new Dictionary<string, IPpsLiveRowViewBase>();
		private readonly Dictionary<string, IPpsLiveTableViewBase> activeChildRelations = new Dictionary<string, IPpsLiveTableViewBase>();

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary>Create a live data row, attached to an table.</summary>
		/// <param name="type"></param>
		/// <param name="data"></param>
		protected PpsLiveDataRow(PpsLiveDataRowType type, PpsLiveData data)
		{
			this.type = type ?? throw new ArgumentNullException(nameof(type));
			this.data = data ?? throw new ArgumentNullException(nameof(data));
		} // ctor

		/// <summary>HashCode is the Source.Key property.</summary>
		/// <returns></returns>
		public override int GetHashCode()
			=> Key?.GetHashCode() ?? 0;

		/// <summary>Equals Source.Key.</summary>
		/// <param name="obj"></param>
		/// <returns></returns>
		public override bool Equals(object obj)
			=> obj is PpsLiveDataRow r && Equals(Key, r.Key);

		/// <summary>Notify property changed event.</summary>
		/// <param name="propertyName"></param>
		protected void OnPropertyChanged(string propertyName)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		/// <summary>Is called if a column value is changed. Notifies the attached properties.</summary>
		/// <param name="index">Index of the value.</param>
		protected virtual void OnColumnChanged(int index)
		{
			var col = type.Columns[index];

			// invoke property change
			ColumnChanged?.Invoke(this, new PpsLiveDataColumnChangedEventArgs(index));
			if (col.HasProperty)
				OnPropertyChanged(col.PropertyName);
		} // proc OnColumnChanged

		/// <summary>Invokes all properties are changed.</summary>
		private void OnColumnsChanged()
		{
			var l = type.Columns.Count;
			for (var i = 0; i < l; i++)
				OnColumnChanged(i);
		} // proc OnColumnsChanged

		#endregion

		#region -- Get/Set ------------------------------------------------------------

		internal void Set(IPpsLiveDataRowSource newSource)
		{
			if (newSource is null)
			{
				source = null;
				OnColumnsChanged();
			}
			else
			{
				if (!(source is null) && ReferenceEquals(source.Key, newSource.Key))
				{
					source = newSource;
					var l = type.Columns.Count;
					for (var i = 0; i < l; i++)
					{
						if (newSource.IsModified(i))
							OnColumnChanged(i);
					}
				}
				else
				{
					source = newSource;
					OnColumnsChanged();
				}
			}
		} // proc Set

		/// <summary>Get a value for the index.</summary>
		/// <param name="index"></param>
		/// <returns></returns>
		public object Get(int index)
			=> source?[index];

		/// <summary>Get a value for the index.</summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="index"></param>
		/// <returns></returns>
		public T Get<T>(int index)
			=> Get(index).ChangeType<T>();

		#endregion

		#region -- Get Property -------------------------------------------------------

		/// <summary>Get a related parent row to the property.</summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="propertyName"></param>
		/// <returns></returns>
		public T GetRelatedRow<T>(string propertyName)
			where T : PpsLiveDataRow
		{
			if (activeParentRelations.TryGetValue(propertyName, out var r))
				return ((IPpsLiveRowView<T>)r).Row;
			else
			{
				var rowView = type.CreateParentRelation<T>(propertyName, this);
				activeParentRelations.Add(propertyName, rowView);

				// attach property change
				rowView.PropertyChanged += (object sender, PropertyChangedEventArgs e) =>
				{
					if (e.PropertyName == nameof(rowView.Row))
						OnPropertyChanged(propertyName);
				};

				return rowView.Row;
			}
		} // func GetRelatedRow

		/// <summary>Get a child row view to the property.</summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="propertyName"></param>
		/// <returns></returns>
		public IPpsLiveTableView<T> GetChildRows<T>(string propertyName)
			where T : PpsLiveDataRow
		{
			if (activeChildRelations.TryGetValue(propertyName, out var r))
				return (IPpsLiveTableView<T>)r;
			else
			{
				var childView = type.CreateChildRelation<T>(propertyName, this);
				activeChildRelations.Add(propertyName, childView);
				return childView;
			}
		} // func GetChildRows

		/// <summary>Get a value by name.</summary>
		/// <param name="name"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public bool TryGetProperty(string name, out object value)
		{
			var idx = type.FindColumnIndex(name, false);
			if (idx >= 0)
			{
				value = Get(idx);
				return true;
			}
			else
			{
				value = null;
				return false;
			}
		} // func TryGetProperty

		#endregion

		/// <summary>Internal object, the data is based on.</summary>
		public object Key => source?.Key;
		/// <summary>Live data, that owns this row.</summary>
		public PpsLiveData Data => data;
		/// <summary>Internal source acces.</summary>
		internal IPpsLiveDataRowSource Source => source;
		/// <summary>Type description of the row.</summary>
		internal PpsLiveDataRowType Type => type;

		bool IDataRow.IsDataOwner => true;
		IReadOnlyList<IDataColumn> IDataColumns.Columns => type.Columns;

		/// <summary>Returns a row value by index.</summary>
		/// <param name="index">Column index.</param>
		/// <returns></returns>
		public object this[int index] => Get(index);

		/// <summary>Returns a row value by field.</summary>
		/// <param name="columnName"></param>
		/// <param name="throwException"></param>
		/// <returns></returns>
		public object this[string columnName, bool throwException = true]
			=> TryGetProperty(columnName, out var value) ? value : null;
	} // class PpsLiveDataRow

	#endregion

	#region -- class LiveKeyBase ------------------------------------------------------

	/// <summary>Internal index key implementation, to support binary search on lists.</summary>
	internal abstract class LiveKeyBase : IEquatable<LiveKeyBase>, IReadOnlyList<object>
	{
		#region -- class ValueKey -----------------------------------------------------

		private sealed class ValueKey : LiveKeyBase
		{
			private readonly object[] values;

			public ValueKey(object[] values)
			{
				this.values = values ?? throw new ArgumentNullException(nameof(values));
			} // ctor

			protected override bool IsSameReference(LiveKeyBase other)
				=> other is ValueKey vk && ReferenceEquals(vk.values, values);

			protected override int Count => values.Length;
			protected override object this[int index] => values[index];
		} // class ValueKeys

		#endregion

		#region -- class LiveKeyComparer ----------------------------------------------

		private sealed class LiveKeyComparer : IComparer<LiveKeyBase>
		{
			public int Compare(LiveKeyBase x, LiveKeyBase y)
			{
				if (x is IComparable<LiveKeyBase> xc)
					return xc.CompareTo(y);
				else if (y is IComparable<LiveKeyBase> yc)
					return yc.CompareTo(y);
				else
					throw new ArgumentException($"At least one must implement {nameof(IComparable<LiveKeyBase>)}.");
			} // func Comparer
		} // class LiveKeyComparer

		#endregion

		#region -- Equal/Compare ------------------------------------------------------

		public sealed override int GetHashCode()
		{
			var r = 0;
			for (var i = 0; i < Count; i++)
				r ^= this[i].GetHashCode();
			return r;
		} // func GetHashCode

		public sealed override bool Equals(object obj)
		{
			if (ReferenceEquals(this, obj))
				return true;
			else if (obj is LiveKeyBase o)
				return Equals(o);
			else
				return false;
		}  // func Equals

		public bool Equals(LiveKeyBase other)
		{
			if (other.Count == Count)
			{
				if (!IsSameReference(other))
				{
					var l = Count;
					for (var i = 0; i < l; i++)
					{
						if (!Equals(other[i], this[i]))
							return false;
					}
				}
				return true;
			}
			else
				return false;
		} // func Equals

		protected abstract bool IsSameReference(LiveKeyBase other);

		private static int CompareValueCore(object a, object b)
		{
			if (ReferenceEquals(a, b))
				return 0;

			if (a is IComparable ac)
				return ac.CompareTo(b);
			else if (b is IComparable bc)
				return bc.CompareTo(a) * -1;
			else
				return -1;
		} // func CompareValueCore

		protected static int CompareTo(LiveKeyBase first, LiveKeyBase other, int[] sortModifier)
		{
			var count = first.Count;
			if (count != other.Count)
				throw new ArgumentException("SortKey is not compatible.");

			if (first.IsSameReference(other))
				return 0;
			else
			{
				for (var i = 0; i < count; i++)
				{
					var r = CompareValueCore(first[i], other[i]);
					if (r != 0)
					{
						if (sortModifier != null)
							r *= sortModifier[i];
						return r;
					}
				}
				return 0;
			}
		} // func CompareTo

		#endregion

		/// <summary>Create a copy of the key values.</summary>
		/// <returns></returns>
		public object[] ToArray()
		{
			var o = new object[Count];
			for (var i = 0; i < o.Length; i++)
				o[i] = this[i];
			return o;
		} // func ToArray

		private IEnumerator<object> GetEnumerator()
		{
			var count = Count;
			for (var i = 0; i < count; i++)
				yield return this[i];
		} // func GetEnumerator

		IEnumerator<object> IEnumerable<object>.GetEnumerator()
			=> GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator()
			=> GetEnumerator();

		int IReadOnlyCollection<object>.Count => Count;
		object IReadOnlyList<object>.this[int index] => this[index];

		protected abstract int Count { get; }
		protected abstract object this[int index] { get; }

		/// <summary>Create a value base index key.</summary>
		/// <param name="values"></param>
		/// <returns></returns>
		public static LiveKeyBase CreateValueKey(params object[] values)
			=> new ValueKey(values);

		internal static LiveKeyBase CreateValueKeyDirect(object[] values)
			=> new ValueKey(values);

		/// <summary>Compare between keys, one site must implement <c>IComparable-LiveKeyBase</c></summary>
		public static IComparer<LiveKeyBase> Comparer { get; } = new LiveKeyComparer();
	} // class LiveKeyBase

	#endregion

	#region -- interface IPpsLiveDataLog ----------------------------------------------

	/// <summary>Logging interface for live data.</summary>
	public interface IPpsLiveDataLog
	{
		/// <summary>Log information, when a table is created.</summary>
		/// <param name="tableName"></param>
		void TableCreated(string tableName);
		/// <summary>Log information, when a view is created.</summary>
		/// <param name="tableName"></param>
		/// <param name="typeName"></param>
		void ViewCreated(string tableName, string typeName);
		/// <summary>Log information, when a view is removed from memory.</summary>
		/// <param name="tableName"></param>
		void ViewRemoved(string tableName);

		/// <summary>Log information, when the refresh thread is started.</summary>
		void StartRefreshThread();
		/// <summary>Log information, when the refrehs thread is stopped.</summary>
		void StopRefreshThread();

		/// <summary>Log information for incoming arguments.</summary>
		/// <param name="argumentInfo"></param>
		void ReportRefreshArgs(string argumentInfo);
		/// <summary>Log information after a data refresh.</summary>
		/// <param name="resultInfo"></param>
		void ReportRefreshResult(string resultInfo);

		/// <summary>Log refresh table</summary>
		/// <param name="tableName"></param>
		/// <param name="filter"></param>
		void ViewRefresh(string tableName, string filter);

		/// <summary>Wait for sync is started.</summary>
		void WaitForSyncStart();
		/// <summary>Wait for sync is finished.</summary>
		void WaitForSyncStop();

		/// <summary>Unexpected exception.</summary>
		/// <param name="e"></param>
		void UnexpectedBackgroundFailure(Exception e);
	} // interface IPpsLiveDataLog

	#endregion

	#region -- class PpsLiveData ------------------------------------------------------

	/// <summary>Live data service.</summary>
	[PpsService(typeof(PpsLiveData)), PpsLazyService]
	public sealed class PpsLiveData : IPpsShellService, IDisposable
	{
		internal static readonly PropertyInfo liveDataRowSourceIndexPropertyInfo = typeof(IPpsLiveDataRowSource).GetRuntimeProperty("Item") ?? throw new ArgumentNullException($"{nameof(IPpsLiveDataRowSource)}.Item not found.");

		#region -- class RowData ------------------------------------------------------

		private sealed class RowData : IReadOnlyList<object>
		{
			#region -- struct RowDataValue --------------------------------------------

			private struct RowDataValue
			{
				public object value;
				public bool isModified;
			} // struct RowDataValue

			#endregion

			private RowDataValue[] values;
			private bool isTouched = true;

			public RowData(int capacity)
				=> values = new RowDataValue[capacity < 4 ? 4 : capacity];

			public bool ResetTouched()
			{
				var r = isTouched;
				isTouched = false;
				return r;
			} // func ResetTouched

			public void SetTouched()
				=> isTouched = true;

			public void ResetModified()
			{
				for (var i = 0; i < values.Length; i++)
					values[i].isModified = false;
			} // proc ResetModified

			public bool IsModified(int index)
				=> index >= 0 && index < values.Length && values[index].isModified;

			public IEnumerator<object> GetEnumerator()
				=> values.Select(c => c.value).GetEnumerator();

			IEnumerator IEnumerable.GetEnumerator()
				=> GetEnumerator();

			public object this[int index]
			{
				get => index >= 0 && index < values.Length ? values[index].value : null;
				set
				{
					if (index < 0)
						throw new ArgumentOutOfRangeException(nameof(index), index, "Invalid index.");

					// ensure index
					if (index >= values.Length)
					{
						var n = new RowDataValue[values.Length * 2]; // double array size
						Array.Copy(values, 0, n, 0, values.Length);
						values = n;
					}

					ref var c = ref values[index];
					if (!Equals(c.value, value))
					{
						c.value = value;
						c.isModified = true;
					}
				}
			} // prop this

			public int Count => values.Length;
		} // class RowData

		#endregion

		#region -- class RowView ------------------------------------------------------

		[DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
		private sealed class RowView : IPpsLiveDataRowSource
		{
			private readonly object key;
			private readonly RowData rowData;
			private readonly int[] mapping;

			public RowView(int[] mapping, object key, RowData rowData)
			{
				this.key = key ?? throw new ArgumentNullException(nameof(key));
				this.rowData = rowData ?? throw new ArgumentNullException(nameof(rowData));
				this.mapping = mapping ?? throw new ArgumentNullException(nameof(mapping));
			} // ctor

			private string GetDebuggerDisplay()
				=> $"row: {key}";

			public bool IsModified(int index)
				=> rowData.IsModified(mapping[index]);

			public object this[int index] => rowData[mapping[index]];
			public object Key => key;
		} // class RowView

		#endregion

		#region -- class TableColumn --------------------------------------------------

		/// <summary>Column definition for the raw data.</summary>
		[DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
		private sealed class TableColumn
		{
			private readonly string fieldName;
			private int refCount = 1;

			public TableColumn(string fieldName)
				=> this.fieldName = fieldName ?? throw new ArgumentNullException(nameof(fieldName));

			private string GetDebuggerDisplay()
				=> $"field: {fieldName} (ref={refCount})";

			public void AddRef()
				=> refCount++;

			public void ReleaseRef()
				=> refCount--;

			public string Name => fieldName;
			public bool IsActive => refCount > 0;
		} // class TableColumn

		#endregion

		#region -- class ViewMapping --------------------------------------------------

		[DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
		private sealed class ViewMapping
		{
			private readonly WeakReference<IPpsLiveDataViewEvents> view;
			private readonly int[] fieldMapping;

			public ViewMapping(int[] fieldMapping, IPpsLiveDataViewEvents view)
			{
				this.view = new WeakReference<IPpsLiveDataViewEvents>(view);
				this.fieldMapping = fieldMapping ?? throw new ArgumentNullException(nameof(fieldMapping));
			} // ctor

			private string GetDebuggerDisplay()
			{
				return view.TryGetTarget(out var v)
					? $"view: {v.Type.Name} / {v.Filter.ToString()}"
					: "<dead>";
			} // func GetDebuggerDisplay

			public static void NotifyRowChanged(ViewMapping v, object k, RowData r)
			{
				if (v.view.TryGetTarget(out var t))
					t.OnRowChanged(new RowView(v.fieldMapping, k, r));
			} // proc NotifyRowAdded

			public static void NotifyRowRemoved(ViewMapping v, object k, RowData r)
			{
				if (v.view.TryGetTarget(out var t))
					t.OnRowRemoved(new RowView(v.fieldMapping, k, r));
			} // proc NotifyRowAdded

			public bool IsView(IPpsLiveDataViewEvents view)
				=> this.view.TryGetTarget(out var t) ? t == view : false;

			/// <summary>Filter expression.</summary>
			public PpsDataFilterExpression Filter => view.TryGetTarget(out var t) ? t.Filter : PpsDataFilterExpression.False;
			/// <summary>Mapping to the raw fields.</summary>
			public IReadOnlyList<int> FieldMapping => fieldMapping;
			/// <summary>Is the view active</summary>
			public bool IsAlive => view.TryGetTarget(out var _);
		} // class ViewMapping

		#endregion

		#region -- class Table --------------------------------------------------------

		[DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
		private sealed class Table : IPpsLiveDataTable, IPpsLiveDataTableInfo
		{
			#region -- class RowDataKey -----------------------------------------------

			private sealed class RowDataKey : LiveKeyBase, IComparable<LiveKeyBase>
			{
				private readonly Table table;
				private readonly RowData row;

				public RowDataKey(Table table, RowData row)
				{
					this.table = table ?? throw new ArgumentNullException(nameof(table));
					this.row = row ?? throw new ArgumentNullException(nameof(row));
				} // ctor

				protected override bool IsSameReference(LiveKeyBase other)
					=> other is RowDataKey k && ReferenceEquals(row, k.row);

				public int CompareTo(LiveKeyBase other)
					=> CompareTo(this, other, null);

				protected override int Count => table.primaryKey.Length;
				protected override object this[int index] => row[table.primaryKey[index]];

				public RowData Row => row;
			} // class RowDataKey

			#endregion

			#region -- class RowPrimaryKey --------------------------------------------

			private sealed class RowPrimaryKey : LiveKeyBase
			{
				private readonly int[] primaryKey;
				private readonly int[] resultMapping;
				private readonly IDataRow row;

				public RowPrimaryKey(int[] resultMapping, IDataRow row, int[] primaryKey)
				{
					this.resultMapping = resultMapping ?? throw new ArgumentNullException(nameof(resultMapping));
					this.row = row ?? throw new ArgumentNullException(nameof(row));
					this.primaryKey = primaryKey ?? throw new ArgumentNullException(nameof(primaryKey));
				} // ctor

				protected override bool IsSameReference(LiveKeyBase other)
					=> other is RowPrimaryKey pk && ReferenceEquals(primaryKey, pk.primaryKey) && ReferenceEquals(row, pk.row);

				protected override object this[int index] => row[resultMapping[primaryKey[index]]];
				protected override int Count => primaryKey.Length;
			} // class RowPrimaryKey

			#endregion

			private readonly PpsLiveData data;
			private readonly string name;
			private readonly List<ViewMapping> views = new List<ViewMapping>();
			private int[] primaryKey = null;
			private readonly List<TableColumn> fields = new List<TableColumn>();
			private readonly List<LiveKeyBase> rows = new List<LiveKeyBase>();

			private long syncId = -1L;

			#region -- Ctor/ Dtor -----------------------------------------------------

			public Table(PpsLiveData data, string name)
			{
				this.data = data ?? throw new ArgumentNullException(nameof(data));
				this.name = name ?? throw new ArgumentNullException(nameof(name));
			} // ctor

			private string GetDebuggerDisplay()
				=> $"table: {name}";

			public void Dump(TextWriter tr)
			{
				tr.WriteLine(new string('-', 60));
				tr.Write("-- ");
				tr.Write(name);
				tr.WriteLine();

				tr.Write("-- SyncId = ");
				tr.WriteLine(syncId.ToString());

				// write columns
				var csv = new TextCsvWriter(tr, new TextCsvSettings { LeaveStreamOpen = true });

				for (var i = 0; i < fields.Count; i++)
				{
					if (!fields[i].IsActive)
						tr.Write("~");

					tr.Write(fields[i].Name);
					if (Array.IndexOf(primaryKey, i) >= 0)
						tr.Write("*");

					tr.Write(";");
				}
				tr.WriteLine();

				foreach (var r in rows.OfType<RowDataKey>())
					csv.WriteRow(r.Row.Select(c => c.ChangeType<string>().GetFirstLine()));

				tr.WriteLine();
			} // proc Dump

			#endregion

			#region -- View - management ----------------------------------------------

			private ViewMapping FindView(IPpsLiveDataViewEvents view)
				=> views.Find(c => c.IsView(view));

			private int EnsureField(string fieldName)
			{
				if (String.IsNullOrEmpty(fieldName))
					throw new ArgumentNullException(nameof(fieldName));

				var idx = fields.FindIndex(c => String.Compare(c.Name, fieldName, StringComparison.OrdinalIgnoreCase) == 0);
				if (idx < 0)
				{
					idx = fields.Count;
					fields.Add(new TableColumn(fieldName));
				}
				else
					fields[idx].AddRef();
				return idx;
			} // proc EnsureField

			private void UpdateOrValidatePrimaryKey(List<int> pkeys)
			{
				if (primaryKey == null) // set primary
					primaryKey = pkeys.ToArray();
				else // validate primary key
				{
					for (var i = 0; i < primaryKey.Length; i++)
					{
						if (primaryKey[i] != pkeys[i])
							throw new InvalidOperationException("Primary key mismatch.");
					}
				}
			} // proc UpdateOrValidatePrimaryKey

			public void Register(IPpsLiveDataViewEvents events)
			{
				var columns = events.Type.Columns;

				// create a mapping for this view
				var fieldMapping = new int[columns.Count];
				var pk = new List<int>();
				for (var i = 0; i < fieldMapping.Length; i++)
				{
					var col = columns[i];
					var idx = EnsureField(col.Name); // aquire field

					fieldMapping[i] = idx; // update mapping
					if (col.IsPrimaryKey) // update primary key
						pk.Add(idx);
				}

				// create primary key
				UpdateOrValidatePrimaryKey(pk);

				// add mapping to list
				AddView(events, fieldMapping);
			} // proc Register

			private void AddView(IPpsLiveDataViewEvents events, int[] fieldMapping)
			{
				views.Add(new ViewMapping(fieldMapping, events));
				data.Notify?.ViewCreated(name, events.Type.Name);
				data.InitBackgroundRefresh();
			} // proc AddView

			private void RemoveView(int idx)
			{
				// deactivate unused fields
				var map = views[idx].FieldMapping;
				for (var i = 0; i < map.Count; i++)
					fields[map[i]].ReleaseRef();

				// remove view
				views.RemoveAt(idx);
				data.Notify?.ViewRemoved(name);
				data.DoneBackgroundRefresh();
			} // proc RemoveView

			#endregion

			#region -- Refresh --------------------------------------------------------

			private IEnumerable<IDataRow> CreateViewDataReader(DEHttpClient client, PpsDataQuery query)
				=> client.CreateViewDataReader(query.ToQuery());

			private bool UpdateRow(int[] resultMapping, IDataRow current, RowData r, bool touchRow)
			{
				var isModified = false;

				r.ResetModified();
				if (touchRow)
					r.SetTouched();

				for (var i = 0; i < current.Columns.Count; i++)
				{
					if (resultMapping[i] >= 0)
					{
						r[resultMapping[i]] = current[i];
						if (r.IsModified(i))
							isModified = true;
					}
				}

				return isModified;
			} // proc UpdateRow

			public bool UpdateRow(int primaryKeyIndex, object[] values)
			{
				var isModified = false;

				// create key to search row
				var idx = rows.BinarySearch(LiveKeyBase.CreateValueKey(values[primaryKeyIndex]), LiveKeyBase.Comparer);
				if (idx >= 0)
				{
					var r = (RowDataKey)rows[idx];
					var row = r.Row;

					row.ResetModified();
					for (var i = 0; i < values.Length; i++)
					{
						var v = values[i];
						if (v != null)
						{
							if (v == DBNull.Value)
								v = null;
							row[i] = v;
							if (row.IsModified(i))
								isModified = true;
						}
					}

					if (isModified)
						NotifyRowEvent(r, ViewMapping.NotifyRowChanged);
					return isModified;
				}
				else
					return false;
			} // func UpdateRow

			public PpsDataFilterExpression GetCurrentViewFilter()
			{
				var filterList = new List<PpsDataFilterExpression>();

				// build filter and column mapping for server
				for (var i = views.Count - 1; i >= 0; i--)
				{
					var v = views[i];
					if (!v.IsAlive) // view is dead, remove it
						RemoveView(i);
					else // combine filter expression
						filterList.Add(v.Filter);
				}

				return filterList.Count == 0
					? PpsDataFilterExpression.False
					: new PpsDataFilterLogicExpression(PpsDataFilterExpressionType.Or, filterList.ToArray()).Reduce();
			} // func GetCurrentViewFilter

			public async Task<bool> RefreshRowsAsync(DEHttpClient client, bool touchRow, PpsDataFilterExpression filter)
			{
				// create local field mapping
				var fieldMapping = new List<int>(fields.Capacity);
				var queryFields = new List<PpsDataColumnExpression>(fields.Capacity);

				for (var i = 0; i < fields.Count; i++)
				{
					if (fields[i].IsActive)
					{
						fieldMapping.Add(i);
						queryFields.Add(new PpsDataColumnExpression(fields[i].Name));
					}
				}

				// create query
				var query = new PpsDataQuery(name)
				{
					Columns = queryFields.ToArray(),
					Filter = filter,
					Order = primaryKey.Select(i => new PpsDataOrderExpression(false, fields[i].Name)).ToArray()
				};

				// execute query for full refresh
				var isModified = false;
				int[] resultMapping = null;
				using (var e = CreateViewDataReader(client, query).GetEnumerator())
				{
					while (await e.MoveNextAsync())
					{
						// build result mapping
						if (resultMapping == null)
						{
							resultMapping = new int[queryFields.Count];
							for (var i = 0; i < resultMapping.Length; i++)
								resultMapping[i] = e.FindColumnIndex(queryFields[i].Alias);
						}

						// create key to search row
						var idx = rows.BinarySearch(new RowPrimaryKey(resultMapping, e.Current, primaryKey), LiveKeyBase.Comparer);

						// update row
						if (idx >= 0)
						{
							var r = (RowDataKey)rows[idx];
							if (UpdateRow(resultMapping, e.Current, r.Row, touchRow))
							{
								NotifyRowEvent(r, ViewMapping.NotifyRowChanged);
								isModified = true;
							}
						}
						else
						{
							var r = new RowData(queryFields.Capacity);
							var rk = new RowDataKey(this, r);
							UpdateRow(resultMapping, e.Current, r, touchRow);
							rows.Insert(~idx, rk);
							NotifyRowEvent(rk, ViewMapping.NotifyRowChanged);
							isModified = true;
						}
					}
				}
				return isModified;
			} // proc RefreshRowsAsync

			public bool DeleteRow(object primaryKeyValue)
			{
				// create key to search row
				var idx = rows.BinarySearch(LiveKeyBase.CreateValueKey(primaryKeyValue), LiveKeyBase.Comparer);
				if (idx >= 0)
				{
					var r = (RowDataKey)rows[idx];
					NotifyRowEvent(r, ViewMapping.NotifyRowRemoved);
					rows.RemoveAt(idx);
					return true;
				}
				else
					return false;
			} // proc DeleteRow

			/// <summary>Execute in background refresh Task only.</summary>
			/// <param name="client"></param>
			/// <returns></returns>
			public async Task<bool> RefreshCoreAsync(DEHttpClient client)
			{
				if (views.Count == 0)
					return false;

				// refresh rows
				var isModified = await RefreshRowsAsync(client, true, GetCurrentViewFilter());

				// refresh notify un touched rows as deleted
				for (var i = rows.Count - 1; i >= 0; i--)
				{
					var r = (RowDataKey)rows[i];
					if (!r.Row.ResetTouched())
					{
						NotifyRowEvent(r, ViewMapping.NotifyRowRemoved);
						rows.RemoveAt(i);
						isModified = true;
					}
				}

				return isModified;
			} // proc RefreshCoreAsync

			private void NotifyRowEvent(RowDataKey r, Action<ViewMapping, object, RowData> rowEvent)
			{
				for (var i = 0; i < views.Count; i++)
					rowEvent(views[i], r, r.Row);
			} // proc NotifyRowAdded

			Task IPpsLiveDataTableInfo.RefreshAsync()
				=> data.EnqueueRefreshTableCore(this).WaitForAsync();

			void IPpsLiveDataTableInfo.EnqueueForRefresh()
				=> data.EnqueueRefreshTableCore(this).WaitForAsync();

			public void RefreshView(IPpsLiveDataViewEvents view)
			{
				var mapping = FindView(view);
				foreach (var r in rows.Cast<RowDataKey>())
					ViewMapping.NotifyRowChanged(mapping, r, r.Row);
			} // proc RefreshView

			#endregion

			internal void SetSyncId(long nextSyncId)
				=> syncId = nextSyncId;

			public string GetFieldName(int fieldIndex)
				=> fields[fieldIndex].Name;

			public int GetFieldIndex(string columnName)
				=> fields.FindIndex(c => String.Compare(c.Name, columnName, StringComparison.OrdinalIgnoreCase) == 0);

			string IPpsLiveDataTableInfo.Name => TableName;

			public PpsLiveData Data => data;
			public string TableName => name;
			public long SyncId => syncId;

			public int FieldCount => fields.Count;
			public int[] PrimaryKey => primaryKey;
		} // class Table

		#endregion

		#region -- class EnforceRefreshTableTask --------------------------------------

		private sealed class EnforceRefreshTableTask
		{
			private readonly Table table;
			private readonly List<TaskCompletionSource<int>> waitTasks = new List<TaskCompletionSource<int>>();

			public EnforceRefreshTableTask(Table table)
			{
				this.table = table ?? throw new ArgumentNullException(nameof(table));
			} // ctor

			public void FinishTasks(Exception e = null)
			{
				lock (waitTasks)
				{
					for (var i = 0; i < waitTasks.Count; i++)
					{
						if (e == null)
							waitTasks[i].TrySetResult(0);
						else
							waitTasks[i].TrySetException(e);
					}
					waitTasks.Clear();
				}
			} // proc FinishTasks

			public Task WaitForAsync()
			{
				lock (waitTasks)
				{
					var t = new TaskCompletionSource<int>();
					waitTasks.Add(t);
					return t.Task;
				}
			} // func WaitForAsync

			public Table Table => table;
		} // class EnforceRefreshTableTask

		#endregion

		#region -- class DebugLiveDataLog ---------------------------------------------

		private sealed class DebugLiveDataLog : IPpsLiveDataLog
		{
			private int startNotifyRefresh = 0;
			private readonly ILogger log;

			public DebugLiveDataLog(ILogger log)
			{
				this.log = log;
			} // ctor

			private void WriteLine(string message)
			{
				if (log == null)
					Debug.WriteLine("[" + nameof(PpsLiveData) + "] " + message);
				else
					log.LogMsg(LogMsgType.Debug, message);
			} // proc WriteLine

			public void StartRefreshThread()
				=> WriteLine("Start refresh thread.");

			public void StopRefreshThread()
				=> WriteLine("Stop refresh thread.");

			public void TableCreated(string tableName)
				=> WriteLine("Create table: " + tableName);

			public void ViewCreated(string tableName, string typeName)
				=> WriteLine($"Create view: {tableName} [{typeName}]");
			
			public void ViewRefresh(string tableName, string filter)
				=> WriteLine($"Refresh view: {tableName} > {filter}");

			public void ViewRemoved(string tableName)
				=> WriteLine("Remove view: " + tableName);

			public void ReportRefreshArgs(string argumentInfo)
			{
				if (startNotifyRefresh > 0)
					WriteLine("Refresh start: " + argumentInfo);
			} // proc ReportRefreshArgs

			public void ReportRefreshResult(string resultInfo)
			{
				if (startNotifyRefresh > 0)
					WriteLine("Refresh stop: " + resultInfo);
			} // proc ReportRefreshResult

			public void UnexpectedBackgroundFailure(Exception e)
			{
				if (log == null)
					WriteLine(e.GetMessageString());
				else
					log.Except(e, "[" + nameof(PpsLiveData) + "] " + e.Message);
			} // proc UnexpectedBackgroundFailure

			public void WaitForSyncStart()
			{
				WriteLine($"Start WaitForSync {Thread.CurrentThread.ManagedThreadId}");
				startNotifyRefresh++;
			}  // proc WaitForSyncStart

			public void WaitForSyncStop()
			{
				startNotifyRefresh--;
				WriteLine($"Stop WaitForSync {Thread.CurrentThread.ManagedThreadId}");
			} // proc WaitForSyncStop

			public static DebugLiveDataLog Instance { get; } = new DebugLiveDataLog(null);
		} // class DebugLiveDataLog

		#endregion

		#region -- class MergeUpdateItem ----------------------------------------------

		private sealed class MergeUpdateItem
		{
			private readonly Table table;
			
			private readonly int primaryKeyIndex;
			private readonly object[] values;

			public MergeUpdateItem(Table table, int primaryKeyIndex, object[] values)
			{
				this.table = table ?? throw new ArgumentNullException(nameof(table));
				this.primaryKeyIndex = primaryKeyIndex;
				this.values = values ?? throw new ArgumentNullException(nameof(values));
			} // ctor

			public bool Apply()
				=> table.UpdateRow(primaryKeyIndex, values);
		} // class MergeUpdateItem

		#endregion

		#region -- class MergeItem ----------------------------------------------------

		[DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
		private sealed class MergeItem
		{
			private readonly Table table;

			private bool refreshAll;
			private readonly List<object> refreshIds = new List<object>();
			private readonly List<MergeUpdateItem> updates = new List<MergeUpdateItem>();
			private readonly List<object> deleteIds = new List<object>();
			private long nextSyncId = -1L;

			public MergeItem(Table table, bool refreshAll)
			{
				this.table = table ?? throw new ArgumentNullException(nameof(table));
				this.refreshAll = refreshAll;
			} // ctor

			private string GetDebuggerDisplay()
				=> $"{table.TableName}: next {nextSyncId}";

			private static void AppendIds(StringBuilder sb, string prefix, IReadOnlyList<object> ids)
			{
				if (ids.Count > 0)
				{
					sb.Append(prefix);
					sb.Append(ids[0]);
					for (var i = 1; i < ids.Count; i++)
					{
						sb.Append(',');
						sb.Append(ids[i]);
					}
					sb.AppendLine();
				}
			} // proc AppendIds

			public void LogBefore(StringBuilder sb)
				=> sb.AppendLine($"{(refreshAll ? "Diff" : "All")} for {table.TableName} with sync {table.SyncId}.");

			public void LogAfter(StringBuilder sb)
			{
				sb.AppendLine($"{table.TableName} new sync {nextSyncId}");
				AppendIds(sb, "+ ", refreshIds);
				AppendIds(sb, "- ", deleteIds);
			} // proc LogAfter

			public void SetRefreshAll()
				=> refreshAll = true;

			private bool IsPrimaryMatching(string rowId)
			{
				if (table.PrimaryKey == null || String.IsNullOrEmpty(rowId) || table.PrimaryKey.Length != 1)
					return false;

				return String.Compare(table.GetFieldName(table.PrimaryKey[0]), rowId, StringComparison.OrdinalIgnoreCase) == 0;
			} // proc IsPrimaryMatching

			public void UpdateNextSyncId(long nextSyncId)
			{
				if (this.nextSyncId < nextSyncId)
					this.nextSyncId = nextSyncId;
			} // proc UpdateNextSyncId

			public void ParseMerge(XElement xTable)
			{
				if (!IsPrimaryMatching(xTable.GetAttribute("rowId", String.Empty)))
					SetRefreshAll(); // no primary key mapping, set to full refresh

				var primaryKeyIndex = -1;
				Tuple<XName, Type>[] mapping = null;

				object GetValue(XElement x, Type type)
				{
					if (x == null || x.IsEmpty || x.Value == null)
						return null;
					return Procs.ChangeType(x.Value, type);
				} // func GetValue

				object GetPrimaryKey(XElement x)
					=> GetValue(x.Element(mapping[primaryKeyIndex].Item1), mapping[primaryKeyIndex].Item2);

				foreach (var xItem in xTable.Elements())
				{
					switch (xItem.Name.LocalName)
					{
						case "full":
							UpdateNextSyncId(xItem.GetAttribute("id", 0L));
							SetRefreshAll();
							break; // nothing else to read
						case "columns":
							if (!refreshAll) // we will refresh all anyway, so only collect id's
							{
								mapping = new Tuple<XName, Type>[table.FieldCount];

								// search for the primary column tag
								foreach (var x in xItem.Elements())
								{
									var targetIndex = table.GetFieldIndex(x.GetAttribute("name", String.Empty));
									if (targetIndex == -1)
										continue; // column not used

									var sourceType = LuaType.GetType(x.GetAttribute("type", "string"), lateAllowed: false).Type;

									// create mapping
									mapping[targetIndex] = new Tuple<XName, Type>(x.Name, sourceType);

									if (x.GetAttribute("isPrimary", false))
									{
										if (primaryKeyIndex >= 0) // multiple primary keys are not supported, refresh all
										{
											primaryKeyIndex = -1;
											break;
										}
										else
											primaryKeyIndex = targetIndex;
									}
								}
								if (primaryKeyIndex < 0) // primary column is not in result, refresh all
									SetRefreshAll();
							}
							break;
						case "u":
							UpdateNextSyncId(xItem.GetAttribute("id", 0L));
							if (!refreshAll)
							{
								var values = new object[table.FieldCount];

								foreach (var x in xItem.Elements())
								{
									var idx = Array.FindIndex(mapping, m => m != null && m.Item1 == x.Name);
									if (idx >= 0)
										values[idx] = GetValue(x, mapping[idx].Item2) ?? DBNull.Value;
								}

								updates.Add(new MergeUpdateItem(table, primaryKeyIndex, values));
							}
							break;
						case "i":
						case "r":
							UpdateNextSyncId(xItem.GetAttribute("id", 0L));
							if (!refreshAll)
								refreshIds.Add(GetPrimaryKey(xItem));
							break;
						case "d":
							if (!refreshAll)
								deleteIds.Add(GetPrimaryKey(xItem));

							UpdateNextSyncId(xItem.GetAttribute("id", 0L));
							break;
					}
				}
			} // proc ParseMerge

			public async Task<(Exception ex, bool isModified)> ApplyMergeAsync(DEHttpClient client)
			{
				var isModified = false;
				try
				{
					if (refreshAll)
					{
						isModified = await table.RefreshCoreAsync(client);
					}
					else
					{
						// remove rows
						for (var i = 0; i < deleteIds.Count; i++)
						{
							if (table.DeleteRow(deleteIds[i]))
								isModified = true;
						}

						// update rows
						for (var i = 0; i < updates.Count; i++)
						{
							if (updates[i].Apply())
								isModified = true;
						}

						// refresh rows
						if (refreshIds.Count > 0)
						{
							// build view filter
							var viewFilter = table.GetCurrentViewFilter();
							// build in filter for refresh rows
							var refreshFilter = PpsDataFilterExpression.CompareIn(table.GetFieldName(table.PrimaryKey[0]), refreshIds.ToArray());
							// create row filter
							var rowFilter = PpsDataFilterExpression.Combine(viewFilter, refreshFilter);
							// fetch rows from server
							if (await table.RefreshRowsAsync(client, false, rowFilter))
								isModified = true;
						}
					}

					return (null, isModified);
				}
				catch (Exception e)
				{
					return (e, isModified);
				}
				finally
				{
					// update sync id
					if (nextSyncId > 0)
						table.SetSyncId(nextSyncId);
					else if (nextSyncId == 0)
						table.SetSyncId(table.Data.globalSyncId);
				}
			} // proc ApplyMergeAsync

			public Table Table => table;
			public bool IsRefreshAll => refreshAll;

			public bool IsSyncNeeded => nextSyncId >= 0;
		} // class MergeItem

		#endregion

		private struct VoidResult {  }

		/// <summary>Some data changed.</summary>
		public event EventHandler DataChanged;

		private readonly IPpsShell shell;
		private readonly List<Table> tables = new List<Table>();
		private readonly List<EnforceRefreshTableTask> refreshTasks = new List<EnforceRefreshTableTask>();
		private long globalSyncId = -1L;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary>Initialize live data service.</summary>
		/// <param name="shell"></param>
		public PpsLiveData(IPpsShell shell)
		{
			this.shell = shell ?? throw new ArgumentNullException(nameof(shell));

			// init logging with debug mode
			if (shell.Settings.IsDebugMode)
				SetDebugLog();
		} // ctor

		/// <summary></summary>
		public void Dispose()
		{
			tables.Clear();

			DoneBackgroundRefresh();
		} // proc Dispose

		#endregion

		#region -- GetTable, NewTable, NewRow -----------------------------------------

		private Table GetTable(string tableName, bool allowCreate)
		{
			lock (tables)
			{
				var table = tables.Find(c => String.Compare(c.TableName, tableName, StringComparison.OrdinalIgnoreCase) == 0);
				if (table != null)
					return table;

				if (allowCreate)
				{
					Notify?.TableCreated(tableName);
					tables.Add(table = new Table(this, tableName));
					return table;
				}
				else
					return null;
			}
		} // func GetTable

		private Table[] TableSnapShot()
		{
			lock (tables)
				return tables.ToArray();
		} // func TableSnapShot

		internal IPpsLiveDataTable GetTable(string tableName)
			=> GetTable(tableName, true);

		/// <summary>Get the information of an registered table.</summary>
		/// <param name="tableName">Name of the table.</param>
		/// <returns></returns>
		public IPpsLiveDataTableInfo GetTableInfo(string tableName)
			=> GetTable(tableName, false);

		/// <summary>Get a snapshot of all tables.</summary>
		/// <returns></returns>
		public IPpsLiveDataTableInfo[] GetTableInfo()
			=> TableSnapShot();

		/// <summary>Dump content of <see cref="PpsLiveData"/>.</summary>
		/// <param name="tr"></param>
		public void Dump(TextWriter tr)
		{
			lock (tables)
			{
				foreach (var t in tables)
					t.Dump(tr);
			}
		} // proc Dump

		/// <summary>Create a new row.</summary>
		/// <typeparam name="T"></typeparam>
		/// <typeparam name="TKEY"></typeparam>
		/// <param name="primaryKey"></param>
		/// <returns></returns>
		public IPpsLiveRowView<T, TKEY> NewRow<T, TKEY>(TKEY primaryKey)
			where T : PpsLiveDataRow
			=> PpsLiveDataRowType.Get<T>().CreateRow<T, TKEY>(this, primaryKey);

		/// <summary>Create view.</summary>
		/// <typeparam name="T"></typeparam>
		/// <returns></returns>
		public IPpsLiveTableFilterView<T> NewTable<T>()
			where T : PpsLiveDataRow
			=> NewTable<T>(PpsDataFilterExpression.True);

		/// <summary>Create view.</summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="filterExpression"></param>
		/// <param name="orderExpressions"></param>
		/// <returns></returns>
		public IPpsLiveTableFilterView<T> NewTable<T>(PpsDataFilterExpression filterExpression, params PpsDataOrderExpression[] orderExpressions)
			where T : PpsLiveDataRow
			=> PpsLiveDataRowType.Get<T>().CreateView<T>(this, filterExpression, orderExpressions);

		/// <summary>Create view.</summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="rows"></param>
		/// <param name="orderExpressions"></param>
		/// <returns></returns>
		public IPpsLiveTableView<T> NewTable<T>(IEnumerable<IDataRow> rows, params PpsDataOrderExpression[] orderExpressions)
			where T : PpsLiveDataRow
			=> PpsLiveDataRowType.Get<T>().CreateView<T>(this, rows, orderExpressions);

		#endregion

		#region -- Refresh ------------------------------------------------------------

		private int scheduledEnforce = 0;
		private int backgroundRefreshActive = 0;
		private IPpsShellBackgroundTask backgroundTask = null;

		private static bool TryFindMergeItem(List<MergeItem> mergeList, XElement xTable, out MergeItem mergeItem)
		{
			var syncExpr = xTable.GetAttribute("expr", String.Empty);
			if (String.IsNullOrEmpty(syncExpr))
			{
				mergeItem = null;
				return false;
			}

			mergeItem = mergeList.Find(c => c.Table.TableName == syncExpr);
			return mergeItem != null;
		} // func TryFindMergeItem

		private async Task<Exception[]> ApplyMergeAsync(List<MergeItem> mergeList, DEHttpClient client)
		{
			var isModified = false;
			var exceptions = new List<Exception>();
			foreach (var m in mergeList)
			{
				if (m.IsSyncNeeded)
				{
					var r = await m.ApplyMergeAsync(client);
					if (r.ex != null)
						exceptions.Add(r.ex.GetInnerException());

					isModified |= r.isModified;
				}
			}

			if (isModified)
				FireDataChanged();

			return exceptions.ToArray();
		} // func ApplyMergeAsync

		private async Task RefreshLiveDataFromServerAsync()
		{
			var com = shell.GetService<IPpsCommunicationService>(true);

			var enforceCDC = Interlocked.Exchange(ref scheduledEnforce, 0) != 0;
			var refreshTasks = GetNextRefreshTasks();
			try
			{
				// create sync request
				var xRequest = new XElement("batch");
				xRequest.SetAttributeValue("lastSyncTimeStamp", globalSyncId.ChangeType<string>());
				if (enforceCDC)
					xRequest.SetAttributeValue("enforceCDC", "true");

				var mergeList = new List<MergeItem>();
				var tables = TableSnapShot();
				foreach (var t in tables)
				{
					var customer = refreshTasks?.Any(c => c.Table == t) ?? false;
					var isClientFullRefresh = customer   // is this table scheduled for refresh
						|| t.SyncId < 0; // is this table initialized

					var xSync = new XElement("sync");
					xSync.SetAttributeValue("table", t.TableName);
					xSync.SetAttributeValue("syncId", isClientFullRefresh ? -1L : t.SyncId);
					xRequest.Add(xSync);

					mergeList.Add(new MergeItem(t, isClientFullRefresh));
				}

				if (mergeList.Count > 0)
				{
					if (Notify != null)
					{
						var sb = new StringBuilder();
						mergeList.ForEach(c => c.LogBefore(sb));
						Notify?.ReportRefreshArgs(sb.ToString());
					}

					// do request and process result
					var http = com.Http;
					// todo: wirft eine ObjectDisposedException
					using (var response = await http.PutResponseXmlAsync("?action=syncget", new XDocument(xRequest), MimeTypes.Text.Xml, MimeTypes.Text.Xml))
					{
						var xResult = await HttpStuff.GetXmlAsync(response);

						// process result
						foreach (var xTable in xResult.Elements())
						{
							if (xTable.Name.LocalName == "table")
							{
								if (!TryFindMergeItem(mergeList, xTable, out var mergeItem))
									continue;

								mergeItem.ParseMerge(xTable);
							}
							else if (xTable.Name.LocalName == "syncStamp") // update global stamp
								globalSyncId = xTable.Value.ChangeType<long>();
						}
					}

					if (Notify != null)
					{
						var sb = new StringBuilder();
						mergeList.ForEach(c => c.LogAfter(sb));
						Notify?.ReportRefreshResult(sb.ToString());
					}

					// apply merge commands to tables
					// use ui-thread for this
					var uiExceptions = await await shell.GetService<IPpsUIService>(true).RunUI(new Func<Task<Exception[]>>(() => ApplyMergeAsync(mergeList, http)));

					// notify finish
					if (uiExceptions.Length > 0)
					{
						var aggEx = new AggregateException(uiExceptions);
						FinishWaitForSync(refreshTasks, aggEx);
						Notify?.UnexpectedBackgroundFailure(aggEx);
					}

					FinishWaitForSync(refreshTasks, null);
				}
				else
					FinishWaitForSync(refreshTasks, null);
			}
			catch (Exception e)
			{
				Notify?.UnexpectedBackgroundFailure(e);

				FinishWaitForSync(refreshTasks, e);
				throw;
			}
		} // proc RefreshLiveDataFromServerAsync

		private EnforceRefreshTableTask EnqueueRefreshViewCore(IPpsLiveDataViewEvents view)
		{
			var t = (Table)view.Type.GetDataTable(this);

			// give all known rows for validate
			t.RefreshView(view);

			return EnqueueRefreshTableCore(t);
		} // func EnqueueRefreshViewCore

		private EnforceRefreshTableTask EnqueueRefreshTableCore(Table table)
		{
			lock (refreshTasks)
			{
				var r = refreshTasks.Find(c => ReferenceEquals(c.Table, table));
				if (r == null)
				{
					Notify?.ViewRefresh(table.TableName, table.GetCurrentViewFilter().ToString());
					refreshTasks.Add(r = new EnforceRefreshTableTask(table));
				}

				backgroundTask.Pulse();
				return r;
			}
		} // func EnqueueRefreshViewCore

		internal void EnqueueRefreshView(IPpsLiveDataViewEvents view)
			=> EnqueueRefreshViewCore(view);

		internal Task RefreshViewAsync(IPpsLiveDataViewEvents view)
			=> EnqueueRefreshViewCore(view).WaitForAsync();

		private EnforceRefreshTableTask[] GetNextRefreshTasks()
		{
			lock (refreshTasks)
			{
				if (refreshTasks.Count > 0)
				{
					var r = refreshTasks.ToArray();
					refreshTasks.Clear();
					return r;
				}
				else
					return null;
			}
		} // func GetNextRefreshTask

		/// <summary>Enforce a full refresh of all cached data.</summary>
		/// <returns></returns>
		public Task RefreshAsync()
		{
			var tables = TableSnapShot();
			EnforceRefreshTableTask last = null;

			foreach (var t in tables)
				last = EnqueueRefreshTableCore(t);

			return last?.WaitForAsync() ?? Task.CompletedTask;
		} // proc RefreshAsync

		private void FinishWaitForSync(EnforceRefreshTableTask[] refreshTasks, Exception e)
		{
			if (refreshTasks != null)
			{
				foreach (var t in refreshTasks)
					t.FinishTasks(e);
			}
		} // proc FinishWaitForSync

		/// <summary>Wait for a complete sync cycle.</summary>
		/// <param name="enforceServerSync">Tell the server to fetsch cdc data.</param>
		/// <returns></returns>
		public Task WaitForSync(bool enforceServerSync = false)
		{
			if (backgroundTask == null)
				throw new InvalidOperationException("Background task is not started.");

			if (enforceServerSync)
				Interlocked.CompareExchange(ref scheduledEnforce, 1, 0);
			
			var t = backgroundTask.RunAsync();
			return Notify == null ? t : DebugWaitForSync(t);
		} // proc WaitForSync

		private Task DebugWaitForSync(Task task)
		{
			Notify?.WaitForSyncStart();
			return task.ContinueWith(t =>
				{
					try
					{
						t.Wait();
					}
					finally
					{
						Notify?.WaitForSyncStop();
					}
				}, TaskContinuationOptions.ExecuteSynchronously
			);
		} // func DebugWaitForSync

		private void InitBackgroundRefresh()
		{
			if (Interlocked.Increment(ref backgroundRefreshActive) == 1) // activate background refresh
			{
				Notify?.StartRefreshThread();
				backgroundTask = shell.RegisterTask(RefreshLiveDataFromServerAsync, TimeSpan.FromSeconds(4));
			}
		} // proc InitBackgroundRefresh

		private void DoneBackgroundRefresh()
		{
			if (Interlocked.Decrement(ref backgroundRefreshActive) == 0) // stop background refresh
			{
				Notify?.StopRefreshThread();
				backgroundTask.Dispose();
				backgroundTask = null;
			}
		} // proc DoneBackgroundRefresh

		#endregion

		private void FireDataChanged()
			=> DataChanged?.Invoke(this, EventArgs.Empty);

		/// <summary>Setup Notify with default logger.</summary>
		/// <param name="log"></param>
		public void SetDebugLog(ILogger log = null)
			=> Notify = log == null ? DebugLiveDataLog.Instance : new DebugLiveDataLog(log);

		/// <summary>Connected shell.</summary>
		public IPpsShell Shell => shell;
		/// <summary></summary>
		public IPpsLiveDataLog Notify { get; set; } = null;
	} // class PpsLiveData

	#endregion
}
