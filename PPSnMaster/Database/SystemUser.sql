-- User "System" muss vorhanden sein
IF NOT EXISTS (SELECT * FROM dbo.[User] WHERE Id = 0)
BEGIN
	SET IDENTITY_INSERT dbo.[User] ON;
	INSERT INTO dbo.[User] (Id, [Login]) VALUES (0, 'System');
	SET IDENTITY_INSERT dbo.[User] OFF;
END