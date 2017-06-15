using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TecWare.PPSn.Server.Data;

namespace TecWare.PPSn.Server.Sql
{
	public sealed class PpsSqlColumnInfo : PpsColumnDescription
	{
		public PpsSqlColumnInfo(PpsSqlTableInfo table, int columnId, string columnName, DbType dataType, int maxLength, int precision, int scale, bool isNullable, bool isIdentity)
			: base(null, columnName, typeof(object))
		{
		} // ctor
	} // class PpsSqlTableInfo

	public sealed class PpsSqlRelationInfo
	{
		public PpsSqlRelationInfo(int objectId, string name, PpsSqlColumnInfo parentColumn, PpsSqlColumnInfo referencedColumn)
		{
		}
	} // class PpsSqlTableInfo

	public sealed class PpsSqlTableInfo
	{
		public PpsSqlTableInfo(int objectId, string schema, string tableName)
		{
		}

		public void AddColumn(PpsSqlColumnInfo column, bool isPrimaryKey = false)
		{
		}

		public void AddRelation(PpsSqlRelationInfo relationInfo)
		{
		}
	} // class PpsSqlTableInfo
}
