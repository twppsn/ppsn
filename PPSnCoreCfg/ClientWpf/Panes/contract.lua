
pushContract = command(
    function (args) : void
        runTask(PushDataAsync());
    end
);

newPosition = command(
    function (args) : void
	    do (trans = UndoManager:BeginTransaction("Neue Position"))
			local viewAupo = getView(Data:Head:First:AupoHead);
			viewAupo:Add({ Text = "Neue Position"});
 			trans:Commit();
		end;
    end
);

