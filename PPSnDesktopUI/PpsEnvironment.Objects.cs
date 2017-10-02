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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Data;
using TecWare.DE.Networking;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;
using TecWare.PPSn.Stuff;

namespace TecWare.PPSn
{
	#region -- enum PpsObjectServerIndex ----------------------------------------------

	/// <summary>Server site columns.</summary>
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

	#region -- class PpsObjectLink ----------------------------------------------------

	/// <summary>Represents a link</summary>
	public sealed class PpsObjectLink
	{
		private readonly PpsObjectLinks parent;

		private long? id;							// local id of the link, always positiv equal ROWID(), null if the link is not inserted yet
		private long linkToId;						// id of the linked object

		private int refCount = 0;					// how often is this link used within the object

		private WeakReference<PpsObject> linkTo;	// weak ref to the actual object
		
		private bool isDirty;   // is the link changed

		#region -- Ctor/Dtor/AddRef/DecRef/Dirty --------------------------------------

		internal PpsObjectLink(PpsObjectLinks parent, long? id, long linkToId, int refCount)
		{
			this.parent = parent ?? throw new ArgumentNullException(nameof(parent));

			this.id = id;
			this.linkToId = linkToId;
			this.refCount = refCount;
			this.isDirty = !id.HasValue;

			if (id.HasValue)
				parent.Parent.Environment.MasterData.RegisterWeakDataRowChanged("ObjectLinks", id, OnLinkChanged);
		} // ctor

		private void OnLinkChanged(object sender, PpsDataRowChangedEventArgs e)
		{
			if (e.Operation == PpsDataChangeOperation.Full
				|| e.Operation == PpsDataChangeOperation.Update)
			{
				linkToId = e.Arguments.GetProperty("LinkObjectId", linkToId); // we only observe linktoid
			}
		} // proc OnLinkChanged

		/// <summary>Changes the reference counter of this link.</summary>
		internal void AddRef()
		{
			refCount++;
			SetDirty();
		} // proc AddRef

		/// <summary>Changes the reference counter of this link.</summary>
		internal void DecRef()
		{
			if (refCount > 0)
				refCount--;
			SetDirty();
		} // proc DecRef

		/// <summary>Marks this link as dirty.</summary>
		internal void SetDirty()
		{
			isDirty = true;
			parent.SetDirty();
		} // proc SetDirty

		/// <summary>Reset the dirty flag.</summary>
		internal void ResetDirty()
		{
			isDirty = false;
		} // proc ResetDirty

		#endregion

		/// <summary>Creates a weak reference to this object.</summary>
		/// <returns></returns>
		private PpsObject GetLinkedObject()
		{
			if (linkTo != null && linkTo.TryGetTarget(out var r))
				return r;

			r = parent.Parent.Environment.GetObject(linkToId);
			linkTo = new WeakReference<PpsObject>(r);
			return r;
		} // func GetLinkedObject

		internal void SetNewLinkToId(long newObjectId)
		{
			this.linkToId = newObjectId;
		} // proc SetNewLinkToId

		/// <summary>DecRef or removes this link from the list.</summary>
		public void Remove()
			=> parent.RemoveLink(this);

		/// <summary>Object</summary>
		public PpsObject Parent => parent.Parent;

		/// <summary>Local database id.</summary>
		internal long? Id
		{
			get => id;
			set
			{
				if (!id.HasValue && value.HasValue)
					parent.Parent.Environment.MasterData.RegisterWeakDataRowChanged("ObjectLinks", id, OnLinkChanged);
				id = value;
			}
		} // prop Id

		/// <summary>Id of the linked object.</summary>
		public long LinkToId
		{
			get => linkToId;
			internal set
			{
				if (linkToId != value)
				{
					linkToId = value;
					linkTo = null;
				}
			}
		} // prop LinkToId

		/// <summary>Object reference to the object.</summary>
		public PpsObject LinkTo => GetLinkedObject();

		/// <summary>Current ref counter</summary>
		public int RefCount
		{
			get => refCount;
			internal set
			{
				if (refCount != value)
					refCount = value;
			}
		} // prop RefCount

		/// <summary>Is the link data different to the local database.</summary>
		public bool IsDirty => isDirty;
	} // class PpsObjectLink

	#endregion

	#region -- class PpsObjectLinks ---------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsObjectLinks : IList, IReadOnlyList<PpsObjectLink>, INotifyCollectionChanged
	{
		/// <summary>Notify for link list changes.</summary>
		public event NotifyCollectionChangedEventHandler CollectionChanged;

		private readonly PpsObject parent;

		private bool isLoaded = false;	// marks if the link list is loaded from the local store
		private bool isDirty = false;	// the link list needs to persist in the local database
		private readonly List<PpsObjectLink> links = new List<PpsObjectLink>(); // local active links
		private readonly List<PpsObjectLink> removedLinks = new List<PpsObjectLink>();

		#region -- Ctor/Dtor/Dirty/Refresh --------------------------------------------

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
			isDirty = true;
			parent.SetDirty();
		} // proc SetDirty
		
		private void RefreshLinks()
		{
			using (var cmd = parent.Environment.MasterData.CreateNativeCommand("SELECT [Id], [LinkObjectId], [RefCount] FROM main.[ObjectLinks] WHERE [ParentObjectId] = @ObjectId"))
			{
				cmd.AddParameter("@ObjectId", DbType.Int64, parent.Id);

				using (var r = cmd.ExecuteReaderEx(CommandBehavior.SingleResult))
				{
					while (r.Read())
					{
						var rowId = r.GetInt64(0);
						var findId = new Predicate<PpsObjectLink>(l => l.Id == rowId);

						var link = links.Find(findId);
						if (link == null)
						{
							link = removedLinks.Find(findId);
							if (link != null)
								removedLinks.Remove(link);
						}

						if (link == null)
						{
							links.Add(new PpsObjectLink(
								this,
								r.GetInt64(0),
								r.GetInt64(1),
								r.GetInt32(2)
							));
						}
						else
						{
							link.LinkToId = r.GetInt64(1);
							link.RefCount = r.GetInt32(2);
							link.ResetDirty();
						}
					}
				}
			}

			removedLinks.Clear();
			isDirty = false;
		} // proc RefreshLinks

		#endregion

		#region -- UpdateLocal --------------------------------------------------------

		internal void UpdateLocal(PpsMasterDataTransaction transaction)
		{
			lock (parent.SyncRoot)
			{
				if (!isLoaded || !isDirty)
					return;

				using (var insertCommand = transaction.CreateNativeCommand("INSERT INTO main.[ObjectLinks] (ParentObjectId, LinkObjectId, RefCount) VALUES (@ParentObjectId, @LinkObjectId, @RefCount)"))
				using (var updateCommand = transaction.CreateNativeCommand("UPDATE main.[ObjectLinks] SET LinkObjectId = @LinkObjectId, RefCount = @RefCount WHERE Id = @Id"))
				{
					var insertParentIdParameter = insertCommand.AddParameter("@ParentObjectId", DbType.Int64);
					var insertLinkIdParameter = insertCommand.AddParameter("@LinkObjectId", DbType.Int64);
					var insertRefCountParameter = insertCommand.AddParameter("@RefCount", DbType.Int32);

					var updateIdParameter = updateCommand.AddParameter("@Id", DbType.Int64);
					var updateLinkIdParameter = updateCommand.AddParameter("@LinkObjectId", DbType.Int64);
					var updateRefCountParameter = updateCommand.AddParameter("@RefCount", DbType.Int64);

					foreach (var cur in links)
					{
						if (cur.IsDirty)
						{
							if (cur.Id.HasValue)
							{
								updateIdParameter.Value = cur.Id;
								updateLinkIdParameter.Value = cur.LinkToId;
								updateRefCountParameter.Value = cur.RefCount;

								updateCommand.ExecuteNonQueryEx();
							}
							else
							{
								insertParentIdParameter.Value = parent.Id;
								insertLinkIdParameter.Value = cur.LinkToId;
								insertRefCountParameter.Value = cur.RefCount;

								insertCommand.ExecuteNonQueryEx();

								cur.Id = transaction.LastInsertRowId;
								transaction.AddRollbackOperation(() => { cur.Id = null; cur.SetDirty(); });
							}

							cur.ResetDirty();
							transaction.AddRollbackOperation(cur.SetDirty);
						}
					}
				} // using

				if (removedLinks.Count > 0)
				{
					var removedLinksArray = removedLinks.ToArray();

					using (var deleteCommand = transaction.CreateNativeCommand("DELETE FROM main.[ObjectLinks] WHERE Id = @Id"))
					{
						var deleteIdParameter = deleteCommand.AddParameter("@Id", DbType.Int64);
						foreach (var cur in removedLinksArray)
						{
							if (cur.Id.HasValue)
							{
								deleteIdParameter.Value = cur.Id;
								deleteCommand.ExecuteNonQueryEx();
							}
							removedLinks.Remove(cur);
						}
					}

					transaction.AddRollbackOperation(() => removedLinks.AddRange(removedLinksArray));
				}

				isDirty = false;
				transaction.AddRollbackOperation(() => isDirty = true);
			}
		} // proc UpdateLocal

		#endregion

		#region -- ReadLinksFromXml ---------------------------------------------------

		internal void ReadLinksFromXml(IEnumerable<XElement> xLinks)
		{
			lock (parent.SyncRoot)
			{
				CheckLinksLoaded();

				var notProcessedLinks = new List<PpsObjectLink>(links);

				// add new links
				foreach (var x in xLinks)
				{
					var objectId = x.GetAttribute("linkId", -1L);
					var refCount = x.GetAttribute("refCount", 0);

					var linkExists = links.Find(c => c.LinkToId == objectId);
					if (linkExists == null)
						links.Add(new PpsObjectLink(this, null, objectId, refCount));
					else
					{
						linkExists.RefCount = refCount;
						notProcessedLinks.Remove(linkExists);
					}
				}

				// remove untouched links
				foreach (var cur in notProcessedLinks)
				{
					links.Remove(cur);
					if (cur.Id.HasValue)
						removedLinks.Add(cur);
				}

				isDirty = true;
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
							new XAttribute("linkId", c.LinkToId),
							new XAttribute("refCount", c.RefCount)
						)
					);
				}
			}
		} // func AddToXml

		#endregion

		#region -- AppendLink/RemoveLink ----------------------------------------------

		public void AppendLink(long linkToId, bool force = false)
			=> AppendLink(Parent.Environment.GetObject(linkToId), force);

		public void AppendLink(PpsObject linkTo, bool force = false)
		{
			lock (parent.SyncRoot)
			{
				CheckLinksLoaded();

				// add link
				var currentLink = links.Find(c => c.LinkToId == linkTo.Id);
				if (currentLink != null)
				{
					currentLink.AddRef();
				}
				else
				{
					var newLink = new PpsObjectLink(this, null, linkTo.Id, 1);
					var newLinkIndex = links.Count;
					links.Add(newLink);
					CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, newLink, newLinkIndex));

					SetDirty();
				}
			}

			OnCollectionReset();
		} // proc AppendLink

		public void RemoveLink(long objectId, bool force = false)
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
					CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, link, idx));
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

		#endregion

		#region -- List members -------------------------------------------------------

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

		public PpsObjectLink FindById(long objectId)
		{
			lock (parent.SyncRoot)
			{
				CheckLinksLoaded();
				foreach (var l in links)
				{
					if (l.LinkToId == objectId)
						return l;
				}
				return null;
			}
		} // func FindById

		public PpsObjectLink FindByGuid(Guid objectGuid)
		{
			lock (parent.SyncRoot)
			{
				CheckLinksLoaded();
				foreach (var l in links)
				{
					if (l.LinkTo.Guid == objectGuid) // time intensive operation
						return l;
				}
				return null;
			}
		} // func FindByGuid

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

		public IEnumerator<PpsObjectLink> GetEnumerator()
		{
			lock (parent.SyncRoot)
			{
				CheckLinksLoaded();
				foreach (var c in links)
					yield return c;
			}
		} // func GetEnumerator

		bool IList.IsReadOnly => true;
		bool IList.IsFixedSize => false;
		object ICollection.SyncRoot => parent.SyncRoot;
		bool ICollection.IsSynchronized => true;
		object IList.this[int index] { get => this[index]; set => throw new NotSupportedException(); }

		#endregion

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
	} // class PpsObjectLinks

	#endregion

	#region -- enum PpsObjectTagLoadState ---------------------------------------------

	public enum PpsObjectTagLoadState
	{
		/// <summary>Nothing loaded.</summary>
		None = 0,
		/// <summary>Tag infos were loaded throw a view (only key, value is set)</summary>
		FastLoad = 1,
		/// <summary>Tag is loaded from the database table.</summary>
		LocalState = 2,
	} // enum PpsObjectTagLoadState

	#endregion

	#region -- class PpsObjectTagView -------------------------------------------------

	/// <summary>Tag implementation</summary>
	public sealed class PpsObjectTagView : INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler PropertyChanged;

		private readonly PpsObjectTags parent;

		private long? id;               // new tags have null
		private readonly bool isRev;	// is this tag attached to the revision
		private readonly string key;    // key for the tag

		private PpsObjectTagLoadState state = PpsObjectTagLoadState.None;

		private PpsObjectTagClass tagClass;
		private object value;
		private long userId;
		private PpsMasterDataRow userRow;
		private DateTime? creationStamp;

		private bool isLocalChanged;	// is the tag changed in the local state
		private bool isDirty;   // is the tag change in memory, an need to persist

		private readonly object lockUserRow = new object();

		#region -- Ctor/Dtor ----------------------------------------------------------

		internal PpsObjectTagView(PpsObjectTags parent, long? id, string key, bool isRev, PpsObjectTagClass tagClass, object value, long userId, DateTime creationStamp, bool isLocalChanged)
		{
			this.parent = parent ?? throw new ArgumentNullException(nameof(parent));

			this.id = id;
			this.key = key;
			this.isRev = isRev;

			RefreshData(tagClass, value, userId, creationStamp, isLocalChanged);

			this.isDirty = !id.HasValue;
		} // ctor

		internal PpsObjectTagView(PpsObjectTags parent, long id, string key, PpsObjectTagClass tagClass, object value, long userId)
		{
			this.parent = parent ?? throw new ArgumentNullException(nameof(parent));

			this.id = id;
			this.key = key;
			this.isRev = true;

			this.state = PpsObjectTagLoadState.FastLoad;
			this.tagClass = tagClass;
			this.value = value;
			this.userId = userId;
			this.userRow = null;
			this.creationStamp = null;

			this.isLocalChanged = false;
			this.isDirty = false;
		} // ctor

		private bool SetValue<T>(ref T variable, T value, string propertyName)
		{
			if (!Object.Equals(variable, value))
			{
				variable = value;
				OnPropertyChanged(propertyName);
				return true;
			}
			else
				return false;
		} // proc SetValue

		private void SetDirty()
		{
			SetValue(ref isDirty, true, nameof(IsDirty));
			parent.SetDirty();
		} // proc SetDirty

		internal void ResetDirty(PpsMasterDataTransaction transaction)
		{
			if (isDirty)
			{
				transaction?.AddRollbackOperation(SetDirty);
				isDirty = false;
				OnPropertyChanged(nameof(IsDirty));
			}
		} // proc ResetDirty

		private void CheckTagState()
		{
			if (state != PpsObjectTagLoadState.LocalState)
				parent.CheckTagsState();
		} // proc CheckTagState

		private void OnPropertyChanged(string propertyName)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		#endregion

		#region -- Refresh ------------------------------------------------------------

		internal void RefreshData(PpsObjectTagClass tagClass, object value, long userId, DateTime creationStamp, bool isLocalChanged)
		{
			this.state = PpsObjectTagLoadState.LocalState;

			SetValue(ref this.tagClass, tagClass, nameof(Class));
			SetValue(ref this.value, value, nameof(Value));
			SetValue(ref this.userId, userId, nameof(User));
			SetValue(ref this.creationStamp, creationStamp, nameof(CreationStamp));

			this.isLocalChanged = isLocalChanged;
			this.isDirty = false;
		} // proc RefreshData
		
		#endregion
		
		private PpsMasterDataRow GetUserRow()
		{
			lock (lockUserRow)
			{
				if (UserId <= 0)
					return null;
				else if (userRow != null && Object.Equals(userRow.Key, userId))
					return userRow;
				else
					return userRow = parent.Parent.Environment.MasterData.GetTable("User")?.GetRowById(UserId, false);
			}
		} // ctor

		private void CheckTagValue(PpsObjectTagClass newClass, object newValue)
		{
			if(newClass == PpsObjectTagClass.Tag)
			{
				if (newValue != null)
					throw new ArgumentOutOfRangeException("Tags of the class Tag can not have an value.");
			}else if(newClass == PpsObjectTagClass.Note)
			{
				if (newValue == null)
					throw new ArgumentOutOfRangeException("Tags of the class Note must have an value.");
			}
		} // proc CheckTagValue
		
		internal void RefreshTag(PpsObjectTag tagData)
		{
			SetValue(ref tagClass, tagData.Class, nameof(Class));
			SetValue(ref value, tagData.Value, nameof(Value));

			if (userId != tagData.UserId)
			{
				userId = tagData.UserId;
				OnPropertyChanged(nameof(UserId));
				OnPropertyChanged(nameof(User));
			}
		} // proc RefreshTag

		/// <summary>Updates the content of the tag.</summary>
		/// <param name="newClass"></param>
		/// <param name="newValue"></param>
		public void Update(PpsObjectTagClass newClass, object newValue)
		{
			CheckTagState();

			// correct strings
			if (newValue is string t && String.IsNullOrEmpty(t))
				newValue = null;

			// update values
			if (Class != newClass)
			{
				if (newClass == PpsObjectTagClass.Deleted)
				{
					if (SetValue(ref value, newValue, nameof(Value)))
						parent.Parent.OnPropertyChanged(key);
					if (SetValue(ref tagClass, PpsObjectTagClass.Deleted, nameof(Class)))
						OnPropertyChanged(nameof(IsRemoved));
					SetValue(ref isLocalChanged, true, nameof(IsLocalChanged));
					parent.OnTagRemoved(this);
					SetDirty();
				}
				else if (Class == PpsObjectTagClass.Tag
				  || Class == PpsObjectTagClass.Note)
					throw new InvalidOperationException("Tag of the Tag or Note, can not change the class.");
				else
				{
					CheckTagValue(tagClass, newValue);
					if (SetValue(ref value, newValue, nameof(Value)))
						parent.Parent.OnPropertyChanged(key);

					SetValue(ref tagClass, newClass, nameof(Class));
					SetValue(ref isLocalChanged, true, nameof(IsLocalChanged));
					OnPropertyChanged(nameof(IsRemoved));
					SetDirty();
				}
			}
			else if (!Object.Equals(value, newValue))
			{
				CheckTagValue(tagClass, newValue);
				value = newValue;
				OnPropertyChanged(nameof(Value));
				parent.Parent.OnPropertyChanged(key);

				SetValue(ref isLocalChanged, true, nameof(IsLocalChanged));
				SetDirty();
			}
		} // proc UpdateTag

		/// <summary>Remove the tag.</summary>
		public void Remove()
			=> Update(PpsObjectTagClass.Deleted, null);

		/// <summary>Internal Id.</summary>
		internal long? Id { get => id; set => id = value; }
		/// <summary>Name of the tag.</summary>
		public string Name => key;

		/// <summary>Returns the current class of the tag.</summary>
		public PpsObjectTagClass Class => tagClass;

		/// <summary>Returns the current value, for the item.</summary>
		public object Value => value;

		/// <summary>Returns the Creation DateTime.</summary>
		public DateTime CreationStamp
		{
			get
			{
				CheckTagState();
				return creationStamp.Value;
			}
		} // prop CreationStamp

		/// <summary>User that created this tag, or 0 for system created (like autotagging, states)</summary>
		public long UserId
		{
			get
			{
				CheckTagState();
				return userId;
			}
		} // prop UserId

		/// <summary>Reference to the master data.</summary>
		public PpsMasterDataRow User => GetUserRow();
		/// <summary>Is this tag removed.</summary>
		public bool IsRemoved => Class == PpsObjectTagClass.Deleted;

		public bool IsRev => isRev;

		/// <summary>Is this tag changed.</summary>
		public bool IsDirty => isDirty;
		/// <summary></summary>
		public bool IsLocalChanged => isLocalChanged;
	} // class PpsObjectTagView

	#endregion

	#region -- class PpsObjectTags ----------------------------------------------------

	/// <summary>List for lazy load support of tags.</summary>
	public sealed class PpsObjectTags : IList, IReadOnlyList<PpsObjectTagView>, IPropertyReadOnlyDictionary, INotifyCollectionChanged
	{
		public event NotifyCollectionChangedEventHandler CollectionChanged;

		private readonly PpsObject parent;
		private readonly List<PpsObjectTagView> tags;
		private readonly List<PpsObjectTagView> deletedTags;

		private PpsObjectTagLoadState state = PpsObjectTagLoadState.None;
		private bool isDirty = false;

		#region -- Ctor/Dtor ----------------------------------------------------------

		internal PpsObjectTags(PpsObject parent)
		{
			this.parent = parent ?? throw new ArgumentNullException(nameof(parent));

			this.tags = new List<PpsObjectTagView>();
			this.deletedTags = new List<PpsObjectTagView>();

			parent.Environment.MasterData.RegisterWeakDataRowChanged("ObjectTags", null, OnTagsChanged);
		} // ctor

		internal void SetDirty()
		{
			isDirty = true;
			parent.SetDirty();
		} // proc SetDirty

		internal void ResetDirty(PpsMasterDataTransaction transaction)
		{
			if (isDirty)
			{
				isDirty = false;
				transaction.AddRollbackOperation(SetDirty);
			}
		} // proc ResetDirty
			
		#endregion

		#region -- Refresh ----------------------------------------------------------------

		private void OnCollectionChanged(NotifyCollectionChangedEventArgs e = null)
			=> CollectionChanged?.Invoke(this, e ?? new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

		internal void CheckTagsState()
		{
			if (state != PpsObjectTagLoadState.LocalState)
				RefreshTags();
		} // proc CheckTagsState

		private void OnTagsChanged(object sender, PpsDataRowChangedEventArgs e)
		{
		} // proc OnTagsChanged

		internal void OnTagRemoved(PpsObjectTagView tag)
		{
			EnsureTagInList(tag);
		} // proc OnTagRemoved

		private void RefreshSingleRevTag(long databaseId, PpsObjectTag tagData)
		{
			var tag = FindTagById(databaseId);
			if (tag != null && !tag.IsDirty && tag.IsRev)
			{
				if (tagData.Class != PpsObjectTagClass.Deleted)
					tag.RefreshTag(tagData);
			}
			else
				tag = new PpsObjectTagView(this, databaseId, tagData.Name, tagData.Class, tagData.Value, tagData.UserId);
			
			EnsureTagInList(tag);
		} // proc RefreshSingleRevTag

		/// <summary>Reads revision attached tags from a text-blob. (new line separated, id:key:class:userId=value)</summary>
		/// <param name="tagList"></param>
		internal void RefreshTagsFromString(string tagList)
		{	
			lock (SyncRoot)
			{
				foreach (var c in tagList.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
				{
					var p = c.IndexOf(':');
					if (p <= 0)
						continue;

					var idText = c.Substring(0, p);
					var databaseId = Int64.Parse(idText.Substring(1));
					var tagData = PpsObjectTag.ParseTag(c.Substring(p + 1));

					RefreshSingleRevTag(databaseId, tagData);
				}

				if (state == PpsObjectTagLoadState.None)
					state = PpsObjectTagLoadState.FastLoad;
			}
			
			OnCollectionChanged(null);
		} // proc RefreshTagsFromString

		/// <summary>Writes revision attached tags</summary>
		/// <param name="x"></param>
		internal void WriteTagsToXml(XElement x, XName tagElementName)
		{
			lock (SyncRoot)
			{
				if (state == PpsObjectTagLoadState.None)
					CheckTagsState();

				foreach (var cur in tags)
				{
					// only tags from this user, or system tags
					if (cur.IsRev)
					{
						x.Add(
							new XElement(tagElementName,
								new XAttribute("key", cur.Name),
								new XAttribute("tagClass", PpsObjectTag.FormatClass(cur.Class)),
								new XAttribute("value", cur.Value),
								new XAttribute("userId", cur.UserId),
								new XAttribute("createDate", cur.CreationStamp.ToUniversalTime().ChangeType<string>())
							)
						);
					}
				}
			}
		} // proc WriteTagsToXml

		/// <summary>Merge revision based tags.</summary>
		internal void ReadTagsFromXml(IEnumerable<XElement> xTags)
		{
			lock (SyncRoot)
			{
				CheckTagsState();

				var revTagsDone = new List<PpsObjectTagView>();

				foreach (var xCur in xTags)
				{
					var key = xCur.GetAttribute("key", null);
					var tagClass = xCur.GetAttribute("tagClass", PpsObjectTagClass.Text);
					var value = Procs.ChangeType(xCur.GetAttribute("value", null), PpsObjectTag.GetTypeFromClass(tagClass));
					var userId = xCur.GetAttribute("userId", 0L);

					var tag = tags.Find(t => t.IsRev && String.Compare(t.Name, key, StringComparison.OrdinalIgnoreCase) == 0);

					if (tag != null)
						tag.Update(tagClass, value);
					else
					{
						tag = new PpsObjectTagView(this, null, key, true, tagClass, value, userId, DateTime.UtcNow, false);
					}

					EnsureTagInList(tag);

					revTagsDone.Add(tag);
				}

				for (var i = tags.Count - 1; i >= 0; i--)
				{
					if (tags[i].IsRev && revTagsDone.IndexOf(tags[i]) == -1)
					{
						deletedTags.Add(tags[i]);
						tags.RemoveAt(i);
						SetDirty();
					}
				}
			}

			OnCollectionChanged(null);
		} // proc RefreshTagsFromXml

		public void RefreshTags()
		{
			lock (parent.SyncRoot)
			{
				using (var trans = parent.Environment.MasterData.CreateReadUncommitedTransaction())
				{
					// refresh first all user generated tags
					using (var selectCommand = trans.CreateNativeCommand("SELECT [Id], [Key], [Class], [Value], [LocalClass], [LocalValue], [UserId], [CreateDate] FROM main.[ObjectTags] WHERE [ObjectId] = @Id"))
					{
						selectCommand.AddParameter("@Id", DbType.Int64, parent.Id);

						using (var r = selectCommand.ExecuteReaderEx(CommandBehavior.SingleResult))
						{
							while (r.Read())
							{
								var id = r.GetInt64(0);
								var key = r.GetString(1);

								var isRemoteClassNull = r.IsDBNull(2);
								var isLocalClassNull = r.IsDBNull(4);

								var tagClass = (PpsObjectTagClass)(isLocalClassNull ? r.GetInt32(2) : r.GetInt32(4));
								var isLocalChanged = !r.IsDBNull(5);
								var value = Procs.ChangeType(r.IsDBNull(5) ? (r.IsDBNull(3) ? null : r.GetString(3)) : r.GetString(5), PpsObjectTag.GetTypeFromClass(tagClass));
								var userId = r.IsDBNull(6) ? 0 : r.GetInt64(6);
								var creationDate = r.IsDBNull(7) ? DateTime.Now : r.GetDateTime(7);

								var isRev = !isLocalClassNull && userId == 0;

								var tag = FindTagById(id);
								if (tag != null && (tag.IsRev != isRev || tag.Name != key))
								{
									deletedTags.Remove(tag);
									tags.Remove(tag);
									tag = null;
								}

								// update tag
								if (tag != null)
								{
									tag.RefreshData(tagClass, value, userId, creationDate, isLocalChanged);
								}
								else
								{
									tag = new PpsObjectTagView(this, id, key, isRev, tagClass, value, userId, creationDate, isLocalChanged);
								}

								// add/remove tag
								EnsureTagInList(tag);
							}
						}
					}
				}

				state = PpsObjectTagLoadState.LocalState;
			}

			OnCollectionChanged(null);
		} // proc RefreshTags

		private void EnsureTagInList(PpsObjectTagView tag)
		{
			if (tag.Class == PpsObjectTagClass.Deleted)
			{
				if (tags.Remove(tag))
					SetDirty();
				if (deletedTags.IndexOf(tag) == -1)
				{
					if (tag.Id.HasValue)
						deletedTags.Add(tag);
					SetDirty();
				}
			}
			else
			{
				if (tags.IndexOf(tag) == -1)
				{
					tags.Add(tag);
					SetDirty();
				}
				if (deletedTags.Remove(tag))
					SetDirty();
			}
		} // proc EnsureTagInList

		#endregion

		#region -- UpdateLocal ------------------------------------------------------------

		internal void UpdateLocal(PpsMasterDataTransaction transaction)
		{
			lock (parent.SyncRoot)
			{
				if (state != PpsObjectTagLoadState.LocalState
					 || !isDirty)
					return;

				using (var updateCommand = transaction.CreateNativeCommand("UPDATE main.[ObjectTags] SET LocalClass = @LClass, LocalValue = @LValue, UserId = @UserId, CreateDate = @CreationDate, _IsUpdated = 1 WHERE Id = @Id"))
				using (var insertCommand = transaction.CreateNativeCommand("INSERT INTO main.[ObjectTags] (Id, ObjectId, Key, LocalClass, LocalValue, UserId, CreateDate, _IsUpdated) VALUES (@Id, @ObjectId, @Key, @LClass, @LValue, @UserId, @CreationDate, 1)"))
				using (var deleteCommand = transaction.CreateNativeCommand("DELETE FROM main.[ObjectTags] WHERE Id = @Id"))
				{
					var updateIdParameter = updateCommand.AddParameter("@Id", DbType.Int64);
					var updateClassParameter = updateCommand.AddParameter("@LClass", DbType.Int32);
					var updateValueParameter = updateCommand.AddParameter("@LValue", DbType.String);
					var updateUserIdParameter = updateCommand.AddParameter("@UserId", DbType.Int64);
					var updateCreationDateParameter = updateCommand.AddParameter("@CreationDate", DbType.DateTime);

					var insertIdParameter = insertCommand.AddParameter("@Id", DbType.Int64);
					var insertObjectIdParameter = insertCommand.AddParameter("@ObjectId", DbType.Int64);
					var insertKeyParameter = insertCommand.AddParameter("@Key", DbType.String);
					var insertClassParameter = insertCommand.AddParameter("@LClass", DbType.Int32);
					var insertValueParameter = insertCommand.AddParameter("@LValue", DbType.String);
					var insertUserIdParameter = insertCommand.AddParameter("@UserId", DbType.Int64);
					var insertCreationDatedParameter = insertCommand.AddParameter("@CreationDate", DbType.DateTime);

					var deleteIdParameter = deleteCommand.AddParameter("@Id", DbType.Int64);

					var nextLocalId = (long?)null;

					foreach (var cur in tags)
					{
						if (cur.IsDirty)
						{
							if (cur.Id.HasValue)
							{
								if (cur.IsRev && cur.IsRemoved)
								{
									// fix me: what is when there is a remote tag with the same key?
									deleteIdParameter.Value = cur.Id.Value;
									deleteCommand.ExecuteNonQueryEx();
								}
								else
								{
									updateIdParameter.Value = cur.Id.Value;
									updateClassParameter.Value = (int)cur.Class;
									updateValueParameter.Value = cur.Value ?? DBNull.Value;
									updateUserIdParameter.Value = cur.UserId;
									updateCreationDateParameter.Value = cur.CreationStamp;

									updateCommand.ExecuteNonQueryEx();
								}
							}
							else
							{
								if (nextLocalId.HasValue)
								{
									nextLocalId = nextLocalId.Value - 1;
									insertIdParameter.Value = nextLocalId.Value;
								}
								else
								{
									nextLocalId = transaction.GetNextLocalId("ObjectTags", "Id");
									insertIdParameter.Value = nextLocalId;
								}
								insertObjectIdParameter.Value = parent.Id;
								insertKeyParameter.Value = cur.Name;
								insertClassParameter.Value = (int)cur.Class;
								insertValueParameter.Value = cur.Value ?? DBNull.Value;
								insertUserIdParameter.Value = cur.UserId;
								insertCreationDatedParameter.Value = cur.CreationStamp;

								insertCommand.ExecuteNonQueryEx();

								// update id
								cur.Id = (long)insertIdParameter.Value;
								transaction.AddRollbackOperation(() => cur.Id = null);
							}
							cur.ResetDirty(transaction);
						}
					}

					ResetDirty(transaction);
				}
			}
		} // proc UpdateLocal

		#endregion

		#region -- Tag Manipulation -------------------------------------------------------

		private void UpdateRevisionTagCore(string key, PpsObjectTagClass tagClass, object value, List<PpsObjectTagView> removeTags)
		{
			var findTag = new Predicate<PpsObjectTagView>(t => t.IsRev && String.Compare(t.Name, key, StringComparison.OrdinalIgnoreCase) == 0);
			var tag = tags.Find(findTag) ?? deletedTags.Find(findTag);

			if (tag != null)
			{
				tag.Update(tagClass, value);
				removeTags?.Remove(tag);
			}
			else
			{
				tag = new PpsObjectTagView(this, null, key, true, tagClass, value, 0, DateTime.Now, true);
			}

			parent.IsDocumentChanged = true;
			EnsureTagInList(tag);
		} // func UpdateRevisionTagCore

		public void UpdateRevisionTag(string key, PpsObjectTagClass tagClass, object value)
		{
			if (tagClass != PpsObjectTagClass.Text
				&& tagClass != PpsObjectTagClass.Date
				&& tagClass != PpsObjectTagClass.Number)
				throw new ArgumentOutOfRangeException(nameof(tagClass));
			
			lock (SyncRoot)
			{
				if (state == PpsObjectTagLoadState.None)
					CheckTagsState();

				UpdateRevisionTagCore(key, tagClass, value, null);
			}
		} // proc UpdateRevisionTag

		public void UpdateRevisionTags(params PpsObjectTag[] tagList)
			=> UpdateRevisionTags(tagList);

		public void UpdateRevisionTags(IEnumerable<PpsObjectTag> tagList)
		{
			lock (SyncRoot)
			{
				if (state == PpsObjectTagLoadState.None)
					CheckTagsState();

				var removeTags = new List<PpsObjectTagView>(tags.Where(c => c.IsRev));

				// update tags
				foreach (var cur in tagList)
				{
					if ((cur.Class == PpsObjectTagClass.Date
						|| cur.Class == PpsObjectTagClass.Number
						|| cur.Class == PpsObjectTagClass.Text)
						&& cur.Value != null)
					{
						UpdateRevisionTagCore(cur.Name, cur.Class, cur.Value, removeTags);
					}
				}

				// remove not updated tags
				foreach (var k in removeTags)
					k.Remove();
			}

			OnCollectionChanged();
		} // proc RefreshTags


		public PpsObjectTagView UpdateTag(string key, PpsObjectTagClass cls, object value)
			=> UpdateTag(parent.Environment.UserId, key, cls, value);

		public PpsObjectTagView UpdateTag(long userId, string key, PpsObjectTagClass cls, object value)
		{
			try
			{
				lock (parent.SyncRoot)
				{
					CheckTagsState();

					var idx = IndexOf(key, userId);
					if (idx == -1 || tags[idx].Class != cls)
					{
						var newTag = new PpsObjectTagView(this, null, key, false, PpsObjectTagClass.Text, value, userId, DateTime.Now, true);
						tags.Add(newTag);
						return newTag;
					}
					else
					{
						var t = tags[idx];
						t.Update(cls, value);
						return t;
					}
				}
			}
			finally
			{
				OnCollectionChanged();
			}
		} // func UpdateTag

		public void Remove(string key)
		{
			lock (parent.SyncRoot)
			{
				CheckTagsState();

				var idx = IndexOf(key, parent.Environment.UserId);
				if (idx >= 0)
					tags[idx].Remove();
			}
		} // proc Remove

		public IEnumerator<PpsObjectTagView> GetEnumerator()
		{
			lock (SyncRoot)
			{
				CheckTagsState();

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

		public bool TryGetProperty(string name, out object value)
		{
			lock (SyncRoot)
			{
				var idx = IndexOf(name);
				if (idx >= 0)
				{
					value = tags[idx].Value;
					return true;
				}
				else
				{
					value = null;
					return false;
				}
			}
		} // func TryGetProperty

		private PpsObjectTagView FindTagById(long id)
		{
			lock (SyncRoot)
			{
				var f = new Predicate<PpsObjectTagView>(c => c.Id.HasValue && c.Id == id);
				return tags.Find(f) ?? deletedTags.Find(f);
			}
		} // func FindTagById
		
		public int IndexOf(string key)
		{
			var idxScore = 0;
			var idxFound = -1;

			lock (SyncRoot)
			{
				CheckTagsState();
				for(var i = 0;i < tags.Count;i++)
				{
					var t = tags[i];
					if (t.Name == key)
					{
						if (t.IsRev)
						{
							idxScore = 100;
							idxFound = i;
							break;
						}
						else
						{
							idxScore = 50;
							idxFound = i;
						}
					}
					else if (String.Compare(t.Name, key, StringComparison.OrdinalIgnoreCase) == 0)
					{
						if (t.IsRev)
						{
							idxScore = 100;
							idxFound = i;
							break;
						}
						else if (idxScore > 10)
						{
							idxScore = 0;
							idxFound = i;
						}
					}
				}
			}

			return idxFound;
		} // func IndexOf

		public int IndexOf(string key, long userId)
		{
			lock (parent.SyncRoot)
			{
				CheckTagsState();
				return tags.FindIndex(c => c.UserId == userId && String.Compare(c.Name, key, StringComparison.CurrentCultureIgnoreCase) == 0);
			}
		} // func Contains

		public int IndexOf(PpsObjectTagView tag)
			=> IndexOf(tag.Name, tag.UserId);

		public int IndexOf(PpsObjectTag tag)
			=> IndexOf(tag.Name, tag.UserId);

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

		void IList.Insert(int index, object value) => throw new NotSupportedException();

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

		void IList.Clear() => throw new NotSupportedException();

		void ICollection.CopyTo(Array array, int index)
		{
			lock (parent.SyncRoot)
				((ICollection)tags).CopyTo(array, index);
		} // proc CopyTo

		bool IList.Contains(object value)
			=> Contains((PpsObjectTag)value);

		int IList.IndexOf(object value)
			=> (value is PpsObjectTagView) ? IndexOf((PpsObjectTagView)value) : IndexOf((PpsObjectTag)value);

		IEnumerator IEnumerable.GetEnumerator()
			=> GetEnumerator();
		
		bool IList.IsReadOnly => true;
		bool IList.IsFixedSize => false;
		object IList.this[int index] { get { return this[index]; } set => throw new NotSupportedException(); }

		bool ICollection.IsSynchronized => true;
		public object SyncRoot => parent.SyncRoot;

		#endregion
		
		public int Count
		{
			get
			{
				lock (parent.SyncRoot)
				{
					return tags.Count;
				}
			}
		} // prop Count
		
		public bool IsDirty => isDirty;

		public PpsObjectTagView this[int index]
		{
			get
			{
				lock (parent.SyncRoot)
				{
					return tags[index];
				}
			}
		} // prop this

		public PpsObject Parent => parent;
	} // class PpsObjectTags

	#endregion

	#region -- interface IPpsObjectData -----------------------------------------------

	/// <summary>Basis implementation for the data-model.</summary>
	public interface IPpsObjectData : INotifyPropertyChanged
	{
		/// <summary>Load data.</summary>
		/// <returns></returns>
		Task LoadAsync();
		/// <summary>Commit the data to the local database.</summary>
		/// <returns></returns>
		Task CommitAsync();
		/// <summary>Pack the data for the server.</summary>
		/// <param name="dst"></param>
		/// <returns></returns>
		Task PushAsync(Stream dst);
		/// <summary>Unload the data.</summary>
		/// <returns></returns>
		Task UnloadAsync();
		
		/// <summary>Is the data loaded.</summary>
		bool IsLoaded { get; }
		/// <summary>Is the data change</summary>
		bool IsReadOnly { get; }

		/// <summary>Returns a preview image.</summary>
		object PreviewImage { get; }
		/// <summary>Returns a preview image.</summary>
		object PreviewImageLazy { get; }
	} // interface IPpsObjectData

	#endregion

	#region -- class PpsObjectBlobData ------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Control byte based data.</summary>
	public class PpsObjectBlobData : IPpsObjectData
	{
		public event PropertyChangedEventHandler PropertyChanged;

		private readonly PpsObject baseObj;
		private byte[] rawData = null;
		private string sha256 = String.Empty;
		private readonly LazyProperty<object> previewImage;

		public PpsObjectBlobData(PpsObject obj)
		{
			this.baseObj = obj;
			this.previewImage = new LazyProperty<object>(() => GetPreviewImageInternal(), () => OnPropertyChanged(nameof(PreviewImageLazy)));
		} // ctor

		internal void OnPropertyChanged(string propertyName)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		public async Task LoadAsync()
		{
			using (var src = await baseObj.LoadRawDataAsync())
			{
				rawData = src.ReadInArray();
				sha256 = baseObj.Tags.GetProperty("Sha256", null);
				OnPropertyChanged(nameof(IsLoaded));
			}
		} // proc LoadAsync

		public async Task CommitAsync()
		{
			using (var trans = await baseObj.Environment.MasterData.CreateTransactionAsync(PpsMasterDataTransactionLevel.ReadCommited))
			{
				baseObj.Tags.UpdateRevisionTag("Sha256", PpsObjectTagClass.Text, sha256);
				await baseObj.SaveRawDataAsync(
					rawData.Length,
					baseObj.MimeType ?? MimeTypes.Application.OctetStream,
					rawData,
					true
				);
				await baseObj.UpdateLocalAsync();

				trans.Commit();
			}
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

		//public Task ReadFromFileAsync(string filename)
		//{
		//	mimeType = StuffIO.MimeTypeFromFilename(filename);

		//	if (new FileInfo(filename).Length == 0)
		//	{
		//		rawData = new byte[] { };
		//		sha256 = StuffIO.GetStreamHash(new MemoryStream(rawData));
		//		return Task.CompletedTask;
		//	}

		//	using (var hashStream = new HashStream(new FileStream(filename, FileMode.Open), HashStreamDirection.Read, false, HashAlgorithm.Create("SHA-256")))
		//	{
		//		rawData = hashStream.ReadInArray();
		//		sha256 = StuffIO.CleanHash(BitConverter.ToString(hashStream.CheckSum));
		//	}
		//	return Task.CompletedTask;
		//}

		// only quick in dirty
		private class UpdateBlobStream: HashStream
		{
			private readonly PpsObjectBlobData blobData;

			public UpdateBlobStream(PpsObjectBlobData blobData, Stream baseStream)
				:base(baseStream, HashStreamDirection.Write, false, SHA256.Create())
			{
				this.blobData = blobData;
			} // ctor

			protected override void OnFinished(byte[] bCheckSum)
			{
				base.OnFinished(bCheckSum);

				blobData.sha256 = StuffIO.CleanHash(BitConverter.ToString(bCheckSum)); // Convert.ToBase64String(bCheckSum);
				blobData.rawData = BaseStream.ReadInArray();
			} // proc OnFinished
		}

		/// <summary>Creates a data stream for file access.</summary>
		/// <param name="mode"></param>
		/// <returns></returns>
		public async Task<Stream> OpenStreamAsync(FileAccess mode)
		{
			
			switch(mode)
			{
				case FileAccess.Read:
					// open an existing data stream
					if (!IsLoaded)
						await LoadAsync();
					return new MemoryStream(RawData, false);
				case FileAccess.Write:
					// open or create the data stream
					return new UpdateBlobStream(this, new MemoryStream());
				default:
					throw new ArgumentOutOfRangeException(nameof(mode));
			}
		} // func CreateDataStreamAsync

		#region -- GetPreviewImageInternal --------------------------------------------

		protected async Task<object> GetPreviewFromImageData()
		{
			// todo: we will cache the previews local

			// we can only create a preview when the data is local availabe, we will not force a pull
			if (!baseObj.HasData)
				return null;

			//await Task.Delay(5000);

			// get access to the image stream, this will not load the data stream
			using (var src = await OpenStreamAsync(FileAccess.Read))
			{
				var sourceImage = new BitmapImage();

				sourceImage.BeginInit();
				sourceImage.CacheOption = BitmapCacheOption.OnLoad;
				sourceImage.StreamSource = src;
				sourceImage.EndInit();
				sourceImage.Freeze();

				var aspect = sourceImage.Height / sourceImage.Width;
				int newWidth;
				int newHeight;
				const int previewHeight = 256;
				if (sourceImage.Height > sourceImage.Width)
				{
					newWidth = Convert.ToInt32(previewHeight / aspect);
					newHeight = previewHeight;
				}
				else
				{
					newWidth = previewHeight;
					newHeight = Convert.ToInt32(previewHeight * aspect);
				}

				// create preview image
				var group = new DrawingGroup();
				RenderOptions.SetBitmapScalingMode(group, BitmapScalingMode.HighQuality);

				group.Children.Add(new ImageDrawing(sourceImage, new Rect(0, 0, newWidth, newHeight)));

				// todo: check for over and render it

				var drawingVisual = new DrawingVisual();
				using (var dc = drawingVisual.RenderOpen())
					dc.DrawDrawing(group);

				var resizedImage = new RenderTargetBitmap(
					newWidth, newHeight,
					96, 96,
					PixelFormats.Default);
				resizedImage.Render(drawingVisual);

				var previewImage = BitmapFrame.Create(resizedImage);
				previewImage.Freeze();
				return previewImage;
			}
		} // func GetPreviewImageInternal

		//protected virtual Task<object> GetPreviewImageInternal()
		//	=> MimeType.StartsWith("image/")
		//		? GetPreviewFromImageData()
		//		: Task.FromResult<object>(null);

		protected virtual Task<object> GetPreviewImageInternal()
			=> MimeType.StartsWith("image/")
				? GetPreviewFromImageData()
				: Task.FromResult<object>("fileOutline");


		protected void ResetPreviewImage()
			=> previewImage.Reset();

		public Task<object> GetPreviewImageAsync()
			=> previewImage.GetValueAsync();

		#endregion

		public bool IsLoaded => rawData != null;
		public bool IsReadOnly => true;

		public string MimeType => baseObj.MimeType ?? MimeTypes.Application.OctetStream;

		//public byte[] RawData => rawData;
		public byte[] RawData { get { return rawData; } internal set { this.rawData = value; } }

		/// <summary>Get preview image synchron.</summary>
		public object PreviewImage => previewImage.GetValueAsync().AwaitTask();
		/// <summary>Get preview image asyncron</summary>
		public object PreviewImageLazy => previewImage.GetValue();
	} // class PpsObjectBlobData

	#endregion

	#region -- class PpsObjectImageData -----------------------------------------------

	//public sealed class PpsObjectImageData : PpsObjectBlobData
	//{
	//	#region privates
	//	private readonly PpsObject baseObj;

	//	private bool imageLoaded = false;
	//	private bool previewLoaded = false;
	//	private bool overlayLoaded = false;

	//	private ImageSource image = null;
	//	private ImageSource preview = null;
	//	private ImageSource overlay = null;
	//	#endregion

	//	#region consts
	//	const string PreviewId = "preview";
	//	const string OverlayId = "overlay";
	//	const string PictureItemId = "PictureItemType";
	//	#endregion

	//	#region syncronisation
	//	private static SemaphoreSlim LoadPreviewSemaphore = new SemaphoreSlim(1, 1);
	//	#endregion

	//	#region ctor

	//	~PpsObjectImageData()
	//	{
	//		image = null;
	//	}

	//	public PpsObjectImageData(PpsObject obj) : base(obj)
	//	{
	//		this.baseObj = obj;
	//	}
	//	#endregion

	//	#region Functionality

	//	#region Preview

	//	private async void LoadPreview()
	//	{
	//		if (baseObj == null || !baseObj.MimeType.StartsWith("image"))
	//			return;

	//		// semaphore prevents this imageobject from being loaded multiple times, if the preview ist getted multiple times
	//		await LoadPreviewSemaphore.WaitAsync();

	//		if (!PreviewLoaded)
	//		{
	//			foreach (var lnk in baseObj.Links)
	//			{
	//				var idx = lnk.LinkTo.Tags.IndexOf(PictureItemId);
	//				if (idx >= 0 && (string)lnk.LinkTo.Tags[idx].Value == PreviewId)
	//				{
	//					var imgObj = await lnk.LinkTo.GetDataAsync<PpsObjectImageData>();
	//					preview = imgObj.Image;

	//					imgObj.PropertyChanged += LinkedImage_PropertyChanged;

	//					if (!imgObj.ImageLoaded)
	//						PreviewLoaded = false;
	//					else
	//						PreviewLoaded = true;
	//					break;
	//				}
	//			}

	//			if (!PreviewLoaded && preview == null)
	//			{
	//				if (imageLoaded)
	//				{
	//					var enc = new PngBitmapEncoder();
	//					var bI = new BitmapImage();

	//					using (MemoryStream stream = new MemoryStream(this.RawData))
	//					{
	//						bI.BeginInit();
	//						bI.CacheOption = BitmapCacheOption.OnLoad;
	//						bI.StreamSource = stream;
	//						bI.DecodePixelHeight = 120;
	//						bI.EndInit();
	//					}

	//					bI.Freeze();

	//					enc.Frames.Add(BitmapFrame.Create(bI));

	//					var obj = await baseObj.Environment.CreateNewObjectAsync(baseObj.Environment.ObjectInfos[PpsEnvironment.AttachmentObjectTyp]);

	//					obj.Tags.UpdateTag(baseObj.Environment.UserId, PictureItemId, PpsObjectTagClass.Text, PreviewId);

	//					using (var ms = new MemoryStream())
	//					{
	//						enc.Save(ms);
	//						ms.Position = 0;

	//						var data = await obj.GetDataAsync<PpsObjectBlobData>();
	//						await data.ReadFromStreamAsync(ms, MimeTypes.Image.Png);
	//						await data.CommitAsync();
	//					}

	//					enc = null;
	//					bI = null;

	//					baseObj.Links.AppendLink(obj);
	//					await baseObj.UpdateLocalAsync();

	//					preview = (await obj.GetDataAsync<PpsObjectImageData>()).Image;
	//					PreviewLoaded = true;
	//				}
	//				else
	//				{
	//					PropertyChanged += CreatePreviewFromImage;
	//					LoadImage();
	//				}
	//			}
	//		}
	//		LoadPreviewSemaphore.Release();
	//	}

	//	/// <summary>
	//	/// This handler is called, when the underlying image is loaded, to restart the creation of the preview
	//	/// </summary>
	//	/// <param name="sender"></param>
	//	/// <param name="e"></param>
	//	private void CreatePreviewFromImage(object sender, PropertyChangedEventArgs e)
	//	{
	//		if (e.PropertyName == nameof(Image))
	//		{
	//			PropertyChanged -= CreatePreviewFromImage;
	//			LoadPreview();
	//		}
	//	}

	//	/// <summary>
	//	/// true, if loading is finished (does not mean there must be a valid preview)
	//	/// </summary>
	//	public bool PreviewLoaded { get { return previewLoaded; } set { previewLoaded = value; base.OnPropertyChanged(nameof(Preview)); } }

	//	/// <summary>
	//	/// returns the Preview if loaded - starts the loading otherwise, if preview is not set, returns Image
	//	/// </summary>
	//	public ImageSource Preview
	//	{
	//		get
	//		{
	//			{
	//				if (!PreviewLoaded)
	//					LoadPreview();
	//				else if (preview == null)
	//					return Image;
	//				return preview;
	//			}
	//		}
	//	}

	//	#endregion

	//	#region Image

	//	/// <summary>
	//	/// requests the image from SQLite
	//	/// </summary>
	//	private async void LoadImage()
	//	{
	//		if (baseObj == null || !baseObj.MimeType.StartsWith("image"))
	//			return;

	//		if (!imageLoaded)
	//		{
	//			await LoadAsync().ConfigureAwait(true);

	//			var bI = new BitmapImage();

	//			using (MemoryStream stream = new MemoryStream(this.RawData))
	//			{
	//				bI.BeginInit();
	//				bI.CacheOption = BitmapCacheOption.OnLoad;
	//				bI.StreamSource = stream;
	//				bI.DecodePixelHeight = 600;
	//				bI.EndInit();
	//			}
	//			image = bI.Clone();
	//			//image = bI;

	//			ImageLoaded = true;
	//		}
	//	}

	//	/// <summary>
	//	/// Resize the image to the specified width and height.
	//	/// </summary>
	//	/// <param name="image">The image to resize.</param>
	//	/// <param name="width">The width to resize to.</param>
	//	/// <param name="height">The height to resize to.</param>
	//	/// <returns>The resized image.</returns>
	//	public static Bitmap ResizeImage(Image image, int width, int height)
	//	{
	//		var destRect = new Rectangle(0, 0, width, height);
	//		var destImage = new Bitmap(width, height);

	//		destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

	//		using (var graphics = Graphics.FromImage(destImage))
	//		{
	//			graphics.CompositingMode = CompositingMode.SourceCopy;
	//			graphics.CompositingQuality = CompositingQuality.HighQuality;
	//			graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
	//			graphics.SmoothingMode = SmoothingMode.HighQuality;
	//			graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

	//			using (var wrapMode = new ImageAttributes())
	//			{
	//				wrapMode.SetWrapMode(WrapMode.TileFlipXY);
	//				graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
	//			}
	//		}

	//		return destImage;
	//	}

	//	/// <summary>
	//	/// true, if loading is finished (does not mean there must be a valid image)
	//	/// </summary>
	//	public bool ImageLoaded { get { return imageLoaded; } set { imageLoaded = value; if (value == true) base.OnPropertyChanged(nameof(Image)); } }

	//	/// <summary>
	//	/// returns the Image if loaded - starts the loading otherwise
	//	/// </summary>
	//	public ImageSource Image
	//	{
	//		get
	//		{
	//			if (!imageLoaded)
	//			{
	//				LoadImage();
	//			}

	//			return image;
	//		}
	//	}

	//	/// <summary>
	//	/// used to propagate through that the underlying imageobject has changed
	//	/// </summary>
	//	/// <param name="sender">underlying object</param>
	//	/// <param name="e">not used</param>
	//	private void LinkedImage_PropertyChanged(object sender, PropertyChangedEventArgs e)
	//	{
	//		if (!((PpsObjectImageData)sender).ImageLoaded)
	//			return;

	//		var idx = ((PpsObjectImageData)sender).baseObj.Tags.IndexOf(PictureItemId);
	//		if (idx >= 0)
	//		{
	//			if ((string)((PpsObjectImageData)sender).baseObj.Tags[idx].Value == PreviewId)
	//			{
	//				preview = ((PpsObjectImageData)sender).Image;
	//				PreviewLoaded = true;
	//				((PpsObjectImageData)sender).PropertyChanged -= LinkedImage_PropertyChanged;
	//			}
	//			if ((string)((PpsObjectImageData)sender).baseObj.Tags[idx].Value == OverlayId)
	//			{
	//				overlay = ((PpsObjectImageData)sender).Image;
	//				OverlayLoaded = true;
	//				((PpsObjectImageData)sender).PropertyChanged -= LinkedImage_PropertyChanged;
	//			}
	//		}
	//	}

	//	#endregion

	//	#region Overlay

	//	/// <summary>
	//	/// requests the overlay from SQLite
	//	/// </summary>
	//	private async void LoadOverlay()
	//	{
	//		if (baseObj == null || !baseObj.MimeType.StartsWith("image"))
	//			return;

	//		if (!overlayLoaded)
	//		{
	//			foreach (var lnk in baseObj.Links)
	//			{
	//				var idx = lnk.LinkTo.Tags.IndexOf(PictureItemId);
	//				if (idx >= 0)
	//				{
	//					if ((string)lnk.LinkTo.Tags[idx].Value == OverlayId)
	//					{
	//						var imgObj = await lnk.LinkTo.GetDataAsync<PpsObjectImageData>().ConfigureAwait(false);

	//						if (imgObj.ImageLoaded)
	//						{
	//							overlay = imgObj.Image;
	//							OverlayLoaded = true;
	//						}
	//						else
	//							imgObj.PropertyChanged += LinkedImage_PropertyChanged;

	//						break;
	//					}
	//				}
	//			}
	//		}
	//	}

	//	/// <summary>p
	//	/// true, if loading is finished (does not mean there must be a valid overlay)
	//	/// </summary>
	//	public bool OverlayLoaded { get { return overlayLoaded; } set { overlayLoaded = value; OnPropertyChanged(nameof(Overlay)); } }

	//	/// <summary>
	//	/// returns the Overlay if loaded - starts the loading otherwise
	//	/// </summary>
	//	public ImageSource Overlay
	//	{
	//		get
	//		{
	//			{
	//				if (!OverlayLoaded)
	//					LoadOverlay();

	//				return overlay;
	//			}
	//		}
	//	}

	//	#endregion

	//	#endregion
	//}

	#endregion

	#region -- class PpsObjectDataSet -------------------------------------------------

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
							Read(xData, false);
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
				baseObj.Tags.UpdateRevisionTags(GetAutoTags().ToList());

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

		public Task<object> GetPreviewImageAsync()
			=> Task.FromResult<object>(null);

		/// <summary>The document it self implements the undo-manager.</summary>
		public PpsUndoManager UndoManager => undoManager;
		/// <summary>Is the document fully loaded.</summary>
		public bool IsLoaded => IsInitialized;
		/// <summary>Is the data set readonly.</summary>
		public bool IsReadOnly => false;
		/// <summary>This document is connected with ...</summary>
		public PpsObject Object => baseObj;
		/// <summary>Returns the icon of this dataset.</summary>
		public object PreviewImage => null;
		/// <summary>Returns the icon of this dataset.</summary>
		public object PreviewImageLazy => null;
	} // class PpsObjectDataSet

	#endregion

	#region -- class PpsRevisionDataSet -----------------------------------------------

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

		private readonly LazyProperty<IPpsObjectData> data;			// access to the object data
		private readonly PpsObjectTags tags;                // list with assigned tags
		private readonly PpsObjectLinks links;              // linked objects

		private bool isDirty = false;

		#region -- Ctor/Dtor --------------------------------------------------------------

		/// <summary></summary>
		/// <param name="environment"></param>
		/// <param name="localId"></param>
		internal PpsObject(PpsEnvironment environment, IDataReader r)
		{
			this.environment = environment;
			this.objectId = r.GetInt64(0);

			this.columns = new PpsObjectColumns(this);
			this.data = new LazyProperty<IPpsObjectData>(() => GetDataCoreAsync(), () => OnPropertyChanged(nameof(DataLazy)));
			this.staticValues = new object[staticColumns.Length];
			this.tags = new PpsObjectTags(this);
			this.links = new PpsObjectLinks(this);

			environment.MasterData.RegisterWeakDataRowChanged("Objects", objectId, OnObjectDataChanged);

			ReadObjectInfo(r);
		} // ctor

		private void OnObjectDataChanged(object sender, PpsDataRowChangedEventArgs e)
			=> ReadObjectInfo(e.Arguments);

		/// <summary>Reads the properties from the local database.</summary>
		/// <param name="r"></param>
		private void ReadObjectInfo(IDataReader r)
		{
			// update the values
			for (var i = 1; i < StaticColumns.Length; i++)
				SetValue((PpsStaticObjectColumnIndex)i, r.IsDBNull(i) ? null : r.GetValue(i), false);

			// check for tags (the browser returns no user tags, only system tags)
			if (r.FieldCount >= StaticColumns.Length && !r.IsDBNull(StaticColumns.Length))
				tags.RefreshTagsFromString(r.GetString(StaticColumns.Length));

			ResetDirty(null);
		} // proc ReadObjectInfo

		/// <summary>Reads the core properties from sync or pull.</summary>
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

			// tags, user tags only
			tags.ReadTagsFromXml(x.Elements("tag")); // refresh of the pulled system tags, removes current system tags

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

			using (var cmd = trans.CreateNativeCommand(
				"UPDATE main.[Objects] SET Id = @Id WHERE Id = @OldId; "+
				"UPDATE main.[ObjectTags] SET ObjectId = @Id WHERE Id = @OldId; "+
				"UPDATE main.[ObjectLinks] SET ParentObjectId = @Id WHERE ParentObjectId = @OldId; " +
				"UPDATE main.[ObjectLinks] SET LinkObjectId = @Id WHERE LinkObjectId = @OldId; "))
			{
				var oldObjectId = objectId;
				// updates the object id in the local database
				cmd.AddParameter("@Id", DbType.Int64, newObjectId);
				cmd.AddParameter("@OldId", DbType.Int64, oldObjectId);
				await cmd.ExecuteNonQueryExAsync();

				// refresh the id's in the cache
				objectId = newObjectId;
				trans.AddRollbackOperation(() => objectId = oldObjectId);

				// refresh Id in dictionary
				// linked objects should realize the change on the database change (in dataset, and object structure)
				environment.ReplaceObjectCacheId(oldObjectId, newObjectId, false);
				trans.AddRollbackOperation(() => environment.ReplaceObjectCacheId(newObjectId, oldObjectId, true));
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
							if (linkTo != null && !linkTo.HasData)
							{
								linkTo.EnqueuePullAsync(null)
								.ContinueWith(t => environment.AppendException(t.Exception), TaskContinuationOptions.OnlyOnFaulted); // not in foreground
							}
						}
					}

					// update data block
					using (var trans = Environment.MasterData.CreateTransactionAsync(PpsMasterDataTransactionLevel.Write, CancellationToken.None, foregroundTransaction).AwaitTask())
					{
						SaveRawDataAsync(c.ContentLength - headerLength, MimeType,
							dst => c.Content.CopyTo(dst),
							false
						).AwaitTask();

						SetValue(PpsStaticObjectColumnIndex.PulledRevId, pulledRevId, true);

						// persist current object state
						UpdateLocalAsync().AwaitTask();

						if (foregroundTransaction == null)
							trans.Commit();
					}
					return c.Content;
				});

				// read the object stream from server
				return request.Enqueue(PpsLoadPriority.ObjectPrimaryData, true);
			}
		} // proc PullDataAsync

		private async Task<IPpsProxyTask> EnqueuePullRevisionAsync(long pulledRevId)
		{
			// check if the environment is online, force online
			await Environment.ForceOnlineAsync();

			// load the data from the server
			if (objectId <= 0)
				throw new ArgumentOutOfRangeException("objectId", "Invalid server id, this is a local only object and has no server representation.");

			if (pulledRevId < 0)
				pulledRevId = RemoteHeadRevId;

			// check download manager
			lock (objectLock)
			{
				var request = PullDataRequest(pulledRevId);
				if (environment.WebProxy.TryGet(request, out var task))
					return task;

				// read the object stream from server
				return request.Enqueue(PpsLoadPriority.ObjectPrimaryData, true);
			}
		} // proc EnqueuePullRevisionAsync

		public async Task<IPpsObjectData> PullAsync()
		{
			// foreground means a thread transission, we just wait for the task to finish.
			// that we do not get any deadlocks with the db-transactions, we need to set the transaction of the current thread.
			using (var r = await (await EnqueuePullAsync(Environment.MasterData.CurrentTransaction)).ForegroundAsync())
			{
				// read prev stored data
				var data = await GetDataCoreAsync();
				await data.LoadAsync();
				return data;
			}
		} // proc PullDataAsync

		[Obsolete("Implemented for a special case, will be removed.")]
		public async Task<T> PullRevisionAsync<T>(long revId)
			where T : IPpsObjectData
		{
			using (var r = await (await EnqueuePullRevisionAsync(-1)).ForegroundAsync())
			using (var src = r.GetResponseStream())
			{

				var headerLengthString = r.Headers["ppsn-header-length"];
				if (String.IsNullOrEmpty(headerLengthString)
					|| !Int64.TryParse(headerLengthString, out var headerLength)
					|| headerLength < 10)
					throw new ArgumentOutOfRangeException("ppsn-header-length", headerLengthString, "Header is missing.");

				using (var headerData = new WindowStream(src, 0, headerLength, false, true))
				using (var xmlHeader = XmlReader.Create(headerData, Procs.XmlReaderSettings))
					XElement.Load(xmlHeader);

				// todo: create object? only implemented for a special sub case
				// PpsRevisionDataSet?
				var schema = await Environment.ActiveDataSets.GetDataSetDefinitionAsync(Typ);
				var ds = new PpsObjectDataSet(schema, this); // wrong object!

				using (var dataSrc = new WindowStream(src, headerLength, r.ContentLength - headerLength, false, true))
				using (var xmlData = XmlReader.Create(dataSrc, Procs.XmlReaderSettings))
					ds.Read(XElement.Load(xmlData));

				return (T)(IPpsObjectData)ds;
			}
		} // func PullRevisionAsync

		public async Task PushAsync()
		{
			XElement xAnswer;
			using (var trans = await Environment.MasterData.CreateTransactionAsync(PpsMasterDataTransactionLevel.Write))
			{
				var request = PushDataRequest();

				Monitor.Enter(objectLock);
				try
				{
					// update local database and object data
					var data = await GetDataAsync<IPpsObjectData>();
					await data.LoadAsync(); // todo: weg? is the responsibility from PushAsync
					await data.CommitAsync();

					foreach(var lnk in links)
					{
						if (lnk.LinkToId < 0 || lnk.LinkTo.IsDocumentChanged)
							await lnk.LinkTo.PushAsync();
					}
					
					// first build object data
					var xHeaderData = ToXml();
					var headerData = Encoding.Unicode.GetBytes(xHeaderData.ToString(SaveOptions.DisableFormatting));
					request.Headers["ppsn-header-length"] = headerData.Length.ChangeType<string>();
					if (PulledRevId > 0) // we do not send pulled rev Id in the header
						request.Headers["ppsn-pulled-revId"] = PulledRevId.ChangeType<string>();

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
				else if (xAnswer.Name.LocalName == "object") // the only answer 
				{
					// first update the new object id
					var newObjectId = xAnswer.GetAttribute<long>(nameof(Id), -1);
					await UpdateObjectIdAsync(trans, newObjectId);

					// update object data
					ReadObjectInfo(new XAttributesPropertyDictionary(xAnswer));
					
					// repull the whole object, to get the revision from server (head)
					await PullAsync();

					// write local database
					await UpdateLocalAsync();

					trans.Commit();
				}
				else
					throw new ArgumentException("Could not parse push-answer.");
			}
		} // proc PushAsync

		private async Task<IPpsObjectData> GetDataCoreAsync()
		{
			// update data from server, if not present (pull head)
			if (objectId >= 0 && !HasData)
				return await PullAsync();

			// create the core data object
			return await environment.CreateObjectDataObjectAsync<IPpsObjectData>(this);
		} // func GetDataCoreAsync

		public async Task<T> GetDataAsync<T>()
			where T : IPpsObjectData
			=> (T)await data.GetValueAsync();

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

		[Obsolete("falsche verantwortung")]
		internal async Task SaveRawDataAsync(long contentLength, string mimeType, byte[] data, bool isDocumentChanged)
		{
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
				cmd.AddParameter("@Document", DbType.Binary, data ?? (object)DBNull.Value);
				cmd.AddParameter("@DocumentIsChanged", DbType.Boolean, isDocumentChanged);

				await cmd.ExecuteNonQueryAsync();

				// set HasData to true
				SetValue(PpsStaticObjectColumnIndex.MimeType, mimeType, false);
				SetValue(PpsStaticObjectColumnIndex.IsDocumentChanged, isDocumentChanged, false);
				SetValue(PpsStaticObjectColumnIndex.HasData, true, false);

				trans.Commit();
			}
		} // proc SaveRawDataAsync

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

		[Obsolete("falsche verantwortung")]
		public async void ShellExecute()
		{
			var filename = (from t in Tags where t.Name == "Filename" select t).FirstOrDefault()?.Value.ToString();

			if (String.IsNullOrEmpty(filename))
				return;

			filename = System.IO.Path.GetTempPath() + "\\" + Path.GetFileName(filename);

			//if (Path.GetExtension(filename) == ".exe") // ToDo: ask if the executeable may run

			using (var fileStream = File.OpenWrite(filename))
			{
				var buffer = await LoadRawDataAsync();
				fileStream.Write(buffer.ReadInArray(), 0, (int)buffer.Length);
				fileStream.Close();
				System.Diagnostics.Process.Start(filename);
			}
		}

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
			links.AddToXml(xObj, "linksTo");

			// add system tags
			tags.WriteTagsToXml(xObj, "tag");

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
		{
			lock (objectLock)
				return index == 0 ? (T)(object)objectId : (staticValues[index] ?? empty).ChangeType<T>();
		} // func GetValue

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
				=> StaticColumns.Concat(new IDataColumn[] { StaticDataColumn, StaticDataAsyncColumn, StaticTagsColumn, StaticLinksColumn }).Concat(obj.Tags.Select(c => CreateSimpleDataColumn(c))).GetEnumerator();

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
							return StaticDataAsyncColumn;
						else if (index == StaticColumns.Length + 2)
							return StaticTagsColumn;
						else if (index == StaticColumns.Length + 3)
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
				if (index < 0)
					throw new ArgumentOutOfRangeException();
				else if (index == 0)
				{
					lock (objectLock)
						return objectId;
				}
				else if (index < StaticColumns.Length)
				{
					lock (objectLock)
						return staticValues[index];
				}
				else if (index == StaticColumns.Length + 0)
					return Data;
				else if (index == StaticColumns.Length + 1)
					return DataLazy;
				else if (index == StaticColumns.Length + 2)
				{
					lock (objectLock)
						return tags;
				}
				else if (index == StaticColumns.Length + 3)
				{
					lock (objectLock)
						return links;
				}
				else if (index < StaticColumns.Length + Tags.Count + staticPropertyCount)
				{
					lock (objectLock)
						return tags[index - StaticColumns.Length - staticPropertyCount].Value;
				}
				else
					throw new ArgumentOutOfRangeException();
			}
		} // prop this

		#endregion

		internal void SetDirty()
		{
			if (!isDirty)
			{
				isDirty = true;
				OnPropertyChanged(nameof(IsChanged));
			}
		} // proc SetDirty

		private void ResetDirty(PpsMasterDataTransaction transaction)
		{
			if (isDirty)
			{
				transaction?.AddRollbackOperation(SetDirty);
				isDirty = false;
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
		public bool IsDocumentChanged
		{
			get => GetValue((int)PpsStaticObjectColumnIndex.IsDocumentChanged, false);
			set => SetValue(PpsStaticObjectColumnIndex.IsDocumentChanged, value, true);
		} // prop IsDocumentChanged


		/// <summary></summary>
		public IPpsObjectData Data => data.GetValueAsync().AwaitTask();
		/// <summary></summary>
		public IPpsObjectData DataLazy => data.GetValue();
		/// <summary>Has this object local data available.</summary>
		public bool HasData => GetValue((int)PpsStaticObjectColumnIndex.HasData, false);

		/// <summary>Access to the links of the object.</summary>
		public PpsObjectLinks Links => links;
		/// <summary>Object tags and properties</summary>
		public PpsObjectTags Tags => tags;

		/// <summary>Is the meta data changed and not persisted in the local database.</summary>
		public bool IsChanged => isDirty;

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

		internal const int staticPropertyCount = 4;

		internal static IDataColumn[] StaticColumns => staticColumns;
		internal static string StaticColumnsSelect { get; }

		internal static IDataColumn StaticDataColumn { get; } = new SimpleDataColumn(nameof(Data), typeof(IPpsObjectData));
		internal static IDataColumn StaticDataAsyncColumn { get; } = new SimpleDataColumn(nameof(DataLazy), typeof(IPpsObjectData));
		internal static IDataColumn StaticTagsColumn { get; } = new SimpleDataColumn(nameof(Tags), typeof(PpsObjectTags));
		internal static IDataColumn StaticLinksColumn { get; } = new SimpleDataColumn(nameof(Links), typeof(PpsObjectLinks));
	} // class PpsObject

	#endregion

	#region -- class PpsObjectInfo ------------------------------------------------------

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
		private readonly Dictionary<long, int> objectStoreById = new Dictionary<long, int>(); // hold current and negative old ids
		private readonly Dictionary<Guid, int> objectStoreByGuid = new Dictionary<Guid, int>();

		private readonly PpsEnvironmentCollection<PpsObjectInfo> objectInfo;

		private long? lastObjectId = null;
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
							this.columnAlias = "coalesce(" + virtualColumn + ".[LocalValue], " + virtualColumn + ".[Value])";

							switch (classification)
							{
								case DateClass:
									columnAlias = CastToDateExpression(columnAlias);
									break;
								case 0:
									this.columnAlias = "coalesce(" + virtualColumn + ".[LocalValue], " + virtualColumn + ".[Value], " + virtualColumn + ".[Key])";
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
							return CastToDateExpression("coalesce(" + joinAlias + ".[LocalValue]," + joinAlias + ".[Value])") + " AS " + columnAlias;
						case NumberClass:
						default:
							return "coalesce(" + joinAlias + ".[LocalValue]," + joinAlias + ".[Value]) AS " + columnAlias;
					}
				} // func CreateWhereExpression

				public string CreateLeftOuterJoinExpression()
				{
					switch (type)
					{
						case ObjectViewColumnType.All:
							return "LEFT OUTER JOIN ObjectTags AS " + AllColumns + " ON (o.Id = " + AllColumns + ".ObjectId AND ifnull(" + AllColumns + ".[LocalClass], " + AllColumns + ".[Class]) >= 0)";
						case ObjectViewColumnType.Date:
							return "LEFT OUTER JOIN ObjectTags AS " + DateColumns + " ON (o.Id = " + DateColumns + ".ObjectId AND ifnull(" + AllColumns + ".[LocalClass], " + AllColumns + ".[Class]) = " + DateClass + ")";
						case ObjectViewColumnType.Number:
							return "LEFT OUTER JOIN ObjectTags AS " + NumberColumns + " ON (o.Id = " + NumberColumns + ".ObjectId AND ifnull(" + AllColumns + ".[LocalClass], " + AllColumns + ".[Class]) = " + NumberClass + ")";

						case ObjectViewColumnType.Key:
							if (classification == 0)
								return "LEFT OUTER JOIN ObjectTags AS " + joinAlias + " ON (o.Id = " + joinAlias + ".ObjectId AND ifnull(" + joinAlias + ".[LocalClass], " + joinAlias + ".[Class]) >= 0 AND " + joinAlias + ".Key = '" + keyName + "' COLLATE NOCASE)";
							else
								return "LEFT OUTER JOIN ObjectTags AS " + joinAlias + " ON (o.Id = " + joinAlias + ".ObjectId AND ifnull(" + joinAlias + ".[LocalClass], " + joinAlias + ".[Class]) = " + classification + " AND " + joinAlias + ".Key = '" + keyName + "' COLLATE NOCASE)";
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
				cmd.Append("group_concat('S' || s_all.Id || ':' || s_all.Key || ':' || ifnull(s_all.LocalClass , s_all.Class) || ':' || s_all.UserId || '=' || replace(ifnull(s_all.LocalValue, s_all.Value), char(10), ' '), char(10)) as [Values]");

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

		public async Task<PpsObject> CreateNewObjectFromFileAsync(string fileName)
		{
			var lastWriteTime = File.GetLastWriteTimeUtc(fileName);

			using (var trans = await MasterData.CreateTransactionAsync(PpsMasterDataTransactionLevel.Write))
			using (var src = new FileStream(fileName, FileMode.Open, FileAccess.Read))
			{
				var newObject = await CreateNewObjectFromStreamAsync(src, Path.GetFileName(fileName));
				newObject.Tags.UpdateRevisionTag("FileName", PpsObjectTagClass.Text, fileName);
				newObject.Tags.UpdateRevisionTag("LastWriteTime", PpsObjectTagClass.Date, lastWriteTime.ToString(CultureInfo.InvariantCulture));

				// write changes
				await newObject.UpdateLocalAsync();

				return newObject;
			}
		} // func CreateNewObjectFromFileAsync

		public async Task<PpsObject> CreateNewObjectFromStreamAsync(Stream dataSource, string name, string mimeType = null)
		{
			using (var trans = await MasterData.CreateTransactionAsync(PpsMasterDataTransactionLevel.Write))
			{
				if (mimeType == null)
					mimeType = StuffIO.MimeTypeFromFilename(name);

				// create the new empty object
				var newObject = await CreateNewObjectAsync(ObjectInfos[AttachmentObjectTyp], mimeType);
				newObject.Tags.UpdateRevisionTag("Name", PpsObjectTagClass.Text, name);

				// import the data
				var data = await newObject.GetDataAsync<PpsObjectBlobData>();

				using (var dst = await data.OpenStreamAsync(FileAccess.Write))
					await dataSource.CopyToAsync(dst);
				await data.CommitAsync();

				// write changes
				await newObject.UpdateLocalAsync();

				trans.Commit();
				return newObject;
			}
		} // func CreateNewObjectFromStreamAsync

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
				lastObjectId = lastObjectId.HasValue ? lastObjectId.Value - 1 : trans.GetNextLocalId("Objects", "Id"); // cache the number to get no conflict with already pulled objects
				var newObjectId = lastObjectId.Value;
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

		internal async Task<T> CreateObjectDataObjectAsync<T>(PpsObject obj)
			where T : IPpsObjectData
		{
			var schema = await ActiveDataSets.GetDataSetDefinitionAsync(obj.Typ);
			if (schema == null)
				return (T)(IPpsObjectData)new PpsObjectBlobData(obj);
			else
				return (T)(IPpsObjectData)new PpsObjectDataSet(schema, obj);
		} // func CreateObjectDataObjectAsync


		[LuaMember]
		public Task PushObjectAsync(PpsObject obj)
			=> obj.IsDocumentChanged
				? obj.PushAsync()
				: Task.CompletedTask;

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

			// mark document as read
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

		internal void ReplaceObjectCacheId(long newObjectId, long oldObjectId, bool removeOldId)
		{
			lock (objectStoreLock)
			{
				if (objectStoreById.TryGetValue(oldObjectId, out var idx))
				{
					if (removeOldId)
						objectStoreById.Remove(oldObjectId);
					objectStoreById[newObjectId] = idx;
				}
			}
		} // func ReplaceObjectCacheId

		[LuaMember]
		public PpsObject GetObject(long localId, bool throwException = false)
			=> GetCachedObjectOrRead(objectStoreById, localId, useId, throwException);

		[LuaMember]
		public PpsObject GetObject(Guid guid, bool throwException = false)
			=> GetCachedObjectOrRead(objectStoreByGuid, guid, useGuid, throwException);

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
