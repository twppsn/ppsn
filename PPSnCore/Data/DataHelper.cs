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
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Xml.Linq;
using TecWare.DE.Stuff;

namespace TecWare.PPSn.Data
{
	#region -- class PpsMetaCollection ------------------------------------------------

	/// <summary></summary>
	public abstract class PpsMetaCollection : IPropertyEnumerableDictionary
	{
		private readonly Dictionary<string, object> metaInfo;

		/// <summary></summary>
		public PpsMetaCollection()
		{
			this.metaInfo = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
		} // ctor

		/// <summary></summary>
		/// <param name="clone"></param>
		protected PpsMetaCollection(PpsMetaCollection clone)
		{
			this.metaInfo = new Dictionary<string, object>(clone.metaInfo);
		} // ctor

		/// <summary>Add a meta info value.</summary>
		/// <param name="key">Meta key.</param>
		/// <param name="getDataType">Data type of the value.</param>
		/// <param name="value">Unconverted value of the meta key.</param>
		protected void Add(string key, Func<Type> getDataType, object value)
		{
			if (String.IsNullOrEmpty(key))
				throw new ArgumentNullException("key");

			if (value == null)
				metaInfo.Remove(key);
			else
			{
				// change the type
				if (WellknownMetaTypes.TryGetValue(key, out var dataType))
					value = Procs.ChangeType(value, dataType);
				else if (getDataType != null)
					value = Procs.ChangeType(value, getDataType());

				// add the key
				if (value == null)
					metaInfo.Remove(key);
				else
					metaInfo[key] = value;
			}
		} // proc Add

		/// <summary>Combine two meta collections.</summary>
		/// <param name="otherMeta">Other meta data.</param>
		public void Merge(PpsMetaCollection otherMeta)
		{
			foreach (var c in otherMeta)
			{
				if (!metaInfo.ContainsKey(c.Name))
					Add(c.Name, null, c.Value);
			}
		} // func Merge

		/// <summary>Does the key exists in the meta collection.</summary>
		/// <param name="key">Meta key.</param>
		/// <returns></returns>
		public bool ContainsKey(string key)
			=> metaInfo.ContainsKey(key) || (StaticKeys?.ContainsKey(key) ?? false);

		/// <summary>Try get a meta property.</summary>
		/// <param name="name">Key of the meta data.</param>
		/// <param name="value">Retrieved value.</param>
		/// <returns><c>true</c>, if the meta key could be found.</returns>
		public bool TryGetProperty(string name, out object value)
		{
			if (metaInfo.TryGetValue(name, out value))
				return true;
			else if (StaticKeys != null && StaticKeys.TryGetValue(name, out value))
				return true;
			else
				return false;
		} // func TryGetProperty

		internal Expression GetMetaConstantExpression(string key, bool generateException)
		{
			if (TryGetProperty(key, out var value))
				return Expression.Constant(value, typeof(object));
			else if (WellknownMetaTypes.TryGetValue(key, out var type))
				return Expression.Convert(Expression.Default(type), typeof(object));
			else if (generateException)
			{
				return Expression.Throw(
					Expression.New(Procs.ArgumentOutOfRangeConstructorInfo2,
						new Expression[]
						{
							Expression.Constant(key),
							Expression.Constant("Could not resolve key.")
						}
					), typeof(object)
				);
			}
			else
				return Expression.Constant(null, typeof(object));
		} // func GetMetaConstantExpression

		IEnumerator IEnumerable.GetEnumerator()
			=> GetEnumerator();

		/// <summary>Enumerates all meta properties.</summary>
		/// <returns></returns>
		public IEnumerator<PropertyValue> GetEnumerator()
			=> (from c in metaInfo select new PropertyValue(c.Key, c.Value)).GetEnumerator();

		/// <summary>Meta key enumeration.</summary>
		public IEnumerable<string> Keys => metaInfo.Keys;
		/// <summary>Returns well known meta properties.</summary>
		public abstract IReadOnlyDictionary<string, Type> WellknownMetaTypes { get; }
		/// <summary>Static key values.</summary>
		protected virtual IReadOnlyDictionary<string, object> StaticKeys => null;

		/// <summary>Get the key.</summary>
		/// <param name="key">Meta key</param>
		/// <returns><c>null</c> or the value.</returns>
		public object this[string key] => TryGetProperty(key, out var v) ? v : null;

		/// <summary>Number of meta properties.</summary>
		public int Count => metaInfo.Count;
	} // class PpsMetaCollection

	#endregion

	#region -- class PpsDataHelper ----------------------------------------------------

	/// <summary>Extension methods for the DataSet, DataTable, DataRow.</summary>
	public static class PpsDataHelper
	{
		#region -- Element/Atributenamen des Daten-XML --

		internal static readonly XName xnData = "data";
		internal static readonly XName xnDataRow = "r";

		internal static readonly XName xnDataRowValueOriginal = "o";
		internal static readonly XName xnDataRowValueCurrent = "c";

		internal static readonly XName xnDataRowState = "s";
		internal static readonly XName xnDataRowAdd = "a";

		#endregion

		private static readonly Dictionary<Type, string[]> publicMembers = new Dictionary<Type, string[]>();

		private static string[] GetPublicMemberList(Type type)
		{
			var tmp = (string[])null;
			if (publicMembers.TryGetValue(type, out tmp))
				return tmp;

			tmp =
				type.GetRuntimeEvents().Select(c => c.Name).Concat(
				type.GetRuntimeFields().Select(c => c.Name).Concat(
				type.GetRuntimeMethods().Select(c => c.Name).Concat(
				type.GetRuntimeProperties().Select(c => c.Name))).OrderBy(c => c)).ToArray();

			publicMembers[type] = tmp;

			return tmp;
		} // func GetPublicMemberList

		internal static object GetConvertedValue(PpsDataColumnDefinition columnInfo, object value)
		{
			if (value != null) // unpack data type
			{
				if (columnInfo.IsRelationColumn && value is PpsDataRow parentRow)
				{
					if (parentRow.Table.TableDefinition != columnInfo.ParentColumn.Table)
						throw new InvalidCastException($"The row (from table '{parentRow.Table.TableName}') is not a member of the parent table ({columnInfo.ParentColumn.Table.Name})");
					value = parentRow[columnInfo.ParentColumn.Index];
				}
				else if (!columnInfo.IsExtended)
					value = Procs.ChangeType(value, columnInfo.DataType);
			}
			return value;
		} // func GetConvertedValue

		internal static bool IsStandardMember(Type type, string memberName)
		{
			var p = GetPublicMemberList(type);
			return Array.BinarySearch(p, memberName) >= 0;
		} // func IsStandardMember
	} // class DataHelper

	#endregion

	#region -- class PpsDataTableForeignKeyRestrictionException -----------------------

	/// <summary></summary>
	public class PpsDataTableForeignKeyRestrictionException : ArgumentOutOfRangeException
	{
		private readonly PpsDataRow parentRow;
		private readonly PpsDataRow childRow;

		/// <summary></summary>
		/// <param name="parentRow"></param>
		/// <param name="childRow"></param>
		public PpsDataTableForeignKeyRestrictionException(PpsDataRow parentRow, PpsDataRow childRow)
			:base("row","row", $"Row {parentRow} is referenced by {childRow}.")
		{
			this.parentRow = parentRow;
			this.childRow = childRow;
		} // ctor

		/// <summary></summary>
		public PpsDataRow ParentRow => parentRow;
		/// <summary></summary>
		public PpsDataRow ChildRow => childRow;
	} // class PpsDataTableForeignKeyRestriction

	#endregion
}
