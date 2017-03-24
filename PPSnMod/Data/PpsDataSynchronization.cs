using System;
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
	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public abstract class PpsDataSynchronization : IDisposable
	{
		private readonly long timeStamp;
		private readonly long lastTimeStamp;

		protected PpsDataSynchronization(long timeStamp)
		{
			this.timeStamp = timeStamp;
			this.lastTimeStamp = DateTime.Now.ToFileTimeUtc();
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
		} // proc Dispose

		protected virtual IEnumerable<IDataRow> GetSynchronizationRows(string name, string timeStampColumn)
		{
			throw new NotImplementedException();
		} // func GetSynchronizationRows

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

		public virtual void GenerateBatch(XmlWriter xml, PpsDataTableDefinition table, string syncType)
		{
			ParseSynchronizationArguments(syncType, out var syncAlgorithm, out var syncArguments);

			if (String.Compare(syncAlgorithm, "TimeStamp", StringComparison.OrdinalIgnoreCase) != 0)
				throw new FormatException("Synchronization token (only timestamp is allowed).");

			// parse view and column
			ParseSynchronizationTimeStampArguments(syncArguments, out var viewName, out var viewColumn);

			// get the synchronization rows
			GenerateTimeStampBatch(xml, table, viewName, viewColumn);
		} // proc GenerateBatch

		protected void GenerateTimeStampBatch(XmlWriter xml, PpsDataTableDefinition table, string viewName, string viewColumn)
		{
			using (var rows = GetSynchronizationRows(viewName, viewColumn).GetEnumerator())
			{
				// create column names
				var sourceColumns = rows as IDataColumns;
				var columnNames = new string[table.Columns.Count];
				var columnIndex = new int[columnNames.Length];
				var columnConvert = new Func<object, string>[columnNames.Length];

				for (var i = 0; i < columnNames.Length; i++)
				{
					columnNames[i] = "c" + i.ToString();

					var targetColumn = table.Columns[i];
					var syncSourceColumnName = targetColumn.Meta.GetProperty("syncSource", targetColumn.Name);
					var sourceColumnIndex = syncSourceColumnName == "#" ? -1 : sourceColumns.FindColumnIndex(syncSourceColumnName);
					if (sourceColumnIndex == -1)
					{
						columnNames[i] = null;
						columnIndex[i] = -1;
						columnConvert[i] = null;
					}
					else
					{
						columnNames[i] = "c" + i.ToString();
						columnIndex[i] = sourceColumnIndex;
						if (sourceColumns.Columns[sourceColumnIndex].DataType == typeof(DateTime) &&
							targetColumn.DataType == typeof(long))
							columnConvert[i] = v =>
							{
								var dt = (DateTime)v;
								return dt == DateTime.MinValue ? null : dt.ToFileTimeUtc().ToString();
							};
						else
							columnConvert[i] = v => v.ChangeType<string>();
					}
				}

				// export columns
				while (rows.MoveNext())
				{
					var r = rows.Current;

					xml.WriteStartElement("r");

					for (var i = 0; i < columnNames.Length; i++)
					{
						if (columnIndex[i] != -1)
						{
							var v = r[columnIndex[i]];
							if (v != null)
							{
								var s = columnConvert[i](v);
								if (s != null)
									xml.WriteElementString(columnNames[i], s);
							}
						}
					}

					xml.WriteEndElement();
				}
			}
		} // proc GenerateTimeStampBatch

		public long TimeStamp => timeStamp;
		public DateTime TimeStampDateTime => DateTime.FromFileTimeUtc(timeStamp);

		public virtual long LastTimeStamp => lastTimeStamp;
		public virtual long LastSyncId => -1;

		public bool IsFull => timeStamp < 0;
	} // class PpsDataSynchronization
}
