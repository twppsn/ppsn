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
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
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

				AddShellKey(); // register environment
			} // ctor

			public void AddShellKey() 
				=> Add(DefaultShellKey, shell);

			protected override void OnGettingValue(object key, ref object value, out bool canCache)
			{
				if (key == DefaultShellKey)  // register environment
				{
					value = shell;
					canCache = false;
				}
				else
					base.OnGettingValue(key, ref value, out canCache);
			} // proc OnGettingValue
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

		internal static ResourceDictionary CreateDefaultResources(IPpsShell shell)
			=> new DefaultResourceProvider(shell);

		internal static void FixDefaultKey(ResourceDictionary resourceDictionary)
			=> ((DefaultResourceProvider)resourceDictionary).AddShellKey();

		private static ResourceDictionary RegisterShell(ResourceDictionary resourceDictionary, IPpsShell shell)
		{
			var mergedDictionaries = resourceDictionary.MergedDictionaries;
			var currentShell = mergedDictionaries.OfType<DefaultResourceProvider>().FirstOrDefault();
			if (currentShell != null)
				mergedDictionaries.Remove(currentShell);

			var defaultResources = CreateDefaultResources(shell);
			mergedDictionaries.Add(defaultResources);
			return defaultResources;
		} // func RegisterShell

		/// <summary>Register shell in wpf resource tree.</summary>
		/// <param name="element"></param>
		/// <param name="shell"></param>
		/// <returns></returns>
		public static ResourceDictionary RegisterShell(this FrameworkElement element, IPpsShell shell)
			=> RegisterShell(element.Resources, shell);

		/// <summary>Get the shell, that is attached to the current element.</summary>
		/// <param name="dependencyObject"></param>
		/// <returns></returns>
		public static IPpsShell GetShell(this DependencyObject dependencyObject)
		{
			switch (dependencyObject)
			{
				case null:
					goto default;
				case FrameworkElement fe:
					return (IPpsShell)fe.TryFindResource(DefaultShellKey) ?? GetShell(null);
				case FrameworkContentElement fce:
					return (IPpsShell)fce.TryFindResource(DefaultShellKey) ?? GetShell(null);
				default:
					return (IPpsShell)Application.Current.TryFindResource(DefaultShellKey);
			}
		} // func GetShell	

		/// <summary>Search for a Service on an Dependency-object. It will also lookup, all its 
		/// parents on the logical tree.</summary>
		/// <typeparam name="T">Type of the service.</typeparam>
		/// <param name="current">Current object in the logical tree.</param>
		/// <param name="throwException"><c>true</c>, to throw an not found exception.</param>
		/// <param name="useVisualTree"></param>
		/// <returns>The service of the default value.</returns>
		public static T GetControlService<T>(this DependencyObject current, bool throwException = false, bool useVisualTree = false)
			=> (T)GetControlService(current, typeof(T), throwException, useVisualTree);

		/// <summary>Search for a Service on an Dependency-object. It will also lookup, all its
		/// parents in the logical tree.</summary>
		/// <param name="current">Current object in the logical tree.</param>
		/// <param name="serviceType">Type of the service.</param>
		/// <param name="useVisualTree"></param>
		/// <returns>The service of the default value.</returns>
		public static object GetControlService(this DependencyObject current, Type serviceType, bool throwException = false, bool useVisualTree = false)
		{
			object r = null;

			if (current == null)
			{
				if (throwException)
					throw new ArgumentException($"Service {serviceType.Name} is not implemented.");
				return null;
			}
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

			var parentObject = useVisualTree
					? GetVisualParent(current)
					: GetLogicalParent(current);
			return parentObject == null
				? (GetShell(current)?.GetService(serviceType) ?? throw new ArgumentException($"Service {serviceType.Name} is not implemented."))
				: GetControlService(parentObject, serviceType, throwException, useVisualTree);
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
		public static ResourceKey DefaultShellKey { get; } = new InstanceKey<IPpsShell>();
		/// <summary>Resource key for the current pane.</summary>
		public static ResourceKey CurrentWindowPaneKey { get; } = new InstanceKey<IPpsWindowPane>();
	} // class PpsWpfShell

	#endregion

	#region -- class PpsWpfSerivce ----------------------------------------------------

	[
	PpsService(typeof(IPpsUIService)),
	PpsService(typeof(IPpsAsyncService))
	]
	internal class PpsWpfSerivce : IPpsUIService, IPpsAsyncService
	{
		private readonly Dispatcher dispatcher;
		private PpsNotificationWindow currentNotificationWindow = null;

		public PpsWpfSerivce()
		{
			dispatcher = Application.Current.Dispatcher;
		} // ctor

		#region -- UI Service ---------------------------------------------------------

		public void ShowException(PpsExceptionShowFlags flags, Exception exception, string alternativeMessage = null)
		{
			// log exception
			PpsShell.Current.LogProxy().Except(exception, alternativeMessage);

			if ((flags & PpsExceptionShowFlags.Background) != PpsExceptionShowFlags.Background)
			{
				// show message simple
				MsgBox(alternativeMessage ?? exception.GetInnerException().ToString(), PpsImage.Error, Ok);

				// shutdown application
				if ((flags & PpsExceptionShowFlags.Shutown) == PpsExceptionShowFlags.Shutown)
					Application.Current.Shutdown(1);
			}
		} // proc ShowException

		public async Task ShowNotificationAsync(object message, PpsImage image)
		{
			await await dispatcher.InvokeAsync(() =>
			{
				var activeWindow = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
				if (currentNotificationWindow == null)
				{
					currentNotificationWindow = new PpsNotificationWindow();
					currentNotificationWindow.Closed += (sender, e) => currentNotificationWindow = null;
				}
				return currentNotificationWindow.ShowAsync(activeWindow, message, image);
			});
		} // proc ShowNotificationAsync

		public int MsgBox(string text, PpsImage image = PpsImage.Information, params string[] buttons)
		{
			// PpsMessageDialog ausbauen
			if (buttons == YesNo || buttons == OkCancel)
			{
				switch (MessageBox.Show(text, "Frage", MessageBoxButton.YesNo, MessageBoxImage.Question))
				{
					case MessageBoxResult.Yes:
						return 0;
					case MessageBoxResult.No:
						return 1;
					default:
						return -1;
				}
			}
			else
			{
				MessageBox.Show(text);
				return 0;
			}
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

		#endregion

		#region -- Async Service ------------------------------------------------------

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

		#endregion
	} // class PpsWpfSerivce

	#endregion

	#region -- class PpsWpfShellService -----------------------------------------------

	[
	PpsService(typeof(IPpsCommandManager)),
	PpsService(typeof(IPpsWpfResources))
	]
	internal sealed class PpsWpfShellService : IPpsCommandManager, IPpsWpfResources, IPpsShellService, IPpsShellServiceInit
	{
		#region -- class PpsWebRequestCreate ------------------------------------------

		private class PpsWebRequestCreate : IWebRequestCreate
		{
			private readonly WeakReference<PpsWpfShellService> shellReference;

			public PpsWebRequestCreate(PpsWpfShellService shell)
			{
				shellReference = new WeakReference<PpsWpfShellService>(shell);
			} // ctor

			public WebRequest Create(Uri uri)
			{
				if (shellReference.TryGetTarget(out var environment))
					return environment.CreateWebRequest(uri);
				else
					throw new ObjectDisposedException("Shell does not exist anymore.");
			} // func Create
		} // class PpsWebRequestCreate

		#endregion

		private readonly IPpsShell shell;
		private readonly Uri shellUri;

		private readonly ResourceDictionary mainResources;    // Application resources
		private readonly ResourceDictionary defaultResources; // default resource, to register shell

		#region -- Ctor/Dtor ----------------------------------------------------------

		public PpsWpfShellService(IPpsShell shell)
		{
			this.shell = shell ?? throw new ArgumentNullException(nameof(shell));

			shellUri = RegisterWebRequestProxy(this, GetNextShellId());

			DefaultExecutedHandler = new ExecutedRoutedEventHandler((sender, e) => ExecutedCommandHandlerImpl(sender, shell, e));
			DefaultCanExecuteHandler = new CanExecuteRoutedEventHandler((sender, e) => CanExecuteCommandHandlerImpl(sender, shell, e));

			mainResources = Application.Current.Resources;
			defaultResources = PpsWpfShell.CreateDefaultResources(shell);
		} // ctor

		Task IPpsShellServiceInit.InitAsync()
		{
			mainResources.MergedDictionaries.Add(defaultResources);

			// try load additional resources from the server
			defaultResources.Source = CreateProxyUri("wpf/styles.xaml");
			PpsWpfShell.FixDefaultKey(defaultResources); // Source clears all keys

			return Task.CompletedTask;
		} // proc IPpsShellServiceInit.InitAsync

		Task IPpsShellServiceInit.InitUserAsync()
			=> Task.CompletedTask;

		Task IPpsShellServiceInit.DoneUserAsync()
			=> Task.CompletedTask;

		Task IPpsShellServiceInit.DoneAsync()
		{
			mainResources.MergedDictionaries.Remove(defaultResources);
			return Task.CompletedTask;
		} // proc IPpsShellServiceInit.DoneAsync

		#endregion

		#region -- Command Service ----------------------------------------------------

		private static void CanExecuteCommandHandlerImpl(object _, IPpsShell shell, CanExecuteRoutedEventArgs e)
		{
			if (e.Command is PpsRoutedCommand)
			{
				var f = Keyboard.FocusedElement;
				Debug.Print($"CanExecute: {e.Command} on {(f == null ? "<null>" : f.GetType().Name)}");
			}

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

		#region -- WebRequest - Proxy -------------------------------------------------

		private static Uri RegisterWebRequestProxy(PpsWpfShellService shellService, int shellId)
		{
			var baseUri = new Uri($"http://ppsn{shellId}.local");
			WebRequest.RegisterPrefix(baseUri.ToString(), new PpsWebRequestCreate(shellService));
			return baseUri;
		} // proc RegisterWebRequestProxy

		private WebRequest CreateWebRequest(Uri uri)
		{
			if (!uri.IsAbsoluteUri)
				throw new ArgumentException("Uri must absolute.", nameof(uri));

			var absolutePath = uri.AbsolutePath;
			if (absolutePath.StartsWith("/")) // if the uri starts with "/", remove it, because the info.remoteUri is our root
				absolutePath = absolutePath.Substring(1);

			// create a relative uri
			var relativeUri = new Uri(absolutePath + uri.GetComponents(UriComponents.Query | UriComponents.KeepDelimiter, UriFormat.UriEscaped), UriKind.Relative);
			return new PpsProxyRequest(shell.Http, uri, relativeUri);
		} // func CreateWebRequest

		private Uri CreateProxyUri(string relativePath)
			=> new Uri(shellUri, relativePath);

		#endregion

		#region -- IPpsWpfResources - members -----------------------------------------

		Uri IPpsWpfResources.CreateProxyUri(string relativePath)
			=> CreateProxyUri(relativePath);

		IEnumerable<T> IPpsWpfResources.FindResourceByKey<TKEY, T>(Predicate<TKEY> predicate)
			=> mainResources.FindResourceByKey<TKEY, T>(predicate);

		T IPpsWpfResources.FindResource<T>(object resourceKey)
			=> defaultResources[resourceKey] as T;

		#endregion

		public IPpsShell Shell => shell;

		// -- Static ----------------------------------------------------------

		private static int shellCounter = 1;

		private static int GetNextShellId()
			=> shellCounter++;
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
		public ICredentials GetAutoLogin(ICredentials credentials)
		{
			if (String.IsNullOrEmpty(AutoLoginUser))
				return null;
			else if (AutoLoginUser == ".")
				return CredentialCache.DefaultCredentials;

			var autoUserName = AutoLoginUser;
			var autoPassword = AutoLoginPassword;

			// check for saved password
			var userName = PpsShell.GetUserNameFromCredentials(credentials);
			if (String.Compare(autoUserName, userName, StringComparison.OrdinalIgnoreCase) == 0 && String.IsNullOrEmpty(autoPassword))
				return credentials;
			else
				return UserCredential.Create(autoUserName, autoPassword);
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

	#region -- class PpsProxyRequest --------------------------------------------------

	/// <summary>Proxy request to implementation, that is able to switch between offline 
	/// cache and online mode.</summary>
	public sealed class PpsProxyRequest : WebRequest, IEquatable<PpsProxyRequest>
	{
		#region -- class PpsProxyResponse ---------------------------------------------

		private sealed class PpsProxyResponse : WebResponse
		{
			private readonly HttpRequestMessage request;
			private readonly HttpResponseMessage response;
			private readonly Stream responseStream;

			private readonly WebHeaderCollection headers;

			public PpsProxyResponse(HttpRequestMessage request, HttpResponseMessage response, Stream responseStream)
			{
				this.request = request ?? throw new ArgumentNullException(nameof(request));
				this.response = response ?? throw new ArgumentNullException(nameof(response));
				this.responseStream = responseStream ?? new MemoryStream();

				// todo: copy headers
			} // ctor

			protected override void Dispose(bool disposing)
			{
				responseStream.Dispose();
				response.Dispose();
				request.Dispose();
				base.Dispose(disposing);
			} // proc Dispose

			public override Stream GetResponseStream()
				=> responseStream;

			public override WebHeaderCollection Headers => headers;

			public override long ContentLength { get => responseStream.Length; set => throw new NotSupportedException(); }
			public override string ContentType { get => response.Content?.Headers.ContentType?.ToString(); set => throw new NotSupportedException(); }

			public override Uri ResponseUri => request.RequestUri;
		} // class PpsProxyResponse

		#endregion

		private readonly DEHttpClient http; // owner, that retrieves a resource
		private readonly Uri originalUri;
		private readonly Uri relativeUri; // relative Uri

		private bool aborted = false; // is the request cancelled

		private WebHeaderCollection headers;
		private readonly string path;
		private readonly NameValueCollection arguments;

		private HttpMethod method = HttpMethod.Get;
		private string contentType = null;
		private long contentLength = -1;

		private MemoryStream requestStream = null;
		private HttpWebRequest directRequest = null;

		#region -- Ctor/Dtor ----------------------------------------------------------

		internal PpsProxyRequest(DEHttpClient http, Uri originalUri, Uri relativeUri)
		{
			this.http = http ?? throw new ArgumentNullException(nameof(http));
			this.originalUri = originalUri ?? throw new ArgumentNullException(nameof(originalUri));
			this.relativeUri = relativeUri ?? throw new ArgumentNullException(nameof(relativeUri));

			if (relativeUri.IsAbsoluteUri)
				throw new ArgumentException("Uri must be relative.", nameof(relativeUri));
			if (!originalUri.IsAbsoluteUri)
				throw new ArgumentException("Uri must be absolute.", nameof(originalUri));
			
			if (relativeUri.OriginalString.StartsWith("/"))
				this.relativeUri = new Uri(relativeUri.OriginalString.Substring(1), UriKind.Relative);

			(path, arguments) = relativeUri.ParseUri();
		} // ctor

		/// <summary>Returns whether the given proxy request is for the same object</summary>
		/// <param name="other">Request to compare</param>
		/// <returns>true if equal</returns>
		public bool Equals(PpsProxyRequest other)
			=> Equals(other.relativeUri);

		/// <summary>Returns whether the Uri is equal to the given Uri</summary>
		/// <param name="otherUri">Uri to compare</param>
		/// <returns>true if equal</returns>
		public bool Equals(Uri otherUri)
			=> PpsWpfShell.EqualUri(relativeUri, otherUri);

		#endregion

		#region -- CreateHttpRequest ----------------------------------------------

		private HttpRequestMessage CreateHttpRequest()
		{
			var request = new HttpRequestMessage(method, http.CreateFullUri(relativeUri.ToString()));

			// update header
			if (headers != null)
			{
				var transferEncoding = headers[HttpRequestHeader.TransferEncoding];
				if (!String.IsNullOrEmpty(transferEncoding))
					request.Headers.TransferEncoding.ParseAdd(transferEncoding);
			}

			// request data, cached POST-Data
			if (requestStream != null)
			{
				requestStream.Position = 0;
				var content = new StreamContent(requestStream);

				// update header
				// todo:

				request.Content = content;
			}

			return request;
		} // func CreateHttpRequest

		private HttpWebRequest CreateWebRequest()
		{
			if (relativeUri.IsAbsoluteUri)
				throw new ArgumentException("Uri must be relative.", nameof(relativeUri));

			// build the remote request with absolute uri and credentials
			var absoluteUri = http.CreateFullUri(relativeUri.ToString());

			var request = CreateHttp(absoluteUri);
			request.Credentials = http.Credentials; // override the current credentials
			request.Headers.Add("des-multiple-authentifications", "true");
			request.Timeout = -1; // 600 * 1000;

			// copy basic request informationen
			request.Method = method.Method;
			if (contentLength > 0)
				request.ContentLength = contentLength;
			if (contentType != null)
				request.ContentType = contentType;

			// copy headers
			if (headers != null)
			{
				headers["ppsn-hostname"] = Environment.MachineName;
				foreach (var k in headers.AllKeys)
				{
					if (String.Compare(k, "Accept", true) == 0)
						request.Accept = headers[k];
					else
						request.Headers[k] = headers[k];
				}
			}

			return request;
		} // func CreateWebRequest

		#endregion

		#region -- GetResponse --------------------------------------------------------

		/// <summary>Handles the request async</summary>
		/// <param name="callback"></param>
		/// <param name="state"></param>
		/// <returns></returns>
		public override IAsyncResult BeginGetResponse(AsyncCallback callback, object state)
		{
			if (aborted)
				throw new WebException("Canceled", WebExceptionStatus.RequestCanceled);

			if (directRequest != null)
				return directRequest.BeginGetResponse(callback, state);
			else
				return GetResponseAsync().ToAsyncResult(callback, state);
		} // func BeginGetResponse

		/// <summary></summary>
		/// <param name="ar"></param>
		/// <returns></returns>
		public override WebResponse EndGetResponse(IAsyncResult ar)
		{
			if (directRequest != null)
				return directRequest.EndGetResponse(ar);
			else
				return PpsShell.EndAsyncResult<WebResponse>(ar);
		} // func EndGetResponse

		/// <summary>Get the response and process the request now.</summary>
		/// <returns></returns>
		public override WebResponse GetResponse()
		{
			if (directRequest != null)
				return directRequest.GetResponse();
			else
				return GetResponseAsync().Await();
		} // func GetResponse

		public async override Task<WebResponse> GetResponseAsync()
		{
			var request = CreateHttpRequest();
			HttpResponseMessage response = null;
			Stream responseStream = null;
			try
			{
				// execute request
				response = await http.SendAsync(request, HttpCompletionOption.ResponseContentRead);
				if (response.Content != null)
					responseStream = await response.Content.ReadAsStreamAsync();

				return new PpsProxyResponse(request, response, responseStream);
			}
			catch
			{
				responseStream?.Dispose();
				response?.Dispose();
				request?.Dispose();
				throw;
			}
		} // func GetResponseAsync

		#endregion

		#region -- GetRequestStream ---------------------------------------------------

		/// <summary></summary>
		/// <param name="callback"></param>
		/// <param name="state"></param>
		/// <returns></returns>
		public override IAsyncResult BeginGetRequestStream(AsyncCallback callback, object state)
			=> GetRequestStreamAsync(false).ToAsyncResult(callback, state);

		/// <summary></summary>
		/// <param name="ar"></param>
		/// <returns></returns>
		public override Stream EndGetRequestStream(IAsyncResult ar)
			=> PpsShell.EndAsyncResult<Stream>(ar);

		/// <summary>Create the request online, and now to support request streams.</summary>
		/// <returns></returns>
		public override Stream GetRequestStream()
			=> GetRequestStreamAsync(false).Await();

		/// <summary>Create the request online, and now to support request streams.</summary>
		/// <param name="sendChunked"><c>true</c>, for large data, the request is executed within the GetRequestStream and the inner request stream returned.</param>
		/// <returns></returns>
		public Task<Stream> GetRequestStreamAsync(bool sendChunked)
		{
			if (directRequest != null || requestStream != null)
				throw new InvalidOperationException("GetResponse or GetRequestStream is already invoked.");

			if (sendChunked)
			{
				directRequest = CreateWebRequest();
				if (directRequest.Method != HttpMethod.Post.Method
					&& directRequest.Method != HttpMethod.Put.Method)
					throw new ArgumentException("Only POST/PUT can use GetRequestStream in none buffering mode.");

				// stream the PUT/POST
				directRequest.SendChunked = true;
				directRequest.AllowWriteStreamBuffering = false;

				return directRequest.GetRequestStreamAsync();
			}
			else
			{
				if (requestStream == null)
					requestStream = new MemoryStream();

				// return a window stream with open end, that the memory stream is not closed.
				return Task.FromResult<Stream>(new WindowStream(requestStream, 0, -1, true, true));
			}
		} // func GetRequestStreamAsync

		#endregion

		/// <summary>Cancel the current request</summary>
		public override void Abort()
		{
			aborted = true;
			throw new NotImplementedException("todo:");
		} // proc Abort

		/// <summary>Request method</summary>
		public override string Method
		{
			get => method.Method;
			set
			{
				switch (value.ToUpperInvariant())
				{
					case "GET":
						method = HttpMethod.Get;
						break;
					case "POST":
					case "PUT":
						method = HttpMethod.Put;
						break;
					case "HEAD":
						method = HttpMethod.Head;
						break;
					default:
						throw new NotSupportedException();
				}
			}
		} // prop Method
		/// <summary>Content type of the request (mime type).</summary>
		public override string ContentType { get => contentType; set => contentType = value; }
		/// <summary>Content length, to send.</summary>
		public override long ContentLength { get => contentLength; set => contentLength = value; }

		/// <summary>Request uri.</summary>
		public override Uri RequestUri => originalUri;

		/// <summary>We do not use any proxy.</summary>
		public override IWebProxy Proxy { get => null; set { } } // avoid NotImplementedExceptions

		/// <summary>Arguments of the request</summary>
		public NameValueCollection Arguments => arguments;
		/// <summary>Relative path for the request.</summary>
		public string Path => path;

		/// <summary>Header</summary>
		public override WebHeaderCollection Headers { get => headers ?? (headers = new WebHeaderCollection()); set => headers = value; }
	} // class PpsProxyRequest

	#endregion








	#region -- class PpsShellWpf ------------------------------------------------------

	///// <summary></summary>
	//public abstract class PpsShellWpf : _PpsShell, IPpsXamlCode
	//{
	//	#region -- class InstanceKey --------------------------------------------------

	//	/// <summary>Special key to select templates.</summary>
	//	private sealed class InstanceKey<T> : ResourceKey
	//		where T : class
	//	{
	//		public InstanceKey()
	//		{
	//		} // ctor

	//		public override int GetHashCode()
	//			=> typeof(T).GetHashCode();

	//		public override bool Equals(object obj)
	//			=> obj is T;

	//		public override Assembly Assembly => null;
	//	} // class DefaultEnvironmentKeyImpl

	//	#endregion

	//	#region -- class DefaultStaticDataTemplateSelector ----------------------------

	//	private sealed class DefaultStaticDataTemplateSelector : DataTemplateSelector
	//	{
	//		public DefaultStaticDataTemplateSelector()
	//		{
	//		} // ctor

	//		public override DataTemplate SelectTemplate(object item, DependencyObject container)
	//			=> GetShell<PpsShellWpf>(container)?.GetDataTemplate(item, container);
	//	} // class DefaultStaticDataTemplateSelector

	//	#endregion

	//	#region -- class DefaultInstanceDataTemplateSelector --------------------------

	//	private sealed class DefaultInstanceDataTemplateSelector : DataTemplateSelector
	//	{
	//		private readonly PpsShellWpf shell;

	//		public DefaultInstanceDataTemplateSelector(PpsShellWpf shell)
	//		{
	//			this.shell = shell;
	//		} // ctor

	//		public override DataTemplate SelectTemplate(object item, DependencyObject container)
	//			=> shell.GetDataTemplate(item, container);
	//	} // class DefaultInstanceDataTemplateSelector

	//	#endregion

	//	private readonly Dispatcher currentDispatcher;
	//	private readonly InputManager inputManager;
	//	private readonly SynchronizationContext synchronizationContext;

	//	#region -- Ctor/Dtor ----------------------------------------------------------

	//	/// <summary></summary>
	//	/// <param name="lua"></param>
	//	/// <param name="mainResources"></param>
	//	protected PpsShellWpf(Lua lua, ResourceDictionary mainResources)
	//		: base(lua)
	//	{

	//		inputManager = InputManager.Current;
	//		currentDispatcher = Dispatcher.CurrentDispatcher ?? throw new ArgumentNullException(nameof(Dispatcher.CurrentDispatcher));
	//		synchronizationContext = new DispatcherSynchronizationContext(currentDispatcher);

	//		DefaultDataTemplateSelector = new DefaultInstanceDataTemplateSelector(this);
	//	} // ctor

	//	#endregion

	//	#region -- Wpf Resource Management --------------------------------------------

	//	/// <summary>Update a resource from a xml-source.</summary>
	//	/// <param name="xamlSource"></param>
	//	/// <returns></returns>
	//	protected async Task<object> UpdateResourceAsync(XmlReader xamlSource)
	//	{
	//		var resourceKey = (object)null;
	//		object resource;

	//		try
	//		{
	//			// create the resource
	//			using (var elementReader = await xamlSource.ReadElementAsSubTreeAsync(XmlNamespaceScope.ExcludeXml))
	//			{
	//				var startObjectCounter = 0;

	//				resource = await PpsXamlParser.LoadAsync<object>(elementReader,
	//					new PpsXamlReaderSettings()
	//					{
	//						FilterNode = (reader) =>
	//						{
	//							switch (reader.NodeType)
	//							{
	//								case XamlNodeType.GetObject:
	//								case XamlNodeType.StartObject:
	//									startObjectCounter++;
	//									goto default;
	//								case XamlNodeType.EndObject:
	//									startObjectCounter--;
	//									goto default;
	//								case XamlNodeType.StartMember:
	//									if (startObjectCounter == 1 && reader.Member == XamlLanguage.Key && resourceKey == null)
	//									{
	//										resourceKey = reader.ReadMemberValue();
	//										return reader.Read();
	//									}
	//									goto default;
	//								default:
	//									return true;
	//							}
	//						},
	//						CloseInput = false,
	//						Code = this,
	//						ServiceProvider = this
	//					}
	//				);
	//			}

	//			if (resource == null)
	//				throw Procs.CreateXmlException(xamlSource, "Resource load failed.");

	//			// check the key
	//			if (resourceKey == null)
	//			{
	//				var style = resource as Style;
	//				if (style == null)
	//					throw Procs.CreateXmlException(xamlSource, "x:Key is missing on resource.");

	//				resourceKey = style.TargetType;
	//			}
	//		}
	//		catch (XmlException) // no recovery, xml is invalid
	//		{
	//			throw;
	//		}
	//		catch (Exception e) // xaml parser error
	//		{
	//			// check resource key
	//			if (resourceKey == null // no resource key to check, do not load
	//				|| defaultResources[resourceKey] == null) // no resource, might be imported
	//				throw;
	//			else
	//			{
	//				var message = $"Could not load resource '{resourceKey}'.";
	//				if (e is XamlParseException)
	//					AppendException(e, message);
	//				else
	//					AppendException(Procs.CreateXmlException(xamlSource, message, e));

	//				return null;
	//			}
	//		}

	//		// update resource
	//		//Debug.Print("UpdateResouce: ({1}){0}", resourceKey, resourceKey.GetType().Name);
	//		defaultResources[resourceKey] = resource;
	//		return resourceKey;
	//	} // func UpdateResource

	//	/// <summary>Find the resource.</summary>
	//	/// <param name="key"></param>
	//	/// <param name="dependencyObject"></param>
	//	/// <returns></returns>
	//	[LuaMember]
	//	public object GetResource(object key, DependencyObject dependencyObject)
	//	{
	//		if (dependencyObject is FrameworkElement fe)
	//			return fe.TryFindResource(key);
	//		else if (dependencyObject is FrameworkContentElement fce)
	//			return fce.TryFindResource(key);
	//		else
	//			return defaultResources[key] ?? Application.Current.TryFindResource(key);
	//	} // func GetResource

	//	void IPpsXamlCode.CompileCode(Uri uri, string code)
	//		=> CompileCodeForXaml(this, uri, code);

	//	/// <summary></summary>
	//	/// <param name="self"></param>
	//	/// <param name="uri"></param>
	//	/// <param name="code"></param>
	//	public void CompileCodeForXaml(LuaTable self, Uri uri, string code)
	//	{
	//		var request = self as IPpsRequest ?? this;

	//		var compileTask =
	//			code != null
	//			? CompileAsync(code, uri?.OriginalString ?? "dummy.lua", true, new KeyValuePair<string, Type>("self", typeof(LuaTable)))
	//			: request.CompileAsync(uri, true, new KeyValuePair<string, Type>("self", typeof(LuaTable)));
	//		compileTask.Await().Run(self, request);
	//	} // proc CompileCode

	//	#endregion

	//	#region -- Synchronization ----------------------------------------------------

	//	/// <summary></summary>
	//	/// <param name="action"></param>
	//	public sealed override void BeginInvoke(Action action)
	//		=> Dispatcher.BeginInvoke(action, DispatcherPriority.ApplicationIdle); // must be idle, that method is invoked after the current changes

	//	/// <summary></summary>
	//	/// <param name="action"></param>
	//	/// <returns></returns>
	//	public sealed override async Task InvokeAsync(Action action)
	//		=> await Dispatcher.InvokeAsync(action);

	//	/// <summary></summary>
	//	/// <typeparam name="T"></typeparam>
	//	/// <param name="func"></param>
	//	/// <returns></returns>
	//	public sealed override async Task<T> InvokeAsync<T>(Func<T> func)
	//		=> await Dispatcher.InvokeAsync(func);

	//	/// <summary>Return context</summary>
	//	public sealed override SynchronizationContext Context => synchronizationContext;

	//	/// <summary>Wpf main thread dispatcher.</summary>
	//	[LuaMember]
	//	public Dispatcher Dispatcher => currentDispatcher;

	//	#endregion

	//	#region -- UI-Helper ----------------------------------------------------------

	//	/// <summary>Append a exception to the log.</summary>
	//	/// <param name="exception"></param>
	//	/// <param name="alternativeMessage"></param>
	//	[LuaMember(nameof(AppendException))]
	//	public void AppendException(Exception exception, string alternativeMessage = null)
	//		=> ShowException(PpsExceptionShowFlags.Background, exception, alternativeMessage);

	//	private static string GetMessageCaptionFromImage(MessageBoxImage image)
	//	{
	//		switch (image)
	//		{
	//			case MessageBoxImage.Error:
	//				return "Fehler";
	//			case MessageBoxImage.Warning:
	//				return "Warnung";
	//			case MessageBoxImage.Question:
	//				return "Frage";
	//			default:
	//				return "Information";
	//		}
	//	} // func GetMessageCaptionFromImage

	//	/// <summary>Display a simple messagebox</summary>
	//	/// <param name="text"></param>
	//	/// <param name="button"></param>
	//	/// <param name="image"></param>
	//	/// <param name="defaultResult"></param>
	//	/// <returns></returns>
	//	public virtual MessageBoxResult MsgBox(string text, MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.Information, MessageBoxResult defaultResult = MessageBoxResult.OK)
	//		=> MessageBox.Show(text, GetMessageCaptionFromImage(image), button, image, defaultResult);

	//	/// <summary>Display a simple messagebox in the main ui-thread.</summary>
	//	/// <param name="text"></param>
	//	/// <param name="button"></param>
	//	/// <param name="image"></param>
	//	/// <param name="defaultResult"></param>
	//	/// <returns></returns>
	//	public async Task<MessageBoxResult> MsgBoxAsync(string text, MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.Information, MessageBoxResult defaultResult = MessageBoxResult.OK)
	//		=> await Dispatcher.InvokeAsync(() => MsgBox(text, button, image, defaultResult));

	//	/// <summary></summary>
	//	/// <param name="message"></param>
	//	public sealed override void ShowMessage(string message)
	//		=> MsgBox(message);

	//	/// <summary></summary>
	//	/// <param name="flags"></param>
	//	/// <param name="exception"></param>
	//	/// <param name="alternativeMessage"></param>
	//	public override void ShowException(PpsExceptionShowFlags flags, Exception exception, string alternativeMessage = null)
	//	{
	//		//Log.Append(PpsLogType.Exception, exception, alternativeMessage);

	//		if ((flags & PpsExceptionShowFlags.Background) != PpsExceptionShowFlags.Background)
	//		{
	//			// show message simple
	//			MsgBox(alternativeMessage ?? exception.UnpackException().ToString(), MessageBoxButton.OK, MessageBoxImage.Error);

	//			// shutdown application
	//			if ((flags & PpsExceptionShowFlags.Shutown) == PpsExceptionShowFlags.Shutown)
	//				Application.Current.Shutdown(1);
	//		}
	//	} // proc ShowException

	//	/// <summary>Return a data template for the object.</summary>
	//	/// <param name="data"></param>
	//	/// <param name="container"></param>
	//	/// <returns></returns>
	//	public abstract DataTemplate GetDataTemplate(object data, DependencyObject container);

	//	#endregion

	//	#region -- Lua-Helper ---------------------------------------------------------

	//	/// <summary>Show a simple message box.</summary>
	//	/// <param name="text"></param>
	//	/// <param name="button"></param>
	//	/// <param name="image"></param>
	//	/// <param name="defaultResult"></param>
	//	/// <returns></returns>
	//	[LuaMember("msgbox")]
	//	private MessageBoxResult LuaMsgBox(string text, MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.Information, MessageBoxResult defaultResult = MessageBoxResult.OK)
	//		=> MsgBox(text, button, image, defaultResult);

	//	/// <summary></summary>
	//	/// <param name="value"></param>
	//	/// <param name="targetType"></param>
	//	/// <returns></returns>
	//	[LuaMember("ToNumberUI")]
	//	public object LuaToNumber(object value, Type targetType)
	//	{
	//		if (targetType == null)
	//			throw new ArgumentNullException(nameof(targetType));

	//		var r = PpsConverter.NumericValue.ConvertBack(value, targetType, null, CultureInfo.CurrentUICulture);
	//		if (r is ValidationResult
	//			|| r == DependencyProperty.UnsetValue)
	//			return null;
	//		return r;
	//	} // func LuaToNumber

	//	/// <summary></summary>
	//	/// <param name="value"></param>
	//	/// <returns></returns>
	//	[LuaMember("ToStringUI")]
	//	public new object LuaToString(object value)
	//	{
	//		var r = PpsConverter.NumericValue.Convert(value, typeof(string), null, CultureInfo.CurrentUICulture);
	//		if (r is ValidationResult
	//			|| r == DependencyProperty.UnsetValue)
	//			return null;
	//		return r;
	//	} // func LuaToString

	//	/// <summary>Helper to await a async process.</summary>
	//	/// <param name="func"></param>
	//	/// <returns></returns>
	//	protected sealed override LuaResult LuaAwaitFunc(object func)
	//	{
	//		if (func is DispatcherOperation o)
	//		{
	//			LuaAwait(o.Task);
	//			return LuaResult.Empty;
	//		}
	//		return base.LuaAwaitFunc(func);
	//	} // func LuaAwaitFunc

	//	#endregion

	//	/// <summary>Default is utf-8</summary>
	//	public sealed override Encoding Encoding => Encoding.UTF8;

	//	/// <summary>Lua ui-wpf framwework.</summary>
	//	[LuaMember("UI")]
	//	public LuaUI LuaUI { get; } = new LuaUI();

	//	// -- Static ----------------------------------------------------------

	//	private readonly static ResourceDictionary staticDefaultResources;

	//	static PpsShellWpf()
	//	{
	//		staticDefaultResources = new ResourceDictionary() { Source = new Uri("/PPSn.Desktop.UI;component/UI/UI.xaml", UriKind.Relative) };
	//	} // ctor

	//	#region -- GetShell, GetCurrentPane -------------------------------------------

	//	/// <summary>Get the environment, that is attached to the current element.</summary>
	//	/// <param name="dependencyObject"></param>
	//	/// <returns></returns>
	//	public static PpsShellWpf GetShell(DependencyObject dependencyObject = null)
	//		=> GetShell<PpsShellWpf>(dependencyObject);

	//	/// <summary>Get the environment, that is attached to the current element.</summary>
	//	/// <param name="dependencyObject"></param>
	//	/// <returns></returns>
	//	/// <typeparam name="T"></typeparam>
	//	public static T GetShell<T>(DependencyObject dependencyObject = null)
	//		where T : PpsShellWpf
	//	{
	//		switch (dependencyObject)
	//		{
	//			case null:
	//				goto default;
	//			case FrameworkElement fe:
	//				return (T)fe.TryFindResource(DefaultEnvironmentKey) ?? GetShell<T>(null);
	//			case FrameworkContentElement fce:
	//				return (T)fce.TryFindResource(DefaultEnvironmentKey) ?? GetShell<T>(null);
	//			default:
	//				return (T)Application.Current.TryFindResource(DefaultEnvironmentKey);
	//		}
	//	} // func GetShell

	//	private static T GetCurrentPaneCore<T>(DependencyObject dependencyObject)
	//		where T : class, IPpsWindowPane
	//	{
	//		switch (dependencyObject)
	//		{
	//			case null:
	//				return default(T);
	//			case FrameworkElement fe:
	//				return (T)fe.TryFindResource(CurrentWindowPaneKey);
	//			case FrameworkContentElement fce:
	//				return (T)fce.TryFindResource(CurrentWindowPaneKey);
	//			default:
	//				return default(T);
	//		}
	//	} // func GetCurrentPaneCore

	//	/// <summary>Get the current pane from the focused element.</summary>
	//	/// <returns></returns>
	//	public static IPpsWindowPane GetCurrentPane()
	//		=> GetCurrentPane<IPpsWindowPane>();

	//	/// <summary>Get the current pane from the focused element.</summary>
	//	/// <returns></returns>
	//	/// <typeparam name="T"></typeparam>
	//	public static T GetCurrentPane<T>()
	//		where T : class, IPpsWindowPane
	//		=> GetCurrentPaneCore<T>(Keyboard.FocusedElement as DependencyObject);

	//	/// <summary>Get the environment, that is attached to the current element.</summary>
	//	/// <param name="dependencyObject"></param>
	//	/// <returns></returns>
	//	public static IPpsWindowPane GetCurrentPane(DependencyObject dependencyObject)
	//		=> GetCurrentPane<IPpsWindowPane>(dependencyObject);

	//	/// <summary>Get the environment, that is attached to the current element.</summary>
	//	/// <param name="dependencyObject"></param>
	//	/// <returns></returns>
	//	/// <typeparam name="T"></typeparam>
	//	public static T GetCurrentPane<T>(DependencyObject dependencyObject)
	//		where T : class, IPpsWindowPane
	//		=> GetCurrentPaneCore<T>(dependencyObject) ?? GetCurrentPane<T>();

	//	/// <summary>Resource key for the environment.</summary>
	//	public static ResourceKey DefaultEnvironmentKey { get; } = new InstanceKey<PpsShellWpf>();
	//	/// <summary>Resource key for the current pane.</summary>
	//	public static ResourceKey CurrentWindowPaneKey { get; } = new InstanceKey<IPpsWindowPane>();
	//	/// <summary>Template selection, that redirects to the GetDataTemplate function.</summary>
	//	public static DataTemplateSelector StaticDataTemplateSelector => new DefaultStaticDataTemplateSelector();

	//	/// <summary></summary>
	//	public DataTemplateSelector DefaultDataTemplateSelector { get; }

	//	#endregion
	//} // class PpsShellWpf

	#endregion
}
