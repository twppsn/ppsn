---
uid: ppsn.mod.sqlcommands
title: PPSnMod SQL-Befehle
---

# Select

Most shown functions can be combined.

```sql

Select any from the table, return every column
{
	select = "dbo.Table"
}

```

```sql

Select any from the table, return only columns Col1, Col2, Col3
{
	select = "dbo.Table",
	columnList = {
		"Col1",
		"Col2",
		"Col3"
	}
}

```

```sql

Select conditional (Col1 must has the value 2), return every column
{
	select = "dbo.Table",
	{
		Col1 = 2
	}
}

```

```sql

Select any from the table, return only columns Col1, Col2, Col3 and rename (''ALIAS'') Col3 to MyColumn
{
	select = "dbo.Table",
	columnList = {
		"Col1",
		"Col2",
		"Col3",
		Col3 = "MyColumn"
	}
}

```

```sql

Select any from the table, if Col3 is null or not existing, return the result of the function
{
	select = "dbo.Table"
	defaults = {
		Col3 = function (x) return (x["Col2"] + 42) end;
    }
}

```

```sql

Select existent and non-existent columns from the table
{
	select = "dbo.Table",
	columnList = { "Col1", "Col2", "Testcolumn" },
	defaults = { }
}

```

```sql

Select with Custom ''WHERE''-Clause
{
	select = "dbo.Table",
	columnList = { "Col1", "Col2", "Testcolumn" },
	where = "[Col1] = 42 AND [Col2] = 'A'""
}


```

select: supports also joins
	t1 alias1=t2 alias2 is a simple inner join
	t1>t2 is a left outer join
	t1<t2 is a right outer join

	t1>(t2=t3=t4) is t1 left outer join (t2 inner join t3 inner join t4)

defaults: LuaTable with key-value pairs for null or not existing columns, the value can be a function (row)
          an empty defaults table allows non existing columns

columnList: alias are allowed, because we can have more than one table

# Upsert/Merge

```sql

{
	upsert = "dbo.Table",
	columnList = { "Col1", "Col2", "Coln" }
	on = { "Col1", "Col2" }
	rows
}

 ```

columnList: column/alias list with all columns that will be touched.
on: optional modification of the on-clause. Compare columns.


rows: is possible to set an whole row set

# All

columnList: is an optional list of columns that has to be in the merge command, if this parameter is missing, only the first argument list is used.
			also IDataColumns is possible

__notrans: