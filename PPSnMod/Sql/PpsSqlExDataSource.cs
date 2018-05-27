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
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Data;
using TecWare.DE.Server;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;
using TecWare.PPSn.Server.Data;

namespace TecWare.PPSn.Server.Sql
{
	/// <summary></summary>
	public class PpsSqlExDataSource : PpsSqlDataSource
	{
		#region -- class SqlConnectionHandle --------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private class SqlConnectionHandle : IPpsConnectionHandle
		{
			public event EventHandler Disposed;

			private readonly PpsSqlExDataSource dataSource;
			private readonly SqlConnectionStringBuilder connectionString;
			private readonly IPpsPrivateDataContext identity;
			private readonly SqlConnection connection; // sql connection for read data

			public SqlConnectionHandle(PpsSqlExDataSource dataSource, SqlConnectionStringBuilder connectionString, IPpsPrivateDataContext identity)
			{
				this.dataSource = dataSource;
				this.connectionString = connectionString;
				this.identity = identity;
				this.connection = new SqlConnection();
			} // ctor

			public void Dispose()
			{
				// clear connection
				connection?.Dispose();

				// invoke disposed
				Disposed?.Invoke(this, EventArgs.Empty);
			} // proc Dispose

			private static async Task<bool> ConnectAsync(SqlConnectionStringBuilder connectionString, SqlConnection connection, IPpsPrivateDataContext identity, bool throwException)
			{
				// create the connection
				try
				{
					using (var currentCredentials = identity.GetNetworkCredential())
					{
						if (currentCredentials is PpsIntegratedCredentials ic)
						{
							connectionString.IntegratedSecurity = true;
							connection.ConnectionString = connectionString.ToString();

							using (ic.Impersonate()) // is only functional in the admin context
								await connection.OpenAsync();
						}
						else if (currentCredentials is PpsUserCredentials uc) // use network credentials
						{
							connectionString.IntegratedSecurity = false;
							connection.ConnectionString = connectionString.ToString();

							connection.Credential = new SqlCredential(uc.UserName, uc.Password);
							await connection.OpenAsync();
						}
					}
					return true;
				}
				catch (Exception)
				{
					if (throwException)
						throw;
					return false;
				}
			} // func Connect

			public async Task<SqlConnection> ForkConnectionAsync()
			{
				// create a new connection
				var con = new SqlConnection();
				var conStr = new SqlConnectionStringBuilder(connectionString.ToString())
				{
					ApplicationName = "User_Trans",
					Pooling = true
				};

				// ensure connection
				await ConnectAsync(conStr, con, identity, true);

				return con;
			} // func ForkConnection

			public Task<bool> EnsureConnectionAsync(bool throwException)
			{
				if (IsConnected)
					return Task.FromResult(true);

				return ConnectAsync(connectionString, connection, identity, throwException);
			} // func EnsureConnection

			public PpsDataSource DataSource => dataSource;
			public SqlConnection Connection => connection;

			public bool IsConnected => IsConnectionOpen(connection);
		} // class SqlConnectionHandle

		#endregion

		#region -- class PpsDataResultColumnDescription ---------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary>Simple column description implementation.</summary>
		private sealed class PpsDataResultColumnDescription : PpsColumnDescription
		{
			#region -- class PpsDataResultColumnAttributes ----------------------------------

			private sealed class PpsDataResultColumnAttributes : IPropertyEnumerableDictionary
			{
				private readonly PpsDataResultColumnDescription column;

				public PpsDataResultColumnAttributes(PpsDataResultColumnDescription column)
				{
					this.column = column;
				} // ctor

				public bool TryGetProperty(string name, out object value)
				{
					if (String.Compare(name, "MaxLength", StringComparison.OrdinalIgnoreCase) == 0)
					{
						value = GetDataRowValue(column.row, "ColumnSize", 0);
						return true;
					}
					else
					{
						foreach (var c in column.row.Table.Columns.Cast<DataColumn>())
						{
							if (String.Compare(c.ColumnName, name, StringComparison.OrdinalIgnoreCase) == 0)
							{
								value = column.row[c];
								return value != DBNull.Value;
							}
						}
					}

					value = null;
					return false;
				} // func TryGetProperty

				public IEnumerator<PropertyValue> GetEnumerator()
				{
					foreach (var c in column.row.Table.Columns.Cast<DataColumn>())
					{
						if (column.row[c] != DBNull.Value)
							yield return new PropertyValue(c.ColumnName, column.row[c]);
					}
				} // func GetEnumerator

				IEnumerator IEnumerable.GetEnumerator()
					=> GetEnumerator();
			} // class PpsDataResultColumnAttributes

			#endregion

			private readonly DataRow row;

			public PpsDataResultColumnDescription(IPpsColumnDescription parent, DataRow row, string name, Type dataType)
				: base(parent, name, dataType)
			{
				this.row = row;
			} // ctor

			protected override IPropertyEnumerableDictionary CreateAttributes()
				=> PpsColumnDescriptionHelper.GetColumnDescriptionParentAttributes(new PpsDataResultColumnAttributes(this), Parent);
		} // class PpsDataResultColumnDescription

		#endregion

		#region -- class SqlDataSelectorToken -------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary>Representation of a data view for the system.</summary>
		private sealed class SqlDataSelectorToken : IPpsSelectorToken
		{
			private readonly PpsSqlExDataSource source;
			private readonly string name;
			private readonly string viewName;

			private readonly IPpsColumnDescription[] columnDescriptions;

			private SqlDataSelectorToken(PpsSqlExDataSource source, string name, string viewName, IPpsColumnDescription[] columnDescriptions)
			{
				this.source = source;
				this.name = name;
				this.viewName = viewName;
				this.columnDescriptions = columnDescriptions;
			} // ctor

			public PpsDataSelector CreateSelector(IPpsConnectionHandle connection, bool throwException = true)
				=> new SqlDataSelector((SqlConnectionHandle)connection, this, null, null, null);

			public IPpsColumnDescription GetFieldDescription(string selectorColumn)
				=> columnDescriptions.FirstOrDefault(c => String.Compare(selectorColumn, c.Name, StringComparison.OrdinalIgnoreCase) == 0);

			public string Name => name;
			public string ViewName => viewName;
			public PpsSqlExDataSource DataSource => source;

			PpsDataSource IPpsSelectorToken.DataSource => DataSource;

			public IEnumerable<IPpsColumnDescription> Columns => columnDescriptions;

			// -- Static  -----------------------------------------------------------

			private static void ExecuteForResultSet(SqlConnection connection, PpsSqlExDataSource source, string name, out IPpsColumnDescription[] columnDescriptions)
			{
				// execute the view once to determine the resultset
				using (var cmd = connection.CreateCommand())
				{
					cmd.CommandTimeout = 6000;
					cmd.CommandText = "select * from " + name;
					using (var r = cmd.ExecuteReader(CommandBehavior.SchemaOnly | CommandBehavior.KeyInfo))
					{
						columnDescriptions = new IPpsColumnDescription[r.FieldCount];

						var dt = r.GetSchemaTable();
						var i = 0;
						foreach (DataRow c in dt.Rows)
						{
							IPpsColumnDescription parentColumnDescription;
							var nativeColumnName = r.GetName(i);

							// try to find the view base description
							parentColumnDescription = source.application.GetFieldDescription(name + "." + nativeColumnName, false);

							// try to find the table based field name
							if (parentColumnDescription == null)
							{
								var schemaName = GetDataRowValue<string>(c, "BaseSchemaName", null) ?? "dbo";
								var tableName = GetDataRowValue<string>(c, "BaseTableName", null);
								var columnName = GetDataRowValue<string>(c, "BaseColumnName", null);

								if (tableName != null && columnName != null)
								{
									var fieldName = schemaName + "." + tableName + "." + columnName;
									parentColumnDescription = source.application.GetFieldDescription(fieldName, false);
								}
							}

							columnDescriptions[i] = new PpsDataResultColumnDescription(parentColumnDescription, c, nativeColumnName, r.GetFieldType(i));
							i++;
						}
					}
				} // using cmd
			} // pro ExecuteForResultSet

			private static string CreateOrReplaceView(SqlConnection connection, string name, string selectStatement)
			{
				// execute the new view
				using (var cmd = connection.CreateCommand())
				{
					cmd.CommandTimeout = 6000;
					cmd.CommandType = CommandType.Text;

					// drop
					cmd.CommandText = $"IF object_id('{name}', 'V') IS NOT NULL DROP VIEW {name}";
					cmd.ExecuteNonQuery();

					// create
					cmd.CommandText = $"CREATE VIEW {name} AS {selectStatement}";
					cmd.ExecuteNonQuery();
				} // using cmd

				return name;
			} // proc CreateOrReplaceView

			private static IPpsSelectorToken CreateCore(PpsSqlExDataSource source, string name, Func<SqlConnection, string> getViewName)
			{
				IPpsColumnDescription[] columnDescriptions;
				SqlConnection connection;

				string viewName = null;
				using (source.UseMasterConnection(out connection))
				{
					viewName = getViewName(connection);
					ExecuteForResultSet(connection, source, viewName, out columnDescriptions);
				}

				return new SqlDataSelectorToken(source, name, viewName, columnDescriptions);
			} // func CreateCore

			public static IPpsSelectorToken CreateFromStatement(PpsSqlExDataSource source, string name, string selectStatement)
				=> CreateCore(source, name, (connection) => CreateOrReplaceView(connection, name, selectStatement));

			public static IPpsSelectorToken CreateFromFile(PpsSqlExDataSource source, string name, string fileName)
				=> CreateCore(source, name, (connection) => CreateOrReplaceView(connection, name, File.ReadAllText(fileName)));

			public static IPpsSelectorToken CreateFromResource(PpsSqlExDataSource source, string name, string resourceName)
				=> CreateCore(source, name, (connection) => CreateOrReplaceView(connection, name, source.GetResourceScript(resourceName)));

			public static IPpsSelectorToken CreateFromPredefinedView(PpsSqlExDataSource source, string name, string viewName = null)
				=> CreateCore(source, name, (connection) => viewName ?? name);

			public static IPpsSelectorToken CreateFromXml(PpsSqlExDataSource source, string name, XElement sourceDescription)
			{
				// file => init by file
				// select => inline sql select
				// view => name of existing view
				try
				{
					var sourceType = sourceDescription.GetAttribute("type", "file");
					if (sourceType == "select") // create view from sql
						return CreateFromStatement(source, name, sourceDescription.Value);
					else if (sourceType == "file")
						return CreateFromFile(source, name, ProcsDE.GetFileName(sourceDescription, sourceDescription.Value));
					else if (sourceType == "resource")
						return CreateFromResource(source, name, sourceDescription.Value);
					else if (sourceType == "view")
						return CreateFromPredefinedView(source, name, sourceDescription?.Value);
					else
						throw new ArgumentOutOfRangeException(); // todo:
				}
				catch (Exception e)
				{
					throw new DEConfigurationException(sourceDescription, String.Format("Can not create selector for '{0}'.", name), e);
				}
			} // func CreateFromXml
		} // class SqlDataSelectorToken

		#endregion

		#region -- class SqlDataSelector ------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class SqlDataSelector : PpsDataSelector
		{
			#region -- class SqlDataFilterVisitor ---------------------------------------------

			///////////////////////////////////////////////////////////////////////////////
			/// <summary></summary>
			private sealed class SqlDataFilterVisitor : PpsDataFilterVisitorSql
			{
				private readonly Func<string, string> lookupNative;
				private readonly Func<string, IPpsColumnDescription> lookupColumn;

				public SqlDataFilterVisitor(Func<string, string> lookupNative, Func<string, IPpsColumnDescription> lookupColumn)
				{
					this.lookupNative = lookupNative;
					this.lookupColumn = lookupColumn;
				} // ctor

				protected override Tuple<string, Type> LookupColumn(string columnToken)
				{
					var column = lookupColumn(columnToken);
					if (column == null)
						throw new ArgumentNullException("operand", $"Could not resolve column '{columnToken}'.");

					return new Tuple<string, Type>(column.Name, column.DataType);
				} // func LookupColumn

				protected override string LookupNativeExpression(string key)
				{
					var expr = lookupNative(key);
					if (String.IsNullOrEmpty(expr))
						throw new ArgumentNullException("nativeExpression", $"Could not resolve native expression '{key}'.");
					return expr;
				} // func LookupNativeExpression
			} // class SqlDataFilterVisitor

			#endregion

			private readonly SqlConnectionHandle connection;

			private readonly SqlDataSelectorToken selectorToken;
			private readonly string selectList;
			private readonly string whereCondition;
			private readonly string orderBy;

			public SqlDataSelector(SqlConnectionHandle connection, SqlDataSelectorToken selectorToken, string selectList, string whereCondition, string orderBy)
				: base(connection.DataSource)
			{
				this.connection = connection;
				this.selectorToken = selectorToken;
				this.selectList = selectList;
				this.whereCondition = whereCondition;
				this.orderBy = orderBy;
			} // ctor

			private string FormatOrderExpression(PpsDataOrderExpression o, Func<string, string> lookupNative, Func<string, IPpsColumnDescription> lookupColumn)
			{
				// check for native expression
				if (lookupNative != null)
				{
					var expr = lookupNative(o.Identifier);
					if (expr != null)
					{
						if (o.Negate)
						{
							// todo: replace asc with desc and desc with asc
							expr = expr.Replace(" asc", " desc");
						}
						return expr;
					}
				}

				// checkt the column
				var column = lookupColumn(o.Identifier);

				if (o.Negate)
					return column.Name + " DESC";
				else
					return column.Name;
			} // func FormatOrderExpression

			public override PpsDataSelector ApplyOrder(IEnumerable<PpsDataOrderExpression> expressions, Func<string, string> lookupNative)
				=> SqlOrderBy(String.Join(", ", from o in expressions select FormatOrderExpression(o, lookupNative, selectorToken.GetFieldDescription)));

			public override PpsDataSelector ApplyFilter(PpsDataFilterExpression expression, Func<string, string> lookupNative)
				=> SqlWhere(new SqlDataFilterVisitor(lookupNative, selectorToken.GetFieldDescription).CreateFilter(expression));

			private string AddSelectList(string addSelectList)
			{
				if (String.IsNullOrEmpty(addSelectList))
					return selectList;

				return String.IsNullOrEmpty(selectList) ? addSelectList : selectList + ", " + addSelectList;
			} // func AddSelectList

			public SqlDataSelector SqlSelect(string addSelectList)
			{
				if (String.IsNullOrEmpty(addSelectList))
					return this;

				var newSelectList = AddSelectList(addSelectList);
				return new SqlDataSelector(connection, selectorToken, newSelectList, whereCondition, orderBy);
			} // func SqlSelect

			private string AddWhereCondition(string addWhereCondition)
			{
				if (String.IsNullOrEmpty(addWhereCondition))
					return whereCondition;

				return String.IsNullOrEmpty(whereCondition) ? addWhereCondition : "(" + whereCondition + ") and (" + addWhereCondition + ")";
			} // func AddWhereCondition

			public SqlDataSelector SqlWhere(string addWhereCondition)
			{
				if (String.IsNullOrEmpty(addWhereCondition))
					return this;

				var newWhereCondition = AddWhereCondition(addWhereCondition);
				return new SqlDataSelector(connection, selectorToken, selectList, newWhereCondition, orderBy);
			} // func SqlWhere

			private string AddOrderBy(string addOrderBy)
			{
				if (String.IsNullOrEmpty(addOrderBy))
					return orderBy;

				return String.IsNullOrEmpty(orderBy) ? addOrderBy : orderBy + ", " + addOrderBy;
			} // func AddOrderBy

			public SqlDataSelector SqlOrderBy(string addOrderBy)
			{
				if (String.IsNullOrEmpty(addOrderBy))
					return this;

				var newOrderBy = AddOrderBy(addOrderBy);
				return new SqlDataSelector(connection, selectorToken, selectList, whereCondition, newOrderBy);
			} // func SqlOrderBy

			public override IEnumerator<IDataRow> GetEnumerator(int start, int count)
			{
				SqlCommand cmd = null;
				try
				{
					var trans = DataSource.Application.Database.GetActiveTransaction(DataSource);
					if (trans is SqlDataTransaction sqlTrans)
					{
						cmd = sqlTrans.CreateCommand(CommandType.Text, false);
					}
					else
					{
						cmd = new SqlCommand
						{
							Connection = connection.Connection,
							CommandType = CommandType.Text,
						};
					}

					var sb = new StringBuilder("select ");

					// build the select
					if (String.IsNullOrEmpty(selectList))
						sb.Append("* ");
					else
						sb.Append(selectList).Append(' ');

					// add the view
					sb.Append("from ").Append(selectorToken.ViewName).Append(' ');

					// add the where
					if (!String.IsNullOrEmpty(whereCondition))
						sb.Append("where ").Append(whereCondition).Append(' ');

					// add the orderBy
					if (!String.IsNullOrEmpty(orderBy))
					{
						sb.Append("order by ").Append(orderBy).Append(' ');

						// build the range, without order fetch is not possible
						if (count >= 0 && start < 0)
							start = 0;
						if (start >= 0)
						{
							sb.Append("offset ").Append(start).Append(" rows ");
							if (count >= 0)
								sb.Append("fetch next ").Append(count).Append(" rows only ");
						}
					}

					cmd.CommandText = sb.ToString();
					return new DbRowEnumerator(cmd);
				}
				catch
				{
					cmd?.Dispose();
					throw;
				}
			} // func GetEnumerator

			public override IPpsColumnDescription GetFieldDescription(string nativeColumnName)
				=> selectorToken.GetFieldDescription(nativeColumnName);
		} // class SqlDataSelector

		#endregion

		#region -- class SqlResultInfo --------------------------------------------------

		private sealed class SqlResultInfo : List<Func<SqlDataReader, IEnumerable<IDataRow>>>
		{
		} // class SqlResultInfo

		#endregion

		#region -- class SqlJoinExpression ----------------------------------------------

		public sealed class SqlJoinExpression : PpsDataJoinExpression<PpsSqlTableInfo>
		{
			#region -- class SqlEmitVisitor ---------------------------------------------

			private sealed class SqlEmitVisitor : PpsJoinVisitor<string>
			{
				public override string CreateJoinStatement(string leftExpression, PpsDataJoinType type, string rightExpression, string on)
				{
					string GetJoinExpr()
					{
						switch(type)
						{
							case PpsDataJoinType.Inner:
								return " INNER JOIN ";
							case PpsDataJoinType.Left:
								return " LEFT OUTER JOIN ";
							case PpsDataJoinType.Right:
								return " RIGHT OUTER JOIN ";
							default:
								throw new ArgumentException(nameof(type));
						}
					} // func GetJoinExpr

					return "(" + leftExpression + GetJoinExpr() + rightExpression + " ON (" + on + "))";
				} // func CreateJoinStatement

				public override string CreateTableStatement(PpsSqlTableInfo table, string alias)
				{
					if (String.IsNullOrEmpty(alias))
						return table.QuallifiedName;
					else
						return table.QuallifiedName + " AS " + alias;
				} // func CreateTableStatement
			} // class SqlEmitVisitor

			#endregion

			private readonly PpsSqlExDataSource dataSource;

			public SqlJoinExpression(PpsSqlExDataSource dataSource)
			{
				this.dataSource = dataSource;
			} // ctor

			protected override PpsSqlTableInfo ResolveTable(string tableName)
				=> dataSource.ResolveTableByName(tableName, true);

			protected override string CreateOnStatement(PpsTableExpression left, PpsDataJoinType joinOp, PpsTableExpression right)
			{
				foreach (var r in right.Table.RelationInfo)
				{
					if (r.ReferencedColumn.Table == left.Table)
					{
						var sb = new StringBuilder();
						AppendColumn(sb, left, r.ReferencedColumn);
						sb.Append(" = ");
						AppendColumn(sb, right, r.ParentColumn);
						return sb.ToString();
					}
				}
				return null;
			} // func CreateOnStatement

			private void SplitColumnName(string name, out string alias, out string columnName)
			{
				var p = name.IndexOf('.'); // alias?
				if (p >= 0)
				{
					alias = name.Substring(0, p);
					columnName = name.Substring(p + 1);
				}
				else
				{
					alias = null;
					columnName = name;
				}
			} // func SplitColumnName

			public (PpsTableExpression, PpsSqlColumnInfo) FindColumn(IPpsColumnDescription ppsColumn, bool throwException)
			{
				foreach (var t in GetTables())
				{
					if (ppsColumn.TryGetColumnDescriptionImplementation<PpsSqlColumnInfo>(out var sqlColumn) && t.Table == sqlColumn.Table)
						return (t, sqlColumn);
				}

				if (throwException)
					throw new ArgumentException($"Column not found ({ppsColumn.Name}).");

				return (null, null);
			} // func FindColumn

			public (PpsTableExpression, PpsSqlColumnInfo) FindColumn(string name, bool throwException)
			{
				SplitColumnName(name, out var alias, out var columnName);
				foreach (var t in GetTables())
				{
					if (alias != null)
					{
						if (t.Alias != null && String.Compare(alias, t.Alias, StringComparison.OrdinalIgnoreCase) == 0)
							return (t, t.Table.FindColumn(columnName, throwException));
					}
					else
					{
						var c = t.Table.FindColumn(columnName, false);
						if (c != null)
							return (t, c);
					}
				}

				if (throwException)
					throw new ArgumentException($"Column not found ({name}).");

				return (null, null);
			} // func FindColumn

			public StringBuilder AppendColumn(StringBuilder commandText, PpsTableExpression table, PpsSqlColumnInfo column)
				=> String.IsNullOrEmpty(table.Alias)
					? column.AppendAsColumn(commandText, true)
					: column.AppendAsColumn(commandText, table.Alias);

			public string EmitJoin()
				=> new SqlEmitVisitor().Visit(this);
		} // class SqlJoinExpression

		#endregion

		#region -- class SqlDataTransaction ---------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class SqlDataTransaction : PpsDataTransaction
		{
			#region -- class PpsColumnMapping -------------------------------------------

			/// <summary></summary>
			[DebuggerDisplay("{DebuggerDisplay,nq}")]
			private sealed class PpsColumnMapping
			{
				private readonly PpsSqlColumnInfo columnInfo;
				private readonly Func<object, object> getValue;
				private readonly Action<object, object> updateValue;

				private readonly string parameterName;
				private DbParameter parameter = null;

				public PpsColumnMapping(PpsSqlColumnInfo columnInfo, Func<object, object> getValue, Action<object, object> updateValue)
				{
					this.columnInfo = columnInfo ?? throw new ArgumentNullException(nameof(columnInfo));
					this.getValue = getValue ?? throw new ArgumentNullException(nameof(getValue));
					this.updateValue = updateValue;

					this.parameterName = "@" + columnInfo.Name;
				} // ctor

				public object GetValue(object row)
					=> getValue(row);

				public void SetParameter(object row)
					=> parameter.Value = GetValue(row) ?? (object)DBNull.Value;

				public void UpdateValue(object row, object value)
					=> updateValue?.Invoke(row, value);

				public void AppendParameter(DbCommand cmd, object initialValues)
				{
					if (parameter != null)
						throw new InvalidOperationException();
					parameter = columnInfo.AppendSqlParameter(cmd, parameterName, initialValues == null ? null : getValue(initialValues));
				} // func AppendParameter

				private string DebuggerDisplay
					=> $"Mapping: {columnInfo.TableColumnName} -> {parameterName}";

				public PpsSqlColumnInfo ColumnInfo => columnInfo;
				public string ColumnName => columnInfo.Name;
				public string ParameterName => parameterName;
			} // class PpsColumnMapping

			#endregion

			private readonly SqlConnection connection;
			private readonly SqlTransaction transaction;

			#region -- Ctor/Dtor --------------------------------------------------------

			public SqlDataTransaction(PpsSqlDataSource dataSource, SqlConnectionHandle connectionHandle)
				: base(dataSource, connectionHandle)
			{
				this.connection = connectionHandle.ForkConnectionAsync().AwaitTask();

				// create the sql transaction
				this.transaction = connection.BeginTransaction(IsolationLevel.ReadUncommitted);
			} // ctor

			protected override void Dispose(bool disposing)
			{
				base.Dispose(disposing); // commit/rollback

				if (disposing)
				{
					transaction.Dispose();
					connection.Dispose();
				}
			} // proc Dispose

			public override void Commit()
			{
				if (!IsCommited.HasValue)
					transaction.Commit();
				base.Commit();
			} // proc Commit

			public override void Rollback()
			{
				try
				{
					if (!IsCommited.HasValue)
						transaction.Rollback();
				}
				finally
				{
					base.Rollback();
				}
			} // proc Rollback

			#endregion

			#region -- Execute Result ---------------------------------------------------

			internal SqlCommand CreateCommand(CommandType commandType, bool noTransaction)
			{
				var cmd = connection.CreateCommand();
				cmd.Connection = connection;
				cmd.CommandTimeout = 7200;
				cmd.Transaction = noTransaction ? null : transaction;
				return cmd;
			} // func CreateCommand

			private SqlCommand CreateCommand(LuaTable parameter, CommandType commandType)
				=> CreateCommand(commandType, parameter.GetOptionalValue("__notrans", false));

			private SqlDataReader ExecuteReaderCommand(SqlCommand cmd, PpsDataTransactionExecuteBehavior behavior)
			{
				switch (behavior)
				{
					case PpsDataTransactionExecuteBehavior.NoResult:
						cmd.ExecuteNonQuery();
						return null;
					case PpsDataTransactionExecuteBehavior.SingleRow:
						return cmd.ExecuteReader(CommandBehavior.SingleRow);
					case PpsDataTransactionExecuteBehavior.SingleResult:
						return cmd.ExecuteReader(CommandBehavior.SingleResult);
					default:
						return cmd.ExecuteReader(CommandBehavior.Default);
				}
			} // func ExecuteReaderCommand

			private LuaTable GetArguments(object value, bool throwException)
			{
				var args = value as LuaTable;
				if (args == null && throwException)
					throw new ArgumentNullException($"value", "No arguments defined.");
				return args;
			} // func GetArguments

			private LuaTable GetArguments(LuaTable parameter, int index, bool throwException)
			{
				var args = GetArguments(parameter[index], false);
				if (args == null && throwException)
					throw new ArgumentNullException($"parameter[{index}]", "No arguments defined.");
				return args;
			} // func GetArguments

			#region -- ExecuteCall ----------------------------------------------------------

			private IEnumerable<IEnumerable<IDataRow>> ExecuteCall(LuaTable parameter, string name, PpsDataTransactionExecuteBehavior behavior)
			{
				using (var cmd = CreateCommand(parameter, CommandType.StoredProcedure))
				{
					cmd.CommandText = name;

					// build argument list
					SqlCommandBuilder.DeriveParameters(cmd);

					// build parameter mapping
					var parameterMapping = new Tuple<string, SqlParameter>[cmd.Parameters.Count];
					var j = 0;
					foreach (SqlParameter p in cmd.Parameters)
					{
						var parameterName = p.ParameterName;
						if ((p.Direction & ParameterDirection.ReturnValue) == ParameterDirection.ReturnValue)
							parameterName = null;
						else if (parameterName.StartsWith("@"))
							parameterName = parameterName.Substring(1);

						parameterMapping[j++] = new Tuple<string, SqlParameter>(parameterName, p);
					}

					// copy arguments
					for (var i = 1; i <= parameter.ArrayList.Count; i++)
					{
						var args = GetArguments(parameter, i, false);
						if (args == null)
							yield break;

						// fill arguments
						foreach (var p in parameterMapping)
						{
							var value = args?.GetMemberValue(p.Item1);
							p.Item2.Value = value == null ? (object)DBNull.Value : value;
						}

						using (var r = ExecuteReaderCommand(cmd, behavior))
						{
							// copy arguments back
							foreach (var p in parameterMapping)
							{
								if (p.Item1 == null)
									args[1] = p.Item2.Value.NullIfDBNull();
								else if ((p.Item2.Direction & ParameterDirection.Output) == ParameterDirection.Output)
									args[p.Item1] = p.Item2.Value.NullIfDBNull();
							}

							// return results
							if (r != null)
							{
								do
								{
									yield return new DbRowReaderEnumerable(r);
									if (behavior == PpsDataTransactionExecuteBehavior.SingleResult)
										break;
								} while (r.NextResult());
							}
						} // using r
					} // for (args)
				} // using cmd
			} // func ExecuteInsertResult

			#endregion

			#region -- ExecuteInsert --------------------------------------------------------

			private IEnumerable<IEnumerable<IDataRow>> ExecuteInsert(LuaTable parameter, string name, PpsDataTransactionExecuteBehavior behavior)
			{
				/*
				 * insert into {name} ({columnList})
				 * output inserted.{column}, inserted.{column}
				 * values ({variableList}
				 */

				// find the connected table
				var tableInfo = SqlDataSource.ResolveTableByName(name, true);

				using (var cmd = CreateCommand(parameter, CommandType.Text))
				{
					var commandText = new StringBuilder();
					var variableList = new StringBuilder();
					var insertedList = new StringBuilder();

					commandText.Append("INSERT INTO ")
						.Append(tableInfo.QuallifiedName);

					// default is that only one row is done
					var args = GetArguments(parameter, 1, true);

					commandText.Append(" (");

					// output always primary key
					var primaryKey = tableInfo.PrimaryKey;
					if (primaryKey != null)
						insertedList.Append("inserted.").Append(primaryKey.Name);

					// create the column list
					var first = true;
					foreach (var column in tableInfo.Columns)
					{
						var columnName = column.Name;

						var value = args.GetMemberValue(columnName, true);
						if (value != null)
						{
							if (first)
								first = false;
							else
							{
								commandText.Append(',');
								variableList.Append(',');
							}

							var parameterName = '@' + columnName;
							commandText.Append('[' + columnName + ']');
							variableList.Append(parameterName);
							column.AppendSqlParameter(cmd, parameterName, value);
						}

						if (primaryKey != column)
						{
							if (insertedList.Length > 0)
								insertedList.Append(',');
							insertedList.Append("inserted.").Append('[' + columnName + ']');
						}
					}

					commandText.Append(") ");

					// generate output clause
					commandText.Append("OUTPUT ").Append(insertedList);

					// values
					commandText.Append(" VALUES (")
						.Append(variableList)
						.Append(");");

					cmd.CommandText = commandText.ToString();

					// execute insert
					using (var r = cmd.ExecuteReader(CommandBehavior.SingleRow))
					{
						if (!r.Read())
							throw new InvalidDataException("Invalid return data from sql command.");

						for (var i = 0; i < r.FieldCount; i++)
							args[r.GetName(i)] = r.GetValue(i).NullIfDBNull();
					}
				}
				yield break; // empty enumeration
			} // func ExecuteInsert

			#endregion

			#region -- ExecuteUpdate --------------------------------------------------------

			private IEnumerable<IEnumerable<IDataRow>> ExecuteUpdate(LuaTable parameter, string name, PpsDataTransactionExecuteBehavior behavior)
			{
				/*
				 * update {name} set {column} = {arg},
				 * output inserted.{column}, inserted.{column}
				 * where {PrimaryKey} = @arg
				 */

				// find the connected table
				var tableInfo = SqlDataSource.ResolveTableByName(name, true);

				using (var cmd = CreateCommand(parameter, CommandType.Text))
				{
					var commandText = new StringBuilder();
					var insertedList = new StringBuilder();

					commandText.Append("UPDATE ")
						.Append(tableInfo.QuallifiedName);

					// default is that only one row is done
					var args = GetArguments(parameter, 1, true);

					commandText.Append(" SET ");

					// create the column list
					var first = true;
					foreach (var column in tableInfo.Columns)
					{
						var columnName = column.Name;
						var value = args.GetMemberValue(columnName, true);
						if (value == null || column == tableInfo.PrimaryKey)
							continue;

						if (first)
							first = false;
						else
						{
							commandText.Append(',');
							insertedList.Append(',');
						}

						var parameterName = '@' + columnName;
						commandText.Append(columnName)
							.Append(" = ")
							.Append(parameterName);

						column.AppendSqlParameter(cmd, parameterName, value);

						insertedList.Append("inserted.").Append("[").Append(columnName).Append("]");
					}
					
					if (insertedList.Length == 0)
						throw new ArgumentException("No Columns to update.");

					// generate output clause
					commandText.Append(" output ").Append(insertedList);

					// where
					var primaryKeyName = tableInfo.PrimaryKey.Name;
					var primaryKeyValue = args[primaryKeyName];
					if (primaryKeyValue == null)
						throw new ArgumentException("Invalid primary key.");

					commandText.Append(" WHERE ")
						.Append(primaryKeyName)
						.Append(" = ")
						.Append("@").Append(primaryKeyName);
					tableInfo.PrimaryKey.AppendSqlParameter(cmd, "@" + primaryKeyName, primaryKeyValue);

					cmd.CommandText = commandText.ToString();

					// execute insert
					using (var r = cmd.ExecuteReader(CommandBehavior.SingleRow))
					{
						if (!r.Read())
							throw new InvalidDataException("Invalid return data from sql command.");

						for (var i = 0; i < r.FieldCount; i++)
							args[r.GetName(i)] = r.GetValue(i).NullIfDBNull();
					}
				}
				yield break; // empty enumeration
			} // func ExecuteUpdate

			#endregion

			#region -- ExecuteUpsert --------------------------------------------------------
			
			private (IEnumerator<object> rows, PpsColumnMapping[] mapping) PrepareColumnMapping(PpsSqlTableInfo tableInfo, LuaTable parameter)
			{
				var columnMapping = new List<PpsColumnMapping>();

				void CheckColumnMapping()
				{
					if (columnMapping.Count == 0)
						throw new ArgumentException("Column Array is empty.");
				} // proc CheckColumnMapping

				IEnumerator<object> GetTableRowEnum()
				{
					var rowEnumerator = parameter.ArrayList.GetEnumerator();
					if (!rowEnumerator.MoveNext())
					{
						rowEnumerator.Dispose();
						throw new ArgumentException("Empty result.");
					}
						return rowEnumerator;
				} // func GetTableRowEnum

				void CreateColumnMapping(IReadOnlyCollection<IDataColumn> columns)
				{
					foreach (var c in columns)
					{
						if (c is IPpsColumnDescription t)
						{
							var dataColumn = (PpsDataColumnDefinition)t;
							var idx = dataColumn.Index;
							var nativeColumn = t.GetColumnDescription<PpsSqlColumnInfo>();
							if (nativeColumn != null && nativeColumn.Table == tableInfo)
							{
								if (dataColumn.IsExtended)
								{
									if (typeof(IPpsDataRowGetGenericValue).IsAssignableFrom(dataColumn.DataType))
									{
										var getterFunc = new Func<object, object>(row => ((IPpsDataRowGetGenericValue)((PpsDataRow)row)[idx]).Value);
										var setterFunc = typeof(IPpsDataRowSetGenericValue).IsAssignableFrom(dataColumn.DataType)
											? new Action<object, object>((row, value) => ((IPpsDataRowSetGenericValue)((PpsDataRow)row)[idx]).SetGenericValue(false, value))
											: null;

										columnMapping.Add(new PpsColumnMapping(nativeColumn, getterFunc, setterFunc));
									}
								}
								else
									columnMapping.Add(new PpsColumnMapping(nativeColumn, row => ((PpsDataRow)row)[idx], (row, value) => ((PpsDataRow)row)[idx] = value));
							}
						}
					}
				} // proc CreateColumnMapping

				if (parameter.GetMemberValue("rows") is IEnumerable<PpsDataRow> rows) // from DataTable
				{
					var rowEnumerator = rows.GetEnumerator();
					if (!rowEnumerator.MoveNext()) // no arguments defined
					{
						rowEnumerator.Dispose();
						return (null, null); // silent return nothing
					}

					// map the columns
					CreateColumnMapping(((IDataColumns)rowEnumerator.Current).Columns);
					CheckColumnMapping();

					return (rowEnumerator, columnMapping.ToArray());
				}
				else if (parameter["columnList"] is LuaTable luaColumnList) // from a "select"-list
				{
					foreach (var k in luaColumnList.ArrayList)
					{
						if (k is string columnName)
							columnMapping.Add(new PpsColumnMapping(tableInfo.FindColumn(columnName, true), row => ((LuaTable)row)[columnName], (row, obj) => ((LuaTable)row)[columnName] = obj));
					}

					CheckColumnMapping();

					return (GetTableRowEnum(), columnMapping.ToArray());
				}
				else if(parameter["columnList"] is IDataColumns columnDefinition) // from a "column"-list
				{
					CreateColumnMapping(columnDefinition.Columns);
					CheckColumnMapping();

					return (GetTableRowEnum(), columnMapping.ToArray());
				}
				else // from arguments
				{
					var args = GetArguments(parameter, 1, true);
					foreach (var columnName in args.Members.Keys)
					{
						var column = tableInfo.FindColumn(columnName, true);
						if (column != null)
							columnMapping.Add(new PpsColumnMapping(column, row => ((LuaTable)row)[columnName], (row, obj) => ((LuaTable)row)[columnName] = obj));
					}

					CheckColumnMapping();

					return (GetTableRowEnum(), columnMapping.ToArray());
				}
			} // func PrepareColumnMapping

			private IEnumerable<IEnumerable<IDataRow>> ExecuteUpsert(LuaTable parameter, string name, PpsDataTransactionExecuteBehavior behavior)
			{
				/*
				 * merge into table as dst
				 *	 using (values (@args), (@args)) as src
				 *	 on @primkey or on clause
				 *	 when matched then
				 *     set @arg = @arg, @arg = @arg, 
				 *	 when not matched then
				 *	   insert (@args) values (@args)
				 *	 output 
				 * 
				 */
				
				// prepare basic parameters for the merge command
				var tableInfo = SqlDataSource.ResolveTableByName(name, true);

				// data table row mapping
				var (rowEnumerator, columnMapping) = PrepareColumnMapping(tableInfo, parameter);
				if (columnMapping == null)
					yield break;

				using (var cmd = CreateCommand(parameter, CommandType.Text))
				{
					var commandText = new StringBuilder();
					
					#region -- dst --
					commandText.Append("MERGE INTO ")
						.Append(tableInfo.QuallifiedName)
						.Append(" as DST ");
					#endregion

					#region -- src --
					var columnNames = new StringBuilder();
					commandText.Append("USING (VALUES (");

					var first = true;
					foreach (var col in columnMapping)
					{
						if (first)
							first = false;
						else
						{
							commandText.Append(", ");
							columnNames.Append(", ");
						}

						commandText.Append(col.ParameterName);
						col.ColumnInfo.AppendAsColumn(columnNames);
						col.AppendParameter(cmd, null);
					}
					
					commandText.Append(")) AS SRC (")
						.Append(columnNames)
						.Append(") ");
					#endregion

					#region -- on --
					commandText.Append("ON ");
					var onClauseValue = parameter.GetMemberValue("on");
					if (onClauseValue == null) // no on clause use primary key
					{
						var col = tableInfo.PrimaryKey ?? throw new ArgumentNullException("primaryKey", $"Table {tableInfo.QuallifiedName} has no primary key (use the onClause).");
						ExecuteUpsertAppendOnClause(commandText, col);
					}
					else if (onClauseValue is string onClauseString) // on clause is defined as expression
					{
						commandText.Append(onClauseString);
					}
					else if (onClauseValue is LuaTable onClause) // create the on clause from colums
					{
						first = true;
						foreach (var p in onClause.ArrayList)
						{
							if (first)
								first = false;
							else
								commandText.Append(" AND ");
							var col = tableInfo.FindColumn((string)p, true);
							ExecuteUpsertAppendOnClause(commandText, col);
						}
					}
					else
						throw new ArgumentException("Can not interpret on-clause.");
					commandText.Append(" ");
					#endregion

					#region -- when matched --
					commandText.Append("WHEN MATCHED THEN UPDATE SET ");
					first = true;
					foreach (var col in columnMapping)
					{
						if (col.ColumnInfo.IsIdentity) // no autoincrement
							continue;
						else if (first)
							first = false;
						else
							commandText.Append(", ");
						commandText.Append("DST.");
						col.ColumnInfo.AppendAsColumn(commandText);
						commandText.Append(" = ");
						commandText.Append("SRC.");
						col.ColumnInfo.AppendAsColumn(commandText);
					}
					commandText.Append(' ');
					#endregion

					#region -- when not matched by target --
					commandText.Append("WHEN NOT MATCHED BY TARGET THEN INSERT (");
					first = true;
					foreach (var col in columnMapping)
					{
						if (col.ColumnInfo.IsIdentity)
							continue;
						else if (first)
							first = false;
						else
							commandText.Append(", ");
						col.ColumnInfo.AppendAsColumn(commandText);
					}
					commandText.Append(") VALUES (");
					first = true;
					foreach (var col in columnMapping)
					{
						if (col.ColumnInfo.IsIdentity)
							continue;
						else if (first)
							first = false;
						else
							commandText.Append(", ");
						commandText.Append("SRC.");
						col.ColumnInfo.AppendAsColumn(commandText);
					}
					commandText.Append(") ");
					#endregion

					#region -- when not matched by source --

					// delete, or update to deleted?
					if (parameter.GetMemberValue("nmsrc") is LuaTable notMatchedSource)
					{
						if (notMatchedSource["delete"] != null)
						{
							if (notMatchedSource["where"] is string whereDelete)
								commandText.Append("WHEN NOT MATCHED BY SOURCE AND (" + whereDelete + ") THEN DELETE ");
							else
								commandText.Append("WHEN NOT MATCHED BY SOURCE THEN DELETE ");
						}
					}

					#endregion
					
					#region -- output --
					commandText.Append("OUTPUT ");
					first = true;
					foreach (var col in tableInfo.Columns)
					{
						if (first)
							first = false;
						else
							commandText.Append(", ");
						commandText.Append("INSERTED.");
						col.AppendAsColumn(commandText);
					}

					#endregion

					commandText.Append(';');

					cmd.CommandText = commandText.ToString();

					do
					{
						var currentRow = rowEnumerator.Current;

						// update parameter
						foreach (var col in columnMapping)
							col.SetParameter(currentRow);

						// exec
						using (var r = cmd.ExecuteReaderEx(CommandBehavior.SingleRow))
						{
							if (!r.Read())
								throw new InvalidDataException("Invalid return data from sql command.");

							for (var i = 0; i < r.FieldCount; i++)
							{
								var col = columnMapping.FirstOrDefault(c => c.ColumnName == r.GetName(i));
								if (col != null)
									col.UpdateValue(currentRow, r.GetValue(i).NullIfDBNull());
								else if (currentRow is LuaTable t)
									t[r.GetName(i)] = r.GetValue(i);
							}
						}
					} while (rowEnumerator.MoveNext());
				}
				yield break;
			} // func ExecuteUpsert

			private static void ExecuteUpsertAppendOnClause(StringBuilder commandText, PpsSqlColumnInfo col)
			{
				if (col.Nullable)
				{
					commandText.Append('(');
					commandText.Append("SRC.");
					col.AppendAsColumn(commandText);
					commandText.Append(" IS NULL AND ");
					commandText.Append("DST.");
					col.AppendAsColumn(commandText);
					commandText.Append(" IS NULL OR ");
					commandText.Append("SRC.");
					col.AppendAsColumn(commandText);
					commandText.Append(" = ");
					commandText.Append("DST.");
					col.AppendAsColumn(commandText);
					commandText.Append(')');
				}
				else
				{
					commandText.Append("SRC.");
					col.AppendAsColumn(commandText);
					commandText.Append(" = ");
					commandText.Append("DST.");
					col.AppendAsColumn(commandText);
				}
			} // proc ExecuteUpsertAppendOnClause

			#endregion

			#region -- ExecuteSimpleSelect --------------------------------------------------

			#region -- class DefaultRowEnumerable -------------------------------------------

			private sealed class DefaultRowEnumerable : IEnumerable<IDataRow>
			{
				private sealed class DefaultValueRow : DynamicDataRow
				{
					private readonly IDataRow current;
					private readonly LuaTable defaults;

					public DefaultValueRow(LuaTable defaults, IDataRow current)
					{
						this.defaults = defaults;
						this.current = current;
					} // ctor

					private object GetDefaultValue(IDataRow current, int index)
					{
						var memberName = current.Columns[index].Name;
						var value = defaults.GetMemberValue(memberName);

						if (Lua.RtInvokeable(value))
							return new LuaResult(Lua.RtInvoke(value, current))[0];
						else
							return value;
					} // func GetDefaultValue

					public override object this[int index] => current[index] ?? GetDefaultValue(current, index);

					public override bool IsDataOwner => current.IsDataOwner;
					public override IReadOnlyList<IDataColumn> Columns => current.Columns;
				} // class DefaultValueRow

				private sealed class DefaultRowEnumerator : IEnumerator<IDataRow>
				{
					private readonly LuaTable defaults;
					private readonly IEnumerator<IDataRow> enumerator;

					public DefaultRowEnumerator(LuaTable defaults, IEnumerator<IDataRow> enumerator)
					{
						this.defaults = defaults ?? throw new ArgumentNullException(nameof(defaults));
						this.enumerator = enumerator ?? throw new ArgumentNullException(nameof(enumerator));
					} // ctor

					public void Dispose() 
						=> enumerator.Dispose();

					public bool MoveNext()
						=> enumerator.MoveNext();

					public void Reset()
						=> enumerator.Reset();

					public IDataRow Current => new DefaultValueRow(defaults, enumerator.Current);
					object IEnumerator.Current => Current;
				} // class DefaultRowEnumerator

				private readonly LuaTable defaults;
				private readonly IEnumerable<IDataRow> rowEnumerable;

				public DefaultRowEnumerable(LuaTable defaults, IEnumerable<IDataRow> rowEnumerable)
				{
					this.defaults = defaults;
					this.rowEnumerable = rowEnumerable;
				} // ctor

				public IEnumerator<IDataRow> GetEnumerator()
					=> new DefaultRowEnumerator(defaults, rowEnumerable.GetEnumerator());

				IEnumerator IEnumerable.GetEnumerator() 
					=> GetEnumerator();
			} // class DefaultRowEnumerable

			#endregion

			private IEnumerable<IEnumerable<IDataRow>> ExecuteSimpleSelect(LuaTable parameter, string name, PpsDataTransactionExecuteBehavior behavior)
			{
				/*
				 * select @cols from @name where @args
				 */

				// collect tables
				var tableInfos = new SqlJoinExpression(SqlDataSource);
				tableInfos.Parse(name);

				var defaults = GetArguments(parameter.GetMemberValue("defaults"), false);
				
				using (var cmd = CreateCommand(parameter, CommandType.Text))
				{
					var first = true;
					var commandText = new StringBuilder("SELECT ");

					#region -- select List --
					var columnList = parameter.GetMemberValue("columnList");
					if (columnList == null) // no columns, simulate a select *
					{
						#region -- append select * --
						foreach (var table in tableInfos.GetTables())
						{
							foreach (var column in table.Table.Columns)
							{
								if (first)
									first = false;
								else
									commandText.Append(", ");

								tableInfos.AppendColumn(commandText, table, column);
							}
						}
						#endregion
					}
					else if (columnList is LuaTable t) // columns are definied in a table
					{
						#region -- append select columns --
						void AppendColumnFromTableKey(string columnName)
						{
							var (table, column) = tableInfos.FindColumn(columnName, defaults == null);
							if (column != null) // append table column
								tableInfos.AppendColumn(commandText, table, column);
							else // try append empty DbNull column
							{
								var field = DataSource.Application.GetFieldDescription(columnName, true);
								commandText.Append(PpsSqlColumnInfo.AppendSqlParameter(cmd, field).ParameterName);
							}
						} // proc AppendColumnFromTableKey

						foreach (var item in t.ArrayList.OfType<string>())
						{
							if (first)
								first = false;
							else
								commandText.Append(", ");
							AppendColumnFromTableKey(item);
						}

						foreach (var m in t.Members)
						{
							if (first)
								first = false;
							else
								commandText.Append(", ");

							AppendColumnFromTableKey(m.Key);

							commandText.Append(" AS [").Append(m.Value).Append(']');
						}
						#endregion
					}
					else if (columnList is IDataColumns forcedColumns) // column set is forced
					{
						#region -- append select columns --
						foreach (var col in forcedColumns.Columns)
						{
							if (first)
								first = false;
							else
								commandText.Append(", ");

							var (table, column) = col is IPpsColumnDescription ppsColumn
								? tableInfos.FindColumn(ppsColumn, defaults == null)
								: tableInfos.FindColumn(col.Name, defaults == null);

							if (column != null) // append table column
								tableInfos.AppendColumn(commandText, table, column);
							else // try append empty DbNull column
								commandText.Append(PpsSqlColumnInfo.AppendSqlParameter(cmd, col).ParameterName);

							commandText.Append(" AS [").Append(col.Name).Append(']');
						}
						#endregion
					}
					else
						throw new ArgumentException("Unknown columnList definition.");
					#endregion

					// append from
					commandText.Append(" FROM ");
					commandText.Append(tableInfos.EmitJoin());

					// get where arguments
					var args = GetArguments(parameter, 1, false);
					if (args != null)
					{
						commandText.Append(" WHERE ");
						first = true;
						foreach (var p in args.Members)
						{
							if (first)
								first = false;
							else
								commandText.Append(" AND ");

							var (table, column) = tableInfos.FindColumn((string)p.Key, true);
							var parm = column.AppendSqlParameter(cmd, value: p.Value);
							tableInfos.AppendColumn(commandText, table, column);
							commandText.Append(" = ")
								.Append(parm.ParameterName);
						}
					}
					else if (parameter.GetMemberValue("where") is string sqlWhere)
						commandText.Append(" WHERE ").Append(sqlWhere);

					cmd.CommandText = commandText.ToString();

					using (var r = ExecuteReaderCommand(cmd, behavior))
					{
						// return results
						if (r != null)
						{
							do
							{
								if (defaults != null)
									yield return new DefaultRowEnumerable(defaults, new DbRowReaderEnumerable(r));
								else
									yield return new DbRowReaderEnumerable(r);

								if (behavior == PpsDataTransactionExecuteBehavior.SingleResult)
									break;
							} while (r.NextResult());
						}
					} // using r
				}
			} // proc ExecuteSimpleSelect

			#endregion

			#region -- ExecuteSql -----------------------------------------------------------

			private static Regex regExSqlParameter = new Regex(@"\@(\w+)", RegexOptions.Compiled);

			private IEnumerable<IEnumerable<IDataRow>> ExecuteSql(LuaTable parameter, string name, PpsDataTransactionExecuteBehavior behavior)
			{
				/*
				 * sql is execute and the args are created as a parameter
				 */

				using (var cmd = CreateCommand(parameter, CommandType.Text))
				{
					cmd.CommandText = name;

					var args = GetArguments(parameter, 1, false);
					if (args != null)
					{
						foreach (Match m in regExSqlParameter.Matches(name))
						{
							var k = m.Groups[1].Value;
							var v = args.GetMemberValue(k, true);
							cmd.Parameters.Add(new SqlParameter("@" + k, v.NullIfDBNull()));
						}
					}

					// execute
					using (var r = ExecuteReaderCommand(cmd, behavior))
					{
						if (r != null)
						{
							do
							{
								yield return new DbRowReaderEnumerable(r);
							} while (r.NextResult());
						}
					}
				}
			} // func ExecuteSql

			#endregion

			#region -- ExecuteDelete --------------------------------------------------------

			private IEnumerable<IEnumerable<IDataRow>> ExecuteDelete(LuaTable parameter, string name, PpsDataTransactionExecuteBehavior behavior)
			{
				/*
				 * DELETE FROM name 
				 * OUTPUT
				 * WHERE Col = @Col
				 */

				// find the connected table
				var tableInfo = SqlDataSource.ResolveTableByName(name, true);

				using (var cmd = CreateCommand(parameter, CommandType.Text))
				{
					var commandText = new StringBuilder();

					commandText.Append("DELETE ")
						.Append(tableInfo.QuallifiedName);

					// default is that only one row is done
					var args = GetArguments(parameter, 1, true);

					// add primary key as out put
					commandText.Append(" OUTPUT ")
						.Append("deleted.")
						.Append(tableInfo.PrimaryKey.Name);

					// append where
					commandText.Append(" WHERE ");

					var first = true;
					foreach (var m in args.Members)
					{
						var column = tableInfo.FindColumn(m.Key, false);
						if (column == null)
							continue;

						if (first)
							first = false;
						else
							commandText.Append(" AND ");

						var columnName = column.Name;
						var parameterName = '@' + columnName;
						commandText.Append(columnName)
							.Append(" = ")
							.Append(parameterName);
						column.AppendSqlParameter(cmd, parameterName, m.Value);
					}

					if (first && args.GetOptionalValue("__all", false))
						throw new ArgumentException("To delete all rows, set __all to true.");

					cmd.CommandText = commandText.ToString();

					// execute delete
					using (var r = ExecuteReaderCommand(cmd, behavior))
					{
						// return results
						if (r != null)
						{
							do
							{
								yield return new DbRowReaderEnumerable(r);
								if (behavior == PpsDataTransactionExecuteBehavior.SingleResult)
									break;
							} while (r.NextResult());
						}
					}
				}
				yield break; // empty enumeration
			} // func ExecuteDelete

			#endregion

			protected override IEnumerable<IEnumerable<IDataRow>> ExecuteResult(LuaTable parameter, PpsDataTransactionExecuteBehavior behavior)
			{
				string name;
				if ((name = (string)parameter["execute"]) != null)
					return ExecuteCall(parameter, name, behavior);
				else if ((name = (string)parameter["insert"]) != null)
					return ExecuteInsert(parameter, name, behavior);
				else if ((name = (string)parameter["update"]) != null)
					return ExecuteUpdate(parameter, name, behavior);
				else if ((name = (string)parameter["delete"]) != null)
					return ExecuteDelete(parameter, name, behavior);
				else if ((name = (string)parameter["upsert"]) != null)
					return ExecuteUpsert(parameter, name, behavior);
				else if ((name = (string)parameter["select"]) != null)
					return ExecuteSimpleSelect(parameter, name, behavior);
				else if ((name = (string)parameter["sql"]) != null)
					return ExecuteSql(parameter, name, behavior);
				else
					throw new NotImplementedException();
			} // func ExecuteResult

			#endregion
			
			public PpsSqlExDataSource SqlDataSource => (PpsSqlExDataSource)base.DataSource;
			public SqlTransaction InternalTransaction => transaction;
		} // class SqlDataTransaction

		#endregion

		#region -- class SqlColumnInfo --------------------------------------------------

		private sealed class SqlColumnInfo : PpsSqlColumnInfo
		{
			private readonly int columnId;
			private readonly SqlDbType sqlType;
			private readonly string udtName;
	
			public SqlColumnInfo(PpsSqlTableInfo table, SqlDataReader r)
				: base(table, 
					  columnName: r.GetString(2), 
					  dataType: GetFieldType(r.GetByte(3)), 
					  maxLength: r.GetInt16(4),
					  precision: r.GetByte(5),
					  scale: r.GetByte(6),
					  isNullable: r.GetBoolean(7),
					  isIdentity: r.GetBoolean(8)
				)
			{
				this.columnId = r.GetInt32(1);
				var t = r.GetByte(3);
				this.sqlType = GetSqlType(t);
				if (t == 240)
					udtName = "geography";
				else
					udtName = null;
				EndInit();
			} // ctor

			protected override IEnumerator<PropertyValue> GetProperties()
			{
				using (var e = base.GetProperties())
				{
					while (e.MoveNext())
						yield return e.Current;
				}
				yield return new PropertyValue(nameof(SqlType), SqlType);
			} // func GetProperties

			protected override bool TryGetProperty(string propertyName, out object value)
			{
				if (!base.TryGetProperty(propertyName, out value))
				{
					if (String.Compare(propertyName, nameof(SqlType), StringComparison.OrdinalIgnoreCase) == 0)
					{
						value = SqlType;
						return true;
					}
				}

				value = null;
				return false;
			} // func TryGetProperty

			protected override void InitSqlParameter(DbParameter parameter, string parameterName, object value)
			{
				base.InitSqlParameter(parameter, parameterName, value);
				((SqlParameter)parameter).SqlDbType = sqlType;
				if (sqlType == SqlDbType.Udt)
					((SqlParameter)parameter).UdtTypeName = udtName;
			} // proc InitSqlParameter

			#region -- GetFieldType, GetSqlType -----------------------------------------------

			private static Type GetFieldType(byte systemTypeId)
			{
				switch (systemTypeId)
				{
					case 36: // uniqueidentifier  
						return typeof(Guid);

					case 40: // date
					case 41: // time
					case 42: // datetime2
					case 58: // smalldatetime
					case 61: // datetime
					case 189: // timestamp
						return typeof(DateTime);
					case 43: // datetimeoffset  
						return typeof(DateTimeOffset);

					case 48: // tinyint
						return typeof(byte);
					case 52: // smallint
						return typeof(short);
					case 56: // int
						return typeof(int);
					case 127: // bigint
						return typeof(long);
					case 59: // real
						return typeof(double);
					case 62: // float
						return typeof(double); // float seems to be a double
					case 98: // sql_variant
						return typeof(object);
					case 104: // bit
						return typeof(bool);

					case 60: // money
					case 106: // decimal
					case 108: // numeric
					case 122: // smallmoney
						return typeof(decimal);

					case 34: // image
					case 165: // varbinary
					case 173: // binary
						return typeof(byte[]);

					case 35: // text
					case 99: // ntext
					case 167: // varchar
					case 175: // char
					case 231: // nvarchar
					case 239: // nchar
						return typeof(string);

					case 240: // GEOGRAPHY
						return typeof(Microsoft.SqlServer.Types.SqlGeography);

					case 241: // xml
						return typeof(string);

					default:
						throw new IndexOutOfRangeException($"Unexpected sql server system type: {systemTypeId}");
				}
			} // func GetFieldType

			private static SqlDbType GetSqlType(byte systemTypeId)
			{
				switch (systemTypeId)
				{
					case 36: // uniqueidentifier  
						return SqlDbType.UniqueIdentifier;

					case 40: // date
						return SqlDbType.Date;
					case 41: // time
						return SqlDbType.Time;
					case 42: // datetime2
						return SqlDbType.DateTime2;
					case 58: // smalldatetime
						return SqlDbType.SmallDateTime;
					case 61: // datetime
						return SqlDbType.DateTime;
					case 189: // timestamp
						return SqlDbType.Timestamp;
					case 43: // datetimeoffset  
						return SqlDbType.DateTimeOffset;

					case 48: // tinyint
						return SqlDbType.TinyInt;
					case 52: // smallint
						return SqlDbType.SmallInt;
					case 56: // int
						return SqlDbType.Int;
					case 127: // bigint
						return SqlDbType.BigInt;
					case 59: // real
						return SqlDbType.Real;
					case 62: // float
						return SqlDbType.Float;
					case 98: // sql_variant
						return SqlDbType.Variant;
					case 104: // bit
						return SqlDbType.Bit;

					case 60: // money
						return SqlDbType.Money;
					case 106: // decimal
					case 108: // numeric
						return SqlDbType.Decimal;
					case 122: // smallmoney
						return SqlDbType.SmallMoney;

					case 34: // image
						return SqlDbType.Image;
					case 165: // varbinary
						return SqlDbType.VarBinary;
					case 173: // binary
						return SqlDbType.Binary;

					case 35: // text
						return SqlDbType.Text;
					case 99: // ntext
						return SqlDbType.NText;
					case 167: // varchar
						return SqlDbType.VarChar;
					case 175: // char
						return SqlDbType.Char;
					case 231: // nvarchar
						return SqlDbType.NVarChar;
					case 239: // nchar
						return SqlDbType.NChar;

					case 240: // GEOGRAPHY
						return SqlDbType.Udt;

					case 241: // xml
						return SqlDbType.Xml;

					default:
						throw new IndexOutOfRangeException($"Unexpected sql server system type: {systemTypeId}");
				}
			} // func GetSqlType

			#endregion

			public int ColumnId => columnId;
			public SqlDbType SqlType => sqlType;
		} // class SqlColumnInfo

		#endregion

		#region -- class SqlRelationInfo ------------------------------------------------

		private sealed class SqlRelationInfo : PpsSqlRelationInfo
		{
			private readonly int objectId;

			public SqlRelationInfo(int objectId, string name, SqlColumnInfo parentColumn, SqlColumnInfo referencedColumn)
				: base(name, parentColumn, referencedColumn)
			{
				this.objectId = objectId;
			} // ctor

			public int RelationId => objectId;
		} // class SqlRelationInfo

		#endregion

		#region -- class SqlTableInfo ---------------------------------------------------

		private sealed class SqlTableInfo : PpsSqlTableInfo
		{
			private readonly int objectId;
			private readonly int primaryColumnId;

			private SqlColumnInfo primaryKeyColumn = null;

			public SqlTableInfo(Func<int, int, SqlColumnInfo> resolveColumn, SqlDataReader r)
				: base(schemaName: r.GetString(1), tableName: r.GetString(2))
			{
				this.objectId = r.GetInt32(0);
				this.primaryColumnId = r.IsDBNull(3) ? -1 : r.GetInt32(3);
			} // ctor

			protected override void OnColumnAdded(PpsSqlColumnInfo column)
			{
				if (column is SqlColumnInfo t)
				{
					if (t.ColumnId == primaryColumnId)
						primaryKeyColumn = t;
				}
			} // proc OnColumnAdded

			public override bool IsPrimaryKeyColumn(PpsSqlColumnInfo column)
				=> column == primaryKeyColumn;

			public int TableId => objectId;
			public override PpsSqlColumnInfo PrimaryKey => primaryKeyColumn;
		} // class SqlTableInfo

		#endregion

		#region -- class SqlSynchronizationTransaction --------------------------------

		private sealed class SqlSynchronizationTransaction : PpsDataSynchronization
		{
			#region -- class SqlSynchronizationBatch ----------------------------------

			private sealed class SqlSynchronizationBatch : IPpsDataSynchronizationBatch
			{
				private readonly SqlCommand command;
				private readonly bool isFull;
				private readonly DbRowEnumerator reader;

				private long currentSyncId;

				public SqlSynchronizationBatch(long currentSyncId, SqlCommand command, bool isFull)
				{
					this.currentSyncId = currentSyncId;
					this.command = command ?? throw new ArgumentNullException(nameof(command));
					this.isFull = isFull;
					this.reader = new DbRowEnumerator(command.ExecuteReader(), true);
				} // ctor

				public void Dispose()
				{
					command.Dispose();
					reader.Dispose();
				} // proc Dispose

				public bool MoveNext()
				{
					var r = reader.MoveNext();
					if (r)
					{
						var t = reader.Current[1].ChangeType<long>();
						if (t > currentSyncId)
							currentSyncId = t;
					}
					return r;
				} // func MoveNext

				public void Reset()
					=> ((IEnumerator)reader).Reset();

				public IDataRow Current => reader.Current;
				object IEnumerator.Current => reader.Current;

				public IReadOnlyList<IDataColumn> Columns => reader.Columns;

				public long CurrentSyncId => currentSyncId;
				public char CurrentMode => reader.Current[0].ToString()[0];
				public bool IsFullSync => isFull;
			} // class SqlSynchronizationBatch

			#endregion

			private readonly long startCurrentSyncId;
			private readonly bool isForceFull;
			private readonly SqlTransaction transaction;

			#region -- Ctor/Dtor --------------------------------------------------------

			public SqlSynchronizationTransaction(PpsApplication application, PpsDataSource dataSource, IPpsPrivateDataContext privateDataContext, DateTime lastSynchronization)
				:base(application, dataSource.CreateConnection(privateDataContext, true), lastSynchronization)
			{
				((SqlConnectionHandle)Connection).EnsureConnectionAsync(true).AwaitTask();

				// create transaction
				this.transaction = SqlConnection.BeginTransaction(IsolationLevel.ReadCommitted);

				// get the current sync id
				using (var cmd = SqlConnection.CreateCommand())
				{
					cmd.CommandTimeout = 6000;
					cmd.Transaction = transaction;
					cmd.CommandText = "SELECT change_tracking_current_version(), create_date FROM sys.databases WHERE database_id = DB_ID()";

					using (var r = cmd.ExecuteReaderEx(CommandBehavior.SingleRow))
					{
						if (!r.Read())
							throw new InvalidOperationException();
						if (r.IsDBNull(0))
							throw new ArgumentException("Change tracking is not active in this database.");

						startCurrentSyncId = r.GetInt64(0); // get highest SyncId
						isForceFull = r.GetDateTime(1).ToUniversalTime() > lastSynchronization; // recreate database
					}
				}
			} // ctor

			protected override void Dispose(bool disposing)
			{
				if (disposing)
					transaction.Dispose();
				base.Dispose(disposing);
			} // proc Dispose

			#endregion

			private static void PrepareSynchronizationColumns(PpsDataTableDefinition table, StringBuilder command, string primaryKeyPrefix = null)
			{
				foreach (var col in table.Columns)
				{
					var colInfo = ((PpsDataColumnServerDefinition)col).GetColumnDescription<SqlColumnInfo>();
					if (colInfo != null)
					{
						if (primaryKeyPrefix != null && colInfo.IsPrimary)
							command.Append(',').Append(primaryKeyPrefix).Append('[');
						else
							command.Append(",d.[");
						command.Append(colInfo.Name).Append(']')
							.Append(" AS [").Append(col.Name).Append(']');
					}
				}

				// add revision hint
				if (table.Name == "ObjectTags")
				{
					command.Append(",CASE WHEN d.[ObjRId] IS NOT NULL THEN d.[Class] ELSE NULL END AS [LocalClass]");
					command.Append(",CASE WHEN d.[ObjRId] IS NOT NULL THEN d.[Value] ELSE NULL END AS [LocalValue]");
				}
			} // func PrepareSynchronizationColumns

			private string PrepareChangeTrackingCommand(PpsDataTableDefinition table, PpsSqlTableInfo tableInfo, PpsSqlColumnInfo columnInfo, long lastSyncId)
			{
				// build command string for change table
				var command = new StringBuilder("SELECT ct.sys_change_operation,ct.sys_change_version");

				PrepareSynchronizationColumns(table, command, "ct.");

				command.Append(" FROM ")
					.Append("changetable(changes ").Append(tableInfo.QuallifiedName).Append(',').Append(lastSyncId).Append(") as Ct ")
					.Append("LEFT OUTER JOIN ").Append(tableInfo.QuallifiedName)
					.Append(" as d ON d.").Append(columnInfo.Name).Append(" = ct.").Append(columnInfo.Name);

				return command.ToString();
			} // proc PrepareChangeTrackingCommand

			private string PrepareFullCommand(PpsDataTableDefinition table, PpsSqlTableInfo tableInfo)
			{
				var command = new StringBuilder("SELECT 'I',cast(" + startCurrentSyncId.ToString() + " as bigint)");

				PrepareSynchronizationColumns(table, command);

				command.Append(" FROM ")
					.Append(tableInfo.QuallifiedName)
					.Append(" as d");

				return command.ToString();
			} // proc PrepareFullCommand

			private IPpsDataSynchronizationBatch GenerateChangeTrackingBatch(PpsDataTableDefinition table, long lastSyncId)
			{
				var column = (PpsDataColumnServerDefinition)table.PrimaryKey;
				var columnInfo = column.GetColumnDescription<SqlColumnInfo>();
				if (columnInfo == null)
					throw new ArgumentOutOfRangeException("columnInfo", null, $"{column.Name} is not a sql column.");

				var tableInfo = columnInfo.Table;
				var isFull = isForceFull || lastSyncId < 0;

				// is the given syncId valid
				if (!isFull)
				{
					using (var getMinVersionCommand = SqlConnection.CreateCommand())
					{
						getMinVersionCommand.Transaction = transaction;
						getMinVersionCommand.CommandText = "SELECT change_tracking_min_valid_version(object_id('" + tableInfo.QuallifiedName + "'))";

						var minValidVersionValue = getMinVersionCommand.ExecuteScalar();
						if (minValidVersionValue == DBNull.Value)
							throw new ArgumentException($"Change tracking is not activated for '{tableInfo.QuallifiedName}'.");

						var minValidVersion = minValidVersionValue.ChangeType<long>();
						isFull = minValidVersion > lastSyncId;
					}
				}

				// generate the command
				var command = SqlConnection.CreateCommand();
				try
				{
					var commandText = isFull ?
						PrepareFullCommand(table, tableInfo) :
						PrepareChangeTrackingCommand(table, tableInfo, columnInfo, lastSyncId);

					if (table.Name == "ObjectTags") // special case for tags
						commandText += " LEFT OUTER JOIN dbo.ObjK o ON (o.Id = d.ObjKId) WHERE d.ObjRId is null OR (d.ObjRId = o.HeadRevId)"; // only no rev tags

					command.Transaction = transaction;
					command.CommandText = commandText;

					return new SqlSynchronizationBatch(startCurrentSyncId, command, isFull);
				}
				catch 
				{
					command.Dispose();
					throw;
				}
			} // proc GenerateChangeTrackingBatch

			public override IPpsDataSynchronizationBatch GenerateBatch(PpsDataTableDefinition table, string syncType, long lastSyncId)
			{
				ParseSynchronizationArguments(syncType, out var syncAlgorithm, out var syncArguments);

				if (String.Compare(syncAlgorithm, "TimeStamp", StringComparison.OrdinalIgnoreCase) == 0)
				{
					ParseSynchronizationTimeStampArguments(syncArguments, out var name, out var column);

					return CreateTimeStampBatchFromSelector(name, column, lastSyncId);
				}
				else if (String.Compare(syncAlgorithm, "ChangeTracking", StringComparison.OrdinalIgnoreCase) == 0)
				{
					return GenerateChangeTrackingBatch(table, lastSyncId);
				}
				else
				{
					throw new ArgumentException(String.Format("Unsupported sync algorithm: {0}", syncAlgorithm));
				}
			} // func GenerateBatch

			private SqlConnection SqlConnection => ((SqlConnectionHandle)base.Connection).Connection;
		} // class SqlSynchronizationTransaction

		#endregion

		private readonly PpsApplication application;
		private readonly SqlConnection masterConnection;
		private string lastReadedConnectionString = String.Empty;
		private readonly ManualResetEventSlim schemInfoInitialized = new ManualResetEventSlim(false);
		private DEThread databaseMainThread = null;

		private readonly List<SqlConnectionHandle> currentConnections = new List<SqlConnectionHandle>();

		private readonly Dictionary<string, SqlTableInfo> tableStore = new Dictionary<string, SqlTableInfo>(StringComparer.OrdinalIgnoreCase);
		private readonly Dictionary<string, SqlColumnInfo> columnStore = new Dictionary<string, SqlColumnInfo>(StringComparer.OrdinalIgnoreCase);

		#region -- Ctor/Dtor/Config -------------------------------------------------------

		public PpsSqlExDataSource(IServiceProvider sp, string name)
			: base(sp, name)
		{
			// must be in a ppsn element
			application = sp.GetService<PpsApplication>(true);

			masterConnection = new SqlConnection();
		} // ctor

		protected override void Dispose(bool disposing)
		{
			try
			{
				if (disposing)
				{
					schemInfoInitialized.Dispose();

					// finish the connection
					Procs.FreeAndNil(ref databaseMainThread);
					masterConnection.Dispose();
				}
			}
			finally
			{
				base.Dispose(disposing);
			}
		} // proc Dispose

		protected override void OnBeginReadConfiguration(IDEConfigLoading config)
		{
			base.OnBeginReadConfiguration(config);

			// read the connection string
			var connectionString = config.ConfigNew.Element(PpsStuff.PpsNamespace + "connectionString")?.Value;
			if (String.IsNullOrEmpty(connectionString))
				throw new DEConfigurationException(config.ConfigNew, "<connectionString> is empty.");

			if (lastReadedConnectionString != connectionString)
			{
				Procs.FreeAndNil(ref databaseMainThread);

				config.Tags.SetProperty("LastConStr", connectionString);
				config.Tags.SetProperty("ConStr", CreateConnectionStringBuilder(connectionString, "DES_Master"));
			}
		} // proc OnBeginReadConfiguration

		protected override void OnEndReadConfiguration(IDEConfigLoading config)
		{
			base.OnEndReadConfiguration(config);

			// update connection string
			var connectionStringBuilder = (SqlConnectionStringBuilder)config.Tags.GetProperty("ConStr", (object)null);

			if (connectionStringBuilder != null)
			{
				lock (masterConnection)
				{
					// close the current connection
					masterConnection.Close();

					// use integrated security by default
					connectionStringBuilder.IntegratedSecurity = true;

					// set the new connection
					masterConnection.ConnectionString = connectionStringBuilder.ToString();
					lastReadedConnectionString = config.Tags.GetProperty("LastConStr", String.Empty);

					// start background thread
					application.RegisterInitializationTask(10000, "Database", async () => await Task.Run(new Action(schemInfoInitialized.Wait))); // block all other tasks
					databaseMainThread = new DEThread(this, "Database", () => ExecuteDatabaseAsync());
				}
			}
		} // proc OnEndReadConfiguration

		#endregion

		private string GetResourceScript(string resourceName)
		{
			// todo: namespace beachten und assembly

			using (var src = typeof(PpsSqlExDataSource).Assembly.GetManifestResourceStream(typeof(PpsSqlExDataSource), "tsql." + resourceName))
			{
				if (src == null)
					throw new ArgumentException($"{resourceName} not found.");

				using (var sr = new StreamReader(src, Encoding.UTF8, true))
					return sr.ReadToEnd();
			}
		} // func GetResourceScript

		#region -- Initialize Schema ------------------------------------------------------

		private void InitializeSchema()
		{
			lock (schemInfoInitialized)
			{
				using (var cmd = masterConnection.CreateCommand())
				{
					cmd.CommandType = CommandType.Text;
					cmd.CommandText = GetResourceScript("ConnectionInitScript.sql");

					// read all tables
					using (var r = cmd.ExecuteReader(CommandBehavior.Default))
					{
						while (r.Read())
						{
							try
							{
								var t = new SqlTableInfo(ResolveColumnById, r);
								tableStore.Add(t.SchemaName + "." + t.TableName, t);
							}
							catch (Exception e)
							{
								Log.Except($"Table initialization failed: {r.GetValue(1)}.{r.GetValue(2)}", e);
							}
						}

						if (!r.NextResult())
							throw new InvalidOperationException();

						// read all columns of the tables
						while (r.Read())
						{
							try
							{
								var c = new SqlColumnInfo(ResolveTableById(r.GetInt32(0)), r);
								columnStore.Add(c.TableColumnName, c);
							}
							catch (Exception e)
							{
								Log.Except($"Column initialization failed: {r.GetValue(2)}", e);
							}
						}

						if (!r.NextResult())
							throw new InvalidOperationException();

						// read all relations between the tables
						while (r.Read())
						{
							var tableInfo = ResolveTableById(r.GetInt32(2)); // table
							if (tableInfo != null)
							{
								var parentColumn = ResolveColumnById(tableInfo.TableId, r.GetInt32(3));
								var referencedColumn = ResolveColumnById(r.GetInt32(4), r.GetInt32(5));

								tableInfo.AddRelation(new SqlRelationInfo(r.GetInt32(0), r.GetString(1), parentColumn, referencedColumn));
							}
						}
					}
				}

				// Register Server logins
				application.RegisterView(SqlDataSelectorToken.CreateFromResource(this, "dbo.serverLogins", "ServerLogins.sql"));

				// done
				schemInfoInitialized.Set();
			}
		} // proc InitializeSchema

		private SqlTableInfo ResolveTableById(int tableId)
		{
			foreach (var t in tableStore.Values)
				if (t.TableId == tableId)
					return t;
			return null;
		} // func ResolveTableById

		private SqlColumnInfo ResolveColumnById(int tableId, int columnId)
		{
			foreach (var c in columnStore.Values)
				if (((SqlTableInfo)c.Table).TableId == tableId && c.ColumnId == columnId)
					return c;
			return null;
		} // func ResolveColumnById

		private SqlColumnInfo ResolveColumnByName(string key)
		{
			// todo: throwException, full name vs. name only
			SqlColumnInfo column;
			if (columnStore.TryGetValue(key, out column))
				return column;
			throw new ArgumentException($"Column '{key}' not found.", "key");
		} // func ResolveColumnByName

		private SqlTableInfo ResolveTableByName(string name, bool throwException = false)
		{
			var tableInfo = tableStore[name];
			if (tableInfo == null && throwException)
				throw new ArgumentNullException("name", $"Table '{name}' is not defined.");
			return tableInfo;
		} // func ResolveTableByName

		#endregion

		#region -- Execute Database -------------------------------------------------------

		private async Task ExecuteDatabaseAsync()
		{
			var lastChangeTrackingId = -1L;

			while (databaseMainThread.IsRunning)
			{
				var executeStartTick = Environment.TickCount;
				try
				{
					try
					{
						// reset connection
						if (masterConnection.State == ConnectionState.Broken)
						{
							Log.Warn("Reset connection.");
							masterConnection.Close();
						}

						// open connection
						if (masterConnection.State == ConnectionState.Closed)
						{
							Log.Info("Open database connection.");
							await masterConnection.OpenAsync();
						}

						// execute background task
						if (masterConnection.State == ConnectionState.Open)
						{
							if (!schemInfoInitialized.IsSet)
								await Task.Run(new Action(InitializeSchema));

							// check for change tracking
							using (var cmd = masterConnection.CreateCommand())
							{
								cmd.CommandText = "SELECT change_tracking_current_version()";
								var r = await cmd.ExecuteScalarAsync();
								if(r is long l)
								{
									if (lastChangeTrackingId == -1L)
										lastChangeTrackingId = l;
									else if (lastChangeTrackingId != l)
									{
										lastChangeTrackingId = l;

										// notify clients, something has changed
										application.FireDataChangedEvent(Name);
									}
								}
							}
						}
					}
					catch (Exception e)
					{
						Log.Except(e); // todo: detect disconnect
					}
				}
				finally
				{
					// delay at least 1 Sekunde
					await Task.Delay(Math.Max(1000 - Math.Abs(Environment.TickCount - executeStartTick), 0));
				}
			}
		} // proc ExecuteDatabaseAsync

		#endregion

		#region -- Master Connection Service ----------------------------------------------

		internal IDisposable UseMasterConnection(out SqlConnection connection)
		{
			connection = masterConnection;
			return null;
		} // func UseMasterConnection

		#endregion

		public override Task<IPpsSelectorToken> CreateSelectorTokenAsync(string name, XElement sourceDescription)
			=> Task.Run(new Func<IPpsSelectorToken>(() => SqlDataSelectorToken.CreateFromXml(this, name, sourceDescription)));

		public override IPpsColumnDescription GetColumnDescription(string columnName, bool throwException)
		{
			if (columnStore.TryGetValue(columnName, out var column))
				return column;
			else if (throwException)
				throw new ArgumentException($"Could not resolve column {columnName} to source {Name}.", columnName);
			else
				return null;
		} // func GetColumnDescription

		private SqlConnectionHandle GetSqlConnection(IPpsConnectionHandle connection, bool throwException)
			=> (SqlConnectionHandle)connection;

		/// <summary>Creates a user specific connection.</summary>
		/// <param name="privateUserData"></param>
		/// <returns></returns>
		public override IPpsConnectionHandle CreateConnection(IPpsPrivateDataContext privateUserData, bool throwException = true)
		{
			lock (currentConnections)
			{
				var c = new SqlConnectionHandle(this, CreateConnectionStringBuilder(lastReadedConnectionString, "User"), privateUserData);
				currentConnections.Add(c);
				return c;
			}
		} // func IPpsConnectionHandle

		public override PpsDataTransaction CreateTransaction(IPpsConnectionHandle connection)
		{
			var c = GetSqlConnection(connection, true);
			return new SqlDataTransaction(this, c);
		} // func CreateTransaction

		public override PpsDataSynchronization CreateSynchronizationSession(IPpsPrivateDataContext privateUserData, DateTime lastSynchronization)
			=> new SqlSynchronizationTransaction(application, this, privateUserData, lastSynchronization);

		public bool IsConnected
		{
			get
			{
				lock (masterConnection)
					return IsConnectionOpen(masterConnection);
			}
		} // prop IsConnected

		public override string Type => "mssql";

		// -- Static --------------------------------------------------------------

		private static SqlConnectionStringBuilder CreateConnectionStringBuilder(string connectionString, string applicationName)
			=> new SqlConnectionStringBuilder(connectionString)
			{
				// remove password, and connection information
				Password = String.Empty,
				UserID = String.Empty,
				IntegratedSecurity = false,

				ApplicationName = applicationName, // add a name
				MultipleActiveResultSets = true // activate MARS
			};

		private static bool IsConnectionOpen(SqlConnection connection)
			=> connection.State != System.Data.ConnectionState.Closed;

		internal static SqlConnection GetSqlConnection(IPpsConnectionHandle connection)
			=> connection is SqlConnectionHandle c
				? c.Connection
				: null;

		internal static SqlCommand CreateSqlCommand(PpsDataTransaction trans, CommandType commandType, bool noTransaction)
			=> trans is SqlDataTransaction t ? t.CreateCommand(commandType, noTransaction) : throw new ArgumentException(nameof(trans));

		#region -- DataTable - Helper -----------------------------------------------------

		private static T GetDataRowValue<T>(DataRow row, string columnName, T @default)
		{
			var r = row[columnName];
			if (r == DBNull.Value)
				return @default;

			try
			{
				return r.ChangeType<T>();
			}
			catch
			{
				return @default;
			}
		} // func GetDataRowValue

		#endregion
	} // PpsSqlExDataSource
}