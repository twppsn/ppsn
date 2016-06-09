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
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Networking;
using TecWare.DE.Stuff;
using TecWare.DE.Data;
using System.Data.SQLite;

namespace TecWare.PPSn.Data
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PpsLocalDataStore : PpsDataStore, IDisposable
	{
		#region -- class PpsStoreCacheRequest ---------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class PpsStoreCacheRequest : PpsStoreRequest
		{
			public PpsStoreCacheRequest(PpsLocalDataStore store, Uri uri, string absolutePath)
				: base(store, uri, absolutePath)
			{
			} // ctor

			public override WebResponse GetResponse()
			{
				string contentType;
				Stream source;

				// is this a static item
				if (DataStore.TryGetOfflineItem(Path, true, out contentType, out source))
				{
					var r = new PpsStoreResponse(this);
					r.SetResponseData(source, contentType);
					return r;
				}
				else if (DataStore.Environment.IsOnline)
				{
					// todo: dynamic cache, copy of properties and headers
					return DataStore.Environment.CreateWebRequestNative(RequestUri, Path).GetResponse();
				}
				else
					throw new WebException("File not found.", null, WebExceptionStatus.ProtocolError, null);
			} // func GetResponse

			public new PpsLocalDataStore DataStore => (PpsLocalDataStore)base.DataStore;
		} // class PpsStoreCacheRequest

		#endregion

		private readonly SQLiteConnection localStore;

		#region -- Ctor/Dtor --------------------------------------------------------------

		public PpsLocalDataStore(PpsEnvironment environment)
			: base(environment)
		{
			// open the 
			var datapath = Path.Combine(environment.Info.LocalPath.FullName, "localStore.db");
			localStore = new SQLiteConnection($"Data Source={datapath};Version=3");
			localStore.Open();
		} // ctor

		public void Dispose()
		{
			Dispose(true);
		} // proc Dispose

		protected virtual void Dispose(bool disposing)
		{
			localStore.Close();
		} // proc Dispose

		#endregion

		#region -- GetRequest -------------------------------------------------------------

		public WebRequest GetCachedRequest(Uri uri, string absolutePath)
		{
			return new PpsStoreCacheRequest(this, uri, absolutePath);
		} // func GetCacheRequest

		protected override void GetResponseDataStream(PpsStoreResponse r)
		{
			Stream src;
			string contentType;
			
			if (TryGetOfflineItem(r.Request.Path, false, out contentType, out src)) // ask the file from the cache
				r.SetResponseData(src, contentType);
			else
				throw new WebException("File not found.", null, WebExceptionStatus.ProtocolError, r);
		} // proc GetResponseDataStream

		#endregion

		#region -- Offline Data -----------------------------------------------------------

		/// <summary></summary>
		/// <param name="path"></param>
		/// <param name="onlineMode"></param>
		/// <param name="contentType"></param>
		/// <param name="data"></param>
		public void UpdateOfflineItem(string path, bool onlineMode, string contentType, Stream data)
		{
		} // proc UpdateOfflineItem

		/// <summary></summary>
		/// <param name="path"></param>
		/// <param name="onlineMode"></param>
		/// <param name="contentType"></param>
		/// <param name="data"></param>
		/// <returns></returns>
		public virtual bool TryGetOfflineItem(string path, bool onlineMode, out string contentType, out Stream data)
		{
			// check if this item is for online&offline mode

			contentType = null;
			data = null;
			return false;
		} // func TryGetOfflineItem

		public IEnumerable<IDataRow> GetOfflineItems()
		{
			yield break;
		} // func GetOfflineItems

		#endregion






		/// <summary>Override to support a better stream of the locally stored data.</summary>
		/// <param name="arguments"></param>
		/// <returns></returns>
		public override IEnumerable<IDataRow> GetViewData(PpsShellGetList arguments)
		{
			var sb = new StringBuilder("remote/?action=viewget&v=");
			sb.Append(arguments.ViewId);

			if (!String.IsNullOrEmpty(arguments.Filter))
				sb.Append("&f=").Append(Uri.EscapeDataString(arguments.Filter));
			if (!String.IsNullOrEmpty(arguments.Order))
				sb.Append("&o=").Append(Uri.EscapeDataString(arguments.Order));
			if (arguments.Start != -1)
				sb.Append("&s=").Append(arguments.Start);
			if (arguments.Count != -1)
				sb.Append("&c=").Append(arguments.Count);

			return Environment.Request.CreateViewDataReader(sb.ToString());

			//// get the table
			//string filterExpression = String.Empty;
			//System.Data.DataTable dt = null;

			//// get the datatable
			//if (!localData.TryGetValue(arguments.ViewId, out dt))
			//{
			//	LoadTestData(@"..\..\..\PPSnDesktop\Local\Data\" + arguments.ViewId + ".xml", ref dt);
			//	localData[arguments.ViewId] = dt;
			//}

			//#region Q&D
			//var filterId = arguments.Filter;
			//if (!String.IsNullOrEmpty(filterId))
			//{
			//	switch (arguments.ViewId.ToLower())
			//	{
			//		case "parts":
			//			if (filterId == "active")
			//				filterExpression = "TEILSTATUS = '10'";
			//			else if (filterId == "inactive")
			//				filterExpression = "TEILSTATUS = '90'";
			//			break;

			//		case "contacts":
			//			if (!String.IsNullOrEmpty(filterId))
			//				if (filterId == "liefonly")
			//					filterExpression = "DEBNR is null";
			//				else if (filterId == "kundonly")
			//					filterExpression = "KREDNR is null";
			//				else if (filterId == "intonly")
			//					filterExpression = "1 = 0";
			//			break;
			//	}
			//}
			//#endregion

			//if (!String.IsNullOrEmpty(arguments.Filter))
			//{
			//	if (filterExpression.Length > 0)
			//		filterExpression += " and OBJKMATCH like '%" + arguments.Filter + "%'";
			//	else
			//		filterExpression += "OBJKMATCH like '%" + arguments.Filter + "%'";
			//}

			//// filter data
			//var orderDef = arguments.Order;
			//if (orderDef != null)
			//	orderDef = orderDef.Replace("+", " asc").Replace("-", " desc");

			//// enumerate lines
			//using (var dv = new System.Data.DataView(dt, filterExpression, orderDef, System.Data.DataViewRowState.CurrentRows))
			//	for (int i = 0; i < arguments.Count; i++)
			//	{
			//		var index = arguments.Start + i;
			//		if (index < dv.Count)
			//			throw new NotImplementedException();
			//	}
			//yield break;
		} // func GetListData

		public override IDataRow GetDetailedData(long objectId, string typ)
		{
			return null;
		} // func GetDetailedData
	} // class PpsLocalDataStore
}
