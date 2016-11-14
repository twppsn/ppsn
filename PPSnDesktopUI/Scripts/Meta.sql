--<info name="Meta" />

--<create />
CREATE TABLE [Meta]
(
	[Id] INTEGER PRIMARY KEY NOT NULL UNIQUE,
	[ResourceName] TEXT NOT NULL UNIQUE,
	[Revision] INTEGER NOT NULL,
	[LastModification] TEXT NOT NULL DEFAULT (DATETIME('now'))
);

-- If the meta structure will be changed in future. Add a table "Meta_1" with <convert previousTable="Meta" .