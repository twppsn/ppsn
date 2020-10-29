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
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Data;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;
using TecWare.PPSn.Stuff;
using TecWare.PPSn.UI;

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
		Task ShowExceptionAsync(PpsExceptionShowFlags flags, Exception exception, string alternativeMessage = null);

		/// <summary></summary>
		/// <param name="task"></param>
		void Await(Task task);

		/// <summary></summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="task"></param>
		/// <returns></returns>
		T Await<T>(Task<T> task);

		/// <summary>Returns the synchronization for the UI thread</summary>
		SynchronizationContext Context { get; }
	} // interface IPpsLuaTaskParent

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

		/// <summary></summary>
		public void Dispose()
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
			switch (func)
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

				parent.Await(parent.ShowExceptionAsync(PpsExceptionShowFlags.None, e, "Handler failed."));
				return;
			}

			currentException = e ?? throw new ArgumentNullException(nameof(e));
			//parent.Log.Append(PpsLogType.Exception, e);
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
			else if (!IsFaulted && !IsCanceled)
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
			=> parent.Await(AwaitAsync());

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

	public partial class _PpsShell
	{
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
				return await Task.Run(() => Lua.CompileChunk(tr, sourceLocation, CompileOptions, arguments));
			}
			catch (LuaParseException e)
			{
				if (throwException)
					throw;
				else
				{
					await ShowExceptionAsync(PpsExceptionShowFlags.Background, e, $"Compile for {sourceLocation} failed.");
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
					await ShowExceptionAsync(PpsExceptionShowFlags.Background, e, $"Compile for {sourceLocation} failed.");
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

		/// <summary>Compile the chunk in a background thread and hold the UI thread</summary>
		/// <param name="xCode"></param>
		/// <param name="throwException"></param>
		/// <param name="arguments"></param>
		/// <returns></returns>
		public LuaChunk CreateChunk(XElement xCode, bool throwException, params KeyValuePair<string, Type>[] arguments)
		{
			if (xCode == null)
				return null;

			return RunAsync(() => CompileAsync(xCode, throwException, arguments), "Worker", CancellationToken.None).Await();
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
					ShowExceptionAsync(PpsExceptionShowFlags.Background, ex, "Execution failed.");
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
		public T RunScriptWithReturn<T>(LuaChunk chunk, LuaTable env, T returnOnException, params object[] arguments)
		{
			var r = chunk != null
				? RunScript(chunk, env, false, arguments)
				: LuaResult.Empty;

			if (r.Count == 0)
			{
				if (returnOnException != null)
					return returnOnException;
				else
					throw new ArgumentNullException("chunk");
			}
			else
				return r[0].ChangeType<T>();
		} // func RunScriptWithReturn

		#endregion

		#region -- Library ------------------------------------------------------------

		/// <summary>Throw a exception.</summary>
		/// <param name="value"></param>
		/// <param name="message"></param>
		/// <returns></returns>
		[LuaMember("assert")]
		public new object LuaAssert(object value, string message)
		{
			if (!value.ChangeType<bool>())
				throw new LuaAssertRuntimeException(message ?? "Assertion failed!", 1, true);
			return value;
		} // func LuaAssert

		/// <summary>Throw a user error.</summary>
		/// <param name="message"></param>
		/// <param name="arg1"></param>
		[LuaMember("error")]
		public static void LuaError(object message, object arg1)
		{
			var level = 1;

			if (arg1 is int i && i > 1)  // validate stack trace level
				level = i;

			if (message is Exception ex) // throw exception
			{
				if (arg1 is string text)
					throw new LuaUserRuntimeException(text, ex);
				else
					throw ex;
			}
			else if (message is string text) // generate exception with message
			{
				if (arg1 is Exception innerException)
					throw new LuaUserRuntimeException(text, innerException);
				else
					throw new LuaUserRuntimeException(text, level, true);
			}
			else
			{
				var messageText = message?.ToString() ?? "Internal error.";
				if (arg1 is Exception innerException)
					throw new LuaRuntimeException(messageText, innerException);
				else
					throw new LuaRuntimeException(messageText, level, true);
			}
		} // proc LuaError

		/// <summary>Show a exception for a remove operation.</summary>
		/// <param name="e"></param>
		/// <param name="objectName"></param>
		[LuaMember]
		public void HandleDataRemoveException(Exception e, object objectName)
		{
			string GetRowHint(PpsDataRow row)
			{
				if (row.TryGetProperty<string>("Name", out var t1))
					return " (" + t1 + ")";
				else if (row.TryGetProperty<string>("Nr", out var t2))
					return " (" + t2 + ")";
				return String.Empty;
			} // func GetRowHint

			var alternativeMessage = (string)null;
			if (e is PpsDataTableForeignKeyRestrictionException foreignKeyRestrictionException)
			{
				alternativeMessage = String.Format("{0} konnte nicht gelöscht werden.\nWird noch verwendet von {1}{2}.",
					foreignKeyRestrictionException.ParentRow.Table.TableDefinition.DisplayName,
					foreignKeyRestrictionException.ChildRow.Table.TableDefinition.DisplayName,
					GetRowHint(foreignKeyRestrictionException.ChildRow)
				);
			}
			else
			{
				string tableName;
				if (objectName is PpsDataRow row)
					tableName = row.Table.TableDefinition.DisplayName;
				else if (objectName is PpsDataTable dt)
					tableName = dt.TableDefinition.DisplayName;
				else if (objectName is PpsDataTableDefinition df)
					tableName = df.DisplayName;
				else
					tableName = objectName?.ToString() ?? "unknown";

				alternativeMessage = String.Format("{0} konnte nicht gelöscht werden.", objectName);
			}

			ShowException(PpsExceptionShowFlags.None, e, alternativeMessage);
		} // proc HandleDataRemoveException

		///// <summary>Write in the trace log.</summary>
		///// <param name="type"></param>
		///// <param name="args"></param>
		//[LuaMember("trace")]
		//public void LuaTrace(PpsLogType type, params object[] args)
		//{
		//	if (args == null || args.Length == 0)
		//		return;

		//	if (args[0] is string)
		//		Log.Append(type, String.Format((string)args[0], args.Skip(1).ToArray()));
		//	else
		//		Log.Append(type, String.Join(", ", args));
		//} // proc LuaTrace

		///// <summary>Send a simple notification to the internal log</summary>
		///// <param name="args"></param>
		//[LuaMember("print")]
		//public void LuaPrint(params object[] args)
		//	=> LuaTrace(PpsLogType.Information, args);

		/// <summary>Creates a table from the data object.</summary>
		/// <param name="table"></param>
		/// <returns></returns>
		[LuaMember("toTable")]
		public LuaTable LuaToTable(object table)
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

		/// <summary>Get the .net type.</summary>
		/// <param name="o"></param>
		/// <returns></returns>
		[LuaMember("typeof")]
		public Type LuaType(object o)
			=> o?.GetType();

		/// <summary>Check if this object callable.</summary>
		/// <param name="o"></param>
		/// <returns></returns>
		[LuaMember("isfunction")]
		public bool LuaIsFunction(object o)
			=> Lua.RtInvokeable(o);

		/// <summary>Get raw value.</summary>
		/// <param name="v"></param>
		/// <returns></returns>
		[LuaMember("getServerRowValue")]
		public virtual object GetServerRowValue(object v)
			=> v;

		#endregion

		#region -- Lua async/await ----------------------------------------------------

		/// <summary>Creates a wrapper for the task.</summary>
		/// <param name="func">Function will be executed in the current context as an task. A task will be wrapped and executed.</param>
		/// <returns></returns>
		[LuaMember("await")]
		public LuaResult LuaAwait(object func)
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
					t.Await();
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
				default:
					return LuaAwaitFunc(func);
			}
		} // func LuaRunTask

		/// <summary>Await a function.</summary>
		/// <param name="func"></param>
		/// <returns></returns>
		protected virtual LuaResult LuaAwaitFunc(object func)
			=> throw new ArgumentException($"The type '{func.GetType().Name}' is not awaitable.");

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

			throw new NotImplementedException();
			// create execution thread
			//var t = cancellationSource != null
			//	? new PpsLuaTask(this, context, cancellationSource, new LuaResult(args))
			//	: new PpsLuaTask(this, context, cancellationToken, new LuaResult(args));

			//// start with the first function
			//return t.Continue(func);
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
			=> CreateLuaTask(Context, func, args);

		#endregion

		/// <summary></summary>
		public static LuaCompileOptions CompileOptions { get; protected set; } = null;
	} // class PpsShell
}
