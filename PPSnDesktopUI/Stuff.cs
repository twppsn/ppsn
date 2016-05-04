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
using System.Linq;
using System.Net;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace TecWare.PPSn
{
	internal static class Stuff
	{
		public static T GetVisualChild<T>(this DependencyObject current)
			where T : DependencyObject
		{
			var c = VisualTreeHelper.GetChildrenCount(current);
			for (int i = 0; i < c; i++)
			{
				var v = VisualTreeHelper.GetChild(current, i);
				var child = v as T;
				if (child != null)
					return child;
				else
				{
					child = GetVisualChild<T>(v);
					if (child != null)
						return child;
				}
			}
			return default(T);
    } // func GetVisualChild
	}
	#region -- class WebRequestHelper ---------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	internal static class WebRequestHelper
	{
		public static ContentDisposition GetContentDisposition(this WebResponse r, bool createDummy = true)
		{
			var tmp = r.Headers["Content-Disposition"];

			if (tmp == null)
			{
				if (createDummy)
				{
					var cd = new ContentDisposition();

					// try to get a filename
					var path = r.ResponseUri.AbsolutePath;
					var pos = -1;
					if (!String.IsNullOrEmpty(path))
						pos = path.LastIndexOf('/', path.Length - 1);
					if (pos >= 0)
						cd.FileName = path.Substring(pos + 1);
					else
						cd.FileName = path;

					// set the date
					cd.ModificationDate = GetLastModified(r);
					return cd;
				}
				else
					return null;
			}
			else
				return new ContentDisposition(tmp);
		} // func GetContentDisposition

		private static DateTime GetLastModified(WebResponse r)
		{
			DateTime lastModified;
			if (!DateTime.TryParse(r.Headers[HttpResponseHeader.LastModified], out lastModified)) // todo: format?
				lastModified = DateTime.Now;
			return lastModified;
		} // func GetLastModified

		public static ContentType GetContentType(this WebResponse r)
		{
			return new ContentType(r.ContentType);
		} // func GetContentType
	} // class WebRequestHelper

	#endregion

}
