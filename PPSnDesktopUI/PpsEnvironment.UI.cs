using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Threading;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Networking;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;
using TecWare.PPSn.UI;

namespace TecWare.PPSn
{
	#region -- interface IPpsIdleAction -------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Implementation for a idle action.</summary>
	public interface IPpsIdleAction
	{
		/// <summary>Gets called on application idle start.</summary>
		/// <param name="elapsed">ms elapsed since the idle started</param>
		/// <returns><c>false</c>, if there are more idles needed.</returns>
		bool OnIdle(int elapsed);
	} // interface IPpsIdleAction

	#endregion

	public partial class PpsEnvironment
	{
		public const string EnvironmentService = "PpsEnvironmentService";

		private readonly Dispatcher currentDispatcher; // Synchronisation
		private readonly InputManager inputManager;
		private readonly SynchronizationContext synchronizationContext;
		private readonly ResourceDictionary mainResources;

		private int restartTime = 0;
		private readonly DispatcherTimer idleTimer;
		private readonly List<WeakReference<IPpsIdleAction>> idleActions = new List<WeakReference<IPpsIdleAction>>();
		private readonly PreProcessInputEventHandler preProcessInputEventHandler;

		public PpsEnvironment(Lua lua)
			: base(lua)
		{
		} // ctor

		private async Task<Tuple<XDocument, DateTime>> GetXmlDocumentAsync(string path, bool isXaml, bool isOptional)
		{
			try
			{
				using (var r = await Request.GetResponseAsync(path))
				using (var xml = Request.GetXmlStream(r, isXaml ? MimeTypes.Application.Xaml : MimeTypes.Text.Xml))
				{
					var dt = DateTime.MinValue;
					return new Tuple<XDocument, DateTime>(XDocument.Load(xml), dt);
				}
			}
			catch (WebException e)
			{
				if (e.Status == WebExceptionStatus.ProtocolError)
					return null;
				throw;
			}
		} // func GetXmlDocumentAsync

		private async Task RefreshDefaultResourcesAsync()
		{
			// update the resources, load a server site resource dictionary
			var t = await GetXmlDocumentAsync("wpf/styles.xaml", true, true);
			if (t == null)
				return;

			var basicTemplates = t.Item1;
			var lastTimeStamp = t.Item2;

			// theme/resources
			var xTheme = basicTemplates.Root;

			// build context
			var parserContext = new ParserContext();
			parserContext.AddNamespaces(xTheme);

			UpdateResources(xTheme, parserContext);
		} // proc RefreshDefaultResourcesAsync

		private async Task RefreshTemplatesAsync()
		{
			var t = await GetXmlDocumentAsync("wpf/templates.xaml", true, true);
			if (t == null)
				return;

			var xTemplates = t.Item1.Root;

			// build context
			var parserContext = new ParserContext();
			parserContext.AddNamespaces(xTemplates);

			// check for a global resource dictionary, and update the main resources
			UpdateResources(xTemplates, parserContext);

			// load the templates
			foreach (var x in xTemplates.Elements("template"))
			{
				var key = x.GetAttribute("key", String.Empty);
				if (String.IsNullOrEmpty(key))
					continue;

				var templateDefinition = templateDefinitions[key];
				if (templateDefinition == null)
				{
					templateDefinition = new PpsDataListItemDefinition(this, key);
					templateDefinitions.AppendItem(templateDefinition);
				}
				templateDefinition.AppendTemplate(x, parserContext);
			}

			// remove unused templates
			// todo:
		} // proc RefreshTemplatesAsync

		private void UpdateResources(XElement xRoot, ParserContext parserContext)
		{
			var xResources = xRoot.Element(StuffUI.PresentationNamespace + "resources");
			if (xResources != null)
			{
				foreach (var cur in xResources.Elements())
					Dispatcher.Invoke(() =>
					{
						try
						{
							UpdateResource(cur.GetAttribute(StuffUI.xnKey, String.Empty), cur.ToString(), parserContext);
						}
						catch (Exception e)
						{
							Debug.Print(e.ToString()); // todo: exception
						}
					}
			);
			}
		} // proc UpdateResources

		#region -- Idle service -----------------------------------------------------------

		#region -- class FunctionIdleActionImplementation ---------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class FunctionIdleActionImplementation : IPpsIdleAction
		{
			private readonly Func<int, bool> onIdle;

			public FunctionIdleActionImplementation(Func<int, bool> onIdle)
			{
				if (onIdle == null)
					throw new ArgumentNullException("onIdle");

				this.onIdle = onIdle;
			} // ctor

			public bool OnIdle(int elapsed)
				=> onIdle(elapsed);
		} // class FunctionIdleActionImplementation

		#endregion

		private int IndexOfIdleAction(IPpsIdleAction idleAction)
		{
			for (int i = 0; i < idleActions.Count; i++)
			{
				IPpsIdleAction t;
				if (idleActions[i].TryGetTarget(out t) && t == idleAction)
					return i;
			}
			return -1;
		} // func IndexOfIdleAction

		[LuaMember(nameof(AddIdleAction))]
		public IPpsIdleAction AddIdleAction(Func<int, bool> onIdle)
			=> AddIdleAction(new FunctionIdleActionImplementation(onIdle));

		public IPpsIdleAction AddIdleAction(IPpsIdleAction idleAction)
		{
			if (IndexOfIdleAction(idleAction) == -1)
				idleActions.Add(new WeakReference<IPpsIdleAction>(idleAction));
			return idleAction;
		} // proc AddIdleAction

		[LuaMember(nameof(RemoveIdleAction))]
		public void RemoveIdleAction(IPpsIdleAction idleAction)
		{
			if (idleAction == null)
				return;

			var i = IndexOfIdleAction(idleAction);
			if (i >= 0)
				idleActions.RemoveAt(i);
		} // proc RemoveIdleAction

		private void OnIdle()
		{
			var stopIdle = true;
			var timeSinceRestart = unchecked(Environment.TickCount - restartTime);
			for (var i = idleActions.Count - 1; i >= 0; i--)
			{
				IPpsIdleAction t;
				if (idleActions[i].TryGetTarget(out t))
				{
					stopIdle = stopIdle && !t.OnIdle(timeSinceRestart);
				}
				else
					idleActions.RemoveAt(i);
			}

			// increase the steps
			if (stopIdle)
				idleTimer.Stop();
			else
				idleTimer.Interval = TimeSpan.FromMilliseconds(100);
		} // proc OnIdle

		private void RestartIdleTimer(PreProcessInputEventArgs e)
		{
			if (idleActions.Count > 0)
			{
				var inputEvent = e.StagingItem.Input;
				if (inputEvent is MouseEventArgs ||
					inputEvent is KeyboardEventArgs)
				{
					restartTime = Environment.TickCount;
					idleTimer.Stop();
					idleTimer.Start();
				}
			}
		} // proc RestartIdleTimer

		#endregion

		#region -- UI - Helper ------------------------------------------------------------

		void IPpsShell.BeginInvoke(Action action)
			=> Dispatcher.BeginInvoke(action, DispatcherPriority.ApplicationIdle); // must be idle, that method is invoked after the current changes

		async Task IPpsShell.InvokeAsync(Action action)
			=> await Dispatcher.InvokeAsync(action);

		async Task<T> IPpsShell.InvokeAsync<T>(Func<T> func)
			=> await Dispatcher.InvokeAsync<T>(func);

		[LuaMember(nameof(AppendException))]
		public void AppendException(Exception exception, string alternativeMessage = null)
			=> ShowException(ExceptionShowFlags.Background, exception, alternativeMessage);

		[LuaMember(nameof(ShowException))]
		public void ShowException(Exception exception, string alternativeMessage = null)
			=> ShowException(ExceptionShowFlags.None, exception, alternativeMessage);

		public void ShowException(ExceptionShowFlags flags, Exception exception, string alternativeMessage = null)
		{
			// always add the exception to the list
			Traces.AppendException(exception, alternativeMessage);

			// show the exception if it is not marked as background
			if ((flags & ExceptionShowFlags.Background) != ExceptionShowFlags.Background)
			{
				var dialogOwner = Application.Current.Windows.OfType<Window>().FirstOrDefault(c => c.IsActive);

				if (ShowExceptionDialog(dialogOwner, flags, exception, alternativeMessage)) // should follow a detailed dialog
					ShowTrace(dialogOwner);

				if ((flags & ExceptionShowFlags.Shutown) != 0) // close application
					Application.Current.Shutdown(1);
			}
		} // proc ShowException

		public static bool ShowExceptionDialog(Window dialogOwner, ExceptionShowFlags flags, Exception exception, string alternativeMessage)
		{
			var shutDown = (flags & ExceptionShowFlags.Shutown) != 0;

			var dialog = new PpsExceptionDialog();
			dialog.MessageType = shutDown ? PpsTraceItemType.Fail : PpsTraceItemType.Exception;
			dialog.MessageText = alternativeMessage ?? exception.Message;
			dialog.SkipVisible = !shutDown;

			dialog.Owner = dialogOwner;
			return dialog.ShowDialog() ?? false; // show the dialog
		} // func ShowExceptionDialog

		public async Task ShowExceptionAsync(ExceptionShowFlags flags, Exception exception, string alternativeMessage = null)
			=> await Dispatcher.InvokeAsync(() => ShowException(flags, exception, alternativeMessage));

		/// <summary></summary>
		/// <param name="owner"></param>
		public void ShowTrace(Window owner)
		{
			var dialog = new PpsTraceDialog();
			var t = dialog.LoadAsync(this);
			if (t.IsCompleted)
				t.Wait();
			dialog.Owner = owner;
			dialog.ShowDialog();
		} // proc ShowTrace

		/// <summary>Returns the pane declaration for the trace pane.</summary>
		public Type TracePane
			=> typeof(PpsTracePane);

		#endregion

		#region -- Resources --------------------------------------------------------------

		public T FindResource<T>(object resourceKey)
			where T : class
			=> mainResources[resourceKey] as T;

		private void UpdateResource(string keyString, string xamlSource, ParserContext parserContext)
		{
			// create the resource
			object resource = CreateResource(xamlSource, parserContext);
			object key;

			// check the key
			if (String.IsNullOrEmpty( keyString ))
			{
				var style = resource as Style;
				if (style == null)
					throw new ArgumentNullException("keyString");

				key = style.TargetType;
			}
			else
				key = keyString;

			mainResources[key] = resource;
		} // func UpdateResource

		public object CreateResource(XDocument xaml)
		{
			if (xaml == null)
				throw new ArgumentNullException("xaml");

			// build context
			var parserContext = new ParserContext();
			parserContext.AddNamespaces(xaml.Root);

			return CreateResource(xaml.ToString(), parserContext);
		} // func CreateResource

		public object CreateResource(string xamlSource, ParserContext parserContext)
		{
			// fix ms bug: XamlReader.Parse creates a MemoryStream with the Default-Ansi-CodePage, but in the XamlReader core utf8 is expected 
			// the XmlReader overload, has no way to give ParserContext
			using (var src = new MemoryStream(Encoding.UTF8.GetBytes(xamlSource), false))
				return XamlReader.Load(src, parserContext);
		} // func CreateResource

		#endregion
	} // class PpsEnvironment
}
