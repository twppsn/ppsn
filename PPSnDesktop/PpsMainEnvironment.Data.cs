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
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Data;
using TecWare.DE.Networking;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;

namespace TecWare.PPSn
{
	#region -- class TagDatabaseCommands ------------------------------------------------

	public sealed class TagDatabaseCommands : IDisposable
	{
		private readonly SQLiteCommand selectCommand;
		private readonly SQLiteParameter selectObjectId;

		private readonly SQLiteCommand insertCommand;
		private readonly SQLiteParameter insertObjectId;
		private readonly SQLiteParameter insertKey;
		private readonly SQLiteParameter insertClass;
		private readonly SQLiteParameter insertValue;

		private readonly SQLiteCommand updateCommand;
		private readonly SQLiteParameter updateId;
		private readonly SQLiteParameter updateClass;
		private readonly SQLiteParameter updateValue;

		private readonly SQLiteCommand deleteCommand;
		private readonly SQLiteParameter deleteId;

		public TagDatabaseCommands(SQLiteConnection localConnection)
		{
			this.selectCommand = new SQLiteCommand("SELECT [Id], [Key], [Class], [Value] FROM main.[ObjectTags] WHERE [ObjectId] = @ObjectId;", localConnection);
			this.selectObjectId = selectCommand.Parameters.Add("@ObjectId", DbType.Int64);

			this.insertCommand = new SQLiteCommand("INSERT INTO main.[ObjectTags] ([ObjectId], [Key], [Class], [Value]) values (@ObjectId, @Key, @Class, @Value);", localConnection);
			this.insertObjectId = insertCommand.Parameters.Add("@ObjectId", DbType.Int64);
			this.insertKey = insertCommand.Parameters.Add("@Key", DbType.String);
			this.insertClass = insertCommand.Parameters.Add("@Class", DbType.Int64);
			this.insertValue = insertCommand.Parameters.Add("@Value", DbType.String);

			this.updateCommand = new SQLiteCommand("UPDATE main.[ObjectTags] SET [Class] = @Class, [Value] = @Value where [Id] = @Id;", localConnection);
			this.updateId = updateCommand.Parameters.Add("@Id", DbType.Int64);
			this.updateClass = updateCommand.Parameters.Add("@Class", DbType.Int64);
			this.updateValue = updateCommand.Parameters.Add("@Value", DbType.String);

			this.deleteCommand = new SQLiteCommand("DELETE FROM main.[ObjectTags] WHERE [Id] = @Id;", localConnection);
			this.deleteId = deleteCommand.Parameters.Add("@Id", DbType.Int64);

			selectCommand.Prepare();
			insertCommand.Prepare();
			updateCommand.Prepare();
			deleteCommand.Prepare();
		} // ctor

		public void Dispose()
		{
			selectCommand.Dispose();
			insertCommand.Dispose();
			updateCommand.Dispose();
			deleteCommand.Dispose();
		} // proc Dispose

		public void UpdateTags(IList<PpsObjectTag> tags)
		{
			var updatedTags = new List<string>();

			// update tags
			using (var r = selectCommand.ExecuteReader(CommandBehavior.SingleResult))
			{
				while (r.Read())
				{
					var tagKey = r.GetString(1);
					var tagClass = r.GetInt64(2);
					var tagValue = r.GetString(3);

					var sourceTag = tags.FirstOrDefault(c => String.Compare(c.Name, tagKey, StringComparison.OrdinalIgnoreCase) == 0);

					if (sourceTag != null) // source exists, compare the value
					{
						if (sourceTag.Value != null)
						{
							if ((long)sourceTag.Class != tagClass || !sourceTag.IsValueEqual(tagValue)) // -> update
							{
								updateId.Value = r.GetInt64(0);
								updateClass.Value = sourceTag.Class;
								updateValue.Value = sourceTag.Value;

								updateCommand.ExecuteNonQuery();
							}

							updatedTags.Add(tagKey);
						}
						else
						{
							deleteId.Value = r.GetInt64(0);
							deleteCommand.ExecuteNonQuery();
						}
					} // if xSource
					else
					{
						deleteId.Value = r.GetInt64(0);
						deleteCommand.ExecuteNonQuery();
					}
				} // while r
			} // using r

			// insert all tags, they are not touched
			foreach (var sourceTag in tags)
			{
				if (sourceTag.Value != null && !updatedTags.Exists(c => String.Compare(c, sourceTag.Name, StringComparison.OrdinalIgnoreCase) == 0))
				{
					insertKey.Value = sourceTag.Name;
					insertClass.Value = (long)sourceTag.Class;
					insertValue.Value = sourceTag.Value;
					insertCommand.ExecuteNonQuery();
				}
			}
		} // func UpdateTags

		public object ObjectId
		{
			get { return selectObjectId.Value; }
			set
			{
				selectObjectId.Value =
					insertObjectId.Value = value;
			}
		} // prop ObjectId

		public SQLiteTransaction Transaction
		{
			get { return selectCommand.Transaction; }
			set
			{
				selectCommand.Transaction =
					insertCommand.Transaction =
					updateCommand.Transaction =
					deleteCommand.Transaction = value;
			}
		} // prop Transaction
	} // class TagDatabaseCommands

	#endregion

	#region -- class PpsConstantRow -----------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsConstantRow : DynamicDataRow
	{
		private readonly PpsConstant parent;
		private readonly object[] values;

		public PpsConstantRow(PpsConstant parent, object[] values)
		{
			this.parent = parent;
			this.values = values;
		} // ctor
		
		protected override BindingRestrictions GetRowBindingRestriction(Expression expression)
		{
			return BindingRestrictions.GetExpressionRestriction(
				Expression.AndAlso(
					Expression.TypeEqual(expression, typeof(PpsConstantRow)),
					Expression.Equal(Expression.Property(Expression.Convert(expression, typeof(PpsConstantRow)), parentProperty), Expression.Constant(parent, typeof(PpsConstant)))
				)
			);
		} // func GetRowBindingRestriction

		public PpsConstant Parent => parent;
		public override object this[int index] => values[index];
		public override bool IsDataOwner => true;
		public override IReadOnlyList<IDataColumn> Columns => parent.Columns;

		// -- Static --------------------------------------------------------------

		private readonly static System.Reflection.PropertyInfo parentProperty;

		static PpsConstantRow()
		{
			parentProperty = Procs.GetProperty(typeof(PpsConstantRow), nameof(Parent));
		} // sctor
	} // class PpsConstantRow

	#endregion

	#region -- class PpsConstant --------------------------------------------------------

	public abstract class PpsConstant : PpsEnvironmentDefinition, IEnumerable, IEnumerable<IDataRow>, IDataColumns, INotifyCollectionChanged
	{
		public event NotifyCollectionChangedEventHandler CollectionChanged;

		private readonly IDataColumn[] columns;

		public PpsConstant(PpsEnvironment environment, string name, IDataColumn[] columns)
			: base(environment, name)
		{
			this.columns = columns;
		} // ctor

		IEnumerator IEnumerable.GetEnumerator()
			=> GetEnumerator();

		public abstract IEnumerator<IDataRow> GetEnumerator();

		public IReadOnlyList<IDataColumn> Columns => columns;
	} // class PpsConstantList

	#endregion

	#region -- class PpsConstantData ----------------------------------------------------

	//public sealed class PpsConstantList//: IList
	//{
	//} // class PpsConstantList

	public sealed class PpsConstantData : PpsConstant
	{
		private const int staticColumns = 3;

		private readonly SQLiteConnection connection;
		private readonly string selectCommand;

		public PpsConstantData(PpsMainEnvironment environment, string name, IEnumerable<IDataColumn> columns)
			: base(environment, name, CreateColumns(columns))
		{
			this.connection = environment.LocalConnection;
			this.selectCommand = BuidSqlCommand();
		} // ctor

		private static IDataColumn[] CreateColumns(IEnumerable<IDataColumn> columns)
		{
			var columnList = new List<IDataColumn>();

			columnList.AddRange(new IDataColumn[] {
				new SimpleDataColumn("ServerId", typeof(long)),
				new SimpleDataColumn("IsActive", typeof(bool)),
				new SimpleDataColumn("Name", typeof(string))
			});

			foreach (var col in columns)
			{
				if (String.Compare(col.Name, "Name", StringComparison.OrdinalIgnoreCase) == 0)
					columnList[2] = col;
				else
					columnList.Add(col);
			}

			return columnList.ToArray();
		} // func CreateColumns

		private string BuidSqlCommand()
		{
			var sql = new StringBuilder();

			sql.Append("SELECT ")
				.Append("k.ServerId")
				.Append(",k.IsActive")
				.Append(",k.Name");

			// add columns for the meta
			for (var i = staticColumns; i < Columns.Count; i++)
			{
				var columnName = Columns[i].Name;
				sql.Append(", a_").Append(columnName).Append(".Value AS [").Append(columnName).Append("]");
			}

			// build FROM
			sql.Append(" FROM main.Constants k");
			for (var i = staticColumns; i < Columns.Count; i++)
			{
				var columnName = Columns[i].Name;
				sql.Append(" LEFT OUTER JOIN main.ConstantTags AS a_").Append(columnName)
					.Append(" ON k.Id = a_").Append(columnName).Append(".ConstantId AND ")
					.Append("a_").Append(columnName).Append(".Key = '").Append(columnName).Append("'");
			}

			sql.Append(" WHERE k.Typ = '").Append(Name).Append("'");

			return sql.ToString();
		} // func BuildSqlCommand

		public override IEnumerator<IDataRow> GetEnumerator()
		{
			var cmd = connection.CreateCommand();
			cmd.CommandText = selectCommand;

			using (var e = new DbRowEnumerator(cmd))
			{
				while (e.MoveNext())
				{
					// copy the items
					var values = new object[Columns.Count];
					for (var i = 0; i < values.Length; i++)
						values[i] = e.Current[i];

					// return the constant
					yield return new PpsConstantRow(this, values);
				}
			}				
		} // func GetEnumerator
	} // class PpsConstantData

	#endregion

	public partial class PpsMainEnvironment
	{
		#region -- UpdateDocumentStore ----------------------------------------------------

		public void UpdateDocumentStore()
		{
			// todo: lock -> mutex
			// todo: user rights -> server

			// get last state
			var maxState = 0L;
			using (var cmd = new SQLiteCommand("SELECT max([StateChg]) FROM main.[Objects]", LocalConnection))
			{
				using (var r = cmd.ExecuteReader(CommandBehavior.SingleRow))
					maxState = r.Read() && !r.IsDBNull(0) ? r.GetInt64(0) : -1;
			}

			// get the new objects
			using (var enumerator = GetViewData(new PpsShellGetList("dbo.objects") { Filter = new PpsDataFilterCompareExpression("StateChg", PpsDataFilterCompareOperator.Greater, new PpsDataFilterCompareTextValue(maxState.ToString())) }).GetEnumerator())
			{
				var indexId = enumerator.FindColumnIndex("Id", true);
				var indexGuid = enumerator.FindColumnIndex("Guid", true);
				var indexTyp = enumerator.FindColumnIndex("Typ", true);
				var indexNr = enumerator.FindColumnIndex("Nr", true);
				var indexIsRev = enumerator.FindColumnIndex("IsRev", true);
				var indexState = enumerator.FindColumnIndex("State", true);
				var indexStateChg = enumerator.FindColumnIndex("StateChg", true);
				var indexRevId = enumerator.FindColumnIndex("RevId", true);
				var indexTags = enumerator.FindColumnIndex("Tags");

				using (SQLiteCommand
						selectCommand = new SQLiteCommand("SELECT [Id] FROM main.[Objects] WHERE [Guid] = @Guid", LocalConnection),
						insertCommand = new SQLiteCommand("INSERT INTO main.[Objects] ([ServerId], [Guid], [Typ], [Nr], [IsRev], [StateChg], [RemoteRevId]) VALUES (@ServerId, @Guid, @Typ, @Nr, @IsRev, @StateChg, @RevId);", LocalConnection),
						updateCommand = new SQLiteCommand("UPDATE main.[Objects] SET [ServerId] = @ServerId, [Nr] = @Nr, [StateChg] = @StateChg, IsRev = @IsRev, [RemoteRevId] = @RevId where [Id] = @Id;", LocalConnection)
				)
				using (var updateTags = new TagDatabaseCommands(LocalConnection))
				{
					#region -- prepare upsert --

					var selectGuid = selectCommand.Parameters.Add("@Guid", DbType.Guid);

					var insertServerId = insertCommand.Parameters.Add("@ServerId", DbType.Int64);
					var insertGuid = insertCommand.Parameters.Add("@Guid", DbType.Guid);
					var insertTyp = insertCommand.Parameters.Add("@Typ", DbType.String);
					var insertNr = insertCommand.Parameters.Add("@Nr", DbType.String);
					var insertIsRev = insertCommand.Parameters.Add("@IsRev", DbType.Boolean);
					var insertStateChg = insertCommand.Parameters.Add("@StateChg", DbType.Int64);
					var insertRevId = insertCommand.Parameters.Add("@RevId", DbType.Int64);

					var updateServerId = updateCommand.Parameters.Add("@ServerId", DbType.Int64);
					var updateNr = updateCommand.Parameters.Add("@Nr", DbType.String);
					var updateIsRev = updateCommand.Parameters.Add("@IsRev", DbType.Boolean);
					var updateStateChg = updateCommand.Parameters.Add("@StateChg", DbType.Int64);
					var updateRevId = updateCommand.Parameters.Add("@RevId", DbType.Int64);
					var updateId = updateCommand.Parameters.Add("@Id", DbType.Int64);

					#endregion

					selectCommand.Prepare();
					insertCommand.Prepare();
					updateCommand.Prepare();

					var procValidateId = new Action<long>(c =>
					{
						if (c <= 0)
							throw new ArgumentException($"Invalid ServerId '{c}'.");
					});
					var procValidateNr = new Action<string>(c =>
					{
						if (String.IsNullOrEmpty(c))
							throw new ArgumentException($"Invalid Nr '{c}'.");
					});

					var run = true;
					do
					{
						using (var transaction = LocalConnection.BeginTransaction(IsolationLevel.ReadCommitted))
						{
							selectCommand.Transaction =
									insertCommand.Transaction =
									updateCommand.Transaction =
									updateTags.Transaction = transaction;

							var sw = Stopwatch.StartNew();
							while (sw.ElapsedMilliseconds < 500)
							{
								run = enumerator.MoveNext();
								if (!run)
									break;

								updateTags.ObjectId = DBNull.Value;

								#region -- upsert on objects -> selectTagsObjectId get filled --

								// find the current element
								selectGuid.Value = enumerator.GetValue(indexGuid, Guid.Empty, c =>
								{
									if (c == Guid.Empty)
										throw new ArgumentNullException("Invalid empty guid.");
								});

								bool objectExists;
								using (var r = selectCommand.ExecuteReader(CommandBehavior.SingleRow))
								{
									if (r.Read())
									{
										updateTags.ObjectId =
												updateId.Value = r.GetInt64(0);
										objectExists = true;
									}
									else
										objectExists = false;
								}

								// upsert
								if (objectExists)
								{
									updateServerId.Value = enumerator.GetValue(indexId, -1L, procValidateId);
									updateNr.Value = enumerator.GetValue(indexNr, String.Empty, procValidateNr);
									updateIsRev.Value = enumerator.GetValue(indexIsRev, false);
									updateStateChg.Value = enumerator.GetValue(indexStateChg, 0L);
									updateRevId.Value = enumerator.GetValue(indexRevId, -1L).DbNullIf(-1L);

									Debug.Print("Upsert Object: {0}", updateServerId.Value);
									updateCommand.ExecuteNonQuery();
								}
								else
								{
									insertServerId.Value = enumerator.GetValue(indexId, -1L, procValidateId);
									insertGuid.Value = selectGuid.Value;
									insertNr.Value = enumerator.GetValue(indexNr, String.Empty, procValidateNr);
									insertIsRev.Value = enumerator.GetValue(indexIsRev, false);
									insertTyp.Value = enumerator.GetValue(indexTyp, String.Empty).DbNullIfString();
									insertStateChg.Value = enumerator.GetValue(indexStateChg, 0L);
									insertRevId.Value = enumerator.GetValue(indexRevId, -1L).DbNullIf(-1L);

									insertCommand.ExecuteNonQuery();

									updateTags.ObjectId = LocalConnection.LastInsertRowId;
								}

								#endregion

								#region -- upsert tags --

								var tagStateString = enumerator.GetValue<string>(indexState, null);
								var tagDataString = enumerator.GetValue<string>(indexTags, null);

								if (tagStateString != null || tagDataString != null)
								{
									updateTags.UpdateTags(
										PpsObjectTag.ParseTagFields(tagStateString).Concat(PpsObjectTag.ParseTagFields(tagDataString)).ToArray()
									);
								}

								#endregion
							} // while sw
							transaction.Commit();
						} // using transaction
					} while (run);
				} // using selectCommand, insertCommand, updateCommand, using updateTags
			} // using enumerator
		} // proc UpdateDocumentStore

		#endregion

		#region -- GetConstantList --------------------------------------------------------

		#region -- class ConstantUpdateCommand --------------------------------------------

		private sealed class ConstantUpdateCommand : IDisposable
		{
			private readonly SQLiteCommand selectCommand;
			private readonly SQLiteParameter selectServerId;
			private readonly SQLiteParameter selectTyp;

			private readonly SQLiteCommand insertCommand;
			private readonly SQLiteParameter insertServerId;
			private readonly SQLiteParameter insertTyp;
			private readonly SQLiteParameter insertIsActive;
			private readonly SQLiteParameter insertSync;
			private readonly SQLiteParameter insertName;

			private readonly SQLiteCommand updateCommand;
			private readonly SQLiteParameter updateIsActive;
			private readonly SQLiteParameter updateSync;
			private readonly SQLiteParameter updateName;
			private readonly SQLiteParameter updateId;

			private readonly SQLiteCommand selectAttrCommand;
			private readonly SQLiteParameter selectAttrConstantId;

			private readonly SQLiteCommand insertAttrCommand;
			private readonly SQLiteParameter insertAttrConstantId;
			private readonly SQLiteParameter insertAttrKey;
			private readonly SQLiteParameter insertAttrValue;

			private readonly SQLiteCommand updateAttrCommand;
			private readonly SQLiteParameter updateAttrId;
			private readonly SQLiteParameter updateAttrValue;

			private readonly SQLiteCommand deleteAttrCommand;
			private readonly SQLiteParameter deleteAttrId;

			#region -- Ctor/Dtor ------------------------------------------------------------

			public ConstantUpdateCommand(SQLiteConnection connection)
			{
				selectCommand = new SQLiteCommand("SELECT Id, Sync FROM main.Constants WHERE ServerId = @ServerId AND Typ = @Typ", connection);
				selectServerId = selectCommand.Parameters.Add("@ServerId", DbType.Int64);
				selectTyp = selectCommand.Parameters.Add("@Typ", DbType.String);

				insertCommand = new SQLiteCommand("INSERT INTO main.Constants (ServerId, Typ, IsActive, Sync, Name) VALUES (@ServerId, @Typ, @IsActive, @Sync, @Name)", connection);
				insertServerId = insertCommand.Parameters.Add("@ServerId", DbType.Int64);
				insertTyp = insertCommand.Parameters.Add("@Typ", DbType.String);
				insertIsActive = insertCommand.Parameters.Add("@IsActive", DbType.Boolean);
				insertSync = insertCommand.Parameters.Add("@Sync", DbType.Int64);
				insertName = insertCommand.Parameters.Add("@Name", DbType.String);

				updateCommand = new SQLiteCommand("UPDATE main.Constants SET IsActive = @IsActive, Sync = @Sync, Name = @Name WHERE Id = @Id", connection);
				updateIsActive = updateCommand.Parameters.Add("@IsActive", DbType.Boolean);
				updateSync = updateCommand.Parameters.Add("@Sync", DbType.Int64);
				updateName = updateCommand.Parameters.Add("@Name", DbType.String);
				updateId = updateCommand.Parameters.Add("@Id", DbType.Int64);

				selectAttrCommand = new SQLiteCommand("SELECT Id, Key, Value FROM main.ConstantTags WHERE ConstantId = @ConstantId", connection);
				selectAttrConstantId = selectAttrCommand.Parameters.Add("@ConstantId", DbType.Int64);

				insertAttrCommand = new SQLiteCommand("INSERT INTO main.ConstantTags (ConstantId, Key, Value) VALUES (@ConstantId, @Key, @Value)", connection);
				insertAttrConstantId = insertAttrCommand.Parameters.Add("@ConstantId", DbType.Int64);
				insertAttrKey = insertAttrCommand.Parameters.Add("@Key", DbType.String);
				insertAttrValue = insertAttrCommand.Parameters.Add("@Value", DbType.String);

				updateAttrCommand = new SQLiteCommand("UPDATE main.ConstantTags SET Value = @Value WHERE Id = @Id", connection);
				updateAttrId = updateAttrCommand.Parameters.Add("@Id", DbType.Int64);
				updateAttrValue = updateAttrCommand.Parameters.Add("@Value", DbType.String);

				deleteAttrCommand = new SQLiteCommand("DELETE FROM main.ConstantTags WHERE Id = @Id", connection);
				deleteAttrId = deleteAttrCommand.Parameters.Add("@Id", DbType.Int64);

				selectCommand.Prepare();
				insertCommand.Prepare();
				updateCommand.Prepare();

				selectAttrCommand.Prepare();
				insertAttrCommand.Prepare();
				updateAttrCommand.Prepare();
				deleteAttrCommand.Prepare();
			} // ctor

			public void Dispose()
			{
				selectCommand?.Dispose();
				insertCommand?.Dispose();
				updateCommand?.Dispose();
				selectAttrCommand?.Dispose();
				insertAttrCommand?.Dispose();
				updateAttrCommand?.Dispose();
				deleteAttrCommand?.Dispose();
			} // proc Dispose

			#endregion

			public void Merge(long serverId, string typ, bool isActive, long remoteSync, string name, string attr)
			{
				long localId;
				long localSync;

				#region -- check if the constants exists on the client --
				selectServerId.Value = serverId;
				selectTyp.Value = typ;

				using (var r = selectCommand.ExecuteReader(CommandBehavior.SingleRow))
				{
					if (r.Read())
					{
						localId = r.GetInt64(0);
						localSync = r.GetInt64(1);
					}
					else
					{
						localId = -1;
						localSync = -1;
					}
				}
				#endregion

				#region -- merge constant, contains "return" --
				if (localId == -1) // insert new constant
				{
					insertServerId.Value = serverId;
					insertTyp.Value = typ;
					insertIsActive.Value = isActive;
					insertSync.Value = localSync = remoteSync;
					insertName.Value = (object)name ?? DBNull.Value;
					insertCommand.ExecuteNonQuery();
					localId = insertCommand.Connection.LastInsertRowId;
				}
				else if (localSync < remoteSync) // update existing constant
				{
					updateIsActive.Value = isActive;
					updateSync.Value = remoteSync;
					updateName.Value = (object)name ?? DBNull.Value;
					updateId.Value = localId;
					updateCommand.ExecuteNonQuery();
				}
				else
					return; // EXIT: nothing todo
				#endregion

				#region -- update attributes --

				var attribues = XElement.Parse(attr);

				selectAttrConstantId.Value = localId;
				using (var r = selectAttrCommand.ExecuteReader(CommandBehavior.SingleResult))
				{
					while (r.Read())
					{
						var attrId = r.GetInt64(0);
						var key = r.GetString(1);
						var value = r.GetString(2);

						var xSource = attribues.Element(key);
						if (xSource == null) // removed
						{
							deleteAttrId.Value = attrId;
							deleteAttrCommand.ExecuteNonQuery();
						}
						else // update key
						{
							xSource.Remove();

							if (value != xSource.Value)
							{
								updateAttrId.Value = attrId;
								updateAttrValue.Value = xSource.Value;
								updateAttrCommand.ExecuteNonQuery();
							}
						}
					}

					// insert missing
					insertAttrConstantId.Value = localId;
					foreach (var x in attribues.Elements())
					{
						insertAttrKey.Value = x.Name.LocalName;
						insertAttrValue.Value = x.Value;
						insertAttrCommand.ExecuteNonQuery();
					}
				}

				#endregion
			} // proc Merge

			public SQLiteTransaction Transaction
			{
				get { return selectCommand.Transaction; }
				set
				{
					selectCommand.Transaction =
						insertCommand.Transaction =
						updateCommand.Transaction =
						selectAttrCommand.Transaction =
						insertAttrCommand.Transaction =
						updateAttrCommand.Transaction =
						deleteAttrCommand.Transaction = value;
				}
			} // prop Transaction
		} // class ConstantUpdateCommand

		#endregion

		private IDataColumn CreateConstantColumn(XElement x)
		{
			return new SimpleDataColumn(
				x.GetAttribute("name", String.Empty),
				LuaType.GetType(x.GetAttribute("dataType", "string"), lateAllowed: false).Type
			);
		} // func CreateConstantColumn

		private async Task RefreshConstantsSchemaAsync()
		{
			constants.Clear();

			try
			{
				var xSchema = await Request.GetXmlAsync("/constants.xml", rootName: "constants");
				foreach (var xTable in xSchema.Elements("constant"))
				{
					var name = xTable.GetAttribute("name", String.Empty);
					if (String.IsNullOrEmpty(name))
						throw new ArgumentException("constant needs name.");

					var constantList = new PpsConstantData(this, name,
						from xColumn in xTable.Elements("column")
						select CreateConstantColumn(xColumn)
					);

					constants.AppendItem(constantList);
				}
			}
			catch (WebException ex)
			{
				if (ex.Status == WebExceptionStatus.ProtocolError) // e.g. file not found
					await ShowExceptionAsync(ExceptionShowFlags.Background, ex);
				else
					throw;
			}
		} // proc RefreshConstantsSchemaAsync

		private void UpdateConstants()
		{
            // sync not via max -> store last sync
            using (var enumerator = GetViewData(new PpsShellGetList("sys.constants") { }).GetEnumerator())
            // second sync data
            using (var update = new ConstantUpdateCommand(LocalConnection))
            {
                var idxServerId = enumerator.FindColumnIndex("Id", true);
                var idxTyp = enumerator.FindColumnIndex("Typ", true);
                var idxIsActive = enumerator.FindColumnIndex("IsActive", true);
                var idxSync = enumerator.FindColumnIndex("Sync", true);
                var idxName = enumerator.FindColumnIndex("Name", true);
                var idxAttr = enumerator.FindColumnIndex("Attr", true);
               
                var run = true;
                do
                {
                    using (var transaction = LocalConnection.BeginTransaction())
                    {
                        update.Transaction = transaction;

                        for (var i = 0; i < 1000; i++)
                        {
                            run = enumerator.MoveNext();
                            if (!run)
                                break;

                            update.Merge(
                                (long)enumerator.Current[idxServerId],
                                (string)enumerator.Current[idxTyp],
                                (bool)(enumerator.Current[idxIsActive] ?? false),
                                (long)(enumerator.Current[idxSync] ?? 0L),
                                (string)enumerator.Current[idxName],
                                (string)enumerator.Current[idxAttr]
                            );
                        } // for i
                        transaction.Commit();
                    } // using transaction
                } while (run);
            } // using enumerator, using update
        } // proc UpdateConstants

		#endregion

		protected override IEnumerable<Tuple<Type, string>> GetStoreTables()
			=> base.GetStoreTables().Union(GetStoreTablesFromAssembly(typeof(PpsMainEnvironment), "Static.SQLite"));

		private bool TryGetStaticItem(string path, out string contentType, out Stream data)
		{
			// check for a resource file
			var baseType = typeof(PpsMainEnvironment);
			data = baseType.Assembly.GetManifestResourceStream(baseType, "Static." + path.Replace('/', '.'));
			contentType = MimeTypes.Text.Xml;
			return data != null;
		} // func TryGetStaticItem

		protected override bool TryGetOfflineItem(string path, bool onlineMode, out string contentType, out Stream data)
		{
			var r = base.TryGetOfflineItem(path, onlineMode, out contentType, out data);
			if (r)
				return r;
			else if (path.StartsWith("/wpf/") && !onlineMode) // request could not resolved for the offline item
				return TryGetStaticItem(path.Substring(5), out contentType, out data);

			return r;
		} // func TryGetOfflineItem

		public override IEnumerable<IDataRow> GetViewData(PpsShellGetList arguments)
		{
			if (arguments.ViewId.StartsWith("local.", StringComparison.OrdinalIgnoreCase)) // it references the local db
			{
				if (arguments.ViewId == "local.objects")
					return CreateObjectFilter(arguments);
				else
					throw new ArgumentOutOfRangeException("todo"); // todo: exception
			}
			else
				return base.GetViewData(arguments);
		} // func GetViewData
	} // class PpsMainEnvironment
}
