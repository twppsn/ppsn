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

namespace TecWare.PPSn
{
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

		/// <summary></summary>
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
			catch (LuaException e)
			{
				if (throwException)
					throw;
				else
				{
					ShowException(ExceptionShowFlags.None, e);
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

		[LuaMember("syncUI")]
		private LuaResult LuaUI(object invoke, params object[] args)
			=> Dispatcher.Invoke<LuaResult>(() => new LuaResult(Lua.RtInvoke(invoke, args)), DispatcherPriority.Normal);

		[LuaMember("spawnThread")]
		private Task LuaSpawn(object invoke, params object[] args)
		{
			return Task.CompletedTask; // todo:
		} // func LuaSpawn

		#endregion
	} // class PpsEnvironment
}
