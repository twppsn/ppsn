--<info name="SyncTokens" rev="0" />

--<create />
CREATE TABLE [SyncTokens]
(
	[Id] INTEGER PRIMARY KEY NOT NULL UNIQUE,	-- this is the SQLite ROWID
	[Name] TEXT NOT NULL UNIQUE,
	[SyncToken] INTEGER NOT NULL DEFAULT 0
);
