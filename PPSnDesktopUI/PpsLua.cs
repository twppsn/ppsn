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
using System.Windows;
using Neo.IronLua;
using TecWare.DE.Networking;
using TecWare.PPSn.UI;

namespace TecWare.PPSn
{
	#region -- interface IPpsLuaShell -------------------------------------------------

	/// <summary>Shell service of the scripting interface.</summary>
	public interface IPpsLuaShell : IPpsLuaCodeSource, IPpsShellService
	{
		/// <summary>Compile a text to a code chunk.</summary>
		/// <param name="code">Chunk source.</param>
		/// <param name="source">Source location for the debug information.</param>
		/// <param name="throwException">If the compile fails, should be raised a exception.</param>
		/// <param name="arguments">Argument definition for the chunk.</param>
		/// <returns></returns>
		Task<LuaChunk> CompileAsync(TextReader code, string source, bool throwException, params KeyValuePair<string, Type>[] arguments);

		/// <summary>Global environment.</summary>
		LuaTable Global { get; }
		/// <summary>Lua engine.</summary>
		Lua Lua { get; }
	} // interface IPpsLuaShell

	#endregion

	#region -- interface IPpsLuaCodeSource --------------------------------------------

	/// <summary>Implement for a lua code source.</summary>
	public interface IPpsLuaCodeSource
	{
		/// <summary>Lua code target.</summary>
		LuaTable Target { get; }
		/// <summary>Source for the code.</summary>
		Uri SourceUri { get; }

		/// <summary>Shell that compiled the code.</summary>
		IPpsLuaShell LuaShell { get; }
	} // interface IPpsLuaCodeSource

	#endregion

	#region -- interface IPpsLuaCodeBehind --------------------------------------------

	/// <summary>Simple code behind contract for wpf user controls.</summary>
	public interface IPpsLuaCodeBehind : IPpsXamlCode, IPpsLuaCodeSource
	{
		/// <summary>Gets called after creation.</summary>
		/// <param name="control"></param>
		/// <param name="arguments"></param>
		void OnControlCreated(FrameworkElement control, LuaTable arguments);
	} // interface IPpsLuaCodeBehind

	#endregion

	#region -- class PpsLuaCodeScope --------------------------------------------------

	public sealed class PpsLuaCodeScope : LuaTable, IPpsLuaCodeSource
	{
		private readonly Uri sourceUri;
		private readonly IPpsLuaShell shell;
		private readonly LuaTable target;

		public PpsLuaCodeScope(IPpsLuaShell shell, LuaTable target, Uri sourceUri)
		{
			this.shell = shell ?? throw new ArgumentNullException(nameof(shell));
			this.target = target ?? throw new ArgumentNullException(nameof(target));
			this.sourceUri = sourceUri ?? throw new ArgumentNullException(nameof(sourceUri));
		} // ctor

		protected override object OnIndex(object key)
			=> base.OnIndex(key) ?? target.GetValue(key);

		protected override bool OnNewIndex(object key, object value)
		{
			target.SetValue(key, value);
			return true;
		} // proc OnNewIndex

		public IPpsLuaShell LuaShell => shell;
		public LuaTable Target => target;
		public Uri SourceUri => sourceUri;

		public static IPpsLuaCodeSource Create(IPpsLuaCodeSource currentCodeSource, LuaTable table)
			=> table is IPpsLuaCodeSource s ? s : new PpsLuaCodeScope(currentCodeSource.LuaShell, table, currentCodeSource.SourceUri);
	} // class PpsLuaCodeScope

	#endregion

	#region -- class PpsLuaCode -------------------------------------------------------

	[DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
	public class PpsLuaCode : LuaTable, IPpsLuaCodeSource
	{
		private readonly IPpsLuaShell shell;
		private readonly Uri sourceUri;

		public PpsLuaCode(IPpsLuaShell shell, Uri sourceUri)
		{
			this.shell = shell ?? throw new ArgumentNullException(nameof(shell));
			this.sourceUri = sourceUri ?? throw new ArgumentNullException(nameof(sourceUri));
		} // ctor

		private string GetDebuggerDisplay()
			=> $"Code: {sourceUri}";

		protected override object OnIndex(object key)
			=> base.OnIndex(key) ?? shell.Global.GetValue(key);

		LuaTable IPpsLuaCodeSource.Target => this;

		public Uri SourceUri => sourceUri;
		public IPpsLuaShell LuaShell => shell;

		/// <summary></summary>
		[LuaMember]
		public LuaTable Self => this;
	} // class PpsLuaCode

	#endregion

	#region -- class PpsLuaCodeBehind -------------------------------------------------

	public class PpsLuaCodeBehind : PpsLuaCode, IPpsLuaCodeBehind
	{
		private FrameworkElement control = null;
		private Task codeCompiledTask = Task.CompletedTask;

		public PpsLuaCodeBehind(IPpsLuaShell shell, Uri sourceUri)
			: base(shell, sourceUri)
		{
		} // ctor

		protected override object OnIndex(object key)
			=> PpsLuaShell.GetXamlElement(control, key) ?? base.OnIndex(key);

		void IPpsXamlCode.CompileCode(Uri uri, string code)
		{
			if (String.IsNullOrEmpty(code))
				return;
			if (uri == null)
				throw new ArgumentNullException(nameof(uri));

			if (codeCompiledTask == null || codeCompiledTask.IsCompleted)
				codeCompiledTask = PpsLuaShell.RequireCodeAsync(this, new StringReader(code), uri);
			else
				codeCompiledTask = Task.WhenAll(codeCompiledTask, PpsLuaShell.RequireCodeAsync(this, new StringReader(code), uri));
		} // proc IPpsXamlCode.CompileCode

		void IPpsLuaCodeBehind.OnControlCreated(FrameworkElement control, LuaTable arguments)
		{
			// wait for code
			codeCompiledTask.Await();

			// execute method
			this.control = control;

			CallMemberDirect("OnCreated", new object[] { control, arguments }, ignoreNilFunction: true);
		} // proc IPpsLuaCodeBehind.OnControlCreated

		/// <summary>Control that is bound to the code</summary>
		[LuaMember]
		public FrameworkElement Control => control;
	} // class PpsLuaCodeBehind

	#endregion

	#region -- class PpsLuaShell ------------------------------------------------------

	/// <summary></summary>
	public static class PpsLuaShell
	{
		/// <summary></summary>
		/// <param name="lua"></param>
		/// <param name="command"></param>
		/// <param name="throwException"></param>
		/// <returns></returns>
		public static async Task<LuaChunk> CompileAsync(this IPpsLuaShell lua, string command, bool throwException = true)
		{
			using (var tr = new StringReader(command))
				return await lua.CompileAsync(tr, null, throwException);
		} // func CompileAsync

		/// <summary>Load code into the table.</summary>
		/// <param name="self">Target for the compiled code</param>
		/// <param name="shell">Shell that should compile cthe code.</param>
		/// <param name="path">Code source</param>
		/// <returns>Result of the executed code.</returns>
		public static Task<LuaResult> RequireCodeAsync(this IPpsLuaCodeSource self, string path)
		{
			if (String.IsNullOrEmpty(path))
				throw new ArgumentException("string as argument expected.", nameof(path));
			return RequireCodeAsync(self, new Uri(path, UriKind.RelativeOrAbsolute));
		} // func RequireCodeAsync

		/// <summary>Load code into the table.</summary>
		/// <param name="self">Target for the compiled code</param>
		/// <param name="shell">Shell that should compile cthe code.</param>
		/// <param name="sourceUri">Code source</param>
		/// <returns>Result of the executed code.</returns>
		public static async Task<LuaResult> RequireCodeAsync(this IPpsLuaCodeSource self, Uri sourceUri)
		{
			// compile code, synchonize the code to this thread
			if (!sourceUri.IsAbsoluteUri)
				sourceUri = new Uri(self.SourceUri, sourceUri);

			using (var response = await self.LuaShell.Shell.Http.GetAsync(sourceUri))
			using (var tr = await response.GetTextReaderAsync())
				return await RequireCodeAsync(self, tr, sourceUri);
		} // func RequireCodeAsync

		/// <summary>Load code into the table.</summary>
		/// <param name="self">Target for the compiled code</param>
		/// <param name="shell">Shell that should compile cthe code.</param>
		/// <param name="tr">Code stream.</param>
		/// <param name="sourceUri">Code source</param>
		/// <returns>Result of the executed code.</returns>
		public static async Task<LuaResult> RequireCodeAsync(this IPpsLuaCodeSource self, TextReader tr, Uri sourceUri)
		{
			if (self == null)
				throw new ArgumentNullException(nameof(self));
			if (sourceUri == null)
				throw new ArgumentNullException(nameof(sourceUri));
			
			// compile code
			var chunk = await self.LuaShell.CompileAsync(tr, sourceUri.ToString(), true, new KeyValuePair<string, Type>("self", typeof(LuaTable)));

			// run code
			return chunk.Run(
				self.Target, // declaration target
				new PpsLuaCodeScope(self.LuaShell, self.Target, new Uri(sourceUri.GetComponents(UriComponents.Scheme | UriComponents.Host | UriComponents.Port | UriComponents.Path, UriFormat.UriEscaped), UriKind.Absolute)) // source for functions
			);
		} // proc RequireCodeAsync

		/// <summary>Find a control by name for lua.</summary>
		/// <param name="control"></param>
		/// <param name="key"></param>
		/// <returns><c>null</c> if there is no control or the current thread is not in the correct ui-thread.</returns>
		public static object GetXamlElement(FrameworkElement control, object key)
			=> key is string controlName && control != null && control.Dispatcher.CheckAccess() ? control.FindName(controlName) : null;
	} // class PpsLuaShell

	#endregion
}
