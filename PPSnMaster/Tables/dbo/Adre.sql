CREATE TABLE [dbo].[Adre]
(
	[Id] BIGINT NOT NULL CONSTRAINT pkAdreId PRIMARY KEY IDENTITY (1,1), 
    [ObjkId] BIGINT NOT NULL CONSTRAINT fkAdreObjkId REFERENCES dbo.Objk (Id), 
    [Name] NVARCHAR(100) NOT NULL, 
    [Adresse] NVARCHAR(1024) NOT NULL,
)

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
    @value = N'vollständige Adresse',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Adre',
    @level2type = N'COLUMN',
    @level2name = N'Adresse'
GO
