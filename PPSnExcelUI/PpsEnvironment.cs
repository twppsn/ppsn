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
using System.Data;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Neo.IronLua;
using TecWare.DE.Data;
using TecWare.DE.Networking;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;

namespace TecWare.PPSn
{
	/// <summary>Special environment for data warehouse applications</summary>
	public sealed class PpsEnvironment : IPpsShell
	{
		private readonly IPpsShell baseShell;
		private readonly PpsEnvironmentInfo info;
		private readonly LuaTable luaLibrary;

		private string fullName = null;
		private BaseWebRequest request = null;

		public PpsEnvironment(IPpsShell baseShell, PpsEnvironmentInfo info)
		{
			this.baseShell = baseShell;
			this.info = info;
			this.luaLibrary = new LuaGlobal(baseShell.Lua);
		} // ctor

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
					var newRequest = new BaseWebRequest(info.Uri, Encoding, login.GetCredentials());
					// 			request.Headers.Add("des-multiple-authentifications", "true");
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
						if (e is WebException we)
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

		public IEnumerable<IDataRow> GetViewData(PpsShellGetList arguments)
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

		public void BeginInvoke(Action action)
			=> baseShell.BeginInvoke(action);

		public Task InvokeAsync(Action action)
			=> baseShell.InvokeAsync(action);

		public Task<T> InvokeAsync<T>(Func<T> func)
			=> baseShell.InvokeAsync(func);

		public void Await(Task task)
			=> baseShell.Await(task);

		public T Await<T>(Task<T> task)
			=> baseShell.Await(task);

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

		public Task ShowExceptionAsync(ExceptionShowFlags flags, Exception exception, string alternativeMessage = null)
			=> baseShell.ShowExceptionAsync(flags, exception, alternativeMessage);

		public Task ShowMessageAsync(string message)
			=> baseShell.ShowMessageAsync(message);

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

		/// <summary></summary>
		public SynchronizationContext Context => baseShell.Context;

		public Lua Lua => baseShell.Lua;
		public LuaTable LuaLibrary => luaLibrary;
		public Uri BaseUri => request?.BaseUri ?? info.Uri;
		public Encoding Encoding => baseShell.Encoding;
	} // class PpsEnvironment
}
