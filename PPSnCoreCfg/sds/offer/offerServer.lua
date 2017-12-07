const OfferType = 'sdsOffer';

function GetNextNumber(trans, dataset) : string
	
	local row = trans:ExecuteSingleRow{
		sql = [[ select max(cast(Nr as bigint)) from dbo.Objk where Typ = 'sdsOffer' ]]
	};
	
	return ((row[0] or 0) + 1):ToString("00000");
end;

function GetOfferData(obj, ds)

	local trans = Db.Main;
	do (t = ds:BeginData())

		-- Kopfdaten --
		ds:Head:AddRange(
			trans:ExecuteSingleResult{
				select = [[
					dbo.ObjK
						=sds.Anko[Objk.Id = Anko.ObjkId]
				]],
				columnList = ds:Head,
				[1] = { Id = obj.Id, Typ = OfferType }
			}
		);

		if #ds:Head == 0 then
			error("Angebot nicht gefunden '{1},{0}:{2}":Format(obj.Id, obj.Nr, obj.Guid));
		end;

		-- Positionen --
		ds:Anpo:AddRange(
			trans:ExecuteSingleResult{
				select = [[
					sds.Anko
						=sds.Anpo[Anko.Id = Anpo.AnkoId]
				]],
				columnList = ds:Anpo,
				[1] = { ObjkId = obj.Id }
			}
		);

		ds:Commit();
	end;
end;

function mergeOfferToSql(obj, data)

	trans = Db.Main;

	-- write anko table
	trans:ExecuteNoneResult {
		upsert ="sds.Anko",
		rows = data:Head
	};

	-- write anpo
	trans:ExecuteNoneResult {
		upsert ="sds.Anpo",
		rows = data:Anpo
	};
	
end;

-- overwrite NextNumber
NextNumber = GetNextNumber;

-- auto merge data
OnAfterPush["sds.offer"] = mergeOfferToSql;

-- get offer data
OnCreateRevision["sds.offer"] = GetOfferData;