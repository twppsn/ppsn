
pushContract = command(
    function (args) : void
        runTask(PushDataAsync());
    end
);

newPosition = command(
    function (args) : void
	    do (trans = UndoManager:BeginTransaction("Neue Position"))
			local viewAnpo = getView(Data:Head:First:AnpoHead);
			viewAnpo:Add({ Text = "Neue Position"});
 			trans:Commit();
		end;
    end
);

