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
using System.Collections.Specialized;
using System.ComponentModel;
using System.Reflection;
using TecWare.DE.Stuff;

namespace TecWare.PPSn.Core.Data
{
	#region -- interface IPpsPropertyWatcher ------------------------------------------

	/// <summary><see cref="PpsPropertyWatcher{T}" /> untyped interface.</summary>
	public interface IPpsPropertyWatcher
	{
		/// <summary>Fire the property change.</summary>
		void FireChanged();

		/// <summary>Current value of the property path.</summary>
		object Value { get; }
	} // interface IPropertyWatcher

	#endregion

	#region -- class PpsPropertyValueChangedEventArgs ---------------------------------

	/// <summary>Extent <see cref="PropertyChangedEventArgs"/> with old/new-value.</summary>
	/// <typeparam name="T"></typeparam>
	public class PpsPropertyValueChangedEventArgs<T> : PropertyChangedEventArgs
	{
		/// <summary></summary>
		/// <param name="propertyName"></param>
		/// <param name="oldValue"></param>
		/// <param name="newValue"></param>
		public PpsPropertyValueChangedEventArgs(string propertyName, T oldValue, T newValue)
			: base(propertyName)
		{
			OldValue = oldValue;
			NewValue = newValue;
		} // ctor

		/// <summary>Value before change.</summary>
		public T OldValue { get; }
		/// <summary>New current value.</summary>
		public T NewValue { get; }
	} // class PropertyValueChangedEventArgs

	#endregion

	#region -- class PpsPropertyWatcher -----------------------------------------------

	/// <summary>Watches a property behind a path description.</summary>
	public abstract class PpsPropertyWatcher : IPpsPropertyWatcher
	{
		#region -- class PathElement --------------------------------------------------

		private abstract class PathElement
		{
			private readonly IPpsPropertyWatcher watcher;
			private readonly PathElement child;

			public PathElement(IPpsPropertyWatcher watcher, PathElement child)
			{
				this.watcher = watcher;
				this.child = child;
			} // ctor

			protected void OnValueChanged()
			{
				if (child == null)
					watcher?.FireChanged();
				else
					child.OnParentValueChanged(Value);
			} // proc OnValueChanged

			public abstract void OnParentValueChanged(object parentValue);

			protected abstract object GetValueCore(object parentValue);

			/// <summary>Return a none cached value.</summary>
			/// <param name="parentValue"></param>
			/// <returns></returns>
			public object GetValue(object parentValue)
			{
				var value = GetValueCore(parentValue);
				if (value == null || child == null)
					return value;
				return child.GetValue(value);
			} // func GetValue

			public abstract object Value { get; }

			public PathElement Child => child;
		} // class PathElement

		#endregion

		#region -- class PropertyElement ----------------------------------------------

		private sealed class PropertyElement : PathElement
		{
			private readonly string propertyName;

			private PropertyInfo propertyInfo = null;
			private INotifyPropertyChanged instance = null;

			public PropertyElement(IPpsPropertyWatcher watcher, string propertyName, PathElement child)
				: base(watcher, child)
			{
				this.propertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
			} // ctor

			private PropertyInfo GetPropertyInfo(object parentValue)
			{
				if (propertyInfo == null || propertyInfo.ReflectedType != parentValue.GetType())
					propertyInfo = parentValue.GetType().GetProperty(propertyName);
				return propertyInfo;
			} // func GetPropertyInfo

			private static object GetValueCore(object parentValue, PropertyInfo propertyInfo)
				=> parentValue == null ? null : propertyInfo.GetValue(parentValue);

			protected override object GetValueCore(object parentValue)
				=> parentValue == null ? null : GetValueCore(parentValue, GetPropertyInfo(parentValue));

			public override void OnParentValueChanged(object parentValue)
			{
				if (parentValue == null)
				{
					if (instance != null)
					{
						instance.PropertyChanged -= PropertyChanged_PropertyChanged;
						instance = null;
						OnValueChanged();
					}
				}
				else
				{
					GetPropertyInfo(parentValue);

					if (instance != null)
						instance.PropertyChanged -= PropertyChanged_PropertyChanged;

					if (propertyInfo != null && parentValue is INotifyPropertyChanged npc)
					{
						instance = npc;

						instance.PropertyChanged += PropertyChanged_PropertyChanged;
						OnValueChanged();
					}
				}
			} // proc OnParentValueChanged

			private void PropertyChanged_PropertyChanged(object sender, PropertyChangedEventArgs e)
			{
				if (e.PropertyName == propertyName)
					OnValueChanged();
			} // event PropertyChanged_PropertyChanged

			public override object Value => GetValueCore(instance, propertyInfo);
		} // class PropertyElement

		#endregion

		#region -- class CollectionElement --------------------------------------------

		private sealed class CollectionElement : PathElement
		{
			private readonly int index;
			private INotifyCollectionChanged collectionChanged;

			public CollectionElement(IPpsPropertyWatcher watcher, int index, PathElement child)
				: base(watcher, child)
			{
				this.index = index;
			} // ctor

			private static object GetValueCore(object parentValue, int index)
			{
				if (parentValue is System.Collections.IList list)
				{
					if (index >= 0)
						return index < list.Count ? list[index] : null;
					else
					{
						var n = list.Count + index;
						return n >= 0 ? list[n] : null;
					}
				}
				else
					return null;
			} // func GetValueCore

			protected override object GetValueCore(object parentValue)
				=> GetValueCore(parentValue, index);

			public override void OnParentValueChanged(object parentValue)
			{
				if (parentValue == null)
				{
					if (collectionChanged != null)
					{
						collectionChanged.CollectionChanged -= CollectionChanged_CollectionChanged;
						collectionChanged = null;
						OnValueChanged();
					}
				}
				else if (parentValue is INotifyCollectionChanged col)
				{
					if (collectionChanged != null)
						collectionChanged.CollectionChanged -= CollectionChanged_CollectionChanged;
					collectionChanged = col;
					collectionChanged.CollectionChanged += CollectionChanged_CollectionChanged;
					OnValueChanged();
				}
			} // proc OnParentValueChanged

			private void CollectionChanged_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
			{
				switch (e.Action)
				{
					case NotifyCollectionChangedAction.Add:
					case NotifyCollectionChangedAction.Remove:
					case NotifyCollectionChangedAction.Replace:
					case NotifyCollectionChangedAction.Move:
					case NotifyCollectionChangedAction.Reset:
						OnValueChanged();
						break;
				}
			} // event CollectionChanged_CollectionChanged

			public override object Value => GetValueCore(collectionChanged, index);
		} // class CollectionElement

		#endregion

		#region -- class PropertyDictionary -------------------------------------------

		private sealed class PropertyDictionary : IPropertyReadOnlyDictionary
		{
			private readonly object root;

			public PropertyDictionary(object root)
			{
				this.root = root;
			} // ctor

			public bool TryGetProperty(string name, out object value)
			{
				if (root == null)
				{
					value = null;
					return false;
				}
				else
				{
					value = GetPropertyValue(root, name);
					return value != null;
				}
			} // func TryGetProperty
		} // class PpsProperties

		#endregion

		private readonly string propertyPath;
		private readonly PathElement firstElement;
		private readonly PathElement lastElement;
		private object currentValue = null;

		private readonly Dictionary<string, Delegate> notifier = new Dictionary<string, Delegate>();

		/// <summary>Watches a property behind a path description.</summary>
		/// <param name="root"></param>
		/// <param name="propertyPath">Property path to the final property.</param>
		public PpsPropertyWatcher(object root, string propertyPath)
		{
			this.propertyPath = propertyPath ?? throw new ArgumentNullException(nameof(propertyPath));

			firstElement = Parse(this, propertyPath, 0);
			lastElement = firstElement;

			if (lastElement != null)
			{
				while (lastElement.Child != null)
					lastElement = lastElement.Child;
			}

			firstElement?.OnParentValueChanged(root);
		} // ctor

		private static PathElement Parse(IPpsPropertyWatcher watcher, string path, int offset)
		{
			var s = 0;
			var i = offset;
			var ofs = offset;
			var currentIdx = 0;
			var currentIdxNeg = false;
			while (i < path.Length)
			{
				var c = path[i];
				switch (s)
				{
					case 0:
						if (c == '[')
						{
							s = 20;
						}
						else if (Char.IsLetter(c))
						{
							s = 10;
							ofs = i;
						}
						break;
					case 10:
						if (!Char.IsLetterOrDigit(c))
							return new PropertyElement(watcher, path.Substring(ofs, i - ofs), Parse(watcher, path, i));
						break;
					case 20:
						if (c == ']')
						{
							if (currentIdxNeg)
								currentIdx = -currentIdx;
							return new CollectionElement(watcher, currentIdx, Parse(watcher, path, i + 1));
						}
						else if (Char.IsDigit(c))
							currentIdx = currentIdx * 10 + c - '0';
						else if (c == '-')
							currentIdxNeg = !currentIdxNeg;
						break;
				}
				i++;
			}

			if (s == 10)
				return new PropertyElement(watcher, path.Substring(ofs), null);
			else
				return default;
		} // func Parse

		private void OnNotifyValueChanged()
		{
			foreach (var kv in notifier)
			{
				switch (kv.Value)
				{
					case PropertyChangedEventHandler pce:
						pce.Invoke(this, new PropertyChangedEventArgs(kv.Key));
						break;
					case Action<string> pc:
						pc.Invoke(kv.Key);
						break;
					default:
						throw new ArgumentOutOfRangeException(nameof(kv.Value));
				}
			}
		} // proc OnNotifyValueChanged

		/// <summary>Get the first value</summary>
		/// <returns></returns>
		protected object GetValueCore()
			=> lastElement?.Value;

		/// <summary></summary>
		/// <param name="oldValue"></param>
		/// <param name="newValue"></param>
		protected abstract void OnValueChanged(object oldValue, object newValue);

		void IPpsPropertyWatcher.FireChanged()
		{
			var newValue = GetValueCore();
			if (!Equals(newValue, currentValue))
			{
				var oldValue = currentValue;
				currentValue = newValue;
				OnValueChanged(oldValue, currentValue);
				OnNotifyValueChanged();
			}
		} // proc FireChanged

		/// <summary>Change the root of the property path.</summary>
		/// <param name="root"></param>
		public void SetRoot(object root)
			=> firstElement.OnParentValueChanged(root);

		#region -- Property Handler ---------------------------------------------------

		private void AddPropertyHandlerCore(string propertyName, Delegate handler)
		{
			if (notifier.TryGetValue(propertyName, out var currentHandler))
				notifier[propertyName] = Delegate.Combine(currentHandler, handler);
			else
				notifier[propertyName] = handler;
		} // proc AddPropertyHandlerCore

		private void RemovePropertyHandlerCore(string propertyName, Delegate handler)
		{
			if (notifier.TryGetValue(propertyName, out var currentHandler))
				notifier[propertyName] = Delegate.Remove(currentHandler, handler);
		} // proc RemovePropertyHandlerCore

		/// <summary>Add property change handler.</summary>
		/// <param name="propertyName"></param>
		/// <param name="handler"></param>
		public void AddPropertyHandler(string propertyName, PropertyChangedEventHandler handler)
			=> AddPropertyHandlerCore(propertyName, handler);

		/// <summary></summary>
		/// <param name="propertyName"></param>
		/// <param name="handler"></param>
		public void AddPropertyHandler(string propertyName, Action<string> handler)
			=> AddPropertyHandlerCore(propertyName, handler);

		/// <summary>Remove a property change handler.</summary>
		/// <param name="propertyName"></param>
		/// <param name="handler"></param>
		public void RemovePropertyHandler(string propertyName, PropertyChangedEventHandler handler)
			=> RemovePropertyHandlerCore(propertyName, handler);

		/// <summary>Remove a property change handler.</summary>
		/// <param name="propertyName"></param>
		/// <param name="handler"></param>
		public void RemovePropertyHandler(string propertyName, Action<string> handler)
			=> RemovePropertyHandlerCore(propertyName, handler);

		#endregion

		object IPpsPropertyWatcher.Value => GetValueCore();

		/// <summary></summary>
		public string Path => propertyPath;

		/// <summary>Return the value from the path.</summary>
		/// <param name="root"></param>
		/// <param name="propertyPath"></param>
		/// <returns></returns>
		public static object GetPropertyValue(object root, string propertyPath)
			=> Parse(null, propertyPath, 0).GetValue(root);

		/// <summary></summary>
		/// <param name="root"></param>
		/// <returns></returns>
		public static IPropertyReadOnlyDictionary GetProperties(object root)
			=> new PropertyDictionary(root);
	} // class PpsPropertyWatcher

	#endregion

	#region -- class PpsPropertyWatcher -----------------------------------------------

	/// <summary>Watches a property behind a path description.</summary>
	/// <typeparam name="T"></typeparam>
	public class PpsPropertyWatcher<T> : PpsPropertyWatcher
	{
		/// <summary>Value behind the path has changed.</summary>
		public event EventHandler<PpsPropertyValueChangedEventArgs<T>> ValueChanged;

		/// <summary></summary>
		/// <param name="root"></param>
		/// <param name="propertyPath">Property path to the final property.</param>
		public PpsPropertyWatcher(object root, string propertyPath)
			: base(root, propertyPath)
		{
		} // ctor

		private static T ConvertValue(object value)
			=> value == null ? default : Procs.ChangeType<T>(value);

		/// <summary></summary>
		/// <param name="oldValue"></param>
		/// <param name="newValue"></param>
		protected override void OnValueChanged(object oldValue, object newValue)
			=> ValueChanged?.Invoke(this, new PpsPropertyValueChangedEventArgs<T>(Path, ConvertValue(oldValue), ConvertValue(newValue)));

		/// <summary>Is a value binded.</summary>
		public bool HasValue => GetValueCore() != null;
		/// <summary>Current value or default.</summary>
		public T Value => ConvertValue(GetValueCore());

		/// <summary>Return the value from the path.</summary>
		/// <param name="root"></param>
		/// <param name="propertyPath"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public static bool TryGetPropertyValue(object root, string propertyPath, out T value)
		{
			var v = GetPropertyValue(root, propertyPath);
			if (v != null)
			{
				value = v.ChangeType<T>();
				return true;
			}
			else
			{
				value = default;
				return false;
			}
		} // func TryGetPropertyValue
	} // class PpsPropertyWatcher

	#endregion
}
