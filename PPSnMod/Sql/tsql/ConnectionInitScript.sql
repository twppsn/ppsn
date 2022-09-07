-- user objects aka tables
SELECT
		u.object_id
		, s.name
		, u.name
		, ct.is_track_columns_updated_on
	FROM sys.objects u
		INNER JOIN sys.schemas s ON u.schema_id = s.schema_id
		LEFT OUTER JOIN sys.change_tracking_tables ct ON u.object_id = ct.object_id
	WHERE u.type = 'U';

-- user columns
SELECT
		c.object_id
		, c.column_id
		, c.name
		, c.system_type_id -- 3 --
		, cast(case when c.system_type_id in (231, 239) then c.max_length / 2 else c.max_length end as smallint)
		, c.precision -- 5 --
		, c.scale
		, c.is_nullable
		, c.is_identity
		, cast((case when ic.object_id is null then 0 else 1 end) as bit) -- 9 --
		, case when c.system_type_id in (240, 243) then uts.name + '.' + ut.name else null end -- 10 -- type name
		, null -- 11 -- xml catalog name
		, null -- 12 -- xml schema name
		, null -- 13 -- xml schema collection name
	FROM sys.columns c
		INNER JOIN sys.objects t ON (c.object_id = t.object_id)
		LEFT OUTER JOIN sys.indexes pk ON (t.object_id = pk.object_id and pk.is_primary_key = 1)
		LEFT OUTER JOIN sys.index_columns ic ON (pk.object_id = ic.object_id and pk.index_id = ic.index_id and ic.column_id = c.column_id)
		LEFT OUTER JOIN sys.types ut ON (c.user_type_id = ut.user_type_id)
		LEFT OUTER JOIN sys.schemas uts on (ut.schema_id = uts.schema_id)
	WHERE t.type = 'U' and c.is_computed = 0
	order by c.object_id, c.column_id;
-- foreign keys
SELECT 
		o.object_id
		, o.name
		, f.parent_object_id
		, f.parent_column_id
		, f.referenced_object_id
		, f.referenced_column_id
	FROM sys.objects o 
		INNER JOIN sys.foreign_key_columns f ON (o.object_id = f.constraint_object_id) 
;	
-- procedures and functions
SELECT 
		u.object_id
		, s.name
		, u.name
		, cast(case when u.[type] = 'FT' then 1 else 0 end as bit)
	FROM sys.objects u
		INNER JOIN sys.schemas s ON (u.schema_id = s.schema_id)
	WHERE u.[type] IN ('FN', 'FS', 'FT', 'TF', 'P', 'PC');
-- arguments
SELECT 
		object_id
		, case when parameter_id = 0 then '@RETURN_VALUE' else p.name end
		, cast(case 
			when parameter_id = 0 then 6 
			when is_output = 1 then 3
			else 1
		end as tinyint)
		, p.system_type_id -- 3 --
		, p.max_length
		, p.precision -- 5 -- datetime precision?
		, p.scale
		, has_default_value
		, case when p.system_type_id in (240, 243) then uts.name + '.' + ut.name else null end -- 8 -- type name
		, null --  9 -- xml catalog name
		, null -- 10 -- xml schema name
		, null -- 11 -- xml schema collection name
	FROM sys.parameters p
		LEFT OUTER JOIN sys.types ut ON (p.user_type_id = ut.user_type_id)
		LEFT OUTER JOIN sys.schemas uts on (ut.schema_id = uts.schema_id)
	ORDER BY object_id, parameter_id;
