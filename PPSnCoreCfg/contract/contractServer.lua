
function GetNextNumber(trans, dataset) : string
	
	local row = trans:ExecuteSingleRow{
		sql = [[ select max(cast(Nr as bigint)) from dbo.Objk where Typ = 'sdsContract' ]]
	};
	
	return ((row[0] or 0) + 1):ToString("00000");
end;