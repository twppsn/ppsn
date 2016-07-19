CREATE TABLE [dbo].[Kont]
(
	[Id] BIGINT NOT NULL CONSTRAINT pkKontId PRIMARY KEY IDENTITY (1,1), 
	[ObjkId] BIGINT NOT NULL CONSTRAINT fkKontObjkId REFERENCES dbo.Objk (Id), 
	[Name] NVARCHAR(100) NOT NULL,
	[LiefNr] NVARCHAR(50) NULL,
	[KundNr] NVARCHAR(50) NULL
)
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'PK',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Kont',
    @level2type = N'COLUMN',
    @level2name = N'Id'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'FK zu Objk',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Kont',
    @level2type = N'COLUMN',
    @level2name = N'ObjkId'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Name des Kontakts',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Kont',
    @level2type = N'COLUMN',
    @level2name = N'Name'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'ext. Lieferantennummer',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Kont',
    @level2type = N'COLUMN',
    @level2name = N'LiefNr'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'ext. Kundennummer',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Kont',
    @level2type = N'COLUMN',
    @level2name = N'KundNr'