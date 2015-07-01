
UndoManager.CanUndoChanged:add(function (sender, e) : void
	UndoCommand:Refresh();
end);
UndoManager.CanRedoChanged:add(function (sender, e) : void
	RedoCommand:Refresh();
end);

Add = command(function () : void
	print("Hallo Welt");
	do (operation = UndoManager:BeginTransaction("Kontaktname ändern"))
		Data.KONT.First.KONTNAME = "KONTNAME - Changed Value ";
		Data.KONT.First.KONTvorNAME = "KONTvorNAME - Changed Value ";
		operation:Commit();
	end;
end);


ShowStackCommand = command(function () : void
	local sb = clr.System.Text.StringBuilder();
	
	foreach c in UndoManager do
		sb:AppendLine(c.Type .. " - " .. c.Description);
	end;
	
	print(sb:ToString());
end);

UndoCommand = command(
	function () : void
		UndoManager:Undo();
	end,
	function () : bool
		return UndoManager.CanUndo;
	end
);

RedoCommand = command(
	function () : void
		UndoManager:Redo();
	end,
	function () : bool
		return UndoManager:CanRedo;
	end
);