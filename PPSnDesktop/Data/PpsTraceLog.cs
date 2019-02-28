﻿#region -- copyright --
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
using System.Text;
using System.Threading;
using TecWare.DE.Stuff;

namespace TecWare.PPSn.Data
{
	#region -- class PpsTraceItemBase -------------------------------------------------

	/// <summary></summary>
	public abstract class PpsTraceItemBase
	{
		public PpsTraceItemBase()
		{
		} // ctor

		public override int GetHashCode()
			=> Type.GetHashCode() ^ Message.GetHashCode();

		public override bool Equals(object obj)
			=> obj is PpsTraceItemBase item && (item.Type == Type && item.Message == Message);

		public override string ToString()
			=> $"{Type}: {Message}";

		public DateTime Stamp { get; } = DateTime.Now;
		public abstract string Message { get; }

		public abstract PpsLogType Type { get; }
	} // class PpsTraceItem

	#endregion

	#region -- class PpsExceptionItem -------------------------------------------------

	/// <summary></summary>
	public sealed class PpsExceptionItem : PpsTraceItemBase
	{
		private readonly string message;
		private readonly Exception exception;
		private readonly PpsLogType type;

		public PpsExceptionItem(string alternativeMessage, Exception exception, PpsLogType type = PpsLogType.Exception)
		{
			message = String.IsNullOrEmpty(alternativeMessage) ? exception.Message : alternativeMessage;

			this.exception = exception;
			this.type = type;
		} // ctor

		public override PpsLogType Type => type;
		public override string Message => message;

		public Exception Exception => exception;
	} // class PpsExceptionItem

	#endregion

	#region -- class PpsTraceItem -----------------------------------------------------

	/// <summary></summary>
	public sealed class PpsTraceItem : PpsTraceItemBase
	{
		private readonly TraceEventType traceEventType;
		private readonly TraceEventCache traceEventCache;

		private readonly int id;
		private readonly string source;
		private readonly string message;

		internal PpsTraceItem(TraceEventType traceEventType, TraceEventCache traceEventCache, string source, int id, string message)
		{
			this.traceEventType = traceEventType;
			this.traceEventCache = traceEventCache;
			this.id = id;
			this.source = source;
			this.message = message;
		} // ctor

		public override int GetHashCode()
			=> traceEventType.GetHashCode() ^ message.GetHashCode() ^ id.GetHashCode() ^ source.GetHashCode();

		public override bool Equals(object obj)
			=> obj is PpsTraceItem item && (item.traceEventType == this.traceEventType && item.message == this.message && item.id == this.id && item.source == this.source);

		public int Id => id;
		public string Source => source; 
		public TraceEventType EventType => traceEventType;
		public TraceEventCache EventCache => traceEventCache;

		public override string Message => message; 

		public override PpsLogType Type
		{
			get
			{
				switch (traceEventType)
				{
					case TraceEventType.Critical:
					case TraceEventType.Error:
						return PpsLogType.Exception;
					case TraceEventType.Warning:
						return PpsLogType.Warning;
					case TraceEventType.Information:
					case TraceEventType.Resume:
					case TraceEventType.Start:
					case TraceEventType.Stop:
					case TraceEventType.Suspend:
						return PpsLogType.Information;
					case TraceEventType.Transfer:
					case TraceEventType.Verbose:
						return PpsLogType.Debug;
					default:
						return PpsLogType.Debug;
				}
			} // prop type
		} // prop Type
	} // class PpsTraceItem

	#endregion

	#region -- class PpsTextItem ------------------------------------------------------

	/// <summary></summary>
	public sealed class PpsTextItem : PpsTraceItemBase
	{
		private readonly PpsLogType type;
		private readonly string message;

		internal PpsTextItem(PpsLogType type, string message)
		{
			this.type = type;
			this.message = message;
		} // ctor

		public override string Message => message;
		public override PpsLogType Type => type;
	} // class PpsTextItem

	#endregion

	#region -- class PpsTraceLog ------------------------------------------------------

	/// <summary>Collection for all collected events in the application. It connects 
	/// to the trace listener and catches exceptions.</summary>
	public sealed class PpsTraceLog : IList, INotifyCollectionChanged, INotifyPropertyChanged, IPpsLogger, ILogger, IPpsIdleAction, IDisposable
	{
		/// <summary>Maximal number of trace items</summary>
		public const int MaxTraceItems = 1 << 19;

		#region -- class PpsTraceListener ---------------------------------------------

		/// <summary></summary>
		private class PpsTraceListener : TraceListener
		{
			private readonly PpsTraceLog owner;
			private readonly Dictionary<int, StringBuilder> currentLines = new Dictionary<int, StringBuilder>();

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

				owner.AppendItem(new PpsTextItem(PpsLogType.Exception, message));
			} // proc Fail

			private static string FormatData(object data)
				=> Convert.ToString(data, CultureInfo.InvariantCulture);

			public override void TraceData(TraceEventCache eventCache, string source, TraceEventType eventType, int id, object data)
				=> TraceEvent(eventCache, source, eventType, id, data == null ? String.Empty : FormatData(data));

			public override void TraceData(TraceEventCache eventCache, string source, TraceEventType eventType, int id, params object[] data)
			{
				var message = new StringBuilder();
				if (data != null)
				{
					for (var i = 0; i < data.Length; i++)
						message.Append('[').Append(i).Append("] ").AppendLine(FormatData(data[i]));
				}

				TraceEvent(eventCache, source, eventType, id, message.ToString());
			} // proc TraceData

			public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string message)
				=> owner.AppendItem(new PpsTraceItem(eventType, eventCache, source, id, message));

			public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string format, params object[] args)
			{
				if (args != null && args.Length > 0)
					format = String.Format(format, args);

				TraceEvent(eventCache, source, eventType, id, format);
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
				=> owner.AppendItem(new PpsTextItem(PpsLogType.Debug, message));
		} // class PpsTraceListener

		#endregion
		
		public event NotifyCollectionChangedEventHandler CollectionChanged;
		public event PropertyChangedEventHandler PropertyChanged;

		private readonly PpsEnvironment environment;
		private readonly PpsTraceListener listener;
		private readonly List<PpsTraceItemBase> items = new List<PpsTraceItemBase>();
		private PpsTraceItemBase lastTrace = null;

		#region -- Ctor/Dtor ----------------------------------------------------------

		public PpsTraceLog(PpsEnvironment environment)
		{
			this.environment = environment ?? throw new ArgumentNullException(nameof(environment));

			Trace.Listeners.Add(listener = new PpsTraceListener(this));

			environment.AddIdleAction(this);
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
				environment.RemoveIdleAction(this);
				Clear();
			}
		} // proc Dispose

		bool IPpsIdleAction.OnIdle(int elapsed)
			=> !UpdateLastTrace();

		#endregion

		#region -- AppendItem ---------------------------------------------------------

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

				if (item.Type == PpsLogType.Exception || item.Type == PpsLogType.Fail || item.Type == PpsLogType.Warning)
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
			OnPropertyChanged(nameof(Count));
			OnPropertyChanged(nameof(LastTrace));
		} // proc Clear

		void IPpsLogger.Append(PpsLogType type, string message)
			=> AppendItem(new PpsTextItem(type, message));

		void IPpsLogger.Append(PpsLogType type, Exception exception, string alternativeMessage)
			=> AppendItem(new PpsExceptionItem(alternativeMessage, exception, type));

		void ILogger.LogMsg(LogMsgType type, string message)
			=> AppendItem(new PpsTextItem((PpsLogType)type, message));

		#endregion

		#region -- Last Trace Item ----------------------------------------------------

		public void ClearLastTrace()
		{
			LastTrace = null;
		} // proc ClearLastTrace

		private bool IsLastTraceNear()
			=> lastTrace != null && (DateTime.Now - lastTrace.Stamp).TotalMilliseconds < 5000;

		private bool SetLastTrace(PpsTraceItemBase item)
		{
			if (IsLastTraceNear() // the current trace event is pretty new
				&& item.Type < lastTrace.Type) // and more important
				return false;
			
			lastTrace = item;
			return true;
		} // proc SetLastTrace

		private bool UpdateLastTrace()
		{
			if (!IsLastTraceNear())
			{
				ClearLastTrace();
				return true;
			}
			else
				return false;
		} // proc UpdateLastTrace

		#endregion

		#region -- Event Handling -----------------------------------------------------

		private void OnPropertyChanged(string propertyName)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		private void OnCollectionAdded(object item, int index)
			=> CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index));

		private void OnCollectionRemoved(object item, int index)
			=> CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item, index));

		private void OnCollectionReset()
			=> CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

		#endregion

		#region -- IList Member -------------------------------------------------------

		int IList.Add(object value)
			=> AppendItem((PpsTraceItemBase)value);

		void IList.Insert(int index, object value) 
			=> throw new NotSupportedException();
		void IList.Remove(object value) 
			=> throw new NotSupportedException();
		void IList.RemoveAt(int index) 
			=>throw new NotSupportedException();
		bool IList.Contains(object value)
		{
			lock (items)
				return items.Contains((PpsTraceItemBase)value);
		} // func IList.Contains

		int IList.IndexOf(object value)
		{
			lock (items)
				return items.IndexOf((PpsTraceItemBase)value);
		} // func IList.IndexOf

		bool IList.IsFixedSize => false;
		bool IList.IsReadOnly => true;

		object IList.this[int index] { get => items[index]; set => throw new NotSupportedException(); }

		#endregion

		#region -- ICollection Member -------------------------------------------------

		void ICollection.CopyTo(Array array, int index)
		{
			lock (items)
				((ICollection)items).CopyTo(array, index);
		} // proc ICollection.CopyTo

		bool ICollection.IsSynchronized => true;
		public object SyncRoot => items;

		#endregion

		#region -- IEnumerable Member -------------------------------------------------

		IEnumerator IEnumerable.GetEnumerator()
			=> items.GetEnumerator(); 

		#endregion

		/// <summary>Currently, catched events.</summary>
		public int Count { get { lock (items) return items.Count; } }
		/// <summary>The last catched trace event.</summary>
		public PpsTraceItemBase LastTrace
		{
			get => lastTrace;
			private set
			{
				if (lastTrace != value)
				{
					lock (items)
						lastTrace = value;

					OnPropertyChanged(nameof(LastTrace));
				}
			}
		} // prop LastTrace
	} // class PpsTraceLog

	#endregion
}