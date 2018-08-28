
function UmlagernScan(code : string, von : int, nach : int) : table
	local row;
	local charge;
	local charge2;

	local t = { BARCODE = code };
	local pps = Pps.Transaction;

	Pps.BarcodeDecode(t);

	if t.TYP == '10' then -- Lagerort

		if von <= 0 then
			von = t.ID;
		else
			nach = t.ID;
		end;

	elseif t.TYP == '12' then -- WECH
		
		row = pps.ExecuteSingleRow {
			select = "pps.WECH=pps.WENR[WENRIDENT = WECHWENRIDENT]",
			columnList = {
				WENRID = "charge",
				WECHCHARGE = "charge2"
			},
			{ WECHID = t.ID }
		};

		charge = row.charge;
		charge2 = row.charge2;

	elseif t.TYP == '13' then -- WACH
		
		row = pps.ExecuteSingleRow {
			select = "pps.WACH=pps.WAKO[WAKOID = WACHWAKOID]",
			columnList = {
				WENRID = "charge",
				WECHCHARGE = "charge2"
			},
			{ WACHID = t.ID }
		};

		charge = row.charge;
		charge2 = row.charge2;
	else
		error("Unbekannter Code!");
	end;

	return {
		charge = charge,
		charge2 = charge2,
		von = von,
		nach = nach
	};
end;


function UmlagernBook(charge : string, charge2 : string, von : int, nach : int) : table
	return { Data = "neee" };
end;

Actions["umlagernScan"] =  {
	Security = "",
	Description = "Werde Scannvorgang umlagern aus",
	SafeCall = true,
	
	{  Name = "code" },
	{  Name = "von", Default = -1 },
	{  Name = "nach", Default = -1 },

	Method = UmlagernScan
};

Actions["umlagernBook"] =  {
	Security = "",
	Description = "Buche eine Umlagerung",
	SafeCall = true,

	{  Name = "charge" },
	{  Name = "charge2" },
	{  Name = "von", Default = -1 },
	{  Name = "nach", Default = -1 },

	Method = UmlagernBook
};