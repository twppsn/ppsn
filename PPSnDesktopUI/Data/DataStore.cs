using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using TecWare.DES.Networking;
using TecWare.DES.Stuff;

namespace TecWare.PPSn.Data
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public abstract class PpsDataStore
	{
		#region -- class PpsStoreRequest --------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		protected sealed class PpsStoreRequest : WebRequest
		{
			private PpsDataStore store; // owner, that retrieves a resource
			private Uri uri; // resource
			private bool aborted = false; // is the request canceled
			private Func<WebResponse> procGetResponse; // async GetResonse

			private string path;
			private NameValueCollection arguments;

			#region -- Ctor/Dtor ------------------------------------------------------------

			public PpsStoreRequest(PpsDataStore store, Uri uri, string path)
			{
				this.store = store;
				this.uri = uri;
				this.procGetResponse = GetResponse;
				this.path = path;

				arguments = HttpUtility.ParseQueryString(uri.Query);
			} // ctor

			#endregion

			#region -- GetResponse ----------------------------------------------------------

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
			{
				return procGetResponse.EndInvoke(asyncResult);
			} // func EndGetResponse

			/// <summary></summary>
			/// <returns></returns>
			public override WebResponse GetResponse()
			{
				var response = new PpsStoreResponse(this);
				store.GetResponseDataStream(response);
				return response;
			} // func GetRepsonse

			#endregion

			public override Uri RequestUri => uri;

			/// <summary>Arguments of the request</summary>
			public NameValueCollection Arguments => arguments;
			/// <summary>Relative path for the request.</summary>
			public string Path => path;








			public override void Abort()
			{
				base.Abort();
			}

			public override IAsyncResult BeginGetRequestStream(AsyncCallback callback, object state)
			{
				return base.BeginGetRequestStream(callback, state);
			}

			public override Stream EndGetRequestStream(IAsyncResult asyncResult)
			{
				return base.EndGetRequestStream(asyncResult);
			}

			public override Stream GetRequestStream()
			{
				return base.GetRequestStream();
			}

			public override WebHeaderCollection Headers
			{
				get
				{
					return base.Headers;
				}
				set
				{
					base.Headers = value;
				}
			}

			public override string ContentType
			{
				get
				{
					return base.ContentType;
				}

				set
				{
					base.ContentType = value;
				}
			}

			public override long ContentLength
			{
				get
				{
					return base.ContentLength;
				}
				set
				{
					base.ContentLength = value;
				}
			}
		} // class PpsStoreRequest

		#endregion

		#region -- class PpsStoreResponse ----------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		protected sealed class PpsStoreResponse : WebResponse
		{
			private PpsStoreRequest request;

			private Stream src;
			private string contentType;
			private long? contentLength = null;

			private WebHeaderCollection headers;

			public PpsStoreResponse(PpsStoreRequest request)
			{
				this.request = request;
				this.headers = new WebHeaderCollection();

				this.src = null;
				this.contentType = null;
			} // ctor

			public override void Close()
			{
				Procs.FreeAndNil(ref src);
				contentLength = null;
				contentType = null;
				base.Close();
			} // proc Close

			public void SetResponseData(Stream src, string contentType)
			{
				Procs.FreeAndNil(ref this.src);

				this.src = src;
				this.contentType = contentType;
			} // proc SetResponseData

			public override Stream GetResponseStream() => src;

			/// <summary></summary>
			public override long ContentLength
			{
				get { return contentLength ?? (src == null ? -1 : src.Length); }
				set { contentLength = value; }
			} // func ContentLength

			/// <summary></summary>
			public override string ContentType
			{
				get { return contentType; }
				set { contentType = value; }
			} // prop ContentType

			/// <summary>Headers will be exists</summary>
			public override bool SupportsHeaders => true;
			/// <summary>Header</summary>
			public override WebHeaderCollection Headers => headers;

			/// <summary>Request uri</summary>
			public override Uri ResponseUri => request.RequestUri;
			/// <summary>Access to the original request.</summary>
			public PpsStoreRequest Request => request;
		} // class PpsStoreResponse

		#endregion


		private PpsEnvironment environment;

		public PpsDataStore(PpsEnvironment environment)
		{
			this.environment = environment;
		} // ctor

		/// <summary>Creates a webrequest for the uri.</summary>
		/// <param name="uri"></param>
		/// <returns></returns>
		public WebRequest GetRequest(Uri uri, string path)
		{
			return new PpsStoreRequest(this, uri, path);
		} // func GetRequet

		/// <summary>Gets the data for the current request.</summary>
		/// <param name="r">Response that will be returned.</param>
		protected abstract void GetResponseDataStream(PpsStoreResponse r);

		public virtual IEnumerable<IDataRecord> GetListData(PpsShellGetList arguments)
		{
			// todo: redirect to a http request
			throw new NotImplementedException();
		} // func GetListData

		public virtual IDataRecord GetDetailedData(long objectId, string typ)
		{
			// todo: redirect to a http request
			throw new NotImplementedException();
		} // func GetDetailedData
	} // class PpsDataStore
}
