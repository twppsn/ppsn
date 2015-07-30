using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;
using System.Xml.Linq;
using Neo.IronLua;
using System.Windows.Controls;
using System.Diagnostics;
using System.Windows.Input;
using System.ComponentModel;
using System.ComponentModel.Design.Serialization;
using System.Globalization;

namespace TecWare.PPSn.UI
{
	#region -- class PpsGenericWpfWindowPane --------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Pane that combines a xaml file with lua code.</summary>
	public class PpsGenericWpfWindowPane : LuaTable, IPpsWindowPane
	{
		private static readonly XName xnCode = XName.Get("Code", "http://schemas.microsoft.com/winfx/2006/xaml");

		private string sXamlFile;
		private FrameworkElement control;

		private PpsEnvironment environment;
		private LuaTable arguments; // arguments

		#region -- Ctor/Dtor --------------------------------------------------------------

		public PpsGenericWpfWindowPane(PpsEnvironment environment)
		{
			this.environment = environment;
		} // ctor

		~PpsGenericWpfWindowPane()
		{
			Dispose(false);
		} // ctor

		public void Dispose()
		{
			GC.SuppressFinalize(this);
			Dispose(true);
		} // proc Dispose

		protected virtual void Dispose(bool lDisposing)
		{
		} // proc Dispose

		#endregion

		#region -- Lua-Interface ----------------------------------------------------------

		[LuaMember("print")]
		private void LuaPrint(string text)
		{
			Trace.TraceInformation(text);
		} // proc LuaPrint

		[LuaMember("msgbox")]
		private void LuaMsgBox(string text, string caption)
		{
			MessageBox.Show(text, caption ?? "Information", MessageBoxButton.OK, MessageBoxImage.Information);
		} // proc LuaMsgBox

		[LuaMember("require")]
		private void LuaRequire(string fileName)
		{
			Lua.CompileChunk(Path.Combine(BaseUri, fileName), null).Run(this);
		} // proc LuaRequire

		[LuaMember("command")]
		private object LuaCommand(Action<object> command, Func<object, bool> canExecute = null, bool idleCall = true)
		{
			var cmd = new PpsIdleCommand(command, canExecute);
			if (idleCall)
				Environment.AddIdleAction(cmd);
			return cmd;
		} // func LuaCommand

		#endregion

		#region -- Load/Unload ------------------------------------------------------------

		public virtual async Task LoadAsync(LuaTable arguments)
		{
			await Task.Yield();

			// use the argument as base table
			this.arguments = arguments;

			// get the basic template
			sXamlFile = arguments["template"].ToString();
			if (String.IsNullOrEmpty(sXamlFile))
				throw new ArgumentException("template is missing."); // todo: exception

			var xaml = XDocument.Load(sXamlFile, LoadOptions.SetBaseUri | LoadOptions.SetLineInfo); // todo: load via env

			// Load the content of the code-tag, to initialize extend functionality
			var xCode = xaml.Root.Element(xnCode);
			var chunk = (LuaChunk)null;
			if (xCode != null)
			{
				chunk = Lua.CompileChunk(xCode.Value, Path.GetFileName(sXamlFile), null); // todo: compile via env
				xCode.Remove();
			}

			// Create the Wpf-Control
			var xamlReader = new XamlReader();
			await Dispatcher.InvokeAsync(() =>
				{
					control = xamlReader.LoadAsync(xaml.CreateReader()) as FrameworkElement;
					OnControlCreated();

					// Initialize the control and run the code in UI-Thread
					if (chunk != null)
						chunk.Run(this);// todo: run via env

					// init bindings
					control.DataContext = this;
					
					// notify if the title will be changed
					if (control is PpsGenericWpfControl)
					{
						var desc = DependencyPropertyDescriptor.FromProperty(PpsGenericWpfControl.TitleProperty, typeof(PpsGenericWpfControl));
						desc.AddValueChanged(control, (sender, e) => OnPropertyChanged("Title"));
					}

					// notify changes on control
					OnPropertyChanged("Control");
					OnPropertyChanged("Commands");
					OnPropertyChanged("Title");
				});
		} // proc LoadAsync

		protected virtual void OnControlCreated()
		{
		} // proc OnControlCreated

		public Task<bool> UnloadAsync()
		{
			return Task.FromResult<bool>(true);
		} // func UnloadAsync

		#endregion

		/// <summary>Access to the current environemnt.</summary>
		[LuaMember("Environment")]
		public PpsEnvironment Environment { get { return environment; } }
		/// <summary>Arguments of the generic content.</summary>
		[LuaMember("Arguments")]
		public LuaTable Arguments { get { return arguments; } }

		/// <summary>Title of the pane</summary>
		public string Title
		{
			get
			{
				if (control == null)
					return String.Empty;

				return (string)control.GetValue(PpsGenericWpfControl.TitleProperty);
			}
		} // prop Title
		/// <summary>Wpf-Control</summary>
		[LuaMember("Control")]
		public FrameworkElement Control { get { return control; } }
		/// <summary>This member is resolved dynamic, that is the reason the FrameworkElement Control is public.</summary>
		object IPpsWindowPane.Control { get { return control; } }

		/// <summary>Source of the Wpf-Control</summary>
		protected string XamlFileName { get { return sXamlFile; } }
		/// <summary>BaseUri of the Wpf-Control</summary>
		public string BaseUri { get { return Path.GetDirectoryName(sXamlFile); } }

		/// <summary>Synchronization to the UI.</summary>
		public Dispatcher Dispatcher { get { return control == null ? Application.Current.Dispatcher : control.Dispatcher; } }
		/// <summary>Access to the current lua compiler</summary>
		public Lua Lua { get { return environment.Lua; } }

		public IEnumerable<UIElement> Commands { get { return control == null ? null : ((PpsGenericWpfControl)control).Commands; } }
	} // class PpsGenericWpfWindowContext

	#endregion

	#region -- class PpsGenericCommandOrderConverter ------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsGenericCommandOrderConverter : TypeConverter
	{
		public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
		{
			return sourceType == typeof(string);
		} // func CanConvertFrom

		public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
		{
			return destinationType == typeof(InstanceDescriptor) || destinationType == typeof(string);
		} // func CanConvertTo

		public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
		{
			if (value != null && value is string)
			{
				var parts = ((string)value).Split(new char[] { ';', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
				if (parts.Length == 0)
					return PpsGenericCommandOrder.Empty;
				else if (parts.Length == 2)
				{
					return new PpsGenericCommandOrder(
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
			var order = value as PpsGenericCommandOrder;
			if (value != null && order == null)
				throw GetConvertToException(value, destinationType);

			if (destinationType == typeof(string))
			{
				if (order == null)
					return null;
				else
					return String.Format(culture, "{0}; {1}", order.Group, order.Order);
			}
			else if (destinationType == typeof(InstanceDescriptor))
			{
				var ci = typeof(PpsGenericCommandOrder).GetConstructor(new Type[] { typeof(int), typeof(int) });
				return new InstanceDescriptor(ci,
					order == null ?
						new object[] { -1, -1 } :
						new object[] { order.Group, order.Order },
					true
				);
			}

			return base.ConvertTo(context, culture, value, destinationType);
		} // func ConvertTo
	} // class PpsGenericCommandOrderConverter

	#endregion

	#region -- class PpsGenericCommandOrder ---------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	[TypeConverter(typeof(PpsGenericCommandOrderConverter))]
	public sealed class PpsGenericCommandOrder
	{
		private readonly int group;
		private readonly int order;

		public PpsGenericCommandOrder(int group, int order)
		{
			this.group = group;
			this.order = order;
		} // ctor

		public int Group { get { return group; } }
		public int Order { get { return order; } }

		public bool IsEmpty {get{return order < 0 && group < 0;}}

		private static readonly PpsGenericCommandOrder empty = new PpsGenericCommandOrder(-1,-1);

		public static PpsGenericCommandOrder Empty { get { return empty; } }
	} // class PpsGenericCommandOrder

	#endregion

	#region -- class PpsGenericWpfControl -----------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Base control for the wpf generic pane.</summary>
	public class PpsGenericWpfControl : ContentControl, ILuaEventSink
	{
		public static readonly DependencyProperty TitleProperty = DependencyProperty.Register("Title", typeof(string), typeof(PpsGenericWpfControl), new UIPropertyMetadata(String.Empty));
		public static readonly DependencyProperty OrderProperty = DependencyProperty.RegisterAttached("Order", typeof(PpsGenericCommandOrder), typeof(PpsGenericWpfControl), new FrameworkPropertyMetadata(PpsGenericCommandOrder.Empty));

		private List<UIElement> commands = new List<UIElement>();

		#region -- ILuaEventSink Member ---------------------------------------------------

		void ILuaEventSink.CallMethod(string methodName, object[] args)
		{
			Pane.CallMember(methodName, args);
		} // proc ILuaEventSink.CallMethod

		#endregion

		public void AppendButton(int group, int order, UIElement element)
		{
		} // proc AppendButton

		/// <summary>Access to the owning pane.</summary>
		public PpsGenericWpfWindowPane Pane { get { return (PpsGenericWpfWindowPane)DataContext; } }
		/// <summary>Title of the window pane</summary>
		public string Title { get { return (string)this.GetValue(TitleProperty); } set { SetValue(TitleProperty, value); } }
		/// <summary>List of commands for the main toolbar.</summary>
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
		public List<UIElement> Commands { get { return commands; } }

		// -- Static --------------------------------------------------------------

		public static void SetOrder(UIElement element, PpsGenericCommandOrder order)
		{
			element.SetValue(OrderProperty, order);
		} // proc SetOrder

		public static PpsGenericCommandOrder GetOrder(UIElement element)
		{
			return (PpsGenericCommandOrder)element.GetValue(OrderProperty);
		} // func GetOrder
	} // class PpsGenericWpfWindowPane

	#endregion
}
