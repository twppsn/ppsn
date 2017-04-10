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
using System.Security.Claims;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Neo.IronLua;
using TecWare.DE.Data;
using TecWare.DE.Server;
using TecWare.DE.Server.Http;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;
using TecWare.PPSn.Server.Data;

namespace TecWare.PPSn.Server
{
	public partial class PpsApplication
	{
		private const int NoUserId = -1;
		private const int SysUserId = Int32.MinValue;

		#region -- class PrivateUserData --------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary>This class holds all information for a currently inactive user.</summary>
		private sealed class PrivateUserData : LuaTable, IDEUser, IDisposable
		{
			private readonly PpsApplication application;
			private readonly string connectionName;
			private readonly ReaderWriterLockSlim connectionLock; // locks the user object

			private readonly long userId;           // unique id of the user
			private LoggerProxy log;                // current log interface for the user
			private string fullName = null;         // full name of the user
			private int currentVersion = -1;
			private string[] securityTokens = null; // access rights

			private PpsUserIdentity userIdentity = null; // user identity for the authentification
			private PpsUserIdentity localIdentity = null; // user identity to access resources on the server

			private int lastAccess;   // last dispose of the last active context

			private readonly List<IPpsConnectionHandle> connections = new List<IPpsConnectionHandle>(); // current connections within the sources
			private readonly List<WeakReference<PrivateUserDataContext>> currentContexts = new List<WeakReference<PrivateUserDataContext>>(); // current active user contexts

			#region -- Ctor/Dtor/Idle -------------------------------------------------------

			public PrivateUserData(PpsApplication application, long userId, string connectionName)
			{
				this.application = application ?? throw new ArgumentNullException(nameof(application));
				this.userId = userId;
				this.connectionName = connectionName ?? throw new ArgumentNullException(nameof(connectionName));
				this.connectionLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);

				if (userId == SysUserId) // mark system user
				{
					userIdentity =
						localIdentity = PpsUserIdentity.System;
					fullName = "System";
				}


				CreateLogger();

				if (userId > 0)
					application.Server.RegisterUser(this); // register the user in http-server
			} // ctor

			public void Dispose()
			{
				// unregister the user
				if (userId > 0)
					application.Server.UnregisterUser(this);

				// enter write lock
				connectionLock.TryEnterWriteLock(30000); // force dispose after 30s

				// dispose lock
				connectionLock.Dispose();

				// dispose identity
				userIdentity.Dispose();

				// dispose connections
				CloseConnections();
			} // proc Dispose

			public void Idle()
			{
				if (IsActive && IsExpired)
					CloseConnections();
			} // proc Idle

			#endregion

			#region -- Connections ----------------------------------------------------------

			private IPpsConnectionHandle GetMainConnectionHandle()
				=> GetConnectionHandle(application.MainDataSource);

			private int FindConnectionIndexBySource(PpsDataSource source)
				=> connections.FindIndex(c => c.DataSource == source);

			public IPpsConnectionHandle GetConnectionHandle(PpsDataSource source)
			{
				lock (connections)
				{
					var idx = FindConnectionIndexBySource(source);
					return idx >= 0 ? connections[idx] : null;
				}
			} // func GetConnectionHandle

			public void UpdateConnectionHandle(PpsDataSource source, IPpsConnectionHandle handle)
			{
				lock (connections)
				{
					var idx = FindConnectionIndexBySource(source);
					if (idx >= 0)
					{
						if (connections[idx].IsConnected)
							throw new ArgumentException("Connection already registered.");
						else
							connections.RemoveAt(idx);
					}

					// add the connection
					connections.Add(handle);
					handle.Disposed += Handle_Disposed;
				}
			} // proc UpdateConnectionHandle

			private void Handle_Disposed(object sender, EventArgs e)
			{
				lock (connections)
				{
					var idx = connections.FindIndex(c => c == sender);
					if (idx >= 0)
						connections.RemoveAt(idx);
				}
			} // func handle_Disposed

			private void CloseConnections()
			{
				lock (connections)
				{
					connections.ForEach(c => c.Dispose());
					connections.Clear();
				}
			} // proc CloseConnections

			#endregion

			#region -- AuthentUser, CreateContext -------------------------------------------

			private PrivateUserDataContext CreateContextIntern(IIdentity currentIdentity)
			{
				lock (currentContexts)
				{
					CleanCurrentContexts();

					if (currentContexts.Count == 0)
						connectionLock.EnterReadLock();

					var t = new PrivateUserDataContext(this, currentIdentity);
					currentContexts.Add(new WeakReference<PrivateUserDataContext>(t));
					return t;
				}
			} // func CreateContextIntern

			private void CleanCurrentContexts()
			{
				var inLock = currentContexts.Count > 0;

				for (var i = currentContexts.Count - 1; i >= 0; i--)
				{
					if (!currentContexts[i].TryGetTarget(out var t))
						currentContexts.RemoveAt(i);
				}

				if (inLock && currentContexts.Count == 0)
					connectionLock.ExitReadLock();
			} // proc CleanCurrentContexts

			internal void ContextDisposed(PrivateUserDataContext context)
			{
				lock (currentContexts)
				{
					CleanCurrentContexts();

					var idx = currentContexts.FindIndex(c => c.TryGetTarget(out var t) && t == context);
					if (idx != -1)
					{
						currentContexts.RemoveAt(idx);
						if (currentContexts.Count == 0)
							connectionLock.ExitReadLock();
					}

					lastAccess = Environment.TickCount;
				}
			} // proc ContextDisposed

			public IDEAuthentificatedUser Authentificate(IIdentity identity)
			{
				connectionLock.EnterReadLock();
				try
				{
					if (userIdentity == null) // is there a identity
						return null;
					if (!userIdentity.Equals(identity)) // check if that the identity matches
						return null;

					var mainConnectionHandle = GetMainConnectionHandle();
					if (mainConnectionHandle != null) // main connection is still active
						return CreateContextIntern(identity);
					else // no main connection create one
					{
						IPpsConnectionHandle newConnection = null;
						var context = CreateContextIntern(identity);
						try
						{
							// check the user information agains the main user
							newConnection = application.MainDataSource.CreateConnection(context);

							// ensure the database connection to the main database
							if (newConnection.EnsureConnection())
							{
								UpdateConnectionHandle(application.MainDataSource, newConnection);
								return context;
							}
							else
								throw new Exception("Connection lost.");
						}
						catch (Exception e)
						{
							log.Except(e);
							newConnection?.Dispose();
							context.Dispose();
							return null;
						}
					}
				}
				finally
				{
					connectionLock.ExitReadLock();
				}
			} // func Authentificate

			public bool DemandToken(string securityToken)
			{
				if (String.IsNullOrEmpty(securityToken) || userId == SysUserId)
					return true;

				lock (connections)
					return Array.BinarySearch(securityTokens, securityToken.ToLower()) >= 0;
			} // func DemandToken

			#endregion

			#region -- UpdateData -----------------------------------------------------------

			private void CreateLogger()
			{
				var loggerName = userIdentity == null
					? "User" + userId.ToString()
					: userIdentity.Name;

				log = LoggerProxy.Create(application, loggerName);
			} // proc CreateLogger

			public void UpdateData(IDataRow r)
			{
				string GetString(string fieldName)
					=> r.GetProperty(fieldName, (string)null) ?? throw new ArgumentNullException($"{fieldName} is null.");

				// check if we need a reload
				var loginVersion = r.GetProperty("LoginVersion", 0);
				if (loginVersion == currentVersion)
					return;

				// create the user
				var userType = r.GetProperty("LoginType", (string)null);
				if (userType == "U") // windows login
				{
					userIdentity = PpsUserIdentity.CreateIntegratedIdentity(GetString("Login"));
					localIdentity = application.systemUser.userIdentity; // currently service is for local stuff
				}
				else if (userType == "S") // sql login
				{
					userIdentity = PpsUserIdentity.CreateBasicIdentity(
						GetString("Login"),
						GetString("LoginHash")
					);

					localIdentity = application.systemUser.userIdentity; // currently service is for local stuff
				}
				else
					throw new ArgumentException($"Unsupported login type '{userType}'.");

				this.fullName = r.GetProperty("Name", userIdentity.Name);
				this.securityTokens = application.Server.BuildSecurityTokens(r.GetProperty("Security", "User"));
			} // proc UpdateData

			#endregion

			[LuaMember("App")]
			public PpsApplication Application => application;

			[LuaMember("UserId")]
			public long Id => userId;
			[LuaMember("UserName")]
			public string Name => userIdentity.Name;
			[LuaMember]
			public string FullName { get { return fullName ?? Name; } set { fullName = value; } }
			[LuaMember]
			public LoggerProxy Log => log;

			/// <summary></summary>
			public PpsUserIdentity User => userIdentity;
			/// <summary></summary>
			public PpsUserIdentity LocalIdentity => localIdentity;

			/// <summary>Are the user context expiered and should be cleared.</summary>
			public bool IsExpired => userId != SysUserId && unchecked(Environment.TickCount - lastAccess) > application.UserLease;
			/// <summary>Is the context active</summary>
			public bool IsActive
			{
				get
				{
					lock (currentContexts)
					{
						CleanCurrentContexts();
						return currentContexts.Count > 0;
					}
				}
			} // prop IsActive
		} // class PrivateUserData

		#endregion

		#region -- class PrivateUserDataContext -------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary>This class holds a active context for a user. It is possible to
		/// attach objects, that will be disposed on the end of the active process.</summary>
		private sealed class PrivateUserDataContext : IPpsPrivateDataContext, IDEAuthentificatedUser
		{
			private readonly PrivateUserData privateUser;
			private readonly IIdentity currentIdentity; // contains the plain identity token from the user

			#region -- Ctor/Dtor ------------------------------------------------------------

			public PrivateUserDataContext(PrivateUserData privateUser, IIdentity currentIdentity)
			{
				this.privateUser = privateUser ?? throw new ArgumentNullException(nameof(privateUser));
				this.currentIdentity = currentIdentity ?? throw new ArgumentNullException(nameof(currentIdentity));

				if (!currentIdentity.IsAuthenticated)
					throw new ArgumentException("Identity is not verified.", nameof(currentIdentity));

				if (currentIdentity is ClaimsIdentity c) // create a copy
					currentIdentity = c.Clone();
			} // ctor

			public void Dispose()
			{
				// dispose identity
				if (currentIdentity is IDisposable d)
					d.Dispose();

				// unregister context
				privateUser.ContextDisposed(this);
			} // proc Dispose

			#endregion

			#region -- Data Tasks -----------------------------------------------------------

			private IPpsConnectionHandle EnsureConnection(PpsDataSource source, bool throwException)
			{
				var c = privateUser.GetConnectionHandle(source);
				if (c == null)
					c = source.CreateConnection(this, throwException);

				return c != null && c.EnsureConnection(throwException) ? c : null;
			} // func EnsureConnection

			public PpsDataSelector CreateSelector(string name, string filter = null, string order = null, bool throwException = true)
			{
				if (String.IsNullOrEmpty(name))
					throw new ArgumentNullException("name");

				// todo: build a joined selector, doppelte spalten müssten entfernt werden, wenn man es machen will
				var viewInfo = Application.GetViewDefinition(name, throwException);
				if (viewInfo == null)
					return null;

				// ensure the connection
				var connectionHandle = EnsureConnection(viewInfo.SelectorToken.DataSource, throwException);
				if (connectionHandle == null)
				{
					if (throwException)
						throw new ArgumentException(); // todo;
					else
						return null;
				}

				// create the selector
				var selector = viewInfo.SelectorToken.CreateSelector(connectionHandle, throwException);

				// apply filter rules
				if (!String.IsNullOrWhiteSpace(filter))
					selector = selector.ApplyFilter(PpsDataFilterExpression.Parse(filter), viewInfo.LookupFilter);

				// apply order
				if (!String.IsNullOrWhiteSpace(order))
					selector = selector.ApplyOrder(PpsDataOrderExpression.Parse(order), viewInfo.LookupOrder);

				return selector;
			} // func CreateSelector

			public PpsDataSelector CreateSelector(LuaTable table)
			{
				if (table == null)
					throw new ArgumentNullException("table");

				return CreateSelector(table.GetOptionalValue("name", (string)null), table.GetOptionalValue("filter", (string)null), table.GetOptionalValue("order", (string)null), table.GetOptionalValue("throwException", true));
			} // func CreateSelector

			public PpsDataTransaction CreateTransaction(string dataSourceName = null, bool throwException = true)
			{
				if (String.IsNullOrEmpty(dataSourceName))
					return CreateTransaction((PpsDataSource)null, throwException);

				var dataSource = Application.GetDataSource(dataSourceName, throwException);
				if (dataSource == null)
					return null;

				return CreateTransaction(dataSource, throwException);
			} // func CreateTransaction

			public PpsDataTransaction CreateTransaction(PpsDataSource dataSource = null, bool throwException = true)
			{
				dataSource = dataSource ?? MainDataSource;

				// create the connection
				var c = EnsureConnection(dataSource, throwException);
				if (c == null)
					return null;

				return dataSource.CreateTransaction(c);
			} // func CreateTransaction

			#endregion

			public PpsCredentials GetNetworkCredential()
				=> privateUser.User.GetCredentialsFromIdentity(currentIdentity);

			public PpsIntegratedCredentials GetLocalCredentials()
				=> (PpsIntegratedCredentials)privateUser.LocalIdentity.GetCredentialsFromIdentity(currentIdentity);

			public bool IsInRole(string role)
				=> privateUser.DemandToken(role);

			public bool TryGetProperty(string name, out object value)
			{
				value = privateUser.GetMemberValue(name);
				return value != null;
			} // func TryGetProperty

			public object GetService(Type serviceType)
			{
				if (serviceType == typeof(IPpsPrivateDataContext))
					return this;
				else
					return null;
			} // func GetService

			public long UserId => privateUser.Id;
			public string UserName => privateUser.Name;

			IIdentity IPrincipal.Identity => currentIdentity;
			PpsUserIdentity IPpsPrivateDataContext.Identity => privateUser.User;

			public PpsApplication Application => privateUser.Application;
			public PpsDataSource MainDataSource => Application.MainDataSource;
		} // class PrivateUserDataContext

		#endregion

		private PrivateUserData systemUser;
		private DEList<PrivateUserData> userList;

		#region -- Init/Done --------------------------------------------------------------

		private void InitUser()
		{
			systemUser = new PrivateUserData(this, SysUserId, "System");
			userList = new DEList<PrivateUserData>(this, "tw_users", "User list");
		} // proc InitUser

		private void BeginReadConfigurationUser(IDEConfigLoading config)
		{
		} // proc BeginReadConfigurationUser

		private void BeginEndConfigurationUser(IDEConfigLoading config)
		{
			// read the user data
			RegisterInitializationTask(11000, "Register users", () => Task.Run(new Action(RefreshUserData)));
		} // proc BeginEndConfigurationUser

		private void RefreshUserData()
		{
			bool UpdateUserData(PrivateUserData userData, IDataRow r)
			{
				try
				{
					userData.UpdateData(r);
					return true;
				}
				catch (Exception e)
				{
					userData.Log.Except(e);
					userData.Dispose();
					return false;
				}
			} // func UpdateUserData

			using (var ctx = CreateSysContext())
			{
				var users = ctx?.CreateSelector("dbo.serverLogins", throwException: false);
				if (users != null)
				{
					// fetch user list
					foreach (IDataRow u in users)
					{
						lock (userList)
						{
							var userId = u.GetProperty("ID", 0L);
							if (userId > 0)
							{
								var idx = userList.FindIndex(c => c.Id == userId);
								if (idx >= 0)
								{
									if (!UpdateUserData(userList[idx], u))
										userList.RemoveAt(idx);
								}
								else
								{
									var user = new PrivateUserData(this, userId, "User");
									if (UpdateUserData(user, u))
										userList.Add(user);
								}
							}
							else
								Log.Warn("User ignored (id={userId}).");
						}
					} // foreach
				} // users != null
			}
		} // proc RefreshUserData

		private void DoneUser()
		{
		} // proc DoneUser

		#endregion

		/// <summary>Creates a context for the system user.</summary>
		/// <returns></returns>
		[LuaMember(nameof(CreateSysContext))]
		public IPpsPrivateDataContext CreateSysContext()
			=> (IPpsPrivateDataContext)systemUser.Authentificate(PpsUserIdentity.System);

		/// <summary>Creates a context for the system user.</summary>
		/// <returns></returns>
		[LuaMember(nameof(GetUserContext))]
		public IPpsPrivateDataContext GetUserContext()
		{
			var ctx = DEContext.GetCurrentUser<IPpsPrivateDataContext>();
			if (ctx == null)
				throw new ArgumentNullException("No context.");
			return ctx;
		} // func CreateUserContext

		/// <summary>Creates a context for a special user.</summary>
		/// <param name="flags"></param>
		/// <param name="user"></param>
		/// <param name="connectionName"></param>
		/// <returns></returns>
		public IPpsPrivateDataContext CreateUserContext(int flags, IIdentity user, string connectionName)
		{
			throw new NotImplementedException();
		}

		public int UserLease => 650000; // todo in ms
	} // class PpsApplication
}
