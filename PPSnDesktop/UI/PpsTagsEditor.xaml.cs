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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using TecWare.DE.Stuff;
using TecWare.PPSn.Controls;
using TecWare.PPSn.Data;

namespace TecWare.PPSn.UI
{
	#region -- enum PpsTagOwnerIdentityIcon -------------------------------------------

	public enum PpsTagOwnerIdentityIcon
	{
		New,
		System,
		Mine,
		Community
	} // enum PpsTagOwnerIdentityIcon

	#endregion

	#region -- class PpsTagsEditor ----------------------------------------------------

	public partial class PpsTagsEditor : UserControl
	{
		public readonly static DependencyProperty TagClassProperty = DependencyProperty.Register(nameof(TagClass), typeof(PpsObjectTagClass), typeof(PpsTagsEditor), new FrameworkPropertyMetadata(PpsObjectTagClass.Deleted, TagClassChanged));
		public readonly static DependencyProperty ObjectProperty = DependencyProperty.Register(nameof(Object), typeof(PpsObject), typeof(PpsTagsEditor), new FrameworkPropertyMetadata(ObjectChanged));

		private readonly static DependencyPropertyKey tagsSourcePropertyKey = DependencyProperty.RegisterReadOnly(nameof(TagsSource), typeof(ListCollectionView), typeof(PpsTagsEditor), new FrameworkPropertyMetadata(null));
		public readonly static DependencyProperty TagsSourceProperty = tagsSourcePropertyKey.DependencyProperty;

		public PpsTagsEditor()
		{
			InitializeComponent();
		} // ctor

		private void RefreshTagsSource()
		{
			var currentSource = TagsSource?.SourceCollection as PpsTagsModel;

			var obj = Object;
			var cls = TagClass;

			if (currentSource == null)
			{
				if (obj != null && cls != PpsObjectTagClass.Deleted)
					SetValue(tagsSourcePropertyKey, CreateCollectionView(obj, cls));
			}
			else
			{
				if (currentSource.Object != obj
					|| currentSource.Filter != cls)
				{
					currentSource.DetachObject();

					if (obj != null && cls != PpsObjectTagClass.Deleted)
						SetValue(tagsSourcePropertyKey, CreateCollectionView(obj, cls));
				}
			}
		} // proc RefreshTagsSource

		private static ListCollectionView CreateCollectionView(PpsObject obj, PpsObjectTagClass cls)
		{
			var collectionView = new ListCollectionView(new PpsTagsModel(obj, cls))
			{
				NewItemPlaceholderPosition = cls == PpsObjectTagClass.Tag ? NewItemPlaceholderPosition.AtEnd : NewItemPlaceholderPosition.AtBeginning
			};
			return collectionView;
		} // func CreateCollectionView

		private static void TagClassChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsTagsEditor)d).RefreshTagsSource();

		private static void ObjectChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsTagsEditor)d).RefreshTagsSource();
		
		private void tagAttributes_AddNewItemFactory(object sender, AddNewItemFactoryEventArgs args)
		{
			args.NewItem = new PpsTagItemModel(Object, TagClass);
			args.Handled = true;
		} // event tagAttributes_AddNewItemFactory

		public PpsObjectTagClass TagClass { get => (PpsObjectTagClass)GetValue(TagClassProperty); set { SetValue(TagClassProperty, value); } }
		public PpsObject Object { get => (PpsObject)GetValue(ObjectProperty); set => SetValue(ObjectProperty, value); }

		public ListCollectionView TagsSource { get => (ListCollectionView)GetValue(TagsSourceProperty); }
	} // class PpsTagsEditor

	#endregion

	#region -- class PpsTagItemModel --------------------------------------------------

	/// <summary></summary>
	public sealed class PpsTagItemModel : IPpsEditableObject, INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler PropertyChanged;

		private readonly PpsObject ppsObject;
		private PpsObjectTagView tag;

		private bool isEditing = false;
		private bool isModified = false;
		private string currentName = null;
		private object currentValue = null;
		private PpsObjectTagClass currentClass;

		public PpsTagItemModel(PpsObject ppsObject, PpsObjectTagClass newClass)
		{
			this.ppsObject = ppsObject;
			this.tag = null;
			
			this.currentClass = newClass;
		} // ctor

		public PpsTagItemModel(PpsObject ppsObject, PpsObjectTagView tag)
		{
			this.ppsObject = ppsObject;
			this.tag = tag;
			
			this.currentClass = tag.Class;
		} // ctor

		private void TagPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (IsEditing)
				return;
			switch (e.PropertyName)
			{
				case nameof(PpsObjectTagView.Name):
					OnPropertyChanged(nameof(Name));
					break;
				case nameof(PpsObjectTagView.Value):
					OnPropertyChanged(nameof(Value));
					break;
				case nameof(PpsObjectTagView.Class):
					OnPropertyChanged(nameof(Class));
					break;
				case nameof(PpsObjectTagView.CreationStamp):
					OnPropertyChanged(nameof(CreationStamp));
					break;
			}
		} // proc TagPropertyChanged

		private void AttachPropertyChanged()
			=> WeakEventManager<PpsObjectTagView, PropertyChangedEventArgs>.AddHandler(tag, nameof(INotifyPropertyChanged.PropertyChanged), TagPropertyChanged);

		public void DetachPropertyChanged()
			=> WeakEventManager<PpsObjectTagView, PropertyChangedEventArgs>.RemoveHandler(tag, nameof(INotifyPropertyChanged.PropertyChanged), TagPropertyChanged);

		private void OnPropertyChanged(string propertyName)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		private void CheckEditMode()
		{
			if (!isEditing)
				throw new InvalidOperationException("Tag is not in edit mode.");
		} // proc CheckEditMode

		public void BeginEdit()
		{
			if (!IsEditable)
				throw new InvalidOperationException("This tag is readonly.");

			currentName = tag?.Name;
			currentValue = tag?.Value;

			isEditing = true;
			isModified = false;

			if (IsNew && currentName == null)
			{
				switch (Class)
				{
					case PpsObjectTagClass.Note:
						Name = "N" + Guid.NewGuid().ToString("N");
						break;
				}
			}
		} // proc BeginEdit

		public void EndEdit()
		{
			// check if in edit mode
			CheckEditMode();

			// update the tag behind
			if (IsNew)
			{
				ppsObject.Tags.UpdateTag(currentName, currentClass, currentValue, t => tag = t);
				AttachPropertyChanged();
			}
			else if (currentName != tag.Name)
			{
				var tmp = tag;
				tag = null; // remove tag, that refresh will not remove this item
				tmp.Remove();
				DetachPropertyChanged();
				ppsObject.Tags.UpdateTag(currentName, currentClass, currentValue, t => tag = t);
				AttachPropertyChanged();
			}
			else if (!Object.Equals(tag.Value, currentValue))
				tag.Update(currentClass, currentValue);

			isEditing = false;
			isModified = false;

			if (ppsObject.IsChanged)
				ppsObject.UpdateLocalAsync().AwaitTask();
		} // proc EndEdit

		public void CancelEdit()
		{
			currentName = null;
			currentValue = null;
			isEditing = false;
		} // proc CancelEdit

		private void SetValue<T>(ref T value, T newValue, string propertyName)
		{
			CheckEditMode();

			if (!Object.Equals(value, newValue))
			{
				value = newValue;
				OnPropertyChanged(propertyName);
				if (propertyName == nameof(Name))
					OnPropertyChanged(nameof(WillOverwrite));
				SetValue(ref isModified, true, nameof(IsModified));
			}
		} // proc SetValue

		private long CurrentUserId => PpsEnvironment.GetEnvironment().UserId;

		/// <summary>Tag name</summary>
		public string Name
		{
			get => IsEditing || IsNew ? currentName : tag.Name;
			set => SetValue(ref currentName, value, nameof(Name));
		} // prop Name

		/// <summary>Value of the tag.</summary>
		public object Value
		{
			get => IsEditing || IsNew ? currentValue : tag.Value;
			set => SetValue(ref currentValue, value, nameof(Value));
		} // prop Value

		/// <summary>Tag class</summary>
		public PpsObjectTagClass Class => IsEditing || IsNew ? currentClass : tag.Class;
		/// <summary>Tag create time stamp.</summary>
		public DateTime CreationStamp => tag?.CreationStamp ?? DateTime.Now;

		/// <summary>Is the tag editable</summary>
		public bool IsEditable => IsNew || tag.UserId == CurrentUserId;
		/// <summary>Is this tag a new one.</summary>
		public bool IsNew => tag == null;
		/// <summary>Is the tag in editmode</summary>
		public bool IsEditing => isEditing;
		/// <summary>Is the current data modified.</summary>
		public bool IsModified => isModified;
		/// <summary>Is already a Tag with that Name present, thus overwriting the old value.</summary>
		public bool WillOverwrite => IsNew && IsEditing && ppsObject.Tags.IndexOf(Name) >= 0;

		/// <summary>User, that created the tag.</summary>
		public string UserName => IsNew ? PpsEnvironment.GetEnvironment().GetMemberValue("UserName") as string : tag.User?.GetProperty("Login", "<error>") ?? ""; // todo:

		public PpsTagOwnerIdentityIcon OwnerIdentityIcon
		{
			get
			{
				if (IsNew)
					return PpsTagOwnerIdentityIcon.New;
				else if (tag.UserId == 0)
					return PpsTagOwnerIdentityIcon.System;
				else if (tag.UserId == CurrentUserId)
					return PpsTagOwnerIdentityIcon.Mine;
				else
					return PpsTagOwnerIdentityIcon.Community;
			}
		} // prop OwnerIdentityIcon

		/// <summary>Only for internal use.</summary>
		internal PpsObjectTagView InnerTag => tag;
	} // class PpsTagItemModel

	#endregion

	#region -- class PpsTagsModel -----------------------------------------------------

	public sealed class PpsTagsModel : IList, INotifyCollectionChanged
	{
		public event NotifyCollectionChangedEventHandler CollectionChanged;

		private readonly PpsObjectTagClass classFilter;
		private readonly PpsObject ppsObject;

		private readonly List<PpsTagItemModel> items = new List<PpsTagItemModel>(); // shadow list

		public PpsTagsModel(PpsObject ppsObject, PpsObjectTagClass classFilter)
		{
			this.ppsObject = ppsObject;
			this.classFilter = classFilter;

			AttachObject();
		} // ctor

		private void AttachObject()
		{
			Refresh(true);

			WeakEventManager<PpsObjectTags, NotifyCollectionChangedEventArgs>.AddHandler(ppsObject.Tags, nameof(INotifyCollectionChanged.CollectionChanged), InnerCollectionChanged);
		} // proc AttachObject

		public void DetachObject()
		{
			WeakEventManager<PpsObjectTags, NotifyCollectionChangedEventArgs>.RemoveHandler(ppsObject.Tags, nameof(INotifyCollectionChanged.CollectionChanged), InnerCollectionChanged);

			// clear list
			items.ForEach(c => c.DetachPropertyChanged());
			items.Clear();
		} // proc DetachObject

		void ICollection.CopyTo(Array array, int index)
			=> throw new NotImplementedException("If it is useful. It must clear all items of the type.");

		void IList.Clear() 
			=> throw new NotImplementedException("If it is useful. It must clear all items of the type.");

		int IList.Add(object value)
			=> Insert((PpsTagItemModel)value, -1);

		void IList.Insert(int index, object value)
			=> Insert((PpsTagItemModel)value, index);

		private int IndexOf(PpsObjectTagView tag)
			=> items.FindIndex(c => tag == c.InnerTag);

		bool IList.Contains(object value)
			=> items.Contains((PpsTagItemModel)value);

		int IList.IndexOf(object value)
			=> items.IndexOf((PpsTagItemModel)value);

		void IList.Remove(object value)
			=> Remove((PpsTagItemModel)value);

		void IList.RemoveAt(int index)
			=> RemoveAt(index);

		IEnumerator IEnumerable.GetEnumerator()
			=> items.GetEnumerator();

		private void InnerCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			switch(e.Action)
			{
				case NotifyCollectionChangedAction.Add:
					if (e.NewItems.Count != 1)
						throw new NotSupportedException();

					{
						var innerTag = (PpsObjectTagView)e.NewItems[0];
						if (innerTag.Class != classFilter)
							return;

						if (IndexOf(innerTag) == -1) // not in list
							Insert(new PpsTagItemModel(ppsObject, innerTag), -1);
					}
					break;
				case NotifyCollectionChangedAction.Remove:
					if (e.NewItems.Count != 1)
						throw new NotSupportedException();

					{
						var idx = IndexOf((PpsObjectTagView)e.NewItems[0]);
						if (idx >= 0)
							RemoveFromView(items[idx], idx);
					}
					break;

				case NotifyCollectionChangedAction.Move:
					throw new NotImplementedException();
				case NotifyCollectionChangedAction.Reset:
					Refresh();
					break;
			}
		} // proc InnerCollectionChanged

		private bool withInRefresh = false;

		public void Refresh(bool force = false)
		{
			if (withInRefresh)
				return;

			withInRefresh = true;
			try
			{
				var itemsToRemove = new List<PpsTagItemModel>();

				// clear all models, on force
				if (force)
				{
					items.ForEach(t => t.DetachPropertyChanged());
					items.Clear();
				}
				else
					itemsToRemove.AddRange(items);

				// rebuild models
				foreach (var innerTag in InnerTagList) // can raise a Reset Event
				{
					if (innerTag.Class == classFilter)
					{
						if (force)
							items.Add(new PpsTagItemModel(ppsObject, innerTag));
						else
						{
							var idx = IndexOf(innerTag);
							if (idx == -1)
								items.Add(new PpsTagItemModel(ppsObject, innerTag));
							else
								itemsToRemove.Remove(items[idx]);
						}
					}
				}

				// remove not updated items
				foreach (var c in itemsToRemove)
				{
					if (c.InnerTag != null)
						items.Remove(c);
				}

				CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
			}
			finally
			{
				withInRefresh = false;
			}			
		} // proc Refresh

		private void Remove(PpsTagItemModel tag)
		{
			var idx = items.IndexOf(tag);
			if (!tag.IsNew)
			{
				// remove in list -> notify changes the view
				tag.InnerTag.Remove();
				ppsObject.UpdateLocalAsync().AwaitTask();
			}
			else
				RemoveFromView(tag, idx);
		} // proc RemoveCore

		private void RemoveAt(int index)
		{
			if (items.Count > 0) // list is destroyed
				Remove(items[index]);
		} // proc RemoveAtt

		private int Insert(PpsTagItemModel tag, int insertAt)
		{
			// find index of the tag
			var idx = items.IndexOf(tag);
			if (idx == -1)
			{
				idx = IndexOf(tag.InnerTag);
				if (idx >= 0)
					items[idx].DetachPropertyChanged();
			}

			// insert at the end
			if (insertAt <= -1)
				insertAt = items.Count;

			// if index changed
			if(idx != insertAt)
			{
				if (idx != -1)
				{
					RemoveFromView(tag, idx);
					if (insertAt > idx)
						insertAt--;
				}

				items.Insert(insertAt, tag);
				CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, tag, insertAt));
			}

			return insertAt;
		} // proc Insert

		private void RemoveFromView(PpsTagItemModel tag, int idx)
		{
			items.RemoveAt(idx);
			CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, tag, idx));
		} // proc RemoveFromView

		bool IList.IsReadOnly => false;
		bool IList.IsFixedSize => false;

		object ICollection.SyncRoot => null;
		bool ICollection.IsSynchronized => false;

		int ICollection.Count => items.Count;

		object IList.this[int index] { get => items[index]; set => throw new NotSupportedException(); }

		internal PpsObjectTags InnerTagList => ppsObject.Tags;

		public PpsObjectTagClass Filter => classFilter;
		public PpsObject Object => ppsObject;
	} // class PpsTagsModel

	#endregion
}