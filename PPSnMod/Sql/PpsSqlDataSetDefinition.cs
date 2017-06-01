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
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;
using TecWare.PPSn.Server.Data;

namespace TecWare.PPSn.Server.Sql
{
	#region -- class PpsSqlDataTableServer ----------------------------------------------

	/// <summary>Extend for load and merge.</summary>
	internal sealed class PpsSqlDataTableServer : PpsDataTable
	{
		public PpsSqlDataTableServer(PpsDataTableDefinition tableDefinition, PpsDataSet dataset)
			: base(tableDefinition, dataset)
		{
		}

		/// <summary>Load content of this table.</summary>
		/// <param name="trans"></param>
		/// <param name="args"></param>
		public void Load(PpsDataTransaction trans, LuaTable args)
		{
			var emptyRows = Count == 0;

			using (var sqlCommand = PpsSqlExDataSource.CreateSqlCommand(trans, CommandType.Text, false))
			{
				((PpsSqlDataTableServerDefinition)TableDefinition).PrepareLoad(sqlCommand, args, out var primaryKeyIndex);

				using (var r = sqlCommand.ExecuteReader(CommandBehavior.SingleResult))
				{
					while (r.Read())
					{
						if (emptyRows)
							Add(r.ToDictionary());
						else
						{
							var row = FindKey(r.GetValue(primaryKeyIndex));
							if (row == null)
								Add(r.ToDictionary());
							else
							{
								for (var i = 0; i < r.FieldCount; i++)
									row[r.GetName(i)] = r.GetValue(i);
							}
						}
					}
				}
			}
		} // proc Load

		/// <summary>Write the the data over the primary key</summary>
		/// <param name="data"></param>
		public void Merge(PpsDataTransaction trans, PpsDataTransaction data)
		{
			using (var sqlCommand = PpsSqlExDataSource.CreateSqlCommand(trans, CommandType.Text, false))
			{
				var primaryKeys = new List<PpsDataColumnServerDefinition>();
				((PpsSqlDataTableServerDefinition)TableDefinition).PrepareMerge(sqlCommand, primaryKeys);

				foreach (var row in this)
				{
					// set parameter
					foreach (SqlParameter p in sqlCommand.Parameters)
						p.Value = row[p.SourceColumn] ?? DBNull.Value;

					// execute merge
					using (var r = sqlCommand.ExecuteReader(CommandBehavior.SingleRow))
					{
						if (r.Read())
							row[r.GetName(0)] = r.GetValue(0);
					}
				}
			}
		} // proc Merge
	} // class PpsSqlDataTableServer

	#endregion

	#region -- class PpsSqlDataTableServerDefinition ----------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	internal sealed class PpsSqlDataTableServerDefinition : PpsDataTableServerDefinition
	{
		#region -- class SqlColumnBinding -----------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class SqlColumnBinding
		{
			private readonly PpsDataColumnServerDefinition dataColumn;
			private readonly PpsSqlExDataSource.SqlColumnInfo sqlColumn;

			public SqlColumnBinding(PpsDataColumnServerDefinition dataColumn, PpsSqlExDataSource.SqlColumnInfo sqlColumn)
			{
				this.dataColumn = dataColumn;
				this.sqlColumn = sqlColumn;
			} // ctor

			public PpsDataColumnServerDefinition DataColumn => dataColumn;
			public PpsSqlExDataSource.SqlColumnInfo SqlColumn => sqlColumn;
		} // class SqlColumnBinding

		#endregion

		#region -- class SqlTableBinding ------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class SqlTableBinding
		{
			private readonly PpsSqlDataTableServerDefinition dataTable; // owner table
			private readonly PpsSqlExDataSource.SqlTableInfo sqlTable; // sql-table
			private readonly SqlColumnBinding[] columnBindings; // column binding for this table


			private readonly SqlTableBinding parent; // way to root table
			private readonly PpsSqlExDataSource.SqlRelationInfo relationToParent; // relation to parent
			private readonly List<SqlTableBinding> childTableBindings = new List<SqlTableBinding>(); // children,, for left outer

			public SqlTableBinding(PpsSqlDataTableServerDefinition dataTable, PpsSqlExDataSource.SqlTableInfo sqlTable, SqlColumnBinding[] columnBindings, SqlTableBinding parent, PpsSqlExDataSource.SqlRelationInfo relationToParent)
			{
				this.dataTable = dataTable;
				this.sqlTable = sqlTable;
				this.columnBindings = columnBindings;

				this.parent = parent;
				this.relationToParent = relationToParent;
			} // ctor
			
			public void AddChild(SqlTableBinding tableBinding)
				=> childTableBindings.Add(tableBinding);

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
			} // func FindTable

			public IEnumerable<SqlTableBinding> GetChildren(bool recursive)
			{
				foreach (var c in childTableBindings)
				{
					yield return c;
					if (recursive)
					{
						foreach (var r in c.GetChildren(true))
							yield return r;
					}
				}
			} // func GetChildren

			public string Name => dataTable.Name;

			public SqlColumnBinding[] Columns => columnBindings;
			public IEnumerable<SqlTableBinding> ChildTables => childTableBindings;
			public PpsSqlDataTableServerDefinition DataTable => dataTable;
			public PpsSqlExDataSource.SqlTableInfo SqlTable => sqlTable;
			public SqlTableBinding ParentTable => parent;
			public PpsSqlExDataSource.SqlRelationInfo SqlParentRelation => relationToParent;
		} // class SqlTableBinding

		#endregion

		#region -- class SqlParameterBinding --------------------------------------------

		private sealed class SqlParameterBinding
		{
			private readonly string memberName;
			private SqlColumnBinding column;
			private SqlParameter parameter;

			public SqlParameterBinding(string memberName)
			{
				this.memberName = memberName;
			} // ctor

			public void SetDataColumn(SqlCommand command, SqlColumnBinding column, object value)
			{
				this.column = column;
				this.parameter = column.SqlColumn.CreateSqlParameter("@" + column.DataColumn.Name, value);
				command.Parameters.Add(parameter);
			} // proc SetDataColumn

			public string MemberName => memberName;
			public PpsDataColumnServerDefinition DataColumn => column.DataColumn;
			public PpsSqlExDataSource.SqlColumnInfo SqlColumn => column.SqlColumn;

			public bool IsValid => column != null && parameter != null;
		} // class SqlParameterBinding

		#endregion

		private SqlTableBinding primaryTable = null;
		private SqlColumnBinding primaryKey = null;

		#region -- Ctor/Dtor ------------------------------------------------------------

		public PpsSqlDataTableServerDefinition(PpsDataSetServerDefinition dataset, string tableName, XElement xTable)
			: base(dataset, tableName, xTable)
		{
		} // ctor

		#endregion

		#region -- EndInit, Schema binding ----------------------------------------------

		private PpsSqlExDataSource.SqlColumnInfo GetSqlColumnInfo(PpsDataColumnDefinition column)
			=> ((IPpsColumnDescription)column).GetColumnDescriptionImplementation<PpsSqlExDataSource.SqlColumnInfo>();

		private SqlTableBinding GenerateTableBinding(PpsSqlExDataSource.SqlTableInfo tableInfo, SqlTableBinding parent, PpsSqlExDataSource.SqlRelationInfo relationToParent)
		{
			if (tableInfo == null)
				throw new ArgumentNullException(nameof(tableInfo));

			var bindColumns = new List<SqlColumnBinding>();

			foreach (var col in Columns)
			{
				var sqlColumn = GetSqlColumnInfo(col);
				if (sqlColumn?.Table == tableInfo)
				{
					if (primaryKey.DataColumn == col)
						bindColumns.Add(primaryKey);
					else
						bindColumns.Add(new SqlColumnBinding((PpsDataColumnServerDefinition)col, sqlColumn));
				}
			}

			return new SqlTableBinding(this, tableInfo, bindColumns.ToArray(), parent, relationToParent);
		} // func GenerateTableBindings

		private void GenerateTableBindings(PpsSqlExDataSource.SqlTableInfo tableInfo, List<SqlTableBinding> tableStack)
		{
			if (tableStack.Exists(c => c.SqlTable == tableInfo))
				throw new ArgumentException("Recursion detected: " + String.Join(" -> ", from c in tableStack select c.DataTable.Name));

			PpsSqlExDataSource.SqlRelationInfo primaryTableRelation = null;

			foreach (var rel in tableInfo.RelationInfo)
			{
				if (rel.ReferencedColumn.Table == tableInfo)
					continue; // ignore self relations

				if (rel.ReferencedColumn.Table == primaryTable.SqlTable) // direct relation 
				{
					primaryTableRelation = rel;
					break;
				}
			}

			if (primaryTableRelation == null)
				throw new ArgumentException($"'{tableInfo.Name}' has no relation to '{primaryTable.Name}'");

			primaryTable.AddChild(GenerateTableBinding(tableInfo, primaryTable, primaryTableRelation));
		} // func GenerateTableBindings

		private SqlTableBinding GetPrimaryRootTable()
		{
			if (primaryTable != null)
				return primaryTable;

			// check if the primary column is a SqlColumn
			var sourceColumn = GetSqlColumnInfo(PrimaryKey);
			if (sourceColumn == null)
				throw new ArgumentException($"Primary key of table {Name} is not a native sql column.", PrimaryKey.Name);

			primaryKey = new SqlColumnBinding((PpsDataColumnServerDefinition)PrimaryKey, sourceColumn);
			
			// create binding to "root" (inner joins)
			SqlTableBinding parentRootTable = null;
			PpsSqlExDataSource.SqlRelationInfo parentRootTableRelation = null;
			var rootColumn = Columns.FirstOrDefault(col => col is PpsDataColumnServerDefinition colServer && colServer.ParentType == PpsDataColumnParentRelationType.Root);
			if (rootColumn != null && rootColumn.Table is PpsSqlDataTableServerDefinition sqlDataTable)
			{
				// find inner join
				parentRootTable = sqlDataTable.GetPrimaryRootTable();

				// find relation
				parentRootTableRelation = sourceColumn.Table.RelationInfo.FirstOrDefault(c => c.ParentColumn == GetSqlColumnInfo(rootColumn.ParentColumn) && c.ReferencedColumn == GetSqlColumnInfo(rootColumn))
					?? throw new ArgumentNullException($"No relation for parent:'{rootColumn.ParentColumn.Name}' -> ref: '{rootColumn.Name}'");
			}

			// generate automatic schema binding
			primaryTable = GenerateTableBinding(sourceColumn.Table, parentRootTable, parentRootTableRelation);

			// create binding for all columns (left outer joins)
			var tableStack = new List<SqlTableBinding>
			{
				primaryTable
			};
			foreach (var col in Columns)
			{
				var sqlColumn = GetSqlColumnInfo(col);
				if (sqlColumn != null)
				{
					// has this column a binding
					if (primaryTable.FindTable(t => sqlColumn.Table == t.SqlTable) != null)
						continue;

					GenerateTableBindings(sqlColumn.Table, tableStack);
				}
			}

			return primaryTable;
		} // func GetPrimaryRootTable

		protected override void EndInit()
		{
			base.EndInit();

			GetPrimaryRootTable();
		} // proc EndInit

		#endregion

		private SqlParameterBinding[] GetParameterMapping(SqlCommand command, LuaTable args)
		{
			// prepare parameter binding
			var columnMapping = args.Members.Keys.Select(k => new SqlParameterBinding(k)).ToArray();
			if (columnMapping.Length > 0)
			{
				var columnMappingCount = 0;
				foreach (var t in DataSet.TableDefinitions)
				{
					foreach (var _c in t.Columns)
					{
						if (_c is PpsDataColumnServerDefinition c)
						{
							var idx = Array.FindIndex(columnMapping, m => String.Compare(c.Name, m.MemberName, true) == 0);
							if (idx >= 0)
							{
								columnMapping[idx].SetDataColumn(command, new SqlColumnBinding(c, c.GetColumnDescriptionImplementation<PpsSqlExDataSource.SqlColumnInfo>()), args[columnMapping[idx].MemberName]);
								columnMappingCount++;
								if (columnMapping.Length == columnMappingCount)
									return columnMapping;
							}
						}
					}
				}

				if (columnMappingCount < columnMapping.Length)
					throw new ArgumentException("Invalid argument(s).");
			}

			return columnMapping;
		} // func GetParameterMapping

		private void CollectInnerJoins(SqlTableBinding currentTable, List<SqlTableBinding> innerJoins, SqlParameterBinding[] parameterInfo, ref int foundInCounter)
		{
			foreach (var col in currentTable.Columns)
			{
				var idx = Array.FindIndex(parameterInfo, c => c.DataColumn == col.DataColumn);
				if (idx >= 0)
				{
					foundInCounter++;
					if (foundInCounter == parameterInfo.Length)
						return;
				}
			}

			if (currentTable.ParentTable != null)
			{
				innerJoins.Add(currentTable.ParentTable);
				CollectInnerJoins(currentTable, innerJoins, parameterInfo, ref foundInCounter);
			}
		} // proc CollectInnerJoins

		private void AppendLoadColumns(StringBuilder loadCommand, SqlTableBinding table, ref int columnIndex, ref int primaryKeyIndex)
		{
			foreach (var columnBinding in table.Columns)
			{
				if (columnIndex > 0)
					loadCommand.Append(", ");

				if (columnBinding == primaryKey)
					primaryKeyIndex = columnIndex;
				

				loadCommand.Append(columnBinding.SqlColumn.TableColumnName)
					.Append(" AS ")
					.Append('[').Append(columnBinding.DataColumn.Name).Append(']');

				columnIndex++;
			}

			foreach (var cur in table.ChildTables)
				AppendLoadColumns(loadCommand, cur, ref columnIndex, ref primaryKeyIndex);
		} // func AppendLoadColumns
		
		private void AppendLoadOuterJoin(StringBuilder loadCommand, SqlTableBinding table)
		{
			foreach (var cur in table.ChildTables)
			{
				loadCommand.Append("LEFT OUTER JOIN ")
					.Append(cur.SqlParentRelation.ParentColumn.Table.FullName).Append(" ON (")
					.Append(cur.SqlParentRelation.ParentColumn.TableColumnName)
					.Append(" = ")
					.Append(cur.SqlParentRelation.ReferencedColumn.TableColumnName)
					.Append(") ");

				AppendLoadOuterJoin(loadCommand, cur);
			}
		} // proc AppendLoadOuterJoin

		internal SqlCommand PrepareLoad(SqlCommand sqlCommand, LuaTable args, out int primaryKeyIndex)
		{
			var parameterInfo = GetParameterMapping(sqlCommand, args);

			// prepare inner join list
			var innerJoins = new List<SqlTableBinding>();
			var foundInCounter = 0;
			if (parameterInfo.Length > 0)
			{
				CollectInnerJoins(primaryTable, innerJoins, parameterInfo, ref foundInCounter);
				if (foundInCounter < parameterInfo.Length)
					throw new ArgumentException("Could not resolve all arguments.");
			}

			// create select list
			var loadCommand = new StringBuilder("SELECT ");
			var columnIndex = 0;
			primaryKeyIndex = -1;
			AppendLoadColumns(loadCommand, primaryTable, ref columnIndex, ref primaryKeyIndex);

			// create from
			loadCommand.Append(" FROM ");
			loadCommand.Append(primaryTable.SqlTable.FullName).Append(' ');

			foreach(var innerJoin in innerJoins)
			{
				loadCommand.Append("INNER JOIN ")
					.Append(innerJoin.SqlTable.FullName).Append(" ON (")
					.Append(innerJoin.SqlParentRelation.ParentColumn.TableColumnName)
					.Append(" = ")
					.Append(innerJoin.SqlParentRelation.ReferencedColumn.TableColumnName)
					.Append(") ");
			}

			AppendLoadOuterJoin(loadCommand, primaryTable);
			
			// append where
			var first = true;
			foreach(var col in parameterInfo)
			{
				if (first)
				{
					loadCommand.Append(" WHERE ");
					first = false;
				}
				else
					loadCommand.Append(" AND ");

				var parameterName = '@' + col.DataColumn.Name;
				loadCommand.Append('(');
				if (col.DataColumn.DataType.IsClass)
					loadCommand.Append(parameterName).Append(" IS null OR ");
				loadCommand.Append(col.SqlColumn.TableColumnName).Append(" = ").Append(parameterName)
				.Append(')');
			}

			// build command object
			sqlCommand.CommandText = loadCommand.ToString();

			return sqlCommand;
		} // func PrepareLoadCommand

		private void PrepareMerge(SqlCommand command, StringBuilder commandText, SqlTableBinding table, List<PpsDataColumnServerDefinition> primaryKeys)
		{
			// merge into
			commandText.Append("MERGE INTO ")
				.Append(table.SqlTable.FullName).Append(" AS dst ");

			// using with variables
			var aliasList = new string[table.Columns.Length];
			var parameterList = new string[table.Columns.Length];
			var primaryKeyIndex = -1;
			for(var i = 0;i< aliasList.Length;i++)
			{
				var sqlColumn = table.Columns[i];
				if (sqlColumn.SqlColumn.IsPrimary)
				{
					primaryKeyIndex = i;
					primaryKeys.Add(sqlColumn.DataColumn);
				}

				aliasList[i] = sqlColumn.DataColumn.Name;
				parameterList[i] = "@" + aliasList[i];
				var parameter = command.Parameters.Add(sqlColumn.SqlColumn.CreateSqlParameter(parameterList[i], DBNull.Value));
				parameter.SourceColumn = table.Columns[i].DataColumn.Name;
			}

			if (primaryKeyIndex == -1)
				throw new ArgumentException("primary key missing.");

			commandText.Append("USING (");
			commandText.Append(String.Join(",", parameterList));
			commandText.Append(") as src (").Append(String.Join(",", aliasList)).Append(") ");

			// on statement
			commandText.Append("ON dst.")
				.Append(table.Columns[primaryKeyIndex].SqlColumn.ColumnName)
				.Append(" = ")
				.Append("src.").Append(table.Columns[primaryKeyIndex].DataColumn.Name).Append(" ");

			// matched
			commandText.Append("WHEN MATCHED THEN UPDATE ");
			var first = true;
			for (var i = 0; i < aliasList.Length; i++)
			{
				if (primaryKeyIndex == i)
					continue;

				if (first)
					first = false;
				else
					commandText.Append(',');
				commandText.Append("dst.").Append(table.Columns[i].SqlColumn.ColumnName)
					.Append(" = ")
					.Append("src.").Append(aliasList[i]);
			}
			commandText.Append(' ');

			// not matched -> insert
			commandText.Append("WHEN NOT MATCHED BY TARGET THEN INSERT (")
				.Append(String.Join(",", table.Columns.Select(c => "dst." + c.SqlColumn.ColumnName)))
				.Append(") VALUES (").Append(String.Join(",", aliasList.Select(c => "src." + c))).Append(") ");

			// not matched delete
			commandText.Append("WHEN NOT MATCHED BY SOURCE THEN DELETE ");

			// build the output for the primary key
			commandText.Append("OUTPUT inserted.")
				.Append(table.Columns[primaryKeyIndex].SqlColumn.ColumnName)
				.Append(" AS ")
				.Append(table.Columns[primaryKeyIndex].DataColumn.Name)
				.Append(';');
		} // proc PrepareMerge

		internal SqlCommand PrepareMerge(SqlCommand command, List<PpsDataColumnServerDefinition> primaryKeys)
		{
			var commandText = new StringBuilder();
			PrepareMerge(command, commandText, primaryTable, primaryKeys);
			foreach (var c in primaryTable.ChildTables)
				PrepareMerge(command, commandText, c, primaryKeys);

			command.CommandText = commandText.ToString();
			return command;
		} // func PrepareMerge

		public override PpsDataTable CreateDataTable(PpsDataSet dataset)
			=> new PpsSqlDataTableServer(this, dataset);

		//public void RegisterColumnBinding(string columnName, function)
		//public void RegisterColumnBinding(function, string sqlTable, string sqlColumn)

		//public void RegisterColumnBinding(string columnName, string sqlTable, string sqlColumn)
		//{
		//} // proc RegisterColumnBinding
	} // class PpsSqlDataTableServerDefinition

	#endregion
}
