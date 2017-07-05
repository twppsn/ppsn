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
using System.Text;
using TecWare.DE.Stuff;
using TecWare.PPSn.Server.Data;

namespace TecWare.PPSn.Server.Sql
{
	#region -- class PpsSqlColumnInfo ---------------------------------------------------

	public abstract class PpsSqlColumnInfo : PpsColumnDescription
	{
		#region -- class PpsColumnAttributes --------------------------------------------

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

		public PpsSqlColumnInfo(PpsSqlTableInfo table, string columnName, Type dataType, int maxLength, byte precision, byte scale, bool isNullable, bool isIdentity)
			: base(null, columnName, dataType)
		{
			this.table = table;
			this.maxLength = maxLength;
			this.precision = precision;
			this.scale = scale;
			this.isNullable = isNullable;
			this.isIdentity = isIdentity;

		} // ctor

		/// <summary>The top most constructor should call this method</summary>
		protected virtual void EndInit()
			=> this.table.AddColumn(this);

		protected override IPropertyEnumerableDictionary CreateAttributes() 
			=> base.CreateAttributes();

		protected virtual IEnumerator<PropertyValue> GetProperties()
		{
			yield return new PropertyValue(nameof(MaxLength), MaxLength);
			yield return new PropertyValue(nameof(Precision), Precision);
			yield return new PropertyValue(nameof(Scale), Scale);
			yield return new PropertyValue(nameof(Nullable), Nullable);
			yield return new PropertyValue(nameof(IsIdentity), IsIdentity);
		} // func GetProperties

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


		public virtual StringBuilder AppendAsParameter(StringBuilder sb)
			=> sb.Append('@').Append(Name);

		public virtual StringBuilder AppendAsColumn(StringBuilder sb)
			=> sb.Append('[').Append(Name).Append(']');

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

		public virtual DbParameter AppendSqlParameter(DbCommand command, string parameterName = null, object value = null)
		{
			var parameter = command.CreateParameter();
			InitSqlParameter(parameter, parameterName, value);
			command.Parameters.Add(parameter);
			return parameter;
		} // AppendSqlParameter
		
		public PpsSqlTableInfo Table => table;

		public string TableColumnName => table.QuallifiedName + "." + Name;
		public int MaxLength => maxLength;
		public byte Precision => precision;
		public byte Scale => scale;
		public bool Nullable => isNullable;
		public bool IsIdentity => isIdentity;
		public bool IsPrimary => table.IsPrimaryKeyColumn(this);
	} // class PpsSqlTableInfo

	#endregion

	#region -- class PpsSqlRelationInfo -------------------------------------------------

	/// <summary></summary>
	[DebuggerDisplay("{DebuggerDisplay,nq}")]
	public abstract class PpsSqlRelationInfo
	{
		private readonly string name;
		private readonly PpsSqlColumnInfo parentColumn;
		private readonly PpsSqlColumnInfo referencedColumn;

		public PpsSqlRelationInfo(string name, PpsSqlColumnInfo parentColumn, PpsSqlColumnInfo referencedColumn)
		{
			this.name = name;
			this.parentColumn = parentColumn;
			this.referencedColumn = referencedColumn;
		} // ctor

		private string DebuggerDisplay
			=> $"RelationInfo: {name} [parent: {parentColumn?.Name ?? "null"}; child: {referencedColumn?.Name ?? "null"}]";

		public string Name => name;
		public PpsSqlColumnInfo ParentColumn => parentColumn;
		public PpsSqlColumnInfo ReferencedColumn => referencedColumn;
	} // class PpsSqlTableInfo

	#endregion

	#region -- class PpsSqlTableInfo ----------------------------------------------------

	[DebuggerDisplay("{DebuggerDisplay,nq}")]
	public abstract class PpsSqlTableInfo
	{
		private readonly string schemaName;
		private readonly string tableName;

		private readonly List<PpsSqlColumnInfo> columns = new List<PpsSqlColumnInfo>();
		private readonly List<PpsSqlRelationInfo> relations = new List<PpsSqlRelationInfo>();

		public PpsSqlTableInfo(string schemaName, string tableName)
		{
			this.schemaName = schemaName;
			this.tableName = tableName;
		} // ctor

		internal void AddColumn(PpsSqlColumnInfo column)
		{
			columns.Add(column);
			OnColumnAdded(column);
		} // proc AddColumn

		protected virtual void OnColumnAdded(PpsSqlColumnInfo column) { }

		internal void AddRelation(PpsSqlRelationInfo relationInfo)
		{
			relations.Add(relationInfo);
			OnRelationAdded(relationInfo);
		} // proc AddRelation

		protected virtual void OnRelationAdded(PpsSqlRelationInfo relationInfo) { }

		public PpsSqlColumnInfo FindColumn(string columnName, bool throwException = false)
		{
			var col = columns.Find(c => String.Compare(c.Name, columnName, StringComparison.OrdinalIgnoreCase) == 0);
			if (col == null && throwException)
				throw new ArgumentOutOfRangeException("columnName", $"Table '{TableName}' does not define  Column '{columnName}'.");
			return col;
		} // func FindColumn

		private string DebuggerDisplay
			=> $"TableInfo: {QuallifiedName} [pk: {PrimaryKey?.Name ?? "null"}]";

		public abstract bool IsPrimaryKeyColumn(PpsSqlColumnInfo column);

		public string TableName => tableName;
		public string QuallifiedName => schemaName + "." + tableName;

		public abstract PpsSqlColumnInfo PrimaryKey { get; }

		public IEnumerable<PpsSqlColumnInfo> Columns => columns;
		public IEnumerable<PpsSqlRelationInfo> RelationInfo => relations;
	} // class PpsSqlTableInfo

	#endregion
}
