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
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Xaml;
using System.Xml;
using Neo.IronLua;
using TecWare.DE.Stuff;
using TecWare.PPSn.UI;

namespace TecWare.PPSn
{
	#region -- interface IPpsIdleAction -----------------------------------------------

	/// <summary>Implementation for a idle action.</summary>
	public interface IPpsIdleAction
	{
		/// <summary>Gets called on application idle start.</summary>
		/// <param name="elapsed">ms elapsed since the idle started</param>
		/// <returns><c>false</c>, if there are more idles needed.</returns>
		bool OnIdle(int elapsed);
	} // interface IPpsIdleAction

	#endregion

	#region -- class PpsShellWpf ------------------------------------------------------

	/// <summary></summary>
	public abstract class PpsShellWpf : PpsShell, IPpsXamlCode
	{
		#region -- class InstanceKey --------------------------------------------------

		/// <summary>Special key to select templates.</summary>
		private sealed class InstanceKey<T> : ResourceKey
			where T : class
		{
			public InstanceKey()
			{
			} // ctor

			public override int GetHashCode()
				=> typeof(T).GetHashCode();

			public override bool Equals(object obj)
				=> obj is T;

			public override Assembly Assembly => null;
		} // class DefaultEnvironmentKeyImpl

		#endregion

		#region -- class DefaultResourceProvider --------------------------------------

		private sealed class DefaultResourceProvider : ResourceDictionary
		{
			private readonly PpsShellWpf shell;

			public DefaultResourceProvider(PpsShellWpf shell)
			{
				this.shell = shell;

				Add(DefaultEnvironmentKey, shell); // register environment
			} // ctor
		} // class DefaultResourceProvider

		#endregion

		#region -- class DefaultStaticDataTemplateSelector ----------------------------

		private sealed class DefaultStaticDataTemplateSelector : DataTemplateSelector
		{
			public DefaultStaticDataTemplateSelector()
			{
			} // ctor

			public override DataTemplate SelectTemplate(object item, DependencyObject container)
				=> GetShell<PpsShellWpf>(container)?.GetDataTemplate(item, container);
		} // class DefaultStaticDataTemplateSelector

		#endregion

		#region -- class DefaultInstanceDataTemplateSelector --------------------------

		private sealed class DefaultInstanceDataTemplateSelector : DataTemplateSelector
		{
			private readonly PpsShellWpf shell;

			public DefaultInstanceDataTemplateSelector(PpsShellWpf shell)
			{
				this.shell = shell;
			} // ctor

			public override DataTemplate SelectTemplate(object item, DependencyObject container)
				=> shell.GetDataTemplate(item, container);
		} // class DefaultInstanceDataTemplateSelector

		#endregion

		private readonly Dispatcher currentDispatcher;
		private readonly ResourceDictionary mainResources;
		private readonly ResourceDictionary defaultResources;
		private readonly InputManager inputManager;
		private readonly SynchronizationContext synchronizationContext;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary></summary>
		/// <param name="lua"></param>
		/// <param name="mainResources"></param>
		protected PpsShellWpf(Lua lua, ResourceDictionary mainResources)
			: base(lua)
		{
			this.mainResources = mainResources ?? throw new ArgumentNullException(nameof(mainResources));

			defaultResources = new DefaultResourceProvider(this);
			mainResources.MergedDictionaries.Add(defaultResources);

			inputManager = InputManager.Current;
			currentDispatcher = Dispatcher.CurrentDispatcher ?? throw new ArgumentNullException(nameof(Dispatcher.CurrentDispatcher));
			synchronizationContext = new DispatcherSynchronizationContext(currentDispatcher);

			// Start idle implementation
			idleTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(10), DispatcherPriority.ApplicationIdle, (sender, e) => OnIdle(), currentDispatcher);
			inputManager.PreProcessInput += preProcessInputEventHandler = (sender, e) => RestartIdleTimer(e);

			DefaultDataTemplateSelector = new DefaultInstanceDataTemplateSelector(this);
			DefaultExecutedHandler = new ExecutedRoutedEventHandler((sender, e) => ExecutedCommandHandlerImpl(sender, this, e));
			DefaultCanExecuteHandler = new CanExecuteRoutedEventHandler((sender, e) => CanExecuteCommandHandlerImpl(sender, this, e));
		} // ctor

		/// <summary></summary>
		/// <param name="disposing"></param>
		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				mainResources.MergedDictionaries.Remove(defaultResources);
				inputManager.PreProcessInput -= preProcessInputEventHandler;
			}
		} // proc Dispose

		#endregion

		#region -- Idle service -------------------------------------------------------

		#region -- class FunctionIdleActionImplementation -----------------------------

		private sealed class FunctionIdleActionImplementation : IPpsIdleAction
		{
			private readonly Func<int, bool> onIdle;

			public FunctionIdleActionImplementation(Func<int, bool> onIdle)
			{
				this.onIdle = onIdle ?? throw new ArgumentNullException(nameof(onIdle));
			} // ctor

			public bool OnIdle(int elapsed)
				=> onIdle(elapsed);
		} // class FunctionIdleActionImplementation

		#endregion

		private int restartTime = 0;
		private readonly DispatcherTimer idleTimer;
		private readonly List<WeakReference<IPpsIdleAction>> idleActions = new List<WeakReference<IPpsIdleAction>>();
		private readonly PreProcessInputEventHandler preProcessInputEventHandler;

		private int IndexOfIdleAction(IPpsIdleAction idleAction)
		{
			for (var i = 0; i < idleActions.Count; i++)
			{
				if (idleActions[i].TryGetTarget(out var t) && t == idleAction)
					return i;
			}
			return -1;
		} // func IndexOfIdleAction

		/// <summary>Add a idle action.</summary>
		/// <param name="onIdle"></param>
		/// <returns></returns>
		[LuaMember(nameof(AddIdleAction))]
		public IPpsIdleAction AddIdleAction(Func<int, bool> onIdle)
			=> AddIdleAction(new FunctionIdleActionImplementation(onIdle));

		/// <summary>Add a idle action.</summary>
		/// <param name="idleAction"></param>
		/// <returns></returns>
		public IPpsIdleAction AddIdleAction(IPpsIdleAction idleAction)
		{
			Dispatcher.VerifyAccess();

			if (IndexOfIdleAction(idleAction) == -1)
				idleActions.Add(new WeakReference<IPpsIdleAction>(idleAction));
			return idleAction;
		} // proc AddIdleAction

		/// <summary>Remove a idle action.</summary>
		/// <param name="idleAction"></param>
		[LuaMember(nameof(RemoveIdleAction))]
		public void RemoveIdleAction(IPpsIdleAction idleAction)
		{
			if (idleAction == null)
				return;

			Dispatcher.VerifyAccess();

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
				if (idleActions[i].TryGetTarget(out var t))
					stopIdle = stopIdle && !t.OnIdle(timeSinceRestart);
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

		#region -- Wpf Resource Management --------------------------------------------

		/// <summary>Find a global resource.</summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="resourceKey"></param>
		/// <returns></returns>
		public T FindResource<T>(object resourceKey)
			where T : class
			=> defaultResources[resourceKey] as T;

		/// <summary>Update a resource from a xml-source.</summary>
		/// <param name="xamlSource"></param>
		/// <returns></returns>
		protected async Task<object> UpdateResourceAsync(XmlReader xamlSource)
		{
			var resourceKey = (object)null;
			object resource;

			try
			{
				// create the resource
				using (var elementReader = await xamlSource.ReadElementAsSubTreeAsync(XmlNamespaceScope.ExcludeXml))
				{
					var startObjectCounter = 0;

					resource = await PpsXamlParser.LoadAsync<object>(elementReader,
						new PpsXamlReaderSettings()
						{
							FilterNode = (reader) =>
							{
								switch (reader.NodeType)
								{
									case XamlNodeType.GetObject:
									case XamlNodeType.StartObject:
										startObjectCounter++;
										goto default;
									case XamlNodeType.EndObject:
										startObjectCounter--;
										goto default;
									case XamlNodeType.StartMember:
										if (startObjectCounter == 1 && reader.Member == XamlLanguage.Key && resourceKey == null)
										{
											resourceKey = reader.ReadMemberValue();
											return reader.Read();
										}
										goto default;
									default:
										return true;
								}
							},
							CloseInput = false,
							Code = this,
							ServiceProvider = this
						}
					);
				}

				if (resource == null)
					throw Procs.CreateXmlException(xamlSource, "Resource load failed.");

				// check the key
				if (resourceKey == null)
				{
					var style = resource as Style;
					if (style == null)
						throw Procs.CreateXmlException(xamlSource, "x:Key is missing on resource.");

					resourceKey = style.TargetType;
				}
			}
			catch (XmlException) // no recovery, xml is invalid
			{
				throw;
			}
			catch (Exception e) // xaml parser error
			{
				// check resource key
				if (resourceKey == null // no resource key to check, do not load
					|| defaultResources[resourceKey] == null) // no resource, might be imported
					throw;
				else
				{
					var message = $"Could not load resource '{resourceKey}'.";
					if (e is XamlParseException)
						AppendException(e, message);
					else
						AppendException(Procs.CreateXmlException(xamlSource, message, e));

					return null;
				}
			}

			// update resource
			//Debug.Print("UpdateResouce: ({1}){0}", resourceKey, resourceKey.GetType().Name);
			defaultResources[resourceKey] = resource;
			return resourceKey;
		} // func UpdateResource

		/// <summary>Find the resource.</summary>
		/// <param name="key"></param>
		/// <param name="dependencyObject"></param>
		/// <returns></returns>
		[LuaMember]
		public object GetResource(object key, DependencyObject dependencyObject)
		{
			if (dependencyObject is FrameworkElement fe)
				return fe.TryFindResource(key);
			else if (dependencyObject is FrameworkContentElement fce)
				return fce.TryFindResource(key);
			else
				return defaultResources[key] ?? Application.Current.TryFindResource(key);
		} // func GetResource

		void IPpsXamlCode.CompileCode(Uri uri, string code)
			=> throw new NotSupportedException(); // todo: load environment extensions

		#endregion

		#region -- Synchronization ----------------------------------------------------

		/// <summary></summary>
		/// <param name="action"></param>
		public sealed override void BeginInvoke(Action action)
			=> Dispatcher.BeginInvoke(action, DispatcherPriority.ApplicationIdle); // must be idle, that method is invoked after the current changes

		/// <summary></summary>
		/// <param name="action"></param>
		/// <returns></returns>
		public sealed override async Task InvokeAsync(Action action)
			=> await Dispatcher.InvokeAsync(action);

		/// <summary></summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="func"></param>
		/// <returns></returns>
		public sealed override async Task<T> InvokeAsync<T>(Func<T> func)
			=> await Dispatcher.InvokeAsync(func);

		/// <summary>Await Task.</summary>
		/// <param name="task"></param>
		public sealed override void Await(Task task)
			=> task.AwaitTask();

		/// <summary>Await Task.</summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="task"></param>
		/// <returns></returns>
		public sealed override T Await<T>(Task<T> task)
			=> task.AwaitTask();

		/// <summary>Return context</summary>
		public sealed override SynchronizationContext Context => synchronizationContext;

		/// <summary>Wpf main thread dispatcher.</summary>
		[LuaMember]
		public Dispatcher Dispatcher => currentDispatcher;

		#endregion

		#region -- UI-Helper ----------------------------------------------------------

		/// <summary>Append a exception to the log.</summary>
		/// <param name="exception"></param>
		/// <param name="alternativeMessage"></param>
		[LuaMember(nameof(AppendException))]
		public void AppendException(Exception exception, string alternativeMessage = null)
			=> ShowException(ExceptionShowFlags.Background, exception, alternativeMessage);

		/// <param name="text"></param>
		/// <param name="button"></param>
		/// <param name="image"></param>
		/// <param name="defaultResult"></param>
		/// <returns></returns>
		public abstract MessageBoxResult MsgBox(string text, MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.Information, MessageBoxResult defaultResult = MessageBoxResult.OK);

		/// <summary>Display a simple messagebox in the main ui-thread.</summary>
		/// <param name="text"></param>
		/// <param name="button"></param>
		/// <param name="image"></param>
		/// <param name="defaultResult"></param>
		/// <returns></returns>
		public async Task<MessageBoxResult> MsgBoxAsync(string text, MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.Information, MessageBoxResult defaultResult = MessageBoxResult.OK)
			=> await Dispatcher.InvokeAsync(() => MsgBox(text, button, image, defaultResult));

		/// <summary></summary>
		/// <param name="message"></param>
		public sealed override void ShowMessage(string message)
			=> MsgBox(message);

		/// <summary>Return a data template for the object.</summary>
		/// <param name="data"></param>
		/// <param name="container"></param>
		/// <returns></returns>
		public abstract DataTemplate GetDataTemplate(object data, DependencyObject container);

		/// <summary>Return the pane type from an pane type identifier.</summary>
		/// <param name="paneType"></param>
		/// <returns></returns>
		public virtual Type GetPaneTypeFromString(string paneType)
		{
			switch (paneType)
			{
				case "generic":
					return typeof(PpsGenericWpfWindowPane);
				case "pdf":
					return typeof(PpsPdfViewerPane);
				case "picture":
					return typeof(PpsPicturePane);
				case "markdown":
					return typeof(PpsMarkdownPane);
				default:
					return Neo.IronLua.LuaType.GetType(paneType, lateAllowed: false).Type;
			}
		} // func GetPaneTypeFromString

		#endregion

		#region -- Command Service ----------------------------------------------------

		private static void CanExecuteCommandHandlerImpl(object sender, PpsShellWpf environment, CanExecuteRoutedEventArgs e)
		{
			if (!e.Handled && e.Command is PpsCommandBase c)
			{
				e.CanExecute = c.CanExecuteCommand(new PpsCommandContext(environment, e.OriginalSource ?? e.Source, e.Source, e.Parameter));
				e.Handled = true;
			}
		} // func CanExecuteCommandHandlerImpl

		private static void ExecutedCommandHandlerImpl(object sender, PpsShellWpf environment, ExecutedRoutedEventArgs e)
		{
			if (!e.Handled && e.Command is PpsCommandBase c)
			{
				c.ExecuteCommand(new PpsCommandContext(environment, e.OriginalSource ?? e.Source, e.Source, e.Parameter));
				e.Handled = true;
			}
		} // func CanExecuteCommandHandlerImpl

		/// <summary></summary>
		public ExecutedRoutedEventHandler DefaultExecutedHandler { get; }
		/// <summary></summary>
		public CanExecuteRoutedEventHandler DefaultCanExecuteHandler { get; }

		#endregion

		#region -- Lua-Helper ---------------------------------------------------------

		/// <summary>Show a simple message box.</summary>
		/// <param name="text"></param>
		/// <param name="button"></param>
		/// <param name="image"></param>
		/// <param name="defaultResult"></param>
		/// <returns></returns>
		[LuaMember("msgbox")]
		private MessageBoxResult LuaMsgBox(string text, MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.Information, MessageBoxResult defaultResult = MessageBoxResult.OK)
			=> MsgBox(text, button, image, defaultResult);

		/// <summary></summary>
		/// <param name="value"></param>
		/// <param name="targetType"></param>
		/// <returns></returns>
		[LuaMember("ToNumberUI")]
		public object LuaToNumber(object value, Type targetType)
		{
			if (targetType == null)
				throw new ArgumentNullException(nameof(targetType));

			var r = PpsConverter.NumericValue.ConvertBack(value, targetType, null, CultureInfo.CurrentUICulture);
			if (r is ValidationResult
				|| r == DependencyProperty.UnsetValue)
				return null;
			return r;
		} // func LuaToNumber

		/// <summary></summary>
		/// <param name="value"></param>
		/// <returns></returns>
		[LuaMember("ToStringUI")]
		public new object LuaToString(object value)
		{
			var r = PpsConverter.NumericValue.Convert(value, typeof(string), null, CultureInfo.CurrentUICulture);
			if (r is ValidationResult
				|| r == DependencyProperty.UnsetValue)
				return null;
			return r;
		} // func LuaToString

		/// <summary>Helper to await a async process.</summary>
		/// <param name="func"></param>
		/// <returns></returns>
		protected sealed override LuaResult LuaAwaitFunc(object func)
		{
			if (func is DispatcherOperation o)
			{
				LuaAwait(o.Task);
				return LuaResult.Empty;
			}
			return base.LuaAwaitFunc(func);
		} // func LuaAwaitFunc

		#endregion

		/// <summary>Default is utf-8</summary>
		public sealed override Encoding Encoding => Encoding.UTF8;

		/// <summary>Lua ui-wpf framwework.</summary>
		[LuaMember("UI")]
		public LuaUI LuaUI { get; } = new LuaUI();

		#region -- GetShell, GetCurrentPane -------------------------------------------

		/// <summary>Get the environment, that is attached to the current element.</summary>
		/// <param name="dependencyObject"></param>
		/// <returns></returns>
		public static PpsShellWpf GetShell(DependencyObject dependencyObject = null)
			=> GetShell<PpsShellWpf>(dependencyObject);

		/// <summary>Get the environment, that is attached to the current element.</summary>
		/// <param name="dependencyObject"></param>
		/// <returns></returns>
		/// <typeparam name="T"></typeparam>
		public static T GetShell<T>(DependencyObject dependencyObject = null)
			where T : PpsShellWpf
		{
			switch (dependencyObject)
			{
				case null:
					goto default;
				case FrameworkElement fe:
					return (T)fe.TryFindResource(DefaultEnvironmentKey) ?? GetShell<T>(null);
				case FrameworkContentElement fce:
					return (T)fce.TryFindResource(DefaultEnvironmentKey) ?? GetShell<T>(null);
				default:
					return (T)Application.Current.TryFindResource(DefaultEnvironmentKey);
			}
		} // func GetShell

		private static T GetCurrentPaneCore<T>(DependencyObject dependencyObject)
			where T : class, IPpsWindowPane
		{
			switch (dependencyObject)
			{
				case null:
					return default(T);
				case FrameworkElement fe:
					return (T)fe.TryFindResource(CurrentWindowPaneKey);
				case FrameworkContentElement fce:
					return (T)fce.TryFindResource(CurrentWindowPaneKey);
				default:
					return default(T);
			}
		} // func GetCurrentPaneCore

		/// <summary>Get the current pane from the focused element.</summary>
		/// <returns></returns>
		public static IPpsWindowPane GetCurrentPane()
			=> GetCurrentPane<IPpsWindowPane>();

		/// <summary>Get the current pane from the focused element.</summary>
		/// <returns></returns>
		/// <typeparam name="T"></typeparam>
		public static T GetCurrentPane<T>()
			where T : class, IPpsWindowPane
			=> GetCurrentPaneCore<T>(Keyboard.FocusedElement as DependencyObject);

		/// <summary>Get the environment, that is attached to the current element.</summary>
		/// <param name="dependencyObject"></param>
		/// <returns></returns>
		public static IPpsWindowPane GetCurrentPane(DependencyObject dependencyObject)
			=> GetCurrentPane<IPpsWindowPane>(dependencyObject);

		/// <summary>Get the environment, that is attached to the current element.</summary>
		/// <param name="dependencyObject"></param>
		/// <returns></returns>
		/// <typeparam name="T"></typeparam>
		public static T GetCurrentPane<T>(DependencyObject dependencyObject)
			where T : class, IPpsWindowPane
			=> GetCurrentPaneCore<T>(dependencyObject) ?? GetCurrentPane<T>();

		/// <summary>Resource key for the environment.</summary>
		public static ResourceKey DefaultEnvironmentKey { get; } = new InstanceKey<PpsShellWpf>();
		/// <summary>Resource key for the current pane.</summary>
		public static ResourceKey CurrentWindowPaneKey { get; } = new InstanceKey<IPpsWindowPane>();
		/// <summary>Template selection, that redirects to the GetDataTemplate function.</summary>
		public static DataTemplateSelector StaticDataTemplateSelector => new DefaultStaticDataTemplateSelector();

		/// <summary></summary>
		public DataTemplateSelector DefaultDataTemplateSelector { get; }

		#endregion
	} // class PpsShellWpf

	#endregion
}
