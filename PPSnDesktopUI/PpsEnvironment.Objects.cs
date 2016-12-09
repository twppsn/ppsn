using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
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
	#region -- enum PpsObjectServerIndex ------------------------------------------------

	internal enum PpsObjectServerIndex : int
	{
		Id = 0,
		Guid,
		Typ,
		Nr,
		IsRev,
		SyncToken,
		RevId,
		Tags,
		Last = Tags
	} // enum PpsObjectServerIndex

	#endregion

	#region -- enum PpsObjectLinkRestriction --------------------------------------------

	public enum PpsObjectLinkRestriction
	{
		Null,
		Restrict,
		Delete,
		Deleted
	} // enum PpsObjectLinkRestriction

	#endregion

	#region -- class PpsNestedDatabaseTransaction ---------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	internal class PpsNestedDatabaseTransaction : IDbTransaction
	{
		private readonly SQLiteTransaction otherTransaction;
		private readonly SQLiteTransaction nestedTransaction;

		internal PpsNestedDatabaseTransaction(SQLiteConnection connection, SQLiteTransaction transaction)
		{
			if (transaction != null)
			{
				this.otherTransaction = transaction;
				this.nestedTransaction = null;
			}
			else
			{
				this.otherTransaction = null;
				this.nestedTransaction = connection.BeginTransaction();
			}
		} // ctor

		public void Dispose()
			=> nestedTransaction?.Dispose();

		public void Commit()
			=> nestedTransaction?.Commit();

		public void Rollback()
			=> nestedTransaction?.Rollback();

		IDbConnection IDbTransaction.Connection => Connection;
		public SQLiteConnection Connection => Transaction.Connection;
		public IsolationLevel IsolationLevel => Transaction.IsolationLevel;
		public SQLiteTransaction Transaction => otherTransaction ?? nestedTransaction;
	} // class PpsDatabaseTransaction

	#endregion

	#region -- class PpsObjectLink ------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsObjectLink
	{
		private readonly PpsObject parent;
		private readonly long localId;
		private readonly long serverId;
		private readonly long linkToObj;   // id of the linked object
		private readonly long syncToken;
		private PpsObjectLinkRestriction onDelete;  // is delete cascade possible

		private WeakReference<PpsObject> linkToCache; // weak ref to the actual object

		internal PpsObjectLink(PpsObject parent, long localId, long serverId, long linkTo, PpsObjectLinkRestriction onDelete, long syncToken)
		{
			this.parent = parent;
			this.localId = localId;
			this.serverId = serverId;
			this.linkToObj = linkTo;
			this.onDelete = onDelete;
			this.syncToken = syncToken;
			this.linkToCache = null;
		} // ctor

		private PpsObject GetLinkedObject()
		{
			PpsObject r;
			if (linkToCache != null && linkToCache.TryGetTarget(out r))
				return r;

			r = parent.Environment.GetObject(linkToObj);
			linkToCache = new WeakReference<PPSn.PpsObject>(r);
			return r;
		} // func GetLinkedObject

		public PpsObject ParentObject => parent;

		public long LocalId => localId;
		public long ServerId => serverId;
		public long ObjectId => linkToObj;
		public long SyncToken => syncToken;
		public PpsObjectLinkRestriction OnDelete => onDelete;

		public PpsObject Object => GetLinkedObject();
	} // class PpsObjectLink

	#endregion

	#region -- class PpsObjectLinks -----------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsObjectLinks : IList, IReadOnlyList<PpsObjectLink>, INotifyCollectionChanged
	{
		#region -- class LinkCommand ------------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private class LinkCommand : IDisposable
		{
			protected readonly SQLiteCommand command;

			public LinkCommand(SQLiteTransaction trans)
			{
				this.command = trans.Connection.CreateCommand();
				this.command.Transaction = trans;
			} // ctor

			public void Dispose()
			{
				command?.Dispose();
			} // proc Dispose
		} // class LinkCommand

		#endregion

		#region -- class LinkSelectCommand ------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class LinkSelectCommand : LinkCommand
		{
			private readonly SQLiteParameter objectIdParameter;

			public LinkSelectCommand(SQLiteTransaction trans, bool visibleOnly)
				: base(trans)
			{
				this.command.CommandText =
					"SELECT [Id], [ServerId], [LinkObjectId], [OnDelete], [SyncToken] FROM main.[ObjectLinks] WHERE [ParentObjectId] = @ObjectId" + (visibleOnly ? " AND [Class] <> '-'" : ";");

				this.objectIdParameter = command.Parameters.Add("@ObjectId", DbType.Int64);

				this.command.Prepare();
			} // ctor

			public IEnumerable<PpsObjectLink> Select(PpsObject parentObject, long objectId)
			{
				objectIdParameter.Value = objectId;

				using (var r = command.ExecuteReader(CommandBehavior.SingleResult))
				{
					while (r.Read())
					{
						yield return new PpsObjectLink(parentObject,
							r.GetInt64(0),
							r.GetInt64(1),
							r.GetInt64(2),
							ParseObjectLinkRestriction(r.GetString(3)),
							r.GetInt64(4)
						);
					}
				}
			}// func Select
		} // class LinkSelectCommand

		#endregion

		/// <summary></summary>
		public event NotifyCollectionChangedEventHandler CollectionChanged;

		private readonly PpsObject parent;

		private readonly object linksLink = new object();
		private bool isLoaded = false; // marks if the link list is loaded from the local store
		private readonly List<PpsObjectLink> links; // local links

		internal PpsObjectLinks(PpsObject parent)
		{
			this.parent = parent;
			this.links = new List<PpsObjectLink>();
		} // ctor

		public int Count
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

		bool IList.IsFixedSize
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

		object ICollection.SyncRoot
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

		public PpsObjectLink this[int index]
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		public IEnumerator<PpsObjectLink> GetEnumerator()
		{
			throw new NotImplementedException();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			throw new NotImplementedException();
		}

		int IList.Add(object value)
		{
			throw new NotImplementedException();
		}

		bool IList.Contains(object value)
		{
			throw new NotImplementedException();
		}

		void IList.Clear()
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

		void ICollection.CopyTo(Array array, int index)
		{
			throw new NotImplementedException();
		}

		internal PpsObjectLinks RefreshLazy()
		{
			throw new NotImplementedException();
		}

		public static PpsObjectLinkRestriction ParseObjectLinkRestriction(string onDelete)
		{
			if (String.IsNullOrEmpty(onDelete))
				return PpsObjectLinkRestriction.Deleted;
			else
				switch (Char.ToUpper(onDelete[0]))
				{
					case 'N':
						return PpsObjectLinkRestriction.Null;
					case 'R':
						return PpsObjectLinkRestriction.Restrict;
					case 'D':
						return PpsObjectLinkRestriction.Delete;
					case '-':
						return PpsObjectLinkRestriction.Deleted;
					default:
						throw new ArgumentOutOfRangeException($"Can not parse '{onDelete}' to an link restriction.");
				}
		} // func ParseObjectLinkRestriction

		public static string FormatObjectLinkRestriction(PpsObjectLinkRestriction onDelete)
		{
			switch (onDelete)
			{
				case PpsObjectLinkRestriction.Null:
					return "N";
				case PpsObjectLinkRestriction.Restrict:
					return "R";
				case PpsObjectLinkRestriction.Delete:
					return "D";
				case PpsObjectLinkRestriction.Deleted:
					return "-";
				default:
					throw new ArgumentOutOfRangeException($"Can not format '{onDelete}'.");
			}
		} // func FormatObjectLinkRestriction
	} // class PpsObjectLinks

	#endregion

	#region -- class PpsObjectTags ------------------------------------------------------

	public sealed class PpsObjectTags : IList, IReadOnlyList<PpsObjectTag>, INotifyCollectionChanged
	{
		#region -- class TagCommand -------------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private class TagCommand : IDisposable
		{
			protected readonly SQLiteCommand command;

			public TagCommand(SQLiteTransaction trans)
			{
				this.command = trans.Connection.CreateCommand();
				this.command.Transaction = trans;
			} // ctor

			public void Dispose()
			{
				command?.Dispose();
			} // proc Dispose
		} // class TagCommand

		#endregion

		#region -- class TagSelectByKeyCommand --------------------------------------------

		private sealed class TagSelectByKeyCommand : TagCommand
		{
			private readonly SQLiteParameter objectIdParameter;
			private readonly SQLiteParameter keyParameter;

			public TagSelectByKeyCommand(SQLiteTransaction trans)
				: base(trans)
			{
				this.command.CommandText = "SELECT [Id] FROM main.[ObjectTags] WHERE [ObjectId] = @ObjectId AND [Key] = @Key;";

				this.objectIdParameter = command.Parameters.Add("@ObjectId", DbType.Int64);
				this.keyParameter = command.Parameters.Add("@Key", DbType.String);

				this.command.Prepare();
			} // ctor

			public long? SelectByKey(long objectId, string key)
			{
				objectIdParameter.Value = objectId;
				keyParameter.Value = key;

				using (var r = command.ExecuteReader(CommandBehavior.SingleRow))
				{
					if (r.Read() && !r.IsDBNull(0))
						return r.GetInt64(0);
					else
						return null;
				}
			}// func Select
		} // class TagSelectByKeyCommand

		#endregion

		#region -- class TagSelectCommand -------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class TagSelectCommand : TagCommand
		{
			private readonly SQLiteParameter objectIdParameter;

			public TagSelectCommand(SQLiteTransaction trans, bool visibleOnly)
				: base(trans)
			{
				this.command.CommandText =
					"SELECT [Id], [Key], [Class], [Value], [SyncToken] FROM main.[ObjectTags] WHERE [ObjectId] = @ObjectId" + (visibleOnly ? " AND [Class] >= 0" : ";");

				this.objectIdParameter = command.Parameters.Add("@ObjectId", DbType.Int64);

				this.command.Prepare();
			} // ctor

			public IEnumerable<Tuple<long, PpsObjectTag>> Select(long objectId)
			{
				objectIdParameter.Value = objectId;

				using (var r = command.ExecuteReader(CommandBehavior.SingleResult))
				{
					while (r.Read())
					{
						var k = r.GetString(1);
						var t = r.GetInt32(2);
						var v = r.IsDBNull(3) ? null : r.GetString(3);
						yield return new Tuple<long, PpsObjectTag>(r.GetInt64(0), new PpsObjectTag(k, (PpsObjectTagClass)t, v, r.GetInt64(4)));
					}
				}
			}// func Select
		} // class TagSelectCommand

		#endregion

		#region -- class TagInsertCommand -------------------------------------------------

		private sealed class TagInsertCommand : TagCommand
		{
			private readonly SQLiteParameter objectIdParameter;
			private readonly SQLiteParameter keyParameter;
			private readonly SQLiteParameter classParameter;
			private readonly SQLiteParameter valueParameter;
			private readonly SQLiteParameter syncTokenParameter;

			public TagInsertCommand(SQLiteTransaction trans)
				: base(trans)
			{
				this.command.CommandText = "INSERT INTO main.[ObjectTags] ([ObjectId], [Key], [Class], [Value], [SyncToken]) values (@ObjectId, @Key, @Class, @Value, @SyncToken);";
				this.objectIdParameter = command.Parameters.Add("@ObjectId", DbType.Int64);
				this.keyParameter = command.Parameters.Add("@Key", DbType.String);
				this.classParameter = command.Parameters.Add("@Class", DbType.Int64);
				this.valueParameter = command.Parameters.Add("@Value", DbType.String);
				this.syncTokenParameter = command.Parameters.Add("@SyncToken", DbType.Int64);

				this.command.Prepare();
			} // ctor

			public long Insert(long objectId, string key, int cls, string value, long syncToken)
			{
				objectIdParameter.Value = objectId;
				keyParameter.Value = key;
				classParameter.Value = cls;
				valueParameter.Value = value;
				syncTokenParameter.Value = syncToken;

				command.ExecuteNonQuery();

				return command.Connection.LastInsertRowId;
			} // func Insert
		} // class TagInsertCommand

		#endregion

		#region -- class TagUpdateCommand -------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class TagUpdateCommand : TagCommand
		{
			private readonly SQLiteParameter idParameter;
			private readonly SQLiteParameter classParameter;
			private readonly SQLiteParameter valueParameter;
			private readonly SQLiteParameter syncTokenParameter;

			public TagUpdateCommand(SQLiteTransaction trans)
				: base(trans)
			{
				this.command.CommandText = "UPDATE main.[ObjectTags] SET [Class] = @Class, [Value] = @Value, [SyncToken] = @syncToken where [Id] = @Id;";

				this.idParameter = command.Parameters.Add("@Id", DbType.Int64);
				this.classParameter = command.Parameters.Add("@Class", DbType.Int64);
				this.valueParameter = command.Parameters.Add("@Value", DbType.String);
				this.syncTokenParameter = command.Parameters.Add("@syncToken", DbType.Int64);

				this.command.Prepare();
			} // ctor

			public void Update(long tagId, int cls, string value, long syncToken)
			{
				idParameter.Value = tagId;
				classParameter.Value = cls;
				valueParameter.Value = value ?? (object)DBNull.Value;
				syncTokenParameter.Value = syncToken;

				command.ExecuteNonQuery();
			} // proc Update
		} // class TagUpdateCommand

		#endregion

		#region -- class TagDeleteCommand -------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class TagDeleteCommand : TagCommand
		{
			private readonly SQLiteParameter idParameter;

			public TagDeleteCommand(SQLiteTransaction trans)
				: base(trans)
			{
				this.command.CommandText = "DELETE FROM main.[ObjectTags] WHERE [Id] = @Id;";

				this.idParameter = command.Parameters.Add("@Id", DbType.Int64);

				this.command.Prepare();
			} // ctor

			public void Delete(long tagId)
			{
				idParameter.Value = tagId;
				command.ExecuteNonQuery();
			} // proc Update
		} // class TagDeleteCommand

		#endregion

		public event NotifyCollectionChangedEventHandler CollectionChanged;

		private readonly PpsObject parent;
		private readonly List<PpsObjectTag> tags;

		private bool isLoaded = false;

		internal PpsObjectTags(PpsObject parent)
		{
			this.parent = parent;
			this.tags = new List<PpsObjectTag>();
		} // ctor

		#region -- Refresh ----------------------------------------------------------------

		internal void RefreshTags(IEnumerable<PpsObjectTag> newTags)
		{
			var isChanged = false;
			lock (parent.SyncRoot)
			{
				foreach (var t in newTags)
				{
					var idx = tags.FindIndex(c => String.Compare(c.Name, t.Name, StringComparison.OrdinalIgnoreCase) == 0);
					if (idx == -1)
					{
						if (t.Class != PpsObjectTagClass.Deleted)
						{
							tags.Add(t);
							parent.OnPropertyChanged(t.Name);
							isChanged = true;
						}
					}
					else if (t.Class == PpsObjectTagClass.Deleted)
					{
						tags.RemoveAt(idx);
						isChanged = true;
					}
					else if (!Object.Equals(tags[idx].Value, t.Value))
					{
						tags[idx] = t;
						parent.OnPropertyChanged(t.Name);
						isChanged = true;
					}
				}

				// mark tags as loaded
				isLoaded = true;
			}
			if (isChanged)
				OnCollectionReset();
		} // proc RefreshTags

		internal void RefreshTagsFromString(string tagList)
		{
			RefreshTags(PpsObjectTag.ParseTagFields(tagList));
		} // proc RefreshTagsFromString

		internal void RefreshTags(SQLiteTransaction transaction = null)
		{
			using (var trans = new PpsNestedDatabaseTransaction(parent.Environment.LocalConnection, transaction))
			using (var cmd = new TagSelectCommand(trans.Transaction, true))
			{
				RefreshTags(cmd.Select(parent.LocalId).Select(c => c.Item2));
				trans.Rollback();
			}
		} // proc RefreshTags

		internal void CheckTagsLoaded(SQLiteTransaction transaction)
		{
			lock (parent.SyncRoot)
			{
				if (!isLoaded)
					RefreshTags(transaction);
			}
		} // proc CheckTagsLoaded

		internal IReadOnlyList<PpsObjectTag> RefreshLazy()
		{
			CheckTagsLoaded(null);
			return tags;
		} // func RefreshLazy

		private void OnCollectionReset()
			=> CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

		#endregion

		#region -- Tag Manipulation -------------------------------------------------------

		public PpsObjectTag Add(PpsObjectTag tag, SQLiteTransaction transaction = null)
		{
			CheckTagsLoaded(transaction);

			lock (parent.SyncRoot)
			{
				using (var trans = new PpsNestedDatabaseTransaction(parent.Environment.LocalConnection, transaction))
				{
					long? tagId;

					// first update the local database
					using (var cmd = new TagSelectByKeyCommand(trans.Transaction))
						tagId = cmd.SelectByKey(parent.LocalId, tag.Name);

					if (tagId.HasValue) // update the entry
					{
						using (var cmd = new TagUpdateCommand(trans.Transaction))
							cmd.Update(tagId.Value, (int)tag.Class, tag.Value.ChangeType<string>(), Procs.GetSyncStamp());
					}
					else // insert new entry
					{
						using (var cmd = new TagInsertCommand(trans.Transaction))
							cmd.Insert(parent.LocalId, tag.Name, (int)tag.Class, tag.Value.ChangeType<string>(), Procs.GetSyncStamp());
					}

					// update the local list
					var index = IndexOf(tag.Name);
					if (index == -1)
					{
						tags.Add(tag);
						CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, tag));
					}
					else
					{
						var oldTag = tags[index];
						tags[index] = tag;
						CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, tag, oldTag, index));
					}

					trans.Commit();
				}
			}

			return tag;
		} // func Add

		public void Update(IReadOnlyList<PpsObjectTag> newTags, bool removeMissingTags = false, bool refreshTags = true, SQLiteTransaction transaction = null)
		{
			var updatedTags = new List<string>();

			// update tags
			using (var trans = new PpsNestedDatabaseTransaction(parent.Environment.LocalConnection, transaction))
			{
				using (var selectTags = new TagSelectCommand(trans.Transaction, false))
				using (var updateTag = new TagUpdateCommand(trans.Transaction))
				using (var insertTag = new TagInsertCommand(trans.Transaction))
				using (var deleteTag = new TagDeleteCommand(trans.Transaction))
				{
					foreach (var cur in selectTags.Select(parent.LocalId))
					{
						var tagId = cur.Item1;
						var currentTag = cur.Item2;
						var sourceTag = newTags.FirstOrDefault(c => String.Compare(c.Name, currentTag.Name, StringComparison.OrdinalIgnoreCase) == 0);

						if (sourceTag != null) // source exists, compare the value
						{
							if (sourceTag.SyncToken >= currentTag.SyncToken)
							{
								if (sourceTag.Class == PpsObjectTagClass.Deleted)
								{
									if (currentTag.Class != PpsObjectTagClass.Deleted)
										deleteTag.Delete(tagId);
								}
								else if (sourceTag.Class != currentTag.Class || !sourceTag.IsValueEqual(currentTag.Value)) // -> update
								{
									updateTag.Update(tagId, (int)sourceTag.Class, sourceTag.Value.ChangeType<string>(), sourceTag.SyncToken);
								}
							}
							updatedTags.Add(currentTag.Name);
						}
						else if (removeMissingTags) // no new tag, mark as deleted 
						{
							updateTag.Update(tagId, -1, null, Procs.GetSyncStamp());
						}
					} // foreach

					// insert all tags, they are not touched
					foreach (var sourceTag in newTags)
					{
						if (sourceTag.Value != null && !updatedTags.Exists(c => String.Compare(c, sourceTag.Name, StringComparison.OrdinalIgnoreCase) == 0))
							insertTag.Insert(parent.LocalId, sourceTag.Name, (int)sourceTag.Class, sourceTag.Value.ChangeType<string>(), sourceTag.SyncToken);
					}
				}

				// refresh tags
				if (refreshTags)
				{
					using (var selectTags = new TagSelectCommand(trans.Transaction, true))
						RefreshTags(selectTags.Select(parent.LocalId).Select(c => c.Item2));
				}
				else
				{
					tags.Clear();
					isLoaded = false;
				}
				trans.Commit();
			}
		} // proc Update

		public void Update(LuaTable tags, bool removeMissingTags = false, SQLiteTransaction transaction = null)
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

				tagList.Add(new PpsObjectTag(c.Key, t, c.Value, Procs.GetSyncStamp()));
			}

			Update(tagList, removeMissingTags, true, transaction);
		} // proc UpdateTags


		public bool Contains(string key)
			=> IndexOf(key) >= 0;

		public bool Contains(PpsObjectTag tag)
			=> IndexOf(tag) >= 0;

		public int IndexOf(string key)
		{
			lock (parent.SyncRoot)
			{
				CheckTagsLoaded(null);
				return tags.FindIndex(c => String.Compare(c.Name, key, StringComparison.CurrentCultureIgnoreCase) == 0);
			}
		} // func Contains

		public int IndexOf(PpsObjectTag tag)
			=> IndexOf(tag.Name);

		public bool Remove(string key, SQLiteTransaction transaction = null)
		{
			CheckTagsLoaded(transaction);

			using (var trans = new PpsNestedDatabaseTransaction(parent.Environment.LocalConnection, transaction))
			{
				long? tagId;
				using (var selectTag = new TagSelectByKeyCommand(trans.Transaction))
					tagId = selectTag.SelectByKey(parent.LocalId, key);

				if (tagId.HasValue)
				{
					// update database
					using (var updateTag = new TagUpdateCommand(trans.Transaction))
						updateTag.Update(tagId.Value, -1, null, Procs.GetSyncStamp());

					// notify ui
					RemoveUI(key);

					trans.Commit();
					return true;
				}
				else
				{
					RemoveUI(key);

					trans.Rollback();
					return false;
				}
			}
		} // func Remove

		private void RemoveUI(string key)
		{
			int index;
			PpsObjectTag tag;
			lock (parent.SyncRoot)
			{
				index = IndexOf(key);
				if (index >= 0)
				{
					tag = tags[index];
					tags.RemoveAt(index);
				}
			}
			if (index >= 0)
				CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, tags[index], index));
		} // proc RemoveUI

		public bool Remove(PpsObjectTag tag, SQLiteTransaction transaction = null)
			=> Remove(tag.Name, transaction);

		public bool RemoveAt(int index, SQLiteTransaction transaction = null)
			=> Remove(tags[index].Name, transaction);

		public IEnumerator<PpsObjectTag> GetEnumerator()
			=> tags.GetEnumerator();

		#endregion

		#region -- IList Interface --------------------------------------------------------

		int IList.Add(object value)
			=> IndexOf(Add((PpsObjectTag)value, null));

		void IList.Insert(int index, object value) { throw new NotSupportedException(); }

		void IList.Remove(object value)
			=> Remove((PpsObjectTag)value);

		void IList.RemoveAt(int index)
			=> RemoveAt(index);

		void IList.Clear() { throw new NotSupportedException(); }

		void ICollection.CopyTo(Array array, int index)
		{
			lock (parent.SyncRoot)
				((ICollection)tags).CopyTo(array, index);
		} // proc CopyTo

		bool IList.Contains(object value)
			=> Contains((PpsObjectTag)value);

		int IList.IndexOf(object value)
			=> IndexOf((PpsObjectTag)value);

		IEnumerator IEnumerable.GetEnumerator()
			=> GetEnumerator();

		bool IList.IsReadOnly => true;
		bool IList.IsFixedSize => false;
		object IList.this[int index] { get { return this[index]; } set { throw new NotSupportedException(); } }

		bool ICollection.IsSynchronized => true;
		object ICollection.SyncRoot => parent.SyncRoot;

		#endregion

		public int Count { get { lock (parent.SyncRoot) return tags.Count; } }

		public PpsObjectTag this[int index] => RefreshLazy()[index];
	} // class PpsObjectTags

	#endregion

	#region -- class IPpsObjectData -----------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public interface IPpsObjectData : INotifyPropertyChanged
	{
		Task LoadAsync(SQLiteTransaction transaction = null);
		Task CommitAsync(SQLiteTransaction transaction = null);
		Task PushAsync(SQLiteTransaction transaction = null);
		Task UnloadAsync(SQLiteTransaction transaction = null);

		bool IsLoaded { get; }
	} // interface IPpsObjectData

	#endregion

	#region -- class PpsObjectBlobData --------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Control byte based data.</summary>
	public sealed class PpsObjectBlobData : IPpsObjectData
	{
		public event PropertyChangedEventHandler PropertyChanged;

		private readonly PpsObject baseObj;

		public PpsObjectBlobData(PpsObject obj)
		{
			this.baseObj = obj;
		} // ctor

		public Task LoadAsync(SQLiteTransaction transaction = null)
		{
			return Task.CompletedTask;
		} // proc LoadAsync

		public Task CommitAsync(SQLiteTransaction transaction = null)
		{
			throw new NotImplementedException();
		}

		public Task PushAsync(SQLiteTransaction transaction = null)
		{
			throw new NotImplementedException();
		}

		public Task UnloadAsync(SQLiteTransaction transaction = null)
		{
			throw new NotImplementedException();
		}

		public bool IsLoaded => false;
	} // class PpsObjectBlobData

	#endregion

	#region -- class PpsObjectDataSet ---------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsObjectDataSet : PpsDataSetDesktop, IPpsObjectData
	{
		private readonly PpsObject baseObj;
		private readonly List<PpsRevisionDataSet> revisions = new List<PpsRevisionDataSet>();
		
		internal PpsObjectDataSet(PpsDataSetDefinitionDesktop definition, PpsObject obj)
			: base(definition, obj.Environment)
		{
			this.baseObj = obj;
		} // ctor

		public async Task LoadAsync(SQLiteTransaction transaction = null)
		{
			using (var docTrans = UndoSink?.BeginTransaction("Internal Read"))
			{
				using (var src = await baseObj.LoadRawDataAsync(transaction))
				{
					if (src == null)
						throw new ArgumentNullException("Data is missing.");

					using (var xml = XmlReader.Create(src, Procs.XmlReaderSettings))
						Read(XDocument.Load(xml).Root);
				}
				docTrans?.Commit();
			}

			UndoManager?.Clear();
		} // proc LoadAsync

		public async Task CommitAsync(SQLiteTransaction trans = null)
		{
			using (var transaction = new PpsNestedDatabaseTransaction(Environment.LocalConnection, trans))
			{
				// update data
				await baseObj.SaveRawDataAsync(transaction.Transaction,
					 dst =>
					 {
						 var settings = Procs.XmlWriterSettings;
						 settings.CloseOutput = false;
						 using (var xml = XmlWriter.Create(dst, settings))
							 Write(xml);
					 }
					);

				// update tags
				baseObj.Tags.Update(GetAutoTags().ToList(), transaction: transaction.Transaction);

				transaction.Commit();
			}
		} // proc CommitAsync

		public Task PushAsync(SQLiteTransaction transaction = null)
		{
			throw new NotImplementedException();
		} // proc PushAsync

		public Task UnloadAsync(SQLiteTransaction transaction = null)
			=> Task.CompletedTask;

		public PpsUndoManager UndoManager => null;
		public bool IsLoaded => IsInitialized;
	} // class PpsObjectDataSet

	#endregion

	#region -- class PpsRevisionDataSet -------------------------------------------------

	public sealed class PpsRevisionDataSet : PpsDataSetDesktop
	{
		internal PpsRevisionDataSet(PpsObjectDataSet parent, long revisionId)
			: base((PpsDataSetDefinitionDesktop)parent.DataSetDefinition, parent.Environment)
		{
		} // ctor
	} // class PpsRevisionDataSet

	#endregion

	#region -- class PpsObject ----------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsObject : DynamicDataRow, INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler PropertyChanged;

		private readonly PpsEnvironment environment;
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
		
		private IPpsObjectData data = null;
		private readonly PpsObjectTags tags;
		private readonly PpsObjectLinks links;

		#region -- Ctor/Dtor --------------------------------------------------------------

		/// <summary></summary>
		/// <param name="environment"></param>
		/// <param name="localId"></param>
		internal PpsObject(PpsEnvironment environment, IDataReader r)
		{
			this.environment = environment;
			this.localId = r.GetInt64(0);

			this.columns = new PpsObjectColumns(this);
			this.data = null;
			this.tags = new PpsObjectTags(this);
			this.links = new PpsObjectLinks(this);

			ReadObjectInfo(r);
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
			if (r.FieldCount >= StaticColumns.Length && !r.IsDBNull(StaticColumns.Length))
				tags.RefreshTagsFromString(r.GetString(StaticColumns.Length));
		} // proc ReadObjectInfo

		/// <summary>Refresh of the object data.</summary>
		/// <param name="transaction">Optional transaction.</param>
		public void Refresh(bool withTags = false, SQLiteTransaction transaction = null)
		{
			// refresh core data
			lock (objectLock)
				using (var cmd = environment.LocalConnection.CreateCommand())
				{
					cmd.CommandText = StaticColumnsSelect + " WHERE o.Id = @Id";
					cmd.Transaction = transaction;
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
			if (withTags)
				tags.RefreshTags(transaction);
		} // proc Refresh

		#endregion

		#region -- Pull, Push -------------------------------------------------------------

		private static bool DbNullOnNeg(long value)
			=> value < 0;

		public void Push(SQLiteTransaction transaction)
		{
			// refresh the objects
			Refresh(true, transaction);

			// pack the object for the server
			throw new NotImplementedException();
			//		CommitWork();

			//		var head = Tables["Head", true];
			//		var documentType = head.First["Typ"];

			//		long newServerId;
			//		long newRevId;

			//		// send the document to the server
			//		using (var xmlAnswer = environment.Request.GetXmlStreamAsync(
			//			await environment.Request.PutTextResponseAsync(documentType + "/?action=push", MimeTypes.Text.Xml,
			//				(tw) =>
			//				{
			//					using (var xmlPush = XmlWriter.Create(tw, Procs.XmlWriterSettings))
			//						Write(xmlPush);
			//				}
			//			)))
			//		{
			//			var xResult = environment.Request.CheckForExceptionResult(XDocument.Load(xmlAnswer).Root);

			//			newServerId = xResult.GetAttribute("id", -1L);
			//			newRevId = xResult.GetAttribute("revId", -1L);
			//			var pullRequest = xResult.GetAttribute("pullRequest", false);
			//			if (pullRequest)
			//				throw new ArgumentException("todo: Pull before push");
			//			if (newServerId < 0 || newRevId < 0)
			//				throw new ArgumentOutOfRangeException("id", "Pull action failed.");
			//		}

			//		// pull the document again, and update the local store
			//		var xRoot = await environment.Request.GetXmlAsync($"{documentType}/?action=pull&id={newServerId}&rev={newRevId}");

			//		// recreate dataset
			//		undoManager.Clear();
			//		Read(xRoot);
			//		SetLocalState(localId, newRevId, false, false);

			//		// update local store and
			//		using (var trans = environment.BeginLocalStoreTransaction())
			//		{
			//			throw new NotImplementedException();
			//			//trans.Update(localId, newServerId, newRevId, Tables["Head", true].First["Nr"].ToString());
			//			//trans.UpdateDataSet(localId, this);
			//			trans.Commit();
			//		}		//
			// objekt descr. target, with data, with ref?
			//
		} // proc Push

		public Task PullAsync(SQLiteTransaction transaction)
			=> Task.Run(() => Environment.GetObjectFromServer(guid, true, transaction));

		internal async Task PullAsnc(SQLiteTransaction transaction, IDataRow current, int[] columnIndexes, bool withData)
		{
			// check guid
			var rowGuid = current.GetValue(columnIndexes[(int)PpsObjectServerIndex.Guid], Guid.Empty);
			if (guid != rowGuid)
				throw new ArgumentOutOfRangeException("guid", $"Guid does not match (expected: {guid}, found: {rowGuid})");
			var rowTyp = current.GetValue(columnIndexes[(int)PpsObjectServerIndex.Typ], String.Empty);
			if (typ != rowTyp)
				throw new ArgumentOutOfRangeException("typ", $"Typ does not match (object: {guid}, expected: {typ}, found: {typ})");

			// update the values
			Set(ref serverId, current.GetValue(columnIndexes[(int)PpsObjectServerIndex.Id], -1), nameof(ServerId));
			Set(ref nr, current.GetValue(columnIndexes[(int)PpsObjectServerIndex.Nr], (string)null), nameof(Nr));
			Set(ref isRev, current.GetValue(columnIndexes[(int)PpsObjectServerIndex.IsRev], false), nameof(IsRev));
			Set(ref remoteRevId, current.GetValue(columnIndexes[(int)PpsObjectServerIndex.RevId], -1L), nameof(RemoteRevId));

			using (var trans = new PpsNestedDatabaseTransaction(Environment.LocalConnection, transaction))
			{
				using (var cmd = environment.LocalConnection.CreateCommand())
				{
					cmd.CommandText = "UPDATE main.Objects SET ServerId = @ServerId, IsRev = @IsRev, PulledRevId = @PulledRevId, Nr = @Nr WHERE Id = @Id";
					cmd.Transaction = trans.Transaction;

					cmd.Parameters.Add("@Id", DbType.Int64).Value = localId;
					cmd.Parameters.Add("@ServerId", DbType.Int64).Value = serverId.DbNullIf(DbNullOnNeg);
					cmd.Parameters.Add("@IsRev", DbType.Boolean).Value = isRev;
					cmd.Parameters.Add("@PulledRevId", DbType.Int64).Value = pulledRevId.DbNullIf(DbNullOnNeg);
					cmd.Parameters.Add("@Nr", DbType.String).Value = nr.DbNullIfString();

					await cmd.ExecuteNonQueryAsync();
				}

				// update Tags
				var tagList = PpsObjectTag.ParseTagFields(current.GetValue(columnIndexes[(int)PpsObjectServerIndex.Tags], String.Empty)).ToArray();
				tags.Update(tagList, refreshTags: false, transaction: trans.Transaction);
				tags.RefreshTags(tagList);

				// update links
				// todo:

				// update data
				if (withData)
					await PullDataAsync(trans.Transaction);

			}
		} // proc PullAsnc

		private async Task<Stream> PullDataAsync(long revisionId)
		{
			var objectInfo = Environment.GetObjectInfo(Typ);
			var objectUri = objectInfo?.GetMemberValue("objectUri") ?? Typ;
			var acceptedMimeType = objectInfo?.GetMemberValue("acceptedMimeType") as string;

			return await Environment.Request.GetStreamAsync($"{objectUri}/?action=pull&id={serverId}&rev={revisionId}", acceptedMimeType);
		} // proc PullDataAsync

		internal async Task PullDataAsync(SQLiteTransaction transaction)
		{
			// check if the environment is online
			await Environment.ForceOnlineAsync();

			// load the data from the server
			if (serverId <= 0)
				throw new ArgumentOutOfRangeException("serverId", "Invalid server id, this is a local only object and has no server representation.");

			var t = Environment.SynchronizationWorker.RemovePull(this);
			if (t == null)
			{
				// create the request for the data
				using (var trans = new PpsNestedDatabaseTransaction(Environment.LocalConnection, transaction))
				{
					using (var src = await PullDataAsync(RemoteRevId))
					{
						// update data
						pulledRevId = RemoteRevId;
						await SaveRawDataAsync(trans.Transaction,
							(dst) => src.CopyTo(dst)
						);
					}

					// read data
					if (data != null && !data.IsLoaded)
						await data.LoadAsync(trans.Transaction);

					trans.Commit();
				}
			}
			else
				await t;
		} // proc PullDataAsync

		public async Task<T> GetDataAsync<T>(SQLiteTransaction transaction, bool asyncPullData = false)
			where T : IPpsObjectData
		{
			if (data == null)
			{
				// create the core data object
				data = await environment.CreateObjectDataObjectAsync<T>(this);

				// update data from server, if not present
				if (serverId >= 0)
				{
					if (!hasData) // first data pull
					{
						//if (asyncPullData)
						//	Environment.SynchronizationWorker.EnqueuePull(this);
						//else
							await PullDataAsync(transaction);
					}
					else // todo: check for changes
					{ }
				}
			}
			return (T)data;
		} // func GetDataAsync

		public async Task<Stream> LoadRawDataAsync(SQLiteTransaction transaction)
		{
			using (var localTransaction = new PpsNestedDatabaseTransaction(Environment.LocalConnection, transaction))
			using (var cmd = Environment.LocalConnection.CreateCommand())
			{
				cmd.CommandText = "SELECT Document, length(Document) FROM main.Objects WHERE Id = @Id";
				cmd.Transaction = transaction;

				cmd.Parameters.Add("@Id", DbType.Int64).Value = localId;

				using (var r = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow))
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
		} // func LoadRawDataAsync

		public async Task SaveRawDataAsync(SQLiteTransaction transaction, Action<Stream> data)
		{
			byte[] bData = null;

			// read the data into a memory stream
			if (data != null)
			{
				using (var dst = new MemoryStream())
				{
					await Task.Run(() => data(dst));
					dst.Position = 0;
					bData = dst.ToArray();
				}
			}

			// store the value
			using (var cmd = Environment.LocalConnection.CreateCommand())
			{
				cmd.CommandText = "UPDATE main.Objects SET ServerId = IFNULL(@ServerId, ServerId), PulledRevId = IFNULL(@PulledRevId, PulledRevId), Nr = IFNULL(@Nr, Nr), Document = @Document, DocumentIsChanged = @DocumentIsChanged WHERE Id = @Id";
				cmd.Transaction = transaction;

				cmd.Parameters.Add("@Id", DbType.Int64).Value = localId;
				cmd.Parameters.Add("@ServerId", DbType.Int64).Value = serverId.DbNullIf(DbNullOnNeg);
				cmd.Parameters.Add("@PulledRevId", DbType.Int64).Value = bData == null ? DBNull.Value : pulledRevId.DbNullIf(DbNullOnNeg);
				cmd.Parameters.Add("@Nr", DbType.String).Value = nr.DbNullIfString();
				cmd.Parameters.Add("@Document", DbType.Binary).Value = bData == null ? (object)DBNull.Value : bData;
				cmd.Parameters.Add("@DocumentIsChanged", DbType.Boolean).Value = bData == null ? false : isDocumentChanged;

				await cmd.ExecuteNonQueryAsync();
			}
		} // proc SaveRawDataAsync

		public override string ToString()
			=> $"Object: {typ}; {localId} # {guid}:{pulledRevId}";

		#endregion

		#region -- Properties -------------------------------------------------------------

		internal void OnPropertyChanged(string propertyName)
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
				=> StaticColumns.Concat(new IDataColumn[] { StaticDataColumn, StaticTagsColumn, StaticLinksColumn }).Concat(obj.Tags.Select(c => CreateSimpleDataColumn(c))).GetEnumerator();

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
						else if (index < StaticColumns.Length)
							return StaticColumns[index];
						else if (index == StaticColumns.Length)
							return StaticDataColumn;
						else if (index == StaticColumns.Length + 1)
							return StaticTagsColumn;
						else if (index == StaticColumns.Length + 2)
							return StaticLinksColumn;
						else if (index < StaticColumns.Length + obj.Tags.Count + StaticPropertyCount)
						{
							var tag = obj.Tags[index - StaticColumns.Length - StaticPropertyCount];
							return CreateSimpleDataColumn(tag);
						}
						else
							throw new ArgumentOutOfRangeException();
					}
				}
			} // prop this

			private static SimpleDataColumn CreateSimpleDataColumn(PpsObjectTag tag)
				=> new SimpleDataColumn(tag.Name, PpsObjectTag.GetTypeFromClass(tag.Class));

			public int Count => StaticColumns.Length + obj.Tags.Count + StaticPropertyCount;
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
					else if (index < StaticColumns.Length + StaticPropertyCount)
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
								return isRev;
							case 6:
								return remoteRevId;
							case 7:
								return pulledRevId;
							case 8:
								return isDocumentChanged;
							case 9:
								return hasData;
							case 10:
								if (data == null)
									data = GetDataAsync<IPpsObjectData>(null, true).Result;
								return data;
							case 11:
								return tags;
							case 12:
								return links;
							default:
								return null;
						}
					}
					else if (index < StaticColumns.Length + Tags.Count + StaticPropertyCount)
						return tags[index - StaticColumns.Length - StaticPropertyCount].Value;
					else
						throw new ArgumentOutOfRangeException();
				}
			}
		} // prop this

		#endregion

		public PpsEnvironment Environment => environment;
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
		public PpsObjectLinks Links => links.RefreshLazy();
		public PpsObjectTags Tags => tags;

		internal object SyncRoot => objectLock;

		// -- Static --------------------------------------------------------------

		static PpsObject()
		{
			StaticColumns = new IDataColumn[]
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
			if (StaticColumnExpressions.Length != StaticColumns.Length)
				throw new ArgumentOutOfRangeException("columns");
#endif

			StaticColumnsSelect = "SELECT " + String.Join(",", StaticColumnExpressions) + " FROM main.[Objects] o";
		} // ctor

		internal const int StaticPropertyCount = 3;

		internal static IDataColumn[] StaticColumns { get; }
		internal static string[] StaticColumnExpressions { get; }
		internal static string StaticColumnsSelect { get; }

		internal static IDataColumn StaticDataColumn { get; } = new SimpleDataColumn("Data", typeof(IPpsObjectData));
		internal static IDataColumn StaticTagsColumn { get; } = new SimpleDataColumn("Tags", typeof(PpsObjectTags));
		internal static IDataColumn StaticLinksColumn { get; } = new SimpleDataColumn("Links", typeof(PpsObjectLinks));
	} // class PpsObject

	#endregion

	#region -- class PpsObjectInfo ------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsObjectInfo : LuaEnvironmentTable, IPpsEnvironmentDefinition
	{
		private readonly string name;

		public PpsObjectInfo(PpsEnvironment environemnt, string name)
			: base(environemnt)
		{
			this.name = name;
		} // ctor

		[LuaMember]
		public string Name
		{
			get { return name; }
			private set { }
		} // prop Name
	} // class PpsObjectInfo

	#endregion

	#region -- class PpsObjectSynchronizationWorker -------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Background work for the synchronization of data.</summary>
	public sealed class PpsObjectSynchronizationWorker
	{
		private readonly PpsEnvironment environment;

		public PpsObjectSynchronizationWorker(PpsEnvironment environment)
		{
			this.environment = environment;
		} // ctor

		//private struct QueuedObject
		//{
		//	public int Action; // pull/push
		//	public PpsObject Object;
		//} // struct QueuedTask

		/*
		 * PushStructure
		 * PullStructure
		 */

		//private List<>

		public void EnqueuePull(PpsObject obj)
		{
			throw new NotImplementedException();
		} // proc EnqueuePull

		public void EnqueuePush(PpsObject obj)
		{
		} // func EnqueuePush

		internal Task RemovePull(PpsObject ppsObject)
		{
			return null;
		}
	} // class PpsObjectSynchronizationWorker

	#endregion

	#region -- class PpsEnvironment -----------------------------------------------------

	public partial class PpsEnvironment
	{
		private readonly PpsActiveDataSetsImplementation activeDataSets;

		// point of improvement: a structure equal to LuaTable-Hash should be created on perf. issues
		private readonly object objectStoreLock = new object();
		private readonly List<WeakReference<PpsObject>> objectStore = new List<WeakReference<PpsObject>>();
		private readonly Dictionary<long, int> objectStoreById = new Dictionary<long, int>();
		private readonly Dictionary<Guid, int> objectStoreByGuid = new Dictionary<Guid, int>();

		private readonly PpsEnvironmentCollection<PpsObjectInfo> objectInfo;
		private readonly PpsObjectSynchronizationWorker synchronizationWorker;

		private const bool UseId = false;
		private const bool UseGuid = true;

		#region -- Update local store index -----------------------------------------------

		protected async Task RefreshObjectStoreAsync()
		{
			long maxSyncToken;
			bool syncTokenRowExists;
			using (var cmd = LocalConnection.CreateCommand())
			{
				cmd.CommandText = "SELECT [SyncToken] FROM main.[SyncTokens] WHERE [Name] = 'Objects'";
				using (var r = await cmd.ExecuteReaderAsync(CommandBehavior.SingleResult))
					maxSyncToken = (syncTokenRowExists = await r.ReadAsync()) && !r.IsDBNull(0) ? r.GetInt64(0) : 0;
			}

			using (var enumerator = GetViewData(
				new PpsShellGetList("dbo.objects")
				{
					Filter = new PpsDataFilterCompareExpression("SyncToken", PpsDataFilterCompareOperator.Greater, new PpsDataFilterCompareTextValue(maxSyncToken.ToString()))
				}).GetEnumerator())
			{
				var columnIndexes = CreateColumnIndexes(enumerator);

				var run = true;
				do
				{
					using (var transaction = BeginLocalStoreTransaction())
					{
						var sw = Stopwatch.StartNew();
						while (sw.ElapsedMilliseconds < 500)
						{
							run = enumerator.MoveNext();
							if (!run)
								break;

							await RefreshObjectAsync(enumerator, columnIndexes, transaction);

							maxSyncToken = Math.Max(maxSyncToken, enumerator.GetValue(columnIndexes[(int)PpsObjectServerIndex.SyncToken], 0L));
						}

						transaction.Commit();
					}
				} while (run);
			}

			// update sync token
			using (var cmd = LocalConnection.CreateCommand())
			{

				cmd.CommandText = syncTokenRowExists ?
					"UPDATE main.[SyncTokens] SET [SyncToken] = @SyncToken WHERE [Name] = 'Objects'" :
					"INSERT INTO main.[SyncTokens] ([Name], [SyncToken]) VALUES ('Objects', @SyncToken)";

				cmd.Parameters.Add("@SyncToken", DbType.Int64).Value = maxSyncToken;
				await cmd.ExecuteNonQueryAsync();
			}
		} // proc RefreshObjectStoreAsync

		private static int[] CreateColumnIndexes(IEnumerator<IDataRow> enumerator)
		{
			return new int[]
			{
					enumerator.FindColumnIndex(PpsObjectServerIndex.Id.ToString(), true),
					enumerator.FindColumnIndex(PpsObjectServerIndex.Guid.ToString(), true),
					enumerator.FindColumnIndex(PpsObjectServerIndex.Typ.ToString(), true),
					enumerator.FindColumnIndex(PpsObjectServerIndex.Nr.ToString(), true),
					enumerator.FindColumnIndex(PpsObjectServerIndex.IsRev.ToString(), true),
					enumerator.FindColumnIndex(PpsObjectServerIndex.SyncToken.ToString(), true),
					enumerator.FindColumnIndex(PpsObjectServerIndex.RevId.ToString(), true),
					enumerator.FindColumnIndex(PpsObjectServerIndex.Tags.ToString())
			};
		} // func CreateColumnIndexes

		private async Task<PpsObject> RefreshObjectAsync(IEnumerator<IDataRow> enumerator, int[] columnIndexes, SQLiteTransaction transaction)
		{
			var objectGuid = enumerator.GetValue(columnIndexes[(int)PpsObjectServerIndex.Guid], Guid.Empty);
			if (objectGuid == Guid.Empty)
				throw new ArgumentException("Object guid is empty. Check the server return.", "guid");

			var obj = GetObject(objectGuid, transaction);

			if (obj == null) // create empty object
			{
				obj = CreateNewObject(
					transaction,
					enumerator.GetValue(columnIndexes[(int)PpsObjectServerIndex.Id], -1L),
					enumerator.GetValue(columnIndexes[(int)PpsObjectServerIndex.Guid], Guid.Empty),
					enumerator.GetValue(columnIndexes[(int)PpsObjectServerIndex.Typ], (string)null),
					enumerator.GetValue(columnIndexes[(int)PpsObjectServerIndex.Nr], (string)null),
					enumerator.GetValue(columnIndexes[(int)PpsObjectServerIndex.IsRev], false),
					enumerator.GetValue(columnIndexes[(int)PpsObjectServerIndex.RevId], -1L),
					enumerator.GetValue(columnIndexes[(int)PpsObjectServerIndex.SyncToken], -1L)
				);
			}

			// update the object informations
			await obj.PullAsnc(transaction, enumerator.Current, columnIndexes, false);

			return obj;
		} // func RefreshObjectAsync

		#endregion

		#region -- CreateObjectFilter -----------------------------------------------------

		#region -- class PpsObjectEnumerator -----------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class PpsObjectEnumerator : IEnumerator<PpsObject>, IDataColumns
		{
			private readonly PpsEnvironment environment;
			private readonly SQLiteCommand command;
			private SQLiteDataReader reader;
			private PpsObject current;

			public PpsObjectEnumerator(PpsEnvironment environment, SQLiteCommand command)
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
							return "LEFT OUTER JOIN ObjectTags AS " + AllColumns + " ON (o.Id = " + AllColumns + ".ObjectId AND " + AllColumns + ".[Class] >= 0)";
						case ObjectViewColumnType.Date:
							return "LEFT OUTER JOIN ObjectTags AS " + DateColumns + " ON (o.Id = " + DateColumns + ".ObjectId AND " + AllColumns + ".[Class] >= 0 AND " + DateColumns + ".class = " + DateClass + ")";
						case ObjectViewColumnType.Number:
							return "LEFT OUTER JOIN ObjectTags AS " + NumberColumns + " ON (o.Id = " + NumberColumns + ".ObjectId AND " + AllColumns + ".[Class] >= 0 AND " + NumberColumns + ".class = " + NumberClass + ")";

						case ObjectViewColumnType.Key:
							if (classification == 0)
								return "LEFT OUTER JOIN ObjectTags AS " + joinAlias + " ON (o.Id = " + joinAlias + ".ObjectId AND " + AllColumns + ".[Class] >= 0 AND " + joinAlias + ".Key = '" + keyName + "' COLLATE NOCASE)";
							else
								return "LEFT OUTER JOIN ObjectTags AS " + joinAlias + " ON (o.Id = " + joinAlias + ".ObjectId AND " + AllColumns + ".[Class] >= 0 AND " + joinAlias + ".Class = " + classification + " AND " + joinAlias + ".Key = '" + keyName + "' COLLATE NOCASE)";
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

			private readonly PpsEnvironment environment;
			private readonly SQLiteConnection localStoreConnection;

			private List<ObjectViewColumn> columnInfos = new List<ObjectViewColumn>();
			private string whereCondition = null;
			private string orderCondition = null;
			private long limitStart = -1;
			private long limitCount = -1;

			public PpsObjectGenerator(PpsEnvironment environment, SQLiteConnection localStoreConnection)
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

		/// <summary>Create a new object.</summary>
		/// <param name="serverId"></param>
		/// <param name="guid"></param>
		/// <param name="typ"></param>
		/// <param name="nr"></param>
		/// <param name="isRev"></param>
		/// <param name="remoteRevId"></param>
		/// <param name="syncToken"></param>
		/// <returns></returns>
		[LuaMember]
		public PpsObject CreateNewObject(SQLiteTransaction transaction, long serverId, Guid guid, string typ, string nr, bool isRev, long remoteRevId, long syncToken = 0)
		{
			using (var cmd = LocalConnection.CreateCommand())
			{
				cmd.CommandText = "INSERT INTO main.Objects (ServerId, Guid, Typ, Nr, IsRev, RemoteRevId, SyncToken) VALUES (@ServerId, @Guid, @Typ, @Nr, @IsRev, @RemoteRevId, @SyncToken)";
				cmd.Transaction = transaction;

				cmd.Parameters.Add("@ServerId", DbType.Int64).Value = serverId.DbNullIf(StuffDB.DbNullOnNeg);
				cmd.Parameters.Add("@Guid", DbType.Guid).Value = guid;
				cmd.Parameters.Add("@Typ", DbType.String).Value = typ.DbNullIfString();
				cmd.Parameters.Add("@Nr", DbType.String).Value = nr.DbNullIfString();
				cmd.Parameters.Add("@IsRev", DbType.Boolean).Value = isRev;
				cmd.Parameters.Add("@RemoteRevId", DbType.Int64).Value = remoteRevId.DbNullIf(StuffDB.DbNullOnNeg);
				cmd.Parameters.Add("@SyncToken", DbType.Int64).Value = syncToken < 0 ? 0 : syncToken;

				cmd.ExecuteNonQuery();

				return GetObject(LocalConnection.LastInsertRowId, transaction);
			}
		} // func CreateNewObject

		internal async Task<T> CreateObjectDataObjectAsync<T>(PpsObject obj)
			where T : IPpsObjectData
		{
			var schema = await ActiveDataSets.GetDataSetDefinition(obj.Typ);
			if (schema == null)
				return (T)(IPpsObjectData)new PpsObjectBlobData(obj);
			else
				return (T)(IPpsObjectData)new PpsObjectDataSet(schema, obj);
		} // func CreateObjectDataObjectAsync

		#region -- Automatic download queue -----------------------------------------------


		#endregion

		#region -- Object Info ------------------------------------------------------------

		protected object GetObjectInfoSyncObject()
			=> objectInfo;

		protected List<string> GetRemoveObjectInfo()
			=> ((IDictionary<string, PpsObjectInfo>)objectInfo).Keys.ToList();

		protected void UpdateObjectInfo(XElement x, List<string> removeObjectInfo)
		{
			var objectTyp = x.GetAttribute("name", String.Empty);
			var sourceUri = x.GetAttribute("source", String.Empty);
			var paneUri = x.GetAttribute("pane", String.Empty);
			if (String.IsNullOrEmpty(objectTyp))
				return;

			// update dataset definitions
			if (!String.IsNullOrEmpty(sourceUri))
				ActiveDataSets.RegisterDataSetSchema(objectTyp, sourceUri, typeof(PpsDataSetDefinitionDesktop));

			var oi = new PpsObjectInfo(this, objectTyp);
			objectInfo.AppendItem(oi);

			// update pane hint
			if (!String.IsNullOrEmpty(paneUri))
				oi["defaultPane"] = paneUri;


			// mark document as readed
			var ri = removeObjectInfo.FindIndex(c => String.Compare(objectTyp, c, StringComparison.OrdinalIgnoreCase) == 0);
			if (ri != -1)
				removeObjectInfo.RemoveAt(ri);
		} // proc UpdateObjectInfo

		protected void ClearObjectInfo(List<string> removeObjectInfo)
		{
			foreach (var d in removeObjectInfo)
				ActiveDataSets.UnregisterDataSetSchema(d);
		} // proc ClearObjectInfo

		#endregion

		#region -- Object Cache -----------------------------------------------------------

		private PpsObject ReadObject(object key, bool useGuid, SQLiteTransaction transaction)
		{
			// refresh core data
			using (var cmd = LocalConnection.CreateCommand())
			{
				cmd.CommandText = PpsObject.StaticColumnsSelect + (useGuid ? " WHERE o.Guid = @Guid" : " WHERE o.Id = @Id");
				cmd.Transaction = transaction;
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

		private PpsObject GetCachedObjectOrRead<T>(Dictionary<T, int> index, T key, bool keyIsGuid, SQLiteTransaction transaction = null)
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
		public PpsObject GetObject(long localId, SQLiteTransaction transaction = null)
			=> GetCachedObjectOrRead(objectStoreById, localId, UseId, transaction);

		[LuaMember]
		public PpsObject GetObject(Guid guid, SQLiteTransaction transaction = null)
			=> GetCachedObjectOrRead(objectStoreByGuid, guid, UseGuid, transaction);


		[LuaMember]
		public PpsObject GetObjectFromServer(Guid guid, bool forceRefresh = false, SQLiteTransaction transaction = null)
		{
			lock (objectStoreLock)
			{
				// ask cache
				if (!forceRefresh)
				{
					var o = GetCachedObject(objectStoreByGuid, guid);
					if (o != null)
						return o;
				}

				using (var e = GetViewData(
					new PpsShellGetList("dbo.objects")
					{
						Filter = new PpsDataFilterCompareExpression("Guid", PpsDataFilterCompareOperator.Equal, new PpsDataFilterCompareTextValue(guid.ToString("G")))
					}).GetEnumerator())
				{
					if (e.MoveNext())
						return RefreshObjectAsync(e, CreateColumnIndexes(e), transaction).Result;
					else
						throw new ArgumentException($"Object '{guid}' not found.");
				}
			}
		} // func GetObjectFromServer

		[LuaMember]
		public LuaTable GetObjectInfo(string objectTyp)
			=> objectInfo[objectTyp, false];

		#endregion

		[LuaMember]
		public SQLiteTransaction BeginLocalStoreTransaction()
			=> LocalConnection.BeginTransaction();

		#region -- ActiveDataSets ---------------------------------------------------------

		internal void OnDataSetActivated(PpsDataSetDesktop dataset)
		{
			if (activeDataSets.IndexOf(dataset) >= 0)
				throw new ArgumentException("DataSet already registered.");

			activeDataSets.Add(dataset);
		} // proc OnDataSetActivated

		internal void OnDataSetDeactivated(PpsDataSetDesktop dataset)
		{
			activeDataSets.Remove(dataset);
		} // proc OnDataSetDeactivated

		[LuaMember]
		public IPpsActiveDataSets ActiveDataSets => activeDataSets;

		[LuaMember]
		public PpsEnvironmentCollection<PpsObjectInfo> ObjectInfos => objectInfo;


		public PpsObjectSynchronizationWorker SynchronizationWorker => synchronizationWorker;
		#endregion
	} // class PpsEnvironment

	#endregion
}
