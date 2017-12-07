
pushContract = command(
    function (args) : void
        UpdateSources();
		if Data:IsDirty or Data:Object:IsDocumentChanged then
			await(PushDataAsync());
		else
			msgbox("Es gibt keine Änderungen.", "Information");
		end
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

