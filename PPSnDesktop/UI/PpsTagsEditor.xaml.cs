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
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using TecWare.DE.Stuff;
using TecWare.PPSn.Controls;
using TecWare.PPSn.Data;

namespace TecWare.PPSn.UI
{
	#region -- enum PpsTagOwnerIdentityIcon -------------------------------------------

	/// <summary>Tag owner icon</summary>
	public enum PpsTagOwnerIdentityIcon
	{
		/// <summary>Unknown tag group.</summary>
		None,
		/// <summary>New tag.</summary>
		New,
		/// <summary>System tag.</summary>
		System,
		/// <summary>My tag.</summary>
		Mine,
		/// <summary>Other than my tag.</summary>
		Community,
		/// <summary>Revision tag</summary>
		Revision
	} // enum PpsTagOwnerIdentityIcon

	#endregion

	#region -- class PpsTagsEditor ----------------------------------------------------

	/// <summary>Tag editor user control.</summary>
	public partial class PpsTagsEditor : UserControl
	{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public static readonly DependencyProperty TagClassProperty = DependencyProperty.Register(nameof(TagClass), typeof(PpsObjectTagClass), typeof(PpsTagsEditor), new FrameworkPropertyMetadata(PpsObjectTagClass.Deleted, TagClassChanged));
		public static readonly DependencyProperty ObjectProperty = DependencyProperty.Register(nameof(Object), typeof(PpsObject), typeof(PpsTagsEditor), new FrameworkPropertyMetadata(ObjectChanged));

		private readonly static DependencyPropertyKey tagsSourcePropertyKey = DependencyProperty.RegisterReadOnly(nameof(TagsSource), typeof(ListCollectionView), typeof(PpsTagsEditor), new FrameworkPropertyMetadata(null));
		public readonly static DependencyProperty TagsSourceProperty = tagsSourcePropertyKey.DependencyProperty;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		/// <summary></summary>
		public PpsTagsEditor()
		{
			InitializeComponent();
		} // ctor

		private void RefreshTagsSource()
		{
			var obj = Object;
			var cls = TagClass;


			if (TagsSource?.SourceCollection is PpsTagsModel currentSource)
			{
				if (currentSource.Object == obj
					&& currentSource.Filter == cls)
					return;

				currentSource.DetachObject();
			}

			if (obj != null && cls != PpsObjectTagClass.Deleted)
			{
				SetValue(tagsSourcePropertyKey,
					new ListCollectionView(new PpsTagsModel(obj, cls))
					{
						NewItemPlaceholderPosition = cls == PpsObjectTagClass.Tag ? NewItemPlaceholderPosition.AtEnd : NewItemPlaceholderPosition.AtBeginning
					}
				);
			}
			else
				SetValue(tagsSourcePropertyKey, null);
		} // proc RefreshTagsSource

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

	/// <summary>Model for tag.</summary>
	public sealed class PpsTagItemModel : IPpsEditableObject, INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler PropertyChanged;

		private readonly PpsObject ppsObject;
		private PpsObjectTagBase tag;

		private bool isEditing = false;
		private bool tagNameExists = false;
		private bool isModified = false;
		private string currentName = null;
		private object currentValue = null;
		private readonly PpsObjectTagClass currentClass;

		public PpsTagItemModel(PpsObject ppsObject, PpsObjectTagClass newClass)
		{
			this.ppsObject = ppsObject;
			this.tag = null;
			
			this.currentClass = newClass;
		} // ctor

		public PpsTagItemModel(PpsObject ppsObject, PpsObjectTagBase tag)
		{
			this.ppsObject = ppsObject;
			this.tag = tag;
			
			this.currentClass = tag.Class;
		} // ctor

		#region -- Tag Property Changed -----------------------------------------------

		private void TagPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (IsEditing)
				return;
			switch (e.PropertyName)
			{
				case nameof(PpsObjectUserTag.Name):
					OnPropertyChanged(nameof(Name));
					break;
				case nameof(PpsObjectUserTag.Value):
					OnPropertyChanged(nameof(Value));
					break;
				case nameof(PpsObjectUserTag.Class):
					OnPropertyChanged(nameof(Class));
					break;
				case nameof(PpsObjectUserTag.TimeStamp):
					OnPropertyChanged(nameof(TimeStamp));
					break;
			}
		} // proc TagPropertyChanged

		private void AttachPropertyChanged()
		{
			if (tag is PpsObjectUserTag userTag)
				AttachPropertyChanged(userTag);
			else if (tag is PpsObjectSystemTag sysTag)
				AttachPropertyChanged(sysTag);
			else if (tag is PpsObjectRevisionTag revTag)
				AttachPropertyChanged(revTag);
		} // proc AttachPropertyChanged
		
		private void AttachPropertyChanged<T>(T tag)
			where T : PpsObjectTagBase
			=> WeakEventManager<T, PropertyChangedEventArgs>.AddHandler(tag, nameof(INotifyPropertyChanged.PropertyChanged), TagPropertyChanged);

		public void DetachPropertyChanged()
		{
			if (tag is PpsObjectUserTag userTag)
				DetachPropertyChanged(userTag);
			else if (tag is PpsObjectSystemTag sysTag)
				DetachPropertyChanged(sysTag);
			else if (tag is PpsObjectRevisionTag revTag)
				DetachPropertyChanged(revTag);
		} // proc DetachPropertyChanged

		private void DetachPropertyChanged<T>(T tag)
			where T : PpsObjectTagBase
			=> WeakEventManager<T, PropertyChangedEventArgs>.RemoveHandler(tag, nameof(INotifyPropertyChanged.PropertyChanged), TagPropertyChanged);

		private void OnPropertyChanged(string propertyName)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		#endregion

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
			CheckTagName();

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
				ppsObject.Tags.AppendUserTag(ppsObject.Environment.UserId, new PpsObjectTag(currentName, currentClass, currentValue));
				AttachPropertyChanged();
			}
			else if (currentName != tag.Name)
			{
				var tmp = tag;
				tag = null; // remove tag, that refresh will not remove this item
				((PpsObjectUserTag)tmp).Remove();
				DetachPropertyChanged();
				ppsObject.Tags.AppendUserTag(ppsObject.Environment.UserId, new PpsObjectTag(currentName, currentClass, currentValue));
				AttachPropertyChanged();
			}
			else if (!Equals(tag.Value, currentValue))
				((PpsObjectUserTag)tag).UpdateValue(currentClass, currentValue);

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
			TagNameExists = false;
			
		} // proc CancelEdit

		public void Remove()
		{
			((PpsObjectUserTag)tag).Remove();
			ppsObject.UpdateLocalAsync().AwaitTask();
		} // proc Remove

		private void CheckTagName()
		{
			TagNameExists = ppsObject.Tags.All.OfType<PpsObjectUserTag>().FirstOrDefault(ut => ut.UserId == CurrentUserId && ut != tag && ut.Name == currentName) != null;
		} // proc CheckTagName

		private void SetValue<T>(ref T value, T newValue, string propertyName)
		{
			CheckEditMode();

			if (!Equals(value, newValue))
			{
				value = newValue;
				OnPropertyChanged(propertyName);
				SetValue(ref isModified, true, nameof(IsModified));
			}
		} // proc SetValue

		private long CurrentUserId => PpsEnvironment.GetEnvironment().UserId;

		/// <summary>Tag name</summary>
		public string Name
		{
			get => IsEditing || IsNew ? currentName : tag.Name;
			set { SetValue(ref currentName, value, nameof(Name)); CheckTagName(); }
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
		public DateTime TimeStamp => tag?.TimeStamp ?? DateTime.Now;

		/// <summary>Is the tag editable</summary>
		public bool IsEditable => IsNew || (tag is PpsObjectUserTag userTag) && userTag.UserId == CurrentUserId;
		/// <summary>Is this tag a new one.</summary>
		public bool IsNew => tag == null;
		/// <summary>Is the tag in editmode</summary>
		public bool IsEditing => isEditing;
		/// <summary>Is the current data modified.</summary>
		public bool IsModified => isModified;
		/// <summary>If the tag name already exists.</summary>
		public bool TagNameExists
		{
			get => tagNameExists;
			private set
			{
				if (tagNameExists != value)
				{
					tagNameExists = value;
					OnPropertyChanged(nameof(TagNameExists));
				}
			}
		} // prop TagNameExists

		/// <summary>User, that created the tag.</summary>
		public string UserName =>
			IsNew
				? PpsEnvironment.GetEnvironment().UsernameDisplay
				: tag is PpsObjectUserTag userTag ? userTag.User?.GetProperty("Login", "<error>") ?? String.Empty : String.Empty;

		public PpsTagOwnerIdentityIcon OwnerIdentityIcon
		{
			get
			{
				if (IsNew)
					return PpsTagOwnerIdentityIcon.New;
				else
				{
					switch (tag)
					{
						case PpsObjectUserTag userTag:
							return userTag.UserId == CurrentUserId ? PpsTagOwnerIdentityIcon.Mine : PpsTagOwnerIdentityIcon.Community;
						case PpsObjectSystemTag sysTag:
							return PpsTagOwnerIdentityIcon.System;
						case PpsObjectRevisionTag revTag:
							return PpsTagOwnerIdentityIcon.Revision;
						default:
							return PpsTagOwnerIdentityIcon.None;
					}
				}
			}
		} // prop OwnerIdentityIcon

		/// <summary>Only for internal use.</summary>
		internal PpsObjectTagBase InnerTag => tag;
	} // class PpsTagItemModel

	#endregion

	#region -- class PpsTagsModel -----------------------------------------------------

	/// <summary>Tag model</summary>
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
			=> ((ICollection)items).CopyTo(array, index);

		void IList.Clear() 
			=> throw new NotSupportedException();

		int IList.Add(object value)
			=> Insert((PpsTagItemModel)value, -1);

		void IList.Insert(int index, object value)
			=> Insert((PpsTagItemModel)value, index);

		private int IndexOf(PpsObjectTagBase tag)
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
						var innerTag = (PpsObjectTagBase)e.NewItems[0];
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
						var idx = IndexOf((PpsObjectTagBase)e.NewItems[0]);
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
				foreach (var innerTag in InnerTagList.All) // can raise a Reset Event
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
					if (c.InnerTag != null) // do not remove new tags
					{
						c.DetachPropertyChanged();
						items.Remove(c);
					}
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
				if (tag.IsEditable)
					tag.Remove();
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