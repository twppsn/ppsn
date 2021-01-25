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
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Neo.IronLua;
using TecWare.DE.Networking;
using TecWare.DE.Stuff;
using TecWare.PPSn.UI;
using LLua = Neo.IronLua.Lua;

namespace TecWare.PPSn.Lua
{
	#region -- class PpsLuaUI ---------------------------------------------------------

	internal sealed class PpsLuaUI : LuaTable
	{
		private readonly IPpsWpfResources resources;
		private readonly IPpsUIService ui;
		private readonly LoggerProxy log;

		#region -- Ctor/Dtor ----------------------------------------------------------

		public PpsLuaUI(IPpsLuaShell shell)
		{
			if (shell == null)
				throw new ArgumentNullException(nameof(shell));

			resources = shell.Shell.GetService<IPpsWpfResources>(true);
			ui = shell.Shell.GetService<IPpsUIService>(true);
			log = shell.Shell.LogProxy();
		} // ctor

		#endregion

		#region -- GetResource, Command -----------------------------------------------

		/// <summary>Find the resource.</summary>
		/// <param name="key"></param>
		/// <param name="dependencyObject"></param>
		/// <returns></returns>
		[LuaMember]
		public object GetResource(object key, DependencyObject dependencyObject = null)
			=> PpsWpfShell.FindResource<object>(dependencyObject, key) ?? resources.FindResource<object>(key);
		
		/// <summary>Create a PpsCommand object.</summary>
		/// <param name="command"></param>
		/// <param name="canExecute"></param>
		/// <returns></returns>
		[LuaMember]
		internal object CreateCommand(Action<PpsCommandContext> command, Func<PpsCommandContext, bool> canExecute = null)
			=> new PpsCommand(command, canExecute);

		#endregion

		#region -- CollectionView -----------------------------------------------------

		/// <summary>Get the default view of a collection.</summary>
		/// <param name="collection"></param>
		/// <returns></returns>
		[LuaMember]
		internal ICollectionView GetView(object collection)
			=> CollectionViewSource.GetDefaultView(collection);

		/// <summary>Create a new CollectionViewSource of a collection.</summary>
		/// <param name="collection"></param>
		/// <returns></returns>
		[LuaMember]
		internal CollectionViewSource CreateSource(object collection)
		{
			var collectionArgs = collection as LuaTable;
			if (collectionArgs != null)
				collection = collectionArgs.GetMemberValue("Source");

			if (collection == null)
				throw new ArgumentNullException(nameof(collection));

			// get containted list
			if (collection is IListSource listSource)
				collection = listSource.GetList();
			
			// function views
			if (!(collection is IEnumerable) && LLua.RtInvokeable(collection))
				collection = new LuaFunctionEnumerator(collection);

			var collectionViewSource = new CollectionViewSource();
			using (collectionViewSource.DeferRefresh())
			{
				collectionViewSource.Source = collection;

				if (collectionArgs != null)
				{
					if (collectionArgs.GetMemberValue(nameof(CollectionView.SortDescriptions)) is LuaTable t)
					{
						foreach (var col in t.ArrayList.OfType<string>())
						{
							if (String.IsNullOrEmpty(col))
								continue;

							string propertyName;
							ListSortDirection direction;

							if (col[0] == '+')
							{
								propertyName = col.Substring(1);
								direction = ListSortDirection.Ascending;
							}
							else if (col[0] == '-')
							{
								propertyName = col.Substring(1);
								direction = ListSortDirection.Descending;
							}
							else
							{
								propertyName = col;
								direction = ListSortDirection.Ascending;
							}

							collectionViewSource.SortDescriptions.Add(new SortDescription(propertyName, direction));
						}
					}

					// todo: beist sich mit CanFilter
					var viewFilter = collectionArgs.GetMemberValue("ViewFilter");
					if (LLua.RtInvokeable(viewFilter))
						collectionViewSource.Filter += (sender, e) => e.Accepted = Procs.ChangeType<bool>(new LuaResult(LLua.RtInvoke(viewFilter, e.Item)));
				}
			}


			if (collectionViewSource.View == null)
				throw new ArgumentNullException("Could not create a collection view.");

			return collectionViewSource;
		} // func CreateSource

		#endregion

		#region -- Log ----------------------------------------------------------------

		[LuaMember]
		internal LoggerProxy CreateLog(string prefix = null)
			=> String.IsNullOrEmpty(prefix) ? log : LoggerProxy.Create(log.Logger, prefix);

		[LuaMember]
		internal void LogInfo(string message)
			=> log.Info(message);

		[LuaMember]
		internal void LogWarning(object message)
		{
			if (message == null)
				return;
			else if (message is Exception ex)
				log.Warn(ex);
			else
				log.Warn(message.ToString());
		} // proc LogWarning

		[LuaMember]
		internal void LogExcept(object message, string alternateMessage)
		{
			if (message == null)
				return;
			else if (message is Exception ex)
				log.Except(ex, alternateMessage);
			else
				log.Except(message.ToString());
		} // proc LogExcept

		#endregion

		#region -- Run ----------------------------------------------------------------

		[LuaMember]
		internal LuaResult Run(object action, params object[] args)
			=> ui.RunUI(() => new LuaResult(LLua.RtInvoke(action, args))).Await();

		#endregion

		#region -- MsgBox, ShowNotification -------------------------------------------

		[LuaMember]
		internal void ShowNotification(string message, PpsImage image = PpsImage.None)
			=> ui.ShowNotificationAsync(message, image).Spawn();

		[LuaMember]
		internal void ShowException(Exception exception, string alternateMessage = null)
			=> ui.ShowException(PpsExceptionShowFlags.None, exception, alternateMessage);

		[LuaMember]
		internal int MsgBox(string text, PpsImage image = PpsImage.None, params string[] buttons)
			=> ui.MsgBox(text, image, buttons);

		#endregion
	} // class PpsLuaUI

	#endregion

	#region -- class PpsLuaHttp -------------------------------------------------------

	internal sealed class PpsLuaHttp : LuaTable
	{
		private readonly IPpsLuaShell shell;

		public PpsLuaHttp(IPpsLuaShell shell)
		{
			this.shell = shell ?? throw new ArgumentNullException(nameof(shell));
		} // ctor

		#region -- Get, Post ----------------------------------------------------------

		private string GetRelativePath(DEHttpClient http, IPpsLuaCodeSource self, string path)
		{
			var uri = new Uri(path, UriKind.RelativeOrAbsolute);
			if (!uri.IsAbsoluteUri)
				uri = new Uri(self.SourceUri, uri);

			return http.MakeRelative(uri);
		} // func GetRelativePath

		private Task<LuaTable> LuaGetTableAsync(IPpsLuaCodeSource self, string path, LuaTable args)
		{
			var http = self.LuaShell.Shell.Http;
			var sb = new StringBuilder(GetRelativePath(http, self, path));
			HttpStuff.MakeUriArguments(sb, false, args.Members.Select(kv => new PropertyValue(kv.Key, kv.Value)));
			return http.GetTableAsync(sb.ToString());
		} // proc LuaGetTableAsync

		private Task<LuaTable> LuaPostTableAsync(IPpsLuaCodeSource self, string path, LuaTable args)
		{
			var http = self.LuaShell.Shell.Http;
			return http.PutTableAsync(GetRelativePath(http, self, path), args);
		} // func LuaPostTableAsync

		[LuaMember]
		internal LuaTable GetTable(string path, LuaTable args)
			=> LuaGetTableAsync(shell, path, args).Await();

		[LuaMember]
		internal LuaTable GetTable(LuaTable self, string path, LuaTable args)
			=> LuaGetTableAsync(self as IPpsLuaCodeSource ?? shell, path, args).Await();

		[LuaMember]
		internal LuaTable PostTable(string path, LuaTable args)
			=> LuaPostTableAsync(shell, path, args).Await();

		[LuaMember]
		internal LuaTable PostTable(LuaTable self, string path, LuaTable args)
			=> LuaPostTableAsync(self as IPpsLuaCodeSource ?? shell, path, args).Await();

		#endregion
	} // class PpsLuaHttp

	#endregion

	#region -- class PpsLuaShellService -----------------------------------------------

	[
	PpsLazyService,
	PpsService(typeof(IPpsLuaShell))
	]
	internal sealed class PpsLuaShellService : LuaGlobal, IPpsShellService, IPpsLuaShell, IPpsLuaCodeSource
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

		private readonly PpsLuaHttp http;
		private readonly PpsLuaUI ui;

		public PpsLuaShellService(IPpsShell shell)
			: base(new LLua(LuaIntegerType.Int64, LuaFloatType.Double))
		{
			this.shell = shell ?? throw new ArgumentNullException(nameof(shell));

			http = new PpsLuaHttp(this);
			ui = new PpsLuaUI(this);

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
			=> PpsLuaShell.RequireCodeAsync(PpsLuaCodeScope.Create(this, self), path).Await();

		#endregion

		#region -- ToString, ToNumber -------------------------------------------------

		/// <summary></summary>
		/// <param name="value"></param>
		/// <param name="targetType"></param>
		/// <returns></returns>
		[LuaMember("ToNumberUI")]
		internal object LuaToNumber(object value, Type targetType)
		{
			if (targetType == null)
				throw new ArgumentNullException(nameof(targetType));

			var r = PpsConverter.NumericValue.ConvertBack(value, targetType, null, CultureInfo.CurrentUICulture);
			if (r is ValidationResult || r == DependencyProperty.UnsetValue)
				return null;
			return r;
		} // func LuaToNumber

		/// <summary></summary>
		/// <param name="value"></param>
		/// <returns></returns>
		[LuaMember("ToStringUI")]
		internal new object LuaToString(object value)
		{
			var r = PpsConverter.NumericValue.Convert(value, typeof(string), null, CultureInfo.CurrentUICulture);
			if (r is ValidationResult || r == DependencyProperty.UnsetValue)
				return null;
			return r;
		} // func LuaToString

		#endregion

		#region -- CompileAsync -------------------------------------------------------

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

		[LuaMember]
		public LuaTable Http => http;
		[LuaMember]
		public LuaTable UI => ui;

		public IPpsShell Shell => shell;
		LLua IPpsLuaShell.Lua => Lua;
		
		LuaTable IPpsLuaShell.Global => this;
		Uri IPpsLuaCodeSource.SourceUri => shell.Http.BaseAddress;
		LuaTable IPpsLuaCodeSource.Target => this;
		IPpsLuaShell IPpsLuaCodeSource.LuaShell => this;
	} // class PpsLuaShellService

	#endregion
}
