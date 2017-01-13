using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TecWare.PPSn.Data;
using System.Xml.Linq;
using System.Data;
using System.Collections.Generic;
using System.Data.SQLite;
using TecWare.DE.Data;
using System.Text;

namespace TecWare.PPSn.PpsEnvironment
{
	[TestClass]
	public class SchemeUpgrade
	{
		private PpsDataSetDefinitionDesktop GetMasterDataScheme()
		{
			var xScheme = XElement.Parse("<schema>" +
													"<table name=\"Table1\">" +
													 "<meta>" +
													  "<mustImport dataType=\"string\">true</mustImport>" +
													 "</meta>" +
													 "<column name=\"Column1\" dataType=\"long\" isPrimary=\"true\" isIdentity=\"true\">" +
														"<meta>" +
														 "<displayName dataType=\"string\">dbo.Knst.Id</displayName>" +
														 "<SqlType dataType=\"System.Data.SqlDbType\">BigInt</SqlType>" +
														 "<MaxLength dataType=\"int\">8</MaxLength>" +
														 "<Precision dataType=\"int\">19</Precision>" +
														 "<Scale dataType=\"int\">0</Scale>" +
														 "<IsNull dataType=\"bool\">false</IsNull>" +
														 "<IsIdentity dataType=\"bool\">true</IsIdentity>" +
														"</meta>" +
													  "</column>" +
													  "<column name =\"Column2\" dataType=\"string\">" +
														"<meta>" +
														 "<displayName dataType=\"string\">dbo.Knst.Id</displayName>" +
														 "<SqlType dataType=\"System.Data.SqlDbType\">NVarChar</SqlType>" +
														 "<MaxLength dataType=\"int\">8</MaxLength>" +
														 "<Precision dataType=\"int\">19</Precision>" +
														 "<Scale dataType=\"int\">0</Scale>" +
														 "<IsNull dataType=\"bool\">true</IsNull>" +
														 "<IsIdentity dataType=\"bool\">false</IsIdentity>" +
														"</meta>" +
													  "</column>" +
													 "</table>" +
													"</schema>");

			return new PpsDataSetDefinitionDesktop(null, "masterDataSet", xScheme);
		}

		[TestMethod]
		public void PpsMasterDataImportTest_mustImport_Same()
		{
			// create a testDB in RAM
			using (var sqliteDataBase = new SQLiteConnection("Data Source=:memory:;DateTimeKind=Utc;foreign keys=true;new=true;"))
			{
				sqliteDataBase.Open();

				#region -- create the test table --

				using (var sqlite = sqliteDataBase.CreateCommand())
				{
					// initialize the table
					sqlite.CommandText = "CREATE TABLE 'Table1' ( [Column1] INTEGER PRIMARY KEY NOT NULL, [Column2] TEXT NULL);";
					sqlite.ExecuteNonQuery();
					sqlite.CommandText = "INSERT INTO 'Table1' VALUES (1,'Testtext');";
					sqlite.ExecuteNonQuery();
				}

				#endregion

				var beforeState = new StringBuilder();
				#region -- get the current state of the database --

				using (var sqlite = sqliteDataBase.CreateCommand())
				{
					sqlite.CommandText = "PRAGMA table_info('Table1');";
					var reader = sqlite.ExecuteReader();
					beforeState.Append("\n--Scheme:\n");
					while (reader.Read())
					{
						for (var i = 0; i < reader.FieldCount; i++)
							beforeState.Append(reader.GetValue(i).ToString()+'\t');
						beforeState.Append('\n');
					}
					reader.Close();
					sqlite.CommandText = "SELECT * FROM 'Table1';";
					reader = sqlite.ExecuteReader();
					beforeState.Append("--Data:\n");
					while (reader.Read())
					{
						for (var i = 0; i < reader.FieldCount; i++)
							beforeState.Append(reader.GetValue(i).ToString() + '\t');
						beforeState.Append('\n');
					}
				}

				#endregion

				var master = new PpsMasterData(GetMasterDataScheme(), sqliteDataBase);
				master.RefreshMasterDataScheme();

				var afterState = new StringBuilder();
				#region -- get the changed state of the database --

				using (var sqlite = sqliteDataBase.CreateCommand())
				{
					sqlite.CommandText = "PRAGMA table_info('Table1');";
					var reader = sqlite.ExecuteReader();
					afterState.Append("\n--Scheme:\n");
					while (reader.Read())
					{
						for (var i = 0; i < reader.FieldCount; i++)
							afterState.Append(reader.GetValue(i).ToString() + '\t');
						afterState.Append('\n');
					}
					reader.Close();
					sqlite.CommandText = "SELECT * FROM 'Table1';";
					reader = sqlite.ExecuteReader();
					afterState.Append("--Data:\n");
					while (reader.Read())
					{
						for (var i = 0; i < reader.FieldCount; i++)
							afterState.Append(reader.GetValue(i).ToString() + '\t');
						afterState.Append('\n');
					}
				}

				#endregion

				Assert.AreEqual(beforeState.ToString(), afterState.ToString(), "The Datatable was expected to remain unchanged");
			}
			/*
				var masterDataSchemeImporter = new PpsMasterData.PpsMasterDataSchemeImporter("testtable", true);
			masterDataSchemeImporter.AddLocalColumn("Testcol", "INTEGER", false, String.Empty, true);
			masterDataSchemeImporter.AddRemoteColumn("Testcol", typeof(int), false, String.Empty, true);
			var ret = masterDataSchemeImporter.RefreshScheme();

			// no commands must be returned

			Assert.AreNotEqual(ret, null);
			Assert.AreEqual(0, ret.Count);*/
		}
		/*
		[TestMethod]
		public void PpsMasterDataImportTest_DbTest_Droppable_Same()
		{
			using (var sqliteDataBase = new SQLiteConnection("Data Source=:memory:;DateTimeKind=Utc;foreign keys=true;new=true;"))
			{
				sqliteDataBase.Open();

				using (var sqlite = sqliteDataBase.CreateCommand())
				{
					// initialize the table
					sqlite.CommandText = "CREATE TABLE 'Table1' ( [Column1] INTEGER PRIMARY KEY NOT NULL, [Column2] TEXT NULL);";
					sqlite.ExecuteNonQuery();
					sqlite.CommandText = "INSERT INTO 'Table1' VALUES (1,'Testtext');";
					sqlite.ExecuteNonQuery();
				}

				var masterDataSchemeImporter = new PpsMasterData.PpsMasterDataSchemeImporter("Table1", false);

				masterDataSchemeImporter.AddLocalColumn("Column1", "INTEGER", false, String.Empty, true);
				masterDataSchemeImporter.AddLocalColumn("Column2", "TEXT", true, String.Empty);

				masterDataSchemeImporter.AddRemoteColumn("Column1", typeof(int), false, String.Empty, true);
				masterDataSchemeImporter.AddRemoteColumn("Column2", typeof(string), true, String.Empty);

				var ret = masterDataSchemeImporter.RefreshScheme();

				using (var sqlite = sqliteDataBase.CreateCommand())
					foreach (var command in ret)
					{
						sqlite.CommandText = command;
						sqlite.ExecuteNonQuery();
					}

				// check the results

				using (var sqlite = sqliteDataBase.CreateCommand())
				{
					sqlite.CommandText = "PRAGMA table_info('Table1')";
					var read = new List<string>();
					var reader = sqlite.ExecuteReader();
					reader.Read();
					Assert.AreEqual("Column1", reader.GetString(1));
					Assert.AreEqual("INTEGER", reader.GetString(2));
					Assert.AreEqual(true, reader.GetBoolean(3),"The column is NULLable.");
					Assert.AreEqual(true, reader.GetBoolean(5),"The column is not PrimaryKey.");
					reader.Read();
					Assert.AreEqual("Column2", reader.GetString(1));
					Assert.AreEqual("TEXT", reader.GetString(2));
					Assert.AreEqual(false, reader.GetBoolean(3), "The column is not NULLable.");
					Assert.AreEqual(false, reader.GetBoolean(5), "The column is PrimaryKey.");
					Assert.AreEqual(false, reader.Read(), "More columns than expected");
				}
				using (var sqlite = sqliteDataBase.CreateCommand())
				{
					sqlite.CommandText = "SELECT * FROM 'Table1';";
					Assert.AreEqual(false, sqlite.ExecuteReader().HasRows, "The table was not DROPped.");
				}
			}
		}

		[TestMethod]
		public void PpsMasterDataImportTest_DbTest_mustImport_AddColumn()
		{
			using (var sqliteDataBase = new SQLiteConnection("Data Source=:memory:;DateTimeKind=Utc;foreign keys=true;new=true;"))
			{
				sqliteDataBase.Open();

				using (var sqlite = sqliteDataBase.CreateCommand())
				{
					// initialize the table
					sqlite.CommandText = "CREATE TABLE 'Table1' ( [Column1] INTEGER PRIMARY KEY NOT NULL, [Column2] TEXT NULL);";
					sqlite.ExecuteNonQuery();
					sqlite.CommandText = "INSERT INTO 'Table1' VALUES (1,'Testtext');";
					sqlite.ExecuteNonQuery();
				}

				var masterDataSchemeImporter = new PpsMasterData.PpsMasterDataSchemeImporter("Table1", true);

				masterDataSchemeImporter.AddLocalColumn("Column1", "INTEGER", false, String.Empty, true);
				masterDataSchemeImporter.AddLocalColumn("Column2", "TEXT", true, String.Empty);

				masterDataSchemeImporter.AddRemoteColumn("Column1", typeof(int), false, String.Empty, true);
				masterDataSchemeImporter.AddRemoteColumn("Column2", typeof(string), true, String.Empty);
				masterDataSchemeImporter.AddRemoteColumn("Column3", typeof(string), false, "Defaulttext");

				var ret = masterDataSchemeImporter.RefreshScheme();

				using (var sqlite = sqliteDataBase.CreateCommand())
					foreach (var command in ret)
					{
						sqlite.CommandText = command;
						sqlite.ExecuteNonQuery();
					}

				// check the results

				using (var sqlite = sqliteDataBase.CreateCommand())
				{
					sqlite.CommandText = "PRAGMA table_info('Table1')";
					var read = new List<string>();
					var reader = sqlite.ExecuteReader();
					reader.Read();
					Assert.AreEqual("Column1", reader.GetString(1));
					Assert.AreEqual("INTEGER", reader.GetString(2));
					Assert.AreEqual(true, reader.GetBoolean(3), "The column is NULLable.");
					Assert.AreEqual(true, reader.GetBoolean(5), "The column is not PrimaryKey.");
					reader.Read();
					Assert.AreEqual("Column2", reader.GetString(1));
					Assert.AreEqual("TEXT", reader.GetString(2));
					Assert.AreEqual(false, reader.GetBoolean(3), "The column is not NULLable.");
					Assert.AreEqual(false, reader.GetBoolean(5), "The column is PrimaryKey.");
					reader.Read();
					Assert.AreEqual("Column3", reader.GetString(1));
					Assert.AreEqual("TEXT", reader.GetString(2));
					Assert.AreEqual(true, reader.GetBoolean(3), "The column is not NULLable.");
					Assert.AreEqual(false, reader.GetBoolean(5), "The column is PrimaryKey.");
					Assert.AreEqual("'Defaulttext'", reader.GetString(4), "the DEFAULTvalue was not set");
					Assert.AreEqual(false, reader.Read(), "More columns than expected");
				}
				using (var sqlite = sqliteDataBase.CreateCommand())
				{
					sqlite.CommandText = "SELECT * FROM 'Table1';";
					var row = sqlite.ExecuteReader();
					Assert.AreEqual(true, row.HasRows, "The table was not properly expanded.");
					row.Read();
					Assert.AreEqual(1, row.GetInt64(0), "The table was not properly expanded.");
					Assert.AreEqual("Testtext", row.GetString(1), "The table was not properly expanded.");
					Assert.AreEqual("Defaulttext", row.GetString(2), "The table was not properly expanded.");
				}
			}
		}

		[TestMethod]
		public void PpsMasterDataImportTest_DbTest_mustImport_RemoveColumn()
		{
			using (var sqliteDataBase = new SQLiteConnection("Data Source=:memory:;DateTimeKind=Utc;foreign keys=true;new=true;"))
			{
				sqliteDataBase.Open();

				using (var sqlite = sqliteDataBase.CreateCommand())
				{
					// initialize the table
					sqlite.CommandText = "CREATE TABLE 'Table1' ( [Column1] INTEGER PRIMARY KEY NOT NULL, [Column2] TEXT NULL);";
					sqlite.ExecuteNonQuery();
					sqlite.CommandText = "INSERT INTO 'Table1' VALUES (1,'Testtext');";
					sqlite.ExecuteNonQuery();
				}

				var masterDataSchemeImporter = new PpsMasterData.PpsMasterDataSchemeImporter("Table1", true);

				masterDataSchemeImporter.AddLocalColumn("Column1", "INTEGER", false, String.Empty, true);
				masterDataSchemeImporter.AddLocalColumn("Column2", "TEXT", true, String.Empty);

				masterDataSchemeImporter.AddRemoteColumn("Column1", typeof(int), false, String.Empty, true);

				var ret = masterDataSchemeImporter.RefreshScheme();

				using (var sqlite = sqliteDataBase.CreateCommand())
					foreach (var command in ret)
					{
						sqlite.CommandText = command;
						sqlite.ExecuteNonQuery();
					}

				// check the results

				using (var sqlite = sqliteDataBase.CreateCommand())
				{
					sqlite.CommandText = "PRAGMA table_info('Table1')";
					var read = new List<string>();
					var reader = sqlite.ExecuteReader();
					reader.Read();
					Assert.AreEqual("Column1", reader.GetString(1));
					Assert.AreEqual("INTEGER", reader.GetString(2));
					Assert.AreEqual(true, reader.GetBoolean(3), "The column is NULLable.");
					Assert.AreEqual(true, reader.GetBoolean(5), "The column is not PrimaryKey.");
					Assert.AreEqual(false,reader.Read(),"More columns than expected");
				}
				using (var sqlite = sqliteDataBase.CreateCommand())
				{
					sqlite.CommandText = "SELECT * FROM 'Table1';";
					var row = sqlite.ExecuteReader();
					Assert.AreEqual(true, row.Read(), "The table was not properly shrinked.");
					Assert.AreEqual(1, row.GetInt64(0), "The table was not properly shrinked.");
				}
				using (var sqlite = sqliteDataBase.CreateCommand())
				{
					sqlite.CommandText = "SELECT * FROM 'sqlite_master' WHERE [type]='table';";
					var row = sqlite.ExecuteReader();
					var rowCount = 0;
					while (row.Read())
						rowCount++;
					Assert.AreEqual(1, rowCount, "Tablecount mismatch.");
				}
				using (var sqlite = sqliteDataBase.CreateCommand())
				{
					sqlite.CommandText = "SELECT * FROM 'sqlite_master' WHERE [type]='index';";
					var row = sqlite.ExecuteReader();
					var rowCount = 0;
					while (row.Read())
						rowCount++;
					Assert.AreEqual(1, rowCount, "Indexcount mismatch.");
				}
			}
		}

		[TestMethod]
		public void PpsMasterDataImportTest_DbTest_mustImport_ChangeColumn()
		{
			using (var sqliteDataBase = new SQLiteConnection("Data Source=:memory:;DateTimeKind=Utc;foreign keys=true;new=true;"))
			{
				sqliteDataBase.Open();

				using (var sqlite = sqliteDataBase.CreateCommand())
				{
					// initialize the table
					sqlite.CommandText = "CREATE TABLE 'Table1' ( [Column1] INTEGER PRIMARY KEY NOT NULL, [Column2] TEXT NULL);";
					sqlite.ExecuteNonQuery();
					sqlite.CommandText = "INSERT INTO 'Table1' VALUES (1,'Testtext');";
					sqlite.ExecuteNonQuery();
				}

				var masterDataSchemeImporter = new PpsMasterData.PpsMasterDataSchemeImporter("Table1", true);

				masterDataSchemeImporter.AddLocalColumn("Column1", "INTEGER", false, String.Empty, true);
				masterDataSchemeImporter.AddLocalColumn("Column2", "TEXT", true, String.Empty);

				masterDataSchemeImporter.AddRemoteColumn("Column1", typeof(int), false, String.Empty, true);
				masterDataSchemeImporter.AddRemoteColumn("Column2", typeof(int), true, "42");

				var ret = masterDataSchemeImporter.RefreshScheme();

				using (var sqlite = sqliteDataBase.CreateCommand())
					foreach (var command in ret)
					{
						sqlite.CommandText = command;
						sqlite.ExecuteNonQuery();
					}

				// check the results

				using (var sqlite = sqliteDataBase.CreateCommand())
				{
					sqlite.CommandText = "PRAGMA table_info('Table1')";
					var read = new List<string>();
					var reader = sqlite.ExecuteReader();
					reader.Read();
					Assert.AreEqual("Column1", reader.GetString(1));
					Assert.AreEqual("INTEGER", reader.GetString(2));
					Assert.AreEqual(true, reader.GetBoolean(3), "The column is NULLable.");
					Assert.AreEqual(true, reader.GetBoolean(5), "The column is not PrimaryKey.");
					reader.Read();
					Assert.AreEqual("Column2", reader.GetString(1));
					Assert.AreEqual("INTEGER", reader.GetString(2));
					Assert.AreEqual(false, reader.GetBoolean(3), "The column is not NULLable.");
					Assert.AreEqual(false, reader.GetBoolean(5), "The column is PrimaryKey.");
					Assert.AreEqual(false, reader.Read(), "More columns than expected");
				}
				using (var sqlite = sqliteDataBase.CreateCommand())
				{
					sqlite.CommandText = "SELECT * FROM 'Table1';";
					var row = sqlite.ExecuteReader();
					Assert.AreEqual(true, row.Read(), "The table was not properly shrinked(lost items).");
					Assert.AreEqual(1, row.GetInt64(0), "The table was not properly shrinked(different items).");
					Assert.AreEqual(42, row.GetInt64(1), "The table was not properly shrinked(not set do default).");
				}
				using (var sqlite = sqliteDataBase.CreateCommand())
				{
					sqlite.CommandText = "SELECT * FROM 'sqlite_master' WHERE [type]='table';";
					var row = sqlite.ExecuteReader();
					var rowCount = 0;
					while (row.Read())
						rowCount++;
					Assert.AreEqual(1, rowCount, "Tablecount mismatch.");
				}
				using (var sqlite = sqliteDataBase.CreateCommand())
				{
					sqlite.CommandText = "SELECT * FROM 'sqlite_master' WHERE [type]='index';";
					var row = sqlite.ExecuteReader();
					var rowCount = 0;
					while (row.Read())
						rowCount++;
					Assert.AreEqual(1, rowCount, "Indexcount mismatch.");
				}
			}
		}*/
	}
}

