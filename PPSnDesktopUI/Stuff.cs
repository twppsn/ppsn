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
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Net;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using System.Web;
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
			foreach (var xAttr in x.Attributes())
			{
				if (xAttr.IsNamespaceDeclaration)
					AddNamespace(context, xAttr.Name.LocalName, xAttr.Value);
			}
		} // proc CollectNameSpaces

		public static object GetControlService(this FrameworkElement frameworkElement, Type serviceType)
		{
			object r = null;

			if (frameworkElement is IServiceProvider sp)
				r = sp.GetService(serviceType);
			else  if (frameworkElement.GetType().IsAssignableFrom(serviceType))
				r = frameworkElement;

			return GetControlService(frameworkElement.Parent as FrameworkElement, serviceType);
		} // func GetControlService

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
		public static DbParameter AddParameter(this DbCommand command, string parameterName, DbType dbType, object value = null)
		{
			var param = command.CreateParameter();
			param.ParameterName = parameterName;
			param.DbType = dbType;
			param.Value = value;
			return param;
		} // func AddParameter

		public static DbDataReader ExecuteReaderEx(this DbCommand command, CommandBehavior commandBehavior = CommandBehavior.Default)
		{
			try
			{
				return command.ExecuteReader(commandBehavior);
			}
			catch (DbException e)
			{
				e.Data["CommandText"] = command.CommandText;
				throw;
			}
		} // func ExecuteReaderEx

		public static bool DbNullOnNeg(long value)
			=> value < 0;

		public static object DbNullIfString(this string value)
			=> String.IsNullOrEmpty(value) ? (object)DBNull.Value : value;

		public static object DbNullIf<T>(this T value, T @null)
			=> Object.Equals(value, @null) ? (object)DBNull.Value : value;

		public static object DbNullIf<T>(this T value, Func<T, bool> @null)
			=> @null(value) ? (object)DBNull.Value : value;
	} // class StuffDB

	#endregion

	#region -- class WebRequestHelper ---------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public static class WebRequestHelper
	{
		public static ContentDisposition GetContentDisposition(this WebResponse r, bool createDummy = true)
		{
			//var tmp = r.Headers["Content-Disposition"];

			//if (tmp == null)
			//{
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
			//}
			//else
			//	return new ContentDisposition(tmp);
		} // func GetContentDisposition

		public static DateTime GetLastModified(this WebHeaderCollection headers)
		{
			DateTime lastModified;
			if (!DateTime.TryParse(headers[HttpResponseHeader.LastModified], out lastModified)) // todo: format?
				lastModified = DateTime.Now;
			return lastModified;
		} // func GetLastModified

		public static DateTime GetLastModified(this WebResponse r)
			=> GetLastModified(r.Headers);

		public static ContentType GetContentType(this WebResponse r)
			=> new ContentType(r.ContentType);

		public static NameValueCollection ParseQuery(this Uri uri)
			=> uri.IsAbsoluteUri
				? HttpUtility.ParseQueryString(uri.Query)
				: ParseQuery(uri.OriginalString);
		
		public static NameValueCollection ParseQuery(string uri)
		{
			var pos = uri.IndexOf('?');
			return pos == -1
				? emptyCollection
				: HttpUtility.ParseQueryString(uri.Substring(pos + 1));
		} // func ParseQuery

		public static string ParsePath(this Uri uri)
			=> uri.IsAbsoluteUri
				? uri.AbsolutePath
				: ParsePath(uri.OriginalString);

		public static string ParsePath(string uri)
		{
			var pos = uri.IndexOf('?');
			return pos == -1 ? uri : uri.Substring(0, pos);
		} // func ParsePath

		public static (string path, NameValueCollection arguments) ParseUri(this Uri uri)
			=> uri.IsAbsoluteUri
				? (uri.AbsolutePath, HttpUtility.ParseQueryString(uri.Query))
				: ParseUri(uri.OriginalString);

		public static (string path, NameValueCollection arguments) ParseUri(string uri)
		{
			var pos = uri.IndexOf('?');
			return pos == -1
				? (uri, emptyCollection)
				: (uri.Substring(0, pos), HttpUtility.ParseQueryString(uri.Substring(pos + 1)));
		} // func ParseUri

		public static bool EqualUri(Uri uri1, Uri uri2)
		{
			if (uri1.IsAbsoluteUri && uri2.IsAbsoluteUri)
				return uri1.Equals(uri2);
			else if (uri1.IsAbsoluteUri || uri2.IsAbsoluteUri)
				return false;
			else
			{
				(var path1, var args1) = uri1.ParseUri();
				(var path2, var args2) = uri2.ParseUri();

				if (path1 == path2 && args1.Count == args2.Count)
				{
					foreach (var k in args1.AllKeys)
					{
						if (args1[k] != args2[k])
							return false;
					}
					return true;
				}
				else
					return false;
			}
		} // func EqualUri

		private static NameValueCollection emptyCollection = new NameValueCollection();
	} // class WebRequestHelper

	#endregion
}
