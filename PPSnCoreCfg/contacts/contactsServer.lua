
local function GetNextNumber(trans, lastNr, dataset) : string
	local curNr = lastNr and lastNr:sub(2) or 0;
	return "K" .. (curNr + 1):ToString("000000");
end;

NextNumber = GetNextNumber;