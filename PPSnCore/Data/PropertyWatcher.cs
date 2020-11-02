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
	/// <typeparam name="T"></typeparam>
	public class PpsPropertyWatcher<T> : IPpsPropertyWatcher
	{
		#region -- class PathElement --------------------------------------------------

		private abstract class PathElement
		{
			private readonly IPpsPropertyWatcher watcher;
			private readonly PathElement child;

			public PathElement(IPpsPropertyWatcher watcher, PathElement child)
			{
				this.watcher = watcher ?? throw new ArgumentNullException(nameof(watcher));
				this.child = child;
			} // ctor

			protected void OnValueChanged()
			{
				if (child == null)
					watcher.FireChanged();
				else
					child.OnParentValueChanged(Value);
			} // proc OnValueChanged

			public abstract void OnParentValueChanged(object newValue);

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

			public override void OnParentValueChanged(object newValue)
			{
				if (newValue == null)
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
					if (propertyInfo == null || propertyInfo.ReflectedType != newValue.GetType())
						propertyInfo = newValue.GetType().GetProperty(propertyName);

					if (instance != null)
						instance.PropertyChanged -= PropertyChanged_PropertyChanged;

					if (propertyInfo != null && newValue is INotifyPropertyChanged npc)
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

			public override object Value => instance == null ? null : propertyInfo.GetValue(instance);
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

			public override void OnParentValueChanged(object newValue)
			{
				if (newValue == null)
				{
					if (collectionChanged != null)
					{
						collectionChanged.CollectionChanged -= CollectionChanged_CollectionChanged;
						collectionChanged = null;
						OnValueChanged();
					}
				}
				else if (newValue is INotifyCollectionChanged col)
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

			public override object Value
			{
				get
				{
					if (collectionChanged is System.Collections.IList list)
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
				}
			} // prop Value
		} // class CollectionElement

		#endregion

		/// <summary>Value behind the path has changed.</summary>
		public event EventHandler<PpsPropertyValueChangedEventArgs<T>> ValueChanged;

		private readonly string propertyPath;
		private readonly PathElement firstElement;
		private readonly PathElement lastElement;

		private readonly Dictionary<string, Delegate> notifier = new Dictionary<string, Delegate>();

		private T currentValue = default;

		/// <summary></summary>
		/// <param name="root"></param>
		/// <param name="propertyPath">Property path to the final property.</param>
		public PpsPropertyWatcher(object root, string propertyPath)
		{
			this.propertyPath = propertyPath ?? throw new ArgumentNullException(nameof(propertyPath));

			firstElement = Parse(propertyPath, 0);
			lastElement = firstElement;

			if (lastElement != null)
			{
				while (lastElement.Child != null)
					lastElement = lastElement.Child;
			}

			if (firstElement != null)
				firstElement.OnParentValueChanged(root);
		} // ctor

		private PathElement Parse(string path, int offset)
		{
			var s = 0;
			var i = offset;
			var ofs = offset;
			var currentIdx = 0;
			var currentIdxNeg = false;
			while (i < path.Length)
			{
				var c = propertyPath[i];
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
							return new PropertyElement(this, path.Substring(ofs, i - ofs), Parse(path, i));
						break;
					case 20:
						if (c == ']')
						{
							if (currentIdxNeg)
								currentIdx = -currentIdx;
							return new CollectionElement(this, currentIdx, Parse(path, i + 1));
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
				return new PropertyElement(this, path.Substring(ofs), null);
			else
				return null;
		} // func Parse

		private T GetValue()
		{
			var r = lastElement?.Value;
			return r == null ? default : Procs.ChangeType<T>(r);
		} // func GetValue

		/// <summary></summary>
		/// <param name="oldValue"></param>
		/// <param name="newValue"></param>
		protected virtual void OnValueChanged(T oldValue, T newValue)
		{
			ValueChanged?.Invoke(this, new PpsPropertyValueChangedEventArgs<T>(propertyPath, oldValue, newValue));

			foreach (var kv in notifier)
				((PropertyChangedEventHandler)kv.Value).Invoke(this, new PropertyChangedEventArgs(kv.Key));
		} // proc OnValueChanged

		void IPpsPropertyWatcher.FireChanged()
		{
			var newValue = GetValue();
			if (!Equals(newValue, currentValue))
			{
				var oldValue = currentValue;
				currentValue = newValue;
				OnValueChanged(oldValue, currentValue);
			}
		} // prop IPropertyWatcher.FireChanged

		/// <summary>Change the root of the property path.</summary>
		/// <param name="root"></param>
		public void SetRoot(object root)
			=> firstElement.OnParentValueChanged(root);

		/// <summary>Add property change handler.</summary>
		/// <param name="propertyName"></param>
		/// <param name="handler"></param>
		public void AddPropertyHandler(string propertyName, PropertyChangedEventHandler handler)
		{
			if (notifier.TryGetValue(propertyName, out var currentHandler))
				notifier[propertyName] = Delegate.Combine(currentHandler, handler);
			else
				notifier[propertyName] = handler;
		} // proc AddPropertyChanged

		/// <summary>Remove a property change handler.</summary>
		/// <param name="propertyName"></param>
		/// <param name="handler"></param>
		public void RemovePropertyHandler(string propertyName, PropertyChangedEventHandler handler)
		{
			if (notifier.TryGetValue(propertyName, out var currentHandler))
				notifier[propertyName] = Delegate.Remove(currentHandler, handler);
		} // proc RemovePropertyHandler

		/// <summary>Is a value binded.</summary>
		public bool HasValue => lastElement?.Value != null;
		/// <summary>Current value or default.</summary>
		public T Value => currentValue;

		object IPpsPropertyWatcher.Value => currentValue;
	} // class PropertyWatcher

	#endregion
}
