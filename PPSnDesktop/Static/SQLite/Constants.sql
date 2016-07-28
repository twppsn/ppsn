--<info schema="main" name="Constants" rev="0" />

--<create />
CREATE TABLE main.[Constants]
(
	[Id] INTEGER PRIMARY KEY NOT NULL UNIQUE,	-- this is a the SQLite ROWID not the server site OBJKID
	[ServerId] INTEGER NULL,					-- the unique server site id
	[Typ] TEXT NOT NULL,						-- Typ of the constant
	[IsActive] BIT NOT NULL DEFAULT 0,			-- Is the constant active
	[Sync] INTEGET NOT NULL DEFAULT 0,          -- Synchronisation key
	[Name] TEXT NULL							-- name of the constant
);

CREATE TABLE main.[ConstantTags]
(
	[Id] INTEGER PRIMARY KEY NOT NULL UNIQUE,
	[ConstantId] INTEGER NOT NULL CONSTRAINT fkConstantId REFERENCES [Constants] ([Id]) ON DELETE CASCADE,
	[Key] TEXT NOT NULL,						-- keyword
	[Value] TEXT NOT NULL,						-- value of the keyword

	CONSTRAINT idxOConstantIdKey UNIQUE ([ConstantId], [Key])
);

--<convert />