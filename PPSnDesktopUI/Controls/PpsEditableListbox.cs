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
using System.Diagnostics;
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

	#region -- interface IPpsEditableObject -------------------------------------------

	public interface IPpsEditableObject : IEditableObject
	{
		bool IsEditable { get; }
	} // proc IPpsEditableObject

	#endregion

	#region -- class PpsEditableListbox -----------------------------------------------

	/// <summary>Extended ListBox</summary>
	public class PpsEditableListbox : ListBox
	{
		#region -- enum NewItemContainerActionType ------------------------------------

		[Flags]
		private enum NewItemContainerActionType
		{
			/// <summary>Set NewItemPlaceholder.Visibility to Visible</summary>
			ShowPlaceHolder,
			/// <summaryKeyBoardFocus to Container</summary>
			FocusContainer,
			/// <summaryKeyBoarFocus to first focusable child</summary>
			FocusChild
		} // enum NewItemContainerActionType

		#endregion

		#region -- class GeneratorStatusChangedEventArgs ------------------------------

		private class GeneratorStatusChangedEventArgs : EventArgs
		{
			/// <summary>NewItemPlaceholder or CurrentAddItem</summary>
			public object Item { get; set; }
			/// <summary>Action with Item</summary>
			public NewItemContainerActionType Action { get; set; }
		} // class GeneratorStatusChangedEventArgs

		#endregion

		public static readonly RoutedEvent AddNewItemFactoryEvent = EventManager.RegisterRoutedEvent(nameof(AddNewItemFactory), RoutingStrategy.Tunnel, typeof(AddNewItemFactoryHandler), typeof(PpsEditableListbox));

		public static readonly RoutedUICommand AppendNewItemCommand = new RoutedUICommand("Append", "AppendNewItem", typeof(PpsEditableListbox));
		public static readonly RoutedUICommand ChangeItemCommand = new RoutedUICommand("Change", "ChangeItem", typeof(PpsEditableListbox));
		public static readonly RoutedUICommand RemoveItemCommand = new RoutedUICommand("Remove", "RemoveItem", typeof(PpsEditableListbox));

		public event AddNewItemFactoryHandler AddNewItemFactory { add => AddHandler(AddNewItemFactoryEvent, value); remove => RemoveHandler(AddNewItemFactoryEvent, value); }

		private IEditableCollectionView editableCollectionView;
		private EventHandler evGeneratorStatusChanged;
		private GeneratorStatusChangedEventArgs generatorStatusChangedEventArgs;

		#region -- ctor / Init --------------------------------------------------------

		public PpsEditableListbox()
		{
			CommandBindings.Add(
				new CommandBinding(AppendNewItemCommand,
					(sender, e) =>
					{
						TryEndTransaction(e.Parameter, true);
					},
					(sender, e) =>
					{
						e.CanExecute = editableCollectionView != null && editableCollectionView.IsAddingNew;
						e.Handled = true;
					}
				)
			);
			CommandBindings.Add(
				new CommandBinding(ChangeItemCommand,
					(sender, e) =>
					{
						TryEndTransaction(e.Parameter, true);
					},
					(sender, e) =>
					{
						e.CanExecute = editableCollectionView != null && editableCollectionView.IsEditingItem;
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
						e.CanExecute =
							e.Parameter is IPpsEditableObject ppso
								? ppso.IsEditable
								: e.Parameter is IEditableObject;
						e.Handled = true;
					}
				)
			);

			generatorStatusChangedEventArgs = new GeneratorStatusChangedEventArgs();
			evGeneratorStatusChanged = (s, e)
				=> OnItemContainerGeneratorStatusChanged(s, e, generatorStatusChangedEventArgs);
		} // ctor

		#endregion

		protected override void OnItemsSourceChanged(IEnumerable oldValue, IEnumerable newValue)
		{
			base.OnItemsSourceChanged(oldValue, newValue);

			// get the collection view
			editableCollectionView = newValue == null
				? null
				: CollectionViewSource.GetDefaultView(newValue) as IEditableCollectionViewAddNewItem;
		} // event OnItemsSourceChanged

		protected override bool IsItemItsOwnContainerOverride(object item)
			=> item is PpsEditableListboxItem;

		protected override DependencyObject GetContainerForItemOverride()
			=> new PpsEditableListboxItem();

		protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
		{
			base.PrepareContainerForItemOverride(element, item);

			bool? isNewItem = null;
			if (IsCurrentlyAddedItem(item))
				isNewItem = true;
			else if(!item.Equals(CollectionView.NewItemPlaceholder))
				isNewItem = false;

			var listBoxItem = (PpsEditableListboxItem)element;
			listBoxItem.IsNewItem = isNewItem;
		} // proc PrepareContainerForItemOverride

		internal bool TryBeginTransaction(object item, bool addNew)
		{
			if (addNew)
				return BeginAddNew();
			else
			{
				try
				{
					editableCollectionView.EditItem(item);
					return true;
				}
				catch (Exception e)
				{
					Debug.Print("Failed to enter edit mode: {0}", e.Message);
					return false;
				}
			}
		} // proc BeginTransaction

		internal bool TryEndTransaction(object item, bool commit)
		{
			if (IsCurrentlyAddedItem(item))
			{
				return FinishAddNew(item, commit);
			}
			else
			{
				try
				{
					if (commit)
						editableCollectionView.CommitEdit();
					else
						editableCollectionView.CancelEdit();
					return true;
				}
				catch (Exception e)
				{
					Debug.Print("Failed to leave edit mode: {0}", e.Message);
					return false;
				}
			}
		} // proc EndTransaction

		#region -- Append -------------------------------------------------------------

		private object AddNewItem()
		{
			if (editableCollectionView.CanAddNew) // simple AddNew via Default Ctor
			{
				return editableCollectionView.AddNew();
			}
			else if (editableCollectionView is IEditableCollectionViewAddNewItem editableCollectionViewAddNewItem
				&& editableCollectionViewAddNewItem.CanAddNewItem) // extented AddNew via Event
			{
				var e = new AddNewItemFactoryEventArgs(AddNewItemFactoryEvent, this);
				RaiseEvent(e);
				if (e.Handled && e.NewItem != null)
					return editableCollectionViewAddNewItem.AddNewItem(e.NewItem);
				// fall through
			}
			return null; // no new item
		} // func AddNewItem

		private bool BeginAddNew()
		{
			var item = AddNewItem();
			if (item == null)
			{
				ForceRefreshNewItemContainer(CollectionView.NewItemPlaceholder, NewItemContainerActionType.FocusContainer);
				return false;
			}

			// collapse
			CollapseNewItemPlaceHolder();
			// ensure container is generated for new item.
			UpdateLayout();
			// Anchor
			ForceRefreshNewItemContainer(item, NewItemContainerActionType.FocusChild);

			return true;
		} // proc BeginAddNew

		private bool FinishAddNew(object item, bool commit)
		{
			try
			{
				if (commit)
					editableCollectionView.CommitNew();
				else
					editableCollectionView.CancelNew();
			}
			catch (Exception e)
			{
				Debug.Print("Failed to leave edit mode: {0}", e.Message);
				return false;
			}

			var actionType = NewItemContainerActionType.ShowPlaceHolder;
			// Remain in AppendMode				
			if (commit)
				actionType |= NewItemContainerActionType.FocusChild;
			ForceRefreshNewItemContainer(CollectionView.NewItemPlaceholder, actionType);

			return true;
		} // proc FinishAddNew

		#endregion

		private void RemoveItem(object item)
		{
			if (editableCollectionView.IsEditingItem)
				editableCollectionView.CancelEdit();

			editableCollectionView.Remove(item);
			UpdateLayout();
		} // proc RemoveItem

		#region -- Manage NewItemContainer --------------------------------------------

		private void ForceRefreshNewItemContainer(object item, NewItemContainerActionType actionType)
		{
			Items.MoveCurrentTo(item);
			var listBoxItem = GetListBoxItemFromContent(item);
			if (listBoxItem != null)
				RefreshNewItemContainer(listBoxItem, actionType);
			else
			{
				generatorStatusChangedEventArgs.Item = item;
				generatorStatusChangedEventArgs.Action = actionType;
				ItemContainerGenerator.StatusChanged += evGeneratorStatusChanged;
				// force
				ScrollIntoView(item);
			}
		} // proc ForceRefreshNewItemContainer

		private void RefreshNewItemContainer(PpsEditableListboxItem listBoxItem, NewItemContainerActionType actionType)
		{
			if (actionType.HasFlag(NewItemContainerActionType.ShowPlaceHolder))
				listBoxItem.Visibility = Visibility.Visible;

			FrameworkElement focusElement = null;
			if (actionType.HasFlag(NewItemContainerActionType.FocusChild))
				focusElement = GetFocusableDescendant(listBoxItem);
			else if (actionType.HasFlag(NewItemContainerActionType.FocusContainer))
				focusElement = listBoxItem;

			if (focusElement != null)
			{
				FocusManager.SetFocusedElement(this, focusElement);
				Keyboard.Focus(focusElement);
			}
		} // proc RefreshNewItemContainer

		private void OnItemContainerGeneratorStatusChanged(object sender, EventArgs e, GeneratorStatusChangedEventArgs args)
		{
			if (ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated)
			{
				ItemContainerGenerator.StatusChanged -= evGeneratorStatusChanged;
				var listBoxItem = GetListBoxItemFromContent(args.Item);
				RefreshNewItemContainer(listBoxItem, args.Action);
			}
		} // event OnItemContainerGeneratorStatusChanged

		internal void FocusNewItemPlaceHolderContainer()
			=> ForceRefreshNewItemContainer(CollectionView.NewItemPlaceholder, NewItemContainerActionType.FocusContainer);

		// When calling this, NewItemPlaceholder is visible and container is generated
		private void CollapseNewItemPlaceHolder()
			=> GetListBoxItemFromContent(CollectionView.NewItemPlaceholder).Visibility = Visibility.Collapsed;

		private bool IsCurrentlyAddedItem(object item)
			=> editableCollectionView != null && item.Equals(editableCollectionView.CurrentAddItem);

		#endregion

		private PpsEditableListboxItem GetListBoxItemFromContent(object item)
			=> (PpsEditableListboxItem)ItemContainerGenerator.ContainerFromItem(item);

		private static FrameworkElement GetFocusableDescendant(DependencyObject element)
		{
			FrameworkElement foundElement = null;

			// ensure that visual tree is complete
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
		public static readonly DependencyProperty IsNewItemProperty = DependencyProperty.Register(nameof(IsNewItem), typeof(bool?), typeof(PpsEditableListboxItem), new FrameworkPropertyMetadata((bool?)null));
		private static readonly DependencyPropertyKey isEditablePropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsEditable), typeof(bool), typeof(PpsEditableListboxItem), new FrameworkPropertyMetadata(true));
		public static readonly DependencyProperty IsEditableProperty = isEditablePropertyKey.DependencyProperty;
		private bool isInEditMode = false;

		protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
		{
			base.OnGotKeyboardFocus(e);

			if (!isInEditMode && IsEditable && IsChildOf(e.NewFocus))
			{
				isInEditMode = true;
				// Ignore when entering currently added item.
				// Transaction already started by entering NewItemPlaceHolder.
				if (!IsCurrentlyAddedItem)
					BeginEdit();
			}
		} // proc OnGotKeyboardFocus

		protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
		{
			base.OnLostKeyboardFocus(e);

			if (isInEditMode && !IsChildOf(e.NewFocus))
			{
				isInEditMode = false;
				// Ignore when NewItemPlaceHolder collapsed.
				// Remains in editing currently added item.
				if (!IsNewItemPlaceHolder)
					EndEdit(this.Equals(e.NewFocus));
			}
		} // proc OnLostKeyboardFocus

		protected override void OnContentChanged(object oldContent, object newContent)
		{
			base.OnContentChanged(oldContent, newContent);
			UpdateIsEditableFlag(newContent);
		} // proc OnContentChanged

		protected override void OnKeyDown(KeyEventArgs e)
		{
			base.OnKeyDown(e);

			if (!IsChildOf(e.OriginalSource))
				return;

			switch (e.Key)
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
						if (parent != null && IsCurrentlyAddedItem)
						{
							e.Handled = true;
							parent.TryEndTransaction(Content, true);
						}
					}
					break;
			}
		} // proc OnKeyDown

		private void BeginEdit()
		{
			var parent = ParentListBox;
			if (parent != null)
				parent.TryBeginTransaction(Content, IsNewItemPlaceHolder);
		} // proc BeginEdit

		private void EndEdit(bool focusedSelf)
		{
			var parent = ParentListBox;
			if (parent != null)
			{
				var addedItem = IsCurrentlyAddedItem;
				parent.TryEndTransaction(Content, !addedItem && !focusedSelf);
				if (addedItem && focusedSelf)
					parent.FocusNewItemPlaceHolderContainer();
			}
		} // proc EndEdit

		private void UpdateIsEditableFlag(object content)
		{
			// property IsNewItemPlaceHolder is not yet updated.
			var isEditable = CollectionView.NewItemPlaceholder.Equals(content);
			if(!isEditable)
			{
				isEditable = content is IPpsEditableObject ppso
					? ppso.IsEditable
					: content is IEditableObject;
			}
			SetValue(isEditablePropertyKey, isEditable);
		} // proc UpdateIsEditableFlag

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
				// MS: Because Nullable<bool> unboxing is very slow (uses reflection) first we cast to bool
				object value = GetValue(IsNewItemProperty);
				return value == null
					? new Nullable<bool>()
					: new Nullable<bool>((bool)value);
			}
			set => SetValue(IsNewItemProperty, value.HasValue ? (bool?)value.Value : null);
		} // prop IsNewItem

		/// <summary>Is content editable?</summary>
		public bool IsEditable
			=> (bool)GetValue(IsEditableProperty);

		private bool IsNewItemPlaceHolder
			=> IsNewItem == null;

		private bool IsCurrentlyAddedItem
		{
			get
			{
				var value = (bool?)GetValue(IsNewItemProperty);
				return value.HasValue && value.Value;
			}
		} // prop IsCurrentlyAddedItem

		private PpsEditableListbox ParentListBox
			=> ParentSelector as PpsEditableListbox;

		internal Selector ParentSelector
			=> ItemsControl.ItemsControlFromItemContainer(this) as Selector;
	} // class PpsEditableListboxItem

	#endregion

}