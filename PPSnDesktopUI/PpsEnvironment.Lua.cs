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
#if DEBUG
#define _DEBUG_LUATASK
#endif
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using System.Xml;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Data;
using TecWare.DE.Networking;
using TecWare.DE.Stuff;
using TecWare.PPSn.Controls;
using TecWare.PPSn.Data;
using TecWare.PPSn.Stuff;
using TecWare.PPSn.UI;
using static TecWare.PPSn.StuffUI;

namespace TecWare.PPSn
{
	#region -- interface IPpsLuaTaskParent --------------------------------------------

	/// <summary></summary>
	public interface IPpsLuaTaskParent
	{
		/// <summary></summary>
		/// <param name="flags"></param>
		/// <param name="exception"></param>
		/// <param name="alternativeMessage"></param>
		/// <returns></returns>
		Task ShowExceptionAsync(ExceptionShowFlags flags, Exception exception, string alternativeMessage = null);

		/// <summary>Returns the dispatcher fo the UI thread</summary>
		Dispatcher Dispatcher { get; }
		/// <summary>Returns the trace sink.</summary>
		PpsTraceLog Traces { get; }
	} // interface IPpsLuaTaskParent

	#endregion

	#region -- interface IPpsLuaRequest -----------------------------------------------

	/// <summary></summary>
	public interface IPpsLuaRequest
	{
		/// <summary>Implementes a redirect for the request call.</summary>
		BaseWebRequest Request { get; }
	} // interface IPpsLuaRequest

	#endregion

	#region -- class PpsLuaTask -------------------------------------------------------

	/// <summary>Task wrapper for the lua script code</summary>
	public sealed class PpsLuaTask : IDisposable
	{
		#region -- class PpsLuaTaskSynchronizationContext --------------------------------

		private sealed class PpsLuaTaskSynchronizationContext : PpsSynchronizationContext, IDisposable
		{
			private WeakReference<PpsLuaTask> task;
			private Thread queueThread;
			
			public PpsLuaTaskSynchronizationContext(PpsLuaTask luaTask)
			{
				this.task = new WeakReference<PpsLuaTask>(luaTask);
				luaTask.cancellationToken.Register(Dispose);

				ThreadPool.QueueUserWorkItem(ExecuteMessageLoop);
			} // ctor

			public void Dispose()
				=> Stop();

			private void ExecuteMessageLoop(object state)
			{
				var oldContext = Current;
				SetSynchronizationContext(this);

				// mark the thread
				this.queueThread = Thread.CurrentThread;

				try
				{
					// execute tasks in this thread
					ProcessMessageLoopUnsafe(CancellationToken.None);
				}
				catch (Exception e)
				{
					if (task.TryGetTarget(out var t))
						t.SetException(e);
					else
						throw;
				}
				finally
				{
					SetSynchronizationContext(oldContext);
				}
			} // proc ExecuteMessageLoop

			protected override Thread QueueThread => queueThread;
		} // class PpsLuaTaskSynchronizationContext

		#endregion

		private readonly IPpsLuaTaskParent parent;
		private readonly CancellationToken cancellationToken;
		private readonly CancellationTokenSource cancellationTokenSource;
		private readonly SynchronizationContext context;

		private int? threadId = null;

		private readonly Queue<object> continueTasks = new Queue<object>();
		private object onExceptionTask = null;
		private object onFinallyTask = null;
		private TaskCompletionSource<LuaResult> onAwaitTask = null;
		
		private readonly object executionLock = new object();
		private bool isQueuedTaskRunning = false;
		private LuaResult currentResult;
		private Exception currentException = null;
		private int isDisposed = 0;
		
		internal PpsLuaTask(IPpsLuaTaskParent parent, SynchronizationContext context, CancellationToken cancellationToken, LuaResult startArguments)
		{
			this.parent = parent;
			this.cancellationToken = cancellationToken;
			// the synchronization context must not have parallelity, we enforce this with the thread id
			this.context = context ?? new PpsLuaTaskSynchronizationContext(this);
			this.currentResult = startArguments;
		} // ctor

		internal PpsLuaTask(IPpsLuaTaskParent parent, SynchronizationContext context, CancellationTokenSource cancellationTokenSource, LuaResult startArguments)
			: this(parent, context, cancellationTokenSource?.Token ?? CancellationToken.None, startArguments)
		{
			this.cancellationTokenSource = cancellationTokenSource;
		} // ctor

		/// <summary></summary>
		~PpsLuaTask()
		{
			if (isDisposed == 0)
			{
				isDisposed = -1;
				DisposeContext();
			}
		} // dtor

		void IDisposable.Dispose()
			=> Await();

		private void DisposeContext()
		{
			if (context is PpsLuaTaskSynchronizationContext ctx)
				ctx.Dispose();
		} // proc DisposeContext

		private void CheckDisposed()
		{
			if (isDisposed != 0)
				throw new ObjectDisposedException(nameof(PpsLuaTask));
		} // proc CheckDisposed

		private void VerifyThreadAccess()
		{
			if (threadId.HasValue)
			{
				if (threadId.Value != Thread.CurrentThread.ManagedThreadId)
					throw new InvalidOperationException($"Task can only run in one context (threadid {threadId.Value} and continued in thread {Thread.CurrentThread.ManagedThreadId})");
			}
			else
				threadId = Thread.CurrentThread.ManagedThreadId;
		} // proc VerifyThreadAccess

		private LuaResult Invoke(object func, params object[] args)
		{
			switch(func)
			{
				case null:
					throw new ArgumentException(nameof(func));
				default:
					return new LuaResult(Lua.RtInvoke(func, args));
			}
		} // proc Invoke
		
		private void SetException(Exception e)
		{
			if (currentException != null)
			{
				parent.ShowExceptionAsync(ExceptionShowFlags.None, e, "Handler failed.").AwaitTask();
				return;
			} 

			currentException = e ?? throw new ArgumentNullException(nameof(e));
			parent.Traces.AppendException(e);
		} // proc SetException

		private void ExecuteTask(object continueWith)
		{
			isQueuedTaskRunning = true;
			try
			{
				// check for cancel or exception
				if (!IsCanceled && !IsFaulted)
				{
					// execute task
					try
					{
						currentResult = Invoke(continueWith, currentResult.Values);
					}
					catch (TaskCanceledException)
					{
						if (!cancellationToken.IsCancellationRequested)
							cancellationTokenSource?.Cancel();
					}
					catch (Exception e)
					{
						SetException(e);
					}
				}
			}
			finally
			{
				isQueuedTaskRunning = false;
			}

			if (continueTasks.Count > 0 && !IsFaulted)
			{
				ExecuteOrQueueTask(continueTasks.Dequeue());
			}
			else
			{
				// call exception
				if (onExceptionTask != null)
					ExecuteOnExceptionTask();

				// call finally
				if (onFinallyTask != null)
					ExecuteOnFinallyTask();

				// call awaitTask
				if (onAwaitTask != null)
					ExecuteOnAwaitTask();
			}
		} // proc ExecuteTask

		private void ExecuteOrQueueTask(object continueWith)
		{
			VerifyThreadAccess();

			if (isQueuedTaskRunning)
				continueTasks.Enqueue(continueWith);
			else if(!IsFaulted && !IsCanceled)
				ExecuteTask(continueWith);
		} // proc AddContinueTask

		private void ExecuteOrQueueOnExceptionTask(object state)
		{
			VerifyThreadAccess();

			if (!isQueuedTaskRunning && currentException != null)
				ExecuteOnExceptionTask();
		} // proc ExecuteOnExceptionTask

		private void ExecuteOnExceptionTask()
			=> Invoke(onExceptionTask, currentException.GetInnerException());

		private void ExecuteOrQueueOnFinallyTask(object state)
		{
			VerifyThreadAccess();

			if (!isQueuedTaskRunning)
				ExecuteOnFinallyTask();
		} // proc ExecuteOrQueueOnFinallyTask

		private void ExecuteOnFinallyTask()
		{
			switch (onFinallyTask)
			{
				case null:
					throw new ArgumentNullException(nameof(onFinallyTask));
				case IDisposable d:
					d.Dispose();
					return;
				default:
					Invoke(onFinallyTask);
					break;
			}
		} // proc ExecuteOnFinallyTask

		private void ExecuteOrQueueAwaitTask(object state)
		{
			VerifyThreadAccess();
			
			if (!isQueuedTaskRunning)
				ExecuteOnAwaitTask();
		} // proc ExecuteOrQueueAwaitTask

		private void ExecuteOnAwaitTask()
		{
			// check if already set
			var state = onAwaitTask.Task.Status;
			if (state == TaskStatus.RanToCompletion
				|| state == TaskStatus.Faulted
				|| state == TaskStatus.Canceled)
				return;

			// check if dispoose is pending
			if (Interlocked.CompareExchange(ref isDisposed, -1, isDisposed) != 0)
				return;
			// do not call dtor
			GC.SuppressFinalize(this);

			// set final state
			if (cancellationToken.IsCancellationRequested)
				ThreadPool.QueueUserWorkItem(s => onAwaitTask.SetCanceled());
			else if (IsFaulted)
				ThreadPool.QueueUserWorkItem(s => onAwaitTask.SetException((Exception)s), currentException);
			else
				ThreadPool.QueueUserWorkItem(s => onAwaitTask.SetResult((LuaResult)s), currentResult);
						
			// dispose context
			DisposeContext();
		} // proc ExecuteOnAwaitTask

		/// <summary>Append a action to the current task.</summary>
		/// <param name="continueWith"></param>
		/// <returns></returns>
		public PpsLuaTask Continue(object continueWith)
		{
			CheckDisposed();

			lock (executionLock)
			{
				if (onExceptionTask != null || onFinallyTask != null || onAwaitTask != null)
					throw new InvalidOperationException($"The execution thread is closed ({nameof(Await)}, {nameof(OnException)} or {nameof(Await)}).");
			}

			if (continueWith == null)
				throw new ArgumentNullException(nameof(continueWith));
			
			context.Post(ExecuteOrQueueTask, continueWith);
			return this;
		} // proc Continue
		
		/// <summary>Append a action, that gets called on an exception to the task.</summary>
		/// <param name="onException"></param>
		/// <returns></returns>
		public PpsLuaTask OnException(object onException)
		{
			lock (executionLock)
			{
				if (onExceptionTask != null)
					throw new InvalidOperationException("OnException already set.");

				onExceptionTask = onException ?? throw new ArgumentNullException(nameof(onException));
			}
			context.Post(ExecuteOrQueueOnExceptionTask, null);
			return this;
		} // proc OnException

		/// <summary>Append a action, that gets called when task is finished (also in case of an exception).</summary>
		/// <param name="onFinally"></param>
		/// <returns></returns>
		public PpsLuaTask OnFinally(object onFinally)
		{
			lock (executionLock)
			{
				if (onFinallyTask != null)
					throw new InvalidOperationException("OnFinally already set.");

				onFinallyTask = onFinally ?? throw new ArgumentNullException(nameof(onFinally));
			}

			if (onFinally != null)
				context.Post(ExecuteOrQueueOnFinallyTask, null);
			return this;
		} // proc OnFinally

		/// <summary>Cancel the task.</summary>
		public void Cancel()
			=> cancellationTokenSource?.Cancel();

		/// <summary>Wait for the task and return the last result.</summary>
		/// <returns></returns>
		public Task<LuaResult> AwaitAsync()
		{
			onAwaitTask = new TaskCompletionSource<LuaResult>();
			context.Post(ExecuteOrQueueAwaitTask, null);
			return onAwaitTask.Task;
		} // func Await

		/// <summary>Wait for the task and return the last result.</summary>
		/// <returns></returns>
		public LuaResult Await()
			=> AwaitAsync().AwaitTask();

		/// <summary>Is this thread cancelable.</summary>
		public bool CanCancel => cancellationTokenSource != null;
		/// <summary>Access to cancellation token source.</summary>
		public CancellationTokenSource CancellationTokenSource => cancellationTokenSource;

		/// <summary>Is the execution thread faulted.</summary>
		public bool IsFaulted => currentException != null;
		/// <summary>Is the execution thread canceled.</summary>
		public bool IsCanceled => cancellationToken.IsCancellationRequested;
		/// <summary>Is the execution thread completed.</summary>
		public bool IsCompleted => isDisposed != 0;
	} // class PpsLuaTask

	#endregion

	public partial class PpsEnvironment : IPpsLuaTaskParent, IPpsLuaRequest
	{
		#region -- class LuaTraceLineDebugInfo ----------------------------------------

		/// <summary></summary>
		private sealed class LuaTraceLineDebugInfo : ILuaDebugInfo
		{
			private readonly string chunkName;
			private readonly string sourceFile;
			private readonly int line;

			public LuaTraceLineDebugInfo(LuaTraceLineExceptionEventArgs e, string sourceFile)
			{
				this.chunkName = e.SourceName;
				this.sourceFile = sourceFile;
				this.line = e.SourceLine;
			} // ctor

			public string ChunkName => chunkName;
			public int Column => 0;
			public string FileName => sourceFile;
			public int Line => line;
		} // class LuaTraceLineDebugInfo

		#endregion

		#region -- class LuaEnvironmentTraceLineDebugger ------------------------------

		/// <summary></summary>
		private sealed class LuaEnvironmentTraceLineDebugger : LuaTraceLineDebugger
		{
			protected override void OnExceptionUnwind(LuaTraceLineExceptionEventArgs e)
			{
				var luaFrames = new List<LuaStackFrame>();
				var offsetForRecalc = 0;
				LuaExceptionData currentData = null;

				// get default exception data
				if (e.Exception.Data[LuaRuntimeException.ExceptionDataKey] is LuaExceptionData)
				{
					currentData = LuaExceptionData.GetData(e.Exception);
					offsetForRecalc = currentData.Count;
					luaFrames.AddRange(currentData);
				}
				else
					currentData = LuaExceptionData.GetData(e.Exception, resolveStackTrace: false);

				// re-trace the stack frame
				var trace = new StackTrace(e.Exception, true);
				for (var i = offsetForRecalc; i < trace.FrameCount - 1; i++)
					luaFrames.Add(LuaExceptionData.GetStackFrame(trace.GetFrame(i)));

				// add trace point
				luaFrames.Add(new LuaStackFrame(trace.GetFrame(trace.FrameCount - 1), new LuaTraceLineDebugInfo(e, e.SourceName)));

				currentData.UpdateStackTrace(luaFrames.ToArray());
			} // proc OnExceptionUnwind
		} // class LuaEnvironmentTraceLineDebugger

		#endregion

		private LuaCompileOptions luaOptions;
		private PpsDataFieldFactory fieldFactory;

		private void CreateLuaCompileOptions()
		{
			luaOptions = new LuaCompileOptions()
			{
				DebugEngine = new LuaEnvironmentTraceLineDebugger()
			};
		} // CreateLuaCompileOptions

		#region -- Lua Compiler -------------------------------------------------------

		/// <summary>Compiles a chunk in the background.</summary>
		/// <param name="tr">Chunk source.</param>
		/// <param name="sourceLocation">Source location for the debug information.</param>
		/// <param name="throwException">If the compile fails, should be raised a exception.</param>
		/// <param name="arguments">Argument definition for the chunk.</param>
		/// <returns>Compiled chunk</returns>
		public async Task<LuaChunk> CompileAsync(TextReader tr, string sourceLocation, bool throwException, params KeyValuePair<string, Type>[] arguments)
		{
			try
			{
				return await Task.Run(() => Lua.CompileChunk(tr, sourceLocation, luaOptions, arguments));
			}
			catch (LuaParseException e)
			{
				if (throwException)
					throw;
				else
				{
					await ShowExceptionAsync(ExceptionShowFlags.Background, e, $"Compile for {sourceLocation} failed.");
					return null;
				}
			}
		} // func CompileAsync
		
		/// <summary></summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="code"></param>
		/// <param name="sourceLocation">Source location for the debug information.</param>
		/// <param name="throwException">If the compile fails, should be raised a exception.</param>
		/// <param name="argumentNames"></param>
		/// <returns></returns>
		public async Task<T> CompileLambdaAsync<T>(string code, string sourceLocation, bool throwException, params string[] argumentNames)
			where T : class
		{
			try
			{
				return await Task.Run(() => Lua.CreateLambda<T>(sourceLocation, code, argumentNames));
			}
			catch (LuaParseException e)
			{
				if (throwException)
					throw;
				else
				{
					await ShowExceptionAsync(ExceptionShowFlags.Background, e, $"Compile for {sourceLocation} failed.");
					return null;
				}
			}
		} // func CompileLambdaAsync

		/// <summary></summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="xSource"></param>
		/// <param name="throwException"></param>
		/// <param name="argumentNames"></param>
		/// <returns></returns>
		public Task<T> CompileLambdaAsync<T>(XElement xSource, bool throwException, params string[] argumentNames)
			where T : class
		{
			var code = xSource.Value;
			var pos = PpsXmlPosition.GetXmlPositionFromAttributes(xSource);
			return CompileLambdaAsync<T>(code, pos.LineInfo ?? "dummy.lua", throwException, argumentNames);
		} // func CompileAsync

		/// <summary></summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="xml"></param>
		/// <param name="throwException"></param>
		/// <param name="argumentNames"></param>
		/// <returns></returns>
		public async Task<T> CompileLambdaAsync<T>(XmlReader xml, bool throwException, params string[] argumentNames)
			where T : class
		{
			var code = await xml.GetElementContentAsync(String.Empty);
			return await CompileLambdaAsync<T>(code, xml.BaseURI ?? "dummy.lua", throwException, argumentNames);
		} // func CompileAsync


		/// <summary>Compiles a chunk in the background.</summary>
		/// <param name="sourceCode">Source code of the chunk.</param>
		/// <param name="sourceFileName">Source location for the debug information.</param>
		/// <param name="throwException">If the compile fails, should be raised a exception.</param>
		/// <param name="arguments">Argument definition for the chunk.</param>
		/// <returns>Compiled chunk</returns>
		public async Task<LuaChunk> CompileAsync(string sourceCode, string sourceFileName, bool throwException, params KeyValuePair<string, Type>[] arguments)
		{
			using (var tr = new StringReader(sourceCode))
				return await CompileAsync(tr, sourceFileName, throwException, arguments);
		} // func CompileAsync

		/// <summary>Compiles a chunk in the background.</summary>
		/// <param name="xSource">Source element of the chunk. The Value is the source code, and the positions encoded in the tag (see GetXmlPositionFromAttributes).</param>
		/// <param name="throwException">If the compile fails, should be raised a exception.</param>
		/// <param name="arguments">Argument definition for the chunk.</param>
		/// <returns>Compiled chunk</returns>
		public Task<LuaChunk> CompileAsync(XElement xSource, bool throwException, params KeyValuePair<string, Type>[] arguments)
		{
			if (xSource == null)
				throw new ArgumentNullException(nameof(xSource));

			var code = xSource.Value;
			var pos = PpsXmlPosition.GetXmlPositionFromAttributes(xSource);
			return CompileAsync(code, pos.LineInfo ?? "dummy.lua", throwException, arguments);
		} // func CompileAsync

		/// <summary>Load an compile the file from a remote source.</summary>
		/// <param name="request"></param>
		/// <param name="source">Source uri</param>
		/// <param name="throwException">Throw an exception on fail</param>
		/// <param name="arguments">Argument definition for the chunk.</param>
		/// <returns>Compiled chunk</returns>
		public async Task<LuaChunk> CompileAsync(BaseWebRequest request, Uri source, bool throwException, params KeyValuePair<string, Type>[] arguments)
		{
			if (request == null)
				throw new ArgumentNullException(nameof(request));
			if (source == null)
				throw new ArgumentNullException(nameof(source));
			try
			{
				using (var r = await request.GetResponseAsync(source.ToString()))
				{
					var contentDisposition = r.GetContentDisposition(true);
					using (var sr = request.GetTextReader(r, MimeTypes.Text.Plain))
						return await CompileAsync(sr, contentDisposition.FileName, throwException, arguments);
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
					await ShowExceptionAsync(ExceptionShowFlags.Background, e, $"Compile failed for {source}.");
					return null;
				}
			}
		} // func CompileAsync

		/// <summary>Compile the chunk in a background thread and hold the UI thread</summary>
		/// <param name="xCode"></param>
		/// <param name="throwException"></param>
		/// <param name="arguments"></param>
		/// <returns></returns>
		public LuaChunk CreateChunk(XElement xCode, bool throwException, params KeyValuePair<string, Type>[] arguments)
		{
			if (xCode == null)
				return null;

			return RunAsync(() => CompileAsync(xCode, throwException, arguments)).AwaitTask();
		} // func CreateChunk

		/// <summary>Executes the script (the script is always execute in the UI thread).</summary>
		/// <param name="chunk"></param>
		/// <param name="env"></param>
		/// <param name="throwException"></param>
		/// <param name="arguments"></param>
		/// <returns></returns>
		public LuaResult RunScript(LuaChunk chunk, LuaTable env, bool throwException, params object[] arguments)
		{
			try
			{
				return chunk.Run(env, arguments);
			}
			catch (LuaException ex)
			{
				if (throwException)
					throw;
				else
				{
					// notify exception as warning
					Traces.AppendException(ex, "Execution failed.", PpsTraceItemType.Warning);
					return LuaResult.Empty;
				}
			}
		} // func RunScript

		/// <summary>Executes the script, and returns a value (the script is always execute in the UI thread).</summary>
		/// <typeparam name="T">Type of the value.</typeparam>
		/// <param name="chunk"></param>
		/// <param name="env"></param>
		/// <param name="returnOnException"></param>
		/// <param name="arguments"></param>
		/// <returns></returns>
		public T RunScriptWithReturn<T>(LuaChunk chunk, LuaTable env, T? returnOnException, params object[] arguments)
			where T : struct
		{
			var r = chunk != null ? RunScript(chunk, env, false, arguments) : LuaResult.Empty;
			if (r.Count == 0)
			{
				if (returnOnException.HasValue)
					return returnOnException.Value;
				else
					throw new ArgumentNullException("chunk");
			}
			else
				return r[0].ChangeType<T>();
		} // func RunScriptWithReturn

		#endregion

		#region -- Async/Await Lua ----------------------------------------------------

		private Task RunBackgroundInternal(Func<Task> task, string name, CancellationToken cancellationToken)
			=> new PpsSingleThreadSynchronizationContext(name, cancellationToken, task).Task;

		/// <summary>Creates a new execution thread for the function in the background.</summary>
		/// <param name="task">Action to run.</param>
		/// <param name="name">name of the background thread</param>
		/// <param name="cancellationToken">cancellation option</param>
		public Task RunAsync(Func<Task> task, string name, CancellationToken cancellationToken)
			=> RunBackgroundInternal(task, name, cancellationToken);

		/// <summary></summary>
		/// <param name="task"></param>
		/// <returns></returns>
		public Task RunAsync(Func<Task> task)
			=> RunAsync(task, "Worker", CancellationToken.None);

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
		/// <typeparam name="T"></typeparam>
		/// <param name="task"></param>
		/// <returns></returns>
		public Task<T> RunAsync<T>(Func<Task<T>> task)
			=> RunAsync(task, "Worker", CancellationToken.None);

		#endregion

		#region -- LuaHelper ----------------------------------------------------------

		/// <summary>Show a simple message box.</summary>
		/// <param name="text"></param>
		/// <param name="caption"></param>
		/// <param name="button"></param>
		/// <param name="image"></param>
		/// <param name="defaultResult"></param>
		/// <returns></returns>
		[LuaMember("msgbox")]
		private MessageBoxResult LuaMsgBox(string text, string caption, MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.Information, MessageBoxResult defaultResult = MessageBoxResult.OK)
			=> MsgBox(text, button, image, defaultResult);

		[LuaMember("trace")]
		private void LuaTrace(PpsTraceItemType type, params object[] args)
		{
			if (args == null || args.Length == 0)
				return;

			if (args[0] is string)
				Traces.AppendText(type, String.Format((string)args[0], args.Skip(1).ToArray()));
			else
				Traces.AppendText(type, String.Join(", ", args));
		} // proc LuaTrace

		/// <summary>Send a simple notification to the internal log</summary>
		/// <param name="args"></param>
		[LuaMember("print")]
		private void LuaPrint(params object[] args)
		{
			LuaTrace(PpsTraceItemType.Information, args);
		} // proc LuaPrint

		[LuaMember("toTable")]
		private LuaTable LuaToTable(object table)
		{
			switch (table)
			{
				case null:
					return null;
				case LuaTable t:
					return t;
				case IDataRow row:
					{
						var r = new LuaTable();
						var i = 0;
						foreach (var c in row.Columns)
							r[c.Name] = GetServerRowValue(row[i++]);
						return r;
					}
				case IEnumerable<PropertyValue> props:
					{
						var r = new LuaTable();
						foreach (var p in props)
							r[p.Name] = p.Value;
						return r;
					}
				default:
					throw new ArgumentException();
			}
		} // func LuaToTable

		[LuaMember("typeof")]
		private Type LuaType(object o)
			=> o?.GetType();

		[LuaMember("isfunction")]
		private bool LuaIsFunction(object o)
			=> Lua.RtInvokeable(o);


		[LuaMember("require", true)]
		private LuaResult LuaRequire(LuaTable self, string path)
		{
			// get the current root
			var webRequest = self.GetMemberValue(nameof(IPpsLuaRequest.Request)) as BaseWebRequest ?? Request;

			if (path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) // load assembly
			{
				return new LuaResult(LoadAssemblyFromUri(new Uri(webRequest.BaseUri, new Uri(path, UriKind.Relative))));
			}
			else // load lua script
			{
				// compile code, synchonize the code to this thread
				var chunk = CompileAsync(webRequest, new Uri(path, UriKind.Relative), true, new KeyValuePair<string, Type>("self", typeof(LuaTable))).AwaitTask();
				return RunScript(chunk, self, true, self);
			}
		} // proc LuaRequire
		
		/// <summary></summary>
		/// <param name="request"></param>
		/// <param name="arguments"></param>
		/// <param name="paneUri"></param>
		/// <returns></returns>
		public async Task<object> LoadPaneDataAsync(BaseWebRequest request, LuaTable arguments, Uri paneUri)
		{
			try
			{
				using (var r = await request.GetResponseAsync(paneUri.ToString()))
				{
					// read the file name
					arguments["_filename"] = r.GetContentDisposition().FileName;

					// check content
					var contentType = r.GetContentType();
					if (contentType.MediaType == MimeTypes.Application.Xaml) // load a xaml file
					{
						XDocument xamlContent;

						// parse the xaml as xml document
						using (var sr = request.GetTextReader(r, MimeTypes.Application.Xaml))
						{
							using (var xml = XmlReader.Create(sr, Procs.XmlReaderSettings, paneUri.ToString()))
								xamlContent = await Task.Run(() => XDocument.Load(xml, LoadOptions.SetBaseUri | LoadOptions.SetLineInfo));
						}

						return xamlContent;
					}
					else if (contentType.MediaType == MimeTypes.Text.Lua
						|| contentType.MediaType == MimeTypes.Text.Plain) // load a code file
					{
						// load an compile the chunk
						using (var sr = request.GetTextReader(r, null))
							return await CompileAsync(sr, paneUri.ToString(), true, new KeyValuePair<string, Type>("self", typeof(LuaTable)));
					}
					else
						throw new ArgumentException($"Expected: xaml/lua; received: {contentType.MediaType}");
				}
			}
			catch (Exception e)
			{
				throw new ArgumentException("Can not load pane definition.\n" + paneUri.ToString(), e);
			}
		} // func LoadPaneDataAsync

		/// <summary>Creates a wrapper for the task.</summary>
		/// <param name="func">Function will be executed in the current context as an task. A task will be wrapped and executed.</param>
		/// <returns></returns>
		[LuaMember("await")]
		private LuaResult LuaAwait(object func)
		{
			int GetTaskType()
			{
				var t = func.GetType();
				if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Task<>) && t.GetGenericArguments()[0].IsPublic)
					return 0;
				else if (typeof(Task).IsAssignableFrom(t))
					return 1;
				else
					return -1;
			};

			switch (func)
			{
				case null:
					throw new ArgumentNullException(nameof(func));
				case PpsLuaTask lt:
					return lt.Await();
				case Task t:
					t.AwaitTask();
					switch (GetTaskType())
					{
						case 0:
							var genericArguments = t.GetType().GetGenericArguments();
							if (genericArguments[0] == typeof(LuaResult))
								return ((Task<LuaResult>)t).Result;
							else
							{
								dynamic d = t;
								return new LuaResult(d.Result);
							}
						case 1:
							return LuaResult.Empty;
						default:
							throw new NotSupportedException($"Could not await for task ({func.GetType().Name}).");
					}
				case DispatcherOperation o:
					LuaAwait(o.Task);
					return LuaResult.Empty;
				default:
					throw new ArgumentException($"The type '{func.GetType().Name}' is not awaitable.");
			}
		} // func LuaRunTask

		private PpsLuaTask CreateLuaTask(SynchronizationContext context, object func, object[] args)
		{
			var cancellationSource = (CancellationTokenSource)null;
			var cancellationToken = CancellationToken.None;

			// find a source or a token in the arguments
			for (var i = 0; i < args.Length; i++)
			{
				if (args[i] is CancellationToken ct)
				{
					cancellationToken = ct;
					break;
				}
				else if (args[i] is CancellationTokenSource cts)
				{
					cancellationSource = cts;
					break;
				}
			}

			// create execution thread
			var t = cancellationSource != null
				? new PpsLuaTask(this, context, cancellationSource, new LuaResult(args))
				: new PpsLuaTask(this, context, cancellationToken, new LuaResult(args));

			// start with the first function
			return t.Continue(func);
		} // func CreateLuaTask

		/// <summary>Executes the function or task in an background thread.</summary>
		/// <param name="func"></param>
		/// <param name="args"></param>
		/// <returns></returns>
		[LuaMember("run")]
		public PpsLuaTask LuaRunBackground(object func, params object[] args)
			=> CreateLuaTask(null, func, args);

		/// <summary>Executes the function or task, async in the ui thread.</summary>
		/// <param name="func"></param>
		/// <param name="args"></param>
		/// <returns></returns>
		[LuaMember("async")]
		public PpsLuaTask LuaAsync(object func, params object[] args)
		{
			SynchronizationContext GetUIContext()
				=> Dispatcher.Thread == Thread.CurrentThread && SynchronizationContext.Current is DispatcherSynchronizationContext
					? SynchronizationContext.Current
					: new DispatcherSynchronizationContext(Dispatcher);

			return CreateLuaTask(GetUIContext(), func, args);
		} // func LuaRunBackground

		/// <summary></summary>
		/// <returns></returns>
		[LuaMember("runSync")]
		public Task RunSynchronization()
			=> masterData.RunSynchronization();

		/// <summary></summary>
		/// <returns></returns>
		[LuaMember("createTransaction")]
		public PpsMasterDataTransaction CreateTransaction()
			=> MasterData.CreateTransactionAsync(PpsMasterDataTransactionLevel.ReadCommited).AwaitTask();

		/// <summary></summary>
		/// <param name="v"></param>
		/// <returns></returns>
		[LuaMember("getServerRowValue")]
		public object GetServerRowValue(object v)
		{
			if (v == null)
				return null;
			else if (v is PpsObject o)
				return o.Id;
			else if (v is PpsMasterDataRow mr)
				return mr.Key;
			else if (v is PpsLinkedObjectExtendedValue l)
				return l.IsNull ? null : (object)((PpsObject)l.Value).Id;
			else if (v is PpsFormattedStringValue fsv)
				return fsv.IsNull ? null : fsv.FormattedValue;
			else if (v is IPpsDataRowGetGenericValue gv)
				return gv.IsNull ? null : gv.Value;
			else
				return v;
		} // func GetServerRowValue

		/// <summary></summary>
		/// <param name="currentControl"></param>
		/// <param name="control"></param>
		/// <param name="throwException"></param>
		/// <returns></returns>
		[LuaMember]
		public DependencyObject GetVisualParent(DependencyObject currentControl, object control, bool throwException = false)
		{
			if (currentControl == null)
				throw new ArgumentNullException(nameof(currentControl));

			switch (control)
			{
				case LuaType lt:
					return GetVisualParent(currentControl, lt.Type, throwException);
				case Type t:
					return currentControl.GetVisualParent(t)
						?? (throwException ? throw new ArgumentException() : (DependencyObject)null);
				case string n:
					return currentControl.GetVisualParent(n)
						?? (throwException ? throw new ArgumentException() : (DependencyObject)null);
				case null:
					return currentControl.GetVisualParent()
						?? (throwException ? throw new ArgumentException() : (DependencyObject)null);
				default:
					throw new ArgumentException(nameof(control));
			}
		} // func GetVisualParent


		/// <summary></summary>
		/// <param name="currentControl"></param>
		/// <param name="control"></param>
		/// <param name="throwException"></param>
		/// <returns></returns>
		[LuaMember]
		public DependencyObject GetLogicalParent(DependencyObject currentControl, object control = null, bool throwException = false)
		{
			if (currentControl == null)
				throw new ArgumentNullException(nameof(currentControl));

			switch (control)
			{
				case LuaType lt:
					return GetLogicalParent(currentControl, lt.Type, throwException);
				case Type t:
					return currentControl.GetLogicalParent(t)
						?? (throwException ? throw new ArgumentException() : (DependencyObject)null);
				case string n:
					return currentControl.GetLogicalParent(n)
						?? (throwException ? throw new ArgumentException() : (DependencyObject)null);
				case null:
					return currentControl.GetLogicalParent()
						?? (throwException ? throw new ArgumentException() : (DependencyObject)null);
				default:
					throw new ArgumentException(nameof(control));
			}
		} // func GetLogicalParent

		/// <summary>Create a local tempfile name for this objekt</summary>
		/// <param name="obj"></param>
		/// <returns></returns>
		[LuaMember]
		public FileInfo GetLocalTempFileInfo(PpsObject obj)
		{
			// create temp directory
			var tempDirectory = new DirectoryInfo(Path.Combine(LocalPath.FullName, "tmp"));
			if (!tempDirectory.Exists)
				tempDirectory.Create();

			// build filename
			if (obj.TryGetProperty<string>(PpsObjectBlobData.fileNameTag, out var fileName))
				fileName = obj.Guid.ToString("N") + "_" + fileName;
			else
				fileName = obj.Guid.ToString("N") + StuffIO.ExtensionFromMimeType(obj.MimeType);

			return new FileInfo(Path.Combine(tempDirectory.FullName, fileName));
		} // func GetLocalTempFileInfo

		[Obsolete("Implemented for a special case, will be removed.")]
		[LuaMember]
		public Task<PpsObjectDataSet> PullRevisionAsync(PpsObject obj, long revId)
			=> obj.PullRevisionAsync<PpsObjectDataSet>(revId);
		
		/// <summary></summary>
		/// <param name="frame"></param>
		/// <param name="message"></param>
		/// <returns></returns>
		public IDisposable BlockAllUI(DispatcherFrame frame, string message = null)
		{
			Thread.Sleep(200); // wait for finish
			if(frame.Continue)
				return null; // block ui
			else
				return null;
		} // proc BlockAllUI

		/// <summary>Lua ui-wpf framwework.</summary>
		[LuaMember("UI")]
		public LuaUI LuaUI { get; } = new LuaUI();
		/// <summary></summary>
		[LuaMember]
		public LuaTable FieldFactory => fieldFactory;

		#endregion
	} // class PpsEnvironment
}
	