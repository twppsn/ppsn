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
	#region -- class PpsSqlColumnInfo -------------------------------------------------

	/// <summary>Column information</summary>
	public abstract class PpsSqlColumnInfo : PpsColumnDescription
	{
		#region -- class PpsColumnAttributes ------------------------------------------

		private sealed class PpsColumnAttributes : IPropertyEnumerableDictionary
		{
			private readonly PpsSqlColumnInfo column;

			public PpsColumnAttributes(PpsSqlColumnInfo column)
			{
				this.column = column;
			} // ctor

			public bool TryGetProperty(string name, out object value)
				=> column.TryGetProperty(name, out value);

			public IEnumerator<PropertyValue> GetEnumerator()
				=> column.GetProperties();

			IEnumerator IEnumerable.GetEnumerator()
				=> column.GetProperties();
		} // class PpsColumnAttributes

		#endregion

		private readonly PpsSqlTableInfo table;

		private readonly int maxLength;
		private readonly byte precision;
		private readonly byte scale;
		private readonly bool isNullable;
		private readonly bool isIdentity;
		private readonly bool isPrimaryKey;

		/// <summary></summary>
		/// <param name="table"></param>
		/// <param name="columnName"></param>
		/// <param name="dataType"></param>
		/// <param name="maxLength"></param>
		/// <param name="precision"></param>
		/// <param name="scale"></param>
		/// <param name="isNullable"></param>
		/// <param name="isIdentity"></param>
		/// <param name="isPrimaryKey"></param>
		public PpsSqlColumnInfo(PpsSqlTableInfo table, string columnName, Type dataType, int maxLength, byte precision, byte scale, bool isNullable, bool isIdentity, bool isPrimaryKey)
			: base(null, columnName, dataType)
		{
			this.table = table ?? throw new ArgumentNullException(nameof(table));
			this.maxLength = maxLength;
			this.precision = precision;
			this.scale = scale;
			this.isNullable = isNullable;
			this.isIdentity = isIdentity;
			this.isPrimaryKey = isPrimaryKey;
		} // ctor

		/// <summary>Create attributes for this column.</summary>
		/// <returns></returns>
		protected override IPropertyEnumerableDictionary CreateAttributes()
			=> new PpsColumnAttributes(this);

		/// <summary>Return default properties.</summary>
		/// <returns></returns>
		protected virtual IEnumerator<PropertyValue> GetProperties()
		{
			yield return new PropertyValue(nameof(MaxLength), MaxLength);
			yield return new PropertyValue(nameof(Precision), Precision);
			yield return new PropertyValue(nameof(Scale), Scale);
			yield return new PropertyValue(nameof(Nullable), Nullable);
			yield return new PropertyValue(nameof(IsIdentity), IsIdentity);
		} // func GetProperties

		/// <summary>Return default properties.</summary>
		/// <param name="propertyName"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		protected virtual bool TryGetProperty(string propertyName, out object value)
		{
			switch (propertyName[0])
			{
				case 'S':
				case 's':
					if (String.Compare(propertyName, nameof(Scale), StringComparison.OrdinalIgnoreCase) == 0)
					{
						value = Scale;
						return true;
					}
					break;
				case 'M':
				case 'm':
					if (String.Compare(propertyName, nameof(MaxLength), StringComparison.OrdinalIgnoreCase) == 0)
					{
						value = MaxLength;
						return true;
					}
					break;
				case 'N':
				case 'n':
					if (String.Compare(propertyName, nameof(Nullable), StringComparison.OrdinalIgnoreCase) == 0)
					{
						value = Nullable;
						return true;
					}
					break;
				case 'P':
				case 'p':
					if (String.Compare(propertyName, nameof(Precision), StringComparison.OrdinalIgnoreCase) == 0)
					{
						value = Precision;
						return true;
					}
					break;
				case 'I':
				case 'i':
					if (String.Compare(propertyName, nameof(IsIdentity), StringComparison.OrdinalIgnoreCase) == 0)
					{
						value = IsIdentity;
						return true;
					}
					break;
			}

			value = null;
			return false;
		} // func TryGetProperty

		/// <summary>Append the column name to an sql expression.</summary>
		/// <param name="sb"></param>
		protected virtual void AppendColumnName(StringBuilder sb)
			=> sb.Append('[').Append(Name).Append(']');

		/// <summary>Append the column as a alias column.</summary>
		/// <param name="sb"></param>
		/// <param name="fullQualified"></param>
		/// <returns></returns>
		public StringBuilder AppendAsColumn(StringBuilder sb, bool fullQualified = false)
		{
			if (fullQualified) // add table
				sb.Append(Table.SqlQualifiedName).Append('.');
			AppendColumnName(sb);
			return sb;
		} // func AppendAsColumn

		/// <summary>Append the column as a alias column.</summary>
		/// <param name="sb"></param>
		/// <param name="aliasName"></param>
		/// <returns></returns>
		public StringBuilder AppendAsColumn(StringBuilder sb, string aliasName)
		{
			sb.Append(aliasName).Append('.');
			AppendColumnName(sb);
			return sb;
		} // proc AppendAsColumn

		/// <summary></summary>
		/// <param name="parameter"></param>
		/// <param name="parameterName"></param>
		/// <param name="value"></param>
		protected virtual void InitSqlParameter(DbParameter parameter, string parameterName, object value)
		{
			parameter.ParameterName = parameterName ?? "@" + Name;
			parameter.Size = maxLength;
			parameter.Precision = precision;
			parameter.Scale = scale;
			parameter.Direction = ParameterDirection.Input;
			parameter.SourceColumn = Name;
			parameter.SourceVersion = DataRowVersion.Current;
			parameter.SetValue(value, DataType);
		} // proc InitSqlParameter

		/// <summary></summary>
		/// <param name="command"></param>
		/// <param name="parameterName"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public virtual DbParameter AppendSqlParameter(DbCommand command, string parameterName = null, object value = null)
		{
			var parameter = command.CreateParameter();
			InitSqlParameter(parameter, parameterName, value);
			command.Parameters.Add(parameter);
			return parameter;
		} // AppendSqlParameter

		/// <summary>Table</summary>
		public PpsSqlTableInfo Table => table;

		/// <summary>Full qualified name of the column for the server.</summary>
		public string TableColumnName => table.SchemaName + "." + table.TableName + "." + Name;

		/// <summary></summary>
		public int MaxLength => maxLength;
		/// <summary></summary>
		public byte Precision => precision;
		/// <summary></summary>
		public byte Scale => scale;
		/// <summary></summary>
		public bool Nullable => isNullable;
		/// <summary></summary>
		public bool IsIdentity => isIdentity;
		/// <summary>Is this a primary column.</summary>
		public bool IsPrimaryKey => isPrimaryKey;

		// -- Static ----------------------------------------------------------

		/// <summary></summary>
		/// <param name="columnName"></param>
		/// <returns></returns>
		public static string GetColumnName(string columnName)
		{
			if (String.IsNullOrEmpty(columnName))
				return columnName;

			var pos = columnName.LastIndexOf('.');
			return pos == -1
				? columnName
				: columnName.Substring(pos + 1);
		} // func GetColumnName

		/// <summary></summary>
		/// <param name="command"></param>
		/// <param name="columnDescription"></param>
		/// <param name="parameterName"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public static DbParameter AppendSqlParameter(DbCommand command, IDataColumn columnDescription, string parameterName = null, object value = null)
		{
			if (columnDescription is IPpsColumnDescription c && c.TryGetColumnDescriptionImplementation<PpsSqlColumnInfo>(out var sqlColumn)) // sql column -> easy to add
				return sqlColumn.AppendSqlParameter(command);
			else // other column, create 
			{
				var parameter = command.CreateParameter();

				parameter.ParameterName = parameterName ?? "@" + GetColumnName(columnDescription.Name);

				parameter.Direction = ParameterDirection.Input;
				parameter.SourceColumn = columnDescription.Name;
				parameter.SourceVersion = DataRowVersion.Current;

				var t = columnDescription.DataType;
				switch (Type.GetTypeCode(t))
				{
					case TypeCode.String:
						parameter.Size = columnDescription.Attributes.GetProperty("maxLength", 32000);
						parameter.DbType = DbType.String;
						break;
					case TypeCode.Boolean:
						parameter.DbType = DbType.Boolean;
						break;
					case TypeCode.DateTime:
						parameter.DbType = DbType.DateTime2;
						break;

					case TypeCode.Single:
						parameter.Precision = columnDescription.Attributes.GetProperty("Precision", (byte)20);
						parameter.Scale = columnDescription.Attributes.GetProperty("Scale", (byte)10);
						parameter.DbType = DbType.Single;
						break;
					case TypeCode.Double:
						parameter.Precision = columnDescription.Attributes.GetProperty("Precision", (byte)15);
						parameter.Scale = columnDescription.Attributes.GetProperty("Scale", (byte)7);
						parameter.DbType = DbType.Double;
						break;
					case TypeCode.Decimal:
						parameter.Precision = columnDescription.Attributes.GetProperty("Precision", (byte)7);
						parameter.Scale = columnDescription.Attributes.GetProperty("Scale", (byte)3);
						parameter.DbType = DbType.Decimal;
						break;

					case TypeCode.Byte:
						parameter.DbType = DbType.Byte;
						break;
					case TypeCode.SByte:
						parameter.DbType = DbType.SByte;
						break;
					case TypeCode.Int16:
						parameter.DbType = DbType.Int16;
						break;
					case TypeCode.Int32:
						parameter.DbType = DbType.Int32;
						break;
					case TypeCode.Int64:
						parameter.DbType = DbType.Int64;
						break;
					case TypeCode.UInt16:
						parameter.DbType = DbType.UInt16;
						break;
					case TypeCode.UInt32:
						parameter.DbType = DbType.UInt32;
						break;
					case TypeCode.UInt64:
						parameter.DbType = DbType.UInt64;
						break;

					case TypeCode.Object:
						if (t == typeof(Guid))
							parameter.DbType = DbType.Guid;
						break;
				}

				parameter.SetValue(value, columnDescription.DataType);
				command.Parameters.Add(parameter);
				return parameter;
			}
		} // func AppendSqlParameter
	} // class PpsSqlColumnInfo

	#endregion

	#region -- class PpsSqlTableInfo --------------------------------------------------

	/// <summary>Table information</summary>
	[DebuggerDisplay("{DebuggerDisplay,nq}")]
	public abstract class PpsSqlTableInfo : IDataColumns
	{
		private readonly string schemaName;
		private readonly string tableName;

		private bool isSinglePrimaryKey = false;
		private PpsSqlColumnInfo primaryKey;

		private readonly List<PpsSqlColumnInfo> columns = new List<PpsSqlColumnInfo>();
		private readonly List<PpsSqlRelationInfo> relations = new List<PpsSqlRelationInfo>();

		/// <summary></summary>
		/// <param name="schemaName"></param>
		/// <param name="tableName"></param>
		public PpsSqlTableInfo(string schemaName, string tableName)
		{
			this.schemaName = schemaName ?? throw new ArgumentNullException(nameof(schemaName));
			this.tableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
		} // ctor

		internal void AddColumn(PpsSqlColumnInfo column)
		{
			columns.Add(column);
			OnColumnAdded(column);
		} // proc AddColumn

		/// <summary>New column added to this table.</summary>
		/// <param name="column"></param>
		protected virtual void OnColumnAdded(PpsSqlColumnInfo column)
		{
			if (column.IsPrimaryKey)
			{
				if (primaryKey == null)
				{
					primaryKey = column;
					isSinglePrimaryKey = true;
				}
				else if (isSinglePrimaryKey)
					isSinglePrimaryKey = false;
			}
		} // proc OnColumnAdded

		internal void AddRelation(PpsSqlRelationInfo relationInfo)
		{
			relations.Add(relationInfo);
			OnRelationAdded(relationInfo);
		} // proc AddRelation

		/// <summary>New relation added to this table.</summary>
		/// <param name="relationInfo"></param>
		protected virtual void OnRelationAdded(PpsSqlRelationInfo relationInfo) { }

		/// <summary>Find column by name.</summary>
		/// <param name="columnName"></param>
		/// <param name="throwException"></param>
		/// <returns></returns>
		public PpsSqlColumnInfo FindColumn(string columnName, bool throwException = false)
		{
			var col = columns.Find(c => String.Compare(c.Name, columnName, StringComparison.OrdinalIgnoreCase) == 0);
			if (col == null && throwException)
				throw new ArgumentOutOfRangeException("columnName", $"Table '{TableName}' does not define  Column '{columnName}'.");
			return col;
		} // func FindColumn

		private string DebuggerDisplay
			=> $"TableInfo: {QualifiedName} [pk: {PrimaryKey?.Name ?? "null"}]";

		/// <summary>Schema</summary>
		public string SchemaName => schemaName;
		/// <summary>Name of the table.</summary>
		public string TableName => tableName;
		/// <summary>Full qualified name for sql-server.</summary>
		public virtual string SqlQualifiedName => schemaName + ".[" + tableName + "]";
		/// <summary>Qualified name within the server.</summary>
		public string QualifiedName => schemaName + "." + tableName;

		/// <summary></summary>
		public bool IsSinglePrimaryKey => isSinglePrimaryKey;
		/// <summary>Get primary key of this table.</summary>
		public PpsSqlColumnInfo PrimaryKey => isSinglePrimaryKey ? primaryKey : null;
		/// <summary>Get the primary keys of the table.</summary>
		public IEnumerable<PpsSqlColumnInfo> PrimaryKeys => Columns.Where(c => c.IsPrimaryKey);

		/// <summary>Column information of this table.</summary>
		public IEnumerable<PpsSqlColumnInfo> Columns => columns;
		/// <summary>Relations of this table.</summary>
		public IEnumerable<PpsSqlRelationInfo> RelationInfo => relations;

		IReadOnlyList<IDataColumn> IDataColumns.Columns => columns;
	} // class PpsSqlTableInfo

	#endregion

	#region -- class PpsSqlRelationInfo -----------------------------------------------

	/// <summary></summary>
	[DebuggerDisplay("{DebuggerDisplay,nq}")]
	public abstract class PpsSqlRelationInfo
	{
		private readonly string name;
		private readonly PpsSqlColumnInfo parentColumn;
		private readonly PpsSqlColumnInfo referencedColumn;

		/// <summary></summary>
		/// <param name="name"></param>
		/// <param name="parentColumn"></param>
		/// <param name="referencedColumn"></param>
		public PpsSqlRelationInfo(string name, PpsSqlColumnInfo parentColumn, PpsSqlColumnInfo referencedColumn)
		{
			this.name = name;
			this.parentColumn = parentColumn;
			this.referencedColumn = referencedColumn;
		} // ctor

		private string DebuggerDisplay
			=> $"RelationInfo: {name} [parent: {parentColumn?.Name ?? "null"}; child: {referencedColumn?.Name ?? "null"}]";

		/// <summary></summary>
		public string Name => name;
		/// <summary></summary>
		public PpsSqlColumnInfo ParentColumn => parentColumn;
		/// <summary></summary>
		public PpsSqlColumnInfo ReferencedColumn => referencedColumn;
	} // class PpsSqlTableInfo

	#endregion

	#region -- interface IPpsSqlConnectionHandle --------------------------------------

	/// <summary></summary>
	public interface IPpsSqlConnectionHandle : IPpsConnectionHandle
	{
		/// <summary>Create a new connection.</summary>
		/// <returns></returns>
		DbConnection ForkConnection();
		/// <summary>Access the database connection.</summary>
		DbConnection Connection { get; }
	} // interface IPpsSqlConnectionHandle

	#endregion

	#region -- class PpsSqlDataSource -------------------------------------------------

	/// <summary>Base class for sql-based datasources.</summary>
	public abstract class PpsSqlDataSource : PpsDataSource
	{
		#region -- class SqlDataResultColumnDescription -------------------------------

		private sealed class SqlDataResultColumnDescription : PpsColumnDescription
		{
			#region -- class PpsDataResultColumnAttributes ----------------------------

			private sealed class PpsDataResultColumnAttributes : IPropertyEnumerableDictionary
			{
				private readonly SqlDataResultColumnDescription column;

				public PpsDataResultColumnAttributes(SqlDataResultColumnDescription column)
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

			public SqlDataResultColumnDescription(IPpsColumnDescription parent, DataRow row, string name, Type dataType)
				: base(parent, name, dataType)
			{
				this.row = row;
			} // ctor

			protected override IPropertyEnumerableDictionary CreateAttributes()
				=> PpsColumnDescriptionHelper.GetColumnDescriptionParentAttributes(new PpsDataResultColumnAttributes(this), Parent);
		} // class PpsDataResultColumnDescription

		#endregion

		#region -- class PpsSqlDataSelectorToken --------------------------------------

		/// <summary>Representation of a data view for the system.</summary>
		protected sealed class PpsSqlDataSelectorToken : IPpsSelectorToken
		{
			private readonly PpsSqlDataSource source;
			private readonly string name;
			private readonly string viewName;

			private readonly IPpsColumnDescription[] columnDescriptions;

			internal PpsSqlDataSelectorToken(PpsSqlDataSource source, string name, string viewName, IPpsColumnDescription[] columnDescriptions)
			{
				this.source = source ?? throw new ArgumentNullException(nameof(source));
				this.name = name ?? throw new ArgumentNullException(nameof(name));
				this.viewName = viewName;
				this.columnDescriptions = columnDescriptions;
			} // ctor

			/// <summary></summary>
			/// <param name="connection"></param>
			/// <param name="throwException"></param>
			/// <returns></returns>
			public PpsDataSelector CreateSelector(IPpsConnectionHandle connection, bool throwException = true)
				=> new PpsSqlDataSelector((IPpsSqlConnectionHandle)connection, this, null, null, null);

			/// <summary></summary>
			/// <param name="selectorColumn"></param>
			/// <returns></returns>
			public IPpsColumnDescription GetFieldDescription(string selectorColumn)
				=> columnDescriptions.FirstOrDefault(c => String.Compare(selectorColumn, c.Name, StringComparison.OrdinalIgnoreCase) == 0);

			/// <summary></summary>
			public string Name => name;
			/// <summary></summary>
			public string ViewName => viewName;
			/// <summary></summary>
			public PpsSqlDataSource DataSource => source;

			PpsDataSource IPpsSelectorToken.DataSource => DataSource;

			IReadOnlyList<IDataColumn> IDataColumns.Columns => columnDescriptions;
		} // class PpsSqlDataSelectorToken

		#endregion

		#region -- class PpsSqlConnectionHandle ---------------------------------------

		/// <summary></summary>
		protected abstract class PpsSqlConnectionHandle<DBCONNECTION, DBCONNECTIONSTRINGBUILDER> : IPpsSqlConnectionHandle
			where DBCONNECTION : DbConnection
			where DBCONNECTIONSTRINGBUILDER : DbConnectionStringBuilder
		{
			/// <summary></summary>
			public event EventHandler Disposed;

			private readonly PpsSqlDataSource dataSource;
			private readonly DBCONNECTION connection;
			private readonly DBCONNECTIONSTRINGBUILDER connectionString;
			private readonly PpsCredentials credentials;

			private bool isDisposed = false;

			#region -- Ctor/Dtor ------------------------------------------------------

			/// <summary></summary>
			/// <param name="dataSource"></param>
			/// <param name="credentials"></param>
			public PpsSqlConnectionHandle(PpsSqlDataSource dataSource, PpsCredentials credentials)
			{
				this.dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
				this.credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
				
				connection = CreateConnection();
				connectionString = CreateConnectionStringBuilder(false);
			} // ctor

			/// <summary></summary>
			public void Dispose()
			{
				if (isDisposed)
					throw new ObjectDisposedException(GetType().Name);

				// clear connection
				connection.Dispose();
				isDisposed = true;

				// invoke disposed
				Disposed?.Invoke(this, EventArgs.Empty);
			} // proc Dispose

			/// <summary></summary>
			/// <returns></returns>
			protected abstract DBCONNECTION CreateConnection();

			/// <summary></summary>
			/// <param name="forWrite"></param>
			/// <returns></returns>
			protected virtual DBCONNECTIONSTRINGBUILDER CreateConnectionStringBuilder(bool forWrite) 
				=> dataSource.CreateConnectionStringBuilder<DBCONNECTIONSTRINGBUILDER>(forWrite ? "UserTrans" : "User");

			#endregion

			#region -- Connect --------------------------------------------------------

			/// <summary></summary>
			/// <returns></returns>
			protected abstract Task ConnectCoreAsync(DBCONNECTION connection, DBCONNECTIONSTRINGBUILDER connectionString);

			private async Task<bool> ConnectAsync(DBCONNECTION connection, DBCONNECTIONSTRINGBUILDER connectionString, bool throwException)
			{
				// create the connection
				try
				{
					await ConnectCoreAsync(connection, connectionString); 
					return true;
				}
				catch (Exception)
				{
					if (throwException)
						throw;
					return false;
				}
			} // func ConnectAsync

			DbConnection IPpsSqlConnectionHandle.ForkConnection()
				=> ForkConnectionAsync().AwaitTask();
			
			/// <summary></summary>
			/// <returns></returns>
			public async Task<DBCONNECTION> ForkConnectionAsync()
			{
				// create a new connection
				var con = CreateConnection();
				
				// ensure connection
				await ConnectAsync(con, CreateConnectionStringBuilder(true), true);

				return con;
			} // func ForkConnection

			/// <summary></summary>
			/// <param name="throwException"></param>
			/// <returns></returns>
			public Task<bool> EnsureConnectionAsync(bool throwException)
			{
				if (IsConnected)
					return Task.FromResult(true);

				return ConnectAsync(connection, connectionString, throwException);
			} // func EnsureConnection

			#endregion

			/// <summary></summary>
			public PpsSqlDataSource DataSource => dataSource;
			PpsDataSource IPpsConnectionHandle.DataSource => dataSource;

			/// <summary></summary>
			public DBCONNECTION Connection => connection;
			DbConnection IPpsSqlConnectionHandle.Connection => connection;
			/// <summary></summary>
			public PpsCredentials Credentials => credentials;

			/// <summary></summary>
			public abstract bool IsConnected { get; }
		} // class PpsSqlConnectionHandle

		#endregion

		#region -- class PpsSqlDataSelector -------------------------------------------

		/// <summary></summary>
		private sealed class PpsSqlDataSelector : PpsDataSelector
		{
			#region -- class SqlDataFilterVisitor -------------------------------------

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

			private readonly IPpsSqlConnectionHandle connection;

			private readonly PpsSqlDataSelectorToken selectorToken;
			private readonly string selectList;
			private readonly string whereCondition;
			private readonly string orderBy;

			/// <summary></summary>
			/// <param name="connection"></param>
			/// <param name="selectorToken"></param>
			/// <param name="selectList"></param>
			/// <param name="whereCondition"></param>
			/// <param name="orderBy"></param>
			public PpsSqlDataSelector(IPpsSqlConnectionHandle connection, PpsSqlDataSelectorToken selectorToken, string selectList, string whereCondition, string orderBy)
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
				var column = lookupColumn(o.Identifier)
					?? throw new ArgumentNullException("orderby", $"Order by column '{o.Identifier} not found.");

				if (o.Negate)
					return column.Name + " DESC";
				else
					return column.Name;
			} // func FormatOrderExpression

			/// <summary></summary>
			/// <param name="expressions"></param>
			/// <param name="lookupNative"></param>
			/// <returns></returns>
			public sealed override PpsDataSelector ApplyOrder(IEnumerable<PpsDataOrderExpression> expressions, Func<string, string> lookupNative)
				=> SqlOrderBy(String.Join(", ", from o in expressions select FormatOrderExpression(o, lookupNative, selectorToken.GetFieldDescription)));

			/// <summary></summary>
			/// <param name="expression"></param>
			/// <param name="lookupNative"></param>
			/// <returns></returns>
			public sealed override PpsDataSelector ApplyFilter(PpsDataFilterExpression expression, Func<string, string> lookupNative)
				=> SqlWhere(new SqlDataFilterVisitor(lookupNative, selectorToken.GetFieldDescription).CreateFilter(expression));

			private string AddSelectList(string addSelectList)
			{
				if (String.IsNullOrEmpty(addSelectList))
					return selectList;

				return String.IsNullOrEmpty(selectList) ? addSelectList : selectList + ", " + addSelectList;
			} // func AddSelectList

			public PpsSqlDataSelector SqlSelect(string addSelectList)
			{
				if (String.IsNullOrEmpty(addSelectList))
					return this;

				var newSelectList = AddSelectList(addSelectList);
				return new PpsSqlDataSelector(connection, selectorToken, newSelectList, whereCondition, orderBy);
			} // func SqlSelect

			private string AddWhereCondition(string addWhereCondition)
			{
				if (String.IsNullOrEmpty(addWhereCondition))
					return whereCondition;

				return String.IsNullOrEmpty(whereCondition) ? addWhereCondition : "(" + whereCondition + ") and (" + addWhereCondition + ")";
			} // func AddWhereCondition

			public PpsSqlDataSelector SqlWhere(string addWhereCondition)
			{
				if (String.IsNullOrEmpty(addWhereCondition))
					return this;

				var newWhereCondition = AddWhereCondition(addWhereCondition);
				return new PpsSqlDataSelector(connection, selectorToken, selectList, newWhereCondition, orderBy);
			} // func SqlWhere

			private string AddOrderBy(string addOrderBy)
			{
				if (String.IsNullOrEmpty(addOrderBy))
					return orderBy;

				return String.IsNullOrEmpty(orderBy) ? addOrderBy : orderBy + ", " + addOrderBy;
			} // func AddOrderBy

			public PpsSqlDataSelector SqlOrderBy(string addOrderBy)
			{
				if (String.IsNullOrEmpty(addOrderBy))
					return this;

				var newOrderBy = AddOrderBy(addOrderBy);
				return new PpsSqlDataSelector(connection, selectorToken, selectList, whereCondition, newOrderBy);
			} // func SqlOrderBy

			public override IEnumerator<IDataRow> GetEnumerator(int start, int count)
				=> new DbRowEnumerator(((PpsSqlDataSource)DataSource).CreateViewCommand(connection, selectList, selectorToken.ViewName, whereCondition, orderBy, start, count));

			public override IPpsColumnDescription GetFieldDescription(string nativeColumnName)
				=> selectorToken.GetFieldDescription(nativeColumnName);
		} // class SqlDataSelector

		#endregion

		#region -- class SqlJoinExpression --------------------------------------------

		/// <summary>Implementation of the join expression for SQL.</summary>
		public sealed class PpsSqlJoinExpression : PpsDataJoinExpression<PpsSqlTableInfo>
		{
			#region -- class SqlEmitVisitor -------------------------------------------

			private sealed class SqlEmitVisitor : PpsJoinVisitor<string>
			{
				public override string CreateJoinStatement(string leftExpression, PpsDataJoinType type, string rightExpression, string on)
				{
					string GetJoinExpr()
					{
						switch (type)
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
					=> String.IsNullOrEmpty(alias)
						? table.SqlQualifiedName
						: table.SqlQualifiedName + " AS " + alias;
			} // class SqlEmitVisitor

			#endregion

			private readonly PpsSqlDataSource dataSource;

			/// <summary></summary>
			/// <param name="dataSource"></param>
			public PpsSqlJoinExpression(PpsSqlDataSource dataSource)
			{
				this.dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
			} // ctor

			/// <summary></summary>
			/// <param name="tableName"></param>
			/// <returns></returns>
			protected override PpsSqlTableInfo ResolveTable(string tableName)
				=> dataSource.ResolveTableByName<PpsSqlTableInfo>(tableName, true);

			/// <summary></summary>
			/// <param name="left"></param>
			/// <param name="joinOp"></param>
			/// <param name="right"></param>
			/// <returns></returns>
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

			/// <summary></summary>
			/// <param name="ppsColumn"></param>
			/// <param name="throwException"></param>
			/// <returns></returns>
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

			/// <summary></summary>
			/// <param name="name"></param>
			/// <param name="throwException"></param>
			/// <returns></returns>
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

			/// <summary></summary>
			/// <param name="commandText"></param>
			/// <param name="table"></param>
			/// <param name="column"></param>
			/// <returns></returns>
			public StringBuilder AppendColumn(StringBuilder commandText, PpsTableExpression table, PpsSqlColumnInfo column)
				=> String.IsNullOrEmpty(table.Alias)
					? column.AppendAsColumn(commandText, true)
					: column.AppendAsColumn(commandText, table.Alias);

			/// <summary></summary>
			/// <returns></returns>
			public string EmitJoin()
				=> new SqlEmitVisitor().Visit(this);
		} // class SqlJoinExpression

		#endregion

		#region -- class PpsSqlDataTransaction ----------------------------------------

		/// <summary>Class to execute data manipulation commands to the database</summary>
		protected abstract class PpsSqlDataTransaction<DBCONNECTION, DBTRANSACTION, DBCOMMAND> : PpsDataTransaction
			where DBCONNECTION : DbConnection
			where DBTRANSACTION : DbTransaction
			where DBCOMMAND : DbCommand
		{
			#region -- class ParameterMapping -----------------------------------------

			/// <summary></summary>
			protected abstract class ParameterMapping
			{
				private readonly DbParameter parameter;

				/// <summary></summary>
				/// <param name="parameter"></param>
				protected ParameterMapping(DbParameter parameter)
				{
					this.parameter = parameter ?? throw new ArgumentNullException(nameof(parameter));
				} // ctor

				/// <summary>Set parameter value</summary>
				/// <param name="table"></param>
				public abstract void UpdateParameter(LuaTable table);
				/// <summary>Set source value</summary>
				/// <param name="table"></param>
				public abstract void UpdateSource(LuaTable table);

				/// <summary></summary>
				protected DbParameter Parameter => parameter;
			} // class ParameterMapping

			/// <summary></summary>
			protected sealed class NameParameterMapping : ParameterMapping
			{
				private readonly string name;

				/// <summary></summary>
				/// <param name="name"></param>
				/// <param name="parameter"></param>
				public NameParameterMapping(string name, DbParameter parameter)
					: base(parameter)
				{
					this.name = name ?? throw new ArgumentNullException(nameof(name));
				} // ctor


				/// <summary>Set parameter value</summary>
				/// <param name="table"></param>
				public override void UpdateParameter(LuaTable table)
				{
					Parameter.Value = table.GetMemberValue(name) ?? DBNull.Value;
				} // proc UpdateParameter

				/// <summary>Set source value</summary>
				/// <param name="table"></param>
				public override void UpdateSource(LuaTable table)
				{
					if ((Parameter.Direction & ParameterDirection.Output) == ParameterDirection.Output)
						table.SetMemberValue(name, Parameter.Value.NullIfDBNull());
				} // proc UpdateSource
			} // class NameParameterMapping

			/// <summary></summary>
			protected sealed class IndexParameterMapping : ParameterMapping
			{
				private readonly int index;

				/// <summary></summary>
				/// <param name="index"></param>
				/// <param name="parameter"></param>
				public IndexParameterMapping(int index, DbParameter parameter)
					: base(parameter)
				{
					this.index = index;
				} // ctor

				/// <summary>Set parameter value</summary>
				/// <param name="table"></param>
				public override void UpdateParameter(LuaTable table)
				{
					Parameter.Value = table.GetArrayValue(index);
				} // proc UpdateParameter

				/// <summary>Set source value</summary>
				/// <param name="table"></param>
				public override void UpdateSource(LuaTable table)
				{
					if ((Parameter.Direction & ParameterDirection.Output) == ParameterDirection.Output)
						table.SetArrayValue(index, Parameter.Value.NullIfDBNull());
				} // proc UpdateSource
			} // class IndexParameterMapping

			#endregion

			private readonly DBCONNECTION connection;
			private readonly DBTRANSACTION transaction;

			#region -- Ctor/Dtor ------------------------------------------------------

			/// <summary></summary>
			/// <param name="dataSource"></param>
			/// <param name="connection"></param>
			public PpsSqlDataTransaction(PpsDataSource dataSource, IPpsConnectionHandle connection) 
				: base(dataSource, connection)
			{
				// create a connection for the transaction
				this.connection = (DBCONNECTION)((IPpsSqlConnectionHandle)connection).ForkConnection();

				// create the sql transaction
				this.transaction = (DBTRANSACTION)this.connection.BeginTransaction(IsolationLevel.ReadUncommitted);
			} // ctor

			/// <summary></summary>
			/// <param name="disposing"></param>
			protected override void Dispose(bool disposing)
			{
				base.Dispose(disposing); // commit/rollback

				if (disposing)
				{
					transaction.Dispose();
					connection.Dispose();
				}
			} // proc Dispose

			/// <summary>Transaction commit</summary>
			public override void Commit()
			{
				if (!IsCommited.HasValue)
					transaction.Commit();
				base.Commit();
			} // proc Commit

			/// <summary>Transaction rollback</summary>
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

			#region -- CreateCommand --------------------------------------------------
			
			/// <summary>Create a command</summary>
			/// <param name="commandType"></param>
			/// <param name="noTransaction"></param>
			/// <returns></returns>
			public virtual DBCOMMAND CreateCommand(CommandType commandType, bool noTransaction)
			{
				var cmd = (DBCOMMAND)connection.CreateCommand();
				cmd.CommandTimeout = 7200;
				cmd.Transaction = noTransaction ? null : transaction;
				return cmd;
			} // func CreateCommand

			/// <summary>Create a command</summary>
			/// <param name="parameter"></param>
			/// <param name="commandType"></param>
			/// <returns></returns>
			protected DBCOMMAND CreateCommand(LuaTable parameter, CommandType commandType)
				=> CreateCommand(commandType, parameter.GetOptionalValue("__notrans", false));

			/// <summary></summary>
			/// <param name="command"></param>
			/// <param name="parameterName"></param>
			/// <param name="parameterValue"></param>
			/// <returns></returns>
			protected virtual DbParameter CreateParameter(DBCOMMAND command, string parameterName, object parameterValue)
			{
				var param = command.CreateParameter();
				param.ParameterName = FormatParameterName(parameterName);
				param.Value = parameterValue ?? DBNull.Value;
				command.Parameters.Add(param);
				return param;
			} // CreateParameter

			/// <summary>Add trailing sql notations</summary>
			/// <param name="parameterName"></param>
			/// <returns></returns>
			protected virtual string FormatParameterName(string parameterName)
				=> "@" + UnformatParameterName(parameterName);

			/// <summary>Remove trailing sql notations.</summary>
			/// <param name="parameterName"></param>
			/// <returns></returns>
			protected virtual string UnformatParameterName(string parameterName)
			{
				if (String.IsNullOrEmpty(parameterName))
					throw new ArgumentNullException(nameof(parameterName));
				
				return parameterName[0] == '@'
					? parameterName.Substring(1)
					: parameterName;
			} // func UnformatParameterName

			/// <summary></summary>
			/// <param name="cmd"></param>
			/// <param name="behavior"></param>
			/// <returns></returns>
			protected DBDATAREADER ExecuteReaderCommand<DBDATAREADER>(DbCommand cmd, PpsDataTransactionExecuteBehavior behavior)
				where DBDATAREADER : DbDataReader
			{
				switch (behavior)
				{
					case PpsDataTransactionExecuteBehavior.NoResult:
						cmd.ExecuteNonQuery();
						return null;
					case PpsDataTransactionExecuteBehavior.SingleRow:
						return (DBDATAREADER)cmd.ExecuteReader(CommandBehavior.SingleRow);
					case PpsDataTransactionExecuteBehavior.SingleResult:
						return (DBDATAREADER)cmd.ExecuteReader(CommandBehavior.SingleResult);
					default:
						return (DBDATAREADER)cmd.ExecuteReader(CommandBehavior.Default);
				}
			} // func ExecuteReaderCommand

			#endregion

			#region -- Arguments ------------------------------------------------------

			/// <summary></summary>
			/// <param name="value"></param>
			/// <param name="throwException"></param>
			/// <returns></returns>
			protected LuaTable GetArguments(object value, bool throwException)
			{
				var args = value as LuaTable;
				if (args == null && throwException)
					throw new ArgumentNullException($"value", "No arguments defined.");
				return args;
			} // func GetArguments

			/// <summary></summary>
			/// <param name="parameter"></param>
			/// <param name="index"></param>
			/// <param name="throwException"></param>
			/// <returns></returns>
			protected LuaTable GetArguments(LuaTable parameter, int index, bool throwException)
			{
				var args = GetArguments(parameter[index], false);
				if (args == null && throwException)
					throw new ArgumentNullException($"parameter[{index}]", "No arguments defined.");
				return args;
			} // func GetArguments

			#endregion

			#region -- ExecuteCall ----------------------------------------------------

			/// <summary></summary>
			/// <param name="command"></param>
			protected abstract void PrepareStoredProcedure(DBCOMMAND command);

			/// <summary></summary>
			/// <param name="parameter"></param>
			/// <param name="name"></param>
			/// <param name="behavior"></param>
			/// <returns></returns>
			protected virtual IEnumerable<IEnumerable<IDataRow>> ExecuteCall(LuaTable parameter, string name, PpsDataTransactionExecuteBehavior behavior)
			{
				using (var cmd = CreateCommand(parameter, CommandType.StoredProcedure))
				{
					// build argument list
					cmd.CommandText = name;
					PrepareStoredProcedure(cmd); 
					
					// build parameter mapping
					var parameterMapping = new ParameterMapping[cmd.Parameters.Count];
					var j = 0;
					foreach (DbParameter p in cmd.Parameters)
					{
						var parameterName = UnformatParameterName(p.ParameterName);

						// threat return value different
						parameterMapping[j++] = (p.Direction & ParameterDirection.ReturnValue) == ParameterDirection.ReturnValue
							? (ParameterMapping)new IndexParameterMapping(1, p)
							: (ParameterMapping)new NameParameterMapping(parameterName, p);
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
							if (p is NameParameterMapping)
								p.UpdateParameter(args);
						}

						using (var r = ExecuteReaderCommand<DbDataReader>(cmd, behavior))
						{
							// copy arguments back
							foreach (var p in parameterMapping)
								p.UpdateSource(args);

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

			#region -- ExecuteSql -----------------------------------------------------

			private static Regex regExSqlParameter = new Regex(@"\@(\w+)", RegexOptions.Compiled);

			/// <summary></summary>
			/// <param name="parameter"></param>
			/// <param name="name"></param>
			/// <param name="behavior"></param>
			/// <returns></returns>
			protected virtual IEnumerable<IEnumerable<IDataRow>> ExecuteSql(LuaTable parameter, string name, PpsDataTransactionExecuteBehavior behavior)
			{
				/*
				 * sql is execute and the args are created as a parameter
				 */
				using (var cmd = CreateCommand(parameter, CommandType.Text))
				{
					cmd.CommandText = name;

					var args = GetArguments(parameter, 1, false);
					var emittedParameter = new List<string>();
					if (args != null)
					{
						foreach (Match m in regExSqlParameter.Matches(name))
						{
							var k = m.Groups[1].Value;
							if (!emittedParameter.Exists(c => String.Compare(c, k, StringComparison.OrdinalIgnoreCase) == 0))
							{
								emittedParameter.Add(k);
								CreateParameter(cmd, k, args.GetMemberValue(k, true));
							}
						}
					}

					// execute
					using (var r = ExecuteReaderCommand<DbDataReader>(cmd, behavior))
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

			#region -- ExecuteSimpleSelect --------------------------------------------

			#region -- class DefaultRowEnumerable -------------------------------------

			private sealed class DefaultRowEnumerable : IEnumerable<IDataRow>
			{
				#region -- class DefaultValueRow --------------------------------------

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

				#endregion

				#region -- class DefaultRowEnumerator ---------------------------------

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

				#endregion

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
				var tableInfos = new PpsSqlJoinExpression((PpsSqlDataSource)DataSource);
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

					using (var r = ExecuteReaderCommand<DbDataReader>(cmd, behavior))
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

			/// <summary></summary>
			/// <param name="parameter"></param>
			/// <param name="behavior"></param>
			/// <returns></returns>
			protected override IEnumerable<IEnumerable<IDataRow>> ExecuteResult(LuaTable parameter, PpsDataTransactionExecuteBehavior behavior)
			{
				string name;
				if ((name = (string)(parameter["execute"] ?? parameter["exec"])) != null)
					return ExecuteCall(parameter, name, behavior);
				else if ((name = (string)parameter["select"]) != null)
					return ExecuteSimpleSelect(parameter, name, behavior);
				else if ((name = (string)parameter["sql"]) != null)
					return ExecuteSql(parameter, name, behavior);
				else
					return base.ExecuteResult(parameter, behavior);
			} // func ExecuteResult

			/// <summary>Connection for the data manipulation</summary>
			public DBCONNECTION DbConnection => connection;
			/// <summary>Access the transaction</summary>
			public DBTRANSACTION DbTransaction => transaction;
		} // class PpsSqlDataTransaction

		#endregion

		private readonly ManualResetEventSlim schemaInfoInitialized = new ManualResetEventSlim(false);
		private bool isSchemaInfoFailed = false;
		private string lastReadedConnectionString = String.Empty;

		private readonly Dictionary<string, PpsSqlTableInfo> tables = new Dictionary<string, PpsSqlTableInfo>(StringComparer.OrdinalIgnoreCase);
		private readonly Dictionary<string, PpsSqlColumnInfo> columns = new Dictionary<string, PpsSqlColumnInfo>(StringComparer.OrdinalIgnoreCase);

		#region -- Ctor/Dtor/Config ---------------------------------------------------

		/// <summary></summary>
		/// <param name="sp"></param>
		/// <param name="name"></param>
		public PpsSqlDataSource(IServiceProvider sp, string name)
			: base(sp, name)
		{
		} // ctor

		/// <summary></summary>
		/// <param name="disposing"></param>
		protected override void Dispose(bool disposing)
		{
			try
			{
				CloseMasterConnection();
				schemaInfoInitialized.Dispose();
			}
			finally
			{
				base.Dispose(disposing);
			}
		} // proc Dispose

		/// <summary></summary>
		/// <param name="config"></param>
		protected override void OnBeginReadConfiguration(IDEConfigLoading config)
		{
			base.OnBeginReadConfiguration(config);

			// read the connection string
			var connectionString = config.ConfigNew.Element(PpsStuff.PpsNamespace + "connectionString")?.Value;
			if (String.IsNullOrEmpty(connectionString))
				throw new DEConfigurationException(config.ConfigNew, "<connectionString> is empty.");

			if (lastReadedConnectionString != connectionString)
			{
				OnBeginReadConnectionString(config, connectionString);

				// close current connection
				CloseMasterConnection();
			}
		} // proc OnBeginReadConfiguration

		/// <summary></summary>
		/// <param name="config"></param>
		/// <param name="connectionString"></param>
		protected virtual void OnBeginReadConnectionString(IDEConfigLoading config, string connectionString)
		{
			// this will be the new last connection string
			config.Tags.SetProperty("LastConStr", connectionString);
			config.Tags.SetProperty("ConStr", CreateConnectionStringBuilderCore(connectionString, "DES_Master"));
		} // proc OnBeginReadConnectionString

		/// <summary></summary>
		/// <param name="config"></param>
		protected override void OnEndReadConfiguration(IDEConfigLoading config)
		{
			base.OnEndReadConfiguration(config);

			// update connection string
			var connectionStringBuilder = (DbConnectionStringBuilder)config.Tags.GetProperty("ConStr", (object)null);

			if (connectionStringBuilder != null)
			{
				// set the new connection
				InitMasterConnection(connectionStringBuilder);

				lastReadedConnectionString = config.Tags.GetProperty("LastConStr", String.Empty);

				// start background thread
				Application.RegisterInitializationTask(10000, "Database", async () => await Task.Run(new Action(schemaInfoInitialized.Wait))); // block all other tasks
			}
		} // proc OnEndReadConfiguration

		#endregion

		#region -- Connection String --------------------------------------------------

		/// <summary>Connect to master connection.</summary>
		/// <param name="connectionString"></param>
		protected abstract void InitMasterConnection(DbConnectionStringBuilder connectionString);

		/// <summary>Close master connection</summary>
		protected abstract void CloseMasterConnection();

		/// <summary></summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="applicationName"></param>
		/// <returns></returns>
		protected T CreateConnectionStringBuilder<T>(string applicationName)
			where T : DbConnectionStringBuilder
		{
			if (String.IsNullOrEmpty(lastReadedConnectionString))
				throw new InvalidOperationException("Not initialized yet.");

			return (T)CreateConnectionStringBuilderCore(lastReadedConnectionString, applicationName);
		} // func CreateConnectionStringBuilder

		/// <summary>Create a new connection string.</summary>
		/// <param name="connectionString"></param>
		/// <param name="applicationName"></param>
		/// <returns></returns>
		protected abstract DbConnectionStringBuilder CreateConnectionStringBuilderCore(string connectionString, string applicationName);

		#endregion

		#region -- Schema Initialization ----------------------------------------------

		/// <summary>Initialize schema</summary>
		protected void InitializeSchema()
		{
			lock (schemaInfoInitialized)
			{
				if (schemaInfoInitialized.IsSet)
					throw new InvalidOperationException("Schema is already loaded.");

				try
				{
					// load schema
					InitializeSchemaCore();
				}
				catch (Exception e)
				{
					Log.Except("Schema initialization failed.", e);
					isSchemaInfoFailed = true;
				}
				finally
				{
					// done
					schemaInfoInitialized.Set();
				}
			}
		} // proc InitializeSchema

		/// <summary>Core code to read schema.</summary>
		protected abstract void InitializeSchemaCore();

		/// <summary>Is schema readed.</summary>
		public bool IsSchemaInitialized => schemaInfoInitialized.IsSet && !isSchemaInfoFailed;

		#endregion

		#region -- Script Management --------------------------------------------------

		/// <summary>Read a sql-script from resource.</summary>
		/// <param name="resourceName"></param>
		/// <param name="type"></param>
		/// <returns></returns>
		protected string GetResourceScript(Type type, string resourceName)
		{
			using (var src = type.Assembly.GetManifestResourceStream(type, resourceName))
			{
				if (src == null)
					throw new ArgumentException($"{resourceName} not found.");

				using (var sr = new StreamReader(src, Encoding.UTF8, true))
					return sr.ReadToEnd();
			}
		} // func GetResourceScript

		#endregion

		#region -- Schema Management --------------------------------------------------

		/// <summary>Add this table to schema</summary>
		/// <param name="table"></param>
		protected void AddTable(PpsSqlTableInfo table)
			=> tables.Add(table.QualifiedName, table);

		/// <summary>Add colum to schema</summary>
		/// <param name="column"></param>
		protected void AddColumn(PpsSqlColumnInfo column)
		{
			columns[column.TableColumnName] = column;
			column.Table.AddColumn(column);
		} // proc AddColumn

		/// <summary>Add relations to the tables.</summary>
		/// <param name="relation"></param>
		protected void AddRelation(PpsSqlRelationInfo relation)
		{
			relation.ParentColumn.Table.AddRelation(relation);
			//relation.ReferencedColumn.Table.AddRelation(relation);
		} // proc AddRelation

		/// <summary></summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="name"></param>
		/// <param name="throwException"></param>
		/// <returns></returns>
		protected T ResolveTableByName<T>(string name, bool throwException = false)
			where T : PpsSqlTableInfo
		{
			var tableInfo = tables[name];
			if (tableInfo == null && throwException)
				throw new ArgumentNullException("name", $"Table '{name}' is not defined.");
			return (T)tableInfo;
		} // func ResolveTableByName

		/// <summary>Full qualified column name.</summary>
		/// <param name="columnName"></param>
		/// <param name="throwException"></param>
		/// <returns></returns>
		public override IPpsColumnDescription GetColumnDescription(string columnName, bool throwException)
		{
			if (columns.TryGetValue(columnName, out var column))
				return column;
			else if (throwException)
				throw new ArgumentException($"Could not resolve column {columnName} to source {Name}.", columnName);
			else
				return null;
		} // func GetColumnDescription

		#endregion

		#region -- Master Connection Service ------------------------------------------

		/// <summary></summary>
		/// <param name="connection"></param>
		/// <returns></returns>
		protected abstract IDisposable UseMasterConnection(out DbConnection connection);

		#endregion

		#region -- View Management ----------------------------------------------------

		private async Task<IPpsColumnDescription[]> ExecuteForResultSetAsync(DbConnection connection, string name)
		{
			// execute the view once to determine the resultset
			using (var cmd = connection.CreateCommand())
			{
				cmd.CommandTimeout = 6000;
				cmd.CommandText = "SELECT * FROM " + name;
				using (var r = await cmd.ExecuteReaderAsync(CommandBehavior.SchemaOnly | CommandBehavior.KeyInfo))
				{
					var columnDescriptions = new IPpsColumnDescription[r.FieldCount];

					var dt = r.GetSchemaTable();
					var i = 0;
					foreach (DataRow c in dt.Rows)
					{
						IPpsColumnDescription parentColumnDescription;
						var nativeColumnName = r.GetName(i);

						// try to find the view base description
						parentColumnDescription = Application.GetFieldDescription(name + "." + nativeColumnName, false);

						// try to find the table based field name
						if (parentColumnDescription == null)
						{
							var schemaName = GetDataRowValue<string>(c, "BaseSchemaName", null) ?? "dbo";
							var tableName = GetDataRowValue<string>(c, "BaseTableName", null);
							var columnName = GetDataRowValue<string>(c, "BaseColumnName", null);

							if (tableName != null && columnName != null)
							{
								var fieldName = schemaName + "." + tableName + "." + columnName;
								parentColumnDescription = Application.GetFieldDescription(fieldName, false);
							}
						}

						columnDescriptions[i] = new SqlDataResultColumnDescription(parentColumnDescription, c, nativeColumnName, r.GetFieldType(i));
						i++;
					}

					return columnDescriptions;
				}
			} // using cmd
		} // pro ExecuteForResultSet

		/// <summary></summary>
		/// <param name="connection"></param>
		/// <param name="selectList"></param>
		/// <param name="viewName"></param>
		/// <param name="whereCondition"></param>
		/// <param name="orderBy"></param>
		/// <param name="start"></param>
		/// <param name="count"></param>
		/// <returns></returns>
		protected abstract DbCommand CreateViewCommand(IPpsSqlConnectionHandle connection, string selectList, string viewName, string whereCondition, string orderBy, int start, int count);
		
		/// <summary>Generate view in the database.</summary>
		/// <param name="connection">Database connection</param>
		/// <param name="name">Name of the view.</param>
		/// <param name="selectStatement">Select statemate of the view</param>
		/// <returns></returns>
		protected abstract Task<string> CreateOrReplaceViewAsync(DbConnection connection, string name, string selectStatement);

		private async Task<IPpsSelectorToken> CreateCoreAsync(string name, Func<DbConnection, Task<string>> createView)
		{
			IPpsColumnDescription[] columnDescriptions;

			string viewName = null;
			using (UseMasterConnection(out var connection))
			{
				viewName = await createView(connection);
				columnDescriptions = await ExecuteForResultSetAsync(connection, viewName);
			}

			return new PpsSqlDataSelectorToken(this, name, viewName, columnDescriptions);
		} // func CreateCore

		private static async Task<string> LoadSqlFileAsync(string fileName)
		{
			using (var src = new FileStream(fileName, FileMode.Open))
			using (var sr = Procs.OpenStreamReader(src, Encoding.UTF8))
				return await sr.ReadToEndAsync();
		} // func LoadSqlFileAsync

		/// <summary></summary>
		/// <param name="name"></param>
		/// <param name="selectStatement"></param>
		/// <returns></returns>
		protected Task<IPpsSelectorToken> CreateSelectorTokenFromSelectAsync(string name, string selectStatement)
			=> CreateCoreAsync(name, (connection) => CreateOrReplaceViewAsync(connection, name, selectStatement));

		/// <summary></summary>
		/// <param name="name"></param>
		/// <param name="fileName"></param>
		/// <returns></returns>
		protected async Task<IPpsSelectorToken> CreateSelectorTokenFromFileAsync(string name, string fileName)
		{
			var content = await LoadSqlFileAsync(fileName);
			return await CreateCoreAsync(name, (connection) => CreateOrReplaceViewAsync(connection, name, content));
		} // func CreateSelectorTokenFromFileAsync

		/// <summary></summary>
		/// <param name="name"></param>
		/// <param name="type"></param>
		/// <param name="resourceScript"></param>
		/// <returns></returns>
		protected Task<IPpsSelectorToken> CreateSelectorTokenFromResourceAsync(string name, Type type, string resourceScript)
		{
			var content = GetResourceScript(type, resourceScript);
			return CreateCoreAsync(name, (connection) => CreateOrReplaceViewAsync(connection, name, content));
		} // func CreateSelectorTokenFromResourceAsync

		/// <summary></summary>
		/// <param name="name"></param>
		/// <param name="viewName"></param>
		/// <returns></returns>
		protected Task<IPpsSelectorToken> CreateSelectorTokenFromViewNameAsync(string name, string viewName)
			=> CreateCoreAsync(name, (connection) => Task.FromResult(viewName ?? name));

		/// <summary>Create selector from </summary>
		/// <param name="name"></param>
		/// <param name="sourceDescription"></param>
		/// <returns></returns>
		public sealed override async Task<IPpsSelectorToken> CreateSelectorTokenAsync(string name, XElement sourceDescription)
		{
			// file => init by file
			// select => inline sql select
			// resource => file from resource
			// view => name of existing view
			try
			{
				var sourceType = sourceDescription.GetAttribute("type", "file");
				if (sourceType == "select") // create view from sql
					return await CreateSelectorTokenFromSelectAsync(name, sourceDescription.Value);
				else if (sourceType == "file")
					return await CreateSelectorTokenFromFileAsync(name, ProcsDE.GetFileName(sourceDescription, sourceDescription.Value));
				else if (sourceType == "resource")
					return await CreateSelectorTokenFromResourceAsync(name, GetType(), sourceDescription.Value);
				else if (sourceType == "view")
					return await CreateSelectorTokenFromViewNameAsync(name, sourceDescription?.Value);
				else
					throw new ArgumentOutOfRangeException("@type", sourceType);
			}
			catch (Exception e)
			{
				throw new DEConfigurationException(sourceDescription, String.Format("Can not create selector for '{0}'.", name), e);
			}
		} // func CreateSelectorTokenAsync

		#endregion

		#region -- DataTable - Helper -------------------------------------------------

		/// <summary></summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="row"></param>
		/// <param name="columnName"></param>
		/// <param name="default"></param>
		/// <returns></returns>
		protected static T GetDataRowValue<T>(DataRow row, string columnName, T @default)
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
	} // class PpsSqlDataSource

	#endregion
}
