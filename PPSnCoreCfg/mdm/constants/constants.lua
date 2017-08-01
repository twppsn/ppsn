
-------------------------------------------------------------------------------
-- Registers the constant in the object table
function RegisterConstant(table)

	-- check table arguments
	assert(table.Guid, "Guid is empty.");
	assert(table.Nr, "Nr is empty.");
	assert(table.Name, "Name is empty.");

	RegisterInitializationAction(
		20000, "RegisterConstant",
		function () : void
			do (ctx = ImpersonateSystem())
				local objData = {
					Guid = table.Guid,
					Nr = table.Nr,
					Typ = table.Typ or ObjectType,
					MimeType = "text/dataset",
					IsRev = true
				};

				Db.Main.ExecuteNoneResult {
					upsert = "dbo.ObjK",
					on = { "Guid" },
					objData
				};

				local args = {
					upsert = "dbo.ObjT",
					on = { "Key", "UserId", "Class" },
					{
						ObjKId = objData.Id,
						Key = "Name",
						Class = 0,
						Value = table.Name,
						UserId = 0
					}
				};

				if table.Comment then
					args[2] = {
						ObjKId = objData.Id,
						Key = "Comment",
						Class = 0,
						Value = table.Comment,
						UserId = 0
					}
				end;

				Db.Main.ExecuteNoneResult(args);

				await(ctx:CommitAsync());
			end;
		end
	);
end;

OnCreateRevision["mdmConstant"] = function (obj, data)

	for i = 0, #data.Tables - 1, 1 do
		local dt = data.Tables[i];
		local sqlTable = dt.SqlTable;
		if sqlTable then
			dt:AddRange(
				Db.Main:ExecuteSingleResult {
					select = sqlTable,
					columnList = dt
				}
			);
		end;
	end;

end;

-- Create update LAND
OnAfterPush["mdmConstant"] = function (obj, data)

	for i = 0, #data.Tables - 1, 1 do
		local dt = data.Tables[i];
		local sqlTable = dt.SqlTable;
		if sqlTable then
			Db.Main:ExecuteNoneResult {
				upsert = sqlTable,
				rows = dt
			}
		end;
	end;

end;