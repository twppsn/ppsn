--<info schema="main" name="Objects" rev="1" />

--<create />
CREATE TABLE main.[Objects]
(
	[Id] INTEGER PRIMARY KEY NOT NULL UNIQUE,	-- this is a the SQLite ROWID not the server site OBJKID
	[ServerId] INTEGER NULL,					-- the unique server site id
	[Guid] UNIQUEIDENTIFIER NOT NULL UNIQUE,	-- unique object id
	[Typ] TEXT NOT NULL,						-- Typ of the object, to find the correct template
	[Nr] TEXT NULL,								-- User number of the object
	[StateChg] INTEGER NOT NULL DEFAULT 0,      -- Server site state information
	[IsRev] BIT NOT NULL DEFAULT 1,             -- Synchronize the document with push/pull
	[RemoteRevId] INTEGER NULL,					-- the last synchronized server site revision
	[PulledRevId] INTEGER NULL,					-- the server site revision of the pulled document
	[DocumentIsChanged] BIT NOT NULL DEFAULT 0,	-- is the current revision modified
	[Document] BLOB NULL						-- current revision of the object
);

CREATE TABLE main.[ObjectTags]
(
	[Id] INTEGER PRIMARY KEY NOT NULL UNIQUE,
	[ObjectId] INTEGER NOT NULL CONSTRAINT fkObjectsId REFERENCES [Objects] ([Id]) ON DELETE CASCADE,
	[Key] TEXT NOT NULL,						-- keyword
	[Class] INTEGER NOT NULL DEFAULT 0,			-- 0 => normal string field, 1 => Number fields
	[Value] TEXT NOT NULL,						-- value of the keyword

	CONSTRAINT idxObjectIdKey UNIQUE ([ObjectId], [Key])
);

--<convert />

-- drop full content
