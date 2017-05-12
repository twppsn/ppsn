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
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
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
		Delete
	} // enum PpsObjectLinkRestriction

	#endregion

	#region -- class PpsObjectLink ------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsObjectLink
	{
		private readonly PpsObjectLinks parent;
		private long? id;                       // local id
		private readonly long linkToId;         // id of the linked object
		private readonly long? linkToLocalId;   // id for the object, that was used within the dataset (only neg numbers are allowed, it is for replacing before push)
		private PpsObjectLinkRestriction onDelete;  // is delete cascade possible

		private WeakReference<PpsObject> linkToCache; // weak ref to the actual object
		private int refCount; // how often is this link used

		private bool isChanged;

		internal PpsObjectLink(PpsObjectLinks parent, long? id, long linkToId, long? linkToLocalId, int refCount, PpsObjectLinkRestriction onDelete)
		{
			this.parent = parent ?? throw new ArgumentNullException(nameof(parent));

			this.id = id;
			this.linkToId = linkToId;
			this.linkToLocalId = linkToLocalId;
			this.refCount = refCount;
			this.isChanged = !id.HasValue;

			this.onDelete = onDelete;
		} // ctor

		public void AddRef()
		{
			refCount++;
			SetDirty();
		} // proc AddRef

		public void DecRef()
		{
			if (refCount > 0)
				refCount--;
			SetDirty();
		} // proc DecRef

		public void SetOnDelete(PpsObjectLinkRestriction newOnDelete, bool merge)
		{
			if (merge)
			{
				if (newOnDelete > onDelete)
					SetOnDelete(newOnDelete, false);
			}
			else if(newOnDelete != onDelete)
			{
				onDelete = newOnDelete;
				SetDirty();
			}
		}

		internal void SetDirty()
		{
			isChanged = true;
			parent.SetDirty();
		} // proc SetDirty

		internal void ResetDirty()
		{
			isChanged = false;
		} // proc ResetDirty

		private PpsObject GetLinkedObject()
		{
			if (linkToCache != null && linkToCache.TryGetTarget(out var r))
				return r;

			r = parent.Parent.Environment.GetObject(linkToId);
			linkToCache = new WeakReference<PpsObject>(r);
			return r;
		} // func GetLinkedObject

		public void Remove()
			=> parent.RemoveLink(this);

		public PpsObject Parent => parent.Parent;

		internal long? Id { get => id; set => id = value; }

		public long LinkToId => linkToId;
		public long? LinkToLocalId => linkToLocalId;

		public PpsObject LinkTo => GetLinkedObject();

		public int RefCount => refCount;
		public PpsObjectLinkRestriction OnDelete => onDelete;

		public bool IsChanged => isChanged;
	} // class PpsObjectLink

	#endregion

	#region -- class PpsObjectLinks -----------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsObjectLinks : IList, IReadOnlyList<PpsObjectLink>, INotifyCollectionChanged
	{
		/// <summary>Notify for link list changes.</summary>
		public event NotifyCollectionChangedEventHandler CollectionChanged;

		private readonly PpsObject parent;

		private bool isLoaded = false; // marks if the link list is loaded from the local store
		private bool isChanged = false;
		private readonly List<PpsObjectLink> links = new List<PpsObjectLink>(); // local active links
		private readonly List<PpsObjectLink> removedLinks = new List<PpsObjectLink>();

		internal PpsObjectLinks(PpsObject parent)
		{
			this.parent = parent ?? throw new ArgumentNullException(nameof(parent));
		} // ctor

		private void CheckLinksLoaded()
		{
			lock (parent.SyncRoot)
			{
				if (isLoaded)
					return;
				RefreshLinks();

				isLoaded = true;
			}
		} // proc CheckLinksLoaded

		internal void SetDirty()
		{
			isChanged = true;
			parent.SetDirty();
		} // proc SetDirty

		private void RefreshLinks()
		{
			links.Clear();
			removedLinks.Clear();

			using (var cmd = parent.Environment.MasterData.CreateNativeCommand("SELECT [Id], [LinkObjectId], [LinkObjectDataId], [RefCount], [OnDelete] FROM main.[ObjectLinks] WHERE [ParentObjectId] = @ObjectId"))
			{
				cmd.AddParameter("@ObjectId", DbType.Int64, parent.Id);

				using (var r = cmd.ExecuteReaderEx(CommandBehavior.SingleResult))
				{
					while (r.Read())
					{
						links.Add(new PpsObjectLink(
							this,
							r.GetInt64(0),
							r.GetInt64(1),
							r.IsDBNull(2) ? null : new long?(r.GetInt64(2)),
							r.GetInt32(3),
							ParseObjectLinkRestriction(r.IsDBNull(3) ? null : r.GetString(4))
						));
					}
				}
			}

			isChanged = false;
			parent.SetDirty();
		} // proc RefreshLinks

		internal void UpdateLocal(PpsMasterDataTransaction transaction)
		{
			lock (parent.SyncRoot)
			{
				if (!isLoaded || !isChanged)
					return;

				using (var insertCommand = transaction.CreateNativeCommand("INSERT INTO main.[ObjectLinks] (ParentObjectId, LinkObjectId, LinkObjectDataId, RefCount, OnDelete) " +
					"VALUES (@ParentObjectId, @LinkObjectId, @LinkObjectDataId, @RefCount, @OnDelete)"))
				{
					var insertParentIdParameter = insertCommand.AddParameter("@ParentObjectId", DbType.Int64);
					var insertLinkIdParameter = insertCommand.AddParameter("@LinkObjectId", DbType.Int64);
					var insertLinkDataIdParameter = insertCommand.AddParameter("@LinkObjectDataId", DbType.Int64);
					var insertRefCountParameter = insertCommand.AddParameter("@RefCount", DbType.Int32);
					var insertOnDeleteParameter = insertCommand.AddParameter("@OnDelete", DbType.String);

					foreach (var cur in links)
					{
						if (cur.IsChanged)
						{
							insertParentIdParameter.Value = parent.Id;
							insertLinkIdParameter.Value = cur.LinkToId;
							insertLinkDataIdParameter.Value = cur.LinkToLocalId ?? (object)DBNull.Value;
							insertRefCountParameter.Value = cur.RefCount;
							insertOnDeleteParameter.Value = FormatObjectLinkRestriction(cur.OnDelete);

							insertCommand.ExecuteNonQueryEx();
							cur.Id = transaction.LastInsertRowId;
							cur.ResetDirty();
							transaction.AddRollbackOperation(() => { cur.Id = null; cur.SetDirty(); });
						}
					}
				}

				if (removedLinks.Count > 0)
				{
					var removedLinksArray = removedLinks.ToArray();

					using (var deleteCommand = transaction.CreateNativeCommand("DELETE FROM main.[ObjectLinks] WHERE Id = @Id"))
					{
						var deleteIdParameter = deleteCommand.AddParameter("@Id", DbType.Int64);
						foreach (var cur in removedLinksArray)
						{
							deleteIdParameter.Value = cur.Id;
							deleteCommand.ExecuteNonQueryEx();

							removedLinks.Remove(cur);
						}
					}

					transaction.AddRollbackOperation(() =>
					{
						removedLinks.AddRange(removedLinksArray);
						isChanged = false;
					});
				}

				isChanged = false;
			}
		} // proc UpdateLocal

		internal void ReadLinksFromXml(IEnumerable<XElement> xLinks)
		{
			lock (parent.SyncRoot)
			{
				CheckLinksLoaded();

				var notProcessedLinks = new List<PpsObjectLink>(links);

				// add new links
				foreach (var x in xLinks)
				{
					var objectId = x.GetAttribute("objectId", -1L);
					var refCount = x.GetAttribute("refCount", 0);
					var onDelete = ParseObjectLinkRestriction(x.GetAttribute("onDelete", "R"));

					var linkExists = links.Find(c => c.LinkToId == objectId && c.OnDelete == onDelete);
					if (linkExists == null)
						links.Add(new PpsObjectLink(this, null, objectId, null, refCount, onDelete));
					else
						notProcessedLinks.Remove(linkExists);
				}

				// remove untouched links
				foreach (var cur in notProcessedLinks)
				{
					links.Remove(cur);
					if (cur.Id.HasValue)
						removedLinks.Add(cur);
				}

				isChanged = true;
				parent.SetDirty();
			}
			OnCollectionReset();
		} // proc ReadLinksFromXml

		internal void AddToXml(XElement xParent, XName linkElementName)
		{
			lock (parent.SyncRoot)
			{
				CheckLinksLoaded();

				foreach (var c in links)
				{
					if (c.LinkToId < 0)
						throw new ArgumentException("Only positive object id's can be pushed to the server.");

					xParent.Add(
						new XElement(linkElementName,
							new XAttribute("objectId", c.LinkToId),
							new XAttribute("refCount", c.RefCount),
							new XAttribute("onDelete", FormatObjectLinkRestriction(c.OnDelete))
						)
					);
				}
			}
		} // func AddToXml

		public void AppendLink(PpsObject linkTo, PpsObjectLinkRestriction onDelete)
		{
			lock (parent.SyncRoot)
			{
				CheckLinksLoaded();

				// add link
				var currentLink = links.Find(c => c.LinkToId == linkTo.Id);
				if (currentLink != null)
				{
					currentLink.SetOnDelete(onDelete, true);
					currentLink.AddRef();
				}
				else
				{
					links.Add(new PpsObjectLink(this, null, linkTo.Id, linkTo.Id < 0 ? new long?(linkTo.Id) : null, 1, onDelete));
					SetDirty();
				}
			}

			OnCollectionReset();
		} // proc AppendLink

		public void RemoveLink(long objectId)
		{
			lock (parent.SyncRoot)
			{
				CheckLinksLoaded();
				var l = links.Find(c => c.LinkToId == objectId);
				if (l == null)
					throw new ArgumentOutOfRangeException(nameof(objectId));
				RemoveLink(l);
			}
		} // func RemoveLink

		public void RemoveLink(PpsObjectLink link)
		{
			lock (parent.SyncRoot)
			{
				var idx = links.IndexOf(link);
				if (idx < 0)
					throw new ArgumentOutOfRangeException(nameof(link));

				link.DecRef();
				if (link.RefCount == 0)
				{
					links.RemoveAt(idx);
					if (link.Id.HasValue)
					{
						removedLinks.Add(link);
						SetDirty();
					}
				}
			}

			OnCollectionReset();
		} // proc RemoveLink

		public long TranslateObjectId(long currentObjectId)
		{
			if (currentObjectId >= 0)
				return currentObjectId;
			lock (parent.SyncRoot)
			{
				CheckLinksLoaded();
				return links.FirstOrDefault(c => c.LinkToLocalId == currentObjectId)?.LinkToId ?? currentObjectId;
			}
		} // func TranslateObjectId

		private void OnCollectionReset()
			=> CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

		void ICollection.CopyTo(Array array, int index)
		{
			CheckLinksLoaded();
			((ICollection)links).CopyTo(array, index);
		} // proc ICollection.CopyTo

		public bool Constains(PpsObjectLink link)
			=> IndexOf(link) >= 0;

		public int IndexOf(PpsObjectLink link)
		{
			lock (parent.SyncRoot)
			{
				CheckLinksLoaded();
				return links.IndexOf(link);
			}
		} // func IndexOf

		bool IList.Contains(object value)
			=> IndexOf(value as PpsObjectLink) >= 0;

		int IList.IndexOf(object value)
			=> IndexOf(value as PpsObjectLink);

		int IList.Add(object value)
			=> throw new NotSupportedException();
		void IList.Clear()
			=> throw new NotSupportedException();
		void IList.Insert(int index, object value)
			=> throw new NotSupportedException();
		void IList.Remove(object value)
			=> throw new NotSupportedException();
		void IList.RemoveAt(int index)
			=> throw new NotSupportedException();

		bool IList.IsReadOnly => true;
		bool IList.IsFixedSize => false;
		object ICollection.SyncRoot => parent.SyncRoot;
		bool ICollection.IsSynchronized => true;
		object IList.this[int index] { get => this[index]; set => throw new NotSupportedException(); }

		public IEnumerator<PpsObjectLink> GetEnumerator()
		{
			lock (parent.SyncRoot)
			{
				CheckLinksLoaded();
				foreach (var c in links)
					yield return c;
			}
		} // func GetEnumerator

		public PpsObjectLink this[int index]
		{
			get
			{
				lock (parent.SyncRoot)
				{
					CheckLinksLoaded();
					return links[index];
				}
			}
		} // prop this 

		public int Count
		{
			get
			{
				lock (parent.SyncRoot)
				{
					CheckLinksLoaded();
					return links.Count;
				}
			}
		} // prop Count

		IEnumerator IEnumerable.GetEnumerator()
			=> GetEnumerator();

		public PpsObject Parent => parent;

		public static PpsObjectLinkRestriction ParseObjectLinkRestriction(string onDelete)
		{
			if (String.IsNullOrEmpty(onDelete))
				return PpsObjectLinkRestriction.Restrict;
			else
				switch (Char.ToUpper(onDelete[0]))
				{
					case 'N':
						return PpsObjectLinkRestriction.Null;
					case 'R':
						return PpsObjectLinkRestriction.Restrict;
					case 'D':
						return PpsObjectLinkRestriction.Delete;
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
				default:
					throw new ArgumentOutOfRangeException($"Can not format '{onDelete}'.");
			}
		} // func FormatObjectLinkRestriction
	} // class PpsObjectLinks

	#endregion

	#region -- class PpsObjectTagView ---------------------------------------------------

	public sealed class PpsObjectTagView : INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler PropertyChanged;

		private readonly PpsObject parent;

		private long? id;
		private readonly string key;

		private readonly bool serverValuesLoaded;
		private readonly PpsObjectTagClass? serverClass; // can null, if the tag was loaded from the view, or not exists in the server
		private readonly object serverValue;
		private PpsObjectTagClass? localClass;
		private object localValue; // the, that is set from the user, and not pushed to the server

		private long userId;

		private bool isChanged;
		private bool setToDefault = false;
		private bool userIsNull = false;
		private WeakReference<PpsMasterDataRow> userRow = null;

		internal PpsObjectTagView(PpsObject parent, long? id, string key, bool serverValuesLoaded, PpsObjectTagClass? serverClass, object serverValue, PpsObjectTagClass? localClass, object localValue, long userId, bool isChanged)
		{
			this.parent = parent ?? throw new ArgumentNullException(nameof(parent));
			this.id = id;
			this.key = key ?? throw new ArgumentNullException(nameof(key));
			this.serverValuesLoaded = serverValuesLoaded;
			this.serverClass = serverClass;
			this.serverValue = serverValue;
			this.localClass = localClass;
			this.localValue = localValue;
			this.userId = userId;

			this.isChanged = isChanged;
		} // ctor

		private void OnPropertyChanged(string propertyName)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		private PpsMasterDataRow GetUserRow()
		{
			lock (parent.SyncRoot)
			{
				if (UserId <= 0 || userIsNull)
					return null;
				else if (userRow.TryGetTarget(out var row))
					return row;
				else
				{
					var table = parent.Environment.MasterData.GetTable("User");
					if (table != null)
					{
						row = table.GetRowById(UserId, false);
						if (row != null)
						{
							userRow = new WeakReference<PpsMasterDataRow>(row);
							userIsNull = false;
						}
						else
							userIsNull = true;
					}
					else
						userIsNull = true;

					return row;
				}
			}
		} // ctor

		public void Reset()
		{
			if (id.HasValue)
			{
				if (serverValuesLoaded)
				{
					localClass = null;
					localValue = null;

					OnPropertyChanged(nameof(Class));
					OnPropertyChanged(nameof(Value));
					OnPropertyChanged(nameof(IsRemoved));

					setToDefault = true;
				}

				setToDefault = true;
				SetDirty();
			}
			else
				Remove();
		} // proc Reset

		public void Update(PpsObjectTagClass newClass, object newValue)
		{
			// correct strings
			if (newValue is string t && String.IsNullOrEmpty(t))
				newValue = null;

			// update values
			if (!localClass.HasValue
				|| localClass.Value != newClass
				|| !Object.Equals(localValue, newValue))
			{
				if (newClass == PpsObjectTagClass.Deleted)
				{ } // check if the tag can deleted

				localClass = newClass;
				localValue = newValue;

				OnPropertyChanged(nameof(Class));
				OnPropertyChanged(nameof(Value));
				OnPropertyChanged(nameof(IsRemoved));

				SetDirty();
			}
		} // proc UpdateTag

		public void Remove()
			=> Update(PpsObjectTagClass.Deleted, localValue);

		private void SetDirty()
		{
			if (!isChanged)
			{
				isChanged = true;
				OnPropertyChanged(nameof(IsChanged));
				parent.SetDirty();
			}
		} // proc SetDirty

		internal void ResetDirty(PpsMasterDataTransaction transaction)
		{
			if (isChanged)
			{
				transaction?.AddRollbackOperation(SetDirty);
				isChanged = false;
				OnPropertyChanged(nameof(IsChanged));
			}
		} // proc ResetDirty

		/// <summary>Internal Id.</summary>
		internal long? Id { get => id; set => id = value; }
		/// <summary>Name of the tag.</summary>
		public string Name => key;

		/// <summary>Returns the current class of the tag.</summary>
		public PpsObjectTagClass Class => serverClass ?? localClass ?? PpsObjectTagClass.Deleted;

		/// <summary>Returns the current value, for the item.</summary>
		public object Value =>
			localClass.HasValue
				? localValue
				: (serverClass.HasValue ? serverClass : null);

		/// <summary>User that created this tag, or 0 for system created (like autotagging, states)</summary>
		public long UserId => userId;

		/// <summary>Reference to the master data.</summary>
		public PpsMasterDataRow User => GetUserRow();
		/// <summary>Is this tag removed.</summary>
		public bool IsRemoved => Class == PpsObjectTagClass.Deleted;

		/// <summary>Is this tag changed.</summary>
		public bool IsChanged => isChanged;

		internal bool SetToDefault { get => (serverClass.HasValue && !localClass.HasValue) || setToDefault; set => setToDefault = false; }
	} // class PpsObjectTagView

	#endregion

	#region -- class PpsObjectTags ------------------------------------------------------

	public sealed class PpsObjectTags : IList, IReadOnlyList<PpsObjectTagView>, INotifyCollectionChanged
	{
		public event NotifyCollectionChangedEventHandler CollectionChanged;

		private readonly PpsObject parent;
		private readonly List<PpsObjectTagView> tags;

		private bool isLoaded = false;

		internal PpsObjectTags(PpsObject parent)
		{
			this.parent = parent ?? throw new ArgumentNullException(nameof(parent));
			this.tags = new List<PpsObjectTagView>();
		} // ctor

		#region -- Refresh ----------------------------------------------------------------

		private void CheckTagsLoaded()
		{
			lock (parent.SyncRoot)
			{
				if (isLoaded)
					return;

				RefreshTags();
			}
		} // proc CheckTagsLoaded

		/// <summary>Reads the text from a text-lob. (new line separated, id:key:class:userId=value</summary>
		/// <param name="tagList"></param>
		internal void RefreshTags(string tagList)
		{
			bool IsServerLoaded(string id)
			{

				switch (id[0])
				{
					case 'S':
					case 's':
						return true;
					case 'L':
					case 'l':
						return false;
					default:
						throw new FormatException();
				}
			} // func IsServerLoaded

			lock (parent.SyncRoot)
			{
				// clear current state
				tags.Clear();

				foreach (var c in tagList.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
				{
					var p = c.IndexOf(':');
					if (p <= 0)
						continue;


					var idText = c.Substring(0, p);
					var isServerLoaded = IsServerLoaded(idText);
					var databaseId = Int64.Parse(idText.Substring(1));
					var tagData = PpsObjectTag.ParseTag(c.Substring(p + 1));

					var t = new PpsObjectTagView(
						parent,
						databaseId,
						tagData.Name,
						isServerLoaded,
						isServerLoaded ? new PpsObjectTagClass?(tagData.Class) : null,
						isServerLoaded ? tagData.Value : null,
						isServerLoaded ? null : new PpsObjectTagClass?(tagData.Class),
						isServerLoaded ? null : tagData.Value,
						tagData.UserId,
						false
					);

					tags.Add(t);
				}
			}
			isLoaded = true;
			OnCollectionReset();
		} // proc RefreshTags

		public void RefreshTags()
		{
			lock (parent.SyncRoot)
			{
				// clear current state
				tags.Clear();

				using (var trans = parent.Environment.MasterData.CreateReadUncommitedTransaction())
				{
					// refresh first all user generated tags
					using (var selectCommand = trans.CreateNativeCommand("SELECT [Id], [Key], [Class], [Value], [LocalClass], [LocalValue], [UserId] FROM main.[ObjectTags] WHERE ObjectId = @Id"))
					{
						selectCommand.AddParameter("@Id", DbType.Int64, parent.Id);
						using (var r = selectCommand.ExecuteReaderEx(CommandBehavior.SingleResult))
						{
							while (r.Read())
							{
								object FormatValue(PpsObjectTagClass? c, object v)
									=> c.HasValue && v != null ? Procs.ChangeType(v, PpsObjectTag.GetTypeFromClass(c.Value)) : null;

								var serverClass = r.IsDBNull(2) ? null : new PpsObjectTagClass?(PpsObjectTag.ParseClass(r.GetInt32(2)));
								var localClass = r.IsDBNull(4) ? null : new PpsObjectTagClass?(PpsObjectTag.ParseClass(r.GetInt32(4)));

								var t = new PpsObjectTagView(
									parent,
									r.GetInt64(0),
									r.GetString(1),
									true,
									serverClass,
									FormatValue(serverClass, r.GetValue(3)),
									localClass,
									FormatValue(localClass, r.GetValue(5)),
									r.IsDBNull(6) ? 0 : r.GetInt64(6),
									false
								);

								tags.Add(t);
							}
						}
					}
				}
				isLoaded = true;
			}
			OnCollectionReset();
		} // proc RefreshTags

		private void OnCollectionReset()
			=> CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

		#endregion

		#region -- UpdateLocal ------------------------------------------------------------

		internal void UpdateLocal(PpsMasterDataTransaction transaction)
		{
			lock (parent.SyncRoot)
			{
				var reReadAll = false;
				if (!isLoaded || !IsChanged)
					return;

				using (var updateCommand = transaction.CreateNativeCommand("UPDATE main.[ObjectTags] SET LocalClass = @LClass, LocalValue = @LValue, _IsUpdated = 1 WHERE Id = @Id"))
				using (var deleteCommand = transaction.CreateNativeCommand("DELETE FROM main.[ObjectTags] WHERE Id = @Id"))
				using (var setDefaultCommand = transaction.CreateNativeCommand("UPDATE main.[ObjectTags] SET LocalClass = null, LocalValue = @null, _IsUpdated = 0 WHERE Id = @Id"))
				using (var insertCommand = transaction.CreateNativeCommand("INSERT INTO main.[ObjectTags] (Id, ObjectId, Key, LocalClass, LocalValue, UserId, _IsUpdated) VALUES (@Id, @ObjectId, @Key, @LClass, @LValue, @UserId, 1)"))
				{
					var updateIdParameter = updateCommand.AddParameter("@Id", DbType.Int64);
					var updateClassParameter = updateCommand.AddParameter("@LClass", DbType.Int32);
					var updateValueParameter = updateCommand.AddParameter("@LValue", DbType.String);

					var deleteIdParameter = deleteCommand.AddParameter("@Id", DbType.Int64);

					var setDefaultParameter = setDefaultCommand.AddParameter("@Id", DbType.Int64);

					var insertIdParameter = insertCommand.AddParameter("@Id", DbType.Int64);
					var insertObjectIdParameter = insertCommand.AddParameter("@ObjectId", DbType.Int64);
					var insertKeyParameter = insertCommand.AddParameter("@Key", DbType.String);
					var insertClassParameter = insertCommand.AddParameter("@LClass", DbType.Int32);
					var insertValueParameter = insertCommand.AddParameter("@LValue", DbType.String);
					var insertUserIdParameter = insertCommand.AddParameter("@UserId", DbType.Int64);

					var removeList = new List<PpsObjectTagView>();

					foreach (var cur in tags)
					{
						if (cur.IsChanged)
						{
							if (cur.Id.HasValue)
							{
								if (cur.SetToDefault)
								{
									setDefaultParameter.Value = cur.Id.Value;
									setDefaultCommand.ExecuteNonQueryEx();

									cur.SetToDefault = false;
									transaction.AddRollbackOperation(() => cur.SetToDefault = true);
									reReadAll = true;
								}
								else if (cur.Class == PpsObjectTagClass.Deleted && cur.Id.Value < 0) // delete it
								{
									deleteIdParameter.Value = cur.Id;
									deleteCommand.ExecuteNonQueryEx();

									removeList.Add(cur);
								}
								else // mark as deleted
								{
									updateIdParameter.Value = cur.Id.Value;
									updateClassParameter.Value = PpsObjectTag.FormatClass(cur.Class);
									updateValueParameter.Value = cur.Value ?? DBNull.Value;

									updateCommand.ExecuteNonQueryEx();
								}
							}
							else if (!cur.IsRemoved)
							{
								insertIdParameter.Value = transaction.GetNextLocalId("ObjectTags", "Id");
								insertObjectIdParameter.Value = parent.Id;
								insertKeyParameter.Value = cur.Name;
								insertClassParameter.Value = PpsObjectTag.FormatClass(cur.Class);
								insertValueParameter.Value = cur.Value ?? DBNull.Value;
								insertUserIdParameter.Value = cur.UserId;

								insertCommand.ExecuteNonQueryEx();
							}
							cur.ResetDirty(transaction);
						}
					}

					transaction.AddRollbackOperation(()=>tags.AddRange(removeList));
					foreach (var c in removeList)
						tags.Remove(c);
				}

				OnCollectionReset();

				if (reReadAll)
					isLoaded = false;
			}
		} // proc UpdateLocal

		#endregion

		#region -- Tag Manipulation -------------------------------------------------------

		internal void AddToXml(XElement xObj, XName tagElementName)
		{
			lock (parent.SyncRoot)
			{
				CheckTagsLoaded();

				foreach (var cur in tags)
				{
					// only tags from this user, or system tags
					if (cur.UserId == 0 || cur.UserId == parent.Environment.UserId)
					{
						xObj.Add(
							new XElement(tagElementName,
								Procs.XAttributeCreate("id", cur.Id, new long?()),
								new XAttribute("key", cur.Name),
								new XAttribute("class", PpsObjectTag.FormatClass(cur.Class)),
								new XAttribute("value", cur.Value),
								new XAttribute("userId", cur.UserId)
							)
						);
					}
				}
			}
		} // proc AddToXml

		internal void ReadTagsFromXml(IEnumerable<XElement> tagsToRead)
		{
			// should processed throw sync?
			//lock (parent.SyncRoot)
			//{
			//	CheckTagsLoaded();

			//	var notProcessedTags = new List<PpsObjectTagView>(tags);

			//	// add the new tags
			//	foreach(var x in tagsToRead)
			//	{
			//		x.GetAttribute("id", -1L);
			//		x.GetAttribute("key", String.Empty);
			//		x.GetAttribute("class", "R");
			//		x.GetAttribute("value", String.Empty);
			//		x.GetAttribute("userId", -1L);
			//	}

			//	// remove not processed tags

			//	// reseet tags to default

			//}
		} // proc ReadTagsFromXml

		public void UpdateTags(long userId, IEnumerable<PpsObjectTag> tagList)
		{
			lock (parent.SyncRoot)
			{
				var removeTags = new List<string>(
					from t in tags
					where t.UserId == userId
					select t.Name
				);

				// update tags
				foreach (var cur in tagList)
				{
					if ((cur.Class == PpsObjectTagClass.Date
						|| cur.Class == PpsObjectTagClass.Number
						|| cur.Class == PpsObjectTagClass.Text)
						&& cur.Value != null)
					{
						UpdateTag(userId, cur.Name, cur.Class, cur.Value);
						var idx = removeTags.FindIndex(c => String.Compare(c, cur.Name, StringComparison.OrdinalIgnoreCase) == 0);
						if (idx != -1)
							removeTags.RemoveAt(idx);
					}
				}

				// remove not updated tags
				foreach (var k in removeTags)
					Remove(k);
			}
		} // proc RefreshTags

		public PpsObjectTagView UpdateTag(string key, PpsObjectTagClass cls, object value)
			=> UpdateTag(parent.Environment.UserId, key, cls, value);

		public PpsObjectTagView UpdateTag(long userId, string key, PpsObjectTagClass cls, object value)
		{
			lock (parent.SyncRoot)
			{
				CheckTagsLoaded();

				var idx = IndexOf(key, userId);
				if (idx == -1)
				{
					var newTag = new PpsObjectTagView(parent, null, key, true, null, null, cls, value, userId, true);
					tags.Add(newTag);
					OnCollectionReset();
					return newTag;
				}
				else
				{
					var t = tags[idx];
					t.Update(cls, value);
					return t;
				}
			}
		} // func UpdateTag

		public void Remove(string key)
		{
			lock (parent.SyncRoot)
			{
				CheckTagsLoaded();

				var idx = IndexOf(key, parent.Environment.UserId);
				if (idx >= 0)
					tags[idx].Remove();
			}
		} // proc Remove

		public IEnumerator<PpsObjectTagView> GetEnumerator()
		{
			lock(parent.SyncRoot)
			{
				CheckTagsLoaded();

				foreach (var c in tags)
					yield return c;
			}
		} // func GetEnumerator

		public bool Contains(string key)
			=> IndexOf(key) >= 0;

		public bool Contains(PpsObjectTag tag)
			=> IndexOf(tag.Name) >= 0;

		public bool Contains(PpsObjectTagView tag)
			=> IndexOf(tag.Name) >= 0;

		public int IndexOf(string key)
			=> IndexOf(key, parent.Environment.UserId);

		public int IndexOf(string key, long userId)
		{
			lock (parent.SyncRoot)
			{
				CheckTagsLoaded();
				return tags.FindIndex(c => String.Compare(c.Name, key, StringComparison.CurrentCultureIgnoreCase) == 0);
			}
		} // func Contains

		public int IndexOf(PpsObjectTagView tag)
			=> IndexOf(tag.Name);

		public int IndexOf(PpsObjectTag tag)
			=> IndexOf(tag.Name);

		#endregion

		#region -- IList Interface --------------------------------------------------------

		int IList.Add(object value)
		{
			switch (value)
			{
				case PpsObjectTag t:
					return IndexOf(UpdateTag(t.Name, t.Class, t.Value));
				default:
					throw new ArgumentException();
			}
		} // func IList.Add

		void IList.Insert(int index, object value) { throw new NotSupportedException(); }

		void IList.Remove(object value)
		{
			switch (value)
			{
				case string key:
					Remove(key);
					break;
				case PpsObjectTag tag:
					Remove(tag.Name);
					break;
				case PpsObjectTagView tag:
					Remove(tag.Name);
					break;
			}
		} // proc Remove

		void IList.RemoveAt(int index)
			=> tags[index].Remove();

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

		public int Count
		{
			get
			{
				lock (parent.SyncRoot)
				{
					CheckTagsLoaded();
					return tags.Count;
				}
			}
		} // prop Count

		public bool IsChanged
		{
			get
			{
				foreach (var c in tags)
				{
					if (c.IsChanged)
						return true;
				}
				return false;
			}
		} // prop IsChanged

		public PpsObjectTagView this[int index]
		{
			get
			{
				lock (parent.SyncRoot)
				{
					CheckTagsLoaded();
					return tags[index];
				}
			}
		} // prop this
	} // class PpsObjectTags

	#endregion

	#region -- class IPpsObjectData -----------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public interface IPpsObjectData : INotifyPropertyChanged
	{
		Task LoadAsync();
		Task CommitAsync();
		Task PushAsync(Stream dst);
		Task UnloadAsync();

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
		private string sha256 = String.Empty;
		private string mimeType = null;

		public PpsObjectBlobData(PpsObject obj)
		{
			this.baseObj = obj;
		} // ctor

		private void OnPropertyChanged(string propertyName)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		public async Task LoadAsync()
		{
			using (var src = await baseObj.LoadRawDataAsync())
			{
				rawData = src.ReadInArray();
				OnPropertyChanged(nameof(IsLoaded));
			}
		} // proc LoadAsync

		public async Task CommitAsync()
		{
			baseObj.Tags.UpdateTag(-1, "Sha256", PpsObjectTagClass.Text, sha256);
			await baseObj.SaveRawDataAsync(
				rawData.Length,
				mimeType ?? baseObj.MimeType ?? MimeTypes.Application.OctetStream,
				dst => dst.Write(rawData, 0, rawData.Length),
				true
			);
			await baseObj.UpdateLocalAsync();
		} // proc CommitAsync

		public async Task PushAsync(Stream dst)
		{
			if (IsLoaded)
				await LoadAsync();
			await dst.WriteAsync(rawData, 0, rawData.Length);
		} // func PushAsync

		public Task UnloadAsync()
		{
			rawData = null;
			return Task.CompletedTask;
		} // func UnloadTask

		public Task ReadFromFileAsync(string filename)
		{
			mimeType = StuffIO.MimeTypeFromFilename(filename);

			using (var hashStream = new HashStream(new FileStream(filename, FileMode.Open), HashStreamDirection.Read, false, HashAlgorithm.Create("SHA-256")))
			{
				rawData = hashStream.ReadInArray();
				sha256 = StuffIO.CleanHash(BitConverter.ToString(hashStream.CheckSum));
			}
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
			if (head != null)
			{
				if (head.Count == 0)
					head.Add();
			}

			await base.OnNewAsync(arguments);
		} // proc OnNewAsync

		public async Task LoadAsync()
		{
			using (var src = await baseObj.LoadRawDataAsync())
			{
				if (src == null)
					throw new ArgumentNullException("Data is missing.");

				using (var xml = XmlReader.Create(src, Procs.XmlReaderSettings))
				{
					var xData = XDocument.Load(xml).Root;
					await Environment.Dispatcher.InvokeAsync(
						() =>
						{
							Read(xData);
							ResetDirty();
						}
					);
				}
			}
		} // proc LoadAsync

		public async Task CommitAsync()
		{
			using (var trans = await Environment.MasterData.CreateTransactionAsync(PpsMasterDataTransactionLevel.Write))
			{
				await baseObj.SaveRawDataAsync(-1, MimeTypes.Text.DataSet,
					dst =>
					{
						var settings = Procs.XmlWriterSettings;
						settings.CloseOutput = false;
						using (var xml = XmlWriter.Create(dst, settings))
							Write(xml);
					},
					true
				);

				// update tags
				baseObj.Tags.UpdateTags(0, GetAutoTags().ToList());

				// persist the object description
				await baseObj.UpdateLocalAsync();

				trans.AddRollbackOperation(SetDirty);
				trans.Commit();
			}

			// mark not dirty anymore
			ResetDirty();
		} // proc CommitAsync

		public async Task PushAsync(Stream dst)
		{
			if (IsDirty)
				await CommitAsync();

			using (var xml = XmlWriter.Create(dst, Procs.XmlWriterSettings))
				Write(xml);
		} // proc PushAsync

		public Task UnloadAsync()
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
		private readonly object[] staticValues;             // values of the table
		private readonly object objectLock = new object();

		private IPpsObjectData data = null;                 // access to the object data
		private readonly PpsObjectTags tags;                // list with assigned tags
		private readonly PpsObjectLinks links;              // linked objects

		private bool isChanged = false;

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

		/// <summary>Reads the properties from the local database.</summary>
		/// <param name="r"></param>
		private void ReadObjectInfo(IDataReader r)
		{
			// update the values
			for (var i = 1; i < StaticColumns.Length; i++)
				SetValue((PpsStaticObjectColumnIndex)i, r.IsDBNull(i) ? null : r.GetValue(i), false);

			// check for tags
			if (r.FieldCount >= StaticColumns.Length && !r.IsDBNull(StaticColumns.Length))
				tags.RefreshTags(r.GetString(StaticColumns.Length));

			ResetDirty(null);
		} // proc ReadObjectInfo

		/// <summary>Reads the core properties from Sync or Pull.</summary>
		/// <param name="properties"></param>
		internal void ReadObjectInfo(IPropertyReadOnlyDictionary properties)
		{
			if (properties.TryGetProperty<Guid>(nameof(Guid), out var guid))
				SetValue(PpsStaticObjectColumnIndex.Guid, guid, false);
			if (properties.TryGetProperty<string>(nameof(Typ), out var typ))
				SetValue(PpsStaticObjectColumnIndex.Typ, typ, false);
			if (properties.TryGetProperty<string>(nameof(Nr), out var nr))
				SetValue(PpsStaticObjectColumnIndex.Nr, nr, false);
			if (properties.TryGetProperty<string>(nameof(MimeType), out var mimeType))
				SetValue(PpsStaticObjectColumnIndex.MimeType, mimeType, false);
			if (properties.TryGetProperty<bool>(nameof(IsRev), out var isRev))
				SetValue(PpsStaticObjectColumnIndex.IsRev, isRev, false);
			if (properties.TryGetProperty<long>("HeadRevId", out var headRevId))
				SetValue(PpsStaticObjectColumnIndex.RemoteHeadRevId, headRevId, false);
			if (properties.TryGetProperty<long>("CurRevId", out var curRevId))
				SetValue(PpsStaticObjectColumnIndex.RemoteCurRevId, curRevId, false);

			ResetDirty(null);
		} // func ReadObject

		/// <summary>Reads the object from the pull request.</summary>
		/// <param name="x"></param>
		private void ReadObjectFromXml(XElement x)
		{
			// update object data
			ReadObjectInfo(new XAttributesPropertyDictionary(x));

			// links
			links.ReadLinksFromXml(x.Elements("linksTo"));

			// tags
			tags.ReadTagsFromXml(x.Elements("tags")); // refresh of the pulled system tags, removes current system tags

			ResetDirty(null);
		} // UpdateObjectFromXml

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

		private async Task UpdateObjectIdAsync(PpsMasterDataTransaction trans, long newObjectId)
		{
			if (newObjectId < 0)
				throw new ArgumentOutOfRangeException(nameof(objectId), newObjectId, "New object Id is invalid.");
			else if (objectId > 0 && objectId != newObjectId)
				throw new ArgumentOutOfRangeException(nameof(Id), newObjectId, "Object id is different.");

			using (var cmd = trans.CreateNativeCommand("UPDATE main.[Objects] SET Id = @Id WHERE Id = @OldId"))
			{
				cmd.AddParameter("@Id", DbType.Int64, newObjectId);
				cmd.AddParameter("@OldId", DbType.Int64, objectId);
				await cmd.ExecuteNonQueryExAsync();

				objectId = newObjectId; // todo: generic transaction will be a good idea
			}
		} // proc UpdateObjectId

		private async Task<IPpsProxyTask> EnqueuePullAsync(PpsMasterDataTransaction foregroundTransaction)
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
						ReadObjectFromXml(XElement.Load(xmlHeader));

					// pull depended objects with lower request
					if (foregroundTransaction != null)
					{
						foreach (var linked in Links)
						{
							var linkTo = linked.LinkTo;
							if (!linkTo.HasData)
								linkTo.EnqueuePullAsync(null).Wait(); // not in foreground
						}
					}

					// update data block
					var trans = foregroundTransaction ?? Environment.MasterData.CreateTransactionAsync(PpsMasterDataTransactionLevel.Write).Result;
					try
					{
						SaveRawDataAsync(c.ContentLength - headerLength, MimeType,
							dst => c.Content.CopyTo(dst),
							false
						).Wait();

						SetValue(PpsStaticObjectColumnIndex.PulledRevId, pulledRevId, true);

						// persist current object state
						UpdateLocalAsync().Wait();

						if (foregroundTransaction == null)
							trans.Commit();
					}
					finally
					{
						if (foregroundTransaction == null)
							trans.Dispose();
					}
					return c.Content;
				});

				// read the object stream from server
				return request.Enqueue(PpsLoadPriority.ObjectPrimaryData, true);
			}
		} // proc PullDataAsync

		public async Task PullAsync(long revId = -1)
		{
			if (revId == -1)
				revId = RemoteHeadRevId;

			// foreground means a thread transission, we just wait for the task to finish.
			// that we do not get any deadlocks with the db-transactions, we need to set the transaction of the current thread.
			using (var r = await (await EnqueuePullAsync(Environment.MasterData.CurrentTransaction)).ForegroundAsync())
			{
				// read prev stored data
				if (data != null)
					await data.LoadAsync();
			}
		} // proc PullDataAsync

		public async Task PushAsync()
		{
			XElement xAnswer;
			using (var trans = await Environment.MasterData.CreateTransactionAsync(PpsMasterDataTransactionLevel.Write))
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
						await dst.WriteAsync(headerData, 0, headerData.Length);

						// write the content
						await data.PushAsync(dst);
					}
				}
				finally
				{
					Monitor.Exit(objectLock);
				}

				// get the result
				xAnswer = await Task.Run(() => Environment.Request.GetXml(request.GetResponse()));
				if (xAnswer.Name.LocalName == "push") // something is wrong / pull request.
				{
					throw new Exception("todo: exception for UI pull request.");
				}
				else if (xAnswer.Name.LocalName == "object")
				{
					// update id and basic meta data
					var newObjectId = xAnswer.GetAttribute<long>(nameof(Id), -1);
					await UpdateObjectIdAsync(trans, newObjectId);

					// update meta data
					ReadObjectFromXml(xAnswer);

					// repull the whole object
					await PullAsync(RemoteHeadRevId);

					// write local database
					await UpdateLocalAsync();

					trans.Commit();
				}
				else
					throw new ArgumentException("Could not parse push-answer.");
			}
		} // proc PushAsync

		public async Task<T> GetDataAsync<T>(bool asyncPullData = false)
			where T : IPpsObjectData
		{
			if (data == null)
			{
				// update data from server, if not present (pull head)
				if (objectId >= 0 && !HasData)
					await PullAsync();

				// create the core data object
				data = await environment.CreateObjectDataObjectAsync<T>(this);
			}
			return (T)data;
		} // func GetDataAsync

		internal async Task<Stream> LoadRawDataAsync()
		{
			using (var trans = await environment.MasterData.CreateTransactionAsync(PpsMasterDataTransactionLevel.ReadUncommited))
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

		internal async Task SaveRawDataAsync(long contentLength, string mimeType, Action<Stream> data, bool isDocumentChanged)
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
			else
				isDocumentChanged = false;

			// store the value
			using (var trans = await environment.MasterData.CreateTransactionAsync(PpsMasterDataTransactionLevel.Write))
			using (var cmd = trans.CreateNativeCommand("UPDATE main.[Objects] " +
				"SET " +
					"MimeType = @MimeType, " +
					"Document = @Document, " +
					"DocumentIsLinked = 0, " +
					"DocumentIsChanged = @DocumentIsChanged, " +
					"_IsUpdated = 1 " +
				"WHERE Id = @Id"))
			{
				cmd.AddParameter("@Id", DbType.Int64, objectId);
				cmd.AddParameter("@MimeType", DbType.String, mimeType);
				cmd.AddParameter("@Document", DbType.Binary, bData ?? (object)DBNull.Value);
				cmd.AddParameter("@DocumentIsChanged", DbType.Boolean, isDocumentChanged);

				await cmd.ExecuteNonQueryAsync();

				// set HasData to true
				SetValue(PpsStaticObjectColumnIndex.MimeType, mimeType, false);
				SetValue(PpsStaticObjectColumnIndex.IsDocumentChanged, isDocumentChanged, false);
				SetValue(PpsStaticObjectColumnIndex.HasData, true, false);

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

			// add links
			links.AddToXml(xObj, "linkTo");

			// add system tags
			tags.AddToXml(xObj, "tags");

			return xObj;
		} // proc ToXml

		private void UpdateLocalInternal(PpsMasterDataTransaction transaction)
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
			links.UpdateLocal(transaction);

			// tags
			tags.UpdateLocal(transaction);
			
			// reset the dirty flag
			ResetDirty(transaction);
		} // proc UpdateLocalInternal

		public async Task UpdateLocalAsync()
		{
			using (var trans = await environment.MasterData.CreateTransactionAsync(PpsMasterDataTransactionLevel.Write))
			{
				UpdateLocalInternal(trans); // blocking operation, currently!!!
				trans.Commit();
			}
		} // proc UpdateLocal

		#region -- Properties -------------------------------------------------------------

		internal void OnPropertyChanged(string propertyName)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		private T GetValue<T>(int index, T empty)
			=> index == 0 ? (T)(object)objectId : (staticValues[index] ?? empty).ChangeType<T>();

		private void SetValue(PpsStaticObjectColumnIndex index, object newValue, bool setDirty)
		{
			if (!Object.Equals(staticValues[(int)index], newValue))
			{
				staticValues[(int)index] = newValue;

				if (setDirty)
					SetDirty();
				OnPropertyChanged(staticColumns[(int)index].Name);
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
						else if (index < StaticColumns.Length + obj.Tags.Count + staticPropertyCount)
						{
							var tag = obj.Tags[index - StaticColumns.Length - staticPropertyCount];
							return CreateSimpleDataColumn(tag);
						}
						else
							throw new ArgumentOutOfRangeException();
					}
				}
			} // prop this

			private static SimpleDataColumn CreateSimpleDataColumn(PpsObjectTagView tag)
				=> new SimpleDataColumn(tag.Name, PpsObjectTag.GetTypeFromClass(tag.Class));

			public int Count => StaticColumns.Length + obj.Tags.Count + staticPropertyCount;
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
							data = GetDataAsync<IPpsObjectData>(true).Result;
						return data;
					}
					else if (index == StaticColumns.Length + 1)
						return tags;
					else if (index == StaticColumns.Length + 2)
						return links;
					else if (index < StaticColumns.Length + Tags.Count + staticPropertyCount)
						return tags[index - StaticColumns.Length - staticPropertyCount].Value;
					else
						throw new ArgumentOutOfRangeException();
				}
			}
		} // prop this

		#endregion

		internal void SetDirty()
		{
			if (!isChanged)
			{
				isChanged = true;
				OnPropertyChanged(nameof(IsChanged));
			}
		} // proc SetDirty

		private void ResetDirty(PpsMasterDataTransaction transaction)
		{
			if (isChanged)
			{
				transaction?.AddRollbackOperation(SetDirty);
				isChanged = false;
				OnPropertyChanged(nameof(IsChanged));
			}
		} // proc SetDirty

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
		/// <summary>Is the local data of the object changed.</summary>
		public bool IsDocumentChanged => GetValue((int)PpsStaticObjectColumnIndex.IsDocumentChanged, false);

		/// <summary>Has this object local data available.</summary>
		public bool HasData => GetValue((int)PpsStaticObjectColumnIndex.HasData, false);

		/// <summary>Access to the links of the object.</summary>
		public PpsObjectLinks Links => links;
		/// <summary>Object tags and properties</summary>
		public PpsObjectTags Tags => tags;

		/// <summary>Is the meta data changed and not persisted in the local database.</summary>
		public bool IsChanged => isChanged;

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

		internal const int staticPropertyCount = 3;

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
		public async Task<string> GetNextNumberAsync()
		{
			using (var trans = await Environment.MasterData.CreateTransactionAsync(PpsMasterDataTransactionLevel.ReadCommited))
			using (var cmd = trans.CreateNativeCommand("SELECT max(Nr) FROM main.[Objects] WHERE substr(Nr, 1, 3) = '*n*' AND abs(substr(Nr, 4)) != 0.0")) //SELECT max(Nr) FROM main.[Objects] WHERE substr(Nr, 1, 3) = '*n*' AND typeof(substr(Nr, 4)) = 'integer'
			{
				var lastNrString = await cmd.ExecuteScalarExAsync() as string;
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
		public static string AttachmentObjectTyp = "attachments";

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
		public async Task<PpsObject> CreateNewObjectAsync(PpsObjectInfo objectInfo, string mimeType = MimeTypes.Application.OctetStream)
			=> await CreateNewObjectAsync(Guid.NewGuid(), objectInfo.Name, await objectInfo.GetNextNumberAsync(), objectInfo.IsRev, mimeType);

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
		public async Task<PpsObject> CreateNewObjectAsync(Guid guid, string typ, string nr, bool isRev, string mimeType = MimeTypes.Application.OctetStream)
		{
			using (var trans = await MasterData.CreateTransactionAsync(PpsMasterDataTransactionLevel.Write))
			using (var cmd = trans.CreateNativeCommand(
				"INSERT INTO main.Objects (Id, Guid, Typ, MimeType, Nr, IsHidden, IsRev, _IsUpdated) " +
				"VALUES (@Id, @Guid, @Typ, @MimeType, @Nr, 0, @IsRev, 1)"))
			{
				var newObjectId = trans.GetNextLocalId("Objects", "Id");
				cmd.AddParameter("@Id", DbType.Int64, newObjectId);
				cmd.AddParameter("@Guid", DbType.Guid, guid);
				cmd.AddParameter("@Typ", DbType.String, typ.DbNullIfString());
				cmd.AddParameter("@MimeType", DbType.String, mimeType.DbNullIfString());
				cmd.AddParameter("@Nr", DbType.String, nr.DbNullIfString());
				cmd.AddParameter("@IsRev", DbType.Boolean, isRev);

				await cmd.ExecuteNonQueryExAsync();
				trans.Commit();

				return GetObject(newObjectId);
			}
		} // func CreateNewObject

		private void RefreshCachedObject(long id, IPropertyReadOnlyDictionary properties)
		{
			lock (objectStoreLock)
			{
				var o = GetCachedObject(objectStoreById, id);
				if (o != null)
					o.ReadObjectInfo(properties);
			}
		} // proc RefreshCachedObject

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
			var isRevDefault = x.GetAttribute("isRev", false);

			if (String.IsNullOrEmpty(objectTyp))
				return;

			// update dataset definitions
			if (!String.IsNullOrEmpty(sourceUri))
				ActiveDataSets.RegisterDataSetSchema(objectTyp, sourceUri, typeof(PpsDataSetDefinitionDesktop));

			var oi = new PpsObjectInfo(this, objectTyp) { IsRev = isRevDefault };
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

		private PpsObject ReadObject(object key, bool useGuid, bool throwException = false)
		{
			// refresh core data
			using (var trans = MasterData.CreateReadUncommitedTransaction())
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
			lock (objectStoreLock)
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
			}
		} // func UpdateCacheItem

		private PpsObject GetCachedObject<T>(Dictionary<T, int> index, T key)
		{
			lock (objectStoreLock)
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
			}
			return null;
		} // func GetCachedObject

		private PpsObject GetCachedObjectOrRead<T>(Dictionary<T, int> index, T key, bool keyIsGuid, bool throwException = false)
		{
			lock (objectStoreLock)
			{
				// check if the object is in memory
				return GetCachedObject(index, key)
					// object is not in memory, create a instance
					?? ReadObject(key, keyIsGuid, throwException);
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
		public PpsObject GetObject(long localId, bool throwException = false)
			=> GetCachedObjectOrRead(objectStoreById, localId, useId, throwException);

		[LuaMember]
		public PpsObject GetObject(Guid guid, bool throwException = false)
			=> GetCachedObjectOrRead(objectStoreByGuid, guid, useId, throwException);

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
