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

		public void UpdateTags(PpsObjectTag[] tags)
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

			// get last rev id
			var maxRevId = 0L;
			using (var cmd = new SQLiteCommand("SELECT max([RemoteRevId]) FROM main.[Objects]", LocalConnection))
			{
				using (var r = cmd.ExecuteReader(CommandBehavior.SingleRow))
					maxRevId = r.Read() && !r.IsDBNull(0) ? r.GetInt64(0) : 0;
			}

			// get the new objects, todo: not via RevId -> change id
			using (var enumerator = GetViewData(new PpsShellGetList("dbo.objects") { Filter = new PpsDataFilterCompareExpression("RevId", PpsDataFilterCompareOperator.Greater, new PpsDataFilterCompareTextValue(maxRevId.ToString())) }).GetEnumerator())
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
                    updateCommand = new SQLiteCommand("UPDATE main.[Objects] SET [ServerId] = @ServerId, [Nr] = @Nr, [RemoteRevId] = @RevId where [Id] = @Id;", LocalConnection)
                )
                using (var updateTags = new TagDatabaseCommands(LocalConnection))
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

                            for (var i = 0; i < 1000; i++)
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
                                    updateServerId.Value = enumerator.GetValue(indexId, -1, procValidateId);
                                    updateNr.Value = enumerator.GetValue(indexNr, String.Empty, procValidateNr);
                                    updateRevId.Value = enumerator.GetValue(indexRevId, -1).DbNullIf(-1);

                                    Debug.Print("Upsert Object: {0}", updateServerId.Value);
                                    updateCommand.ExecuteNonQuery();
                                }
                                else
                                {
                                    insertServerId.Value = enumerator.GetValue(indexId, -1, procValidateId);
                                    insertGuid.Value = selectGuid.Value;
                                    insertNr.Value = enumerator.GetValue(indexNr, String.Empty, procValidateNr);
                                    insertTyp.Value = enumerator.GetValue(indexTyp, String.Empty).DbNullIfString();
                                    insertRevId.Value = enumerator.GetValue(indexRevId, -1).DbNullIf(-1);

                                    Debug.Print("Insert Object: {0}", insertServerId.Value);
                                    var sw = Stopwatch.StartNew();
                                    insertCommand.ExecuteNonQuery();
                                    Debug.Print("Elapsed: {0}ms", sw.ElapsedMilliseconds);

                                    updateTags.ObjectId = LocalConnection.LastInsertRowId;
                                }

                                #endregion

                                #region -- upsert tags --

                                var tagDataString = enumerator.GetValue<string>(indexTags, null);
                                if (tagDataString != null)
                                    updateTags.UpdateTags(PpsObjectTag.ParseTagFields(tagDataString).ToArray());

                                #endregion
                            } // for i
                            transaction.Commit();
                        } // using transaction
                    } while (run);
                } // using selectCommand, insertCommand, updateCommand, using updateTags
            } // using enumerator
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

				///////////////////////////////////////////////////////////////////////////////
				/// <summary></summary>
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

				#region -- Ctor/Dtor ----------------------------------------------------------

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

					if (indexColon > 0 && indexColon < indexEqual) // with class hint
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
					if (classHint == 2)
						return typeof(DateTime);
					// todo:
					return typeof(string);
				} // func TypeFromClassHint

				#endregion

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
							return dynamicKeys[index - staticColumns.Length].Value;
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

			internal static readonly IDataColumn[] staticColumns;
			internal static readonly string[] staticColumnExpr;

			static ObjectViewEnumerator()
			{
				staticColumns = new IDataColumn[]
					{
						new SimpleDataColumn("Id", typeof(long)),
						new SimpleDataColumn("ServerId", typeof(long)),
						new SimpleDataColumn("Guid", typeof(Guid)),
						new SimpleDataColumn("Typ", typeof(string)),
						new SimpleDataColumn("Nr", typeof(string)),
						new SimpleDataColumn("RemoteRevId", typeof(long)),
						new SimpleDataColumn("PulledRevId", typeof(long)),
						new SimpleDataColumn("IsLocalModified", typeof(bool))
					};

				staticColumnExpr = new string[]
					{
						"o.Id",
						"o.ServerId",
						"o.Guid",
						"o.Typ",
						"o.Nr",
						"o.RemoteRevId",
						"o.PulledRevId",
						"o.DocumentIsChanged"
					};

#if DEBUG
				if (staticColumnExpr.Length != staticColumns.Length)
					throw new ArgumentException();
#endif
			} // sctor
		} // class ObjectViewEnumerator

		#endregion

		#region -- class ObjectViewGenerator ----------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class ObjectViewGenerator : IEnumerable<IDataRow>
		{
			private const string AllColumns = "s_all";
			private const string NumberColumns = "s_number";
			private const int NumberClass = 1;
			private const string DateColumns = "s_date";
			private const int DateClass = 2;
			private const string ColumnAliasPrefix = "a_";
			private const string ColumnStaticPrefix = "s_";
			private const string ColumnJoinPrefix = "t_";

			private const string StaticNr = "NR";
			private const string StaticTyp = "TYP";

			#region -- class ObjectViewFilterVisitor ----------------------------------------

			///////////////////////////////////////////////////////////////////////////////
			/// <summary></summary>
			private sealed class ObjectViewFilterVisitor : PpsDataFilterVisitorSql
			{
				private readonly ObjectViewGenerator owner;

				public ObjectViewFilterVisitor(ObjectViewGenerator owner)
				{
					this.owner = owner;
				} // ctor

				private string RegisterColumn(string columnToken, string nullToken, int @class)
					=> owner.RegisterColumn(String.IsNullOrEmpty(columnToken) ? nullToken : columnToken, @class);

				protected override Tuple<string, Type> LookupDateColumn(string columnToken)
					=> new Tuple<string, Type>(RegisterColumn(columnToken, DateColumns, DateClass), typeof(DateTime));

				protected override Tuple<string, Type> LookupNumberColumn(string columnToken)
					=> new Tuple<string, Type>(RegisterColumn(columnToken, NumberColumns, NumberClass), typeof(string));

				protected override Tuple<string, Type> LookupColumn(string columnToken)
					=> new Tuple<string, Type>(RegisterColumn(columnToken, AllColumns, 0), typeof(string));

				protected override string LookupNativeExpression(string key)
					=> "1=1"; // not supported

				protected override string CreateDateString(DateTime value)
					=> "datetime('" + value.ToString("s") + "')";
		} // class ObjectViewFilterVisitor

			#endregion

			#region -- class ObjectViewColumn -----------------------------------------------

			private enum ObjectViewColumnType
			{
				All,
				Number,
				Date,
				Key,
				Static
			} // enum ObjectViewColumnType

			private sealed class ObjectViewColumn
			{
				private readonly ObjectViewColumnType type;
				private readonly string keyName; // tag name for the column (meta key)
				private readonly int classification; // the forced classification for the tag (0 for not present)

				private readonly string columnAlias;  // alias for the where expression (used in where/orderby)
				private readonly string joinAlias;

				public ObjectViewColumn(string virtualColumn, int classification)
				{
					switch (virtualColumn)
					{
						case AllColumns:
							type = ObjectViewColumnType.All;
							joinAlias = AllColumns;
							goto case "\0";
						case DateColumns:
							type = ObjectViewColumnType.Date;
							joinAlias = DateColumns;
							goto case "\0";
						case NumberColumns:
							type = ObjectViewColumnType.Number;
							joinAlias = NumberColumns;
							goto case "\0";
						case "\0":
							this.keyName = null;
							this.columnAlias = null;

							switch (classification)
							{
								case DateClass:
									columnAlias = CastToDateExpression(virtualColumn + ".value");
									break;
								case NumberClass:
								default:
									columnAlias = virtualColumn + ".value";
									break;
							}
							break;
						default:
							if (String.Compare(virtualColumn, StaticNr, StringComparison.OrdinalIgnoreCase) == 0 ||
								String.Compare(virtualColumn, StaticTyp, StringComparison.OrdinalIgnoreCase) == 0)
							{
								type = ObjectViewColumnType.Static;
								if (classification != 0)
									classification = 0; // ignore classification
								this.columnAlias = ColumnStaticPrefix + virtualColumn;
							}
							else
							{
								type = ObjectViewColumnType.Key;

								this.keyName = virtualColumn;
								this.joinAlias = ColumnJoinPrefix + virtualColumn;

								if (classification != 0)
									this.columnAlias = ColumnAliasPrefix + virtualColumn + classification.ToString();
								else
									this.columnAlias = ColumnAliasPrefix + virtualColumn;
							}
							break;
					}

					this.classification = classification;
				} // ctor

				public override string ToString()
				=> $"{type}Column: {columnAlias} -> {joinAlias} from {keyName}, {classification}";

				public bool Equals(string virtualColumn, int classification)
				{
					if (classification == this.classification)
					{
						switch (virtualColumn)
						{
							case AllColumns:
								return virtualColumn == AllColumns;
							case DateColumns:
								return virtualColumn == DateColumns;
							case NumberColumns:
								return virtualColumn == NumberColumns;
							default:
								return String.Compare(this.keyName, virtualColumn, StringComparison.OrdinalIgnoreCase) == 0;
						}
					}
					else
						return false;
				} // func Equals

				private string CastToDateExpression(string columnExpr)
					=> "datetime(" + columnExpr + ")";

				public string CreateWhereExpression()
				{
					if (type != ObjectViewColumnType.Key)
						throw new NotSupportedException();

					switch (classification)
					{
						case DateClass:
							return CastToDateExpression(joinAlias + ".value") + " AS " + columnAlias;
						case NumberClass:
						default:
							return joinAlias + ".value AS " + columnAlias;
					}
				} // func CreateWhereExpression

				public string CreateLeftOuterJoinExpression()
				{
					switch (type)
					{
						case ObjectViewColumnType.All:
							return "LEFT OUTER JOIN ObjectTags AS " + AllColumns + " ON (o.Id = " + AllColumns + ".ObjectId)";
						case ObjectViewColumnType.Date:
							return "LEFT OUTER JOIN ObjectTags AS " + DateColumns + " ON (o.Id = " + DateColumns + ".ObjectId AND " + DateColumns + ".class = " + DateClass + ")";
						case ObjectViewColumnType.Number:
							return "LEFT OUTER JOIN ObjectTags AS " + NumberColumns + " ON (o.Id = " + NumberColumns + ".ObjectId AND " + NumberColumns + ".class = " + NumberClass + ")";

						case ObjectViewColumnType.Key:
							if (classification == 0)
								return "LEFT OUTER JOIN ObjectTags AS " + joinAlias + " ON (o.Id = " + joinAlias + ".ObjectId AND " + joinAlias + ".Key = '" + keyName + "' COLLATE NOCASE)";
							else
								return "LEFT OUTER JOIN ObjectTags AS " + joinAlias + " ON (o.Id = " + joinAlias + ".ObjectId AND " + joinAlias + ".Class = " + classification + " AND " + joinAlias + ".Key = '" + keyName + "' COLLATE NOCASE)";
						default:
							throw new NotSupportedException();
					}
				} //func CreateLeftOuterJoinExpression

				public string KeyName => keyName;
				public int Classification => classification;
				public string ColumnAlias => columnAlias;

				public ObjectViewColumnType Type => type;
			} // ObjectViewColumn

			#endregion

			private readonly SQLiteConnection localStoreConnection;

			private List<ObjectViewColumn> columnInfos = new List<ObjectViewColumn>();
			private string whereCondition = null;
			private string orderCondition = null;
			private long limitStart = -1;
			private long limitCount = -1;

			public ObjectViewGenerator(SQLiteConnection localStoreConnection)
			{
				this.localStoreConnection = localStoreConnection;

				RegisterColumn(AllColumns, 0); // register always "s_all"
			} // ctor

			private string RegisterColumn(string virtualColumn, int classification)
			{
				// find the column definiton
				var columnInfo = columnInfos.FirstOrDefault(c => c.Equals(virtualColumn, classification));
				if (columnInfo == null) // create new column info
					columnInfos.Add(columnInfo = new ObjectViewColumn(virtualColumn, classification));

				return columnInfo.ColumnAlias;
			} // func RegisterColumn

			private string CreateSqlOrder(string identifier, bool negate)
				=> RegisterColumn(identifier, 0) + (negate ? " DESC" : " ASC");

			public void ApplyFilter(PpsDataFilterExpression filter)
			{
				if (filter is PpsDataFilterTrueExpression)
					whereCondition = null;
				else
					whereCondition = new ObjectViewFilterVisitor(this).CreateFilter(filter);
			} // proc ApplyFilter

			public void ApplyOrder(IEnumerable<PpsDataOrderExpression> orders)
			{
				if (orders == null)
					orderCondition = null;
				else
					orderCondition = String.Join(",", from o in orders where !String.IsNullOrEmpty(o.Identifier) select CreateSqlOrder(o.Identifier, o.Negate));
			} // proc ApplyOrder

			public void ApplyLimit(long startAt, long count)
			{
				this.limitStart = startAt;
				this.limitCount = count;
			} // proc ApplyLimit

			private IEnumerable<ObjectViewColumn> GetAllKeyColumns()
				=> columnInfos.Where(c => c.Type == ObjectViewColumnType.Key);

			private SQLiteCommand CreateCommand()
			{
				// build complete sql expression
				var cmd = new StringBuilder();

				cmd.Append("SELECT ");

				// generate static columns
				for (var i = 0; i < ObjectViewEnumerator.staticColumns.Length; i++)
				{
					cmd.Append(ObjectViewEnumerator.staticColumnExpr[i])
						.Append(" AS ")
						.Append(ColumnStaticPrefix).Append(ObjectViewEnumerator.staticColumns[i].Name)
						.Append(',');
				}

				// append multi-value column
				cmd.Append("group_concat(s_all.Key || ':' || s_all.Class || '=' || replace(s_all.Value, char(10), ' '), char(10)) as [Values]");

				// generate dynamic columns
				foreach (var c in GetAllKeyColumns())
				{
					cmd.Append(',')
						.Append(c.CreateWhereExpression());
				}

				cmd.AppendLine().Append("FROM main.[Objects] o");

				// create left outer joins
				foreach (var c in columnInfos)
				{
					if (c.Type != ObjectViewColumnType.Static)
						cmd.AppendLine().Append(c.CreateLeftOuterJoinExpression());
				}

				// add the where condition
				if (!String.IsNullOrEmpty(whereCondition))
					cmd.AppendLine().Append("WHERE ").Append(whereCondition);

				// create the group by
				cmd.AppendLine().Append("GROUP BY ");
				var first = true;
				foreach (var c in ObjectViewEnumerator.staticColumns)
				{
					if (first)
						first = false;
					else
						cmd.Append(',');
					cmd.Append(ColumnStaticPrefix).Append(c.Name);
				}
				foreach (var c in GetAllKeyColumns())
				{
					cmd.Append(',');
					cmd.Append(c.ColumnAlias);
				}

				// order by condition
				cmd.AppendLine();
				if (String.IsNullOrEmpty(orderCondition))
					orderCondition = ColumnStaticPrefix + "Id DESC";
				cmd.Append("ORDER BY ").Append(orderCondition);

				// add limit
				if (limitStart != -1 || limitCount != -1)
				{
					if (limitCount < 0)
						limitCount = 0;

					cmd.Append(" LIMIT ").Append(limitCount);
					if (limitStart > 0)
						cmd.Append(" OFFSET ").Append(limitStart);
				}

				var sqlCommand = cmd.ToString();
				return new SQLiteCommand(sqlCommand, localStoreConnection);
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

			gn.ApplyFilter(arguments.Filter);
			gn.ApplyOrder(arguments.Order);
			gn.ApplyLimit(arguments.Start, arguments.Count);

			return gn;
		} // func CreateObjectFilter

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
