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
using System.Threading.Tasks;
using Neo.IronLua;
using TecWare.DE.Data;
using TecWare.DE.Server;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;
using TecWare.PPSn.Server.Data;

namespace TecWare.PPSn.Server
{
	public partial class PpsApplication
	{
		/// <summary>User context optional wellknown property: Contact id.</summary>
		public const string UserContextKtKtId = "KtKtId";
		/// <summary>User context optional wellknown property: Personal id.</summary>
		public const string UserContextPersId = "PersId";
		/// <summary>User context optional wellknown property: Full name of the contact or user.</summary>
		public const string UserContextFullName = "FullName";
		/// <summary>User context optional wellknown property: Initals of the contact or user.</summary>
		public const string UserContextInitials = "Initials";

		private const int noUserId = -1;
		private const int sysUserId = Int32.MinValue;

		#region -- class PrivateUserData ----------------------------------------------

		/// <summary>This class holds all information for a currently inactive user.</summary>
		private sealed class PrivateUserData : LuaTable, IDEUser, IDisposable
		{
			#region -- class PooledConnection -----------------------------------------

			private sealed class PooledConnection
			{
				private readonly int createdAt;
				private IPpsConnectionHandle connection;
				private readonly string dataSourceName;

				public PooledConnection(IPpsConnectionHandle connection)
				{
					if (connection == null)
						throw new ArgumentNullException(nameof(connection));

					dataSourceName = connection.DataSource.Name;

					createdAt = Environment.TickCount;
					connection.Disposed += (sender, e) => Clear();

					this.connection = connection;
				} // ctor

				public void Clear()
				{
					if (connection.IsConnected)
						connection.Dispose();
					connection = null;
				} // proc Clear

				public bool IsAlive
					=> connection != null && connection.IsConnected && unchecked(Environment.TickCount - createdAt) < connection.DataSource.Application.ConnectionLease;
				
				public IPpsConnectionHandle Handle => connection;

				public int CreatedAt => createdAt;
				public string DataSourceName => dataSourceName;
			} // class PooledConnection

			#endregion

			private readonly PpsApplication application;

			private readonly long userId;           // unique id of the user
			private readonly LoggerProxy log;       // current log interface for the user
			private int currentVersion = -1;        // version of the user data
			private string[] securityTokens = null; // access rights

			private readonly PpsUserIdentity userIdentity = null;   // user identity for the authentification
			private PpsUserIdentity localIdentity = null;           // user identity to access resources on the server

			private readonly List<PooledConnection> pooledConnections = new List<PooledConnection>();
			private readonly List<WeakReference<PrivateUserDataContext>> currentContexts = new List<WeakReference<PrivateUserDataContext>>(); // current active user contexts

			#region -- Ctor/Dtor/Idle -------------------------------------------------

			public PrivateUserData(PpsApplication application, long userId, PpsUserIdentity userIdentity)
			{
				this.application = application ?? throw new ArgumentNullException(nameof(application));
				this.userId = userId;

				if (userId == sysUserId) // mark system user
				{
					if (userIdentity != null)
						throw new ArgumentException("UserIdentity must be null.", nameof(userIdentity));

					userIdentity =
						localIdentity = PpsUserIdentity.System;
					this[UserContextFullName] = "System";
				}
				this.userIdentity = userIdentity;

				log = LoggerProxy.Create(application, userIdentity.Name);

				application.Server.RegisterUser(this); // register the user in http-server
			} // ctor

			public void Dispose()
			{
				// unregister the user
				application.Server.UnregisterUser(this);

				// identity is not disposed, because is might used by pending contexts
			} // proc Dispose

			#endregion

			#region -- User connection pool -------------------------------------------

			private PooledConnection FindPooledConnection(PpsDataSource dataSource)
			{
				lock (pooledConnections)
				{
					for (var i = pooledConnections.Count - 1; i >= 0; i--)
					{
						var cur = pooledConnections[i];
						if (cur.IsAlive)
						{
							if (cur.Handle.DataSource == dataSource)
								return cur;
						}
						else
						{
							Log.Info("Remove pooled connection: {0} after {1:N0}", cur.DataSourceName, unchecked(Environment.TickCount - cur.CreatedAt));
							pooledConnections.RemoveAt(i);
						}
					}
					return null;
				}
			} // func FindPooledConnection

			internal IPpsConnectionHandle GetOrCreatePooledConnection(PpsDataSource dataSource, IPpsPrivateDataContext userData, bool throwException)
			{
				lock (pooledConnections)
				{
					var pooled = FindPooledConnection(dataSource);
					if (pooled == null)
					{
						var handle = dataSource.CreateConnection(userData, throwException);
						if (handle == null)
							return null;

						pooledConnections.Add(new PooledConnection(handle));

						Log.Info("New pooled connection: {0}", dataSource.Name);
						return handle;
					}
					else
					{
						//Log.Info("Reuse pooled connection: {0}", dataSource.Name);
						return pooled.Handle;
					}
				}
			} // func GetOrCreatePooledConnection

			#endregion

			#region -- AuthentUser, CreateContext -------------------------------------

			private PrivateUserDataContext CreateContextIntern(IIdentity currentIdentity)
			{
				lock (currentContexts)
				{
					RemoveContext(null);

					var t = new PrivateUserDataContext(this, currentIdentity);
					currentContexts.Add(new WeakReference<PrivateUserDataContext>(t));
					return t;
				}
			} // func CreateContextIntern

			internal void RemoveContext(PrivateUserDataContext context)
			{
				lock (currentContexts)
				{
					for (var i = currentContexts.Count - 1; i >= 0; i--)
					{
						if (!currentContexts[i].TryGetTarget(out var t)
							|| t.IsDisposed
							|| t == context)
						{
							currentContexts.RemoveAt(i);
						}
					}
				}
			} // proc RemoveContext

			public async Task<IDEAuthentificatedUser> AuthentificateAsync(IIdentity identity)
			{
				if (userIdentity == null) // is there a identity
					return null;
				if (!userIdentity.Equals(identity)) // check if that the identity matches
					return null;
								
				// check the user information agains the main user
				var context = CreateContextIntern(identity); // create new context for this identity
				try
				{
					var newConnection = GetOrCreatePooledConnection(application.MainDataSource, context, false);
					try
					{
						// ensure the database connection to the main database
						if (await newConnection.EnsureConnectionAsync(true))
							return context;
						else
						{
							context.Dispose();
							return null;
						}
					}
					catch (Exception e)
					{
						log.Except(e);
						newConnection?.Dispose();
						return null;
					}
				}
				catch(Exception e)
				{
					log.Except(e);
					context.Dispose();
					return null;
				}
			} // func Authentificate

			public bool DemandToken(string securityToken)
			{
				if (String.IsNullOrEmpty(securityToken) || userId == sysUserId)
					return true;
				else if (securityToken == "user")
					return true;

				return Array.BinarySearch(securityTokens, securityToken.ToLower()) >= 0;
			} // func DemandToken

			#endregion

			#region -- UpdateData -----------------------------------------------------

			public void UpdateData(IDataRow r)
			{
				// check if we need a reload
				var loginVersion = r.GetProperty("LoginVersion", 0);
				if (loginVersion == currentVersion)
					return;

				// currently service is for local stuff
				localIdentity = application.systemUser.userIdentity;
				currentVersion = loginVersion;

				// update optinal values
				SetMemberValue(UserContextFullName, r.GetProperty("Name", userIdentity.Name));
				if (r.TryGetProperty<long>(UserContextKtKtId, out var ktktId))
					SetMemberValue(UserContextKtKtId, ktktId);
				if (r.TryGetProperty<long>(UserContextPersId, out var persId))
					SetMemberValue(UserContextPersId, persId);
				if (r.TryGetProperty<string>(UserContextInitials, out var initials))
					SetMemberValue(UserContextInitials, initials);

				this.securityTokens = application.Server.BuildSecurityTokens(r.GetProperty("Security", "User"));
			} // proc UpdateData

			public static PpsUserIdentity CreateUserIdentity(IDataRow r)
			{
				string GetString(string fieldName)
					=> r.GetProperty(fieldName, (string)null) ?? throw new ArgumentNullException($"{fieldName} is null.");

				// create the user
				var userType = r.GetProperty("LoginType", (string)null);
				if (userType == "U") // windows login
					return PpsUserIdentity.CreateIntegratedIdentity(GetString("Login"));
				else if (userType == "S") // sql login
				{
					return PpsUserIdentity.CreateBasicIdentity(
						GetString("Login"),
						(byte[])r["LoginHash", true]
					);
				}
				else
					throw new ArgumentException($"Unsupported login type '{userType}'.");
			} // func CreateUserIdentity

			#endregion

			[LuaMember("App")]
			public PpsApplication Application => application;

			[LuaMember("UserId")]
			public long Id => userId;
			[LuaMember("UserName")]
			public string Name => userIdentity.Name;

			string IDEUser.DisplayName => userIdentity.Name;
			IIdentity IDEUser.Identity => userIdentity;

			[LuaMember]
			public LoggerProxy Log => log;

			/// <summary></summary>
			public PpsUserIdentity User => userIdentity;
			/// <summary></summary>
			public PpsUserIdentity LocalIdentity => localIdentity;
		} // class PrivateUserData

		#endregion

		#region -- class PrivateUserDataContext -----------------------------------------

		/// <summary>This class holds a active context for a user. It is possible to
		/// attach objects, that will be disposed on the end of the active process.</summary>
		private sealed class PrivateUserDataContext : IPpsPrivateDataContext, IDEAuthentificatedUser
		{
			private readonly PrivateUserData privateUser;
			private readonly IIdentity currentIdentity; // contains the plain identity token from the user
			private bool isDisposed = false;

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
				if (isDisposed)
					throw new ObjectDisposedException(nameof(PrivateUserData));

				isDisposed = true;

				// dispose identity
				if (currentIdentity is IDisposable d)
					d.Dispose();

				// unregister context
				privateUser.RemoveContext(this);
			} // proc Dispose

			#endregion

			#region -- Data Tasks -----------------------------------------------------------

			public async Task<IPpsConnectionHandle> EnsureConnectionAsync(PpsDataSource source, bool throwException)
			{
				var c = privateUser.GetOrCreatePooledConnection(source, this, throwException);
				if (c == null)
					c = source.CreateConnection(this, throwException);

				return c != null && await c.EnsureConnectionAsync(throwException) ? c : null;
			} // func EnsureConnection

			public async Task<PpsDataSelector> CreateSelectorAsync(string select, PpsDataColumnExpression[] columns, PpsDataFilterExpression filter = null, PpsDataOrderExpression[] order = null, bool throwException = true)
			{
				if (String.IsNullOrEmpty(select))
					throw new ArgumentNullException(nameof(select));

				// todo: build a joined selector, doppelte spalten müssten entfernt werden, wenn man es machen will
				var viewInfo = Application.GetViewDefinition(select, throwException);
				if (viewInfo == null)
					return null;

				// ensure the connection
				var connectionHandle = await EnsureConnectionAsync(viewInfo.SelectorToken.DataSource, throwException);
				if (connectionHandle == null)
				{
					if (throwException)
						throw new ArgumentException(); // todo;
					else
						return null;
				}

				// create the selector
				var selector = viewInfo.SelectorToken.CreateSelector(connectionHandle, throwException);

				// column restrictions
				if (!PpsDataColumnExpression.IsEmpty(columns))
					selector = selector.ApplyColumns(columns);

				// apply filter rules
				if (!PpsDataFilterExpression.IsEmpty(filter))
					selector = selector.ApplyFilter(filter, viewInfo.LookupFilter);

				// apply order
				if (!PpsDataOrderExpression.IsEmpty(order))
					selector = selector.ApplyOrder(order, viewInfo.LookupOrder);

				return selector;
			} // func CreateSelector
			
			public Task<PpsDataTransaction> CreateTransactionAsync(string dataSourceName = null, bool throwException = true)
			{
				if (String.IsNullOrEmpty(dataSourceName))
					return CreateTransactionAsync((PpsDataSource)null, throwException);

				var dataSource = Application.GetDataSource(dataSourceName, throwException);
				if (dataSource == null)
					return null;

				return CreateTransactionAsync(dataSource, throwException);
			} // func CreateTransaction

			public async Task<PpsDataTransaction> CreateTransactionAsync(PpsDataSource dataSource = null, bool throwException = true)
			{
				dataSource = dataSource ?? MainDataSource;

				// create the connection
				var c = await EnsureConnectionAsync(dataSource, throwException);
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

			public bool IsDisposed => isDisposed;

			IIdentity IPrincipal.Identity => currentIdentity;
			PpsUserIdentity IPpsPrivateDataContext.Identity => privateUser.User;

			/// <summary>Access the application object.</summary>
			public PpsApplication Application => privateUser.Application;
			/// <summary>Main data source for this user.</summary>
			public PpsDataSource MainDataSource => Application.MainDataSource;
		} // class PrivateUserDataContext

		#endregion
		
		private PrivateUserData systemUser;
		private DEList<PrivateUserData> userList;

		#region -- Init/Done --------------------------------------------------------------

		private void InitUser()
		{
			systemUser = new PrivateUserData(this, sysUserId, null);
			userList = new DEList<PrivateUserData>(this, "tw_users", "User list");
		} // proc InitUser

		private void BeginReadConfigurationUser(IDEConfigLoading config)
		{
		} // proc BeginReadConfigurationUser

		private void BeginEndConfigurationUser(IDEConfigLoading config)
		{
			// read the user data
			RegisterInitializationTask(11000, "Register users", () => RefreshUserDataAsync());
		} // proc BeginEndConfigurationUser

		private async Task RefreshUserDataAsync()
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

			using (var ctx = await CreateSystemContextAsync())
			{
				var userData = ctx.GetService<IPpsPrivateDataContext>();
				var users = await userData?.CreateSelectorAsync("dbo.serverLogins", throwException: false);
				if (users != null)
				{
					// fetch user list
					foreach (var u in users)
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
									var user = new PrivateUserData(this, userId, PrivateUserData.CreateUserIdentity(u));
									if (UpdateUserData(user, u))
										userList.Add(user);
								}
							}
							else
								Log.Warn("User ignored (id={userId}).");
						}
					} // foreach
				} // users != null

				await ctx.RollbackAsync();
			}
		} // proc RefreshUserData

		private void DoneUser()
		{
		} // proc DoneUser

		#endregion

		/// <summary>Create user scope for the system user</summary>
		/// <returns></returns>
		[LuaMember("ImpersonateSystem")]
		public IDECommonScope ImpersonateSystemContext()
		{
			// build system context
			var context = CreateSystemContextAsync().AwaitTask();
			context.RegisterDispose(context.Use());
			return context;
		} // func ImpersonateSystemContext
		
		/// <summary>Create a scope for the system user.</summary>
		/// <returns></returns>
		public async Task<IDECommonScope> CreateSystemContextAsync()
		{
			var context = new DECommonScope(this, true);
			await context.AuthentificateUserAsync(systemUser.User);
			return context;
		} // proc CreateSystemContextAsync

		/// <summary>Time in ms after a good connection is recreated in the pool.</summary>
		public int ConnectionLease => 1 * 3600 * 1000; // 1h
	} // class PpsApplication
}
