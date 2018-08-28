PPSn = PPSn or {}

function PPSn.Hello()
    return {a=1;b="test";}
end;

function PPSn.GetContacts()
    ret = {};
    ppsni = Db.GetDatabase();
    ctcs = ppsni.ExecuteSingleResult { select= "dbo.Ktkt", columnList = { Name = "Kontaktname", ObjkId ="Id", Plz ="Postleitzahl"}};
    do (e = ctcs.GetEnumerator())
		if e.MoveNext() then -- there is (another) contact
			while true do
				table.insert(ret, {Name = e.Current.Kontaktname});
				if not e.MoveNext() then
					break
				end;
			end;
			return ret;
		else -- there was no single contact
			return nil;
		end;
	end;
end;