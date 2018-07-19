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
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Data.SQLite;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Data;
using TecWare.DE.Networking;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;
using TecWare.PPSn.Properties;

namespace TecWare.PPSn
{
	#region -- enum PpsDataRowOperation -----------------------------------------------

	/// <summary>Data change operation type</summary>
	public enum PpsDataRowOperation
	{
		/// <summary>All rows a marked for delete.</summary>
		UnTouchRows,
		/// <summary>Delete not process rows from views.</summary>
		UnTouchedDeleteRows,
		/// <summary>Notifies about a table is changed operation.</summary>
		TableChanged,

		/// <summary>Insert operation</summary>
		RowInsert,
		/// <summary>Delete operation</summary>
		RowDelete,
		/// <summary>Update operation</summary>
		RowUpdate
	} // enum PpsDataRowOperation

	#endregion

	#region -- class PpsDataTableOperationEventArgs -----------------------------------

	/// <summary>Event arguments for table based change operations from the system.</summary>
	public class PpsDataTableOperationEventArgs : EventArgs
	{
		private readonly PpsDataTableDefinition table;
		private readonly PpsDataRowOperation operation;

		/// <summary></summary>
		/// <param name="table"></param>
		/// <param name="operation"></param>
		public PpsDataTableOperationEventArgs(PpsDataTableDefinition table, PpsDataRowOperation operation)
		{
			this.table = table;
			this.operation = operation;
		} // ctor

		/// <summary></summary>
		public PpsDataTableDefinition Table => table;
		/// <summary></summary>
		public PpsDataRowOperation Operation => operation;
	} // class PpsDataTableOperationEventArgs

	/// <summary></summary>
	/// <param name="sender"></param>
	/// <param name="e"></param>
	public delegate void PpsDataTableOperationEventHandler(object sender, PpsDataTableOperationEventArgs e);

	#endregion

	#region -- class IPpsDataRowOperationArguments ------------------------------------

	/// <summary></summary>
	public interface IPpsDataRowOperationArguments : IPropertyEnumerableDictionary, IDataRecord
	{
	} // IPpsDataRowOperationArguments

	#endregion

	#region -- class PpsDataRowOperationEventArgs -------------------------------------

	/// <summary>Row based event</summary>
	public class PpsDataRowOperationEventArgs : PpsDataTableOperationEventArgs
	{
		private readonly object rowId;
		private readonly object oldRowId;
		private readonly IPpsDataRowOperationArguments arguments;

		/// <summary></summary>
		/// <param name="operation"></param>
		/// <param name="table"></param>
		/// <param name="rowId"></param>
		/// <param name="oldRowId"></param>
		/// <param name="arguments"></param>
		public PpsDataRowOperationEventArgs(PpsDataRowOperation operation, PpsDataTableDefinition table, object rowId, object oldRowId, IPpsDataRowOperationArguments arguments)
			: base(table, operation)
		{
			this.rowId = rowId;
			this.oldRowId = oldRowId;
			this.arguments = arguments;
		} // ctor

		/// <summary>Primary key of the row.</summary>
		public object RowId => rowId;
		/// <summary>Returns the old id.</summary>
		public object OldRowId => oldRowId ?? rowId;
		/// <summary>Optional column description.</summary>
		public IPpsDataRowOperationArguments Arguments => arguments;
	} // class PpsDataRowOperationEventArgs

	#endregion

	#region -- enum PpsWriteTransactionState ------------------------------------------

	/// <summary>Write transaction state</summary>
	public enum PpsWriteTransactionState
	{
		/// <summary>No transaction active.</summary>
		None,
		/// <summary>The current thread has the active write transaction.</summary>
		CurrentThread,
		/// <summary>An other thread has the active write transaction.</summary>
		OtherThread
	} // enum PpsWriteTransactionState

	#endregion

	#region -- enum PpsLoadPriority ---------------------------------------------------

	/// <summary>Defines the importance of an item</summary>
	public enum PpsLoadPriority
	{
		/// <summary>Second-most important</summary>
		Default = 1,
		/// <summary>Top-most important</summary>
		ApplicationFile = 0,
		/// <summary>Third-most important</summary>
		ObjectPrimaryData = 1,
		/// <summary>Fourth-most important</summary>
		ObjectReferencedData = 2,
		/// <summary>Least important</summary>
		Background = 3
	} // enum PpsLoadPriority

	#endregion

	#region -- interface IInternalFileCacheStream -------------------------------------

	internal interface IInternalFileCacheStream
	{
		void MoveTo(string fileName);
	} // interface IInternalFileCacheStream

	#endregion

	#region -- enum PpsMasterDataTransactionLevel -------------------------------------

	/// <summary>Access level for the database transactions</summary>
	public enum PpsMasterDataTransactionLevel
	{
		/// <summary>Read only access.</summary>
		ReadUncommited,
		/// <summary>Read only access, only on committed data.</summary>
		ReadCommited,
		/// <summary>Write access.</summary>
		Write
	} // enum PpsMasterDataTransactionLevel

	#endregion

	#region -- class PpsMasterDataTransaction -----------------------------------------

	/// <summary>Transaction for the sqlite data manipulation.</summary>
	public abstract class PpsMasterDataTransaction : IDbTransaction, IDisposable
	{
		private readonly PpsMasterData masterData;

		#region -- Ctor/Dtor/Commit/Rollback --------------------------------------------

		/// <summary>Transaction for the sqlite data manipulation.</summary>
		/// <param name="masterData"></param>
		protected PpsMasterDataTransaction(PpsMasterData masterData)
		{
			this.masterData = masterData ?? throw new ArgumentNullException(nameof(masterData));
		} // ctor

		/// <summary></summary>
		~PpsMasterDataTransaction()
		{
			Dispose(false);
		} // dtor

		/// <summary></summary>
		public void Dispose()
		{
			GC.SuppressFinalize(this);
			Dispose(true);
		} // proc Dispose

		/// <summary></summary>
		/// <param name="disposing"></param>
		protected virtual void Dispose(bool disposing)
		{
		} // proc Dispose

		/// <summary>Override to do commit actions.</summary>
		protected virtual void CommitCore() { }
		/// <summary>Override to do rollback actions.</summary>
		protected virtual void RollbackCore() { }

		/// <summary>Commit current transaction.</summary>
		public void Commit()
			=> CommitCore();

		/// <summary>Rollback current transaction.</summary>
		public void Rollback()
			=> RollbackCore();

		#endregion

		/// <summary>Add a action to the rollback.</summary>
		/// <param name="rollback"></param>
		public abstract void AddRollbackOperation(Action rollback);

		/// <summary></summary>
		/// <param name="commandText"></param>
		/// <returns></returns>
		public DbCommand CreateNativeCommand(string commandText = null)
			=> new SQLiteCommand(commandText, ConnectionCore, TransactionCore);

		/// <summary>Get the next local id.</summary>
		/// <param name="tableName">Table name.</param>
		/// <param name="primaryKey">Primary key field.</param>
		/// <returns></returns>
		public long GetNextLocalId(string tableName, string primaryKey)
		{
			// local id's are negative (--> min)
			using (var cmd = CreateNativeCommand("SELECT min([" + primaryKey + "]) FROM main.[" + tableName + "]"))
			{
				var nextIdObject = cmd.ExecuteScalarEx();
				if (nextIdObject == DBNull.Value)
					return -1;
				else
				{
					var nextId = nextIdObject.ChangeType<long>();
					return nextId < 0 ? nextId - 1 : -1;
				}
			}
		} // func GetNextLocalId

		/// <summary>Notify the system about a data change.</summary>
		/// <param name="args"></param>
		/// <param name="raiseTableChanged"><c>true</c>, to raise a table change on insert or delete.</param>
		public void RaiseOperationEvent(PpsDataTableOperationEventArgs args, bool raiseTableChanged = true)
			=> masterData.OnMasterDataTableChanged(args, raiseTableChanged);
		
		/// <summary>Last inserted id.</summary>
		public long LastInsertRowId => ConnectionCore.LastInsertRowId;

		/// <summary>Access the the local database connection.</summary>
		protected abstract SQLiteConnection ConnectionCore { get; }
		/// <summary>Access the the local database transaciton.</summary>
		protected abstract SQLiteTransaction TransactionCore { get; }

		IDbConnection IDbTransaction.Connection => ConnectionCore;

		/// <summary>Connection of this transaction.</summary>
		public DbConnection Connection => ConnectionCore;
		/// <summary>Transaction isolation level.</summary>
		public IsolationLevel IsolationLevel => TransactionCore.IsolationLevel;

		/// <summary>Access master data service.</summary>
		public PpsMasterData MasterData => masterData;

		/// <summary>Is the transaction disposed.</summary>
		public abstract bool IsDisposed { get; }
		/// <summary>Is the transaction commited/rollbacked.</summary>
		public abstract bool IsCommited { get; }
	} // class PpsMasterTransaction

	#endregion

	#region -- class PpsMasterDataRow -------------------------------------------------

	/// <summary>Represents a datarow of a master data table.</summary>
	public sealed class PpsMasterDataRow : DynamicDataRow, IDataValues2, INotifyPropertyChanged
	{
		/// <summary>Is called if a value gets changed.</summary>
		public event PropertyChangedEventHandler PropertyChanged;

		private readonly PpsMasterDataTable owner;
		private readonly object rowId;
		private readonly object[] values;

		internal PpsMasterDataRow(PpsMasterDataTable owner, IDataRecord r)
		{
			this.owner = owner;
			this.values = new object[owner.Columns.Count];

			var primaryKeyIndex = owner.GetPrimaryKeyColumnIndex();
			for (var i = 0; i < values.Length; i++)
			{
				var v = r.GetValue(i);
				if (primaryKeyIndex == i)
				{
					if (v == null || v == DBNull.Value)
						throw new ArgumentNullException(owner.Columns[primaryKeyIndex].Name, "Null primary columns are not allowed.");
					rowId = v;
				}

				values[i] = v == DBNull.Value ? null : v;
			}
		} // ctor

		internal void UpdateValues(IPpsDataRowOperationArguments arguments)
		{
			var primaryKeyIndex = owner.GetPrimaryKeyColumnIndex();
			for (var i = 0; i < values.Length; i++)
			{
				if (!Equals(values[i], arguments[i]))
				{
					values[i] = arguments[i];
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(owner.Columns[i].Name));
				}
			}
		} // func UpdateValues

		/// <summary>Check of the rows are the same.</summary>
		/// <param name="obj">other object</param>
		/// <returns></returns>
		public override bool Equals(object obj)
			=> obj is PpsMasterDataRow r
				? (ReferenceEquals(this, obj) || owner.Definition == r.owner.Definition && Equals(RowId, r.RowId))
				: false;

		/// <summary>Hashcode for the current datarow.</summary>
		/// <returns></returns>
		public override int GetHashCode()
			=> owner.Definition.GetHashCode() ^ RowId.GetHashCode();

		/// <summary></summary>
		/// <param name="columnName"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public override bool TryGetProperty(string columnName, out object value)
		{
			if (base.TryGetProperty(columnName, out value))
				return true;
			else
			{
				var rel = owner.Definition.Relations[columnName, false];
				if (rel != null)
				{
					value = owner.CreateRelation(rel, values[rel.ParentColumn.Index]);
					return true;
				}
				else
				{
					value = null;
					return false;
				}
			}
		} // func TryGetProperty

		private PpsMasterDataRow GetParentRow(int index, PpsDataColumnDefinition column)
			=> owner.MasterData.GetTable(column.ParentColumn.Table)
				?.GetRowById(values[index].ChangeType<long>());

		bool IDataValues2.TryGetRelatedDataRow(int index, out IDataRow row)
		{
			var columnInfo = owner.GetColumnDefinition(index);
			if (columnInfo.IsRelationColumn)
			{
				row = GetParentRow(index, columnInfo);
				return row != null;
			}
			else
			{
				row = null;
				return false;
			}
		} // func TryGetRelatedDataRow

		/// <summary>Access the column decriptions.</summary>
		public override IReadOnlyList<IDataColumn> Columns
			=> owner.Columns;

		/// <summary>Return a column of this datarow.</summary>
		/// <param name="index"></param>
		/// <returns></returns>
		public override object this[int index]
		{
			get
			{
				var value = values[index];
				if (value != null)
				{
					var column = owner.GetColumnDefinition(index);
					if (column.IsRelationColumn) // return the related row
						return GetParentRow(index, column);
					else
						return values[index];
				}
				else
					return null;
			}
		} // prop this

		/// <summary>Master data rows, own there data.</summary>
		public override bool IsDataOwner => true;
		/// <summary>Internal flag for batch updates.</summary>
		internal bool IsTouched { get; set; } = false;

		/// <summary>Return key value for this row.</summary>
		public object RowId => rowId;
	} // class PpsMasterDataRow

	#endregion

	#region -- class PpsMasterDataSelector --------------------------------------------

	/// <summary>Base implementation of a data selector.</summary>
	public abstract class PpsMasterDataSelector : IDataRowEnumerable, IDataColumns, INotifyCollectionChanged
	{
		/// <summary>Use only reset.</summary>
		public event NotifyCollectionChangedEventHandler CollectionChanged;

		/// <summary></summary>
		protected PpsMasterDataSelector()
		{
		} // ctor

		/// <summary>Creates the select for all data rows.</summary>
		/// <returns></returns>
		protected abstract DbCommand PrepareCommand();

		/// <summary>Returns the rows for the prepared command.</summary>
		/// <returns></returns>
		public IEnumerator<IDataRow> GetEnumerator()
		{
			using (var command = PrepareCommand())
			using (var r = command.ExecuteReaderEx(CommandBehavior.SingleResult))
			{
				var primaryKeyColumnIndex = Table.GetPrimaryKeyColumnIndex();
				while (r.Read())
					yield return Table.CreateRow(primaryKeyColumnIndex, r);
			}
		} // func GetEnumerator

		/// <summary></summary>
		protected void OnCollectionReset()
			=> CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

		IEnumerator IEnumerable.GetEnumerator()
			=> GetEnumerator();

		/// <summary>Apply an order to the data selector</summary>
		/// <param name="expressions">Order expression.</param>
		/// <param name="lookupNative">Native lookup.</param>
		/// <returns></returns>
		public virtual IDataRowEnumerable ApplyOrder(IEnumerable<PpsDataOrderExpression> expressions, Func<string, string> lookupNative = null)
			=> this;

		/// <summary>Apply a filter to the data selector.</summary>
		/// <param name="expression">Filter expression.</param>
		/// <param name="lookupNative"></param>
		/// <returns></returns>
		public virtual IDataRowEnumerable ApplyFilter(PpsDataFilterExpression expression, Func<string, string> lookupNative = null)
			=> this;

		/// <summary>Apply a column reduction.</summary>
		/// <param name="columns">Columns to return.</param>
		/// <returns></returns>
		public virtual IDataRowEnumerable ApplyColumns(IEnumerable<PpsDataColumnExpression> columns)
			=> this;

		/// <summary>Columns of the rows.</summary>
		public abstract IReadOnlyList<IDataColumn> Columns { get; }
		/// <summary>Owner of the the rows.</summary>
		public abstract PpsMasterDataTable Table { get; }
	} // class PpsMasterDataSelector

	#endregion

	#region -- class PpsMasterDataTable -----------------------------------------------

	/// <summary>Proxy Table to select master data tables from the sqlite database.</summary>
	public sealed class PpsMasterDataTable : PpsMasterDataSelector
	{
		#region -- class PpsSqLiteFilterVisitor ---------------------------------------

		private sealed class PpsMasterDataFilterVisitor : PpsSqLiteFilterVisitor
		{
			public PpsMasterDataFilterVisitor(IDataColumns columns)
				: base(columns)
			{
			} // ctor

			protected override Tuple<string, Type> LookupColumn(string columnToken)
			{
				if (String.IsNullOrEmpty(columnToken))
				{
					NeedFullTextColumn = true;
					return new Tuple<string, Type>("__FULLTEXT__", typeof(string));
				}
				return base.LookupColumn(columnToken);
			} // func LookupColumn

			protected override Tuple<string, Type> LookupDateColumn(string columnToken)
				=> base.LookupDateColumn(columnToken);

			protected override Tuple<string, Type> LookupNumberColumn(string columnToken)
				=> base.LookupNumberColumn(columnToken);

			public bool NeedFullTextColumn { get; private set; } = false;
		} // class PpsMasterDataFilterVisitor

		#endregion
			
		#region -- class PpsMasterDataTableResult -------------------------------------

		private sealed class PpsMasterDataTableResult : PpsMasterDataSelector
		{
			private readonly PpsMasterDataTable table;
			private readonly PpsDataFilterExpression filter;

			public PpsMasterDataTableResult(PpsMasterDataTable table, PpsDataFilterExpression filter)
			{
				this.table = table ?? throw new ArgumentNullException(nameof(table));
				this.filter = filter ?? PpsDataFilterExpression.True;

				table.MasterData.RegisterWeakDataRowChanged(table.Definition.Name, null, OnTableChanged);
			} // ctor

			private void OnTableChanged(object sender, PpsDataTableOperationEventArgs e)
			{
				if (e.Operation == PpsDataRowOperation.TableChanged)
					OnCollectionReset();
			} // proc OnTableChanged

			protected override DbCommand PrepareCommand()
			{
				var command = Table.MasterData.CreateNativeCommand();
				try
				{
					var filterVisitor = new PpsMasterDataFilterVisitor(table);
					var where = filterVisitor.CreateFilter(filter);

					var commandText = table.PrepareCommandText(
						sb =>
						{
							if (filterVisitor.NeedFullTextColumn)
							{
								var expr = String.Join(" || ' ' || ", from c in table.Columns where c.DataType == typeof(string) select "COALESCE([" + c.Name + "],'')");
								if (String.IsNullOrEmpty(expr))
									expr = "null";
								sb.Append(',').Append(expr).Append(" AS __FULLTEXT__");
							}
						}
					);

					commandText.Append(" WHERE ");
					commandText.Append(where);
					command.CommandText = commandText.ToString();

					return command;
				}
				catch
				{
					command.Dispose();
					throw;
				}
			} // func PrepareCommand

			public override IDataRowEnumerable ApplyFilter(PpsDataFilterExpression expression, Func<string, string> lookupNative = null)
				=> new PpsMasterDataTableResult(table, PpsDataFilterExpression.Combine(filter, expression));

			public override PpsMasterDataTable Table => table;
			public override IReadOnlyList<IDataColumn> Columns => table.Columns;
		} // class PpsMasterDataTableResult

		#endregion

		private readonly Dictionary<object, WeakReference<PpsMasterDataRow>> cachedRows = new Dictionary<object, WeakReference<PpsMasterDataRow>>();
		private readonly object dataRowChangedToken;

		/// <summary></summary>
		/// <param name="masterData"></param>
		/// <param name="table"></param>
		public PpsMasterDataTable(PpsMasterData masterData, PpsDataTableDefinition table)
		{
			this.MasterData = masterData;
			this.Definition = table;

			dataRowChangedToken = masterData.RegisterWeakDataRowChanged(table.Name, null, OnTableRowChanged);
		} // ctor

		private void OnTableRowChanged(object sender, PpsDataTableOperationEventArgs args)
		{
			switch (args.Operation)
			{
				case PpsDataRowOperation.UnTouchRows:
					foreach (var cur in cachedRows.Values)
					{
						if (cur.TryGetTarget(out var row))
							row.IsTouched = false;
					}
					break;
				case PpsDataRowOperation.UnTouchedDeleteRows:
					var removeList = new List<object>();
					foreach (var cur in cachedRows.Values)
					{
						if (cur.TryGetTarget(out var row) && !row.IsTouched)
							removeList.Add(row.RowId);
					}

					// remove items
					for (var i = 0; i < removeList.Count; i++)
						cachedRows.Remove(removeList[i]);
					break;
				case PpsDataRowOperation.TableChanged:
					OnCollectionReset();
					break;

				case PpsDataRowOperation.RowUpdate:
					{
						if (args is PpsDataRowOperationEventArgs e
						  && cachedRows.TryGetValue(e.RowId, out var cur)
						  && cur.TryGetTarget(out var row))
							row.UpdateValues(e.Arguments);
					}
					break;
				case PpsDataRowOperation.RowDelete:
					{
						if (args is PpsDataRowOperationEventArgs e
							&& cachedRows.TryGetValue(e.RowId, out var cur))
							cachedRows.Remove(e.RowId);
					}
					break;
			}
		} // proc OnTableRowChanged

		private StringBuilder PrepareCommandText(Action<StringBuilder> appendVirtualColumns)
		{
			var commandText = new StringBuilder("SELECT ");

			// create select
			var first = true;
			foreach (var c in Definition.Columns)
			{
				if (first)
					first = false;
				else
					commandText.Append(',');
				commandText.Append('[')
					.Append(c.Name)
					.Append(']');
			}

			appendVirtualColumns?.Invoke(commandText);

			// build from
			commandText.Append(" FROM main.[").Append(Definition.Name).Append(']');
			return commandText;
		} // proc PrepareCommandText

		/// <summary>Prepare command.</summary>
		/// <returns></returns>
		protected override DbCommand PrepareCommand()
		{
			var commandText = PrepareCommandText(null);

			return MasterData.CreateNativeCommand(commandText.ToString());
		} // func PrepareCommand

		/// <summary></summary>
		/// <param name="key"></param>
		/// <param name="throwException"></param>
		/// <returns></returns>
		public PpsMasterDataRow GetRowById(long key, bool throwException = false)
		{
			lock (cachedRows)
			{
				if (cachedRows.TryGetValue(key, out var rowRef) && rowRef.TryGetTarget(out var row) && row != null)
					return row;
			}

			var commandText = PrepareCommandText(null);
			commandText.Append(" WHERE [")
				.Append(Definition.PrimaryKey.Name)
				.Append("] = @Key");

			using (var cmd = MasterData.CreateNativeCommand(commandText.ToString()))
			{
				cmd.AddParameter("@key").Value = key;
				var primaryKeyColumnIndex = GetPrimaryKeyColumnIndex();

				using (var r = cmd.ExecuteReaderEx(CommandBehavior.SingleRow))
				{
					if (r.Read())
						return CreateRow(primaryKeyColumnIndex, r);
					else if (throwException)
						throw new ArgumentException($"Could not seek row with key '{key}' in table '{Definition.Name}'.");
					else
						return null;
				}
			} // using cmd
		} // func GetRowById

		/// <summary>Returns the primary key index.</summary>
		/// <returns></returns>
		internal int GetPrimaryKeyColumnIndex()
			=> Definition.PrimaryKey.Index;

		internal PpsDataColumnDefinition GetColumnDefinition(int index)
			=> Definition.Columns[index];

		internal PpsMasterDataRow CreateRow(int primaryKeyColumnIndex, IDataRecord r)
		{
			lock (cachedRows)
			{
				var key = r.GetInt64(primaryKeyColumnIndex);
				if (cachedRows.TryGetValue(key, out var rowRef) && rowRef.TryGetTarget(out var row) && row != null)
					return row;
				else
				{
					row = new PpsMasterDataRow(Table, r);
					cachedRows[key] = new WeakReference<PpsMasterDataRow>(row);
					return row;
				}
			}
		} // func CreateRow

		internal PpsMasterDataSelector CreateRelation(PpsDataTableRelationDefinition relation, object key)
			=> new PpsMasterDataTableResult(MasterData.GetTable(relation.ChildColumn.Table), PpsDataFilterExpression.Compare(relation.ChildColumn.Name, PpsDataFilterCompareOperator.Equal, PpsDataFilterCompareValueType.Integer, key));

		/// <summary></summary>
		/// <param name="expression"></param>
		/// <param name="lookupNative"></param>
		/// <returns></returns>
		public override IDataRowEnumerable ApplyFilter(PpsDataFilterExpression expression, Func<string, string> lookupNative = null)
			=> new PpsMasterDataTableResult(this, expression);
		
		/// <summary>Columns</summary>
		public override IReadOnlyList<IDataColumn> Columns => Definition.Columns;
		/// <summary>Self</summary>
		public override PpsMasterDataTable Table => this;
		/// <summary></summary>
		public PpsDataTableDefinition Definition { get; }

		/// <summary>The master data service.</summary>
		public PpsMasterData MasterData { get; }
	} // class PpsMasterDataTable

	#endregion

	#region -- class PpsMasterData ----------------------------------------------------

	/// <summary></summary>
	public sealed class PpsMasterData : IDynamicMetaObjectProvider, IDisposable
	{
		/// <summary>Name of the master data schema</summary>
		public const string MasterDataSchema = "masterData";

		private const string refreshColumnName = "_IsUpdated"; // column for update hints

		#region -- class PpsMasterDataMetaObject --------------------------------------

		private sealed class PpsMasterDataMetaObject : DynamicMetaObject
		{
			public PpsMasterDataMetaObject(PpsMasterData masterData, Expression parameter)
				: base(parameter, BindingRestrictions.Empty, masterData)
			{
			} // ctor

			public override DynamicMetaObject BindGetMember(GetMemberBinder binder)
			{
				var masterData = (PpsMasterData)Value;

				// check for table
				var tableDefinition = masterData.FindTable(binder.Name, false);
				if (tableDefinition == null)
					return base.BindGetMember(binder);

				// return the table
				return new DynamicMetaObject(
					Expression.Call(
						Expression.Convert(Expression, typeof(PpsMasterData)),
						getTableMethodInfo,
						Expression.Constant(tableDefinition)
					),
					BindingRestrictions.GetInstanceRestriction(Expression, masterData)
				);
			} // proc BindGetMember
		} // class PpsMasterDataMetaObject

		#endregion

		#region -- class IndexDef -----------------------------------------------------

		private class IndexDef
		{
			private readonly string indexName;
			private bool isUnique;
			private readonly List<IDataColumn> columns;

			public IndexDef(string indexName)
			{
				this.indexName = indexName;
				this.isUnique = false;
				this.columns = new List<IDataColumn>();
			} // ctor

			public IndexDef(string indexName, bool isUnique, params IDataColumn[] columns)
			{
				this.indexName = indexName;
				this.isUnique = isUnique;
				this.columns = new List<IDataColumn>(columns);
			} // ctor

			public string IndexName => indexName;

			public string QualifiedName => columns.Count > 1 ? indexName + "_" + columns.Count + "_" : indexName;

			public bool IsUnique
			{
				get => isUnique;
				set => isUnique = isUnique | value;
			} // prop IsUnique

			public List<IDataColumn> Columns => columns;
		} // class IndexDef

		#endregion

		private readonly PpsEnvironment environment;
		private readonly SQLiteConnection connection;

		private PpsDataSetDefinitionDesktop schema;
		private bool? schemaIsOutDated = null;
		private DateTime lastSynchronizationSchema = DateTime.MinValue; // last synchronization of the schema
		private DateTime lastSynchronizationStamp = DateTime.MinValue;  // last synchronization stamp
		private bool isSynchronizationStarted = false; // number of sync processes
		private bool isObjectTagsDirty = true; // synchronize object tags

		private bool isDisposed = false;
		private readonly object synchronizationLock = new object();
		private ThreadLocal<bool> isSynchronizationRunning = new ThreadLocal<bool>(() => false, false);
		private bool updateUserInfo = false;

		private Lazy<PpsDataTableDefinition> objectsTable = null;
		private Lazy<PpsDataTableDefinition> objectTagsTable = null;
		private Lazy<PpsDataTableDefinition> objectLinksTable = null;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary></summary>
		/// <param name="environment"></param>
		/// <param name="connection"></param>
		/// <param name="schema"></param>
		/// <param name="lastSynchronizationSchema"></param>
		/// <param name="lastSynchronizationStamp"></param>
		public PpsMasterData(PpsEnvironment environment, SQLiteConnection connection, PpsDataSetDefinitionDesktop schema, DateTime lastSynchronizationSchema, DateTime lastSynchronizationStamp)
		{
			this.environment = environment;
			this.connection = connection;

			this.schema = schema;
			this.lastSynchronizationSchema = lastSynchronizationSchema;
			this.lastSynchronizationStamp = lastSynchronizationStamp;

			ResetStaticTableDefinitions();
		} // ctor

		/// <summary></summary>
		public void Dispose()
		{
			if (!isDisposed)
			{
				isDisposed = true;
				connection?.Dispose();
			}
		} // proc Dispose

		private void ResetStaticTableDefinitions()
		{
			objectsTable = new Lazy<PpsDataTableDefinition>(() => FindTable("Objects", true));
			objectTagsTable = new Lazy<PpsDataTableDefinition>(() => FindTable("ObjectTags", true));
			objectLinksTable = new Lazy<PpsDataTableDefinition>(() => FindTable("ObjectLinks", true));
		} // proc ResetStaticTableDefinitions

		DynamicMetaObject IDynamicMetaObjectProvider.GetMetaObject(Expression parameter)
			=> new PpsMasterDataMetaObject(this, parameter);

		#endregion

		#region -- Local store schema update ------------------------------------------

		private async Task UpdateSchemaAsync(IProgress<string> progress)
		{
			progress?.Report("Lokale Datenbank wird aktualisiert...");

			// load new schema
			var respone = await environment.Request.GetResponseAsync(environment.GetDocumentUri(MasterDataSchema));
			var schemaStamp = respone.GetLastModified();
			var xSchema = environment.Request.GetXml(respone);

			var newMasterDataSchema = new PpsDataSetDefinitionDesktop(environment, MasterDataSchema, xSchema);
			newMasterDataSchema.EndInit();

			// generate update commands
			var updateScript = GetUpdateCommands(connection, newMasterDataSchema, CheckLocalTableExists(connection, "SyncState"));

			// execute update commands
			using (var transaction = connection.BeginTransaction())
			{
				try
				{
					if (updateScript.Count > 0)
						ExecuteUpdateScript(connection, transaction, updateScript);

					// update header
					var existRow = false;
					using (var cmd = connection.CreateCommand())
					{
						cmd.CommandText = "SELECT EXISTS (SELECT * FROM main.Header)";
						existRow = ((long)cmd.ExecuteScalarEx()) != 0;
					}

					using (var cmd = connection.CreateCommand())
					{
						cmd.Transaction = transaction;

						cmd.CommandText = existRow
							? "UPDATE main.Header SET SchemaStamp = @stamp, SchemaContent = @content;"
							: "INSERT INTO main.Header (SchemaStamp, SchemaContent) VALUES (@stamp, @content);";
						cmd.Parameters.Add("@stamp", DbType.Int64).Value = schemaStamp.ToFileTimeUtc();
						cmd.Parameters.Add("@content", DbType.AnsiString).Value = xSchema.ToString(SaveOptions.None);
						cmd.ExecuteNonQueryEx();
					}

					transaction.Commit();
				}
				catch
				{
					transaction.Rollback();
					throw;
				}
			}

			// update schema
			schema = newMasterDataSchema;
			ResetStaticTableDefinitions();
		} // proc UpdateSchemaAsync

		private static IReadOnlyList<string> GetUpdateCommands(SQLiteConnection connection, PpsDataSetDefinitionDesktop schema, bool syncStateTableExists)
		{
			var commands = new List<string>();
			var tableChanged = false;
			foreach (var table in schema.TableDefinitions)
			{
				if (CheckLocalTableExists(connection, table.Name)) // generate alter table script
				{
					tableChanged = CreateAlterTableScript(commands,
						table.Name,
						table.Meta.GetProperty("syncType", String.Empty) == "None",
						GetLocalTableColumns(connection, table.Name),
						GetLocalTableIndexes(connection, table.Name),
						table.Columns
					);
				}
				else // generate create table script
				{
					CreateTableScript(commands, table.Name, table.Columns, null);
					tableChanged = true;
				}

				// clear sync token
				if (tableChanged && syncStateTableExists)
					commands.Add($"DELETE FROM main.[SyncState] WHERE [Table] = '{table.Name}'");
			}

			return commands;
		} // func GetUpdateCommands

		private static void ExecuteUpdateScript(SQLiteConnection connection, SQLiteTransaction transaction, IEnumerable<string> commands)
		{
			using (var cmd = connection.CreateCommand())
			{
				cmd.Transaction = transaction;
				foreach (var c in commands)
				{
					cmd.CommandText = c;
					cmd.ExecuteNonQueryEx();
				}
			}
		} // proc ExecuteUpdateScript

		private static void CreateTableScript(List<string> commands, string tableName, IEnumerable<IDataColumn> remoteColumns, string[] localIndexArray)
		{
			var indices = new Dictionary<string, IndexDef>(StringComparer.OrdinalIgnoreCase);

			// create table
			var commandText = new StringBuilder("CREATE TABLE ");
			AppendSqlIdentifier(commandText, tableName).Append(" (");

			foreach (var column in remoteColumns)
			{
				if (String.Compare(column.Name, "_rowId", StringComparison.OrdinalIgnoreCase) == 0)
					continue; // ignore rowId column

				AppendSqlIdentifier(commandText, column.Name).Append(' ');
				commandText.Append(ConvertDataTypeToSqLite(column));

				// append primray key
				if (column.Attributes.GetProperty("IsPrimary", false))
					commandText.Append(" PRIMARY KEY");

				CreateCommandColumnAttribute(commandText, column);

				AddIndexDefinition(tableName, column, indices);

				commandText.Append(',');
			}
			commandText[commandText.Length - 1] = ')'; // replace last comma
			commandText.Append(";");

			commands.Add(commandText.ToString());

			foreach (var idx in indices.Values)
				CreateTableIndex(commands, tableName, idx, localIndexArray);
		} // func CreateTableScript

		private static bool CreateAlterTableScript(List<string> commands, string tableName, bool preserveCurrentData, IEnumerable<IDataColumn> localColumns, IEnumerable<Tuple<string, bool>> localIndexes, IEnumerable<IDataColumn> remoteColumns)
		{
			var indices = new Dictionary<string, IndexDef>();
			var localColumnsArray = localColumns.ToArray();
			var localIndexArray = localIndexes.ToArray();
			var newColumns = new List<IDataColumn>();
			var sameColumns = new List<string>();   // for String.Join - only Column names are used

			var newIndices = new List<IndexDef>();
			var changedIndices = new List<IndexDef>();
			var removeIndices = new List<string>();

			foreach (var remoteColumn in remoteColumns)
			{
				if (String.Compare(remoteColumn.Name, "_rowId", StringComparison.OrdinalIgnoreCase) == 0)
					continue; // ignore rowId column

				var found = false;
				foreach (var localColumn in localColumnsArray)
				{
					if (localColumn.Name == refreshColumnName)
						preserveCurrentData = true;

					// todo: check default
					if ((remoteColumn.Name == localColumn.Name)
						&& String.Compare(ConvertDataTypeToSqLite(remoteColumn), localColumn.Attributes.GetProperty("SQLiteType", "Integer"), StringComparison.OrdinalIgnoreCase) == 0
						&& (remoteColumn.Attributes.GetProperty("Nullable", false) == localColumn.Attributes.GetProperty("Nullable", false))
						&& (remoteColumn.Attributes.GetProperty("IsPrimary", false) == localColumn.Attributes.GetProperty("IsPrimary", false))
						)
					{
						found = true;
						break;
					}
				}
				if (found)
					sameColumns.Add(remoteColumn.Name);
				else
					newColumns.Add(remoteColumn);

				AddIndexDefinition(tableName, remoteColumn, indices);
			}

			foreach (var idx in indices.Values)
			{
				var li = Array.FindIndex(localIndexArray, c => c != null && CompareIndexName(idx.QualifiedName, c.Item1));
				if (li != -1)
				{
					if (idx.IsUnique != localIndexArray[li].Item2)
					{
						removeIndices.Add(localIndexArray[li].Item1);
						changedIndices.Add(idx);
					}
					localIndexArray[li] = null;
				}
				else
					newIndices.Add(idx);
			}
			for (var i = 0; i < localIndexArray.Length; i++)
			{
				if (localIndexArray[i] != null)
				{
					removeIndices.Add(localIndexArray[i].Item1);
					localIndexArray[i] = null;
				}
			}

			if (newIndices.Count > 0 || changedIndices.Count > 0 || removeIndices.Count > 0 || sameColumns.Count < localColumnsArray.Length || newColumns.Count > 0)
			{
				if (!preserveCurrentData) // drop and recreate
				{
					CreateDropScript(commands, tableName);
					CreateTableScript(commands, tableName, remoteColumns, null);
				}
				else if (sameColumns.Count < localColumnsArray.Length) // this is more performant than checking for obsolete columns
				{
					// rename local table
					commands.Add($"ALTER TABLE [{tableName}] RENAME TO [{tableName}_temp];");

					// create a new table, according to new Scheme...
					CreateTableScript(commands, tableName, remoteColumns, localIndexes.Select(c => c.Item1).ToArray());
					// copy
					var insertColumns = new List<string>(sameColumns);
					for (var i = 0; i < newColumns.Count; i++)
					{
						var idx = Array.FindIndex(localColumnsArray, c => String.Compare(c.Name, newColumns[i].Name, StringComparison.OrdinalIgnoreCase) == 0);
						if (idx >= 0)
							insertColumns.Add(newColumns[i].Name);
					}
					commands.Add($"INSERT INTO [{tableName}] ([{String.Join("], [", insertColumns)}]) SELECT [{String.Join("], [", insertColumns)}] FROM [{tableName}_temp];");

					// drop old local table
					commands.Add($"DROP TABLE [{tableName}_temp];");  // no IF EXISTS - at this point the table must exist or error
				}
				else if (newColumns.Count > 0 || newIndices.Count > 0 || changedIndices.Count > 0 || removeIndices.Count > 0) // there are no columns, which have to be deleted - check now if there are new columns to add
				{
					// todo: rk primary key column changed
					foreach (var column in newColumns)
					{
						var commandText = new StringBuilder("ALTER TABLE ");
						AppendSqlIdentifier(commandText, tableName);
						commandText.Append(" ADD COLUMN ");
						AppendSqlIdentifier(commandText, column.Name);
						commandText.Append(' ').Append(ConvertDataTypeToSqLite(column));
						CreateCommandColumnAttribute(commandText, column);
						commands.Add(commandText.ToString());
					}

					if (removeIndices.Count > 0)
					{
						foreach (var c in removeIndices)
							commands.Add($"DROP INDEX IF EXISTS '{c}';");
					}

					if (newIndices.Count > 0 || changedIndices.Count > 0)
					{
						var indexNameArray = localIndexArray.Where(c => c != null).Select(c => c.Item1).ToArray();
						foreach (var idx in changedIndices)
							CreateTableIndex(commands, tableName, idx, indexNameArray);
						foreach (var idx in newIndices)
							CreateTableIndex(commands, tableName, idx, indexNameArray);
					}
				}
				else
					throw new InvalidOperationException();

				return true;
			}
			else
				return false;
		} // proc CreateAlterTableScript

		private static void CreateDropScript(List<string> commands, string tableName)
		{
			commands.Add($"DROP TABLE IF EXISTS '{tableName}';");
		} // proc CreateDropScript

		private static void CreateTableIndex(List<string> commands, string tableName, IndexDef idx, string[] localIndexArray)
		{
			var commandText = new StringBuilder("CREATE");
			if (idx.IsUnique)
				commandText.Append(" UNIQUE");
			commandText.Append(" INDEX ");

			var indexName = idx.QualifiedName;
			var baseName = indexName;
			if (localIndexArray != null)
			{
				var nameIndex = 1;
				while (Array.Exists(localIndexArray, c => String.Compare(c, indexName, StringComparison.OrdinalIgnoreCase) == 0))
					indexName = baseName + (nameIndex++).ToString();
			}

			AppendSqlIdentifier(commandText, indexName);
			commandText.Append(" ON ");
			AppendSqlIdentifier(commandText, tableName);
			commandText.Append(" (");
			var first = true;
			foreach (var col in idx.Columns)
			{
				if (first)
					first = false;
				else
					commandText.Append(", ");
				AppendSqlIdentifier(commandText, col.Name);
			}
			commandText.Append(");");

			commands.Add(commandText.ToString());
		} // proc CreateSqLiteIndex

		private static int GetLengthWithoutTrailingNumbers(string n)
		{
			if (String.IsNullOrEmpty(n))
				return 0;

			for (var i = n.Length - 1; i >= 0; i--)
			{
				if (!Char.IsDigit(n[i]))
					return i;
			}

			return 0;
		} // func GetLengthWithoutTrailingNumbers

		private static bool CompareIndexName(string name1, string name2)
		{
			// find trailing numbers
			var l1 = GetLengthWithoutTrailingNumbers(name1);
			var l2 = GetLengthWithoutTrailingNumbers(name2);
			if (l1 != l2)
				return false;
			else
				return String.Compare(name1, 0, name2, 0, l1, StringComparison.OrdinalIgnoreCase) == 0;
		} // func CompareIndexName

		private static void AddIndexDefinition(string tableName, IDataColumn column, Dictionary<string, IndexDef> indices)
		{
			if (column.Attributes.GetProperty("IsPrimary", true))
			{
				var primaryKeyIndexName = tableName + "_" + column.Name + "_primaryIndex";
				indices.Add(primaryKeyIndexName, new IndexDef(primaryKeyIndexName, true, column));
			}

			if (IsIndexColumn(tableName, column, out var indexName, out var isUnique))
			{
				if (indices.TryGetValue(indexName, out var t))
				{
					t.IsUnique = isUnique;
					t.Columns.Add(column);
				}
				else
					indices[indexName] = new IndexDef(indexName, isUnique, column);
			}
		} // proc AddIndexDefinition

		private static bool IsIndexColumn(string tableName, IDataColumn column, out string indexName, out bool isUnique)
		{
			if (column.Attributes.GetProperty("IsUnique", false))
			{
				indexName = GetDefaultIndexName(tableName, column);
				isUnique = true;
				return true;
			}
			else
			{
				indexName = column.Attributes.GetProperty("Index", (string)null);
				if (indexName != null
					|| (column is PpsDataColumnDefinition dc && dc.IsRelationColumn))
				{
					if (indexName == null || String.Compare(indexName, Boolean.TrueString, StringComparison.OrdinalIgnoreCase) == 0)
						indexName = GetDefaultIndexName(tableName, column);
					isUnique = false;
					return true;
				}
				else
				{
					isUnique = false;
					indexName = null;
					return false;
				}
			}
		} // func IsIndexColumn

		private static string GetDefaultIndexName(string tableName, IDataColumn column)
			=> tableName + "_" + column.Name + "_index";

		private static StringBuilder CreateCommandColumnAttribute(StringBuilder commandText, IDataColumn column)
		{
			// not? null
			if (!column.Attributes.GetProperty("Nullable", false))
				commandText.Append(" NOT");
			commandText.Append(" NULL");

			// append default
			if (!String.IsNullOrEmpty(column.Attributes.GetProperty("Default", String.Empty)))
				commandText.Append(" DEFAULT ").Append("'").Append(column.Attributes.GetProperty("Default", String.Empty)).Append("'");

			return commandText;
		} // func CreateCommandColumnAttribute

		private static StringBuilder AppendSqlIdentifier(StringBuilder commandText, string name)
			=> commandText.Append('[').Append(name).Append(']');

		#endregion

		#region -- FetchData ----------------------------------------------------------

		#region -- class ProcessBatchBase ---------------------------------------------

		private abstract class ProcessBatchBase : IDisposable
		{
			#region -- class PpsDataRowOperationArgument ------------------------------

			private sealed class PpsDataRowOperationArgument : IPpsDataRowOperationArguments
			{
				private readonly PpsDataTableDefinition table;
				private readonly string[] parameterValues;

				public PpsDataRowOperationArgument(PpsDataTableDefinition table, string[] parameterValues)
				{
					this.table = table ?? throw new ArgumentNullException(nameof(table));
					this.parameterValues = parameterValues ?? throw new ArgumentNullException(nameof(table));
				} // ctor

				public IEnumerator<PropertyValue> GetEnumerator()
				{
					for (var i = 0; i < parameterValues.Length; i++)
					{
						var v = (object)parameterValues[i];
						var t = table.Columns[i].DataType;
						if (v != null)
							v = Procs.ChangeType(v, t);

						yield return new PropertyValue(table.Columns[i].Name, t, v);
					}
				} // func GetEnumerator

				IEnumerator IEnumerable.GetEnumerator()
					=> GetEnumerator();

				public bool TryGetProperty(string name, out object value)
				{
					var idx = table.FindColumnIndex(name, false);
					if (idx >= 0 && idx < parameterValues.Length)
					{
						value = parameterValues[idx];
						return true;
					}
					else
					{
						value = null;
						return false;
					}
				} // func TryGetProperty

				public string GetName(int i) 
					=> table.Columns[i].Name;

				public object GetValue(int i)
				{
					if (i >= 0 && i < parameterValues.Length)
					{
						if (parameterValues[i] == null)
							return null;
						else if (table.Columns[i].IsExtended)
						{
							var type = table.Columns[i].DataType;
							if (typeof(PpsObjectExtendedValue) == type
								|| typeof(PpsMasterDataExtendedValue) == type)
								return Procs.ChangeType(parameterValues[i], typeof(long));
							else
								return parameterValues[i];
						}
						else
							return Procs.ChangeType(parameterValues[i], table.Columns[i].DataType);
					}
					else
						return null;
				} // func GetValue

				public int GetValues(object[] values)
				{
					for (var i = 0; i < values.Length; i++)
						values[i] = GetValue(i);
					return values.Length;
				} // func GetValues

				public int GetOrdinal(string name) 
					=> table.FindColumnIndex(name, false);

				public Type GetFieldType(int i)
					=> table.Columns[i].DataType;

				public string GetDataTypeName(int i)
					=> GetFieldType(i).Name;

				public bool GetBoolean(int i)
					=> GetValue(i).ChangeType<bool>();

				public byte GetByte(int i)
					=> GetValue(i).ChangeType<byte>();

				public long GetBytes(int i, long fieldOffset, byte[] buffer, int bufferoffset, int length)
					=> throw new NotImplementedException();
				public char GetChar(int i) 
					=> throw new NotImplementedException();
				public long GetChars(int i, long fieldoffset, char[] buffer, int bufferoffset, int length) 
					=> throw new NotImplementedException();

				public Guid GetGuid(int i)
					=> GetValue(i).ChangeType<Guid>();

				public short GetInt16(int i)
					=> GetValue(i).ChangeType<short>();
				public int GetInt32(int i)
					=> GetValue(i).ChangeType<int>();
				public long GetInt64(int i)
					=> GetValue(i).ChangeType<long>();

				public float GetFloat(int i)
					=> GetValue(i).ChangeType<float>();
				public double GetDouble(int i)
					=> GetValue(i).ChangeType<double>();
				public string GetString(int i)
					=> GetValue(i).ChangeType<string>();

				public decimal GetDecimal(int i)
					=> GetValue(i).ChangeType<decimal>();

				public DateTime GetDateTime(int i)
					=> GetValue(i).ChangeType<DateTime>();
				public IDataReader GetData(int i) 
					=> throw new NotSupportedException();

				public bool IsDBNull(int i)
					=> GetValue(i) == null;

				public int FieldCount => table.Columns.Count;

				public object this[int i]
					=> GetValue(i);

				public object this[string name]
				{
					get
					{
						var idx = table.FindColumnIndex(name, false);
						return idx == -1 ? null : GetValue(idx);
					}
				} // prop this
			} // class PpsDataRowOperationArgument

			#endregion

			private readonly PpsDataTableDefinition table;
			private readonly PpsMasterDataTransaction transaction;
			private readonly bool isFull;

			private bool isDisposed = false;

			#region -- Ctor/Dtor ------------------------------------------------------

			public ProcessBatchBase(PpsMasterDataTransaction transaction, string tableName, bool isFull)
			{
				this.transaction = transaction;
				this.isFull = isFull;

				// check definition
				this.table = transaction.MasterData.schema.FindTable(tableName) ?? throw new ArgumentOutOfRangeException(nameof(tableName), tableName, $"Could not find master table '{tableName}.'");
			} // ctor

			// This code added to correctly implement the disposable pattern.
			public void Dispose()
			{
				Dispose(true);
			} // proc Dispose

			protected virtual void Dispose(bool disposing)
			{
				if (!isDisposed)
				{
					if (disposing)
					{
					}
					isDisposed = true;
				}
			} // proc Dispose

			#endregion

			#region -- Prepare, Clean, Parse ------------------------------------------

			public abstract Task PrepareAsync();

			public abstract Task<bool> CleanAsync();

			/// <summary></summary>
			/// <returns></returns>
			protected async Task<bool> CleanUntouchedRows()
			{
				var deleted = false;

				// raise delete events
				using (var cmd = Transaction.CreateNativeCommand($"SELECT [{Table.PrimaryKey.Name}] FROM main.[{Table.Name}] WHERE [" + refreshColumnName + "] is null"))
				using (var r = cmd.ExecuteReader(CommandBehavior.SingleResult))
				{
					while (r.Read())
					{
						Transaction.RaiseOperationEvent(new PpsDataRowOperationEventArgs(PpsDataRowOperation.RowDelete, Table, r.GetValue(0), null, null), false);
						deleted = true;
					}
				}

				// execute delete
				if (deleted)
				{
					using (var cmd = Transaction.CreateNativeCommand($"DELETE FROM main.[{Table.Name}] WHERE [" + refreshColumnName + "] is null"))
						await cmd.ExecuteNonQueryExAsync();
				}

				return deleted;
			} // func CleanUntouchedRows

			public async Task<bool> ParseAsync(XmlReader xml, IProgress<string> progress)
			{
				var objectCounter = 0;
				var lastProgress = Environment.TickCount;
				var parameterValues = new string[ColumnCount];

				while (xml.NodeType == XmlNodeType.Element)
				{
					if (xml.IsEmptyElement) // skip empty element
					{
						await xml.ReadAsync();
						continue;
					}

					// action to process
					var actionName = xml.LocalName.ToLower();
					if (actionName != "r"
						&& actionName != "u"
						&& actionName != "i"
						&& actionName != "d"
						&& actionName != "syncid")
						throw new InvalidOperationException($"The operation {actionName} is not supported.");

					if (actionName == "syncid")
					{
						#region -- update SyncState --
						await xml.ReadAsync(); // read element

						var newSyncId = await xml.GetElementContentAsync<long>(-1);
						if (newSyncId == -1)
						{
							using (var cmd = Transaction.CreateNativeCommand("DELETE FROM main.[SyncState] WHERE [Table] = @Table"))
							{
								cmd.AddParameter("@Table", DbType.String, Table.Name);
								cmd.ExecuteNonQueryEx();
							}
						}
						else
						{
							using (var cmd = Transaction.CreateNativeCommand(
								"INSERT OR REPLACE INTO main.[SyncState] ([Table], [SyncId]) " +
								"VALUES (@Table, @SyncId);"))
							{
								cmd.AddParameter("@Table", DbType.String, Table.Name);
								cmd.AddParameter("@SyncId", DbType.Int64, newSyncId);
								await cmd.ExecuteNonQueryExAsync();
							}
						}
						#endregion
					}
					else
					{
						#region -- upsert --

						Array.Clear(parameterValues, 0, parameterValues.Length);

						// collect values
						await xml.ReadAsync();
						while (xml.NodeType == XmlNodeType.Element)
						{
							if (xml.IsEmptyElement) // read column data
								await xml.ReadAsync();
							else
							{
								var columnName = xml.LocalName;
								if (columnName.StartsWith("c") && Int32.TryParse(columnName.Substring(1), out var columnIndex))
								{
									await xml.ReadAsync();
									parameterValues[columnIndex] = await xml.ReadContentAsStringAsync();
									await xml.ReadEndElementAsync();
								}
								else
									await xml.SkipAsync();
							}
						}

						await ProcessCurrentNodeAsync(actionName, parameterValues);

						objectCounter++;
						if (progress != null && unchecked(Environment.TickCount - lastProgress) > 500)
						{
							progress.Report(String.Format(Resources.MasterDataFetchSyncString, Table.Name + " (" + objectCounter.ToString("N0") + ")"));
							lastProgress = Environment.TickCount;
						}

						#endregion
					}

					await xml.ReadEndElementAsync();
				}
				if (objectCounter > 0)
				{
					Trace.TraceInformation($"Synchonization of {Table.Name} finished ({objectCounter:N0} objects).");
					return true;
				}
				else
					return false;
			} // proc Parse

			protected abstract Task ProcessCurrentNodeAsync(string actionName, string[] parameterValues);

			/// <summary></summary>
			/// <param name="parameterValues"></param>
			/// <returns></returns>
			protected IPpsDataRowOperationArguments CreateOperationArguments(string[] parameterValues)
				=> new PpsDataRowOperationArgument(Table, parameterValues);

			#endregion

			protected abstract int ColumnCount { get; }

			public PpsMasterData MasterData => transaction.MasterData;
			public PpsMasterDataTransaction Transaction => transaction;
			public bool IsFull => isFull;
			public PpsDataTableDefinition Table => table;
		} // class ProcessBatchBase

		#endregion

		#region -- class ProcessBatchTags ---------------------------------------------

		private sealed class ProcessBatchTags : ProcessBatchBase
		{
			private readonly DbCommand existsCommand;
			private readonly DbParameter parameterExistsId;
			private readonly DbParameter parameterExistsObjectId;
			private readonly DbParameter parameterExistsClass;
			private readonly DbParameter parameterExistsKey;
			private readonly DbParameter parameterExistsUserId;

			private readonly DbCommand updateCommand;
			private readonly DbParameter parameterUpdateOldId;
			private readonly DbParameter parameterUpdateNewId;
			private readonly DbParameter parameterUpdateObjectId;
			private readonly DbParameter parameterUpdateKey;
			private readonly DbParameter parameterUpdateUserId;
			private readonly DbParameter parameterUpdateClass;
			private readonly DbParameter parameterUpdateValue;
			private readonly DbParameter parameterUpdateCreateDate;


			private readonly DbCommand insertCommand;
			private readonly DbParameter parameterInsertId;
			private readonly DbParameter parameterInsertObjectId;
			private readonly DbParameter parameterInsertKey;
			private readonly DbParameter parameterInsertUserId;
			private readonly DbParameter parameterInsertClass;
			private readonly DbParameter parameterInsertValue;
			private readonly DbParameter parameterInsertLClass;
			private readonly DbParameter parameterInsertLValue;
			private readonly DbParameter parameterInsertCreateDate;

			private readonly DbCommand deleteCommand;
			private readonly DbParameter parameterDeleteId;

			public ProcessBatchTags(PpsMasterDataTransaction transaction, string tableName, bool isFull)
				: base(transaction, tableName, isFull)
			{
				existsCommand = transaction.CreateNativeCommand("SELECT [Id] FROM main.[ObjectTags] WHERE [Id] = @OldId OR ([ObjectId] = @ObjectId AND [Key] = @Key AND ifnull([LocalClass], [Class]) = @Class AND [UserId] = @UserId)");
				parameterExistsId = existsCommand.AddParameter("@OldId", DbType.Int64);
				parameterExistsObjectId = existsCommand.AddParameter("@ObjectId", DbType.Int64);
				parameterExistsClass = existsCommand.AddParameter("@Class", DbType.Int32);
				parameterExistsKey = existsCommand.AddParameter("@Key", DbType.String);
				parameterExistsUserId = existsCommand.AddParameter("@UserId", DbType.Int64);

				updateCommand = transaction.CreateNativeCommand("UPDATE main.[ObjectTags] SET " +
					"[Id] = @NewId, " +
					"[ObjectId] = @ObjectId, " +
					"[Key] = @Key, " +
					"[UserId] = @UserId, " +
					"[Class] = @Class, " +
					"[Value] = @Value, " +
					"[CreateDate] = @CreateDate, " +
					"[LocalClass] = CASE WHEN [UserId] <> 0 THEN null ELSE [LocalClass] END, " +
					"[LocalValue] = CASE WHEN [UserId] <> 0 THEN null ELSE [LocalValue] END, " +
					"[" + refreshColumnName + "] = CASE WHEN [UserId] <> 0 THEN 0 ELSE [" + refreshColumnName + "] END " +
					"WHERE [Id] = @OldId"
				);
				parameterUpdateOldId = updateCommand.AddParameter("@OldId", DbType.Int64);
				parameterUpdateNewId = updateCommand.AddParameter("@NewId", DbType.Int64);
				parameterUpdateObjectId = updateCommand.AddParameter("@ObjectId", DbType.Int64);
				parameterUpdateKey = updateCommand.AddParameter("@Key", DbType.String);
				parameterUpdateUserId = updateCommand.AddParameter("@UserId", DbType.Int64);
				parameterUpdateClass = updateCommand.AddParameter("@Class", DbType.Int32);
				parameterUpdateValue = updateCommand.AddParameter("@Value", DbType.String);
				parameterUpdateCreateDate = updateCommand.AddParameter("@CreateDate", DbType.DateTime);

				insertCommand = transaction.CreateNativeCommand("INSERT INTO main.[ObjectTags] ([Id], [ObjectId], [Key], [UserId], [Class], [Value], [CreateDate], [LocalClass], [LocalValue], [" + refreshColumnName + "]) VALUES (@Id, @ObjectId, @Key, @UserId, @Class, @Value, @CreateDate, @LClass, @LValue, 0)");
				parameterInsertId = insertCommand.AddParameter("@Id", DbType.Int64);
				parameterInsertObjectId = insertCommand.AddParameter("@ObjectId", DbType.Int64);
				parameterInsertKey = insertCommand.AddParameter("@Key", DbType.String);
				parameterInsertUserId = insertCommand.AddParameter("@UserId", DbType.Int64);
				parameterInsertClass = insertCommand.AddParameter("@Class", DbType.Int32);
				parameterInsertValue = insertCommand.AddParameter("@Value", DbType.String);
				parameterInsertLClass = insertCommand.AddParameter("@LClass", DbType.Int32);
				parameterInsertLValue = insertCommand.AddParameter("@LValue", DbType.String);
				parameterInsertCreateDate = insertCommand.AddParameter("@CreateDate", DbType.DateTime);

				deleteCommand = transaction.CreateNativeCommand("DELETE FROM main.[ObjectTags] WHERE Id = @Id AND ([UserId] <> 0 OR ([UserId] = 0 AND [LocalClass] IS NULL))");
				parameterDeleteId = deleteCommand.AddParameter("@Id", DbType.Int64);
			} // ctor

			public override async Task PrepareAsync()
			{
				if (IsFull)
				{
					// mark all columns
					using (var cmd = Transaction.CreateNativeCommand("UPDATE main.[ObjectTags] SET [" + refreshColumnName + "] = null WHERE [" + refreshColumnName + "] <> 1 AND [LocalClass] IS NULL"))
						await cmd.ExecuteNonQueryExAsync();
				}
			} // proc PrepareAsync

			protected override async Task ProcessCurrentNodeAsync(string actionName, string[] parameterValues)
			{
				if (actionName[0] == 'd')
				{
					parameterDeleteId.Value = ConvertStringToSQLiteValue(parameterValues[0], DbType.Int64);
					await deleteCommand.ExecuteNonQueryExAsync();

					var remoteId = Convert.ToInt64(parameterValues[0]);
					Transaction.RaiseOperationEvent(new PpsDataRowOperationEventArgs(PpsDataRowOperation.RowDelete, Table, remoteId, null, null), false);
				}
				else
				{
					var remoteId = ConvertStringToSQLiteValue(parameterValues[0], DbType.Int64);
					var remoteObjectId = ConvertStringToSQLiteValue(parameterValues[1], DbType.Int64);
					var remoteKey = parameterValues[2];
					var remoteClass = ConvertStringToSQLiteValue(parameterValues[3], DbType.Int32);
					var remoteValue = parameterValues[4];
					var remoteUserId = ConvertStringToSQLiteValue(parameterValues[7], DbType.Int64);
					var remoteDateTime = ConvertStringToSQLiteValue(parameterValues[8], DbType.DateTime);

					// check if the row exists
					parameterExistsId.Value = remoteId;
					parameterExistsObjectId.Value = remoteObjectId;
					parameterExistsClass.Value = remoteClass;
					parameterExistsKey.Value = remoteKey;
					parameterExistsUserId.Value = remoteUserId;

					long? rowId;
					using (var r = await existsCommand.ExecuteReaderExAsync())
						rowId = r.Read() ? r.GetInt64(0) : (long?)null;

					// update row
					if (rowId.HasValue)
					{
						parameterUpdateOldId.Value = rowId.Value;
						parameterUpdateNewId.Value = remoteId;
						parameterUpdateObjectId.Value = remoteObjectId;
						parameterUpdateKey.Value = remoteKey;
						parameterUpdateUserId.Value = remoteUserId;
						parameterUpdateClass.Value = remoteClass;
						parameterUpdateValue.Value = remoteValue;
						parameterUpdateCreateDate.Value = remoteDateTime;

						await updateCommand.ExecuteNonQueryExAsync();

						Transaction.RaiseOperationEvent(new PpsDataRowOperationEventArgs(PpsDataRowOperation.RowUpdate, Table, remoteId, rowId.Value, CreateOperationArguments(parameterValues)), false);
					}
					else // insert row
					{
						parameterInsertId.Value = remoteId;
						parameterInsertObjectId.Value = remoteObjectId;
						parameterInsertKey.Value = remoteKey;
						parameterInsertUserId.Value = remoteUserId;
						parameterInsertClass.Value = remoteClass;
						parameterInsertValue.Value = remoteValue;
						parameterInsertLClass.Value = ConvertStringToSQLiteValue(parameterValues[5], DbType.Int32);
						parameterInsertLValue.Value = parameterValues[6];
						parameterInsertCreateDate.Value = remoteDateTime;

						await insertCommand.ExecuteNonQueryExAsync();
						var newId = ((SQLiteConnection)insertCommand.Connection).LastInsertRowId;

						Transaction.RaiseOperationEvent(new PpsDataRowOperationEventArgs(PpsDataRowOperation.RowInsert, Table, newId, null, CreateOperationArguments(parameterValues)), false);
					}
				}
			} // proc ProcessCurrentNodeAsync

			public override Task<bool> CleanAsync()
				=> IsFull ? CleanUntouchedRows() : Task.FromResult(false);

			protected override int ColumnCount => 9;
		} // class ProcessBatchTags

		#endregion

		#region -- class ProcessBatchGeneric ------------------------------------------

		private sealed class ProcessBatchGeneric : ProcessBatchBase
		{
			private readonly int physPrimaryColumnIndex;
			private readonly int virtPrimaryColumnIndex;
			private readonly SQLiteCommand existCommand;
			private readonly SQLiteParameter existIdParameter;

			private readonly SQLiteCommand insertCommand;
			private readonly SQLiteParameter[] insertParameters;

			private readonly SQLiteCommand updateCommand;
			private readonly SQLiteParameter[] updateParameters;

			private readonly SQLiteCommand deleteCommand;
			private readonly SQLiteParameter deleteIdParameter;

			private readonly int refreshColumnIndex = -1;

			#region -- Ctor/Dtor ----------------------------------------------------

			public ProcessBatchGeneric(PpsMasterDataTransaction transaction, string tableName, bool isFull)
				: base(transaction, tableName, isFull)
			{
				var physPrimaryKey = Table.PrimaryKey ?? throw new ArgumentException($"Table '{Table.Name}' has no primary key.", nameof(Table.PrimaryKey));

				var alternativePrimaryKey = Table.Meta.GetProperty<string>("useAsKey", null);
				var virtPrimaryKey = String.IsNullOrEmpty(alternativePrimaryKey) ? physPrimaryKey : Table.Columns[alternativePrimaryKey];

				refreshColumnIndex = Table.FindColumnIndex(refreshColumnName);

				// prepare column parameter
				insertCommand = (SQLiteCommand)transaction.CreateNativeCommand(String.Empty);
				updateCommand = (SQLiteCommand)transaction.CreateNativeCommand(String.Empty);
				insertParameters = new SQLiteParameter[Table.Columns.Count];
				updateParameters = new SQLiteParameter[Table.Columns.Count];

				physPrimaryColumnIndex = -1;
				virtPrimaryColumnIndex = -1;
				for (var i = 0; i < Table.Columns.Count; i++)
				{
					var column = Table.Columns[i];
					var syncSourceColumn = column.Meta.GetProperty(PpsDataColumnMetaData.SourceColumn, String.Empty);
					if (syncSourceColumn == "#")
					{
						if (column == physPrimaryKey)
							throw new ArgumentException($"Primary column '{column.Name}' is not in sync list.");
						if (column == virtPrimaryKey)
							throw new ArgumentException($"Alternative primary column '{column.Name}' is not in sync list.");

						// exclude from update list
						insertParameters[i] = null;
						updateParameters[i] = null;
					}
					else
					{
						if (column == physPrimaryKey)
							physPrimaryColumnIndex = i;
						if (column == virtPrimaryKey)
							virtPrimaryColumnIndex = i;

						insertParameters[i] = insertCommand.Parameters.Add("@" + column.Name, ConvertDataTypeToDbType(column.DataType));
						insertParameters[i].SourceColumn = column.Name;
						updateParameters[i] = updateCommand.Parameters.Add("@" + column.Name, ConvertDataTypeToDbType(column.DataType));
						updateParameters[i].SourceColumn = column.Name;
					}
				}

				// prepare insert, update
				bool excludeNull(SQLiteParameter p)
					=> p != null;

				string insertColumnList()
				{
					var t = String.Join(", ", insertParameters.Where(excludeNull).Select(c => "[" + c.SourceColumn + "]"));
					if (refreshColumnIndex >= 0)
						t += ",[" + refreshColumnName + "]";
					return t;
				}

				string insertValueList()
				{
					var t = String.Join(", ", insertParameters.Where(excludeNull).Select(c => c.ParameterName));
					if (refreshColumnIndex >= 0)
						t += ",0";
					return t;
				}

				string updateColumnValueList()
				{
					var t = String.Join(", ", updateParameters.Where(excludeNull).Where(c => c != updateParameters[virtPrimaryColumnIndex]).Select(c => "[" + c.SourceColumn + "] = " + c.ParameterName));
					if (refreshColumnIndex >= 0)
						t += ",[" + refreshColumnName + "]=IFNULL([" + refreshColumnName + "], 0)";
					return t;
				}

				insertCommand.CommandText =
					"INSERT INTO main.[" + Table.Name + "] (" + insertColumnList() + ") " +
					"VALUES (" + insertValueList() + ");";

				updateCommand.CommandText = "UPDATE main.[" + Table.Name + "] SET " +
					updateColumnValueList() +
					" WHERE [" + updateParameters[virtPrimaryColumnIndex].SourceColumn + "] = " + updateParameters[virtPrimaryColumnIndex].ParameterName;

				// prepare exists
				existCommand = (SQLiteCommand)transaction.CreateNativeCommand("SELECT EXISTS(SELECT * FROM main.[" + Table.Name + "] WHERE [" + virtPrimaryKey.Name + "] = @Id)");
				existIdParameter = existCommand.Parameters.Add("@Id", ConvertDataTypeToDbType(virtPrimaryKey.DataType));

				// prepare delete
				deleteCommand = (SQLiteCommand)transaction.CreateNativeCommand("DELETE FROM main.[" + Table.Name + "] WHERE [" + physPrimaryKey.Name + "] = @Id;");
				deleteIdParameter = deleteCommand.Parameters.Add("@Id", ConvertDataTypeToDbType(physPrimaryKey.DataType));

				existCommand.Prepare();
				insertCommand.Prepare();
				updateCommand.Prepare();
				deleteCommand.Prepare();
			} // ctor

			protected override void Dispose(bool disposing)
			{
				try
				{
					if (disposing)
					{
						existCommand?.Dispose();
						insertCommand?.Dispose();
						updateCommand?.Dispose();
						deleteCommand?.Dispose();
					}
				}
				finally
				{
					base.Dispose(disposing);
				}
			}// proc Dispose

			#endregion

			#region -- Parse --------------------------------------------------------

			public async override Task PrepareAsync()
			{
				// clear table, is full mode
				if (IsFull)
				{
					if (refreshColumnIndex == -1)
					{
						// remove all values
						using (var cmd = Transaction.CreateNativeCommand($"DELETE FROM main.[{Table.Name}]"))
							await cmd.ExecuteNonQueryExAsync();

						// mark cached rows
						Transaction.RaiseOperationEvent(new PpsDataTableOperationEventArgs(Table, PpsDataRowOperation.UnTouchRows));
					}
					else
					{
						using (var cmd = Transaction.CreateNativeCommand($"UPDATE main.[{Table.Name}] SET [" + refreshColumnName + "] = null WHERE [" + refreshColumnName + "] <> 1"))
							await cmd.ExecuteNonQueryExAsync();
					}
				}
			} // proc PrepareAsync

			public override Task<bool> CleanAsync()
			{
				if (IsFull)
				{
					if (refreshColumnIndex >= 0)
						return CleanUntouchedRows();
					else
					{
						Transaction.RaiseOperationEvent(new PpsDataTableOperationEventArgs(Table, PpsDataRowOperation.UnTouchedDeleteRows));
						return Task.FromResult(true);
					}
				}
				else
					return Task.FromResult(false);
			} // proc CleanAsync

			protected override async Task ProcessCurrentNodeAsync(string actionName, string[] parameterValues)
			{
				if (IsFull || actionName[0] == 'i')
					actionName = refreshColumnIndex == -1 ? "i" : "r";

				existIdParameter.Value = DBNull.Value;
				deleteIdParameter.Value = DBNull.Value;

				// collect columns
				for (var i = 0; i < parameterValues.Length; i++)
				{
					// clear current column set
					if (updateParameters[i] != null)
						updateParameters[i].Value = DBNull.Value;
					if (insertParameters[i] != null)
						insertParameters[i].Value = DBNull.Value;

					// set values
					if (parameterValues[i] != null)
					{
						var value = ConvertStringToSQLiteValue(parameterValues[i], updateParameters[i].DbType);

						updateParameters[i].Value = value;
						insertParameters[i].Value = value;

						if (i == virtPrimaryColumnIndex)
							existIdParameter.Value = value;
						if (i == physPrimaryColumnIndex)
							deleteIdParameter.Value = value;
					}
				}

				// process action
				var arguments = CreateOperationArguments(parameterValues);
				switch (actionName[0])
				{
					case 'r':
						if (await RowExistsAsync())
							goto case 'u';
						else
							goto case 'i';
					case 'i':
						// execute the command
						await ExecuteCommandAsync(insertCommand);
						// raise change event

						Transaction.RaiseOperationEvent(new PpsDataRowOperationEventArgs(PpsDataRowOperation.RowInsert, Table, arguments[physPrimaryColumnIndex], null, arguments), false);
						break;
					case 'u':
						// execute the command
						await ExecuteCommandAsync(updateCommand);
						// raise change event
						Transaction.RaiseOperationEvent(new PpsDataRowOperationEventArgs(PpsDataRowOperation.RowUpdate, Table, arguments[physPrimaryColumnIndex], null, arguments), false);
						break;
					case 'd':
						// execute the command
						await ExecuteCommandAsync(deleteCommand);
						// raise change event
						Transaction.RaiseOperationEvent(new PpsDataRowOperationEventArgs(PpsDataRowOperation.RowDelete, Table, arguments[physPrimaryColumnIndex], null, null), false);
						break;
				}
			} // proc ProcessCurrentNode

			private async Task<bool> RowExistsAsync()
			{
				using (var r = await existCommand.ExecuteReaderExAsync(CommandBehavior.SingleRow))
				{
					if (r.Read())
						return r.GetBoolean(0);
					else
					{
						var exc = new ArgumentException();
						exc.Data.Add("SQL-Command", existCommand.CommandText);
						throw exc;
					}
				}
			} // func RowExistsAsync

			private Task ExecuteCommandAsync(SQLiteCommand command)
				=> command.ExecuteNonQueryExAsync();

			#endregion

			protected override int ColumnCount => updateParameters.Length;
		} // class ProcessBatch

		#endregion

		private void WriteCurentSyncState(XmlWriter xml)
		{
			xml.WriteStartElement("sync");
			if (lastSynchronizationStamp > DateTime.MinValue)
				xml.WriteAttributeString("lastSyncTimeStamp", lastSynchronizationStamp.ToFileTimeUtc().ChangeType<string>());

			// write sync state for the tables
			using (var cmd = new SQLiteCommand("SELECT [Table], [SyncId] FROM main.[SyncState]", connection))
			using (var r = cmd.ExecuteReaderEx(CommandBehavior.SingleResult))
			{
				while (r.Read())
				{
					if (!r.IsDBNull(1))
					{
						xml.WriteStartElement("sync");
						xml.WriteAttributeString("table", r.GetString(0));
						xml.WriteAttributeString("syncId", r.GetInt64(1).ChangeType<string>());
						xml.WriteEndElement();
					}
				}
			}

			// write user tags to sync to the server
			// all system tags are ignored
			if (isObjectTagsDirty)
			{
				isObjectTagsDirty = false; // reset

				// check for changes
				using (var cmd = new SQLiteCommand("SELECT [Id], [ObjectId], [Key], [LocalClass], [LocalValue], [UserId], [CreateDate] FROM main.[" + ObjectTagsTable.Name + "] WHERE [ObjectId] >= 0 AND [" + refreshColumnName + "] <> 0 AND [UserId] > 0 AND [LocalClass] IS NOT NULL", connection))
				using (var r = cmd.ExecuteReaderEx(CommandBehavior.SingleResult))
				{
					while (r.Read())
					{
						xml.WriteStartElement("utag");

						// ids
						xml.WriteAttributeString("id", r.GetInt64(0).ChangeType<string>());
						xml.WriteAttributeString("objectId", r.GetInt64(1).ChangeType<string>());

						// tag info
						xml.WriteAttributeString("name", r.GetString(2));
						xml.WriteAttributeString("tagClass", r.GetInt32(3).ChangeType<string>());
						xml.WriteAttributeString("userId", r.GetInt64(5).ChangeType<string>());
						xml.WriteAttributeString("createDate", (r.IsDBNull(6) ? DateTime.Now : r.GetDateTime(6)).ToUniversalTime().ChangeType<string>());

						if (!r.IsDBNull(4))
							xml.WriteValue(r.GetString(4));

						xml.WriteEndElement();
					}
				}
			}

			xml.WriteEndElement();
		} // proc WriteCurentSyncState

		private async Task<bool> FetchDataAsync(bool nonBlocking, IProgress<string> progess = null)
		{
			// create request
			var requestString = "/remote/wpf/?action=mdata";

			// parse and process result
			using (var xml = environment.Request.GetXmlStream(await environment.Request.PutXmlResponseAsync(requestString, MimeTypes.Text.Xml, WriteCurentSyncState),
				settings: new XmlReaderSettings() { IgnoreComments = true, IgnoreWhitespace = true, Async = true })
			)
			{
				await xml.ReadStartElementAsync("mdata");
				if (!xml.IsEmptyElement)
				{
					// read batches
					while (xml.NodeType == XmlNodeType.Element)
					{
						switch (xml.LocalName)
						{
							case "batch":
								// the batch is atomar, and uses a new transaction
								// this conflict with the synchronization lock
								// so we give the chance to cancel the process
								if (!await FetchDataXmlBatchAsync(xml, nonBlocking, progess))
									return false;
								break;
							case "error":
								{
									var msg =
										await xml.ReadAsync()
											? await xml.ReadContentAsStringAsync()
											: "unkown error";
									throw new Exception($"Synchronization error: {msg}");
								}
							case "syncStamp":
								var timeStamp = await xml.ReadElementContentAsync<long>(-1);

								using (var transaction = await CreateWriteTransactionAsync(nonBlocking))
								{
									if (transaction == null)
										return false;

									using (var cmd = transaction.CreateNativeCommand("UPDATE main.Header SET SyncStamp = IFNULL(@syncStamp, SyncStamp)"))
									{
										cmd.AddParameter("@syncStamp", DbType.Int64).Value = timeStamp.DbNullIf(-1L);

										await cmd.ExecuteNonQueryExAsync();
										if (timeStamp >= 0)
											lastSynchronizationStamp = DateTime.FromFileTimeUtc(timeStamp);

										transaction.Commit();
									}
								}
								break;
							default:
								await xml.SkipAsync();
								break;
						}
					}
				}
			}
			isSynchronizationStarted = true;
			return true;
		} // proc FetchDataAsync

		private ProcessBatchBase CreateProcessBatch(PpsMasterDataTransaction transaction, string tableName, bool isFull)
		{
			if (tableName == ObjectTagsTable.Name) // special case for ObjectTags
				return new ProcessBatchTags(transaction, tableName, isFull);
			else
				return new ProcessBatchGeneric(transaction, tableName, isFull);
		} // func CreateProcessBatch

		private async Task<bool> FetchDataXmlBatchAsync(XmlReader xml, bool nonBlocking, IProgress<string> progress)
		{
			// read batch attributes
			var tableName = xml.GetAttribute("table");
			var isFull = xml.GetAttribute("isFull", false);

			progress?.Report(String.Format(Resources.MasterDataFetchSyncString, tableName));

			if (!xml.IsEmptyElement) // batch needs rows
			{
				await xml.ReadAsync();  // fetch element
										// process values
				using (var transaction = await CreateWriteTransactionAsync(nonBlocking))
				{
					if (transaction == null) // process cancelled
						return false;

					using (var b = CreateProcessBatch(transaction, tableName, isFull))
					{
						// prepare table
						await b.PrepareAsync();

						// parse data
						var changed = await b.ParseAsync(xml, progress);

						changed |= await b.CleanAsync();
						transaction.Commit(); // commit can block

						// Run a table change command
						if (changed)
							OnMasterDataTableChanged(new PpsDataTableOperationEventArgs(b.Table, PpsDataRowOperation.TableChanged), false);
					}
				}

				await xml.ReadEndElementAsync();
			}
			else // fetch element
				await xml.ReadAsync();

			return true;
		} // proc FetchDataXmlBatchAsync

		#endregion

		#region -- Table access -------------------------------------------------------

		private readonly Dictionary<PpsDataTableDefinition, WeakReference<PpsMasterDataTable>> cachedTables = new Dictionary<PpsDataTableDefinition, WeakReference<PpsMasterDataTable>>();

		/// <summary>Creates a master data table result for the table definition</summary>
		/// <param name="tableDefinition">Table definition for the new result.</param>
		/// <returns>A new PpsMasterTable or a cache entry.</returns>
		[EditorBrowsable(EditorBrowsableState.Never)]
		public PpsMasterDataTable GetTable(PpsDataTableDefinition tableDefinition)
		{
			if (tableDefinition == null)
				throw new ArgumentNullException(nameof(tableDefinition));

			if (!schema.TableDefinitions.Contains(tableDefinition))
				throw new ArgumentOutOfRangeException(nameof(tableDefinition));

			lock (cachedTables)
			{
				if (cachedTables.TryGetValue(tableDefinition, out var r) && r.TryGetTarget(out var table))
					return table;
				else
				{
					table = new PpsMasterDataTable(this, tableDefinition);
					cachedTables[tableDefinition] = new WeakReference<PpsMasterDataTable>(table);
					return table;
				}
			}
		} // func GetTable

		/// <summary>Creates a ,master data table result for the table name.</summary>
		/// <param name="tableName"></param>
		/// <param name="throwException"></param>
		/// <returns></returns>
		public PpsMasterDataTable GetTable(string tableName, bool throwException = true)
		{
			// find schema
			var tableDefinition = FindTable(tableName, throwException);
			if (tableDefinition == null)
				return null;

			return GetTable(tableDefinition);
		} // func GetTable

		internal PpsDataTableDefinition FindTable(string tableName, bool throwException)
			=> schema?.FindTable(tableName)
			?? (throwException ? throw new ArgumentOutOfRangeException(nameof(tableName), tableName, $"MasterDataTable '{tableName}' not found.") : (PpsDataTableDefinition)null);

		#endregion

		#region -- Synchronization ----------------------------------------------------

		/// <summary>Force a foreground synchronization.</summary>
		/// <returns><c>false</c>, if the sync was cancelled by an blocking transaction.</returns>
		public async Task<bool> RunSynchronization(bool enforce = true)
		{
			using (var progressTracer = environment.Traces.TraceProgress())
			{
				try
				{
					return await SynchronizationAsync(enforce, progressTracer);
				}
				catch (Exception e)
				{
					progressTracer.Except(e);
					throw;
				}
			}
		} // proc RunSynchronization

		/*
		 *  This is the core synchronization dispatcher.
		 *  There are global variables:
		 *  - synchronizationLock: Global lock, that only one request exists at the time.
		 *  - isSynchronizationRunning: Prevents the code to run recursivly in one thread.
		 *                              This variable is thread-based, that other threads can enqueue other calls.
		 *  - updateUserInfo: Writes the UserId, Name to the local database.
		 *  
		 *  Locks:
		 *    1. synchronizationLock
		 *    2. CreateTransactionAsync
		 *    
		 *   Return:
		 *     true: for full sync process
		 *     false: sync was cancelled
		 *  
		 */
		internal async Task<bool> SynchronizationAsync(bool enforce, IProgress<string> progress)
		{
			// check for single thread sync context, to get the monitor work
			StuffThreading.VerifySynchronizationContext();

			if (isSynchronizationRunning.Value)
				throw new InvalidOperationException("Recursion detected.");

			// detect if the is a running transaction in another thread => prevent blocking by setting a timeout to every transaction
			// if we a within an transaction, run the batch in blocking mode
			var nonBlocking = true;
			switch (WriteTransactionState())
			{
				case PpsWriteTransactionState.CurrentThread:
					nonBlocking = false;
					break;
				case PpsWriteTransactionState.OtherThread: // cancel, because it will block soon
					if (enforce)
					{
						using (var cancellationSource = new CancellationTokenSource(360000))
						{
							if (!await WaitForWriteTransactionAsync(cancellationSource.Token))
								throw new InvalidOperationException("Transaction is blocked.");
						}
					}
					return false;
			}

			Monitor.Enter(synchronizationLock); // secure the execution of this function, build a queue
												// it is safe to use the lock here, because the thread scheduler returns always back to the current thread.
			isSynchronizationRunning.Value = true;
			try
			{
				// synchronize schema
				if (schemaIsOutDated.HasValue || await CheckSynchronizationStateAsync())
				{
					if (schemaIsOutDated.Value)
						await UpdateSchemaAsync(progress);
					schemaIsOutDated = false;
				}

				progress?.Report("Synchronization...");

				// Fetch data
				environment.OnBeforeSynchronization();
				try
				{
					// update header
					if (updateUserInfo)
					{
						using (var trans = await CreateWriteTransactionAsync(nonBlocking))
						{
							if (trans == null)
								return false;


							// update header
							using (var cmd = trans.CreateNativeCommand())
							{
								var commandText = new StringBuilder("UPDATE main.[Header] SET ");
								var idx = 1;

								// create command for properties
								foreach (var col in GetLocalTableColumns(connection, "Header"))
								{
									if (col.Name == "SchemaStamp"
										|| col.Name == "SchemaContent"
										|| col.Name == "SyncStamp")
										continue;

									if (idx > 1)
										commandText.Append(", ");

									commandText.Append('[')
										.Append(col.Name)
										.Append("] = @v")
										.Append(idx);

									var v = environment[col.Name];
									if (v == null)
										v = DBNull.Value;
									else
										v = Procs.ChangeType(v, col.DataType);

									cmd.AddParameter("@v" + idx.ToString(), ConvertDataTypeToDbType(col.DataType), v);

									idx++;
								}

								cmd.CommandText = commandText.ToString();
								cmd.ExecuteNonQueryEx();

								trans.Commit();
								updateUserInfo = false;
							}
						}
					}

					// fetch data from server
					if (!await FetchDataAsync(nonBlocking, progress))
						return false;
				}
				finally
				{
					environment.OnAfterSynchronization();
				}
			}
			finally
			{
				isSynchronizationRunning.Value = false;
				Monitor.Exit(synchronizationLock);
			}
			return true;
		} // func SynchronizationAsync

		/// <summary>Tests, if the synchronization needs to be in foreground (last sync it to far away e.g. 1 day)</summary>
		/// <returns></returns>
		internal async Task<bool> CheckSynchronizationStateAsync()
		{
			// check if schema is change
			var schemaUri = environment.GetDocumentUri(MasterDataSchema) ?? throw new ArgumentNullException(MasterDataSchema, "Schema uri missing.");
			var request = WebRequest.Create(environment.Request.GetFullUri(schemaUri));
			request.Method = "HEAD";

			using (var r = await request.GetResponseAsync())
			{
				var schemaDate = r.GetLastModified();
				if (schemaDate == DateTime.MinValue || schemaDate.ToUniversalTime() != lastSynchronizationSchema)
				{
					schemaIsOutDated = true;
					return true;
				}
				else
					schemaIsOutDated = false;
			}

			// is the system "synchrone enough"?
			return schemaIsOutDated.Value || (DateTime.UtcNow - lastSynchronizationStamp) > TimeSpan.FromDays(1);
		} // proc CheckSynchronizationStateAsync

		internal void CheckOfflineCache()
		{
			using (var cmd = connection.CreateCommand())
			{
				cmd.CommandText = "SELECT Path FROM main.OfflineCache " +
					"WHERE ContentType IS NULL OR " +
					"IFNULL(LocalContentSize,-2) <> ServerContentSize OR " +
					"LocalContentLastModification is null OR " +
					"LocalContentLastModification <> ServerContentLastModification";

				using (var r = cmd.ExecuteReaderEx(CommandBehavior.SingleResult))
				{
					while (r.Read())
					{
						var path = r.GetString(0);
						var request = environment.GetProxyRequest(new Uri(path, UriKind.Relative), path);
						request.SetUpdateOfflineCache(c => UpdateOfflineDataAsync(path, c).AwaitTask());
						request.Enqueue(PpsLoadPriority.Background, true);
					}
				}
			}
		} // proc CheckOfflineCache

		/// <summary>Mark user info invalid, to update the sqlite database.</summary>
		public void SetUpdateUserInfo()
			=> updateUserInfo = true;

		/// <summary>Is a synchronization in progess.</summary>
		public bool IsInSynchronization => isSynchronizationRunning.Value;

		#endregion

		#region -- Offline Data -------------------------------------------------------

		#region -- class PpsLocalStoreRequest -----------------------------------------

		private sealed class PpsLocalStoreRequest : WebRequest
		{
			private readonly Uri originalUri;
			private readonly Uri requestUri;
			private readonly MemoryStream content;
			private readonly string localPath;
			private readonly string contentType;
			private readonly bool isCompressed;

			private readonly Func<WebResponse> getResponse;

			public PpsLocalStoreRequest(Uri originalUri, Uri requestUri, MemoryStream content, string localPath, string contentType, bool isCompressed)
			{
				if (content == null && String.IsNullOrEmpty(localPath))
					throw new ArgumentNullException(nameof(content));

				this.requestUri = requestUri ?? throw new ArgumentNullException(nameof(requestUri));
				if (requestUri.IsAbsoluteUri)
					throw new ArgumentNullException("Uri must be relative.", nameof(requestUri));

				this.originalUri = originalUri ?? throw new ArgumentNullException(nameof(originalUri));
				if (!originalUri.IsAbsoluteUri)
					throw new ArgumentNullException("Uri must be original.", nameof(originalUri));

				this.content = content;
				this.localPath = localPath;

				this.contentType = contentType ?? throw new ArgumentNullException(nameof(contentType));
				this.isCompressed = isCompressed;

				this.getResponse = GetResponse;
			} // ctor

			public override IAsyncResult BeginGetRequestStream(AsyncCallback callback, object state)
				=> throw new NotSupportedException();

			public override Stream EndGetRequestStream(IAsyncResult asyncResult)
				=> throw new NotSupportedException();

			public override Stream GetRequestStream()
				=> throw new NotSupportedException();

			public override IAsyncResult BeginGetResponse(AsyncCallback callback, object state)
				=> getResponse.BeginInvoke(callback, state);

			public override WebResponse EndGetResponse(IAsyncResult asyncResult)
				=> getResponse.EndInvoke(asyncResult);

			public override WebResponse GetResponse()
				=> new PpsLocalStoreResponse(originalUri, CreateContentStream(), contentType, isCompressed);

			private Stream CreateContentStream()
			{
				var src = content ?? (Stream)new FileStream(localPath, FileMode.Open, FileAccess.Read);
				if (isCompressed)
					src = new GZipStream(src, CompressionMode.Decompress, false);

				return src;
			} // func CreateContentStream

			public override Uri RequestUri => originalUri;
			public override IWebProxy Proxy { get => null; set { } }
		} // class PpsLocalStoreRequest

		#endregion

		#region -- class PpsLocalStoreResponse ----------------------------------------

		private sealed class PpsLocalStoreResponse : WebResponse
		{
			private readonly Uri responeUri;
			private readonly Stream content;
			private readonly string contentType;
			private readonly bool isCompressed;

			private readonly WebHeaderCollection headers = new WebHeaderCollection();

			public PpsLocalStoreResponse(Uri responseUri, Stream content, string contentType, bool isCompressed)
			{
				this.responeUri = responseUri ?? throw new ArgumentNullException(nameof(responseUri));
				this.content = content ?? throw new ArgumentNullException(nameof(content));
				this.contentType = contentType ?? throw new ArgumentNullException(nameof(contentType));
				this.isCompressed = isCompressed;

				if (!content.CanRead)
					throw new ArgumentException();
			} // ctor

			protected override void Dispose(bool disposing)
			{
				// dispose content stream
				content?.Dispose();

				base.Dispose(disposing);
			} // proc Dispose

			public override Stream GetResponseStream()
				=> content;

			public override string ContentType
			{
				get => contentType;
				set => throw new NotSupportedException();
			} // func ContentType

			public override long ContentLength
			{
				get => content.CanSeek ? content.Length : -1;
				set => throw new NotSupportedException();
			} // func ContentLength

			public override WebHeaderCollection Headers => headers;

			public override bool SupportsHeaders => false;

			public override Uri ResponseUri => responeUri;
		} // class PpsLocalStoreResponse

		#endregion

		internal string GetLocalPath(string relativePath)
		{
			if (Path.IsPathRooted(relativePath))
				return relativePath;
			return Path.Combine(environment.LocalPath.FullName, relativePath);
		} // func GetLocalPath

		internal bool MakeRelativePath(string fullPath, out string relativePath)
		{
			if (Path.IsPathRooted(fullPath))
			{
				// simple starts with test
				var localPath = environment.LocalPath.FullName;
				if (!localPath.EndsWith("\\"))
					localPath += "\\";
				if (fullPath.StartsWith(localPath, StringComparison.OrdinalIgnoreCase))
				{
					relativePath = fullPath.Substring(localPath.Length);
					return true;
				}
				else
				{
					relativePath = fullPath;
					return false;
				}
			}
			else
			{
				relativePath = fullPath;
				return true;
			}
		} // func MakeRelativePath

		private bool MoveReader(SQLiteDataReader r, Uri uri)
		{
			(var path, var arguments) = uri.ParseUri();

			while (r.Read())
			{
				var testUri = new Uri(r.GetString(0), UriKind.Relative);

				// get query is only allowed for absolute queries, so we scan for ?
				if (testUri.OriginalString.IndexOf('?') == -1 && arguments.Count == 0) // path is exact
				{
					if (String.Compare(path, testUri.ParsePath(), StringComparison.OrdinalIgnoreCase) == 0)
						return true;
				}
				else if (arguments.Count > 0)
				{
					var testArguments = testUri.ParseQuery();
					var failed = false;
					foreach (var c in arguments.AllKeys)
					{
						var testValue = testArguments[c];
						if (testValue == null || String.Compare(testValue, arguments[c], StringComparison.OrdinalIgnoreCase) != 0)
						{
							failed = true;
							break;
						}
					}
					if (!failed)
						return true; // all arguments are fit
				}
			}
			return false;
		} // func MoveReader

		internal bool TryGetOfflineCacheFile(Uri requestUri, out IPpsProxyTask task)
		{
			try
			{
				using (var command = new SQLiteCommand("SELECT [Path], [ContentType], [ContentEncoding], [Content], [LocalPath] FROM [main].[OfflineCache] WHERE substr([Path], 1, length(@path)) = @path  COLLATE NOCASE", connection))
				{
					command.Parameters.Add("@path", DbType.String).Value = requestUri.ParsePath();
					using (var reader = command.ExecuteReaderEx(CommandBehavior.SingleRow))
					{
						if (!MoveReader((SQLiteDataReader)reader, requestUri))
							goto NoResult;

						// check proxy for download process
						if (environment.WebProxy.TryGet(requestUri, out task))
							return true;

						// check content type
						var contentType = reader.IsDBNull(1) ? String.Empty : reader.GetString(1);
						if (String.IsNullOrEmpty(contentType))
							goto NoResult;

						var readContentEncoding = reader.IsDBNull(2) ?
							new string[0] :
							reader.GetString(2).Split(';');

						if (readContentEncoding.Length > 0 && !String.IsNullOrEmpty(readContentEncoding[0]))
							contentType = contentType + ";charset=" + readContentEncoding[0];

						var isCompressedContent = readContentEncoding.Length > 1 && readContentEncoding[1] == "gzip"; // compression is marked on index 1
						task = PpsDummyProxyHelper.GetProxyTask(
							new PpsLocalStoreRequest(
								new Uri(environment.BaseUri, requestUri),
								requestUri,
								(MemoryStream)reader.GetStream(3), // This method returns a newly created MemoryStream object.
								reader.IsDBNull(4) ? null : GetLocalPath(reader.GetString(4)),
								contentType,
								isCompressedContent
							),
							reader.GetString(0)
						);
						return true;
					} // using reader
				} // using command
			} // try
			catch (Exception e)
			{
				environment.Traces.AppendException(e, String.Format("Failed to resolve offline item with path \"{0}\".", requestUri.ToString()));
			} // catch e

			NoResult:
			// no result
			task = null;
			return false;
		} // func TryGetOfflineCacheFile

		private async Task<Stream> UpdateOfflineDataAsync(string path, IPpsOfflineItemData item)
		{
			if (String.IsNullOrEmpty(path))
				throw new ArgumentNullException(nameof(path));
			if (item == null)
				throw new ArgumentNullException(nameof(item));

			var outputStream = item.Content;

			using (var transaction = await CreateTransactionAsync(PpsMasterDataTransactionLevel.Write))
			{
				if (String.IsNullOrEmpty(item.ContentType))
					throw new ArgumentNullException(nameof(item.ContentType));

				// update data base
				using (var command = transaction.CreateNativeCommand(
						"UPDATE [main].[OfflineCache] " +
							"SET [ContentType] = @contentType, " +
								"[ContentEncoding] = @contentEncoding, " +
								"[LocalContentSize] = @contentSize, " +
								"[LocalContentLastModification] = @lastModified, " +
								"[Content] = @content, " +
								"[LocalPath] = @LocalPath " +
							"WHERE [Path] = @path;"
						)
					)
				{

					command.AddParameter("@path", DbType.String).Value = path;
					command.AddParameter("@contentType", DbType.String).Value = item.ContentType; // split mime from rest
					command.AddParameter("@contentEncoding", DbType.String).Value = DBNull.Value;
					command.AddParameter("@contentSize", DbType.Int64).Value = item.ContentLength;
					command.AddParameter("@lastModified", DbType.Int64).Value = item.LastModification.ToFileTimeUtc();
					var parameterContent = command.AddParameter("@content", DbType.Binary);
					var parameterLocalPath = command.AddParameter("@LocalPath", DbType.String);

					if (item.ContentLength > 1 << 20) // create a link
					{
						var relativePath = Path.Combine("data", Guid.NewGuid().ToString("N"));
						var fileInfo = new FileInfo(GetLocalPath(relativePath));
						if (!fileInfo.Directory.Exists)
							fileInfo.Directory.Create();

						if (item.Content is IInternalFileCacheStream fcs)
						{
							await Task.Run(() => fcs.MoveTo(fileInfo.FullName));
							// dispose is done in moveto
						}
						else
						{
							using (var dst = fileInfo.Create())
								await item.Content.CopyToAsync(dst);
							item.Content.Dispose();
						}

						parameterContent.Value = DBNull.Value;
						parameterLocalPath.Value = relativePath;

						// switch stream
						outputStream = fileInfo.OpenRead();
					}
					else // simple data into an byte array
					{
						var contentBytes = await item.Content.ReadInArrayAsync();
						parameterContent.Value = contentBytes;
						parameterLocalPath.Value = DBNull.Value;

						item.Content.Position = 0; // move to first byte

						if (item.ContentLength > 0 && item.ContentLength != contentBytes.Length)
							throw new ArgumentOutOfRangeException("content", String.Format("Expected {0:N0} bytes, but received {1:N0} bytes.", item.ContentLength, contentBytes.Length));
					}

					var affectedRows = await command.ExecuteNonQueryExAsync();
					if (affectedRows != 1)
					{
						var exc = new Exception(String.Format("The insert of item \"{0}\" affected an unexpected number ({1}) of rows.", path, affectedRows));
						exc.UpdateExceptionWithCommandInfo(command);
						throw exc;
					}
				}

				transaction.Commit();
			} // transaction

			return outputStream;
		} // func UpdateOfflineData

		#endregion

		#region -- Write Access -------------------------------------------------------

		#region -- class PpsMasterNestedTransaction -----------------------------------

		private sealed class PpsMasterNestedTransaction : PpsMasterDataTransaction
		{
			private readonly PpsMasterThreadSafeTransaction rootTransaction;

			public PpsMasterNestedTransaction(PpsMasterThreadSafeTransaction rootTransaction)
				: base(rootTransaction.MasterData)
			{
				this.rootTransaction = rootTransaction ?? throw new ArgumentNullException(nameof(rootTransaction));

				rootTransaction.AddRef();
			} // ctor

			protected override void Dispose(bool disposing)
			{
				base.Dispose(disposing);
				rootTransaction.DecRef();
			} // proc Dispose

			public override void AddRollbackOperation(Action rollback)
			{
				if (IsDisposed)
					throw new ObjectDisposedException(nameof(PpsMasterDataTransaction));

				rootTransaction.AddRollbackOperation(rollback);
			} // proc AddRollbackOperation

			protected override void CommitCore()
				=> rootTransaction.SetCommitOnDispose();

			protected override void RollbackCore() { }

			protected override SQLiteConnection ConnectionCore => rootTransaction.SQLiteConnection;
			protected override SQLiteTransaction TransactionCore => rootTransaction.SQLiteTransaction;

			public override bool IsDisposed => rootTransaction.IsDisposed;
			public override bool IsCommited => rootTransaction.IsCommited;

			public PpsMasterThreadSafeTransaction RootTransaction => rootTransaction;
		} // class PpsMasterNestedTransaction

		#endregion

		#region -- class PpsMasterThreadSafeTransaction -------------------------------

		private abstract class PpsMasterThreadSafeTransaction : PpsMasterDataTransaction
		{
			private readonly int threadId;

			public PpsMasterThreadSafeTransaction(PpsMasterData masterData)
				: base(masterData)
			{
				this.threadId = Thread.CurrentThread.ManagedThreadId;
			} // ctor

			public void VerifyThreadAccess()
			{
				if (!CheckAccess())
					throw new InvalidOperationException();
			} // proc CheckThreadAccess

			public bool CheckAccess()
			{
				var currentId = Thread.CurrentThread.ManagedThreadId;
				return threadId == currentId;
			} // func CheckAccess

			public void AddRef()
			{
				VerifyThreadAccess();
				AddRefUnsafe();
			} // proc AddRef

			internal abstract void AddRefUnsafe();

			public void DecRef()
			{
				VerifyThreadAccess();
				DecRefUnsafe();
			} // proc DecRef

			internal abstract void DecRefUnsafe();

			public void SetCommitOnDispose()
			{
				VerifyThreadAccess();
				SetCommitOnDisposeUnsafe();
			} // proc SetCommitOnDispose

			internal abstract void SetCommitOnDisposeUnsafe();

			public sealed override void AddRollbackOperation(Action rollback)
			{
				VerifyThreadAccess();
				AddRollbackOperationUnsafe(rollback);
			} // proc AddRollbackOperation

			internal abstract void AddRollbackOperationUnsafe(Action rollback);

			public SQLiteConnection SQLiteConnection
			{
				get
				{
					VerifyThreadAccess();
					return this.ConnectionCore;
				}
			} // prop SQLiteConnection

			public SQLiteTransaction SQLiteTransaction
			{
				get
				{
					VerifyThreadAccess();
					return this.TransactionCore;
				}
			} // prop SQLiteTransaction
		} // class PpsMasterThreadSafeTransaction

		#endregion

		#region -- class PpsMasterRootTransaction -------------------------------------

		private sealed class PpsMasterRootTransaction : PpsMasterThreadSafeTransaction
		{
			private readonly SQLiteConnection connection;
			private readonly SQLiteTransaction transaction;

			private readonly List<Action> rollbackActions = new List<Action>();
			private readonly ThreadLocal<PpsMasterThreadJoinTransaction> joinedTransaction = new ThreadLocal<PpsMasterThreadJoinTransaction>(false);

			private bool? transactionState = null;
			private bool commitOnDispose = false;

			private int nestedTransactionCounter = 0;

			public PpsMasterRootTransaction(PpsMasterData masterData, SQLiteConnection connection, SQLiteTransaction transaction)
				: base(masterData)
			{
				this.connection = connection ?? throw new ArgumentNullException(nameof(connection));
				this.transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
			} // ctor

			protected override void Dispose(bool disposing)
			{
				base.Dispose(disposing);

				if (nestedTransactionCounter > 0)
					throw new InvalidOperationException("There are still nested transactions.");

				if (!transactionState.HasValue)
				{
					if (commitOnDispose)
						Commit();
					else
						Rollback();
				}

				// dispose transaction
				if (disposing)
					transaction.Dispose();

				MasterData.ClearTransaction(this);
			} // proc Dispose

			internal override void AddRollbackOperationUnsafe(Action rollback)
				=> rollbackActions.Add(rollback);

			protected override void CommitCore()
			{
				VerifyThreadAccess();

				transaction.Commit();
				transactionState = true;
			} // proc CommitCore

			protected override void RollbackCore()
			{
				VerifyThreadAccess();

				transaction.Rollback();
				transactionState = false;

				// run rollback actions
				foreach (var c in rollbackActions)
				{
					try
					{
						c();
					}
					catch { }
				}
			} // proc RollbackCore

			internal override void SetCommitOnDisposeUnsafe()
				=> commitOnDispose = true;

			internal override void AddRefUnsafe()
				=> Interlocked.Increment(ref nestedTransactionCounter);

			internal override void DecRefUnsafe()
				=> Interlocked.Decrement(ref nestedTransactionCounter);

			protected override SQLiteConnection ConnectionCore => connection;
			internal SQLiteConnection ConnectionUnsafe => connection;
			protected override SQLiteTransaction TransactionCore => transaction;
			internal SQLiteTransaction TransactionUnsafe => transaction;

			public override bool IsDisposed => transactionState.HasValue;
			public override bool IsCommited => transactionState ?? false;

			public PpsMasterThreadJoinTransaction JoinedTransaction
			{
				get { return joinedTransaction.Value; }
				set
				{
					if (value == null)
						joinedTransaction.Value = null;
					else if (joinedTransaction.Value == null)
						joinedTransaction.Value = value;
					else
						throw new InvalidOperationException();
				}
			} // prop JoinedTransaction
		} // class PpsMasterRootTransaction

		#endregion

		#region -- class PpsMasterThreadJoinTransaction -------------------------------

		private sealed class PpsMasterThreadJoinTransaction : PpsMasterThreadSafeTransaction
		{
			private readonly PpsMasterRootTransaction rootTransaction;

			public PpsMasterThreadJoinTransaction(PpsMasterRootTransaction rootTransaction)
				: base(rootTransaction.MasterData)
			{
				this.rootTransaction = rootTransaction ?? throw new ArgumentNullException(nameof(rootTransaction));
				rootTransaction.JoinedTransaction = this;
			} // ctor

			protected override void Dispose(bool disposing)
			{
				if (rootTransaction != null)
					rootTransaction.JoinedTransaction = null;
				base.Dispose(disposing);
			} // proc Dispose

			public override bool IsDisposed => rootTransaction.IsDisposed;
			public override bool IsCommited => rootTransaction.IsCommited;

			internal override void AddRefUnsafe()
				=> rootTransaction.AddRefUnsafe();

			internal override void DecRefUnsafe()
				=> rootTransaction.DecRefUnsafe();

			internal override void AddRollbackOperationUnsafe(Action rollback)
				=> rootTransaction.AddRollbackOperationUnsafe(rollback);

			internal override void SetCommitOnDisposeUnsafe()
				=> rootTransaction.SetCommitOnDisposeUnsafe();

			protected override SQLiteConnection ConnectionCore => rootTransaction.ConnectionUnsafe;
			protected override SQLiteTransaction TransactionCore => rootTransaction.TransactionUnsafe;
		} // class PpsMasterThreadJoinTransaction

		#endregion

		#region -- class PpsMasterReadTransaction -------------------------------------

		/// <summary>Dummy transaction that does nothing. Dirty read is always possible.</summary>
		private sealed class PpsMasterReadTransaction : PpsMasterDataTransaction
		{
			private readonly SQLiteConnection connection;
			private bool isDisposed = false;

			public PpsMasterReadTransaction(PpsMasterData masterData, SQLiteConnection connection)
				: base(masterData)
				=> this.connection = connection ?? throw new ArgumentNullException(nameof(connection));

			protected override void Dispose(bool disposing)
			{
				if (disposing)
				{
					if (isDisposed)
						throw new ObjectDisposedException(nameof(PpsMasterDataTransaction));
					isDisposed = true;
				}
				base.Dispose(disposing);
			} // proc Dispose

			public override void AddRollbackOperation(Action rollback) { }

			protected override SQLiteConnection ConnectionCore => connection;
			protected override SQLiteTransaction TransactionCore => null;

			public override bool IsDisposed => isDisposed;
			public override bool IsCommited => false;
		} // class PpsMasterReadTransaction

		#endregion

		private readonly ManualResetEventAsync currentTransactionLock = new ManualResetEventAsync();
		private PpsMasterRootTransaction currentTransaction;

		/// <summary>Create a simple transaction for read access only..</summary>
		/// <returns></returns>
		public PpsMasterDataTransaction CreateReadUncommitedTransaction()
			=> new PpsMasterReadTransaction(this, connection);

		/// <summary>Create a transaction with an access level.</summary>
		/// <param name="level">Level of access.</param>
		/// <returns></returns>
		public Task<PpsMasterDataTransaction> CreateTransactionAsync(PpsMasterDataTransactionLevel level)
			=> CreateTransactionAsync(level, CancellationToken.None);

		/// <summary>Create a transaction with write access.</summary>
		/// <param name="nonBlocking">Should the call block the execution.</param>
		/// <returns><c>null</c>, if no transaction was created.</returns>
		private async Task<PpsMasterDataTransaction> CreateWriteTransactionAsync(bool nonBlocking)
		{
			if (nonBlocking)
			{
				using (var cts = new CancellationTokenSource(100))
					return await CreateTransactionAsync(PpsMasterDataTransactionLevel.Write, cts.Token, null);
			}
			else
				return await CreateTransactionAsync(PpsMasterDataTransactionLevel.Write, CancellationToken.None, null);
		} // func CreateWriteTransactionAsync

		/// <summary>Create a database transaction.</summary>
		/// <param name="level">Level of access.</param>
		/// <param name="cancellationToken">Cancellation token.</param>
		/// <param name="transactionJoinTo">Allow the transaction to join with an other transaction on a different thread.</param>
		/// <returns><c>null</c>, if no transaction was created.</returns>
		public async Task<PpsMasterDataTransaction> CreateTransactionAsync(PpsMasterDataTransactionLevel level, CancellationToken cancellationToken, PpsMasterDataTransaction transactionJoinTo = null)
		{
			if (level == PpsMasterDataTransactionLevel.ReadUncommited) // we done care about any transaction
				return new PpsMasterReadTransaction(this, connection);
			else if (transactionJoinTo != null)  // join thread access
			{
				PpsMasterThreadSafeTransaction GetRootTransaction()
				{
					switch (transactionJoinTo)
					{
						case PpsMasterNestedTransaction mnt:
							return mnt.RootTransaction;
						case PpsMasterThreadSafeTransaction mrt:
							return mrt;
						default:
							throw new InvalidOperationException();
					}
				} // func GetRootTransaction

				// check for single thread sync context
				StuffThreading.VerifySynchronizationContext();

				var threadRootTransaction = GetRootTransaction();
				return threadRootTransaction.CheckAccess()
					? (PpsMasterDataTransaction)new PpsMasterNestedTransaction(threadRootTransaction)
					: (PpsMasterDataTransaction)new PpsMasterThreadJoinTransaction((PpsMasterRootTransaction)threadRootTransaction);
			}
			else // ReadCommit is thread as write for sqlite
			{
				// check for single thread sync context
				StuffThreading.VerifySynchronizationContext();

				// try get the context
				while (true)
				{
					lock (currentTransactionLock)
					{
						if (currentTransaction == null || currentTransaction.IsDisposed) // currently, no transaction
						{
							currentTransactionLock.Reset();
							currentTransaction = new PpsMasterRootTransaction(this, connection, connection.BeginTransaction());
							return currentTransaction;
						}
						else if (currentTransaction.CheckAccess()) // same thread, create a nested transaction
						{
							return new PpsMasterNestedTransaction(currentTransaction);
						}
						else if (currentTransaction.JoinedTransaction != null) // other thread, check for a joined transaction
						{
							var threadRootTransaction = currentTransaction.JoinedTransaction;
							if (threadRootTransaction.IsDisposed)
								throw new InvalidOperationException("JoinedTransaction is already disposed, but current is still alive.");

							return new PpsMasterNestedTransaction(threadRootTransaction);
						}
					}

					// different thread, block until currentTransaction is zero
					await currentTransactionLock.WaitAsync(cancellationToken);
					if (cancellationToken.IsCancellationRequested)
						return null;
				}
			}
		} // func CreateTransaction

		private void ClearTransaction(PpsMasterRootTransaction trans)
		{
			lock (currentTransactionLock)
			{
				if (Object.ReferenceEquals(currentTransaction, trans))
				{
					currentTransaction = null;
					currentTransactionLock.Set();
				}
			}
		} // proc ClearTransaction

		/// <summary>Get the current active transaction of the current thread.</summary>
		/// <returns></returns>
		public PpsMasterDataTransaction GetCurrentTransaction()
		{
			lock (currentTransactionLock)
			{
				if (currentTransaction != null && currentTransaction.CheckAccess())
					return currentTransaction;
			}
			return null;
		} // func GetCurrentTransaction

		/// <summary>Get the state of the current write transaction.</summary>
		/// <returns></returns>
		public PpsWriteTransactionState WriteTransactionState()
		{
			lock (currentTransactionLock)
			{
				if (currentTransaction == null)
					return PpsWriteTransactionState.None;
				else if (currentTransaction.CheckAccess())
					return PpsWriteTransactionState.CurrentThread;
				else
					return PpsWriteTransactionState.OtherThread;
			}
		} // func WriteTransactionState

		/// <summary>Wait for write transaction on a different thread.</summary>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public Task<bool> WaitForWriteTransactionAsync(CancellationToken cancellationToken)
		{
			lock (currentTransactionLock)
			{
				if (currentTransaction == null
					|| currentTransaction.CheckAccess())
					return Task.FromResult(true);
				else
					return currentTransactionLock.WaitAsync(cancellationToken);
			}
		} // func WaitForWriteTransactionAsync

		/// <summary>Create a native command, without any transaction handling</summary>
		/// <param name="commandText"></param>
		/// <returns></returns>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public DbCommand CreateNativeCommand(string commandText = null)
			=> new SQLiteCommand(commandText, connection, null);

		#endregion

		#region -- Change Events ------------------------------------------------------

		#region -- class DataRowChangedEventItem --------------------------------------

		private sealed class DataRowChangedEventItem
		{
			private readonly PpsDataTableDefinition table;
			private readonly object rowId;
			private readonly PpsDataTableOperationEventHandler changeEvent;

			public DataRowChangedEventItem(PpsDataTableDefinition table, object rowId, PpsDataTableOperationEventHandler changeEvent)
			{
				this.table = table;
				this.rowId = rowId;
				this.changeEvent = changeEvent;
			} // ctor

			public void Invoke(object sender, PpsDataTableOperationEventArgs e)
			{
				if (e.Table == Table)
				{
					if (rowId != null)
					{
						if (e is PpsDataRowOperationEventArgs e2 && Equals(rowId, e2.OldRowId))
							changeEvent.Invoke(sender, e);
					}
					else
						changeEvent.Invoke(sender, e);
				}
			} // proc Invoke

			/// <summary>Table that is selected</summary>
			public PpsDataTableDefinition Table => table;
			/// <summary>Row id to raise.</summary>
			public object RowId => rowId;
		} // class DataRowChangedEventItem

		#endregion

		private readonly List<WeakReference<DataRowChangedEventItem>> weakDataRowEvents = new List<WeakReference<DataRowChangedEventItem>>();

		/// <summary>Registers a listener to local database changes.</summary>
		/// <param name="tableName"></param>
		/// <param name="rowId"></param>
		/// <param name="handler"></param>
		public object RegisterWeakDataRowChanged(string tableName, long? rowId, PpsDataTableOperationEventHandler handler)
		{
			var table = FindTable(tableName, true);
			return RegisterWeakDataRowChanged(table, rowId, handler);
		} // func RegisterWeakDataRowChanged

		/// <summary></summary>
		/// <param name="table"></param>
		/// <param name="rowId"></param>
		/// <param name="handler"></param>
		/// <returns></returns>
		public object RegisterWeakDataRowChanged(PpsDataTableDefinition table, object rowId, PpsDataTableOperationEventHandler handler)
		{
			var token = new DataRowChangedEventItem(table, rowId, handler);
			weakDataRowEvents.Add(new WeakReference<DataRowChangedEventItem>(token));
			return token;
		} // func RegisterWeakDataRowChanged

		internal void OnMasterDataTableChanged(PpsDataTableOperationEventArgs args, bool raiseTableChanged)
		{
			// set object tags to dirty
			if (args.Table == ObjectTagsTable)
				isObjectTagsDirty = true;

			// raise weak event
			var i = weakDataRowEvents.Count - 1;
			while (i >= 0)
			{
				if (weakDataRowEvents[i].TryGetTarget(out var t))
					t.Invoke(this, args);
				else
					weakDataRowEvents.RemoveAt(i);
				i--;
			}

			// raise vent on environment
			environment.OnMasterDataTableChanged(args);

			if (raiseTableChanged && (args.Operation == PpsDataRowOperation.RowInsert || args.Operation == PpsDataRowOperation.RowDelete))
				OnMasterDataTableChanged(new PpsDataTableOperationEventArgs(args.Table, PpsDataRowOperation.TableChanged), false);
		} // proc OnMasterDataTableChanged

		#endregion

		/// <summary><c>true</c>, if the sync process is started.</summary>
		public bool IsSynchronizationStarted => isSynchronizationStarted;
		[Obsolete("ConnectionAccess")]
		public SQLiteConnection Connection => connection;

		/// <summary></summary>
		public PpsDataTableDefinition ObjectsTable => objectsTable.Value;
		/// <summary></summary>
		public PpsDataTableDefinition ObjectLinksTable => objectLinksTable.Value;
		/// <summary></summary>
		public PpsDataTableDefinition ObjectTagsTable => objectTagsTable.Value;

		/// <summary>Return the current transaction.</summary>
		public PpsMasterDataTransaction CurrentTransaction
		{
			get
			{
				lock (currentTransactionLock)
					return currentTransaction;
			}
		} // prop CurrentTransaction

		// -- Static ------------------------------------------------------

		private static readonly MethodInfo getTableMethodInfo;

		static PpsMasterData()
		{
			var ti = typeof(PpsMasterData);
			getTableMethodInfo = ti.GetMethod(nameof(GetTable), typeof(PpsDataTableDefinition));
		} // ctor

		#region -- Read/Write Schema --------------------------------------------------

		internal static XElement ReadSchemaValue(IDataReader r, int columnIndex)
		{
			using (var sr = new StringReader(r.GetString(columnIndex)))
				return XDocument.Load(sr).Root;
		} // func ReadSchemaValue

		#endregion

		#region -- Local store primitives ---------------------------------------------

		/// <summary>Data type mapping for sqlite.</summary>
		private static (Type Type, string SqlLite, DbType DbType)[] SqlLiteTypeMapping { get; } =
		{
			(typeof(bool), "Boolean", DbType.Boolean),
			(typeof(DateTime), "DateTime", DbType.DateTime),

			(typeof(sbyte), "Int8", DbType.SByte),
			(typeof(short), "Int16", DbType.Int16),
			(typeof(int), "Int32", DbType.Int32),
			(typeof(long), "Int64", DbType.Int64),
			(typeof(byte), "UInt8", DbType.Byte),
			(typeof(ushort), "UInt16", DbType.UInt16),
			(typeof(uint), "UInt32", DbType.UInt32),
			(typeof(ulong), "UInt64", DbType.UInt64),

			(typeof(float), "Float", DbType.Single),
			(typeof(double), "Double", DbType.Double),
			(typeof(decimal), "Decimal", DbType.Decimal),

			(typeof(string), "Text", DbType.String),
			(typeof(Guid), "Guid", DbType.Guid),
			(typeof(byte[]), "Blob", DbType.Binary),
			// alt
			(typeof(long), "integer", DbType.Int64),
			(typeof(PpsObjectExtendedValue), "Integer", DbType.Int64),
			(typeof(PpsMasterDataExtendedValue), "Integer", DbType.Int64),
			(typeof(PpsFormattedStringValue), "Text", DbType.String)
		};

		private static Type ConvertSqLiteToDataType(string dataType)
			=> String.IsNullOrEmpty(dataType)
				? typeof(string)
				:
					(
						from c in SqlLiteTypeMapping
						where String.Compare(c.SqlLite, dataType, StringComparison.OrdinalIgnoreCase) == 0
						select c.Type
					).FirstOrDefault() ?? throw new ArgumentOutOfRangeException("type", $"No c# type assigned for '{dataType}'.");

		private static int FindSqlLiteTypeMappingByType(Type type)
			=> Array.FindIndex(SqlLiteTypeMapping, c => c.Type == type);

		private static bool IsIntegerType(Type t)
		{
			switch (Type.GetTypeCode(t))
			{
				case TypeCode.Int32:
				case TypeCode.UInt32:
				case TypeCode.Int64:
				case TypeCode.UInt64:
					return true;
				default:
					return false;
			}
		} // func IsIntegerType

		private static string ConvertDataTypeToSqLite(IDataColumn column)
		{
			if (column.Attributes.GetProperty("IsIdentity", false) && IsIntegerType(column.DataType))
				return "Integer";
			else
			{
				var index = FindSqlLiteTypeMappingByType(column.DataType);
				return index >= 0 ? SqlLiteTypeMapping[index].SqlLite : throw new ArgumentOutOfRangeException("type", $"No sqlite type assigned for '{column.DataType.Name}'.");
			}
		} // func ConvertDataTypeToSqLite

		private static DbType ConvertDataTypeToDbType(Type type)
		{
			var index = FindSqlLiteTypeMappingByType(type);
			return index >= 0 ? SqlLiteTypeMapping[index].DbType : throw new ArgumentOutOfRangeException("type", $"No DbType type assigned for '{type.Name}'.");
		} // func ConvertDataTypeToDbType

		private static object ConvertStringToSQLiteValue(string value, DbType type)
		{
			var index = Array.FindIndex(SqlLiteTypeMapping, c => c.DbType == type);
			return index >= 0
				? value == null ? (object)DBNull.Value : (object)Procs.ChangeType(value, SqlLiteTypeMapping[index].Type)
				: throw new ArgumentOutOfRangeException(nameof(type), type, $"DB-Type {type} is not supported.");
		} // func ConvertStringToSQLiteValue

		internal static bool CheckLocalTableExists(SQLiteConnection connection, string tableName)
		{
			using (var command = new SQLiteCommand("SELECT [tbl_name] FROM [sqlite_master] WHERE [type] = 'table' AND [tbl_name] = @tableName;", connection))
			{
				command.Parameters.Add("@tableName", DbType.String, tableName.Length + 1).Value = tableName;
				using (var r = command.ExecuteReaderEx(CommandBehavior.SingleRow))
					return r.Read();
			}
		} // func CheckLocalTableExistsAsync

		internal static IEnumerable<IDataColumn> GetLocalTableColumns(SQLiteConnection connection, string tableName)
		{
			using (var command = new SQLiteCommand($"PRAGMA table_info({tableName});", connection))
			{
				using (var r = command.ExecuteReaderEx(CommandBehavior.SingleResult))
				{
					while (r.Read())
					{
						yield return new SimpleDataColumn(
							r.GetString(1),
							r.IsDBNull(2) ? typeof(string) : ConvertSqLiteToDataType(r.GetString(2)),
							new PropertyDictionary(
								new PropertyValue(nameof(PpsDataColumnMetaData.Nullable), r.IsDBNull(3) || !r.GetBoolean(3)),
								new PropertyValue(nameof(PpsDataColumnMetaData.Default), r.GetValue(4)?.ToString()),
								new PropertyValue("SQLiteType", r.GetString(2)),
								new PropertyValue("IsPrimary", !r.IsDBNull(5) && r.GetBoolean(5))
							)
						);
					}
				}
			}
		} // func GetLocalTableColumns

		internal static IEnumerable<Tuple<string, bool>> GetLocalTableIndexes(SQLiteConnection connection, string tableName)
		{
			using (var command = new SQLiteCommand($"PRAGMA index_list({tableName});", connection))
			{
				using (var r = command.ExecuteReaderEx(CommandBehavior.SingleResult))
				{
					const int indexName = 1;
					const int indexIsUnique = 2;

					while (r.Read())
					{
						var indexNameValue = r.GetString(indexName);
						if (!indexNameValue.StartsWith("sqlite_autoindex_", StringComparison.OrdinalIgnoreCase)) // hide system index
						{
							yield return new Tuple<string, bool>(
							  indexNameValue,
							  r.GetBoolean(indexIsUnique)
						  );
						}
					}
				}
			}
		} // func GetLocalTableIndexes

		internal static bool TestTableColumns(SQLiteConnection connection, string tableName, params SimpleDataColumn[] columns)
		{
			var foundColumnCount = 0;

			foreach (var expectedColumn in GetLocalTableColumns(connection, tableName))
			{
				var testColumn = columns.FirstOrDefault(c => String.Compare(expectedColumn.Name, c.Name, StringComparison.OrdinalIgnoreCase) == 0);
				if (testColumn != null)
				{
					if (testColumn.DataType != expectedColumn.DataType)
						return false;
					foundColumnCount++;
				}
			}

			return foundColumnCount == columns.Length;
		} // func TestLocalTableColumns

		#endregion
	} // class PpsMasterData

	#endregion

	#region -- enum PpsLoadState ------------------------------------------------------

	/// <summary>State of the pps upload task.</summary>
	public enum PpsLoadState
	{
		/// <summary>Task is queued</summary>
		Pending,
		/// <summary>Task is currently loading</summary>
		Started,
		/// <summary>Task is fully loaded</summary>
		Finished,
		/// <summary>Loading of the task was cancelled</summary>
		Canceled,
		/// <summary>Loading of the task failed</summary>
		Failed
	} // enum PpsWebLoadState

	#endregion

	#region -- interface IPpsOfflineItemData ------------------------------------------

	/// <summary>For internal use, to give access to the offline data.</summary>
	[EditorBrowsable(EditorBrowsableState.Advanced)]
	public interface IPpsOfflineItemData : IPropertyReadOnlyDictionary
	{
		/// <summary>Access to the content</summary>
		Stream Content { get; }
		/// <summary>Content type</summary>
		string ContentType { get; }
		/// <summary>Expected content length</summary>
		long ContentLength { get; }
		/// <summary>Last modification time stamp.</summary>
		DateTime LastModification { get; }
	} // interface IPpsOfflineItemData

	#endregion

	#region -- interface IPpsProxyTask ------------------------------------------------

	/// <summary>Access to a queued proxy request.</summary>
	public interface IPpsProxyTask : INotifyPropertyChanged
	{
		/// <summary>Append a function to change the default response behaviour.</summary>
		/// <param name="response">Request response.</param>
		void AppendResponseSink(Action<WebResponse> response);

		/// <summary>Processes the request in the forground (change priority to first).</summary>
		/// <returns></returns>
		Task<WebResponse> ForegroundAsync();
		/// <summary>Task to watch the download process.</summary>
		Task<WebResponse> Task { get; }

		/// <summary>State of the download progress</summary>
		PpsLoadState State { get; }

		/// <summary>Download state of the in percent.</summary>
		int Progress { get; }
		/// <summary>Displayname that will be shown in the ui.</summary>
		string DisplayName { get; }
	} // interface IPpsProxyTask

	#endregion

	#region -- class PpsDummyProxyHelper ----------------------------------------------

	internal static class PpsDummyProxyHelper
	{
		#region -- class PpsDummyProxyTask ----------------------------------------------

		internal sealed class PpsDummyProxyTask : IPpsProxyTask
		{
			event PropertyChangedEventHandler INotifyPropertyChanged.PropertyChanged { add { } remove { } }

			private readonly WebRequest request;
			private readonly string displayName;
			private bool responseCalled;

			public PpsDummyProxyTask(WebRequest request, string displayName)
			{
				this.request = request;
				this.displayName = displayName ?? request.RequestUri.PathAndQuery;
			} // ctor

			private WebRequest InitResponse()
			{
				if (responseCalled)
					throw new InvalidOperationException();

				responseCalled = true;
				return request;
			} // proc InitResponse

			public void AppendResponseSink(Action<WebResponse> response)
				=> response(InitResponse().GetResponse());

			public Task<WebResponse> ForegroundAsync()
				=> InitResponse().GetResponseAsync();

			public void SetUpdateOfflineCache(Func<IPpsOfflineItemData, Stream> updateOfflineCache)
				=> throw new NotSupportedException();

			public Task<WebResponse> Task => InitResponse().GetResponseAsync();

			public PpsLoadState State => PpsLoadState.Started;
			public int Progress => -1;
			public string DisplayName => displayName;
		} // class PpsDummyProxyTask

		#endregion

		/// <summary>Wrap a webrequest to an proxy task.</summary>
		/// <param name="request"></param>
		/// <param name="displayName"></param>
		/// <param name="priority"></param>
		/// <returns></returns>
		public static IPpsProxyTask GetProxyTask(this WebRequest request, string displayName, PpsLoadPriority priority = PpsLoadPriority.Default)
		   => request is PpsProxyRequest p
			   ? p.Enqueue(priority)
			   : new PpsDummyProxyTask(request, displayName);
	} // class PpsDummyProxyHelper

	#endregion

	#region -- class PpsProxyRequest --------------------------------------------------

	/// <summary>Proxy request to implementation, that is able to switch between offline 
	/// cache and online mode.</summary>
	public sealed class PpsProxyRequest : WebRequest, IEquatable<PpsProxyRequest>
	{
		private readonly PpsEnvironment environment; // owner, that retrieves a resource
		private readonly string displayName;
		private readonly Uri originalUri;
		private readonly Uri relativeUri; // relative Uri

		private readonly bool offlineOnly;
		private bool aborted = false; // is the request cancelled

		private readonly Func<WebResponse> procGetResponse; // async GetResponse
		private readonly Func<Stream> procGetRequestStream; // async

		private WebHeaderCollection headers;
		private readonly string path;
		private readonly NameValueCollection arguments;

		private string method = HttpMethod.Get.Method;
		private string contentType = null;
		private long contentLength = -1;

		private Func<IPpsOfflineItemData, Stream> updateOfflineCache = null;
		private MemoryStream requestStream = null;
		private HttpWebRequest onlineRequest = null;

		#region -- Ctor/Dtor ----------------------------------------------------------

		internal PpsProxyRequest(PpsEnvironment environment, string displayName, Uri originalUri, Uri relativeUri, bool offlineOnly)
		{
			this.environment = environment ?? throw new ArgumentNullException(nameof(environment));
			this.displayName = displayName ?? relativeUri.ToString();
			this.originalUri = originalUri ?? throw new ArgumentNullException(nameof(originalUri));
			this.relativeUri = relativeUri ?? throw new ArgumentNullException(nameof(relativeUri));
			this.offlineOnly = offlineOnly;

			if (relativeUri.IsAbsoluteUri)
				throw new ArgumentException("Uri must be relative.", nameof(relativeUri));
			if (!originalUri.IsAbsoluteUri)
				throw new ArgumentException("Uri must be absolute.", nameof(originalUri));

			this.procGetResponse = GetResponse;
			this.procGetRequestStream = GetRequestStream;

			(path, arguments) = relativeUri.ParseUri();
		} // ctor

		/// <summary>Returns whether the given proxy request is for the same object</summary>
		/// <param name="other">Request to compare</param>
		/// <returns>true if equal</returns>
		public bool Equals(PpsProxyRequest other)
			=> Equals(other.relativeUri);

		/// <summary>Returns whether the Uri is equal to the given Uri</summary>
		/// <param name="otherUri">Uri to compare</param>
		/// <returns>true if equal</returns>
		public bool Equals(Uri otherUri)
			=> WebRequestHelper.EqualUri(relativeUri, otherUri);

		#endregion

		#region -- GetResponse --------------------------------------------------------

		/// <summary>Handles the request async</summary>
		/// <param name="callback"></param>
		/// <param name="state"></param>
		/// <returns></returns>
		public override IAsyncResult BeginGetResponse(AsyncCallback callback, object state)
		{
			if (aborted)
				throw new WebException("Canceled", WebExceptionStatus.RequestCanceled);

			return procGetResponse.BeginInvoke(callback, state);
		} // func BeginGetResponse

		/// <summary></summary>
		/// <param name="asyncResult"></param>
		/// <returns></returns>
		public override WebResponse EndGetResponse(IAsyncResult asyncResult)
			=> procGetResponse.EndInvoke(asyncResult);

		/// <summary>Get the response and process the request now.</summary>
		/// <returns></returns>
		public override WebResponse GetResponse()
		{
			if (UseOnlineRequest) // we have request data, execute always online
				return InternalGetResponse();
			else if (environment.TryGetOfflineObject(this, out var task)) // check if the object is local available, cached
				return task.ForegroundAsync().AwaitTask(); // block thread
			else
				return InternalGetResponse();
		} // func GetResponse

		/// <summary>Get the response and process the request now.</summary>
		/// <returns></returns>
		public override Task<WebResponse> GetResponseAsync()
		{
			if (UseOnlineRequest) // we have request data, execute always online
				return InternalGetResponseAsync();
			else if (environment.TryGetOfflineObject(this, out var task)) // check if the object is local available, cached
				return task.ForegroundAsync();
			else
				return InternalGetResponseAsync();
		} // func GetResponse

		/// <summary>Puts the request of an item on the queue</summary>
		/// <param name="priority">Importance of the item.</param>
		/// <param name="forceOnline">If true, the object is requested only from the server, not from the cache. Defaults to false.</param>
		/// <returns></returns>
		public IPpsProxyTask Enqueue(PpsLoadPriority priority, bool forceOnline = false)
		{
			// check for offline item
			if (!forceOnline && updateOfflineCache == null && environment.TryGetOfflineObject(this, out var task1))
				return task1;
			else if (!UseOnlineRequest && updateOfflineCache == null && environment.WebProxy.TryGet(this, out var task2)) // check for already existing task
				return task2;
			else // enqueue the new task
				return environment.WebProxy.Append(this, priority);
		} // func Enqueue

		private void CreateOnlineRequest()
		{
			if (onlineRequest != null)
				throw new InvalidOperationException("Request always created.");

			// create new online request
			onlineRequest = environment.CreateOnlineRequest(relativeUri);

			// copy basic request informationen
			onlineRequest.Method = method;
			if (contentLength > 0)
				onlineRequest.ContentLength = contentLength;
			if (contentType != null)
				onlineRequest.ContentType = contentType;

			// copy headers
			if (headers != null)
			{
				headers["ppsn-hostname"] = System.Environment.MachineName;
				foreach (var k in headers.AllKeys)
					onlineRequest.Headers[k] = headers[k];
			}

			// request data, cached POST-Data
			if (requestStream != null)
			{
				using (var dst = onlineRequest.GetRequestStream())
				{
					requestStream.Position = 0;
					requestStream.CopyTo(dst);
				}
			}
		} // func CreateOnlineRequest

		internal WebResponse InternalGetResponse()
		{
			if (onlineRequest == null)
				CreateOnlineRequest();
			return onlineRequest.GetResponse();
		} // func InternalGetResponse

		private Task<WebResponse> InternalGetResponseAsync()
		{
			if (onlineRequest == null)
				CreateOnlineRequest();
			return onlineRequest.GetResponseAsync();
		} // func InternalGetResponseAsync

		private bool UseOnlineRequest
			=> requestStream != null || onlineRequest != null;

		#endregion

		#region -- GetRequestStream ---------------------------------------------------

		/// <summary></summary>
		/// <param name="callback"></param>
		/// <param name="state"></param>
		/// <returns></returns>
		public override IAsyncResult BeginGetRequestStream(AsyncCallback callback, object state)
			=> procGetRequestStream.BeginInvoke(callback, state);

		/// <summary></summary>
		/// <param name="asyncResult"></param>
		/// <returns></returns>
		public override Stream EndGetRequestStream(IAsyncResult asyncResult)
			=> procGetRequestStream.EndInvoke(asyncResult);

		/// <summary>Create the request online, and now to support request streams.</summary>
		/// <returns></returns>
		public override Stream GetRequestStream()
			=> GetRequestStream(false);

		/// <summary>Create the request online, and now to support request streams.</summary>
		/// <param name="sendChunked"><c>true</c>, for large data, the request is executed within the GetRequestStream and the inner request stream returned.</param>
		/// <returns></returns>
		public Stream GetRequestStream(bool sendChunked)
		{
			if (offlineOnly)
				throw new ArgumentException("Request data is not allowed in offline mode.");
			if (onlineRequest != null || requestStream != null)
				throw new InvalidOperationException("GetResponse or GetRequestStream is already invoked.");

			if (sendChunked)
			{
				CreateOnlineRequest();
				if (onlineRequest.Method != HttpMethod.Post.Method
					&& onlineRequest.Method != HttpMethod.Put.Method)
					throw new ArgumentException("Only POST/PUT can use GetRequestStream in none buffering mode.");

				// stream the PUT/POST
				onlineRequest.SendChunked = true;
				onlineRequest.AllowWriteStreamBuffering = false;

				return onlineRequest.GetRequestStream();
			}
			else
			{
				if (requestStream == null)
					requestStream = new MemoryStream();

				// return a window stream with open end, that the memory stream is not closed.
				return new WindowStream(requestStream, 0, -1, true, true);
			}
		} // func GetRequestStream

		#endregion

		/// <summary>Cancel the current request</summary>
		public override void Abort()
		{
			aborted = true;
			throw new NotImplementedException("todo:");
		} // proc Abort

		/// <summary>Internal use, method to update offline cache.</summary>
		/// <param name="updateOfflineCache"></param>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public void SetUpdateOfflineCache(Func<IPpsOfflineItemData, Stream> updateOfflineCache)
		{
			this.updateOfflineCache = updateOfflineCache ?? throw new ArgumentNullException(nameof(updateOfflineCache));
		} // proc SetUpdateOfflineCache

		internal Stream UpdateOfflineCache(IPpsOfflineItemData data)
			=> updateOfflineCache?.Invoke(data) ?? data.Content;

		/// <summary>Description for the ui.</summary>
		public string DisplayName => displayName;
		/// <summary>Request method</summary>
		public override string Method { get => method; set => method = value; }
		/// <summary>Content type of the request (mime type).</summary>
		public override string ContentType { get => contentType; set => contentType = value; }
		/// <summary>Content length, to send.</summary>
		public override long ContentLength { get => contentLength; set => contentLength = value; }
		
		/// <summary>Environment access.</summary>
		public PpsEnvironment Environment => environment;

		/// <summary>Request uri.</summary>
		public override Uri RequestUri => originalUri;

		/// <summary>We do not use any proxy.</summary>
		public override IWebProxy Proxy { get => null; set { } } // avoid NotImplementedExceptions

		/// <summary>Arguments of the request</summary>
		public NameValueCollection Arguments => arguments;
		/// <summary>Relative path for the request.</summary>
		public string Path => path;

		/// <summary>Header</summary>
		public override WebHeaderCollection Headers { get => headers ?? (headers = new WebHeaderCollection()); set => headers = value; }
	} // class PpsProxyRequest

	#endregion

	#region -- class PpsWebProxy ------------------------------------------------------

	/// <summary>Internal proxy to queue download and upload request.</summary>
	public sealed class PpsWebProxy : IEnumerable<IPpsProxyTask>, INotifyCollectionChanged, IDisposable
	{
		#region -- class MemoryCacheStream --------------------------------------------

		private sealed class MemoryCacheStream : Stream
		{
			private readonly MemoryStream nestedMemoryStream;

			public MemoryCacheStream(long expectedLength)
			{
				ExpectedLength = expectedLength;
				nestedMemoryStream = new MemoryStream(unchecked((int)(expectedLength > 0 ? expectedLength : 4096)));
			} // ctor

			public override void Flush()
				=> nestedMemoryStream.Flush();

			public override void SetLength(long value)
			{
				if (value != Length)
					throw new NotSupportedException();
			} // proc SetLength

			public override long Seek(long offset, SeekOrigin origin)
				=> nestedMemoryStream.Seek(offset, origin);

			public override int Read(byte[] buffer, int offset, int count)
				=> nestedMemoryStream.Read(buffer, offset, count);

			public override void Write(byte[] buffer, int offset, int count)
				=> nestedMemoryStream.Write(buffer, offset, count);

			public override bool CanRead => true;
			public override bool CanWrite => true;
			public override bool CanSeek => true;

			public override long Position { get => nestedMemoryStream.Position; set => nestedMemoryStream.Position = value; }
			public override long Length => nestedMemoryStream.Length;

			public long ExpectedLength { get; }
		} // class MemoryCacheStream

		#endregion

		#region -- class FileCacheStream ----------------------------------------------

		private sealed class FileCacheStream : Stream, IInternalFileCacheStream
		{
			private readonly string fileName;
			private readonly long expectedLength;
			private readonly FileStream nestedFileStream;

			private long currentLength = 0L;

			public FileCacheStream(long expectedLength)
			{
				fileName = Path.GetTempFileName();
				this.expectedLength = expectedLength;
				nestedFileStream = new FileStream(fileName, FileMode.Create);

				if (expectedLength > 0)
					nestedFileStream.SetLength(expectedLength);
			} // ctor

			public FileCacheStream(MemoryCacheStream copyFrom, long expectedLength)
				: this(expectedLength)
			{
				// copy stream
				copyFrom.Position = 0;
				copyFrom.CopyTo(this);
			} // ctor

			protected override void Dispose(bool disposing)
			{
				if (File.Exists(fileName))
				{
					try { File.Delete(fileName); }
					catch { }
				}
				base.Dispose(disposing);
			} // proc Dispose

			public void MoveTo(string targetFileName)
			{
				nestedFileStream.Dispose(); // close stream

				File.Move(fileName, targetFileName);
			} // proc MoveTo

			public override void Flush()
				=> nestedFileStream.Flush();

			public override int Read(byte[] buffer, int offset, int count)
				=> nestedFileStream.Read(buffer, offset, count);

			public override void Write(byte[] buffer, int offset, int count)
			{
				var appendOperation = nestedFileStream.Position == currentLength;
				nestedFileStream.Write(buffer, offset, count);

				if (appendOperation)
					currentLength += count;
			} // proc Write

			public override long Seek(long offset, SeekOrigin origin)
				=> nestedFileStream.Seek(offset, origin);

			public override void SetLength(long value)
			{
				if (value != currentLength)
					throw new NotSupportedException();
			} // proc SetLength

			public override bool CanRead => true;
			public override bool CanSeek => true;
			public override bool CanWrite => true;

			public override long Length => currentLength;

			public override long Position { get => nestedFileStream.Position; set => nestedFileStream.Position = value; }
		} // class FileCacheStream

		#endregion

		#region -- class CacheResponseStream ------------------------------------------

		private sealed class CacheResponseStream : Stream
		{
			private readonly Stream resultStream;
			private long position = 0L;

			public CacheResponseStream(Stream resultStream)
			{
				this.resultStream = resultStream;
			} // ctor

			private void EnsurePosition()
			{
				if (resultStream.Position != position)
					resultStream.Position = position;
			}

			public override void Flush() { }

			public override int Read(byte[] buffer, int offset, int count)
			{
				lock (resultStream)
				{
					EnsurePosition();
					var readed = resultStream.Read(buffer, offset, count);
					position += readed;
					return readed;
				}
			} // func Read

			public override long Seek(long offset, SeekOrigin origin)
			{
				long getNewPosition()
				{
					switch (origin)
					{
						case SeekOrigin.Begin:
							return offset;
						case SeekOrigin.Current:
							return position + offset;
						case SeekOrigin.End:
							return Length - position;
						default:
							throw new ArgumentOutOfRangeException(nameof(origin));
					}
				}

				var newPosition = getNewPosition();
				if (newPosition < 0 || newPosition > Length)
					throw new ArgumentOutOfRangeException(nameof(offset));

				return position = newPosition;
			} // func Seek

			public override void SetLength(long value)
				=> throw new NotSupportedException();

			public override void Write(byte[] buffer, int offset, int count)
				=> throw new NotSupportedException();


			public override bool CanRead => true;
			public override bool CanSeek => true;
			public override bool CanWrite => false;


			public override long Position { get => position; set => Seek(value, SeekOrigin.Begin); }
			public override long Length => resultStream.Length;
		} // class CacheResponseStream

		#endregion

		#region -- class CacheResponseProxy -------------------------------------------

		private sealed class CacheResponseProxy : WebResponse
		{
			private readonly Uri responseUri;
			private readonly Stream resultStream;
			private readonly string contentType;
			private readonly WebHeaderCollection headers;

			public CacheResponseProxy(Uri responseUri, Stream resultStream, string contentType, WebHeaderCollection headers)
			{
				this.responseUri = responseUri;
				this.resultStream = resultStream ?? throw new ArgumentNullException(nameof(headers));
				this.contentType = contentType ?? throw new ArgumentNullException(nameof(headers));
				this.headers = headers ?? throw new ArgumentNullException(nameof(headers));

				if (!resultStream.CanSeek)
					throw new ArgumentException("resultStream is not seekable", nameof(resultStream));
				if (!resultStream.CanRead)
					throw new ArgumentException("resultStream is not readable", nameof(resultStream));
			} // ctor

			public override Stream GetResponseStream()
				=> new CacheResponseStream(resultStream); // create a new stream

			public override WebHeaderCollection Headers => headers;

			public override long ContentLength { get => resultStream.Length; set => throw new NotSupportedException(); }
			public override string ContentType { get => contentType; set => throw new NotSupportedException(); }

			public override Uri ResponseUri => responseUri;
		} // class CacheResponseProxy

		#endregion

		#region -- class WebLoadRequest -----------------------------------------------

		private sealed class WebLoadRequest : IPpsProxyTask
		{
			#region -- class PpsOfflineItemDataImplementation ---------------------------

			private sealed class PpsOfflineItemDataImplementation : IPpsOfflineItemData
			{
				private readonly Stream data;
				private readonly string contentType;
				private readonly WebHeaderCollection headers;

				public PpsOfflineItemDataImplementation(Stream data, string contentType, WebHeaderCollection headers)
				{
					this.data = data;
					this.contentType = contentType;
					this.headers = headers;
				} // ctor

				public bool TryGetProperty(string name, out object value)
					=> (value = headers.Get(name)) != null;

				public Stream Content => data;
				public string ContentType => contentType;
				public long ContentLength => data.Length;
				public DateTime LastModification => headers.GetLastModified();
			} // class PpsOfflineItemDataImplementation

			#endregion

			private const long tempFileBorder = 10 << 20;

			public event PropertyChangedEventHandler PropertyChanged;

			private readonly PpsWebProxy manager;
			private readonly PpsLoadPriority priority;
			private readonly PpsProxyRequest request;

			private readonly List<Action<WebResponse>> webResponseSinks = new List<Action<WebResponse>>();
			private readonly TaskCompletionSource<WebResponse> task;

			private readonly object stateLock = new object();
			private PpsLoadState currentState = PpsLoadState.Pending;
			private int progress = -1;

			private CacheResponseProxy resultResponse = null;
			private Exception resultException = null;

			public WebLoadRequest(PpsWebProxy manager, PpsLoadPriority priority, PpsProxyRequest request)
			{
				this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
				this.priority = priority;
				this.request = request;

				task = new TaskCompletionSource<WebResponse>();
			} // ctor

			public bool IsSameRequest(PpsProxyRequest request)
				=> this.request.Equals(request);

			public bool IsSameRequest(Uri requestUri)
				=> this.request.Equals(requestUri);

			public void AppendResponseSink(Action<WebResponse> response)
			{
				lock (stateLock)
				{
					if (State == PpsLoadState.Finished)
						response(resultResponse);
					else if (State == PpsLoadState.Canceled)
						throw new OperationCanceledException("Response aborted.");
					else if (State == PpsLoadState.Failed)
						throw new Exception("Repsonse failed.", resultException);
					else if (webResponseSinks.IndexOf(response) == -1)
						webResponseSinks.Add(response);
				}
			} // proc AppendResponseSink

			public Task<WebResponse> ForegroundAsync()
			{
				lock (stateLock)
				{
					if (currentState == PpsLoadState.Pending)
						manager.MoveToForeground(this);
				}
				return Task;
			} // func ForegroundAsync

			private Stream CreateCacheStream(long contentLength)
				=> contentLength > tempFileBorder // create a temp file
					? (Stream)new FileCacheStream(contentLength)
					: new MemoryCacheStream(contentLength);

			internal void Execute()
			{
				lock (stateLock)
					UpdateState(PpsLoadState.Started);
				try
				{
					// is the request data
					using (var response = request.InternalGetResponse())
					{
						// cache the header information
						var contentLength = response.ContentLength;
						var contentType = response.ContentType;
						var headers = new WebHeaderCollection();
						foreach (var k in response.Headers.AllKeys)
							headers.Set(k, response.Headers[k]);

						// start the download
						var checkForSwitchToFile = false;
						var dst = CreateCacheStream(contentLength);
						using (var src = response.GetResponseStream())
						{
							try
							{
								var copyBuffer = new byte[4096];
								var readedTotal = 0L;
								checkForSwitchToFile = dst is MemoryCacheStream;

								while (true)
								{
									var readed = src.Read(copyBuffer, 0, copyBuffer.Length);

									UpdateProgress(unchecked((int)(readed * 1000 / contentLength)));
									if (readed > 0)
									{
										dst.Write(copyBuffer, 0, readed);
										readedTotal += readed;
										if (contentLength > readedTotal)
											UpdateProgress(unchecked((int)(readedTotal * 1000 / contentLength)));
										else if (checkForSwitchToFile && readedTotal > tempFileBorder)
										{
											if (dst is MemoryCacheStream oldDst)
											{
												dst = new FileCacheStream(oldDst, oldDst.ExpectedLength);
												oldDst.Dispose();
											}
										}
									}
									else
										break;
								}

								// process finished
								UpdateState(PpsLoadState.Finished);
								dst.Flush();

								// the cache stream will be disposed by the garbage collector, or if it is moved to the offline cache
								request.UpdateOfflineCache(new PpsOfflineItemDataImplementation(dst, contentType, headers));

								// spawn the result functions
								lock (stateLock)
								{
									UpdateState(PpsLoadState.Finished);
									resultResponse = new CacheResponseProxy(request.RequestUri, dst, contentType, headers);
								}
								foreach (var s in webResponseSinks)
									System.Threading.Tasks.Task.Run(() => s(resultResponse));

								// set the result
								task.SetResult(resultResponse);
							}
							catch
							{
								dst.Dispose(); // dispose because error
								throw;
							}// using src,dst
						}
					} // using response
				}
				catch (TaskCanceledException)
				{
					UpdateState(PpsLoadState.Canceled);
					task.SetCanceled();
				}
				catch (Exception e)
				{
					lock (stateLock)
					{
						UpdateState(PpsLoadState.Failed);
						resultException = e;
					}
					task.SetException(e);
				}
			} // proc Execute

			private void OnPropertyChanged(string propertyName)
				=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

			private void UpdateProgress(int newProgress)
			{
				if (progress != newProgress)
				{
					progress = newProgress;
					OnPropertyChanged(nameof(Progress));
				}
			} // proc UpdateProgress

			private void UpdateState(PpsLoadState newState)
			{
				if (currentState != newState)
				{
					currentState = newState;
					OnPropertyChanged(nameof(State));
				}
			} // proc UpdateState

			public Task<WebResponse> Task => task.Task;
			public PpsLoadState State => currentState;
			public PpsLoadPriority Priority => priority;
			public int Progress => progress;
			public string DisplayName => request.DisplayName;
		} // class WebLoadRequest

		#endregion

		/// <summary>Raised if the queue of the proxy has changed.</summary>
		public event NotifyCollectionChangedEventHandler CollectionChanged;

		private readonly PpsEnvironment environment;
		private readonly List<WebLoadRequest> downloadList = new List<WebLoadRequest>(); // current list of request
		private int currentForegroundCount = 0; // web requests, that marked as foreground tasks (MoveToForeground moves to this point)

		private readonly PpsSynchronizationContext executeLoadQueue;
		private readonly ManualResetEventAsync executeLoadIsRunning = new ManualResetEventAsync(false);
		private readonly CancellationTokenSource disposed;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary></summary>
		/// <param name="environment"></param>
		internal PpsWebProxy(PpsEnvironment environment)
		{
			this.disposed = new CancellationTokenSource();
			this.environment = environment;
			this.executeLoadQueue = new PpsSingleThreadSynchronizationContext("PpsWebProxy", disposed.Token, () => ExecuteLoadQueueAsync(disposed.Token));
		} // class PpsDownloadManager

		public void Dispose()
		{
			if (disposed.IsCancellationRequested)
				throw new ObjectDisposedException(nameof(PpsWebProxy));

			disposed.Cancel();
			executeLoadIsRunning.Set();
		} // proc Dispose

		#endregion

		private void OnCollectionChanged()
			=> CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

		/// <summary>Enumerator for the download task.</summary>
		/// <returns></returns>
		/// <remarks>It locks the current process.</remarks>
		public IEnumerator<IPpsProxyTask> GetEnumerator()
		{
			lock (downloadList)
			{
				foreach (var c in downloadList)
					yield return c;
			}
		} // func GetEnumerator

		IEnumerator IEnumerable.GetEnumerator()
			=> GetEnumerator();

		private WebLoadRequest TryDequeueTask()
		{
			lock (downloadList)
			{
				if (downloadList.Count == 0)
				{
					executeLoadIsRunning.Reset();
					return null;
				}
				else
				{
					var r = downloadList[0];
					if (currentForegroundCount == 0)
						currentForegroundCount = 1; // mark as foreground, that no other request moves before
					return r;
				}
			}
		} // proc TryDequeueTask

		private void RemoveCurrentTask()
		{
			var notifyReset = false;
			lock (downloadList)
			{
				if (currentForegroundCount > 0)
				{
					downloadList.RemoveAt(0);
					currentForegroundCount--;
					notifyReset = true;
				}
			}
			if (notifyReset)
				OnCollectionChanged();
		} // proc RemoveCurrentTask

		private async Task ExecuteLoadQueueAsync(CancellationToken cancellationToken)
		{
			await Task.Yield(); // enque the loop

			while (!cancellationToken.IsCancellationRequested)
			{
				var nextTask = TryDequeueTask();
				if (nextTask != null)
				{
					try
					{
						nextTask.Execute();
					}
					catch (Exception e)
					{
						// todo: connect lost?
						await environment.ShowExceptionAsync(ExceptionShowFlags.Background, e);
					}
					finally
					{
						RemoveCurrentTask();
					}
				}

				// wait for next item
				await executeLoadIsRunning.WaitAsync();
			}
		} // proc ExecuteLoadQueue

		internal void MoveToForeground(IPpsProxyTask task)
		{
			lock (downloadList)
			{
				var t = (WebLoadRequest)task;
				var idx = downloadList.IndexOf(t);
				if (idx >= currentForegroundCount) // check if the task is already in foreground
				{
					downloadList.RemoveAt(idx);
					downloadList.Insert(currentForegroundCount++, t);
				}
			}
			OnCollectionChanged();
		} // proc MoveToForeground

		private IPpsProxyTask AppendTask(WebLoadRequest task)
		{
			try
			{
				lock (downloadList)
				{
					// priority section, and not before the current requests
					var i = currentForegroundCount;
					while (i < downloadList.Count && downloadList[i].Priority <= task.Priority)
						i++;

					// add at pos
					downloadList.Insert(i, task);
					executeLoadIsRunning.Set();

					return task;
				}
			}
			finally
			{
				OnCollectionChanged();
			}
		} // proc AppendTask

		/// <summary>Get proxy task from the proxy request.</summary>
		/// <param name="request"></param>
		/// <param name="task"></param>
		/// <returns></returns>
		internal bool TryGet(PpsProxyRequest request, out IPpsProxyTask task)
		{
			// check, request exists
			lock (downloadList)
			{
				task = downloadList.Find(c => c.IsSameRequest(request));
				return task != null;
			}
		} // func TryGet

		/// <summary>Get a proxy task from the request uri.</summary>
		/// <param name="requestUri"></param>
		/// <param name="task"></param>
		/// <returns></returns>
		public bool TryGet(Uri requestUri, out IPpsProxyTask task)
		{
			// check, request exists
			lock (downloadList)
			{
				task = downloadList.Find(c => c.IsSameRequest(requestUri));
				return task != null;
			}
		} // func TryGet

		/// <summary>Append a new request to the download/upload list.</summary>
		/// <param name="request"></param>
		/// <param name="priority"></param>
		/// <returns></returns>
		internal IPpsProxyTask Append(PpsProxyRequest request, PpsLoadPriority priority)
			=> AppendTask(new WebLoadRequest(this, priority, request));
	} // class PpsDownloadManager

	#endregion

	public partial class PpsEnvironment
	{
		private const string temporaryTablePrefix = "old_";

		#region -- class PpsWebRequestCreate ------------------------------------------

		private class PpsWebRequestCreate : IWebRequestCreate
		{
			private readonly WeakReference<PpsEnvironment> environmentReference;

			public PpsWebRequestCreate(PpsEnvironment environment)
			{
				this.environmentReference = new WeakReference<PpsEnvironment>(environment);
			} // ctor

			public WebRequest Create(Uri uri)
			{
				if (environmentReference.TryGetTarget(out var environment))
					return environment.CreateProxyRequest(uri);
				else
					throw new ObjectDisposedException("Environment does not exists anymore.");
			}
		} // class PpsWebRequestCreate

		#endregion

		private PpsMasterData masterData;   // local datastore
		private PpsWebProxy webProxy;       // remote download/upload manager
		private readonly Uri baseUri;       // internal uri for this datastore
		private ProxyStatus statusOfProxy;  // interface for the transaction manager

		private readonly BaseWebRequest request;

		#region -- Init -----------------------------------------------------------------

		private const string simpleEncryptionKey = "ppsn_sqlite_dau_key";
		private const string validSqlitePasswordChars =
			"ABCDEFGHIJKLMNOPQRSTUVWXYZ_.0123456789" +
			"abcdefghijklmnopqrstuvwxyz_.0123456789";

		internal static string DecryptSqlitePassword(byte[] cryptedPassword)
		{
			if (cryptedPassword == null)
				return null;

			var encryptedPassword = new char[cryptedPassword.Length - 1];
			for (var i = 1; i < cryptedPassword.Length; i++)
			{
				var c = (char)(cryptedPassword[i] ^ cryptedPassword[i - 1]);
				if (validSqlitePasswordChars.IndexOf(c) == -1)
					throw new ArgumentOutOfRangeException("char", c, "Encrypted Password is invalid.");
				encryptedPassword[i - 1] = c;
			}

			return new string(encryptedPassword);
		} // func DecryptSqlitePassword

		internal static byte[] EncryptSqlitePassword(string encryptedPassword)
		{
			if (encryptedPassword == null)
				return null;

			var cryptedPassword = new byte[encryptedPassword.Length + 1];
			cryptedPassword[0] = (byte)new Random(Environment.TickCount).Next(1, 255);
			for (var i = 0; i < encryptedPassword.Length; i++)
				cryptedPassword[i + 1] = (byte)(cryptedPassword[i] ^ (byte)encryptedPassword[i]);

			return cryptedPassword;
		} // func EncryptSqlitePassword

		private static Random randomNumberGenerator = new Random();

		internal static string GenerateSqlitePassword()
		{
			var pwd = new char[simpleEncryptionKey.Length];

			for (var i = 0; i < pwd.Length; i++)
			{
				var j = randomNumberGenerator.Next(0, validSqlitePasswordChars.Length - 1);
				pwd[i] = validSqlitePasswordChars[j];
			}

			return new string(pwd);
		} // func GenerateSqlitePassword

		[LuaMember("__getpwd")]
		private string LuaGetLocalStorePassword(string fileName = null)
			=> GetLocalStorePassword(fileName ?? Path.Combine(LocalPath.FullName, "localStore.db"), false);
		
		private string GetLocalStorePassword(string databaseFile, bool canGeneratePassword)
		{
			// generate password file
			var passwordFile = new FileInfo(Path.ChangeExtension(databaseFile, ".dat"));

			// test if file exists
			if (passwordFile.Exists)
			{
				if (passwordFile.Length > 1024)
					throw new ArgumentOutOfRangeException(nameof(passwordFile), passwordFile.Length, "Password file is to big.");
				return DecryptSqlitePassword(File.ReadAllBytes(passwordFile.FullName));
			}
			else if (canGeneratePassword)
			{
				var pwd =
#if DEBUG
					(string)null;
#else
					GenerateSqlitePassword();
#endif
				if (pwd != null)
				{
					if (!passwordFile.Directory.Exists)
						passwordFile.Directory.Create();
					File.WriteAllBytes(passwordFile.FullName, EncryptSqlitePassword(pwd));
				}
				return pwd;
			}
			else
				return null;
		} // func GetLocalStorePassword

		/// <summary></summary>
		/// <returns><c>true</c>, if a valid database is present.</returns>
		private async Task<bool> InitLocalStoreAsync(IProgress<string> progress)
		{
			var isDataUseable = false;
			var isSchemaUseable = false;
			// open a new local store
			SQLiteConnection newLocalStore = null;
			PpsDataSetDefinitionDesktop newDataSet = null;
			DateTime? lastSynchronizationSchema = null;
			DateTime? lastSynchronizationStamp = null;
			try
			{
				// open the local database
				progress.Report("Lokale Datenbank öffnen...");
				var dataPath = Path.Combine(LocalPath.FullName, "localStore.db");
				var connectionString = "Data Source=" + dataPath + ";DateTimeKind=Utc";
				var pwd = GetLocalStorePassword(dataPath, !File.Exists(dataPath));
				if (pwd != null)
					connectionString += ";Password=Pps" + pwd;

				newLocalStore = new SQLiteConnection(connectionString); // foreign keys=true;

				newLocalStore.StateChange += (sender, e) =>
				{
					if (e.CurrentState == ConnectionState.Closed | e.CurrentState == ConnectionState.Broken)
						Trace.TraceError("Verbindung zur lokalen Datenbank verloren!");
				};

				await newLocalStore.OpenAsync();

				// set pragma's
				using (var cmd = newLocalStore.CreateCommand())
				{
					cmd.CommandText = "PRAGMA journal_mode=TRUNCATE"; // do not delete the transactio lock
					cmd.ExecuteNonQueryEx();
				}

				// check synchronisation table
				progress.Report("Lokale Datenbank verifizieren...");
				if (PpsMasterData.TestTableColumns(newLocalStore, "Header",
					new SimpleDataColumn("SchemaStamp", typeof(long)),
					new SimpleDataColumn("SchemaContent", typeof(byte[])),
					new SimpleDataColumn("SyncStamp", typeof(long)),
					new SimpleDataColumn("UserId", typeof(long))
					))
				{
					var commandText = new StringBuilder("SELECT ");
					var first = true;
					foreach (var col in PpsMasterData.GetLocalTableColumns(newLocalStore, "Header"))
					{
						if (first)
							first = false;
						else
							commandText.Append(", ");

						commandText.Append('[')
							.Append(col.Name)
							.Append(']');
					}

					commandText.Append(" FROM main.[Header]");

					// read sync tokens
					using (var command = new SQLiteCommand(commandText.ToString(), newLocalStore))
					{
						using (var r = command.ExecuteReaderEx(CommandBehavior.SingleRow))
						{
							var columnIndices = r.FindColumnIndices(true,
								"SchemaStamp",
								"SchemaContent",
								"SyncStamp",
								"UserId"
							);

							if (r.Read())
							{
								// check schema
								if (!r.IsDBNull(columnIndices[0]) && !r.IsDBNull(columnIndices[1]))
								{
									lastSynchronizationSchema = DateTime.FromFileTimeUtc(r.GetInt64(columnIndices[0]));
									newDataSet = new PpsDataSetDefinitionDesktop(this, PpsMasterData.MasterDataSchema, PpsMasterData.ReadSchemaValue(r, columnIndices[1]));
									newDataSet.EndInit();
									isSchemaUseable = true;
								}
								// check data and user info
								if (!r.IsDBNull(columnIndices[2]) && !r.IsDBNull(columnIndices[3]))
								{
									lastSynchronizationStamp = DateTime.FromFileTimeUtc(r.GetInt64(columnIndices[2]));
									userId = r.GetInt64(columnIndices[3]);

									for (var i = 0; i < r.FieldCount; i++)
									{
										if (!columnIndices.Contains(i)
											&& !r.IsDBNull(i))
											SetMemberValue(r.GetName(i), r.GetValue(i));
									}

									isDataUseable = true;
								}
							}
						}
					}
				}
				else
					Trace.WriteLine("[MasterData] Header table hast different schema.");

				// reset values
				if (!isSchemaUseable)
					lastSynchronizationSchema = DateTime.MinValue;
				if (!isDataUseable)
					lastSynchronizationStamp = DateTime.MinValue;
			}
			catch
			{
				newLocalStore?.Dispose();
				throw;
			}

			// close current connection
			masterData?.Dispose();

			// set new connection
			masterData = new PpsMasterData(this, newLocalStore, newDataSet, lastSynchronizationSchema.Value, lastSynchronizationStamp.Value);

			Trace.WriteLine($"[MasterData] Create with Schema: {lastSynchronizationSchema.Value}; SyncStamp: {lastSynchronizationStamp.Value}; ==> Use Schema={isSchemaUseable}, Use Data={isDataUseable}");

			return isDataUseable && isSchemaUseable;
		} // proc InitLocalStore

		private Uri InitProxy()
		{
			// register proxy for the web requests
			var baseUri = new Uri($"http://ppsn{environmentId}.local");
			WebRequest.RegisterPrefix(baseUri.ToString(), new PpsWebRequestCreate(this));
			return baseUri;
		} // func InitProxy

		#endregion

		#region -- Web Request --------------------------------------------------------

		/// <summary>Core function that gets called on a request.</summary>
		/// <param name="uri"></param>
		/// <returns></returns>
		private WebRequest CreateProxyRequest(Uri uri)
		{
			if (!uri.IsAbsoluteUri)
				throw new ArgumentException("Uri must absolute.", nameof(uri));

			const string localPrefix = "/local/";
			const string remotePrefix = "/remote/";

			var useOfflineRequest = CurrentMode == PpsEnvironmentMode.Offline;
			var useCache = true;
			var absolutePath = uri.AbsolutePath;

			// is the local data prefered
			if (absolutePath.StartsWith(localPrefix))
			{
				absolutePath = absolutePath.Substring(localPrefix.Length);
				useOfflineRequest = true;
			}
			else if (absolutePath.StartsWith(remotePrefix))
			{
				absolutePath = absolutePath.Substring(remotePrefix.Length);
				useOfflineRequest = false;
				useCache = false;
			}
			else if (absolutePath.StartsWith("/")) // if the uri starts with "/", remove it, because the info.remoteUri is our root
			{
				absolutePath = absolutePath.Substring(1);
			}

			// create a relative uri
			var relativeUri = new Uri(absolutePath + uri.GetComponents(UriComponents.Query | UriComponents.KeepDelimiter, UriFormat.UriEscaped), UriKind.Relative);

			// create the request proxy
			if (useCache || useOfflineRequest)
				return new PpsProxyRequest(this, relativeUri.ToString(), uri, relativeUri, useOfflineRequest);
			else
				return CreateOnlineRequest(relativeUri);
		} // func CreateWebRequest

		/// <summary>Is used only internal to create the real request.</summary>
		/// <param name="relativeUri"></param>
		/// <returns></returns>
		internal HttpWebRequest CreateOnlineRequest(Uri relativeUri)
		{
			if (relativeUri.IsAbsoluteUri)
				throw new ArgumentException("Uri must be relative.", nameof(relativeUri));
			if (relativeUri.OriginalString.StartsWith("/"))
				relativeUri = new Uri(relativeUri.OriginalString.Substring(1), UriKind.Relative);

			// build the remote request with absolute uri and credentials
			var absoluteUri = new Uri(info.Uri, relativeUri);
			var request = WebRequest.CreateHttp(absoluteUri);
			request.Credentials = UserCredential.Wrap(userInfo); // override the current credentials
			request.Headers.Add("des-multiple-authentifications", "true");
			request.Timeout = -1; // 600 * 1000;

			if (!absoluteUri.ToString().EndsWith("/?action=mdata"))
				Debug.Print($"WebRequest: {absoluteUri}");

			return request;
		} // func CreateOnlineRequest

		/// <summary>Get a proxy request for the request path.</summary>
		/// <param name="path"></param>
		/// <param name="displayName"></param>
		/// <returns></returns>
		public PpsProxyRequest GetProxyRequest(string path, string displayName)
			=> GetProxyRequest(new Uri(path, UriKind.Relative), displayName);

		/// <summary>Get a proxy request for the request path.</summary>
		/// <param name="uri"></param>
		/// <param name="displayName"></param>
		/// <returns></returns>
		public PpsProxyRequest GetProxyRequest(Uri uri, string displayName)
			=> new PpsProxyRequest(this, displayName, new Uri(BaseUri, uri), uri, CurrentState == PpsEnvironmentState.Offline);

		/// <summary>Loads an item from offline cache.</summary>
		/// <param name="request">Selects the item.</param>
		/// <param name="task">Out: the Task returning the item.</param>
		/// <returns>True if successfull.</returns>
		protected internal virtual bool TryGetOfflineObject(WebRequest request, out IPpsProxyTask task)
			=> masterData.TryGetOfflineCacheFile(BaseUri.MakeRelativeUri(request.RequestUri), out task);
		
		#endregion

		#region -- GetViewData ----------------------------------------------------------

		/// <summary></summary>
		/// <param name="arguments"></param>
		/// <returns></returns>
		public virtual IEnumerable<IDataRow> GetViewData(PpsShellGetList arguments)
			=> GetRemoteViewData(arguments);

		protected IEnumerable<IDataRow> GetRemoteViewData(PpsShellGetList arguments)
		{
			if (arguments.ViewId.StartsWith("local.", StringComparison.OrdinalIgnoreCase)) // it references the local db
			{
				if (arguments.ViewId == "local.objects")
					return CreateObjectFilter(arguments);
				else
				{
					var exc = new ArgumentOutOfRangeException();
					exc.Data.Add("Variable", "ViewId");
					exc.Data.Add("Value", arguments.ViewId);
					throw exc;
				}
			}
			else
				return Request.CreateViewDataReader(arguments.ToQuery("remote/"));
		} // func GetRemoteViewData

		#endregion

		/// <summary>Gets called before a synchronization run.</summary>
		protected internal virtual void OnBeforeSynchronization() { }
		/// <summary>Gets called after a synchronization run.</summary>
		protected internal virtual void OnAfterSynchronization() { }

		/// <summary>Is called when the system changes the state to online.</summary>
		/// <returns></returns>
		protected async virtual Task OnSystemOnlineAsync()
		{
			Trace.WriteLine("[Environment] System goes online.");
			masterData.CheckOfflineCache(); // start download

			await RefreshDefaultResourcesAsync();
			await RefreshTemplatesAsync();
		} // proc OnSystemOnline

		/// <summary>Is called when the system changes the state to offline.</summary>
		/// <returns></returns>
		protected async virtual Task OnSystemOfflineAsync()
		{
			Trace.WriteLine("[Environment] System goes offline.");
			await RefreshDefaultResourcesAsync();
			await RefreshTemplatesAsync();
		} // proc OnSystemOffline

		/// <summary>Is called when a table gets changed.</summary>
		/// <param name="args"></param>
		public virtual void OnMasterDataTableChanged(PpsDataTableOperationEventArgs args)
		{
			if (!masterData.IsInSynchronization && args.Table.Name == "OfflineCache")
				masterData.CheckOfflineCache();

			if (args.Table == MasterData.ObjectsTable && args is PpsDataRowOperationEventArgs re)
			{
				switch (args.Operation)
				{
					case PpsDataRowOperation.RowUpdate:
						{
							var rowId = re.RowId == null ? null : (long?)re.RowId;
							var oldRowId = (long)re.OldRowId;
							if (rowId != oldRowId)
								ReplaceObjectCacheId(rowId, oldRowId);
						}
						break;
					case PpsDataRowOperation.RowDelete:
						ReplaceObjectCacheId(null, (long)re.OldRowId);
						break;
				}
			}
		} // proc OnMasterDataTableChanged

		/// <summary>Enforce online mode and return true, if the operation was successfull.</summary>
		/// <param name="throwException"></param>
		/// <returns></returns>
		public async Task<bool> ForceOnlineAsync(bool throwException = true)
		{
			if (CurrentMode == PpsEnvironmentMode.Online)
				return true;
			else if (CurrentMode != PpsEnvironmentMode.Online)
			{
				switch (await WaitForEnvironmentMode(PpsEnvironmentMode.Online))
				{
					case PpsEnvironmentModeResult.Online:
						return true;
				}
			}
			return throwException
				? throw new PpsEnvironmentOnlineFailedException()
				: false;
		} // func ForceOnlineMode

		/// <summary></summary>
		[LuaMember]
		public BaseWebRequest Request => request;
		/// <summary>Default encodig for strings.</summary>
		public Encoding Encoding => Encoding.Default;
		/// <summary>Internal Uri of the environment.</summary>
		public Uri BaseUri => baseUri;

		public PpsWebProxy WebProxy => webProxy;
		public ProxyStatus StatusOfProxy => statusOfProxy;

		/// <summary>Connection to the local datastore</summary>
		[Obsolete("Use master data.")]
		public SQLiteConnection LocalConnection => masterData.Connection;

		/// <summary>Access to the local store for the synced data.</summary>
		[LuaMember]
		public PpsMasterData MasterData => masterData;
	} // class PpsEnvironment

	// interface Status
	public interface IStatusList : INotifyPropertyChanged
	{
		object ActualItem { get; }
		ObservableCollection<object> TopTen { get; }
	}

	public class ProxyStatus : IStatusList
	{
		private PpsWebProxy proxy;
		private ObservableCollection<object> topTen = new ObservableCollection<object>();
		private IPpsProxyTask actualItem;
		private System.Windows.Threading.Dispatcher dispatcher;

		public ProxyStatus(PpsWebProxy Proxy, System.Windows.Threading.Dispatcher Dispatcher)
		{
			this.proxy = Proxy;
			this.dispatcher = Dispatcher;
			this.proxy.CollectionChanged += WebProxyChanged;
		}

		public event PropertyChangedEventHandler PropertyChanged;

		private void OnPropertyChanged(string propertyName)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		private void WebProxyChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			dispatcher?.Invoke(() =>
			{
				topTen.Clear();
				using (var walker = proxy.GetEnumerator())
				{
					for (var i = 0; i < 10; i++)
					{
						if (walker.MoveNext())
							if (i == 0)
							{
								actualItem = walker.Current;
								OnPropertyChanged(nameof(actualItem));
							}
							else
								topTen.Insert(0, walker.Current);
						else if (i == 0)
						{
							actualItem = null;
							OnPropertyChanged(nameof(actualItem));
						}
					}
				}
			});
		}

		public object ActualItem => actualItem;
		public ObservableCollection<object> TopTen => topTen;
	}
}
