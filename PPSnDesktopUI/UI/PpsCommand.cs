using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

namespace TecWare.PPSn.UI
{
	#region -- class PpsCommand ---------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PpsCommand : ICommand, IPpsIdleAction
	{
		private Action<object> command;
		private Func<object, bool> canExecute;

		public PpsCommand(Action<object> command, Func<object, bool> canExecute)
		{
			this.command = command;
			this.canExecute = canExecute;
		} // ctor

		#region -- ICommand Member ---------------------------------------------------------

		public event EventHandler CanExecuteChanged;

		public virtual bool CanExecute(object parameter)
		{
			return canExecute == null || canExecute(parameter);
		} // func CanExecute

		public virtual void Execute(object parameter)
		{
			command(parameter);
		} // proc Execute

		#endregion

		public void Refresh()
		{
			if (CanExecuteChanged != null)
				CanExecuteChanged(this, EventArgs.Empty);
		} // proc Refresh

		void IPpsIdleAction.OnIdle() { Refresh(); }
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
			if (value != null && value is string)
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
	public sealed class PpsCommandOrder
	{
		private readonly int group;
		private readonly int order;

		public PpsCommandOrder(int group, int order)
		{
			this.group = group;
			this.order = order;
		} // ctor

		public int Group { get { return group; } }
		public int Order { get { return order; } }

		public bool IsEmpty { get { return order < 0 && group < 0; } }

		private static readonly PpsCommandOrder empty = new PpsCommandOrder(-1, -1);

		public static PpsCommandOrder Empty { get { return empty; } }
	} // class PpsCommandOrder

	#endregion

	#region -- class PpsUICommand -------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public abstract class PpsUICommand : FrameworkContentElement
	{
		public static readonly DependencyProperty IsVisibleProperty = DependencyProperty.Register("IsVisible", typeof(bool), typeof(PpsUICommandButton));

		private PpsCommandOrder order;

		/// <summary>Position of the command.</summary>
		public PpsCommandOrder Order { get { return order ?? PpsCommandOrder.Empty; } set { order = value; } }
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

	#region -- class PPsUICommandCollection ---------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PPsUICommandCollection : Collection<PpsUICommand>
	{
		protected override void ClearItems()
		{
			base.ClearItems();
		}

		protected override void RemoveItem(int index)
		{
			base.RemoveItem(index);
		}

		protected override void InsertItem(int index, PpsUICommand item)
		{
			base.InsertItem(index, item);
		}

		protected override void SetItem(int index, PpsUICommand item)
		{
			base.SetItem(index, item);
		}
	} //class PPsUICommandCollection

	#endregion
}
