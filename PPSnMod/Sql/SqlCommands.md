# Simple Select

{
	select = "dbo.Table",
	columnList = { "Col1", "Col2", "Coln"},
	{
		Col1 = 2
	}
}


select: supports also joins
	t1 alias1=t2 alias2 is a simple inner join
	t1>t2 is a left outer join
	t1<t2 is a right outer join

	t1>(t2=t3=t4) is t1 left outer join (t2 inner join t3 inner join t4)

defaults: LuaTable with key-value pairs for null or not existing columns, the value can be a function (row)
          an empty defaults table allows non existing columns

columnList: alias are allowed, because we can have more than one table

# Upsert/Merge

{
	upsert = "dbo.Table",
	columnList = { "Col1", "Col2", "Coln"}
	rows
}

rows: is possible to set an whole row set

# All

columnList: is an optional list of columns that has to be in the merge command, if this parameter is missing, only the first argument list is used.
			also IDataColumns is possible

__notrans: