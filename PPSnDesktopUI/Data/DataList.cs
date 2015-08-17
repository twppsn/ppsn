using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Neo.IronLua;
using TecWare.DES.Networking;

namespace TecWare.PPSn.Data
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Fetches data from a source page by page.</summary>
	public sealed class PpsDataList : IList, INotifyCollectionChanged, INotifyPropertyChanged
	{
		private enum FetchFollow
		{
			None,
			NextWindow,
			NextAll
		} // enum FetchFollow

		/// <summary></summary>
		public event NotifyCollectionChangedEventHandler CollectionChanged;
		/// <summary></summary>
		public event PropertyChangedEventHandler PropertyChanged;

		private PpsEnvironment environment;
		private Uri dataSource;			// Source of the current list
		private int windowSize;			// Size of the window, that will be fetched

		private bool fullyLoaded = false;				// is all data fetched
		private object rowLock = new object();  // Synchronisation
		private int loadedCount = 0;						// Current number of items in the array
		private int visibleCount = 0;          // Current number of items for the ui
		private dynamic[] rows = emptyList;    // Currently loaded lines

		private Action<int, int> procFetchNextWindow;	// Delegate, for the background fetch
		private int currentFetchTo = 0;               // position to read
		private FetchFollow currentFetchFollow = FetchFollow.None; // type of background fetch
		private IAsyncResult currentFetchResult = null; // Current fetch

		#region -- Ctor/Dtor --------------------------------------------------------------

		/// <summary>Creates a list, that will fetch the data in pages.</summary>
		/// <param name="windowSize"></param>
		public PpsDataList(PpsEnvironment environment, int windowSize = 50)
		{
			if (dataSource != null)
				throw new ArgumentNullException("dataSource");
			if (windowSize < 1)
				throw new IndexOutOfRangeException("windowSize darf nicht unter 1 liegen.");

			this.environment = environment;
			this.windowSize = windowSize;

			procFetchNextWindow = FetchNextWindow;
		} // ctor

		#endregion

		#region -- Clear ------------------------------------------------------------------

		public void Clear()
		{
			ClearAsync().Wait();
		} // proc Clear

		public async Task Reset(Uri dataSource)
		{
			// clear current list
			await ClearAsync();

			this.dataSource = dataSource;

			// Load first page
			BeginFetchNextWindow();
		} // proc Reset

		/// <summary>Lädt die Liste neu</summary>
		public async Task ClearAsync()
		{
			// Führe das aktuelle Fetch zu einem Ende
			if (currentFetchResult != null)
				await Task.Run(new Action(EndFetchNextWindow));

			// Lösche den Inhalt der Liste
			await Environment.Dispatcher.InvokeAsync(
				() =>
				{
					lock (rowLock)
					{
						visibleCount = 0;
						loadedCount = 0;
						IsLoaded = false;
						rows = emptyList;
						OnCollectionChanged();
					}
				});

			// Starte das Fetchen der Daten erneut
			BeginFetchNextWindow();
		} // proc ClearAsync

		#endregion

		#region -- FetchNextWindow --------------------------------------------------------

		private void BeginFetchNextWindow(bool all = false)
		{
			bool lRaisePropertyChanged = false;

			if (fullyLoaded) // wurden schon alle Daten geladen
				return;

			lock (this)
			{
				if (currentFetchResult == null)
				{

					int fetchCount = all ? -1 : windowSize;
					currentFetchFollow = FetchFollow.None;
					currentFetchTo = all ? -1 : visibleCount + fetchCount;

					currentFetchResult = procFetchNextWindow.BeginInvoke(visibleCount, fetchCount, EndFetchNextWindow, null);
					lRaisePropertyChanged = true;
				}
				else if (all)
				{
					if (currentFetchTo != -1)
						currentFetchFollow = FetchFollow.NextAll;
				}
				else if (currentFetchFollow == FetchFollow.None)
				{
					if (visibleCount < currentFetchTo - 1)
						currentFetchFollow = FetchFollow.NextWindow;
				}
			}

			if (lRaisePropertyChanged)
				OnPropertyChanged("IsLoading");
		} // proc BeginFetchNextWindow

		private void EndFetchNextWindow(IAsyncResult ar)
		{
			lock (this)
			{
				bool lSilent = ar != currentFetchResult;
				try
				{
					procFetchNextWindow.EndInvoke(ar);
				}
				catch (Exception e)
				{
					Environment.ShowException(ExceptionShowFlags.None, e);
					throw;
				}
				finally
				{
					if (!lSilent)
					{
						lock (this)
							currentFetchResult = null;

						switch (currentFetchFollow)
						{
							case FetchFollow.NextAll:
								BeginFetchNextWindow(true);
								break;
							case FetchFollow.NextWindow:
								BeginFetchNextWindow(false);
								break;
						}

						OnPropertyChanged("IsLoading");
					}
				}
			}
		} // EndFetchNextWindow

		private void EndFetchNextWindow()
		{
			if (currentFetchResult != null)
				currentFetchResult.AsyncWaitHandle.WaitOne();
		} // proc EndFetchNextWindow

		private void FetchNextWindow(int startAt, int count)
		{
			DebugPrint("Fetch next: start={0};ende={1}", startAt, count == -1 ? "eof" : count.ToString());

			// Berchne das nächste Fenster
			string sFetchUrl = String.Format(count == -1 ? "{0}&start={1}" : "{0}&start={1}&count={2}", dataSource, startAt, count);

			// Lade die Zeilen
			int iFetchedStarted = visibleCount;
			var web = new BaseWebReqeust(null, Encoding.Default);
			foreach (var c in web.GetReaderAsync(sFetchUrl).Result)
			{
				// Zeilen werden als Tabellen verwaltet
				LuaTable t = new LuaTable();

				// Übertrage die Attribute
				for (int i = 0; i < c.FieldCount; i++)
				{
					if (!c.IsNull(i))
						t[c.GetName(i)] = c[i];
				}

				AppendRow(t);
				if (loadedCount - visibleCount >= windowSize)
					OnCountChanged();
			}

			OnCountChanged();
		} // func FetchNextWindow

		private void AppendRow(LuaTable t)
		{
			lock (rowLock)
			{
				if (loadedCount >= rows.Length) // Vergrößere das Array
				{
					dynamic[] newRows = new dynamic[rows.Length == 0 ? 16 : rows.Length * 2];
					Array.Copy(rows, 0, newRows, 0, rows.Length);
					rows = newRows;
				}
#if LDEBUG
						t.SetMemberValue("__Index", iLoadedCount);
#endif
				rows[loadedCount] = t;
				loadedCount++;
			}
		} // proc AppendRow

		private void OnCountChanged()
		{
			lock (rowLock)
			{
				if (visibleCount - loadedCount == 0)
					return;

				// Benachrichtige die UI, indem VisibleCount neu gesetzt wird
				DebugPrint("Collection added l:{0} --> v:{1}", loadedCount, visibleCount);

				Environment.Dispatcher.BeginInvoke(
					new Action(() =>
					{
						if (CollectionChanged != null)
						{
							while (true)
							{
								lock (rowLock)
								{
									if (visibleCount < loadedCount)
										CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, rows[visibleCount++]));
									else
										break;
								}
							}
						}
					})
				);
			}
		} // proc OnCountChanged

		private dynamic FetchDetailData(dynamic item)
		{
			if (item.isLoading == null) // müssen die Detaildaten noch geladen werden
			{
				item.isLoading = true;
#if LDEBUG
						//DebugPrint("Fetch detail[{0}]: {1} [{2}]", (object)item.__Index, (object)item.OBJKID, (object)item.OBJKTYP);
#endif
				//dynamic cls = context.Data.Classes[(string)item.OBJKTYP];
				//if (cls != null)
				//	PushFetchDetail(this, item, cls);
			}
			return item;
		} // proc FetchDetailData

		//private void UpdateTableWithRecord(PpsDataClass cls, dynamic item, IEnumerable<KeyValuePair<string, object>> r)
		//{
		//	// Aktualisiere die Werte
		//	if (r != null)
		//	{
		//		foreach (var v in r)
		//			item[v.Key] = v.Value;
		//	}
		//	item.isLoading = false;

		//	// Führe die Methode zur Aufbereitung der Daten auf
		//} // proc UpdateTableWithRecord

		#endregion

		private void OnCollectionChanged()
		{
			DebugPrint("Collection reset.");

			Environment.Dispatcher.Invoke(
				() =>
				{
					if (CollectionChanged != null)
						CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
				}
			);
		} // proc OnCollectionChanged

		private void OnPropertyChanged([CallerMemberName] string sPropertyName = null)
		{
			DebugPrint("{0} changed.", sPropertyName);

			Environment.Dispatcher.BeginInvoke(new Action(
				() =>
				{
					if (PropertyChanged != null)
						PropertyChanged(this, new PropertyChangedEventArgs(sPropertyName));
				})
			);
		} // prop OnPropertyChanged

		bool IList.Contains(object value)
		{
			lock (rowLock)
				return Array.IndexOf(rows, value, 0, visibleCount) >= 0;
		} // func IList.Contains

		int IList.IndexOf(object value)
		{
			lock (rowLock)
				return Array.IndexOf(rows, value, 0, visibleCount);
		} // func IList<dynamic>.IndexOf

		void ICollection.CopyTo(Array array, int arrayIndex)
		{
			lock (rowLock)
				Array.Copy(rows, 0, array, arrayIndex, array.Length);
		} // func ICollection<dynamic>.CopyTo

		public IEnumerator GetEnumerator()
		{
			DebugPrint("GetEnumerator");

			if (visibleCount > 0)
			{
				int i = 0;
				while (true)
				{
					// warte auf das Ende eines Fetch-Prozesses
					EndFetchNextWindow(); // warte Synchron

					lock (rowLock)
					{
						// Prüfe auf das Ende
						if (i >= visibleCount)
							break;

						// gib das aktuelle Element zurück
						yield return this[i++];
					}
				}
			}
		} // func IEnumerable<dynamic>.GetEnumerator

		int IList.Add(object item) { throw new NotSupportedException(); }
		void IList.Insert(int index, object item) { throw new NotSupportedException(); }
		void IList.Remove(object value) { throw new NotSupportedException(); }
		void IList.RemoveAt(int index) { throw new NotSupportedException(); }
		bool IList.IsReadOnly => true;
		bool IList.IsFixedSize => false;

		bool ICollection.IsSynchronized => true;
		object ICollection.SyncRoot => rowLock;

		public PpsEnvironment Environment => environment;

		/// <summary>Zugriff auf das angegebene Element</summary>
		/// <param name="index"></param>
		/// <returns></returns>
		public object this[int index]
		{
			get
			{
				lock (rowLock)
				{
					if (index >= 0 && index < visibleCount)
					{
						if (index > loadedCount - 3)
							BeginFetchNextWindow(); // Starte ein nachladen von Daten

						return FetchDetailData(rows[index]);
					}
					else
						throw new IndexOutOfRangeException();
				}
			}
			set { throw new NotSupportedException(); }
		} // prop Index

		/// <summary>Anzahl der Elemente in der Liste</summary>
		public int Count { get { return visibleCount; } }

		/// <summary>Wird die Liste gerade geladen</summary>
		public bool IsLoading
		{
			get
			{
				lock (this)
					return currentFetchResult != null;
			}
		} // prop IsLoading

		/// <summary>Wurden alle Zeilen geladen.</summary>
		public bool IsLoaded
		{
			get { return fullyLoaded; }
			private set
			{
				if (fullyLoaded != value)
				{
					fullyLoaded = value;
					OnPropertyChanged();
				}
			}
		} // prop IsLoaded

		// -- Static --------------------------------------------------------------

		#region -- class FetchItem --------------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class FetchItem
		{
			private WeakReference<PpsDataList> pOwner;
			private WeakReference<object> pItem;
			private WeakReference<object> pCls;

			public FetchItem(PpsDataList owner, object item, object cls)
			{
				this.pOwner = new WeakReference<PpsDataList>(owner);
				this.pItem = new WeakReference<object>(item);
				this.pCls = new WeakReference<object>(cls);
			} // ctor

			public void Update()
			{
				PpsDataList owner;
				dynamic item;
				dynamic cls;
				if (pOwner.TryGetTarget(out owner) && pItem.TryGetTarget(out item) && pCls.TryGetTarget(out cls))
				{
					try
					{
						//Stopwatch sw = Stopwatch.StartNew();
						var data = (IEnumerable<KeyValuePair<string, object>>)cls.UpdateRow(item);
						//owner.context.Sync.Post(s => owner.UpdateTableWithRecord(cls, item, data), null);
						//Debug.Print("ID = {0} ==> {1} ms", (object)item.OBJKID, sw.ElapsedMilliseconds);
					}
					catch (Exception e)
					{
						throw e;
						//owner.context.ShowException(ExceptionShowFlags.Background, e);
					}
				}
			} // proc Update
		} // class FetchItem

		#endregion

		private static readonly LuaTable[] emptyList = new LuaTable[0];

		private static Thread threadFetchDetail;
		private static ManualResetEventSlim fetchDetailRun = new ManualResetEventSlim(true);
		private static Stack<FetchItem> fetchDetailList = new Stack<FetchItem>();

		static PpsDataList()
		{
			threadFetchDetail = new Thread(FetchDetailBackground);
			threadFetchDetail.IsBackground = true;
			threadFetchDetail.Name = "List DetailFetch";
			threadFetchDetail.Start();
		} // ctor

		private static void FetchDetailBackground()
		{
			while (true)
			{
				FetchItem item;
				lock (fetchDetailList)
				{
					if (fetchDetailList.Count == 0)
					{
						item = null;
						fetchDetailRun.Reset();
					}
					else
						item = fetchDetailList.Pop();
				}

				if (item != null)
					item.Update();

				// Warte auf das neu Item
				fetchDetailRun.Wait();
			}
		} // proc FetchDetailBackground

		private static void PushFetchDetail(PpsDataList owner, object item, object cls)
		{
			lock (fetchDetailList)
			{
				fetchDetailList.Push(new FetchItem(owner, item, cls));
				fetchDetailRun.Set();
			}
		} // proc PushFetchDetail

		[Conditional("LDEBUG")]
		private static void DebugPrint(string sMessage, params object[] args)
		{
			Trace.TraceInformation(String.Format("[PpsDataList] " + sMessage, args));
		} // proc DebugPrint
	} // class PpsDataList
}
