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
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Data;
using TecWare.DE.Stuff;

namespace TecWare.PPSn.Data
{
	#region -- class PpsMetaCollection --------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public abstract class PpsMetaCollection : IPropertyEnumerableDictionary
	{
		private readonly Dictionary<string, object> metaInfo;

		public PpsMetaCollection()
		{
			this.metaInfo = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
		} // ctor

		protected PpsMetaCollection(PpsMetaCollection clone)
		{
			this.metaInfo = new Dictionary<string, object>(clone.metaInfo);
		} // ctor

		protected void Add(string key, Func<Type> getDataType, object value)
		{
			if (String.IsNullOrEmpty(key))
				throw new ArgumentNullException("key");

			if (value == null)
				metaInfo.Remove(key);
			else
			{
				// change the type
				Type dataType;
				if (WellknownMetaTypes.TryGetValue(key, out dataType))
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

		public void Merge(PpsMetaCollection otherMeta)
		{
			foreach (var c in otherMeta)
			{
				if (!metaInfo.ContainsKey(c.Name))
					Add(c.Name, null, c.Value);
			}
		} // func Merge

		public bool ContainsKey(string key)
			=> metaInfo.ContainsKey(key) || (StaticKeys?.ContainsKey(key) ?? false);

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
			object value;
			Type type;
			if (TryGetProperty(key, out value))
				return Expression.Constant(value, typeof(object));
			else if (WellknownMetaTypes.TryGetValue(key, out type))
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

		public IEnumerator<PropertyValue> GetEnumerator()
			=> (from c in metaInfo select new PropertyValue(c.Key, c.Value)).GetEnumerator();

		public IEnumerable<string> Keys => metaInfo.Keys;
		public abstract IReadOnlyDictionary<string, Type> WellknownMetaTypes { get; }
		protected virtual IReadOnlyDictionary<string, object> StaticKeys => null;

		public object this[string key]
		{
			get
			{
				object v;
				return TryGetProperty(key, out v) ? v : null;
			}
		} // prop this

		public int Count => metaInfo.Count;
	} // class PpsMetaCollection

	#endregion

	#region -- class PpsDataHelper ------------------------------------------------------

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

		private static Dictionary<Type, string[]> publicMembers = new Dictionary<Type, string[]>();

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

		internal static bool IsStandardMember(Type type, string memberName)
		{
			var p = GetPublicMemberList(type);
			return Array.BinarySearch(p, memberName) >= 0;
		} // func IsStandardMember

	} // class DataHelper

	#endregion

	#region -- class PpsDataTableForeignKeyRestriction ----------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PpsDataTableForeignKeyRestriction : ArgumentOutOfRangeException
	{
		private readonly PpsDataRow parentRow;
		private readonly PpsDataRow childRow;

		public PpsDataTableForeignKeyRestriction(PpsDataRow parentRow, PpsDataRow childRow)
			:base("row","row", $"Row {parentRow} is referenced by {childRow}.")
		{
			this.parentRow = parentRow;
			this.childRow = childRow;
		} // ctor

		public PpsDataRow ParentRow => parentRow;
		public PpsDataRow ChildRow => childRow;
	} // class PpsDataTableForeignKeyRestriction

	#endregion
}
