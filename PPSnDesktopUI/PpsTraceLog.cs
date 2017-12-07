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
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TecWare.DE.Stuff;

namespace TecWare.PPSn
{
	#region -- enum PpsTraceItemType ----------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public enum PpsTraceItemType
	{
		Debug = 0,
		Information,
		Warning,
		Fail,
		Exception
	} // enum PpsTraceItemType

	#endregion

	#region -- class PpsTraceItemBase ---------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public abstract class PpsTraceItemBase
	{
		private DateTime stamp;

		public PpsTraceItemBase()
		{
			this.stamp = DateTime.Now;
		} // ctor

		public override int GetHashCode()
		{
			return Type.GetHashCode() ^ Message.GetHashCode();
		} // func GetHashCode

		public override bool Equals(object obj)
		{
			var item = obj as PpsTraceItemBase;
			if (item == null)
				return false;

			return item.Type == this.Type && item.Message == this.Message;
		} // func Equals

		public override string ToString()
		{
			return String.Format("{0}: {1}", Type, Message);
		} // func ToString

		public DateTime Stamp { get { return stamp; } }
		public abstract string Message { get; }

		public abstract PpsTraceItemType Type { get; }
	} // class PpsTraceItem

	#endregion

	#region -- class PpsExceptionItem ---------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsExceptionItem : PpsTraceItemBase
	{
		private readonly string message;
		private readonly Exception exception;
		private readonly PpsTraceItemType traceItemType;

		public PpsExceptionItem(string alternativeMessage, Exception exception, PpsTraceItemType traceItemType = PpsTraceItemType.Exception)
		{
			if (!String.IsNullOrEmpty(alternativeMessage))
				message = alternativeMessage;
			else
				message = exception.Message;

			this.exception = exception;
			this.traceItemType = traceItemType;
		} // ctor

		public override string Message => message;
		public Exception Exception => exception;
		public override PpsTraceItemType Type => traceItemType;
	} // class PpsExceptionItem

	#endregion

	#region -- class PpsTraceItem -------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsTraceItem : PpsTraceItemBase
	{
		private TraceEventType eventType;
		private TraceEventCache eventCache;

		private int id;
		private string source;
		private string message;

		internal PpsTraceItem(TraceEventType eventType, TraceEventCache eventCache, string source, int id, string message)
		{
			this.eventType = eventType;
			this.eventCache = eventCache;
			this.id = id;
			this.source = source;
			this.message = message;
		} // ctor

		public override int GetHashCode()
		{
			return eventType.GetHashCode() ^ message.GetHashCode() ^ id.GetHashCode() ^ source.GetHashCode();
		} // func GetHashCode

		public override bool Equals(object obj)
		{
			var item = obj as PpsTraceItem;
			if (item == null)
				return false;
			return item.eventType == this.eventType && item.message == this.message && item.id == this.id && item.source == this.source;
		} // func Equals

		public int Id { get { return id; } }
		public string Source { get { return source; } }
		public TraceEventType EventType { get { return eventType; } }
		public TraceEventCache EventCache { get { return eventCache; } }

		public override string Message { get { return message; } }

		public override PpsTraceItemType Type
		{
			get
			{
				switch (eventType)
				{
					case TraceEventType.Critical:
					case TraceEventType.Error:
						return PpsTraceItemType.Fail;
					case TraceEventType.Warning:
						return PpsTraceItemType.Warning;
					case TraceEventType.Information:
					case TraceEventType.Resume:
					case TraceEventType.Start:
					case TraceEventType.Stop:
					case TraceEventType.Suspend:
						return PpsTraceItemType.Information;
					case TraceEventType.Transfer:
					case TraceEventType.Verbose:
						return PpsTraceItemType.Debug;
					default:
						return PpsTraceItemType.Debug;
				}
			} // prop type
		} // prop Type
	} // class PpsTraceItem

	#endregion

	#region -- class PpsTextItem --------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsTextItem : PpsTraceItemBase
	{
		private PpsTraceItemType type;
		private string message;

		internal PpsTextItem(PpsTraceItemType type, string message)
		{
			this.type = type;
			this.message = message;
		} // ctor

		public override string Message { get { return message; } }
		public override PpsTraceItemType Type { get { return type; } }
	} // class PpsTextItem

	#endregion

	#region -- class PpsTraceProgress ---------------------------------------------------

	public sealed class PpsTraceProgress : IProgress<string>, IDisposable
	{
		private readonly PpsTraceLog trace;

		internal PpsTraceProgress(PpsTraceLog trace)
		{
			this.trace = trace;
		} // ctor

		public void Dispose() { }
		public void Report(string progressText) { }

		public void Except(Exception e)
		{
			trace.AppendException(e);
		} // proc Except
	} // class PpsTraceProgres

	#endregion

	#region -- class PpsTraceLog --------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Collection for all collected events in the application. It connects 
	/// to the trace listener and catches exceptions.</summary>
	public sealed class PpsTraceLog : IList, INotifyCollectionChanged, INotifyPropertyChanged, IDisposable
	{
		private const int MaxTraceItems = 1 << 19;

		#region -- class PpsTraceListener -------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private class PpsTraceListener : TraceListener
		{
			private PpsTraceLog owner;
			private Dictionary<int, StringBuilder> currentLines = new Dictionary<int, StringBuilder>();

			public PpsTraceListener(PpsTraceLog owner)
			{
				this.owner = owner;
			} // ctor

			public override void Fail(string message, string detailMessage)
			{
				if (message == null)
				{
					message = detailMessage;
					detailMessage = null;
				}
				if (detailMessage != null)
					message = message + Environment.NewLine + detailMessage;

				owner.AppendItem(new PpsTextItem(PpsTraceItemType.Fail, message));
			} // proc Fail

			private static string FormatData(object data)
			{
				return Convert.ToString(data, CultureInfo.InvariantCulture);
			} // func FormatData

			public override void TraceData(TraceEventCache eventCache, string source, TraceEventType eventType, int id, object data)
			{
				TraceEvent(eventCache, source, eventType, id, data == null ? String.Empty : FormatData(data));
			} // func TraceData

			public override void TraceData(TraceEventCache eventCache, string source, TraceEventType eventType, int id, params object[] data)
			{
				var message = new StringBuilder();
				if (data != null)
					for (int i = 0; i < data.Length; i++)
						message.Append('[').Append(i).Append("] ").AppendLine(FormatData(data[i]));

				TraceEvent(eventCache, source, eventType, id, message.ToString());
			} // proc TraceData

			public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string message)
			{
				owner.AppendItem(new PpsTraceItem(eventType, eventCache, source, id, message));
			} // proc TraceEvent

			public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string format, params object[] args)
			{
				if (args != null && args.Length > 0)
					format = String.Format(format, args);
				this.TraceEvent(eventCache, source, eventType, id, format);
			} // proc TraceEvent

			public override void Write(string message)
			{
				if (String.IsNullOrEmpty(message))
					return;

				var threadId = Thread.CurrentThread.ManagedThreadId;
				StringBuilder currentLine;
				lock (currentLines)
				{
					if (!currentLines.TryGetValue(threadId, out currentLine))
						currentLines[threadId] = currentLine = new StringBuilder();
				}


				currentLine.Append(message);
				if (message.Length > 1 && message[message.Length - 1] == '\r' || message[message.Length - 1] == '\n')
				{
					WriteLine(currentLine.ToString().TrimEnd('\n', '\r'));
					lock (currentLines)
						currentLines.Remove(threadId);
				}
			} // proc Write

			public override void WriteLine(string message)
			{
				owner.AppendItem(new PpsTextItem(PpsTraceItemType.Debug, message));
			} // proc WriteLine
		} // class PpsTraceListener

		#endregion

		public event NotifyCollectionChangedEventHandler CollectionChanged;
		public event PropertyChangedEventHandler PropertyChanged;

		private PpsTraceListener listener;
		private List<PpsTraceItemBase> items = new List<PpsTraceItemBase>();
		private PpsTraceItemBase lastTrace = null;
		private Timer updateTimer;

		#region -- Ctor/Dtor --------------------------------------------------------------

		public PpsTraceLog()
		{
			Trace.Listeners.Add(listener = new PpsTraceListener(this));
			updateTimer = new Timer((a) => UpdateLastTrace(), null, 0, 2000);
		} // ctor

		~PpsTraceLog()
		{
			Dispose(false);
		} // dtor

		public void Dispose()
		{
			GC.SuppressFinalize(this);
			Dispose(true);
		} // proc Dispose

		private void Dispose(bool disposing)
		{
			Trace.Listeners.Remove(listener);
			if (disposing)
			{
				Clear();
			}
		} // proc Dispose

		#endregion

		#region -- Traces Progress ------------------------------------------------------

		public PpsTraceProgress TraceProgress()
			=> new PpsTraceProgress(this);

		#endregion

		#region -- AppendItem -------------------------------------------------------------

		private int AppendItem(PpsTraceItemBase item)
		{
			var index = -1;
			var resetList = false;
			var lastTraceChanged = false;
			object itemRemoved = null;

			// change list
			lock (items)
			{
				while (items.Count > MaxTraceItems)
				{
					if (itemRemoved == null)
						itemRemoved = items[0];
					else
						resetList = true;
					items.RemoveAt(0);
				}

				items.Add(item);
				index = items.Count - 1;

				if (item.Type == PpsTraceItemType.Exception || item.Type == PpsTraceItemType.Fail || item.Type == PpsTraceItemType.Warning)
					lastTraceChanged = SetLastTrace(item);
				else
					lastTraceChanged = UpdateLastTrace();
			}

			// update view
			if (resetList)
				OnCollectionReset();
			else
			{
				if (itemRemoved != null)
					OnCollectionRemoved(itemRemoved, 0);
				OnCollectionAdded(item, items.Count - 1);
			}
			OnPropertyChanged(nameof(Count));
			if (lastTraceChanged)
				OnPropertyChanged(nameof(LastTrace));

			return index;
		} // proc AppendItem

		public void Clear()
		{
			lock (items)
			{
				lastTrace = null;
				items.Clear();
			}

			OnCollectionReset();
			OnPropertyChanged("Count");
			OnPropertyChanged("LastTrace");
		} // proc Clear

		public void AppendText(PpsTraceItemType type, string message)
		{
			AppendItem(new PpsTextItem(type, message));
		} // proc AppendText

		public void AppendException(Exception exception, string alternativeMessage = null, PpsTraceItemType traceItemType = PpsTraceItemType.Exception)
			=> AppendItem(new PpsExceptionItem(alternativeMessage, exception, traceItemType));

		#endregion

		#region -- Last Trace Item --------------------------------------------------------

		public void ClearLastTrace()
		{
			LastTrace = null;
		} // proc ClearLastTrace

		private bool IsLastTraceNear()
		{
			return lastTrace != null && (DateTime.Now - lastTrace.Stamp).TotalMilliseconds < 5000;
		} // func IsLastTraceNear

		private bool SetLastTrace(PpsTraceItemBase item)
		{
			if (IsLastTraceNear() && // the current trace event is pretty new
				item.Type < lastTrace.Type) // and more important
				return false;
			
			lastTrace = item;
			return true;
		} // proc SetLastTrace

		private bool UpdateLastTrace()
		{
			if (!IsLastTraceNear())
			{
				lastTrace = null;
				OnPropertyChanged(nameof(LastTrace));
				return true;
			}
			else
				return false;
		} // proc UpdateLastTrace

		#endregion

		#region -- Event Handling ---------------------------------------------------------

		private void OnPropertyChanged(string propertyName)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		private void OnCollectionAdded(object item, int index)
			=> CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index));

		private void OnCollectionRemoved(object item, int index)
			=> CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item, index));

		private void OnCollectionReset()
			=> CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

		#endregion

		#region -- IList Member -----------------------------------------------------------

		int IList.Add(object value) { return AppendItem((PpsTraceItemBase)value); }
		void IList.Insert(int index, object value) { throw new NotSupportedException(); }
		void IList.Remove(object value) { throw new NotSupportedException(); }
		void IList.RemoveAt(int index) { throw new NotSupportedException(); }
		bool IList.Contains(object value) { return items.Contains((PpsTraceItemBase)value); }
		int IList.IndexOf(object value) { return items.IndexOf((PpsTraceItemBase)value); }

		bool IList.IsFixedSize { get { return false; } }
		bool IList.IsReadOnly { get { return true; } }

		object IList.this[int index] { get { return items[index]; } set { throw new NotSupportedException(); } }

		#endregion

		#region -- ICollection Member -----------------------------------------------------

		void ICollection.CopyTo(Array array, int index)
		{
			lock (items)
				((ICollection)items).CopyTo(array, index);
		} // proc ICollection.CopyTo

		bool ICollection.IsSynchronized { get { return true; } }
		public object SyncRoot { get { return items; } }

		#endregion

		#region -- IEnumerable Member -----------------------------------------------------

		IEnumerator IEnumerable.GetEnumerator() { return items.GetEnumerator(); }

		#endregion

		/// <summary>Currently, catched events.</summary>
		public int Count { get { lock (items) return items.Count; } }
		/// <summary>The last catched trace event.</summary>
		public PpsTraceItemBase LastTrace
		{
			get { return lastTrace; }
			private set
			{
				if (lastTrace != value)
				{
					lock (items)
						lastTrace = value;

					OnPropertyChanged("LastTrace");
				}
			}
		} // prop LastTrace
	} // class PpsTraceLog

	#endregion
}
