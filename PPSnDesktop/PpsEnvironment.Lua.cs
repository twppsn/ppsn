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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Neo.IronLua;
using TecWare.DE.Data;
using TecWare.DE.Networking;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;
using TecWare.PPSn.UI;

namespace TecWare.PPSn
{
	public partial class PpsEnvironment : IPpsRequest
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

		private PpsDataFieldFactory fieldFactory;

		#region -- LuaHelper ----------------------------------------------------------

		[LuaMember("require", true)]
		private LuaResult LuaRequire(object arg)
		{
			if (arg is string path)
				return LuaRequire(this, path);
			else
				throw new ArgumentException("string as argument expected.");
		} // func LuaRequire

		[LuaMember("require", true)]
		private LuaResult LuaRequire(LuaTable self, string path)
		{
			if (String.IsNullOrEmpty(path))
				throw new ArgumentException("string as argument expected.");

			// get the current root
			var webRequest = self as IPpsRequest ?? this;

			if (path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) // load assembly
			{
				return new LuaResult(LoadAssemblyFromUriAsync(webRequest.Request.CreateFullUri(path)).AwaitTask());
			}
			else // load lua script
			{
				// compile code, synchonize the code to this thread
				var chunk = webRequest.CompileAsync(new Uri(path, UriKind.Relative), true, new KeyValuePair<string, Type>("self", typeof(LuaTable))).AwaitTask();
				return RunScript(chunk, self, true, self);
			}
		} // proc LuaRequire


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
		public override object GetServerRowValue(object v)
		{
			if (v == null)
				return null;
			else if (v is PpsObject o)
				return o.Id;
			else if (v is PpsMasterDataRow mr)
				return mr.RowId;
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

		///// <summary>Create a DataTemplateSelector</summary>
		///// <param name="func"></param>
		///// <returns></returns>
		//[LuaMember("templateSelector")]
		//private DataTemplateSelector LuaDataTemplateSelectorCreate(Delegate func)
		//	=> new LuaDataTemplateSelector(func);

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
			if (obj.TryGetProperty<string>(PpsObjectBlobData.FileNameTag, out var fileName))
				fileName = obj.Guid.ToString("N") + "_" + fileName;
			else
				fileName = obj.Guid.ToString("N") + MimeTypeMapping.GetExtensionFromMimeType(obj.MimeType);

			return new FileInfo(Path.Combine(tempDirectory.FullName, fileName));
		} // func GetLocalTempFileInfo

		/// <summary></summary>
		/// <param name="filterExpr"></param>
		/// <returns></returns>
		[LuaMember]
		public Predicate<IDataRow> CreateDataRowFilter(string filterExpr)
			=> PpsDataFilterVisitorDataRow.CreateDataRowFilter<IDataRow>(PpsDataFilterExpression.Parse(filterExpr));

		[Obsolete("Implemented for a special case, will be removed.")]
		[LuaMember]
		public Task<PpsObjectDataSet> PullRevisionAsync(PpsObject obj, long revId)
			=> obj.PullRevisionAsync<PpsObjectDataSet>(revId);

		/// <summary></summary>
		/// <param name="reportName"></param>
		/// <param name="arguments"></param>
		/// <returns></returns>
		[LuaMember]
		public async Task<string> RunServerReportAsync(string reportName, LuaTable arguments)
		{
			var requestUrl = new StringBuilder("/?action=report&name=");
			requestUrl.Append(Uri.EscapeUriString(reportName));

			if (arguments != null)
			{
				foreach (var m in arguments.Members)
				{
					requestUrl.Append('&')
						.Append(Uri.EscapeUriString(m.Key))
						.Append('=')
						.Append(Uri.EscapeUriString(m.Value.ChangeType<string>()));
				}
			}

			string targetFileName;

			using (var r = await Request.GetResponseAsync(requestUrl.ToString(), MimeTypes.Application.Pdf))
			using (var src = await r.Content.ReadAsStreamAsync())
			{
				// download file
				var tempFileName = Path.GetTempFileName();
				targetFileName = Path.ChangeExtension(tempFileName, ".pdf");
				using (var dst = new FileStream(targetFileName, FileMode.CreateNew))
					await src.CopyToAsync(dst);

				File.Delete(tempFileName);
			}

			return targetFileName;
		} // func RunServerReportAsync

		/// <summary></summary>
		/// <param name="frame"></param>
		/// <param name="message"></param>
		/// <returns></returns>
		public IDisposable BlockAllUI(DispatcherFrame frame, string message = null)
		{
			Thread.Sleep(200); // wait for finish
			if (frame.Continue)
				return null; // block ui
			else
				return null;
		} // proc BlockAllUI

		/// <summary>Field factory for controls</summary>
		[LuaMember]
		public LuaTable FieldFactory => fieldFactory;

		#endregion
	} // class PpsEnvironment
}
	