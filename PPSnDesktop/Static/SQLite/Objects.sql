--<info schema="main" name="Objects" rev="0" />

--<create />
CREATE TABLE main.[Objects]
(
	[Id] INTEGER PRIMARY KEY NOT NULL UNIQUE,	-- this is a the SQLite ROWID not the server site OBJKID
	[Guid] UNIQUEIDENTIFIER NOT NULL UNIQUE,	-- unique object id
	[ServerId] INTEGER NULL,					-- the unique server site id
	[ServerRevId] INTEGER NULL,					-- the server seite revision of the pulled document
	[Typ] TEXT NOT NULL,						-- Typ of the object, to find the correct template
	[Nr] TEXT NULL,								-- User number of the object
	[Tags] TEXT NULL,							-- Tags and data values of the object (for the user)
	[DocumentIsChanged] BIT NOT NULL DEFAULT 0,	-- is the current revision modified
	[Document] BLOB NULL						-- current revision of the object
);

--<convert />