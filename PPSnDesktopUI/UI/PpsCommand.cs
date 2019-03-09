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
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using TecWare.PPSn.Controls;

namespace TecWare.PPSn.UI
{
	#region -- class PpsCommandContext ------------------------------------------------

	/// <summary>Command context extension for the service model.</summary>
	public sealed class PpsCommandContext : IServiceProvider
	{
		private readonly PpsShellWpf shell;
		private readonly object target;
		private readonly object source;
		private readonly object parameter;

		private readonly Lazy<object> getDataContext;

		/// <summary>Command context extension for the service model.</summary>
		/// <param name="shell">Environment</param>
		/// <param name="target">Command target object</param>
		/// <param name="source">Command source object</param>
		/// <param name="parameter">Command parameter</param>
		public PpsCommandContext(PpsShellWpf shell, object target, object source, object parameter)
		{
			this.shell = shell ?? throw new ArgumentNullException(nameof(shell));
			this.target = target ?? throw new ArgumentNullException(nameof(target));
			this.source = source ?? throw new ArgumentNullException(nameof(source));
			this.getDataContext = new Lazy<object>(GetDataContext);
			this.parameter = parameter;
		} // ctor

		private object GetDataContext()
		{
			switch (target)
			{
				case FrameworkElement fe:
					return fe.DataContext;
				case FrameworkContentElement fce:
					return fce.DataContext;
				default:
					return null;
			}
		} // func GetDataContext
		
		/// <summary>GetService implementation</summary>
		/// <param name="serviceType"></param>
		/// <returns></returns>
		public object GetService(Type serviceType)
		{
			object r = null;

			// ask service
			if (target is IServiceProvider sp)
				r = sp.GetService(serviceType);

			// next ask controls
			if (r == null && target is DependencyObject dc1)
				r = StuffUI.GetControlService(dc1, serviceType, true);

			if (r == null && target != source && source is DependencyObject dc2)
				r = StuffUI.GetControlService(dc2, serviceType, false);

			return r ?? shell.GetService(serviceType);
		} // func GetService

		/// <summary>Shell</summary>
		public PpsShellWpf Shell => shell;
		/// <summary>Target control</summary>
		public object Target => target;
		/// <summary>Source control</summary>
		public object Source => source;
		/// <summary>Data context</summary>
		public object DataContext => getDataContext.Value;
		/// <summary>Command parameter</summary>
		public object Parameter => parameter;
	} // class PpsCommandContext

	#endregion

	#region -- class PpsCommandBase ---------------------------------------------------

	/// <summary>We define a routed command to get the ExecutedEvent,CanExecuteEvent in the root control.
	/// The result is we get the command source for free, the drawback is we need to catch the event in 
	/// the root and call the ExecuteCommand method.</summary>
	public abstract class PpsCommandBase : RoutedCommand
	{
		/// <summary>Attached property to mark, that the command is currently executed.</summary>
		public static readonly DependencyProperty AsyncCommandIsRunningProperty = DependencyProperty.RegisterAttached("AsyncCommandIsRunning", typeof(List<PpsCommandBase>), typeof(PpsCommandBase), new FrameworkPropertyMetadata(null));

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary></summary>
		protected PpsCommandBase()
			: base()
		{
		} // ctor

		/// <summary></summary>
		/// <param name="name"></param>
		/// <param name="ownerType"></param>
		protected PpsCommandBase(string name, Type ownerType)
			: base(name, ownerType)
		{
		} // ctor

		/// <summary></summary>
		/// <param name="name"></param>
		/// <param name="ownerType"></param>
		/// <param name="inputGestures"></param>
		protected PpsCommandBase(string name, Type ownerType, InputGestureCollection inputGestures)
			: base(name, ownerType, inputGestures)
		{
		} // ctor

		#endregion

		#region -- IsRunning ----------------------------------------------------------

		/// <summary>Get the IsRunning property value.</summary>
		/// <param name="element">UIElement the command is running on.</param>
		/// <returns></returns>
		public static List<PpsCommandBase> GetAsyncCommandIsRunning(UIElement element)
			=> (List<PpsCommandBase>)element.GetValue(AsyncCommandIsRunningProperty);

		/// <summary>Set the IsRunning property for a command execution.</summary>
		/// <param name="target"></param>
		/// <param name="isRunning"></param>
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

		/// <summary>Get the IsRunning property</summary>
		/// <param name="target"></param>
		/// <returns></returns>
		protected bool GetIsRunning(object target)
			=> target is UIElement element ? GetAsyncCommandIsRunning(element)?.Contains(this) ?? false : throw new ArgumentException("target must be an UIElement");

		#endregion

		/// <summary></summary>
		/// <param name="commandContext"></param>
		/// <returns></returns>
		public virtual bool CanExecuteCommand(PpsCommandContext commandContext) 
			=> true;

		/// <summary></summary>
		/// <param name="commandContext"></param>
		public abstract void ExecuteCommand(PpsCommandContext commandContext);

		/// <summary></summary>
		/// <param name="shell"></param>
		/// <param name="target"></param>
		/// <param name="command"></param>
		/// <returns></returns>
		public static CommandBinding CreateBinding(PpsShellWpf shell, object target, PpsCommandBase command)
		{
			return new CommandBinding(command,
				(sender, e) =>
				{
					command.ExecuteCommand(new PpsCommandContext(shell, target ?? e.OriginalSource, e.Source, e.Parameter));
					e.Handled = true;
				},
				(sender, e) =>
				{
					e.CanExecute = command.CanExecuteCommand(new PpsCommandContext(shell, target ?? e.OriginalSource, e.Source, e.Parameter));
					e.Handled = true;
				}
			);
		} // func CreateBinding

		/// <summary></summary>
		/// <param name="shell"></param>
		/// <param name="command"></param>
		/// <param name="commandImpl"></param>
		/// <returns></returns>
		public static CommandBinding CreateBinding(PpsShellWpf shell, RoutedCommand command, PpsCommandBase commandImpl)
		{
			return new CommandBinding(command,
				(sender, e) =>
				{
					commandImpl.ExecuteCommand(new PpsCommandContext(shell, e.OriginalSource, e.Source, e.Parameter));
					e.Handled = true;
				},
				(sender, e) =>
				{
					e.CanExecute = commandImpl.CanExecuteCommand(new PpsCommandContext(shell, e.OriginalSource, e.Source, e.Parameter));
					e.Handled = true;
				}
			);
		} // func CreateBinding
	} // class PpsCommandBase

	#endregion

	#region -- class PpsCommandImpl ---------------------------------------------------

	/// <summary>Implements CanExecute with an function and secure the ExecuteCommand
	/// with an exception handler.</summary>
	public abstract class PpsCommandImpl : PpsCommandBase
	{
		private readonly Func<PpsCommandContext, bool> canExecute;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary></summary>
		/// <param name="canExecute"></param>
		public PpsCommandImpl(Func<PpsCommandContext, bool> canExecute = null)
		{
			this.canExecute = canExecute;
		} // ctor

		/// <summary></summary>
		/// <param name="name"></param>
		/// <param name="ownerType"></param>
		/// <param name="canExecute"></param>
		public PpsCommandImpl(string name, Type ownerType, Func<PpsCommandContext, bool> canExecute = null)
			: base(name, ownerType)
		{
			this.canExecute = canExecute;
		} // ctor

		/// <summary></summary>
		/// <param name="name"></param>
		/// <param name="ownerType"></param>
		/// <param name="inputGestures"></param>
		/// <param name="canExecute"></param>
		public PpsCommandImpl(string name, Type ownerType, InputGestureCollection inputGestures, Func<PpsCommandContext, bool> canExecute = null)
			: base(name, ownerType, inputGestures)
		{
			this.canExecute = canExecute;
		} // ctor

		#endregion

		#region -- Command Member -----------------------------------------------------

		/// <summary></summary>
		/// <param name="commandContext"></param>
		/// <returns></returns>
		public override bool CanExecuteCommand(PpsCommandContext commandContext)
			=> canExecute?.Invoke(commandContext) ?? true;

		/// <summary></summary>
		/// <param name="commandContext"></param>
		public sealed override void ExecuteCommand(PpsCommandContext commandContext)
		{
			try
			{
				if (CanExecuteCommand(commandContext))
					ExecuteCommandCore(commandContext);
			}
			catch (Exception e)
			{
				commandContext.Shell.ShowException(ExceptionShowFlags.None, e);
			}
		} // proc Execute

		/// <summary></summary>
		/// <param name="commandContext"></param>
		protected abstract void ExecuteCommandCore(PpsCommandContext commandContext);

		#endregion
	} // class PpsCommandImpl

	#endregion

	#region -- class PpsCommand -------------------------------------------------------

	/// <summary>Implements a command that can call a delegate. This command
	/// can also be added to the idle collection.</summary>
	public sealed class PpsCommand : PpsCommandImpl
	{
		private readonly Action<PpsCommandContext> command;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary></summary>
		/// <param name="command"></param>
		/// <param name="canExecute"></param>
		public PpsCommand(Action<PpsCommandContext> command, Func<PpsCommandContext, bool> canExecute = null)
			: base(canExecute)
		{
			this.command = command;
		} // ctor

		/// <summary></summary>
		/// <param name="name"></param>
		/// <param name="ownerType"></param>
		/// <param name="command"></param>
		/// <param name="canExecute"></param>
		public PpsCommand(string name, Type ownerType, Action<PpsCommandContext> command, Func<PpsCommandContext, bool> canExecute = null)
			: base(name, ownerType, canExecute)
		{
			this.command = command;
		} // ctor

		/// <summary></summary>
		/// <param name="name"></param>
		/// <param name="ownerType"></param>
		/// <param name="inputGestures"></param>
		/// <param name="command"></param>
		/// <param name="canExecute"></param>
		public PpsCommand(string name, Type ownerType, InputGestureCollection inputGestures, Action<PpsCommandContext> command, Func<PpsCommandContext, bool> canExecute = null)
			: base(name, ownerType, inputGestures, canExecute)
		{
			this.command = command;
		} // ctor

		#endregion

		#region -- Command Member -----------------------------------------------------

		/// <summary></summary>
		/// <param name="commandContext"></param>
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

		/// <summary></summary>
		/// <param name="commandContext"></param>
		/// <returns></returns>
		public override bool CanExecuteCommand(PpsCommandContext commandContext) 
			=> !GetIsRunning(commandContext.Target) && base.CanExecuteCommand(commandContext);
	} // class PpsCommand

	#endregion

	#region -- class PpsAsyncCommand --------------------------------------------------

	/// <summary>Executions a Async command method.</summary>
	public sealed class PpsAsyncCommand : PpsCommandImpl
	{
		private readonly Func<PpsCommandContext, Task> command;

		#region -- Ctor/Dtor ----------------------------------------------------------
		
		/// <summary></summary>
		/// <param name="command"></param>
		/// <param name="canExecute"></param>
		public PpsAsyncCommand(Func<PpsCommandContext, Task> command, Func<PpsCommandContext, bool> canExecute = null)
			: base(canExecute)
		{
			this.command = command;
		} // ctor

		/// <summary></summary>
		/// <param name="name"></param>
		/// <param name="ownerType"></param>
		/// <param name="command"></param>
		/// <param name="canExecute"></param>
		public PpsAsyncCommand(string name, Type ownerType, Func<PpsCommandContext, Task> command, Func<PpsCommandContext, bool> canExecute) :
			base(name, ownerType, canExecute)
		{
			this.command = command;
		} // ctor

		/// <summary></summary>
		/// <param name="name"></param>
		/// <param name="ownerType"></param>
		/// <param name="inputGestures"></param>
		/// <param name="command"></param>
		/// <param name="canExecute"></param>
		public PpsAsyncCommand(string name, Type ownerType, InputGestureCollection inputGestures, Func<PpsCommandContext, Task> command, Func<PpsCommandContext, bool> canExecute)
			: base(name, ownerType, inputGestures, canExecute)
		{
			this.command = command;
		} // ctor

		#endregion

		#region -- Command Member -----------------------------------------------------

		/// <summary></summary>
		/// <param name="commandContext"></param>
		/// <returns></returns>
		public override bool CanExecuteCommand(PpsCommandContext commandContext)
			=> !GetIsRunning(commandContext.Target) && base.CanExecuteCommand(commandContext);

		/// <summary></summary>
		/// <param name="commandContext"></param>
		protected override void ExecuteCommandCore(PpsCommandContext commandContext)
		{
			SetIsRunning(commandContext.Target, true);
			try
			{
				var task = command(commandContext);
				if (task == null)
					SetIsRunning(commandContext.Target, false);
				else
					task.ContinueWith(t => SetIsRunning(commandContext.Target, false), TaskContinuationOptions.ExecuteSynchronously);
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

	/// <summary>Command order converter, to convert a string in the format {group}:{order} to 
	/// the PpsCommandOrder structure.</summary>
	public sealed class PpsCommandOrderConverter : TypeConverter
	{
		/// <summary>Only string is allowed.</summary>
		/// <param name="context"></param>
		/// <param name="sourceType"></param>
		/// <returns></returns>
		public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
			=> sourceType == typeof(string);
		
		/// <summary>Only string is allowed.</summary>
		/// <param name="context"></param>
		/// <param name="destinationType"></param>
		/// <returns></returns>
		public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
			=> destinationType == typeof(System.ComponentModel.Design.Serialization.InstanceDescriptor)
			|| destinationType == typeof(string);
		
		/// <summary>Converter implementation.</summary>
		/// <param name="context"></param>
		/// <param name="culture"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
		{
			switch (value)
			{
				case null:
					return PpsCommandOrder.Empty;
				case string s:
					if (PpsCommandOrder.TryParse(s, out var order))
						return order;
					else
						goto default;
				default:
					throw GetConvertFromException(value);
			}
		} // func ConvertFrom

		/// <summary></summary>
		/// <param name="context"></param>
		/// <param name="culture"></param>
		/// <param name="value"></param>
		/// <param name="destinationType"></param>
		/// <returns></returns>
		public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
		{
			if (value == null)
			{
				if (destinationType == typeof(string))
					return PpsCommandOrder.Empty;
				else if (destinationType == typeof(System.ComponentModel.Design.Serialization.InstanceDescriptor))
				{
					var ci = typeof(PpsCommandOrder).GetConstructor(new Type[] { typeof(int), typeof(int) });
					return new System.ComponentModel.Design.Serialization.InstanceDescriptor(ci, new object[] { PpsCommandOrder.Empty.Group, PpsCommandOrder.Empty.Order }, true);
				}
				else
					throw GetConvertToException(value, destinationType);
			}
			else if (value is PpsCommandOrder order)
			{
				if (destinationType == typeof(string))
					return order.ToString();
				else if (destinationType == typeof(System.ComponentModel.Design.Serialization.InstanceDescriptor))
				{
					var ci = typeof(PpsCommandOrder).GetConstructor(new Type[] { typeof(int), typeof(int) });
					return new System.ComponentModel.Design.Serialization.InstanceDescriptor(ci, new object[] { order.Group, order.Order }, true);
				}
				else
					throw GetConvertToException(value, destinationType);
			}
			else
				throw GetConvertToException(value, destinationType);
		} // func ConvertTo
	} // class PpsCommandOrderConverter

	#endregion

	#region -- class PpsCommandOrder --------------------------------------------------

	/// <summary>Command order structure</summary>
	[TypeConverter(typeof(PpsCommandOrderConverter))]
	public sealed class PpsCommandOrder : IEquatable<PpsCommandOrder>, IComparable<PpsCommandOrder>
	{
		/// <summary></summary>
		/// <param name="group"></param>
		/// <param name="order"></param>
		public PpsCommandOrder(int group, int order)
		{
			Group = group;
			Order = order;
		} // ctor

		/// <summary></summary>
		/// <param name="obj"></param>
		/// <returns></returns>
		public override bool Equals(object obj)
			=> obj is PpsCommandOrder other ? Equals(other) : false;

		/// <summary></summary>
		/// <returns></returns>
		public override int GetHashCode() 
			=> Group.GetHashCode() ^ Order.GetHashCode();

		/// <summary></summary>
		/// <returns></returns>
		public override string ToString() 
			=> $"{Group},{Order}";

		/// <summary></summary>
		/// <param name="other"></param>
		/// <returns></returns>
		public bool Equals(PpsCommandOrder other) 
			=> Group == other.Group && Order == other.Order;

		/// <summary></summary>
		/// <param name="other"></param>
		/// <returns></returns>
		public int CompareTo(PpsCommandOrder other) 
			=> Group == other.Group ? Order - other.Order : Group - other.Group;

		/// <summary>Group of commands</summary>
		public int Group { get; }
		/// <summary>Order of with the group.</summary>
		public int Order { get; }

		/// <summary>Is the command order empty.</summary>
		public bool IsEmpty => Equals(Empty);

		// -- Static ----------------------------------------------------------------------

		/// <summary>Empty command order</summary>
		public static PpsCommandOrder Empty { get; }= new PpsCommandOrder(Int32.MaxValue, -1);

		/// <summary>Parse command order from a string.</summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static PpsCommandOrder Parse(string value)
			=> TryParse(value, out var r) ? r : throw new FormatException();

		/// <summary>Parse command order from a string.</summary>
		/// <param name="value"></param>
		/// <param name="order"></param>
		/// <returns></returns>
		public static bool TryParse(string value, out PpsCommandOrder order)
			=> TryParse(value, CultureInfo.CurrentUICulture, out order);

		/// <summary>Parse command order from a string.</summary>
		/// <param name="value"></param>
		/// <param name="culture"></param>
		/// <param name="order"></param>
		/// <returns></returns>
		public static bool TryParse(string value, CultureInfo culture, out PpsCommandOrder order)
		{
			if (value == null)
			{
				order = Empty;
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

	/// <summary>Baseclass for a UI-Command implementation.</summary>
	public abstract class PpsUICommand : FrameworkContentElement
	{
		/// <summary>Is this ui-command visible</summary>
		public static readonly DependencyProperty IsVisibleProperty = DependencyProperty.Register(nameof(IsVisible), typeof(bool), typeof(PpsUICommand));

		private PpsCommandOrder order = PpsCommandOrder.Empty;

		/// <summary>Position of the command.</summary>
		public PpsCommandOrder Order
		{
			get => order ?? PpsCommandOrder.Empty;
			set
			{
				order = value;
				if (ParentCollection != null
					&& ParentCollection.Contains(this))
				{
					ParentCollection.Remove(this);
					ParentCollection.Insert(0, this);
				}
			}
		} // prop Order

		/// <summary>Collection</summary>
		public PpsUICommandCollection ParentCollection { get; internal set; }

		/// <summary>Is the command currently visible.</summary>
		public bool IsVisible { get => (bool)GetValue(IsVisibleProperty); set => SetValue(IsVisibleProperty, value); }
	} // class PpsUICommand

	#endregion

	#region -- class PpsUICommandButton -----------------------------------------------

	/// <summary>UI-Command button</summary>
	public class PpsUICommandButton : PpsUICommand, ICommandSource
	{
		/// <summary>Text to be shown on the Button</summary>
		public static readonly DependencyProperty DisplayTextProperty = DependencyProperty.Register(nameof(DisplayText), typeof(string), typeof(PpsUICommandButton));
		/// <summary>meaningful explanation of the Button, may be shown in ToolTip</summary>
		public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(nameof(Description), typeof(string), typeof(PpsUICommandButton));
		/// <summary>Name of the Image for the Button</summary>
		public static readonly DependencyProperty ImageProperty = DependencyProperty.Register(nameof(Image), typeof(string), typeof(PpsUICommandButton));
		/// <summary>The Command the Button schould execute</summary>
		public static readonly DependencyProperty CommandProperty = ButtonBase.CommandProperty.AddOwner(typeof(PpsUICommandButton));
		/// <summary>The Command the Button schould execute</summary>
		public static readonly DependencyProperty CommandParameterProperty = ButtonBase.CommandParameterProperty.AddOwner(typeof(PpsUICommandButton));
		/// <summary>The Command the Button schould execute</summary>
		public static readonly DependencyProperty CommandTargetProperty = ButtonBase.CommandTargetProperty.AddOwner(typeof(PpsUICommandButton));

		/// <summary>Text to be shown on the Button</summary>
		public string DisplayText { get => (string)GetValue(DisplayTextProperty); set => SetValue(DisplayTextProperty, value); }
		/// <summary>meaningful explanation of the Button, may be shown in ToolTip</summary>
		public string Description { get => (string)GetValue(DescriptionProperty);  set => SetValue(DescriptionProperty, value); }
		/// <summary>Name of the Image for the Button</summary>
		public string Image { get => (string)GetValue(ImageProperty);  set => SetValue(ImageProperty, value); }
		/// <summary>The Command the Button schould execute</summary>
		public ICommand Command { get => (ICommand)GetValue(CommandProperty); set => SetValue(CommandProperty, value); }
		/// <summary></summary>
		public object CommandParameter { get => GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }
		/// <summary></summary>
		public IInputElement CommandTarget { get => (IInputElement)GetValue(CommandTargetProperty); set => SetValue(CommandTargetProperty, value); }
	} // class PpsUICommandButton

	#endregion

	#region -- class PpsUISplitCommandButton ------------------------------------------

	/// <summary>UI-Command split button</summary>
	public class PpsUISplitCommandButton : PpsUICommandButton
	{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public static readonly DependencyProperty PopupProperty = PpsSplitButton.PopupProperty.AddOwner(typeof(PpsUISplitCommandButton), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnPopupChanged)));
		public static readonly DependencyProperty ModeProperty = PpsSplitButton.ModeProperty.AddOwner(typeof(PpsUISplitCommandButton));
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		private static void OnPopupChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsUISplitCommandButton)d).OnPopupChanged((Popup)e.NewValue, (Popup)e.OldValue);

		private void OnPopupChanged(Popup newValue, Popup oldValue)
		{
			if (oldValue != null)
				oldValue.DataContext = DependencyProperty.UnsetValue;
			RemoveLogicalChild(oldValue);

			AddLogicalChild(newValue);
			if (newValue != null)
				newValue.DataContext = DataContext;
		} // proc OnPopupChanged

		/// <summary></summary>
		/// <param name="e"></param>
		protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
		{
			if (e.Property == DataContextProperty && Popup != null)
				Popup.DataContext = e.NewValue; // we do not want to bind the DataContext to the PlacementTarget
			base.OnPropertyChanged(e);
		} // proc OnPropertyChanged

		/// <summary></summary>
		protected override IEnumerator LogicalChildren
			=> LogicalContentEnumerator.GetLogicalEnumerator(this, base.LogicalChildren, () => Popup);

		/// <summary>Split button type</summary>
		public PpsSplitButtonType Mode { get; set; }
		/// <summary>Popup of the split button</summary>
		public Popup Popup { get => (Popup)GetValue(PopupProperty); set => SetValue(PopupProperty, value); }
	} // class PpsUISplitCommandButton

	#endregion

	#region -- class PpsUICommandCollection -------------------------------------------

	/// <summary></summary>
	/// <param name="child"></param>
	public delegate void PpsUICommandLogicalChildDelegate(object child);

	/// <summary>Command collection to hold a list of commands.</summary>
	public class PpsUICommandCollection : Collection<PpsUICommand>, INotifyCollectionChanged
	{
		private static readonly PpsUICommand[] seperator = new PpsUICommand[] { null };

		/// <summary>Called if the command is changed.</summary>
		public event NotifyCollectionChangedEventHandler CollectionChanged;

		/// <summary></summary>
		public PpsUICommandLogicalChildDelegate AddLogicalChildHandler;
		/// <summary></summary>
		public PpsUICommandLogicalChildDelegate RemoveLogicalChildHandler;
		/// <summary></summary>
		public IInputElement DefaultCommandTarget;
		
		/// <summary>Add a simple command button to the collection.</summary>
		/// <param name="order"></param>
		/// <param name="image"></param>
		/// <param name="command"></param>
		/// <param name="displayText"></param>
		/// <param name="description"></param>
		/// <param name="commandParameter"></param>
		/// <returns></returns>
		public PpsUICommandButton AddButton(string order, string image, ICommand command, string displayText, string description, object commandParameter = null)
		{
			var tmp = new PpsUICommandButton()
			{
				Order = PpsCommandOrder.Parse(order),
				Image = image,
				Command = command,
				CommandParameter = commandParameter,
				CommandTarget = DefaultCommandTarget,
				DisplayText = displayText,
				Description = description
			};

			Add(tmp);
			return tmp;
		} // ctor

		/// <summary>Clear all command buttons</summary>
		protected override void ClearItems()
		{
			// remove item by item
			while (Count > 0)
				RemoveAt(Count - 1);

			// refresh the property ItemsControl.HasItems
			CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
		} // proc ClearItems

		/// <summary></summary>
		/// <param name="index"></param>
		protected override void RemoveItem(int index)
		{
			var item = this[index];

			base.RemoveItem(index);

			RemoveLogicalChildHandler?.Invoke(item);
			
			if (index == Count && index > 0 && this[index - 1] == null) // remove group before
				base.RemoveItem(index - 1);
			else if (index < Count && this[index] == null) // remove group after
				base.RemoveItem(index);
			
			// force rebuild, templates will not show up correctly
			CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
		} // proc RemoveItem

		private static bool IsDifferentGroup(PpsUICommand item, int group)
			=> item != null && item.Order.Group != group;

		/// <summary>Insert a ui-command at the position (force the position by Order).</summary>
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
			AddLogicalChildHandler?.Invoke(this[index]);

			// force rebuild, templates will not show up correctly
			CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
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
