--<info name="Objects" rev="2" />

--<create />
CREATE TABLE [Objects]
(
	[Id] INTEGER PRIMARY KEY NOT NULL UNIQUE,	-- this is a the SQLite ROWID not the server site OBJKID
	[ServerId] INTEGER NULL,					-- the unique server site id
	[Guid] UNIQUEIDENTIFIER NOT NULL UNIQUE,	-- unique object id
	[Typ] TEXT NOT NULL,						-- Typ of the object, to find the correct template
	[Nr] TEXT NULL,								-- User number of the object
	[SyncToken] INTEGER NOT NULL DEFAULT 0,		-- Server site synchronization token
	[IsRev] BIT NOT NULL DEFAULT 1,				-- Synchronize the document with push/pull
	[RemoteRevId] INTEGER NULL,					-- the last synchronized server site revision
	[PulledRevId] INTEGER NULL,					-- the server site revision of the pulled document
	[DocumentIsChanged] BIT NOT NULL DEFAULT 0,	-- is the current revision modified
	[DocumentIsLinked] BIT NOT NULL DEFAULT 0,  -- is the document data stored on disk
	[Document] BLOB NULL						-- current revision of the object
);

CREATE TABLE [ObjectTags]
(
	[Id] INTEGER PRIMARY KEY NOT NULL UNIQUE,
	[ObjectId] INTEGER NOT NULL REFERENCES [Objects] ([Id]) ON DELETE CASCADE,
	[Key] TEXT NOT NULL,															-- keyword
	[Class] INTEGER NOT NULL DEFAULT 0,												-- 0 => normal string field, 1 => Number fields
	[Value] TEXT NULL,																-- value of the keyword (is the value NULL then is equals the tag)
	[UserId] INTEGER NULL,															-- UserId that created the tag (null for system created)
	[SyncToken] INTEGER NOT NULL DEFAULT 0,
	UNIQUE ([ObjectId], [Key])
);

CREATE TABLE [ObjectLinks]
(
	[Id] INTEGER PRIMARY KEY NOT NULL UNIQUE,
	[ServerId] INTEGER NULL,						-- Server site Id of the link
	[ParentObjectId] INTEGER NULL CONSTRAINT fkParentObjectId REFERENCES [Objects] ([Id]) ON DELETE CASCADE,
	[LinkObjectId] INTEGER NULL CONSTRAINT fkLinkObjectId REFERENCES [Objects] ([Id]) ON DELETE NO ACTION,
    [OnDelete] CHAR(1) NOT NULL DEFAULT 0,			-- Delete,Null,Restrict
    [SyncToken] INTEGER NOT NULL					-- int64 time stamp
);

-- ----------------------------------------------------------------------------
--<convert previousRev="0" />
-- ----------------------------------------------------------------------------
DROP TABLE IF EXISTS [new_Objects];
DROP TABLE IF EXISTS [new_ObjectTags];

CREATE TABLE [new_Objects]
(
	[Id] INTEGER PRIMARY KEY NOT NULL UNIQUE,	-- this is a the SQLite ROWID not the server site OBJKID
	[ServerId] INTEGER NULL,					-- the unique server site id
	[Guid] UNIQUEIDENTIFIER NOT NULL UNIQUE,	-- unique object id
	[Typ] TEXT NOT NULL,						-- Typ of the object, to find the correct template
	[Nr] TEXT NULL,								-- User number of the object
	[StateChg] INTEGER NOT NULL DEFAULT 0,		-- Server site state information
	[IsRev] BIT NOT NULL DEFAULT 1,				-- Synchronize the document with push/pull
	[RemoteRevId] INTEGER NULL,					-- the last synchronized server site revision
	[PulledRevId] INTEGER NULL,					-- the server site revision of the pulled document
	[DocumentIsChanged] BIT NOT NULL DEFAULT 0,	-- is the current revision modified
	[Document] BLOB NULL						-- current revision of the object
);

INSERT INTO [new_Objects] ([Id], [ServerId], [Guid], [Typ], [Nr], [IsRev], [RemoteRevId], [PulledRevId], [DocumentIsChanged], [Document])
	SELECT [Id], [ServerId], [Guid], [Typ], [Nr], [IsRev], [RemoteRevId], [PulledRevId], [DocumentIsChanged], [Document]
		FROM [Objects];

CREATE TABLE [new_ObjectTags]
(
	[Id] INTEGER PRIMARY KEY NOT NULL UNIQUE,
	[ObjectId] INTEGER NOT NULL REFERENCES [new_Objects] ([Id]) ON DELETE CASCADE,
	[Key] TEXT NOT NULL,															-- keyword
	[Class] INTEGER NOT NULL DEFAULT 0,												-- 0 => normal string field, 1 => Number fields
	[Value] TEXT NOT NULL,															-- value of the keyword
	UNIQUE ([ObjectId], [Key])
);

INSERT INTO [new_ObjectTags] ([Id], [ObjectId], [Key], [Class], [Value])
	SELECT [Id], [ObjectId], [Key], [Class], [Value]
		FROM [ObjectTags];

DROP TABLE [Objects];
DROP TABLE [ObjectTags];

ALTER TABLE [new_Objects] RENAME TO [Objects];
ALTER TABLE [new_ObjectTags] RENAME TO [ObjectTags];

-- ----------------------------------------------------------------------------
--<convert previousRev="1" />
-- ----------------------------------------------------------------------------
DROP TABLE IF EXISTS [new_Objects];
DROP TABLE IF EXISTS [new_ObjectTags];
DROP TABLE IF EXISTS [new_ObjectLinks];

CREATE TABLE [new_Objects]
(
	[Id] INTEGER PRIMARY KEY NOT NULL UNIQUE,	-- this is a the SQLite ROWID not the server site OBJKID
	[ServerId] INTEGER NULL,					-- the unique server site id
	[Guid] UNIQUEIDENTIFIER NOT NULL UNIQUE,	-- unique object id
	[Typ] TEXT NOT NULL,						-- Typ of the object, to find the correct template
	[Nr] TEXT NULL,								-- User number of the object
	[SyncToken] INTEGER NOT NULL DEFAULT 0,		-- Server site synchronization token
	[IsRev] BIT NOT NULL DEFAULT 1,				-- Synchronize the document with push/pull
	[RemoteRevId] INTEGER NULL,					-- the last synchronized server site revision
	[PulledRevId] INTEGER NULL,					-- the server site revision of the pulled document
	[DocumentIsChanged] BIT NOT NULL DEFAULT 0,	-- is the current revision modified
	[DocumentIsLinked] BIT NOT NULL DEFAULT 0,  -- is the document data stored on disk
	[Document] BLOB NULL						-- current revision of the object
);

INSERT INTO [new_Objects] ([Id], [ServerId], [Guid], [Typ], [Nr], [IsRev], [SyncToken], [RemoteRevId], [PulledRevId], [DocumentIsChanged], [DocumentIsLinked], [Document])
	SELECT [Id], [ServerId], [Guid], [Typ], [Nr], [IsRev], [StateChg], [RemoteRevId], [PulledRevId], [DocumentIsChanged], 0, [Document] FROM main.[Objects];

CREATE TABLE [new_ObjectTags]
(
	[Id] INTEGER PRIMARY KEY NOT NULL UNIQUE,
	[ObjectId] INTEGER NOT NULL REFERENCES [new_Objects] ([Id]) ON DELETE CASCADE,
	[Key] TEXT NOT NULL,															-- keyword
	[Class] INTEGER NOT NULL DEFAULT 0,												-- 0 => normal string field, 1 => Number fields
	[Value] TEXT NULL,																-- value of the keyword (is the value NULL then is equals the tag)
	[UserId] INTEGER NULL,															-- UserId that created the tag (null for system created)
	[SyncToken] INTEGER NOT NULL DEFAULT 0,
	UNIQUE ([ObjectId], [Key])
);

INSERT INTO [new_ObjectTags] ([Id], [ObjectId], [Key], [Class], [Value], [UserId])
	SELECT [Id], [ObjectId], [Key], [Class], [Value], null FROM main.[ObjectTags];

CREATE TABLE [new_ObjectLinks]
(
	[Id] INTEGER PRIMARY KEY NOT NULL UNIQUE,
	[ServerId] INTEGER NULL,						-- Server site Id of the link
	[ParentObjectId] INTEGER NULL CONSTRAINT fkParentObjectId REFERENCES [Objects] ([Id]) ON DELETE CASCADE,
	[LinkObjectId] INTEGER NULL CONSTRAINT fkLinkObjectId REFERENCES [Objects] ([Id]) ON DELETE NO ACTION,
    [OnDelete] CHAR(1) NOT NULL DEFAULT 0,			-- Delete,Null,Restrict
    [SyncToken] INTEGER NOT NULL					-- int64 time stamp
);

DROP TABLE IF EXISTS [Objects];
DROP TABLE IF EXISTS [ObjectTags];
DROP TABLE IF EXISTS [ObjectLinks];

ALTER TABLE [new_Objects] RENAME TO [Objects];
ALTER TABLE [new_ObjectTags] RENAME TO [ObjectTags];
ALTER TABLE [new_ObjectLinks] RENAME TO [ObjectLinks];