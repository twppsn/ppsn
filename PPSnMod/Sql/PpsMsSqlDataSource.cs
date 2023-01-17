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
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Security;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.SqlServer.Server;
using Neo.IronLua;
using TecWare.DE.Data;
using TecWare.DE.Networking;
using TecWare.DE.Server;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;
using TecWare.PPSn.Server.Data;

namespace TecWare.PPSn.Server.Sql
{
	/// <summary>Access sql-server.</summary>
	public class PpsMsSqlDataSource : PpsSqlDataSource
	{
		private static readonly XName xnAccess = PpsStuff.PpsNamespace + "access";

		#region -- class SqlConnectionHandle ------------------------------------------

		private sealed class SqlConnectionHandle : PpsSqlConnectionHandle<SqlConnection, SqlConnectionStringBuilder>
		{
			public SqlConnectionHandle(PpsMsSqlDataSource dataSource)
				: base(dataSource)
			{
			} // ctor

			protected override SqlConnection CreateConnection()
				=> new SqlConnection();

			protected override SqlConnectionStringBuilder CreateConnectionStringBuilder(bool forWrite)
			{
				var r = base.CreateConnectionStringBuilder(forWrite);
				r.Pooling = forWrite;
				return r;
			} // func CreateConnectionStringBuilder

			private static async Task OpenConnectionIntegratedAsync(SqlConnection connection, SqlConnectionStringBuilder connectionString)
			{
				connectionString.IntegratedSecurity = true;
				connection.ConnectionString = connectionString.ToString();
				await connection.OpenAsync();
			} // proc OpenConnectionIntegratedAsync

			private static async Task OpenConnectionAsync(SqlConnection connection, SqlConnectionStringBuilder connectionString, SqlCredential credential)
			{
				connectionString.IntegratedSecurity = false;
				connection.ConnectionString = connectionString.ToString();
				connection.Credential = credential;
				await connection.OpenAsync();
			} // proc OpenConnectionAsync


			private static Task ConnectSystemUserAsync(PpsMsSqlDataSource dataSource, SqlConnection connection, SqlConnectionStringBuilder connectionString)
			{
				if (dataSource.sysUserName == null)
					return OpenConnectionIntegratedAsync(connection, connectionString);
				else
					return OpenConnectionAsync(connection, connectionString, new SqlCredential(dataSource.sysUserName, dataSource.sysPassword));
			} // func ConnectSystemUserAsync

			protected override async Task ConnectCoreAsync(SqlConnection connection, SqlConnectionStringBuilder connectionString, IDEAuthentificatedUser authentificatedUser)
			{
				var sqlDataSource = (PpsMsSqlDataSource)DataSource;
				if (ReferenceEquals(PpsUserIdentity.System, authentificatedUser.Identity) || PpsUserIdentity.System.Equals(authentificatedUser.Identity)) // system user is asking for a connection
				{
					await ConnectSystemUserAsync(sqlDataSource, connection, connectionString);
				}
				else // ask for other user information
				{
					switch (sqlDataSource.TryGetConnectionMode(authentificatedUser))
					{
						case UserCredential uc:
							await OpenConnectionAsync(connection, connectionString, new SqlCredential(uc.UserName, ReadOnlyPassword(uc.Password)));
							break;
						case SqlCredential cred:
							await OpenConnectionAsync(connection, connectionString, cred);
							break;
						case WindowsImpersonationContext ctx:
							using (ctx)
								await OpenConnectionIntegratedAsync(connection, connectionString);
							break;
						case WindowsIdentity identity:
							if (identity.User.Equals(WindowsIdentity.GetCurrent().User))
							{
								await ConnectSystemUserAsync(sqlDataSource, connection, connectionString);
								break;
							}
							else
							{
								using (identity.Impersonate())
									await OpenConnectionIntegratedAsync(connection, connectionString);
								break;
							}
						case bool system:
							if (system)
							{
								await ConnectSystemUserAsync(sqlDataSource, connection, connectionString);
								break;
							}
							else if (authentificatedUser.TryImpersonate(out var ctx)) // does only work in the admin context
							{
								using (ctx)
									await OpenConnectionIntegratedAsync(connection, connectionString);
								break;
							}
							else if (authentificatedUser.TryGetCredential(out var uc)) // use user credentials
							{
								await OpenConnectionAsync(connection, connectionString, new SqlCredential(uc.UserName, ReadOnlyPassword(uc.Password)));
								break;
							}
							else
								goto default;
						default:
							throw new ArgumentOutOfRangeException(nameof(authentificatedUser));
					}
				}
			} // func ConnectCoreAsync

			private static SecureString ReadOnlyPassword(SecureString password)
			{
				if (password.IsReadOnly())
					return password;
				else
				{
					var copy = password.Copy();
					copy.MakeReadOnly();
					return copy;
				}
			} // proc ReadOnlyPassword

			/// <summary>Is connection alive.</summary>
			public override bool IsConnected => IsConnectionOpen(Connection);
		} // class PpsMsSqlConnectionHandle

		#endregion

		#region -- class SqlConnectionLogger ------------------------------------------

		private sealed class SqlConnectionLogger : IDisposable
		{
			private readonly SqlConnection connection;
			private readonly LogMessageScopeProxy log;

			public SqlConnectionLogger(SqlConnection connection, LogMessageScopeProxy log)
			{
				this.connection = connection ?? throw new ArgumentNullException(nameof(connection));
				this.log = log ?? throw new ArgumentNullException(nameof(log));

				connection.InfoMessage += Connection_InfoMessage;
			} // ctor

			public void Dispose()
			{
				connection.InfoMessage -= Connection_InfoMessage;
			} // proc Dispose

			private void Connection_InfoMessage(object sender, SqlInfoMessageEventArgs e)
			{
				log.WriteLine(e.Message);
			} // event Connection_InfoMessage
		} // class SqlConnectionLogger

		#endregion

		#region -- class PpsMsSqlDataTransaction --------------------------------------

		/// <summary></summary>
		protected class PpsMsSqlDataTransaction : PpsSqlDataTransaction<SqlConnection, SqlTransaction, SqlCommand>
		{
			#region -- Ctor/Dtor ------------------------------------------------------

			/// <summary></summary>
			/// <param name="dataSource"></param>
			/// <param name="connectionHandle"></param>
			public PpsMsSqlDataTransaction(PpsSqlDataSource dataSource, IPpsConnectionHandle connectionHandle)
				: base(dataSource, connectionHandle)
			{
			} // ctor

			#endregion

			#region -- Execute Results ------------------------------------------------

			#region -- PrepareColumnsForUpsert ----------------------------------------

			private (bool, IReadOnlyList<PpsSqlColumnInfo>) PrepareColumnsForUpsert(PpsSqlDataCommand cmd, LuaTable parameter, PpsSqlTableInfo tableInfo, LuaTable firstArgs)
			{
				var emitRowResults = false;
				var targetColumns = new List<PpsSqlColumnInfo>();
				var columnList = parameter.GetMemberValue("columnList");
				if (columnList is IDataColumns columnMap)
				{
					foreach (var c in columnMap.Columns)
					{
						if (c is IPpsColumnDescription t)
						{
							var dataColumn = (PpsDataColumnDefinition)t;
							var idx = dataColumn.Index;
							var nativeColumn = t.GetColumnDescription<PpsSqlColumnInfo>();
							if (nativeColumn != null && nativeColumn.Table == tableInfo)
							{
								targetColumns.Add(nativeColumn);
								cmd.AppendParameter(ParameterMapping.CreateRowIndex(idx, CreateParameter(cmd.Command, nativeColumn, c.Name), nativeColumn.DataType, DBNull.Value));
							}
						}
						else
						{
							var columnInfo = tableInfo.FindColumn(c.Name, true);
							targetColumns.Add(columnInfo);
							cmd.AppendParameter(ParameterMapping.CreateRowName(c.Name, CreateParameter(cmd.Command, columnInfo, c.Name), columnInfo.DataType, DBNull.Value));
						}
					}
					emitRowResults = true;
				}
				else if (columnList is LuaTable columnLua)
				{
					foreach (var k in columnLua.ArrayList)
					{
						if (k is string columnName)
						{
							var columnInfo = tableInfo.FindColumn(columnName, true);
							targetColumns.Add(columnInfo);
							cmd.AppendParameter(ParameterMapping.CreateTableName(columnName, CreateParameter(cmd.Command, columnInfo, columnName), columnInfo.DataType, DBNull.Value));
						}
					}
				}
				else if (firstArgs != null)
				{
					foreach (var m in firstArgs.Members)
					{
						var columnInfo = tableInfo.FindColumn(m.Key, true);
						targetColumns.Add(columnInfo);
						cmd.AppendParameter(ParameterMapping.CreateTableName(m.Key, CreateParameter(cmd.Command, columnInfo, m.Key), columnInfo.DataType, DBNull.Value));
					}
				}
				else
				{
					foreach (var t in tableInfo.Columns)
					{
						targetColumns.Add(t);
						cmd.AppendParameter(ParameterMapping.CreateTableName(t.Name, CreateParameter(cmd.Command, t, t.Name), t.DataType, DBNull.Value));
					}
				}

				if (cmd.ParameterMappings.Count != targetColumns.Count)
					throw new InvalidOperationException();

				return (emitRowResults, targetColumns);
			} // proc PrepareColumnsForUpsert

			private static void AppendOutputValues(SqlTableInfo tableInfo, PpsSqlDataCommand cmd, StringBuilder commandText, string prefix, bool emitRowResults, IReadOnlyList<PpsSqlColumnInfo> targetColumns)
			{
				if (emitRowResults)
				{
					// first primary keys
					var first = true;
					foreach (var p in tableInfo.PrimaryKeys)
					{
						if (first)
							first = false;
						else
							commandText.Append(",");

						commandText.Append(prefix);
						p.AppendAsColumn(commandText);
						cmd.AppendResult(ResultMapping.CreateRowName(p.Name, p.DataType));
					}

					// append all target columns
					foreach (var t in targetColumns)
					{
						if (first)
							first = false;
						else
							commandText.Append(",");

						commandText.Append(prefix);
						t.AppendAsColumn(commandText);

						cmd.AppendResult(ResultMapping.CreateRowName(t.Name, t.DataType));
					}
				}
				else
				{
					var first = true;
					foreach (var col in tableInfo.Columns)
					{
						if (first)
							first = false;
						else
							commandText.Append(",");

						commandText.Append(prefix);
						col.AppendAsColumn(commandText);
						cmd.AppendResult(ResultMapping.CreateTableName(col.Name));
					}
				}
			} // proc AppendOutputValues

			#endregion

			#region -- PrepareInsert --------------------------------------------------

			private PpsSqlDataCommand PrepareInsert(LuaTable parameter, string name, LuaTable firstArgs)
			{
				/*
				 * insert into {name} ({columnList})
				 * output inserted.{column}, inserted.{column}
				 * values ({variableList}
				 */

				// find the connected table
				var tableInfo = SqlDataSource.ResolveTableByName<SqlTableInfo>(name, true);

				var cmd = CreateCommand(CommandType.Text, parameter);
				try
				{
					var first = true;
					var commandText = new StringBuilder();

					var (emitRowResults, targetColumns) = PrepareColumnsForUpsert(cmd, parameter, tableInfo, firstArgs);

					commandText.Append("INSERT INTO ")
						.Append(tableInfo.SqlQualifiedName);

					// insert columns
					commandText.Append(" (");

					first = true;
					foreach (var col in targetColumns)
					{
						if (col.IsIdentity) // do not insert primary key identity
							continue;

						if (first)
							first = false;
						else
							commandText.Append(",");

						col.AppendAsColumn(commandText);
					}

					// output clause
					if (parameter.GetOptionalValue("output", true))
					{
						commandText.Append(") OUTPUT ");
						AppendOutputValues(tableInfo, cmd, commandText, "inserted.", emitRowResults, targetColumns);
					}
					else
						commandText.Append(") ");

					// values
					commandText.Append(" VALUES (");

					first = true;
					foreach (var m in cmd.ParameterMappings)
					{
						if (first)
							first = false;
						else
							commandText.Append(',');

						commandText.Append(FormatParameterName(m.Parameter.ParameterName));
					}

					commandText.Append(") ");

					return cmd.Prepare(commandText.ToString());
				}
				catch
				{
					cmd?.Dispose();
					throw;
				}
			} // func ExecuteInsert

			#endregion

			#region -- ExecuteUpdate --------------------------------------------------

			private PpsSqlDataCommand PrepareUpdate(LuaTable parameter, string name, LuaTable firstArgs)
			{
				/*
				 * update {name} set {column} = {arg},
				 * output inserted.{column}, inserted.{column}
				 * where {PrimaryKey} = @arg
				 */

				// find the connected table
				var tableInfo = SqlDataSource.ResolveTableByName<SqlTableInfo>(name, true);

				var cmd = CreateCommand(CommandType.Text, parameter);
				try
				{
					var first = true;
					var commandText = new StringBuilder();

					var (emitRowResults, targetColumns) = PrepareColumnsForUpsert(cmd, parameter, tableInfo, firstArgs);

					commandText.Append("UPDATE ")
						.Append(tableInfo.SqlQualifiedName);

					commandText.Append(" SET ");

					// create the column list
					for (var i = 0; i < targetColumns.Count; i++)
					{
						if (targetColumns[i].IsIdentity) // do not update primary key
							continue;

						if (first)
							first = false;
						else
							commandText.Append(',');

						targetColumns[i].AppendAsColumn(commandText)
							.Append(" = ")
							.Append(FormatParameterName(cmd.ParameterMappings[i].Parameter.ParameterName));
					}

					// output clause
					if (parameter.GetOptionalValue("output", true))
					{
						commandText.Append(" OUTPUT ");
						AppendOutputValues(tableInfo, cmd, commandText, "inserted.", emitRowResults, targetColumns);
					}

					// where
					commandText.Append(" WHERE ");
					first = true;
					foreach (var p in tableInfo.PrimaryKeys)
					{
						CreateWhereParameter(commandText, cmd, p, first, p.Name, false);
						first = false;
					}

					return cmd.Prepare(commandText.ToString());
				}
				catch
				{
					cmd?.Dispose();
					throw;
				}

			} // func PrepareUpdate

			#endregion

			#region -- PrepareUpsert --------------------------------------------------

			private PpsSqlDataCommand PrepareUpsert(LuaTable parameter, string name, LuaTable firstArgs)
			{
				/*
				 * merge into table as dst
				 *	 using (values (@args), (@args)) as src
				 *	 on @primkey or on clause
				 *	 when matched then
				 *     set @arg = @arg, @arg = @arg, 
				 *	 when not matched then
				 *	   insert (@args) values (@args)
				 *	 output 
				 * 
				 */

				// prepare basic parameters for the merge command
				var tableInfo = SqlDataSource.ResolveTableByName<SqlTableInfo>(name, true);

				var cmd = CreateCommand(CommandType.Text, parameter);
				try
				{
					var first = true;
					var commandText = new StringBuilder();

					var (emitRowResults, targetColumns) = PrepareColumnsForUpsert(cmd, parameter, tableInfo, firstArgs);

					#region -- dst --
					commandText.Append("MERGE INTO ")
						.Append(tableInfo.SqlQualifiedName)
						.Append(" as DST ");
					#endregion

					#region -- src --
					var columnNames = new StringBuilder();
					commandText.Append("USING (VALUES (");

					first = true;
					foreach (var p in cmd.ParameterMappings)
					{
						if (first)
							first = false;
						else
							commandText.Append(", ");

						commandText.Append(FormatParameterName(p.Parameter.ParameterName));
					}

					commandText.Append(")) AS SRC (");
					first = true;
					foreach (var c in targetColumns)
					{
						if (first)
							first = false;
						else
							commandText.Append(", ");

						c.AppendAsColumn(commandText);
					}
					commandText.Append(") ");
					#endregion

					#region -- on --
					commandText.Append("ON ");
					var onClauseValue = parameter.GetMemberValue("on");
					if (onClauseValue == null) // no on clause use primary key
					{
						var col = tableInfo.PrimaryKey ?? throw new ArgumentNullException("primaryKey", $"Table {tableInfo.SqlQualifiedName} has no primary key (use the onClause).");
						PrepareUpsertAppendOnClause(commandText, col);
					}
					else if (onClauseValue is string onClauseString) // on clause is defined as expression
					{
						commandText.Append(onClauseString);
					}
					else if (onClauseValue is LuaTable onClause) // create the on clause from colums
					{
						first = true;
						foreach (var p in onClause.ArrayList)
						{
							if (first)
								first = false;
							else
								commandText.Append(" AND ");
							var col = tableInfo.FindColumn((string)p, true);
							PrepareUpsertAppendOnClause(commandText, col);
						}
					}
					else
						throw new ArgumentException("Can not interpret on-clause.");
					commandText.Append(" ");
					#endregion

					#region -- when matched --
					commandText.Append("WHEN MATCHED THEN UPDATE SET ");
					first = true;
					foreach (var col in targetColumns)
					{
						if (col.IsIdentity) // no autoincrement
							continue;
						else if (first)
							first = false;
						else
							commandText.Append(", ");
						commandText.Append("DST.");
						col.AppendAsColumn(commandText);
						commandText.Append(" = ");
						commandText.Append("SRC.");
						col.AppendAsColumn(commandText);
					}
					commandText.Append(' ');
					#endregion

					#region -- when not matched by target --
					commandText.Append("WHEN NOT MATCHED BY TARGET THEN INSERT (");
					first = true;
					foreach (var col in targetColumns)
					{
						if (col.IsIdentity)
							continue;
						else if (first)
							first = false;
						else
							commandText.Append(", ");
						col.AppendAsColumn(commandText);
					}
					commandText.Append(") VALUES (");
					first = true;
					foreach (var col in targetColumns)
					{
						if (col.IsIdentity)
							continue;
						else if (first)
							first = false;
						else
							commandText.Append(", ");
						commandText.Append("SRC.");
						col.AppendAsColumn(commandText);
					}
					commandText.Append(") ");
					#endregion

					#region -- when not matched by source --

					// delete, or update to deleted?
					if (parameter.GetMemberValue("nmsrc") is LuaTable notMatchedSource)
					{
						if (notMatchedSource["delete"] != null)
						{
							if (notMatchedSource["where"] is string whereDelete)
								commandText.Append("WHEN NOT MATCHED BY SOURCE AND (" + whereDelete + ") THEN DELETE ");
							else
								commandText.Append("WHEN NOT MATCHED BY SOURCE THEN DELETE ");
						}
					}

					#endregion

					#region -- output --
					if (parameter.GetOptionalValue("output", true))
					{
						commandText.Append("OUTPUT ");
						AppendOutputValues(tableInfo, cmd, commandText, "inserted.", emitRowResults, targetColumns);
					}


					#endregion

					commandText.Append(';');

					return cmd.Prepare(commandText.ToString());
				}
				catch
				{
					cmd?.Dispose();
					throw;
				}
			} // func Preparepsert

			private static void PrepareUpsertAppendOnClause(StringBuilder commandText, PpsSqlColumnInfo col)
			{
				if (col.Nullable)
				{
					commandText.Append('(');
					commandText.Append("SRC.");
					col.AppendAsColumn(commandText);
					commandText.Append(" IS NULL AND ");
					commandText.Append("DST.");
					col.AppendAsColumn(commandText);
					commandText.Append(" IS NULL OR ");
					commandText.Append("SRC.");
					col.AppendAsColumn(commandText);
					commandText.Append(" = ");
					commandText.Append("DST.");
					col.AppendAsColumn(commandText);
					commandText.Append(')');
				}
				else
				{
					commandText.Append("SRC.");
					col.AppendAsColumn(commandText);
					commandText.Append(" = ");
					commandText.Append("DST.");
					col.AppendAsColumn(commandText);
				}
			} // proc ExecuteUpsertAppendOnClause

			#endregion

			#region -- PrepareDelete --------------------------------------------------

			private PpsSqlDataCommand PrepareDelete(LuaTable parameter, string name, LuaTable firstArgs)
			{
				/*
				 * DELETE FROM name 
				 * OUTPUT
				 * WHERE Col = @Col
				 */

				// find the connected table
				var tableInfo = SqlDataSource.ResolveTableByName<SqlTableInfo>(name, true);
				var cmd = CreateCommand(CommandType.Text, parameter);
				try
				{
					var commandText = new StringBuilder();

					commandText.Append("DELETE ")
						.Append(tableInfo.SqlQualifiedName);

					// add primary key as out put
					if (parameter.GetOptionalValue("output", true))
					{
						commandText.Append(" OUTPUT ")
							.Append("deleted.")
							.Append(tableInfo.PrimaryKey.Name);
					}

					// append where
					commandText.Append(" WHERE ");

					var columnList = parameter.GetMemberValue("columnList");
					var first = true;
					if (columnList is IDataColumns columnMap)
					{
						throw new NotImplementedException();
					}
					else // analyse args
					{
						var args = GetArguments(parameter, 1, false);
						if (args != null)
						{
							foreach (var m in args.Members)
							{
								var column = tableInfo.FindColumn(m.Key, false);
								if (column == null)
									continue;

								CreateWhereParameter(commandText, cmd, column, first, m.Key, true);
								first = false;
							}
						}
					}

					// append primary keys
					if (first && !parameter.GetOptionalValue("all", false))
					{
						foreach (var columnInfo in tableInfo.PrimaryKeys)
						{
							CreateWhereParameter(commandText, cmd, columnInfo, first, columnInfo.Name);
							first = false;
						}
					}

					if (first && !parameter.GetOptionalValue("all", false))
						throw new ArgumentException("To delete all rows, set __all to true.");

					return cmd.Prepare(commandText.ToString());
				}
				catch
				{
					cmd?.Dispose();
					throw;
				}
			} // func PrepareDelete

			#endregion

			/// <summary></summary>
			/// <param name="parameter"></param>
			/// <param name="firstArgs"></param>
			/// <returns></returns>
			protected override PpsDataCommand PrepareCore(LuaTable parameter, LuaTable firstArgs)
			{
				string name;
				if ((name = (string)parameter["insert"]) != null)
					return PrepareInsert(parameter, name, firstArgs);
				else if ((name = (string)parameter["update"]) != null)
					return PrepareUpdate(parameter, name, firstArgs);
				else if ((name = (string)parameter["delete"]) != null)
					return PrepareDelete(parameter, name, firstArgs);
				else if ((name = (string)parameter["upsert"]) != null)
					return PrepareUpsert(parameter, name, firstArgs);
				else
					return base.PrepareCore(parameter, firstArgs);
			} // func PrepareCore

			#endregion

			/// <summary>Intercepts messages from the connection.</summary>
			/// <param name="log"></param>
			/// <returns></returns>
			public IDisposable LogMessages(LogMessageScopeProxy log)
				=> new SqlConnectionLogger(SqlConnection, log);

			/// <summary></summary>
			public PpsMsSqlDataSource SqlDataSource => (PpsMsSqlDataSource)DataSource;
			/// <summary></summary>
			public SqlConnection SqlConnection => DbConnection;
			/// <summary></summary>
			public SqlTransaction SqlTransaction => DbTransaction;
		} // class PpsMsSqlDataTransaction

		#endregion

		#region -- class PpsMsSqlSynchronizationCache ---------------------------------

		private sealed class PpsMsSqlSynchronizationCache
		{
			private readonly PpsMsSqlDataSource dataSource;
			private readonly SemaphoreSlim changeTrackingSync = new SemaphoreSlim(1);

			private readonly object changeTrackingLock = new object();
			private long databaseCreationTime = -1L;
			private long lastChangeTrackingId = -1;
			private readonly Dictionary<long, long> minValidVersions = new Dictionary<long, long>();

			public PpsMsSqlSynchronizationCache(PpsMsSqlDataSource dataSource)
			{
				this.dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
			} // ctor

			public bool TryGetMinValidVersion(SqlTableInfo tableInfo, out long minValidVersion, out long currentVersion, out long databaseCreationTime)
			{
				lock (changeTrackingLock)
				{
					databaseCreationTime = this.databaseCreationTime;
					currentVersion = lastChangeTrackingId;
					return minValidVersions.TryGetValue(tableInfo.ObjectId, out minValidVersion);
				}
			} // func TryGetMinValidVersion

			#region -- RefreshChangeTrackingAsync -------------------------------------

			private async Task RefreshChangeTrackingCoreAsync()
			{
				await changeTrackingSync.WaitAsync();
				try
				{
					var isChanged = false;

					if (databaseCreationTime < 0L)
					{
						using (var cmd = dataSource.masterConnection.CreateCommand())
						{
							cmd.CommandText = "SELECT create_date FROM sys.databases WHERE database_id = DB_ID()";
							databaseCreationTime = ((DateTime)await cmd.ExecuteScalarAsync()).ToFileTimeUtc();
						}
					}

					using (var cmd = dataSource.masterConnection.CreateCommand())
					{
						cmd.Parameters.AddWithValue("@LASTVERSION", lastChangeTrackingId);
						cmd.CommandText = "BEGIN\r\n" +
							"\tDECLARE @CURVERSION BIGINT;\r\n" +
							"\tSET @CURVERSION = change_tracking_current_version();\r\n" +
							"\tIF  @LASTVERSION < @CURVERSION\r\n" +
							"\t\tSELECT @CURVERSION, object_id, min_valid_version from sys.change_tracking_tables;\r\n" +
							"\tELSE\r\n" +
							"\t\tSELECT @CURVERSION\r\n" +
							"END;";

						using (var r = await cmd.ExecuteReaderAsync(CommandBehavior.SingleResult))
						{
							var newChangeTrackingVersion = -1L;
							var newMinValidVersions = new List<Tuple<int, long>>(minValidVersions.Count);

							while (await r.ReadAsync())
							{
								if (r.FieldCount > 1)
									newMinValidVersions.Add(Tuple.Create(r.GetInt32(1), r.GetInt64(2)));
								newChangeTrackingVersion = r.IsDBNull(0) ? 0 : r.GetInt64(0);
							}

							if (newChangeTrackingVersion != -1L)
							{
								lock (changeTrackingLock)
								{
									if (newChangeTrackingVersion != lastChangeTrackingId)
										lastChangeTrackingId = newChangeTrackingVersion;

									for (var i = 0; i < newMinValidVersions.Count; i++)
									{
										var tableId = newMinValidVersions[i].Item1;
										var newMinValidVersion = newMinValidVersions[i].Item2;
										if (!minValidVersions.TryGetValue(tableId, out var currentMinValidVersion) || currentMinValidVersion != newMinValidVersion)
										{
											dataSource.Log.Debug("[RefreshChangeTracking] ({0}) {1} -> {2}", tableId, currentMinValidVersion, newMinValidVersion);
											minValidVersions[tableId] = newMinValidVersion;
											isChanged = true;
										}
									}
								}
							}
						}
					}

					// notify clients, something has changed
					if (isChanged)
					{
						dataSource.Application.FireDataChangedEvent(dataSource.Name);
					}
				}
				finally
				{
					changeTrackingSync.Release();
				}
			} // proc RefreshChangeTrackingCoreAsync

			public Task RefreshChangeTrackingAsync()
				=> RefreshChangeTrackingCoreAsync();

			#endregion

			public long CurerntVersion => lastChangeTrackingId;
		} // class PpsMsSqlSynchronizationCache

		#endregion

		#region -- class PpsMsSqlSynchronizationBatchParts ----------------------------

		private sealed class PpsMsSqlSynchronizationBatchParts : IPpsDataSynchronizationBatch
		{
			#region -- class DataRowProxy -----------------------------------------

			private sealed class DataRowProxy : IDataRow
			{
				private readonly PpsMsSqlSynchronizationBatchParts owner;

				public DataRowProxy(PpsMsSqlSynchronizationBatchParts owner)
				{
					this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
				} // ctor

				private object GetValue(int columnIndex)
				{
					var fieldIndex = owner.columnMapping[columnIndex];
					var reader = GetReader();
					if (owner.tableInfo.ChangeTracking == SqlTableChangeTracking.Rows
						|| owner.columnIsPrimary[columnIndex])
						return reader.GetValue(fieldIndex);
					else
					{
						var isChanged = reader.GetInt32(fieldIndex + 1) != 0;
						return isChanged ? reader.GetValue(fieldIndex) : null;
					}
				} // func GetValue

				public bool TryGetProperty(string name, out object value)
				{
					var idx = Array.FindIndex(owner.columns, c => String.Compare(c.Name, name, StringComparison.OrdinalIgnoreCase) == 0);
					if (idx == -1)
					{
						value = null;
						return false;
					}
					else
					{
						value = GetValue(idx);
						return true;
					}
				} // func TryGetProperty

				public object this[int columnIndex] => GetValue(columnIndex);

				public object this[string columnName, bool throwException = true]
				{
					get
					{
						if (TryGetProperty(columnName, out var v))
							return v;
						else if (throwException)
							throw new ArgumentOutOfRangeException(nameof(columnName), columnName, "Column not found.");
						else
							return null;
					}
				} // prop this

				private SqlDataReader GetReader()
					=> owner.reader;

				public char CurrentMode // sys_change_operation
				{
					get
					{
						var m = Char.ToLower(GetReader().GetString(0)[0]);
						if (owner.tableInfo.ChangeTracking == SqlTableChangeTracking.Rows)
							return m == 'u' ? 'r' : m;
						else
							return m;
					}
				} // func CurrentMode

				public long CurrentVersion => GetReader().GetInt64(1); // sys_change_version

				public IReadOnlyList<IDataColumn> Columns => owner.Columns;
				public bool IsDataOwner => false;
			} // class DataRowProxy

			#endregion

			private readonly SqlTableInfo tableInfo;
			private readonly long lastChangeTrackingVersion;
			private readonly long currentChangeTrackingVersion;

			private readonly SqlCommand command;
			private readonly IDataColumn[] columns;
			private readonly int[] columnMapping;
			private readonly bool[] columnIsPrimary;
			private readonly DataRowProxy dataRowProxy;
			private SqlDataReader reader = null;
			private bool? hasRow = null;

			public PpsMsSqlSynchronizationBatchParts(SqlConnection connection, SqlTableInfo tableInfo, long lastChangeTrackingVersion, long currentChangeTrackingVersion)
			{
				this.tableInfo = tableInfo ?? throw new ArgumentNullException(nameof(tableInfo));
				this.lastChangeTrackingVersion = lastChangeTrackingVersion;
				this.currentChangeTrackingVersion = currentChangeTrackingVersion;

				InitCommand(tableInfo, lastChangeTrackingVersion, out var commandText, out columns, out columnMapping, out columnIsPrimary);

				dataRowProxy = new DataRowProxy(this);
				command = connection.CreateCommand();
				command.CommandType = CommandType.Text;
				command.CommandText = commandText;
			} // ctor

			public void Dispose()
			{
				CloseReader();
				command.Dispose();
			} // proc Dispose

			private void OpenReader()
			{
				reader = command.ExecuteReader(CommandBehavior.SingleResult);
				hasRow = null;
			} // proc OpenReader

			private void CloseReader()
			{
				if (reader != null)
				{
					reader.Close();
					reader = null;
				}
				hasRow = null;
			} // proc CloseReader

			private DataRowProxy GetValidRowProxy()
			{
				if (hasRow.HasValue && hasRow.Value)
					return dataRowProxy;
				throw new InvalidOperationException();
			} // func GetValidRowProxy

			public void Reset()
				=> CloseReader();

			public bool MoveNext()
			{
				if (reader == null)
					OpenReader();

				var r = reader.Read();
				hasRow = r;
				return r;
			} // func MoveNext

			public IReadOnlyList<IDataColumn> Columns => columns;

			object IEnumerator.Current => Current;
			public IDataRow Current => GetValidRowProxy();
			public char CurrentMode => GetValidRowProxy().CurrentMode;

			public long CurrentSyncId
			{
				get
				{
					if (hasRow.HasValue) // hat zeilen gelesen
					{
						if (hasRow.Value) // hat eine aktive zeile
							return dataRowProxy.CurrentVersion;
						else
							return currentChangeTrackingVersion;
					}
					else
						return lastChangeTrackingVersion;
				}
			} // func CurrentSyncId

			public PpsSynchonizationMode Mode => PpsSynchonizationMode.Parts;

			#region -- CreateCommandText ----------------------------------------------

			private static void InitCommand(SqlTableInfo tableInfo, long lastChangeTrackingVersion, out string commandText, out IDataColumn[] columns, out int[] columnMapping, out bool[] columnIsPrimary)
			{
				var sb = new StringBuilder();

				sb.Append("SELECT ct.sys_change_operation,ct.sys_change_version");

				var columnInfos = new List<Tuple<int, bool, IDataColumn>>();
				var columnOffset = 2;

				// add changed columns
				if (tableInfo.ChangeTracking == SqlTableChangeTracking.Columns)
				{
					foreach (var col in tableInfo.Columns.Cast<SqlColumnInfo>())
					{
						var isPrimary = col.IsPrimaryKey;
						sb.Append(',').Append(isPrimary ? "ct" : "d").Append(".[").Append(col.Name).Append("]");
						sb.Append(",CHANGE_TRACKING_IS_COLUMN_IN_MASK(").Append(col.ColumnId).Append(", ct.sys_change_columns)");
						columnInfos.Add(Tuple.Create<int, bool, IDataColumn>(columnOffset, isPrimary, new SimpleDataColumn(col.Name, col.DataType)));
						columnOffset += 2;
					}
				}
				else
				{
					foreach (var col in tableInfo.PrimaryKeys)
					{
						sb.Append(",ct.[").Append(col.Name).Append("]");
						columnInfos.Add(Tuple.Create<int, bool, IDataColumn>(columnOffset, true, new SimpleDataColumn(col.Name, col.DataType)));
						columnOffset++;
					}
				}

				// from
				sb.Append(" FROM changetable(changes ").Append(tableInfo.SqlQualifiedName).Append(",").Append(lastChangeTrackingVersion).Append(") AS ct")
					.Append(" LEFT OUTER JOIN ").Append(tableInfo.QualifiedName).Append(" d ON ");
				var first = true;
				foreach (var col in tableInfo.PrimaryKeys)
				{
					if (first)
						first = false;
					else
						sb.Append(" AND ");
					sb.Append("ct.[").Append(col.Name).Append("]").Append("=").Append("d.[").Append(col.Name).Append("]");
				}

				// prepare result
				commandText = sb.ToString();

				columnMapping = new int[columnInfos.Count];
				columnIsPrimary = new bool[columnInfos.Count];
				columns = new IDataColumn[columnInfos.Count];
				for(var i = 0;i< columnInfos.Count;i++)
				{
					columnMapping[i] = columnInfos[i].Item1;
					columnIsPrimary[i] = columnInfos[i].Item2;
					columns[i] = columnInfos[i].Item3;
				}
			} // func CreateCommandText

			#endregion
		} // class PpsMsSqlSynchronizationBatch

		#endregion

		#region -- class PpsMsSqlSynchronizationBatchNone -----------------------------

		private sealed class PpsMsSqlSynchronizationBatchNone : IPpsDataSynchronizationBatch
		{
			private readonly PpsSynchonizationMode mode;
			private readonly long currentChangeTrackingVersion;

			public PpsMsSqlSynchronizationBatchNone(PpsSynchonizationMode mode, long currentChangeTrackingVersion)
			{
				this.mode = mode;
				this.currentChangeTrackingVersion = currentChangeTrackingVersion;
			} // ctor

			public void Dispose() { }

			public void Reset() { }

			public bool MoveNext()
				=> false;
			
			public IReadOnlyList<IDataColumn> Columns => throw new InvalidOperationException();

			object IEnumerator.Current => Current;
			public IDataRow Current => throw new InvalidOperationException();
			public char CurrentMode => throw new InvalidOperationException();

			public long CurrentSyncId => currentChangeTrackingVersion;
			public PpsSynchonizationMode Mode => mode;
		} // class PpsMsSqlSynchronizationBatchNone

		#endregion

		#region -- class PpsMsSqlDataSynchronization ----------------------------------

		/// <summary>Synchronization Batch</summary>
		protected class PpsMsSqlDataSynchronization : PpsDataSynchronization
		{
			private readonly long lastSyncronizationStamp;

			/// <inherited />
			public PpsMsSqlDataSynchronization(PpsApplication application, IPpsConnectionHandle connection, long lastSyncronizationStamp, bool leaveConnectionOpen)
				: base(application, connection, leaveConnectionOpen)
			{
				this.lastSyncronizationStamp = lastSyncronizationStamp;
			} // ctor

			/// <inhertied/>
			public override PpsDataSelector CreateSelector(string tableName, long lastSyncId)
				=> base.CreateSelector(tableName, lastSyncId);

			/// <inhertied/>
			public override IPpsDataSynchronizationBatch GetChanges(string tableName, long lastSyncId)
			{
				var tableInfo = (SqlTableInfo)DataSource.GetTableDescription(tableName, true);

				var connection = DataSource.masterConnection;
				if (tableInfo.ChangeTracking == SqlTableChangeTracking.None) // no change tracking active, return nothing
				{
					var currentChangeTrackingVersion = DataSource.changeTracking.CurerntVersion;
					return new PpsMsSqlSynchronizationBatchNone(lastSyncId == -1L ? PpsSynchonizationMode.Full : PpsSynchonizationMode.None, currentChangeTrackingVersion);
				}
				else
				{
					if (DataSource.changeTracking.TryGetMinValidVersion(tableInfo, out var minValidVersion, out var currentChangeTrackingVersion, out var databaseCreationTime))
					{
						var isForceFull = databaseCreationTime > lastSyncronizationStamp; // recreate database
						if (isForceFull || lastSyncId < minValidVersion)
							return new PpsMsSqlSynchronizationBatchNone(PpsSynchonizationMode.Full, currentChangeTrackingVersion);
						else if (lastSyncId < currentChangeTrackingVersion)
						{
							DataSource.Log.Debug("[GetChanges] {0}({1}) {2} -> {3}", tableInfo.TableName, tableInfo.ObjectId, lastSyncId, currentChangeTrackingVersion);
							return new PpsMsSqlSynchronizationBatchParts(connection, tableInfo, lastSyncId, currentChangeTrackingVersion);
						}
						else
							return new PpsMsSqlSynchronizationBatchNone(PpsSynchonizationMode.None, currentChangeTrackingVersion);
					}
					else
						return new PpsMsSqlSynchronizationBatchNone(PpsSynchonizationMode.Full, currentChangeTrackingVersion);
				}
			} // func GetChanges

			/// <inhertied/>
			public override void RefreshChanges()
				=> DataSource.changeTracking.RefreshChangeTrackingAsync().AwaitTask();

			/// <summary>Access datasource</summary>
			public PpsMsSqlDataSource DataSource => (PpsMsSqlDataSource)Connection.DataSource;
		} // class PpsMsSqlDataSynchronization

		#endregion

		#region -- interface ISqlParameterTypeInfo ------------------------------------

		private interface ISqlParameterTypeInfo
		{
			SqlDbType SqlType { get; }
			int MaxLength { get; }
			byte Precision { get; }
			byte Scale { get; }

			string TypeName { get; }

			string XmlSchemaCollectionDatabase { get; }
			string XmlSchemaCollectionName { get; }
			string XmlSchemaCollectionOwningSchema { get; }
		} // interface ISqlParameterTypeInfo

		#endregion

		#region -- class SqlColumnInfo ------------------------------------------------

		private sealed class SqlColumnInfo : PpsSqlColumnInfo, ISqlParameterTypeInfo
		{
			private readonly int columnId;
			private readonly SqlDbType sqlType;

			private readonly string typeName;

			private readonly string xmlSchemaCollectionDatabase;
			private readonly string xmlSchemaCollectionName;
			private readonly string xmlSchemaCollectionOwningSchema;

			public SqlColumnInfo(PpsSqlTableInfo table, SqlDataReader r)
				: base(table,
					  columnName: r.GetString(2),
					  dataType: GetFieldType(r.GetByte(3), r.IsDBNull(10) ? null : r.GetString(10)),
					  maxLength: r.GetInt16(4),
					  precision: r.GetByte(5),
					  scale: r.GetByte(6),
					  isNullable: r.GetBoolean(7),
					  isIdentity: r.GetBoolean(8),
					  isPrimaryKey: r.GetBoolean(9)
				)
			{
				columnId = r.GetInt32(1);

				var t = r.GetByte(3);
				sqlType = GetSqlType(t);

				typeName = typeName = r.IsDBNull(10) ? null : r.GetString(10);

				xmlSchemaCollectionDatabase = r.IsDBNull(11) ? null : r.GetString(11);
				xmlSchemaCollectionName = r.IsDBNull(12) ? null : r.GetString(12);
				xmlSchemaCollectionOwningSchema = r.IsDBNull(13) ? null : r.GetString(13);
			} // ctor

			protected override IEnumerator<PropertyValue> GetProperties()
			{
				using (var e = base.GetProperties())
				{
					while (e.MoveNext())
						yield return e.Current;
				}

				yield return new PropertyValue("Sql.Type", typeof(SqlDbType), SqlType);

				if (typeName != null)
					yield return new PropertyValue("Sql.TypeName", typeof(string), typeName);
			} // func GetProperties

			protected override bool TryGetProperty(string propertyName, out object value)
			{
				if (!base.TryGetProperty(propertyName, out value))
				{
					if (String.Compare(propertyName, "Sql.Type", StringComparison.OrdinalIgnoreCase) == 0)
					{
						value = SqlType;
						return true;
					}
					else if (String.Compare(propertyName, "Sql.TypeName", StringComparison.OrdinalIgnoreCase) == 0)
					{
						value = typeName;
						return true;
					}
					else
					{
						value = null;
						return false;
					}
				}
				else
					return true;
			} // func TryGetProperty

			public override void InitSqlParameter(DbParameter parameter, string parameterName, object value)
			{
				if (String.IsNullOrEmpty(parameterName))
					parameterName = "@" + Name;

				base.InitSqlParameter(parameter, parameterName, value);

				InitSqlParameterType((SqlParameter)parameter, this);
			} // proc InitSqlParameter

			internal static void InitSqlParameterType(SqlParameter p, ISqlParameterTypeInfo sqlType)
			{
				var t = sqlType.SqlType;
				p.SqlDbType = t;
				switch (t)
				{
					case SqlDbType.NVarChar:
					case SqlDbType.VarBinary:
					case SqlDbType.VarChar:
						p.Size = -1;
						break;
					case SqlDbType.Binary:
					case SqlDbType.NChar:
					case SqlDbType.Char:
						p.Size = sqlType.MaxLength;
						break;
					case SqlDbType.Decimal:
						p.Precision = sqlType.Precision;
						p.Scale = sqlType.Scale;
						break;
					case SqlDbType.Udt:
						p.UdtTypeName = sqlType.TypeName;
						break;
					case SqlDbType.Structured:
						p.TypeName = sqlType.TypeName;
						break;
					case SqlDbType.Xml:
						p.XmlSchemaCollectionDatabase = sqlType.XmlSchemaCollectionDatabase;
						p.XmlSchemaCollectionName = sqlType.XmlSchemaCollectionName;
						p.XmlSchemaCollectionOwningSchema = sqlType.XmlSchemaCollectionOwningSchema;
						break;
					case SqlDbType.Time:
					case SqlDbType.DateTime2:
						p.SqlDbType = SqlDbType.DateTimeOffset;
						p.Scale = sqlType.Scale;
						break;
				}
			} // proc InitSqlParameterType

			#region -- GetFieldType, GetSqlType -----------------------------------------------

			private static readonly string[] geometryTypeNames = {
				"sys.Geometry",
				"sys.Point",
				"sys.LineString",
				"sys.Polygon",
				"sys.Curve",
				"sys.Surface",
				"sys.MultiPoint",
				"sys.MultiLineString",
				"sys.MultiPolygon",
				"sys.MultiCurve",
				"sys.MultiSurface",
				"sys.GeometryCollection",
				"sys.FullGlobe",
				"sys.CircularString",
				"sys.CompoundCurve",
				"sys.CurvePolygon"
			};

			private static Type GetFieldType(byte systemTypeId, string userTypeName)
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
						return typeof(double); // float seems to be a double
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

					case 240: // GEOGRAPHY, UserDefinedType
						if (userTypeName != null && Array.Exists(geometryTypeNames, c => String.Compare(c, userTypeName, StringComparison.OrdinalIgnoreCase) == 0))
							return typeof(Microsoft.SqlServer.Types.SqlGeography);
						else
							return typeof(object);

					case 241: // xml
						return typeof(string);

					case 243: // table_type
						return typeof(IEnumerable<SqlDataRecord>);

					default:
						throw new IndexOutOfRangeException($"Unexpected sql server system type: {systemTypeId}");
				}
			} // func GetFieldType

			internal static SqlDbType GetSqlType(byte systemTypeId)
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

					case 240: // GEOGRAPHY, User Defined Type
						return SqlDbType.Udt;

					case 241: // xml
						return SqlDbType.Xml;

					case 243: // table type
						return SqlDbType.Structured;

					default:
						throw new IndexOutOfRangeException($"Unexpected sql server system type: {systemTypeId}");
				}
			} // func GetSqlType

			#endregion

			public int ColumnId => columnId;

			public SqlDbType SqlType => sqlType;

			public string TypeName => typeName;

			public string XmlSchemaCollectionDatabase => xmlSchemaCollectionDatabase;
			public string XmlSchemaCollectionName => xmlSchemaCollectionName;
			public string XmlSchemaCollectionOwningSchema => xmlSchemaCollectionOwningSchema;
		} // class SqlColumnInfo

		#endregion

		#region -- class SqlTableInfo -------------------------------------------------

		private enum SqlTableChangeTracking
		{
			None,
			Rows,
			Columns
		} // enum SqlTableChangeTracking

		private sealed class SqlTableInfo : PpsSqlTableInfo
		{
			private readonly long objectId;
			private readonly SqlTableChangeTracking changeTracking;

			public SqlTableInfo(SqlDataReader r)
				: base(r.GetString(1), r.GetString(2))
			{
				objectId = r.GetInt32(0);
				changeTracking = r.IsDBNull(3) ? SqlTableChangeTracking.None : (r.GetBoolean(3) ? SqlTableChangeTracking.Columns : SqlTableChangeTracking.Rows);
			} // ctor

			public long ObjectId => objectId;
			public SqlTableChangeTracking ChangeTracking => changeTracking;
		} // class SqlTableInfo

		#endregion

		#region -- class SqlRelationInfo ----------------------------------------------

		private sealed class SqlRelationInfo : PpsSqlRelationInfo
		{
			public SqlRelationInfo(string name, SqlColumnInfo parentColumn, SqlColumnInfo referencedColumn)
				: base(name, parentColumn, referencedColumn)
			{
			} // ctor
		} // class SqlRelationInfo

		#endregion

		#region -- class SqlParameterInfo ---------------------------------------------

		private sealed class SqlParameterInfo : PpsSqlParameterInfo, ISqlParameterTypeInfo
		{
			private readonly SqlDbType dbType;
			private readonly int maxLength;
			private readonly byte scale;
			private readonly byte precision;

			private readonly string typeName;
			private readonly string xmlSchemaCollectionDatabase;
			private readonly string xmlSchemaCollectionName;
			private readonly string xmlSchemaCollectionOwningSchema;

			internal SqlParameterInfo(string name)
				: base(name, ParameterDirection.ReturnValue, false)
			{
				dbType = SqlDbType.Int;
				maxLength = 0;
				precision = 0;
				scale = 0;
				typeName = null;
				xmlSchemaCollectionDatabase = null;
				xmlSchemaCollectionName = null;
				xmlSchemaCollectionOwningSchema = null;
			} // ctor

			public SqlParameterInfo(IDataRecord r)
				: base(r.GetString(1), (ParameterDirection)r.GetByte(2), r.GetBoolean(7))
			{
				dbType = SqlColumnInfo.GetSqlType(r.GetByte(3));
				maxLength = r.GetInt16(4);
				precision = r.GetByte(5);
				scale = r.GetByte(6);

				typeName = r.IsDBNull(8) ? null : r.GetString(8);

				xmlSchemaCollectionDatabase = r.IsDBNull(9) ? null : r.GetString(9);
				xmlSchemaCollectionName = r.IsDBNull(10) ? null : r.GetString(10);
				xmlSchemaCollectionOwningSchema = r.IsDBNull(11) ? null : r.GetString(11);
			} // ctor

			public override string ToString()
				=> $"{Name} {dbType}";

			public override void InitSqlParameter(DbParameter parameter)
			{
				var p = (SqlParameter)parameter;
				p.ParameterName = Name;
				p.Direction = Direction;

				SqlColumnInfo.InitSqlParameterType(p, this);
			} // proc InitSqlParameter

			public SqlDbType SqlType => dbType;
			public int MaxLength => maxLength;
			public byte Precision => precision;
			public byte Scale => scale;

			public string TypeName => typeName;

			public string XmlSchemaCollectionDatabase => xmlSchemaCollectionDatabase;
			public string XmlSchemaCollectionName => xmlSchemaCollectionName;
			public string XmlSchemaCollectionOwningSchema => xmlSchemaCollectionOwningSchema;

		} // class SqlParameterInfo

		#endregion

		#region -- class SqlProcedureInfo ---------------------------------------------

		private sealed class SqlProcedureInfo : PpsSqlProcedureInfo
		{
			private bool hasReturnValue = false;
			private bool hasOutput = false;
			private readonly bool hasResult = false;

			public SqlProcedureInfo(IDataRecord r)
				: base(r.GetString(1), r.GetString(2))
			{
				hasResult = r.GetBoolean(3);
			} // ctor

			public override void AddParameter(PpsSqlParameterInfo parameterInfo)
			{
				if (ParameterCount == 0)
				{
					if (parameterInfo.Direction != ParameterDirection.ReturnValue) // enforce int return, but mark procedure/function that it has no return
					{
						base.AddParameter(new SqlParameterInfo("@RETURN_VALUE"));
						hasReturnValue = false;
					}
					else
						hasReturnValue = true;
				}
				else if ((parameterInfo.Direction & ParameterDirection.Output) == ParameterDirection.Output)
					hasOutput = true;

				base.AddParameter(parameterInfo);
			} // func AddParameter

			public override void AddResult(PpsSqlParameterInfo resultInfo)
				=> base.AddResult(resultInfo);

			public override bool HasResult => hasResult;
			public override bool HasOutput => hasOutput;
			public override bool HasReturnValue => hasReturnValue;
		} // class SqlProcedureInfo

		#endregion

		private readonly SqlConnection masterConnection;
		private readonly PpsMsSqlSynchronizationCache changeTracking;
		private string sysUserName = null;
		private SecureString sysPassword = null;
		private DEThread databaseMainThread = null;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary></summary>
		/// <param name="sp"></param>
		/// <param name="name"></param>
		public PpsMsSqlDataSource(IServiceProvider sp, string name)
			: base(sp, name)
		{
			masterConnection = new SqlConnection();
			changeTracking = new PpsMsSqlSynchronizationCache(this);
		} // ctor

		/// <summary></summary>
		/// <param name="disposing"></param>
		protected override void Dispose(bool disposing)
		{
			try
			{
				if (disposing)
				{
					// finish the connection
					CloseMasterConnection();

					// dispose connection
					masterConnection?.Dispose();

					databaseMainThread?.Dispose();
					sysPassword?.Dispose();
				}
			}
			finally
			{
				base.Dispose(disposing);
			}
		} // proc Dispose

		/// <summary></summary>
		/// <param name="config"></param>
		protected override void OnBeginReadConfiguration(IDEConfigLoading config)
		{
			base.OnBeginReadConfiguration(config);

			config.Tags.SetProperty("sysuser", config.ConfigNew.GetAttribute("sysuser", (string)null));
			config.Tags.SetProperty("syspassword", ProcsDE.DecodePassword(config.ConfigNew.GetAttribute("syspassword", (string)null)));
		} // proc OnBeginReadConfiguration

		/// <summary></summary>
		/// <param name="config"></param>
		protected override void OnEndReadConfiguration(IDEConfigLoading config)
		{
			if (config.Tags.TryGetProperty<string>("sysuser", out var tmpUser) && !String.IsNullOrWhiteSpace(tmpUser)
				&& config.Tags.TryGetProperty<SecureString>("syspassword", out var tmpPwd))
			{
				sysUserName = tmpUser;
				sysPassword = tmpPwd;
				sysPassword.MakeReadOnly();
			}
			else
			{
				sysUserName = null;
				sysPassword = null;
			}

			base.OnEndReadConfiguration(config);
		} // proc OnEndReadConfiguration

		#endregion

		#region -- Initialize Schema --------------------------------------------------

		private SqlColumnInfo ResolveTableColumnById(PpsSqlTableInfo tableInfo, int columnId)
		{
			var column = tableInfo.Columns.Cast<SqlColumnInfo>().Where(c => c.ColumnId == columnId).FirstOrDefault()
				?? throw new ArgumentOutOfRangeException(nameof(columnId), columnId, $"Could not resolve column {columnId} in table {tableInfo.QualifiedName}");
			return column;
		} // func ResolveTableColumnById

		/// <summary>Read database schema</summary>
		protected override void RefreshSchemaCore(IPpsSqlSchemaUpdate scope)
		{
			using (UseMasterConnection(out var connection))
			using (var cmd = ((SqlConnection)connection).CreateCommand())
			{
				cmd.CommandType = CommandType.Text;
				cmd.CommandTimeout = 1200;
				cmd.CommandText = GetResourceScript(typeof(PpsSqlExDataSource), "tsql.ConnectionInitScript.sql");

				// read all tables
				using (var r = cmd.ExecuteReader(CommandBehavior.Default))
				{
					var tableIndex = new Dictionary<int, PpsSqlTableInfo>();

					while (r.Read())
					{
						var objectId = r.GetInt32(0);
						try
						{
							var tab = new SqlTableInfo(r);
							scope.AddTable(tab);
							tableIndex[objectId] = tab;
						}
						catch (Exception e)
						{
							scope.Failed("table", objectId, e);
						}
					}

					if (!r.NextResult())
						throw new InvalidOperationException();

					// read all columns of the tables
					while (r.Read())
					{
						try
						{
							if (tableIndex.TryGetValue(r.GetInt32(0), out var table))
								scope.AddColumn(new SqlColumnInfo(table, r));
						}
						catch (Exception e)
						{
							scope.Failed("column", r.GetValue(2), e);
						}
					}

					if (!r.NextResult())
						throw new InvalidOperationException();

					// read all relations between the tables
					while (r.Read())
					{
						if (tableIndex.TryGetValue(r.GetInt32(2), out var parentTableInfo)
							&& tableIndex.TryGetValue(r.GetInt32(4), out var referencedTableInfo))
						{
							var parentColumn = ResolveTableColumnById(parentTableInfo, r.GetInt32(3));
							var referencedColumn = ResolveTableColumnById(referencedTableInfo, r.GetInt32(5));

							scope.AddRelation(new SqlRelationInfo(r.GetString(1), parentColumn, referencedColumn));
						}
					}

					if (!r.NextResult())
						throw new InvalidOperationException();

					// read all stored procedures/functions
					var procedureIndex = new Dictionary<int, SqlProcedureInfo>();
					while (r.Read())
					{
						var objectId = r.GetInt32(0);
						try
						{
							var tab = new SqlProcedureInfo(r);
							scope.AddProcedure(tab);
							procedureIndex[objectId] = tab;
						}
						catch (Exception e)
						{
							scope.Failed("procedure", objectId, e);
						}
					}

					if (!r.NextResult())
						throw new InvalidOperationException();

					// read all arguments
					while (r.Read())
					{
						if (procedureIndex.TryGetValue(r.GetInt32(0), out var procedureInfo))
							procedureInfo.AddParameter(new SqlParameterInfo(r));
					}

					// todo: read all result columns
					// select * from sys.columns where object_id = 1931934750

				} // using r
			}
		} // proc InitializeSchemaCore

		#endregion

		#region -- Connection String --------------------------------------------------

		/// <summary></summary>
		/// <param name="connectionString"></param>
		protected override void InitMasterConnection(DbConnectionStringBuilder connectionString)
		{
			var sqlConnectionString = (SqlConnectionStringBuilder)connectionString;
			lock (masterConnection)
			{
				// close the current connection
				masterConnection.Close();

				// use integrated security by default
				if (sysUserName != null)
				{
					Log.Info("Init master connection with {0}", sysUserName);
					sqlConnectionString.UserID = sysUserName;
					sqlConnectionString.Password = sysPassword?.AsPlainText();
					sqlConnectionString.IntegratedSecurity = false;
				}
				else
				{
					Log.Info("Init master connection with integrated security.");
					sqlConnectionString.UserID = String.Empty;
					sqlConnectionString.Password = String.Empty;
					sqlConnectionString.IntegratedSecurity = true;
				}

				// set the new connection
				masterConnection.ConnectionString = sqlConnectionString.ToString();

				// start background thread
				databaseMainThread = new DEThread(this, "Database", ExecuteDatabaseAsync);
			}
		} // proc InitMasterConnection

		/// <summary></summary>
		protected override void CloseMasterConnection()
			=> Procs.FreeAndNil(ref databaseMainThread);

		/// <summary></summary>
		/// <param name="throwException"></param>
		/// <returns></returns>
		public override IPpsConnectionHandle CreateConnection(bool throwException = true)
			=> new SqlConnectionHandle(this);

		#endregion

		#region -- Execute Database ---------------------------------------------------

		private async Task ExecuteDatabaseAsync(DEThread thread)
		{
			var lastExceptionNumber = 0;
			
			while (thread.IsRunning)
			{
				var executeStartTick = Environment.TickCount;
				try
				{
					try
					{
						// reset connection
						if (masterConnection.State == ConnectionState.Broken)
						{
							Log.Warn("Reset connection.");
							masterConnection.Close();
						}

						// open connection
						if (masterConnection.State == ConnectionState.Closed)
						{
							Log.Info("Open database connection.");
							await masterConnection.OpenAsync();
						}

						// execute background task
						if (masterConnection.State == ConnectionState.Open)
						{
							if (!IsSchemaInitialized)
								InitializeSchema();

							// check for change tracking
							await changeTracking.RefreshChangeTrackingAsync();
						}
					}
					catch (SqlException e)
					{
						if (e.Number != lastExceptionNumber) // todo: detect disconnect
						{
							lastExceptionNumber = e.Number;
							Log.Except(e);
						}
					}
					catch (Exception e)
					{
						Log.Except(e);
					}
				}
				finally
				{
					// delay at least 5 Sekunde
					await Task.Delay(Math.Max(5000 - Math.Abs(Environment.TickCount - executeStartTick), 0));
				}
			}
		} // proc ExecuteDatabaseAsync

		#endregion

		#region -- Master Connection Service ------------------------------------------

		/// <summary></summary>
		/// <param name="connection"></param>
		/// <returns></returns>
		protected override IDisposable UseMasterConnection(out DbConnection connection)
		{
			connection = masterConnection;
			return null;
		} // func UseMasterConnection

		#endregion

		#region -- View Management ----------------------------------------------------

		#region -- class MsSqlDataFilterVisitor ---------------------------------------

		private sealed class MsSqlDataFilterVisitor : SqlDataFilterVisitor
		{
			public MsSqlDataFilterVisitor(Func<string, string> lookupNative, SqlColumnFinder columnLookup)
				: base(lookupNative, columnLookup)
			{
			} // ctor

			protected override string CreateDateString(DateTime value)
				=> "convert(datetime, '" + value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss") + "', 126)";
		} // class MsSqlDataFilterVisitor

		#endregion

		/// <summary></summary>
		/// <param name="whereCondition"></param>
		/// <param name="lookupNative"></param>
		/// <param name="columnLookup"></param>
		/// <returns></returns>
		protected override string FormatWhereExpression(PpsDataFilterExpression whereCondition, Func<string, string> lookupNative, SqlColumnFinder columnLookup)
			=> new MsSqlDataFilterVisitor(lookupNative, columnLookup).CreateFilter(whereCondition);

		/// <summary></summary>
		/// <param name="connection"></param>
		/// <param name="name"></param>
		/// <param name="timeStamp"></param>
		/// <param name="selectStatement"></param>
		/// <returns></returns>
		protected override async Task<string> CreateOrReplaceViewAsync(DbConnection connection, string name, DateTime? timeStamp, string selectStatement)
		{
			// execute the new view
			using (var cmd = connection.CreateCommand())
			{
				cmd.CommandTimeout = 6000;
				cmd.CommandType = CommandType.Text;

				// drop
				cmd.CommandText = $"IF object_id('{name}', 'V') IS NOT NULL DROP VIEW {name}";
				await cmd.ExecuteNonQueryAsync();

				// create
				cmd.CommandText = $"CREATE VIEW {name} AS {selectStatement}";
				await cmd.ExecuteNonQueryAsync();
			} // using cmd

			return name;
		} // func CreateOrReplaceViewAsync

		/// <inherited/>
		protected override Task CreateSelectRightsAsync(DbConnection connection, string name, IEnumerable<string> grantSelectTo)
		{
			if (grantSelectTo == null)
				grantSelectTo = new string[] { "public" };

			return base.CreateSelectRightsAsync(connection, name, grantSelectTo);
		} // proc CreateSelectRightsAsync

		/// <inherited/>
		protected override DbCommand CreateViewCommand(IPpsSqlConnectionHandle connection, IEnumerable<IDataColumn> selectList, PpsSqlJoinExpression from, PpsDataFilterExpression whereCondition, Func<string, string> whereNativeLookup, IEnumerable<PpsDataOrderExpression> orderBy, Func<string, string> orderByNativeLookup, int start, int count)
		{
			SqlCommand cmd = null;
			try
			{
				var trans = Application.Database.GetActiveTransaction(connection.DataSource);
				if (trans is PpsMsSqlDataTransaction sqlTrans)
				{
					cmd = sqlTrans.CreateCommand(CommandType.Text, false);
				}
				else
				{
					cmd = new SqlCommand
					{
						Connection = GetSqlConnection(connection),
						CommandType = CommandType.Text,
					};
				}

				var sb = new StringBuilder("SELECT ");
				var columnHelper = new SqlColumnFinder(
					selectList.OfType<IPpsSqlAliasColumn>().ToArray(),
					from
				);

				// build the select
				FormatSelectList(sb, columnHelper);

				// add the view
				sb.Append("FROM ").Append(from.EmitJoin()).Append(' ');

				// add the where
				if (whereCondition != null && whereCondition != PpsDataFilterExpression.True)
					sb.Append("WHERE ").Append(FormatWhereExpression(whereCondition, whereNativeLookup, columnHelper)).Append(' ');

				// add the orderBy
				var orderByEmitted = !FormatOrderList(sb, orderBy, orderByNativeLookup, columnHelper);

				// build the range, without order fetch is not possible
				if (count >= 0 && start < 0)
					start = 0;
				if (start >= 0 && count < Int32.MaxValue)
				{
					if (orderByEmitted)
						sb.Append(' ');
					else
						sb.Append("ORDER BY 1 ");
					sb.Append("OFFSET ").Append(start).Append(" ROWS ");
					if (count >= 0)
						sb.Append("FETCH NEXT ").Append(count).Append(" ROWS ONLY ");
				}

				cmd.CommandText = sb.ToString();
				return cmd;
			}
			catch
			{
				cmd?.Dispose();
				throw;
			}
		} // func CreateViewCommand

		#endregion

		/// <inherited />
		public override PpsDataTransaction CreateTransaction(IPpsConnectionHandle connection)
			=> new PpsMsSqlDataTransaction(this, connection);

		/// <inherited />
		public override PpsDataSynchronization CreateSynchronizationSession(IPpsConnectionHandle connection, long lastSyncronizationStamp, bool leaveConnectionOpen)
			=> new PpsMsSqlDataSynchronization(Application, connection, lastSyncronizationStamp, leaveConnectionOpen);

		/// <summary>Get connection mode for the user</summary>
		/// <param name="authentificatedUser"></param>
		/// <returns></returns>
		protected virtual object TryGetConnectionMode(IDEAuthentificatedUser authentificatedUser)
		{
			foreach(var cur in ConfigNode.Elements(xnAccess))
			{
				if (authentificatedUser.IsInRole(cur.GetAttribute<string>("security")))
				{
					var user = cur.GetAttribute<string>("user");
					if (user == ".sys")
						return true;
					else if (user == ".self")
						return false;
					else
						return UserCredential.Create(user, cur.GetAttribute<SecureString>("password")); 
				}
			}
			return null;
		} // func TryGetConnectionMode

		/// <summary></summary>
		/// <param name="connectionString"></param>
		/// <param name="applicationName"></param>
		/// <returns></returns>
		protected override DbConnectionStringBuilder CreateConnectionStringBuilderCore(string connectionString, string applicationName)
		{
			return new SqlConnectionStringBuilder(connectionString)
			{
				// remove password, and connection information
				Password = String.Empty,
				UserID = String.Empty,
				IntegratedSecurity = false,

				ApplicationName = applicationName, // add a name
				MultipleActiveResultSets = true // activate MARS
			};
		} // func CreateConnectionStringBuilderCore

		/// <summary>Is the master connection of the data source connected.</summary>
		public bool IsConnected
		{
			get
			{
				lock (masterConnection)
					return IsConnectionOpen(masterConnection);
			}
		} // prop IsConnected

		/// <summary>Returns mssql</summary>
		public override string Type => "mssql";

		// -- Static --------------------------------------------------------------

		/// <summary></summary>
		/// <param name="connection"></param>
		/// <returns></returns>
		protected static bool IsConnectionOpen(SqlConnection connection)
			=> connection.State != ConnectionState.Closed;

		/// <summary></summary>
		/// <param name="connection"></param>
		/// <returns></returns>
		protected static SqlConnection GetSqlConnection(IPpsConnectionHandle connection)
			=> connection is SqlConnectionHandle c
				? c.Connection
				: null;
	} // class PpsMsSqlDataSource
}
