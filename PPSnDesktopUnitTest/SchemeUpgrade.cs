﻿#region -- copyright --
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TecWare.PPSn.Data;
using System.Xml.Linq;
using System.Data.SQLite;
using System.Collections.Generic;
using System.Text;

namespace TecWare.PPSn
{
	[TestClass]
	public class SchemeUpgrade
	{
		/// <summary>
		/// Not really the Hash - but pretty close
		/// </summary>
		/// <returns></returns>
		private string GetDatabaseHash(SQLiteConnection sqliteDatabase)
		{
			// ToDo: rk: make me smart
			var hash = String.Empty;
			using (var sqlite = sqliteDatabase.CreateCommand())
			{
				// Tables
				sqlite.CommandText = "SELECT [name] FROM 'sqlite_master' WHERE [type] = 'table';";
				var reader = sqlite.ExecuteReaderEx();
				hash += "\n--Tables:\n";
				while (reader.Read())
				{
					for (var i = 0; i < reader.FieldCount; i++)
						hash += reader.GetValue(i).ToString() + '\t';
					hash += '\n';
				}
				reader.Close();

				// Scheme
				sqlite.CommandText = "PRAGMA table_info('Table1');";
				reader = sqlite.ExecuteReaderEx();
				hash += "--Scheme:\n";
				while (reader.Read())
				{
					for (var i = 0; i < reader.FieldCount; i++)
						hash += reader.GetValue(i).ToString() + '\t';
					hash += '\n';
				}
				reader.Close();

				// Indexes
				sqlite.CommandText = "SELECT [name] FROM 'sqlite_master' WHERE ([type] = 'index' AND [tbl_name] = 'Table1');";
				reader = sqlite.ExecuteReaderEx();
				hash += "--Indexes:\n";
				while (reader.Read())
				{
					for (var i = 0; i < reader.FieldCount; i++)
						hash += reader.GetValue(i).ToString() + '\t';
					hash += '\n';
				}
				reader.Close();

				// Data
				sqlite.CommandText = "SELECT * FROM 'Table1';";
				reader = sqlite.ExecuteReaderEx();
				hash += "--Data:\n";
				while (reader.Read())
				{
					for (var i = 0; i < reader.FieldCount; i++)
						hash += reader.GetValue(i).ToString() + '\t';
					hash += '\n';
				}
			}
			return hash;
		}

		[TestMethod]
		public void PpsMasterDataImportTest_UnchangedTable()
		{
			var testtablelist = new List<TestTable>();

			// table1
			var testtable1 = new TestTable("Table1");
			var testcolumn1 = new TestColumn("Column1", typeof(int), true, false, true, String.Empty);
			testtable1.Columns.Add(testcolumn1);

			testtablelist.Add(testtable1);
			// table1
			
			var testdatabase = CreateTestDatabase(testtablelist);
			var testdataset = CreateTestDataSet(testtablelist);
			
			using (testdatabase)
			{
				var commands = GetUpdateCommands(testdatabase, testdataset, CheckLocalTableExists(testdatabase, "SyncState"));

				Assert.AreEqual(0, commands.Count);
			}
		}
		
		#region -- Accessors ------------------------------------------------------------

		private string ConvertDataTypeToSqLite(Type type)
		{
			PrivateType accessor = new PrivateType(typeof(PpsMasterData));
			return (string)accessor.InvokeStatic("ConvertDataTypeToSqLite", type);
		}

		private IReadOnlyList<string> GetUpdateCommands(SQLiteConnection sqliteDataBase, PpsDataSetDefinitionDesktop schema, bool syncStateTableExists)
		{
			PrivateType accessor = new PrivateType(typeof(PpsMasterData));
			return (IReadOnlyList<string>)accessor.InvokeStatic("GetUpdateCommands", sqliteDataBase, schema, syncStateTableExists);
		}

		private bool CheckLocalTableExists(SQLiteConnection connection, string tableName)
		{
			PrivateType accessor = new PrivateType(typeof(PpsMasterData));
			return (bool)accessor.InvokeStatic("CheckLocalTableExists", connection, tableName);
		}

		#endregion

		#region -- Helper Functions -----------------------------------------------------

		private PpsDataSetDefinitionDesktop CreateTestDataSet(List<TestTable> Tables)
		{
			var tables = new List<XElement>();

			foreach (var table in Tables)
			{
				List<object> content = new List<object>();
				content.Add(new XAttribute("name", table.Name));

				foreach(var column in table.Columns)
				{
					var xmlcolumn = XElement.Parse($"<column name=\"{column.Name}\" dataType=\"{column.DataType}\" isPrimary=\"{column.IsPrimary}\" isIdentity=\"{column.IsIndex}\">" +
														$"<meta>" +
														 $"<displayName dataType=\"string\">dbo.test.{column.Name}</displayName>" +
														 $"<IsNull dataType=\"bool\">{column.IsNull}</IsNull>" +
														 $"<IsIdentity dataType=\"bool\">{column.IsIndex}</IsIdentity>" +
														"</meta>" +
													  "</column>");
					content.Add(xmlcolumn);
				}

				var xmltable = new XElement("table",content);
				tables.Add(xmltable);
			}

			var schema = new XElement("schema", tables);
			
			return new PpsDataSetDefinitionDesktop(null, "masterDataSet", schema);
		}

		private SQLiteConnection CreateTestDatabase(List<TestTable> Tables)
		{
			var sqliteDataBase = new SQLiteConnection("Data Source=:memory:;DateTimeKind=Utc;foreign keys=true;new=true;");
			{
				sqliteDataBase.Open();

				using (var sqlite = sqliteDataBase.CreateCommand())
				{
					foreach (var table in Tables)
					{
						var createcmd = new StringBuilder();
						var indexcommands = new List<string>();

						createcmd.Append("CREATE TABLE");
						createcmd.Append($" '{table.Name}'");
						createcmd.Append(" (");
						foreach (var column in table.Columns)
						{
							createcmd.Append($" [{column.Name}]");
							createcmd.Append($" {ConvertDataTypeToSqLite(column.DataType)}");
							createcmd.Append($" {column.PrimaryString}");
							createcmd.Append($" {column.NullString}");
							createcmd.Append(",");

							if (column.IsIndex)
								indexcommands.Add($"CREATE UNIQUE INDEX '{table.Name}_{column.Name}_index' ON '{table.Name}'([{column.Name}]);");
						}
						createcmd.Remove(createcmd.Length - 1, 1);  // remove the last colon
						createcmd.Append(");");

						sqlite.CommandText = createcmd.ToString();
						sqlite.ExecuteNonQueryEx();

						foreach (var indexcommand in indexcommands)
						{
							sqlite.CommandText = indexcommand;
							sqlite.ExecuteNonQueryEx();
						}
					}

					// initialize the table
					/*
					sqlite.CommandText = "CREATE TABLE 'Table1' ( [Column1] INTEGER PRIMARY KEY NOT NULL, [Column2] TEXT NULL);";
					sqlite.ExecuteNonQueryEx();
					sqlite.CommandText = "CREATE UNIQUE INDEX 'Table1_Column1_index' ON 'Table1'([Column1]);";
					sqlite.ExecuteNonQueryEx();
					sqlite.CommandText = "INSERT INTO 'Table1' VALUES (1,'Testtext');";
					sqlite.ExecuteNonQueryEx();
					sqlite.CommandText = "CREATE TABLE [SyncState] ([Table] TEXT PRIMARY KEY NOT NULL,[SyncId] INTEGER NOT NULL);";
					sqlite.ExecuteNonQueryEx();
					sqlite.CommandText = "INSERT INTO 'SyncState' VALUES('Table1', 1);";
					sqlite.ExecuteNonQueryEx();*/
				}
			}
			return sqliteDataBase;
		}

		#endregion

		#region -- TestClasses ----------------------------------------------------------

		private class TestTable
		{
			private string name;
			private List<TestColumn> columns;

			public TestTable(string Name, List<TestColumn> Columns = null)
			{
				this.name = Name;
				this.columns = (Columns == null) ? new List<TestColumn>() : Columns;
			}

			public string Name { get { return name; } set { name = value; } }
			public List<TestColumn> Columns { get { return columns; } set { columns = value; } }

		}

		private class TestColumn
		{
			private string name;
			private Type datatype;
			private bool isprimary;
			private bool isnull;
			private bool isindex;
			private string defaultvalue;

			public TestColumn(string Name, Type DataType, bool IsPrimary, bool IsNull, bool IsIndex, string DefaultValue)
			{
				this.name = Name;
				this.datatype = DataType;
				this.isprimary = IsPrimary;
				this.isnull = IsNull;
				this.isindex = IsIndex;
				this.defaultvalue = DefaultValue;
			}

			public string Name { get { return name; } set { name = value; } }
			public Type DataType { get { return datatype; } set { datatype = value; } }
			public bool IsPrimary { get { return isprimary; } set { isprimary = value; } }
			public string PrimaryString => isprimary ? " PRIMARY KEY" : String.Empty;
			public bool IsNull { get { return isnull; } set { isnull = value; } }
			public string NullString => isnull ? " NULL" : " NOT NULL";
			public bool IsIndex { get { return isindex; } set { isindex = value; } }
			public string DefaultValue { get { return defaultvalue; } set { defaultvalue = value; } }
		}

		#endregion
	}
}

