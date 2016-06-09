--<info schema="main" name="OfflineCache" rev="0" />

--<create />
CREATE TABLE main.OfflineCache
(
	Id INTEGER PRIMARY KEY NOT NULL UNIQUE,
	Path TEXT NOT NULL UNIQUE,
	OnlineMode INTEGER NOT NULL,
	ContentType TEXT NOT NULL,
	ContentEncoding TEXT,
	ContentSize INTEGER NOT NULL,
	ContentLastModification TEXT NOT NULL DEFAULT (DATETIME('now')),
	Content BLOB NOT NULL
);

--<convert />