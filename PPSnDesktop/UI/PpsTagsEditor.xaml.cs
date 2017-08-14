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
		string Name { get; }
		string Value { get; }
		PpsObjectTagClass Class { get; }
	} // interface IPpsTagItem

	public interface IPpsTags : IEnumerable<IPpsTagItem>
	{
		void Append(string tagName, PpsObjectTagClass tagClass, string tagValue);
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
							(isender, ie) => ie.CanExecute = !String.IsNullOrEmpty(((IPpsTagItem)ie.Parameter).Name)
						)
					);
		}

		public readonly static DependencyProperty TagsSourceProperty = DependencyProperty.Register(nameof(PTETagsSource), typeof(PpsObject), typeof(PpsTagsEditor));
		public PpsObject PTETagsSource { get => (PpsObject)GetValue(TagsSourceProperty); set { SetValue(TagsSourceProperty, value); } }

		public readonly static RoutedUICommand AppendTagCommand = new RoutedUICommand("AppendTag", "AppendTag", typeof(PpsTagsEditor));
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
			public string Value { get { return tag != null ? (string)tag.Value : createNewValue; } set { if (tag != null) tag.Update(Class, value); else createNewValue = value; } }

			public PpsObjectTagClass Class => tag != null ? tag.Class : PpsObjectTagClass.Text;

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
				tags.Append(createNewName, PpsObjectTagClass.Text, createNewValue);
				createNewName = String.Empty;
				createNewValue = String.Empty;
			}
		}

		private sealed class PpsTagsImplementation : IPpsTags, INotifyCollectionChanged
		{
			private PpsObject obj;
			private List<PpsTagItemImplementation> tags = new List<PpsTagItemImplementation>();

			public PpsTagsImplementation(PpsObject obj)
			{
				this.obj = obj;
				foreach (var tag in (from t in obj.Tags where t.Class == PpsObjectTagClass.Text select t))
					tags.Add(new PpsTagItemImplementation(tag, this));
				tags.Add(new PpsTagItemImplementation(this));
			}

			public event NotifyCollectionChangedEventHandler CollectionChanged;

			public void Append(string tagName, PpsObjectTagClass tagClass, string tagValue)
			{
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
				return new PpsTagsImplementation(obj);
			return null;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
