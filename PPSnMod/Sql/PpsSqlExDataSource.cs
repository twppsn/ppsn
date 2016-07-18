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
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Principal;
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
	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PpsSqlExDataSource : PpsSqlDataSource
	{
		#region -- class SqlConnectionHandle ----------------------------------------------

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
				Disposed?.Invoke(this, EventArgs.Empty);
			}

			private static bool Connect(SqlConnectionStringBuilder connectionString, SqlConnection connection, IPpsPrivateDataContext identity, bool throwException)
			{
				// create the connection
				try
				{
					if (identity.SystemIdentity == null) // use network credentials
					{
						connectionString.IntegratedSecurity = false;
						connection.ConnectionString = connectionString.ToString();

						var altLogin = identity.AlternativeCredential;
						if (altLogin == null)
							throw new ArgumentException("User has no sql-login data.");

						connection.Credential = new SqlCredential(altLogin.UserName, altLogin.SecurePassword);
						connection.Open();
					}
					else
					{
						connectionString.IntegratedSecurity = true;
						connection.ConnectionString = connectionString.ToString();

						//using (identity.SystemIdentity.Impersonate()) todo: Funktioniert nur als ADMIN?
							connection.Open();
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

			public SqlConnection ForkConnection()
			{
				// create a new connection
				var con = new SqlConnection();
				var conStr = new SqlConnectionStringBuilder(connectionString.ToString());
				conStr.ApplicationName = "User_Trans";
				conStr.Pooling = true;

				// ensure connection
				Connect(conStr, con, identity, true);

				return con;
			} // func ForkConnection

			public bool EnsureConnection(bool throwException)
			{
				if (IsConnected)
					return true;

				return Connect(connectionString, connection, identity, throwException);
			} // func EnsureConnection

			public PpsDataSource DataSource => dataSource;
			public SqlConnection Connection => connection;

			public bool IsConnected => IsConnectionOpen(connection);
		} // class SqlConnectionHandle

		#endregion

		#region -- class PpsDataResultColumnDescription -----------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary>Simple column description implementation.</summary>
		private sealed class PpsDataResultColumnDescription : PpsColumnDescription
		{
			#region -- class PpsDataResultColumnAttributes ----------------------------------

			private sealed class PpsDataResultColumnAttributes : PpsColumnDescriptionAttributes<PpsDataResultColumnDescription>
			{
				public PpsDataResultColumnAttributes(PpsDataResultColumnDescription owner)
					: base(owner)
				{
				} // ctor

				public override bool TryGetProperty(string name, out object value)
				{
					if (String.Compare(name, "MaxLength", StringComparison.OrdinalIgnoreCase) == 0)
					{
						value = GetDataRowValue(Owner.row, "ColumnSize", 0);
						return true;
					}
					else
					{
						foreach (DataColumn c in Owner.row.Table.Columns)
						{
							if (String.Compare(c.ColumnName, name, StringComparison.OrdinalIgnoreCase) == 0)
							{
								value = Owner.row[c];
								return value != DBNull.Value;
							}
						}
					}
					return base.TryGetProperty(name, out value);
				} // func TryGetProperty

				public override IEnumerator<PropertyValue> GetEnumerator()
				{
					foreach (DataColumn c in Owner.row.Table.Columns)
						yield return new PropertyValue(c.ColumnName, Owner.row[c]);

					using (var e = base.GetEnumerator())
					{
						while (e.MoveNext())
							yield return e.Current;
					}
				} // func GetEnumerator
			} // class PpsDataResultColumnAttributes

			#endregion

			private readonly DataRow row;

			public PpsDataResultColumnDescription(IPpsColumnDescription parent, DataRow row, string name, Type dataType)
				: base(parent, name, dataType)
			{
				this.row = row;
			} // ctor

			protected override IPropertyEnumerableDictionary CreateAttributes()
				=> new PpsDataResultColumnAttributes(this);
		} // class PpsDataResultColumnDescription

		#endregion

		#region -- class SqlDataSelectorToken ---------------------------------------------

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

		#region -- class SqlDataSelector --------------------------------------------------

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

				string newSelectList = AddSelectList(addSelectList);
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

				string newWhereCondition = AddWhereCondition(addWhereCondition);
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

				string newOrderBy = AddOrderBy(addOrderBy);
				return new SqlDataSelector(connection, selectorToken, selectList, whereCondition, newOrderBy);
			} // func SqlOrderBy

			public override IEnumerator<IDataRow> GetEnumerator(int start, int count)
			{
				SqlCommand cmd = null;
				try
				{
					cmd = new SqlCommand();
					cmd.Connection = connection.Connection;
					cmd.CommandType = CommandType.Text;

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

		#region -- class SqlResultInfo ----------------------------------------------------

		private sealed class SqlResultInfo : List<Func<SqlDataReader, IEnumerable<IDataRow>>>
		{
		} // class SqlResultInfo

		#endregion

		#region -- class SqlDataTransaction -----------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class SqlDataTransaction : PpsDataTransaction
		{
			private readonly SqlConnection connection;
			private readonly SqlTransaction transaction;

			#region -- Ctor/Dtor ------------------------------------------------------------

			public SqlDataTransaction(PpsSqlDataSource dataSource, SqlConnection connection)
				: base(dataSource)
			{
				this.connection = connection;

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
				transaction.Commit();
				base.Commit();
			} // proc Commit

			public override void Rollback()
			{
				try
				{
					transaction.Rollback();
				}
				finally
				{
					base.Rollback();
				}
			} // proc Rollback

			#endregion

			#region -- Execute Result -------------------------------------------------------

			private SqlCommand CreateCommand(LuaTable parameter, CommandType commandType)
			{
				var cmd = connection.CreateCommand();
				cmd.Connection = connection;
				cmd.Transaction = parameter.GetOptionalValue("__notrans", false) ? null : transaction;
				return cmd;
			} // func CreateCommand

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
						var args = parameter[1] as LuaTable;
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
				var tableInfo = SqlDataSource.ResolveTableByName(name);
				if (tableInfo == null)
					throw new ArgumentNullException("insert", $"Table '{name}' is not defined.");
				
				using (var cmd = CreateCommand(parameter, CommandType.Text))
				{
					var commandText = new StringBuilder();
					var variableList = new StringBuilder();
					var insertedList = new StringBuilder();

					commandText.Append("INSERT INTO ")
						.Append(tableInfo.FullName);

					// default is that only one row is done
					var args = parameter[1] as LuaTable;
					if (args == null)
						throw new ArgumentNullException("parameter[1]", "No arguments defined.");

					commandText.Append(" (");

					// create the column list
					var first = true;
					foreach (var column in tableInfo.Columns)
					{
						var columnName = column.ColumnName;

						var value = args[columnName];
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
							commandText.Append(columnName);
							variableList.Append(parameterName);
							cmd.Parameters.Add(column.CreateSqlParameter(parameterName, value));
						}

						if (insertedList.Length > 0)
							insertedList.Append(',');
						insertedList.Append("inserted.").Append(columnName);
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
				var tableInfo = SqlDataSource.ResolveTableByName(name);
				if (tableInfo == null)
					throw new ArgumentNullException("update", $"Table '{name}' is not defined.");

				using (var cmd = CreateCommand(parameter, CommandType.Text))
				{
					var commandText = new StringBuilder();
					var insertedList = new StringBuilder();

					commandText.Append("UPDATE ")
						.Append(tableInfo.FullName);

					// default is that only one row is done
					var args = parameter[1] as LuaTable;
					if (args == null)
						throw new ArgumentNullException("parameter[1]", "No arguments defined.");

					commandText.Append(" SET ");

					// create the column list
					var first = true;
					foreach (var column in tableInfo.Columns)
					{
						var columnName = column.ColumnName;
						var value = args[columnName];
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

						cmd.Parameters.Add(column.CreateSqlParameter(parameterName, value));

						insertedList.Append("inserted.").Append(columnName);
					}

					// generate output clause
					commandText.Append(" output ").Append(insertedList);

					// where
					var primaryKeyName = tableInfo.PrimaryKey.ColumnName;
					var primaryKeyValue = args[primaryKeyName];
					if (primaryKeyValue == null)
						throw new ArgumentException("Invalid primary key.");

					commandText.Append(" WHERE ")
						.Append(primaryKeyName)
						.Append(" = ")
						.Append("@").Append(primaryKeyName);
					cmd.Parameters.Add(tableInfo.PrimaryKey.CreateSqlParameter("@" + primaryKeyName, primaryKeyValue));
					
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

			private IEnumerable<IEnumerable<IDataRow>> ExecuteUpsert(LuaTable parameter, string name, PpsDataTransactionExecuteBehavior behavior)
			{
				throw new NotImplementedException();
			} // func ExecuteUpsert

			#endregion

			#region -- ExecuteSql -----------------------------------------------------------

			private IEnumerable<IEnumerable<IDataRow>> ExecuteSql(LuaTable parameter, string name, PpsDataTransactionExecuteBehavior behavior)
			{
				/*
				 * sql is execute and the args are created as a parameter
				 */

				using (var cmd = CreateCommand(parameter, CommandType.Text))
				{
					cmd.CommandText = name;

					var args = parameter[1] as LuaTable;
					if (args != null)
					{
						foreach (var kv in args.Members)
						{
							var parameterName = "@" + kv.Key;
							cmd.Parameters.Add(new SqlParameter(parameterName, kv.Value));
						}
					}

					// execute
					using (var r = ExecuteReaderCommand(cmd, behavior))
					{
						do
						{
							yield return new DbRowReaderEnumerable(r);
						} while (r.NextResult());
					}	
				}
			} // func ExecuteSql

			#endregion

			#region -- ExecuteDelete --------------------------------------------------------

			private IEnumerable<IEnumerable<IDataRow>> ExecuteDelete(LuaTable parameter, string name, PpsDataTransactionExecuteBehavior behavior)
			{
				throw new NotImplementedException();
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
				else if ((name = (string)parameter["sql"]) != null)
					return ExecuteSql(parameter, name, behavior);
				else
					throw new NotImplementedException();
			} // func ExecuteResult

			#endregion

			public PpsSqlExDataSource SqlDataSource => (PpsSqlExDataSource)base.DataSource;
		} // class SqlDataTransaction

		#endregion

		#region -- class SqlRelationInfo --------------------------------------------------

		// todo: new name, new position
		internal sealed class SqlRelationInfo
		{
			private readonly int objectId;
			private readonly string name;
			private readonly SqlColumnInfo parentColumn;
			private readonly SqlColumnInfo referencedColumn;

			public SqlRelationInfo(int objectId, string name, SqlColumnInfo parentColumn, SqlColumnInfo referencedColumn)
			{
				this.objectId = objectId;
				this.name = name;
				this.parentColumn = parentColumn;
				this.referencedColumn = referencedColumn;
			} // ctor

			public int RelationId => objectId;
			public string Name => name;
			public SqlColumnInfo ParentColumn => parentColumn;
			public SqlColumnInfo ReferncedColumn => referencedColumn;
		} // class SqlRelationInfo

		#endregion

		#region -- class SqlTableInfo -----------------------------------------------------

		// todo: new name, new position
		internal sealed class SqlTableInfo
		{
			private readonly int objectId;
			private readonly string schema;
			private readonly string name;
			private readonly int primaryColumnId;
			private readonly List<SqlColumnInfo> columns = new List<SqlColumnInfo>();
			private readonly List<SqlRelationInfo> relationInfo = new List<SqlRelationInfo>();
			private readonly Lazy<SqlColumnInfo> primaryColumn;

			public SqlTableInfo(Func<int, int, SqlColumnInfo> resolveColumn, SqlDataReader r)
			{
				this.objectId = r.GetInt32(0);
				this.schema = r.GetString(1);
				this.name = r.GetString(2);
				this.primaryColumnId = r.IsDBNull(3) ? -1 : r.GetInt32(3);

				this.primaryColumn = primaryColumnId == -1 ? null : new Lazy<SqlColumnInfo>(() => resolveColumn(objectId, primaryColumnId));
			} // ctor

			internal void AddColumn(SqlColumnInfo column)
			{
				columns.Add(column);
			} // proc AddColumn

			internal void AddRelation(SqlRelationInfo sqlRelationInfo)
			{
				relationInfo.Add(sqlRelationInfo);
			} // proc AddRelation
			
			public int TableId => objectId;
			public string Schema => schema;
			public string Name => name;
			public string FullName => schema + "." + name;

			public SqlColumnInfo PrimaryKey => primaryColumn?.Value;
			public IEnumerable<SqlColumnInfo> Columns => columns;

			public IEnumerable<SqlRelationInfo> RelationInfo => relationInfo;
		} // class SqlTableInfo

		#endregion

		#region -- class SqlColumnInfo ----------------------------------------------------

		// todo: new name, new position
		internal sealed class SqlColumnInfo : PpsColumnDescription
		{
			#region -- class PpsColumnAttributes --------------------------------------------

			private sealed class PpsColumnAttributes : PpsColumnDescriptionAttributes<SqlColumnInfo>
			{
				public PpsColumnAttributes(SqlColumnInfo owner) 
					: base(owner)
				{
				} // ctor

				public override bool TryGetProperty(string name, out object value)
				{
					switch (name[0])
					{
						case 'S':
						case 's':
							if (String.Compare(name, nameof(Owner.SqlType), StringComparison.OrdinalIgnoreCase) == 0)
							{
								value = Owner.SqlType;
								return true;
							}
							else if (String.Compare(name, nameof(Owner.SqlType), StringComparison.OrdinalIgnoreCase) == 0)
							{
								value = Owner.Scale;
								return true;
							}
							break;
						case 'M':
						case 'm':
							if (String.Compare(name, nameof(Owner.MaxLength), StringComparison.OrdinalIgnoreCase) == 0)
							{
								value = Owner.MaxLength;
								return true;
							}
							break;
						case 'P':
						case 'p':
							if (String.Compare(name, nameof(Owner.Precision), StringComparison.OrdinalIgnoreCase) == 0)
							{
								value = Owner.Precision;
								return true;
							}
							break;
						case 'I':
						case 'i':
							if (String.Compare(name, nameof(Owner.IsIdentity), StringComparison.OrdinalIgnoreCase) == 0)
							{
								value = Owner.IsIdentity;
								return true;
							}
							else if (String.Compare(name, nameof(Owner.IsNull), StringComparison.OrdinalIgnoreCase) == 0)
							{
								value = Owner.IsNull;
								return true;
							}
							break;
					}
					return base.TryGetProperty(name, out value);
				} // func TryGetProperty

				public override IEnumerator<PropertyValue> GetEnumerator()
				{
					yield return new PropertyValue(nameof(Owner.SqlType), Owner.SqlType);
					yield return new PropertyValue(nameof(Owner.MaxLength), Owner.MaxLength);
					yield return new PropertyValue(nameof(Owner.Precision), Owner.Precision);
					yield return new PropertyValue(nameof(Owner.Scale), Owner.Scale);
					yield return new PropertyValue(nameof(Owner.IsNull), Owner.IsNull);
					yield return new PropertyValue(nameof(Owner.IsIdentity), Owner.IsIdentity);

					using (var e = base.GetEnumerator())
					{
						while (e.MoveNext())
							yield return e.Current;
					}
				} // func GetEnumerator
			} // class PpsColumnAttributes

			#endregion

			private readonly SqlTableInfo table;
			private readonly int columnId;
			private readonly string columnName;
			private readonly SqlDbType sqlType;
			private readonly int maxLength;
			private readonly byte precision;
			private readonly byte scale;
			private readonly bool isNull;
			private readonly bool isIdentity;

			public SqlColumnInfo(SqlTableInfo table, SqlDataReader r)
				:base(null, table.Schema + "." + table.Name + "." + r.GetString(2), GetFieldType(r.GetByte(3)))
			{
				this.table = table;
				this.columnId = r.GetInt32(1);
				this.columnName = r.GetString(2);
				this.sqlType = GetSqlType(r.GetByte(3));
				this.maxLength = r.GetInt16(4);
				this.precision = r.GetByte(5);
				this.scale = r.GetByte(6);
				this.isNull = r.GetBoolean(7);
				this.isIdentity = r.GetBoolean(8);

				this.table.AddColumn(this);
			} // ctor

			protected override IPropertyEnumerableDictionary CreateAttributes()
				=> new PpsColumnAttributes(this);

			public SqlParameter CreateSqlParameter(string parameterName, object value)
				=> new SqlParameter(parameterName, sqlType, maxLength, ParameterDirection.Input, isNull, precision, scale, Name, DataRowVersion.Current, Procs.ChangeType(value, DataType));

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
						return typeof(float);
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

					case 241: // xml
						return SqlDbType.Xml;

					default:
						throw new IndexOutOfRangeException($"Unexpected sql server system type: {systemTypeId}");
				}
			} // func GetSqlType

			#endregion

			public SqlTableInfo Table => table;
			public int ColumnId => columnId;
			public string ColumnName => columnName;
			public string TableColumnName => table.Name + "." + columnName;
			public SqlDbType SqlType => sqlType;
			public int MaxLength => maxLength;
			public int Precision => precision;
			public int Scale => scale;
			public bool IsNull => isNull;
			public bool IsIdentity => isIdentity;
		} // class SqlColumnInfo

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
					databaseMainThread = new DEThread(this, "Database", ExecuteDatabase);
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
								tableStore.Add(t.FullName, t);
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
								columnStore.Add(c.Name, c);
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
							var tableInfo = ResolveTableById( r.GetInt32(2)); // table
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
				if (c.Table.TableId == tableId && c.ColumnId == columnId)
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

		private SqlTableInfo ResolveTableByName(string name)
		{
			// todo: throwException, with or without schema
			return tableStore[name];
		} // func ResolveTableByName

		#endregion

		#region -- Execute Database -------------------------------------------------------

		private void ExecuteDatabase()
		{
			var executeStartTick = Environment.TickCount;

			try
			{
				lock (masterConnection)
				{
					try
					{
						// reset connection
						if (masterConnection.State == System.Data.ConnectionState.Broken)
						{
							Log.Warn("Reset connection.");
							masterConnection.Close();
						}

						// open connection
						if (masterConnection.State == System.Data.ConnectionState.Closed)
						{
							Log.Info("Open database connection.");
							masterConnection.Open();
						}

						// execute background task
						if (masterConnection.State == ConnectionState.Open)
						{
							if (!schemInfoInitialized.IsSet)
								InitializeSchema();

						}
					}
					catch (Exception e)
					{
						Log.Except(e); // todo: detect disconnect
					}
				}
			}
			finally
			{
				// Mindestens eine Sekunde
				databaseMainThread.WaitFinish(Math.Max(1000 - Math.Abs(Environment.TickCount - executeStartTick), 0));
			}
		} // proc ExecuteDatabase

		#endregion

		#region -- Master Connection Service ----------------------------------------------

		internal IDisposable UseMasterConnection(out SqlConnection connection)
		{
			connection = masterConnection;
			return null;
		} // func UseMasterConnection

		#endregion

		public override Task<IPpsSelectorToken> CreateSelectorTokenAsync(string name, XElement sourceDescription)
		{
			var selectorToken = Task.Run(new Func<IPpsSelectorToken>(() => SqlDataSelectorToken.CreateFromXml(this, name, sourceDescription)));
			return selectorToken;
		} // func CreateSelectorTokenAsync

		public override IPpsColumnDescription GetColumnDescription(string columnName)
		{
			SqlColumnInfo column;
			return columnStore.TryGetValue(columnName, out column) ? column : null;
		} // func GetColumnDescription

		private SqlConnectionHandle GetSqlConnection(IPpsConnectionHandle connection, bool throwException)
		{
			return (SqlConnectionHandle)connection;
		} // func GetSqlConnection

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
			return new SqlDataTransaction(this, c.ForkConnection());
		} // func CreateTransaction

		public override PpsDataSetServerDefinition CreateDocumentDescription(IServiceProvider sp, string documentName, XElement config, DateTime configurationStamp)
			=> new PpsSqlDataSetDefinition(sp, this, documentName, config, configurationStamp);

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
		{
			var connectionStringBuilder = new SqlConnectionStringBuilder(connectionString);

			// remove password, and connection information
			connectionStringBuilder.Password = String.Empty;
			connectionStringBuilder.UserID = String.Empty;
			connectionStringBuilder.IntegratedSecurity = false;

			connectionStringBuilder.ApplicationName = applicationName; // add a name
			connectionStringBuilder.MultipleActiveResultSets = true; // activate MARS

			return connectionStringBuilder;
		} // func CreateConnectionStringBuilder

		private static bool IsConnectionOpen(SqlConnection connection)
			=> connection.State != System.Data.ConnectionState.Closed;

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
