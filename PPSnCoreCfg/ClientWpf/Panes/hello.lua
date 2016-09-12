		
data = { Name = "Test" };
        
clickCount = 1;
		
bindTitle = "Hello {0}":Format(Arguments.index);

local function waitTaskAsync(progressCreate)
	
	do (p = progressCreate())
		
		for i = 1, 10, 1 do
			p.Text = "Warte {0:N0}ms":Format(i * 300);
			p.Value = i * 1000;
			clr.System.Threading.Thread:Sleep(300);
		end;
	end;
	
end;
		
helloWorld = command(
	function (arg) : void
        data.Name = "Test {0}":Format(clickCount);
		bindTitle = "Hello World clicked {0} times.":Format(clickCount);
		clickCount = clickCount + 1;
	end
);

blockPane = command(
	function (arg) : void
		runTask(waitTaskAsync, disableUI);
	end
);

blockBk = command(
	function (arg) : void
		runTask(waitTaskAsync, TestBackgroundProgressState);
	end
);

blockGlobal = command(
	function (arg) : void
		runTask(waitTaskAsync, TestForegroundProgressState);
	end
);