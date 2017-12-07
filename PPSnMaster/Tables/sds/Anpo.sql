CREATE TABLE [sds].[Anpo]
(
	[Id] BIGINT NOT NULL CONSTRAINT pkAnpoId PRIMARY KEY IDENTITY (1,1), 
	[AnkoId] BIGINT NOT NULL CONSTRAINT fkAnpoAnkoId REFERENCES sds.Anko (Id),
	[Pos] INT NOT NULL, 
	[ProdId] BIGINT NULL, 
	[ProdNr] NVARCHAR(20) NULL, 
	[Znr] NVARCHAR(50) NULL, 
	[Text] NVARCHAR(MAX) NULL, 
	[KontProdNr] NVARCHAR(50) NULL, 
	[Termin] NVARCHAR(50) NULL, 
	[Menge] DECIMAL(15, 4) NULL
)
GO
ALTER TABLE [sds].[Anpo] ENABLE CHANGE_TRACKING;

GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'PK',
    @level0type = N'SCHEMA',
    @level0name = N'sds',
    @level1type = N'TABLE',
    @level1name = N'Anpo',
    @level2type = N'COLUMN',
    @level2name = N'Id'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'FK zu Objk',
    @level0type = N'SCHEMA',
    @level0name = N'sds',
    @level1type = N'TABLE',
    @level1name = N'Anpo',
    @level2type = N'COLUMN',
    @level2name = N'AnkoId'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Position',
    @level0type = N'SCHEMA',
    @level0name = N'sds',
    @level1type = N'TABLE',
    @level1name = N'Anpo',
    @level2type = N'COLUMN',
    @level2name = N'Pos'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'FK zu Prod',
    @level0type = N'SCHEMA',
    @level0name = N'sds',
    @level1type = N'TABLE',
    @level1name = N'Anpo',
    @level2type = N'COLUMN',
    @level2name = N'ProdId'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Produktnummer',
    @level0type = N'SCHEMA',
    @level0name = N'sds',
    @level1type = N'TABLE',
    @level1name = N'Anpo',
    @level2type = N'COLUMN',
    @level2name = N'ProdNr'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Zeichnungsnummer',
    @level0type = N'SCHEMA',
    @level0name = N'sds',
    @level1type = N'TABLE',
    @level1name = N'Anpo',
    @level2type = N'COLUMN',
    @level2name = N'Znr'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Positionstext',
    @level0type = N'SCHEMA',
    @level0name = N'sds',
    @level1type = N'TABLE',
    @level1name = N'Anpo',
    @level2type = N'COLUMN',
    @level2name = N'Text'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Produktnummer des Kontakts',
    @level0type = N'SCHEMA',
    @level0name = N'sds',
    @level1type = N'TABLE',
    @level1name = N'Anpo',
    @level2type = N'COLUMN',
    @level2name = N'KontProdNr'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Termin',
    @level0type = N'SCHEMA',
    @level0name = N'sds',
    @level1type = N'TABLE',
    @level1name = N'Anpo',
    @level2type = N'COLUMN',
    @level2name = N'Termin'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Positionsmenge',
    @level0type = N'SCHEMA',
    @level0name = N'sds',
    @level1type = N'TABLE',
    @level1name = N'Anpo',
    @level2type = N'COLUMN',
    @level2name = N'Menge'
GO

CREATE INDEX [idxAnpoAnkoId] ON [sds].[Anpo] ([AnkoId])
GO
