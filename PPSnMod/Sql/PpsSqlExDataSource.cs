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
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Neo.IronLua;
using TecWare.DE.Data;
using TecWare.DE.Server;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;
using TecWare.PPSn.Server.Data;
using TecWare.PPSn.Stuff;

namespace TecWare.PPSn.Server.Sql
{
	/// <summary></summary>
	public sealed class PpsSqlExDataSource : PpsSqlDataSource
	{
		#region -- class SqlConnectionHandle ------------------------------------------

		private sealed class SqlConnectionHandle : PpsSqlConnectionHandle<SqlConnection, SqlConnectionStringBuilder>
		{
			public SqlConnectionHandle(PpsSqlExDataSource dataSource, PpsCredentials credentials)
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

			public override bool IsConnected => IsConnectionOpen(Connection);
		} // class SqlConnectionHandle

		#endregion
		
		#region -- class SqlResultInfo ------------------------------------------------

		private sealed class SqlResultInfo : List<Func<SqlDataReader, IEnumerable<IDataRow>>>
		{
		} // class SqlResultInfo

		#endregion

		#region -- class SqlJoinExpression ----------------------------------------------

		public sealed class SqlJoinExpression : PpsDataJoinExpression<PpsSqlTableInfo>
		{
			#region -- class SqlEmitVisitor ---------------------------------------------

			private sealed class SqlEmitVisitor : PpsJoinVisitor<string>
			{
				public override string CreateJoinStatement(string leftExpression, PpsDataJoinType type, string rightExpression, string on)
				{
					string GetJoinExpr()
					{
						switch(type)
						{
							case PpsDataJoinType.Inner:
								return " INNER JOIN ";
							case PpsDataJoinType.Left:
								return " LEFT OUTER JOIN ";
							case PpsDataJoinType.Right:
								return " RIGHT OUTER JOIN ";
							default:
								throw new ArgumentException(nameof(type));
						}
					} // func GetJoinExpr

					return "(" + leftExpression + GetJoinExpr() + rightExpression + " ON (" + on + "))";
				} // func CreateJoinStatement

				public override string CreateTableStatement(PpsSqlTableInfo table, string alias)
				{
					if (String.IsNullOrEmpty(alias))
						return table.SqlQualifiedName;
					else
						return table.SqlQualifiedName + " AS " + alias;
				} // func CreateTableStatement
			} // class SqlEmitVisitor

			#endregion

			private readonly PpsSqlExDataSource dataSource;

			public SqlJoinExpression(PpsSqlExDataSource dataSource)
			{
				this.dataSource = dataSource;
			} // ctor

			protected override PpsSqlTableInfo ResolveTable(string tableName)
				=> dataSource.ResolveTableByName<SqlTableInfo>(tableName, true);

			protected override string CreateOnStatement(PpsTableExpression left, PpsDataJoinType joinOp, PpsTableExpression right)
			{
				foreach (var r in right.Table.RelationInfo)
				{
					if (r.ReferencedColumn.Table == left.Table)
					{
						var sb = new StringBuilder();
						AppendColumn(sb, left, r.ReferencedColumn);
						sb.Append(" = ");
						AppendColumn(sb, right, r.ParentColumn);
						return sb.ToString();
					}
				}
				return null;
			} // func CreateOnStatement

			private void SplitColumnName(string name, out string alias, out string columnName)
			{
				var p = name.IndexOf('.'); // alias?
				if (p >= 0)
				{
					alias = name.Substring(0, p);
					columnName = name.Substring(p + 1);
				}
				else
				{
					alias = null;
					columnName = name;
				}
			} // func SplitColumnName

			public (PpsTableExpression, PpsSqlColumnInfo) FindColumn(IPpsColumnDescription ppsColumn, bool throwException)
			{
				foreach (var t in GetTables())
				{
					if (ppsColumn.TryGetColumnDescriptionImplementation<PpsSqlColumnInfo>(out var sqlColumn) && t.Table == sqlColumn.Table)
						return (t, sqlColumn);
				}

				if (throwException)
					throw new ArgumentException($"Column not found ({ppsColumn.Name}).");

				return (null, null);
			} // func FindColumn

			public (PpsTableExpression, PpsSqlColumnInfo) FindColumn(string name, bool throwException)
			{
				SplitColumnName(name, out var alias, out var columnName);
				foreach (var t in GetTables())
				{
					if (alias != null)
					{
						if (t.Alias != null && String.Compare(alias, t.Alias, StringComparison.OrdinalIgnoreCase) == 0)
							return (t, t.Table.FindColumn(columnName, throwException));
					}
					else
					{
						var c = t.Table.FindColumn(columnName, false);
						if (c != null)
							return (t, c);
					}
				}

				if (throwException)
					throw new ArgumentException($"Column not found ({name}).");

				return (null, null);
			} // func FindColumn

			public StringBuilder AppendColumn(StringBuilder commandText, PpsTableExpression table, PpsSqlColumnInfo column)
				=> String.IsNullOrEmpty(table.Alias)
					? column.AppendAsColumn(commandText, true)
					: column.AppendAsColumn(commandText, table.Alias);

			public string EmitJoin()
				=> new SqlEmitVisitor().Visit(this);
		} // class SqlJoinExpression

		#endregion

		#region -- class SqlDataTransaction ---------------------------------------------

		/// <summary></summary>
		private sealed class SqlDataTransaction : PpsDataTransaction
		{
			#region -- class PpsColumnMapping -------------------------------------------

			/// <summary></summary>
			[DebuggerDisplay("{DebuggerDisplay,nq}")]
			private sealed class PpsColumnMapping
			{
				private readonly PpsSqlColumnInfo columnInfo;
				private readonly Func<object, object> getValue;
				private readonly Action<object, object> updateValue;

				private readonly string parameterName;
				private DbParameter parameter = null;

				public PpsColumnMapping(PpsSqlColumnInfo columnInfo, Func<object, object> getValue, Action<object, object> updateValue)
				{
					this.columnInfo = columnInfo ?? throw new ArgumentNullException(nameof(columnInfo));
					this.getValue = getValue ?? throw new ArgumentNullException(nameof(getValue));
					this.updateValue = updateValue;

					this.parameterName = "@" + columnInfo.Name;
				} // ctor

				public object GetValue(object row)
					=> getValue(row);

				public void SetParameter(object row)
					=> parameter.Value = GetValue(row) ?? (object)DBNull.Value;

				public void UpdateValue(object row, object value)
					=> updateValue?.Invoke(row, value);

				public void AppendParameter(DbCommand cmd, object initialValues)
				{
					if (parameter != null)
						throw new InvalidOperationException();
					parameter = columnInfo.AppendSqlParameter(cmd, parameterName, initialValues == null ? null : getValue(initialValues));
				} // func AppendParameter

				private string DebuggerDisplay
					=> $"Mapping: {columnInfo.TableColumnName} -> {parameterName}";

				public PpsSqlColumnInfo ColumnInfo => columnInfo;
				public string ColumnName => columnInfo.Name;
				public string ParameterName => parameterName;
			} // class PpsColumnMapping

			#endregion

			private readonly SqlConnection connection;
			private readonly PpsCredentials credentials;
			private readonly SqlTransaction transaction;

			#region -- Ctor/Dtor --------------------------------------------------------

			public SqlDataTransaction(PpsSqlDataSource dataSource, SqlConnectionHandle connectionHandle)
				: base(dataSource, connectionHandle)
			{
				this.connection = connectionHandle.ForkConnectionAsync().AwaitTask();
				this.credentials = connectionHandle.Credentials;

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
				if (!IsCommited.HasValue)
					transaction.Commit();
				base.Commit();
			} // proc Commit

			public override void Rollback()
			{
				try
				{
					if (!IsCommited.HasValue)
						transaction.Rollback();
				}
				finally
				{
					base.Rollback();
				}
			} // proc Rollback

			#endregion

			#region -- Execute Result ---------------------------------------------------

			internal SqlCommand CreateCommand(CommandType commandType, bool noTransaction)
			{
				var cmd = connection.CreateCommand();
				cmd.Connection = connection;
				cmd.CommandTimeout = 7200;
				cmd.Transaction = noTransaction ? null : transaction;
				return cmd;
			} // func CreateCommand

			private SqlCommand CreateCommand(LuaTable parameter, CommandType commandType)
				=> CreateCommand(commandType, parameter.GetOptionalValue("__notrans", false));

			private SqlDataReader ExecuteReaderCommand(SqlCommand cmd, PpsDataTransactionExecuteBehavior behavior)
			{
				switch (behavior)
				{
					case PpsDataTransactionExecuteBehavior.NoResult:
						cmd.ExecuteNonQuery();
						return null;
					case PpsDataTransactionExecuteBehavior.SingleRow:
						return cmd.ExecuteReader(CommandBehavior.SingleRow);
					case PpsDataTransactionExecuteBehavior.SingleResult:
						return cmd.ExecuteReader(CommandBehavior.SingleResult);
					default:
						return cmd.ExecuteReader(CommandBehavior.Default);
				}
			} // func ExecuteReaderCommand

			private LuaTable GetArguments(object value, bool throwException)
			{
				var args = value as LuaTable;
				if (args == null && throwException)
					throw new ArgumentNullException($"value", "No arguments defined.");
				return args;
			} // func GetArguments

			private LuaTable GetArguments(LuaTable parameter, int index, bool throwException)
			{
				var args = GetArguments(parameter[index], false);
				if (args == null && throwException)
					throw new ArgumentNullException($"parameter[{index}]", "No arguments defined.");
				return args;
			} // func GetArguments

			#region -- ExecuteCall ----------------------------------------------------------

			private IEnumerable<IEnumerable<IDataRow>> ExecuteCall(LuaTable parameter, string name, PpsDataTransactionExecuteBehavior behavior)
			{
				using (var cmd = CreateCommand(parameter, CommandType.StoredProcedure))
				{
					cmd.CommandText = name;

					// build argument list
					SqlCommandBuilder.DeriveParameters(cmd);

					// build parameter mapping
					var parameterMapping = new Tuple<string, SqlParameter>[cmd.Parameters.Count];
					var j = 0;
					foreach (SqlParameter p in cmd.Parameters)
					{
						var parameterName = p.ParameterName;
						if ((p.Direction & ParameterDirection.ReturnValue) == ParameterDirection.ReturnValue)
							parameterName = null;
						else if (parameterName.StartsWith("@"))
							parameterName = parameterName.Substring(1);

						parameterMapping[j++] = new Tuple<string, SqlParameter>(parameterName, p);
					}

					// copy arguments
					for (var i = 1; i <= parameter.ArrayList.Count; i++)
					{
						var args = GetArguments(parameter, i, false);
						if (args == null)
							yield break;

						// fill arguments
						foreach (var p in parameterMapping)
						{
							var value = args?.GetMemberValue(p.Item1);
							p.Item2.Value = value ?? DBNull.Value;
						}

						using (var r = ExecuteReaderCommand(cmd, behavior))
						{
							// copy arguments back
							foreach (var p in parameterMapping)
							{
								if (p.Item1 == null)
									args[1] = p.Item2.Value.NullIfDBNull();
								else if ((p.Item2.Direction & ParameterDirection.Output) == ParameterDirection.Output)
									args[p.Item1] = p.Item2.Value.NullIfDBNull();
							}

							// return results
							if (r != null)
							{
								do
								{
									yield return new DbRowReaderEnumerable(r);
									if (behavior == PpsDataTransactionExecuteBehavior.SingleResult)
										break;
								} while (r.NextResult());
							}
						} // using r
					} // for (args)
				} // using cmd
			} // func ExecuteInsertResult

			#endregion

			#region -- ExecuteUpdateRevisionData --------------------------------------

			private static int ReadStreamData(Stream src, byte[] buf)
			{
				var ofs = 0;
				while (ofs < buf.Length)
				{
					var r = src.Read(buf, ofs, buf.Length - ofs);
					if (r <= 0)
						break;
					ofs += r;
				}
				return ofs;
			} // func ReadStreamData

			private static (string hashName, byte[] hashValue) CopyData(bool shouldDeflate, Stream srcStream, byte[] buf, int readed, Stream dstStream)
			{
				// pack destination stream
				var dst = shouldDeflate
					? new GZipStream(dstStream, CompressionMode.Compress, true)
					: dstStream;

				using (var dstHashStream = new HashStream(dst, HashStreamDirection.Write, false, SHA256.Create()))
				{
					try
					{
						// copy stream into file
						dstHashStream.Write(buf, 0, readed);
						srcStream.CopyTo(dst);
					}
					finally
					{
						dstHashStream.Flush();
						dstHashStream.Dispose();
					}

					dstHashStream.Close();

					return ("SHA2_256", dstHashStream.HashSum);
				}
			} // func CopyData

			private IEnumerable<IEnumerable<IDataRow>> ExecuteUpdateRevisionData(LuaTable parameter, PpsDataTransactionExecuteBehavior behavior)
			{
				// function to update revision
				// UpdateRevision(long ObjkId, long RevId, long ParentId, long CreateUserId, [opt] date CreateDate, bool Deflate, bool IsDocumentText)

				var args = GetArguments(parameter, 1, true);

				// read arguments
				var objkId = args.GetOptionalValue("ObjkId", -1L);
				if (objkId < 0)
					throw new ArgumentOutOfRangeException("ObjkId", objkId, "ObjkId is mising.");
				var revId = args.GetOptionalValue("RevId", -1L);
				var parentRevId = args.GetOptionalValue("ParentRevId", -1L);
				var createUserId = args.GetOptionalValue("CreateUserId", 0L);
				var createDate = args.GetOptionalValue("CreateDate", DateTime.Now);
				var isDocumentText = args.GetOptionalValue("IsDocumentText", false);
				var shouldDeflate = args.GetOptionalValue("Deflate", false);

				var srcStream = (Stream)args.GetMemberValue("Content"); // get source stream

				long documentId = -1;

				// read first block
				var buf = new byte[0x80000];
				var readed = ReadStreamData(srcStream, buf);
				var integratedUser = credentials as PpsIntegratedCredentials;
				var useFileStream = readed >= buf.Length;

				if (useFileStream) // inline data in revision
				{
					if (integratedUser != null)
					{
						#region -- insert into objf --
						using (integratedUser.Impersonate())
						using (var cmd = CreateCommand(parameter, CommandType.Text))
						{
							cmd.CommandText = "INSERT INTO dbo.[ObjF] ([HashAlgo], [Hash], [Data]) "
								+ "OUTPUT inserted.Id, inserted.Data.PathName(), GET_FILESTREAM_TRANSACTION_CONTEXT() "
								+ "VALUES ('SHA2_256', 0x, 0x);";

							using (var r = ExecuteReaderCommand(cmd, PpsDataTransactionExecuteBehavior.SingleRow))
							{
								if (r.Read())
								{
									// get destination file
									documentId = r.GetInt64(0);
									var path = r.GetString(1);
									var context = r.GetSqlBytes(2).Buffer;
									//throw new Exception();

									using (var dstFileStream = new SqlFileStream(path, context, FileAccess.Write))
									{
										var (hashName, hashValue) = CopyData(shouldDeflate, srcStream, buf, readed, dstFileStream);
										args["HashValue"] = hashValue;
										args["HashAlgo"] = hashName;
									}
								}
								else
									throw new Exception("Insert FileStream failed.");
							}
						}
						#endregion
						#region -- update objf --
						using (var cmd = CreateCommand(parameter, CommandType.Text))
						{
							cmd.CommandText = "UPDATE dbo.[ObjF] SET [HashAlgo] = @HashAlgo, [Hash] = @HashValue WHERE [Id] = @DocumentId;";
							cmd.Parameters.Add("@HashValue", SqlDbType.VarBinary).Value = args["HashValue"];
							cmd.Parameters.Add("@HashAlgo", SqlDbType.VarChar).Value = args["HashAlgo"];
							cmd.Parameters.Add("@DocumentId", SqlDbType.BigInt).Value = documentId;

							ExecuteReaderCommand(cmd, PpsDataTransactionExecuteBehavior.NoResult);
						}
						#endregion
					}
					else
					{
						#region -- insert into obf --
						using (var cmd = CreateCommand(parameter, CommandType.Text))
						{
							cmd.CommandText = "INSERT INTO dbo.[ObjF] ([HashAlgo], [Hash], [Data]) "
								+ "OUTPUT inserted.Id "
								+ "VALUES (@HashAlgo, @HashValue, @Data);";

							using (var dstMem = new MemoryStream())
							{
								var (hashName, hashValue) = CopyData(shouldDeflate, srcStream, buf, readed, dstMem);

								cmd.Parameters.Add("@HashValue", SqlDbType.VarBinary).Value = hashValue;
								cmd.Parameters.Add("@HashAlgo", SqlDbType.VarChar).Value = hashName;
								cmd.Parameters.Add("@Data", SqlDbType.VarBinary).Value = dstMem.ToArray();
							}
							
							using (var r = ExecuteReaderCommand(cmd, PpsDataTransactionExecuteBehavior.SingleResult))
							{
								if (r.Read())
									documentId = r.GetInt64(0);
								else
									throw new Exception("Insert FileStream failed.");
							}
						}
						#endregion
					}
				}
				else
				{
					#region -- build buf --
					if (shouldDeflate) // deflate buffer
					{
						using (var dstMem = new MemoryStream())
						using (var dst = new GZipStream(dstMem, CompressionMode.Compress))
						{
							dst.Write(buf, 0, readed);
							dst.Dispose();
							dstMem.Flush();
							buf = dstMem.ToArray();
						}
					}
					else // create simple byte-array
					{
						var newBuf = new byte[readed];
						Array.Copy(buf, 0, newBuf, 0, readed);
						buf = newBuf;
					}
					#endregion
				}

				// write table or update revision
				using (var cmd = CreateCommand(parameter, CommandType.Text))
				{
					if (revId < 0) // always insert
					{
						cmd.CommandText = "INSERT INTO dbo.[ObjR] ([ObjkId], [ParentId], [IsDocumentText], [IsDocumentDeflate], [Document], [DocumentId], [DocumentLink], [CreateDate], [CreateUserId]) "
							+ "OUTPUT inserted.[Id] "
							+ " VALUES (@ObjkId, @ParentId, @IsDocumentText, @IsDocumentDeflate, @Document, @DocumentId, NULL, @CreateDate, @CreateUserId);";
					}
					else // merge this rev
					{
						cmd.CommandText = "MERGE INTO dbo.[ObjR] as dst"
							+ "USING (SELECT @ObjkId, @RevId, @IsDocumentText, @IsDocumentDeflate, @Document, @DocumentId, @CreateDate, @CreateUserId) as src (NewObjkId, NewRevId, NewIsDocumentText, NewIsDocumentDeflate, NewDocument, NewDocumentId, NewCreateDate, NewCreateUserId) "
							+ "ON (dst.ObjkId = src.NewObjkId AND dst.Id = src.NewRevId) "
							+ "WHEN MATCHED THEN"
							+ "UPDATE SET [IsDocumentText] = src.NewIsDocumentText, [IsDocumentDeflate] = src.NewIsDocumentDeflate, [Document] = src.NewDocument, [CreateDate] = src.NewCreateDate, [CreateUserId] = src.NewCreateUserId"
							+ "WHEN NOT MATCHED THEN "
							+ "INSERT ([ObjkId], [IsDocumentText], [IsDocumentDeflate], [Document], [DocumentId], [DocumentLink], [CreateDate], [CreateUserId]) "
							+ "  VALUES (src.NewObjkId, src.NewIsDocumentText, src.NewIsDocumentDeflate, src.NewDocument, src.NewDocumentId, NULL, src.NewCreateDate, src.NewCreateUserId)"
							+ "OUTPUT inserted.Id";

						cmd.Parameters.Add("@RevId", SqlDbType.BigInt).Value = revId;
					}

					cmd.Parameters.Add("@ObjkId", SqlDbType.BigInt).Value = objkId;
					cmd.Parameters.Add("@ParentId", SqlDbType.BigInt).Value = parentRevId <= 0 ? (object)DBNull.Value : parentRevId;
					cmd.Parameters.Add("@IsDocumentText", SqlDbType.Bit).Value = isDocumentText;
					cmd.Parameters.Add("@IsDocumentDeflate", SqlDbType.Bit).Value = shouldDeflate;
					cmd.Parameters.Add("@Document", SqlDbType.VarBinary).Value = useFileStream ? (object)DBNull.Value : buf;
					cmd.Parameters.Add("@DocumentId", SqlDbType.BigInt).Value = useFileStream ? documentId : (object)DBNull.Value;
					cmd.Parameters.Add("@CreateDate", SqlDbType.DateTime2).Value = createDate;
					cmd.Parameters.Add("@CreateUserId", SqlDbType.BigInt).Value = createUserId;

					using (var r = ExecuteReaderCommand(cmd, PpsDataTransactionExecuteBehavior.SingleRow))
					{
						if (r.Read())
						{
							args["RevId"] = r.GetInt64(0);
						}
						else
							throw new InvalidOperationException("Merge/Insert failed.");
					}
				}

				yield break;
			} // proc ExecuteUpdateRevisionData

			#endregion

			#region -- ExecuteGetRevisionData -----------------------------------------

			private IEnumerable<IEnumerable<IDataRow>> ExecuteGetRevisionData(LuaTable parameter, PpsDataTransactionExecuteBehavior behavior)
			{
				// function to get revision data
				// GetRevisionData(long ObjkId oder long RevId)

				var args = GetArguments(parameter, 1, true);
				var revId = args.GetOptionalValue("RevId", -1L);
				var objkId = args.GetOptionalValue("ObjkId", -1L);

				if (revId <= 0
					&& objkId <= 0)
					throw new ArgumentException("Invalid arguments.", "revId|objkId");

				using (var cmd = CreateCommand(parameter, CommandType.Text))
				{
					var useFileStream = credentials is PpsIntegratedCredentials; // only integrated credentials can use filestream

					cmd.CommandText = "SELECT [IsDocumentText], [IsDocumentDeflate], [Document], [DocumentId], [DocumentLink], [HashAlgo], [Hash], " + (useFileStream ? "[Data].PathName(), GET_FILESTREAM_TRANSACTION_CONTEXT() " : "[Data] ")
						+ (revId > 0
							? "FROM dbo.[ObjR] r LEFT OUTER JOIN dbo.[ObjF] f ON (r.[DocumentId] = f.[Id]) WHERE r.[Id] = @Id;"
							: "FROM dbo.[ObjK] o INNER JOIN dbo.[ObjR] r ON (o.HeadRevId = r.[Id]) LEFT OUTER JOIN dbo.[ObjF] f ON (r.[DocumentId] = f.[Id]) WHERE o.[Id] = @Id;"
						);

					cmd.Parameters.Add("@Id", SqlDbType.BigInt).Value = revId > 0 ? revId : objkId;

					using (var r = ExecuteReaderCommand(cmd, PpsDataTransactionExecuteBehavior.SingleRow))
					{
						if (!r.Read())
							yield break;

						var hashAlgo = r.IsDBNull(5) ? null : r.GetString(5);
						var hashValue = r.IsDBNull(6) ? null : r.GetSqlBytes(6).Buffer;

						// convert stream
						var isDocumentDeflated = r.GetBoolean(1);
						Stream src;
						if (!r.IsDBNull(7)) // file stream or bytes
						{
							if (useFileStream)
							{
								using (((PpsIntegratedCredentials)credentials).Impersonate())
									src = new SqlFileStream(r.GetString(7), r.GetSqlBytes(8).Buffer, FileAccess.Read);
							}
							else
								src = r.GetSqlBytes(7).Stream;
						}
						else if (!r.IsDBNull(2)) // inline content
						{
							src = r.GetSqlBytes(2).Stream;
						}
						else if (!r.IsDBNull(3)) // linked content
						{
							src = new FileStream(r.GetString(3), FileMode.Open, FileAccess.Read);
						}
						else // no content
						{
							src = null;
						}

						if (src != null && isDocumentDeflated)
							src = new GZipStream(src, CompressionMode.Decompress, false);

						// return result
						yield return new IDataRow[]
						{
							new SimpleDataRow(
								new object[]
								{
									r.GetBoolean(0),
									src,
									hashAlgo,
									hashValue
								},
								new SimpleDataColumn[]
								{
									new SimpleDataColumn("IsDocumentText", typeof(bool)),
									new SimpleDataColumn("Document", typeof(Stream)),
									new SimpleDataColumn("HashAlgo", typeof(string)),
									new SimpleDataColumn("Hash", typeof(byte[]))
								}
							)
						};
					}
				}
			} // proc ExecuteGetRevisionData

			#endregion

			#region -- ExecuteInsert --------------------------------------------------------

			private IEnumerable<IEnumerable<IDataRow>> ExecuteInsert(LuaTable parameter, string name, PpsDataTransactionExecuteBehavior behavior)
			{
				/*
				 * insert into {name} ({columnList})
				 * output inserted.{column}, inserted.{column}
				 * values ({variableList}
				 */

				// find the connected table
				var tableInfo = SqlDataSource.ResolveTableByName<SqlTableInfo>(name, true);

				using (var cmd = CreateCommand(parameter, CommandType.Text))
				{
					var commandText = new StringBuilder();
					var variableList = new StringBuilder();
					var insertedList = new StringBuilder();

					commandText.Append("INSERT INTO ")
						.Append(tableInfo.SqlQualifiedName);

					// default is that only one row is done
					var args = GetArguments(parameter, 1, true);

					commandText.Append(" (");

					// output always primary key
					var primaryKey = tableInfo.PrimaryKey;
					if (primaryKey != null)
						insertedList.Append("inserted.").Append(primaryKey.Name);

					// create the column list
					var first = true;
					foreach (var column in tableInfo.Columns)
					{
						var columnName = column.Name;

						var value = args.GetMemberValue(columnName, true);
						if (value != null)
						{
							if (first)
								first = false;
							else
							{
								commandText.Append(',');
								variableList.Append(',');
							}

							var parameterName = '@' + columnName;
							commandText.Append('[' + columnName + ']');
							variableList.Append(parameterName);
							column.AppendSqlParameter(cmd, parameterName, value);
						}

						if (primaryKey != column)
						{
							if (insertedList.Length > 0)
								insertedList.Append(',');
							insertedList.Append("inserted.").Append('[' + columnName + ']');
						}
					}

					commandText.Append(") ");

					// generate output clause
					commandText.Append("OUTPUT ").Append(insertedList);

					// values
					commandText.Append(" VALUES (")
						.Append(variableList)
						.Append(");");

					cmd.CommandText = commandText.ToString();

					// execute insert
					using (var r = cmd.ExecuteReader(CommandBehavior.SingleRow))
					{
						if (!r.Read())
							throw new InvalidDataException("Invalid return data from sql command.");

						for (var i = 0; i < r.FieldCount; i++)
							args[r.GetName(i)] = r.GetValue(i).NullIfDBNull();
					}
				}
				yield break; // empty enumeration
			} // func ExecuteInsert

			#endregion

			#region -- ExecuteUpdate --------------------------------------------------------

			private IEnumerable<IEnumerable<IDataRow>> ExecuteUpdate(LuaTable parameter, string name, PpsDataTransactionExecuteBehavior behavior)
			{
				/*
				 * update {name} set {column} = {arg},
				 * output inserted.{column}, inserted.{column}
				 * where {PrimaryKey} = @arg
				 */

				// find the connected table
				var tableInfo = SqlDataSource.ResolveTableByName<SqlTableInfo>(name, true);

				using (var cmd = CreateCommand(parameter, CommandType.Text))
				{
					var commandText = new StringBuilder();
					var insertedList = new StringBuilder();

					commandText.Append("UPDATE ")
						.Append(tableInfo.SqlQualifiedName);

					// default is that only one row is done
					var args = GetArguments(parameter, 1, true);

					commandText.Append(" SET ");

					// create the column list
					var first = true;
					foreach (var column in tableInfo.Columns)
					{
						var columnName = column.Name;
						var value = args.GetMemberValue(columnName, true);
						if (value == null || column == tableInfo.PrimaryKey)
							continue;

						if (first)
							first = false;
						else
						{
							commandText.Append(',');
							insertedList.Append(',');
						}

						var parameterName = '@' + columnName;
						commandText.Append(columnName)
							.Append(" = ")
							.Append(parameterName);

						column.AppendSqlParameter(cmd, parameterName, value);

						insertedList.Append("inserted.").Append("[").Append(columnName).Append("]");
					}
					
					if (insertedList.Length == 0)
						throw new ArgumentException("No Columns to update.");

					// generate output clause
					commandText.Append(" output ").Append(insertedList);

					// where
					var primaryKeyName = tableInfo.PrimaryKey.Name;
					var primaryKeyValue = args[primaryKeyName];
					if (primaryKeyValue == null)
						throw new ArgumentException("Invalid primary key.");

					commandText.Append(" WHERE ")
						.Append(primaryKeyName)
						.Append(" = ")
						.Append("@").Append(primaryKeyName);
					tableInfo.PrimaryKey.AppendSqlParameter(cmd, "@" + primaryKeyName, primaryKeyValue);

					cmd.CommandText = commandText.ToString();

					// execute insert
					using (var r = cmd.ExecuteReader(CommandBehavior.SingleRow))
					{
						if (!r.Read())
							throw new InvalidDataException("Invalid return data from sql command.");

						for (var i = 0; i < r.FieldCount; i++)
							args[r.GetName(i)] = r.GetValue(i).NullIfDBNull();
					}
				}
				yield break; // empty enumeration
			} // func ExecuteUpdate

			#endregion

			#region -- ExecuteUpsert --------------------------------------------------------
			
			private (IEnumerator<object> rows, PpsColumnMapping[] mapping) PrepareColumnMapping(PpsSqlTableInfo tableInfo, LuaTable parameter)
			{
				var columnMapping = new List<PpsColumnMapping>();

				void CheckColumnMapping()
				{
					if (columnMapping.Count == 0)
						throw new ArgumentException("Column Array is empty.");
				} // proc CheckColumnMapping

				IEnumerator<object> GetTableRowEnum()
				{
					var rowEnumerator = parameter.ArrayList.GetEnumerator();
					if (!rowEnumerator.MoveNext())
					{
						rowEnumerator.Dispose();
						throw new ArgumentException("Empty result.");
					}
						return rowEnumerator;
				} // func GetTableRowEnum

				void CreateColumnMapping(IReadOnlyCollection<IDataColumn> columns)
				{
					foreach (var c in columns)
					{
						if (c is IPpsColumnDescription t)
						{
							var dataColumn = (PpsDataColumnDefinition)t;
							var idx = dataColumn.Index;
							var nativeColumn = t.GetColumnDescription<PpsSqlColumnInfo>();
							if (nativeColumn != null && nativeColumn.Table == tableInfo)
							{
								if (dataColumn.IsExtended)
								{
									if (typeof(IPpsDataRowGetGenericValue).IsAssignableFrom(dataColumn.DataType))
									{
										var getterFunc = new Func<object, object>(row => ((IPpsDataRowGetGenericValue)((PpsDataRow)row)[idx]).Value);
										var setterFunc = typeof(IPpsDataRowSetGenericValue).IsAssignableFrom(dataColumn.DataType)
											? new Action<object, object>((row, value) => ((IPpsDataRowSetGenericValue)((PpsDataRow)row)[idx]).SetGenericValue(false, value))
											: null;

										columnMapping.Add(new PpsColumnMapping(nativeColumn, getterFunc, setterFunc));
									}
								}
								else
									columnMapping.Add(new PpsColumnMapping(nativeColumn, row => ((PpsDataRow)row)[idx], (row, value) => ((PpsDataRow)row)[idx] = value));
							}
						}
					}
				} // proc CreateColumnMapping

				if (parameter.GetMemberValue("rows") is IEnumerable<PpsDataRow> rows) // from DataTable
				{
					var rowEnumerator = rows.GetEnumerator();
					if (!rowEnumerator.MoveNext()) // no arguments defined
					{
						rowEnumerator.Dispose();
						return (null, null); // silent return nothing
					}

					// map the columns
					CreateColumnMapping(((IDataColumns)rowEnumerator.Current).Columns);
					CheckColumnMapping();

					return (rowEnumerator, columnMapping.ToArray());
				}
				else if (parameter["columnList"] is LuaTable luaColumnList) // from a "select"-list
				{
					foreach (var k in luaColumnList.ArrayList)
					{
						if (k is string columnName)
							columnMapping.Add(new PpsColumnMapping(tableInfo.FindColumn(columnName, true), row => ((LuaTable)row)[columnName], (row, obj) => ((LuaTable)row)[columnName] = obj));
					}

					CheckColumnMapping();

					return (GetTableRowEnum(), columnMapping.ToArray());
				}
				else if(parameter["columnList"] is IDataColumns columnDefinition) // from a "column"-list
				{
					CreateColumnMapping(columnDefinition.Columns);
					CheckColumnMapping();

					return (GetTableRowEnum(), columnMapping.ToArray());
				}
				else // from arguments
				{
					var args = GetArguments(parameter, 1, true);
					foreach (var columnName in args.Members.Keys)
					{
						var column = tableInfo.FindColumn(columnName, true);
						if (column != null)
							columnMapping.Add(new PpsColumnMapping(column, row => ((LuaTable)row)[columnName], (row, obj) => ((LuaTable)row)[columnName] = obj));
					}

					CheckColumnMapping();

					return (GetTableRowEnum(), columnMapping.ToArray());
				}
			} // func PrepareColumnMapping

			private IEnumerable<IEnumerable<IDataRow>> ExecuteUpsert(LuaTable parameter, string name, PpsDataTransactionExecuteBehavior behavior)
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

				// data table row mapping
				var (rowEnumerator, columnMapping) = PrepareColumnMapping(tableInfo, parameter);
				if (columnMapping == null)
					yield break;

				using (var cmd = CreateCommand(parameter, CommandType.Text))
				{
					var commandText = new StringBuilder();
					
					#region -- dst --
					commandText.Append("MERGE INTO ")
						.Append(tableInfo.SqlQualifiedName)
						.Append(" as DST ");
					#endregion

					#region -- src --
					var columnNames = new StringBuilder();
					commandText.Append("USING (VALUES (");

					var first = true;
					foreach (var col in columnMapping)
					{
						if (first)
							first = false;
						else
						{
							commandText.Append(", ");
							columnNames.Append(", ");
						}

						commandText.Append(col.ParameterName);
						col.ColumnInfo.AppendAsColumn(columnNames);
						col.AppendParameter(cmd, null);
					}
					
					commandText.Append(")) AS SRC (")
						.Append(columnNames)
						.Append(") ");
					#endregion

					#region -- on --
					commandText.Append("ON ");
					var onClauseValue = parameter.GetMemberValue("on");
					if (onClauseValue == null) // no on clause use primary key
					{
						var col = tableInfo.PrimaryKey ?? throw new ArgumentNullException("primaryKey", $"Table {tableInfo.SqlQualifiedName} has no primary key (use the onClause).");
						ExecuteUpsertAppendOnClause(commandText, col);
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
							ExecuteUpsertAppendOnClause(commandText, col);
						}
					}
					else
						throw new ArgumentException("Can not interpret on-clause.");
					commandText.Append(" ");
					#endregion

					#region -- when matched --
					commandText.Append("WHEN MATCHED THEN UPDATE SET ");
					first = true;
					foreach (var col in columnMapping)
					{
						if (col.ColumnInfo.IsIdentity) // no autoincrement
							continue;
						else if (first)
							first = false;
						else
							commandText.Append(", ");
						commandText.Append("DST.");
						col.ColumnInfo.AppendAsColumn(commandText);
						commandText.Append(" = ");
						commandText.Append("SRC.");
						col.ColumnInfo.AppendAsColumn(commandText);
					}
					commandText.Append(' ');
					#endregion

					#region -- when not matched by target --
					commandText.Append("WHEN NOT MATCHED BY TARGET THEN INSERT (");
					first = true;
					foreach (var col in columnMapping)
					{
						if (col.ColumnInfo.IsIdentity)
							continue;
						else if (first)
							first = false;
						else
							commandText.Append(", ");
						col.ColumnInfo.AppendAsColumn(commandText);
					}
					commandText.Append(") VALUES (");
					first = true;
					foreach (var col in columnMapping)
					{
						if (col.ColumnInfo.IsIdentity)
							continue;
						else if (first)
							first = false;
						else
							commandText.Append(", ");
						commandText.Append("SRC.");
						col.ColumnInfo.AppendAsColumn(commandText);
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
					commandText.Append("OUTPUT ");
					first = true;
					foreach (var col in tableInfo.Columns)
					{
						if (first)
							first = false;
						else
							commandText.Append(", ");
						commandText.Append("INSERTED.");
						col.AppendAsColumn(commandText);
					}

					#endregion

					commandText.Append(';');

					cmd.CommandText = commandText.ToString();

					do
					{
						var currentRow = rowEnumerator.Current;

						// update parameter
						foreach (var col in columnMapping)
							col.SetParameter(currentRow);

						// exec
						using (var r = cmd.ExecuteReaderEx(CommandBehavior.SingleRow))
						{
							if (!r.Read())
								throw new InvalidDataException("Invalid return data from sql command.");

							for (var i = 0; i < r.FieldCount; i++)
							{
								var col = columnMapping.FirstOrDefault(c => c.ColumnName == r.GetName(i));
								if (col != null)
									col.UpdateValue(currentRow, r.GetValue(i).NullIfDBNull());
								else if (currentRow is LuaTable t)
									t[r.GetName(i)] = r.GetValue(i);
							}
						}
					} while (rowEnumerator.MoveNext());
				}
				yield break;
			} // func ExecuteUpsert

			private static void ExecuteUpsertAppendOnClause(StringBuilder commandText, PpsSqlColumnInfo col)
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

			#region -- ExecuteSimpleSelect --------------------------------------------------

			#region -- class DefaultRowEnumerable -------------------------------------------

			private sealed class DefaultRowEnumerable : IEnumerable<IDataRow>
			{
				private sealed class DefaultValueRow : DynamicDataRow
				{
					private readonly IDataRow current;
					private readonly LuaTable defaults;

					public DefaultValueRow(LuaTable defaults, IDataRow current)
					{
						this.defaults = defaults;
						this.current = current;
					} // ctor

					private object GetDefaultValue(IDataRow current, int index)
					{
						var memberName = current.Columns[index].Name;
						var value = defaults.GetMemberValue(memberName);

						if (Lua.RtInvokeable(value))
							return new LuaResult(Lua.RtInvoke(value, current))[0];
						else
							return value;
					} // func GetDefaultValue

					public override object this[int index] => current[index] ?? GetDefaultValue(current, index);

					public override bool IsDataOwner => current.IsDataOwner;
					public override IReadOnlyList<IDataColumn> Columns => current.Columns;
				} // class DefaultValueRow

				private sealed class DefaultRowEnumerator : IEnumerator<IDataRow>
				{
					private readonly LuaTable defaults;
					private readonly IEnumerator<IDataRow> enumerator;

					public DefaultRowEnumerator(LuaTable defaults, IEnumerator<IDataRow> enumerator)
					{
						this.defaults = defaults ?? throw new ArgumentNullException(nameof(defaults));
						this.enumerator = enumerator ?? throw new ArgumentNullException(nameof(enumerator));
					} // ctor

					public void Dispose() 
						=> enumerator.Dispose();

					public bool MoveNext()
						=> enumerator.MoveNext();

					public void Reset()
						=> enumerator.Reset();

					public IDataRow Current => new DefaultValueRow(defaults, enumerator.Current);
					object IEnumerator.Current => Current;
				} // class DefaultRowEnumerator

				private readonly LuaTable defaults;
				private readonly IEnumerable<IDataRow> rowEnumerable;

				public DefaultRowEnumerable(LuaTable defaults, IEnumerable<IDataRow> rowEnumerable)
				{
					this.defaults = defaults;
					this.rowEnumerable = rowEnumerable;
				} // ctor

				public IEnumerator<IDataRow> GetEnumerator()
					=> new DefaultRowEnumerator(defaults, rowEnumerable.GetEnumerator());

				IEnumerator IEnumerable.GetEnumerator() 
					=> GetEnumerator();
			} // class DefaultRowEnumerable

			#endregion

			private IEnumerable<IEnumerable<IDataRow>> ExecuteSimpleSelect(LuaTable parameter, string name, PpsDataTransactionExecuteBehavior behavior)
			{
				/*
				 * select @cols from @name where @args
				 */

				// collect tables
				var tableInfos = new SqlJoinExpression(SqlDataSource);
				tableInfos.Parse(name);

				var defaults = GetArguments(parameter.GetMemberValue("defaults"), false);
				
				using (var cmd = CreateCommand(parameter, CommandType.Text))
				{
					var first = true;
					var commandText = new StringBuilder("SELECT ");

					#region -- select List --
					var columnList = parameter.GetMemberValue("columnList");
					if (columnList == null) // no columns, simulate a select *
					{
						#region -- append select * --
						foreach (var table in tableInfos.GetTables())
						{
							foreach (var column in table.Table.Columns)
							{
								if (first)
									first = false;
								else
									commandText.Append(", ");

								tableInfos.AppendColumn(commandText, table, column);
							}
						}
						#endregion
					}
					else if (columnList is LuaTable t) // columns are definied in a table
					{
						#region -- append select columns --
						void AppendColumnFromTableKey(string columnName)
						{
							var (table, column) = tableInfos.FindColumn(columnName, defaults == null);
							if (column != null) // append table column
								tableInfos.AppendColumn(commandText, table, column);
							else // try append empty DbNull column
							{
								var field = DataSource.Application.GetFieldDescription(columnName, true);
								commandText.Append(PpsSqlColumnInfo.AppendSqlParameter(cmd, field).ParameterName);
							}
						} // proc AppendColumnFromTableKey

						foreach (var item in t.ArrayList.OfType<string>())
						{
							if (first)
								first = false;
							else
								commandText.Append(", ");
							AppendColumnFromTableKey(item);
						}

						foreach (var m in t.Members)
						{
							if (first)
								first = false;
							else
								commandText.Append(", ");

							AppendColumnFromTableKey(m.Key);

							commandText.Append(" AS [").Append(m.Value).Append(']');
						}
						#endregion
					}
					else if (columnList is IDataColumns forcedColumns) // column set is forced
					{
						#region -- append select columns --
						foreach (var col in forcedColumns.Columns)
						{
							if (first)
								first = false;
							else
								commandText.Append(", ");

							var (table, column) = col is IPpsColumnDescription ppsColumn
								? tableInfos.FindColumn(ppsColumn, defaults == null)
								: tableInfos.FindColumn(col.Name, defaults == null);

							if (column != null) // append table column
								tableInfos.AppendColumn(commandText, table, column);
							else // try append empty DbNull column
								commandText.Append(PpsSqlColumnInfo.AppendSqlParameter(cmd, col).ParameterName);

							commandText.Append(" AS [").Append(col.Name).Append(']');
						}
						#endregion
					}
					else
						throw new ArgumentException("Unknown columnList definition.");
					#endregion

					// append from
					commandText.Append(" FROM ");
					commandText.Append(tableInfos.EmitJoin());

					// get where arguments
					var args = GetArguments(parameter, 1, false);
					if (args != null)
					{
						commandText.Append(" WHERE ");
						first = true;
						foreach (var p in args.Members)
						{
							if (first)
								first = false;
							else
								commandText.Append(" AND ");

							var (table, column) = tableInfos.FindColumn((string)p.Key, true);
							var parm = column.AppendSqlParameter(cmd, value: p.Value);
							tableInfos.AppendColumn(commandText, table, column);
							commandText.Append(" = ")
								.Append(parm.ParameterName);
						}
					}
					else if (parameter.GetMemberValue("where") is string sqlWhere)
						commandText.Append(" WHERE ").Append(sqlWhere);

					cmd.CommandText = commandText.ToString();

					using (var r = ExecuteReaderCommand(cmd, behavior))
					{
						// return results
						if (r != null)
						{
							do
							{
								if (defaults != null)
									yield return new DefaultRowEnumerable(defaults, new DbRowReaderEnumerable(r));
								else
									yield return new DbRowReaderEnumerable(r);

								if (behavior == PpsDataTransactionExecuteBehavior.SingleResult)
									break;
							} while (r.NextResult());
						}
					} // using r
				}
			} // proc ExecuteSimpleSelect

			#endregion

			#region -- ExecuteSql -----------------------------------------------------------

			private static Regex regExSqlParameter = new Regex(@"\@(\w+)", RegexOptions.Compiled);

			private IEnumerable<IEnumerable<IDataRow>> ExecuteSql(LuaTable parameter, string name, PpsDataTransactionExecuteBehavior behavior)
			{
				/*
				 * sql is execute and the args are created as a parameter
				 */

				using (var cmd = CreateCommand(parameter, CommandType.Text))
				{
					cmd.CommandText = name;

					var args = GetArguments(parameter, 1, false);
					if (args != null)
					{
						foreach (Match m in regExSqlParameter.Matches(name))
						{
							var k = m.Groups[1].Value;
							var v = args.GetMemberValue(k, true);
							cmd.Parameters.Add(new SqlParameter("@" + k, v.NullIfDBNull()));
						}
					}

					// execute
					using (var r = ExecuteReaderCommand(cmd, behavior))
					{
						if (r != null)
						{
							do
							{
								yield return new DbRowReaderEnumerable(r);
							} while (r.NextResult());
						}
					}
				}
			} // func ExecuteSql

			#endregion

			#region -- ExecuteDelete --------------------------------------------------------

			private IEnumerable<IEnumerable<IDataRow>> ExecuteDelete(LuaTable parameter, string name, PpsDataTransactionExecuteBehavior behavior)
			{
				/*
				 * DELETE FROM name 
				 * OUTPUT
				 * WHERE Col = @Col
				 */

				// find the connected table
				var tableInfo = SqlDataSource.ResolveTableByName<SqlTableInfo>(name, true);

				using (var cmd = CreateCommand(parameter, CommandType.Text))
				{
					var commandText = new StringBuilder();

					commandText.Append("DELETE ")
						.Append(tableInfo.SqlQualifiedName);

					// default is that only one row is done
					var args = GetArguments(parameter, 1, true);

					// add primary key as out put
					commandText.Append(" OUTPUT ")
						.Append("deleted.")
						.Append(tableInfo.PrimaryKey.Name);

					// append where
					commandText.Append(" WHERE ");

					var first = true;
					foreach (var m in args.Members)
					{
						var column = tableInfo.FindColumn(m.Key, false);
						if (column == null)
							continue;

						if (first)
							first = false;
						else
							commandText.Append(" AND ");

						var columnName = column.Name;
						var parameterName = '@' + columnName;
						commandText.Append(columnName)
							.Append(" = ")
							.Append(parameterName);
						column.AppendSqlParameter(cmd, parameterName, m.Value);
					}

					if (first && args.GetOptionalValue("__all", false))
						throw new ArgumentException("To delete all rows, set __all to true.");

					cmd.CommandText = commandText.ToString();

					// execute delete
					using (var r = ExecuteReaderCommand(cmd, behavior))
					{
						// return results
						if (r != null)
						{
							do
							{
								yield return new DbRowReaderEnumerable(r);
								if (behavior == PpsDataTransactionExecuteBehavior.SingleResult)
									break;
							} while (r.NextResult());
						}
					}
				}
				yield break; // empty enumeration
			} // func ExecuteDelete

			#endregion

			protected override IEnumerable<IEnumerable<IDataRow>> ExecuteResult(LuaTable parameter, PpsDataTransactionExecuteBehavior behavior)
			{
				string name;
				if ((name = (string)parameter["execute"]) != null)
				{
					if (name == "sys.UpdateRevisionData")
						return ExecuteUpdateRevisionData(parameter, behavior);
					else if (name == "sys.GetRevisionData")
						return ExecuteGetRevisionData(parameter, behavior);
					else
						return ExecuteCall(parameter, name, behavior);
				}
				else if ((name = (string)parameter["insert"]) != null)
					return ExecuteInsert(parameter, name, behavior);
				else if ((name = (string)parameter["update"]) != null)
					return ExecuteUpdate(parameter, name, behavior);
				else if ((name = (string)parameter["delete"]) != null)
					return ExecuteDelete(parameter, name, behavior);
				else if ((name = (string)parameter["upsert"]) != null)
					return ExecuteUpsert(parameter, name, behavior);
				else if ((name = (string)parameter["select"]) != null)
					return ExecuteSimpleSelect(parameter, name, behavior);
				else if ((name = (string)parameter["sql"]) != null)
					return ExecuteSql(parameter, name, behavior);
				else
					throw new NotImplementedException();
			} // func ExecuteResult

			#endregion

			public PpsSqlExDataSource SqlDataSource => (PpsSqlExDataSource)base.DataSource;
			public SqlTransaction InternalTransaction => transaction;
		} // class SqlDataTransaction

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

			protected override void InitSqlParameter(DbParameter parameter, string parameterName, object value)
			{
				base.InitSqlParameter(parameter, parameterName, value);
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

		#region -- SqlTableInfo -------------------------------------------------------

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

		#region -- class SqlSynchronizationTransaction --------------------------------

		private sealed class SqlSynchronizationTransaction : PpsDataSynchronization
		{
			#region -- class SqlSynchronizationBatch ----------------------------------

			private sealed class SqlSynchronizationBatch : IPpsDataSynchronizationBatch
			{
				private readonly SqlCommand command;
				private readonly bool isFull;
				private readonly DbRowEnumerator reader;

				private long currentSyncId;

				public SqlSynchronizationBatch(long currentSyncId, SqlCommand command, bool isFull)
				{
					this.currentSyncId = currentSyncId;
					this.command = command ?? throw new ArgumentNullException(nameof(command));
					this.isFull = isFull;
					this.reader = new DbRowEnumerator(command.ExecuteReader(), true);
				} // ctor

				public void Dispose()
				{
					command.Dispose();
					reader.Dispose();
				} // proc Dispose

				public bool MoveNext()
				{
					var r = reader.MoveNext();
					if (r)
					{
						var t = reader.Current[1].ChangeType<long>();
						if (t > currentSyncId)
							currentSyncId = t;
					}
					return r;
				} // func MoveNext

				public void Reset()
					=> ((IEnumerator)reader).Reset();

				public IDataRow Current => reader.Current;
				object IEnumerator.Current => reader.Current;

				public IReadOnlyList<IDataColumn> Columns => reader.Columns;

				public long CurrentSyncId => currentSyncId;
				public char CurrentMode => reader.Current[0].ToString()[0];
				public bool IsFullSync => isFull;
			} // class SqlSynchronizationBatch

			#endregion

			private readonly long startCurrentSyncId;
			private readonly bool isForceFull;
			private readonly SqlTransaction transaction;

			#region -- Ctor/Dtor --------------------------------------------------------

			public SqlSynchronizationTransaction(PpsApplication application, PpsDataSource dataSource, IPpsPrivateDataContext privateDataContext, DateTime lastSynchronization)
				:base(application, dataSource.CreateConnection(privateDataContext, true), lastSynchronization)
			{
				((SqlConnectionHandle)Connection).EnsureConnectionAsync(true).AwaitTask();

				// create transaction
				this.transaction = SqlConnection.BeginTransaction(IsolationLevel.ReadCommitted);

				// get the current sync id
				using (var cmd = SqlConnection.CreateCommand())
				{
					cmd.CommandTimeout = 6000;
					cmd.Transaction = transaction;
					cmd.CommandText = "SELECT change_tracking_current_version(), create_date FROM sys.databases WHERE database_id = DB_ID()";

					using (var r = cmd.ExecuteReaderEx(CommandBehavior.SingleRow))
					{
						if (!r.Read())
							throw new InvalidOperationException();
						if (r.IsDBNull(0))
							throw new ArgumentException("Change tracking is not active in this database.");

						startCurrentSyncId = r.GetInt64(0); // get highest SyncId
						isForceFull = r.GetDateTime(1).ToUniversalTime() > lastSynchronization; // recreate database
					}
				}
			} // ctor

			protected override void Dispose(bool disposing)
			{
				if (disposing)
					transaction.Dispose();
				base.Dispose(disposing);
			} // proc Dispose

			#endregion

			private static void PrepareSynchronizationColumns(PpsDataTableDefinition table, StringBuilder command, string primaryKeyPrefix = null)
			{
				foreach (var col in table.Columns)
				{
					var colInfo = ((PpsDataColumnServerDefinition)col).GetColumnDescription<SqlColumnInfo>();
					if (colInfo != null)
					{
						if (primaryKeyPrefix != null && colInfo.IsPrimaryKey)
							command.Append(',').Append(primaryKeyPrefix).Append('[');
						else
							command.Append(",d.[");
						command.Append(colInfo.Name).Append(']')
							.Append(" AS [").Append(col.Name).Append(']');
					}
				}

				// add revision hint
				if (table.Name == "ObjectTags")
				{
					command.Append(",CASE WHEN d.[ObjRId] IS NOT NULL THEN d.[Class] ELSE NULL END AS [LocalClass]");
					command.Append(",CASE WHEN d.[ObjRId] IS NOT NULL THEN d.[Value] ELSE NULL END AS [LocalValue]");
				}
			} // func PrepareSynchronizationColumns

			private string PrepareChangeTrackingCommand(PpsDataTableDefinition table, PpsSqlTableInfo tableInfo, PpsSqlColumnInfo columnInfo, long lastSyncId)
			{
				// build command string for change table
				var command = new StringBuilder("SELECT ct.sys_change_operation,ct.sys_change_version");

				PrepareSynchronizationColumns(table, command, "ct.");

				command.Append(" FROM ")
					.Append("changetable(changes ").Append(tableInfo.SqlQualifiedName).Append(',').Append(lastSyncId).Append(") as Ct ")
					.Append("LEFT OUTER JOIN ").Append(tableInfo.SqlQualifiedName)
					.Append(" as d ON d.").Append(columnInfo.Name).Append(" = ct.").Append(columnInfo.Name);

				return command.ToString();
			} // proc PrepareChangeTrackingCommand

			private string PrepareFullCommand(PpsDataTableDefinition table, PpsSqlTableInfo tableInfo)
			{
				var command = new StringBuilder("SELECT 'I',cast(" + startCurrentSyncId.ToString() + " as bigint)");

				PrepareSynchronizationColumns(table, command);

				command.Append(" FROM ")
					.Append(tableInfo.SqlQualifiedName)
					.Append(" as d");

				return command.ToString();
			} // proc PrepareFullCommand

			private IPpsDataSynchronizationBatch GenerateChangeTrackingBatch(PpsDataTableDefinition table, long lastSyncId)
			{
				var column = (PpsDataColumnServerDefinition)table.PrimaryKey;
				var columnInfo = column.GetColumnDescription<SqlColumnInfo>();
				if (columnInfo == null)
					throw new ArgumentOutOfRangeException("columnInfo", null, $"{column.Name} is not a sql column.");

				var tableInfo = columnInfo.Table;
				var isFull = isForceFull || lastSyncId < 0;

				// is the given syncId valid
				if (!isFull)
				{
					using (var getMinVersionCommand = SqlConnection.CreateCommand())
					{
						getMinVersionCommand.Transaction = transaction;
						getMinVersionCommand.CommandText = "SELECT change_tracking_min_valid_version(object_id('" + tableInfo.SqlQualifiedName + "'))";

						var minValidVersionValue = getMinVersionCommand.ExecuteScalar();
						if (minValidVersionValue == DBNull.Value)
							throw new ArgumentException($"Change tracking is not activated for '{tableInfo.SqlQualifiedName}'.");

						var minValidVersion = minValidVersionValue.ChangeType<long>();
						isFull = minValidVersion > lastSyncId;
					}
				}

				// generate the command
				var command = SqlConnection.CreateCommand();
				try
				{
					var commandText = isFull ?
						PrepareFullCommand(table, tableInfo) :
						PrepareChangeTrackingCommand(table, tableInfo, columnInfo, lastSyncId);

					if (table.Name == "ObjectTags") // special case for tags
						commandText += " LEFT OUTER JOIN dbo.ObjK o ON (o.Id = d.ObjKId) WHERE d.ObjRId is null OR (d.ObjRId = o.HeadRevId)"; // only no rev tags

					command.CommandTimeout = 7200;
					command.Transaction = transaction;
					command.CommandText = commandText;

					return new SqlSynchronizationBatch(startCurrentSyncId, command, isFull);
				}
				catch 
				{
					command.Dispose();
					throw;
				}
			} // proc GenerateChangeTrackingBatch

			public override IPpsDataSynchronizationBatch GenerateBatch(PpsDataTableDefinition table, string syncType, long lastSyncId)
			{
				ParseSynchronizationArguments(syncType, out var syncAlgorithm, out var syncArguments);

				if (String.Compare(syncAlgorithm, "TimeStamp", StringComparison.OrdinalIgnoreCase) == 0)
				{
					ParseSynchronizationTimeStampArguments(syncArguments, out var name, out var column);

					return CreateTimeStampBatchFromSelector(name, column, lastSyncId);
				}
				else if (String.Compare(syncAlgorithm, "ChangeTracking", StringComparison.OrdinalIgnoreCase) == 0)
				{
					return GenerateChangeTrackingBatch(table, lastSyncId);
				}
				else
				{
					throw new ArgumentException(String.Format("Unsupported sync algorithm: {0}", syncAlgorithm));
				}
			} // func GenerateBatch

			private SqlConnection SqlConnection => ((SqlConnectionHandle)base.Connection).Connection;
		} // class SqlSynchronizationTransaction

		#endregion

		private readonly SqlConnection masterConnection;
		private DEThread databaseMainThread = null;

		#region -- Ctor/Dtor/Config ---------------------------------------------------

		/// <summary></summary>
		/// <param name="sp"></param>
		/// <param name="name"></param>
		public PpsSqlExDataSource(IServiceProvider sp, string name)
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
				sqlConnectionString.IntegratedSecurity = true;

				// set the new connection
				masterConnection.ConnectionString = sqlConnectionString.ToString();

				// start background thread
				databaseMainThread = new DEThread(this, "Database", () => ExecuteDatabaseAsync());
			}
		} // proc InitMasterConnection

		/// <summary></summary>
		protected override void CloseMasterConnection()
			=> Procs.FreeAndNil(ref databaseMainThread);

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
			using (var cmd = masterConnection.CreateCommand())
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
				}
			}

			// Register Server logins
			Application.RegisterView(CreateSelectorTokenFromResourceAsync("dbo.serverLogins", typeof(PpsSqlExDataSource), "tsql.ServerLogins.sql").AwaitTask());
		} // proc InitializeSchemaCore
		
		#endregion

		#region -- Execute Database ---------------------------------------------------

		private async Task ExecuteDatabaseAsync()
		{
			var lastChangeTrackingId = -1L;

			while (databaseMainThread.IsRunning)
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
								if(r is long l)
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
					catch (Exception e)
					{
						Log.Except(e); // todo: detect disconnect
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
		/// <param name="selectStatement"></param>
		/// <returns></returns>
		protected override async Task<string> CreateOrReplaceViewAsync(DbConnection connection, string name, string selectStatement)
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
		/// <param name="viewName"></param>
		/// <param name="whereCondition"></param>
		/// <param name="orderBy"></param>
		/// <param name="start"></param>
		/// <param name="count"></param>
		/// <returns></returns>
		protected override DbCommand CreateViewCommand(IPpsSqlConnectionHandle connection, string selectList, string viewName, string whereCondition, string orderBy, int start, int count)
		{
			SqlCommand cmd = null;
			try
			{
				var trans = Application.Database.GetActiveTransaction(connection.DataSource);
				if (trans is SqlDataTransaction sqlTrans)
				{
					cmd = sqlTrans.CreateCommand(CommandType.Text, false);
				}
				else
				{
					cmd = new SqlCommand
					{
						Connection = ((SqlConnectionHandle)connection).Connection,
						CommandType = CommandType.Text,
					};
				}

				var sb = new StringBuilder("SELECT ");

				// build the select
				if (String.IsNullOrEmpty(selectList))
					sb.Append("* ");
				else
					sb.Append(selectList).Append(' ');

				// add the view
				sb.Append("FROM ").Append(viewName).Append(' ');

				// add the where
				if (!String.IsNullOrEmpty(whereCondition))
					sb.Append("WHERE ").Append(whereCondition).Append(' ');

				// add the orderBy
				if (!String.IsNullOrEmpty(orderBy))
				{
					sb.Append("ORDER BY ").Append(orderBy).Append(' ');

					// build the range, without order fetch is not possible
					if (count >= 0 && start < 0)
						start = 0;
					if (start >= 0)
					{
						sb.Append("OFFSET ").Append(start).Append(" ROWS ");
						if (count >= 0)
							sb.Append("FETCH NEXT ").Append(count).Append(" ROWS ONLY ");
					}
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

		private SqlConnectionHandle GetSqlConnection(IPpsConnectionHandle connection, bool throwException)
			=> (SqlConnectionHandle)connection;

		/// <summary></summary>
		/// <param name="userContext"></param>
		/// <param name="throwException"></param>
		/// <returns></returns>
		public override IPpsConnectionHandle CreateConnection(IPpsPrivateDataContext userContext, bool throwException = true)
			=> new SqlConnectionHandle(this, userContext.GetNetworkCredential());

		/// <summary></summary>
		/// <param name="connection"></param>
		/// <returns></returns>
		public override PpsDataTransaction CreateTransaction(IPpsConnectionHandle connection)
		{
			var c = GetSqlConnection(connection, true);
			return new SqlDataTransaction(this, c);
		} // func CreateTransaction

		/// <summary></summary>
		/// <param name="privateUserData"></param>
		/// <param name="lastSynchronization"></param>
		/// <returns></returns>
		public override PpsDataSynchronization CreateSynchronizationSession(IPpsPrivateDataContext privateUserData, DateTime lastSynchronization)
			=> new SqlSynchronizationTransaction(Application, this, privateUserData, lastSynchronization);

		/// <summary>Is the master connection of the data source connected.</summary>
		public bool IsConnected
		{
			get
			{
				lock (masterConnection)
					return IsConnectionOpen(masterConnection);
			}
		} // prop IsConnected

		/// <summary>Returns always mssql</summary>
		public override string Type => "mssql";

		// -- Static --------------------------------------------------------------
	

		private static bool IsConnectionOpen(SqlConnection connection)
			=> connection.State != System.Data.ConnectionState.Closed;

		internal static SqlConnection GetSqlConnection(IPpsConnectionHandle connection)
			=> connection is SqlConnectionHandle c
				? c.Connection
				: null;

		internal static SqlCommand CreateSqlCommand(PpsDataTransaction trans, CommandType commandType, bool noTransaction)
			=> trans is SqlDataTransaction t ? t.CreateCommand(commandType, noTransaction) : throw new ArgumentException(nameof(trans));
	} // PpsSqlExDataSource
}