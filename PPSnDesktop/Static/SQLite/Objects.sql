--<info schema="main" name="Objects" rev="0" />

--<create />
CREATE TABLE main.[Objects]
(
	[Id] INTEGER PRIMARY KEY NOT NULL UNIQUE, -- this is a the SQLite ROWID not the server site OBJKID
	[Guid] UNIQUEIDENTIFIER NOT NULL UNIQUE,
	[ServerId] INTEGER NULL,
	[ServerRevId] INTEGER NULL,
	[Typ] TEXT NOT NULL,
	[Nr] TEXT NULL,
	[Tags] TEXT NULL,
	[Document] BLOB NULL
);

--<convert />