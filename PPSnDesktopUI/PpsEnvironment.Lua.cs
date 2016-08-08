using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Networking;
using TecWare.DE.Stuff;

namespace TecWare.PPSn
{
	#region -- class PpsLuaTask ---------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Task wrapper for the lua script code</summary>
	public sealed class PpsLuaTask
	{
		private readonly PpsEnvironment environment;
		private readonly Task<LuaResult> task;

		private Queue<object> continueWith = new Queue<object>();
		private Queue<object> continueUI = new Queue<object>();
		private object onException = null;

		private volatile bool isCompleted = false;
		private DispatcherFrame currentFrame = null;
		private LuaResult currentResult = LuaResult.Empty;

		#region -- Ctor/Dtor --------------------------------------------------------------

		public PpsLuaTask(PpsEnvironment environment, Task<LuaResult> task)
		{
			this.environment = environment;
			this.task = task.ContinueWith(Continue);
		} // ctor

		public PpsLuaTask(PpsEnvironment environment, params Task[] tasks)
		{
			this.environment = environment;

			this.task = Task.Run(() => { Task.WaitAll(tasks); return LuaResult.Empty; })
				.ContinueWith(Continue); // parallel task are combined in one task
		} // ctor

		#endregion

		private LuaResult Continue(Task<LuaResult> t)
		{
			lock (task)
			{
				try
				{
					isCompleted = true;

					// fetch async tasks
					currentResult = t.Result;
					FetchContinue(continueWith);

					// fetch ui tasks
					environment.Dispatcher.Invoke(() => FetchContinue(continueUI));

					return currentResult;
				}
				catch (Exception e)
				{
					environment.Traces.AppendException(e);

					if (onException == null)
						currentResult = LuaResult.Empty;
					else
						currentResult = environment.Dispatcher.Invoke(() => new LuaResult(Lua.RtInvoke(onException, e)));

					isCompleted = true;
					return currentResult;
				}
				finally
				{
					if (currentFrame != null)
						currentFrame.Continue = true;
				}
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
					environment.Dispatcher.Invoke(() => FetchContinue(onContinue));
				else // else queue
					continueUI.Enqueue(onContinue);
			}
			return this;
		} // func ContinueUI

		public LuaResult BlockUI(string statusText)
		{
			lock (task)
			{
				if (IsCompleted)
					return currentResult;
				else
					currentFrame = new DispatcherFrame();
			}

			// block current execute line
			Dispatcher.PushFrame(currentFrame);

			return currentResult; // return the result
		} // func BlockUI

		public LuaResult Wait()
		{
			task.Wait();
			return currentResult;
		} // func Wait

		public bool IsCompleted => isCompleted;
		} // class PpsLuaTask

	#endregion

	public partial class PpsEnvironment
	{
		private LuaCompileOptions luaOptions = LuaDeskop.StackTraceCompileOptions;

		#region -- Lua Compiler -----------------------------------------------------------

		public async Task<LuaChunk> CompileAsync(XElement xSource, bool throwException, params KeyValuePair<string, Type>[] arguments)
		{
			try
			{
				var code = xSource.Value;
				var fileName = "dummy.lua"; // todo: get position
				return Lua.CompileChunk(code, fileName, luaOptions, arguments);
			}
			catch (LuaParseException e)
			{
				if (throwException)
					throw;
				else
				{
					await ShowExceptionAsync(ExceptionShowFlags.Background, e, "Compile failed.");
					return null;
				}
			}
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
					var contentDisposion = r.GetContentDisposition(true);
					using (var sr = request.GetTextReaderAsync(r, MimeTypes.Text.Lua))
						return Lua.CompileChunk(sr, contentDisposion.FileName, luaOptions, arguments);
				}
			}
			catch (LuaParseException e)
			{
				if (throwException)
					throw;
				else
				{
					await ShowExceptionAsync(ExceptionShowFlags.Background, e, $"Compile for {source} failed.");
					return null;
				}
			}
		} // func CompileAsync

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

		/// <summary>Creates a background task, and run's the given function in it.</summary>
		/// <param name="func"></param>
		/// <param name="args"></param>
		/// <returns></returns>
		[LuaMember("runAsync")]
		public PpsLuaTask RunAsync(object func, params object[] args)
		{
			if (func is Task)
			{
				Task<LuaResult> t;

				if (func.GetType() == typeof(Task))
					t = ((Task)func).ContinueWith(_ => LuaResult.Empty);
				else if (func.GetType() == typeof(Task<LuaResult>))
					t = (Task<LuaResult>)func;
				else
					throw new NotImplementedException("todo: convert code missing.");

				return new PpsLuaTask(this, t);
			}
			else
				return new PpsLuaTask(this, Task.Run<object>(() => Lua.RtInvoke(func, args)));
		} // func RunAsync

		/// <summary>Executes the function in the UI thread.</summary>
		/// <param name="func"></param>
		/// <param name="args"></param>
		/// <returns></returns>
		[LuaMember("runSync")]
		public LuaResult RunSync(object func, params object[] args)
			=> Dispatcher.Invoke<LuaResult>(() => new LuaResult(Lua.RtInvoke(func, args)));

		#endregion
	} // class PpsEnvironment
}
