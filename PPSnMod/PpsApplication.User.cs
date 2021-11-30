﻿#region -- copyright --
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
using System.Net;
using System.Security.Principal;
using System.Threading.Tasks;
using Neo.IronLua;
using TecWare.DE.Networking;
using TecWare.DE.Server;
using TecWare.DE.Stuff;
using TecWare.PPSn.Server.Data;

namespace TecWare.PPSn.Server
{
	#region -- class PpsApplication ---------------------------------------------------

	public partial class PpsApplication
	{
		/// <summary>User context optional wellknown property: Full name of the contact or user.</summary>
		public const string UserContextFullName = "FullName";
		/// <summary>User context optional wellknown property: Initals of the contact or user.</summary>
		public const string UserContextInitials = "Initials";
		/// <summary>User context optional wellknown property: User symbol</summary>
		public const string UserContextIdenticon = "Identicon";
		/// <summary>User context optional wellknown property: DataSource name</summary>
		public const string UserContextDataSource = "DataSource";

		/// <summary>Extension for the login command</summary>
		public const string ExtendLoginMethods = "ExtendLogin";

		#region -- class UserConnectionPool -------------------------------------------

		private sealed class UserConnectionPool
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

			private readonly PpsApplication application;
			private readonly WeakReference<IDEUser> userToken;
			private readonly LoggerProxy log;
			private readonly List<PooledConnection> pooledConnections = new List<PooledConnection>();

			#region -- Ctor/Dtor ------------------------------------------------------

			public UserConnectionPool(PpsApplication application, IDEUser user)
			{
				this.application = application ?? throw new ArgumentNullException(nameof(application));
				userToken = new WeakReference<IDEUser>(user ?? throw new ArgumentNullException(nameof(user)));
				log = LoggerProxy.Create(application, user.Identity.Name);
			} // ctor

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
									log.Debug(msg);
							}
							catch (Exception e)
							{
								log.Except(msg, e);
							}
						}
					}
					return null;
				}
			} // func FindPooledConnection

			public IPpsConnectionHandle GetOrCreatePooledConnection(PpsDataSource dataSource, bool throwException)
			{
				if (!userToken.TryGetTarget(out var _))
				{
					if (throwException)
						throw new InvalidOperationException("User is not active anymore");
					return null;
				}
				
				lock (pooledConnections)
				{
					var pooled = FindPooledConnection(dataSource);
					if (pooled == null)
					{
						var handle = dataSource.CreateConnection(throwException);
						if (handle == null)
							return null;

						pooledConnections.Add(new PooledConnection(handle));

						if (application.IsDebug)
							log.Debug("New pooled connection: {0}", dataSource.Name);
						return handle;
					}
					else
					{
						//log.Info("Reuse pooled connection: {0}", dataSource.Name);
						return pooled.Handle;
					}
				}
			} // func GetOrCreatePooledConnection

			#endregion

			[DEListTypeProperty("@user")]
			public string UserId => User?.Identity.Name;
			[DEListTypeProperty("@type")]
			public string UserType => User?.GetType().Name;
			[DEListTypeProperty("@active")]
			public bool IsActive => userToken.TryGetTarget(out _);
			[DEListTypeProperty("@cons")]
			public int ConnectionCount
			{
				get
				{
					lock (pooledConnections)
						return pooledConnections.Count;
				}
			} // prop ConnectionCount

			public IDEUser User => userToken.TryGetTarget(out var user) ? user : null;
		} // class UserConnectionPool

		#endregion

		#region -- class SystemUser ---------------------------------------------------

		[DEUserProperty(UserContextDataSource, typeof(string), "source")]
		private sealed class SystemUser : IDEUser, IDEAuthentificatedUser
		{
			private readonly PpsApplication application;

			#region -- Ctor/Dtor ------------------------------------------------------

			public SystemUser(PpsApplication application)
			{
				this.application = application ?? throw new ArgumentNullException(nameof(application));

				application.Server.RegisterUser(this);
			} // ctor

			public bool Equals(IDEUser other)
				=> ReferenceEquals(other, this);

			#region -- IDEAuthentificatedUser - members -------------------------------

			bool IPrincipal.IsInRole(string role)
			{
				if (role == SecurityUser)
					return false; // no login allowed
				else
					return true;
			} // func IPrincipal.IsInRole

			bool IDEAuthentificatedUser.TryGetCredential(out UserCredential userCredential)
			{
				userCredential = null;
				return false;
			} // func IDEAuthentificatedUser.TryGetCredential

			bool IDEAuthentificatedUser.TryImpersonate(out WindowsImpersonationContext impersonationContext)
			{
				impersonationContext = null;
				return true;
			} // func TryImpersonate

			IDEUser IDEAuthentificatedUser.Info => this;
			bool IDEAuthentificatedUser.CanImpersonate => true;

			#endregion

			public Task<IDEAuthentificatedUser> AuthentificateAsync(IIdentity identity)
				=> Task.FromResult<IDEAuthentificatedUser>(PpsUserIdentity.System.Equals(identity) ? this : null);

			#endregion

			#region -- Properties -----------------------------------------------------

			public bool TryGetProperty(string name, out object value)
			{
				switch (name)
				{
					case UserContextDataSource:
						value = "sys";
						return true;
					default:
						value = null;
						return false;
				}
			} // func TryGetProperty

			public IEnumerator<PropertyValue> GetEnumerator()
			{
				yield return new PropertyValue(UserContextDataSource, "sys");
			} // func GetEnumerator

			IEnumerator IEnumerable.GetEnumerator()
				=> GetEnumerator();

			#endregion

			public IIdentity Identity => PpsUserIdentity.System;
			public string DisplayName => Environment.UserDomainName + "\\" + Environment.UserName;

			public IReadOnlyList<string> SecurityTokens { get; } = Array.Empty<string>();
		} // class SystemUser

		#endregion

		private readonly SystemUser systemUser;
		private readonly DEList<UserConnectionPool> userPool;

		/// <summary>Get a pooled connection for a user.</summary>
		/// <param name="dataSource"></param>
		/// <param name="user"></param>
		/// <param name="throwException"></param>
		/// <returns></returns>
		public IPpsConnectionHandle GetOrCreatePooledConnection(PpsDataSource dataSource, IDEUser user, bool throwException = true)
		{
			if (user == null)
			{
				if (throwException)
					throw new ArgumentNullException(nameof(user));
				else
					return null;
			}

			using (userPool.EnterWriteLock())
			{
				UserConnectionPool p = null;

				for (var i = userPool.Count - 1; i >= 0; i--)
				{
					var u = userPool[i].User;
					if (u == null)
						userPool.RemoveAt(i);
					else if (u.Equals(user))
						p = userPool[i];
				}

				if (p == null)
					userPool.Add(p = new UserConnectionPool(this, user));

				return p.GetOrCreatePooledConnection(dataSource, throwException);
			}
		} // func GetOrCreatePooledConnection

		/// <summary>Get a pooled connection for a user.</summary>
		/// <param name="dataSource"></param>
		/// <param name="authentificatedUser"></param>
		/// <param name="throwException"></param>
		/// <returns></returns>
		public async Task<IPpsConnectionHandle> EnsurePooledConnectionAsync(PpsDataSource dataSource, IDEAuthentificatedUser authentificatedUser, bool throwException = true)
		{
			var c = GetOrCreatePooledConnection(dataSource, authentificatedUser.Info, throwException);
			if (c == null)
				c = dataSource.CreateConnection(throwException);

			return c != null && await c.EnsureConnectionAsync(authentificatedUser, throwException) ? c : null;
		} // func EnsurePooledConnectionAsync

		/// <summary>Create user scope for the system user</summary>
		/// <returns></returns>
		[LuaMember("ImpersonateSystem")]
		public IDECommonScope ImpersonateSystemContext()
		{
			// build system context
			var context = CreateSystemContext();
			context.RegisterDispose(context.Use());
			return context;
		} // func ImpersonateSystemContext

		/// <summary></summary>
		/// <param name="userName"></param>
		/// <param name="password"></param>
		/// <returns></returns>
		[LuaMember("ImpersonateUser")]
		public IDECommonScope ImpersonateUserContext(string userName, string password)
		{
			var context = CreateUserContextAsync(userName, password).AwaitTask();
			context.RegisterDispose(context.Use());
			return context;
		} // func ImpersonateUser

		/// <summary>Get the authentificated system user.</summary>
		/// <returns></returns>
		public IDEAuthentificatedUser GetSystemUser()
			=> systemUser;

		/// <summary>Create a scope for the system user.</summary>
		/// <returns></returns>
		public IDECommonScope CreateSystemContext()
			=> new DECommonScope(this, GetSystemUser());

		/// <summary>Create a user context with username and password.</summary>
		/// <param name="userName"></param>
		/// <param name="password"></param>
		/// <returns></returns>
		public async Task<IDECommonScope> CreateUserContextAsync(string userName, string password)
		{
			var scope = new DECommonScope(this, true, null);
			try
			{
				await scope.AuthentificateUserAsync(new HttpListenerBasicIdentity(userName, password));
				return scope;
			}
			catch
			{
				scope.Dispose();
				throw;
			}
		} // func CreateUserContextAsync

		/// <summary>Returns the login data for the given context.</summary>
		/// <param name="userInfo"></param>
		/// <returns>The return is a pointer to user properties</returns>
		[LuaMember]
		public LuaTable GetLoginData(IDEUser userInfo = null)
		{
			if (userInfo == null)
				userInfo = DEScope.GetScopeService<IDECommonScope>(true).DemandUser().Info;

			// execute script based extensions
			var options = new LuaTable();
			foreach (var c in userInfo)
			{
				if (c.Value != null)
					options.SetMemberValue(c.Name, c.Value);
			}
			CallTableMethods(ExtendLoginMethods, new object[] { userInfo, options });
			return options;
		} // func GetLoginData

		/// <summary>Time in ms after a good connection is recreated in the pool.</summary>
		public int ConnectionLease => 1 * 3600 * 1000; // 1h
	} // class PpsApplication

	#endregion
}
