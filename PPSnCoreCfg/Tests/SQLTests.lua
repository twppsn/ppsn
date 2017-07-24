local function PrintTable(table, prologue)
    if table == nil then error("Table was null"); end;
    local tableenum = table.GetEnumerator();
    while tableenum:MoveNext() do
        local tablecolumns = tableenum:Current;
        local line = prologue;
        for i = 0, #(tablecolumns.Columns)-1, 1 do
            line = line .. " - " .. tablecolumns.Columns[i].Name.. tablecolumns[i];
            --print("table1: " .. tablecolumns.Columns[i].Name .. "[" .. tablecolumns.Columns[i].DataType .. "]" .. ":" .. tablecolumns[i]);
        end;
        print(line);
    end;
end;

local function InitSystem()
    UseNode("/ppsn")
end;

local function InitDbForSaneability()
    if Db == nil or Db.Main == nil then
        AssertFail("No Database connection!")
    end;
    Db.Main:ExecuteNoneResult({ sql = "CREATE TABLE [dbo].[#tests] ( id int NOT NULL, name varchar(10))"});
    Db.Main:ExecuteNoneResult({ sql = "INSERT INTO [dbo].[#tests] VALUES (1, 'test')"});
end;

local function CleanUp()
    if Db == nil or Db.Main == nil then
        AssertFail("No Database connection!")
    end;
    Db.Main:ExecuteNoneResult({ sql = "DROP TABLE IF EXISTS [dbo].[#tests]"});
end;

local function CompareDbResultCount(table1, table2)
    local table1count = 0;
    local table2count = 0;
    local table1enum = table1.GetEnumerator();
    while table1enum:MoveNext() do table1count = table1count + 1; end;
    local table2enum = table2.GetEnumerator();
    while table2enum:MoveNext() do table2count = table2count + 1; end;

    if table1count ~= table2count then return "Tables have different number of rows ("..table1count.." != "..table2count..")"; end;
end;

local function CompareDbLine(line1, line2, headersonly)
    if #(line2.Columns) ~= #(line1.Columns) then return "Columncount mismatch"; end;
    for i=0, #(line1.Columns)-1, 1 do
        if (((line1[i] ~= line2[i]) and (line1.Columns[i].DataType ~= clr.System.Byte[]:GetType())) or (line1.Columns[i].Name ~= line2.Columns[i].Name)) then return "Mismatch"; end;
    end;
end;

local function CompareResultTables(table1, table2, headersonly)
    local cntreturn = CompareDbResultCount(table1, table2);
    if cntreturn ~= nil then return cntreturn; end;

    local correct = true;
    local ran = false;
    
    local table1enum = table1.GetEnumerator();
    while table1enum:MoveNext() do
        correct = false;
        local table1row = table1enum:Current;
        local result = nil;

        local table2enum = table2.GetEnumerator();
        while table2enum:MoveNext() do
            ran = true;
            local table2row = table2enum:Current;
            result = CompareDbLine(table1row, table2row, headersonly);
            if result == nil then correct = true; break; end;
        end;
        if correct == false then return result; end;
    end;
    if ran == true then return; else return "Different"; end;
end;

function SaneabilityTest()
    InitSystem();
	InitDbForSaneability();
    local row = Db.Main:ExecuteSingleRow({ sql = "SELECT * FROM [dbo].[#tests] WHERE id=1"});
    AssertIsNotNull(row, "Could not SELECT an existing dataset.");
    AssertAreEqual("test",row["name"], "Test DB gives unplausible results");
    CleanUp();
end;

function SelectAnyTest()
    InitSystem();
    
    local validret = Db.Main:ExecuteSingleResult({ sql = "SELECT * FROM [dbo].[ObjK]"});
    local cmd = 
    {
	    select = "dbo.ObjK"
    }
    local ret = Db.Main:ExecuteSingleResult(cmd);
    
    local sametest = CompareResultTables(validret, ret);
    AssertIsNull(sametest, "Mismatch: " .. sametest)
end;

function SelectDistinctColumnsTest()
    InitSystem();
    
    local validret = Db.Main:ExecuteSingleResult({ sql = "SELECT [Id],[IsRev], [Guid] FROM [dbo].[ObjK]"});
    local cmd = 
    {
	    select = "dbo.ObjK",
        columnList = { "Id", "IsRev", "Guid"}
    }
    local ret = Db.Main:ExecuteSingleResult(cmd);

    local sametest = CompareResultTables(validret, ret, true);
    AssertIsNull(sametest, "Columns mismatch: " .. sametest)
end;

function SelectDistinctColumnsTest_Saneability()
    InitSystem();
    
    local validret = Db.Main:ExecuteSingleResult({ sql = "SELECT [Id],[IsRev], 1 AS [Guid] FROM [dbo].[ObjK]"});
    local cmd = 
    {
	    select = "dbo.ObjK",
        columnList = { "Id", "IsRev", "Guid"}
    }
    local ret = Db.Main:ExecuteSingleResult(cmd);

    local sametest = CompareResultTables(validret, ret, false);
    AssertIsNotNull(sametest, "Saneability error - Test functions may not work as expected");
end;

function SelectDistinctRowTest()
    InitSystem();
    
    local validid = Db.Main:ExecuteSingleRow({ sql = "SELECT [Id] FROM [dbo].[ObjK]"});

    AssertIsNotNull(validid, "The Table has no data to compare!");

    local validret = Db.Main:ExecuteSingleResult({ sql = "SELECT * FROM [dbo].[ObjK] WHERE [Id] = " .. validid["Id"]});
    local cmd = 
    {
	    select = "dbo.ObjK",
        {   
            Id = validid["Id"]
        }
    }
    local ret = Db.Main:ExecuteSingleResult(cmd);

    local sametest = CompareResultTables(validret, ret, false);
    AssertIsNull(sametest, "Mismatch: " .. sametest)
end;

function SelectWithDefaultFunctionTest()
    InitSystem();
    
    local validid = Db.Main:ExecuteSingleRow({ sql = "SELECT * FROM [dbo].[ObjK]"});
    AssertIsNotNull(validid, "The Table has no data to compare!");

    local oldvalue = validid["CurRevId"];

    Db.Main:ExecuteNoneResult({ sql = "UPDATE [dbo].[ObjK] SET [CurRevId] = null WHERE [Id] = " .. validid["Id"]});

    local cmd = 
    {
	    select = "dbo.ObjK",
        columnList = {"Id", "CurRevId"},
        {   
            Id = validid["Id"]
        },
        defaults = {
            CurRevId = function (x) return (x["Id"] + 42) end;
        }
    }

    local ret = Db.Main:ExecuteSingleRow(cmd);

    AssertAreEqual(ret["CurRevId"], validid["Id"] + 42, "Default function not applied - expected ''" .. validid["Id"] + 42 .. "'', returned ''" .. ret["CurRevId"] .. "''");

    Db.Main:ExecuteNoneResult({ sql = "UPDATE [dbo].[ObjK] SET [CurRevId] = " .. oldvalue .. " WHERE [Id] = " .. validid["Id"]});
end;

function SelectWithDefaultFunctionTest_Saneability()
    InitSystem();
    
    local validid = Db.Main:ExecuteSingleRow({ sql = "SELECT * FROM [dbo].[ObjK]"});
    AssertIsNotNull(validid, "The Table ObjK has no data to compare!");

    local validrev = Db.Main:ExecuteSingleRow({ sql = "SELECT MAX([Id]) FROM [dbo].[ObjR] WHERE [ObjkId] = " .. validid["Id"]});
    AssertIsNotNull(validrev, "The Table ObjR has no revision for the data!");

    local oldvalue = validid["CurRevId"];

    Db.Main:ExecuteNoneResult({ sql = "UPDATE [dbo].[ObjK] SET [CurRevId] = " .. validrev[0] .. " WHERE [Id] = " .. validid["Id"]});

    local cmd = 
    {
	    select = "dbo.ObjK",
        columnList = {"Id", "CurRevId"},
        {   
            Id = validid["Id"]
        },
        defaults = {
            CurRevId = function (x) return (x["Id"] + 42) end;
        }
    }

    local ret = Db.Main:ExecuteSingleRow(cmd);

    AssertAreEqual(ret["CurRevId"], validrev[0], "Default function applied while there was a value.");

    if oldvalue == Db.Null then oldvalue = "NULL"; end;
    Db.Main:ExecuteNoneResult({ sql = "UPDATE [dbo].[ObjK] SET [CurRevId] = " .. oldvalue .. " WHERE [Id] = " .. validid["Id"]});
end;

function SelectWithAliasTest()
    InitSystem();
    
    local validid = Db.Main:ExecuteSingleRow({ sql = "SELECT * FROM [dbo].[ObjK]"});

    AssertIsNotNull(validid, "The Table has no data to compare!");

    local cmd = 
    {
	    select = "dbo.ObjK",
        columnList = {"Id", "CurRevId", Id = "ColumnSameAsId"},
        {   
            Id = validid["Id"]
        }
    }

    local ret = Db.Main:ExecuteSingleRow(cmd);

    AssertIsNotNull(ret["ColumnSameAsId"], "Alias Column not in result.")

    AssertAreEqual(ret["ColumnSameAsId"], validid["Id"], "Target culumn not represented as alias.");
end;

function SelectWithNonExistingColumnTest()
    InitSystem();
    
    local validid = Db.Main:ExecuteSingleRow({ sql = "SELECT * FROM [dbo].[ObjK]"});

    AssertIsNotNull(validid, "The Table has no data to compare!");

    local cmd = 
    {
	    select = "dbo.ObjK",
        columnList = {"sys.Fields.Int", "Id", "Typ"},
        defaults = {}
    }

    local ret = Db.Main:ExecuteSingleRow(cmd);

    local found = false;
    for i = 0, #(ret.Columns)-1, 1 do
        if ret.Columns[i].Name == "_Col0" then found = true; end;
    end;

    AssertIsTrue(found, "Extra Column not in result.")
end;

function SelectWithWhereTest()
    InitSystem();
    
    local validid = Db.Main:ExecuteSingleRow({ sql = "SELECT * FROM [dbo].[ObjK]"});

    AssertIsNotNull(validid, "The Table has no data to compare!");

    local cmd = 
    {
	    select = "dbo.ObjK",
        where = "[Id] = " .. validid.Id
    }

    local ret = Db.Main:ExecuteSingleRow(cmd);

    local found = false;

    AssertIsNotNull(ret, "Nothing was returned.")

    AssertAreEqual(validid.Guid, ret.Guid, "A different Item was returned.")
end;

function SelectWithEmptyDefaultTest() 
    InitSystem();
 
    local columns = clr.TecWare.DE.Data.SimpleDataColumns(
        GetFieldDescription("dbo.Adre.Region"),
        GetFieldDescription("dbo.ObjK.Id")
    );
    local cmd = 
    {
	    select = "dbo.ObjK",
        columnList = columns,
        defaults = {}
    }
    local ret = Db.Main:ExecuteSingleResult(cmd);
    local retenum = ret.GetEnumerator();
    AssertIsTrue(retenum:MoveNext(), "The select gave no results.");

    AssertAreEqual("dbo.Adre.Region", retenum:Current.Columns[0].Name, "External Column was not returned.");
    AssertIsNull(retenum:Current[0], "External Column is supposed to be null, but resulted in: ''" .. retenum:Current[0] .. "''.");

    AssertAreEqual("dbo.ObjK.Id", retenum:Current.Columns[1].Name, "Internal Column was not returned.");
    AssertIsNotNull(retenum:Current[1], "Internal Column is supposed to be not null, but resulted in null.");
end;

function SelectWithEmptyDefaultAndAliasTest() 
    InitSystem();
 
    local columns = clr.TecWare.DE.Data.SimpleDataColumns(
        GetFieldDescription("dbo.Adre.Region"),
        GetFieldDescription("dbo.ObjK.Id")
    );
    local cmd = 
    {
	    select = "dbo.ObjK",
        columnList = columns,
        defaults = {}
    }
    local ret = Db.Main:ExecuteSingleResult(cmd);
    local retenum = ret.GetEnumerator();
    AssertIsTrue(retenum:MoveNext(), "The select gave no results.");

    AssertAreEqual("dbo.Adre.Region", retenum:Current.Columns[0].Name, "External Column was not returned.");
    AssertIsNull(retenum:Current[0], "External Column is supposed to be null, but resulted in: ''" .. retenum:Current[0] .. "''.");

    AssertAreEqual("dbo.ObjK.Id", retenum:Current.Columns[1].Name, "Internal Column was not returned.");
    AssertIsNotNull(retenum:Current[1], "Internal Column is supposed to be not null, but resulted in null.");
end;

function SelectWithLeftJoinTest()
    InitSystem();
    
    local validid = Db.Main:ExecuteSingleRow({ sql = "SELECT * FROM [dbo].[ObjK]"});

    AssertIsNotNull(validid, "The Table has no data to compare!");

    local validret = Db.Main:ExecuteSingleResult({ sql = "SELECT * FROM [dbo].[ObjK] k LEFT JOIN [dbo].[ObjR] r ON r.[Objkid] = k.[Id] WHERE k.[CurRevId]=r.[Id] AND k.[Id] = " .. validid["Id"] });

    local cmd = 
    {
	    select = "dbo.ObjK k > dbo.ObjR r [k.CurRevId = r.Id]",
        {   
            Id = validid["Id"]
        }
    }

    local ret = Db.Main:ExecuteSingleResult(cmd);

    local sametest = CompareResultTables(validret, ret, false);
    AssertIsNull(sametest, "Mismatch: " .. sametest)
end;

function SelectWithRightJoinTest()
    InitSystem();
    
    local validid = Db.Main:ExecuteSingleRow({ sql = "SELECT * FROM [dbo].[ObjK]"});

    AssertIsNotNull(validid, "The Table has no data to compare!");

    local validret = Db.Main:ExecuteSingleResult({ sql = "SELECT * FROM [dbo].[ObjK] k RIGHT JOIN [dbo].[ObjR] r ON r.[Objkid] = k.[Id] WHERE k.[CurRevId]=r.[Id] AND k.[Id] = " .. validid["Id"] });

    local cmd = 
    {
	    select = "dbo.ObjK k < dbo.ObjR r [k.CurRevId = r.Id]",
        {   
            Id = validid["Id"]
        }
    }

    local ret = Db.Main:ExecuteSingleResult(cmd);

    local sametest = CompareResultTables(validret, ret, false);
    AssertIsNull(sametest, "Mismatch: " .. sametest)
end;

function SelectWithInnerJoinTest()
    InitSystem();
    
    local validid = Db.Main:ExecuteSingleRow({ sql = "SELECT * FROM [dbo].[ObjK]"});

    AssertIsNotNull(validid, "The Table has no data to compare!");

    local validret = Db.Main:ExecuteSingleResult({ sql = "SELECT * FROM [dbo].[ObjK] k INNER JOIN [dbo].[ObjR] r ON r.[Id] = k.[CurRevId]" });
    
    local cmd = 
    {
	    select = "dbo.ObjK k = dbo.ObjR r [k.CurRevId = r.Id]"
    }

    local ret = Db.Main:ExecuteSingleResult(cmd);
    
    local sametest = CompareResultTables(validret, ret, false);
    AssertIsNull(sametest, "Mismatch: " .. sametest)
end;

function UpdateSingleRowTest()
    InitSystem();

    local testvalue = "Testvalue";

    local validid = Db.Main:ExecuteSingleRow({ sql = "SELECT * FROM [dbo].[ObjK]"});

    AssertIsNotNull(validid, "The Table has no data to compare!");

    local cmd = 
    {
	    update = "dbo.ObjK",
        {
            Id = validid["Id"], Nr=testvalue
        }
    }

    Db.Main:ExecuteNoneResult(cmd);

    local ret = Db.Main:ExecuteSingleRow({ sql = "SELECT * FROM [dbo].[ObjK] WHERE [Id] = " .. validid["Id"]});

    AssertAreEqual(ret["Nr"], testvalue, "The Database was not updated correctly. (expected: '" .. testvalue .. "', read: '" .. ret["Nr"] .. "', value before: '" .. validid["Nr"] .. "'")

    Db.Main:ExecuteNoneResult({ sql = "UPDATE [dbo].[ObjK] SET [Nr] = '" .. validid["Nr"] .. "' WHERE [Id] = " .. validid["Id"]});
end;

function UpdateSingleRowMustFailTest()
    InitSystem();

    local testvalue = "Testvalue";

    local validid = Db.Main:ExecuteSingleRow({ sql = "SELECT * FROM [dbo].[ObjK]"});

    AssertIsNotNull(validid, "The Table has no data to compare!");

    local cmd = 
    {
	    update = "dbo.ObjK",
        {
            Id = validid["Id"], IsRev=testvalue
        }
    }

    do
        Db.Main:ExecuteNoneResult(cmd);
    end(function (e: System.ArgumentException) return; end)

    AssertFail("An erroneus Update was executed!")

    Db.Main:ExecuteNoneResult({ sql = "UPDATE [dbo].[ObjK] SET [IsRev] = '" .. validid["IsRev"] .. "' WHERE [Id] = " .. validid["Id"]});
end;

function InsertSingleRowTest()
    InitSystem();
    const Guid typeof System.Guid;
    local tGuid = Guid("b6e8ec5c-f3b4-4ae6-8fff-a576b9b516bb");
    local tTyp = "attachment";
    local tNr = "abc1";
    local tIsRev = false;

    local cmd = 
    {
	    insert = "dbo.ObjK",
        [1] = {
            Guid = tGuid,
            Typ = tTyp,
            Nr = tNr,
            IsRev = tIsRev
        }
    }

    Db.Main:ExecuteNoneResult(cmd);
    
    local validid = Db.Main:ExecuteSingleRow({ sql = ("SELECT * FROM [dbo].[ObjK] WHERE [Id] = " .. cmd[1].Id) });

    AssertAreEqual(tGuid, validid["Guid"], "The Insert was not executed correctly. (expected: '" .. tGuid .. "', read: '" .. validid["Guid"] .. "'")
    AssertAreEqual(tTyp, validid["Typ"], "The Insert was not executed correctly. (expected: '" .. tTyp .. "', read: '" .. validid["Typ"] .. "'")
    AssertAreEqual(tNr, validid["Nr"], "The Insert was not executed correctly. (expected: '" .. tNr .. "', read: '" .. validid["Nr"] .. "'")
    AssertAreEqual(tIsRev, validid["IsRev"], "The Insert was not executed correctly. (expected: '" .. cast(bool, tIsRev) .. "', read: '" .. validid["IsRev"] .. "'")

    Db.Main:ExecuteNoneResult({ sql = "DELETE FROM [dbo].[ObjK] WHERE [Id] = " .. validid["Id"]});
end;

function InsertMultiRowTest()
    print("This Test is supposed to fail - the function is not implemented at the moment.")
    
    InitSystem();
    const Guid typeof System.Guid;
    local t1Guid = Guid("b6e8ec5c-f3b4-4ae6-8fff-a576b9b516bb");
    local t1Typ = "attachment";
    local t1Nr = "abc1";
    local t1IsRev = false;

    local t2Guid = Guid("b6e8ec5c-f3b4-4ae6-8fff-a576b9b516bc");
    local t2Typ = "attachment";
    local t2Nr = "abc2";
    local t2IsRev = false;

    local cmd = 
    {
	    insert = "dbo.ObjK",
        [1] = {
            Guid = t1Guid,
            Typ = t1Typ,
            Nr = t1Nr,
            IsRev = t1IsRev
        },
        [2] = {
            Guid = t2Guid,
            Typ = t2Typ,
            Nr = t2Nr,
            IsRev = t2IsRev
        }
    }

    Db.Main:ExecuteNoneResult(cmd);
    
    local validid = Db.Main:ExecuteSingleRow({ sql = ("SELECT * FROM [dbo].[ObjK] WHERE [Id] = " .. cmd[1].Id) });

    AssertIsNotNull(cmd[1].Id, "First insert got no Id.")
    AssertAreEqual(t1Guid, validid["Guid"], "The Insert was not executed correctly. (expected: '" .. t1Guid .. "', read: '" .. validid["Guid"] .. "'")
    AssertAreEqual(t1Typ, validid["Typ"], "The Insert was not executed correctly. (expected: '" .. t1Typ .. "', read: '" .. validid["Typ"] .. "'")
    AssertAreEqual(t1Nr, validid["Nr"], "The Insert was not executed correctly. (expected: '" .. t1Nr .. "', read: '" .. validid["Nr"] .. "'")
    AssertAreEqual(t1IsRev, validid["IsRev"], "The Insert was not executed correctly. (expected: '" .. t1IsRev .. "', read: '" .. validid["IsRev"] .. "'")

    Db.Main:ExecuteNoneResult({ sql = "DELETE FROM [dbo].[ObjK] WHERE [Id] = " .. validid["Id"]});

    AssertIsNotNull(cmd[2].Id, "Second insert got no Id.")
    validid = Db.Main:ExecuteSingleRow({ sql = ("SELECT * FROM [dbo].[ObjK] WHERE [Id] = " .. cmd[2].Id) });

    AssertAreEqual(t2Guid, validid["Guid"], "The Insert was not executed correctly. (expected: '" .. t2Guid .. "', read: '" .. validid["Guid"] .. "'")
    AssertAreEqual(t2Typ, validid["Typ"], "The Insert was not executed correctly. (expected: '" .. t2Typ .. "', read: '" .. validid["Typ"] .. "'")
    AssertAreEqual(t2Nr, validid["Nr"], "The Insert was not executed correctly. (expected: '" .. t2Nr .. "', read: '" .. validid["Nr"] .. "'")
    AssertAreEqual(t2IsRev, validid["IsRev"], "The Insert was not executed correctly. (expected: '" .. t2IsRev .. "', read: '" .. validid["IsRev"] .. "'")

    Db.Main:ExecuteNoneResult({ sql = "DELETE FROM [dbo].[ObjK] WHERE [Id] = " .. validid["Id"]});
end;

local function UpdateMultiRowMultiInputTest()
    InitSystem();

    local testvalue1 = "Testvalue";
    local testvalue2 = "Testvalue";
    local old1 = "";
    local old2 = "";

    local olddb = Db.Main:ExecuteSingleResult({ sql = "SELECT * FROM [dbo].[ObjR]"});

    local tableenum = olddb.GetEnumerator();
    if tableenum:MoveNext() then old1 = tableenum:Current; else AssertFail("No Testdata.1"); end;
    if tableenum:MoveNext() then old2 = tableenum:Current; else AssertFail("No Testdata.2"); end;
    local cmd = 
    {
	    update = "dbo.ObjR",
        {
            {
                Id = old1["Id"], IsDocumentText=not old1["IsDocumentText"]
            },
            {
                Id = old2["Id"], IsDocumentText=not old2["IsDocumentText"]
            }
        },
        
    }

    Db.Main:ExecuteNoneResult(cmd);

    local ret = Db.Main:ExecuteSingleRow({ sql = "SELECT * FROM [dbo].[ObjR] WHERE [Id] = " .. old1["Id"]});
    AssertAreEqual(ret["IsDocumentText"], not old1["IsDocumentText"], "The Database was not updated correctly. (expected: '" .. not old1["IsDocumentText"] .. "', read: '" .. ret["IsDocumentText"] .. "', value before: '" .. old1["IsDocumentText"].. "'")

    local ret = Db.Main:ExecuteSingleRow({ sql = "SELECT * FROM [dbo].[ObjR] WHERE [Id] = " .. old2["Id"]});
    AssertAreEqual(ret["IsDocumentText"], not old2["IsDocumentText"], "The Database was not updated correctly. (expected: '" .. not old2["IsDocumentText"] .. "', read: '" .. ret["IsDocumentText"] .. "', value before: '" .. old2["IsDocumentText"].. "'")

    Db.Main:ExecuteNoneResult({ sql = "UPDATE [dbo].[ObjR] SET [IsDocumentText] = '" .. old1["IsDocumentText"] .. "' WHERE [Id] = " .. old1["Id"]});
    Db.Main:ExecuteNoneResult({ sql = "UPDATE [dbo].[ObjR] SET [IsDocumentText] = '" .. old2["IsDocumentText"] .. "' WHERE [Id] = " .. old2["Id"]});
end;

local function UpdateMultiRowSingleInputTest()
    InitSystem();

    local testvalue1 = "Testvalue";
    local testvalue2 = "Testvalue";
    local old1 = "";
    local old2 = "";

    local olddb = Db.Main:ExecuteSingleResult({ sql = "SELECT * FROM [dbo].[ObjR]"});

    local tableenum = olddb.GetEnumerator();
    if tableenum:MoveNext() then old1 = tableenum:Current; else AssertFail("No Testdata."); end;
    if tableenum:MoveNext() then old2 = tableenum:Current; else AssertFail("No Testdata."); end;
    local cmd = 
    {
	    update = "dbo.ObjR",
        {
            IsDocumentText=not old1["IsDocumentText"]
        }
    }

    Db.Main:ExecuteNoneResult(cmd);

    local ret = Db.Main:ExecuteSingleRow({ sql = "SELECT * FROM [dbo].[ObjR] WHERE [Id] = " .. old1["Id"]});

    AssertAreEqual(ret["IsDocumentText"], not old1["IsDocumentText"], "The Database was not updated correctly. (expected: '" .. not old1["IsDocumentText"] .. "', read: '" .. ret["IsDocumentText"] .. "', value before: '" .. old1["IsDocumentText"].. "'")

    Db.Main:ExecuteNoneResult({ sql = "UPDATE [dbo].[ObjR] SET [IsDocumentText] = '" .. old1["IsDocumentText"] .. "' WHERE [Id] = " .. old1["Id"]});
end;