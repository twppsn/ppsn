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
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Data;
using TecWare.DE.Networking;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;

namespace TecWare.PPSn
{
	#region -- interface IPpsFormsApplication -----------------------------------------

	public interface IPpsFormsApplication : ISynchronizeInvoke, IWin32Window, IPpsProgressFactory
	{
		void Await(Task t);

		SynchronizationContext SynchronizationContext { get; }
	} // interface IPpsFormsApplication

	#endregion

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

		public void ClearCredentials()
		{
			request = null;
			fullName = null;
		} // proc ClearCredentials

		public async Task<bool> LoginAsync(IWin32Window parent)
		{
			using (var login = new PpsClientLogin("ppsn_env:" + info.Uri.ToString(), info.Name, false))
			{
				var showLoginDialog = false;

				while (true)
				{
					// show login dialog
					if (showLoginDialog)
					{
						if (!await InvokeAsync(() => login.ShowWindowsLogin(parent.Handle)))
							return false;
					}

					// try login with user
					var newRequest = DEHttpClient.Create(info.Uri, login.GetCredentials(), Encoding);
					try
					{
						var xLogin = await newRequest.GetXmlAsync("login.xml");
						fullName = xLogin.GetAttribute("FullName", (string)null);

						request = newRequest;
						login.Commit();
						return true;
					}
					catch (Exception e)
					{
						if (e is HttpRequestException he
							&& he.InnerException is WebException we)
						{
							switch (we.Status)
							{
								case WebExceptionStatus.Timeout:
								case WebExceptionStatus.ConnectFailure:
									await ShowMessageAsync("Verbindung zum Server konnte nicht hergestellt werden.");
									return false;

								case WebExceptionStatus.ProtocolError:
									login.ShowErrorMessage = true;
									break;
								default:
									await ShowExceptionAsync(ExceptionShowFlags.None, e, "Verbindung fehlgeschlagen.");
									return false;
							}
						}
						else
						{
							await ShowExceptionAsync(ExceptionShowFlags.None, e);
							return false;
						}
					}
				}
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

		public override DEHttpClient Request => throw new NotImplementedException();

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

		public void Spawn(Func<Task> task)
		{
			Task.Run(task).ContinueWith(
				t => Await(ShowExceptionAsync(ExceptionShowFlags.None, t.Exception)),
				TaskContinuationOptions.OnlyOnFaulted
			);
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

		#endregion

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
}
