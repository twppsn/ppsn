using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Neo.IronLua;
using TecWare.DES.Stuff;

namespace TecWare.PPSn.Data
{
	#region -- class PpsMetaCollection --------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public abstract class PpsMetaCollection : IReadOnlyDictionary<string, object>
	{
		private Dictionary<string, object> metaInfo = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

		protected void Add(string sKey, Func<Type> getDataType, object value)
		{
			if (String.IsNullOrEmpty(sKey))
				throw new ArgumentNullException("key");

			if (value == null)
				metaInfo.Remove(sKey);
			else
			{
				// Umwandlung der Daten
				Type dataType;
				if (WellknownMetaTypes.TryGetValue(sKey, out dataType))
					value = Procs.ChangeType(value, dataType);
				else
					value = Procs.ChangeType(value, getDataType());

				// Füge die Daten hinzu
				if (value == null)
					metaInfo.Remove(sKey);
				else
					metaInfo[sKey] = value;
			}
		} // proc Add

		public bool ContainsKey(string key)
		{
			return metaInfo.ContainsKey(key);
		} // func ContainsKey

		public bool TryGetValue(string key, out object value)
		{
			return metaInfo.TryGetValue(key, out value);
		} // func TryGetValue

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

		public IEnumerable<string> Keys { get { return metaInfo.Keys; } }
		public IEnumerable<object> Values { get { return metaInfo.Values; } }
		public abstract IReadOnlyDictionary<string, Type> WellknownMetaTypes { get; }

		public object this[string key]
		{
			get
			{
				object v;
				return TryGetValue(key, out v) ? v : null;
			}
		} // prop this

		public int Count { get { return metaInfo.Count; } }
	} // class PpsMetaCollection

	#endregion
}
