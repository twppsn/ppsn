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
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Xml.Linq;
using TecWare.DE.Stuff;

namespace TecWare.PPSn
{
	#region -- class ThreadSafeMonitor ------------------------------------------------

	/// <summary>Build a monitor, that raises an exception, if the exit gets called in the wrong thread.</summary>
	public sealed class ThreadSafeMonitor : IDisposable
	{
		private readonly object threadLock;
		private readonly int threadId;

		private bool isDisposed = false;

		/// <summary>Enter lock</summary>
		/// <param name="threadLock"></param>
		public ThreadSafeMonitor(object threadLock)
		{
			this.threadLock = threadLock;
			this.threadId = Thread.CurrentThread.ManagedThreadId;

			Monitor.Enter(threadLock);
		} // ctor

		/// <summary></summary>
		~ThreadSafeMonitor()
		{
			Dispose(false);
		} // dtor

		/// <summary>Exit lock</summary>
		public void Dispose()
		{
			GC.SuppressFinalize(this);
			Dispose(true);
		} // proc Dispose

		private void Dispose(bool disposing)
		{
			if (disposing)
			{
				if (isDisposed)
					throw new ObjectDisposedException(nameof(ThreadSafeMonitor));
				if (threadId != Thread.CurrentThread.ManagedThreadId)
					throw new ArgumentException();

				Monitor.Exit(threadLock);
			}
			else if (!isDisposed)
			{
				throw new ArgumentException();
			}
		} // proc Dispose
	} // class ThreadSafeMonitor

	#endregion

	#region -- class BooleanBox -------------------------------------------------------

	/// <summary>Boolean box helper</summary>
	public static class BooleanBox
	{
		public static object GetObject(bool value)
			=> value ? True : False;

		public static object GetObject(bool? value)
			=> value.HasValue ? GetObject(value.Value) : null;

		public static bool GetBool(object value)
			=> Object.Equals(value, True);

		public static bool? GetBoolNullable(object value)
			=> value == null ? (bool?)null : Object.Equals(value, True);

		public static object True { get; } = true;
		public static object False { get; } = false;
	} // class BooleanBox

	#endregion

	#region -- class LogicalContentEnumerator -----------------------------------------

	internal class LogicalContentEnumerator : IEnumerator
	{
		private int state = -1;
		private readonly IEnumerator baseItems; // base enumerator
		private readonly object content;
		private readonly Func<object> getContent;

		private LogicalContentEnumerator(IEnumerator baseItems, Func<object> getContent)
		{
			this.baseItems = baseItems;
			this.content = getContent();
			this.getContent = getContent;
		} // ctor

		private object GetContent()
		{
			if (content != getContent())
				throw new InvalidOperationException();
			return content;
		} // func GetContent

		public object Current
			=> state <= 0
				? GetContent()
				: baseItems?.Current;

		public bool MoveNext()
		{
			if (++state <= 0)
				return true;
			else if (state > 0)
				return baseItems?.MoveNext() ?? false;
			return false;
		} // func MoveNext

		public void Reset()
		{
			state = -1;
			baseItems?.Reset();
		} // proc Reset

		internal static IEnumerator GetLogicalEnumerator(DependencyObject d, IEnumerator logicalChildren, Func<object> getContent)
		{
			var content = getContent();
			if (content != null)
			{
				var templatedParent =
					d is FrameworkElement fe
						? fe.TemplatedParent
						: (d is FrameworkContentElement fce ? fce.TemplatedParent : null);

				if (templatedParent != null)
				{
					if (content is DependencyObject obj)
					{
						var p = LogicalTreeHelper.GetParent(obj);
						if (p != null && p != d)
							return logicalChildren;
					}
				}
				return new LogicalContentEnumerator(logicalChildren, getContent);
			}
			return logicalChildren;
		} // func GetLogicalEnumerator
	} // class LogicalElementEnumerator

	#endregion

	#region -- class StuffUI -----------------------------------------------------------

	/// <summary></summary>
	public static class StuffUI
	{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public static readonly XNamespace PresentationNamespace = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
		public static readonly XNamespace XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";
		public static readonly XName xnResourceDictionary = PresentationNamespace + "ResourceDictionary";
		public static readonly XName xnKey = XamlNamespace + "Key";
		public static readonly XName xnCode = XamlNamespace + "Code";
		public static readonly XName xnResources = PresentationNamespace + "resources";

		public static readonly XName xnTheme = "theme";
		public static readonly XName xnTemplates = "templates";
		public static readonly XName xnTemplate = "template";
		public static readonly XName xnCondition = "condition";
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		/// <summary>Search for a Service on an Dependency-object. It will also lookup, all its 
		/// parents on the logical tree.</summary>
		/// <typeparam name="T">Type of the service.</typeparam>
		/// <param name="current">Current object in the logical tree.</param>
		/// <param name="throwException"><c>true</c>, to throw an not found exception.</param>
		/// <returns>The service of the default value.</returns>
		public static T GetControlService<T>(this DependencyObject current, bool throwException = false)
			=> (T)GetControlService(current, typeof(T), throwException);

		/// <summary>Search for a Service on an Dependency-object. It will also lookup, all its
		/// parents in the logical tree.</summary>
		/// <param name="current">Current object in the logical tree.</param>
		/// <param name="serviceType">Type of the service.</param>
		/// <param name="useVisualTree"></param>
		/// <returns>The service of the default value.</returns>
		public static object GetControlService(this DependencyObject current, Type serviceType, bool useVisualTree = false)
		{
			object r = null;

			if (current == null)
				return null;
			else if (current is IServiceProvider sp)
				r = sp.GetService(serviceType);
			else if (serviceType.IsAssignableFrom(current.GetType()))
				r = current;

			if (r != null)
				return r;

			return GetControlService(
				useVisualTree
					? GetVisualParent(current)
					: GetLogicalParent(current), serviceType, useVisualTree
			);
		} // func GetControlService

		/// <summary></summary>
		/// <param name="current"></param>
		/// <returns></returns>
		public static string GetName(this DependencyObject current)
		{
			switch (current)
			{
				case FrameworkElement fe:
					return fe.Name;
				case FrameworkContentElement fce:
					return fce.Name;
				default:
					return null;
			}
		} // func GetName

		/// <summary></summary>
		/// <param name="current"></param>
		/// <param name="name"></param>
		/// <param name="comparison"></param>
		/// <returns></returns>
		public static int CompareName(this DependencyObject current, string name, StringComparison comparison = StringComparison.Ordinal)
			=> String.Compare(GetName(current), name, comparison);

		/// <summary>Get the logical parent or the template parent.</summary>
		/// <param name="current"></param>
		/// <returns></returns>
		public static DependencyObject GetLogicalParent(this DependencyObject current)
		{
			switch (current)
			{
				case FrameworkContentElement fce:
					return fce.Parent ?? fce.TemplatedParent;
				case FrameworkElement fe:
					return fe.Parent ?? fe.TemplatedParent;
				default:
					return null;
			}
		} // func GetLogicalParent

		/// <summary></summary>
		/// <param name="current"></param>
		/// <param name="typeOfParent"></param>
		/// <returns></returns>
		public static DependencyObject GetLogicalParent(this DependencyObject current, Type typeOfParent)
		{
			var parent = GetLogicalParent(current);
			return parent == null || typeOfParent == null || typeOfParent.IsAssignableFrom(parent.GetType())
				? parent
				: GetLogicalParent(parent, typeOfParent);
		} // func GetVisualParent

		/// <summary></summary>
		/// <param name="current"></param>
		/// <param name="name"></param>
		/// <returns></returns>
		public static DependencyObject GetLogicalParent(this DependencyObject current, string name)
		{
			var parent = GetLogicalParent(current);
			return parent == null || CompareName(parent, name) == 0
				? parent
				: GetLogicalParent(parent, name);
		} // func GetVisualParent


		/// <summary>Get the logical parent or the template parent.</summary>
		/// <param name="current"></param>
		/// <returns></returns>
		public static T GetLogicalParent<T>(this DependencyObject current)
			where T : DependencyObject
		{
			var parent = GetLogicalParent(current);
			return parent is T r
				? r
				: GetLogicalParent<T>(parent);
		} // func GetLogicalParent

		/// <summary></summary>
		/// <param name="current"></param>
		/// <returns></returns>
		public static DependencyObject GetVisualParent(this DependencyObject current)
			=> current is Visual || current is Visual3D ? VisualTreeHelper.GetParent(current) : null;

		/// <summary></summary>
		/// <param name="current"></param>
		/// <param name="typeOfParent"></param>
		/// <returns></returns>
		public static DependencyObject GetVisualParent(this DependencyObject current, Type typeOfParent)
		{
			var parent = GetVisualParent(current);
			if (parent == null && current != null && current.GetType().Name == "PopupRoot")
				parent = GetLogicalParent(current);

			return parent == null || typeOfParent == null || typeOfParent.IsAssignableFrom(parent.GetType())
				? parent
				: GetVisualParent(parent, typeOfParent);
		} // func GetVisualParent

		/// <summary></summary>
		/// <param name="current"></param>
		/// <param name="name"></param>
		/// <returns></returns>
		public static DependencyObject GetVisualParent(this DependencyObject current, string name)
		{
			var parent = GetVisualParent(current);
			return parent == null || CompareName(parent, name) == 0
				? parent
				: GetVisualParent(parent, name);
		} // func GetVisualParent

		/// <summary></summary>
		/// <param name="current"></param>
		/// <typeparam name="T"></typeparam>
		/// <returns></returns>
		public static T GetVisualParent<T>(this DependencyObject current)
			where T : DependencyObject
		{
			var parent = GetVisualParent(current);
			return parent is T r
				? r
				: GetVisualParent<T>(parent);
		} // func GetVisualParent

		/// <summary>Find a child in the Visual tree.</summary>
		/// <typeparam name="T">Type of the child</typeparam>
		/// <param name="current">Current visual element.</param>
		/// <returns>Child or <c>null</c>.</returns>
		public static T GetVisualChild<T>(this DependencyObject current)
			where T : DependencyObject
		{
			var c = VisualTreeHelper.GetChildrenCount(current);
			for (var i = 0; i < c; i++)
			{
				var v = VisualTreeHelper.GetChild(current, i);
				if (v is T child)
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

		/// <summary></summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="value"></param>
		/// <returns></returns>
		public static T ChangeTypeWithConverter<T>(this object value)
			=> (T)ChangeTypeWithConverter(value, typeof(T));

		/// <summary></summary>
		/// <param name="value"></param>
		/// <param name="typeTo"></param>
		/// <returns></returns>
		public static object ChangeTypeWithConverter(this object value, Type typeTo)
		{
			if (value == null)
				return Procs.ChangeType(null, typeTo);
			else if (typeTo.IsAssignableFrom(value.GetType()))
				return value;
			else
			{
				var convTo = TypeDescriptor.GetConverter(value.GetType());
				if (convTo.CanConvertTo(typeTo))
					return convTo.ConvertTo(null, CultureInfo.InvariantCulture, value, typeTo);
				else
				{
					var convFrom = TypeDescriptor.GetConverter(typeTo);
					if (convFrom.CanConvertFrom(value.GetType()))
						return convFrom.ConvertFrom(null, CultureInfo.InvariantCulture, value);
					else
						return Procs.ChangeType(value, typeTo);
				}
			}
		} // func ChangeTypeWithConverter

		internal static void PrintVisualTreeToConsole(DependencyObject current)
		{
			Debug.Print("Visual Tree:");
			while (current != null)
			{
				Debug.Print("V {0}: {1}", current.GetType().Name, current.GetName() ?? "<null>");
				current = GetVisualParent(current);
			}
		} // proc PrintVisualTreeToConsole

		internal static void PrintLogicalTreeToConsole(DependencyObject current)
		{
			Debug.Print("Logical Tree:");
			while (current != null)
			{
				Debug.Print("L {0}: {1}", current.GetType().Name, current.GetName() ?? "<null>");
				current = GetLogicalParent(current);
			}
		} // proc PrintVisualTreeToConsole

		private static DependencyObject InvokeGetUIParent<T>(DependencyObject current)
			where T : class
		{
			var mi = typeof(T).GetMethod("GetUIParentCore", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.InvokeMethod, null, Array.Empty<Type>(), null);
			return (DependencyObject)mi.Invoke(current, Array.Empty<object>());
		} // proc InvokeGetUIParent

		private static DependencyObject GetUIParent(DependencyObject current)
		{
			switch (current)
			{
				case UIElement ui:
					return InvokeGetUIParent<UIElement>(current);
				case UIElement3D ui3d:
					return InvokeGetUIParent<UIElement3D>(current);
				case ContentElement c:;
					return InvokeGetUIParent<ContentElement>(current);
				default:
					return null;
			}
		} // func GetUIParent

		internal static void PrintEventTreeToConsole(DependencyObject current)
		{
			Debug.Print("UI Tree:");
			while (current != null)
			{
				Debug.Print("U {0}: {1}", current.GetType().Name, current.GetName() ?? "<null>");
				current = GetUIParent(current);
			}
		} // proc PrintVisualTreeToConsole

		#region -- remove after update DES --

		#endregion
	} // class StuffUI

	#endregion

	#region -- class StuffDB ------------------------------------------------------------

	/// <summary>Db-Extensions</summary>
	public static class StuffDB
	{
		/// <summary>Key for extended exception attribute.</summary>
		public const string CommandTextKey = "CommandText";

		/// <summary>Add parameter to an DbCommand</summary>
		/// <param name="command"></param>
		/// <param name="parameterName"></param>
		/// <returns></returns>
		public static DbParameter AddParameter(this DbCommand command, string parameterName)
		{
			var param = command.CreateParameter();
			param.ParameterName = parameterName;
			command.Parameters.Add(param);
			return param;
		} // func AddParameter

		/// <summary>Add parameter to an DbCommand</summary>
		/// <param name="command"></param>
		/// <param name="parameterName"></param>
		/// <param name="dbType"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public static DbParameter AddParameter(this DbCommand command, string parameterName, DbType dbType, object value = null)
		{
			var param = AddParameter(command, parameterName);
			param.DbType = dbType;
			param.Value = value;
			return param;
		} // func AddParameter

		/// <summary>Execute with CommandText-Attribute.</summary>
		/// <param name="command"></param>
		/// <returns></returns>
		public static int ExecuteNonQueryEx(this DbCommand command)
		{
			try
			{
				return command.ExecuteNonQuery();
			}
			catch (DbException e)
			{
				e.UpdateExceptionWithCommandInfo(command);
				throw;
			}
		} // func ExecuteReaderEx

		/// <summary>Execute with CommandText-Attribute.</summary>
		/// <param name="command"></param>
		/// <returns></returns>
		public static async Task<int> ExecuteNonQueryExAsync(this DbCommand command)
		{
			try
			{
				return await command.ExecuteNonQueryAsync();
			}
			catch (DbException e)
			{
				e.UpdateExceptionWithCommandInfo(command);
				throw;
			}
		} // func ExecuteReaderEx

		/// <summary>Execute with CommandText-Attribute.</summary>
		/// <param name="command"></param>
		/// <param name="commandBehavior"></param>
		/// <returns></returns>
		public static DbDataReader ExecuteReaderEx(this DbCommand command, CommandBehavior commandBehavior = CommandBehavior.Default)
		{
			try
			{
				return command.ExecuteReader(commandBehavior);
			}
			catch (DbException e)
			{
				e.UpdateExceptionWithCommandInfo(command);
				throw;
			}
		} // func ExecuteReaderEx

		/// <summary>Execute with CommandText-Attribute.</summary>
		/// <param name="command"></param>
		/// <param name="commandBehavior"></param>
		/// <returns></returns>
		public static async Task<DbDataReader> ExecuteReaderExAsync(this DbCommand command, CommandBehavior commandBehavior = CommandBehavior.Default)
		{
			try
			{
				return await command.ExecuteReaderAsync(commandBehavior);
			}
			catch (DbException e)
			{
				e.UpdateExceptionWithCommandInfo(command);
				throw;
			}
		} // func ExecuteReaderEx

		/// <summary>Execute with CommandText-Attribute.</summary>
		/// <param name="command"></param>
		/// <returns></returns>
		public static object ExecuteScalarEx(this DbCommand command)
		{
			try
			{
				return command.ExecuteScalar();
			}
			catch (DbException e)
			{
				e.UpdateExceptionWithCommandInfo(command);
				throw;
			}
		} // func ExecuteScalarEx

		/// <summary>Execute with CommandText-Attribute.</summary>
		/// <param name="command"></param>
		/// <returns></returns>
		public static async Task<object> ExecuteScalarExAsync(this DbCommand command)
		{
			try
			{
				return await command.ExecuteScalarAsync();
			}
			catch (DbException e)
			{
				e.UpdateExceptionWithCommandInfo(command);
				throw;
			}
		} // func ExecuteScalarEx

		/// <summary>Add CommandText-Attribute to the exception.</summary>
		/// <param name="e"></param>
		/// <param name="command"></param>
		/// <returns></returns>
		public static void UpdateExceptionWithCommandInfo(this Exception e, DbCommand command)
		{
			var ret = command.CommandText;
			foreach (var parameter in command.Parameters.Cast<DbParameter>())
				ret = ret.Replace(parameter.ParameterName, parameter.Value.ToString());
			e.Data[CommandTextKey] = ret;
		} // proc UpdateExceptionWithCommandInfo

		/// <summary></summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static bool DbNullOnNeg(long value)
			=> value < 0;

		/// <summary></summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static object DbNullIfString(this string value)
			=> String.IsNullOrEmpty(value) ? (object)DBNull.Value : value;

		/// <summary></summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="value"></param>
		/// <param name="null"></param>
		/// <returns></returns>
		public static object DbNullIf<T>(this T value, T @null)
			=> Object.Equals(value, @null) ? (object)DBNull.Value : value;

		/// <summary></summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="value"></param>
		/// <param name="null"></param>
		/// <returns></returns>
		public static object DbNullIf<T>(this T value, Func<T, bool> @null)
			=> @null(value) ? (object)DBNull.Value : value;

		/// <summary></summary>
		/// <param name="r"></param>
		/// <param name="columnName"></param>
		/// <param name="throwException"></param>
		/// <returns></returns>
		public static int FindColumnIndex(this IDataRecord r, string columnName, bool throwException = true)
		{
			for (var i = 0; i < r.FieldCount; i++)
			{
				if (String.Compare(r.GetName(i), columnName, StringComparison.OrdinalIgnoreCase) == 0)
					return i;
			}
			if (throwException)
				throw new ArgumentOutOfRangeException(nameof(columnName), columnName, $"Column '{columnName}' not found.");
			return -1;
		} // func FindColumnIndex

		/// <summary></summary>
		/// <param name="r"></param>
		/// <param name="throwException"></param>
		/// <param name="columnNames"></param>
		/// <returns></returns>
		public static int[] FindColumnIndices(this IDataRecord r, bool throwException, params string[] columnNames)
		{
			// init result
			var idx = new int[columnNames.Length];
			for (var i = 0; i < idx.Length; i++)
				idx[i] = -1;

			// match columns
			for (var i = 0; i < r.FieldCount; i++)
			{
				var n = r.GetName(i);
				var j = Array.FindIndex(columnNames, c => String.Compare(n, c, StringComparison.OrdinalIgnoreCase) == 0);
				if (j != -1)
					idx[j] = i;
			}

			// return values
			for (var i = 0; i < idx.Length; i++)
			{
				if (idx[i] == -1)
					throw new ArgumentOutOfRangeException(nameof(columnNames), columnNames[i], $"Column '{columnNames[i]}' not found.");
			}

			return idx;
		} // func FindColumnIndices
	} // class StuffDB

	#endregion

	#region -- class StuffIO ------------------------------------------------------------

	/// <summary></summary>
	public static class StuffIO
	{
		/// <summary>We only use sha256, the prefix should be useful, if the algorithm will be changed in future.</summary>
		/// <returns></returns>
		public static string GetHashPrefix(HashAlgorithm algorithm)
		{
			if (algorithm is SHA256
				|| algorithm is SHA256Managed)
				return "sha256";
			else
				throw new ArgumentOutOfRangeException(nameof(algorithm), "Only sha256 is allowed.");
		} // func GetHashPrefix

		/// <summary>Build hash information from hash.</summary>
		/// <param name="hash"></param>
		/// <param name="algorithm"></param>
		/// <returns></returns>
		[Obsolete("Not compatible with server")]
		public static string ConvertHashToString(HashAlgorithm algorithm, byte[] hash)
		{
			if (hash == null || hash.Length == 0)
				return null;

			var sb = new StringBuilder(GetHashPrefix(algorithm)).Append(':');
			for (var i = 0; i < hash.Length; i++)
				sb.Append(hash[i].ToString("X2"));
			return sb.ToString();
		} // func ConvertHashToString

		/// <summary>Convert a hash string to the algorithm and hash value.</summary>
		/// <param name="hashString"></param>
		/// <param name="algorithm"></param>
		/// <param name="hash"></param>
		/// <returns></returns>
		public static bool TryConvertStringToHash(string hashString, out HashAlgorithm algorithm, out byte[] hash)
		{
			throw new NotImplementedException();
		} // func TryConvertStringToHash
	} // class StuffIO

	#endregion

	#region -- class WebRequestHelper ---------------------------------------------------

	/// <summary></summary>
	public static class WebRequestHelper
	{
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

		/// <summary>Compares two Uri</summary>
		/// <param name="uri1">first Uri</param>
		/// <param name="uri2">second Uri</param>
		/// <returns>return true if both Uris have the same result</returns>
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

		private static readonly NameValueCollection emptyCollection = new NameValueCollection();
	} // class WebRequestHelper

	#endregion

	#region -- class PpsUserException -------------------------------------------------

	/// <summary></summary>
	public sealed class PpsUserException : Exception, ILuaUserRuntimeException
	{
		/// <summary></summary>
		/// <param name="message"></param>
		/// <param name="innerException"></param>
		public PpsUserException(string message, Exception innerException)
			: base(message, innerException)
		{
		}
	} // class PpsUserException

	#endregion

	#region -- class PpsCircularView --------------------------------------------------

	/// <summary>Ring list over a list.</summary>
	public sealed class PpsCircularView : IList, INotifyCollectionChanged
	{
		/// <summary>Notify changed.</summary>
		public event NotifyCollectionChangedEventHandler CollectionChanged;

		private readonly IList itemList;
		private readonly int maxViewCount;

		private int currentItemListCount = 0;
		private int currentCount = 0;
		private int currentMitte = 0;
		private int currentPosition = 0;

		/// <summary></summary>
		/// <param name="itemList"></param>
		/// <param name="maxViewCount"></param>
		public PpsCircularView(IList itemList, int maxViewCount)
		{
			this.itemList = itemList;
			this.maxViewCount = maxViewCount;
			ResetList();

			if (itemList is INotifyCollectionChanged collectionChanged)
				collectionChanged.CollectionChanged += CollectionChanged_CollectionChanged;
		} // ctor

		private void CollectionChanged_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			switch (e.Action)
			{
				case NotifyCollectionChangedAction.Add:
				case NotifyCollectionChangedAction.Remove:
				case NotifyCollectionChangedAction.Reset:
				case NotifyCollectionChangedAction.Move:
					ResetList();
					break;
				case NotifyCollectionChangedAction.Replace:
					var idx = GetRealIndex(e.NewStartingIndex);
					if (idx >= 0 && idx < currentCount)
						CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, e.NewItems, e.OldItems, idx));
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
		} // event CollectionChanged_CollectionChanged

		private void ResetList()
		{
			var oldSelectedIndex = GetRealIndex(currentMitte);

			currentItemListCount = itemList.Count;
			currentCount = maxViewCount > currentItemListCount ? currentItemListCount : maxViewCount;
			currentMitte = currentCount / 2;

			if (oldSelectedIndex >= currentItemListCount)
				Move(currentItemListCount - oldSelectedIndex - 1, false);

			CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
		} // proc ResetList

		/// <summary>Copy the items from the circular view.</summary>
		/// <param name="array"></param>
		/// <param name="index"></param>
		public void CopyTo(Array array, int index)
		{
			var realIndex = GetRealIndex(0);
			for (var i = 0; i < currentCount; i++)
			{
				array.SetValue(itemList[realIndex], index++);
				if (realIndex >= currentItemListCount)
					realIndex = 0;
			}
		} // proc CopyTo

		/// <summary>Get the index in the circular view.</summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public int IndexOf(object value)
		{
			var virtualIndex = GetVirtualIndex(value);
			return virtualIndex >= 0 && virtualIndex < currentCount
				? virtualIndex
				: -1;
		} // func IndexOf

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private int GetVirtualIndex(object value)
		{
			var realIndex = itemList.IndexOf(value);
			var virtualIndex = realIndex - currentPosition + currentMitte;

			if (virtualIndex < 0)
				virtualIndex = currentItemListCount - virtualIndex;
			else if (virtualIndex >= currentItemListCount)
				virtualIndex = virtualIndex - currentItemListCount;
			return virtualIndex;
		} // func GetVirtualIndex

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private int GetRealIndex(int virtualIndex)
		{
			var realIndex = currentPosition - currentMitte + virtualIndex;

			if (realIndex < 0)
				realIndex = currentItemListCount + realIndex;
			else if (realIndex >= currentItemListCount)
				realIndex = realIndex - currentItemListCount;
			return realIndex;
		} // func GetRealIndex

		/// <summary>Is the item in the circular view.</summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public bool Contains(object value)
			=> itemList.Contains(value);

		/// <summary>Enumerate the visible items.</summary>
		/// <returns></returns>
		public IEnumerator GetEnumerator()
		{
			var realIndex = GetRealIndex(0);
			for (var virtualIndex = 0; virtualIndex < currentCount; virtualIndex++)
			{
				yield return itemList[realIndex];

				realIndex++;
				if (realIndex >= currentItemListCount)
					realIndex = 0;
			}
		} // func GetEnumerator

		/// <summary>Move the view over the base list.</summary>
		/// <param name="relative"></param>
		/// <param name="notifyChanges"></param>
		public void Move(int relative, bool notifyChanges = true)
		{
			if (relative == 0)
				return;

			var newPos = currentPosition + relative;

			// move into view
			while (newPos >= currentItemListCount)
				newPos = newPos - currentItemListCount;
			while (newPos < 0)
				newPos = newPos + currentItemListCount;

			// something changed
			if (newPos == currentPosition)
				return;

			var oldIndex = GetRealIndex(0);
			currentPosition = newPos;
			var newIndex = GetRealIndex(0);

			for (var i = 0; i < currentCount; i++)
			{
				if (notifyChanges)
					CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, itemList[newIndex], itemList[oldIndex], i));

				if (++oldIndex >= currentItemListCount)
					oldIndex = 0;
				if (++newIndex >= currentItemListCount)
					newIndex = 0;
			}
		} // proc Move

		/// <summary>Move the base list to an object (the object will be the first).</summary>
		/// <param name="value"></param>
		public bool MoveTo(object value)
		{
			var realIndex = itemList.IndexOf(value);
			if (realIndex >= 0)
			{
				Move(realIndex - GetRealIndex(currentMitte));
				return true;
			}
			else
				return false;
		} // proc MoveTo

		/// <summary>Make virtual index to the first.</summary>
		/// <param name="virtualIndex"></param>
		public void MoveTo(int virtualIndex)
			=> Move(GetRealIndex(virtualIndex) - currentPosition);

		int IList.Add(object value) => throw new NotSupportedException();
		void IList.Clear() => throw new NotSupportedException();
		void IList.Insert(int index, object value) => throw new NotSupportedException();
		void IList.Remove(object value) => throw new NotSupportedException();
		void IList.RemoveAt(int index) => throw new NotSupportedException();

		/// <summary></summary>
		public bool IsReadOnly => true;
		/// <summary></summary>
		public bool IsFixedSize => true;
		/// <summary></summary>
		public object SyncRoot => null;
		/// <summary></summary>
		public bool IsSynchronized => false;

		/// <summary></summary>
		/// <param name="index"></param>
		/// <returns></returns>
		public object this[int index] { get => itemList[GetRealIndex(index)]; set => throw new NotSupportedException(); }

		/// <summary></summary>
		public int Count => currentCount;
		/// <summary></summary>
		public object CurrentItem => this[currentMitte];
	} // class PpsCircularView

	#endregion
}
