using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using TecWare.DE.Data;
using TecWare.DE.Networking;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;

namespace TecWare.PPSn
{
	public partial class PpsMainEnvironment
	{
		#region -- UpdateDocumentStore ----------------------------------------------------

		public void UpdateDocumentStore()
		{
			// todo: lock
			// todo: user rights

			// get last rev id
			//"RevId:>0"

			using (var enumerator = GetViewData(new PpsShellGetList("dbo.objects") { Filter = null }).GetEnumerator())
			{
				var indexId = enumerator.FindColumnIndex("Id", true);
				var indexGuid = enumerator.FindColumnIndex("Guid", true);
				var indexTyp = enumerator.FindColumnIndex("Typ", true);
				var indexNr = enumerator.FindColumnIndex("Nr", true);
				var indexRevId = enumerator.FindColumnIndex("RevId", true);
				var indexTags = enumerator.FindColumnIndex("Tags");

				using (SQLiteCommand
					selectCommand = new SQLiteCommand("SELECT [Id] FROM main.[Objects] WHERE [Guid] = @Guid", LocalConnection),
					insertCommand = new SQLiteCommand("INSERT INTO main.[Objects] ([ServerId], [Guid], [Typ], [Nr], [RemoteRevId]) VALUES (@ServerId, @Guid, @Typ, @Nr, @RevId);", LocalConnection),
					updateCommand = new SQLiteCommand("UPDATE main.[Objects] SET [ServerId] = @ServerId, [Nr] = @Nr, [RemoteRevId] = @RevId where [Id] = @Id;", LocalConnection),

					selectTagsCommand = new SQLiteCommand("SELECT [Id], [Key], [Class], [Value] FROM main.[ObjectTags] WHERE [ObjectId] = @ObjectId;", LocalConnection),
					insertTagsCommand = new SQLiteCommand("INSERT INTO main.[ObjectTags] ([ObjectId], [Key], [Class], [Value]) values (@ObjectId, @Key, @Class, @Value);", LocalConnection),
					updateTagsCommand = new SQLiteCommand("UPDATE main.[ObjectTags] SET [Class] = @Class, [Value] = @Value where [Id] = @Id;", LocalConnection),
					deleteTagsCommand = new SQLiteCommand("DELETE FROM main.[ObjectTags] WHERE [Id] = @Id;", LocalConnection)
				)
				{
					#region -- prepare upsert --

					var selectGuid = selectCommand.Parameters.Add("@Guid", DbType.Guid);

					var insertServerId = insertCommand.Parameters.Add("@ServerId", DbType.Int64);
					var insertGuid = insertCommand.Parameters.Add("@Guid", DbType.Guid);
					var insertTyp = insertCommand.Parameters.Add("@Typ", DbType.String);
					var insertNr = insertCommand.Parameters.Add("@Nr", DbType.String);
					var insertRevId = insertCommand.Parameters.Add("@RevId", DbType.Int64);

					var updateServerId = updateCommand.Parameters.Add("@ServerId", DbType.Int64);
					var updateNr = updateCommand.Parameters.Add("@Nr", DbType.String);
					var updateRevId = updateCommand.Parameters.Add("@RevId", DbType.Int64);
					var updateId = updateCommand.Parameters.Add("@Id", DbType.Int64);

					var selectTagsObjectId = selectTagsCommand.Parameters.Add("@ObjectId", DbType.Int64);

					var insertTagsObjectId = insertTagsCommand.Parameters.Add("@ObjectId", DbType.Int64);
					var insertTagsKey = insertTagsCommand.Parameters.Add("@Key", DbType.String);
					var insertTagsClass = insertTagsCommand.Parameters.Add("@Class", DbType.Int64);
					var insertTagsValue = insertTagsCommand.Parameters.Add("@Value", DbType.String);

					var updateTagsId = updateTagsCommand.Parameters.Add("@Id", DbType.Int64);
					var updateTagsClass = updateTagsCommand.Parameters.Add("@Class", DbType.Int64);
					var updateTagsValue = updateTagsCommand.Parameters.Add("@Value", DbType.String);

					var deleteTagsId = deleteTagsCommand.Parameters.Add("@Id", DbType.Int64);

					#endregion

					selectCommand.Prepare();
					insertCommand.Prepare();
					updateCommand.Prepare();

					selectTagsCommand.Prepare();
					insertTagsCommand.Prepare();
					updateTagsCommand.Prepare();
					deleteTagsCommand.Prepare();

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

					while (enumerator.MoveNext())
					{
						using (var transaction = LocalConnection.BeginTransaction(IsolationLevel.ReadCommitted))
						{
							// update the transaction
							selectCommand.Transaction =
								insertCommand.Transaction =
								updateCommand.Transaction =
								selectTagsCommand.Transaction =
								 insertTagsCommand.Transaction =
								 updateTagsCommand.Transaction =
								 deleteTagsCommand.Transaction = transaction;

							selectTagsObjectId.Value = DBNull.Value;

							#region -- upsert on objects -> selectTagsObjectId get filled --

							// find the current element
							selectGuid.Value = enumerator.GetValue(indexGuid, Guid.Empty, c =>
							{
								if (c == Guid.Empty)
									throw new ArgumentNullException("Invalid empty guid.");
							}
							);

							bool objectExists;
							using (var r = selectCommand.ExecuteReader(CommandBehavior.SingleRow))
							{
								if (r.Read())
								{
									selectTagsObjectId.Value =
										updateId.Value = r.GetInt64(0);
									objectExists = true;
								}
								else
									objectExists = false;
							}

							// upsert
							if (objectExists)
							{
								updateServerId.Value = enumerator.GetValue(indexId, -1, procValidateId);
								updateNr.Value = enumerator.GetValue(indexNr, String.Empty, procValidateNr);
								updateRevId.Value = enumerator.GetValue(indexRevId, -1).DbNullIf(-1);

								updateCommand.ExecuteNonQuery();
							}
							else
							{
								insertServerId.Value = enumerator.GetValue(indexId, -1, procValidateId);
								insertGuid.Value = selectGuid.Value;
								insertNr.Value = enumerator.GetValue(indexNr, String.Empty, procValidateNr);
								insertTyp.Value = enumerator.GetValue(indexTyp, String.Empty).DbNullIfString();
								insertRevId.Value = enumerator.GetValue(indexRevId, -1).DbNullIf(-1);

								insertCommand.ExecuteNonQuery();

								selectTagsObjectId.Value = LocalConnection.LastInsertRowId;
							}
							#endregion

							#region -- upsert tabs --

							var updatedTags = new List<string>();
							var tagDataString = enumerator.GetValue<string>(indexTags, null);
							if (tagDataString != null)
							{
								var tagData = XDocument.Parse(tagDataString);

								// update tags
								using (var r = selectTagsCommand.ExecuteReader(CommandBehavior.SingleResult))
								{
									while (r.Read())
									{
										var tagKey = r.GetString(1);
										var tagClass = r.GetInt64(2);
										var tagValue = r.GetString(3);

										var xSource = tagData.Root.Element(tagKey);
										if (xSource != null) // source exists, compare the value
										{
											var otherClass = xSource.GetAttribute("c", 0);
											var otherValue = Procs.EscapeSpecialChars(xSource.Value);
											if (!String.IsNullOrEmpty(otherValue))
											{
												if (otherClass != tagClass && tagValue != otherValue) // -> update
												{
													updateTagsId.Value = r.GetInt64(0);
													updateTagsClass.Value = otherClass;
													updateTagsValue.Value = otherValue;
												}

												updatedTags.Add(tagKey);
											}
											else
											{
												deleteTagsId.Value = r.GetInt64(0);
												deleteTagsCommand.ExecuteNonQuery();
											}
										} // if xSource
										else
										{
											deleteTagsId.Value = r.GetInt64(0);
											deleteTagsCommand.ExecuteNonQuery();
										}
									} // while r
								} // using r

								// insert all tags, they are not touched
								foreach (var xSource in tagData.Root.Elements())
								{
									var tagKey = xSource.Name.LocalName;
									var tagValue = xSource.Value;
									if (!String.IsNullOrEmpty(tagValue) && !updatedTags.Exists(c => String.Compare(c, tagKey, StringComparison.OrdinalIgnoreCase) == 0))
									{
										insertTagsObjectId.Value = selectTagsObjectId.Value;
										insertTagsKey.Value = tagKey;
										insertTagsClass.Value = xSource.GetAttribute("c", 0);
										insertTagsValue.Value = tagValue;
										insertTagsCommand.ExecuteNonQuery();
									}
								}
							} // if tagData

							#endregion

							transaction.Commit();
						} // while enumerator
					}
				}
			} // using prepare
		} // proc UpdateDocumentStore

		#endregion

		#region -- CreateObjectFilter -----------------------------------------------------

		#region -- class ObjectViewEnumerator ---------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class ObjectViewEnumerator : IEnumerator<IDataRow>, IDataColumns
		{
			#region -- class ObjectViewRow --------------------------------------------------

			///////////////////////////////////////////////////////////////////////////////
			/// <summary></summary>
			private sealed class ObjectViewRow : DynamicDataRow
			{
				#region -- class ObjectViewColumns --------------------------------------------

				private sealed class ObjectViewColumns : IReadOnlyList<IDataColumn>
				{
					private readonly ObjectViewRow row;

					public ObjectViewColumns(ObjectViewRow row)
					{
						this.row = row;
					} // ctor

					public IEnumerator<IDataColumn> GetEnumerator()
						=> staticColumns.Concat(row.dynamicKeys.Select(c => CreateSimpleDataColumn(c))).GetEnumerator();

					IEnumerator IEnumerable.GetEnumerator()
						=> GetEnumerator();

					public IDataColumn this[int index]
					{
						get
						{
							if (index < 0)
								throw new ArgumentOutOfRangeException();
							else if (index < staticColumns.Length)
								return staticColumns[index];
							else if (index < staticColumns.Length + row.dynamicKeys.Length)
							{
								var attribute = row.dynamicKeys[index - staticColumns.Length];
								return CreateSimpleDataColumn(attribute);
							}
							else
								throw new ArgumentOutOfRangeException();
						}
					} // prop this

					private static SimpleDataColumn CreateSimpleDataColumn(PropertyValue attribute)
						=> new SimpleDataColumn(attribute.Name, attribute.Type);

					public int Count => staticColumns.Length + row.dynamicKeys.Length;
				} // class ObjectViewColumns

				#endregion

				private readonly object[] staticValues;
				private readonly PropertyValue[] dynamicKeys;
				private readonly ObjectViewColumns columns;

				public ObjectViewRow(IDataRecord r)
				{
					var staticColumnCount = staticColumns.Length;

					// read static values
					staticValues = new object[staticColumnCount];
					for (var i = 0; i < staticColumnCount; i++)
						staticValues[i] = r.IsDBNull(i) ? null : r.GetValue(i);

					// parse dynamic attributes
					if (!r.IsDBNull(staticColumnCount))
					{
						dynamicKeys = r.GetString(staticColumnCount)
							.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
							.Select(c => CreateKeyValue(c))
							.ToArray();
					}
					else
						dynamicKeys = PropertyValue.EmptyArray;

					this.columns = new ObjectViewColumns(this);
				} // ctor

				private PropertyValue CreateKeyValue(string attributeLine)
				{
					var indexColon = attributeLine.IndexOf(':');
					var indexEqual = attributeLine.IndexOf('=');

					string attributeName;
					int classHint;

					if (indexColon > 0 && indexColon > indexEqual) // with class hint
					{
						attributeName = attributeLine.Substring(0, indexColon);
						if (!Int32.TryParse(attributeLine.Substring(indexColon + 1, indexEqual - indexColon - 1), out classHint))
							classHint = 0;
					}
					else // no class hint
					{
						attributeName = attributeLine.Substring(0, indexEqual);
						classHint = 0;
					}

					var dataType = TypeFromClassHint(classHint);
					object value = Procs.UnescapeSpecialChars(attributeLine.Substring(indexEqual + 1));
					if (value != null)
						value = Procs.ChangeType(value, dataType);
					return new PropertyValue(attributeName, dataType, value);
				} // func CreateKeyValue

				private static Type TypeFromClassHint(int classHint)
				{
					// todo:
					return typeof(string);
				}

				public override IReadOnlyList<IDataColumn> Columns => columns;

				public override object this[int index]
				{
					get
					{
						if (index < 0)
							throw new ArgumentOutOfRangeException();
						else if (index < staticColumns.Length)
							return staticValues[index];
						else if (index < staticColumns.Length + dynamicKeys.Length)
							return dynamicKeys[index - staticColumns.Length];
						else
							throw new ArgumentOutOfRangeException();
					}
				}
			} // class ObjectViewRow

			#endregion

			private readonly SQLiteCommand command;
			private SQLiteDataReader reader;
			private ObjectViewRow current;

			public ObjectViewEnumerator(SQLiteCommand command)
			{
				if (command == null)
					throw new ArgumentNullException("command");

				this.command = command;
			} // ctor

			public void Dispose()
			{
				reader?.Dispose();
				command.Dispose();
			} // proc Dispose

			public bool MoveNext()
			{
				if (reader == null)
					reader = command.ExecuteReader(CommandBehavior.SingleResult);

				if (reader.Read())
				{
					current = new ObjectViewRow(reader);
					return true;
				}
				else
				{
					current = null;
					return false;
				}
			} // func MoveNext

			public void Reset()
			{
				reader?.Dispose();
				reader = null;
				current = null;
			} // func Reset

			public IDataRow Current => current;
			object IEnumerator.Current => current;

			public IReadOnlyList<IDataColumn> Columns => staticColumns;

			// -- Static ------------------------------------------------------------

			private static readonly IDataColumn[] staticColumns;

			static ObjectViewEnumerator()
			{
				staticColumns = new IDataColumn[]
					{
						new SimpleDataColumn("Id", typeof(long)),
						new SimpleDataColumn("ServerId", typeof(long)),
						new SimpleDataColumn("Guid", typeof(Guid)),
						new SimpleDataColumn("Typ", typeof(string)),
						new SimpleDataColumn("Nr", typeof(string)),
						new SimpleDataColumn("RemoteRevId", typeof(long))
					};
			} // sctor
		} // class ObjectViewEnumerator

		#endregion

		#region -- class ObjectViewGenerator ----------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class ObjectViewGenerator : IEnumerable<IDataRow>
		{
			private readonly SQLiteConnection localStoreConnection;

			public ObjectViewGenerator(SQLiteConnection localStoreConnection)
			{
				this.localStoreConnection = localStoreConnection;
			} // ctor

			public string LookupField(string fieldName, bool order)
			{
				return fieldName;
			} // func LookupField

			public string LookupFieldForFilter(string fieldName)
				=> LookupField(fieldName, false);

			public string LookupFieldForFOrder(string fieldName)
				=> LookupField(fieldName, false);

			public void ApplyFilter(PpsDataFilterExpression filter)
			{
			} // proc ApplyFilter

			public void ApplyOrder(IEnumerable<PpsDataOrderExpression> orders)
			{
			} // proc ApplyOrder

			public void ApplyLimit(long startAt, long count)
			{
			} // proc ApplyLimit

			private SQLiteCommand CreateCommand()
			{
				return new SQLiteCommand("select o.Id, o.ServerId, o.Guid, o.Typ, o.Nr, o.RemoteRevId, group_concat(t.Key || ':' || t.Class || '='  || t.Value, char(10)) as [Values] from Objects o left outer join ObjectTags t on (o.Id = t.ObjectId) group by o.Id, o.ServerId, o.Guid, o.Typ, o.Nr, o.RemoteRevId", localStoreConnection);
				/*
select o.Id, o.ServerId, o.Guid, Typ, o.Nr, o.RemoteRevId, group_concat(t.Key || ':' || t.Class || '='  || t.Value, char(10)) as [Values]
from Objects o 
left outer join ObjectTags t on (o.Id = t.ObjectId) 
left outer join ObjectTags t_liefnr on (o.Id = t_liefnr.ObjectId and t_liefnr.Key = 'LIEFNR')
left outer join ObjectTags t_kundnr on (o.Id = t_kundnr.ObjectId and t_kundnr.Key = 'KUNDNR')
left outer join ObjectTags t_number on (o.Id = t_number .ObjectId and t_number.class = 1) 
left outer join ObjectTags t_all on (o.Id = t_all .ObjectId) 
where t_liefnr.value is not null and t_number .Value ='50014' and t_all.Value like 'Kleinskunden'
group by o.Id, o.ServerId, o.Guid, o.Typ, o.Nr, o.RemoteRevId, t_liefnr.value
order by t_liefnr.value desc
*/
			} // func CreateCommand

			public IEnumerator<IDataRow> GetEnumerator()
				=> new ObjectViewEnumerator(CreateCommand());

			IEnumerator IEnumerable.GetEnumerator()
				=> GetEnumerator();
		} // class ObjectViewGenerator

		#endregion

		private IEnumerable<IDataRow> CreateObjectFilter(PpsShellGetList arguments)
		{
			var gn = new ObjectViewGenerator(LocalConnection);

			gn.ApplyFilter(PpsDataFilterExpression.Parse(arguments.Filter, 0, null, gn.LookupFieldForFilter));
			gn.ApplyOrder(PpsDataOrderExpression.Parse(arguments.Order, gn.LookupFieldForFOrder));
			gn.ApplyLimit(arguments.Start, arguments.Count);

			return gn;
		} // func CreateObjectFilter

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
