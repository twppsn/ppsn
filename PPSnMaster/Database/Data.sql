/*
Vorlage für ein Skript nach der Bereitstellung							
--------------------------------------------------------------------------------------
 Diese Datei enthält SQL-Anweisungen, die an das Buildskript angefügt werden.		
 Schließen Sie mit der SQLCMD-Syntax eine Datei in das Skript nach der Bereitstellung ein.			
 Beispiel:   :r .\myfile.sql								
 Verwenden Sie die SQLCMD-Syntax, um auf eine Variable im Skript nach der Bereitstellung zu verweisen.		
 Beispiel:   :setvar TableName MyTable							
               SELECT * FROM [$(TableName)]					
--------------------------------------------------------------------------------------
*/
DECLARE @data int = 1;

IF @data = 1
BEGIN TRANSACTION
SET IDENTITY_INSERT [dbo].[User] ON
INSERT INTO [dbo].[User] (Id, Login, Security, LoginVersion) VALUES (1, 'TECWARE\Stein', 'desSys;Chef',3)
INSERT INTO [dbo].[User] (Id, Login, Security, LoginVersion) VALUES (2, 'TECWARE\Hermann', 'desSys;Chef',3)
INSERT INTO [dbo].[User] (Id, Login, Security, LoginVersion) VALUES (3, 'TECWARE\Schmidt', 'desSys;Chef',3)
SET IDENTITY_INSERT [dbo].[User] OFF
ALTER TABLE [dbo].[Kgrp] DROP CONSTRAINT [fkKgrpKnst]
ALTER TABLE [dbo].[Ansp] DROP CONSTRAINT [fkAnspAdreId]
ALTER TABLE [dbo].[Waeh] DROP CONSTRAINT [fkWaehKnst]
ALTER TABLE [dbo].[Plzd] DROP CONSTRAINT [fkPlzdKons]
ALTER TABLE [dbo].[Plzd] DROP CONSTRAINT [fkPlzdLand]
ALTER TABLE [dbo].[Adre] DROP CONSTRAINT [fkAdreObjkId]
ALTER TABLE [dbo].[Adre] DROP CONSTRAINT [fkAdreLandId]
ALTER TABLE [dbo].[Land] DROP CONSTRAINT [fkLandKnst]
ALTER TABLE [dbo].[Kont] DROP CONSTRAINT [fkKontObjkId]
ALTER TABLE [dbo].[Kont] DROP CONSTRAINT [fkKontKgrpId]
ALTER TABLE [dbo].[Objr] DROP CONSTRAINT [fkObjrObjrId]
ALTER TABLE [dbo].[Objr] DROP CONSTRAINT [fkObjrObjkId]
ALTER TABLE [dbo].[Objr] DROP CONSTRAINT [fkObjrUserId]
ALTER TABLE [dbo].[Objk] DROP CONSTRAINT [fkObjkObjrCurId]
ALTER TABLE [dbo].[Objk] DROP CONSTRAINT [fkObjkObjrHeadId]
INSERT INTO [dbo].[Land] ([KnstId], [Name], [EnglishName], [Iso], [Iso3], [Tld], [Vorwahl], [Zone], [PostAdr]) VALUES (4, N'Deutschland', N'Germany', N'DE', N'DEU', N'.de', 49, 'EU', NULL)
INSERT INTO [dbo].[Land] ([KnstId], [Name], [EnglishName], [Iso], [Iso3], [Tld], [Vorwahl], [Zone], [PostAdr]) VALUES (5, N'Österreich', N'Austria', N'AT', N'AUT', N'.at', 43, 'EU', NULL)
INSERT INTO [dbo].[Land] ([KnstId], [Name], [EnglishName], [Iso], [Iso3], [Tld], [Vorwahl], [Zone], [PostAdr]) VALUES (6, N'Schweiz', N'Schwitzerland', N'CH', N'CHE', N'.ch', 41, NULL, NULL)
INSERT INTO [dbo].[Land] ([KnstId], [Name], [EnglishName], [Iso], [Iso3], [Tld], [Vorwahl], [Zone], [PostAdr]) VALUES (7, N'Frankreich', N'France', N'FR', N'FRA', N'.fr', 33, 'EU', NULL)
INSERT INTO [dbo].[Land] ([KnstId], [Name], [EnglishName], [Iso], [Iso3], [Tld], [Vorwahl], [Zone], [PostAdr]) VALUES (8, N'Russland', N'Russia', N'RU', N'RUS', N'.ru', 7, NULL, NULL)
SET IDENTITY_INSERT [dbo].[Knst] ON
INSERT INTO [dbo].[Knst] ([Id], [Typ], [IsActive], [Sync]) VALUES (1, N'Waeh', 1, '20160728 08:50:59.0733199')
INSERT INTO [dbo].[Knst] ([Id], [Typ], [IsActive], [Sync]) VALUES (2, N'Waeh', 1, '20160728 08:50:59.0773302')
INSERT INTO [dbo].[Knst] ([Id], [Typ], [IsActive], [Sync]) VALUES (3, N'Waeh', 1, '20160728 08:50:59.0783575')
INSERT INTO [dbo].[Knst] ([Id], [Typ], [IsActive], [Sync]) VALUES (4, N'Land', 1, '20160728 08:50:59.0793225')
INSERT INTO [dbo].[Knst] ([Id], [Typ], [IsActive], [Sync]) VALUES (5, N'Land', 1, '20160728 08:50:59.0803413')
INSERT INTO [dbo].[Knst] ([Id], [Typ], [IsActive], [Sync]) VALUES (6, N'Land', 1, '20160728 08:50:59.0813444')
INSERT INTO [dbo].[Knst] ([Id], [Typ], [IsActive], [Sync]) VALUES (7, N'Land', 1, '20160728 08:50:59.0823251')
INSERT INTO [dbo].[Knst] ([Id], [Typ], [IsActive], [Sync]) VALUES (8, N'Land', 1, '20160728 08:50:59.0828386')
INSERT INTO [dbo].[Knst] ([Id], [Typ], [IsActive], [Sync]) VALUES (9, N'Kgrp', 1, '20160728 08:50:59.0838247')
INSERT INTO [dbo].[Knst] ([Id], [Typ], [IsActive], [Sync]) VALUES (10, N'Kgrp', 1, '20160728 08:50:59.0848247')
INSERT INTO [dbo].[Knst] ([Id], [Typ], [IsActive], [Sync]) VALUES (11, N'Kgrp', 1, '20160728 08:50:59.0853386')
SET IDENTITY_INSERT [dbo].[Knst] OFF
INSERT INTO [dbo].[Waeh] ([KnstId], [Name], [Symbol], [Kurs], [Iso], [System]) VALUES (1, N'Euro', N'€    ', 1.0000, N'EUR', 1)
INSERT INTO [dbo].[Waeh] ([KnstId], [Name], [Symbol], [Kurs], [Iso], [System]) VALUES (2, N'US-Dollar', N'$    ', 1.0986, N'USD', 0)
INSERT INTO [dbo].[Waeh] ([KnstId], [Name], [Symbol], [Kurs], [Iso], [System]) VALUES (3, N'Pfund', N'£    ', 0.9323, N'GBP', 0)
INSERT INTO [dbo].[Kgrp] ([KnstId], [Name]) VALUES (9, N'Stahl')
INSERT INTO [dbo].[Kgrp] ([KnstId], [Name]) VALUES (10, N'Kunststoff')
INSERT INTO [dbo].[Kgrp] ([KnstId], [Name]) VALUES (11, N'Glas')
ALTER TABLE [dbo].[Kgrp]
    ADD CONSTRAINT [fkKgrpKnst] FOREIGN KEY ([KnstId]) REFERENCES [dbo].[Knst] ([Id])
ALTER TABLE [dbo].[Ansp]
    ADD CONSTRAINT [fkAnspAdreId] FOREIGN KEY ([AdreId]) REFERENCES [dbo].[Adre] ([Id])
ALTER TABLE [dbo].[Waeh]
    ADD CONSTRAINT [fkWaehKnst] FOREIGN KEY ([KnstId]) REFERENCES [dbo].[Knst] ([Id])
ALTER TABLE [dbo].[Plzd]
    ADD CONSTRAINT [fkPlzdKons] FOREIGN KEY ([KnstId]) REFERENCES [dbo].[Knst] ([Id])
ALTER TABLE [dbo].[Plzd]
    ADD CONSTRAINT [fkPlzdLand] FOREIGN KEY ([LandId]) REFERENCES [dbo].[Land] ([KnstId])
ALTER TABLE [dbo].[Adre]
    ADD CONSTRAINT [fkAdreObjkId] FOREIGN KEY ([ObjkId]) REFERENCES [dbo].[Objk] ([Id])
ALTER TABLE [dbo].[Adre]
    ADD CONSTRAINT [fkAdreLandId] FOREIGN KEY ([LandId]) REFERENCES [dbo].[Land] ([KnstId])
ALTER TABLE [dbo].[Land]
    ADD CONSTRAINT [fkLandKnst] FOREIGN KEY ([KnstId]) REFERENCES [dbo].[Knst] ([Id])
ALTER TABLE [dbo].[Kont]
    ADD CONSTRAINT [fkKontObjkId] FOREIGN KEY ([ObjkId]) REFERENCES [dbo].[Objk] ([Id])
ALTER TABLE [dbo].[Kont]
    ADD CONSTRAINT [fkKontKgrpId] FOREIGN KEY ([KgrpId]) REFERENCES [dbo].[Kgrp] ([KnstId])
ALTER TABLE [dbo].[Objr]
    ADD CONSTRAINT [fkObjrObjrId] FOREIGN KEY ([ParentId]) REFERENCES [dbo].[Objr] ([Id])
ALTER TABLE [dbo].[Objr]
    ADD CONSTRAINT [fkObjrObjkId] FOREIGN KEY ([ObjkId]) REFERENCES [dbo].[Objk] ([Id])
ALTER TABLE [dbo].[Objr]
    ADD CONSTRAINT [fkObjrUserId] FOREIGN KEY ([CreateUserId]) REFERENCES [dbo].[User] ([Id])
ALTER TABLE [dbo].[Objk]
    ADD CONSTRAINT [fkObjkObjrCurId] FOREIGN KEY ([CurRevId]) REFERENCES [dbo].[Objr] ([Id])
ALTER TABLE [dbo].[Objk]
    ADD CONSTRAINT [fkObjkObjrHeadId] FOREIGN KEY ([HeadRevId]) REFERENCES [dbo].[Objr] ([Id])
COMMIT TRANSACTION



/*BEGIN
	DELETE FROM dbo.Kont;
	DELETE FROM dbo.Adre;
	DELETE FROM dbo.Plzd;
	DELETE FROM dbo.Waeh;
	DELETE FROM dbo.Land;
	DELETE FROM dbo.Kgrp;
	DELETE FROM dbo.Knst;
	DECLARE @id int;

	-- Währungen
	INSERT INTO dbo.Knst (Typ, IsActive) values ('Waeh', 1);
	SET @id = @@IDENTITY;
	INSERT INTO dbo.Waeh (KnstId, Name, Symbol, Kurs, Iso, System) values (@id, 'Euro', '€', 1.0, 'EUR', 1);
	INSERT INTO dbo.Knst (Typ, IsActive) values ('Waeh', 1);
	SET @id = @@IDENTITY;
	INSERT INTO dbo.Waeh (KnstId, Name, Symbol, Kurs, Iso, System) values (@id, 'US-Dollar', '$', 1.0986, 'USD', 0);
	INSERT INTO dbo.Knst (Typ, IsActive) values ('Waeh', 1);
	SET @id = @@IDENTITY;
	INSERT INTO dbo.Waeh (KnstId, Name, Symbol, Kurs, Iso, System) values (@id, 'Pfund', '£', 0.9323, 'GBP', 0);
	
	-- Land
	INSERT INTO dbo.Knst (Typ, IsActive) values ('Land', 1);
	SET @id = @@IDENTITY;
	INSERT INTO dbo.Land (KnstId, Name, EnglishName, Tld, Vorwahl, Iso, Iso3, EuroZone) values (@id, 'Deutschland', 'Germany', '.de', '0049', 'DE', 'DEU', 1);
	INSERT INTO dbo.Knst (Typ, IsActive) values ('Land', 1);
	SET @id = @@IDENTITY;
	INSERT INTO dbo.Land (KnstId, Name, EnglishName, Tld, Vorwahl, Iso, Iso3, EuroZone) values (@id, 'Österreich', 'Austria', '.at', '0043', 'AT', 'AUT', 1);
	INSERT INTO dbo.Knst (Typ, IsActive) values ('Land', 1);
	SET @id = @@IDENTITY;
	INSERT INTO dbo.Land (KnstId, Name, EnglishName, Tld, Vorwahl, Iso, Iso3, EuroZone) values (@id, 'Schweiz', 'Schwitzerland', '.ch', '0041', 'CH', 'CHE', 0);
	INSERT INTO dbo.Knst (Typ, IsActive) values ('Land', 1);
	SET @id = @@IDENTITY;
	INSERT INTO dbo.Land (KnstId, Name, EnglishName, Tld, Vorwahl, Iso, Iso3, EuroZone) values (@id, 'Frankreich', 'France', '.fr', '0033', 'FR', 'FRA', 1);
	INSERT INTO dbo.Knst (Typ, IsActive) values ('Land', 1);
	SET @id = @@IDENTITY;
	INSERT INTO dbo.Land (KnstId, Name, EnglishName, Tld, Vorwahl, Iso, Iso3, EuroZone) values (@id, 'Russland', 'Russia', '.ru', '0007', 'RU', 'RUS', 0);

	-- Kontaktgruppen
	INSERT INTO dbo.Knst (Typ, IsActive) values ('Kgrp', 1);
	SET @id = @@IDENTITY;
	INSERT INTO dbo.Kgrp (KnstId, Name) values (@id, 'Stahl');
	INSERT INTO dbo.Knst (Typ, IsActive) values ('Kgrp', 1);
	SET @id = @@IDENTITY;
	INSERT INTO dbo.Kgrp (KnstId, Name) values (@id, 'Kunststoff');
	INSERT INTO dbo.Knst (Typ, IsActive) values ('Kgrp', 1);
	SET @id = @@IDENTITY;
	INSERT INTO dbo.Kgrp (KnstId, Name) values (@id, 'Glas');
END;*/