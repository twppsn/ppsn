using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;

namespace TecWare.PPSn.UI
{
	#region -- Interfaces ---------------------------------------------------------------

	public interface IPpsTagItem
	{
		void Append();
		void Remove();
		void Save();
		string Name { get; }
		string Value { get; }
		bool CanSave { get; }
		PpsObjectTagClass Class { get; }
	} // interface IPpsTagItem

	public interface IPpsTags : IEnumerable<IPpsTagItem>
	{
		void Append(string tagName, PpsObjectTagClass tagClass, object tagValue);
		void Remove(IPpsTagItem tag);
	} // interface IPpsTags

	#endregion

	#region -- enum PpsTagOwnerIdentityIcon ---------------------------------------------

	public enum PpsTagOwnerIdentityIcon
	{
		System,
		Mine,
		Community
	} // enum PpsTagOwnerIdentityIcon

	#endregion

	/// <summary>
	/// Interaction logic for PpsTagsEditor.xaml
	/// </summary>
	public partial class PpsTagsEditor : UserControl
	{
		public PpsTagsEditor()
		{
			InitializeComponent();

			CommandBindings.Add(
						new CommandBinding(RemoveTagCommand,
							(isender, ie) =>
							{
								((IPpsTagItem)ie.Parameter).Remove();
								ie.Handled = true;
							},
							(isender, ie) => ie.CanExecute = true
						)
					);
			CommandBindings.Add(
						new CommandBinding(AppendTagCommand,
							(isender, ie) =>
							{
								((IPpsTagItem)ie.Parameter).Append();
								ie.Handled = true;
							},
							(isender, ie) => ie.CanExecute = (((IPpsTagItem)ie.Parameter).Class == PpsObjectTagClass.Tag && !String.IsNullOrEmpty(((IPpsTagItem)ie.Parameter).Name)) ||
															 (((IPpsTagItem)ie.Parameter).Class == PpsObjectTagClass.Text && !String.IsNullOrEmpty(((IPpsTagItem)ie.Parameter).Name) && !String.IsNullOrEmpty(((IPpsTagItem)ie.Parameter).Value)) ||
															 (((IPpsTagItem)ie.Parameter).Class == PpsObjectTagClass.Date && !String.IsNullOrEmpty(((IPpsTagItem)ie.Parameter).Name) && DateTime.TryParse(((IPpsTagItem)ie.Parameter).Value, out var temp))
						)
					);
			CommandBindings.Add(
						new CommandBinding(SaveTagCommand,
							(isender, ie) =>
							{
								((IPpsTagItem)ie.Parameter).Save();
								ie.Handled = true;
							},
							(isender, ie) => ie.CanExecute = ((IPpsTagItem)ie.Parameter).CanSave
						)
					);
		}

		public readonly static DependencyProperty TagsClassProperty = DependencyProperty.Register(nameof(TagsClass), typeof(PpsObjectTagClass), typeof(PpsTagsEditor));
		public PpsObjectTagClass TagsClass { get => (PpsObjectTagClass)GetValue(TagsClassProperty); set { SetValue(TagsClassProperty, value); } }

		public readonly static DependencyProperty TagsSourceProperty = DependencyProperty.Register(nameof(TagsSource), typeof(PpsObject), typeof(PpsTagsEditor));
		public PpsObject TagsSource { get => (PpsObject)GetValue(TagsSourceProperty); set { SetValue(TagsSourceProperty, value); } }

		public readonly static RoutedUICommand AppendTagCommand = new RoutedUICommand("AppendTag", "AppendTag", typeof(PpsTagsEditor));
		public readonly static RoutedUICommand SaveTagCommand = new RoutedUICommand("SaveTag", "SaveTag", typeof(PpsTagsEditor));
		public readonly static RoutedUICommand RemoveTagCommand = new RoutedUICommand("RemoveTag", "RemoveTag", typeof(PpsTagsEditor));
	}

	public sealed class PpsObjectTagsConverter : IValueConverter
	{
		private sealed class PpsTagItemImplementation : IPpsTagItem, INotifyPropertyChanged
		{
			private PpsObjectTagView tag;
			private PpsTagsImplementation tags;

			public PpsTagItemImplementation(PpsTagsImplementation tags)
			{
				this.tags = tags;
			}

			public PpsTagItemImplementation(PpsObjectTagView tag, PpsTagsImplementation tags)
			{
				this.tag = tag;
				this.tags = tags;
			}
			private string createNewName = String.Empty;
			public string Name { get { return tag != null ? tag.Name : createNewName; } set { createNewName = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name))); } }

			private string createNewValue = String.Empty;
			public string Value
			{
				get
				{
					switch (tags.TagClass)
					{
						case PpsObjectTagClass.Text:
						case PpsObjectTagClass.Note:
							return tag != null && String.IsNullOrEmpty(createNewValue) ? (string)tag.Value : createNewValue;
						case PpsObjectTagClass.Date:
							return tag != null && String.IsNullOrEmpty(createNewValue) ? tag.Value is DateTime ? ((DateTime)tag.Value).ToLocalTime().ToShortDateString() : DateTime.Parse((string)tag.Value, CultureInfo.InvariantCulture).ToLocalTime().ToShortDateString() : createNewValue;
						case PpsObjectTagClass.Tag:
							throw new FieldAccessException();
					}
					return tag.Value.ToString();
				}

				set
				{
					switch (tags.TagClass)
					{
						case PpsObjectTagClass.Text:
							if (tag != null)
							{
								tag.Update(Class, value);
								tags.Commit();
							}
							else
								createNewValue = (string)value;
							break;
						case PpsObjectTagClass.Note:
						case PpsObjectTagClass.Date:
							createNewValue = (string)value;
							break;
						case PpsObjectTagClass.Tag:
							throw new FieldAccessException();
					}
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
				}
			}

			public PpsObjectTagClass Class => tag != null ? tag.Class : tags.TagClass;

			public long UserId => tag != null ? tag.UserId : PpsEnvironment.GetEnvironment().UserId;

			public bool IsUserChangeable => tag != null ? tag.UserId == PpsEnvironment.GetEnvironment().UserId : true;

			public bool CreateNewBool => tag == null;

			public bool CanDelete => IsUserChangeable && !CreateNewBool ? true : false;

			public PpsTagOwnerIdentityIcon OwnerIdentityIcon
			{
				get
				{
					if (UserId == 0)
						return PpsTagOwnerIdentityIcon.System;
					else if (UserId == PpsEnvironment.GetEnvironment().UserId)
						return PpsTagOwnerIdentityIcon.Mine;
					else
						return PpsTagOwnerIdentityIcon.Community;
				}
			} // prop OwnerIdentityIcon

			public event PropertyChangedEventHandler PropertyChanged;

			public void Remove()
			{
				tags.Remove(this);
			}

			public void Append()
			{
				tags.Append(createNewName, createNewValue);
				createNewName = String.Empty;
				createNewValue = String.Empty;
			}

			public void Save()
			{
				tag.Update(Class, createNewValue);
				tags.Commit();
				createNewValue = String.Empty;
			}

			public bool CanSave
				=> !String.IsNullOrEmpty(createNewValue);
		}

		private sealed class PpsTagsImplementation : IPpsTags, INotifyCollectionChanged
		{
			private PpsObject obj;
			private PpsObjectTagClass tagClass;
			private List<PpsTagItemImplementation> tags = new List<PpsTagItemImplementation>();

			public PpsTagsImplementation(PpsObject obj, PpsObjectTagClass tagClass)
			{
				this.obj = obj;
				obj.Tags.RefreshTags();
				this.tagClass = tagClass;

				if (tagClass == PpsObjectTagClass.Date)
					foreach (var tag in (from t in obj.Tags where t.Class == this.tagClass select t).OrderBy(t => (DateTime)t.Value).ThenBy(t => t.Name))
						tags.Add(new PpsTagItemImplementation(tag, this));
				else
					foreach (var tag in (from t in obj.Tags where t.Class == this.tagClass select t).OrderBy(t => t.UserId).ThenBy(t => t.Name))
						tags.Add(new PpsTagItemImplementation(tag, this));

				if (tagClass == PpsObjectTagClass.Note)
					tags.Insert(0, new PpsTagItemImplementation(this));
				else
					tags.Add(new PpsTagItemImplementation(this));
			}

			public event NotifyCollectionChangedEventHandler CollectionChanged;

			public PpsObjectTagClass TagClass
				=> tagClass;

			public void Append(string tagName)
				=> Append(tagName, tagClass, null);
			public void Append(string tagName, string tagValue)
				=> Append(tagName, tagClass, tagValue);

			public void Append(string tagName, PpsObjectTagClass tagClass, object tagValue)
			{
				if (String.IsNullOrEmpty(tagName))
					throw new ArgumentNullException("Tag Name");

				switch (tagClass)
				{
					case PpsObjectTagClass.Text:
						if (!((tagValue is string) && !String.IsNullOrEmpty((string)tagValue)))
							throw new ArgumentNullException("Tag Value");
						break;
					case PpsObjectTagClass.Tag:
						tagValue = null;
						break;
					case PpsObjectTagClass.Date:
						tagValue = DateTime.Parse((string)tagValue).ToUniversalTime().ToString(CultureInfo.InvariantCulture);
						//tagValue = DateTime.Parse((string)tagValue).ToUniversalTime().ToFileTime();
						break;
				}
				var tag = this.obj.Tags.UpdateTag(tagName, tagClass, tagValue);
				tags.Insert(tags.Count - 1, new PpsTagItemImplementation(tag, this));
				//if (tagClass == PpsObjectTagClass.Date)
					//tags.Sort((a,b)=>((DateTime)a.Value - (DateTime)b.Value))
				obj.UpdateLocalAsync().AwaitTask();
				CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
			}

			public void Remove(IPpsTagItem tag)
			{
				var remtag = (PpsTagItemImplementation)tag;
				var remidx = tags.IndexOf(remtag);

				obj.Tags.Remove(tag.Name);
				obj.UpdateLocalAsync().AwaitTask();

				tags.Remove(remtag);
				CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, remtag, remidx));
			}

			public void Commit()
			{
				obj.UpdateLocalAsync().AwaitTask();
			}

			public IEnumerator<IPpsTagItem> GetEnumerator()
			{
				throw new NotImplementedException();
			}

			IEnumerator IEnumerable.GetEnumerator()
			{
				return tags.GetEnumerator();
			}
		}

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is PpsObject obj)
				return new PpsTagsImplementation(obj, (PpsObjectTagClass)parameter);
			return null;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
