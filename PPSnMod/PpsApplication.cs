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
using System.Xml;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Data;
using TecWare.DE.Networking;
using TecWare.DE.Server;
using TecWare.DE.Server.Configuration;
using TecWare.DE.Server.Http;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;
using TecWare.PPSn.Reporting;
using TecWare.PPSn.Server.Data;

namespace TecWare.PPSn.Server
{
	#region -- interface IPpsApplicationInitialization --------------------------------

	/// <summary></summary>
	public interface IPpsApplicationInitialization : IDEConfigItem
	{
		/// <summary>Register a one time task, that will be run durring initialization.</summary>
		/// <param name="order"></param>
		/// <param name="status"></param>
		/// <param name="task"></param>
		void RegisterInitializationTask(int order, string status, Func<Task> task);

		/// <summary>Wait until all application information are processed.</summary>
		/// <param name="timeout"></param>
		/// <returns></returns>
		bool? WaitForInitializationProcess(int timeout = -1);

		/// <summary></summary>
		bool IsInitializedSuccessful { get; }
	} // interface IPpsApplicationInitialization

	#endregion

	/// <summary>Base service provider, for all pps-moduls:
	/// - user administration
	/// - data cache, for commonly used data or states
	/// - view services (executes and updates all views, to data)
	/// </summary>
	public partial class PpsApplication : DEConfigLogItem, IPpsApplicationInitialization
	{
		#region -- struct InitializationTask ------------------------------------------

		private struct InitializationTask : IComparable<InitializationTask>
		{
			public int CompareTo(InitializationTask other)
			{
				var r = Order.CompareTo(other.Order);
				return r == 0 ? Status.CompareTo(other.Status) : r;
			} // func CompareTo

			public int Order { get; set; }
			public string Status { get; set; }
			public Func<Task> Task { get; set; }
		} // struct InitializationTask

		#endregion

		private readonly SimpleConfigItemProperty<string> initializationProgress;
		private Task initializationProcess = null;        // initialization process
		private bool isInitializedSuccessful = false;     // is the system initialized properly

		private List<InitializationTask> initializationTasks = new List<InitializationTask>(); // Action that should be done in the initialization process

		private PpsReportEngine reporting = null;
		private readonly PpsServerReportProvider reportProvider;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary></summary>
		/// <param name="sp"></param>
		/// <param name="name"></param>
		public PpsApplication(IServiceProvider sp, string name)
			: base(sp, name)
		{
			initializationProgress = new SimpleConfigItemProperty<string>(this, "ppsn_init_progress", "Initialization", "Misc", "Show the current state of the initialization of the node.", null, "Pending");

			this.databaseLibrary = new PpsDatabaseLibrary(this);
			this.objectsLibrary = new PpsObjectsLibrary(this);
			this.httpLibrary = new PpsHttpLibrary(this);
			this.reportProvider = new PpsServerReportProvider(this);
 
			// register shortcut for text
			LuaType.RegisterTypeAlias("text", typeof(PpsFormattedStringValue));
			LuaType.RegisterTypeAlias("blob", typeof(byte[]));
			LuaType.RegisterTypeAlias("geography", typeof(Microsoft.SqlServer.Types.SqlGeography));

			InitUser();
		} // ctor

		/// <summary></summary>
		/// <param name="config"></param>
		protected override void OnBeginReadConfiguration(IDEConfigLoading config)
		{
			base.OnBeginReadConfiguration(config);

			// shutdown the init
			UpdateInitializationState("Shutdown previous init");
			if (!(initializationProcess?.IsCompleted ?? true))
				initializationProcess.Wait();

			UpdateInitializationState("Read configuration");

			// parse the configuration
			BeginReadConfigurationData(config);
			BeginReadConfigurationUser(config);
			BeginReadConfigurationReport(config);
		} // proc OnBeginReadConfiguration

		/// <summary></summary>
		/// <param name="config"></param>
		protected override void OnEndReadConfiguration(IDEConfigLoading config)
		{
			base.OnEndReadConfiguration(config);

			// set the configuration
			BeginEndConfigurationData(config);
			BeginEndConfigurationUser(config);
			
			// restart main thread
			initializationProcess = Task.Run(new Action(InitializeApplication));
		} // proc OnEndReadConfiguration

		/// <summary></summary>
		/// <param name="disposing"></param>
		protected override void Dispose(bool disposing)
		{
			try
			{
				UpdateInitializationState("Shuting down");

				DoneUser();
				
				initializationProgress.Dispose();
			}
			finally
			{
				base.Dispose(disposing);
			}
		} // proc Dispose

		#endregion

		#region -- Init ---------------------------------------------------------------

		private void UpdateInitializationState(string state)
		{
			if (String.IsNullOrEmpty(state))
				state = "Initialization";

			initializationProgress.Value = state;
		} // proc UpdateInitializationState

		private void UpdateInitializationState(LogMessageScopeProxy scope, int order, string state)
		{
			UpdateInitializationState(state);

			scope.WriteStopWatch()
				.WriteLine("{0:N0}: {1}", order, state);
		} // proc UpdateInitializationState

		private void InitializeApplication()
		{
			using (var msg = Log.CreateScope(LogMsgType.Information, stopTime: true))
			{
				try
				{
					msg.WriteLine("Initialize system");

					UpdateInitializationState("Initialize databases");

					// get the init tasks
					initializationTasks.Sort();

					var i = 0;
					while (i < initializationTasks.Count)
					{
						// combine same order
						var startAt = i;
						var order = initializationTasks[i].Order;
						while (i < initializationTasks.Count && initializationTasks[i].Order == order)
							i++;

						UpdateInitializationState(
							msg,
							initializationTasks[startAt].Order,
							initializationTasks[startAt].Status
						);

						// execute the action
						var count = i - startAt;
						if (count == 1)
						{
							initializationTasks[startAt].Task().Wait();
						}
						else
						{
							// start all tasks parallel
							var currentTasks = new Task[count];
							for (var j = startAt; j < i; j++)
								currentTasks[j - startAt] = initializationTasks[j].Task();

							Task.WaitAll(currentTasks);
						}
					}

					isInitializedSuccessful = true;
					UpdateInitializationState("Successful");
				}
				catch (Exception e)
				{
					isInitializedSuccessful = false;
					UpdateInitializationState("Failed");
					msg.NewLine()
						.WriteException(e);

					Server.LogMsg(EventLogEntryType.Error, "Configuration not initialized. See log for details.");
				}
			}
		} // proc InitializeApplication

		/// <summary>Wait for initialization of the system, initialization is processed synchron.</summary>
		/// <param name="timeout"></param>
		/// <returns></returns>
		public bool? WaitForInitializationProcess(int timeout = -1)
			=> initializationProcess.Wait(timeout) ? new bool?(isInitializedSuccessful) : null;

		/// <summary>Extent the initialization process.</summary>
		/// <param name="order"></param>
		/// <param name="status"></param>
		/// <param name="task"></param>
		[LuaMember(nameof(RegisterInitializationAction))]
		public void RegisterInitializationAction(int order, string status, Action task)
			=> RegisterInitializationTask(order, status, () => Task.Run(task));

		/// <summary>Extent the initialization process.</summary>
		/// <param name="order"></param>
		/// <param name="status"></param>
		/// <param name="task"></param>
		public void RegisterInitializationTask(int order, string status, Func<Task> task)
		{
			if (status == null)
				status = String.Empty;

			lock (initializationTasks)
			{
				var initTask = new InitializationTask() { Order = order, Status = status, Task = task };

				var index = initializationTasks.BinarySearch(initTask);
				if (index < 0)
					initializationTasks.Insert(~index, initTask);
				else
				{
					while (index < initializationTasks.Count && initializationTasks[index].Order == initTask.Order)
						index++;
					initializationTasks.Insert(index, initTask);
				}
			}
		} // proc RegisterInitializationTask

		/// <summary>Is the ppsn configuration initialized.</summary>
		public bool IsInitializedSuccessful => isInitializedSuccessful;

		#endregion

		#region -- Reporting ----------------------------------------------------------

		#region -- class PpsServerReportProvider --------------------------------------

		/// <summary></summary>
		private sealed class PpsServerReportProvider : PpsDataServerProviderBase
		{
			private readonly PpsApplication application;

			public PpsServerReportProvider(PpsApplication application)
			{
				this.application = application ?? throw new ArgumentNullException(nameof(application));
			} // ctor

			public async override Task<IEnumerable<IDataRow>> GetListAsync(string select, PpsDataColumnExpression[] columns, PpsDataFilterExpression filter, PpsDataOrderExpression[] order)
				=> await application.Database.CreateSelectorAsync(select, columns, filter, order);

			public override async Task<PpsDataSet> GetDataSetAsync(object identitfication, string[] tableFilter)
			{
				PpsObjectAccess GetObjectCore()
				{
					switch (identitfication)
					{
						case int i:
							return application.Objects.GetObject(i);
						case long l:
							return application.Objects.GetObject(l);
						case Guid g:
							return application.Objects.GetObject(g);
						case LuaTable t:
							var o = application.Objects.GetObject(t);
							if (t.TryGetValue<long>("RevId", out var revId))
								o.SetRevision(revId);
							return o;
						default:
							return application.Objects.GetObject(identitfication.ChangeType<long>());
					}
				} // func GetObjectCore

				// initialize object
				var obj = await Task.Run(() => GetObjectCore());
				var item = application.Objects.GetObjectItem(obj.Typ);
				return (PpsDataSet)await Task.Run(() => item.PullData(obj));
			} // func GetDataSetAsync

			public override Task<LuaTable> ExecuteAsync(LuaTable arguments)
			{
				var functionName = arguments.GetOptionalValue("name", (string)null);
				if (functionName == null)
					throw new ArgumentNullException("name", "No function definied.");

				return Task.FromResult(new LuaResult(application.CallMember(functionName, arguments))[0] as LuaTable);
			} // func ExecuteAsync
		} // class PpsServerReportProvider

		#endregion

		#region -- class PpsReportSession ---------------------------------------------

		/// <summary></summary>
		private sealed class PpsReportSession : PpsReportSessionBase
		{
			#region -- class ImageConverter -------------------------------------------

			private sealed class ImageConverter : PpsReportValueEmitter
			{
				private readonly PpsReportSession session;

				public ImageConverter(PpsReportSession session, string columnName, IDataColumns columns)
					: base(columnName, columns)
				{
					this.session = session ?? throw new ArgumentNullException(nameof(session));
				} // ctor

				private async Task WriteAsync(XmlWriter xml, FileInfo fi)
				{
					await xml.WriteStartElementAsync(null, "ref", null);
					await xml.WriteAttributeStringAsync(null, "src", null, fi.FullName);
					await xml.WriteEndElementAsync();
				} // proc WriteAsync

				public override async Task WriteAsync(XmlWriter xml, IDataRow row)
				{
					var objkIdKey = GetValue(row);
					if (objkIdKey == null)
						return;

					var objkId = objkIdKey.ChangeType<long>();

					// check cache
					if (session.exportedImages.TryGetValue(objkId, out var fi) && fi != null)
					{
						await WriteAsync(xml, fi);
						return;
					}

					// mark as exported
					session.exportedImages[objkId] = null;

					// test if the object is an image
					var obj = session.application.Objects.GetObject(objkId);
					if (obj.MimeType != MimeTypes.Image.Jpeg
						&& obj.MimeType != MimeTypes.Image.Png
						&& obj.MimeType != MimeTypes.Application.Pdf)
						return;

					// write object to disc
					// todo: optimize to use direct link
					fi = session.CreateTempFile(objkId.ToString() + MimeTypeMapping.GetExtensionFromMimeType(obj.MimeType), true);

					using (var dst = fi.OpenWrite())
					using (var src = obj.GetDataStream())
						await src.CopyToAsync(dst);

					await WriteAsync(xml, fi);

					session.exportedImages[objkId] = fi;
				} // proc WriteAsnyc
			} // class ImageConverter

			#endregion

			private readonly PpsApplication application;

			private readonly Dictionary<long, FileInfo> exportedImages = new Dictionary<long, FileInfo>();

			public PpsReportSession(PpsApplication application)
			{
				this.application = application ?? throw new ArgumentNullException(nameof(application));
			} // ctor

			public override IPpsReportValueEmitter CreateColumnEmitter(string argument, string columnName, IDataColumns columns)
			{
				if (argument == "image" && columnName != null)
					return new ImageConverter(this, columnName, columns);
				else
					return base.CreateColumnEmitter(argument, columnName, columns);
			} // func CreateColumnEmitter
		} // class PpsReportSession

		#endregion

		private void BeginReadConfigurationReport(IDEConfigLoading config)
		{
			var currentNode = XConfigNode.GetElement(Server.Configuration, config.ConfigNew, PpsStuff.xnReports);

			var systemPath = currentNode.GetAttribute<string>("system") ?? throw new DEConfigurationException(currentNode.Element, "@system is empty.");
			var basePath = currentNode.GetAttribute<string>("base") ?? throw new DEConfigurationException(currentNode.Element, "@base is empty.");
			var logPath = currentNode.GetAttribute<string>("logs");
			var workPath = currentNode.GetAttribute<string>("work");

			// check for recreate the reporting engine
			if (reporting == null
				|| !ProcsDE.IsPathEqual(reporting.EnginePath, systemPath)
				|| !ProcsDE.IsPathEqual(reporting.BasePath, basePath)
				|| (logPath != null && !ProcsDE.IsPathEqual(reporting.LogPath, logPath))
				|| (workPath != null && !ProcsDE.IsPathEqual(reporting.WorkingPath, workPath)))
			{
				reporting = new PpsReportEngine(systemPath, basePath, reportProvider, CreateReportSession, reportWorkingPath: workPath, reportLogPath: logPath);
			}

			// update values
			reporting.CleanBaseDirectoryAfter = currentNode.GetAttribute<int>("cleanBaseDirectory");
			reporting.ZipLogFiles = currentNode.GetAttribute<bool>("zipLogFiles");
			reporting.StoreSuccessLogs = currentNode.GetAttribute<bool>("storeSuccessLogs");
		} // proc BeginReadConfigurationReport

		private PpsReportSessionBase CreateReportSession()
			=> new PpsReportSession(this);

		/// <summary>Run a report.</summary>
		/// <param name="table"></param>
		/// <returns></returns>
		[LuaMember]
		public string RunReport(LuaTable table)
			=> RunReportAsync(table).AwaitTask();

		/// <summary>Run a report data.</summary>
		/// <param name="table"></param>
		/// <returns></returns>
		[LuaMember]
		public string DebugData(LuaTable table)
			=> RunDataAsync(table).AwaitTask();

		/// <summary>Run a report, with a static name.</summary>
		/// <param name="table"></param>
		[LuaMember]
		public void DebugReport(LuaTable table)
		{
			table["DebugOutput"] = new Action<string>(text => Log.LogMsg(LogMsgType.Debug, text));
			var resultFile = RunReportAsync(table).AwaitTask();

			// move to a unique name
			var moveFileTo = table.GetMemberValue("name") as string;
			var p = moveFileTo.LastIndexOfAny(new char[] { '/', '\\' });
			moveFileTo = Path.Combine(Path.GetDirectoryName(resultFile), moveFileTo.Substring(p + 1)) + ".pdf";
			if (File.Exists(moveFileTo))
				File.Delete(moveFileTo);
			File.Move(resultFile, moveFileTo);
		} // proc DebugReport

		private static SimpleDataRow GetRowContent(LuaTable table)
		{
			var columns = new List<SimpleDataColumn>();
			var values = new List<object>();
			foreach (var c in table.Members)
			{
				columns.Add(new SimpleDataColumn(c.Key, c.Value.GetType()));
				values.Add(c.Value);
			}
			return new SimpleDataRow(values.ToArray(), columns.ToArray());
		} // func GetRowContent

		/// <summary></summary>
		/// <param name="table"></param>
		/// <returns></returns>
		/// <remarks>return DebugEmitter { converter = "markdown", columnName = "test", row = { test = [[ Hallo **Welt**!]] } }</remarks>
		/// <remarks>return DebugEmitter { converter = "image", columnName = "test", row = { test = 57006 } }</remarks>
		[LuaMember]
		public string DebugEmitter(LuaTable table)
		{
			var arguments = table.GetOptionalValue("converter", (string)null);
			var columnName = table.GetOptionalValue("columnName", (string)null);
			// create row
			var row = GetRowContent((table.GetMemberValue("row") as LuaTable) ?? throw new ArgumentNullException("row", "Row information is missing."));

			using (var session = reporting.CreateDebugReportSession())
			using (var sw = new StringWriter())
			using (var xml = XmlWriter.Create(sw, new XmlWriterSettings() { NewLineHandling = NewLineHandling.Entitize, IndentChars = "  ", Async = true }))
			{
				// emit column
				var emitter = session.CreateColumnEmitter(arguments, columnName, row);
				xml.WriteStartElement(emitter.ElementName);
				emitter.WriteAsync(xml, row).AwaitTask();
				xml.WriteEndElement();
				xml.Flush();

				return sw.GetStringBuilder().ToString();
			}
		} // func DebugEmitter

		/// <summary>Run a report.</summary>
		/// <param name="table"></param>
		/// <returns></returns>
		[LuaMember]
		public Task<string> RunReportAsync(LuaTable table)
			=> reporting.RunReportAsync(table);

		/// <summary>Run a report data only.</summary>
		/// <param name="table"></param>
		/// <returns></returns>
		[LuaMember]
		public Task<string> RunDataAsync(LuaTable table)
			=> reporting.RunDataAsync(table);

		[DEConfigHttpAction("report")]
		private void HttpRunReport(IDEWebRequestScope r)
		{
			var reportName = r.GetProperty("name", null);
			if (reportName == null)
				throw new ArgumentNullException("name", "Report name is missing.");
			// enforce user context
			var user = r.GetUser<IPpsPrivateDataContext>();

			try
			{
				// collection arguments
				var properties = new List<KeyValuePair<string, object>>();
				foreach (var p in r.ParameterNames)
				{
					var v = r.GetProperty(p, (object)null);
					if (v != null)
						properties.Add(new KeyValuePair<string, object>(p, v));
				}

				// execute report
				var resultInfo = reporting.RunReportAsync(reportName, properties.ToArray()).AwaitTask();
				r.OutputHeaders["x-ppsn-reportname"] = reportName;
				r.WriteFile(resultInfo, MimeTypes.Application.Pdf);
			}
			catch (Exception e)
			{
				Log.Except(e);
				throw;
			}
		} // proc HttpRunReport

		/// <summary>Access to the report engine.</summary>
		[LuaMember]
		public PpsReportEngine Reports => reporting;

		#endregion
		
		/// <summary></summary>
		/// <param name="database"></param>
		public void FireDataChangedEvent(string database)
			=> FireEvent("ppsn_database_changed", database);

		private XElement GetMimeTypesInfo()
		{
			var x = new XElement("mimeTypes");

			foreach (var cur in MimeTypeMapping.Mappings)
			{
				x.Add(new XElement("mimeType",
					new XAttribute("id", cur.MimeType),
					Procs.XAttributeCreate("isCompressedContent", cur.IsCompressedContent),
					Procs.XAttributeCreate("extensions", String.Join(";", cur.Extensions))
				));
			}

			return x;
		} // func GetMimeTypesInfo

		/// <summary></summary>
		/// <param name="r"></param>
		/// <returns></returns>
		protected override async Task<bool> OnProcessRequestAsync(IDEWebRequestScope r)
		{
			switch (r.RelativeSubPath)
			{
				case "info.xml":
					await Task.Run(() => r.WriteObject(
						new XElement("ppsn",
							new XAttribute("displayName", DisplayName),
							new XAttribute("version", "1.0.0.0"),
							new XAttribute("loginSecurity", "NTLM,Basic"),
							GetMimeTypesInfo()
						)
					));
					return true;
				case "login.xml":
					r.DemandToken(SecurityUser);
					
					var ctx = r.GetUser<IPpsPrivateDataContext>();
					await Task.Run(() =>
						{
							// basic login data
							var xLoginData = new XElement("user",
								new XAttribute("userId", ctx.UserId),
								new XAttribute("displayName", ctx.UserName)
							);

							// update optional values
							if (ctx.TryGetProperty<long>(UserContextKtKtId, out var ktktId))
								xLoginData.SetAttributeValue(UserContextKtKtId, ktktId.ChangeType<string>());
							if (ctx.TryGetProperty<long>(UserContextPersId, out var persId))
								xLoginData.SetAttributeValue(UserContextPersId, persId.ChangeType<string>());
							if (ctx.TryGetProperty(UserContextFullName, out var fullName))
								xLoginData.SetAttributeValue(UserContextFullName, fullName);
							if (ctx.TryGetProperty(UserContextInitials, out var initials))
								xLoginData.SetAttributeValue(UserContextInitials, initials);

							// execute script based extensions
							var t = new LuaTable();
							CallMemberDirect("OnExtentLogin", new object[] { ctx, t }, ignoreNilFunction: true);
							foreach (var kv in t.Members)
								xLoginData.SetAttributeValue(kv.Key, kv.Value);
							
							r.WriteObject(xLoginData);
						}
					);
					return true;
				default:
					return await base.OnProcessRequestAsync(r);
			}
		} // proc OnProcessRequest
	} // class PpsApplication
}
