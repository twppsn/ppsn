using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Data;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;

namespace TecWare.PPSn
{
	#region -- interface IPpsLocalStoreTransaction --------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public interface IPpsLocalStoreTransaction : IDbTransaction
	{
		/// <summary>Create a new object.</summary>
		/// <param name="serverId"></param>
		/// <param name="guid"></param>
		/// <param name="typ"></param>
		/// <param name="nr"></param>
		/// <param name="isRev"></param>
		/// <param name="remoteRevId"></param>
		/// <returns></returns>
		long Create(long serverId, Guid guid, string typ, string nr, bool isRev, long remoteRevId);

		/// <summary></summary>
		/// <param name="localId"></param>
		/// <param name="serverId"></param>
		/// <param name="pulledRevId"></param>
		/// <param name="nr"></param>
		void Update(long localId, long serverId, long pulledRevId, string nr);
		/// <summary>Write document data in the local store.</summary>
		/// <param name="trans"></param>
		/// <param name="localId"></param>
		/// <param name="serverId"></param>
		/// <param name="pulledRevId"></param>
		/// <param name="nr"></param>
		/// <param name="data"></param>
		void UpdateData(long localId, Action<Stream> data, long serverId = -1, long pulledRevId = -1, string nr = null, bool isDocumentChanged = true);

		/// <summary>Read data from the object.</summary>
		/// <param name="localId"></param>
		/// <returns></returns>
		Stream GetData(long localId);

		/// <summary></summary>
		/// <param name="localId"></param>
		void Delete(long localId);

		/// <summary>Refresh the meta data/tags of a local store object</summary>
		/// <param name="localId"></param>
		/// <param name="tags"></param>
		void UpdateTags(long localId, IEnumerable<PpsObjectTag> tags);
		/// <summary>Refresh the meta data/tags of a local store object</summary>
		/// <param name="localId"></param>
		/// <param name="tags"></param>
		void UpdateTags(long localId, LuaTable tags);

		/// <summary>Read a DataSet from the local store.</summary>
		/// <param name="dataset">DataSet</param>
		/// <param name="guid"></param>
		/// <returns></returns>
		bool ReadDataSet(long localId, PpsDataSet dataset);
		/// <summary>Refresh a dataset in the local store.</summary>
		/// <param name="dataset"></param>
		void UpdateDataSet(long localId, PpsDataSet dataset);

		/// <summary>Access to the core transaction.</summary>
		IDbTransaction Transaction { get; }
	} // interface IPpsLocalStoreTransaction

	#endregion

	#region -- class PpsObjectLinks -----------------------------------------------------

	public sealed class PpsObjectLinks : IList, IList<PpsObject>, INotifyCollectionChanged
	{
		public PpsObject this[int index]
		{
			get
			{
				throw new NotImplementedException();
			}

			set
			{
				throw new NotImplementedException();
			}
		}

		object IList.this[int index]
		{
			get
			{
				throw new NotImplementedException();
			}

			set
			{
				throw new NotImplementedException();
			}
		}

		public int Count
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		public bool IsReadOnly
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		int ICollection.Count
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		bool IList.IsFixedSize
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		bool IList.IsReadOnly
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		bool ICollection.IsSynchronized
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		object ICollection.SyncRoot
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		public event NotifyCollectionChangedEventHandler CollectionChanged;

		public void Add(PpsObject item)
		{
			throw new NotImplementedException();
		}

		public void Clear()
		{
			throw new NotImplementedException();
		}

		public bool Contains(PpsObject item)
		{
			throw new NotImplementedException();
		}

		public void CopyTo(PpsObject[] array, int arrayIndex)
		{
			throw new NotImplementedException();
		}

		public IEnumerator<PpsObject> GetEnumerator()
		{
			throw new NotImplementedException();
		}

		public int IndexOf(PpsObject item)
		{
			throw new NotImplementedException();
		}

		public void Insert(int index, PpsObject item)
		{
			throw new NotImplementedException();
		}

		public bool Remove(PpsObject item)
		{
			throw new NotImplementedException();
		}

		public void RemoveAt(int index)
		{
			throw new NotImplementedException();
		}

		int IList.Add(object value)
		{
			throw new NotImplementedException();
		}

		void IList.Clear()
		{
			throw new NotImplementedException();
		}

		bool IList.Contains(object value)
		{
			throw new NotImplementedException();
		}

		void ICollection.CopyTo(Array array, int index)
		{
			throw new NotImplementedException();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			throw new NotImplementedException();
		}

		int IList.IndexOf(object value)
		{
			throw new NotImplementedException();
		}

		void IList.Insert(int index, object value)
		{
			throw new NotImplementedException();
		}

		void IList.Remove(object value)
		{
			throw new NotImplementedException();
		}

		void IList.RemoveAt(int index)
		{
			throw new NotImplementedException();
		}
	} // class PpsObjectLinks

	#endregion

	#region -- class PpsObject ----------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsObject : DynamicDataRow, INotifyPropertyChanged
	{
		private static readonly IDataColumn[] staticColumns;

		public event PropertyChangedEventHandler PropertyChanged;

		private readonly PpsMainEnvironment environment;
		private readonly PpsObjectColumns columns;
		private readonly long localId;
		private readonly object objectLock = new object();

		private long serverId;
		private Guid guid;
		private string typ;
		private string nr;
		private bool isRev;
		private long remoteRevId;
		private long pulledRevId;
		private bool isDocumentChanged;
		private bool hasData;

		private bool isTagsLoaded = false;
		private readonly List<PropertyValue> tags = new List<PropertyValue>();

		private readonly PpsObjectLinks links;

		#region -- Ctor/Dtor --------------------------------------------------------------

		/// <summary></summary>
		/// <param name="environment"></param>
		/// <param name="localId"></param>
		internal PpsObject(PpsMainEnvironment environment, IDataReader r)
		{
			this.environment = environment;
			this.localId = r.GetInt64(0);
			ReadObjectInfo(r);

			this.columns = new PpsObjectColumns(this);
		} // ctor

		private void ReadObjectInfo(IDataReader r)
		{
			Set(ref serverId, r.IsDBNull(1) ? -1 : r.GetInt64(1), nameof(ServerId));
			Set(ref guid, r.GetGuid(2), nameof(Guid));
			Set(ref typ, r.GetString(3), nameof(Typ));
			Set(ref nr, r.IsDBNull(4) ? null : r.GetString(4), nameof(Nr));
			Set(ref isRev, r.GetBoolean(5), nameof(IsRev));
			Set(ref remoteRevId, r.IsDBNull(6) ? -1 : r.GetInt64(6), nameof(RemoteRevId));
			Set(ref pulledRevId, r.IsDBNull(7) ? -1 : r.GetInt64(7), nameof(PulledRevId));
			Set(ref isDocumentChanged, r.IsDBNull(8) ? false : r.GetBoolean(8), nameof(IsDocumentChanged));
			Set(ref hasData, !r.IsDBNull(9), nameof(HasData));

			// check for tags
			if (r.FieldCount >= staticColumns.Length && !r.IsDBNull(staticColumns.Length))
				RefreshTagsFromString(r.GetString(staticColumns.Length));
		} // proc ReadObjectInfo

		/// <summary>Refresh of the object data.</summary>
		/// <param name="transaction">Optional transaction.</param>
		public void Refresh(bool withTags = false, IPpsLocalStoreTransaction transaction = null)
		{
			// refresh core data
			lock (objectLock)
			using (var cmd = environment.LocalConnection.CreateCommand())
			{
				cmd.CommandText = StaticColumnsSelect + " WHERE o.Id = @Id";
				cmd.Transaction = (SQLiteTransaction)transaction.Transaction;
				cmd.Parameters.Add("@Id", DbType.Int64).Value = localId;

				using (var r = cmd.ExecuteReader(CommandBehavior.SingleRow))
				{
					if (r.Read())
						ReadObjectInfo(r);
					else
						throw new InvalidOperationException("No result set.");
				}
			}

			// refresh tags
			if (isTagsLoaded || withTags)
				RefreshTags(transaction);
		} // proc Refresh
		
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

		private void RefreshTagsFromString(string tagList)
		{
			RefreshTags(tagList
					.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
					.Select(c => CreateKeyValue(c))
			);
		} // proc RefreshTagsFromString

		private void RefreshTags(IPpsLocalStoreTransaction transaction = null)
		{
		} // proc RefreshTags

		private void RefreshTags(IEnumerable<PropertyValue> newTags)
		{
			lock (objectLock)
			{
				foreach (var t in newTags)
				{
					var idx = tags.FindIndex(c => String.Compare(c.Name, t.Name, StringComparison.OrdinalIgnoreCase) == 0);
					if (idx == -1)
					{
						tags.Add(t);
						OnPropertyChanged(t.Name);
					}
					else if (!Object.Equals(tags[idx].Value, t.Value))
					{
						tags[idx] = t;
						OnPropertyChanged(t.Name);
					}
				}

				// mark tags as loaded
				isTagsLoaded = true;
			}
		} // proc RefreshTags

		public override string ToString()
			=> $"Object: {typ}; {localId} # {guid}:{pulledRevId}";

		#endregion

		#region -- Properties -------------------------------------------------------------

		private void OnPropertyChanged(string propertyName)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		private void Set<T>(ref T field, T newValue, string propertyName)
		{
			if (!Object.Equals(field, newValue))
			{
				field = newValue;
				OnPropertyChanged(propertyName);
			}
		} // func Set

		#endregion

		#region -- DataRow implementation -------------------------------------------------

		#region -- class PpsObjectColumns -------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class PpsObjectColumns : IReadOnlyList<IDataColumn>
		{
			private readonly PpsObject obj;

			public PpsObjectColumns(PpsObject obj)
			{
				this.obj = obj;
			} // ctor

			public IEnumerator<IDataColumn> GetEnumerator()
				=> staticColumns.Concat(obj.Tags.Select(c => CreateSimpleDataColumn(c))).GetEnumerator();

			IEnumerator IEnumerable.GetEnumerator()
				=> GetEnumerator();

			public IDataColumn this[int index]
			{
				get
				{
					lock (obj.objectLock)
					{
						if (index < 0)
							throw new ArgumentOutOfRangeException();
						else if (index < staticColumns.Length)
							return staticColumns[index];
						else if (index < staticColumns.Length + obj.Tags.Count)
						{
							var tag = obj.tags[index - staticColumns.Length];
							return CreateSimpleDataColumn(tag);
						}
						else
							throw new ArgumentOutOfRangeException();
					}
				}
			} // prop this

			private static SimpleDataColumn CreateSimpleDataColumn(PropertyValue tag)
				=> new SimpleDataColumn(tag.Name, tag.Type);

			public int Count => staticColumns.Length + obj.Tags.Count;
		} // class PpsObjectColumns

		#endregion

		public override IReadOnlyList<IDataColumn> Columns => columns;

		public override object this[int index]
		{
			get
			{
				lock (objectLock)
				{
					if (index < 0)
						throw new ArgumentOutOfRangeException();
					else if (index < staticColumns.Length)
					{
						switch (index)
						{
							case 0:
								return localId;
							case 1:
								return serverId;
							case 2:
								return guid;
							case 3:
								return typ;
							case 4:
								return nr;
							case 5:
								return remoteRevId;
							case 6:
								return pulledRevId;
							case 7:
								return isDocumentChanged;
							case 8:
								return hasData;
							default:
								return null;
						}
					}
					else if (index < staticColumns.Length + Tags.Count)
						return tags[index - staticColumns.Length].Value;
					else
						throw new ArgumentOutOfRangeException();
				}
			}
		} // prop this

		#endregion

		public override bool IsDataOwner => true;

		public long LocalId => localId;
		public long ServerId => serverId;
		public Guid Guid => guid;
		public string Typ => typ;
		public string Nr => nr;
		public bool IsRev => isRev;
		public long RemoteRevId => remoteRevId;
		public long PulledRevId => pulledRevId;
		public bool IsDocumentChanged => isDocumentChanged;
		public bool HasData => hasData;

		private IReadOnlyList<PropertyValue> Tags
		{
			get
			{
				lock (objectLock)
				{
					if (!isTagsLoaded)
						RefreshTags();
					return tags;
				}
			}
		} // prop Tags

		private static Type TypeFromClassHint(int classHint)
		{
			if (classHint == 2)
				return typeof(DateTime);
			// todo:
			return typeof(string);
		} // func TypeFromClassHint

		static PpsObject()
		{
			staticColumns = new IDataColumn[]
			{
				new SimpleDataColumn("Id", typeof(long)),
				new SimpleDataColumn("ServerId", typeof(long)),
				new SimpleDataColumn("Guid", typeof(Guid)),
				new SimpleDataColumn("Typ", typeof(string)),
				new SimpleDataColumn("Nr", typeof(string)),
				new SimpleDataColumn("IsRev", typeof(bool)),
				new SimpleDataColumn("RemoteRevId", typeof(long)),
				new SimpleDataColumn("PulledRevId", typeof(long)),
				new SimpleDataColumn("IsDocumentChanged", typeof(bool)),
				new SimpleDataColumn("HasData", typeof(bool))
			};

			StaticColumnExpressions = new string[]
			{
				"o.Id",
				"o.ServerId",
				"o.Guid",
				"o.Typ",
				"o.Nr",
				"o.IsRev",
				"o.RemoteRevId",
				"o.PulledRevId",
				"o.DocumentIsChanged",
				"length(o.Document)"
			};

#if DEBUG
			if (StaticColumnExpressions.Length != staticColumns.Length)
				throw new ArgumentOutOfRangeException("columns");
#endif

			StaticColumnsSelect = "SELECT " + String.Join(",", StaticColumnExpressions) + " FROM main.[Objects]";
		} // ctor

		internal static IDataColumn[] StaticColumns => staticColumns;
		internal static string[] StaticColumnExpressions { get; }
		internal static string StaticColumnsSelect { get; }
	} // class PpsObject

	#endregion

	#region -- class PpsMainEnvironment -------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public partial class PpsMainEnvironment
	{
		#region -- class PpsLocalStoreTransaction -----------------------------------------

		private sealed class PpsLocalStoreTransaction : IPpsLocalStoreTransaction
		{
			private readonly PpsMainEnvironment environment;
			private readonly SQLiteTransaction transaction;
			private bool isDisposed = false;

			#region -- Ctor/Dtor/Commit -----------------------------------------------------

			public PpsLocalStoreTransaction(PpsMainEnvironment environment)
			{
				this.environment = environment;
				this.transaction = LocalConnection.BeginTransaction();
			} // ctor

			public void Dispose()
				=> Dispose(true);

			private void Dispose(bool disposing)
			{
				if (!isDisposed)
				{
					if (disposing)
						transaction.Dispose();

					isDisposed = true;
				}
			} // proc Dispose

			public void Commit()
			{
				if (!isDisposed)
					transaction.Commit();
				else
					throw new InvalidOperationException();
			} // proc Commit

			public void Rollback()
			{
				if (!isDisposed)
					transaction.Rollback();
				else
					throw new InvalidOperationException();
			} // proc Rollback

			#endregion

			private static bool DbNullOnNeg(long value)
				=> value < 0;

			public long Create(long serverId, Guid guid, string typ, string nr, bool isRev, long remoteRevId)
			{
				using (var cmd = LocalConnection.CreateCommand())
				{
					cmd.CommandText = "INSERT INTO main.Objects (ServerId, Guid, Typ, Nr, IsRev, RemoteRevId) VALUES (@ServerId, @Guid, @Typ, @Nr, @IsRev, @RemoteRevId)";
					cmd.Transaction = transaction;

					cmd.Parameters.Add("@ServerId", DbType.Int64).Value = serverId.DbNullIf(DbNullOnNeg);
					cmd.Parameters.Add("@Guid", DbType.Guid).Value = guid;
					cmd.Parameters.Add("@Typ", DbType.String).Value = typ.DbNullIfString();
					cmd.Parameters.Add("@Nr", DbType.String).Value = nr.DbNullIfString();
					cmd.Parameters.Add("@IsRev", DbType.Boolean).Value = isRev;
					cmd.Parameters.Add("@RemoteRevId", DbType.Int64).Value = remoteRevId.DbNullIf(DbNullOnNeg);

					cmd.ExecuteNonQuery();

					return LocalConnection.LastInsertRowId;
				}
			} // func Create

			public void Update(long localId, long serverId, long pulledRevId, string nr)
			{
				using (var cmd = LocalConnection.CreateCommand())
				{
					cmd.CommandText = "UPDATE main.Objects SET ServerId = @ServerId, PulledRevId = @PulledRevId, Nr = @Nr WHERE Id = @Id";
					cmd.Transaction = transaction;

					cmd.Parameters.Add("@Id", DbType.Int64).Value = localId;
					cmd.Parameters.Add("@ServerId", DbType.Int64).Value = serverId.DbNullIf(DbNullOnNeg);
					cmd.Parameters.Add("@PulledRevId", DbType.Int64).Value = pulledRevId.DbNullIf(DbNullOnNeg);
					cmd.Parameters.Add("@Nr", DbType.String).Value = nr.DbNullIfString();

					cmd.ExecuteNonQuery();
				}
			} // proc Update

			public void UpdateData(long localId, Action<Stream> data, long serverId, long pulledRevId, string nr, bool isDocumentChanged)
			{
				byte[] bData = null;

				// read the data into a memory stream
				if (data != null)
				{
					using (var dst = new MemoryStream())
					{
						data(dst);
						dst.Position = 0;
						bData = dst.ToArray();
					}
				}

				// store the value
				using (var cmd = LocalConnection.CreateCommand())
				{
					cmd.CommandText = "UPDATE main.Objects SET ServerId = IFNULL(@ServerId, ServerId), PulledRevId = IFNULL(@PulledRevId, PulledRevId), Nr = IFNULL(@Nr, Nr), Document = @Document, DocumentIsChanged = @DocumentIsChanged WHERE Id = @Id";
					cmd.Transaction = transaction;

					cmd.Parameters.Add("@Id", DbType.Int64).Value = localId;
					cmd.Parameters.Add("@ServerId", DbType.Int64).Value = serverId.DbNullIf(DbNullOnNeg);
					cmd.Parameters.Add("@PulledRevId", DbType.Int64).Value = bData == null ? DBNull.Value : pulledRevId.DbNullIf(DbNullOnNeg);
					cmd.Parameters.Add("@Nr", DbType.String).Value = nr.DbNullIfString();
					cmd.Parameters.Add("@Document", DbType.Binary).Value = bData == null ? (object)DBNull.Value : bData;
					cmd.Parameters.Add("@DocumentIsChanged", DbType.Boolean).Value = bData == null ? false : isDocumentChanged;

					cmd.ExecuteNonQuery();
				}
			} // proc UpdateData

			public Stream GetData(long localId)
			{
				using (var cmd = LocalConnection.CreateCommand())
				{
					cmd.CommandText = "SELECT Document, length(Document) FROM main.Objects WHERE Id = @Id";
					cmd.Transaction = transaction;

					cmd.Parameters.Add("@Id", DbType.Int64).Value = localId;

					using (var r = cmd.ExecuteReader(CommandBehavior.SingleRow))
					{
						if (r.Read() && !r.IsDBNull(0))
						{
							var data = new byte[r.GetInt64(1)];

							r.GetBytes(0, 0, data, 0, data.Length);

							return new MemoryStream(data, false);
						}
						else
							return null;
					}
				}
			} // func GetData

			public void UpdateTags(long localId, IEnumerable<PpsObjectTag> tags)
			{
				using (var tagUpdater = new TagDatabaseCommands(LocalConnection))
				{
					tagUpdater.ObjectId = localId;
					tagUpdater.Transaction = transaction;

					IList<PpsObjectTag> t;
					if (tags is PpsObjectTag[])
						t = (PpsObjectTag[])tags;
					else if (tags is IList<PpsObjectTag>)
						t = (IList<PpsObjectTag>)tags;
					else
						t = tags.ToArray();

					tagUpdater.UpdateTags(t);
				}
			} // proc UpdateTags

			public void UpdateTags(long localId, LuaTable tags)
			{
				var tagList = new List<PpsObjectTag>();
				foreach (var c in tags.Members)
				{
					if (c.Key[0] == '_')
						continue;

					var v = c.Value;
					var t = PpsObjectTagClass.Text;
					var tString = tags.GetOptionalValue<string>("_" + c.Key, null);
					if (tString != null)
					{
						if (String.Compare(tString, "Number", StringComparison.OrdinalIgnoreCase) == 0)
							t = PpsObjectTagClass.Number;
						else if (String.Compare(tString, "Date", StringComparison.OrdinalIgnoreCase) == 0)
							t = PpsObjectTagClass.Date;
					}
					else if (v is DateTime)
						t = PpsObjectTagClass.Date;

					tagList.Add(new PpsObjectTag(c.Key, t, c.Value));
				}

				UpdateTags(localId, tagList);
			} // proc UpdateTags

			public void Delete(long localId)
			{
				using (var cmd = LocalConnection.CreateCommand())
				{
					cmd.CommandText = "DELETE FROM main.Objects WHERE Id = @Id;";
					cmd.Transaction = transaction;

					cmd.Parameters.Add("@Id", DbType.Int64).Value = localId;

					cmd.ExecuteNonQuery();
				}
			} // func Delete

			public bool ReadDataSet(long localId, PpsDataSet dataset)
			{
				var src = GetData(localId);
				if (src == null)
					return false;

				using (var xml = XmlReader.Create(src, Procs.XmlReaderSettings))
					dataset.Read(XDocument.Load(xml).Root);

				return true;
			} // func ReadDataSet

			public void UpdateDataSet(long localId, PpsDataSet dataset)
			{
				// update data
				UpdateData(localId,
					dst =>
					{
						var settings = Procs.XmlWriterSettings;
						settings.CloseOutput = false;
						using (var xml = XmlWriter.Create(dst, settings))
							dataset.Write(xml);
					}, -1, -1, null, true
				);

				// update tags
				UpdateTags(localId, dataset.GetAutoTags());
			} // func UpdateDataSet

			public IDbConnection Connection => LocalConnection;
			public IsolationLevel IsolationLevel => transaction.IsolationLevel;
			public IDbTransaction Transaction => transaction;

			private SQLiteConnection LocalConnection => environment.LocalConnection;
		} // class PpsLocalStoreTransaction

		#endregion

		// point of improvement: a structure equal to LuaTable-Hash should be created on perf. issues
		private readonly object objectStoreLock = new object();
		private readonly List<WeakReference<PpsObject>> objectStore = new List<WeakReference<PpsObject>>();
		private readonly Dictionary<long, int> objectStoreById = new Dictionary<long, int>();
		private readonly Dictionary<Guid, int> objectStoreByGuid = new Dictionary<Guid, int>();

		private const bool UseId = false;
		private const bool UseGuid = true;

		#region -- CreateObjectFilter -----------------------------------------------------

		#region -- class PpsObjectEnumerator -----------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class PpsObjectEnumerator : IEnumerator<PpsObject>, IDataColumns
		{
			private readonly PpsMainEnvironment environment;
			private readonly SQLiteCommand command;
			private SQLiteDataReader reader;
			private PpsObject current;

			public PpsObjectEnumerator(PpsMainEnvironment environment, SQLiteCommand command)
			{
				if (command == null)
					throw new ArgumentNullException("command");

				this.environment = environment;
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
					current = environment.GetCachedObjectOrCreate(reader);
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

			public PpsObject Current => current;
			object IEnumerator.Current => current;

			public IReadOnlyList<IDataColumn> Columns => PpsObject.StaticColumns;
		} // class PpsObjectEnumerator

		#endregion

		#region -- class PpsObjectGenerator -----------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class PpsObjectGenerator : IEnumerable<IDataRow>
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
				private readonly PpsObjectGenerator owner;

				public ObjectViewFilterVisitor(PpsObjectGenerator owner)
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

			private readonly PpsMainEnvironment environment;
			private readonly SQLiteConnection localStoreConnection;

			private List<ObjectViewColumn> columnInfos = new List<ObjectViewColumn>();
			private string whereCondition = null;
			private string orderCondition = null;
			private long limitStart = -1;
			private long limitCount = -1;

			public PpsObjectGenerator(PpsMainEnvironment environment, SQLiteConnection localStoreConnection)
			{
				this.environment = environment;
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
				for (var i = 0; i < PpsObject.StaticColumns.Length; i++)
				{
					cmd.Append(PpsObject.StaticColumnExpressions[i])
						.Append(" AS ")
						.Append(ColumnStaticPrefix).Append(PpsObject.StaticColumns[i].Name)
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
				foreach (var c in PpsObject.StaticColumns)
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
				=> new PpsObjectEnumerator(environment, CreateCommand());

			IEnumerator IEnumerable.GetEnumerator()
				=> GetEnumerator();
		} // class PpsObjectGenerator

		#endregion

		private IEnumerable<IDataRow> CreateObjectFilter(PpsShellGetList arguments)
		{
			var gn = new PpsObjectGenerator(this, LocalConnection);

			gn.ApplyFilter(arguments.Filter);
			gn.ApplyOrder(arguments.Order);
			gn.ApplyLimit(arguments.Start, arguments.Count);

			return gn;
		} // func CreateObjectFilter

		#endregion

		#region -- Object Cache -----------------------------------------------------------

		private PpsObject ReadObject(object key, bool useGuid, IPpsLocalStoreTransaction transaction)
		{
			// refresh core data
			using (var cmd = LocalConnection.CreateCommand())
			{
				cmd.CommandText = PpsObject.StaticColumnsSelect + (useGuid ? " WHERE o.Guid = @Guid" : " WHERE o.Id = @Id");
				cmd.Transaction = (SQLiteTransaction)transaction.Transaction;
				if (useGuid)
					cmd.Parameters.Add("@Guid", DbType.Guid).Value = key;
				else
					cmd.Parameters.Add("@Id", DbType.Int64).Value = key;

				using (var r = cmd.ExecuteReader(CommandBehavior.SingleRow))
				{
					if (r.Read())
						return UpdateCacheItem(new PpsObject(this, r));
					else
						return null;
				}
			}
		} // func ReadObject

		private bool IsEmptyObject(WeakReference<PpsObject> c)
		{
			if (c == null)
				return true;
			PpsObject o;
			return !c.TryGetTarget(out o);
		} // func IsEmptyObject

		private PpsObject UpdateCacheItem(PpsObject obj)
		{
			int cacheIndex;
			// find a cache index
			if (!objectStoreByGuid.TryGetValue(obj.Guid, out cacheIndex) && !objectStoreById.TryGetValue(obj.LocalId, out cacheIndex))
			{
				cacheIndex = objectStore.FindIndex(c => IsEmptyObject(c));
				if (cacheIndex == -1)
				{
					cacheIndex = objectStore.Count;
					objectStore.Add(null);
				}
			}

			// set cache
			objectStore[cacheIndex] = new WeakReference<PpsObject>(obj);
			objectStoreById[obj.LocalId] = cacheIndex;
			objectStoreByGuid[obj.Guid] = cacheIndex;

			return obj;
		} // func UpdateCacheItem

		private PpsObject GetCachedObject<T>(Dictionary<T, int> index, T key)
		{
			int cacheIndex;
			if (index.TryGetValue(key, out cacheIndex))
			{
				var reference = objectStore[cacheIndex];
				if (reference != null)
				{
					PpsObject obj;
					if (reference.TryGetTarget(out obj))
						return obj;
					else
					{
						objectStore[cacheIndex] = null;
						index.Remove(key);
					}
				}
				else
					index.Remove(key);
			}
			return null;
		} // func GetCachedObject

		private PpsObject GetCachedObjectOrRead<T>(Dictionary<T, int> index, T key, bool keyIsGuid, IPpsLocalStoreTransaction transaction = null)
		{
			lock (objectStoreLock)
			{
				// check if the object is in memory
				return GetCachedObject(index, key)
					// object is not in memory, create a instance
					?? ReadObject(key, keyIsGuid, transaction);
			}
		} // func GetCachedObject

		private PpsObject GetCachedObjectOrCreate(SQLiteDataReader reader)
		{
			var localId = reader.GetInt64(0);
			lock (objectStoreLock)
			{
				return GetCachedObject(objectStoreById, localId)
					?? UpdateCacheItem(new PpsObject(this, reader));
			}
		} // func GetCachedObjectOrCreate

		[LuaMember]
		public PpsObject GetObject(long localId, IPpsLocalStoreTransaction transaction = null)
			=> GetCachedObjectOrRead(objectStoreById, localId, UseId, transaction);

		[LuaMember]
		public PpsObject GetObject(Guid guid, IPpsLocalStoreTransaction transaction = null)
			=> GetCachedObjectOrRead(objectStoreByGuid, guid, UseGuid, transaction);

		#endregion

		[LuaMember]
		public IPpsLocalStoreTransaction BeginLocalStoreTransaction()
			=> new PpsLocalStoreTransaction(this);
	} // class PpsMainEnvironment

	#endregion
}
