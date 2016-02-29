using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Server;
using TecWare.DE.Stuff;
using TecWare.DES.Data;
using TecWare.PPSn.Server.Data;

namespace TecWare.PPSn.Server.Sql
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PpsSqlExDataSource : PpsSqlDataSource
	{
		#region -- class SqlConnectionHandle ----------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private class SqlConnectionHandle : IPpsConnectionHandle
		{
			public event EventHandler Disposed;

			private readonly PpsSqlExDataSource dataSource;
			private readonly SqlConnectionStringBuilder connectionString;
			private readonly IPpsPrivateDataContext identity;
			private readonly SqlConnection connection; // sql connection for read data

			public SqlConnectionHandle(PpsSqlExDataSource dataSource, SqlConnectionStringBuilder connectionString, IPpsPrivateDataContext identity)
			{
				this.dataSource = dataSource;
				this.connectionString = connectionString;
				this.identity = identity;
				this.connection = new SqlConnection();
			} // ctor

			public void Dispose()
			{
				Disposed?.Invoke(this, EventArgs.Empty);
			}

			private static bool Connect(SqlConnectionStringBuilder connectionString, SqlConnection connection, IPpsPrivateDataContext identity, bool throwException)
			{
				// create the connection
				try
				{
					if (identity.SystemIdentity == null) // use network credentials
					{
						connectionString.IntegratedSecurity = false;
						connection.ConnectionString = connectionString.ToString();

						var altLogin = identity.AlternativeCredential;
						if (altLogin == null)
							throw new ArgumentException("User has no sql-login data.");

						connection.Credential = new SqlCredential(altLogin.UserName, altLogin.SecurePassword);
						connection.Open();
					}
					else
					{
						connectionString.IntegratedSecurity = true;
						connection.ConnectionString = connectionString.ToString();

						using (identity.SystemIdentity.Impersonate())
							connection.Open();
					}
					return true;
				}
				catch (Exception)
				{
					if (throwException)
						throw;
					return false;
				}
			} // func Connect

			public SqlConnection ForkConnection()
			{
				// create a new connection
				var con = new SqlConnection();
				var conStr = new SqlConnectionStringBuilder(connectionString.ToString());
				conStr.ApplicationName = "User_Trans";
				conStr.Pooling = true;

				// ensure connection
				Connect(conStr, con, identity, true);

				return con;
			} // func ForkConnection

			public bool EnsureConnection(bool throwException)
			{
				if (IsConnected)
					return true;

				return Connect(connectionString, connection, identity, throwException);
			} // func EnsureConnection

			public PpsDataSource DataSource => dataSource;
			public SqlConnection Connection => connection;

			public bool IsConnected => IsConnectionOpen(connection);
		} // class SqlConnectionHandle

		#endregion

		#region -- class SqlDataRow -------------------------------------------------------

		private sealed class SqlDataRow : DynamicDataRow
		{
			private readonly SqlDataReader r;
			private readonly Lazy<Type[]> columnTypes;
			private readonly Lazy<string[]> columnNames;

			public SqlDataRow(SqlDataReader r)
			{
				this.r = r;

				this.columnTypes = new Lazy<Type[]>(
					() =>
					{
						var t = new Type[r.FieldCount];
						for (var i = 0; i < r.FieldCount; i++)
							t[i] = r.GetFieldType(i);
						return t;
					});

				this.columnNames = new Lazy<string[]>(
					() =>
					{
						var t = new string[r.FieldCount];
						for (var i = 0; i < r.FieldCount; i++)
							t[i] = r.GetName(i);
						return t;
					});

			} // ctor

			public override object this[int index]
			{
				get
				{
					var v = r.GetValue(index);
					if (v == DBNull.Value)
						v = null;
					return v;
				}
			} // func this

			public override int ColumnCount => r.FieldCount;

			public override string[] ColumnNames => columnNames.Value;
			public override Type[] ColumnTypes => columnTypes.Value;
		} // class SqlDataRow

		#endregion

		#region -- class SqlDataSelector --------------------------------------------------

		private sealed class SqlDataSelector : PpsDataSelector
		{
			//private readonly List<string> columns = new List<string>();
			private readonly SqlConnectionHandle connection;

			private readonly string viewName;
			private readonly string selectList;
			private readonly string whereCondition;
			private readonly string orderBy;

			public SqlDataSelector(SqlConnectionHandle connection, string viewName, string selectList, string whereCondition, string orderBy)
				: base(connection.DataSource)
			{
				this.connection = connection;
				this.viewName = viewName;
				this.selectList = selectList;
				this.whereCondition = whereCondition;
				this.orderBy = orderBy;
			} // ctor

			public SqlDataSelector SqlSelect(string addSelectList)
			{
				if (String.IsNullOrEmpty(addSelectList))
					return this;

				var newSelectList = String.IsNullOrEmpty(selectList) ? addSelectList : selectList + ", " + addSelectList;
				return new SqlDataSelector(connection, viewName, newSelectList, whereCondition, orderBy);
			} // func SqlSelect

			public SqlDataSelector SqlWhere(string addWhereCondition)
			{
				if (String.IsNullOrEmpty(addWhereCondition))
					return this;

				var newWhereCondition = String.IsNullOrEmpty(selectList) ? addWhereCondition : "(" + whereCondition + ") and (" + addWhereCondition + ")";
				return new SqlDataSelector(connection, viewName, selectList, newWhereCondition, orderBy);
			} // func SqlWhere

			public SqlDataSelector SqlOrderBy(string addOrderBy)
			{
				if (String.IsNullOrEmpty(addOrderBy))
					return this;

				var newOrderBy = String.IsNullOrEmpty(addOrderBy) ? addOrderBy : orderBy + ", " + addOrderBy;
				return new SqlDataSelector(connection, viewName, selectList, whereCondition, newOrderBy);
			} // func SqlOrderBy

			public override IEnumerator<IDataRow> GetEnumerator(int start, int count, IPropertyReadOnlyDictionary selector)
			{
				using (var cmd = new SqlCommand())
				{
					cmd.Connection = connection.Connection;
					cmd.CommandType = CommandType.Text;

					var sb = new StringBuilder("select ");

					// build the select
					if (String.IsNullOrEmpty(selectList))
						sb.Append("* ");
					else
						sb.Append(selectList).Append(' ');

					// add the view
					sb.Append("from ").Append(viewName).Append(" ");

					// add the where
					if(!String.IsNullOrEmpty(whereCondition))
						sb.Append("where ").Append(whereCondition).Append(' ');

					// add the orderBy
					if (!String.IsNullOrEmpty(orderBy))
					{
						sb.Append("order by ")
							.Append(orderBy);

						// build the range, without order fetch is not possible
						if (count >= 0 && start < 0)
							start = 0;
						if (start >= 0)
						{
							sb.Append("offset ").Append(start).Append(" rows ");
							if (count >= 0)
								sb.Append("fetch next ").Append(count).Append(" rows only ");
						}
					}

					cmd.CommandText = sb.ToString();
					
					using (var r = cmd.ExecuteReader(CommandBehavior.SingleResult))
					{
						var c = new SqlDataRow(r);
						while (r.Read())
							yield return c;
					}
				}
			} // func GetEnumerator
		} // class SqlDataSelector

		#endregion

		#region -- class SqlCodeBasedDataSelectorToken ------------------------------------

		private class SqlCodeBasedDataSelector : IPpsSelectorToken
		{
			//private string sourceCode;
			public PpsDataSource DataSource
			{
				get
				{
					throw new NotImplementedException();
				}
			}

			public PpsDataSelector CreateSelector(IPpsConnectionHandle connection, bool throwException = true)
			{
				throw new NotImplementedException();
			}
		} // class SqlCodeBasedDataSelector

		#endregion

		#region -- class SqlResultInfo ----------------------------------------------------

		private sealed class SqlResultInfo : List<Func<SqlDataReader, IEnumerable<IDataRow>>>
		{
		} // class SqlResultInfo

		#endregion

		#region -- class SqlDataTransaction -----------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class SqlDataTransaction : PpsDataTransaction
		{
			private readonly SqlConnection connection;
			private readonly SqlTransaction transaction;

			#region -- Ctor/Dtor ------------------------------------------------------------

			public SqlDataTransaction(PpsSqlDataSource dataSource, SqlConnection connection)
				: base(dataSource)
			{
				this.connection = connection;

				// create the sql transaction
				this.transaction = connection.BeginTransaction(IsolationLevel.ReadUncommitted);
			} // ctor

			protected override void Dispose(bool disposing)
			{
				base.Dispose(disposing); // commit/rollback

				if (disposing)
				{
					transaction.Dispose();
					connection.Dispose();
				}
			} // proc Dispose

			public override void Commit()
			{
				transaction.Commit();
				base.Commit();
			} // proc Commit

			public override void Rollback()
			{
				try
				{
					transaction.Rollback();
				}
				finally
				{
					base.Rollback();
				}
			} // proc Rollback

			#endregion

			private SqlCommand PrepareCommand(LuaTable table, SqlResultInfo resultInfo)
			{
				var cmd = new SqlCommand();
				try
				{
					cmd.Connection = connection;
					cmd.Transaction = transaction;

					string name;
					if ((name = (string)table["execute"]) != null)
						BindSqlExecute(table, cmd, name, resultInfo);
					else if ((name = (string)table["insert"]) != null)
						BindSqlInsert(table, cmd, (string)table["insert"], resultInfo);
					else if ((name = (string)table["delete"]) != null)
						throw new NotImplementedException();
					else if ((name = (string)table["merge"]) != null)
						throw new NotImplementedException();
					else // name = "sql"
						throw new NotImplementedException();
					
					return cmd;
				}
				catch
				{
					cmd.Dispose();
					throw;
				}
			} // func PrepareCommand

			private static void BindSqlExecute(LuaTable table, SqlCommand cmd, string name, SqlResultInfo resultInfo)
			{
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = name;

				// build argument list
				SqlCommandBuilder.DeriveParameters(cmd);

				// map arguments
				foreach (SqlParameter p in cmd.Parameters)
				{
					var parameterName = p.ParameterName;
					if (parameterName.StartsWith("@"))
						parameterName = parameterName.Substring(1);

					var value = table[parameterName];
					if (value == null)
						p.Value = DBNull.Value;
					else
						p.Value = value;
				}
			} // proc BindSqlExecute

			private void BindSqlInsert(LuaTable table, SqlCommand cmd, string name, SqlResultInfo resultInfo)
			{
				var commandText = new StringBuilder();
				var variableList = new StringBuilder();
				cmd.CommandType = CommandType.Text;

				var tableInfo = ((PpsSqlExDataSource)DataSource).ResolveTableByName(name);

				commandText.Append("insert into ")
					.Append(tableInfo.FullName);
				
				var values = table[1] as LuaTable;
				if (values == null)
					throw new ArgumentNullException("values missing");

				commandText.Append(" (");
				var first = true;
				foreach (var kv in values.Members)
				{
					if (first)
						first = false;
					else {
						commandText.Append(", ");
						variableList.Append(", ");
					}

					commandText.Append("[").Append(kv.Key).Append("]");

					var parameterName = "@" + kv.Key;
					var column = ((PpsSqlExDataSource)DataSource).ResolveColumnByName(kv.Key);
					cmd.Parameters.Add(column?.CreateSqlParameter(parameterName, kv.Value ?? DBNull.Value) ?? new SqlParameter(parameterName, kv.Value != null ? Procs.ChangeType(kv.Value, column.FieldType) : DBNull.Value));
					variableList.Append(parameterName);
				}
				commandText.Append(") ");

				// generate output clause
				var primaryKeyName = tableInfo.PrimaryKey.Name;
				commandText.Append("output inserted.").Append(primaryKeyName);
				resultInfo.Add(r =>
				{
					var index = 1;
					while (r.Read())
					{
						//table[index, primaryKeyName] = r.GetValue(0); gibt StackOverflowException?
						dynamic t = table[index];
						t[primaryKeyName] = r.GetValue(0);
						index++;
					}
					return null; // do not emit this result
				});
				commandText.Append(" values (");
				commandText.Append(variableList);
				commandText.Append(")");

				cmd.CommandText = commandText.ToString();
			} // proc BindSqlInsert

			public override void ExecuteNoneResult(LuaTable parameter)
			{
				foreach (var rs in ExecuteMultipleResult(parameter)) { }
			} // proc ExecuteNoneResult

			public override IEnumerable<IDataRow> ExecuteSingleResult(LuaTable parameter)
			{
				var first = true;
				foreach (var rs in ExecuteMultipleResult(parameter))
				{
					if (first)
					{
						first = false;
						return rs;
					}
				}
				throw new ArgumentException("No resultset."); // todo:
			} // func ExecuteSingleResult

			public override IEnumerable<IEnumerable<IDataRow>> ExecuteMultipleResult(LuaTable parameter)
			{
				var resultInfo = new SqlResultInfo();

				using (var cmd = PrepareCommand(parameter, resultInfo))
				{
					if (resultInfo.Count == 0)
						cmd.ExecuteNonQuery();
					else
					{
						using (SqlDataReader r = cmd.ExecuteReader(resultInfo.Count == 1 ? CommandBehavior.SingleResult : CommandBehavior.Default))
						{
							foreach (var c in resultInfo)
							{
								if (r.FieldCount < 0)
									throw new ArgumentException("Missmatch, result vs. result info."); // todo: besser meldung

								// enumerate result
								if (c != null)
								{
									var rs = c(r);
									if (rs != null)
										yield return rs;
								}

								// fetch next result
								r.NextResult();
							}
						}
					}

					// update parameter
					foreach (SqlParameter p in cmd.Parameters)
					{
						if ((p.Direction & ParameterDirection.Output) != 0)
							parameter[p.ParameterName.Substring(1)] = p.Value;
					}
				}  // using cmd
			} // ExecuteMultipleResult
		} // class SqlDataTransaction

		#endregion

		#region -- class SqlColumnInfo ----------------------------------------------------

		private sealed class SqlTableInfo
		{
			private readonly int objectId;
			private readonly string schema;
			private readonly string name;
			private readonly int primaryColumnId;
			private readonly Lazy<SqlColumnInfo> primaryColumn;

			public SqlTableInfo(Func<int, int, SqlColumnInfo> resolveColumn, SqlDataReader r)
			{
				this.objectId = r.GetInt32(0);
				this.schema = r.GetString(1);
				this.name = r.GetString(2);
				this.primaryColumnId = r.GetInt32(3);

				this.primaryColumn = new Lazy<SqlColumnInfo>(() => resolveColumn(objectId, primaryColumnId));
			} // ctor

			public int TableId => objectId;
			public string Schema => schema;
			public string Name => name;
			public string FullName => schema + "." + name;
			public SqlColumnInfo PrimaryKey => primaryColumn.Value;
		} // class SqlTableInfo

		#endregion

		#region -- class SqlColumnInfo ----------------------------------------------------

		private sealed class SqlColumnInfo
		{
			private readonly SqlTableInfo table;
			private readonly int columnId;
			private readonly string name;
			private readonly Type fieldType;
			private readonly SqlDbType sqlType;
			private readonly int maxLength;
			private readonly byte precision;
			private readonly byte scale;
			private readonly bool isNull;
			private readonly bool isIdentity;

			public SqlColumnInfo(Func<int, SqlTableInfo> resolveObjectId, SqlDataReader r)
			{
				this.table = resolveObjectId(r.GetInt32(0));
				this.columnId = r.GetInt32(1);
				this.name = r.GetString(2);
				this.fieldType = GetFieldType(r.GetByte(3));
				this.sqlType = GetSqlType(r.GetByte(3));
				this.maxLength = r.GetInt16(4);
				this.precision = r.GetByte(5);
				this.scale = r.GetByte(6);
				this.isNull = r.GetBoolean(7);
				this.isIdentity = r.GetBoolean(8);
			} // ctor

			public SqlParameter CreateSqlParameter(string parameterName, object value)
				=> new SqlParameter(parameterName, sqlType, maxLength, ParameterDirection.Input, isNull, precision, scale, name, DataRowVersion.Current, value);

			private static Type GetFieldType(byte systemTypeId)
			{
				switch (systemTypeId)
				{
					case 36: // uniqueidentifier  
						return typeof(Guid);

					case 40: // date
					case 41: // time
					case 42: // datetime2
					case 58: // smalldatetime
					case 61: // datetime
					case 189: // timestamp
						return typeof(DateTime);
					case 43: // datetimeoffset  
						return typeof(DateTimeOffset);

					case 48: // tinyint
						return typeof(byte);
					case 52: // smallint
						return typeof(short);
					case 56: // int
						return typeof(int);
					case 127: // bigint
						return typeof(long);
					case 59: // real
						return typeof(double);
					case 62: // float
						return typeof(float);
					case 98: // sql_variant
						return typeof(object);
					case 104: // bit
						return typeof(bool);

					case 60: // money
					case 106: // decimal
					case 108: // numeric
					case 122: // smallmoney
						return typeof(decimal);

					case 34: // image
					case 165: // varbinary
					case 173: // binary
						return typeof(byte[]);

					case 35: // text
					case 99: // ntext
					case 167: // varchar
					case 175: // char
					case 231: // nvarchar
					case 239: // nchar
						return typeof(string);

					case 241: // xml
						return typeof(XDocument);

					default:
						throw new IndexOutOfRangeException($"Unexpected sql server system type: {systemTypeId}");
				}
			} // func GetFieldType

			private static SqlDbType GetSqlType(byte systemTypeId)
			{
				switch (systemTypeId)
				{
					case 36: // uniqueidentifier  
						return SqlDbType.UniqueIdentifier;

					case 40: // date
						return SqlDbType.Date;
					case 41: // time
						return SqlDbType.Time;
					case 42: // datetime2
						return SqlDbType.DateTime2;
					case 58: // smalldatetime
						return SqlDbType.SmallDateTime;
					case 61: // datetime
						return SqlDbType.DateTime;
					case 189: // timestamp
						return SqlDbType.Timestamp;
					case 43: // datetimeoffset  
						return SqlDbType.DateTimeOffset;

					case 48: // tinyint
						return SqlDbType.TinyInt;
					case 52: // smallint
						return SqlDbType.SmallInt;
					case 56: // int
						return SqlDbType.Int;
					case 127: // bigint
						return SqlDbType.BigInt;
					case 59: // real
						return SqlDbType.Real;
					case 62: // float
						return SqlDbType.Float;
					case 98: // sql_variant
						return SqlDbType.Variant;
					case 104: // bit
						return SqlDbType.Bit;

					case 60: // money
						return SqlDbType.Money;
					case 106: // decimal
					case 108: // numeric
						return SqlDbType.Decimal;
					case 122: // smallmoney
						return SqlDbType.SmallMoney;

					case 34: // image
						return SqlDbType.Image;
					case 165: // varbinary
						return SqlDbType.VarBinary;
					case 173: // binary
						return SqlDbType.Binary;

					case 35: // text
						return SqlDbType.Text;
					case 99: // ntext
						return SqlDbType.NText;
					case 167: // varchar
						return SqlDbType.VarChar;
					case 175: // char
						return SqlDbType.Char;
					case 231: // nvarchar
						return SqlDbType.NVarChar;
					case 239: // nchar
						return SqlDbType.NChar;

					case 241: // xml
						return SqlDbType.Xml;

					default:
						throw new IndexOutOfRangeException($"Unexpected sql server system type: {systemTypeId}");
				}
			} // func GetSqlType

			public SqlTableInfo Table => table;
			public int ColumnId => columnId;
			public string Name => name;
			public Type FieldType => fieldType;
			public bool IsNull => isNull;
			public bool IsIdentity => isIdentity;
		} // class SqlColumnInfo

		#endregion

		private readonly SqlConnection masterConnection;
		private string lastReadedConnectionString = String.Empty;
		private DEThread databaseMainThread = null;

		private readonly List<SqlConnectionHandle> currentConnections = new List<SqlConnectionHandle>();

		private readonly ManualResetEventSlim schemInfoInitialized = new ManualResetEventSlim(false);
		private readonly Dictionary<string, SqlTableInfo> tableStore = new Dictionary<string, SqlTableInfo>(StringComparer.OrdinalIgnoreCase);
		private readonly Dictionary<string, SqlColumnInfo> columnStore = new Dictionary<string, SqlColumnInfo>(StringComparer.OrdinalIgnoreCase);

		#region -- Ctor/Dtor/Config -------------------------------------------------------

		public PpsSqlExDataSource(IServiceProvider sp, string name)
			: base(sp, name)
		{
			masterConnection = new SqlConnection();
		} // ctor

		protected override void Dispose(bool disposing)
		{
			try
			{
				if (disposing)
				{
					schemInfoInitialized.Dispose();

					// finish the connection
					Procs.FreeAndNil(ref databaseMainThread);
					masterConnection.Dispose();
				}
			}
			finally
			{
				base.Dispose(disposing);
			}
		} // proc Dispose

		protected override void OnBeginReadConfiguration(IDEConfigLoading config)
		{
			base.OnBeginReadConfiguration(config);

			// read the connection string
			var connectionString = config.ConfigNew.Element(PpsStuff.xnPps + "connectionString")?.Value;
			if (String.IsNullOrEmpty(connectionString))
				throw new DEConfigurationException(config.ConfigNew, "<connectionString> is empty.");

			if (lastReadedConnectionString != connectionString)
			{
				Procs.FreeAndNil(ref databaseMainThread);

				config.Tags.SetProperty("LastConStr", connectionString);
				config.Tags.SetProperty("ConStr", CreateConnectionStringBuilder(connectionString, "DES_Master"));
			}
		} // proc OnBeginReadConfiguration

		protected override void OnEndReadConfiguration(IDEConfigLoading config)
		{
			base.OnEndReadConfiguration(config);

			// update connection string
			var connectionStringBuilder = (SqlConnectionStringBuilder)config.Tags.GetProperty("ConStr", (object)null);

			if (connectionStringBuilder != null)
			{
				lock (masterConnection)
				{
					// close the current connection
					masterConnection.Close();

					// use integrated security by default
					connectionStringBuilder.IntegratedSecurity = true;

					// set the new connection
					masterConnection.ConnectionString = connectionStringBuilder.ToString();
					lastReadedConnectionString = config.Tags.GetProperty("LastConStr", String.Empty);

					// start background thread
					databaseMainThread = new DEThread(this, "Database", ExecuteDatabase);
				}
			}
		} // proc OnEndReadConfiguration

		#endregion

		#region -- Initialize Schema ------------------------------------------------------

		private void InitializeSchema()
		{
			lock (schemInfoInitialized)
			{
				using (SqlCommand cmd = masterConnection.CreateCommand())
				{
					cmd.CommandType = CommandType.Text;
					cmd.CommandText = String.Join(Environment.NewLine,
						// user tables
						"select u.object_id, s.name, u.name, ic.column_id",
							"from sys.objects u",
								"inner join sys.schemas s on (u.schema_id = s.schema_id)",
								"inner join sys.indexes pk on (u.object_id = pk.object_id and pk.is_primary_key = 1)",
								"inner join sys.index_columns ic on(pk.object_id = ic.object_id and pk.index_id = ic.index_id)",
							"where u.type = 'U';",
							// user columns
							"select c.object_id, c.column_id, c.name, c.system_type_id, c.max_length, c.precision, c.scale, c.is_nullable, c.is_identity",
								"from sys.columns c",
									"inner join sys.objects t on (c.object_id = t.object_id)",
							"where t.type = 'U' and c.is_computed = 0"
						);

					using (SqlDataReader r = cmd.ExecuteReader(CommandBehavior.Default))
					{
						while (r.Read())
						{
							try
							{
								var t = new SqlTableInfo(ResolveColumnById, r);
								tableStore.Add(t.FullName, t);
							}
							catch (Exception e)
							{
								Log.Except($"Table initialization failed: {r.GetValue(1)}.{r.GetValue(2)}", e);
							}
						}

						if (!r.NextResult())
							throw new InvalidOperationException();

						while (r.Read())
						{
							try
							{
								var c = new SqlColumnInfo(ResolveTableById, r);
								columnStore.Add(c.Name, c);
							}
							catch (Exception e)
							{
								Log.Except($"Column initialization failed: {r.GetValue(2)}", e);
							}
						}
					}
				}

				// done
				schemInfoInitialized.Set();
			}
		} // proc InitializeSchema

		private SqlTableInfo ResolveTableById(int tableId)
		{
			foreach (var t in tableStore.Values)
				if (t.TableId == tableId)
					return t;
			return null;
		} // func ResolveTableById

		private SqlColumnInfo ResolveColumnById(int tableId, int columnId)
		{
			foreach (var c in columnStore.Values)
				if (c.Table.TableId == tableId && c.ColumnId == columnId)
					return c;
			return null;
		} // func ResolveColumnById

		private SqlColumnInfo ResolveColumnByName(string key)
		{
			// todo: throwException, full name vs. name only
			SqlColumnInfo column;
			if (columnStore.TryGetValue(key, out column))
				return column;
			throw new ArgumentException($"Column '{key}' not found.", "key");
		} // func ResolveColumnByName

		private SqlTableInfo ResolveTableByName(string name)
		{
			// todo: throwException, with or without schema
			return tableStore[name];
		} // func ResolveTableByName

		#endregion

		#region -- Execute Database -------------------------------------------------------

		private void ExecuteDatabase()
		{
			var executeStartTick = Environment.TickCount;

			try
			{
				lock (masterConnection)
				{
					try
					{
						// reset connection
						if (masterConnection.State == System.Data.ConnectionState.Broken)
						{
							Log.Warn("Reset connection.");
							masterConnection.Close();
						}

						// open connection
						if (masterConnection.State == System.Data.ConnectionState.Closed)
						{
							Log.Info("Open database connection.");
							masterConnection.Open();
						}

						// execute background task
						if (masterConnection.State == ConnectionState.Open)
						{
							if (!schemInfoInitialized.IsSet)
								InitializeSchema();

						}
					}
					catch (Exception e)
					{
						Log.Except(e); // todo: detect disconnect
					}
				}
			}
			finally
			{
				// Mindestens eine Sekunde
				databaseMainThread.WaitFinish(Math.Max(1000 - Math.Abs(Environment.TickCount - executeStartTick), 0));
			}
		} // proc ExecuteDatabase

		#endregion

		public IPpsSelectorToken RegisterAndUpdateViewCode(string name, string sqlCode)
		{
			return null;
		} // proc RegisterAndUpdateViewCode

		public IPpsSelectorToken RegisterAndUpdateViewFile(string name, string sqlFile)
		{
			return null;
		} // proc RegisterAndUpdateViewFile

		private SqlConnectionHandle GetSqlConnection(IPpsConnectionHandle connection, bool throwException)
		{
			return (SqlConnectionHandle)connection;
		} // func GetSqlConnection

		/// <summary>Creates a user specific connection.</summary>
		/// <param name="privateUserData"></param>
		/// <returns></returns>
		public override IPpsConnectionHandle CreateConnection(IPpsPrivateDataContext privateUserData, bool throwException = true)
		{
			lock (currentConnections)
			{
				var c = new SqlConnectionHandle(this, CreateConnectionStringBuilder(lastReadedConnectionString, "User"), privateUserData);
				currentConnections.Add(c);
				return c;
			}
		} // func IPpsConnectionHandle

		public override PpsDataSelector CreateSelector(IPpsConnectionHandle connection, string name, bool throwException = true)
			=> new SqlDataSelector(GetSqlConnection(connection, throwException), name, null, null, null);

		public override PpsDataTransaction CreateTransaction(IPpsConnectionHandle connection)
		{
			var c = GetSqlConnection(connection, true);
			return new SqlDataTransaction(this, c.ForkConnection());
		} // func CreateTransaction

		public bool IsConnected
		{
			get
			{
				lock (masterConnection)
					return IsConnectionOpen(masterConnection);
			}
		} // prop IsConnected

		// -- Static --------------------------------------------------------------

		private static SqlConnectionStringBuilder CreateConnectionStringBuilder(string connectionString, string applicationName)
		{
			var connectionStringBuilder = new SqlConnectionStringBuilder(connectionString);

			// remove password, and connection information
			connectionStringBuilder.Password = String.Empty;
			connectionStringBuilder.UserID = String.Empty;
			connectionStringBuilder.IntegratedSecurity = false;

			connectionStringBuilder.ApplicationName = applicationName; // add a name
			connectionStringBuilder.MultipleActiveResultSets = true; // activate MARS

			return connectionStringBuilder;
		} // func CreateConnectionStringBuilder

		private static bool IsConnectionOpen(SqlConnection connection)
			=> connection.State != System.Data.ConnectionState.Closed;
	} // PpsSqlExDataSource
}
