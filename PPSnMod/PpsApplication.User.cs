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
using ICSharpCode.SharpZipLib.Zip;
using Neo.IronLua;
using System;
using System.Collections.Generic;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Xml.Linq;
using TecWare.DE.Data;
using TecWare.DE.Server;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;
using TecWare.PPSn.Server.Data;

namespace TecWare.PPSn.Server
{
	public partial class PpsApplication
	{
		/// <summary>Extension for the login command</summary>
		public const string ExtendLoginMethods = "ExtendLogin";

		/// <summary>User context optional wellknown property: Contact id.</summary>
		public const string UserContextKtKtId = "KtKtId";
		/// <summary>User context optional wellknown property: Personal id.</summary>
		public const string UserContextPersId = "PersId";
		/// <summary>User context optional wellknown property: Full name of the contact or user.</summary>
		public const string UserContextFullName = "FullName";
		/// <summary>User context optional wellknown property: Initals of the contact or user.</summary>
		public const string UserContextInitials = "Initials";
		/// <summary>User context optional wellknown property: User symbol</summary>
		public const string UserContextIdenticon = "Identicon";

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
					if (connection != null)
					{
						if (connection.IsConnected)
							connection.Dispose();
						connection = null;
					}
				} // proc Clear

				public bool IsAlive
					=> connection != null && connection.IsConnected && unchecked(Environment.TickCount - createdAt) < connection.DataSource.Application.ConnectionLease;
				
				public IPpsConnectionHandle Handle => connection;

				public int CreatedAt => createdAt;
				public string DataSourceName => dataSourceName;
			} // class PooledConnection

			#endregion

			#region -- class LuaConfigTable -------------------------------------------

			private sealed class LuaConfigTable : LuaTable
			{
				private readonly PrivateUserData user;

				public LuaConfigTable(PrivateUserData user)
				{
					this.user = user ?? throw new ArgumentNullException(nameof(user));
				} // ctor
				
				protected override object OnIndex(object key)
				{
					if (key is string k)
					{
						var idx = Array.FindIndex(WellKnownUserOptionKeys, c => String.Compare(c, k, StringComparison.OrdinalIgnoreCase) == 0);
						if (idx >= 0)
						{
							switch (idx)
							{
								case 0:
									return user.userId;
								case 1:
									return user.userIdentity.Name;
								default:
									return user.wellKnownUserOptionValues[idx - 2];
							}
						}
					}
					return base.OnIndex(key);
				} // func OnIndex
			} // class LuaConfigTable

			#endregion

			private readonly PpsApplication application;

			private readonly long userId;           // unique id of the user
			private readonly LoggerProxy log;       // current log interface for the user
			private int currentVersion = -1;        // version of the user data
			private string[] securityTokens = null; // access rights

			private readonly LuaTable userConfig;
			private readonly object[] wellKnownUserOptionValues = new object[WellKnownUserOptionKeys.Length - 2];

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
				userConfig = new LuaConfigTable(this);

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
							var msg = String.Format("Remove pooled connection: {0} after {1:N0}", cur.DataSourceName, unchecked(Environment.TickCount - cur.CreatedAt));
							try
							{
								cur.Clear();
								pooledConnections.RemoveAt(i);

								if (application.IsDebug)
									Log.Info(msg);
							}
							catch (Exception e)
							{
								Log.Except(msg, e);
							}
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

						if (application.IsDebug)
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
				
				return Array.BinarySearch(securityTokens, securityToken.ToLower()) >= 0;
			} // func DemandToken

			#endregion

			#region -- UpdateData -----------------------------------------------------

			private static void UpdateMemberValues(LuaTable targetTable, LuaTable fromTable)
			{
				foreach (var kv in fromTable.Members)
				{
					if (kv.Value is LuaTable fromChildTable)
					{
						var targetValue = targetTable.GetMemberValue(kv.Key, rawGet: true);
						if (!(targetValue is LuaTable targetChildTable))
						{
							targetChildTable = new LuaTable();
							targetTable[kv.Key] = targetChildTable;
						}
						UpdateMemberValues(targetChildTable, fromChildTable);
					}
					else
						targetTable.SetMemberValue(kv.Key, kv.Value == DBNull.Value ? null : kv.Value, rawSet: true);
				}
			} // proc UpdateMemberValues

			internal void UpdateData(IDataRow r, bool force)
			{
				// check if we need a reload
				var loginVersion = r.GetProperty("LoginVersion", 0);
				if (!force && loginVersion == currentVersion)
					return;

				// currently service is for local stuff
				localIdentity = application.systemUser.userIdentity;
				currentVersion = loginVersion;

				// update optional values
				wellKnownUserOptionValues[0] = r.GetProperty("Name", userIdentity.Name);
				for (var i = 3; i < WellKnownUserOptionKeys.Length; i++)
					wellKnownUserOptionValues[i - 2] = r.TryGetProperty(WellKnownUserOptionKeys[i], out var value) ? value : null;
				
				// update parameter-set from database, use only members
				UpdateUserConfigCore(FromLson(r.GetProperty("Cfg", "{}")));

				securityTokens = application.Server.BuildSecurityTokens(r.GetProperty("Security", String.Empty), SecurityUser);
			} // proc UpdateData

			public static PpsUserIdentity CreateUserIdentity(IDataRow r)
			{
				string GetString(string fieldName)
					=> r.GetProperty(fieldName, null) ?? throw new ArgumentNullException($"{fieldName} is null.");

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

			#region -- GetProperty, UpdateProperties ----------------------------------

			bool IPropertyReadOnlyDictionary.TryGetProperty(string name, out object value)
			{
				value = GetMemberValue(name);
				return value != null;
			} // func TryGetProperty

			private void UpdateUserConfigCore(LuaTable properties)
				=> UpdateMemberValues(userConfig, properties);

			public async Task UpdatePropertiesAsync(LuaTable propertiesToUpdate)
			{
				using (var sysContext = await application.CreateSystemContextAsync())
				using (var mainCon = application.MainDataSource.CreateConnection(sysContext.GetUser<IPpsPrivateDataContext>(), true))
				using(var trans = application.MainDataSource.CreateTransaction(mainCon))
				{
					var row = GetFirstRow(trans.CreateSelector("dbo.serverLogins")
						.ApplyFilter(PpsDataFilterExpression.Compare("Id", PpsDataFilterCompareOperator.Equal, userId))
						.ApplyColumns(new PpsDataColumnExpression("Cfg"))
					);

					// get fresh user config from database
					UpdateUserConfigCore(FromLson(row.GetProperty("Cfg", "{}")));
					// update configuration from argument
					UpdateUserConfigCore(propertiesToUpdate);

					// update database
					trans.ExecuteNoneResult(new LuaTable
					{
						["exec"] = "sys.UpdateUserCfg",
						[1] = new LuaTable { ["Id"] = userId, ["Cfg"] = userConfig }
					});

					// commit changes
					await sysContext.CommitAsync();
				}
			} // proc UpdatePropertiesAsync

			#endregion

			/// <summary>Access application.</summary>
			[LuaMember("App")]
			public PpsApplication Application => application;

			/// <summary>Database ID of the user.</summary>
			[LuaMember("UserId"), DEListTypeProperty("@id")]
			public long Id => userId;
			/// <summary>Name of the user</summary>
			[LuaMember("UserName"), DEListTypeProperty("@name")]
			public string Name => userIdentity.Name;

			/// <summary>Configuration of the user.</summary>
			[LuaMember("Config")]
			public LuaTable Config { get => userConfig; set { } }

			string IDEUser.DisplayName => userIdentity.Name;
			IIdentity IDEUser.Identity => userIdentity;

			/// <summary>Return all security tokens in a semicolon separeted list.</summary>
			[LuaMember, DEListTypeProperty("groups")]
			public string SecurityTokens => String.Join(";", securityTokens);

			string[] IDEUser.SecurityTokens => securityTokens;

			[LuaMember]
			public LoggerProxy Log => log;

			/// <summary>Return the user identity token.</summary>
			public PpsUserIdentity User => userIdentity;
			/// <summary>Return the user's local identity.</summary>
			public PpsUserIdentity LocalIdentity => localIdentity;

			public static readonly string[] WellKnownUserOptionKeys = new string[] {
				"userId",
				"displayName",
				UserContextFullName,
				UserContextKtKtId,
				UserContextPersId,
				UserContextInitials,
				UserContextIdenticon
			};
		} // class PrivateUserData

		#endregion

		#region -- class PrivateUserDataContext ---------------------------------------

		/// <summary>This class holds a active context for a user. It is possible to
		/// attach objects, that will be disposed on the end of the active process.</summary>
		private sealed class PrivateUserDataContext : IPpsPrivateDataContext, IDEAuthentificatedUser
		{
			private readonly PrivateUserData privateUser;
			private readonly IIdentity currentIdentity; // contains the plain identity token from the user
			private bool isDisposed = false;

			#region -- Ctor/Dtor ------------------------------------------------------

			public PrivateUserDataContext(PrivateUserData privateUser, IIdentity currentIdentity)
			{
				this.privateUser = privateUser ?? throw new ArgumentNullException(nameof(privateUser));
				this.currentIdentity = currentIdentity ?? throw new ArgumentNullException(nameof(currentIdentity));

				if (!currentIdentity.IsAuthenticated)
					throw new ArgumentException("Identity is not verified.", nameof(currentIdentity));

				if (this.currentIdentity is WindowsIdentity c) // create a copy for WindowsIdentity
					this.currentIdentity = c.Clone();
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

			#region -- Data Tasks -----------------------------------------------------

			#region -- class PpsViewJoinExpression ------------------------------------

			private sealed class PpsViewJoinExpression : PpsDataJoinExpression<PpsViewDescription>
			{
				#region -- class PpsViewJoinVisitor -----------------------------------

				private sealed class PpsViewJoinVisitor : PpsJoinVisitor<PpsDataSelector>
				{
					private readonly PpsViewJoinExpression owner;

					public PpsViewJoinVisitor(PpsViewJoinExpression owner)
					{
						this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
					} // ctor

					public override PpsDataSelector CreateJoinStatement(PpsDataSelector leftExpression, PpsDataJoinType type, PpsDataSelector rightExpression, PpsDataJoinStatement[] on) 
						=> leftExpression.ApplyJoin(rightExpression, type, on);

					public override PpsDataSelector CreateTableStatement(PpsViewDescription table, string alias)
						=> owner.CreateSelector(table, alias);
				} // class PpsViewJoinVisitor

				#endregion

				private readonly PrivateUserDataContext context;
				private readonly bool throwException;

				private readonly Dictionary<PpsDataSource, IPpsConnectionHandle> openConnections = new Dictionary<PpsDataSource, IPpsConnectionHandle>();

				public PpsViewJoinExpression(PrivateUserDataContext context, string expression, bool throwException)
				{
					this.context = context ?? throw new ArgumentNullException(nameof(context));
					this.throwException = throwException;

					Parse(expression);
				} // ctor

				private PpsDataSelector CreateSelector(PpsViewDescription viewInfo, string alias)
				{
					// ensure the connection
					var dataSource = viewInfo.SelectorToken.DataSource;
					if (!openConnections.TryGetValue(dataSource, out var connectionHandle))
					{
						connectionHandle = context.EnsureConnectionAsync(dataSource, throwException).Result;
						if (connectionHandle == null)
						{
							if (throwException)
								throw new ArgumentNullException("No connection handle returned.");
							else
								return null;
						}
						openConnections.Add(dataSource, connectionHandle);
					}

					// create the selector
					return viewInfo.SelectorToken.CreateSelector(connectionHandle, alias, throwException);
				} // func CreateSelector

				protected override PpsDataJoinStatement[] CreateOnStatement(PpsTableExpression left, PpsDataJoinType joinOp, PpsTableExpression right)
					=> left.Table.LookupJoin(right.Table.Name)?.Statement;

				protected override PpsViewDescription ResolveTable(string tableName)
					=> context.Application.GetViewDefinition(tableName, throwException);

				public string LookupFilter(string expr)
					=> null;

				public string LookupOrder(string expr)
					=> null;

				public Task<PpsDataSelector> CreateSelectorAsync()
				{
					if (!IsValid)
						throw new InvalidOperationException("Expression is not valid.");
					return Task.Run(() => new PpsViewJoinVisitor(this).Visit(this));
				} // func CreateSelectorAsync
			} // class PpsViewJoinExpression

			#endregion

			public async Task<IPpsConnectionHandle> EnsureConnectionAsync(PpsDataSource source, bool throwException)
			{
				var c = privateUser.GetOrCreatePooledConnection(source, this, throwException);
				if (c == null)
					c = source.CreateConnection(this, throwException);

				return c != null && await c.EnsureConnectionAsync(throwException) ? c : null;
			} // func EnsureConnection

			/// <summary>Create a selector from a view description.</summary>
			/// <param name="selectorToken"></param>
			/// <param name="alias"></param>
			/// <param name="throwException"></param>
			/// <returns></returns>
			public async Task<PpsDataSelector> CreateSelectorAsync(IPpsSelectorToken selectorToken, string alias = null, bool throwException = true)
			{
				// ensure the connection
				var connectionHandle = await EnsureConnectionAsync(selectorToken.DataSource, throwException);
				if (connectionHandle == null)
				{
					if (throwException)
						throw new ArgumentNullException("No connection handle returned.");
					else
						return null;
				}
				return selectorToken.CreateSelector(connectionHandle, alias, throwException);
			} // func CreateSelectorAsync

			/// <summary>Create a selector from a select information.</summary>
			/// <param name="select"></param>
			/// <param name="columns"></param>
			/// <param name="filter"></param>
			/// <param name="order"></param>
			/// <param name="throwException"></param>
			/// <returns></returns>
			public async Task<PpsDataSelector> CreateSelectorAsync(string select, PpsDataColumnExpression[] columns, PpsDataFilterExpression filter = null, PpsDataOrderExpression[] order = null, bool throwException = true)
			{
				if (String.IsNullOrEmpty(select))
					throw new ArgumentNullException(nameof(select));

				// create selector
				var selectorInfo = new PpsViewJoinExpression(this, select, throwException);
				if (!selectorInfo.IsValid && !throwException)
					return null;

				// create selector
				var selector = await selectorInfo.CreateSelectorAsync();
				if (selector == null)
					return null;

				// column restrictions
				if (!PpsDataColumnExpression.IsEmpty(columns))
					selector = selector.ApplyColumns(columns);

				// apply filter rules
				if (!PpsDataFilterExpression.IsEmpty(filter))
					selector = selector.ApplyFilter(filter, selectorInfo.LookupFilter);

				// apply order
				if (!PpsDataOrderExpression.IsEmpty(order))
					selector = selector.ApplyOrder(order, selectorInfo.LookupOrder);

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

			public bool TryDemandToken(string securityToken)
				=> privateUser.DemandToken(securityToken);

			public bool TryGetProperty(string name, out object value)
				=> TryGetTableProperty(privateUser, name, out value);

			Task IPpsPrivateDataContext.UpdatePropertiesAsync(LuaTable properties)
				=> privateUser.UpdatePropertiesAsync(properties);

			public void UpdateProperties(LuaTable properties)
				=> privateUser.UpdatePropertiesAsync(properties).AwaitTask();

			public object GetService(Type serviceType)
			{
				if (serviceType == typeof(IPpsPrivateDataContext))
					return this;
				else if (serviceType == typeof(IDEUser))
					return privateUser;
				else
					return null;
			} // func GetService

			public long UserId => privateUser.Id;
			public string UserName => privateUser.Name;

			/// <summary>Return user properties</summary>
			public LuaTable Properties => privateUser;
			/// <summary>Configuration of the current user.</summary>
			public LuaTable Config => privateUser.Config;

			public bool IsDisposed => isDisposed;

			IIdentity IPrincipal.Identity => currentIdentity;
			PpsUserIdentity IPpsPrivateDataContext.Identity => privateUser.User;
			IDEUser IDEAuthentificatedUser.Info => privateUser;

			/// <summary>Access the application object.</summary>
			public PpsApplication Application => privateUser.Application;
			/// <summary>Main data source for this user.</summary>
			public PpsDataSource MainDataSource => Application.MainDataSource;
		} // class PrivateUserDataContext

		#endregion
		
		private PrivateUserData systemUser;
		private DEList<PrivateUserData> userList;

		#region -- Init/Done ----------------------------------------------------------

		private void InitUser()
		{
			systemUser = new PrivateUserData(this, sysUserId, null);
			userList = new DEList<PrivateUserData>(this, "tw_users", "User list");

			PublishItem(new DEConfigItemPublicAction("refreshUsers") { DisplayName = "user-refresh" });
			PublishItem(userList);
		} // proc InitUser

		private void BeginReadConfigurationUser(IDEConfigLoading config)
		{
		} // proc BeginReadConfigurationUser

		private void BeginEndConfigurationUser(IDEConfigLoading config)
		{
			// read the user data
			RegisterInitializationTask(11000, "Register users", () => RefreshUserDataAsync(true));
		} // proc BeginEndConfigurationUser

		private async Task RefreshUserDataAsync(bool force)
		{
			bool UpdateUserData(PrivateUserData userData, IDataRow r)
			{
				try
				{
					userData.UpdateData(r, force);
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

				// dbo.serverLogins will be defined by the main database
				var users = await userData?.CreateSelectorAsync("dbo.serverLogins", throwException: false);
				if (users != null)
				{
					// fetch user list
					using (userList.EnterWriteLock())
					{
						foreach (var u in users)
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
									try
									{
										var user = new PrivateUserData(this, userId, PrivateUserData.CreateUserIdentity(u));
										if (UpdateUserData(user, u))
											userList.Add(user);
									}
									catch (SystemException e)
									{
										Log.Warn($"User (id={userId}): {e.Message}");
									}
								}
							}
							else
								Log.Warn($"User ignored (id={userId}).");

						} // foreach
					} // using
				} // users != null

				await ctx.RollbackAsync();
			}
		} // proc RefreshUserData

		private void DoneUser()
		{
		} // proc DoneUser

		/// <summary>Force refresh of all users</summary>
		[
		LuaMember,
		DEConfigHttpAction("refreshUsers", IsSafeCall = true, SecurityToken = "desSys")
		]
		public void RefreshUsers(bool force = true)
			=> Task.Run(new Action(RefreshUserDataAsync(force).Wait)).Wait();

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
			var context = new DECommonScope(this, true, null);
			await context.AuthentificateUserAsync(systemUser.User);
			return context;
		} // proc CreateSystemContextAsync

		/// <summary>Returns the login data for the given context.</summary>
		/// <param name="ctx"></param>
		/// <returns>The return is a pointer to user properties</returns>
		[LuaMember]
		public LuaTable GetLoginData(IPpsPrivateDataContext ctx = null)
		{
			if (ctx == null)
				ctx = DEScope.GetScopeService<IPpsPrivateDataContext>(true);

			// execute script based extensions
			var options = ((PrivateUserDataContext)ctx).Config;
			CallTableMethods(ExtendLoginMethods, new object[] { ctx, options });
			return options;
		} // func GetLoginData

		/// <summary>Time in ms after a good connection is recreated in the pool.</summary>
		public int ConnectionLease => 1 * 3600 * 1000; // 1h
	} // class PpsApplication
}
