
local function GetNextNumber(trans, lastNr, dataset) : string
	local curNr = lastNr:sub(2)
	return "K" .. ((curNr or 0) + 1):ToString("000000");
end;

NextNumber = GetNextNumber;