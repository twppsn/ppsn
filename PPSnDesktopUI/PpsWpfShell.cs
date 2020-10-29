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
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using System.Xaml;
using System.Xml;
using Neo.IronLua;
using TecWare.DE.Networking;
using TecWare.DE.Stuff;
using TecWare.PPSn.UI;

namespace TecWare.PPSn
{
	#region -- class PpsWpfShell ------------------------------------------------------

	/// <summary>Shell extensions für Wpf-applications</summary>
	public static partial class PpsWpfShell
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
			private readonly IPpsShell shell;

			public DefaultResourceProvider(IPpsShell shell)
			{
				this.shell = shell;

				Add(DefaultEnvironmentKey, shell); // register environment
			} // ctor
		} // class DefaultResourceProvider

		#endregion

		#region -- GetShell, GetControlService ----------------------------------------

		/// <summary>Adds command handler</summary>
		/// <param name="element"></param>
		/// <param name="shell"></param>
		public static void AddCommandDefaultHandler(this UIElement element, IPpsShell shell)
		{
			var commandManager = shell.GetService<IPpsCommandManager>(true);
			CommandManager.AddExecutedHandler(element, commandManager.DefaultExecutedHandler);
			CommandManager.AddCanExecuteHandler(element, commandManager.DefaultCanExecuteHandler);
		} // proc AddCommandDefaultHandler

		/// <summary>Register shell in wpf resource tree.</summary>
		/// <param name="element"></param>
		/// <param name="shell"></param>
		public static void RegisterShell(this FrameworkElement element, IPpsShell shell)
		{
			var mergedDictionaries = element.Resources.MergedDictionaries;
			var currentShell = mergedDictionaries.OfType<DefaultResourceProvider>().FirstOrDefault();
			if (currentShell != null)
				mergedDictionaries.Remove(currentShell);
			mergedDictionaries.Add(new DefaultResourceProvider(shell));
		} // proc AddShell

		/// <summary>Get the shell, that is attached to the current element.</summary>
		/// <param name="dependencyObject"></param>
		/// <returns></returns>
		public static IPpsShell GetShell(DependencyObject dependencyObject = null)
			=> GetShell<IPpsShell>(dependencyObject);

		/// <summary>Get the shell, that is attached to the current element.</summary>
		/// <param name="dependencyObject"></param>
		/// <returns></returns>
		/// <typeparam name="T"></typeparam>
		public static T GetShell<T>(DependencyObject dependencyObject = null)
			where T : class, IPpsShell
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

		/// <summary>Search for a Service on an Dependency-object. It will also lookup, all its 
		/// parents on the logical tree.</summary>
		/// <typeparam name="T">Type of the service.</typeparam>
		/// <param name="current">Current object in the logical tree.</param>
		/// <param name="throwException"><c>true</c>, to throw an not found exception.</param>
		/// <returns>The service of the default value.</returns>
		public static T GetControlService<T>(this DependencyObject current, bool throwException = false)
			=> (T)GetControlService(current, typeof(T), throwException);

		/// <summary>Search for a Service on an Dependency-object. It will also lookup, all its
		/// parents in the logical tree.</summary>
		/// <param name="current">Current object in the logical tree.</param>
		/// <param name="serviceType">Type of the service.</param>
		/// <param name="useVisualTree"></param>
		/// <returns>The service of the default value.</returns>
		public static object GetControlService(this DependencyObject current, Type serviceType, bool useVisualTree = false)
		{
			object r = null;

			if (current == null)
				return null;
			else if (current is IServiceProvider sp)
			{
				if (serviceType == typeof(IServiceProvider))
					r = current;
				else
					r = sp.GetService(serviceType);
			}
			else if (serviceType.IsAssignableFrom(current.GetType()))
				r = current;

			if (r != null)
				return r;

			return GetControlService(
				useVisualTree
					? GetVisualParent(current)
					: GetLogicalParent(current), serviceType, useVisualTree
			);
		} // func GetControlService

		#endregion

		#region -- GetName, CompareName -----------------------------------------------

		/// <summary></summary>
		/// <param name="current"></param>
		/// <returns></returns>
		public static string GetName(this DependencyObject current)
		{
			switch (current)
			{
				case FrameworkElement fe:
					return fe.Name;
				case FrameworkContentElement fce:
					return fce.Name;
				default:
					return null;
			}
		} // func GetName

		/// <summary></summary>
		/// <param name="current"></param>
		/// <param name="name"></param>
		/// <param name="comparison"></param>
		/// <returns></returns>
		public static int CompareName(this DependencyObject current, string name, StringComparison comparison = StringComparison.Ordinal)
			=> String.Compare(GetName(current), name, comparison);

		#endregion

		#region -- GetLogicalParent, GetVisualParent, GetVisualChild ------------------

		/// <summary>Get the logical parent or the template parent.</summary>
		/// <param name="current"></param>
		/// <returns></returns>
		public static DependencyObject GetLogicalParent(this DependencyObject current)
		{
			switch (current)
			{
				case FrameworkContentElement fce:
					return fce.Parent ?? fce.TemplatedParent;
				case FrameworkElement fe:
					return fe.Parent ?? fe.TemplatedParent;
				default:
					return null;
			}
		} // func GetLogicalParent

		/// <summary></summary>
		/// <param name="current"></param>
		/// <param name="typeOfParent"></param>
		/// <returns></returns>
		public static DependencyObject GetLogicalParent(this DependencyObject current, Type typeOfParent)
		{
			if (current == null)
				return null;
			else if (typeOfParent == null)
				return GetLogicalParent(current);
			else if (typeOfParent.IsAssignableFrom(current.GetType()))
				return current;
			else
				return GetLogicalParent(GetLogicalParent(current), typeOfParent);
		} // func GetVisualParent

		/// <summary></summary>
		/// <param name="current"></param>
		/// <param name="name"></param>
		/// <returns></returns>
		public static DependencyObject GetLogicalParent(this DependencyObject current, string name)
		{
			if (current == null || name == null)
				return null;
			else if (CompareName(current, name) == 0)
				return current;
			else
				return GetLogicalParent(GetLogicalParent(current), name);
		} // func GetVisualParent

		/// <summary>Get the logical parent or the template parent.</summary>
		/// <param name="current"></param>
		/// <returns></returns>
		public static T GetLogicalParent<T>(this DependencyObject current)
			where T : DependencyObject
		{
			var parent = GetLogicalParent(current);
			return parent is T r
				? r
				: GetLogicalParent<T>(parent);
		} // func GetLogicalParent

		/// <summary></summary>
		/// <param name="current"></param>
		/// <returns></returns>
		public static DependencyObject GetVisualParent(this DependencyObject current)
			=> current is Visual || current is Visual3D ? VisualTreeHelper.GetParent(current) : null;

		/// <summary></summary>
		/// <param name="current"></param>
		/// <param name="typeOfParent"></param>
		/// <returns></returns>
		public static DependencyObject GetVisualParent(this DependencyObject current, Type typeOfParent)
		{
			if (current == null)
				return null;
			else if (typeOfParent == null)
				return GetVisualParent(current);
			else if (typeOfParent.IsAssignableFrom(current.GetType()))
				return current;
			else
			{
				var parent = GetVisualParent(current);
				if (parent == null && current.GetType().Name == "PopupRoot")
					parent = GetLogicalParent(current);
				return GetVisualParent(parent, typeOfParent);
			}
		} // func GetVisualParent

		/// <summary></summary>
		/// <param name="current"></param>
		/// <param name="name"></param>
		/// <returns></returns>
		public static DependencyObject GetVisualParent(this DependencyObject current, string name)
		{
			if (current == null || name == null)
				return null;
			else if (CompareName(current, name) == 0)
				return current;
			else
			{
				var parent = GetVisualParent(current);
				if (parent == null && current.GetType().Name == "PopupRoot")
					parent = GetLogicalParent(current);
				return GetVisualParent(parent, name);
			}
		} // func GetVisualParent

		/// <summary></summary>
		/// <param name="current"></param>
		/// <typeparam name="T"></typeparam>
		/// <returns></returns>
		public static T GetVisualParent<T>(this DependencyObject current)
			where T : DependencyObject
		{
			var parent = GetVisualParent(current);
			return parent is T r
				? r
				: GetVisualParent<T>(parent);
		} // func GetVisualParent

		/// <summary>Find a child in the Visual tree.</summary>
		/// <typeparam name="T">Type of the child</typeparam>
		/// <param name="current">Current visual element.</param>
		/// <returns>Child or <c>null</c>.</returns>
		public static T GetVisualChild<T>(this DependencyObject current)
			where T : DependencyObject
		{
			var c = VisualTreeHelper.GetChildrenCount(current);
			for (var i = 0; i < c; i++)
			{
				var v = VisualTreeHelper.GetChild(current, i);
				if (v is T child)
					return child;
				else
				{
					child = GetVisualChild<T>(v);
					if (child != null)
						return child;
				}
			}
			return default;
		} // func GetVisualChild

		#endregion

		#region -- Print Object Tree --------------------------------------------------

		private static DependencyObject InvokeGetUIParent<T>(DependencyObject current)
			where T : class
		{
			var mi = typeof(T).GetMethod("GetUIParentCore", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.InvokeMethod, null, Array.Empty<Type>(), null);
			return (DependencyObject)mi.Invoke(current, Array.Empty<object>());
		} // proc InvokeGetUIParent

		private static DependencyObject GetUIParent(DependencyObject current)
		{
			switch (current)
			{
				case UIElement ui:
					return InvokeGetUIParent<UIElement>(current);
				case UIElement3D ui3d:
					return InvokeGetUIParent<UIElement3D>(current);
				case ContentElement c:
					;
					return InvokeGetUIParent<ContentElement>(current);
				default:
					return null;
			}
		} // func GetUIParent

		private static StringBuilder GetDependencyObjectTree(StringBuilder sb, string prefix, DependencyObject current, Func<DependencyObject, DependencyObject> next)
		{
			while (current != null)
			{
				sb.AppendFormat("{0}{1}: {2}", prefix, current.GetType().Name, current.GetName() ?? "<null>").AppendLine();
				current = next(current);
			}
			return sb;
		} // func GetDependencyObjectTree

		internal static void PrintVisualTreeToConsole(DependencyObject current)
			=> Debug.Print(GetDependencyObjectTree(new StringBuilder("Visual Tree:").AppendLine(), "V ", current, GetVisualParent).ToString());

		internal static void PrintLogicalTreeToConsole(DependencyObject current)
			=> Debug.Print(GetDependencyObjectTree(new StringBuilder("Logical Tree:").AppendLine(), "L ", current, GetLogicalParent).ToString());

		internal static void PrintEventTreeToConsole(DependencyObject current)
				=> Debug.Print(GetDependencyObjectTree(new StringBuilder("UI Tree:").AppendLine(), "U ", current, GetUIParent).ToString());

		#endregion

		#region -- Await --------------------------------------------------------------

		/// <summary>Check if the current synchronization context has a message loop.</summary>
		/// <returns></returns>
		public static SynchronizationContext VerifySynchronizationContext()
		{
			var ctx = SynchronizationContext.Current;
			if (ctx is DispatcherSynchronizationContext || ctx is IPpsProcessMessageLoop)
				return ctx;
			else
				throw new InvalidOperationException($"The synchronization context must be in the single-threaded.");
		} // func VerifySynchronizationContext

		/// <summary></summary>
		/// <param name="task"></param>
		/// <param name="dp"></param>
		public static void AwaitUI(this Task task, DependencyObject dp = null)
			=> PpsShell.Await(task, GetControlService<IServiceProvider>(dp));

		/// <summary></summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="task"></param>
		/// <param name="dp"></param>
		public static T AwaitUI<T>(this Task<T> task, DependencyObject dp = null)
			=> PpsShell.Await(task, GetControlService<IServiceProvider>(dp));

		#endregion

		/// <summary>Resource key for the environment.</summary>
		public static ResourceKey DefaultEnvironmentKey { get; } = new InstanceKey<PpsShellWpf>();
		/// <summary>Resource key for the current pane.</summary>
		public static ResourceKey CurrentWindowPaneKey { get; } = new InstanceKey<IPpsWindowPane>();
	} // class PpsWpfShell

	#endregion

	#region -- class PpsWpfSerivce ----------------------------------------------------

	[PpsService(typeof(IPpsUIService))]
	internal class PpsWpfSerivce : IPpsUIService
	{
		private readonly Dispatcher dispatcher;

		public PpsWpfSerivce()
		{
			dispatcher = Application.Current.Dispatcher;
		} // ctor

		public void ShowException(PpsExceptionShowFlags flags, Exception exception, string alternativeMessage = null)
		{
			// todo: Log.Append(PpsLogType.Exception, exception, alternativeMessage);

			if ((flags & PpsExceptionShowFlags.Background) != PpsExceptionShowFlags.Background)
			{
				// show message simple
				MsgBox(alternativeMessage ?? exception.UnpackException().ToString(), PpsImage.Error, Ok);

				// shutdown application
				if ((flags & PpsExceptionShowFlags.Shutown) == PpsExceptionShowFlags.Shutown)
					Application.Current.Shutdown(1);
			}
		} // proc ShowException

		public int MsgBox(string text, PpsImage image = PpsImage.Information, params string[] buttons)
		{
			MessageBox.Show(text);
			return 0;
		} // func MsgBox

		public async Task RunUI(Action action)
		{
			await dispatcher.InvokeAsync(action);
		} // func IPpsUIService

		public async Task<T> RunUI<T>(Func<T> func)
		{
			return await dispatcher.InvokeAsync(func);
		} // func IPpsUIService

		public string[] Ok { get; } = new string[] { "Ok" };
		public string[] YesNo { get; } = new string[] { "Ja", "Nein" };
		public string[] OkCancel { get; } = new string[] { "Ok", "Abbrechen" };
	} // class PpsWpfSerivce

	#endregion

	#region -- class PpsWpfShellService -----------------------------------------------

	[PpsService(typeof(IPpsCommandManager))]
	internal sealed class PpsWpfShellService : IPpsCommandManager
	{
		private readonly IPpsShell shell;

		public PpsWpfShellService(IPpsShell shell)
		{
			this.shell = shell ?? throw new ArgumentNullException(nameof(shell));

			DefaultExecutedHandler = new ExecutedRoutedEventHandler((sender, e) => ExecutedCommandHandlerImpl(sender, shell, e));
			DefaultCanExecuteHandler = new CanExecuteRoutedEventHandler((sender, e) => CanExecuteCommandHandlerImpl(sender, shell, e));
		} // ctor

		#region -- Command Service ----------------------------------------------------

		private static void CanExecuteCommandHandlerImpl(object _, IPpsShell shell, CanExecuteRoutedEventArgs e)
		{
			if (!e.Handled && e.Command is PpsCommandBase c)
			{
				e.CanExecute = c.CanExecuteCommand(new PpsCommandContext(shell, e.OriginalSource ?? e.Source, e.Source, e.Parameter));
				e.Handled = true;
			}
		} // func CanExecuteCommandHandlerImpl

		private static void ExecutedCommandHandlerImpl(object _, IPpsShell shell, ExecutedRoutedEventArgs e)
		{
			if (!e.Handled && e.Command is PpsCommandBase c)
			{
				c.ExecuteCommand(new PpsCommandContext(shell, e.OriginalSource ?? e.Source, e.Source, e.Parameter));
				e.Handled = true;
			}
		} // func CanExecuteCommandHandlerImpl

		public ExecutedRoutedEventHandler DefaultExecutedHandler { get; }
		public CanExecuteRoutedEventHandler DefaultCanExecuteHandler { get; }

		#endregion

		public IPpsShell Shell { get => shell; set { } }
	} // class PpsWpfShellService

	#endregion

	#region -- class PpsWpfShellSettings ----------------------------------------------

	/// <summary></summary>
	public sealed class PpsWpfShellSettings : PpsSettingsInfoBase
	{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public const string ApplicationModeKey = "PPSn.Application.Mode";
		public const string ApplicationModulKey = "PPSn.Application.Modul";
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		/// <summary></summary>
		/// <param name="settingsService"></param>
		public PpsWpfShellSettings(IPpsSettingsService settingsService)
			: base(settingsService)
		{
		} // ctor

		/// <summary>Automatic login</summary>
		/// <return></return>
		public ICredentials GetAutoLogin()
		{
			if (String.IsNullOrEmpty(AutoLoginUser))
				return null;
			else if (AutoLoginUser == ".")
				return CredentialCache.DefaultCredentials;

			return UserCredential.Create(AutoLoginUser, AutoLoginPassword);
		} // func GetAutoLogin

		/// <summary>Automatic login</summary>
		public string AutoLoginUser => this.GetProperty("PPSn.AutoLogin.UserName", null);
		/// <summary>Automatic login</summary>
		public string AutoLoginPassword => this.GetProperty("PPSn.AutoLogin.Password", null);

		/// <summary>Application mode</summary>
		public string ApplicationMode => this.GetProperty(ApplicationModeKey, "main");
		/// <summary>Initial modul to load.</summary>
		public string ApplicationModul => this.GetProperty(ApplicationModulKey, null);
	} // class PpsWpfShellSettings

	#endregion

	#region -- class PpsWpfIdleService ------------------------------------------------

	[PpsService(typeof(IPpsIdleService))]
	internal sealed class PpsWpfIdleService : PpsIdleServiceBase, IDisposable
	{
		private readonly Dispatcher dispatcher;
		private readonly InputManager inputManager;
		private readonly DispatcherTimer idleTimer;
		private readonly PreProcessInputEventHandler preProcessInputEventHandler;

		public PpsWpfIdleService()
		{
			dispatcher = Application.Current.Dispatcher;
			inputManager = InputManager.Current;

			// Start idle implementation
			idleTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(10), DispatcherPriority.ApplicationIdle, (sender, e) => OnIdle(), dispatcher);
			inputManager.PreProcessInput += preProcessInputEventHandler = (sender, e) => RestartIdleTimer(e);
		} // ctor

		public void Dispose()
		{
			inputManager.PreProcessInput -= preProcessInputEventHandler;
		} // proc Dispose

		protected override void VerifyAccess() 
			=> dispatcher.VerifyAccess();

		protected override void StartIdleCore()
		{
			idleTimer.Stop();
			idleTimer.Start();
		} // proc StartIdleCore

		private void OnIdle()
		{
			DoIdle(out var stopIdle);

			// increase the steps
			if (stopIdle)
				idleTimer.Stop();
			else
				idleTimer.Interval = TimeSpan.FromMilliseconds(100);
		} // proc OnIdle

		private void RestartIdleTimer(PreProcessInputEventArgs e)
		{
			var inputEvent = e.StagingItem.Input;
			if (inputEvent is MouseEventArgs || inputEvent is KeyboardEventArgs)
				RestartIdle();

			CommandManager.InvalidateRequerySuggested();
		} // proc RestartIdleTimer
	} // class class PpsWpfIdleService

	#endregion

	#region -- class PpsWpfAsyncService -----------------------------------------------

	[PpsService(typeof(IPpsAsyncService))]
	internal sealed class PpsWpfAsyncService : IPpsAsyncService
	{
		void IPpsAsyncService.Await(IServiceProvider sp, Task task)
		{
			if (task.IsCompleted)
				return;

			if (SynchronizationContext.Current is DispatcherSynchronizationContext)
			{
				var frame = new DispatcherFrame();

				// get the awaiter
				task.GetAwaiter().OnCompleted(() => frame.Continue = false);

				// block ui for the task
				Thread.Sleep(1); // force context change
				if (frame.Continue)
				{
					using (sp?.CreateProgress())
						Dispatcher.PushFrame(frame);
				}

				// thread is cancelled, do not wait for finish
				if (!task.IsCompleted)
					throw new OperationCanceledException();
			}
			else if (SynchronizationContext.Current is IPpsProcessMessageLoop ctx)
				ctx.ProcessMessageLoop(task);
		} // func AwaitCore
	} // class PpsWpfAsyncService

	#endregion








	#region -- class PpsShellWpf ------------------------------------------------------

	/// <summary></summary>
	public abstract class PpsShellWpf : _PpsShell, IPpsXamlCode
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

			DefaultDataTemplateSelector = new DefaultInstanceDataTemplateSelector(this);
		} // ctor

		/// <summary></summary>
		/// <param name="disposing"></param>
		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				mainResources.MergedDictionaries.Remove(defaultResources);
			}
		} // proc Dispose

		#endregion

		#region -- Wpf Resource Management --------------------------------------------

		private class FindResourceStackItem
		{
			private int mergedIndex;

			public FindResourceStackItem(ResourceDictionary resourceDictionary)
			{
				Resources = resourceDictionary;

				mergedIndex = resourceDictionary.MergedDictionaries.Count - 1;
			} // ctor

			public bool TryGetNextDictionary(out FindResourceStackItem stackItem)
			{
				if (mergedIndex >= 0)
				{
					stackItem = new FindResourceStackItem(Resources.MergedDictionaries[mergedIndex--]);
					return true;
				}
				else
				{
					stackItem = null;
					return false;
				}
			} // func TryGetNextDictionary

			public ResourceDictionary Resources { get; }
		} // class FindResourceStackItem

		/// <summary>Find a global resource.</summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="resourceKey"></param>
		/// <returns></returns>
		public T FindResource<T>(object resourceKey)
			where T : class
			=> defaultResources[resourceKey] as T;

		/// <summary></summary>
		/// <typeparam name="TKEY"></typeparam>
		/// <typeparam name="T"></typeparam>
		/// <param name="resourceDictionary"></param>
		/// <param name="predicate"></param>
		/// <returns></returns>
		public static IEnumerable<T> FindResourceByKeyCore<TKEY, T>(ResourceDictionary resourceDictionary, Predicate<TKEY> predicate = null)
			where TKEY : ResourceKey
		{
			var dictionaryStack = new Stack<FindResourceStackItem>();
			var current = new FindResourceStackItem(resourceDictionary);

			var returnedKeys = new List<TKEY>();

			while (current != null)
			{
				// enumerate merged resources
				while (current.TryGetNextDictionary(out var stackItem))
				{
					dictionaryStack.Push(current);
					current = stackItem;
				}

				// enumerate resource keys
				foreach (var key in current.Resources.Keys.OfType<TKEY>())
				{
					if (!returnedKeys.Contains(key) && (predicate == null || predicate(key)) && current.Resources[key] is T v)
					{
						returnedKeys.Add(key);
						yield return v;
					}
				}
				
				current = dictionaryStack.Count > 0 ? dictionaryStack.Pop() : null;
			}
		} // func FindResourceByKeyCore

		/// <summary>Find resource by key in the main resource dictionary</summary>
		/// <typeparam name="TKEY"></typeparam>
		/// <typeparam name="T"></typeparam>
		/// <param name="predicate"></param>
		/// <returns></returns>
		public IEnumerable<T> FindResourceByKey<TKEY,T>(Predicate<TKEY> predicate = null)
			where TKEY : ResourceKey
			=> FindResourceByKeyCore<TKEY,T>(mainResources, predicate);

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
			=> CompileCodeForXaml(this, uri, code);

		/// <summary></summary>
		/// <param name="self"></param>
		/// <param name="uri"></param>
		/// <param name="code"></param>
		public void CompileCodeForXaml(LuaTable self, Uri uri, string code)
		{
			var request = self as IPpsRequest ?? this;

			var compileTask =
				code != null
				? CompileAsync(code, uri?.OriginalString ?? "dummy.lua", true, new KeyValuePair<string, Type>("self", typeof(LuaTable)))
				: request.CompileAsync(uri, true, new KeyValuePair<string, Type>("self", typeof(LuaTable)));
			compileTask.Await().Run(self, request);
		} // proc CompileCode

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
			=> ShowException(PpsExceptionShowFlags.Background, exception, alternativeMessage);

		private static string GetMessageCaptionFromImage(MessageBoxImage image)
		{
			switch (image)
			{
				case MessageBoxImage.Error:
					return "Fehler";
				case MessageBoxImage.Warning:
					return "Warnung";
				case MessageBoxImage.Question:
					return "Frage";
				default:
					return "Information";
			}
		} // func GetMessageCaptionFromImage

		/// <summary>Display a simple messagebox</summary>
		/// <param name="text"></param>
		/// <param name="button"></param>
		/// <param name="image"></param>
		/// <param name="defaultResult"></param>
		/// <returns></returns>
		public virtual MessageBoxResult MsgBox(string text, MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.Information, MessageBoxResult defaultResult = MessageBoxResult.OK)
			=> MessageBox.Show(text, GetMessageCaptionFromImage(image), button, image, defaultResult);

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

		/// <summary></summary>
		/// <param name="flags"></param>
		/// <param name="exception"></param>
		/// <param name="alternativeMessage"></param>
		public override void ShowException(PpsExceptionShowFlags flags, Exception exception, string alternativeMessage = null)
		{
			//Log.Append(PpsLogType.Exception, exception, alternativeMessage);

			if ((flags & PpsExceptionShowFlags.Background) != PpsExceptionShowFlags.Background)
			{
				// show message simple
				MsgBox(alternativeMessage ?? exception.UnpackException().ToString(), MessageBoxButton.OK, MessageBoxImage.Error);

				// shutdown application
				if ((flags & PpsExceptionShowFlags.Shutown) == PpsExceptionShowFlags.Shutown)
					Application.Current.Shutdown(1);
			}
		} // proc ShowException
		
		/// <summary>Return a data template for the object.</summary>
		/// <param name="data"></param>
		/// <param name="container"></param>
		/// <returns></returns>
		public abstract DataTemplate GetDataTemplate(object data, DependencyObject container);

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

		// -- Static ----------------------------------------------------------

		private readonly static ResourceDictionary staticDefaultResources;

		static PpsShellWpf()
		{
			staticDefaultResources = new ResourceDictionary() { Source = new Uri("/PPSn.Desktop.UI;component/UI/UI.xaml", UriKind.Relative) };
		} // ctor

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
