--<info name="Meta_0" />

--<create />
CREATE TABLE [Meta_0]
(
	[Id] INTEGER PRIMARY KEY NOT NULL UNIQUE,
	[ResourceName] TEXT NOT NULL UNIQUE,
	[Revision] INTEGER NOT NULL,
	[LastModification] TEXT NOT NULL DEFAULT (DATETIME('now'))
);