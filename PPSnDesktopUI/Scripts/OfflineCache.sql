--<info schema="main" name="OfflineCache" rev="0" />

--<create />
CREATE TABLE main.OfflineCache
(
	Id INTEGER PRIMARY KEY NOT NULL UNIQUE,
	Path TEXT NOT NULL UNIQUE,		-- Path for the request
	OnlineMode INTEGER NOT NULL,	-- Is the file also in the online mode available
	ContentType TEXT NOT NULL,		-- MimeType of the file
	ContentEncoding TEXT NULL,		-- Encoding of the file
	ContentSize INTEGER NOT NULL,	-- Size of the file
	ContentLastModification TEXT NOT NULL, -- Last Modification
	ContentLink BIT NOT NULL DEFAULT 0, -- Is this file persisted on the disk
	Content BLOB NOT NULL			-- Link/Data
);

--<convert />