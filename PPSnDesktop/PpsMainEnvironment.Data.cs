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
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Data;
using TecWare.DE.Networking;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;

namespace TecWare.PPSn
{
	public partial class PpsMainEnvironment
	{
		//private bool TryGetStaticItem(string path, out string contentType, out Stream data)
		//{
		//	// check for a resource file
		//	var baseType = typeof(PpsMainEnvironment);
		//	data = baseType.Assembly.GetManifestResourceStream(baseType, "Static." + path.Replace('/', '.'));
		//	contentType = MimeTypes.Text.Xml;
		//	return data != null;
		//} // func TryGetStaticItem

		//protected override bool TryGetOfflineItem(string path, NameValueCollection arguments, bool onlineMode, out string contentType, out Stream data)
		//{
		//	var r = base.TryGetOfflineItem(path, arguments, onlineMode, out contentType, out data);
		//	if (r)
		//		return r;
		//	else if (path.StartsWith("/wpf/") && !onlineMode) // request could not resolved for the offline item
		//		return TryGetStaticItem(path.Substring(5), out contentType, out data);

		//	return r;
		//} // func TryGetOfflineItem

		internal Task<bool> ShutdownAsync()
		{
			return Task.FromResult<bool>(true);
		}
	} // class PpsMainEnvironment
}
