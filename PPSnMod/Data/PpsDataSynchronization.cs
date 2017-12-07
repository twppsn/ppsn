using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using TecWare.DE.Data;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;

namespace TecWare.PPSn.Server.Data
{
	#region -- interface IPpsDataSynchronizationBatch -----------------------------------

	public interface IPpsDataSynchronizationBatch : IEnumerator<IDataRow>, IDataColumns
	{
		long CurrentSyncId { get; }
		char CurrentMode { get; }
		bool IsFullSync { get; }
	} // class PpsDataSynchronizationBatch

	#endregion

	#region -- class PpsDataSynchronizationTimeStampBatch -------------------------------

	public sealed class PpsDataSynchronizationTimeStampBatch : IPpsDataSynchronizationBatch
	{
		private readonly IEnumerator<IDataRow> currentView;
		private readonly int syncIdColumnIndex;

		private readonly long lastSyncId;
		private readonly long startTimeStamp;

		private bool beforeFirstRow = true;

		public PpsDataSynchronizationTimeStampBatch(IEnumerable<IDataRow> view, string syncIdColumn, long lastSyncId)
		{
			this.currentView = (view ?? throw new ArgumentNullException(nameof(view))).GetEnumerator();
			this.syncIdColumnIndex = syncIdColumn == null
				? -1
				: ((IDataColumns)currentView).FindColumnIndex(syncIdColumn, true);
			this.lastSyncId = lastSyncId;
			this.startTimeStamp = DateTime.Now.ToFileTimeUtc();
		} // ctor

		public void Dispose()
		{
			currentView.Dispose();
		} // proc Dispose

		public void Reset()
		{
			beforeFirstRow = true;
			currentView.Reset();
		} // proc Reset

		public bool MoveNext()
		{
			beforeFirstRow = false;
			return currentView.MoveNext();
		} // func MoveNext

		public IDataRow Current => beforeFirstRow ? throw new InvalidOperationException() : currentView.Current;
		object IEnumerator.Current => currentView.Current;

		public IReadOnlyList<IDataColumn> Columns => ((IDataColumns)currentView).Columns;

		public char CurrentMode => 'r';

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

		public bool IsFullSync => false;
	} // class PpsDataSynchronizationTimeStampBatch

	#endregion

	#region -- class PpsDataSynchronization ---------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PpsDataSynchronization : IDisposable
	{
		private readonly PpsApplication application;
		private readonly IPpsConnectionHandle connection;
		private readonly DateTime lastSynchronization;

		public PpsDataSynchronization(PpsApplication application, IPpsConnectionHandle connection, DateTime lastSynchronization)
		{
			this.application = application ?? throw new ArgumentNullException(nameof(application));
			this.connection = connection ?? throw new ArgumentNullException(nameof(connection));
			this.lastSynchronization = lastSynchronization;
		} // ctor

		~PpsDataSynchronization()
		{
			Dispose(false);
		} // dtor

		public void Dispose()
		{
			GC.SuppressFinalize(this);
			Dispose(true);
		} // proc Dispose

		protected virtual void Dispose(bool disposing)
		{
			if (disposing)
				connection.Dispose();
		} // proc Dispose

		protected void ParseSynchronizationArguments(string syncType, out string syncAlgorithm, out string syncArguments)
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

		protected void ParseSynchronizationTimeStampArguments(string syncArguments, out string name, out string column)
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

		protected IPpsDataSynchronizationBatch CreateTimeStampBatchFromSelector(string viewName, string viewSyncColumn, long lastSyncId)
		{
			var view = application.GetViewDefinition(viewName, true);
			var selector = view.SelectorToken.CreateSelector(connection, true);
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
			return new PpsDataSynchronizationTimeStampBatch(selector, viewSyncColumn, lastSyncId);
		} // proc ApplyLastSyncIdFilter

		public virtual IPpsDataSynchronizationBatch GenerateBatch(PpsDataTableDefinition table, string syncType, long lastSyncId)
		{
			ParseSynchronizationArguments(syncType, out var syncAlgorithm, out var syncArguments);

			if (String.Compare(syncAlgorithm, "TimeStamp", StringComparison.OrdinalIgnoreCase) != 0)
				throw new FormatException("Synchronization token (only timestamp is allowed).");

			// parse view and column
			ParseSynchronizationTimeStampArguments(syncArguments, out var viewName, out var viewColumn);

			// get the synchronization rows
			return CreateTimeStampBatchFromSelector(viewName, viewColumn, lastSyncId);
		} // proc GenerateBatch

		public PpsApplication Application => application;
		public IPpsConnectionHandle Connection => connection;
		public DateTime LastSynchronization => lastSynchronization;
	} // class PpsDataSynchronization

	#endregion
}
