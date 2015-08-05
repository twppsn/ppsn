
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

function Button_Click(sender, e)
	--Control.Title = "Geklickt";
	Counter = Counter + 1000;
end;