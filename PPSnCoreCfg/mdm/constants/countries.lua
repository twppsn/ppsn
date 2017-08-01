const Guid typeof System.Guid;


-- Register
RegisterConstant {
	Guid = Guid:Parse("{7bc6d439-4b63-49a2-88c9-0856c55e783e}"),
	Nr = "LAND",
	Typ = "mdmCountries",
	Name = "Ländereinstellungen"
};


-- Create a revision
OnCreateRevision["mdmCountries"] = function (obj, data)

	data:Land:AddRange(
		Db.Main:ExecuteSingleResult {
			select = "dbo.Land",
			columnList = data:Land
		}
	);

end;

-- Create update LAND
OnAfterPush["mdmCountries"] = function (obj, data)

	Db.Main.ExecuteNoneResult {
		upsert = "dbo.Land",
		rows = data:Land
	};

end;