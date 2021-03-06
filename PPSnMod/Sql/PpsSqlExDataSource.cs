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
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
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
	public sealed class PpsSqlExDataSource : PpsMsSqlDataSource
	{
		#region -- class SqlDataTransaction -------------------------------------------

		/// <summary></summary>
		private sealed class SqlDataTransaction : PpsMsSqlDataTransaction
		{
			#region -- Ctor/Dtor --------------------------------------------------------

			public SqlDataTransaction(PpsSqlDataSource dataSource, IPpsConnectionHandle connectionHandle)
				: base(dataSource, connectionHandle)
			{
			} // ctor

			#endregion

			#region -- Execute Result -------------------------------------------------

			#region -- UpdateRevisionData ---------------------------------------------

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

			private IEnumerable<IEnumerable<IDataRow>> UpdateRevisionData(object _args, PpsDataTransactionExecuteBehavior behavior)
			{
				// function to update revision
				// UpdateRevision(long ObjkId, long RevId, long ParentId, long CreateUserId, [opt] date CreateDate, bool Deflate, bool IsDocumentText)

				// read arguments
				var args = (LuaTable)_args;
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
				var integratedUser = Credentials as PpsIntegratedCredentials;
				var useFileStream = readed >= buf.Length;

				if (useFileStream) // inline data in revision
				{
					if (integratedUser != null)
					{
						#region -- insert into objf --
						using (integratedUser.Impersonate())
						using (var cmd = CreateCommand(CommandType.Text, false))
						{
							cmd.CommandText = "INSERT INTO dbo.[ObjF] ([HashAlgo], [Hash], [Data]) "
								+ "OUTPUT inserted.Id, inserted.Data.PathName(), GET_FILESTREAM_TRANSACTION_CONTEXT() "
								+ "VALUES ('SHA2_256', 0x, 0x);";

							using (var r = (SqlDataReader)cmd.ExecuteReaderEx(CommandBehavior.SingleRow))
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
						using (var cmd = CreateCommand(CommandType.Text, false))
						{
							cmd.CommandText = "UPDATE dbo.[ObjF] SET [HashAlgo] = @HashAlgo, [Hash] = @HashValue WHERE [Id] = @DocumentId;";
							cmd.Parameters.Add("@HashValue", SqlDbType.VarBinary).Value = args["HashValue"];
							cmd.Parameters.Add("@HashAlgo", SqlDbType.VarChar).Value = args["HashAlgo"];
							cmd.Parameters.Add("@DocumentId", SqlDbType.BigInt).Value = documentId;

							cmd.ExecuteNonQueryEx();
						}
						#endregion
					}
					else
					{
						#region -- insert into obf --
						using (var cmd = CreateCommand(CommandType.Text, false))
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

							using (var r = (SqlDataReader)cmd.ExecuteReaderEx(CommandBehavior.SingleRow))
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
				using (var cmd = CreateCommand(CommandType.Text, false))
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

					using (var r = (SqlDataReader)cmd.ExecuteReaderEx(CommandBehavior.SingleRow))
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
			} // proc UpdateRevisionData

			#endregion

			#region -- GetRevisionData ------------------------------------------------

			private IEnumerable<IEnumerable<IDataRow>> GetRevisionData(object _args, PpsDataTransactionExecuteBehavior behavior)
			{
				// function to get revision data
				// GetRevisionData(long ObjkId oder long RevId)

				var args = (LuaTable)_args;
				var revId = args.GetOptionalValue("RevId", -1L);
				var objkId = args.GetOptionalValue("ObjkId", -1L);

				if (revId <= 0
					&& objkId <= 0)
					throw new ArgumentException("Invalid arguments.", "revId|objkId");

				using (var cmd = CreateCommand(CommandType.Text, false))
				{
					var useFileStream = Credentials is PpsIntegratedCredentials; // only integrated credentials can use filestream

					cmd.CommandText = "SELECT [IsDocumentText], [IsDocumentDeflate], [Document], [DocumentId], [DocumentLink], [HashAlgo], [Hash], " + (useFileStream ? "[Data].PathName(), GET_FILESTREAM_TRANSACTION_CONTEXT() " : "[Data] ")
						+ (revId > 0
							? "FROM dbo.[ObjR] r LEFT OUTER JOIN dbo.[ObjF] f ON (r.[DocumentId] = f.[Id]) WHERE r.[Id] = @Id;"
							: "FROM dbo.[ObjK] o INNER JOIN dbo.[ObjR] r ON (o.HeadRevId = r.[Id]) LEFT OUTER JOIN dbo.[ObjF] f ON (r.[DocumentId] = f.[Id]) WHERE o.[Id] = @Id;"
						);

					cmd.Parameters.Add("@Id", SqlDbType.BigInt).Value = revId > 0 ? revId : objkId;

					using (var r = (SqlDataReader)cmd.ExecuteReaderEx(CommandBehavior.SingleRow))
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
								using (((PpsIntegratedCredentials)Credentials).Impersonate())
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
			} // proc GetRevisionData

			#endregion

			#region -- PrepareCore ----------------------------------------------------

			protected override PpsDataCommand PrepareCore(LuaTable parameter, LuaTable firstArgs)
			{
				string name;
				if ((name = (string)(parameter["execute"] ?? parameter["exec"])) != null)
				{
					if (name == "sys.UpdateRevisionData")
						return new PpsInvokeDataCommand(UpdateRevisionData);
					else if (name == "sys.GetRevisionData")
						return new PpsInvokeDataCommand(GetRevisionData);
				}
				return base.PrepareCore(parameter, firstArgs);
			} // func PrepareCore

			#endregion

			#endregion
		} // class SqlDataTransaction

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
				public PpsSynchonizationMode Mode => isFull ? PpsSynchonizationMode.Full : PpsSynchonizationMode.Parts;
			} // class SqlSynchronizationBatch

			#endregion

			private readonly long startCurrentSyncId;
			private readonly bool isForceFull;
			private readonly SqlTransaction transaction;

			#region -- Ctor/Dtor --------------------------------------------------------

			public SqlSynchronizationTransaction(PpsApplication application, IPpsSqlConnectionHandle connection, long lastSyncronizationStamp, bool leaveConnectionOpen)
				: base(application, connection, leaveConnectionOpen)
			{
				Connection.EnsureConnectionAsync(true).AwaitTask();

				// create transaction
				transaction = SqlConnection.BeginTransaction(IsolationLevel.ReadCommitted);

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
						isForceFull = r.GetDateTime(1).ToFileTimeUtc() > lastSyncronizationStamp; // recreate database
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

			private static void PrepareSynchronizationColumns(PpsDataTableDefinition syncTable, PpsSqlTableInfo sourceTable, StringBuilder command, string primaryKeyPrefix = null)
			{
				foreach (var col in syncTable.Columns)
				{
					var colInfo = ((PpsDataColumnServerDefinition)col).GetColumnDescription<PpsSqlColumnInfo>();
					if (colInfo != null)
					{
						if (colInfo.Table == sourceTable)
						{
							if (primaryKeyPrefix != null && colInfo.IsPrimaryKey)
								command.Append(',').Append(primaryKeyPrefix).Append('[');
							else
								command.Append(",d.[");

							command.Append(colInfo.Name).Append(']')
							  .Append(" AS [").Append(col.Name).Append(']');
						}
						else
							throw new ArgumentException($"Column '{colInfo.TableColumnName}' is not definied in table '{sourceTable.QualifiedName}.");
					}
				}

				// add revision hint
				if (syncTable.Name == "ObjectTags")
				{
					command.Append(",CASE WHEN d.[ObjRId] IS NOT NULL THEN d.[Class] ELSE NULL END AS [LocalClass]");
					command.Append(",CASE WHEN d.[ObjRId] IS NOT NULL THEN d.[Value] ELSE NULL END AS [LocalValue]");
				}
			} // func PrepareSynchronizationColumns

			private string PrepareChangeTrackingCommand(PpsDataTableDefinition table, PpsSqlTableInfo tableInfo, PpsSqlColumnInfo columnInfo, long lastSyncId)
			{
				// build command string for change table
				var command = new StringBuilder("SELECT ct.sys_change_operation,ct.sys_change_version");

				PrepareSynchronizationColumns(table, tableInfo, command, "ct.");

				command.Append(" FROM ")
					.Append("changetable(changes ").Append(tableInfo.SqlQualifiedName).Append(',').Append(lastSyncId).Append(") as Ct ")
					.Append("LEFT OUTER JOIN ").Append(tableInfo.SqlQualifiedName)
					.Append(" as d ON d.").Append(columnInfo.Name).Append(" = ct.").Append(columnInfo.Name);

				return command.ToString();
			} // proc PrepareChangeTrackingCommand

			private string PrepareFullCommand(PpsDataTableDefinition table, PpsSqlTableInfo tableInfo)
			{
				var command = new StringBuilder("SELECT 'I',cast(" + startCurrentSyncId.ToString() + " as bigint)");

				PrepareSynchronizationColumns(table, tableInfo, command);

				command.Append(" FROM ")
					.Append(tableInfo.SqlQualifiedName)
					.Append(" as d");

				return command.ToString();
			} // proc PrepareFullCommand

			private IPpsDataSynchronizationBatch GenerateChangeTrackingBatch(PpsDataTableDefinition table, long lastSyncId)
			{
				var column = (PpsDataColumnServerDefinition)table.PrimaryKey;
				var columnInfo = column.GetColumnDescription<PpsSqlColumnInfo>();
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

			private SqlConnection SqlConnection => GetSqlConnection(base.Connection);
		} // class SqlSynchronizationTransaction

		#endregion

		#region -- Ctor/Dtor/Config ---------------------------------------------------

		/// <summary></summary>
		/// <param name="sp"></param>
		/// <param name="name"></param>
		public PpsSqlExDataSource(IServiceProvider sp, string name)
			: base(sp, name)
		{
		} // ctor

		/// <summary>Initialize server logins view</summary>
		protected override void RefreshSchemaCore(IPpsSqlSchemaUpdate log)
		{
			base.RefreshSchemaCore(log);

			// Register Server logins
			Application.RegisterView(CreateSelectorTokenFromResourceAsync("dbo.serverLogins", typeof(PpsSqlExDataSource), "tsql.ServerLogins.sql").AwaitTask());
		} // proc InitializeSchemaCore

		#endregion

		/// <summary></summary>
		/// <param name="connection"></param>
		/// <returns></returns>
		public override PpsDataTransaction CreateTransaction(IPpsConnectionHandle connection)
			=> new SqlDataTransaction(this, connection);

		/// <summary></summary>
		/// <param name="connection"></param>
		/// <param name="lastSyncronizationStamp"></param>
		/// <param name="leaveConnectionOpen"></param>
		/// <returns></returns>
		public override PpsDataSynchronization CreateSynchronizationSession(IPpsConnectionHandle connection, long lastSyncronizationStamp, bool leaveConnectionOpen)
			=> new SqlSynchronizationTransaction(Application, (IPpsSqlConnectionHandle)connection, lastSyncronizationStamp, leaveConnectionOpen);

		/// <summary>Returns always mssql</summary>
		public override string Type => "sqlex";
	} // PpsSqlExDataSource
}