--<info schema="main" name="Meta" />

--<create />
CREATE TABLE main.Meta
(
	Id INTEGER PRIMARY KEY NOT NULL UNIQUE,
	ResourceName TEXT NOT NULL UNIQUE,
	Revision INTEGER NOT NULL,
	LastModification TEXT NOT NULL DEFAULT (DATETIME('now'))
);
