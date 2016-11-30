--<info name="Constants" rev="0" />

--<create />
CREATE TABLE [Constants]
(
	[Id] INTEGER PRIMARY KEY NOT NULL UNIQUE,	-- this is the SQLite ROWID, not the server site OBJKID
	[ServerId] INTEGER NULL,					-- the unique server site id
	[Typ] TEXT NOT NULL,						-- Type of the constant
	[IsActive] BIT NOT NULL DEFAULT 0,			-- Is the constant active
	[Sync] INTEGER NOT NULL DEFAULT 0,			-- Synchronisation key
	[Name] TEXT NULL							-- name of the constant
);

CREATE TABLE [ConstantTags]
(
	[Id] INTEGER PRIMARY KEY NOT NULL UNIQUE,
	[ConstantId] INTEGER NOT NULL REFERENCES [Constants] ([Id]) ON DELETE CASCADE,
	[Key] TEXT NOT NULL,															-- keyword
	[Value] TEXT NOT NULL,															-- value of the keyword
	UNIQUE ([ConstantId], [Key])
);