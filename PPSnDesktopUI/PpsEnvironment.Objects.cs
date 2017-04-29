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
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Data;
using TecWare.DE.Networking;
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

				using (var r = command.ExecuteReaderEx(CommandBehavior.SingleResult))
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
			protected readonly DbCommand command;

			public TagCommand(PpsMasterDataTransaction transaction)
			{
				this.command = transaction.CreateNativeCommand();
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
			private readonly DbParameter objectIdParameter;
			private readonly DbParameter keyParameter;

			public TagSelectByKeyCommand(PpsMasterDataTransaction transaction)
				: base(transaction)
			{
				this.command.CommandText = "SELECT [Id] FROM main.[ObjectTags] WHERE [ObjectId] = @ObjectId AND [Key] = @Key;";

				this.objectIdParameter = command.AddParameter("@ObjectId", DbType.Int64);
				this.keyParameter = command.AddParameter("@Key", DbType.String);

				this.command.Prepare();
			} // ctor

			public long? SelectByKey(long objectId, string key)
			{
				objectIdParameter.Value = objectId;
				keyParameter.Value = key;

				using (var r = command.ExecuteReaderEx(CommandBehavior.SingleRow))
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
			private readonly DbParameter objectIdParameter;

			public TagSelectCommand(PpsMasterDataTransaction transaction, bool visibleOnly)
				: base(transaction)
			{
				this.command.CommandText =
					"SELECT [Id], [Key], [Class], [Value], [UserId] FROM main.[ObjectTags] WHERE [ObjectId] = @ObjectId" + (visibleOnly ? " AND [Class] >= 0" : ";");

				this.objectIdParameter = command.AddParameter("@ObjectId", DbType.Int64);

				this.command.Prepare();
			} // ctor

			public IEnumerable<Tuple<long, PpsObjectTag>> Select(long objectId)
			{
				objectIdParameter.Value = objectId;

				using (var r = command.ExecuteReaderEx(CommandBehavior.SingleResult))
				{
					while (r.Read())
					{
						var k = r.GetString(1);
						var t = r.GetInt32(2);
						var v = r.IsDBNull(3) ? null : r.GetString(3);
						var u = r.IsDBNull(4) ? -1L : r.GetInt64(4);
						yield return new Tuple<long, PpsObjectTag>(r.GetInt64(0), new PpsObjectTag(k, (PpsObjectTagClass)t, v, u));
					}
				}
			}// func Select
		} // class TagSelectCommand

		#endregion

		#region -- class TagInsertCommand -------------------------------------------------

		private sealed class TagInsertCommand : TagCommand
		{
			private readonly PpsMasterDataTransaction transaction;
			private readonly DbParameter idParameter;
			private readonly DbParameter objectIdParameter;
			private readonly DbParameter keyParameter;
			private readonly DbParameter classParameter;
			private readonly DbParameter valueParameter;
			private readonly DbParameter userIdParameter;

			public TagInsertCommand(PpsMasterDataTransaction transaction)
				: base(transaction)
			{
				this.transaction = transaction;

				this.command.CommandText = "INSERT INTO main.[ObjectTags] ([Id], [ObjectId], [Key], [Class], [Value], [UserId]) values (@Id, @ObjectId, @Key, @Class, @Value, @UserId);";
				this.idParameter = command.AddParameter("@Id", DbType.Int64);
				this.objectIdParameter = command.AddParameter("@ObjectId", DbType.Int64);
				this.keyParameter = command.AddParameter("@Key", DbType.String);
				this.classParameter = command.AddParameter("@Class", DbType.Int64);
				this.valueParameter = command.AddParameter("@Value", DbType.String);
				this.userIdParameter = command.AddParameter("@UserId", DbType.Int64);

				this.command.Prepare();
			} // ctor

			public long Insert(long objectId, string key, int cls, string value, long userId)
			{
				var id = transaction.GetNextLocalId(transaction, "ObjectTags", "Id");
				idParameter.Value = id;
				objectIdParameter.Value = objectId;
				keyParameter.Value = key;
				classParameter.Value = cls;
				valueParameter.Value = value;
				userIdParameter.Value = userId;

				command.ExecuteNonQueryEx();

				return id;
			} // func Insert
		} // class TagInsertCommand

		#endregion

		#region -- class TagUpdateCommand -------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class TagUpdateCommand : TagCommand
		{
			private readonly DbParameter idParameter;
			private readonly DbParameter classParameter;
			private readonly DbParameter valueParameter;
			private readonly DbParameter userIdParameter;

			public TagUpdateCommand(PpsMasterDataTransaction transaction)
				: base(transaction)
			{
				this.command.CommandText = "UPDATE main.[ObjectTags] SET [Class] = @Class, [Value] = @Value, [SyncToken] = @syncToken where [Id] = @Id;";

				this.idParameter = command.AddParameter("@Id", DbType.Int64);
				this.classParameter = command.AddParameter("@Class", DbType.Int64);
				this.valueParameter = command.AddParameter("@Value", DbType.String);
				this.userIdParameter = command.AddParameter("@userId", DbType.Int64);

				this.command.Prepare();
			} // ctor

			public void Update(long tagId, int cls, string value, long userId)
			{
				idParameter.Value = tagId;
				classParameter.Value = cls;
				valueParameter.Value = value ?? (object)DBNull.Value;
				userIdParameter.Value = userId;

				command.ExecuteNonQueryEx();
			} // proc Update
		} // class TagUpdateCommand

		#endregion

		#region -- class TagDeleteCommand -------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class TagDeleteCommand : TagCommand
		{
			private readonly DbParameter idParameter;

			public TagDeleteCommand(PpsMasterDataTransaction transaction)
				: base(transaction)
			{
				this.command.CommandText = "DELETE FROM main.[ObjectTags] WHERE [Id] = @Id;";

				this.idParameter = command.AddParameter("@Id", DbType.Int64);

				this.command.Prepare();
			} // ctor

			public void Delete(long tagId)
			{
				idParameter.Value = tagId;
				command.ExecuteNonQueryEx();
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

		internal void RefreshTags(PpsMasterDataTransaction transaction = null)
		{
			using (var cmd = new TagSelectCommand(transaction, true))
				RefreshTags(cmd.Select(parent.Id).Select(c => c.Item2));
		} // proc RefreshTags

		internal void CheckTagsLoaded(PpsMasterDataTransaction transaction)
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

		public PpsObjectTag Add(PpsObjectTag tag, PpsMasterDataTransaction transaction = null)
		{
			CheckTagsLoaded(transaction);

			lock (parent.SyncRoot)
			{
				using (var trans = parent.Environment.MasterData.CreateTransaction(transaction))
				{
					long? tagId;

					// first update the local database
					using (var cmd = new TagSelectByKeyCommand(trans))
						tagId = cmd.SelectByKey(parent.Id, tag.Name);

					if (tagId.HasValue) // update the entry
					{
						using (var cmd = new TagUpdateCommand(trans))
							cmd.Update(tagId.Value, (int)tag.Class, tag.Value.ChangeType<string>(), Procs.GetSyncStamp());
					}
					else // insert new entry
					{
						using (var cmd = new TagInsertCommand(trans))
							cmd.Insert(parent.Id, tag.Name, (int)tag.Class, tag.Value.ChangeType<string>(), Procs.GetSyncStamp());
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

		public void Update(IReadOnlyList<PpsObjectTag> newTags, bool removeMissingTags = false, bool refreshTags = true, PpsMasterDataTransaction transaction = null)
		{
			var updatedTags = new List<string>();

			// update tags
			using (var trans = parent.Environment.MasterData.CreateTransaction(transaction))
			{
				using (var selectTags = new TagSelectCommand(trans, false))
				using (var updateTag = new TagUpdateCommand(trans))
				using (var insertTag = new TagInsertCommand(trans))
				using (var deleteTag = new TagDeleteCommand(trans))
				{
					foreach (var cur in selectTags.Select(parent.Id))
					{
						var tagId = cur.Item1;
						var currentTag = cur.Item2;
						var sourceTag = newTags.FirstOrDefault(c => String.Compare(c.Name, currentTag.Name, StringComparison.OrdinalIgnoreCase) == 0);

						if (sourceTag != null) // source exists, compare the value
						{
							if (sourceTag.Class == PpsObjectTagClass.Deleted)
							{
								if (currentTag.Class != PpsObjectTagClass.Deleted)
									deleteTag.Delete(tagId);
							}
							else if (sourceTag.Class != currentTag.Class || !sourceTag.IsValueEqual(currentTag.Value)) // -> update
							{
								updateTag.Update(tagId, (int)sourceTag.Class, sourceTag.Value.ChangeType<string>(), sourceTag.UserId);
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
							insertTag.Insert(parent.Id, sourceTag.Name, (int)sourceTag.Class, sourceTag.Value.ChangeType<string>(), sourceTag.UserId);
					}
				}

				// refresh tags
				if (refreshTags)
				{
					using (var selectTags = new TagSelectCommand(trans, true))
						RefreshTags(selectTags.Select(parent.Id).Select(c => c.Item2));
				}
				else
				{
					tags.Clear();
					isLoaded = false;
				}
				trans.Commit();
			}
		} // proc Update

		public void Update(LuaTable tags, bool removeMissingTags = false, PpsMasterDataTransaction transaction = null)
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

		public bool Remove(string key, PpsMasterDataTransaction transaction = null)
		{
			CheckTagsLoaded(transaction);

			using (var trans = parent.Environment.MasterData.CreateTransaction(transaction))
			{
				long? tagId;
				using (var selectTag = new TagSelectByKeyCommand(trans))
					tagId = selectTag.SelectByKey(parent.Id, key);

				if (tagId.HasValue)
				{
					// update database
					using (var updateTag = new TagUpdateCommand(trans))
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

		public bool Remove(PpsObjectTag tag, PpsMasterDataTransaction transaction = null)
			=> Remove(tag.Name, transaction);

		public bool RemoveAt(int index, PpsMasterDataTransaction transaction = null)
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
		Task LoadAsync(PpsMasterDataTransaction transaction = null);
		Task CommitAsync(PpsMasterDataTransaction transaction = null);
		Task PushAsync(PpsMasterDataTransaction transaction, Stream dst);
		Task UnloadAsync(PpsMasterDataTransaction transaction = null);

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
		private byte[] rawData = null;

		public PpsObjectBlobData(PpsObject obj)
		{
			this.baseObj = obj;
		} // ctor

		private void OnPropertyChanged(string propertyName)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		public async Task LoadAsync(PpsMasterDataTransaction transaction = null)
		{
			using (var src = await baseObj.LoadRawDataAsync(transaction))
			{
				rawData = src.ReadInArray();
				OnPropertyChanged(nameof(IsLoaded));
			}
		} // proc LoadAsync

		public async Task CommitAsync(PpsMasterDataTransaction transaction = null)
		{
			await baseObj.SaveRawDataAsync(transaction, rawData.Length, MimeTypes.Application.OctetStream, dst => dst.Write(rawData, 0, rawData.Length));
		} // proc CommitAsync

		public async Task PushAsync(PpsMasterDataTransaction transaction, Stream dst)
		{
			if (IsLoaded)
				await LoadAsync(transaction);
			await dst.WriteAsync(rawData, 0, rawData.Length);
		} // func PushAsync

		public Task UnloadAsync(PpsMasterDataTransaction transaction = null)
		{
			rawData = null;
			return Task.CompletedTask;
		} // func UnloadTask

		public Task ReadFromFile(string filename, PpsMasterDataTransaction transaction = null)
		{
			var fileStream = new FileStream(filename, FileMode.Open);
			rawData = fileStream.ReadInArray();
			return Task.CompletedTask;
		}

		public bool IsLoaded => rawData != null;
	} // class PpsObjectBlobData

	#endregion

	#region -- class PpsObjectDataSet ---------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsObjectDataSet : PpsDataSetDesktop, IPpsObjectData, IPpsObjectBasedDataSet
	{
		private readonly PpsObject baseObj;
		private readonly PpsUndoManager undoManager;
		private readonly List<PpsRevisionDataSet> revisions = new List<PpsRevisionDataSet>();
		
		internal PpsObjectDataSet(PpsDataSetDefinitionDesktop definition, PpsObject obj)
			: base(definition, obj.Environment)
		{
			this.baseObj = obj;
			this.RegisterUndoSink(this.undoManager = new PpsUndoManager());
		} // ctor

		public override async Task OnNewAsync(LuaTable arguments)
		{
			// add the basic head table and update the object data
			var head = Tables["Head", false];
			if(head != null)
			{
				if (head.Count == 0)
					head.Add();
			}

			await base.OnNewAsync(arguments);
		} // proc OnNewAsync

		public async Task LoadAsync(PpsMasterDataTransaction transaction = null)
		{
			using (var src = await baseObj.LoadRawDataAsync(transaction))
			{
				if (src == null)
					throw new ArgumentNullException("Data is missing.");

				using (var xml = XmlReader.Create(src, Procs.XmlReaderSettings))
				{
					var xData = XDocument.Load(xml).Root;
					await Environment.Dispatcher.InvokeAsync(() => Read(xData));
				}
			}
		} // proc LoadAsync

		public async Task CommitAsync(PpsMasterDataTransaction transaction = null)
		{
			using (var trans = Environment.MasterData.CreateTransaction(transaction))
			{
				await baseObj.SaveRawDataAsync(trans, -1, MimeTypes.Text.DataSet,
					dst =>
					{
						var settings = Procs.XmlWriterSettings;
						settings.CloseOutput = false;
						using (var xml = XmlWriter.Create(dst, settings))
							Write(xml);
					}
				);

				//		// update tags
				//		baseObj.Tags.Update(GetAutoTags().ToList(), transaction: transaction.Transaction);

				trans.Commit();
			}

			// mark not dirty anymore
			ResetDirty();
		} // proc CommitAsync

		public async Task PushAsync(PpsMasterDataTransaction transaction, Stream dst)
		{
			if (IsDirty)
				await CommitAsync(transaction);
			
			using (var xml = XmlWriter.Create(dst, Procs.XmlWriterSettings))
				Write(xml);
		} // proc PushAsync

		public Task UnloadAsync(PpsMasterDataTransaction transaction = null)
			=> Task.CompletedTask;

		/// <summary>The document it self implements the undo-manager.</summary>
		public PpsUndoManager UndoManager => undoManager;
		/// <summary>Is the document fully loaded.</summary>
		public bool IsLoaded => IsInitialized;
		/// <summary>This document is connected with ...</summary>
		public PpsObject Object => baseObj;
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
		#region -- class PpsStaticObjectColumn ------------------------------------------

		private sealed class PpsStaticObjectColumn : SimpleDataColumn
		{
			private readonly string columnExpression;

			public PpsStaticObjectColumn(string columnName, string columnExpression, Type dataType)
				: base(columnName, dataType)
			{
				this.columnExpression = columnExpression;
			} // ctor

			public string Expression => columnExpression;
		} // class PpsStaticObjectColumn

		#endregion

		public event PropertyChangedEventHandler PropertyChanged;

		private readonly PpsEnvironment environment;
		private readonly PpsObjectColumns columns;
		private long objectId;
		private readonly object[] staticValues;				// values of the table
		private readonly object objectLock = new object();
		
		private IPpsObjectData data = null;					// access to the object data
		private readonly PpsObjectTags tags;				// list with assigned tags
		private readonly PpsObjectLinks links;				// linked objects

		#region -- Ctor/Dtor --------------------------------------------------------------

		/// <summary></summary>
		/// <param name="environment"></param>
		/// <param name="localId"></param>
		internal PpsObject(PpsEnvironment environment, IDataReader r)
		{
			this.environment = environment;
			this.objectId = r.GetInt64(0);

			this.columns = new PpsObjectColumns(this);
			this.data = null;
			this.staticValues = new object[staticColumns.Length];
			this.tags = new PpsObjectTags(this);
			this.links = new PpsObjectLinks(this);

			ReadObjectInfo(r);
		} // ctor

		private void ReadObjectInfo(IDataReader r)
		{
			// update the values
			for (var i = 1; i < StaticColumns.Length; i++)
				SetValue(i, r.IsDBNull(i) ? null : r.GetValue(i));

			// check for tags
			if (r.FieldCount >= StaticColumns.Length && !r.IsDBNull(StaticColumns.Length))
				tags.RefreshTagsFromString(r.GetString(StaticColumns.Length));
		} // proc ReadObjectInfo

		/// <summary>Refresh of the object data.</summary>
		/// <param name="transaction">Optional parent transaction.</param>
		public void Refresh(bool withTags = false, PpsMasterDataTransaction transaction = null)
		{
			// refresh core data
			lock (objectLock)
			{
				using (var trans = environment.MasterData.CreateTransaction(transaction))
				using(var cmd = trans.CreateNativeCommand(StaticColumnsSelect + " WHERE o.Id = @Id"))
				{
					cmd.AddParameter("@Id", DbType.Int64, objectId);

					using (var r = cmd.ExecuteReaderEx(CommandBehavior.SingleRow))
					{
						if (r.Read())
							ReadObjectInfo(r);
						else
							throw new InvalidOperationException("No result set.");
					}
				}
			}

			// refresh tags
			if (withTags)
				tags.RefreshTags(transaction);
		} // proc Refresh

		#endregion

		#region -- Pull, Push -------------------------------------------------------------

		private void GetObjectUri(out LuaTable objectInfo, out object objectUri)
		{
			objectInfo = Environment.GetObjectInfo(Typ);
			objectUri = objectInfo?.GetMemberValue("objectUri") ?? Typ;
		} // proc GetObjectUri

		private PpsProxyRequest PullDataRequest(long revisionId)
		{
			GetObjectUri(out var objectInfo, out var objectUri);

			var acceptedMimeType = objectInfo?.GetMemberValue("acceptedMimeType") as string;

			// create a proxy request, and enqueue it with high priority
			return Environment.GetProxyRequest($"{objectUri}/?action=pull&id={objectId}&rev={revisionId}");
		} // proc PullDataAsync

		private PpsProxyRequest PushDataRequest()
		{
			GetObjectUri(out var objectInfo, out var objectUri);
			var request = Environment.GetProxyRequest($"{objectUri}/?action=push");
			request.Method = HttpMethod.Put.Method;
			return request;
		} //func PushDataRequest

		private void UpdateObjectId(PpsMasterDataTransaction trans, long newObjectId)
		{
			if (newObjectId < 0)
				throw new ArgumentOutOfRangeException(nameof(objectId), newObjectId, "New object Id is invalid.");
			else if (objectId > 0 && objectId != newObjectId)
				throw new ArgumentOutOfRangeException(nameof(Id), newObjectId, "Object id is different.");

			using (var cmd = trans.CreateNativeCommand("UPDATE main.[Objects] SET Id = @Id WHERE Id = @OldId"))
			{
				cmd.AddParameter("@Id", DbType.Int64, newObjectId);
				cmd.AddParameter("@OldId", DbType.Int64, objectId);
				cmd.ExecuteNonQueryEx();

				objectId = newObjectId; // todo: generic transaction will be a good idea
			}
		} // proc UpdateObjectId

		private void ReadObjectFromXml(XElement x)
		{
			// update object data
			SetValue((int)PpsStaticObjectColumnIndex.Guid, x.GetAttribute(nameof(Guid), Guid));
			SetValue((int)PpsStaticObjectColumnIndex.Typ, x.GetAttribute(nameof(Typ), Typ));
			SetValue((int)PpsStaticObjectColumnIndex.Nr, x.GetAttribute(nameof(Nr), Nr));
			SetValue((int)PpsStaticObjectColumnIndex.MimeType, x.GetAttribute(nameof(MimeType), MimeType));
			SetValue((int)PpsStaticObjectColumnIndex.IsRev, x.GetAttribute(nameof(IsRev), IsRev));
			SetValue((int)PpsStaticObjectColumnIndex.RemoteHeadRevId, x.GetAttribute("HeadRevId", RemoteHeadRevId));
			SetValue((int)PpsStaticObjectColumnIndex.RemoteCurRevId, x.GetAttribute("CurRevId", RemoteCurRevId));

			// links

			// tags

		} // UpdateObjectFromXml

		private async Task<IPpsProxyTask> EnqueuePull(PpsMasterDataTransaction transaction)
		{
			// check if the environment is online, force online
			await Environment.ForceOnlineAsync();

			// load the data from the server
			if (objectId <= 0)
				throw new ArgumentOutOfRangeException("objectId", "Invalid server id, this is a local only object and has no server representation.");

			var pulledRevId = RemoteHeadRevId;

			// check download manager
			lock (objectLock)
			{
				var request = PullDataRequest(pulledRevId);
				if (environment.WebProxy.TryGet(request, out var task))
					return task;

				// create new request
				request.SetUpdateOfflineCache(c =>
				{
					// first read the object header
					var headerLength = c.GetProperty("ppsn-header-length", -1L);
					if (headerLength < 10)
						throw new ArgumentOutOfRangeException("ppsn-header-length", headerLength, "Header is missing.");

					using (var headerData = new WindowStream(c.Content, 0, headerLength, false, true))
					using (var xmlHeader = XmlReader.Create(headerData, Procs.XmlReaderSettings))
					{
						var xObject = XElement.Load(xmlHeader);
						ReadObjectFromXml(xObject);

						// persist current object state
						UpdateLocal(transaction);
					}

					// update data block
					SaveRawDataAsync(transaction, c.ContentLength, c.ContentType,
						dst => c.Content.CopyTo(dst)
					).Wait();
					return c.Content;
				});

				// read the object stream from server
				return request.Enqueue(PpsLoadPriority.ObjectPrimaryData, true);
			}
		} // proc PullDataAsync

		public async Task PullAsync(PpsMasterDataTransaction transaction, long revId = -1)
		{
			if (revId == -1)
				revId = RemoteHeadRevId;

			using (var r = await (await EnqueuePull(transaction)).ForegroundAsync())
			{
				SetValue((int)PpsStaticObjectColumnIndex.PulledRevId, revId);

				// read prev stored data
				if (data != null)
					await data.LoadAsync(transaction);
			}
		} // proc PullDataAsync

		public async Task PushAsync(PpsMasterDataTransaction transaction = null)
		{
			XElement xAnswer;
			using (var trans = Environment.MasterData.CreateTransaction(transaction))
			{
				var request = PushDataRequest();

				Monitor.Enter(objectLock);
				try
				{
					// first build object data
					var headerData = Encoding.Unicode.GetBytes(ToXml().ToString(SaveOptions.DisableFormatting));
					request.Headers["ppsn-header-length"] = headerData.Length.ChangeType<string>();

					// write data
					using (var dst = request.GetRequestStream())
					{
						// write object structure
						dst.Write(headerData, 0, headerData.Length);

						// write the content
						await data.PushAsync(trans, dst);
					}
				}
				finally
				{
					Monitor.Exit(objectLock);
				}

				// get the result
				xAnswer = Environment.Request.GetXml(await request.GetResponseAsync());
				if (xAnswer.Name.LocalName == "push") // something is wrong / pull request.
				{
					throw new Exception("todo: exception for UI pull request.");
				}
				else if (xAnswer.Name.LocalName == "object")
				{
					// update id and basic meta data
					var newObjectId = xAnswer.GetAttribute<long>(nameof(Id), -1);
					UpdateObjectId(trans, newObjectId);

					// update meta data
					ReadObjectFromXml(xAnswer);

					// repull the whole object
					await PullAsync(trans, RemoteHeadRevId);

					// write local database
					UpdateLocal(trans);

					trans.Commit();
				}
				else
					throw new ArgumentException("Could not parse push-answer.");
			}
		} // proc PushAsync

		public async Task<T> GetDataAsync<T>(PpsMasterDataTransaction transaction, bool asyncPullData = false)
			where T : IPpsObjectData
		{
			if (data == null)
			{
				// update data from server, if not present (pull head)
				if (objectId >= 0 && !HasData)
					await PullAsync(transaction);

				// create the core data object
				data = await environment.CreateObjectDataObjectAsync<T>(this);
			}
			return (T)data;
		} // func GetDataAsync

		internal async Task<Stream> LoadRawDataAsync(PpsMasterDataTransaction transaction = null)
		{
			using (var trans = environment.MasterData.CreateTransaction(transaction))
			using (var cmd = trans.CreateNativeCommand("SELECT Document, DocumentIsLinked, length(Document) FROM main.Objects WHERE Id = @Id"))
			{
				cmd.AddParameter("@Id", DbType.Int64, objectId);

				using (var r = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow))
				{
					if (r.Read() && !r.IsDBNull(0))
					{
						var data = new byte[r.GetInt64(2)];
						r.GetBytes(0, 0, data, 0, data.Length);

						if (!r.IsDBNull(1) && r.GetValue(1).ChangeType<bool>()) // linked document
						{
							var path = Encoding.Unicode.GetString(data);
							return new FileStream(environment.MasterData.GetLocalPath(path), FileMode.Open);
						}
						else
							return new MemoryStream(data, false);
					}
					else
						return null;
				}
			}
		} // func LoadRawDataAsync

		internal async Task SaveRawDataAsync(PpsMasterDataTransaction transaction, long contentLength, string mimeType, Action<Stream> data)
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
			using (var trans = environment.MasterData.CreateTransaction(transaction))
			using (var cmd = trans.CreateNativeCommand("UPDATE main.Objects " +
				"SET " +
					"PulledRevId = IFNULL(@PulledRevId, PulledRevId), " +
					"MimeType = @MimeType," +
					"Document = @Document, " +
					"DocumentIsLinked = 0, " +
					"DocumentIsChanged = @DocumentIsChanged, " +
					"_IsUpdated = 1 " +
				"WHERE Id = @Id"))
			{
				cmd.AddParameter("@Id", DbType.Int64, objectId);
				cmd.AddParameter("@PulledRevId", DbType.Int64, bData == null ? DBNull.Value : PulledRevId.DbNullIf(StuffDB.DbNullOnNeg));
				cmd.AddParameter("@MimeType", DbType.String, mimeType.DbNullIfString());
				cmd.AddParameter("@Document", DbType.Binary, bData == null ? (object)DBNull.Value : bData);
				cmd.AddParameter("@DocumentIsChanged", DbType.Boolean, bData == null ? false : true);
				
				await cmd.ExecuteNonQueryAsync();
				SetValue((int)PpsStaticObjectColumnIndex.HasData, true);

				trans.Commit();
			}
		} // proc SaveRawDataAsync

		public override string ToString()
			=> $"Object: {Typ}; {objectId} # {Guid}:{PulledRevId}";

		#endregion
		
		private XElement ToXml()
		{
			var xObj = new XElement("object");
			xObj.Add(
				Procs.XAttributeCreate("Id", objectId, -1L),
				Procs.XAttributeCreate("Guid", Guid, Guid.Empty),
				Procs.XAttributeCreate("Typ", Typ, null),
				Procs.XAttributeCreate("MimeType", MimeType),
				Procs.XAttributeCreate("Nr", Nr)
			);

			// todo: add links

			return xObj;
		} // proc ToXml

		public void UpdateLocal(PpsMasterDataTransaction transaction)
		{

			using (var cmd = transaction.CreateNativeCommand(
				"UPDATE main.[Objects] SET " +
						"Guid = @Guid," +
						"Typ = @Typ," +
						"Nr = @Nr," +
						"MimeType = @MimeType," +
						"RemoteCurRevId = @CurRevId," +
						"RemoteHeadRevId = @HeadRevId," +
						"PulledRevId = @PulledRevId " +
					"WHERE Id = @Id"))
			{
				cmd.AddParameter("@Id", DbType.Int64, objectId);
				cmd.AddParameter("@Guid", DbType.Guid, Guid);
				cmd.AddParameter("@Typ", DbType.String, Typ.DbNullIfString());
				cmd.AddParameter("@Nr", DbType.String, Nr.DbNullIfString());
				cmd.AddParameter("@MimeType", DbType.String, MimeType.DbNullIfString());
				cmd.AddParameter("@IsRev", DbType.Boolean, IsRev);
				cmd.AddParameter("@CurRevId", DbType.Int64, RemoteCurRevId.DbNullIf(-1L));
				cmd.AddParameter("@HeadRevId", DbType.Int64, RemoteHeadRevId.DbNullIf(-1L));
				cmd.AddParameter("@PulledRevId", DbType.Int64, PulledRevId.DbNullIf(-1L));

				cmd.ExecuteNonQueryEx();
			}

			// links

			// tags
		} // proc UpdateLocal

		#region -- Properties -------------------------------------------------------------

		internal void OnPropertyChanged(string propertyName)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		private T GetValue<T>(int index, T empty)
			=> index == 0 ? (T)(object)objectId : (staticValues[index] ?? empty).ChangeType<T>();

		private void SetValue(int index, object newValue)
		{
			if (!Object.Equals(staticValues[index], newValue))
			{
				staticValues[index] = newValue;
				OnPropertyChanged(staticColumns[index].Name);
			}
		} // func SetValue

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
					else if (index == 0)
						return objectId;
					else if (index < StaticColumns.Length)
						return staticValues[index];
					else if (index == StaticColumns.Length + 0)
					{
						if (data == null)
							data = GetDataAsync<IPpsObjectData>(null, true).Result;
						return data;
					}
					else if (index == StaticColumns.Length + 1)
						return tags;
					else if (index == StaticColumns.Length + 2)
						return links;
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

		public long Id => objectId;
		public Guid Guid => GetValue((int)PpsStaticObjectColumnIndex.Guid, Guid.Empty);
		public string Typ => GetValue((int)PpsStaticObjectColumnIndex.Typ, String.Empty);
		public string MimeType => GetValue((int)PpsStaticObjectColumnIndex.MimeType, String.Empty);
		public string Nr => GetValue((int)PpsStaticObjectColumnIndex.Nr, String.Empty);
		public bool IsRev => GetValue((int)PpsStaticObjectColumnIndex.IsRev, false);
		public long RemoteCurRevId => GetValue((int)PpsStaticObjectColumnIndex.RemoteCurRevId, -1L);
		public long RemoteHeadRevId => GetValue((int)PpsStaticObjectColumnIndex.RemoteHeadRevId, -1L);
		public long PulledRevId => GetValue((int)PpsStaticObjectColumnIndex.PulledRevId, -1L);
		public bool IsDocumentChanged => GetValue((int)PpsStaticObjectColumnIndex.IsDocumentChanged, false);
		public bool HasData => GetValue((int)PpsStaticObjectColumnIndex.HasData, false);

		public PpsObjectLinks Links => links.RefreshLazy();
		public PpsObjectTags Tags => tags;

		internal object SyncRoot => objectLock;

		// -- Static ----------------------------------------------------------------

		private static PpsStaticObjectColumn[] staticColumns;
		
		private enum PpsStaticObjectColumnIndex
		{
			Guid = 1,
			Typ,
			MimeType,
			Nr,
			IsRev,
			RemoteCurRevId,
			RemoteHeadRevId,
			PulledRevId,
			IsDocumentChanged,
			HasData
		} // enum PpsStaticObjectColumnIndex

		static PpsObject()
		{
			staticColumns = new PpsStaticObjectColumn[]
			{
				new PpsStaticObjectColumn(nameof(Id), "o.Id", typeof(long)),
				new PpsStaticObjectColumn(nameof(Guid),"o.Guid", typeof(Guid)),
				new PpsStaticObjectColumn(nameof(Typ), "o.Typ", typeof(string)),
				new PpsStaticObjectColumn(nameof(MimeType), "o.MimeType", typeof(string)),
				new PpsStaticObjectColumn(nameof(Nr),"o.Nr", typeof(string)),
				new PpsStaticObjectColumn(nameof(IsRev),"o.IsRev", typeof(bool)),
				new PpsStaticObjectColumn(nameof(RemoteCurRevId),"o.RemoteCurRevId", typeof(long)),
				new PpsStaticObjectColumn(nameof(RemoteHeadRevId), "o.RemoteHeadRevId", typeof(long)),
				new PpsStaticObjectColumn(nameof(PulledRevId),"o.PulledRevId", typeof(long)),
				new PpsStaticObjectColumn(nameof(IsDocumentChanged), "o.DocumentIsChanged", typeof(bool)),
				new PpsStaticObjectColumn(nameof(HasData), "length(o.Document)", typeof(bool))
			};

			StaticColumnsSelect = "SELECT " + String.Join(",", staticColumns.Select(c => c.Expression)) + " FROM main.[Objects] o";
		} // ctor

		internal static string GetStaticColumnExpression(int index)
			=> staticColumns[index].Expression;

		internal const int StaticPropertyCount = 3;

		internal static IDataColumn[] StaticColumns => staticColumns;
		internal static string StaticColumnsSelect { get; }

		internal static IDataColumn StaticDataColumn { get; } = new SimpleDataColumn("Data", typeof(IPpsObjectData));
		internal static IDataColumn StaticTagsColumn { get; } = new SimpleDataColumn(nameof(Tags), typeof(PpsObjectTags));
		internal static IDataColumn StaticLinksColumn { get; } = new SimpleDataColumn(nameof(Links), typeof(PpsObjectLinks));
	} // class PpsObject

	#endregion

	#region -- class PpsObjectInfo ------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Special environment table, that holds information about the 
	/// object class.</summary>
	public sealed class PpsObjectInfo : LuaEnvironmentTable, IPpsEnvironmentDefinition
	{
		private readonly string name;
		private bool createServerSiteOnly = false;
		private bool isRev = false;
		
		public PpsObjectInfo(PpsEnvironment environemnt, string name)
			: base(environemnt)
		{
			this.name = name;
		} // ctor

		/// <summary>Creates a new local number for the document.</summary>
		/// <param name="transaction">Database transaction.</param>
		/// <returns><c>null</c>, or a temporary local number for the user.</returns>
		[LuaMember]
		public string GetNextNumber(PpsMasterDataTransaction transaction)
		{
			using (var cmd = transaction.CreateNativeCommand("SELECT max(Nr) FROM main.[Objects] WHERE substr(Nr, 1, 3) = '*n*' AND abs(substr(Nr, 4)) != 0.0")) //SELECT max(Nr) FROM main.[Objects] WHERE substr(Nr, 1, 3) = '*n*' AND typeof(substr(Nr, 4)) = 'integer'
			{
				var lastNrString = cmd.ExecuteScalarEx() as string;
				var lastNr = lastNrString == null ? 0 : Int32.Parse(lastNrString.Substring(3));
				return "*n*" + (lastNr + 1).ToString("000");
			}
		} // func GetNextNumber

		[LuaMember]
		public string Name
		{
			get { return name; }
			private set { }
		} // prop Name

		/// <summary>If this option is set, new documents can only create on the server site (type: bool, default: false).</summary>
		[LuaMember]
		public bool CreateServerSiteOnly
		{
			get => createServerSiteOnly;
			set => SetDeclaredMember(ref createServerSiteOnly, value, nameof(CreateServerSiteOnly));
		} // prop CreateServerSiteOnly

		/// <summary>Holds the document uri for loading a schema.</summary>
		[LuaMember]
		public string DocumentUri => Environment.ActiveDataSets.GetDataSetSchemaUri(name);

		/// <summary>Will this object have revision.</summary>
		[LuaMember]
		public bool IsRev
		{
			get => isRev;
			set => SetDeclaredMember(ref isRev, value, nameof(IsRev));
		}
	} // class PpsObjectInfo

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

		private const bool useId = false;
		private const bool useGuid = true;

		#region -- CreateObjectFilter -----------------------------------------------------

		#region -- class PpsObjectEnumerator -----------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class PpsObjectEnumerator : IEnumerator<PpsObject>, IDataColumns
		{
			private readonly PpsEnvironment environment;
			private readonly SQLiteCommand command;
			private DbDataReader reader;
			private PpsObject current;

			public PpsObjectEnumerator(PpsEnvironment environment, SQLiteCommand command)
			{
				this.environment = environment ?? throw new ArgumentNullException(nameof(environment));
				this.command = command ?? throw new ArgumentNullException(nameof(command));
			} // ctor

			public void Dispose()
			{
				reader?.Dispose();
				command.Dispose();
			} // proc Dispose

			public bool MoveNext()
			{
				if (reader == null)
					reader = command.ExecuteReaderEx(CommandBehavior.SingleResult);

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
				} // func CreateLeftOuterJoinExpression

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
					cmd.Append(PpsObject.GetStaticColumnExpression(i))
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
		
		/// <summary>Create a new object in the local database.</summary>
		/// <param name="transaction"></param>
		/// <param name="objectInfo"></param>
		/// <returns></returns>
		public PpsObject CreateNewObject(PpsMasterDataTransaction transaction, PpsObjectInfo objectInfo)
			=> CreateNewObject(transaction, Guid.NewGuid(), objectInfo.Name, objectInfo.GetNextNumber(transaction), objectInfo.IsRev);
		
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
		public PpsObject CreateNewObject(PpsMasterDataTransaction transaction, Guid guid, string typ, string nr, bool isRev)
		{
			using (var trans = MasterData.CreateTransaction(transaction))
			using (var cmd = trans.CreateNativeCommand(
				"INSERT INTO main.Objects (Id, Guid, Typ, Nr, IsHidden, IsRev, _IsUpdated) " +
				"VALUES (@Id, @Guid, @Typ, @Nr, 0, @IsRev, 1)"))
			{
				var newObjectId = trans.GetNextLocalId(transaction, "Objects", "Id");
				cmd.AddParameter("@Id", DbType.Int64, newObjectId);
				cmd.AddParameter("@Guid", DbType.Guid, guid);
				cmd.AddParameter("@Typ", DbType.String, typ.DbNullIfString());
				cmd.AddParameter("@Nr", DbType.String, nr.DbNullIfString());
				cmd.AddParameter("@IsRev", DbType.Boolean, isRev);
				
				cmd.ExecuteNonQueryEx();
				trans.Commit();

				return GetObject(newObjectId, transaction);
			}
		} // func CreateNewObject

		internal async Task<T> CreateObjectDataObjectAsync<T>(PpsObject obj)
			where T : IPpsObjectData
		{
			var schema = await ActiveDataSets.GetDataSetDefinitionAsync(obj.Typ);
			if (schema == null)
				return (T)(IPpsObjectData)new PpsObjectBlobData(obj);
			else
				return (T)(IPpsObjectData)new PpsObjectDataSet(schema, obj);
		} // func CreateObjectDataObjectAsync

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

		private PpsObject ReadObject(object key, bool useGuid, PpsMasterDataTransaction transaction, bool throwException = false)
		{
			// refresh core data
			using (var trans = MasterData.CreateTransaction(transaction))
			using (var cmd = trans.CreateNativeCommand(PpsObject.StaticColumnsSelect + (useGuid ? " WHERE o.Guid = @Guid" : " WHERE o.Id = @Id")))
			{
				if (useGuid)
					cmd.AddParameter("@Guid", DbType.Guid, key);
				else
					cmd.AddParameter("@Id", DbType.Int64, key);

				using (var r = cmd.ExecuteReaderEx(CommandBehavior.SingleRow))
				{
					if (r.Read())
						return UpdateCacheItem(new PpsObject(this, r));
					else if (throwException)
						throw new ArgumentOutOfRangeException($"Object with key '{key}' not found.", nameof(key));
					else
						return null;
				}
			}
		} // func ReadObject

		private bool IsEmptyObject(WeakReference<PpsObject> c)
		{
			if (c == null)
				return true;
			return !c.TryGetTarget(out var o);
		} // func IsEmptyObject

		private PpsObject UpdateCacheItem(PpsObject obj)
		{
			// find a cache index
			if (!objectStoreByGuid.TryGetValue(obj.Guid, out var cacheIndex) && !objectStoreById.TryGetValue(obj.Id, out cacheIndex))
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
			objectStoreById[obj.Id] = cacheIndex;
			objectStoreByGuid[obj.Guid] = cacheIndex;

			return obj;
		} // func UpdateCacheItem

		private PpsObject GetCachedObject<T>(Dictionary<T, int> index, T key)
		{
			if (index.TryGetValue(key, out var cacheIndex))
			{
				var reference = objectStore[cacheIndex];
				if (reference != null)
				{
					if (reference.TryGetTarget(out var obj))
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

		private PpsObject GetCachedObjectOrRead<T>(Dictionary<T, int> index, T key, bool keyIsGuid, PpsMasterDataTransaction transaction = null, bool throwException = false)
		{
			lock (objectStoreLock)
			{
				// check if the object is in memory
				return GetCachedObject(index, key)
					// object is not in memory, create a instance
					?? ReadObject(key, keyIsGuid, transaction, throwException);
			}
		} // func GetCachedObject

		private PpsObject GetCachedObjectOrCreate(DbDataReader reader)
		{
			var localId = reader.GetInt64(0);
			lock (objectStoreLock)
			{
				return GetCachedObject(objectStoreById, localId)
					?? UpdateCacheItem(new PpsObject(this, reader));
			}
		} // func GetCachedObjectOrCreate

		[LuaMember]
		public PpsObject GetObject(long localId, PpsMasterDataTransaction transaction = null, bool throwException = false)
			=> GetCachedObjectOrRead(objectStoreById, localId, useId, transaction, throwException);

		[LuaMember]
		public PpsObject GetObject(Guid guid, PpsMasterDataTransaction transaction = null, bool throwException = false)
			=> GetCachedObjectOrRead(objectStoreByGuid, guid, useGuid, transaction, throwException);

		[LuaMember]
		public LuaTable GetObjectInfo(string objectTyp)
			=> objectInfo[objectTyp, false];

		#endregion
		
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

		#endregion
	} // class PpsEnvironment

	#endregion
}
