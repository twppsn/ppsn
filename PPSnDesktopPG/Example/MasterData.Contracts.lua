

Add = command(function () : void
	print("Hallo Welt");
	Data.KONT.First.KONTNAME = "Hallo Welt";
	--[[
	do (operation = Data:BeginOperation("Addresse anfügen"))
	
		Data.ADDR:Add(3, 1, "Addresse n");
		
		operation:Commit();
	end;
	]]
end);