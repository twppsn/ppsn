--<info schema="main" name="Meta" />

--<create />
CREATE TABLE main.Meta
(
	Id INTEGER PRIMARY KEY NOT NULL UNIQUE,
	Res TEXT NOT NULL UNIQUE,
	Rev INTEGER NOT NULL,
	ResLastModification TEXT NOT NULL DEFAULT (DATETIME('now'))
);
INSERT INTO main.Meta (Id, Res, Rev) VALUES (0, 'OfflineCache.sql', 0);