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
using System.Collections.Specialized;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Net;
using System.Net.Mime;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using System.Xml;
using System.Xml.Linq;
using TecWare.DE.Networking;
using TecWare.DE.Stuff;
using TecWare.PPSn.Stuff;

namespace TecWare.PPSn
{
	#region -- enum HashStreamDirection --------------------------------------------------

	public enum HashStreamDirection
	{
		Read,
		Write
	} // enum HashStreamDirection

	#endregion

	#region -- class HashStream ----------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class HashStream : Stream
	{
		private readonly Stream baseStream;
		private readonly bool leaveOpen;

		private readonly HashStreamDirection direction;
		private readonly HashAlgorithm hashAlgorithm;
		private bool isFinished = false;

		public HashStream(Stream baseStream, HashStreamDirection direction, bool leaveOpen, HashAlgorithm hashAlgorithm)
		{
			this.baseStream = baseStream ?? throw new ArgumentNullException(nameof(baseStream));
			this.leaveOpen = leaveOpen;
			this.hashAlgorithm = hashAlgorithm ?? throw new ArgumentNullException(nameof(hashAlgorithm));
			this.direction = direction;

			if (direction == HashStreamDirection.Write && !baseStream.CanWrite)
				throw new ArgumentException("baseStream is not writeable.");
			if (direction == HashStreamDirection.Read && !baseStream.CanRead)
				throw new ArgumentException("baseStream is not readable.");
		} // ctor

		protected override void Dispose(bool disposing)
		{
			if (disposing && !leaveOpen)
				baseStream?.Close();

			base.Dispose(disposing);
		} // proc Dispose

		public override void Flush()
			=> baseStream.Flush();

		public override int Read(byte[] buffer, int offset, int count)
		{
			if (direction != HashStreamDirection.Read)
				throw new NotSupportedException("The stream is in read mode.");
			else if (isFinished)
				return 0;

			var readed = baseStream.Read(buffer, offset, count);
			if (readed == 0 || baseStream.CanSeek && baseStream.Position == baseStream.Length)
			{
				FinalBlock(buffer, offset, readed);
				isFinished = true;
			}
			else
				hashAlgorithm.TransformBlock(buffer, offset, readed, buffer, offset);

			return readed;
		} // func Read

		public override void Write(byte[] buffer, int offset, int count)
		{
			if (direction != HashStreamDirection.Write)
				throw new NotSupportedException("The stream is in write mode.");
			else if (isFinished)
				throw new InvalidOperationException("Stream is finished.");

			baseStream.Write(buffer, offset, count);

			if (count == 0)
				FinalBlock(buffer, offset, count);
			else
				hashAlgorithm.TransformBlock(buffer, offset, count, buffer, offset);
		} // proc Write

		public byte[] CalcHash()
		{
			if (!isFinished)
				FinalBlock(Array.Empty<byte>(), 0, 0);

			return hashAlgorithm.Hash;
		} // func CalcHash

		private void FinalBlock(byte[] buffer, int offset, int count)
		{
			hashAlgorithm.TransformFinalBlock(buffer, offset, count);
			isFinished = true;

			OnFinished(hashAlgorithm.Hash);
		} // proc FinalBlock

		protected virtual void OnFinished(byte[] bCheckSum)
		{
		} // proc Finished

		public override long Seek(long offset, SeekOrigin origin)
			=> throw new NotSupportedException();

		public override void SetLength(long value)
		{
			if (direction == HashStreamDirection.Write)
				baseStream.SetLength(value);
			else
				throw new NotSupportedException();
		} // proc SetLength

		public override bool CanRead => direction == HashStreamDirection.Read;
		public override bool CanWrite => direction == HashStreamDirection.Write;
		public override bool CanSeek => false;
		public override long Length { get { return baseStream.Length; } }
		public override long Position { get { return baseStream.Position; } set { throw new NotSupportedException(); } }

		public Stream BaseStream => baseStream;
		public HashAlgorithm HashAlgorithm => hashAlgorithm;

		public bool IsFinished => isFinished;

		public byte[] CheckSum => isFinished ? hashAlgorithm.Hash : null;
	} // class HashStream

	#endregion

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
			else if (frameworkElement.GetType().IsAssignableFrom(serviceType))
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
		public const string CommandTextKey = "CommandText";

		public static DbParameter AddParameter(this DbCommand command, string parameterName)
		{
			var param = command.CreateParameter();
			param.ParameterName = parameterName;
			command.Parameters.Add(param);
			return param;
		} // func AddParameter

		public static DbParameter AddParameter(this DbCommand command, string parameterName, DbType dbType, object value = null)
		{
			var param = AddParameter(command, parameterName);
			param.DbType = dbType;
			param.Value = value;
			return param;
		} // func AddParameter

		public static int ExecuteNonQueryEx(this DbCommand command)
		{
			try
			{
				return command.ExecuteNonQuery();
			}
			catch (DbException e)
			{
				e.Data[CommandTextKey] = InsertDbParams(command);
				throw;
			}
		} // func ExecuteReaderEx

		public static async Task<int> ExecuteNonQueryExAsync(this DbCommand command)
		{
			try
			{
				return await command.ExecuteNonQueryAsync();
			}
			catch (DbException e)
			{
				e.Data[CommandTextKey] = InsertDbParams(command);
				throw;
			}
		} // func ExecuteReaderEx

		public static DbDataReader ExecuteReaderEx(this DbCommand command, CommandBehavior commandBehavior = CommandBehavior.Default)
		{
			try
			{
				return command.ExecuteReader(commandBehavior);
			}
			catch (DbException e)
			{
				e.Data[CommandTextKey] = InsertDbParams(command);
				throw;
			}
		} // func ExecuteReaderEx

		public static async Task<DbDataReader> ExecuteReaderExAsync(this DbCommand command, CommandBehavior commandBehavior = CommandBehavior.Default)
		{
			try
			{
				return await command.ExecuteReaderAsync(commandBehavior);
			}
			catch (DbException e)
			{
				e.Data[CommandTextKey] = InsertDbParams(command);
				throw;
			}
		} // func ExecuteReaderEx

		public static object ExecuteScalarEx(this DbCommand command)
		{
			try
			{
				return command.ExecuteScalar();
			}
			catch (DbException e)
			{
				e.Data[CommandTextKey] = InsertDbParams(command);
				throw;
			}
		} // func ExecuteScalarEx

		public static async Task<object> ExecuteScalarExAsync(this DbCommand command)
		{
			try
			{
				return await command.ExecuteScalarAsync();
			}
			catch (DbException e)
			{
				e.Data[CommandTextKey] = InsertDbParams(command);
				throw;
			}
		} // func ExecuteScalarEx

		public static bool DbNullOnNeg(long value)
			=> value < 0;

		public static object DbNullIfString(this string value)
			=> String.IsNullOrEmpty(value) ? (object)DBNull.Value : value;

		public static object DbNullIf<T>(this T value, T @null)
			=> Object.Equals(value, @null) ? (object)DBNull.Value : value;

		public static object DbNullIf<T>(this T value, Func<T, bool> @null)
			=> @null(value) ? (object)DBNull.Value : value;

		private static string InsertDbParams(DbCommand cmd)
		{
			var ret = cmd.CommandText;
			foreach (DbParameter parameter in cmd.Parameters)
				ret = ret.Replace(parameter.ParameterName, parameter.Value.ToString());

			return ret;
		}
	} // class StuffDB

	#endregion

	#region -- class StuffIO ------------------------------------------------------------

	public static class StuffIO
	{
		public static string GetFileHash(string filename)
		{
			using (FileStream filestream = File.OpenRead(filename))
			{
				return GetStreamHash(filestream);
			}
		}

		public static string GetStreamHash(Stream stream)
		{
			var bstream = new BufferedStream(stream, 1024 * 32); // no using, because we want to keep the stream alive
			var ret = BitConverter.ToString(new SHA256Managed().ComputeHash(bstream)).Replace("-", String.Empty).ToLower();
			bstream.Flush();
			return ret;
		}

		public static string CleanHash(string hash)
		{
			return hash.Replace("-", String.Empty).ToLower();
		}

		#region ---- MimeTypes ----------------------------------------------------------

		private static (string Extension, string MimeType)[] mimeTypeMapping =
		{
			("css", MimeTypes.Text.Css),
			("exe", MimeTypes.Application.OctetStream),
			("gif", MimeTypes.Image.Gif),
			("htm", MimeTypes.Text.Html),
			("html", MimeTypes.Text.Html),
			("ico", MimeTypes.Image.Icon),
			("js", MimeTypes.Text.JavaScript),
			("jpeg", MimeTypes.Image.Jpeg),
			("jpg", MimeTypes.Image.Jpeg),
			("json", MimeTypes.Text.Json),
			("log", MimeTypes.Text.Plain),
			("lua", MimeTypes.Text.Lua),
			("png", MimeTypes.Image.Png),
			("txt", MimeTypes.Text.Plain),
			("xaml", MimeTypes.Application.Xaml),
			("xml", MimeTypes.Text.Xml),
		};

		private static string DefaultMimeType => MimeTypes.Application.OctetStream;

		private static int FindTypeMappingByExtension(string extension)
			=> Array.FindIndex(mimeTypeMapping, mt => mt.Extension == extension.TrimStart('.'));

		private static int FindTypeMappingByMimeType(string mimeType)
			=> Array.FindIndex(mimeTypeMapping, mt => mt.MimeType == mimeType);

		public static string MimeTypeFromExtension(string extension)
		{
			var typeIndex = FindTypeMappingByExtension(extension);
			return (typeIndex >= 0) ? mimeTypeMapping[typeIndex].MimeType : DefaultMimeType;
		}

		public static string MimeTypeFromFilename(string filename)
		{
			return MimeTypeFromExtension(Path.GetExtension(filename));
		}

		#endregion

	}

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
			=> DateTime.TryParse(headers[HttpResponseHeader.LastModified], out var lastModified) ? lastModified : DateTime.Now; // todo: format?

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
