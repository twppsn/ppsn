
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
        runTask(PushDataAsync());
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
        local cur = ADR_TreeView.SelectedValue;
        if cur ~= nil then
            do (trans = UndoManager:BeginTransaction("Neuer Partner"))
                if cur:Table:TableName == "Adre" then
                    local viewAdre = getView(cur.AnspAdre);
                    viewAdre:Add({ Name = "Neuer Partner"});
                else
                    cur.Table:Add({AdreId = cur:AdreId, Name = "Neuer Partner"});
                end;
                trans:Commit();
            end;
		end;
	end
);
				
delItem = command(
    function (args) : void
  		local cur = ADR_TreeView:SelectedValue;
        if cur ~= nil then
			do (trans = UndoManager:BeginTransaction("Neuer Partner"))
 			    cur:Remove({"Löschen " .. (cur.Name)});
				trans:Commit();
			end;
		end;
    end
);