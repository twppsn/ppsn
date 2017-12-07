
--ViewAdre = getView(Data:Head:First:AdreHead);

--[[
pushContact = command(
    function (args) : void
    	do (trans = UndoManager:BeginTransaction('Neue Adresse'))
    		
    		local ansp = getView(tvw:SelectedItem.AnspAdre);
    		local newRow = ansp:Add { Name = "Test4" };
			
			local tviParent = tvw:ItemContainerGenerator:ContainerFromItem(tvw:SelectedItem);
			tviParent.IsExpanded = true;
			
			local tviNew = tviParent:ItemContainerGenerator:ContainerFromItem(newRow);
			if tviNew ~= nil then
				tviNew.IsExpanded = true;
				tviNew.IsSelected = true;
			else
				msgbox("tvi is null");
			end;			

            trans:Commit();
        end;
        
        -- PushDataAsync();
    end
);
]]
		
pushContact = command(
    function (args) : void
		UpdateSources();
		if Data:IsDirty or Data:Object:IsDocumentChanged then
			await(PushDataAsync());
		else
			msgbox("Es gibt keine Änderungen.", "Information");
		end
    end
);

newAddress = command(
    function (args) : void
	    do (trans = UndoManager:BeginTransaction("Neue Adresse"))
			local viewAdre = getView(Data:Head:First:AdreHead);
			viewAdre:Add({ Name = "Neue Adresse"});
			
 			trans:Commit();
		end;
    end
);

newPartner = command(
    function (args) : void
        local cur = AdreTreeView.SelectedValue;
        if cur ~= nil then
            do (trans = UndoManager:BeginTransaction("Neuer Partner"))
                if cur:Table:TableName == "Adre" then
                    local viewAdre = getView(cur:AnspAdre);
                    viewAdre:Add({ Name = "Neuer Partner"});
                else
                    cur:Table:Add({AdreId = cur:AdreId, Name = "Neuer Partner"});
                end;
                trans:Commit();
            end;
		end;
	end
);
				
delItem = command(
    function (args) : void
  		local cur = AdreTreeView:SelectedValue;
        if cur ~= nil then
			do (trans = UndoManager:BeginTransaction("Löschen"))
 			    cur:Remove({"Löschen " .. (cur.Name)});
				trans:Commit();
			end;
		end;
    end
);

templateSelectorAdr = templateSelector(
    function (item, container) : object
    	if item == nil then
    		return nil;
		end;

		local resName = item:Table:TableName;
    	return GetResource(resName);
    end
);

testCommand = command(
    function (args) : void
  		msgbox("Test");
    end
);