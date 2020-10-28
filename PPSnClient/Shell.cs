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
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Data;
using TecWare.DE.Networking;
using TecWare.DE.Stuff;
using TecWare.PPSn.Core.Data;
using TecWare.PPSn.Data;
using TecWare.PPSn.UI;

namespace TecWare.PPSn
{
	#region -- struct PpsShellGetList -------------------------------------------------

	/// <summary>Define all parameter of the ppsn viewget command.</summary>
	[Obsolete("PpsDataQuery")]
	public sealed class PpsShellGetList
	{
		/// <summary></summary>
		/// <param name="viewId"></param>
		public PpsShellGetList(string viewId)
		{
			ViewId = viewId;
		} // ctor

		/// <summary>Copy parameter.</summary>
		/// <param name="copy"></param>
		public PpsShellGetList(PpsShellGetList copy)
		{
			if (copy == null)
				copy = Empty;

			ViewId = copy.ViewId;
			Filter = copy.Filter;
			Order = copy.Order;
			Start = copy.Start;
			Count = copy.Count;
		} // ctor

		private StringBuilder ToString(StringBuilder sb)
		{
			sb.Append("v=");
			sb.Append(ViewId);

			if (Filter != null && Filter != PpsDataFilterExpression.True)
				sb.Append("&f=").Append(Uri.EscapeDataString(Filter.ToString()));
			if (Columns != null && Columns.Length > 0)
				sb.Append("&r=").Append(PpsDataColumnExpression.ToString(Columns));
			if (Order != null && Order.Length > 0)
				sb.Append("&o=").Append(Uri.EscapeDataString(PpsDataOrderExpression.ToString(Order)));
			if (Start != -1)
				sb.Append("&s=").Append(Start);
			if (Count != -1)
				sb.Append("&c=").Append(Count);
			if (!String.IsNullOrEmpty(AttributeSelector))
				sb.Append("&a=").Append(AttributeSelector);

			return sb;
		} // func ToString

		/// <summary>Gets a uri-style query string for the properties.</summary>
		/// <returns></returns>
		public override string ToString()
			=> ToString(new StringBuilder()).ToString();

		/// <summary>Create request path.</summary>
		/// <param name="path"></param>
		/// <returns></returns>
		public string ToQuery(string path = null)
			=> ToString(new StringBuilder((path ?? String.Empty) + "?action=viewget&")).ToString();

		/// <summary>View to select.</summary>
		public string ViewId { get; }
		/// <summary>Columns to return</summary>
		public PpsDataColumnExpression[] Columns { get; set; }
		/// <summary>Filter</summary>
		public PpsDataFilterExpression Filter { get; set; }
		/// <summary>Row order</summary>
		public PpsDataOrderExpression[] Order { get; set; }
		/// <summary>Pagination</summary>
		public int Start { get; set; } = -1;
		/// <summary>Pagination</summary>
		public int Count { get; set; } = -1;
		/// <summary>Attribute</summary>
		public string AttributeSelector { get; set; } = String.Empty;

		/// <summary>Empty parameter.</summary>
		public bool IsEmpty => String.IsNullOrEmpty(ViewId);

		/// <summary>Parse string representation from ToString.</summary>
		/// <param name="data"></param>
		/// <param name="list"></param>
		/// <returns></returns>
		public static bool TryParse(string data, out PpsShellGetList list)
		{
			var arguments = HttpUtility.ParseQueryString(data, Encoding.UTF8);
			var viewId = arguments["v"];
			if (String.IsNullOrEmpty(viewId))
			{
				list = null;
				return false;
			}

			list = new PpsShellGetList(viewId);

			var f = arguments["f"];
			if (!String.IsNullOrEmpty(f))
				list.Filter = PpsDataFilterExpression.Parse(f);

			var l = arguments["l"];
			if (!String.IsNullOrEmpty(l))
				list.Columns = PpsDataColumnExpression.Parse(l).ToArray();

			var o = arguments["o"];
			if (!String.IsNullOrEmpty(o))
				list.Order = PpsDataOrderExpression.Parse(o).ToArray();

			var s = arguments["s"];
			if (!String.IsNullOrEmpty(s))
				list.Start = Int32.Parse(s);
			var c = arguments["c"];
			if (!String.IsNullOrEmpty(c))
				list.Count = Int32.Parse(c);

			var a = arguments["a"];
			if (!String.IsNullOrEmpty(a))
				list.AttributeSelector = a;

			return true;
		} // func TryParse

		/// <summary>Parse string representation from ToString.</summary>
		/// <param name="data"></param>
		/// <returns></returns>
		public static PpsShellGetList Parse(string data)
			=> TryParse(data, out var t) ? t : throw new FormatException();

		/// <summary>Representation of an empty selector.</summary>
		public static PpsShellGetList Empty { get; } = new PpsShellGetList((string)null);
	} // class PpsShellGetList

	#endregion

	#region -- interface IPpsRequest --------------------------------------------------

	/// <summary>Defines a new base for a request. The uri </summary>
	public interface IPpsRequest
	{
		/// <summary>Implementes a redirect for the request call.</summary>
		DEHttpClient Request { get; }

		/// <summary>Get the assigned environment for the request.</summary>
		_PpsShell Shell { get; }
	} // interface IPpsRequest

	#endregion

	#region -- class PpsShell ---------------------------------------------------------

	/// <summary>Basic application environment, that is used by this library.
	/// - Implements a basic Lua execution environment
	/// - Server access model
	/// - Basic code execution paths</summary>
	public abstract partial class _PpsShell : LuaGlobal, IPpsRequest, IPpsProgressFactory, IServiceProvider, IDisposable
	{
		#region -- class PpsDummyProgress ---------------------------------------------

		private sealed class PpsDummyProgress : IPpsProgress
		{
			public PpsDummyProgress()
			{
			} // ctor

			public void Dispose()
			{
			} // proc Dispose

			public void Report(string text) { }

			public int Value { get; set; }
			public string Text { get; set; }
		} // class PpsDummyProgress

		#endregion

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary>Initialize shell.</summary>
		/// <param name="lua"></param>
		protected _PpsShell(Lua lua)
			: base(lua)
		{
		} // ctor

		/// <summary></summary>
		public void Dispose()
		{
			Dispose(true);
		} // proc Dispose

		/// <summary></summary>
		/// <param name="disposing"></param>
		protected virtual void Dispose(bool disposing)
		{
			if (disposing)
			{
				services.ForEach(serv => (serv as IDisposable)?.Dispose());
			}
		} // proc Dispose

		/// <summary></summary>
		/// <returns></returns>
		public override string ToString()
			=> GetType().Name;

		#endregion

		#region -- Synchronization ----------------------------------------------------

		/// <summary>Synchronization with UI, do not wait for the return.</summary>
		/// <param name="action"></param>
		public virtual void BeginInvoke(Action action)
			=> Context.Post(new SendOrPostCallback(o => ((Action)o)()), action);

		/// <summary>Synchronization with UI, async/await.</summary>
		/// <param name="action"></param>
		/// <returns></returns>
		public abstract Task InvokeAsync(Action action);
		/// <summary>Synchronization with UI, async/await.</summary>
		/// <typeparam name="T">Return type of the async function.</typeparam>
		/// <param name="func">Function that will executed in the ui context.</param>
		/// <returns></returns>
		public abstract Task<T> InvokeAsync<T>(Func<T> func);

		/// <summary>Synchronization with UI</summary>
		public abstract SynchronizationContext Context { get; }

		#endregion

		#region -- RunAsync -----------------------------------------------------------

		private Task RunBackgroundInternal(Func<Task> task, string name, CancellationToken cancellationToken)
			=> new PpsSingleThreadSynchronizationContext(name, cancellationToken, task).Task;

		/// <summary>Creates a new execution thread for the function in the background.</summary>
		/// <param name="task">Action to run.</param>
		/// <param name="name">name of the background thread</param>
		/// <param name="cancellationToken">cancellation option</param>
		public Task RunAsync(Func<Task> task, string name, CancellationToken cancellationToken)
			=> RunBackgroundInternal(task, name, cancellationToken);

		/// <summary>Creates a new execution thread for the function in the background.</summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="task"></param>
		/// <param name="name"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public async Task<T> RunAsync<T>(Func<Task<T>> task, string name, CancellationToken cancellationToken)
		{
			var returnValue = default(T);
			await RunBackgroundInternal(async () => returnValue = await task(), name, cancellationToken);
			return returnValue;
		} // proc RunTaskBackground

		/// <summary></summary>
		/// <param name="task"></param>
		/// <returns></returns>
		public Task RunAsync(Func<Task> task)
			=> RunAsync(task, "Worker", CancellationToken.None);

		/// <summary></summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="task"></param>
		/// <returns></returns>
		public Task<T> RunAsync<T>(Func<Task<T>> task)
			=> RunAsync(task, "Worker", CancellationToken.None);

		#endregion

		#region -- Await --------------------------------------------------------------

		/// <summary>Await for a task</summary>
		/// <param name="task"></param>
		public abstract void Await(Task task);

		/// <summary>Await for a task</summary>
		/// <param name="task"></param>
		/// <returns></returns>
		public virtual T Await<T>(Task<T> task)
		{
			Await((Task)task);
			return task.Result;
		} // func Await

		#endregion

		#region -- ShowException, ShowMessage -----------------------------------------

		/// <summary>Return a dummy progress.</summary>
		/// <param name="blockUI"></param>
		/// <returns></returns>
		public IPpsProgress CreateProgress(bool blockUI = true)
			=> CreateProgressCore(blockUI) ?? EmptyProgress;

		/// <summary>Return a dummy progress.</summary>
		/// <param name="blockUI"></param>
		/// <returns></returns>
		protected abstract IPpsProgress CreateProgressCore(bool blockUI);

		/// <summary>Display the exception dialog.</summary>
		/// <param name="exception"></param>
		/// <param name="alternativeMessage"></param>
		[LuaMember(nameof(ShowException))]
		public void ShowException(Exception exception, string alternativeMessage = null)
			=> ShowException(PpsExceptionShowFlags.None, exception, alternativeMessage);

		/// <summary>Display the exception dialog in the main ui-thread.</summary>
		/// <param name="exception"></param>
		/// <param name="alternativeMessage"></param>
		/// <returns></returns>
		[LuaMember(nameof(ShowExceptionAsync))]
		public Task ShowExceptionAsync(Exception exception, string alternativeMessage = null)
			=> ShowExceptionAsync(PpsExceptionShowFlags.None, exception, alternativeMessage);

		/// <summary>Notifies a exception in the UI context.</summary>
		/// <param name="flags"></param>
		/// <param name="exception"></param>
		/// <param name="alternativeMessage"></param>
		public abstract void ShowException(PpsExceptionShowFlags flags, Exception exception, string alternativeMessage = null);

		/// <summary>Notifies a exception in the UI context.</summary>
		/// <param name="flags"></param>
		/// <param name="exception"></param>
		/// <param name="alternativeMessage"></param>
		/// <returns></returns>
		public virtual Task ShowExceptionAsync(PpsExceptionShowFlags flags, Exception exception, string alternativeMessage = null)
			=> InvokeAsync(() => ShowException(flags, exception, alternativeMessage));

		/// <summary></summary>
		/// <param name="message"></param>
		/// <returns></returns>
		[LuaMember]
		public abstract void ShowMessage(string message);

		/// <summary></summary>
		/// <param name="message"></param>
		/// <returns></returns>
		[LuaMember]
		public virtual Task ShowMessageAsync(string message)
			=> InvokeAsync(() => ShowMessage(message));

		#endregion

		#region -- Http ---------------------------------------------------------------

		/// <summary>Returns a http-client.</summary>
		/// <param name="uri">Relative path from the base uri.</param>
		/// <returns></returns>
		public abstract DEHttpClient CreateHttp(Uri uri = null);

		/// <summary>Request a list from the shell client.</summary>
		/// <param name="arguments"></param>
		/// <returns></returns>
		public abstract IEnumerable<IDataRow> GetViewData(PpsDataQuery arguments);

		/// <summary></summary>
		[LuaMember]
		public abstract DEHttpClient Request { get; }

		#endregion

		#region -- Services -----------------------------------------------------------

		private readonly List<object> services = new List<object>();

		/// <summary>Register Service to the environment root.</summary>
		/// <param name="key"></param>
		/// <param name="service"></param>
		public void RegisterService(string key, object service)
		{
			if (services.Exists(c => c.GetType() == service.GetType()))
				throw new InvalidOperationException(nameof(service));
			if (ContainsKey(key))
				throw new InvalidOperationException(nameof(key));

			// dynamic interface
			this[key] = service;

			// static interface
			services.Add(service);
		} // proc RegisterService

		/// <summary>Returns a service.</summary>
		/// <param name="serviceType"></param>
		/// <returns></returns>
		public virtual object GetService(Type serviceType)
		{
			foreach (var service in services)
			{
				var r = (service as IServiceProvider)?.GetService(serviceType);
				if (r != null)
					return r;
				else if (serviceType.IsAssignableFrom(service.GetType()))
					return service;
			}

			if (serviceType.IsAssignableFrom(GetType()))
				return this;

			return null;
		} // func GetService

		#endregion

		/// <summary>Returns the default Encoding for the Application.</summary>
		public abstract Encoding Encoding { get; }

		_PpsShell IPpsRequest.Shell => this;

		#region -- Current Shell Management -------------------------------------------

		private static _PpsShell currentShell = null;

		/// <summary></summary>
		/// <param name="shell"></param>
		public static void SetShell(_PpsShell shell)
		{
			if (currentShell != null)
				throw new ArgumentException();

			currentShell = shell;
		} // proc SetShell

		/// <summary>Returns the current shell.</summary>
		/// <param name="shell"></param>
		/// <returns></returns>
		public static bool TryGetShell(out _PpsShell shell)
			=> (shell = currentShell) != null;

		/// <summary>Returns the current shell.</summary>
		/// <returns></returns>
		public static _PpsShell GetShell()
			=> TryGetShell(out var shell) ? shell : throw new ArgumentException("For this environment is no shell definied.");

		/// <summary>Returns the current shell.</summary>
		public static _PpsShell Current => TryGetShell(out var shell) ? shell : null;

		#endregion

		/// <summary>Empty progress bar implementation</summary>
		public static IPpsProgress EmptyProgress { get; } = new PpsDummyProgress();
	} // class PpsShell

	#endregion

	#region -- class LuaShellTable ----------------------------------------------------

	/// <summary>Connects the current table with the shell.</summary>
	public class LuaShellTable : LuaTable
	{
		private readonly _PpsShell shell;
		private readonly LuaShellTable parentShellTable;

		private readonly Dictionary<string, Action> onPropertyChanged = new Dictionary<string, Action>();

		/// <summary></summary>
		/// <param name="parentTable"></param>
		public LuaShellTable(LuaShellTable parentTable)
		{
			this.shell = parentTable.Shell;
			this.parentShellTable = parentTable;
		} // ctor

		/// <summary></summary>
		/// <param name="shell"></param>
		public LuaShellTable(_PpsShell shell)
		{
			this.shell = shell;
			this.parentShellTable = null;
		} // ctor

		/// <summary></summary>
		/// <returns></returns>
		public override string ToString()
			=> GetType().Name;

		/// <summary></summary>
		/// <param name="key"></param>
		/// <returns></returns>
		protected override object OnIndex(object key)
			=> base.OnIndex(key) ?? ((LuaTable)parentShellTable ?? shell).GetValue(key);

		/// <summary></summary>
		/// <param name="propertyName"></param>
		protected override void OnPropertyChanged(string propertyName)
		{
			if (onPropertyChanged.TryGetValue(propertyName, out var a) && a != null)
				a();
			base.OnPropertyChanged(propertyName);
		} // proc OnPropertyChganged

		/// <summary></summary>
		/// <param name="propertyName"></param>
		/// <param name="onChanged"></param>
		[LuaMember]
		public void OnPropertyChangedListener(string propertyName, Action onChanged = null)
		{
			if (onChanged == null)
				onPropertyChanged.Remove(propertyName);
			else
				onPropertyChanged[propertyName] = onChanged;
		} // proc OnPropertyChangedListener

		/// <summary>Helper to set a declared member with an new value. If the value is changed OnPropertyChanged will be invoked.</summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="m">Field that to set.</param>
		/// <param name="n">Value for the field.</param>
		/// <param name="propertyName">Name of the property.</param>
		protected void SetDeclaredMember<T>(ref T m, T n, string propertyName)
		{
			if (!Equals(m, n))
			{
				m = n;
				OnPropertyChanged(propertyName);
			}
		} // proc SetDeclaredMember

		/// <summary>Optional parent table.</summary>
		[LuaMember]
		public LuaShellTable Parent => parentShellTable;
		/// <summary>Access to the current environemnt.</summary>
		[LuaMember("Environment")]
		public _PpsShell Shell => shell;
	} // class LuaShellTable

	#endregion

	#region -- class PpsShellExtensions -----------------------------------------------

	/// <summary></summary>
	public static class PpsShellExtensions
	{
		/// <summary>Set the progress bar.</summary>
		/// <param name="progress">Progress bar</param>
		/// <param name="value">Value to set</param>
		/// <param name="minimum">Minium of the value.</param>
		/// <param name="maximum">Maximum of the value.</param>
		public static void UpdateProgress(this IPpsProgress progress, int value, int minimum, int maximum)
		{
			if (minimum != -1 && maximum != -1)
				value = (value - minimum) * 1000 / (maximum - minimum);

			int newValue;
			if (value == -1)
				newValue = -1;
			else if (value < -1)
				newValue = 0;
			else if (value > 1000)
				newValue = 1000;
			else
				newValue = value;

			progress.Value = newValue;
		} // proc UpdateProgress

		/// <summary>Unpack exceptions</summary>
		/// <param name="exception"></param>
		/// <returns></returns>
		public static Exception UnpackException(this Exception exception)
			=> exception is AggregateException agg
				? UnpackException(agg.InnerException)
				: exception;

		#region -- CompileAsync -------------------------------------------------------

		/// <summary>Load an compile the file from a remote source.</summary>
		/// <param name="request"></param>
		/// <param name="source">Source uri</param>
		/// <param name="throwException">Throw an exception on fail</param>
		/// <param name="arguments">Argument definition for the chunk.</param>
		/// <returns>Compiled chunk</returns>
		public static async Task<LuaChunk> CompileAsync(this IPpsRequest request, Uri source, bool throwException, params KeyValuePair<string, Type>[] arguments)
		{
			if (request == null)
				throw new ArgumentNullException(nameof(request));
			if (source == null)
				throw new ArgumentNullException(nameof(source));

			try
			{
				using (var r = await request.Request.GetResponseAsync(source.ToString(), null))
				{
					var contentDisposition = r.GetContentDisposition();
					using (var sr = await r.GetTextReaderAsync(MimeTypes.Text.Plain))
						return await request.Shell.CompileAsync(sr, contentDisposition.FileName.Trim('"'), throwException, arguments);
				}
			}
			catch (Exception e)
			{
				if (throwException)
					throw;
				else if (e is LuaParseException) // alread handled
					return null;
				else
				{
					await request.Shell.ShowExceptionAsync(PpsExceptionShowFlags.Background, e, $"Compile failed for {source}.");
					return null;
				}
			}
		} // func CompileAsync

		#endregion

		/// <summary></summary>
		/// <param name="task"></param>
		/// <param name="shell"></param>
		public static void SpawnTask(this Task task, _PpsShell shell)
			=> task.ContinueWith(t => shell.ShowException(t.Exception), TaskContinuationOptions.OnlyOnFaulted);

		/// <summary></summary>
		/// <param name="task"></param>
		/// <param name="shell"></param>
		public static void SpawnTask(this Task task, IPpsShell shell)
			=> task.ContinueWith(t => shell.GetService<IPpsUIService>(true).ShowException(t.Exception), TaskContinuationOptions.OnlyOnFaulted);
	} // class PpsShellExtensions

	#endregion
}
