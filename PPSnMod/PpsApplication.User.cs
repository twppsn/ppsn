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
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Server;
using TecWare.DE.Stuff;
using TecWare.DE.Data;
using TecWare.PPSn.Server.Data;
using TecWare.PPSn.Data;

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
			private readonly LoggerProxy log;

			private readonly long userId;							// unique id of the user
			private readonly PpsUserIdentity user;    // user name for the system
			private string fullName = null;						// full name of the user
			private string[] securityTokens = null;   // access rights

			private WindowsIdentity systemIdentity = null;
			private string altUserName = null;
			private SecureString password = null;
			//private SecureString pin = null;

			private int lastAccess;   // last dispose of the last active context

			private List<IPpsConnectionHandle> connections = new List<IPpsConnectionHandle>(); // current connections within the sources
			private List<WeakReference<PrivateUserDataContext>> currentContexts = new List<WeakReference<PrivateUserDataContext>>();

			#region -- Ctor/Dtor/Idle -------------------------------------------------------

			public PrivateUserData(PpsApplication application, long userId, PpsUserIdentity user, string connectionName)
			{
				if (user == null)
					throw new ArgumentNullException("user");

				this.application = application;
				this.connectionName = connectionName;
				this.connectionLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
				this.log = LoggerProxy.Create(application, user.Name);

				this.userId = userId;
				this.user = user;

				if (user == PpsUserIdentity.System) // mark system user
					userId = SysUserId;

				this.fullName = null;
				
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

				// dispose connections
				CloseConnections();

				// free the windows identity
				systemIdentity?.Dispose();
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
				lock(connections)
				{
					var idx = FindConnectionIndexBySource(source);
					return idx >= 0 ? connections[idx] : null;
				}
			} // func GetConnectionHandle

			public void UpdateConnectionHandle(PpsDataSource source, IPpsConnectionHandle handle)
			{
				lock(connections)
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
					handle.Disposed += handle_Disposed;
				}
			} // proc UpdateConnectionHandle

			private void handle_Disposed(object sender, EventArgs e)
			{
				lock(connections)
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

			#region -- TestIdentity ---------------------------------------------------------

			public unsafe bool TestIdentity(IIdentity testIdentity)
			{
				if (user == testIdentity)
					return true;

				if (testIdentity is HttpListenerBasicIdentity)
					return TestIdentity((HttpListenerBasicIdentity)testIdentity);
				else if (testIdentity is WindowsIdentity)
					return TestIdentity((WindowsIdentity)testIdentity);
				else
					return false;
			} // func TestIdentity

			//private static unsafe bool TestIdentity(NetworkCredential a, NetworkCredential b)
			//{
			//	if (String.Compare(a.UserName, b.UserName, true) != 0)
			//		return false;

			//	var p1 = Marshal.SecureStringToBSTR(a.SecurePassword);
			//	var p2 = Marshal.SecureStringToBSTR(b.SecurePassword);
			//	try
			//	{
			//		var c1 = (char*)p1.ToPointer();
			//		var c2 = (char*)p2.ToPointer();
			//		while (*c1 != 0 && *c2 != 0)
			//		{
			//			if (*c1 != *c2)
			//				return false;
			//			c1++;
			//			c2++;
			//		}
			//		return true;
			//	}
			//	finally
			//	{
			//		Marshal.ZeroFreeBSTR(p1);
			//		Marshal.ZeroFreeBSTR(p2);
			//	}
			//} // func TestIdentity

			private unsafe bool TestIdentity(HttpListenerBasicIdentity testIdentity)
			{
				if (String.Compare(Name, testIdentity.Name, StringComparison.OrdinalIgnoreCase) != 0)
					return false;

				var p1 = Marshal.SecureStringToBSTR(password);
				var p2 = testIdentity.Password;
				try
				{
					var c1 = (char*)p1.ToPointer();
					var i = 0;
					while (*c1 != 0 && i < p2.Length)
					{
						if (*c1 != p2[i])
							return false;
						c1++;
						i++;
					}
					return true;
				}
				finally
				{
					Marshal.ZeroFreeBSTR(p1);
				}
			} // func TestIdentiy

			private bool TestIdentity(WindowsIdentity testIdentity)
				=> systemIdentity != null && systemIdentity.User == testIdentity.User;

			#endregion

			#region -- AuthentUser, CreateContext -------------------------------------------

			private PrivateUserDataContext CreateContextIntern(IIdentity currentIdentity)
			{
				lock(currentContexts)
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
					PrivateUserDataContext t;
					if (!currentContexts[i].TryGetTarget(out t))
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
					
					var idx = currentContexts.FindIndex(c =>
						{
							PrivateUserDataContext t;
							return c.TryGetTarget(out t) && t == context;
						});

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
					var mainConnectionHandle = GetMainConnectionHandle();

					if (mainConnectionHandle != null) // main connection is still active
					{
						if (TestIdentity(identity)) // todo: es gibt mehr wie eine identität, Windows, DbUser, PIN-User, ... erzeuge den Context mit der aktuellen
							return CreateContextIntern(null);
						else
							return null;
					}
					else // no main connection create one
					{
						IPpsConnectionHandle newConnection = null;
						var context = CreateContextIntern(null);
						try
						{
							// update the system identity
							if (systemIdentity == null && identity is WindowsIdentity)
								systemIdentity = (WindowsIdentity)identity;

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
				if (String.IsNullOrEmpty(securityToken))
					return true;

				lock (connections)
					return Array.BinarySearch(securityTokens, securityToken.ToLower()) >= 0;
			} // func DemandToken

			#endregion

			#region -- UpdateData -----------------------------------------------------------

			public void UpdateData(IDataRow r)
			{
			} // proc UpdateData

			public void UpdateData(XElement x)
			{
				if (x == null)
				{
					systemIdentity = WindowsIdentity.GetCurrent();
					altUserName = null;
					password = null;
				}
				else
				{
					throw new NotImplementedException("todo");
				}
			} // proc UpdateData

			#endregion

			[LuaMember("App")]
			public PpsApplication Application => application;

			[LuaMember("UserId")]
			public long Id => userId;
			[LuaMember("UserName")]
			public string Name => user.Name;
			[LuaMember("FullName")]
			public string FullName { get { return fullName ?? Name; } set { fullName = value; } }

			/// <summary></summary>
			public PpsUserIdentity User => user;
			/// <summary></summary>
			public NetworkCredential AlternativeCredential { get { return password == null ? null : new NetworkCredential(altUserName ?? Name, password); } }
			/// <summary></summary>
			public WindowsIdentity SystemIdentity { get { return systemIdentity; } set { systemIdentity = value; } }

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
			private readonly IIdentity currentIdentity;

			#region -- Ctor/Dtor ------------------------------------------------------------

			public PrivateUserDataContext(PrivateUserData privateUser, IIdentity currentIdentity)
			{
				this.privateUser = privateUser;
				this.currentIdentity = currentIdentity;
			} // ctor

			public void Dispose()
			{
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
					selector = selector.ApplyFilter(PpsDataFilterExpression.Parse(filter, 0, tok => viewInfo.Filter.FirstOrDefault(c => String.Compare(c.Name, tok, StringComparison.OrdinalIgnoreCase) == 0)?.Parameter, null));

				// apply order
				if (!String.IsNullOrWhiteSpace(order))
					selector = selector.ApplyOrder(PpsDataOrderExpression.Parse(order, tok => viewInfo.Order.FirstOrDefault(c => String.Compare(c.Name, tok, StringComparison.OrdinalIgnoreCase) == 0)?.Parameter));

				return selector;
			} // func CreateSelector

			public PpsDataSelector CreateSelector(LuaTable table)
			{
				if (table == null)
					throw new ArgumentNullException("table");

				return CreateSelector(table.GetOptionalValue("name", (string)null), table.GetOptionalValue("filter", (string)null), table.GetOptionalValue("order", (string)null), table.GetOptionalValue("throwException", true));
			} // func CreateSelector

			public PpsDataTransaction CreateTransaction(PpsDataSource source = null, bool throwException = true)
			{
				source = source ?? MainDataSource;

				// ensure the connection
				var c = EnsureConnection(source, throwException);
				if (c == null)
					return null;

				return source.CreateTransaction(c);
			} // func CreateTransaction

			#endregion

			public object GetService(Type serviceType)
			{
				if (serviceType == typeof(IPpsPrivateDataContext))
					return this;
				else
					return null;
			} // func GetService

			public bool IsInRole(string role)
			{
				return true; // todo: folge identität
			}

			public string UserName => privateUser.Name;

			public IIdentity Identity => currentIdentity;

			public PpsUserIdentity User => privateUser.User;
			public NetworkCredential AlternativeCredential => privateUser.AlternativeCredential;
			public WindowsIdentity SystemIdentity => privateUser.SystemIdentity;

			public PpsApplication Application => privateUser.Application;
			public PpsDataSource MainDataSource => Application.MainDataSource;
		} // class PrivateUserDataContext

		#endregion

		private PrivateUserData systemUser;
		private DEList<PrivateUserData> userList;

		#region -- Init/Done --------------------------------------------------------------

		private void InitUser()
		{
			systemUser = new PrivateUserData(this, SysUserId, PpsUserIdentity.System, "System");
			userList = new DEList<PrivateUserData>(this, "tw_users", "User list");
		} // proc InitUser

		private void BeginReadConfigurationUser(IDEConfigLoading config)
		{
		} // proc BeginReadConfigurationUser

		private void BeginEndConfigurationUser(IDEConfigLoading config)
		{
			// read system identity
			systemUser.UpdateData(Config.Element(PpsStuff.PpsNamespace + "systemUser"));

			// read the user data
			RegisterInitializationTask(11000, "Register users", () => Task.Run(new Action(RefreshUserData)));
		} // proc BeginEndConfigurationUser

		private void RefreshUserData()
		{
			using (var ctx = CreateSysContext())
			{
				var users = ctx?.CreateSelector("ServerLogins", throwException: false);
				if (users != null)
				{
					// fetch user list
					foreach (IDataRow u in users)
					{
						lock (userList)
						{
							var persId = u.GetProperty("ID", 0L);
							if (persId > 0)
							{
								var idx = userList.FindIndex(c => c.Id == persId);
								if (idx >= 0)
									userList[idx].UpdateData(u);
								else
								{
									var user = new PrivateUserData(this, persId, new PpsUserIdentity((string)u["LOGIN"]), "User");
									userList.Add(user);
									user.UpdateData(u);
								}
							}
							//else
							// todo: log fail
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
		[LuaMember("CreateSysContext")]
		public IPpsPrivateDataContext CreateSysContext()
			=> (IPpsPrivateDataContext)systemUser.Authentificate(PpsUserIdentity.System);

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
