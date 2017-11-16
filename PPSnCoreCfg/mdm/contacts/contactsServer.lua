const ContactType = 'crmContacts';

local function GetNextNumber(lastNr, dataset) : string
	local curNr = lastNr and lastNr:sub(2) or 0;
	return "K" .. (curNr + 1):ToString("000000");
end;

function GetContactData(obj, ds)

	local trans = Db.Main;
	do (t = ds:BeginData())

	-- Kopfdaten --
		ds:Head:AddRange(
			trans:ExecuteSingleResult{
				select = [[
					dbo.ObjK
						=dbo.Kont[Objk.Id = Kont.ObjkId]
				]],
				columnList = ds:Head,
				[1] = { Id = obj.Id, Typ = ContactType }
			}
		);

		if #ds:Head == 0 then
			error("Kontakt nicht gefunden '{1},{0}:{2}":Format(obj.Id, obj.Nr, obj.Guid));
		end;

		-- Adressen --
		ds:Adre:AddRange(
			trans:ExecuteSingleResult{
				select = "dbo.Adre",
				columnList = ds:Adre,
				[1] = { ObjKId = obj.Id }
			}
		);

		-- Ansprechpartner --
		ds:Ansp:AddRange(
			trans:ExecuteSingleResult{
				select = [[
					dbo.Adre
						=dbo.Ansp[Adre.Id = Ansp.AdreId]
				]],
				columnList = ds:Ansp,
				defaults = {},
				[1] = { ObjKId = obj.Id }
			}
		);

		ds:Commit();
	end;
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
	
	-- Default-Values can not be passed as argument.
	--   Q&D: set ANSP.Std

	foreach row in data:Ansp do
		if row.Std == nil then
			row.Std = false;
		end;
	end;

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

-- get Contact data
OnCreateRevision["sds.contacts"] = GetContactData;