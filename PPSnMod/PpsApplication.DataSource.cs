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
using System.Reflection;
using System.Threading.Tasks;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Data;
using TecWare.DE.Server;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;
using TecWare.PPSn.Server.Data;

namespace TecWare.PPSn.Server
{
	#region -- class PpsSysConnectionHandle -------------------------------------------

	internal sealed class PpsSysConnectionHandle : IPpsConnectionHandle
	{
		/// <summary></summary>
		public event EventHandler Disposed;

		private readonly PpsSysDataSource dataSource;
		private readonly IDEAuthentificatedUser authentificatedUser;
		private bool isDisposed = false;

		internal PpsSysConnectionHandle(PpsSysDataSource dataSource, IDEAuthentificatedUser authentificatedUser)
		{
			this.dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
			this.authentificatedUser = authentificatedUser ?? throw new ArgumentNullException(nameof(authentificatedUser));
		} // ctor

		public void Dispose()
		{
			if (isDisposed)
				throw new ObjectDisposedException(nameof(PpsSysConnectionHandle));

			isDisposed = true;
			Disposed?.Invoke(this, EventArgs.Empty);
		} // proc Dispose

		public Task<bool> EnsureConnectionAsync(IDEAuthentificatedUser authentificatedUser, bool throwException = true)
			=> Task.FromResult(true);

		public PpsDataSource DataSource => dataSource;
		public IDEUser User => authentificatedUser.Info;
		public bool IsConnected => !isDisposed;
	} // class PpsSysConnectionHandle

	#endregion

	#region -- class PpsSysDataSource -------------------------------------------------

	/// <summary>System data source, to get access to services.</summary>
	public sealed class PpsSysDataSource : PpsDataSource
	{
		#region -- class PpsSysMethodSelectorToken ------------------------------------

		private sealed class PpsSysMethodSelectorToken : IPpsSelectorToken
		{
			private readonly PpsSysDataSource dataSource;
			private readonly string name;
			private readonly string path;
			private readonly MethodInfo method;

			private IPpsColumnDescription[] columnDescriptions;

			public PpsSysMethodSelectorToken(PpsSysDataSource dataSource, string name, string path, MethodInfo method)
			{
				this.dataSource = dataSource;
				this.name = name;
				this.path = path;
				this.method = method;
			} // ctor

			public void InitializeColumns()
			{
				var selector = CreateSelector(dataSource.systemConnection, null, false); // call head
				if (selector is IDataColumns columns)
				{
					columnDescriptions = new IPpsColumnDescription[columns.Columns.Count];
					for (var i = 0; i < columnDescriptions.Length; i++)
						columnDescriptions[i] = selector.GetFieldDescription(columns.Columns[i].Name);
				}
				else
					columnDescriptions = null;
			} // proc InitializeColumnsAsync

			public PpsDataSelector CreateSelector(IPpsConnectionHandle connection, string alias, bool throwException = true)
			{
				PpsDataSelector ret = null;
				if (String.IsNullOrEmpty(path))
					ret = InvokeCreateSelector((PpsSysConnectionHandle)connection, dataSource.application, throwException);
				else
				{
					if (!dataSource.application.FirstChildren<DEConfigItem>(
						c => String.Compare(c.Name, path, StringComparison.OrdinalIgnoreCase) == 0,
						c => ret = InvokeCreateSelector((PpsSysConnectionHandle)connection, c, throwException)
					))
					{
						if (throwException)
							throw new ArgumentOutOfRangeException($"Path '{path}' not found.");
						return null;
					}
				}
				return ret;
			} // func CreateSelector

			private PpsDataSelector InvokeCreateSelector(PpsSysConnectionHandle connection, DEConfigItem c, bool throwException)
			{
				try
				{
					var parameterInfo = method.GetParameters();
					var args = new object[parameterInfo.Length];

					for (var i = 0; i < parameterInfo.Length; i++)
					{
						var parameterType = parameterInfo[i].ParameterType;
						if (parameterType == typeof(PpsDataSource) || parameterType == typeof(PpsSysDataSource))
							args[i] = connection?.DataSource;
						else
							throw new ArgumentNullException(parameterInfo[i].Name, $"Could not assign type {parameterType.Name}.");
					}

					return (PpsDataSelector)method.Invoke(c, args);
				}
				catch (TargetInvocationException)
				{
					if (throwException)
						throw;
					return null;
				}
			} // proc InvokeCreateSelector

			public IPpsColumnDescription GetFieldDescription(string selectorColumn)
			{
				var description = (IPpsColumnDescription)dataSource.application.GetFieldDescription(name + "." + selectorColumn, false);
				if (description == null)
					description = Array.Find(columnDescriptions, c => String.Compare(c.Name, selectorColumn, StringComparison.OrdinalIgnoreCase) == 0);
				return description;
			} // func GetFieldDescription

			IReadOnlyList<IDataColumn> IDataColumns.Columns => columnDescriptions;

			public PpsDataSource DataSource => dataSource;
			public string Name => name;
		} // class PpsSysMethodSelectorToken

		#endregion

		private readonly PpsApplication application;
		private readonly PpsSysConnectionHandle systemConnection;

		/// <summary></summary>
		/// <param name="sp"></param>
		/// <param name="name"></param>
		public PpsSysDataSource(IServiceProvider sp, string name)
			: base(sp, name)
		{
			application = sp.GetService<PpsApplication>(true);
			systemConnection = (PpsSysConnectionHandle)application.GetOrCreatePooledConnection(this, application.GetSystemUser(), true);
		} // ctor

		/// <inherited/>
		public override IPpsConnectionHandle CreateConnection(IDEAuthentificatedUser authentificatedUser, bool throwException = true)
			=> new PpsSysConnectionHandle(this, authentificatedUser);

		/// <summary></summary>
		/// <param name="name"></param>
		/// <param name="sourceDescription"></param>
		/// <returns></returns>
		public override Task<IPpsSelectorToken> CreateSelectorTokenAsync(string name, XElement sourceDescription)
		{
			var viewType = sourceDescription.GetAttribute("type", "view");
			if (viewType != "view")
				throw new DEConfigurationException(sourceDescription, "Only 'view' is allowed on @type.");

			// [applicationNode]/function
			var viewDescription = sourceDescription.Value;

			var sep = viewDescription.LastIndexOf('/');
			var path = sep == -1 ? String.Empty : viewDescription.Substring(0, sep);
			var methodName = sep == -1 ? viewDescription : viewDescription.Substring(sep + 1);
			MethodInfo method = null;
			if (path.Length == 0)
				method = application.GetType().GetMethod(methodName);
			else
			{
				application.FirstChildren<DEConfigItem>(
					c => String.Compare(c.Name, path, StringComparison.OrdinalIgnoreCase) == 0,
					c =>
					{
						method = c.GetType().GetMethod(methodName);
					});
			}
			if (method == null)
				throw new FormatException($"Sys view format '{viewDescription}' not resolved.");

			var selector = new PpsSysMethodSelectorToken(this, name, path, method);
			return Task.Run<IPpsSelectorToken>(() => { selector.InitializeColumns(); return selector; });
		} // proc CreateSelectorTokenAsync

		/// <summary></summary>
		/// <param name="connection"></param>
		/// <returns></returns>
		public override PpsDataTransaction CreateTransaction(IPpsConnectionHandle connection)
			=> throw new NotSupportedException();

		/// <summary></summary>
		/// <param name="connection"></param>
		/// <param name="lastSyncronizationStamp"></param>
		/// <param name="leaveConnectionOpen"></param>
		/// <returns></returns>
		public override PpsDataSynchronization CreateSynchronizationSession(IPpsConnectionHandle connection, long lastSyncronizationStamp, bool leaveConnectionOpen = false)
			=> new PpsDataSynchronization(application, connection, leaveConnectionOpen);

		/// <summary>Get the system connection handle.</summary>
		public IPpsConnectionHandle SystemConnection => systemConnection;
		/// <summary></summary>
		public override string Type => "Sys";
	} // class PpsSysDataSource

	#endregion

	#region -- class PpsApplication ---------------------------------------------------

	public partial class PpsApplication
	{
		#region -- class PpsDatabaseLibrary ---------------------------------------------

		/// <summary>Library implementation for database access.</summary>
		public sealed class PpsDatabaseLibrary : LuaTable
		{
			private readonly PpsApplication application;
			private readonly Dictionary<string, object> memberCache = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

			internal PpsDatabaseLibrary(PpsApplication application)
			{
				this.application = application;
			} // ctor

			/// <summary>Clear dynamic member cache</summary>
			public void ClearCache()
			{
				lock (memberCache)
					memberCache.Clear();
			} // proc ClearCache

			#region -- EnsureConnectionAsync, GetDatabase -----------------------------

			private static bool IsNull(object value, bool throwException, string name)
			{
				if (value == null)
				{
					if (throwException)
						throw new ArgumentNullException(name);
					return true;
				}
				else
					return false;
			} // func IsNull

			private static bool TryGetCurrentUser(IDECommonScope scope, bool throwException, out IDEAuthentificatedUser authentificatedUser)
			{
				if (IsNull(scope, throwException, nameof(scope)))
				{
					authentificatedUser = null;
					return false;
				}

				authentificatedUser = throwException ? scope.DemandUser() : scope.TryDemandUser();
				return authentificatedUser != null;
			} // func TryGetCurrentUser

			private static bool TryGetCurrentUser(bool throwException, out IDEAuthentificatedUser authentificatedUser)
				=> TryGetCurrentUser(DEScope.GetScopeService<IDECommonScope>(throwException), throwException, out authentificatedUser);

			/// <summary>Ensure connection</summary>
			/// <param name="source"></param>
			/// <param name="throwException"></param>
			/// <returns></returns>
			public Task<IPpsConnectionHandle> EnsureConnectionAsync(PpsDataSource source, bool throwException)
			{
				// get datasource
				return TryGetCurrentUser(throwException, out var user)
					? application.EnsurePooledConnectionAsync(source, user, throwException)
					: Task.FromResult<IPpsConnectionHandle>(null);
			} // func EnsureConnectionAsync

			/// <summary>Access a database and connection this transaction with the scope-transaction.</summary>
			/// <param name="scope"></param>
			/// <param name="dataSource">Source of the database</param>
			/// <param name="throwException"></param>
			/// <returns></returns>
			public async Task<PpsDataTransaction> GetDatabaseAsync(IDECommonScope scope, PpsDataSource dataSource, bool throwException)
			{
				if (IsNull(scope, throwException, nameof(scope)))
					return null;

				// cached transaction
				if (scope.TryGetGlobal<PpsDataTransaction>(this, dataSource, out var trans))
					return trans;

				// get user
				if (!TryGetCurrentUser(scope, throwException, out var authentificatedUser))
					return null;

				// get database connection
				var connectionHandle = await application.EnsurePooledConnectionAsync(dataSource, authentificatedUser, throwException);
				if (connectionHandle == null)
					return null;

				// create and register transaction
				trans = dataSource.CreateTransaction(connectionHandle);

				scope.RegisterCommitAction(new Action(trans.Commit), true);
				scope.RegisterRollbackAction(new Action(trans.Rollback), true);
				scope.RegisterDispose(trans);

				scope.SetGlobal(this, dataSource, trans);

				return trans;
			} // func GetDatabaseAsync

			/// <summary>Access a database and connection this transaction with the scope-transaction.</summary>
			/// <param name="dataSource">Source of the database</param>
			/// <param name="throwException"></param>
			/// <returns></returns>
			public Task<PpsDataTransaction> GetDatabaseAsync(PpsDataSource dataSource, bool throwException = true)
			{
				var scope = DEScope.GetScopeService<IDECommonScope>(throwException);
				if (scope == null)
					return null;

				return GetDatabaseAsync(scope, dataSource, throwException);
			} // func GetDatabaseAsync

			/// <summary>Access a database and connection this transaction with the scope-transaction.</summary>
			/// <param name="name">Name of the database</param>
			/// <param name="throwException"></param>
			/// <returns></returns>
			public Task<PpsDataTransaction> GetDatabaseAsync(string name = null, bool throwException = true)
			{
				// find existing source
				var dataSource = name == null ? application.MainDataSource : application.GetDataSource(name, true);
				return GetDatabaseAsync(dataSource, throwException);
			} // func GetDatabaseAsync

			/// <summary>Get a active transaction of the data source.</summary>
			/// <param name="scope"></param>
			/// <param name="dataSource"></param>
			/// <returns><c>null</c>, if there is no active transaction.</returns>
			public PpsDataTransaction GetActiveTransaction(IDECommonScope scope, PpsDataSource dataSource)
				=> scope != null && scope.TryGetGlobal<PpsDataTransaction>(this, dataSource, out var trans) ? trans : null;

			/// <summary>Get a active transaction of the data source.</summary>
			/// <param name="dataSource"></param>
			/// <returns><c>null</c>, if there is no active transaction.</returns>
			public PpsDataTransaction GetActiveTransaction(PpsDataSource dataSource)
				=> GetActiveTransaction(DEScope.GetScopeService<IDECommonScope>(false), dataSource);

			/// <summary>Access a database and connection this transaction with the scope-transaction.</summary>
			/// <param name="name">Name of the database</param>
			/// <returns></returns>
			[LuaMember]
			public PpsDataTransaction GetDatabase(string name = null)
				=> GetDatabaseAsync(name).AwaitTask();

			#endregion

			#region -- CreateSelectorAsync --------------------------------------------

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

				private readonly PpsApplication application;
				private readonly IDEAuthentificatedUser authentificatedUser;
				private readonly bool throwException;

				private readonly Dictionary<PpsDataSource, IPpsConnectionHandle> openConnections = new Dictionary<PpsDataSource, IPpsConnectionHandle>();

				public PpsViewJoinExpression(PpsApplication application, IDEAuthentificatedUser authentificatedUser, string expression, bool throwException)
				{
					this.application = application ?? throw new ArgumentNullException(nameof(application));
					this.authentificatedUser = authentificatedUser ?? throw new ArgumentNullException(nameof(authentificatedUser));
					this.throwException = throwException;

					Parse(expression);
				} // ctor

				private PpsDataSelector CreateSelector(PpsViewDescription viewInfo, string alias)
				{
					// ensure the connection
					var dataSource = viewInfo.SelectorToken.DataSource;
					if (!openConnections.TryGetValue(dataSource, out var connectionHandle))
					{
						connectionHandle = application.EnsurePooledConnectionAsync(dataSource, authentificatedUser, throwException).Result;
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
					=> application.GetViewDefinition(tableName, throwException);

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

			/// <summary>Create a selector database views.</summary>
			/// <param name="scope"></param>
			/// <param name="selectorToken"></param>
			/// <param name="alias"></param>
			/// <param name="throwException"></param>
			/// <returns></returns>
			public async Task<PpsDataSelector> CreateSelectorAsync(IDECommonScope scope, IPpsSelectorToken selectorToken, string alias = null, bool throwException = true)
			{
				if (IsNull(scope, throwException, nameof(scope)))
					return null;

				// ensure user
				if (!TryGetCurrentUser(scope, throwException, out var authentificatedUser))
					return null;

				// ensure connection
				var connectionHandle = await application.EnsurePooledConnectionAsync(selectorToken.DataSource, authentificatedUser, throwException);
				if (connectionHandle == null)
					return null;

				return selectorToken.CreateSelector(connectionHandle, alias, throwException);
			} // func CreateSelectorAsync

			/// <summary>Create a selector database views.</summary>
			/// <param name="selectorToken"></param>
			/// <param name="alias"></param>
			/// <param name="throwException"></param>
			/// <returns></returns>
			public Task<PpsDataSelector> CreateSelectorAsync(IPpsSelectorToken selectorToken, string alias = null, bool throwException = true)
				=> CreateSelectorAsync(DEScope.GetScopeService<IDECommonScope>(throwException), selectorToken, alias, throwException);

			/// <summary>Create a selector database views.</summary>
			/// <param name="scope"></param>
			/// <param name="select"></param>
			/// <param name="columns"></param>
			/// <param name="filter"></param>
			/// <param name="order"></param>
			/// <param name="throwException"></param>
			/// <returns></returns>
			public async Task<PpsDataSelector> CreateSelectorAsync(IDECommonScope scope, string select, PpsDataColumnExpression[] columns = null, PpsDataFilterExpression filter = null, PpsDataOrderExpression[] order = null, bool throwException = true)
			{
				if (String.IsNullOrEmpty(select))
					throw new ArgumentNullException(nameof(select));
				if (IsNull(scope, throwException, nameof(scope)))
					return null;

				// ensure user
				if (!TryGetCurrentUser(scope, throwException, out var authentificatedUser))
					return null;

				// create selector
				var selectorInfo = new PpsViewJoinExpression(application, authentificatedUser, select, throwException);
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
			} // func CreateSelectorAsync

			/// <summary>Create a selector database views.</summary>
			/// <param name="select"></param>
			/// <param name="columns"></param>
			/// <param name="filter"></param>
			/// <param name="order"></param>
			/// <param name="throwException"></param>
			/// <returns></returns>
			public Task<PpsDataSelector> CreateSelectorAsync(string select, PpsDataColumnExpression[] columns = null, PpsDataFilterExpression filter = null, PpsDataOrderExpression[] order = null, bool throwException = true)
				=> CreateSelectorAsync(DEScope.GetScopeService<IDECommonScope>(throwException), select, columns, filter, order, throwException);

			/// <summary>Create a selector database views.</summary>
			/// <param name="select"></param>
			/// <param name="columns"></param>
			/// <param name="filter"></param>
			/// <param name="order"></param>
			/// <param name="scope"></param>
			/// <param name="formatProvider"></param>
			/// <param name="throwException"></param>
			/// <returns></returns>
			public Task<PpsDataSelector> CreateSelectorAsync(string select, string columns, string filter, string order, IDECommonScope scope = null, IFormatProvider formatProvider = null, bool throwException = true)
			{
				return CreateSelectorAsync(
					scope ?? DEScope.GetScopeService<IDECommonScope>(throwException),
					select,
					PpsDataColumnExpression.Parse(columns).ToArray(),
					PpsDataFilterExpression.Parse(filter, formatProvider: formatProvider),
					PpsDataOrderExpression.Parse(order).ToArray(),
					throwException
				);
			} // func CreateSelectorAsync

			/// <summary>Create a selector database views.</summary>
			/// <param name="table"></param>
			/// <param name="scope"></param>
			/// <param name="formatProvider"></param>
			/// <param name="throwException"></param>
			/// <returns></returns>
			public Task<PpsDataSelector> CreateSelectorAsync(LuaTable table, IDECommonScope scope = null, IFormatProvider formatProvider = null, bool throwException = true)
			{
				return CreateSelectorAsync(
					scope ?? DEScope.GetScopeService<IDECommonScope>(throwException),
					table.GetOptionalValue("select", table.GetOptionalValue("name", (string)null)),
					PpsDataColumnExpression.Parse(table.GetMemberValue("columns")).ToArray(),
					PpsDataFilterExpression.Parse(table.GetMemberValue("filter"), formatProvider: formatProvider),
					PpsDataOrderExpression.Parse(table.GetMemberValue("order")).ToArray()
				);
			} // func CreateSelectorAsync

			/// <summary>Create a selector database views.</summary>
			/// <param name="select"></param>
			/// <returns></returns>
			[LuaMember]
			public PpsDataSelector CreateSelector(string select)
				=> CreateSelectorAsync(select, (PpsDataColumnExpression[])null, null, null).AwaitTask();

			/// <summary>Create a selector database views.</summary>
			/// <param name="select"></param>
			/// <param name="columns"></param>
			/// <param name="filter"></param>
			/// <param name="order"></param>
			/// <returns></returns>
			[LuaMember]
			public PpsDataSelector CreateSelector(string select, string columns, string filter, string order)
				=> CreateSelectorAsync(select, columns, filter, order).AwaitTask();

			/// <summary>Create a selector database views.</summary>
			/// <param name="table"></param>
			/// <returns></returns>
			[LuaMember]
			public PpsDataSelector CreateSelector(LuaTable table)
				=> CreateSelectorAsync(table).AwaitTask();

			#endregion

			private object GetMemberCacheItem(string memberName)
			{
				lock (memberCache)
				{
					if (memberCache.TryGetValue(memberName, out var cacheItem))
						return cacheItem;
					else
					{
						var dataSource = application.GetDataSource(memberName, false);
						if (dataSource != null)
						{
							memberCache[memberName] = dataSource;
							return dataSource;
						}

						var viewInfo = application.GetViewDefinition(memberName);
						if (viewInfo != null)
						{
							memberCache[memberName] = viewInfo;
							return viewInfo;
						}

						memberCache[memberName] = null;
						return null;
					}
				}
			} // func GetMemberCacheItem

			/// <summary>Add virtual keys</summary>
			/// <param name="key"></param>
			/// <returns></returns>
			protected override object OnIndex(object key)
			{
				if (key is string memberName)
				{
					switch (GetMemberCacheItem(memberName))
					{
						case PpsDataSource dataSource:
							return GetDatabaseAsync(dataSource).AwaitTask();
						case PpsViewDescription view:
							return CreateSelectorAsync(view.SelectorToken).AwaitTask();
						default:
							return base.OnIndex(key);
					}
				}
				else
					return base.OnIndex(key);
			} // func OnIndex

			/// <summary>Get a global transaction to the main database.</summary>
			[LuaMember]
			public PpsDataTransaction Main => GetDatabase();
		} // class PpsDatabaseLibrary

		#endregion

		private readonly PpsDatabaseLibrary databaseLibrary;

		/// <summary>Find a data source by name.</summary>
		/// <param name="name"></param>
		/// <param name="throwException"></param>
		/// <returns></returns>
		[LuaMember]
		public PpsDataSource GetDataSource(string name, bool throwException = true)
		{
			if (name == null)
				throw new ArgumentNullException(nameof(name));

			using (EnterReadLock())
				return (PpsDataSource)UnsafeChildren.FirstOrDefault(c => c is PpsDataSource && String.Compare(c.Name, name, StringComparison.OrdinalIgnoreCase) == 0)
					?? (throwException ? throw new ArgumentOutOfRangeException(nameof(name), name, $"Data source is not defined ('{name}').") : (PpsDataSource)null);
		} // func GetDataSource

		/// <summary>Library for access the object store.</summary>
		[LuaMember("Db")]
		public PpsDatabaseLibrary Database => databaseLibrary;
	} // class PpsApplication

	#endregion
}
