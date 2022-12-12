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
using System.Dynamic;
using Neo.IronLua;
using TecWare.DE.Stuff;

namespace TecWare.PPSn.Data
{
	/// <summary>Better bindable view for lua tables.</summary>
	public class PpsLuaTableView : DynamicObject, INotifyPropertyChanged, INotifyCollectionChanged, IList
	{
		/// <summary>Notify</summary>
		public event PropertyChangedEventHandler PropertyChanged { add => table.PropertyChanged += value; remove => table.PropertyChanged -= value; }
		/// <summary>Array was changed</summary>
		public event NotifyCollectionChangedEventHandler CollectionChanged;

		private readonly LuaTable table;
		private readonly LuaTable metaTable;

		private readonly Dictionary<string, PpsLuaTableView> memberTableViews = new Dictionary<string, PpsLuaTableView>();
		private PpsLuaTableView[] indexTableViews = null;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary></summary>
		/// <param name="table"></param>
		/// <param name="metaTable"></param>
		public PpsLuaTableView(LuaTable table, LuaTable metaTable = null)
		{
			this.table = table ?? new LuaTable();

			this.metaTable = metaTable ?? table?.MetaTable?.GetMemberValue("Types", rawGet: true) as LuaTable;
		} // ctor

		/// <summary></summary>
		public void Reset()
			=> CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

		#endregion

		#region -- Get/Set ------------------------------------------------------------

		private static Type GetType(object type)
		{
			if (type is LuaType lt)
				return lt.Type;
			else if (type is Type t)
				return t;
			else if (type is string s)
				return LuaType.GetType(s, lateAllowed: false).Type;
			else if (type is LuaTable)
				return typeof(LuaTable);
			else
				throw new ArgumentException(nameof(type));
		} // func GetType

		private object GetTypedValue(object v, Type type)
			=> v == null ? null : Procs.ChangeType(v, type);

		private bool TryGetType(string name, out Type type, out LuaTable childMetaTable)
		{
			if (metaTable != null)
			{
				var v = metaTable.GetMemberValue(name, rawGet: true);
				if (v is LuaTable t)
				{
					type = typeof(LuaTable);
					childMetaTable = t;
					return true;
				}
				else if (v != null)
				{
					type = GetType(v);
					childMetaTable = null;
					if (type == null)
					{
						type = typeof(object);
						return false;
					}
					else
						return true;
				}
			}
			type = typeof(object);
			childMetaTable = null;
			return false;
		} // func TryGetType

		private object GetTableView(int index, LuaTable t, LuaTable mt)
		{
			if (indexTableViews == null)
				indexTableViews = new PpsLuaTableView[table.ArrayList.Count];

			if (indexTableViews[index] != null && indexTableViews[index].table == t)
				return indexTableViews[index];
			else
			{
				var v = new PpsLuaTableView(t, mt);
				indexTableViews[index] = v;
				return v;
			}
		} // func GetTableView

		private object GetTableView(string name, LuaTable t, LuaTable mt)
		{
			if (memberTableViews.TryGetValue(name, out var v) && v.table == t)
				return v;
			else
			{
				v = new PpsLuaTableView(t, mt);
				memberTableViews[name] = v;
				return v;
			}
		} // func GetTableView

		private object GetIndexedValue(int index)
		{
			var v = table.ArrayList[index];
			return v is LuaTable t ? GetTableView(index, t, metaTable?.GetArrayValue(1, rawGet: true) as LuaTable) : v;
		} // func GetIndexedValue

		private object GetMemberValue(string name)
		{
			var v = table.GetMemberValue(name);
			if (TryGetType(name, out var type, out var childMetaTable))
			{
				return type == typeof(LuaTable)
					? GetTableView(name, (LuaTable)v, childMetaTable)
					: GetTypedValue(v, type);
			}
			else if (v is LuaTable t)
				return GetTableView(name, t, null);
			else
				return v;
		} // func GetMemberValue

		private bool TrySetMemberValue(string name, object value)
		{
			if (TryGetType(name, out var type, out var childMetaTable))
			{
				if (type == typeof(LuaTable))
				{
					memberTableViews[name] = new PpsLuaTableView((LuaTable)value, childMetaTable);
					table[name] = value;
				}
				else
					table[name] = GetTypedValue(value, type);
				return true;
			}
			else
				return false;
		} // proc TrySetMemberValue

		#endregion

		#region -- Dynamic ------------------------------------------------------------

		/// <summary></summary>
		/// <param name="binder"></param>
		/// <param name="result"></param>
		/// <returns></returns>
		public override bool TryGetMember(GetMemberBinder binder, out object result)
		{
			if (binder.Name == nameof(IList.Count)
				|| binder.Name == nameof(Reset))
			{
				result = null;
				return false;
			}
			result = GetMemberValue(binder.Name);
			return true;
		} // func TryGetMember

		/// <summary></summary>
		/// <param name="binder"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public override bool TrySetMember(SetMemberBinder binder, object value)
			=> TrySetMemberValue(binder.Name, value) && base.TrySetMember(binder, value);

		/// <summary></summary>
		/// <param name="binder"></param>
		/// <param name="indexes"></param>
		/// <param name="result"></param>
		/// <returns></returns>
		public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object result)
		{
			if (binder.CallInfo.ArgumentCount == 1 && indexes[0] is int i)
			{
				result = GetIndexedValue(i);
				return true;
			}
			else
				return base.TryGetIndex(binder, indexes, out result);
		} // func TryGetIndex

		#endregion

		#region -- IList - members-----------------------------------------------------

		void ICollection.CopyTo(Array array, int index)
			=> ((ICollection)table.ArrayList).CopyTo(array, index);

		int IList.Add(object value)
			=> throw new NotSupportedException();

		void IList.Insert(int index, object value)
			=> throw new NotSupportedException();

		void IList.Remove(object value)
			=> throw new NotSupportedException();

		void IList.RemoveAt(int index)
			=> throw new NotSupportedException();

		void IList.Clear()
			=> throw new NotSupportedException();

		bool IList.Contains(object value)
			=> table.ArrayList.Contains(value);

		int IList.IndexOf(object value)
			=> table.ArrayList.IndexOf(value);

		IEnumerator IEnumerable.GetEnumerator()
			=> table.ArrayList.GetEnumerator();

		bool IList.IsFixedSize => false;
		bool IList.IsReadOnly => true;
		bool ICollection.IsSynchronized => false;
		object ICollection.SyncRoot => null;

		/// <summary></summary>
		public int Count => table.ArrayList.Count;

		object IList.this[int index] { get => GetIndexedValue(index); set => throw new NotSupportedException(); }

		#endregion

		/// <summary>Access row table.</summary>
		public LuaTable RawTable => table;
	} // class PpsLuaTableView
}
