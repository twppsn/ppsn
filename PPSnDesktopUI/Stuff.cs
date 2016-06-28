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
using System.Windows.Markup;
using System.Windows.Media;
using System.Xml;
using System.Xml.Linq;
using TecWare.PPSn.Stuff;

namespace TecWare.PPSn
{
	internal static class StuffUI
	{
		public static readonly XNamespace PresentationNamespace = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
		public static readonly XNamespace XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";
		public static readonly XName xnResourceDictionary = PresentationNamespace + "ResourceDictionary";
		public static readonly XName xnKey = XamlNamespace + "Key";
		public static readonly XName xnCode = XamlNamespace + "Code";

		public static void AddNamespace(this ParserContext context, string namespacePrefix, string namepsaceName)
		{
			if (namepsaceName != PpsXmlPosition.XmlPositionNamespace.NamespaceName)
			{
				if (namespacePrefix == "xmlns")
					namespacePrefix = String.Empty; // must be empty
				context.XmlnsDictionary.Add(namespacePrefix, namepsaceName);
			}
		} // proc AddNamespace

		public static void AddNamespaces(this ParserContext context, XmlReader xr)
		{
			if (xr.HasAttributes)
			{
				while (xr.MoveToNextAttribute())
					AddNamespace(context, xr.LocalName, xr.Value);
			}
			xr.MoveToElement();
		} // proc CollectNameSpaces

		public static void AddNamespaces(this ParserContext context, XElement x)
		{
			foreach (XAttribute xAttr in x.Attributes())
			{
				if (xAttr.IsNamespaceDeclaration)
					AddNamespace(context, xAttr.Name.LocalName, xAttr.Value);
			}
		} // proc CollectNameSpaces

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
	} // class StuffUI

	#region -- class StuffDB ------------------------------------------------------------

	public static class StuffDB
	{
		public static object DbNullIfString(this string value)
			=> String.IsNullOrEmpty(value) ? (object)DBNull.Value : value;

		public static object DbNullIf<T>(this T value, T @null)
			=> Object.Equals(value, @null) ? (object)DBNull.Value : value;
	} // class StuffDB

	#endregion

	#region -- class WebRequestHelper ---------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public static class WebRequestHelper
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

		public static DateTime GetLastModified(this WebResponse r)
		{
			DateTime lastModified;
			if (!DateTime.TryParse(r.Headers[HttpResponseHeader.LastModified], out lastModified)) // todo: format?
				lastModified = DateTime.Now;
			return lastModified;
		} // func GetLastModified

		public static ContentType GetContentType(this WebResponse r)
			=> new ContentType(r.ContentType);
	} // class WebRequestHelper

	#endregion
}
