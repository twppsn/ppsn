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
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Networking;
using TecWare.DE.Server;
using TecWare.DE.Server.Http;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;
using TecWare.PPSn.Server.Data;
using TecWare.PPSn.Server.Sql;

namespace TecWare.PPSn.Server
{
	#region -- interface IPpsApplicationInitialization ----------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public interface IPpsApplicationInitialization : IDEConfigItem
	{
		/// <summary>Register a one time task, that will be run durring initialization.</summary>
		/// <param name="order"></param>
		/// <param name="status"></param>
		/// <param name="task"></param>
		void RegisterInitializationTask(int order, string status, Func<Task> task);

		bool? WaitForInitializationProcess(int timeout = -1);

		/// <summary></summary>
		bool IsInitializedSuccessful { get; }
	} // interface IPpsApplicationInitialization

	#endregion

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Base service provider, for all pps-moduls:
	/// - user administration
	/// - data cache, for commonly used data or states
	/// - view services (executes and updates all views, to data)
	/// </summary>
	public partial class PpsApplication : DEConfigLogItem, IPpsApplicationInitialization
	{
		#region -- struct InitializationTask ------------------------------------------------

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
		private Task initializationProcess = null;				// initialization process
		private bool isInitializedSuccessful = false;     // is the system initialized properly

		private List<InitializationTask> initializationTasks = new List<InitializationTask>(); // Action that should be done in the initialization process

		#region -- Ctor/Dtor --------------------------------------------------------------

		public PpsApplication(IServiceProvider sp, string name)
			: base(sp, name)
		{
			initializationProgress = new SimpleConfigItemProperty<string>(this, "ppsn_init_progress", "Initialization", "Misc", "Show the current state of the initialization of the node.", null, "Pending");

			this.objectsLibrary =  new PpsObjectsLibrary(this);
			this.httpLibrary = new PpsHttpLibrary(this);

			// register shortcut for text
			LuaType.RegisterTypeAlias("text", typeof(PpsFormattedStringValue));
			LuaType.RegisterTypeAlias("blob", typeof(byte[]));

			InitData();
			InitUser();
		} // ctor

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
		} // proc OnBeginReadConfiguration

		protected override void OnEndReadConfiguration(IDEConfigLoading config)
		{
			base.OnEndReadConfiguration(config);

			// set the configuration
			BeginEndConfigurationData(config);
			BeginEndConfigurationUser(config);

			// restart main thread
			initializationProcess = Task.Run(new Action(InitializeApplication));
		} // proc OnEndReadConfiguration

		protected override void Dispose(bool disposing)
		{
			try
			{
				UpdateInitializationState("Shuting down");

				DoneUser();
				DoneData();

				initializationProgress.Dispose();
			}
			finally
			{
				base.Dispose(disposing);
			}
		} // proc Dispose

		#endregion

		#region -- Init -------------------------------------------------------------------

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
				}
			}
		} // proc InitializeApplication
		
		/// <summary>Wait for initialization of the system, initialization is processed synchron.</summary>
		/// <param name="timeout"></param>
		/// <returns></returns>
		public bool? WaitForInitializationProcess(int timeout = -1)
			=> initializationProcess.Wait(timeout) ? new bool?(isInitializedSuccessful) : null;

		[LuaMember(nameof(RegisterInitializationAction))]
		public void RegisterInitializationAction(int order, string status, Action task)
			=> RegisterInitializationTask(order, status, () => Task.Run(task));

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

		public bool IsInitializedSuccessful => isInitializedSuccessful;

		#endregion

		protected override bool OnProcessRequest(IDEContext r)
		{
			switch (r.RelativeSubPath)
			{
				case "info.xml":
					r.WriteObject(
						new XElement("ppsn",
							new XAttribute("displayName", DisplayName),
							new XAttribute("version", "1.0.0.0"),
							new XAttribute("loginSecurity", "NTLM,Basic")
						)
					);
					return true;
				case "login.xml":
					// r.DemandToken("USER");

					var ctx = r.GetUser<IPpsPrivateDataContext>();
					r.WriteObject(
						new XElement("user",
							new XAttribute("displayName", ctx.UserName)
						)
					);
					return true;
				default:
					return base.OnProcessRequest(r);
			}
		} // proc OnProcessRequest
	} // class PpsApplication
}
