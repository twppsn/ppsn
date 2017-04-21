using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Data;
using TecWare.DE.Networking;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;
using TecWare.PPSn.Properties;
using TecWare.PPSn.Stuff;
using System.Collections.ObjectModel;

namespace TecWare.PPSn
{
	#region -- enum PpsDataChangeOperation ----------------------------------------------

	public enum PpsDataChangeOperation
	{
		Insert,
		Delete,
		Update
	} // enum PpsDataChangeOperation

	#endregion

	#region -- enum PpsLoadPriority -----------------------------------------------------

	public enum PpsLoadPriority
	{
		Default = 1,
		ApplicationFile = 0,
		ObjectPrimaryData = 1,
		ObjectReferencedData = 2,
		Background = 3
	} // enum PpsLoadPriority

	#endregion

	#region -- interface IInternalFileCacheStream ---------------------------------------

	internal interface IInternalFileCacheStream
	{
		void MoveTo(string fileName);
	} // interface IInternalFileCacheStream

	#endregion

	#region -- class PpsMasterDataTransaction -------------------------------------------

	public abstract class PpsMasterDataTransaction : IDbTransaction, IDisposable
	{
		#region -- class PpsMasterNestedTransaction -------------------------------------

		private sealed class PpsMasterNestedTransaction : PpsMasterDataTransaction
		{
			public PpsMasterNestedTransaction(SQLiteConnection connection, SQLiteTransaction parentTransaction)
				: base(connection, parentTransaction)
			{
			} // ctor

			protected override void CommitCore() { }
			protected override void RollbackCore() { }
		} // class PpsMasterNestedTransaction

		#endregion

		#region -- class PpsMasterRootTransaction ---------------------------------------

		private sealed class PpsMasterRootTransaction : PpsMasterDataTransaction
		{
			public PpsMasterRootTransaction(SQLiteConnection connection, SQLiteTransaction rootTransaction)
				: base(connection, rootTransaction)
			{
			} // ctor

			protected override void Dispose(bool disposing)
			{
				base.Dispose(disposing);

				if (disposing)
					Transaction.Dispose();
			} // proc Dispose

			protected override void CommitCore()
				=> transaction.Commit();

			protected override void RollbackCore()
				=> transaction.Rollback();
		} // class PpsMasterRootTransaction

		#endregion

		private readonly SQLiteConnection connection;
		private readonly SQLiteTransaction transaction;
		private bool? transactionState = null;

		#region -- Ctor/Dtor/Commit/Rollback --------------------------------------------

		protected PpsMasterDataTransaction(SQLiteConnection connection, SQLiteTransaction transaction)
		{
			this.connection = connection ?? throw new ArgumentNullException(nameof(connection));
			this.transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
		} // ctor

		public void Dispose()
		{
			Dispose(true);
		} // proc Dispose

		protected virtual void Dispose(bool disposing)
		{
			if (!transactionState.HasValue)
			{
				if (transaction != null)
					Rollback();
				else
					transactionState = false;
			}
		} // proc Dispose

		protected abstract void CommitCore();
		protected abstract void RollbackCore();

		public void Commit()
		{
			CommitCore();
			transactionState = true;
		} // proc Commit

		public void Rollback()
		{
			RollbackCore();
			transactionState = false;
		} // proc Rollback

		#endregion

		public DbCommand CreateNativeCommand(string commandText = null)
			=> new SQLiteCommand(commandText, connection, transaction);

		public long GetNextLocalId(string tableName, string primaryKey)
			=> -1;

		public long LastInsertRowId => connection.LastInsertRowId;

		protected SQLiteTransaction Transaction => transaction;

		IDbConnection IDbTransaction.Connection => connection;
		public DbConnection Connection => connection;
		public IsolationLevel IsolationLevel => transaction?.IsolationLevel ?? System.Data.IsolationLevel.Unspecified;

		public bool IsDisposed => transactionState.HasValue;
		public bool IsCommited => transactionState ?? false;

		internal static PpsMasterDataTransaction Create(SQLiteConnection connection, PpsMasterDataTransaction transaction)
			=> transaction == null
				? (PpsMasterDataTransaction)new PpsMasterRootTransaction(connection, connection.BeginTransaction())
				: (PpsMasterDataTransaction)new PpsMasterNestedTransaction(connection, transaction.transaction);
	} // class PpsMasterTransaction

	#endregion

	#region -- class PpsMasterData ------------------------------------------------------

	public sealed class PpsMasterData : IDisposable
	{
		public const string MasterDataSchema = "masterData";
		private const string refreshColumnName = "_IsUpdated";

		#region -- class SqLiteParameterDictionaryWrapper -------------------------------

		private sealed class SqLiteParameterDictionaryWrapper : IPropertyReadOnlyDictionary
		{
			private readonly SQLiteParameter[] arguments;

			public SqLiteParameterDictionaryWrapper(SQLiteParameter[] arguments)
				=> this.arguments = arguments;

			public bool TryGetProperty(string name, out object value)
			{
				var p = arguments.FirstOrDefault(c => String.Compare(c.SourceColumn, name, StringComparison.OrdinalIgnoreCase) == 0);
				if (p == null)
				{
					value = null;
					return false;
				}
				else
				{
					value = p.Value;
					return true;
				}
			} // func TryGetProperty
		} // class SqLiteParameterDictionaryWrapper

		#endregion

		private readonly PpsEnvironment environment;
		private readonly SQLiteConnection connection;

		private PpsDataSetDefinitionDesktop schema;
		private bool? schemaIsOutDated = null;
		private DateTime lastSynchronizationSchema = DateTime.MinValue; // last synchronization of the schema
		private DateTime lastSynchronizationStamp = DateTime.MinValue;  // last synchronization stamp
		private bool isSynchronizationStarted = false; // number of sync processes

		private bool isDisposed = false;
		private bool isInSynchronization = false;

		#region -- Ctor/Dtor ------------------------------------------------------------

		public PpsMasterData(PpsEnvironment environment, SQLiteConnection connection, PpsDataSetDefinitionDesktop schema, DateTime lastSynchronizationSchema, DateTime lastSynchronizationStamp)
		{
			this.environment = environment;
			this.connection = connection;

			this.schema = schema;
			this.lastSynchronizationSchema = lastSynchronizationSchema;
			this.lastSynchronizationStamp = lastSynchronizationStamp;
		} // ctor

		public void Dispose()
		{
			if (!isDisposed)
			{
				isDisposed = true;
				connection?.Dispose();
			}
		} // proc Dispose

		#endregion

		#region -- Local store schema update --------------------------------------------

		private async Task UpdateSchemaAsync(IProgress<string> progress)
		{
			progress?.Report("Lokale Datenbank wird aktualisiert...");

			// load new schema
			var respone = await environment.Request.GetResponseAsync(environment.ActiveDataSets.GetDataSetSchemaUri(MasterDataSchema));
			var schemaStamp = respone.GetLastModified();
			var xSchema = environment.Request.GetXml(respone);

			var newMasterDataSchema = new PpsDataSetDefinitionDesktop(environment, MasterDataSchema, xSchema);
			newMasterDataSchema.EndInit();

			// generate update commands
			var updateScript = GetUpdateCommands(connection, newMasterDataSchema, CheckLocalTableExists(connection, "SyncState"));

			// execute update commands
			using (var transaction = connection.BeginTransaction())
			{
				try
				{
					if (updateScript.Count > 0)
						ExecuteUpdateScript(connection, transaction, updateScript);

					// update header
					var existRow = false;
					using (var cmd = connection.CreateCommand())
					{
						cmd.CommandText = "SELECT EXISTS (SELECT * FROM main.Header)";
						existRow = ((long)cmd.ExecuteScalarEx()) != 0;
					}

					using (var cmd = connection.CreateCommand())
					{
						cmd.Transaction = transaction;

						cmd.CommandText = existRow
							? "UPDATE main.Header SET SchemaStamp = @stamp, SchemaContent = @content;"
							: "INSERT INTO main.Header (SchemaStamp, SchemaContent) VALUES (@stamp, @content);";
						cmd.Parameters.Add("@stamp", DbType.Int64).Value = schemaStamp.ToFileTimeUtc();
						cmd.Parameters.Add("@content", DbType.AnsiString).Value = xSchema.ToString(SaveOptions.None);
						cmd.ExecuteNonQueryEx();
					}

					transaction.Commit();
				}
				catch
				{
					transaction.Rollback();
					throw;
				}
			}

			// update schema
			schema = newMasterDataSchema;
		} // proc UpdateSchemaAsync

		private static IReadOnlyList<string> GetUpdateCommands(SQLiteConnection connection, PpsDataSetDefinitionDesktop schema, bool syncStateTableExists)
		{
			var commands = new List<string>();
			var tableChanged = false;
			foreach (var table in schema.TableDefinitions)
			{
				if (CheckLocalTableExists(connection, table.Name)) // generate alter table script
				{
					tableChanged = CreateAlterTableScript(commands,
						table.Name,
						GetLocalTableColumns(connection, table.Name),
						GetLocalTableIndexes(connection, table.Name),
						table.Columns
					);
				}
				else // generate create table script
				{
					CreateTableScript(commands, table.Name, table.Columns, null);
					tableChanged = true;
				}

				// clear sync token
				if (tableChanged && syncStateTableExists)
					commands.Add($"DELETE FROM main.[SyncState] WHERE [Table] = '{table.Name}'");
			}

			return commands;
		} // func GetUpdateCommands

		private static void ExecuteUpdateScript(SQLiteConnection connection, SQLiteTransaction transaction, IEnumerable<string> commands)
		{
			using (var cmd = connection.CreateCommand())
			{
				cmd.Transaction = transaction;
				foreach (var c in commands)
				{
					cmd.CommandText = c;
					cmd.ExecuteNonQueryEx();
				}
			}
		} // proc ExecuteUpdateScript

		private static void CreateTableScript(List<string> commands, string tableName, IEnumerable<IDataColumn> remoteColumns, string[] localIndexArray)
		{
			// add dummy for the create table
			var createTableIndex = commands.Count;
			commands.Add(String.Empty);

			// create table
			var commandText = new StringBuilder("CREATE TABLE ");
			AppendSqlIdentifier(commandText, tableName).Append(" (");

			foreach (var column in remoteColumns)
			{
				if (String.Compare(column.Name, "_rowId", StringComparison.OrdinalIgnoreCase) == 0)
					continue; // ignore rowId column

				AppendSqlIdentifier(commandText, column.Name).Append(' ').Append(ConvertDataTypeToSqLite(column.DataType));

				// append primray key
				if (column.Attributes.GetProperty("IsPrimary", false))
				{
					commandText.Append(" PRIMARY KEY");
					CreateTableIndex(commands, tableName, column.Name, true, localIndexArray);
				}

				CreateCommandColumnAttribute(commandText, column);
				if (column.Attributes.GetProperty<bool>("IsUnique", false))
					CreateTableIndex(commands, tableName, column.Name, true, localIndexArray);

				commandText.Append(',');
			}
			commandText[commandText.Length - 1] = ')'; // replace last comma
			commandText.Append(";");

			commands[createTableIndex] = commandText.ToString();
		} // func CreateTableScript

		private static bool CreateAlterTableScript(List<string> commands, string tableName, IEnumerable<IDataColumn> localColumns, IEnumerable<Tuple<string, bool>> localIndexes, IEnumerable<IDataColumn> remoteColumns)
		{
			var localColumnsArray = localColumns.ToArray();
			var newColumns = new List<IDataColumn>();
			var sameColumns = new List<string>();   // for String.Join - only Column names are used
			var refreshColumnExists = false;

			// todo: check index list

			foreach (var remoteColumn in remoteColumns)
			{
				if (String.Compare(remoteColumn.Name, "_rowId", StringComparison.OrdinalIgnoreCase) == 0)
					continue; // ignore rowId column

				var found = false;
				foreach (var localColumn in localColumnsArray)
				{
					if (localColumn.Name == refreshColumnName)
						refreshColumnExists = true;

					// todo: check default
					if ((remoteColumn.Name == localColumn.Name)
						&& (ConvertDataTypeToSqLite(remoteColumn.DataType) == ConvertDataTypeToSqLite(localColumn.DataType))
						&& (remoteColumn.Attributes.GetProperty("IsNull", false) == localColumn.Attributes.GetProperty("IsNull", false))
						&& (remoteColumn.Attributes.GetProperty("IsPrimary", false) == localColumn.Attributes.GetProperty("IsPrimary", false))
						)
					{
						found = true;
						break;
					}
				}
				if (found)
					sameColumns.Add(remoteColumn.Name);
				else
					newColumns.Add(remoteColumn);
			}

			if (sameColumns.Count < localColumnsArray.Length || newColumns.Count > 0)
			{
				if (!refreshColumnExists) // drop and recreate
				{
					CreateDropScript(commands, tableName);
					CreateTableScript(commands, tableName, remoteColumns, null);
				}
				else if (sameColumns.Count < localColumnsArray.Length) // this is more performant than checking for obsolete columns
				{
					// rename local table
					commands.Add($"ALTER TABLE '{tableName}' RENAME TO '{tableName}_temp';");

					// create a new table, according to new Scheme...
					CreateTableScript(commands, tableName, remoteColumns, localIndexes.Select(c => c.Item1).ToArray());
					// copy
					var insertColumns = new List<string>(sameColumns);
					for (var i = 0; i < newColumns.Count; i++)
					{
						var idx = Array.FindIndex(localColumnsArray, c => String.Compare(c.Name, newColumns[i].Name, StringComparison.OrdinalIgnoreCase) == 0);
						if (idx >= 0)
							insertColumns.Add(newColumns[i].Name);
					}
					commands.Add($"INSERT INTO '{tableName}' ({String.Join(", ", insertColumns)}) SELECT {String.Join(", ", insertColumns)} FROM '{tableName}_temp';");

					// drop old local table
					commands.Add($"DROP TABLE '{tableName}_temp';");  // no IF EXISTS - at this point the table must exist or error
				}
				else if (newColumns.Count > 0) // there are no columns, which have to be deleted - check now if there are new columns to add
				{
					// todo: rk primary key column changed
					foreach (var column in newColumns)
					{
						var commandText = new StringBuilder("ALTER TABLE ");
						AppendSqlIdentifier(commandText, tableName);
						commandText.Append(" ADD COLUMN ");
						AppendSqlIdentifier(commandText, column.Name);
						commandText.Append(' ').Append(ConvertDataTypeToSqLite(column.DataType));
						CreateCommandColumnAttribute(commandText, column);
						commands.Add(commandText.ToString());
					}
				}
				else
					throw new InvalidOperationException();

				return true;
			}
			else
				return false;
		} // proc CreateAlterTableScript

		private static void CreateDropScript(List<string> commands, string tableName)
		{
			commands.Add($"DROP TABLE IF EXISTS '{tableName}';");
		} // proc CreateDropScript

		private static void CreateTableIndex(List<string> commands, string tableName, string columnName, bool isUnique, string[] localIndexArray)
		{
			var commandText = new StringBuilder("CREATE");
			if (isUnique)
				commandText.Append(" UNIQUE");
			commandText.Append(" INDEX ");

			var baseName = tableName + "_" + columnName + "_index";
			var indexName = baseName;
			if (localIndexArray != null)
			{
				var nameIndex = 1;
				while (Array.Exists(localIndexArray, c => String.Compare(c, indexName, StringComparison.OrdinalIgnoreCase) == 0))
					indexName = baseName + (nameIndex++).ToString();
			}

			AppendSqlIdentifier(commandText, indexName);
			commandText.Append(" ON ");
			AppendSqlIdentifier(commandText, tableName);
			commandText.Append(" (");
			AppendSqlIdentifier(commandText, columnName);
			commandText.Append(");");

			commands.Add(commandText.ToString());
		} // proc CreateSqLiteIndex

		private static StringBuilder CreateCommandColumnAttribute(StringBuilder commandText, IDataColumn column)
		{
			// not? null
			if (!column.Attributes.GetProperty("IsNull", false))
				commandText.Append(" NOT");
			commandText.Append(" NULL");

			// append default
			if (!String.IsNullOrEmpty(column.Attributes.GetProperty("Default", String.Empty)))
				commandText.Append(" DEFAULT ").Append("'").Append(column.Attributes.GetProperty("Default", String.Empty)).Append("'");

			return commandText;
		} // func CreateCommandColumnAttribute

		private static StringBuilder AppendSqlIdentifier(StringBuilder commandText, string name)
			=> commandText.Append('[').Append(name).Append(']');

		#endregion

		#region -- FetchData ------------------------------------------------------------

		#region -- class ProcessBatch -----------------------------------------------

		private sealed class ProcessBatch : IDisposable
		{
			private readonly PpsMasterData masterData;
			private readonly PpsDataTableDefinition table;
			private readonly SQLiteConnection connection;
			private readonly SQLiteTransaction transaction;
			private readonly bool isFull;

			private readonly int physPrimaryColumnIndex;
			private readonly int virtPrimaryColumnIndex;
			private readonly SQLiteCommand existCommand;
			private readonly SQLiteParameter existIdParameter;

			private readonly SQLiteCommand insertCommand;
			private readonly SQLiteParameter[] insertParameters;

			private readonly SQLiteCommand updateCommand;
			private readonly SQLiteParameter[] updateParameters;

			private readonly SQLiteCommand deleteCommand;
			private readonly SQLiteParameter deleteIdParameter;

			private readonly int refreshColumnIndex = -1;

			#region -- Ctor/Dtor ----------------------------------------------------

			public ProcessBatch(SQLiteConnection connection, SQLiteTransaction transaction, PpsMasterData masterData, string tableName, bool isFull)
			{
				this.masterData = masterData;
				this.connection = connection;
				this.transaction = transaction;
				this.isFull = isFull;

				// check definition
				this.table = masterData.schema.FindTable(tableName);
				if (table == null)
					throw new ArgumentOutOfRangeException(nameof(tableName), tableName, $"Could not find master table '{tableName}.'");

				var physPrimaryKey = table.PrimaryKey;
				if (physPrimaryKey == null)
					throw new ArgumentException($"Table '{table.Name}' has no primary key.", nameof(physPrimaryKey));

				var alternativePrimaryKey = table.Meta.GetProperty<string>("useAsKey", null);
				var virtPrimaryKey = String.IsNullOrEmpty(alternativePrimaryKey) ? table.PrimaryKey : table.Columns[alternativePrimaryKey];

				refreshColumnIndex = table.FindColumnIndex(refreshColumnName);

				// prepare column parameter
				insertCommand = new SQLiteCommand(connection) { Transaction = transaction };
				updateCommand = new SQLiteCommand(connection) { Transaction = transaction };
				insertParameters = new SQLiteParameter[table.Columns.Count];
				updateParameters = new SQLiteParameter[table.Columns.Count];

				physPrimaryColumnIndex = -1;
				virtPrimaryColumnIndex = -1;
				for (var i = 0; i < table.Columns.Count; i++)
				{
					var column = table.Columns[i];
					var syncSourceColumn = column.Meta.GetProperty("syncSource", String.Empty);
					if (syncSourceColumn == "#")
					{
						if (column == physPrimaryKey)
							throw new ArgumentException($"Primary column '{column.Name}' is not in sync list.");
						if (column == virtPrimaryKey)
							throw new ArgumentException($"Alternative primary column '{column.Name}' is not in sync list.");

						// exclude from update list
						insertParameters[i] = null;
						updateParameters[i] = null;
					}
					else
					{
						if (column == physPrimaryKey)
							physPrimaryColumnIndex = i;
						if (column == virtPrimaryKey)
							virtPrimaryColumnIndex = i;

						insertParameters[i] = insertCommand.Parameters.Add("@" + column.Name, ConvertDataTypeToDbType(column.DataType));
						insertParameters[i].SourceColumn = column.Name;
						updateParameters[i] = updateCommand.Parameters.Add("@" + column.Name, ConvertDataTypeToDbType(column.DataType));
						updateParameters[i].SourceColumn = column.Name;
					}
				}

				// prepare insert, update
				bool excludeNull(SQLiteParameter p)
					=> p != null;

				string insertColumnList()
				{
					var t = String.Join(", ", insertParameters.Where(excludeNull).Select(c => "[" + c.SourceColumn + "]"));
					if (refreshColumnIndex >= 0)
						t += ",[" + refreshColumnName + "]";
					return t;
				}

				string insertValueList()
				{
					var t = String.Join(", ", insertParameters.Where(excludeNull).Select(c => c.ParameterName));
					if (refreshColumnIndex >= 0)
						t += ",0";
					return t;
				}

				string updateColumnValueList()
				{
					var t = String.Join(", ", updateParameters.Where(excludeNull).Where(c => c != updateParameters[virtPrimaryColumnIndex]).Select(c => "[" + c.SourceColumn + "] = " + c.ParameterName));
					if (refreshColumnIndex >= 0)
						t += ",[" + refreshColumnName + "]=IFNULL([" + refreshColumnName + "], 0)";
					return t;
				}

				insertCommand.CommandText =
					"INSERT INTO main.[" + table.Name + "] (" + insertColumnList() + ") " +
					"VALUES (" + insertValueList() + ");";

				updateCommand.CommandText = "UPDATE main.[" + table.Name + "] SET " +
					updateColumnValueList() +
					" WHERE [" + updateParameters[virtPrimaryColumnIndex].SourceColumn + "] = " + updateParameters[virtPrimaryColumnIndex].ParameterName;

				// prepare exists
				existCommand = new SQLiteCommand("SELECT EXISTS(SELECT * FROM main.[" + table.Name + "] WHERE [" + virtPrimaryKey.Name + "] = @Id)", connection, transaction);
				existIdParameter = existCommand.Parameters.Add("@Id", ConvertDataTypeToDbType(virtPrimaryKey.DataType));

				// prepare delete
				deleteCommand = new SQLiteCommand("DELETE FROM main.[" + table.Name + "] WHERE [" + physPrimaryKey.Name + "] = @Id;", connection, transaction);
				deleteIdParameter = deleteCommand.Parameters.Add("@Id", ConvertDataTypeToDbType(physPrimaryKey.DataType));

				existCommand.Prepare();
				insertCommand.Prepare();
				updateCommand.Prepare();
				deleteCommand.Prepare();
			} // ctor

			public void Dispose()
			{
				existCommand?.Dispose();
				insertCommand?.Dispose();
				updateCommand?.Dispose();
				deleteCommand?.Dispose();
			} // proc Dispose

			#endregion

			#region -- Parse --------------------------------------------------------

			public void Prepare()
			{
				// clear table, is full mode
				if (isFull)
				{
					if (refreshColumnIndex == -1)
					{
						using (var cmd = new SQLiteCommand($"DELETE FROM main.[{table.Name}]", connection, transaction))
							cmd.ExecuteNonQueryEx();
					}
					else
					{
						using (var cmd = new SQLiteCommand($"UPDATE main.[{table.Name}] SET [" + refreshColumnName + "] = null WHERE [" + refreshColumnName + "] <> 1", connection, transaction))
							//using (var cmd = new SQLiteCommand($"DELETE FROM main.[{table.Name}] WHERE [" + refreshColumnName + "] <> 1", connection, transaction))
							cmd.ExecuteNonQueryEx();
					}
				}
			} // proc Prepare

			public void Clean()
			{
				if (isFull && refreshColumnIndex >= 0)
				{
					using (var cmd = new SQLiteCommand($"DELETE FROM main.[{table.Name}] WHERE [" + refreshColumnName + "] is null", connection, transaction))
						cmd.ExecuteNonQueryEx();
				}
			} // proc Clean

			public void Parse(XmlReader xml, IProgress<string> progress)
			{
				var objectCounter = 0;
				var lastProgress = Environment.TickCount;

				while (xml.NodeType == XmlNodeType.Element)
				{
					if (xml.IsEmptyElement) // skip empty element
					{
						xml.Read();
						continue;
					}

					// action to process
					var actionName = xml.LocalName.ToLower();
					if (actionName != "r"
						&& actionName != "u"
						&& actionName != "i"
						&& actionName != "d"
						&& actionName != "syncid")
						throw new InvalidOperationException($"The operation {actionName} is not supported.");

					if (actionName == "syncid")
					{
						#region -- update SyncState --
						xml.Read(); // read element

						var newSyncId = xml.GetElementContent<long>(-1);
						if (newSyncId == -1)
						{
							using (var cmd = new SQLiteCommand("DELETE FROM main.[SyncState] WHERE [Table] = @Table", connection, transaction))
							{
								cmd.AddParameter("@Table", DbType.String, table.Name);
								cmd.ExecuteNonQueryEx();
							}
						}
						else
						{
							using (var cmd = new SQLiteCommand(
								"INSERT OR REPLACE INTO main.[SyncState] ([Table], [SyncId]) " +
								"VALUES (@Table, @SyncId);", connection, transaction))
							{
								cmd.AddParameter("@Table", DbType.String, table.Name);
								cmd.AddParameter("@SyncId", DbType.Int64, newSyncId);
								cmd.ExecuteNonQueryEx();
							}
						}
						#endregion
					}
					else
					{
						#region -- upsert --
						if (isFull)
							actionName = refreshColumnIndex == -1 ? "i" : "r";

						// clear current column set
						for (var i = 0; i < updateParameters.Length; i++)
						{
							if (updateParameters[i] != null)
								updateParameters[i].Value = DBNull.Value;
							if (insertParameters[i] != null)
								insertParameters[i].Value = DBNull.Value;
						}
						existIdParameter.Value = DBNull.Value;
						deleteIdParameter.Value = DBNull.Value;

						// collect columns
						xml.Read();
						while (xml.NodeType == XmlNodeType.Element)
						{
							if (xml.IsEmptyElement) // read column data
								xml.Read();
							else
							{
								var columnName = xml.LocalName;
								if (columnName.StartsWith("c") && Int32.TryParse(columnName.Substring(1), out var columnIndex))
								{
									xml.Read();

									var value = ConvertStringToSQLiteValue(xml.ReadContentAsString(), updateParameters[columnIndex].DbType);
									updateParameters[columnIndex].Value = value;
									insertParameters[columnIndex].Value = value;

									if (columnIndex == virtPrimaryColumnIndex)
										existIdParameter.Value = value;
									if (columnIndex == physPrimaryColumnIndex)
										deleteIdParameter.Value = value;

									xml.ReadEndElement();
								}
								else
									xml.Skip();
							}
						}

						// process action
						switch (actionName[0])
						{
							case 'r':
								if (RowExists())
									goto case 'u';
								else
									goto case 'i';
							case 'i':
								ExecuteCommand(insertCommand);
								masterData.environment.OnMasterDataRowChanged(PpsDataChangeOperation.Insert, table, existIdParameter.Value, new SqLiteParameterDictionaryWrapper(updateParameters));
								break;
							case 'u':
								ExecuteCommand(updateCommand);
								masterData.environment.OnMasterDataRowChanged(PpsDataChangeOperation.Update, table, existIdParameter.Value, new SqLiteParameterDictionaryWrapper(updateParameters));
								break;
							case 'd':
								ExecuteCommand(deleteCommand);
								masterData.environment.OnMasterDataRowChanged(PpsDataChangeOperation.Delete, table, deleteIdParameter.Value, new SqLiteParameterDictionaryWrapper(updateParameters));
								break;
						}

						objectCounter++;
						if (progress != null && unchecked(Environment.TickCount - lastProgress) > 500)
						{
							progress.Report(String.Format(Resources.MasterDataFetchSyncString, table.Name + " (" + objectCounter.ToString("N0") + ")"));
							lastProgress = Environment.TickCount;
						}

						#endregion
					}

					xml.ReadEndElement();
				}
				if (objectCounter > 0)
					Trace.TraceInformation($"Synchonization of {table.Name} finished ({objectCounter:N0} objects.");
			} // proc Parse

			private bool RowExists()
			{
				using (var r = existCommand.ExecuteReaderEx(CommandBehavior.SingleRow))
				{
					if (r.Read())
						return r.GetBoolean(0);
					else
					{
						var exc = new ArgumentException();
						exc.Data.Add("SQL-Command", existCommand.CommandText);
						throw exc;
					}
				}
			} // func RowExists

			private void ExecuteCommand(SQLiteCommand command)
			{
					command.ExecuteNonQueryEx();
			} // proc ExecuteCommand

			#endregion

			public PpsDataTableDefinition Table => table;
		} // class ProcessBatch

		#endregion

		private void WriteCurentSyncState(XmlWriter xml)
		{
			xml.WriteStartElement("sync");
			if (lastSynchronizationStamp > DateTime.MinValue)
				xml.WriteAttributeString("lastSyncTimeStamp", lastSynchronizationStamp.ToFileTimeUtc().ChangeType<string>());

			using (var cmd = new SQLiteCommand("SELECT [Table], [SyncId] FROM main.[SyncState]", connection))
			using (var r = cmd.ExecuteReaderEx(CommandBehavior.SingleResult))
			{
				while (r.Read())
				{
					if (!r.IsDBNull(1))
					{
						xml.WriteStartElement("sync");
						xml.WriteAttributeString("table", r.GetString(0));
						xml.WriteAttributeString("syncId", r.GetInt64(1).ChangeType<string>());
						xml.WriteEndElement();
					}
				}
			}

			xml.WriteEndElement();
		} // proc WriteCurentSyncState

		private async Task FetchDataAsync(IProgress<string> progess = null)
		{
			// create request
			var requestString = "/remote/wpf/?action=mdata";

			// parse and process result
			using (var xml = environment.Request.GetXmlStream(await environment.Request.PutXmlResponseAsync(requestString, MimeTypes.Text.Xml, WriteCurentSyncState)))
			{
				xml.ReadStartElement("mdata");
				if (!xml.IsEmptyElement)
				{
					// read batches
					while (xml.NodeType == XmlNodeType.Element)
					{
						switch (xml.LocalName)
						{
							case "batch":
								FetchDataXmlBatch(xml, progess);
								break;
							case "syncStamp":
								var timeStamp = xml.ReadElementContent<long>(-1);

								using (var cmd = new SQLiteCommand("UPDATE main.Header SET SyncStamp = IFNULL(@syncStamp, SyncStamp)", connection))
								{
									cmd.Parameters.Add("@syncStamp", DbType.Int64).Value = timeStamp.DbNullIf(-1L);

									cmd.ExecuteNonQueryEx();
									if (timeStamp >= 0)
										lastSynchronizationStamp = DateTime.FromFileTimeUtc(timeStamp);
								}
								break;
							default:
								xml.Skip();
								break;
						}
					}
				}
			}
			isSynchronizationStarted = true;
		} // proc FetchDataAsync

		private void FetchDataXmlBatch(XmlReader xml, IProgress<string> progress)
		{
			// read batch attributes
			var tableName = xml.GetAttribute("table");
			var isFull = xml.GetAttribute("isFull", false);

			progress?.Report(String.Format(Resources.MasterDataFetchSyncString, tableName));

			if (!xml.IsEmptyElement) // batch needs rows
			{
				xml.Read(); // fetch element
							// process values
				using (var transaction = connection.BeginTransaction())
				using (var b = new ProcessBatch(connection, transaction, this, tableName, isFull))
				{
					// prepare table
					b.Prepare();

					// parse data
					b.Parse(xml, progress);

					b.Clean();
					transaction.Commit();

					// run outsite the transaction
					environment.OnMasterDataTableChanged(b.Table);
				}

				xml.ReadEndElement();
			}
			else // fetch element
				xml.Read();
		} // proc FetchDataXmlBatch

		#endregion

		#region -- Synchronization ------------------------------------------------------

		public Task StartSynchronization()
		{
			var progressTracer = environment.Traces.TraceProgress();
			return Task.Run(new Action(SynchronizationAsync(progressTracer).Wait))
			.ContinueWith(t =>
			{
				try
				{
					t.Wait();
				}
				catch (Exception e)
				{
					progressTracer.Except(e);
					throw;
				}
				progressTracer.Dispose();
			});
		} // proc StartSynchronization


		internal async Task<bool> SynchronizationAsync(IProgress<string> progress)
		{
			// synchronize schema
			if (schemaIsOutDated.HasValue || await CheckSynchronizationStateAsync())
			{
				if (schemaIsOutDated.Value)
					await UpdateSchemaAsync(progress);
				schemaIsOutDated = false;
			}

			progress?.Report("Synchronization...");

			// Fetch data
			environment.OnBeforeSynchronization();
			isInSynchronization = true;
			try
			{
				await FetchDataAsync(progress);
			}
			finally
			{
				isInSynchronization = false;
				environment.OnAfterSynchronization();
			}
			return true;
		} // func SynchronizationAsync

		/// <summary>Tests, if the synchronization needs to be in foreground (last sync it to far away e.g. 1 day)</summary>
		/// <returns></returns>
		internal async Task<bool> CheckSynchronizationStateAsync()
		{
			// check if schema is change
			var schemaUri = environment.ActiveDataSets.GetDataSetSchemaUri(MasterDataSchema);
			var request = WebRequest.Create(environment.Request.GetFullUri(schemaUri));
			request.Method = "HEAD";

			using (var r = await request.GetResponseAsync())
			{
				if (r.GetLastModified().ToUniversalTime() != lastSynchronizationSchema)
				{
					schemaIsOutDated = true;
					return true;
				}
				else
					schemaIsOutDated = false;
			}

			// is the system "synchrone enough"?
			return schemaIsOutDated.Value || (DateTime.UtcNow - lastSynchronizationStamp) > TimeSpan.FromDays(1);
		} // proc CheckSynchronizationStateAsync

		internal void CheckOfflineCache()
		{
			using (var cmd = connection.CreateCommand())
			{
				cmd.CommandText = "SELECT Path FROM main.OfflineCache " +
					"WHERE ContentType IS NULL OR " +
					"IFNULL(LocalContentSize,-2) <> ServerContentSize OR " +
					"LocalContentLastModification is null OR " +
					"LocalContentLastModification <> ServerContentLastModification";

				using (var r = cmd.ExecuteReaderEx(CommandBehavior.SingleResult))
				{
					while (r.Read())
					{
						var path = r.GetString(0);
						var request = environment.GetProxyRequest(new Uri(path, UriKind.Relative));
						request.SetUpdateOfflineCache(c => UpdateOfflineData(path, c));
						request.Enqueue(PpsLoadPriority.Background, true);
					}
				}
			}
		} // proc CheckOfflineCache

		public bool IsInSynchronization => isInSynchronization;

		#endregion

		#region -- Offline Data ---------------------------------------------------------

		#region -- class PpsLocalStoreRequest -------------------------------------------

		private sealed class PpsLocalStoreRequest : WebRequest
		{
			private readonly Uri originalUri;
			private readonly Uri requestUri;
			private readonly MemoryStream content;
			private readonly string localPath;
			private readonly string contentType;
			private readonly bool isCompressed;

			private readonly Func<WebResponse> getResponse;

			public PpsLocalStoreRequest(Uri originalUri, Uri requestUri, MemoryStream content, string localPath, string contentType, bool isCompressed)
			{
				if (content == null && String.IsNullOrEmpty(localPath))
					throw new ArgumentNullException(nameof(content));

				this.requestUri = requestUri ?? throw new ArgumentNullException(nameof(requestUri));
				if (requestUri.IsAbsoluteUri)
					throw new ArgumentNullException("Uri must be relative.", nameof(requestUri));

				this.originalUri = originalUri ?? throw new ArgumentNullException(nameof(originalUri));
				if (!originalUri.IsAbsoluteUri)
					throw new ArgumentNullException("Uri must be original.", nameof(originalUri));

				this.content = content;
				this.localPath = localPath;

				this.contentType = contentType ?? throw new ArgumentNullException(nameof(contentType));
				this.isCompressed = isCompressed;

				this.getResponse = GetResponse;
			} // ctor

			public override IAsyncResult BeginGetRequestStream(AsyncCallback callback, object state)
				=> throw new NotSupportedException();

			public override Stream EndGetRequestStream(IAsyncResult asyncResult)
				=> throw new NotSupportedException();

			public override Stream GetRequestStream()
				=> throw new NotSupportedException();

			public override IAsyncResult BeginGetResponse(AsyncCallback callback, object state)
				=> getResponse.BeginInvoke(callback, state);

			public override WebResponse EndGetResponse(IAsyncResult asyncResult)
				=> getResponse.EndInvoke(asyncResult);

			public override WebResponse GetResponse()
				=> new PpsLocalStoreResponse(originalUri, CreateContentStream(), contentType, isCompressed);

			private Stream CreateContentStream()
			{
				var src = content ?? (Stream)new FileStream(localPath, FileMode.Open, FileAccess.Read);
				if (isCompressed)
					src = new GZipStream(src, CompressionMode.Decompress, false);

				return src;
			} // func CreateContentStream

			public override Uri RequestUri => originalUri;
		} // class PpsLocalStoreRequest

		#endregion

		#region -- class PpsLocalStoreResponse ------------------------------------------

		private sealed class PpsLocalStoreResponse : WebResponse
		{
			private readonly Uri responeUri;
			private readonly Stream content;
			private readonly string contentType;
			private readonly bool isCompressed;

			private readonly WebHeaderCollection headers = new WebHeaderCollection();

			public PpsLocalStoreResponse(Uri responseUri, Stream content, string contentType, bool isCompressed)
			{
				this.responeUri = responseUri ?? throw new ArgumentNullException(nameof(responseUri));
				this.content = content ?? throw new ArgumentNullException(nameof(content));
				this.contentType = contentType ?? throw new ArgumentNullException(nameof(contentType));
				this.isCompressed = isCompressed;

				if (!content.CanRead)
					throw new ArgumentException();
			} // ctor

			protected override void Dispose(bool disposing)
			{
				// dispose content stream
				content?.Dispose();

				base.Dispose(disposing);
			} // proc Dispose

			public override Stream GetResponseStream()
				=> content;

			public override string ContentType
			{
				get => contentType;
				set => throw new NotSupportedException();
			} // func ContentType

			public override long ContentLength
			{
				get => content.CanSeek ? content.Length : -1;
				set => throw new NotSupportedException();
			} // func ContentLength

			public override WebHeaderCollection Headers => headers;

			public override bool SupportsHeaders => false;

			public override Uri ResponseUri => responeUri;
		} // class PpsLocalStoreResponse

		#endregion

		internal string GetLocalPath(string relativePath)
			=> Path.Combine(environment.LocalPath.FullName, relativePath);

		private bool MoveReader(SQLiteDataReader r, Uri uri)
		{
			(var path, var arguments) = uri.ParseUri();

			while (r.Read())
			{
				var testUri = new Uri(r.GetString(0), UriKind.Relative);

				// get query is only allowed for absolute queries, so we scan for ?
				if (testUri.OriginalString.IndexOf('?') == -1 && arguments.Count == 0) // path is exact
				{
					if (String.Compare(path, testUri.ParsePath(), StringComparison.OrdinalIgnoreCase) == 0)
						return true;
				}
				else if (arguments.Count > 0)
				{
					var testArguments = testUri.ParseQuery();
					var failed = false;
					foreach (var c in arguments.AllKeys)
					{
						var testValue = testArguments[c];
						if (testValue == null || String.Compare(testValue, arguments[c], StringComparison.OrdinalIgnoreCase) != 0)
						{
							failed = true;
							break;
						}
					}
					if (!failed)
						return true; // all arguments are fit
				}
			}
			return false;
		} // func MoveReader

		internal bool TryGetOflineCacheFile(Uri requestUri, out IPpsProxyTask task)
		{
			try
			{
				using (var command = new SQLiteCommand("SELECT [Path], [ContentType], [ContentEncoding], [Content], [LocalPath] FROM [main].[OfflineCache] WHERE substr([Path], 1, length(@path)) = @path", connection))
				{
					command.Parameters.Add("@path", DbType.String).Value = requestUri.ParsePath();
					using (var reader = command.ExecuteReaderEx(CommandBehavior.SingleRow))
					{
						if (!MoveReader((SQLiteDataReader)reader, requestUri))
							goto NoResult;

						// check proxy for download process
						if (environment.WebProxy.TryGet(requestUri, out task))
							return true;

						// check content type
						var contentType = reader.IsDBNull(1) ? String.Empty : reader.GetString(1);
						if (String.IsNullOrEmpty(contentType))
							goto NoResult;

						var readContentEncoding = reader.IsDBNull(2) ?
							new string[0] :
							reader.GetString(2).Split(';');

						if (readContentEncoding.Length > 0 && !String.IsNullOrEmpty(readContentEncoding[0]))
							contentType = contentType + ";charset=" + readContentEncoding[0];

						var isCompressedContent = readContentEncoding.Length > 1 && readContentEncoding[1] == "gzip"; // compression is marked on index 1
						task = PpsDummyProxyHelper.GetProxyTask(
							new PpsLocalStoreRequest(
								new Uri(environment.BaseUri, requestUri),
								requestUri,
								(MemoryStream)reader.GetStream(3), // This method returns a newly created MemoryStream object.
								reader.IsDBNull(4) ? null : GetLocalPath(reader.GetString(4)),
								contentType,
								isCompressedContent
							)
						);
						return true;
					} // using reader
				} // using command
			} // try
			catch (Exception e)
			{
				environment.Traces.AppendException(e, String.Format("Failed to resolve offline item with path \"{0}\".", requestUri.ToString()));
			} // catch e

			NoResult:
			// no result
			task = null;
			return false;
		} // func TryGetOfflineCacheFile

		private Stream UpdateOfflineData(string path, IPpsOfflineItemData item)
		{
			Stream outputStream = item.Content;

			if (String.IsNullOrEmpty(path))
				throw new ArgumentNullException(nameof(path));
			if (item == null)
				throw new ArgumentNullException(nameof(item));

			using (var transaction = connection.BeginTransaction())
			{
				if (String.IsNullOrEmpty(item.ContentType))
					throw new ArgumentNullException(nameof(item.ContentType));

				// update data base
				using (var command = new SQLiteCommand(
						"UPDATE [main].[OfflineCache] " +
							"SET [ContentType] = @contentType, " +
								"[ContentEncoding] = @contentEncoding, " +
								"[LocalContentSize] = @contentSize, " +
								"[LocalContentLastModification] = @lastModified, " +
								"[Content] = @content, " +
								"[LocalPath] = @LocalPath " +
							"WHERE [Path] = @path;", connection, transaction
						)
					)
				{

					command.Parameters.Add("@path", DbType.String).Value = path;
					command.Parameters.Add("@contentType", DbType.String).Value = item.ContentType; // split mime from rest
					command.Parameters.Add("@contentEncoding", DbType.String).Value = DBNull.Value;
					command.Parameters.Add("@contentSize", DbType.Int32).Value = item.ContentLength;
					command.Parameters.Add("@lastModified", DbType.DateTime).Value = item.LastModification;
					var parameterContent = command.Parameters.Add("@content", DbType.Binary);
					var parameterLocalPath = command.Parameters.Add("@LocalPath", DbType.String);

					if (item.ContentLength > 1 << 20) // create a link
					{
						var relativePath = Path.Combine("data", Guid.NewGuid().ToString("N"));
						var fileInfo = new FileInfo(GetLocalPath(relativePath));
						if (!fileInfo.Directory.Exists)
							fileInfo.Directory.Create();

						if (item.Content is IInternalFileCacheStream fcs)
						{
							fcs.MoveTo(fileInfo.FullName);
							// dispose is done in moveto
						}
						else
						{
							using (var dst = fileInfo.Create())
								item.Content.CopyTo(dst);
							item.Content.Dispose();
						}

						parameterContent.Value = DBNull.Value;
						parameterLocalPath.Value = relativePath;

						// switch stream
						outputStream = fileInfo.OpenRead();
					}
					else // simple data into an byte array
					{
						var contentBytes = item.Content.ReadInArray();
						parameterContent.Value = contentBytes;
						parameterLocalPath.Value = DBNull.Value;

						item.Content.Position = 0; // move to first byte

						if (item.ContentLength > 0 && item.ContentLength != contentBytes.Length)
							throw new ArgumentOutOfRangeException("content", String.Format("Expected {0:N0} bytes, but received {1:N0} bytes.", item.ContentLength, contentBytes.Length));
					}

					var affectedRows = command.ExecuteNonQueryEx();
					if (affectedRows != 1)
					{
						var exc = new Exception(String.Format("The insert of item \"{0}\" affected an unexpected number ({1}) of rows.", path, affectedRows));
						exc.Data["CommandText"] = command.CommandText;
						throw exc;
					}
				}

				transaction.Commit();
			} // transaction

			return outputStream;
		} // func UpdateOfflineData

		#endregion

		#region -- Write Access ---------------------------------------------------------

		public DbCommand CreateNativeCommand(string commandText)
			=> new SQLiteCommand(commandText, connection, null);

		public PpsMasterDataTransaction CreateTransaction(PpsMasterDataTransaction transaction = null)
			=> PpsMasterDataTransaction.Create(connection, transaction);

		#endregion

		public bool IsSynchronizationStarted => isSynchronizationStarted;
		[Obsolete("ConnectionAccess")]
		public SQLiteConnection Connection => connection;

		// -- Static ------------------------------------------------------
		
		#region -- Read/Write Schema ------------------------------------------------

		internal static XElement ReadSchemaValue(IDataReader r, int columnIndex)
		{
			using (var sr = new StringReader(r.GetString(columnIndex)))
				return XDocument.Load(sr).Root;
		} // func ReadSchemaValue

		#endregion

		#region -- Local store primitives -------------------------------------------

		// according to https://www.sqlite.org/datatype3.html there are only these datatypes - so map everything to these 5 - but we can define new

		private static (Type Type, string SqlLite, DbType DbType)[] sqlLiteTypeMapping = 
		{
			(typeof(bool), "Boolean", DbType.Boolean),
			(typeof(DateTime), "DateTime", DbType.DateTime),

			(typeof(sbyte), "Int8", DbType.SByte),
			(typeof(short), "Int16", DbType.Int16),
			(typeof(int), "Int32", DbType.Int32),
			(typeof(long), "Int64", DbType.Int64),
			(typeof(byte), "UInt8", DbType.Byte),
			(typeof(ushort), "UInt16", DbType.UInt16),
			(typeof(uint), "UInt32", DbType.UInt32),
			(typeof(ulong), "UInt64", DbType.UInt64),

			(typeof(float), "Float", DbType.Single),
			(typeof(double), "Double", DbType.Double),
			(typeof(decimal), "Decimal", DbType.Decimal),

			(typeof(string), "Text", DbType.String),
			(typeof(Guid), "Guid", DbType.Guid),
			(typeof(byte[]), "Blob", DbType.Binary),
			// alt
			(typeof(long), "Integer", DbType.Int64)
		};

		private static Type ConvertSqLiteToDataType(string dataType)
			=> String.IsNullOrEmpty(dataType)
				? typeof(string)
				:
					(
						from c in sqlLiteTypeMapping
						where String.Compare(c.SqlLite, dataType, StringComparison.OrdinalIgnoreCase) == 0
						select c.Type
					).FirstOrDefault() ?? throw new ArgumentOutOfRangeException("type", $"No c# type assigned for '{dataType}'.");

		private static int FindSqlLiteTypeMappingByType(Type type)
			=> Array.FindIndex(sqlLiteTypeMapping, c => c.Type == type);

		private static string ConvertDataTypeToSqLite(Type type)
		{
			var index = FindSqlLiteTypeMappingByType(type);
			return index >= 0 ? sqlLiteTypeMapping[index].SqlLite : throw new ArgumentOutOfRangeException("type", $"No sqlite type assigned for '{type.Name}'.");
		} // func ConvertDataTypeToSqLite

		private static DbType ConvertDataTypeToDbType(Type type)
		{
			var index = FindSqlLiteTypeMappingByType(type);
			return index >= 0 ? sqlLiteTypeMapping[index].DbType : throw new ArgumentOutOfRangeException("type", $"No DbType type assigned for '{type.Name}'.");
		} // func ConvertDataTypeToDbType

		private static object ConvertStringToSQLiteValue(string value, DbType type)
		{
			var index = Array.FindIndex(sqlLiteTypeMapping, c => c.DbType == type);
			return index >= 0
				? Procs.ChangeType(value, sqlLiteTypeMapping[index].Type)
				: throw new ArgumentOutOfRangeException(nameof(type), type, $"DB-Type {type} is not supported.");
		} // func ConvertStringToSQLiteValue

		internal static bool CheckLocalTableExists(SQLiteConnection connection, string tableName)
		{
			using (var command = new SQLiteCommand("SELECT [tbl_name] FROM [sqlite_master] WHERE [type] = 'table' AND [tbl_name] = @tableName;", connection))
			{
				command.Parameters.Add("@tableName", DbType.String, tableName.Length + 1).Value = tableName;
				using (var r = command.ExecuteReaderEx(CommandBehavior.SingleRow))
					return r.Read();
			}
		} // func CheckLocalTableExistsAsync

		internal static IEnumerable<IDataColumn> GetLocalTableColumns(SQLiteConnection connection, string tableName)
		{
			using (var command = new SQLiteCommand($"PRAGMA table_info({tableName});", connection))
			{
				using (var r = command.ExecuteReaderEx(CommandBehavior.SingleResult))
				{
					while (r.Read())
					{
						yield return new SimpleDataColumn(
							r.GetString(1),
							r.IsDBNull(2) ? typeof(string) : ConvertSqLiteToDataType(r.GetString(2)),
							new PropertyDictionary(
								new PropertyValue("IsNull", r.IsDBNull(3) || !r.GetBoolean(3)),
								new PropertyValue("Default", r.GetValue(4)?.ToString()),
								new PropertyValue("IsPrimary", !r.IsDBNull(5) && r.GetBoolean(5))
							)
						);
					}
				}
			}
		} // func GetLocalTableColumns

		internal static IEnumerable<Tuple<string, bool>> GetLocalTableIndexes(SQLiteConnection connection, string tableName)
		{
			using (var command = new SQLiteCommand($"PRAGMA index_list({tableName});", connection))
			{
				using (var r = command.ExecuteReaderEx(CommandBehavior.SingleResult))
				{
					const int indexName = 1;
					const int indexIsUnique = 2;

					while (r.Read())
					{
						yield return new Tuple<string, bool>(
							r.GetString(indexName),
							r.GetBoolean(indexIsUnique)
						);
					}
				}
			}
		} // func GetLocalTableIndexes

		internal static bool TestTableColumns(SQLiteConnection connection, string tableName, params SimpleDataColumn[] columns)
		{
			using (var e = GetLocalTableColumns(connection, tableName).GetEnumerator())
			{
				for (var i = 0; i < columns.Length; i++)
				{
					if (!e.MoveNext())
						return false;

					var textColumn = e.Current;
					var expectedColumn = columns[i];
					if (String.Compare(textColumn.Name, expectedColumn.Name, StringComparison.OrdinalIgnoreCase) != 0)
						return false;
					else if (textColumn.DataType != expectedColumn.DataType)
						return false;
				}
			}
			return true;
		} // func TestLocalTableColumns

		#endregion
	} // class PpsMasterData

	#endregion

	#region -- enum PpsLoadState --------------------------------------------------------

	public enum PpsLoadState
	{
		Pending,
		Started,
		Finished,
		Canceled,
		Failed
	} // enum PpsWebLoadState

	#endregion

	#region -- interface IPpsProxyTask --------------------------------------------------

	[EditorBrowsable(EditorBrowsableState.Advanced)]
	public interface IPpsOfflineItemData
	{
		/// <summary>Access to the content</summary>
		Stream Content { get; }
		/// <summary>Content type</summary>
		string ContentType { get; }
		/// <summary>Expected content length</summary>
		long ContentLength { get; }
		/// <summary>Last modification time stamp.</summary>
		DateTime LastModification { get; }
	} // interface IPpsOfflineItemData

	public interface IPpsProxyTask : INotifyPropertyChanged
	{
		/// <summary></summary>
		/// <param name="response"></param>
		void AppendResponseSink(Action<WebResponse> response);

		/// <summary>Processes the request in the forground (change priority to first).</summary>
		/// <returns></returns>
		Task<WebResponse> ForegroundAsync();
		/// <summary>Task to watch the download process.</summary>
		Task<WebResponse> Task { get; }

		/// <summary>State of the download progress</summary>
		PpsLoadState State { get; }
		/// <summary>Download state of the in percent.</summary>
		int Progress { get; }
		/// <summary>Displayname that will be shown in the ui.</summary>
		string DisplayName { get; }
	} // interface IPpsProxyTask

	#endregion

	#region -- class PpsDummyProxyHelper ------------------------------------------------

	public static class PpsDummyProxyHelper
	{
		private sealed class PpsDummyProxyTask : IPpsProxyTask
		{
			event PropertyChangedEventHandler INotifyPropertyChanged.PropertyChanged { add { } remove { } }

			private readonly WebRequest request;
			private bool responseCalled;

			public PpsDummyProxyTask(WebRequest request)
				=> this.request = request;

			private WebRequest InitResponse()
			{
				if (responseCalled)
					throw new InvalidOperationException();

				responseCalled = true;
				return request;
			} // proc InitResponse

			public void AppendResponseSink(Action<WebResponse> response)
				=> response(InitResponse().GetResponse());

			public Task<WebResponse> ForegroundAsync()
				=> InitResponse().GetResponseAsync();

			public void SetUpdateOfflineCache(Func<IPpsOfflineItemData, Stream> updateOfflineCache)
				=> throw new NotSupportedException();

			public Task<WebResponse> Task => InitResponse().GetResponseAsync();

			public PpsLoadState State => PpsLoadState.Started;
			public int Progress => -1;
			public string DisplayName => PpsWebProxy.GetDisplayNameFromRequest(request);
		} // class PpsDummyProxyTask

		public static IPpsProxyTask GetProxyTask(this WebRequest request, PpsLoadPriority priority = PpsLoadPriority.Default)
		   => request is PpsProxyRequest p
			   ? p.Enqueue(priority)
			   : new PpsDummyProxyTask(request);
	} // class PpsDummyProxyHelper

	#endregion

	#region -- class PpsProxyRequest ----------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsProxyRequest : WebRequest, IEquatable<PpsProxyRequest>
	{
		private readonly PpsEnvironment environment; // owner, that retrieves a resource
		private readonly Uri originalUri;
		private readonly Uri relativeUri; // relative Uri

		private readonly bool offlineOnly;
		private bool aborted = false; // is the request cancelled

		private readonly Func<WebResponse> procGetResponse; // async GetResponse
		private readonly Func<Stream> procGetRequestStream; // async

		private WebHeaderCollection headers;
		private string path;
		private NameValueCollection arguments;

		private string method = HttpMethod.Get.Method;
		private string contentType = null;
		private long contentLength = -1;

		private Func<IPpsOfflineItemData, Stream> updateOfflineCache = null;
		private MemoryStream requestStream = null;

		#region -- Ctor/Dtor ------------------------------------------------------------

		internal PpsProxyRequest(PpsEnvironment environment, Uri originalUri, Uri relativeUri, bool offlineOnly)
		{
			this.environment = environment;
			this.originalUri = originalUri ?? throw new ArgumentNullException(nameof(originalUri));
			this.relativeUri = relativeUri ?? throw new ArgumentNullException(nameof(relativeUri));
			this.offlineOnly = offlineOnly;

			if (relativeUri.IsAbsoluteUri)
				throw new ArgumentException("Uri must be relative.", nameof(relativeUri));
			if (!originalUri.IsAbsoluteUri)
				throw new ArgumentException("Uri must be absolute.", nameof(originalUri));

			this.procGetResponse = GetResponse;
			this.procGetRequestStream = GetRequestStream;

			(path, arguments) = relativeUri.ParseUri();
		} // ctor

		public bool Equals(PpsProxyRequest other)
			=> Equals(other.relativeUri);

		public bool Equals(Uri otherUri)
			=> WebRequestHelper.EqualUri(relativeUri, otherUri);

		#endregion

		#region -- GetResponse ----------------------------------------------------------

		/// <summary>Handles the request async</summary>
		/// <param name="callback"></param>
		/// <param name="state"></param>
		/// <returns></returns>
		public override IAsyncResult BeginGetResponse(AsyncCallback callback, object state)
		{
			if (aborted)
				throw new WebException("Canceled", WebExceptionStatus.RequestCanceled);

			return procGetResponse.BeginInvoke(callback, state);
		} // func BeginGetResponse

		/// <summary></summary>
		/// <param name="asyncResult"></param>
		/// <returns></returns>
		public override WebResponse EndGetResponse(IAsyncResult asyncResult)
			=> procGetResponse.EndInvoke(asyncResult);

		/// <summary></summary>
		/// <returns></returns>
		public override WebResponse GetResponse()
		{
			if (HasRequestData) // we have request data, execute always online
				return InternalGetResponse();
			else if (environment.TryGetOfflineObject(this, out var task)) // check if the object is local available, cached
				return task.ForegroundAsync().Result; // block thread
			else
				return InternalGetResponse();
		} // func GetResponse

		public override Task<WebResponse> GetResponseAsync()
		{
			if (HasRequestData) // we have request data, execute always online
				return InternalGetResponseAsync();
			else if (environment.TryGetOfflineObject(this, out var task)) // check if the object is local available, cached
				return task.ForegroundAsync();
			else
				return InternalGetResponseAsync();
		} // func GetResponse

		public IPpsProxyTask Enqueue(PpsLoadPriority priority, bool forceOnline = false)
		{
			// check for offline item
			if (!forceOnline && updateOfflineCache == null && environment.TryGetOfflineObject(this, out var task1))
				return task1;
			else if (!HasRequestData && updateOfflineCache == null && environment.WebProxy.TryGet(this, out var task2)) // check for already existing task
				return task2;
			else // enqueue the new task
				return environment.WebProxy.Append(this, priority);
		} // func Enqueue

		private WebRequest GetOnlineRequest()
		{
			var onlineRequest = environment.CreateOnlineRequest(relativeUri);

			onlineRequest.Method = method;
			if (contentLength > 0)
				onlineRequest.ContentLength = contentLength;
			if (contentType != null)
				onlineRequest.ContentType = contentType;

			// copy headers
			if (headers != null)
			{
				foreach (var k in headers.AllKeys)
					onlineRequest.Headers[k] = headers[k];
			}

			// request data
			if (HasRequestData)
			{
				using (var dst = onlineRequest.GetRequestStream())
					RequestData.CopyTo(dst);
			}

			return onlineRequest;
		} // func GetOnlineRequest

		internal WebResponse InternalGetResponse()
			=> GetOnlineRequest().GetResponse();

		private Task<WebResponse> InternalGetResponseAsync()
			=> GetOnlineRequest().GetResponseAsync();

		#endregion

		#region -- GetRequestStream -----------------------------------------------------

		public override IAsyncResult BeginGetRequestStream(AsyncCallback callback, object state)
			=> procGetRequestStream.BeginInvoke(callback, state);

		public override Stream EndGetRequestStream(IAsyncResult asyncResult)
			=> procGetRequestStream.EndInvoke(asyncResult);

		public override Stream GetRequestStream()
		{
			if (offlineOnly)
				throw new ArgumentException("Request data is not allowed in offline mode.");

			if (requestStream == null)
				requestStream = new MemoryStream();
			return requestStream;
		} // func GetRequestStream

		#endregion

		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public void SetUpdateOfflineCache(Func<IPpsOfflineItemData, Stream> updateOfflineCache)
		{
			this.updateOfflineCache = updateOfflineCache ?? throw new ArgumentNullException(nameof(updateOfflineCache));
		} // proc SetUpdateOfflineCache

		internal Stream UpdateOfflineCache(IPpsOfflineItemData data)
			=> updateOfflineCache?.Invoke(data) ?? data.Content;

		/// <summary></summary>
		internal bool HasRequestData => requestStream != null;
		/// <summary></summary>
		internal Stream RequestData => requestStream;

		public override string Method { get => method; set => method = value; }
		public override string ContentType { get => contentType; set => contentType = value; }
		public override long ContentLength { get => contentLength; set => contentLength = value; }

		public PpsEnvironment Environment => environment;

		public override Uri RequestUri => originalUri;

		public override IWebProxy Proxy { get => null; set { } } // avoid NotImplementedExceptions

		/// <summary>Arguments of the request</summary>
		public NameValueCollection Arguments => arguments;
		/// <summary>Relative path for the request.</summary>
		public string Path => path;
		/// <summary>Header</summary>
		public override WebHeaderCollection Headers { get => headers ?? (headers = new WebHeaderCollection()); set => headers = value; }
	} // class PpsProxyRequest

	#endregion

	#region -- class PpsWebProxy --------------------------------------------------------

	public sealed class PpsWebProxy : IEnumerable<IPpsProxyTask>, INotifyCollectionChanged, IDisposable
	{
		#region -- class MemoryCacheStream ----------------------------------------------

		private sealed class MemoryCacheStream : Stream
		{
			private readonly long expectedLength;
			private readonly MemoryStream nestedMemoryStream;

			public MemoryCacheStream(long expectedLength)
			{
				this.expectedLength = expectedLength;
				this.nestedMemoryStream = new MemoryStream(unchecked((int)(expectedLength > 0 ? expectedLength : 4096)));
			} // ctor

			public override void Flush()
				=> nestedMemoryStream.Flush();

			public override void SetLength(long value)
				=> throw new NotSupportedException();

			public override long Seek(long offset, SeekOrigin origin)
				=> nestedMemoryStream.Seek(offset, origin);

			public override int Read(byte[] buffer, int offset, int count)
				=> nestedMemoryStream.Read(buffer, offset, count);

			public override void Write(byte[] buffer, int offset, int count)
				=> nestedMemoryStream.Write(buffer, offset, count);

			public override bool CanRead => true;
			public override bool CanWrite => true;
			public override bool CanSeek => true;

			public override long Position { get => nestedMemoryStream.Position; set => nestedMemoryStream.Position = value; }
			public override long Length => nestedMemoryStream.Length;

			public long ExpectedLength => expectedLength;
		} // class MemoryCacheStream

		#endregion

		#region -- class FileCacheStream ------------------------------------------------

		private sealed class FileCacheStream : Stream, IInternalFileCacheStream
		{
			private readonly string fileName;
			private readonly long expectedLength;
			private readonly FileStream nestedFileStream;

			private long currentLength = 0L;

			public FileCacheStream(long expectedLength)
			{
				this.fileName = Path.GetTempFileName();
				this.expectedLength = expectedLength;
				this.nestedFileStream = new FileStream(fileName, FileMode.Create);

				if (expectedLength > 0)
					nestedFileStream.SetLength(expectedLength);
			} // ctor

			public FileCacheStream(MemoryCacheStream copyFrom, long expectedLength)
				: this(expectedLength)
			{
				// copy stream
				copyFrom.Position = 0;
				copyFrom.CopyTo(this);
			} // ctor

			protected override void Dispose(bool disposing)
			{
				if (File.Exists(fileName))
				{
					try { File.Delete(fileName); }
					catch { }
				}
				base.Dispose(disposing);
			} // proc Dispose

			public void MoveTo(string targetFileName)
			{
				nestedFileStream.Dispose(); // close stream

				File.Move(fileName, targetFileName);
			} // proc MoveTo

			public override void Flush()
				=> nestedFileStream.Flush();

			public override int Read(byte[] buffer, int offset, int count)
				=> nestedFileStream.Read(buffer, offset, count);

			public override void Write(byte[] buffer, int offset, int count)
			{
				var appendOperation = nestedFileStream.Position == currentLength;
				nestedFileStream.Write(buffer, offset, count);

				if (appendOperation)
					currentLength += count;
			} // proc Write

			public override long Seek(long offset, SeekOrigin origin)
				=> Seek(offset, origin);

			public override void SetLength(long value)
				=> throw new NotSupportedException();

			public override bool CanRead => true;
			public override bool CanSeek => true;
			public override bool CanWrite => true;

			public override long Length => currentLength;

			public override long Position { get => nestedFileStream.Position; set => nestedFileStream.Position = value; }
		} // class FileCacheStream

		#endregion

		#region -- class CacheResponseStream --------------------------------------------

		private sealed class CacheResponseStream : Stream
		{
			private readonly Stream resultStream;
			private long position = 0L;

			public CacheResponseStream(Stream resultStream)
			{
				this.resultStream = resultStream;
			} // ctor

			private void EnsurePosition()
			{
				if (resultStream.Position != position)
					resultStream.Position = position;
			}

			public override void Flush() { }

			public override int Read(byte[] buffer, int offset, int count)
			{
				lock (resultStream)
				{
					EnsurePosition();
					var readed = resultStream.Read(buffer, offset, count);
					position += readed;
					return readed;
				}
			} // func Read

			public override long Seek(long offset, SeekOrigin origin)
			{
				long getNewPosition()
				{
					switch (origin)
					{
						case SeekOrigin.Begin:
							return offset;
						case SeekOrigin.Current:
							return position + offset;
						case SeekOrigin.End:
							return Length - position;
						default:
							throw new ArgumentOutOfRangeException(nameof(origin));
					}
				}

				var newPosition = getNewPosition();
				if (newPosition < 0 || newPosition > Length)
					throw new ArgumentOutOfRangeException(nameof(offset));

				return position = newPosition;
			} // func Seek

			public override void SetLength(long value)
				=> throw new NotSupportedException();

			public override void Write(byte[] buffer, int offset, int count)
				=> throw new NotSupportedException();


			public override bool CanRead => true;
			public override bool CanSeek => true;
			public override bool CanWrite => false;


			public override long Position { get => position; set => Seek(value, SeekOrigin.Begin); }
			public override long Length => resultStream.Length;
		} // class CacheResponseStream

		#endregion

		#region -- class CacheResponseProxy ---------------------------------------------

		private sealed class CacheResponseProxy : WebResponse
		{
			private readonly Uri responseUri;
			private readonly Stream resultStream;
			private readonly string contentType;
			private readonly WebHeaderCollection headers;

			public CacheResponseProxy(Uri responseUri, Stream resultStream, string contentType, WebHeaderCollection headers)
			{
				this.responseUri = responseUri;
				this.resultStream = resultStream ?? throw new ArgumentNullException(nameof(headers));
				this.contentType = contentType ?? throw new ArgumentNullException(nameof(headers));
				this.headers = headers ?? throw new ArgumentNullException(nameof(headers));

				if (!resultStream.CanSeek)
					throw new ArgumentException("resultStream is not seekable", nameof(resultStream));
				if (!resultStream.CanRead)
					throw new ArgumentException("resultStream is not readable", nameof(resultStream));
			} // ctor

			public override Stream GetResponseStream()
				=> new CacheResponseStream(resultStream); // create a new stream

			public override WebHeaderCollection Headers => headers;

			public override long ContentLength { get => resultStream.Length; set => throw new NotSupportedException(); }
			public override string ContentType { get => contentType; set => throw new NotSupportedException(); }

			public override Uri ResponseUri => responseUri;
		} // class CacheResponseProxy

		#endregion

		#region -- class WebLoadRequest -------------------------------------------------

		private sealed class WebLoadRequest : IPpsProxyTask
		{
			#region -- class PpsOfflineItemDataImplementation ---------------------------

			private sealed class PpsOfflineItemDataImplementation : IPpsOfflineItemData
			{
				private readonly Stream data;
				private readonly string contentType;
				private readonly DateTime lastModification;

				public PpsOfflineItemDataImplementation(Stream data, string contentType, DateTime lastModification)
				{
					this.data = data;
					this.contentType = contentType;
					this.lastModification = lastModification;
				} // ctor

				public Stream Content => data;
				public string ContentType => contentType;
				public long ContentLength => data.Length;
				public DateTime LastModification => lastModification;
			} // class PpsOfflineItemDataImplementation

			#endregion

			private const long tempFileBorder = 10 << 20;

			public event PropertyChangedEventHandler PropertyChanged;

			private readonly PpsWebProxy manager;
			private readonly PpsLoadPriority priority;
			private readonly PpsProxyRequest request;

			private readonly List<Action<WebResponse>> webResponseSinks = new List<Action<WebResponse>>();
			private readonly TaskCompletionSource<WebResponse> task;

			private readonly object stateLock = new object();
			private PpsLoadState currentState = PpsLoadState.Pending;
			private int progress = -1;

			private CacheResponseProxy resultResponse = null;
			private Exception resultException = null;

			public WebLoadRequest(PpsWebProxy manager, PpsLoadPriority priority, PpsProxyRequest request)
			{
				this.manager = manager;
				this.priority = priority;
				this.request = request;

				this.task = new TaskCompletionSource<WebResponse>();
			} // ctor

			public bool IsSameRequest(PpsProxyRequest request)
				=> this.request.Equals(request);

			public bool IsSameRequest(Uri requestUri)
				=> this.request.Equals(requestUri);

			public void AppendResponseSink(Action<WebResponse> response)
			{
				lock (stateLock)
				{
					if (State == PpsLoadState.Finished)
						response(resultResponse);
					else if (State == PpsLoadState.Canceled)
						throw new OperationCanceledException("Response aborted.");
					else if (State == PpsLoadState.Failed)
						throw new Exception("Repsonse failed.", resultException);
					else if (webResponseSinks.IndexOf(response) == -1)
						webResponseSinks.Add(response);
				}
			} // proc AppendResponseSink

			public Task<WebResponse> ForegroundAsync()
			{
				lock (stateLock)
				{
					if (currentState == PpsLoadState.Pending)
						manager.MoveToForeground(this);
				}
				return Task;
			} // func ForegroundAsync

			private Stream CreateCacheStream(long contentLength)
				=> contentLength > tempFileBorder // create a temp file
					? (Stream)new FileCacheStream(contentLength)
					: new MemoryCacheStream(contentLength);

			internal void Execute()
			{
				lock (stateLock)
					UpdateState(PpsLoadState.Started);
				try
				{
					// is the request data
					using (var response = request.InternalGetResponse())
					{
						// cache the header information
						var contentLength = response.ContentLength;
						var contentType = response.ContentType;
						var headers = new WebHeaderCollection();
						foreach (var k in response.Headers.AllKeys)
							headers.Set(k, response.Headers[k]);

						// start the download
						var checkForSwitchToFile = false;
						var dst = CreateCacheStream(contentLength);
						using (var src = response.GetResponseStream())
						{
							try
							{
								var copyBuffer = new byte[4096];
								var readedTotal = 0L;
								checkForSwitchToFile = dst is MemoryCacheStream;

								while (true)
								{
									var readed = src.Read(copyBuffer, 0, copyBuffer.Length);

									UpdateProgress(unchecked((int)(readed * 1000 / contentLength)));
									if (readed > 0)
									{
										dst.Write(copyBuffer, 0, readed);
										readedTotal += readed;
										if (contentLength > readedTotal)
											UpdateProgress(unchecked((int)(readedTotal * 1000 / contentLength)));
										else if (checkForSwitchToFile && readedTotal > tempFileBorder)
										{
											var oldDst = (MemoryCacheStream)dst;
											dst = new FileCacheStream(oldDst, oldDst.ExpectedLength);
											oldDst.Dispose();
										}
									}
									else
										break;
								}

								// process finished
								UpdateState(PpsLoadState.Finished);
								dst.Flush();

								// the cache stream will be disposed by the garbage collector, or if it is moved to the offline cache
								request.UpdateOfflineCache(new PpsOfflineItemDataImplementation(dst, contentType, headers.GetLastModified()));

								// spawn the result functions
								lock (stateLock)
								{
									UpdateState(PpsLoadState.Finished);
									resultResponse = new CacheResponseProxy(request.RequestUri, dst, contentType, headers);
								}
								foreach (var s in webResponseSinks)
									System.Threading.Tasks.Task.Run(() => s(resultResponse));

								// set the result
								task.SetResult(resultResponse);
							}
							catch
							{
								dst.Dispose(); // dispose because error
							}// using src,dst
						}
					} // using response
				}
				catch (TaskCanceledException)
				{
					UpdateState(PpsLoadState.Canceled);
					task.SetCanceled();
				}
				catch (Exception e)
				{
					lock (stateLock)
					{
						UpdateState(PpsLoadState.Failed);
						resultException = e;
					}
					task.SetException(e);
				}
			} // proc Execute

			private void OnPropertyChanged(string propertyName)
				=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

			private void UpdateProgress(int newProgress)
			{
				if (progress != newProgress)
				{
					progress = newProgress;
					OnPropertyChanged(nameof(Progress));
				}
			} // proc UpdateProgress

			private void UpdateState(PpsLoadState newState)
			{
				if (currentState != newState)
				{
					currentState = newState;
					OnPropertyChanged(nameof(State));
				}
			} // proc UpdateState

			public Task<WebResponse> Task => task.Task;
			public PpsLoadState State => currentState;
			public PpsLoadPriority Priority => priority;
			public int Progress => progress;
			public string DisplayName => PpsWebProxy.GetDisplayNameFromRequest(request);
		} // class WebLoadRequest

		#endregion

		public event NotifyCollectionChangedEventHandler CollectionChanged;

		private readonly PpsEnvironment environment;
		private readonly List<WebLoadRequest> downloadList = new List<WebLoadRequest>();
		private int currentForegroundCount = 0;

		private readonly Thread executeLoadQueue;
		private readonly ManualResetEventSlim executeLoadIsRunning = new ManualResetEventSlim(false);
		private bool isDisposed = false;

		public PpsWebProxy(PpsEnvironment environment)
		{
			this.environment = environment;
			this.executeLoadQueue = new Thread(ExecuteLoadQueue)
			{
				Name = "PpsWebProxy",
				IsBackground = true,
			};
			executeLoadQueue.Start();
		} // class PpsDownloadManager

		public void Dispose()
		{
			if (isDisposed)
				throw new ObjectDisposedException(nameof(PpsWebProxy));

			isDisposed = true;
			executeLoadIsRunning.Set();
		} // proc Dispose

		private void OnCollectionChanged()
			=> CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

		/// <summary>Enumerator for the download task.</summary>
		/// <returns></returns>
		/// <remarks>It locks the current process.</remarks>
		public IEnumerator<IPpsProxyTask> GetEnumerator()
		{
			lock (downloadList)
			{
				foreach (var c in downloadList)
					yield return c;
			}
		} // func GetEnumerator

		IEnumerator IEnumerable.GetEnumerator()
			=> GetEnumerator();

		private WebLoadRequest TryDequeueTask()
		{
			lock (downloadList)
			{
				if (downloadList.Count == 0)
				{
					executeLoadIsRunning.Reset();
					return null;
				}
				else
				{
					var r = downloadList[0];
					if (currentForegroundCount == 0)
						currentForegroundCount = 1; // mark as foreground
					return r;
				}
			}
		} // proc TryDequeueTask

		private void RemoveCurrentTask()
		{
			var notifyReset = false;
			lock (downloadList)
			{
				if (currentForegroundCount > 0)
				{
					downloadList.RemoveAt(0);
					currentForegroundCount--;
					notifyReset = true;
				}
			}
			if (notifyReset)
				OnCollectionChanged();
		} // proc RemoveCurrentTask

		private void ExecuteLoadQueue()
		{
			while (!isDisposed)
			{
				var nextTask = TryDequeueTask();
				if (nextTask != null)
				{
					try
					{
						nextTask.Execute();
					}
					catch (Exception e)
					{
						// todo: connect lost?
						environment.ShowExceptionAsync(ExceptionShowFlags.Background, e).Wait();
					}
					finally
					{
						RemoveCurrentTask();
					}
				}

				// wait for next item
				executeLoadIsRunning.Wait();
			}
		} // proc ExecuteLoadQueue

		internal void MoveToForeground(IPpsProxyTask task)
		{
			lock (downloadList)
			{
				var t = (WebLoadRequest)task;
				if (downloadList.Remove(t))
					downloadList.Insert(currentForegroundCount++, t);
			}
			OnCollectionChanged();
		} // proc MoveToForeground

		private IPpsProxyTask AppendTask(WebLoadRequest task)
		{
			try
			{
				lock (downloadList)
				{
					// priority section
					var i = currentForegroundCount;
					while (i < downloadList.Count && downloadList[i].Priority <= task.Priority)
						i++;

					// add at pos
					downloadList.Insert(i, task);
					executeLoadIsRunning.Set();

					return task;
				}
			}
			finally
			{
				OnCollectionChanged();
			}
		} // proc AppendTask

		internal bool TryGet(PpsProxyRequest request, out IPpsProxyTask task)
		{
			// check, request exists
			lock (downloadList)
			{
				task = downloadList.Find(c => c.IsSameRequest(request));
				return task != null;
			}
		} // func TryGet

		public bool TryGet(Uri requestUri, out IPpsProxyTask task)
		{
			// check, request exists
			lock (downloadList)
			{
				task = downloadList.Find(c => c.IsSameRequest(requestUri));
				return task != null;
			}
		} // func TryGet

		internal IPpsProxyTask Append(PpsProxyRequest request, PpsLoadPriority priority)
			=> AppendTask(new WebLoadRequest(this, priority, request));

		// -- Static --------------------------------------------------------------------

		internal static string GetDisplayNameFromRequest(WebRequest request)
			=> request.RequestUri.AbsolutePath;
	} // class PpsDownloadManager

	#endregion

	public partial class PpsEnvironment
	{
		private const string temporaryTablePrefix = "old_";

		#region -- class PpsWebRequestCreate --------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private class PpsWebRequestCreate : IWebRequestCreate
		{
			private readonly WeakReference<PpsEnvironment> environmentReference;

			public PpsWebRequestCreate(PpsEnvironment environment)
			{
				this.environmentReference = new WeakReference<PpsEnvironment>(environment);
			} // ctor

			public WebRequest Create(Uri uri)
			{
				if (environmentReference.TryGetTarget(out var environment))
					return environment.CreateProxyRequest(uri);
				else
					throw new ObjectDisposedException("Environment does not exists anymore.");
			}
		} // class PpsWebRequestCreate

		#endregion

		#region -- class CredentialWrapper ----------------------------------------------

		private sealed class CredentialWrapper : ICredentials
		{
			private readonly NetworkCredential userInfo;

			public CredentialWrapper(NetworkCredential userInfo)
			{
				this.userInfo = userInfo;
			} // ctor

			public NetworkCredential GetCredential(Uri uri, string authType)
			{
				if (userInfo == CredentialCache.DefaultCredentials
					|| userInfo == CredentialCache.DefaultNetworkCredentials)
					return userInfo;
				else if (String.IsNullOrEmpty(userInfo.Domain))
				{
					// force basic, if we have no domain
					return String.Compare(authType, "basic", StringComparison.OrdinalIgnoreCase) == 0 ? userInfo : null;
				}
				else
					return userInfo;
			} // func GetCredential
		} // class CredentialWrapper

		#endregion

		#region -- class KnownDataSetDefinition -----------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class KnownDataSetDefinition
		{
			private readonly PpsEnvironment environment;

			private readonly Type datasetDefinitionType;
			private readonly string schema;
			private readonly string sourceUri;

			private PpsDataSetDefinitionDesktop definition = null;

			public KnownDataSetDefinition(PpsEnvironment environment, Type datasetDefinitionType, string schema, string sourceUri)
			{
				this.environment = environment;
				this.datasetDefinitionType = datasetDefinitionType;
				this.schema = schema;
				this.sourceUri = sourceUri;
			} // ctor

			public async Task<PpsDataSetDefinitionDesktop> GetDocumentDefinitionAsync()
			{
				if (definition != null)
					return definition;

				// load the schema
				var xSchema = await environment.Request.GetXmlAsync(sourceUri);
				definition = (PpsDataSetDefinitionDesktop)Activator.CreateInstance(datasetDefinitionType, environment, schema, xSchema);
				definition.EndInit();

				return definition;
			} // func GetDocumentDefinitionAsync

			public Type DataSetDefinitionType => datasetDefinitionType;
			public string Schema => schema;
			public string SourceUri => sourceUri;
		} // class KnownDataSetDefinition

		#endregion

		#region -- class PpsActiveDataSetsImplementation --------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private class PpsActiveDataSetsImplementation : List<PpsDataSetDesktop>, IPpsActiveDataSets
		{
			private readonly PpsEnvironment environment;
			private readonly Dictionary<string, KnownDataSetDefinition> datasetDefinitions = new Dictionary<string, KnownDataSetDefinition>(StringComparer.OrdinalIgnoreCase);

			public PpsActiveDataSetsImplementation(PpsEnvironment environment)
			{
				this.environment = environment;
			} // ctor

			public bool RegisterDataSetSchema(string schema, string uri, Type datasetDefinitionType)
			{
				lock (datasetDefinitions)
				{
					KnownDataSetDefinition definition;
					if (!datasetDefinitions.TryGetValue(schema, out definition) || // definition is not registered
						definition.Schema != schema || definition.SourceUri != uri) // or registration has changed
					{
						datasetDefinitions[schema] = new KnownDataSetDefinition(environment, datasetDefinitionType ?? typeof(PpsDataSetDefinitionDesktop), schema, uri);
						return true;
					}
					else
						return false;
				}
			} // proc IPpsActiveDataSets.RegisterDataSetSchema

			public void UnregisterDataSetSchema(string schema)
			{
				lock (datasetDefinitions)
					datasetDefinitions.Remove(schema);
			} // proc UnregisterDataSetSchema

			public string GetDataSetSchemaUri(string schema)
			{
				lock (datasetDefinitions)
				{
					KnownDataSetDefinition definition;
					return datasetDefinitions.TryGetValue(schema, out definition) ? definition.SourceUri : null;
				}
			} // func GetDataSetSchemaUri

			public Task<PpsDataSetDefinitionDesktop> GetDataSetDefinitionAsync(string schema)
			{
				KnownDataSetDefinition definition;
				lock (datasetDefinitions)
				{
					if (!datasetDefinitions.TryGetValue(schema, out definition))
						return Task.FromResult<PpsDataSetDefinitionDesktop>(null);
				}
				return definition.GetDocumentDefinitionAsync();
			} // func GetDataSetDefinition

			public IEnumerable<T> GetKnownDataSets<T>(string schema = null)
				where T : PpsDataSetDesktop
			{
				foreach (var c in this)
				{
					var ds = c as T;
					if (ds != null && (schema == null || ((PpsDataSetDefinitionDesktop)ds.DataSetDefinition).SchemaType == schema))
						yield return ds;
				}
			} // func GetKnownDataSets

			public IEnumerable<string> KnownSchemas
			{
				get
				{
					lock (datasetDefinitions)
						return datasetDefinitions.Keys.ToArray();
				}
			} // prop KnownSchemas
		} // class PpsActiveDataSetsImplementation

		#endregion

		private PpsMasterData masterData;   // local datastore
		private PpsWebProxy webProxy;       // remote download/upload manager
		private readonly Uri baseUri;       // internal uri for this datastore
		private ProxyStatus statusOfProxy;  // interface for the transaction manager

		private readonly BaseWebRequest request;

		#region -- Init -----------------------------------------------------------------

		private Task<string> GetLocalStorePassword()
		{
			var passwordFile = Path.Combine(LocalPath.FullName, "localStore.dat");
			return File.Exists(passwordFile)
				? ReadPasswordFile(passwordFile)
				: CreatePasswordFile(passwordFile, 256);
		} // func GetLocalStorePassword

		#region -- Passwording ----------------------------------------------------------

		public Task<string> ReadPasswordFile(string fileName)
			=> Task.Run(() => PpsProcs.StringDecypher(File.ReadAllText(fileName)));

		public Task<string> CreatePasswordFile(string fileName, int passwordLength, byte passwordLowerBoundary = 32, byte passwordUpperBoundary = 126)
		{
			var passwordChars = String.Empty;
			for (var i = passwordLowerBoundary; i <= passwordUpperBoundary; i++)
				passwordChars += (char)i;
			return CreatePasswordFile(fileName, passwordLength, passwordChars.ToCharArray());
		} // func CreatePasswordFile

		public Task<string> CreatePasswordFile(string fileName, int passwordLength, char[] passwordValidChars)
		{
			if (File.Exists(fileName))
				File.Delete(fileName);
			File.WriteAllText(fileName, PpsProcs.StringCypher(PpsProcs.GeneratePassword(passwordLength, passwordValidChars)));
			return ReadPasswordFile(fileName);
		} // func CreatePasswordFile

		#endregion

		/// <summary></summary>
		/// <returns><c>true</c>, if a valid database is present.</returns>
		private async Task<bool> InitLocalStoreAsync(IProgress<string> progress)
		{
			var isDataUseable = false;
			var isSchemaUseable = false;
			// open a new local store
			SQLiteConnection newLocalStore = null;
			PpsDataSetDefinitionDesktop newDataSet = null;
			DateTime? lastSynchronizationSchema = null;
			DateTime? lastSynchronizationStamp = null;
			try
			{
				// open the local database
				progress.Report("Lokale Datenbank öffnen...");
				var dataPath = Path.Combine(LocalPath.FullName, "localStore.db");
				newLocalStore = new SQLiteConnection($"Data Source={dataPath};DateTimeKind=Utc"); // foreign keys=true;Password=Pps{GetLocalStorePassword()}
				await newLocalStore.OpenAsync();

				// check synchronisation table
				progress.Report("Lokale Datenbank verifizieren...");
				if (PpsMasterData.TestTableColumns(newLocalStore, "Header",
					new SimpleDataColumn("SchemaStamp", typeof(long)),
					new SimpleDataColumn("SchemaContent", typeof(byte[])),
					new SimpleDataColumn("SyncStamp", typeof(long))
					))
				{
					// read sync tokens
					using (var commd = new SQLiteCommand("SELECT SchemaStamp, SchemaContent, SyncStamp FROM main.Header ", newLocalStore))
					{
						using (var r = commd.ExecuteReaderEx(CommandBehavior.SingleRow))
						{
							if (r.Read())
							{
								// check schema
								if (!r.IsDBNull(0) && !r.IsDBNull(1))
								{
									lastSynchronizationSchema = DateTime.FromFileTimeUtc(r.GetInt64(0));
									newDataSet = new PpsDataSetDefinitionDesktop(this, PpsMasterData.MasterDataSchema, PpsMasterData.ReadSchemaValue(r, 1));
									isSchemaUseable = true;
								}
								// check data
								if (!r.IsDBNull(2) && !r.IsDBNull(2))
								{
									lastSynchronizationStamp = DateTime.FromFileTimeUtc(r.GetInt64(2));
									isDataUseable = true;
								}
							}
						}
					}
				}

				// reset values
				if (!isSchemaUseable)
					lastSynchronizationSchema = DateTime.MinValue;
				if (!isDataUseable)
					lastSynchronizationStamp = DateTime.MinValue;
			}
			catch
			{
				newLocalStore?.Dispose();
				throw;
			}

			// close current connection
			masterData?.Dispose();

			// set new connection
			masterData = new PpsMasterData(this, newLocalStore, newDataSet, lastSynchronizationSchema.Value, lastSynchronizationStamp.Value);

			Trace.WriteLine($"[MasterData] Create with Schema: {lastSynchronizationSchema.Value}; SyncStamp: {lastSynchronizationStamp.Value}; ==> Use Schema={isSchemaUseable}, Use Data={isDataUseable}");

			return isDataUseable && isSchemaUseable;
		} // proc InitLocalStore

		private Uri InitProxy()
		{
			// register proxy for the web requests
			var baseUri = new Uri($"http://ppsn{environmentId}.local");
			WebRequest.RegisterPrefix(baseUri.ToString(), new PpsWebRequestCreate(this));
			return baseUri;
		} // func InitProxy

		#endregion

		#region -- Web Request ----------------------------------------------------------

		/// <summary>Core function that gets called on a request.</summary>
		/// <param name="uri"></param>
		/// <returns></returns>
		private WebRequest CreateProxyRequest(Uri uri)
		{
			if (!uri.IsAbsoluteUri)
				throw new ArgumentException("Uri must absolute.", nameof(uri));

			const string localPrefix = "/local/";
			const string remotePrefix = "/remote/";

			var useOfflineRequest = CurrentMode == PpsEnvironmentMode.Offline;
			var useCache = true;
			var absolutePath = uri.AbsolutePath;

			// is the local data prefered
			if (absolutePath.StartsWith(localPrefix))
			{
				absolutePath = absolutePath.Substring(localPrefix.Length);
				useOfflineRequest = true;
			}
			else if (absolutePath.StartsWith(remotePrefix))
			{
				absolutePath = absolutePath.Substring(remotePrefix.Length);
				useOfflineRequest = false;
				useCache = false;
			}
			else if (absolutePath.StartsWith("/")) // if the uri starts with "/", remove it, because the info.remoteUri is our root
			{
				absolutePath = absolutePath.Substring(1);
			}

			// create a relative uri
			var relativeUri = new Uri(absolutePath + uri.GetComponents(UriComponents.Query | UriComponents.KeepDelimiter, UriFormat.UriEscaped), UriKind.Relative);

			// create the request proxy
			if (useCache || useOfflineRequest)
				return new PpsProxyRequest(this, uri, relativeUri, useOfflineRequest);
			else
				return CreateOnlineRequest(relativeUri);
		} // func CreateWebRequest

		/// <summary>Is used only internal to create the real request.</summary>
		/// <param name="relativeUri"></param>
		/// <param name="absolutePath"></param>
		/// <returns></returns>
		internal WebRequest CreateOnlineRequest(Uri relativeUri)
		{
			if (relativeUri.IsAbsoluteUri)
				throw new ArgumentException("Uri must be relative.", nameof(relativeUri));
			if (relativeUri.OriginalString.StartsWith("/"))
				relativeUri = new Uri(relativeUri.OriginalString.Substring(1), UriKind.Relative);

			// build the remote request with absolute uri and credentials
			var absoluteUri = new Uri(info.Uri, relativeUri);
			var request = WebRequest.Create(absoluteUri);
			request.Credentials = new CredentialWrapper(userInfo); // override the current credentials

			Debug.Print($"WebRequest: {absoluteUri}");

			return request;
		} // func CreateOnlineRequest

		public PpsProxyRequest GetProxyRequest(string path)
			=> GetProxyRequest(new Uri(path, UriKind.Relative));

		/// <summary>Starts a request through the proxy.</summary>
		/// <param name="uri"></param>
		/// <param name="priority"></param>
		/// <returns></returns>
		public PpsProxyRequest GetProxyRequest(Uri uri)
			=> new PpsProxyRequest(this, new Uri(BaseUri, uri), uri, CurrentState == PpsEnvironmentState.Offline);

		protected internal virtual bool TryGetOfflineObject(WebRequest request, out IPpsProxyTask task)
		{
			return masterData.TryGetOflineCacheFile(BaseUri.MakeRelativeUri(request.RequestUri), out task);
		} // func TryGetOfflineObject

		#endregion

		#region -- GetViewData ----------------------------------------------------------

		/// <summary></summary>
		/// <param name="arguments"></param>
		/// <returns></returns>
		public virtual IEnumerable<IDataRow> GetViewData(PpsShellGetList arguments)
			=> GetRemoteViewData(arguments);

		protected IEnumerable<IDataRow> GetRemoteViewData(PpsShellGetList arguments)
		{
			if (arguments.ViewId.StartsWith("local.", StringComparison.OrdinalIgnoreCase)) // it references the local db
			{
				if (arguments.ViewId == "local.objects")
					return CreateObjectFilter(arguments);
				else
				{
					var exc = new ArgumentOutOfRangeException();
					exc.Data.Add("Variable", "ViewId");
					exc.Data.Add("Value", arguments.ViewId);
					throw exc;
				}
			}
			else
			{
				var sb = new StringBuilder("remote/?action=viewget&v=");
				sb.Append(arguments.ViewId);

				if (arguments.Filter != null && arguments.Filter != PpsDataFilterTrueExpression.True)
					sb.Append("&f=").Append(Uri.EscapeDataString(arguments.Filter.ToString()));
				if (arguments.Order != null && arguments.Order.Length > 0)
					sb.Append("&o=").Append(Uri.EscapeDataString(PpsDataOrderExpression.ToString(arguments.Order)));
				if (arguments.Start != -1)
					sb.Append("&s=").Append(arguments.Start);
				if (arguments.Count != -1)
					sb.Append("&c=").Append(arguments.Count);
				if (!String.IsNullOrEmpty(arguments.AttributeSelector))
					sb.Append("&a=").Append(arguments.AttributeSelector);

				return Request.CreateViewDataReader(sb.ToString());
			}
		} // func GetRemoteViewData

		#endregion

		protected internal virtual void OnBeforeSynchronization() { }
		protected internal virtual void OnAfterSynchronization() { }

		protected async virtual Task OnSystemOnlineAsync()
		{
			Trace.WriteLine("[Environment] System goes online.");
			masterData.CheckOfflineCache(); // start download

			await RefreshDefaultResourcesAsync();
			await RefreshTemplatesAsync();
		} // proc OnSystemOnline

		protected async virtual Task OnSystemOfflineAsync()
		{
			Trace.WriteLine("[Environment] System goes offline.");
			await RefreshDefaultResourcesAsync();
			await RefreshTemplatesAsync();
		} // proc OnSystemOffline

		/// <summary>Gets called if the local database gets changed.</summary>
		/// <param name="operation"></param>
		/// <param name="table">Table description</param>
		/// <param name="id">Primary key.</param>
		public virtual void OnMasterDataRowChanged(PpsDataChangeOperation operation, PpsDataTableDefinition table, object id, IPropertyReadOnlyDictionary arguments)
		{
		} // proc OnMasterDataChanged

		/// <summary>Gets called if a batch is processed.</summary>
		/// <param name="table"></param>
		public void OnMasterDataTableChanged(PpsDataTableDefinition table)
		{
			if (!masterData.IsInSynchronization && table.Name == "OfflineCache")
			{
				masterData.CheckOfflineCache();
			}
		} // proc OnMasterDataTableChanged

		public async Task<bool> ForceOnlineAsync(bool throwException = true)
		{
			if (CurrentMode == PpsEnvironmentMode.Online)
				return true;
			else if (CurrentMode != PpsEnvironmentMode.Online)
			{
				switch (await WaitForEnvironmentMode(PpsEnvironmentMode.Online))
				{
					case PpsEnvironmentModeResult.Online:
						return true;
				}
			}
			throw new NotImplementedException("Todo: Force online mode.");
		} // func ForceOnlineMode

		/// <summary></summary>
		[LuaMember]
		public BaseWebRequest Request => request;
		/// <summary>Default encodig for strings.</summary>
		public Encoding Encoding => Encoding.Default;
		/// <summary>Internal Uri of the environment.</summary>
		public Uri BaseUri => baseUri;

		public PpsWebProxy WebProxy => webProxy;
		public ProxyStatus StatusOfProxy => statusOfProxy;

		/// <summary>Connection to the local datastore</summary>
		[Obsolete("Use master data.")]
		public SQLiteConnection LocalConnection => masterData.Connection;

		/// <summary>Access to the local store for the synced data.</summary>
		public PpsMasterData MasterData => masterData;
	} // class PpsEnvironment

	// interface Status
	public interface IStatusList : INotifyPropertyChanged
	{
		object ActualItem { get; }
		ObservableCollection<object> TopTen { get; }
	}

	public class ProxyStatus : IStatusList
	{
		private PpsWebProxy proxy;
		private ObservableCollection<object> topTen = new ObservableCollection<object>();
		private IPpsProxyTask actualItem;
		private System.Windows.Threading.Dispatcher dispatcher;

		public ProxyStatus(PpsWebProxy Proxy, System.Windows.Threading.Dispatcher Dispatcher)
		{
			this.proxy = Proxy;
			this.dispatcher = Dispatcher;
			this.proxy.CollectionChanged += WebProxyChanged;
		}

		public event PropertyChangedEventHandler PropertyChanged;
		private void OnPropertyChanged(string propertyName)
		=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		private void WebProxyChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
		{
			dispatcher?.Invoke(() =>
			{
				topTen.Clear();
				using (var walker = proxy.GetEnumerator())
				{
					for (var i = 0; i < 10; i++)
					{
						if (walker.MoveNext())
							if (i == 0)
							{
								actualItem = walker.Current;
								OnPropertyChanged(nameof(actualItem));
							}
							else
								topTen.Insert(0, walker.Current);
						else if (i == 0)
						{
							actualItem = null;
							OnPropertyChanged(nameof(actualItem));
						}
					}
				}
			});
		}

		public object ActualItem => actualItem;
		public ObservableCollection<object> TopTen => topTen;
	}
}
