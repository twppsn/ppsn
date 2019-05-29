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
		/// <param name="parameterValue"></param>
		public virtual void InitSqlParameter(DbParameter parameter, string parameterName = null, object parameterValue = null)
		{
			if (parameterName != null)
				parameter.ParameterName = parameterName;

			parameter.Size = maxLength;
			parameter.Precision = precision;
			parameter.Scale = scale;
			parameter.Direction = ParameterDirection.Input;
			parameter.SourceColumn = Name;
			parameter.SourceVersion = DataRowVersion.Current;
			parameter.SetValue(parameterValue, DataType, DBNull.Value);
		} // proc InitSqlParameter

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
	} // class PpsSqlColumnInfo

	#endregion

	#region -- interface IPpsSqlTableOrView -------------------------------------------

	/// <summary></summary>
	public interface IPpsSqlTableOrView : IDataColumns
	{
		/// <summary></summary>
		string TableName { get; }
		/// <summary></summary>
		string QualifiedName { get; }
		/// <summary></summary>
		new IReadOnlyList<IPpsColumnDescription> Columns { get; }
	} // interface IPpsSqlTableOrView

	#endregion

	#region -- interface IPpsSqlAliasColumn -------------------------------------------

	/// <summary></summary>
	public interface IPpsSqlAliasColumn
	{
		/// <summary></summary>
		string Alias { get; }
		/// <summary></summary>
		string Expression { get; }
		/// <summary></summary>
		Type DataType { get; }
	} // interface IPpsSqlAliasColumn

	#endregion

	#region -- class PpsSqlTableInfo --------------------------------------------------

	/// <summary>Table information</summary>
	[DebuggerDisplay("{DebuggerDisplay,nq}")]
	public abstract class PpsSqlTableInfo : IDataColumns, IPpsSqlTableOrView
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

		string IPpsSqlTableOrView.QualifiedName => SqlQualifiedName;

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


		IReadOnlyList<IPpsColumnDescription> IPpsSqlTableOrView.Columns => columns;
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

	#region -- class PpsSqlParameterInfo ----------------------------------------------

	/// <summary></summary>
	public abstract class PpsSqlParameterInfo
	{
		private readonly string name;
		private readonly bool hasDefault;
		private readonly ParameterDirection direction;

		/// <summary></summary>
		/// <param name="name"></param>
		/// <param name="direction"></param>
		/// <param name="hasDefault"></param>
		public PpsSqlParameterInfo(string name, ParameterDirection direction, bool hasDefault)
		{
			this.name = name;
			this.direction = direction;
			this.hasDefault = hasDefault;
		} // ctor

		/// <summary></summary>
		/// <returns></returns>
		public override string ToString()
			=> name;

		/// <summary></summary>
		/// <param name="parameter"></param>
		public abstract void InitSqlParameter(DbParameter parameter);

		/// <summary></summary>
		public string Name => name;
		/// <summary>Parameter direction</summary>
		public ParameterDirection Direction => direction;
		/// <summary></summary>
		public bool HasDefault => hasDefault;
		/// <summary>Return default value for c# null.</summary>
		public virtual object DefaultValue => hasDefault ? null : DBNull.Value;
	} // class PpsSqlParameterInfo

	#endregion

	#region -- class PpsSqlProcedureInfo ----------------------------------------------

	/// <summary></summary>
	[DebuggerDisplay("{DebuggerDisplay,nq}")]
	public abstract class PpsSqlProcedureInfo
	{
		private readonly string schemaName;
		private readonly string procedureName;

		private readonly List<PpsSqlParameterInfo> parameters = new List<PpsSqlParameterInfo>();

		/// <summary></summary>
		/// <param name="schemaName"></param>
		/// <param name="procedureName"></param>
		public PpsSqlProcedureInfo(string schemaName, string procedureName)
		{
			this.schemaName = schemaName;
			this.procedureName = procedureName;
		} // ctor

		/// <summary></summary>
		/// <param name="parameterInfo"></param>
		public virtual void AddParameter(PpsSqlParameterInfo parameterInfo)
			=> parameters.Add(parameterInfo);

		private string DebuggerDisplay
			=> $"ProcedureInfo: {QualifiedName} ({ (String.Join(", ", from p in parameters select p.ToString())) })";

		/// <summary>Schema</summary>
		public string SchemaName => schemaName;
		/// <summary>Name of the procedure.</summary>
		public string ProcedureName => procedureName;
		/// <summary>Full qualified name for sql-server.</summary>
		public virtual string SqlQualifiedName => schemaName + ".[" + procedureName + "]";
		/// <summary>Qualified name within the server.</summary>
		public string QualifiedName => schemaName + "." + procedureName;

		/// <summary>Has this procedure has output parameter.</summary>
		public virtual bool HasOutput => parameters.FirstOrDefault(c => c.Direction != ParameterDirection.ReturnValue && (c.Direction & ParameterDirection.Output) == ParameterDirection.Output) != null;
		/// <summary>Has this procedure an return value.</summary>
		public virtual bool HasReturnValue => parameters[0].Direction == ParameterDirection.ReturnValue;
		/// <summary>Has this procedure a result.</summary>
		public abstract bool HasResult { get; }

		/// <summary>Parameter information of this table.</summary>
		public IEnumerable<PpsSqlParameterInfo> Parameters => parameters;
		/// <summary>Number of arguments</summary>
		public int ParameterCount => parameters.Count;
	} // class PpsSqlProcedureInfo

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
				private static readonly string[,] translateTable = new string[,]
				{
					{ "MaxLength", "ColumnSize" },
					{ "Nullable", "AllowDBNull" }
				};

				private readonly SqlDataResultColumnDescription column;

				public PpsDataResultColumnAttributes(SqlDataResultColumnDescription column)
				{
					this.column = column;
				} // ctor

				private bool TryTranslateAttribute(string attributeName, int sourceIndex, int returnIndex, out string rowName)
				{
					for (var i = 0; i < translateTable.GetLength(0); i++)
					{
						if (String.Compare(translateTable[i, sourceIndex], attributeName, StringComparison.OrdinalIgnoreCase) == 0)
						{
							rowName = translateTable[i, returnIndex];
							return true;
						}
					}

					rowName = attributeName;
					return false;
				} // func TryTranslateAttribute

				private bool TryTranslateFromAttribute(string attributeName, out string rowName)
					=> TryTranslateAttribute(attributeName, 0, 1, out rowName);

				private bool TryTranslateToAttribute(string rowName, out string attributeName)
					=> TryTranslateAttribute(rowName, 1, 0, out attributeName);

				public bool TryGetProperty(string name, out object value)
				{
					// find value translation
					if (TryTranslateFromAttribute(name, out var rowName))
					{
						value = column.row[rowName];
						return value != DBNull.Value;
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
						{
							TryTranslateToAttribute(c.ColumnName, out var attributeName);
							yield return new PropertyValue(attributeName, column.row[c]);
						}
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
		protected sealed class PpsSqlDataSelectorToken : IPpsSelectorToken, IPpsSqlTableOrView
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
				=> CreateSelector(connection, null, throwException);

			/// <summary></summary>
			/// <param name="connection"></param>
			/// <param name="throwException"></param>
			/// <param name="aliasName"></param>
			/// <returns></returns>
			public PpsDataSelector CreateSelector(IPpsConnectionHandle connection, string aliasName = null, bool throwException = true)
				=> new PpsSqlDataSelector((IPpsSqlConnectionHandle)connection, this, aliasName);

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

			/// <summary></summary>
			public IReadOnlyCollection<IPpsColumnDescription> Columns => columnDescriptions;

			PpsDataSource IPpsSelectorToken.DataSource => DataSource;

			IReadOnlyList<IDataColumn> IDataColumns.Columns => columnDescriptions;

			string IPpsSqlTableOrView.TableName => name;
			string IPpsSqlTableOrView.QualifiedName => viewName;

			IReadOnlyList<IPpsColumnDescription> IPpsSqlTableOrView.Columns => columnDescriptions;
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

		#region -- class PpsSqlJoinExpression -----------------------------------------

		/// <summary>Implementation of the join expression for SQL.</summary>
		protected sealed class PpsSqlJoinExpression : PpsDataJoinExpression<IPpsSqlTableOrView>
		{
			#region -- class SqlEmitVisitor -------------------------------------------

			private sealed class SqlEmitVisitor : PpsJoinVisitor<string>
			{
				public override string CreateJoinStatement(string leftExpression, PpsDataJoinType type, string rightExpression, PpsDataJoinStatement[] on)
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

					return "(" + leftExpression + GetJoinExpr() + rightExpression + " ON (" + CreateOnStatement(on) + "))";
				} // func CreateJoinStatement

				private string CreateOnStatement(PpsDataJoinStatement[] on)
					=> String.Join(" AND ", on.Select(c => c.Left + "=" + c.Right));

				public override string CreateTableStatement(IPpsSqlTableOrView table, string alias)
					=> String.IsNullOrEmpty(alias)
						? table.QualifiedName
						: table.QualifiedName + " AS " + alias;
			} // class SqlEmitVisitor

			#endregion

			#region -- class AliasColumnExpression ------------------------------------

			private sealed class AliasColumnExpression : IPpsSqlAliasColumn
			{
				private readonly PpsTableExpression table;
				private readonly IPpsColumnDescription column;

				public AliasColumnExpression(PpsTableExpression table, IPpsColumnDescription column)
				{
					this.table = table ?? throw new ArgumentNullException(nameof(table));
					this.column = column ?? throw new ArgumentNullException(nameof(column));
				} // ctor

				public string Alias => table.Alias;
				public string Expression => GetColumnExpression(table, column);
				public Type DataType => column.DataType;
			} // class AliasColumnExpression

			#endregion

			private readonly PpsSqlDataSource dataSource;

			/// <summary></summary>
			/// <param name="dataSource"></param>
			/// <param name="part"></param>
			private PpsSqlJoinExpression(PpsSqlDataSource dataSource, PpsExpressionPart part)
				: base(part)
			{
				this.dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
			} // ctor

			/// <summary></summary>
			/// <param name="dataSource"></param>
			/// <param name="tableOrView"></param>
			/// <param name="aliasName"></param>
			public PpsSqlJoinExpression(PpsSqlDataSource dataSource, IPpsSqlTableOrView tableOrView, string aliasName)
				: base(tableOrView, aliasName)
			{
				this.dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
			} // ctor

			/// <summary></summary>
			/// <param name="dataSource"></param>
			/// <param name="expression"></param>
			public PpsSqlJoinExpression(PpsSqlDataSource dataSource, string expression)
			{
				this.dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
				Parse(expression);
			} // ctor

			/// <summary></summary>
			/// <param name="table"></param>
			/// <param name="column"></param>
			/// <returns></returns>
			public static string GetColumnExpression(PpsTableExpression table, IPpsColumnDescription column)
				=> String.IsNullOrEmpty(table.Alias)
					? table.Table.QualifiedName + ".[" + column.Name + "]"
					: table.Alias + ".[" + column.Name + "]";

			/// <summary>Create automatic on statement.</summary>
			/// <param name="left"></param>
			/// <param name="joinOp"></param>
			/// <param name="right"></param>
			/// <returns></returns>
			protected override PpsDataJoinStatement[] CreateOnStatement(PpsTableExpression left, PpsDataJoinType joinOp, PpsTableExpression right)
			{
				if (right.Table is PpsSqlTableInfo rightTable
					&& left.Table is PpsSqlTableInfo leftTable)
				{
					var returnStatements = new List<PpsDataJoinStatement>();
					foreach (var r in rightTable.RelationInfo)
					{
						if (r.ReferencedColumn.Table == leftTable)
						{
							var sb = new StringBuilder();
							returnStatements.Add(new PpsDataJoinStatement(
								GetColumnExpression(left, r.ReferencedColumn),
								GetColumnExpression(right, r.ParentColumn)
							));
						}
					}
					return returnStatements.ToArray();
				}

				return null;
			} // func CreateOnStatement

			/// <summary>Resolve table by name</summary>
			/// <param name="tableName"></param>
			/// <returns></returns>
			protected override IPpsSqlTableOrView ResolveTable(string tableName)
				=> dataSource.ResolveTableByName<PpsSqlTableInfo>(tableName, true); // todo: views?

			private PpsDataJoinStatement[] CreateOnStatement(PpsSqlJoinExpression right, PpsDataJoinStatement[] statements)
			{
				if (statements == null || statements.Length == 0)
					throw new ArgumentNullException(nameof(statements), "No statements");

				var sb = new StringBuilder();

				var returnStatements = new PpsDataJoinStatement[statements.Length];
				for (var i = 0; i < statements.Length; i++)
				{
					var (leftTable, leftColumn) = FindNativeColumn(statements[i].Left, true);
					var (rightTable, rightColumn) = right.FindNativeColumn(statements[i].Right, true);

					returnStatements[i] = new PpsDataJoinStatement(
						GetColumnExpression(leftTable, leftColumn),
						GetColumnExpression(rightTable, rightColumn)
					);
				}

				return returnStatements;
			} // func CreateOnStatement

			/// <summary>Attache a new table join</summary>
			/// <param name="expr"></param>
			/// <param name="aliasName"></param>
			/// <param name="joinType"></param>
			/// <param name="statements"></param>
			/// <returns></returns>
			public PpsSqlJoinExpression Combine(PpsSqlJoinExpression expr, string aliasName, PpsDataJoinType joinType, PpsDataJoinStatement[] statements)
				=> new PpsSqlJoinExpression(dataSource, AppendCore(expr, aliasName, joinType, CreateOnStatement(expr, statements)));

			#region -- Find Column ----------------------------------------------------

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
			/// <param name="name"></param>
			/// <param name="throwException"></param>
			/// <returns></returns>
			public (PpsTableExpression table, IPpsColumnDescription nativeColumn) FindNativeColumn(string name, bool throwException)
			{
				SplitColumnName(name, out var alias, out var columnName);

				Func<PpsTableExpression, IPpsColumnDescription, bool> predicate;

				if (alias != null)
				{
					predicate = (t, c) =>
						t.Alias != null
							&& String.Compare(alias, t.Alias, StringComparison.OrdinalIgnoreCase) == 0
							&& String.Compare(c.Name, columnName, StringComparison.OrdinalIgnoreCase) == 0;
				}
				else
				{
					predicate = (t, c) => String.Compare(c.Name, columnName, StringComparison.OrdinalIgnoreCase) == 0;
				}

				var r = FindNativeColumn(predicate);
				if (r.nativeColumn == null && throwException)
					throw new ArgumentException($"Column not found ({name}).");
				return r;
			} // func GetNativeColumn

			/// <summary></summary>
			/// <param name="columnDescription"></param>
			/// <param name="throwException"></param>
			/// <returns></returns>
			public (PpsTableExpression table, IPpsColumnDescription nativeColumn) FindNativeColumn(IPpsColumnDescription columnDescription, bool throwException)
			{
				var r = FindNativeColumn((t, c) => columnDescription.TryGetColumnDescriptionImplementation<PpsSqlColumnInfo>(out var sqlColumn) && t.Table == sqlColumn.Table && c.Name == sqlColumn.Name);
				if (r.nativeColumn == null && throwException)
					throw new ArgumentException($"Column not found ({columnDescription.Name}).");
				return r;
			} // func FindNativeColumn

			/// <summary></summary>
			/// <param name="name"></param>
			/// <param name="throwException"></param>
			/// <returns></returns>
			public IPpsSqlAliasColumn GetAliasColumn(string name, bool throwException)
			{
				var (table, nativeColumn) = FindNativeColumn(name, throwException);
				return table == null ? null : new AliasColumnExpression(table, nativeColumn);
			} // func GetAliasColumn

			private (PpsTableExpression table, IPpsColumnDescription nativeColumn) FindNativeColumn(Func<PpsTableExpression, IPpsColumnDescription, bool> predicate)
			{
				foreach (var tableExpr in GetTables())
				{
					var col = tableExpr.Table.Columns.FirstOrDefault(c => predicate(tableExpr, c));
					if (col != null)
						return (tableExpr, col);
				}
				return (null, null);
			} // func FindNativeColumn

			#endregion

			/// <summary>Compile full join.</summary>
			/// <returns></returns>
			public string EmitJoin()
				=> new SqlEmitVisitor().Visit(this);
		} // class PpsSqlJoinExpression

		#endregion

		#region -- class PpsSqlDataSelector -------------------------------------------

		/// <summary></summary>
		private sealed class PpsSqlDataSelector : PpsDataSelector
		{
			#region -- class SelectColumn ---------------------------------------------

			private sealed class SelectColumn : AliasColumn, IPpsSqlAliasColumn
			{
				public SelectColumn(string expression, IPpsColumnDescription nativeColumn, string aliasName)
					: base(nativeColumn, aliasName ?? throw new ArgumentNullException(nameof(aliasName)))
				{
					Expression = expression ?? throw new ArgumentNullException(nameof(expression));
				} // ctor

				public string Expression { get; }
			} // class SelectColumn

			#endregion

			#region -- class WhereConditionStore --------------------------------------

			private sealed class WhereConditionStore
			{
				private readonly PpsDataFilterExpression condition;
				private readonly Func<string, string>[] nativeLookupList;

				public WhereConditionStore(WhereConditionStore oldStore, WhereConditionStore otherStore)
				{
					condition = PpsDataFilterExpression.Combine(oldStore.condition, otherStore.condition);
					nativeLookupList = NativeLookupListCombine(oldStore.nativeLookupList, otherStore.nativeLookupList);
				} // ctor

				public WhereConditionStore(WhereConditionStore oldStore, PpsDataFilterExpression whereCondition, Func<string, string> nativeLookup)
				{
					if (whereCondition != null && whereCondition != PpsDataFilterExpression.True)
					{
						condition = PpsDataFilterExpression.Combine(oldStore.Expression, whereCondition);
						nativeLookupList = NativeLookupListCombine(oldStore.nativeLookupList, nativeLookup);
					}
					else if (oldStore != null)
					{
						condition = oldStore.condition;
						nativeLookupList = oldStore.nativeLookupList;
					}
					else
					{
						condition = PpsDataFilterExpression.True;
						nativeLookupList = null;
					}
				} // ctor

				public string NativeLookup(string expr)
					=> NativeLookupListImpl(nativeLookupList, expr);

				public PpsDataFilterExpression Expression => condition;

				public static WhereConditionStore Empty { get; } = new WhereConditionStore(null, PpsDataFilterExpression.True, null);
			} // class WhereConditionStore

			#endregion

			#region -- class OrderByStore ---------------------------------------------

			private sealed class OrderByStore
			{
				private readonly PpsDataOrderExpression[] orderBy;
				private readonly Func<string, string>[] nativeLookupList;

				public OrderByStore(OrderByStore oldStore, OrderByStore otherStore)
				{
					orderBy = oldStore.orderBy.Union(otherStore.orderBy).ToArray();
					nativeLookupList = NativeLookupListCombine(oldStore.nativeLookupList, otherStore.nativeLookupList);
				} // ctor

				public OrderByStore(OrderByStore oldStore, IEnumerable<PpsDataOrderExpression> orderBy, Func<string, string> nativeLookup)
				{
					if (orderBy != null)
					{
						this.orderBy = oldStore.orderBy.Union(orderBy).ToArray();
						this.nativeLookupList = NativeLookupListCombine(oldStore.nativeLookupList, nativeLookup);
					}
					else if (oldStore != null)
					{
						this.orderBy = oldStore.orderBy;
						this.nativeLookupList = oldStore.nativeLookupList;
					}
					else
					{
						this.orderBy = Array.Empty<PpsDataOrderExpression>();
						this.nativeLookupList = null;
					}
				} // ctor

				public bool IsOrderDesc(string columnName)
					=> orderBy.FirstOrDefault(c => String.Compare(c.Identifier, columnName, StringComparison.OrdinalIgnoreCase) == 0)?.Negate ?? false;

				public string NativeLookup(string expr)
					=> NativeLookupListImpl(nativeLookupList, expr);

				public IEnumerable<PpsDataOrderExpression> Expression => orderBy;

				public static OrderByStore Empty { get; } = new OrderByStore(null, null, null);
			} // class OrderByStore

			private static string NativeLookupListImpl(Func<string, string>[] nativeLookupList, string expr)
			{
				if (nativeLookupList == null)
					return null;

				return nativeLookupList.Select(f => f(expr)).FirstOrDefault(c => c != null);
			} // func NativeLookupListImpl

			private static Func<string, string>[] NativeLookupListCombine(Func<string, string>[] a, Func<string, string>[] b)
			{
				if (a == null)
					return b;
				else if (b == null)
					return a;
				else
				{
					var newArray = new Func<string, string>[a.Length + b.Length];
					Array.Copy(a, 0, newArray, 0, a.Length);
					Array.Copy(b, 0, newArray, a.Length, b.Length);
					return newArray;
				}
			} // func NativeLookupListCombine

			private static Func<string, string>[] NativeLookupListCombine(Func<string, string>[] nativeLookupList, Func<string, string> nativeLookup)
			{
				if (nativeLookupList == null)
				{
					if (nativeLookup == null)
						return null;
					else
						return new Func<string, string>[] { nativeLookup };
				}
				else
				{
					if (nativeLookup == null)
						return nativeLookupList;
					else
					{
						var nativeLookupListLength = nativeLookupList.Length;
						var newArray = new Func<string, string>[nativeLookupListLength + 1];
						Array.Copy(nativeLookupList, newArray, nativeLookupListLength);
						newArray[nativeLookupListLength] = nativeLookup;
						return newArray;
					}
				}

			} // func NativeLookupListCombine

			#endregion

			private readonly PpsSqlJoinExpression from;
			private readonly WhereConditionStore whereCondition;
			private readonly OrderByStore orderBy;

			#region -- Ctor/Dtor ------------------------------------------------------

			/// <summary></summary>
			/// <param name="connection"></param>
			/// <param name="viewOrTable"></param>
			/// <param name="tableAlias"></param>
			public PpsSqlDataSelector(IPpsSqlConnectionHandle connection, IPpsSqlTableOrView viewOrTable, string tableAlias)
				: base(connection, GetAliasColumns(viewOrTable, tableAlias).ToArray())
			{
				this.from = new PpsSqlJoinExpression((PpsSqlDataSource)connection.DataSource, viewOrTable, tableAlias);
				this.whereCondition = WhereConditionStore.Empty;
				this.orderBy = OrderByStore.Empty;
			} // ctor

			/// <summary></summary>
			/// <param name="connection"></param>
			/// <param name="columns"></param>
			/// <param name="from"></param>
			/// <param name="whereCondition"></param>
			/// <param name="orderBy"></param>
			private PpsSqlDataSelector(IPpsSqlConnectionHandle connection, AliasColumn[] columns, PpsSqlJoinExpression from, WhereConditionStore whereCondition, OrderByStore orderBy)
				: base(connection, columns ?? throw new ArgumentNullException(nameof(columns)))
			{
				this.from = from ?? throw new ArgumentNullException(nameof(from));
				this.whereCondition = whereCondition ?? WhereConditionStore.Empty;
				this.orderBy = orderBy ?? OrderByStore.Empty;
			} // ctor

			#endregion

			#region -- Apply Columns --------------------------------------------------

			private static IEnumerable<AliasColumn> GetAliasColumns(IPpsSqlTableOrView tableOrView, string tableAlias)
			{
				var hasAlias = !String.IsNullOrEmpty(tableAlias);
				foreach (var col in tableOrView.Columns)
				{
					if (hasAlias)
						yield return new SelectColumn(GetColumnExpression(tableAlias, col.Name), col, col.Name);
					else
						yield return new SelectColumn(GetColumnExpression(null, col.Name), col, col.Name);
				}
			} // func GetAliasColumns

			private static string GetColumnExpression(string tableAlias, string name)
				=> tableAlias == null ? FormatColumn(name) : tableAlias + "." + FormatColumn(name);

			protected override AliasColumn CreateColumnAliasFromExisting(PpsDataColumnExpression col, AliasColumn aliasColumn)
			{
				if (aliasColumn is SelectColumn selectColumn)
				{
					if (col.HasAlias) // rename column
						return new SelectColumn(selectColumn.Expression, selectColumn.NativeColumnInfo, col.Alias);
					else // nothing to change, just copy reference
						return aliasColumn;
				}
				else
					throw new InvalidOperationException("Wrong type.");
			} // func CreateColumnAliasFromExisting

			protected override AliasColumn CreateColumnAliasFromNative(PpsDataColumnExpression col)
			{
				var (table, nativeColumn) = from.FindNativeColumn(col.Name, false);
				if (nativeColumn == null)
					return null;

				return new SelectColumn(GetColumnExpression(table.Alias, nativeColumn.Name), nativeColumn, col.HasAlias ? col.Alias : col.Name);
			} // func CreateColumnAliasFromNative

			protected override PpsDataSelector ApplyColumnsCore(AliasColumn[] columns)
				=> new PpsSqlDataSelector(SqlConnection, columns, from, whereCondition, orderBy);

			#endregion

			public sealed override PpsDataSelector ApplyFilter(PpsDataFilterExpression expression, Func<string, string> lookupNative = null)
				=> new PpsSqlDataSelector(SqlConnection, AliasColumns, from, new WhereConditionStore(whereCondition, expression, lookupNative), orderBy);

			public sealed override bool IsOrderDesc(string columnName)
				=> orderBy.IsOrderDesc(columnName);

			public sealed override PpsDataSelector ApplyOrder(IEnumerable<PpsDataOrderExpression> expressions, Func<string, string> lookupNative = null)
				=> new PpsSqlDataSelector(SqlConnection, AliasColumns, from, whereCondition, new OrderByStore(orderBy, expressions, lookupNative));

			public sealed override PpsDataSelector ApplyJoin(PpsDataSelector selector, PpsDataJoinType joinType, PpsDataJoinStatement[] statements)
			{
				return selector is PpsSqlDataSelector tableOrView
					? ApplyJoin(tableOrView, null, joinType, statements)
					: base.ApplyJoin(selector, joinType, statements);
			} // func ApplyJoin

			public PpsDataSelector ApplyJoin(PpsSqlDataSelector sqlSelector, string aliasName, PpsDataJoinType joinType, PpsDataJoinStatement[] statements)
			{
				if (sqlSelector.DataSource != DataSource) // teste datasource
					return base.ApplyJoin(sqlSelector, joinType, statements);

				return new PpsSqlDataSelector(SqlConnection,
					AliasColumns.Concat(sqlSelector.AliasColumns).ToArray(),
					from.Combine(sqlSelector.from, aliasName, joinType, statements),
					new WhereConditionStore(whereCondition, sqlSelector.whereCondition),
					new OrderByStore(orderBy, sqlSelector.orderBy)
				);
			} // func ApplyJoin

			protected sealed override IEnumerator<IDataRow> GetEnumeratorCore(int start, int count)
				=> new DbRowEnumerator(((PpsSqlDataSource)DataSource).CreateViewCommand(SqlConnection, Columns, from, whereCondition.Expression, whereCondition.NativeLookup, orderBy.Expression, orderBy.NativeLookup, start, count));

			/// <summary>Access sql connection handle.</summary>
			private IPpsSqlConnectionHandle SqlConnection => (IPpsSqlConnectionHandle)Connection;
		} // class SqlDataSelector

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
			[DebuggerDisplay("{DebuggerDisplay,nq}")]
			protected sealed class ParameterMapping
			{
				private readonly DbParameter parameter;
				private readonly Type dataType;
				private readonly object defaultValue;
				private readonly Func<object, object> getParameterCore;
				private readonly Action<object, object> setParameterCore;

				private ParameterMapping(DbParameter parameter, Type dataType, object defaultValue, Func<object, object> getParameterCore, Action<object, object> setParameterCore)
				{
					this.parameter = parameter ?? throw new ArgumentNullException(nameof(parameter));
					this.dataType = dataType ?? throw new ArgumentNullException(nameof(dataType));
					this.defaultValue = defaultValue;

					this.getParameterCore = getParameterCore;
					this.setParameterCore = setParameterCore;
				} // ctor

				/// <summary></summary>
				/// <param name="parameterName"></param>
				/// <returns></returns>
				public bool IsParameter(string parameterName)
					=> String.Compare(parameter.ParameterName, parameterName, StringComparison.OrdinalIgnoreCase) == 0;

				/// <summary>Set parameter value</summary>
				/// <param name="args"></param>
				public void UpdateParameter(object args)
				{
					if ((parameter.Direction & ParameterDirection.Input) == ParameterDirection.Input)
						parameter.SetValue(getParameterCore?.Invoke(args), dataType, defaultValue);
				} // proc UpdateParameter
				 
				/// <summary>Set source value</summary>
				/// <param name="args"></param>
				public void UpdateSource(object args)
				{
					if ((parameter.Direction & ParameterDirection.Output) == ParameterDirection.Output)
						setParameterCore?.Invoke(args, parameter.Value.NullIfDBNull());
				} // proc UpdateSource

				private string DebuggerDisplay
					=> $"Mapping: {parameter.ParameterName}";

				/// <summary></summary>
				public DbParameter Parameter => parameter;

				/// <summary></summary>
				/// <param name="parameter"></param>
				/// <param name="dataType"></param>
				/// <param name="defaultValue"></param>
				/// <returns></returns>
				public static ParameterMapping CreateNull(DbParameter parameter, Type dataType, object defaultValue)
				{
					return new ParameterMapping(
						parameter,
						dataType,
						defaultValue,
						null,
						null
					);
				} // func CreateTableName

				/// <summary></summary>
				/// <param name="name"></param>
				/// <param name="parameter"></param>
				/// <param name="dataType"></param>
				/// <param name="defaultValue"></param>
				/// <returns></returns>
				public static ParameterMapping CreateTableName(string name, DbParameter parameter, Type dataType, object defaultValue)
				{
					return new ParameterMapping(
						parameter,
						dataType,
						defaultValue,
						t => ((LuaTable)t).GetMemberValue(name, ignoreCase: true),
						(t, v) => ((LuaTable)t).SetMemberValue(name, v, ignoreCase: true)
					);
				} // func CreateTableName

				/// <summary></summary>
				/// <param name="index"></param>
				/// <param name="parameter"></param>
				/// <param name="dataType"></param>
				/// <param name="defaultValue"></param>
				/// <returns></returns>
				public static ParameterMapping CreateTableIndex(int index, DbParameter parameter, Type dataType, object defaultValue)
				{
					return new ParameterMapping(
						parameter,
						dataType,
						defaultValue,
						t => ((LuaTable)t).GetArrayValue(index),
						(t, v) => ((LuaTable)t).SetArrayValue(index, v)
					);
				} // func CreateTableIndex

				/// <summary></summary>
				/// <param name="name"></param>
				/// <param name="parameter"></param>
				/// <param name="dataType"></param>
				/// <param name="defaultValue"></param>
				/// <returns></returns>
				public static ParameterMapping CreateRowName(string name, DbParameter parameter, Type dataType, object defaultValue)
				{
					Func<object, object> getRowValueCore = null;
					Action<object, object> setRowValueCore = null;

					if (typeof(IPpsDataRowExtendedValue).IsAssignableFrom(dataType))
					{
						if (typeof(IPpsDataRowGetGenericValue).IsAssignableFrom(dataType))
						{
							getRowValueCore = r => ((IPpsDataRowGetGenericValue)((PpsDataRow)r)[name]).Value;
							if (typeof(IPpsDataRowSetGenericValue).IsAssignableFrom(dataType))
								setRowValueCore = (r, v) => ((IPpsDataRowSetGenericValue)((PpsDataRow)r)[name]).SetGenericValue(false, v);
						}
					}
					else
					{
						getRowValueCore = r => ((IDataRow)r)[name, false];
						setRowValueCore = (r, v) =>
						{
							if (r is PpsDataRow pr)
								pr[name, false] = v;
						};
					}

					return new ParameterMapping(
						parameter,
						dataType,
						defaultValue,
						getRowValueCore,
						setRowValueCore
					);
				} // func CreateRowName

				/// <summary></summary>
				/// <param name="index"></param>
				/// <param name="parameter"></param>
				/// <param name="dataType"></param>
				/// <param name="defaultValue"></param>
				/// <returns></returns>
				public static ParameterMapping CreateRowIndex(int index, DbParameter parameter, Type dataType, object defaultValue)
				{
					Func<object, object> getRowValueCore = null;
					Action<object, object> setRowValueCore = null;

					if (typeof(IPpsDataRowExtendedValue).IsAssignableFrom(dataType))
					{
						if (typeof(IPpsDataRowGetGenericValue).IsAssignableFrom(dataType))
						{
							getRowValueCore = r => ((IPpsDataRowGetGenericValue)((PpsDataRow)r)[index]).Value;
							if (typeof(IPpsDataRowSetGenericValue).IsAssignableFrom(dataType))
								setRowValueCore = (r, v) => ((IPpsDataRowSetGenericValue)((PpsDataRow)r)[index]).SetGenericValue(false, v);
						}
					}
					else
					{
						getRowValueCore = r => ((IDataRow)r)[index];
						setRowValueCore = (r, v) =>
						{
							if (r is PpsDataRow pr)
								pr[index] = v;
						};
					}

					return new ParameterMapping(
						parameter,
						dataType,
						defaultValue,
						getRowValueCore,
						setRowValueCore
					);
				} // func CreateRowIndex
			} // class ParameterMapping

			#endregion

			#region -- class ResultMapping --------------------------------------------

			/// <summary></summary>
			protected sealed class ResultMapping
			{
				private readonly string columnName;
				private readonly Action<object, object> updateValue;

				private ResultMapping(string columnName, Action<object,object> updateValue)
				{
					this.columnName = columnName ?? throw new ArgumentNullException(nameof(columnName));
					this.updateValue = updateValue ?? throw new ArgumentNullException(nameof(columnName));
				} // ctor

				/// <summary></summary>
				/// <param name="args"></param>
				/// <param name="value"></param>
				public void UpdateSource(object args, object value)
					=> updateValue?.Invoke(args, value);

				/// <summary></summary>
				public string ColumnName => columnName;

				/// <summary></summary>
				/// <param name="name"></param>
				/// <returns></returns>
				public static ResultMapping CreateTableName(string name)
					=> new ResultMapping(name, (t, v) => ((LuaTable)t)[name] = v);

				/// <summary></summary>
				/// <param name="name"></param>
				/// <param name="dataType"></param>
				/// <returns></returns>
				public static ResultMapping CreateRowName(string name, Type dataType)
				{
					Action<object, object> setRowValueCore = null;

					if (typeof(IPpsDataRowExtendedValue).IsAssignableFrom(dataType))
					{
						if (typeof(IPpsDataRowGetGenericValue).IsAssignableFrom(dataType)
							&& typeof(IPpsDataRowSetGenericValue).IsAssignableFrom(dataType))
								setRowValueCore = (r, v) => ((IPpsDataRowSetGenericValue)((PpsDataRow)r)[name]).SetGenericValue(false, v);
					}
					else
					{
						setRowValueCore = (r, v) =>
						{
							if (r is PpsDataRow pr)
								pr[name, false] = v;
						};
					}

					return new ResultMapping(name, setRowValueCore);
				} // func CreateRowName
			} // class ResultMapping

			#endregion

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

			#region -- class PpsSqlDataCommand ----------------------------------------

			/// <summary></summary>
			protected sealed class PpsSqlDataCommand : PpsDataCommand
			{
				private readonly bool noTransaction;
				private readonly DBCOMMAND command;
				private readonly LuaTable defaults;
				private readonly List<ParameterMapping> parameterMappings = new List<ParameterMapping>();
				private readonly List<ResultMapping> resultMappings = new List<ResultMapping>();

				#region -- Ctor/Dtor --------------------------------------------------

				/// <summary></summary>
				/// <param name="transaction"></param>
				/// <param name="parameter"></param>
				/// <param name="command"></param>
				public PpsSqlDataCommand(PpsDataTransaction transaction, LuaTable parameter, DBCOMMAND command)
					: base(transaction)
				{
					this.command = command;

					noTransaction = command.Transaction == null;
					defaults = parameter.GetMemberValue("defaults") as LuaTable;
				} // ctor

				/// <summary></summary>
				/// <param name="disposing"></param>
				protected override void Dispose(bool disposing)
				{
					if (disposing)
						command?.Dispose();
					base.Dispose(disposing);
				} // proc Dispose

				/// <summary></summary>
				/// <param name="commandText"></param>
				public PpsSqlDataCommand Prepare(string commandText)
				{
					command.CommandText = commandText;
					command.Prepare();
					return this;
				} // proc Prepare

				#endregion

				#region -- Parameter Mapping ------------------------------------------

				/// <summary></summary>
				/// <param name="mapping"></param>
				public void AppendParameter(ParameterMapping mapping)
				{
					if (!command.Parameters.Contains(mapping.Parameter))
						command.Parameters.Add(mapping.Parameter);
					parameterMappings.Add(mapping);
				} // proc AppendParameter

				/// <summary></summary>
				/// <param name="parameterName"></param>
				/// <returns></returns>
				public bool ExistsParameter(string parameterName)
					=> parameterMappings.Exists(m => m.IsParameter(parameterName));

				/// <summary></summary>
				/// <param name="mapping"></param>
				public void AppendResult(ResultMapping mapping)
					=> resultMappings.Add(mapping);

				#endregion

				#region -- ExecuteResultCore ------------------------------------------

				/// <summary></summary>
				/// <param name="cmd"></param>
				/// <param name="behavior"></param>
				/// <returns></returns>
				private DBDATAREADER ExecuteReaderCommand<DBDATAREADER>(DbCommand cmd, PpsDataTransactionExecuteBehavior behavior)
					where DBDATAREADER : DbDataReader
				{
					switch (behavior)
					{
						case PpsDataTransactionExecuteBehavior.NoResult:
							cmd.ExecuteNonQueryEx();
							return null;
						case PpsDataTransactionExecuteBehavior.SingleRow:
							return (DBDATAREADER)cmd.ExecuteReaderEx(CommandBehavior.SingleRow);
						case PpsDataTransactionExecuteBehavior.SingleResult:
							return (DBDATAREADER)cmd.ExecuteReaderEx(CommandBehavior.SingleResult);
						default:
							return (DBDATAREADER)cmd.ExecuteReaderEx(CommandBehavior.Default);
					}
				} // func ExecuteReaderCommand
				
				/// <summary></summary>
				/// <param name="args"></param>
				/// <param name="behavior"></param>
				/// <returns></returns>
				protected sealed override IEnumerable<IEnumerable<IDataRow>> ExecuteResultCore(object args, PpsDataTransactionExecuteBehavior behavior)
				{
					if (parameterMappings.Count > 0 && args == null)
						throw new ArgumentNullException(nameof(args), "Arguments are missing.");

					command.Transaction = ((PpsSqlDataTransaction<DBCONNECTION, DBTRANSACTION, DBCOMMAND>)Transaction).DbTransaction;

					// fill arguments
					foreach (var p in parameterMappings)
						p.UpdateParameter(args);
					
					using (var r = ExecuteReaderCommand<DbDataReader>(command, 
						resultMappings.Count > 0 && behavior == PpsDataTransactionExecuteBehavior.NoResult ? PpsDataTransactionExecuteBehavior.SingleRow :  behavior
					))
					{
						// copy arguments back
						foreach (var p in parameterMappings)
							p.UpdateSource(args);

						// return results
						if (r != null)
						{
							// catch first result
							if (resultMappings.Count > 0 && r.Read())
							{
								for (var i = 0; i < r.FieldCount; i++)
								{
									var n = r.GetName(i);
									var f = resultMappings.FirstOrDefault(c => String.Compare(c.ColumnName, n, StringComparison.OrdinalIgnoreCase) == 0);
									if (f != null)
										f.UpdateSource(args, r.GetValue(i).NullIfDBNull());
								}
							}

							do
							{
								var t = new DbRowReaderEnumerable(r);
								yield return defaults != null ? (IEnumerable<IDataRow>)new DefaultRowEnumerable(defaults, t) : t;

								if (behavior == PpsDataTransactionExecuteBehavior.SingleResult)
									break;
							} while (r.NextResult());
						}
					} // using r
				} // func ExecuteResultCore

				#endregion

				/// <summary></summary>
				public bool HasDefaults => defaults != null;
				/// <summary></summary>
				public IReadOnlyList<ParameterMapping> ParameterMappings => parameterMappings;
				/// <summary></summary>
				public IReadOnlyList<ResultMapping> ResultMappings => resultMappings;
				/// <summary></summary>
				public DBCOMMAND Command => command;
			} // class PpsSqlDataCommand

			#endregion

			private readonly DBCONNECTION connection;
			private DBTRANSACTION transaction;

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
				this.transaction = CreateTransaction();
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

			/// <summary>Overwrite to create a new transaction.</summary>
			/// <returns></returns>
			protected DBTRANSACTION CreateTransaction()
				=> (DBTRANSACTION)this.connection.BeginTransaction(IsolationLevel.ReadUncommitted);

			private DBTRANSACTION GetTransaction()
			{
				if (IsCommited.HasValue)
					ResetTransaction();
				return transaction;
			} // func GetTransaction

			/// <summary>Set a new transaction.</summary>
			protected internal override void ResetTransaction()
			{
				if (IsCommited.HasValue)
				{
					transaction?.Dispose();
					transaction = CreateTransaction();
				}
				base.ResetTransaction();
			} // proc ResetTransaction

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

			/// <summary></summary>
			/// <param name="procedureName"></param>
			/// <param name="throwException"></param>
			/// <returns></returns>
			public PpsSqlProcedureInfo FindProcedure(string procedureName, bool throwException = true)
				=> ((PpsSqlDataSource)DataSource).ResolveProcedureByName<PpsSqlProcedureInfo>(procedureName, throwException);

			/// <summary></summary>
			/// <param name="tableName"></param>
			/// <param name="throwException"></param>
			/// <returns></returns>
			public PpsSqlTableInfo FindTable(string tableName, bool throwException = true)
				=> ((PpsSqlDataSource)DataSource).ResolveTableByName<PpsSqlTableInfo>(tableName, throwException);

			/// <summary>Create a command</summary>
			/// <param name="commandType"></param>
			/// <param name="noTransaction"></param>
			/// <returns></returns>
			public virtual DBCOMMAND CreateCommand(CommandType commandType, bool noTransaction)
			{
				var cmd = (DBCOMMAND)connection.CreateCommand();
				cmd.CommandTimeout = 7200;
				cmd.CommandType = commandType;
				cmd.Transaction = noTransaction ? null : GetTransaction();
				return cmd;
			} // func CreateCommand

			/// <summary></summary>
			/// <param name="commandType"></param>
			/// <param name="parameter"></param>
			/// <returns></returns>
			protected PpsSqlDataCommand CreateCommand(CommandType commandType, LuaTable parameter)
				=> new PpsSqlDataCommand(this, parameter, CreateCommand(commandType, parameter.GetOptionalValue("__notrans", false)));
				
			#endregion

			#region -- CreateParameter ------------------------------------------------

			private static T GetColumnInfoAttribute<T>(IDataColumn columnInfo, string name, T @default)
				=> columnInfo == null ? @default : columnInfo.Attributes.GetProperty(name, @default);

			/// <summary></summary>
			/// <param name="parameter"></param>
			/// <param name="dataType"></param>
			/// <param name="columnInfo"></param>
			protected virtual void SetSqlParameterType(DbParameter parameter, Type dataType, IDataColumn columnInfo = null)
			{
				switch (System.Type.GetTypeCode(dataType))
				{
					case TypeCode.String:
						parameter.Size = GetColumnInfoAttribute(columnInfo, "maxLength", 32000);
						parameter.DbType = DbType.String;
						break;
					case TypeCode.Boolean:
						parameter.DbType = DbType.Boolean;
						break;
					case TypeCode.DateTime:
						parameter.DbType = DbType.DateTime2;
						break;

					case TypeCode.Single:
						parameter.Precision = GetColumnInfoAttribute(columnInfo, "Precision", (byte)20);
						parameter.Scale = GetColumnInfoAttribute(columnInfo, "Scale", (byte)10);
						parameter.DbType = DbType.Single;
						break;
					case TypeCode.Double:
						parameter.Precision = GetColumnInfoAttribute(columnInfo, "Precision", (byte)15);
						parameter.Scale = GetColumnInfoAttribute(columnInfo, "Scale", (byte)7);
						parameter.DbType = DbType.Double;
						break;
					case TypeCode.Decimal:
						parameter.Precision = GetColumnInfoAttribute(columnInfo, "Precision", (byte)7);
						parameter.Scale = GetColumnInfoAttribute(columnInfo, "Scale", (byte)3);
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
						if (dataType == typeof(Guid))
							parameter.DbType = DbType.Guid;
						else
							parameter.DbType = DbType.Int32;
						break;
				}
			} // proc InitSqlParameter

			/// <summary></summary>
			/// <param name="command"></param>
			/// <param name="columnInfo"></param>
			/// <param name="parameterName"></param>
			/// <param name="parameterValue"></param>
			/// <returns></returns>
			protected virtual DbParameter CreateParameter(DBCOMMAND command, IDataColumn columnInfo = null, string parameterName = null, object parameterValue = null)
			{
				var param = command.CreateParameter();

				// set parameter name
				if (parameterName != null)
					parameterName = UnformatParameterName(parameterName);

				// set value
				if (columnInfo is PpsSqlParameterInfo sqlParameterInfo)
				{
					sqlParameterInfo.InitSqlParameter(param);
					if (parameterValue != null)
						param.SetValue(parameterValue, param.GetDataType(), sqlParameterInfo.DefaultValue);
				}
				else if (columnInfo is PpsSqlColumnInfo sqlColumnInfo)
					sqlColumnInfo.InitSqlParameter(param, parameterName, parameterValue);
				else if (columnInfo is IPpsColumnDescription c && c.TryGetColumnDescriptionImplementation<PpsSqlColumnInfo>(out var sqlColumnInfo2)) // sql column -> easy to add
					sqlColumnInfo2.InitSqlParameter(param, parameterName, parameterValue);
				else
				{
					if (!String.IsNullOrEmpty(parameterName))
						param.ParameterName = parameterName;

					param.Direction = ParameterDirection.Input;
					if (columnInfo != null)
						param.SourceColumn = columnInfo.Name;
					param.SourceVersion = DataRowVersion.Current;

					if (columnInfo != null || parameterValue != null)
					{
						var t = columnInfo == null
							? parameterValue.GetType()
							: columnInfo.DataType;

						SetSqlParameterType(param, t, columnInfo);

						param.SetValue(parameterValue, t, DBNull.Value);
					}
					else
						param.SetValue(parameterValue, parameterValue?.GetType(), DBNull.Value);
				}

				command.Parameters.Add(param);
				return param;
			} // CreateParameter

			/// <summary></summary>
			/// <param name="commandText"></param>
			/// <param name="command"></param>
			/// <param name="columnInfo"></param>
			/// <param name="isFirst"></param>
			/// <param name="parameterName"></param>
			/// <param name="isNullable"></param>
			protected void CreateWhereParameter(StringBuilder commandText, PpsSqlDataCommand command, PpsSqlColumnInfo columnInfo, bool isFirst, string parameterName, bool? isNullable = null)
			{
				if (!isFirst)
					commandText.Append(" AND ");

				if (!isNullable.HasValue)
					isNullable = !columnInfo.IsPrimaryKey;

				var formattedParameterName = FormatParameterName(parameterName);
				if (isNullable.Value)
				{
					commandText.Append("(");
					commandText.Append(formattedParameterName).Append(" IS NULL OR ");
					columnInfo.AppendAsColumn(commandText).Append(" = ").Append(formattedParameterName);
					commandText.Append(")");
				}
				else
				{
					columnInfo.AppendAsColumn(commandText)
					  .Append(" = ")
					  .Append(formattedParameterName);
				}

				if (!command.ExistsParameter(parameterName))
				{
					var param = CreateParameter(command.Command, columnInfo, parameterName);
					command.AppendParameter(ParameterMapping.CreateTableName(parameterName, param, columnInfo.DataType, DBNull.Value));
				}
			} // func CreateWhereParameter

			/// <summary>Add trailing sql notations</summary>
			/// <param name="parameterName"></param>
			/// <returns></returns>
			protected virtual string FormatParameterName(string parameterName)
				=> String.IsNullOrEmpty(parameterName) ? "?" : "@" + UnformatParameterName(parameterName);

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

			#endregion

			#region -- Prepare Command ------------------------------------------------

			#region -- PrepareCall ----------------------------------------------------

			private PpsSqlDataCommand PrepareCall(LuaTable parameter, string procedureName)
			{
				var cmd = CreateCommand(CommandType.StoredProcedure, parameter);
				try
				{
					var procedureInfo = FindProcedure(procedureName);
					cmd.Command.CommandText = procedureInfo.SqlQualifiedName;

					// check for a columnList
					var columnList = parameter.GetMemberValue("columnList");
					if (columnList is IDataColumns columnMap)
					{
						foreach (var p in procedureInfo.Parameters)
						{
							var sqlParameter = cmd.Command.CreateParameter();
							p.InitSqlParameter(sqlParameter);

							var columnIndex = columnMap.FindColumnIndex(UnformatParameterName(p.Name), false);
							if (columnIndex == -1
								|| (p.Direction & ParameterDirection.ReturnValue) == ParameterDirection.ReturnValue)
								cmd.AppendParameter(ParameterMapping.CreateNull(sqlParameter, sqlParameter.GetDataType(), p.DefaultValue));
							else
								cmd.AppendParameter(ParameterMapping.CreateRowIndex(columnIndex, sqlParameter, sqlParameter.GetDataType(), p.DefaultValue));
						}
					}
					else
					{
						foreach (var p in procedureInfo.Parameters)
						{
							var sqlParameter = cmd.Command.CreateParameter();
							p.InitSqlParameter(sqlParameter);

							cmd.AppendParameter(
								(p.Direction & ParameterDirection.ReturnValue) == ParameterDirection.ReturnValue
									? ParameterMapping.CreateTableIndex(1, sqlParameter, sqlParameter.GetDataType(), DBNull.Value)
									: ParameterMapping.CreateTableName(UnformatParameterName(p.Name), sqlParameter, sqlParameter.GetDataType(), p.DefaultValue)
							);
						}
					}

					return cmd;
				}
				catch
				{
					cmd?.Dispose();
					throw;
				}
			} // proc PrepareCall

			#endregion

			#region -- PrepareSimpleSelect --------------------------------------------

			private PpsSqlDataCommand PrepareSimpleSelect(LuaTable parameter, string name)
			{
				/*
				 * select @cols from @name where @args
				 */

				// collect tables
				var tableInfos = new PpsSqlJoinExpression((PpsSqlDataSource)DataSource, name);

				var cmd = CreateCommand(CommandType.Text, parameter);
				try
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

								commandText.Append(PpsSqlJoinExpression.GetColumnExpression(table, column));
							}
						}
						#endregion
					}
					else if (columnList is LuaTable t) // columns are definied in a table
					{
						#region -- append select columns --
						void AppendColumnFromTableKey(string columnName)
						{
							var (table, column) = tableInfos.FindNativeColumn(columnName, !cmd.HasDefaults);
							if (column != null) // append table column
								commandText.Append(PpsSqlJoinExpression.GetColumnExpression(table, column));
							else // try append empty DbNull column
							{
								var field = DataSource.Application.GetFieldDescription(columnName, true);
								commandText.Append(FormatParameterName(CreateParameter(cmd.Command, field).ParameterName));
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
								? tableInfos.FindNativeColumn(ppsColumn, !cmd.HasDefaults)
								: tableInfos.FindNativeColumn(col.Name, !cmd.HasDefaults);

							if (column != null) // append table column
								commandText.Append(PpsSqlJoinExpression.GetColumnExpression(table, column));
							else // try append empty DbNull column
								commandText.Append(FormatParameterName(CreateParameter(cmd.Command, col).ParameterName));

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
					var args = GetArguments(parameter, 1, false); // todo: create arguments?
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

							var (table, column) = tableInfos.FindNativeColumn((string)p.Key, true);
							var parm = CreateParameter(cmd.Command, column, null, p.Value);
							commandText.Append(PpsSqlJoinExpression.GetColumnExpression(table, column));
							commandText.Append(" = ")
								.Append(FormatParameterName(parm.ParameterName));
						}
					}
					else if (parameter.GetMemberValue("where") is string sqlWhere)
						commandText.Append(" WHERE ").Append(sqlWhere);

					if (parameter.GetMemberValue("orderby") is string orderByString)
					{
						first = true;
						foreach (var orderBy in PpsDataOrderExpression.Parse(orderByString))
						{
							if (first)
							{
								commandText.Append(" ORDER BY ");
								first = false;
							}
							else
								commandText.Append(',');

							commandText.Append(orderBy.Identifier)
								.Append(' ');
							if (orderBy.Negate)
								commandText.Append("DESC ");
						}
					}

					return cmd.Prepare(commandText.ToString());
				}
				catch
				{
					cmd?.Dispose();
					throw;
				}
			} // func PrepareSimpleSelect

			#endregion

			#region -- PrepareSql -----------------------------------------------------

			private static Regex regExSqlParameter = new Regex(@"\@(\w+)", RegexOptions.Compiled);

			/// <summary>Get a table info from an object.</summary>
			/// <param name="tableInfo"></param>
			/// <returns></returns>
			protected PpsSqlTableInfo GetColumnTableInfo(object tableInfo)
			{
				switch (tableInfo)
				{
					case PpsSqlTableInfo ti:
						return ti;
					case string tn:
						return FindTable(tn, true);
					default:
						throw new ArgumentNullException(nameof(tableInfo));
				}
			} // func GetTableInto

			/// <summary></summary>
			/// <param name="columnList"></param>
			/// <returns></returns>
			protected Func<string, IDataColumn> GetColumnMapper(object columnList)
			{
				if (columnList == null)
					return null;
				else if (columnList is IDataColumns columns)
				{
					return new Func<string, IDataColumn>(k => columns.Columns[columns.FindColumnIndex(k, true)]);
				}
				else if(columnList is string tableName)
				{
					var tableInfo = FindTable(tableName, true);
					return new Func<string, IDataColumn>(k => tableInfo.FindColumn(k, true));
				}
				else if (columnList is LuaTable table)
				{
					var tableInfo = GetColumnTableInfo(table[1]);

					return new Func<string, IDataColumn>(
						k =>
						{
							var v = table[k];
							if (v == null)
								return tableInfo.FindColumn(k, true);
							else if (v is string cn)
								return tableInfo.FindColumn(cn, true);
							else if (v is IDataColumn column)
								return column;
							else
								throw new ArgumentOutOfRangeException(nameof(k), $"Can not resolve {k} in {tableInfo.TableName}.");
						}
					);
				}
				else
					throw new ArgumentOutOfRangeException(nameof(columnList), $"Can not create a columnd mapper from type {columnList.GetType().Name}.");
			} // func GetColumnMapper

			private PpsSqlDataCommand PrepareSql(LuaTable parameter, string name)
			{
				/*
				 * sql is execute and the args are created as a parameter
				 */
				var cmd = CreateCommand(CommandType.Text, parameter);
				try
				{
					cmd.Command.CommandText = name;

					var columnMapper = GetColumnMapper(parameter.GetMemberValue("columnList"));
					// create mapping for table
					foreach (Match m in regExSqlParameter.Matches(name))
					{
						var k = m.Groups[1].Value;
						if (!cmd.ExistsParameter(k))
						{
							var p = CreateParameter(cmd.Command, columnMapper?.Invoke(k), k, GetSampleValueFromArguments(parameter, k));
							cmd.AppendParameter(ParameterMapping.CreateTableName(k, p, p.GetDataType(), DBNull.Value));
						}
					}
					
					cmd.Command.Prepare();
					return cmd;
				}
				catch
				{
					cmd?.Dispose();
					throw;
				}
			} // func PrepareSql

			#endregion

			/// <summary></summary>
			/// <param name="parameter"></param>
			/// <param name="firstArgs"></param>
			/// <returns></returns>
			protected override PpsDataCommand PrepareCore(LuaTable parameter, LuaTable firstArgs) 
			{
				string name;
				if ((name = (string)(parameter["execute"] ?? parameter["exec"])) != null)
					return PrepareCall(parameter, name);
				else if ((name = (string)parameter["select"]) != null)
					return PrepareSimpleSelect(parameter, name);
				else if ((name = (string)parameter["sql"]) != null)
					return PrepareSql(parameter, name);
				else
					return base.PrepareCore(parameter, firstArgs);
			} // func Prepare

			#endregion
			
			#region -- Arguments ------------------------------------------------------

			/// <summary></summary>
			/// <param name="value"></param>
			/// <param name="throwException"></param>
			/// <returns></returns>
			protected static LuaTable GetArguments(object value, bool throwException)
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
			protected static LuaTable GetArguments(LuaTable parameter, int index, bool throwException)
			{
				var args = GetArguments(parameter[index], false);
				if (args == null && throwException)
					throw new ArgumentNullException($"parameter[{index}]", "No arguments defined.");
				return args;
			} // func GetArguments

			/// <summary></summary>
			/// <param name="parameter"></param>
			/// <param name="name"></param>
			/// <returns></returns>
			protected static object GetSampleValueFromArguments(LuaTable parameter, string name)
			{
				var i = 1;
				var args = GetArguments(parameter, i, false);
				while (args != null)
				{
					var v = args.GetMemberValue(name, true);
					if (v != null)
						return v;

					args = GetArguments(parameter, ++i, false);
				}
				return null;
			} // func GetSampleValueFromArguments

			#endregion

			/// <summary></summary>
			/// <param name="viewOrTableName"></param>
			/// <param name="alias"></param>
			/// <returns></returns>
			public PpsDataSelector CreateSelector(string viewOrTableName, string alias)
				=> ((PpsSqlDataSource)DataSource).CreateSelector(Connection, viewOrTableName, alias);

			/// <summary>Connection for the data manipulation</summary>
			public DBCONNECTION DbConnection => connection;
			/// <summary>Access the transaction</summary>
			public DBTRANSACTION DbTransaction => GetTransaction();
		} // class PpsSqlDataTransaction

		#endregion

		private readonly ManualResetEventSlim schemaInfoInitialized = new ManualResetEventSlim(false);
		private bool isSchemaInfoFailed = false;
		private string lastReadedConnectionString = String.Empty;

		private readonly Dictionary<string, PpsSqlTableInfo> tables = new Dictionary<string, PpsSqlTableInfo>(StringComparer.OrdinalIgnoreCase);
		private readonly Dictionary<string, PpsSqlColumnInfo> columns = new Dictionary<string, PpsSqlColumnInfo>(StringComparer.OrdinalIgnoreCase);
		private readonly Dictionary<string, PpsSqlProcedureInfo> procedures = new Dictionary<string, PpsSqlProcedureInfo>(StringComparer.OrdinalIgnoreCase);

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
				schemaInfoInitialized?.Dispose();
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

		/// <summary>Add this procedure to schema.</summary>
		/// <param name="procedure"></param>
		protected void AddProcedure(PpsSqlProcedureInfo procedure)
			=> procedures.Add(procedure.QualifiedName, procedure);

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
			=> tables.TryGetValue(name, out var tableInfo)
				? (T)tableInfo
				: (throwException ? throw new ArgumentNullException("name", $"Table '{name}' is not defined.") : (T)null);

		/// <summary></summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="name"></param>
		/// <param name="throwException"></param>
		/// <returns></returns>
		protected T ResolveProcedureByName<T>(string name, bool throwException = false)
			where T : PpsSqlProcedureInfo
			=> procedures.TryGetValue(name, out var procedureInfo)
				? (T)procedureInfo
				: (throwException ? throw new ArgumentNullException("name", $"Procedure '{name}' is not defined.") : (T)null);

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

		/// <summary></summary>
		/// <param name="connection"></param>
		/// <param name="selectorName"></param>
		/// <returns></returns>
		public sealed override PpsDataSelector CreateSelector(IPpsConnectionHandle connection, string selectorName)
			=> CreateSelector(connection, selectorName, null);

		/// <summary></summary>
		/// <param name="connection"></param>
		/// <param name="viewOrTableName"></param>
		/// <param name="alias"></param>
		/// <returns></returns>
		public PpsDataSelector CreateSelector(IPpsConnectionHandle connection, string viewOrTableName, string alias)
		{
			// first look up view
			var view = Application.GetViewDefinition(viewOrTableName, false);
			if(view != null && view.SelectorToken.DataSource == this)
				return view.SelectorToken.CreateSelector(connection, alias, true);

			// try to create from table
			return new PpsSqlDataSelector((IPpsSqlConnectionHandle)connection, ResolveTableByName<PpsSqlTableInfo>(viewOrTableName, true), alias);
		} // func CreateSelector

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
					var columnDescriptions = new List<IPpsColumnDescription>(r.FieldCount);

					var dt = r.GetSchemaTable();
					var i = 0;
					foreach (DataRow c in dt.Rows)
					{
						IPpsColumnDescription parentColumnDescription;
						var nativeColumnName = r.GetName(i);

						var isHidden = c["IsHidden"];
						if (isHidden is bool t && t)
							continue;

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

						columnDescriptions.Add(new SqlDataResultColumnDescription(parentColumnDescription, c, nativeColumnName, r.GetFieldType(i)));
						i++;
					}

					return columnDescriptions.ToArray();
				}
			} // using cmd
		} // pro ExecuteForResultSet

		/// <summary></summary>
		/// <param name="connection"></param>
		/// <param name="selectList"></param>
		/// <param name="from"></param>
		/// <param name="whereCondition"></param>
		/// <param name="whereConditionLookup"></param>
		/// <param name="orderBy"></param>
		/// <param name="orderByLookup"></param>
		/// <param name="start"></param>
		/// <param name="count"></param>
		/// <returns></returns>
		protected abstract DbCommand CreateViewCommand(IPpsSqlConnectionHandle connection, IEnumerable<IDataColumn> selectList, PpsSqlJoinExpression from, PpsDataFilterExpression whereCondition, Func<string, string> whereConditionLookup, IEnumerable<PpsDataOrderExpression> orderBy, Func<string, string> orderByLookup, int start, int count);

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
			using (var sr = new StreamReader(src, Encoding.UTF8, true))
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

		#region -- Sql Helper ---------------------------------------------------------

		#region -- class SqlColumnFinder ----------------------------------------------

		/// <summary>Helper to locate columns in a sql-selector expression.</summary>
		protected sealed class SqlColumnFinder
		{
			private readonly IPpsSqlAliasColumn[] selectedColumns;
			private readonly PpsSqlJoinExpression from;

			/// <summary></summary>
			/// <param name="selectColumns"></param>
			/// <param name="from"></param>
			public SqlColumnFinder(IPpsSqlAliasColumn[] selectColumns, PpsSqlJoinExpression from)
			{
				this.from = from ?? throw new ArgumentNullException(nameof(from));
				this.selectedColumns = selectColumns;
			} // ctor

			/// <summary></summary>
			/// <param name="expression"></param>
			/// <returns></returns>
			public IPpsSqlAliasColumn Find(string expression)
			{
				return selectedColumns?.FirstOrDefault(c => String.Compare(c.Alias, expression, StringComparison.OrdinalIgnoreCase) == 0)
					?? from.GetAliasColumn(expression, false);
			} // func Find

			/// <summary></summary>
			public IPpsSqlAliasColumn[] SelectedColumns => selectedColumns;
		} // class SqlColumnFinder

		#endregion

		#region -- class SqlDataFilterVisitor -----------------------------------------

		private sealed class SqlDataFilterVisitor : PpsDataFilterVisitorSql
		{
			private readonly Func<string, string> lookupNative;
			private readonly SqlColumnFinder columnLookup;

			public SqlDataFilterVisitor(Func<string, string> lookupNative, SqlColumnFinder columnLookup)
			{
				this.lookupNative = lookupNative;
				this.columnLookup = columnLookup;
			} // ctor

			protected override Tuple<string, Type> LookupColumn(string columnToken)
			{
				var column = columnLookup.Find(columnToken);
				if (column == null)
					throw new ArgumentNullException("operand", $"Could not resolve column '{columnToken}'.");

				return new Tuple<string, Type>(column.Expression, column.DataType);
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

		/// <summary></summary>
		/// <param name="orderBy"></param>
		/// <param name="lookupNative"></param>
		/// <param name="columnLookup"></param>
		/// <returns></returns>
		protected static string FormatOrderExpression(PpsDataOrderExpression orderBy, Func<string, string> lookupNative, SqlColumnFinder columnLookup)
		{
			// check for native expression
			if (lookupNative != null)
			{
				var expr = lookupNative(orderBy.Identifier);
				if (expr != null)
				{
					if (orderBy.Negate)
					{
						// todo: replace asc with desc and desc with asc
						expr = expr.Replace(" asc", " desc");
					}
					return expr;
				}
			}

			// checkt the column
			var column = columnLookup.Find(orderBy.Identifier)
				?? throw new ArgumentNullException("orderby", $"Order by column '{orderBy.Identifier} not found.");

			if (orderBy.Negate)
				return column.Expression + " DESC";
			else
				return column.Expression;
		} // func FormatOrderExpression

		/// <summary>Generate a default order by expression.</summary>
		/// <param name="sb"></param>
		/// <param name="orderBy"></param>
		/// <param name="orderByNativeLookup"></param>
		/// <param name="columnLookup"></param>
		/// <returns>Returns <c>true</c>, if no elements appended.</returns>
		protected static bool FormatOrderList(StringBuilder sb, IEnumerable<PpsDataOrderExpression> orderBy, Func<string, string> orderByNativeLookup, SqlColumnFinder columnLookup)
		{
			var first = true;

			if (orderBy != null)
			{
				foreach (var o in orderBy)
				{
					if (first)
					{
						sb.Append("ORDER BY ");
						first = false;
					}
					else
						sb.Append(", ");

					sb.Append(FormatOrderExpression(o, orderByNativeLookup, columnLookup));
				}
			}

			return first;
		} // func FormatOrderList

		/// <summary></summary>
		/// <param name="whereCondition"></param>
		/// <param name="lookupNative"></param>
		/// <param name="columnLookup"></param>
		/// <returns></returns>
		protected static string FormatWhereExpression(PpsDataFilterExpression whereCondition, Func<string, string> lookupNative, SqlColumnFinder columnLookup)
			=> new SqlDataFilterVisitor(lookupNative, columnLookup).CreateFilter(whereCondition);

		/// <summary></summary>
		/// <param name="sb"></param>
		/// <param name="columns"></param>
		protected static void FormatSelectList(StringBuilder sb, SqlColumnFinder columns)
		{
			var first = true;
			if (columns.SelectedColumns != null) // emit column expressions
			{
				foreach (var cur in columns.SelectedColumns)
				{
					if (first)
						first = false;
					else
						sb.Append(", ");

					sb.Append(cur.Expression);
					if (!String.IsNullOrEmpty(cur.Alias) && cur.Alias != cur.Expression)
					{
						sb.Append(" AS ")
							.Append('[').Append(cur.Alias).Append(']');
					}
				}
			}
			if (first) // emit select all
				sb.Append("* ");
			else
				sb.Append(' ');
		} // func FormatSelectList

		/// <summary></summary>
		/// <param name="columnName"></param>
		protected static string FormatColumn(string columnName)
			=> "[" + columnName + "]";

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
