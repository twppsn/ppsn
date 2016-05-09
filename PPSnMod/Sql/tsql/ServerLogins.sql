SELECT 
		p.Id			AS Id,
		p.Name			AS Name,
		p.[Security]	AS [Security],
		s.name			AS [Login],
		s.[type]		AS PersLoginType,
		p.LoginVersion	AS LoginVersion,
		CASE type WHEN 'S' THEN loginproperty(s.name, 'PasswordHash') ELSE NULL END AS LoginHash
	FROM hrs.Pers AS p INNER JOIN
		sys.server_principals AS s ON s.name COLLATE database_default = p.[Login] COLLATE database_default AND s.[type] IN ('S', 'U') AND s.is_disabled = 0