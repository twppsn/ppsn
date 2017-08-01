CREATE TABLE [dbo].[Adre]
(
	[Id] BIGINT NOT NULL CONSTRAINT pkAdreId PRIMARY KEY IDENTITY (1,1),
	[ObjkId] BIGINT NOT NULL CONSTRAINT fkAdreObjkId REFERENCES dbo.ObjK (Id), 
	[Name] NVARCHAR(100) NOT NULL, 
	[Postfach] NVARCHAR(20) NULL, 
	[Zusatz] NVARCHAR(20) NULL, 
	[Strasse] NVARCHAR(50) NULL, 
	[Ort] NVARCHAR(50) NULL, 
	[Region] NVARCHAR(50) NULL, 
	[Plz] NVARCHAR(10) NULL, 
	[LandId] BIGINT NULL CONSTRAINT fkAdreLandId REFERENCES dbo.Land (Id), 
	--[Koord] GEOGRAPHY NULL,
	[Adresse] NVARCHAR(512) NULL, 
	[Template] NVARCHAR(512) NULL 
)
GO
ALTER TABLE [dbo].[Adre] ENABLE CHANGE_TRACKING;
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'PK',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Adre',
    @level2type = N'COLUMN',
    @level2name = N'Id'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'FK zu Objk',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Adre',
    @level2type = N'COLUMN',
    @level2name = N'ObjkId'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Adressname',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Adre',
    @level2type = N'COLUMN',
    @level2name = N'Name'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Postfach',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Adre',
    @level2type = N'COLUMN',
    @level2name = 'Postfach'
GO

EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'FK zu Land (bei Postanschrift immer engl. Namen anzeigen)',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Adre',
    @level2type = N'COLUMN',
    @level2name = N'LandId'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Adresszusatz (z.B. Wohnungsnummer)',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Adre',
    @level2type = N'COLUMN',
    @level2name = N'Zusatz'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Straße',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Adre',
    @level2type = N'COLUMN',
    @level2name = N'Strasse'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Stadt',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Adre',
    @level2type = N'COLUMN',
    @level2name = N'Ort'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Region (z.B. Bundesstaat)',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Adre',
    @level2type = N'COLUMN',
    @level2name = N'Region'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Postleitzahl',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Adre',
    @level2type = N'COLUMN',
    @level2name = N'Plz'
GO

CREATE INDEX [idxAdreObjkId] ON [dbo].[Adre] ([ObjkId])
GO
CREATE INDEX [idxAdreLandId] ON [dbo].[Adre] ([LandId])
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Adresse komplett',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Adre',
    @level2type = N'COLUMN',
    @level2name = N'Adresse'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'geändertes Template',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Adre',
    @level2type = N'COLUMN',
    @level2name = N'Template'
GO
