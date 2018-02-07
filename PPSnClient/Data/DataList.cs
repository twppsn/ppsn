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
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Neo.IronLua;
using TecWare.DE.Data;

namespace TecWare.PPSn.Data
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Fetches data from a source page by page as a bindable collection.
	/// </summary>
	public sealed class PpsDataList : IList, INotifyCollectionChanged, INotifyPropertyChanged
	{
		#region -- class LuaItemTable -----------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class LuaItemTable : LuaTable
		{
			private readonly IDataRow row;

			public LuaItemTable(IDataRow row)
			{
				if (row.IsDataOwner) // use reference for special items
				{
					this.row = row;

					// todo: memleak?
					var t = this.row as INotifyPropertyChanged;
					if (t != null)
						t.PropertyChanged += (sender, e) => OnPropertyChanged(e.PropertyName);
				}
				else
				{
					// copy the columns
					var i = 0;
					foreach (var c in row.Columns)
					{
						if (c != null)
							SetMemberValue(c.Name, row[i], rawSet: true);
						i++;
					}
				}
			} // ctor

			protected override object OnIndex(object key)
			{
				var r = base.OnIndex(key);
				if (r == null && row != null && key is string)
					r = row[(string)key, false];
				return r;
			} // func OnIndex
		} // class LuaItemTable

		#endregion

		#region -- enum FetchFollow -------------------------------------------------------

		private enum FetchFollow
		{
			None,
			NextWindow,
			NextAll,
			Cancel
		} // enum FetchFollow

		#endregion

		/// <summary>Notifies if the items of the collection are changed.</summary>
		public event NotifyCollectionChangedEventHandler CollectionChanged;
		/// <summary>Notifies if a property of the datalist is changed.</summary>
		public event PropertyChangedEventHandler PropertyChanged;

		private readonly IPpsShell shell;            // shell for the data list, to retrieve data and synchronize the events
		private PpsShellGetList dataSource = PpsShellGetList.Empty; // List, that will be fetched
		private int windowSize;             // Size of the window, that will be fetched

		private bool fullyLoaded = false;       // is all data fetched
		private object rowLock = new object();  // Synchronisation
		private int loadedCount = 0;            // Current number of items in the array
		private int visibleCount = 0;           // Current number of items for the ui
		private dynamic[] rows = emptyList;     // Currently loaded lines

		private Action<int, int> procFetchNextWindow; // Delegate, for the background fetch of a page
		private int currentFetchTo = 0;               // position to read
		private readonly object currentFetchLock = new object();
		private FetchFollow currentFetchFollow = FetchFollow.None; // type of background fetch
		private IAsyncResult currentFetchResult = null; // Current fetch

		#region -- Ctor/Dtor --------------------------------------------------------------

		/// <summary>Creates a list, that will fetch the data in pages.</summary>
		/// <param name="context"></param>
		/// <param name="windowSize"></param>
		public PpsDataList(IPpsShell context, int windowSize = 50)
		{
			if (context == null)
				throw new ArgumentNullException("context");
			if (windowSize < 1)
				throw new IndexOutOfRangeException("windowSize lower than 1 is not allowed.");

			this.shell = context;
			this.windowSize = windowSize;

			procFetchNextWindow = FetchNextWindow;
		} // ctor

		#endregion

		#region -- Reset, Clear -----------------------------------------------------------

		/// <summary>Resets the datasource.</summary>
		/// <param name="dataSource"></param>
		/// <returns></returns>
		public async Task Reset(PpsShellGetList dataSource)
		{
			// copy the data source
			var tmpDataSource = new PpsShellGetList(dataSource);

			// clear current list
			await ClearAsync();

			// set the new data source
			this.dataSource = tmpDataSource;

			// Load first page
			if (!tmpDataSource.IsEmpty)
				BeginFetchNextWindow();
		} // proc Reset

		/// <summary>Clears the current content of the list.</summary>
		public void Clear()
		{
			Task.Run(async () => await ClearAsync()).Wait();
		} // proc Clear

		/// <summary>Clears the current content of the list.</summary>
		public async Task ClearAsync()
		{
			await Task.Run(new Action(() => EndFetchNextWindow(true)));

			// Clear list synchron to the ui
			await shell.InvokeAsync(
				() =>
				{
					lock (rowLock)
					{
						visibleCount = 0;
						loadedCount = 0;
						IsLoaded = false;
						rows = emptyList;

						DebugPrint("Collection reset.");
						CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
					}
				});
		} // proc ClearAsync

		#endregion

		#region -- FetchNextWindow --------------------------------------------------------

		/// <summary>All data should be fetched</summary>
		public void ReadToEnd()
		{
			BeginFetchNextWindow(true);
		} // proc ReadToEnd

		private void BeginFetchNextWindow(bool all = false)
		{
			var raisePropertyChanged = false;

			if (fullyLoaded) // wurden schon alle Daten geladen
				return;
			
			lock (currentFetchLock)
			{
				if (currentFetchFollow == FetchFollow.Cancel)
					return;

				if (currentFetchResult == null)
				{
					var fetchCount = all ? -1 : windowSize;
					currentFetchFollow = FetchFollow.None;
					currentFetchTo = all ? -1 : visibleCount + fetchCount;

					currentFetchResult = procFetchNextWindow.BeginInvoke(visibleCount, fetchCount, EndFetchNextWindow, null);
					raisePropertyChanged = true;
				}
				else if (all)
				{
					if (currentFetchTo != -1)
						currentFetchFollow = FetchFollow.NextAll;
				}
				else if (currentFetchFollow == FetchFollow.None)
				{
					if (visibleCount >= currentFetchTo - 3) // fetch is done, but we are at the end of the list
						currentFetchFollow = FetchFollow.NextWindow;
				}
			}

			if (raisePropertyChanged)
				OnPropertyChanged(nameof(IsLoading));
		} // proc BeginFetchNextWindow

		private void EndFetchNextWindow(IAsyncResult ar)
		{
			lock (currentFetchLock)
			{
				var doSilent = ar != currentFetchResult;
				try
				{
					procFetchNextWindow.EndInvoke(ar);
				}
				catch (Exception e)
				{
					shell.ShowExceptionAsync(ExceptionShowFlags.None, e).Wait();
				}
				finally
				{
					if (!doSilent)
					{
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

						OnPropertyChanged(nameof(IsLoading));
					}
				}
			}
		} // EndFetchNextWindow

		private void EndFetchNextWindow(bool cancel)
		{
			lock (currentFetchLock)
			{
				if (cancel)
					currentFetchFollow = FetchFollow.Cancel;

				if (currentFetchResult != null)
					currentFetchResult.AsyncWaitHandle.WaitOne();

				if (cancel)
					currentFetchFollow = FetchFollow.None;
			}
		} // proc EndFetchNextWindow

		private void FetchNextWindow(int startAt, int count)
		{
			if (dataSource.IsEmpty)
				return;

#if DEBUG
			DebugPrint("Fetch next: start={0};ende={1}", startAt, count == -1 ? "eof" : count.ToString());
#endif

			// Set the fetch window
			var fetchSource = new PpsShellGetList(dataSource);
			fetchSource.Start = startAt;
			fetchSource.Count = count;

			// Fetch the lines
			var currentIndex = loadedCount;
			var fetchedRows = 0;
			foreach (var row in shell.GetViewData(fetchSource))
			{
				// All lines are simple lua tables
				var t = new LuaItemTable(row);

				// todo: are more data available
				//if (shell.IsOnline)
				//	t["isLoading"] = false;


				// update the data
				if (currentFetchFollow == FetchFollow.Cancel)
					break;
				AppendRow(currentIndex++, t);
				fetchedRows++;

				// update the view in page's 
				if (loadedCount - visibleCount >= windowSize)
					OnCountChanged();
			}

			// is all fetched
			IsLoaded = fetchedRows < count;

			OnCountChanged();
		} // func FetchNextWindow

		private void AppendRow(int index, LuaTable t)
		{
			lock (rowLock)
			{
				if (index >= rows.Length) // resize the array
				{
					dynamic[] newRows = new dynamic[rows.Length == 0 ? 16 : rows.Length * 2];
					Array.Copy(rows, 0, newRows, 0, rows.Length);
					rows = newRows;
				}

#if LDEBUG
				t.SetMemberValue("__Index", loadedCount);
#endif
				rows[index] = t;
				currentFetchResult.AsyncWaitHandle.WaitOne(10);
				if (index == loadedCount)
					loadedCount++;
			}
		} // proc AppendRow

		private void OnCountChanged()
		{
			lock (rowLock)
			{
				if (visibleCount - loadedCount == 0)
					return;

				// notify ui, that visible count is changed
#if LDEBUG
				DebugPrint("Collection added l:{0} --> v:{1}", loadedCount, visibleCount);
#endif

				shell.BeginInvoke(
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
			if (item.isLoading == null) // all data loaded
			{
				item.isLoading = true;
#if LDEBUG
				DebugPrint("Fetch detail[{0}]: {1} [{2}]", (object)item.__Index, (object)item.OBJKID, (object)item.OBJKTYP);
#endif
				PushFetchDetail(this, item);
			}
			return item;
		} // proc FetchDetailData

		private void UpdateTableWithRow(dynamic item, IDataRow r)
		{
			// Update the table, and finish loading
			if (r != null)
			{
				var i = 0;
				foreach (var c in r.Columns)
				{
					if (c != null)
						item[c.Name] = r[i];
					i++;
				}
			}
			item.isLoading = false;

			// todo: execute a extented function
		} // proc UpdateTableWithRow

		#endregion

		#region -- IList, OnPropertyChanged -----------------------------------------------

		private void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
#if LDEBUG
			DebugPrint("{0} changed.", sPropertyName);
#endif

			shell.BeginInvoke(new Action(
				() =>
				{
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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

		/// <summary></summary>
		/// <returns></returns>
		public IEnumerator GetEnumerator()
		{
			DebugPrint("GetEnumerator");

			if (visibleCount > 0)
			{
				int i = 0;
				while (true)
				{
					// warte auf das Ende eines Fetch-Prozesses
					EndFetchNextWindow(false); // warte Synchron

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

		#endregion

		/// <summary></summary>
		public IPpsShell Shell => shell;

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
		} // prop this

		/// <summary>Anzahl der Elemente in der Liste</summary>
		public int Count => visibleCount;

		/// <summary>Wird die Liste gerade geladen</summary>
		public bool IsLoading
		{
			get
			{
				lock (currentFetchLock)
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
			private readonly WeakReference<PpsDataList> pOwner;
			private readonly WeakReference<object> pItem;

			public FetchItem(PpsDataList owner, object item)
			{
				this.pOwner = new WeakReference<PpsDataList>(owner);
				this.pItem = new WeakReference<object>(item);
			} // ctor

			public void Update()
			{
				PpsDataList owner;
				dynamic item;
				if (pOwner.TryGetTarget(out owner) && pItem.TryGetTarget(out item))
				{
					try
					{
#if LDEBUG
						var sw = Stopwatch.StartNew();
#endif

						//long objectId = item.OBJKID;
						//string objectTyp = item.OBJKTYP;
						//var r = owner.Shell.GetDetailedData(objectId, objectTyp);
						//if (r != null)
						//	owner.Shell.BeginInvoke(() => owner.UpdateTableWithRecord(item, r));
#if LDEBUG
						Debug.WriteLine("ID = {0} ==> {1} ms", objectId, sw.ElapsedMilliseconds);
#endif
					}
					catch (Exception e)
					{
						owner.Shell.ShowExceptionAsync(ExceptionShowFlags.Background, e).Wait();
					}
				}
			} // proc Update
		} // class FetchItem

		#endregion

		private static readonly LuaTable[] emptyList = new LuaTable[0];

		private static Action actionFetchDetail;
		private static ManualResetEventSlim fetchDetailRun = new ManualResetEventSlim(true);
		private static Stack<FetchItem> fetchDetailList = new Stack<FetchItem>();

		static PpsDataList()
		{
			actionFetchDetail = FetchDetailBackground;
			actionFetchDetail.BeginInvoke(null, null);
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

		private static void PushFetchDetail(PpsDataList owner, object item)
		{
			lock (fetchDetailList)
			{
				fetchDetailList.Push(new FetchItem(owner, item));
				fetchDetailRun.Set();
			}
		} // proc PushFetchDetail

		[Conditional("DEBUG")]
		private static void DebugPrint(string message, params object[] args)
		{
			Debug.WriteLine(String.Format("[PpsDataList] " + message, args));
		} // proc DebugPrint
	} // class PpsDataList
}
