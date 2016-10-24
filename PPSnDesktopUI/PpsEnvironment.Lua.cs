using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Networking;
using TecWare.DE.Stuff;
using TecWare.PPSn.Stuff;

namespace TecWare.PPSn
{
	#region -- interface IPpsLuaTaskParent ----------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public interface IPpsLuaTaskParent
	{
		void RunTaskSync(Task task);

		/// <summary>Returns the dispatcher fo the UI thread</summary>
		Dispatcher Dispatcher { get; }
		/// <summary>Returns the trace sink.</summary>
		PpsTraceLog Traces { get; }
	} // interface IPpsLuaTaskParent

	#endregion

	#region -- class PpsLuaTask ---------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Task wrapper for the lua script code</summary>
	public sealed class PpsLuaTask
	{
		private readonly IPpsLuaTaskParent parent;
		private readonly Task<LuaResult> task;

		private Queue<object> continueWith = new Queue<object>();
		private Queue<object> continueUI = new Queue<object>();
		private object onException = null;

		private volatile bool isCompleted = false;
		private Exception currentException = null;
		private LuaResult currentResult = LuaResult.Empty;

		private readonly CancellationToken cancellationToken;
		private readonly CancellationTokenSource cancellationSource = null;

		#region -- Ctor/Dtor --------------------------------------------------------------

		public PpsLuaTask(IPpsLuaTaskParent parent, CancellationTokenSource cancellationSource, CancellationToken cancellationToken , Task<LuaResult> task)
		{
			this.parent = parent;
			this.cancellationToken = cancellationToken;
			this.task = task.ContinueWith(Continue, cancellationToken);
		} // ctor

		public PpsLuaTask(IPpsLuaTaskParent parent, CancellationTokenSource cancellationSource, CancellationToken cancellationToken, params Task[] tasks)
		{
			this.parent = parent;
			this.cancellationToken = cancellationToken;

			this.task = Task.Run(() => { Task.WaitAll(tasks); return LuaResult.Empty; })
				.ContinueWith(Continue, cancellationToken); // parallel task are combined in one task
		} // ctor

		#endregion

		#region -- Continue ---------------------------------------------------------------

		private LuaResult Continue(Task<LuaResult> t)
		{
			try
			{
				lock (task)
				{
					isCompleted = true;

					// fetch async tasks
					currentResult = t.Result;
					FetchContinue(continueWith);
					continueWith.Clear();
				}
				// fetch ui tasks
				parent.Dispatcher.Invoke(() => FetchContinue(continueUI));
				lock (task)
					continueUI.Clear();

				return currentResult;
			}
			catch (Exception e)
			{
				var cleanException = e.GetInnerException();

				lock (task)
				{

					isCompleted = true;

					continueWith.Clear();
					continueUI.Clear();
				}
				if (onException == null)
				{
					lock (task)
					{
						currentException = cleanException;
						currentResult = LuaResult.Empty;
					}
				}
				else
				{
					if (onException is LuaResult)
					{
						parent.Traces.AppendException(cleanException);
						currentResult = (LuaResult)onException;
					}
					else if (Lua.RtInvokeable(onException))
						currentResult = parent.Dispatcher.Invoke(() => new LuaResult(Lua.RtInvoke(onException, cleanException)));
					else
						currentResult = new LuaResult(onException, cleanException);
				}

				return currentResult;
			}
		} // proc Continue

		private void FetchContinue(Queue<object> continueQueue)
		{
			while (continueQueue.Count > 0)
				FetchContinue(continueQueue.Dequeue());
		} // proc FetchContinue

		private void FetchContinue(object func)
		{
			currentResult = new LuaResult(Lua.RtInvoke(func, currentResult.Values));
		} // proc FetchContinue

		public PpsLuaTask Continue(object onContinue)
		{
			lock (task)
			{
				if (IsCompleted) // run sync
					FetchContinue(onContinue);
				else // else queue
					continueWith.Enqueue(onContinue);
			}
			return this;
		} // proc Continue

		public PpsLuaTask ContinueUI(object onContinue)
		{
			lock (task)
			{
				if (IsCompleted) // run sync
				{
					if (parent.Dispatcher.Thread == Thread.CurrentThread)
						FetchContinue(onContinue);
					else
						parent.Dispatcher.Invoke(() => FetchContinue(onContinue));
				}
				else // else queue
					continueUI.Enqueue(onContinue);
			}
			return this;
		} // func ContinueUI

		public PpsLuaTask OnException(object onException)
		{
			lock (task)
				this.onException = onException;
			return this;
		} // func OnException

		#endregion

		public void Cancel()
			=> cancellationSource?.Cancel();

		public LuaResult Wait()
		{
			var  waitInUIThread = parent.Dispatcher.Thread == Thread.CurrentThread; // check if the current thread, is the main thread4
			if (waitInUIThread)
			{
				Task t;
				lock (task)
					t = task;
				parent.RunTaskSync(t);
			}
			else
			{
				lock (task)
					task.Wait();
			}

			if (onException == null && currentException != null)
				throw new TargetInvocationException(currentException);
			
			// return the result
			return currentResult;
		} // func Wait
		
		public bool IsCompleted => isCompleted;
		public bool CanCanceled => cancellationSource != null;
		public CancellationToken CancellationToken => cancellationToken;

		public Task<LuaResult> BaseTask => task;
	} // class PpsLuaTask

	#endregion

	public partial class PpsEnvironment : IPpsLuaTaskParent
	{
		#region -- class LuaTraceLineDebugInfo --------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
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

		#region -- class LuaEnvironmentTraceLineDebugger ----------------------------------

		///////////////////////////////////////////////////////////////////////////////
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

		private void CreateLuaCompileOptions()
		{
			luaOptions = new LuaCompileOptions();
			luaOptions.DebugEngine = new LuaEnvironmentTraceLineDebugger();
		} // CreateLuaCompileOptions

		#region -- Lua Compiler -----------------------------------------------------------

		public async Task<LuaChunk> CompileAsync(TextReader tr, string sourceLocation, bool throwException, params KeyValuePair<string, Type>[] arguments)
		{
			try
			{
				return Lua.CompileChunk(tr, sourceLocation, luaOptions, arguments);
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

		public Task<LuaChunk> CompileAsync(string sourceCode, string sourceFileName, bool throwException, params KeyValuePair<string, Type>[] arguments)
		{
			using (var tr = new StringReader(sourceCode))
				return CompileAsync(tr, sourceFileName, throwException, arguments);
		} // func CompileAsync

		public Task<LuaChunk> CompileAsync(XElement xSource, bool throwException, params KeyValuePair<string, Type>[] arguments)
		{
			var code = xSource.Value;
			var pos = PpsXmlPosition.GetXmlPositionFromAttributes(xSource);
			return CompileAsync(code, pos.LineInfo ?? "dummy.lua", throwException, arguments);
		} // func CompileAsync

		/// <summary>Load an compile the file from a remote source.</summary>
		/// <param name="source">Source</param>
		/// <param name="throwException">Throw an exception on fail</param>
		/// <param name="arguments"></param>
		/// <returns></returns>
		public async Task<LuaChunk> CompileAsync(Uri source, bool throwException, params KeyValuePair<string, Type>[] arguments)
		{
			try
			{
				using (var r = await request.GetResponseAsync(source.ToString()))
				{
					var contentDisposition = r.GetContentDisposition(true);
					using (var sr = request.GetTextReaderAsync(r, MimeTypes.Text.Plain))
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

		public LuaChunk CreateChunk(XElement xCode, bool throwException, params KeyValuePair<string, Type>[] arguments)
		{
			if (xCode == null)
				return null;

			return RunTaskSync(CompileAsync(xCode, throwException, arguments));
		} // func CreateChunk
		
		public T RunScriptWithReturn<T>(LuaChunk chunk, LuaTable context, Nullable<T> returnOnException, params object[] arguments)
			where T : struct
		{
			try
			{
				if (chunk == null)
				{
					if (returnOnException.HasValue)
						return returnOnException.Value;
					else
						throw new ArgumentNullException("chunk");
				}
				else 
					return chunk.Run(context, arguments).ChangeType<T>();
			}
			catch (Exception ex)
			{
				if (returnOnException.HasValue)
				{
					// notify exception as warning
					Traces.AppendException(ex, "Execution failed.", true);

					return returnOnException.Value;
				}
				else
					throw;
			}
		} // func RunScriptWithReturn

		/// <summary></summary>
		/// <param name="chunk"></param>
		/// <param name="context"></param>
		/// <param name="throwException"></param>
		/// <param name="arguments"></param>
		/// <returns></returns>
		public LuaResult RunScript(LuaChunk chunk, LuaTable context, bool throwException, params object[] arguments)
		{
			try
			{
				return chunk.Run(context, arguments);
			}
			catch (LuaException ex)
			{
				if (throwException)
					throw;
				else
				{
					// notify exception as warning
					Traces.AppendException(ex, "Execution failed.", true);
					return LuaResult.Empty;
				}
			}
		} // func RunScript

		#endregion

		#region -- LuaHelper --------------------------------------------------------------

		/// <summary>Show a simple message box.</summary>
		/// <param name="text"></param>
		/// <param name="caption"></param>
		/// <param name="button"></param>
		/// <param name="image"></param>
		/// <param name="defaultResult"></param>
		/// <returns></returns>
		[LuaMember("msgbox")]
		private MessageBoxResult LuaMsgBox(string text, string caption, MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.Information, MessageBoxResult defaultResult = MessageBoxResult.OK)
		{
			return MessageBox.Show(text, caption ?? "Information", button, image, defaultResult);
		} // proc LuaMsgBox

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

		[LuaMember("typeof")]
		private Type LuaType(object o)
			=> o?.GetType();

		[LuaMember("require")]
		private LuaResult LuaRequire(string path)
		{
			// compile code, synchonize the code to this thread
			var chunk = RunTaskSync(CompileAsync(new Uri(path, UriKind.Relative), true));

			return RunScript(chunk, this, true);
		} // proc LuaRequire

		private static Task<LuaResult> ConvertToLuaResultTask<T>(Task<T> task, CancellationToken cancellationToken)
			=> task.ContinueWith(_ => new LuaResult(_.Result), cancellationToken);

		public static PpsLuaTask RunTask(IPpsLuaTaskParent parent, object func, CancellationToken cancellationToken , params object[] args)
		{
			CancellationTokenSource cancellationSource  = null;
			if (cancellationToken == CancellationToken.None) // find a source or a token in the arguments
			{
				for (var i = 0; i < args.Length; i++)
				{
					if (args[i] is CancellationToken)
					{
						cancellationToken = (CancellationToken)args[i];
						break;
					}
					else if (args[i] is CancellationTokenSource)
					{
						var tmp = (CancellationTokenSource)args[i];
						cancellationToken = tmp.Token;
						if (i == 0)
							cancellationSource = tmp;
						break;
					}
				}
			}

			if (func is PpsLuaTask)
				return (PpsLuaTask)func;
			else if (func is Task)
			{
				Task<LuaResult> t;

				if (func.GetType() == typeof(Task))
					t = ((Task)func).ContinueWith(_ => { _.Wait(); return LuaResult.Empty; }, cancellationToken);
				else if (func.GetType().GetGenericTypeDefinition() == typeof(Task<>))
				{
					var genericArguments = func.GetType().GetGenericArguments();
					if (genericArguments[0] == typeof(LuaResult))
						t = (Task<LuaResult>)func;
					else
					{
						var mi = convertToLuaResultTask.MakeGenericMethod(genericArguments);
						t = (Task<LuaResult>)mi.Invoke(null, new object[] { func, cancellationToken });
					}
				}
				else
					throw new NotImplementedException("todo: convert code missing.");

				return new PpsLuaTask(parent, cancellationSource, cancellationToken, t);
			}
			else
				return new PpsLuaTask(parent, cancellationSource, cancellationToken, Task.Run<object>(() => Lua.RtInvoke(func, args), cancellationToken));
		} // func RunTask

		public void RunTaskSync(Task task)
		{
			var inUIThread = Dispatcher.Thread == Thread.CurrentThread;

			if (inUIThread && !task.IsCompleted)
			{
				var frame = new DispatcherFrame();
				Task.Run(new Action(task.Wait)).ContinueWith(t => { frame.Continue = false; });
				using (BlockAllUI(frame))
					Dispatcher.PushFrame(frame);
			}

			task.Wait();
		} // proc RunTaskSync

		public T RunTaskSync<T>(Task<T> task)
		{
			RunTaskSync((Task)task);
			return task.Result;
		} // proc RunTaskSync

		/// <summary>Creates a background task, and run's the given function in it.</summary>
		/// <param name="func"></param>
		/// <param name="args"></param>
		/// <returns></returns>
		[LuaMember("runTask")]
		public PpsLuaTask RunTask(object func, params object[] args)
			=> RunTask(this, func, CancellationToken.None, args);

		[LuaMember("run")]
		public PpsLuaTask RunBackground(object func, params object[] args)
			=> RunTask(Task.Run(() => Lua.RtInvoke(func, args)));

		/// <summary>Executes the function in the UI thread.</summary>
		/// <param name="func"></param>
		/// <param name="args"></param>
		/// <returns></returns>
		[LuaMember("runUI")]
		public LuaResult RunUI(object func, params object[] args)
			=> Dispatcher.Invoke<LuaResult>(() => new LuaResult(Lua.RtInvoke(func, args)));

		public IDisposable BlockAllUI(DispatcherFrame frame, string message = null)
		{
			Thread.Sleep(200); // wait for finish
			if(frame.Continue)
				return null; // block ui
			else
				return null;
		} // proc BlockAllUI

		#endregion

		private readonly static MethodInfo convertToLuaResultTask;
		
		static PpsEnvironment()
		{
			convertToLuaResultTask = typeof(PpsEnvironment).GetMethod(nameof(ConvertToLuaResultTask), BindingFlags.NonPublic | BindingFlags.DeclaredOnly | BindingFlags.InvokeMethod | BindingFlags.Static);
			if (convertToLuaResultTask == null)
				throw new ArgumentNullException();
		} // sctor
	} // class PpsEnvironment
}
	