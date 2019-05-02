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
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using Microsoft.Win32;
using Neo.IronLua;
using TecWare.DE.Data;
using TecWare.DE.Networking;
using TecWare.DE.Stuff;
using TecWare.PPSn.Controls;
using TecWare.PPSn.Data;

namespace TecWare.PPSn
{
	#region -- interface IPpsFormsApplication -----------------------------------------

	public interface IPpsFormsApplication : ISynchronizeInvoke, IWin32Window, IPpsProgressFactory
	{
		void Await(Task t);

		/// <summary>Internal application-id for the server.</summary>
		string ApplicationId { get; }
		/// <summary>Title of the application.</summary>
		string Title { get; }

		SynchronizationContext SynchronizationContext { get; }
	} // interface IPpsFormsApplication

	#endregion

	#region -- enum PpsLoginResult ----------------------------------------------------

	public enum PpsLoginResult
	{
		Canceled,
		Sucess,
		Restart
	} // enum PpsLoginResult

	#endregion

	#region -- class PpsEnvironment ---------------------------------------------------

	/// <summary>Special environment for data warehouse applications</summary>
	public sealed class PpsEnvironment : PpsShell
	{
		private readonly IPpsFormsApplication formsApplication;
		private readonly PpsEnvironmentInfo info;

		private string fullName = null;
		private DEHttpClient request = null;

		public PpsEnvironment(Lua lua, IPpsFormsApplication formsApplication, PpsEnvironmentInfo info)
			: base(lua)
		{
			this.formsApplication = formsApplication;
			this.info = info;
		} // ctor

		#region -- Login --------------------------------------------------------------

		private static Version GetExcelAssemblyVersion()
		{
			var fileVersion = typeof(PpsEnvironment).Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>();
			return fileVersion == null ? new Version(1, 0, 0, 0) : new Version(fileVersion.Version);
		} // func GetExcelAssemblyVersion

		private static Version GetExcelAddinVersion()
		{
			using (var reg = Registry.CurrentUser.OpenSubKey(@"Software\TecWare\PPSnExcel\Components", false))
			{
				return reg?.GetValue(null) is string v
					? new Version(v)
					: new Version(1, 0, 0, 0);
			}
		} // func GetExcelAddinVersion

		public void ClearCredentials()
		{
			request = null;
			fullName = null;
		} // proc ClearCredentials

		public async Task<PpsLoginResult> LoginAsync(IWin32Window parent)
		{
			var newRequest = DEHttpClient.Create(info.Uri, null, Encoding);
			try
			{
				// get app-info
				var xInfo = await newRequest.GetXmlAsync("info.xml?app=" + formsApplication.ApplicationId);

				// check version
				var clientVersion = GetExcelAddinVersion();
				var assemblyVesion = GetExcelAssemblyVersion();
				var serverVersion = new Version(xInfo.GetAttribute("version", "1.0.0.0"));

#if DEBUG
				assemblyVesion = serverVersion = clientVersion;
#endif

				if (clientVersion < serverVersion) // new version is provided
				{
					return await UpdateApplicationAsync(newRequest, xInfo.GetAttribute("src", null))
						? PpsLoginResult.Restart
						: PpsLoginResult.Canceled;
				}
				else if (assemblyVesion < clientVersion) // new version is installed, but not active
				{
					return AskForRestart()
						? PpsLoginResult.Restart
						: PpsLoginResult.Canceled;
				}

				// update mime type mappings
				PpsShellExtensions.UpdateMimeTypesFromInfo(xInfo);

				// open trust stroe
				using (var login = new PpsClientLogin("ppsn_env:" + info.Uri.ToString(), info.Name, false))
				{
					var loginCounter = 0; // first time, do not show login dialog
					while (true)
					{
						// show login dialog
						if (loginCounter > 0)
						{
							if (!await InvokeAsync(() => login.ShowWindowsLogin(parent.Handle)))
								return PpsLoginResult.Canceled;
						}

						// create new client request
						loginCounter++;
						newRequest?.Dispose();
						newRequest = DEHttpClient.Create(info.Uri, login.GetCredentials(true), Encoding);

						// try login with user
						try
						{
							// execute login
							var xLogin = await newRequest.GetXmlAsync("login.xml");
							request = newRequest;
							fullName = xLogin.GetAttribute("FullName", (string)null);

							login.Commit();
							return PpsLoginResult.Sucess;
						}
						catch (HttpResponseException e)
						{
							switch (e.StatusCode)
							{
								case HttpStatusCode.Unauthorized:
									if (loginCounter > 1)
										ShowMessage("Passwort oder Nutzername falsch.");
									break;
								default:
									formsApplication.ShowMessage(String.Format("Verbindung mit fehlgeschlagen.\n{0} ({2} {1})", e.Message, e.StatusCode, (int)e.StatusCode), MessageBoxIcon.Error);
									return PpsLoginResult.Canceled;
							}
						}
					}
				}
			}
			catch (HttpRequestException e)
			{
				newRequest?.Dispose();
				if (e.InnerException is WebException we)
				{
					switch (we.Status)
					{
						case WebExceptionStatus.Timeout:
						case WebExceptionStatus.ConnectFailure:
							ShowMessage("Server nicht erreichbar.");
							return PpsLoginResult.Canceled;
					}
				}
				else
					ShowException(ExceptionShowFlags.None, e, "Verbindung fehlgeschlagen.");
				return PpsLoginResult.Canceled;
			}
			catch
			{
				newRequest?.Dispose();
				throw;
			}
		} // func LoginAsync

		#endregion

		#region -- Http ---------------------------------------------------------------

		public override DEHttpClient CreateHttp(Uri uri = null)
		{
			return uri == null
				? request
				: DEHttpClient.Create(new Uri(request.BaseAddress, uri), request.Credentials, request.DefaultEncoding);
		} // func CreateHttp

		public Task<XElement> GetXmlData(string requestUri)
			=> request.GetXmlAsync(requestUri);

		public override IEnumerable<IDataRow> GetViewData(PpsShellGetList arguments)
			=> request.CreateViewDataReader(arguments.ToQuery());

		public async Task<DataTable> GetViewDataAsync(PpsShellGetList arguments)
		{
			var dt = new DataTable();
			using (var e = (await Task.Run(() => GetViewData(arguments))).GetEnumerator())
			{
				while (await Task.Run(new Func<bool>(e.MoveNext)))
				{
					if (dt.Columns.Count == 0)
					{

						for (var i = 0; i < e.Current.Columns.Count; i++)
						{
							var col = e.Current.Columns[i];
							dt.Columns.Add(new DataColumn(col.Name, col.DataType));
						}
					}

					var r = dt.NewRow();
					for (var i = 0; i < dt.Columns.Count; i++)
						r[i] = e.Current[i] ?? DBNull.Value;

					dt.Rows.Add(r);
				}
			}
			return dt.Columns.Count == 0 ? null : dt;
		} // func GetViewDataAsync

		public override DEHttpClient Request => request;

		#endregion

		#region -- Synchronization ----------------------------------------------------

		public override void BeginInvoke(Action action)
		{
			if (InvokeRequired)
				formsApplication.BeginInvoke(action, EmptyArgs);
			else
				action();
		} // proc BeginInvoke

		public void Invoke(Action action)
		{
			if (InvokeRequired)
				formsApplication.Invoke(action, EmptyArgs);
			else
				action();
		} // proc Invoke

		public T Invoke<T>(Func<T> func)
		{
			if (InvokeRequired)
				return (T)formsApplication.Invoke(func, EmptyArgs);
			else
				return func();
		} // proc Invoke

		public override void Await(Task task)
			=> formsApplication.Await(task);

		public override Task InvokeAsync(Action action)
		{
			if (InvokeRequired)
			{
				return Task.Factory.FromAsync(
					formsApplication.BeginInvoke(action, EmptyArgs),
					formsApplication.EndInvoke
				);
			}
			else
			{
				action();
				return Task.CompletedTask;
			}
		} // func InvokeAsync

		public override Task<T> InvokeAsync<T>(Func<T> func)
		{
			if (InvokeRequired)
			{
				return Task.Factory.FromAsync(
					formsApplication.BeginInvoke(func, EmptyArgs),
					ar => (T)formsApplication.EndInvoke(ar)
				);
			}
			else
				return Task.FromResult(func());
		} // func InvokeAsync

		public void ContinueCatch(Task task)
		{
			task.ContinueWith(
				t => Await(ShowExceptionAsync(ExceptionShowFlags.None, t.Exception)),
				TaskContinuationOptions.OnlyOnFaulted
			);
		} // func ContinueCatch

		public void Spawn(Func<Task> task)
		{
			Task.Run(task);
		} // proc Spawn

		public void Spawn(Action action)
		{
			Task.Run(action).ContinueWith(
				t => Await(ShowExceptionAsync(ExceptionShowFlags.None, t.Exception)),
				TaskContinuationOptions.OnlyOnFaulted
			);
		} // proc Spawn

		private bool InvokeRequired => formsApplication.InvokeRequired; // Thread.CurrentThread.ManagedThreadId != mainThreadId;

		public override SynchronizationContext Context => formsApplication.SynchronizationContext;

		#endregion

		#region -- ShowException, ShowMessage -----------------------------------------

		public override void ShowException(ExceptionShowFlags flags, Exception exception, string alternativeMessage = null)
			=> formsApplication.ShowException(flags, exception, alternativeMessage);

		public override void ShowMessage(string message)
			=> formsApplication.ShowMessage(message);

		protected override IPpsProgress CreateProgressCore(bool blockUI)
			=> formsApplication.CreateProgress(blockUI);

		private bool AskForRestart()
			=> formsApplication.ShowMessage(String.Format("Die Anwenundung muss für eine Aktivierung neugestartet werden.\nNeustart von {0} sofort einleiten?", formsApplication.Title), MessageBoxIcon.Question, MessageBoxButtons.YesNo) == DialogResult.Yes;

		private async Task<bool> UpdateApplicationAsync(DEHttpClient client, string src)
		{
			if (formsApplication.ShowMessage(String.Format("Eine neue Anwendung steht bereit.\nInstallieren und {0} neustarten?", formsApplication.Title), MessageBoxIcon.Question, MessageBoxButtons.YesNo) != DialogResult.Yes)
				return false;

			var sourceUri = client.CreateFullUri(src);
			var msiExe = Path.Combine(Environment.SystemDirectory, "msiexec.exe");
			var psi = new ProcessStartInfo(msiExe, "/i " + sourceUri.ToString() + " /b");
			using (var ps = Process.Start(psi))
			{
				await Task.Run(new Action(ps.WaitForExit));
				// ps.ExitCode
			}

			return true;
		} // proc UpdateApplicationAsync

		#endregion

		/// <summary>Edit the current table</summary>
		/// <param name="table"></param>
		public void EditTable(IPpsTableData table)
		{
			if (table == null)
				throw new ArgumentNullException(nameof(table));

			using (var frm = new TableInsertForm(this))
			{
				frm.LoadData(table);
				frm.ShowDialog(formsApplication);
			}
		} // void EditTable

		/// <summary>Name of the environment.</summary>
		public string Name => info.Name;
		/// <summary>Authentificated user.</summary>
		public string UserName => Credentials is null ? null : fullName ?? PpsEnvironmentInfo.GetUserNameFromCredentials(request.Credentials);

		/// <summary>Access the environment information block.</summary>
		public PpsEnvironmentInfo Info => info;
		/// <summary>Login information</summary>
		public ICredentials Credentials => request?.Credentials;
		/// <summary>Has this environment credentials.</summary>
		public bool IsAuthentificated => !(request?.Credentials is null);

		public override Encoding Encoding => Encoding.UTF8;

		public override IPpsLogger Log => DebugLogger;

		private static object[] EmptyArgs { get; } = Array.Empty<object>();
	} // class PpsEnvironment

	#endregion
}
