
print("Trace Line");

HalloEnabled = true;

HalloCommand = command(
	function (parameter) : void
		msgbox("Hallo Welt");
	end,
	function (parameter) : bool
		return HalloEnabled;
	end
);