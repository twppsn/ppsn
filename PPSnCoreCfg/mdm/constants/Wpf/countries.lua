local landSource = createSource {
	Source = Data:Land,
	SortDescriptions = { "+ISO3" }
};


currentView = nil;
IsDataGridVisible = false;

cmdChangeView = command(
	function (args) : void
		ChangeView(args:Parameter);
	end
);

function ChangeView(constName) : void

	UpdateSources();

	-- RootItem
	if constName == nil then
		IsDataGridVisible = false;
		currentView = nil;
		currentConstName = "";
		return;
	end;

	local newView = nil;
	if constName == "land" then
		newView = landSource:View;
	end;
	if currentView == nil or newView ~= currentView then
		currentView = newView;
	end;

	-- new order?
	currentView:Refresh();
	-- goto top
	currentView:MoveCurrentToFirst();
	-- show
	IsDataGridVisible = true;
	-- ensure visibility
	local currentItem = currentView:CurrentItem;
	if currentItem ~= nil then
		listConst:ScrollIntoView(currentItem);
	end;
end;

pushCountries = command(
    function (args) : void
		UpdateSources();
		if Data:IsDirty or Data:Object:IsDocumentChanged then
			await(PushDataAsync());
		else
			msgbox("Es gibt keine Änderungen.", "Information");
		end
    end
);