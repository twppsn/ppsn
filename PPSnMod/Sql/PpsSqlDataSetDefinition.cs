using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using TecWare.PPSn.Data;
using TecWare.PPSn.Server.Data;

namespace TecWare.PPSn.Server.Sql
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	internal sealed class PpsSqlDataSetDefinition : PpsDataSetServerDefinition
	{
		#region -- class SqlColumnBinding -------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class SqlColumnBinding
		{
			private readonly PpsSqlExDataSource.SqlColumnInfo sqlColumn;
			private readonly PpsDataColumnServerDefinition dataColumn;

			public SqlColumnBinding(PpsDataColumnServerDefinition dataColumn, PpsSqlExDataSource.SqlColumnInfo sqlColumn)
			{
				this.dataColumn = dataColumn;
				this.sqlColumn = sqlColumn;
			} // ctor

			public PpsDataColumnServerDefinition DataColumn => dataColumn;
			public PpsSqlExDataSource.SqlColumnInfo SqlColumn => sqlColumn;
		} // class SqlColumnBinding

		#endregion

		#region -- class SqlParameterBinding ----------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class SqlParameterBinding
		{
			private readonly PpsDataSetParameterServerDefinition dataParameter;
			private readonly PpsSqlExDataSource.SqlColumnInfo sqlParameterColumn;

			public SqlParameterBinding(PpsDataSetParameterServerDefinition dataParameter, PpsSqlExDataSource.SqlColumnInfo sqlParameterColumn)
			{
				this.dataParameter = dataParameter;
				this.sqlParameterColumn = sqlParameterColumn;
			} // ctor

			public PpsDataSetParameterServerDefinition DataParameter => dataParameter;
			public PpsSqlExDataSource.SqlColumnInfo SqlParameterColumn => sqlParameterColumn;
		} // class SqlParameterBinding

		#endregion

		#region -- class SqlTableBinding --------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class SqlTableBinding
		{
			private readonly PpsSqlDataTableServerDefinition dataTable;
			private readonly PpsSqlExDataSource.SqlTableInfo sqlTable;
			private readonly SqlColumnBinding[] columnBindings;
			private readonly List<SqlTableBinding> childTableBindings = new List<SqlTableBinding>();

			private readonly SqlTableBinding parent;
			private readonly PpsSqlExDataSource.SqlRelationInfo relation;

			public SqlTableBinding(PpsSqlDataTableServerDefinition dataTable, PpsSqlExDataSource.SqlTableInfo sqlTable, SqlColumnBinding[] columnBindings)
			{
				this.dataTable = dataTable;
				this.sqlTable = sqlTable;
				this.columnBindings = columnBindings;

				this.parent = null;
				this.relation = null;
			} // ctor

			public SqlTableBinding(PpsSqlDataTableServerDefinition dataTable, PpsSqlExDataSource.SqlTableInfo sqlTable, SqlColumnBinding[] columnBindings, SqlTableBinding parent, PpsSqlExDataSource.SqlRelationInfo relation)
			{
				this.dataTable = dataTable;
				this.sqlTable = sqlTable;
				this.columnBindings = columnBindings;

				this.parent = parent;
				this.relation = relation;
			} // ctor

			public void AddChild(SqlTableBinding tableBinding)
			{
				childTableBindings.Add(tableBinding);
			} // proc AddChild

			public SqlTableBinding FindTable(Predicate<SqlTableBinding> predicate)
			{
				if (predicate(this))
					return this;

				foreach (var cur in childTableBindings)
				{
					var t = cur.FindTable(predicate);
					if (t != null)
						return t;
				}

				return null;
			} // func FindSqlTable

			public SqlColumnBinding[] Columns => columnBindings;
			public IEnumerable<SqlTableBinding> ChildTables => childTableBindings;
			public PpsSqlExDataSource.SqlTableInfo SqlTable => sqlTable;
			public PpsSqlExDataSource.SqlRelationInfo SqlParentRelation => relation;
		} // class SqlTableBinding

		#endregion

		#region -- class PpsSqlDataTableServerDefinition ----------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class PpsSqlDataTableServerDefinition : PpsDataTableServerDefinition
		{
			private readonly List<SqlTableBinding> rootTableBindings = new List<SqlTableBinding>();

			private List<SqlParameterBinding> parameterBindings = new List<SqlParameterBinding>();
			private SqlColumnBinding parentRelationColumn = null;

			public PpsSqlDataTableServerDefinition(PpsDataSetServerDefinition dataset, string tableName, XElement xTable)
				: base(dataset, tableName, xTable)
			{
			} // ctor

			public PpsSqlDataTableServerDefinition GenerateSchemaBindings(PpsSqlDataSetDefinition dataset, List<PpsSqlDataTableServerDefinition> dataTableStack)
			{
				// bind to database schema
				foreach (PpsDataColumnServerDefinition col in Columns)
				{
					var nativeColumn = col.FieldDescription.NativeColumnDescription as PpsSqlExDataSource.SqlColumnInfo;
					if (nativeColumn == null)
						continue; // skip unknown column type

					var tmp = GenerateTableBinding(dataset, nativeColumn.Table, new List<PpsSqlExDataSource.SqlTableInfo>(), dataTableStack);
					if (GetTableBinding(c => c == tmp) == null)
						rootTableBindings.Add(tmp);
				}

				return this;
			} // proc GenerateSchemaBindings

			private SqlTableBinding GenerateTableBinding(PpsSqlDataSetDefinition dataset, PpsSqlExDataSource.SqlTableInfo sqlTable, List<PpsSqlExDataSource.SqlTableInfo> sqlTableStack, List<PpsSqlDataTableServerDefinition> dataTableStack)
			{
				// is the table generated?
				var tmp = GetTableBinding(c => c.SqlTable == sqlTable);
				if (tmp != null)
					return tmp;

				// check for recursion
				if (sqlTableStack.Contains(sqlTable))
					throw new InvalidOperationException("Recursion!"); // todo:
				sqlTableStack.Add(sqlTable);

				var columnBindings = new List<SqlColumnBinding>();

				// collect all columns to the current table
				var rootTableConnection = false;
				var hasParameterBdindings = false;
				foreach (PpsDataColumnServerDefinition col in Columns)
				{
					var nativeColumn = col.FieldDescription.NativeColumnDescription as PpsSqlExDataSource.SqlColumnInfo;
					if (nativeColumn != null && nativeColumn.Table == sqlTable)
					{
						var currentBinding = new SqlColumnBinding(col, nativeColumn);
						columnBindings.Add(currentBinding);

						// check for parameter columns
						if (col.RelatedParameter != null)
						{
							hasParameterBdindings = true;
							parameterBindings.Add(new SqlParameterBinding(col.RelatedParameter, currentBinding.SqlColumn));
						}

						// check for a declared relation
						if (col.ParentColumn != null)
						{
							// set update the process order of the data tables
							var parentRelationTable = (PpsSqlDataTableServerDefinition)col.ParentColumn.Table;
							if (parentRelationTable != this)
							{
								dataset.AddToProcessOrder(parentRelationTable, dataTableStack);

								// set relation to the root
								if (col.ParentType == PpsDataColumnParentRelationType.Root)
								{
									if (parentRelationColumn != null)
										throw new ArgumentException("only one way to root is allowed."); // todo:

									this.parentRelationColumn = currentBinding;
									CollectParameters(AddParameter);

									rootTableConnection = true;
								}
							}
						}
					}
				}

				// is there a way to a root table
				if (rootTableConnection || hasParameterBdindings)
				{
					return new SqlTableBinding(this, sqlTable, columnBindings.ToArray());
				}
				else // is this a left outer table
				{
					foreach (var rel in sqlTable.RelationInfo)
					{
						if (rel.ReferncedColumn.Table == sqlTable)
							continue; // ignore self relations

						var parentTableBinding = GenerateTableBinding(dataset, rel.ReferncedColumn.Table, sqlTableStack, dataTableStack);
						parentTableBinding.AddChild(new SqlTableBinding(this, sqlTable, columnBindings.ToArray(), parentTableBinding, rel));
						return parentTableBinding;
					}

					throw new ArgumentException("no parameter, no root table, no inner relation."); // todo:
				}
			} // func GenerateTableBinding

			private void AddParameter(SqlParameterBinding parameterBinding)
			{
				if (!parameterBindings.Contains(parameterBinding))
					parameterBindings.Add(parameterBinding);
			} // proc AddParameter

			private void CollectParameters(Action<SqlParameterBinding> add)
			{
				foreach (var cur in parameterBindings)
					add(cur);

				if (this.parentRelationColumn != null)
					((PpsSqlDataTableServerDefinition)this.parentRelationColumn.DataColumn.ParentColumn.Table).CollectParameters(add);
			} // proc CollectParameters

			private SqlTableBinding GetTableBinding(Predicate<SqlTableBinding> predicate)
			{
				foreach (var cur in rootTableBindings)
				{
					var t = cur.FindTable(predicate);
					if (t != null)
						return t;
				}
				return null;
			} // func GetTableBinding

			private void AppendLoadColumns(StringBuilder loadCommand, SqlTableBinding table, ref bool first)
			{
				foreach (var columnBinding in table.Columns)
				{
					if (first)
						first = false;
					else
						loadCommand.Append(", ");
					loadCommand.Append(columnBinding.SqlColumn.NativeName)
						.Append(" AS ")
						.Append('[').Append(columnBinding.DataColumn.Name).Append(']');
				}

				foreach (var cur in table.ChildTables)
					AppendLoadColumns(loadCommand, cur, ref first);
			} // func AppendLoadColumns

			private void AppendLoadParentJoin(StringBuilder loadCommand, bool first)
			{
				var nativeColumn = (PpsSqlExDataSource.SqlColumnInfo)(((PpsDataColumnServerDefinition)parentRelationColumn.DataColumn.ParentColumn).FieldDescription.NativeColumnDescription);
				loadCommand.Append("INNER JOIN ")
					.Append(nativeColumn.Table.FullName).Append(" ON (")
					.Append(parentRelationColumn.SqlColumn.NativeName)
					.Append(" = ")
					.Append(nativeColumn.NativeName)
					.Append(") ");
				

				var parentTable = ((PpsSqlDataTableServerDefinition)parentRelationColumn.DataColumn.ParentColumn.Table);
				if (parentTable.parentRelationColumn != null)
					parentTable.AppendLoadParentJoin(loadCommand, false);
			} // proc AppendLoadParentJoin

			private void AppendLoadOuterJoin(StringBuilder loadCommand, SqlTableBinding table)
			{
				foreach (var cur in table.ChildTables)
				{
					loadCommand.Append("LEFT OUTER JOIN ")
						.Append(cur.SqlParentRelation.ParentColumn.Table.FullName).Append(" ON (")
						.Append(cur.SqlParentRelation.ParentColumn.NativeName)
						.Append(" = ")
						.Append(cur.SqlParentRelation.ReferncedColumn.NativeName)
						.Append(") ");

					AppendLoadOuterJoin(loadCommand, cur);
				}
			} // proc AppendLoadOuterJoin

			public void AppenddLoadCommand(StringBuilder loadCommand)
			{
				// select column, .. from table, ... where relations, parameter
				loadCommand.Append("SELECT ");
				var first = true;
				foreach (var cur in rootTableBindings)
					AppendLoadColumns(loadCommand, cur, ref first);

				loadCommand.Append(" FROM ");

				first = true;
				foreach (var cur in rootTableBindings)
				{
					if (first)
						first = false;
					else
						loadCommand.Append(", ");

					// create inner parent joins
					if (parentRelationColumn != null && cur.SqlTable == parentRelationColumn.SqlColumn.Table)
					{
						loadCommand.Append(cur.SqlTable.FullName).Append(' ');
						AppendLoadParentJoin(loadCommand, true);
					}
					else
						loadCommand.Append(cur.SqlTable.FullName).Append(' ');

					// create left outer joins
					AppendLoadOuterJoin(loadCommand, cur);
				}

				first = true;
				foreach (var cur in parameterBindings)
				{
					if (first)
					{
						loadCommand.Append(" WHERE ");
						first = false;
					}
					else
						loadCommand.Append(" AND ");

					loadCommand.Append('(');
					if (cur.DataParameter.IsNullable)
						loadCommand.Append(cur.DataParameter.VariableName).Append(" IS null OR ");
					loadCommand.Append(cur.SqlParameterColumn.NativeName).Append(" = ").Append(cur.DataParameter.VariableName)
					.Append(')');
				}

				loadCommand.Append(';').AppendLine();
			} // proc AppenddLoadCommand
		} // class PpsSqlDataTableServerDefinition

		#endregion

		#region -- class PpsSqlDataSetServer ----------------------------------------------

		private sealed class PpsSqlDataSetServer : PpsDataSetServer, IPpsLoadableDataSet
		{
			public PpsSqlDataSetServer(PpsSqlDataSetDefinition datasetDefinition)
				: base(datasetDefinition)
			{
			} // ctor
		} // class PpsSqlDataSetServer

		#endregion

		private readonly PpsSqlExDataSource dataSource;
		private readonly List<PpsSqlDataTableServerDefinition> tableOrder = new List<PpsSqlDataTableServerDefinition>(); // order to load tables

		private string loadCommandBatch = null;

		public PpsSqlDataSetDefinition(IServiceProvider sp, PpsSqlExDataSource dataSource, string name, XElement config)
				: base(sp, name, config)
		{
			this.dataSource = dataSource;
		} // ctor

		public override PpsDataSet CreateDataSet()
		{
			return base.CreateDataSet();
		} // fu

		protected override PpsDataTableServerDefinition CreateTableDefinition(string tableName, XElement config)
			=> new PpsSqlDataTableServerDefinition(this, tableName, config);

		private void AddToProcessOrder(PpsSqlDataTableServerDefinition table, List<PpsSqlDataTableServerDefinition> tableStack)
		{
			if (table == null || tableOrder.Contains(table)) // already processed
				return;

			if (tableStack.Contains(table))
				throw new ArgumentException("Recursion!"); // todo:
			tableStack.Add(table);

			tableOrder.Add(table.GenerateSchemaBindings(this, tableStack)); // will call AddToProcessOrder
		} // proc AddToProcessOrder

		public override void EndInit()
		{
			base.EndInit(); // initialize columns

			// generate schema
			foreach (var table in TableDefinitions)
				AddToProcessOrder(table as PpsSqlDataTableServerDefinition, new List<PpsSqlDataTableServerDefinition>());

			// generate load command block
			var loadCommand = new StringBuilder();
			foreach (var table in tableOrder)
				table.AppenddLoadCommand(loadCommand);

			loadCommandBatch = loadCommand.ToString();
		} // proc EndInit
	} // class PpsSqlDataSetDefinition
}
