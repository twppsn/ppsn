SELECT 
		u.Id			AS Id
		,s.Name			AS Name
		,u.[Security]	AS [Security]
		,s.name			AS [Login]
		,s.[type]		AS PersLoginType
		,u.LoginVersion	AS LoginVersion
		,CASE type WHEN 'S' THEN loginproperty(s.name, 'PasswordHash') ELSE NULL END AS LoginHash
	FROM dbo.[User] AS u INNER JOIN
		sys.server_principals AS s ON s.name COLLATE database_default = u.[Login] COLLATE database_default AND s.[type] IN ('S', 'U') AND s.is_disabled = 0