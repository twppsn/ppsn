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
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace TecWare.PPSn.Controls
{
	#region -- class AddNewItemFactoryEventArgs----------------------------------------

	/// <summary></summary>
	public class AddNewItemFactoryEventArgs : RoutedEventArgs
	{
		public AddNewItemFactoryEventArgs()
		{
		}

		public AddNewItemFactoryEventArgs(RoutedEvent routedEvent)
			: base(routedEvent)
		{
		}

		public AddNewItemFactoryEventArgs(RoutedEvent routedEvent, object source)
			: base(routedEvent, source)
		{
		}

		/// <summary>Newly created item.</summary>
		public object NewItem { get; set; } = null;
	} // class AddNewItemFactoryEventArgs

	/// <summary>Event for item creation.</summary>
	/// <param name="sender"></param>
	/// <param name="args"></param>
	public delegate void AddNewItemFactoryHandler(object sender, AddNewItemFactoryEventArgs args);

	#endregion

	#region -- class PpsEditableListbox -----------------------------------------------

	/// <summary>Extended ListBox</summary>
	public class PpsEditableListbox : ListBox
	{
		public static readonly RoutedEvent AddNewItemFactoryEvent = EventManager.RegisterRoutedEvent(nameof(AddNewItemFactory), RoutingStrategy.Tunnel, typeof(AddNewItemFactoryHandler), typeof(PpsEditableListbox));

		public static readonly RoutedUICommand AppendNewItemCommand = new RoutedUICommand("AppendNewItem", "AppendNewItem", typeof(PpsEditableListbox));
		public static readonly RoutedUICommand RemoveItemCommand = new RoutedUICommand("RemoveItem", "RemoveItem", typeof(PpsEditableListbox));

		public event AddNewItemFactoryHandler AddNewItemFactory { add => AddHandler(AddNewItemFactoryEvent, value); remove => RemoveHandler(AddNewItemFactoryEvent, value); }

		private IEditableCollectionView editableCollectionView;

		#region -- ctor / Init --------------------------------------------------------

		public PpsEditableListbox()
		{
			CommandBindings.Add(
				new CommandBinding(AppendNewItemCommand,
					(sender, e) =>
					{
						FinishAddNew(e.Parameter, true);
					},
					(sender, e) =>
					{
						e.CanExecute = true;
						e.Handled = true;
					}
				)
			);
			CommandBindings.Add(
				new CommandBinding(RemoveItemCommand,
					(sender, e) =>
					{
						RemoveItem(e.Parameter);
						e.Handled = true;
					},
					(sender, e) =>
					{
						e.CanExecute = true;
						e.Handled = true;
					}
				)
			);
		} // ctor

		protected override void OnItemsSourceChanged(IEnumerable oldValue, IEnumerable newValue)
		{
			base.OnItemsSourceChanged(oldValue, newValue);

			editableCollectionView = CollectionViewSource.GetDefaultView(Items) as IEditableCollectionViewAddNewItem;
			if (editableCollectionView != null)
			{
				editableCollectionView.NewItemPlaceholderPosition = NewItemPlaceholderPosition.AtBeginning;
			}
		} // event OnItemsSourceChanged

		protected override DependencyObject GetContainerForItemOverride()
			=> new PpsEditableListboxItem();

		protected override bool IsItemItsOwnContainerOverride(object item)
			=> item is PpsEditableListboxItem;

		#endregion

		internal void BeginTransaction(object item, bool addNew)
		{
			if (addNew)
			{
				BeginAddNew();
			}
			else
			{
				editableCollectionView.EditItem(item);
			}
		} // proc BeginTransaction

		internal void EndTransaction(object item, bool commit)
		{
			if (IsCurrentlyAddedItem(item))
			{
				FinishAddNew(item, commit);
			}
			else
			{
				if (commit)
					editableCollectionView.CommitEdit();
				else
					editableCollectionView.CancelEdit();
			}
		} // proc EndTransaction

		#region -- Append -------------------------------------------------------------

		private object AddNewItem()
		{
			if (editableCollectionView.CanAddNew)
			{
				return editableCollectionView.AddNew();
			}
			else if (editableCollectionView is IEditableCollectionViewAddNewItem editableCollectionViewAddNewItem
				&& editableCollectionViewAddNewItem.CanAddNewItem)
			{
				var e = new AddNewItemFactoryEventArgs(AddNewItemFactoryEvent, this);
				RaiseEvent(e);
				if (e.Handled && e.NewItem != null)
					return editableCollectionViewAddNewItem.AddNewItem(e.NewItem);
				// fall through
			}
			return null;
		} // func AddNewItem

		private void BeginAddNew()
		{
			var item = AddNewItem();
			if (item == null)
			{
				AnchorNewItemPosition(CollectionView.NewItemPlaceholder, false);
				return;
			}

			// collapse
			UpdateNewItemPlaceHolderVisibility(false);

			// ensure container is generated for new item.
			UpdateLayout();

			// keyboard focus to first control
			AnchorNewItemPosition(item, true);
		} // proc BeginAddNew

		private void FinishAddNew(object item, bool commit)
		{
			if (commit)
				editableCollectionView.CommitNew();
			else
				editableCollectionView.CancelNew();

			// show
			UpdateNewItemPlaceHolderVisibility(true);

			// When adding the first item, ListBox use the container, generated from AddNewItem().
			// Must update the property IsNewItem.
			// For all other new items the container is null and will be created.
			// Property then will be set in OnApplyTemplate()
			var listboxItem = GetListBoxItemFromContent(item);
			if (listboxItem != null)
				listboxItem.RefreshItemStatus();

			// Stay on newitem position
			if (commit)
				AnchorNewItemPosition(CollectionView.NewItemPlaceholder, true);
		} // proc FinishAddNew

		public void AnchorNewItemPosition(object item, bool focusChildElement)
			=> SetAnchorItem(item, focusChildElement);

		//public void AnchorNewItemPosition(object item, bool focusChildElement)
		//{
		//	//call it out of the current stack ???
		//	Dispatcher.BeginInvoke(new Action<object, bool>(SetAnchorItem), DispatcherPriority.Input, item, focusChildElement);
		//} // proc AnchorNewItemPosition

		private void SetAnchorItem(object item, bool focusChildElement)
		{
			Items.MoveCurrentTo(item);
			var listBoxItem = GetListBoxItemFromContent(item);
			if (listBoxItem != null)
			{
				// ??? listBoxItem.IsSelected = true;
				if (focusChildElement)
				{
					var element = GetFocusableDescendant(listBoxItem);
					if (element != null)
					{
						FocusManager.SetFocusedElement(this, element);
						Keyboard.Focus(element);
					}
				}
				else
				{
					FocusManager.SetFocusedElement(this, listBoxItem);
					Keyboard.Focus(listBoxItem);
				}
			}
		} // proc SetAnchorItem

		internal bool IsCurrentlyAddedItem(object item)
			=> item.Equals(editableCollectionView.CurrentAddItem);

		private void UpdateNewItemPlaceHolderVisibility(bool show)
		{
			var listBoxItem = GetListBoxItemFromContent(CollectionView.NewItemPlaceholder);
			listBoxItem.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
		} // proc UpdateNewItemPlaceHolderVisibility

		#endregion

		private void RemoveItem(object item)
		{
			if (editableCollectionView.IsEditingItem)
				editableCollectionView.CancelEdit();

			editableCollectionView.Remove(item);
			UpdateLayout();
		} // proc RemoveItem

		private PpsEditableListboxItem GetListBoxItemFromContent(object content)
			=> (PpsEditableListboxItem)ItemContainerGenerator.ContainerFromItem(content);

		private static FrameworkElement GetFocusableDescendant(DependencyObject element)
		{
			FrameworkElement foundElement = null;

			if (element is FrameworkElement fe)
				fe.ApplyTemplate();

			for (var i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
			{
				var child = VisualTreeHelper.GetChild(element, i);
				if (child is FrameworkElement frameWorkElement && frameWorkElement.Focusable)
				{
					foundElement = frameWorkElement;
					break;
				}
				foundElement = GetFocusableDescendant(child);
				if (foundElement != null)
					break;
			}
			return foundElement;
		} // func GetFocusableDescendant
	} // class PpsEditableListbox

	#endregion

	#region -- class PpsEditableListboxItem -------------------------------------------

	public class PpsEditableListboxItem : ListBoxItem
	{
		private static readonly DependencyPropertyKey isNewItemPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsNewItem), typeof(bool?), typeof(PpsEditableListboxItem), new FrameworkPropertyMetadata((bool?)null));
		public static readonly DependencyProperty IsNewItemProperty = isNewItemPropertyKey.DependencyProperty;

		private bool isInEditMode = false;

		internal void RefreshItemStatus()
		{
			bool? isNew = null;

			var parent = ParentListBox;
			if (parent != null)
			{
				if (parent.IsCurrentlyAddedItem(Content))
					isNew = true;
				else if (!IsNewItemPlaceHolder)
					isNew = false;
			}
			SetValue(isNewItemPropertyKey, isNew.HasValue ? (bool?)isNew.Value : null);
		} // proc RefreshItemStatus

		public override void OnApplyTemplate()
		{
			base.OnApplyTemplate();
			RefreshItemStatus();
		} // proc OnApplyTemplate

		protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
		{
			base.OnGotKeyboardFocus(e);

			if (!isInEditMode && IsChildOf(e.NewFocus))
			{
				isInEditMode = true;
				OnEnter();
			}
		} // proc OnGotKeyboardFocus

		protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
		{
			base.OnLostKeyboardFocus(e);

			if (isInEditMode && !IsChildOf(e.NewFocus))
			{
				isInEditMode = false;
				OnLeave(this.Equals(e.NewFocus));
			}
		} // proc OnLostKeyboardFocus

		protected override void OnKeyDown(KeyEventArgs e)
		{
			base.OnKeyDown(e);

			if (!IsChildOf(e.OriginalSource))
				return;

			Key key = e.Key;
			switch (key)
			{
				case Key.Escape:
					{
						e.Handled = true;
						// this finally trigger EndTransaction(commit = false)
						FocusManager.SetFocusedElement(ParentListBox, this);
						Keyboard.Focus(this);
						break;
					}
				case Key.Enter:
					{
						var parent = ParentListBox;
						if (parent != null && parent.IsCurrentlyAddedItem(Content))
						{
							e.Handled = true;
							parent.EndTransaction(Content, true);
						}
					}
					break;
			}
		} // proc OnKeyDown

		private void OnEnter()
		{
			var parent = ParentListBox;
			if (parent != null)
			{
				// Transaction already started by entering NewItemPlaceHolder.
				if (!parent.IsCurrentlyAddedItem(Content))
				{
					parent.BeginTransaction(Content, IsNewItemPlaceHolder);
				}
			}
		} // proc OnEnter

		private void OnLeave(bool focusedSelf)
		{
			// Occurs when NewItemPlaceHolder is changing visibility.
			if (IsNewItemPlaceHolder)
				return;

			var parent = ParentListBox;
			if (parent != null)
			{
				var addedItem = parent.IsCurrentlyAddedItem(Content);
				parent.EndTransaction(Content, !addedItem && !focusedSelf);
				if (addedItem && focusedSelf)
				{
					parent.AnchorNewItemPosition(CollectionView.NewItemPlaceholder, false);
				}
			}
		} // proc OnLeave

		private bool IsChildOf(object child)
		{
			var d = child as DependencyObject;
			if (d == null)
				return false;
			if (d == this)
				return false;
			return IsChildOf(d, this);
		} // func IsChildOf

		// todo: Static ProcsUI
		private static bool IsChildOf(DependencyObject child, DependencyObject parent)
		{
			for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
			{
				var d = VisualTreeHelper.GetChild(parent, i);
				if (child == d)
					return true;
				else if (IsChildOf(child, d))
					return true;
			}
			return false;
		} // func IsChildOf

		public bool? IsNewItem
		{
			get
			{
				// Because Nullable<bool> unboxing is very slow (uses reflection) first we cast to bool
				object value = GetValue(IsNewItemProperty);
				if (value == null)
					return new Nullable<bool>();
				else
					return new Nullable<bool>((bool)value);
			}
		} // prop IsNewItem

		private bool IsNewItemPlaceHolder
			=> CollectionView.NewItemPlaceholder.Equals(this.Content);

		private PpsEditableListbox ParentListBox
			=> ParentSelector as PpsEditableListbox;

		internal Selector ParentSelector
			=> ItemsControl.ItemsControlFromItemContainer(this) as Selector;
	} // class PpsEditableListboxItem

	#endregion

}