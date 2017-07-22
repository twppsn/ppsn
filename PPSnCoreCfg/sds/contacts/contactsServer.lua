
local function GetNextNumber(lastNr, dataset) : string
	local curNr = lastNr and lastNr:sub(2) or 0;
	return "K" .. (curNr + 1):ToString("000000");
end;

function mergeContactToSql(obj, data)

	trans = Db.Main;

	-- write kont table
	trans:ExecuteNoneResult {
		upsert ="dbo.Kont",
		rows = data:Head
	};

	-- write adre
	trans:ExecuteNoneResult {
		upsert ="dbo.Adre",
		rows = data:Adre
	};
	
	-- write ansp
	trans:ExecuteNoneResult {
		upsert ="dbo.Ansp",
		rows = data:Ansp
	};
end;

-- overwrite NextNumber
NextNumber = GetNextNumber;

-- auto merge data
OnAfterPush["sds.contacts"] = mergeContactToSql;