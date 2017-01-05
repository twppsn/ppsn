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
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using TecWare.PPSn.Controls;

namespace TecWare.PPSn.UI
{
	#region -- class PpsCommand ---------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Implements a command that can call a delegate. This command
	/// can also be added to the idle collection.</summary>
	public class PpsCommand : ICommand, IPpsIdleAction
	{
		private readonly PpsEnvironment environment;
		private readonly Action<object> command;
		private readonly Func<object, bool> canExecute;

		public PpsCommand(PpsEnvironment environment, Action<object> command, Func<object, bool> canExecute = null, bool idleCall = true)
		{
			this.environment = environment;
			this.command = command;
			this.canExecute = canExecute;

			if (canExecute != null && idleCall)
				environment.AddIdleAction(this);
		} // ctor

		#region -- ICommand Member ---------------------------------------------------------

		public event EventHandler CanExecuteChanged;

		public virtual bool CanExecute(object parameter)
			=> canExecute == null || canExecute(parameter);

		public virtual void Execute(object parameter)
		{
			try
			{
				command(parameter);
			}
			catch (Exception e)
			{
				environment.ShowException(ExceptionShowFlags.None, e);
			}
		} // proc Execute

		#endregion

		public void Refresh()
			=> CanExecuteChanged?.Invoke(this, EventArgs.Empty);

		bool IPpsIdleAction.OnIdle(int elapsed)
		{
			Refresh();
			return false;
		} // func IPpsIdleAction.OnIdle
	} // class PpsCommand
	
	#endregion

	#region -- class PpsCommandOrderConverter -------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsCommandOrderConverter : TypeConverter
	{
		public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
		{
			return sourceType == typeof(string);
		} // func CanConvertFrom

		public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
		{
			return destinationType == typeof(System.ComponentModel.Design.Serialization.InstanceDescriptor) || destinationType == typeof(string);
		} // func CanConvertTo

		public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
		{
			if (value == null)
				return PpsCommandOrder.Empty;
			else if (value is string)
			{
				var parts = ((string)value).Split(new char[] { ';', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
				if (parts.Length == 0)
					return PpsCommandOrder.Empty;
				else if (parts.Length == 2)
				{
					return new PpsCommandOrder(
						int.Parse(parts[0], culture),
						int.Parse(parts[1], culture)
					);
				}
				else
					throw GetConvertFromException(value);
			}
			else 
				throw GetConvertFromException(value);
		} // func ConvertFrom

		public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
		{
			var order = value as PpsCommandOrder;
			if (value != null && order == null)
				throw GetConvertToException(value, destinationType);

			if (destinationType == typeof(string))
			{
				if (order == null)
					return null;
				else
					return String.Format(culture, "{0}; {1}", order.Group, order.Order);
			}
			else if (destinationType == typeof(System.ComponentModel.Design.Serialization.InstanceDescriptor))
			{
				var ci = typeof(PpsCommandOrder).GetConstructor(new Type[] { typeof(int), typeof(int) });
				return new System.ComponentModel.Design.Serialization.InstanceDescriptor(ci,
					order == null ?
						new object[] { -1, -1 } :
						new object[] { order.Group, order.Order },
					true
				);
			}

			return base.ConvertTo(context, culture, value, destinationType);
		} // func ConvertTo
	} // class PpsCommandOrderConverter

	#endregion

	#region -- class PpsCommandOrder ----------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	[TypeConverter(typeof(PpsCommandOrderConverter))]
	public sealed class PpsCommandOrder : IEquatable<PpsCommandOrder>, IComparable<PpsCommandOrder>
	{
		private readonly int group;
		private readonly int order;

		public PpsCommandOrder(int group, int order)
		{
			this.group = group;
			this.order = order;
		} // ctor

		public override bool Equals(object obj)
		{
			var other = obj as PpsCommandOrder;
			if (other != null)
				return Equals(other);
			else
				return false;
		} // func Equals

		public override int GetHashCode() => group.GetHashCode() ^ order.GetHashCode();

		public override string ToString() => $"{group},{order}";

		public bool Equals(PpsCommandOrder other) => group == other.group && order == other.order;
		public int CompareTo(PpsCommandOrder other) => group == other.group ? order - other.order : group - other.group;

		public int Group { get { return group; } }
		public int Order { get { return order; } }

		public bool IsEmpty { get { return order == -1 && group == Int32.MaxValue; } }

		private static readonly PpsCommandOrder empty = new PpsCommandOrder(Int32.MaxValue, -1);

		// -- Static ----------------------------------------------------------------------

		public static PpsCommandOrder Empty { get { return empty; } }

		public static PpsCommandOrder Parse(string value)
		{
			PpsCommandOrder r;
			if (TryParse(value, out r))
				return r;
			else
				throw new FormatException();
		} // func Parse

		public static bool TryParse(string value, out PpsCommandOrder order)
		{
			return TryParse(value, CultureInfo.CurrentUICulture, out order);
		} // func TryParse

		public static bool TryParse(string value, CultureInfo culture, out PpsCommandOrder order)
		{
			if (value == null)
			{
				order = PpsCommandOrder.Empty;
				return true;
			}
			else
			{
				var parts = ((string)value).Split(new char[] { ';', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
				if (parts.Length == 0)
				{
					order = Empty;
					return true;
				}
				else if (parts.Length == 2)
				{
					order = new PpsCommandOrder(
						int.Parse(parts[0], culture),
						int.Parse(parts[1], culture)
					);
					return true;
				}
			}

			order = Empty;
			return false;
		} //func TryParse
	} // class PpsCommandOrder

	#endregion

	#region -- class PpsUICommand -------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Baseclass for a UI-Command implementation.</summary>
	public abstract class PpsUICommand : FrameworkContentElement
	{
		public static readonly DependencyProperty IsVisibleProperty = DependencyProperty.Register("IsVisible", typeof(bool), typeof(PpsUICommand));
		
		private PpsCommandOrder order;
		
		/// <summary>Position of the command.</summary>
		public PpsCommandOrder Order
		{
			get { return order ?? PpsCommandOrder.Empty; }
			set
			{
				order = value;
				if (ParentCollection != null)
				{
					ParentCollection.Remove(this);
					ParentCollection.Insert(0, this);
				}
			}
		} // prop Order

		/// <summary>Collection</summary>
		public PpsUICommandCollection ParentCollection { get; internal set; }

		/// <summary>Is the command currently visible.</summary>
		public bool IsVisible { get { return (bool)GetValue(IsVisibleProperty); } set { SetValue(IsVisibleProperty, value); } }
	} // class PpsUICommand

	#endregion

	#region -- class PpsUICommandButton -------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PpsUICommandButton : PpsUICommand, IUriContext
	{
		public static readonly DependencyProperty DisplayTextProperty = DependencyProperty.Register("DisplayText", typeof(string), typeof(PpsUICommandButton));
		public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register("Description", typeof(string), typeof(PpsUICommandButton));
		public static readonly DependencyProperty ImageProperty = DependencyProperty.Register("Image", typeof(string), typeof(PpsUICommandButton));

		//public static readonly DependencyProperty ImageProperty = DependencyProperty.Register("Image", typeof(object), typeof(PpsUICommandButton), new FrameworkPropertyMetadata(ImagePropertyChanged));
		//private static void ImagePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		//{
		//	var value = e.NewValue;

		//	var uri = value as Uri;
		//	if (uri == null && value is string)
		//		uri = new Uri((string)value, UriKind.RelativeOrAbsolute);

		//	if (uri != null && !uri.IsAbsoluteUri)
		//	{
		//		var uriContext = d as IUriContext;
		//		if (uriContext != null && uriContext.BaseUri != null)
		//			d.SetValue(e.Property, new Uri(uriContext.BaseUri, uri));
		//	}
		//} // proc ImagePropertyChanged

		public static readonly DependencyProperty CommandProperty = DependencyProperty.Register("Command", typeof(ICommand), typeof(PpsUICommandButton));

		private Uri baseUri;

		public string DisplayText { get { return (string)GetValue(DisplayTextProperty); } set { SetValue(DisplayTextProperty, value); } }
		public string Description { get { return (string)GetValue(DescriptionProperty); } set { SetValue(DescriptionProperty, value); } }
		public string Image { get { return (string)GetValue(ImageProperty); } set { SetValue(ImageProperty, value); } }
		//public object Image { get { return GetValue(ImageProperty); } set { SetValue(ImageProperty, value); } }
		public ICommand Command { get { return (ICommand)GetValue(CommandProperty); } set { SetValue(CommandProperty, value); } }
		public Uri BaseUri { get { return baseUri; } set { baseUri = value; } }
	} // class PpsUICommandButton

	#endregion

	#region -- class PpsUISplitCommandButton --------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PpsUISplitCommandButton : PpsUICommandButton
	{
		public SplitButtonType Mode { get; set; }
		public Popup Popup { get; set; }
	} // class PpsUISplitCommandButton

	#endregion

	#region -- class PpsUICommandCollection ---------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PpsUICommandCollection : Collection<PpsUICommand>, INotifyCollectionChanged
	{
		/// <summary></summary>
		public event NotifyCollectionChangedEventHandler CollectionChanged;

		public PpsUICommandButton AddButton(string order, string image, ICommand command, string displayText, string description)
		{
			var tmp = new PpsUICommandButton()
			{
				Order = PpsCommandOrder.Parse(order),
				Image = image,
				Command = command,
				DisplayText = displayText,
				Description = description
			};

			Add(tmp);
			return tmp;
		} // ctor

		/// <summary></summary>
		protected override void ClearItems()
		{
			// remove item by item
			while (Count > 0)
				RemoveAt(Count - 1);
		} // proc ClearItems
		
		/// <summary></summary>
		/// <param name="index"></param>
		protected override void RemoveItem(int index)
		{
			CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, this[index], index));
			base.RemoveItem(index);
			if (index == Count && index > 0 && this[index - 1] == null) // remove group before
					base.RemoveItem(index - 1);
			else if (index < Count && this[index] == null) // remove group after
				base.RemoveItem(index);
		} // proc RemoveItem

		private static bool IsDifferentGroup(PpsUICommand item, int group)
		{
			return item != null && item.Order.Group != group;
		} // func IsDifferentGroup

		/// <summary></summary>
		/// <param name="index"></param>
		/// <param name="item"></param>
		protected override void InsertItem(int index, PpsUICommand item)
		{
			if (item == null)
				return; // ignore null values

			item.ParentCollection = this; // update collection

			// find the correct position
			var group = item.Order.Group;
			var order = item.Order.Order;

			if (item.Order.IsEmpty && Count > 0)
				index = Count - 1; // add at the end
			else
				index = 0;

			for (; index < Count; index++)
			{
				if (this[index] != null)
				{
					var currentGroup = this[index].Order.Group;
					if (currentGroup == group) // first item in this group
					{
						for (; index < Count; index++)
						{
							var t = this[index];
							if (t == null || (t.Order.Group == currentGroup && t.Order.Order > order))
								break;
						}
						break;
					}
					else if (currentGroup > group) // a greater group, insert a new group
					{
						break;
					}
				}
			}

			// create a group before
			if (index > 0 && IsDifferentGroup(this[index - 1], group))
				base.InsertItem(index++, null);
			if (index < Count && IsDifferentGroup(this[index], group))
				base.InsertItem(index, null);

			// insert the item
			base.InsertItem(index, item);
			CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, this[index], index));
		} // proc InsertItem

		/// <summary></summary>
		/// <param name="index"></param>
		/// <param name="item"></param>
		protected override void SetItem(int index, PpsUICommand item)
		{
			RemoveAt(index);
			InsertItem(index, item);
		} // proc SetItem
	} // class PPsUICommandCollection

	#endregion
}
