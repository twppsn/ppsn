﻿#region -- copyright --
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
using System.Web.Caching;
using TecWare.DE.Data;
using TecWare.PPSn.Data;

namespace TecWare.PPSn.Server.Data
{
	#region -- enum PpsSynchonizationMode ---------------------------------------------

	/// <summary>Synchronization state.</summary>
	public enum PpsSynchonizationMode
	{
		/// <summary>Unknown, call the synchronization function.</summary>
		Unknown = -1,
		/// <summary>No synchronization needed.</summary>
		None,
		/// <summary>Full synchronization is scheduled.</summary>
		Full,
		/// <summary>Diff synchronization is scheduled.</summary>
		Parts
	} // enum PpsSynchonizationMode

	#endregion

	#region -- interface IPpsDataSynchronizationBatch ---------------------------------

	/// <summary>Needs to be implemented by a synchronization batch part.</summary>
	public interface IPpsDataSynchronizationBatch : IEnumerator<IDataRow>, IDataColumns
	{
		/// <summary>Current sync id of current row.</summary>
		long CurrentSyncId { get; }
		/// <summary>Update mode i,u,d,r of the current row.</summary>
		char CurrentMode { get; }
		/// <summary>Is the a whole batch part a full sync.</summary>
		PpsSynchonizationMode Mode { get; }
	} // class PpsDataSynchronizationBatch

	#endregion

	#region -- class PpsDataSynchronizationTimeStampBatch -----------------------------

	/// <summary>Synchronization batch implementation for a time stamp based synchronization.</summary>
	public sealed class PpsDataSynchronizationTimeStampBatch : IPpsDataSynchronizationBatch
	{
		private readonly IEnumerator<IDataRow> currentView;
		private readonly int syncIdColumnIndex;
		private readonly bool isFull;

		private readonly long lastSyncId;
		private readonly long startTimeStamp;

		private bool beforeFirstRow = true;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary>Synchronization batch implementation for a time stamp based synchronization.</summary>
		/// <param name="view">Server site view of the data.</param>
		/// <param name="syncIdColumn">Column, that contains the syncid.</param>
		/// <param name="lastSyncId">Last sync id of the client.</param>
		/// <param name="isFull"></param>
		public PpsDataSynchronizationTimeStampBatch(IEnumerable<IDataRow> view, string syncIdColumn, long lastSyncId, bool isFull)
		{
			currentView = (view ?? throw new ArgumentNullException(nameof(view))).GetEnumerator();
			syncIdColumnIndex = syncIdColumn == null
				? -1
				: ((IDataColumns)currentView).FindColumnIndex(syncIdColumn, true);

			this.lastSyncId = lastSyncId;
			this.isFull = isFull;

			startTimeStamp = DateTime.Now.ToFileTimeUtc();
		} // ctor

		/// <summary></summary>
		public void Dispose()
		{
			currentView.Dispose();
		} // proc Dispose

		#endregion

		/// <summary>Reset the enumerator</summary>
		public void Reset()
		{
			beforeFirstRow = true;
			currentView.Reset();
		} // proc Reset

		/// <summary>Move to the next row.</summary>
		/// <returns><c>true</c>, if a new row is selected.</returns>
		public bool MoveNext()
		{
			beforeFirstRow = false;
			return currentView.MoveNext();
		} // func MoveNext

		/// <summary>Current row.</summary>
		public IDataRow Current => beforeFirstRow ? throw new InvalidOperationException() : currentView.Current;
		object IEnumerator.Current => currentView.Current;

		/// <summary>Column description of the batch.</summary>
		public IReadOnlyList<IDataColumn> Columns => ((IDataColumns)currentView).Columns;
		/// <summary>Mode is always r</summary>
		public char CurrentMode => 'r';

		/// <summary>Sync id is the timestamp in utc.</summary>
		public long CurrentSyncId
		{
			get
			{
				if (syncIdColumnIndex == -1)
					return startTimeStamp;
				else
				{
					if (beforeFirstRow)
						return lastSyncId;
					else
					{
						var v = Current[syncIdColumnIndex];
						if (v is DateTime dt)
							return dt == DateTime.MinValue
								? 0
								: dt.ToFileTimeUtc();
						else if (v is long l)
							return l;
						else
							throw new ArgumentException(Current.Columns[syncIdColumnIndex].Name);
					}
				}
			}
		} // prop CurrentSyncId

		/// <summary>Is always <c>false</c>.</summary>
		public PpsSynchonizationMode Mode => isFull ? PpsSynchonizationMode.Full : PpsSynchonizationMode.Parts;
	} // class PpsDataSynchronizationTimeStampBatch

	#endregion

	#region -- class PpsDataSynchronizationFullBatch ----------------------------------

	/// <summary>Synchronization batch implementation for a time stamp based synchronization.</summary>
	public sealed class PpsDataSynchronizationFullBatch : IPpsDataSynchronizationBatch
	{
		private readonly IEnumerator<IDataRow> currentView;
		private readonly long newSyncId;
		private readonly bool emitRows;
		private bool beforeFirstRow = true;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary>Synchronization batch implementation for a always full synchronization.</summary>
		/// <param name="view">Server site view of the data.</param>
		/// <param name="newSyncId"></param>
		/// <param name="lastSyncId"></param>
		public PpsDataSynchronizationFullBatch(IEnumerable<IDataRow> view, long lastSyncId, long newSyncId)
		{
			this.newSyncId = newSyncId;
			this.emitRows = lastSyncId != newSyncId;
			this.currentView = (view ?? throw new ArgumentNullException(nameof(view))).GetEnumerator();
		} // ctor

		/// <summary></summary>
		public void Dispose()
		{
			currentView.Dispose();
		} // proc Dispose

		#endregion

		/// <summary>Reset the enumerator</summary>
		public void Reset()
		{
			beforeFirstRow = true;
			currentView.Reset();
		} // proc Reset

		/// <summary>Move to the next row.</summary>
		/// <returns><c>true</c>, if a new row is selected.</returns>
		public bool MoveNext()
		{
			beforeFirstRow = false;
			return emitRows && currentView.MoveNext();
		} // func MoveNext

		/// <summary>Current row.</summary>
		public IDataRow Current => beforeFirstRow ? throw new InvalidOperationException() : currentView.Current;
		object IEnumerator.Current => currentView.Current;

		/// <summary>Column description of the batch.</summary>
		public IReadOnlyList<IDataColumn> Columns => ((IDataColumns)currentView).Columns;
		/// <summary>Mode is always r</summary>
		public char CurrentMode => 'r';

		/// <summary>Sync id is alway 0.</summary>
		public long CurrentSyncId => newSyncId;

		/// <summary>Is always <c>true</c>.</summary>
		public PpsSynchonizationMode Mode => emitRows ? PpsSynchonizationMode.Full : PpsSynchonizationMode.Parts;
	} // class PpsDataSynchronizationFullBatch

	#endregion

	#region -- class PpsDataSynchronization -------------------------------------------

	/// <summary>Synchronization batch for the client.</summary>
	public class PpsDataSynchronization : IDisposable
	{
		private readonly PpsApplication application;
		private readonly IPpsConnectionHandle connection;
		private readonly bool leaveConnectionOpen;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary>Initialize the synchronization batch for the client.</summary>
		/// <param name="application">Application instance.</param>
		/// <param name="connection">Database connection.</param>
		/// <param name="leaveConnectionOpen"></param>
		public PpsDataSynchronization(PpsApplication application, IPpsConnectionHandle connection, bool leaveConnectionOpen)
		{
			this.application = application ?? throw new ArgumentNullException(nameof(application));
			this.connection = connection ?? throw new ArgumentNullException(nameof(connection));
			this.leaveConnectionOpen = leaveConnectionOpen;
		} // ctor

		/// <summary></summary>
		~PpsDataSynchronization()
		{
			Dispose(false);
		} // dtor

		/// <summary></summary>
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		} // proc Dispose

		/// <summary></summary>
		/// <param name="disposing"></param>
		protected virtual void Dispose(bool disposing)
		{
			if(disposing)
			{
				if (!leaveConnectionOpen)
					connection.Dispose();
			}
		} // proc Dispose

		#endregion

		/// <summary>Helper, to parse synchronization hint.</summary>
		/// <param name="syncType"></param>
		/// <param name="syncAlgorithm"></param>
		/// <param name="syncArguments"></param>
		protected static void ParseSynchronizationArguments(string syncType, out string syncAlgorithm, out string syncArguments)
		{
			// get synchronization type "TimeStamp: viewName,column"
			var pos = syncType.IndexOf(':');
			if (pos == -1)
			{
				syncAlgorithm = syncType;
				syncArguments = String.Empty;
			}
			else
			{
				syncAlgorithm = syncType.Substring(0, pos);
				syncArguments = syncType.Substring(pos + 1).Trim();
			}
		} // func ParseSynchronizationArguments

		/// <summary>Helper, to parse synchronization hint.</summary>
		/// <param name="syncArguments"></param>
		/// <param name="name"></param>
		/// <param name="column"></param>
		protected static void ParseSynchronizationTimeStampArguments(string syncArguments, out string name, out string column)
		{
			var pos = syncArguments.IndexOf(',');
			if (pos == -1)
			{
				name = syncArguments.Trim();
				column = null;
			}
			else
			{
				name = syncArguments.Substring(0, pos).Trim();
				column = syncArguments.Substring(pos + 1).Trim();
			}
		} // proc ParseSynchronizationTimeStampArguments

		private PpsDataSelector CreateSelector(string viewName)
		{
			var view = application.GetViewDefinition(viewName, true);
			var selector = view.SelectorToken.CreateSelector(connection, null, true);
			return selector;
		} // func CreateSelector

		/// <summary>Create a synchonization batch part for a internal view.</summary>
		/// <param name="viewName">Name of the view.</param>
		/// <param name="viewSyncColumn">Synchronization column.</param>
		/// <param name="lastSyncId">Last synchronization stamp.</param>
		/// <returns>Synchronization batch part.</returns>
		protected IPpsDataSynchronizationBatch CreateTimeStampBatchFromSelector(string viewName, string viewSyncColumn, long lastSyncId)
		{
			var selector = CreateSelector(viewName);
			if (lastSyncId > 0)
			{
				var timeStampDateTime = DateTime.FromFileTimeUtc(lastSyncId);
				selector = selector.ApplyFilter(
					new PpsDataFilterLogicExpression(PpsDataFilterExpressionType.Or,
						new PpsDataFilterCompareExpression(viewSyncColumn, PpsDataFilterCompareOperator.LowerOrEqual, new PpsDataFilterCompareDateValue(DateTime.MinValue, DateTime.MinValue)),
						new PpsDataFilterCompareExpression(viewSyncColumn, PpsDataFilterCompareOperator.Greater, new PpsDataFilterCompareDateValue(timeStampDateTime, timeStampDateTime))
					)
				);
			}
			return new PpsDataSynchronizationTimeStampBatch(selector, viewSyncColumn, lastSyncId, false);
		} // proc CreateTimeStampBatchFromSelector

		/// <summary>Create a synchonization batch part for a internal view.</summary>
		/// <param name="viewName">Name of the view.</param>
		/// <param name="lastSyncId">Last synchronization stamp.</param>
		/// <returns>Synchronization batch part.</returns>
		protected IPpsDataSynchronizationBatch CreateFullBatchFromSelector(string viewName, long lastSyncId)
		{
			var selector = CreateSelector(viewName);
			return new PpsDataSynchronizationFullBatch(selector, lastSyncId,selector.Version);
		} // proc CreateTimeStampBatchFromSelector

		/// <summary>Create a synchronization batch part for the givven table.</summary>
		/// <param name="table">Table to synchronize.</param>
		/// <param name="syncType">Synchronization type, description.</param>
		/// <param name="lastSyncId">Last synchronization stamp.</param>
		/// <returns>Synchronization batch part.</returns>
		[Obsolete]
		public virtual IPpsDataSynchronizationBatch GenerateBatch(PpsDataTableDefinition table, string syncType, long lastSyncId)
		{
			ParseSynchronizationArguments(syncType, out var syncAlgorithm, out var syncArguments);

			if (String.Compare(syncAlgorithm, "TimeStamp", StringComparison.OrdinalIgnoreCase) == 0)
			{
				// parse view and column
				ParseSynchronizationTimeStampArguments(syncArguments, out var viewName, out var viewColumn);

				// get the synchronization rows
				return CreateTimeStampBatchFromSelector(viewName, viewColumn, lastSyncId);
			}
			else if (String.Compare(syncAlgorithm, "Full", StringComparison.OrdinalIgnoreCase) == 0)
			{
				return CreateFullBatchFromSelector(syncArguments, lastSyncId);
			}
			else
				throw new FormatException("Synchronization token (only timestamp or full is allowed).");
		} // proc GenerateBatch

		/// <summary>Creates a selector with the synchronization batch.</summary>
		/// <param name="tableName">Name of the synchronization table.</param>
		/// <param name="lastSyncId">Last synchronization state.</param>
		/// <returns></returns>
		public virtual PpsDataSelector CreateSelector(string tableName, long lastSyncId)
			=> throw new NotImplementedException();

		/// <summary>Creates a enumeration with the raw changes of the table.</summary>
		/// <param name="tableName">Name of the synchronization table.</param>
		/// <param name="lastSyncId">Last synchronization state.</param>
		/// <returns></returns>
		public virtual IPpsDataSynchronizationBatch GetChanges(string tableName, long lastSyncId)
			=> throw new NotImplementedException();

		/// <summary>Enforce a refresh of the change set.</summary>
		public virtual void RefreshChanges() { }

		/// <summary>Access to Application</summary>
		public PpsApplication Application => application;
		/// <summary>Database connection</summary>
		public IPpsConnectionHandle Connection => connection;
	} // class PpsDataSynchronization

	#endregion
}
