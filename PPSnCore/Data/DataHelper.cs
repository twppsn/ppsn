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
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Stuff;

namespace TecWare.PPSn.Data
{
	#region -- class PpsMetaCollection --------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public abstract class PpsMetaCollection : IReadOnlyDictionary<string, object>
	{
		private readonly Dictionary<string, object> metaInfo = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

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
				if (!ContainsKey(c.Key))
					Add(c.Key, null, c.Value);
			}
		} // func Merge

		public bool ContainsKey(string key)
			=> metaInfo.ContainsKey(key);

		public bool TryGetValue(string key, out object value)
			=> metaInfo.TryGetValue(key, out value);

		public T Get<T>(string sKey, T @default)
		{
			object v;
			try
			{
				if (TryGetValue(sKey, out v))
					return v.ChangeType<T>();
				else
					return @default;
			}
			catch
			{
				return @default;
			}
		} // func Get

		internal Expression GetMetaConstantExpression(string sKey)
		{
			object value;
			Type type;
			if (TryGetValue(sKey, out value))
				return Expression.Constant(value, typeof(object));
			else if (WellknownMetaTypes.TryGetValue(sKey, out type))
				return Expression.Convert(Expression.Default(type), typeof(object));
			else
				return Expression.Constant(null, typeof(object));
		} // func GetMetaConstantExpression

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return ((System.Collections.IEnumerable)this).GetEnumerator();
		} // func System.Collections.IEnumerable.GetEnumerator

		public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
		{
			return metaInfo.GetEnumerator();
		} // func GetEnumerator

		public IEnumerable<string> Keys => metaInfo.Keys;
		public IEnumerable<object> Values => metaInfo.Values;
		public abstract IReadOnlyDictionary<string, Type> WellknownMetaTypes { get; }

		public object this[string key]
		{
			get
			{
				object v;
				return TryGetValue(key, out v) ? v : null;
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
		internal static readonly XName xnTable = "t";
		internal static readonly XName xnDataRow = "r";

		internal static readonly XName xnDataRowValue = "v";
		internal static readonly XName xnDataRowValueOriginal = "o";
		internal static readonly XName xnDataRowValueCurrent = "c";

		internal static readonly XName xnDataRowState = "s";
		internal static readonly XName xnDataRowAdd = "a";
		internal static readonly XName xnRowName = "n";

		public static readonly XName xnCombine = "combine";

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
}
