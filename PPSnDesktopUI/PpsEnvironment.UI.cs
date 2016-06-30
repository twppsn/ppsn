using System;
using System.Collections.Generic;
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
		void OnIdle();
	} // interface IPpsIdleAction

	#endregion

	public partial class PpsEnvironment
	{
		public const string EnvironmentService = "PpsEnvironmentService";

		private readonly Dispatcher currentDispatcher; // Synchronisation
		private readonly InputManager inputManager;
		private readonly SynchronizationContext synchronizationContext;
		private readonly ResourceDictionary mainResources;

		private readonly DispatcherTimer idleTimer;
		private readonly List<WeakReference<IPpsIdleAction>> idleActions = new List<WeakReference<IPpsIdleAction>>();
		private readonly PreProcessInputEventHandler preProcessInputEventHandler;

		private async Task<Tuple<XDocument, DateTime>> GetXmlDocumentAsync(string path, bool isXaml, bool isOptional)
		{
			try
			{
				using (var r = await Request.GetResponseAsync(path))
				using (var xml = Request.GetXmlStreamAsync(r, isXaml ? MimeTypes.Application.Xaml : MimeTypes.Text.Xml))
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
			var t = await GetXmlDocumentAsync("wpf/default.xaml", true, true);
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
				templateDefinition.AppendTemplate(x);
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
				{
					var key = cur.GetAttribute(StuffUI.xnKey, String.Empty);
					if (key != null)
						Dispatcher.Invoke(() => UpdateResource(key, cur.ToString(), parserContext));
				}
			}
		} // proc UpdateResources

		#region -- Idle service -----------------------------------------------------------

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

		public void AddIdleAction(IPpsIdleAction idleAction)
		{
			if (IndexOfIdleAction(idleAction) == -1)
				idleActions.Add(new WeakReference<IPpsIdleAction>(idleAction));
		} // proc AddIdleAction

		public void RemoveIdleAction(IPpsIdleAction idleAction)
		{
			var i = IndexOfIdleAction(idleAction);
			if (i >= 0)
				idleActions.RemoveAt(i);
		} // proc RemoveIdleAction

		private void OnIdle()
		{
			for (var i = idleActions.Count - 1; i >= 0; i--)
			{
				IPpsIdleAction t;
				if (idleActions[i].TryGetTarget(out t))
					t.OnIdle();
				else
					idleActions.RemoveAt(i);
			}

			// stop the timer, this function should only called once
			idleTimer.Stop();
		} // proc OnIdle

		private void RestartIdleTimer()
		{
			if (idleActions.Count > 0)
			{
				idleTimer.Stop();
				idleTimer.Start();
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
			var resource = XamlReader.Parse(xamlSource, parserContext); // todo: Exception handling
			mainResources[keyString] = resource;
		} // func UpdateResource

		#endregion
	} // class PpsEnvironment
}
