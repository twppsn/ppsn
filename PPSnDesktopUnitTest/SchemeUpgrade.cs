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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TecWare.PPSn.Data;
using System.Xml.Linq;
using System.Data.SQLite;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace TecWare.PPSn
{
	[TestClass]
	public class SchemeUpgrade
	{ 
		/// <summary>
		/// Nothing should be done
		/// </summary>
		[TestMethod]
		public void PpsMasterDataImportTest_UnchangedTable()
		{
			var testdb = new TestDatabase();

			// table1
			var testtable1 = new TestTable("Table1", null, "'1'");
			var testcolumn1 = new TestColumn("Column1", typeof(int), true, false, true, String.Empty);
			testtable1.Columns.Add(testcolumn1);

			testdb.Tables.Add(testtable1);
			// table1

			// table SyncState
			var syncstate = new TestTable("SyncState", null, $"'{testtable1.Name}', '1'");
			var synccol1 = new TestColumn("Table", typeof(string), true, false, false, String.Empty);
			var synccol2 = new TestColumn("Syncid", typeof(int), false, false, false, String.Empty);
			syncstate.Columns.Add(synccol1);
			syncstate.Columns.Add(synccol2);

			testdb.Tables.Add(syncstate);
			// table syncstate

			var testdataset = CreateTestDataSet(testdb);

			using (var testdatabase = CreateTestDatabase(testdb))
			{
				var commands = GetUpdateCommands(testdatabase, testdataset, CheckLocalTableExists(testdatabase, "SyncState"));

				using (var transaction = testdatabase.BeginTransaction())
				{
					ExecuteUpdateScript(testdatabase, transaction, commands);

					transaction.Commit();

					var newschema = GetDefinitionOfDataBase(testdatabase);

					Assert.AreEqual(0, testdb.CompareTo(newschema));
				}
			}
		}
		
		[TestMethod]
		public void PpsMasterDataImportTest_AddColumn()
		{
			foreach (var nullable in new bool[] { false, true })
				foreach (var defaultstring in new string[] { "Teststring", String.Empty })
					foreach (var upgrade in new bool[] { true, false })
					{
						var testtablename = "Table1";

						var remotedb = new TestDatabase();
						
						// table1
						remotedb.Tables.Add(new TestTable(testtablename, new List<TestColumn> {
								new TestColumn("Column1", typeof(int), true, false, true, String.Empty),
								new TestColumn("Column2", typeof(string), false, nullable, false, defaultstring)
							}, "'1', '1'"));
						if (upgrade)
							remotedb.Tables[remotedb.Tables.FindIndex((a) => a.Name == testtablename)].Columns.Add(
							new TestColumn("_IsUpdated", typeof(string), false, true, false, String.Empty));

						// table SyncState
						remotedb.Tables.Add(new TestTable("SyncState", new List<TestColumn> {
								new TestColumn("Table", typeof(string), true, false, false, String.Empty),
								new TestColumn("Syncid", typeof(int), false, true, false, String.Empty)
							}, $"'{testtablename}', '1'"));



						var localdb = remotedb.Clone();
						var localtable = localdb.Tables[localdb.Tables.FindIndex((a) => a.Name == testtablename)];
						localtable.Columns.RemoveAt(localtable.Columns.FindIndex((a) => a.Name == "Column2"));
						localtable.FillString = upgrade ? "'1','1'" : "'1'";
						// table syncstate
						
						var testdataset = CreateTestDataSet(remotedb);

						using (var testdatabase = CreateTestDatabase(localdb))
						{
							var commands = GetUpdateCommands(testdatabase, testdataset, CheckLocalTableExists(testdatabase, "SyncState"));

							using (var transaction = testdatabase.BeginTransaction())
							{
								if (!nullable && String.IsNullOrEmpty(defaultstring))
								{
									try
									{
										ExecuteUpdateScript(testdatabase, transaction, commands);
										Assert.Fail();
									}
									catch (Exception)
									{
										transaction.Rollback();
									}
								}
								else
								{
									ExecuteUpdateScript(testdatabase, transaction, commands);
									transaction.Commit();
								}
							}

							// check the schema
							if (!nullable && String.IsNullOrEmpty(defaultstring))
								Assert.AreEqual(0, GetDefinitionOfDataBase(testdatabase).CompareTo(localdb));
							else
								Assert.AreEqual(0, GetDefinitionOfDataBase(testdatabase).CompareTo(remotedb));
							// check the schema

							// check the data
							var cmd = testdatabase.CreateCommand();

							cmd.CommandText = "SELECT * FROM 'SyncState';";
							if (!nullable && String.IsNullOrEmpty(defaultstring))
								Assert.AreEqual("Table1", cmd.ExecuteScalar()); // Table1 must be in SyncState
							else
								Assert.AreEqual(null, cmd.ExecuteScalar()); // Table1 must not be in SyncState anymore

							cmd.CommandText = "SELECT Column1 FROM 'Table1';";
							if (upgrade) Assert.AreEqual(1, cmd.ExecuteScalar());    // 1 was inserted in Table 1 - must remain

							cmd.CommandText = "SELECT Column2 FROM 'Table1';";
							if (!nullable && !String.IsNullOrEmpty(defaultstring) && upgrade)
								Assert.AreEqual("Teststring", cmd.ExecuteScalar());    // Column upgraded
							// check the data
						}
					}
		}

		#region -- Accessors ------------------------------------------------------------

		private void ExecuteUpdateScript(SQLiteConnection connection, SQLiteTransaction transaction, IEnumerable<string> commands)
		{
			PrivateType accessor = new PrivateType(typeof(PpsMasterData));
			accessor.InvokeStatic("ExecuteUpdateScript", connection, transaction, commands);
		}

		private string ConvertDataTypeToSqLite(Type type)
		{
			PrivateType accessor = new PrivateType(typeof(PpsMasterData));
			return (string)accessor.InvokeStatic("ConvertDataTypeToSqLite", type);
		}

		private Type ConvertSqLiteToDataType(string type)
		{
			PrivateType accessor = new PrivateType(typeof(PpsMasterData));
			return (Type)accessor.InvokeStatic("ConvertSqLiteToDataType", type);
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

		private PpsDataSetDefinitionDesktop CreateTestDataSet(TestDatabase testdb)
		{
			var tables = new List<XElement>();

			foreach (var table in testdb.Tables)
			{
				List<object> content = new List<object>();
				content.Add(new XAttribute("name", table.Name));

				foreach (var column in table.Columns)
				{
					var xmlcolumn = XElement.Parse($"<column name=\"{column.Name}\" dataType=\"{column.DataType}\" isPrimary=\"{column.IsPrimary}\" isIdentity=\"{column.IsIndex}\">" +
														$"<meta>" +
														 $"<displayName dataType=\"string\">dbo.test.{column.Name}</displayName>" +
														 $"<nullable dataType=\"bool\">{column.Nullable}</nullable>" +
														 $"<IsIdentity dataType=\"bool\">{column.IsIndex}</IsIdentity>" +
														 $"<default dataType=\"string\">{column.DefaultValue}</default>" +
														"</meta>" +
													  "</column>");
					content.Add(xmlcolumn);
				}

				var xmltable = new XElement("table", content);
				tables.Add(xmltable);
			}

			var schema = new XElement("schema", tables);

			return new PpsDataSetDefinitionDesktop(null, "masterDataSet", schema);
		}

		private SQLiteConnection CreateTestDatabase(TestDatabase testdb)
		{
			var sqliteDataBase = new SQLiteConnection("Data Source=:memory:;DateTimeKind=Utc;foreign keys=true;new=true;");
			{
				sqliteDataBase.Open();

				using (var sqlite = sqliteDataBase.CreateCommand())
				{
					foreach (var table in testdb.Tables)
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
							createcmd.Append($" {column.DefaultString}");
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

						if (!String.IsNullOrWhiteSpace(table.FillString))
						{
							sqlite.CommandText = $"INSERT INTO '{table.Name}' VALUES ({table.FillString});";
							sqlite.ExecuteNonQueryEx();
						}
					}
				}
			}
			return sqliteDataBase;
		}

		private TestDatabase GetDefinitionOfDataBase(SQLiteConnection sqlite)
		{
			var ret = new TestDatabase();

			var cmd = sqlite.CreateCommand();

			cmd.CommandText = "SELECT [tbl_name] FROM [sqlite_master] WHERE [type] = 'table'";

			var sqltables = cmd.ExecuteReaderEx();
			var tables = new List<string>();
			while (sqltables.Read())
				tables.Add(sqltables.GetString(0));
			sqltables.Close();

			cmd.CommandText = "SELECT [name] FROM [sqlite_master] WHERE [type] = 'index'";
			var sqlindexes = cmd.ExecuteReaderEx();
			var indexes = new List<string>();
			while (sqlindexes.Read())
				indexes.Add(sqlindexes.GetString(0));
			sqlindexes.Close();

			foreach (var name in tables)
			{
				var newtable = new TestTable(name);

				cmd.CommandText = $"PRAGMA table_info('{name}')";
				var columns = cmd.ExecuteReaderEx();

				while (columns.Read())
				{
					var n = columns.GetString(1);
					var d = columns.GetString(2);
					var p = columns.GetBoolean(5);
					var na = !columns.GetBoolean(3);
					var ind = (from inde in indexes where inde.StartsWith($"{name}_{n}_index") select inde).Count() == 1;
					var de = columns.IsDBNull(4) ? String.Empty : columns.GetString(4).Trim('\'');
					var col = new TestColumn(n, ConvertSqLiteToDataType(d), p, na, ind, de);

					newtable.Columns.Add(col);
				}

				columns.Close();

				ret.Tables.Add(newtable);
			}

			return ret;
		}

		#endregion

		#region -- TestClasses ----------------------------------------------------------

		private class TestDatabase : IComparable
		{
			private List<TestTable> tables;

			public TestDatabase()
			{
				this.tables = new List<TestTable>();
			}

			public List<TestTable> Tables { get { return tables; } set { tables = value; } }

			public int CompareTo(object obj)
			{
				if (!(obj is TestDatabase))
					return 1;
				var tdb = (TestDatabase)obj;

				if (tables.Count != tdb.Tables.Count)
					return 1;

				tables.Sort((a, b) => a.Name.CompareTo(b.Name));
				tdb.Tables.Sort((a, b) => a.Name.CompareTo(b.Name));

				var ret = 0;

				for (var i = 0; i < tables.Count; i++)
					ret += tables[i].CompareTo(tdb.Tables[i]);

				return ret;
			}

			public TestDatabase Clone()
			{
				var ret = new TestDatabase();
				foreach(var tab in tables)
				{
					var newtab = new TestTable(tab.Name,null,tab.FillString);
					foreach(var col in tab.Columns)
					{
						var newcol = new TestColumn(col.Name, col.DataType, col.IsPrimary, col.Nullable, col.IsIndex, col.DefaultValue);
						newtab.Columns.Add(newcol);
					}
					ret.Tables.Add(newtab);
				}

				return ret;
			}
		}

		private class TestTable : IComparable
		{
			private string name;
			private string fillstring;
			private List<TestColumn> columns;

			public TestTable(string Name, List<TestColumn> Columns = null, string FillString = null)
			{
				this.name = Name;
				this.columns = (Columns == null) ? new List<TestColumn>() : Columns;
				this.fillstring = FillString;
			}

			public string Name { get { return name; } set { name = value; } }
			public string FillString { get { return fillstring; } set { fillstring = value; } }
			public List<TestColumn> Columns { get { return columns; } set { columns = value; } }

			public int CompareTo(object obj)
			{
				if (!(obj is TestTable))
					return 1;

				var tt = (TestTable)obj;

				if (name != tt.Name)
					return 1;

				if (columns.Count != tt.Columns.Count)
					return 1;

				columns.Sort((a, b) => a.Name.CompareTo(b.Name));
				tt.Columns.Sort((a, b) => a.Name.CompareTo(b.Name));

				var ret = 0;

				for (var i = 0; i < columns.Count; i++)
					ret += columns[i].CompareTo(tt.Columns[i]);

				return ret;
			}
		}

		private class TestColumn : IComparable
		{
			private string name;
			private Type datatype;
			private bool isprimary;
			private bool nullable;
			private bool isindex;
			private string defaultvalue;

			public TestColumn(string Name, Type DataType, bool IsPrimary, bool Nullable, bool IsIndex, string DefaultValue)
			{
				this.name = Name;
				this.datatype = DataType;
				this.isprimary = IsPrimary;
				this.nullable = Nullable;
				this.isindex = IsIndex;
				this.defaultvalue = DefaultValue;
			}

			public string Name { get { return name; } set { name = value; } }
			public Type DataType { get { return datatype; } set { datatype = value; } }
			public bool IsPrimary { get { return isprimary; } set { isprimary = value; } }
			public string PrimaryString => isprimary ? " PRIMARY KEY" : String.Empty;
			public bool Nullable { get { return nullable; } set { nullable = value; } }
			public string NullString => nullable ? " NULL" : " NOT NULL";
			public bool IsIndex { get { return isindex; } set { isindex = value; } }
			public string DefaultValue { get { return defaultvalue; } set { defaultvalue = value; } }
			public string DefaultString => String.IsNullOrWhiteSpace(defaultvalue) ? String.Empty : $" DEFAULT '{defaultvalue}'";

			public int CompareTo(object obj)
			{
				if (!(obj is TestColumn))
					return 1;

				var tc = (TestColumn)obj;

				if (name != tc.Name || datatype != tc.DataType || isprimary != tc.IsPrimary || nullable != tc.Nullable || isindex != tc.IsIndex || defaultvalue != tc.DefaultValue)
					return 1;

				return 0;
			}
		}

		#endregion
	}
}

