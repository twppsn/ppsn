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
using System.IO;
using System.Threading.Tasks;
using Neo.IronLua;
using TecWare.DE.Networking;
using TecWare.DE.Stuff;
using LLua = Neo.IronLua.Lua;

namespace TecWare.PPSn.Lua
{
	#region -- class PpsLuaShellService -----------------------------------------------

	[
	PpsLazyService,
	PpsService(typeof(IPpsLuaShell))
	]
	internal class PpsLuaShellService : LuaGlobal, IPpsShellService, IPpsLuaShell, IPpsLuaCodeSource
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

		#region -- class PpsLuaDebugger -----------------------------------------------

		/// <summary></summary>
		private sealed class PpsLuaDebugger : LuaTraceLineDebugger
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

		private readonly IPpsShell shell;
		private readonly LuaCompileOptions scriptCompileOptions;
		private readonly LuaCompileOptions commandCompileOptions;

		public PpsLuaShellService(IPpsShell shell)
			: base(new LLua(LuaIntegerType.Int64, LuaFloatType.Double))
		{
			this.shell = shell ?? throw new ArgumentNullException(nameof(shell));

			scriptCompileOptions = new LuaCompileOptions { DebugEngine = new PpsLuaDebugger() };
			commandCompileOptions = new LuaCompileOptions { DebugEngine = LuaStackTraceDebugger.Default };
		} // ctor

		#region -- Require ------------------------------------------------------------

		[LuaMember("require", true)]
		internal LuaResult LuaRequire(object arg)
		{
			if (arg is string path)
				return LuaRequire(this, path);
			else
				throw new ArgumentException("string as argument expected.");
		} // func LuaRequire

		[LuaMember("require", true)]
		internal LuaResult LuaRequire(LuaTable self, string path)
			=> PpsLuaShell.RequireCodeAsync(self, this, path).Await();

		#endregion

		#region -- CombileAsync -------------------------------------------------------

		private async Task<LuaChunk> CompileCoreAsync(TextReader code, string source, bool throwException, KeyValuePair<string, Type>[] arguments)
		{
			var name = source ?? "cmd.lua";
			try
			{
				var compileOptions = String.IsNullOrEmpty(source) ? commandCompileOptions : scriptCompileOptions;
				return await Task.Run(() => Lua.CompileChunk(code, name, compileOptions, arguments));
			}
			catch (LuaParseException e)
			{
				if (throwException)
					throw;
				else
				{
					await shell.GetService<IPpsUIService>(true).ShowExceptionAsync(true, e, $"Compile for {name} failed.");
					return null;
				}
			}
		} // func CompileCoreAsync

		Task<LuaChunk> IPpsLuaShell.CompileAsync(TextReader code, string source, bool throwException, params KeyValuePair<string, Type>[] arguments)
			=> CompileCoreAsync(code, source, throwException, arguments);

		#endregion

		protected override void OnPrint(string text)
			=> shell.LogProxy().Debug(text);

		public IPpsShell Shell => shell;
		LLua IPpsLuaShell.Lua => Lua;
		
		LuaTable IPpsLuaShell.Global => this;
		Uri IPpsLuaCodeSource.SourceUri => shell.Http.BaseAddress;
		IPpsLuaShell IPpsLuaCodeSource.LuaShell => this;
	} // class PpsLuaShellService

	#endregion
}
