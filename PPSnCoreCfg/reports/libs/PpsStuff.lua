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

local module = {
	_name = "PpsStuff"
};

--
-- Create an environment, that can not call any lua function
--
local safeEnvironment = {};
local safeEnvironmentMetaTable = {
	__index = function (t, key)
		return nil;
	end,
	__newindex = function (t, key, value)
		return nil;
	end
};
setmetatable(safeEnvironment, safeEnvironmentMetaTable);
module.safeEnvironment = safeEnvironment;

--
-- tableToStringCore
--
local function tableToStringCore(table, emit, dumpall, r)

	local function writeValue(type, v)
		if type == "boolean" then
			if v then
				emit("true");
			else
				emit("false");
			end;
		elseif type == "number" then
			emit(tostring(v));
		elseif type == "string" then
			emit('"');
			emit(v); -- todo: escape
			emit('"');
		elseif dumpall then
			emit("<data " .. type .. ">");
		else
			error("Type is not supported: " .. type);
		end;
	end; -- writeValue

	r = r or 0;
	if r > 10 then
		if dumpall then
			emit("<Table Level to deep>");
			return;
		else
			error("Table structure is to deep.");
		end;
	end;

	local currentIndex = 1;
	emit("{");
	for k,v in pairs(table) do
		local n = tonumber(k);
		if n and currentIndex == n then -- emit array alement
			writeValue(type(v), v);
			currentIndex = n + 1;
		else -- emit key/value pair
			emit("[");
			writeValue(type(k), k);
			emit("] = ");

			local tmp = type(v);
			if tmp == "table" then
				tableToStringCore(v, emit, dumpall, r + 1);
			else
				writeValue(tmp, v);
			end;

		end;

		emit(",");
	end;
	emit("}");
end;

--
-- extend table
--
function table.WriteLson(t, output)
	tableToStringCore(t, output, false);
end;

function table.ToLson(t, writeAll)

	local buffer = {};

	local function addBuffer(s)
		table.insert(buffer, s);

		-- optimize array concatation
		for i= #buffer - 1, 1, -1 do
			if #buffer[i] > #buffer[i + 1] then
				break
			end;
			buffer[i] = buffer[i] .. table.remove(buffer)
		end;
	end; -- addBuffer

	-- convert
	tableToStringCore(t, addBuffer, writeAll);

	-- return buffer
	return table.concat(buffer);
end; -- table.ToLson

function table.FromLson(tableString)
	-- it does not work?
	-- because load points to loadstring?
	-- , "t", safeEnvironment
	return assert(load("return " .. tableString))(); 
end; -- table.FromLson

return module;