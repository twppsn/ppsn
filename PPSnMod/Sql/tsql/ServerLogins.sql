SELECT 
		p.PERSID		AS ID,
		p.PERSNAME		AS NAME,
		p.PERSSECURITY	AS [SECURITY],
		s.name			AS [LOGIN],
		s.type			AS PERSLOGINTYPE,
		p.PERSLOGINVERSION as LOGINVERSION,
		CASE type WHEN 'S' THEN loginproperty(s.name, 'PasswordHash') ELSE NULL END AS LOGINHASH
	FROM dbo.PERS AS p INNER JOIN
		sys.server_principals AS s ON s.name COLLATE database_default = p.PERSLOGIN COLLATE database_default AND s.type IN ('S', 'U') AND s.is_disabled = 0