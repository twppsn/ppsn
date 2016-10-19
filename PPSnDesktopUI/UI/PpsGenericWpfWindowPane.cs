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
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Threading;
using System.Xml;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Networking;
using TecWare.DE.Stuff;
using static TecWare.PPSn.StuffUI;

namespace TecWare.PPSn.UI
{
	#region -- class PpsGenericWpfWindowPane --------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Pane that combines a xaml file with lua code.</summary>
	public class PpsGenericWpfWindowPane : LuaEnvironmentTable, IPpsWindowPane
	{
		private BaseWebRequest fileSource;
		private FrameworkElement control;

		private LuaTable arguments; // arguments

		#region -- Ctor/Dtor --------------------------------------------------------------

		public PpsGenericWpfWindowPane(PpsEnvironment environment)
			: base(environment)
		{
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
		} // proc Dispose

		#endregion

		#region -- Lua-Interface ----------------------------------------------------------

		[LuaMember("require")]
		private LuaResult LuaRequire(string path)
		{
			var chunk = Task.Run(async () => await Environment.CompileAsync(fileSource.GetFullUri(path), true)).Result;
			return Environment.RunScript(chunk, this, true);
		} // proc LuaRequire

		[LuaMember("command")]
		private object LuaCommand(Action<object> command, Func<object, bool> canExecute = null, bool idleCall = true)
			=> new PpsCommand(Environment, command, canExecute, idleCall);

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

			// Load the xaml file
			var xaml = await LoadXamlAsync(arguments, xamlUri);

			// Load the content of the code-tag, to initialize extended functionality
			var xCode = xaml.Root.Element(xnCode);
			var chunk = (LuaChunk)null;
			if (xCode != null)
			{
				chunk = await Environment.CompileAsync(xCode, true);
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
					Environment.RunScript(chunk, this, true);

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

		private async Task<XDocument> LoadXamlAsync(LuaTable arguments, Uri xamlUri)
		{
			try
			{
				using (var r = await fileSource.GetResponseAsync(xamlUri.ToString()))
				{
					// read the file name
					arguments["_filename"] = r.GetContentDisposition().FileName;

					// parse the xaml as xml document
					using (var sr = fileSource.GetTextReaderAsync(r, MimeTypes.Application.Xaml))
					{
						using (var xml = XmlReader.Create(sr, Procs.XmlReaderSettings, xamlUri.ToString()))
							return XDocument.Load(xml, LoadOptions.SetBaseUri | LoadOptions.SetLineInfo);
					}
				}
			}
			catch (Exception e)
			{
				throw new ArgumentException("Can not load xaml definition.\n" + xamlUri.ToString(), e);
			}
		} // func LoadXamlAsync

		protected virtual void OnControlCreated()
		{
		} // proc OnControlCreated

		public virtual Task<bool> UnloadAsync(bool? commit = default(bool?))
			=> Task.FromResult(true);

		#endregion

		#region -- LuaTable, OnIndex ------------------------------------------------------

		private object GetXamlElement(object key)
		{
			if (key is string)
				return Control.FindName((string)key);
			else
				return null;
		} // func GetXamlElement

		protected override object OnIndex(object key)
		{
			return base.OnIndex(key) ?? GetXamlElement(key);
		} // func OnIndex

		#endregion
		
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
		[LuaMember(nameof(Control))]
		public FrameworkElement Control => control;
		/// <summary>This member is resolved dynamic, that is the reason the FrameworkElement Control is public.</summary>
		object IPpsWindowPane.Control => control;

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

		/// <summary>Title of the window pane</summary>
		[
		DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden),
		]
		public PpsProgressStack ProgressStack => progressStack;

		/// <summary>List of commands for the main toolbar.</summary>
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
		public PpsUICommandCollection Commands => commands;
	} // class PpsGenericWpfControl

	#endregion

	#region -- class PpsContentTemplateSelector -----------------------------------------

	public class PpsContentTemplateSelector : DataTemplateSelector
	{
		public override DataTemplate SelectTemplate(object item, DependencyObject container)
		{
			var row = item as Data.PpsDataRow;
			if (row == null)
				return null;

			var control = container as FrameworkElement;
			if (control == null)
				return null;

			var r = (DataTemplate)control.FindResource(row.Table.TableName);
			return r;
			//var r = control.TryFindResource(row.Table.TableName);
			//return r as DataTemplate;
		}
	} // class PpsContentTemplateSelector

	#endregion
}
