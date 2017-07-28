const ContractType = 'sdsContract';

local function GetNextNumber(trans, lastNr, dataset) : string
	return ((lastNr or 0) + 1):ToString("00000");
end;

function GetContractData(obj, ds)

	local trans = Db.Main;
	do (t = ds:BeginData())

		-- Kopfdaten --
		ds:Head:AddRange(
			trans:ExecuteSingleResult{
				select = [[
					dbo.ObjK
						=sds.Auko[Objk.Id = Auko.ObjkId]
				]],
				columnList = ds:Head,
				[1] = { Id = obj.Id, Typ = ContractType }
			}
		);

		if #ds:Head == 0 then
			error("Auftrag nicht gefunden '{1},{0}:{2}":Format(obj.Id, obj.Nr, obj.Guid));
		end;

		-- Positionen --
		ds:Aupo:AddRange(
			trans:ExecuteSingleResult{
				select = [[
					sds.Auko
						=sds.Aupo[Auko.Id = Aupo.AukoId]
				]],
				columnList = ds:Aupo,
				[1] = { ObjKId = obj.Id }
			}
		);

		ds:Commit();
	end;
end;

function mergeContractToSql(obj, data)

	trans = Db.Main;

	-- write auko table
	trans:ExecuteNoneResult {
		upsert ="sds.Auko",
		rows = data:Head
	};

	-- write aupo
	trans:ExecuteNoneResult {
		upsert ="sds.Aupo",
		rows = data:Aupo
	};
	
end;

NextNumber = GetNextNumber;

-- auto merge data
OnAfterPush["sds.contract"] = mergeContractToSql;

-- get offer data
OnCreateRevision["sds.contract"] = GetContractData;