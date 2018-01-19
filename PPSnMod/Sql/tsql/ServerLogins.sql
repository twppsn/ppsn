﻿SELECT 
		u.Id			AS Id
		,ISNULL(p.Name, s.name) AS Name
		,null           AS [Initial]
		,u.[Security]	AS [Security]
		,u.LoginVersion	AS LoginVersion
		,s.[type]		AS LoginType
		,s.name			AS [Login]
		,CASE type WHEN 'S' THEN loginproperty(s.name, 'PasswordHash') ELSE NULL END AS LoginHash
		,p.Id			AS [KtKtId]
	FROM dbo.[User] AS u 
		INNER JOIN sys.server_principals AS s ON (s.name COLLATE database_default = u.[Login] COLLATE database_default AND s.[type] IN ('S', 'U') AND s.is_disabled = 0)
		LEFT OUTER JOIN dbo.Pers p ON (u.PersId = p.Id)