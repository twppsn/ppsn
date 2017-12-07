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
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using TecWare.PPSn.Controls;
using TecWare.PPSn.Data;

namespace TecWare.PPSn.UI
{
	#region -- class PpsCommandContext --------------------------------------------------

	public sealed class PpsCommandContext : IServiceProvider
	{
		private readonly PpsEnvironment environment;
		private readonly object target;
		private readonly object source;
		private readonly object parameter;

		private readonly Lazy<object> getDataContext;

		public PpsCommandContext(PpsEnvironment environment, object target, object source, object parameter)
		{
			this.environment = environment ?? throw new ArgumentNullException(nameof(environment));
			this.target = target ?? throw new ArgumentNullException(nameof(target));
			this.source = source ?? throw new ArgumentNullException(nameof(source));
			this.getDataContext = new Lazy<object>(GetDataContext);
			this.parameter = parameter;
		} // ctor

		private object GetDataContext()
			=> target is FrameworkElement frameworkElement ? frameworkElement.DataContext : null;
		
		public object GetService(Type serviceType)
		{
			object r = null;

			// ask service
			if (target is IServiceProvider sp)
				r = sp.GetService(serviceType);

			// next ask controls
			if (r == null && target is DependencyObject dc1)
				r = StuffUI.GetControlService(dc1, serviceType);

			if (r == null && target != source && source is DependencyObject dc2)
				r = StuffUI.GetControlService(dc2, serviceType);

			return r ?? environment.GetService(serviceType);
		} // func GetService

		public PpsEnvironment Environment => environment;
		public object Target => target;
		public object Source => source;
		public object DataContext => getDataContext.Value;
		public object Parameter => parameter;
	} // class PpsCommandContext

	#endregion

	#region -- class PpsCommandBase -----------------------------------------------------

	/// <summary>We define a routed command to get the ExecutedEvent,CanExecuteEvent in the root control. The result is we get the command source for free, the drawback is we need to catch the event in the root and call the ExecuteCommand method.</summary>
	public abstract class PpsCommandBase : RoutedCommand
	{
		public static readonly DependencyProperty AsyncCommandIsRunningProperty = DependencyProperty.RegisterAttached("AsyncCommandIsRunning", typeof(List<PpsCommandBase>), typeof(PpsCommandBase), new FrameworkPropertyMetadata(null));

		protected PpsCommandBase()
			: base()
		{
		} // ctor

		protected PpsCommandBase(string name, Type ownerType)
			: base(name, ownerType)
		{
		} // ctor

		protected PpsCommandBase(string name, Type ownerType, InputGestureCollection inputGestures)
			: base(name, ownerType, inputGestures)
		{
		} // ctor

		public static List<PpsCommandBase> GetAsyncCommandIsRunning(UIElement element)
			=> (List<PpsCommandBase>)element.GetValue(AsyncCommandIsRunningProperty);

		protected void SetIsRunning(object target, bool isRunning)
		{
			if (target is UIElement element)
			{
				var runningCommandList = GetAsyncCommandIsRunning(element);
				if (isRunning)
				{
					if (runningCommandList != null
						&& runningCommandList.Contains(this))
						throw new ArgumentException("Command is currently running.");

					// set command list
					if (runningCommandList == null)
					{
						runningCommandList = new List<PpsCommandBase>();
						element.SetValue(AsyncCommandIsRunningProperty, runningCommandList);
					}

					runningCommandList.Add(this);
				}
				else if (runningCommandList != null)
					runningCommandList.Remove(this);
			}
			else
				throw new ArgumentException("target must be an UIElement");
		} // proc SetRunningState

		protected bool GetIsRunning(object target)
			=> target is UIElement element ? GetAsyncCommandIsRunning(element)?.Contains(this) ?? false : throw new ArgumentException("target must be an UIElement");

		public virtual bool CanExecuteCommand(PpsCommandContext commandContext) 
			=> true;

		public abstract void ExecuteCommand(PpsCommandContext commandContext);
	} // class PpsCommandBase

	#endregion

	#region -- class PpsCommandImpl ---------------------------------------------------

	/// <summary></summary>
	public abstract class PpsCommandImpl : PpsCommandBase
	{
		private readonly Func<PpsCommandContext, bool> canExecute;

		#region -- Ctor/Dtor ----------------------------------------------------------

		public PpsCommandImpl(Func<PpsCommandContext, bool> canExecute = null)
		{
			this.canExecute = canExecute;
		} // ctor

		public PpsCommandImpl(string name, Type ownerType, Func<PpsCommandContext, bool> canExecute = null)
			: base(name, ownerType)
		{
			this.canExecute = canExecute;
		} // ctor

		public PpsCommandImpl(string name, Type ownerType, InputGestureCollection inputGestures, Func<PpsCommandContext, bool> canExecute = null)
			: base(name, ownerType, inputGestures)
		{
			this.canExecute = canExecute;
		} // ctor

		#endregion

		#region -- Command Member -------------------------------------------------------

		public override bool CanExecuteCommand(PpsCommandContext commandContext)
			=> canExecute?.Invoke(commandContext) ?? true;

		public sealed override void ExecuteCommand(PpsCommandContext commandContext)
		{
			try
			{
				if (CanExecuteCommand(commandContext))
					ExecuteCommandCore(commandContext);
			}
			catch (PpsDataTableForeignKeyRestriction)
			{
				// todo: in lua verlagern
				commandContext.Environment.MsgBox("Auf diesen Datensatz wird noch verwiesen.\nLöschen nicht möglich.", MessageBoxButton.OK, MessageBoxImage.Information);
			}
			catch (Exception e)
			{
				commandContext.Environment.ShowException(ExceptionShowFlags.None, e);
			}
		} // proc Execute

		protected abstract void ExecuteCommandCore(PpsCommandContext commandContext);

		#endregion
	} // class PpsCommandImpl

	#endregion

	#region -- class PpsCommand -------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Implements a command that can call a delegate. This command
	/// can also be added to the idle collection.</summary>
	public sealed class PpsCommand : PpsCommandImpl
	{
		private readonly Action<PpsCommandContext> command;

		#region -- Ctor/Dtor ----------------------------------------------------------

		public PpsCommand(Action<PpsCommandContext> command, Func<PpsCommandContext, bool> canExecute = null)
			: base(canExecute)
		{
			this.command = command;
		} // ctor

		public PpsCommand(string name, Type ownerType, Action<PpsCommandContext> command, Func<PpsCommandContext, bool> canExecute = null)
			: base(name, ownerType, canExecute)
		{
			this.command = command;
		} // ctor

		public PpsCommand(string name, Type ownerType, InputGestureCollection inputGestures, Action<PpsCommandContext> command, Func<PpsCommandContext, bool> canExecute = null)
			: base(name, ownerType, inputGestures, canExecute)
		{
			this.command = command;
		} // ctor

		#endregion

		#region -- Command Member -----------------------------------------------------

		protected override void ExecuteCommandCore(PpsCommandContext commandContext)
		{
			SetIsRunning(commandContext.Target, true);
			try
			{
				command(commandContext);
			}
			finally
			{
				SetIsRunning(commandContext.Target, false);
			}
		} // proc ExecuteCommandCore

		#endregion

		public override bool CanExecuteCommand(PpsCommandContext commandContext) 
			=> !GetIsRunning(commandContext.Target) && base.CanExecuteCommand(commandContext);
	} // class PpsCommand

	#endregion

	#region -- class PpsAsyncCommand --------------------------------------------------

	public sealed class PpsAsyncCommand : PpsCommandImpl
	{

		private readonly Func<PpsCommandContext, Task> command;

		#region -- Ctor/Dtor ----------------------------------------------------------
		
		public PpsAsyncCommand(Func<PpsCommandContext, Task> command, Func<PpsCommandContext, bool> canExecute)
			: base(canExecute)
		{
			this.command = command;
		} // ctor

		public PpsAsyncCommand(string name, Type ownerType, Func<PpsCommandContext, Task> command, Func<PpsCommandContext, bool> canExecute) :
			base(name, ownerType, canExecute)
		{
			this.command = command;
		} // ctor

		public PpsAsyncCommand(string name, Type ownerType, InputGestureCollection inputGestures, Func<PpsCommandContext, Task> command, Func<PpsCommandContext, bool> canExecute)
			: base(name, ownerType, inputGestures, canExecute)
		{
			this.command = command;
		} // ctor

		#endregion

		#region -- Command Member -------------------------------------------------------

		public override bool CanExecuteCommand(PpsCommandContext commandContext)
			=> !GetIsRunning(commandContext.Target)
				&& base.CanExecuteCommand(commandContext);

		protected override void ExecuteCommandCore(PpsCommandContext commandContext)
		{
			SetIsRunning(commandContext.Target, true);
			try
			{
				command(commandContext)
					.ContinueWith(t => SetIsRunning(commandContext.Target, false), TaskContinuationOptions.ExecuteSynchronously);
			}
			catch
			{
				SetIsRunning(commandContext.Target, false);
				throw;
			}
		} // func ExecuteCommandCore

		#endregion
	} // class PpsAsyncCommand

	#endregion

	#region -- class PpsCommandOrderConverter -----------------------------------------

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

	#region -- class PpsCommandOrder --------------------------------------------------

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
			=> obj is PpsCommandOrder other ? Equals(other) : false;

		public override int GetHashCode() 
			=> group.GetHashCode() ^ order.GetHashCode();

		public override string ToString() 
			=> $"{group},{order}";

		public bool Equals(PpsCommandOrder other) => group == other.group && order == other.order;
		public int CompareTo(PpsCommandOrder other) => group == other.group ? order - other.order : group - other.group;

		public int Group { get { return group; } }
		public int Order { get { return order; } }

		public bool IsEmpty { get { return order == -1 && group == Int32.MaxValue; } }

		private static readonly PpsCommandOrder empty = new PpsCommandOrder(Int32.MaxValue, -1);

		// -- Static ----------------------------------------------------------------------

		public static PpsCommandOrder Empty { get { return empty; } }

		public static PpsCommandOrder Parse(string value)
			=> TryParse(value, out var r) ? r : throw new FormatException();

		public static bool TryParse(string value, out PpsCommandOrder order)
			=> TryParse(value, CultureInfo.CurrentUICulture, out order);

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
				else if (parts.Length == 2 
					&& Int32.TryParse(parts[0], NumberStyles.Integer, culture, out var groupPart) 
					&& Int32.TryParse(parts[1], NumberStyles.Integer, culture, out var orderPart))
				{
					order = new PpsCommandOrder(groupPart, orderPart);
					return true;
				}
			}

			order = Empty;
			return false;
		} //func TryParse
	} // class PpsCommandOrder

	#endregion

	#region -- class PpsUICommand -----------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Baseclass for a UI-Command implementation.</summary>
	public abstract class PpsUICommand : FrameworkContentElement
	{
		public static readonly DependencyProperty IsVisibleProperty = DependencyProperty.Register(nameof(IsVisible), typeof(bool), typeof(PpsUICommand));
		
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

	#region -- class PpsUICommandButton -----------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PpsUICommandButton : PpsUICommand, IUriContext
	{
		public static readonly DependencyProperty DisplayTextProperty = DependencyProperty.Register(nameof(DisplayText), typeof(string), typeof(PpsUICommandButton));
		public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(nameof(Description), typeof(string), typeof(PpsUICommandButton));
		public static readonly DependencyProperty ImageProperty = DependencyProperty.Register(nameof(Image), typeof(string), typeof(PpsUICommandButton));

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

		public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(nameof(Command), typeof(ICommand), typeof(PpsUICommandButton), new FrameworkPropertyMetadata(null));

		private Uri baseUri;

		public string DisplayText { get { return (string)GetValue(DisplayTextProperty); } set { SetValue(DisplayTextProperty, value); } }
		public string Description { get { return (string)GetValue(DescriptionProperty); } set { SetValue(DescriptionProperty, value); } }
		public string Image { get { return (string)GetValue(ImageProperty); } set { SetValue(ImageProperty, value); } }
		//public object Image { get { return GetValue(ImageProperty); } set { SetValue(ImageProperty, value); } }
		public ICommand Command { get { return (ICommand)GetValue(CommandProperty); } set { SetValue(CommandProperty, value); } }
		public Uri BaseUri { get { return baseUri; } set { baseUri = value; } }
	} // class PpsUICommandButton

	#endregion

	#region -- class PpsUISplitCommandButton ------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PpsUISplitCommandButton : PpsUICommandButton
	{
		public SplitButtonType Mode { get; set; }
		public Popup Popup { get; set; }
	} // class PpsUISplitCommandButton

	#endregion

	#region -- class PpsUICommandCollection -------------------------------------------

	/// <summary></summary>
	public class PpsUICommandCollection : Collection<PpsUICommand>, INotifyCollectionChanged
	{
		private static PpsUICommand[] seperator = new PpsUICommand[] { null };

		/// <summary>Called if the command is changed.</summary>
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
			{
				CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, seperator, index - 1));
				base.RemoveItem(index - 1);
			}
			else if (index < Count && this[index] == null) // remove group after
			{
				CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, seperator, index));
				base.RemoveItem(index);
			}
		} // proc RemoveItem

		private static bool IsDifferentGroup(PpsUICommand item, int group)
			=> item != null && item.Order.Group != group;

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
			{
				base.InsertItem(index++, null);
				CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, seperator, index - 1));
			}
			if (index < Count && IsDifferentGroup(this[index], group))
			{
				base.InsertItem(index, null);
				CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, seperator, index));
			}

			// insert the item
			base.InsertItem(index, item);
			CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, this[index], index));
			CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset)); // force rebuild, templates will not show up correctly
		} // proc InsertItem

		/// <summary></summary>
		/// <param name="index"></param>
		/// <param name="item"></param>
		protected override void SetItem(int index, PpsUICommand item)
		{
			RemoveItem(index);
			InsertItem(index, item);
		} // proc SetItem
	} // class PpsUICommandCollection

	#endregion
}
