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
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Xml.Linq;
using TecWare.DE.Stuff;
using TecWare.PPSn.UI;

namespace TecWare.PPSn
{
	#region -- class StuffUI ----------------------------------------------------------

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

		/// <summary></summary>
		/// <param name="owner"></param>
		/// <returns></returns>
		public static ImageSource TakePicture(DependencyObject owner)
			=> PpsCameraDialog.TakePicture(owner);

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
