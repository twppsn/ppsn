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
using System.Data.SqlClient;
using System.IO;
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
using TecWare.DE.Data;
using TecWare.PPSn.Server.Data;
using TecWare.PPSn.Data;

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

						//using (identity.SystemIdentity.Impersonate()) todo: Funktioniert nur als ADMIN?
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

		#region -- PpsDataResultColumnDescription -----------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary>Simple column description implementation.</summary>
		private sealed class PpsDataResultColumnDescription : IPpsColumnDescription
		{
			private readonly DataRow row;

			private readonly string name;
			private readonly Type fieldType;

			public PpsDataResultColumnDescription(DataRow row, string name, Type fieldType)
			{
				this.row = row;

				this.name = name;
				this.fieldType = fieldType;
			} // ctor

			public string Name => name;
			public Type DataType => fieldType;
			public int MaxLength => GetDataRowValue(row, "ColumnSize", Int32.MaxValue);
			public bool IsIdentity => false;
		} // class PpsDataResultColumnDescription

		#endregion

		#region -- class SqlDataSelectorToken ---------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary>Representation of a data view for the system.</summary>
		private sealed class SqlDataSelectorToken : IPpsSelectorToken
		{
			private readonly PpsSqlExDataSource source;
			private readonly string name;

			private readonly string[] columnNames;
			private readonly IPpsColumnDescription[] columnDescriptions;

			private SqlDataSelectorToken(PpsSqlExDataSource source, string name, string[] columnNames, IPpsColumnDescription[] columnDescriptions)
			{
				this.source = source;
				this.name = name;
				this.columnNames = columnNames;
				this.columnDescriptions = columnDescriptions;
			} // ctor

			public PpsDataSelector CreateSelector(IPpsConnectionHandle connection, bool throwException = true)
				=> new SqlDataSelector((SqlConnectionHandle)connection, this, null, null, null);

			public IPpsColumnDescription GetFieldDescription(string selectorColumn)
			{
				var index = Array.IndexOf(columnNames, selectorColumn);
				return index == -1 ? null : columnDescriptions[index];
			} // func GetFieldDefinition

			public string Name => name;
			public PpsSqlExDataSource DataSource => source;

			PpsDataSource IPpsSelectorToken.DataSource => DataSource;

			public IEnumerable<IPpsColumnDescription> Columns => columnDescriptions;

			// -- Static  -----------------------------------------------------------

			private static void ExecuteForResultSet(SqlConnection connection, PpsSqlExDataSource source, string name, out string[] columnNames, out IPpsColumnDescription[] columnDescriptions)
			{
				// execute the view once to determine the resultset
				using (var cmd = connection.CreateCommand())
				{
					cmd.CommandType = CommandType.Text;
					cmd.CommandText = "select * from " + name;
					using (var r = cmd.ExecuteReader(CommandBehavior.SchemaOnly | CommandBehavior.KeyInfo))
					{
						columnNames = new string[r.FieldCount];
						columnDescriptions = new IPpsColumnDescription[r.FieldCount];

						var dt = r.GetSchemaTable();
						var i = 0;
						foreach (DataRow c in dt.Rows)
						{
							IPpsColumnDescription columnDescription;
							var nativeColumnName = r.GetName(i);

							// try to find the view base description
							columnDescription = source.application.GetFieldDescription(name + "." + nativeColumnName, false);

							// try to find the table based field name
							if (columnDescription == null)
							{
								var schemaName = GetDataRowValue<string>(c, "BaseSchemaName", null) ?? "dbo";
								var tableName = GetDataRowValue<string>(c, "BaseTableName", null);
								var columnName = GetDataRowValue<string>(c, "BaseColumnName", null);

								if (tableName != null && columnName != null)
								{
									var fieldName = schemaName + "." + tableName + "." + columnName;
									columnDescription = source.application.GetFieldDescription(fieldName, false);
								}
							}

							// create a generic description
							if (columnDescription == null)
								columnDescription = new PpsDataResultColumnDescription(c, nativeColumnName, r.GetFieldType(i));

							columnNames[i] = nativeColumnName;
							columnDescriptions[i++] = columnDescription;
						}
					}
				} // using cmd
			} // pro ExecuteForResultSet

			private static string CreateOrReplaceView(SqlConnection connection, string name, string selectStatement)
			{
				// execute the new view
				using (var cmd = connection.CreateCommand())
				{
					cmd.CommandType = CommandType.Text;

					// drop
					cmd.CommandText = $"IF object_id('{name}', 'V') IS NOT NULL DROP VIEW {name}";
					cmd.ExecuteNonQuery();

					// create
					cmd.CommandText = $"CREATE VIEW {name} AS {selectStatement}";
					cmd.ExecuteNonQuery();
				} // using cmd

				return name;
			} // proc CreateOrReplaceView

			private static IPpsSelectorToken CreateCore(PpsSqlExDataSource source, string name, Func<SqlConnection, string> viewName)
			{
				string[] columnNames;
				IPpsColumnDescription[] columnDescriptions;
				SqlConnection connection;

				using (source.UseMasterConnection(out connection))
					ExecuteForResultSet(connection, source, viewName(connection), out columnNames, out columnDescriptions);

				return new SqlDataSelectorToken(source, name, columnNames, columnDescriptions);
			} // func CreateCore

			public static IPpsSelectorToken CreateFromStatement(PpsSqlExDataSource source, string name, string selectStatement)
				=> CreateCore(source, name, (connection) => CreateOrReplaceView(connection, name, selectStatement));

			public static IPpsSelectorToken CreateFromFile(PpsSqlExDataSource source, string name, string fileName)
				=> CreateCore(source, name, (connection) => CreateOrReplaceView(connection, name, File.ReadAllText(fileName)));

			public static IPpsSelectorToken CreateFromResource(PpsSqlExDataSource source, string name, string resourceName)
				=> CreateCore(source, name, (connection) => CreateOrReplaceView(connection, name, source.GetResourceScript(resourceName)));

			public static IPpsSelectorToken CreateFromPredefinedView(PpsSqlExDataSource source, string name, string viewName = null)
				=> CreateCore(source, name, (connection) => viewName ?? name);

			public static IPpsSelectorToken CreateFromXml(PpsSqlExDataSource source, string name, XElement sourceDescription)
			{
				// file => init by file
				// select => inline sql select
				// view => name of existing view
				var sourceType = sourceDescription.GetAttribute("type", "file");
				if (sourceType == "select") // create view from sql
					return CreateFromStatement(source, name, sourceDescription.Value);
				else if (sourceType == "file")
					return CreateFromFile(source, name, ProcsDE.GetFileName(sourceDescription, sourceDescription.Value));
				else if (sourceType == "resource")
					return CreateFromResource(source, name, sourceDescription.Value);
				else if (sourceType == "view")
					return CreateFromPredefinedView(source, name, sourceDescription?.Value);
				else
					throw new ArgumentOutOfRangeException(); // todo:
			} // func CreateFromXml
		} // class SqlDataSelectorToken

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
			private readonly SqlConnectionHandle connection;

			private readonly SqlDataSelectorToken selectorToken;
			private readonly string selectList;
			private readonly string whereCondition;
			private readonly string orderBy;

			public SqlDataSelector(SqlConnectionHandle connection, SqlDataSelectorToken selectorToken, string selectList, string whereCondition, string orderBy)
				: base(connection.DataSource)
			{
				this.connection = connection;
				this.selectorToken = selectorToken;
				this.selectList = selectList;
				this.whereCondition = whereCondition;
				this.orderBy = orderBy;
			} // ctor

			private string AddSelectList(string addSelectList)
			{
				if (String.IsNullOrEmpty(addSelectList))
					return selectList;

				return String.IsNullOrEmpty(selectList) ? addSelectList : selectList + ", " + addSelectList;
			} // func AddSelectList

			public SqlDataSelector SqlSelect(string addSelectList)
			{
				if (String.IsNullOrEmpty(addSelectList))
					return this;

				string newSelectList = AddSelectList(addSelectList);
				return new SqlDataSelector(connection, selectorToken, newSelectList, whereCondition, orderBy);
			} // func SqlSelect

			private string AddWhereCondition(string addWhereCondition)
			{
				if (String.IsNullOrEmpty(addWhereCondition))
					return whereCondition;

				return String.IsNullOrEmpty(selectList) ? addWhereCondition : "(" + whereCondition + ") and (" + addWhereCondition + ")";
			} // func AddWhereCondition

			public SqlDataSelector SqlWhere(string addWhereCondition)
			{
				if (String.IsNullOrEmpty(addWhereCondition))
					return this;

				string newWhereCondition = AddWhereCondition(addWhereCondition);
				return new SqlDataSelector(connection, selectorToken, selectList, newWhereCondition, orderBy);
			} // func SqlWhere

			private string AddOrderBy(string addOrderBy)
			{
				if (String.IsNullOrEmpty(addOrderBy))
					return orderBy;

				return String.IsNullOrEmpty(addOrderBy) ? addOrderBy : orderBy + ", " + addOrderBy;
			} // func AddOrderBy

			public SqlDataSelector SqlOrderBy(string addOrderBy)
			{
				if (String.IsNullOrEmpty(addOrderBy))
					return this;

				string newOrderBy = AddOrderBy(addOrderBy);
				return new SqlDataSelector(connection, selectorToken, selectList, whereCondition, newOrderBy);
			} // func SqlOrderBy

			public override IEnumerator<IDataRow> GetEnumerator(int start, int count)
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
					sb.Append("from ").Append(selectorToken.Name).Append(" ");

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

			public override IPpsColumnDescription GetFieldDescription(string nativeColumnName)
				=> selectorToken.GetFieldDescription(nativeColumnName);
		} // class SqlDataSelector

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
					cmd.Transaction = table.GetOptionalValue("__notrans", false) ? null : transaction;

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

		#region -- interface ISqlColumnInfo -------------------------------------------------

		private interface ISqlColumnInfo : IPpsColumnDescription
		{
			SqlTableInfo Table { get; }
		} // interface ISqlColumnInfo

		#endregion

		#region -- class SqlTableInfo -----------------------------------------------------

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

		private sealed class SqlColumnInfo : ISqlColumnInfo
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
				this.name = table.Schema + "." + table.Name + "." + r.GetString(2);
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

			#region -- GetFieldType, GetSqlType -----------------------------------------------

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

			#endregion

			public SqlTableInfo Table => table;
			public int ColumnId => columnId;
			public string Name => name;
			public Type FieldType => fieldType;
			public bool IsNull => isNull;
			public bool IsIdentity => isIdentity;

			int IPpsColumnDescription.MaxLength => maxLength;
			Type IPpsColumnDescription.DataType => fieldType;
		} // class SqlColumnInfo

		#endregion

		#region -- SqlDataSetDefinition -----------------------------------------------------

		private sealed class SqlDataSetDefinition : PpsDataSetServerDefinition
		{
			#region -- class SqlFieldBinding---------------------------------------------------

			///////////////////////////////////////////////////////////////////////////////
			/// <summary></summary>
			private class SqlFieldBinding
			{
				private readonly PpsDataColumnDefinition field;
				private readonly ISqlColumnInfo fieldSource;
				
				public SqlFieldBinding(PpsDataColumnDefinition field, ISqlColumnInfo fieldSource)
				{
					this.field = field;
					this.fieldSource = fieldSource;
				} // ctor
			} // class SqlFieldBinding

			#endregion

			#region -- class SqlFieldBinding---------------------------------------------------

			///////////////////////////////////////////////////////////////////////////////
			/// <summary></summary>
			private class SqlTableBinding
			{
				private readonly SqlDataSetDefinition dataSetDefinition;
				private readonly PpsDataTableDefinition table;

				private readonly List<SqlTableInfo> tableBindings = new List<SqlTableInfo>();
				private readonly List<SqlFieldBinding> fieldBindings = new List<SqlFieldBinding>();

				public SqlTableBinding(SqlDataSetDefinition dataSetDefinition, PpsDataTableDefinition table)
				{
					this.table = table;

					// collect the columns to the table binding
					foreach (PpsDataColumnDefinitionServer c in table.Columns)
					{
						if (c.FieldDescription == null || c.FieldDescription.DataSource != dataSetDefinition) // emit null field
						{
							fieldBindings.Add(new SqlFieldBinding(c, null));
						}
						else // emit bind field
						{
							var columnInfo = c.FieldDescription?.NativeColumnDescription as ISqlColumnInfo;
							if (columnInfo == null)
								throw new ArgumentNullException(); // todo: dürfte nicht passieren

							fieldBindings.Add(new SqlFieldBinding(c, columnInfo));

							// bind table
							if (tableBindings.IndexOf(columnInfo.Table) == -1)
								tableBindings.Add(columnInfo.Table);
						}
					}

					// find relations between the tables
					if (tableBindings.Count > 1)
					{
						throw new NotImplementedException("todo: multi select");
					}
				} // ctor

				public void GenerateLoadCommand()
				{
				} // proc GenerateLoadCommand

			} // class SqlTableBinding

			#endregion

			private readonly PpsSqlExDataSource dataSource;
			private readonly List<SqlTableBinding> tableBindings = new List<SqlTableBinding>();
			
			public SqlDataSetDefinition(IServiceProvider sp, PpsSqlExDataSource dataSource, string name, XElement config)
				: base(sp, name, config)
			{
				this.dataSource = dataSource;
			} // ctor

			public override void EndInit()
			{
				base.EndInit(); // initialize columns

				// collect the schema of the document
				foreach (var table in TableDefinitions)
					tableBindings.Add(new SqlTableBinding(this, table)); // create table binding (it depends on a equal index)



			} // proc EndInit
		} // class SqlDataSetDefinition
		
		#endregion

		private readonly PpsApplication application;
		private readonly SqlConnection masterConnection;
		private string lastReadedConnectionString = String.Empty;
		private readonly ManualResetEventSlim schemInfoInitialized = new ManualResetEventSlim(false);
		private DEThread databaseMainThread = null;

		private readonly List<SqlConnectionHandle> currentConnections = new List<SqlConnectionHandle>();

		private readonly Dictionary<string, SqlTableInfo> tableStore = new Dictionary<string, SqlTableInfo>(StringComparer.OrdinalIgnoreCase);
		private readonly Dictionary<string, SqlColumnInfo> columnStore = new Dictionary<string, SqlColumnInfo>(StringComparer.OrdinalIgnoreCase);

		#region -- Ctor/Dtor/Config -------------------------------------------------------

		public PpsSqlExDataSource(IServiceProvider sp, string name)
			: base(sp, name)
		{
			// must be in a ppsn element
			application = sp.GetService<PpsApplication>(true);

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
			var connectionString = config.ConfigNew.Element(PpsStuff.PpsNamespace + "connectionString")?.Value;
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
					application.RegisterInitializationTask(10000, "Database", async () => await Task.Run(new Action(schemInfoInitialized.Wait))); // block all other tasks
					databaseMainThread = new DEThread(this, "Database", ExecuteDatabase);
				}
			}
		} // proc OnEndReadConfiguration

		#endregion

		private string GetResourceScript(string resourceName)
		{
			// todo: namespace beachten und assembly

			using (var src = typeof(PpsSqlExDataSource).Assembly.GetManifestResourceStream(typeof(PpsSqlExDataSource), "tsql." + resourceName))
			{
				if (src == null)
					throw new ArgumentException($"{resourceName} not found.");

				using (var sr = new StreamReader(src, Encoding.UTF8, true))
					return sr.ReadToEnd();
			}
		} // func GetResourceScript

		#region -- Initialize Schema ------------------------------------------------------

		private void InitializeSchema()
		{
			lock (schemInfoInitialized)
			{
				using (SqlCommand cmd = masterConnection.CreateCommand())
				{
					cmd.CommandType = CommandType.Text;
					cmd.CommandText = GetResourceScript("ConnectionInitScript.sql");

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

				// Register Server logins
				application.RegisterView(SqlDataSelectorToken.CreateFromResource(this, "ServerLogins", "ServerLogins.sql"));


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

		#region -- Master Connection Service ----------------------------------------------

		internal IDisposable UseMasterConnection(out SqlConnection connection)
		{
			connection = masterConnection;
			return null;
		} // func UseMasterConnection

		#endregion

		public override Task<IPpsSelectorToken> CreateSelectorToken(string name, XElement sourceDescription)
		{
			var selectorToken = Task.Run(new Func<IPpsSelectorToken>(() => SqlDataSelectorToken.CreateFromXml(this, name, sourceDescription)));
			return selectorToken;
		} // func CreateSelectorToken

		public override IPpsColumnDescription GetColumnDescription(string columnName)
		{
			SqlColumnInfo column;
			return columnStore.TryGetValue(columnName, out column) ? column : null;
		} // func GetColumnDescription

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

		#region -- DataTable - Helper -----------------------------------------------------

		private static T GetDataRowValue<T>(DataRow row, string columnName, T @default)
		{
			var r = row[columnName];
			if (r == DBNull.Value)
				return @default;

			try
			{
				return r.ChangeType<T>();
			}
			catch 
			{
				return @default;
			}
		} // func GetDataRowValue
		
		#endregion
	} // PpsSqlExDataSource
}
