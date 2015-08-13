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
using TecWare.DES.Stuff;

namespace TecWare.PPSn.Data
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PpsLocalDataStore : PpsDataStore
	{
		public PpsLocalDataStore(PpsEnvironment environment)
			: base(environment)
		{
		} // ctor

		protected override void GetResponseDataStream(PpsStoreResponse r)
		{
			var fi = new FileInfo(Path.Combine(@"..\..\Local", r.Request.Path.Substring(1).Replace('/', '\\')));
			if (!fi.Exists)
				throw new WebException("File not found.", WebExceptionStatus.ProtocolError);

			var contentType = String.Empty;
			if (fi.Extension == ".xaml")
				contentType = MimeTypes.Xaml;
			else if (fi.Extension == ".lua")
				contentType = MimeTypes.Text;

			r.SetResponseData(fi.OpenRead(), contentType);
		} // func GetResponseDataStream
	} // class PpsLocalDataStore
}
