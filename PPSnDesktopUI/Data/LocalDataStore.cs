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
using System.Collections.Specialized;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Data;
using TecWare.DE.Networking;
using TecWare.DE.Stuff;

namespace TecWare.PPSn.Data
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PpsLocalDataStore : PpsDataStore, IDisposable
	{
		private const string TemporaryTablePrefix = "old_";

		#region -- class PpsStoreCacheRequest ---------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class PpsStoreCacheRequest : PpsStoreRequest
		{
			public PpsStoreCacheRequest(PpsLocalDataStore store, Uri uri, string absolutePath)
				: base(store, uri, absolutePath)
			{
			} // ctor

			public override WebResponse GetResponse()
			{
				string contentType;
				Stream source;

				// is this a static item
				if (DataStore.TryGetOfflineItem(Path, true, out contentType, out source))
				{
					var r = new PpsStoreResponse(this);
					r.SetResponseData(source, contentType);
					return r;
				}
				else if (DataStore.Environment.IsOnline)
				{
					// todo: dynamic cache, copy of properties and headers
					return DataStore.Environment.CreateWebRequestNative(RequestUri, Path).GetResponse();
				}
				else
					throw new WebException("File not found.", null, WebExceptionStatus.ProtocolError, null);
			} // func GetResponse

			public new PpsLocalDataStore DataStore => (PpsLocalDataStore)base.DataStore;
		} // class PpsStoreCacheRequest

		#endregion

		private readonly SQLiteConnection localStore;

		#region -- Ctor/Dtor --------------------------------------------------------------

		public PpsLocalDataStore(PpsEnvironment environment)
			: base(environment)
		{
			try
			{
				// open the local database
				var dataPath = Path.Combine(environment.Info.LocalPath.FullName, "localStore.db");
				localStore = new SQLiteConnection($"Data Source={dataPath};DateTimeKind=Utc");
				localStore.Open();
				VerifyLocalStore();
			}
			catch
			{
				localStore?.Dispose();
				throw;
			}
		} // ctor

		public void Dispose()
			=> Dispose(true);

		protected virtual void Dispose(bool disposing)
		{
			if (disposing)
				localStore?.Dispose();
		} // proc Dispose

		#endregion

		#region -- GetRequest -------------------------------------------------------------

		public WebRequest GetCachedRequest(Uri uri, string absolutePath)
		{
			return new PpsStoreCacheRequest(this, uri, absolutePath);
		} // func GetCacheRequest

		protected override void GetResponseDataStream(PpsStoreResponse r)
		{
			Stream src;
			string contentType;
			
			if (TryGetOfflineItem(r.Request.Path, false, out contentType, out src)) // ask the file from the cache
				r.SetResponseData(src, contentType);
			else
				throw new WebException("File not found.", null, WebExceptionStatus.ProtocolError, r);
		} // proc GetResponseDataStream

		#endregion

		#region -- VerifyLocalStore -------------------------------------------------------

		#region -- enum ParsingState ------------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private enum ParsingState
		{
			Default = 0,
			ReadElementDescription = 1,
			ReadElementContent = 2,
		} // enum ParsingState

		#endregion

		private XElement ParseSQLiteCreateTableCommands(string resourceName)
		{
			var assembly = Assembly.GetExecutingAssembly();
			var resolvedResourceName = assembly.GetManifestResourceNames().First(c => c.EndsWith(String.Format(".{0}", resourceName), StringComparison.Ordinal));
			var command = new XElement(resourceName);

			using (var source = assembly.GetManifestResourceStream(resolvedResourceName))
			using (var reader = new StreamReader(source, Encoding.UTF8))
			{
				var state = ParsingState.Default;
				var lineData = new StringBuilder();
				XElement currentNode = null;

				while (true)
				{
					var line = reader.ReadLine();

					switch (state)
					{
						#region -- ParsingState.Default --
						case ParsingState.Default:
							if (line == null)
								return command;
							else if (line.StartsWith("--<", StringComparison.Ordinal))
							{
								lineData.Append(line.Substring(2));
								state = ParsingState.ReadElementDescription;
							}
							break;
						#endregion
						#region -- ParsingState.ReadElementDescription --
						case ParsingState.ReadElementDescription:
							if (line != null && line.StartsWith("--", StringComparison.Ordinal))
								lineData.Append(line.Substring(2));
							else
							{
								currentNode = XElement.Parse(lineData.ToString());
								command.Add(currentNode);
								lineData = new StringBuilder();
								state = ParsingState.ReadElementContent;
								goto case ParsingState.ReadElementContent;
							}
							break;
						#endregion
						#region -- ParsingState.ReadElementContent --
						case ParsingState.ReadElementContent:
							if (line == null || line.StartsWith("--<", StringComparison.Ordinal))
							{
								currentNode.Add(new XCData(lineData.ToString()));
								lineData = new StringBuilder();
								state = ParsingState.Default;
								goto case ParsingState.Default;
							}
							else if (!line.StartsWith("--", StringComparison.Ordinal))
								lineData.AppendLine(line);
							break;
							#endregion
					} // switch state
				} // while true
			} // using reader source
		} // func ParseSQLiteCreateTableCommands

		private void VerifyLocalStore()
		{
			// todo: use "*.scripts.*" resources, not a static string array

			using (var transaction = localStore.BeginTransaction())
			{
				var existsMetaTable = false;

				var metaTableCommands = ParseSQLiteCreateTableCommands("Meta.sql");

				var metaTableInfo = metaTableCommands.Element("info");
				var metaTableSchema = metaTableInfo.Attribute("schema").Value;
				var metaTableName = metaTableInfo.Attribute("name").Value;

				var metaTableCreate = metaTableCommands.Element("create").Value;

				if (String.Compare(metaTableSchema, "main", StringComparison.OrdinalIgnoreCase) != 0)
					throw new InvalidDataException(String.Format("The schema \"{0}\" for the table \"{1}\" is not supported. Only \"main\" schema is supported!", metaTableSchema, metaTableName));

				using (var command = new SQLiteCommand("SELECT 1 FROM [sqlite_master] WHERE [type] = 'table' AND [tbl_name] = @metaTableName;", localStore, transaction))
				{
					command.Parameters.Add("@metaTableName", DbType.AnsiString).Value = metaTableName;
					using (var reader = command.ExecuteReader())
						existsMetaTable = reader.HasRows;
				}

				if (!existsMetaTable)
				{
					using (var command = new SQLiteCommand(metaTableCreate, localStore, transaction))
						command.ExecuteNonQuery();
				}

				#region -- *.sql --
				foreach (var script in new string[] { "OfflineCache.sql" })
				{
					var existsTable = false;

					var tableCommands = ParseSQLiteCreateTableCommands(script);

					var tableInfo = tableCommands.Element("info");
					var tableSchema = tableInfo.Attribute("schema").Value;
					var tableName = tableInfo.Attribute("name").Value;
					var tableRev = Int64.Parse(tableInfo.Attribute("rev").Value);

					var tableCreate = tableCommands.Element("create").Value;
					var tableConvert = tableCommands.Element("convert").Value;

					if (String.Compare(tableSchema, "main", StringComparison.OrdinalIgnoreCase) != 0)
						throw new InvalidDataException(String.Format("The schema \"{0}\" for the table \"{1}\" is not supported. Only \"main\" schema is supported!", metaTableSchema, metaTableName));

					using (var command = new SQLiteCommand("SELECT 1 FROM [sqlite_master] WHERE [type] = 'table' AND [tbl_name] = @tableName;", localStore, transaction))
					{
						command.Parameters.Add("@tableName", DbType.String).Value = tableName;
						using (var reader = command.ExecuteReader())
							existsTable = reader.HasRows;
					}

					if (!existsMetaTable)
					{
						if (existsTable)
							throw new InvalidDataException(String.Format("The table \"{0}\".\"{1}\" can not be verified, because it was created before the revision table \"{2}\".\"{3}\".", tableSchema, tableName, metaTableSchema, metaTableName));

						using (var command = new SQLiteCommand(tableCreate, localStore, transaction))
							command.ExecuteNonQuery();
					}
					else
					{
						long readRev = -1;

						using (var command = new SQLiteCommand($"SELECT [Rev] FROM [{metaTableSchema}].[{metaTableName}] WHERE [Res] = @resourceName;", localStore, transaction))
						{
							command.Parameters.Add("@resourceName", DbType.String).Value = script;
							using (var reader = command.ExecuteReader())
							{
								var enumerator = reader.GetEnumerator();
								if (!enumerator.MoveNext())
									throw new InvalidDataException(String.Format("There is no entry in the revision table \"{0}\".\"{1}\" for resource \"{2}\".", metaTableSchema, metaTableName, script));

								readRev = reader.GetInt64(0);
							}
						} // using command

						if (!existsTable)
						{
							using (var command = new SQLiteCommand(tableCreate, localStore, transaction))
								command.ExecuteNonQuery();
						}
						else
						{
							if (readRev > tableRev)
								throw new InvalidDataException(String.Format("The table \"{0}\".\"{1}\" can not be verified, because the revision number in the revision table \"{2}\".\"{3}\" is greater than the revision number for resource \"{4}\".", tableSchema, tableName, metaTableSchema, metaTableName, script));
							else if (readRev == tableRev)
								continue; // Matching revision. Skip revision table update.
							else if (readRev < tableRev)
							{
								using (var command = new SQLiteCommand($"DROP TABLE IF EXISTS [{tableSchema}].[{TemporaryTablePrefix}{tableName}];", localStore, transaction))
									command.ExecuteNonQuery();

								using (var command = new SQLiteCommand($"ALTER TABLE [{tableSchema}].[{tableName}] RENAME TO [{TemporaryTablePrefix}{tableName}];", localStore, transaction))
									command.ExecuteNonQuery();

								using (var command = new SQLiteCommand(tableCreate, localStore, transaction))
									command.ExecuteNonQuery();

								using (var command = new SQLiteCommand(tableConvert, localStore, transaction))
									command.ExecuteNonQuery();

								using (var command = new SQLiteCommand($"DROP TABLE IF EXISTS [{tableSchema}].[{TemporaryTablePrefix}{tableName}];", localStore, transaction))
									command.ExecuteNonQuery();
							} // if readRev < tableRev
						} // else
					} // else

					using (var command = new SQLiteCommand($"UPDATE [{metaTableSchema}].[{metaTableName}] SET [Rev] = @tableRev, [ResLastModification] = DATETIME('now') WHERE [Res] = @resourceName;", localStore, transaction))
					{
						command.Parameters.Add("@tableRev", DbType.Int64).Value = tableRev;
						command.Parameters.Add("@resourceName", DbType.String).Value = script;
						var affectedRows = command.ExecuteNonQuery();

						string errorText = null;
						if (affectedRows < 0)
							errorText = String.Format("unknown ({0})", affectedRows);
						else if (affectedRows == 0)
							errorText = "no entry";
						else if (affectedRows > 1)
							errorText = "multiple entries";

						if (errorText != null)
							throw new Exception(String.Format("The update in the revision table \"{0}\".\"{1}\" for resource \"{2}\" failed. Reason: {3}", metaTableSchema, metaTableName, script, errorText));
					} // using command
				} // foreach script
				#endregion

				transaction.Commit();
			} // using transaction
		} // proc VerifyLocalStore

		#endregion

		#region -- Offline Data -----------------------------------------------------------

		public void UpdateOfflineItem(string path, bool onlineMode, string contentType, string contentEncoding, Stream data)
		{
			if (String.IsNullOrEmpty(path))
				throw new ArgumentException("Parameter \"path\" is null or empty.");

			if (String.IsNullOrEmpty(contentType))
				throw new ArgumentException("Parameter \"contentType\" is null or empty.");

			// The usage of contentEncoding null or "" is allowed. Null will be handle as System.DBNull.

			var buffer = data?.ReadInArray();
			if (buffer == null)
				throw new ArgumentException("Parameter \"data\" is null or empty.");

			using (var transaction = localStore.BeginTransaction())
			{
				long? id = null;
				using (var command = new SQLiteCommand("SELECT [Id] FROM [main].[OfflineCache] WHERE [Path] = @path;", localStore))
				{
					command.Parameters.Add("@path", DbType.String).Value = path;
					using (var reader = command.ExecuteReader(CommandBehavior.SingleRow))
					{
						if (reader.Read())
							id = reader.GetInt64(0);
					}
				}

				if (id == null)
				{
					using (var command = new SQLiteCommand("INSERT INTO [main].[OfflineCache] ([Path], [OnlineMode], [ContentType], [ContentEncoding], [ContentSize], [Content]) VALUES(@path, @onlineMode, @contentType, @contentEncoding, @contentSize, @content);", localStore, transaction))
					{
						command.Parameters.Add("@path", DbType.String).Value = path;
						command.Parameters.Add("@onlineMode", DbType.Boolean).Value = onlineMode;
						command.Parameters.Add("@contentType", DbType.String).Value = contentType;
						command.Parameters.Add("@contentEncoding", DbType.String).Value = contentEncoding;
						command.Parameters.Add("@contentSize", DbType.Int32).Value = buffer.Length;
						command.Parameters.Add("@content", DbType.Binary).Value = buffer;
						var affectedRows = command.ExecuteNonQuery();
						if (affectedRows != 1)
							throw new Exception(String.Format("The insert of item \"{0}\" affected an unexpected number ({1}) of rows.", path, affectedRows));
					}
				} // if id == null
				else
				{
					using (var command = new SQLiteCommand("UPDATE [main].[OfflineCache] SET [OnlineMode] = @onlineMode, [ContentType] = @contentType, [ContentEncoding] = @contentEncoding, [ContentSize] = @contentSize, [ContentLastModification] = DATETIME('now'), [Content] = @content WHERE [Id] = @id;", localStore, transaction))
					{
						command.Parameters.Add("@onlineMode", DbType.Boolean).Value = onlineMode;
						command.Parameters.Add("@contentType", DbType.String).Value = contentType;
						command.Parameters.Add("@contentEncoding", DbType.String).Value = contentEncoding;
						command.Parameters.Add("@contentSize", DbType.Int32).Value = buffer.Length;
						command.Parameters.Add("@content", DbType.Binary).Value = buffer;
						command.Parameters.Add("@id", DbType.Int64).Value = id;
						var affectedRows = command.ExecuteNonQuery();
						if (affectedRows != 1)
							throw new Exception(String.Format("The update of item \"{0}\" affected an unexpected number ({1}) of rows.", path, affectedRows));
					}
				} // else

				transaction.Commit();
			} // using transaction
		} // proc UpdateOfflineItem

		public virtual bool TryGetOfflineItem(string path, bool onlineMode, out string contentType, out Stream data)
		{
			contentType = null;
			data = null;

			if (String.IsNullOrEmpty(path))
				return false;

			try
			{
				if (localStore == null || localStore.State != ConnectionState.Open)
					return false;
			}
			catch (ObjectDisposedException)
			{
				return false;
			}

			string resultContentType = null;
			MemoryStream resultData = null;
			try
			{
				using (var command = new SQLiteCommand("SELECT [OnlineMode], [ContentType], [ContentEncoding], [Content] FROM [main].[OfflineCache] WHERE [Path] = @path;", localStore))
				{
					command.Parameters.Add("@path", DbType.String).Value = path;
					using (var reader = command.ExecuteReader(CommandBehavior.SingleRow))
					{
						if (!reader.Read())
							return false;

						var readOnlineMode = reader.GetBoolean(0);
						if (onlineMode && !readOnlineMode) // Verify that the stored item can be used in online mode.
							return false;

						resultContentType = reader.GetString(1);
						if (String.IsNullOrEmpty(resultContentType))
							return false;

						var readContentEncoding = reader.IsDBNull(2) ? null : reader.GetString(2);
						var isCompressedContent = (readContentEncoding != null && readContentEncoding.IndexOf("gzip", StringComparison.OrdinalIgnoreCase) != -1);

						Stream readerData = null;
						try
						{
							readerData = reader.GetStream(3); // This method returns a newly created MemoryStream object.
							if (readerData is MemoryStream)
							{
								if (isCompressedContent)
								{
									resultData = new MemoryStream();
									using (var decompressionData = new GZipStream(readerData, CompressionMode.Decompress)) // The underlying stream gets automatically closed.
										decompressionData.CopyTo(resultData);
								}
								else
									resultData = (MemoryStream)readerData;
							}
							else
							{
								resultData = new MemoryStream();
								if (isCompressedContent)
									using (var decompressionData = new GZipStream(readerData, CompressionMode.Decompress)) // The underlying stream gets automatically closed.
										decompressionData.CopyTo(resultData);
								else
									using (readerData)
										readerData.CopyTo(resultData);
							}
						} // try
						catch
						{
							readerData?.Dispose();
							throw;
						} // catch
					} // using reader
				} // using command
			} // try
			catch (Exception e)
			{
				Environment.Traces.AppendException(e, String.Format("Failed to resolve offline item with path \"{0}\".", path));
				resultData?.Dispose();
				return false;
			} // catch e

			contentType = resultContentType;
			data = resultData;
			return true;
		} // func TryGetOfflineItem

		#region -- GetOfflineItems --------------------------------------------------------

		#region -- class LocalStoreColumn -------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class LocalStoreColumn : IDataColumn
		{
			private readonly string name;
			private readonly Type dataType;
			private readonly IDataColumnAttributes attributes;

			#region -- Ctor/Dtor --------------------------------------------------------------

			public LocalStoreColumn(string name, Type dataType, IDataColumnAttributes attributes)
			{
				this.name = name;
				this.dataType = dataType;
				this.attributes = attributes;
			} // ctor

			#endregion

			#region -- IDataColumn ------------------------------------------------------------

			public string Name => name;
			public Type DataType => dataType;
			public IDataColumnAttributes Attributes => attributes;

			#endregion
		} // class LocalStoreColumn

		#endregion

		#region -- class LocalStoreRow ----------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class LocalStoreRow : IDataRow
		{
			private readonly LocalStoreEnumerator enumerator;
			private readonly object[] values;

			#region -- Ctor/Dtor --------------------------------------------------------------

			public LocalStoreRow(LocalStoreEnumerator enumerator, object[] values)
			{
				this.enumerator = enumerator;
				this.values = values;
			} // ctor

			#endregion

			#region -- IDataRow ---------------------------------------------------------------

			public bool TryGetProperty(string columnName, out object value)
			{
				value = null;

				try
				{
					if (String.IsNullOrEmpty(columnName))
						return false;

					if (Columns == null || Columns.Length < 1)
						return false;

					if (Columns.Length != ColumnCount)
						return false;

					var index = Array.FindIndex(Columns, c => String.Compare(c.Name, columnName, StringComparison.OrdinalIgnoreCase) == 0);
					if (index == -1)
						return false;

					value = this[index];
					return true;
				}
				catch
				{
					return false;
				}
			} // func TryGetProperty

			public object this[int index] => values[index];

			public IDataColumn[] Columns => enumerator.Columns;
			public int ColumnCount => enumerator.ColumnCount;

			public object this[string columnName]
			{
				get
				{
					var index = Array.FindIndex(Columns, c => String.Compare(c.Name, columnName, StringComparison.OrdinalIgnoreCase) == 0);
					if (index == -1)
						throw new ArgumentException(String.Format("Column with name \"{0}\" not found.", columnName ?? "null"));
					return values[index];
				}
			} // prop this

			#endregion
		} // class LocalStoreRow

		#endregion

		#region -- class LocalStoreEnumerator ---------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class LocalStoreEnumerator : IEnumerator<IDataRow>, IDataColumns
		{
			#region -- enum ReadingState ------------------------------------------------------

			///////////////////////////////////////////////////////////////////////////////
			/// <summary></summary>
			private enum ReadingState
			{
				Unread,
				Partly,
				Complete,
			} // enum ReadingState

			#endregion

			private bool disposed;
			private readonly SQLiteCommand command;
			private SQLiteDataReader reader;
			private ReadingState state;
			private IDataRow currentRow;
			private Lazy<IDataColumn[]> columns;

			#region -- Ctor/Dtor --------------------------------------------------------------

			public LocalStoreEnumerator(SQLiteCommand command)
			{
				this.command = command;

				columns = new Lazy<IDataColumn[]>(() =>
				{
					CheckDisposed();

					if (state == ReadingState.Unread && !MoveNext())
						return null;

					if (state != ReadingState.Partly)
						return null;

					var tmp = new LocalStoreColumn[reader.FieldCount];
					for (var i = 0; i < reader.FieldCount; i++)
						tmp[i] = new LocalStoreColumn(reader.GetName(i), reader.GetFieldType(i), null);
					return tmp;
				});
			} // ctor

			public void Dispose()
				=> Dispose(true);

			private void Dispose(bool disposing)
			{
				if (disposed)
					return;

				if (disposing)
				{
					command?.Dispose();
					reader?.Dispose();
				}

				disposed = true;
			} // proc Dispose

			#endregion

			private void CheckDisposed()
			{
				if (disposed)
					throw new ObjectDisposedException(typeof(LocalStoreEnumerator).FullName);
			} // proc CheckDisposed

			#region -- IEnumerator<T> ---------------------------------------------------------

			public bool MoveNext()
			{
				CheckDisposed();

				switch (state)
				{
					case ReadingState.Unread:
						if (reader == null)
							reader = command.ExecuteReader(CommandBehavior.SingleResult);

						if (!reader.Read())
							goto case ReadingState.Complete;

						goto case ReadingState.Partly;
					case ReadingState.Partly:
						if (state == ReadingState.Partly)
						{
							if (!reader.Read())
								goto case ReadingState.Complete;
						}
						else
							state = ReadingState.Partly;

						var values = new object[reader.FieldCount];
						for (var i = 0; i < reader.FieldCount; i++)
							values[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);
						currentRow = new LocalStoreRow(this, values);
						return true;
					case ReadingState.Complete:
						state = ReadingState.Complete;
						// todo: Dispose command and reader?
						currentRow = null;
						return false;
					default:
						throw new InvalidOperationException("The state of the object is invalid.");
				} // switch state
			} // func MoveNext

			void IEnumerator.Reset()
			{
				CheckDisposed();
				if (state != ReadingState.Unread)
					throw new InvalidOperationException("The state of the object forbids the calling of this method.");
			} // proc Reset

			public IDataRow Current
			{
				get
				{
					CheckDisposed();
					if (state != ReadingState.Partly)
						throw new InvalidOperationException("The state of the object forbids the retrieval of this property.");
					return currentRow;
				}
			} // prop Current

			object IEnumerator.Current => Current;

			#endregion

			#region -- IDataColumns -----------------------------------------------------------

			public IDataColumn[] Columns
			{
				get
				{
					CheckDisposed();
					return columns.Value;
				}
			} // prop Columns

			public int ColumnCount
			{
				get
				{
					CheckDisposed();
					return Columns?.Length ?? 0;
				}
			} // prop ColumnCount

			#endregion
		} // class LocalStoreEnumerator

		#endregion

		#region -- class LocalStoreReader -------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class LocalStoreReader : IEnumerable<IDataRow>
		{
			private readonly string commandText;
			private readonly SQLiteConnection connection;

			#region -- Ctor/Dtor --------------------------------------------------------------

			public LocalStoreReader(string commandText, SQLiteConnection connection)
			{
				this.commandText = commandText;
				this.connection = connection;
			} // ctor

			#endregion

			#region -- IEnumerable<T> ---------------------------------------------------------

			public IEnumerator<IDataRow> GetEnumerator()
			{
				SQLiteCommand command = null;
				try
				{
					command = new SQLiteCommand(commandText, connection);
					return new LocalStoreEnumerator(command);
				}
				catch
				{
					command?.Dispose();
					throw;
				}
			} // func GetEnumerator

			IEnumerator IEnumerable.GetEnumerator()
				=> GetEnumerator();

			#endregion
		} // class LocalStoreReader

		#endregion

		public IEnumerable<IDataRow> GetOfflineItems()
			=> new LocalStoreReader("SELECT [Path], [OnlineMode], [ContentType], [ContentEncoding], [ContentSize], [ContentLastModification] FROM [main].[OfflineCache];", localStore);

		#endregion

		#endregion





		/// <summary>Override to support a better stream of the locally stored data.</summary>
		/// <param name="arguments"></param>
		/// <returns></returns>
		public override IEnumerable<IDataRow> GetViewData(PpsShellGetList arguments)
		{
			var sb = new StringBuilder("remote/?action=viewget&v=");
			sb.Append(arguments.ViewId);

			if (!String.IsNullOrEmpty(arguments.Filter))
				sb.Append("&f=").Append(Uri.EscapeDataString(arguments.Filter));
			if (!String.IsNullOrEmpty(arguments.Order))
				sb.Append("&o=").Append(Uri.EscapeDataString(arguments.Order));
			if (arguments.Start != -1)
				sb.Append("&s=").Append(arguments.Start);
			if (arguments.Count != -1)
				sb.Append("&c=").Append(arguments.Count);

			return Environment.Request.CreateViewDataReader(sb.ToString());

			//// get the table
			//string filterExpression = String.Empty;
			//System.Data.DataTable dt = null;

			//// get the datatable
			//if (!localData.TryGetValue(arguments.ViewId, out dt))
			//{
			//	LoadTestData(@"..\..\..\PPSnDesktop\Local\Data\" + arguments.ViewId + ".xml", ref dt);
			//	localData[arguments.ViewId] = dt;
			//}

			//#region Q&D
			//var filterId = arguments.Filter;
			//if (!String.IsNullOrEmpty(filterId))
			//{
			//	switch (arguments.ViewId.ToLower())
			//	{
			//		case "parts":
			//			if (filterId == "active")
			//				filterExpression = "TEILSTATUS = '10'";
			//			else if (filterId == "inactive")
			//				filterExpression = "TEILSTATUS = '90'";
			//			break;

			//		case "contacts":
			//			if (!String.IsNullOrEmpty(filterId))
			//				if (filterId == "liefonly")
			//					filterExpression = "DEBNR is null";
			//				else if (filterId == "kundonly")
			//					filterExpression = "KREDNR is null";
			//				else if (filterId == "intonly")
			//					filterExpression = "1 = 0";
			//			break;
			//	}
			//}
			//#endregion

			//if (!String.IsNullOrEmpty(arguments.Filter))
			//{
			//	if (filterExpression.Length > 0)
			//		filterExpression += " and OBJKMATCH like '%" + arguments.Filter + "%'";
			//	else
			//		filterExpression += "OBJKMATCH like '%" + arguments.Filter + "%'";
			//}

			//// filter data
			//var orderDef = arguments.Order;
			//if (orderDef != null)
			//	orderDef = orderDef.Replace("+", " asc").Replace("-", " desc");

			//// enumerate lines
			//using (var dv = new System.Data.DataView(dt, filterExpression, orderDef, System.Data.DataViewRowState.CurrentRows))
			//	for (int i = 0; i < arguments.Count; i++)
			//	{
			//		var index = arguments.Start + i;
			//		if (index < dv.Count)
			//			throw new NotImplementedException();
			//	}
			//yield break;
		} // func GetViewData

		public override IDataRow GetDetailedData(long objectId, string typ)
		{
			return null;
		} // func GetDetailedData
	} // class PpsLocalDataStore
}
