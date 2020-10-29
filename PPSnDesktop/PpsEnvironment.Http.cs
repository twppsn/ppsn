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
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using TecWare.DE.Networking;
using TecWare.DE.Stuff;
using TecWare.PPSn.UI;

namespace TecWare.PPSn
{
	#region -- enum PpsLoadState ------------------------------------------------------

	/// <summary>State of the pps upload task.</summary>
	public enum PpsLoadState
	{
		/// <summary>Task is queued</summary>
		Pending,
		/// <summary>Task is currently loading</summary>
		Started,
		/// <summary>Task is fully loaded</summary>
		Finished,
		/// <summary>Loading of the task was cancelled</summary>
		Canceled,
		/// <summary>Loading of the task failed</summary>
		Failed
	} // enum PpsWebLoadState

	#endregion

	#region -- enum PpsLoadPriority ---------------------------------------------------

	/// <summary>Defines the importance of an item</summary>
	public enum PpsLoadPriority
	{
		/// <summary>Second-most important</summary>
		Default = 1,
		/// <summary>Top-most important</summary>
		ApplicationFile = 0,
		/// <summary>Third-most important</summary>
		ObjectPrimaryData = 1,
		/// <summary>Fourth-most important</summary>
		ObjectReferencedData = 2,
		/// <summary>Least important</summary>
		Background = 3
	} // enum PpsLoadPriority

	#endregion

	#region -- interface IPpsProxyTask ------------------------------------------------

	/// <summary>Access to a queued proxy request.</summary>
	public interface IPpsProxyTask : INotifyPropertyChanged
	{
		/// <summary>Append a function to change the default response behaviour.</summary>
		/// <param name="response">Request response.</param>
		void AppendResponseSink(Action<WebResponse> response);

		/// <summary>Processes the request in the forground (change priority to first).</summary>
		/// <returns></returns>
		Task<WebResponse> ForegroundAsync();
		/// <summary>Task to watch the download process.</summary>
		Task<WebResponse> Task { get; }

		/// <summary>State of the download progress</summary>
		PpsLoadState State { get; }

		/// <summary>Download state of the in percent.</summary>
		int Progress { get; }
		/// <summary>Displayname that will be shown in the ui.</summary>
		string DisplayName { get; }
	} // interface IPpsProxyTask

	#endregion

	#region -- interface IPpsOfflineItemData ------------------------------------------

	/// <summary>For internal use, to give access to the offline data.</summary>
	[EditorBrowsable(EditorBrowsableState.Advanced)]
	public interface IPpsOfflineItemData : IPropertyReadOnlyDictionary
	{
		/// <summary>Access to the content</summary>
		Stream Content { get; }
		/// <summary>Content type</summary>
		string ContentType { get; }
		/// <summary>Expected content length</summary>
		long ContentLength { get; }
		/// <summary>Last modification time stamp.</summary>
		DateTime LastModification { get; }
	} // interface IPpsOfflineItemData

	#endregion

	#region -- interface IInternalFileCacheStream -------------------------------------

	internal interface IInternalFileCacheStream
	{
		void MoveTo(string fileName);
	} // interface IInternalFileCacheStream

	#endregion

	#region -- class PpsProxyRequest --------------------------------------------------

	/// <summary>Proxy request to implementation, that is able to switch between offline 
	/// cache and online mode.</summary>
	public sealed class PpsProxyRequest : WebRequest, IEquatable<PpsProxyRequest>
	{
		private readonly PpsEnvironment environment; // owner, that retrieves a resource
		private readonly string displayName;
		private readonly Uri originalUri;
		private readonly Uri relativeUri; // relative Uri

		private readonly bool offlineOnly;
		private bool aborted = false; // is the request cancelled

		private readonly Func<WebResponse> procGetResponse; // async GetResponse
		private readonly Func<Stream> procGetRequestStream; // async

		private WebHeaderCollection headers;
		private readonly string path;
		private readonly NameValueCollection arguments;

		private string method = HttpMethod.Get.Method;
		private string contentType = null;
		private long contentLength = -1;

		private Func<IPpsOfflineItemData, Stream> updateOfflineCache = null;
		private MemoryStream requestStream = null;
		private HttpWebRequest onlineRequest = null;

		#region -- Ctor/Dtor ----------------------------------------------------------

		internal PpsProxyRequest(PpsEnvironment environment, string displayName, Uri originalUri, Uri relativeUri, bool offlineOnly)
		{
			this.environment = environment ?? throw new ArgumentNullException(nameof(environment));
			this.displayName = displayName ?? relativeUri.ToString();
			this.originalUri = originalUri ?? throw new ArgumentNullException(nameof(originalUri));
			this.relativeUri = relativeUri ?? throw new ArgumentNullException(nameof(relativeUri));
			this.offlineOnly = offlineOnly;

			if (relativeUri.IsAbsoluteUri)
				throw new ArgumentException("Uri must be relative.", nameof(relativeUri));
			if (!originalUri.IsAbsoluteUri)
				throw new ArgumentException("Uri must be absolute.", nameof(originalUri));

			procGetResponse = GetResponse;
			procGetRequestStream = GetRequestStream;

			(path, arguments) = relativeUri.ParseUri();
		} // ctor

		/// <summary>Returns whether the given proxy request is for the same object</summary>
		/// <param name="other">Request to compare</param>
		/// <returns>true if equal</returns>
		public bool Equals(PpsProxyRequest other)
			=> Equals(other.relativeUri);

		/// <summary>Returns whether the Uri is equal to the given Uri</summary>
		/// <param name="otherUri">Uri to compare</param>
		/// <returns>true if equal</returns>
		public bool Equals(Uri otherUri)
			=> WebRequestHelper.EqualUri(relativeUri, otherUri);

		#endregion

		#region -- GetResponse --------------------------------------------------------

		/// <summary>Handles the request async</summary>
		/// <param name="callback"></param>
		/// <param name="state"></param>
		/// <returns></returns>
		public override IAsyncResult BeginGetResponse(AsyncCallback callback, object state)
		{
			if (aborted)
				throw new WebException("Canceled", WebExceptionStatus.RequestCanceled);

			return procGetResponse.BeginInvoke(callback, state);
		} // func BeginGetResponse

		/// <summary></summary>
		/// <param name="asyncResult"></param>
		/// <returns></returns>
		public override WebResponse EndGetResponse(IAsyncResult asyncResult)
			=> procGetResponse.EndInvoke(asyncResult);

		/// <summary>Get the response and process the request now.</summary>
		/// <returns></returns>
		public override WebResponse GetResponse()
		{
			if (UseOnlineRequest) // we have request data, execute always online
				return InternalGetResponse();
			else if (environment.TryGetOfflineObject(this, out var task)) // check if the object is local available, cached
				return task.ForegroundAsync().Await(); // block thread
			else
				return InternalGetResponse();
		} // func GetResponse

		/// <summary>Get the response and process the request now.</summary>
		/// <returns></returns>
		public override Task<WebResponse> GetResponseAsync()
		{
			if (UseOnlineRequest) // we have request data, execute always online
				return InternalGetResponseAsync();
			else if (environment.TryGetOfflineObject(this, out var task)) // check if the object is local available, cached
				return task.ForegroundAsync();
			else
				return InternalGetResponseAsync();
		} // func GetResponse

		/// <summary>Puts the request of an item on the queue</summary>
		/// <param name="priority">Importance of the item.</param>
		/// <param name="forceOnline">If true, the object is requested only from the server, not from the cache. Defaults to false.</param>
		/// <returns></returns>
		public IPpsProxyTask Enqueue(PpsLoadPriority priority, bool forceOnline = false)
		{
			// check for offline item
			if (!forceOnline && updateOfflineCache == null && environment.TryGetOfflineObject(this, out var task1))
				return task1;
			else if (!UseOnlineRequest && updateOfflineCache == null && environment.WebProxy.TryGet(this, out var task2)) // check for already existing task
				return task2;
			else // enqueue the new task
				return environment.WebProxy.Append(this, priority);
		} // func Enqueue

		private void CreateOnlineRequest()
		{
			if (onlineRequest != null)
				throw new InvalidOperationException("Request always created.");

			// create new online request
			onlineRequest = environment.CreateOnlineRequest(relativeUri);

			// copy basic request informationen
			onlineRequest.Method = method;
			if (contentLength > 0)
				onlineRequest.ContentLength = contentLength;
			if (contentType != null)
				onlineRequest.ContentType = contentType;

			// copy headers
			if (headers != null)
			{
				headers["ppsn-hostname"] = System.Environment.MachineName;
				foreach (var k in headers.AllKeys)
				{
					if (String.Compare(k, "Accept", true) == 0)
						onlineRequest.Accept = headers[k];
					else
						onlineRequest.Headers[k] = headers[k];
				}
			}

			// request data, cached POST-Data
			if (requestStream != null)
			{
				using (var dst = onlineRequest.GetRequestStream())
				{
					requestStream.Position = 0;
					requestStream.CopyTo(dst);
				}
			}
		} // func CreateOnlineRequest

		internal WebResponse InternalGetResponse()
		{
			if (onlineRequest == null)
				CreateOnlineRequest();
			return onlineRequest.GetResponse();
		} // func InternalGetResponse

		private Task<WebResponse> InternalGetResponseAsync()
		{
			if (onlineRequest == null)
				CreateOnlineRequest();
			return onlineRequest.GetResponseAsync();
		} // func InternalGetResponseAsync

		private bool UseOnlineRequest
			=> requestStream != null || onlineRequest != null;

		#endregion

		#region -- GetRequestStream ---------------------------------------------------

		/// <summary></summary>
		/// <param name="callback"></param>
		/// <param name="state"></param>
		/// <returns></returns>
		public override IAsyncResult BeginGetRequestStream(AsyncCallback callback, object state)
			=> procGetRequestStream.BeginInvoke(callback, state);

		/// <summary></summary>
		/// <param name="asyncResult"></param>
		/// <returns></returns>
		public override Stream EndGetRequestStream(IAsyncResult asyncResult)
			=> procGetRequestStream.EndInvoke(asyncResult);

		/// <summary>Create the request online, and now to support request streams.</summary>
		/// <returns></returns>
		public override Stream GetRequestStream()
			=> GetRequestStream(false);

		/// <summary>Create the request online, and now to support request streams.</summary>
		/// <param name="sendChunked"><c>true</c>, for large data, the request is executed within the GetRequestStream and the inner request stream returned.</param>
		/// <returns></returns>
		public Stream GetRequestStream(bool sendChunked)
		{
			if (offlineOnly)
				throw new ArgumentException("Request data is not allowed in offline mode.");
			if (onlineRequest != null || requestStream != null)
				throw new InvalidOperationException("GetResponse or GetRequestStream is already invoked.");

			if (sendChunked)
			{
				CreateOnlineRequest();
				if (onlineRequest.Method != HttpMethod.Post.Method
					&& onlineRequest.Method != HttpMethod.Put.Method)
					throw new ArgumentException("Only POST/PUT can use GetRequestStream in none buffering mode.");

				// stream the PUT/POST
				onlineRequest.SendChunked = true;
				onlineRequest.AllowWriteStreamBuffering = false;

				return onlineRequest.GetRequestStream();
			}
			else
			{
				if (requestStream == null)
					requestStream = new MemoryStream();

				// return a window stream with open end, that the memory stream is not closed.
				return new WindowStream(requestStream, 0, -1, true, true);
			}
		} // func GetRequestStream

		#endregion

		/// <summary>Cancel the current request</summary>
		public override void Abort()
		{
			aborted = true;
			throw new NotImplementedException("todo:");
		} // proc Abort

		/// <summary>Internal use, method to update offline cache.</summary>
		/// <param name="updateOfflineCache"></param>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public void SetUpdateOfflineCache(Func<IPpsOfflineItemData, Stream> updateOfflineCache)
		{
			this.updateOfflineCache = updateOfflineCache ?? throw new ArgumentNullException(nameof(updateOfflineCache));
		} // proc SetUpdateOfflineCache

		internal Stream UpdateOfflineCache(IPpsOfflineItemData data)
			=> updateOfflineCache?.Invoke(data) ?? data.Content;

		/// <summary>Description for the ui.</summary>
		public string DisplayName => displayName;
		/// <summary>Request method</summary>
		public override string Method { get => method; set => method = value; }
		/// <summary>Content type of the request (mime type).</summary>
		public override string ContentType { get => contentType; set => contentType = value; }
		/// <summary>Content length, to send.</summary>
		public override long ContentLength { get => contentLength; set => contentLength = value; }

		/// <summary>Environment access.</summary>
		public PpsEnvironment Environment => environment;

		/// <summary>Request uri.</summary>
		public override Uri RequestUri => originalUri;

		/// <summary>We do not use any proxy.</summary>
		public override IWebProxy Proxy { get => null; set { } } // avoid NotImplementedExceptions

		/// <summary>Arguments of the request</summary>
		public NameValueCollection Arguments => arguments;
		/// <summary>Relative path for the request.</summary>
		public string Path => path;

		/// <summary>Header</summary>
		public override WebHeaderCollection Headers { get => headers ?? (headers = new WebHeaderCollection()); set => headers = value; }
	} // class PpsProxyRequest

	#endregion

	#region -- class PpsWebProxy ------------------------------------------------------

	/// <summary>Internal proxy to queue download and upload request.</summary>
	public sealed class PpsWebProxy : IEnumerable<IPpsProxyTask>, INotifyCollectionChanged, IDisposable
	{
		#region -- class MemoryCacheStream --------------------------------------------

		private sealed class MemoryCacheStream : Stream
		{
			private readonly MemoryStream nestedMemoryStream;

			public MemoryCacheStream(long expectedLength)
			{
				ExpectedLength = expectedLength;
				nestedMemoryStream = new MemoryStream(unchecked((int)(expectedLength > 0 ? expectedLength : 4096)));
			} // ctor

			public override void Flush()
				=> nestedMemoryStream.Flush();

			public override void SetLength(long value)
			{
				if (value != Length)
					throw new NotSupportedException();
			} // proc SetLength

			public override long Seek(long offset, SeekOrigin origin)
				=> nestedMemoryStream.Seek(offset, origin);

			public override int Read(byte[] buffer, int offset, int count)
				=> nestedMemoryStream.Read(buffer, offset, count);

			public override void Write(byte[] buffer, int offset, int count)
				=> nestedMemoryStream.Write(buffer, offset, count);

			public override bool CanRead => true;
			public override bool CanWrite => true;
			public override bool CanSeek => true;

			public override long Position { get => nestedMemoryStream.Position; set => nestedMemoryStream.Position = value; }
			public override long Length => nestedMemoryStream.Length;

			public long ExpectedLength { get; }
		} // class MemoryCacheStream

		#endregion

		#region -- class FileCacheStream ----------------------------------------------

		private sealed class FileCacheStream : Stream, IInternalFileCacheStream
		{
			private readonly string fileName;
			private readonly long expectedLength;
			private readonly FileStream nestedFileStream;

			private long currentLength = 0L;

			public FileCacheStream(long expectedLength)
			{
				fileName = Path.GetTempFileName();
				this.expectedLength = expectedLength;
				nestedFileStream = new FileStream(fileName, FileMode.Create);

				if (expectedLength > 0)
					nestedFileStream.SetLength(expectedLength);
			} // ctor

			public FileCacheStream(MemoryCacheStream copyFrom, long expectedLength)
				: this(expectedLength)
			{
				// copy stream
				copyFrom.Position = 0;
				copyFrom.CopyTo(this);
			} // ctor

			protected override void Dispose(bool disposing)
			{
				if (File.Exists(fileName))
				{
					try { File.Delete(fileName); }
					catch { }
				}
				base.Dispose(disposing);
			} // proc Dispose

			public void MoveTo(string targetFileName)
			{
				nestedFileStream.Dispose(); // close stream

				File.Move(fileName, targetFileName);
			} // proc MoveTo

			public override void Flush()
				=> nestedFileStream.Flush();

			public override int Read(byte[] buffer, int offset, int count)
				=> nestedFileStream.Read(buffer, offset, count);

			public override void Write(byte[] buffer, int offset, int count)
			{
				var appendOperation = nestedFileStream.Position == currentLength;
				nestedFileStream.Write(buffer, offset, count);

				if (appendOperation)
					currentLength += count;
			} // proc Write

			public override long Seek(long offset, SeekOrigin origin)
				=> nestedFileStream.Seek(offset, origin);

			public override void SetLength(long value)
			{
				if (value != currentLength)
					throw new NotSupportedException();
			} // proc SetLength

			public override bool CanRead => true;
			public override bool CanSeek => true;
			public override bool CanWrite => true;

			public override long Length => currentLength;

			public override long Position { get => nestedFileStream.Position; set => nestedFileStream.Position = value; }
		} // class FileCacheStream

		#endregion

		#region -- class CacheResponseStream ------------------------------------------

		private sealed class CacheResponseStream : Stream
		{
			private readonly Stream resultStream;
			private long position = 0L;

			public CacheResponseStream(Stream resultStream)
			{
				this.resultStream = resultStream;
			} // ctor

			private void EnsurePosition()
			{
				if (resultStream.Position != position)
					resultStream.Position = position;
			}

			public override void Flush() { }

			public override int Read(byte[] buffer, int offset, int count)
			{
				lock (resultStream)
				{
					EnsurePosition();
					var readed = resultStream.Read(buffer, offset, count);
					position += readed;
					return readed;
				}
			} // func Read

			public override long Seek(long offset, SeekOrigin origin)
			{
				long getNewPosition()
				{
					switch (origin)
					{
						case SeekOrigin.Begin:
							return offset;
						case SeekOrigin.Current:
							return position + offset;
						case SeekOrigin.End:
							return Length - position;
						default:
							throw new ArgumentOutOfRangeException(nameof(origin));
					}
				}

				var newPosition = getNewPosition();
				if (newPosition < 0 || newPosition > Length)
					throw new ArgumentOutOfRangeException(nameof(offset));

				return position = newPosition;
			} // func Seek

			public override void SetLength(long value)
				=> throw new NotSupportedException();

			public override void Write(byte[] buffer, int offset, int count)
				=> throw new NotSupportedException();


			public override bool CanRead => true;
			public override bool CanSeek => true;
			public override bool CanWrite => false;


			public override long Position { get => position; set => Seek(value, SeekOrigin.Begin); }
			public override long Length => resultStream.Length;
		} // class CacheResponseStream

		#endregion

		#region -- class CacheResponseProxy -------------------------------------------

		private sealed class CacheResponseProxy : WebResponse
		{
			private readonly Uri responseUri;
			private readonly Stream resultStream;
			private readonly string contentType;
			private readonly WebHeaderCollection headers;

			public CacheResponseProxy(Uri responseUri, Stream resultStream, string contentType, WebHeaderCollection headers)
			{
				this.responseUri = responseUri;
				this.resultStream = resultStream ?? throw new ArgumentNullException(nameof(headers));
				this.contentType = contentType ?? throw new ArgumentNullException(nameof(headers));
				this.headers = headers ?? throw new ArgumentNullException(nameof(headers));

				if (!resultStream.CanSeek)
					throw new ArgumentException("resultStream is not seekable", nameof(resultStream));
				if (!resultStream.CanRead)
					throw new ArgumentException("resultStream is not readable", nameof(resultStream));
			} // ctor

			public override Stream GetResponseStream()
				=> new CacheResponseStream(resultStream); // create a new stream

			public override WebHeaderCollection Headers => headers;

			public override long ContentLength { get => resultStream.Length; set => throw new NotSupportedException(); }
			public override string ContentType { get => contentType; set => throw new NotSupportedException(); }

			public override Uri ResponseUri => responseUri;
		} // class CacheResponseProxy

		#endregion

		#region -- class WebLoadRequest -----------------------------------------------

		private sealed class WebLoadRequest : IPpsProxyTask
		{
			#region -- class PpsOfflineItemDataImplementation ---------------------------

			private sealed class PpsOfflineItemDataImplementation : IPpsOfflineItemData
			{
				private readonly Stream data;
				private readonly string contentType;
				private readonly WebHeaderCollection headers;

				public PpsOfflineItemDataImplementation(Stream data, string contentType, WebHeaderCollection headers)
				{
					this.data = data;
					this.contentType = contentType;
					this.headers = headers;
				} // ctor

				public bool TryGetProperty(string name, out object value)
					=> (value = headers.Get(name)) != null;

				private static DateTime GetNowWithoutTicks()
				{
					var n = DateTime.Now;
					return new DateTime(n.Year, n.Month, n.Day, n.Hour, n.Minute, n.Second, n.Kind);
				} // func GetNowWithoutTicks

				public Stream Content => data;
				public string ContentType => contentType;
				public long ContentLength => data.Length;
				public DateTime LastModification => DateTime.TryParse(headers[HttpResponseHeader.LastModified], out var lastModified) ? lastModified : GetNowWithoutTicks();
			} // class PpsOfflineItemDataImplementation

			#endregion

			private const long tempFileBorder = 10 << 20;

			public event PropertyChangedEventHandler PropertyChanged;

			private readonly PpsWebProxy manager;
			private readonly PpsLoadPriority priority;
			private readonly PpsProxyRequest request;

			private readonly List<Action<WebResponse>> webResponseSinks = new List<Action<WebResponse>>();
			private readonly TaskCompletionSource<WebResponse> task;

			private readonly object stateLock = new object();
			private PpsLoadState currentState = PpsLoadState.Pending;
			private int progress = -1;

			private CacheResponseProxy resultResponse = null;
			private Exception resultException = null;

			public WebLoadRequest(PpsWebProxy manager, PpsLoadPriority priority, PpsProxyRequest request)
			{
				this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
				this.priority = priority;
				this.request = request;

				task = new TaskCompletionSource<WebResponse>();
			} // ctor

			public bool IsSameRequest(PpsProxyRequest request)
				=> this.request.Equals(request);

			public bool IsSameRequest(Uri requestUri)
				=> this.request.Equals(requestUri);

			public void AppendResponseSink(Action<WebResponse> response)
			{
				lock (stateLock)
				{
					if (State == PpsLoadState.Finished)
						response(resultResponse);
					else if (State == PpsLoadState.Canceled)
						throw new OperationCanceledException("Response aborted.");
					else if (State == PpsLoadState.Failed)
						throw new Exception("Repsonse failed.", resultException);
					else if (webResponseSinks.IndexOf(response) == -1)
						webResponseSinks.Add(response);
				}
			} // proc AppendResponseSink

			public Task<WebResponse> ForegroundAsync()
			{
				lock (stateLock)
				{
					if (currentState == PpsLoadState.Pending)
						manager.MoveToForeground(this);
				}
				return Task;
			} // func ForegroundAsync

			private Stream CreateCacheStream(long contentLength)
				=> contentLength > tempFileBorder // create a temp file
					? (Stream)new FileCacheStream(contentLength)
					: new MemoryCacheStream(contentLength);

			internal void Execute()
			{
				lock (stateLock)
					UpdateState(PpsLoadState.Started);
				try
				{
					// is the request data
					using (var response = request.InternalGetResponse())
					{
						// cache the header information
						var contentLength = response.ContentLength;
						var contentType = response.ContentType;
						var headers = new WebHeaderCollection();
						foreach (var k in response.Headers.AllKeys)
							headers.Set(k, response.Headers[k]);

						// start the download
						var checkForSwitchToFile = false;
						var dst = CreateCacheStream(contentLength);
						using (var src = response.GetResponseStream())
						{
							try
							{
								var copyBuffer = new byte[4096];
								var readedTotal = 0L;
								checkForSwitchToFile = dst is MemoryCacheStream;

								while (true)
								{
									var readed = src.Read(copyBuffer, 0, copyBuffer.Length);

									UpdateProgress(unchecked((int)(readed * 1000 / contentLength)));
									if (readed > 0)
									{
										dst.Write(copyBuffer, 0, readed);
										readedTotal += readed;
										if (contentLength > readedTotal)
											UpdateProgress(unchecked((int)(readedTotal * 1000 / contentLength)));
										else if (checkForSwitchToFile && readedTotal > tempFileBorder)
										{
											if (dst is MemoryCacheStream oldDst)
											{
												dst = new FileCacheStream(oldDst, oldDst.ExpectedLength);
												oldDst.Dispose();
											}
										}
									}
									else
										break;
								}

								// process finished
								UpdateState(PpsLoadState.Finished);
								dst.Flush();

								// the cache stream will be disposed by the garbage collector, or if it is moved to the offline cache
								request.UpdateOfflineCache(new PpsOfflineItemDataImplementation(dst, contentType, headers));

								// spawn the result functions
								lock (stateLock)
								{
									UpdateState(PpsLoadState.Finished);
									resultResponse = new CacheResponseProxy(request.RequestUri, dst, contentType, headers);
								}
								foreach (var s in webResponseSinks)
									System.Threading.Tasks.Task.Run(() => s(resultResponse));

								// set the result
								task.SetResult(resultResponse);
							}
							catch
							{
								dst.Dispose(); // dispose because error
								throw;
							}// using src,dst
						}
					} // using response
				}
				catch (TaskCanceledException)
				{
					UpdateState(PpsLoadState.Canceled);
					task.SetCanceled();
				}
				catch (Exception e)
				{
					lock (stateLock)
					{
						UpdateState(PpsLoadState.Failed);
						resultException = e;
					}
					task.SetException(e);
				}
			} // proc Execute

			private void OnPropertyChanged(string propertyName)
				=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

			private void UpdateProgress(int newProgress)
			{
				if (progress != newProgress)
				{
					progress = newProgress;
					OnPropertyChanged(nameof(Progress));
				}
			} // proc UpdateProgress

			private void UpdateState(PpsLoadState newState)
			{
				if (currentState != newState)
				{
					currentState = newState;
					OnPropertyChanged(nameof(State));
				}
			} // proc UpdateState

			public Task<WebResponse> Task => task.Task;
			public PpsLoadState State => currentState;
			public PpsLoadPriority Priority => priority;
			public int Progress => progress;
			public string DisplayName => request.DisplayName;
		} // class WebLoadRequest

		#endregion

		/// <summary>Raised if the queue of the proxy has changed.</summary>
		public event NotifyCollectionChangedEventHandler CollectionChanged;

		private readonly PpsEnvironment environment;
		private readonly List<WebLoadRequest> downloadList = new List<WebLoadRequest>(); // current list of request
		private int currentForegroundCount = 0; // web requests, that marked as foreground tasks (MoveToForeground moves to this point)

		private readonly PpsSynchronizationContext executeLoadQueue;
		private readonly ManualResetEventAsync executeLoadIsRunning = new ManualResetEventAsync(false);
		private readonly CancellationTokenSource disposed;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary></summary>
		/// <param name="environment"></param>
		internal PpsWebProxy(PpsEnvironment environment)
		{
			this.disposed = new CancellationTokenSource();
			this.environment = environment;
			this.executeLoadQueue = new PpsSingleThreadSynchronizationContext("PpsWebProxy", disposed.Token, () => ExecuteLoadQueueAsync(disposed.Token));
		} // class PpsDownloadManager

		public void Dispose()
		{
			if (disposed.IsCancellationRequested)
				throw new ObjectDisposedException(nameof(PpsWebProxy));

			disposed.Cancel();
			executeLoadIsRunning.Set();
		} // proc Dispose

		#endregion

		private void OnCollectionChanged()
			=> CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

		/// <summary>Enumerator for the download task.</summary>
		/// <returns></returns>
		/// <remarks>It locks the current process.</remarks>
		public IEnumerator<IPpsProxyTask> GetEnumerator()
		{
			lock (downloadList)
			{
				foreach (var c in downloadList)
					yield return c;
			}
		} // func GetEnumerator

		IEnumerator IEnumerable.GetEnumerator()
			=> GetEnumerator();

		private WebLoadRequest TryDequeueTask()
		{
			lock (downloadList)
			{
				if (downloadList.Count == 0)
				{
					executeLoadIsRunning.Reset();
					return null;
				}
				else
				{
					var r = downloadList[0];
					if (currentForegroundCount == 0)
						currentForegroundCount = 1; // mark as foreground, that no other request moves before
					return r;
				}
			}
		} // proc TryDequeueTask

		private void RemoveCurrentTask()
		{
			var notifyReset = false;
			lock (downloadList)
			{
				if (currentForegroundCount > 0)
				{
					downloadList.RemoveAt(0);
					currentForegroundCount--;
					notifyReset = true;
				}
			}
			if (notifyReset)
				OnCollectionChanged();
		} // proc RemoveCurrentTask

		private async Task ExecuteLoadQueueAsync(CancellationToken cancellationToken)
		{
			await Task.Yield(); // enque the loop

			while (!cancellationToken.IsCancellationRequested)
			{
				var nextTask = TryDequeueTask();
				if (nextTask != null)
				{
					try
					{
						nextTask.Execute();
					}
					catch (Exception e)
					{
						// todo: connect lost?
						await environment.ShowExceptionAsync(PpsExceptionShowFlags.Background, e);
					}
					finally
					{
						RemoveCurrentTask();
					}
				}

				// wait for next item
				await executeLoadIsRunning.WaitAsync();
			}
		} // proc ExecuteLoadQueue

		internal void MoveToForeground(IPpsProxyTask task)
		{
			lock (downloadList)
			{
				var t = (WebLoadRequest)task;
				var idx = downloadList.IndexOf(t);
				if (idx >= currentForegroundCount) // check if the task is already in foreground
				{
					downloadList.RemoveAt(idx);
					downloadList.Insert(currentForegroundCount++, t);
				}
			}
			OnCollectionChanged();
		} // proc MoveToForeground

		private IPpsProxyTask AppendTask(WebLoadRequest task)
		{
			try
			{
				lock (downloadList)
				{
					// priority section, and not before the current requests
					var i = currentForegroundCount;
					while (i < downloadList.Count && downloadList[i].Priority <= task.Priority)
						i++;

					// add at pos
					downloadList.Insert(i, task);
					executeLoadIsRunning.Set();

					return task;
				}
			}
			finally
			{
				OnCollectionChanged();
			}
		} // proc AppendTask

		/// <summary>Get proxy task from the proxy request.</summary>
		/// <param name="request"></param>
		/// <param name="task"></param>
		/// <returns></returns>
		internal bool TryGet(PpsProxyRequest request, out IPpsProxyTask task)
		{
			// check, request exists
			lock (downloadList)
			{
				task = downloadList.Find(c => c.IsSameRequest(request));
				return task != null;
			}
		} // func TryGet

		/// <summary>Get a proxy task from the request uri.</summary>
		/// <param name="requestUri"></param>
		/// <param name="task"></param>
		/// <returns></returns>
		public bool TryGet(Uri requestUri, out IPpsProxyTask task)
		{
			// check, request exists
			lock (downloadList)
			{
				task = downloadList.Find(c => c.IsSameRequest(requestUri));
				return task != null;
			}
		} // func TryGet

		/// <summary>Append a new request to the download/upload list.</summary>
		/// <param name="request"></param>
		/// <param name="priority"></param>
		/// <returns></returns>
		internal IPpsProxyTask Append(PpsProxyRequest request, PpsLoadPriority priority)
			=> AppendTask(new WebLoadRequest(this, priority, request));
	} // class PpsDownloadManager

	#endregion

	#region -- class PpsDummyProxyHelper ----------------------------------------------

	internal static class PpsDummyProxyHelper
	{
		#region -- class PpsDummyProxyTask ----------------------------------------------

		internal sealed class PpsDummyProxyTask : IPpsProxyTask
		{
			event PropertyChangedEventHandler INotifyPropertyChanged.PropertyChanged { add { } remove { } }

			private readonly WebRequest request;
			private readonly string displayName;
			private bool responseCalled;

			public PpsDummyProxyTask(WebRequest request, string displayName)
			{
				this.request = request;
				this.displayName = displayName ?? request.RequestUri.PathAndQuery;
			} // ctor

			private WebRequest InitResponse()
			{
				if (responseCalled)
					throw new InvalidOperationException();

				responseCalled = true;
				return request;
			} // proc InitResponse

			public void AppendResponseSink(Action<WebResponse> response)
				=> response(InitResponse().GetResponse());

			public Task<WebResponse> ForegroundAsync()
				=> InitResponse().GetResponseAsync();

			public void SetUpdateOfflineCache(Func<IPpsOfflineItemData, Stream> updateOfflineCache)
				=> throw new NotSupportedException();

			public Task<WebResponse> Task => InitResponse().GetResponseAsync();

			public PpsLoadState State => PpsLoadState.Started;
			public int Progress => -1;
			public string DisplayName => displayName;
		} // class PpsDummyProxyTask

		#endregion

		/// <summary>Wrap a webrequest to an proxy task.</summary>
		/// <param name="request"></param>
		/// <param name="displayName"></param>
		/// <param name="priority"></param>
		/// <returns></returns>
		public static IPpsProxyTask GetProxyTask(this WebRequest request, string displayName, PpsLoadPriority priority = PpsLoadPriority.Default)
		   => request is PpsProxyRequest p
			   ? p.Enqueue(priority)
			   : new PpsDummyProxyTask(request, displayName);
	} // class PpsDummyProxyHelper

	#endregion

	#region -- class PpsEnvironment ---------------------------------------------------

	public partial class PpsEnvironment
	{
		#region -- class PpsMessageHandler --------------------------------------------

		private sealed class PpsMessageHandler : HttpClientHandler
		{
			private readonly PpsEnvironment environment;

			public PpsMessageHandler(PpsEnvironment environment)
			{
				this.environment = environment ?? throw new ArgumentNullException(nameof(environment));
			} // ctor

			protected async override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
			{
				// base.SendAsync uses the ctor of HttpWebRequest
				var webRequest = environment.CreateProxyRequest(request.RequestUri);
				webRequest.Method = request.Method.ToString();

				if (request.Headers.Contains("Transfer-Encoding"))
					webRequest.Headers.Add(HttpRequestHeader.TransferEncoding, String.Join(",", request.Headers.TransferEncoding));

				// post-data
				if (request.Content != null)
				{
					// copy content headers
					foreach (var kv in request.Content.Headers)
					{
						if (String.Compare(kv.Key, "Content-Type", StringComparison.OrdinalIgnoreCase) == 0)
							webRequest.ContentType = String.Join(";", kv.Value);
						else if (String.Compare(kv.Key, "Content-Length", StringComparison.OrdinalIgnoreCase) != 0)
							webRequest.Headers.Add(kv.Key, String.Join(",", kv.Value));
					}

					// send content to server
					using (var dst = await webRequest.GetRequestStreamAsync())
						await request.Content.CopyToAsync(dst);
				}

				// get data
				var webResponse = await webRequest.GetResponseAsync();
				var httpResponse = webResponse as HttpWebResponse;

				var response = new HttpResponseMessage(httpResponse?.StatusCode ?? HttpStatusCode.OK)
				{
					ReasonPhrase = httpResponse?.StatusDescription ?? String.Empty,
					Version = httpResponse?.ProtocolVersion ?? new Version(1, 0),
					RequestMessage = request
				};
				request.RequestUri = webResponse.ResponseUri;

				response.Content = new StreamContent(webResponse.GetResponseStream());
				var contentHeaders = response.Content.Headers;
				var responseHeaders = webResponse.Headers;

				if (!String.IsNullOrEmpty(webResponse.ContentType))
					contentHeaders.ContentType = MediaTypeHeaderValue.Parse(webResponse.ContentType);
				if (webResponse.ContentLength >= 0)
					contentHeaders.ContentLength = webResponse.ContentLength;

				for (var i = 0; i < responseHeaders.Count; i++)
				{
					var key = responseHeaders.GetKey(i);
					if (String.Compare("Content-Length", key, StringComparison.OrdinalIgnoreCase) != 0)
					{
						var values = responseHeaders.GetValues(i);
						if (!response.Headers.TryAddWithoutValidation(key, values))
							contentHeaders.TryAddWithoutValidation(key, values);
					}
				}

				return response;
			} // func SendAsync
		} // class PpsMessageHandler

		#endregion

		#region -- class PpsWebRequestCreate ------------------------------------------

		private class PpsWebRequestCreate : IWebRequestCreate
		{
			private readonly WeakReference<PpsEnvironment> environmentReference;

			public PpsWebRequestCreate(PpsEnvironment environment)
			{
				environmentReference = new WeakReference<PpsEnvironment>(environment);
			} // ctor

			public WebRequest Create(Uri uri)
			{
				if (environmentReference.TryGetTarget(out var environment))
					return environment.CreateProxyRequest(uri);
				else
					throw new ObjectDisposedException("Environment does not exists anymore.");
			} // func Create
		} // class PpsWebRequestCreate

		#endregion

		private PpsWebProxy webProxy; // remote download/upload manager
		private ProxyStatus statusOfProxy;  // interface for the transaction manager

		#region -- Web Request --------------------------------------------------------

		/// <summary>Core function that gets called on a request.</summary>
		/// <param name="uri"></param>
		/// <returns></returns>
		private WebRequest CreateProxyRequest(Uri uri)
		{
			if (!uri.IsAbsoluteUri)
				throw new ArgumentException("Uri must absolute.", nameof(uri));

			const string localPrefix = "/local/";
			const string remotePrefix = "/remote/";

			var useOfflineRequest = CurrentMode == PpsEnvironmentMode.Offline;
			var useCache = true;
			var absolutePath = uri.AbsolutePath;

			// is the local data prefered
			if (absolutePath.StartsWith(localPrefix))
			{
				absolutePath = absolutePath.Substring(localPrefix.Length);
				useOfflineRequest = true;
			}
			else if (absolutePath.StartsWith(remotePrefix))
			{
				absolutePath = absolutePath.Substring(remotePrefix.Length);
				useOfflineRequest = false;
				useCache = false;
			}
			else if (absolutePath.StartsWith("/")) // if the uri starts with "/", remove it, because the info.remoteUri is our root
			{
				absolutePath = absolutePath.Substring(1);
			}

			// create a relative uri
			var relativeUri = new Uri(absolutePath + uri.GetComponents(UriComponents.Query | UriComponents.KeepDelimiter, UriFormat.UriEscaped), UriKind.Relative);

			// create the request proxy
			if (useCache || useOfflineRequest)
				return new PpsProxyRequest(this, relativeUri.ToString(), uri, relativeUri, useOfflineRequest);
			else
				return CreateOnlineRequest(relativeUri);
		} // func CreateWebRequest

		/// <summary>Is used only internal to create the real request.</summary>
		/// <param name="relativeUri"></param>
		/// <returns></returns>
		internal HttpWebRequest CreateOnlineRequest(Uri relativeUri)
		{
			if (relativeUri.IsAbsoluteUri)
				throw new ArgumentException("Uri must be relative.", nameof(relativeUri));
			if (relativeUri.OriginalString.StartsWith("/"))
				relativeUri = new Uri(relativeUri.OriginalString.Substring(1), UriKind.Relative);

			// build the remote request with absolute uri and credentials
			var absoluteUri = new Uri(info.Uri, relativeUri);
			var request = WebRequest.CreateHttp(absoluteUri);
			request.Credentials = userInfo is NetworkCredential nc ? UserCredential.Wrap(nc) : userInfo; // override the current credentials
			request.Headers.Add("des-multiple-authentifications", "true");
			request.Timeout = -1; // 600 * 1000;

			if (!absoluteUri.ToString().EndsWith("/?action=mdata"))
				Debug.Print($"WebRequest: {absoluteUri}");

			return request;
		} // func CreateOnlineRequest

		/// <summary>Get a proxy request for the request path.</summary>
		/// <param name="path"></param>
		/// <param name="displayName"></param>
		/// <returns></returns>
		public PpsProxyRequest GetProxyRequest(string path, string displayName)
			=> GetProxyRequest(new Uri(path, UriKind.Relative), displayName);

		/// <summary>Get a proxy request for the request path.</summary>
		/// <param name="uri"></param>
		/// <param name="displayName"></param>
		/// <returns></returns>
		public PpsProxyRequest GetProxyRequest(Uri uri, string displayName)
			=> new PpsProxyRequest(this, displayName, new Uri(Request.BaseAddress, uri), uri, CurrentState == PpsEnvironmentState.Offline);

		/// <summary>Loads an item from offline cache.</summary>
		/// <param name="request">Selects the item.</param>
		/// <param name="task">Out: the Task returning the item.</param>
		/// <returns>True if successfull.</returns>
		protected internal virtual bool TryGetOfflineObject(WebRequest request, out IPpsProxyTask task)
		{
			var relativeUri = Request.BaseAddress.MakeRelativeUri(request.RequestUri);

			// ask local database
			return masterData.TryGetOfflineCacheFile(relativeUri, out task);
		} // func TryGetOfflineObject

		#endregion

		private DEHttpClient CreateHttpCore(Uri uri)
			=> DEHttpClient.Create(uri, httpHandler: new PpsMessageHandler(this));
		
		public override DEHttpClient CreateHttp(Uri uri = null)
		{
			if (uri == null)
				return CreateHttpCore(Request.BaseAddress);
			else if (uri.IsAbsoluteUri)
			{
				var relativeUri = Request.BaseAddress.MakeRelativeUri(uri);
				return CreateHttpCore(new Uri(Request.BaseAddress, relativeUri));
			}
			else // relative to base
				return CreateHttpCore(new Uri(Request.BaseAddress, uri));
		} // func CreateHttp
		
		public override DEHttpClient Request { get;  }

		public PpsWebProxy WebProxy => webProxy;

		public ProxyStatus StatusOfProxy => statusOfProxy;

		internal static void LoadLocalResourceMap(string resourceMapFile)
		{
			// every line in resource map has

		} // proc LoadLocalResourceMap
	} // class PpsEnvironment

	#endregion

	// interface Status
	public interface IStatusList : INotifyPropertyChanged
	{
		object ActualItem { get; }
		ObservableCollection<object> TopTen { get; }
	}

	public class ProxyStatus : IStatusList
	{
		private PpsWebProxy proxy;
		private ObservableCollection<object> topTen = new ObservableCollection<object>();
		private IPpsProxyTask actualItem;
		private System.Windows.Threading.Dispatcher dispatcher;

		public ProxyStatus(PpsWebProxy Proxy, System.Windows.Threading.Dispatcher Dispatcher)
		{
			this.proxy = Proxy;
			this.dispatcher = Dispatcher;
			this.proxy.CollectionChanged += WebProxyChanged;
		}

		public event PropertyChangedEventHandler PropertyChanged;

		private void OnPropertyChanged(string propertyName)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		private void WebProxyChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			dispatcher?.Invoke(() =>
			{
				topTen.Clear();
				using (var walker = proxy.GetEnumerator())
				{
					for (var i = 0; i < 10; i++)
					{
						if (walker.MoveNext())
							if (i == 0)
							{
								actualItem = walker.Current;
								OnPropertyChanged(nameof(actualItem));
							}
							else
								topTen.Insert(0, walker.Current);
						else if (i == 0)
						{
							actualItem = null;
							OnPropertyChanged(nameof(actualItem));
						}
					}
				}
			});
		}

		public object ActualItem => actualItem;
		public ObservableCollection<object> TopTen => topTen;
	} // class ProxyStatus
}
