
print("Trace Line");

HalloEnabled = true;
Counter = 0;

HalloCommand = command(
	function (parameter) : void
		msgbox("Hallo Welt");
	end,
	function (parameter) : bool
		return HalloEnabled;
	end
);

Control.Commands:AddButton("100,120", "server_mail_uploadImage",
	command(
		function (args) : void
			msgbox("Test");
		end
	), nil, "Test Button der dazwischen ersccheinen sollte."
);

function Button_Click(sender, e)
	--Control.Title = "Geklickt";
	Counter = Counter + 1000;
end;