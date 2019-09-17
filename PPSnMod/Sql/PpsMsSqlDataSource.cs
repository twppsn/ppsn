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
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using Neo.IronLua;
using TecWare.DE.Data;
using TecWare.DE.Server;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;
using TecWare.PPSn.Server.Data;

namespace TecWare.PPSn.Server.Sql
{
	/// <summary>Access sql-server.</summary>
	public class PpsMsSqlDataSource : PpsSqlDataSource
	{
		#region -- class SqlConnectionHandle ------------------------------------------

		private sealed class SqlConnectionHandle : PpsSqlConnectionHandle<SqlConnection, SqlConnectionStringBuilder>
		{
			public SqlConnectionHandle(PpsMsSqlDataSource dataSource, PpsCredentials credentials)
				: base(dataSource, credentials)
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

			protected override async Task ConnectCoreAsync(SqlConnection connection, SqlConnectionStringBuilder connectionString)
			{
				if (Credentials is PpsIntegratedCredentials ic)
				{
					connectionString.IntegratedSecurity = true;
					connection.ConnectionString = connectionString.ToString();

					using (ic.Impersonate()) // is only functional in the admin context
						await connection.OpenAsync();
				}
				else if (Credentials is PpsUserCredentials uc) // use network credentials
				{
					connectionString.IntegratedSecurity = false;
					connection.ConnectionString = connectionString.ToString();

					connection.Credential = new SqlCredential(uc.UserName, uc.Password);
					await connection.OpenAsync();
				}
				else
					throw new ArgumentOutOfRangeException(nameof(Credentials));
			} // func ConnectCoreAsync

			/// <summary>Is connection alive.</summary>
			public override bool IsConnected => IsConnectionOpen(Connection);
		} // class PpsMsSqlConnectionHandle

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
				Credentials = ((SqlConnectionHandle)connectionHandle).Credentials;
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
					foreach(var p in tableInfo.PrimaryKeys)
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

			/// <summary></summary>
			public PpsCredentials Credentials { get; }
			/// <summary></summary>
			public PpsMsSqlDataSource SqlDataSource => (PpsMsSqlDataSource)DataSource;
			/// <summary></summary>
			public SqlConnection SqlConnection => (SqlConnection)DbConnection;
			/// <summary></summary>
			public SqlTransaction SqlTransaction => (SqlTransaction)DbTransaction;
		} // class PpsMsSqlDataTransaction

		#endregion

		#region -- class SqlColumnInfo ------------------------------------------------

		private sealed class SqlColumnInfo : PpsSqlColumnInfo
		{
			private readonly int columnId;
			private readonly SqlDbType sqlType;
			private readonly string udtName;

			public SqlColumnInfo(PpsSqlTableInfo table, SqlDataReader r)
				: base(table,
					  columnName: r.GetString(2),
					  dataType: GetFieldType(r.GetByte(3)),
					  maxLength: r.GetInt16(4),
					  precision: r.GetByte(5),
					  scale: r.GetByte(6),
					  isNullable: r.GetBoolean(7),
					  isIdentity: r.GetBoolean(8),
					  isPrimaryKey: r.GetBoolean(9)
				)
			{
				this.columnId = r.GetInt32(1);
				var t = r.GetByte(3);
				this.sqlType = GetSqlType(t);
				if (t == 240)
					udtName = "geography";
				else
					udtName = null;
			} // ctor

			protected override IEnumerator<PropertyValue> GetProperties()
			{
				using (var e = base.GetProperties())
				{
					while (e.MoveNext())
						yield return e.Current;
				}
				yield return new PropertyValue(nameof(SqlType), SqlType);
			} // func GetProperties

			protected override bool TryGetProperty(string propertyName, out object value)
			{
				if (!base.TryGetProperty(propertyName, out value))
				{
					if (String.Compare(propertyName, nameof(SqlType), StringComparison.OrdinalIgnoreCase) == 0)
					{
						value = SqlType;
						return true;
					}
				}

				value = null;
				return false;
			} // func TryGetProperty

			public override void InitSqlParameter(DbParameter parameter, string parameterName, object value)
			{
				if (String.IsNullOrEmpty(parameterName))
					parameterName = "@" + Name;

				base.InitSqlParameter(parameter, parameterName, value);

				if (parameter.Size == 0 && (sqlType == SqlDbType.NVarChar || sqlType == SqlDbType.VarChar || sqlType == SqlDbType.VarBinary))
					parameter.Size = -1;

				((SqlParameter)parameter).SqlDbType = sqlType;
				if (sqlType == SqlDbType.Udt)
					((SqlParameter)parameter).UdtTypeName = udtName;
			} // proc InitSqlParameter

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

					case 240: // GEOGRAPHY
						return typeof(Microsoft.SqlServer.Types.SqlGeography);

					case 241: // xml
						return typeof(string);

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

					case 240: // GEOGRAPHY
						return SqlDbType.Udt;

					case 241: // xml
						return SqlDbType.Xml;

					default:
						throw new IndexOutOfRangeException($"Unexpected sql server system type: {systemTypeId}");
				}
			} // func GetSqlType

			#endregion

			public int ColumnId => columnId;
			public SqlDbType SqlType => sqlType;
		} // class SqlColumnInfo

		#endregion

		#region -- class SqlTableInfo -------------------------------------------------

		private sealed class SqlTableInfo : PpsSqlTableInfo
		{
			public SqlTableInfo(SqlDataReader r)
				: base(r.GetString(1), r.GetString(2))
			{
			}
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

		private sealed class SqlParameterInfo : PpsSqlParameterInfo
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
				p.SqlDbType = dbType;
				p.Direction = Direction;
				switch (dbType)
				{
					case SqlDbType.NVarChar:
					case SqlDbType.VarBinary:
					case SqlDbType.VarChar:
						p.Size = maxLength;
						break;
					case SqlDbType.Decimal:
						p.Precision = precision;
						p.Scale = scale;
						break;
					case SqlDbType.Udt:
						p.UdtTypeName = typeName;
						break;
					case SqlDbType.Structured:
						p.TypeName = typeName;
						break;
					case SqlDbType.Xml:
						p.XmlSchemaCollectionDatabase = xmlSchemaCollectionDatabase;
						p.XmlSchemaCollectionName = xmlSchemaCollectionName;
						p.XmlSchemaCollectionOwningSchema = xmlSchemaCollectionOwningSchema;
						break;
					case SqlDbType.Time:
					case SqlDbType.DateTime2:
						p.SqlDbType = SqlDbType.DateTimeOffset;
						p.Scale = scale;
						break;
				}
			} // proc InitSqlParameter
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
					if (parameterInfo.Direction != ParameterDirection.ReturnValue)
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

			public override bool HasResult => hasResult;
			public override bool HasOutput => hasOutput;
			public override bool HasReturnValue => hasReturnValue;
		} // class SqlProcedureInfo

		#endregion

		private readonly SqlConnection masterConnection;
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
					masterConnection.Dispose();
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
		protected override void InitializeSchemaCore()
		{
			using (UseMasterConnection(out var connection))
			using (var cmd = ((SqlConnection)connection).CreateCommand())
			{
				cmd.CommandType = CommandType.Text;
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
							AddTable(tab);
							tableIndex[objectId] = tab;
						}
						catch (Exception e)
						{
							Log.Except($"Table initialization failed: objectId={objectId}", e);
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
								AddColumn(new SqlColumnInfo(table, r));
						}
						catch (Exception e)
						{
							Log.Except($"Column initialization failed: {r.GetValue(2)}", e);
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

							AddRelation(new SqlRelationInfo(r.GetString(1), parentColumn, referencedColumn));
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
							AddProcedure(tab);
							procedureIndex[objectId] = tab;
						}
						catch (Exception e)
						{
							Log.Except($"Procedure initialization failed: objectId={objectId}", e);
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

		#endregion

		#region -- Execute Database ---------------------------------------------------

		private async Task ExecuteDatabaseAsync(DEThread thread)
		{
			var lastChangeTrackingId = -1L;
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
							using (var cmd = masterConnection.CreateCommand())
							{
								cmd.CommandText = "SELECT change_tracking_current_version()";
								var r = await cmd.ExecuteScalarAsync();
								if (r is long l)
								{
									if (lastChangeTrackingId == -1L)
										lastChangeTrackingId = l;
									else if (lastChangeTrackingId != l)
									{
										lastChangeTrackingId = l;

										// notify clients, something has changed
										Application.FireDataChangedEvent(Name);
									}
								}
							}
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
					// delay at least 1 Sekunde
					await Task.Delay(Math.Max(1000 - Math.Abs(Environment.TickCount - executeStartTick), 0));
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

				// rights
				cmd.CommandText = $"GRANT SELECT ON {name} TO [public]";
				await cmd.ExecuteNonQueryAsync();
			} // using cmd

			return name;
		} // func CreateOrReplaceViewAsync

		/// <summary></summary>
		/// <param name="connection"></param>
		/// <param name="selectList"></param>
		/// <param name="from"></param>
		/// <param name="whereCondition"></param>
		/// <param name="whereNativeLookup"></param>
		/// <param name="orderBy"></param>
		/// <param name="orderByNativeLookup"></param>
		/// <param name="start"></param>
		/// <param name="count"></param>
		/// <returns></returns>
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
				var orderByEmitted = FormatOrderList(sb, orderBy, orderByNativeLookup, columnHelper);

				// build the range, without order fetch is not possible
				if (count >= 0 && start < 0)
					start = 0;
				if (start >= 0 && count < Int32.MaxValue)
				{
					if (orderByEmitted)
						sb.Append("ORDER BY ");
					else
						sb.Append(' ');
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

		/// <summary></summary>
		/// <param name="userContext"></param>
		/// <param name="throwException"></param>
		/// <returns></returns>
		public override IPpsConnectionHandle CreateConnection(IPpsPrivateDataContext userContext, bool throwException = true)
		{
			PpsCredentials credentials;
			if (userContext.Identity == PpsUserIdentity.System)
			{
				if (sysUserName != null)
					credentials = new PpsUserCredentials(sysUserName, sysPassword);
				else
					credentials = PpsUserIdentity.System.GetCredentialsFromIdentity(PpsUserIdentity.System);
			}
			else
				credentials = userContext.GetNetworkCredential();

			return new SqlConnectionHandle(this, credentials);
		} // func CreateConnection

		/// <summary></summary>
		/// <param name="connection"></param>
		/// <returns></returns>
		public override PpsDataTransaction CreateTransaction(IPpsConnectionHandle connection)
			=> new PpsMsSqlDataTransaction(this, connection);

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
