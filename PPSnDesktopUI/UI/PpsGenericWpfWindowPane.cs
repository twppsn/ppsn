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
using System.Web;
using TecWare.DE.Stuff;
using TecWare.DE.Networking;
using System.Xml;
using System.Collections.Specialized;

namespace TecWare.PPSn.UI
{
	#region -- class PpsGenericWpfWindowPane --------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Pane that combines a xaml file with lua code.</summary>
	public class PpsGenericWpfWindowPane : LuaEnvironmentTable, IPpsWindowPane
	{
		private static readonly XName xnCode = XName.Get("Code", "http://schemas.microsoft.com/winfx/2006/xaml");

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

		protected virtual void Dispose(bool lDisposing)
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
		{
			var cmd = new PpsCommand(Environment, command, canExecute);
			if (canExecute != null && idleCall)
				Environment.AddIdleAction(cmd);
			return cmd;
		} // func LuaCommand

		#endregion

		#region -- Load/Unload ------------------------------------------------------------

		private Uri GetTemplateUri(LuaTable arguments, bool throwException)
		{
			// get the basic template
			var xamlFile = arguments.GetOptionalValue("template", String.Empty);
			if (String.IsNullOrEmpty(xamlFile))
			{
				if (throwException)
					throw new ArgumentException("template is missing.");
				else
					return null;
			}

			// prepare the base
			return Environment.BaseRequest.GetFullUri(xamlFile);
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

			// use the argument as base table
			this.arguments = arguments;
			
			// prepare the base
			var xamlUri = GetTemplateUri(arguments, true);
			fileSource = new BaseWebRequest(new Uri(xamlUri, "."), Environment.Encoding);

			// Load the xaml file
			var xaml = await LoadXamlAsync(arguments, xamlUri);

			// Load the content of the code-tag, to initialize extend functionality
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
				
		public Task<bool> UnloadAsync(bool? commit = default(bool?))
		{
			return Task.FromResult(true);
		} // func UnloadAsync

		#endregion

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
		public FrameworkElement Control { get { return control; } }
		/// <summary>This member is resolved dynamic, that is the reason the FrameworkElement Control is public.</summary>
		object IPpsWindowPane.Control { get { return control; } }

		/// <summary>BaseUri of the Wpf-Control</summary>
		public Uri BaseUri => fileSource?.BaseUri;

		/// <summary>Synchronization to the UI.</summary>
		public Dispatcher Dispatcher => control == null ? Application.Current.Dispatcher : control.Dispatcher;
		/// <summary>Access to the current lua compiler</summary>
		public Lua Lua => Environment.Lua;

		public IEnumerable<object> Commands => control == null ? null : ((PpsGenericWpfControl)control).Commands;

		public virtual bool IsDirty => false;
	} // class PpsGenericWpfWindowContext

	#endregion

	#region -- class PpsGenericWpfControl -----------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Base control for the wpf generic pane.</summary>
	public class PpsGenericWpfControl : ContentControl, ILuaEventSink
	{
		public static readonly DependencyProperty TitleProperty = DependencyProperty.Register("Title", typeof(string), typeof(PpsGenericWpfControl), new UIPropertyMetadata(String.Empty));

		private PpsUICommandCollection commands;

		#region -- Ctor/Dtor --------------------------------------------------------------

		/// <summary></summary>
		public PpsGenericWpfControl()
			: base()
		{
			commands = new PpsUICommandCollection();
			commands.CollectionChanged += Commands_CollectionChanged;
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
		/// <summary>List of commands for the main toolbar.</summary>
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
		public PpsUICommandCollection Commands => commands;
	} // class PpsGenericWpfWindowPane

	#endregion
}
