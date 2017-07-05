# Simple Select

{
	select = "dbo.Table",
	selectList = { "Col1", "Col2", "Coln"},
	{
		Col1 = 2
	}
}


todo: selectList -> columns list

# Upsert/Merge

{
	upsert = "dbo.Table",
	columnList = { "Col1", "Col2", "Coln"}
}

columnList: is an optional list of columns that has to be in the merge command, if this parameter is missing, only the first argument list is used.
rows:
