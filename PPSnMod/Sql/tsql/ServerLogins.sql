SELECT 
		p.Id		    AS ID,
		p.Name		  AS NAME,
		p.Security	AS [SECURITY],
		s.name			AS [LOGIN],
		s.type			AS PERSLOGINTYPE,
		p.LoginVersion as LOGINVERSION,
		CASE s.type WHEN 'S' THEN loginproperty(s.name, 'PasswordHash') ELSE NULL END AS LOGINHASH
	FROM hrs.Pers AS p 
    INNER JOIN sys.server_principals AS s ON s.name COLLATE database_default = p.Login COLLATE database_default AND s.type IN ('S', 'U') AND s.is_disabled = 0