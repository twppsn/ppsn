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
							(isender, ie) => ie.CanExecute = !String.IsNullOrEmpty(((IPpsTagItem)ie.Parameter).Name) && (((IPpsTagItem)ie.Parameter).Class == PpsObjectTagClass.Tag || !String.IsNullOrEmpty(((IPpsTagItem)ie.Parameter).Value))
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
			public string Name { get { return tag != null ? tag.Name : createNewName; } set { createNewName = value; } }

			private string createNewValue = String.Empty;
			public string Value
			{
				get
				{
					return tag != null && String.IsNullOrEmpty(createNewValue) ? (string)tag.Value : createNewValue;
				}

				set
				{
					if ((tag != null) && (tag.Class != PpsObjectTagClass.Note))
					{
						tag.Update(Class, value);
						tags.Commit();
					}
					else
						createNewValue = value;
				}
			}

			public PpsObjectTagClass Class => tag != null ? tag.Class : tags.TagClass;

			public long UserId => tag != null ? tag.UserId : PpsEnvironment.GetEnvironment().UserId;

			public bool IsUserChangeable => tag != null ? tag.UserId == PpsEnvironment.GetEnvironment().UserId : true;

			public Visibility CreateNewVisibility => tag != null ? Visibility.Collapsed : Visibility.Visible;
			public bool CreateNewBool => tag == null;
			public Visibility CanDelete => IsUserChangeable && !CreateNewBool ? Visibility.Visible : Visibility.Collapsed;

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
				this.tagClass = tagClass;
				foreach (var tag in (from t in obj.Tags where t.Class == this.tagClass select t))
					tags.Add(new PpsTagItemImplementation(tag, this));
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
				switch (tagClass)
				{
					case PpsObjectTagClass.Text:
						if (!((tagValue is string) && !String.IsNullOrEmpty((string)tagValue)))
							throw new ArgumentNullException("Tag Value");
						break;
					case PpsObjectTagClass.Tag:
						tagValue = null;
						break;
				}
				var tag = this.obj.Tags.UpdateTag(tagName, tagClass, tagValue);
				tags.Insert(tags.Count - 1, new PpsTagItemImplementation(tag, this));
				obj.UpdateLocalAsync().AwaitTask();
				CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
			}

			public void Remove(IPpsTagItem tag)
			{
				obj.Tags.Remove(tag.Name);
				obj.UpdateLocalAsync().AwaitTask();
				tags.Remove((from t in tags where t.Name == tag.Name select t).First());
				CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
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
