
local function GetNextNumber(trans, lastNr, dataset) : string
	return ((lastNr or 0) + 1):ToString("00000");
end;

NextNumber = GetNextNumber;