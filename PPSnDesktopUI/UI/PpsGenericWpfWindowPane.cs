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
using System.ComponentModel;
using System.Linq;
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
using TecWare.DE.Stuff;
using TecWare.PPSn.Controls;
using TecWare.PPSn.Data;
using TecWare.PPSn.Stuff;

namespace TecWare.PPSn.UI
{
	//	#region -- class WpfPaneHelper ----------------------------------------------------

	//	internal static class WpfPaneHelper
	//	{
	//		/// <summary>Find a control by name.</summary>
	//		/// <param name="control"></param>
	//		/// <param name="key"></param>
	//		/// <returns><c>null</c> if there is no control or the current thread is not in the correct ui-thread.</returns>
	//		public static object GetXamlElement(FrameworkElement control, object key)
	//			=> key is string && control != null && control.Dispatcher.CheckAccess() ? control.FindName((string)key) : null;
	//	} // class WpfPaneHelper

	//	#endregion

	//	#region -- class PpsGenericWpfChildPane -------------------------------------------

	//	/// <summary>Sub pane implementation</summary>
	//	public class PpsGenericWpfChildPane : LuaShellTable, IPpsRequest, IPpsXamlCode, IPpsXamlDynamicProperties
	//	{
	//		/// <summary></summary>
	//		/// <param name="parentPane"></param>
	//		/// <param name="paneData"></param>
	//		/// <param name="fullUri"></param>
	//		public PpsGenericWpfChildPane(PpsGenericWpfWindowPane parentPane, object paneData, Uri fullUri)
	//			: base(parentPane)
	//		{
	//			if (parentPane == null)
	//				throw new ArgumentNullException("parentPane");

	//			// create a new request object for this child pane
	//			Request = parentPane.Shell.CreateHttp(new Uri(fullUri, "."));

	//			// create the control
	//			if (paneData is XDocument xamlCode)
	//			{
	//				Control = PpsXamlParser.LoadAsync<FrameworkElement>(PpsXmlPosition.CreateLinePositionReader(xamlCode.CreateReader()), new PpsXamlReaderSettings() { Code = this, CloseInput = true, ServiceProvider = (IServiceProvider)Parent }).AwaitTask();
	//			}
	//			else if (paneData is LuaChunk luaCode) // run the chunk on the current table
	//				luaCode.Run(this, this);
	//			else
	//				throw new ArgumentException(); // todo:

	//			Control.DataContext = this;
	//			CallMemberDirect("OnControlCreated", Array.Empty<object>(), rawGet: true, ignoreNilFunction: true);
	//		} // ctor

	//		#region -- IPpsXamlCode members -----------------------------------------------

	//		void IPpsXamlCode.CompileCode(Uri uri, string code)
	//			=> ShellWpf.CompileCodeForXaml(this, uri, code);

	//		#endregion

	//		#region -- IPpsXamlDynamicProperties members ----------------------------------

	//		void IPpsXamlDynamicProperties.SetValue(string name, object value)
	//			=> this[name] = value;

	//		object IPpsXamlDynamicProperties.GetValue(string name)
	//			=> this[name];

	//		#endregion

	//		/// <summary></summary>
	//		/// <param name="key"></param>
	//		/// <returns></returns>
	//		protected override object OnIndex(object key)
	//			=> base.OnIndex(key) ?? WpfPaneHelper.GetXamlElement(Control, key);

	//		[LuaMember("setControl")]
	//		private FrameworkElement SetControl(object content)
	//		{
	//			if (content is FrameworkElement frameworkElement)
	//				Control = frameworkElement;
	//			else if (content is LuaWpfCreator creator)
	//				Control = creator.GetInstanceAsync<FrameworkElement>(null).AwaitTask();
	//			else
	//				throw new ArgumentNullException(nameof(Control), "Invalid control type.");

	//			OnPropertyChanged(nameof(Control));
	//			return Control;
	//		} // proc UpdateControl

	//		[LuaMember]
	//		private object GetResource(object key, DependencyObject dependencyObject)
	//			=> ShellWpf.GetResource(key, dependencyObject ?? Control);

	//		/// <summary></summary>
	//		[LuaMember]
	//		public FrameworkElement Control { get; private set; }

	//		/// <summary>Returns a PpsShellWpf shell reference.</summary>
	//		public PpsShellWpf ShellWpf => (PpsShellWpf)Shell;

	//		/// <summary></summary>
	//		[LuaMember]
	//		public DEHttpClient Request { get; }
	//	} // class PpsGenericWpfChildPane 

	//	#endregion

	//	#region -- class LuaDataTemplateSelector ------------------------------------------

	//	/// <summary>Helper, to implement a template selector as a lua function.</summary>
	//	public sealed class LuaDataTemplateSelector : DataTemplateSelector
	//	{
	//		private readonly Delegate selectTemplate;

	//		/// <summary>Helper, to implement a template selector as a lua function.</summary>
	//		/// <param name="selectTemplate">Function that gets called.</param>
	//		public LuaDataTemplateSelector(Delegate selectTemplate)
	//		{
	//			this.selectTemplate = selectTemplate;
	//		} // ctor

	//		/// <summary>Calls the template selector function.</summary>
	//		/// <param name="item">Current item.</param>
	//		/// <param name="container">Container element.</param>
	//		/// <returns></returns>
	//		public override DataTemplate SelectTemplate(object item, DependencyObject container)
	//			=> (DataTemplate)new LuaResult(Lua.RtInvoke(selectTemplate, item, container))[0];
	//	} // class LuaDataTemplateSelector

	//	#endregion

	//	#region -- class PpsGenericWpfWindowPane ------------------------------------------

	//	/// <summary>Pane that loads a xaml file, or an lua script.</summary>
	//	public class PpsGenericWpfWindowPane : LuaShellTable, IPpsWindowPane, IPpsIdleAction, IPpsRequest, IUriContext, IPpsXamlCode, IPpsXamlDynamicProperties, IServiceProvider
	//	{
	//		/// <summary>Set this to true, to update the document on idle.</summary>
	//		private bool forceUpdateSource = false;

	//		#region -- Ctor/Dtor ----------------------------------------------------------

	//		/// <summary>Create the pane.</summary>
	//		/// <param name="paneHost"></param>
	//		public PpsGenericWpfWindowPane(IPpsWindowPaneHost paneHost)
	//			: base(paneHost.PaneManager._Shell)
	//		{
	//			PaneHost = paneHost ?? throw new ArgumentNullException(nameof(paneHost));

	//			this.PaneHost.GetService<IPpsIdleService>(true).Add(this);
	//		} // ctor

	//		/// <summary></summary>
	//		~PpsGenericWpfWindowPane()
	//		{
	//			Dispose(false);
	//		} // dtor

	//		/// <summary></summary>
	//		public void Dispose()
	//		{
	//			GC.SuppressFinalize(this);
	//			Dispose(true);
	//		} // proc Dispose

	//		/// <summary></summary>
	//		/// <param name="disposing"></param>
	//		protected virtual void Dispose(bool disposing)
	//		{
	//			if (disposing)
	//			{
	//				PaneManager.GetService<IPpsIdleService>(true).Remove(this);
	//				CallMemberDirect("Dispose", new object[0], rawGet: true, throwExceptions: false);
	//			}
	//		} // proc Dispose

	//		#endregion

	//		#region -- Undo/Redo Management -----------------------------------------------

	//		/*
	//		 * TextBox default binding is LostFocus, to support changes in long text, we try to
	//		 * connact undo operations.
	//		 */
	//		private BindingExpression currentBindingExpression = null;

	//		private static bool IsCharKey(Key k)
	//		{
	//			switch (k)
	//			{
	//				case Key.Back:
	//				case Key.Return:
	//				case Key.Space:
	//				case Key.D0:
	//				case Key.D1:
	//				case Key.D2:
	//				case Key.D3:
	//				case Key.D4:
	//				case Key.D5:
	//				case Key.D6:
	//				case Key.D7:
	//				case Key.D8:
	//				case Key.D9:
	//				case Key.A:
	//				case Key.B:
	//				case Key.C:
	//				case Key.D:
	//				case Key.E:
	//				case Key.F:
	//				case Key.G:
	//				case Key.H:
	//				case Key.I:
	//				case Key.J:
	//				case Key.K:
	//				case Key.L:
	//				case Key.M:
	//				case Key.N:
	//				case Key.O:
	//				case Key.P:
	//				case Key.Q:
	//				case Key.R:
	//				case Key.S:
	//				case Key.T:
	//				case Key.U:
	//				case Key.V:
	//				case Key.W:
	//				case Key.X:
	//				case Key.Y:
	//				case Key.Z:
	//				case Key.NumPad0:
	//				case Key.NumPad1:
	//				case Key.NumPad2:
	//				case Key.NumPad3:
	//				case Key.NumPad4:
	//				case Key.NumPad5:
	//				case Key.NumPad6:
	//				case Key.NumPad7:
	//				case Key.NumPad8:
	//				case Key.NumPad9:
	//				case Key.Multiply:
	//				case Key.Add:
	//				case Key.Separator:
	//				case Key.Subtract:
	//				case Key.Decimal:
	//				case Key.Divide:
	//				case Key.Oem1:
	//				case Key.OemPlus:
	//				case Key.OemComma:
	//				case Key.OemMinus:
	//				case Key.OemPeriod:
	//				case Key.Oem2:
	//				case Key.Oem3:
	//				case Key.Oem4:
	//				case Key.Oem5:
	//				case Key.Oem6:
	//				case Key.Oem7:
	//				case Key.Oem8:
	//				case Key.Oem102:
	//					return true;
	//				default:
	//					return false;
	//			}
	//		} // func IsCharKey

	//		private void Control_MouseDownHandler(object sender, MouseButtonEventArgs e)
	//		{
	//			if (currentBindingExpression != null && currentBindingExpression.IsDirty)
	//			{
	//#if DEBUG && NOTIFY_BINDING_SOURCE_UPDATE
	//				Debug.Print("TextBox force update on mouse.");
	//#endif
	//				forceUpdateSource = true;
	//			}
	//		} // event Control_MouseDownHandler

	//		private void Control_KeyUpHandler(object sender, KeyEventArgs e)
	//		{
	//			if (currentBindingExpression != null && currentBindingExpression.IsDirty && !IsCharKey(e.Key))
	//			{
	//#if DEBUG && NOTIFY_BINDING_SOURCE_UPDATE
	//				Debug.Print("TextBox force update on keyboard.");
	//#endif
	//				forceUpdateSource = true;
	//			}
	//		} // event Control_KeyUpHandler

	//		private void Control_GotKeyboardFocusHandler(object sender, KeyboardFocusChangedEventArgs e)
	//		{
	//			if (e.NewFocus is TextBox newTextBox)
	//			{
	//				var b = BindingOperations.GetBinding(newTextBox, TextBox.TextProperty);
	//				var expr = BindingOperations.GetBindingExpression(newTextBox, TextBox.TextProperty);
	//				if (b != null && (b.UpdateSourceTrigger == UpdateSourceTrigger.Default || b.UpdateSourceTrigger == UpdateSourceTrigger.LostFocus) && expr.Status != BindingStatus.PathError)
	//				{
	//					currentBindingExpression = expr;
	//#if DEBUG && NOTIFY_BINDING_SOURCE_UPDATE
	//					Debug.Print("Textbox GotFocus");
	//#endif
	//				}
	//			}
	//		} // event Control_GotKeyboardFocusHandler

	//		private void Control_LostKeyboardFocusHandler(object sender, KeyboardFocusChangedEventArgs e)
	//		{
	//			if (currentBindingExpression != null && e.OldFocus == currentBindingExpression.Target)
	//			{
	//#if DEBUG && NOTIFY_BINDING_SOURCE_UPDATE
	//				Debug.Print("LostFocus");
	//#endif
	//				currentBindingExpression = null;
	//			}
	//		} // event Control_LostKeyboardFocusHandler

	//		private void CheckBindingOnIdle()
	//		{
	//			if (currentBindingExpression != null && !forceUpdateSource && currentBindingExpression.IsDirty && !PaneManager.IsActive)
	//			{
	//#if DEBUG && NOTIFY_BINDING_SOURCE_UPDATE
	//				Debug.Print("TextBox force update on idle.");
	//#endif
	//				forceUpdateSource = true;
	//			}
	//		} // proc CheckBindingOnIdle

	//		#endregion

	//		#region -- Lua-Interface ------------------------------------------------------

	//		/// <summary>Load a sub control panel.</summary>
	//		/// <param name="self"></param>
	//		/// <param name="path"></param>
	//		/// <param name="initialTable"></param>
	//		/// <returns></returns>
	//		[
	//		LuaMember("requireXaml", true),
	//		LuaMember("requirePane", true)
	//		]
	//		private LuaResult LuaRequirePane(LuaTable self, string path, LuaTable initialTable = null)
	//		{
	//			// get the current root
	//			var webRequest = self.GetMemberValue(nameof(IPpsRequest.Request)) as DEHttpClient ?? Request;
	//			var fullUri = webRequest.CreateFullUri(path);
	//			var paneData = this.LoadPaneDataAsync(initialTable ?? new LuaTable(), fullUri).AwaitTask();
	//			return new LuaResult(new PpsGenericWpfChildPane(this, paneData, fullUri));
	//		} // func LuaRequirePane


	//		[
	//		LuaMember("CreateControl")
	//		]
	//		private object LuaCreateCrontrol(LuaWpfCreator control, LuaTable services)
	//			=> control.GetInstanceAsync<object>(this, 
	//				new PpsXamlReaderSettings()
	//				{
	//					Code = this,
	//					ServiceProvider = this,
	//					ParserServices = services.ArrayList.Cast<PpsParserService>().ToArray()
	//				}
	//			).AwaitTask();

	//		/// <summary>Disable the current panel.</summary>
	//		/// <returns></returns>
	//		[LuaMember("disableUI")]
	//		public IPpsProgress DisableUI(string text = null, int value = -1)
	//			=> PaneHost.DisableUI(text, value);

	//		#endregion

	//		#region -- Load/Unload --------------------------------------------------------

	//		private Uri GetPaneUri(LuaTable arguments, bool throwException)
	//		{
	//			// get the basic template
	//			var paneFile = arguments.GetOptionalValue("Pane", String.Empty);
	//			return String.IsNullOrEmpty(paneFile)
	//				? (throwException ? throw new ArgumentException("Pane is missing.") : (Uri)null)
	//				: Shell.Request.CreateFullUri(paneFile); // prepare the base
	//		} // func GetPaneUri

	//		/// <summary>Compare the arguments.</summary>
	//		/// <param name="otherArgumens"></param>
	//		/// <returns></returns>
	//		public virtual PpsWindowPaneCompareResult CompareArguments(LuaTable otherArgumens)
	//		{
	//			var currentPaneUri = GetPaneUri(Arguments, false);
	//			var otherPaneUri = GetPaneUri(otherArgumens, false);

	//			if (Uri.Compare(currentPaneUri, otherPaneUri, UriComponents.SchemeAndServer | UriComponents.Path, UriFormat.UriEscaped, StringComparison.Ordinal) == 0)
	//				return PpsWindowPaneCompareResult.Reload;

	//			return PpsWindowPaneCompareResult.Incompatible;
	//		} // func CompareArguments

	//		/// <summary></summary>
	//		/// <returns></returns>
	//		protected virtual PpsParserService[] GetRootParserServices()
	//			=> Array.Empty<PpsParserService>();

	//		/// <summary>Load the content of the panel</summary>
	//		/// <param name="arguments"></param>
	//		/// <returns></returns>
	//		public Task LoadAsync(LuaTable arguments)
	//			=> LoadInternAsync(arguments);

	//		/// <summary>Load the content of the panel</summary>
	//		/// <param name="arguments"></param>
	//		/// <returns></returns>
	//		protected virtual async Task LoadInternAsync(LuaTable arguments)
	//		{
	//			// save the arguments
	//			this.Arguments = arguments;

	//			// prepare the base
	//			var paneUri = GetPaneUri(arguments, true);
	//			Request = Shell.CreateHttp(new Uri(paneUri, "."));

	//			// Load the xaml file and code
	//			var paneData = await this.LoadPaneDataAsync(arguments, paneUri);

	//			// Create the Wpf-Control
	//			if (paneData is XDocument xamlCode)
	//			{
	//				//PpsXamlParser.DebugTransform = true;
	//				Control = await PpsXamlParser.LoadAsync<PpsGenericWpfControl>(PpsXmlPosition.CreateLinePositionReader(xamlCode.CreateReader()), 
	//					new PpsXamlReaderSettings()
	//					{
	//						BaseUri = paneUri,
	//						Code = this,
	//						ServiceProvider = this,
	//						ParserServices = GetRootParserServices()
	//					}
	//				);
	//				//PpsXamlParser.DebugTransform = false;
	//				Control.Resources[PpsShellWpf.CurrentWindowPaneKey] = this;
	//				OnControlCreated();
	//			}
	//			else if (paneData is LuaChunk luaCode) // run the code to initalize control, setControl should be called.
	//				Shell.RunScript(luaCode, this, true, this);

	//			if (Control == null)
	//				throw new ArgumentException("No control created.");

	//			// init bindings
	//			Control.DataContext = this;

	//			DependencyPropertyDescriptor.FromProperty(PpsGenericWpfControl.TitleProperty, typeof(PpsGenericWpfControl)).AddValueChanged(Control, ControlTitleChanged);
	//			DependencyPropertyDescriptor.FromProperty(PpsGenericWpfControl.SubTitleProperty, typeof(PpsGenericWpfControl)).AddValueChanged(Control, ControlSubTitleChanged);
	//			DependencyPropertyDescriptor.FromProperty(PpsGenericWpfControl.HasSideBarProperty, typeof(PpsGenericWpfControl)).AddValueChanged(Control, ControlHasSideBarChanged);

	//			// notify changes on control
	//			OnPropertyChanged(nameof(Control));
	//			OnPropertyChanged(nameof(Title));
	//			OnPropertyChanged(nameof(SubTitle));
	//			OnPropertyChanged(nameof(IPpsWindowPane.HasSideBar));
	//			OnPropertyChanged(nameof(Commands));
	//		} // proc LoadInternAsync

	//		/// <summary>Control is created.</summary>
	//		protected virtual void OnControlCreated()
	//		{
	//			CallMemberDirect(nameof(OnControlCreated), Array.Empty<object>(), rawGet: true, ignoreNilFunction: true);

	//			Mouse.AddPreviewMouseDownHandler(Control, Control_MouseDownHandler);
	//			Mouse.AddPreviewMouseDownOutsideCapturedElementHandler(Control, Control_MouseDownHandler);
	//			Keyboard.AddPreviewGotKeyboardFocusHandler(Control, Control_GotKeyboardFocusHandler);
	//			Keyboard.AddPreviewLostKeyboardFocusHandler(Control, Control_LostKeyboardFocusHandler);
	//			Keyboard.AddPreviewKeyUpHandler(Control, Control_KeyUpHandler);
	//		} // proc OnControlCreated

	//		/// <summary>Unload the control.</summary>
	//		/// <param name="commit"></param>
	//		/// <returns></returns>
	//		public virtual Task<bool> UnloadAsync(bool? commit = default(bool?))
	//		{
	//			if (Members.ContainsKey("UnloadAsync"))
	//				CallMemberDirect("UnloadAsync", new object[] { commit }, rawGet: true, throwExceptions: true);

	//			if (Control != null)
	//			{
	//				DependencyPropertyDescriptor.FromProperty(PpsGenericWpfControl.TitleProperty, typeof(PpsGenericWpfControl)).RemoveValueChanged(Control, ControlTitleChanged);
	//				DependencyPropertyDescriptor.FromProperty(PpsGenericWpfControl.SubTitleProperty, typeof(PpsGenericWpfControl)).RemoveValueChanged(Control, ControlSubTitleChanged);

	//				Control = null;
	//				OnPropertyChanged(nameof(Control));
	//				OnPropertyChanged(nameof(Title));
	//				OnPropertyChanged(nameof(SubTitle));
	//			}

	//			return Task.FromResult(true);
	//		} // func UnloadAsync

	//		private void ControlTitleChanged(object sender, EventArgs e)
	//			=> OnPropertyChanged(nameof(Title));

	//		private void ControlSubTitleChanged(object sender, EventArgs e)
	//			=> OnPropertyChanged(nameof(SubTitle));

	//		private void ControlHasSideBarChanged(object sender, EventArgs e)
	//			=> OnPropertyChanged(nameof(IPpsWindowPane.HasSideBar));

	//		#endregion

	//		#region -- LuaTable, OnIndex --------------------------------------------------

	//		/// <summary>Also adds logic to find a control by name.</summary>
	//		/// <param name="key"></param>
	//		/// <returns></returns>
	//		protected override object OnIndex(object key)
	//			=> base.OnIndex(key) ?? WpfPaneHelper.GetXamlElement(Control, key); // todo: XamlParser could do this.

	//		#endregion

	//		#region -- UpdateSources ------------------------------------------------------

	//		/// <summary>Update all bindings. Push pending values to the source.</summary>
	//		[LuaMember]
	//		public void UpdateSources()
	//		{
	//			forceUpdateSource = false;

	//			foreach (var expr in BindingOperations.GetSourceUpdatingBindings(Control))
	//			{
	//				if (!expr.HasError)
	//					expr.UpdateSource();
	//			}
	//		} // proc UpdateSources

	//		bool IPpsIdleAction.OnIdle(int elapsed)
	//		{
	//			if (elapsed > 300)
	//			{
	//				if (forceUpdateSource)
	//					UpdateSources();
	//				return false;
	//			}
	//			else
	//			{
	//				CheckBindingOnIdle();
	//				return forceUpdateSource;
	//			}
	//		} // proc OnIdle

	//		#endregion

	//		#region -- UpdateControl (setControl) -----------------------------------------

	//		private void CheckControl()
	//		{
	//			if (Control == null)
	//				throw new InvalidOperationException("Control is not created.");
	//		} // proc CheckControl

	//		/// <summary>Set within lua code the control.</summary>
	//		/// <param name="args"></param>
	//		/// <returns></returns>
	//		[LuaMember("setControl")]
	//		private PpsGenericWpfControl UpdateControl(object args)
	//		{
	//			PpsGenericWpfControl returnValue;
	//			if (args is PpsGenericWpfControl paneControl) // force a framework element
	//				returnValue = paneControl;
	//			else if (args is LuaTable t) // should be properties for the PpsGenericWpfControl
	//			{
	//				var creator = LuaWpfCreator.CreateFactory(ShellWpf.LuaUI, typeof(PpsGenericWpfControl));
	//				creator.SetTableMembers(t);
	//				using (var xamlReader = new PpsXamlReader(creator.CreateReader(this), new PpsXamlReaderSettings() { Code = this, CloseInput = true, BaseUri = Request.BaseAddress, ServiceProvider = this }))
	//					returnValue = PpsXamlParser.LoadAsync<PpsGenericWpfControl>(xamlReader).AwaitTask();
	//			}
	//			else
	//				throw new ArgumentException(nameof(args));

	//			Control = returnValue;
	//			OnControlCreated();

	//			return Control;
	//		} // proc UpdateControl

	//		#endregion

	//		#region -- IPpsXamlCode members -----------------------------------------------

	//		void IPpsXamlCode.CompileCode(Uri uri, string code)
	//			=> ((PpsShellWpf)Shell).CompileCodeForXaml(this, uri, code);

	//		#endregion

	//		#region -- IPpsXamlDynamicProperties members ----------------------------------

	//		void IPpsXamlDynamicProperties.SetValue(string name, object value)
	//			=> this[name] = value;

	//		object IPpsXamlDynamicProperties.GetValue(string name)
	//			=> this[name];

	//		#endregion

	//		/// <summary></summary>
	//		/// <param name="serviceType"></param>
	//		/// <returns></returns>
	//		public virtual object GetService(Type serviceType)
	//		{
	//			if (serviceType == typeof(IUriContext)
	//				|| serviceType == typeof(IPpsWindowPane))
	//				return this;
	//			else if (serviceType == typeof(IPpsWindowPaneManager))
	//				return PaneManager;
	//			else
	//				return Shell.GetService(serviceType);
	//		} // func GetService

	//		/// <summary>Arguments of the generic content.</summary>
	//		[LuaMember]
	//		public LuaTable Arguments { get; private set; }

	//		/// <summary>Title of the pane</summary>
	//		[LuaMember]
	//		public string Title
	//		{
	//			get => Control?.Title;
	//			set
	//			{
	//				CheckControl();
	//				Control.Title = value;
	//			}
	//		} // prop Title

	//		/// <summary>SubTitle of the pane</summary>
	//		[LuaMember]
	//		public string SubTitle
	//		{
	//			get => Control?.SubTitle;
	//			set
	//			{
	//				CheckControl();
	//				Control.SubTitle = value;
	//			}
	//		} // prop SubTitle

	//		/// <summary>Image of the pane.</summary>
	//		[LuaMember]
	//		public object Image
	//		{
	//			get => Control?.Image;
	//			set
	//			{
	//				CheckControl();
	//				Control.Image = value;
	//			}
	//		}

	//		bool IPpsWindowPane.HasSideBar => Control?.HasSideBar ?? false;

	//		/// <summary>Wpf-Control</summary>
	//		[LuaMember]
	//		public PpsGenericWpfControl Control { get; private set; }

	//		/// <summary>This member is resolved dynamic, that is the reason the FrameworkElement Control is public.</summary>
	//		object IPpsWindowPane.Control => Control;
	//		/// <summary></summary>
	//		[LuaMember]
	//		public PpsUICommandCollection Commands => Control?.Commands;

	//		/// <summary>Access the containing window.</summary>
	//		[LuaMember]
	//		public IPpsWindowPaneManager PaneManager => PaneHost.PaneManager;
	//		/// <summary>Access the containing host.</summary>
	//		[LuaMember]
	//		public IPpsWindowPaneHost PaneHost { get; }

	//		/// <summary></summary>
	//		[LuaMember]
	//		public string HelpKey { get; set; }

	//		/// <summary></summary>
	//		protected virtual IPpsDataInfo CurrentData => null;

	//		IPpsDataInfo IPpsWindowPane.CurrentData => CurrentData;

	//		/// <summary>Base web request, for the pane.</summary>
	//		[LuaMember]
	//		public DEHttpClient Request { get; private set; }

	//		/// <summary>BaseUri of the Wpf-Control</summary>
	//		public Uri BaseUri { get => Request?.BaseAddress; set => throw new NotSupportedException(); }

	//		/// <summary>Synchronization to the UI.</summary>
	//		public Dispatcher Dispatcher => PaneHost.Dispatcher;
	//		/// <summary>Access to the current lua compiler</summary>
	//		public Lua Lua => Shell.Lua;

	//		/// <summary></summary>
	//		[LuaMember("Environment")]
	//		public PpsShellWpf ShellWpf => (PpsShellWpf)Shell;

	//		/// <summary>Is the current pane dirty.</summary>
	//		public virtual bool IsDirty => false;
	//	} // class PpsGenericWpfWindowContext

	//	#endregion
}
