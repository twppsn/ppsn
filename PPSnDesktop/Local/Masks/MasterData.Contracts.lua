-------------------------------------------------------------------------------
-- Define Commands
-------------------------------------------------------------------------------

-- todo: Speichern, Rückgangig, Wiederholen -> durch maske selbst
SaveCommand = command(
	function (arg) : void
		msgbox("Speichern...");
	end
);

TestStackCommand = command(
	function (arg) : void
		
		local sb = clr.System.Text.StringBuilder();
		foreach c in UndoManager do
			sb:AppendLine(c.Type .. " - " .. c.Description);
		end;
		msgbox(sb:ToString());

	end
);

UndoCommand = command(
	function (arg) : void
		UndoManager:Undo();
	end,
	function (arg) : bool
		return UndoManager.CanUndo;
	end
);

RedoCommand = command(
	function (arg) : void
		UndoManager:Redo();
	end,
	function (arg) : bool
		return UndoManager.CanRedo;
	end
);


NewAddressCommand = command(
	function (arg) : void

		do (trans = UndoManager:BeginTransaction("Neue Addresse"))
			Data.KONT.First.ADRE:Add();
			trans:Commit();
		end;

	end
);

NewPartnerCommand = command(
	function (arg) : void
		do (trans = UndoManager:BeginTransaction("Neue Addresse"))
			local cur = APART_TreeView.SelectedValue;

			msgbox(cur.Table.Name);
			if cur.Table.Name == "ADRE" then
				cur.ANSP:Add();
			end;

			trans:Commit();
		end;
	end
);

RemovePosCommand = command(
	function (arg) : void
		msgbox("todo");
	end
);



--[[

Add = command(function () : void
	print("Hallo Welt");
	do (operation = UndoManager:BeginTransaction("Kontaktname ändern"))
		Data.KONT.First.KONTNAME = "KONTNAME - Changed Value ";
		Data.KONT.First.KONTvorNAME = "KONTvorNAME - Changed Value ";
		operation:Commit();
	end;
end);

]]