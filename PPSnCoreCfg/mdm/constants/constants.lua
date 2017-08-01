
-------------------------------------------------------------------------------
-- Registers the constant in the object table
function RegisterConstant(table)

	-- check table arguments
	assert(table.Guid, "Guid is empty.");
	assert(table.Nr, "Nr is empty.");
	assert(table.Typ, "Typ is empty.");
	assert(table.Name, "Name is empty.");

	RegisterInitializationAction(
		20000, "RegisterConstant",
		function () : void
			do (ctx = ImpersonateSystem())
				local objData = {
					Guid = table.Guid,
					Nr = table.Nr,
					Typ = table.Typ,
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
