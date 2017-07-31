const Procs typeof TecWare.DE.Stuff.Procs;

function DebugEnv.InitSession(session)

end;

function DebugEnv.loadTable(fileName : string)

	local x = clr.System.Xml.Linq.XDocument:Load(fileName);
	return Procs:CreateLuaTable(x = x:Root);
end;
