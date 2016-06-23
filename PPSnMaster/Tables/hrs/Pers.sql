CREATE TABLE [hrs].[Pers]
(
	[Id] BIGINT NOT NULL CONSTRAINT pkPersId PRIMARY KEY IDENTITY (1,1), 
    [ObjkId] BIGINT NOT NULL CONSTRAINT fkPersObjkId REFERENCES dbo.Objk (Id), 
    [Name] NVARCHAR(100) NOT NULL, 
    [Geschlecht] CHAR(1) NULL, 
    [Freigabe] DATETIME NULL
)
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'PK',
    @level0type = N'SCHEMA',
    @level0name = N'hrs',
    @level1type = N'TABLE',
    @level1name = N'Pers',
    @level2type = N'COLUMN',
    @level2name = N'Id'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'FK zu Objk',
    @level0type = N'SCHEMA',
    @level0name = N'hrs',
    @level1type = N'TABLE',
    @level1name = N'Pers',
    @level2type = N'COLUMN',
    @level2name = N'ObjkId'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Name des Personals',
    @level0type = N'SCHEMA',
    @level0name = N'hrs',
    @level1type = N'TABLE',
    @level1name = N'Pers',
    @level2type = N'COLUMN',
    @level2name = N'Name'
GO

GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Geschlecht ''m'' oder ''w''',
    @level0type = N'SCHEMA',
    @level0name = N'hrs',
    @level1type = N'TABLE',
    @level1name = N'Pers',
    @level2type = N'COLUMN',
    @level2name = N'Geschlecht'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Freigabedatum',
    @level0type = N'SCHEMA',
    @level0name = N'hrs',
    @level1type = N'TABLE',
    @level1name = N'Pers',
    @level2type = N'COLUMN',
    @level2name = 'Freigabe'
GO
