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

		#region -- class SqlDataTransaction -------------------------------------------

		/// <summary></summary>
		private sealed class SqlDataTransaction : PpsSqlDataTransaction<SqlConnection, SqlTransaction, SqlCommand>
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

				public void AppendParameter(SqlDataTransaction trans, SqlCommand cmd, object initialValues)
				{
					if (parameter != null)
						throw new InvalidOperationException();
					parameter = trans.CreateParameter(cmd, columnInfo, parameterName, initialValues == null ? null : getValue(initialValues));
				} // func AppendParameter

				private string DebuggerDisplay
					=> $"Mapping: {columnInfo.TableColumnName} -> {parameterName}";

				public PpsSqlColumnInfo ColumnInfo => columnInfo;
				public string ColumnName => columnInfo.Name;
				public string ParameterName => parameterName;
			} // class PpsColumnMapping

			#endregion

			private readonly PpsCredentials credentials;

			#region -- Ctor/Dtor --------------------------------------------------------

			public SqlDataTransaction(PpsSqlDataSource dataSource, SqlConnectionHandle connectionHandle)
				: base(dataSource, connectionHandle)
			{
				this.credentials = connectionHandle.Credentials;
			} // ctor

			#endregion

			#region -- Execute Result -------------------------------------------------

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

							using (var r = ExecuteReaderCommand<SqlDataReader>(cmd, PpsDataTransactionExecuteBehavior.SingleRow))
							{
								if (r.Read())
								{
									// get destination file
									documentId = r.GetInt64(0);
									var path = r.GetString(1);
									var context = r.GetSqlBytes(2).Buffer;
									
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

							ExecuteReaderCommand<SqlDataReader>(cmd, PpsDataTransactionExecuteBehavior.NoResult);
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
							
							using (var r = ExecuteReaderCommand<SqlDataReader>(cmd, PpsDataTransactionExecuteBehavior.SingleResult))
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

					using (var r = ExecuteReaderCommand<SqlDataReader>(cmd, PpsDataTransactionExecuteBehavior.SingleRow))
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

					using (var r = ExecuteReaderCommand<SqlDataReader>(cmd, PpsDataTransactionExecuteBehavior.SingleRow))
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

			#region -- ExecuteCall ----------------------------------------------------

			protected override IEnumerable<IEnumerable<IDataRow>> ExecuteCall(LuaTable parameter, string name, PpsDataTransactionExecuteBehavior behavior)
			{
				if (name == "sys.UpdateRevisionData")
					return ExecuteUpdateRevisionData(parameter, behavior);
				else if (name == "sys.GetRevisionData")
					return ExecuteGetRevisionData(parameter, behavior);
				else
					return base.ExecuteCall(parameter, name, behavior);
			} // func ExecuteCall

			#endregion

			#region -- ExecuteInsert --------------------------------------------------

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
							CreateParameter(cmd, column, parameterName, value);
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

			#region -- ExecuteUpdate --------------------------------------------------

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

						CreateParameter(cmd, column, parameterName, value);

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
					CreateParameter(cmd, tableInfo.PrimaryKey, "@" + primaryKeyName, primaryKeyValue);

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

			#region -- ExecuteUpsert --------------------------------------------------
			
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
						col.AppendParameter(this, cmd, null);
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

			#region -- ExecuteDelete --------------------------------------------------

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
						CreateParameter(cmd, column, parameterName, m.Value);
					}

					if (first && args.GetOptionalValue("__all", false))
						throw new ArgumentException("To delete all rows, set __all to true.");

					cmd.CommandText = commandText.ToString();

					// execute delete
					using (var r = ExecuteReaderCommand<SqlDataReader>(cmd, behavior))
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
				if ((name = (string)parameter["insert"]) != null)
					return ExecuteInsert(parameter, name, behavior);
				else if ((name = (string)parameter["update"]) != null)
					return ExecuteUpdate(parameter, name, behavior);
				else if ((name = (string)parameter["delete"]) != null)
					return ExecuteDelete(parameter, name, behavior);
				else if ((name = (string)parameter["upsert"]) != null)
					return ExecuteUpsert(parameter, name, behavior);
				else
					return base.ExecuteResult(parameter, behavior);
			} // func ExecuteResult

			#endregion

			public PpsSqlExDataSource SqlDataSource => (PpsSqlExDataSource)DataSource;
			public SqlConnection SqlConnection => (SqlConnection)DbConnection;
			public SqlTransaction SqlTransaction => (SqlTransaction)DbTransaction;
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

			public override void InitSqlParameter(DbParameter parameter, string parameterName, object value)
			{
				if (String.IsNullOrEmpty(parameterName))
					parameterName = "@" + Name;

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
			private readonly ParameterDirection direction;
			private readonly SqlDbType dbType;
			private readonly int maxLength;
			private readonly byte scale;
			private readonly byte precision;

			private readonly string typeName;
			private readonly string xmlSchemaCollectionDatabase;
			private readonly string xmlSchemaCollectionName;
			private readonly string xmlSchemaCollectionOwningSchema;

			public SqlParameterInfo(IDataRecord r)
				: base(r.GetString(1), r.GetBoolean(7))
			{
				direction = (ParameterDirection)r.GetByte(2);
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
				p.Direction = direction;
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
			public SqlProcedureInfo(IDataRecord r) 
				: base(r.GetString(1), r.GetString(2))
			{
			} // ctor
		} // class SqlProcedureInfo

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
				databaseMainThread = new DEThread(this, "Database", ExecuteDatabaseAsync);
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

			// Register Server logins
			Application.RegisterView(CreateSelectorTokenFromResourceAsync("dbo.serverLogins", typeof(PpsSqlExDataSource), "tsql.ServerLogins.sql").AwaitTask());
		} // proc InitializeSchemaCore
		
		#endregion

		#region -- Execute Database ---------------------------------------------------

		private async Task ExecuteDatabaseAsync(DEThread thread)
		{
			var lastChangeTrackingId = -1L;

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
	} // PpsSqlExDataSource
}