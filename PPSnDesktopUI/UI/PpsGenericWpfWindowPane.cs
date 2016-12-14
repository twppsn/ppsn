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
#define _NOTIFY_BINDING_SOURCE_UPDATE
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Threading;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Networking;

namespace TecWare.PPSn.UI
{
	#region -- class WpfPaneHelper ------------------------------------------------------

	internal static class WpfPaneHelper
	{
		public static object GetXamlElement(FrameworkElement control, object key)
			=> key is string && control != null && control.Dispatcher.CheckAccess() ? control.FindName((string)key) : null;
	} // class WpfPaneHelper

	#endregion

	#region -- class PpsGenericWpfChildPane ---------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PpsGenericWpfChildPane : LuaEnvironmentTable, IPpsLuaRequest
	{
		private readonly FrameworkElement control;
		private readonly BaseWebRequest fileSource;

		public PpsGenericWpfChildPane(PpsGenericWpfWindowPane parentPane, XDocument xXaml, LuaChunk code)
			:base(parentPane)
		{
			if (parentPane == null)
				throw new ArgumentNullException("parentPane");

			this.fileSource = new BaseWebRequest(new Uri(new Uri(xXaml.BaseUri), "."), Environment.Encoding);

			// create the control
			control = (FrameworkElement)parentPane.Environment.CreateResource(xXaml);
			control.DataContext = this;

			// run the chunk on the current table
			code?.Run(this, this);
		} // ctor

		protected override object OnIndex(object key)
			=> base.OnIndex(key) ?? WpfPaneHelper.GetXamlElement(control, key);

		[LuaMember]
		private object GetResource(object key)
			=> Control.TryFindResource(key);
		[LuaMember]
		public FrameworkElement Control => control;
		[LuaMember]
		public BaseWebRequest Request => fileSource;
	} // class PpsGenericWpfChildPane 

	#endregion

	#region -- class LuaDataTemplateSelector ---------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class LuaDataTemplateSelector : DataTemplateSelector
	{
		private readonly Delegate selectTemplate;

		public LuaDataTemplateSelector(Delegate selectTemplate)
		{
			this.selectTemplate = selectTemplate;
		} // ctor

		public override DataTemplate SelectTemplate(object item, DependencyObject container)
			=> (DataTemplate)new LuaResult(Lua.RtInvoke(selectTemplate, item, container))[0];
	} // class LuaDataTemplateSelector

	#endregion

	#region -- class PpsGenericWpfWindowPane --------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Pane that combines a xaml file with lua code.</summary>
	public class PpsGenericWpfWindowPane : LuaEnvironmentTable, IPpsWindowPane, IPpsIdleAction, IPpsLuaRequest
	{
		private readonly PpsWindow window;

		private BaseWebRequest fileSource;
		private FrameworkElement control;

		private LuaTable arguments; // arguments

		private bool forceUpdateSource = false; // set this to true, to update the document on idle

		#region -- Ctor/Dtor --------------------------------------------------------------

		public PpsGenericWpfWindowPane(PpsEnvironment environment, PpsWindow window)
			: base(environment)
		{
			this.window = window;
			Environment.AddIdleAction(this);
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

		protected virtual void Dispose(bool disposing)
		{
			if (disposing)
			{
				if (disposing)
					Environment.RemoveIdleAction(this);

				CallMemberDirect("Dispose", new object[0], throwExceptions: false);
			}
		} // proc Dispose

		#endregion

		#region -- Undo/Redo Management ---------------------------------------------------

		/*
		 * TextBox default binding is LostFocus, to support changes in long text, we try to
		 * connact undo operations.
		 */
		private BindingExpression currentBindingExpression = null;

		private static bool IsCharKey(Key k)
		{
			switch (k)
			{
				case Key.Back:
				case Key.Return:
				case Key.Space:
				case Key.D0:
				case Key.D1:
				case Key.D2:
				case Key.D3:
				case Key.D4:
				case Key.D5:
				case Key.D6:
				case Key.D7:
				case Key.D8:
				case Key.D9:
				case Key.A:
				case Key.B:
				case Key.C:
				case Key.D:
				case Key.E:
				case Key.F:
				case Key.G:
				case Key.H:
				case Key.I:
				case Key.J:
				case Key.K:
				case Key.L:
				case Key.M:
				case Key.N:
				case Key.O:
				case Key.P:
				case Key.Q:
				case Key.R:
				case Key.S:
				case Key.T:
				case Key.U:
				case Key.V:
				case Key.W:
				case Key.X:
				case Key.Y:
				case Key.Z:
				case Key.NumPad0:
				case Key.NumPad1:
				case Key.NumPad2:
				case Key.NumPad3:
				case Key.NumPad4:
				case Key.NumPad5:
				case Key.NumPad6:
				case Key.NumPad7:
				case Key.NumPad8:
				case Key.NumPad9:
				case Key.Multiply:
				case Key.Add:
				case Key.Separator:
				case Key.Subtract:
				case Key.Decimal:
				case Key.Divide:
				case Key.Oem1:
				case Key.OemPlus:
				case Key.OemComma:
				case Key.OemMinus:
				case Key.OemPeriod:
				case Key.Oem2:
				case Key.Oem3:
				case Key.Oem4:
				case Key.Oem5:
				case Key.Oem6:
				case Key.Oem7:
				case Key.Oem8:
				case Key.Oem102:
					return true;
				default:
					return false;
			}
		} // func IsCharKey

		private void Control_MouseDownHandler(object sender, MouseButtonEventArgs e)
		{
			if (currentBindingExpression != null && currentBindingExpression.IsDirty)
			{
#if DEBUG && NOTIFY_BINDING_SOURCE_UPDATE
				Debug.Print("TextBox force update on mouse.");
#endif
				forceUpdateSource = true;
			}
		} // event Control_MouseDownHandler

		private void Control_KeyUpHandler(object sender, KeyEventArgs e)
		{
			if (currentBindingExpression != null && currentBindingExpression.IsDirty && !IsCharKey(e.Key))
			{
#if DEBUG && NOTIFY_BINDING_SOURCE_UPDATE
				Debug.Print("TextBox force update on keyboard.");
#endif
				forceUpdateSource = true;
			}
		} // event Control_KeyUpHandler

		private void Control_GotKeyboardFocusHandler(object sender, KeyboardFocusChangedEventArgs e)
		{
			var newTextBox = e.NewFocus as TextBox;
			if (newTextBox != null)
			{
				var b = BindingOperations.GetBinding(newTextBox, TextBox.TextProperty);
				var expr = BindingOperations.GetBindingExpression(newTextBox, TextBox.TextProperty);
				if (b != null && (b.UpdateSourceTrigger == UpdateSourceTrigger.Default || b.UpdateSourceTrigger == UpdateSourceTrigger.LostFocus) && expr.Status != BindingStatus.PathError)
				{
					currentBindingExpression = expr;
#if DEBUG && NOTIFY_BINDING_SOURCE_UPDATE
					Debug.Print("Textbox GotFocus");
#endif
				}
			}
		} // event Control_GotKeyboardFocusHandler

		private void Control_LostKeyboardFocusHandler(object sender, KeyboardFocusChangedEventArgs e)
		{
			if (currentBindingExpression != null && e.OldFocus == currentBindingExpression.Target)
			{
#if DEBUG && NOTIFY_BINDING_SOURCE_UPDATE
				Debug.Print("LostFocus");
#endif
				currentBindingExpression = null;
			}
		} // event Control_LostKeyboardFocusHandler

		private void CheckBindingOnIdle()
		{
			if (currentBindingExpression != null && !forceUpdateSource && currentBindingExpression.IsDirty && !Window.IsActive)
			{
#if DEBUG && NOTIFY_BINDING_SOURCE_UPDATE
				Debug.Print("TextBox force update on idle.");
#endif
				forceUpdateSource = true;
			}
		} // proc CheckBindingOnIdle

		#endregion

		#region -- Lua-Interface ----------------------------------------------------------

		[LuaMember]
		private object GetResource(object key)
			=> Control.TryFindResource(key);

		[LuaMember("requireXaml", true)]
		private LuaResult LuaRequireXaml(LuaTable self, string path, LuaTable initialTable = null)
		{
			// get the current root
			var webRequest = self.GetMemberValue(nameof(IPpsLuaRequest.Request)) as BaseWebRequest ?? Request;

			var parts = Task.Run(() => Environment.LoadXamlAsync(webRequest, initialTable ?? new LuaTable(), webRequest.GetFullUri(path))).Result;
			return Environment.RunUI(new Func<PpsGenericWpfChildPane>(() => new PpsGenericWpfChildPane(this, parts.Item1, parts.Item2)));
		} // func LuaRequireXaml

		[LuaMember("command")]
		private object LuaCommand(Action<object> command, Func<object, bool> canExecute = null, bool idleCall = true)
			=> new PpsCommand(Environment, command, canExecute, idleCall);

		[LuaMember("templateSelector")]
		private DataTemplateSelector LuaDataTemplateSelectorCreate(Delegate func)
			=> new LuaDataTemplateSelector(func);

		[LuaMember("disableUI")]
		public IPpsProgress DisableUI(PpsLuaTask task)
			=> PaneControl?.ProgressStack?.CreateProgress() ?? PpsProgressStack.Dummy;

		[LuaMember("getView")]
		private ICollectionView LuaGetView(object collection)
			=> CollectionViewSource.GetDefaultView(collection);

		[LuaMember("createView")]
		private ICollectionView LuaCreateView(object collection)
		{
			// get containted list
			var listSource = collection as IListSource;
			if (listSource != null)
				collection = listSource.GetList();

			// do we have a factory
			var factory = collection as ICollectionViewFactory;
			if (factory != null)
				return factory.CreateView();

			if (collection is IBindingList)
				return new BindingListCollectionView((IBindingList)collection);
			else if (collection is IList)
				return new ListCollectionView((IList)collection);
			else if (collection is IEnumerable)
				return new CollectionView((IEnumerable)collection);
			else
				throw new ArgumentException("collection is no enumerable.");
		} // func LuaCreateView

		#endregion

		#region -- Load/Unload ------------------------------------------------------------

		private Uri GetTemplateUri(LuaTable arguments, bool throwException)
		{
			// get the basic template
			var xamlFile = arguments.GetOptionalValue("pane", String.Empty);
			if (String.IsNullOrEmpty(xamlFile))
			{
				if (throwException)
					throw new ArgumentException("pane is missing.");
				else
					return null;
			}

			// prepare the base
			return Environment.Request.GetFullUri(xamlFile);
		} // func GetTemplateUri

		public virtual PpsWindowPaneCompareResult CompareArguments(LuaTable otherArgumens)
		{
			var currentXamlUri = GetTemplateUri(arguments, false);
			var otherXamlUri = GetTemplateUri(otherArgumens, false);

			if (Uri.Compare(currentXamlUri, otherXamlUri, UriComponents.SchemeAndServer | UriComponents.Path, UriFormat.UriEscaped, StringComparison.Ordinal) == 0)
				return PpsWindowPaneCompareResult.Reload;

			return PpsWindowPaneCompareResult.Incompatible;
		} // func CompareArguments

		public virtual async Task LoadAsync(LuaTable arguments)
		{
			await Task.Yield(); // move to background

			// save the arguments
			this.arguments = arguments;

			// prepare the base
			var xamlUri = GetTemplateUri(arguments, true);
			fileSource = new BaseWebRequest(new Uri(xamlUri, "."), Environment.Encoding);

			// Load the xaml file and code
			var r = await Environment.LoadXamlAsync(fileSource, arguments, xamlUri);
			var xaml = r.Item1;
			var chunk = r.Item2;

			// Create the Wpf-Control
			var xamlReader = new XamlReader();
			await Dispatcher.InvokeAsync(() =>
			{
				control = xamlReader.LoadAsync(xaml.CreateReader()) as FrameworkElement;
				OnControlCreated();

				// Initialize the control and run the code in UI-Thread
				if (chunk != null)
					Environment.RunScript(chunk, this, true, this);

				// init bindings
				control.DataContext = this;

				// notify if the title will be changed
				if (control is PpsGenericWpfControl)
				{
					var desc = DependencyPropertyDescriptor.FromProperty(PpsGenericWpfControl.TitleProperty, typeof(PpsGenericWpfControl));
					desc.AddValueChanged(control, (sender, e) => OnPropertyChanged("Title"));
					desc = DependencyPropertyDescriptor.FromProperty(PpsGenericWpfControl.SubTitleProperty, typeof(PpsGenericWpfControl));
					desc.AddValueChanged(control, (sender, e) => OnPropertyChanged("SubTitle"));
					desc = DependencyPropertyDescriptor.FromProperty(PpsGenericWpfControl.HasSideBarProperty, typeof(PpsGenericWpfControl));
					desc.AddValueChanged(control, (sender, e) => OnPropertyChanged("HasSideBar"));
				}

				// notify changes on control
				OnPropertyChanged("Control");
				OnPropertyChanged("Commands");
				OnPropertyChanged("Title");
				OnPropertyChanged("SubTitle");
				OnPropertyChanged("HasSideBar");
			});
		} // proc LoadAsync

		protected virtual void OnControlCreated()
		{
			Mouse.AddPreviewMouseDownHandler(Control, Control_MouseDownHandler);
			Mouse.AddPreviewMouseDownOutsideCapturedElementHandler(Control, Control_MouseDownHandler);
			Keyboard.AddPreviewGotKeyboardFocusHandler(Control, Control_GotKeyboardFocusHandler);
			Keyboard.AddPreviewLostKeyboardFocusHandler(Control, Control_LostKeyboardFocusHandler);
			Keyboard.AddPreviewKeyUpHandler(Control, Control_KeyUpHandler);
		} // proc OnControlCreated

		public virtual Task<bool> UnloadAsync(bool? commit = default(bool?))
		{
			if (Members.ContainsKey("UnloadAsync"))
				CallMemberDirect("UnloadAsync", new object[] { commit }, throwExceptions: true);
			return Task.FromResult(true);
		} // func UnloadAsync

		#endregion

		#region -- LuaTable, OnIndex ------------------------------------------------------

		protected override object OnIndex(object key)
			=> base.OnIndex(key) ?? WpfPaneHelper.GetXamlElement(control, key);

		#endregion

		[LuaMember]
		public void UpdateSources()
		{
			forceUpdateSource = false;

			foreach (var expr in BindingOperations.GetSourceUpdatingBindings(Control))
				expr.UpdateSource();
		} // proc UpdateSources

		bool IPpsIdleAction.OnIdle(int elapsed)
		{
			if (elapsed > 300)
			{
				if (forceUpdateSource)
					UpdateSources();
				return false;
			}
			else
			{
				CheckBindingOnIdle();
				return forceUpdateSource;
			}
		} // proc OnIdle

		/// <summary>Arguments of the generic content.</summary>
		[LuaMember]
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

		/// <summary>SubTitle of the pane</summary>
		public string SubTitle
		{
			get
			{
				if (control == null)
					return String.Empty;
				return (string)control.GetValue(PpsGenericWpfControl.SubTitleProperty);
			}
		} // prop SubTitle

		/// <summary>Has sidebar?</summary>
		public bool HasSideBar
		{
			get
			{
				if (control == null)
					return false;
				return (bool)control.GetValue(PpsGenericWpfControl.HasSideBarProperty);
			}
		} // prop HasSideBar

		/// <summary>Wpf-Control</summary>
		[LuaMember]
		public FrameworkElement Control => control;
		/// <summary>This member is resolved dynamic, that is the reason the FrameworkElement Control is public.</summary>
		object IPpsWindowPane.Control => control;

		[LuaMember]
		public PpsWindow Window => window;
		[LuaMember]
		public BaseWebRequest Request => fileSource;

		/// <summary>BaseUri of the Wpf-Control</summary>
		public Uri BaseUri => fileSource?.BaseUri;

		/// <summary>Synchronization to the UI.</summary>
		public Dispatcher Dispatcher => control == null ? Application.Current.Dispatcher : control.Dispatcher;
		/// <summary>Access to the current lua compiler</summary>
		public Lua Lua => Environment.Lua;

		public IEnumerable<object> Commands
		{
			get
			{
				var ppsGeneric = control as PpsGenericWpfControl;
				return ppsGeneric == null ? null : ppsGeneric.Commands;
			}
		} // prop Commands

		/// <summary>Interface of the hosted control.</summary>
		public IPpsPWindowPaneControl PaneControl => control as IPpsPWindowPaneControl;

		public virtual bool IsDirty => false;
	} // class PpsGenericWpfWindowContext

	#endregion

	#region -- class PpsGenericWpfControl -----------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Base control for the wpf generic pane.</summary>
	public class PpsGenericWpfControl : ContentControl, ILuaEventSink, IPpsPWindowPaneControl
	{
		public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(PpsGenericWpfControl), new UIPropertyMetadata(String.Empty));
		public static readonly DependencyProperty SubTitleProperty = DependencyProperty.Register(nameof(SubTitle), typeof(string), typeof(PpsGenericWpfControl), new UIPropertyMetadata(String.Empty));
		public static readonly DependencyProperty HasSideBarProperty = DependencyProperty.Register("HasSideBar", typeof(bool), typeof(PpsGenericWpfControl), new PropertyMetadata(false));

		private readonly PpsUICommandCollection commands;
		private readonly PpsProgressStack progressStack;

		#region -- Ctor/Dtor --------------------------------------------------------------

		/// <summary></summary>
		public PpsGenericWpfControl()
			: base()
		{
			commands = new PpsUICommandCollection();
			commands.CollectionChanged += Commands_CollectionChanged;

			progressStack = new PpsProgressStack(Dispatcher);

			Focusable = false;
		} // ctor

		#endregion

		#region -- ILuaEventSink Member ---------------------------------------------------

		void ILuaEventSink.CallMethod(string methodName, object[] args)
		{
			Pane.CallMember(methodName, args);
		} // proc ILuaEventSink.CallMethod

		#endregion

		#region -- Command Handling -------------------------------------------------------

		private void Commands_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			switch (e.Action)
			{
				case NotifyCollectionChangedAction.Add:
					AddLogicalChild(e.NewItems[0]);
					break;
				case NotifyCollectionChangedAction.Remove:
					RemoveLogicalChild(e.OldItems[0]);
					break;
				default:
					throw new InvalidOperationException();
			}
		} // proc Commands_CollectionChanged

		#endregion

		/// <summary>Access to the owning pane.</summary>
		public PpsGenericWpfWindowPane Pane => (PpsGenericWpfWindowPane)DataContext;

		/// <summary>Title of the window pane</summary>
		[
		DesignerSerializationVisibility(DesignerSerializationVisibility.Visible),
		Description("Sets the title of the pane")
		]
		public string Title { get { return (string)GetValue(TitleProperty); } set { SetValue(TitleProperty, value); } }

		/// <summary>SubTitle of the window pane</summary>
		[
		DesignerSerializationVisibility(DesignerSerializationVisibility.Visible),
		Description("Sets the subtitle of the pane")
		]
		public string SubTitle { get { return (string)GetValue(SubTitleProperty); } set { SetValue(SubTitleProperty, value); } }

		/// <summary>pane with SideBar?</summary>
		[
		DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)
		]
		public bool HasSideBar { get { return (bool)GetValue(HasSideBarProperty); } set { SetValue(HasSideBarProperty, value); } }

		/// <summary>ProgressStack of the pane</summary>
		[
		DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)
		]
		public PpsProgressStack ProgressStack => progressStack;

		/// <summary>List of commands for the main toolbar.</summary>
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
		public PpsUICommandCollection Commands => commands;
	} // class PpsGenericWpfControl

	#endregion

	#region -- class PpsContentTemplateSelector -----------------------------------------

	//public class PpsContentTemplateSelector : DataTemplateSelector
	//{
	//	public override DataTemplate SelectTemplate(object item, DependencyObject container)
	//	{
	//		var row = item as Data.PpsDataRow;
	//		if (row == null)
	//			return null;

	//		var control = container as FrameworkElement;
	//		if (control == null)
	//			return null;

	//		var r = (DataTemplate)control.FindResource(row.Table.TableName);
	//		return r;
	//		//var r = control.TryFindResource(row.Table.TableName);
	//		//return r as DataTemplate;
	//	}
	//} // class PpsContentTemplateSelector

	#endregion
}
