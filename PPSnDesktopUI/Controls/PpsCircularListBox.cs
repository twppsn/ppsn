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
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using TecWare.PPSn.UI;

namespace TecWare.PPSn.Controls
{
	#region -- class PpsCircularListBox -----------------------------------------------

	/// <summary></summary>
	public class PpsCircularListBox : ItemsControl
	{
		/// <summary>The height of an item.</summary>
		public const double ListViewItemHeight = 40.0;

		private const string partItemBorder = "PART_ItemBorder";
		private PpsCircularView circularListView = null;
		private double lastTouchPosition = 0.0;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		private static readonly DependencyPropertyKey hasTwoItemsPropertyKey = DependencyProperty.RegisterReadOnly(nameof(HasTwoItems), typeof(bool), typeof(PpsCircularListBox), new FrameworkPropertyMetadata(BooleanBox.False));
		public static readonly DependencyProperty HasTowItemsProperty = hasTwoItemsPropertyKey.DependencyProperty;

		public static readonly DependencyProperty ListViewCountProperty = DependencyProperty.Register(nameof(ListViewCount), typeof(int), typeof(PpsCircularListBox), new FrameworkPropertyMetadata(11, new PropertyChangedCallback(OnListViewCountChanged)));
		public static readonly DependencyProperty ListProperty = DependencyProperty.Register(nameof(List), typeof(IList), typeof(PpsCircularListBox), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnListChanged)));
		public static readonly DependencyProperty SelectedItemProperty = Selector.SelectedItemProperty.AddOwner(typeof(PpsCircularListBox), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnSelectedItemChanged)));
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		/// <summary></summary>
		public PpsCircularListBox()
		{

			CommandBindings.Add(new CommandBinding(
				ComponentCommands.MoveDown,
				(sender, e) =>
				{
					EnsureFocus();
					SelectNextItem();
				},
				(sender, e) => e.CanExecute = circularListView != null)
			);
			CommandBindings.Add(new CommandBinding(
				ComponentCommands.MoveUp,
				(sender, e) =>
				{
					EnsureFocus();
					SelectPreviousItem();
				},
				(sender, e) => e.CanExecute = circularListView != null)
			);
			CommandBindings.Add(new CommandBinding(
				NavigationCommands.GoToPage,
				(sender, e) =>
				{
					EnsureFocus();
					SelectThisItem(e.Parameter);
				},
				(sender, e) => e.CanExecute = circularListView != null)
			);
		} // ctor

		private static void OnListChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsCircularListBox)d).OnListChanged((IList)e.NewValue, (IList)e.OldValue);

		private static void OnListViewCountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsCircularListBox)d).OnListViewCountChanged((int)e.NewValue, (int)e.OldValue);

		private void OnListChanged(IList newValue, IList oldValue)
			=> Initialize(newValue, ListViewCount);

		private void OnListViewCountChanged(int newValue, int oldValue)
			=> Initialize(List, newValue);

		private void Initialize(IList items, int listViewCount)
		{
			circularListView = new PpsCircularView(items, listViewCount);
			circularListView.CollectionChanged += CollectionChanged_CollectionChanged;

			if (items.Count < listViewCount && items.Count % 2 == 0)
				HasTwoItems = true;

			if (SelectedItem != null)
			{
				if (!circularListView.MoveTo(SelectedItem))
					SelectedItem = circularListView.CurrentItem;
			}

			ItemsSource = circularListView;
		} // proc Initialize
		
		private void CollectionChanged_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			switch (e.Action)
			{
				case NotifyCollectionChangedAction.Add:
				case NotifyCollectionChangedAction.Remove:
				case NotifyCollectionChangedAction.Reset:
				case NotifyCollectionChangedAction.Move:
				case NotifyCollectionChangedAction.Replace:
					if (e.NewStartingIndex == 0)
						SelectedItem = circularListView.CurrentItem;
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
		} // event CollectionChanged_CollectionChanged

		private static void OnSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsCircularListBox)d).OnSelectedItemChanged(e.NewValue, e.OldValue);

		/// <summary></summary>
		/// <param name="newValue"></param>
		/// <param name="oldValue"></param>
		protected virtual void OnSelectedItemChanged(object newValue, object oldValue)
		{
			if (circularListView != null && !circularListView.MoveTo(newValue))
				SelectedItem = circularListView.CurrentItem;
		} // func OnSelectedItemChanged

		private void SelectNextItem()
			=> circularListView?.Move(1);

		private void SelectPreviousItem()
			=> circularListView?.Move(-1);

		private void SelectThisItem(object value)
			=> circularListView?.MoveTo(value);

		private void EnsureFocus()
		{
			if (IsFocused)
				return;
			var focusScope = FocusManager.GetFocusScope(this);
			FocusManager.SetFocusedElement(focusScope, this);
			Keyboard.Focus(this);
		} // proc EnsureFocus

		/// <summary></summary>
		/// <param name="e"></param>
		protected override void OnKeyDown(KeyEventArgs e)
		{
			var key = e.Key;
			if (key == Key.System)
				key = e.SystemKey;

			switch (key)
			{
				case Key.Down:
					e.Handled = true;
					SelectNextItem();
					break;
				case Key.Up:
					e.Handled = true;
					SelectPreviousItem();
					break;
			}

			base.OnKeyDown(e);
		} // proc OnKeyDown

		/// <summary></summary>
		/// <param name="e"></param>
		protected override void OnMouseWheel(MouseWheelEventArgs e)
		{
			EnsureFocus();
			if (e.Delta > 0)
				SelectPreviousItem();
			else if (e.Delta < 0)
				SelectNextItem();

			e.Handled = true;
			base.OnMouseWheel(e);
		} // proc OnMouseWheel


		/// <summary></summary>
		/// <param name="e"></param>
		protected override void OnTouchDown(TouchEventArgs e)
		{
			base.OnTouchDown(e);

			if (e.Source is RepeatButton)
				return;

			if (e.OriginalSource is Border border && String.Compare(border.Name, partItemBorder, false) == 0 && TouchesCaptured.Count() == 0)
			{
				lastTouchPosition = e.GetTouchPoint(this).Position.Y;
				EnsureFocus();
				CaptureTouch(e.TouchDevice);
			}
		} // proc OnTouchDown

		/// <summary></summary>
		/// <param name="e"></param>
		protected override void OnTouchMove(TouchEventArgs e)
		{
			base.OnTouchMove(e);

			if (e.TouchDevice.Captured == this && e.TouchDevice.DirectlyOver == this)
			{
				var currentPosition = e.GetTouchPoint(this).Position.Y;
				var delta = currentPosition - lastTouchPosition;
				if (delta >= ListViewItemHeight)
				{
					lastTouchPosition = currentPosition;
					SelectPreviousItem();
				}
				else if (delta <= -ListViewItemHeight)
				{
					lastTouchPosition = currentPosition;
					SelectNextItem();
				}
			}
		} // proc OnTouchMove

		/// <summary></summary>
		/// <param name="e"></param>
		protected override void OnTouchUp(TouchEventArgs e)
		{
			base.OnTouchUp(e);

			if (e.TouchDevice.Captured == this)
				ReleaseTouchCapture(e.TouchDevice);
		} // proc OnTouchUp

		/// <summary></summary>
		/// <param name="item"></param>
		/// <returns></returns>
		protected override bool IsItemItsOwnContainerOverride(object item)
			=> item is ContentControl;

		/// <summary></summary>
		/// <returns></returns>
		protected override DependencyObject GetContainerForItemOverride()
			=> new ContentControl();

		/// <summary>Source list</summary>
		public IList List { get => (IList)GetValue(ListProperty); set => SetValue(ListProperty, value); }
		/// <summary>Visible list items.</summary>
		public int ListViewCount { get => (int)GetValue(ListViewCountProperty); set => SetValue(ListViewCountProperty, value); }
		/// <summary></summary>
		public object SelectedItem { get => GetValue(SelectedItemProperty); set => SetValue(SelectedItemProperty, value); }
		/// <summary>Has the base list only two items?</summary>
		public bool HasTwoItems { get => BooleanBox.GetBool(GetValue(HasTowItemsProperty)); private set => SetValue(hasTwoItemsPropertyKey, BooleanBox.GetObject(value)); }

		static PpsCircularListBox()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PpsCircularListBox), new FrameworkPropertyMetadata(typeof(PpsCircularListBox)));
		} // sctor
	} // class PpsCircularListBox

	#endregion

	#region -- class PpsMultiCircularListBox ------------------------------------------

	/// <summary></summary>
	public class PpsMultiCircularListBox : ContentControl
	{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public static readonly DependencyProperty ListViewCountProperty = PpsCircularListBox.ListViewCountProperty.AddOwner(typeof(PpsMultiCircularListBox), new FrameworkPropertyMetadata(11));
		public static readonly DependencyProperty ListSourceProperty = DependencyProperty.Register(nameof(ListSource), typeof(IEnumerable), typeof(PpsMultiCircularListBox), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnListSourceChanged)));
		public static readonly DependencyProperty SelectedItemsProperty = DependencyProperty.Register(nameof(SelectedItems), typeof(object[]), typeof(PpsMultiCircularListBox), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnSelectedItemsChanged), new CoerceValueCallback(OnCoerceValue)));

		public static readonly RoutedEvent SelectedItemsChangedEvent = EventManager.RegisterRoutedEvent(nameof(SelectedItemsChanged), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(PpsMultiCircularListBox));
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		#region -- class Item ---------------------------------------------------------

		/// <summary></summary>
		public sealed class Item : INotifyPropertyChanged
		{
			/// <summary></summary>
			public event PropertyChangedEventHandler PropertyChanged;

			private readonly PpsMultiCircularListBox owner;
			private readonly int index;

			private readonly IList list;

			internal Item(PpsMultiCircularListBox owner, int index, IList list)
			{
				this.owner = owner;
				this.index = index;
				this.list = list;
			} // ctor

			private void OnPropertyChanged(string propertyName)
				=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

			internal void RaiseSelectionChanged(object newValue, object oldValue)
			{
				if (!Equals(newValue, oldValue))
					OnPropertyChanged(nameof(SelectedItem));
			} // proc RaiseSelectionChanged

			/// <summary></summary>
			public object SelectedItem
			{
				get => GetIndexSafe(owner.SelectedItems, index);
				set => owner.UpdateSelectedItems(value, index);
			} // prop SelectedItem
			
			/// <summary></summary>
			public IList List => list;
		} // class Item

		#endregion
		
		private readonly ObservableCollection<Item> parts = new ObservableCollection<Item>();

		/// <summary></summary>
		public PpsMultiCircularListBox()
		{
		} // ctor

		private static void OnListSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsMultiCircularListBox)d).OnListSourceChanged(e.NewValue, e.OldValue);

		private static object OnCoerceValue(DependencyObject d, object baseValue)
			=> ((PpsMultiCircularListBox)d).OnCoerceValue(baseValue);

		private static void OnSelectedItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsMultiCircularListBox)d).OnSelectedItemsChanged((object[])e.NewValue, (object[])e.OldValue);

		private static object GetIndexSafe(object[] values, int index)
			=> values != null && index >= 0 && index < values.Length ? values[index] : null;

		private object OnCoerceValue(object baseValue)
		{
			if (baseValue != null && baseValue is object[] values)
			{
				if (values.Length == parts.Count)
					return values;
				else
				{
					var tmp = new object[parts.Count];
					for (var i = 0; i < tmp.Length; i++)
						tmp[i] = GetIndexSafe(values, i);
					return tmp;
				}
			}
			else
				return new object[parts.Count];
		} // func OnCoerceValue

		/// <summary></summary>
		/// <param name="newValue"></param>
		/// <param name="oldValue"></param>
		protected virtual void OnSelectedItemsChanged(object[] newValue, object[] oldValue)
		{
			for (var i = 0; i < parts.Count; i++)
				parts[i].RaiseSelectionChanged(GetIndexSafe(newValue, i), GetIndexSafe(oldValue, i));
			
			RaiseEvent(new RoutedEventArgs(SelectedItemsChangedEvent, this));
		} // proc OnSelectedItems

		private void UpdateSelectedItems(object newValue, int index)
		{
			var v = SelectedItems[index];
			if (!Equals(v, newValue))
			{
				SelectedItems[index] = newValue;
				RaiseEvent(new RoutedEventArgs(SelectedItemsChangedEvent, this));
			}
		} // proc UpdateSelectedItems

		/// <summary></summary>
		/// <param name="newValue"></param>
		/// <param name="oldValue"></param>
		protected virtual void OnListSourceChanged(object newValue, object oldValue)
		{
			parts.Clear();

			if (newValue is IEnumerable e)
			{
				var i = 0;
				foreach (var c in e)
					parts.Add(new Item(this, i++, (IList)c));
			}
		} // proc OnListSourceChanged

		/// <summary>Access parts</summary>
		public IList Parts => parts;

		/// <summary>Visible list items.</summary>
		public int ListViewCount { get => (int)GetValue(ListViewCountProperty); set => SetValue(ListViewCountProperty, value); }
		/// <summary></summary>
		public IEnumerable ListSource { get => (IEnumerable)GetValue(ListSourceProperty); set => SetValue(ListSourceProperty, value); }
		/// <summary></summary>
		public object[] SelectedItems { get => (object[])GetValue(SelectedItemsProperty); set => SetValue(SelectedItemsProperty, value); }

		/// <summary></summary>
		public event RoutedEventHandler SelectedItemsChanged { add => AddHandler(SelectedItemsChangedEvent, value); remove => RemoveHandler(SelectedItemsChangedEvent, value); }

		static PpsMultiCircularListBox()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PpsMultiCircularListBox), new FrameworkPropertyMetadata(typeof(PpsMultiCircularListBox)));
		} // sctor
	} // class PpsMultiCircularListBox

	#endregion
}
