--
-- Licensed under the EUPL, Version 1.1 or - as soon they will be approved by the
-- European Commission - subsequent versions of the EUPL(the "Licence"); You may
-- not use this work except in compliance with the Licence.
--
-- You may obtain a copy of the Licence at:
-- http://ec.europa.eu/idabc/eupl
--
-- Unless required by applicable law or agreed to in writing, software distributed
-- under the Licence is distributed on an "AS IS" basis, WITHOUT WARRANTIES OR
-- CONDITIONS OF ANY KIND, either express or implied. See the Licence for the
-- specific language governing permissions and limitations under the Licence.
--

-- Imports
PpsStuff = PpsStuff or require "libs/PpsStuff";

local data = { 
	name = "PpsData"
};

local function coreMessage(t)
	-- send request
	io.write("datacmd>");
	table.WriteLson(t, io.write);
	io.write("\n");

	-- get answer
	local line = io.read();
	if line == nil then
		error("No data returned.");
	end;

	local result = table.FromLson(line);
	if result.error then
		error(result.error);
	end;

	return result;
end;

--
-- Defines a way to stream data from the client without any caching
--
function data.open(settings)

	-- open an cursor
	settings.cmd = "open";
	local readerData = coreMessage(settings);

	readerData.moveNext = function ()
		readerData.current = coreMessage({ cmd = "next", id = readerData.id }).row;
		return readerData.current;
	end;

	readerData.fetch = function (count)
		return coreMessage({ cmd = "nextc", id = readerData.id, count = count }).rows;
	end;

	readerData.close = function ()
		coreMessage { cmd = "close", id = readerData.id};
	end;

	return readerData;
end

--
-- get a list of data and cache it, in second pass the function will returned the cached data
--   name:		list name
--   columns:  to transfer
--   selector: todo!
--   order:
--
function data.loadList(settings)
	if not settings then
		error("loadList arguments are missing.");
	end;

	local listName = settings.name;
	if not listName then
		error("loadList has no name.");
	end;

	-- https://tex.stackexchange.com/questions/52067/storing-and-retrieving-data-in-tuc-file
	local list = job.datasets.getdata("lists", listName); -- check cache for data

	if not list then
		
		list = {};

		-- open the list
		local r = data.open(settings);

		-- fetch all rows
		while r.moveNext() do
			table.insert(list, r.current);
		end;
	
		-- clean up
		r.close();
	end;

	-- update cache, should be done in every pass
	job.datasets.setdata {
		name = "lists",
		tag = listName,
		data = list
	};

	return list;
end;


--
-- get a dataset and cache it, in second pass the function will returned the cached data
--   id: id of the object 
--       it is also possible to set a table { Guid = "{guid}", Id = 111, RevId = 111 };
--   filter: list of tables that should returned
--
function data.loadDataSet(settings)
	if not settings then
		error("LoadDataSet arguments are missing.");
	end;

	if not settings.id then
		error("LoadDataSet has no name.");
	end;

	local dataset = job.datasets.getdata("datasets", settings.name); -- check cache for dataset
	
	if not dataset then

		-- get dataset
		dataset = coreMessage {
			cmd = "data",
			id =  settings.id,
			filter = settings.filter
		};

		-- build relation functions
		-- todo:

	end;

	-- update cache, should be done in every pass
	job.datasets.setdata {
		name = "lists",
		tag = settings.id,
		data = dataset
	};

	return dataset;
end;


return data;
