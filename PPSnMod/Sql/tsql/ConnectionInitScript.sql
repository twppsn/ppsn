-- user objects aka tables
SELECT
		u.object_id,
		s.name, 
		u.name,
		ic.column_id
	FROM sys.objects u
		INNER JOIN sys.schemas s ON (u.schema_id = s.schema_id)
		INNER JOIN sys.indexes pk ON (u.object_id = pk.object_id and pk.is_primary_key = 1)
		INNER JOIN sys.index_columns ic ON (pk.object_id = ic.object_id and pk.index_id = ic.index_id)
	WHERE u.type = 'U';

-- user columns
SELECT
		c.object_id,
		c.column_id, 
		c.name, 
		c.system_type_id, 
		c.max_length, 
		c.precision, 
		c.scale, 
		c.is_nullable, 
		c.is_identity
	FROM sys.columns c
		INNER JOIN sys.objects t ON (c.object_id = t.object_id)
	WHERE t.type = 'U' and c.is_computed = 0;
