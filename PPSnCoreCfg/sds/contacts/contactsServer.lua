
local function GetNextNumber(trans, lastNr, dataset) : string
	local curNr = lastNr and lastNr:sub(2) or 0;
	return "K" .. (curNr + 1):ToString("000000");
end;

local function mergeToSql(trans, obj, data)
	-- write kont table
	data:Head:Merge(trans, {
		ObjkId = obj.Id
	});

	-- write adre
	data:Adre:Merge(trans, {
		ObjkId = obj.Id
	});

	-- write ansp
	data:Ansp:Merge(trans, {
		ObjkId = obj.Id
	});
end;

-- overwrite NextNumber
NextNumber = GetNextNumber;

-- auto merge data
OnAfterPush["sds.contacts"] = mergeToSql;